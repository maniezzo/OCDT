using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using System.Data;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Globalization;
using System.Collections.Specialized;
using System.Xml.Linq;
using System.Linq.Expressions;
using System.Collections;

/* Data is in X, classes in Y. Attributes are columns of X
*/

namespace PlotTreeCsharp
{
   // Each node of the nonbinary decision tree
   public class NodeHeu
   {
      public NodeHeu() { }
      public NodeHeu(int id, int ndim, int nclasses) 
      {  this.id = id;
         this.visited = false;
         this.nPointClass = new int[nclasses];
         this.isUsedDim  = new bool[ndim];
         this.lstPoints  = new List<int> { };
         this.lstCuts    = new List<int> { };
         this.lstSons    = new List<int> { };
      }

      public int id;
      public int dim;      // dimension (attribute, column) associated with the node
      public int npoints;  // number of points (records) clustered in the node
      public int idFather; // id of father node 
      public bool visited; // node already visited during search
      public bool isLeaf;  // node is a leaf node
      public int[] nPointClass;   // number of node points of each class (sum is npoints)
      public bool[] isUsedDim;    // dimensions already used in the path to the node
      public List<int> lstPoints; // list of points clustered in the node (length = npoints)
      public List<int> lstCuts;   // id of the cuts associated with the node (see arrays cutdim and cutval)
      public List<int> lstSons;   // id of each offspring, in array decTree. If null, node is a leaf
   }

   /* DPtable : a list DPcells
    * DPcell  : node with info about costs and data for DP search
    * NodeDP: the partitions at the node + info
    */
   // a cell of the DP table
   public class DPcell
   {  public int id;
      public NodeDP node;     // the node 
      public int depth;       // distance from the root
      public int nnodes;      // number of tree nodes so far
      public bool isExpanded; // the cell was expanded
   }

   // Node of the exact tree, explicit point partitions into clusters
   public class NodeDP
   {  public NodeDP() 
      {  this.lstPartitions = new List<List<int>>();
         this.lstFathers    = new List<List<List<(int,int)>>>();
         this.usedDim       = new List<List<int?>>();
         this.lstPartClass  = new List<int>();
         this.lstPartDepth  = new List<int>();
      }
      public NodeDP(int id, int ndim, int nclasses) 
      {  this.id = id;
         this.lstPartitions = new List<List<int>> { };
         this.lstFathers    = new List<List<List<(int,int)>>> { };
         this.usedDim       = new List<List<int?>> { };
         this.lstPartClass  = new List<int>();
         this.lstPartDepth  = new List<int>();
      }

      public int id;
      public int idDPcell; // id of the DP table cell the node is set into
      public int npoints;  // number of points (records) clustered in the node
      public int hash;     // hash code of the partition
      public List<List<int?>> usedDim;      // dimensions used in the path to each partition of the node (will give the tree)
      public List<List<int>> lstPartitions; // list of the point in each partitions at the node
      public List<List<List<(int node,int part)>>> lstFathers; // list of father nodes (originating node/partition): depth->sons->fathers
      public List<int> lstPartClass;        // the class of each partition, -1 heterogeneous
      public List<int> lstPartDepth;        // the iDepth of each partition
   }

   internal class TreePlotter
   {  private double[,] X;
      private int[]     Y;
      private List<NodeHeu> decTree;  // l'albero euristico
      private List<DPcell>[] DPtable; // la tabella della dinamica, una riga ogni altezza dell'albero (max ndim)
      private List<int> leaf2node;  // mapping of tree leaves (parittions) to tree nodes
      private int[]     cutdim;     // dimension on which each cut acts
      private double[]  cutval;     // value where the cut acts
      private int[]     dimValues;  // number of values (of cuts) acting on each dimension
      private int numcol,ndim;      // num dimensions (attributes, columns of X)
      private int n, nclasses;      // num of records, num of classes
      private string splitRule;     // criterium for node plitting
      private string splitDir;      // max o min
      private string method;        // exact or heuristic
      private string[] dataColumns;
      private int totNodes=0, totcells = 0, treeHeight=0, totLeaves=0;

      public TreePlotter()
      {  decTree = new List<NodeHeu>();
      }
      public void run_plotter()
      {  string dataset = readConfig();
         Console.WriteLine($"Plotting {dataset}");
         readData(dataset); // gets the X and Y matrices (data and class)

         DPtable = new List<DPcell>[ndim+1]; // la radice (level 0) poi max un livello per dim (non fissi!)
         for(int i=0;i<ndim+1;i++) DPtable[i] = new List<DPcell>();

         if(method == "exact")
            exactTree();
         else
            heuristicTree();

         postProcessing();
         bool ok = checkSol();
         if(ok) plotTree(dataset);
      }

