#include <iostream>
#include "Bbox.h"

using namespace std;

int main()
{
   Bbox B;
   string fpath;
   fpath = "../data/test1.csv";
   B.bboxHeu(fpath);
   cout << "Fine\n";
}
