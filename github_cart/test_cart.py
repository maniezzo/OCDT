#  just sklearn cart
import dataset
import numpy as np, pandas as pd
from sklearn import tree
from sklearn.metrics import accuracy_score
from sklearn.model_selection import train_test_split

from sklearn.tree import _tree

def tree_to_code(tree, feature_names):
    tree_ = tree.tree_
    feature_name = [
        feature_names[i] if i != _tree.TREE_UNDEFINED else "undefined!"
        for i in tree_.feature
    ]
    feature_names = [f.replace(" ", "_")[:-5] for f in feature_names]
    print("def predict({}):".format(", ".join(feature_names)))

    def recurse(node, depth):
        indent = "    " * depth
        if tree_.feature[node] != _tree.TREE_UNDEFINED:
            name = feature_name[node]
            threshold = tree_.threshold[node]
            print("{}if {} <= {}:".format(indent, name, np.round(threshold,2)))
            recurse(tree_.children_left[node], depth + 1)
            print("{}else:  # if {} > {}".format(indent, name, np.round(threshold,2)))
            recurse(tree_.children_right[node], depth + 1)
        else:
            print("{}return {}".format(indent, tree_.value[node]))

    recurse(0, 1)

    def get_rules(tree, feature_names, class_names):
       tree_ = tree.tree_
       feature_name = [
          feature_names[i] if i != _tree.TREE_UNDEFINED else "undefined!"
          for i in tree_.feature
       ]

       paths = []
       path = []

       def recurse(node, path, paths):

          if tree_.feature[node] != _tree.TREE_UNDEFINED:
             name = feature_name[node]
             threshold = tree_.threshold[node]
             p1, p2 = list(path), list(path)
             p1 += [f"({name} <= {np.round(threshold, 3)})"]
             recurse(tree_.children_left[node], p1, paths)
             p2 += [f"({name} > {np.round(threshold, 3)})"]
             recurse(tree_.children_right[node], p2, paths)
          else:
             path += [(tree_.value[node], tree_.n_node_samples[node])]
             paths += [path]

       recurse(0, path, paths)

       # sort by samples count
       samples_count = [p[-1][1] for p in paths]
       ii = list(np.argsort(samples_count))
       paths = [paths[i] for i in reversed(ii)]

       rules = []
       for path in paths:
          rule = "if "

          for p in path[:-1]:
             if rule != "if ":
                rule += " and "
             rule += str(p)
          rule += " then "
          if class_names is None:
             rule += "response: " + str(np.round(path[-1][0][0][0], 3))
          else:
             classes = path[-1][0][0]
             l = np.argmax(classes)
             rule += f"class: {class_names[l]} (proba: {np.round(100.0 * classes[l] / np.sum(classes), 2)}%)"
          rule += f" | based on {path[-1][1]:,} samples"
          rules += [rule]

       return rules

"""## Load Data """
#df = pd.read_csv('../data/points.csv', delimiter=',',usecols=lambda column: column not in [0])
df = pd.read_csv('../data/whouse.csv', delimiter=',',usecols=lambda column: column not in [0])
# Clean the column names by stripping whitespace and newline characters
df.columns = df.columns.str.replace(r'[\n\r]', '', regex=True)
x, y = dataset.loadData('points')

"""## Set Args"""
timelimit = 600
seed      = 550
depth     = 0

"""## SK-Learn Decision Tree """
clf = tree.DecisionTreeClassifier(criterion='entropy',max_depth=None)
#clf = tree.DecisionTreeClassifier(
#    criterion='entropy',
#    max_depth=None,         # No depth limit, allow deep trees
#    min_samples_split=2,    # Minimum number of samples required to split a node
#    min_samples_leaf=1,     # Every leaf must contain at least one sample (this is the default)
#    class_weight=None       # No class weighting; for pure leaves, this isn't needed directly
#)
clf.fit(x, y)
tree_rules = tree.export_text(clf,feature_names=list(df.columns[1:-1]))
print(tree_rules)

# Get the number of nodes in the tree
num_nodes = clf.tree_.node_count
print(f"Number of nodes: {num_nodes}")
# Get the height of the tree
tree_height = clf.tree_.max_depth
print(f"Height of the tree: {tree_height}")

# Get the indices of features used in splits (ignoring -2, which indicates leaf nodes)
used_feature_indices = np.unique(clf.tree_.feature[clf.tree_.feature >= 0])
# Get feature names if they are available
if hasattr(df, 'columns'):  # df is a DataFrame with column names
   used_features = df.columns[used_feature_indices]
else:  # otherwise, just use the indices
   used_features = used_feature_indices

print("Features used in the tree:", used_features)

# Access the tree structure
tree = clf.tree_

# Print nodes information with multiple children
print("Nodes:")
for i in range(tree.node_count):
   if tree.children_left[i] != tree.children_right[i]:  # Not a leaf node
      # Identify the feature and threshold (split conditions)
      feature_name = df.columns[tree.feature[i]]
      threshold = tree.threshold[i]

      # Check if it's a multi-way split (non-binary)
      if tree.children_left[i] == tree.children_right[i]:  # No actual split, just leaves
         print(f"Node {i} (Leaf Node): Class Distribution = {tree.value[i]}")
      else:
         print(f"Node {i}: Feature '{feature_name}' <= {threshold:.3f}")
         print(
            f"  Left child: {tree.children_left[i]} (Feature '{df.columns[tree.feature[tree.children_left[i]]]}')")
         print(
            f"  Right child: {tree.children_right[i]} (Feature '{df.columns[tree.feature[tree.children_right[i]]]}')")
         print(f"  Threshold: {threshold:.3f}")
         # Add further logic here to identify the split
   else:
      print(f"Leaf Node {i}: Class Distribution = {tree.value[i]}")

# You can also print the arcs based on children
print("\nArcs:")
for i in range(tree.node_count):
   if tree.children_left[i] != tree.children_right[i]:  # Not a leaf node
      print(f"Node {i} -> Left Child {tree.children_left[i]}")
      print(f"Node {i} -> Right Child {tree.children_right[i]}")


# Get the leaf nodes each sample ends up in
leaf_indices = clf.apply(x)

# Create a DataFrame to associate leaf indices with corresponding data points
df2 = pd.DataFrame(x, columns=df.columns[1:-1])
df2['Leaf Node'] = leaf_indices
df2['Class'] = y

# Group by leaf node and print all data points in each leaf
for leaf_node, group in df2.groupby('Leaf Node'):
   print(f"Leaf Node {leaf_node}:")
   print(group.drop(columns='Leaf Node'))  # Drop the 'Leaf Node' column for readability
   print("\n---\n")

print(f"Finito")