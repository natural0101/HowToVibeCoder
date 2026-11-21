"""v8.0 AAA ART-DECO CORPORATE CYBER-SENTINEL ("SYSTEM ZERO") -> assets/models/escaped_ai_boss.glb

Stage 2 Concept-Art Master Sculpt matching `system_zero_turnaround_1795283054472.jpg`:
  - EXACT 10-part articulated skeleton contract for `BossArticulation.cs` / `CoderArticulation.cs`:
      Torso_Mesh     @ Pelvis   ( 0.000, 0.000, 1.135)
      Head_Mesh      @ Neck     ( 0.000, 0.000, 1.880)
      UpperArmL_Mesh @ Shld L   (+0.285, 0.000, 1.730)
      ForearmL_Mesh  @ Elbow L  (+0.285, 0.000, 1.375)
      UpperArmR_Mesh @ Shld R   (-0.285, 0.000, 1.730)
      ForearmR_Mesh  @ Elbow R  (-0.285, 0.000, 1.375)
      ThighL_Mesh    @ Hip L    (+0.135, 0.000, 1.110)
      ShinL_Mesh     @ Knee L   (+0.135, 0.000, 0.585)  [boot sole strictly at local z = -0.585]
      ThighR_Mesh    @ Hip R    (-0.135, 0.000, 1.110)
      ShinR_Mesh     @ Knee R   (-0.135, 0.000, 0.585)  [boot sole strictly at local z = -0.585]
  - Stage 2 Sculptural Upgrades:
      1. Chiseled angular helmet jaw/chin + recessed horizontal robo-mouth + carved cheek bevels with integrated
         AntiqueBrass inlays + sharper V-dip obsidian visor + wider uninterrupted blazing crimson central scanner flare
      2. Continuous sweeping S-curved flared horseshoe Executive Collar (ZERO box corners!) with double brass piping
      3. Pearl-ivory upper chest & pectorals wrapped TIGHTLY around the recessed brass Delta Reactor (ZERO black chest wedge!)
      4. Broad sculpted Torso Shoulder Mantle + curved dome-hugging 3-tier overlapping pauldrons (ZERO hollow gap when arms lift!)
      5. Sweeping Iliac Hip-Crest Armor + high parabolic upper-thigh carapace (ELIMINATES exposed round black hip balls!)
      6. Tapered angular V-notched shin greaves + faceted hexagonal kneecaps with brass borders (ZERO cylindrical tin-cans!)
      7. Broad, flat, chamfered stealth-wing Dorsal Heat-Sink Blades (ZERO thin radio-antenna sticks!)
      8. Luxury satin-matte Pearl-Ivory Ceramic (`rough=0.30`) + brighter champagne-gold AntiqueBrass for crisp Raylib readability!
"""
import json
import math
import os
import struct
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[3]
OUT_MODELS = ROOT / "assets/models"
OUT_SHOTS = ROOT / "screenshots"
TARGET_HEIGHT = 2.2365

PART_ORDER = [
    "Torso",
    "Head",
    "UpperArmL",
    "ForearmL",
    "UpperArmR",
    "ForearmR",
    "ThighL",
    "ShinL",
    "ThighR",
    "ShinR",
]

PIVOTS = {
    "Torso":     Vector(( 0.000, 0.000, 1.135)),
    "Head":      Vector(( 0.000, 0.000, 1.880)),
    "UpperArmL": Vector(( 0.285, 0.000, 1.730)),
    "ForearmL":  Vector(( 0.285, 0.000, 1.375)),
    "UpperArmR": Vector((-0.285, 0.000, 1.730)),
    "ForearmR":  Vector((-0.285, 0.000, 1.375)),
    "ThighL":    Vector(( 0.135, 0.000, 1.110)),
    "ShinL":     Vector(( 0.135, 0.000, 0.585)),
    "ThighR":    Vector((-0.135, 0.000, 1.110)),
    "ShinR":     Vector((-0.135, 0.000, 0.585)),
}

MAT_ORDER = [
    "WhiteCeramic",
    "AntiqueBrass",
    "DarkArmor",
    "SatinTitanium",
    "CarbonSubframe",
    "VisorObsidian",
    "PistonChrome",
    "CrimsonCore",
]

MAT_PROPS = {
    # (metallic, roughness, emission_rgb_or_None, emission_strength)
    "WhiteCeramic":   (0.04, 0.30, None, 0.0),   # Luxury satin-matte pearl-ivory porcelain (zero cheap plastic glare!)
    "AntiqueBrass":   (0.88, 0.24, None, 0.0),   # Bright brushed champagne-gold Art-Deco brass
    "DarkArmor":      (0.82, 0.28, None, 0.0),   # Satin anodized gunmetal-charcoal titanium
    "SatinTitanium":  (0.88, 0.20, None, 0.0),   # Aerospace brushed titanium
    "CarbonSubframe": (0.20, 0.52, None, 0.0),   # Matte woven carbon nanoweave musculature
    "VisorObsidian":  (0.96, 0.04, None, 0.0),   # Liquid-mirror jet-obsidian canopy glass
    "PistonChrome":   (0.98, 0.08, None, 0.0),   # Mirror-polished mechanical chrome
    "CrimsonCore":    (0.00, 0.12, (1.00, 0.004, 0.009), 3.10),  # Deep menacing blazing ruby-crimson!
}


# =============================================================================
# 1. 8-MATERIAL STUDIO HORIZON REFLECTION & AO LUT GENERATOR
# =============================================================================

def build_boss_lut_texture(key):
    """Per-material 256x256 2D LUT calibrated for luxury satin porcelain & crisp Raylib gold readability."""
    img_name = f"BossLUT_{key}"
    existing = bpy.data.images.get(img_name)
    if existing is not None:
        return existing

    W, H = 256, 256
    arr = np.ones((H, W, 4), dtype=np.float32)
    rng = np.random.default_rng(8026 + sum(ord(c) for c in key))
    y, x = np.mgrid[0:H, 0:W].astype(np.float32)
    u = x / float(W - 1)
    v = y / float(H - 1)

    ao_part = np.clip(u / 0.56, 0.0, 1.0)
    edge_part = np.clip((u - 0.56) / 0.44, 0.0, 1.0)
    noise = rng.normal(0.0, 1.0, (H, W)).astype(np.float32)

    sky_peak = np.exp(-((v - 0.78) / 0.15) ** 2)
    sec_peak = np.exp(-((v - 0.94) / 0.09) ** 2)
    horiz_dip = 1.0 - 0.22 * np.exp(-((v - 0.45) / 0.12) ** 2)
    floor_bounce = 0.16 * np.exp(-((v - 0.16) / 0.15) ** 2)

    if key == "WhiteCeramic":
        # Luxury satin-matte warm pearl-ivory porcelain (softened specular, rich sculptural AO depth!)
        ao_curve = 0.34 + 0.66 * (ao_part ** 1.32)
        satin_sheen = 0.88 + 0.12 * sky_peak + 0.06 * sec_peak + 0.05 * floor_bounce
        edge_glint = 1.0 + 0.11 * (edge_part ** 1.25)
        base = np.clip(0.84 * ao_curve * satin_sheen * edge_glint * (1.0 + 0.004 * noise), 0.24, 0.96)
        crev = (1.0 - ao_part) * 0.16
        arr[:, :, 0] = np.clip(base * (1.01 - crev * 0.14) + 0.03 * sky_peak * ao_part, 0.18, 0.96)
        arr[:, :, 1] = np.clip(base * (0.97 - crev * 0.15) + 0.03 * sky_peak * ao_part, 0.17, 0.94)
        arr[:, :, 2] = np.clip(base * (0.89 - crev * 0.11) + 0.03 * sky_peak * ao_part, 0.15, 0.88)

    elif key == "AntiqueBrass":
        # Brighter, unmistakable champagne-gold / antique Art-Deco brass for BOTH Eevee & live Raylib!
        ao_curve = 0.34 + 0.66 * (ao_part ** 1.20)
        metal_env = 0.74 * horiz_dip + 0.66 * sky_peak + 0.34 * sec_peak + 0.24 * floor_bounce
        edge_wear = 1.0 + 0.42 * (edge_part ** 1.20)
        brush = 1.0 + 0.044 * np.sin(y * 1.9 + 0.4 * np.sin(x * 0.12)) + 0.014 * noise
        base = np.clip(0.66 * ao_curve * metal_env * edge_wear * brush, 0.16, 0.98)
        arr[:, :, 0] = np.clip(base * 1.15, 0.18, 1.00)
        arr[:, :, 1] = np.clip(base * 0.93, 0.14, 0.88)
        arr[:, :, 2] = np.clip(base * 0.49, 0.06, 0.52)

    elif key == "SatinTitanium":
        ao_curve = 0.26 + 0.74 * (ao_part ** 1.28)
        metal_env = 0.70 * horiz_dip + 0.58 * sky_peak + 0.28 * sec_peak + floor_bounce
        edge_wear = 1.0 + 0.46 * (edge_part ** 1.30)
        brush = 1.0 + 0.034 * np.sin(y * 1.55) + 0.014 * noise
        base = np.clip(0.32 * ao_curve * metal_env * edge_wear * brush, 0.07, 0.84)
        arr[:, :, 0] = np.clip(base * 0.93, 0.06, 0.80)
        arr[:, :, 1] = np.clip(base * 0.95, 0.06, 0.82)
        arr[:, :, 2] = np.clip(base * 0.99, 0.07, 0.86)

    elif key == "DarkArmor":
        ao_curve = 0.24 + 0.76 * (ao_part ** 1.30)
        metal_env = 0.74 * horiz_dip + 0.56 * sky_peak + 0.24 * sec_peak + 0.14 * floor_bounce
        edge_wear = 1.0 + 0.76 * (edge_part ** 1.35)
        grain = 1.0 + 0.028 * np.sin(y * 1.35) + 0.014 * noise
        base = np.clip(0.088 * ao_curve * metal_env * edge_wear * grain, 0.022, 0.30)
        arr[:, :, 0] = np.clip(base * 0.92, 0.018, 0.28)
        arr[:, :, 1] = np.clip(base * 0.94, 0.019, 0.29)
        arr[:, :, 2] = np.clip(base * 1.02, 0.022, 0.33)

    elif key == "CarbonSubframe":
        ao_curve = 0.26 + 0.74 * (ao_part ** 1.26)
        weave = 1.0 + 0.18 * np.sin((x + y) * 1.30) * np.cos((x - y) * 1.30) + 0.018 * noise
        base = np.clip(0.036 * ao_curve * (0.88 + 0.24 * sky_peak) * weave, 0.009, 0.100)
        arr[:, :, 0] = base * 0.94
        arr[:, :, 1] = base * 0.95
        arr[:, :, 2] = base * 1.00

    elif key == "VisorObsidian":
        visor_streak = np.exp(-((v - 0.77) / 0.065) ** 2)
        visor_rim = np.exp(-((v - 0.32) / 0.09) ** 2)
        base = np.clip(0.010 * (0.55 + 0.45 * ao_part) + 0.22 * visor_streak + 0.036 * visor_rim, 0.003, 0.32)
        arr[:, :, 0] = np.clip(base * 0.82, 0.003, 0.28)
        arr[:, :, 1] = np.clip(base * 0.90, 0.003, 0.31)
        arr[:, :, 2] = np.clip(base * 1.10, 0.004, 0.37)

    elif key == "PistonChrome":
        chrome_env = 0.28 + 0.82 * sky_peak + 0.46 * sec_peak + 0.22 * floor_bounce
        chrome_env *= (1.0 - 0.48 * np.exp(-((v - 0.48) / 0.09) ** 2))
        ao_curve = 0.36 + 0.64 * (ao_part ** 1.20)
        base = np.clip(0.64 * ao_curve * chrome_env * (1.0 + 0.16 * edge_part), 0.14, 0.98)
        arr[:, :, 0] = np.clip(base * 0.96, 0.12, 0.97)
        arr[:, :, 1] = np.clip(base * 0.97, 0.13, 0.98)
        arr[:, :, 2] = np.clip(base * 1.01, 0.14, 1.00)

    elif key == "CrimsonCore":
        pulse = np.clip(0.55 * u + 0.45 * v, 0.0, 1.0)
        arr[:, :, 0] = np.clip(0.84 + 0.16 * pulse, 0.80, 1.00)
        arr[:, :, 1] = np.clip(0.004 + 0.014 * (pulse ** 2.6), 0.003, 0.022)
        arr[:, :, 2] = np.clip(0.010 + 0.018 * (pulse ** 2.4), 0.008, 0.034)

    img = bpy.data.images.new(img_name, width=W, height=H, alpha=False)
    img.pixels.foreach_set(arr.ravel())
    img.pack()
    return img


def init_materials():
    mats = {}
    for key in MAT_ORDER:
        met, rough, em, em_str = MAT_PROPS[key]
        m = bpy.data.materials.new(name=key)
        m.use_nodes = True
        m.diffuse_color = (1.0, 1.0, 1.0, 1.0)
        tree = m.node_tree
        bsdf = tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = (1.0, 1.0, 1.0, 1.0)
        bsdf.inputs["Metallic"].default_value = met
        bsdf.inputs["Roughness"].default_value = rough
        if em is not None and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (*em, 1.0)
            bsdf.inputs["Emission Strength"].default_value = em_str
        tex = tree.nodes.new("ShaderNodeTexImage")
        tex.image = build_boss_lut_texture(key)
        tex.extension = "EXTEND"
        tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        mats[key] = m
    return mats


# =============================================================================
# 2. CLEAN HARD-SURFACE GEOMETRY PRIMITIVES
# =============================================================================

