using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Threading;
using System.Text.Json.Nodes;
using Google.OrTools;
using Google.OrTools.LinearSolver;
using static Google.OrTools.LinearSolver.Solver; // I guess this and the former one could be united
using System.Diagnostics;
using System.Runtime.InteropServices;
//using Google.OrTools.ConstraintSolver;

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
      TimeSpan startCpuUsage, endCpuUsage;
      double cpuUsedSec;

      public MIPmodel()
      {  coord = null;
      }
      public void run_MIP()
      {
         Console.Write("C# MIP model ");

         StreamReader fconf = new StreamReader("config.json");
         string jconf = fconf.ReadToEnd();
         fconf.Close();
         JsonNode jobj = JsonSerializer.Deserialize<JsonNode>(jconf)!;

         string dataset  = jobj["datafile"].GetValue<string>();
         string datapath = jobj["datapath"].GetValue<string>();
         string fpath = $"{datapath}{dataset}.csv";
         Console.WriteLine("dataset "+dataset);
         read_data(fpath);

         var startTime = DateTime.UtcNow;
         startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;   // Init time

         string LPsolver = jobj["LPsolver"].GetValue<string>();
         string IPsolver = jobj["IPsolver"].GetValue<string>();
         lpModel(LPsolver, IPsolver, fpath, dataset);   // linear model

         var endTime = DateTime.UtcNow;
         endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
         double cpuUsedSec = (endCpuUsage - startCpuUsage).TotalSeconds;
         Console.WriteLine($"End - CPU: {cpuUsedSec}");
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
      private void findCuts(List<Tuple<int, double>> lstCuts, string dataset)
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
l0:            continue;
            }
         }

         // write out cut file
         using (StreamWriter fout = new StreamWriter($"{dataset}_allcuts.json"))
         {
            fout.Write("{\n\"dim\" : [");
            for (i = 0; i < lstCuts.Count - 1; i++)
               fout.Write($"{lstCuts[i].Item1},");
            fout.Write($"{lstCuts[lstCuts.Count - 1].Item1}],\n\"pos\" : [");
            for (i = 0; i < lstCuts.Count - 1; i++)
               fout.Write($"{lstCuts[i].Item2},");
            fout.WriteLine($"{lstCuts[lstCuts.Count - 1].Item2}]\n}}");
         }
      }

      // removes dominated constraints
      private bool[] checkDominance(int numConstr, List<List<int>> lstTableauRows)
      {  int i,j,k;

         // remove dominated
         bool[] fOut = new bool[numConstr];
         int[] tsmall, tbig;
         for (i = 0; i < numConstr - 1; i++) fOut[i] = false;

         bool isSubset;
         for (i = 0; i < numConstr - 1; i++)
         {  if (fOut[i]) continue;

            for (j = i + 1; j < numConstr; j++)
            {  if (fOut[j]) continue;

               if (lstTableauRows[i].Count < lstTableauRows[j].Count) // i may dominate j
               {  isSubset = true;
                  tsmall = lstTableauRows[i].ToArray();
                  tbig = lstTableauRows[j].ToArray();
                  foreach (int element in tsmall)
                     if (!tbig.Contains(element))
                     {  isSubset = false; // tsmall is not a subset of tbig
                        break;
                     }
                  if (isSubset) fOut[j] = true; // constraint j is dominated
               }
               else
               {  isSubset = true;
                  tsmall = lstTableauRows[j].ToArray();
                  tbig = lstTableauRows[i].ToArray();
                  foreach (int element in tsmall)
                     if (!tbig.Contains(element))
                     {  isSubset = false; // tsmall is not a subset of tbig
                        break;
                     }
                  if (isSubset)
                  {  fOut[i] = true; // constraint i is dominated
                     break; // check next i
                  }
               }
            }
         }
         // resulting non dominated constraints
         if(numConstr < 500)
         {  Console.WriteLine("Non dominated constraints");
            for (i = 0; i < numConstr; i++)
            {  if (fOut[i]) continue;
               Console.Write($"{i} - ");
               for (j = 0; j < lstTableauRows[i].Count; j++)
                  Console.Write($" {lstTableauRows[i][j]}");
               Console.WriteLine();
            }
         }
         return fOut;
      }

      // linear relaxation
      private void lpModel(String LPsolver, string IPsolver, string fpath, string dataset)
      {  int i, j, k, d, m=0, numConstrOld;
         List<int> lstCols; // il cut che verrà costruito e testato
         List<int> lstHash = new List<int>(); // un hashcode per ogni riga del tableau
         List<List<int>> lstTableauRows = new List<List<int>>(); // cut accettati, riche del tableau

         List<Tuple<int,double>> lstCuts = new List<Tuple<int, double>>();
         findCuts(lstCuts,dataset);  // costruisce lista lstCuts
         numVar = lstCuts.Count;

         endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
         cpuUsedSec = (endCpuUsage - startCpuUsage).TotalMilliseconds/1000.0;
         Console.WriteLine($"After cut generation: CPU: {cpuUsedSec}");

         // da qui modello LP
         Console.WriteLine($"---- Linear programming model with {LPsolver} ----");
         Google.OrTools.LinearSolver.Solver solver = Google.OrTools.LinearSolver.Solver.CreateSolver(LPsolver);
         if (solver == null)
         {  Console.WriteLine("Could not create linear solver " + LPsolver);
            return;
         }
         Solver.ResultStatus resultStatus;

         // continuous 0/1 variables, if cut is used
         Variable[] x = new Variable[numVar];
         for (i = 0; i < numVar; i++)
         {  x[i] = solver.MakeNumVar(0.0, 1.0, $"x{i}");
            x[i].SetInteger(false);
         }

         // objective function
         Objective objective = solver.Objective();
         for (i = 0; i < numVar; i++)
            objective.SetCoefficient(x[i], 1);

         // optimization sense
         objective.SetMinimization();

         // constraint section, range of feasible values
         numConstr = numConstrOld = 0;
         int n2 = npoints*(npoints+1)/2;
         int p1,p2,hash;
         int n = npoints;
         // for all pairs of points (n2)
         for (k = 0; k < n2; k++)
         {  p1 = (int) (0.5*(2*n+1-Math.Sqrt(4*n*n + 4*n - 8*k + 1)));  // my own! from linear to upper triangular indices
            p2 = (int) (k - (p1*n - 0.5*p1*p1 - 0.5*p1));               // my own!
            // Console.WriteLine($"Coppia {p1} - {p2} test {k}");
            if(p1!=p2 && classe[p1] != classe[p2])
            {  lstCols = new List<int>();
               // checks all cuts to see which ones separate p1 from p2
               for (i=0;i<lstCuts.Count;i++)
                  if (separates(p1,p2,lstCuts,i))
                     lstCols.Add(i);

               // here I just chack for repeated subsets
               hash = getRowhash(lstCols);
               for(i=0;i<numConstr;i++)
               {  if(hash == lstHash[i] && lstCols.Count() == lstTableauRows[i].Count())
                  {  for(j=0;j<lstCols.Count;j++)
                        if (lstCols[j] != lstTableauRows[i][j])
                           goto l1; // try next cut, check if it dominates
                     Console.WriteLine($">>> Cut {i} duplicato");
                     goto l0;  // cut dominated, do not add
                  }
l1:               continue;
               }
               lstHash.Add(hash);
               lstTableauRows.Add(lstCols);
               numConstr++;
l0:            continue;
            }

            if(numConstr > numConstrOld && numConstr%1000==0)
            {  Console.WriteLine("Number of variables  = " + numVar + " Number of constraints = " + numConstr);
               numConstrOld = numConstr;
            }
         }

         // insertion of constraints into the model
         bool[] fOut; // flags, true if constraint to be removed
         fOut = checkDominance(numConstr,lstTableauRows);
         for (i = 0; i < numConstr; i++)
            if (!fOut[i]) m++;   // counting eventual constraints
         writeProb(dataset, numVar, numConstr, m, lstTableauRows, fOut); // problema formato mio

         Google.OrTools.LinearSolver.Constraint[] cuts = new Google.OrTools.LinearSolver.Constraint[m]; // m constraints in the model
         k = 0;
         for (i = 0; i < numConstr; i++)
         {  if (fOut[i]) continue;   // no constraint for dominated sets
            cuts[k] = solver.MakeConstraint(1, double.PositiveInfinity, $"c{k}");
            for (j = 0; j < lstTableauRows[i].Count; j++)
               cuts[k].SetCoefficient(x[lstTableauRows[i][j]], 1); // consraint coeffiicients
            k++;
         }
         numConstr = m;

         Console.WriteLine("Final number of variables  = " + solver.NumVariables());
         Console.WriteLine("Final number of constraints = " + solver.NumConstraints());

         if(solver.NumVariables() < 1000 && solver.NumConstraints() < 1000)
         {  string lp_text = solver.ExportModelAsLpFormat(false);
            using (StreamWriter out_f = new StreamWriter($"{dataset}.lp"))
               out_f.Write(lp_text);
         }

         // ------------------------------------ SOLVE
         resultStatus = solver.Solve();

         // Check that the problem has an optimal solution.
         if (resultStatus != Google.OrTools.LinearSolver.Solver.ResultStatus.OPTIMAL)
         {  Console.WriteLine("The problem does not have an optimal solution!");
            return;
         }

         Console.WriteLine("Problem solved in " + solver.WallTime() + " milliseconds");
         endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
         cpuUsedSec = (endCpuUsage - startCpuUsage).TotalSeconds;
         Console.WriteLine($"After LP: CPU: {cpuUsedSec}");

         // The objective value of the solution.
         Console.WriteLine("Optimal objective value = " + solver.Objective().Value());

         // The value of each variable in the solution.
         for (i = 0; i < numVar; ++i)
            if(x[i].SolutionValue()>0.01)
               Console.WriteLine($"x{i} = " + x[i].SolutionValue());

         Console.WriteLine("Advanced usage:");
         double[] activities = solver.ComputeConstraintActivities();

         Console.WriteLine("Problem solved in " + solver.Iterations() + " iterations");
         endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
         cpuUsedSec = (endCpuUsage - startCpuUsage).TotalSeconds;
         Console.WriteLine($"After IP: CPU: {cpuUsedSec}");

         // reduced costs
         for (i = 0; i < numVar; i++)
            if(x[i].ReducedCost() > 0.0001)
               Console.WriteLine($"x{i}: reduced cost = " + x[i].ReducedCost());

         // dual variables
         for (j = 0; j < numConstr; j++)
            if(cuts[j].DualValue() > 0.0001)
               Console.WriteLine($"c0: dual value = {cuts[j].DualValue()} activity = {activities[cuts[j].Index()]}");

         // GO INTEGER
         IPmodel(IPsolver, lstTableauRows, lstCuts, fpath);                   
      }

      // integer model
      private void IPmodel(String IPsolver, List<List<int>> lstTableauRows, List<Tuple<int, double>> lstCuts, string fpath)
      {  int i,j;

         // -------------------------------------------- integer solution
         Google.OrTools.LinearSolver.Solver Isolver = Google.OrTools.LinearSolver.Solver.CreateSolver(IPsolver);

         // integer 0/1 variables, if cut is used
         Variable[] xi = Isolver.MakeBoolVarArray(numVar, "xi");
         for (i = 0; i < numVar; i++) xi[0].SetInteger(true);

         Objective objective = Isolver.Objective();
         objective.SetMinimization();
         for (i = 0; i < numVar; i++) objective.SetCoefficient(xi[i], 1);

         // Constraint section
         int numConstr = lstTableauRows.Count;
         Google.OrTools.LinearSolver.Constraint[] cuts = new Google.OrTools.LinearSolver.Constraint[numConstr]; 
         for (i=0;i< numConstr; i++)
         {
            cuts[i] = Isolver.MakeConstraint(1, double.PositiveInfinity, $"geq{i}");
            for (j = 0; j < lstTableauRows[i].Count; j++)
               cuts[i].SetCoefficient(xi[lstTableauRows[i][j]], 1);
         }

         // >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> SOLVE
         ResultStatus resultStatus = Isolver.Solve();

         // Check that the problem has an optimal solution.
         if (resultStatus != ResultStatus.OPTIMAL)
         {  Console.WriteLine("The integer problem does not have an optimal solution!");
            return;
         }

         Console.WriteLine("Integer problem solved in " + Isolver.WallTime() + " milliseconds");
         Console.WriteLine("Problem solved in " + Isolver.Iterations() + " iterations");
         Console.WriteLine("Problem solved in " + Isolver.Nodes() + " branch-and-bound nodes");

         // The objective value of the solution.
         Console.WriteLine("Optimal integer objective value = " + Isolver.Objective().Value());

         List<int> lstDim = new List<int>();
         List<double> lstPos = new List<double>();
         for (i = 0; i < numVar; i++)
         {  if (xi[i].SolutionValue() > 0.0001)
            {  Console.WriteLine($"xi{i} = {xi[i].SolutionValue()}");
               Console.WriteLine($"dim {lstCuts[i].Item1} pos {lstCuts[i].Item2}");
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

      // calcola un hashcode di una riga del tableau
      private int getRowhash(List<int> cut)
      {  int i,hash=0;
         for(i=0;i<cut.Count();i++)
            hash = hash + 13 + (17 * cut[i] * cut[i]);
         return hash;
      }

      // scrive il problema su file, formato mio
      private void writeProb(string dataset, int nvar, int ncons, int m, List<List<int>> lstTableauRows, bool[] fOut)
      {  int i,j;

         using (StreamWriter fout = new StreamWriter($"{dataset}.prob"))
         {  fout.WriteLine(nvar);
            fout.WriteLine(m);
            for(i=0;i<ncons;i++)
            {  if (fOut[i]) continue;
               for (j = 0; j < lstTableauRows[i].Count;j++)
                  fout.Write($"{lstTableauRows[i][j]} ");
               fout.WriteLine();
            }
         }
      }
   }
}
