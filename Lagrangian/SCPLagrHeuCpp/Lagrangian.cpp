#include <string.h>
#include <stdlib.h>
#include "Lagrangian.h"

void Lagrangian::run_lagrangian()
{
   cout << "Starting Lagrangian" << endl;
   read_data();
   build_structures();
   subgradient();
   cout << "Lagrangian completed" << endl;
}

// LA funzione
void Lagrangian::subgradient()
{  int i,j;
   double zlb=0,zlbiter;
   int zub=INT_MAX,zubiter;
   vector<double> lambda(nconstr);
   vector<int> x(nvar);

   for(i=0;i<nconstr;i++) lambda[i] = 0;

   zlbiter = zubiter = 0;
   subproblem(x, lambda, zlbiter, zubiter);
   if(zlbiter > zlb) zlb = zlbiter;

   // optimality check
   if (zub - zlb < 1)
   {
      cout << "OPTIMUM FOUND!! \n exiting ..." << endl;
      return;
   }

   // subgradient update

}

// solves the SCP given the lambdas
void Lagrangian::subproblem(vector<int> x, vector<double> lambda, double &zlbiter, int &zubiter)
{
   int i,j;
   double colCost; // the penalized cost of each column
   
   zlbiter = 0;
   for (i = 0; i < nvar; i++)
   {  colCost = 1;
      for(j=0;j<lstCols[i].size(); j++)
         colCost -= lambda[lstCols[i][j]];

      if(colCost < 0) x[i] = 1;
      else            x[i] = 0;
      zlbiter += x[i]*colCost;
   }

   for(j=0;j<nconstr;j++)
      zlbiter += lambda[j];
}

// build some auxiliary memory structures
void Lagrangian::build_structures()
{  int i,j,col;
   
   // for each column, the constraints that cover it
   lstCols = vector<vector<int>>(nvar);
   for(i=0;i<nconstr;i++)
      for(j=0;j<lstConstr[i].size();j++)
      {  col = lstConstr[i][j];
         lstCols[col].push_back(i);
      }
}

void Lagrangian::read_data()
{  int i,j;
   string line;
   vector<string> elem;
   vector<int> val;

   // leggo i punti
   ifstream f;
   string path = "\\git\\ODT\\MIPmodel\\cSharp\\ODTMIPmodel\\bin\\Debug\\net6.0\\";
   string dataFile = path + "test1.prob";
   cout << "Opening datafile " << dataFile << endl;
   f.open(dataFile);
   if (f.is_open())
   {
      getline(f, line);
      nvar = stoi(line);
      getline(f, line);
      nconstr = stoi(line);
      //lstConstr = vector<vector<int>>(nconstr);
      while (getline(f, line))
      {  //read data from file object and put it into string.
         elem = split(line,' ');
         val.clear();
         for(i=0;i<elem.size();i++)
            val.push_back( stoi(elem[i]));
         lstConstr.push_back(val);
      }
      f.close();
   }
   else cout << "Cannot open dataset input file\n";
}

// split di una stringa in un array di elementi delimitati da separatori
vector<string> Lagrangian::split(string str, char sep)
{  vector<string> tokens;
   size_t start;
   size_t end = 0;
   while ((start = str.find_first_not_of(sep, end)) != std::string::npos) {
      end = str.find(sep, start);
      tokens.push_back(str.substr(start, end - start));
   }
   return tokens;
}
