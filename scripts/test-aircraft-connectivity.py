#!/usr/bin/env python3
"""Regression: every part of every runtime aircraft chains back to the fuselage; no Unity required.

Catches the class of defect where a part hovers or a whole assembly floats: static wicks and nav
lights 1.7 m above a winglet, main gear a metre under the wing, tyres beside (not on) their strut,
the Q400's wing assembly sitting 10 cm above the fuselage crown. Uses the same audit as
`scripts/audit-aircraft-geometry.py floating` (5 cm tolerance, runs in a few minutes for all seven).

Usage: python3 scripts/test-aircraft-connectivity.py [ATR42 SF34 DH8D B38M A21N A359 B78X]
"""
import importlib.util
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("audit", os.path.join(HERE, "audit-aircraft-geometry.py"))
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)

ids = sys.argv[1:] or list(audit.MODELS)
failures = []
for cid in ids:
    for name, centre in audit.floating(cid, 0.05):
        failures.append(f"{cid}: {name} is not attached to the airframe (near {centre.round(2).tolist()})")

if failures:
    print("\n".join(failures))
    sys.exit(1)
print(f"PASS: every part of {', '.join(ids)} chains back to the fuselage within 5 cm.")
