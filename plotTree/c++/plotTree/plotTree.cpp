#include "plotTree.h"
#include <windows.h>    // GetModuleFileName
#include <stack>

void Tree::goTree()
{  vector<int> dummy;
   ptClass.push_back(dummy); // row 0, class 0 points
   ptClass.push_back(dummy); // row 1, class 1 points

   string dataSetFile = "..//..//..//data//test1.csv";
   readData(dataSetFile);
   regionBitmasks();
   DFS(0);
}

void Tree::DFS(int s)
{  int i;
   // Create a stack for DFS
   stack<int> stack;

   // Push the source node, points are id of all points
   std::vector<int> v(Y.size());
   for(i=0;i<v.size();i++) v[i]=i;
   nodePoints.push_back(v);
   contingency3D(s);
   stack.push(s);

   while (!stack.empty())
   {
      // Pop a vertex from stack 
      int s = stack.top();
      stack.pop();

      // Stack may contain same vertex twice. So
      // we need to print the popped item only if it is not visited.
      if (!decTree[s].visited)
      {  cout << s << " ";
         decTree[s].visited = true;
      }

      // Get all adjacent vertices of the popped vertex s
      // If a adjacent has not been visited, then push it to the stack.
      int l = decTree[s].left;
      if (!decTree[l].visited)
         stack.push(l);
      int r = decTree[s].right;
      if (!decTree[r].visited)
         stack.push(r);
   }
}

// number of cases per cut and per value. Works on the regions not on the points
void Tree::contingency3D(int idnode)
{  int i,j,dim,ptClass;
   vector<vector<vector<int>>> freq (ncuts, vector<vector<int>>(2,vector<int>(2,0)));
   
   // contingency table (num cases per cut, per attr. value (above/below cut), per class
   for (i = 0; i < bitmasks.size(); i++) // for each bitmask (region)
   {  ptClass = Y[clusters[bitmasks[i]][0]]; // class 0 or 1 of the region. Bitmasks encode regions
      for (j = 0; j < ncuts; j++)
      {  //dim = cutlines[j].dim;
         if(bitmasks[i]&(1 << j))
            freq[j][1][ptClass]++;
         else
            freq[j][0][ptClass]++;
      }
   }
   defineNode(freq,idnode);
}

void Tree::defineNode(vector<vector<vector<int>>> freq, int idnode)
{  int i,minval;
   minval = INT_MAX;
   int idCut = -1, idmax = -1;
   double sum;
   double h,maxh = -1;
   for (i = 0; i < ncuts; i++)
   {  if (freq[i][0][0] < minval) { minval = freq[i][0][0]; idCut = i; }
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
      {
         maxh  = h;
         idmax = i; // cut di entropia massima
      }
   }

   Node N;
   N.id = decTree.size();
   N.idCut    = idmax;
   N.cutDim   = cutlines[i].dim;
   N.cutValue = cutlines[i].cutval;
   N.left     = N.id + 1;
   N.right    = N.id + 2;
   N.visited  = false;
   decTree.push_back(N);

   vector<int> leftpoints, rightpoints; // smaller than cut, bigger than cut
   for(i=0;i<nodePoints[idnode].size();i++)
      if(X[nodePoints[idnode][i]][N.cutDim]>N.cutValue)
         rightpoints.push_back(nodePoints[idnode][i]);
      else
         leftpoints.push_back(nodePoints[idnode][i]);

   nodePoints.push_back(leftpoints);
   nodePoints.push_back(rightpoints);
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
void Tree::readData(string dataSetFile)
{  int i,j,cont,id;
   double d;
   string line;
   vector<string> elem;
   vector<float> val;
   cout << "Running from " << exePath() << endl;

   // leggo i tagli
   ifstream f;
   f.open("..//..//..//MIPmodel//python//cuts.txt");
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
         for (i = 1; i < 1 + ndim; i++)         // FILTERING DATA ?
         //if(i==2 || i==3)
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
{
   vector<string> tokens;
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
{
   wchar_t buffer[MAX_PATH];
   GetModuleFileName(NULL, buffer, MAX_PATH);
   wstring ws(buffer);
   string s = string(ws.begin(), ws.end());
   string::size_type pos = s.find_last_of("\\/");
   return s.substr(0, pos);
}