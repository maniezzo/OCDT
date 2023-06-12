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

// the hyperboxes with internal and external boundaries
class AABB
{
   public:
      AABB(int);
      ~AABB();
      int id;
      vector<double> loOut, loIn, hiOut, hiIn; // internal and external max coordinates
};

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
      struct hbox{vector<double> min; vector<double> max;}; // a final hyperbox, means of AABB
      vector<hbox> finalBoxes;   // the final hyperboxes
      vector<AABB> hboxes;       // the AABB along the way
      vector<int> ind0,ind1;     // indices of the two classes

      void initializeBox(int idx, AABB& box, hbox domain);
      void expandBox(int idx, AABB& box, int dim);
      string ExePath();
      vector<string> split(string str, char sep);
      void read_data(string fpath);
      bool isInside(int idx, AABB box);
      bool isInside(int idx, vector<double> lo, vector<double> hi);
};
