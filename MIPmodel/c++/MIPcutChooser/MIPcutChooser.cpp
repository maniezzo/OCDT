#include "MIPmodel.h"

int main()
{  string testSet;
   MIPmodel MIP;

   testSet = "test1";
   clock_t c_start = clock();
   MIP.run_MIP(testSet);
   clock_t c_end = clock();

   double tt = (c_end - c_start) / CLOCKS_PER_SEC;
   std::cout << "Fine. CPU time: " << tt << " s" << endl;
}
