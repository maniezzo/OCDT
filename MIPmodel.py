import numpy as np, pandas as pd, os
from pulp import *

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
