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

   if (env == NULL) {
      char  errmsg[CPXMESSAGEBUFSIZE];
      fprintf(stderr, "Could not open CPLEX environment.\n");
      CPXgeterrorstring(env, status, errmsg);
      fprintf(stderr, "%s", errmsg);
      goto TERMINATE;
   }
   else
      cout <<"CPLEX running" << endl;


   // Turn on output to the screen 
   status = CPXsetintparam(env, CPXPARAM_ScreenOutput, CPX_ON);
   if (status) 
   {  fprintf(stderr, "Failure to turn on screen indicator, error %d.\n", status);
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
   for (i = 0; i < GAP->m; i++)
      for (j = 0; j < GAP->n; j++)
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