def set_smooth_and_recalc(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    for p in obj.data.polygons:
        p.use_smooth = True
    obj.data.update()
    return obj


def apply_mod(obj, mod):
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.select_set(False)


def make_loft(name, rings, mat, closed_u=True, cap_start=False, cap_end=False,
              solidify_t=0.0, solidify_off=1.0, bevel_w=0.0, bevel_segs=2, subsurf_pre=0,
              outward_ref=None):
    bm = bmesh.new()
    v_rings = [[bm.verts.new(Vector(p)) for p in ring] for ring in rings]
    n_r = len(v_rings)
    n_s = len(v_rings[0])
    for ri in range(n_r - 1):
        r0, r1 = v_rings[ri], v_rings[ri + 1]
        u_lim = n_s if closed_u else (n_s - 1)
        for si in range(u_lim):
            ns = (si + 1) % n_s
            bm.faces.new((r0[si], r0[ns], r1[ns], r1[si]))
    if cap_start and closed_u:
        c0 = sum((v.co for v in v_rings[0]), Vector()) / n_s
        vc = bm.verts.new(c0)
        for si in range(n_s):
            ns = (si + 1) % n_s
            bm.faces.new((vc, v_rings[0][ns], v_rings[0][si]))
    if cap_end and closed_u:
        c1 = sum((v.co for v in v_rings[-1]), Vector()) / n_s
        vc = bm.verts.new(c1)
        for si in range(n_s):
            ns = (si + 1) % n_s
            bm.faces.new((vc, v_rings[-1][si], v_rings[-1][ns]))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    # CRITICAL: For open sheets (closed_u=False) BEFORE Solidify, ensure mirrored L/R grids ALWAYS
    # point strictly OUTWARD away from outward_ref so Solidify(offset=+1.0) NEVER extrudes inward!
    if (not closed_u) and outward_ref is not None and len(bm.faces) > 0:
        ref_v = Vector(outward_ref)
        bm.normal_update()
        dot_sum = 0.0
        for f in bm.faces:
            fc = f.calc_center_median()
            # Project reference onto same Z if outward_ref has z=None-sentinel, else use 3D radial
            rad = Vector((fc.x - ref_v.x, fc.y - ref_v.y, (fc.z - ref_v.z) if ref_v.z > -900.0 else 0.0))
            if rad.length > 1e-6:
                dot_sum += f.normal.dot(rad.normalized())
        if dot_sum < 0.0:
            bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)

    if subsurf_pre > 0:
        sub = obj.modifiers.new("SubPre", "SUBSURF")
        sub.levels = subsurf_pre
        sub.render_levels = subsurf_pre
        sub.boundary_smooth = "PRESERVE_CORNERS"
        apply_mod(obj, sub)

    if solidify_t > 0.0:
        sol = obj.modifiers.new("Solidify", "SOLIDIFY")
        sol.thickness = solidify_t
        sol.offset = solidify_off
        sol.use_even_offset = True
        sol.use_quality_normals = True
        apply_mod(obj, sol)

    if bevel_w > 0.0:
        bev = obj.modifiers.new("Bevel", "BEVEL")
        bev.width = bevel_w
        bev.segments = bevel_segs
        bev.limit_method = "ANGLE"
        bev.angle_limit = math.radians(38)
        bev.use_clamp_overlap = True
        apply_mod(obj, bev)

    return set_smooth_and_recalc(obj)


def make_sphere(name, center, radii, mat, segs=18, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segs, ring_count=rings, radius=1.0, location=center)
    o = bpy.context.active_object
    o.name = name
    o.scale = Vector(radii) if isinstance(radii, (tuple, list, Vector)) else Vector((radii, radii, radii))
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    o.data.materials.append(mat)
    return set_smooth_and_recalc(o)


def make_cyl(name, p0, p1, r1, r2, mat, segs=16, bevel_w=0.0):
    vec = Vector(p1) - Vector(p0)
    L = vec.length
    if L < 1e-5:
        return None
    mid = (Vector(p0) + Vector(p1)) * 0.5
    q = Vector((0.0, 0.0, 1.0)).rotation_difference(vec.normalized())
    bpy.ops.mesh.primitive_cone_add(vertices=segs, radius1=r1, radius2=r2, depth=L, location=mid)
    o = bpy.context.active_object
    o.name = name
    o.rotation_mode = "QUATERNION"
    o.rotation_quaternion = q
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    o.data.materials.append(mat)
    if bevel_w > 0.0:
        bev = o.modifiers.new("Bevel", "BEVEL")
        bev.width = bevel_w
        bev.segments = 2
        bev.limit_method = "ANGLE"
        bev.angle_limit = math.radians(36)
        bev.use_clamp_overlap = True
        apply_mod(o, bev)
    return set_smooth_and_recalc(o)


def make_rotary_joint(name, center, axis_dir, radius, depth, mats, brass_ring=True, segs=20):
    q = Vector((0.0, 0.0, 1.0)).rotation_difference(Vector(axis_dir).normalized())
    out = []

    def add_disc(tag, r, d, mat, bw):
        bpy.ops.mesh.primitive_cylinder_add(vertices=segs, radius=r, depth=d, location=center)
        o = bpy.context.active_object
        o.name = f"{name}_{tag}"
        o.rotation_mode = "QUATERNION"
        o.rotation_quaternion = q
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        o.data.materials.append(mat)
        if bw > 0.0:
            bev = o.modifiers.new("Bevel", "BEVEL")
            bev.width = bw
            bev.segments = 2
            bev.use_clamp_overlap = True
            apply_mod(o, bev)
        out.append(set_smooth_and_recalc(o))

    add_disc("Drum", radius, depth, mats["DarkArmor"], min(radius * 0.14, depth * 0.22))
    if brass_ring:
        add_disc("BrassRing", radius * 0.84, depth * 1.16, mats["AntiqueBrass"], min(radius * 0.08, depth * 0.15))
        add_disc("Inner", radius * 0.60, depth * 1.24, mats["DarkArmor"], min(radius * 0.06, depth * 0.12))
    add_disc("Axle", radius * 0.36, depth * 1.34, mats["PistonChrome"], min(radius * 0.06, depth * 0.10))
    return out


def make_piston(name, p0, p1, r_rod, r_sleeve, mats, segs=12):
    p0, p1 = Vector(p0), Vector(p1)
    out = []
    rod = make_cyl(f"{name}_Rod", p0, p1, r_rod, r_rod, mats["PistonChrome"], segs=segs)
    slv = make_cyl(f"{name}_Sleeve", p0.lerp(p1, 0.12), p0.lerp(p1, 0.66), r_sleeve, r_sleeve, mats["SatinTitanium"], segs=segs, bevel_w=0.0012)
    if rod:
        out.append(rod)
    if slv:
        out.append(slv)
    return out


# =============================================================================
# 3. TORSO_MESH — SWEEPING COLLAR, TIGHT CHEST AROUND REACTOR, HIP CRESTS & STEALTH DORSAL BLADES
# =============================================================================

def torso_radius_at(z):
    knots = [
        (0.96, 0.110, 0.095,  0.006),
        (1.06, 0.156, 0.114,  0.004),
        (1.14, 0.160, 0.112,  0.002),
        (1.25, 0.145, 0.105,  0.002),
        (1.38, 0.168, 0.116, -0.002),
        (1.53, 0.210, 0.134, -0.006),
        (1.67, 0.248, 0.146, -0.008),
        (1.78, 0.240, 0.132,  0.000),
        (1.86, 0.136, 0.100,  0.006),
    ]
    if z <= knots[0][0]:
        return knots[0][1], knots[0][2], knots[0][3]
    if z >= knots[-1][0]:
        return knots[-1][1], knots[-1][2], knots[-1][3]
    for i in range(len(knots) - 1):
        z0, rx0, ry0, yc0 = knots[i]
        z1, rx1, ry1, yc1 = knots[i + 1]
        if z0 <= z <= z1:
            t = (z - z0) / (z1 - z0)
            s = t * t * (3.0 - 2.0 * t)
            return rx0 + (rx1 - rx0) * s, ry0 + (ry1 - ry0) * s, yc0 + (yc1 - yc0) * s
    return knots[-1][1], knots[-1][2], knots[-1][3]


def torso_surf(z, ang, extra=0.0):
    rx, ry, yc = torso_radius_at(z)
    ct, st = math.cos(ang), math.sin(ang)
    sq = 1.0 + 0.048 * (ct * ct) * (st * st) * 4.0
    front_chest = 0.014 * max(0.0, -st) * math.exp(-((z - 1.65) / 0.13) ** 2)
    return Vector((ct * (rx + extra) * sq, yc + st * (ry + extra + front_chest) * sq, z))


