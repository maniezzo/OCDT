#include "plotTree.h"
#include <windows.h>    // GetModuleFileName
#include <stack>
#include <algorithm>    // remove, erase

void Tree::goTree()
{  vector<int> dummy;
   ptClass.push_back(dummy); // row 0, class 0 points
   ptClass.push_back(dummy); // row 1, class 1 points

   string line, dataFileName = "test1";
   ifstream fconf;
   vector<string> elem;
   fconf.open("config.json");
   if (fconf.is_open())
   {
      getline(fconf, line);
      getline(fconf, line);
      elem = split(line, ':');
      dataFileName = elem[1];
      dataFileName.erase(remove(dataFileName.begin(), dataFileName.end(), '"'), dataFileName.end());
      dataFileName.erase(remove_if(dataFileName.begin(), dataFileName.end(), isspace), dataFileName.end());
      cout << dataFileName << endl;;
      fconf.close();
   }

   readData(dataFileName);
   regionBitmasks();
   DFS(0);
   writeTree(dataFileName);
}

// writes the tree on a file, input for graphviz
void Tree::writeTree(string dataFileName)
{  int i;
   ofstream f;
   f.open("graph.txt");
   f << "digraph G {" << endl;
   f << "graph[fontname = \"helvetica\"]"<< endl;
   f << "node[fontname = \"helvetica\"]" << endl;
   f << "edge[fontname = \"helvetica\"]" << endl;
   for(i=0;i<decTree.size();i++)
      if(decTree[i].idCut >=0)
         f << i << " [label = \"" << decTree[i].id << " (cut "<< decTree[i].idCut <<") \ndim "<< decTree[i].cutDim <<" val. "<< decTree[i].cutValue <<"\"]" << endl;
      else
         f << i << " [shape = box label = \"" << decTree[i].id << "\n class " << decTree[i].idClass << "\"]" << endl;

   for (i = 0; i < decTree.size(); i++)
   {
      if (decTree[i].left >= 0)
         f << decTree[i].id << " -> " << decTree[i].left << endl;
      if (decTree[i].right >= 0)
         f << decTree[i].id << " -> " << decTree[i].right << endl;
   }
   // title
   f << "labelloc = \"t\""<<endl;
   f << "label = \""<< dataFileName << "\"" << endl;
   f << "}" << endl;
   f.close();
}

// Depth First /search/ construction 
void Tree::DFS(int s)
{  int i, cutBitMask, cutBM=0;
   // Create a stack for DFS
   stack<stackItem> stack;
   stackItem si;

   // Push the source node, points are id of all points
   vector<int> v(Y.size());
   for(i=0;i<v.size();i++) v[i]=i;
   nodePoints.push_back(v);
   newNode(s,0);
   pointsLeftSon(s);
   pointsRightSon(s);
   cutBM |= (1 << decTree[0].idCut);  // mette a 1 il *-esimo bit DA DESTRA
   si = {s,cutBM };
   stack.push(si);

   while (!stack.empty())
   {
      // Pop a vertex from stack 
      si = stack.top();
      stack.pop();
      s          = si.idnode;
      cutBitMask = si.bitMaskCuts;

      // Stack may contain same vertex twice. So
      // we need to print the popped item only if it is not visited.
      if (!decTree[s].visited)
      {  cout << "Expanding node " << s << " bpoints " << nodePoints[s].size() << endl;
         decTree[s].visited = true;

         if(!sameClass(s))
         {  // Get all adjacent vertices of the popped vertex s
            int l = decTree[s].left;
            if (l>0 && (decTree.size() <= l || !decTree[l].visited))
            {  newNode(l, cutBitMask);
               if(decTree[l].idCut >= 0)
               {  cutBM = cutBitMask;
                  cutBM |= (1 << decTree[l].idCut);  // mette a 1 il *-esimo bit DA DESTRA
                  pointsLeftSon(l);
                  pointsRightSon(l);
                  si = {l,cutBM};
                  stack.push(si);
               }
            }
            int r = decTree[s].right;
            if (r>0 && (decTree.size() <= r || !decTree[r].visited))
            {  newNode(r, cutBitMask);
               if (decTree[r].idCut >= 0)
               {  cutBM = cutBitMask;
                  cutBM |= (1 << decTree[r].idCut);  // mette a 1 il *-esimo bit DA DESTRA
                  pointsLeftSon(r);
                  pointsRightSon(r);
                  si = {r,cutBM};
                  stack.push(si);
               }
            }
         }
         else // leaf node
            decTree[s].idCut = -1;
      }
   }
}

