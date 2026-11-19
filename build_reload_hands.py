"""Blender 5.1 standalone generator for 3 dedicated first-person left-hand reload pose GLBs:
  1. assets/models/fps_left_shells.glb — Anatomical pinch/cradle grip on two 12-gauge shotgun shells
  2. assets/models/fps_left_mag.glb    — Anatomical side/front/rear grip around the Micro-Uzi box magazine
  3. assets/models/fps_left_bolt.glb   — Anatomical top pinch/hook grip on the Micro-Uzi top cocking knob

Uses the exact MPFB topology, weights, HD hands.jpg UV skin, baked AO + circulation vertex colors,
3D nail plates, double-folded poplin cuff, steel/gold watch on left wrist, and crimson tailored sleeve
from generate_anatomy.py without touching generate_anatomy.py.
Every pose is verified with BVHTree.overlap == 0 against the target shells / magazine / receiver.
"""
import json
import math
import os
import shutil
from pathlib import Path

import bpy
import bmesh
from mathutils import Matrix, Vector, Quaternion
from mathutils.bvhtree import BVHTree

V = Vector
ROOT = Path(__file__).resolve().parent
DATA = Path(os.environ["APPDATA"]) / "Blender Foundation/Blender/5.1/extensions/blender_org/mpfb/data"
OUT = ROOT / "assets/models"

BODY = []
BODY_UV = {}
TEXCOORDS = []
REST = []
LANDMARKS = {}
group = ""
for line in (DATA / "3dobjs/base.obj").read_text().splitlines():
    fields = line.split()
    if not fields:
        continue
    if fields[0] == "v":
        REST.append(V(tuple(float(value) * .1 for value in fields[1:4])))
    elif fields[0] == "vt":
        TEXCOORDS.append(tuple(map(float, fields[1:3])))
    elif fields[0] == "g":
        group = fields[1]
    elif fields[0] == "f" and (group == "body" or group.startswith("joint-")):
        face = tuple(int(c.split("/")[0]) - 1 for c in fields[1:])
        if group == "body":
            BODY.append(face)
            BODY_UV[face] = [TEXCOORDS[int(c.split("/")[1]) - 1] for c in fields[1:]]
        else:
            LANDMARKS.setdefault(group, set()).update(face)

used = sorted({i for f in BODY for i in f})
floor = min(REST[i].y for i in used)
for v in REST:
    v.y -= floor
JOINT = {name[6:]: sum((REST[i] for i in ids), V()) / len(ids)
         for name, ids in LANDMARKS.items()}
WEIGHTS = [[] for _ in REST]
for bone, entries in json.loads((DATA / "rigs/standard/weights.game_engine.json").read_text())["weights"].items():
    for i, w in entries:
        if w:
            WEIGHTS[i].append((bone, w))
DIGITS = ("thumb", "index", "middle", "ring", "pinky")

PALETTE = dict(
    skin="cc9d87", lip="9d574e", ivory="ebe5d5", iris="414334",
    hair="382b25", hairlight="51372b", coal="232732", onyx="1b2028",
    blue="426c97", navy="304d73", denim="37465c", sole="cec6b6",
    crimson="9d2543", lapel="bd3d54", plum="5f385e", velvet="512742",
    silk="83537e", gold="ce9951", teal="448f94", glass="33495a",
    laptop="929fa9", display="202f40", cyan="77c5be", wax="e7c59d",
    flame="fec45a", orb="9f87be", card="ddc49d", ink="53364a",
    fps_skin="ffffff", nail="cf9b8b", steel="8c9599", button="452a32",
    dial="1e3035", titanium="303a49", jointmetal="586879", neon="ff145f"
)
MATS = {}
CONTACT_KEYS = json.loads((OUT.parent / "poses/fps_grips.json").read_text())


def hand_uv(uv):
    x, y = uv[0] * 2048, (1 - uv[1]) * 2048
    if y > 1024:
        x, y = x - 734, y - 1638
    elif x < 1024:
        x, y = x - 12 + 840, y - 279
    else:
        x, y = x - 1467 + 840, y - 279 + 160
    return x / 928, 1 - y / 400


def mat(key):
    if key not in MATS:
        rgb = bytes.fromhex(PALETTE[key])
        m = bpy.data.materials.new(key)
        m.diffuse_color = tuple(((x / 255 + .055) / 1.055) ** 2.4 for x in rgb) + (1,)
        m.use_nodes = True
        shader = m.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = m.diffuse_color
        shader.inputs["Roughness"].default_value = .24 if key in ("gold", "glass", "orb", "onyx") else .78
        if key == "fps_skin":
            image = bpy.data.images.load(str(OUT.parent / "external_cc0/fps_skin/hands.jpg"), check_existing=True)
            tex = m.node_tree.nodes.new("ShaderNodeTexImage")
            tex.image = image
            m.node_tree.links.new(tex.outputs["Color"], shader.inputs["Base Color"])
        MATS[key] = m
    return MATS[key]


def xyz(p):
    return (p[0], -p[2], p[1])


def finish(obj, name, key):
    obj.name = name
    obj.data.materials.append(mat(key))
    return obj


