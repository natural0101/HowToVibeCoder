"""
Cinema-Grade Stylized High-Poly HR Veronika v3 Builder (Arcane / Overwatch / Sims-4 Beauty Standard)
Matches target concept sheet `veronika_target_concept_1795129376528.jpg` 1-to-1 and renders cleanly in Raylib OpenGL (`GL_CULL_FACE`).

Exports 8 synchronized GLBs:
  1. assets/models/char_hr_veronika_head.glb        (origin at HEAD_PIVOT_LOCAL = (0.0, 1.415, 0.045))
  2. assets/models/char_hr_veronika_head_blink.glb  (origin at HEAD_PIVOT_LOCAL = (0.0, 1.415, 0.045))
  3. assets/models/char_hr_veronika_body.glb        (slender wrap blazer torso + lariat + trousers/boots, NO arms, origin [0,0,0])
  4. assets/models/char_hr_veronika_arm_l.glb       (complete left sleeve + tailored flared cuff + snug bangles + palm-down graceful hand, origin at ARM_L_PIVOT_LOCAL = (+0.138, 1.298, 0.032))
  5. assets/models/char_hr_veronika_arm_r.glb       (exact mirror-symmetric complete right sleeve + tailored flared cuff + snug bangles + palm-down graceful hand, origin at ARM_R_PIVOT_LOCAL = (-0.138, 1.298, 0.032))
  6. assets/models/char_hr_veronika_hands.glb       (both complete unified arms shifted to HANDS_PIVOT_LOCAL = (0.0, 1.310, 0.320) for 100% backward compatibility)
  7. assets/models/prop_veronika_tarot_table.glb    (64-seg velvet table, 48x32 crystal ball on 4-legged brass scroll stand, candles & 4 illustrated Tarot cards, origin [0,0,0])
  8. assets/models/char_hr_veronika.glb             (full combined exhibit for Archive [N] turntable, origin [0,0,0])

Also renders 3-panel proof `veronika_v3_proof.png` with `use_backface_culling = True` enabled on all materials.
"""

import math
import pathlib
import bmesh
import bpy
import numpy as np
from mathutils import Euler, Vector as V


HEAD_PIVOT_LOCAL = V((0.0, 1.415, 0.045))
ARM_L_PIVOT_LOCAL = V((0.138, 1.298, 0.032))
ARM_R_PIVOT_LOCAL = V((-0.138, 1.298, 0.032))
HANDS_PIVOT_LOCAL = V((0.0, 1.310, 0.320))

ROOT_DIR = pathlib.Path(__file__).resolve().parents[3]
OUT_MODELS = ROOT_DIR / "assets" / "models"
ARTIFACT_PROOF_1 = pathlib.Path(r"C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a\veronika_v3_proof.png")
ARTIFACT_PROOF_2 = pathlib.Path(r"C:\Users\se-20\.gemini\antigravity\brain\34a5dbd6-7dba-4b8d-8112-53de935b5112\scratch\veronika_v3_proof.png")
SCREENSHOT_PROOF = ROOT_DIR / "screenshots" / "veronika_v3_proof.png"

VERONIKA_MAT_ORDER = [
    "skin",
    "lip",
    "eyeshadow",
    "nail",
    "hair",
    "hairlight",
    "gold",
    "silk",
    "plum",
    "trouser",
    "ivory",
    "iris",
    "catchlight",
    "onyx",
    "teal",
    "orb",
    "card",
    "flame",
    "coal",
    "sole",
    "ink",
    "mouth_cavity",
    "teeth",
]

# Tuned specifically for Raylib's new Tarot booth lighting:
# ZERO emissive on skin/lips/clothes so 3D facial shading, nose bridge, cheekbones & blush render with rich depth!
VERONIKA_PALETTE_HEX = {
    "skin":       "E2AD92",  # Warm sculpted peach-tan complexion
    "lip":        "861832",  # Rich berry-crimson smiling lips
    "eyeshadow":  "98505E",  # Smoky plum-rose upper eyelid crease
    "nail":       "520A1E",  # Glossy deep-burgundy manicure
    "hair":       "241310",  # Rich dark-espresso chocolate waves
    "hairlight":  "3C221C",  # Warm mahogany-espresso wave highlights
    "gold":       "DFB248",  # Antique polished gold
    "silk":       "42102B",  # Deep plum-burgundy wine velvet blazer
    "plum":       "2E0A1D",  # Deeper wine-plum velvet lapel & table drape
    "trouser":    "1A131E",  # Tailored dark charcoal-plum slim trousers
    "ivory":      "F6F2F4",  # Clean eye sclera & beeswax candles
    "iris":       "8E32F0",  # Luminous electric-violet iris
    "catchlight": "FFFFFF",  # Pure sparkling white cornea catchlight
    "onyx":       "050307",  # Jet-black winged eyeliner, lashes & pupil
    "teal":       "12A87C",  # Faceted emerald-teal forehead drop gem
    "orb":        "CF96FF",  # Glowing lavender-amethyst crystal ball
    "card":       "EFE0B8",  # Illustrated Tarot card parchment
    "flame":      "FFF0AA",  # Warm candle flame
    "coal":       "141018",  # Sleek dark ankle boots
    "sole":       "0A080C",  # Boot heel/sole
    "ink":        "220816",  # Deep wine-black inner cuff lining
    "mouth_cavity": "140916",  # Deep matte plum-shadow inner mouth cavity (B>R avoids shader isHair specular)
    "teeth":      "EFE8E4",  # Clean soft natural ivory upper teeth (no emissive glow)
}

PBR_SPECS = {
    # key: (metallic, roughness, glTF emissiveFactor (R, G, B))
    # Skin, lips, hair, clothes have emissiveFactor = None (0,0,0) so Raylib lighting + painted shading gives real 3D form!
    "skin":       (0.00, 0.46, None),
    "lip":        (0.00, 0.28, None),
    "eyeshadow":  (0.00, 0.48, None),
    "nail":       (0.05, 0.18, None),
    "hair":       (0.02, 0.46, None),
    "hairlight":  (0.04, 0.40, None),
    "gold":       (0.86, 0.24, None),
    "silk":       (0.00, 0.68, None),
    "plum":       (0.00, 0.74, None),
    "trouser":    (0.00, 0.64, None),
    "ivory":      (0.00, 0.20, (0.06, 0.05, 0.06)),
    "iris":       (0.00, 0.10, (0.12, 0.04, 0.20)),  # Subtle mystical violet iris glow
    "catchlight": (0.00, 0.04, (0.78, 0.78, 0.82)),  # Crisp pure-white eye catchlights!
    "onyx":       (0.02, 0.20, None),
    "teal":       (0.15, 0.14, (0.04, 0.18, 0.12)),
    "orb":        (0.05, 0.08, (0.32, 0.18, 0.44)),
    "card":       (0.00, 0.58, None),
    "flame":      (0.00, 0.10, (0.95, 0.72, 0.28)),
    "coal":       (0.08, 0.30, None),
    "sole":       (0.04, 0.45, None),
    "ink":        (0.00, 0.80, None),
    "mouth_cavity": (0.00, 0.90, None),
    "teeth":      (0.00, 0.22, None),
}


