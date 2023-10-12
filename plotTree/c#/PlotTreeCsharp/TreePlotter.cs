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

   internal class TreePlotter
   {
      private int[]     cutdim;
      private double[]  cutval;
      private string[]  dataColumns;
      private double[,] X;
      private int[]     Y;
      private int numcol,ndim;  // num dimensions (attributes, columns of X)
      private int n, nclasses;  // num of records, num of classes
      private List<Node> decTree;
      private string splitRule; // criterium for node plitting

      public TreePlotter() 
      {
         decTree = new List<Node> ();
      }
      public void run_plotter()
      {
         Console.WriteLine("Plotting");
         string dataset = readConfig();
         readData(dataset); // gets the X and Y matrices (data and class)
         makeTree();
         bool ok = checkSol();
         if(ok) plotTree(dataset);
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
            splitRule   = Convert.ToString(config.splitRule);
            dataset = path + file;
         }
         catch (Exception ex)
         { Console.WriteLine(ex.Message); }
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
               string[] elem = line.Split(',');
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
         catch(Exception ex)
         { Console.WriteLine(ex.Message); }

         // read cuts
         try
         {
            StreamReader fin = new StreamReader(dataset + "_cuts.json");
            string jcuts = fin.ReadToEnd();
            fin.Close();

            var cuts = JsonConvert.DeserializeObject<dynamic>(jcuts);
            cutdim = cuts.dim.ToObject<int[]>();
            cutval = cuts.pos.ToObject<double[]>();
         }
         catch (Exception ex)
         { Console.WriteLine(ex.Message); }
      }

      // starts the process of tree construction
      private void makeTree()
      {  int i,j;
         List<int>[] dimCuts = new List<int>[numcol]; // which cuts for ech dim
         for(i=0;i<numcol;i++)
            dimCuts[i] = new List<int>();
         for(i=0;i<cutdim.Length;i++)
            dimCuts[cutdim[i]].Add(i);

         int[,] idx; // indices of sorted values for each column
         idx = getSortIdx();
         depthFirstConstruction(idx); // construct the tree
      }

      // check the correctness of the tree
      private bool checkSol()
      {  int i,j,d,idcut,currnode;
         bool res=true;

         for(i=0;i<n;i++)
         {
            currnode  = 0;
            if (decTree[currnode].isLeaf)
               if (Y[i] != Y[decTree[currnode].lstSons[0]])
               {  res = false;
                  Console.WriteLine($"ERROR, misclassification of record {i}");
                  goto lend;
               }
            else
            {
               d = decTree[currnode].dim;
               for(j=0;j<decTree[currnode].lstCuts.Count;j++)
                  if (X[i,d] < decTree[currnode].lstCuts[j])
                     currnode = decTree[currnode].lstSons[j];
               if(j==decTree[currnode].lstCuts.Count)
                  currnode = decTree[currnode].lstSons[j];
            }
         }
         if(res) Console.WriteLine("Checked. Solution is ok");
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
               {
                  fout.WriteLine($"{i}  [shape = box label = \"{decTree[i].id}\nclass {Y[decTree[i].lstPoints[0]]}\"]");
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
      }

      // initializes fields of a new node
      private void fillNode(Node currNode, int[,] idx)
      {  int i,j,jj,d,pt,mind;
         double h,minh; // split criterium value
         List<int[]> lstNptson;
         bool[] fOut;

         for (i = 0; i < currNode.npoints; i++)
         {  pt = currNode.lstPoints[i];
            currNode.nPointClass[Y[pt]]++;
         }
         minh = double.MaxValue;
         mind = int.MaxValue;
         for (d=0;d<ndim;d++)  // for each dimension upon which we could separate
         {
            if (currNode.isUsedDim[d]) continue;
            lstNptson = new List<int[]> (); // for each value range, how many of each class
            fOut = new bool[currNode.npoints]; // point already considered
            for(j=0;j<cutdim.Length;j++)  // for each cut acting on that dimension
            {
               if (cutdim[j]!=d) continue;
               separateNodePoints(currNode, lstNptson, fOut, cutval[j], d);
            }
            // points after the biggest cut
            separateNodePoints(currNode, lstNptson, fOut, double.MaxValue, d);
            if (lstNptson.Count() == 1) continue; // dim d generates no separation
            switch (splitRule)
            {  case "entropy": 
                  h = computeEntropy(lstNptson);
                  if(h<minh) 
                  {  minh = h;
                     mind = d;
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
         
         currNode.dim = mind;
         currNode.isUsedDim[mind] = true;
         for (j = 0; j < cutdim.Length; j++)  // for each cut acting on that dimension
            if (cutdim[j] == mind) 
               currNode.lstCuts.Add(j);       // cuts active at the node

         // generate the offsprings of the current node
         j=0;
         while (j <= currNode.lstCuts.Count() )
         {  Node son = new Node(decTree.Count(),ndim,nclasses);
            son.isUsedDim = currNode.isUsedDim;
            decTree.Add(son);
            currNode.lstSons.Add(son.id);
            j++;
         }

         // compute the points of each offspring
         lstNptson = new List<int[]>(); // for each value range, how many of each class
         fOut = new bool[currNode.npoints]; // points already considered
         for (jj = 0; jj < currNode.lstCuts.Count(); jj++)  // for each cut acting on that dimension
         {  j = currNode.lstCuts[jj];
            i = currNode.lstSons[jj];
            decTree[i].lstPoints = separateNodePoints(currNode, lstNptson, fOut, cutval[j], mind);
            decTree[i].npoints = decTree[i].lstPoints.Count();
            // check is son is a leaf
            for (d = 0; d < nclasses; d++)
               if (lstNptson[jj][d] == decTree[i].npoints)
                  decTree[i].isLeaf = true;
         }
         // points after the biggest cut
         i = currNode.lstSons[jj];
         decTree[i].lstPoints = separateNodePoints(currNode, lstNptson, fOut, double.MaxValue, mind);
         decTree[i].npoints  = decTree[i].lstPoints.Count();
         // check is son is a leaf
         for (d = 0; d < nclasses; d++)
            if (lstNptson[jj][d] == decTree[i].npoints)
               decTree[i].isLeaf = true;
      }

      // calcola i punti in ogni segmento definito dai cut
      private List<int> separateNodePoints(Node currNode, List<int[]> lstNptson, bool[] fOut, double maxVal, int d)
      {  int i,pt;
         List<int> ptslice; // points of each slice (not separated per class)
         int[] nptslice = new int[nclasses]; // num points of each slice, per class

         ptslice = new List<int>();
         for (i = 0; i < currNode.npoints; i++)
         {
            pt = currNode.lstPoints[i];
            if (!fOut[i] && X[pt, d] < maxVal)
            {
               ptslice.Add(pt);
               nptslice[Y[pt]]++;
               fOut[i] = true;
            }
         }
         lstNptson.Add(nptslice);
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

      // Depth-first construction
      private void depthFirstConstruction(int[,] idx)
      {  int i,j,idNode;
         Node currNode;
         // Initially mark all vertices as not visited
         // Boolean[] visited = new Boolean[V];

         // Create a stack for DFS
         Stack<int> stack = new Stack<int>();

         // Push the current source node
         idNode = decTree.Count;
         currNode = new Node(idNode,ndim,nclasses);
         for(i=0;i<ndim;i++)     currNode.isUsedDim[i] = false;
         for(i=0;i<nclasses;i++) currNode.nPointClass[i] = 0;
         for(i=0;i<n;i++)        currNode.lstPoints.Add(i);
         currNode.npoints = n;
         decTree.Add(currNode);
         //fillNode(currNode,idx);

         stack.Push(idNode);

         while (stack.Count > 0)
         {
            // Pop a vertex from stack and print it
            idNode = stack.Peek();
            stack.Pop();
            currNode = decTree[idNode];

            // we work on the popped item if it is not visited.
            if (!currNode.visited)
            {  Console.WriteLine($"expanding node {idNode}");
               currNode.visited = true;
               if(!currNode.isLeaf)
                  fillNode(currNode, idx);
            }

            // Get all offsprings of the popped vertex s, if not visited, then push it to the stack.
            foreach (int v in decTree[idNode].lstSons)
               if (!decTree[v].visited)
                  stack.Push(v);
         }
      }

      // computes the indices that sort each dimension
      private int[,] getSortIdx()
      {  int i,j;
         int[,] idxSort = new int[n,numcol];
         double[] col = new double[n];
         int[]    ind = new int[n];

         for (j=0;j<numcol;j++)
         {  for(i=0;i<n;i++)
            {  col[i] = X[i,j];
               ind[i] = i;
            }
            Array.Sort<int>(ind, (a, b) => col[a].CompareTo(col[b]));
            for(i=0;i<n; i++)
               idxSort[i,j] = ind[i];
         }

         return idxSort;
      }
   }
}
