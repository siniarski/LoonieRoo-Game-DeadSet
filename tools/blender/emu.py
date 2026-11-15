"""Emu: the common swarm enemy. Shaggy body, long neck, swollen glowing lime head at eye height.

Parts: Body, LegL, LegR (shank and foot: swing with the run),
Striker/Head (neck + head: MeleeChaser rears it back and pecks with it).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from deadset_lib import Model, V, arc  # noqa: E402

m = Model("Emu")
m.material("M_Feather", (0.29, 0.22, 0.18))
m.material("M_Dark", (0.15, 0.13, 0.13))
m.material("M_Neck", (0.42, 0.47, 0.55))
m.material("M_Glow", (0.7, 1.0, 0.35), emission=(0.7, 1.0, 0.35), energy=1.3)
m.material("M_Eye", (1.0, 0.12, 0.05), unshaded=True)

# The striker pivots at the base of the neck, so a pitch swings the head in an arc: a real peck.
STRIKER = V(0, 1.08, -0.2)
HEAD = V(0, 1.62, -0.31)
m.node("Striker", "Visual", STRIKER)

# ---- Body ---------------------------------------------------------------------------------
body = m.part("Body")
body.loft([
    ((0, 1.06, -0.40), 0.09, 0.11),
    ((0, 1.03, -0.27), 0.21, 0.22),
    ((0, 1.00, -0.03), 0.31, 0.29),
    ((0, 1.00, 0.24), 0.34, 0.30),
    ((0, 0.95, 0.48), 0.29, 0.26),
    ((0, 0.87, 0.66), 0.18, 0.17),
    ((0, 0.78, 0.78), 0.07, 0.07),
], "M_Feather", seg=18, sub=3)

# Shaggy skirt: chunky drooping feather tufts along the flanks and a tail fan.
for s in (-1, 1):
    for i, z in enumerate((-0.16, 0.0, 0.16, 0.32, 0.47)):
        w = 0.29 + 0.05 * (1 - abs(z - 0.2) / 0.4)
        base = V(s * w * 0.86, 0.88 - 0.03 * i, z)
        tip = V(s * (w + 0.05 + 0.05 * (i % 2)), 0.64 - 0.03 * i - 0.09 * ((i + 1) % 2), z + 0.16 + 0.1 * (i % 2))
        body.spike(base, tip, 0.11, 0.06, "M_Dark", seg=4, mid=0.45, bulge=1.3, side=(0, 0, 1))
    # Upper layer, smaller, same colour as the body: breaks the smooth egg outline.
    for i, z in enumerate((-0.05, 0.2, 0.42)):
        base = V(s * 0.24, 1.15 - 0.05 * i, z)
        tip = V(s * 0.37, 0.96 - 0.07 * i, z + 0.25)
        body.spike(base, tip, 0.1, 0.055, "M_Feather", seg=4, mid=0.5, bulge=1.3, side=(0, 0, 1))
for x, y, tz in ((-0.22, 0.56, 0.86), (-0.08, 0.47, 0.96), (0.08, 0.50, 0.94), (0.22, 0.58, 0.84), (0, 0.62, 1.02)):
    body.spike((x * 0.6, 0.86, 0.58), (x, y, tz), 0.1, 0.06, "M_Dark", seg=4, mid=0.5, bulge=1.3)
# Ruff where the neck leaves the chest.
for x, y, z in ((-0.14, 1.17, -0.27), (0.14, 1.17, -0.27), (0, 1.21, -0.18), (0, 0.97, -0.38)):
    body.spike((x * 0.6, y - 0.02, z + 0.08), (x * 1.6, y - 0.2, z - 0.08), 0.075, 0.05, "M_Feather",
               seg=4, mid=0.5, bulge=1.25)

# Legs. The feathered drumstick is part of the body; the bare shank and foot are their own
# parts, pivoting at the ankle joint just under the plumage, so MeleeChaser can swing them.
for s, name in ((-1, "LegL"), (1, "LegR")):
    x = s * 0.15
    hip, ankle, foot = V(x, 0.86, 0.16), V(x, 0.47, 0.26), V(x, 0.06, 0.08)
    body.loft([(hip, 0.13, 0.14), (hip.lerp(ankle, 0.45), 0.115, 0.13), (hip.lerp(ankle, 0.8), 0.075, 0.08),
               (ankle, 0.055, 0.06)], "M_Feather", seg=10, sub=2, caps=("none", "round"))
    leg = m.part(name, "Visual", ankle)
    leg.ball(ankle + V(0, -0.02, 0.01), (0.078, 0.085, 0.085), "M_Dark", seg=8, rings=5)
    leg.loft([(ankle, 0.06, 0.066), (ankle.lerp(foot, 0.5), 0.052, 0.056), (foot + V(0, 0.03, 0), 0.056, 0.06),
              (foot + V(0, -0.025, -0.03), 0.07, 0.045)], "M_Dark", seg=8, caps=("none", "round"))
    for tx, tz, r in ((0, -0.32, 0.055), (s * 0.17, -0.21, 0.048), (-s * 0.13, -0.22, 0.048)):
        leg.spike(foot + V(0, -0.02, 0), foot + V(tx, -0.05, tz), r, 0.042, "M_Dark", seg=4, mid=0.55, bulge=1.3)

# ---- Striker: neck and head ---------------------------------------------------------------
head = m.part("Head", "Visual/Striker", HEAD)
head.loft([
    ((0, 1.00, -0.16), 0.11, 0.11),
    ((0, 1.10, -0.30), 0.095, 0.10),
    ((0, 1.24, -0.36), 0.07, 0.075),
    ((0, 1.38, -0.34), 0.055, 0.06),
    ((0, 1.50, -0.30), 0.05, 0.055),
    ((0, 1.58, -0.30), 0.05, 0.055),
], "M_Neck", seg=10, sub=2, caps=("none", "none"))

# Swollen mutant head: one big bulb with tumour lobes, so the outline is lumpy, not a ball.
head.blob(HEAD, (0.13, 0.135, 0.155), "M_Glow", subdiv=3)
head.blob(HEAD + V(-0.065, 0.085, 0.05), (0.08, 0.075, 0.085), "M_Glow", subdiv=2)
head.blob(HEAD + V(0.075, 0.05, 0.08), (0.07, 0.07, 0.075), "M_Glow", subdiv=2)
head.blob(HEAD + V(0.03, 0.09, -0.06), (0.06, 0.055, 0.065), "M_Glow", subdiv=2)
# The mutation creeping down the neck.
head.blob((0.04, 1.46, -0.345), 0.038, "M_Glow", subdiv=2)
head.blob((-0.04, 1.33, -0.385), 0.034, "M_Glow", subdiv=2)

# Beak: a blunt dark wedge, upper mandible over a shorter lower one.
head.spike(HEAD + V(0, 0.0, -0.12), HEAD + V(0, -0.085, -0.33), 0.055, 0.04, "M_Dark", seg=6, mid=0.5, bulge=1.25)
head.spike(HEAD + V(0, -0.055, -0.11), HEAD + V(0, -0.105, -0.26), 0.038, 0.022, "M_Dark", seg=4, mid=0.5, bulge=1.2)

for s in (-1, 1):
    head.ball(HEAD + V(s * 0.098, 0.035, -0.09), 0.034, "M_Eye", seg=8, rings=6)

m.export()
