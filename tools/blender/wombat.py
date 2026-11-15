"""Wombat: the charger. A barrel of muscle on four stumps with a blocky battering-ram head.

Parts: Body, LegFR, LegBL, LegBR, Paw/Leg (front-left leg: also scrapes the ground in the wind-up),
Glow/Patches (mutation boils: Wombat.cs brightens them), HeadPivot/Head (lowers and jabs).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from deadset_lib import Model, V  # noqa: E402

m = Model("Wombat")
m.material("M_Fur", (0.44, 0.35, 0.28))
m.material("M_Dark", (0.24, 0.2, 0.18))
m.material("M_Pale", (0.62, 0.53, 0.45))
m.material("M_Glow", (0.35, 0.95, 0.3), emission=(0.35, 0.95, 0.3), energy=1.5)
m.material("M_Eye", (1.0, 0.12, 0.05), unshaded=True)

HEAD = V(0, 1.34, -0.72)
PAW = V(-0.40, 0, -0.42)
m.node("Paw", "Visual", PAW)
m.node("Glow", "Visual", (0, 0, 0))
m.node("HeadPivot", "Visual", HEAD)

# Torso spine, rump to neck: (z, y, rx, ry). Also used to seat the glow patches on the hide.
SPINE = [
    (1.00, 0.80, 0.26, 0.28),
    (0.84, 0.86, 0.50, 0.45),
    (0.48, 0.94, 0.62, 0.54),
    (0.06, 1.00, 0.64, 0.57),
    (-0.34, 1.07, 0.62, 0.58),
    (-0.62, 1.12, 0.50, 0.47),
    (-0.82, 1.16, 0.32, 0.32),
]
POWER = 2.5


def hide(z, angle_deg):
    """Point on the torso surface and the outward angle there. 90 = top, 0 = its right (+x) flank."""
    for a, b in zip(SPINE[:-1], SPINE[1:]):
        if b[0] <= z <= a[0]:
            t = (z - a[0]) / (b[0] - a[0])
            y, rx, ry = (a[i] + (b[i] - a[i]) * t for i in (1, 2, 3))
            break
    ang = math.radians(angle_deg)
    e = 2.0 / POWER
    cs = math.copysign(abs(math.cos(ang)) ** e, math.cos(ang))
    sn = math.copysign(abs(math.sin(ang)) ** e, math.sin(ang))
    return V(rx * cs, y + ry * sn, z)


# ---- Body ---------------------------------------------------------------------------------
body = m.part("Body")
body.loft([((0, y, z), rx, ry) for z, y, rx, ry in SPINE], "M_Fur", seg=20, sub=3, power=POWER)


def leg(part, x, z, front):
    s = 1 if x > 0 else -1
    top = V(x, 0.70, z)
    part.loft([(top, 0.26, 0.28), (V(x, 0.42, z), 0.225, 0.24), (V(x + s * 0.01, 0.16, z - 0.02), 0.205, 0.225),
               (V(x + s * 0.01, 0.05, z - 0.05), 0.235, 0.27)], "M_Dark", seg=12, sub=2,
              caps=("none", "flat"), power=2.4)
    # Digging claws.
    for i in (-1, 0, 1):
        base = V(x + s * 0.01 + i * 0.14, 0.075, z - 0.24)
        part.spike(base, base + V(i * 0.04, -0.055, -0.22 if front else -0.16), 0.065, 0.055, "M_Pale",
                   seg=4, mid=0.5, bulge=1.25)


# One part per leg, pivoting where it meets the body, so Wombat.cs can swing them in
# diagonal pairs. The front-left one hangs under Visual/Paw, which also scrapes in the wind-up.
HIP_Y = 0.62
leg(m.part("LegFR", "Visual", (0.40, HIP_Y, -0.42)), 0.40, -0.42, True)
leg(m.part("LegBL", "Visual", (-0.40, HIP_Y, 0.56)), -0.40, 0.56, False)
leg(m.part("LegBR", "Visual", (0.40, HIP_Y, 0.56)), 0.40, 0.56, False)
leg(m.part("Leg", "Visual/Paw", (-0.40, HIP_Y, -0.42)), -0.40, -0.42, True)

# ---- Head: a blunt wedge skull, pale muzzle, wide nose pad, angry brow ----------------------
head = m.part("Head", "Visual/HeadPivot", HEAD)
head.loft([
    ((0, 1.34, -0.40), 0.30, 0.29),
    ((0, 1.36, -0.60), 0.385, 0.34),
    ((0, 1.35, -0.80), 0.375, 0.32),
    ((0, 1.30, -0.96), 0.29, 0.24),
    ((0, 1.27, -1.06), 0.22, 0.17),
], "M_Fur", seg=16, sub=3, power=2.7, caps=("none", "round"))
head.loft([
    ((0, 1.25, -0.90), 0.235, 0.17),
    ((0, 1.235, -1.02), 0.25, 0.185),
    ((0, 1.225, -1.14), 0.215, 0.16),
], "M_Pale", seg=14, sub=2, power=2.8, caps=("none", "round"), cap_len=0.07)
head.ball((0, 1.30, -1.2), (0.125, 0.07, 0.06), "M_Dark", seg=10, rings=6)
for s in (-1, 1):
    # Ears: small, rounded, set wide.
    head.loft([((s * 0.27, 1.60, -0.56), 0.095, 0.05), ((s * 0.325, 1.69, -0.545), 0.1, 0.045),
               ((s * 0.355, 1.75, -0.535), 0.075, 0.035)], "M_Fur", seg=8, caps=("none", "round"),
              side=(1, 0, 0), cap_len=0.035)
    head.ball((s * 0.33, 1.695, -0.585), (0.05, 0.055, 0.02), "M_Dark", seg=8, rings=4, rot=(0, 0, -s * 25))
    # Brow ridge, slanting down to the nose: reads as a scowl from the front.
    head.box((s * 0.235, 1.535, -0.935), (0.24, 0.06, 0.13), "M_Dark", rot=(20, s * 14, s * 22))
    head.ball((s * 0.255, 1.445, -0.965), 0.07, "M_Eye", seg=8, rings=6)

# ---- Mutation boils -------------------------------------------------------------------------
glow = m.part("Patches", "Visual/Glow", (0, 0, 0))


def boil(z, angle, w, l, t=0.085, subdiv=2, yaw=0.0):
    c = hide(z, angle)
    out = V(math.cos(math.radians(angle)), math.sin(math.radians(angle)), 0)
    c = c + out * 0.035
    t += 0.03
    glow.blob(c, (w, t, l), "M_Glow", subdiv=subdiv, rot=(0, yaw, angle - 90))


# Back and shoulders (seen as it charges past), flanks, and the chest either side of the head.
boil(0.22, 68, 0.26, 0.34, 0.1, 3, 20)
boil(0.52, 80, 0.13, 0.15, 0.08)
boil(-0.32, 118, 0.22, 0.28, 0.1, 3, -25)
boil(-0.56, 100, 0.11, 0.12, 0.08)
boil(0.20, 176, 0.20, 0.30, 0.09, 3)
boil(0.58, 168, 0.11, 0.13, 0.07)
boil(-0.20, 8, 0.17, 0.22, 0.09, 3)
boil(0.14, -8, 0.09, 0.11, 0.07)
for s in (-1, 1):
    glow.blob((s * 0.43, 0.98, -0.66), (0.15, 0.2, 0.085), "M_Glow", subdiv=2, rot=(0, -s * 38, 0))

m.export()
