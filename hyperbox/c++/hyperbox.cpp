#include <iostream>
#include "Bbox.h"
#include <ctime>
using namespace std;

int main()
{
   Bbox B;
   string fpath;
   string dataFileName = "monks-2-train";
   fpath = "..//..//data//"+dataFileName+".csv";
   B.isVerbose = false;

   clock_t c_start = clock();
   B.bboxHeu(fpath, dataFileName);
   clock_t c_end = clock();

   double tt = (c_end - c_start) / CLOCKS_PER_SEC;
   std::cout << "Fine. CPU time: " << tt << " s" << endl;
}
