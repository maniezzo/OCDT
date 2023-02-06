import numpy
import numpy as np, pandas as pd, os
import matplotlib.pyplot as plt
import seaborn as sn
from sklearn.preprocessing import MinMaxScaler

if __name__ == "__main__":
   os.chdir(os.path.dirname(os.path.abspath(__file__)))
   df = pd.read_csv("..\dataset2_nonan.csv")

   # generalità descrittive
   numcol = len(df.columns)
   for i in np.arange(numcol):
      print(df.iloc[:,i].describe())

   # correlazione fra serie
   corr_matrix = df.iloc[:,1:numcol].corr()
   print(corr_matrix)
   sn.heatmap(corr_matrix, annot=True)
   plt.show()

   # distribuzione, scomposta per classi
   pd.DataFrame.hist(df.iloc[:,1:numcol], figsize=[9, 6]);
   #for i in np.arange(1,numcol-1):
   #   ax = df.plot.hist(column=[df.columns[i]], by = "Class_0 or 1", figsize=(9, 6))
   #   plt.show()

   # datasets, numpy array
   data = df.values
   X,y = data[:, 1:-1], data[:, -1]

   # outliers
   from sklearn.neighbors import LocalOutlierFactor
   print("Outlier detection. Original size: "+str(X.shape))

   # Isolation Forest (iForest) is a tree-based anomaly detection algorithm.
   # it models  normal data so to isolate anomalies that are both
   # few in number and different in the feature space.
   from sklearn.ensemble import IsolationForest
   iso = IsolationForest(contamination=0.1)
   yhat = iso.fit_predict(X)
   maskIso = (yhat != -1)
   Xif = np.array(df)[maskIso,:]
   print("Iso "+str(Xif.shape))

   # Minimum Covariance Determinant
   # If the input variables have a Gaussian distribution (NOT HERE)

   # Local Outlier Factor (lof) locates examples that are far from others in the feature space
   lof = LocalOutlierFactor()
   yhat = lof.fit_predict(X)
   # select all rows that are not outliers
   maskLof = (yhat != -1)
   Xlof = np.array(df)[maskLof,:]
   print("lof "+str(Xlof.shape))

   # One-Class SVM (un po' sforzato)
   from sklearn.svm import OneClassSVM
   # identify outliers in the training dataset
   ee = OneClassSVM(nu=0.01)
   yhat = ee.fit_predict(X)
   maskSvm = (yhat != -1)
   Xsvm = np.array(df)[maskSvm,:]
   print("SVM "+str(Xsvm.shape))

   # 2D scatters with outliers
   maskOut   = maskLof
   maskClass = np.array([True if df.iloc[i, -1] > 0.5 else False for i in np.arange(len(maskOut))])

   cont = 1
   if cont > 0:    # bypass plots
      for ii in np.arange(1,len(df.columns)-2):
         for jj in np.arange(ii+1,len(df.columns)-1):
            print(str(cont)+") "+df.columns[ii]+"-"+df.columns[jj])
            cont += 1
            i = ii-1
            j = jj-1

            plt.figure()
            # positive records
            s1 = plt.scatter(X[maskClass[:], i], X[maskClass[:], j], marker='o', c=['#0f0'])
            # negative records
            s2 = plt.scatter(X[numpy.invert(maskClass[:]), i], X[numpy.invert(maskClass[:]), j], marker='o', c=['#f00'])
            # valid records
            s3 =  plt.scatter(X[maskOut[:],i],X[maskOut[:],j],marker='.',c=['#ccc'])
            # outliers
            s4 = plt.scatter(X[numpy.invert( maskOut[:] ),i] ,X[numpy.invert( maskOut[:] ),j],marker='.',c=['#000'])
            plt.legend((s1,s2,s3,s4),
                       ('Positive', 'Negative', 'Valid', 'Outliers'),
                       scatterpoints=1,
                       loc='upper right',
                       ncol=1,
                       fontsize=8)
            plt.title(df.columns[ii]+"-"+df.columns[jj])
            plt.show()

   # 3D scatter
   colors = ['g','r','k','0.95']

   fig = plt.figure()
   ax = plt.subplot(111, projection='3d')
   ax.plot(X[maskClass[:], 0],
           X[maskClass[:], 1],
           X[maskClass[:], 2], 'o', color=colors[0], alpha=0.5, label='Positive')
   ax.plot(X[numpy.invert(maskClass[:]), 0],
           X[numpy.invert(maskClass[:]), 1],
           X[numpy.invert(maskClass[:]), 2], 'o', color=colors[1], alpha=0.5, label='Negative')
   ax.plot(X[numpy.invert( maskOut[:] ),0],
           X[numpy.invert( maskOut[:] ),1],
           X[numpy.invert( maskOut[:] ),2], '.', color=colors[2], label='Outliers')
   ax.plot(X[maskOut[:],0],
           X[maskOut[:],1],
           X[maskOut[:],2], '.', color=colors[3], alpha=0.2, label='Valid')
   ax.set_xlabel(df.columns[1], rotation=150)
   ax.set_ylabel(df.columns[2])
   ax.set_zlabel(df.columns[3], rotation=60)
   plt.legend(loc='upper right', numpoints=1, ncol=1, fontsize=8, bbox_to_anchor=(0,0))
   plt.show()

   fig = plt.figure()
   ax = plt.subplot(111, projection='3d')
   ax.plot(X[maskClass[:], 3],
           X[maskClass[:], 4],
           X[maskClass[:], 5], 'o', color=colors[0], alpha=0.5, label='Positive')
   ax.plot(X[numpy.invert(maskClass[:]), 3],
           X[numpy.invert(maskClass[:]), 4],
           X[numpy.invert(maskClass[:]), 5], 'o', color=colors[1], alpha=0.5, label='Negative')
   ax.plot(X[numpy.invert(maskOut[:]), 3],
           X[numpy.invert(maskOut[:]), 4],
           X[numpy.invert(maskOut[:]), 5], '.', color=colors[2], label='Outliers')
   ax.plot(X[maskOut[:], 3],
           X[maskOut[:], 4],
           X[maskOut[:], 5], '.', color=colors[3], alpha=0.2, label='Inlieers')
   ax.set_xlabel(df.columns[4], rotation=150)
   ax.set_ylabel(df.columns[5])
   ax.set_zlabel(df.columns[6], rotation=60)
   plt.legend(loc='upper right', numpoints=1, ncol=1, fontsize=8, bbox_to_anchor=(0, 0))
   plt.show()

   dfInliers = pd.DataFrame(X[maskOut[:],:],columns=df.columns[1:-1])
   dsIdOrg = df.iloc[maskOut[:],0]
   dfInliers = dfInliers.merge(dsIdOrg, left_index=True, right_index=True)
   dsClass = df.iloc[maskOut[:],-1]
   dfInliers = dfInliers.merge(dsClass, left_index=True, right_index=True)
   dfInliers.to_csv("inliers.csv", index=False)

   pass