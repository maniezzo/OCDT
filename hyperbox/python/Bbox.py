import numpy as np

# bounding box class
class Bbox:
   def __init__(self,ndim):
      self.ndim = ndim
      self.box = np.zeros(2*ndim).reshape(2,ndim)
   def set(self,lo,hi):
      for i in np.arange(self.ndim):
         self.box[0,i] = lo[i]
         self.box[1,i] = hi[i]
   def get(self):
      return self.box
   def intersect(self,xp):
      fIntersect = True
      for i in np.arange(self.ndim):
         if(xp[i]<self.box[0,i] or xp[i]>self.box[1,i]):
            fIntersect = False
            break
      return fIntersect