def build_torso_part(mats):
    objs = []

    # --- A) Sculpted Carbon-Fiber Biomechanical Core & Ribbed Obliques ---
    rings = []
    n_z, n_c = 26, 28
    for iz in range(n_z):
        t = iz / (n_z - 1)
        z = 0.97 + t * (1.86 - 0.97)
        rib_zone = math.exp(-((z - 1.35) / 0.15) ** 2)
        rib_wave = 0.0044 * math.sin(z * 118.0) * rib_zone
        ring = []
        for ic in range(n_c):
            ang = -math.pi * 0.5 + (2.0 * math.pi * ic) / n_c
            side_w = abs(math.cos(ang)) ** 1.3
            ring.append(torso_surf(z, ang, extra=rib_wave * side_w))
        rings.append(ring)
    objs.append(make_loft("TorsoCarbonCore", rings, mats["CarbonSubframe"], closed_u=True, cap_start=True, cap_end=True))

    # Recessed matte CarbonSubframe sockets so joints rotate cleanly without shiny exposed black balls!
    for sgn, tag in ((1.0, "L"), (-1.0, "R")):
        objs.append(make_sphere(f"ShoulderSocket_{tag}", (sgn * 0.246, 0.000, 1.730), (0.078, 0.078, 0.076), mats["DarkArmor"], segs=16, rings=10))
        objs.append(make_sphere(f"HipSocket_{tag}", (sgn * 0.135, 0.000, 1.110), (0.078, 0.078, 0.076), mats["CarbonSubframe"], segs=16, rings=10))
    objs.append(make_sphere("NeckSocketBase", (0.000, 0.000, 1.855), (0.084, 0.084, 0.056), mats["DarkArmor"], segs=16, rings=10))

    # --- B) CONTINUOUS ROUNDED FLARED HORSESHOE EXECUTIVE COLLAR (ZERO UPRIGHT FRONT TABS!) ---
    # Smooth sinusoidal Art-Deco chalice sweep hugging close around jawline with continuous double brass piping!
    c_rows, c_cols = 9, 40
    collar_outer, collar_inner = [], []
    a_start, a_end = math.radians(-88.0), math.radians(268.0)

    def collar_bounds(ang):
        # Smooth continuous sinusoidal funnel: 0 at front throat (-90), 0.5 at ears, 1.0 at nape (+90) -> ZERO front box tabs!
        u_wrap = 0.5 + 0.5 * math.sin(ang)
        front_s = max(0.0, -math.sin(ang))
        z_base = 1.774 - 0.016 * front_s
        z_top  = 1.808 + 0.220 * (u_wrap ** 0.70)
        return z_base, z_top, front_s

    for ir in range(c_rows):
        tv = ir / (c_rows - 1)
        row_o, row_i = [], []
        for ic in range(c_cols):
            tu = ic / (c_cols - 1)
            ang = a_start + tu * (a_end - a_start)
            ct, st = math.cos(ang), math.sin(ang)
            z_base, z_top, front_s = collar_bounds(ang)
            zc = z_base + tv * (z_top - z_base)
            # Snug sculpted chalice funnel curving smoothly outward at upper rim!
            flare = (0.026 * (tv ** 1.70) + 0.005 * math.sin(math.pi * tv)) * (1.0 - 0.42 * (front_s ** 1.3))
            rx = 0.110 + flare + 0.008 * (1.0 - tv)
            ry = 0.108 + flare * 0.90 + 0.007 * (1.0 - tv)
            yc = 0.002 - 0.014 * (1.0 - tv)
            row_o.append(Vector((ct * rx, yc + st * ry, zc)))
            row_i.append(Vector((ct * (rx - 0.005), yc + st * (ry - 0.005), zc)))
        collar_outer.append(row_o)
        collar_inner.append(row_i)

    torso_ref = (0.0, 0.0, -999.0)
    objs.append(make_loft(
        "ExecutiveCollarIvory", collar_outer, mats["WhiteCeramic"],
        closed_u=False, solidify_t=0.015, solidify_off=1.0, bevel_w=0.0026, subsurf_pre=1,
        outward_ref=torso_ref,
    ))
    objs.append(make_loft(
        "ExecutiveCollarInnerDark", collar_inner, mats["DarkArmor"],
        closed_u=False, solidify_t=0.006, solidify_off=-1.0, bevel_w=0.0014,
        outward_ref=torso_ref,
    ))

    # Continuous Double Brushed AntiqueBrass Piping along Upper Rim & Recessed Mid-Rim Groove!
    for band_tag, tv0, tv1, thick_b, rad_off in [
        ("CollarBrassTopRim",   0.89, 1.00, 0.017, 0.002),
        ("CollarBrassInnerPip", 0.78, 0.84, 0.006, 0.011),
        ("CollarBrassBaseTrim", 0.02, 0.13, 0.008, 0.014),
    ]:
        b_rows = []
        for ir in range(3):
            tv = tv0 + (ir / 2.0) * (tv1 - tv0)
            row_b = []
            for ic in range(c_cols):
                tu = ic / (c_cols - 1)
                ang = a_start + tu * (a_end - a_start)
                ct, st = math.cos(ang), math.sin(ang)
                z_base, z_top, front_s = collar_bounds(ang)
                zc = z_base + tv * (z_top - z_base) + (0.004 if (band_tag == "CollarBrassTopRim" and ir == 2) else 0.0)
                flare = (0.026 * (tv ** 1.70) + 0.005 * math.sin(math.pi * tv)) * (1.0 - 0.42 * (front_s ** 1.3))
                rx = 0.110 + flare + 0.008 * (1.0 - tv) + rad_off
                ry = 0.108 + flare * 0.90 + 0.007 * (1.0 - tv) + rad_off
                yc = 0.002 - 0.014 * (1.0 - tv)
                row_b.append(Vector((ct * rx, yc + st * ry, zc)))
            b_rows.append(row_b)
        objs.append(make_loft(
            band_tag, b_rows, mats["AntiqueBrass"],
            closed_u=False, solidify_t=thick_b, solidify_off=1.0, bevel_w=0.0016,
            outward_ref=torso_ref,
        ))

    # --- C) PEARL-IVORY UPPER CHEST & SPLIT PECTORALS WRAPPED TIGHTLY AROUND DELTA REACTOR! ---
    us_g = []
    for ir in range(5):
        tv = ir / 4.0
        row = []
        for ic in range(9):
            tu = ic / 8.0
            sx = (tu - 0.5) * 2.0
            ang = -math.pi * 0.5 + sx * math.radians(36.0)
            v_drop = -0.018 * (1.0 - abs(sx)) * (1.0 - tv)
            zc = 1.702 + tv * 0.095 + v_drop
            row.append(torso_surf(zc, ang, extra=0.015 + 0.006 * math.sin(math.pi * tv)))
        us_g.append(row)
    objs.append(make_loft(
        "UpperSternumIvoryBridge", us_g, mats["WhiteCeramic"],
        closed_u=False, solidify_t=0.015, solidify_off=1.0, bevel_w=0.0024, subsurf_pre=1,
        outward_ref=torso_ref,
    ))

    for sgn, tag in ((1.0, "L"), (-1.0, "R")):
        kb_g = []
        for ir in range(4):
            tv = ir / 3.0
            row = []
            for ic in range(3):
                tu = ic / 2.0
                a_mid = math.radians(26.0 - 16.0 * tv)
                ang = -math.pi * 0.5 + sgn * (a_mid + (tu - 0.5) * math.radians(6.5))
                zc = 1.708 + tv * 0.092
                row.append(torso_surf(zc, ang, extra=0.025 + 0.004 * tv))
            kb_g.append(row)
        objs.append(make_loft(
            f"SternumBrassBridge_{tag}", kb_g, mats["AntiqueBrass"],
            closed_u=False, solidify_t=0.007, solidify_off=1.0, bevel_w=0.0014,
            outward_ref=torso_ref,
        ))

    for sgn, tag in ((1.0, "L"), (-1.0, "R")):
        cl_g = []
        for ir in range(6):
            tv = ir / 5.0
            row = []
            for ic in range(8):
                tu = ic / 7.0
                ang = -math.pi * 0.5 + sgn * (math.radians(16.0) + tu * math.radians(136.0))
                zc = 1.745 + tv * 0.092 - 0.024 * math.sin(math.pi * tu) * (1.0 - tv)
                out_ext = 0.042 * (math.sin(math.pi * tu) ** 1.4) * (1.0 - 0.45 * tv)
                lift = 0.018 + out_ext + 0.008 * math.sin(math.pi * tv)
                row.append(torso_surf(zc, ang, extra=lift))
            cl_g.append(row)
        objs.append(make_loft(
            f"ClavicleShoulderMantle_{tag}", cl_g, mats["WhiteCeramic"],
            closed_u=False, solidify_t=0.015, solidify_off=1.0, bevel_w=0.0026, subsurf_pre=1,
            outward_ref=torso_ref,
        ))

        pec_g = []
        n_r, n_c = 6, 8
        for ir in range(n_r):
            tv = ir / (n_r - 1)
            row = []
            for ic in range(n_c):
                tu = ic / (n_c - 1)
                tri_notch = math.radians(22.0) * (0.16 + 0.84 * (tv ** 0.92))
                a_in = math.radians(3.5) + tri_notch
                a_out = math.radians(82.0) + math.radians(6.0) * tv
                ang = -math.pi * 0.5 + sgn * (a_in + tu * (a_out - a_in))
                z_lo = 1.482 + 0.072 * (tu ** 1.15)
                z_hi = 1.762 - 0.014 * ((1.0 - tu) ** 1.3)
                zc = z_lo + tv * (z_hi - z_lo)
                bulge = 0.015 + 0.022 * math.sin(math.pi * (0.14 + 0.76 * tv)) * math.sin(math.pi * (0.12 + 0.78 * tu))
                row.append(torso_surf(zc, ang, extra=bulge))
            pec_g.append(row)
        objs.append(make_loft(
            f"PectoralCuirass_{tag}", pec_g, mats["WhiteCeramic"],
            closed_u=False, solidify_t=0.017, solidify_off=1.0, bevel_w=0.0028, subsurf_pre=1,
            outward_ref=torso_ref,
        ))

        ps_g = []
        for ir in range(2):
            row = []
            for ic in range(6):
                tu = ic / 5.0
                ang = -math.pi * 0.5 + sgn * (math.radians(28.0) + tu * math.radians(52.0))
                zc = 1.662 + tu * 0.068 + ir * 0.010
                row.append(torso_surf(zc, ang, extra=0.029))
            ps_g.append(row)
        objs.append(make_loft(
            f"PectoralSeamAccent_{tag}", ps_g, mats["AntiqueBrass"],
            closed_u=False, solidify_t=0.0055, solidify_off=1.0,
            outward_ref=torso_ref,
        ))

        pb_g = []
        for ir in range(3):
            tv = ir / 2.0
            row = []
            for ic in range(7):
                tu = ic / 6.0
                ang = -math.pi * 0.5 + sgn * (math.radians(9.0) + tu * math.radians(82.0))
                z_mid = 1.468 + 0.076 * (tu ** 1.15)
                zc = z_mid + (tv - 0.5) * 0.017
                row.append(torso_surf(zc, ang, extra=0.022))
            pb_g.append(row)
        objs.append(make_loft(
            f"PectoralBrassTrim_{tag}", pb_g, mats["AntiqueBrass"],
            closed_u=False, solidify_t=0.009, solidify_off=1.0, bevel_w=0.0016,
            outward_ref=torso_ref,
        ))

        sr_g = []
        for ir in range(4):
            tv = ir / 3.0
            row = []
            for ic in range(5):
                tu = ic / 4.0
                ang = -math.pi * 0.5 + sgn * (math.radians(54.0) + tu * math.radians(56.0))
                zc = 1.240 + tv * 0.255 + 0.024 * tu
                row.append(torso_surf(zc, ang, extra=0.006))
            sr_g.append(row)
        objs.append(make_loft(
            f"LateralRibArmor_{tag}", sr_g, mats["DarkArmor"],
            closed_u=False, solidify_t=0.009, solidify_off=1.0, bevel_w=0.0018,
            outward_ref=torso_ref,
        ))

    # --- D) DEEPLY RECESSED 3-TIER TRIANGULAR RUBY-CRIMSON CORE IN BOLD ART-DECO BRASS BEZEL ---
    reactor_z = 1.636
    chest_y = torso_surf(reactor_z, -math.pi * 0.5, extra=0.020).y
    angs_tri = (-math.pi * 0.5, math.pi / 6.0, 5.0 * math.pi / 6.0)

    def tri_pts(rad, z_off=0.0):
        return [(rad * math.cos(a), reactor_z + z_off + rad * math.sin(a)) for a in angs_tri]

    def cy_tri(px, y_base):
        return y_base + (px * px) * 1.45

    r_brass_out = 0.134
    r_brass_in  = 0.104
    r_dark_in   = 0.078
    r_outer_bar = 0.068
    r_mid_dark  = 0.050
    r_inner_tri = 0.036

    y_emb = chest_y + 0.050
    y_fr  = chest_y - 0.028
    y_rec = chest_y + 0.006

    def make_tri_ring(name, r_o, r_i, y_front, y_back, mat, bw=0.0028, y_inner_front=None):
        if y_inner_front is None:
            y_inner_front = y_front
        bm = bmesh.new()
        po, pi = tri_pts(r_o), tri_pts(r_i)
        vob = [bm.verts.new((px, cy_tri(px, y_back), pz)) for px, pz in po]
        vof = [bm.verts.new((px, cy_tri(px, y_front), pz)) for px, pz in po]
        vif = [bm.verts.new((px, cy_tri(px, y_inner_front), pz)) for px, pz in pi]
        vib = [bm.verts.new((px, cy_tri(px, y_back), pz)) for px, pz in pi]
        for i in range(3):
            ni = (i + 1) % 3
            bm.faces.new((vob[i], vob[ni], vof[ni], vof[i]))
            bm.faces.new((vof[i], vof[ni], vif[ni], vif[i]))
            bm.faces.new((vif[i], vif[ni], vib[ni], vib[i]))
            bm.faces.new((vib[i], vib[ni], vob[ni], vob[i]))
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        m = bpy.data.meshes.new(name)
        bm.to_mesh(m)
        bm.free()
        o = bpy.data.objects.new(name, m)
        bpy.context.collection.objects.link(o)
        o.data.materials.append(mat)
        if bw > 0.0:
            bev = o.modifiers.new("Bevel", "BEVEL")
            bev.width = bw
            bev.segments = 2
            bev.use_clamp_overlap = True
            apply_mod(o, bev)
        return set_smooth_and_recalc(o)

    objs.append(make_tri_ring("ReactorBrassFrame", r_brass_out, r_brass_in, y_fr - 0.005, y_emb, mats["AntiqueBrass"], bw=0.0036))
    objs.append(make_tri_ring("ReactorDarkFunnel", r_brass_in * 1.03, r_dark_in, y_fr + 0.002, y_emb, mats["DarkArmor"], bw=0.0024, y_inner_front=y_rec))

    bm_bp = bmesh.new()
    vbp = [bm_bp.verts.new((px, cy_tri(px, y_rec + 0.016), pz)) for px, pz in tri_pts(r_brass_in)]
    vbp2 = [bm_bp.verts.new((px, cy_tri(px, y_emb), pz)) for px, pz in tri_pts(r_brass_in)]
    bm_bp.faces.new(list(reversed(vbp)))
    bm_bp.faces.new(vbp2)
    for i in range(3):
        ni = (i + 1) % 3
        bm_bp.faces.new((vbp[i], vbp[ni], vbp2[ni], vbp2[i]))
    bmesh.ops.recalc_face_normals(bm_bp, faces=list(bm_bp.faces))
    m_bp = bpy.data.meshes.new("ReactorBackWell")
    bm_bp.to_mesh(m_bp)
    bm_bp.free()
    o_bp = bpy.data.objects.new("ReactorBackWell", m_bp)
    bpy.context.collection.objects.link(o_bp)
    o_bp.data.materials.append(mats["DarkArmor"])
    objs.append(set_smooth_and_recalc(o_bp))

    objs.append(make_tri_ring("ReactorCrimsonOuterRing", r_dark_in * 0.98, r_outer_bar * 0.78, y_rec + 0.002, y_rec + 0.016, mats["CrimsonCore"], bw=0.0016))
    objs.append(make_tri_ring("ReactorInnerDarkDivider", r_mid_dark * 1.12, r_inner_tri * 1.02, y_rec - 0.003, y_rec + 0.015, mats["DarkArmor"], bw=0.0016))

    bm_c = bmesh.new()
    vc_r = [bm_c.verts.new((px, cy_tri(px, y_rec + 0.004), pz)) for px, pz in tri_pts(r_inner_tri)]
    vc_b = [bm_c.verts.new((px, cy_tri(px, y_rec + 0.017), pz)) for px, pz in tri_pts(r_inner_tri)]
    vc_a = bm_c.verts.new((0.0, y_rec - 0.007, reactor_z))
    for i in range(3):
        ni = (i + 1) % 3
        bm_c.faces.new((vc_a, vc_r[ni], vc_r[i]))
        bm_c.faces.new((vc_r[i], vc_r[ni], vc_b[ni], vc_b[i]))
    bm_c.faces.new(vc_b)
    bmesh.ops.recalc_face_normals(bm_c, faces=list(bm_c.faces))
    m_c = bpy.data.meshes.new("ReactorCrimsonCenterCore")
    bm_c.to_mesh(m_c)
    bm_c.free()
    o_c = bpy.data.objects.new("ReactorCrimsonCenterCore", m_c)
    bpy.context.collection.objects.link(o_c)
    o_c.data.materials.append(mats["CrimsonCore"])
    bev_c = o_c.modifiers.new("Bevel", "BEVEL")
    bev_c.width = 0.0020
    bev_c.segments = 2
    bev_c.use_clamp_overlap = True
    apply_mod(o_c, bev_c)
    objs.append(set_smooth_and_recalc(o_c))

    # --- E) 4 SHARP LAYERED V-CHEVRON IVORY ABDOMINAL PLATES ---
    ab_z_top = 1.498
    ab_z_bot = 1.224
    plate_step = (ab_z_top - ab_z_bot) / 4.0
    for idx in range(4):
        z_hi = ab_z_top - idx * plate_step - 0.004
        z_lo = z_hi - (plate_step - 0.015)
        span_deg = 50.0 - idx * 4.5
        grid = []
        for ir in range(4):
            tv = ir / 3.0
            row = []
            for ic in range(9):
                tu = ic / 8.0
                sx = (tu - 0.5) * 2.0
                v_dip = -0.064 * (1.0 - abs(sx))
                ang = -math.pi * 0.5 + sx * math.radians(span_deg)
                zc = (z_lo + tv * (z_hi - z_lo)) + v_dip
                ridge = 0.012 + 0.009 * (1.0 - abs(sx)) + 0.005 * math.sin(math.pi * tv)
                row.append(torso_surf(zc, ang, extra=ridge))
            grid.append(row)
        objs.append(make_loft(
            f"AbChevronIvory_{idx}", grid, mats["WhiteCeramic"],
            closed_u=False, solidify_t=0.012, solidify_off=1.0, bevel_w=0.0022, subsurf_pre=1,
        ))
        lip_g = []
        for ir in range(2):
            tv = ir
            row = []
            for ic in range(9):
                tu = ic / 8.0
                sx = (tu - 0.5) * 2.0
                v_dip = -0.064 * (1.0 - abs(sx))
                ang = -math.pi * 0.5 + sx * math.radians(span_deg * 0.98)
                zc = (z_lo - 0.008 + tv * 0.012) + v_dip
                row.append(torso_surf(zc, ang, extra=0.007))
            lip_g.append(row)
        objs.append(make_loft(
            f"AbChevronUnderSeam_{idx}", lip_g, mats["DarkArmor"],
            closed_u=False, solidify_t=0.007, solidify_off=1.0, bevel_w=0.0012,
        ))

    # --- F) SLEEK PELVIS V-SHIELD + SWEEPING ILIAC HIP-CREST WINGS (ELIMINATES EXPOSED HIP BALLS!) ---
    pel_g = []
    for ir in range(6):
        tv = ir / 5.0
        row = []
        for ic in range(9):
            tu = ic / 8.0
            sx = (tu - 0.5) * 2.0
            span = math.radians(14.0 + 44.0 * (tv ** 0.88))
            ang = -math.pi * 0.5 + sx * span
            v_top = -0.042 * (1.0 - abs(sx)) * tv
            z_lo = 0.968 + 0.125 * (abs(sx) ** 1.25)
            z_hi = 1.214 + v_top
            zc = z_lo + tv * (z_hi - z_lo)
            lift = 0.013 + 0.009 * (1.0 - abs(sx) * 0.85) * math.sin(math.pi * (0.18 + 0.72 * tv))
            row.append(torso_surf(zc, ang, extra=lift))
        pel_g.append(row)
    objs.append(make_loft(
        "PelvisFrontIvoryShield", pel_g, mats["WhiteCeramic"],
        closed_u=False, solidify_t=0.013, solidify_off=1.0, bevel_w=0.0024, subsurf_pre=1,
    ))

    # Smooth Blended Iliac Hip-Crest Contour (flush into waist-to-pelvis line, ZERO protruding corner tabs!)
    for sgn, tag in ((1.0, "L"), (-1.0, "R")):
        hip_w = []
        for ir in range(5):
            tv = ir / 4.0
            row = []
            for ic in range(7):
                tu = ic / 6.0
                ang = -math.pi * 0.5 + sgn * (math.radians(40.0) + tu * math.radians(96.0))
                arch_down = -0.044 * math.sin(math.pi * tu) * (1.0 - tv)
                # Smoothly tapers upper rim flush down into the waist belt curve at both front & back ends!
                top_blend = -0.026 * ((1.0 - math.sin(math.pi * tu)) ** 1.4) * tv
                zc = 1.112 + tv * 0.094 + arch_down + top_blend
                flare = 0.018 * math.sin(math.pi * tu) * (1.0 - 0.65 * tv)
                row.append(torso_surf(zc, ang, extra=0.011 + flare))
            hip_w.append(row)
        objs.append(make_loft(
            f"IliacHipCrestWing_{tag}", hip_w, mats["WhiteCeramic"],
            closed_u=False, solidify_t=0.012, solidify_off=1.0, bevel_w=0.0024, subsurf_pre=1,
            outward_ref=torso_ref,
        ))
        hb_g = [hip_w[3], hip_w[4]]
        objs.append(make_loft(
            f"IliacBrassTrim_{tag}",
            [[p + Vector((sgn * 0.003, 0.0, 0.001)) for p in r] for r in hb_g],
            mats["AntiqueBrass"], closed_u=False, solidify_t=0.0055, solidify_off=1.0,
            outward_ref=torso_ref,
        ))

    # Brushed AntiqueBrass Pelvic Waist Trim Band
    pbelt_g = []
    for ir in range(3):
        tv = ir / 2.0
        row = []
        for ic in range(11):
            tu = ic / 10.0
            sx = (tu - 0.5) * 2.0
            ang = -math.pi * 0.5 + sx * math.radians(74.0)
            zc = 1.204 - 0.040 * (1.0 - abs(sx)) + (tv - 0.5) * 0.013
            row.append(torso_surf(zc, ang, extra=0.020))
        pbelt_g.append(row)
    objs.append(make_loft(
        "PelvisBrassTrim", pbelt_g, mats["AntiqueBrass"],
        closed_u=False, solidify_t=0.008, solidify_off=1.0, bevel_w=0.0014,
        outward_ref=torso_ref,
    ))

    # Rear Sacrum / Glute Ceramic Armor Plates
    for sgn, tag in ((1.0, "L"), (-1.0, "R")):
        rg = []
        for ir in range(5):
            tv = ir / 4.0
            row = []
            for ic in range(6):
                tu = ic / 5.0
                ang = math.pi * 0.5 - sgn * (math.radians(8.0) + tu * math.radians(62.0))
                zc = 1.005 + tv * 0.205
                row.append(torso_surf(zc, ang, extra=0.012 + 0.006 * math.sin(math.pi * tv) * math.sin(math.pi * tu)))
            rg.append(row)
        objs.append(make_loft(
            f"PelvisRearPlate_{tag}", rg, mats["WhiteCeramic"],
            closed_u=False, solidify_t=0.013, solidify_off=1.0, bevel_w=0.0024, subsurf_pre=1,
            outward_ref=torso_ref,
        ))

    # --- G) DORSAL CARAPACE, ARTICULATED SPINE & BROAD FLAT STEALTH-WING DORSAL HEAT-SINK BLADES ---
    for sgn, tag in ((1.0, "L"), (-1.0, "R")):
        bg = []
        for ir in range(5):
            tv = ir / 4.0
            row = []
            for ic in range(6):
                tu = ic / 5.0
                ang = math.pi * 0.5 - sgn * (math.radians(14.0) + tu * math.radians(66.0))
                zc = 1.44 + tv * 0.34
                lift = 0.014 + 0.010 * math.sin(math.pi * tv) * math.sin(math.pi * tu)
                row.append(torso_surf(zc, ang, extra=lift))
            bg.append(row)
        objs.append(make_loft(
            f"BackScapulaPlate_{tag}", bg, mats["WhiteCeramic"],
            closed_u=False, solidify_t=0.015, solidify_off=1.0, bevel_w=0.0026, subsurf_pre=1,
            outward_ref=torso_ref,
        ))

    for idx in range(6):
        vz = 1.24 + idx * 0.082
        p_b = torso_surf(vz, math.pi * 0.5, extra=0.014)
        by = p_b.y
        wv = 0.034 + 0.006 * math.sin(math.pi * idx / 5.0)
        dv, hv = 0.028, 0.022
        v_rings = [
            [Vector((-wv, by - 0.010, vz - hv)), Vector((wv, by - 0.010, vz - hv)),
             Vector((wv * 0.68, by + dv, vz - hv * 0.55)), Vector((-wv * 0.68, by + dv, vz - hv * 0.55))],
            [Vector((-wv * 0.85, by - 0.010, vz + hv)), Vector((wv * 0.85, by - 0.010, vz + hv)),
             Vector((wv * 0.48, by + dv * 0.80, vz + hv * 0.80)), Vector((-wv * 0.48, by + dv * 0.80, vz + hv * 0.80))],
        ]
        objs.append(make_loft(
            f"SpineVertebra_{idx}", v_rings,
            mats["SatinTitanium"] if idx % 2 == 0 else mats["DarkArmor"],
            closed_u=True, cap_start=True, cap_end=True, bevel_w=0.0020,
        ))

    # TWO BROAD, FLAT, CHAMFERED STEALTH-WING DORSAL HEAT-SINK BLADES (Bold wide wings from EVERY angle!)
    for sgn, tag in ((1.0, "L"), (-1.0, "R")):
        p_root = Vector((sgn * 0.118, 0.148, 1.695))
        p_tip  = Vector((sgn * 0.405, 0.320, 2.142))
        fin_dir = (p_tip - p_root).normalized()
        # Fore-aft stealth swept chord (reads wide & unmistakable from BOTH Front AND 3/4 views!)
        chord_raw = Vector((sgn * 0.15, 0.92, -0.36)).normalized()
        chord_dir = (chord_raw - fin_dir * chord_raw.dot(fin_dir)).normalized()
        thick_dir = fin_dir.cross(chord_dir).normalized()

        # Sculpted AntiqueBrass dorsal manifold root housing
        objs.append(make_cyl(
            f"DorsalFinMount_{tag}",
            p_root - fin_dir * 0.045, p_root + fin_dir * 0.056,
            0.048, 0.038, mats["AntiqueBrass"], segs=14, bevel_w=0.0024,
        ))

        fin_rings = []
        n_f = 9
        for i in range(n_f):
            t = i / (n_f - 1)
            center = p_root.lerp(p_tip, t)
            # Broad armored stealth-wing chord (156mm -> 56mm chisel tip) + strong chamfered profile!
            chord = 0.156 * (1.0 - 0.60 * (t ** 1.15)) + 0.016 * math.sin(math.pi * t)
            thick = 0.022 * (1.0 - 0.48 * t)
            tip_skew = chord_dir * (0.026 * t)
            ring = [
                center + tip_skew + chord_dir * ( chord * 0.54),
                center + tip_skew + chord_dir * ( chord * 0.24) + thick_dir * thick,
                center + tip_skew - chord_dir * ( chord * 0.30) + thick_dir * (thick * 0.88),
                center + tip_skew - chord_dir * ( chord * 0.54),
                center + tip_skew - chord_dir * ( chord * 0.30) - thick_dir * (thick * 0.88),
                center + tip_skew + chord_dir * ( chord * 0.24) - thick_dir * thick,
            ]
            fin_rings.append(ring)
        objs.append(make_loft(
            f"DorsalHeatSinkFin_{tag}", fin_rings, mats["DarkArmor"],
            closed_u=True, cap_start=True, cap_end=True, bevel_w=0.0024,
        ))

        # SatinTitanium center cooling louver spine!
        rail_rings = []
        for i in range(6):
            t = 0.08 + (i / 5.0) * 0.82
            center = p_root.lerp(p_tip, t) + chord_dir * (0.026 * t)
            chord = 0.032 * (1.0 - 0.48 * t)
            thick = 0.0245 * (1.0 - 0.44 * t)
            rail_rings.append([
                center + chord_dir * chord,
                center + thick_dir * thick,
                center - chord_dir * chord,
                center - thick_dir * thick,
            ])
        objs.append(make_loft(
            f"DorsalFinCoolRail_{tag}", rail_rings, mats["SatinTitanium"],
            closed_u=True, cap_start=True, cap_end=True, bevel_w=0.0014,
        ))

    return objs