def srgb_hex_to_linear(h):
    vals = [int(h[i : i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in vals] + [1.0]


def build_veronika_beauty_atlas():
    """
    Create a 1024x1024 procedural RGBA beauty texture atlas (`VeronikaBeautyAtlas`):
      - Quadrant 0 (x: 0..512,   y: 0..512,   u in [0.0..0.5], v in [0.0..0.5]):
        Rich Sculpted Warm Peach-Tan Facial Complexion Map (3D nose bridge & under-tip contour, rosy-terracotta cheek blush,
        smoky plum-brown eye socket depth, cheekbone/forehead highlights, and warm jawline contour)
      - Quadrant 1 (x: 512..1024, y: 0..512,  u in [0.5..1.0], v in [0.0..0.5]):
        Luminous Electric Violet-Amethyst Radial Iris Map (dark indigo limbal ring, glowing violet-magenta iris fibers,
        and jet-black central pupil)
      - Quadrant 3 (x: 512..1024, y: 512..1024, u in [0.5..1.0], v in [0.5..1.0]):
        4 Illustrated Major Arcana Tarot Cards (The Moon XVIII, The Sun XIX, The Tower XVI, The Star XVII)
    """
    existing = bpy.data.images.get("VeronikaBeautyAtlas")
    if existing is not None:
        return existing

    W, H = 1024, 1024
    arr = np.ones((H, W, 4), dtype=np.float32)

    # --- Quadrant 0 (0..512, 0..512): Sculpted Warm Peach-Tan Face & 3D Makeup Map ---
    y0, x0 = np.mgrid[0:512, 0:512].astype(np.float32)
    uf = (x0 - 256.0) / 200.0
    vf = (y0 - 256.0) / 200.0

    # Warm luminous peach-ivory base (`sRGB #F2C8B2` -> linear ~(0.88, 0.58, 0.44))
    r_skin = np.full_like(uf, 0.88)
    g_skin = np.full_like(uf, 0.58)
    b_skin = np.full_like(uf, 0.44)

    # 1. Rosy-peach cheek blush on cheek apples (|uf| ~ 0.44, vf ~ -0.02) + warm nose tip blush
    cheek_blush = np.exp(-(((np.abs(uf) - 0.44) / 0.23) ** 2 + ((vf + 0.02) / 0.18) ** 2))
    nose_blush = 0.40 * np.exp(-(((uf) / 0.13) ** 2 + ((vf + 0.10) / 0.09) ** 2))
    blush = np.clip(cheek_blush + nose_blush, 0.0, 1.0)
    r_skin = r_skin * (1.0 - 0.12 * blush) + 0.82 * (0.12 * blush)
    g_skin = g_skin * (1.0 - 0.30 * blush) + 0.32 * (0.30 * blush)
    b_skin = b_skin * (1.0 - 0.30 * blush) + 0.28 * (0.30 * blush)

    # 2. Soft Sculpted Nose Bridge Sides & Under-Nose Tip Shadow
    nose_side_shadow = 0.65 * np.exp(-(((np.abs(uf) - 0.115) / 0.055) ** 2 + ((vf - 0.08) / 0.22) ** 2))
    nose_under_shadow = 0.70 * np.exp(-(((uf) / 0.12) ** 2 + ((vf + 0.18) / 0.065) ** 2))
    philtrum_shadow = 0.35 * np.exp(-(((uf) / 0.055) ** 2 + ((vf + 0.28) / 0.08) ** 2))
    under_lip_shadow = 0.45 * np.exp(-(((uf) / 0.16) ** 2 + ((vf + 0.56) / 0.07) ** 2))
    contour_nose = np.clip(nose_side_shadow + nose_under_shadow + philtrum_shadow + under_lip_shadow, 0.0, 1.0)
    r_skin *= 1.0 - 0.16 * contour_nose
    g_skin *= 1.0 - 0.22 * contour_nose
    b_skin *= 1.0 - 0.24 * contour_nose

    # 3. Smoky plum-rose upper eye socket & brow bone shading (|uf| ~ 0.38, vf ~ 0.28)
    eye_shadow = np.exp(-(((np.abs(uf) - 0.38) / 0.20) ** 2 + ((vf - 0.28) / 0.12) ** 2))
    r_skin = r_skin * (1.0 - 0.26 * eye_shadow) + 0.52 * (0.26 * eye_shadow)
    g_skin = g_skin * (1.0 - 0.36 * eye_shadow) + 0.24 * (0.36 * eye_shadow)
    b_skin = b_skin * (1.0 - 0.26 * eye_shadow) + 0.34 * (0.26 * eye_shadow)

    # 4. Soft luminous highlights on center nose bridge, cheekbone tops, chin center, and forehead center
    highlight = (
        0.65 * np.exp(-((uf / 0.055) ** 2 + ((vf - 0.06) / 0.20) ** 2))
        + 0.45 * np.exp(-(((np.abs(uf) - 0.34) / 0.15) ** 2 + ((vf - 0.09) / 0.09) ** 2))
        + 0.35 * np.exp(-((uf / 0.26) ** 2 + ((vf - 0.52) / 0.16) ** 2))
        + 0.35 * np.exp(-((uf / 0.14) ** 2 + ((vf + 0.70) / 0.10) ** 2))
    )
    r_skin = np.clip(r_skin + 0.08 * highlight, 0.0, 1.0)
    g_skin = np.clip(g_skin + 0.09 * highlight, 0.0, 1.0)
    b_skin = np.clip(b_skin + 0.08 * highlight, 0.0, 1.0)

    # 5. Warm jawline & temple perimeter contour
    jaw_contour = np.clip(((uf / 0.82) ** 2 + ((vf + 0.08) / 0.85) ** 2 - 0.62) * 0.45, 0.0, 0.24)
    r_skin *= 1.0 - 0.14 * jaw_contour
    g_skin *= 1.0 - 0.18 * jaw_contour
    b_skin *= 1.0 - 0.20 * jaw_contour

    arr[0:512, 0:512, 0] = np.clip(r_skin, 0.0, 1.0)
    arr[0:512, 0:512, 1] = np.clip(g_skin, 0.0, 1.0)
    arr[0:512, 0:512, 2] = np.clip(b_skin, 0.0, 1.0)

    # --- Quadrant 1 (512..1024, 0..512): Electric Violet-Amethyst Radial Iris Texture ---
    yi, xi = np.mgrid[0:512, 0:512].astype(np.float32)
    dx = (xi - 256.0) / 215.0
    dy = (yi - 256.0) / 215.0
    r = np.sqrt(dx * dx + dy * dy)
    theta = np.arctan2(dy, dx)

    fibers = (
        0.5
        + 0.28 * np.sin(theta * 42.0 + np.sin(r * 16.0) * 1.8)
        + 0.14 * np.cos(theta * 78.0 - r * 10.0)
        + 0.08 * np.sin(theta * 19.0)
    )
    limbal = np.clip((r - 0.70) / 0.25, 0.0, 1.0)
    pupil_ring = np.exp(-(((r - 0.39) / 0.08) ** 2))
    crescent = np.exp(-(((r - 0.56) / 0.16) ** 2)) * np.clip((-dy * 0.85 + dx * 0.35) + 0.40, 0.0, 1.40)

    stroma_r = 0.34 + 0.32 * fibers + 0.38 * crescent + 0.18 * pupil_ring
    stroma_g = 0.03 + 0.08 * fibers + 0.16 * crescent + 0.05 * pupil_ring
    stroma_b = 0.82 + 0.18 * fibers + 0.22 * crescent + 0.14 * pupil_ring

    iris_r = stroma_r * (1.0 - 0.90 * limbal) + 0.02 * (0.90 * limbal)
    iris_g = stroma_g * (1.0 - 0.94 * limbal) + 0.005 * (0.94 * limbal)
    iris_b = stroma_b * (1.0 - 0.82 * limbal) + 0.10 * (0.82 * limbal)

    # Crisp central jet-black pupil (r < 0.38)
    p_mask = np.clip((0.38 - r) / 0.028, 0.0, 1.0)
    iris_r = iris_r * (1.0 - p_mask) + 0.005 * p_mask
    iris_g = iris_g * (1.0 - p_mask) + 0.002 * p_mask
    iris_b = iris_b * (1.0 - p_mask) + 0.010 * p_mask

    arr[0:512, 512:1024, 0] = np.clip(iris_r, 0.0, 1.0)
    arr[0:512, 512:1024, 1] = np.clip(iris_g, 0.0, 1.0)
    arr[0:512, 512:1024, 2] = np.clip(iris_b, 0.0, 1.0)

    # --- Quadrant 3 (512..1024, 512..1024): 4 Illustrated Major Arcana Tarot Cards ---
    for c_idx in range(4):
        x_start = 512 + c_idx * 128
        x_end = x_start + 128
        yc, xc = np.mgrid[0:512, 0:128].astype(np.float32)
        nx = (xc - 64.0) / 52.0
        ny = (yc - 256.0) / 220.0

        sub = np.zeros((512, 128, 3), dtype=np.float32)
        sub[:, :] = (0.95, 0.88, 0.73)

        border_outer = (np.abs(nx) > 0.88) | (np.abs(ny) > 0.90)
        border_gold = ((np.abs(nx) > 0.77) & (np.abs(nx) <= 0.88)) | ((np.abs(ny) > 0.81) & (np.abs(ny) <= 0.90))
        header_box = (np.abs(nx) < 0.72) & (ny > 0.64) & (ny < 0.78)
        footer_box = (np.abs(nx) < 0.72) & (ny < -0.64) & (ny > -0.78)

        sub[border_outer] = (0.16, 0.07, 0.18)
        sub[border_gold] = (0.88, 0.68, 0.25)
        sub[header_box] = (0.20, 0.10, 0.24)
        sub[footer_box] = (0.20, 0.10, 0.24)

        rad = np.sqrt(nx * nx + (ny * 1.32) ** 2)
        ang = np.arctan2(ny * 1.32, nx)

        if c_idx == 0:
            bg = (np.abs(nx) < 0.74) & (np.abs(ny) < 0.60)
            sub[bg] = (0.14, 0.09, 0.26)
            moon = (rad < 0.44) & (np.sqrt((nx - 0.15) ** 2 + (ny * 1.32 - 0.06) ** 2) > 0.33)
            pillars = (np.abs(np.abs(nx) - 0.46) < 0.09) & (ny > -0.56) & (ny < -0.05)
            sub[pillars] = (0.58, 0.44, 0.34)
            sub[moon] = (0.96, 0.78, 0.28)
        elif c_idx == 1:
            rays = (rad < 0.62) & (rad > 0.28) & (np.cos(ang * 12.0) > 0.15)
            disk = rad <= 0.30
            core = rad <= 0.13
            sub[rays] = (0.92, 0.56, 0.18)
            sub[disk] = (0.98, 0.80, 0.22)
            sub[core] = (0.86, 0.24, 0.22)
        elif c_idx == 2:
            tower = (np.abs(nx) < 0.22 + 0.08 * (-ny)) & (ny > -0.56) & (ny < 0.28)
            crown = (np.abs(nx) < 0.28) & (ny >= 0.28) & (ny < 0.38)
            bolt = (np.abs(nx - 0.44 * (ny - 0.24)) < 0.07) & (ny > 0.12) & (ny < 0.58)
            sub[tower] = (0.22, 0.14, 0.24)
            sub[crown] = (0.94, 0.72, 0.24)
            sub[bolt] = (0.94, 0.24, 0.26)
        else:
            bg = (np.abs(nx) < 0.74) & (np.abs(ny) < 0.60)
            sub[bg] = (0.22, 0.12, 0.34)
            star_r = 0.50 * (0.42 + 0.58 * (np.abs(np.cos(ang * 4.0)) ** 3))
            star = rad < star_r
            orbit = (rad > 0.46) & (rad < 0.51)
            sub[orbit] = (0.86, 0.66, 0.26)
            sub[star] = (0.98, 0.80, 0.28)
            sub[rad < 0.12] = (0.28, 0.82, 0.76)

        arr[512:1024, x_start:x_end, :3] = sub

    img = bpy.data.images.new("VeronikaBeautyAtlas", width=W, height=H, alpha=False)
    img.pixels.foreach_set(arr.ravel())
    img.pack()
    return img


def make_veronika_mat(ga, key, atlas_img):
    """Create distinct PBR material for `key` with backface culling enabled so Blender Eevee matches Raylib OpenGL."""
    if key not in ga.MATS:
        lin = srgb_hex_to_linear(VERONIKA_PALETTE_HEX[key])
        if key in ("skin", "iris", "card"):
            lin = [1.0, 1.0, 1.0, 1.0]
        m = bpy.data.materials.new(key)
        m.diffuse_color = tuple(lin)
        m.use_backface_culling = True
        m.use_nodes = True
        tree = m.node_tree
        bsdf = tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = tuple(lin)
        met, rough, em = PBR_SPECS.get(key, (0.0, 0.50, None))
        bsdf.inputs["Metallic"].default_value = met
        bsdf.inputs["Roughness"].default_value = rough
        if "Specular IOR Level" in bsdf.inputs and key in ("silk", "plum", "hair", "trouser", "card", "skin"):
            bsdf.inputs["Specular IOR Level"].default_value = 0.18
        if em is not None and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (*em, 1.0)
            bsdf.inputs["Emission Strength"].default_value = 1.0
        if key in ("skin", "iris", "card"):
            tex = tree.nodes.new("ShaderNodeTexImage")
            tex.image = atlas_img
            tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        ga.MATS[key] = m
    return ga.MATS[key]


def fix_outward_normals(obj):
    """Guarantee 100% consistent outward-pointing normals on `obj` so OpenGL `GL_CULL_FACE` never culls front faces."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def assign_veronika_mat(ga, obj, name, key):
    obj.name = name
    atlas = build_veronika_beauty_atlas()
    obj.data.materials.clear()
    obj.data.materials.append(make_veronika_mat(ga, key, atlas))
    return fix_outward_normals(obj)


def v_mesh(ga, name, pts, faces, key):
    data = bpy.data.meshes.new(name)
    data.from_pydata([ga.xyz(p) for p in pts], [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    return assign_veronika_mat(ga, obj, name, key)


def add_loft_grid(ga, name, rings_pts, key, closed_u=True, cap_ends=True, solidify_thickness=0.0):
    n_rings = len(rings_pts)
    n_seg = len(rings_pts[0])
    # Compute Blender-space ring centers so we can verify 100% outward radial normals relative to the tube spine!
    ring_centers = [
        sum((V(ga.xyz(p)) for p in ring), V((0.0, 0.0, 0.0))) / float(len(ring))
        for ring in rings_pts
    ]
    pts = [p for ring in rings_pts for p in ring]
    faces = []
    face_ring_mids = []
    u_lim = n_seg if closed_u else (n_seg - 1)
    for k in range(n_rings - 1):
        mid_c = 0.5 * (ring_centers[k] + ring_centers[k + 1])
        for j in range(u_lim):
            nj = (j + 1) % n_seg
            faces.append((k * n_seg + j, k * n_seg + nj, (k + 1) * n_seg + nj, (k + 1) * n_seg + j))
            face_ring_mids.append(mid_c)

    # Check if the side quads point radially outward from their local ring center; if not, reverse winding!
    v_bl = [V(ga.xyz(p)) for p in pts]
    radial_score = 0.0
    for f, mid_c in zip(faces, face_ring_mids):
        p0, p1, p2, p3 = v_bl[f[0]], v_bl[f[1]], v_bl[f[2]], v_bl[f[3]]
        fn = (p1 - p0).cross(p3 - p0)
        fc = 0.25 * (p0 + p1 + p2 + p3)
        radial_score += fn.dot(fc - mid_c)

    flip = radial_score < 0.0
    if flip:
        faces = [tuple(reversed(f)) for f in faces]

    if closed_u and cap_ends:
        cap0 = tuple(range(n_seg)) if flip else tuple(reversed(range(n_seg)))
        base = (n_rings - 1) * n_seg
        cap1 = tuple(reversed(range(base, base + n_seg))) if flip else tuple(range(base, base + n_seg))
        faces.append(cap0)
        faces.append(cap1)

    data = bpy.data.meshes.new(name)
    data.from_pydata([ga.xyz(p) for p in pts], [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.name = name
    atlas = build_veronika_beauty_atlas()
    obj.data.materials.clear()
    obj.data.materials.append(make_veronika_mat(ga, key, atlas))
    if solidify_thickness > 0.0:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        sol = obj.modifiers.new("SolidVolume", "SOLIDIFY")
        sol.thickness = solidify_thickness
        sol.offset = -1.0
        bpy.ops.object.modifier_apply(modifier=sol.name)
        fix_outward_normals(obj)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def add_ribbon_loft(ga, name, spine_pts, widths, thicknesses, normals, key, subsurf_level=1):
    rings = []
    n_cs = 12
    for idx, p in enumerate(spine_pts):
        p_v = V(p)
        if idx == 0:
            tang = (V(spine_pts[1]) - p_v).normalized()
        elif idx == len(spine_pts) - 1:
            tang = (p_v - V(spine_pts[-2])).normalized()
        else:
            tang = (V(spine_pts[idx + 1]) - V(spine_pts[idx - 1])).normalized()
        n_v = V(normals[idx]).normalized()
        b_v = tang.cross(n_v).normalized()
        n_v = b_v.cross(tang).normalized()
        w = widths[idx]
        th = thicknesses[idx]
        ring = []
        for j in range(n_cs):
            a = j * math.tau / n_cs
            pt = p_v + b_v * (w * math.cos(a)) + n_v * (th * math.sin(a))
            ring.append((pt.x, pt.y, pt.z))
        rings.append(ring)
    obj = add_loft_grid(ga, name, rings, key, closed_u=True, cap_ends=True)
    if subsurf_level > 0:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        sub = obj.modifiers.new("RibbonSub", "SUBSURF")
        sub.levels = subsurf_level
        bpy.ops.object.modifier_apply(modifier=sub.name)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def smooth_oval(ga, name, p, radii, key, segs=32, rings=20, rot=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segs, ring_count=rings, location=ga.xyz(p))
    obj = bpy.context.object
    obj.scale = (radii[0], radii[2], radii[1])
    if any(abs(a) > 1e-5 for a in rot):
        obj.rotation_euler = Euler((rot[0], -rot[2], rot[1]), "XYZ")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return assign_veronika_mat(ga, obj, name, key)


def smooth_wire(ga, name, points, radius, key, closed=False, res=6):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = radius
    curve.bevel_resolution = res
    spline = curve.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for v, p in zip(spline.points, points):
        v.co = (*ga.xyz(p), 1.0)
    spline.use_cyclic_u = closed
    o = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(o)
    atlas = build_veronika_beauty_atlas()
    o.data.materials.append(make_veronika_mat(ga, key, atlas))
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.convert(target="MESH")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    o.select_set(False)
    return fix_outward_normals(o)


def smooth_box(ga, name, p, size, key, bevel=0.002, segments=2, rot_z=0.0, rot_x=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=ga.xyz(p))
    obj = bpy.context.object
    obj.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if abs(rot_x) > 1e-5 or abs(rot_z) > 1e-5:
        obj.rotation_euler = Euler((rot_x, 0.0, rot_z), "XYZ")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("SmoothBevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return assign_veronika_mat(ga, obj, name, key)


def build_stylized_head_mesh(ga, mouth_viseme="closed"):
    """
    Resculpt & Laplacian-relax the MPFB head mesh into the heart-shaped stylized beauty face
    from `veronika_target_concept_1795129376528.jpg`.
    When `mouth_viseme` is "slight", "open", or "round", applies a smooth natural jaw/chin drop
    and strictly-contained inner mouth recess behind the lips so skin never pokes through open vowels.
    """
    raw_faces = ga.BODY
    faces = []
    for face in raw_faces:
        fc = sum((ga.REST[i] for i in face), V()) / len(face)
        is_arm = abs(fc.x) > 0.19 and fc.y > 0.90
        if not is_arm and fc.y >= 1.404:
            faces.append(face)

    ids = sorted({i for face in faces for i in face})
    lookup = {old: i for i, old in enumerate(ids)}

    pts = []
    for i in ids:
        p = ga.REST[i].copy()
        # 1. Slender feminine neck column (1.404 <= y <= 1.458)
        if p.y < 1.458:
            nt = max(0.0, min(1.0, (1.458 - p.y) / 0.054))
            p.x *= 1.0 - 0.16 * nt
            p.z = 0.045 + (p.z - 0.045) * (1.0 - 0.14 * nt)
            if p.y < 1.418:
                bt = max(0.0, min(1.0, (1.418 - p.y) / 0.014))
                p.y = p.y * (1.0 - 0.75 * bt) + 1.412 * (0.75 * bt)
                p.x *= 1.0 - 0.08 * bt
                p.z = 0.045 + (p.z - 0.045) * (1.0 - 0.08 * bt)

        # 2. Heart-shaped oval jaw & softly rounded chin
        if 1.425 < p.y < 1.542:
            jaw_t = max(0.0, min(1.0, (1.542 - p.y) / 0.095))
            p.x *= 1.0 - 0.18 * (jaw_t ** 1.22)
            if p.y < 1.474 and p.z > 0.055:
                chin_lift = max(0.0, (1.474 - p.y) / 0.032)
                p.y += 0.0082 * (chin_lift ** 1.15)
            if 1.425 < p.y < 1.468 and abs(p.x) < 0.028 and 0.060 < p.z < 0.138:
                hyoid_lift = math.exp(-((p.x / 0.018) ** 2) - (((p.y - 1.448) / 0.016) ** 2))
                p.y += 0.0075 * hyoid_lift

        # 3. Youthful full apple cheeks & fill MakeHuman nasolabial hollows
        if 1.465 < p.y < 1.545 and p.z > 0.085:
            cheek = math.exp(-(((abs(p.x) - 0.038) / 0.025) ** 2) - (((p.y - 1.518) / 0.024) ** 2))
            naso = math.exp(-(((abs(p.x) - 0.020) / 0.014) ** 2) - (((p.y - 1.494) / 0.020) ** 2))
            p.z += 0.0068 * cheek + 0.0058 * naso
            p.x += math.copysign(0.0035 * cheek, p.x)

        # 4. Delicate slender stylized nose (1.500 < y < 1.552, |x| < 0.025, z > 0.132)
        if 1.498 < p.y < 1.555 and abs(p.x) < 0.026 and p.z > 0.132:
            nose_m = math.exp(-((p.x / 0.018) ** 2) - (((p.y - 1.520) / 0.024) ** 2))
            p.x *= 1.0 - 0.28 * nose_m
            tip_m = math.exp(-((p.x / 0.012) ** 2) - (((p.y - 1.508) / 0.009) ** 2))
            bridge_m = math.exp(-((p.x / 0.012) ** 2) - (((p.y - 1.534) / 0.012) ** 2))
            p.y += 0.0024 * tip_m
            p.z -= 0.0020 * bridge_m

        # 5. Warm upturned smiling mouth corners
        if 1.468 < p.y < 1.495 and abs(p.x) < 0.028 and p.z > 0.135:
            smile_corner = math.exp(-(((abs(p.x) - 0.0175) / 0.0085) ** 2) - (((p.y - 1.481) / 0.009) ** 2))
            p.y += 0.0032 * smile_corner
            p.z += 0.0018 * smile_corner

        # 6. Open & smooth the eye socket region around (±0.0325, 1.5465)
        if 1.525 < p.y < 1.568 and p.z > 0.105 and 0.010 < abs(p.x) < 0.058:
            dx_e = (abs(p.x) - 0.0325) / 0.022
            dy_e = (p.y - 1.5465) / 0.014
            r2_e = dx_e * dx_e + dy_e * dy_e
            if r2_e < 1.35:
                w_e = math.exp(-r2_e * 1.6)
                p.z = p.z * (1.0 - 0.50 * w_e) + 0.125 * (0.50 * w_e)

        pts.append(p)

    reindexed_faces = [tuple(lookup[i] for i in face) for face in faces]

    adj = [set() for _ in range(len(pts))]
    for f in reindexed_faces:
        for k in range(len(f)):
            a, b = f[k], f[(k + 1) % len(f)]
            adj[a].add(b)
            adj[b].add(a)

    for _ in range(5):
        new_pts = [p.copy() for p in pts]
        for idx, p in enumerate(pts):
            if p.y > 1.442 and p.z > 0.065:
                is_nose_ridge = abs(p.x) < 0.011 and 1.502 < p.y < 1.542 and p.z > 0.152
                if not is_nose_ridge and adj[idx]:
                    avg = sum((pts[n] for n in adj[idx]), V()) / len(adj[idx])
                    new_pts[idx] = p.lerp(avg, 0.42)
        pts = new_pts

    # Smooth natural jaw/chin drop & strictly bounded inner mouth recess for talking visemes
    if mouth_viseme != "closed":
        vis_cfg = {
            "slight": (0.0018, 0.0005, 1.4802, 0.0145, 0.0048, 0.0095, 0.0000, 0.0000),
            "open":   (0.0045, 0.0015, 1.4774, 0.0155, 0.0088, 0.0135, 0.0000, 0.0000),
            "round":  (0.0028, 0.0008, 1.4792, 0.0098, 0.0066, 0.0115, 0.0024, 0.0020),
        }[mouth_viseme]
        jaw_drop, chin_back, cav_cy, cav_rx, cav_ry, cav_depth, pucker_narrow, pucker_fwd = vis_cfg
        for p in pts:
            # 1) Smooth mandible/chin rotation downward (neck base y <= 1.425 stays 100% locked!)
            if 1.425 < p.y < 1.486 and p.z > 0.075:
                if p.y < 1.454:
                    wy = (p.y - 1.425) / (1.454 - 1.425)
                else:
                    wy = max(0.0, (1.486 - p.y) / (1.486 - 1.454))
                wy = wy * wy * (3.0 - 2.0 * wy)
                wx = math.exp(-((p.x / 0.040) ** 2))
                wz = max(0.0, min(1.0, (p.z - 0.075) / 0.055))
                w_jaw = wy * wx * wz
                p.y -= jaw_drop * w_jaw
                p.z -= chin_back * w_jaw

            # 2) Subtle lip-corner gather & forward cushion for rounded O/OO vowels
            if pucker_narrow > 0.0 and 1.466 < p.y < 1.494 and p.z > 0.132:
                w_corner = math.exp(-(((abs(p.x) - 0.0155) / 0.0085) ** 2) - (((p.y - 1.480) / 0.011) ** 2))
                p.x -= math.copysign(pucker_narrow * w_corner, p.x)
                w_puck = math.exp(-((p.x / 0.0135) ** 2) - (((p.y - 1.480) / 0.011) ** 2))
                p.z += pucker_fwd * w_puck

            # 3) Strictly contained inner mouth recess behind the lip opening (zero cheek bleed!)
            if p.z > 0.134 and abs(p.x) < cav_rx * 1.35 and abs(p.y - cav_cy) < cav_ry * 1.45:
                dx = p.x / cav_rx
                dy = (p.y - cav_cy) / cav_ry
                r2 = dx * dx + dy * dy
                if r2 < 1.55:
                    w_cav = math.exp(-r2 * 2.0)
                    p.z -= cav_depth * w_cav

    obj = v_mesh(ga, "VeronikaStylizedHead", pts, reindexed_faces, "skin")

    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    sub = obj.modifiers.new("HeadSubsurf", "SUBSURF")
    sub.levels = 1
    bpy.ops.object.modifier_apply(modifier=sub.name)
    return fix_outward_normals(obj)


def add_curved_mouth_cavity(ga, name, rx, ry, cy, z_front, z_curve, thickness=0.0028, key="mouth_cavity"):
    """Build a watertight curved 3D inner mouth backdrop strictly enclosed by the lip contour."""
    n_u = 28
    n_v = 10
    rings = []
    for kv in range(n_v):
        tv = kv / (n_v - 1)
        # Taper cleanly from back wall -> front inner rim -> closed center so normals are 100% outward
        ang = tv * math.pi
        rad_s = max(0.06, math.sin(ang))
        z_off = -thickness * 0.5 + thickness * math.cos(ang) * 0.5
        ring = []
        for ku in range(n_u):
            a = ku * math.tau / n_u
            ca, sa = math.cos(a), math.sin(a)
            x = rx * rad_s * ca
            y = cy + ry * rad_s * sa
            z = z_front - z_curve * (x * x) + z_off
            ring.append((x, y, z))
        rings.append(ring)
    return add_loft_grid(ga, name, rings, key, closed_u=True, cap_ends=True)


def add_curved_upper_teeth(ga, name, half_w, cy, z_front, z_curve, half_h=0.0020, thickness=0.0014):
    """Build a clean, feminine, softly curved ivory upper teeth arch recessed neatly behind the upper lip."""
    n_pts = 9
    pts = []
    widths = []
    thicknesses = []
    normals = []
    for i in range(n_pts):
        u = (i / (n_pts - 1)) * 2.0 - 1.0  # -1..+1
        x = half_w * u
        # Gentle dental smile arc: center incisors slightly lower/fuller, tapering gracefully at sides
        y = cy + 0.0007 * (u * u)
        z = z_front - z_curve * (x * x)
        pts.append((x, y, z))
        taper = max(0.38, 1.0 - 0.52 * (u * u))
        widths.append(half_h * taper)
        thicknesses.append(thickness * taper)
        normals.append((u * 0.25, -0.05, 1.0))
    return add_ribbon_loft(ga, name, pts, widths=widths, thicknesses=thicknesses, normals=normals, key="teeth", subsurf_level=1)


def build_veronika_head_accessories(ga, blink=False, mouth_viseme="closed"):
    """
    Build the exact head features from `veronika_target_concept_1795129376528.jpg`:
      - Smooth 3D sculpted Cupid's-bow smiling berry-crimson lips (or talking viseme: slight / open / round)
      - 1.55x HUGE wide-open luminous violet-amethyst almond eyes
      - Bold sculpted jet-black winged cat-eye eyeliner + full curled upper lashes + lower-seated dark-espresso arched brows
      - WATER-TIGHT SOLIDIFIED (`9mm` thick) dark-espresso wavy hair crown with sweeping curtain bangs framing the forehead
        and cheekbones (100% outward normals verified for OpenGL `GL_CULL_FACE`!)
    """
    smooth_oval(ga, "NeckPivotSocketBall", (0.0, 1.412, 0.038), (0.024, 0.018, 0.024), "skin", segs=24, rings=16)

    # 1) RAZOR-SMOOTH 3D SCULPTED CUPID'S-BOW BERRY-ROSE LIPS (`lip`) + OPTIONAL TALKING VISEME CAVITY & TEETH
    if mouth_viseme == "closed":
        upper_lip_pts = [
            (-0.0196, 1.4832, 0.1495),
            (-0.0128, 1.4838, 0.1560),
            (-0.0054, 1.4858, 0.1598),
            ( 0.0000, 1.4846, 0.1604),
            ( 0.0054, 1.4858, 0.1598),
            ( 0.0128, 1.4838, 0.1560),
            ( 0.0196, 1.4832, 0.1495),
        ]
        upper_w = [0.0012, 0.0028, 0.0040, 0.0036, 0.0040, 0.0028, 0.0012]
        upper_t = [0.0012, 0.0026, 0.0036, 0.0034, 0.0036, 0.0026, 0.0012]
        lower_lip_pts = [
            (-0.0190, 1.4828, 0.1492),
            (-0.0120, 1.4792, 0.1558),
            (-0.0052, 1.4774, 0.1596),
            ( 0.0000, 1.4770, 0.1604),
            ( 0.0052, 1.4774, 0.1596),
            ( 0.0120, 1.4792, 0.1558),
            ( 0.0190, 1.4828, 0.1492),
        ]
        lower_w = [0.0011, 0.0032, 0.0046, 0.0050, 0.0046, 0.0032, 0.0011]
        lower_t = [0.0011, 0.0028, 0.0040, 0.0042, 0.0040, 0.0028, 0.0011]
    elif mouth_viseme == "slight":
        # Slightly parted lips (~4.5-5mm opening) for consonants / narrow vowels (И/Е/I/E)
        upper_lip_pts = [
            (-0.0202, 1.4824, 0.1494),
            (-0.0130, 1.4845, 0.1560),
            (-0.0055, 1.4868, 0.1598),
            ( 0.0000, 1.4858, 0.1605),
            ( 0.0055, 1.4868, 0.1598),
            ( 0.0130, 1.4845, 0.1560),
            ( 0.0202, 1.4824, 0.1494),
        ]
        upper_w = [0.0012, 0.0026, 0.0036, 0.0033, 0.0036, 0.0026, 0.0012]
        upper_t = [0.0012, 0.0025, 0.0035, 0.0033, 0.0035, 0.0025, 0.0012]
        lower_lip_pts = [
            (-0.0196, 1.4820, 0.1492),
            (-0.0124, 1.4768, 0.1556),
            (-0.0054, 1.4742, 0.1592),
            ( 0.0000, 1.4736, 0.1598),
            ( 0.0054, 1.4742, 0.1592),
            ( 0.0124, 1.4768, 0.1556),
            ( 0.0196, 1.4820, 0.1492),
        ]
        lower_w = [0.0011, 0.0029, 0.0040, 0.0044, 0.0040, 0.0029, 0.0011]
        lower_t = [0.0011, 0.0026, 0.0036, 0.0038, 0.0036, 0.0026, 0.0011]
        add_curved_mouth_cavity(
            ga, "MouthInnerCavitySlight",
            rx=0.0158, ry=0.0046, cy=1.4800, z_front=0.1544, z_curve=20.0, thickness=0.0032,
        )
        add_curved_upper_teeth(
            ga, "UpperTeethSlight",
            half_w=0.0112, cy=1.4818, z_front=0.1566, z_curve=24.0, half_h=0.0017, thickness=0.0013,
        )
    elif mouth_viseme == "open":
        # Expressive open vowel mouth (~11-13mm opening) with natural jaw drop (А/Я/Э/AH/AY)
        upper_lip_pts = [
            (-0.0194, 1.4806, 0.1494),
            (-0.0125, 1.4852, 0.1560),
            (-0.0054, 1.4882, 0.1600),
            ( 0.0000, 1.4872, 0.1606),
            ( 0.0054, 1.4882, 0.1600),
            ( 0.0125, 1.4852, 0.1560),
            ( 0.0194, 1.4806, 0.1494),
        ]
        upper_w = [0.0013, 0.0025, 0.0035, 0.0032, 0.0035, 0.0025, 0.0013]
        upper_t = [0.0012, 0.0024, 0.0034, 0.0032, 0.0034, 0.0024, 0.0012]
        lower_lip_pts = [
            (-0.0188, 1.4802, 0.1492),
            (-0.0120, 1.4724, 0.1548),
            (-0.0055, 1.4682, 0.1580),
            ( 0.0000, 1.4672, 0.1586),
            ( 0.0055, 1.4682, 0.1580),
            ( 0.0120, 1.4724, 0.1548),
            ( 0.0188, 1.4802, 0.1492),
        ]
        lower_w = [0.0012, 0.0028, 0.0040, 0.0043, 0.0040, 0.0028, 0.0012]
        lower_t = [0.0012, 0.0026, 0.0036, 0.0038, 0.0036, 0.0026, 0.0012]
        add_curved_mouth_cavity(
            ga, "MouthInnerCavityOpen",
            rx=0.0162, ry=0.0084, cy=1.4776, z_front=0.1536, z_curve=22.0, thickness=0.0042,
        )
        add_curved_upper_teeth(
            ga, "UpperTeethOpen",
            half_w=0.0118, cy=1.4828, z_front=0.1558, z_curve=26.0, half_h=0.0021, thickness=0.0013,
        )
    elif mouth_viseme == "round":
        # Rounded oval pursed lips (~8mm height, narrower width) for rounded vowels (О/У/O/OO)
        upper_lip_pts = [
            (-0.0130, 1.4795, 0.1552),
            (-0.0102, 1.4848, 0.1592),
            (-0.0048, 1.4879, 0.1620),
            ( 0.0000, 1.4874, 0.1627),
            ( 0.0048, 1.4879, 0.1620),
            ( 0.0102, 1.4848, 0.1592),
            ( 0.0130, 1.4795, 0.1552),
        ]
        upper_w = [0.0019, 0.0031, 0.0039, 0.0038, 0.0039, 0.0031, 0.0019]
        upper_t = [0.0019, 0.0029, 0.0037, 0.0037, 0.0037, 0.0029, 0.0019]
        lower_lip_pts = [
            (-0.0128, 1.4794, 0.1550),
            (-0.0100, 1.4742, 0.1588),
            (-0.0047, 1.4716, 0.1614),
            ( 0.0000, 1.4710, 0.1620),
            ( 0.0047, 1.4716, 0.1614),
            ( 0.0100, 1.4742, 0.1588),
            ( 0.0128, 1.4794, 0.1550),
        ]
        lower_w = [0.0019, 0.0033, 0.0043, 0.0045, 0.0043, 0.0033, 0.0019]
        lower_t = [0.0019, 0.0031, 0.0039, 0.0041, 0.0039, 0.0031, 0.0019]
        add_curved_mouth_cavity(
            ga, "MouthInnerCavityRound",
            rx=0.0102, ry=0.0062, cy=1.4794, z_front=0.1558, z_curve=26.0, thickness=0.0038,
        )
    else:
        raise ValueError(f"Unknown mouth_viseme: {mouth_viseme}")

    add_ribbon_loft(
        ga,
        "SculptedUpperSmilingLip",
        upper_lip_pts,
        widths=upper_w,
        thicknesses=upper_t,
        normals=[(0.0, 0.20, 1.0)] * 7,
        key="lip",
        subsurf_level=1,
    )
    add_ribbon_loft(
        ga,
        "SculptedLowerSmilingLip",
        lower_lip_pts,
        widths=lower_w,
        thicknesses=lower_t,
        normals=[(0.0, -0.20, 1.0)] * 7,
        key="lip",
        subsurf_level=1,
    )

    # 2) HUGE 1.55x WIDE-OPEN STYLIZED LUMINOUS VIOLET-AMETHYST EYES (OR CLOSED BLINKING EYELIDS)
    for side in (-1, 1):
        ex = side * 0.0326
        ey = 1.5465
        ez = 0.1332

        if not blink:
            # Almond Sclera Eyeball (vertical radius 0.0092 so NO white sclera ever peeks above the black winged eyeliner!)
            smooth_oval(
                ga,
                f"EyeSclera_{side}",
                (ex, ey, ez),
                (0.0156, 0.0092, 0.0142),
                "ivory",
                segs=36,
                rings=24,
            )

            ix = ex - side * 0.0006
            iy = ey + 0.0002
            iz = ez + 0.0122
            iris_obj = smooth_oval(
                ga,
                f"EyeIris_{side}",
                (ix, iy, iz),
                (0.0090, 0.0088, 0.0032),
                "iris",
                segs=36,
                rings=18,
            )
            iris_obj["is_iris"] = True

            smooth_oval(
                ga,
                f"EyePupil_{side}",
                (ix, iy, iz + 0.0019),
                (0.0038, 0.0038, 0.0015),
                "onyx",
                segs=28,
                rings=14,
            )

            smooth_oval(
                ga,
                f"EyeCatchlightMain_{side}",
                (ix - 0.0027, iy + 0.0026, iz + 0.0036),
                (0.0022, 0.0020, 0.0009),
                "catchlight",
                segs=20,
                rings=12,
            )
            smooth_oval(
                ga,
                f"EyeCatchlightSub_{side}",
                (ix + 0.0024, iy - 0.0023, iz + 0.0035),
                (0.0011, 0.0011, 0.0007),
                "catchlight",
                segs=16,
                rings=10,
            )

            # BOLD SCULPTED WINGED CAT-EYE EYELINER & UPPER EYELASH SWEEP (`onyx`) capping the entire upper eye rim
            wing_pts = [
                (ex - side * 0.0148, ey - 0.0016, ez + 0.0088),
                (ex - side * 0.0074, ey + 0.0066, ez + 0.0145),
                (ex,                 ey + 0.0082, ez + 0.0156),
                (ex + side * 0.0082, ey + 0.0068, ez + 0.0142),
                (ex + side * 0.0154, ey + 0.0014, ez + 0.0090),
                (ex + side * 0.0206, ey + 0.0052, ez + 0.0054),
            ]
            add_ribbon_loft(
                ga,
                f"UpperEyelashWing_{side}",
                wing_pts,
                widths=[0.0016, 0.0032, 0.0040, 0.0045, 0.0038, 0.0010],
                thicknesses=[0.0016, 0.0028, 0.0032, 0.0036, 0.0030, 0.0010],
                normals=[(side * 0.15, 0.45, 1.0)] * 6,
                key="onyx",
                subsurf_level=1,
            )

            crease_pts = [
                (ex - side * 0.0135, ey + 0.0022, ez + 0.0092),
                (ex - side * 0.0068, ey + 0.0096, ez + 0.0140),
                (ex,                 ey + 0.0110, ez + 0.0148),
                (ex + side * 0.0078, ey + 0.0096, ez + 0.0136),
                (ex + side * 0.0148, ey + 0.0044, ez + 0.0086),
            ]
            smooth_wire(ga, f"UpperEyelidCrease_{side}", crease_pts, 0.0014, "eyeshadow", res=6)

            lower_rim_pts = [
                (ex - side * 0.0138, ey - 0.0020, ez + 0.0088),
                (ex - side * 0.0060, ey - 0.0056, ez + 0.0138),
                (ex + side * 0.0060, ey - 0.0054, ez + 0.0136),
                (ex + side * 0.0148, ey + 0.0002, ez + 0.0084),
            ]
            smooth_wire(ga, f"LowerEyelidRim_{side}", lower_rim_pts, 0.0018, "skin", res=6)
        else:
            smooth_oval(
                ga,
                f"ClosedEyelidDome_{side}",
                (ex, ey, ez + 0.0022),
                (0.0156, 0.0124, 0.0144),
                "eyeshadow",
                segs=36,
                rings=22,
            )
            closed_wing_pts = [
                (ex - side * 0.0145, ey - 0.0016, ez + 0.0095),
                (ex - side * 0.0072, ey - 0.0044, ez + 0.0152),
                (ex,                 ey - 0.0050, ez + 0.0162),
                (ex + side * 0.0080, ey - 0.0040, ez + 0.0148),
                (ex + side * 0.0152, ey + 0.0002, ez + 0.0092),
                (ex + side * 0.0198, ey + 0.0038, ez + 0.0056),
            ]
            add_ribbon_loft(
                ga,
                f"ClosedEyelashCrescent_{side}",
                closed_wing_pts,
                widths=[0.0013, 0.0026, 0.0034, 0.0038, 0.0032, 0.0008],
                thicknesses=[0.0013, 0.0022, 0.0026, 0.0029, 0.0025, 0.0008],
                normals=[(side * 0.15, -0.35, 1.0)] * 6,
                key="onyx",
                subsurf_level=1,
            )
            crease_pts = [
                (ex - side * 0.0135, ey + 0.0024, ez + 0.0102),
                (ex,                 ey + 0.0072, ez + 0.0150),
                (ex + side * 0.0142, ey + 0.0024, ez + 0.0096),
            ]
            smooth_wire(ga, f"ClosedUpperLidCrease_{side}", crease_pts, 0.0014, "plum", res=5)

        # Bold Expressive Arched Dark-Espresso Eyebrow seated closer to the eye (`y = 1.561 -> 1.571`)
        brow_pts = [
            (side * 0.0105, 1.5612, 0.1522),
            (side * 0.0230, 1.5685, 0.1518),
            (side * 0.0372, 1.5718, 0.1472),
            (side * 0.0485, 1.5672, 0.1400),
            (side * 0.0568, 1.5602, 0.1315),
        ]
        add_ribbon_loft(
            ga,
            f"SculptedEyebrow_{side}",
            brow_pts,
            widths=[0.0030, 0.0038, 0.0035, 0.0025, 0.0010],
            thicknesses=[0.0022, 0.0026, 0.0025, 0.0019, 0.0008],
            normals=[(side * 0.2, 0.25, 1.0)] * 5,
            key="hair",
            subsurf_level=1,
        )

    # 3) SOLID WATER-TIGHT (`solidify_thickness = 0.010`) VOLUMINOUS DARK-ESPRESSO WAVY HAIR CROWN & INTEGRATED CURTAIN BANGS
    n_h = 72
    hair_rings = []
    for k in range(25, -1, -1):
        v = k / 25.0
        ang_v = v * math.pi * 0.80
        sv, cv = math.sin(ang_v), math.cos(ang_v)
        ring = []
        for i in range(n_h):
            a = i * math.tau / n_h
            sa, ca = math.sin(a), math.cos(a)
            part_crease = 0.0040 * math.exp(-((sa / 0.09) ** 2)) * max(0.0, ca) * math.sin(v * math.pi)
            crown_fullness = 0.012 * (abs(sa) ** 0.85) * math.sin(v * math.pi * 0.85)
            wave = 1.0 + 0.018 * math.sin(a * 16.0 + v * 6.0) * sv + 0.010 * math.cos(a * 10.0 - v * 4.5) * sv

            front_window = max(0.0, (ca - 0.18) / 0.82) * max(0.0, (v - 0.30) / 0.70)
            curtain_arch = 0.96 * math.exp(-((sa / 0.34) ** 2)) + 0.18 * math.exp(-(((abs(sa) - 0.56) / 0.22) ** 2))
            y_base = 1.582 + 0.099 * cv + crown_fullness - part_crease + 0.082 * (front_window ** 1.15) * curtain_arch

            temple_bang_swell = 0.013 * math.exp(-(((abs(sa) - 0.62) / 0.24) ** 2)) * max(0.0, ca) * (sv ** 1.2)
            rx = (0.093 * sv + temple_bang_swell) * wave
            rz = (0.111 * sv + 0.55 * temple_bang_swell) * wave - 0.015 * (front_window ** 1.3)
            ring.append((rx * sa, y_base, 0.050 + rz * ca))
        hair_rings.append(ring)
    add_loft_grid(ga, "VoluminousHairCrown", hair_rings, "hair", closed_u=True, cap_ends=False, solidify_thickness=0.010)

    # Continuous Sculpted Wavy Upper-Neck Hair Mantle behind the ears & neck (ordered bottom-to-top + recalc_face_normals)
    n_u_mantle = 36
    closed_mantle_rings = []
    for kv in range(13, -1, -1):
        tv = kv / 13.0
        y = 1.575 - 0.185 * tv
        ring = []
        for ku in range(n_u_mantle):
            tu = ku / (n_u_mantle - 1)
            ang = math.pi * 0.42 + tu * math.pi * 1.16
            sa, ca = math.sin(ang), math.cos(ang)
            s_wave = 0.008 * math.sin(tv * math.tau * 1.6 + tu * math.tau * 3.0)
            rx = (0.090 + 0.014 * math.sin(tv * math.pi * 0.85)) + s_wave
            rz = (0.086 + 0.012 * tv) + s_wave
            ring.append((rx * sa, y, 0.042 + rz * ca))
        inner_ring = [(p[0] * 0.84, p[1], 0.042 + (p[2] - 0.042) * 0.82) for p in reversed(ring)]
        closed_mantle_rings.append(ring + inner_ring)
    add_loft_grid(ga, "WavyNeckHairMantle", closed_mantle_rings, "hair", closed_u=True, cap_ends=True)

    # Half-Up Twisted Back Chignon Bun
    smooth_oval(ga, "SculptedChignonBun", (0.0, 1.588, -0.064), (0.064, 0.058, 0.054), "hair", segs=36, rings=24)
    bun_twist_pts = [
        (0.052 * math.cos(i * math.tau / 28), 1.588 + 0.046 * math.sin(i * math.tau / 28), -0.054 + 0.008 * math.sin(i * math.tau * 3 / 28))
        for i in range(28)
    ]
    smooth_wire(ga, "ChignonTwistCoil", bun_twist_pts, 0.013, "hairlight", closed=True, res=6)

    # 4) DELICATE CURVED GOLD FOREHEAD U-CHAIN (tucked cleanly under the center-part curtain bangs!) + EMERALD TEARDROP GEM
    forehead_v_pts = [
        (-0.022, 1.602, 0.151),
        (-0.015, 1.594, 0.156),
        (-0.008, 1.589, 0.1585),
        ( 0.000, 1.587, 0.1592),
        ( 0.008, 1.589, 0.1585),
        ( 0.015, 1.594, 0.156),
        ( 0.022, 1.602, 0.151),
    ]
    smooth_wire(ga, "GoldForeheadChain", forehead_v_pts, 0.0013, "gold", closed=False, res=6)
    smooth_oval(ga, "ForeheadGemSetting", (0.0, 1.579, 0.1578), (0.0042, 0.0065, 0.0028), "gold", segs=22, rings=14)
    smooth_oval(ga, "ForeheadEmeraldDrop", (0.0, 1.579, 0.1590), (0.0032, 0.0052, 0.0026), "teal", segs=22, rings=14)


def chest_skin_z(x, y):
    t = max(0.0, min(1.0, (1.415 - y) / 0.230))
    z_center = 0.088 + 0.052 * (t ** 0.88)
    chest_curve = -0.22 * (x * x)
    return z_center + chest_curve


def build_veronika_slender_body(ga):
    """
    Build `char_hr_veronika_body.glb` matching `veronika_target_concept_1795129376528.jpg`:
      - Seamless neck-to-collarbone transition at y = 1.415
      - Narrowed `DecolletageSkinColumn` strictly inside the V-neck opening + recessed outer edges so ZERO Z-fighting with lapels!
      - Shawl lapels offset cleanly +6mm forward in Z with zero chevron artifacts!
      - Smooth sloped feminine shoulders (NO separate bulging ball-joint spheres on torso!)
      - 0.0mm FLUSH Gold Y-Lariat Necklace
    """
    # 1) Warm Peach-Tan Neck Base & Décolletage V-Neck Skin Column
    # Strictly confined to the V-neck opening below y=1.39 so it NEVER intersects or Z-fights the outer blazer or lapels!
    n_u, n_v = 32, 24
    skin_rings = []
    for kv in range(n_v):
        tv = kv / (n_v - 1)
        y = 1.185 + (1.426 - 1.185) * tv
        if y < 1.385:
            # Narrow V-wedge width (0.022 at y=1.185 -> 0.056 at y=1.385), staying well inside the lapels!
            vt = (y - 1.185) / (1.385 - 1.185)
            half_w = 0.024 + 0.032 * vt
        elif y < 1.406:
            nt = (y - 1.385) / (1.406 - 1.385)
            half_w = 0.056 + (0.044 - 0.056) * nt
        else:
            ut = (y - 1.406) / (1.426 - 1.406)
            half_w = 0.044 - 0.009 * ut
        ring = []
        for ku in range(n_u):
            a = ku * math.tau / n_u
            sa, ca = math.sin(a), math.cos(a)
            x = half_w * sa
            if ca >= 0.0:
                z_front = chest_skin_z(x, min(y, 1.408))
                if y > 1.406:
                    z_front -= 0.008 * ((y - 1.406) / 0.020)
                z = 0.044 + (z_front - 0.044) * (ca ** 0.88)
            else:
                z = 0.044 + 0.040 * ca
            ring.append((x, y, z))
        skin_rings.append(ring)
    add_loft_grid(ga, "DecolletageSkinColumn", skin_rings, "skin", closed_u=True, cap_ends=False)

    # 2) 0.0mm FLUSH Gold Y-Lariat Necklace resting directly on the décolletage skin surface
    v_chain_pts = []
    for k in range(25):
        u = (k / 24.0) * 2.0 - 1.0
        x = u * 0.046
        y = 1.288 + 0.098 * (abs(u) ** 1.18)
        z = chest_skin_z(x, y) + 0.0016
        v_chain_pts.append((x, y, z))
    smooth_wire(ga, "NecklaceGoldVChain", v_chain_pts, 0.0016, "gold", closed=False, res=6)

    smooth_oval(ga, "NecklaceLariatBead", (0.0, 1.288, chest_skin_z(0.0, 1.288) + 0.0024), (0.0026, 0.0036, 0.0024), "gold", segs=18, rings=12)
    lariat_drop_pts = [
        (0.0, float(y), chest_skin_z(0.0, float(y)) + 0.0016)
        for y in np.linspace(1.288, 1.204, 10)
    ]
    smooth_wire(ga, "NecklaceGoldLariatDrop", lariat_drop_pts, 0.0014, "gold", closed=False, res=6)

    # 3) Slender Tailored Deep Wine-Plum Velvet Wrap Blazer (`SlenderVelvetWrapBlazer`, 64 segs x 28 height rings)
    n_b = 64
    b_table = [
        (0.800, 0.160, 0.118, 0.110, 0.032),
        (0.865, 0.154, 0.114, 0.106, 0.032),
        (0.930, 0.140, 0.104, 0.096, 0.034),
        (1.000, 0.120, 0.095, 0.086, 0.036),
        (1.060, 0.108, 0.093, 0.084, 0.038),
        (1.135, 0.118, 0.106, 0.088, 0.042),
        (1.215, 0.132, 0.118, 0.090, 0.042),
        (1.275, 0.148, 0.100, 0.084, 0.034),
        (1.312, 0.156, 0.084, 0.078, 0.032),
        (1.348, 0.132, 0.072, 0.068, 0.034),
        (1.392, 0.060, 0.054, 0.054, 0.040),
    ]
    b_rings = []
    for k in range(28):
        t = k / 27.0
        h = 0.800 + (1.392 - 0.800) * t
        rx, rzf, rzb, cz = b_table[-1][1:]
        for row0, row1 in zip(b_table, b_table[1:]):
            if row0[0] <= h <= row1[0]:
                u = (h - row0[0]) / (row1[0] - row0[0])
                us = u * u * (3.0 - 2.0 * u)
                rx = row0[1] + (row1[1] - row0[1]) * us
                rzf = row0[2] + (row1[2] - row0[2]) * us
                rzb = row0[3] + (row1[3] - row0[3]) * us
                cz = row0[4] + (row1[4] - row0[4]) * us
                break
        ring = []
        for i in range(n_b):
            a = i * math.tau / n_b
            sa, ca = math.sin(a), math.cos(a)
            x = rx * sa
            rz = rzf if ca >= 0.0 else rzb
            pleat = 1.0 + 0.008 * math.sin(a * 10.0 + (h - 1.06) * 16.0) * max(0.0, ca)
            z = cz + rz * ca * pleat
            y_pt = h
            if k < 4 and ca > 0.35:
                hem_notch = 0.036 * max(0.0, 1.0 - abs(sa - 0.10) / 0.35) * (1.0 - k / 4.0)
                y_pt += hem_notch
            if h > 1.195 and ca > 0.45:
                v_half_w = 0.004 + 0.052 * ((h - 1.195) / (1.392 - 1.195))
                if abs(x) < v_half_w:
                    recess = 1.0 - (abs(x) / v_half_w)
                    z = min(z, chest_skin_z(x, h) - 0.012 * recess)
            ring.append((x, y_pt, z))
        b_rings.append(ring)
    add_loft_grid(ga, "SlenderVelvetWrapBlazer", b_rings, "silk", closed_u=True, cap_ends=True)

    # 4) Sculpted Flat-Profile Velvet Shawl Wrap Lapels seated cleanly +6mm/+9mm forward in Z (ZERO Z-fighting!)
    right_lapel_pts = [
        (-0.048, 1.390, 0.072),
        (-0.068, 1.342, 0.114),
        (-0.052, 1.275, 0.146),
        (-0.022, 1.215, 0.158),
        ( 0.005, 1.170, 0.154),
    ]
    add_ribbon_loft(
        ga,
        "RightShawlWrapLapel",
        right_lapel_pts,
        widths=[0.011, 0.020, 0.022, 0.018, 0.010],
        thicknesses=[0.0035, 0.0045, 0.0045, 0.0038, 0.0030],
        normals=[(-0.15, 0.10, 0.98)] * 5,
        key="plum",
        subsurf_level=1,
    )

    left_lapel_pts = [
        ( 0.048, 1.390, 0.072),
        ( 0.068, 1.342, 0.114),
        ( 0.052, 1.275, 0.148),
        ( 0.020, 1.210, 0.163),
        (-0.018, 1.140, 0.157),
        (-0.058, 1.062, 0.138),
        (-0.075, 0.960, 0.142),
        (-0.082, 0.878, 0.146),
    ]
    add_ribbon_loft(
        ga,
        "LeftCrossOverWrapLapel",
        left_lapel_pts,
        widths=[0.011, 0.020, 0.023, 0.021, 0.018, 0.015, 0.013, 0.009],
        thicknesses=[0.0035, 0.0045, 0.0050, 0.0045, 0.0042, 0.0038, 0.0035, 0.0028],
        normals=[(0.12, 0.10, 0.98)] * 8,
        key="plum",
        subsurf_level=1,
    )

    # 5) Continuous Sculpted Wavy Back/Shoulder Hair Cascade Mantle (ordered bottom-to-top + recalc_face_normals!)
    n_u_back = 36
    back_mantle_rings = []
    for kv in range(15, -1, -1):
        tv = kv / 15.0
        y = 1.425 - 0.245 * tv
        ring = []
        for ku in range(n_u_back):
            tu = ku / (n_u_back - 1)
            ang = math.pi * 0.46 + tu * math.pi * 1.08
            sa, ca = math.sin(ang), math.cos(ang)
            wave = 0.010 * math.sin(tv * math.tau * 2.0 + tu * math.tau * 4.0)
            rx = (0.100 + 0.042 * math.sin(tv * math.pi * 0.75)) + wave
            rz = (0.088 + 0.026 * math.sin(tv * math.pi * 0.80)) + wave
            ring.append((rx * sa, y, 0.035 + rz * ca))
        inner_ring = [(p[0] * 0.85, p[1], 0.035 + (p[2] - 0.035) * 0.82) for p in reversed(ring)]
        back_mantle_rings.append(ring + inner_ring)
    add_loft_grid(ga, "BackWavyShoulderHairMantle", back_mantle_rings, "hair", closed_u=True, cap_ends=True)

    # 6) Slim Tailored Dark Charcoal-Plum Trousers & Sleek Heeled Ankle Boots
    for side in (-1, 1):
        leg_rings = []
        n_leg = 28
        leg_profile = [
            (0.145, side * 0.072, 0.010, 0.038, 0.042),
            (0.280, side * 0.074, 0.012, 0.044, 0.048),
            (0.460, side * 0.076, 0.018, 0.052, 0.056),
            (0.660, side * 0.078, 0.024, 0.066, 0.070),
            (0.840, side * 0.074, 0.030, 0.078, 0.084),
            (0.910, side * 0.068, 0.032, 0.076, 0.080),
        ]
        for k in range(18):
            t = k / 17.0
            y = 0.145 + (0.910 - 0.145) * t
            xc, zc, rx, rz = leg_profile[-1][1:]
            for r0, r1 in zip(leg_profile, leg_profile[1:]):
                if r0[0] <= y <= r1[0]:
                    u = (y - r0[0]) / (r1[0] - r0[0])
                    us = u * u * (3.0 - 2.0 * u)
                    xc = r0[1] + (r1[1] - r0[1]) * us
                    zc = r0[2] + (r1[2] - r0[2]) * us
                    rx = r0[3] + (r1[3] - r0[3]) * us
                    rz = r0[4] + (r1[4] - r0[4]) * us
                    break
            ring = [
                (xc + rx * math.sin(j * math.tau / n_leg), y, zc + rz * math.cos(j * math.tau / n_leg))
                for j in range(n_leg)
            ]
            leg_rings.append(ring)
        add_loft_grid(ga, f"SlimTailoredTrouserLeg_{side}", leg_rings, "trouser", closed_u=True, cap_ends=True)

        boot_rings = [
            [(side * 0.072 + 0.033 * math.sin(a), 0.004, 0.045 + 0.078 * math.cos(a)) for a in (j * math.tau / 24 for j in range(24))],
            [(side * 0.072 + 0.036 * math.sin(a), 0.042, 0.035 + 0.072 * math.cos(a)) for a in (j * math.tau / 24 for j in range(24))],
            [(side * 0.072 + 0.036 * math.sin(a), 0.095, 0.012 + 0.044 * math.cos(a)) for a in (j * math.tau / 24 for j in range(24))],
            [(side * 0.072 + 0.037 * math.sin(a), 0.155, 0.010 + 0.041 * math.cos(a)) for a in (j * math.tau / 24 for j in range(24))],
        ]
        add_loft_grid(ga, f"SleekAnkleBoot_{side}", boot_rings, "coal", closed_u=True, cap_ends=True)
        smooth_box(ga, f"BootBlockHeel_{side}", (side * 0.072, 0.028, -0.015), (0.028, 0.055, 0.030), "sole", bevel=0.004)


def eval_catmull_rom(pts, t):
    n = len(pts) - 1
    s = max(0.0, min(float(n), t * n))
    i = min(n - 1, int(s))
    u = s - i
    p0 = V(pts[max(0, i - 1)])
    p1 = V(pts[i])
    p2 = V(pts[i + 1])
    p3 = V(pts[min(n, i + 2)])
    u2 = u * u
    u3 = u2 * u
    return 0.5 * (
        (2.0 * p1)
        + (-p0 + p2) * u
        + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * u2
        + (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * u3
    )


def build_veronika_unified_arm(ga, side):
    """
    Build ONE continuous seamless articulated arm (`side = +1` for Left Arm, `side = -1` for Right Arm)
    matching `veronika_target_concept_1795129376528.jpg`:
      - Build canonical Left Arm (`x > 0`, palm facing DOWNWARD-INWARD over the crystal ball, dorsal back facing UPWARD-OUTWARD,
        sleek 35%-slimmer tailored bell cuff, smooth tapered shoulder cap with zero ball-joint bump),
      - And for `side = -1` (Right Arm), mirror every vertex `(x, y, z) -> (-x, y, z)` and recalc outward normals!
        This guarantees 100% identical palm-down symmetry and 100% outward normals on BOTH arms!
    """
    created = []

    # 1) Smooth Tapered Shoulder Cap tucked flush inside the sloped blazer shoulder at ARM_L_PIVOT_LOCAL (NO protruding step!)
    created.append(
        smooth_oval(
            ga,
            f"ArmShoulderPivotCap_{side}",
            (0.130, 1.304, 0.032),
            (0.038, 0.035, 0.036),
            "silk",
            segs=28,
            rings=18,
        )
    )

    # 2) Continuous Velvet Arm Sleeve with Sleek Tailored Bell Cuff (smoothly anchored into shoulder socket)
    sleeve_ctrl = [
        (0.116, 1.314, 0.032),
        (0.146, 1.292, 0.038),
        (0.178, 1.204, 0.086),
        (0.198, 1.122, 0.150),
        (0.178, 1.156, 0.216),
        (0.146, 1.204, 0.268),
        (0.128, 1.232, 0.296),
    ]
    n_seg = 36
    n_rings = 30
    sleeve_rings = []
    cuff_mouth_frame = None
    for k in range(n_rings):
        t = k / (n_rings - 1)
        p_c = eval_catmull_rom(sleeve_ctrl, t)
        t_next = min(1.0, t + 0.02)
        t_prev = max(0.0, t - 0.02)
        tang = (eval_catmull_rom(sleeve_ctrl, t_next) - eval_catmull_rom(sleeve_ctrl, t_prev)).normalized()
        up_ref = V((0.0, 1.0, 0.0))
        right_v = tang.cross(up_ref).normalized()
        up_v = right_v.cross(tang).normalized()

        shoulder_blend = max(0.0, 1.0 - t / 0.24)
        base_r = 0.031 + 0.011 * (shoulder_blend ** 1.5) - 0.004 * math.sin(t * math.pi * 0.8)
        bell_t = max(0.0, (t - 0.70) / 0.30)
        # Sleek 35% slimmer bell cuff flare (`rx = 0.037`, `ry = 0.043`)
        rx = base_r + 0.010 * (bell_t ** 1.6)
        ry = base_r + 0.016 * (bell_t ** 1.7)
        droop = -0.008 * (bell_t ** 2.0)

        ring = []
        for j in range(n_seg):
            a = j * math.tau / n_seg
            ca, sa = math.cos(a), math.sin(a)
            pt = p_c + right_v * (rx * ca) + up_v * (ry * sa + droop)
            ring.append((pt.x, pt.y, pt.z))
        sleeve_rings.append(ring)
        if k == n_rings - 1:
            cuff_mouth_frame = (p_c, tang, right_v, up_v, rx, ry, droop)

    created.append(add_loft_grid(ga, f"VelvetBellSleeve_{side}", sleeve_rings, "silk", closed_u=True, cap_ends=True))

    # Recessed Deep Wine-Plum Inner Bell-Cuff Cavity (`plum`)
    p_m, tang_m, right_m, up_m, rx_m, ry_m, droop_m = cuff_mouth_frame
    cuff_inner_rings = []
    for f_scale, f_depth in [(0.94, -0.001), (0.86, -0.010), (0.56, -0.032), (0.32, -0.045)]:
        ring = []
        for j in range(n_seg):
            a = j * math.tau / n_seg
            ca, sa = math.cos(a), math.sin(a)
            pt = p_m + tang_m * f_depth + right_m * (rx_m * f_scale * ca) + up_m * ((ry_m * f_scale * sa) + droop_m * f_scale)
            ring.append((pt.x, pt.y, pt.z))
        cuff_inner_rings.append(ring)
    created.append(add_loft_grid(ga, f"BellCuffDarkInterior_{side}", cuff_inner_rings, "plum", closed_u=True, cap_ends=True))

    # 3) Slender Wrist Skin + Snug Twin Gold Bangles
    wrist_ctrl = [
        (0.150, 1.198, 0.262),
        (0.132, 1.226, 0.292),
        (0.116, 1.244, 0.308),
        (0.100, 1.256, 0.324),
    ]
    dorsal_up = V((0.24, 0.93, 0.28)).normalized()

    wrist_rings = []
    bangle_frames = []
    for k in range(14):
        t = k / 13.0
        p_c = eval_catmull_rom(wrist_ctrl, t)
        tang = (eval_catmull_rom(wrist_ctrl, min(1.0, t + 0.04)) - eval_catmull_rom(wrist_ctrl, max(0.0, t - 0.04))).normalized()
        across_v = tang.cross(dorsal_up).normalized()
        up_v = across_v.cross(tang).normalized()
        rx = 0.0195 - 0.0020 * math.sin(t * math.pi)
        ry = 0.0145 - 0.0015 * math.sin(t * math.pi)
        if t > 0.72:
            pt_w = (t - 0.72) / 0.28
            rx += 0.0065 * (pt_w ** 1.4)
            ry -= 0.0010 * pt_w
        ring = []
        for j in range(24):
            a = j * math.tau / 24
            pt = p_c + across_v * (rx * math.cos(a)) + up_v * (ry * math.sin(a))
            ring.append((pt.x, pt.y, pt.z))
        wrist_rings.append(ring)
        if k in (5, 8):
            bangle_frames.append((p_c, across_v, up_v, rx, ry))

    created.append(add_loft_grid(ga, f"SlenderWristSkin_{side}", wrist_rings, "skin", closed_u=True, cap_ends=True))

    for b_idx, (p_b, r_b, u_b, rx_b, ry_b) in enumerate(bangle_frames):
        bangle_pts = [
            tuple(p_b + r_b * ((rx_b + 0.0016) * math.cos(j * math.tau / 32)) + u_b * ((ry_b + 0.0016) * math.sin(j * math.tau / 32)))
            for j in range(32)
        ]
        created.append(smooth_wire(ga, f"SnugGoldBangle_{side}_{b_idx}", bangle_pts, 0.0020, "gold", closed=True, res=6))

    # 4) Canonical Left Hand (`x > 0`, PALM DOWNWARD-INWARD over the crystal ball, dorsal back UPWARD-OUTWARD!)
    palm_forward = V((-0.78, 0.14, 0.61)).normalized()
    across_to_pinky = palm_forward.cross(dorsal_up).normalized()
    dorsal_up = across_to_pinky.cross(palm_forward).normalized()

    wrist_end = V((0.102, 1.255, 0.322))
    palm_rings = []
    for k in range(12):
        t = k / 11.0
        p_c = wrist_end + palm_forward * (0.054 * t) - dorsal_up * (0.003 * (t ** 1.4))
        w_half = 0.023 + 0.008 * math.sin(t * math.pi * 0.85)
        th_half = 0.0115 - 0.0028 * t
        ring = []
        for j in range(24):
            a = j * math.tau / 24
            ca, sa = math.cos(a), math.sin(a)
            arch = 0.0025 * math.cos(ca * math.pi * 0.5) * t
            pt = p_c + across_to_pinky * (w_half * ca) + dorsal_up * (th_half * sa + arch)
            ring.append((pt.x, pt.y, pt.z))
        palm_rings.append(ring)
    created.append(add_loft_grid(ga, f"GracefulPalm_{side}", palm_rings, "skin", closed_u=True, cap_ends=True))

    knuckle_center = wrist_end + palm_forward * 0.052 - dorsal_up * 0.002
    finger_specs = [
        ("Index",  -0.021, 0.062, 0.0064, 0.0040, ( 8.0, 18.0, 22.0), -5.0),
        ("Middle", -0.007, 0.068, 0.0067, 0.0041, ( 6.0, 20.0, 24.0),  0.0),
        ("Ring",    0.007, 0.063, 0.0063, 0.0039, (12.0, 25.0, 28.0),  5.0),
        ("Pinky",   0.021, 0.050, 0.0054, 0.0035, (16.0, 30.0, 34.0), 11.0),
    ]
    for f_name, u_off, f_len, r0, r1, pitches, yaw_deg in finger_specs:
        arch_drop = -0.0030 * ((u_off / 0.021) ** 2)
        base_pt = knuckle_center + across_to_pinky * u_off + dorsal_up * arch_drop
        yaw_rad = math.radians(yaw_deg)
        f_dir0 = (palm_forward * math.cos(yaw_rad) + across_to_pinky * math.sin(yaw_rad)).normalized()

        seg_lens = [f_len * 0.44, f_len * 0.31, f_len * 0.25]
        f_pts = [base_pt]
        cur_p = base_pt
        cur_d = f_dir0
        cur_u = dorsal_up
        for s_len, p_deg in zip(seg_lens, pitches):
            p_rad = math.radians(p_deg)
            d_step = (cur_d * math.cos(p_rad) - cur_u * math.sin(p_rad)).normalized()
            cur_u = (cur_u * math.cos(p_rad) + cur_d * math.sin(p_rad)).normalized()
            cur_d = d_step
            cur_p = cur_p + cur_d * s_len
            f_pts.append(cur_p)

        f_rings = []
        tip_frame = None
        for k in range(16):
            t = k / 15.0
            p_c = eval_catmull_rom(f_pts, t)
            tang = (eval_catmull_rom(f_pts, min(1.0, t + 0.04)) - eval_catmull_rom(f_pts, max(0.0, t - 0.04))).normalized()
            r_v = tang.cross(dorsal_up).normalized()
            u_v = r_v.cross(tang).normalized()
            rad = r0 + (r1 - r0) * (t ** 0.85)
            knuckle_swell = 1.0 + 0.05 * math.exp(-(((t - 0.44) / 0.08) ** 2)) + 0.04 * math.exp(-(((t - 0.75) / 0.07) ** 2))
            if t > 0.88:
                rad *= math.sqrt(max(0.08, 1.0 - ((t - 0.88) / 0.125) ** 2))
            rx_f = rad * knuckle_swell
            ry_f = rad * 0.88 * knuckle_swell
            ring = [
                tuple(p_c + r_v * (rx_f * math.cos(j * math.tau / 16)) + u_v * (ry_f * math.sin(j * math.tau / 16)))
                for j in range(16)
            ]
            f_rings.append(ring)
            if k == 13:
                tip_frame = (p_c, tang, r_v, u_v, r1)

        created.append(add_loft_grid(ga, f"Finger_{f_name}_{side}", f_rings, "skin", closed_u=True, cap_ends=True))

        p_t, tang_t, rv_t, uv_t, r_t = tip_frame
        nail_center = p_t + tang_t * (r_t * 0.55) + uv_t * (r_t * 0.76)
        nail_pts = [
            tuple(nail_center - tang_t * (r_t * 1.00)),
            tuple(nail_center),
            tuple(nail_center + tang_t * (r_t * 1.35) - uv_t * (r_t * 0.14)),
        ]
        created.append(
            add_ribbon_loft(
                ga,
                f"BurgundyAlmondNail_{f_name}_{side}",
                nail_pts,
                widths=[r_t * 0.62, r_t * 0.86, r_t * 0.30],
                thicknesses=[0.0007, 0.0012, 0.0006],
                normals=[tuple(uv_t)] * 3,
                key="nail",
                subsurf_level=1,
            )
        )

        if side < 0 and f_name == "Ring":
            p_ring = eval_catmull_rom(f_pts, 0.24)
            tang_ring = (eval_catmull_rom(f_pts, 0.28) - eval_catmull_rom(f_pts, 0.20)).normalized()
            rv_r = tang_ring.cross(dorsal_up).normalized()
            uv_r = rv_r.cross(tang_ring).normalized()
            r_band = r0 * 0.98 + 0.0007
            ring_pts = [
                tuple(p_ring + rv_r * (r_band * math.cos(j * math.tau / 24)) + uv_r * (r_band * 0.90 * math.sin(j * math.tau / 24)))
                for j in range(24)
            ]
            created.append(smooth_wire(ga, "SnugGoldFingerRing", ring_pts, 0.0011, "gold", closed=True, res=5))

    thumb_base = wrist_end + palm_forward * 0.016 - across_to_pinky * 0.021 - dorsal_up * 0.005
    t_dir1 = (palm_forward * 0.72 - across_to_pinky * 0.50 - dorsal_up * 0.48).normalized()
    t_dir2 = (palm_forward * 0.88 - across_to_pinky * 0.20 - dorsal_up * 0.43).normalized()
    t_dir3 = (palm_forward * 0.94 + across_to_pinky * 0.12 - dorsal_up * 0.32).normalized()
    t_pts = [
        thumb_base,
        thumb_base + t_dir1 * 0.023,
        thumb_base + t_dir1 * 0.023 + t_dir2 * 0.019,
        thumb_base + t_dir1 * 0.023 + t_dir2 * 0.019 + t_dir3 * 0.015,
    ]
    t_rings = []
    t_tip_frame = None
    for k in range(14):
        t = k / 13.0
        p_c = eval_catmull_rom(t_pts, t)
        tang = (eval_catmull_rom(t_pts, min(1.0, t + 0.05)) - eval_catmull_rom(t_pts, max(0.0, t - 0.05))).normalized()
        r_v = tang.cross(dorsal_up).normalized()
        u_v = r_v.cross(tang).normalized()
        rad = 0.0074 + (0.0044 - 0.0074) * (t ** 0.85)
        if t > 0.86:
            rad *= math.sqrt(max(0.08, 1.0 - ((t - 0.86) / 0.145) ** 2))
        ring = [
            tuple(p_c + r_v * (rad * math.cos(j * math.tau / 16)) + u_v * (rad * 0.88 * math.sin(j * math.tau / 16)))
            for j in range(16)
        ]
        t_rings.append(ring)
        if k == 11:
            t_tip_frame = (p_c, tang, r_v, u_v, 0.0044)
    created.append(add_loft_grid(ga, f"Thumb_{side}", t_rings, "skin", closed_u=True, cap_ends=True))

    p_t, tang_t, rv_t, uv_t, r_t = t_tip_frame
    nail_center = p_t + tang_t * (r_t * 0.50) + uv_t * (r_t * 0.74)
    created.append(
        add_ribbon_loft(
            ga,
            f"BurgundyAlmondNail_Thumb_{side}",
            [
                tuple(nail_center - tang_t * (r_t * 0.95)),
                tuple(nail_center),
                tuple(nail_center + tang_t * (r_t * 1.25) - uv_t * (r_t * 0.12)),
            ],
            widths=[r_t * 0.60, r_t * 0.82, r_t * 0.28],
            thicknesses=[0.0006, 0.0011, 0.0005],
            normals=[tuple(uv_t)] * 3,
            key="nail",
            subsurf_level=1,
        )
    )

    # Mirror X coordinates for Right Arm (`side = -1`) and reverse face winding so normals stay 100% outward!
    if side < 0:
        for o in created:
            bm = bmesh.new()
            bm.from_mesh(o.data)
            for v in bm.verts:
                v.co.x = -v.co.x
            bmesh.ops.reverse_faces(bm, faces=bm.faces)
            bm.to_mesh(o.data)
            bm.free()
            o.data.update()
            for poly in o.data.polygons:
                poly.use_smooth = True


def build_veronika_tarot_table(ga):
    n_t = 64
    t_rings = []
    for k in range(18):
        t = k / 17.0
        h = 0.010 + (0.992 - 0.010) * t
        flare = 1.028 - 0.028 * (t ** 0.7)
        fold_amp = 0.015 * ((1.0 - t) ** 1.15)
        rx = 0.835 * flare
        rz = 0.375 * flare
        ring = []
        for i in range(n_t):
            a = i * math.tau / n_t
            ripple = fold_amp * (math.sin(a * 12.0) + 0.35 * math.cos(a * 24.0 + 0.6))
            ring.append(((rx + ripple) * math.sin(a), h, 0.380 + (rz + ripple * 0.65) * math.cos(a)))
        t_rings.append(ring)
    add_loft_grid(ga, "SmoothDrapedTablecloth", t_rings, "plum")

    smooth_oval(ga, "VelvetTableTop", (0.0, 0.985, 0.380), (0.838, 0.020, 0.378), "silk", segs=64, rings=18)

    fringe_pts = [
        (0.840 * math.sin(i * math.tau / 64), 0.968, 0.380 + 0.380 * math.cos(i * math.tau / 64))
        for i in range(64)
    ]
    smooth_wire(ga, "TableGoldBullionFringe", fringe_pts, 0.0085, "gold", closed=True, res=6)

    n_p = 48
    plinth_profile = [
        (1.010, 0.072),
        (1.022, 0.068),
        (1.032, 0.078),
        (1.042, 0.088),
    ]
    p_rings = [
        [(r * math.sin(i * math.tau / n_p), h, 0.357 + r * math.cos(i * math.tau / n_p)) for i in range(n_p)]
        for h, r in plinth_profile
    ]
    add_loft_grid(ga, "CrystalBallBrassPlinth", p_rings, "gold")

    for leg_i in range(4):
        ang = math.radians(45.0 + leg_i * 90.0)
        ca, sa = math.cos(ang), math.sin(ang)
        leg_pts = [
            (0.064 * sa, 1.024, 0.357 + 0.064 * ca),
            (0.084 * sa, 1.020, 0.357 + 0.084 * ca),
            (0.096 * sa, 1.004, 0.357 + 0.096 * ca),
            (0.098 * sa, 0.996, 0.357 + 0.098 * ca),
        ]
        smooth_wire(ga, f"PlinthScrollLeg_{leg_i}", leg_pts, 0.0065, "gold", closed=False, res=6)
        smooth_oval(ga, f"PlinthFootPad_{leg_i}", (0.098 * sa, 0.995, 0.357 + 0.098 * ca), (0.008, 0.004, 0.008), "gold", segs=16, rings=10)

    smooth_oval(ga, "CrystalBallOuterSphere", (0.0, 1.115, 0.357), (0.104, 0.104, 0.104), "orb", segs=48, rings=32)
    smooth_oval(ga, "CrystalBallInnerMist",   (0.010, 1.118, 0.362), (0.056, 0.044, 0.052), "iris", segs=32, rings=20, rot=(0.2, 0.4, 0.1))
    smooth_oval(ga, "CrystalBallInnerCore",   (-0.006, 1.112, 0.354), (0.028, 0.028, 0.028), "flame", segs=24, rings=16)

    for idx, (cx, cz, ch) in enumerate([(-0.56, 0.31, 0.195), (-0.64, 0.44, 0.145), (0.56, 0.31, 0.185), (0.64, 0.44, 0.135)]):
        holder_rings = [
            [(cx + r * math.sin(i * math.tau / 28), h, cz + r * math.cos(i * math.tau / 28)) for i in range(28)]
            for h, r in [(0.993, 0.036), (0.998, 0.032), (1.005, 0.022), (1.012, 0.024)]
        ]
        add_loft_grid(ga, f"BrassCandleHolder_{idx}", holder_rings, "gold")
        candle_rings = [
            [(cx + r * math.sin(i * math.tau / 28), h, cz + r * math.cos(i * math.tau / 28)) for i in range(28)]
            for h, r in [(1.008, 0.020), (1.008 + ch * 0.5, 0.0185), (1.008 + ch, 0.0165), (1.008 + ch + 0.004, 0.009)]
        ]
        add_loft_grid(ga, f"BeeswaxCandle_{idx}", candle_rings, "ivory")
        smooth_oval(ga, f"CandleFlameOuter_{idx}", (cx, 1.008 + ch + 0.024, cz), (0.0085, 0.021, 0.0085), "flame", segs=20, rings=14)

    for c_idx in range(4):
        x = (c_idx - 1.5) * 0.122
        z = 0.598 - 0.038 * abs(c_idx - 1.5)
        rot_y = (c_idx - 1.5) * 0.10
        card_obj = smooth_box(ga, f"TarotCardArcana_{c_idx}", (x, 1.000, z), (0.092, 0.0040, 0.140), "card", bevel=0.001, segments=2, rot_z=rot_y)
        card_obj["card_index"] = c_idx


def finalize_veronika_piece(ga, objs, export_name, pivot_shift=None):
    """Join meshes, assign UVs to the 1024x1024 `VeronikaBeautyAtlas`, and export GLB."""
    atlas = build_veronika_beauty_atlas()

    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    poly_uv_tags = []
    for o in objs:
        tag = 99
        if o.get("is_iris"):
            tag = -1
        elif "card_index" in o:
            tag = int(o["card_index"])
        elif o.name.startswith("VeronikaStylizedHead") or o.name.startswith("DecolletageSkinColumn"):
            tag = -2
        poly_uv_tags.extend([tag] * len(o.data.polygons))

    if len(objs) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = export_name
    mesh = obj.data

    for p in mesh.polygons:
        p.use_smooth = True

    used = sorted({p.material_index for p in mesh.polygons})
    old_mats = [mesh.materials[i] for i in used]
    remap = {old_i: new_i for new_i, old_i in enumerate(used)}
    remapped_indices = [remap[p.material_index] for p in mesh.polygons]
    mesh.materials.clear()
    for m in old_mats:
        mesh.materials.append(m)
    for p, new_mi in zip(mesh.polygons, remapped_indices):
        p.material_index = new_mi

    if len(mesh.materials) < 2:
        fallback_key = "gold" if old_mats[0].name != "gold" else "plum"
        mesh.materials.append(make_veronika_mat(ga, fallback_key, atlas))

    mat_order = [m.name for m in mesh.materials]

    while mesh.uv_layers:
        mesh.uv_layers.remove(mesh.uv_layers[0])
    uv_layer = mesh.uv_layers.new(name="UVMap")

    verts = mesh.vertices
    for poly, uv_tag in zip(mesh.polygons, poly_uv_tags):
        m_name = mat_order[poly.material_index]
        if uv_tag == -1 or m_name == "iris":
            cx_eye = 0.0320 if poly.center.x >= 0 else -0.0320
            cy_eye = 1.5467
            for li in poly.loop_indices:
                v_co = verts[mesh.loops[li].vertex_index].co
                du = (v_co.x - cx_eye) / 0.0092
                dv = (v_co.z - cy_eye) / 0.0092
                uv_layer.data[li].uv = (0.75 + du * 0.21, 0.25 + dv * 0.21)
        elif uv_tag in (0, 1, 2, 3) or m_name == "card":
            c_idx = uv_tag if uv_tag in (0, 1, 2, 3) else 0
            x_center = (c_idx - 1.5) * 0.122
            z_center = 0.598 - 0.038 * abs(c_idx - 1.5)
            rot_z = (c_idx - 1.5) * 0.10
            ca, sa = math.cos(-rot_z), math.sin(-rot_z)
            u0 = 0.5 + (c_idx * 128.0 + 8.0) / 1024.0
            u_span = 112.0 / 1024.0
            for li in poly.loop_indices:
                v_co = verts[mesh.loops[li].vertex_index].co
                dx = v_co.x - x_center
                dy = v_co.y - (-z_center)
                lx = dx * ca - dy * sa
                ly = dx * sa + dy * ca
                tu = min(1.0, max(0.0, (lx / 0.092) + 0.5))
                tv = min(1.0, max(0.0, (ly / 0.140) + 0.5))
                uv_layer.data[li].uv = (u0 + tu * u_span, 0.52 + tv * 0.46)
        elif m_name == "skin":
            for li in poly.loop_indices:
                v_co = verts[mesh.loops[li].vertex_index].co
                if uv_tag == -2 and v_co.z > 1.462 and v_co.y < -0.08:
                    uf = max(-0.92, min(0.92, v_co.x / 0.082))
                    vf = max(-0.92, min(0.92, (v_co.z - 1.522) / 0.085))
                    uv_layer.data[li].uv = (0.25 + uf * 0.21, 0.25 + vf * 0.21)
                else:
                    uv_layer.data[li].uv = (0.12, 0.12)
        else:
            for li in poly.loop_indices:
                v_co = verts[mesh.loops[li].vertex_index].co
                tu = min(1.0, max(0.0, (v_co.x + 0.85) / 1.70))
                tv = min(1.0, max(0.0, v_co.z / 1.70))
                uv_layer.data[li].uv = (0.05 + tu * 0.40, 0.55 + tv * 0.40)

    while mesh.color_attributes:
        mesh.color_attributes.remove(mesh.color_attributes[0])

    if pivot_shift is not None:
        bl_shift = V((pivot_shift.x, -pivot_shift.z, pivot_shift.y))
        for v in mesh.vertices:
            v.co -= bl_shift

    mesh.update()

    OUT_MODELS.mkdir(parents=True, exist_ok=True)
    out_path = OUT_MODELS / f"{export_name}.glb"
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(
        filepath=str(out_path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_texcoords=True,
        export_normals=True,
        export_materials="EXPORT",
    )
    print(f"EXPORTED {export_name}.glb: {len(mesh.vertices)} verts, {len(mesh.polygons)} tris, {len(mesh.materials)} materials")
    return out_path


def render_veronika_v3_proof():
    """
    Render high-res 1920x1080 3-panel proof `veronika_v3_proof.png` with `use_backface_culling = True` on all imported materials
    so Blender Eevee matches Raylib OpenGL (`GL_CULL_FACE`) 1-to-1!
    """
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x = 1920
    sc.render.resolution_y = 1080
    sc.render.film_transparent = False
    world = bpy.data.worlds.new("VeronikaV3ProofWorld")
    sc.world = world
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.14, 0.11, 0.16, 1.0)
        bg.inputs[1].default_value = 1.0

    # Panel 1 (Left, x = -1.16): Tight Macro Beauty Close-up of Open-Eyes Head (`char_hr_veronika_head.glb`)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / "char_hr_veronika_head.glb"))
    head_open = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
    for o in head_open:
        o.scale = (3.05, 3.05, 3.05)
        o.location = V((-1.16, -0.35, 0.48))
        o.rotation_mode = "XYZ"
        o.rotation_euler = (0.0, 0.0, math.radians(-6.0))

    # Panel 2 (Center, x = -0.52): Tight Macro Beauty Close-up of Blinking Head (`char_hr_veronika_head_blink.glb`)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / "char_hr_veronika_head_blink.glb"))
    head_blink = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
    for o in head_blink:
        o.scale = (3.05, 3.05, 3.05)
        o.location = V((-0.52, -0.35, 0.48))
        o.rotation_mode = "XYZ"
        o.rotation_euler = (0.0, 0.0, math.radians(6.0))

    # Panel 3 (Right, x = +0.66): Full Modular Assembly (Body + Arm_L at ARM_L_PIVOT_LOCAL + Arm_R at ARM_R_PIVOT_LOCAL + Head at HEAD_PIVOT_LOCAL + Tarot Table)
    p3_base = V((0.66, 0.0, -0.02))
    for glb_fn, local_piv in [
        ("char_hr_veronika_body.glb", V((0.0, 0.0, 0.0))),
        ("char_hr_veronika_arm_l.glb", ARM_L_PIVOT_LOCAL),
        ("char_hr_veronika_arm_r.glb", ARM_R_PIVOT_LOCAL),
        ("char_hr_veronika_head.glb", HEAD_PIVOT_LOCAL),
        ("prop_veronika_tarot_table.glb", V((0.0, 0.0, 0.0))),
    ]:
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / glb_fn))
        objs = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
        bl_piv = V((local_piv.x, -local_piv.z, local_piv.y))
        for o in objs:
            o.location = p3_base + bl_piv
            o.rotation_mode = "XYZ"

    # Enable backface culling on every single imported glTF material so Eevee tests GL_CULL_FACE identically to Raylib!
    for m in bpy.data.materials:
        m.use_backface_culling = True

    for name, loc, energy, col in [
        ("Key",  (2.4, -3.4, 2.3), 3.4, (1.0, 0.95, 0.90)),
        ("Fill", (-2.6, -2.8, 1.6), 2.1, (0.88, 0.86, 1.0)),
        ("Rim",  (1.8, 2.8, 2.2),   2.0, (1.0, 0.78, 0.62)),
    ]:
        ld = bpy.data.lights.new(name, type="SUN")
        ld.energy = energy
        ld.color = col
        lo = bpy.data.objects.new(name, ld)
        sc.collection.objects.link(lo)
        lo.location = loc
        d = V((0.0, 0.0, 1.15)) - V(loc)
        lo.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()

    cd = bpy.data.cameras.new("ProofCam")
    cd.type = "ORTHO"
    cd.ortho_scale = 3.04
    co = bpy.data.objects.new("ProofCam", cd)
    sc.collection.objects.link(co)
    sc.camera = co
    co.location = (0.0, -3.1, 1.76)
    d = V((0.0, -0.10, 0.94)) - co.location
    co.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()

    for target_path in (ARTIFACT_PROOF_1, ARTIFACT_PROOF_2, SCREENSHOT_PROOF):
        try:
            target_path.parent.mkdir(parents=True, exist_ok=True)
            sc.render.filepath = str(target_path)
            bpy.ops.render.render(write_still=True)
            print("Saved Veronika v3 proof:", target_path)
        except Exception as exc:
            print("Warning saving proof to", target_path, ":", exc)


def render_veronika_lipsync_qc():
    """
    Render a crisp 4-panel macro close-up QC proof (`scratch/veronika_lipsync_qc.png`) showing
    Closed / Slight / Open / Round heads side-by-side (top row: straight front macro, bottom row: 24 deg 3/4 view)
    with `use_backface_culling = True` so Blender Eevee matches Raylib OpenGL (`GL_CULL_FACE`) 1-to-1.
    """
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x = 1920
    sc.render.resolution_y = 1080
    sc.render.film_transparent = False
    world = bpy.data.worlds.new("VeronikaLipsyncQCWorld")
    sc.world = world
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.14, 0.11, 0.16, 1.0)
        bg.inputs[1].default_value = 1.0

    panels = [
        ("char_hr_veronika_head.glb",            -1.08),
        ("char_hr_veronika_head_talk_slight.glb", -0.36),
        ("char_hr_veronika_head_talk_open.glb",   +0.36),
        ("char_hr_veronika_head_talk_round.glb",  +1.08),
    ]
    for glb_fn, x_pos in panels:
        # Top row: crisp front macro close-up
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / glb_fn))
        objs = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
        for o in objs:
            o.scale = (2.85, 2.85, 2.85)
            o.location = V((x_pos, -0.25, 0.96))
            o.rotation_mode = "XYZ"
            o.rotation_euler = (0.0, 0.0, 0.0)

        # Bottom row: 24-degree 3/4 angle close-up to verify zero cheek bleed & clean lip depth
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / glb_fn))
        objs_34 = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
        for o in objs_34:
            o.scale = (2.85, 2.85, 2.85)
            o.location = V((x_pos, -0.25, 0.16))
            o.rotation_mode = "XYZ"
            o.rotation_euler = (0.0, 0.0, math.radians(-24.0))

    for m in bpy.data.materials:
        m.use_backface_culling = True

    for name, loc, energy, col in [
        ("Key",  (2.2, -3.5, 2.2), 3.3, (1.0, 0.95, 0.90)),
        ("Fill", (-2.4, -2.8, 1.5), 2.1, (0.88, 0.86, 1.0)),
        ("Rim",  (1.5, 2.8, 2.1),   1.9, (1.0, 0.78, 0.62)),
    ]:
        ld = bpy.data.lights.new(name, type="SUN")
        ld.energy = energy
        ld.color = col
        lo = bpy.data.objects.new(name, ld)
        sc.collection.objects.link(lo)
        lo.location = loc
        d = V((0.0, 0.0, 0.90)) - V(loc)
        lo.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()

    cd = bpy.data.cameras.new("LipsyncQCCam")
    cd.type = "ORTHO"
    cd.ortho_scale = 2.98
    co = bpy.data.objects.new("LipsyncQCCam", cd)
    sc.collection.objects.link(co)
    sc.camera = co
    co.location = (0.0, -3.2, 0.92)
    d = V((0.0, 0.0, 0.92)) - co.location
    co.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()

    qc_path = ROOT_DIR / "scratch" / "veronika_lipsync_qc.png"
    qc_path.parent.mkdir(parents=True, exist_ok=True)
    sc.render.filepath = str(qc_path)
    bpy.ops.render.render(write_still=True)
    print("Saved Veronika lip-sync 4-panel QC proof:", qc_path)


def export_veronika_lipsync_heads(ga):
    """Export the 3 synchronized talking viseme head GLBs with identical HEAD_PIVOT_LOCAL."""
    for viseme, export_name in [
        ("slight", "char_hr_veronika_head_talk_slight"),
        ("open",   "char_hr_veronika_head_talk_open"),
        ("round",  "char_hr_veronika_head_talk_round"),
    ]:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        ga.MATS.clear()
        atlas = build_veronika_beauty_atlas()
        for k in VERONIKA_MAT_ORDER:
            make_veronika_mat(ga, k, atlas)
        build_stylized_head_mesh(ga, mouth_viseme=viseme)
        build_veronika_head_accessories(ga, blink=False, mouth_viseme=viseme)
        finalize_veronika_piece(
            ga,
            [o for o in bpy.context.scene.objects if o.type == "MESH"],
            export_name,
            pivot_shift=HEAD_PIVOT_LOCAL,
        )
    render_veronika_lipsync_qc()


def export_all_veronika_models(ga):
    # 1. char_hr_veronika_head.glb (Open Eyes, origin at HEAD_PIVOT_LOCAL)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ga.MATS.clear()
    atlas = build_veronika_beauty_atlas()
    for k in VERONIKA_MAT_ORDER:
        make_veronika_mat(ga, k, atlas)
    build_stylized_head_mesh(ga, mouth_viseme="closed")
    build_veronika_head_accessories(ga, blink=False, mouth_viseme="closed")
    finalize_veronika_piece(
        ga,
        [o for o in bpy.context.scene.objects if o.type == "MESH"],
        "char_hr_veronika_head",
        pivot_shift=HEAD_PIVOT_LOCAL,
    )

    # 2. char_hr_veronika_head_blink.glb (Closed Blinking Eyelids, origin at HEAD_PIVOT_LOCAL)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ga.MATS.clear()
    atlas = build_veronika_beauty_atlas()
    for k in VERONIKA_MAT_ORDER:
        make_veronika_mat(ga, k, atlas)
    build_stylized_head_mesh(ga, mouth_viseme="closed")
    build_veronika_head_accessories(ga, blink=True, mouth_viseme="closed")
    finalize_veronika_piece(
        ga,
        [o for o in bpy.context.scene.objects if o.type == "MESH"],
        "char_hr_veronika_head_blink",
        pivot_shift=HEAD_PIVOT_LOCAL,
    )

    # 2b. 3 Talking Lip-Sync Viseme Heads (Slight / Open / Round, identical HEAD_PIVOT_LOCAL)
    export_veronika_lipsync_heads(ga)

    # 3. char_hr_veronika_body.glb (Slender torso + wrap blazer + V-neck lariat + back wave cascade + trousers/boots, NO arms, origin [0,0,0])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ga.MATS.clear()
    atlas = build_veronika_beauty_atlas()
    for k in VERONIKA_MAT_ORDER:
        make_veronika_mat(ga, k, atlas)
    build_veronika_slender_body(ga)
    finalize_veronika_piece(
        ga,
        [o for o in bpy.context.scene.objects if o.type == "MESH"],
        "char_hr_veronika_body",
        pivot_shift=None,
    )

    # 4. char_hr_veronika_arm_l.glb (Complete Left Arm from shoulder cap to fingertips, origin at ARM_L_PIVOT_LOCAL)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ga.MATS.clear()
    atlas = build_veronika_beauty_atlas()
    for k in VERONIKA_MAT_ORDER:
        make_veronika_mat(ga, k, atlas)
    build_veronika_unified_arm(ga, side=+1)
    finalize_veronika_piece(
        ga,
        [o for o in bpy.context.scene.objects if o.type == "MESH"],
        "char_hr_veronika_arm_l",
        pivot_shift=ARM_L_PIVOT_LOCAL,
    )

    # 5. char_hr_veronika_arm_r.glb (Complete Right Arm from shoulder cap to fingertips, origin at ARM_R_PIVOT_LOCAL)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ga.MATS.clear()
    atlas = build_veronika_beauty_atlas()
    for k in VERONIKA_MAT_ORDER:
        make_veronika_mat(ga, k, atlas)
    build_veronika_unified_arm(ga, side=-1)
    finalize_veronika_piece(
        ga,
        [o for o in bpy.context.scene.objects if o.type == "MESH"],
        "char_hr_veronika_arm_r",
        pivot_shift=ARM_R_PIVOT_LOCAL,
    )

    # 6. char_hr_veronika_hands.glb (Both complete unified arms shifted to HANDS_PIVOT_LOCAL)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ga.MATS.clear()
    atlas = build_veronika_beauty_atlas()
    for k in VERONIKA_MAT_ORDER:
        make_veronika_mat(ga, k, atlas)
    build_veronika_unified_arm(ga, side=+1)
    build_veronika_unified_arm(ga, side=-1)
    finalize_veronika_piece(
        ga,
        [o for o in bpy.context.scene.objects if o.type == "MESH"],
        "char_hr_veronika_hands",
        pivot_shift=HANDS_PIVOT_LOCAL,
    )

    # 7. prop_veronika_tarot_table.glb (Origin at floor [0,0,0])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ga.MATS.clear()
    atlas = build_veronika_beauty_atlas()
    for k in VERONIKA_MAT_ORDER:
        make_veronika_mat(ga, k, atlas)
    build_veronika_tarot_table(ga)
    finalize_veronika_piece(
        ga,
        [o for o in bpy.context.scene.objects if o.type == "MESH"],
        "prop_veronika_tarot_table",
        pivot_shift=None,
    )

    # 8. char_hr_veronika.glb (Combined full exhibit for Archive [N] turntable & fallback, origin at floor [0,0,0])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ga.MATS.clear()
    atlas = build_veronika_beauty_atlas()
    for k in VERONIKA_MAT_ORDER:
        make_veronika_mat(ga, k, atlas)
    build_stylized_head_mesh(ga)
    build_veronika_head_accessories(ga, blink=False)
    build_veronika_slender_body(ga)
    build_veronika_unified_arm(ga, side=+1)
    build_veronika_unified_arm(ga, side=-1)
    build_veronika_tarot_table(ga)
    finalize_veronika_piece(
        ga,
        [o for o in bpy.context.scene.objects if o.type == "MESH"],
        "char_hr_veronika",
        pivot_shift=None,
    )

    render_veronika_v3_proof()
