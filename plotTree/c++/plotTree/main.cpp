#include "plotTree.h"

int main()
{
   Tree T;
   T.goTree();
   system("graphviz.bat"); // running graphviz on file graph.txt
   cout << "Fine, result in file output.png"<< endl;
}