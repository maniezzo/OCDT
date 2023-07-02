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
   this->removed=false;
   this->hash   =-1;
   return;
}

AABB::~AABB() {  return; }

Bbox::Bbox()
{  vector<int> dummy;
   ptClass.push_back(dummy); // row 0
   ptClass.push_back(dummy); // row 1
   return;
}

//dtor
Bbox::~Bbox() {  return; }

// >>>>>>>>>>>>>>>>>>> main method, everythong starts from here <<<<<<<<<<<<<<<<<<<<<<<
int Bbox::bboxHeu(string fpath)
{  int i,dim,idx;
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
   removeNonParetian(domain);

   // box a set
   idx = 0; // index of incumbent record
   while (idx<Y.size()) // initialize box stack
   {  cout << "initializaing point " << idx << endl; 
      AABB box(ndim);
      initializeBox(idx,box,domain); // out is whole domain, in is the point
      AABBstack.push_back(box);
      idx++;
   }
   expandBox();

   cout << "n. box: " << AABBstack.size() << endl;
   writeHboxes();
   return 0;
}

// writes out the final boxes
void Bbox::writeHboxes()
{  int i,j,dim;
   vector<int> lstIdBox;  // list of undominated boxes

   for (i = 0; i < AABBstack.size(); i++)
   {  if(AABBstack[i].removed) continue;
      lstIdBox.push_back(i);
      cout << i << ") Box " << AABBstack[i].id << " class " << AABBstack[i].classe << " pts ";
      for(j=0;j<AABBstack[i].points.size();j++)
         cout << " " << AABBstack[i].points[j]; cout << endl;
      for(dim=0;dim<ndim;dim++)
         cout << setw(5) << AABBstack[i].loOut[dim] << 
                 setw(5) << AABBstack[i].loIn[dim]  <<
                 setw(5) << AABBstack[i].hiIn[dim]  <<
                 setw(5) << AABBstack[i].hiOut[dim] << endl;
   }
   writeFinals(lstIdBox);
}

void Bbox::writeFinals(vector<int> lstIdBox)
{  int i,j,k,dim;

   for (i = 0; i < lstIdBox.size(); i++)
      for (j = 0; j < lstIdBox.size(); j++) // è asimmetrico
         if(i!=j && !AABBstack[lstIdBox[j]].removed && !AABBstack[lstIdBox[i]].removed)
            if (std::includes(AABBstack[lstIdBox[i]].points.begin(), AABBstack[lstIdBox[i]].points.end(),
                AABBstack[lstIdBox[j]].points.begin(), AABBstack[lstIdBox[j]].points.end()))
            {  cout << "box " << i << " dominates box " << j << endl;
               AABBstack[lstIdBox[j]].removed = true;
            }
   for (j = 0; j < lstIdBox.size(); j++)
   {  i = lstIdBox[j];
      if (AABBstack[i].removed) continue;
      vector<double>minVal(ndim);
      vector<double>maxVal(ndim);
      cout << i << ") Box " << AABBstack[i].id << " class " << AABBstack[i].classe << " pts ";
      for (k = 0; k < AABBstack[i].points.size(); k++)
         cout << " " << AABBstack[i].points[k]; cout << endl;
      for (dim = 0; dim < ndim; dim++)
      {  cout << setw(5) << AABBstack[i].loOut[dim] <<
         setw(5) << AABBstack[i].loIn[dim] <<
         setw(5) << AABBstack[i].hiIn[dim] <<
         setw(5) << AABBstack[i].hiOut[dim] << endl;
         minVal[dim] = (AABBstack[i].loIn[dim] + AABBstack[i].loOut[dim])/2;
         maxVal[dim] = (AABBstack[i].hiIn[dim] + AABBstack[i].hiOut[dim])/2;
      }
      hbox hb = {minVal, maxVal};
      finalBoxes.push_back(hb);
   }

   // writing output file
   ofstream f("hyperboxes.txt");
   for (i = 0; i < finalBoxes.size(); i++)
   {
      f << "Hyperbox " << i << endl;
      for (dim = 0; dim < ndim; dim++) f << finalBoxes[i].min[dim] << " "; f << endl;
      for (dim = 0; dim < ndim; dim++) f << finalBoxes[i].max[dim] << " "; f << endl;
   }
   f.close();
}

