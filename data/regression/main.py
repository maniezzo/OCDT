import matplotlib.pyplot as plt
import numpy as np, pandas as pd

def create_dataset(dataset, look_back=1):
   dataX, dataY = [], []
   for i in range(len(dataset) - look_back):
      a = dataset[i:(i + look_back)]
      dataX.append(a)
      dataY.append(dataset[i + look_back])
   return np.array(dataX), np.array(dataY)

if __name__ == '__main__':
   path = 'BoxJenkins.csv'
   df = pd.read_csv(path)
   val = df.iloc[:,1].values
   X,Y = create_dataset(val,12)

   q = np.quantile(Y[:-12],[0,0.25,0.5,0.75,1])
   nquant = 4
   mids = []
   for i in range(nquant):
      mids.append((q[i]+q[i+1])/2)

   yquant = []
   for i in range(len(Y)):
      for j in range(len(q)):
         if (Y[i]>=q[len(q)-j-1]):
            yq = q[len(q)-j-1]
            yquant.append(yq)
            break

   train = np.append(Y[:-12],[None for i in range(12)])
   valid = np.append([None for i in range(len(Y)-12)],Y[-12:])
   trainq= np.append(yquant[:-12],[None for i in range(12)])
   validq= np.append([None for i in range(len(Y)-12)],yquant[-12:])

   fig = plt.Figure()
   plt.plot(train,':',linewidth=2,label="train")
   plt.plot(valid,':',linewidth=2,label="test")
   plt.plot(trainq,label="prediction")
   plt.plot(validq,label="forecast")
   plt.legend()
   plt.savefig("boxJenkins.eps",format="eps")
   plt.show()
