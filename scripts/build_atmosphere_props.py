"""Living & Chaotic Office Props Sprint — Architectural Atmosphere Assets Builder (Blender 5.1):
  1. assets/models/prop_pendant_lamp.glb
     - Origin at ceiling mount Y_rl = 0 (Z_bl = 0), hanging down to Y_rl = -1.15m (Z_bl = -1.15m),
       width ~1.10m (X_rl in [-0.55, +0.55]), depth ~0.22m (Z_rl in [-0.11, +0.11]).
     - Warm-brass & matte-charcoal suspended linear + sculpted bell office pendant light fixture
       with warm glowing diffuser strip (#FFE2B0) for FP16 HDR bloom, internal brass anti-glare
       louver baffles, twin telescoping ceiling down-rods, gimbal joints & braided power cord.
     - Proof render: pendant_lamp_proof.png

  2. assets/models/prop_poster_frame.glb
     - Width 0.86m (X_rl in [-0.43, +0.43]), height 1.18m (Y_rl in [-0.59, +0.59]),
       thin 0.03m depth (Z_rl in [0.0, +0.03]), origin at center-back Z_rl = 0 facing +Z in glTF.
     - Sleek dark-anodized aluminum gallery frame with chamfered profile, subtle warm-brass inner
       fillet, recessed archival passe-partout matboard, rear mounting hardware, and a high-res
       vector-rendered 512x700 cream editorial print texture (#F0E8D8 archival paper, bold dark
       Cyrillic print + single crimson accent: «РАБОТАЕМ ЗА ОПЦИОН» / «АО «ЭНТРОПИЯ» · МОСКВА-СИТИ»).
     - Proof render: poster_frame_proof.png
"""
import math
import os
import shutil
import subprocess
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

V = Vector
ROOT = Path(__file__).resolve().parent.parent
OUT_MODELS = ROOT / "assets" / "models"
OUT_MODELS.mkdir(parents=True, exist_ok=True)
ARTIFACT_DIR = Path(r"C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a")
MY_SCRATCH_DIR = Path(r"C:\Users\se-20\.gemini\antigravity\brain\5a288ef4-482c-487e-b05f-e3d0e2267cf7\scratch")
SCREENSHOTS_DIR = ROOT / "screenshots"


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def rl_pos(p):
    """Maps Raylib (X_right, Y_up, Z_front) to Blender (X_right, -Z_front, Y_up) so glTF export has +Y_up and +Z_front."""
    return (p[0], -p[2], p[1])


def rl_scale(s):
    return (s[0], s[2], s[1])


def rl_rot(r):
    return (r[0], -r[2], r[1])


def pack_image(name, rgb_array):
    h, w, _ = rgb_array.shape
    img = bpy.data.images.new(name, width=w, height=h, alpha=False)
    flat = np.flipud(rgb_array).reshape(-1)
    img.pixels.foreach_set(flat.astype(np.float32))
    img.pack()
    return img


def make_mat(name, rgba, metallic=0.15, roughness=0.35, emission_rgb=None, emission_strength=0.0, tex_img=None):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
        if tex_img is not None:
            tex = nodes.new("ShaderNodeTexImage")
            tex.image = tex_img
            links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if emission_rgb is not None and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (*emission_rgb, 1.0)
            bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def tex_matte_charcoal(name="Tex_MatteCharcoal", size=256):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    rng = np.random.default_rng(2026)
    grain = rng.normal(0.0, 0.008, (size, size)).astype(np.float32)
    brush = rng.normal(0.0, 0.006, (1, size)).astype(np.float32)
    img = np.zeros((size, size, 4), dtype=np.float32)
    img[..., 0] = 0.095 + grain + brush
    img[..., 1] = 0.105 + grain + brush
    img[..., 2] = 0.125 + grain + brush
    img[..., 3] = 1.0
    return pack_image(name, np.clip(img, 0.0, 1.0))


def tex_warm_brass(name="Tex_WarmBrass", size=256):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    rng = np.random.default_rng(1998)
    streak = rng.normal(0.0, 0.025, (size, 1)).astype(np.float32)
    streak = np.repeat(streak, size, axis=1)
    shimmer = 0.04 * np.sin(x * math.tau * 2.0)
    img = np.zeros((size, size, 4), dtype=np.float32)
    img[..., 0] = 0.78 + streak + shimmer
    img[..., 1] = 0.58 + streak * 0.8 + shimmer * 0.8
    img[..., 2] = 0.26 + streak * 0.4 + shimmer * 0.4
    img[..., 3] = 1.0
    return pack_image(name, np.clip(img, 0.0, 1.0))


def tex_warm_diffuser(name="Tex_WarmDiffuser", size=256):
    """Warm opal diffuser texture (#FFE2B0 -> sRGB 255, 226, 176) with subtle center filament warmth."""
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    r_center = np.abs(y - 0.5) * 2.0
    glow = 0.92 + 0.08 * (1.0 - r_center ** 1.5)
    img = np.zeros((size, size, 4), dtype=np.float32)
    img[..., 0] = 1.00 * glow
    img[..., 1] = 0.886 * glow
    img[..., 2] = 0.690 * glow
    img[..., 3] = 1.0
    return pack_image(name, np.clip(img, 0.0, 1.0))


