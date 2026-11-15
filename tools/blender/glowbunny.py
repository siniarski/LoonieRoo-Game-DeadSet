"""Glow Bunny (debug roster): the small glowing rabbit that hops and nips.

Parts: Body, Head (WeakSpot follows it).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from deadset_lib import Model, V  # noqa: E402

m = Model("GlowBunny")
m.material("M_Glow", (0.35, 0.95, 0.3), emission=(0.35, 0.95, 0.3), energy=1.6)
m.material("M_Belly", (0.75, 1.0, 0.6), emission=(0.75, 1.0, 0.6), energy=0.8)
m.material("M_Dark", (0.1, 0.3, 0.12))
m.material("M_Eye", (1.0, 0.12, 0.05), unshaded=True)

HEAD = V(0, 0.44, -0.22)

body = m.part("Body")
body.loft([
    ((0, 0.20, 0.30), 0.12, 0.12),
    ((0, 0.26, 0.14), 0.215, 0.21),
    ((0, 0.25, -0.06), 0.18, 0.18),
    ((0, 0.33, -0.18), 0.12, 0.12),
    ((0, 0.42, -0.24), 0.09, 0.09),
], "M_Glow", seg=16, sub=3, caps=("round", "none"))
body.blob((0, 0.30, 0.36), 0.085, "M_Belly", subdiv=2)
body.loft([((0, 0.15, 0.12), 0.11, 0.07), ((0, 0.16, -0.08), 0.11, 0.08), ((0, 0.27, -0.2), 0.07, 0.06)],
          "M_Belly", seg=10, sub=3)
for s in (-1, 1):
    body.ball((s * 0.15, 0.17, 0.16), (0.095, 0.15, 0.18), "M_Glow", seg=12, rings=8, rot=(20, 0, 0))
    body.loft([((s * 0.16, 0.04, 0.24), 0.05, 0.035), ((s * 0.16, 0.035, 0.08), 0.06, 0.035),
               ((s * 0.16, 0.03, -0.08), 0.04, 0.025)], "M_Glow", seg=8, sub=2)
    body.loft([((s * 0.09, 0.2, -0.12), 0.045, 0.05), ((s * 0.1, 0.08, -0.17), 0.036, 0.038),
               ((s * 0.1, 0.03, -0.21), 0.042, 0.028)], "M_Glow", seg=8, sub=2)

head = m.part("Head", "Visual", HEAD)
head.loft([
    ((0, 0.44, -0.11), 0.085, 0.085),
    ((0, 0.455, -0.21), 0.13, 0.125),
    ((0, 0.43, -0.31), 0.105, 0.09),
    ((0, 0.41, -0.38), 0.055, 0.048),
], "M_Glow", seg=14, sub=3, cap_len=0.03)
head.ball((0, 0.425, -0.405), (0.024, 0.018, 0.016), "M_Dark", seg=8, rings=4)
for s in (-1, 1):
    head.blob((s * 0.06, 0.405, -0.34), (0.05, 0.042, 0.05), "M_Belly", subdiv=2)
    head.loft([((s * 0.06, 0.54, -0.18), 0.035, 0.024), ((s * 0.085, 0.68, -0.15), 0.052, 0.024),
               ((s * 0.105, 0.82, -0.11), 0.04, 0.02), ((s * 0.115, 0.9, -0.09), 0.015, 0.01)],
              "M_Glow", seg=8, sub=2, caps=("none", "round"), cap_len=0.02)
    head.ball((s * 0.092, 0.475, -0.3), 0.032, "M_Eye", seg=8, rings=6)
    head.box((s * 0.078, 0.52, -0.31), (0.085, 0.024, 0.07), "M_Dark", rot=(14, s * 10, s * 22))

m.export()
