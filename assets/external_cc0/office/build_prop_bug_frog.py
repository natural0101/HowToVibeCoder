"""Blender 5.1 generator for `assets/models/prop_bug_frog.glb` (Zero-to-Empire Progression Sprint):
  - Cybernetic Office Frog-Bug jumping on the floor ($1 Green, $2 Cyan, $4 Crimson, $8 Gold variants!)
  - Origin at bottom feet (`Y_rl = 0` in Raylib / `Z_bl = 0` in Blender), facing `+Z` in Raylib (`-Y_bl` in Blender)
  - Compact size (~0.28m tall, ~0.34m wide, ~0.38m long), smooth normals, ~3,800 tris, baked 3D hemisphere BVHTree AO
  - Main body material is a bright light-neutral textured surface (`0.88..0.98` micro-hex scale & dorsal ridge albedo)
    so C# `VisualEngine3D.Asset("prop_bug_frog", ..., tint: speciesColor)` cleanly tints Green, Cyan, Crimson, and Gold variants!
  - Features:
    * Chunky amphibious cyber-frog torso with dorsal ridges, vocal throat pouch, and wide mischievous grin
    * Two big bulging glowing visor-frog eyes with dark pupils & bright emissive iris rings (`emissiveFactor` for HDR Bloom!)
    * Miniature PCB microchip backpack strapped to the back (dark solder-mask board, copper traces, IC chip, gold header pins, twin flexible antennas, and glowing status LED)
    * Spring-loaded bent hind legs (sculpted thigh, calf, ankle joint pistons, and 3-toed webbed hind feet flat on `Z_bl = 0`)
    * Sturdy front legs with 3-toed webbed front pads on `Z_bl = 0`
"""
import math
import os
import shutil
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

V = Vector
ROOT = Path(__file__).resolve().parents[3]
OUT_MODELS = ROOT / "assets/models"
SCREENSHOTS = ROOT / "screenshots"
os.makedirs(OUT_MODELS, exist_ok=True)
os.makedirs(SCREENSHOTS, exist_ok=True)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


# Convert Raylib coordinates (X right, Y up, Z forward) -> Blender coordinates (X right, Y = -Z, Z = Y)
def rl_pos(p):
    return V((p[0], -p[2], p[1]))


def make_packed_image(name, width, height, rgb_array):
    img = bpy.data.images.new(name, width=width, height=height, alpha=False)
    rgba = np.ones((height, width, 4), dtype=np.float32)
    rgba[:, :, :3] = np.clip(rgb_array, 0.0, 1.0)
    img.pixels.foreach_set(rgba.ravel())
    img.pack()
    return img


def make_frog_skin_texture():
    """Generates a 256x256 bright neutral-white (`0.88..0.98`) hexagonal cyber-scale & organic amphibian texture
    so runtime vertex/material tinting (`Green $1`, `Cyan $2`, `Crimson $4`, `Gold $8`) multiplies cleanly!
    """
    h, w = 256, 256
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    u = x / w
    v = y / h

    # Hexagonal cyber-amphibian micro-scale pattern + subtle dorsal spots
    gx = u * 28.0
    gy = v * 28.0
    hex_w = (
        np.cos(gx * math.tau)
        + np.cos((0.5 * gx + 0.866 * gy) * math.tau)
        + np.cos((0.5 * gx - 0.866 * gy) * math.tau)
    ) / 3.0
    spots = np.sin(u * math.tau * 5.0) * np.cos(v * math.tau * 4.0)
    val = 0.93 + 0.035 * hex_w + 0.025 * spots
    val = np.clip(val, 0.86, 0.99)
    rgb = np.stack([val, val * 0.995, val * 0.985], axis=-1)
    return make_packed_image("CyberFrogSkinNeutral", w, h, rgb)


