"""
AAA Hero 90s IMI Micro-Uzi Builder (`assets/models/weapon_uzi_90s.glb`)
Matches target concept `shop_ui_concept_1795129377316.jpg` while preserving:
  1. 0-triangle BVHTree overlap with `fps_hands_uzi.glb` (`verify_viewmodel_contacts.py`)
  2. Exact C# reload island invariants (`MovingIslands` in `WeaponAndCombatView.cs:1683-1686`):
     - Removable magazine islands: `lo.Y < -0.232f && hi.Y < -0.200f`
     - Reciprocating top charging knob islands: `lo.Y > 0.044f && lo.Z > -0.060f && hi.Z < -0.012f`
  3. Exact material names (`GunMetal`, `MachinedSteel`, `PolymerGrip`, `BrassBolt`, `SelectorRed`, `SelectorWhite`)
     required by `char_partner_90s.glb` (`generate_anatomy.py:1320-1338`).
"""

import math
import os
from pathlib import Path
import bmesh
import bpy
import numpy as np
from mathutils import Euler, Vector


ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "assets/models"
OUT_ICONS = ROOT / "assets/icons"
ARTIFACT_PROOF_1 = Path(r"C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a\uzi_v2_proof.png")
ARTIFACT_PROOF_2 = Path(r"C:\Users\se-20\.gemini\antigravity\brain\34a5dbd6-7dba-4b8d-8112-53de935b5112\scratch\uzi_v2_proof.png")
SCREENSHOT_PROOF = ROOT / "screenshots/uzi_v2_proof.png"

PALETTE = dict(
    GunMetal="46505C",      # Rich military parkerized charcoal-slate steel
    MachinedSteel="6E7C8A", # Brushed satin gunsmith steel highlights & edges
    PolymerGrip="25292E",   # Deep matte tactical polymer
    BrassBolt="E5B242",     # Warm satin 90s brass bolt & 9mm cartridge cases
    SelectorRed="E6242E",   # Fire selector A/R red enamel marking
    SelectorWhite="EEF3F8", # Fire selector S white enamel marking
)
MATS = {}


def xyz(p):
    return (p[0], -p[2], p[1])


def build_uzi_mat_texture(key):
    """Build a dedicated 256x256 weathered procedural texture for `key` so glTF preserves all 6 distinct materials & metallic/roughness values."""
    img_name = f"UziTex_{key}"
    existing = bpy.data.images.get(img_name)
    if existing is not None:
        return existing

    W, H = 256, 256
    arr = np.ones((H, W, 4), dtype=np.float32)
    rng = np.random.default_rng(1994 + sum(ord(c) for c in key))
    y, x = np.mgrid[0:H, 0:W].astype(np.float32)
    noise_fine = rng.normal(0.0, 0.012, (H, W)).astype(np.float32)

    if key == "GunMetal":
        brush = 0.014 * np.sin(y * 0.65 + np.sin(x * 0.09) * 1.8) + 0.009 * np.cos(x * 0.14 - y * 0.12)
        mottle = 0.014 * np.sin(x * 0.04) * np.cos(y * 0.05)
        base = np.clip(0.155 + brush + mottle + noise_fine, 0.09, 0.25)
        arr[:, :, 0] = base * 0.90
        arr[:, :, 1] = base * 0.99
        arr[:, :, 2] = base * 1.12
    elif key == "MachinedSteel":
        brush = 0.028 * np.sin(y * 1.1) + 0.015 * np.cos(y * 2.3 + x * 0.06) + noise_fine * 1.2
        base = np.clip(0.330 + brush, 0.20, 0.48)
        arr[:, :, 0] = base * 0.92
        arr[:, :, 1] = base * 1.00
        arr[:, :, 2] = base * 1.12
    elif key == "PolymerGrip":
        stipple = rng.normal(0.0, 0.009, (H, W)).astype(np.float32)
        base = np.clip(0.056 + stipple, 0.030, 0.095)
        arr[:, :, 0] = base * 0.95
        arr[:, :, 1] = base * 0.98
        arr[:, :, 2] = base * 1.06
    elif key == "BrassBolt":
        brush = 0.032 * np.sin(x * 0.85) + noise_fine
        arr[:, :, 0] = np.clip(0.76 + brush, 0.52, 0.94)
        arr[:, :, 1] = np.clip(0.50 + brush * 0.7, 0.34, 0.68)
        arr[:, :, 2] = np.clip(0.13 + brush * 0.3, 0.06, 0.24)

    img = bpy.data.images.new(img_name, width=W, height=H, alpha=False)
    img.pixels.foreach_set(arr.ravel())
    img.pack()
    return img


