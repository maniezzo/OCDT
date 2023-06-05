#pragma once
#include <iostream>
#include <fstream>
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
      int bboxHeu();

};

