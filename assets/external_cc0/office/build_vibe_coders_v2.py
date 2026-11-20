"""High-craft stylized 2026 Vibe Coder articulated 3D character builder (Blender 5.1).

Exports clean rigid-joint articulated body-part GLBs (ZERO vertex-shader stretching / bat-wings!)
plus full assembled standing (`char_vibe_coder.glb`) & seated (`char_vibe_coder_seated.glb`) hero models:
  - coder_torso.glb    (pivot: pelvis 0,0,0 -> world Y=0.90)  mats: [blue, navy, skin, ivory, coal, denim]
  - coder_head.glb     (pivot: neck   0,0,0 -> torso +0.54)   mats: [skin, hair, ivory, iris, teal, coal]
  - coder_arm_l.glb    (pivot: L shld 0,0,0 -> torso +0.205,+0.455) mats: [blue, navy, skin]
  - coder_arm_r.glb    (pivot: R shld 0,0,0 -> torso -0.205,+0.455) mats: [blue, navy, skin]
  - coder_thigh_l.glb  (pivot: L hip  0,0,0 -> world +0.105,+0.90)  mats: [denim]
  - coder_thigh_r.glb  (pivot: R hip  0,0,0 -> world -0.105,+0.90)  mats: [denim]
  - coder_shin_l.glb   (pivot: L knee 0,0,0 -> thigh -0.42)         mats: [denim, navy, ivory, sole, coal]
  - coder_shin_r.glb   (pivot: R knee 0,0,0 -> thigh -0.42)         mats: [denim, navy, ivory, sole, coal]
  - char_vibe_coder.glb        (full assembled standing, legacy 10-slot order: skin..coal)
  - char_vibe_coder_seated.glb (full assembled seated typing pose aligned to WorkstationScale=0.92 desk/chair)

Coordinate convention: Blender Z-up, front faces Blender -Y -> exports with export_yup=True to glTF +Z forward, +Y up.
Every GLB bakes 16-ray BVHTree Ambient Occlusion into COLOR_0 wired to Base Color Multiply.
"""
import math
import os
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler
from mathutils.bvhtree import BVHTree

MODELS_DIR = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\assets\models"

# Palette in sRGB (blue & navy are neutral bright so dynamic C# HoodieColor tinting stays vivid & clean!)
PALETTE = {
    "skin":  ((0.88, 0.72, 0.62), 0.56, 0.0, None, 0.0),
    "hair":  ((0.15, 0.12, 0.10), 0.72, 0.0, None, 0.0),
    "blue":  ((0.88, 0.90, 0.94), 0.80, 0.0, None, 0.0),  # dynamically tinted by HoodieColor
    "denim": ((0.16, 0.21, 0.30), 0.84, 0.0, None, 0.0),
    "navy":  ((0.82, 0.85, 0.90), 0.78, 0.0, None, 0.0),  # dynamically tinted dark HoodieColor
    "sole":  ((0.92, 0.91, 0.89), 0.65, 0.0, None, 0.0),
    "ivory": ((0.94, 0.93, 0.90), 0.58, 0.0, None, 0.0),
    "iris":  ((0.10, 0.11, 0.14), 0.18, 0.0, None, 0.0),
    "gold":  ((0.95, 0.76, 0.24), 0.24, 0.85, (0.35, 0.24, 0.04), 0.15),
    "silver":((0.82, 0.86, 0.92), 0.20, 0.92, None, 0.0),
    "wood":  ((0.46, 0.27, 0.14), 0.72, 0.0, None, 0.0),
    "crimson":((0.78, 0.16, 0.20), 0.50, 0.05, None, 0.0),
    "teal":  ((0.78, 0.95, 0.96), 0.14, 0.25, (0.55, 0.92, 0.95), 0.65),  # tinted by GlassesColor
    "coal":  ((0.12, 0.13, 0.16), 0.42, 0.18, None, 0.0),
}


def srgb_to_lin(c):
    return tuple(pow(max(0.0, min(1.0, v)), 2.2) for v in c)


def make_mat(key):
    srgb, rough, met, em, em_str = PALETTE[key]
    m = bpy.data.materials.new(key)
    m.use_nodes = True
    m.use_backface_culling = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    vc = nt.nodes.new("ShaderNodeVertexColor")
    vc.layer_name = "COLOR_0"
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.inputs["Factor"].default_value = 1.0
    mix.inputs["A"].default_value = (*srgb_to_lin(srgb), 1.0)
    nt.links.new(vc.outputs["Color"], mix.inputs["B"])
    nt.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = met
    if em is not None and em_str > 0:
        bsdf.inputs["Emission Color"].default_value = (*srgb_to_lin(em), 1.0)
        bsdf.inputs["Emission Strength"].default_value = em_str
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return m


def add_loft(bm, sections, mat_idx, segs=18, cap_bottom=True, cap_top=True, smooth=True):
    """Build smooth organic lofted volume from list of (center_xyz, rx, ry, rot_x_deg)."""
    rings = []
    for sec in sections:
        c = Vector(sec[0])
        rx, ry = sec[1], sec[2]
        pitch = math.radians(sec[3]) if len(sec) > 3 else 0.0
        rmat = Matrix.Rotation(pitch, 3, 'X')
        ring = []
        for i in range(segs):
            a = 2.0 * math.pi * i / segs
            local = Vector((rx * math.cos(a), ry * math.sin(a), 0.0))
            ring.append(bm.verts.new(c + rmat @ local))
        rings.append(ring)
    for k in range(len(rings) - 1):
        r0, r1 = rings[k], rings[k + 1]
        for i in range(segs):
            j = (i + 1) % segs
            f = bm.faces.new((r0[i], r0[j], r1[j], r1[i]))
            f.material_index = mat_idx
            f.smooth = smooth
    if cap_bottom:
        fb = bm.faces.new(list(reversed(rings[0])))
        fb.material_index = mat_idx
        fb.smooth = smooth
    if cap_top:
        ft = bm.faces.new(rings[-1])
        ft.material_index = mat_idx
        ft.smooth = smooth


def add_sphere(bm, center, radii, mat_idx, u_seg=18, v_seg=12, rot=None, smooth=True):
    ret = bmesh.ops.create_uvsphere(bm, u_segments=u_seg, v_segments=v_seg, radius=1.0)
    verts = ret["verts"]
    mat = Matrix.Diagonal((radii[0], radii[1], radii[2], 1.0))
    if rot is not None:
        mat = rot.to_matrix().to_4x4() @ mat
    mat = Matrix.Translation(center) @ mat
    bmesh.ops.transform(bm, matrix=mat, verts=verts)
    for v in verts:
        for f in v.link_faces:
            f.material_index = mat_idx
            f.smooth = smooth
    return verts


def add_box(bm, center, size, mat_idx, bevel=0.005, segs=2, rot=None, smooth=False):
    ret = bmesh.ops.create_cube(bm, size=1.0)
    verts = ret["verts"]
    bmesh.ops.transform(bm, matrix=Matrix.Diagonal((size[0], size[1], size[2], 1.0)), verts=verts)
    if bevel > 0:
        edges = list({e for v in verts for e in v.link_edges})
        bev = bmesh.ops.bevel(bm, geom=edges, offset=bevel, offset_type='OFFSET',
                              segments=segs, profile=0.5, affect='EDGES', clamp_overlap=True)
        verts = [v for v in ({v for f in bev["faces"] for v in f.verts} | set(verts)) if v.is_valid]
    if rot is not None:
        bmesh.ops.transform(bm, matrix=rot.to_matrix().to_4x4(), verts=verts)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center), verts=verts)
    for v in verts:
        for f in v.link_faces:
            f.material_index = mat_idx
            f.smooth = smooth
    return verts


def add_tube(bm, points, radii, mat_idx, segs=10, cap=True, smooth=True):
    """Tube along 3D polyline with per-point radius (scalar or list)."""
    if isinstance(radii, (int, float)):
        radii = [float(radii)] * len(points)
    rings = []
    for idx, p in enumerate(points):
        p = Vector(p)
        if idx == 0:
            t = (Vector(points[1]) - p).normalized()
        elif idx == len(points) - 1:
            t = (p - Vector(points[idx - 1])).normalized()
        else:
            t = (Vector(points[idx + 1]) - Vector(points[idx - 1])).normalized()
        up = Vector((0, 0, 1)) if abs(t.z) < 0.92 else Vector((1, 0, 0))
        u = t.cross(up).normalized()
        v = t.cross(u).normalized()
        r = radii[idx]
        ring = []
        for s in range(segs):
            a = 2.0 * math.pi * s / segs
            ring.append(bm.verts.new(p + (u * math.cos(a) + v * math.sin(a)) * r))
        rings.append(ring)
    for k in range(len(rings) - 1):
        r0, r1 = rings[k], rings[k + 1]
        for s in range(segs):
            sn = (s + 1) % segs
            f = bm.faces.new((r0[s], r0[sn], r1[sn], r1[s]))
            f.material_index = mat_idx
            f.smooth = smooth
    if cap:
        f0 = bm.faces.new(list(reversed(rings[0])))
        f1 = bm.faces.new(rings[-1])
        f0.material_index = f1.material_index = mat_idx


