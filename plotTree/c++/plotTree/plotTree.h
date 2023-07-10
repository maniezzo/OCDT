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
#include <cstdlib>    /* srand, rand */
#include <time.h>     /* time */
#include <assert.h>   /* assert */
#include <algorithm>  /* std::sort */

using namespace std;

class Tree
{
   public:
      void goTree();

   private:
      vector<int> dim;  // dimensione su cui agisce il cut corrispondente
      vector<double> cutval; // valore in cui è posizionato il taglio
      vector<vector<int>> ptClass; // indices of the two classes
      vector<vector<float>> X;  // features
      vector<int> Y;             // classes
      int ndim;  // num dimensions
      int n;     // num points

      string exePath();
      vector<string> split(string str, char sep);
      void readData(string dataSetFile);
};
