#include <iostream> // console
#include <fstream>  // file
#include <sstream>  // stringa
#include <vector>
#include <queue>
#include <string>
#include <algorithm> // count
using namespace std;

/*
Compiles a set of if-then rules into a decision tree
*/

// >> https://cp-algorithms.com/graph/min_cost_flow.html

struct Edge
{  int from, to, capacity, cost;
};

const int INF = 1e9;
vector<vector<int>> adj, cost, flows, capacity;

///////////////// MFMC bellman - ford /////////////////////////
void shortest_paths(int n, int v0, vector<int>& d, vector<int>& p) {
    d.assign(n, INF);
    d[v0] = 0;
    vector<bool> inq(n, false);
    queue<int> q;
    q.push(v0);
    p.assign(n, -1);

    while (!q.empty()) {
        int u = q.front();
        q.pop();
        inq[u] = false;
        for (int v : adj[u]) {
            if (capacity[u][v] > 0 && d[v] > d[u] + cost[u][v]) {
                d[v] = d[u] + cost[u][v];
                p[v] = u;
                if (!inq[v]) {
                    inq[v] = true;
                    q.push(v);
                }
            }
        }
    }
}

int min_cost_flow(int N, vector<Edge>& edges, int K, int s, int t, vector<int>& arcs) 
{  int i,j,k;
    adj.assign(N, vector<int>());
    cost.assign(N, vector<int>(N, 0));
    flows.assign(N, vector<int>(N, 0));
    capacity.assign(N, vector<int>(N, 0));
    for (Edge e : edges) {
        adj[e.from].push_back(e.to);
        adj[e.to].push_back(e.from);
        cost[e.from][e.to] = e.cost;
        cost[e.to][e.from] = -e.cost;
        flows[e.to][e.from] = 0;
        capacity[e.from][e.to] = e.capacity;
    }

    int flow = 0;
    int cost = 0;
    vector<int> d, p;
    while (flow < K) {
        shortest_paths(N, s, d, p);
        if (d[t] == INF)
            break;

        // find max flow on that path
        int f = K - flow;
        int cur = t;
        while (cur != s) {
            f = min(f, capacity[p[cur]][cur]);
            cur = p[cur];
        }

        // apply flow
        flow += f;
        cost += f * d[t];
        cur = t;
        while (cur != s) {
            capacity[p[cur]][cur] -= f;
            capacity[cur][p[cur]] += f;
            flows[p[cur]][cur]    += f;
            cur = p[cur];
        }
    }
   for(i=0;i<N;i++)
      for(j=0;j<N;j++)
         if(flows[i][j]>0)
         {  for(k=0;k<edges.size();k++)
               if(edges[k].from==i && edges[k].to==j)
               {  cout << "Arc "<<i<<"-"<<j<<" flow "<<flows[i][j]<<" arc "<<k<<endl;
                  arcs.push_back(k);
                  break;
               }
         }

   if (flow < K)
      return -1;
   else
      return cost;
}

void printODT(int n, vector<int> arcs)
{  int i,j;
   ofstream outFile;
   outFile.open ("graph.txt");
   outFile << "digraph G {\n";

   for(i=0;i<n;i++)
      outFile << i << " [label=\"Type "<<i<<"\nPippo\"]"<< endl;

   for(i=0;i<arcs.size()-1;i++)
      for(j=i+1;j<arcs.size();j++)
         outFile << arcs[i] << " -> " << arcs[j] << " [style=bold,label=\" "<<i<<"-"<<j<<"\"]" << endl;

   outFile << "}\n";
   outFile.close();
}

vector<string> split (const std::string &s, char delim) {
    vector<string> result;
    stringstream ss (s);
    string item;

    while (getline (ss, item, delim)) {
        result.push_back (item);
    }
    return result;
}

// reads the boundaries to impose to hyperboxes 
void readBoundaries(vector<vector<int>>& cuts, vector<int>& lstCuts)
{  int i,j;
   string line;

   ifstream infile ("boxcuts.txt");
   vector<string> elem;
   if (infile.is_open())
   {  while ( getline(infile,line) )
      {  cout << line << '\n';
         vector<int> linecuts;
         elem = split(line,' ');
         for(i=1;i<elem.size();i++)
         {  j = stoi(elem[i]);
            linecuts.push_back(j);
            if(count(lstCuts.begin(),lstCuts.end(),j)==0)
               lstCuts.push_back(j);
         }
         cuts.push_back(linecuts);
      }
      infile.close();
   }
   else cout << "Unable to open boxcuts file"; 
}

void makeGraph(vector<Edge>& edges, int& nunEdges, vector<vector<int>> cuts, vector<int> lstCuts, vector<int>& nodes)
{  int i,j,maxcap=1000;
   nunEdges = 0;
   Edge e;
   nodes.push_back(0); // supersource
   // init: source to cuts
   for(i=0;i<lstCuts.size();i++)
   {
      e = {0,i+1,1,maxcap};
      edges.push_back(e);
      nunEdges++;
      nodes.push_back(i+1);
   }
   
   // close, all to supersink
   for(i=0;i<lstCuts.size();i++)
   {
      e = {1+i,4,1,maxcap};
      edges.push_back(e);
      nunEdges++;
   }
   nodes.push_back(nodes.size());  // the supersink
}

int main()
{  int i,j;
   vector<int> arcs;

   cout << "inizio"<<endl;
   int n;  // num nodes
   int k;  // the flow
   int s;  // source
   int t;  // destination
   vector<Edge> edges;
   vector<int> nodes;
   vector<int> lstCuts;  // list of all identified cuts
   vector<vector<int>> cuts;  // boxes and separating cuts

   readBoundaries(cuts,lstCuts);
   makeGraph(edges,n,cuts,lstCuts,nodes);

   k=2;  // test
   s = 0;  // source
   t = 4;  // destination
   int cost = min_cost_flow(nodes.size(),edges,k,s,t,arcs);
   cout << "fine, cost= "<<cost<<endl;

   printODT(n,arcs);
   return 0;
}