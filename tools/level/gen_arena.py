"""Generates scenes/Arena.tscn: the Bore Yard, a 64 x 64 m outback depot.

Layout (north = -Z):
- West: Container Alley, a 3.6 m lane between two container rows, fed by the NW spawn, with
  a side opening halfway. Flocks string out in a line: rivet gun territory.
- East: the Shearing Shed, walled on all sides, two doorways (west and north). Hold a door.
- Centre-south: the bore tank ringed by troughs with gaps, a loop to kite a flock around.
- North yard: car wrecks and hay bales, open with scattered cover.
- South yard: zigzag besser walls (low cover), pallets, a ute.
- The windmill is a tall landmark you can see from anywhere.
Six spawn gates around the edge.
Run from the project root: python gen_arena.py
"""
import math

HALF = 32.0          # inner half-size of the yard
WALL_H = 5.0

subs = []      # sub_resource text
nodes = []     # node text
sub_ids = {}


def sub(sid, text):
    sub_ids[sid] = True
    subs.append(f'[sub_resource type="{text[0]}" id="{sid}"]\n{text[1]}\n')


def mat(sid, color, rough=0.8, extra=""):
    sub(sid, ("StandardMaterial3D", f"albedo_color = Color({color[0]}, {color[1]}, {color[2]}, 1)\nroughness = {rough}\n{extra}"))


def vec(v):
    return "Vector3(%s)" % ", ".join("%g" % round(x, 4) for x in v)


def box(name, parent, pos, size, material, rot_y=0.0, collide=True, rot=None, cast_shadow=True):
    lines = [f'[node name="{name}" type="CSGBox3D" parent="{parent}"]', f"position = {vec(pos)}"]
    if rot is not None:
        lines.append(f"rotation = {vec(rot)}")
    elif rot_y:
        lines.append(f"rotation = Vector3(0, {round(rot_y, 4)}, 0)")
    if collide:
        lines.append("use_collision = true")
    if not cast_shadow:
        lines.append("cast_shadow = 0")
    lines.append(f"size = {vec(size)}")
    lines.append(f'material = SubResource("{material}")')
    nodes.append("\n".join(lines) + "\n")


def cyl(name, parent, pos, radius, height, material, collide=True, sides=16, rot=None):
    lines = [f'[node name="{name}" type="CSGCylinder3D" parent="{parent}"]', f"position = {vec(pos)}"]
    if rot is not None:
        lines.append(f"rotation = {vec(rot)}")
    if collide:
        lines.append("use_collision = true")
    lines += [f"radius = {radius}", f"height = {height}", f"sides = {sides}", f'material = SubResource("{material}")']
    nodes.append("\n".join(lines) + "\n")


def group(name, parent):
    nodes.append(f'[node name="{name}" type="Node3D" parent="{parent}"]\n')


# ---- Materials ------------------------------------------------------------------------
subs.append('''[sub_resource type="ProceduralSkyMaterial" id="SkyMat"]
sky_top_color = Color(0.42, 0.3, 0.26, 1)
sky_horizon_color = Color(0.86, 0.58, 0.38, 1)
ground_bottom_color = Color(0.25, 0.17, 0.12, 1)
ground_horizon_color = Color(0.86, 0.58, 0.38, 1)
sun_angle_max = 20.0
''')
subs.append('''[sub_resource type="Sky" id="Sky"]
sky_material = SubResource("SkyMat")
''')
subs.append('''[sub_resource type="Environment" id="Env"]
background_mode = 2
sky = SubResource("Sky")
ambient_light_source = 3
tonemap_mode = 2
fog_enabled = true
fog_light_color = Color(0.8, 0.55, 0.38, 1)
fog_density = 0.006
glow_enabled = true
glow_intensity = 0.7
glow_hdr_threshold = 1.0
''')
subs.append('''[sub_resource type="FastNoiseLite" id="DirtNoise"]
frequency = 0.035
''')
subs.append('''[sub_resource type="Gradient" id="DirtRamp"]
colors = PackedColorArray(0.7, 0.7, 0.7, 1, 1, 1, 1, 1)
''')
subs.append('''[sub_resource type="NoiseTexture2D" id="DirtTex"]
width = 256
height = 256
seamless = true
color_ramp = SubResource("DirtRamp")
noise = SubResource("DirtNoise")
''')
TRI = "albedo_texture = SubResource(\"DirtTex\")\nuv1_scale = Vector3({s}, {s}, {s})\nuv1_triplanar = true\nuv1_world_triplanar = true\n"
mat("FloorMat", (0.62, 0.34, 0.2), 1.0, TRI.format(s=0.25))
mat("TrackMat", (0.48, 0.25, 0.15), 1.0, TRI.format(s=0.3))
mat("WallMat", (0.46, 0.48, 0.5), 0.8, TRI.format(s=0.5))
mat("TimberMat", (0.66, 0.48, 0.27), 0.9)
mat("ContainerRustMat", (0.6, 0.22, 0.14), 0.7)
mat("ContainerTealMat", (0.22, 0.45, 0.45), 0.7)
mat("ContainerCreamMat", (0.82, 0.74, 0.55), 0.7)
mat("ConcreteMat", (0.68, 0.66, 0.6), 1.0)
mat("TankMat", (0.35, 0.45, 0.28), 0.6, "metallic = 0.3\n")
mat("ShedMat", (0.55, 0.6, 0.58), 0.6, "metallic = 0.4\n")
mat("HayMat", (0.88, 0.72, 0.36), 1.0)
mat("WreckBlueMat", (0.32, 0.42, 0.55), 0.7)
mat("WreckWhiteMat", (0.78, 0.76, 0.7), 0.7)
mat("SteelMat", (0.3, 0.3, 0.32), 0.5, "metallic = 0.6\n")
mat("GatePostMat", (0.85, 0.75, 0.2), 0.6)
sub("SpawnPadMat", ("StandardMaterial3D", "albedo_color = Color(0.9, 0.15, 0.1, 1)\nemission_enabled = true\nemission = Color(0.9, 0.15, 0.1, 1)\nemission_energy_multiplier = 0.6\n"))
sub("NavMesh", ("NavigationMesh", "geometry_parsed_geometry_type = 1\nagent_radius = 0.5\nagent_height = 1.25\nagent_max_climb = 0.25\n"))

