import interpretableai.installation
import sys
from interpretableai.installation import (install, install_julia, install_system_image,
                                          cleanup_installation)
from interpretableai.predictor import Predictor
# Julia executable: C:\Users\vittorio.maniezzo\AppData\Local\InterpretableAI\InterpretableAI\julia\1.8.5\julia-1.8.5\bin\julia.exe
import interpretableai.iai as iai
from julia import Julia
Julia(sysimage='d:/ongoing/interpretableai/sys.dll')
import pandas as pd

df = pd.read_csv("data_banknote_authentication.txt",header=None)
X = df.iloc[:,0:3]
y = df.iloc[:,4]
(train_X, train_y), (test_X, test_y) = iai.split_data("classification", X, y,seed=1)

print("CART")
grid = iai.GridSearch(
    iai.OptimalTreeClassifier(
        random_seed=1,
        localsearch=False,
        missingdatamode='separate_class',
        criterion='gini',
    ),
    max_depth=range(1, 10),
)
grid.fit(train_X, train_y)
cartlnr = grid.get_learner()

print("Testing without hyperplanes")
grid = iai.GridSearch(
   iai.OptimalTreeClassifier(
      random_seed=1,
   ),
   max_depth=range(1, 6),
)
grid.fit(train_X, train_y)
print(grid.get_grid_result_summary())
lnr = grid.get_learner()
iai.load_graphviz()
lnr.write_png("learner.png")
param = grid.get_best_params()
score1 = grid.score(test_X, test_y, criterion='auc')
print(f"No hyp score={score1}")

treeplot = lnr.TreePlot()
lnr.write_json("tree.json")
lnr.write_html("tree.html")

print("Testing with hyperplanes")
grid = iai.GridSearch(
   iai.OptimalTreeClassifier(
      random_seed=1,
      max_depth=2,
      hyperplane_config={'sparsity': 'all'}
   ),
)
grid.fit(train_X, train_y)
grid.get_learner()
score2 = grid.score(test_X, test_y, criterion='auc')
print(f"Hyp score={score2}")

print("Fine")