def add_cyl(bm, center, r1, r2, depth, mat_idx, segs=18, rot=None, smooth=True):
    ret = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs,
                                radius1=r1, radius2=r2, depth=depth)
    verts = ret["verts"]
    mat = Matrix.Identity(4)
    if rot is not None:
        mat = rot.to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ mat, verts=verts)
    for v in verts:
        for f in v.link_faces:
            f.material_index = mat_idx
            f.smooth = smooth
    return verts


def bake_ao_and_export(bm, mat_keys, out_path):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("CoderPartMesh")
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new("CoderPart", mesh)
    bpy.context.collection.objects.link(obj)
    for k in mat_keys:
        mesh.materials.append(make_mat(k))

    # 16-ray BVHTree AO baked into COLOR_0
    bm_ao = bmesh.new()
    bm_ao.from_mesh(mesh)
    bm_ao.verts.ensure_lookup_table()
    bvh = BVHTree.FromBMesh(bm_ao)

    dirs = []
    n_rays = 16
    phi = math.pi * (3.0 - math.sqrt(5.0))
    for i in range(n_rays):
        y = 1.0 - (i / float(n_rays - 1)) * 0.85
        r = math.sqrt(max(0.0, 1.0 - y * y))
        th = phi * i
        dirs.append(Vector((math.cos(th) * r, y, math.sin(th) * r)).normalized())

    vert_ao = []
    for v in bm_ao.verts:
        n = v.normal.normalized() if v.normal.length > 1e-6 else Vector((0, -1, 0))
        up = Vector((0, 0, 1)) if abs(n.z) < 0.9 else Vector((1, 0, 0))
        tx = n.cross(up).normalized()
        ty = n.cross(tx).normalized()
        occ = 0.0
        orig = v.co + n * 0.0025
        for d in dirs:
            ray = (tx * d.x + n * d.y + ty * d.z).normalized()
            hit = bvh.ray_cast(orig, ray, 0.22)
            if hit[0] is not None:
                occ += 1.0 - (hit[3] / 0.22)
        ao = max(0.70, min(1.0, 1.0 - (occ / n_rays) * 0.36))
        vert_ao.append(ao)
    bm_ao.free()

    col_attr = mesh.color_attributes.new(name="COLOR_0", type='BYTE_COLOR', domain='CORNER')
    for poly in mesh.polygons:
        for li in poly.loop_indices:
            ao = vert_ao[mesh.loops[li].vertex_index]
            col_attr.data[li].color = (ao, ao, ao, 1.0)

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(
        filepath=out_path,
        export_format='GLB',
        use_selection=True,
        export_yup=True,
        export_apply=True,
        export_attributes=True,
    )
    print(f"EXPORTED {os.path.basename(out_path)} ({len(mesh.polygons)} polys)")


# =============================================================================
# GEOMETRY BUILDERS (each adds into bm using a mat_map dict: key -> mat_idx)
# =============================================================================

def build_torso_geom(bm, M, xf=Matrix.Identity(4)):
    tmp = bmesh.new()
    # Pelvis seat / relaxed denim hips (snug natural hip width matching thighs at +/-0.088)
    add_loft(tmp, [
        ((0, 0.00, -0.055), 0.140, 0.100),
        ((0, 0.00,  0.005), 0.160, 0.116),
        ((0, 0.00,  0.050), 0.164, 0.120),
    ], M["denim"], segs=20)
    # Ribbed hoodie hem waistband (navy trim)
    add_loft(tmp, [
        ((0, -0.002, 0.036), 0.166, 0.122),
        ((0, -0.003, 0.064), 0.172, 0.128),
        ((0, -0.002, 0.088), 0.174, 0.130),
    ], M["navy"], segs=20)
    # Main relaxed tech hoodie torso with NATURAL sloped human shoulders (NO boxy robot shelf!)
    add_loft(tmp, [
        ((0, -0.002, 0.084), 0.174, 0.130),
        ((0, -0.006, 0.175), 0.178, 0.134),
        ((0, -0.008, 0.285), 0.180, 0.136),
        ((0, -0.004, 0.388), 0.182, 0.130),
        ((0,  0.002, 0.448), 0.158, 0.114),  # natural trapezoid shoulder slope!
        ((0,  0.005, 0.488), 0.118, 0.092),
        ((0,  0.006, 0.512), 0.092, 0.082),
    ], M["blue"], segs=22)
    # Subtle blended shoulder transitions tucked naturally inside sloped shoulders (+/-0.162, z=0.428)
    for s in (-1, 1):
        add_sphere(tmp, (s * 0.156, 0.002, 0.428), (0.058, 0.060, 0.058), M["blue"], 16, 12)
    # Sculpted front kangaroo pocket with tapered side entries
    add_box(tmp, (0, -0.134, 0.170), (0.220, 0.028, 0.128), M["blue"], bevel=0.012, segs=2, smooth=True)
    add_box(tmp, (0, -0.141, 0.108), (0.214, 0.014, 0.014), M["navy"], bevel=0.004, segs=1, smooth=True)
    # Folded hood draped naturally behind the neck & collar rim
    add_loft(tmp, [
        ((0, 0.082, 0.415), 0.124, 0.064, 22),
        ((0, 0.098, 0.472), 0.138, 0.072, 12),
        ((0, 0.072, 0.526), 0.122, 0.068, -10),
        ((0, 0.026, 0.542), 0.098, 0.084, -22),
    ], M["blue"], segs=20)
    # Inner hood lining shadow / collar rim
    add_loft(tmp, [
        ((0, -0.002, 0.490), 0.094, 0.082),
        ((0, -0.004, 0.518), 0.084, 0.076),
    ], M["navy"], segs=18)
    # White crew-neck undershirt peek + neck column
    add_loft(tmp, [
        ((0, -0.004, 0.500), 0.072, 0.070),
        ((0, -0.004, 0.526), 0.064, 0.064),
    ], M["ivory"], segs=16)
    add_loft(tmp, [
        ((0, -0.002, 0.510), 0.054, 0.056),
        ((0, -0.002, 0.578), 0.050, 0.052),
    ], M["skin"], segs=16)
    # Two curved hoodie drawstrings + aglet tips
    for s in (-1, 1):
        add_tube(tmp, [
            (s * 0.042, -0.084, 0.486),
            (s * 0.048, -0.136, 0.432),
            (s * 0.044, -0.145, 0.362),
            (s * 0.040, -0.144, 0.310),
        ], 0.0050, M["ivory"], segs=8)
        add_cyl(tmp, (s * 0.040, -0.144, 0.302), 0.0060, 0.0052, 0.017, M["coal"], segs=8)
    # Modern 2026 startup crossbody tech sling strap cleanly hugging outside chest contour + slim back pouch
    add_tube(tmp, [
        (-0.108, -0.092, 0.482),
        (-0.042, -0.144, 0.395),
        ( 0.045, -0.152, 0.280),
        ( 0.135, -0.125, 0.165),
        ( 0.184, -0.020, 0.115),
        ( 0.175,  0.078, 0.130),
        ( 0.085,  0.146, 0.225),
        (-0.048,  0.148, 0.375),
        (-0.105,  0.088, 0.482),
        (-0.108, -0.092, 0.482),
    ], 0.011, M["coal"], segs=8, cap=False)
    add_box(tmp, (0.015, 0.156, 0.285), (0.168, 0.048, 0.215), M["coal"],
            bevel=0.015, segs=2, rot=Euler((0, math.radians(-18), 0)), smooth=True)

    bmesh.ops.transform(tmp, matrix=xf, verts=tmp.verts)
    _merge_bmesh(tmp, bm)