      private void exactTree()
      {  int i, j, d, idNode, iCell, iDepth, idDPcell=-1;
         NodeDP currNode;
         double maxVal = double.MaxValue; // limite superiore ai val da considerare per la dim corrente
         bool[] fOut   = new bool[n];     // punti da escludere

         List<int>[] dimCuts = new List<int>[numcol]; // which cuts for each dim
         for (i = 0; i < numcol; i++)        dimCuts[i] = new List<int>();
         for (i = 0; i < cutdim.Length; i++) dimCuts[cutdim[i]].Add(i);   // dimesioni su cui agisce ogni cut

         Console.WriteLine("Exact tree construction");
         int[,] idx;                                     // indices of sorted values for each column
         idx = getSortIdxAllDim();

         List<int> lstPoints = new List<int> ();         // indici punti del dataset
         for (i=0;i<n;i++) lstPoints.Add(i);
         List<int[]> lstNptClass = new List<int[]>();    // num punti di ogni slice individuata
         List<int>[] ptSlice = new List<int>[nclasses];  // punti delle slice

         // --------------------------------------------------------- node 0
         idNode = totNodes++;
         currNode = new NodeDP(idNode, ndim, nclasses); // inizializza anche lstPartitions, lstPartClass, usedDim
         currNode.lstPartitions.Add(new List<int>());
         currNode.lstFathers.Add(new List<List<(int,int)>>());
         currNode.lstFathers[0].Add(new List<(int,int)>());
         currNode.usedDim.Add(new List<int?>());
         currNode.lstPartClass.Add(-1);  // unica partizione, dati eterogenei
         currNode.lstPartDepth.Add(0);
         for (i=0;i<n;i++) currNode.lstPartitions[0].Add(i); // tutti i punti nell'unica partizione
         currNode.lstFathers[0][0].Add((0,0)); // no father node, root node
         currNode.npoints = n;
         currNode.hash    = nodeHash(currNode);

         DPcell dpc = new DPcell(); // insert the node in a cell of the DP table (it is its state)
         dpc.id     = totcells; totcells++;
         currNode.idDPcell = dpc.id;
         dpc.node   = currNode;
         dpc.depth  = 0;
         dpc.nnodes = 1;
         dpc.isExpanded = false;
         DPtable[0].Add(dpc);

         // espansione della tabella, per ogni livello (distanza dalla radice)
         for(iDepth=0;iDepth<DPtable.Length;iDepth++)
         {  // per ogni cella dek livello
            iCell = 0;
            while(iCell < DPtable[iDepth].Count)
            {  currNode = DPtable[iDepth][iCell].node;
               for (d = 0; d < ndim; d++)
                  if (dimValues[d] > 0)
                  {  idNode = expandNode(currNode, d, iDepth);
                     if(idDPcell < 0 && idNode >= 0) idDPcell = idNode;
                  }

               DPtable[iDepth][iCell].isExpanded = true;
               iCell++;
            }
         }

         if(idDPcell > 0)
            marshalTree(idDPcell);
      }