# =============================================================================
# 4. HEAD_MESH — CHISELED STOIC JAW/CHIN, INTEGRATED BRASS CHEEK BEVELS & WIDE RUBY SCANNER FLARE
# =============================================================================

def build_head_part(mats):
    objs = []
    neck_p = PIVOTS["Head"]  # (0.000, 0.000, 1.880)
    # Seated 16mm deeper into the continuous rounded collar + fuller masculine dome/jawline!
    head_c = Vector((0.000, -0.014, 2.066))
    rx_base = 0.124
    ry_base = 0.150
    rz_top  = TARGET_HEIGHT - head_c.z  # ~0.1705 -> balanced rounded dome with wider rx/ry, crown at 2.2365m!
    rz_bot  = 0.152                     # strong squared chin seated snugly at Z ~ 1.914m inside collar!

    # Zero-Gap Mechanical Neck Ball-Socket + Short Muscular Ribbed Neck Column + Chrome Tendons
    objs.append(make_sphere("NeckJointBall", neck_p, (0.072, 0.072, 0.072), mats["DarkArmor"], segs=18, rings=12))

    neck_rings = []
    z_n0, z_n1 = neck_p.z - 0.036, head_c.z - 0.028
    for ir in range(12):
        t = ir / 11.0
        zc = z_n0 + t * (z_n1 - z_n0)
        rib = 1.0 + 0.055 * math.sin(t * 8.0 * math.pi)
        rx_n = (0.072 - 0.006 * t) * rib
        ry_n = (0.076 - 0.006 * t) * rib
        yc = neck_p.y + 0.004 - 0.012 * t
        neck_rings.append([Vector((math.cos(2 * math.pi * ic / 22) * rx_n, yc + math.sin(2 * math.pi * ic / 22) * ry_n, zc)) for ic in range(22)])
    objs.append(make_loft("BiomechNeckColumn", neck_rings, mats["CarbonSubframe"], closed_u=True, cap_start=True, cap_end=True))

    for sgn, tag in ((1.0, "L"), (-1.0, "R")):
        objs.extend(make_piston(
            f"NeckTendon_{tag}",
            (sgn * 0.062, neck_p.y - 0.010, neck_p.z - 0.010),
            (sgn * 0.054, head_c.y + 0.016, head_c.z - 0.064),
            0.007, 0.011, mats, segs=12,
        ))

    # --- Sculpted Blind Pearl-Ivory Aero-Helmet (Wider Squared Masculine Chin, Sharp Jaw Bevel & Stoic Mouth!) ---
    def helmet_pt(phi, theta, extra_r=0.0, carve_visor=True):
        cp, sp = math.cos(phi), math.sin(phi)
        ct, st = math.cos(theta), math.sin(theta)
        front_w = max(0.0, -st)
        back_w = max(0.0, st)
        az_abs = abs(math.atan2(ct, -st))

        # Stronger, wider squared masculine jaw taper (matches Panel 3 concept art!)
        jaw_taper = 1.0 if sp >= -0.10 else (1.0 - 0.24 * (((-sp - 0.10) / 0.90) ** 1.25))
        cheek_bevel = -0.0085 * math.exp(-((az_abs - math.radians(46.0)) / math.radians(15.0)) ** 2) * math.exp(-((sp + 0.40) / 0.20) ** 2)
        gonion_flare = 1.0 + 0.082 * math.exp(-((az_abs - math.radians(64.0)) / math.radians(18.0)) ** 2) * math.exp(-((sp + 0.42) / 0.18) ** 2)
        occip = 0.024 * back_w * math.exp(-((sp - 0.12) / 0.42) ** 2)
        brow = 0.015 * (front_w ** 1.35) * math.exp(-((sp - 0.34) / 0.16) ** 2)

        # Squared masculine front chin-guard + stoic carved lip plane!
        front_face_mask = math.exp(-((az_abs / math.radians(32.0)) ** 3.2))
        chin_block = 0.030 * front_face_mask * math.exp(-((sp + 0.64) / 0.17) ** 2)
        upper_lip  = 0.011 * front_face_mask * math.exp(-((sp + 0.41) / 0.062) ** 2)
        mouth_seam = -0.0095 * front_face_mask * math.exp(-((sp + 0.50) / 0.034) ** 2)
        lower_lip  = 0.0090 * front_face_mask * math.exp(-((sp + 0.57) / 0.055) ** 2)

        visor_recess = 0.0
        if carve_visor:
            v_az = max(0.0, 1.0 - (az_abs / math.radians(98.0)) ** 3.2)
            v_dip = -0.145 * ((1.0 - min(1.0, abs(ct) * 1.45)) ** 1.65)
            v_el = math.exp(-((sp - (0.04 + v_dip)) / 0.24) ** 2)
            visor_recess = -0.011 * v_az * v_el

        rx = (rx_base + extra_r + cheek_bevel) * jaw_taper * gonion_flare
        ry = (ry_base + extra_r) * (1.0 if sp >= 0 else 0.94)
        x = head_c.x + ct * cp * rx
        y = head_c.y + st * cp * ry + occip - brow - chin_block - upper_lip - mouth_seam - lower_lip - visor_recess
        z = head_c.z + (sp * (rz_top + extra_r * 0.55) if sp >= 0 else sp * (rz_bot + extra_r * 0.45))
        return Vector((x, y, z))

    n_lat, n_lon = 32, 46
    h_rings = []
    for il in range(n_lat):
        t = il / (n_lat - 1)
        phi = -0.47 * math.pi + t * (0.965 * math.pi)
        h_rings.append([helmet_pt(phi, (2.0 * math.pi * ic) / n_lon, 0.0, carve_visor=True) for ic in range(n_lon)])
    objs.append(make_loft("AeroHelmetCranium", h_rings, mats["WhiteCeramic"], closed_u=True, cap_start=True, cap_end=True))

    # --- Flush Curved Glossy Obsidian Wrap-Around Visor (Sharper V-Dip Nose Bridge!) ---
    v_rows, v_cols = 12, 32
    v_grid, b_grid = [], []
    for ir in range(v_rows):
        tv = ir / (v_rows - 1)
        vr, br = [], []
        for ic in range(v_cols):
            tu = ic / (v_cols - 1)
            ang = (tu - 0.5) * math.radians(184.0)
            theta = -math.pi * 0.5 + ang
            side_t = abs(tu - 0.5) * 2.0

            v_dip = -0.155 * ((1.0 - side_t) ** 1.50)
            phi_lo = -0.24 + 0.25 * (side_t ** 1.25) + v_dip
            phi_hi =  0.29 - 0.09 * (side_t ** 1.35) + v_dip * 0.45
            phi = phi_lo + tv * (phi_hi - phi_lo)

            flush_r = 0.0018 * math.sin(math.pi * tv) * (1.0 - 0.72 * (side_t ** 1.8))
            vr.append(helmet_pt(phi, theta, extra_r=flush_r, carve_visor=False))

            phi_b = (phi_lo - 0.036) + tv * ((phi_hi + 0.036) - (phi_lo - 0.036))
            br.append(helmet_pt(phi_b, theta, extra_r=-0.0018, carve_visor=False))
        v_grid.append(vr)
        b_grid.append(br)

    objs.append(make_loft("ObsidianVisorCanopy", v_grid, mats["VisorObsidian"], closed_u=False, solidify_t=0.0038, solidify_off=-1.0, outward_ref=head_c))
    objs.append(make_loft("VisorDarkBezel", b_grid, mats["DarkArmor"], closed_u=False, solidify_t=0.0040, solidify_off=1.0, outward_ref=head_c))

    # --- WIDER UNINTERRUPTED GLOWING RUBY-CRIMSON OPTICAL SCANNER + CENTRAL FLARE! ---
    s_grid = []
    for ir in range(4):
        tv = ir / 3.0
        row = []
        for ic in range(26):
            tu = ic / 25.0
            ang = (tu - 0.5) * math.radians(122.0)
            theta = -math.pi * 0.5 + ang
            side_t = abs(tu - 0.5) * 2.0
            center_flare = 0.025 * math.exp(-((side_t / 0.22) ** 2))
            half_h = 0.017 * (1.0 - 0.72 * (side_t ** 1.35)) + center_flare
            phi_mid = 0.032 - 0.068 * ((1.0 - side_t) ** 1.50) + 0.030 * side_t
            phi = phi_mid + (tv - 0.5) * 2.0 * half_h
            row.append(helmet_pt(phi, theta, extra_r=0.0056, carve_visor=False))
        s_grid.append(row)
    objs.append(make_loft("VisorCrimsonScannerSlit", s_grid, mats["CrimsonCore"], closed_u=False, solidify_t=0.0022, solidify_off=1.0, outward_ref=head_c))

    # --- Bold Brushed AntiqueBrass Crown Crest Stripe ---
    cr_rings = []
    cr_dark_l, cr_dark_r = [], []
    n_cr = 20
    for i in range(n_cr):
        t = i / (n_cr - 1)
        phi_a = math.radians(13.0) + t * math.radians(152.0)
        ct_p, st_p = math.cos(phi_a), math.sin(phi_a)
        yc = head_c.y - ct_p * (ry_base + 0.005)
        zc = head_c.z + st_p * (rz_top + 0.002)
        w_half = 0.034 * (math.sin(math.pi * (0.10 + 0.82 * t)) ** 0.65)
        if i == 0:
            w_half = 0.004
        elif i == 1:
            w_half = 0.018
        h_out = 0.0062 * math.sin(math.pi * (0.12 + 0.78 * t))
        nx_v = Vector((0.0, -ct_p, st_p)).normalized()
        p_mid = Vector((0.0, yc, zc))
        cr_rings.append([
            p_mid + Vector((-w_half, 0.0, 0.0)) - nx_v * 0.003,
            p_mid + Vector((-w_half * 0.76, 0.0, 0.0)) + nx_v * h_out,
            p_mid + Vector(( w_half * 0.76, 0.0, 0.0)) + nx_v * h_out,
            p_mid + Vector(( w_half, 0.0, 0.0)) - nx_v * 0.003,
        ])
        for sgn, arr_d in ((1.0, cr_dark_l), (-1.0, cr_dark_r)):
            w0 = w_half + 0.001
            w1 = w_half + 0.0065
            arr_d.append([
                p_mid + Vector((sgn * w0, 0.0, 0.0)) + nx_v * 0.0015,
                p_mid + Vector((sgn * w1, 0.0, 0.0)) + nx_v * 0.0005,
            ])

    objs.append(make_loft("HelmetBrassCrownCrest", cr_rings, mats["AntiqueBrass"], closed_u=True, cap_start=True, cap_end=True, bevel_w=0.0018))
    objs.append(make_loft("HelmetCrownSeamL", cr_dark_l, mats["DarkArmor"], closed_u=False, solidify_t=0.0024, solidify_off=-1.0, outward_ref=head_c))
    objs.append(make_loft("HelmetCrownSeamR", cr_dark_r, mats["DarkArmor"], closed_u=False, solidify_t=0.0024, solidify_off=-1.0, outward_ref=head_c))

    # --- Integrated Recessed Art-Deco Cheek Bevel Framing + Circular Ear Pods ---
    for sgn, tag in ((1.0, "L"), (-1.0, "R")):
        bg_c = []
        for ir in range(3):
            tv = ir / 2.0
            row = []
            for ic in range(9):
                tu = ic / 8.0
                theta = -math.pi * 0.5 + sgn * (math.radians(24.0) + tu * math.radians(58.0))
                phi = (-0.64 + 0.42 * tu) + tv * 0.095
                row.append(helmet_pt(phi, theta, extra_r=0.0005, carve_visor=False))
            bg_c.append(row)
        objs.append(make_loft(f"CheekDarkBevel_{tag}", bg_c, mats["DarkArmor"], closed_u=False, solidify_t=0.0026, solidify_off=1.0, outward_ref=head_c))

        for li, (phi_off, w_band) in enumerate([(0.010, 0.032), (0.056, 0.028)]):
            cg = []
            for ir in range(3):
                tv = ir / 2.0
                row = []
                for ic in range(9):
                    tu = ic / 8.0
                    theta = -math.pi * 0.5 + sgn * (math.radians(25.0) + tu * math.radians(56.0))
                    phi = (-0.64 + 0.42 * tu + phi_off) + tv * w_band
                    row.append(helmet_pt(phi, theta, extra_r=0.0018, carve_visor=False))
                cg.append(row)
            objs.append(make_loft(
                f"CheekBrassInlay_{tag}_{li}", cg, mats["AntiqueBrass"],
                closed_u=False, solidify_t=0.0032, solidify_off=1.0, bevel_w=0.0008,
                outward_ref=head_c,
            ))

        epos = Vector((sgn * (rx_base - 0.006), head_c.y + 0.012, head_c.z - 0.008))
        objs.extend(make_rotary_joint(
            f"HelmetEarPod_{tag}", epos, (sgn, 0.0, 0.0),
            radius=0.040, depth=0.020, mats=mats, brass_ring=True, segs=22,
        ))
        sh_g = []
        for ir in range(4):
            tv = ir / 3.0
            row = []
            for ic in range(6):
                tu = ic / 5.0
                theta = -math.pi * 0.5 + sgn * (math.radians(80.0) + tu * math.radians(34.0))
                phi = -0.24 + tv * 0.48
                row.append(helmet_pt(phi, theta, extra_r=0.005, carve_visor=False))
            sh_g.append(row)
        objs.append(make_loft(f"EarPodShroud_{tag}", sh_g, mats["WhiteCeramic"], closed_u=False, solidify_t=0.008, solidify_off=1.0, bevel_w=0.0016, outward_ref=head_c))

    return objs


