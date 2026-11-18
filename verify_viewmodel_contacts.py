"""blender -b --python-exit-code 1 -P verify_viewmodel_contacts.py"""
from pathlib import Path
import math
import re
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parent
MODELS = ROOT / "assets/models"
SLOTS = ("belt", "solder", "uzi", "shotgun")


def check_solder_pads(vertices, prop):
    # Check each pad near its own distal phalanx against the exported tape.
    # A single minimum for the entire hand can miss a floating little finger.
    sys.path.insert(0, str(ROOT))
    from generate_anatomy import contact_pose, DIGITS, JOINT, solder_palm_fit
    pose, _ = contact_pose("fps_solder", "r")
    knuckles = [pose[name + "_01_r"][1] for name in DIGITS[1:]]
    assert all(a.z > b.z for a, b in zip(knuckles, knuckles[1:])), "Index must face the guard"
    assert all(p.x < -.18 and p.y > -.15 for p in knuckles), "MCP row must enclose the handle"
    for i, digit in enumerate(DIGITS, 1):
        origin, joint, rotation = pose[digit + "_03_r"]
        center = solder_palm_fit(joint + .65 * (rotation @ (JOINT[f"r-finger-{i}-4"] - origin)))
        candidates = [p for p in vertices if (p-center).length < .012]
        gap = min(prop.find_nearest(p)[3] for p in candidates)
        print(f"  {digit} pad to tape: {gap*1000:.2f} mm")
        assert gap <= .003, f"{digit} pad floats {gap*1000:.2f} mm from tape"


def read_meshes(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(MODELS / (name + ".glb")))
    meshes = {}
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        assert obj.name not in meshes
        vertices = []
        for v in obj.data.vertices:
            x, y, z = obj.matrix_world @ v.co
            vertices.append(Vector((x, z, -y)))
        obj.data.calc_loop_triangles()
        triangles = [tuple(t.vertices) for t in obj.data.loop_triangles]
        assert triangles and vertices, name
        meshes[obj.name] = vertices, triangles
    assert meshes, name
    return meshes


def weapon_mesh(slot):
    # Both draws share the final pose and assembly offset. Compare in arm-local
    # space by undoing the X reflection from VisualEngine3D.Arm.
    source = (ROOT / "WeaponAndCombatView.cs").read_text(encoding="utf-8-sig")
    name = "weapon_" + slot + "_90s"
    draw = re.search(r'VisualEngine3D\.Asset\("' + name +
                     r'",\s*(\w+),\s*([\d.]+)f?,\s*([\d.]+)f?,', source)
    position, yaw, scale = draw.groups()
    origin = re.search(r'Vector3 ' + position + r' = new Vector3\(([^)]+)\) \+ ', source)
    if origin is not None:
        raw_offset = origin[1]
    else:
        gun = re.search(r'Vector3 receiver=offset\+\(uzi \? new Vector3\(([^)]+)\) : new Vector3\(([^)]+)\)\);', source)
        assert gun is not None, (slot, position)
        raw_offset = gun[1 if slot == "uzi" else 2]
    offset = Vector(tuple(float(x.strip().rstrip("f")) for x in raw_offset.split(",")))
    angle = math.radians(float(yaw))
    verts, faces = [], []
    for points, triangles in read_meshes(name).values():
        base = len(verts)
        for x, y, z in points:
            p = offset + float(scale) * Vector((math.cos(angle)*x + math.sin(angle)*z,
                                                y, math.cos(angle)*z - math.sin(angle)*x))
            verts.append(Vector((-p.x, p.y, p.z)))
        faces.extend(tuple(base+i for i in f) for f in triangles)
    return verts, faces


def verify():
    failed = False
    for number, slot in enumerate(SLOTS, 1):
        prop = BVHTree.FromPolygons(*weapon_mesh(slot), all_triangles=True)
        meshes = read_meshes("fps_hands_" + slot)
        assert set(meshes) == {"Right", "Left"}, (slot, list(meshes))
        hands = {}
        counts = []
        # Slot 2 only draws Right, but also check its unused Left mesh so the
        # result covers the entire exported GLB for every pair.
        for side in ("Right", "Left"):
            vertices, triangles = meshes[side]
            hand = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)
            hands[side] = hand
            hits = len(hand.overlap(prop))
            counts.append(hits)
            gap = min(prop.find_nearest(p)[3] for p in vertices)
            print(f"Slot {number} {side}: {hits} intersecting triangle pairs, nearest vertex {gap*1000:.3f} mm")
            if slot=="solder" and side=="Right":
                check_solder_pads(vertices, prop)
        between = len(hands["Right"].overlap(hands["Left"]))
        print(f"Slot {number} ({slot}): FINAL overlap = {sum(counts)}, hand-hand = {between}")
        failed |= bool(sum(counts) or between)
    if failed:
        raise RuntimeError("Viewmodel contact check failed")


if __name__ == "__main__":
    verify()
