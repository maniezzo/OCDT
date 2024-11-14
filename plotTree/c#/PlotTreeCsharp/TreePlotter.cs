using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using System.Collections;
using System.Reflection;

/* Data is in X, classes in Y. Attributes are columns of X
*/

namespace PlotTreeCsharp
{
   // Each node of the nonbinary decision tree
   public class NodeHeu
   {
      public NodeHeu() {}
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
      public int rndid;    // random id (in exact tree)
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
         this.lstFathers    = new List<List<List<(int, int, int)>>>();
         this.lstFcut       = new List<List<List<(int, int)>>>();
         this.lstIdNode     = new List<List<int>>();
         this.usedDim       = new List<List<int?>>();
         this.lstPartClass  = new List<int>();
         this.lstPartDepth  = new List<int>();
         this.lstPartNode   = new List<int>();
      }
      public NodeDP(int id, int ndim, int nclasses) 
      {  this.id = id;
         this.lstPartitions = new List<List<int>> { };
         this.lstFathers    = new List<List<List<(int,int, int)>>> { };
         this.lstFcut       = new List<List<List<(int, int)>>>();
         this.lstIdNode     = new List<List<int>>();
         this.usedDim       = new List<List<int?>> { };
         this.lstPartClass  = new List<int>();
         this.lstPartDepth  = new List<int>();
         this.lstPartNode   = new List<int>();
      }

