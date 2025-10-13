from collections import defaultdict, Counter
import ast


# -------------------------
# Example rule format
# -------------------------
# Each rule is a dict:
#   {"cond": [("A", ">", 3.1), ("B", "<", 5)], "class": "Class1"}

# -------------------------
# Build minimal tree
# -------------------------
def build_rule_tree(rules):
    """
    Recursively build a minimal decision tree from a set of non-overlapping rules.
    Each node tests a single condition from some rule.
    """
    # If all rules have the same class or only one rule left, return leaf
    classes = {r['class'] for r in rules}
    if len(classes) == 1 or len(rules) == 1:
        return rules[0]['class']

    # Count how often each condition appears across remaining rules
    condition_counts = Counter()
    for r in rules:
        for cond in r['cond']:
            condition_counts[cond] += 1

    # Pick the condition that separates the most rules
    best_cond, _ = condition_counts.most_common(1)[0]

    # Partition rules into true/false branches
    true_branch = []
    false_branch = []
    for r in rules:
        if best_cond in r['cond']:
            true_branch.append(r)
        else:
            false_branch.append(r)

    # Recurse
    return {
        'cond': best_cond,
        'true_branch': build_rule_tree(true_branch),
        'false_branch': build_rule_tree(false_branch)
    }

# -------------------------
# Utilities
# -------------------------
def pretty_print(tree, indent=0):
    if isinstance(tree, dict):
        var, op, thr = tree['cond']
        print("  " * indent + f"IF {var} {op} {thr}:")
        pretty_print(tree['true_branch'], indent + 1)
        print("  " * indent + "ELSE:")
        pretty_print(tree['false_branch'], indent + 1)
    else:
        print("  " * indent + f"-> {tree}")

def tree_stats(tree, depth=0):
    """Return max depth, average depth, total nodes and leaves."""
    if not isinstance(tree, dict):
        return {'max_depth': depth, 'avg_depth': depth, 'n_nodes': 1, 'n_leaves': 1}
    left = tree_stats(tree['false_branch'], depth + 1)
    right = tree_stats(tree['true_branch'], depth + 1)
    max_depth = max(left['max_depth'], right['max_depth'])
    n_leaves = left['n_leaves'] + right['n_leaves']
    avg_depth = (left['avg_depth']*left['n_leaves'] + right['avg_depth']*right['n_leaves']) / n_leaves
    n_nodes = 1 + left['n_nodes'] + right['n_nodes']
    return {'max_depth': max_depth, 'avg_depth': avg_depth, 'n_nodes': n_nodes, 'n_leaves': n_leaves}

if __name__ == "__main__":
   rules = []
   with open('rules.txt', 'r', encoding='utf-8') as file:
       for line in file:
           line = line.strip()  # Remove any leading/trailing whitespace or newlines
           if line:  # Skip empty lines
               data = ast.literal_eval(line)
               rules.append(data)

   tree = build_rule_tree(rules)

   print("Decision Tree:")
   pretty_print(tree)

   print("\nTree Statistics:")
   print(tree_stats(tree))
