import numpy as np, pandas as pd, os
from pulp import *

# basic IP model for cut selection
class MIPmodel:
   def __init__(self,npoints,nbox):
      self.npoints = npoints  # total number of points
      self.nbox    = nbox     # number of AABBs
      return
   def colAttributes(self,ncol,ndim,lstAABB,colattr):
      for i in np.arange(ncol):
         ibox = i // (2*ndim)  # external index (id box)
         hilo = (i - ibox*2*ndim) // ndim # middle index (min / max)
         idim = (i - ibox*2*ndim - hilo*ndim) # internal index (dim)
         if(len(lstAABB[0][0])== ndim):
            if(hilo==0):
               xcut = lstAABB[ibox][0][idim]
            else:
               xcut = lstAABB[ibox][1][idim]
         else:
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

      # covering constraints, on points
      nrows = 0
      for i in np.arange(self.npoints):
         for j in np.arange(self.npoints):
            if (i == j): continue
            if (class01[i] == class01[j]): continue
            # check each column whe it separates i from j
            separ = []
            for icol in np.arange(ncol): # check if col separates box i from j
               dim  = colattr[icol]['dim']
               xcut = colattr[icol]['xcut']
               xmin1 = Xcoord[i,dim] #lstAABB[i][dim,1]
               xmax1 = Xcoord[i,dim] #lstAABB[i][dim,2]
               xmin2 = Xcoord[j,dim] #lstAABB[j][dim,1]
               xmax2 = Xcoord[j,dim] #lstAABB[j][dim,2]
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
      sol = []
      for v in probl.variables():
         if (v.varValue > 0):
            print(v.name, "=", v.varValue)
            i = int(v.name[1:])
            dim  = colattr[i]['dim']
            xcut = colattr[i]['xcut']
            sol.append({'dim':dim,'xcut':xcut})
      return sol
