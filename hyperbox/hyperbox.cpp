#include <iostream>
#include "Bbox.h"

using namespace std;

int main()
{
   Bbox B;
   string fpath;
   fpath = "../data/Iris_setosa.csv";
   B.bboxHeu(fpath);
   cout << "Fine\n";
}
