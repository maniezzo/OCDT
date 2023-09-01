import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import os, json

# This just plots the cuts over the data points. See c++ for true tree
if __name__ == '__main__':
   os.chdir(os.path.dirname(os.path.abspath(__file__)))
   dataFileName = "test1"
   if(dataFileName == "iris_setosa"):
      df = pd.read_csv("..\\..\\data\\Iris_setosa.csv",usecols=["Id","SepalWidthCm","PetalLengthCm","class"])
   else:
      df = pd.read_csv(f"..\\..\data\\{dataFileName}.csv")

   n = len(df["class"])       # num of points
   X = df.iloc[:,1:-1].values # coords of the points
   max0 = max(X[:,0])
   max1 = max(X[:,1])

   with open(f"..\\..\data\\{dataFileName}_cuts.json") as jfile: # file con i cut, da usare per ricavare l'ODT
      jobj = json.load(jfile)
   cutdim = jobj["dim"]
   cutval = jobj["pos"]
   plt.figure(figsize=(9,6))
   plt.scatter(X[:, 0], X[:, 1], c=df["class"].values)
   for i in np.arange(n):
      plt.annotate(df.iloc[i, 0], (X[i, 0], X[i, 1]))

   for i in np.arange(len(cutdim)):
      if(cutdim[i]==0):
         x1 = cutval[i]
         y1 = 0
         x2 = cutval[i]
         y2 = max1
      else:
         y1 = cutval[i]
         x1 = 0
         y2 = cutval[i]
         x2 = max0
      plt.plot([x1, x2], [y1, y2], linewidth=2, marker='')
   plt.show()

   print("... END")
   pass
