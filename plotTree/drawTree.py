import numpy as np
import matplotlib.pyplot as plt
import sklearn.tree as tree
import graphviz

if __name__ == "__main__":
   from sklearn.datasets import load_iris
   clf = tree.DecisionTreeClassifier(random_state=0)
   iris = load_iris()
   clf = clf.fit(iris.data, iris.target)
   fig = plt.figure(figsize=(25,20))
   _ = tree.plot_tree(clf,
                      feature_names=iris.feature_names,
                      class_names=iris.target_names,
                      filled=True)
   fig.savefig("decistion_tree.png")

   dot = graphviz.Digraph(comment="A graph", format="svg")
   dot.node('A', 'King Arthur')
   dot.node('B', 'Sir Bedevere the Wise')
   dot.node('C', 'Sir Lancelot the Brave')
   dot.edge('A', 'B')
   dot.edge('A', 'C')
   dot.render('digraph.gv', view=True)