def build_head_geom(bm, M, xf=Matrix.Identity(4), variant=0):
    """12 hilariously distinct comedy-collectible 2026 Vibe Coder heads (ONLY 2 wear eyewear — ZERO glasses spam!):
      0  (tab/stack):        VIBE-PIRATE — tricorne pirate hat + black eyepatch + bandana + scar smirk (NO glasses!)
      1  (div/smoothie):     CAFFEINE MANIAC — slouchy pom-pom beanie + HUGE wide caffeine eyes + dark sleep rings (NO glasses!)
      2  (weekend/leverage): TINFOIL CRYPTO-BRO — crinkled silver tinfoil cone hat + spiral antenna + paranoid eyes (NO glasses!)
      3  (prompt/framework): 40-FRAMEWORK WIZARD — tall wizard hat with rune band + long beard + bushy brows (NO glasses!)
      4  (call/deadline):    SLEEPY CALL-CENTER LEAD — bedhead + sleep mask on forehead + droopy eyes + travel neck pillow + boom-mic (NO glasses!)
      5  (refactor):         PUNK CYBORG REFACTORER — huge dyed mohawk + headband tails + X-bandage on nose + fierce bare eyes (NO glasses!)
      6  (no-commits):       ZEN MONK HALO — silver man-bun + full silver beard + closed serene eyes + floating GOLDEN HALO (NO glasses!)
      7  (skolkovo):         SKOLKOVO VR SHAMAN — chunky cyberpunk VR visor + dual glowing antennas (eyewear #1)
      8  (meetup):           MEETUP CROWN BOSS — tilted shiny GOLDEN CROWN + smug grin + hypebeast hair (NO glasses!)
      9  (friday):           FRIDAY 19:59 PROD DEMON — huge curved DEVIL HORNS + glowing demon eyes + wicked grin (NO glasses!)
      10 (legacy):           LEGACY 1998 GHOST — Retro CRT Monitor Helmet with glowing terminal face & antenna (NO glasses!)
      11 (sb_chief/pm):      TOWER SECURITY CHIEF — tall peaked officer cap + gold badge + dark aviator shades (eyewear #2)
    """
    tmp = bmesh.new()
    # Neck transition dome
    add_sphere(tmp, (0, 0.0, 0.020), (0.050, 0.050, 0.038), M["skin"], 14, 10)

    if variant == 10:
        # Variant 10 (LEGENDARY): Retro CRT Monitor Helmet!
        add_box(tmp, (0, 0.015, 0.155), (0.255, 0.240, 0.220), M["ivory"], bevel=0.018, segs=2, smooth=True)
        add_box(tmp, (0, 0.140, 0.155), (0.175, 0.095, 0.155), M["ivory"], bevel=0.014, segs=2, smooth=True)
        add_box(tmp, (0, -0.104, 0.160), (0.210, 0.014, 0.168), M["coal"], bevel=0.006, segs=1)
        add_box(tmp, (0, -0.111, 0.162), (0.184, 0.008, 0.142), M["teal"], bevel=0.004, segs=1)
        add_cyl(tmp, (0, 0.005, 0.045), 0.072, 0.070, 0.022, M["hair"], segs=16)
        for s in (-1, 1):
            add_box(tmp, (s * 0.042, -0.116, 0.180), (0.022, 0.005, 0.030), M["iris"], bevel=0.001, segs=1)
        add_box(tmp, (0, -0.116, 0.132), (0.068, 0.005, 0.012), M["iris"], bevel=0.001, segs=1)
        add_cyl(tmp, (0.078, -0.108, 0.068), 0.008, 0.008, 0.008, M["teal"], segs=10, rot=Euler((math.radians(90), 0, 0)))
        add_tube(tmp, [(0.06, 0.04, 0.262), (0.095, 0.05, 0.365)], 0.006, M["coal"], segs=8)
        add_sphere(tmp, (0.095, 0.05, 0.372), (0.014, 0.014, 0.014), M["teal"], 10, 8)
        bmesh.ops.transform(tmp, matrix=xf, verts=tmp.verts)
        _merge_bmesh(tmp, bm)
        return

    # Base stylized sculpted human head
    add_loft(tmp, [
        ((0, -0.018, 0.042), 0.044, 0.056),
        ((0, -0.016, 0.072), 0.078, 0.090),
        ((0, -0.010, 0.118), 0.096, 0.106),
        ((0, -0.005, 0.165), 0.101, 0.112),
        ((0,  0.002, 0.212), 0.096, 0.107),
        ((0,  0.006, 0.250), 0.070, 0.080),
        ((0,  0.006, 0.266), 0.026, 0.030),
    ], M["skin"], segs=24)
    add_box(tmp, (0, -0.113, 0.125), (0.022, 0.026, 0.036), M["skin"],
            bevel=0.008, segs=2, rot=Euler((math.radians(-16), 0, 0)), smooth=True)
    for s in (-1, 1):
        add_sphere(tmp, (s * 0.100, 0.004, 0.132), (0.016, 0.023, 0.031), M["skin"], 12, 10)

    # Expressive bare faces tailored to each archetype!
    brow_tilt = -16 if variant in (0, 5, 9, 11) else (12 if variant in (1, 2, 4) else -4)
    for s in (-1, 1):
        if variant == 6:
            # Zen Monk: peaceful closed curved meditation eyelids!
            add_box(tmp, (s * 0.038, -0.101, 0.146), (0.030, 0.008, 0.006), M["coal"], bevel=0.002, segs=1)
        elif variant == 9:
            # Friday Demon: GLOWING demonic eyes (teal/amber tinted)!
            add_sphere(tmp, (s * 0.038, -0.098, 0.148), (0.019, 0.012, 0.014), M["teal"], 12, 8)
            add_sphere(tmp, (s * 0.038, -0.108, 0.148), (0.006, 0.005, 0.011), M["coal"], 10, 8)
        elif variant in (1, 2):
            # Caffeine Maniac & Tinfoil Crypto-Bro: HUGE wide-eyed stare + tiny pupils!
            add_sphere(tmp, (s * 0.039, -0.097, 0.149), (0.023, 0.014, 0.022), M["ivory"], 14, 10)
            add_sphere(tmp, (s * 0.039, -0.110, 0.149), (0.007, 0.005, 0.007), M["iris"], 10, 8)
            if variant == 1:
                # Dark exhausted under-eye caffeine rings
                add_box(tmp, (s * 0.039, -0.102, 0.128), (0.034, 0.009, 0.008), M["coal"], bevel=0.003, segs=1)
        else:
            add_sphere(tmp, (s * 0.037, -0.096, 0.148), (0.017, 0.010, 0.014), M["ivory"], 12, 8)
            add_sphere(tmp, (s * 0.037, -0.104, 0.148), (0.009, 0.006, 0.010), M["iris"], 10, 8)
            if variant == 4:
                # Sleepy droopy half-closed upper eyelids
                add_box(tmp, (s * 0.037, -0.102, 0.156), (0.034, 0.012, 0.012), M["skin"], bevel=0.003, segs=1)
        brow_mat = M["ivory"] if variant == 6 else M["hair"]
        brow_thick = 0.013 if variant == 3 else 0.009
        add_box(tmp, (s * 0.038, -0.104, 0.174 if variant in (1, 2) else 0.170), (0.036, 0.011, brow_thick), brow_mat,
                bevel=0.003, segs=1, rot=Euler((0, s * math.radians(brow_tilt), 0)), smooth=True)

    # Distinct mouths!
    if variant == 9:
        # Wicked white toothy demon grin
        add_box(tmp, (0, -0.101, 0.088), (0.056, 0.010, 0.015), M["coal"], bevel=0.003, segs=1)
        add_box(tmp, (0, -0.105, 0.090), (0.044, 0.006, 0.008), M["ivory"], bevel=0.002, segs=1)
    elif variant == 1:
        # Jittery wide caffeine teeth grin
        add_box(tmp, (0, -0.101, 0.086), (0.048, 0.010, 0.014), M["ivory"], bevel=0.003, segs=1)
    elif variant in (0, 8):
        # Cocky tilted smirk
        add_box(tmp, (0.006, -0.100, 0.088), (0.038, 0.008, 0.007), M["iris"], bevel=0.002, segs=1, rot=Euler((0, math.radians(10), 0)))
    else:
        add_box(tmp, (0, -0.099, 0.088), (0.034, 0.008, 0.007), M["iris"], bevel=0.002, segs=1, smooth=True)

    if variant == 0:
        # 0: VIBE-PIRATE! Pirate Tricorne Hat + Black Eyepatch + Bandana + Scruffy Beard (NO GLASSES!)
        add_sphere(tmp, (0, 0.030, 0.155), (0.104, 0.096, 0.076), M["hair"], 16, 10)
        # Pirate stubble goatee + mustache
        add_sphere(tmp, (0, -0.078, 0.056), (0.034, 0.028, 0.026), M["hair"], 12, 8)
        add_box(tmp, (0, -0.106, 0.100), (0.054, 0.012, 0.012), M["hair"], bevel=0.004, segs=1)
        # Bright pirate bandana wrapped around forehead with knot tails at back!
        add_loft(tmp, [((0, 0.002, 0.178), 0.106, 0.116), ((0, 0.002, 0.214), 0.108, 0.118)], M["crimson"], segs=22, cap_bottom=False, cap_top=False)
        for s in (-1, 1):
            add_tube(tmp, [(s * 0.016, 0.116, 0.194), (s * 0.048, 0.180, 0.162), (s * 0.070, 0.218, 0.125)], 0.012, M["crimson"], segs=6)
        # BLACK PIRATE EYEPATCH over left eye (+X) with diagonal strap!
        add_cyl(tmp, (0.039, -0.108, 0.149), 0.027, 0.025, 0.012, M["coal"], segs=16, rot=Euler((math.radians(90), 0, 0)))
        add_tube(tmp, [(-0.088, -0.068, 0.188), (0.039, -0.112, 0.152), (0.104, -0.010, 0.122)], 0.005, M["coal"], segs=6)
        # Iconic wide PIRATE TRICORNE HAT (cocked folded brim + gold trim + glowing teal skull emblem!)
        add_sphere(tmp, (0, 0.008, 0.236), (0.112, 0.118, 0.082), M["coal"], 20, 12)
        # Wide sweeping bicorne/tricorne front shield & side wings
        add_box(tmp, (0, -0.080, 0.278), (0.285, 0.026, 0.118), M["coal"], bevel=0.012, segs=2, rot=Euler((math.radians(16), 0, 0)), smooth=True)
        add_box(tmp, (0, -0.092, 0.332), (0.275, 0.014, 0.016), M["gold"], bevel=0.005, segs=1, rot=Euler((math.radians(16), 0, 0)))
        for s in (-1, 1):
            add_box(tmp, (s * 0.122, 0.035, 0.268), (0.026, 0.200, 0.100), M["coal"], bevel=0.010, segs=2, rot=Euler((0, s * math.radians(-14), s * math.radians(-22))), smooth=True)
        # Glowing teal skull/badge on front of pirate hat!
        add_sphere(tmp, (0, -0.098, 0.282), (0.026, 0.014, 0.026), M["teal"], 12, 8)

    elif variant == 1:
        # 1: CAFFEINE / SMOOTHIE MANIAC — slouchy pom-pom beanie + wild caffeine eyes (NO GLASSES!)
        add_sphere(tmp, (0, 0.038, 0.152), (0.100, 0.092, 0.068), M["hair"], 16, 10)
        add_sphere(tmp, (0, -0.076, 0.054), (0.032, 0.026, 0.022), M["hair"], 12, 8)
        beanie_xf = Matrix.Translation((0, 0.020, 0.205)) @ Euler((math.radians(22), 0, 0)).to_matrix().to_4x4()
        b_tmp = bmesh.new()
        add_loft(b_tmp, [
            ((0, 0.000, -0.026), 0.118, 0.126),
            ((0, 0.000,  0.012), 0.122, 0.130),
            ((0, 0.004,  0.032), 0.110, 0.118),
            ((0, 0.016,  0.068), 0.098, 0.106),
            ((0, 0.026,  0.092), 0.064, 0.070),
            ((0, 0.030,  0.104), 0.028, 0.030),
        ], M["coal"], segs=22)
        add_loft(b_tmp, [((0, 0, -0.018), 0.124, 0.132), ((0, 0, 0.006), 0.125, 0.133)], M["teal"], segs=22, cap_bottom=False, cap_top=False)
        add_sphere(b_tmp, (0, 0.032, 0.116), (0.036, 0.036, 0.032), M["teal"], 12, 8)
        bmesh.ops.transform(b_tmp, matrix=beanie_xf, verts=b_tmp.verts)
        _merge_bmesh(b_tmp, tmp)

    elif variant == 2:
        # 2: TINFOIL CRYPTO-BRO — CRINKLED SHINY SILVER TINFOIL CONE HAT + SPIRAL ANTENNA (NO GLASSES!)
        add_sphere(tmp, (0, 0.034, 0.154), (0.102, 0.096, 0.072), M["hair"], 16, 10)
        # Crinkled faceted silver tinfoil brim + tall jagged foil cone (smooth=False for real foil crinkles!)
        add_loft(tmp, [
            ((0, 0.004, 0.188), 0.145, 0.152),
            ((0, 0.004, 0.212), 0.116, 0.124),
            ((0, 0.006, 0.278), 0.092, 0.096),
            ((0, 0.008, 0.352), 0.048, 0.052),
            ((0, 0.008, 0.415), 0.010, 0.010),
        ], M["silver"], segs=11, smooth=False)
        # Folded tinfoil band + coiled antenna with glowing teal signal orb!
        add_loft(tmp, [((0, 0.004, 0.194), 0.128, 0.135), ((0, 0.004, 0.214), 0.122, 0.129)], M["coal"], segs=12, cap_bottom=False, cap_top=False)
        spiral = [(0.022 * math.cos(i * 1.4), 0.008 + 0.022 * math.sin(i * 1.4), 0.412 + i * 0.012) for i in range(9)]
        add_tube(tmp, spiral, 0.005, M["silver"], segs=6)
        add_sphere(tmp, (0, 0.008, 0.515), (0.018, 0.018, 0.018), M["teal"], 10, 8)

    elif variant == 3:
        # 3: 40-FRAMEWORK WIZARD — Tall pointed wizard hat + long beard + bushy brows (NO GLASSES!)
        add_sphere(tmp, (0, 0.032, 0.155), (0.102, 0.094, 0.078), M["hair"], 16, 12)
        add_loft(tmp, [
            ((0, -0.078,  0.095), 0.072, 0.038),
            ((0, -0.088,  0.042), 0.060, 0.036),
            ((0, -0.092, -0.022), 0.044, 0.030),
            ((0, -0.086, -0.078), 0.018, 0.016),
        ], M["hair"], segs=16)
        add_cyl(tmp, (0, 0.004, 0.214), 0.215, 0.205, 0.014, M["coal"], segs=24, rot=Euler((math.radians(8), 0, 0)))
        add_loft(tmp, [
            ((0,  0.004, 0.218), 0.114, 0.120),
            ((0,  0.012, 0.292), 0.090, 0.094),
            ((0,  0.034, 0.375), 0.058, 0.060),
            ((0,  0.072, 0.452), 0.028, 0.030),
            ((0,  0.112, 0.498), 0.008, 0.008),
        ], M["coal"], segs=20)
        add_loft(tmp, [((0, 0.005, 0.222), 0.118, 0.124), ((0, 0.008, 0.252), 0.112, 0.118)], M["teal"], segs=22, cap_bottom=False, cap_top=False)

    elif variant == 4:
        # 4: SLEEPY CALL-CENTER LEAD — wild bedhead + SLEEP MASK on forehead + TRAVEL NECK PILLOW + BOOM-MIC (NO GLASSES!)
        add_sphere(tmp, (0, 0.018, 0.198), (0.106, 0.114, 0.086), M["hair"], 18, 12)
        for pos, rad in [((-0.07, -0.02, 0.268), (0.042, 0.038, 0.038)), ((0.06, 0.01, 0.275), (0.045, 0.040, 0.035)), ((0.0, 0.05, 0.280), (0.048, 0.045, 0.032))]:
            add_sphere(tmp, pos, rad, M["hair"], 10, 8)
        # Puffy teal SLEEP MASK pushed up on forehead with sleeping closed-eye arcs!
        add_box(tmp, (0, -0.106, 0.214), (0.148, 0.024, 0.058), M["teal"], bevel=0.012, segs=2, rot=Euler((math.radians(16), 0, 0)), smooth=True)
        add_loft(tmp, [((0, 0.005, 0.205), 0.106, 0.114), ((0, 0.005, 0.220), 0.106, 0.114)], M["coal"], segs=20, cap_bottom=False, cap_top=False)
        for s in (-1, 1):
            add_box(tmp, (s * 0.036, -0.120, 0.214), (0.028, 0.006, 0.007), M["coal"], bevel=0.002, segs=1, rot=Euler((math.radians(16), 0, 0)))
        # Plush U-shaped airplane TRAVEL NECK PILLOW wrapped around neck!
        pillow_pts = [(0.118 * math.sin(math.radians(-125 + i * 25)), 0.118 * math.cos(math.radians(-125 + i * 25)), 0.022) for i in range(11)]
        add_tube(tmp, pillow_pts, 0.042, M["ivory"], segs=12, cap=True)
        # Call-center boom mic headset!
        add_cyl(tmp, (-0.108, 0.004, 0.136), 0.030, 0.026, 0.024, M["coal"], segs=16, rot=Euler((0, math.radians(90), 0)))
        add_tube(tmp, [(-0.108, -0.008, 0.124), (-0.098, -0.082, 0.098), (-0.032, -0.122, 0.088)], 0.006, M["coal"], segs=8)
        add_sphere(tmp, (-0.024, -0.124, 0.088), (0.013, 0.011, 0.011), M["teal"], 10, 8)

    elif variant == 5:
        # 5: PUNK CYBORG REFACTORER — HUGE dyed spiked mohawk + headband tails + X-bandage on nose (NO GLASSES!)
        add_sphere(tmp, (0, 0.018, 0.188), (0.100, 0.108, 0.078), M["hair"], 16, 10)
        # Huge iconic neon-dyed punk mohawk spikes!
        for k in range(6):
            z = 0.285 + 0.042 * math.sin(k * 0.6)
            y = -0.082 + k * 0.046
            add_cyl(tmp, (0, y, z), 0.028, 0.004, 0.125, M["teal"], segs=8, rot=Euler((math.radians(-20 + k * 10), 0, 0)))
        # Martial headband + fluttering tails
        add_loft(tmp, [((0, 0.0, 0.184), 0.106, 0.118), ((0, 0.0, 0.214), 0.107, 0.119)], M["coal"], segs=22, cap_bottom=False, cap_top=False)
        for s in (-1, 1):
            add_tube(tmp, [(s * 0.018, 0.118, 0.196), (s * 0.048, 0.185, 0.172), (s * 0.082, 0.242, 0.140)], 0.012, M["teal"], segs=6)
        # Anime/punk white plaster bandage across the bridge of nose!
        add_box(tmp, (0, -0.118, 0.128), (0.068, 0.009, 0.018), M["ivory"], bevel=0.003, segs=1, rot=Euler((0, math.radians(8), 0)))

    elif variant == 6:
        # 6 (EPIC): ZEN MONK WITHOUT COMMITS — silver man-bun + beard + CLOSED EYES + FLOATING GOLDEN HALO (NO GLASSES!)
        add_sphere(tmp, (0, 0.018, 0.196), (0.104, 0.112, 0.084), M["ivory"], 18, 12)
        add_sphere(tmp, (0, 0.042, 0.288), (0.046, 0.046, 0.040), M["ivory"], 14, 10)
        add_cyl(tmp, (0, 0.035, 0.266), 0.034, 0.034, 0.014, M["coal"], segs=14)
        add_loft(tmp, [((0, -0.070, 0.105), 0.074, 0.046), ((0, -0.080, 0.052), 0.064, 0.040), ((0, -0.078, 0.008), 0.038, 0.026)], M["ivory"], segs=16)
        # Thick glowing golden/teal Zen Halo ring hovering cleanly above crown!
        halo_pts = [(0.136 * math.cos(i * math.tau / 20), 0.136 * math.sin(i * math.tau / 20), 0.360) for i in range(21)]
        add_tube(tmp, halo_pts, 0.016, M["teal"], segs=10, cap=False)

    elif variant == 7:
        # 7 (EPIC): SKOLKOVO VR SHAMAN — chunky cyberpunk VR Headset + dual glowing antennas (eyewear #1 of 2!)
        add_sphere(tmp, (0, 0.016, 0.196), (0.106, 0.114, 0.088), M["hair"], 18, 12)
        add_box(tmp, (0, -0.115, 0.152), (0.208, 0.084, 0.096), M["coal"], bevel=0.016, segs=2, smooth=True)
        add_box(tmp, (0, -0.156, 0.152), (0.176, 0.016, 0.064), M["teal"], bevel=0.006, segs=1)
        add_loft(tmp, [((0, 0.008, 0.136), 0.108, 0.118), ((0, 0.008, 0.168), 0.108, 0.118)], M["coal"], segs=20, cap_bottom=False, cap_top=False)
        for s in (-1, 1):
            add_tube(tmp, [(s * 0.104, -0.025, 0.175), (s * 0.138, -0.010, 0.348)], 0.009, M["coal"], segs=8)
            add_sphere(tmp, (s * 0.138, -0.010, 0.364), (0.025, 0.025, 0.025), M["teal"], 12, 8)

    elif variant == 8:
        # 8 (EPIC): MEETUP BOSS IN BALENCIAGA — TILTED GOLDEN CROWN + cocky smirk (NO GLASSES!)
        add_sphere(tmp, (0, 0.016, 0.196), (0.106, 0.114, 0.086), M["hair"], 18, 12)
        crown_xf = Matrix.Translation((0.014, 0.0, 0.242)) @ Euler((math.radians(-6), math.radians(-12), 0)).to_matrix().to_4x4()
        c_tmp = bmesh.new()
        add_loft(c_tmp, [((0, 0, -0.025), 0.106, 0.114), ((0, 0, 0.045), 0.126, 0.134)], M["gold"], segs=16)
        add_loft(c_tmp, [((0, 0, -0.022), 0.109, 0.117), ((0, 0, -0.006), 0.112, 0.120)], M["coal"], segs=16, cap_bottom=False, cap_top=False)
        for k in range(6):
            a = k * math.tau / 6
            px, py = 0.118 * math.cos(a), 0.124 * math.sin(a)
            add_cyl(c_tmp, (px, py, 0.082), 0.026, 0.005, 0.075, M["gold"], segs=8)
            add_sphere(c_tmp, (px, py, 0.122), (0.013, 0.013, 0.013), M["teal"], 8, 6)
        bmesh.ops.transform(c_tmp, matrix=crown_xf, verts=c_tmp.verts)
        _merge_bmesh(c_tmp, tmp)

    elif variant == 9:
        # 9 (LEGENDARY): FRIDAY 19:59 PROD DEMON — chaotic hair + MASSIVE DEVIL HORNS + glowing eyes (NO GLASSES!)
        add_sphere(tmp, (0, 0.014, 0.198), (0.107, 0.116, 0.088), M["hair"], 18, 12)
        for s in (-1, 1):
            add_tube(tmp, [
                (s * 0.058, -0.050, 0.232),
                (s * 0.118, -0.064, 0.308),
                (s * 0.102, -0.092, 0.392),
                (s * 0.056, -0.112, 0.440),
            ], [0.032, 0.024, 0.016, 0.005], M["crimson"], segs=10, cap=True)
            add_cyl(tmp, (s * 0.058, -0.050, 0.232), 0.035, 0.033, 0.018, M["coal"], segs=12)

    else:
        # 11: TOWER SECURITY CHIEF / SCRUM INQUISITOR — tall peaked officer cap + dark aviator shades (eyewear #2 of 2!)
        add_sphere(tmp, (0, 0.024, 0.184), (0.100, 0.108, 0.074), M["hair"], 16, 10)
        add_loft(tmp, [
            ((0, 0.008, 0.205), 0.110, 0.118),
            ((0, 0.004, 0.272), 0.124, 0.128),
            ((0, 0.000, 0.292), 0.112, 0.118),
        ], M["coal"], segs=22)
        add_box(tmp, (0, -0.130, 0.198), (0.150, 0.102, 0.014), M["coal"], bevel=0.005, segs=2, rot=Euler((math.radians(-14), 0, 0)), smooth=True)
        add_cyl(tmp, (0, -0.118, 0.248), 0.022, 0.019, 0.012, M["gold"], segs=14, rot=Euler((math.radians(90), 0, 0)))
        for s in (-1, 1):
            add_box(tmp, (s * 0.042, -0.113, 0.148), (0.058, 0.008, 0.038), M["coal"], bevel=0.004, segs=1)
            add_box(tmp, (s * 0.042, -0.116, 0.148), (0.048, 0.006, 0.029), M["teal"], bevel=0.003, segs=1)
            add_box(tmp, (s * 0.079, -0.056, 0.150), (0.007, 0.114, 0.008), M["coal"], bevel=0.002, segs=1)
        add_box(tmp, (0, -0.114, 0.156), (0.030, 0.007, 0.007), M["coal"], bevel=0.001, segs=1)

    bmesh.ops.transform(tmp, matrix=xf, verts=tmp.verts)
    _merge_bmesh(tmp, bm)


