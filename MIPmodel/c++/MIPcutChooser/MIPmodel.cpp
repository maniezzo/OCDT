#include "MIPmodel.h"
#include "json.h"
#include <ilcplex\cplex.h>
#include <string.h>
#include <stdlib.h>

void MIPmodel::run_MIP()
{
   cout << "run CPLEX, see C# for full code." << endl;
   string dataFileName = readConfig();
   readData(dataFileName);
   cplexModel();
}

void MIPmodel::cplexModel()
{
   int      solstat;
   double   objval;
   double   incobjval;
   double   meanobjval;
   double* x = NULL;
   double* incx = NULL;
   int      numsol;
   int      numsolreplaced;
   int      numdiff;

   CPXENVptr     env = NULL;
   CPXLPptr      lp = NULL;
   int           status;
   int           i, j;
   int           cur_numcols;

   env = CPXopenCPLEX(&status);

   if (env == NULL) 
   {  char  errmsg[CPXMESSAGEBUFSIZE];
      CPXgeterrorstring(env, status, errmsg);
      cout << "Could not open CPLEX environment " << errmsg << endl;
      goto TERMINATE;
   }
   else
      cout <<"CPLEX running" << endl;

   // Turn on output to the screen 
   status = CPXsetintparam(env, CPXPARAM_ScreenOutput, CPX_ON);
   if (status) 
   {  cout << "Failure to turn on screen indicator, error " << status << endl;
      goto TERMINATE;
   }

   // Fill in the data for the problem.
   status = setproblemdata(&probname, &numcols, &numrows, &objsen, &obj,
      &rhs, &sense, &matbeg, &matcnt, &matind,
      &matval, &lb, &ub, &ctype, &qmatbeg, &qmatcnt,
      &qmatind, &qmatval);

   if (status)
   {  fprintf(stderr, "Failed to build problem data arrays.\n");
      goto TERMINATE;
   }

   // Create the problem, using the filename as the problem name
   lp = CPXcreateprob(env, &status, "OCTP");
   if (lp == NULL) 
   {  fprintf(stderr, "Failed to create LP.\n");
      goto TERMINATE;
   }

   CPXchgobjsen(env, lp, CPX_MIN);  // Problem is minimization 

   // Create the new columns.
   ij = 0;
   for (i = 0; i < m; i++)
      for (j = 0; j < n; j++)
      {
         obj[ij] = GAP->c[i][j];
         lb[ij] = 0;
         ub[ij] = 1.0;
         ctype[ij] = 'B'; // 'B', 'I','C' to indicate binary, general integer, continuous 
         lptype[ij] = 'C'; // 'B', 'I','C' to indicate binary, general integer, continuous 
         colname[ij] = (char*)malloc(sizeof(char) * (11));   // why not 11?
         sprintf(colname[ij], "%s%d", "x", ij);
         ij++;
      }

   // Write a copy of the problem to a file, if instance sufficiently small
   if (n * m < 100)
      status = CPXwriteprob(env, lp, "qgap.lp", NULL);
   if (status)
   {  cout << "Failed to write LP to disk" << endl;
      goto TERMINATE;
   }

   // max cplex time: 60 seconds
   //status = CPXsetintparam(env, CPXPARAM_TimeLimit, 60);   // max cpu time, can't make this work
   status = CPXsetintparam(env, CPXPARAM_MIP_Limits_Nodes, conf->maxnodes);   // max num of nodes
   //status = CPXsetintparam(env,    CPXPARAM_DetTimeLimit, 20000);  // max cpu absolute time (ticks), can't make this work
   if (status)
   {  cout << "Failure to reset cpu max time, error " << status << endl;
      goto TERMINATE;
   }

   status = CPXmipopt(env, lp);
   if (status)
   {  cout << "Failed to optimize QP." << endl;
      goto TERMINATE;
   }

   cur_numrows = CPXgetnumrows(env, lp);
   cur_numcols = CPXgetnumcols(env, lp);

   int nn = CPXgetsolnpoolnumsolns(env, lp);
   if (nn == 0)
   {  cout << "Failed to find feasible solutions." << endl;
      goto TERMINATE;
   }
   double mincost = DBL_MAX;
   int minind = -1;
   for (i = 0; i < nn; i++)
   {  status = CPXgetsolnpoolobjval(env, lp, i, &objval);
      cout << "Solution " << i << " cost " << std::fixed << objval << endl;
      if (objval < mincost)
      {  minind = i;
         mincost = objval;
      }
   }

   x = (double*)malloc(cur_numcols * sizeof(double));
   slack = (double*)malloc(cur_numrows * sizeof(double));
   //dj = (double *)malloc(cur_numcols * sizeof(double));
   //pi = (double *)malloc(cur_numrows * sizeof(double));
   status = CPXgetsolnpoolx(env, lp, minind, x, 0, CPXgetnumcols(env, lp) - 1);

   status = CPXsolution(env, lp, &solstat, &objval, x, NULL, slack, NULL);
   if (status)
   {  cout << "Failed to obtain solution values" << endl;
      goto TERMINATE;
   }

   status = checkfeas(x, objval);
   if (status)
   {  cout << "Solution infeasible !!! status = " << status << endl;
      goto TERMINATE;
   }
   else cout << "Solution checked, status " << status << " cost " << std::fixed << objval << endl;

   // Write the output to the screen.
   cout << "\nSolution status = " << solstat << endl;
   cout << "Solution value  = " << std::fixed << objval << endl;
   cout << "Solution:" << endl;
   for (i = 0; i < cur_numcols; i++)
      cout << x[i] << " ";
   cout << endl;

TERMINATE:

   // Free up the solution
   free_and_null((char**)&incx);
   free_and_null((char**)&x);

   // Free up the problem as allocated by CPXcreateprob, if necessary
   if (lp != NULL) 
   {  status = CPXfreeprob(env, &lp);
      if (status) fprintf(stderr, "CPXfreeprob failed, error code %d.\n", status); 
   }

   // Free up the CPLEX environment, if necessary
   if (env != NULL) 
   {  status = CPXcloseCPLEX(&env);
      if (status) 
      {  char  errmsg[CPXMESSAGEBUFSIZE];
         fprintf(stderr, "Could not close CPLEX environment.\n");
         CPXgeterrorstring(env, status, errmsg);
         fprintf(stderr, "%s", errmsg);
      }
   }

   return;
}