def generate_editorial_poster_png(png_path):
    """Uses Windows .NET System.Drawing (GDI+ AntiAliasGridFit) via a UTF-8-BOM PowerShell script
    to render a razor-sharp 512x700 editorial print poster ('РАБОТАЕМ ЗА ОПЦИОН' / 'АО «ЭНТРОПИЯ» · МОСКВА-СИТИ')
    on warm archival paper #F0E8D8."""
    ps_script = rf"""
Add-Type -AssemblyName System.Drawing
$w = 512
$h = 700
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

# 1. Warm off-white archival paper background (#F0E8D8)
$paperColor = [System.Drawing.Color]::FromArgb(255, 240, 232, 216)
$g.Clear($paperColor)

# Subtle archival paper fiber speckles
$rng = New-Object System.Random(1998)
for ($i = 0; $i -lt 1800; $i++) {{
    $px = $rng.Next(0, $w)
    $py = $rng.Next(0, $h)
    $delta = $rng.Next(-6, 6)
    $r = [Math]::Max(0, [Math]::Min(255, 236 + $delta))
    $gr = [Math]::Max(0, [Math]::Min(255, 227 + $delta))
    $b = [Math]::Max(0, [Math]::Min(255, 210 + $delta))
    $bmp.SetPixel($px, $py, [System.Drawing.Color]::FromArgb(255, $r, $gr, $b))
}}

$inkDark = [System.Drawing.Color]::FromArgb(255, 24, 26, 31)
$inkMuted = [System.Drawing.Color]::FromArgb(255, 88, 82, 74)
$inkCrimson = [System.Drawing.Color]::FromArgb(255, 162, 28, 38)
$darkBrush = New-Object System.Drawing.SolidBrush($inkDark)
$mutedBrush = New-Object System.Drawing.SolidBrush($inkMuted)
$crimsonBrush = New-Object System.Drawing.SolidBrush($inkCrimson)

# 2. Inner archival print border hair-line
$borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(110, 88, 82, 74), 1.5)
$g.DrawRectangle($borderPen, 34, 34, $w - 68, $h - 68)

# 3. Top Archival Kicker: АО «ЭНТРОПИЯ» · МОСКВА-СИТИ
$sfCenter = New-Object System.Drawing.StringFormat
$sfCenter.Alignment = [System.Drawing.StringAlignment]::Center
$sfCenter.LineAlignment = [System.Drawing.StringAlignment]::Center

$fontKicker = New-Object System.Drawing.Font("Georgia", 11.5, [System.Drawing.FontStyle]::Bold)
$g.DrawString("АО «ЭНТРОПИЯ»   ·   МОСКВА-СИТИ", $fontKicker, $mutedBrush, [System.Drawing.RectangleF]::new(40, 56, $w - 80, 28), $sfCenter)

# 4. Editorial Constructivist / Swiss Graphic Centerpiece (Monolithic Skyscraper + Crimson Sun)
$g.FillEllipse($crimsonBrush, 166, 122, 180, 180)

$towerPts = [System.Drawing.Point[]]@(
    [System.Drawing.Point]::new(142, 382),
    [System.Drawing.Point]::new(158, 198),
    [System.Drawing.Point]::new(188, 198),
    [System.Drawing.Point]::new(198, 144),
    [System.Drawing.Point]::new(268, 114),
    [System.Drawing.Point]::new(284, 382)
)
$g.FillPolygon($darkBrush, $towerPts)

$winPen = New-Object System.Drawing.Pen($paperColor, 2.2)
for ($wx = 208; $wx -le 262; $wx += 11) {{
    $topY = 148 - [int](($wx - 200) * 0.42)
    $g.DrawLine($winPen, $wx, $topY, $wx, 372)
}}
for ($wx = 166; $wx -le 188; $wx += 11) {{
    $g.DrawLine($winPen, $wx, 208, $wx, 372)
}}

$wingPts = [System.Drawing.Point[]]@(
    [System.Drawing.Point]::new(296, 382),
    [System.Drawing.Point]::new(296, 224),
    [System.Drawing.Point]::new(358, 242),
    [System.Drawing.Point]::new(368, 382)
)
$g.FillPolygon($darkBrush, $wingPts)
for ($wx = 310; $wx -le 350; $wx += 13) {{
    $g.DrawLine($winPen, $wx, 248, $wx, 372)
}}

$basePen = New-Object System.Drawing.Pen($inkDark, 3.0)
$g.DrawLine($basePen, 84, 382, 428, 382)

# 5. Bold Editorial Cyrillic Headline: «РАБОТАЕМ» (Dark Ink) / «ЗА ОПЦИОН» (Single Crimson Accent)
$fontMain = New-Object System.Drawing.Font("Arial", 44, [System.Drawing.FontStyle]::Bold)
$g.DrawString("РАБОТАЕМ", $fontMain, $darkBrush, [System.Drawing.RectangleF]::new(36, 424, $w - 72, 68), $sfCenter)
$g.DrawString("ЗА ОПЦИОН", $fontMain, $crimsonBrush, [System.Drawing.RectangleF]::new(36, 488, $w - 72, 68), $sfCenter)

# 6. Archival Editorial Subline & Stamp at Bottom
$fontSub = New-Object System.Drawing.Font("Georgia", 11.0, [System.Drawing.FontStyle]::Italic)
$g.DrawString("Выплата после релиза ядра. Перерыв — по согласованию с HR.", $fontSub, $mutedBrush, [System.Drawing.RectangleF]::new(44, 578, $w - 88, 30), $sfCenter)

$fontFolio = New-Object System.Drawing.Font("Consolas", 9.5, [System.Drawing.FontStyle]::Bold)
$g.DrawString("ИНВ. № 1998-01   ·   ОТДЕЛ КАДРОВ И ТАРО   ·   ЭТАЖ 01", $fontFolio, $mutedBrush, [System.Drawing.RectangleF]::new(44, 622, $w - 88, 24), $sfCenter)

$g.Dispose()
$bmp.Save("{str(png_path)}", [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
"""
    png_path.parent.mkdir(parents=True, exist_ok=True)
    ps1_file = png_path.parent / "gen_poster_tex.ps1"
    ps1_file.write_text(ps_script, encoding="utf-8-sig")
    subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(ps1_file)], check=True)