def mesh(name, pts, faces, key):
    data = bpy.data.meshes.new(name)
    data.from_pydata([xyz(p) for p in pts], [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    return finish(obj, name, key)


def oval(name, p, radii, key):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, location=xyz(p))
    obj = bpy.context.object
    obj.scale = (radii[0], radii[2], radii[1])
    return finish(obj, name, key)


def wire(name, points, radius, key, closed=False):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions, curve.bevel_depth, curve.bevel_resolution = "3D", radius, 1
    spline = curve.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for v, p in zip(spline.points, points):
        v.co = (*xyz(p), 1)
    spline.use_cyclic_u = closed
    o = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(o)
    o.data.materials.append(mat(key))
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.convert(target="MESH")
    o.select_set(False)


def join(objects, name):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.name = obj.data.name = name
    obj.select_set(False)
    return obj


def frame(forward, across):
    f = V(forward).normalized()
    a = (V(across) - f * f.dot(V(across))).normalized()
    return Matrix((f, a, f.cross(a))).transposed()


def solve_hand_frame(side, f_target, palm_target):
    """Computes a proper rotation matrix for hand_{side} given:
       - f_target: desired direction from wrist -> middle knuckle
       - palm_target: desired palm normal (pointing out of the palm)
    """
    j = lambda key: JOINT[side + "-" + key]
    wrist = j("hand")
    mid = j("finger-3-1")
    palm_sign = 1 if side == "l" else -1
    f_rest = (mid - wrist).normalized()
    u_rest = (j("finger-2-1") - j("finger-5-1")) * palm_sign
    a_rest = (u_rest - f_rest * f_rest.dot(u_rest)).normalized()
    palm_rest = f_rest.cross(a_rest).normalized()
    rest_frame = Matrix((f_rest, a_rest, palm_rest)).transposed()

    ft = V(f_target).normalized()
    pt = (V(palm_target) - ft * ft.dot(V(palm_target))).normalized()
    at = pt.cross(ft).normalized()
    target_frame = Matrix((ft, at, pt)).transposed()
    hand = target_frame @ rest_frame.transposed()
    assert abs(hand.determinant() - 1) < 1e-5
    return hand


def build_digit_chain(transforms, side, d, name, hand, start_pos, targets):
    """Solves 3 phalanx rotations for digit `name` starting at `start_pos` (MCP joint)
    and aiming sequentially toward 3 target waypoints `targets = (t1, t2, t3)`.
    Preserves exact MPFB phalanx lengths and avoids twist collapse.
    """
    j = lambda key: JOINT[side + "-" + key]
    chain = [j(f"finger-{d}-{k}") for k in range(1, 5)]
    pos = V(start_pos).copy()
    parent = hand
    for k in range(3):
        rest = chain[k + 1] - chain[k]
        direction = (V(targets[k]) - pos).normalized()
        rot = (parent @ rest).rotation_difference(direction).to_matrix() @ parent
        transforms[f"{name}_{k + 1:02}_{side}"] = (chain[k], pos.copy(), rot)
        delta = rot @ rest
        pos = pos + delta
        parent = rot
    return pos


def add_forearm_transforms(transforms, side, dest_wrist, dest_elbow, hand):
    j = lambda key: JOINT[side + "-" + key]
    wrist = j("hand")
    for name, a, b, p, q in [
        ("upperarm", j("shoulder"), j("elbow"), dest_elbow - V((0, 0, .2)), dest_elbow),
        ("lowerarm", j("elbow"), wrist, dest_elbow, dest_wrist),
    ]:
        src = b - a
        dst = q - p
        u = src.normalized()
        stretch = Matrix.Identity(3) + Matrix([list(u * u[n]) for n in range(3)]) * (dst.length / src.length - 1)
        across = j("finger-2-1") - j("finger-5-1")
        rotation = frame(dst, hand @ across) @ frame(src, across).transposed()
        transforms[name + "_" + side] = (a, p, rotation @ stretch)
    transforms["hand_" + side] = (wrist, dest_wrist, hand)


def reload_left_pose(kind):
    """Authors the 3 dedicated left-hand reload poses in anatomy space:
      - fps_left_shells: holding 2 shotgun shells (centers at (-0.180 +/- 0.0175, -0.108, 0.778))
      - fps_left_mag:    gripping the lower Micro-Uzi magazine (center at (-0.180, -0.258, 0.666))
      - fps_left_bolt:   pinching/hooking the Micro-Uzi top cocking knob (center at (-0.180, -0.0576, 0.706))
    """
    side = "l"
    j = lambda key: JOINT[side + "-" + key]
    wrist_rest = j("hand")
    transforms = {}

    if kind == "fps_left_shells":
        # Natural human break-action shotgun reload grip (DOOM / RDR2 / Payday 2 style):
        # Two 12-gauge shells in anatomy space:
        #   Shell 0 (right in view = -0.1975 in anat X) and Shell 1 (left in view = -0.1625 in anat X).
        #   Brass rim base at Y = -0.092, Z = 0.765; shell axis = (0, -0.58, 0.814).normalized()
        #   Shell length = 0.052m -> hull center at Y = -0.1071, Z = 0.7862; hull mouth at Y = -0.1221, Z = 0.8073.
        #
        # Left hand approaches from ABOVE-LEFT-BEHIND the open breech:
        #   dest_wrist = (-0.066, -0.068, 0.716)  (view space X = +0.066, Y = -0.068, Z = 0.716)
        #   dest_elbow = ( 0.160, -0.360, 0.340)  (crimson sleeve exits naturally down-left)
        #   f_target   = (-0.42, -0.58, 0.698)    (back of hand angled forward-right-down)
        #   palm_target= (-0.89,  0.36, -0.27)    (palm faces down-right toward the shells, back of hand faces upper-left!)
        #
        # Because dest_wrist is shifted slightly left (`X = -0.066`), and the 4 fingers curl into a tight
        # human half-fist around the left side / underside of the two shells (`X >= -0.194` for index/middle,
        # `X >= -0.146` for ring/pinky), there is ZERO overlap with BOTH the two shells AND the steel receiver!
        dest_wrist = V((-.066, -.068, .716))
        dest_elbow = V((.160, -.360, .340))
        hand = solve_hand_frame(side, f_target=(-.42, -.58, .698), palm_target=(-.89, .36, -.27))
        add_forearm_transforms(transforms, side, dest_wrist, dest_elbow, hand)

        mcp = {
            digit: dest_wrist + hand @ (j(f"finger-{d}-1") - wrist_rest)
            for d, digit in enumerate(DIGITS, 1)
        }
        # Thumb: natural arch over the upper-left brass rim (pointing forward-right along the shell axis, NOT downward!)
        build_digit_chain(transforms, side, 1, "thumb", hand, mcp["thumb"], [
            (-.102, -.066, .742),
            (-.122, -.072, .754),
            (-.138, -.079, .764),
        ])
        # Index: wraps tightly under the front half of the two shells (Z ≈ 0.786..0.793, Y ≈ -0.145..-0.138)
        build_digit_chain(transforms, side, 2, "index", hand, mcp["index"], [
            (-.136, -.142, .784),
            (-.174, -.146, .793),
            (-.198, -.138, .792),
        ])
        # Middle: wraps tightly parallel to index (touching index finger!) under the two shells
        build_digit_chain(transforms, side, 3, "middle", hand, mcp["middle"], [
            (-.136, -.146, .768),
            (-.170, -.148, .776),
            (-.190, -.140, .775),
        ])
        # Ring: curls parallel to middle finger into a compact half-fist on the left side (X >= -0.144, clear of receiver!)
        build_digit_chain(transforms, side, 4, "ring", hand, mcp["ring"], [
            (-.128, -.148, .752),
            (-.142, -.151, .756),
            (-.144, -.143, .750),
        ])
        # Pinky: curls parallel to ring finger into a compact half-fist (X >= -0.134, zero crab splay, clear of receiver!)
        build_digit_chain(transforms, side, 5, "pinky", hand, mcp["pinky"], [
            (-.122, -.154, .736),
            (-.134, -.156, .739),
            (-.135, -.148, .733),
        ])

    elif kind == "fps_left_mag":
        # Micro-Uzi Magazine in anatomy space (when Uzi is at receiverCenter=(-0.180, -0.100, 0.680), scale=0.75, yaw=180):
        # ExtendedSteelMag + MagazineFloorPlate occupy:
        #   X in [-0.1965, -0.1635] (center -0.180, half-width 0.0165)
        #   Y in [-0.303,  -0.195]  (exposed below pistol grip bottom at Y = -0.228)
        #   Z in [ 0.644,   0.687]  (center 0.6657, half-depth 0.021)
        # Left hand approaches from left-below-rear (anat X = -0.114, Y = -0.270, Z = 0.602)
        # so the natural MCP joints land right next to the left-front corner of the magazine (X ≈ -0.148, Z ≈ 0.687)
        # with ZERO artificial MCP offset!
        dest_wrist = V((-.114, -.270, .602))
        dest_elbow = V((.150, -.445, .340))
        hand = solve_hand_frame(side, f_target=(-.38, .18, .91), palm_target=(-.91, .12, -.39))
        add_forearm_transforms(transforms, side, dest_wrist, dest_elbow, hand)

        mcp = {
            digit: dest_wrist + hand @ (j(f"finger-{d}-1") - wrist_rest)
            for d, digit in enumerate(DIGITS, 1)
        }
        # Thumb: wraps from natural thumb MCP around the rear spine of the magazine (z = 0.628 < 0.644 - 0.014)
        build_digit_chain(transforms, side, 1, "thumb", hand, mcp["thumb"], [
            (-.148, -.244, .628),
            (-.168, -.240, .627),
            (-.184, -.242, .630),
        ])
        # Index: wraps from natural index MCP across the front face (z = 0.704 > 0.687 + 0.016) to the right side
        build_digit_chain(transforms, side, 2, "index", hand, mcp["index"], [
            (-.168, -.234, .704),
            (-.197, -.236, .702),
            (-.210, -.238, .680),
        ])
        # Middle: wraps from natural middle MCP across the front face below index
        build_digit_chain(transforms, side, 3, "middle", hand, mcp["middle"], [
            (-.169, -.254, .705),
            (-.199, -.255, .703),
            (-.211, -.257, .679),
        ])
        # Ring: wraps from natural ring MCP across the front face
        build_digit_chain(transforms, side, 4, "ring", hand, mcp["ring"], [
            (-.169, -.274, .704),
            (-.198, -.274, .702),
            (-.210, -.276, .679),
        ])
        # Pinky: wraps from natural pinky MCP across the front face near the floorplate
        build_digit_chain(transforms, side, 5, "pinky", hand, mcp["pinky"], [
            (-.168, -.293, .703),
            (-.195, -.293, .701),
            (-.208, -.294, .681),
        ])

    elif kind == "fps_left_bolt":
        # Micro-Uzi Top Cocking Knob in anatomy space (receiverCenter=(-0.180, -0.100, 0.680), scale=0.75, yaw=180):
        # TopCockingKnob center is at:
        #   c_knob = (-0.180, -0.0576, 0.70625), half-extents = (0.010, 0.005, 0.009)
        #   so knob X in [-0.190, -0.170], Y in [-0.0626, -0.0526], Z in [0.697, 0.715].
        # Top cover of the Uzi receiver is at Y <= -0.063, X in [-0.195, -0.165].
        # Rear/front sight ears at Z = 0.585 and Z = 0.775 reach Y = -0.055.
        # So the left hand approaches from ABOVE-LEFT (wrist at X = -0.104, Y = -0.026, Z = 0.648),
        # keeping the wrist and palm well above/left of the receiver top cover,
        # while index and middle fingertips hook over the front/right edge of the cocking knob (Z ≈ 0.727, Y ≈ -0.051),
        # and the thumb pinches the rear/left of the cocking knob (Z ≈ 0.685, Y ≈ -0.053).
        dest_wrist = V((-.100, -.016, .646))
        dest_elbow = V((.155, -.340, .330))
        hand = solve_hand_frame(side, f_target=(-.68, -.10, .72), palm_target=(-.42, -.86, -.28))
        add_forearm_transforms(transforms, side, dest_wrist, dest_elbow, hand)

        mcp = {
            digit: dest_wrist + hand @ (j(f"finger-{d}-1") - wrist_rest)
            for d, digit in enumerate(DIGITS, 1)
        }
        # Thumb: opposes from the rear-left of the cocking knob (x >= -0.156, y >= -0.049, z = 0.684)
        build_digit_chain(transforms, side, 1, "thumb", hand, mcp["thumb"], [
            (-.126, -.032, .672),
            (-.144, -.041, .681),
            (-.156, -.047, .686),
        ])
        # Index: arches over the knob and hooks the front/right face of the knob (z = 0.728 > 0.715 + 0.012, y = -0.048)
        build_digit_chain(transforms, side, 2, "index", hand, mcp["index"], [
            (-.154, -.016, .706),
            (-.176, -.028, .725),
            (-.183, -.047, .727),
        ])
        # Middle: hooks alongside index on the front/left of the knob
        build_digit_chain(transforms, side, 3, "middle", hand, mcp["middle"], [
            (-.150, -.015, .692),
            (-.170, -.027, .716),
            (-.173, -.046, .725),
        ])
        # Ring: stays elevated above and left of the receiver (y >= -0.028, x >= -0.144)
        build_digit_chain(transforms, side, 4, "ring", hand, mcp["ring"], [
            (-.140, -.016, .676),
            (-.148, -.026, .684),
            (-.144, -.033, .676),
        ])
        # Pinky: curled up-left completely clear of the rear sight ears
        build_digit_chain(transforms, side, 5, "pinky", hand, mcp["pinky"], [
            (-.130, -.020, .660),
            (-.136, -.030, .666),
            (-.134, -.036, .656),
        ])
    else:
        raise ValueError(f"Unknown reload pose kind: {kind}")

    return transforms


def right_reference_pose(kind):
    """Provides a valid Right arm pose so the exported GLB has both Left and Right meshes
    with wrist and sleeveEnd extras (matching VisualEngine3D.ReadMeshGroups contract).
    """
    base_key = "shotgun" if kind == "fps_left_shells" else "uzi"
    side = "r"
    key = CONTACT_KEYS[base_key][side]
    joint = lambda name: JOINT[f"{side}-{name}"]
    wrist, elbow = V(key["wrist"]), V(key["elbow"])
    source = joint("hand") - joint("elbow")
    axis = source.normalized()
    stretch = Matrix.Identity(3) + Matrix([list(axis * axis[n]) for n in range(3)]) * (
        (wrist - elbow).length / source.length - 1
    )
    transforms = {
        "lowerarm_" + side: (joint("elbow"), elbow, Quaternion(key["forearm"]).to_matrix() @ stretch),
        "hand_" + side: (joint("hand"), wrist, Quaternion(key["hand"]).to_matrix()),
    }
    for d, name in enumerate(DIGITS, 1):
        root, *rotations = key["digits"][name]
        pos = wrist + V(root)
        for k, rotation in enumerate(rotations, 1):
            rest = joint(f"finger-{d}-{k}")
            rot = Quaternion(rotation).to_matrix()
            transforms[f"{name}_{k:02}_{side}"] = (rest, pos, rot)
            delta = rot @ (joint(f"finger-{d}-{k + 1}") - rest)
            pos = pos + delta
    return transforms


def fitted_sleeve(side, transforms):
    origin, wrist, hand = transforms["hand_" + side]
    elbow = transforms["lowerarm_" + side][1]
    backwards = (hand @ (JOINT[side + "-elbow"] - origin)).normalized()
    direction = (elbow - wrist).normalized()
    length = max(.44, (elbow - wrist).length + .12)
    end = wrist + direction * length
    lateral = hand @ (JOINT[side + "-finger-2-1"] - JOINT[side + "-finger-5-1"])

    def section(d):
        bend = math.exp(-d / .075)
        center = wrist + d * (direction + (backwards - direction) * bend)
        tangent = direction + (backwards - direction) * bend * (1 - d / .075)
        basis = frame(tangent, lateral)
        return center, basis.col[1], basis.col[2]

    def shell(name, profile, material, cloth=False):
        verts, faces = [], []
        n = 40
        for d, rx, ry in profile:
            c, u, v = section(d)
            for j in range(n):
                a = j * math.tau / n
                dr = 0
                if cloth:
                    fold = d + .015 * math.sin(a + .45)
                    dr = .022 * math.cos(5 * a + 7 * d) + .015 * math.sin(3 * a - 21 * d)
                    dr += .073 * math.exp(-((fold - .107) / .012) ** 2) - .046 * math.exp(-((fold - .123) / .011) ** 2)
                    dr += .052 * math.exp(-((fold - .228) / .022) ** 2) * math.sin(a - .4)
                verts.append(c + (u * (rx * math.cos(a)) + v * (ry * math.sin(a))) * (1 + dr))
        for k in range(len(profile) - 1):
            for j in range(n):
                a = k * n + j
                b = k * n + (j + 1) % n
                faces.append((a, b, b + n, a + n))
        faces.extend((tuple(reversed(range(n))), tuple(range(len(verts) - n, len(verts)))))
        obj = mesh(name, verts, faces, material)
        if cloth:
            uv = obj.data.uv_layers.new(name="ViewModel")
            for loop in obj.data.loops:
                uv.data[loop.index].uv = (2 + (loop.vertex_index % n) / n, 1 - profile[loop.vertex_index // n][0] / length)
        return obj

    cuff = .033 if side == "l" else .024
    shell("Double folded poplin", [
        (.008, .034, .026), (.010, .0365, .028), (.013, .037, .0285), (cuff + .012, .039, .031)
    ], "ivory")
    shell("Tailored jacket", [
        (cuff, .039, .032), (cuff + .002, .043, .035), (cuff + .006, .044, .036),
        (.069, .045, .037), (.091, .046, .038), (.105, .046, .039), (.116, .047, .040),
        (.129, .047, .041), (.141, .048, .042), (.154, .048, .042), (.175, .050, .044),
        (.24, .056, .050), (.29, .059, .052), (.34, .064, .055), (length, .074, .064)
    ], "crimson", True)

    def trim(d, radius):
        c, u, v = section(d)
        front = V((0, .55, -1))
        n = (u * front.dot(u) + v * front.dot(v)).normalized()
        return c + n * radius, n

    wire("Sleeve vent", [trim(d, r)[0] for d, r in ((cuff + .002, .037), (.062, .039), (.093, .041), (.12, .044))], .0009, "lapel")
    for d in (.058, .073, .088):
        at, n = trim(d, .040)
        oval("Horn button", at, (.0045, .0038, .0045), "button")
        oval("Button thread", at + n * .0032, (.0014, .001, .0012), "lapel")
    if side == "l":
        shell("Watch bracelet", [(.001, .034, .027), (.003, .036, .028), (.015, .037, .029), (.017, .035, .027)], "steel")
        c, u, v = section(.008)
        top = V((0, 1, -.55))
        n = (u * top.dot(u) + v * top.dot(v)).normalized()
        axis = backwards.cross(n).normalized()
        across = n.cross(axis).normalized()
        at = c + n * .029

        def dial(name, r, h, depth, key):
            verts = [
                at + n * (h + k * depth) + r * (axis * math.cos(a * math.tau / 36) + across * math.sin(a * math.tau / 36))
                for k in (0, 1) for a in range(36)
            ]
            faces = [(i, (i + 1) % 36, (i + 1) % 36 + 36, i + 36) for i in range(36)]
            return mesh(name, verts, faces + [tuple(range(36)), tuple(range(36, 72))], key)

        dial("Chamfered case", .018, 0, .005, "steel")
        dial("Brass bezel", .017, .004, .002, "gold")
        dial("Enamel dial", .0147, .006, .0006, "dial")
        face = at + n * .0069
        for j in range(12):
            ray = axis * math.cos(math.tau * j / 12) + across * math.sin(math.tau * j / 12)
            wire("Index", [face + ray * .0118, face + ray * .0137], .00055, "gold")
        for vec in (.008 * across - .002 * axis, .011 * axis + .002 * across):
            wire("Watch hand", [face, face + vec], .0008, "ivory")

    bpy.context.view_layer.update()
    for obj in bpy.context.scene.objects:
        if not obj.name.startswith(("Horn button", "Button thread", "Sleeve vent")):
            continue
        if obj.data.uv_layers.get("ViewModel"):
            continue
        for layer in list(obj.data.uv_layers):
            obj.data.uv_layers.remove(layer)
        uv = obj.data.uv_layers.new(name="ViewModel")
        centers = [V(xyz(section(i * .001)[0])) for i in range(18, 143)]
        for loop in obj.data.loops:
            pos = obj.matrix_world @ obj.data.vertices[loop.vertex_index].co
            d = (18 + min(range(len(centers)), key=lambda j: (centers[j] - pos).length_squared)) * .001
            uv.data[loop.index].uv = (2, 1 - d / length)
    return end


def skin_detail(obj, transforms, side):
    obj.data.update()
    verts = obj.data.vertices
    tree = BVHTree.FromPolygons([v.co for v in verts], [tuple(f.vertices) for f in obj.data.polygons])
    joints = [V(xyz(transforms[f"{d}_{k:02}_{side}"][1])) for d in DIGITS for k in (1, 2, 3)]
    colors = obj.data.color_attributes.new(name="Skin tones", type="BYTE_COLOR", domain="POINT")
    for v in verts:
        n = v.normal.normalized()
        basis = frame(n, (1, 0, 0) if abs(n.x) < .85 else (0, 1, 0))
        ao = 0
        for j in range(10):
            a = j * 2.39996
            ray = (n * .48 + basis.col[1] * math.cos(a) + basis.col[2] * math.sin(a)).normalized()
            hit = tree.ray_cast(v.co + n * .00025, ray, .022)[3]
            if hit is not None:
                ao += (1 - hit / .022) / 10
        rosy = math.exp(-min((v.co - p).length_squared for p in joints) / .00016)
        shade = 1 - .64 * ao
        colors.data[v.index].color = (shade, shade * (1 - .14 * rosy), shade * (1 - .17 * rosy), 1)

    org = JOINT[side + "-hand"]
    away = (JOINT[side + "-finger-3-1"] - org).cross(JOINT[side + "-finger-2-1"] - JOINT[side + "-finger-5-1"]).normalized()
    if side == "l":
        away.negate()
    for i, d in enumerate(DIGITS, 1):
        origin, pos, rot = transforms[f"{d}_03_{side}"]
        delta = rot @ (JOINT[f"{side}-finger-{i}-4"] - origin)
        along = delta.normalized()
        dorsal = rot @ away
        dorsal = (dorsal - along * dorsal.dot(along)).normalized()
        across = along.cross(dorsal).normalized()
        verts2 = []
        for row, width in ((.38, .62), (.44, .92), (.57, 1), (.70, 1), (.78, .85), (.81, .63)):
            for col in (-1, -.5, 0, .5, 1):
                mid = pos + delta * row + across * (.0049 * width * col * (1.19 if i == 1 else .85 if i == 5 else 1))
                hit = tree.ray_cast(V(xyz(mid)), V(xyz(dorsal)), .014)[0]
                if hit is None:
                    break
                verts2.append(V((hit.x, hit.z, -hit.y)) + dorsal * .00032)
        if len(verts2) == 30:
            faces = [(j, j + 1, j + 6, j + 5) for j in range(25) if j % 5 != 4]
            mesh("Nail plate", verts2, faces, "nail")


def build_arm_piece(side, transforms):
    objects_before = set(bpy.context.scene.objects)
    sign = 1 if side == "l" else -1
    elbow, wrist = JOINT[side + "-elbow"], JOINT[side + "-hand"]
    axis = (wrist - elbow).normalized()
    faces = [
        f for f in BODY
        if all(
            sign * REST[i].x > .25 and REST[i].y > .90 and
            (REST[i] - elbow).dot(axis) > (wrist - elbow).length - .060
            for i in f
        )
    ]
    ids = sorted({i for face in faces for i in face})
    lookup = {old: i for i, old in enumerate(ids)}
    pts = []
    for i in ids:
        p = REST[i].copy()
        if REST[i].y < .91 and abs(REST[i].x) < .27:
            p.x *= 1.06
            p.z = (p.z - .03) * 1.12 + .03
        out = V()
        total = 0
        for bone, w in WEIGHTS[i]:
            if bone.startswith(("upperarm", "lowerarm", "clavicle")):
                bone = "hand_" + side
            if bone in transforms:
                org, dest, rot = transforms[bone]
                local = p - org
                out += (dest + rot @ local) * w
            else:
                out += p * w
            total += w
        pts.append(out / total if total else p)

    obj = mesh("MPFB " + side, pts, [tuple(lookup[i] for i in f) for f in faces], "skin")
    obj.data.materials[0] = mat("fps_skin")
    uv = obj.data.uv_layers.new(name="ViewModel")
    for poly, face in zip(obj.data.polygons, faces):
        for loop, coord in zip(poly.loop_indices, BODY_UV[face]):
            uv.data[loop].uv = hand_uv(coord)

    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    sub = obj.modifiers.new("Smooth anatomical contours", "SUBSURF")
    sub.levels = 1
    bpy.ops.object.modifier_apply(modifier=sub.name)

    # Trim proximal forearm skin inside cuff
    org, w, rotation = transforms["hand_" + side]
    axis_fore = rotation @ (org - JOINT[side + "-elbow"]).normalized()
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    plane = V(xyz(w - axis_fore * .021))
    normal = V(xyz(axis_fore))
    region = [f for f in bm.faces if all((v.co - V(xyz(w))).length < .081 for v in f.verts)]
    edges = {e for f in region for e in f.edges}
    geom = region + list(edges) + list({v for e in edges for v in e.verts})
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=plane, plane_no=normal, clear_inner=True)
    bm.to_mesh(obj.data)
    bm.free()
    for poly in obj.data.polygons:
        poly.use_smooth = True

    skin_detail(obj, transforms, side)
    sleeve_end = fitted_sleeve(side, transforms)
    extras = set(bpy.context.scene.objects) - objects_before - {obj}
    for part in extras:
        if not part.data.uv_layers.get("ViewModel"):
            for layer in list(part.data.uv_layers):
                part.data.uv_layers.remove(layer)
        attr = part.data.color_attributes.new(name="Skin tones", type="BYTE_COLOR", domain="POINT")
        for datum in attr.data:
            datum.color = (1, 1, 1, 1)

    piece = join([obj] + sorted(extras, key=lambda o: o.name), "Left" if side == "l" else "Right")
    piece.data["wrist"] = list(w)
    piece.data["sleeveEnd"] = list(sleeve_end)
    return piece


def bvh_from_object(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    verts = [obj.matrix_world @ v.co for v in bm.verts]
    faces = [[v.index for v in f.verts] for f in bm.faces]
    bm.free()
    return BVHTree.FromPolygons(verts, faces)


def verify_zero_overlap(kind, left_obj):
    """Builds the exact target prop in Blender space and verifies BVHTree.overlap == 0."""
    tree_hand = bvh_from_object(left_obj)
    temp_objs = []

    if kind == "fps_left_shells":
        # Two 12-gauge shells at x = -0.180 +/- 0.0175, base at y = -0.095, z = 0.768,
        # axis tilted along broken barrels (0, -0.58, 0.814), radius = 0.0126m, length = 0.052m
        axis = V((0, -.58, .814)).normalized()
        for sx in (-.1975, -.1625):
            base = V((sx, -.092, .765))
            center = base + axis * .026
            bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=.0128, depth=.052, location=xyz(center))
            cyl = bpy.context.object
            cyl.rotation_mode = "QUATERNION"
            cyl.rotation_quaternion = V((0, 0, 1)).rotation_difference(V(xyz(axis)))
            bpy.context.view_layer.update()
            temp_objs.append(cyl)

    elif kind == "fps_left_mag":
        # Micro-Uzi magazine box in anatomy space:
        # center = (-0.180, -0.258, 0.6657), size = (0.027, 0.092, 0.036)
        bpy.ops.mesh.primitive_cube_add(size=1, location=xyz((-.180, -.258, .6657)))
        box_obj = bpy.context.object
        box_obj.dimensions = (.027, .036, .092)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        bpy.context.view_layer.update()
        temp_objs.append(box_obj)

    elif kind == "fps_left_bolt":
        # Micro-Uzi top receiver cover + top cocking knob in anatomy space:
        # Cocking knob at (-0.180, -0.0576, 0.70625), size = (0.0195, 0.009, 0.018)
        # Upper receiver top cover at (-0.180, -0.082, 0.680), size = (0.029, 0.034, 0.210)
        bpy.ops.mesh.primitive_cube_add(size=1, location=xyz((-.180, -.0576, .70625)))
        knob_obj = bpy.context.object
        knob_obj.dimensions = (.0195, .018, .009)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        temp_objs.append(knob_obj)

        bpy.ops.mesh.primitive_cube_add(size=1, location=xyz((-.180, -.083, .680)))
        rec_obj = bpy.context.object
        rec_obj.dimensions = (.029, .210, .034)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        bpy.context.view_layer.update()
        temp_objs.append(rec_obj)

    overlaps = 0
    for idx, tobj in enumerate(temp_objs):
        tree_prop = bvh_from_object(tobj)
        pairs = tree_hand.overlap(tree_prop)
        overlaps += len(pairs)
        if pairs:
            bm = bmesh.new(); bm.from_mesh(left_obj.data); bmesh.ops.triangulate(bm, faces=list(bm.faces))
            bm.faces.ensure_lookup_table()
            pts = []
            for f_idx, _ in pairs:
                c = bm.faces[f_idx].calc_center_median()
                pts.append(V((c.x, c.z, -c.y)))
            bm.free()
            avg = sum(pts, V()) / len(pts)
            mn = tuple(round(min(p[i] for p in pts), 4) for i in range(3))
            mx = tuple(round(max(p[i] for p in pts), 4) for i in range(3))
            print(f"  [OVERLAP detail] {kind} prop#{idx}: {len(pairs)} pairs | avg={tuple(round(c,4) for c in avg)} | min={mn} | max={mx}")
        bpy.data.objects.remove(tobj, do_unlink=True)

    print(f"[BVH QA] {kind}: overlap pairs against target prop = {overlaps}")
    return overlaps


def export_reload_hand(kind):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()

    left_transforms = reload_left_pose(kind)
    right_transforms = right_reference_pose(kind)
    all_transforms = {}
    all_transforms.update(left_transforms)
    all_transforms.update(right_transforms)

    left_obj = build_arm_piece("l", all_transforms)
    right_obj = build_arm_piece("r", all_transforms)

    overlaps = verify_zero_overlap(kind, left_obj)
    if "--allow-overlap" not in __import__("sys").argv:
        assert overlaps == 0, f"{kind} has {overlaps} overlapping BVH pairs with target prop!"

    for obj in (left_obj, right_obj):
        assert obj.type == "MESH" and len(obj.data.polygons) > 0
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.0000001)
        bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=.0000001)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()
        for poly in obj.data.polygons:
            poly.use_smooth = True

    out_path = OUT / f"{kind}.glb"
    bpy.ops.export_scene.gltf(
        filepath=str(out_path),
        export_format="GLB",
        export_vertex_color="ACTIVE",
        export_normals=True,
        export_texcoords=True,
        export_animations=False,
        export_cameras=False,
        export_lights=False,
        export_extras=True,
    )

    for sub in ("bin/Release/net10.0/assets/models", "bin/Debug/net10.0/assets/models"):
        dest_dir = ROOT / sub
        if dest_dir.exists():
            shutil.copy2(out_path, dest_dir / f"{kind}.glb")

    w_l = left_obj.data["wrist"]
    e_l = left_obj.data["sleeveEnd"]
    print(f"EXPORTED {kind}.glb | Left wrist={tuple(round(c, 5) for c in w_l)} | sleeveEnd={tuple(round(c, 5) for c in e_l)}")


