import numpy as np
import pandas as pd
from sklearn.datasets import make_blobs
import matplotlib.pyplot as plt
from scipy.spatial import ConvexHull, convex_hull_plot_2d
from mpl_toolkits.mplot3d.art3d import Poly3DCollection, Line3DCollection # comes with matplotlib
#from mpl_toolkits.mplot3d import Axes3D

# Nearest neighbor expansion
from scipy.spatial import KDTree

# create dataset using make_blobs - assign centers, standard deviation and the number of points
def generate_clusters():
   # center points for the clusters
   centers = [[0, 1, 0], [2, 2, 2]]
   # standard deviations for the clusters
   stds = [0.8, 0.9]
   npoints = 100
   # X : coords, labels_true : which cluster the point belongs to
   X, labels_true = make_blobs(n_samples=npoints, centers=centers, cluster_std=stds, random_state=0)
   return X, labels_true

def init_hull(cluster, seed):
   hull = ConvexHull(cluster, incremental=True)
   kdtree = KDTree(clus[0:10])
   dpoint, idpoint = kdtree.query(c0[11:20]) # distance, index of closest point
   print(f"Distance is {dpoint}, point is {idpoint}")
   count_arr = np.bincount(idpoint)
   return hull

# checks whether s point is in a hull
# The hull is defined as all points x for which Ax + b <= 0.
# We compare to eps to account for floating point issues.
# Assuming pnt is shape (m, d), output is boolean shape (m,).
def contained(pnt, eqn):
   # A is shape (f, d) and b is shape (f, 1).
   A, b = eqn[:, :-1], eqn[:, -1:]
   eps = np.finfo(np.float32).eps
   return np.all(np.asarray(pnt) @ A.T + b.T < eps, axis=1)

# bounding box già calcolati
def bbox3d():
   f = open("..\\..\\hyperbox\\c++\\hyperboxes_"+dataset+".txt", "r")
   numBox = 0
   for line in f:
      elem = line.strip().split()
      if(elem[0]== "Hyperbox"):
         numBox += 1
         idClass = int(elem[3])
         idLine = 0 # della tripletta di valori
      elif (idLine==0):
         coordmin = [float(x) for x in elem] # from string to float
         idLine = 1
      elif (idLine==1):
         coordmax = [float(x) for x in elem]
         hboxes.append([coordmin,coordmax,idClass])
         idLine = 2 # inutile
   print(f"Read {numBox} hyperboxes")
   f.close()
   return

def plotBboxes(list_colours):
   x, y, z = 0, 1, 2 # dimensinos to plot
   fig = plt.figure()
   ax = fig.add_subplot(111, projection='3d')

   scatter_plot = ax.scatter3D(xp, yp, zp, c=cluster_colors, marker='o')
   for i in np.arange(len(xp)):
      ax.text(xp[i],yp[i],zp[i],i)

   for i in np.arange(len(hboxes)):
      a = hboxes[i][0]  # min coords
      b = hboxes[i][1]  # max coords
      idClass = hboxes[i][2] # hbox class

      vertices = [
         # vertices of XZ 2 faces
         [(a[x], a[y], a[z]), (b[x], a[y], a[z]), (b[x], a[y], b[z]), (a[x], a[y], b[z])],
         [(a[x], b[y], a[z]), (b[x], b[y], a[z]), (b[x], b[y], b[z]), (a[x], b[y], b[z])],
         # vertices of YZ 2 faces
         [(a[x], a[y], a[z]), (a[x], b[y], a[z]), (a[x], b[y], b[z]), (a[x], a[y], b[z])],
         [(b[x], a[y], a[z]), (b[x], b[y], a[z]), (b[x], b[y], b[z]), (b[x], a[y], b[z])],
         # vertices of XY 2 faces
         [(a[x], a[y], a[z]), (b[x], a[y], a[z]), (b[x], b[y], a[z]), (a[x], b[y], a[z])],
         [(a[x], a[y], b[z]), (b[x], a[y], b[z]), (b[x], b[y], b[z]), (a[x], b[y], b[z])],
      ]

      ax.plot([a[x], b[x]], [a[y], b[y]], [a[z], b[z]], '-r', alpha=0) # blank, but could not work without
      ax.add_collection3d(Poly3DCollection(
         vertices, facecolors=list_colours[idClass], linewidths=1, edgecolors='k', alpha=.1))

   plt.show()
   return
def checkUnique(toRemove,toAdapt):
   # checks for duplicated rows in the dataset
   values = np.unique(df.iloc[:, 1:-1].values, axis=0)  # check for duplicate rows
   if(len(df) != len(values)):
      print(f"Duplicated rows! num {len(df) - len(values)}")
   else:
      print("No duplicated rows")
   for i1 in range(len(df)-1):
      for i2 in range(i1+1,len(df)):
         isEqual = True
         for d in range(1,len(df.columns)-1):
            if (df.iloc[i1,d] != df.iloc[i2,d]):
               isEqual = False
               break
         if(isEqual):
            if(df.iloc[i1,-1] == df.iloc[i2,-1]):
               toRemove[i2] = 1;
            else:
               toAdapt[i1] = 1;
               toAdapt[i2] = 1;
               print(f"Records {i1} and {i2} are equal, classes {df.iloc[i1,-1]} and {df.iloc[i2,-1]}")
      if(i1%100==0):
         print(f"i1 = {i1}")
   return toRemove,toAdapt