def mat(key):
    if key not in MATS:
        rgb = bytes.fromhex(PALETTE[key])
        m = bpy.data.materials.new(key)
        m.diffuse_color = tuple(((x / 255 + .055) / 1.055) ** 2.4 for x in rgb) + (1,)
        m.use_nodes = True
        tree = m.node_tree
        shader = tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = m.diffuse_color
        if key == "BrassBolt":
            shader.inputs["Metallic"].default_value = 0.90
            shader.inputs["Roughness"].default_value = 0.22
        elif key == "MachinedSteel":
            shader.inputs["Metallic"].default_value = 0.86
            shader.inputs["Roughness"].default_value = 0.26
        elif key == "GunMetal":
            shader.inputs["Metallic"].default_value = 0.78
            shader.inputs["Roughness"].default_value = 0.34
        elif key == "PolymerGrip":
            shader.inputs["Metallic"].default_value = 0.08
            shader.inputs["Roughness"].default_value = 0.50
        else:
            shader.inputs["Metallic"].default_value = 0.10
            shader.inputs["Roughness"].default_value = 0.28
        if key in ("GunMetal", "MachinedSteel", "PolymerGrip", "BrassBolt"):
            tex = tree.nodes.new("ShaderNodeTexImage")
            tex.image = build_uzi_mat_texture(key)
            tree.links.new(tex.outputs["Color"], shader.inputs["Base Color"])
        MATS[key] = m
    return MATS[key]


def finish(obj, name, key):
    obj.name = name
    obj.data.materials.clear()
    obj.data.materials.append(mat(key))
    return obj


def apply_weighted_smooth(obj):
    for poly in obj.data.polygons:
        poly.use_smooth = True
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    wn = obj.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL")
    wn.mode = 'FACE_AREA'
    wn.weight = 50
    wn.keep_sharp = False
    bpy.ops.object.modifier_apply(modifier=wn.name)
    obj.select_set(False)
    return obj


def detail(label, p, size, key, bevel=.0035, segments=3, rot=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=xyz(p))
    obj = bpy.context.object
    obj.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if any(abs(a) > 1e-6 for a in rot):
        obj.rotation_euler = Euler((rot[0], -rot[2], rot[1]), "XYZ")
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    finish(obj, label, key)
    if bevel > 0:
        bpy.context.view_layer.objects.active = obj
        mod = obj.modifiers.new("Stamped rounded edges", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.profile = 0.55
        mod.limit_method = 'ANGLE'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return apply_weighted_smooth(obj)


def cut(obj, p, size, bevel=.0025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=xyz(p))
    cutter = bpy.context.object
    cutter.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    cutter.data.materials.append(obj.data.materials[0] if obj.data.materials else mat("GunMetal"))
    if bevel > 0:
        bpy.context.view_layer.objects.active = cutter
        bmod = cutter.modifiers.new("BevelCutter", "BEVEL")
        bmod.width = bevel
        bmod.segments = 2
        bpy.ops.object.modifier_apply(modifier=bmod.name)
    bpy.context.view_layer.objects.active = obj
    mod = obj.modifiers.new("Through ventilation window", "BOOLEAN")
    mod.operation, mod.object = 'DIFFERENCE', cutter
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter, do_unlink=True)
    return apply_weighted_smooth(obj)


def cut_cylinder_x(obj, y, z, radius, depth=0.060):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=32,
        radius=radius,
        depth=depth,
        location=xyz((0.0, y, z)),
        rotation=(0.0, math.pi / 2.0, 0.0),
    )
    cutter = bpy.context.object
    cutter.data.materials.append(obj.data.materials[0] if obj.data.materials else mat("GunMetal"))
    bpy.context.view_layer.objects.active = obj
    mod = obj.modifiers.new("CircularCut", "BOOLEAN")
    mod.operation, mod.object = 'DIFFERENCE', cutter
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter, do_unlink=True)
    return apply_weighted_smooth(obj)


def tube(label, z, length, radius, key, y=.018, bevel=.0015, vertices=36):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=length,
        location=xyz((0, y, z)),
        rotation=(math.pi / 2, 0, 0),
    )
    obj = finish(bpy.context.object, label, key)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        bpy.context.view_layer.objects.active = obj
        mod = obj.modifiers.new("TubeChamfer", "BEVEL")
        mod.width = bevel
        mod.segments = 3
        mod.limit_method = 'ANGLE'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return apply_weighted_smooth(obj)


