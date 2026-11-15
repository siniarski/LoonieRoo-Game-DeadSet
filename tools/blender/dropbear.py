"""Drop Bear (debug roster): a koala gone wrong. Round ears, big nose, fangs, raking claws.

Parts: Body, Head (WeakSpot follows it), Striker/Arms (MeleeChaser swipes with them).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from deadset_lib import Model, V  # noqa: E402

m = Model("DropBear")
m.material("M_Fur", (0.5, 0.5, 0.52))
m.material("M_Pale", (0.85, 0.85, 0.82))
m.material("M_Nose", (0.08, 0.07, 0.07))
m.material("M_Eye", (1.0, 0.12, 0.05), emission=(1.0, 0.12, 0.05), energy=2.5, unshaded=True)
m.material("M_Glow", (0.35, 0.95, 0.3), emission=(0.35, 0.95, 0.3), energy=1.4)

HEAD = V(0, 0.98, -0.08)
STRIKER = V(0, 0.65, -0.15)
m.node("Striker", "Visual", STRIKER)

# ---- Body ---------------------------------------------------------------------------------
body = m.part("Body")
body.loft([
    ((0, 0.16, 0.10), 0.24, 0.24),
    ((0, 0.42, 0.06), 0.34, 0.31),
    ((0, 0.68, 0.02), 0.29, 0.26),
    ((0, 0.86, -0.03), 0.19, 0.17),
], "M_Fur", seg=16, sub=3, caps=("round", "none"))
body.loft([((0, 0.28, -0.14), 0.16, 0.13), ((0, 0.48, -0.17), 0.21, 0.16), ((0, 0.70, -0.16), 0.15, 0.12)],
          "M_Pale", seg=12, sub=3)
body.blob((-0.04, 0.57, -0.315), (0.115, 0.03, 0.022), "M_Glow", subdiv=2, rot=(0, 0, 25))
body.blob((0.0, 0.45, -0.32), (0.075, 0.025, 0.02), "M_Glow", subdiv=2, rot=(0, 0, 25))
for s in (-1, 1):
    body.ball((s * 0.26, 0.2, 0.0), (0.14, 0.17, 0.22), "M_Fur", seg=12, rings=8)
    body.loft([((s * 0.27, 0.07, -0.08), 0.08, 0.06), ((s * 0.28, 0.06, -0.24), 0.085, 0.055),
               ((s * 0.28, 0.05, -0.34), 0.07, 0.045)], "M_Fur", seg=8, sub=2)
    for i in (-1, 0, 1):
        body.spike((s * 0.28 + i * 0.045, 0.05, -0.36), (s * 0.28 + i * 0.06, 0.02, -0.47), 0.024, 0.022,
                   "M_Pale", seg=4, mid=0.5, bulge=1.1)
    body.ball((s * 0.265, 0.74, -0.02), (0.11, 0.115, 0.12), "M_Fur", seg=10, rings=6)

# ---- Head ---------------------------------------------------------------------------------
head = m.part("Head", "Visual", HEAD)
head.ball(HEAD, (0.215, 0.19, 0.195), "M_Fur", seg=16, rings=10)
head.ball((0, 0.955, -0.275), (0.065, 0.095, 0.055), "M_Nose", seg=10, rings=6)
for s in (-1, 1):
    # Big round fluffy ears with pale insides.
    head.ball((s * 0.225, 1.13, -0.03), (0.125, 0.125, 0.055), "M_Fur", seg=12, rings=6, rot=(90, 0, 0))
    head.ball((s * 0.23, 1.13, -0.065), (0.075, 0.08, 0.03), "M_Pale", seg=10, rings=4, rot=(90, 0, 0))
    for dx, dy in ((0.11, 0.09), (0.14, 0.0), (0.1, -0.09)):
        head.spike((s * 0.26, 1.13, -0.02), (s * (0.26 + dx), 1.13 + dy, -0.02), 0.045, 0.03, "M_Fur",
                   seg=4, mid=0.5, bulge=1.3, side=(0, 0, 1))
    head.ball((s * 0.105, 1.03, -0.235), 0.038, "M_Eye", seg=8, rings=6)
    head.box((s * 0.1, 1.085, -0.235), (0.12, 0.035, 0.08), "M_Fur", rot=(16, s * 12, s * 24))
    head.spike((s * 0.05, 0.875, -0.235), (s * 0.055, 0.775, -0.255), 0.022, 0.022, "M_Pale", seg=4, mid=0.5,
               bulge=1.1)

# ---- Arms: raised to rake -----------------------------------------------------------------
arms = m.part("Arms", "Visual/Striker", STRIKER)
for s in (-1, 1):
    arms.loft([((s * 0.275, 0.76, -0.02), 0.09, 0.095), ((s * 0.33, 0.62, -0.17), 0.075, 0.08),
               ((s * 0.30, 0.62, -0.33), 0.07, 0.072)], "M_Fur", seg=8, sub=3)
    arms.blob((s * 0.295, 0.63, -0.38), (0.085, 0.08, 0.085), "M_Fur", subdiv=2)
    for i in (-1, 0, 1):
        arms.spike((s * 0.295 + i * 0.045, 0.64, -0.43), (s * 0.295 + i * 0.06, 0.56, -0.58), 0.026, 0.024,
                   "M_Pale", seg=5, mid=0.5, bulge=1.15)

m.export()
