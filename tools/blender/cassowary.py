"""Cassowary: the kicker. Tall black bird, blue neck, red wattles, horn casque, glowing dagger claw.

Parts: Body, LegL (shank and foot: swings with the run), Head (WeakSpot follows it),
Striker/Leg (the right shank and foot: swings with the run, and MeleeChaser kicks with it).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from deadset_lib import Model, V  # noqa: E402

m = Model("Cassowary")
m.material("M_Feather", (0.08, 0.08, 0.09))
m.material("M_Neck", (0.15, 0.4, 0.8))
m.material("M_Wattle", (0.85, 0.12, 0.1))
m.material("M_Casque", (0.45, 0.35, 0.2))
m.material("M_Leg", (0.35, 0.33, 0.25))
m.material("M_Glow", (0.7, 1.0, 0.3), emission=(0.7, 1.0, 0.3), energy=1.5)
m.material("M_Eye", (0.7, 1.0, 0.3), emission=(0.7, 1.0, 0.3), energy=3.0, unshaded=True)

HEAD = V(0, 1.76, -0.3)
# The striker pivots at the joint of the kicking leg, so a pitch swings the foot up and out.
STRIKER = V(0.17, 0.50, 0.22)
m.node("Striker", "Visual", STRIKER)

# ---- Body ---------------------------------------------------------------------------------
body = m.part("Body")
body.loft([
    ((0, 1.16, -0.36), 0.11, 0.13),
    ((0, 1.13, -0.22), 0.25, 0.27),
    ((0, 1.10, 0.02), 0.34, 0.35),
    ((0, 1.09, 0.28), 0.37, 0.36),
    ((0, 1.02, 0.52), 0.31, 0.30),
    ((0, 0.92, 0.70), 0.19, 0.19),
    ((0, 0.82, 0.80), 0.07, 0.08),
], "M_Feather", seg=18, sub=3)
# Coarse hanging plumage.
for s in (-1, 1):
    for i, z in enumerate((-0.14, 0.04, 0.22, 0.4, 0.55)):
        w = 0.31 + 0.05 * (1 - abs(z - 0.2) / 0.4)
        base = V(s * w * 0.86, 0.95 - 0.03 * i, z)
        tip = V(s * (w + 0.05 + 0.04 * (i % 2)), 0.62 - 0.03 * i - 0.07 * ((i + 1) % 2), z + 0.18 + 0.08 * (i % 2))
        body.spike(base, tip, 0.12, 0.065, "M_Feather", seg=4, mid=0.45, bulge=1.3, side=(0, 0, 1))
for x, y, tz in ((-0.22, 0.56, 0.9), (-0.08, 0.46, 1.0), (0.08, 0.49, 0.98), (0.22, 0.58, 0.88), (0, 0.6, 1.06)):
    body.spike((x * 0.6, 0.9, 0.6), (x, y, tz), 0.11, 0.065, "M_Feather", seg=4, mid=0.5, bulge=1.3)

# Neck: bare blue skin, with the red wattles hanging at the throat.
body.loft([
    ((0, 1.12, -0.18), 0.12, 0.12),
    ((0, 1.26, -0.30), 0.10, 0.105),
    ((0, 1.42, -0.34), 0.075, 0.08),
    ((0, 1.58, -0.31), 0.062, 0.066),
    ((0, 1.72, -0.29), 0.06, 0.065),
], "M_Neck", seg=10, sub=2, caps=("none", "none"))
for s in (-1, 1):
    body.loft([((s * 0.03, 1.56, -0.375), 0.022, 0.022), ((s * 0.04, 1.45, -0.41), 0.034, 0.032),
               ((s * 0.045, 1.36, -0.42), 0.03, 0.028)], "M_Wattle", seg=6, sub=2)
# Feather ruff at the base of the neck.
for x, y, z in ((-0.15, 1.27, -0.26), (0.15, 1.27, -0.26), (0, 1.33, -0.16), (0, 1.07, -0.36)):
    body.spike((x * 0.6, y - 0.02, z + 0.08), (x * 1.6, y - 0.2, z - 0.08), 0.08, 0.05, "M_Feather",
               seg=4, mid=0.5, bulge=1.25)


KNEE_Y, KNEE_Z = 0.50, 0.22


def leg(s, name, parent, glow_claw):
    """Drumstick on the body; shank and foot as their own part, pivoting at the joint under the plumage."""
    x = s * 0.17
    hip, ankle, foot = V(x, 0.95, 0.14), V(x, KNEE_Y, KNEE_Z), V(x, 0.06, 0.08)
    body.loft([(hip, 0.14, 0.15), (hip.lerp(ankle, 0.45), 0.125, 0.14), (hip.lerp(ankle, 0.8), 0.085, 0.09),
               (ankle, 0.062, 0.066)], "M_Feather", seg=10, sub=2, caps=("round", "round"))
    part = m.part(name, parent, ankle)
    part.ball(ankle + V(0, -0.02, 0.01), (0.085, 0.09, 0.09), "M_Leg", seg=8, rings=5)
    part.loft([(ankle, 0.066, 0.07), (ankle.lerp(foot, 0.5), 0.058, 0.062), (foot + V(0, 0.03, 0), 0.06, 0.064),
               (foot + V(0, -0.025, -0.03), 0.075, 0.048)], "M_Leg", seg=8, caps=("none", "round"))
    for tx, tz, r in ((0, -0.33, 0.058), (s * 0.17, -0.22, 0.05)):
        part.spike(foot + V(0, -0.02, 0), foot + V(tx, -0.05, tz), r, 0.044, "M_Leg", seg=4, mid=0.55, bulge=1.3)
    # Inner toe: the dagger claw. The claw on the kicking leg is mutated and glows.
    inner = foot + V(-s * 0.1, -0.01, -0.12)
    part.spike(foot + V(0, -0.02, 0), inner, 0.05, 0.044, "M_Leg", seg=4, mid=0.6, bulge=1.3)
    part.spike(inner + V(0, 0.0, 0.03), inner + V(-s * 0.05, 0.03, -0.26), 0.036, 0.03,
               "M_Glow" if glow_claw else "M_Leg", seg=5, mid=0.5, bulge=1.15)


leg(-1, "LegL", "Visual", False)
leg(1, "Leg", "Visual/Striker", True)

# ---- Head ---------------------------------------------------------------------------------
head = m.part("Head", "Visual", HEAD)
head.loft([
    ((0, 1.75, -0.19), 0.075, 0.08),
    ((0, 1.77, -0.29), 0.10, 0.10),
    ((0, 1.76, -0.39), 0.085, 0.08),
    ((0, 1.745, -0.46), 0.055, 0.05),
], "M_Neck", seg=12, sub=3)
# Casque: a tall horn blade over the skull.
head.loft([
    ((0, 1.80, -0.47), 0.018, 0.03),
    ((0, 1.875, -0.41), 0.04, 0.085),
    ((0, 1.93, -0.31), 0.048, 0.125),
    ((0, 1.91, -0.20), 0.04, 0.10),
    ((0, 1.84, -0.12), 0.02, 0.04),
], "M_Casque", seg=8, sub=3, power=2.3)
head.spike((0, 1.755, -0.42), (0, 1.715, -0.66), 0.05, 0.042, "M_Leg", seg=6, mid=0.5, bulge=1.2)
for s in (-1, 1):
    head.ball((s * 0.088, 1.79, -0.37), 0.034, "M_Eye", seg=8, rings=6)

m.export()
