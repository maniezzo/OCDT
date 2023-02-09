import numpy as np, pandas as pd, os
from pulp import *

class MIPmodel:
   def __init__(self,npoints,nbox):
      self.npoints = npoints  # total number of points
      self.nbox    = nbox     # number of AABBs
      return
   def colAttributes(self,ncol,ndim,lstAABB,colattr):
      for i in np.arange(ncol):
         ibox = i // (2*ndim)
         idim = (i - ibox*2*ndim) // 2
         hilo = (i - ibox*2*ndim - idim*2)
         if(hilo==0):
            xcut = (lstAABB[ibox][idim,0]+lstAABB[ibox][idim,1])/2
         else:
            xcut = (lstAABB[ibox][idim,2]+lstAABB[ibox][idim,3])/2
         colattr.append({"box":ibox,"dim":idim,"xcut":xcut})
      return
   def makeModel(self,lstAABB,Xcoord,ndim,class01):
      ncol = 2*ndim*self.nbox  # num boundary surfaces, each box, each dimension, lo and hi
      colattr = []
      self.colAttributes(ncol,ndim,lstAABB,colattr)
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
            for icol in np.arange(ncol): # check if col separates box i from j
               dim  = colattr[icol]['dim']
               xcut = colattr[icol]['xcut']
               xmin1 = lstAABB[i][0,1]
               xmax1 = lstAABB[i][0,2]
               xmin2 = lstAABB[j][0,1]
               xmax2 = lstAABB[j][0,2]
               # check wh xcut separates i from j
               if((xmin1>xcut and xmax2<xcut) or (xmin2>xcut and xmax1<xcut)):
                  separ.append(icol)
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