      /* raffina tutte le partizioni di un nodo lungo una dimensione 
       * (un nodo figlio per partizione)
       * ritorna idCell di un nodo completato, se trovato
       */
      private int expandNode(NodeDP nd, int d, int iDepth)
      {  int i,j,k,id,idcell,idpoint,idpart,npartitions,res=-1;
         bool isComplete;

         // add list of pointers to father one level below, if not yet initialized
         if(nd.lstFathers.Count == (iDepth+1)) nd.lstFathers.Add(new List<List<(int,int)>>());

         npartitions = nd.lstPartitions.Count;
         // ogni partizione del padre genera un figlio, con la partizione partizionata secondo dim d
         for(i=0;i<npartitions;i++)                   // for each partition of the father node
         {  if (nd.lstPartClass[i] >= 0)   continue;  // unique class, no expansion
            if (nd.usedDim[i].Contains(d)) continue;  // dimension already used to get the partition
            Console.WriteLine($" -- Partitioning node {nd.id} partition {i} along dim {d}");

            // qui ho il nodo figlio della partizione i-esima
            string jsonlst = JsonConvert.SerializeObject(nd);
            NodeDP newNode = JsonConvert.DeserializeObject<NodeDP>(jsonlst);
            newNode.id = totNodes++;

            // find original father partition
            int idFathPart = 0;  // partizione nodo padre
            int idFathNode = 0; // partizione nodo nonno (id nodo padre)
            if(nd.id==0) idFathPart = i;
            else
            {  int dim = (int) newNode.usedDim[i][newNode.usedDim[i].Count-1]; // last used dimension
               idpoint = newNode.lstPartitions[i][0];
               k = 0;      // index in cutdim
               while (cutdim[k] != dim) k++; // first value for dimension d
               idpart = 0; // id of the partiorn of the node in the father
               while (k < cutdim.Length && cutdim[k] == dim && cutval[k] < X[idpoint, dim]) // find the partition of idpoint
               {  k++;
                  idpart++;
               }
               idFathPart = idpart;
               idFathNode =i;
            }
            int newDepth = nd.lstPartDepth[i] + 1;   // depth dei nodi figli
            while (newNode.lstFathers.Count < newDepth+1) 
               newNode.lstFathers.Add(new List<List<(int,int)>>());

            // inizializzo le nuove partizioni del figlio
            List<int>        newPartClass  = new List<int>();    // la classe della partizione, -1 non univoca
            List<int>        newPartDepth  = new List<int>();
            List<(int,int)>  newFathers    = new List<(int,int)>();
            List<List<int>>  newpartitions = new List<List<int>>();
            List<List<int?>> newUsedDim    = new List<List<int?>>();

            for (idpart = 0; idpart < dimValues[d]+1; idpart++) 
            {  newpartitions.Add(new List<int>());

               newUsedDim.Add(new List<int?>()); // per ogni partizione del nodo, lista delle dimensioni che hanno portato a lei
               newUsedDim[idpart] = new( nd.usedDim[i] ); 
               newUsedDim[idpart].Add(d);    
               
               newFathers.Add((idFathNode, idFathPart));      // for each partition, the father's originating one

               newPartClass.Add(-2);
               newPartDepth.Add(newDepth);
            }

            // riempio le partizioni che ottengo nei figli
            for (j = 0; j < nd.lstPartitions[i].Count;j++)  // for each point in the father node partition
            {  idpoint = nd.lstPartitions[i][j];
               k = 0;
               while (cutdim[k]!=d) k++; // first value for dimension d
               idpart=0;   // id of the new partiorn of the node
               while (k<cutdim.Length && cutdim[k] == d && cutval[k] < X[idpoint,d]) // find the partition of idpoint
               {  k++;
                  idpart++;
               }
               newpartitions[idpart].Add(idpoint); // le nuove partizioni lungo la dimensione, sostituiscono la vecchia

               if (newPartClass[idpart] == -2) // initialization
                  newPartClass[idpart] = Y[idpoint];
               else if (newPartClass[idpart] != Y[idpoint])
                  newPartClass[idpart] = -1;   // classi eterogenee
            }

            // tolgo eventuali nuove partizioni senza punti
            idpart = 0;
            while (idpart<newpartitions.Count)
               if (newpartitions[idpart].Count == 0)
               {  newpartitions.RemoveAt(idpart);
                  newPartClass.RemoveAt(idpart);
                  newPartDepth.RemoveAt(idpart);
                  newFathers.RemoveAt(idpart);
                  newUsedDim.RemoveAt(idpart);
                  Console.WriteLine("-- removed pointless partition "+idpart);
               }
               else
                  idpart++;

            // tolgo la partizione appena espansa
            newNode.lstPartitions.RemoveAt(i); 
            newNode.lstPartClass.RemoveAt(i);
            newNode.lstPartDepth.RemoveAt(i);
            newNode.usedDim.RemoveAt(i);
            // aggiungo le nuove partizioni
            newNode.lstPartitions = newNode.lstPartitions.Concat(newpartitions).ToList();  // lista punti di ogni partizione
            newNode.usedDim       = newNode.usedDim.Concat(newUsedDim).ToList();
            newNode.lstPartClass  = newNode.lstPartClass.Concat(newPartClass).ToList();    // la classe di ogni partizione se uniforme, sennò -1
            newNode.lstPartDepth  = newNode.lstPartDepth.Concat(newPartDepth).ToList();
            newNode.lstFathers[newDepth].Add(newFathers);
            newNode.hash = nodeHash(newNode);
            isComplete = false;

            int minDepth = ndim+1, maxDepth = 0; // min and max iDepth of node partitions
            for(j=0;j<newNode.lstPartitions.Count;j++)
            {  if (newNode.lstPartDepth[j] > maxDepth)
                  maxDepth = newNode.lstPartDepth[j];
               if (newNode.lstPartDepth[j] < minDepth && newNode.lstPartClass[j] < 0)
                  minDepth = newNode.lstPartDepth[j];
            }
            if(minDepth > ndim)  // da cambiare controllando che non ci siano lstPartClass negativi ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ
            {  minDepth = maxDepth;
               isComplete = true;
               Console.WriteLine($"Node completed, depth {maxDepth}");
               int nLevels = newNode.lstFathers.Count;
               if (newNode.lstFathers[nLevels-1].Count == 0)   // last level empty
                  newNode.lstFathers.RemoveAt(nLevels-1);
            }

            NodeDP nhash = checkHash(newNode.hash);
            bool fSamePartitions = false;
            if(nhash != null)
            {  Console.WriteLine($"STESSO Hash!!! nodi {newNode.id} {nhash.id}");
               fSamePartitions = isEqualPartition(newNode,nhash);   // gestisce la dominanza fra nodi
               int depthNhash  = getDPcellDepth(nhash.idDPcell);
               if(fSamePartitions && depthNhash <= maxDepth+1)
               {  Console.WriteLine($"Nodo {newNode.id} dominato");
                  continue; // newnode discarded
               }
            }

            if(fSamePartitions) // nuovo nodo domina cella nhash
            {  idcell = nhash.idDPcell;
               var colrow = getDPtablecell(idcell);
               newNode.idDPcell = idcell;
               DPtable[colrow.Item1][colrow.Item2].node  = newNode;
               DPtable[colrow.Item1][colrow.Item2].depth = iDepth+1;
            }
            else
            {  DPcell dpc = new DPcell(); // insert the node in a cell of the DP table (it is its state)
               dpc.id = totcells; totcells++;
               newNode.idDPcell = dpc.id;
               dpc.node   = newNode;
               dpc.depth  = minDepth; // profondità nodo quella min di partizione ancora da espandere
               dpc.nnodes = 1;
               dpc.isExpanded = false;
               DPtable[dpc.depth].Add(dpc);
               Console.WriteLine($"expanded node {nd.id} into {newNode.id}");
               if(isComplete && res < 0) 
                  res = dpc.id;
            }
         }
         if (nd.lstFathers[nd.lstFathers.Count - 1].Count == 0)   // last level empty, added for cloning at the beginning of the method
            nd.lstFathers.RemoveAt(nd.lstFathers.Count - 1);
         return res;
      }

      // il nodo con lo stesso hash se h già in tabella, null altrimenti
      private NodeDP checkHash(int h)
      {  int i,j;
         NodeDP res = null;

         for(i=0;i<DPtable.Length;i++)
            for (j = 0; j < DPtable[i].Count;j++)
               if (DPtable[i][j].node.hash == h)
               {  res = DPtable[i][j].node;
                  break;
               }
         return res;
      }

