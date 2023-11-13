#pragma once
#include <iostream>
#include <fstream>
#include <sstream>
#include <iomanip>
#include <limits>
#include <cmath>
#include <string>
#include <vector>
#include <array>
#include <map>        /* dictionary  */
#include <cstdlib>    /* srand, rand */
#include <time.h>     /* time        */
#include <assert.h>   /* assert      */
#include <algorithm>  /* std::sort   */

using namespace std;

class MIPmodel
{
   public:
      void run_MIP();

   private:
      int ndim;     // num dimensions
      int n;        // num points
      int ncuts;    // num of cuts
      int nclasses; // num of classes

      struct Cutline { int dim; double cutval; };
      map<int, Cutline> cutlines;  // dictionary dei tagli
      vector<vector<int>> ptClass; // indices of the classes
      vector<vector<float>> X;     // features
      vector<int> Y;               // classes

      void cplexModel(string dataFile);
      string readConfig();
      void readData(string fpath);
      vector<string> split(string str, char sep);
      void free_and_null(char** ptr);
};