if __name__ == "__main__":
   plt.style.use('seaborn') # sono gusti
   rng = np.random.default_rng() # new random number generator

   dataset = "inliers"
   df = pd.read_csv("../"+dataset+".csv")
   toRemove = np.zeros(len(df))
   toAdapt  = np.zeros(len(df))
   toRemove,toAdapt = checkUnique(toRemove,toAdapt)
   np.savetxt("toAdapt.csv",toAdapt,delimiter=',')

   if(len(df)>100):
      dfSmall = df.iloc[df.index%10==1]  # sampling, 1 in 10
      dfSmall.reset_index(drop=True,inplace=True) # does not insert old index as a column
   else:
      dfSmall = df # already small by itself

   #X, labels_true = generate_clusters()
   X = dfSmall.iloc[:,1:-1].values
   colnames = np.array( df.columns[1:-1] )
   labels_true = dfSmall.iloc[:,-1].values
   c0 = np.array([[X[i,0],X[i,1],X[i,2]] for i in np.arange(len(X)) if labels_true[i] == 0])
   c1 = np.array([[X[i,0],X[i,1],X[i,2]] for i in np.arange(len(X)) if labels_true[i] == 1])
   #c1 = [X[i] for i in np.arange(len(X)) if labels_true[i] == 1]
   point_indices = np.arange(len(X))

   list_colours = ["red", "green"]       # a color for each cluster
   cluster_colors = [list_colours[i] for i in labels_true] # color of each point

   # plot the clusters
   d = [0,1,2]  # dimensions to plot

   xp, yp, zp = X[:, d[0]], X[:, d[1]], X[:, d[2]]   # point coords (3d)
   fig = plt.figure(figsize=(9, 6), facecolor="w")
   ax  = plt.axes(projection="3d")
   scatter_plot = ax.scatter3D(xp, yp, zp, c=cluster_colors, marker='o')
   plt.title("The whole dataset", fontsize=20)
   ax.set_xlabel(colnames[d[0]], fontweight='bold')
   ax.set_ylabel(colnames[d[1]], fontweight='bold')
   ax.set_zlabel(colnames[d[2]], fontweight='bold')
   for i in np.arange(len(xp)):
      ax.text(xp[i],yp[i],zp[i],i)
   plt.show()

   # -------------------------------- convex hulls
   lstHulls = []
   clusters = [c0,c1]
   for i in np.arange(len(clusters)):
      lstHulls.append(ConvexHull(clusters[i])) # <<=== qui convex hull

   # plotting points
   fig = plt.figure(figsize=(9,6), facecolor="w")
   plt.title("Convex hulls")
   ax = plt.axes(projection="3d")
   scatter_plot = ax.scatter3D(xp, yp, zp, c=cluster_colors, marker='o')
   for i in np.arange(len(xp)):
      ax.text(xp[i],yp[i],zp[i],i)

   # plotting each hull
   for i in np.arange(len(clusters)):
      hull = lstHulls[i]
      nsimplices = len(hull.simplices) # num of facets of the hull
      xvert = X[hull.vertices, d[0]] # coords of hull vertices
      yvert = X[hull.vertices, d[1]]
      zvert = X[hull.vertices, d[2]]

      colr = cluster_colors[point_indices[labels_true==i][0]]

      # plot testset points
      #Xs = c0[hull.vertices[0]]
      #Ys = c0[hull.vertices[1]]
      #Zs = c0[hull.vertices[2]]
      #ax.plot3D(Xs, Ys, Zs,'s-',c=colr)

      for simplex in hull.simplices:
         clus = np.array(clusters[i])
         pnts = [clus[simplex,:]]
         try:
            facet = Poly3DCollection(pnts, facecolors=colr, alpha=0.3)
            lines = Line3DCollection(pnts, colors=colr, linewidths=1)
            ax.add_collection3d(facet)
            ax.add_collection3d(lines)
         except:
            print("Problem: "+str(len(pnts)))

   ax.set_xlabel(colnames[d[0]], fontweight='bold')
   ax.set_ylabel(colnames[d[1]], fontweight='bold')
   ax.set_zlabel(colnames[d[2]], fontweight='bold')
   plt.show()

   # legge e plotta bounding boxes
   hboxes = []
   bbox3d()
   plotBboxes(list_colours)

   # check for point in a cluster
   c0points = point_indices[labels_true == 0]
   c1points = point_indices[labels_true == 1]
   newclus = []
   seed = np.random.randint(len(c0points))
   clus = c0
   clus = init_hull(clus,seed)
   # To test the point:
   idpoint=0
   print('Random point is in the hull?', contained([c0[idpoint]],hull.equations))

   # adding points
   fAddPoint = False
   if(fAddPoint):
      hull = ConvexHull(X[hull.vertices], incremental=True)
      print("Adding a new point to the hull")
      for pt in [(1,2,1,1,2,1)]:
         hull.add_points([pt])

      # check for separating values
      idcol = 1
      print("check for separating values, clumn "+dfSmall.columns[idcol])
      val = np.unique(dfSmall.iloc[:,idcol].values, axis=0) # keep only unique values
      for v in val:
         count0 = 0
         count1 = 0
         for i in np.arange(len(dfSmall)):
            if(dfSmall.iloc[i,idcol]==v):
               if(dfSmall.iloc[i,-1]==0):
                  count0 += 1
               else:
                  count1 += 1
         print(f"{v}- class0 {count0} class1 {count1}")
   pass
