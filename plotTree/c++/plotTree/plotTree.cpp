#include "plotTree.h"
#include <windows.h>    // GetModuleFileName
#include <stack>
#include <algorithm>    // remove, erase
#include "json.h"

void Tree::goTree()
{  vector<int> dummy;
   ptClass.push_back(dummy); // row 0, class 0 points
   ptClass.push_back(dummy); // row 1, class 1 points
   ptClass.push_back(dummy); // row 2, class 2 points, if any

   string line, dataFileName = "test1";
   vector<string> elem;

   ifstream fconf("config.json");
   stringstream buffer;
   buffer << fconf.rdbuf();
   line = buffer.str();
   json::Value JSV = json::Deserialize(line);
   dataFileName = JSV["datafile"];
   cout << dataFileName << endl;;

   readData(dataFileName);
   if (ncuts > 60) // troppi cut, non stanno in un long, niente bitmask
   {  cout << "Too many cuts, exiting" << endl;
      exit(1);
   }
   regionBitmasks();  // bitmasks, identificatori bitmask delle regioni del dominio
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
   for(i=0;i<v.size();i++) v[i]=i; // i punti associati al nodo radice (tutti)
   nodePoints.push_back(v);
   newNode(s,0);
   decTree[0].idNodePoints = 0;
   cutBM |= (1 << decTree[0].idCut);  // mette a 1 il *-esimo bit DA DESTRA nell'identificatore bitmask del cut associato al nodo
   si = {s,cutBM };   // id nodo e cut associato
   stack.push(si);

   while (!stack.empty())
   {
      // Pop a vertex from stack 
      si = stack.top();
      stack.pop();
      s          = si.idnode;
      cutBitMask = si.bitMaskCuts;
      cout << "Pop node " << s << endl;

      // Stack may contain same vertex twice. So
      // we need to print the popped item only if it is not visited.
      if (!decTree[s].visited)
      {  cout << "Expanding node " << s << " num.points " << nodePoints[s].size() << endl;
         decTree[s].visited = true;

         if(!sameClass(s)) // se punti non della stessa classe genero due figli
         {  // split node points

            pointsLeftSon(s);  // lista leftPoints, punti a sinistra del cut del nodo, saranno nel figlio sx
            int l = decTree[s].left;  // id del figlio sinistro (ancora da riempire)
            if (l>0 && (decTree.size() <= l || !decTree[l].visited))
            {  newNode(l, cutBitMask);
               if(decTree[l].idCut >= 0) // internl points need to be cut
               {  cutBM = cutBitMask;
                  cutBM |= (1 << decTree[l].idCut);  // mette a 1 il *-esimo bit DA DESTRA
               }
               else
                  cutBM = -1; // will be leaf
               si = {l,cutBM};
               stack.push(si);
               cout << "Push node " << l << endl;
            }

            pointsRightSon(s); // lista rightPoints, punti a destra del cut del nodo, saranno nel figlio dx
            int r = decTree[s].right;  // id del figlio destro (ancora da riempire)
            if (r>0 && (decTree.size() <= r || !decTree[r].visited))
            {  newNode(r, cutBitMask);
               if (decTree[r].idCut >= 0) // internl points need to be cut
               {  cutBM = cutBitMask;
                  cutBM |= (1 << decTree[r].idCut);  // mette a 1 il *-esimo bit DA DESTRA
               }
               else
                  cutBM = -1; // will be leaf
               si = {r,cutBM};
               stack.push(si);
               cout << "Push node " << r << endl;
            }
         }
         else // leaf node, all points of the same class
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
   vector<vector<vector<int>>> freq (ncuts, vector<vector<int>>(2,vector<int>(nclasses,0))); // 3D: ncuts, region bit, class
   
   // contingency table (num regions per cut, per attr. value (above/below cut), per class
   for (i = 0; i < bitMaskRegion.size(); i++)     // for each bitmask (region)
   {  ptClass = Y[clusters[bitMaskRegion[i]][0]]; // class of the region. Bitmasks encode regions
      for (j = 0; j < ncuts; j++)
      {  //dim = cutlines[j].dim;
         if(bitMaskRegion[i]&(1 << j)) // region bitmask (NOT CUT)
            freq[j][1][ptClass]++;
         else
            freq[j][0][ptClass]++;
      }
   }
   defineNode(freq,idnode, cutBitMask);
}

// puts node in the decision tree
void Tree::defineNode(vector<vector<vector<int>>> freq, int idnode, int cutBitMask)
{  int i,j,k,minval = INT_MAX;
   int idCut = -1, idm = -1;
   double sum;
   double h,maxh = -1, minh=DBL_MAX;
   bool isSameCLass = false;

   if(sameClass(idnode)) // tutti i punti della stessa classe
   {  isSameCLass = true;
      goto l0;
   }

   // contingency table e taglio di entropia massima/minima
   // cerco il cut che sparpaglia di più/meno i punti nelle varie regioni
   for (i = 0; i < ncuts; i++)
   {
      bool is_set = (cutBitMask & (1 << i)) != 0; // check if i-th bit is set
      if (is_set)
      {  h = -1;   // this cut has already been used, discard from consideration
         continue;
      }
      
      if(minval > 0) // mettere sameclass
      {  sum = 0;
         for(j=0;j<2;j++)
            for (k = 0; k < nclasses; k++)
               sum += freq[i][j][k];
         h = 0;   // entropia del cut
         for (j = 0; j < 2; j++)
            for(k=0;k<nclasses;k++)
               h += -(freq[i][j][k] / sum) * (freq[i][j][k]>0 ? log(freq[i][j][k]) : 0) / sum;
      }
      else
         h = DBL_MAX;

      if (h > maxh)
      {  maxh = h;
         idm = i; // cut di entropia massima
      }

      if (h < minh && i<0)  // disabled
      {  minh = h;
         idm = i; // cut di entropia minima
      }
   }

l0:Node N;
   N.id = decTree.size();
   N.idCut    = (isSameCLass ? -1 : idm);
   N.cutDim   = (isSameCLass ? -1 : cutlines[idm].dim);
   N.cutValue = (isSameCLass ? -1 : cutlines[idm].cutval);
   N.left     = -1;
   N.right    = -1;
   N.visited  = false;
   N.idClass  = (isSameCLass ? Y[nodePoints[idnode][0]] : INT_MIN);
   N.idNodePoints  = nodePoints.size() -1;
   decTree[idnode] = (N);
}

// points smaller than cut, sets parent left pointer
void Tree::pointsLeftSon(int idnode)
{  int i,inp;
   vector<int> leftpoints;
   Node* N = &decTree[idnode]; // parent node
   inp = N->idNodePoints;
   for (i = 0; i < nodePoints[inp].size(); i++)
      if (X[nodePoints[inp][i]][N->cutDim] < N->cutValue)
         leftpoints.push_back(nodePoints[inp][i]);

   if (leftpoints.size() > 0 && leftpoints.size() < nodePoints[inp].size())
   {  nodePoints.push_back(leftpoints);
      N->left = nodePoints.size() - 1;
   }
}

// points bigger than cut, sets parent right pointer
void Tree::pointsRightSon(int idnode)
{  int i,inp;
   vector<int> rightpoints;
   Node* N = &decTree[idnode]; // parent node
   inp = N->idNodePoints;
   for (i = 0; i < nodePoints[inp].size(); i++)
      if (X[nodePoints[inp][i]][N->cutDim] > N->cutValue)
         rightpoints.push_back(nodePoints[inp][i]);

   if (rightpoints.size() > 0 && rightpoints.size() < nodePoints[inp].size())
   {  nodePoints.push_back(rightpoints);
      N->right = nodePoints.size() - 1;
   }
}

// bitmask identifier of all domain partitions, 0/1, below or above each cut
void Tree::regionBitmasks()
{  int i,j,dim;
   double val;
   unsigned long bitmask;

   for(i=0;i<n;i++) // chech there are no empty regions (for each point, where it lays)
   {  bitmask=0;
      for(j=0;j<ncuts;j++)
      {  dim = cutlines[j].dim;
         val = cutlines[j].cutval;
         if(X[i][dim]>val)
            bitmask |= (1 << j);  // mette a 1 il j-esimo bit da destra (i cut sarammo da dx a sx !!!!)
      }
      myCluster.push_back(bitmask);   // cluster in cui cade il punto i
      clusters[bitmask].push_back(i); // punti dentro ogni cluster
      if(find(bitMaskRegion.begin(), bitMaskRegion.end(), bitmask) == bitMaskRegion.end()) // se non c'è già'
         bitMaskRegion.push_back(bitmask); // aggiungi bitmask dei cluster
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
   ifstream fin("..//..//..//data//" + dataFileName + "_cuts.json");
   stringstream buffer;
   buffer << fin.rdbuf();
   line = buffer.str();
   json::Value JSV  = json::Deserialize(line);
   json::Array fdim = (json::Array)JSV["dim"];
   json::Array fpos = (json::Array)JSV["pos"];

   for(cont = 0;cont<fdim.size();cont++)
   {  Cutline c;
      c.dim = fdim[cont];      // dimensione in cui agisce il taglio
      c.cutval = fpos[cont];   // posizione del taglio
      cutlines[cont] = c;
   }
   ncuts = cont;

   // leggo i punti
   ifstream f;
   string dataSetFile = "..//..//..//data//" + dataFileName + ".csv";
   f.open(dataSetFile);
   if (f.is_open())
   {  getline(f, line);  // headers
      elem = split(line, ',');
      ndim = elem.size() - 2;
      nclasses = 0;

      while (getline(f, line))
      {  //read data from file object and put it into string.
         cont = 0;
         val.clear();
         elem = split(line, ',');
         id   = stoi(elem[0]);
         //if (id > 40 && !(id > 100 && id < 141)) goto l0;
         cout << "Read node " << id << endl;
         for (i = 1; i < 1 + ndim; i++)         
            if(dataFileName != "iris_setosa") // || (i==2 || i==3))  // FILTERING DATA for iris_setosa
            {  d = stof(elem[i]);
               //d = round(100.0 * d) / 100.0;     // rounded to 2nd decimal
               val.push_back(d);
            }
         X.push_back(val);
         j = stoi(elem[ndim + 1]);
         Y.push_back(j);
         if(j>(nclasses-1)) nclasses = j+1;
         ptClass[j].push_back(Y.size() - 1); // starts at 0
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