def build_upper_arm_geom(bm, M, side, xf=Matrix.Identity(4)):
    """Articulated upper arm with seamless tucked hoodie shoulder cap (pivot at shoulder 0,0,0 -> elbow Z=-0.255)."""
    tmp = bmesh.new()
    add_sphere(tmp, (-side * 0.014, 0, 0.002), (0.062, 0.062, 0.062), M["blue"], 16, 12)
    add_loft(tmp, [
        ((-side*0.010, 0.000, -0.008), 0.059, 0.060),
        (( side*0.008,-0.002, -0.130), 0.053, 0.054),
        (( side*0.014,-0.004, -0.255), 0.049, 0.050),
    ], M["blue"], segs=16)
    add_sphere(tmp, (side * 0.014, -0.004, -0.255), (0.048, 0.049, 0.048), M["blue"], 14, 10)
    bmesh.ops.transform(tmp, matrix=xf, verts=tmp.verts)
    _merge_bmesh(tmp, bm)


def build_forearm_geom(bm, M, side, xf=Matrix.Identity(4)):
    """Articulated forearm + ribbed cuff + sculpted hand (pivot at elbow 0,0,0 -> fingertip Z=-0.30)."""
    tmp = bmesh.new()
    add_sphere(tmp, (0, 0, 0.006), (0.047, 0.048, 0.047), M["blue"], 14, 10)
    add_loft(tmp, [
        ((0.0,       0.000, -0.006), 0.046, 0.047),
        ((0.0,      -0.004, -0.115), 0.043, 0.044),
        ((0.0,      -0.006, -0.205), 0.037, 0.038),
    ], M["blue"], segs=16)
    # Ribbed hoodie cuff
    add_loft(tmp, [
        ((0.0, -0.006, -0.202), 0.038, 0.039),
        ((0.0, -0.006, -0.238), 0.032, 0.033),
    ], M["navy"], segs=14)
    # Stylized sculpted hand
    add_sphere(tmp, (0.0, -0.006, -0.252), (0.024, 0.022, 0.022), M["skin"], 12, 8)
    add_box(tmp, (0.0, -0.010, -0.286), (0.028, 0.048, 0.056), M["skin"],
            bevel=0.010, segs=2, rot=Euler((math.radians(10), 0, 0)), smooth=True)
    add_box(tmp, (-side * 0.014, -0.026, -0.274), (0.016, 0.020, 0.032), M["skin"],
            bevel=0.006, segs=1, rot=Euler((math.radians(28), side * math.radians(16), 0)), smooth=True)
    bmesh.ops.transform(tmp, matrix=xf, verts=tmp.verts)
    _merge_bmesh(tmp, bm)