def make_pcb_texture():
    """Generates a 256x256 high-contrast dark charcoal-emerald PCB texture with bright golden-copper circuit traces."""
    h, w = 256, 256
    rgb = np.zeros((h, w, 3), dtype=np.float32)
    # Deep dark PCB substrate
    rgb[:, :, 0] = 0.08
    rgb[:, :, 1] = 0.14
    rgb[:, :, 2] = 0.12

    # Outer copper ground ring
    rgb[8:14, 8:248, :] = (0.85, 0.64, 0.22)
    rgb[242:248, 8:248, :] = (0.85, 0.64, 0.22)
    rgb[8:248, 8:14, :] = (0.85, 0.64, 0.22)
    rgb[8:248, 242:248, :] = (0.85, 0.64, 0.22)

    # Parallel golden-copper bus traces and via pads
    for t_i in range(10):
        ty = 28 + t_i * 20
        rgb[ty:ty + 3, 24:232, :] = (0.88, 0.68, 0.24)
        # Via pads at ends
        rgb[ty - 3:ty + 6, 20:29, :] = (0.95, 0.78, 0.32)
        rgb[ty - 3:ty + 6, 227:236, :] = (0.95, 0.78, 0.32)
    for t_j in range(6):
        tx = 48 + t_j * 32
        rgb[24:232, tx:tx + 3, :] = (0.82, 0.62, 0.22)
    return make_packed_image("FrogPcbTraceTex", w, h, rgb)


def make_mat(name, rgba, metallic=0.1, roughness=0.38, image=None, emission_rgba=None, emission_strength=0.0):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = rgba
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if image is not None:
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = image
        links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if emission_strength > 0.0:
        e_col = emission_rgba if emission_rgba is not None else rgba
        bsdf.inputs["Emission Color"].default_value = e_col
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def apply_uv_and_smooth(obj, scale_uv=3.5):
    mesh = obj.data
    if not mesh.uv_layers:
        mesh.uv_layers.new(name="UVMap")
    uv_layer = mesh.uv_layers.active
    for poly in mesh.polygons:
        poly.use_smooth = True
        n = poly.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for loop_idx in poly.loop_indices:
            v = mesh.vertices[mesh.loops[loop_idx].vertex_index].co
            if az >= ax and az >= ay:
                u, w_uv = v.x * scale_uv + 0.5, v.y * scale_uv + 0.5
            elif ay >= ax:
                u, w_uv = v.x * scale_uv + 0.5, v.z * scale_uv + 0.5
            else:
                u, w_uv = v.y * scale_uv + 0.5, v.z * scale_uv + 0.5
            uv_layer.data[loop_idx].uv = (u, w_uv)
    return obj


