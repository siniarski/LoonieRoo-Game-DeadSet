"""Procedural low-poly enemy modelling helpers for Deadset.

Run a model script with:
    blender --background --python tools/blender/<enemy>.py

All coordinates in the model scripts are in GODOT space: x right, y up, z back (the model
faces -z), metres, origin at the feet. The library converts to Blender space on the way in,
and the glTF exporter converts back, so the numbers in a script are the numbers in Godot.

A model is a list of Parts. Each Part becomes one MeshInstance3D in the enemy scene; its
pivot is the node position. Parts are rigid: the behaviour scripts move the nodes.
Each flat colour is one material, shared by every part of the enemy.

Output per enemy, in assets/models/enemies/:
    <Enemy>.glb    the meshes
    <Enemy>.json   manifest: nodes, parts, materials (read by extract_meshes.gd and build_scenes.py)
"""
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT_DIR = os.path.join(ROOT, "assets", "models", "enemies")


def V(x, y=None, z=None):
    if y is None:
        return Vector(x)
    return Vector((x, y, z))


def g2b(v):
    """Godot (x, y up, z back) -> Blender (x, y forward-ish, z up)."""
    return Vector((v[0], -v[2], v[1]))


def catmull(p0, p1, p2, p3, t):
    t2, t3 = t * t, t * t * t
    return 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2
                  + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)


class Model:
    def __init__(self, name):
        self.name = name
        self.materials = {}     # name -> spec
        self.material_order = []
        self.nodes = []         # empty pivots: {name, parent, position}
        self.parts = []

    def material(self, name, color, emission=None, energy=0.0, unshaded=False):
        self.materials[name] = {
            "color": list(color),
            "emission": list(emission) if emission else None,
            "energy": energy,
            "unshaded": unshaded,
        }
        self.material_order.append(name)

    def node(self, name, parent, position):
        """An empty Node3D the scripts animate (Striker, HeadPivot, Paw, Glow)."""
        self.nodes.append({"name": name, "parent": parent, "position": list(position)})

    def part(self, name, parent="Visual", pivot=(0, 0, 0), sharp_angle=50.0):
        """pivot is in model space; the node's local position is worked out from its parents."""
        p = Part(self, name, parent, V(pivot), sharp_angle)
        self.parts.append(p)
        return p

    # ---- export -------------------------------------------------------------------------

    def _node_world(self, path):
        """Model-space position of a node path like 'Visual/Striker'."""
        pos = Vector((0, 0, 0))
        names = path.split("/")
        for i in range(1, len(names)):
            parent = "/".join(names[:i])
            match = [n for n in self.nodes if n["name"] == names[i] and n["parent"] == parent]
            if not match:
                raise ValueError("unknown node %s under %s" % (names[i], parent))
            pos += Vector(match[0]["position"])
        return pos

    def export(self):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        mats = {}
        for name in self.material_order:
            spec = self.materials[name]
            m = bpy.data.materials.new(name)
            m.use_nodes = True
            bsdf = m.node_tree.nodes.get("Principled BSDF")
            c = spec["color"]
            bsdf.inputs["Base Color"].default_value = (c[0], c[1], c[2], 1.0)
            bsdf.inputs["Roughness"].default_value = 0.85
            if spec["emission"]:
                e = spec["emission"]
                bsdf.inputs["Emission Color"].default_value = (e[0], e[1], e[2], 1.0)
                bsdf.inputs["Emission Strength"].default_value = spec["energy"]
            mats[name] = m

        manifest = {"name": self.name, "materials": self.materials, "nodes": self.nodes, "parts": []}
        total = 0
        for part in self.parts:
            used = [n for n in self.material_order if n in part.used_materials]
            me = bpy.data.meshes.new(part.name)
            # Material slots in a fixed order; remap the indices the pieces stored.
            remap = {}
            for i, n in enumerate(used):
                me.materials.append(mats[n])
                remap[part.material_index[n]] = i
            for f in part.bm.faces:
                f.material_index = remap[f.material_index]
            bmesh.ops.triangulate(part.bm, faces=part.bm.faces[:])
            part.bm.to_mesh(me)
            part.bm.free()
            me.set_sharp_from_angle(angle=math.radians(part.sharp_angle))
            ob = bpy.data.objects.new(part.name, me)
            ob.location = g2b(part.pivot)
            bpy.context.scene.collection.objects.link(ob)

            local = part.pivot - self._node_world(part.parent)
            manifest["parts"].append({
                "name": part.name,
                "parent": part.parent,
                "position": [round(v, 4) for v in local],
                "materials": used,
                "verts_blender": len(me.vertices),
                "tris": len(me.polygons),
                "cast_shadow": part.cast_shadow,
            })
            total += len(me.vertices)
            print("PART %-12s %5d verts %5d tris  %s" % (part.name, len(me.vertices), len(me.polygons), used))
        print("MODEL %s: %d blender verts, %d parts" % (self.name, total, len(self.parts)))

        os.makedirs(OUT_DIR, exist_ok=True)
        bpy.ops.export_scene.gltf(
            filepath=os.path.join(OUT_DIR, self.name + ".glb"),
            export_format="GLB",
            export_yup=True,
            export_apply=True,
            export_texcoords=False,
            export_normals=True,
            export_materials="EXPORT",
            export_animations=False,
            export_skins=False,
            export_morph=False,
            export_cameras=False,
            export_lights=False,
        )
        with open(os.path.join(OUT_DIR, self.name + ".json"), "w") as f:
            json.dump(manifest, f, indent="\t")
        if "--save-blend" in sys.argv:
            bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT_DIR, self.name + ".blend"))


