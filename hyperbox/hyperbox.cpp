#include <iostream>
#include "Bbox.h"
#include <ctime>
using namespace std;

int main()
{
   Bbox B;
   string fpath;
   fpath = "../data/Iris_setosa.csv";
   B.isVerbose = false;

   clock_t c_start = clock();
   B.bboxHeu(fpath);
   clock_t c_end = clock();

   double tt = (c_end - c_start) / CLOCKS_PER_SEC;
   std::cout << "Fine. CPU time: " << tt << " s" << endl;
}
