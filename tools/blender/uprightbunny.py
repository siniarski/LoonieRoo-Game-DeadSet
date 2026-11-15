"""Upright Bunny (debug roster): a man-sized glowing rabbit that bites.

Parts: Body, Striker/Head (MeleeChaser pulls it back and snaps it forward).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from deadset_lib import Model, V  # noqa: E402

m = Model("UprightBunny")
m.material("M_Glow", (0.35, 0.95, 0.3), emission=(0.35, 0.95, 0.3), energy=1.4)
m.material("M_Belly", (0.75, 1.0, 0.6), emission=(0.75, 1.0, 0.6), energy=0.8)
m.material("M_Dark", (0.1, 0.3, 0.12))
m.material("M_Eye", (1.0, 0.12, 0.05), unshaded=True)
m.material("M_Tooth", (0.95, 0.95, 0.85))

STRIKER = V(0, 1.48, -0.06)
m.node("Striker", "Visual", STRIKER)

# ---- Body ---------------------------------------------------------------------------------
body = m.part("Body")
body.loft([
    ((0, 0.30, 0.08), 0.19, 0.19),
    ((0, 0.58, 0.05), 0.27, 0.25),
    ((0, 0.92, 0.01), 0.245, 0.215),
    ((0, 1.20, -0.03), 0.18, 0.155),
    ((0, 1.36, -0.05), 0.105, 0.10),
    ((0, 1.46, -0.06), 0.09, 0.09),
], "M_Glow", seg=16, sub=3, caps=("round", "none"))
body.loft([
    ((0, 0.40, -0.07), 0.12, 0.13),
    ((0, 0.62, -0.10), 0.18, 0.17),
    ((0, 0.92, -0.11), 0.165, 0.14),
    ((0, 1.16, -0.11), 0.10, 0.09),
], "M_Belly", seg=12, sub=3)
body.blob((0, 0.42, 0.29), 0.11, "M_Belly", subdiv=2)
for s in (-1, 1):
    x = s * 0.17
    body.ball((x, 0.33, 0.06), (0.135, 0.22, 0.21), "M_Glow", seg=12, rings=8, rot=(-15, 0, 0))
    body.loft([((x, 0.06, 0.16), 0.065, 0.05), ((x, 0.055, -0.05), 0.08, 0.05), ((x, 0.045, -0.3), 0.07, 0.04),
               ((x, 0.04, -0.4), 0.04, 0.028)], "M_Glow", seg=8, sub=2, power=2.4)
    # Arms held up to the chest, dark claws.
    body.loft([((s * 0.2, 1.2, -0.03), 0.07, 0.075), ((s * 0.25, 1.02, -0.09), 0.055, 0.06),
               ((s * 0.215, 1.03, -0.25), 0.05, 0.052)], "M_Glow", seg=8, sub=3)
    body.blob((s * 0.205, 1.04, -0.29), (0.065, 0.06, 0.07), "M_Glow", subdiv=2)
    for i in (-1, 0, 1):
        body.spike((s * 0.205 + i * 0.035, 1.03, -0.33), (s * 0.205 + i * 0.045, 0.97, -0.42), 0.02, 0.02,
                   "M_Dark", seg=4, mid=0.5, bulge=1.1)

# ---- Striker: head ------------------------------------------------------------------------
head = m.part("Head", "Visual/Striker", STRIKER)
head.loft([
    ((0, 1.48, 0.08), 0.10, 0.10),
    ((0, 1.50, -0.04), 0.165, 0.155),
    ((0, 1.47, -0.16), 0.14, 0.12),
    ((0, 1.445, -0.25), 0.075, 0.065),
], "M_Glow", seg=14, sub=3, cap_len=0.035)
for s in (-1, 1):
    head.blob((s * 0.085, 1.435, -0.2), (0.07, 0.06, 0.07), "M_Belly", subdiv=2)
    head.loft([((s * 0.07, 1.6, 0.0), 0.045, 0.03), ((s * 0.1, 1.78, 0.03), 0.065, 0.03),
               ((s * 0.125, 1.95, 0.06), 0.05, 0.024), ((s * 0.135, 2.04, 0.08), 0.018, 0.012)],
              "M_Glow", seg=8, sub=2, caps=("none", "round"), cap_len=0.02)
    head.ball((s * 0.118, 1.535, -0.15), 0.038, "M_Eye", seg=8, rings=6)
    head.box((s * 0.1, 1.59, -0.16), (0.11, 0.03, 0.09), "M_Dark", rot=(14, s * 10, s * 22))
    head.box((s * 0.022, 1.385, -0.265), (0.04, 0.085, 0.022), "M_Tooth", rot=(-8, 0, 0))
head.ball((0, 1.465, -0.285), (0.03, 0.022, 0.02), "M_Dark", seg=8, rings=4)

m.export()