// expands the stack box along all dimensions, first point was iSeed, starts from dimension d, inner loop from node idpt
void Bbox::expandBox()
{  int i,j,k,h,h1,dim,idx,iSeed;
   vector<int> pts;

   // expand stack, idx pointer to current
   idx=0;
   nb = AABBstack.size()-1;
   while(idx<AABBstack.size())
   {  AABB box = AABBstack[idx];
      iSeed = box.seed;
      cout << ">>>>> Expanding box " << box.id << " class " << Y[iSeed] << " seed " << iSeed << endl;
      for(i=0;i<n;i++) // check current box against all points
      {
         if(isInside(i, box, false)) // false: i confini out hanno punti non cluster
            if(Y[iSeed]==Y[i]) // same category (aka box.classe), expand in
            {  for (dim = 0; dim < this->ndim; dim++)
               {  if (X[i][dim] > box.hiIn[dim] && X[i][dim] < box.hiOut[dim]) // inner box cresce hi
                     box.hiIn[dim] = X[i][dim];
                  if (X[i][dim] < box.loIn[dim] && X[i][dim] > box.loOut[dim]) // inner box cala lo 
                     box.loIn[dim] = X[i][dim]; 
               }
               if (isInsideVec(i,box.loIn,box.hiIn, true) &&
                   find(box.points.begin(), box.points.end(), i) == box.points.end()) // punto i non è già incluso
                  box.points.push_back(i);
            }
            else  // different category, reduce out
            {  for (dim = 0; dim < ndim; dim++)
               {  pts = box.points;
                  // taglio out in basso
                  if (X[i][dim] > box.loOut[dim] && X[i][dim] < X[iSeed][dim])
                  {  cout << "------ box " << box.id <<" point " << i <<" dim "<<dim<< " Alzo il lo " << box.loOut[dim] << " -> " << X[i][dim] << endl; // taglio sotto
                     AABBstack[idx].removed = true;
                     AABB newBox(box);
                     nb++;
                     newBox.id = nb;
                     newBox.loOut[dim] = X[i][dim];
                     newBox.loIn[dim]  = X[iSeed][dim]; // ricalcolo i punti interni
                     newBox.points.clear();
                     for (j = 0; j < pts.size(); j++)
                     {  k = pts[j];
                        if (isInsideVec(k, newBox.loOut, newBox.hiOut, true))
                        {  //cout << k << " is inside lo" << endl;
                           if(X[k][dim] < newBox.loIn[dim])
                           {  cout << "loin dim "<< dim << " " << newBox.loIn[dim] << " -> " << X[k][dim] << endl;
                              newBox.loIn[dim] = X[k][dim];
                           }
                           newBox.points.push_back(k);
                        }
                     }
                     if (newBox.hiIn[dim] < newBox.loIn[dim])
                     {  cout << "ERROR hiin " << newBox.hiIn[dim] << " lo " << newBox.loIn[dim] << ", aborting ..." << endl;
                        abort();
                     }
                     if(!checkDominated(newBox))
                     {  AABBstack.push_back(newBox);
                        cout << "Queuing " << newBox.id << " removing " << idx << endl;
                     }
                  }
                  // taglio out in alto
                  if (X[i][dim] < box.hiOut[dim] && X[i][dim] > X[iSeed][dim])
                  {  cout << "++++++ box "<<box.id<< " point " << i << " dim " << dim << " Abbasso hi " << box.hiOut[dim] << " -> " << X[i][dim] << endl;  // abbasso sopra
                     AABBstack[idx].removed = true;
                     AABB newBox(box);
                     nb++;
                     newBox.id = nb;
                     newBox.hiOut[dim] = X[i][dim];
                     newBox.hiIn[dim]  = X[iSeed][dim]; // ricalcolo i punti interni
                     newBox.points.clear();
                     for (j = 0; j < pts.size(); j++)
                     {  k = pts[j];
                        if (isInsideVec(k, newBox.loOut, newBox.hiOut, false))
                        {  //cout << k << " is inside hi" << endl;
                           if (X[k][dim] > newBox.hiIn[dim])
                           {  cout << "hiin dim " << dim << " " << newBox.hiIn[dim] << " -> " << X[k][dim] << endl;
                              newBox.hiIn[dim] = X[k][dim];
                           }
                           newBox.points.push_back(k);
                        }
                     }
                     if (newBox.hiIn[dim] < newBox.loIn[dim])
                     {  cout << "ERROR hiin " << newBox.hiIn[dim] << " lo " << newBox.loIn[dim] << ", aborting ..." << endl;
                        abort();
                     }
                     if (!checkDominated(newBox))
                     {  AABBstack.push_back(newBox);
                        cout << "Queuing " << newBox.id << " removing " << idx << endl;
                     }
                  }
               }
            }  // else, different category
      }   // for i

      // base della ricorsione, keep box alive
      if(!AABBstack[idx].removed)
         if(!checkDominated(box))
         {  cout << "Confirming box " << box.id << endl;
            AABBstack[idx] = box; // should have sorted arrays
            hashtable[box.hash] = 1;  // cell is used
         }

      idx++; // expand next box in the stack
   }
}

