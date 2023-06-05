#include "Bbox.h"
#include <windows.h>    // GetModuleFileName

// ctor
Bbox::Bbox()
{
   return;
}

//dtor
Bbox::~Bbox()
{
   return;
}

int Bbox::bboxHeu(string fpath)
{  int i,j,dim,idx;
   read_data(fpath);

   // find min and max coords
   AABB domain;
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
   {  if(idx != n) idx++;
      continue;

      AABB box;
      setBox(box);

      idx++;
   }

   return 0;
}

void Bbox::setBox(AABB box)
{

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
