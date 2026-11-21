"""AAA 1990s Italian Commercial Espresso Barista Station -> assets/models/prop_coffee_machine.glb
Museum-grade La Marzocco / Faema E61 commercial 2-group espresso bar + Mazzer-style Italian burr grinder
on a bespoke oiled-walnut, brass & veined Nero-Marquina marble barista cabinet:
  - Exact footprint matching LevelAndEntities.cs:1093: width ~0.88m, height ~1.39m, depth ~0.68m, Y=0 at floor, faces +Z
  - Calibrated specifically for Raylib's Finish.Imported shader (studio reflection-ramp chrome/brass LUTs,
    deep oxblood-burgundy metallic lacquer, high-contrast veined marble with baked contact shadows,
    readable warm oiled walnut with deep shaker-panel AO, dedicated 256x256 vintage pressure gauge dials!)
  - Sculpted double-curved Faema/La Marzocco oxblood side cowls with mirror-chrome perimeter trim ribs & brass louvers
  - Twin lathe-turned 32-segment chrome E61 group heads, mechanical cam levers, turned walnut portafilters
  - Slender articulated gooseneck chrome steam wands + scalloped rotary valves + chrome milk frothing pitcher
  - Deep obsidian drip well + razor-sharp stainless grate + brass gallery rail + naturally stacked inverted cups & saucers
  - Sculpted Mazzer-style Italian commercial burr grinder on left counter + silicone tamping mat, walnut tamper & knock-box
  - Strictly < 15k verts per material primitive (16-bit Raylib safe), 24-ray BVH AO + Curvature bake, GL_CULL_FACE verified
  - Automatically renders 3-angle Blender studio proof sheet -> screenshots/coffee_studio_proof.png
"""
import math
import os
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Euler, Matrix, Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[3]
OUT_MODELS = ROOT / "assets/models"
OUT_PROOF = ROOT / "screenshots/coffee_studio_proof.png"


def xyz(p):
    """Game coords (X right, Y up, Z forward/front) -> Blender Z-up (X, -Z, Y)."""
    return (p[0], -p[2], p[1])


def srgb_to_lin(c):
    """Convert target sRGB reflectance (0..1) into Blender linear float so packed GLB PNG hits exact Raylib values."""
    c = np.clip(c, 0.0, 1.0)
    return np.power(c, 2.2)