int MIPmodel::setproblemdata(char** probname_p, int* numcols_p, int* numrows_p,
   int* objsen_p, double** obj_p, double** rhs_p, char** sense_p, int** matbeg_p, int** matcnt_p,
   int** matind_p, double** matval_p, double** lb_p, double** ub_p, char** ctype_p, int** qmatbeg_p, int** qmatcnt_p,
   int** qmatind_p, double** qmatval_p)
{
   int i, j, h, k, ij, hk, idRow, idCol;

   char* zprobname = NULL;
   double* zobj = NULL;
   double* zrhs = NULL;
   char* zsense = NULL;
   int* zmatbeg = NULL;
   int* zmatcnt = NULL;
   int* zmatind = NULL;
   double* zmatval = NULL;
   double* zlb = NULL;
   double* zzub = NULL;
   char* zctype = NULL;
   int* zqmatbeg = NULL;
   int* zqmatcnt = NULL;
   int* zqmatind = NULL;
   double* zqmatval = NULL;
   int      status = 0;

   int numCols = n * m;
   int numRows = n + m;
   int numNZ = n * m + n * m;  // nonzeros in the linear coefficient matrix
   int numQNZ = n * n * m * m; // nonzeros in the quadratic coefficient matrix

   zprobname = (char*)malloc(16 * sizeof(char));
   zobj = (double*)malloc(numCols * sizeof(double));
   zrhs = (double*)malloc(numRows * sizeof(double));
   zsense = (char*)malloc(numRows * sizeof(char));
   zmatbeg = (int*)malloc(numCols * sizeof(int));
   zmatcnt = (int*)malloc(numCols * sizeof(int));
   zmatind = (int*)malloc(numNZ * sizeof(int));
   zmatval = (double*)malloc(numNZ * sizeof(double));
   zlb = (double*)malloc(numCols * sizeof(double));
   zzub = (double*)malloc(numCols * sizeof(double));
   zctype = (char*)malloc(numCols * sizeof(char));
   zqmatbeg = (int*)malloc(numCols * sizeof(int));
   zqmatcnt = (int*)malloc(numCols * sizeof(int));
   zqmatind = (int*)malloc(numQNZ * sizeof(int));
   zqmatval = (double*)malloc(numQNZ * sizeof(double));

   if (zprobname == NULL || zobj == NULL ||
      zrhs == NULL || zsense == NULL ||
      zmatbeg == NULL || zmatcnt == NULL ||
      zmatind == NULL || zmatval == NULL ||
      zlb == NULL || zzub == NULL ||
      zqmatbeg == NULL || zqmatcnt == NULL ||
      zqmatind == NULL || zqmatval == NULL) {
      status = 1;
      goto TERMINATE;
   }

   zprobname = (char*)name.c_str();

   // -------------------------- linear objective costs
   ij = 0;
   for (i = 0; i < m; i++)
      for (j = 0; j < n; j++)
      {
         zobj[ij] = cl[i][j];
         zlb[ij] = 0;
         zzub[ij] = 1;
         zctype[ij] = 'I';
         ij++;
      }

   // -------------------------- quadratic cost matrix
   ij = 0;
   numNZ = 0;
   for (i = 0; i < m; i++)
      for (j = 0; j < n; j++)
      {
         hk = 0;
         zqmatbeg[ij] = numNZ;
         for (h = 0; h < m; h++)
            for (k = 0; k < n; k++)
            {
               zqmatind[numNZ] = hk;
               if (cqf != NULL)
                  zqmatval[numNZ] = 2 * cqd[i][h] * cqf[j][k]; // d_ih f_jk input format MIND THE 2*
               else
                  zqmatval[numNZ] = 2 * cqd[ij][hk];           // c_ijhk input format    MIND THE 2*
               numNZ++;
               hk++;
            }
         zqmatcnt[ij] = numNZ - zqmatbeg[ij];
         ij++;
      }


   // -------------------------- find eigenvalues
   //double evalue = eigenValues(zqmatval,i*j);
   double evalue = eigenValues(zqmatval, m * n);
   cout << "Eigenvalue: " << evalue << endl;

   // -------------------------- constraints section
   idCol = 0;
   numNZ = 0;
   for (i = 0; i < m; i++)
      for (j = 0; j < n; j++)
      {
         zmatbeg[idCol] = numNZ;

         zmatind[numNZ] = j;           // Assignment constraint
         zmatval[numNZ] = 1.0;
         numNZ++;

         zmatind[numNZ] = n + i;       // Capacity constraint
         zmatval[numNZ] = req[i][j];
         numNZ++;

         zmatcnt[idCol] = numNZ - zmatbeg[idCol];
         idCol++;
      }

   // -------------------------- rhs
   for (j = 0; j < n; j++)
   {
      zsense[j] = 'E';
      zrhs[j] = 1.0;
   }

   for (i = 0; i < m; i++)
   {
      zsense[n + i] = 'L';
      zrhs[n + i] = cap[i];
   }

TERMINATE:
   if (status)
   {
      free_and_null((char**)&zprobname);
      free_and_null((char**)&zobj);
      free_and_null((char**)&zrhs);
      free_and_null((char**)&zsense);
      free_and_null((char**)&zmatbeg);
      free_and_null((char**)&zmatcnt);
      free_and_null((char**)&zmatind);
      free_and_null((char**)&zmatval);
      free_and_null((char**)&zlb);
      free_and_null((char**)&zzub);
      free_and_null((char**)&zctype);
      free_and_null((char**)&zqmatbeg);
      free_and_null((char**)&zqmatcnt);
      free_and_null((char**)&zqmatind);
      free_and_null((char**)&zqmatval);
   }
   else
   {
      *numcols_p = numCols;
      *numrows_p = numRows;
      *objsen_p = CPX_MIN;

      *probname_p = zprobname;
      *obj_p = zobj;
      *rhs_p = zrhs;
      *sense_p = zsense;
      *matbeg_p = zmatbeg;
      *matcnt_p = zmatcnt;
      *matind_p = zmatind;
      *matval_p = zmatval;
      *lb_p = zlb;
      *ub_p = zzub;
      *ctype_p = zctype;
      *qmatbeg_p = zqmatbeg;
      *qmatcnt_p = zqmatcnt;
      *qmatind_p = zqmatind;
      *qmatval_p = zqmatval;
   }
   return (status);

}  // END setproblemdata