def build_arm_geom(bm, M, side, xf=Matrix.Identity(4)):
    """Assembled slightly-bent arm used for static hero standing/seated exports."""
    build_upper_arm_geom(bm, M, side, xf)
    elbow_xf = xf @ Matrix.Translation((side * 0.014, -0.004, -0.255)) @ Euler((math.radians(-24), 0, 0)).to_matrix().to_4x4()
    build_forearm_geom(bm, M, side, elbow_xf)


def build_thigh_geom(bm, M, side, xf=Matrix.Identity(4)):
    """Relaxed-fit 2026 jogger/denim thigh with fabric knee overlap (pivot at hip 0,0,0 -> knee Z=-0.42)."""
    tmp = bmesh.new()
    add_sphere(tmp, (0, 0, 0.008), (0.088, 0.090, 0.086), M["denim"], 16, 12)
    add_loft(tmp, [
        ((0, 0.000, -0.012), 0.087, 0.089),
        ((0,-0.005, -0.155), 0.081, 0.085),
        ((0,-0.007, -0.310), 0.074, 0.077),
        ((0,-0.004, -0.430), 0.068, 0.071),
    ], M["denim"], segs=16)
    add_sphere(tmp, (0, -0.004, -0.422), (0.066, 0.068, 0.065), M["denim"], 14, 10)
    bmesh.ops.transform(tmp, matrix=xf, verts=tmp.verts)
    _merge_bmesh(tmp, bm)