def add_limb_curve(name, pts_radii, mat, sides=10, steps_per_span=2):
    """Lofts a smooth Catmull-Rom interpolated tubular limb with consistent Parallel-Transport frames
    through a sequence of `(raylib_pos, radius_x, radius_y)` control points (zero sharp joint kinks!).
    """
    raw = [(rl_pos(p), rx, ry) for p, rx, ry in pts_radii]
    b_pts = []
    n_raw = len(raw)
    steps = steps_per_span if n_raw >= 3 else 1
    for i in range(n_raw - 1):
        p0, rx0, ry0 = raw[max(0, i - 1)]
        p1, rx1, ry1 = raw[i]
        p2, rx2, ry2 = raw[i + 1]
        p3, rx3, ry3 = raw[min(n_raw - 1, i + 2)]
        for s in range(steps):
            t = s / steps
            t2, t3 = t * t, t * t * t
            pt = 0.5 * ((2.0 * p1) + (-p0 + p2) * t + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t2 + (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * t3)
            rx = rx1 * (1.0 - t) + rx2 * t
            ry = ry1 * (1.0 - t) + ry2 * t
            b_pts.append((pt, rx, ry))
    b_pts.append(raw[-1])

    verts = []
    faces = []
    prev_tang = None
    prev_right = None
    for idx, (pt, rx, ry) in enumerate(b_pts):
        if idx == 0:
            tang = (b_pts[1][0] - pt).normalized()
        elif idx == len(b_pts) - 1:
            tang = (pt - b_pts[-2][0]).normalized()
        else:
            tang = (b_pts[idx + 1][0] - b_pts[idx - 1][0]).normalized()
        if prev_right is None:
            up = V((0, 0, 1)) if abs(tang.z) < 0.90 else V((1, 0, 0))
            right = tang.cross(up).normalized()
        else:
            q = prev_tang.rotation_difference(tang)
            right = (q @ prev_right)
            right = (right - tang * right.dot(tang)).normalized()
        ortho = right.cross(tang).normalized()
        prev_tang = tang
        prev_right = right
        for s in range(sides):
            a = s * math.tau / sides
            verts.append(pt + right * (rx * math.cos(a)) + ortho * (ry * math.sin(a)))
    for k in range(len(b_pts) - 1):
        b0 = k * sides
        b1 = (k + 1) * sides
        for s in range(sides):
            sn = (s + 1) % sides
            faces.append((b0 + s, b0 + sn, b1 + sn, b1 + s))
    faces.append(tuple(reversed(range(sides))))
    last = (len(b_pts) - 1) * sides
    faces.append(tuple(range(last, last + sides)))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(mat)
    return apply_uv_and_smooth(obj)


def build_prop_bug_frog():
    clear_scene()
    tex_skin = make_frog_skin_texture()
    tex_pcb = make_pcb_texture()

    # 1) Main tintable neutral body material (`0.88..0.98` albedo so C# tint gives vibrant Green/Cyan/Crimson/Gold!)
    m_skin = make_mat("FrogSkinTintable", (0.96, 0.96, 0.96, 1.0), metallic=0.08, roughness=0.34, image=tex_skin)
    # 2) Lighter throat/belly accent (also neutral-warm ivory so it tints harmoniously with speciesColor!)
    m_belly = make_mat("FrogBellyTintable", (0.99, 0.97, 0.92, 1.0), metallic=0.04, roughness=0.42, image=tex_skin)
    # 3) Dark gunmetal cyber-joints, eye sockets & microchip housing
    m_cyber = make_mat("CyberChassis", (0.14, 0.17, 0.22, 1.0), metallic=0.82, roughness=0.26)
    # 4) PCB circuit board backpack with gold-copper traces
    m_pcb = make_mat("PcbBackpack", (1.0, 1.0, 1.0, 1.0), metallic=0.45, roughness=0.28, image=tex_pcb)
    # 5) Bright gold pins, connectors & antenna tips
    m_gold = make_mat("CyberGoldPins", (0.98, 0.78, 0.24, 1.0), metallic=0.92, roughness=0.18)
    # 6) Bulging glowing cyber-visor frog eyes (catches HDR Bloom on the dark office floor!)
    m_eye_glow = make_mat(
        "FrogEyeGlow",
        (0.98, 0.95, 0.75, 1.0),
        metallic=0.05,
        roughness=0.12,
        emission_rgba=(1.0, 0.92, 0.55, 1.0),
        emission_strength=2.4,
    )
    # 7) Bright glowing status LED beacon on the PCB backpack
    m_led_glow = make_mat(
        "FrogLedBeacon",
        (0.35, 0.98, 0.88, 1.0),
        metallic=0.05,
        roughness=0.10,
        emission_rgba=(0.35, 0.98, 0.88, 1.0),
        emission_strength=2.8,
    )

    # --------------------------------------------------------------------------
    # 1. SCULPTED AMPHIBIAN TORSO & HEAD (facing +Z in Raylib -> -Y in Blender)
    #    Total bounds: X in [-0.17, +0.17] (0.34m wide), Y_rl in [0.0, 0.28] (0.28m tall), Z_rl in [-0.19, +0.19] (0.38m long)
    # --------------------------------------------------------------------------
    # Lofted organic frog torso from rear rump (z_rl = -0.155) to wide grinning snout (z_rl = +0.178)
    torso_Ctrl = [
        # (z_rl,   y_rl,   rx,     ry_up,  ry_down)
        (-0.162, 0.068, 0.018, 0.015, 0.012),  # tailbone tip
        (-0.145, 0.078, 0.068, 0.048, 0.036),  # rear rump
        (-0.095, 0.098, 0.112, 0.072, 0.052),  # wide hind-hip body
        (-0.025, 0.118, 0.124, 0.082, 0.062),  # chunky mid-belly & back arch
        ( 0.045, 0.132, 0.118, 0.076, 0.066),  # chest & shoulder girdle
        ( 0.105, 0.138, 0.106, 0.064, 0.058),  # wide head & puffed vocal throat sac
        ( 0.152, 0.132, 0.078, 0.042, 0.036),  # broad frog snout
        ( 0.176, 0.124, 0.034, 0.018, 0.014),  # snout tip
        ( 0.182, 0.122, 0.004, 0.003, 0.003),  # front nose closure
    ]
    n_circ = 28
    t_verts = []
    for z_rl, y_rl, rx, ry_u, ry_d in torso_Ctrl:
        for i in range(n_circ):
            a = i * math.tau / n_circ
            ca, sa = math.cos(a), math.sin(a)
            x = rx * ca
            ry = ry_u if sa >= 0 else ry_d
            # Subtle amphibian dorsal ridges along top of back (sa > 0.4)
            ridge = 1.0 + (0.06 * math.cos(3.0 * ca * math.pi) if sa > 0.35 and -0.11 < z_rl < 0.09 else 0.0)
            y = y_rl + ry * sa * ridge
            t_verts.append(rl_pos((x, y, z_rl)))
    t_faces = [
        (k * n_circ + i, k * n_circ + (i + 1) % n_circ, (k + 1) * n_circ + (i + 1) % n_circ, (k + 1) * n_circ + i)
        for k in range(len(torso_Ctrl) - 1) for i in range(n_circ)
    ]
    t_faces.extend([tuple(reversed(range(n_circ))), tuple(range(len(t_verts) - n_circ, len(t_verts)))])
    t_mesh = bpy.data.meshes.new("FrogTorso")
    t_mesh.from_pydata(t_verts, [], t_faces)
    t_mesh.update()
    t_obj = bpy.data.objects.new("FrogTorso", t_mesh)
    bpy.context.collection.objects.link(t_obj)
    t_mesh.materials.append(m_skin)   # 0: main dorsal/side skin
    t_mesh.materials.append(m_belly)  # 1: underbelly & vocal sac
    t_mesh.materials.append(m_cyber)  # 2: wide grinning mouth seam
    for k in range(len(torso_Ctrl) - 1):
        z_mid = 0.5 * (torso_Ctrl[k][0] + torso_Ctrl[k + 1][0])
        for i in range(n_circ):
            p_idx = k * n_circ + i
            ang = (i + 0.5) * math.tau / n_circ
            sa = math.sin(ang)
            if z_mid > 0.085 and -0.22 < sa < -0.02:
                t_mesh.polygons[p_idx].material_index = 2  # wide frog grin line
            elif sa < -0.25:
                t_mesh.polygons[p_idx].material_index = 1  # bright belly & throat sac
    apply_uv_and_smooth(t_obj)

    # Puffed vocal sac bubble under the chin (z_rl = 0.095, y_rl = 0.092)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=18, ring_count=12, radius=1.0, location=rl_pos((0.0, 0.094, 0.092)))
    sac = bpy.context.active_object
    sac.scale = (0.076, 0.068, 0.044)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    sac.data.materials.append(m_belly)
    apply_uv_and_smooth(sac)

    # --------------------------------------------------------------------------
    # 2. TWO BIG BULGING GLOWING CYBER-FROG EYES (high on head so clearly visible from player view!)
    # --------------------------------------------------------------------------
    for side in (-1, 1):
        ex = side * 0.068
        ey = 0.198
        ez = 0.095
        # Sculpted amphibian upper brow ridge / turret socket
        bpy.ops.mesh.primitive_uv_sphere_add(segments=18, ring_count=12, radius=1.0, location=rl_pos((ex, ey, ez)))
        socket = bpy.context.active_object
        socket.scale = (0.044, 0.046, 0.044)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        socket.data.materials.append(m_skin)
        apply_uv_and_smooth(socket)

        # Metallic cyber-goggle bezel ring facing forward-outward
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=18, radius=0.036, depth=0.024,
            location=rl_pos((ex + side * 0.008, ey + 0.004, ez + 0.018)),
            rotation=(math.radians(78), 0, math.radians(-side * 18)),
        )
        bezel = bpy.context.active_object
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        bezel.data.materials.append(m_cyber)
        apply_uv_and_smooth(bezel)

        # Bulging glowing iris dome
        bpy.ops.mesh.primitive_uv_sphere_add(
            segments=18, ring_count=12, radius=0.032,
            location=rl_pos((ex + side * 0.011, ey + 0.005, ez + 0.024)),
        )
        eye_dome = bpy.context.active_object
        eye_dome.data.materials.append(m_eye_glow)
        apply_uv_and_smooth(eye_dome)

        # Horizontal amphibian slit pupil (smooth dark ellipsoid nestled cleanly on the front surface of the eye dome)
        bpy.ops.mesh.primitive_uv_sphere_add(
            segments=16, ring_count=10, radius=1.0,
            location=rl_pos((ex + side * 0.014, ey + 0.005, ez + 0.049)),
            rotation=(0, 0, math.radians(-side * 10)),
        )
        pupil = bpy.context.active_object
        pupil.scale = (0.019, 0.008, 0.0085)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        pupil.data.materials.append(m_cyber)
        apply_uv_and_smooth(pupil)

    # --------------------------------------------------------------------------
    # 3. MINIATURE PCB MICROCHIP BACKPACK + DUAL ANTENNAS + GLOWING STATUS BEACON
    #    Total height reaches Y_rl = 0.280m
    # --------------------------------------------------------------------------
    # Snug cyber harness straps hugging the frog's torso (no hanging loop under the belly!)
    for sz, rx_s, ry_s in ((-0.052, 0.118, 0.072), (0.018, 0.122, 0.076)):
        bpy.ops.mesh.primitive_torus_add(
            major_segments=22, minor_segments=8,
            major_radius=1.0, minor_radius=0.075,
            location=rl_pos((0.0, 0.124, sz)),
            rotation=(math.pi / 2, 0, 0),
        )
        strap = bpy.context.active_object
        # After rotation=(pi/2, 0, 0), local X is X_bl (width), local Y is Z_bl (height), local Z is -Y_bl (length)
        strap.scale = (rx_s, ry_s, 0.12)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        strap.data.materials.append(m_cyber)
        apply_uv_and_smooth(strap)

    # Angled PCB circuit board mounted on the dorsal saddle
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=rl_pos((0.0, 0.196, -0.018)), rotation=(math.radians(12), 0, 0))
    pcb = bpy.context.active_object
    pcb.scale = (0.136, 0.148, 0.016)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    mod = pcb.modifiers.new("Bevel", "BEVEL")
    mod.width = 0.004
    mod.segments = 2
    bpy.ops.object.modifier_apply(modifier=mod.name)
    pcb.data.materials.append(m_pcb)
    apply_uv_and_smooth(pcb, scale_uv=5.5)

    # Quad-Flat Package (QFP) CPU microchip in center of PCB + 12 gold pins + glowing status dome
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=rl_pos((0.0, 0.210, -0.015)), rotation=(math.radians(12), 0, 0))
    cpu = bpy.context.active_object
    cpu.scale = (0.068, 0.068, 0.018)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    mod_c = cpu.modifiers.new("Bevel", "BEVEL")
    mod_c.width = 0.003
    mod_c.segments = 2
    bpy.ops.object.modifier_apply(modifier=mod_c.name)
    cpu.data.materials.append(m_cyber)
    apply_uv_and_smooth(cpu)

    for p_i in range(4):
        off = -0.022 + p_i * 0.0145
        for side in (-1, 1):
            bpy.ops.mesh.primitive_cube_add(size=1.0, location=rl_pos((side * 0.042, 0.208, -0.015 + off)))
            pin = bpy.context.active_object
            pin.scale = (0.016, 0.006, 0.005)
            bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
            pin.data.materials.append(m_gold)
            apply_uv_and_smooth(pin)

    # Glowing status LED dome + twin spring antennas reaching Y_rl = 0.280
    bpy.ops.mesh.primitive_uv_sphere_add(segments=18, ring_count=12, radius=0.018, location=rl_pos((0.0, 0.224, -0.012)))
    beacon = bpy.context.active_object
    beacon.data.materials.append(m_led_glow)
    apply_uv_and_smooth(beacon)

    for side in (-1, 1):
        add_limb_curve(
            f"Antenna_{side}",
            [
                ((side * 0.045, 0.205, -0.055), 0.006, 0.006),
                ((side * 0.062, 0.242, -0.082), 0.0045, 0.0045),
                ((side * 0.074, 0.270, -0.105), 0.0035, 0.0035),
            ],
            m_gold,
            sides=10,
        )
        bpy.ops.mesh.primitive_uv_sphere_add(
            segments=12, ring_count=10, radius=0.011,
            location=rl_pos((side * 0.074, 0.270, -0.105)),
        )
        tip = bpy.context.active_object
        tip.data.materials.append(m_eye_glow if side == 1 else m_led_glow)
        apply_uv_and_smooth(tip)

    # --------------------------------------------------------------------------
    # 4. SPRING-LOADED BENT HIND LEGS & FRONT LEGS (Feet planted cleanly on Y_rl = 0.0!)
    # --------------------------------------------------------------------------
    for side in (-1, 1):
        # Hind leg: Hip -> High cocked knee -> Rear ankle -> Forward foot pad
        add_limb_curve(
            f"HindLeg_{side}",
            [
                ((side * 0.095, 0.088, -0.085), 0.042, 0.046),  # muscular hip attachment
                ((side * 0.145, 0.155, -0.040), 0.036, 0.040),  # high folded knee
                ((side * 0.152, 0.110, -0.115), 0.028, 0.032),  # muscular calf
                ((side * 0.138, 0.042, -0.158), 0.020, 0.022),  # spring ankle joint
                ((side * 0.135, 0.016, -0.085), 0.022, 0.016),  # tarsus foot arch
            ],
            m_skin,
            sides=12,
        )
        # Cybernetic knee/ankle servo cap on outer side of hind knee (protruding cleanly outward so zero Z-fighting!)
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=16, radius=0.020, depth=0.014,
            location=rl_pos((side * 0.178, 0.150, -0.042)),
            rotation=(0, math.pi / 2, 0),
        )
        servo = bpy.context.active_object
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        servo.data.materials.append(m_gold)
        apply_uv_and_smooth(servo)

        # 3-toed webbed hind foot flat on Y_rl = 0.0..0.024
        for toe_angle, toe_len in ((-0.32, 0.075), (0.0, 0.088), (0.32, 0.072)):
            tx0 = side * 0.135
            tz0 = -0.085
            tx1 = tx0 + math.sin(toe_angle + side * 0.12) * toe_len
            tz1 = tz0 + math.cos(toe_angle + side * 0.12) * toe_len
            add_limb_curve(
                f"HindToe_{side}_{toe_angle}",
                [
                    ((tx0, 0.012, tz0), 0.014, 0.011),
                    (((tx0 + tx1) * 0.5, 0.010, (tz0 + tz1) * 0.5), 0.012, 0.009),
                    ((tx1, 0.010, tz1), 0.015, 0.009),
                ],
                m_skin,
                sides=7,
                steps_per_span=1,
            )

        # Front leg: Shoulder -> Outward elbow -> Forearm -> Planted front hand at Y_rl = 0.0..0.022
        add_limb_curve(
            f"FrontLeg_{side}",
            [
                ((side * 0.095, 0.105, 0.065), 0.028, 0.030),  # shoulder
                ((side * 0.132, 0.068, 0.082), 0.022, 0.024),  # elbow
                ((side * 0.112, 0.016, 0.115), 0.018, 0.016),  # wrist
            ],
            m_skin,
            sides=10,
            steps_per_span=2,
        )
        for toe_angle, toe_len in ((-0.35, 0.052), (0.0, 0.060), (0.35, 0.050)):
            tx0 = side * 0.112
            tz0 = 0.115
            tx1 = tx0 + math.sin(toe_angle + side * 0.10) * toe_len
            tz1 = tz0 + math.cos(toe_angle + side * 0.10) * toe_len
            add_limb_curve(
                f"FrontToe_{side}_{toe_angle}",
                [
                    ((tx0, 0.010, tz0), 0.011, 0.009),
                    ((tx1, 0.009, tz1), 0.013, 0.008),
                ],
                m_skin,
                sides=7,
                steps_per_span=1,
            )

    # --------------------------------------------------------------------------
    # 5. JOIN, CLAMP FLOOR AT Z_BL >= 0.0, BAKE 3D HEMISPHERE VERTEX AO & EXPORT
    # --------------------------------------------------------------------------
    mesh_objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in mesh_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objs[0]
    if len(mesh_objs) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = obj.data.name = "prop_bug_frog"

    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-6)
    # Shift lowest vertex to exact Z_bl = 0.000
    min_z = min(v.co.z for v in bm.verts)
    for v in bm.verts:
        v.co.z -= min_z
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    for poly in obj.data.polygons:
        poly.use_smooth = True

    # Bake 16-ray hemisphere BVHTree ambient occlusion into COLOR_0 so crevices pop while keeping main body bright (0.82..1.00)
    verts = obj.data.vertices
    tree = BVHTree.FromPolygons([v.co for v in verts], [tuple(p.vertices) for p in obj.data.polygons])
    col_attr = obj.data.color_attributes.new(name="AO", type="BYTE_COLOR", domain="POINT")
    for v in verts:
        n = v.normal.normalized()
        up = V((1, 0, 0)) if abs(n.x) < 0.85 else V((0, 1, 0))
        u_axis = n.cross(up).normalized()
        v_axis = n.cross(u_axis).normalized()
        ao = 0.0
        for j in range(14):
            a = j * 2.39996
            ray = (n * 0.55 + u_axis * math.cos(a) + v_axis * math.sin(a)).normalized()
            hit_dist = tree.ray_cast(v.co + n * 0.001, ray, 0.045)[3]
            if hit_dist is not None:
                ao += (1.0 - hit_dist / 0.045) / 14.0
        # Ground proximity contact shadow near Z_bl = 0
        ground_ao = max(0.0, 1.0 - v.co.z / 0.035) * 0.12
        shade = max(0.76, min(1.0, 1.0 - 0.28 * ao - ground_ao))
        col_attr.data[v.index].color = (shade, shade, shade, 1.0)

    b_verts = [obj.matrix_world @ v.co for v in obj.data.vertices]
    mn = tuple(round(min(v[i] for v in b_verts), 4) for i in range(3))
    mx = tuple(round(max(v[i] for v in b_verts), 4) for i in range(3))
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)

    out_path = OUT_MODELS / "prop_bug_frog.glb"
    bpy.ops.export_scene.gltf(
        filepath=str(out_path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_normals=True,
        export_texcoords=True,
        export_vertex_color="ACTIVE",
        export_materials="EXPORT",
    )
    for sub in ("bin/Release/net10.0/assets/models", "bin/Debug/net10.0/assets/models"):
        d = ROOT / sub
        if d.exists():
            shutil.copy2(out_path, d / "prop_bug_frog.glb")
    print(f"EXPORTED prop_bug_frog.glb | size={out_path.stat().st_size}B | tris={tris} | min_bl={mn} | max_bl={mx}")


def render_bug_frog_proof():
    """Renders a 4-variant showcase (`$1 Green`, `$2 Cyan`, `$4 Crimson`, `$8 Gold`) to `screenshots/bug_frog_proof.png`."""
    clear_scene()
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1800
    scene.render.resolution_y = 620

    world = bpy.data.worlds.new("FrogStudio")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.07, 0.09, 0.13, 1.0)

    # Dark polished slate floor slab under the 4 frogs
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, -0.015))
    floor = bpy.context.active_object
    floor.scale = (2.6, 1.1, 0.03)
    m_floor = make_mat("SlateFloor", (0.09, 0.11, 0.15, 1.0), metallic=0.25, roughness=0.22)
    floor.data.materials.append(m_floor)

    variants = [
        ("Green_$1",   (-0.72, 0.0, 0.0), (0.22, 0.88, 0.34, 1.0), 0.35),
        ("Cyan_$2",    (-0.24, 0.0, 0.0), (0.14, 0.84, 0.98, 1.0), -0.35),
        ("Crimson_$4", ( 0.24, 0.0, 0.0), (0.95, 0.18, 0.32, 1.0), 0.55),
        ("Gold_$8",    ( 0.72, 0.0, 0.0), (0.98, 0.78, 0.18, 1.0), -2.45),  # rear-3/4 view showing PCB backpack!
    ]
    for vname, loc, tint_rgba, rz in variants:
        bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / "prop_bug_frog.glb"))
        for o in bpy.context.selected_objects:
            if o.type == "MESH":
                o.rotation_mode = "XYZ"
                o.rotation_euler = (0.0, 0.0, rz)
                o.location = V(loc)
                # Duplicate tintable materials to preview how Raylib's speciesColor tint looks on the neutral texture
                for slot in o.material_slots:
                    if slot.material and "Tintable" in slot.material.name:
                        m_copy = slot.material.copy()
                        bsdf = m_copy.node_tree.nodes.get("Principled BSDF")
                        for n in list(m_copy.node_tree.nodes):
                            if n.type == "TEX_IMAGE":
                                mix = m_copy.node_tree.nodes.new("ShaderNodeMix")
                                mix.data_type = "RGBA"
                                mix.blend_type = "MULTIPLY"
                                mix.inputs[0].default_value = 1.0
                                mix.inputs[7].default_value = tint_rgba
                                m_copy.node_tree.links.new(n.outputs["Color"], mix.inputs[6])
                                m_copy.node_tree.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
                        slot.material = m_copy

    cam_data = bpy.data.cameras.new("FrogCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 1.95
    cam = bpy.data.objects.new("FrogCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.location = (0.0, -1.55, 0.72)
    target = V((0.0, 0.0, 0.13))
    cam.rotation_mode = "QUATERNION"
    cam.rotation_quaternion = (target - cam.location).to_track_quat("-Z", "Y")

    for loc, energy, col in [
        ((-1.6, -1.8, 1.8), 4.6, (1.0, 0.95, 0.86)),
        (( 1.8, -1.2, 1.5), 3.2, (0.80, 0.90, 1.0)),
        (( 0.0,  1.8, 1.4), 2.8, (1.0, 0.88, 0.72)),
    ]:
        ld = bpy.data.lights.new("Sun", type="SUN")
        ld.energy = energy
        ld.color = col
        lo = bpy.data.objects.new("Sun", ld)
        scene.collection.objects.link(lo)
        lo.location = loc
        lo.rotation_mode = "QUATERNION"
        lo.rotation_quaternion = (target - V(loc)).to_track_quat("-Z", "Y")

    out_png = SCREENSHOTS / "bug_frog_proof.png"
    scene.render.filepath = str(out_png)
    bpy.ops.render.render(write_still=True)
    print("RENDERED FROG PROOF:", out_png)


if __name__ == "__main__":
    build_prop_bug_frog()
    render_bug_frog_proof()
