import ast
import pandas as pd

def load_rules(filepath):
    """Load list of rule dictionaries from rules.txt."""
    with open(filepath, 'r') as f:
        text = f.read().strip()
        # If the file contains a Python literal (e.g., [ {...}, {...} ])
        # or single dicts per line, handle both cases
        if text.startswith('['):
            rules = ast.literal_eval(text)
        else:
            rules = [ast.literal_eval(line) for line in text.splitlines() if line.strip()]
    return rules


def record_matches_rule(record, conds):
    """Check if one record matches all conditions in a rule."""
    for attr, op, value in conds:
        v = record[f"f{attr}"]
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

dataFileName = "whouse"
df = pd.read_csv(f"..\\..\data\\{dataFileName}.csv")
rules = load_rules("rules.txt")
result_df = check_classification(df, rules)