// checks whether a box is already dominated
bool Bbox::checkDominated(AABB& box)
{  bool isDominated = false;
   int i,j,h,k,h1;

   sort(box.points.begin(), box.points.end());
   h = hash(box);
   box.hash = h;
   j = 0;
   while (j < AABBstack.size())
   {  if(AABBstack[j].removed) goto l0;
      h1 = AABBstack[j].hash;
      if (hashtable[h] != 0)
      {
         if (h1 == h)   // maybe the box is already there
            for (k = 0; k < ndim; k++)
               if (box.hiOut[k] == AABBstack[j].hiOut[k] &&
                  box.loOut[k] == AABBstack[j].loOut[k] &&
                  box.hiIn[k] == AABBstack[j].hiIn[k] &&
                  box.loIn[k] == AABBstack[j].loIn[k])
               {  cout << "Duplicate box: " << box.id << endl;
                  isDominated = true;
                  break;
               }
      }
      else  // forse contiene / è contenuto in qualcun altro
      {  bool fContained = true, fContaining = true;
         for (k = 0; k < ndim; k++)
         {  if (!(box.hiOut[k] <= AABBstack[j].hiOut[k] &&
               box.loOut[k] >= AABBstack[j].loOut[k] &&
               box.hiIn[k] <= AABBstack[j].hiIn[k] &&
               box.loIn[k] >= AABBstack[j].loIn[k]))
            {  fContained = false;
            }
            if (!(box.hiOut[k] >= AABBstack[j].hiOut[k] &&
               box.loOut[k] <= AABBstack[j].loOut[k] &&
               box.hiIn[k] >= AABBstack[j].hiIn[k] &&
               box.loIn[k] <= AABBstack[j].loIn[k]))
            {  fContaining = false;
            }
         }
         if (fContained)
         {  cout << "Box contained, " << box.id << " rejected" << endl;
            isDominated = true;
            break;
         }
         if (fContaining)
         {  cout << "Box containing, redefining box " << AABBstack[j].id << endl;
            AABBstack[j] = box;
            isDominated = true;
            break;
         }
      }
l0:   j++;
   }
   return isDominated;
}

// hash of box, fast check of duplicate boxes
int Bbox::hash(AABB& box)
{  int i,h,sum = 0;
   for(i=0;i<this->ndim;i++)
      sum += box.hiOut[i] * box.loOut[i] * box.hiIn[i] * box.loIn[i];
   h = (int)sum % this->m;
   //cout << " h=" << h << endl;
   return h;
}

// if a point is inside the outer box of an AABB, option on boundaries accepting
bool Bbox::isInside(int idx, AABB& box, bool fBoundariesIncluded)
{  int dim;
   bool isIn = false;
   for (dim = 0; dim < this->ndim; dim++)
      if(fBoundariesIncluded)
      {  if (X[idx][dim] > box.hiOut[dim]) goto l0; // non è dentro, confini dentro
         if (X[idx][dim] < box.loOut[dim]) goto l0;
      }
      else
      {  if (X[idx][dim] >= box.hiOut[dim]) goto l0; // non è dentro, confini fuori
         if (X[idx][dim] <= box.loOut[dim]) goto l0;
      }
   isIn = true;

l0:return isIn;
}

