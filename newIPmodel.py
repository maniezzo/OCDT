import numpy as np, pandas as pd, os
import matplotlib.pyplot as plt
from pulp import *
import copy, Bbox

def MILPmodel():
   n = len(df) # num boundary surfaces
   # decision variables
   categ = 'Binary';  # 'Continuous''
   X = LpVariable.dicts('X_%s', (range(n)),
                        cat=categ,
                        lowBound=0,
                        upBound=1)
   # create the LP object, set up as a MINIMIZATION problem
   probl = LpProblem('ODT', LpMinimize)
   # cost function
   probl += sum(sum(c[i][j] * X[i][j] for j in range(n)) for i in range(n))

   # assignment constraint
   for j in range(n):
      probl += (sum(X[i][j] for i in range(n)) == 1, "Assignment %d" % j)

   for i in range(n):
      for j in range(n):
         probl += (X[i][j] - X[i][i] <= 0, "c{0}-{1}".format(i, j))

   # save the model in a lp file
   probl.writeLP("octmodel.lp")
   # view the model
   print(probl)

   # solve the model
   probl.solve()
   print("Status:", LpStatus[probl.status])
   print("Objective: ", value(probl.objective))
   for v in probl.variables():
      if (v.varValue > 0):
         print(v.name, "=", v.varValue)

# plots a solution
def plotSolution():
   plt.figure(figsize=(9,6))
   plt.scatter(X[:, 0], X[:, 1], c=df["class"].values)
   for i in np.arange(n):
      plt.annotate(df.iloc[i, 0], (X[i, 0], X[i, 1]))

   from matplotlib.patches import Rectangle
   ax = plt.gca()
   for b in lstAABB:
      lx,ly = b[0,1]-0.2,b[1,1]-0.2
      wx,wy = b[0,2]-b[0,1]+0.4,b[1,2]-b[1,1]+0.4
      rect = Rectangle((lx, ly), wx, wy, linewidth=1, edgecolor='r', facecolor='none')
      ax.add_patch(rect)
   plt.show()

# check wh point j intersects current tight AABB (minhi,minlo)
def intersect(jp):
   fIntersect = True
   for dim in np.arange(ndim):
      if X[jp,dim] < minlo[dim] or X[jp, dim] > minhi[dim]:
         fIntersect = False
         break
   return fIntersect

# initializes a new AABB with the point with index in argument
def initializeAABB(ip):
   minlo = np.zeros(ndim)  # minimum (tighest) lower extent
   minhi = np.zeros(ndim)  # minimum (tighest) upper extent
   maxlo = np.zeros(ndim)  # maximum (widest)  lower extent
   maxhi = np.zeros(ndim)  # maximum (widest)  upper extent
   for i in np.arange(ndim):
      maxlo[i] = np.min(X[:, i]) - 1  # going to infinite
      maxhi[i] = np.max(X[:, i]) + 1
      minlo[i] = X[ind[idp, k], i]
      minhi[i] = X[ind[idp, k], i]
   return minlo,minhi,maxlo,maxhi

# recompute bbox
def recomputeAABB():
   for dim in np.arange(ndim):
      minhi[dim] = minlo[dim] = X[lstPoints[0], dim]  # recompute tight bbox
      for lp in lstPoints:
         if (X[lp, dim] > minhi[dim]): minhi[dim] = X[lp,dim]
         if (X[lp, dim] < minlo[dim]): minlo[dim] = X[lp,dim]

# the bbox given current min values
def AABB2bbox():
   bbox = np.zeros(4 * ndim).reshape(ndim, 4)  # rows: dim, cols: maxlo minlo minh maxhi
   for i in np.arange(ndim):
      bbox[i, 0] = maxlo[i]
      bbox[i, 1] = minlo[i]
      bbox[i, 2] = minhi[i]
      bbox[i, 3] = maxhi[i]
   return bbox

# remove from lstPoints all those incompatible with jp along direction dim
def pruneLstPoints(jp,dim):
   for lp in lstPoints:  # patch, if I find an incompatible presented too late
      if (X[lp, dim] == X[jp, dim] and X[lp, dim] == minlo[dim]):
         maxlo[dim] = X[lp, dim]
         print(f"Removing {lp} from {lstPoints} (incompatible with {jp})")
         lstPoints.remove(lp)  # confliting internal point
      if (X[lp, dim] == X[jp, dim] and X[lp, dim] == minhi[dim]):
         maxhi[dim] = X[lp, dim]
         print(f"Removing {lp} from {lstPoints} (incompatible with {jp})")
         lstPoints.remove(lp)  # confliting internal point

# reduces AABB given point jp of different class. Return false if impossible
def reduceAABB(jp,dir):
   for dim in np.arange(ndim):
      if X[jp,dim] > minlo[dim] and X[jp, dim] < minhi[dim]:
         fIntersect = intersect(jp)
         if(fIntersect):
            pruneLstPoints(jp,dir)
            recomputeAABB()
         return False # cannot reduce, point incompatible
      if dim != dir: # do not reduce on the explored dimension
         if (X[jp,dim] < minlo[dim] and X[jp,dim] > maxlo[dim]): maxlo[dim] = X[jp,dim]
         if (X[jp,dim] > minhi[dim] and X[jp,dim] < maxhi[dim]): maxhi[dim] = X[jp,dim]
      else: #dim = dir
         if(intersect(jp)):
            pruneLstPoints(jp,dim)
         recomputeAABB()
   return True

