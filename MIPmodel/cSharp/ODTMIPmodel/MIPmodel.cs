using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using Google.OrTools;
using Google.OrTools.LinearSolver;
using System.Text.Json.Nodes;
using Google.OrTools.Sat;

namespace ODTMIPmodel
{
   internal class MIPmodel
   {
      int numVar, numConstr;
      double[] c;    // model costs
      int[,] constr; // constraint matrix coefficients
      double[] rhs;  // constraint right hand sides

      int ndim,npoints;
      double[][] coord;
      int[] classe;

      public MIPmodel()
      {
         coord = null;
      }
      public void run_MIP()
      {
         Console.WriteLine("MIP model");

         StreamReader fconf = new StreamReader("config.json");
         string jconf = fconf.ReadToEnd();
         fconf.Close();
         JsonNode jobj = JsonSerializer.Deserialize<JsonNode>(jconf)!;

         string dataset  = jobj["datafile"].GetValue<string>();
         string datapath = jobj["datapath"].GetValue<string>();
         string fpath = $"{datapath}{dataset}.csv";
         read_data(fpath);

         lpModel("GLOP",fpath, dataset);
      }

      private void read_data(string fpath)
      {  int i,j;
         string line;
         string[] elem;
         double[] coord;
         List<double[]> lstPoints = new List<double[]>();
         List<int> lstClass = new List<int>();

         StreamReader datafile=null;
         try
         {
            datafile = new StreamReader(fpath);
            line = datafile.ReadLine();  // headers
            elem = line.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            ndim = elem.Length -2;
            while (datafile.Peek() != -1) 
            { 
               line = datafile.ReadLine();   
               elem = line.Split(new char[] {','},StringSplitOptions.RemoveEmptyEntries);
               coord = new double[ndim];
               for (i=0;i<ndim;i++) 
                  coord[i] = double.Parse(elem[i+1]);
               lstPoints.Add(coord);
               lstClass.Add(Convert.ToInt32(elem[i+1]));

               Console.WriteLine(line);
            }
            this.coord = lstPoints.ToArray();
            classe = lstClass.ToArray();
         }
         catch (Exception ex)
         {   Console.WriteLine(ex.Message); 
         }
         datafile.Close();
         npoints = classe.Length;
      }

      // trova tutti i possibili cut, tagli fra due punti ordinati che devono essere separati
      private void findCuts(List<Tuple<int, double>> lstCuts)
      {  int cont,i,j,d;

         cont = 0;
         int[] idx = new int[npoints];
         double[] coo = new double[npoints];
         for (d = 0; d < ndim; d++)
         {
            for (int ii = 0; ii < npoints; ii++) coo[ii] = coord[ii][d];
            idx = idxBBsort(coo);
            // i e j sono indici, non punti !!
            for (i = 0; i < npoints - 1; i++)
            {  // trova il primo dell'altra classe che non è allineato con i
               j = i + 1;
               while (j < npoints)
               {
                  if (classe[idx[i]] != classe[idx[j]] &&
                      coord[idx[i]][d] != coord[idx[j]][d])
                     break;
                  // se c'è un punto dopo stessa classe passa a quello
                  if (classe[idx[i]] == classe[idx[j]] &&
                      coord[idx[i]][d] != coord[idx[j]][d] && j < npoints - 1 &&
                      coord[idx[j]][d] < coord[idx[j + 1]][d]) // a meno che non sia allineato a uno altra classe
                     goto l0;
                  j++;
               }
               if (j == npoints) continue; // passa al punto successivo
               else // crea il cut
               {
                  double m = (coord[idx[i]][d] + coord[idx[j]][d]) / 2.0;
                  if (m != coord[idx[i]][d])
                  {
                     Tuple<int, double> t = new Tuple<int, double>(d, m);
                     if (!lstCuts.Contains(t))
                     {
                        lstCuts.Add(t);
                        Console.WriteLine($"Cut {cont} {t.Item1}->{t.Item2}");
                        cont++;
                     }
                  }
               }
l0: continue;
            }
         }
      }