# ---- Scene root ------------------------------------------------------------------------
nodes.append('''[node name="Arena" type="Node3D"]
''')
nodes.append('''[node name="WorldEnvironment" type="WorldEnvironment" parent="."]
environment = SubResource("Env")
''')
nodes.append('''[node name="Sun" type="DirectionalLight3D" parent="."]
rotation = Vector3(-0.9, 0.6, 0)
light_color = Color(1, 0.88, 0.72, 1)
light_energy = 1.2
shadow_enabled = true
directional_shadow_max_distance = 80.0
''')
nodes.append('''[node name="Navigation" type="NavigationRegion3D" parent="."]
navigation_mesh = SubResource("NavMesh")
script = ExtResource("1_navbaker")
''')

G = "Navigation/Geometry"
group("Geometry", "Navigation")
size = HALF * 2 + 2
box("Floor", G, (0, -0.5, 0), (size, 1, size), "FloorMat")
box("WallNorth", G, (0, WALL_H / 2, -HALF - 0.5), (size, WALL_H, 1), "WallMat")
box("WallSouth", G, (0, WALL_H / 2, HALF + 0.5), (size, WALL_H, 1), "WallMat")
box("WallWest", G, (-HALF - 0.5, WALL_H / 2, 0), (1, WALL_H, HALF * 2), "WallMat")
box("WallEast", G, (HALF + 0.5, WALL_H / 2, 0), (1, WALL_H, HALF * 2), "WallMat")

# ---- Container Alley (west) ------------------------------------------------------------
A = "Navigation/ContainerAlley"
group("ContainerAlley", "Navigation")
outer_x, inner_x = -30.8, -24.8          # alley runs between x = -29.6 and -26.0 (3.6 m)
colours = ["ContainerRustMat", "ContainerTealMat", "ContainerCreamMat"]
for i, z in enumerate([-22, -16, -10, -4, 2, 8]):
    box(f"Outer{i}", A, (outer_x, 1.3, z), (2.4, 2.6, 6), colours[i % 3])
    if z != -4:   # side opening halfway down the alley
        box(f"Inner{i}", A, (inner_x, 1.3, z), (2.4, 2.6, 6), colours[(i + 1) % 3])
# A few stacked on top for a skyline.
box("OuterStack1", A, (outer_x, 3.9, -16), (2.4, 2.6, 6), "ContainerCreamMat")
box("OuterStack2", A, (outer_x, 3.9, 2), (2.4, 2.6, 6), "ContainerRustMat")
box("InnerStack", A, (inner_x, 3.9, -10), (2.4, 2.6, 6), "ContainerTealMat")
# Close the pocket north of the inner row, so the alley spawn can only empty down the alley.
box("InnerCapNorth", A, (inner_x, 1.3, -28.5), (2.4, 2.6, 7), "ContainerRustMat")

