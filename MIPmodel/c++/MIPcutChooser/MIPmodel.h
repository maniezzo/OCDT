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
      void run_MIP(string testSet);

   private:
      void readData(string fpath);
      vector<string> split(string str, char sep);
};