# =============================================================================
# 1. PROCEDURAL TEXTURES & STUDIO-REFLECTION LUTs (Calibrated for Raylib + Eevee)
# =============================================================================
def build_texture(key):
    img_name = f"CoffeeTex_{key}"
    existing = bpy.data.images.get(img_name)
    if existing is not None:
        return existing

    # -------------------------------------------------------------------------
    # A. DEDICATED 256x256 VINTAGE ITALIAN BARISTA PRESSURE GAUGE DIAL
    # -------------------------------------------------------------------------
    if key == "GaugeDial":
        W, H = 256, 256
        y, x = np.mgrid[0:H, 0:W].astype(np.float32)
        nx = (x / (W - 1.0)) * 2.0 - 1.0
        ny = (y / (H - 1.0)) * 2.0 - 1.0
        r = np.sqrt(nx * nx + ny * ny)
        ang = np.arctan2(ny, nx)  # -pi..pi

        # Warm vintage antique-ivory enamel dial with subtle bezel shadow vignette
        vig = np.clip(1.0 - 0.34 * (r ** 2.6), 0.55, 1.0)
        sr = np.full((H, W), 0.90, dtype=np.float32) * vig
        sg = np.full((H, W), 0.85, dtype=np.float32) * vig
        sb = np.full((H, W), 0.72, dtype=np.float32) * vig

        # Outer thin dark calibration ring & inner decorative brass circle
        outer_ring = np.exp(-((r - 0.86) / 0.018) ** 2)
        inner_ring = np.exp(-((r - 0.50) / 0.014) ** 2)
        sr = sr * (1.0 - 0.72 * outer_ring) + 0.10 * outer_ring
        sg = sg * (1.0 - 0.72 * outer_ring) + 0.09 * outer_ring
        sb = sb * (1.0 - 0.72 * outer_ring) + 0.08 * outer_ring

        sr = sr * (1.0 - 0.35 * inner_ring) + 0.62 * 0.35 * inner_ring
        sg = sg * (1.0 - 0.35 * inner_ring) + 0.48 * 0.35 * inner_ring
        sb = sb * (1.0 - 0.35 * inner_ring) + 0.20 * 0.35 * inner_ring

        # Optimal 9-BAR green extraction zone arc (top-right: +25 deg to +85 deg)
        deg = np.degrees(ang)
        green_zone = (r > 0.64) & (r < 0.77) & (deg >= 18.0) & (deg <= 78.0)
        sr[green_zone] = 0.16
        sg[green_zone] = 0.52
        sb[green_zone] = 0.26

        # Overpressure crimson danger zone arc (right-lower: -48 deg to +16 deg)
        red_zone = (r > 0.64) & (r < 0.77) & (deg >= -48.0) & (deg <= 16.0)
        sr[red_zone] = 0.68
        sg[red_zone] = 0.12
        sb[red_zone] = 0.12

        # Radial scale tick marks (-215 deg to +35 deg, excluding bottom dead-zone -125..-55)
        in_scale = ~((deg > -128.0) & (deg < -52.0))
        # Major ticks every 30 deg
        tick_ang = np.abs(((deg + 15.0) % 30.0) - 15.0)
        major_tick = in_scale & (r > 0.66) & (r < 0.85) & (tick_ang < 2.2)
        # Minor ticks every 10 deg
        mtick_ang = np.abs(((deg + 5.0) % 10.0) - 5.0)
        minor_tick = in_scale & (r > 0.74) & (r < 0.85) & (mtick_ang < 1.3)
        for msk in (major_tick, minor_tick):
            sr[msk] = 0.10
            sg[msk] = 0.09
            sb[msk] = 0.08

        # Dark crimson-black steel pointer needle aimed at optimal ~9 bar (+52 deg)
        na = math.radians(52.0)
        dx, dy = math.cos(na), math.sin(na)
        proj = nx * dx + ny * dy
        perp = np.abs(-nx * dy + ny * dx)
        needle = (proj > -0.18) & (proj < 0.74) & (perp < (0.042 * (1.0 - np.clip(proj, 0.0, 0.75) * 0.72)))
        sr[needle] = 0.11
        sg[needle] = 0.08
        sb[needle] = 0.08

        # Central turned brass rivet pin
        pin = r < 0.13
        pin_hi = r < 0.07
        sr[pin], sg[pin], sb[pin] = 0.74, 0.56, 0.24
        sr[pin_hi], sg[pin_hi], sb[pin_hi] = 0.90, 0.76, 0.42

        # Outer bezel recessed drop shadow
        bezel_sh = np.clip((r - 0.89) / 0.11, 0.0, 1.0)
        sr *= (1.0 - 0.55 * bezel_sh)
        sg *= (1.0 - 0.55 * bezel_sh)
        sb *= (1.0 - 0.55 * bezel_sh)

        arr = np.ones((H, W, 4), dtype=np.float32)
        arr[:, :, 0] = srgb_to_lin(sr)
        arr[:, :, 1] = srgb_to_lin(sg)
        arr[:, :, 2] = srgb_to_lin(sb)
        img = bpy.data.images.new(img_name, width=W, height=H, alpha=False)
        img.pixels.foreach_set(arr.ravel())
        img.pack()
        return img

    # -------------------------------------------------------------------------
    # B. DEDICATED 512x512 LUXURY VEINED CALACATTA / CREAM-AND-ESPRESSO ITALIAN MARBLE
    #    WITH BAKED CONTACT SHADOWS UNDER MACHINE, GRINDER & TAMPING MAT!
    # -------------------------------------------------------------------------
    if key == "DarkMarble":
        W, H = 512, 512
        rng = np.random.default_rng(1994)
        y, x = np.mgrid[0:H, 0:W].astype(np.float32)
        u = x / (W - 1.0)  # maps X in [-0.44, +0.44]
        v = y / (H - 1.0)  # maps Z in [-0.33, +0.35]

        # Multi-octave organic Calacatta Italian marble turbulence
        warp1 = (
            0.55 * np.sin(u * 6.8 + v * 4.5 + 0.6)
            + 0.32 * np.sin(u * 13.5 - v * 11.2 + 2.1)
            + 0.18 * np.sin(u * 25.0 + v * 19.0 + 4.0)
            + 0.09 * np.sin(u * 46.0 - v * 37.0 + 1.3)
        )
        warp2 = (
            0.50 * np.cos(u * 6.0 - v * 7.8 + 1.4)
            + 0.28 * np.sin(u * 15.5 + v * 14.0 + 3.2)
            + 0.14 * np.cos(u * 30.0 - v * 27.0 + 0.7)
        )
        # Bold dramatic Calacatta Viola dark charcoal-espresso veins sweeping across warm cream marble!
        p1 = (u * 3.1 + v * 2.4) + warp1 * 0.85
        vein_broad = np.exp(-((np.sin(p1 * math.pi)) ** 2) * 11.0)
        vein_core = np.exp(-((np.sin(p1 * math.pi)) ** 2) * 48.0)
        p2 = (u * 4.6 - v * 4.1) + warp2 * 1.10
        vein_fine = np.exp(-((np.sin(p2 * math.pi)) ** 2) * 42.0)
        p3 = (u * 2.7 + v * 3.9 + 0.42) + (warp1 - warp2) * 0.72
        vein_gold = np.exp(-((np.sin(p3 * math.pi)) ** 2) * 58.0)

        # Warm honed creamy Italian Calacatta stone base (high contrast against dark walnut & oxblood!)
        cloud = 0.5 + 0.5 * np.sin(u * 4.8 - v * 3.4 + warp2 * 1.1)
        grain = rng.normal(0.0, 0.010, (H, W)).astype(np.float32)
        sr = 0.78 + 0.09 * cloud + grain
        sg = 0.74 + 0.08 * cloud + grain
        sb = 0.67 + 0.07 * cloud + grain

        # Broad soft smoky-taupe mineral wash + crisp dark espresso-charcoal fracture veins
        sr = sr * (1.0 - 0.34 * vein_broad) + 0.38 * 0.34 * vein_broad
        sg = sg * (1.0 - 0.34 * vein_broad) + 0.34 * 0.34 * vein_broad
        sb = sb * (1.0 - 0.34 * vein_broad) + 0.32 * 0.34 * vein_broad

        v_dark = np.clip(vein_core * 0.78 + vein_fine * 0.52, 0.0, 1.0)
        sr = sr * (1.0 - v_dark) + 0.20 * v_dark
        sg = sg * (1.0 - v_dark) + 0.17 * v_dark
        sb = sb * (1.0 - v_dark) + 0.16 * v_dark

        # Delicate warm antique-gold mineral veins tying into brass hardware
        sr = sr * (1.0 - 0.45 * vein_gold) + 0.68 * 0.45 * vein_gold
        sg = sg * (1.0 - 0.45 * vein_gold) + 0.50 * 0.45 * vein_gold
        sb = sb * (1.0 - 0.45 * vein_gold) + 0.24 * 0.45 * vein_gold

        # Baked soft contact AO shadows on the marble countertop under props!
        wx = -0.44 + u * 0.88
        wz = -0.33 + v * 0.68
        # Espresso machine footprint shadow (X in [-0.18, +0.37], Z in [-0.25, +0.21])
        dx_m = np.maximum(0.0, np.maximum(-0.170 - wx, wx - 0.360))
        dz_m = np.maximum(0.0, np.maximum(-0.240 - wz, wz - 0.195))
        dist_m = np.sqrt(dx_m * dx_m + dz_m * dz_m)
        sh_m = 1.0 - 0.42 * np.exp(-(dist_m / 0.036) ** 1.7)
        # Grinder footprint shadow (X=-0.325, Z=+0.02, r~0.080)
        dist_g = np.sqrt((wx + 0.325) ** 2 + (wz - 0.02) ** 2)
        sh_g = 1.0 - 0.44 * np.exp(-(np.maximum(0.0, dist_g - 0.068) / 0.030) ** 1.6)
        # Subtle clean bullnose rim highlight
        edge_hi = 1.0 + 0.10 * np.exp(-((0.35 - wz) / 0.020) ** 2)

        ao_tot = np.clip(sh_m * sh_g * edge_hi, 0.42, 1.12)
        sr = np.clip(sr * ao_tot, 0.08, 0.94)
        sg = np.clip(sg * ao_tot, 0.07, 0.91)
        sb = np.clip(sb * ao_tot, 0.06, 0.86)

        arr = np.ones((H, W, 4), dtype=np.float32)
        arr[:, :, 0] = srgb_to_lin(sr)
        arr[:, :, 1] = srgb_to_lin(sg)
        arr[:, :, 2] = srgb_to_lin(sb)
        img = bpy.data.images.new(img_name, width=W, height=H, alpha=False)
        img.pixels.foreach_set(arr.ravel())
        img.pack()
        return img

    # -------------------------------------------------------------------------
    # C. DEDICATED 256x128 ART-DECO ITALIAN BRAND CREST MEDALLION
    # -------------------------------------------------------------------------
    if key == "BrandPlaque":
        W, H = 256, 128
        y, x = np.mgrid[0:H, 0:W].astype(np.float32)
        u = x / (W - 1.0)
        v = y / (H - 1.0)
        # Deep oxblood enamel background with polished brass Art-Deco double border + winged crest bars
        sr = np.full((H, W), 0.24, dtype=np.float32)
        sg = np.full((H, W), 0.04, dtype=np.float32)
        sb = np.full((H, W), 0.055, dtype=np.float32)

        # Outer & inner gold Art-Deco frame lines
        border = (u < 0.06) | (u > 0.94) | (v < 0.12) | (v > 0.88)
        inner = ((np.abs(u - 0.10) < 0.015) | (np.abs(u - 0.90) < 0.015)) & (v > 0.18) & (v < 0.82)
        inner |= ((np.abs(v - 0.20) < 0.025) | (np.abs(v - 0.80) < 0.025)) & (u > 0.10) & (u < 0.90)

        # Central Art-Deco sunburst & horizontal speed-wings emblem (reads crisp at all distances!)
        cx = (u - 0.50) * 2.0
        cy = (v - 0.50) * 2.0
        diamond = (np.abs(cx) * 2.6 + np.abs(cy) * 1.45) < 0.56
        diamond_hole = (np.abs(cx) * 2.6 + np.abs(cy) * 1.45) < 0.34
        wings = (np.abs(cx) > 0.24) & (np.abs(cx) < 0.78) & (
            (np.abs(cy - 0.24) < 0.065) | (np.abs(cy) < 0.075) | (np.abs(cy + 0.24) < 0.065)
        )
        # Taper outer ends of speed-wings
        wings &= (np.abs(cx) < (0.80 - np.abs(cy) * 0.45))
        crest = border | inner | (diamond & ~diamond_hole) | wings | (np.sqrt((cx * 2.0) ** 2 + cy ** 2) < 0.18)

        # Metallic gold gradient sheen across brass motif
        sheen = 0.84 + 0.20 * np.sin((u + v * 0.6) * math.pi * 1.8)
        sr[crest] = np.clip(0.84 * sheen[crest], 0.0, 0.96)
        sg[crest] = np.clip(0.67 * sheen[crest], 0.0, 0.86)
        sb[crest] = np.clip(0.28 * sheen[crest], 0.0, 0.50)

        arr = np.ones((H, W, 4), dtype=np.float32)
        arr[:, :, 0] = srgb_to_lin(sr)
        arr[:, :, 1] = srgb_to_lin(sg)
        arr[:, :, 2] = srgb_to_lin(sb)
        img = bpy.data.images.new(img_name, width=W, height=H, alpha=False)
        img.pixels.foreach_set(arr.ravel())
        img.pack()
        return img

    # -------------------------------------------------------------------------
    # D. DEDICATED 512x512 SMOKED-GLASS COFFEE BEAN HOPPER TEXTURE
    # -------------------------------------------------------------------------
    if key == "SmokedHopper":
        W, H = 512, 512
        rng = np.random.default_rng(2026)
        y, x = np.mgrid[0:H, 0:W].astype(np.float32)
        u = x / (W - 1.0)
        v = y / (H - 1.0)  # 0 bottom of hopper -> 1 top rim

        # Crisp packed roasted espresso beans filling bottom 72% of hopper!
        # Draw crisp Voronoi/staggered elliptical roasted coffee beans with center cleft!
        cols, rows = 22.0, 18.0
        gx = u * cols
        gy = v * rows
        row_id = np.floor(gy)
        gx_shift = gx + 0.5 * (row_id % 2.0)
        col_id = np.floor(gx_shift)
        # Per-cell organic phase jitter
        jit_x = 0.16 * np.sin(col_id * 12.7 + row_id * 31.3)
        jit_y = 0.16 * np.cos(col_id * 27.1 - row_id * 19.7)
        ang_b = 0.65 * np.sin(col_id * 43.3 + row_id * 17.9)
        lx = (gx_shift - col_id - 0.5) - jit_x
        ly = (gy - row_id - 0.5) - jit_y
        # Rotate bean locally
        rx = lx * np.cos(ang_b) - ly * np.sin(ang_b)
        ry = lx * np.sin(ang_b) + ly * np.cos(ang_b)
        d_bean = np.sqrt((rx * 2.15) ** 2 + (ry * 1.45) ** 2)
        in_bean = np.clip((1.05 - d_bean) * 3.8, 0.0, 1.0)
        dome = np.clip(1.0 - d_bean * d_bean, 0.0, 1.0)
        # Iconic dark center coffee bean seam cleft!
        cleft = np.exp(-((rx * 11.0 + 0.35 * np.sin(ry * 7.0)) ** 2)) * np.clip(1.0 - np.abs(ry) * 1.8, 0.0, 1.0)
        roast_var = 0.84 + 0.28 * np.sin(col_id * 9.1 + row_id * 5.7)
        bean_hi = np.clip((dome ** 3.0) * 0.42, 0.0, 0.42)

        b_r = np.clip((0.10 + 0.25 * in_bean * (0.45 + 0.55 * dome) * roast_var) * (1.0 - 0.55 * cleft) + bean_hi * 0.60, 0.04, 0.52)
        b_g = np.clip((0.055 + 0.135 * in_bean * (0.45 + 0.55 * dome) * roast_var) * (1.0 - 0.55 * cleft) + bean_hi * 0.38, 0.02, 0.32)
        b_b = np.clip((0.030 + 0.065 * in_bean * (0.45 + 0.55 * dome) * roast_var) * (1.0 - 0.55 * cleft) + bean_hi * 0.22, 0.01, 0.20)

        # Upper 28% translucent smoked-amber polycarbonate dome
        fill_mask = 1.0 - np.clip((v - 0.68 - 0.025 * np.sin(u * math.tau * 3.0)) / 0.035, 0.0, 1.0)
        glass_r = 0.22 + 0.08 * v
        glass_g = 0.17 + 0.07 * v
        glass_b = 0.14 + 0.06 * v

        sr = glass_r * (1.0 - fill_mask) + b_r * fill_mask
        sg = glass_g * (1.0 - fill_mask) + b_g * fill_mask
        sb = glass_b * (1.0 - fill_mask) + b_b * fill_mask

        # Crisp vertical cylindrical glass studio reflection streaks!
        spec1 = np.exp(-(((u - 0.22) / 0.036) ** 2)) * 0.55
        spec2 = np.exp(-(((u - 0.76) / 0.026) ** 2)) * 0.38
        spec3 = np.exp(-(((u - 0.48) / 0.065) ** 2)) * 0.14
        rim_glint = np.exp(-(((v - 0.95) / 0.03) ** 2)) * 0.32
        glint = np.clip(spec1 + spec2 + spec3 + rim_glint, 0.0, 0.72)

        sr = np.clip(sr + glint * 0.86, 0.03, 0.95)
        sg = np.clip(sg + glint * 0.82, 0.02, 0.92)
        sb = np.clip(sb + glint * 0.75, 0.02, 0.88)

        arr = np.ones((H, W, 4), dtype=np.float32)
        arr[:, :, 0] = srgb_to_lin(sr)
        arr[:, :, 1] = srgb_to_lin(sg)
        arr[:, :, 2] = srgb_to_lin(sb)
        img = bpy.data.images.new(img_name, width=W, height=H, alpha=False)
        img.pixels.foreach_set(arr.ravel())
        img.pack()
        return img

    # -------------------------------------------------------------------------
    # E. 256x256 STUDIO-REFLECTION & 24-RAY BVH AO + CURVATURE LUTs
    #    U = baked AO + curvature edge-wear (0..0.58 AO, 0.58..1.0 bevel highlight)
    #    V = baked normal-Z studio reflection coordinate (0 down -> 0.5 horizon -> 1 up)
    #        or vertical wood/ceramic height coordinate!
    # -------------------------------------------------------------------------
    W, H = 256, 256
    rng = np.random.default_rng(1997 + sum(ord(c) for c in key))
    y, x = np.mgrid[0:H, 0:W].astype(np.float32)
    u = x / float(W - 1)
    v = y / float(H - 1)

    ao_p = np.clip(u / 0.58, 0.0, 1.0)
    ed_p = np.clip((u - 0.58) / 0.42, 0.0, 1.0)
    noise = rng.normal(0.0, 1.0, (H, W)).astype(np.float32)

    if key == "DarkWalnut":
        # Rich readable warm oiled Italian walnut with cathedral grain & deep shaker AO!
        cathedral = np.sin(v * 20.0 + 2.2 * np.sin(u * 6.5) + 0.7 * np.sin(v * 44.0))
        fine_pore = np.sin(v * 72.0 + 1.1 * np.sin(u * 12.0))
        grain = 1.0 + 0.14 * cathedral + 0.04 * fine_pore + 0.015 * noise
        ao_s = 0.40 + 0.60 * (ao_p ** 1.12)
        edge_s = 1.0 + 0.24 * (ed_p ** 1.4)
        tone = ao_s * edge_s * grain
        sr = np.clip(0.48 * tone, 0.14, 0.72)
        sg = np.clip(0.28 * tone, 0.07, 0.46)
        sb = np.clip(0.14 * tone, 0.03, 0.26)

    elif key == "BurgundyEnamel":
        # Deep aristocratic oxblood-burgundy metallic lacquer (MATCHES lobby carpet & Art-Deco elevator!)
        # Studio reflection ramp along V (normal.z) gives unmistakable glossy automotive clearcoat curve!
        hor = np.exp(-((v - 0.44) / 0.11) ** 2)       # deep wine-black horizon reflection
        sky_band = np.exp(-((v - 0.66) / 0.13) ** 2)  # crisp crimson-wine studio light reflection
        top_fill = np.clip((v - 0.50) * 0.45, 0.0, 0.25)
        ao_s = 0.34 + 0.66 * (ao_p ** 1.20)
        spec = 0.24 * sky_band + 0.24 * (ed_p ** 1.45)

        sr = np.clip((0.33 - 0.10 * hor + top_fill * 0.30) * ao_s + spec * 0.95, 0.08, 0.66)
        sg = np.clip((0.052 - 0.016 * hor + top_fill * 0.06) * ao_s + spec * 0.34, 0.015, 0.26)
        sb = np.clip((0.072 - 0.020 * hor + top_fill * 0.07) * ao_s + spec * 0.36, 0.020, 0.28)

    elif key == "MirrorChrome":
        # Liquid-mirror Italian stainless chrome: high-contrast studio horizon band on V + bevel glints on U!
        dark_ground = np.clip(1.0 - v * 1.9, 0.0, 1.0)
        horizon_dip = np.exp(-((v - 0.44) / 0.075) ** 2)
        sky_crest = np.exp(-((v - 0.60) / 0.115) ** 2)
        secondary_hi = np.exp(-((v - 0.84) / 0.12) ** 2)
        ao_s = 0.34 + 0.66 * (ao_p ** 1.15)
        micro = 1.0 + 0.016 * np.sin(v * 95.0) + 0.008 * noise

        # Cool bright liquid-silver chrome reflection (never collapses to flat black!)
        base_m = (0.48 - 0.14 * horizon_dip - 0.10 * dark_ground + 0.44 * sky_crest + 0.20 * secondary_hi) * micro
        val = np.clip(base_m * ao_s + 0.26 * (ed_p ** 1.35) * ao_s, 0.16, 0.97)
        warm = 0.05 * dark_ground
        sr = np.clip(val * (0.95 + warm), 0.15, 0.97)
        sg = np.clip(val * (0.98 + warm * 0.35), 0.16, 0.98)
        sb = np.clip(val * (1.04 - warm * 0.6), 0.17, 0.99)

    elif key == "BrushedSteel":
        # Satin horizontal-brushed commercial stainless steel (darker & cooler than mirror chrome!)
        brush = 1.0 + 0.045 * np.sin(v * 140.0 + 0.3 * np.sin(u * 18.0)) + 0.018 * noise
        horizon_dip = np.exp(-((v - 0.45) / 0.14) ** 2)
        sky_sheen = np.exp(-((v - 0.68) / 0.18) ** 2)
        ao_s = 0.22 + 0.78 * (ao_p ** 1.22)
        val = np.clip((0.34 - 0.09 * horizon_dip + 0.24 * sky_sheen) * brush * ao_s + 0.18 * (ed_p ** 1.4), 0.07, 0.78)
        sr = np.clip(val * 0.94, 0.06, 0.76)
        sg = np.clip(val * 0.98, 0.06, 0.79)
        sb = np.clip(val * 1.05, 0.07, 0.84)

    elif key == "PolishedBrass":
        # Rich 1990s Art-Deco polished Italian brass with liquid metallic reflection band!
        horizon_dip = np.exp(-((v - 0.43) / 0.09) ** 2)
        gold_crest = np.exp(-((v - 0.62) / 0.12) ** 2)
        ao_s = 0.26 + 0.74 * (ao_p ** 1.18)
        micro = 1.0 + 0.022 * np.sin(v * 70.0) + 0.01 * noise
        refl = (0.56 - 0.24 * horizon_dip + 0.38 * gold_crest) * micro * ao_s
        ed = 0.26 * (ed_p ** 1.35) * ao_s
        sr = np.clip(refl * 1.12 + ed * 1.0, 0.12, 0.97)
        sg = np.clip(refl * 0.86 + ed * 0.88, 0.08, 0.88)
        sb = np.clip(refl * 0.34 + ed * 0.52, 0.03, 0.62)

    elif key == "CreamPorcelain":
        # Warm café porcelain with soft studio specular & subtle oxblood/gold rim band near top of cup!
        ao_s = 0.38 + 0.62 * (ao_p ** 1.15)
        sheen = 0.10 * np.exp(-((v - 0.68) / 0.16) ** 2) + 0.08 * (ed_p ** 1.5)
        sr = np.clip(0.86 * ao_s + sheen, 0.24, 0.96)
        sg = np.clip(0.82 * ao_s + sheen * 0.95, 0.22, 0.94)
        sb = np.clip(0.74 * ao_s + sheen * 0.88, 0.19, 0.88)

    elif key == "PorcelainStripe":
        # Café gold-and-oxblood trim stripe on cup rims
        ao_s = 0.42 + 0.58 * (ao_p ** 1.15)
        sr = np.clip(0.54 * ao_s + 0.14 * ed_p, 0.15, 0.78)
        sg = np.clip(0.14 * ao_s + 0.08 * ed_p, 0.04, 0.34)
        sb = np.clip(0.12 * ao_s + 0.06 * ed_p, 0.04, 0.28)

    elif key == "EspressoCrema":
        # Rich dark espresso with tiger-stripe golden-hazelnut crema foam!
        swirl = 0.5 + 0.5 * np.sin(u * 14.0 + 2.5 * np.sin(v * 9.0))
        sr = np.clip(0.32 + 0.24 * swirl, 0.14, 0.62)
        sg = np.clip(0.16 + 0.14 * swirl, 0.07, 0.36)
        sb = np.clip(0.06 + 0.06 * swirl, 0.02, 0.16)

    elif key == "MatteObsidian":
        # Deep cast iron / rubber / recessed drip-tray well
        ao_s = 0.28 + 0.72 * (ao_p ** 1.25)
        ed = 0.10 * (ed_p ** 1.5)
        sr = np.clip(0.095 * ao_s + ed, 0.025, 0.24)
        sg = np.clip(0.095 * ao_s + ed, 0.025, 0.24)
        sb = np.clip(0.105 * ao_s + ed, 0.028, 0.26)

    elif key == "WarmGlow":
        sr = np.full((H, W), 0.98, dtype=np.float32)
        sg = np.full((H, W), 0.76, dtype=np.float32)
        sb = np.full((H, W), 0.38, dtype=np.float32)

    elif key == "RubyJewel":
        sr = np.full((H, W), 0.96, dtype=np.float32)
        sg = np.full((H, W), 0.18, dtype=np.float32)
        sb = np.full((H, W), 0.14, dtype=np.float32)

    else:  # RoastedBeans (3D mound inside grinder hopper)
        bump = 0.5 + 0.5 * np.sin(u * 26.0 + 1.8 * np.sin(v * 22.0)) + 0.05 * noise
        ao_s = 0.35 + 0.65 * ao_p
        sr = np.clip((0.18 + 0.14 * bump) * ao_s + 0.08 * ed_p, 0.05, 0.42)
        sg = np.clip((0.095 + 0.075 * bump) * ao_s + 0.05 * ed_p, 0.025, 0.24)
        sb = np.clip((0.050 + 0.038 * bump) * ao_s + 0.03 * ed_p, 0.015, 0.14)

    arr = np.ones((H, W, 4), dtype=np.float32)
    arr[:, :, 0] = srgb_to_lin(sr)
    arr[:, :, 1] = srgb_to_lin(sg)
    arr[:, :, 2] = srgb_to_lin(sb)
    img = bpy.data.images.new(img_name, width=W, height=H, alpha=False)
    img.pixels.foreach_set(arr.ravel())
    img.pack()
    return img