// checks if all points are of the same class
bool Tree::sameClass(int node)
{  int i,idclass;
   bool fSame = true;
   idclass = Y[nodePoints[node][0]];
   for(i=0;i<nodePoints[node].size();i++)
      if(Y[nodePoints[node][i]] != idclass)
      {  fSame = false;
         break;
      }
   return fSame;
}

// 3D contingency table, number of cases per cut and per value. Works on the regions not on the points
void Tree::newNode(int idnode, int cutBitMask)
{  int i,j,ptClass;
   vector<vector<vector<int>>> freq (ncuts, vector<vector<int>>(2,vector<int>(2,0)));
   
   // contingency table (num regions per cut, per attr. value (above/below cut), per class
   for (i = 0; i < bitmasks.size(); i++) // for each bitmask (region)
   {  ptClass = Y[clusters[bitmasks[i]][0]]; // class 0 or 1 of the region. Bitmasks encode regions
      for (j = 0; j < ncuts; j++)
      {  //dim = cutlines[j].dim;
         if(bitmasks[i]&(1 << j)) // region bitmask (NOT CUT)
            freq[j][1][ptClass]++;
         else
            freq[j][0][ptClass]++;
      }
   }
   defineNode(freq,idnode, cutBitMask);
}

// puts node in the decision tree
void Tree::defineNode(vector<vector<vector<int>>> freq, int idnode, int cutBitMask)
{  int i,minval = INT_MAX;
   int idCut = -1, idmax = -1;
   double sum;
   double h,maxh = -1;
   bool isSameCLass = false;

   if(sameClass(idnode))
   {  isSameCLass = true;
      goto l0;
   }

   for (i = 0; i < ncuts; i++)
   {
      bool is_set = (cutBitMask & (1 << i)) != 0; // check if i-th bit is set
      if (is_set)
      {  h = -1;
         continue;
      }
      if (freq[i][0][0] < minval) { minval = freq[i][0][0]; idCut = i; }
      if (freq[i][0][1] < minval) { minval = freq[i][0][1]; idCut = i; }
      if (freq[i][1][0] < minval) { minval = freq[i][1][0]; idCut = i; }
      if (freq[i][1][1] < minval) { minval = freq[i][1][1]; idCut = i; }

      if(minval > 0)
      {  sum = freq[i][0][0] + freq[i][0][1] + freq[i][1][0] + freq[i][1][1];
         h = 0;   // entropia del cut
         h += -(freq[i][0][0] / sum) * log(freq[i][0][0] / sum);
         h += -(freq[i][0][1] / sum) * log(freq[i][0][1] / sum);
         h += -(freq[i][1][0] / sum) * log(freq[i][1][0] / sum);
         h += -(freq[i][1][1] / sum) * log(freq[i][1][1] / sum);
      }
      else
         h = DBL_MAX;

      if (h > maxh)
      {  maxh  = h;
         idmax = i; // cut di entropia massima
      }
   }

l0:Node N;
   N.id = decTree.size();
   N.idCut    = (isSameCLass ? -1 : idmax);
   N.cutDim   = (isSameCLass ? -1 : cutlines[idmax].dim);
   N.cutValue = (isSameCLass ? -1 : cutlines[idmax].cutval);
   N.left     = -1;
   N.right    = -1;
   N.visited  = false;
   N.idClass  = (isSameCLass ? Y[nodePoints[idnode][0]] : INT_MIN);
   decTree[idnode] = (N);
}

// points smaller than cut
void Tree::pointsLeftSon(int idnode)
{  int i;
   vector<int> leftpoints;
   Node* N = &decTree[decTree.size() - 1]; // parent node
   for (i = 0; i < nodePoints[idnode].size(); i++)
      if (X[nodePoints[idnode][i]][N->cutDim] < N->cutValue)
         leftpoints.push_back(nodePoints[idnode][i]);

   if (leftpoints.size() > 0 && leftpoints.size() < nodePoints[idnode].size())
   {  nodePoints.push_back(leftpoints);
      N->left = nodePoints.size() - 1;
   }
}