      // gets the iDepth of a node given its id in the DPtable
      private int getDPcellDepth(int idCell)
      {  int i,j,res=-1;
         for(i=0;i<DPtable.Length;i++)
            for (j = 0; j < DPtable[i].Count;j++)
               if (DPtable[i][j].id == idCell)
               {  res = DPtable[i][j].depth;
                  goto lend;
               }

lend:    return res;
      }

      // gets coords of a DP table cell given its id
      private (int,int) getDPtablecell(int idCell)
      {  int i=-1,j=-1; // would cause an error
         for (i = 0; i < DPtable.Length; i++)
            for (j = 0; j < DPtable[i].Count; j++)
               if (DPtable[i][j].id == idCell)
                  goto lend;

lend:    return (i,j);
      }

      // confronta due nodi con lo stesso hash. True davvero uguali, false partizioni diverse
      private bool isEqualPartition(NodeDP newnode, NodeDP nhash)
      {  int i,j;
         bool res = false; 
         if(newnode.lstPartitions.Count != nhash.lstPartitions.Count) goto lend;
         for(i=0;i<nhash.lstPartitions.Count;i++)
         {
            if (nhash.lstPartitions[i].Count != newnode.lstPartitions[i].Count) goto lend;
            for (j = 0; j < newnode.lstPartitions[i].Count;j++)
               if (nhash.lstPartitions[i][j]!= newnode.lstPartitions[i][j])
                  goto lend;
         }
         // qui stesse partizioni
         res = true;

lend:    Console.WriteLine($"Same partitions: {res}");
         return res;
      }

      // hash function of a node (mod product of its points). Assumes id in partitions to be ordered
      int nodeHash(NodeDP ndClus)
      {  int i,j,hash = 1;
         int[] firstElems = new int[ndClus.lstPartitions.Count];
         // GESTIRE CASI PARTIZIONI CON 0 ELEMENTI
         for(i=0;i<firstElems.Length;i++) firstElems[i] = ndClus.lstPartitions[i][0]; // array con i primi elementi di ogni partizione
         int[] idxPart = getSortIdx(firstElems); // indici ordinati partizioni per primo elemento crescente

         for(i=0;i<idxPart.Length;i++)
            for (j = 0; j < ndClus.lstPartitions[idxPart[i]].Count;j++)
               hash = (hash * (j*ndClus.lstPartitions[idxPart[i]][j] % 31 + 1)) % 193939;
         return hash;
      }