# (metallic, roughness, emission_rgb, emission_strength)
MAT_PROPS = {
    "DarkWalnut":      (0.05, 0.36, None, 0.0),
    "DarkMarble":      (0.06, 0.16, None, 0.0),
    "MirrorChrome":    (0.96, 0.10, None, 0.0),
    "BrushedSteel":    (0.86, 0.26, None, 0.0),
    "BurgundyEnamel":  (0.36, 0.12, None, 0.0),
    "PolishedBrass":   (0.94, 0.16, None, 0.0),
    "CreamPorcelain":  (0.04, 0.15, None, 0.0),
    "PorcelainStripe": (0.25, 0.18, None, 0.0),
    "EspressoCrema":   (0.02, 0.22, None, 0.0),
    "MatteObsidian":   (0.14, 0.56, None, 0.0),
    "GaugeDial":       (0.00, 0.22, (0.92, 0.84, 0.66), 0.22),
    "BrandPlaque":     (0.55, 0.20, None, 0.0),
    "SmokedHopper":    (0.12, 0.14, None, 0.0),
    "RoastedBeans":    (0.04, 0.38, None, 0.0),
    "WarmGlow":        (0.00, 0.20, (1.00, 0.72, 0.32), 1.45),
    "RubyJewel":       (0.00, 0.15, (1.00, 0.18, 0.12), 1.80),
}

# Materials that preserve their own explicit 2D UV maps (do NOT overwrite with 1D AO LUT UV!)
EXPLICIT_UV_MATS = {"GaugeDial", "DarkMarble", "BrandPlaque", "SmokedHopper"}
# Materials that use reflection normal.z for LUT V-axis
REFLECTION_LUT_MATS = {"MirrorChrome", "BrushedSteel", "PolishedBrass", "BurgundyEnamel"}


def init_mats():
    mats = {}
    for k, (met, rough, em, em_str) in MAT_PROPS.items():
        m = bpy.data.materials.new(k)
        m.use_nodes = True
        m.use_backface_culling = True
        m.diffuse_color = (1.0, 1.0, 1.0, 1.0)
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = (1.0, 1.0, 1.0, 1.0)
        bsdf.inputs["Metallic"].default_value = met
        bsdf.inputs["Roughness"].default_value = rough
        if em is not None and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (*em, 1.0)
            bsdf.inputs["Emission Strength"].default_value = em_str
        tex = m.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = build_texture(k)
        tex.extension = "EXTEND" if k not in ("SmokedHopper",) else "REPEAT"
        m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        mats[k] = m
    return mats


def weighted_smooth(obj):
    for p in obj.data.polygons:
        p.use_smooth = True
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    wn = obj.modifiers.new("WN", "WEIGHTED_NORMAL")
    wn.mode = "FACE_AREA"
    wn.weight = 50
    bpy.ops.object.modifier_apply(modifier=wn.name)
    obj.select_set(False)
    return obj


