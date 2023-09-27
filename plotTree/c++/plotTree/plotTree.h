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

class Node
{
   public:
      int id;
      int idCut;        // cut associato al nodo
      int cutDim;       // dimension where the cut acts
      double cutValue;  // value of the cut
      bool visited;     // used by the DFS
      int idClass;      // in case of a node, class of all points
      int idNodePoints; // row of nodePoints array cotaining the points of the node
      int left, right;  // pointers to left and right offspring
};

class Tree
{
   public:
      void goTree();

   private:
      struct stackItem {int idnode; int bitMaskCuts;}; // the node and the cuts used so far
      struct Cutline   {int dim; double cutval;};
      map<int, Cutline> cutlines;  // dictionary dei tagli
      vector<vector<int>> ptClass; // indices of the classes
      vector<vector<float>> X;     // features
      vector<int> Y;               // classes
      map<unsigned long, vector<int>> regCluster; // points of each cluster
      vector<unsigned long> ptCluster;            // cluster of each point
      vector<unsigned long> bitMaskRegion;        // list of bitmasks encoding regions of attribute space ndim-dimensional

      int ndim;     // num dimensions
      int n;        // num points
      int ncuts;    // num of cuts
      int nclasses; // num of classes
      map<int,Node> decTree;          // the resulting decision tree (dict of id / nodes, id is the index in nodepoints)
      vector<vector<int>> nodePoints; // subset of points associated with each node of the decision tree

      string exePath();
      vector<string> split(string str, char sep);
      void readData(string dataSetFile);
      void regionBitmasks();                    // bitmask identifier of all domain partitions
      void newNode(int idnode, int cutBitMask); // new tree node, based on number of cases per cut and per value
      void defineNode(vector<vector<vector<int>>> freq, int idnode, int cutBitMask);
      void DFS(int s);                     // actually, not search but construction
      bool sameClass(int node);            // checks if all points are of the same class
      void pointsLeftSon(int idnode);      // points smaller than cut
      void pointsRightSon(int idnode);     // points bigger than cut
      void writeTree(string dataFileName); // writes the tree on a file, input for graphviz
      void checkSol();                     // checks the correctness of the tree
};
