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
{  return;
}

//dtor
Bbox::~Bbox()
{  return;
}

// main method, everythong starts from here
int Bbox::bboxHeu(string fpath)
{  int i,j,dim,idx;
   read_data(fpath);

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
   while (idx<Y.size())
   {  
      AABB box(ndim);
      initializeBox(idx,box,domain); // out is whole domain, in is the point
      expandBox(idx,box,dim);
      idx++;
   }

   return 0;
}

// expands a box along all dimensions, starts from dimension d
void Bbox::expandBox(int idx, AABB& box, int d)
{  int i,j,k,dim;
   vector<int>* p;

   cout << idx << " dim " << d << endl;
   // check box against all points
   for(i=0;i<this->n;i++)
   {
      if(Y[idx]==Y[i] && isInside(i,box)) // same category, expand in
      {  for (dim = d; dim < this->ndim; dim++)
         {  if (X[i][dim] > box.hiIn[dim]) box.loIn[dim] = X[i][dim];
            if (X[i][dim] < box.loIn[dim]) box.loIn[dim] = X[i][dim];
         }
      }
      else  // different category, reduce out and fork
      {
         for (dim = d; dim < this->ndim; dim++)
         {
            if (X[i][dim] > box.loOut[dim])
            {  cout << "Alzo il lo" << endl; // taglio sotto
               if (Y[idx] == 0) p = &ind0;
               else             p = &ind1;
               box.loOut[dim] = X[i][dim];
               box.loIn[dim]  = X[idx][dim];
               for (j = 0; j < (*p).size(); j++)
                  if (isInside((*p)[j], box.hiOut, box.loOut))
                  {
                     cout << (*p)[j] << " is inside lo" << endl;
                     k = (*p)[j];
                     if(X[k][dim] < box.loIn[dim]) box.loIn[dim]= X[k][dim];
                  }
               if (dim < ndim - 1) expandBox(idx, box, dim + 1);
            }
            if (X[i][dim] < box.hiOut[dim])
            {  cout << "Abbasso hi" << endl;  // abbasso sopra
               if (Y[idx] == 0) p = &ind0;
               else             p = &ind1;
               box.loOut[dim] = X[i][dim];
               for (j = 0; j < (*p).size(); j++)
                  if (isInside((*p)[j], box.hiOut,box.loOut))
                  {  cout << (*p)[j] << " is inside hi" << endl;
                     k = (*p)[j];
                     if (X[k][dim] > box.hiIn[dim]) box.hiIn[dim] = X[k][dim];
                  }
               if(dim<ndim-1) expandBox(idx, box, dim+1);
            }
         }
         hboxes.push_back(box);
      }
   }
}

// if a point is inside the outer box of an AABB
bool Bbox::isInside(int idx, AABB box)
{  int dim;
   bool isIn = false;
   for (dim = 0; dim < this->ndim; dim++)
   {  if (X[idx][dim] > box.hiOut[dim]) goto l0;
      if (X[idx][dim] < box.loOut[dim]) goto l0;
   }
   isIn = true;

l0:return isIn;
}

// if a point is inside a hyperrect given hi and lo coords
bool Bbox::isInside(int idx, vector<double> lo, vector<double> hi)
{  int dim;
   bool isIn = false;
   for (dim = 0; dim < this->ndim; dim++)
   {  if (X[idx][dim] > hi[dim]) goto l0;
      if (X[idx][dim] < lo[dim]) goto l0;
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
      box.hiOut[dim] = domain.max[dim];
      box.loIn[dim] = X[idx][dim];
      box.loOut[dim] = domain.min[dim];
   }
   box.id = this->hboxes.size();
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
      if(j==0) ind0.push_back(stoi(elem[0]));
      else     ind1.push_back(stoi(elem[0]));
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
