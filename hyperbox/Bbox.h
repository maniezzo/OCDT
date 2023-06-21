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
      int classe;
      vector<double> loOut, loIn, hiOut, hiIn; // internal and external max coordinates
      vector<int> points;  // points inside the box
};

class Bbox
{
   public:
      Bbox();
      ~Bbox();
      int bboxHeu(string fpath);
   private:
      int ndim,n,m;  // number of dimensions (features), num points, hashtable size
      int nb;        // boxes global counter
      vector<vector<double>> X;  // features
      vector<int> Y;             // classes
      struct hbox{vector<double> min; vector<double> max;}; // a final hyperbox, means of AABB
      vector<hbox> finalBoxes;   // the final hyperboxes
      vector<AABB> hboxes;       // the AABB along the way
      vector<int> hashtable;     // hash of hboxes
      vector<vector<int>> ptClass; // indices of the two classes

      void initializeBox(int idx, AABB& box, hbox domain);
      void expandBox(int idx, AABB& box, int dim, int idpt);
      string ExePath();
      vector<string> split(string str, char sep);
      void read_data(string fpath);
      bool isInside(int idx, AABB box, bool fBoundariesIncluded);
      bool isInsideVec(int idx, vector<double> lo, vector<double> hi, bool fBoundariesIncluded);
      int  hash(AABB box);
      void writeHboxes();
};