def build_shin_geom(bm, M, side, xf=Matrix.Identity(4)):
    """Relaxed-fit tapered jogger calf + chunky modern sneaker (pivot at knee 0,0,0 -> flat sole Z=-0.48)."""
    tmp = bmesh.new()
    add_sphere(tmp, (0, 0, 0.008), (0.064, 0.066, 0.063), M["denim"], 14, 10)
    add_loft(tmp, [
        ((0, 0.000,  0.002), 0.065, 0.067),
        ((0, 0.006, -0.135), 0.063, 0.066),
        ((0, 0.008, -0.275), 0.054, 0.057),
        ((0, 0.006, -0.362), 0.046, 0.048),
    ], M["denim"], segs=16)
    add_loft(tmp, [
        ((0, 0.006, -0.358), 0.046, 0.048),
        ((0, 0.005, -0.384), 0.039, 0.040),
    ], M["navy"], segs=14)
    add_loft(tmp, [
        ((0, 0.005, -0.380), 0.036, 0.037),
        ((0, 0.004, -0.412), 0.036, 0.037),
    ], M["ivory"], segs=14)

    # Chunky retro-modern startup sneaker (sole bottom strictly at Z = -0.480)
    add_box(tmp, (0, -0.052, -0.473), (0.100, 0.246, 0.014), M["coal"], bevel=0.005, segs=1)
    add_box(tmp, (0, -0.052, -0.453), (0.104, 0.252, 0.030), M["sole"], bevel=0.009, segs=2, smooth=True)
    add_box(tmp, (0, -0.036, -0.420), (0.096, 0.198, 0.052), M["navy"], bevel=0.016, segs=2, smooth=True)
    add_sphere(tmp, (0, -0.124, -0.428), (0.046, 0.058, 0.026), M["ivory"], 14, 10)
    add_box(tmp, (0, -0.060, -0.396), (0.058, 0.048, 0.026), M["ivory"],
            bevel=0.008, segs=1, rot=Euler((math.radians(-28), 0, 0)), smooth=True)
    add_box(tmp, (0, -0.036, -0.424), (0.099, 0.072, 0.016), M["ivory"], bevel=0.004, segs=1)

    bmesh.ops.transform(tmp, matrix=xf, verts=tmp.verts)
    _merge_bmesh(tmp, bm)


def _merge_bmesh(src, dst):
    vmap = {}
    for v in src.verts:
        vmap[v] = dst.verts.new(v.co)
    for f in src.faces:
        try:
            nf = dst.faces.new([vmap[v] for v in f.verts])
            nf.material_index = f.material_index
            nf.smooth = f.smooth
        except ValueError:
            pass
    src.free()


def export_part(filename, mat_keys, build_fn):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bm = bmesh.new()
    M = {k: i for i, k in enumerate(mat_keys)}
    build_fn(bm, M)
    bake_ao_and_export(bm, mat_keys, os.path.join(MODELS_DIR, filename))


def build_assembled_standing():
    """Assembled standing hero GLB (exact legacy 10-material slot order + comedy prop materials)."""
    mat_keys = ["skin", "hair", "blue", "denim", "navy", "sole", "ivory", "iris", "teal", "coal", "gold", "silver", "wood", "crimson"]
    def _build(bm, M):
        pelvis_z = 0.90
        build_torso_geom(bm, M, Matrix.Translation((0, 0, pelvis_z)))
        build_head_geom(bm, M, Matrix.Translation((0, 0, pelvis_z + 0.54)))
        build_arm_geom(bm, M, +1, Matrix.Translation(( 0.168, 0.0, pelvis_z + 0.432)) @ Euler((math.radians(6), math.radians(-5), 0)).to_matrix().to_4x4())
        build_arm_geom(bm, M, -1, Matrix.Translation((-0.168, 0.0, pelvis_z + 0.432)) @ Euler((math.radians(6), math.radians( 5), 0)).to_matrix().to_4x4())
        for s in (-1, 1):
            build_thigh_geom(bm, M, s, Matrix.Translation((s * 0.088, 0, pelvis_z)))
            build_shin_geom(bm, M, s, Matrix.Translation((s * 0.088, 0, pelvis_z - 0.42)))
    export_part("char_vibe_coder.glb", mat_keys, _build)


