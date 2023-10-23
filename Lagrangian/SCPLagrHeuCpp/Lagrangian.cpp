#include <string.h>
#include <stdlib.h>
#include <iomanip>   // setw
#include "Lagrangian.h"

void Lagrangian::run_lagrangian()
{  int maxiter;
   double alpha;

   cout << "Starting Lagrangian" << endl;
   read_data();
   build_structures();

   maxiter = 100;
   alpha = 2.5;
   isVerbose = false;
   subgradient(alpha,maxiter);
   cout << "Lagrangian completed" << endl;
}

// LA funzione
void Lagrangian::subgradient(double alpha, int maxiter)
{  int i,j;
   double zlb,zlbiter,sumSubgr2,step;
   int zub,zubiter,iter;
   vector<double> lambda(nconstr);
   vector<int> x(nvar);
   vector<int> subgr(nconstr);

   for(i=0;i<nconstr;i++) lambda[i] = 0;
   zlb = 2;       // safe guess
   zub = nvar;    // safe guess
   cout.precision(2);

   ofstream fout("lagrheu.log");
   fout.precision(2);
   for(iter=0;iter<maxiter;iter++)
   {
      zlbiter = zubiter = 0;
      subproblem(x, lambda, zlbiter);
      if(zlbiter > zlb) zlb = zlbiter;

      zubiter = fixZub(x);
      if(zubiter<zub) zub = zubiter;

      // optimality check
      if (zub - zlb < 1)
      {  cout << "OPTIMUM FOUND!! zub=" << zub <<"\nexiting ..." << endl;
         return;
      }

      // subgradients computation
      sumSubgr2 = 0;
      for (i = 0; i < nconstr; i++) 
      {  subgr[i] = 1;
         for(j=0;j<lstColOfConstr[i].size();j++)
            subgr[i] -= x[lstColOfConstr[i][j]];
         sumSubgr2 += subgr[i]*subgr[i];
      }

      if(zub > 2.0*zlb) step = alpha*1.5*zlb / sumSubgr2;
      else              step = alpha*(zub-zlb) / sumSubgr2;

      for (i = 0; i < nconstr; i++)
      {  lambda[i] += step*subgr[i];
         if(lambda[i]<=0) lambda[i]=0;
      }

      cout << "iter " << iter <<" zlb=" << zlb << " zlbiter= " << zlbiter << " zubiter=" << zubiter << " zub=" 
           << zub << " sumSubgr2=" << sumSubgr2 << " step=" << step <<endl;
      
      if(iter%100==0) alpha = 0.9*alpha;

      // log
      if(isVerbose)
      {  for (i = 0; i < nconstr; i++) fout << setw(6) << subgr[i]; fout << endl;
         for (i = 0; i < nconstr; i++) fout << setw(6) << std::fixed << lambda[i]; fout << endl;
      }
   }
   fout.close();
}

int Lagrangian::fixZub(vector<int> &x)
{  int i,j,nunc,zubiter=0;
   vector<bool> fCol(nvar);     // columns in the solution
   vector<bool> fRow(nconstr);  // row already covered;
   vector<int>  lstUncovered;   // uncovered rows;
   bool fLoop=false;

   for(i=0;i<nconstr;i++) fRow[i]=false;
   for(i=0;i<nvar;i++)    fCol[i]=false;

   for (i = 0; i < nvar; i++)
      if(x[i]>0)
      {  fCol[i] = true;
         zubiter++;
         for(j=0;j<lstConstrOfCol[i].size();j++)
            fRow[lstConstrOfCol[i][j]] = true;
      }
      else
         fLoop = true;
   
   while (fLoop)
   {
      lstUncovered.clear();
      for(i=0;i<nconstr;i++)
         if(!fRow[i])
            lstUncovered.push_back(i);

      int minNunc=INT_MAX;
      vector<int> lstMin;
      for (int ii = 0; ii < lstUncovered.size(); ii++) // I choose the col that covers the least covered row
      {  nunc = 0;
         i = lstUncovered[ii];  // uncovered row
         for (int jj = 0; jj < lstColOfConstr[i].size(); jj++)
         {  j = lstColOfConstr[i][jj];
            if(!fRow[j])
               nunc++;
         }
         if (nunc < minNunc) // least num of covering columns so far
         {  minNunc = nunc;
            lstMin.clear();
            lstMin.push_back(i);
         }
         else if(nunc==minNunc)
            //if(find(lstMin.begin(), lstMin.end(), i) != lstMin.end()) // not already present
               lstMin.push_back(i);
      }
      // scelgo la colonna in lstMin che copre piu' righe
      ...
   }

   return zubiter;
}

// solves the LR of the SCP given the lambdas
void Lagrangian::subproblem(vector<int> &x, vector<double> &lambda, double &zlbiter)
{
   int i,j;
   double colCost; // the penalized cost of each column
   
   zlbiter = 0;
   for (i = 0; i < nvar; i++)
   {  colCost = 1;  // c_j
      for(j=0;j<lstConstrOfCol[i].size(); j++)
         colCost -= lambda[lstConstrOfCol[i][j]];

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
   lstConstrOfCol = vector<vector<int>>(nvar);
   for(i=0;i<nconstr;i++)
      for(j=0;j<lstColOfConstr[i].size();j++)
      {  col = lstColOfConstr[i][j];
         lstConstrOfCol[col].push_back(i);
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
   {  getline(f, line);
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
         lstColOfConstr.push_back(val);
      }
      f.close();
   }
   else 
   {  cout << "Cannot open dataset input file\n";
      exit(1);
   }
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
