using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using System.Data;
using Newtonsoft.Json.Linq;
using System.Linq.Expressions;

namespace PlotTreeCsharp
{
   internal class TreePlotter
   {
      private int[]    cutdim;
      private double[] cutval;
      private string[]  dataColumns;
      private double[,] X;
      private int[]     Y;
      private int numcol;

      public TreePlotter() { }
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
            numcol = dataColumns.Length - 2; // data column, excluding id (first) and class (last)
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

      private void makeTree()
      {  int i,j;
         List<int>[] dimCuts = new List<int>[numcol]; // which cuts for ech dim
         for(i=0;i<numcol;i++)
            dimCuts[i] = new List<int>();
         for(i=0;i<cutdim.Length;i++)
            dimCuts[cutdim[i]].Add(i);
      }
   }
}