def build_assembled_seated():
    """Dedicated crisp seated typing pose aligned with WorkstationScale=0.92 chair (seat Y=0.47, desk Y=0.73)."""
    mat_keys = ["skin", "hair", "blue", "denim", "navy", "sole", "ivory", "iris", "teal", "coal", "gold", "silver", "wood", "crimson"]
    def _build(bm, M):
        # Chair seat in prop_workstation_full is at local Y_gltf ~= 0.51; seated feet at SeatedFeet.
        # Build seated pose in local units (scaled by WorkstationScale=0.92 in C#):
        # Pelvis sits squarely on seat cushion at Z=0.56, shifted slightly back (Y=+0.10),
        # thighs angled horizontally forward toward desk (-Y), shins drop cleanly vertically to Z=0.0!
        pelvis = Vector((0.0, 0.14, 0.54))
        torso_xf = Matrix.Translation(pelvis) @ Euler((math.radians(-10), 0, 0)).to_matrix().to_4x4()
        build_torso_geom(bm, M, torso_xf)
        head_pos = torso_xf @ Vector((0, 0, 0.54))
        build_head_geom(bm, M, Matrix.Translation(head_pos) @ Euler((math.radians(-14), 0, 0)).to_matrix().to_4x4())
        # Arms reaching naturally forward & slightly inward over the mechanical keyboard!
        for s in (-1, 1):
            shld = torso_xf @ Vector((s * 0.168, 0.0, 0.432))
            arm_rot = Euler((math.radians(-58), s * math.radians(-8), s * math.radians(-10)))
            build_arm_geom(bm, M, s, Matrix.Translation(shld) @ arm_rot.to_matrix().to_4x4())
        for s in (-1, 1):
            hip = Vector((s * 0.094, 0.12, 0.53))
            thigh_rot = Euler((math.radians(-84), 0, s * math.radians(5)))
            thigh_xf = Matrix.Translation(hip) @ thigh_rot.to_matrix().to_4x4()
            build_thigh_geom(bm, M, s, thigh_xf)
            knee = thigh_xf @ Vector((0, 0, -0.42))
            # Adjust knee Z slightly so sole hits exactly Z=0.0
            knee_grounded = Vector((knee.x, knee.y, 0.48))
            build_shin_geom(bm, M, s, Matrix.Translation(knee_grounded) @ Euler((math.radians(6), 0, 0)).to_matrix().to_4x4())
    export_part("char_vibe_coder_seated.glb", mat_keys, _build)


def build_seated_body_headless():
    """Headless seated body GLB so Open-Space seated coders wear their EXACT captured collectible head variant!"""
    mat_keys = ["skin", "hair", "blue", "denim", "navy", "sole", "ivory", "iris", "teal", "coal"]
    def _build(bm, M):
        pelvis = Vector((0.0, 0.14, 0.54))
        torso_xf = Matrix.Translation(pelvis) @ Euler((math.radians(-10), 0, 0)).to_matrix().to_4x4()
        build_torso_geom(bm, M, torso_xf)
        for s in (-1, 1):
            shld = torso_xf @ Vector((s * 0.168, 0.0, 0.432))
            arm_rot = Euler((math.radians(-58), s * math.radians(-8), s * math.radians(-10)))
            build_arm_geom(bm, M, s, Matrix.Translation(shld) @ arm_rot.to_matrix().to_4x4())
        for s in (-1, 1):
            hip = Vector((s * 0.094, 0.12, 0.53))
            thigh_rot = Euler((math.radians(-84), 0, s * math.radians(5)))
            thigh_xf = Matrix.Translation(hip) @ thigh_rot.to_matrix().to_4x4()
            build_thigh_geom(bm, M, s, thigh_xf)
            knee = thigh_xf @ Vector((0, 0, -0.42))
            knee_grounded = Vector((knee.x, knee.y, 0.48))
            build_shin_geom(bm, M, s, Matrix.Translation(knee_grounded) @ Euler((math.radians(6), 0, 0)).to_matrix().to_4x4())
    export_part("coder_seated_body.glb", mat_keys, _build)


def build_forearm_hook_geom(bm, M):
    """Vibe-Pirate right forearm: hoodie sleeve ending in a leather cuff & CURVED STEEL PIRATE HOOK!"""
    tmp = bmesh.new()
    add_sphere(tmp, (0, 0, 0.006), (0.047, 0.048, 0.047), M["blue"], 14, 10)
    add_loft(tmp, [((0, 0, -0.006), 0.046, 0.047), ((0, -0.004, -0.130), 0.042, 0.043), ((0, -0.006, -0.205), 0.038, 0.039)], M["blue"], segs=16)
    # Leather/iron pirate socket cup
    add_cyl(tmp, (0, -0.006, -0.228), 0.044, 0.036, 0.056, M["coal"], segs=16)
    add_cyl(tmp, (0, -0.006, -0.204), 0.046, 0.046, 0.014, M["gold"], segs=16)
    # Gleaming curved steel pirate hook!
    hook_pts = [
        (0.0, -0.006, -0.254),
        (0.0, -0.010, -0.295),
        (0.0, -0.048, -0.342),
        (0.0, -0.094, -0.348),
        (0.0, -0.118, -0.312),
        (0.0, -0.098, -0.275),
    ]
    add_tube(tmp, hook_pts, [0.015, 0.014, 0.013, 0.011, 0.008, 0.003], M["silver"], segs=10, cap=True)
    _merge_bmesh(tmp, bm)


def build_shin_peg_geom(bm, M):
    """Vibe-Pirate right leg: rolled-up denim stump + CARVED WOODEN PEG-LEG with iron foot cap!"""
    tmp = bmesh.new()
    add_sphere(tmp, (0, 0, 0.008), (0.064, 0.066, 0.063), M["denim"], 14, 10)
    add_loft(tmp, [((0, 0, 0.002), 0.065, 0.067), ((0, 0.004, -0.105), 0.062, 0.064), ((0, 0.004, -0.135), 0.067, 0.069)], M["denim"], segs=16)
    add_cyl(tmp, (0, 0.004, -0.155), 0.050, 0.044, 0.036, M["coal"], segs=14)
    # Turned wooden pirate peg-leg reaching exact ground Z = -0.480!
    add_loft(tmp, [
        ((0, 0.004, -0.165), 0.040, 0.040),
        ((0, 0.004, -0.240), 0.035, 0.035),
        ((0, 0.004, -0.345), 0.026, 0.026),
        ((0, 0.004, -0.448), 0.021, 0.021),
    ], M["wood"], segs=14)
    add_cyl(tmp, (0, 0.004, -0.464), 0.024, 0.022, 0.032, M["coal"], segs=14)
    add_cyl(tmp, (0, 0.004, -0.240), 0.039, 0.039, 0.014, M["gold"], segs=14)
    _merge_bmesh(tmp, bm)


def build_forearm_cyber_geom(bm, M):
    """Punk Refactorer right forearm: CHUNKY HYDRAULIC CYBER-CLAW ROBOT ARM!"""
    tmp = bmesh.new()
    add_sphere(tmp, (0, 0, 0.006), (0.052, 0.052, 0.052), M["coal"], 14, 10)
    add_loft(tmp, [((0, 0, -0.012), 0.054, 0.054), ((0, -0.004, -0.195), 0.048, 0.048)], M["silver"], segs=12, smooth=False)
    add_box(tmp, (-0.048, -0.004, -0.105), (0.016, 0.022, 0.140), M["coal"], bevel=0.003, segs=1)
    add_cyl(tmp, (0, -0.006, -0.220), 0.052, 0.048, 0.042, M["coal"], segs=14)
    # Glowing power core ring + twin mechanical pincer claws!
    add_cyl(tmp, (0, -0.006, -0.130), 0.055, 0.055, 0.018, M["teal"], segs=14)
    for s in (-1, 1):
        add_tube(tmp, [(s * 0.026, -0.006, -0.238), (s * 0.058, -0.024, -0.305), (s * 0.024, -0.042, -0.360)], [0.016, 0.014, 0.006], M["silver"], segs=8, cap=True)
    _merge_bmesh(tmp, bm)


