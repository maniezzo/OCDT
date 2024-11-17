#  just sklearn cart
import dataset
import numpy as np, pandas as pd
from sklearn import tree
from sklearn.metrics import accuracy_score
from sklearn.model_selection import train_test_split

"""## Load Data """
df = pd.read_csv('../data/points.csv', delimiter=',',usecols=lambda column: column not in [0])
# Clean the column names by stripping whitespace and newline characters
df.columns = df.columns.str.replace(r'[\n\r]', '', regex=True)
x, y = dataset.loadData('points')

"""## Set Args"""
timelimit = 600
seed = 550
depth = 0

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
tree_rules = tree.export_text(clf)  # ,feature_names=list(res_sk.columns)
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