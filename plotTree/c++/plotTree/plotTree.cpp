#include "plotTree.h"
#include <windows.h>    // GetModuleFileName

void Tree::goTree()
{  vector<int> dummy;
   ptClass.push_back(dummy); // row 0
   ptClass.push_back(dummy); // row 1

   string dataSetFile = "..//..//..//data//test1.csv";
   readData(dataSetFile);
}

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
   {  while (getline(f, line))
      {  cout << line << endl;
         elem = split(line, ' ');
         i = atoi(elem[1].c_str());
         dim.push_back(i); // dimensione in cui agisce il taglio
         d = atof(elem[3].c_str());
         cutval.push_back(d); // posizione del taglio
      }
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