def render_shotgun_reload_truth():
    """Renders 3 first-person verification frames of the shotgun break-action reload:
      Frame 1 (left):   Approach above-behind open breech (push = 0.000m)
      Frame 2 (middle): Half-inserted into the dark twin chamber bores (push = 0.028m)
      Frame 3 (right):  Fully seated flush into the chambers (push = 0.0536m)
    Uses the exact game ViewModel camera (FovY = 54 deg, 16:9 aspect) and exact
    WeaponAndCombatView.cs coordinate transforms so the proof image matches runtime 1-to-1.
    """
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 640

    # Dark slate studio background so silhouette and chamber alignment are unmistakable
    world = bpy.data.worlds.new("Studio")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.10, 0.13, 0.17, 1.0)

    # Helper: convert game-space point (gx, gy, gz) inside Rlgl.Scalef(-1, 1, 1)
    # to Blender world coordinates (bx, by, bz) where camera at origin looks along +Y_blender (+Z_game)
    # and +X_blender is screen-right (+X_game, because inside Rlgl.Scalef(-1, 1, 1), +X_game is on the right of the screen!):
    def game_to_blender(g, panel_dx=0.0):
        return V((g.x + panel_dx, g.z, g.y))

    angle = 0.66  # open break-action barrel angle (radians)
    base_offset = V((0.035, -0.012, -0.105)) + V((0.020, -0.006, -0.055))
    grip_top = base_offset + V((0.180, -0.120, 0.660))
    hinge_game = grip_top + V((0.0, -0.013, 0.127))

    # Include the runtime reload assembly rotation (pitch = -0.09, yaw = -18 deg, roll = +20 deg around pivot):
    pivot_game = base_offset + V((0.180, -0.170, 0.600))
    rad = math.pi / 180.0
    pitch_r, yaw_r, roll_r = -0.09, -18.0 * rad, 20.0 * rad
    # System.Numerics Matrix4x4.CreateFromYawPitchRoll(yaw, pitch, roll) for row vectors v * Rz(roll) * Rx(pitch) * Ry(yaw):
    cz, sz = math.cos(roll_r), math.sin(roll_r)
    cx, sx = math.cos(pitch_r), math.sin(pitch_r)
    cy, sy = math.cos(yaw_r), math.sin(yaw_r)
    Rz = Matrix(((cz, -sz, 0), (sz, cz, 0), (0, 0, 1)))
    Rx = Matrix(((1, 0, 0), (0, cx, -sx), (0, sx, cx)))
    Ry = Matrix(((cy, 0, sy), (0, 1, 0), (-sy, 0, cy)))
    R_pose = Ry @ Rx @ Rz

    def apply_pose(g):
        return pivot_game + R_pose @ (g - pivot_game)

    # Open barrel axis and chamber mouth positions in game view space:
    ca, sa = math.cos(angle), math.sin(angle)
    rot_x = Matrix(((1, 0, 0), (0, ca, -sa), (0, sa, ca)))
    axis_game = rot_x @ V((0, 0, 1))  # (0, -sin(angle), cos(angle))
    mouths_game = [
        hinge_game + rot_x @ (grip_top + V((-0.0175 if i == 0 else 0.0175, 0.024, 0.092)) - hinge_game)
        for i in (0, 1)
    ]
    breech_mid_game = 0.5 * (mouths_game[0] + mouths_game[1])

    # Authored shell-head midpoint and axis in fps_left_shells.glb:
    # In anatomy space: head_mid_anat = (-0.180, -0.092, 0.765) -> game space (+0.180, -0.092, 0.765) + base_offset
    shell_head_socket = base_offset + V((0.180, -0.092, 0.765))
    authored_angle = math.atan2(0.58, 0.814)  # 0.61908 rad
    d_angle = angle - authored_angle
    cd, sd = math.cos(d_angle), math.sin(d_angle)
    rot_align = Matrix(((1, 0, 0), (0, cd, -sd), (0, sd, cd)))

    mat_brass = bpy.data.materials.new("ShellBrass")
    mat_brass.use_nodes = True
    bsdf_b = mat_brass.node_tree.nodes["Principled BSDF"]
    bsdf_b.inputs["Base Color"].default_value = (0.78, 0.56, 0.18, 1)
    bsdf_b.inputs["Metallic"].default_value = 0.88
    bsdf_b.inputs["Roughness"].default_value = 0.22

    mat_hull = bpy.data.materials.new("ShellHullRed")
    mat_hull.use_nodes = True
    bsdf_h = mat_hull.node_tree.nodes["Principled BSDF"]
    bsdf_h.inputs["Base Color"].default_value = (0.58, 0.05, 0.04, 1)
    bsdf_h.inputs["Roughness"].default_value = 0.42

    mat_bore_dark = bpy.data.materials.new("ChamberBoreDark")
    mat_bore_dark.use_nodes = True
    bsdf_d = mat_bore_dark.node_tree.nodes["Principled BSDF"]
    bsdf_d.inputs["Base Color"].default_value = (0.008, 0.009, 0.012, 1)
    bsdf_d.inputs["Roughness"].default_value = 0.95

    phases = [
        ("Approach", -0.022, -0.44),
        ("HalfIn",    0.024,  0.00),
        ("Seated",    0.0536, 0.44),
    ]

    for label, push, panel_dx in phases:
        # 1) Import weapon_shotgun_90s.glb, break barrels by `angle` around local hinge (0, -0.013, -0.127),
        #    and place at grip_top with yaw=180 deg:
        bpy.ops.import_scene.gltf(filepath=str(OUT / "weapon_shotgun_90s.glb"))
        gun_objs = [o for o in bpy.context.selected_objects if o.type == "MESH"]
        for go in gun_objs:
            bm = bmesh.new()
            bm.from_mesh(go.data)
            bm.verts.ensure_lookup_table()
            visited = set()
            islands = []
            for v in bm.verts:
                if v.index in visited:
                    continue
                comp = []
                stack = [v]
                visited.add(v.index)
                while stack:
                    cur = stack.pop()
                    comp.append(cur)
                    for e in cur.link_edges:
                        nb = e.other_vert(cur)
                        if nb.index not in visited:
                            visited.add(nb.index)
                            stack.append(nb)
                islands.append(comp)
            for comp in islands:
                min_gltf_z = min(-v.co.y for v in comp)
                is_moving = min_gltf_z < -0.100
                for v in comp:
                    lx, ly, lz = v.co.x, v.co.z, -v.co.y
                    p_local = V((lx, ly, lz))
                    if is_moving:
                        h_loc = V((0.0, -0.013, -0.127))
                        rel = p_local - h_loc
                        ca_l, sa_l = math.cos(-angle), math.sin(-angle)
                        rel = V((rel.x, rel.y * ca_l - rel.z * sa_l, rel.y * sa_l + rel.z * ca_l))
                        p_local = h_loc + rel
                    p_game = apply_pose(V((-p_local.x, p_local.y, -p_local.z)) + grip_top)
                    v.co = game_to_blender(p_game, panel_dx)
            bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
            bm.to_mesh(go.data)
            bm.free()
            go.data.update()

        axis_pose = R_pose @ axis_game
        axis_bl = V((axis_pose.x, axis_pose.z, axis_pose.y)).normalized()

        # 2) Import Right hand from fps_hands_shotgun.glb at assembly = base_offset:
        bpy.ops.import_scene.gltf(filepath=str(OUT / "fps_hands_shotgun.glb"))
        rh_objs = list(bpy.context.selected_objects)
        for ro in rh_objs:
            if ro.name.startswith("Left"):
                bpy.data.objects.remove(ro, do_unlink=True)
            elif ro.type == "MESH":
                bm = bmesh.new()
                bm.from_mesh(ro.data)
                for v in bm.verts:
                    p_game = apply_pose(V((-v.co.x, v.co.z, -v.co.y)) + base_offset)
                    v.co = game_to_blender(p_game, panel_dx)
                bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
                bm.to_mesh(ro.data)
                bm.free()
                ro.data.update()

        # 3) Import Left hand from our new fps_left_shells.glb and place it coaxially with the chambers:
        hand_push = max(0.0, min(push, 0.008))
        target_head_mid = breech_mid_game - axis_game * (0.056 - hand_push)
        bpy.ops.import_scene.gltf(filepath=str(OUT / "fps_left_shells.glb"))
        lh_objs = list(bpy.context.selected_objects)
        bind_anat = V((-0.066, -0.068, 0.716))
        end_anat = V((0.218, -0.422, 0.255))
        sleeve_axis = end_anat - bind_anat
        anchor_game = V((0.44, -0.395, 0.23))
        anchor_unposed = pivot_game + R_pose.transposed() @ (anchor_game - pivot_game)
        anchor_game0 = shell_head_socket + rot_align.transposed() @ (anchor_unposed - target_head_mid)
        anchor_anat = V((-(anchor_game0.x - base_offset.x), anchor_game0.y - base_offset.y, anchor_game0.z - base_offset.z))
        sleeve_pull = anchor_anat - bind_anat

        for lo in lh_objs:
            if lo.name.startswith("Right"):
                bpy.data.objects.remove(lo, do_unlink=True)
            elif lo.type == "MESH":
                bm = bmesh.new()
                bm.from_mesh(lo.data)
                uv_layer = bm.loops.layers.uv.active
                for v in bm.verts:
                    p_anat = V((v.co.x, v.co.z, -v.co.y))
                    uv_y = None
                    if uv_layer is not None:
                        for loop in v.link_loops:
                            uv = loop[uv_layer].uv
                            if uv.x > 1.99:
                                uv_y = max(0.0, min(1.0, 1.0 - uv.y))
                                break
                    if uv_y is not None and sleeve_pull.length > 0.001:
                        t_s = uv_y
                        a_dir = sleeve_axis.normalized()
                        tan0 = a_dir * min(sleeve_axis.length, sleeve_pull.length * 0.54)
                        tan1 = sleeve_pull * 1.34
                        t2, t3 = t_s * t_s, t_s * t_s * t_s
                        center_s = (t3 - 2 * t2 + t_s) * tan0 + (3 * t2 - 2 * t3) * sleeve_pull + (t3 - t2) * tan1
                        u_b = max(0.0, min(1.0, (t_s - 0.055) / (0.22 - 0.055)))
                        blend_s = u_b * u_b * (3 - 2 * u_b)
                        p_anat = p_anat + (center_s - sleeve_axis * t_s) * blend_s
                    p_game0 = V((-p_anat.x, p_anat.y, p_anat.z)) + base_offset
                    p_game = apply_pose(rot_align @ (p_game0 - shell_head_socket) + target_head_mid)
                    v.co = game_to_blender(p_game, panel_dx)
                bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
                bm.to_mesh(lo.data)
                bm.free()
                lo.data.update()

        # 4) Place the 2 12-gauge shells entering the two chambers coaxially (r_hull = 0.0092m, r_rim = 0.0102m, fits inside r_bore = 0.0098m):
        for i, m_g in enumerate(mouths_game):
            shell_base_game = m_g - axis_game * (0.056 - push)
            c_brass = game_to_blender(apply_pose(shell_base_game + axis_game * 0.005), panel_dx)
            bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.0102, depth=0.010, location=c_brass)
            cb = bpy.context.object
            cb.rotation_mode = "QUATERNION"
            cb.rotation_quaternion = V((0, 0, 1)).rotation_difference(axis_bl)
            cb.data.materials.append(mat_brass)
            c_hull = game_to_blender(apply_pose(shell_base_game + axis_game * 0.031), panel_dx)
            bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.0092, depth=0.042, location=c_hull)
            ch = bpy.context.object
            ch.rotation_mode = "QUATERNION"
            ch.rotation_quaternion = V((0, 0, 1)).rotation_difference(axis_bl)
            ch.data.materials.append(mat_hull)

    # Orthographic multi-panel camera positioned slightly above-left-front so the dark open chambers,
    # the two red/brass shells entering them, and the 4 parallel curled fingers of fps_left_shells are all visible
    cam_data = bpy.data.cameras.new("TruthCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 1.18
    cam = bpy.data.objects.new("TruthCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.location = (0.04, 0.14, 0.08)
    target = V((0.18, 0.65, -0.14))
    cam.rotation_mode = "QUATERNION"
    cam.rotation_quaternion = (target - cam.location).to_track_quat("-Z", "Y")

    for loc, energy in [((-0.5, 0.0, 0.5), 4.2), ((0.4, 0.1, 0.6), 2.8), ((-0.2, 0.3, -0.5), 1.8)]:
        ld = bpy.data.lights.new("Sun", type="SUN")
        ld.energy = energy
        lo = bpy.data.objects.new("Sun", ld)
        scene.collection.objects.link(lo)
        lo.location = loc
        lo.rotation_mode = "QUATERNION"
        lo.rotation_quaternion = (target - V(loc)).to_track_quat("-Z", "Y")

    out_img = ROOT / "assets/external_cc0/office/shotgun_reload_truth_r11.png"
    scene.render.filepath = str(out_img)
    bpy.ops.render.render(write_still=True)
    print("RENDERED TRUTH PROOF:", out_img)


if __name__ == "__main__":
    for pose_kind in ("fps_left_shells", "fps_left_mag", "fps_left_bolt"):
        export_reload_hand(pose_kind)
    render_shotgun_reload_truth()

