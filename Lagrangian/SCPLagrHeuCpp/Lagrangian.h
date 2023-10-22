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
      vector<vector<int>> lstConstr,lstCols; // colonne di ogni vincolo e vincoli di ogni colonna

      void subgradient();
      void subproblem(vector<int> x, vector<double> lambda, double &zlbiter);
      void build_structures();
      void read_data();
      vector<string> split(string str, char sep);
};