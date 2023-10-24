#pragma once
#include <iostream>
#include <fstream>
#include <sstream>
#include <string>
#include <vector>

using namespace std;

class Lagrangian
{
   public:
      void run_lagrangian();

   private:
      int nvar,nconstr;
      bool isVerbose;
      vector<vector<int>> lstColOfConstr,lstConstrOfCol; // colonne di ogni vincolo e vincoli di ogni colonna
      vector<int> zubSol; // the best found solution, columns id

      void subgradient(double alpha, int maxiter);
      void subproblem(vector<int> &x, vector<double> &lambda, double &zlbiter);
      int fixZub(vector<int> x, int& zub);
      void writeSolution(string path, string dataset);
      void build_structures();
      void read_data(string path, string dataset);
      vector<string> split(string str, char sep);
};