def add_box(name, pos, size, mat, rot=(0, 0, 0), bevel=0.006, segments=2):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=rl_pos(pos), rotation=rl_rot(rot))
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = rl_scale(size)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def add_vcyl(name, pos, radius, height, mat, rot=(0, 0, 0), vertices=24, bevel=0.002):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices, radius=radius, depth=height, location=rl_pos(pos), rotation=rl_rot(rot)
    )
    obj = bpy.context.active_object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def add_rod(name, p0, p1, r0, r1, mat, vertices=16):
    v0 = Vector(rl_pos(p0))
    v1 = Vector(rl_pos(p1))
    d = v1 - v0
    length = d.length
    if length < 1e-5:
        return None
    mid = (v0 + v1) * 0.5
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r0, radius2=r1, depth=length, location=mid)
    obj = bpy.context.active_object
    obj.name = name
    quat = Vector((0, 0, 1)).rotation_difference(d.normalized())
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = quat
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def apply_uvs(obj, scale_uv=1.5):
    mesh = obj.data
    if not mesh.uv_layers:
        mesh.uv_layers.new(name="UVMap")
    uv_layer = mesh.uv_layers.active.data
    verts = mesh.vertices

    if "Poster_Print_Canvas" in obj.name and len(verts) > 0:
        xs = [(obj.matrix_world @ v.co).x for v in verts]
        zs = [(obj.matrix_world @ v.co).z for v in verts]
        min_x, max_x = min(xs), max(xs)
        min_z, max_z = min(zs), max(zs)
        sx = max(1e-5, max_x - min_x)
        sz = max(1e-5, max_z - min_z)
        for poly in mesh.polygons:
            for loop_idx in poly.loop_indices:
                vw = obj.matrix_world @ verts[mesh.loops[loop_idx].vertex_index].co
                # Facing +Z in glTF (-Y in Blender): left is min_x (u=0), right is max_x (u=1), bottom is min_z (w=0), top is max_z (w=1)
                u = (vw.x - min_x) / sx
                w = (vw.z - min_z) / sz
                uv_layer[loop_idx].uv = (u, w)
        return

    for poly in mesh.polygons:
        n = poly.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for loop_idx in poly.loop_indices:
            v = obj.matrix_world @ verts[mesh.loops[loop_idx].vertex_index].co
            if az >= ax and az >= ay:
                u, w = v.x * scale_uv, v.y * scale_uv
            elif ax >= ay and ax >= az:
                u, w = v.y * scale_uv, v.z * scale_uv
            else:
                u, w = v.x * scale_uv, v.z * scale_uv
            uv_layer[loop_idx].uv = (u + 0.5, w + 0.5)


def bake_ao_color_0(obj, n_rays=16, max_dist=0.18):
    """Bakes hemisphere BVHTree ambient occlusion into COLOR_0 vertex attribute."""
    mesh = obj.data
    mesh.update()
    verts = mesh.vertices
    tree = BVHTree.FromPolygons([v.co for v in verts], [tuple(f.vertices) for f in mesh.polygons])
    if "COLOR_0" in mesh.color_attributes:
        mesh.color_attributes.remove(mesh.color_attributes["COLOR_0"])
    col_attr = mesh.color_attributes.new(name="COLOR_0", type="BYTE_COLOR", domain="POINT")
    golden_angle = math.pi * (3.0 - math.sqrt(5.0))

    for v in verts:
        n = v.normal.normalized()
        up = V((1, 0, 0)) if abs(n.x) < 0.85 else V((0, 1, 0))
        t1 = n.cross(up).normalized()
        t2 = n.cross(t1).normalized()
        occ = 0.0
        origin = v.co + n * 0.001
        for k in range(n_rays):
            z_h = (k + 0.5) / n_rays
            r_h = math.sqrt(max(0.0, 1.0 - z_h * z_h))
            phi = k * golden_angle
            ray_dir = (n * (0.35 + 0.65 * z_h) + t1 * (r_h * math.cos(phi)) + t2 * (r_h * math.sin(phi))).normalized()
            hit_dist = tree.ray_cast(origin, ray_dir, max_dist)[3]
            if hit_dist is not None:
                occ += (1.0 - hit_dist / max_dist) / n_rays
        shade = max(0.76, 1.0 - 0.28 * occ)
        col_attr.data[v.index].color = (shade, shade, shade, 1.0)