# =============================================================================
# 5. UPPERARM_L/R — CURVED DOME-HUGGING 3-TIER PAULDRONS + BEEFY BICEP
# =============================================================================

def build_upper_arm_part(sgn, tag, mats):
    objs = []
    sh = PIVOTS[f"UpperArm{tag}"]  # (±0.285, 0, 1.730)
    el = PIVOTS[f"Forearm{tag}"]   # (±0.285, 0, 1.375)

    objs.append(make_sphere(f"ShoulderBall_{tag}", sh, (0.078, 0.080, 0.078), mats["DarkArmor"], segs=18, rings=12))
    objs.append(make_sphere(f"UpperElbowCap_{tag}", el, (0.052, 0.052, 0.052), mats["DarkArmor"], segs=16, rings=10))

    # --- CURVED DOME-HUGGING 3-TIER OVERLAPPING SAMURAI/ART-DECO PAULDRONS (ZERO FLAT PANCAKE FINS!) ---
    # Each tier follows a continuous spherical deltoid dome arc cascading tightly downward over the upper arm!
    tier_specs = [
        # (idx, phi_top_deg, phi_bot_deg, dome_r, span_deg, thick)
        (0,  80.0,  16.0, 0.156, 188.0, 0.018),   # Broad monumental sculpted upper deltoid dome cap
        (1,  28.0, -22.0, 0.142, 176.0, 0.015),   # Mid curved overlapping shell hugging deltoid
        (2,  -8.0, -52.0, 0.122, 164.0, 0.014),   # Lower curved overlapping shell transitioning into bicep!
    ]
    dome_c = Vector((sh.x - sgn * 0.010, sh.y, sh.z - 0.006))
    for tidx, p_top_d, p_bot_d, d_r, span_deg, thick in tier_specs:
        p_grid = []
        n_r, n_c = 7, 13
        for ir in range(n_r):
            tv = ir / (n_r - 1)  # 0 = lower lip -> 1 = upper crest
            phi = math.radians(p_bot_d + tv * (p_top_d - p_bot_d))
            cp, sp = math.cos(phi), math.sin(phi)
            # Crisp stepped samurai armor overhang on each lower lip!
            lip_flare = 0.016 * ((1.0 - tv) ** 1.55)
            row = []
            for ic in range(n_c):
                tu = ic / (n_c - 1)
                ang = (tu - 0.5) * math.radians(span_deg) * (0.78 + 0.22 * math.sin(math.pi * (0.14 + 0.72 * tv)))
                ca, sa = math.cos(ang), math.sin(ang)
                rad_x = (d_r + lip_flare) * (1.10 if tidx == 0 else (1.03 if tidx == 1 else 0.98))
                rad_y = (d_r + lip_flare * 0.7) * 0.88
                rad_z = d_r * 0.96
                xc = dome_c.x + sgn * (cp * ca * rad_x)
                yc = dome_c.y + cp * sa * rad_y
                zc = dome_c.z + sp * rad_z - 0.014 * (max(0.0, ca) ** 1.5) * (1.0 - tv)
                row.append(Vector((xc, yc, zc)))
            p_grid.append(row)

        objs.append(make_loft(
            f"PauldronTier{tidx}_{tag}", p_grid, mats["WhiteCeramic"],
            closed_u=False, solidify_t=thick, solidify_off=1.0, bevel_w=0.0026, subsurf_pre=1,
            outward_ref=dome_c,
        ))

        # Gleaming Brushed AntiqueBrass edge trim hugging each tier's curved lower rim!
        tr_grid = [
            [p + Vector((sgn * 0.003, 0.0, -0.004)) for p in p_grid[0]],
            [p + Vector((sgn * 0.004, 0.0,  0.000)) for p in p_grid[1]],
        ]
        objs.append(make_loft(
            f"PauldronBrassTrim{tidx}_{tag}", tr_grid, mats["AntiqueBrass"],
            closed_u=False, solidify_t=0.0075, solidify_off=1.0, bevel_w=0.0014,
            outward_ref=dome_c,
        ))

    # --- Beefy Athletic Carbon Biomechanical Bicep + 360-Wrap Armored Pearl-Ivory Bicep Cuffs! ---
    arm_ref = (sh.x, sh.y, -999.0)
    arm_rings = []
    for ir in range(10):
        t = ir / 9.0
        zc = sh.z - t * (sh.z - el.z)
        rx = 0.072 - 0.015 * t + 0.010 * math.sin(math.pi * t)
        ry = 0.074 - 0.016 * t + 0.010 * math.sin(math.pi * t)
        rib = 1.0 + 0.025 * math.sin(t * 14.0 * math.pi)
        arm_rings.append([Vector((sh.x + math.cos(2 * math.pi * ic / 18) * rx * rib, sh.y + math.sin(2 * math.pi * ic / 18) * ry * rib, zc)) for ic in range(18)])
    objs.append(make_loft(f"BicepCarbonCore_{tag}", arm_rings, mats["CarbonSubframe"], closed_u=True, cap_start=True, cap_end=True))

    # Extended 296-deg wrap sculpted Pearl-Ivory Bicep Armor (reads armored even when arm raises high!)
    bp_g = []
    for ir in range(5):
        tv = ir / 4.0
        zc = (el.z + 0.044) + tv * ((sh.z - 0.098) - (el.z + 0.044))
        rx = 0.071 + 0.010 * math.sin(math.pi * tv)
        ry = 0.073 + 0.010 * math.sin(math.pi * tv)
        row = []
        for ic in range(11):
            tu = ic / 10.0
            ang = (tu - 0.5) * math.radians(296.0)
            ca, sa = math.cos(ang), math.sin(ang)
            row.append(Vector((sh.x + sgn * ca * rx, sh.y + sa * ry, zc)))
        bp_g.append(row)
    objs.append(make_loft(
        f"BicepCeramicArmor_{tag}", bp_g, mats["WhiteCeramic"],
        closed_u=False, solidify_t=0.011, solidify_off=1.0, bevel_w=0.0020, subsurf_pre=1,
        outward_ref=arm_ref,
    ))
    # Brushed AntiqueBrass lower bicep cuff band
    objs.append(make_cyl(
        f"BicepBrassBand_{tag}",
        (sh.x, 0.0, el.z + 0.056), (sh.x, 0.0, el.z + 0.072),
        0.074, 0.076, mats["AntiqueBrass"], segs=18, bevel_w=0.0014,
    ))

    objs.append(make_cyl(
        f"BicepElbowCuff_{tag}",
        (sh.x, 0.0, el.z + 0.030), (sh.x, 0.0, el.z + 0.068),
        0.062, 0.070, mats["DarkArmor"], segs=18, bevel_w=0.0018,
    ))

    return objs


# =============================================================================
# 6. FOREARM_L/R — ROTARY ELBOW, ATHLETIC GAUNTLET & HEROIC 5-FINGER CYBER-HAND
# =============================================================================

