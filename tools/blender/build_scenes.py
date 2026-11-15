"""Rewrites the Visual subtree of each enemy scene from its model manifest.

    python tools/blender/build_scenes.py [Enemy ...]

For every assets/models/enemies/<Enemy>.json it edits scenes/enemies/<Enemy>.tscn:
  - drops the old primitive meshes, inline materials and every node under Visual
  - adds the manifest's pivot nodes (Striker, HeadPivot, ...) and one MeshInstance3D per part,
    pointing at assets/models/enemies/<Enemy>/<Part>.res
Everything else (root node exports, collision shapes, components) is left untouched,
and the script can be re-run after a model is regenerated.
"""
import json
import os
import re
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
MODELS = os.path.join(ROOT, "assets", "models", "enemies")
SCENES = os.path.join(ROOT, "scenes", "enemies")


def fmt(v):
    s = ("%.4f" % v).rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s


def vec(p):
    return "Vector3(%s, %s, %s)" % (fmt(p[0]), fmt(p[1]), fmt(p[2]))


def build(enemy):
    with open(os.path.join(MODELS, enemy + ".json")) as f:
        manifest = json.load(f)
    path = os.path.join(SCENES, enemy + ".tscn")
    with open(path, encoding="utf-8") as f:
        text = f.read()

    blocks = [b for b in re.split(r"(?m)^(?=\[)", text) if b.strip()]
    header, ext, sub, nodes = blocks[0], [], [], []
    for b in blocks[1:]:
        if b.startswith("[ext_resource"):
            if "res://assets/models/" not in b:
                ext.append(b)
        elif b.startswith("[sub_resource"):
            kind = re.match(r'\[sub_resource type="(\w+)"', b).group(1)
            if not (kind.endswith("Mesh") or kind == "StandardMaterial3D"):
                sub.append(b)
        elif b.startswith("[node"):
            parent = re.search(r'parent="([^"]*)"', b.split("\n")[0])
            if parent and (parent.group(1) == "Visual" or parent.group(1).startswith("Visual/")):
                continue
            nodes.append(b)
        else:
            nodes.append(b)

    new_ext, visual = [], []
    for n in manifest["nodes"]:
        visual.append('[node name="%s" type="Node3D" parent="%s"]\nposition = %s\n\n'
                      % (n["name"], n["parent"], vec(n["position"])))
    for p in manifest["parts"]:
        rid = "mesh_" + p["name"]
        new_ext.append('[ext_resource type="ArrayMesh" path="res://assets/models/enemies/%s/%s.res" id="%s"]\n'
                       % (enemy, p["name"], rid))
        lines = ['[node name="%s" type="MeshInstance3D" parent="%s"]' % (p["name"], p["parent"])]
        if any(abs(c) > 1e-6 for c in p["position"]):
            lines.append("position = %s" % vec(p["position"]))
        if not p.get("cast_shadow", True):
            lines.append("cast_shadow = 0")
        lines.append('mesh = ExtResource("%s")' % rid)
        visual.append("\n".join(lines) + "\n\n")
    # Parents before children.
    visual.sort(key=lambda b: re.search(r'parent="([^"]*)"', b).group(1).count("/"))

    out_nodes = []
    for b in nodes:
        out_nodes.append(b)
        if b.startswith('[node name="Visual"'):
            out_nodes.extend(visual)

    ext = [e.rstrip("\n") + "\n" for e in ext] + new_ext
    steps = len(ext) + len(sub) + 1
    header = re.sub(r"\[gd_scene[^\]]*\]", "[gd_scene load_steps=%d format=3]" % steps, header.strip()) + "\n\n"
    result = header + "".join(ext) + "\n" + "".join(sub) + "".join(out_nodes)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(result.rstrip("\n") + "\n")
    print("%s.tscn: %d nodes, %d parts" % (enemy, len(manifest["nodes"]), len(manifest["parts"])))


if __name__ == "__main__":
    names = sys.argv[1:] or sorted(f[:-5] for f in os.listdir(MODELS) if f.endswith(".json"))
    for name in names:
        build(name)
