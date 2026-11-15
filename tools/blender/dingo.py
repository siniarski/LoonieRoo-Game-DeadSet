"""Dingo: the low, fast lunger. Lean dog with a ridge of glowing mutation spines down its back.

Parts: Body (torso, tail, spines), LegFL, LegFR, LegBL, LegBR (trot in diagonal pairs),
Head (WeakSpot follows it).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from deadset_lib import Model, V  # noqa: E402

m = Model("Dingo")
m.material("M_Fur", (0.82, 0.58, 0.3))
m.material("M_Pale", (0.92, 0.82, 0.62))
m.material("M_Glow", (0.35, 0.95, 0.3), emission=(0.35, 0.95, 0.3), energy=1.4)
m.material("M_Eye", (0.7, 1.0, 0.3), emission=(0.7, 1.0, 0.3), energy=3.0, unshaded=True)
m.material("M_Nose", (0.1, 0.08, 0.07))

HEAD = V(0, 0.76, -0.45)

# ---- Body ---------------------------------------------------------------------------------
body = m.part("Body")
body.loft([
    ((0, 0.57, 0.56), 0.10, 0.11),
    ((0, 0.57, 0.40), 0.155, 0.17),
    ((0, 0.58, 0.12), 0.14, 0.155),
    ((0, 0.57, -0.14), 0.17, 0.205),
    ((0, 0.62, -0.30), 0.13, 0.15),
    ((0, 0.71, -0.40), 0.10, 0.105),
    ((0, 0.76, -0.47), 0.085, 0.085),
], "M_Fur", seg=16, sub=3, caps=("round", "none"))
# Pale chest and belly.
body.loft([
    ((0, 0.47, 0.30), 0.07, 0.05),
    ((0, 0.45, 0.05), 0.10, 0.06),
    ((0, 0.42, -0.16), 0.13, 0.10),
    ((0, 0.52, -0.33), 0.105, 0.10),
    ((0, 0.64, -0.43), 0.07, 0.07),
], "M_Pale", seg=12, sub=3)
# Mutation spines down the back: tallest over the shoulders.
for z, h, lean in ((-0.24, 0.17, 0.10), (-0.10, 0.22, 0.12), (0.06, 0.18, 0.12), (0.22, 0.15, 0.11), (0.37, 0.11, 0.09)):
    body.spike((0, 0.70, z), (0, 0.72 + h, z + lean), 0.05, 0.075, "M_Glow", seg=4, mid=0.45, bulge=1.25)
# Tail: bushy, carried out behind, pale tip.
body.loft([((0, 0.60, 0.52), 0.05, 0.05), ((0, 0.62, 0.68), 0.075, 0.075), ((0, 0.68, 0.84), 0.08, 0.08),
           ((0, 0.75, 0.96), 0.055, 0.055)], "M_Fur", seg=10, sub=2, caps=("none", "none"))
body.loft([((0, 0.75, 0.96), 0.056, 0.056), ((0, 0.80, 1.05), 0.03, 0.03)], "M_Pale", seg=10,
          caps=("none", "round"))

# Legs: one part each, pivoting at the shoulder or hip, so they can trot in diagonal pairs.
for s, side in ((-1, "L"), (1, "R")):
    x = s * 0.115
    front = m.part("LegF" + side, "Visual", (x, 0.52, -0.17))
    front.loft([((x, 0.55, -0.17), 0.075, 0.10), ((x, 0.36, -0.19), 0.05, 0.058), ((x, 0.20, -0.2), 0.04, 0.045),
                ((x, 0.07, -0.21), 0.042, 0.045), ((x, 0.035, -0.26), 0.05, 0.035)], "M_Fur", seg=8, sub=2)
    x = s * 0.125
    hind = m.part("LegB" + side, "Visual", (x, 0.52, 0.40))
    hind.ball((x, 0.50, 0.40), (0.075, 0.17, 0.14), "M_Fur", seg=10, rings=6, rot=(18, 0, 0))
    hind.loft([((x, 0.40, 0.42), 0.058, 0.07), ((x, 0.25, 0.53), 0.042, 0.048), ((x, 0.17, 0.52), 0.038, 0.042),
               ((x, 0.06, 0.47), 0.04, 0.043), ((x, 0.035, 0.42), 0.05, 0.035)], "M_Fur", seg=8, sub=2)

# ---- Head ---------------------------------------------------------------------------------
head = m.part("Head", "Visual", HEAD)
head.loft([
    ((0, 0.76, -0.36), 0.09, 0.09),
    ((0, 0.775, -0.45), 0.118, 0.105),
    ((0, 0.76, -0.55), 0.095, 0.085),
    ((0, 0.735, -0.62), 0.06, 0.055),
], "M_Fur", seg=12, sub=3, cap_len=0.03)
# Muzzle, lower jaw hanging open a little, nose.
head.loft([((0, 0.74, -0.58), 0.058, 0.05), ((0, 0.73, -0.68), 0.05, 0.043), ((0, 0.725, -0.75), 0.04, 0.035)],
          "M_Pale", seg=10, sub=2, power=2.4, cap_len=0.02)
head.loft([((0, 0.70, -0.55), 0.045, 0.028), ((0, 0.675, -0.64), 0.04, 0.022), ((0, 0.665, -0.71), 0.028, 0.016)],
          "M_Pale", seg=8, sub=2, cap_len=0.015)
head.ball((0, 0.745, -0.775), (0.032, 0.026, 0.026), "M_Nose", seg=8, rings=5)
for s in (-1, 1):
    head.spike((s * 0.075, 0.84, -0.42), (s * 0.115, 1.02, -0.40), 0.052, 0.03, "M_Fur", seg=5, mid=0.45, bulge=1.3)
    head.ball((s * 0.078, 0.805, -0.535), 0.03, "M_Eye", seg=8, rings=6)
    head.box((s * 0.07, 0.84, -0.535), (0.085, 0.025, 0.07), "M_Fur", rot=(16, s * 12, s * 22))

m.export()
