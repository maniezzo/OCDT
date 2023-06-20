#include "Bbox.h"
#include <windows.h>    // GetModuleFileName

// ctors
AABB::AABB(int ndim)
{  int i;
   for (i = 0; i < ndim; i++)
   {  this->hiIn.push_back(DBL_MIN);
      this->hiOut.push_back(DBL_MIN);
      this->loIn.push_back(DBL_MAX);
      this->loOut.push_back(DBL_MAX);
   }
   return;
}

AABB::~AABB()
{  return;
}

Bbox::Bbox()
{  vector<int> dummy;
   ptClass.push_back(dummy); // row 0
   ptClass.push_back(dummy); // row 1
   return;
}

//dtor
Bbox::~Bbox()
{  return;
}

// >>>>>>>>>>>>>>>>>>> main method, everythong starts from here <<<<<<<<<<<<<<<<<<<<<<<
int Bbox::bboxHeu(string fpath)
{  int i,j,dim,idx;
   read_data(fpath);

   nb = 0;     // box counter
   m = 10000;  // hash table size
   hashtable.resize(m);
   fill(hashtable.begin(), hashtable.end(), 0);

   // find min and max coords of the domain
   hbox domain;
   for (dim = 0; dim < ndim; dim++)
   {  domain.min.push_back(X[0][dim]);
      domain.max.push_back(X[0][dim]);
      for (i = 0; i < n; i++)
      {  if (X[i][dim] < domain.min[dim]) domain.min[dim] = X[i][dim];
         if (X[i][dim] > domain.max[dim]) domain.max[dim] = X[i][dim];
      }
   }

   // box a set
   dim = 0; // the class (dimension) under study
   idx = 0; // index of incumbent record
   while (idx<Y.size()) // expand each point along every dimension
   {  cout << "-> -> -> -> -> -> -> -> -> initializaing point " << idx << endl; 
      AABB box(ndim);
      initializeBox(idx,box,domain); // out is whole domain, in is the point
      expandBox(idx,box,dim,0);
      idx++;
   }

   cout << "n. box: " << hboxes.size() << endl;
   writeHboxes();
   return 0;
}

// writes out the final boxes
void Bbox::writeHboxes()
{  int i,j,dim;
   for (i = 0; i < hboxes.size(); i++)
   {
      cout << i << ") Box " << hboxes[i].id << " class " << hboxes[i].classe << " pts ";
      for(j=0;j<hboxes[i].points.size();j++)
         cout << " " << hboxes[i].points[j]; cout << endl;
      for(dim=0;dim<ndim;dim++)
         cout << setw(5) << hboxes[i].loOut[dim] << 
                 setw(5) << hboxes[i].loIn[dim]  <<
                 setw(5) << hboxes[i].hiIn[dim]  <<
                 setw(5) << hboxes[i].hiOut[dim] << endl;
   }
}

