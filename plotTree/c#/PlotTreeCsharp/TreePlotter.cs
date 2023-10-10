using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using System.Data;

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
      public int[] nPointClass;   // number of node points of each class (sum is npoints)
      public bool[] isUsedDim;    // dimensions already used in the path to the node
      public List<int> lstPoints; // list of points clustered in the node (length = npoints)
      public List<int> lstCuts;   // id of the cuts associated with the node (see arrays cutdim and cutval)
      public List<int> lstSons;   // id of each offspring, in array decTree
   }

   internal class TreePlotter
   {
      private int[]     cutdim;
      private double[]  cutval;
      private string[]  dataColumns;
      private double[,] X;
      private int[]     Y;
      private int numcol,ndim; // num dimensions (attributes, columns of X)
      private int n, nclasses; // num of records, num of classes
      private List<Node> decTree;

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

      // initializes fields of a new node
      private void fillNode(Node currNode, int[,] idx)
      {  int i,j,d,pt;

         for (i = 0; i < currNode.npoints; i++)
         {  pt = currNode.lstPoints[i];
            currNode.nPointClass[Y[pt]]++;
         }
         for (d=0;d<ndim;d++)
         {
            if (currNode.isUsedDim[d]) continue;
            int[] nptclass = new int[nclasses];
         }

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
         fillNode(currNode,idx);
         decTree.Add(currNode);

         stack.Push(idNode);

         while (stack.Count > 0)
         {
            // Pop a vertex from stack and print it
            idNode = stack.Peek();
            stack.Pop();
            currNode = decTree[idNode];

            // we print the popped item only if it is not visited.
            if (!currNode.visited)
            {
               Console.Write(idNode + " ");
               currNode.visited = true;
            }

            // Get all offsprings of the popped vertex s, if not visited, then push it to the stack.
            foreach (int v in decTree[idNode].lstCuts)
            {
               if (!decTree[v].visited)
                  stack.Push(v);
            }
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
