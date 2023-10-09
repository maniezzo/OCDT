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

      public TreePlotter() { }
      public void run_plotter()
      {
         Console.WriteLine("Plotting");
         string dataset = readConfig();
         readData(dataset); // gets the X and Y matrices (data and class)
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
            X = new double[Y1.Count(), dataColumns.Count()-1];
            for(i=0;i<X1.Count();i++)
               for(j=0;j<dataColumns.Count()-1;j++)
                  X[i,j] = X1[i][j];
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
   }
}