def clamp_to_target_bbox(obj, target_min, target_max):
    verts = [v.co for v in obj.data.vertices]
    cur_min = Vector((min(v.x for v in verts), min(v.y for v in verts), min(v.z for v in verts)))
    cur_max = Vector((max(v.x for v in verts), max(v.y for v in verts), max(v.z for v in verts)))
    t_min = Vector(target_min)
    t_max = Vector(target_max)
    for axis in range(3):
        span_cur = cur_max[axis] - cur_min[axis]
        span_tgt = t_max[axis] - t_min[axis]
        if span_cur > 1e-5 and (abs(cur_min[axis] - t_min[axis]) > 0.001 or abs(cur_max[axis] - t_max[axis]) > 0.001):
            for v in obj.data.vertices:
                norm_t = (v.co[axis] - cur_min[axis]) / span_cur
                v.co[axis] = t_min[axis] + norm_t * span_tgt
    obj.data.update()


def finalize_and_export(filename, target_min=None, target_max=None, uv_scale=1.5):
    mesh_objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for o in mesh_objs:
        apply_uvs(o, scale_uv=uv_scale)
    bpy.ops.object.select_all(action="DESELECT")
    for o in mesh_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objs[0]
    if len(mesh_objs) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.00001)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=0.000005)
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()

    if target_min is not None and target_max is not None:
        clamp_to_target_bbox(obj, target_min, target_max)

    for poly in obj.data.polygons:
        poly.use_smooth = True
    bake_ao_color_0(obj)

    out_path = OUT_MODELS / filename
    bpy.ops.export_scene.gltf(
        filepath=str(out_path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_normals=True,
        export_texcoords=True,
        export_materials="EXPORT",
    )
    for sub in ("bin/Release/net10.0/assets/models", "bin/Debug/net10.0/assets/models"):
        dest = ROOT / sub
        if dest.exists():
            shutil.copy2(out_path, dest / filename)

    verts = [v.co for v in obj.data.vertices]
    bmin = tuple(round(min(v[i] for v in verts), 4) for i in range(3))
    bmax = tuple(round(max(v[i] for v in verts), 4) for i in range(3))
    print(f"[EXPORTED] {filename}: tris={len(obj.data.polygons)}, min={bmin}, max={bmax}, size_bytes={out_path.stat().st_size}")


# ==============================================================================
# 1. PROP: ARCHITECTURAL SUSPENDED PENDANT LAMP (prop_pendant_lamp.glb)
# Origin at ceiling mount Y_rl = 0 (Z_bl = 0), hanging down to Y_rl = -1.15m (Z_bl = -1.15m),
# width = 1.10m (X_rl in [-0.55, +0.55]), depth = 0.22m (Z_rl in [-0.11, +0.11]).
# ==============================================================================
def build_prop_pendant_lamp():
    clear_scene()
    img_charcoal = tex_matte_charcoal("Tex_PendantCharcoal", 256)
    img_brass = tex_warm_brass("Tex_PendantBrass", 256)
    img_diffuser = tex_warm_diffuser("Tex_PendantDiffuser", 256)

    m_charcoal = make_mat("Pendant_MatteCharcoal", (1.0, 1.0, 1.0, 1.0), metallic=0.68, roughness=0.34, tex_img=img_charcoal)
    m_brass = make_mat("Pendant_WarmBrass", (1.0, 1.0, 1.0, 1.0), metallic=0.88, roughness=0.22, tex_img=img_brass)
    m_reflector = make_mat("Pendant_GoldReflector", (0.94, 0.72, 0.34, 1.0), metallic=0.82, roughness=0.18, emission_rgb=(0.95, 0.72, 0.32), emission_strength=0.45)
    # Warm glowing diffuser strip #FFE2B0 (RGB 255, 226, 176) for FP16 HDR Bloom!
    m_diffuser = make_mat(
        "Pendant_WarmDiffuser",
        (1.0, 1.0, 1.0, 1.0),
        metallic=0.05,
        roughness=0.12,
        emission_rgb=(1.0, 0.886, 0.690),
        emission_strength=2.6,
        tex_img=img_diffuser,
    )

    # 1. Ceiling Canopy Junction Box at Y_rl = 0.00..-0.045m (top surface flush at Y_rl = 0.00m!)
    add_box("Ceiling_Brass_MountPlate", (0, -0.006, 0), (0.86, 0.012, 0.13), m_brass, bevel=0.003, segments=1)
    add_box("Ceiling_Charcoal_Canopy", (0, -0.026, 0), (0.82, 0.032, 0.11), m_charcoal, bevel=0.006, segments=2)

    # 2. Twin Telescoping Down-Rods at X_rl = ±0.36m (from Y_rl = -0.04m down to Y_rl = -0.94m)
    for sx in (-1, 1):
        rx = sx * 0.36
        # Ceiling strain-relief brass gland
        add_vcyl(f"Ceiling_Brass_Gland_{sx}", (rx, -0.052, 0), 0.024, 0.026, m_brass, vertices=20, bevel=0.003)
        # Upper matte-charcoal outer suspension tube (Y_rl = -0.06..-0.52m)
        add_rod(f"Upper_DownRod_{sx}", (rx, -0.06, 0), (rx, -0.52, 0), 0.011, 0.011, m_charcoal, vertices=16)
        # Knurled brass mid-height locking clutch collar (Y_rl = -0.52m)
        add_vcyl(f"Mid_Brass_Clutch_{sx}", (rx, -0.52, 0), 0.017, 0.036, m_brass, vertices=20, bevel=0.003)
        # Lower brushed warm-brass stem (Y_rl = -0.53..-0.92m)
        add_rod(f"Lower_Brass_Stem_{sx}", (rx, -0.53, 0), (rx, -0.92, 0), 0.0085, 0.0085, m_brass, vertices=16)
        # Lower articulated brass gimbal / bell cupola on top of the linear luminaire (Y_rl = -0.935m)
        add_vcyl(f"Luminaire_Brass_Cupola_{sx}", (rx, -0.925, 0), 0.028, 0.026, m_brass, vertices=22, bevel=0.004)
        add_vcyl(f"Luminaire_Charcoal_Collar_{sx}", (rx, -0.948, 0), 0.036, 0.022, m_charcoal, vertices=22, bevel=0.003)

    # Continuous 3D spline textile power cable looping from left ceiling gland (-0.30m) into a brass gland on the luminaire spine (-0.18m)
    cable_pts = []
    for k in range(25):
        t = k / 24.0
        cx = -0.30 + 0.12 * t + 0.032 * math.sin(t * math.pi * 2.0)
        cy = -0.025 - 0.950 * t
        cz = 0.024 * math.sin(t * math.pi)
        cable_pts.append(rl_pos((cx, cy, cz)))
    cc = bpy.data.curves.new("Textile_Power_Cord", "CURVE")
    cc.dimensions = "3D"
    cc.bevel_depth = 0.0044
    cc.bevel_resolution = 4
    csp = cc.splines.new("POLY")
    csp.points.add(len(cable_pts) - 1)
    for pt_c, p in zip(csp.points, cable_pts):
        pt_c.co = (*p, 1.0)
    cord_obj = bpy.data.objects.new("Textile_Power_Cord", cc)
    bpy.context.collection.objects.link(cord_obj)
    bpy.context.view_layer.objects.active = cord_obj
    cord_obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    cord_obj.select_set(False)
    cord_obj.data.materials.append(m_charcoal)
    for p in cord_obj.data.polygons:
        p.use_smooth = True
    add_vcyl("Cord_Entry_Brass_Gland", (-0.18, -0.952, 0), 0.014, 0.018, m_brass, vertices=18, bevel=0.002)

    # 3. Suspended Architectural Linear + Bell-Hood Luminaire Body (Y_rl = -0.95..-1.15m, width = 1.10m, depth = 0.22m)
    # Upper extruded charcoal aluminum spine with brass end caps & cooling fins
    add_box("Luminaire_Upper_Spine", (0, -0.978, 0), (1.06, 0.042, 0.115), m_charcoal, bevel=0.008, segments=2)
    for sx in (-1, 1):
        add_box(f"Spine_Brass_EndCap_{sx}", (sx * 0.538, -0.978, 0), (0.024, 0.046, 0.122), m_brass, bevel=0.004, segments=1)

    # Flared architectural trapezoidal/bell shade hood (Y_rl = -0.992..-1.105m)
    n_prof = 8
    hood_sections = [
        (-0.992, 0.515, 0.058),
        (-1.008, 0.528, 0.074),
        (-1.035, 0.540, 0.094),
        (-1.070, 0.548, 0.106),
        (-1.104, 0.550, 0.110),
    ]
    h_verts = []
    h_faces = []
    for y_h, hw, hd in hood_sections:
        c_cham = 0.028
        ring = [
            (-hw + c_cham, y_h,  hd),
            ( hw - c_cham, y_h,  hd),
            ( hw,          y_h,  hd - c_cham),
            ( hw,          y_h, -hd + c_cham),
            ( hw - c_cham, y_h, -hd),
            (-hw + c_cham, y_h, -hd),
            (-hw,          y_h, -hd + c_cham),
            (-hw,          y_h,  hd - c_cham),
        ]
        for pt in ring:
            h_verts.append(rl_pos(pt))
    for ir in range(len(hood_sections) - 1):
        for j in range(n_prof):
            jn = (j + 1) % n_prof
            a = ir * n_prof + j
            b = ir * n_prof + jn
            c = (ir + 1) * n_prof + jn
            d = (ir + 1) * n_prof + j
            h_faces.append((a, b, c, d))
    h_faces.append(tuple(range(n_prof)))
    hm = bpy.data.meshes.new("Luminaire_Bell_Hood")
    hm.from_pydata(h_verts, [], h_faces)
    hobj = bpy.data.objects.new("Luminaire_Bell_Hood", hm)
    bpy.context.collection.objects.link(hobj)
    solid_h = hobj.modifiers.new("Solidify", "SOLIDIFY")
    solid_h.thickness = 0.008
    bev_h = hobj.modifiers.new("Bevel", "BEVEL")
    bev_h.width = 0.0025
    bev_h.segments = 2
    bpy.context.view_layer.objects.active = hobj
    bpy.ops.object.modifier_apply(modifier=solid_h.name)
    bpy.ops.object.modifier_apply(modifier=bev_h.name)
    hobj.data.materials.append(m_charcoal)

    # Brushed Warm-Brass Perimeter Trim Ring around the bottom lip of the Bell Hood (Y_rl = -1.105m)
    for sz in (-1, 1):
        add_box(f"Hood_Bottom_Brass_Rail_Z_{sz}", (0, -1.105, sz * 0.106), (1.08, 0.015, 0.012), m_brass, bevel=0.003, segments=1)
    for sx in (-1, 1):
        add_box(f"Hood_Bottom_Brass_Rail_X_{sx}", (sx * 0.542, -1.105, 0), (0.016, 0.015, 0.212), m_brass, bevel=0.003, segments=1)

    # Internal Anodized Gold Reflector Plate & 6 Cross-Baffle Louver Fins
    add_box("Internal_Gold_Reflector", (0, -1.060, 0), (1.02, 0.008, 0.175), m_reflector, bevel=0.002, segments=1)
    for bi in (-0.42, -0.15, 0.15, 0.42):
        add_box(f"Internal_Brass_Baffle_{bi}", (bi, -1.112, 0), (0.012, 0.044, 0.168), m_brass, bevel=0.002, segments=1)

    # 4. Warm Glowing #FFE2B0 Continuous Linear Opal Diffuser Strip + 3 Sculpted Bell Diffuser Domes (down to Y_rl = -1.15m!)
    # Protrudes cleanly below the brass rim (Y_rl = -1.105..-1.132m) so the full 1.02m warm glowing bar is visible from all angles!
    add_box("Warm_Linear_Diffuser_Core", (0, -1.116, 0), (1.02, 0.036, 0.142), m_diffuser, bevel=0.014, segments=3)
    for di, dx in enumerate((-0.29, 0.0, 0.29)):
        add_vcyl(f"Bell_Diffuser_Brass_Ring_{di}", (dx, -1.128, 0), 0.068, 0.012, m_brass, vertices=22, bevel=0.002)
        add_vcyl(f"Bell_Diffuser_Glow_Capsule_{di}", (dx, -1.139, 0), 0.058, 0.022, m_diffuser, vertices=22, bevel=0.006)

    finalize_and_export(
        "prop_pendant_lamp.glb",
        target_min=(-0.55, -0.11, -1.15),
        target_max=(0.55, 0.11, 0.0),
        uv_scale=1.8,
    )


# ==============================================================================
# 2. PROP: DARK-ALUMINUM FRAMED CORPORATE SATIRE POSTER (prop_poster_frame.glb)
# Width 0.86m (X_rl in [-0.43, +0.43]), height 1.18m (Y_rl in [-0.59, +0.59]),
# depth 0.03m (Z_rl in [0.0, +0.03]), origin at center-back Z_rl = 0 facing +Z in glTF.
# ==============================================================================
def build_prop_poster_frame():
    clear_scene()
    poster_png_path = MY_SCRATCH_DIR / "poster_rabotaem_za_opcion.png"
    generate_editorial_poster_png(poster_png_path)
    img_poster = bpy.data.images.load(str(poster_png_path))
    img_poster.pack()

    img_alu = tex_matte_charcoal("Tex_PosterAluminum", 256)
    img_brass = tex_warm_brass("Tex_PosterBrassFillet", 256)

    m_alu = make_mat("Poster_DarkAluminum", (1.0, 1.0, 1.0, 1.0), metallic=0.82, roughness=0.28, tex_img=img_alu)
    m_brass = make_mat("Poster_BrassFillet", (1.0, 1.0, 1.0, 1.0), metallic=0.88, roughness=0.22, tex_img=img_brass)
    m_matboard = make_mat("Poster_ArchivalMatboard", (0.86, 0.82, 0.74, 1.0), metallic=0.0, roughness=0.78)
    m_backing = make_mat("Poster_Backboard", (0.06, 0.065, 0.075, 1.0), metallic=0.25, roughness=0.55)
    m_print = make_mat("Poster_EditorialPrint", (1.0, 1.0, 1.0, 1.0), metallic=0.02, roughness=0.38, tex_img=img_poster)

    w_full, h_full, d_full = 0.86, 1.18, 0.030
    hw, hh = w_full * 0.5, h_full * 0.5
    frame_w = 0.028

    # 1. Rear Backboard & Wall-Mount Hardware (flush at Z_rl = 0.000..0.008m)
    add_box("Poster_Rear_Backboard", (0, 0, 0.005), (w_full - 0.012, h_full - 0.012, 0.010), m_backing, bevel=0.002, segments=1)

    # 2. Four Chamfered Dark-Anodized Aluminum Outer Gallery Frame Rails (Z_rl = 0.000..0.030m)
    add_box("Frame_Top_Rail", (0, hh - frame_w * 0.5, d_full * 0.5), (w_full, frame_w, d_full), m_alu, bevel=0.0035, segments=2)
    add_box("Frame_Bot_Rail", (0, -hh + frame_w * 0.5, d_full * 0.5), (w_full, frame_w, d_full), m_alu, bevel=0.0035, segments=2)
    add_box("Frame_Left_Rail", (-hw + frame_w * 0.5, 0, d_full * 0.5), (frame_w, h_full - frame_w * 1.6, d_full), m_alu, bevel=0.0035, segments=2)
    add_box("Frame_Right_Rail", (hw - frame_w * 0.5, 0, d_full * 0.5), (frame_w, h_full - frame_w * 1.6, d_full), m_alu, bevel=0.0035, segments=2)

    # Corner brass joinery splines on the outer frame corners
    for sx in (-1, 1):
        for sy in (-1, 1):
            add_box(
                f"Corner_Brass_Spline_{sx}_{sy}",
                (sx * (hw - 0.008), sy * (hh - 0.008), d_full * 0.5),
                (0.018, 0.018, d_full + 0.0006),
                m_brass,
                bevel=0.001,
                segments=1,
            )

    # 3. Recessed Archival Passe-Partout Matboard Bevel Frame (Z_rl = 0.010..0.020m)
    mat_w = 0.034
    in_w = w_full - frame_w * 2.0
    in_h = h_full - frame_w * 2.0
    add_box("PassePartout_Top", (0, in_h * 0.5 - mat_w * 0.5, 0.015), (in_w, mat_w, 0.010), m_matboard, bevel=0.002, segments=1)
    add_box("PassePartout_Bot", (0, -in_h * 0.5 + mat_w * 0.5, 0.015), (in_w, mat_w, 0.010), m_matboard, bevel=0.002, segments=1)
    add_box("PassePartout_Left", (-in_w * 0.5 + mat_w * 0.5, 0, 0.015), (mat_w, in_h - mat_w * 1.8, 0.010), m_matboard, bevel=0.002, segments=1)
    add_box("PassePartout_Right", (in_w * 0.5 - mat_w * 0.5, 0, 0.015), (mat_w, in_h - mat_w * 1.8, 0.010), m_matboard, bevel=0.002, segments=1)

    # 4. Inner Warm-Brass Fillet Trim around the Poster Canvas (Z_rl = 0.017..0.022m)
    canv_w = in_w - mat_w * 2.0  # 0.736m
    canv_h = in_h - mat_w * 2.0  # 1.056m
    fil_t = 0.0045
    add_box("Fillet_Top", (0, canv_h * 0.5 + fil_t * 0.5, 0.0195), (canv_w + fil_t * 2, fil_t, 0.005), m_brass, bevel=0.001, segments=1)
    add_box("Fillet_Bot", (0, -canv_h * 0.5 - fil_t * 0.5, 0.0195), (canv_w + fil_t * 2, fil_t, 0.005), m_brass, bevel=0.001, segments=1)
    add_box("Fillet_Left", (-canv_w * 0.5 - fil_t * 0.5, 0, 0.0195), (fil_t, canv_h, 0.005), m_brass, bevel=0.001, segments=1)
    add_box("Fillet_Right", (canv_w * 0.5 + fil_t * 0.5, 0, 0.0195), (fil_t, canv_h, 0.005), m_brass, bevel=0.001, segments=1)

    # 5. Editorial Print Canvas (Z_rl = 0.012..0.018m, front face at Z_rl = +0.018m facing +Z in glTF)
    add_box("Poster_Print_Canvas", (0, 0, 0.015), (canv_w, canv_h, 0.006), m_print, bevel=0.0005, segments=1)

    finalize_and_export(
        "prop_poster_frame.glb",
        target_min=(-0.43, -0.03, -0.59),
        target_max=(0.43, 0.0, 0.59),
        uv_scale=1.5,
    )


def render_pendant_lamp_proof():
    """Renders a 3-angle studio turntable proof of prop_pendant_lamp.glb to pendant_lamp_proof.png."""
    clear_scene()
    # Remember: prop_pendant_lamp has origin at Z_bl = 0 and hangs down to Z_bl = -1.15m!
    # Lift each imported copy to Z_bl = +1.45m so it hangs above the studio floor (Z_bl = +0.30..+1.45m).
    views = [
        ((-1.25, 0.0, 1.38), (math.radians(-16.0), 0.0, math.radians(-28.0))),  # Left: tilted upward view showing glowing diffuser & brass baffles
        ((0.0, -0.15, 1.48), (0.0, 0.0, 0.0)),                                  # Center: full front elevation showing ceiling mount (Y=0) to diffuser (Y=-1.15m)
        ((1.25, 0.0, 1.42), (math.radians(12.0), 0.0, math.radians(34.0))),     # Right: 3/4 view showing canopy, telescoping rods & brass end-caps
    ]
    for loc, rot_e in views:
        bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / "prop_pendant_lamp.glb"))
        imported = [o for o in bpy.context.selected_objects if o.type == "MESH"]
        for o in imported:
            o.rotation_mode = "XYZ"
            o.rotation_euler = rot_e
            o.location += Vector(loc)

    bpy.ops.mesh.primitive_plane_add(size=18.0, location=(0.0, 0.0, 0.0))
    floor = bpy.context.active_object
    m_floor = make_mat("ProofFloor", (0.065, 0.075, 0.095, 1.0), metallic=0.18, roughness=0.48)
    floor.data.materials.append(m_floor)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1800
    scene.render.resolution_y = 920
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("PendantProofWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.055, 0.068, 0.088, 1.0)
        bg.inputs[1].default_value = 1.1

    cam_data = bpy.data.cameras.new("PendantProofCam")
    cam_data.lens = 34.0
    cam_obj = bpy.data.objects.new("PendantProofCam", cam_data)
    scene.collection.objects.link(cam_obj)
    cam_obj.location = (0.0, -3.55, 0.86)
    cam_obj.rotation_euler = (math.radians(89.5), 0.0, 0.0)
    scene.camera = cam_obj

    for lname, lpos, energy, col in [
        ("KeyLight", (3.2, -3.8, 3.6), 4.4, (1.0, 0.95, 0.86)),
        ("FillLight", (-3.4, -3.0, 2.2), 2.8, (0.76, 0.88, 1.0)),
        ("BottomFill", (0.0, -2.5, 0.15), 2.2, (1.0, 0.90, 0.74)),
    ]:
        ldata = bpy.data.lights.new(name=lname, type="SUN")
        ldata.energy = energy
        ldata.color = col
        lobj = bpy.data.objects.new(name=lname, object_data=ldata)
        scene.collection.objects.link(lobj)
        lobj.location = lpos
        lobj.rotation_euler = (math.radians(52 if "Bottom" not in lname else 115), 0, math.radians(24 if "Key" in lname else -32))

    out_paths = [
        ARTIFACT_DIR / "pendant_lamp_proof.png",
        MY_SCRATCH_DIR / "pendant_lamp_proof.png",
        SCREENSHOTS_DIR / "pendant_lamp_proof.png",
    ]
    out_paths[0].parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(out_paths[0])
    bpy.ops.render.render(write_still=True)
    for dst in out_paths[1:]:
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(out_paths[0], dst)
    print(f"[PENDANT LAMP PROOF RENDERED] {out_paths[0]}")


def render_poster_frame_proof():
    """Renders a 3-angle studio turntable proof of prop_poster_frame.glb to poster_frame_proof.png."""
    clear_scene()
    # prop_poster_frame is centered at Z_bl = 0 (Z_bl in [-0.59, +0.59]), facing -Y_bl (+Z_rl).
    # Lift to Z_bl = 0.72m above the floor.
    views = [
        ((-1.08, 0.0, 0.72), (0.0, 0.0, math.radians(-26.0))),  # Left: 3/4 Front-Left showing frame depth, brass spline & matboard bevel
        ((0.0, -0.22, 0.72), (0.0, 0.0, 0.0)),                  # Center: Direct Front close-up on «РАБОТАЕМ ЗА ОПЦИОН» print
        ((1.08, 0.0, 0.72), (0.0, 0.0, math.radians(28.0))),    # Right: 3/4 Front-Right view
    ]
    for loc, rot_e in views:
        bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / "prop_poster_frame.glb"))
        imported = [o for o in bpy.context.selected_objects if o.type == "MESH"]
        for o in imported:
            o.rotation_mode = "XYZ"
            o.rotation_euler = rot_e
            o.location += Vector(loc)

    bpy.ops.mesh.primitive_plane_add(size=16.0, location=(0.0, 0.0, 0.0))
    floor = bpy.context.active_object
    m_floor = make_mat("PosterProofFloor", (0.07, 0.082, 0.105, 1.0), metallic=0.18, roughness=0.48)
    floor.data.materials.append(m_floor)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1800
    scene.render.resolution_y = 960
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("PosterProofWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.06, 0.072, 0.092, 1.0)
        bg.inputs[1].default_value = 1.15

    cam_data = bpy.data.cameras.new("PosterProofCam")
    cam_data.lens = 36.0
    cam_obj = bpy.data.objects.new("PosterProofCam", cam_data)
    scene.collection.objects.link(cam_obj)
    cam_obj.location = (0.0, -3.15, 0.78)
    cam_obj.rotation_euler = (math.radians(88.5), 0.0, 0.0)
    scene.camera = cam_obj

    for lname, lpos, energy, col in [
        ("KeyLight", (2.8, -3.5, 3.8), 3.8, (1.0, 0.96, 0.90)),
        ("FillLight", (-3.2, -2.8, 2.8), 2.6, (0.80, 0.90, 1.0)),
        ("RimLight", (0.0, 3.2, 3.5), 2.8, (1.0, 0.86, 0.64)),
    ]:
        ldata = bpy.data.lights.new(name=lname, type="SUN")
        ldata.energy = energy
        ldata.color = col
        lobj = bpy.data.objects.new(name=lname, object_data=ldata)
        scene.collection.objects.link(lobj)
        lobj.location = lpos
        lobj.rotation_euler = (math.radians(52), 0, math.radians(22 if "Key" in lname else -28))

    out_paths = [
        ARTIFACT_DIR / "poster_frame_proof.png",
        MY_SCRATCH_DIR / "poster_frame_proof.png",
        SCREENSHOTS_DIR / "poster_frame_proof.png",
    ]
    out_paths[0].parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(out_paths[0])
    bpy.ops.render.render(write_still=True)
    for dst in out_paths[1:]:
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(out_paths[0], dst)
    print(f"[POSTER FRAME PROOF RENDERED] {out_paths[0]}")


if __name__ == "__main__":
    build_prop_pendant_lamp()
    build_prop_poster_frame()
    render_pendant_lamp_proof()
    render_poster_frame_proof()
