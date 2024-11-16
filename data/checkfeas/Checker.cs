using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

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

         StreamReader fcuts = new StreamReader("../../../../points_cuts.json");
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
   }
}