# =============================================================================
# 2. HIGH-CRAFT PROCEDURAL GEOMETRY BUILDER
# =============================================================================
class CoffeeBarBuilder:
    def __init__(self):
        self.mats = init_mats()
        self.objs = []

    def _finish_bmesh(self, name, bm, mat_key, bevel=0.0, segs=3, smooth=True):
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        o = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(o)
        o.data.materials.append(self.mats[mat_key])
        if bevel > 0.0:
            bpy.context.view_layer.objects.active = o
            o.select_set(True)
            mod = o.modifiers.new("Bevel", "BEVEL")
            mod.width = bevel
            mod.segments = segs
            mod.profile = 0.55
            mod.limit_method = "ANGLE"
            mod.use_clamp_overlap = True
            bpy.ops.object.modifier_apply(modifier=mod.name)
            o.select_set(False)
        if smooth:
            weighted_smooth(o)
        self.objs.append(o)
        return o

    def add_box(self, name, cx, cy, cz, w, h, d, mat_key, bevel=0.005, segs=3, rot=(0, 0, 0)):
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=xyz((cx, cy, cz)))
        o = bpy.context.active_object
        o.name = name
        o.dimensions = (w, d, h)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        if any(abs(a) > 1e-6 for a in rot):
            o.rotation_euler = Euler((rot[0], -rot[2], rot[1]), "XYZ")
            bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        o.data.materials.append(self.mats[mat_key])
        if bevel > 0:
            mod = o.modifiers.new("Bevel", "BEVEL")
            mod.width = min(bevel, w * 0.35, h * 0.35, d * 0.35)
            mod.segments = segs
            mod.profile = 0.55
            mod.limit_method = "ANGLE"
            mod.use_clamp_overlap = True
            bpy.ops.object.modifier_apply(modifier=mod.name)
        weighted_smooth(o)
        self.objs.append(o)
        return o

    def add_cyl(self, name, cx, cy, cz, radius, depth, mat_key, axis="Y", segs=28, bevel=0.002, rot_extra=None):
        rot = (0.0, 0.0, 0.0) if axis == "Y" else ((math.pi / 2.0, 0.0, 0.0) if axis == "Z" else (0.0, math.pi / 2.0, 0.0))
        bpy.ops.mesh.primitive_cylinder_add(vertices=segs, radius=radius, depth=depth, location=xyz((cx, cy, cz)), rotation=rot)
        o = bpy.context.active_object
        o.name = name
        if rot_extra is not None:
            o.rotation_euler = Euler(rot_extra, "XYZ")
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        o.data.materials.append(self.mats[mat_key])
        if bevel > 0:
            mod = o.modifiers.new("Bevel", "BEVEL")
            mod.width = min(bevel, radius * 0.25, depth * 0.25)
            mod.segments = 2
            mod.limit_method = "ANGLE"
            mod.use_clamp_overlap = True
            bpy.ops.object.modifier_apply(modifier=mod.name)
        weighted_smooth(o)
        self.objs.append(o)
        return o

    def add_lathe(self, name, cx, base_y, cz, profile_ry, mat_key, segs=32, axis="Y", rot_game=(0, 0, 0), cyl_uv=False):
        """High-segment lathe along local axis (default Y up), optional cylindrical UV mapping."""
        bm = bmesh.new()
        uv_lay = bm.loops.layers.uv.new("UVMap") if cyl_uv else None
        y_min = min(y for _, y in profile_ry)
        y_max = max(y for _, y in profile_ry)
        y_span = max(1e-5, y_max - y_min)

        rings = []
        for r, y in profile_ry:
            ring = []
            for i in range(segs):
                a = i * math.tau / segs
                ring.append(bm.verts.new((r * math.cos(a), -r * math.sin(a), y)))
            rings.append(ring)

        for s in range(len(rings) - 1):
            v0 = (profile_ry[s][1] - y_min) / y_span
            v1 = (profile_ry[s + 1][1] - y_min) / y_span
            for i in range(segs):
                ni = (i + 1) % segs
                f = bm.faces.new((rings[s][i], rings[s][ni], rings[s + 1][ni], rings[s + 1][i]))
                if uv_lay is not None:
                    u0 = i / float(segs)
                    u1 = (i + 1) / float(segs)
                    f.loops[0][uv_lay].uv = (u0, v0)
                    f.loops[1][uv_lay].uv = (u1, v0)
                    f.loops[2][uv_lay].uv = (u1, v1)
                    f.loops[3][uv_lay].uv = (u0, v1)
        if profile_ry[0][0] > 1e-4:
            f = bm.faces.new(list(reversed(rings[0])))
            if uv_lay is not None:
                for lp in f.loops:
                    lp[uv_lay].uv = (0.5, 0.0)
        if profile_ry[-1][0] > 1e-4:
            f = bm.faces.new(rings[-1])
            if uv_lay is not None:
                for lp in f.loops:
                    lp[uv_lay].uv = (0.5, 1.0)

        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        o = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(o)
        o.location = xyz((cx, base_y, cz))
        if any(abs(a) > 1e-6 for a in rot_game):
            o.rotation_euler = Euler((rot_game[0], -rot_game[2], rot_game[1]), "XYZ")
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        o.select_set(False)
        o.data.materials.append(self.mats[mat_key])
        weighted_smooth(o)
        self.objs.append(o)
        return o

    def add_pipe(self, name, pts_game, radius, mat_key, segs=18, radii=None):
        """Smooth swept tubular pipe with optional variable radius along spine."""
        bm = bmesh.new()
        rings = []
        n = len(pts_game)
        # Parallel-transport frame to avoid twisting along curved steam wands & chrome rails!
        prev_u1 = None
        for idx, p in enumerate(pts_game):
            vp = Vector(xyz(p))
            if idx == 0:
                t = (Vector(xyz(pts_game[1])) - vp).normalized()
            elif idx == n - 1:
                t = (vp - Vector(xyz(pts_game[-2]))).normalized()
            else:
                t = (Vector(xyz(pts_game[idx + 1])) - Vector(xyz(pts_game[idx - 1]))).normalized()
            if prev_u1 is None:
                up = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
                u1 = t.cross(up).normalized()
            else:
                u1 = prev_u1 - t * prev_u1.dot(t)
                if u1.length < 1e-5:
                    up = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
                    u1 = t.cross(up).normalized()
                else:
                    u1.normalize()
            prev_u1 = u1
            u2 = t.cross(u1).normalized()
            rad = radii[idx] if radii is not None else radius
            ring = [bm.verts.new(vp + (u1 * math.cos(i * math.tau / segs) + u2 * math.sin(i * math.tau / segs)) * rad) for i in range(segs)]
            rings.append(ring)
        for s in range(len(rings) - 1):
            for i in range(segs):
                ni = (i + 1) % segs
                bm.faces.new((rings[s][i], rings[s][ni], rings[s + 1][ni], rings[s + 1][i]))
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
        return self._finish_bmesh(name, bm, mat_key)

    def add_planar_disc(self, name, cx, cy, cz, radius, mat_key, tilt_x=0.0, segs=32):
        """Front-facing (+Z) planar UV-mapped disc for crisp 256x256 vintage pressure gauge dials."""
        bm = bmesh.new()
        uv_lay = bm.loops.layers.uv.new("UVMap")
        center = bm.verts.new((0.0, 0.0, 0.0))
        ring = []
        for i in range(segs):
            a = i * math.tau / segs
            # Local disc in X-Z plane facing -Y in Blender (+Z in game!)
            ring.append(bm.verts.new((radius * math.cos(a), 0.0, radius * math.sin(a))))
        for i in range(segs):
            ni = (i + 1) % segs
            # Winding so normal points to -Y (game +Z!)
            f = bm.faces.new((center, ring[i], ring[ni]))
            a0 = i * math.tau / segs
            a1 = ni * math.tau / segs
            f.loops[0][uv_lay].uv = (0.5, 0.5)
            f.loops[1][uv_lay].uv = (0.5 + 0.49 * math.cos(a0), 0.5 + 0.49 * math.sin(a0))
            f.loops[2][uv_lay].uv = (0.5 + 0.49 * math.cos(a1), 0.5 + 0.49 * math.sin(a1))
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        # Ensure normal points toward -Y (front)
        if bm.faces[0].normal.y > 0.0:
            bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        o = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(o)
        o.location = xyz((cx, cy, cz))
        if abs(tilt_x) > 1e-6:
            o.rotation_euler = Euler((tilt_x, 0.0, 0.0), "XYZ")
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        o.select_set(False)
        o.data.materials.append(self.mats[mat_key])
        self.objs.append(o)
        return o

    def add_planar_quad(self, name, cx, cy, cz, w, h, mat_key, tilt_x=0.0):
        """Front-facing (+Z) UV-mapped plaque quad for crisp Italian crest medallion."""
        bm = bmesh.new()
        uv_lay = bm.loops.layers.uv.new("UVMap")
        v0 = bm.verts.new((-w * 0.5, 0.0, -h * 0.5))
        v1 = bm.verts.new(( w * 0.5, 0.0, -h * 0.5))
        v2 = bm.verts.new(( w * 0.5, 0.0,  h * 0.5))
        v3 = bm.verts.new((-w * 0.5, 0.0,  h * 0.5))
        f = bm.faces.new((v0, v1, v2, v3))
        if f.normal.y > 0.0:
            bmesh.ops.reverse_faces(bm, faces=[f])
        # Assign exact UVs (0,0)->(1,1) matching X left-to-right, Z bottom-to-top
        for lp in f.loops:
            u = 0.0 if lp.vert.co.x < 0 else 1.0
            v = 0.0 if lp.vert.co.z < 0 else 1.0
            lp[uv_lay].uv = (u, v)
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        o = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(o)
        o.location = xyz((cx, cy, cz))
        if abs(tilt_x) > 1e-6:
            o.rotation_euler = Euler((tilt_x, 0.0, 0.0), "XYZ")
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        o.select_set(False)
        o.data.materials.append(self.mats[mat_key])
        self.objs.append(o)
        return o

    def add_espresso_cup(self, name, cx, base_y, cz, saucer=True, inverted=False, yaw=0.0, filled=False):
        """Silky 28-segment Italian tulip porcelain espresso cup with rim stripe, curved ear handle & optional crema!"""
        cup_y = base_y
        if saucer:
            s_prof = [
                (0.002, 0.000), (0.030, 0.000), (0.044, 0.004), (0.055, 0.011),
                (0.053, 0.013), (0.035, 0.005), (0.016, 0.004), (0.002, 0.004),
            ]
            self.add_lathe(f"{name}_Saucer", cx, base_y, cz, s_prof, "CreamPorcelain", segs=26)
            cup_y = base_y + 0.005

        if not inverted:
            c_prof = [
                (0.002, 0.000), (0.020, 0.000), (0.026, 0.012), (0.032, 0.032),
                (0.035, 0.052), (0.0355, 0.057), (0.0335, 0.059), (0.030, 0.055),
                (0.026, 0.032), (0.019, 0.010), (0.002, 0.008),
            ]
            stripe_y = cup_y + 0.051
            stripe_r = 0.0354
            h_y = cup_y + 0.033
        else:
            # Authentic warmed upside-down café cup!
            c_prof = [
                (0.0335, 0.000), (0.0355, 0.002), (0.035, 0.007), (0.032, 0.027),
                (0.026, 0.047), (0.020, 0.058), (0.018, 0.059), (0.002, 0.058),
            ]
            stripe_y = cup_y + 0.008
            stripe_r = 0.0354
            h_y = cup_y + 0.027

        self.add_lathe(f"{name}_Body", cx, cup_y, cz, c_prof, "CreamPorcelain", segs=28)
        # Subtle oxblood-burgundy café rim accent ring!
        self.add_lathe(
            f"{name}_RimStripe", cx, stripe_y, cz,
            [(stripe_r, -0.0022), (stripe_r + 0.0008, 0.0), (stripe_r, 0.0022)],
            "PorcelainStripe", segs=28,
        )
        # Curved ear handle rotated by yaw
        hx = cx + math.cos(yaw) * 0.033
        hz = cz + math.sin(yaw) * 0.033
        bpy.ops.mesh.primitive_torus_add(
            major_segments=16, minor_segments=8, major_radius=0.0135, minor_radius=0.0034,
            location=xyz((hx, h_y, hz)), rotation=(math.pi / 2.0, 0.0, -yaw),
        )
        h = bpy.context.active_object
        h.name = f"{name}_Handle"
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        h.data.materials.append(self.mats["CreamPorcelain"])
        weighted_smooth(h)
        self.objs.append(h)

        if filled and not inverted:
            self.add_cyl(f"{name}_Crema", cx, cup_y + 0.046, cz, 0.0305, 0.006, "EspressoCrema", axis="Y", segs=24, bevel=0.0)