string MIPmodel::readConfig()
{  string line, datapath, dataFileName;
   vector<string> elem;

   ifstream fconf("config.json");
   stringstream buffer;
   buffer << fconf.rdbuf();
   line = buffer.str();
   json::Value JSV = json::Deserialize(line);
   datapath = JSV["datapath"];
   dataFileName = JSV["datafile"];
   cout << dataFileName << endl;
   return datapath + dataFileName;
}

// data points
void MIPmodel::readData(string dataFileName)
{
   int i, j, cont, id;
   double d;
   string line;
   vector<string> elem;
   vector<float> val;

   // leggo i punti
   ifstream f;
   string dataSetFile = dataFileName + ".csv";
   cout << "Opening datafile " << dataSetFile << endl;
   f.open(dataSetFile);
   if (f.is_open())
   {
      getline(f, line);  // headers
      elem = split(line, ',');
      ndim = elem.size() - 2;
      nclasses = 0;

      while (getline(f, line))
      {  //read data from file object and put it into string.
         cont = 0;
         val.clear();
         elem = split(line, ',');
         id = stoi(elem[0]);
         //if (id > 40 && !(id > 100 && id < 141)) goto l0;
         //cout << "Read node " << id << endl;
         for (i = 1; i < 1 + ndim; i++)
            if (dataFileName != "iris_setosa") // || (i==2 || i==3))  // FILTERING DATA for iris_setosa
            {  d = stof(elem[i]);
               //d = round(100.0 * d) / 100.0;     // rounded to 2nd decimal
               val.push_back(d);
            }
         X.push_back(val);
         j = stoi(elem[ndim + 1]);
         Y.push_back(j);
         if (j > (nclasses - 1)) nclasses = j + 1;
      l0:      cont++;
      }
      f.close();
      ndim = X[0].size(); // in case of partial dataset
      n = Y.size();  // number of input records
   }
   else cout << "Cannot open dataset input file\n";
}

// da cplex
void MIPmodel::free_and_null(char** ptr)
{  if (*ptr != NULL) {
      free(*ptr);
      *ptr = NULL;
   }
} /* END free_and_null */


// split di una stringa in un array di elementi delimitati da separatori
vector<string> MIPmodel::split(string str, char sep)
{  vector<string> tokens;
   size_t start;
   size_t end = 0;
   while ((start = str.find_first_not_of(sep, end)) != std::string::npos) {
      end = str.find(sep, start);
      tokens.push_back(str.substr(start, end - start));
   }
   return tokens;
}