// expands a box along all dimensions, starts from dimension d, inner loop from node idpt
void Bbox::expandBox(int idx, AABB& box, int d, int idpt)
{  int i,i1,j,k,h,h1,dim;
   vector<int> pts;
   bool changed;

   nb++;
   box.id = nb;
   cout << ">>>>> Box " << box.id << " class " << Y[idx] << " seed " << idx << " dim " << d << endl;
   // check box against all points
   for(i=idpt;i<n;i++)
   {
      if(isInside(i, box))
         if(Y[idx]==Y[i]) // same category, expand in
         {  for (dim = 0; dim < this->ndim; dim++)
            {  if (X[i][dim] > box.hiIn[dim] && X[i][dim] < box.hiOut[dim]) 
                  box.hiIn[dim] = X[i][dim];
               if (X[i][dim] < box.loIn[dim] && X[i][dim] > box.loOut[dim]) 
                  box.loIn[dim] = X[i][dim]; 
            }
            if (isInsideVec(i,box.loIn,box.hiIn) &&
                find(box.points.begin(), box.points.end(), i) == box.points.end())
               box.points.push_back(i);
         }
         else  // different category, reduce out and fork
         {  for (dim = d; dim < this->ndim; dim++)
            {  
               if (X[i][dim] > box.loOut[dim] && X[i][dim] < X[idx][dim])
               {  cout << "------ box " << box.id <<" point " << i << " Alzo il lo " << box.loOut[dim] << " -> " << X[i][dim] << endl; // taglio sotto
                  box.loOut[dim] = X[i][dim];
                  box.loIn[dim]  = X[idx][dim]; // reinizializzo i punti nterni
                  pts = box.points;
                  box.points.clear();
                  for (j = 0; j < pts.size(); j++)
                  {  k = pts[j];
                     if (isInsideVec(k, box.loOut, box.hiOut))
                     {  cout << k << " is inside lo" << endl;
                        if(X[k][dim] < box.loIn[dim])
                        {  cout << "loin dim "<< dim << " " << box.loIn[dim] << " -> " << X[k][dim] << endl;
                           box.loIn[dim] = X[k][dim];
                        }
                        box.points.push_back(k);
                     }
                  }
                  expandBox(idx, box, dim, i+1);
               }

               if (X[i][dim] < box.hiOut[dim] && X[i][dim] > X[idx][dim])
               {  cout << "++++++ box "<<box.id<< " point " << i << " Abbasso hi " << box.hiOut[dim] << " -> " << X[i][dim] << endl;  // abbasso sopra
                  box.hiOut[dim] = X[i][dim];
                  box.hiIn[dim]  = X[idx][dim]; // reinizializzo i punti nterni
                  pts = box.points;
                  box.points.clear();
                  for (j = 0; j < pts.size(); j++)
                  {  k = pts[j];
                     if (isInsideVec(k, box.loOut, box.hiOut))
                     {  cout << k << " is inside hi" << endl;
                        if (X[k][dim] > box.hiIn[dim])
                        {  cout << "hiin dim " << dim << " " << box.hiIn[dim] << " -> " << X[k][dim] << endl;
                           box.hiIn[dim] = X[k][dim];
                        }
                        box.points.push_back(k);
                     }
                  }
                  expandBox(idx, box, dim, i+1);
               }

               if (box.hiIn[dim] < box.loIn[dim])
                  cout << "ERROR hiin " << box.hiIn[dim] << " lo " << box.loIn[dim] << endl;
            }
         }
   }

   // base della ricorsione, add box to hboxes list
   if (d <= ndim)
   {  sort(box.loOut.begin(), box.loOut.end());
      sort(box.loIn.begin(), box.loIn.end());
      sort(box.hiIn.begin(), box.hiIn.end());
      sort(box.hiOut.begin(), box.hiOut.end());
      h = hash(box);
      j=0;
      while (j < hboxes.size())
      {  h1 = hash(hboxes[j]);
         if (hashtable[h] != 0)
         {  if (h1 == h)   // maybe the box is already there
               for (k = 0; k < ndim; k++)
                  if (box.hiOut[k] == hboxes[j].hiOut[k] &&
                     box.loOut[k] == hboxes[j].loOut[k] &&
                     box.hiIn[k] == hboxes[j].hiIn[k] &&
                     box.loIn[k] == hboxes[j].loIn[k])
                  {  cout << "Duplicate box" << endl;
                     return;
                  }
         }
         else  // forse contiene / è contenuto in qualcun altro
         {
            for (k = 0; k < ndim; k++)
               if (box.hiOut[k] <= hboxes[j].hiOut[k] &&
                  box.loOut[k] >= hboxes[j].loOut[k] &&
                  box.hiIn[k] <= hboxes[j].hiIn[k] &&
                  box.loIn[k] >= hboxes[j].loIn[k])
               {  cout << "Box contained, rejected" << endl;
                  return;
               }
               else
                  if (box.hiOut[k] >= hboxes[j].hiOut[k] &&
                     box.loOut[k] <= hboxes[j].loOut[k] &&
                     box.hiIn[k] >= hboxes[j].hiIn[k] &&
                     box.loIn[k] <= hboxes[j].loIn[k])
                  {  cout << "Box containing" << endl;
                     hboxes.erase(hboxes.begin() + j);
                     j--;
                  }
         }
         j++;
      }
      cout << "Adding box " << box.id << endl;
      hboxes.push_back(box);
      hashtable[h] = 1;  // cell is used
      return;
   }
   else
      // niente da cambiare in questa dimensione
      if (d < ndim) expandBox(idx, box, d + 1,0);
}