      // translates tree structure into decTreee for plotting
      private void marshalTree(int idDPcell)
      {  int i,j,k,d,nid=0,idFather,idNode,idLeaf;
         List<List<int>> nodes = new List<List<int>>();
         var cellCoord = getDPtablecell(idDPcell);
         NodeDP  ndp = DPtable[cellCoord.Item1][cellCoord.Item2].node;

         for(int iDepth=0; iDepth < ndp.lstFathers.Count; iDepth++)
         {  nodes.Add(new List<int>());
            for (i = 0; i < ndp.lstFathers[iDepth].Count; i++)
               for (j = 0; j < ndp.lstFathers[iDepth][i].Count; j++)
               {  NodeHeu n0 = new NodeHeu();
                  n0.id  = nid++;
                  n0.dim = -1;
                  n0.lstSons   = new List<int>();
                  n0.lstPoints = new List<int>(); // punti nel nodo (se foglia)
                  n0.lstCuts   = new List<int>(); // tutti i cut attivi al nodo (se interno)
                  n0.isLeaf    = true;
                  nodes[iDepth].Add(n0.id); // le id dei nodi, invece delle partizioni in fathers
                  if(iDepth>0)
                  {  idFather = ndp.lstFathers[iDepth][i][j].node; // qui solo l'indice della partizione nel padre
                     idFather = nodes[iDepth-1][idFather]; // qui l'id del nodo padre
                     n0.idFather = idFather;
                     Console.WriteLine($" -- arco {idFather}-{n0.id}");
                     decTree[idFather].lstSons.Add(n0.id);
                     decTree[idFather].isLeaf = false;
                  }
                  decTree.Add(n0);
               }
         }

         // --------------- BFS to recontruct node assignments
         Queue<int> que = new Queue<int>();
         List<int> lstPath = new List<int>();   // path to the current leaf
         leaf2node = new List<int>(); // the node corrsponding to each leaf
         NodeHeu currNode;

         // Push the current source node
         idNode = decTree[0].id;
         que.Enqueue(idNode);
         idLeaf = 0;

         while (que.Count > 0)
         {  idNode = que.Dequeue();
            lstPath.Clear();
            currNode = decTree[idNode];
            while (currNode.id != 0)
            {  lstPath.Add(currNode.id);
               currNode = decTree[currNode.idFather];
            }
            lstPath.Add(0);
            lstPath.Sort();

            currNode = decTree[idNode];

            // we work on the popped item if it is not visited.
            if (!currNode.visited)
            {  Console.WriteLine($"expanding node {idNode}");
               currNode.visited = true;
            }

            // Get all offsprings of the popped vertex s, if not visited, then push it to the que.
            if(currNode.lstSons.Count > 0)
            {  for(int v = 0; v < decTree[idNode].lstSons.Count;v++)
                  if (!decTree[decTree[idNode].lstSons[v]].visited)
                     que.Enqueue(decTree[idNode].lstSons[v]);
            }
            else
            {  lstPath.Remove(idNode);
               if(!decTree[idNode].isLeaf) Console.WriteLine(">> ERROR << unrecognized leaf");
               decTree[idNode].npoints = ndp.lstPartitions[idLeaf].Count;
               // i punti della partizione
               for (i = 0; i < ndp.lstPartitions[idLeaf].Count; i++)
                  decTree[idNode].lstPoints.Add(ndp.lstPartitions[idLeaf][i]);
               leaf2node.Add(idNode);
               idLeaf++;
               Console.WriteLine($"{idNode} is a leaf, n.points {decTree[idNode].npoints}");
            }
         }

         // --------------- recostruction of node points (for each node, add its points to its ancestors)
         for (i = 0; i < decTree.Count; i++)
         {  k = i;
            while (k > 0)
            {  k = decTree[k].idFather;
               for (j = 0; j < decTree[i].lstPoints.Count; j++)
               {  int ip = decTree[i].lstPoints[j];
                  if (!decTree[k].lstPoints.Contains(ip))
                  {  decTree[k].lstPoints.Add(ip);
                     decTree[k].npoints++;
                  }
               }
            }
         }

         // -------------- reconstruction of cuts used at each node (if any)
         // per ogni antenato, che cut ha usato (attivi nella sua dimensione)
         for(idLeaf=0; idLeaf < leaf2node.Count; idLeaf++)
         {  i = leaf2node[idLeaf]; // nodo di cui troverò tutti i cut che hanno portato a lui
            for (j = ndp.usedDim[idLeaf].Count-1;j>=0; j--) // dimensioni che lo hanno individuato
            {  i = decTree[i].idFather; // a turno, tutti gli antenati
               d = (int)ndp.usedDim[idLeaf][j];
               decTree[i].dim = d;      // dimensione di taglio dell'antenato corrente
               for (k = 0; k < cutdim.Length; k++)
                  if (cutdim[k] == d && !decTree[i].lstCuts.Contains(k))
                  {  // controllo che ci sia un punto sopra e uno sotto 
                     bool fInsert = false;
                     for (int i1 = 0; i1 < decTree[i].lstPoints.Count; i1++)
                     {  int iPoint1 = decTree[i].lstPoints[i1];
                        for (int i2 = 0; i2 < decTree[i].lstPoints.Count; i2++)
                        {  int iPoint2 = decTree[i].lstPoints[i2];
                           if (X[iPoint1, d] < cutval[k] && X[iPoint2, d] > cutval[k])
                              fInsert = true;
                           if (X[iPoint1, d] > cutval[k] && X[iPoint2, d] < cutval[k])
                              fInsert = true;
                           if(fInsert)
                              for(int kk = 0; kk < decTree[i].lstCuts.Count; kk++)
                              {  int kkCut = decTree[i].lstCuts[kk];
                                 if (X[iPoint1, d] < cutval[kkCut] && X[iPoint2, d] > cutval[kkCut] ||
                                     X[iPoint1, d] > cutval[kkCut] && X[iPoint2, d] < cutval[kkCut])
                                    fInsert = false; // punti già separati da un altro cut incluso
                              }
                           if(fInsert)
                              goto l0;
                        }
                     }
l0:                  if (fInsert)
                        decTree[i].lstCuts.Add(k);
                  }
            }
         }
      }

      // removes nodes with no points
      private void postProcessing()
      {  int i,j;

         for(i=0;i<decTree.Count;i++) 
            if (decTree[i].npoints == 0 && method != "exact")
               Console.WriteLine(">>>>>> EMPTY NODE IN TREE !! PostProcessing needed for removal");
            else if (decTree[i].lstSons.Count == 1)
               Console.WriteLine(">>>>>> SINGLE OFFSPRING NODE. PostProcessing needed for removal");
      }

      private string readConfig()
      {  string dataset = "";
         StreamReader fin = new StreamReader("config.json");
         string jconfig = fin.ReadToEnd();
         fin.Close();

         var config = JsonConvert.DeserializeObject<dynamic>(jconfig);
         try
         {  string path = Convert.ToString(config.datapath);
            string file = Convert.ToString(config.datafile);
            splitRule = Convert.ToString(config.splitRule);
            splitDir  = Convert.ToString(config.splitDir);
            method    = Convert.ToString(config.method);
            dataset = path + file;
         }
         catch (Exception ex)
         {  Console.WriteLine(ex.Message); }
         return dataset;
      }

      private void readData(string dataset)
      {  string line;
         int i,j;

         // read raw data
         try
         {
            StreamReader fin = new StreamReader(dataset + ".csv");
            line = fin.ReadLine();
            dataColumns = line.Split(',');
            numcol = ndim = dataColumns.Length - 2; // data column, excluding id (first) and class (last)
            List<double[]> X1 = new List<double[]>();
            List<int> Y1 = new List<int>();
            i = 0;
            while (fin.Peek() >=0)
            {
               line = fin.ReadLine();
               i++;
               if(i%10==0) Console.WriteLine(line);
               string[] elem  = line.Split(',');
               string[] elem1 = elem.Take(elem.Length - 1).ToArray();
               double[] aline = Array.ConvertAll( elem1 , double.Parse);
               X1.Add( aline );
               Y1.Add(Convert.ToInt32(elem[elem.Length-1]) );
            }
            fin.Close();
            X = new double[Y1.Count(), numcol];
            for(i=0;i<X1.Count();i++)
               for(j=0;j<numcol;j++)
                  X[i,j] = X1[i][j+1]; // removing id column
            Y = Y1.ToArray();
            n = Y.Length; // number of records in the dataset
            nclasses = Y.Max() + 1; // number of different classes to classify into
         }
         catch (Exception ex)
         {  Console.WriteLine(ex.Message);
            Environment.Exit(1);
         }

         // read cuts
         try
         {
            dimValues = new int[ndim];
            StreamReader fin = new StreamReader(dataset + "_cuts.json");
            string jcuts = fin.ReadToEnd();
            fin.Close();

            var cuts = JsonConvert.DeserializeObject<dynamic>(jcuts);
            cutdim = cuts.dim.ToObject<int[]>();
            cutval = cuts.pos.ToObject<double[]>();
            for(i=0;i<cutdim.Length;i++)
               dimValues[cutdim[i]]++;
         }
         catch (Exception ex)
         {  Console.WriteLine(ex.Message);
            Environment.Exit(1);
         }
      }

