import ast
import pandas as pd
import operator

# Load list of rule dictionaries from rules.txt.
def load_rules(filepath):
   # If the file contains a Python literal (e.g., [ {...}, {...} ]) or single dicts per line, handle both cases
   with open(filepath, 'r') as f:
      text = f.read().strip()
      if text.startswith('['):
         rules = ast.literal_eval(text)
      else:
         rules = [ast.literal_eval(line) for line in text.splitlines() if line.strip()]
   return rules

def validate_rules(df, lstRules):
   """
   Validates that all rows in the DataFrame are correctly classified by the given rules.
   Parameters:
   - df: pandas DataFrame with numerical features and the true class in the last column.
   - lstRules: list of rule dicts, each with 'class' and 'cond' keys.
               'cond' is a list of tuples: (col_index, operator, threshold)
   Returns:
   - True if all rows are correctly classified, False otherwise.
   """

   # Mapping of operator strings to functions
   op_map = {
      '<':  operator.lt,
      '<=': operator.le,
      '>':  operator.gt,
      '>=': operator.ge,
      '==': operator.eq,
      '!=': operator.ne
   }

   # Extract features and true labels
   X      = df.iloc[:, :-1]  # all but last column
   y_true = df.iloc[:, -1]  # last column

   n_rows = len(df)
   for idx in range(n_rows):
      row = X.iloc[idx]
      true_class = y_true.iloc[idx]
      predicted_class = None

      # Try rules in order until one matches
      for rule in lstRules:
         conditions = rule['cond']
         matches = True
         for col_idx, op_str, thresh in conditions:
            if col_idx >= len(row):
               raise IndexError(f"Column index {col_idx} out of bounds for row with {len(row)} features.")
            val = row.iloc[col_idx]
            op_func = op_map.get(op_str)
            if op_func is None:
               raise ValueError(f"Unsupported operator: {op_str}")
            if not op_func(val, thresh):
               matches = False
               break
         if matches:
            predicted_class = rule['class']
            break  # first matching rule

      if predicted_class is None:
         print(f"Row {idx} did not match any rule.")
         return False

      if predicted_class != true_class:
         print(f"Misclassification at row {idx}: predicted {predicted_class}, true {true_class}")
         return False
      else:
         print(f"Row {idx} matches predicted {predicted_class}")

   return True

def record_matches_rule(record, conds):
   """Check if one record matches all conditions in a rule."""
   for attr, op, value in conds:
      v = record[f"f{int(attr)+1}"]
      if op == '>':
         if not v > value:
            print(f"ERROR on {attr}")
            return False
      elif op == '>=':
         if not v >= value:
            print(f"ERROR on {attr}")
            return False
      elif op == '<':
         if not v < value:
            print(f"ERROR on {attr}")
            return False
      elif op == '<=':
         if not v <= value:
            print(f"ERROR on {attr}")
            return False
      elif op == '==':
         if not v == value:
            print(f"ERROR on {attr}")
            return False
      elif op == '!=':
         if not v != value:
            print(f"ERROR on {attr}")
            return False
      else:
         raise ValueError(f"Unsupported operator: {op}")
   return True


def classify_dataframe(df, rules):
   """Apply rules to classify the dataframe and return predicted labels."""
   predictions = []
   for _, row in df.iterrows():
      predicted_class = None
      for rule in rules:
         if record_matches_rule(row, rule["cond"]):
            predicted_class = rule["class"]
            break  # stop at first matching rule
      predictions.append(predicted_class)
   return predictions


def check_classification(df, rules):
   """Compare rule-based classification to df['class'] and report accuracy."""
   preds = classify_dataframe(df, rules)
   df["predicted_class"] = preds
   correct = (df["predicted_class"] == df["class"]).sum()
   total = len(df)
   accuracy = correct / total
   print(f"Correctly classified: {correct}/{total} ({accuracy:.2%})")
   if correct < total:
      print("Misclassified records:")
      print(df[df["predicted_class"] != df["class"]])
   return df

if __name__ == "__main__":
   dataFileName = "whouse"
   df = pd.read_csv(f"..\\..\data\\{dataFileName}.csv")
   df = df.iloc[:, 1:]  # Drop the first column after loading, just indices
   rules = load_rules(f"..\\..\data\\{dataFileName}_rules2.txt")
   result_df = validate_rules(df, rules)
