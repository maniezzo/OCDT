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

/* Data is in X, classes in Y. Attributes are columns of X
*/

namespace PlotTreeCsharp
{
   // Each node of the non binary decision tree
   public class Node
   {
      public Node() { }
      public Node(int id, int ndim, int nclasses) 
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
    * NodeClus: the partitions at the node + info
    */
   // a cell of the DP table
   public class DPcell
   {  public int id;
      public NodeClus node;   // the node 
      public int depth;       // distance from the root
      public int nnodes;      // number of tree nodes so far
      public bool isExpanded; // the cell was expanded
   }

   // Node of the exact tree, explicit point partitions into clusters
   public class NodeClus
   {  public NodeClus() { this.lstPartitions = new List<List<int>>(); }
      public NodeClus(int id, int ndim, int nclasses) 
      {  this.id = id;
         this.usedDim       = new List<List<int?>> { };
         this.lstPartitions = new List<List<int>> { };
      }

      public int id;
      public int dim;      // dimension (attribute, column) associated with the node
      public int npoints;  // number of points (records) clustered in the node
      public int hash;     // hash code of the partition
      public List<List<int?>> usedDim;      // dimensions used in the path to each partition of the node (will give the tree)
      public List<List<int>> lstPartitions; // list of the point in each partitions at the node
      public List<int> lstPartClass;        // the class of each partition, -1 heterogeneous
   }

   internal class TreePlotter
   {  private double[,] X;
      private int[]     Y;
      private List<Node> decTree;     // l'albero euristico
      private List<DPcell>[] DPtable; // la tabella della dinamica, una riga ogni altezza dell'albero (max ndim)
      private int[]     cutdim;     // dimension on which each cut acts
      private double[]  cutval;     // value where the cut acts
      private int[]     dimValues;  // number of values (of cuts) acting on each dimension
      private int numcol,ndim;      // num dimensions (attributes, columns of X)
      private int n, nclasses;      // num of records, num of classes
      private string splitRule;     // criterium for node plitting
      private string splitDir;      // max o min
      private string method;        // exact or heuristic
      private string[] dataColumns;
      private int totNodes=0, treeHeight=0, totLeaves=0;

      public TreePlotter()
      {  decTree = new List<Node>();
      }
      public void run_plotter()
      {  string dataset = readConfig();
         Console.WriteLine($"Plotting {dataset}");
         readData(dataset); // gets the X and Y matrices (data and class)

         DPtable = new List<DPcell>[ndim];
         for(int i=0;i<ndim;i++) DPtable[i] = new List<DPcell>();

         if(method == "exact")
            exactTree();
         else
            heuristicTree();

         postProcessing();
         bool ok = checkSol();
         if(ok) plotTree(dataset);
      }

      private void exactTree()
      {  int i, j, d, idNode;
         double maxVal = double.MaxValue; // limite superiore ai val da considerare per la dim corrente
         NodeClus currNode;
         bool[] fOut   = new bool[n];     // punti da escludere

         List<int>[] dimCuts = new List<int>[numcol]; // which cuts for each dim
         for (i = 0; i < numcol; i++)        dimCuts[i] = new List<int>();
         for (i = 0; i < cutdim.Length; i++) dimCuts[cutdim[i]].Add(i);   // dimesioni su cui agisce ogni cut

         Console.WriteLine("Exact tree construction");
         int[,] idx;          // indices of sorted values for each column
         idx = getSortIdxAllDim();

         List<int> lstPoints = new List<int> ();         // indici punti del dataset
         for (i=0;i<n;i++) lstPoints.Add(i);
         List<int[]> lstNptClass = new List<int[]>();    // num punti di ogni slice individuata
         List<int>[] ptSlice = new List<int>[nclasses];

         // --------------------------------------------------------- node 0
         idNode = totNodes++;
         currNode = new NodeClus(idNode, ndim, nclasses);
         currNode.lstPartitions.Add(new List<int>());
         currNode.lstPartClass = new List<int>();
         currNode.lstPartClass.Add(-1);  // unica partizione, dati eterogenei
         for (i=0;i<n;i++) currNode.lstPartitions[0].Add(i);       // tutti i punti nell'unica partizione
         for (i = 0; i < ndim; i++) currNode.usedDim[0].Add(null); // dim usate fino a lui (radice, nessuna)
         currNode.npoints = n;
         DPcell dpc = new DPcell();
         dpc.id   = 0;
         dpc.node = currNode;
         dpc.depth = 0;
         dpc.nnodes = 1;
         dpc.isExpanded = false;
         DPtable[0].Add(dpc);

         // ogni cut come partiziona
         for (d = 0; d < ndim; d++)
            if (dimValues[d] > 0)
               expandNode(currNode, cutdim[d]);

         Environment.Exit(0);
      }

