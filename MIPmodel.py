import numpy as np, pandas as pd, os
from pulp import *

class MIPmodel:
   def __init__(self,npoints,nbox):
      self.npoints = npoints  # total number of points
      self.nbox    = nbox     # number of AABBs
      return
   def colAttributes(self,ncol,ndim,lstAABB):
      ibox = 0
      for i in np.arange(ncol):
         ibox = i // (2*ndim)
         idim = (i - ibox*2*ndim) // 2
         hilo = (i - ibox*2*ndim - idim*2)
         if(hilo==0):
            xcut = (lstAABB[ibox,1][1].lstAABB[ibox,0][1])/2
      return
   def makeModel(self,lstAABB,Xcoord,ndim,class01):
      ncol = 2*ndim*self.nbox  # num boundary surfaces, each box, each dimension, lo and hi
      self.colAttributes(ncol,ndim,lstAABB)
      # decision variables
      categ = 'Binary';  # 'Continuous''
      X = LpVariable.dicts('X%s', (range(ncol)),
                           cat=categ,
                           lowBound=0,
                           upBound=1)
      # create the LP object, set up as a MINIMIZATION problem
      probl = LpProblem('ODT', LpMinimize)
      # cost function
      c = np.ones(ncol)
      probl += sum(c[i] * X[i] for i in range(ncol))

      # covering constraints
      nrows = 0
      for i in np.arange(self.nbox):
         for j in np.arange(self.nbox):
            if (i == j): continue
            if (class01[i] == class01[j]): continue
            # check each column whe it separates i from j
            separ = []
            for ibox in np.arange(self.nbox):
               for dim in np.arange(ndim):     # still lo and hi
                  icol1 = 2*ndim*ibox + 2*dim    # lo
                  mid1 = (lstAABB[i][dim,1]-lstAABB[j][dim,1])/2
                  if((Xcoord[i,dim]-Xcoord[j,dim])>mid1):
                     separ.append(icol1)
                  icol2 = 2*ndim*ibox+2*dim+1    # hi
                  mid2 = (lstAABB[i][dim, ] - lstAABB[j][dim, 2]) / 2
                  if (Xcoord[i, dim] < mid2 and Xcoord[j, dim] > mid2):
                     separ.append(icol2)
            if(len(separ)>0):
               probl += sum(X[i] for i in separ) >= 1, "Cover %d" % nrows
               nrows += 1

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