      // starts the process of tree construction
      private void heuristicTree()
      {  int i,j;
         List<int>[] dimCuts = new List<int>[numcol]; // which cuts for ech dim
         for(i=0;i<numcol;i++)
            dimCuts[i] = new List<int>();
         for(i=0;i<cutdim.Length;i++)
            dimCuts[cutdim[i]].Add(i);

         int[,] idx; // indices of sorted values for each column
         idx = getSortIdxAllDim();
         depthFirstConstruction(idx); // construct the tree
      }

      // Depth-first construction
      private void depthFirstConstruction(int[,] idx)
      {
         int i, j, idNode;
         NodeHeu currNode;
         // Initially mark all vertices as not visited
         // Boolean[] visited = new Boolean[V];

         // Create a que for DFS
         Stack<int> stack = new Stack<int>();

         // Push the current source node
         idNode = decTree.Count;
         currNode = new NodeHeu(idNode, ndim, nclasses);
         for (i = 0; i < ndim; i++) currNode.isUsedDim[i] = false;
         for (i = 0; i < nclasses; i++) currNode.nPointClass[i] = 0;
         for (i = 0; i < n; i++) currNode.lstPoints.Add(i);
         currNode.npoints = n;
         decTree.Add(currNode);
         //fillNode(currNode,idx);

         stack.Push(idNode);

         while (stack.Count > 0)
         {
            // Pop a vertex from que and print it
            // idNode = que.Peek();
            idNode = stack.Pop();
            currNode = decTree[idNode];

            // we work on the popped item if it is not visited.
            if (!currNode.visited)
            {
               Console.WriteLine($"expanding node {idNode}");
               currNode.visited = true;
               if (!currNode.isLeaf)
                  fillNode(currNode, idx);
            }

            // Get all offsprings of the popped vertex s, if not visited, then push it to the que.
            foreach (int v in decTree[idNode].lstSons)
               if (!decTree[v].visited)
                  stack.Push(v);
         }
      }

      // check the correctness of the tree
      private bool checkSol()
      {  int i,j,k,d,nc,idcut,currnode,child;
         bool res=true;
         int[] heights = new int[decTree.Count];

         heights[0]=0;
         for(i=0;i<n;i++)
         {
            currnode = 0;
            while (!decTree[currnode].isLeaf)
            {
               d  = decTree[currnode].dim;
               nc = decTree[currnode].lstCuts.Count;
               for(j=0;j<nc;j++)
                  if (X[i,d] < cutval[decTree[currnode].lstCuts[j]])
                  {  
                     //child = decTree[currnode].lstSons[j];
                     //heights[child] = heights[currnode]+1;
                     //if (heights[child]>treeHeight) treeHeight = heights[child];
                     //currnode = child;
                     nc = int.MaxValue; // to avoid entering the following if, useless when if commented out
                     break;
                  }
               //if(j==nc)
               //   currnode = decTree[currnode].lstSons[j];

               child = decTree[currnode].lstSons[j];
               heights[child] = heights[currnode] + 1;
               if (heights[child] > treeHeight) treeHeight = heights[child];
               currnode = child;
            }
            if(method=="exact")
            {
               for(j=0;j<leaf2node.Count;j++)
               {  k = leaf2node[j];
                  if (decTree[k].lstPoints.Contains(i))
                     if (Y[i] != Y[decTree[k].lstPoints[0]])
                     {  res = false;
                        Console.WriteLine($"ERROR, misclassification of record {i}");
                        goto lend;
                     }
               }
            }
            else
               if (Y[i] != Y[decTree[currnode].lstPoints[0]])
               {  res = false;
                  Console.WriteLine($"ERROR, misclassification of record {i}");
                  goto lend;
               }
            Console.WriteLine($"Record {i} node {currnode} class {Y[i]}"); 
         }
         if (res) Console.WriteLine("Checked. Solution is ok");
         Console.WriteLine($"Tree height = {treeHeight}");
lend:    return res;
      }