      private void lpModel(String solverType, string fpath, string dataset)
      {
         int i, j, k, d, cont, numConstrOld;

         List<Tuple<int,double>> lstCuts = new List<Tuple<int, double>>();
         findCuts(lstCuts);  // costruisce lista lstCuts
         numVar = lstCuts.Count;

         // da qui modello LP
         Console.WriteLine($"---- Linear programming model with {solverType} ----");
         Solver solver = Solver.CreateSolver(solverType);
         if (solver == null)
         {  Console.WriteLine("Could not create linear solver " + solverType);
            return;
         }
         Solver.ResultStatus resultStatus;

         // continuous 0/1 variables, if cut is used
         Variable[] x = new Variable[numVar];
         for (i = 0; i < numVar; i++)
            x[i] = solver.MakeNumVar(0.0, 1.0, $"x{i}");

         // objective function
         Objective objective = solver.Objective();
         for (i = 0; i < numVar; i++)
            objective.SetCoefficient(x[i], 1);

         // optimization sense
         objective.SetMinimization();

         // constraint section, range of feasible values
         numConstr = numConstrOld = 0;
         int n2 = npoints*(npoints+1)/2;
         Constraint[] cuts = new Constraint[n2]; // one cut for each pair of points
         int p1,p2;
         int n = npoints;
         // for all pairs of points (n2)
         for (k = 0; k < n2; k++)
         {  p1 = (int) (0.5*(2*n+1-Math.Sqrt(4*n*n + 4*n - 8*k + 1)));  // my own! from linear to upper triangular indices
            p2 = (int) (k - (p1*n - 0.5*p1*p1 - 0.5*p1));               // my own!
            // Console.WriteLine($"Coppia {p1} - {p2} test {k}");
            if(p1!=p2 && classe[p1] != classe[p2])
            {  cuts[numConstr] = solver.MakeConstraint(1, double.PositiveInfinity, $"geq{p1}_{p2}");
               // checks all cuts to see which ones separate p1 from p2
               for (i=0;i<lstCuts.Count;i++)
                  if (separates(p1,p2,lstCuts,i))
                     cuts[numConstr].SetCoefficient(x[i], 1);
               numConstr++;
            }
            if(numConstr > numConstrOld && numConstr%10==0)
            {
               Console.WriteLine("Number of variables  = " + solver.NumVariables());
               Console.WriteLine("Number of constraints = " + solver.NumConstraints());
               resultStatus = solver.Solve();
               Console.WriteLine("Optimal objective value = " + solver.Objective().Value());
               numConstrOld = numConstr;
            }
         }

         Console.WriteLine("Final number of variables  = " + solver.NumVariables());
         Console.WriteLine("Final number of constraints = " + solver.NumConstraints());

         string lp_text = solver.ExportModelAsLpFormat(false);
         using (StreamWriter out_f = new StreamWriter($"{dataset}.lp"))
            out_f.Write(lp_text);


         Console.WriteLine($"---- Integer programming model with {solverType} ----");
         Solver Isolver = Solver.CreateSolver(solverType);
         if (Isolver == null)
         {  Console.WriteLine("Could not create integer solver " + solverType);
            return;
         }
         // continuous 0/1 variables, if cut is used
         BoolVar[] xi = new BoolVar[numVar];
         for (i = 0; i < numVar; i++)
            xi[i] = Isolver.MakeBoolVar($"xi{i}");

         xi[0].set

         resultStatus = solver.Solve();

         // Check that the problem has an optimal solution.
         if (resultStatus != Solver.ResultStatus.OPTIMAL)
         {  Console.WriteLine("The problem does not have an optimal solution!");
            return;
         }

         Console.WriteLine("Problem solved in " + solver.WallTime() + " milliseconds");

         // The objective value of the solution.
         Console.WriteLine("Optimal objective value = " + solver.Objective().Value());

         List<int> lstDim = new List<int>();
         List<double> lstPos = new List<double>();
         // The value of each variable in the solution.
         for (i = 0; i < numVar; ++i)
         {  Console.WriteLine($"x{i} = " + x[i].SolutionValue());
            if(x[i].SolutionValue()>0.01)
            {  Console.WriteLine($"dim {lstCuts[i].Item1} pos {lstCuts[i].Item2}");
               lstDim.Add(lstCuts[i].Item1);
               lstPos.Add(lstCuts[i].Item2);
            }
         }
         StreamWriter fout = new StreamWriter(fpath.Replace(".csv", "_cuts.json"));
         fout.WriteLine("{");
         fout.WriteLine($"\"dim\" : [{string.Join(",", lstDim)}],");
         fout.WriteLine($"\"pos\" : [{string.Join(",", lstPos)}]");
         fout.WriteLine("}");
         fout.Close();

         Console.WriteLine("Advanced usage:");
         double[] activities = solver.ComputeConstraintActivities();

         Console.WriteLine("Problem solved in " + solver.Iterations() + " iterations");

         // reduced costs
         for (i = 0; i < numVar; i++)
            Console.WriteLine($"x{i}: reduced cost = " + x[i].ReducedCost());

         // dual variables
         for (j = 0; j < numConstr; j++)
         {  Console.WriteLine($"c0: dual value = {cuts[j].DualValue()} activity = {activities[cuts[j].Index()]}");
         }
      }

      // se il cut idcut separa il punto p1 dal punto p2
      private bool separates(int p1, int p2, List<Tuple<int, double>> lstCuts, int idcut)
      {  int d;
         double cutval;
         bool res = false;
         d      = lstCuts[idcut].Item1;
         cutval = lstCuts[idcut].Item2;
         if ((coord[p1][d]<cutval && coord[p2][d]>cutval) ||
             (coord[p2][d]<cutval && coord[p1][d]>cutval))
            res = true;
         return res;
      }

      // restituisce gli indici ordinati per valori crescenti di a
      private int[] idxBBsort(double[] a)
      {  int i,j,n;
         int[] idx = new int[a.Length];  // indici dell'array a
         for (i = 0;i < a.Length;i++) idx[i] = i;

         n = a.Length;
         for(i=0;i<n;i++)
            for(j=0;j<n-1;j++)
               if (a[idx[j]] > a[idx[j+1]])
                  (idx[j], idx[j+1]) = (idx[j+1], idx[j]);
         return idx;
      }
   }
}
