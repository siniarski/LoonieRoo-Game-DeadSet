"""Magpie: the swooper. Black and white, wings locked in a dive, a long grey dagger of a beak.

Parts: Body (body, wings, tail), Striker/Head (MeleeChaser pulls it back and stabs with it).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from deadset_lib import Model, V  # noqa: E402

m = Model("Magpie")
m.material("M_Black", (0.05, 0.05, 0.06))
m.material("M_White", (0.92, 0.92, 0.9))
m.material("M_Beak", (0.7, 0.72, 0.75))
m.material("M_Eye", (1.0, 0.12, 0.05), emission=(1.0, 0.12, 0.05), energy=3.0, unshaded=True)

STRIKER = V(0, 2.38, -0.3)
m.node("Striker", "Visual", STRIKER)

# ---- Body ---------------------------------------------------------------------------------
body = m.part("Body")
body.loft([
    ((0, 2.36, -0.30), 0.085, 0.085),
    ((0, 2.32, -0.14), 0.15, 0.15),
    ((0, 2.29, 0.06), 0.165, 0.15),
    ((0, 2.29, 0.26), 0.12, 0.105),
    ((0, 2.30, 0.42), 0.065, 0.05),
], "M_Black", seg=14, sub=3)
# White nape and back saddle, white rump.
body.blob((0, 2.405, -0.02), (0.125, 0.07, 0.17), "M_White", subdiv=2, rot=(-8, 0, 0))
body.blob((0, 2.36, 0.30), (0.085, 0.05, 0.12), "M_White", subdiv=2)
# Tail fan.
for i, x in enumerate((-0.14, -0.07, 0, 0.07, 0.14)):
    body.spike((x * 0.35, 2.30, 0.36), (x, 2.26 - 0.01 * abs(i - 2), 0.80 - 0.04 * abs(i - 2)), 0.05, 0.016,
               "M_Black", seg=4, mid=0.6, bulge=1.5)
# Feet tucked up under the belly.
for s in (-1, 1):
    body.spike((s * 0.06, 2.17, 0.12), (s * 0.07, 2.10, 0.30), 0.03, 0.025, "M_Beak", seg=4, mid=0.5, bulge=1.2)

for s in (-1, 1):
    # Wing: swept back and raised, as in a dive. Chord runs along z, so side = z.
    wing = [
        ((s * 0.08, 2.35, 0.02), 0.17, 0.045),
        ((s * 0.34, 2.42, 0.03), 0.19, 0.032),
        ((s * 0.62, 2.48, 0.10), 0.15, 0.024),
        ((s * 0.84, 2.52, 0.22), 0.085, 0.016),
    ]
    body.loft(wing, "M_Black", seg=10, sub=3, side=(0, 0, 1), caps=("none", "round"), cap_len=0.03)
    # White shoulder band.
    body.loft([((s * 0.2, 2.385, 0.0), 0.15, 0.045), ((s * 0.3, 2.41, 0.0), 0.16, 0.04),
               ((s * 0.4, 2.435, 0.02), 0.145, 0.035)], "M_White", seg=10, sub=2, side=(0, 0, 1))
    # Primary feathers splayed at the tip, and a ragged trailing edge.
    for i, (dx, dz) in enumerate(((0.2, 0.16), (0.16, 0.26), (0.09, 0.33), (0.0, 0.34))):
        body.spike((s * 0.78, 2.505, 0.2), (s * (0.8 + dx), 2.53 + 0.012 * (3 - i), 0.2 + dz), 0.042, 0.012,
                   "M_Black", seg=4, mid=0.55, bulge=1.4, side=(0, 0, 1))
    for i, x in enumerate((0.2, 0.33, 0.46, 0.59, 0.7)):
        y = 2.385 + (x - 0.2) * 0.24
        body.spike((s * x, y, 0.12 + i * 0.02), (s * (x + 0.05), y - 0.01, 0.36 + i * 0.035 - 0.04 * (i % 2)),
                   0.062, 0.013, "M_Black", seg=4, mid=0.55, bulge=1.4)

# ---- Striker: head and beak ---------------------------------------------------------------
head = m.part("Head", "Visual/Striker", STRIKER)
head.loft([
    ((0, 2.37, -0.20), 0.08, 0.08),
    ((0, 2.385, -0.30), 0.115, 0.11),
    ((0, 2.375, -0.40), 0.09, 0.085),
    ((0, 2.36, -0.46), 0.055, 0.05),
], "M_Black", seg=12, sub=3)
head.spike((0, 2.365, -0.44), (0, 2.32, -0.76), 0.048, 0.042, "M_Beak", seg=6, mid=0.5, bulge=1.15)
for s in (-1, 1):
    head.ball((s * 0.092, 2.41, -0.37), 0.032, "M_Eye", seg=8, rings=6)
    head.box((s * 0.078, 2.432, -0.385), (0.075, 0.022, 0.07), "M_Black", rot=(14, s * 10, s * 22))

m.export()
