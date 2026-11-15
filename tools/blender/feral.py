"""Feral (debug roster): a mutated bloke in a singlet, stubbies and work boots. Mullet included.

Parts: Body (legs, torso, neck), Head (WeakSpot follows it), Striker/Arms (MeleeChaser swings them).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from deadset_lib import Model, V  # noqa: E402

m = Model("Feral")
m.material("M_Skin", (0.5, 0.6, 0.42))
m.material("M_Singlet", (0.2, 0.32, 0.6))
m.material("M_Stubbies", (0.55, 0.45, 0.3))
m.material("M_Boot", (0.2, 0.15, 0.1))
m.material("M_Eye", (0.6, 1.0, 0.3), emission=(0.6, 1.0, 0.3), energy=3.0, unshaded=True)
m.material("M_Boil", (0.35, 0.95, 0.3), emission=(0.35, 0.95, 0.3), energy=1.5)

HEAD = V(0, 1.58, -0.05)
STRIKER = V(0, 1.25, -0.05)
m.node("Striker", "Visual", STRIKER)

# ---- Body ---------------------------------------------------------------------------------
body = m.part("Body")
for s in (-1, 1):
    x = s * 0.115
    body.loft([((x, 0.62, 0.02), 0.09, 0.095), ((x, 0.42, 0.0), 0.075, 0.085), ((x, 0.24, 0.02), 0.062, 0.068)],
              "M_Skin", seg=10, sub=2, caps=("none", "none"))
    # Work boot: shaft, then a blunt toe.
    body.loft([((x, 0.27, 0.02), 0.078, 0.085), ((x, 0.12, 0.02), 0.08, 0.09), ((x, 0.03, 0.02), 0.085, 0.1)],
              "M_Boot", seg=10, power=2.4, caps=("flat", "flat"))
    body.loft([((x, 0.06, 0.02), 0.085, 0.06), ((x, 0.06, -0.12), 0.09, 0.06), ((x, 0.055, -0.2), 0.08, 0.05)],
              "M_Boot", seg=10, power=2.6, caps=("none", "round"), cap_len=0.03)
    # Stubbies: short, wide legs.
    body.loft([((x, 0.84, 0.02), 0.12, 0.13), ((x, 0.70, 0.02), 0.125, 0.13), ((x, 0.585, 0.02), 0.12, 0.125)],
              "M_Stubbies", seg=12, power=2.4, caps=("none", "flat"))
body.loft([((0, 0.74, 0.02), 0.22, 0.14), ((0, 0.86, 0.02), 0.225, 0.15), ((0, 0.92, 0.02), 0.2, 0.14)],
          "M_Stubbies", seg=14, power=2.4)
# Singlet over a beer gut, shoulders rolled forward.
body.loft([
    ((0, 0.86, 0.02), 0.20, 0.15),
    ((0, 1.00, -0.02), 0.225, 0.20),
    ((0, 1.16, -0.01), 0.23, 0.175),
    ((0, 1.31, 0.0), 0.225, 0.14),
    ((0, 1.40, -0.01), 0.14, 0.10),
], "M_Singlet", seg=16, sub=3, power=2.3)
body.loft([((0, 1.37, -0.01), 0.07, 0.07), ((0, 1.46, -0.03), 0.065, 0.068), ((0, 1.54, -0.04), 0.065, 0.068)],
          "M_Skin", seg=10, caps=("none", "none"))
for s in (-1, 1):
    body.ball((s * 0.255, 1.33, 0.0), (0.095, 0.1, 0.105), "M_Skin", seg=10, rings=6)
# Boils: one swelling out of the shoulder, one through the singlet.
body.blob((0.2, 1.43, 0.02), (0.085, 0.08, 0.085), "M_Boil", subdiv=2)
body.blob((0.27, 1.42, -0.04), 0.045, "M_Boil", subdiv=2)
body.blob((-0.1, 1.08, -0.2), (0.08, 0.075, 0.05), "M_Boil", subdiv=2)

# ---- Head ---------------------------------------------------------------------------------
head = m.part("Head", "Visual", HEAD)
head.ball(HEAD + V(0, 0.02, 0.0), (0.118, 0.135, 0.13), "M_Skin", seg=14, rings=10)
head.loft([((0, 1.52, -0.06), 0.095, 0.075), ((0, 1.47, -0.10), 0.085, 0.07), ((0, 1.44, -0.13), 0.06, 0.05)],
          "M_Skin", seg=10, sub=2, power=2.4)
head.box((0, 1.575, -0.185), (0.045, 0.06, 0.05), "M_Skin", rot=(-12, 0, 0))
head.box((0, 1.49, -0.165), (0.085, 0.018, 0.03), "M_Boot")
# The mullet: short on top, long down the back of the neck.
head.loft([((0, 1.72, -0.04), 0.085, 0.03), ((0, 1.70, 0.05), 0.115, 0.05), ((0, 1.58, 0.09), 0.115, 0.055),
           ((0, 1.44, 0.11), 0.10, 0.045), ((0, 1.34, 0.13), 0.07, 0.03)], "M_Boot", seg=10, sub=2)
for s in (-1, 1):
    head.ball((s * 0.12, 1.58, -0.02), (0.022, 0.04, 0.03), "M_Skin", seg=6, rings=4)
    head.ball((s * 0.052, 1.61, -0.155), 0.03, "M_Eye", seg=8, rings=6)
    head.box((s * 0.052, 1.65, -0.16), (0.075, 0.022, 0.05), "M_Boot", rot=(10, s * 8, s * 18))

# ---- Arms: reaching ---------------------------------------------------------------------
arms = m.part("Arms", "Visual/Striker", STRIKER)
for s in (-1, 1):
    arms.loft([((s * 0.265, 1.33, -0.0), 0.075, 0.08), ((s * 0.29, 1.22, -0.2), 0.062, 0.066),
               ((s * 0.27, 1.23, -0.38), 0.055, 0.058), ((s * 0.24, 1.26, -0.50), 0.045, 0.048)],
              "M_Skin", seg=8, sub=3)
    arms.blob((s * 0.235, 1.27, -0.55), (0.06, 0.065, 0.07), "M_Skin", subdiv=2)
    for i in (-1, 0, 1):
        arms.spike((s * 0.235 + i * 0.035, 1.27, -0.59), (s * 0.235 + i * 0.05, 1.23, -0.7), 0.02, 0.02,
                   "M_Skin", seg=4, mid=0.5, bulge=1.1)

m.export()