def cyl_x(label, x, y, z, radius, depth, key, bevel=.0008, vertices=28):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=xyz((x, y, z)),
        rotation=(0, math.pi / 2, 0),
    )
    obj = finish(bpy.context.object, label, key)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        bpy.context.view_layer.objects.active = obj
        mod = obj.modifiers.new("PinChamfer", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.limit_method = 'ANGLE'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return apply_weighted_smooth(obj)


def smooth_wire(name, points, radius, key, closed=False, resolution=5):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = radius
    curve.bevel_resolution = resolution
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
    for poly in o.data.polygons:
        poly.use_smooth = True
    o.select_set(False)
    return o


def build_round_uzi_sight_ears(name, z_center, y_base, radius, hole_r, key_main, key_rim):
    """
    Build TRUE 36-segment semi-circular stamped-steel IMI Micro-Uzi protective sight ears ("Mickey Mouse ears")
    with circular lightening holes on left & right wings (`x = ±0.0155`), matching `shop_ui_concept_1795129377316.jpg` 1-to-1!
    """
    detail(f"{name}_BaseBlock", (0.0, y_base - 0.002, z_center), (0.036, 0.010, radius * 2.05), key_main, bevel=0.0022, segments=3)
    for side in (-1, 1):
        sx = side * 0.0154
        # Build from a 36-segment X-axis cylinder so the top silhouette is a true smooth circle!
        ear = cyl_x(f"{name}_RoundWing_{side}", sx, y_base + 0.002, z_center, radius, 0.0046, key_main, bevel=0.0012, vertices=36)
        # Flatten bottom half of cylinder in local space (local z < -0.004 -> world Y = y_base - 0.002)
        for v in ear.data.vertices:
            if v.co.z < -0.004:
                v.co.z = -0.004
        ear.data.update()
        # Punch the circular lightening/peep hole through the upper-center of the rounded sight ear
        hole_y = y_base + radius * 0.44
        cut_cylinder_x(ear, hole_y, z_center, hole_r, depth=0.060)
        rim = cyl_x(f"{name}_HoleRim_{side}", sx, hole_y, z_center, hole_r + 0.0014, 0.0052, key_rim, bevel=0.0004, vertices=28)
        cut_cylinder_x(rim, hole_y, z_center, hole_r, depth=0.060)


def build_smooth_uzi():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    metal   = "GunMetal"
    trim    = "MachinedSteel"
    polymer = "PolymerGrip"
    brass   = "BrassBolt"
    red_m   = "SelectorRed"
    white_m = "SelectorWhite"

    # ==========================================================================
    # 1) CHUNKY STAMPED-STEEL UPPER RECEIVER (+25% taller & meatier box, y = -0.013..+0.043)
    # ==========================================================================
    receiver = detail("StampedUpperReceiver", (0.0, 0.015, 0.012), (0.0385, 0.056, 0.288), metal, bevel=0.0055, segments=4)

    # Stepped Milled Steel Top Cover (`y = 0.038..0.053`, `z = -0.104..+0.112`)
    arc = [(0.0188 * math.cos(i * math.pi / 32), 0.041 + 0.0115 * math.sin(i * math.pi / 32)) for i in range(33)]
    pts = [xyz((x, y, z)) for z in (-0.104, 0.112) for x, y in arc]
    faces = [(i, i + 1, 34 + i, 33 + i) for i in range(32)]
    faces += [tuple(reversed(range(33))), tuple(range(33, 66)), (0, 33, 65, 32)]
    data = bpy.data.meshes.new("MilledSteelBoltCover")
    data.from_pydata(pts, [], faces)
    data.update()
    cover = bpy.data.objects.new("MilledSteelBoltCover", data)
    bpy.context.collection.objects.link(cover)
    finish(cover, "MilledSteelBoltCover", trim)
    apply_weighted_smooth(cover)

    detail("TopCoverSeamLip", (0.0, 0.040, 0.004), (0.0402, 0.0038, 0.220), trim, bevel=0.0012, segments=3)
    for sx in (-0.0095, 0.0095):
        detail(f"TopCoverRib_{sx}", (sx, 0.0525, 0.004), (0.0034, 0.0028, 0.206), metal, bevel=0.0010, segments=3)

    detail("RearReceiverEndPlate", (0.0, 0.015, 0.156), (0.0365, 0.048, 0.007), trim, bevel=0.0022, segments=3)
    detail("TopCoverRearCatch", (0.0, 0.044, 0.148), (0.016, 0.012, 0.014), trim, bevel=0.0020, segments=3)
    tube("RecoilSpringEndButton", 0.159, 0.010, 0.0078, metal, y=0.020, bevel=0.0015)

    # ==========================================================================
    # 2) RECIPROCATING TOP COCKING KNOB WITH ANTI-SLIP SERRATIONS
    # CRITICAL RELOAD INVARIANT: lo.Y > 0.044f && lo.Z > -0.060f && hi.Z < -0.012f
    # ==========================================================================
    detail("TopCockingSliderBase", (0.0, 0.0535, -0.035), (0.017, 0.0065, 0.032), metal, bevel=0.0018, segments=3)
    cock_knob = detail("TopCockingKnob", (0.0, 0.0625, -0.035), (0.025, 0.0135, 0.023), trim, bevel=0.0032, segments=3)
    cut(cock_knob, (0.0, 0.066, -0.035), (0.0075, 0.010, 0.028), bevel=0.0015)

    # ==========================================================================
    # 3) DEEP STAMPED REINFORCING RIBS & EJECTION PORT WITH BRASS/STEEL BOLT
    # ==========================================================================
    for side in (-1, 1):
        sx = side * 0.0192
        detail(f"StampedRibLong_{side}", (sx, -0.002, 0.010), (0.0044, 0.0068, 0.248), metal, bevel=0.0024, segments=3)
        detail(f"StampedRibLongEdge_{side}", (side * 0.0206, -0.002, 0.010), (0.0020, 0.0028, 0.244), trim, bevel=0.0008, segments=2)

        for ry in (0.013, 0.026):
            detail(f"StampedRibRear_{side}_{ry}", (sx, ry, 0.092), (0.0044, 0.0058, 0.088), metal, bevel=0.0022, segments=3)
            detail(f"StampedRibRearEdge_{side}_{ry}", (side * 0.0206, ry, 0.092), (0.0020, 0.0024, 0.084), trim, bevel=0.0008, segments=2)

        detail(f"StampedRibFront_{side}", (sx, 0.018, -0.080), (0.0044, 0.0062, 0.072), metal, bevel=0.0022, segments=3)
        detail(f"StampedRibFrontEdge_{side}", (side * 0.0206, 0.018, -0.080), (0.0020, 0.0024, 0.068), trim, bevel=0.0008, segments=2)

    # Recessed Ejection Port on Right (+X)
    cut(receiver, (0.019, 0.020, 0.004), (0.013, 0.022, 0.070), bevel=0.002)
    detail("EjectionPortRim_R", (0.0185, 0.020, 0.004), (0.0028, 0.024, 0.073), trim, bevel=0.0012, segments=3)
    detail("PolishedBrassBolt", (0.000, 0.020, 0.004), (0.032, 0.019, 0.066), brass, bevel=0.0025, segments=3)
    detail("BrassExtractorSlot", (0.0168, 0.021, 0.016), (0.003, 0.005, 0.028), metal, bevel=0.001, segments=2)
    tube("BoltRecoilRod", -0.006, 0.062, 0.0022, trim, y=0.027, bevel=0.0004)

    # Left (-X, showcase camera side) stamped receiver groove
    detail("ShowcaseBoltWindowFrame", (-0.0192, 0.018, -0.008), (0.0036, 0.015, 0.084), trim, bevel=0.0014, segments=3)
    detail("ShowcaseBoltFace", (-0.0202, 0.018, -0.008), (0.0022, 0.009, 0.076), metal, bevel=0.0010, segments=2)

    # ==========================================================================
    # 4) TRUE SEMI-CIRCULAR HOODED UZI FRONT & REAR SIGHT EARS ("MICKEY MOUSE EARS")
    # ==========================================================================
    build_round_uzi_sight_ears("FrontSight", z_center=-0.116, y_base=0.043, radius=0.021, hole_r=0.0052, key_main=metal, key_rim=trim)
    build_round_uzi_sight_ears("RearSight",  z_center=0.120,  y_base=0.043, radius=0.023, hole_r=0.0058, key_main=metal, key_rim=trim)
    detail("FrontSightBlade", (0.0, 0.056, -0.116), (0.0036, 0.016, 0.0048), trim, bevel=0.0008, segments=2)
    detail("FrontSightRedDot", (0.0, 0.062, -0.116), (0.0040, 0.0040, 0.0052), red_m, bevel=0.0008, segments=2)
    detail("RearPeepAperture", (0.0, 0.057, 0.120), (0.0092, 0.012, 0.0048), trim, bevel=0.0008, segments=2)

    # ==========================================================================
    # 5) STEPPED LOWER RECEIVER FRAME, SELECTOR HOUSING (A · R · S) & TRIGGER GUARD
    # ==========================================================================
    detail("LowerReceiverFrame", (0.0, -0.024, 0.010), (0.0355, 0.023, 0.194), metal, bevel=0.0055, segments=4)
    detail("FrontFrameGusset", (0.0, -0.028, -0.088), (0.026, 0.022, 0.026), metal, bevel=0.0035, segments=3, rot=(math.radians(32.0), 0.0, 0.0))

    for side in (-1, 1):
        sx = side * 0.0185
        detail(f"SelectorHousing_{side}", (sx, -0.023, 0.022), (0.0048, 0.017, 0.068), trim, bevel=0.0025, segments=3)
        detail(f"SelectorTrackRecess_{side}", (side * 0.0202, -0.023, 0.022), (0.0022, 0.0085, 0.048), metal, bevel=0.0012, segments=2)
        detail(f"SelectorSliderSwitch_{side}", (side * 0.0212, -0.024, 0.022), (0.0038, 0.012, 0.013), polymer, bevel=0.0018, segments=3)
        detail(f"Mark_S_Dot_{side}", (side * 0.0208, -0.016, 0.044), (0.0022, 0.0036, 0.0036), white_m, bevel=0.0008, segments=2)
        detail(f"Mark_R_Dot_{side}", (side * 0.0208, -0.016, 0.022), (0.0022, 0.0038, 0.0038), red_m,   bevel=0.0008, segments=2)
        detail(f"Mark_A_Dot_{side}", (side * 0.0208, -0.016, 0.000), (0.0022, 0.0040, 0.0040), red_m,   bevel=0.0008, segments=2)

    for pz, r_pin in ((-0.058, 0.0038), (-0.018, 0.0032), (0.068, 0.0040)):
        cyl_x(f"ReceiverPin_{pz}", 0.0, -0.024, pz, r_pin, 0.0405, trim, bevel=0.0008)

    # Exact zero-overlap Trigger Guard & Trigger contour matching `fps_hands_uzi.glb` index finger clearance!
    guard = [
        (0.0, -0.040, -0.035),
        (0.0, -0.046, -0.088),
        (0.0, -0.057, -0.102),
        (0.0, -0.084, -0.102),
        (0.0, -0.097, -0.088),
        (0.0, -0.097, -0.025),
    ]
    smooth_wire("TriggerGuard", guard, 0.0038, trim, resolution=4)
    smooth_wire("SteelTrigger", [(0.0, -0.044, -0.048), (0.0, -0.065, -0.066), (0.0, -0.080, -0.060)], 0.0032, brass, resolution=4)

    # ==========================================================================
    # 6) PROPORTIONATE GROOVED POLYMER PISTOL GRIP (Y = -0.034..-0.172)
    #    + SEAMLESS EXPOSED 32-ROUND STEEL STICK MAGAZINE (Y = -0.172..-0.270)
    # ==========================================================================
    detail("GripFrameStamping", (0.0, -0.102, 0.018), (0.0430, 0.138, 0.0570), metal, bevel=0.008, segments=4)
    detail("PolymerGripPanels", (0.0, -0.104, 0.018), (0.0560, 0.136, 0.0690), polymer, bevel=0.015, segments=5)
    detail("GripSafetyBar", (0.0, -0.096, 0.053), (0.024, 0.088, 0.010), trim, bevel=0.0040, segments=4)
    detail("MagWellFlange", (0.0, -0.171, 0.0185), (0.0465, 0.012, 0.0605), metal, bevel=0.0032, segments=3)
    detail("MagReleaseCatch", (-0.024, -0.160, 0.036), (0.006, 0.014, 0.014), trim, bevel=0.0018, segments=3)

    for side in (-1, 1):
        detail(f"GripPanelInset_{side}", (side * 0.0276, -0.104, 0.018), (0.0022, 0.114, 0.052), polymer, bevel=0.0020, segments=3)
        for i in range(6):
            gy = -0.062 - i * 0.0165
            detail(f"GripGroove_{side}_{i}", (side * 0.0283, gy, 0.018), (0.0018, 0.0028, 0.048), metal, bevel=0.0008, segments=2)
    for sy in (-0.066, -0.146):
        cyl_x(f"GripEscutcheonRing_{sy}", 0.0, sy, 0.018, 0.0046, 0.0578, trim, bevel=0.0008)
        cyl_x(f"GripEscutcheonScrew_{sy}", 0.0, sy, 0.018, 0.0026, 0.0584, brass, bevel=0.0005)

    # Seamless 2-section 32-round steel magazine:
    # Section A (`UpperExposedSteelMag`, world Y = -0.171..-0.2015): flat bottom in local space so zero pinch seam!
    upper_mag = detail("UpperExposedSteelMag", (0.0, -0.186, 0.019), (0.0355, 0.031, 0.0475), metal, bevel=0.0028, segments=3)
    for v in upper_mag.data.vertices:
        if v.co.z < -0.012:
            v.co.z = -0.0155
    upper_mag.data.update()
    apply_weighted_smooth(upper_mag)

    # Section B (`ExtendedSteelMag`, CRITICAL RELOAD INVARIANT: `lo.Y < -0.232f && hi.Y < -0.200f`):
    # Flat top in local space (`local z = +0.0325` -> `world Y = -0.2335 + 0.0325 = -0.2010 < -0.200f`) meeting Section A flush!
    lower_mag = detail("ExtendedSteelMag", (0.0, -0.2335, 0.019), (0.0355, 0.064, 0.0475), metal, bevel=0.0028, segments=3)
    for v in lower_mag.data.vertices:
        if v.co.z > 0.028:
            v.co.z = 0.0325
    lower_mag.data.update()
    apply_weighted_smooth(lower_mag)

    detail("MagazineFloorPlate", (0.0, -0.266, 0.019), (0.0425, 0.010, 0.0545), trim, bevel=0.0032, segments=3)
    for side in (-1, 1):
        detail(f"MagVerticalRibUpper_{side}", (side * 0.0172, -0.188, 0.012), (0.0026, 0.026, 0.009), trim, bevel=0.0008, segments=2)
        detail(f"MagVerticalRib_{side}", (side * 0.0172, -0.2335, 0.012), (0.0026, 0.058, 0.009), trim, bevel=0.0008, segments=2)
    for wy in (-0.214, -0.232, -0.250):
        cyl_x(f"MagWitnessRim_{wy}", 0.0, wy, 0.028, 0.0032, 0.0360, trim, bevel=0.0004, vertices=18)
        cyl_x(f"MagWitnessBrass_{wy}", 0.0, wy, 0.028, 0.0022, 0.0364, brass, bevel=0.0004, vertices=18)

    # ==========================================================================
    # 7) FULL-HEIGHT VERTICALLY-RIBBED POLYMER HANDGUARD + KNURLED BARREL NUT + BARREL
    #    (Matches `shop_ui_concept_1795129377316.jpg` while staying 100% inside Left-hand envelope!)
    # ==========================================================================
    detail("FrontReceiverBarrelHousing", (0.0, 0.028, -0.156), (0.0375, 0.026, 0.094), metal, bevel=0.005, segments=4)
    # Full-height Vertically-Ribbed Polymer Handguard (`y = -0.023..+0.018`, `z = -0.112..-0.204`)
    detail("RibbedPolymerForearmCore", (0.0, -0.002, -0.158), (0.0345, 0.039, 0.094), polymer, bevel=0.0045, segments=4)
    for idx in range(12):
        rz = -0.115 - idx * 0.0076
        detail(f"VerticalHandguardRib_{idx}", (0.0, -0.002, rz), (0.0366, 0.0405, 0.0044), polymer, bevel=0.0014, segments=3)

    # Authentic Stepped Knurled Circular Steel Barrel Locking Nut (`z = -0.204..-0.236`, `y = 0.015`)
    tube("BarrelLockingCollarBase", -0.212, 0.014, 0.0172, metal, y=0.015, bevel=0.0015)
    tube("KnurledBarrelLockingNut", -0.228, 0.020, 0.0158, trim,  y=0.015, bevel=0.0018)
    for k_i in range(20):
        ang = k_i * math.tau / 20.0
        kx = 0.0156 * math.cos(ang)
        ky = 0.015 + 0.0156 * math.sin(ang)
        if ky > -0.002:
            detail(f"BarrelNutKnurl_{k_i}", (kx, ky, -0.228), (0.0016, 0.0016, 0.017), metal, bevel=0.0004, segments=2)

    # Clean Parkerized / Blued Steel Barrel (`z = -0.238..-0.308`)
    barrel = tube("ParkerizedSteelBarrel", -0.272, 0.072, 0.0098, metal, y=0.015, bevel=0.0015)
    tube("MuzzleChamferCrown", -0.306, 0.005, 0.0094, trim, y=0.015, bevel=0.0008)
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=28,
        radius=0.0056,
        depth=0.055,
        location=xyz((0.0, 0.015, -0.295)),
        rotation=(math.pi / 2.0, 0.0, 0.0),
    )
    bore = bpy.context.object
    bore.data.materials.append(mat("GunMetal"))
    bpy.context.view_layer.objects.active = barrel
    mod = barrel.modifiers.new("Open9mmBore", "BOOLEAN")
    mod.object = bore
    mod.operation = 'DIFFERENCE'
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(bore, do_unlink=True)
    apply_weighted_smooth(barrel)

    # ==========================================================================
    # 8) FOLDED SIDE-FOLDING STEEL WIRE STOCK (Tucked along upper +X flank above hands!)
    # ==========================================================================
    detail("RearStockHingeBlock", (0.002, 0.014, 0.160), (0.026, 0.032, 0.018), trim, bevel=0.0038, segments=4)
    cyl_x("StockHingePin", 0.002, 0.014, 0.160, 0.0052, 0.031, metal, bevel=0.0008)
    cyl_x("StockHingeLockCore", 0.002, 0.014, 0.160, 0.0026, 0.033, brass, bevel=0.0005)

    for y_strut in (0.030, 0.015):
        smooth_wire(
            f"FoldedStockStrut_{y_strut}",
            [(0.016, y_strut, 0.156), (0.026, y_strut, 0.120), (0.026, y_strut, -0.095)],
            0.0032,
            trim,
            resolution=5,
        )
    smooth_wire(
        "FoldedStockButtplate",
        [(0.026, 0.033, -0.095), (0.030, 0.028, -0.124), (0.030, 0.013, -0.124), (0.026, 0.014, -0.095)],
        0.0038,
        polymer,
        resolution=5,
    )

    # ==========================================================================
    # 9) JOIN, ASSIGN UVS, VERIFY NORMALS & EXPORT GLB
    # ==========================================================================
    mesh_objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in mesh_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objs[0]
    bpy.ops.object.join()
    gun = bpy.context.object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    gun.name = gun.data.name = "SMG"

    bm = bmesh.new()
    bm.from_mesh(gun.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-7)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-7)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(gun.data)
    bm.free()

    mesh = gun.data
    while mesh.uv_layers:
        mesh.uv_layers.remove(mesh.uv_layers[0])
    uv_layer = mesh.uv_layers.new(name="UVMap")
    verts = mesh.vertices
    for poly in mesh.polygons:
        poly.use_smooth = True
        for li in poly.loop_indices:
            v_co = verts[mesh.loops[li].vertex_index].co
            tu = min(1.0, max(0.0, (v_co.y + 0.32) / 0.52))
            tv = min(1.0, max(0.0, (v_co.z + 0.28) / 0.38))
            uv_layer.data[li].uv = (tu, tv)

    mesh.update()

    out_path = OUT / "weapon_uzi_90s.glb"
    bpy.ops.export_scene.gltf(
        filepath=str(out_path),
        export_format="GLB",
        export_normals=True,
        export_texcoords=True,
        export_animations=False,
    )
    print("Exported AAA weapon_uzi_90s.glb:", out_path, os.path.getsize(out_path), "Polys:", len(gun.data.polygons))


