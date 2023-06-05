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

class Bbox
{
   public:
      Bbox();
      ~Bbox();
      int bboxHeu(string fpath);
   private:
      int ndim,n; // number of dimensions (features), num points
      vector<vector<double>> X;  // features
      vector<int> Y;             // classes
      struct AABB{vector<double> min; vector<double> max;};
      vector<AABB> hbox;         // the final hyperboxes
      vector<int> ind0,ind1;     // indices of the two classes

      string ExePath();
      vector<string> split(string str, char sep);
      void read_data(string fpath);

};