# ---- Shearing Shed (east) --------------------------------------------------------------
S = "Navigation/ShearingShed"
group("ShearingShed", "Navigation")
x0, x1, z0, z1, h, t = 14.0, 27.0, -10.0, 4.0, 3.2, 0.4
door_w = (z0 + 5.0, z0 + 8.2)     # west doorway (3.2 m)
door_n = (x0 + 4.0, x0 + 7.2)     # north doorway (3.2 m)
# West wall, split by its door.
box("WallW_A", S, (x0, h / 2, (z0 + door_w[0]) / 2), (t, h, door_w[0] - z0), "ShedMat")
box("WallW_B", S, (x0, h / 2, (door_w[1] + z1) / 2), (t, h, z1 - door_w[1]), "ShedMat")
# North wall, split by its door.
box("WallN_A", S, ((x0 + door_n[0]) / 2, h / 2, z0), (door_n[0] - x0, h, t), "ShedMat")
box("WallN_B", S, ((door_n[1] + x1) / 2, h / 2, z0), (x1 - door_n[1], h, t), "ShedMat")
box("WallS", S, ((x0 + x1) / 2, h / 2, z1), (x1 - x0 + t, h, t), "ShedMat")
box("WallE", S, (x1, h / 2, (z0 + z1) / 2), (t, h, z1 - z0), "ShedMat")
# Wool bales inside: something to back onto.
box("Bale1", S, (24.5, 0.55, 1.5), (1.6, 1.1, 1.1), "HayMat")
box("Bale2", S, (24.5, 0.55, 0.2), (1.6, 1.1, 1.1), "HayMat")
box("Bale3", S, (24.5, 1.65, 0.85), (1.6, 1.1, 1.1), "HayMat")
box("ShearingBoard", S, (19.5, 0.35, -1.5), (4, 0.7, 1.2), "TimberMat")

# ---- Bore tank loop (centre-south) -----------------------------------------------------
B = "Navigation/BoreTank"
group("BoreTank", "Navigation")
cx, cz = 9.0, 12.0
cyl("Tank", B, (cx, 2, cz), 2.2, 4, "TankMat", sides=20)
for i in range(5):
    a = i / 5 * math.tau + 0.3
    r = 5.0
    box(f"Trough{i}", B, (cx + math.cos(a) * r, 0.4, cz + math.sin(a) * r), (3.2, 0.8, 0.9), "SteelMat",
        rot_y=-a + math.pi / 2)

# ---- North yard ------------------------------------------------------------------------
N = "Navigation/NorthYard"
group("NorthYard", "Navigation")
box("WreckBlue", N, (7, 0.7, -19), (4.2, 1.4, 2), "WreckBlueMat", rot_y=0.35)
box("WreckBlueCab", N, (6.6, 1.65, -18.85), (1.8, 0.5, 1.8), "WreckBlueMat", rot_y=0.35)
box("WreckWhite", N, (-7, 0.7, -23), (4.2, 1.4, 2), "WreckWhiteMat", rot_y=-0.8)
box("WreckWhiteCab", N, (-7.4, 1.65, -22.6), (1.8, 0.5, 1.8), "WreckWhiteMat", rot_y=-0.8)
for i, (x, z) in enumerate([(17, -25), (18.7, -25), (17.85, -25)]):
    box(f"Hay{i}", N, (x, 0.55 if i < 2 else 1.65, z), (1.6, 1.1, 1.1), "HayMat")
box("Hay3", N, (-16, 0.55, -14), (1.1, 1.1, 1.6), "HayMat")
box("Hay4", N, (-16, 0.55, -12.4), (1.1, 1.1, 1.6), "HayMat")
cyl("FuelBowser", N, (-2, 1.0, -26), 1.0, 3.6, "ContainerRustMat", rot=(0, 0, math.pi / 2))
box("CrateLow", N, (-6, 0.5, -6), (2, 1, 2), "TimberMat")
box("CrateTall", N, (-8, 1, -6), (2, 2, 2), "TimberMat")
box("ContainerNE", N, (25, 1.3, -24), (6, 2.6, 2.4), "ContainerTealMat", rot_y=0.25)

