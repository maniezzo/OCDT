#include "MIPmodel.h"

void MIPmodel::run_MIP(string testSet)
{
   cout << "run MIP, testset " << testSet << endl;
}

void MIPmodel::readData(string fpath)
{  int i, j, cont, id;
   double v;
   string s, line;
   vector<string> elem;
   vector<float> val;

   ifstream f;
   f.open(fpath, ios::in);
   if (!f)
      cout << "Could not open file" << endl;
   else
   {
      getline(f, line);
      elem = split(line, ',');
   }
   cout << "READING ONLY PARTIAL DATASET" << endl;
   cont = 0;
   while (getline(f, line))
   {  //read data from file object and put it into string.
      val.clear();
      elem = split(line, ',');
      id = stoi(elem[0]);
      //if (id > 40 && !(id > 100 && id < 141)) goto l0;
      cout << id << endl;
   l0:   cont++;
   }
   f.close();
}


// split di una stringa in un array di elementi delimitati da separatori
vector<string> MIPmodel::split(string str, char sep)
{  vector<string> tokens;
   size_t start;
   size_t end = 0;
   while ((start = str.find_first_not_of(sep, end)) != std::string::npos) {
      end = str.find(sep, start);
      tokens.push_back(str.substr(start, end - start));
   }
   return tokens;
}
