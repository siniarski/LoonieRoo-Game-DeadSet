"""Cane Toad (debug roster): squat, warty, hops. Glowing poison glands and warts, yellow eyes.

Parts: Body, Head (WeakSpot follows it).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from deadset_lib import Model, V  # noqa: E402

m = Model("CaneToad")
m.material("M_Skin", (0.45, 0.4, 0.22))
m.material("M_Belly", (0.75, 0.7, 0.5))
m.material("M_Wart", (0.35, 0.95, 0.3), emission=(0.35, 0.95, 0.3), energy=2.0)
m.material("M_Eye", (1.0, 0.75, 0.1), emission=(1.0, 0.75, 0.1), energy=2.0, unshaded=True)

HEAD = V(0, 0.27, -0.22)

body = m.part("Body")
body.loft([
    ((0, 0.16, 0.36), 0.12, 0.10),
    ((0, 0.23, 0.22), 0.28, 0.19),
    ((0, 0.25, 0.02), 0.33, 0.21),
    ((0, 0.25, -0.14), 0.27, 0.17),
    ((0, 0.26, -0.24), 0.20, 0.12),
], "M_Skin", seg=18, sub=3, caps=("round", "none"), power=2.2)
body.loft([((0, 0.13, 0.26), 0.20, 0.10), ((0, 0.12, 0.04), 0.29, 0.11), ((0, 0.13, -0.16), 0.24, 0.10),
           ((0, 0.16, -0.3), 0.16, 0.08)], "M_Belly", seg=14, sub=3)
for s in (-1, 1):
    # Folded hind leg and splayed foot.
    body.ball((s * 0.27, 0.14, 0.18), (0.11, 0.12, 0.2), "M_Skin", seg=12, rings=8, rot=(0, s * 20, 0))
    body.loft([((s * 0.33, 0.05, 0.3), 0.06, 0.04), ((s * 0.38, 0.04, 0.14), 0.065, 0.035),
               ((s * 0.42, 0.03, -0.02), 0.07, 0.025)], "M_Skin", seg=8, sub=2)
    for dx, dz in ((0.1, -0.1), (0.04, -0.14), (-0.03, -0.13)):
        body.spike((s * 0.42, 0.03, -0.01), (s * (0.42 + dx), 0.02, -0.01 + dz), 0.025, 0.018, "M_Skin",
                   seg=4, mid=0.6, bulge=1.3)
    # Front leg.
    body.loft([((s * 0.2, 0.18, -0.14), 0.06, 0.065), ((s * 0.27, 0.09, -0.2), 0.045, 0.048),
               ((s * 0.26, 0.03, -0.27), 0.05, 0.03)], "M_Skin", seg=8, sub=2)
    # Poison gland behind each eye: the big glowing sac.
    body.blob((s * 0.16, 0.37, -0.09), (0.075, 0.055, 0.13), "M_Wart", subdiv=2, rot=(0, s * 14, 0))
for x, y, z, r in ((-0.17, 0.42, 0.1, 0.05), (0.14, 0.42, 0.2, 0.045), (0.02, 0.45, 0.05, 0.055),
                   (-0.07, 0.4, 0.27, 0.04), (0.24, 0.36, 0.05, 0.04), (-0.26, 0.34, 0.2, 0.038),
                   (0.0, 0.36, 0.34, 0.035)):
    body.blob((x, y, z), (r, r * 0.75, r), "M_Wart", subdiv=2)

head = m.part("Head", "Visual", HEAD)
head.loft([
    ((0, 0.27, -0.14), 0.20, 0.12),
    ((0, 0.27, -0.25), 0.215, 0.115),
    ((0, 0.25, -0.35), 0.17, 0.085),
    ((0, 0.235, -0.41), 0.10, 0.05),
], "M_Skin", seg=16, sub=3, power=2.3, cap_len=0.03)
head.loft([((0, 0.2, -0.16), 0.18, 0.07), ((0, 0.19, -0.3), 0.17, 0.06), ((0, 0.2, -0.4), 0.10, 0.04)],
          "M_Belly", seg=12, sub=2, cap_len=0.025)
for s in (-1, 1):
    head.ball((s * 0.11, 0.365, -0.27), (0.062, 0.05, 0.065), "M_Skin", seg=10, rings=6)
    head.ball((s * 0.125, 0.375, -0.295), 0.045, "M_Eye", seg=8, rings=6)

m.export()