      // saves the tree on file and calls graphx to plot it
      void plotTree(string dataset)
      {  int i,j;
         string label;

         using (StreamWriter fout = File.CreateText("graph.txt"))
         {
            fout.WriteLine("digraph G {");
            fout.WriteLine("graph[fontname = \"helvetica\"]");
            fout.WriteLine("node[fontname = \"helvetica\"]");
            fout.WriteLine("edge[fontname = \"helvetica\"]");

            for(i=0;i<decTree.Count;i++)
               if (decTree[i].isLeaf)
               {  fout.WriteLine($"{i}  [shape = box label = \"{decTree[i].id}\nclass {Y[decTree[i].lstPoints[0]]}\"]");
                  totLeaves++;
               }
               else
                  fout.WriteLine($"{i}  [label = \"{decTree[i].id} dim {decTree[i].dim}\"]");

            for(i=0;i<decTree.Count;i++)
               if (!decTree[i].isLeaf)
                  for (j = 0; j < decTree[i].lstSons.Count;j++)
                  { 
                     if(j < decTree[i].lstCuts.Count)
                        label = $"[ label = \" < {cutval[decTree[i].lstCuts[j]]}\"]";
                     else
                        label = $"[ label = \" > {cutval[decTree[i].lstCuts[j-1]]}\"]";
                     fout.WriteLine($"{decTree[i].id} -> {decTree[i].lstSons[j]} {label}");
                  }
            fout.WriteLine("labelloc = \"t\"");
            fout.WriteLine($"label = \"{dataset.Substring(dataset.LastIndexOf('\\')+1)}\"");
            fout.WriteLine("}");
         }

         string batfile = "graphviz.bat";
         string parameters = $"/k \"{batfile}\"";
         Process.Start("cmd", parameters);
         totNodes = decTree.Count;
         Console.WriteLine($"Tot cuts   = {cutdim.Length}");
         Console.WriteLine($"Tot nodes  = {totNodes}");
         Console.WriteLine($"Tot leaves = {totLeaves}");
      }

      // initializes fields of a new node
      private void fillNode(NodeHeu currNode, int[,] idx)
      {  int i,j,jj,d,pt,splitd;
         double h,splith; // split criterium value
         List<int[]> lstNptClass;
         List<int> lstp;
         bool[] fOut;
         NodeHeu child;
         bool fSkip,isLeaf;
         NodeHeu prev;

         for (i = 0; i < currNode.npoints; i++)
         {  pt = currNode.lstPoints[i];
            currNode.nPointClass[Y[pt]]++;
         }
         splith = (splitDir == "max" ? double.MinValue : double.MaxValue); 
         splitd = int.MaxValue;
         for (d=0;d<ndim;d++)  // for each dimension upon which we could separate
         {
            if (currNode.isUsedDim[d]) continue;
            lstNptClass = new List<int[]> ();  // for each value range, how many of each class
            fOut = new bool[currNode.npoints]; // point already considered
            fSkip = false;
            for(j=0;j<cutdim.Length;j++)  // for each cut acting on that dimension
            {
               if (cutdim[j]!=d) continue;
               lstp = separateNodePoints(currNode, lstNptClass, fOut, cutval[j], d);
               if (lstp.Count == currNode.npoints) fSkip=true; // dim d generates no separation
            }
            // points after the biggest cut
            lstp = separateNodePoints(currNode, lstNptClass, fOut, double.MaxValue, d);
            if (lstp.Count == currNode.npoints) fSkip = true; // dim d generates no separation
            if (fSkip || lstNptClass.Count() == 1) continue;    // dim d generates no separation
            switch (splitRule)
            {  case "entropy": 
                  h = computeEntropy(lstNptClass);
                  if(splitDir=="max" ? h>splith : h<splith) 
                  {  splith = h;
                     splitd = d;
                  }
                  break;
               case "infoGain":
                  Console.WriteLine("Split rule not implemented");
                  Environment.Exit(0);
                  break;
               case "variance":
                  Console.WriteLine("Split rule not implemented");
                  Environment.Exit(0);
                  break;
               case "gini":
                  Console.WriteLine("Split rule not implemented");
                  Environment.Exit(0);
                  break;
               default:
                  Console.WriteLine("Split rule not defined");
                  Environment.Exit(0);
                  break;
            }
         }
         // ----------------------------------------------- current node completion
         
         currNode.dim = splitd;
         currNode.isUsedDim[splitd] = true;
         lstNptClass = new List<int[]>();     // for each value range, how many of each class
         fOut = new bool[currNode.npoints]; // points already considered

         // generate the offsprings of the current node and compute the points of each offspring
         int nSons = 0;
         for (int idcut = 0; idcut < cutdim.Length; idcut++)
         {  if (cutdim[idcut] != splitd) continue;  // for each cut acting on the mind dimension

            j = idcut;
            child = new NodeHeu(decTree.Count(), ndim, nclasses); // tentative son
            List<int> lstPoints = separateNodePoints(currNode, lstNptClass, fOut, cutval[j], splitd);
            if(lstPoints.Count == 0 ) continue; // region with no points, no need for a son

            // check if potential child would be a leaf
            isLeaf = false;
            for (d = 0; d < nclasses; d++)
               if (lstNptClass[lstNptClass.Count - 1][d] == lstPoints.Count)
                  isLeaf = true;

            // if leaf, and previous a leaf, same class, then merge
            prev = decTree[decTree.Count-1];
            if(nSons>0 && isLeaf && currNode.lstSons.Count > 0 && prev.isLeaf && Y[prev.lstPoints[0]] == Y[lstPoints[0]])
            {
               for(i=0;i<lstPoints.Count;i++)
               {  prev.lstPoints.Add(lstPoints[i]);
                  prev.npoints++;
                  currNode.lstCuts[currNode.lstCuts.Count-1] = idcut;
               }
            }
            else
            {  // here add the child to the tree
               Array.Copy(currNode.isUsedDim, child.isUsedDim, currNode.isUsedDim.Length);
               decTree.Add(child);
               nSons++;
               i = child.id;
               currNode.lstSons.Add(i);
               currNode.lstCuts.Add(idcut);         // cuts active at the node
               decTree[i].lstPoints = lstPoints;
               decTree[i].npoints = decTree[i].lstPoints.Count();
               if(isLeaf) decTree[i].isLeaf = true;
            }
         }
         // points after the biggest cut
         // check if potential child would be a leaf
         List<int> lstPoints1 = separateNodePoints(currNode, lstNptClass, fOut, double.MaxValue, splitd); // all remaining points
         if (lstPoints1.Count == 0) goto l0; // region with no points, no need for a son
         isLeaf = false;
         for (d = 0; d < nclasses; d++)
            if (lstNptClass[lstNptClass.Count - 1][d] == lstPoints1.Count)
               isLeaf = true;

         // if leaf, and previous a leaf, same class, then merge
         prev = decTree[decTree.Count - 1];
         if (isLeaf && currNode.lstSons.Count > 0 && prev.isLeaf && Y[prev.lstPoints[0]] == Y[lstPoints1[0]])
         {
            for (i = 0; i < lstPoints1.Count; i++)
            {
               prev.lstPoints.Add(lstPoints1[i]);
               prev.npoints++;
            }
            if(currNode.lstCuts.Count>0)
               currNode.lstCuts.RemoveAt(currNode.lstCuts.Count-1);
            if(currNode.lstCuts.Count == 0)
               Console.WriteLine($"Nonleaf with no cuts {currNode.id}");
         }
         else
         {  child = new NodeHeu(decTree.Count(), ndim, nclasses); // tentative son
            if (lstPoints1.Count > 0)  // if empty son, do not include in the tree
            {
               Array.Copy(currNode.isUsedDim, child.isUsedDim, currNode.isUsedDim.Length);
               decTree.Add(child);
               i = child.id;
               currNode.lstSons.Add(i);
               decTree[i].lstPoints = lstPoints1;
               decTree[i].npoints  = decTree[i].lstPoints.Count();
               if(isLeaf) decTree[i].isLeaf = true;
               if (currNode.lstCuts.Count == 0 && !currNode.isLeaf)
                  Console.WriteLine("WARNING. uncut non leaf"); ;
            }
         }

l0:      if(currNode.lstSons.Count == 1)
            Console.WriteLine("WARNING. Single child");
      }

