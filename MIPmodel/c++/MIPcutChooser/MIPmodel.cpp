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
   int      objsen;
   double   objval;
   double*  incx  = NULL; 

   int numRows;
   int numCols;
   int numNZ, numNZrow;  // nonzeros in the whole problem and in a row

   vector<double> c{ 13, 12, 8, 10, 10, 4, 4, 20, 5, 4, 4, -16, 0, 0, 0, 16 };
   vector<double> b{ 12,13,10,8,4,4,10,5,20,4,4 };
   vector<vector<int>> a{ { 1,-11, 12},
                           { 0,-11, 13},
                           {-12,13, 3},
                           {-12,14, 2},
                           {12,-13, 6},
                           {-13,14, 5},
                           {-13,15, 4},
                           {13,-14, 8},
                           {-14,16, 7},
                           {10,13,-15},
                           {-15,16, 9}
   };
   numRows = b.size();
   numCols = c.size();
   numNZ   = numRows*numCols;

   double* obj  = (double*)malloc(numCols * sizeof(double));
   double* lb   = (double*)malloc(numCols * sizeof(double));
   double* ub   = (double*)malloc(numCols * sizeof(double));
   int* rmatbeg = (int*)malloc(numRows * sizeof(int));
   int* rmatind = (int*)malloc(numNZ * sizeof(int));
   double* rmatval = (double*)malloc(numNZ * sizeof(double));
   double* rhs     = (double*)malloc(numRows * sizeof(double));
   char* sense     = (char*)malloc(numRows * sizeof(char));
   char* ctype     = (char*)malloc(numCols * sizeof(char));
   char* lptype    = (char*)malloc(numCols * sizeof(char));
   char* probname  = NULL;
   char** colname  = (char**)malloc(numCols * sizeof(char*));
   char** rowname  = (char**)malloc(numRows * sizeof(char*));
   //for(int i = 0 ; i < numRows; ++i) rowname[i] = (char*) malloc(sizeof(char) * sizeOfString);
   double* x = NULL;         // cplex solution vector
   double* pi;
   double* slack;
   double* dj;

   dj      = NULL;
   pi      = NULL;
   slack   = NULL;

   CPXENVptr env = NULL;
   CPXLPptr  lp  = NULL;
   int       status, minind, maxnodes = 1000000;
   int       i,j,nn;
   int       cur_numrows,cur_numcols, idRow;
   double    mincost;
   
   string name = "Prob1";

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
   
   probname = (char*)name.c_str();
   objsen  = CPX_MIN;

   if (status)
   {  fprintf(stderr, "Failed to build problem data arrays.\n");
      goto TERMINATE;
   }

   // Create the problem, using the filename as the problem name
   lp = CPXcreateprob(env, &status, probname);
   if (lp == NULL) 
   {  fprintf(stderr, "Failed to create LP.\n");
      goto TERMINATE;
   }

   CPXchgobjsen(env, lp, CPX_MIN);  // Problem is minimization 

   // Create the columns.
   numCols = c.size();
   for (i = 0; i < numCols; i++)
   {  obj[i] = c[i];
      lb[i] = 0;
      ub[i] = 1;
      ctype[i]  = 'B'; // 'B', 'I','C' to indicate binary, general integer, continuous 
      lptype[i] = 'C'; // 'B', 'I','C' to indicate binary, general integer, continuous 
      colname[i] = (char*)malloc(sizeof(char) * (11));   // why not 11?
      sprintf_s(colname[i], 11, "%s%d", "v", i);
   }
   status = CPXnewcols(env, lp, numCols, obj, lb, ub, NULL, colname);  // <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<

   // Create the constraints
   idRow = 0;
   for (i = 0; i < 11; i++)
   {
      numNZrow = 0;  // number of nonzero element in the row to add
      rmatbeg[0] = 0;
      sense[0] = 'L';
      rhs[0] = b[i];
      for (j = 0; j < a[i].size(); j++)
      {
         rmatind[j] = abs(a[i][j]);
         rmatval[j] = (a[i][j] >= 0 ? 1 : -1);
         numNZrow++;
      }
      rmatbeg[1] = numNZrow;
      rowname[idRow] = (char*)malloc(sizeof(char) * (11));   // why not 11?
      sprintf_s(rowname[0], 11, "%s%d", "c", idRow);
      status = CPXaddrows(env, lp, 0, 1, numNZrow, rhs, sense, rmatbeg, rmatind, rmatval, NULL, rowname);
      if (status) goto TERMINATE;
      idRow++;
   }

   // Write a copy of the problem to a file, if instance sufficiently small
   if (numCols * numRows < 1000)
      status = CPXwriteprob(env, lp, "prob.lp", NULL);
   if (status)
   {  cout << "Failed to write LP to disk" << endl;
      goto TERMINATE;
   }

   // max cplex time: 60 seconds
   //status = CPXsetintparam(env, CPXPARAM_TimeLimit, 60);   // max cpu time, can't make this work
   status = CPXsetintparam(env, CPXPARAM_MIP_Limits_Nodes, maxnodes);   // max num of nodes
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

   nn = CPXgetsolnpoolnumsolns(env, lp);
   if (nn == 0)
   {  cout << "Failed to find feasible solutions." << endl;
      goto TERMINATE;
   }
   mincost = DBL_MAX;
   minind = -1;
   for (i = 0; i < nn; i++)
   {  status = CPXgetsolnpoolobjval(env, lp, i, &objval);
      cout << "Solution " << i << " cost " << std::fixed << objval << endl;
      if (objval < mincost)
      {  minind = i;
         mincost = objval;
      }
   }

   x     = (double*)malloc(cur_numcols * sizeof(double));
   slack = (double*)malloc(cur_numrows * sizeof(double));
   //dj = (double *)malloc(cur_numcols * sizeof(double));
   //pi = (double *)malloc(cur_numrows * sizeof(double));
   status = CPXgetsolnpoolx(env, lp, minind, x, 0, CPXgetnumcols(env, lp) - 1);

   status = CPXsolution(env, lp, &solstat, &objval, x, NULL, slack, NULL);
   if (status)
   {  cout << "Failed to obtain solution values" << endl;
      goto TERMINATE;
   }

   //status = checkfeas(x, objval);
   //if (status)
   //{  cout << "Solution infeasible !!! status = " << status << endl;
   //   goto TERMINATE;
   //}
   //else cout << "Solution checked, status " << status << " cost " << std::fixed << objval << endl;

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