      public int id;
      public int idDPcell; // id of the DP table cell the node is set into
      public int npoints;  // number of points (records) clustered in the node
      public int hash;     // hash code of the partition
      public int bound;    // a bound to the cost of a complete solution from the node
      public List<int> lstPartClass;    // the class of each partition, -1 heterogeneous
      public List<int> lstPartDepth;    // the iPartNum of each partition
      public List<int> lstPartNode;     // the node of the tree the partition is associated with
      public List<List<int>> lstIdNode; // random id assogned to each tree node
      public List<List<int?>> usedDim;      // dimensions used in the path to each partition of the node (will give the tree)
      public List<List<int>> lstPartitions; // list of the point in each partitions at the node
      public List<List<List<(int node,int part, int idFather)>>>  lstFathers; // list of father nodes (originating node/partition): depth->sons->fathers
      public List<List<List<(int fdim,int fpart)>>> lstFcut;    // list of dimension and cut used at the father to generate the node
   }

   internal class TreePlotter
   {  private double[,] X;
      private int[]     Y;
      private List<NodeHeu> decTree;  // l'albero euristico
      private List<DPcell>[] DPtable; // la tabella della dinamica, una riga ogni altezza dell'albero (max ndim)
      private int[] part2node;  // mapping of partitions to tree nodes
      private int[] cutdim;     // dimension on which each cut acts
      private int[] dimValues;  // number of values (of cuts) acting on each dimension
      private double[] cutval;  // value where the cut acts
      private int numcol,ndim;  // num dimensions (attributes, columns of X)
      private int n, nclasses;  // num of records, num of classes
      private int numDominated; // num dominated nodes
      private string splitRule; // criterium for node plitting
      private string splitDir;  // max o min
      private string method;    // exact (depth tree), heuristic, part_tree
      private string[] dataColumns;
      private int totNodes=0, totcells = 0, treeHeight=0, totLeaves=0;
      private int verbose;      // 0 no print, 1 average, 2 extended
      private bool fBeamSearch; // true beam search, false exact
      private bool fDPpartition;// true tree based on number of partitions
      Random rnd;

      public TreePlotter()
      {  decTree = new List<NodeHeu>();
      }
      public void run_plotter()
      {  string dataset = readConfig();
         Console.WriteLine($"Plotting {dataset}");
         rnd = new Random(666);
         readData(dataset); // gets the X and Y matrices (data and class)

         if(method == "part_tree")
            DPtable = new List<DPcell>[X.Length];
         else
            DPtable = new List<DPcell>[ndim+1]; // la radice (level 0) poi max un livello per dim (non fissi!)

         for(int i=0;i<DPtable.Length;i++) DPtable[i] = new List<DPcell>();

         if(method == "exact" || method == "part_tree")
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

         // --------------------------------------------------------- initiailization node 0
         idNode   = totNodes++;
         currNode = new NodeDP(idNode, ndim, nclasses); // inizializza anche lstPartitions, lstPartClass, usedDim
         currNode.lstIdNode.Add(new List<int>());
         currNode.lstPartitions.Add(new List<int>());
         currNode.lstFathers.Add(new List<List<(int,int,int)>>());
         currNode.lstFathers[0].Add(new List<(int,int,int)>());
         currNode.lstFcut.Add(new List<List<(int fdim, int fpart)>>());
         currNode.lstFcut[0].Add(new List<(int fdim, int fpart)>());
         currNode.usedDim.Add(new List<int?>());
         currNode.lstPartClass.Add(-1);  // unica partizione, dati eterogenei
         currNode.lstPartDepth.Add(0);
         currNode.lstPartNode.Add(0);    // the id of the root node of any decision tree
         for (i=0;i<n;i++) currNode.lstPartitions[0].Add(i); // tutti i punti nell'unica partizione
         currNode.lstFathers[0][0].Add((0,-1,0)); // root node, no father, no partition coming from
         currNode.lstFcut[0][0].Add((-1,-1));   // root node, no father cut
         currNode.lstIdNode[0].Add(0);
         currNode.npoints = n;
         currNode.hash    = nodeHash(currNode);
         currNode.bound   = completionBound(currNode.lstPartitions[0]);

         // ----------------------------------------- inizializzazione matrice DP
         DPcell dpc = new DPcell(); // insert the node in a cell of the DP table (it is its state)
         dpc.id     = totcells; totcells++;
         currNode.idDPcell = dpc.id;
         dpc.node   = currNode;
         dpc.depth  = 0;
         dpc.nnodes = 1;
         dpc.isExpanded = false;
         DPtable[0].Add(dpc);
         numDominated = 0;

         // ------------------------------------------ dinamica basata sul numero di partizioni
         if(method == "part_tree") fDPpartition = true;
         if(fDPpartition)
         {  int beamWidth = 3;     // just because
            idDPcell = beamSearchPart(beamWidth);
            goto l0;
         }

         // ------------------------------------------ espansione della tabella, per ogni livello (distanza dalla radice)
         if(fBeamSearch)
         {  int beamWidth = 3;     // just because
            idDPcell = beamSearch(beamWidth);
         }
         else
            for(iDepth=0;iDepth<DPtable.Length;iDepth++)
            {  // per ogni cella dek livello
               iCell = 0;
               while(iCell < DPtable[iDepth].Count)
               {  currNode = DPtable[iDepth][iCell].node;
                  for (d = 0; d < ndim; d++)
                     if (dimValues[d] > 0) // if any cut was selected acting on dimension d
                     {  idNode = expandNode(currNode, d, iDepth);
                        if(idDPcell < 0 && idNode >= 0) idDPcell = idNode;  // node completed
                     }

                  DPtable[iDepth][iCell].isExpanded = true;
                  iCell++;
               }
            }

         // this is for debug
         // printPartitions();

         // -------------------------------------------- recover the best decision tree
l0:      if(idDPcell > 0)
            marshalTree(idDPcell);
      }

      // beam search based on number of partitions
      int beamSearchPart(int beamWidth)
      {  int i, j, d, iPartNum, iCell, idNode, nExpanded;
         int idDPcell = -1;
         NodeDP currNode;
         bool fTerminate = false;

         while (!fTerminate)
         {  // ogni livello contiene partizionamenti con lo stesso numero di partizioni
            for (iPartNum = 0; iPartNum < DPtable.Length; iPartNum++)
            {  // per ogni cella del livello
               int jj = 0;
               nExpanded = 0;

               while (jj < DPtable[iPartNum].Count && nExpanded < beamWidth)
               {
                  // re-sort at avery cicle as new cells are added at the same level
                  int[] idx = new int[DPtable[iPartNum].Count];
                  for (j = 0; j < DPtable[iPartNum].Count; j++) idx[j] = j;
                  Array.Sort(idx, (a, b) => DPtable[iPartNum][a].node.bound.CompareTo(DPtable[iPartNum][b].node.bound));
                  iCell = idx[jj]; // cells by increasing bound

                  // actual nose expansion
                  if (!DPtable[iPartNum][iCell].isExpanded)
                  {  currNode = DPtable[iPartNum][iCell].node;
                     for (d = 0; d < ndim; d++)
                        if (dimValues[d] > 0) // if any cut was selected acting on dimension d
                        {  idNode = expandNode(currNode, d, iPartNum);
                           if (idDPcell < 0 && idNode >= 0) idDPcell = idNode;  // node completed
                        }

                     DPtable[iPartNum][iCell].isExpanded = true;
                     nExpanded++;
                  }
                  jj++;
               }
            }
            if (idDPcell >= 0)
               fTerminate = true;
            else
               Console.WriteLine($"Nother round, iDepth={iPartNum} idDPcell {idDPcell}");
         }
         return idDPcell; 
      }

      // beam search expansion
      int beamSearch(int beamWidth)
      {  int i,j,d,iDepth,iCell, idNode, nExpanded;
         int idDPcell=-1;
         NodeDP currNode;
         bool fTerminate = false;

         while(!fTerminate)
         { 
            for (iDepth = 0; iDepth < DPtable.Length; iDepth++)
            {  // per ogni cella dek livello
               int jj = 0;
               nExpanded = 0;

               while (jj < DPtable[iDepth].Count && nExpanded < beamWidth)
               {
                  // repeated avery cicle as new cells are added at the same level
                  int[] idx = new int[DPtable[iDepth].Count];
                  for (j = 0; j < DPtable[iDepth].Count; j++)
                     idx[j] = j;
                  Array.Sort(idx, (a, b) => DPtable[iDepth][a].node.bound.CompareTo(DPtable[iDepth][b].node.bound));
                  iCell = idx[jj]; // cells by increasing bound

                  if (!DPtable[iDepth][iCell].isExpanded)
                  {  currNode = DPtable[iDepth][iCell].node;
                     for (d = 0; d < ndim; d++)
                        if (dimValues[d] > 0) // if any cut was selected acting on dimension d
                        {
                           idNode = expandNode(currNode, d, iDepth);
                           if (idDPcell < 0 && idNode >= 0) idDPcell = idNode;  // node completed
                        }

                     DPtable[iDepth][iCell].isExpanded = true;
                     nExpanded++;
                  }
                  jj++;
               }
            }
            if(idDPcell >= 0)
               fTerminate = true;
            else
               Console.WriteLine($"Nother round, iDepth={iDepth} idDPcell {idDPcell}");
         }

         return idDPcell;
      }

      /* raffina tutte le partizioni di un nodo lungo una dimensione 
       * (un nodo figlio per partizione)
       * ritorna idCell di un nodo completato, se trovato
       */
      private int expandNode(NodeDP nd, int d, int iDepth)
      {  int i,j,h,k,id,idcell,idpoint,idpart,npartitions,treeNodeId,res=-1;
         bool isComplete;

         // add list of pointers to father one level below, if not yet initialized
         if (nd.lstFathers.Count == (iDepth+1)) 
         {  nd.lstFathers.Add(new List<List<(int,int,int)>>());
            nd.lstFcut.Add(new List<List<(int fdim, int fpart)>>());
            nd.lstIdNode.Add(new List<int>()); // random id's of tree child nodes
         }

         npartitions = nd.lstPartitions.Count;
         // ogni partizione del padre genera un figlio, con la partizione partizionata secondo dim d
         for(i=0;i<npartitions;i++)                   // for each partition of the father node
         {  if (nd.lstPartClass[i] >= 0)   continue;  // unique class, no expansion
            if (nd.usedDim[i].Contains(d)) continue;  // dimension already used to get the partition
            treeNodeId = nd.lstPartNode[i];           // the tree node (NOT DP NODE) associated with the current partition
            if(verbose >= 1)
               Console.WriteLine($" -- Partitioning node {nd.id} depth {iDepth} partition {i} depth {nd.usedDim[i].Count} num.part. {nd.lstPartitions.Count} along dim {d}");

            // qui ho il nodo figlio della partizione i-esima
            string jsonlst = JsonConvert.SerializeObject(nd);
            NodeDP newNode = JsonConvert.DeserializeObject<NodeDP>(jsonlst);
            newNode.id = totNodes++;
            if(newNode.id % 1000 == 0) Console.WriteLine($"Expanding node {nd.id} Generating node {newNode.id} depth {iDepth+1}");

            // find original father partition
            int idFathPart = 0;  // partizione nodo padre
            int idArrFath  = 0;  // posizione dell'array con le partizioni del padre nella lista dei fathers al suo livello (del padre)
            if(nd.id==0) idFathPart = i;
            else
            {  int dim = (int) newNode.usedDim[i][newNode.usedDim[i].Count-1]; // last used dimension
               idpoint = newNode.lstPartitions[i][0]; // 0 because any point of the partion is above the cut used to find the partition
               List<int> lstFathId = findFatherPos(nd, idpoint, nd.usedDim[i], newNode.usedDim[i].Count - 1);  // per ogni livello, il nodo avo nella sua lista di fratelli
               idFathPart = lstFathId[^1]; // partizione del nodo padre
               if(lstFathId.Count > 1)
                  idArrFath  = lstFathId[^2]; // figlio in cui c'è la partizione
               else idArrFath = 0;
            }
            int newDepth = nd.lstPartDepth[i] + 1;  // depth of child nodes
            while (newNode.lstFathers.Count < newDepth+1) 
            {  newNode.lstFathers.Add(new List<List<(int,int,int)>>()); // per essere sicuri che il livello ci sia
               newNode.lstFcut.Add(new List<List<(int fdim, int fpart)>>());
               newNode.lstIdNode.Add(new List<int>());
            }

            // inizializzo le nuove partizioni del figlio
            List<int>        newPartClass  = new List<int>();       // la classe della partizione, -1 non univoca
            List<int>        newPartDepth  = new List<int>();
            List<int>        newPartNode   = new List<int>();
            List<(int,int,int)>  newFathers    = new List<(int,int,int)>();
            List<(int,int)>  newFcut       = new List<(int,int)>();
            List<int>        newIdNodes    = new List<int> ();
            List<List<int>>  newpartitions = new List<List<int>>();
            List<List<int?>> newUsedDim    = new List<List<int?>>();

            for (idpart = 0; idpart < dimValues[d]+1; idpart++) 
            {  newpartitions.Add(new List<int>());

               newUsedDim.Add(new List<int?>()); // per ogni partizione del nodo, lista delle dimensioni che hanno portato a lei
               newUsedDim[idpart] = new( nd.usedDim[i] ); 
               newUsedDim[idpart].Add(d);    
               
               newFathers.Add((idArrFath, idFathPart, treeNodeId));      // for each partition, the father's originating one
               newFcut.Add((d, idpart));
               int randint = rnd.Next();
               newIdNodes.Add(randint);
               newPartNode.Add(randint);

               newPartClass.Add(-2);
               newPartDepth.Add(newDepth);
            }

            // riempio le partizioni che ottengo nei figli
            int complBound;
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

               complBound = 0;   // bound to the number of cuts needed to disentangle the partition
               if (newPartClass[idpart] == -2) // initialization
                  newPartClass[idpart] = Y[idpoint];  // classi omogenee
               else if (newPartClass[idpart] != Y[idpoint])
                  newPartClass[idpart] = -1;          // classi eterogenee
            }

            // tolgo eventuali nuove partizioni senza punti
            idpart = 0;
            while (idpart<newpartitions.Count)
               if (newpartitions[idpart].Count == 0)
               {  newpartitions.RemoveAt(idpart);
                  newPartClass.RemoveAt(idpart);
                  newPartDepth.RemoveAt(idpart);
                  newPartNode.RemoveAt(idpart);
                  newFathers.RemoveAt(idpart);
                  newIdNodes.RemoveAt(idpart);
                  newFcut.RemoveAt(idpart);
                  newUsedDim.RemoveAt(idpart);
                  if(verbose>=1)
                     Console.WriteLine("-- removed pointless partition "+idpart);  // pun intended
               }
               else
                  idpart++;

            // unisco eventuali partizioni contigue della stessa classe
            idpart = 1;
            while (idpart < newpartitions.Count)
            {
               if (newPartClass[idpart] > -1 && newPartClass[idpart] == newPartClass[idpart-1])
               {  // merge the thwo partitions and remove idpart-1
                  for (j = 0; j < newpartitions[idpart-1].Count; j++)
                     newpartitions[idpart].Insert(0,newpartitions[idpart-1][j]);
                  newpartitions[idpart].Sort();
                  newFcut[idpart] = newFcut[idpart-1];

                  newpartitions.RemoveAt(idpart-1);
                  newPartClass.RemoveAt(idpart-1);
                  newPartDepth.RemoveAt(idpart-1);
                  newPartNode.RemoveAt(idpart-1);
                  newFathers.RemoveAt(idpart-1);
                  newFcut.RemoveAt(idpart-1);
                  newIdNodes.RemoveAt (idpart-1);
                  newUsedDim.RemoveAt(idpart-1);
                  if(verbose>=1)
                     Console.WriteLine("-- merged mergeable partitions into " + idpart);
                  idpart = 0;
               }
               idpart++;
            }

            // tolgo la partizione appena espansa
            {  newNode.lstPartitions.RemoveAt(i); 
               newNode.lstPartClass.RemoveAt(i);
               newNode.lstPartDepth.RemoveAt(i);
               newNode.lstPartNode.RemoveAt(i);
               newNode.usedDim.RemoveAt(i);
            }
            // aggiungo le nuove partizioni
            {  newNode.lstPartitions = newNode.lstPartitions.Concat(newpartitions).ToList();  // lista punti di ogni partizione
               newNode.usedDim       = newNode.usedDim.Concat(newUsedDim).ToList();
               newNode.lstPartClass  = newNode.lstPartClass.Concat(newPartClass).ToList();    // la classe di ogni partizione se uniforme, sennò -1
               newNode.lstPartDepth  = newNode.lstPartDepth.Concat(newPartDepth).ToList();
               newNode.lstPartNode   = newNode.lstPartNode.Concat(newPartNode).ToList();
               newNode.lstFathers[newDepth].Add(newFathers);
               newNode.lstFcut[newDepth].Add(newFcut);
               newNode.lstIdNode[newDepth].AddRange(newIdNodes);
               newNode.hash = nodeHash(newNode);
            }

            // min and max iPartNum of node partitions
            int minDepth = ndim+1, maxDepth = 0, boundCost = 0;
            for(j=0;j<newNode.lstPartitions.Count;j++)
            {  if (newNode.lstPartDepth[j] > maxDepth)
                  maxDepth = newNode.lstPartDepth[j];
               if (newNode.lstPartDepth[j] < minDepth && newNode.lstPartClass[j] < 0)
                  minDepth = newNode.lstPartDepth[j];

               complBound = completionBound(newNode.lstPartitions[j]);
               int expCost = newNode.lstPartDepth[j] + complBound;
               if (expCost > boundCost) boundCost = expCost;
               if(verbose > 1)
                  Console.WriteLine($" Expanded node {newNode.id} partition {j} depth {newNode.lstPartDepth[j]} bound {complBound} exp.cost {expCost}");
            }
            if (verbose >= 1)
               Console.WriteLine($"Node {newNode.id} depth {newDepth} exp.cost {boundCost}");
            newNode.bound = boundCost;

            isComplete = true;
            for(int ii=0;ii<newNode.lstPartClass.Count;ii++)
               if (newNode.lstPartClass[ii] < 0)
                  isComplete = false;
            if(isComplete)  
            {  minDepth = maxDepth;
               if(verbose>=0)
                  Console.WriteLine($"Node completed, depth {maxDepth}");

               int nLevels = newNode.lstFathers.Count;
               if (newNode.lstFathers[nLevels-1].Count == 0)   // last level empty
               {  newNode.lstFathers.RemoveAt(nLevels-1);
                  newNode.lstFcut.RemoveAt(nLevels-1);
                  newNode.lstIdNode.RemoveAt(nLevels-1);
               }
            }

            // check for dominance
            List<NodeDP> lstHash = checkHash(newNode.hash);
            bool fSamePartitions = false;
            h = -1;
            if(lstHash != null)
            {  for(h=0;h<lstHash.Count;h++)
               {  if(verbose>=1)
                     Console.WriteLine($"STESSO Hash!!! nodi {newNode.id} {lstHash[h].id}");
                  fSamePartitions = isEqualPartition(newNode, lstHash[h]);   // gestisce la dominanza fra nodi
                  int depthNhash  = getDPcellDepth(lstHash[h].idDPcell);
                  if(fSamePartitions && depthNhash <= maxDepth+1)
                  {  if(verbose>=1)
                        Console.WriteLine($"Nodo {newNode.id} dominato");
                     numDominated++;
                     break;
                  }
                  else
                     fSamePartitions = false;
               }
               if (fSamePartitions)
                  continue; // newnode discarded
            }

            if(fSamePartitions) // nuovo nodo domina cella lstHash
            {  idcell = lstHash[h].idDPcell;
               var colrow = getDPtablecell(idcell);
               newNode.idDPcell = idcell;
               DPtable[colrow.Item1][colrow.Item2].node  = newNode;
               DPtable[colrow.Item1][colrow.Item2].depth = iDepth+1;
            }
            else  // nuovo nodo NON domina cella lstHash
            {  DPcell dpc = new DPcell(); // insert the node in a cell of the DP table (it is its state)
               dpc.id = totcells; totcells++;
               newNode.idDPcell = dpc.id;
               dpc.node   = newNode;
               dpc.depth  = minDepth; // profondità nodo quella min di partizione ancora da espandere
               dpc.nnodes = 1;
               dpc.isExpanded = false;
               if(fDPpartition)
                  DPtable[newNode.lstPartitions.Count].Add(dpc); // recursion based on the number of partitions
               else
                  DPtable[dpc.depth].Add(dpc); // recursion based on the depth of the trees
               if(verbose>=1)
                  Console.WriteLine($"expanded node {nd.id} into {newNode.id} num.part {newNode.lstPartitions.Count} bound {newNode.bound}");
               if(verbose>=2)
                  printPartitions(newNode);
               if(isComplete && res < 0) 
               {  res = dpc.id;
                  break;
               }
            }
         }
         if (nd.lstFathers[nd.lstFathers.Count - 1].Count == 0)   // last level empty, added for cloning at the beginning of the method
         {  nd.lstFathers.RemoveAt(nd.lstFathers.Count - 1);
            nd.lstFcut.RemoveAt(nd.lstFcut.Count - 1);
            nd.lstIdNode.RemoveAt(nd.lstIdNode.Count - 1);
         }
         return res;
      }

      // computes a bound to the number of dimensions that will need to be cut to complete a partition
      int completionBound(List<int> partition)
      {  int i,j,d,idPoint;
         int bound=0;
         double[,] mind = new double[nclasses, ndim]; // min values taken by elements of each class along each dim
         double[,] maxd = new double[nclasses, ndim]; // max values taken by elements of each class along each dim

         for (i=0;i<nclasses;i++)
            for(d=0;d<ndim;d++)
            {  mind[i,d] = double.MaxValue;
               maxd[i,d] = double.MinValue;
            }

         // find the bounding box of the AABB containing points of each class for the partition
         for (j = 0; j < partition.Count; j++)
         {  idPoint = partition[j];
            i = Y[idPoint];
            for(d=0;d<ndim;d++)
            {  if (X[idPoint, d] < mind[i,d])
                  mind[i, d] = X[idPoint,d];
               if (X[idPoint, d] > maxd[i, d])
                  maxd[i, d] = X[idPoint, d];
            }
         }

         // check for interseactions
         int[,] numIntersect = new int[nclasses, nclasses];
         bound = 0;
         for (int i1 = 0; i1 < nclasses - 1; i1++)
            for (int i2 = i1+1; i2 < nclasses; i2++)
               for (d = 0; d < ndim; d++)
                  if ( ((maxd[i1,d] > mind[i2,d]) && (mind[i1,d] < maxd[i2,d])) ||
                       ((maxd[i2,d] > mind[i1,d]) && (mind[i2,d] < maxd[i1,d])) )   // if the classes intersect on the dimension
                  {  numIntersect[i1,i2]++;
                     if (numIntersect[i1,i2] > bound)
                        bound = numIntersect[i1,i2];
                  }

         return bound;
      }

      // print all partitionings on a file
      void printPartitions()
      {  int i,j,k;
         StreamWriter fout = new StreamWriter("partitions.out");

         for (int iDepth = 0; iDepth < DPtable.Length; iDepth++)
         {  if (DPtable[iDepth].Count == 0) continue;
            for (i = 0; i < DPtable[iDepth].Count;i++)
            {
               var cellCoord = getDPtablecell(i);
               NodeDP ndp = DPtable[cellCoord.Item1][cellCoord.Item2].node;
               int[] firsts = new int[ndp.lstPartitions.Count];
               int[] idx = new int[ndp.lstPartitions.Count];
               for (j = 0; j < ndp.lstPartitions.Count; j++)
               {  firsts[j] = ndp.lstPartitions[j][0];
                  idx[j] = j;
               }
               Array.Sort(idx, (a, b) => firsts[a].CompareTo(firsts[b]));

               for (int jj=0;jj<ndp.lstPartitions.Count;jj++)
               {  j = idx[jj];
                  for(k=0;k<ndp.lstPartitions[j].Count;k++)
                  {
                     Console.Write(" " + ndp.lstPartitions[j][k]);
                     fout.Write(" " + ndp.lstPartitions[j][k]);
                  }
                  Console.WriteLine();
                  fout.Write(" *");
               }
               Console.WriteLine("==================");
               fout.WriteLine();
            }
         }
         fout.Close();
      }

      // trova la posizione del nodo contenente il punto nella lista dei fathers al livello iPartNum
      private List<int> findFatherPos(NodeDP nd, int idPoint, List<int?> usedDim, int iDepth)
      {  int i=-1,k,d,id;
         List<int> lstFathId = new List<int>(); // lista posizioni padri nei livelli father

         id=0; // depth id
         while(id<=iDepth)
         {  d = (int) usedDim[id];
            k = 0;
            while (cutdim[k] != d) k++; // first value for dimension d
            i = 0;   // id of the new partition of the node
            while (k < cutdim.Length && cutdim[k] == d && cutval[k] < X[idPoint, d]) // find the partition of idpoint
            {  k++;
               i++;
            }
            lstFathId.Add(i); // per ogni livello, il nodo avo nella sua lista di fratelli
            id++;
         }

         return lstFathId;
      }

      // il nodo con lo stesso hash se h già in tabella, null altrimenti
      private List<NodeDP> checkHash(int h)
      {  int i,j;
         List<NodeDP> res = new List<NodeDP>();

         for(i=0;i<DPtable.Length;i++)
            for (j = 0; j < DPtable[i].Count;j++)
               if (DPtable[i][j].node.hash == h)
                  res.Add(DPtable[i][j].node);
         return res;
      }

      // gets the iPartNum of a node given its id in the DPtable
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
         if(newnode.lstPartitions.Count != nhash.lstPartitions.Count)
         {  if(verbose>=1)
               Console.WriteLine("  -- different number of partitions");
            goto lend;
         }
         int[] first1=new int[nhash.lstPartitions.Count], first2=new int[nhash.lstPartitions.Count]; // first elements of each partition
         for(i=0;i<nhash.lstPartitions.Count;i++)
         {  first1[i] = newnode.lstPartitions[i][0];
            first2[i] = nhash.lstPartitions[i][0];
         }
         int[] idxNew  = getSortIdx(first1);
         int[] idxHash = getSortIdx(first2);

         for (i=0;i<nhash.lstPartitions.Count;i++)
         {
            if (nhash.lstPartitions[idxHash[i]].Count != newnode.lstPartitions[idxNew[i]].Count)
            {  if(verbose>=1)
                  Console.WriteLine($"  -- different num nodes of partition {i} : {nhash.lstPartitions[idxHash[i]].Count} {newnode.lstPartitions[idxNew[i]].Count}");
               goto lend;
            }
            for (j = 0; j < newnode.lstPartitions[idxNew[i]].Count;j++)
               if (nhash.lstPartitions[idxHash[i]][j] != newnode.lstPartitions[idxNew[i]][j])
               {  if(verbose>=1)
                     Console.WriteLine($"  -- different node: i {i} j {j} : {nhash.lstPartitions[idxHash[i]][j]} {newnode.lstPartitions[idxNew[i]][j]}");
                  goto lend;
               }
         }
         // qui stesse partizioni
         res = true;

