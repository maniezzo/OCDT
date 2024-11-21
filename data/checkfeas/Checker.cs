using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Data.Analysis;

namespace Checkfeas
{
   public class cut
   {  public int dim { get; set; } 
      public float pos { get; set; } 
   }
   internal class Checker
   {
      public List<cut> cuts = new List<cut>();

      public void readCuts()
      {  int i;

         //StreamReader fcuts = new StreamReader("../../../../points_cuts.json");
         StreamReader fcuts = new StreamReader("../../../../points_cart_cuts.json");
         string jCuts = fcuts.ReadToEnd();
         fcuts.Close();
         JsonNode? node = JsonNode.Parse(jCuts);
         if(node is not null)
         {  var dim = node["dim"]?.AsArray();
            if(dim!=null)
               foreach(var d in dim)
               {  cut s = new cut(); 
                  s.dim = d.GetValue<int>();
                  cuts.Add(s);
               }
            var pos = node["pos"]?.AsArray();
            i = 0;
            if(pos!=null)
               foreach(var p in pos)
               {  cuts[i].pos = p.GetValue<float>();
                  i++;
               }
            foreach(cut s in cuts)
               Console.WriteLine($"dim: {s.dim} pos: {s.pos}");
         }
      }

      public void checkBoundaries()
      {  int i,j;

         // Load the CSV and apply the specified column types
         var df = DataFrame.LoadCsv("../../../../points.csv");
         var val = df.Columns[1][30];  // first index column, then row !!!!!

         // dataframe of floats
         DataFrame dfFloats = new DataFrame();

         foreach(var column in df.Columns)
         {
            try
            {  // Convert the column to float
               var floatData = column.Cast<float>();
               // Create a new DataFrameColumn with float values
               var floatColumn = new PrimitiveDataFrameColumn<float>(column.Name,floatData);
               dfFloats.Columns.Add(floatColumn);
            }
            catch(InvalidCastException)
            {  Console.WriteLine($"Column '{column.Name}' contains non-convertible data. Skipping.");
            }
         }
         checkSeparation(dfFloats);
      }

      private void checkSeparation(DataFrame df)
      {  int d,i1,i2;
         float pos,val1,val2;
         bool isSeparated=false;

         for(i1 = 0;i1<df.Rows.Count-1;i1++)
         {  for(i2 = i1+1;i2<df.Rows.Count;i2++)
            {  isSeparated=false;
               foreach(cut c in cuts)
               {  val1 = (float)df.Columns[c.dim][i1];
                  val2 = (float)df.Columns[c.dim][i2];
                  if(val1<c.pos && val2>c.pos ||val1>c.pos && val2<c.pos)
                  {  isSeparated = true;
                     break;  // exit foreach
                  }
               }
               if(isSeparated) break; // exit for i2
            }
            if(i2<df.Rows.Count && !isSeparated)
               Console.WriteLine($"OHI! unseparated {i1}-{i2}");
         }
      }
   }
}
