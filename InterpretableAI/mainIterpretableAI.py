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

grid = iai.GridSearch(
    iai.OptimalTreeClassifier(
        random_seed=1,
    ),
    max_depth=range(1, 6),
)
grid.fit(train_X, train_y)
grid.get_learner()
score = grid.score(test_X, test_y, criterion='auc')
fig = grid.ROCCurve(test_X, test_y, positive_label=1)
print("Fine")