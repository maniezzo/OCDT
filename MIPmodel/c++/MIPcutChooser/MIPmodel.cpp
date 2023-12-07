#include "MIPmodel.h"
#include "json.h"
#include <string.h>
#include <stdlib.h>


struct loginfo {
   double timestart;
   double dettimestart;
   double lastincumbent;
   int    lastlog;
   int    numcols;
};
typedef struct loginfo LOGINFO, * LOGINFOptr;
static int CPXPUBLIC
logcallback(CPXCENVptr env, void* cbdata, int wherefrom, void* cbhandle);

void MIPmodel::run_MIP()
{
   cout << "run CPLEX, see C# for full code." << endl;
   string dataFileName = readConfig();
   readData(dataFileName);
   cplexModel(dataFileName);
}

void MIPmodel::cplexModel(string dataFileName)
{
   double objval;
   int    solstat, objsen;
   bool   isVerbose = false;
   bool   fReadLpProblem = true;
   bool   fReadFromProbFile = false;
   int numRows=0, numCols=0;
   int numNZ, numNZrow;  // nonzeros in the whole problem and in a row
   vector<vector<int>> lstConstr;

   double* obj  = NULL;
   double* lb   = NULL;
   double* ub   = NULL;
   int* rmatbeg = NULL;                 // one row, 0 begin, 1 end
   int* rmatind = NULL;           // nonzeros are at ost the cells of a row (adding one row at a time)
   double* rmatval = NULL;  // nonzeros are at ost the cells of a row (adding one row at a time)
   double* rhs     = NULL;        // one row at a time
   char* sense     = NULL;            // one row at a time
   char* ctype     = NULL;
   char* lptype    = NULL;
   char* probname  = NULL;
   char** colname  = NULL;
   char** rowname  = NULL;           // one row at a time
   double* x     = NULL;         // cplex solution vector
   double* pi    = NULL;
   double* slack = NULL;
   double* dj    = NULL;

   CPXENVptr env = NULL;
   CPXLPptr  lp  = NULL;
   int       status, minind, maxnodes;
   int       i,j,nn;
   int       cur_numrows,cur_numcols, idRow;
   double    mincost;
   string path;
   
   size_t lastPos = dataFileName.find_last_of("/\\");
   dataFileName = dataFileName.substr(lastPos + 1);
   string name = dataFileName;
   probname = (char*)name.c_str();
   maxnodes = 100000000;

   bool uselogcallback = true;  // variables for the log callout
   LOGINFO myloginfo;

   env = CPXopenCPLEX(&status);
   if (env == NULL) 
   {  char  errmsg[CPXMESSAGEBUFSIZE];
      CPXgeterrorstring(env, status, errmsg);
      cout << "Could not open CPLEX environment " << errmsg << endl;
      goto TERMINATE;
   }
   else
      cout <<"CPLEX running" << endl;

   // Create the problem, using the filename as the problem name
   lp = CPXcreateprob(env, &status, probname);
   if (lp == NULL)
   {  fprintf(stderr, "Failed to create LP.\n");
      goto TERMINATE;
   }

   // Turn on output to the screen 
   status = CPXsetintparam(env, CPXPARAM_ScreenOutput, CPX_ON);
   if (status) 
   {  cout << "Failure to turn on screen indicator, error " << status << endl;
      goto TERMINATE;
   }

   // reads the problem from file
   path = "c:\\git\\ODT\\MIPmodel\\cSharp\\ODTMIPmodel\\bin\\Debug\\net6.0\\";
   //path = ".\\";
   if(fReadLpProblem)
   {
      name = path + dataFileName+".lp";
      status = CPXreadcopyprob(env, lp, name.c_str(), NULL);
      if (status)
      {  cout << "Failed to read lp data." << endl;
         goto TERMINATE;
      }
      numRows = cur_numrows = CPXgetnumrows(env, lp);
      numCols = cur_numcols = CPXgetnumcols(env, lp);
   }
   else if (fReadFromProbFile)
   {  int cont=0;
      ifstream fprob;
      string line;
      vector<string> elem;
      string dataProbFile = path + dataFileName + ".prob";
      cout << "Opening datafile " << dataProbFile << endl;
      fprob.open(dataProbFile);
      if (fprob.is_open())
      {
         getline(fprob, line);
         numCols = stoi(line);
         getline(fprob, line);
         //numRows = stoi(line);
         getline(fprob, line);

         while (getline(fprob, line))
         {  
            elem = split(line, ' ');
            vector<int> ints(elem.size(),-1);
            for(i=0;i<elem.size();i++)
               ints[i] = stoi(elem[i]);
            lstConstr.push_back(ints);
            numRows++;
            if(cont%100==0) cout << "reading constraint "<<cont<<endl;
            cont++;
         }
      }
      fprob.close();
   }
   else
   {
      numCols = n*n/2;
      numRows = 6;
   }

   obj     = (double*)malloc(numCols * sizeof(double));
   lb      = (double*)malloc(numCols * sizeof(double));
   ub      = (double*)malloc(numCols * sizeof(double));
   rmatbeg = (int*)malloc(2 * sizeof(int));              // one row, 0 begin, 1 end
   rmatind = (int*)malloc(numCols * sizeof(int));        // nonzeros are at ost the cells of a row (adding one row at a time)
   rmatval = (double*)malloc(numCols * sizeof(double));  // nonzeros are at ost the cells of a row (adding one row at a time)
   rhs     = (double*)malloc(1 * sizeof(double));        // one row at a time
   sense   = (char*)malloc(1 * sizeof(char));            // one row at a time
   ctype   = (char*)malloc(numCols * sizeof(char));
   lptype  = (char*)malloc(numCols * sizeof(char));
   colname = (char**)malloc(numCols * sizeof(char*));
   rowname = (char**)malloc(1 * sizeof(char*));          // one row at a time

   // construct the problem, do not read it
   if (!fReadLpProblem && fReadFromProbFile)
   {   
      objsen   = CPX_MIN;
      if (status)
      {  fprintf(stderr, "Failed to build problem data arrays.\n");
         goto TERMINATE;
      }
      CPXchgobjsen(env, lp, CPX_MIN);  // Problem is minimization 

      // Create the columns.
      for (i = 0; i < numCols; i++)
      {  obj[i] = 1;
         lb[i] = 0;
         ub[i] = 1;
         ctype[i]  = 'B'; // 'B', 'I','C' to indicate binary, general integer, continuous 
         lptype[i] = 'C'; // 'B', 'I','C' to indicate binary, general integer, continuous 
         colname[i] = (char*)malloc(sizeof(char) * (11));   // why not 11?
         sprintf_s(colname[i], 11, "%s%d", "v", i);
      }
      status = CPXnewcols(env, lp, numCols, obj, lb, ub, NULL, colname);  // <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<

      // Create the constraints, one row at a time
      idRow = 0;
      for (i = 0; i < numRows; i++)
      {  numNZrow = 0;  // number of nonzero element in the row to add
         rmatbeg[0] = 0;
         sense[0] = 'G';
         rhs[0]   = 1;
         for (j = 0; j < lstConstr[i].size(); j++)
         {  rmatind[numNZrow] = lstConstr[i][j];
            rmatval[numNZrow] = 1;
            numNZrow++;
         }
         rmatbeg[1] = numNZrow;
         rowname[0] = (char*)malloc(sizeof(char) * (11));   // why not 11?
         sprintf_s(rowname[0], 11, "%s%d", "c", idRow);
         status = CPXaddrows(env, lp, 0, 1, numNZrow, rhs, sense, rmatbeg, rmatind, rmatval, NULL, rowname);
         if (status) goto TERMINATE;
         idRow++;
      }
   }
   numRows = cur_numrows = CPXgetnumrows(env, lp);
   numCols = cur_numcols = CPXgetnumcols(env, lp);
   if (x == NULL)     x     = (double*)malloc(cur_numcols * sizeof(double));
   if (slack == NULL) slack = (double*)malloc(cur_numrows * sizeof(double));
   // Write a copy of the problem to a file, if instance sufficiently small
   if (numCols * numRows < 1000)
      status = CPXwriteprob(env, lp, "prob.lp", NULL);
   if (status)
   {  cout << "Failed to write LP to disk" << endl;
      goto TERMINATE;
   }

   status = CPXsetintparam(env, CPXPARAM_MIP_Limits_Nodes, maxnodes);   // max num of nodes
   //status = CPXsetintparam(env, CPXPARAM_TimeLimit, 60);   // max cpu time in sec, can't make this work
   //status = CPXsetintparam(env,    CPXPARAM_DetTimeLimit, 20000);  // max cpu absolute time (ticks), can't make this work
   if (status)
   {  cout << "Failure to reset max nodes, error " << status << endl;
      goto TERMINATE;
   }

   // linear case
   status = CPXchgprobtype(env, lp, CPXPROB_LP);
   if (status)
   {  cerr << "Failed to set problem to LP.\n";
      goto TERMINATE;
   }

   // solve linear
   status = CPXlpopt(env, lp);
   if (status)
   {  cerr << "Failed to optimize LP.\n";
      goto TERMINATE;
   }
   else
   {  status = CPXsolution(env, lp, &solstat, &objval, x, pi, slack, dj);
      if (status || solstat > 1)
      {  cerr << "Failed to obtain LP solution.\n";
         status = max(status, solstat);
         goto TERMINATE;
      }
      cout << "\nLP Solution status (0 ok) = " << status << endl;
      cout << "LP Solution value  = " << objval << endl;
      if (isVerbose)
      {
         if (dj == NULL) dj = (double*)malloc(cur_numcols * sizeof(double));
         if (pi == NULL) pi = (double*)malloc(cur_numrows * sizeof(double));
         if (dj == NULL || pi == NULL)
         {
            status = CPXERR_NO_MEMORY;
            cerr << "Could not allocate memory for solution.\n";
            goto TERMINATE;
         }
         status = CPXgetpi(env, lp, pi, 0, CPXgetnumrows(env, lp) - 1);
         status = CPXgetdj(env, lp, dj, 0, CPXgetnumcols(env, lp) - 1);

         if(cur_numcols < 100)
         {  status = CPXgetlb(env, lp, lb, 0, cur_numcols - 1);
            cout << "LB:  "; for (j = 0; j < cur_numcols; j++) cout << lb[j] << ", "; cout << endl;
            status = CPXgetub(env, lp, ub, 0, cur_numcols - 1);
            cout << "UB:  "; for (j = 0; j < cur_numcols; j++) cout << ub[j] << ", "; cout << endl;
            cout << "sol: "; for (j = 0; j < cur_numcols; j++) cout <<  x[j] << ", "; cout << endl;
         }
      }
   }

   // -------------------------------------------- Going to MIP
   cout << " -------------- Looking for integer optimality -------------- " << endl;
   for (i = 0; i < numCols; i++)
      ctype[i] = 'B'; // 'B', 'I','C' to indicate binary, general integer, continuous 

   status = CPXcopyctype(env, lp, ctype);
   if (status)
   {  cerr << "Failed to set integrality on vars.\n";
      goto TERMINATE;
   }
   status = CPXwriteprob(env, lp, "MIP.lp", NULL);
   if (uselogcallback)
   {  /* Set overall node limit in case callback conditions are not met */
      status = CPXsetintparam(env, CPXPARAM_MIP_Limits_Nodes, 5000);
      if (status) goto TERMINATE;

      status = CPXgettime(env, &myloginfo.timestart);
      if (status) {
         fprintf(stderr, "Failed to query time.\n");
         goto TERMINATE;
      }
      status = CPXgetdettime(env, &myloginfo.dettimestart);
      if (status) {
         fprintf(stderr, "Failed to query deterministic time.\n");
         goto TERMINATE;
      }
      myloginfo.numcols = CPXgetnumcols(env, lp);
      myloginfo.lastincumbent = CPXgetobjsen(env, lp) * 1e+35;
      myloginfo.lastlog = -10000;
      status = CPXsetinfocallbackfunc(env, logcallback, &myloginfo);
      if (status) {
         fprintf(stderr, "Failed to set logging callback function.\n");
         goto TERMINATE;
      }
      /* Turn off CPLEX logging */
      status = CPXsetintparam(env, CPXPARAM_MIP_Display, 0);
      if (status)  goto TERMINATE;
   }

   status = CPXmipopt(env, lp);
   if (status)
   {  cout << "Failed to optimize ." << name << endl;
      goto TERMINATE;
   }

   status = CPXsolution(env, lp, &solstat, &objval, x, NULL, slack, NULL);
   if (status)
   {  cerr << "Failed to obtain MIP solution.\n";
      goto TERMINATE;
   }
   cout << "\nMIP Solution status (101 optimal ok) = " << solstat << endl;
   cout << "MIP Solution value  = " << objval << "\nSolution: " << endl;
   for (j = 0; j < cur_numcols; j++)
      if (x[j] > 0.001)
         cout << j << " ";
   cout << endl;

   if (isVerbose)
   {
      int solnmethod, solntype, pfeasind, dfeasind;
      status = CPXsolninfo(env, lp, &solnmethod, &solntype, &pfeasind, &dfeasind);

      status = CPXgetlb(env, lp, lb, 0, cur_numcols - 1);
      status = CPXgetub(env, lp, ub, 0, cur_numcols - 1);

      if(cur_numcols<200 && cur_numrows<200)
      {  for (i = 0; i < cur_numrows; i++) cout << "Row: " << i << " Slack = " << slack[i] << endl;
         for (j = 0; j < cur_numcols; j++)
            if (x[j] > 0.001)
               cout << "Column " << j << "  Value = " << x[j] << endl;

         cout << "LB:  "; for (j = 0; j < cur_numcols; j++) cout << lb[j] << ", "; cout << endl;
         cout << "UB:  "; for (j = 0; j < cur_numcols; j++) cout << ub[j] << ", "; cout << endl;
         cout << "sol: "; for (j = 0; j < cur_numcols; j++) cout << x[j] << ", "; cout << endl;
      }
   }

TERMINATE:

   // Free up the solution
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
   datapath     = JSV["datapath"];
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

// Log new incumbents if they are at better than the old by a
// relative tolerance of 1e-5; also log progress info every 100 nodes.
static int CPXPUBLIC
logcallback(CPXCENVptr env, void* cbdata, int wherefrom, void* cbhandle) 
{
   int status = 0;

   LOGINFOptr info = (LOGINFOptr)cbhandle;
   int        hasincumbent = 0;
   int        newincumbent = 0;
   int        nodecnt;
   int        nodesleft;
   double     objval;
   double     bound;
   double* x = NULL;


   status = CPXgetcallbackinfo(env, cbdata, wherefrom,
      CPX_CALLBACK_INFO_NODE_COUNT, &nodecnt);
   if (status)  goto TERMINATE;

   status = CPXgetcallbackinfo(env, cbdata, wherefrom,
      CPX_CALLBACK_INFO_NODES_LEFT, &nodesleft);
   if (status)  goto TERMINATE;

   status = CPXgetcallbackinfo(env, cbdata, wherefrom,
      CPX_CALLBACK_INFO_MIP_FEAS, &hasincumbent);
   if (status)  goto TERMINATE;

   if (hasincumbent) {
      status = CPXgetcallbackinfo(env, cbdata, wherefrom,
         CPX_CALLBACK_INFO_BEST_INTEGER, &objval);
      if (status)  goto TERMINATE;

      if (fabs(info->lastincumbent - objval) > 1e-5 * (1.0 + fabs(objval))) {
         newincumbent = 1;
         info->lastincumbent = objval;
      }
   }

   if (nodecnt >= info->lastlog + 1000 || newincumbent) {
      double walltime;
      double dettime;

      status = CPXgetcallbackinfo(env, cbdata, wherefrom, CPX_CALLBACK_INFO_BEST_REMAINING, &bound);
      if (status)  goto TERMINATE;

      if (!newincumbent)  info->lastlog = nodecnt;

      status = CPXgettime(env, &walltime);
      if (status)  goto TERMINATE;

      status = CPXgetdettime(env, &dettime);
      if (status)  goto TERMINATE;

      printf("Time = %.2f  Dettime = %.2f  Nodes = %d(%d)  Best objective = %g",
         walltime - info->timestart, dettime - info->dettimestart,
         nodecnt, nodesleft, bound);
      if (hasincumbent)  cout << "  Incumbent objective = " << objval << endl;
      else               cout << endl;
   }

   if (newincumbent) 
   {  int j;
      int numcols = info->numcols;

      x = (double*)malloc(numcols * sizeof(double));
      if (x == NULL) 
      {  status = CPXERR_NO_MEMORY;
         goto TERMINATE;
      }
      status = CPXgetcallbackincumbent(env, cbdata, wherefrom, x, 0, numcols - 1);
      if (status)  goto TERMINATE;

      cout <<"New ZUB! " << objval << " incumbent variables:" << endl;
      for (j = 0; j < numcols; j++) {
         if (fabs(x[j]) > 1e-6) 
            cout << "  " << j; // << ": " << x[j];
      }
      cout << endl;
   }

TERMINATE:

   free(x);
   x = NULL;
   return (status);

} /* END logcallback */