// hash of box, fast check of duplicate boxes
int Bbox::hash(AABB box)
{  int i,m,h,sum = 0;
   for(i=0;i<this->ndim;i++)
      sum += box.hiOut[i] * box.loOut[i] * box.hiIn[i] * box.loIn[i];
   h = (int)sum % this->m;
   //cout << " h=" << h << endl;
   return h;
}

// if a point is inside the outer box of an AABB
bool Bbox::isInside(int idx, AABB box)
{  int dim;
   bool isIn = false;
   for (dim = 0; dim < this->ndim; dim++)
   {  if (X[idx][dim] > box.hiOut[dim]) goto l0; // non è dentro
      if (X[idx][dim] < box.loOut[dim]) goto l0;
   }
   isIn = true;

l0:return isIn;
}

// if a point is inside a hyperrect given hi and lo coords
bool Bbox::isInsideVec(int i1, vector<double> lo, vector<double> hi)
{  int dim;
   bool isIn = false;
   for (dim = 0; dim < this->ndim; dim++)
   {  if (X[i1][dim] > hi[dim]) goto l0; // non è dentro
      if (X[i1][dim] < lo[dim]) goto l0;
   }
   isIn = true;

l0:return isIn;
}

// initializes a new AABB
void Bbox::initializeBox(int idx, AABB& box, hbox domain)
{
   for (int dim = 0; dim < this->ndim; dim++)
   {
      box.hiIn[dim] = X[idx][dim];
      box.hiOut[dim] = domain.max[dim]+1; // sennò un punto non sta dentro il suo box
      box.loIn[dim] = X[idx][dim];
      box.loOut[dim] = domain.min[dim]-1;
   }
   box.id = this->hboxes.size();
   box.classe = Y[idx];
   box.points.push_back(idx);
}

void Bbox::read_data(string fpath)
{  int i, j;
   string s, line;
   vector<string> elem;

   cout << "Running from " << ExePath() << endl;
   ifstream f;
   f.open(fpath, ios::in);
   if (!f)
      cout << "Could not open file" << endl;
   else
   {
      getline(f, line);
      elem = split(line, ',');
      ndim = elem.size() - 2;
   }
   while (getline(f, line)) 
   {  //read data from file object and put it into string.
      elem = split(line, ',');
      cout << elem[0] << endl; //print the data of the string
      vector<double> val;
      for (i = 1; i < 1 + ndim; i++)
         val.push_back(stoi(elem[i]));
      X.push_back(val);
      j = stoi(elem[ndim + 1]);
      Y.push_back(j);
      if(j==0) ptClass[0].push_back(stoi(elem[0]));
      else     ptClass[1].push_back(stoi(elem[0]));
      
   }
   f.close();
   n = Y.size();  // number of input records
}

// trova il path del direttorio da cui si e' lanciato l'eseguibile
string Bbox::ExePath()
{
   wchar_t buffer[MAX_PATH];
   GetModuleFileName(NULL, buffer, MAX_PATH);
   wstring ws(buffer);
   string s = string(ws.begin(), ws.end());
   string::size_type pos = s.find_last_of("\\/");
   return s.substr(0, pos);
}

// split di una stringa in un array di elementi delimitati da separatori
vector<string> Bbox::split(string str, char sep)
{  vector<string> tokens;
   size_t start;
   size_t end = 0;
   while ((start = str.find_first_not_of(sep, end)) != std::string::npos) {
      end = str.find(sep, start);
      tokens.push_back(str.substr(start, end - start));
   }
   return tokens;
}