      // raffina tutte le partizioni di un nodo in accordo con una dimensione (genera nodi figli)
      private void expandNode(NodeClus nd, int d)
      {  int i,j,k,id,idpoint,idpart,npartitions;

         npartitions = nd.lstPartitions.Count;
         for(i=0;i<npartitions;i++)  // for each partition of the node
         {
            if (nd.lstPartClass[i] >= 0) continue;    // unique class, no expansion

            List<int> partClass = new List<int>();    // la classe della partizione, -1 non univoca
            List<List<int>> newpartitions = new List<List<int>>();
            for (idpart = 0; idpart < dimValues[d]+1; idpart++) 
            {  newpartitions.Add(new List<int>());
               partClass.Add(-2);
            }

            for (j = 0; j < nd.lstPartitions[i].Count;j++)  // for each point in the partition
            {  idpoint = nd.lstPartitions[i][j];
               k = 0;
               while (cutdim[k]!=d) k++; // first value for dimension d
               idpart=0;   // id of the new partiorn of the node
               while (cutdim[k] == d && cutval[k] < X[idpoint,d]) // find the partition of idpoint
               {  k++;
                  idpart++;
               }
               newpartitions[idpart].Add(idpoint); // le nuove partizioni lungo la dimensione, sostituiscono la vecchia

               if (partClass[idpart] == -2) // initialization
                  partClass[idpart] = Y[idpoint];
               else if (partClass[idpart] != Y[idpoint])
                  partClass[idpart] = -1; // classi eterogenee
            }

            // qui ho il nodo figlio della partizione i-esima
            string jsonlst =JsonConvert.SerializeObject(nd);
            NodeClus newNode = JsonConvert.DeserializeObject<NodeClus>(jsonlst);
            newNode.id  = totNodes++;
            newNode.dim = d;
            newNode.usedDim.Add(d);
            newNode.hash = nodeHash(newNode);
         }
      }

      // hash function of a node (mod product of its points). Assumes id in partitions to be ordered
      int nodeHash(NodeClus ndClus)
      {  int i,j,hash = 1;
         int[] firstElems = new int[ndClus.lstPartitions.Count];
         for(i=0;i<firstElems.Length;i++) firstElems[i] = ndClus.lstPartitions[i][0]; // array con i primi elementi di ogni partizione
         int[] idxPart = getSortIdx(firstElems); // indici ordinati partizioni per primo elemento crescente

         for(i=0;i<idxPart.Length;i++)
            for (j = 0; j < ndClus.lstPartitions[idxPart[i]].Count;j++)
               hash = (hash * (ndClus.lstPartitions[idxPart[i]][j] % 31 + 1)) % 193939;
         return hash;
      }

      // removes nodes with no points
      private void postProcessing()
      {  int i,j;

         for(i=0;i<decTree.Count;i++) 
            if (decTree[i].npoints == 0)
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
         Node currNode;
         // Initially mark all vertices as not visited
         // Boolean[] visited = new Boolean[V];

         // Create a stack for DFS
         Stack<int> stack = new Stack<int>();

         // Push the current source node
         idNode = decTree.Count;
         currNode = new Node(idNode, ndim, nclasses);
         for (i = 0; i < ndim; i++) currNode.isUsedDim[i] = false;
         for (i = 0; i < nclasses; i++) currNode.nPointClass[i] = 0;
         for (i = 0; i < n; i++) currNode.lstPoints.Add(i);
         currNode.npoints = n;
         decTree.Add(currNode);
         //fillNode(currNode,idx);

         stack.Push(idNode);

         while (stack.Count > 0)
         {
            // Pop a vertex from stack and print it
            // idNode = stack.Peek();
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

            // Get all offsprings of the popped vertex s, if not visited, then push it to the stack.
            foreach (int v in decTree[idNode].lstSons)
               if (!decTree[v].visited)
                  stack.Push(v);
         }
      }

      // check the correctness of the tree
      private bool checkSol()
      {  int i,j,d,nc,idcut,currnode,child;
         bool res=true;
         int[] heights = new int[decTree.Count];

         heights[0]=0;
         for(i=0;i<n;i++)
         {
            currnode = 0;
            while (!decTree[currnode].isLeaf)
            {
               d = decTree[currnode].dim;
               nc = decTree[currnode].lstCuts.Count;
               for(j=0;j<nc;j++)
                  if (X[i,d] < cutval[decTree[currnode].lstCuts[j]])
                  {  
                     //child = decTree[currnode].lstSons[j];
                     //heights[child] = heights[currnode]+1;
                     //if (heights[child]>treeHeight) treeHeight = heights[child];
                     //currnode = child;
                     nc = int.MaxValue; // to avoid entering the following if
                     break;
                  }
               //if(j==nc)
               //   currnode = decTree[currnode].lstSons[j];

               child = decTree[currnode].lstSons[j];
               heights[child] = heights[currnode] + 1;
               if (heights[child] > treeHeight) treeHeight = heights[child];
               currnode = child;
            }
            if (Y[i] != Y[decTree[currnode].lstPoints[0]])
            {  res = false;
               Console.WriteLine($"ERROR, misclassification of record {i}");
               goto lend;
            }
            else
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
      private void fillNode(Node currNode, int[,] idx)
      {  int i,j,jj,d,pt,splitd;
         double h,splith; // split criterium value
         List<int[]> lstNptClass;
         List<int> lstp;
         bool[] fOut;
         Node child;
         bool fSkip,isLeaf;
         Node prev;

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
            child = new Node(decTree.Count(), ndim, nclasses); // tentative son
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
         {  child = new Node(decTree.Count(), ndim, nclasses); // tentative son
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
      private List<int> separateNodePoints(Node currNode, List<int[]> lstNptClass, bool[] fOut, double maxVal, int d)
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
