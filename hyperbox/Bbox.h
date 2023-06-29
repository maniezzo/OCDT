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
      int seed;      // primo punto del cluster
      int hash;      // hash dei vettori
      bool removed;  // removed from the stack
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
      vector<hbox> finalBoxes;     // the final hyperboxes
      vector<AABB> AABBstack;      // the stack of boxes
      vector<int>  hashtable;      // hash of hboxes
      vector<vector<int>> ptClass; // indices of the two classes

      void initializeBox(int idx, AABB& box, hbox domain);
      void expandBox();
      string ExePath();
      vector<string> split(string str, char sep);
      void read_data(string fpath);
      bool isInside(int idx, AABB& box, bool fBoundariesIncluded);
      bool isInsideVec(int idx, vector<double> lo, vector<double> hi, bool fBoundariesIncluded);
      int  hash(AABB& box);
      void writeHboxes();
      bool checkDominated(AABB& box); // checks whether a box is already dominated
      void writeFinals(vector<int> lstIdBox); // writes out the final solution
      void removeNonParetian(hbox domain);  // removes points surronded by similar ones
      bool inBetween(double a, double b, double c);
};
