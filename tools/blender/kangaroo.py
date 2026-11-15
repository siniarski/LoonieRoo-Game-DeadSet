"""Kangaroo: the boxer. Upright, heavy haunches, tail as a third leg, mutated glowing fists.

Parts: Body (torso, legs, tail), Head (WeakSpot follows it), Striker/Arms (MeleeChaser punches with them).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from deadset_lib import Model, V  # noqa: E402

m = Model("Kangaroo")
m.material("M_Fur", (0.6, 0.32, 0.2))
m.material("M_Pale", (0.8, 0.62, 0.45))
m.material("M_Dark", (0.2, 0.12, 0.1))
m.material("M_Glow", (0.35, 0.95, 0.3), emission=(0.35, 0.95, 0.3), energy=1.5)
m.material("M_Eye", (1.0, 0.12, 0.05), unshaded=True)

HEAD = V(0, 1.65, -0.08)
STRIKER = V(0, 1.2, -0.2)
m.node("Striker", "Visual", STRIKER)

# ---- Body ---------------------------------------------------------------------------------
body = m.part("Body")
body.loft([
    ((0, 0.50, 0.22), 0.20, 0.20),
    ((0, 0.66, 0.16), 0.27, 0.27),
    ((0, 0.92, 0.07), 0.275, 0.26),
    ((0, 1.18, -0.02), 0.27, 0.22),
    ((0, 1.36, -0.06), 0.22, 0.17),
    ((0, 1.50, -0.07), 0.11, 0.105),
    ((0, 1.62, -0.07), 0.085, 0.085),
], "M_Fur", seg=16, sub=3, caps=("round", "none"))
# Pale belly and chest: a second skin just proud of the front.
body.loft([
    ((0, 0.62, 0.02), 0.13, 0.16),
    ((0, 0.90, -0.07), 0.185, 0.2),
    ((0, 1.17, -0.13), 0.185, 0.16),
    ((0, 1.36, -0.13), 0.12, 0.1),
    ((0, 1.48, -0.1), 0.06, 0.06),
], "M_Pale", seg=12, sub=3)
# Glowing scars slashed across the chest.
body.blob((0.02, 1.13, -0.262), (0.2, 0.04, 0.028), "M_Glow", subdiv=2, rot=(0, 0, 38))
body.blob((-0.05, 0.98, -0.262), (0.11, 0.03, 0.026), "M_Glow", subdiv=2, rot=(0, 0, 38))
# Shoulder caps so the arms have something to hang from.
for s in (-1, 1):
    body.ball((s * 0.235, 1.32, -0.06), (0.105, 0.12, 0.12), "M_Fur", seg=10, rings=6)

# Tail: thick at the root, resting on the ground like a third leg.
body.loft([
    ((0, 0.62, 0.30), 0.17, 0.17),
    ((0, 0.40, 0.52), 0.14, 0.14),
    ((0, 0.18, 0.82), 0.105, 0.10),
    ((0, 0.09, 1.12), 0.07, 0.065),
    ((0, 0.07, 1.36), 0.035, 0.035),
], "M_Fur", seg=12, sub=3, caps=("none", "round"))

for s in (-1, 1):
    x = s * 0.21
    # Haunch, shank, and the long foot.
    body.ball((x, 0.52, 0.16), (0.15, 0.28, 0.25), "M_Fur", seg=12, rings=8, rot=(-24, 0, 0))
    body.loft([((x, 0.40, -0.02), 0.085, 0.10), ((x, 0.24, 0.10), 0.065, 0.075), ((x, 0.10, 0.2), 0.06, 0.065)],
              "M_Fur", seg=8, sub=2)
    body.loft([((x, 0.07, 0.26), 0.06, 0.055), ((x, 0.06, 0.0), 0.075, 0.05), ((x, 0.05, -0.26), 0.07, 0.042),
               ((x, 0.04, -0.4), 0.045, 0.03)], "M_Fur", seg=8, sub=2, power=2.4)
    body.spike((x, 0.045, -0.38), (x, 0.02, -0.53), 0.035, 0.028, "M_Dark", seg=4, mid=0.5, bulge=1.2)

# ---- Head ---------------------------------------------------------------------------------
head = m.part("Head", "Visual", HEAD)
head.loft([
    ((0, 1.66, 0.05), 0.085, 0.09),
    ((0, 1.675, -0.06), 0.12, 0.125),
    ((0, 1.65, -0.19), 0.10, 0.10),
    ((0, 1.61, -0.31), 0.072, 0.07),
    ((0, 1.59, -0.40), 0.058, 0.052),
], "M_Fur", seg=12, sub=3, cap_len=0.035)
head.ball((0, 1.60, -0.435), (0.045, 0.035, 0.03), "M_Dark", seg=8, rings=5)
for s in (-1, 1):
    # Long ears, swept back a touch.
    head.loft([((s * 0.065, 1.74, 0.0), 0.04, 0.03), ((s * 0.10, 1.87, 0.03), 0.06, 0.028),
               ((s * 0.125, 1.98, 0.055), 0.045, 0.022), ((s * 0.135, 2.05, 0.07), 0.015, 0.012)],
              "M_Fur", seg=8, sub=2, caps=("none", "round"), cap_len=0.02)
    head.ball((s * 0.107, 1.675, -0.2), 0.034, "M_Eye", seg=8, rings=6)
    # Heavy brow over each eye.
    head.box((s * 0.085, 1.725, -0.2), (0.11, 0.035, 0.1), "M_Fur", rot=(14, s * 10, s * 20))

# ---- Arms: guard up, mutated fists ---------------------------------------------------------
arms = m.part("Arms", "Visual/Striker", STRIKER)
for s in (-1, 1):
    arms.loft([((s * 0.235, 1.33, -0.08), 0.075, 0.08), ((s * 0.265, 1.14, -0.14), 0.062, 0.066),
               ((s * 0.25, 1.07, -0.22), 0.058, 0.06), ((s * 0.205, 1.17, -0.34), 0.052, 0.055),
               ((s * 0.185, 1.26, -0.40), 0.05, 0.05)], "M_Fur", seg=8, sub=2)
    arms.blob((s * 0.18, 1.30, -0.43), (0.095, 0.1, 0.105), "M_Glow", subdiv=2)
    arms.blob((s * 0.215, 1.355, -0.41), 0.05, "M_Glow", subdiv=2)

m.export()