def build_forearm_part(sgn, tag, mats):
    objs = []
    el = PIVOTS[f"Forearm{tag}"]  # (±0.285, 0, 1.375)
    wri = Vector((el.x, el.y, 1.095))

    objs.append(make_sphere(f"ElbowBall_{tag}", el, (0.054, 0.054, 0.054), mats["DarkArmor"], segs=16, rings=10))
    objs.extend(make_rotary_joint(
        f"ElbowRotaryOut_{tag}", el + Vector((sgn * 0.054, 0.003, 0.0)), (sgn, 0.0, 0.0),
        radius=0.033, depth=0.018, mats=mats, brass_ring=True, segs=18,
    ))
    objs.extend(make_rotary_joint(
        f"ElbowRotaryIn_{tag}", el + Vector((-sgn * 0.050, 0.003, 0.0)), (-sgn, 0.0, 0.0),
        radius=0.026, depth=0.014, mats=mats, brass_ring=True, segs=16,
    ))

    fa_ref = (el.x, el.y, -999.0)
    ec_g = []
    for ir in range(4):
        tv = ir / 3.0
        zc = el.z - 0.052 + tv * 0.098
        row = []
        for ic in range(5):
            tu = ic / 4.0
            ang = math.pi * 0.5 + (tu - 0.5) * math.radians(118.0)
            row.append(Vector((el.x + math.cos(ang) * 0.062, el.y + math.sin(ang) * 0.068, zc)))
        ec_g.append(row)
    objs.append(make_loft(f"ElbowGuard_{tag}", ec_g, mats["DarkArmor"], closed_u=False, solidify_t=0.010, solidify_off=1.0, bevel_w=0.0018, outward_ref=fa_ref))

    fa_rings = []
    for ir in range(10):
        t = ir / 9.0
        zc = el.z - t * (el.z - wri.z)
        bulge = 0.012 * math.sin(math.pi * (t ** 0.68))
        rx = 0.058 - 0.017 * t + bulge
        ry = 0.061 - 0.017 * t + bulge
        fa_rings.append([Vector((el.x + math.cos(2 * math.pi * ic / 18) * rx, el.y + math.sin(2 * math.pi * ic / 18) * ry, zc)) for ic in range(18)])
    objs.append(make_loft(f"ForearmDarkChassis_{tag}", fa_rings, mats["DarkArmor"], closed_u=True, cap_start=True, cap_end=True))

    # Extended 298-deg wrap sculpted Pearl-Ivory Forearm Gauntlet (ZERO bare black inner forearm when raised!)
    gv_g = []
    for ir in range(6):
        tv = ir / 5.0
        t_arm = 0.12 + tv * 0.82
        zc = el.z - t_arm * (el.z - wri.z)
        bulge = 0.012 * math.sin(math.pi * (t_arm ** 0.68))
        rx = 0.060 - 0.016 * t_arm + bulge
        ry = 0.063 - 0.016 * t_arm + bulge
        span = math.radians(298.0) * (0.88 + 0.12 * math.sin(math.pi * tv))
        row = []
        for ic in range(13):
            tu = ic / 12.0
            ang = (tu - 0.5) * span
            ca, sa = math.cos(ang), math.sin(ang)
            crest = 0.009 * (max(0.0, ca) ** 2.0) * math.sin(math.pi * (0.15 + 0.75 * tv))
            row.append(Vector((el.x + sgn * ca * (rx + crest), el.y + sa * ry, zc)))
        gv_g.append(row)
    objs.append(make_loft(
        f"ForearmIvoryGauntlet_{tag}", gv_g, mats["WhiteCeramic"],
        closed_u=False, solidify_t=0.011, solidify_off=1.0, bevel_w=0.0020, subsurf_pre=1,
        outward_ref=fa_ref,
    ))

    fb_g = []
    for ir in range(5):
        tv = ir / 4.0
        t_arm = 0.18 + tv * 0.68
        zc = el.z - t_arm * (el.z - wri.z)
        rx = 0.072 - 0.016 * t_arm
        row = []
        for ic in range(2):
            ang = (ic - 0.5) * math.radians(17.0)
            row.append(Vector((el.x + sgn * math.cos(ang) * rx, el.y + math.sin(ang) * rx, zc)))
        fb_g.append(row)
    objs.append(make_loft(f"ForearmBrassInlay_{tag}", fb_g, mats["AntiqueBrass"], closed_u=False, solidify_t=0.0055, solidify_off=1.0, outward_ref=fa_ref))

    objs.append(make_cyl(
        f"WristBrassRing_{tag}",
        (el.x, 0.0, wri.z + 0.010), (el.x, 0.0, wri.z + 0.034),
        0.044, 0.050, mats["AntiqueBrass"], segs=18, bevel_w=0.0014,
    ))
    objs.append(make_sphere(f"WristBall_{tag}", wri, (0.040, 0.044, 0.036), mats["DarkArmor"], segs=14, rings=10))

    # Heroic Articulated 5-Finger Cyber-Hand
    palm_rings = [
        [Vector((el.x - sgn * 0.021, -0.041, wri.z - 0.006)), Vector((el.x + sgn * 0.025, -0.039, wri.z - 0.006)),
         Vector((el.x + sgn * 0.025,  0.039, wri.z - 0.006)), Vector((el.x - sgn * 0.021,  0.041, wri.z - 0.006))],
        [Vector((el.x - sgn * 0.023, -0.054, wri.z - 0.060)), Vector((el.x + sgn * 0.027, -0.050, wri.z - 0.060)),
         Vector((el.x + sgn * 0.027,  0.050, wri.z - 0.060)), Vector((el.x - sgn * 0.023,  0.054, wri.z - 0.060))],
        [Vector((el.x - sgn * 0.018, -0.051, wri.z - 0.098)), Vector((el.x + sgn * 0.022, -0.048, wri.z - 0.098)),
         Vector((el.x + sgn * 0.022,  0.048, wri.z - 0.098)), Vector((el.x - sgn * 0.018,  0.051, wri.z - 0.098))],
    ]
    objs.append(make_loft(f"CyberPalm_{tag}", palm_rings, mats["DarkArmor"], closed_u=True, cap_start=True, cap_end=True, bevel_w=0.0048))

    dh_rings = [
        [Vector((el.x + sgn * 0.018, -0.034, wri.z - 0.010)), Vector((el.x + sgn * 0.036, -0.026, wri.z - 0.014)),
         Vector((el.x + sgn * 0.036,  0.026, wri.z - 0.014)), Vector((el.x + sgn * 0.018,  0.034, wri.z - 0.010))],
        [Vector((el.x + sgn * 0.017, -0.046, wri.z - 0.090)), Vector((el.x + sgn * 0.032, -0.038, wri.z - 0.088)),
         Vector((el.x + sgn * 0.032,  0.038, wri.z - 0.088)), Vector((el.x + sgn * 0.017,  0.046, wri.z - 0.090))],
    ]
    objs.append(make_loft(f"HandDorsalIvory_{tag}", dh_rings, mats["WhiteCeramic"], closed_u=True, cap_start=True, cap_end=True, bevel_w=0.0026))

    finger_specs = [
        ("Index",  -0.038, 0.046, 0.035, 0.026, 0.0114),
        ("Middle", -0.013, 0.050, 0.038, 0.028, 0.0118),
        ("Ring",    0.013, 0.047, 0.035, 0.026, 0.0110),
        ("Pinky",   0.037, 0.038, 0.029, 0.022, 0.0096),
    ]
    for fname, fy, l1, l2, l3, rad in finger_specs:
        p0 = Vector((el.x + sgn * 0.003, fy, wri.z - 0.094))
        d1 = Vector((-sgn * 0.16, 0.0, -0.987)).normalized()
        d2 = Vector((-sgn * 0.48, 0.0, -0.877)).normalized()
        d3 = Vector((-sgn * 0.76, 0.0, -0.650)).normalized()
        p1 = p0 + d1 * l1
        p2 = p1 + d2 * l2
        p3 = p2 + d3 * l3
        objs.append(make_sphere(f"Knuckle0_{fname}_{tag}", p0, rad * 1.12, mats["SatinTitanium"], segs=10, rings=8))
        objs.append(make_cyl(f"Phal1_{fname}_{tag}", p0, p1, rad, rad * 0.92, mats["WhiteCeramic"], segs=10, bevel_w=0.0010))
        objs.append(make_sphere(f"Knuckle1_{fname}_{tag}", p1, rad * 0.96, mats["DarkArmor"], segs=10, rings=8))
        objs.append(make_cyl(f"Phal2_{fname}_{tag}", p1, p2, rad * 0.90, rad * 0.80, mats["DarkArmor"], segs=10, bevel_w=0.0008))
        objs.append(make_sphere(f"Knuckle2_{fname}_{tag}", p2, rad * 0.84, mats["SatinTitanium"], segs=8, rings=6))
        objs.append(make_cyl(f"Phal3_{fname}_{tag}", p2, p3, rad * 0.78, rad * 0.56, mats["DarkArmor"], segs=10, bevel_w=0.0008))

    t0 = Vector((el.x - sgn * 0.014, -0.050, wri.z - 0.044))
    t1 = t0 + Vector((-sgn * 0.34, -0.52, -0.78)).normalized() * 0.046
    t2 = t1 + Vector((-sgn * 0.58, -0.26, -0.77)).normalized() * 0.038
    t3 = t2 + Vector((-sgn * 0.74, -0.08, -0.66)).normalized() * 0.026
    objs.append(make_sphere(f"ThumbBase_{tag}", t0, 0.015, mats["DarkArmor"], segs=10, rings=8))
    objs.append(make_cyl(f"Thumb1_{tag}", t0, t1, 0.0138, 0.0124, mats["WhiteCeramic"], segs=10, bevel_w=0.0010))
    objs.append(make_sphere(f"ThumbJ1_{tag}", t1, 0.0126, mats["SatinTitanium"], segs=10, rings=8))
    objs.append(make_cyl(f"Thumb2_{tag}", t1, t2, 0.0118, 0.0104, mats["DarkArmor"], segs=10, bevel_w=0.0008))
    objs.append(make_cyl(f"Thumb3_{tag}", t2, t3, 0.0100, 0.0072, mats["DarkArmor"], segs=10, bevel_w=0.0008))

    return objs


# =============================================================================
# 7. THIGH_L/R — HIGH PARABOLIC HIP-CREST CARAPACE (ZERO EXPOSED HIP BALLS!) + SMOOTH NORMALS
# =============================================================================

def build_thigh_part(sgn, tag, mats):
    objs = []
    hip = PIVOTS[f"Thigh{tag}"]  # (±0.135, 0, 1.110)
    kn  = PIVOTS[f"Shin{tag}"]   # (±0.135, 0, 0.585)

    # Recessed matte CarbonSubframe hip ball (stays cleanly hidden behind sweeping hip/thigh armor!)
    objs.append(make_sphere(f"HipBall_{tag}", hip, (0.076, 0.076, 0.076), mats["CarbonSubframe"], segs=18, rings=12))
    objs.append(make_sphere(f"ThighKneeBall_{tag}", kn, (0.060, 0.060, 0.060), mats["DarkArmor"], segs=16, rings=10))

    def thigh_r(t):
        quad_swell = 0.018 * math.sin(math.pi * (t ** 0.72))
        rx = 0.097 - 0.035 * t + quad_swell
        ry = 0.103 - 0.035 * t + quad_swell
        return rx, ry

    t_rings = []
    for ir in range(14):
        t = ir / 13.0
        zc = hip.z - t * (hip.z - kn.z)
        rx, ry = thigh_r(t)
        t_rings.append([Vector((hip.x + math.cos(2 * math.pi * ic / 22) * rx, hip.y + math.sin(2 * math.pi * ic / 22) * ry, zc)) for ic in range(22)])
    objs.append(make_loft(f"ThighDarkCore_{tag}", t_rings, mats["CarbonSubframe"], closed_u=True, cap_start=True, cap_end=True))

    # High Sweeping Parabolic Pearl-Ivory Thigh Carapace (wraps smoothly up over outer hip crest!)
    tq_g = []
    n_r, n_c = 10, 13
    for ir in range(n_r):
        tv = ir / (n_r - 1)  # 0 = above knee -> 1 = high sweeping hip crest!
        t_leg = 0.86 - tv * 0.88  # reaches slightly above hip pivot on outer crest!
        zc_base = hip.z - t_leg * (hip.z - kn.z)
        rx, ry = thigh_r(max(0.0, t_leg))
        row = []
        for ic in range(n_c):
            tu = ic / (n_c - 1)
            ang_span = math.radians(220.0) * (0.78 + 0.22 * math.sin(math.pi * (0.12 + 0.76 * tv)))
            ang_mid = -math.pi * 0.5 + sgn * math.radians(40.0)
            ang = ang_mid + sgn * (tu - 0.5) * ang_span
            ca, sa = math.cos(ang), math.sin(ang)
            outer_w = max(0.0, sgn * ca)
            front_w = max(0.0, -sa)
            # Sweeps high & smoothly inward over the outer hip socket, curves into a crisp arch above knee!
            top_taper = 1.0 - 0.11 * (tv ** 2.4)
            z_sculpt = 0.044 * (outer_w ** 1.1) * (tv ** 1.2) - 0.045 * (1.0 - outer_w) * (tv ** 1.8) + 0.022 * front_w * (1.0 - tv)
            crest = 0.009 * (front_w ** 1.6) * math.sin(math.pi * tv)
            row.append(Vector((hip.x + ca * ((rx + 0.007 + crest) * top_taper), hip.y + sa * ((ry + 0.007 + crest) * top_taper), zc_base + z_sculpt)))
        tq_g.append(row)
    leg_ref = (hip.x, hip.y, -999.0)
    objs.append(make_loft(
        f"ThighIvoryCarapace_{tag}", tq_g, mats["WhiteCeramic"],
        closed_u=False, solidify_t=0.014, solidify_off=1.0, bevel_w=0.0026, subsurf_pre=1,
        outward_ref=leg_ref,
    ))

    # Brushed AntiqueBrass Inner-Front Thigh Seam Trim
    tb_g = []
    for ir in range(7):
        tv = ir / 6.0
        t_leg = 0.80 - tv * 0.68
        zc = hip.z - t_leg * (hip.z - kn.z)
        rx, ry = thigh_r(t_leg)
        row = []
        for ic in range(2):
            ang = -math.pi * 0.5 - sgn * math.radians(38.0 - ic * 9.0)
            row.append(Vector((hip.x + math.cos(ang) * (rx + 0.013), hip.y + math.sin(ang) * (ry + 0.013), zc)))
        tb_g.append(row)
    objs.append(make_loft(f"ThighBrassSeam_{tag}", tb_g, mats["AntiqueBrass"], closed_u=False, solidify_t=0.0065, solidify_off=1.0, outward_ref=leg_ref))

    objs.extend(make_rotary_joint(
        f"ThighHipPod_{tag}",
        Vector((hip.x + sgn * 0.104, 0.008, hip.z - 0.105)), (sgn, 0.0, 0.0),
        radius=0.036, depth=0.016, mats=mats, brass_ring=True, segs=20,
    ))

    return objs


# =============================================================================
# 8. SHIN_L/R — FACETED HEXAGONAL KNEECAP, ANGULAR V-NOTCHED GREAVE & GROUNDED CYBER-BOOT
# =============================================================================

