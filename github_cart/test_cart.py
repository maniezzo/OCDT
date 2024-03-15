#  just sklearn cart
import dataset
from sklearn import tree
from sklearn.metrics import accuracy_score
from sklearn.model_selection import train_test_split

"""## Load Data """
x, y = dataset.loadData('points')

"""## Set Args"""
timelimit = 600
seed = 550
depth = 0

train_ratio = 0.5
val_ratio = 0.25
test_ratio = 0.25
x_train, x_test, y_train, y_test = train_test_split(x, y, test_size=1-train_ratio, random_state=seed)
x_val, x_test, y_val, y_test = train_test_split(x_test, y_test, test_size=test_ratio/(test_ratio+val_ratio), random_state=seed)

x_train = x
y_train = y
x_test = x
y_test = y
"""## SK-Learn Decision Tree """
clf = tree.DecisionTreeClassifier(max_depth=None)
clf.fit(x_train, y_train)
tree_rules = tree.export_text(clf)  # ,feature_names=list(res_sk.columns)
print(tree_rules)

y_train_pred = clf.predict(x_train)
score_train = accuracy_score(y_train, y_train_pred)

y_test_pred = clf.predict(x_test)
score_test = accuracy_score(y_test, y_test_pred)

print(f"Finito, score train = {score_train} score test = {score_test}")