# enlarges AABB given point of same class. Return false if impossible
def enlargeAABB(jp,dim):
   isCompatible = True
   lo = np.zeros(ndim)
   hi = np.zeros(ndim)
   for i in np.arange(ndim): # Bbox including new point
      lo[i] = minlo[i]
      if(X[jp,i]<lo[i]): lo[i] = X[jp,i]
      hi[i] = minhi[i]
      if(X[jp,i]>hi[i]): hi[i] = X[jp,i]
   B = Bbox.Bbox(ndim)
   B.set(lo,hi)
   for ii in np.arange(n): # check if compatible with all opposite class points
      if(df.iloc[ind[ii,k],3]!=cls):
         if(B.intersect(X[ind[ii,k],:])):
            isCompatible = False
            break
   if(isCompatible):
      lstPoints.append(jp)
      for dim in np.arange(ndim):
         if X[jp,dim] >= maxhi[dim] or X[jp, dim] <= maxlo[dim]:
            return False
         if X[jp, dim] < minlo[dim]: minlo[dim] = X[jp,dim]
         if X[jp, dim] > minhi[dim]: minhi[dim] = X[jp,dim]
      return True
   else:
      return False # cannot enlarge with given point

# adds (if it is the case) a bbox the the bbox list
def addBB(bbox):
   b = 0
   while b < len(lstAABB):
      isContained = True
      fContains   = True
      for d in np.arange(ndim):
         if bbox[d,1] < lstAABB[b][d,1] or bbox[d,2] > lstAABB[b][d,2]:
            isContained = False
         if bbox[d,1] > lstAABB[b][d,1] or bbox[d,2] < lstAABB[b][d,2]:
            fContains = False
      if isContained:
         print("bbox contained, no appending")
         return
      if fContains:
         lstAABB.pop(b)
         b -= 1
      b += 1
   lstAABB.append(copy.deepcopy(bbox))
   print(f"New AAB: {bbox}")

if __name__ == "__main__":
   os.chdir(os.path.dirname(os.path.abspath(__file__)))
   df = pd.read_csv("test3.csv")
   df["class"] = df["class"].map({"x":0,"o":1})

   n = len(df["class"])       # num of points
   X = df.iloc[:,1:-1].values # coords of the points
   plt.scatter(X[:,0],X[:,1],c=df["class"].values)
   for i in np.arange(n):
      plt.annotate(df.iloc[i,0], (X[i,0], X[i,1]))
   plt.show()

   ndim    = len(df.columns)-2 # id and class
   lstAABB = []
   eps     = 0.5

   # ordino i punti secondo ciascuna dimensione
   ind = np.zeros(ndim*n).reshape(n,ndim).astype(int)
   for k in np.arange(ndim):
      hax = np.zeros(n)  # hash of coords, to get them all increasing
      for i in np.arange(n):
         coord = copy.deepcopy(X[i,:]) # assegnare è solo shallow
         coord[0],coord[k] = coord[k],coord[0] # a turno, metto prima quella su cui poi farò l'ordinamento
         for j in np.arange(ndim):
            hax[i] += coord[j]*10**(ndim-j-1) # hashing
      ind[:,k] = np.argsort(hax) # ordinamento indici rispetto coord k

   # permutazioni degli indici delle dimensioni
   from itertools import permutations
   lstPrmt = list(permutations(np.arange(ndim)))

   for idperm in np.arange(len(lstPrmt)): # per ogni permutazione delle dimensioni
      for k in lstPrmt[idperm]:      # dimensione corrente
         for idp in np.arange(n):    # indice punto corrente
            print(f">>>> new bbox on point {ind[idp,k]} dir {k}")
            minlo, minhi, maxlo, maxhi = initializeAABB(ind[idp,k])
            # ordinamento sulla dimensione k, bbox con tutte le altre
            cls = df.iloc[ind[idp,k],3]
            lstPoints = [ind[idp,k]] # points in current cluster

            #j = idp + 1
            j = 0
            while j < n:
               jp = ind[j,k]
               clspt = df.iloc[jp,3] # class of current point j
               if(clspt!=cls):
                  if reduceAABB(jp,k):
                     print(f"reducing cause of {jp} ({X[jp,0]},{X[jp,1]})")
                  else:
                     maxhi[k] = X[jp,k]
                     print(f"point {jp} ({X[jp,0]},{[jp,1]}) incompatible. Closing bbox")
                     break
               else:
                  enlargeAABB(jp,k) # add a point proceding in direction k
                  print(f"adding {jp} ({X[jp,0]},{X[jp,1]})")
               print(f"bbox (cls:{cls}): after idpt {jp} class {clspt} x:{maxlo[0]}/{minlo[0]}/{minhi[0]}/{maxhi[0]} y:{maxlo[1]}/{minlo[1]}/{minhi[1]}/{maxhi[1]}")
               j+=1
            bbox = AABB2bbox()
            addBB(bbox)
   plotSolution()
   print("... END")
   pass