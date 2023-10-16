#include "MIPmodel.h"

int main()
{  MIPmodel MIP;

   clock_t c_start = clock();
   MIP.run_MIP();
   clock_t c_end = clock();

   double tt = (c_end - c_start) / CLOCKS_PER_SEC;
   std::cout << "Fine. CPU time: " << tt << " s" << endl;
}