# ---- South yard ------------------------------------------------------------------------
So = "Navigation/SouthYard"
group("SouthYard", "Navigation")
box("Besser1", So, (-10, 0.65, 21), (7, 1.3, 0.8), "ConcreteMat", rot_y=0.5)
box("Besser2", So, (-2, 0.65, 23), (7, 1.3, 0.8), "ConcreteMat", rot_y=-0.5)
box("Besser3", So, (-18, 0.65, 18), (6, 1.3, 0.8), "ConcreteMat", rot_y=-0.4)
box("Pallets1", So, (20, 0.3, 15), (1.6, 0.6, 1.6), "TimberMat", rot_y=0.4)
box("Pallets2", So, (-14, 0.45, 27), (1.6, 0.9, 1.6), "TimberMat", rot_y=0.2)
box("Ute", So, (21, 0.7, 25), (4.2, 1.4, 2), "WreckWhiteMat", rot_y=0.6)
box("UteTray", So, (20.0, 1.2, 24.4), (2.0, 0.5, 2.0), "SteelMat", rot_y=0.6)
box("ContainerSE", So, (27, 1.3, 13), (2.4, 2.6, 6), "ContainerRustMat")

# ---- Windmill landmark (visual tower, solid legs) ---------------------------------------
W = "Windmill"
nodes.append('[node name="Windmill" type="Node3D" parent="."]\nposition = Vector3(-12, 0, 13)\n')
for i, (lx, lz) in enumerate([(-1, -1), (1, -1), (-1, 1), (1, 1)]):
    box(f"Leg{i}", "Navigation/Geometry", (-12 + lx * 0.9, 4.5, 13 + lz * 0.9), (0.2, 9, 0.2), "SteelMat",
        rot=(lz * 0.06, 0, -lx * 0.06))
box("Platform", W, (0, 9.1, 0), (1.4, 0.2, 1.4), "SteelMat", collide=False)
box("Tail", W, (0, 9.6, 1.6), (0.1, 0.8, 2.4), "SteelMat", collide=False)
cyl("Wheel", W, (0, 9.6, -0.5), 1.8, 0.12, "ContainerCreamMat", collide=False, sides=18, rot=(math.pi / 2, 0, 0))
cyl("Hub", W, (0, 9.6, -0.6), 0.3, 0.3, "SteelMat", collide=False, sides=10, rot=(math.pi / 2, 0, 0))

# ---- Dirt tracks (visual only) ---------------------------------------------------------
T = "Tracks"
group("Tracks", ".")
box("TrackNS", T, (3, 0.01, 0), (2.4, 0.02, 60), "TrackMat", collide=False, cast_shadow=False)
box("TrackEW", T, (0, 0.01, -14), (56, 0.02, 2.4), "TrackMat", collide=False, cast_shadow=False)

# ---- Spawn gates -----------------------------------------------------------------------
group("SpawnPoints", ".")
spawns = {
    "SpawnAlley": (-27.8, -29.5, 0),         # head of Container Alley (faces south)
    "SpawnNorth": (6, -29.5, 0),
    "SpawnEast": (29.5, -16, math.pi / 2),
    "SpawnSouthEast": (29.5, 24, math.pi / 2),
    "SpawnSouth": (-4, 29.5, 0),
    "SpawnWest": (-29.5, 22, math.pi / 2),
}
for name, (x, z, yaw) in spawns.items():
    nodes.append(f'[node name="{name}" type="Marker3D" parent="SpawnPoints" groups=["enemy_spawn"]]\nposition = {vec((x, 0, z))}\n')
    box("Pad", f"SpawnPoints/{name}", (0, 0.02, 0), (3, 0.04, 3), "SpawnPadMat", collide=False, cast_shadow=False)
    # Gate frame against the wall behind the pad.
    on_ns_wall = abs(z) > 28            # north/south walls run along X
    if on_ns_wall:
        back, along = (0.0, 2.0 * (1 if z > 0 else -1)), (1.0, 0.0)
        lintel = (4.2, 0.35, 0.35)
    else:
        back, along = (2.0 * (1 if x > 0 else -1), 0.0), (0.0, 1.0)
        lintel = (0.35, 0.35, 4.2)
    for side, sgn in (("PostL", -1), ("PostR", 1)):
        box(side, f"SpawnPoints/{name}", (back[0] + along[0] * 1.9 * sgn, 1.9, back[1] + along[1] * 1.9 * sgn),
            (0.35, 3.8, 0.35), "GatePostMat", collide=False)
    box("Lintel", f"SpawnPoints/{name}", (back[0], 3.9, back[1]), lintel, "GatePostMat", collide=False)

header = f'[gd_scene load_steps={len(sub_ids) + 6 + 2} format=3]\n\n[ext_resource type="Script" path="res://scripts/World/NavigationBaker.cs" id="1_navbaker"]\n\n'
open("scenes/Arena.tscn", "w").write(header + "\n".join(subs) + "\n" + "\n".join(nodes))
print("nodes:", len(nodes))