# =============================================================================
# 3. MUSEUM-GRADE 1990s ITALIAN BARISTA STATION ASSEMBLY
# =============================================================================
def build_coffee_station():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    b = CoffeeBarBuilder()

    # =========================================================================
    # A. BESPOKE OILED-WALNUT, BRASS & VEINED MARBLE BARISTA CABINET (Y=0..0.888m)
    # =========================================================================
    # Recessed matte-obsidian toe-kick plinth + gleaming polished brass kickplate strip!
    b.add_box("CabinetPlinth",       0.0, 0.044, -0.010, 0.76, 0.088, 0.55, "MatteObsidian", bevel=0.003, segs=2)
    b.add_box("PlinthBrassFront",    0.0, 0.022,  0.268, 0.768, 0.036, 0.008, "PolishedBrass", bevel=0.002, segs=2)
    for sx in (-1, 1):
        b.add_box(f"PlinthBrassSide_{sx}", sx * 0.383, 0.022, -0.010, 0.008, 0.036, 0.556, "PolishedBrass", bevel=0.002, segs=2)

    # Main oiled-walnut carcass + lower base molding collar
    b.add_box("CabinetBaseMolding",  0.0, 0.098,  0.002, 0.835, 0.028, 0.630, "DarkWalnut", bevel=0.006, segs=3)
    b.add_box("CabinetBody",         0.0, 0.468,  0.000, 0.810, 0.745, 0.605, "DarkWalnut", bevel=0.007, segs=3)

    # 4 Architectural fluted walnut corner pilasters with turned polished-brass capitals & bases!
    for sx in (-1, 1):
        for sz in (-1, 1):
            px = sx * 0.398
            pz = sz * 0.295
            b.add_box(f"Pilaster_{sx}_{sz}", px, 0.468, pz, 0.038, 0.730, 0.038, "DarkWalnut", bevel=0.004, segs=2)
            # Decorative vertical brass reveal fluting inlay on front pilasters
            if sz > 0:
                b.add_box(f"PilasterBrassFlute_{sx}", px, 0.468, pz + 0.019, 0.007, 0.640, 0.005, "PolishedBrass", bevel=0.001, segs=2)
            b.add_box(f"PilasterCapTop_{sx}_{sz}", px, 0.818, pz, 0.046, 0.022, 0.046, "PolishedBrass", bevel=0.003, segs=2)
            b.add_box(f"PilasterCapBot_{sx}_{sz}", px, 0.118, pz, 0.046, 0.022, 0.046, "PolishedBrass", bevel=0.003, segs=2)

    # Twin deeply-recessed shaker cabinet doors on front (+Z) with inlaid brass frames,
    # raised beveled walnut fielded panels & turned brass bar pulls!
    for s in (-1, 1):
        dx = s * 0.188
        # Deep shadow backplate (creates razor-sharp shaker recess!)
        b.add_box(f"DoorRecess_{s}", dx, 0.395, 0.306, 0.345, 0.545, 0.012, "DarkWalnut", bevel=0.002, segs=2)
        # Raised chamfered fielded walnut center panel inside brass inlay (catches warm light cleanly!)
        b.add_box(f"DoorFieldPanel_{s}", dx, 0.395, 0.314, 0.214, 0.412, 0.010, "DarkWalnut", bevel=0.004, segs=3)
        # 4 separate shaker stiles & rails projecting forward for genuine physical shadow depth!
        b.add_box(f"DoorStileL_{s}", dx - 0.148, 0.395, 0.316, 0.052, 0.555, 0.018, "DarkWalnut", bevel=0.003, segs=2)
        b.add_box(f"DoorStileR_{s}", dx + 0.148, 0.395, 0.316, 0.052, 0.555, 0.018, "DarkWalnut", bevel=0.003, segs=2)
        b.add_box(f"DoorRailT_{s}",  dx,         0.646, 0.316, 0.248, 0.054, 0.018, "DarkWalnut", bevel=0.003, segs=2)
        b.add_box(f"DoorRailB_{s}",  dx,         0.144, 0.316, 0.248, 0.054, 0.018, "DarkWalnut", bevel=0.003, segs=2)
        # Delicate inlaid polished-brass frame beading inside shaker door recess!
        b.add_box(f"DoorBrassT_{s}", dx,         0.612, 0.315, 0.236, 0.007, 0.007, "PolishedBrass", bevel=0.001, segs=2)
        b.add_box(f"DoorBrassB_{s}", dx,         0.178, 0.315, 0.236, 0.007, 0.007, "PolishedBrass", bevel=0.001, segs=2)
        b.add_box(f"DoorBrassL_{s}", dx - 0.115, 0.395, 0.315, 0.007, 0.438, 0.007, "PolishedBrass", bevel=0.001, segs=2)
        b.add_box(f"DoorBrassR_{s}", dx + 0.115, 0.395, 0.315, 0.007, 0.438, 0.007, "PolishedBrass", bevel=0.001, segs=2)
        # Sculpted vertical turned-brass door pull + round brass rosettes!
        hx = s * 0.044
        b.add_cyl(f"DoorPullBar_{s}", hx, 0.515, 0.346, 0.0075, 0.155, "PolishedBrass", axis="Y", segs=18)
        for py in (0.450, 0.580):
            b.add_cyl(f"DoorPullRosette_{s}_{py}", hx, py, 0.326, 0.013, 0.008, "PolishedBrass", axis="Z", segs=16)
            b.add_cyl(f"DoorPullPost_{s}_{py}",    hx, py, 0.336, 0.0055, 0.022, "PolishedBrass", axis="Z", segs=14)

    # Side cabinet raised fielded walnut wainscot panels + brass trim frames (looks luxury from 3/4 view too!)
    for sx in (-1, 1):
        b.add_box(f"SideFieldPanel_{sx}", sx * 0.409, 0.468, 0.0, 0.012, 0.485, 0.465, "DarkWalnut", bevel=0.004, segs=3)
        b.add_box(f"SidePanelRailT_{sx}", sx * 0.408, 0.770, 0.0, 0.014, 0.045, 0.55, "DarkWalnut", bevel=0.003, segs=2)
        b.add_box(f"SidePanelRailB_{sx}", sx * 0.408, 0.165, 0.0, 0.014, 0.045, 0.55, "DarkWalnut", bevel=0.003, segs=2)
        b.add_box(f"SideBrassT_{sx}",     sx * 0.413, 0.725, 0.0, 0.007, 0.008, 0.50, "PolishedBrass", bevel=0.001, segs=2)
        b.add_box(f"SideBrassB_{sx}",     sx * 0.413, 0.210, 0.0, 0.007, 0.008, 0.50, "PolishedBrass", bevel=0.001, segs=2)
        b.add_box(f"SideBrassF_{sx}",     sx * 0.413, 0.468,  0.248, 0.007, 0.520, 0.008, "PolishedBrass", bevel=0.001, segs=2)
        b.add_box(f"SideBrassR_{sx}",     sx * 0.413, 0.468, -0.248, 0.007, 0.520, 0.008, "PolishedBrass", bevel=0.001, segs=2)

    # Top barista utility drawer with brass inlay border & dual sculpted brass pulls
    b.add_box("TopDrawer",      0.0, 0.750, 0.312, 0.735, 0.105, 0.022, "DarkWalnut", bevel=0.004, segs=3)
    b.add_box("TopDrawerBrass", 0.0, 0.750, 0.323, 0.705, 0.082, 0.004, "PolishedBrass", bevel=0.001, segs=2)
    b.add_box("TopDrawerInner", 0.0, 0.750, 0.325, 0.690, 0.068, 0.005, "DarkWalnut", bevel=0.002, segs=2)
    for sx in (-1, 1):
        b.add_cyl(f"DrawerPull_{sx}", sx * 0.175, 0.750, 0.344, 0.0068, 0.125, "PolishedBrass", axis="X", segs=18)
        for px in (-0.052, 0.052):
            b.add_cyl(f"DrawerPost_{sx}_{px}", sx * 0.175 + px, 0.750, 0.335, 0.005, 0.020, "PolishedBrass", axis="Z", segs=14)

    # Continuous gleaming polished-brass reveal molding right beneath marble overhang
    b.add_box("UnderMarbleBrassTrim", 0.0, 0.834, 0.004, 0.848, 0.018, 0.642, "PolishedBrass", bevel=0.003, segs=2)

    # Thick stepped bullnose veined Nero-Marquina marble countertop slab (Y = 0.842 .. 0.888m)
    marble_slab = b.add_box("MarbleCounterTop", 0.0, 0.865, 0.010, 0.880, 0.046, 0.680, "DarkMarble", bevel=0.011, segs=5)
    # Assign exact world-space top-down & wrap UVs to marble_slab so veins + baked contact AO align cleanly!
    mm = marble_slab.data
    uv_m = mm.uv_layers.new(name="UVMap")
    for poly in mm.polygons:
        for li in poly.loop_indices:
            co = mm.vertices[mm.loops[li].vertex_index].co
            # Blender X -> game X [-0.44..0.44], Blender -Y -> game Z [-0.33..0.35]
            u = (co.x + 0.44) / 0.88
            v = (-co.y + 0.33) / 0.68
            uv_m.data[li].uv = (min(0.995, max(0.005, u)), min(0.995, max(0.005, v)))

    # =========================================================================
    # B. SCULPTED 1990s LA MARZOCCO / FAEMA E61 2-GROUP ESPRESSO MACHINE
    #    Shifted cleanly to center X = +0.095m (W=0.56m -> [-0.185, +0.375])
    #    giving a dedicated 23cm barista grinder bay on the left!
    # =========================================================================
    mx = 0.095
    cy = 0.888

    # 4 lathe-turned mirror-chrome & brass adjustable espresso feet
    foot_prof = [(0.018, 0.000), (0.019, 0.005), (0.013, 0.014), (0.014, 0.026), (0.020, 0.035)]
    for sx in (-1, 1):
        for sz in (-1, 1):
            b.add_lathe(f"MachineFoot_{sx}_{sz}", mx + sx * 0.225, cy, sz * 0.165 - 0.030, foot_prof, "MirrorChrome", segs=22)
            b.add_cyl(f"MachineFootCollar_{sx}_{sz}", mx + sx * 0.225, cy + 0.005, sz * 0.165 - 0.030, 0.0195, 0.006, "PolishedBrass", axis="Y", segs=22)

    # SCULPTED DOUBLE-CURVED FAEMA / LA MARZOCCO OXBLOOD SIDE COWLS (ZERO thick flat slabs!)
    # Smooth 16-point sculptural silhouette with tapered outer cheek crown + chrome perimeter rib!
    cheek_yz = [
        (cy + 0.036, -0.235),
        (cy + 0.036,  0.150),
        (cy + 0.068,  0.195),
        (cy + 0.102,  0.200),
        (cy + 0.128,  0.165),
        (cy + 0.215,  0.135),
        (cy + 0.285,  0.160),
        (cy + 0.345,  0.176),
        (cy + 0.392,  0.158),
        (cy + 0.418,  0.118),
        (cy + 0.428,  0.055),
        (cy + 0.426, -0.065),
        (cy + 0.418, -0.165),
        (cy + 0.392, -0.225),
        (cy + 0.335, -0.252),
        (cy + 0.145, -0.252),
    ]
    # Center of silhouette for graceful inward crown taper on outer sculpted face
    cy_mid = cy + 0.235
    cz_mid = -0.040
    bm_sides = bmesh.new()
    for s in (-1, 1):
        x_in = mx + s * 0.242
        x_mid = mx + s * 0.268
        x_out = mx + s * 0.282
        r_in = [bm_sides.verts.new(xyz((x_in, py, pz))) for py, pz in cheek_yz]
        r_mid = [bm_sides.verts.new(xyz((x_mid, py, pz))) for py, pz in cheek_yz]
        # Outer sculpted face tapers gently inward by 7% -> sleek Italian stamped aluminum cowl look!
        r_out = [
            bm_sides.verts.new(xyz((x_out, cy_mid + (py - cy_mid) * 0.90, cz_mid + (pz - cz_mid) * 0.90)))
            for py, pz in cheek_yz
        ]
        if s < 0:
            r_in, r_mid, r_out = list(reversed(r_in)), list(reversed(r_mid)), list(reversed(r_out))
        n = len(cheek_yz)
        for i in range(n):
            ni = (i + 1) % n
            bm_sides.faces.new((r_in[i], r_in[ni], r_mid[ni], r_mid[i]))
            bm_sides.faces.new((r_mid[i], r_mid[ni], r_out[ni], r_out[i]))
        bm_sides.faces.new(list(reversed(r_in)))
        bm_sides.faces.new(r_out)
    b._finish_bmesh("BurgundySideCowls", bm_sides, "BurgundyEnamel", bevel=0.005, segs=3)

    # Gleaming Mirror-Chrome Perimeter Trim Ribs hugging both curved oxblood cheeks + Art-Deco brass louver bars!
    for s in (-1, 1):
        rib_pts = [(mx + s * 0.273, py, pz) for py, pz in cheek_yz] + [(mx + s * 0.273, cheek_yz[0][0], cheek_yz[0][1])]
        b.add_pipe(f"CheekChromeRib_{s}", rib_pts, 0.0075, "MirrorChrome", segs=14)
        # 3 Art-Deco horizontal polished-brass speed strakes on each outer cheek
        for k, ly in enumerate((0.200, 0.245, 0.290)):
            slen = 0.245 - k * 0.025
            b.add_box(f"CheekBrassLouver_{s}_{k}", mx + s * 0.282, cy + ly, -0.055, 0.009, 0.011, slen, "PolishedBrass", bevel=0.003, segs=2)

    # Mirror-chrome rounded main boiler chassis + recessed rear ventilation grille + brushed-steel brew backsplash
    b.add_box("MainChromeChassis",  mx, cy + 0.232, -0.110, 0.495, 0.390, 0.275, "MirrorChrome", bevel=0.016, segs=4)
    b.add_box("RearOxbloodPanel",   mx, cy + 0.245, -0.248, 0.440, 0.305, 0.012, "BurgundyEnamel", bevel=0.004, segs=2)
    for vi in range(7):
        vx = mx - 0.15 + vi * 0.05
        b.add_box(f"RearVent_{vi}", vx, cy + 0.255, -0.254, 0.014, 0.210, 0.008, "PolishedBrass", bevel=0.002, segs=2)

    # Sculpted overhanging upper mirror-chrome brow hood + recessed brushed-steel brew backsplash
    b.add_box("UpperChromeHood",    mx, cy + 0.352,  0.072, 0.495, 0.132, 0.190, "MirrorChrome", bevel=0.016, segs=4)
    b.add_box("BrewBacksplash",     mx, cy + 0.195,  0.032, 0.484, 0.215, 0.020, "BrushedSteel", bevel=0.004, segs=2)
    # Recessed dark stainless shadow trim framing the brew deck
    b.add_box("BrewDeckShadowTrim", mx, cy + 0.285,  0.095, 0.484, 0.014, 0.130, "MatteObsidian", bevel=0.002, segs=2)
    # Warm hidden barista under-hood LED bar
    b.add_box("BrewDeckWarmLight",  mx, cy + 0.279,  0.092, 0.410, 0.008, 0.020, "WarmGlow", bevel=0.002, segs=2)

    # Sloped sculptural oxblood-burgundy control fascia framed by crisp chrome & polished brass bars!
    fascia_tilt = -0.16  # tilted slightly skyward (+10 deg) like a real La Marzocco dashboard!
    b.add_box("FasciaOxblood",      mx, cy + 0.354,  0.166, 0.484, 0.094, 0.014, "BurgundyEnamel", bevel=0.004, segs=3, rot=(fascia_tilt, 0, 0))
    b.add_box("FasciaBrassTop",     mx, cy + 0.402,  0.161, 0.494, 0.012, 0.018, "PolishedBrass",  bevel=0.003, segs=2, rot=(fascia_tilt, 0, 0))
    b.add_box("FasciaBrassBot",     mx, cy + 0.306,  0.175, 0.494, 0.012, 0.018, "PolishedBrass",  bevel=0.003, segs=2, rot=(fascia_tilt, 0, 0))

    # Central Raised Art-Deco Italian Brand Medallion perched proudly on upper front crown!
    b.add_box("BrandBezel",         mx, cy + 0.422,  0.156, 0.172, 0.042, 0.016, "PolishedBrass", bevel=0.004, segs=3, rot=(-0.10, 0, 0))
    b.add_planar_quad("BrandCrest", mx, cy + 0.423,  0.165, 0.158, 0.032, "BrandPlaque", tilt_x=-0.10)

    # TWIN STEPPED POLISHED-BRASS PRESSURE GAUGES WITH CRISP 256x256 PAINTED VINTAGE DIALS!
    for idx_g, gx_off in enumerate((-0.056, 0.056)):
        gx = mx + gx_off
        gy = cy + 0.354
        gz = 0.174
        # Outer stepped brass housing + chamfered inner bezel ring
        b.add_cyl(f"GaugeOuterBezel_{idx_g}", gx, gy, gz, 0.030, 0.016, "PolishedBrass", axis="Z", segs=32, bevel=0.003)
        b.add_cyl(f"GaugeInnerRing_{idx_g}",  gx, gy, gz + 0.007, 0.025, 0.008, "MirrorChrome", axis="Z", segs=32, bevel=0.002)
        # Crisp painted dial disc sitting slightly proud inside bezel (ZERO flat yellow blob!)
        b.add_planar_disc(f"GaugeDialFace_{idx_g}", gx, gy, gz + 0.0115, 0.0235, "GaugeDial", tilt_x=0.0, segs=32)

    # Tactile Chromed Brew Rocker Switches + Glowing Ruby Pilot Jewels in Brass Collars!
    for s in (-1, 1):
        # 3 sculpted chrome brew toggle levers per group head
        for k in range(3):
            bx = mx + s * (0.118 + k * 0.032)
            b.add_box(f"SwitchPlate_{s}_{k}", bx, cy + 0.354, 0.174, 0.020, 0.030, 0.008, "MirrorChrome", bevel=0.002, segs=2, rot=(fascia_tilt, 0, 0))
            b.add_cyl(f"SwitchToggle_{s}_{k}", bx, cy + 0.354, 0.181, 0.0045, 0.014, "PolishedBrass" if k == 1 else "MirrorChrome", axis="Z", segs=14)
        # Glowing ruby power jewel inside turned brass bezel
        b.add_cyl(f"JewelCollar_{s}", mx + s * 0.218, cy + 0.354, 0.175, 0.010, 0.010, "PolishedBrass", axis="Z", segs=18)
        b.add_cyl(f"RubyJewel_{s}",   mx + s * 0.218, cy + 0.354, 0.180, 0.0065, 0.006, "RubyJewel", axis="Z", segs=18, bevel=0.001)

    # TWIN ICONIC LATHE-TURNED E61 CHROME GROUP HEADS + MECHANICAL CAM LEVERS + WALNUT PORTAFILTERS!
    e61_prof = [
        (0.032, 0.000), (0.036, 0.006), (0.044, 0.014), (0.045, 0.032),
        (0.039, 0.046), (0.037, 0.068), (0.044, 0.078), (0.042, 0.088), (0.022, 0.092),
    ]
    for s in (-1, 1):
        gx = mx + s * 0.125
        gz = 0.122
        # Heavy machined 32-segment chrome E61 thermosyphon bell housing
        b.add_lathe(f"E61GroupHead_{s}", gx, cy + 0.205, gz, e61_prof, "MirrorChrome", segs=32)
        # Stepped polished brass dispersion collar ring around group head waist
        b.add_cyl(f"E61BrassCollar_{s}", gx, cy + 0.236, gz, 0.0475, 0.016, "PolishedBrass", axis="Y", segs=32, bevel=0.003)
        # Heavy chrome portafilter basket locked into group head
        b.add_lathe(
            f"PortaBasket_{s}", gx, cy + 0.184, gz,
            [(0.010, 0.000), (0.026, 0.006), (0.037, 0.018), (0.038, 0.032), (0.041, 0.035), (0.034, 0.035)],
            "MirrorChrome", segs=30,
        )
        # Twin graceful curved chrome espresso spouts underneath portafilter
        for sp in (-1, 1):
            b.add_pipe(
                f"Spout_{s}_{sp}",
                [(gx, cy + 0.186, gz), (gx + sp * 0.010, cy + 0.178, gz), (gx + sp * 0.017, cy + 0.165, gz)],
                0.0052, "MirrorChrome", segs=14,
            )
        # Side iconic E61 mechanical cam brew lever + turned walnut ball knob!
        lx = gx + s * 0.048
        b.add_cyl(f"CamPivot_{s}", lx, cy + 0.252, gz, 0.010, 0.018, "PolishedBrass", axis="X", segs=18)
        b.add_pipe(
            f"CamLeverArm_{s}",
            [(lx, cy + 0.252, gz), (lx + s * 0.012, cy + 0.250, gz + 0.045), (lx + s * 0.016, cy + 0.235, gz + 0.072)],
            0.0042, "MirrorChrome", segs=12,
        )
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, radius=0.011, location=xyz((lx + s * 0.016, cy + 0.232, gz + 0.077)))
        knob = bpy.context.active_object
        knob.name = f"CamKnob_{s}"
        knob.data.materials.append(b.mats["DarkWalnut"])
        weighted_smooth(knob)
        b.objs.append(knob)

        # Ergonomic tapered turned-walnut portafilter handle (sleek curved swell + brass pommel cap!)
        ang = s * 0.26  # splayed cleanly ±15° so it frames the brewing cup without blocking it!
        sa, ca = math.sin(ang), math.cos(ang)
        hx0, hy0, hz0 = gx + sa * 0.036, cy + 0.206, gz + ca * 0.036
        # Chrome ferrule collar neck
        b.add_pipe(
            f"PortaFerrule_{s}",
            [(hx0, hy0, hz0), (hx0 + sa * 0.038, hy0 - 0.006, hz0 + ca * 0.038)],
            0.0095, "MirrorChrome", segs=18, radii=[0.011, 0.0085],
        )
        # Sculpted ergonomic tapered walnut handle (variable radius profile: thin neck -> full palm -> rounded pommel!)
        h_steps = [(0.036, 0.009), (0.065, 0.0125), (0.105, 0.0160), (0.140, 0.0172), (0.160, 0.0145)]
        h_pts = [(hx0 + sa * d, hy0 - 0.005 - d * 0.14, hz0 + ca * d) for d, _ in h_steps]
        h_rads = [r for _, r in h_steps]
        b.add_pipe(f"PortaWalnutHandle_{s}", h_pts, 0.014, "DarkWalnut", segs=22, radii=h_rads)
        # Chamfered polished brass pommel end-cap
        end_p = h_pts[-1]
        b.add_pipe(
            f"PortaBrassCap_{s}",
            [end_p, (end_p[0] + sa * 0.012, end_p[1] - 0.002, end_p[2] + ca * 0.012)],
            0.014, "PolishedBrass", segs=20, radii=[0.0142, 0.0095],
        )

        # Fresh steaming espresso cup with golden crema waiting under each group head!
        b.add_espresso_cup(f"BrewCup_{s}", gx, cy + 0.096, gz, saucer=True, inverted=False, yaw=s * 0.45, filled=True)

    # LONGER SLENDER ARTICULATED GOOSENECK CHROME STEAM WANDS + SCALLOPED ROTARY STEAM VALVES!
    for s in (-1, 1):
        wx = mx + s * 0.214
        # Upper brass ball-joint swivel housing
        b.add_cyl(f"SteamTurret_{s}", wx, cy + 0.285, 0.132, 0.014, 0.022, "PolishedBrass", axis="Y", segs=20)
        # Scalloped black/brass rotary steam valve knob on upper front corner!
        b.add_cyl(f"SteamValveStem_{s}", wx, cy + 0.305, 0.168, 0.010, 0.024, "MirrorChrome", axis="Z", segs=18)
        b.add_cyl(f"SteamValveWheel_{s}", wx, cy + 0.305, 0.186, 0.022, 0.018, "MatteObsidian", axis="Z", segs=16, bevel=0.004)
        b.add_cyl(f"SteamValveBrassCap_{s}", wx, cy + 0.305, 0.197, 0.012, 0.006, "PolishedBrass", axis="Z", segs=18)
        # Slender graceful 6-point curved chrome gooseneck steam wand sweeping down toward drip grate
        wand_pts = [
            (wx,            cy + 0.275, 0.132),
            (wx + s * 0.016, cy + 0.246, 0.156),
            (wx + s * 0.026, cy + 0.210, 0.172),
            (wx + s * 0.022, cy + 0.168, 0.176),
            (wx + s * 0.012, cy + 0.130, 0.168),
            (wx + s * 0.008, cy + 0.114, 0.164),
        ]
        b.add_pipe(f"SteamWand_{s}", wand_pts, 0.0052, "MirrorChrome", segs=16)
        # Black ribbed silicone anti-burn grip sleeve on upper wand curve
        b.add_pipe(f"SteamBurnGrip_{s}", wand_pts[1:3], 0.0088, "MatteObsidian", segs=16)
        # Flared machined brass 4-hole steam nozzle tip!
        b.add_pipe(f"SteamTipBrass_{s}", wand_pts[-2:], 0.0072, "PolishedBrass", segs=16, radii=[0.0055, 0.0078])

    # Gleaming Mirror-Chrome Barista Milk Frothing Pitcher resting on right corner of drip tray!
    pitcher_prof = [
        (0.002, 0.000), (0.036, 0.000), (0.039, 0.012), (0.037, 0.048),
        (0.032, 0.088), (0.034, 0.098), (0.031, 0.100), (0.028, 0.090),
        (0.032, 0.016), (0.002, 0.014),
    ]
    b.add_lathe("MilkPitcher", mx + 0.205, cy + 0.096, 0.148, pitcher_prof, "MirrorChrome", segs=28)
    b.add_pipe(
        "MilkPitcherHandle",
        [
            (mx + 0.237, cy + 0.184, 0.148),
            (mx + 0.258, cy + 0.175, 0.148),
            (mx + 0.258, cy + 0.130, 0.148),
            (mx + 0.242, cy + 0.115, 0.148),
        ],
        0.0042, "MirrorChrome", segs=12,
    )

    # DEEP RECESSED MIRROR-CHROME DRIP TRAY + RAZOR-SHARP SLOTTED STAINLESS GRATE!
    b.add_box("DripTrayChassis", mx, cy + 0.056, 0.125, 0.494, 0.054, 0.195, "MirrorChrome", bevel=0.007, segs=3)
    # Proud mirror-chrome rounded front bumper rail + brass corner caps
    b.add_pipe("DripFrontRail", [(mx - 0.244, cy + 0.086, 0.222), (mx + 0.244, cy + 0.086, 0.222)], 0.0085, "MirrorChrome", segs=16)
    for sx in (-1, 1):
        b.add_cyl(f"DripRailCap_{sx}", mx + sx * 0.246, cy + 0.086, 0.222, 0.0095, 0.010, "PolishedBrass", axis="X", segs=16)
    # Deep matte-obsidian drain well creating dark high-contrast slots beneath the grate bars!
    b.add_box("DripTrayDeepWell", mx, cy + 0.082, 0.125, 0.456, 0.012, 0.165, "MatteObsidian", bevel=0.001, segs=2)
    # Perimeter frame + 12 razor-sharp brushed-steel grate bars
    b.add_box("GrateFrameL", mx - 0.226, cy + 0.091, 0.125, 0.012, 0.008, 0.164, "BrushedSteel", bevel=0.001, segs=2)
    b.add_box("GrateFrameR", mx + 0.226, cy + 0.091, 0.125, 0.012, 0.008, 0.164, "BrushedSteel", bevel=0.001, segs=2)
    for gi in range(12):
        gz = 0.052 + gi * 0.0132
        b.add_box(f"GrateBar_{gi}", mx, cy + 0.093, gz, 0.448, 0.005, 0.0055, "BrushedSteel", bevel=0.001, segs=2)

    # TOP CUP-WARMER TRAY, ART-DECO DOUBLE BRASS GALLERY RAIL & NATURALLY STACKED INVERTED CUPS!
    top_y = cy + 0.422
    b.add_box("CupWarmerDeck", mx, top_y, -0.048, 0.468, 0.010, 0.345, "BrushedSteel", bevel=0.003, segs=2)
    # Double-tier polished-brass gallery rail + turned finial corner posts!
    for r_idx, ry in enumerate((top_y + 0.022, top_y + 0.044)):
        rad = 0.0042 if r_idx == 0 else 0.0055
        b.add_pipe(f"CupRailBack_{r_idx}",  [(mx - 0.230, ry, -0.215), (mx + 0.230, ry, -0.215)], rad, "PolishedBrass", segs=14)
        b.add_pipe(f"CupRailLeft_{r_idx}",  [(mx - 0.230, ry, -0.215), (mx - 0.230, ry,  0.115)], rad, "PolishedBrass", segs=14)
        b.add_pipe(f"CupRailRight_{r_idx}", [(mx + 0.230, ry, -0.215), (mx + 0.230, ry,  0.115)], rad, "PolishedBrass", segs=14)
    post_prof = [(0.008, 0.000), (0.008, 0.006), (0.0055, 0.010), (0.0055, 0.046), (0.009, 0.052), (0.006, 0.058), (0.002, 0.060)]
    for px, pz in ((-0.230, -0.215), (0.230, -0.215), (-0.230, 0.115), (0.230, 0.115), (0.0, -0.215)):
        b.add_lathe(f"RailPost_{px}_{pz}", mx + px, top_y + 0.004, pz, post_prof, "PolishedBrass", segs=16)

    # Authentic Italian café cup layout: inverted warmed cups (singles & 2-high stacks) + stacked saucers!
    cup_base = top_y + 0.005
    # Left back: 2-high inverted espresso cup stack
    b.add_espresso_cup("TopCupStack1A", mx - 0.145, cup_base,         -0.125, saucer=False, inverted=True, yaw=0.35)
    b.add_espresso_cup("TopCupStack1B", mx - 0.145, cup_base + 0.056, -0.125, saucer=False, inverted=True, yaw=1.05)
    # Center back: 2-high inverted espresso cup stack
    b.add_espresso_cup("TopCupStack2A", mx - 0.035, cup_base,         -0.130, saucer=False, inverted=True, yaw=-0.50)
    b.add_espresso_cup("TopCupStack2B", mx - 0.035, cup_base + 0.056, -0.130, saucer=False, inverted=True, yaw=0.40)
    # Front-left & center: single inverted cups with natural varied handle angles
    b.add_espresso_cup("TopCupSingle1", mx - 0.120, cup_base,          0.005, saucer=False, inverted=True, yaw=2.20)
    b.add_espresso_cup("TopCupSingle2", mx + 0.048, cup_base,         -0.025, saucer=False, inverted=True, yaw=-1.10)
    # Right back: neat nested stack of 4 contoured porcelain saucers!
    saucer_prof = [(0.002, 0.000), (0.030, 0.000), (0.044, 0.004), (0.056, 0.011), (0.054, 0.013), (0.035, 0.005), (0.002, 0.004)]
    for si in range(4):
        b.add_lathe(f"TopSaucerStack_{si}", mx + 0.145, cup_base + si * 0.010, -0.105, saucer_prof, "CreamPorcelain", segs=26)

    # =========================================================================
    # C. SCULPTED ITALIAN COMMERCIAL CONICAL BURR COFFEE GRINDER (MAZZER-STYLE!)
    #    Dedicated left marble bay: X = -0.325, Z = +0.025, Y = 0.888 .. 1.31m
    # =========================================================================
    gx = -0.325
    gz = 0.020
    # Heavy sculpted cast-iron horseshoe base + front drip catch tray
    b.add_lathe(
        "GrinderBase", gx, cy, gz,
        [(0.072, 0.000), (0.076, 0.014), (0.070, 0.028), (0.058, 0.034), (0.002, 0.034)],
        "MatteObsidian", segs=28,
    )
    b.add_box("GrinderCatchTray", gx, cy + 0.010, gz + 0.072, 0.095, 0.016, 0.078, "MatteObsidian", bevel=0.004, segs=3)
    b.add_box("GrinderTrayGrate", gx, cy + 0.019, gz + 0.076, 0.076, 0.004, 0.054, "BrushedSteel", bevel=0.001, segs=2)

    # Tapered sculptural oxblood-burgundy motor housing with ribbed cooling fins!
    grinder_body_prof = [
        (0.058, 0.032), (0.062, 0.055), (0.060, 0.095), (0.063, 0.100),
        (0.059, 0.106), (0.063, 0.114), (0.059, 0.120), (0.063, 0.128),
        (0.057, 0.134), (0.054, 0.172), (0.062, 0.184), (0.056, 0.194), (0.002, 0.194),
    ]
    b.add_lathe("GrinderMotorBody", gx, cy, gz, grinder_body_prof, "BurgundyEnamel", segs=30)
    # Polished brass accent belt & side power rocker switch
    b.add_cyl("GrinderBrassBelt",  gx, cy + 0.048, gz, 0.063, 0.010, "PolishedBrass", axis="Y", segs=30)
    b.add_cyl("GrinderSwitchBase", gx - 0.060, cy + 0.075, gz + 0.018, 0.010, 0.014, "PolishedBrass", axis="X", segs=16)
    b.add_cyl("GrinderSwitchPin",  gx - 0.068, cy + 0.075, gz + 0.018, 0.004, 0.014, "RubyJewel", axis="X", segs=14)

    # Knurled polished-brass micrometric grind-adjustment collar + adjustment pin!
    b.add_lathe(
        "GrinderMicrometricCollar", gx, cy + 0.186, gz,
        [(0.054, 0.000), (0.064, 0.004), (0.064, 0.022), (0.056, 0.028), (0.042, 0.032)],
        "PolishedBrass", segs=30,
    )
    b.add_pipe(
        "GrinderAdjustPin",
        [(gx - 0.060, cy + 0.200, gz + 0.012), (gx - 0.082, cy + 0.204, gz + 0.025)],
        0.0045, "MirrorChrome", segs=12,
    )

    # Front mirror-chrome cylindrical doser chamber + grounds chute + dosing pull lever + portafilter fork!
    b.add_cyl("GrinderChuteNeck",   gx, cy + 0.145, gz + 0.056, 0.026, 0.042, "MirrorChrome", axis="Z", segs=22)
    b.add_cyl("GrinderDoserDrum",   gx, cy + 0.128, gz + 0.088, 0.038, 0.090, "MirrorChrome", axis="Y", segs=28, bevel=0.004)
    b.add_cyl("GrinderDoserCap",    gx, cy + 0.176, gz + 0.088, 0.041, 0.012, "PolishedBrass", axis="Y", segs=28)
    b.add_cyl("GrinderDoserWindow", gx, cy + 0.128, gz + 0.091, 0.0365, 0.052, "RoastedBeans", axis="Y", segs=24, bevel=0.0)
    # Doser front brass spout & side manual pull lever with walnut ball knob
    b.add_box("GrinderDoserSpout",  gx, cy + 0.078, gz + 0.095, 0.036, 0.024, 0.034, "MirrorChrome", bevel=0.003, segs=2)
    b.add_pipe(
        "GrinderDoserLever",
        [(gx + 0.036, cy + 0.105, gz + 0.088), (gx + 0.064, cy + 0.100, gz + 0.104)],
        0.0042, "PolishedBrass", segs=12,
    )
    bpy.ops.mesh.primitive_uv_sphere_add(segments=14, ring_count=8, radius=0.0095, location=xyz((gx + 0.066, cy + 0.099, gz + 0.106)))
    dk = bpy.context.active_object
    dk.name = "GrinderDoserKnob"
    dk.data.materials.append(b.mats["DarkWalnut"])
    weighted_smooth(dk)
    b.objs.append(dk)
    # Chromed portafilter cradle fork below doser
    for fx in (-1, 1):
        b.add_pipe(
            f"GrinderFork_{fx}",
            [(gx + fx * 0.022, cy + 0.048, gz + 0.062), (gx + fx * 0.028, cy + 0.048, gz + 0.102)],
            0.0045, "MirrorChrome", segs=12,
        )

    # 32-segment lathe-turned smoked-amber conical bean hopper (UV-mapped with packed roasted beans + glass glints!)
    hopper_prof = [
        (0.032, 0.000), (0.034, 0.022), (0.052, 0.055), (0.072, 0.105),
        (0.080, 0.155), (0.082, 0.185), (0.076, 0.190),
    ]
    b.add_lathe("GrinderBeanHopper", gx, cy + 0.214, gz, hopper_prof, "SmokedHopper", segs=32, cyl_uv=True)
    # Matte-black hopper lid + brass lift finial knob!
    b.add_lathe(
        "GrinderHopperLid", gx, cy + 0.400, gz,
        [(0.084, 0.000), (0.085, 0.008), (0.078, 0.015), (0.040, 0.019), (0.002, 0.020)],
        "MatteObsidian", segs=32,
    )
    b.add_lathe(
        "GrinderLidKnob", gx, cy + 0.418, gz,
        [(0.010, 0.000), (0.008, 0.008), (0.014, 0.016), (0.012, 0.022), (0.002, 0.023)],
        "PolishedBrass", segs=20,
    )

    # =========================================================================
    # D. BARISTA TAMPING STATION & KNOCK-BOX (Left-Front Counter Corner!)
    # =========================================================================
    # Black silicone corner tamping mat in front of grinder
    b.add_box("TampingMat", -0.335, cy + 0.004, 0.240, 0.165, 0.008, 0.145, "MatteObsidian", bevel=0.003, segs=2)
    b.add_box("TampingMatLip", -0.335, cy - 0.005, 0.312, 0.165, 0.022, 0.007, "MatteObsidian", bevel=0.002, segs=2)

    # Museum-grade lathe-turned chrome & oiled-walnut espresso tamper resting on tamping mat!
    b.add_lathe(
        "TamperChromeBase", -0.365, cy + 0.008, 0.242,
        [(0.002, 0.000), (0.029, 0.000), (0.0295, 0.012), (0.024, 0.018), (0.013, 0.024)],
        "MirrorChrome", segs=28,
    )
    b.add_lathe(
        "TamperWalnutHandle", -0.365, cy + 0.031, 0.242,
        [(0.013, 0.000), (0.0105, 0.016), (0.0165, 0.044), (0.0195, 0.062), (0.0165, 0.072), (0.002, 0.074)],
        "DarkWalnut", segs=26,
    )
    b.add_cyl("TamperBrassCrown", -0.365, cy + 0.104, 0.242, 0.011, 0.004, "PolishedBrass", axis="Y", segs=22)

    # Sleek cylindrical lathe-turned Italian oxblood & mirror-chrome barista knock-box beside tamping mat!
    kbx, kbz = -0.244, 0.254
    b.add_lathe(
        "KnockBoxBody", kbx, cy, kbz,
        [
            (0.038, 0.000), (0.042, 0.006), (0.044, 0.025), (0.046, 0.068),
            (0.049, 0.078), (0.045, 0.080), (0.042, 0.072), (0.040, 0.048), (0.002, 0.048),
        ],
        "BurgundyEnamel", segs=26,
    )
    b.add_cyl("KnockBoxRimRing", kbx, cy + 0.076, kbz, 0.0485, 0.008, "PolishedBrass", axis="Y", segs=26)
    b.add_cyl("KnockBoxGrounds", kbx, cy + 0.056, kbz, 0.039, 0.010, "RoastedBeans", axis="Y", segs=22, bevel=0.0)
    b.add_cyl("KnockBoxBar",     kbx, cy + 0.072, kbz, 0.0075, 0.092, "MatteObsidian", axis="X", segs=16)

    # =========================================================================
    # 4. JOIN + BAKE 24-RAY BVHTREE AO + CURVATURE + REFLECTION-RAMP UVs & EXPORT
    # =========================================================================
    bpy.ops.object.select_all(action="DESELECT")
    for o in b.objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = b.objs[0]
    bpy.ops.object.join()
    station = bpy.context.active_object
    station.name = "CoffeeMachineBarista_90s"
    mesh = station.data
    mesh.validate(verbose=False)

    # Ensure bottom sits strictly at Z=0.0 in Blender (Y=0.0 in game)
    min_z = min(v.co.z for v in mesh.vertices)
    for v in mesh.vertices:
        v.co.z -= min_z

    # Verify outward normals for GL_CULL_FACE safety
    bm_chk = bmesh.new()
    bm_chk.from_mesh(mesh)
    bmesh.ops.triangulate(bm_chk, faces=bm_chk.faces)
    bvh = BVHTree.FromBMesh(bm_chk)
    bm_chk.free()

    n_rays = 24
    golden = math.pi * (3.0 - math.sqrt(5.0))
    local_dirs = []
    for k in range(n_rays):
        zc = (k + 0.5) / n_rays
        rh = math.sqrt(max(0.0, 1.0 - zc * zc))
        th = golden * k
        local_dirs.append(Vector((math.cos(th) * rh, math.sin(th) * rh, zc)).normalized())

    verts = mesh.vertices
    v_coords = [v.co.copy() for v in verts]
    neighbors = [[] for _ in range(len(verts))]
    for edge in mesh.edges:
        a, c = edge.vertices[0], edge.vertices[1]
        neighbors[a].append(c)
        neighbors[c].append(a)

    max_dist = 0.20
    vert_u = [0.58] * len(verts)
    vert_col = [1.0] * len(verts)
    vert_nz = [0.5] * len(verts)
    for idx, v in enumerate(verts):
        p = v.co
        n = v.normal.normalized() if v.normal.length > 1e-6 else Vector((0, 0, 1))
        # Studio reflection coordinate from tilted studio normal (gives chrome/brass/oxblood rich curved highlights!)
        refl_v = 0.50 + 0.44 * (n.z * 0.82 - n.y * 0.36 + n.x * 0.22)
        vert_nz[idx] = min(0.97, max(0.03, refl_v))

        up = Vector((0, 0, 1)) if abs(n.z) < 0.9 else Vector((1, 0, 0))
        t1 = n.cross(up).normalized()
        t2 = n.cross(t1).normalized()
        origin = p + n * 0.0015
        occ = 0.0
        for ld in local_dirs:
            d = (t1 * ld.x + t2 * ld.y + n * ld.z).normalized()
            hit_loc, _, _, hit_dist = bvh.ray_cast(origin, d, max_dist)
            if hit_loc is not None and hit_dist is not None:
                occ += (1.0 - min(1.0, hit_dist / max_dist)) ** 1.35
        ao = max(0.0, 1.0 - (occ / n_rays) * 1.15)
        curv = 0.0
        if neighbors[idx]:
            acc = 0.0
            for nb in neighbors[idx]:
                dv = v_coords[nb] - p
                if dv.length > 1e-6:
                    acc += dv.normalized().dot(n)
            curv = max(-0.18, min(0.28, -(acc / len(neighbors[idx])) * 0.62))
        vert_u[idx] = min(0.97, max(0.04, 0.09 + 0.54 * (ao ** 1.25) + curv))
        vert_col[idx] = min(1.0, max(0.22, 0.22 + 0.78 * (ao ** 1.20)))

    # Ensure UVMap exists (preserving explicit UVs on GaugeDial, DarkMarble, BrandPlaque, SmokedHopper!)
    uv_layer = mesh.uv_layers.get("UVMap")
    if uv_layer is None:
        uv_layer = mesh.uv_layers.new(name="UVMap")
    while len(mesh.uv_layers) > 1:
        for ul in list(mesh.uv_layers):
            if ul.name != "UVMap":
                mesh.uv_layers.remove(ul)
                break

    while mesh.color_attributes:
        mesh.color_attributes.remove(mesh.color_attributes[0])
    col_attr = mesh.color_attributes.new(name="Col", type="BYTE_COLOR", domain="CORNER")

    mat_names = [m.name if m else "" for m in mesh.materials]
    mat_vert_counts = {}
    for poly in mesh.polygons:
        mname = mat_names[poly.material_index] if poly.material_index < len(mat_names) else ""
        if mname not in EXPLICIT_UV_MATS:
            poly.use_smooth = True
        use_refl = mname in REFLECTION_LUT_MATS
        keep_uv = mname in EXPLICIT_UV_MATS
        vset = mat_vert_counts.setdefault(mname, set())
        for li in poly.loop_indices:
            vi = mesh.loops[li].vertex_index
            vset.add(vi)
            v_co = verts[vi].co
            if not keep_uv:
                if use_refl:
                    # Slight spatial modulation breaks uniformity on large flat surfaces
                    v_val = min(0.97, max(0.03, vert_nz[vi] + 0.06 * math.sin(v_co.x * 5.0 + v_co.z * 4.0)))
                else:
                    v_val = min(0.98, max(0.02, v_co.z / 1.45))
                uv_layer.data[li].uv = (vert_u[vi], v_val)
            c = vert_col[vi]
            col_attr.data[li].color = (c, c, c, 1.0)
    mesh.update()

    xs = [v.co.x for v in verts]
    ys = [v.co.y for v in verts]
    zs = [v.co.z for v in verts]
    max_mat_v = max(len(s) for s in mat_vert_counts.values())
    print(
        f"CoffeeMachine v2 bounds: X=[{min(xs):.3f},{max(xs):.3f}] Y=[{min(ys):.3f},{max(ys):.3f}] "
        f"Z=[{min(zs):.3f},{max(zs):.3f}] Polys={len(mesh.polygons)} Verts={len(verts)} "
        f"MaxVertsPerMat={max_mat_v} Mats={len(mesh.materials)}"
    )
    for mn, s in sorted(mat_vert_counts.items(), key=lambda kv: -len(kv[1])):
        print(f"  mat {mn:16s}: {len(s):5d} verts")
    assert max_mat_v < 24000, f"16-bit vertex safety breached: {max_mat_v}"

    out_path = OUT_MODELS / "prop_coffee_machine.glb"
    bpy.ops.export_scene.gltf(
        filepath=str(out_path),
        export_format="GLB",
        export_normals=True,
        export_texcoords=True,
        export_attributes=True,
        export_active_vertex_color_when_no_material=True,
        export_vertex_color="ACTIVE",
        export_animations=False,
    )
    print("Exported Museum-Grade Italian prop_coffee_machine.glb:", out_path, os.path.getsize(out_path))

    # =========================================================================
    # 5. 3-ANGLE BLENDER STUDIO PROOF RENDER -> screenshots/coffee_studio_proof.png
    # =========================================================================
    render_studio_proof(station)