class Part:
    def __init__(self, model, name, parent, pivot, sharp_angle):
        self.model = model
        self.name = name
        self.parent = parent
        self.pivot = pivot
        self.sharp_angle = sharp_angle
        self.cast_shadow = True
        self.bm = bmesh.new()
        self.material_index = {}
        self.used_materials = set()

    # ---- low level ----------------------------------------------------------------------

    def _mat(self, name):
        if name not in self.model.materials:
            raise ValueError("unknown material " + name)
        if name not in self.material_index:
            self.material_index[name] = len(self.material_index)
        self.used_materials.add(name)
        return self.material_index[name]

    def _vert(self, p):
        return self.bm.verts.new(g2b(Vector(p) - self.pivot))

    def _finish(self, faces, mat, smooth):
        index = self._mat(mat)
        faces = [f for f in faces if f is not None]
        # Face normals must be current, or recalc can turn a moved shape inside out.
        self.bm.normal_update()
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        for f in faces:
            f.material_index = index
            f.smooth = smooth
        return faces

    def _face(self, verts):
        try:
            return self.bm.faces.new(verts)
        except ValueError:
            return None

    # ---- shapes -------------------------------------------------------------------------

    def loft(self, rings, mat, seg=12, sub=1, caps=("round", "round"), power=2.0, smooth=True,
             side=(1, 0, 0), cap_len=None):
        """Skin of elliptical rings along a spine.

        rings: list of (center, rx, ry) or (center, r). rx is the width along `side`
        (x for anything lying in the y/z plane), ry the other way.
        sub > 1 inserts Catmull-Rom rings between the control rings (rounder forms).
        caps: "round", "flat", "point" (the ring collapses to its centre) or "none" (open,
        for ends buried in another shape) for the start and the end.
        power: 2 = ellipse, higher = boxier cross-section.
        """
        ctrl = []
        for r in rings:
            c = V(r[0])
            rx = r[1]
            ry = r[2] if len(r) > 2 else r[1]
            ctrl.append((c, rx, ry))

        if sub > 1 and len(ctrl) > 2:
            dense = []
            n = len(ctrl)
            for i in range(n - 1):
                a, b = ctrl[max(i - 1, 0)], ctrl[i]
                c, d = ctrl[i + 1], ctrl[min(i + 2, n - 1)]
                for k in range(sub):
                    t = k / sub
                    dense.append((catmull(a[0], b[0], c[0], d[0], t),
                                  max(catmull(a[1], b[1], c[1], d[1], t), 0.001),
                                  max(catmull(a[2], b[2], c[2], d[2], t), 0.001)))
            dense.append(ctrl[-1])
            ctrl = dense

        n = len(ctrl)
        tangents = []
        for i in range(n):
            t = ctrl[min(i + 1, n - 1)][0] - ctrl[max(i - 1, 0)][0]
            tangents.append(t.normalized())

        # Parallel-transported frame, so a curving spine does not twist the rings.
        u_ref = V(side)
        if abs(tangents[0].dot(u_ref)) > 0.9:
            u_ref = V(0, 0, 1)
        frames = []
        for t in tangents:
            u = (u_ref - t * u_ref.dot(t)).normalized()
            v = t.cross(u).normalized()
            frames.append((u, v))
            u_ref = u

        def ring_verts(c, u, v, rx, ry):
            out = []
            for k in range(seg):
                a = math.tau * k / seg
                cs, sn = math.cos(a), math.sin(a)
                if power != 2.0:
                    e = 2.0 / power
                    cs = math.copysign(abs(cs) ** e, cs)
                    sn = math.copysign(abs(sn) ** e, sn)
                out.append(self._vert(c + u * (rx * cs) + v * (ry * sn)))
            return out

        loops = []
        poles = [None, None]

        def cap(end):
            i = 0 if end == 0 else n - 1
            c, rx, ry = ctrl[i]
            u, v = frames[i]
            t = tangents[i] * (-1 if end == 0 else 1)
            kind = caps[end]
            extra = []
            if kind == "round":
                length = cap_len if cap_len is not None else min(rx, ry) * 0.9
                for ang in (35.0, 65.0):
                    a = math.radians(ang)
                    extra.append(ring_verts(c + t * (length * math.sin(a)), u, v,
                                            rx * math.cos(a), ry * math.cos(a)))
                pole = self._vert(c + t * length)
            elif kind == "none":
                pole = None  # left open: for ends buried inside another shape
            else:
                pole = self._vert(c)
            return extra, pole

        start_extra, start_pole = cap(0)
        end_extra, end_pole = cap(1)
        for ring in reversed(start_extra):
            loops.append(ring)
        for i in range(n):
            if (i == 0 and caps[0] == "point") or (i == n - 1 and caps[1] == "point"):
                continue
            c, rx, ry = ctrl[i]
            u, v = frames[i]
            loops.append(ring_verts(c, u, v, rx, ry))
        for ring in end_extra:
            loops.append(ring)

        faces = []
        for a, b in zip(loops[:-1], loops[1:]):
            for k in range(seg):
                k2 = (k + 1) % seg
                faces.append(self._face((a[k], a[k2], b[k2], b[k])))
        for k in range(seg):
            k2 = (k + 1) % seg
            if start_pole is not None:
                faces.append(self._face((start_pole, loops[0][k2], loops[0][k])))
            if end_pole is not None:
                faces.append(self._face((end_pole, loops[-1][k], loops[-1][k2])))
        return self._finish(faces, mat, smooth)

    def blob(self, center, radii, mat, subdiv=2, rot=(0, 0, 0), smooth=True):
        """Icosphere ellipsoid. subdiv 1 = 12 verts, 2 = 42, 3 = 162. rot in degrees (Godot axes)."""
        if isinstance(radii, (int, float)):
            radii = (radii, radii, radii)
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=1.0)
        r = Euler([math.radians(a) for a in rot], "XYZ").to_matrix()
        c = V(center)
        for v in res["verts"]:
            p = Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2]))
            v.co = g2b(r @ p + c - self.pivot)
        faces = list({f for v in res["verts"] for f in v.link_faces})
        return self._finish(faces, mat, smooth)

    def ball(self, center, radii, mat, seg=12, rings=8, rot=(0, 0, 0), smooth=True):
        """UV-sphere ellipsoid (poles along y before rotation): cleaner rows than blob()."""
        if isinstance(radii, (int, float)):
            radii = (radii, radii, radii)
        r = Euler([math.radians(a) for a in rot], "XYZ").to_matrix()
        c = V(center)
        loops = []
        for j in range(1, rings):
            a = math.pi * j / rings
            y, s = math.cos(a), math.sin(a)
            loops.append([self._vert(c + r @ Vector((radii[0] * s * math.cos(math.tau * k / seg),
                                                     radii[1] * y,
                                                     radii[2] * s * math.sin(math.tau * k / seg))))
                          for k in range(seg)])
        top = self._vert(c + r @ Vector((0, radii[1], 0)))
        bottom = self._vert(c + r @ Vector((0, -radii[1], 0)))
        faces = []
        for a, b in zip(loops[:-1], loops[1:]):
            for k in range(seg):
                k2 = (k + 1) % seg
                faces.append(self._face((a[k], a[k2], b[k2], b[k])))
        for k in range(seg):
            k2 = (k + 1) % seg
            faces.append(self._face((top, loops[0][k2], loops[0][k])))
            faces.append(self._face((bottom, loops[-1][k], loops[-1][k2])))
        return self._finish(faces, mat, smooth)

    def spike(self, base, tip, rx, ry=None, mat=None, seg=6, smooth=False, mid=0.5, bulge=1.0,
              side=(1, 0, 0)):
        """Tapered spike from base to tip (claws, beaks, ears, feather tufts). Flat-shaded by default."""
        base, tip = V(base), V(tip)
        ry = rx if ry is None else ry
        m = base.lerp(tip, mid)
        rings = [(base, rx, ry), (m, rx * bulge * (1 - mid * 0.6), ry * bulge * (1 - mid * 0.6)),
                 (tip, 0.001, 0.001)]
        return self.loft(rings, mat, seg=seg, caps=("none", "point"), smooth=smooth, side=side)

    def box(self, center, size, mat, rot=(0, 0, 0), smooth=False, taper=1.0):
        """Box; taper scales the -z (front) face in x and y."""
        r = Euler([math.radians(a) for a in rot], "XYZ").to_matrix()
        c = V(center)
        hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
        vs = []
        for sz in (-1, 1):
            k = taper if sz < 0 else 1.0
            for sy in (-1, 1):
                for sx in (-1, 1):
                    vs.append(self._vert(c + r @ Vector((sx * hx * k, sy * hy * k, sz * hz))))
        quads = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
        faces = [self._face([vs[i] for i in q]) for q in quads]
        return self._finish(faces, mat, smooth)


def arc(p0, p1, bend, n=5):
    """n points from p0 to p1 bowed sideways by the vector `bend` (peak offset at the middle)."""
    p0, p1, bend = V(p0), V(p1), V(bend)
    return [p0.lerp(p1, i / (n - 1)) + bend * math.sin(math.pi * i / (n - 1)) for i in range(n)]