// if a point is inside a hyperrect given hi and lo coords, option on boundaries accepting
bool Bbox::isInsideVec(int i1, vector<double> lo, vector<double> hi, bool fBoundariesIncluded)
{  int dim;
   bool isIn = false;
   for (dim = 0; dim < this->ndim; dim++)
      if (fBoundariesIncluded)
      {  if (X[i1][dim] > hi[dim]) goto l0; // non è dentro, confini dentro
         if (X[i1][dim] < lo[dim]) goto l0;
      }
      else
      {  if (X[i1][dim] >= hi[dim]) goto l0; // non è dentro, confini fuori
         if (X[i1][dim] <= lo[dim]) goto l0;
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
   box.id = this->AABBstack.size();
   box.classe = Y[idx];
   box.seed   = idx;
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
         val.push_back(stof(elem[i]));
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

// removes points surronded by similar ones
void Bbox::removeNonParetian(hbox domain)
{  int i,ii,j,jj,dim,maxdim,mindim, idClass;
   bool isDominated;

   hbox base; // tutto nullo, per inizializzare e per confronto
   for (i = 0; i < ndim; i++)
   {  base.min.push_back(-1);
      base.max.push_back(-1);
   }

   for(idClass = 0; idClass <=1; idClass++)
      for(ii=0;ii<ptClass[idClass].size();ii++)
      {  isDominated = true;
         i = ptClass[idClass][ii];
         // same class bounding area
         hbox limitPoints = base;
         for (jj = 0; jj < ptClass[idClass].size(); jj++)
         {  // if j different on more dimensions, it is not aligned
            j = ptClass[idClass][jj];
            if(j==i) continue;
            mindim = -1;  // id punto tutte coordinate uguali e una più piccola (-1 non c'è, id c'è)
            maxdim = -1;  // id punto tutte coordinate uguali e una più grande
            for (dim = 0; dim < ndim; dim++)
            {  if ((X[j][dim] > X[i][dim]))
                  if(maxdim<0)
                     maxdim=dim;
                  else
                     maxdim=n+1;
               if ((X[j][dim] < X[i][dim]))
                  if(mindim<0)
                     mindim=dim;
                  else
                     mindim=n+1;
            }
            if (mindim < 0 && maxdim >= 0 && maxdim < ndim)
               limitPoints.max[maxdim] = j;
            if (maxdim < 0 && mindim >= 0 && mindim < ndim)
               limitPoints.min[mindim] = j;
            if (maxdim > n || mindim > n)
               continue; // j not aligned with i
         }

         // check if bounded on all sides
         isDominated = false;
         for (int d = 0; d < ndim; d++)
            if (limitPoints.min[d] < 0 || limitPoints.max[d] < 0)
               goto l0;
         isDominated = true;

         // check if opposite class point is inside
         for (jj = 0; jj < ptClass[1-idClass].size(); jj++)
         {  j = ptClass[1 - idClass][jj];
            for(dim=0;dim<ndim;dim++)
            {  // check whether they differ only in one dim
               for (int d = 0; d < ndim; d++)
                  if (d != dim && X[j][d] != X[i][d])
                     goto nextj;
               // check se j sotto i
               if(inBetween(limitPoints.min[dim], X[j][dim],X[i][dim]))
               {  isDominated = false;
                  goto l0;
               }
               // check se j sopra j
               if(inBetween(X[i][dim],X[j][dim], limitPoints.max[dim]))
               {  isDominated = false;
                  goto l0;
               }
            }
nextj:      continue;
         }
l0:      if(isDominated) cout << "point " << i << " to be removed" << endl;
      }
}

// not accepting domain bounds
bool Bbox::inBetween(double a, double b, double c)
{  bool isBetween = false;
   if (a < 0) isBetween = true;
   if (a >= 0 && a < b && b < c) isBetween = true;
   if (c >= 0 && a < b && b < c) isBetween = true;
   if (c < 0) isBetween = true;
   return isBetween;
}