def build_shin_part(sgn, tag, mats):
    objs = []
    kn = PIVOTS[f"Shin{tag}"]  # (±0.135, 0, 0.585)
    ank = Vector((kn.x, kn.y, 0.102))

    objs.append(make_sphere(f"KneeBall_{tag}", kn, (0.062, 0.062, 0.062), mats["DarkArmor"], segs=18, rings=12))
    objs.extend(make_rotary_joint(
        f"KneeRotaryOut_{tag}", kn + Vector((sgn * 0.066, 0.006, 0.0)), (sgn, 0.0, 0.0),
        radius=0.035, depth=0.020, mats=mats, brass_ring=True, segs=20,
    ))
    objs.extend(make_rotary_joint(
        f"KneeRotaryIn_{tag}", kn + Vector((-sgn * 0.060, 0.006, 0.0)), (-sgn, 0.0, 0.0),
        radius=0.027, depth=0.016, mats=mats, brass_ring=True, segs=16,
    ))

    # Faceted Art-Deco Hexagonal/Shield Pearl-Ivory Patella Kneecap + Brass Contour Border!
    pat_g = []
    for ir in range(5):
        tv = ir / 4.0
        row = []
        for ic in range(7):
            tu = ic / 6.0
            sx = (tu - 0.5) * 2.0
            # Crisp hexagonal shield width: widest across middle, sharply faceted at top & bottom points!
            hex_w = 0.45 + 0.55 * math.sin(math.pi * tv) ** 0.65
            ang = -math.pi * 0.5 + sx * math.radians(66.0) * hex_w
            v_pt = 0.032 * (1.0 - abs(sx)) * (1.0 if tv > 0.5 else -1.0)
            zc = kn.z - 0.068 + tv * 0.136 + v_pt
            rad = 0.074 + 0.016 * (1.0 - abs(sx) * 0.88) * math.sin(math.pi * (0.12 + 0.76 * tv))
            row.append(Vector((kn.x + math.cos(ang) * rad, kn.y + math.sin(ang) * (rad + 0.008), zc)))
        pat_g.append(row)
    shin_ref = (kn.x, kn.y, -999.0)
    objs.append(make_loft(
        f"KneePatellaIvory_{tag}", pat_g, mats["WhiteCeramic"],
        closed_u=False, solidify_t=0.013, solidify_off=1.0, bevel_w=0.0024, subsurf_pre=1,
        outward_ref=shin_ref,
    ))
    pb_kn = [[p + Vector((0.0, -0.004, -0.004)) for p in pat_g[0]], [p + Vector((0.0, -0.002, 0.000)) for p in pat_g[1]]]
    objs.append(make_loft(f"KneeBrassTrim_{tag}", pb_kn, mats["AntiqueBrass"], closed_u=False, solidify_t=0.006, solidify_off=1.0, outward_ref=shin_ref))

    # Sleek Athletic Tapered Shin/Calf Profile (Muscular Upper Calf -> Slender Ankle!)
    def shin_r(t):
        calf_swell = 0.028 * math.sin(math.pi * (t ** 0.52)) * (1.0 - 0.48 * t)
        rx = 0.064 - 0.025 * t + calf_swell * 0.76
        ry = 0.068 - 0.025 * t + calf_swell * 1.22
        y_off = 0.018 * math.sin(math.pi * (t ** 0.55)) * (1.0 - 0.35 * t)
        return rx, ry, y_off

    s_rings = []
    for ir in range(12):
        t = ir / 11.0
        zc = kn.z - t * (kn.z - ank.z)
        rx, ry, y_off = shin_r(t)
        s_rings.append([Vector((kn.x + math.cos(2 * math.pi * ic / 20) * rx, kn.y + y_off + math.sin(2 * math.pi * ic / 20) * ry, zc)) for ic in range(20)])
    objs.append(make_loft(f"ShinDarkCore_{tag}", s_rings, mats["DarkArmor"], closed_u=True, cap_start=True, cap_end=True))

    # Dramatic Tapered Angular V-Notched Pearl-Ivory Shin Greave (ZERO flat cylindrical tin-can top rim!)
    gr_g = []
    n_r, n_c = 9, 13
    for ir in range(n_r):
        tv = ir / (n_r - 1)  # 0=slender ankle -> 1=high angular side-wings + deep front V-notch!
        t_s = 0.94 - tv * 0.84
        zc_base = kn.z - t_s * (kn.z - ank.z)
        rx, ry, y_off = shin_r(t_s)
        row = []
        for ic in range(n_c):
            tu = ic / (n_c - 1)
            sx = (tu - 0.5) * 2.0
            span = math.radians(132.0) * (0.80 + 0.20 * (1.0 - tv * 0.25))
            ang = -math.pi * 0.5 + sx * span
            ca, sa = math.cos(ang), math.sin(ang)
            # Deep dramatic front V-cutout below kneecap while side armor wings sweep cleanly UP beside knee!
            v_notch = -0.105 * ((1.0 - abs(sx)) ** 1.35) * (tv ** 1.45)
            side_wing = 0.022 * (abs(sx) ** 1.5) * (tv ** 1.5)
            # Crisp sharp anterior shin-blade ridge!
            ridge = 0.017 * ((1.0 - abs(sx)) ** 1.8) * (0.40 + 0.60 * tv)
            # Taper inward smoothly at top & bottom edges (zero rain-boot cylinder flare!)
            edge_hug = 1.0 - 0.06 * (tv ** 3.0)
            row.append(Vector((kn.x + ca * ((rx + 0.007) * edge_hug), kn.y + y_off + sa * ((ry + 0.007 + ridge) * edge_hug), zc_base + v_notch + side_wing)))
        gr_g.append(row)
    objs.append(make_loft(
        f"ShinIvoryGreave_{tag}", gr_g, mats["WhiteCeramic"],
        closed_u=False, solidify_t=0.013, solidify_off=1.0, bevel_w=0.0024, subsurf_pre=1,
        outward_ref=shin_ref,
    ))

    # Bold Brushed AntiqueBrass Upper V-Notch Contour Border!
    gb_g = [
        [p + Vector((0.0, -0.003, 0.000)) for p in gr_g[n_r - 2]],
        [p + Vector((0.0, -0.005, 0.004)) for p in gr_g[n_r - 1]],
    ]
    objs.append(make_loft(f"ShinBrassTrim_{tag}", gb_g, mats["AntiqueBrass"], closed_u=False, solidify_t=0.0075, solidify_off=1.0, bevel_w=0.0014, outward_ref=shin_ref))

    # Rear Calf Ivory Stabilizer + Chrome Achilles Hydraulic Damper
    cf_g = []
    for ir in range(5):
        tv = ir / 4.0
        t_s = 0.72 - tv * 0.52
        zc = kn.z - t_s * (kn.z - ank.z)
        rx, ry, y_off = shin_r(t_s)
        row = []
        for ic in range(7):
            tu = ic / 6.0
            ang = math.pi * 0.5 + (tu - 0.5) * math.radians(102.0)
            row.append(Vector((kn.x + math.cos(ang) * (rx + 0.007), kn.y + y_off + math.sin(ang) * (ry + 0.007), zc)))
        cf_g.append(row)
    objs.append(make_loft(f"CalfIvoryPlate_{tag}", cf_g, mats["WhiteCeramic"], closed_u=False, solidify_t=0.011, solidify_off=1.0, bevel_w=0.0020, outward_ref=shin_ref))

    objs.extend(make_piston(
        f"AchillesPiston_{tag}",
        (kn.x, 0.072, 0.068),
        (kn.x, 0.094, 0.345),
        0.0085, 0.0145, mats, segs=12,
    ))

    # --- Grounded Broad Armored Mechanical Cyber-Boot (SOLE STRICTLY AT Z = 0.0000!) ---
    objs.append(make_sphere(f"AnkleBall_{tag}", ank, (0.044, 0.048, 0.044), mats["DarkArmor"], segs=14, rings=10))
    objs.extend(make_rotary_joint(
        f"AnkleRotaryOut_{tag}", Vector((kn.x + sgn * 0.054, 0.004, ank.z)), (sgn, 0.0, 0.0),
        radius=0.025, depth=0.016, mats=mats, brass_ring=True, segs=16,
    ))

    fw = 0.074
    boot_rings = []
    boot_profile = [
        (0.0000,   1.00,   -0.224,   0.088),
        (0.0250,   1.04,   -0.228,   0.092),
        (0.0500,   0.98,   -0.214,   0.086),
        (0.0840,   0.86,   -0.152,   0.076),
        (0.1180,   0.72,   -0.078,   0.062),
    ]
    for zb, xs, yf, yb in boot_profile:
        yc_m = 0.5 * (yf + yb)
        ry_b = 0.5 * (yb - yf)
        ring = []
        for ic in range(22):
            ang = (2.0 * math.pi * ic) / 22
            ct, st = math.cos(ang), math.sin(ang)
            toe_tap = 0.88 if st < -0.15 else 0.96
            ring.append(Vector((kn.x + ct * fw * xs * toe_tap, yc_m + st * ry_b, zb)))
        boot_rings.append(ring)
    objs.append(make_loft(
        f"CyberBootChassis_{tag}", boot_rings, mats["DarkArmor"],
        closed_u=True, cap_start=True, cap_end=True, bevel_w=0.0022,
    ))

    ic_rings = []
    for ir in range(5):
        t = ir / 4.0
        yc = -0.154 + t * 0.148
        zc = 0.058 + t * 0.070
        wi = fw * (0.86 - 0.12 * t)
        ic_rings.append([
            Vector((kn.x - wi,        yc, zc - 0.012)),
            Vector((kn.x - wi * 0.48, yc, zc + 0.017)),
            Vector((kn.x + wi * 0.48, yc, zc + 0.017)),
            Vector((kn.x + wi,        yc, zc - 0.012)),
        ])
    objs.append(make_loft(
        f"BootInstepIvory_{tag}", ic_rings, mats["WhiteCeramic"],
        closed_u=True, cap_start=True, cap_end=True, bevel_w=0.0022,
    ))

    for ti in range(2):
        ty = -0.204 + ti * 0.032
        tz = 0.045 + ti * 0.013
        tw = fw * (0.76 + ti * 0.10)
        tr = [
            [Vector((kn.x - tw, ty - 0.011, tz - 0.008)), Vector((kn.x + tw, ty - 0.011, tz - 0.008)),
             Vector((kn.x + tw * 0.72, ty - 0.011, tz + 0.011)), Vector((kn.x - tw * 0.72, ty - 0.011, tz + 0.011))],
            [Vector((kn.x - tw, ty + 0.011, tz - 0.008)), Vector((kn.x + tw, ty + 0.011, tz - 0.008)),
             Vector((kn.x + tw * 0.72, ty + 0.011, tz + 0.011)), Vector((kn.x - tw * 0.72, ty + 0.011, tz + 0.011))],
        ]
        objs.append(make_loft(f"BootToeRib_{tag}_{ti}", tr, mats["SatinTitanium"], closed_u=True, cap_start=True, cap_end=True, bevel_w=0.0014))

    return objs


# =============================================================================
# 9. GLOBAL 24-RAY BVH AO + CURVATURE + STUDIO REFLECTION BAKE -> LOCAL PIVOT SPLIT
# =============================================================================

def join_part_objects(part_name, sub_objs):
    valid = [o for o in sub_objs if o is not None]
    for o in valid:
        mw = o.matrix_world.copy()
        if mw != Matrix.Identity(4):
            o.data.transform(mw)
            o.matrix_world = Matrix.Identity(4)
            o.data.update()
    bpy.ops.object.select_all(action="DESELECT")
    for o in valid:
        o.select_set(True)
    bpy.context.view_layer.objects.active = valid[0]
    if len(valid) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = part_name
    obj.data.name = f"{part_name}_Mesh"
    obj.matrix_world = Matrix.Identity(4)
    obj.data.validate(verbose=False)
    return set_smooth_and_recalc(obj)


def bake_global_ao_and_uvs(part_objs):
    bm_all = bmesh.new()
    for obj in part_objs.values():
        bm_all.from_mesh(obj.data)
    bmesh.ops.triangulate(bm_all, faces=bm_all.faces)
    bvh = BVHTree.FromBMesh(bm_all)
    bm_all.free()

    n_rays = 24
    golden = math.pi * (3.0 - math.sqrt(5.0))
    local_dirs = []
    for k in range(n_rays):
        zc = (k + 0.5) / n_rays
        r_h = math.sqrt(max(0.0, 1.0 - zc * zc))
        theta = golden * k
        local_dirs.append(Vector((math.cos(theta) * r_h, math.sin(theta) * r_h, zc)).normalized())

    max_dist = 0.24
    for part_name, obj in part_objs.items():
        mesh = obj.data
        verts = mesh.vertices
        v_coords = [v.co.copy() for v in verts]
        neighbors = [[] for _ in range(len(verts))]
        for edge in mesh.edges:
            a, c = edge.vertices[0], edge.vertices[1]
            neighbors[a].append(c)
            neighbors[c].append(a)

        vert_u = [0.55] * len(verts)
        vert_v = [0.50] * len(verts)
        vert_col = [1.0] * len(verts)

        for idx, v in enumerate(verts):
            p = v.co
            n = v.normal.normalized() if v.normal.length > 1e-6 else Vector((0.0, 0.0, 1.0))
            up = Vector((0.0, 0.0, 1.0)) if abs(n.z) < 0.9 else Vector((1.0, 0.0, 0.0))
            t1 = n.cross(up).normalized()
            t2 = n.cross(t1).normalized()

            origin = p + n * 0.0025
            occ = 0.0
            for ld in local_dirs:
                d = (t1 * ld.x + t2 * ld.y + n * ld.z).normalized()
                hit_loc, _, _, hit_dist = bvh.ray_cast(origin, d, max_dist)
                if hit_loc is not None and hit_dist is not None:
                    occ += (1.0 - min(1.0, hit_dist / max_dist)) ** 1.28
            ao = max(0.0, 1.0 - (occ / n_rays) * 1.12)

            curv = 0.0
            if neighbors[idx]:
                acc = 0.0
                for nb in neighbors[idx]:
                    dv = v_coords[nb] - p
                    if dv.length > 1e-6:
                        acc += dv.normalized().dot(n)
                mean_dot = acc / len(neighbors[idx])
                curv = max(-0.18, min(0.26, -mean_dot * 0.62))

            vert_u[idx] = min(0.98, max(0.03, 0.08 + 0.56 * (ao ** 1.24) + curv))
            refl_elev = n.z * 0.68 - n.y * 0.24 + n.x * 0.12
            vert_v[idx] = min(0.98, max(0.02, 0.50 + 0.48 * refl_elev))
            vert_col[idx] = min(1.0, max(0.70, 0.70 + 0.30 * (ao ** 1.15)))

        while mesh.uv_layers:
            mesh.uv_layers.remove(mesh.uv_layers[0])
        uv_layer = mesh.uv_layers.new(name="UVMap")

        while mesh.color_attributes:
            mesh.color_attributes.remove(mesh.color_attributes[0])
        col_attr = mesh.color_attributes.new(name="COLOR_0", type="BYTE_COLOR", domain="CORNER")

        crimson_idx = next((i for i, m in enumerate(mesh.materials) if m and m.name == "CrimsonCore"), -1)
        for poly in mesh.polygons:
            poly.use_smooth = True
            is_core = (poly.material_index == crimson_idx)
            for li in poly.loop_indices:
                vi = mesh.loops[li].vertex_index
                tu = 0.90 if is_core else vert_u[vi]
                tv = 0.85 if is_core else vert_v[vi]
                uv_layer.data[li].uv = (tu, tv)
                c = 1.0 if is_core else vert_col[vi]
                col_attr.data[li].color = (c, c, c, 1.0)
        mesh.update()


# =============================================================================
# 10. STRICT 10-PART CONTRACT & 16-BIT SAFETY VERIFICATION
# =============================================================================

def verify_glb_contract(glb_path):
    raw = glb_path.read_bytes()
    magic, version, length = struct.unpack_from("<III", raw, 0)
    assert magic == 0x46546C67
    chunk_len, chunk_type = struct.unpack_from("<II", raw, 12)
    assert chunk_type == 0x4E4F534A
    gltf = json.loads(raw[20:20 + chunk_len].decode("utf-8"))

    accessors = gltf["accessors"]
    materials = gltf.get("materials", [])
    meshes = gltf.get("meshes", [])
    expected_names = {f"{p}_Mesh" for p in PART_ORDER}
    found_names = {m.get("name", "") for m in meshes}

    print("=== GLB 10-PART CONTRACT & 16-BIT SAFETY AUDIT ===")
    assert len(meshes) == 10, f"Expected 10 meshes, got {len(meshes)}: {found_names}"
    assert found_names == expected_names, f"Mesh name mismatch! expected={expected_names}, found={found_names}"

    total_verts, total_tris, max_prim_verts = 0, 0, 0
    for m in meshes:
        mname = m["name"]
        m_verts, m_tris = 0, 0
        min_y, max_y = 1e9, -1e9
        for prim in m.get("primitives", []):
            pos_acc = accessors[prim["attributes"]["POSITION"]]
            v_cnt = pos_acc["count"]
            i_cnt = accessors[prim["indices"]]["count"] if "indices" in prim else v_cnt
            t_cnt = i_cnt // 3
            mat_idx = prim.get("material", -1)
            mat_name = materials[mat_idx]["name"] if 0 <= mat_idx < len(materials) else f"mat_{mat_idx}"
            if "min" in pos_acc and "max" in pos_acc:
                min_y = min(min_y, pos_acc["min"][1])
                max_y = max(max_y, pos_acc["max"][1])
            m_verts += v_cnt
            m_tris += t_cnt
            total_verts += v_cnt
            total_tris += t_cnt
            max_prim_verts = max(max_prim_verts, v_cnt)
            assert v_cnt < 15000, f"16-BIT LIMIT EXCEEDED on {mname}/{mat_name}: {v_cnt} >= 15000!"
        print(f"  {mname:16s}: verts={m_verts:5d} tris={m_tris:5d} local_Y_glTF=[{min_y:+.4f}, {max_y:+.4f}]")
        if mname in ("ShinL_Mesh", "ShinR_Mesh"):
            assert abs(min_y - (-0.585)) < 0.0025, f"{mname} sole drift! local min_Y={min_y:.4f} (expected -0.5850)"
        if mname == "Head_Mesh":
            top_h = 1.880 + max_y
            assert abs(top_h - TARGET_HEIGHT) < 0.004, f"Head crown height drift! {top_h:.4f} vs {TARGET_HEIGHT}"
    print(f"  TOTAL: 10 meshes, verts={total_verts}, tris={total_tris}, max_prim_verts={max_prim_verts} [ALL < 15,000 OK]")