// points bigger than cut
void Tree::pointsRightSon(int idnode)
{  int i;
   vector<int> rightpoints;
   Node* N = &decTree[decTree.size() - 1]; // parent node
   for (i = 0; i < nodePoints[idnode].size(); i++)
      if (X[nodePoints[idnode][i]][N->cutDim] > N->cutValue)
         rightpoints.push_back(nodePoints[idnode][i]);

   if (rightpoints.size() > 0 && rightpoints.size() < nodePoints[idnode].size())
   {  nodePoints.push_back(rightpoints);
      N->right = nodePoints.size() - 1;
   }
}

// bitmask identifier of all domain partitions
void Tree::regionBitmasks()
{  int i,j,dim;
   double val;
   unsigned long bitmask;

   for(i=0;i<n;i++)
   {  bitmask=0;
      for(j=0;j<ncuts;j++)
      {  dim = cutlines[j].dim;
         val = cutlines[j].cutval;
         if(X[i][dim]>val)
            bitmask |= (1 << j);  // mette a 1 il j-esimo bit da destra (i cut sarammo da dx a sx !!!!)
      }
      myCluster.push_back(bitmask);   // cluster in cui cade ogni punto
      clusters[bitmask].push_back(i); // punti dentro ogni cluster
      if(find(bitmasks.begin(), bitmasks.end(), bitmask) == bitmasks.end())
         bitmasks.push_back(bitmask); // bitmask dei cluster
   }
}

// cut lines and data points
void Tree::readData(string dataFileName)
{  int i,j,cont,id;
   double d;
   string line;
   vector<string> elem;
   vector<float> val;
   cout << "Running from " << exePath() << endl;

   // leggo i tagli
   ifstream f;
   f.open("..//..//..//data//"+ dataFileName +"_cuts.txt");
   if (f.is_open())
   {  cont = 0;
      while (getline(f, line))
      {  cout << line << endl;
         elem = split(line, ' ');
         i = atoi(elem[1].c_str());
         d = atof(elem[3].c_str());
         Cutline c;
         c.dim = i;      // dimensione in cui agisce il taglio
         c.cutval = d;   // posizione del taglio
         cutlines[cont] = c;
         cont++;
      }
      ncuts = cont;
      f.close();
   }
   else cout << "Cannot open cuts input file\n";

   // leggo i punti
   string dataSetFile = "..//..//..//data//" + dataFileName + ".csv";
   f.open(dataSetFile);
   if (f.is_open())
   {  getline(f, line);  // headers
      elem = split(line, ',');
      ndim = elem.size() - 2;

      while (getline(f, line))
      {  //read data from file object and put it into string.
         cont = 0;
         val.clear();
         elem = split(line, ',');
         id = stoi(elem[0]);
         //if (id > 40 && !(id > 100 && id < 141)) goto l0;
         cout << id << endl;
         for (i = 1; i < 1 + ndim; i++)         // FILTERING DATA for iris_setosa
         if(dataFileName != "iris_setosa" || (i==2 || i==3))
         {  d = stof(elem[i]);
            d = round(100.0 * d) / 100.0;     // rounded to 2nd decimal
            val.push_back(d);
         }
         X.push_back(val);
         j = stoi(elem[ndim + 1]);
         Y.push_back(j);
         if (j == 0) ptClass[0].push_back(Y.size() - 1); // starts at 0
         else        ptClass[1].push_back(Y.size() - 1);
l0:      cont++;
      }
      f.close();
      ndim = X[0].size(); // in case of partial dataset
      n = Y.size();  // number of input records
   }
   else cout << "Cannot open dataset input file\n";
}

// split di una stringa in un array di elementi delimitati da separatori
vector<string> Tree::split(string str, char sep)
{  vector<string> tokens;
   size_t start;
   size_t end = 0;
   while ((start = str.find_first_not_of(sep, end)) != std::string::npos) {
      end = str.find(sep, start);
      tokens.push_back(str.substr(start, end - start));
   }
   return tokens;
}

// trova il path del direttorio da cui si e' lanciato l'eseguibile
string Tree::exePath()
{  wchar_t buffer[MAX_PATH];
   GetModuleFileName(NULL, buffer, MAX_PATH);
   wstring ws(buffer);
   string s = string(ws.begin(), ws.end());
   string::size_type pos = s.find_last_of("\\/");
   return s.substr(0, pos);
}