lend:    if(verbose>=1) Console.WriteLine($"Same partitions: {res}");
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
               hash = (hash * ( ((j*7)*ndClus.lstPartitions[idxPart[i]][j]) % 31 + 1)) % 193939;
         return hash;
      }

      // translates tree structure into decTreee for plotting
      private void marshalTree(int idDPcell)
      {  int i,j,k,r,d,nid=0,idFather,idNode,idPart,idLeaf=0;
         List<List<int>> nodes = new List<List<int>>();
         Dictionary<int,int> mapId = new Dictionary<int, int>(); // maps random node id in lstIdNode to actual ones
         var cellCoord = getDPtablecell(idDPcell);
         NodeDP ndp = DPtable[cellCoord.Item1][cellCoord.Item2].node;

         for(int iDepth=0; iDepth < ndp.lstFathers.Count; iDepth++)
         {  nodes.Add(new List<int>());
            sortFatherLists(ndp,iDepth);
            r = 0;   // indice del nodo nel suo livello
            for (i = 0; i < ndp.lstFathers[iDepth].Count; i++)
               for (j = 0; j < ndp.lstFathers[iDepth][i].Count; j++)
               {  NodeHeu n0 = new NodeHeu();
                  n0.id  = nid++;
                  n0.dim = -1;
                  n0.rndid = ndp.lstIdNode[iDepth][r]; r++;
                  mapId.Add(n0.rndid, n0.id);
                  n0.lstSons   = new List<int>();
                  n0.lstPoints = new List<int>(); // punti nel nodo
                  n0.lstCuts   = new List<int>(); // tutti i cut attivi al nodo (se interno)
                  n0.isLeaf    = true;
                  nodes[iDepth].Add(n0.id); // le id dei nodi, invece delle partizioni in fathers
                  if(iDepth>0)
                  {  idFather = mapId[ndp.lstFathers[iDepth][i][j].idFather];           // qui l'id del nodo padre
                     n0.idFather = idFather;
                     if (verbose>=1)
                        Console.WriteLine($" -- arco {idFather}-{n0.id}");
                     decTree[idFather].lstSons.Add(n0.id);
                     decTree[idFather].isLeaf = false;
                     decTree[idFather].dim    = ndp.lstFcut[iDepth][i][j].fdim;
                     if (j > 0) // la partizione più bassa non è selezionata da un cut
                        decTree[idFather].lstCuts.Add(ndp.lstFcut[iDepth][i][j].fpart-1); // il cut che identifica la partizione
                  }
                  decTree.Add(n0);
               }
         }

         // -------------- reconstruction of cuts used to come to each node (if any)
         // da indice del cut nella dimensione a indice assoluto
         for (i = 0; i < decTree.Count; i++)
         {
            if (decTree[i].lstCuts.Count > 0)
            {  d = decTree[i].dim;
               k = 0;
               while (cutdim[k] != d) k++; // first value for dimension d
               for (j = 0; j < decTree[i].lstCuts.Count; j++)
                  decTree[i].lstCuts[j] += k;
            }
         }

         // --------------- the leaf node corresponding to each partition
         part2node = new int[ndp.lstPartitions.Count]; // the node corrsponding to each partition
         for(i=0;i<part2node.Length;i++)
            part2node[i] = mapId[ndp.lstPartNode[i]];

         if(verbose>=1)
         {  Console.Write("Leaves: ");
            for(i=0;i<part2node.Length;i++) Console.Write($" {part2node[i]}");
            Console.WriteLine();
         }

         // --------------- points at each leaf
         for(idPart=0;idPart<ndp.lstPartitions.Count;idPart++)
         {  k = part2node[idPart];
            for (i = 0; i < ndp.lstPartitions[idPart].Count;i++)
               decTree[k].lstPoints.Add(ndp.lstPartitions[idPart][i]);
            decTree[k].npoints = decTree[k].lstPoints.Count;
         }

         // --------------- recostruction of internal node points (for each node, add its points to its ancestors)
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
      }

      // ordina le liste fathers (e collegata fcuts) per campo part crescente. Bubble sort.
      void sortFatherLists(NodeDP ndp, int iDepth)
      {  int i,j;
         bool loopAgain = true;
         List<(int, int, int)> temp = new List<(int, int, int)>();
         List<(int, int)> tempCut = new List<(int, int)>();

         while (loopAgain)
         {  loopAgain = false;
            for (i = 0; i < ndp.lstFathers[iDepth].Count-1;i++)
               // assumo tutti elementi array interno stessa partizione (ex. [01 01][00 00 00])
               if (ndp.lstFathers[iDepth][i][0].part > ndp.lstFathers[iDepth][i+1][0].part)
               {  temp = ndp.lstFathers[iDepth][i];
                  ndp.lstFathers[iDepth][i] = ndp.lstFathers[iDepth][i+1];
                  ndp.lstFathers[iDepth][i+1] = temp;

                  tempCut = ndp.lstFcut[iDepth][i];
                  ndp.lstFcut[iDepth][i] = ndp.lstFcut[iDepth][i+1];
                  ndp.lstFcut[iDepth][i+1] = tempCut;

                  loopAgain = true;
               }
         }
      }

      // finds the partition coresponding to a decision tree leaf
      private int whichPartition(int idNode, NodeDP ndp)
      {  int i,j,k,d;
         int idPart = -1;

         for(idPart=0;idPart<ndp.lstPartitions.Count;idPart++)
         {  i = ndp.lstPartitions[idPart][0];
            k = 0; // faccio cadere il punto nell'albero 
            while(!decTree[k].isLeaf)
            {  d = decTree[k].dim;
               j = 0;
               while (j < decTree[k].lstCuts.Count && cutval[decTree[k].lstCuts[j]] < X[i,d])
                  j++;
               k = decTree[k].lstSons[j];
            }
            Debug.Assert(Y[i] == ndp.lstPartClass[idPart]);
            if(k==idNode)  // found the partition corresponding to the leaf node
               break;
         }

         return idPart;
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
         string confPath = File.Exists("config.json") ? "config.json" : @"..\..\..\config.json";
         StreamReader fconf = new StreamReader(confPath);
         string jconfig = fconf.ReadToEnd();
         fconf.Close();

         var config = JsonConvert.DeserializeObject<dynamic>(jconfig);
         try
         {  string path = Convert.ToString(config.datapath);
            string file = Convert.ToString(config.datafile);
            splitRule   = Convert.ToString(config.splitRule);
            splitDir    = Convert.ToString(config.splitDir);
            method      = Convert.ToString(config.method);
            verbose     = Convert.ToInt32(config.verbose);
            fBeamSearch = (Convert.ToInt32(config.beam) == 1 ? true : false);
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
         for(i=0;i<n;i++) // for each point, I look for the child conteining it
         {  currnode = 0;
            while (!decTree[currnode].isLeaf)
            {  d  = decTree[currnode].dim;
               nc = decTree[currnode].lstCuts.Count;
               for(j=0;j<nc;j++)
                  if (X[i,d] < cutval[decTree[currnode].lstCuts[j]])
                     break; // trovato il figlio contenente il punto

               child = decTree[currnode].lstSons[j];
               heights[child] = heights[currnode] + 1;
               if (heights[child] > treeHeight) treeHeight = heights[child];
               currnode = child;
            }

            if(method!="heuristic")
            {  for(j=0;j<part2node.Length;j++)
               {  k = part2node[j];
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
            if(verbose>=1)
               Console.WriteLine($"Record {i} node {currnode} class {Y[i]}");
         }

         // per ogni cut attivo, controllo che ci sia un punto sopra e uno sotto
         for(i=0;i<decTree.Count;i++)
         {  d = decTree[i].dim;
            if(d<0) continue; // a leaf
            for (k = 0; k < decTree[i].lstCuts.Count; k++)
            {  //Console.WriteLine($"cutdim[decTree[i].lstCuts[k]] = {cutdim[decTree[i].lstCuts[k]]} d={d}");
               Debug.Assert(cutdim[decTree[i].lstCuts[k]] == d);
               int kkCut = decTree[i].lstCuts[k];
               for (int i1 = 0; i1 < decTree[i].lstPoints.Count; i1++)
               {  int iPoint1 = decTree[i].lstPoints[i1];
                  for (int i2 = 0; i2 < decTree[i].lstPoints.Count; i2++)
                  {  int iPoint2 = decTree[i].lstPoints[i2];
                     if (X[iPoint1, d] < cutval[kkCut] && X[iPoint2, d] > cutval[kkCut] ||
                         X[iPoint1, d] > cutval[kkCut] && X[iPoint2, d] < cutval[kkCut])
                        goto l0; // punti separati 
                  }
               }
               //res = false;
               Console.WriteLine($"ERROR, cut not active {kkCut}");
               goto lend;
l0:            continue;
            }
         }

         if (res) Console.WriteLine("Checked. Solution is ok");
         Console.WriteLine($"Tree height   = {treeHeight}");
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
         Console.WriteLine($"Tot cuts      = {cutdim.Length}");
         Console.WriteLine($"Tot nodes     = {totNodes}");
         Console.WriteLine($"Tot leaves    = {totLeaves}");
         Console.WriteLine($"Num dominated = {numDominated}");
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
                  h=computeVariance(lstNptClass);
                  if(splitDir=="max" ? h>splith : h<splith)
                  {  splith=h;
                     splitd=d;
                  }
                  break;
               case "gini":
                  h=computeGiniIndex(lstNptClass);
                  if(splitDir=="max" ? h>splith : h<splith)
                  {  splith=h;
                     splitd=d;
                  }
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
         {  sums[i] = 0.0;
            for (j=0;j<nclasses;j++)
               sums[i] += lstNptson[i][j];
            tot += sums[i];
         }
         for (i = 0; i < lstNptson.Count; i++)
            if (sums[i] > 0)
               h += (sums[i]/tot)*Math.Log(sums[i] / tot);

         return -h;
      }

      // variance at the node
      private double computeVariance(List<int[]> lstNptson)
      {  int i,j;
         double mean, var = 0;
         double tot = 0;
         double[] sums;

         sums=new double[lstNptson.Count];
         for(i=0;i<lstNptson.Count;i++)
         {
            sums[i]=0.0;
            for(j=0;j<nclasses;j++)
               sums[i]+=lstNptson[i][j];
            tot+=sums[i];
         }
         mean =sums.Average();
         var  =sums.Select(num => Math.Pow(num-mean,2)).Average();
         return var;
      }

      private double computeGiniIndex(List<int[]> lstNptson)
      {  int i,j,nsum;
         double mean, var = 0;
         double tot = 0;
         double[] sums;

         sums=new double[lstNptson.Count];
         for(i=0;i<lstNptson.Count;i++)
         {  sums[i]=0.0;
            for(j=0;j<nclasses;j++)
               sums[i]+=lstNptson[i][j];
            tot+=sums[i];
         }
         mean=sums.Average();
         nsum=sums.Length;

         // Calculate the sum of absolute differences
         double sumOfDifferences = 0;
         for(i = 0;i<nsum;i++)
            for(j = 0;j<nsum;j++)
               sumOfDifferences+=Math.Abs(sums[i]-sums[j]);

         // Calculate the Gini Index
         double giniIndex = sumOfDifferences/(2.0*n*n*mean);
         return giniIndex;
      }

      // information gain, one level lookahead
      static double computeInformationGain(int[] features,int[] labels,int[] splitPoints)
      {  double initialEntropy = CalculateEntropy(labels);
         var partitions = PartitionData(features,labels,splitPoints);

         // Calculate weighted entropy of all partitions
         double weightedEntropy = 0;
         foreach(var partition in partitions)
         {  double partitionWeight = (double)partition.Count/labels.Length;
            weightedEntropy+=partitionWeight*CalculateEntropy(partition.ToArray());
         }

         // Information Gain
         return initialEntropy-weightedEntropy;
      }

      static List<List<int>> PartitionData(int[] features,int[] labels,int[] splitPoints)
      {  var partitions = new List<List<int>>();

         // Initialize lists for each partition
         foreach(var split in splitPoints)
            partitions.Add(new List<int>());
         partitions.Add(new List<int>()); // Add an extra list for values greater than all split points

         // Partition labels based on feature values and split points
         for(int i = 0;i<features.Length;i++)
         {  int feature = features[i];
            int label = labels[i];

            bool added = false;
            for(int j = 0;j<splitPoints.Length;j++)
               if(feature<=splitPoints[j])
               {  partitions[j].Add(label);
                  added=true;
                  break;
               }

            if(!added)
               partitions[splitPoints.Length].Add(label); // Add to the final partition
         }

         return partitions;
      }

      static double CalculateEntropy(int[] labels)
      {  var labelCounts = labels.GroupBy(x => x).Select(g => g.Count()).ToArray();
         double entropy = 0;

         foreach(var count in labelCounts)
         {  double probability = (double)count/labels.Length;
            entropy-=probability*Math.Log2(probability);
         }

         return entropy;
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

      // outputs all partitions
      private void printPartitions(NodeDP ndp)
      {  int i,j;
         for(i=0;i<ndp.lstPartitions.Count;i++)
         {  Console.Write($" part.{i} - ");
            for (j = 0; j < ndp.lstPartitions[i].Count;j++)
               Console.Write($" {ndp.lstPartitions[i][j]}");
            Console.WriteLine();
         }
      }
   }
}