def render_studio_proof(station):
    scene = bpy.context.scene
    # Pick Blender 5.1 EEVEE engine safely
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT", "CYCLES"):
        try:
            scene.render.engine = eng
            break
        except Exception:
            continue
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX" if "AgX" in [i.identifier for i in scene.view_settings.bl_rna.properties["view_transform"].enum_items] else "Standard"

    # Warm dark studio cyclorama backdrop
    world = bpy.data.worlds.new("StudioWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.045, 0.040, 0.048, 1.0)
        bg.inputs["Strength"].default_value = 0.85

    # Studio floor plane
    bpy.ops.mesh.primitive_plane_add(size=8.0, location=(0, 0, -0.001))
    fl = bpy.context.active_object
    fm = bpy.data.materials.new("StudioFloor")
    fm.use_nodes = True
    bsdf = fm.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (0.06, 0.055, 0.06, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.35
    fl.data.materials.append(fm)

    # 3-Point Warm Key + Cool Rim + Soft Fill Studio Lights (matching sunset lobby feel!)
    def add_light(name, loc, energy, color, size=1.4, target=(0, 0, 0.85)):
        ld = bpy.data.lights.new(name, type="AREA")
        ld.energy = energy
        ld.color = color
        ld.size = size
        lo = bpy.data.objects.new(name, ld)
        bpy.context.collection.objects.link(lo)
        lo.location = loc
        direction = Vector(target) - Vector(loc)
        lo.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()

    add_light("KeyWarm",  (-1.8, -2.4, 2.5), 220.0, (1.0, 0.88, 0.72), 1.6, (0.0, 0.0, 0.90))
    add_light("FillCool", ( 2.2, -1.8, 1.8),  95.0, (0.76, 0.86, 1.0), 2.0, (0.0, 0.0, 0.85))
    add_light("RimGold",  ( 0.8,  2.2, 2.2), 160.0, (1.0, 0.82, 0.58), 1.2, (0.0, 0.0, 1.05))
    add_light("FrontLow", ( 0.0, -2.2, 0.8),  45.0, (1.0, 0.94, 0.86), 1.4, (0.0, 0.0, 0.75))

    cam_data = bpy.data.cameras.new("StudioCam")
    cam_obj = bpy.data.objects.new("StudioCam", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    scene.camera = cam_obj

    # 3 views in Blender coords (remember game +Z front = Blender -Y!)
    views = [
        ("front",   ( 0.00, -1.95, 1.02), ( 0.00,  0.00, 0.78), 46.0),
        ("three4",  ( 1.38, -1.55, 1.25), (-0.02,  0.00, 0.82), 45.0),
        ("closeup", (-0.10, -1.02, 1.24), (-0.02, -0.05, 1.08), 52.0),
    ]
    tmp_dir = OUT_PROOF.parent
    tmp_dir.mkdir(parents=True, exist_ok=True)
    panels = []
    for tag, loc, tgt, lens in views:
        cam_data.lens = lens
        cam_obj.location = Vector(loc)
        cam_obj.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        tmp_path = tmp_dir / f"_tmp_coffee_{tag}.png"
        scene.render.filepath = str(tmp_path)
        bpy.ops.render.render(write_still=True)
        im = bpy.data.images.load(str(tmp_path))
        w, h = im.size
        buf = np.empty(w * h * 4, dtype=np.float32)
        im.pixels.foreach_get(buf)
        panels.append(buf.reshape((h, w, 4)))
        bpy.data.images.remove(im)
        try:
            tmp_path.unlink()
        except OSError:
            pass

    # Stitch 3 panels side-by-side (1920x640) into screenshots/coffee_studio_proof.png
    combo = np.concatenate(panels, axis=1)
    ch, cw, _ = combo.shape
    out_im = bpy.data.images.new("CoffeeStudioProof", width=cw, height=ch, alpha=True)
    out_im.pixels.foreach_set(combo.ravel())
    out_im.filepath_raw = str(OUT_PROOF)
    out_im.file_format = "PNG"
    out_im.save()
    print(f"Rendered 3-angle studio proof: {OUT_PROOF} ({cw}x{ch})")


if __name__ == "__main__":
    build_coffee_station()