      // calcola i punti in ogni segmento definito dai cut, albero euristico
      private List<int> separateNodePoints(NodeHeu currNode, List<int[]> lstNptClass, bool[] fOut, double maxVal, int d)
      {  int i,pt;
         List<int> ptslice; // points of each slice (not separated per class)
         int[] nptslice = new int[nclasses]; // num points of each slice, per class

         ptslice = new List<int>();
         for (i = 0; i < currNode.npoints; i++)
         {
            pt = currNode.lstPoints[i];
            if (!fOut[i] && X[pt, d] < maxVal)
            {
               ptslice.Add(pt);   // i punti
               nptslice[Y[pt]]++; // quanti punti di ogni classe
               fOut[i] = true;
            }
         }
         lstNptClass.Add(nptslice);
         return ptslice;
      }
      
      // separate che restituisce i punti separati per classe
      private List<int>[] separateNodePoints(List<int> lstPoints, List<int[]> lstNptClass, bool[] fOut, double maxVal, int d)
      {
         int i, pt;
         List<int>[] ptslice = new List<int>[nclasses] ; // points of each slice separated per class
         for(i=0;i<nclasses;i++)
            ptslice[i] = new List<int>();
         int[] nptslice = new int[nclasses]; // num points of each slice, per class
         int npoints = lstPoints.Count;

         for (i = 0; i < npoints; i++)
         {
            pt = lstPoints[i];
            if (!fOut[i] && X[pt, d] < maxVal)
            {
               ptslice[Y[pt]].Add(pt);   // i punti
               nptslice[Y[pt]]++; // quanti punti di ogni classe
               fOut[i] = true;
            }
         }
         lstNptClass.Add(nptslice);
         return ptslice;
      }

      // entropy at the node, on number of points in each son
      private double computeEntropy(List<int[]> lstNptson)
      {  int i,j;
         double tot=0;
         double[] sums;
         double h = 0;

         sums = new double[lstNptson.Count];
         for(i=0;i<lstNptson.Count;i++)
         {
            sums[i] = 0.0;
            for (j=0;j<nclasses;j++)
               sums[i] += lstNptson[i][j];
            tot += sums[i];
         }
         for (i = 0; i < lstNptson.Count; i++)
            if (sums[i] > 0)
               h += (sums[i]/tot)*Math.Log(sums[i] / tot);

         return -h;
      }

      // computes the indices that sort each dimension
      private int[,] getSortIdxAllDim()
      {  int i, j;
         int[,] idxSort = new int[n, numcol];
         double[] col = new double[n];
         int[] ind = new int[n];

         for (j = 0; j < numcol; j++)
         {  for (i = 0; i < n; i++)
            {  col[i] = X[i, j];
               ind[i] = i;
            }
            Array.Sort<int>(ind, (a, b) => col[a].CompareTo(col[b]));
            for (i = 0; i < n; i++)
               idxSort[i, j] = ind[i];
         }
         return idxSort;
      }

      // gets the sorte indices of an array of int
      private int[] getSortIdx(int[] A)
      {  int i, j;
         int[] idxSort = new int[A.Length];

         for (i = 0; i < A.Length; i++)
            idxSort[i] = i;
         Array.Sort<int>(idxSort, (a, b) => A[a].CompareTo(A[b]));
         return idxSort;
      }
   }
}