def build_forearm_coffee_geom(bm, M):
    """Caffeine/Smoothie Maniac left forearm holding an OVERSIZED ICED SMOOTHIE CUP WITH STRAW!"""
    build_forearm_geom(bm, M, +1)
    tmp = bmesh.new()
    # Oversized clear/pastel smoothie cup gripped in hand (tilted upright when forearm is raised!)
    cup_xf = Matrix.Translation((0.0, -0.072, -0.295)) @ Euler((math.radians(82), 0, 0)).to_matrix().to_4x4()
    add_loft(tmp, [((0, 0, -0.095), 0.042, 0.042), ((0, 0, 0.095), 0.056, 0.056)], M["ivory"], segs=18)
    add_loft(tmp, [((0, 0, -0.025), 0.050, 0.050), ((0, 0, 0.040), 0.054, 0.054)], M["teal"], segs=18, cap_bottom=False, cap_top=False)
    add_sphere(tmp, (0, 0, 0.095), (0.057, 0.057, 0.036), M["ivory"], 16, 10)  # dome lid
    add_tube(tmp, [(0, 0, 0.095), (0.012, -0.008, 0.215)], 0.007, M["crimson"], segs=8)  # long straw!
    bmesh.ops.transform(tmp, matrix=cup_xf, verts=tmp.verts)
    _merge_bmesh(tmp, bm)


def build_forearm_staff_geom(bm, M):
    """40-Framework Wizard right forearm holding a TALL MAGIC WOODEN STAFF WITH GLOWING ORB!"""
    build_forearm_geom(bm, M, -1)
    tmp = bmesh.new()
    staff_xf = Matrix.Translation((-0.012, -0.042, -0.290)) @ Euler((math.radians(78), 0, 0)).to_matrix().to_4x4()
    add_tube(tmp, [(0, 0, -0.58), (0, 0, 0.0), (0.015, -0.01, 0.48), (-0.012, 0.01, 0.72)], [0.018, 0.020, 0.022, 0.016], M["wood"], segs=10)
    # Gold cradle prongs + floating glowing crystal orb at top of staff!
    for s in (-1, 1):
        add_tube(tmp, [(-0.012, 0.01, 0.71), (s * 0.055, 0.01, 0.79), (s * 0.036, 0.01, 0.87)], 0.009, M["gold"], segs=8)
    add_sphere(tmp, (-0.008, 0.01, 0.825), (0.052, 0.052, 0.052), M["teal"], 14, 10)
    bmesh.ops.transform(tmp, matrix=staff_xf, verts=tmp.verts)
    _merge_bmesh(tmp, bm)


def build_torso_chain_geom(bm, M):
    """Crypto-Bro torso: standard hoodie + HUGE CHUNKY GOLD CHAIN & MEDALLION on chest!"""
    build_torso_geom(bm, M)
    tmp = bmesh.new()
    chain_pts = [
        (-0.088, -0.055, 0.488),
        (-0.072, -0.145, 0.390),
        (-0.035, -0.156, 0.285),
        ( 0.000, -0.158, 0.245),
        ( 0.035, -0.156, 0.285),
        ( 0.072, -0.145, 0.390),
        ( 0.088, -0.055, 0.488),
    ]
    add_tube(tmp, chain_pts, 0.015, M["gold"], segs=8)
    add_cyl(tmp, (0.0, -0.164, 0.228), 0.056, 0.056, 0.018, M["gold"], segs=18, rot=Euler((math.radians(90), 0, 0)))
    add_cyl(tmp, (0.0, -0.172, 0.228), 0.034, 0.034, 0.008, M["coal"], segs=16, rot=Euler((math.radians(90), 0, 0)))
    _merge_bmesh(tmp, bm)


def build_torso_jetpack_geom(bm, M):
    """Skolkovo VR Shaman torso: hoodie + MINI SERVER-RACK JETPACK with twin thrusters on back!"""
    build_torso_geom(bm, M)
    tmp = bmesh.new()
    add_box(tmp, (0, 0.195, 0.295), (0.245, 0.115, 0.295), M["coal"], bevel=0.014, segs=2, smooth=True)
    for s in (-1, 1):
        add_cyl(tmp, (s * 0.075, 0.215, 0.295), 0.048, 0.044, 0.270, M["silver"], segs=14)
        add_cyl(tmp, (s * 0.075, 0.215, 0.135), 0.036, 0.050, 0.068, M["coal"], segs=14)
        add_sphere(tmp, (s * 0.075, 0.215, 0.105), (0.032, 0.032, 0.022), M["teal"], 10, 8)
    _merge_bmesh(tmp, bm)


def build_torso_devil_geom(bm, M):
    """Friday 19:59 Prod Demon torso: hoodie + CURVED POINTED DEVIL TAIL behind!"""
    build_torso_geom(bm, M)
    tmp = bmesh.new()
    tail_pts = [
        (0.0, 0.125, 0.035),
        (0.04, 0.245, 0.015),
        (0.14, 0.345, 0.095),
        (0.20, 0.365, 0.240),
        (0.16, 0.320, 0.345),
    ]
    add_tube(tmp, tail_pts, [0.022, 0.018, 0.015, 0.012, 0.008], M["crimson"], segs=8)
    add_cyl(tmp, (0.155, 0.312, 0.365), 0.032, 0.004, 0.068, M["crimson"], segs=4, rot=Euler((math.radians(-22), math.radians(-18), 0)))
    _merge_bmesh(tmp, bm)


def main():
    head_mats = ["skin", "hair", "ivory", "iris", "gold", "silver", "wood", "crimson", "teal", "coal"]
    export_part("coder_torso.glb",         ["blue", "navy", "skin", "ivory", "coal", "denim"], lambda bm, M: build_torso_geom(bm, M))
    export_part("coder_torso_chain.glb",   ["blue", "navy", "skin", "ivory", "coal", "denim", "gold"], lambda bm, M: build_torso_chain_geom(bm, M))
    export_part("coder_torso_jetpack.glb", ["blue", "navy", "skin", "ivory", "coal", "denim", "silver", "teal"], lambda bm, M: build_torso_jetpack_geom(bm, M))
    export_part("coder_torso_devil.glb",   ["blue", "navy", "skin", "ivory", "coal", "denim", "crimson"], lambda bm, M: build_torso_devil_geom(bm, M))
    for v in range(12):
        export_part(f"coder_head_{v}.glb", head_mats, lambda bm, M, vv=v: build_head_geom(bm, M, variant=vv))
    export_part("coder_upper_arm_l.glb",   ["blue"], lambda bm, M: build_upper_arm_geom(bm, M, +1))
    export_part("coder_upper_arm_r.glb",   ["blue"], lambda bm, M: build_upper_arm_geom(bm, M, -1))
    export_part("coder_forearm_l.glb",     ["blue", "navy", "skin"], lambda bm, M: build_forearm_geom(bm, M, +1))
    export_part("coder_forearm_r.glb",     ["blue", "navy", "skin"], lambda bm, M: build_forearm_geom(bm, M, -1))
    export_part("coder_forearm_r_hook.glb",  ["blue", "navy", "coal", "gold", "silver"], lambda bm, M: build_forearm_hook_geom(bm, M))
    export_part("coder_forearm_r_cyber.glb", ["blue", "navy", "coal", "silver", "teal"], lambda bm, M: build_forearm_cyber_geom(bm, M))
    export_part("coder_forearm_l_coffee.glb",["blue", "navy", "skin", "ivory", "crimson", "teal"], lambda bm, M: build_forearm_coffee_geom(bm, M))
    export_part("coder_forearm_r_staff.glb", ["blue", "navy", "skin", "wood", "gold", "teal"], lambda bm, M: build_forearm_staff_geom(bm, M))
    export_part("coder_thigh_l.glb",       ["denim"], lambda bm, M: build_thigh_geom(bm, M, +1))
    export_part("coder_thigh_r.glb",       ["denim"], lambda bm, M: build_thigh_geom(bm, M, -1))
    export_part("coder_shin_l.glb",        ["denim", "navy", "ivory", "sole", "coal"], lambda bm, M: build_shin_geom(bm, M, +1))
    export_part("coder_shin_r.glb",        ["denim", "navy", "ivory", "sole", "coal"], lambda bm, M: build_shin_geom(bm, M, -1))
    export_part("coder_shin_r_peg.glb",    ["denim", "coal", "wood", "gold"], lambda bm, M: build_shin_peg_geom(bm, M))
    build_assembled_standing()
    build_assembled_seated()
    build_seated_body_headless()
    print("ALL 12 COMEDY-COLLECTIBLE VIBE CODERS + PIRATE HOOK/PEG-LEG/CYBER-CLAW/STAFF/COFFEE/JETPACK/DEVIL BUILT!")


if __name__ == "__main__":
    main()