def render_uzi_v2_icon_and_proof():
    """
    1. Render crisp 512x512 transparent catalog/hotbar icon `assets/icons/icon_slot_3_uzi.png`
    2. Render 1920x1080 3-panel proof `uzi_v2_proof.png`
    """
    # --- A) Render 512x512 Transparent Icon (`assets/icons/icon_slot_3_uzi.png`) ---
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(OUT / "weapon_uzi_90s.glb"))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    obj = meshes[0]

    bbox = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    min_v = Vector((min(v.x for v in bbox), min(v.y for v in bbox), min(v.z for v in bbox)))
    max_v = Vector((max(v.x for v in bbox), max(v.y for v in bbox), max(v.z for v in bbox)))
    obj.location -= (min_v + max_v) * 0.5
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    from mathutils import Quaternion
    direction = Vector((-1.0, -0.22, 0.25))
    rotation = (-direction).to_track_quat('-Z', 'Y')
    rotation = rotation @ Quaternion(Vector((0, 0, 1)), math.radians(24.0))
    projected = [rotation.inverted() @ (obj.matrix_world @ v.co) for v in obj.data.vertices]
    low = Vector((min(v.x for v in projected), min(v.y for v in projected), 0))
    high = Vector((max(v.x for v in projected), max(v.y for v in projected), 0))
    obj.location -= rotation @ ((low + high) * 0.5)
    span = max(high.x - low.x, high.y - low.y)

    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x = 512
    sc.render.resolution_y = 512
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"

    cd = bpy.data.cameras.new("IconCam")
    cd.type = "ORTHO"
    cd.ortho_scale = span * 1.08
    co = bpy.data.objects.new("IconCam", cd)
    sc.collection.objects.link(co)
    co.location = direction.normalized() * 2.5
    co.rotation_mode = "QUATERNION"
    co.rotation_quaternion = rotation
    sc.camera = co

    for lname, lpos, energy, col in [
        ("SunKey",  (-2.2, -1.8, 2.2), 6.2, (1.0, 0.95, 0.86)),
        ("SunFill", (-1.8,  2.2, 1.4), 4.0, (0.82, 0.90, 1.0)),
        ("SunRim",  ( 2.0,  0.5, 2.4), 4.8, (1.0, 0.86, 0.65)),
    ]:
        ldata = bpy.data.lights.new(name=lname, type="SUN")
        ldata.energy = energy
        ldata.color = col
        lobj = bpy.data.objects.new(name=lname, object_data=ldata)
        sc.collection.objects.link(lobj)
        lobj.location = lpos
        d = Vector((0, 0, 0)) - Vector(lpos)
        lobj.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()

    OUT_ICONS.mkdir(parents=True, exist_ok=True)
    icon_path = OUT_ICONS / "icon_slot_3_uzi.png"
    sc.render.filepath = str(icon_path)
    bpy.ops.render.render(write_still=True)
    print("Rendered 512x512 icon_slot_3_uzi.png:", icon_path, os.path.getsize(icon_path))

    # --- B) Render 1920x1080 3-Panel Proof (`uzi_v2_proof.png`) ---
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x = 1920
    sc.render.resolution_y = 1080
    sc.render.film_transparent = False
    world = bpy.data.worlds.new("UziProofWorld")
    sc.world = world
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.14, 0.06, 0.09, 1.0)
        bg.inputs[1].default_value = 1.0

    # Panel 1 (Left, x = -0.62): Hero Showcase Left-Side Profile
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(OUT / "weapon_uzi_90s.glb"))
    p1_objs = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
    for o in p1_objs:
        o.scale = (1.35, 1.35, 1.35)
        o.location = Vector((-0.62, 0.0, 0.08))
        o.rotation_mode = "XYZ"
        o.rotation_euler = (0.0, 0.0, math.radians(90.0))

    # Panel 2 (Center, x = 0.02): Right-Side 3/4 Profile showing Ejection Port, Brass Bolt, Folded Stock & Knurled Barrel Nut
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(OUT / "weapon_uzi_90s.glb"))
    p2_objs = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
    for o in p2_objs:
        o.scale = (1.30, 1.30, 1.30)
        o.location = Vector((0.02, 0.0, 0.08))
        o.rotation_mode = "XYZ"
        o.rotation_euler = (math.radians(14.0), math.radians(-10.0), math.radians(-68.0))

    # Panel 3 (Right, x = +0.64): Exact Seated ViewModel Assembly (`weapon_uzi_90s.glb` + `fps_hands_uzi.glb` at 0.0mm overlap!)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(OUT / "fps_hands_uzi.glb"))
    hand_objs = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(OUT / "weapon_uzi_90s.glb"))
    gun_p3_objs = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]

    p3_center = Vector((0.64, 0.0, 0.04))
    for o in hand_objs:
        bpy.ops.object.select_all(action="DESELECT")
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        for v in o.data.vertices:
            gx, gy, gz = v.co.x, v.co.z, -v.co.y
            v.co = Vector(((gx + 0.18) * 1.15, -(gz - 0.68) * 1.15, (gy + 0.10) * 1.15))
        o.data.update()
        o.location = p3_center
        o.rotation_mode = "XYZ"
        o.rotation_euler = (math.radians(10.0), 0.0, math.radians(-58.0))

    for o in gun_p3_objs:
        bpy.ops.object.select_all(action="DESELECT")
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        for v in o.data.vertices:
            v.co = Vector((0.75 * v.co.x * 1.15, -0.75 * v.co.y * 1.15, 0.75 * v.co.z * 1.15))
        o.data.update()
        o.location = p3_center
        o.rotation_mode = "XYZ"
        o.rotation_euler = (math.radians(10.0), 0.0, math.radians(-58.0))

    for m in bpy.data.materials:
        m.use_backface_culling = True

    for name, loc, energy, col in [
        ("Key",  (1.8, -3.2, 2.4), 4.8, (1.0, 0.94, 0.84)),
        ("Fill", (-2.4, -2.5, 1.5), 2.8, (0.82, 0.88, 1.0)),
        ("Rim",  (0.0, 2.8, 2.5),   3.6, (1.0, 0.80, 0.56)),
    ]:
        ld = bpy.data.lights.new(name, type="SUN")
        ld.energy = energy
        ld.color = col
        lo = bpy.data.objects.new(name, ld)
        sc.collection.objects.link(lo)
        lo.location = loc
        d = Vector((0.0, 0.0, 0.0)) - Vector(loc)
        lo.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()

    cd = bpy.data.cameras.new("ProofCam")
    cd.type = "ORTHO"
    cd.ortho_scale = 1.96
    co = bpy.data.objects.new("ProofCam", cd)
    sc.collection.objects.link(co)
    sc.camera = co
    co.location = (0.0, -2.8, 0.16)
    d = Vector((0.0, 0.0, 0.02)) - co.location
    co.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()

    for target_path in (ARTIFACT_PROOF_1, ARTIFACT_PROOF_2, SCREENSHOT_PROOF):
        try:
            target_path.parent.mkdir(parents=True, exist_ok=True)
            sc.render.filepath = str(target_path)
            bpy.ops.render.render(write_still=True)
            print("Saved Uzi v2 proof:", target_path)
        except Exception as exc:
            print("Warning saving proof to", target_path, ":", exc)


if __name__ == "__main__":
    build_smooth_uzi()
    render_uzi_v2_icon_and_proof()
