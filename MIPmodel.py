import numpy as np, pandas as pd, os
from pulp import *

class MIPmodel:
   def __init__(self,npoints,nbox):
      self.npoints = npoints  # total number of points
      self.nbox    = nbox # number of AABBs
      return
   def makeModel(self,lstAABB,Xcoord,ndim,class01):
      ncol = 2*ndim*self.npoints  # num boundary surfaces
      # decision variables
      categ = 'Binary';  # 'Continuous''
      X = LpVariable.dicts('X_%s', (range(ncol)),
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
      for ibox in np.arange(self.nbox):
         for dim in np.arange(ndim):
            separ = []
            for i in np.arange(self.npoints):
               for j in np.arange(self.npoints):
                  if(i==j): continue
                  if(class01[i]==class01[j]): continue
                  mid = (lstAABB[ibox][dim,1]-lstAABB[ibox][dim,0])/2
                  if(Xcoord[i,dim]<mid and Xcoord[j,dim]>mid):
                     separ.append(2*ndim*dim+dim)
            if(len(separ)>0):
               probl += (sum(X[i] for i in separ) >= 1, "Cover %d" % nrows)
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