# =============================================================================
# 11. 3-PANEL STUDIO PROOF RENDER (FRONT NEUTRAL + 3/4 HERO COMBAT POSE + CLOSE-UP)
# =============================================================================

def pose_boss_hierarchy(part_objs, pose):
    def rot_mat(yaw_z=0.0, pitch_x=0.0, roll_y=0.0):
        return (
            Matrix.Rotation(yaw_z, 4, "Z")
            @ Matrix.Rotation(pitch_x, 4, "X")
            @ Matrix.Rotation(roll_y, 4, "Y")
        )

    pelvis_m = Matrix.Translation((0.0, pose.get("pelvis_y", 0.0), pose.get("pelvis_z", 1.135))) @ rot_mat(
        yaw_z=pose.get("pelvis_yaw", 0.0),
        pitch_x=pose.get("pelvis_pitch", 0.0),
        roll_y=pose.get("pelvis_roll", 0.0),
    )
    torso_m = pelvis_m @ rot_mat(
        yaw_z=pose.get("torso_yaw", 0.0),
        pitch_x=pose.get("torso_pitch", 0.0),
        roll_y=pose.get("torso_roll", 0.0),
    )
    head_m = torso_m @ Matrix.Translation((0.0, 0.0, 0.745)) @ rot_mat(
        yaw_z=pose.get("head_yaw", 0.0),
        pitch_x=pose.get("head_pitch", 0.0),
        roll_y=pose.get("head_roll", 0.0),
    )
    uarm_l_m = torso_m @ Matrix.Translation((0.285, 0.0, 0.595)) @ rot_mat(
        yaw_z=pose.get("arm_l_yaw", 0.0),
        pitch_x=pose.get("arm_l_pitch", 0.0),
        roll_y=pose.get("arm_l_roll", 0.0),
    )
    farm_l_m = uarm_l_m @ Matrix.Translation((0.0, 0.0, -0.355)) @ rot_mat(
        yaw_z=pose.get("elb_l_yaw", 0.0),
        pitch_x=pose.get("elb_l_pitch", 0.0),
        roll_y=0.0,
    )
    uarm_r_m = torso_m @ Matrix.Translation((-0.285, 0.0, 0.595)) @ rot_mat(
        yaw_z=pose.get("arm_r_yaw", 0.0),
        pitch_x=pose.get("arm_r_pitch", 0.0),
        roll_y=pose.get("arm_r_roll", 0.0),
    )
    farm_r_m = uarm_r_m @ Matrix.Translation((0.0, 0.0, -0.355)) @ rot_mat(
        yaw_z=pose.get("elb_r_yaw", 0.0),
        pitch_x=pose.get("elb_r_pitch", 0.0),
        roll_y=0.0,
    )
    thigh_l_m = pelvis_m @ Matrix.Translation((0.135, 0.0, -0.025)) @ rot_mat(
        yaw_z=pose.get("thigh_l_yaw", 0.0),
        pitch_x=pose.get("thigh_l_pitch", 0.0),
        roll_y=pose.get("thigh_l_roll", 0.0),
    )
    shin_l_m = thigh_l_m @ Matrix.Translation((0.0, 0.0, -0.525)) @ rot_mat(
        yaw_z=0.0,
        pitch_x=pose.get("shin_l_pitch", 0.0),
        roll_y=-pose.get("thigh_l_roll", 0.0) * 0.45,
    )
    thigh_r_m = pelvis_m @ Matrix.Translation((-0.135, 0.0, -0.025)) @ rot_mat(
        yaw_z=pose.get("thigh_r_yaw", 0.0),
        pitch_x=pose.get("thigh_r_pitch", 0.0),
        roll_y=pose.get("thigh_r_roll", 0.0),
    )
    shin_r_m = thigh_r_m @ Matrix.Translation((0.0, 0.0, -0.525)) @ rot_mat(
        yaw_z=0.0,
        pitch_x=pose.get("shin_r_pitch", 0.0),
        roll_y=-pose.get("thigh_r_roll", 0.0) * 0.45,
    )

    mats_map = {
        "Torso": torso_m, "Head": head_m,
        "UpperArmL": uarm_l_m, "ForearmL": farm_l_m,
        "UpperArmR": uarm_r_m, "ForearmR": farm_r_m,
        "ThighL": thigh_l_m, "ShinL": shin_l_m,
        "ThighR": thigh_r_m, "ShinR": shin_r_m,
    }
    for pname, mat in mats_map.items():
        part_objs[pname].matrix_world = mat


def render_studio_proof(part_objs, out_path):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.image_settings.file_format = "PNG"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 960
    scene.render.resolution_percentage = 100
    vt_items = [i.identifier for i in scene.view_settings.bl_rna.properties["view_transform"].enum_items]
    scene.view_settings.view_transform = "AgX" if "AgX" in vt_items else "Filmic"

    world = bpy.data.worlds.new("StudioWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.042, 0.046, 0.056, 1.0)
        bg.inputs["Strength"].default_value = 0.85

    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=1.45, depth=0.04, location=(0.0, 0.0, -0.02))
    plinth = bpy.context.active_object
    m_plinth = bpy.data.materials.new("StudioPlinth")
    m_plinth.use_nodes = True
    bsdf_p = m_plinth.node_tree.nodes.get("Principled BSDF")
    bsdf_p.inputs["Base Color"].default_value = (0.055, 0.058, 0.068, 1.0)
    bsdf_p.inputs["Roughness"].default_value = 0.34
    bsdf_p.inputs["Metallic"].default_value = 0.22
    plinth.data.materials.append(m_plinth)

    def add_area_light(name, loc, target, energy, color, size):
        ldata = bpy.data.lights.new(name=name, type="AREA")
        ldata.energy = energy
        ldata.color = color
        ldata.size = size
        lobj = bpy.data.objects.new(name, ldata)
        bpy.context.collection.objects.link(lobj)
        lobj.location = Vector(loc)
        d = Vector(target) - lobj.location
        lobj.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        return lobj

    add_area_light("KeyLight",  ( 2.2, -3.1, 2.8), (0.0, 0.0, 1.25), 460.0, (1.00, 0.97, 0.94), 1.9)
    add_area_light("FillLight", (-2.6, -2.3, 1.6), (0.0, 0.0, 1.15), 185.0, (0.94, 0.95, 1.00), 2.3)
    add_area_light("RimLight",  (-0.9,  2.9, 2.7), (0.0, 0.0, 1.35), 540.0, (0.98, 0.92, 0.84), 1.6)
    add_area_light("TopRim",    ( 1.2,  2.1, 3.2), (0.0, 0.0, 1.65), 280.0, (0.90, 0.95, 1.00), 1.4)

    cam_data = bpy.data.cameras.new("StudioCam")
    cam = bpy.data.objects.new("StudioCam", cam_data)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam

    neutral_pose = {
        "pelvis_z": 1.135,
        "arm_l_roll": math.radians(-8.0),
        "arm_r_roll": math.radians( 8.0),
        "elb_l_pitch": math.radians(-10.0),
        "elb_r_pitch": math.radians(-10.0),
    }

    combat_action_pose = {
        "pelvis_z": 1.045,
        "pelvis_roll": math.radians(-3.0),
        "torso_pitch": math.radians(14.0),
        "torso_yaw": math.radians(-22.0),
        "head_yaw": math.radians(32.0),
        "head_pitch": math.radians(-8.0),
        "arm_l_pitch": math.radians(-48.0),
        "arm_l_roll": math.radians(-22.0),
        "elb_l_pitch": math.radians(-52.0),
        "elb_l_yaw": math.radians(-22.0),
        "arm_r_pitch": math.radians(38.0),
        "arm_r_roll": math.radians(28.0),
        "elb_r_pitch": math.radians(-62.0),
        "elb_r_yaw": math.radians(14.0),
        "thigh_l_pitch": math.radians(-24.0),
        "thigh_l_roll": math.radians(-9.0),
        "shin_l_pitch": math.radians(32.0),
        "thigh_r_pitch": math.radians(22.0),
        "thigh_r_roll": math.radians(9.0),
        "shin_r_pitch": math.radians(22.0),
    }

    closeup_pose = {
        "pelvis_z": 1.135,
        "torso_yaw": math.radians(10.0),
        "head_yaw": math.radians(-12.0),
        "head_pitch": math.radians(5.0),
        "arm_l_roll": math.radians(-8.0),
        "arm_r_roll": math.radians( 8.0),
    }

    shots = [
        ("front_neutral", neutral_pose,       (0.00, -3.55, 1.16), (0.00,  0.00, 1.12), 54.0),
        ("combat_34",     combat_action_pose, (1.85, -3.15, 1.18), (0.00, -0.02, 1.08), 54.0),
        ("closeup",       closeup_pose,       (0.42, -1.42, 1.92), (0.00, -0.04, 1.87), 58.0),
    ]

    panels, tmp_files = [], []
    OUT_SHOTS.mkdir(parents=True, exist_ok=True)
    for tag, pose, loc, tgt, lens in shots:
        pose_boss_hierarchy(part_objs, pose)
        bpy.context.view_layer.update()
        cam_data.lens = lens
        cam.location = Vector(loc)
        d = Vector(tgt) - cam.location
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        tmp_p = OUT_SHOTS / f"_tmp_boss_{tag}.png"
        scene.render.filepath = str(tmp_p)
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(str(tmp_p))
        w, h = img.size[0], img.size[1]
        px = np.array(img.pixels[:], dtype=np.float32).reshape((h, w, 4))
        panels.append(px)
        bpy.data.images.remove(img)
        tmp_files.append(tmp_p)

    h, w, _ = panels[0].shape
    bar = np.zeros((h, 4, 4), dtype=np.float32)
    bar[:, :, 3] = 1.0
    bar[:, :, :3] = 0.14
    stitched = np.concatenate([panels[0], bar, panels[1], bar, panels[2]], axis=1)
    sh, sw, _ = stitched.shape

    out_img = bpy.data.images.new("BossStudioProof", width=sw, height=sh, alpha=False)
    out_img.pixels.foreach_set(stitched.ravel())
    out_img.filepath_raw = str(out_path)
    out_img.file_format = "PNG"
    out_img.save()
    bpy.data.images.remove(out_img)

    for tf in tmp_files:
        if tf.exists():
            tf.unlink()
    print("Saved 3-panel Eevee studio proof:", out_path)


# =============================================================================
# 12. MAIN BUILD PIPELINE
# =============================================================================

def build_boss_v8():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = init_materials()

    raw_parts = {
        "Torso":     build_torso_part(mats),
        "Head":      build_head_part(mats),
        "UpperArmL": build_upper_arm_part(+1.0, "L", mats),
        "ForearmL":  build_forearm_part(+1.0, "L", mats),
        "UpperArmR": build_upper_arm_part(-1.0, "R", mats),
        "ForearmR":  build_forearm_part(-1.0, "R", mats),
        "ThighL":    build_thigh_part(+1.0, "L", mats),
        "ShinL":     build_shin_part(+1.0, "L", mats),
        "ThighR":    build_thigh_part(-1.0, "R", mats),
        "ShinR":     build_shin_part(-1.0, "R", mats),
    }

    part_objs = {}
    for pname in PART_ORDER:
        part_objs[pname] = join_part_objects(pname, raw_parts[pname])

    all_zs = [v.co.z for o in part_objs.values() for v in o.data.vertices]
    lo_z, hi_z = min(all_zs), max(all_zs)
    print(f"World-space assembled height BEFORE pivot split: Z=[{lo_z:.4f}, {hi_z:.4f}] (target [0.0000, {TARGET_HEIGHT:.4f}])")
    if abs(lo_z) > 1e-5 or abs((hi_z - lo_z) - TARGET_HEIGHT) > 5e-4:
        sc = TARGET_HEIGHT / max(0.5, hi_z - lo_z)
        for o in part_objs.values():
            for v in o.data.vertices:
                v.co = Vector((v.co.x * sc, v.co.y * sc, (v.co.z - lo_z) * sc))
            o.data.update()

    # 1) Bake 24-ray Global BVHTree AO + Curvature + Studio Horizon Reflection in WORLD SPACE!
    bake_global_ao_and_uvs(part_objs)

    # 2) Translate each part's vertices into its exact canonical local joint pivot space!
    for pname in PART_ORDER:
        obj = part_objs[pname]
        piv = PIVOTS[pname]
        for v in obj.data.vertices:
            v.co -= piv
        obj.data.update()
        obj.location = (0.0, 0.0, 0.0)
        obj.rotation_euler = (0.0, 0.0, 0.0)
        obj.scale = (1.0, 1.0, 1.0)

    # 3) Export ONLY the 10 articulated part mesh objects to escaped_ai_boss.glb!
    bpy.ops.object.select_all(action="DESELECT")
    for pname in PART_ORDER:
        part_objs[pname].select_set(True)
    bpy.context.view_layer.objects.active = part_objs["Torso"]

    OUT_MODELS.mkdir(parents=True, exist_ok=True)
    out_path = OUT_MODELS / "escaped_ai_boss.glb"
    bpy.ops.export_scene.gltf(
        filepath=str(out_path),
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_apply=True,
        export_normals=True,
        export_texcoords=True,
        export_attributes=True,
        export_vertex_color="ACTIVE",
        export_animations=False,
    )
    print("Exported 10-part articulated System Zero v8.0 GLB:", out_path, os.path.getsize(out_path))

    # 4) Verify strict 10-part contract & 16-bit vertex safety!
    verify_glb_contract(out_path)

    # 5) Render 3-panel Eevee studio proof!
    render_studio_proof(part_objs, OUT_SHOTS / "boss_studio_proof.png")


if __name__ == "__main__":
    build_boss_v8()
