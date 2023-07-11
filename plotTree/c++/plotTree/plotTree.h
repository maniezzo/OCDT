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

class Tree
{
   public:
      void goTree();

   private:
      struct Cutline {int dim; double cutval;};
      map<int, Cutline> cutlines;  // dictionary dei tagli
      vector<vector<int>> ptClass; // indices of the two classes
      vector<vector<float>> X;     // features
      vector<int> Y;               // classes
      map<unsigned long, vector<int>> clusters; // points of each cluster
      vector<unsigned long> myCluster;          // cluster of each point

      int ndim;  // num dimensions
      int n;     // num points
      int ncuts; // num of cuts

      string exePath();
      vector<string> split(string str, char sep);
      void readData(string dataSetFile);
      void regionBitmasks();  // bitmask identifier of all domain partitions
};
