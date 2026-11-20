"""Steam-Grade Stylized 3D Vibe Coder Character Pipeline v3.5 (Blender 5.1 + MPFB2 Real Human Anatomy).

Builds 12 completely distinct, tailored, anatomically sculpted articulated characters:
  assets/models/vc_0.glb .. assets/models/vc_11.glb

v3.5 Art-Direction & Craft Upgrades:
  - Real 3D sculpted luminous eyeballs placed at exact MPFB2 eye-socket centers (bright radial irises + catchlights, AO-exempt)
  - Exact face UV landmark alignment (brows, eye shadows, blush, scars & stubble centered dead-on MPFB2 orbital bones)
  - Continuous scalp-extruded stylized hair shells (ZERO meatball spheres!)
  - Tailored body-hugging curved shells (_mk_curved_shell) centered on real MPFB2 torso Y=-0.028 (ZERO hovering flat boxes!)
  - True polar-sculpted upturned 3-cornered Pirate Tricorne + arched plume, swept 7-blade Punk Mohawk, sleek curved Sleep Mask
    & side-tied Bathrobe Sash, wrap-around VR Visor, contoured Barista Apron & Kevlar Vest
  - Cohesive grounded Steam-indie color palettes (ZERO garish neon bands or front-of-leg racing stripes)
  - Seamless bone-weight overlap (weight >= 0.14) so elbows, wrists, shoulders, hips & knees never gap
"""
import math
import os
import sys
import numpy as np
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler
from mathutils.bvhtree import BVHTree

from bl_ext.blender_org.mpfb.services.humanservice import HumanService
from bl_ext.blender_org.mpfb.services.targetservice import TargetService

MODELS_DIR = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\assets\models"

PART_NAMES = [
    "Torso", "Head",
    "UpperArmL", "ForearmL",
    "UpperArmR", "ForearmR",
    "ThighL", "ShinL",
    "ThighR", "ShinR",
]

# Real MPFB2 torso Y center relative to pelvis pivot (chest front ~ -0.145, back ~ +0.085 -> center = -0.028)
TORSO_CY = -0.028

# 12 distinct, cohesive Steam-indie character recipes
RECIPES = [
    # 0: VIBE-PIRATE ("tab"/"stack") — Deep maritime navy captain coat, burgundy sash, sculpted tricorne, eyepatch, STEEL HOOK, OAK PEG-LEG
    dict(name="pirate", macro=(0.85, 0.55, 0.58, 0.42, 0.52),
         skin=(0.85, 0.65, 0.52), blush=(0.74, 0.36, 0.30), hair=(0.14, 0.09, 0.07),
         iris=(0.20, 0.56, 0.74), upper=(0.11, 0.17, 0.28), trim=(0.84, 0.66, 0.26),
         accent=(0.64, 0.15, 0.19), lower=(0.16, 0.15, 0.14), shoe=(0.24, 0.15, 0.10),
         pattern="pirate_coat", face_fx="pirate", glow=(0.28, 0.86, 0.82)),
    # 1: SMOOTHIE / CAFFEINE MANIAC ("div"/"smoothie") — Dusty lavender hoodie + contoured charcoal barista apron + iced matcha cup
    dict(name="smoothie", macro=(0.72, 0.22, 0.32, 0.34, 0.48),
         skin=(0.93, 0.77, 0.68), blush=(0.84, 0.48, 0.45), hair=(0.20, 0.13, 0.09),
         iris=(0.30, 0.66, 0.38), upper=(0.56, 0.46, 0.72), trim=(0.20, 0.24, 0.30),
         accent=(0.48, 0.72, 0.44), lower=(0.16, 0.19, 0.25), shoe=(0.90, 0.88, 0.84),
         pattern="barista_hoodie", face_fx="caffeine", glow=(0.58, 0.82, 0.44)),
    # 2: TINFOIL CRYPTO-BRO ("weekend"/"leverage") — Retro ochre-navy-teal windbreaker + Cuban gold chain + origami tinfoil cone
    dict(name="tinfoil", macro=(0.80, 0.32, 0.48, 0.46, 0.50),
         skin=(0.89, 0.70, 0.58), blush=(0.80, 0.42, 0.36), hair=(0.28, 0.19, 0.11),
         iris=(0.22, 0.62, 0.76), upper=(0.78, 0.56, 0.20), trim=(0.14, 0.17, 0.24),
         accent=(0.20, 0.62, 0.68), lower=(0.15, 0.17, 0.22), shoe=(0.82, 0.64, 0.24),
         pattern="windbreaker", face_fx="manic", glow=(0.28, 0.84, 0.92)),
    # 3: 40-FRAMEWORK WIZARD ("prompt"/"framework") — Celestial indigo velvet robe + silver beard + carved oak crystal staff
    dict(name="wizard", macro=(0.88, 0.78, 0.40, 0.58, 0.50),
         skin=(0.88, 0.70, 0.58), blush=(0.80, 0.44, 0.39), hair=(0.70, 0.72, 0.76),
         iris=(0.28, 0.66, 0.94), upper=(0.15, 0.21, 0.44), trim=(0.88, 0.70, 0.28),
         accent=(0.38, 0.80, 0.94), lower=(0.11, 0.15, 0.34), shoe=(0.28, 0.19, 0.13),
         pattern="wizard_stars", face_fx="elder", glow=(0.45, 0.86, 0.98)),
    # 4: SLEEPY TEAMLEAD ("call"/"deadline") — Dusty slate-teal waffle bathrobe + striped pajamas + curved silk sleep mask + slippers
    dict(name="sleepy", macro=(0.76, 0.48, 0.38, 0.56, 0.48),
         skin=(0.91, 0.75, 0.66), blush=(0.82, 0.48, 0.46), hair=(0.22, 0.16, 0.12),
         iris=(0.36, 0.46, 0.56), upper=(0.24, 0.48, 0.50), trim=(0.90, 0.87, 0.81),
         accent=(0.80, 0.52, 0.58), lower=(0.78, 0.83, 0.88), shoe=(0.91, 0.86, 0.82),
         pattern="bathrobe", face_fx="sleepy", glow=(0.86, 0.56, 0.66)),
    # 5: PUNK CYBORG REFACTORER ("refactor") — Distressed matte black biker vest + crimson under-tee + 7-fin mohawk + CYBER-CLAW
    dict(name="cyborg", macro=(0.92, 0.42, 0.72, 0.46, 0.54),
         skin=(0.86, 0.67, 0.55), blush=(0.76, 0.38, 0.33), hair=(0.12, 0.11, 0.13),
         iris=(0.90, 0.48, 0.16), upper=(0.14, 0.14, 0.17), trim=(0.58, 0.16, 0.20),
         accent=(0.88, 0.42, 0.14), lower=(0.17, 0.18, 0.21), shoe=(0.11, 0.11, 0.13),
         pattern="punk_leather", face_fx="punk", glow=(0.96, 0.46, 0.14)),
    # 6: ZEN MONK ("no-commits") — Warm saffron & charcoal linen monk robe + wooden prayer beads + closed eyes + GOLDEN HALO
    dict(name="zen", macro=(0.82, 0.82, 0.42, 0.45, 0.50),
         skin=(0.86, 0.68, 0.55), blush=(0.76, 0.42, 0.36), hair=(0.84, 0.85, 0.88),
         iris=(0.40, 0.35, 0.28), upper=(0.76, 0.44, 0.16), trim=(0.20, 0.19, 0.23),
         accent=(0.94, 0.78, 0.32), lower=(0.19, 0.18, 0.22), shoe=(0.40, 0.27, 0.16),
         pattern="monk_robe", face_fx="zen", glow=(0.98, 0.82, 0.32)),
    # 7: SKOLKOVO VR SHAMAN ("skolkovo") — Deep plum-slate techwear parka + wrap-around VR visor + SERVER JETPACK
    dict(name="skolkovo", macro=(0.78, 0.38, 0.52, 0.44, 0.52),
         skin=(0.89, 0.72, 0.62), blush=(0.79, 0.44, 0.42), hair=(0.15, 0.13, 0.18),
         iris=(0.82, 0.34, 0.92), upper=(0.22, 0.18, 0.34), trim=(0.13, 0.14, 0.18),
         accent=(0.82, 0.32, 0.78), lower=(0.14, 0.14, 0.18), shoe=(0.16, 0.16, 0.20),
         pattern="techwear", face_fx="vr", glow=(0.88, 0.34, 0.84)),
    # 8: MEETUP CROWN BOSS ("meetup") — Warm bone-cream quilted puffer jacket + GOLDEN CROWN + charcoal cargos + TRIPLE-S
    dict(name="meetup", macro=(0.75, 0.30, 0.46, 0.48, 0.52),
         skin=(0.88, 0.69, 0.56), blush=(0.80, 0.44, 0.38), hair=(0.16, 0.12, 0.10),
         iris=(0.34, 0.26, 0.18), upper=(0.88, 0.85, 0.79), trim=(0.16, 0.17, 0.20),
         accent=(0.78, 0.18, 0.26), lower=(0.22, 0.22, 0.25), shoe=(0.92, 0.90, 0.86),
         pattern="puffer", face_fx="smug", glow=(0.88, 0.24, 0.32)),
    # 9: FRIDAY 19:59 PROD DEMON ("friday") — Tailored oxblood pinstripe suit + curved obsidian HORNS + DEVIL TAIL
    dict(name="friday", macro=(0.88, 0.44, 0.62, 0.48, 0.54),
         skin=(0.85, 0.61, 0.53), blush=(0.74, 0.26, 0.24), hair=(0.10, 0.08, 0.10),
         iris=(0.98, 0.70, 0.16), upper=(0.44, 0.13, 0.16), trim=(0.13, 0.11, 0.13),
         accent=(0.94, 0.68, 0.22), lower=(0.13, 0.11, 0.14), shoe=(0.10, 0.09, 0.11),
         pattern="devil_suit", face_fx="demon", glow=(0.98, 0.66, 0.18)),
    # 10: LEGACY GHOST 1998 ("legacy") — Classic muted sage argyle cardigan + flush collar/tie + CRT MONITOR HEAD
    dict(name="legacy", macro=(0.80, 0.65, 0.40, 0.54, 0.48),
         skin=(0.89, 0.73, 0.63), blush=(0.79, 0.46, 0.42), hair=(0.35, 0.34, 0.32),
         iris=(0.35, 0.96, 0.52), upper=(0.26, 0.38, 0.29), trim=(0.88, 0.84, 0.75),
         accent=(0.64, 0.20, 0.18), lower=(0.54, 0.47, 0.37), shoe=(0.28, 0.20, 0.14),
         pattern="argyle_98", face_fx="crt", glow=(0.35, 0.98, 0.55)),
    # 11: TOWER SECURITY CHIEF ("sb_chief") — Matte navy tactical uniform + contoured Kevlar vest + peaked cap + chrome aviators
    dict(name="security", macro=(0.96, 0.60, 0.82, 0.62, 0.56),
         skin=(0.85, 0.66, 0.55), blush=(0.74, 0.38, 0.34), hair=(0.18, 0.17, 0.18),
         iris=(0.24, 0.36, 0.48), upper=(0.15, 0.18, 0.24), trim=(0.22, 0.25, 0.30),
         accent=(0.88, 0.70, 0.26), lower=(0.13, 0.15, 0.19), shoe=(0.09, 0.09, 0.11),
         pattern="kevlar", face_fx="stern", glow=(0.40, 0.76, 0.94)),
]


def srgb_to_lin(c):
    return tuple(c_i / 12.92 if c_i <= 0.04045 else ((c_i + 0.055) / 1.055) ** 2.4 for c_i in c)


# =============================================================================
# 1024x1024 PAINTED PBR TEXTURE ATLAS GENERATOR (Unique per archetype!)
#   Q0 (x:0..512,   y:0..512):   Face Complexion + Exact-Aligned Brows/Scars/Stubble
#   Q1 (x:512..1024,y:0..512):   Left: Radial Eye Iris + Catchlights | Right: Hair Strands
#   Q2 (x:0..512,   y:512..1024):Upper Outfit Fabric/Leather/Argyle/Stars/Kevlar
#   Q3 (x:512..1024,y:512..1024):Lower Garment Twill/Pajamas + Woodgrain + Brushed Steel
# =============================================================================
def build_character_atlas(idx, r):
    W, H = 1024, 1024
    arr = np.ones((H, W, 4), dtype=np.float32)
    rng = np.random.default_rng(1000 + idx * 97)

    # ---------------- Q0: Sculpted Stylized Face Map (x:0..512, y:0..512) ----------------
    # Exact MPFB2 face landmarks in (uf, vf) coords (mapped from co.x/0.22 & (co.z-0.125)/0.22):
    #   Eyes: |uf| ~ 0.226, vf ~ +0.200
    #   Brows: |uf| ~ 0.230, vf ~ +0.345
    #   Nose tip: uf = 0, vf ~ -0.052
    #   Lips: uf = 0, vf ~ -0.272
    yy, xx = np.mgrid[0:512, 0:512].astype(np.float32)
    uf = (xx - 256.0) / 180.0
    vf = (yy - 256.0) / 180.0
    skin = np.array(r["skin"], dtype=np.float32)
    blush_col = np.array(r["blush"], dtype=np.float32)
    hair_col = np.array(r["hair"], dtype=np.float32)
    q0 = np.empty((512, 512, 3), dtype=np.float32)
    q0[:, :, :] = skin
    q0 += rng.uniform(-0.012, 0.012, (512, 512, 1)).astype(np.float32)

    # Soft cheek warmth & nose tip blush
    cheek = np.exp(-(((np.abs(uf) - 0.28) / 0.22) ** 2 + ((vf + 0.02) / 0.16) ** 2))
    nose_tip = 0.40 * np.exp(-((uf / 0.10) ** 2 + ((vf + 0.05) / 0.08) ** 2))
    blush_m = np.clip(cheek * 0.36 + nose_tip * 0.26, 0.0, 0.55)[:, :, None]
    q0 = q0 * (1.0 - blush_m) + blush_col * blush_m

    # Subtle sculpted orbital & nasal contour shading (NOT black!)
    nose_sides = 0.16 * np.exp(-(((np.abs(uf) - 0.09) / 0.045) ** 2 + ((vf - 0.06) / 0.16) ** 2))
    nose_under = 0.22 * np.exp(-((uf / 0.09) ** 2 + ((vf + 0.12) / 0.05) ** 2))
    eye_socket = 0.16 * np.exp(-(((np.abs(uf) - 0.226) / 0.11) ** 2 + ((vf - 0.21) / 0.085) ** 2))
    jaw_shade = 0.14 * np.clip((-vf - 0.38) * 2.0, 0.0, 1.0)
    shade = np.clip(1.0 - nose_sides - nose_under - eye_socket - jaw_shade, 0.70, 1.0)[:, :, None]
    q0 *= shade

    # Nose bridge & brow-bone soft specular warmth
    hi = 0.07 * np.exp(-((uf / 0.048) ** 2 + ((vf - 0.04) / 0.15) ** 2)) + 0.05 * np.exp(-((uf / 0.30) ** 2 + ((vf - 0.44) / 0.14) ** 2))
    q0 = np.clip(q0 + hi[:, :, None], 0.0, 1.0)

    # Natural lips around vf = -0.27
    lip_m = np.exp(-((uf / 0.16) ** 2 + ((vf + 0.27) / 0.055) ** 2))
    lip_c = skin * 0.72 + np.array([0.22, 0.06, 0.06], dtype=np.float32)
    q0 = q0 * (1.0 - 0.62 * lip_m[:, :, None]) + lip_c * (0.62 * lip_m[:, :, None])

    fx = r["face_fx"]
    if fx in ("pirate", "stern", "punk", "sleepy", "manic"):
        stub = np.clip((0.10 - (vf + 0.34)) * 3.0, 0.0, 1.0) * np.exp(-((uf / 0.44) ** 2))
        stub_n = rng.uniform(0.68, 1.0, (512, 512)).astype(np.float32)
        strength = 0.34 if fx in ("pirate", "stern") else 0.19
        m = np.clip(stub * stub_n * strength, 0.0, 0.40)[:, :, None]
        q0 = q0 * (1.0 - m) + hair_col * m
    if fx == "pirate":
        d_scar = np.abs((uf + 0.26) * 0.78 + (vf - 0.04) * 0.62)
        scar_m = np.exp(-((d_scar / 0.015) ** 2)) * (np.abs(vf - 0.04) < 0.19)
        q0 = q0 * (1.0 - 0.70 * scar_m[:, :, None]) + np.array([0.50, 0.20, 0.18]) * (0.70 * scar_m[:, :, None])
    elif fx in ("caffeine", "sleepy"):
        bag = np.exp(-(((np.abs(uf) - 0.226) / 0.11) ** 2 + ((vf - 0.115) / 0.048) ** 2))
        bag_c = np.array([0.40, 0.32, 0.48], dtype=np.float32)
        amt = 0.52 if fx == "caffeine" else 0.42
        q0 = q0 * (1.0 - amt * bag[:, :, None]) + bag_c * (amt * bag[:, :, None])
    elif fx == "demon":
        emb = np.exp(-(((np.abs(uf) - 0.226) / 0.14) ** 2 + ((vf - 0.20) / 0.10) ** 2))
        q0 = q0 * (1.0 - 0.55 * emb[:, :, None]) + np.array([0.24, 0.06, 0.07]) * (0.55 * emb[:, :, None])
    elif fx == "vr":
        vr_g = np.exp(-((uf / 0.38) ** 2 + ((vf - 0.02) / 0.18) ** 2))
        q0 = np.clip(q0 + np.array(r["glow"]) * 0.18 * vr_g[:, :, None], 0.0, 1.0)
    elif fx == "crt":
        q0[:, :, :] = np.array([0.04, 0.14, 0.08], dtype=np.float32)
        scan = 0.86 + 0.14 * np.sin(yy * 0.55)
        vig = np.clip(1.0 - 0.42 * (uf * uf + vf * vf), 0.35, 1.0)
        q0 *= (scan * vig)[:, :, None]
        gcol = np.array([0.32, 0.98, 0.54], dtype=np.float32)
        eye_l = (np.abs(uf + 0.32) < 0.10) & (np.abs(vf - 0.16) < 0.13)
        eye_r = (np.abs(uf - 0.32) < 0.10) & (np.abs(vf - 0.16) < 0.13)
        smile = (np.abs(uf) < 0.28) & (np.abs(vf + 0.24 - 0.25 * uf * uf) < 0.035)
        border = (np.abs(uf) > 0.88) | (np.abs(vf) > 0.82)
        q0[eye_l | eye_r | smile] = gcol
        q0[border] = gcol * 0.38

    # Crisp expressive eyebrows dead-center over MPFB2 brow ridges (|uf|=0.23, vf=+0.34)
    if fx != "crt":
        slope = -0.24 if fx in ("pirate", "punk", "demon", "stern") else (0.14 if fx in ("caffeine", "manic", "sleepy") else -0.05)
        b_y = 0.345 + slope * (np.abs(uf) - 0.23)
        brow = np.exp(-(((np.abs(uf) - 0.23) / 0.135) ** 2 + ((vf - b_y) / 0.032) ** 2))
        q0 = q0 * (1.0 - 0.80 * brow[:, :, None]) + hair_col * (0.80 * brow[:, :, None])
        # Subtle upper lash line framing the eyes!
        if fx == "zen":
            lash = np.exp(-(((np.abs(uf) - 0.226) / 0.11) ** 2 + ((vf - 0.195 + 0.18 * (np.abs(uf) - 0.226) ** 2) / 0.018) ** 2))
            q0 = q0 * (1.0 - 0.86 * lash[:, :, None]) + np.array([0.12, 0.10, 0.10]) * (0.86 * lash[:, :, None])
        else:
            lash = np.exp(-(((np.abs(uf) - 0.226) / 0.10) ** 2 + ((vf - 0.235) / 0.014) ** 2))
            q0 = q0 * (1.0 - 0.55 * lash[:, :, None]) + (hair_col * 0.65) * (0.55 * lash[:, :, None])

    arr[0:512, 0:512, :3] = np.clip(q0, 0.0, 1.0)

    # ---------------- Q1: Eye Iris (x:512..768, y:0..512) & Hair Strands (x:768..1024, y:0..512) ----------------
    ey, ex = np.mgrid[0:512, 0:256].astype(np.float32)
    eu = (ex - 128.0) / 100.0
    ev = (ey - 256.0) / 100.0
    rad = np.sqrt(eu * eu + ev * ev)
    ang = np.arctan2(ev, eu)
    eye_img = np.ones((512, 256, 3), dtype=np.float32) * np.array([0.91, 0.89, 0.87], dtype=np.float32)
    if fx == "caffeine":
        veins = (rad > 0.50) & (np.abs(np.sin(ang * 9.0 + rad * 12.0)) > 0.94)
        eye_img[veins] = np.array([0.88, 0.46, 0.46])
    elif fx == "demon":
        eye_img[:, :, :] = np.array([0.16, 0.04, 0.04])
    iris_c = np.array(r["iris"], dtype=np.float32)
    iris_rad = 0.50 if fx == "caffeine" else 0.58
    pup_rad = 0.20 if fx == "caffeine" else 0.28
    fibers = 0.84 + 0.16 * np.sin(ang * 26.0) * np.cos(rad * 16.0)
    in_iris = rad < iris_rad
    limbal = (rad >= iris_rad - 0.08) & (rad < iris_rad + 0.03)
    eye_img[in_iris] = np.clip(iris_c * fibers[in_iris, None], 0.0, 1.0)
    eye_img[limbal] = iris_c * 0.26
    eye_img[rad < pup_rad] = np.array([0.02, 0.02, 0.03])
    # Sparkling dual cornea catchlights!
    c1 = ((eu + 0.16) ** 2 + (ev - 0.17) ** 2) < 0.013
    c2 = ((eu - 0.16) ** 2 + (ev + 0.14) ** 2) < 0.004
    eye_img[c1 | c2] = np.array([1.0, 1.0, 1.0])
    arr[0:512, 512:768, :3] = eye_img

    # Hair directional strand texture (x:768..1024, y:0..512)
    hy, hx = np.mgrid[0:512, 0:256].astype(np.float32)
    strand = 0.87 + 0.13 * np.sin(hx * 0.65 + 2.0 * np.sin(hy * 0.032)) + 0.06 * np.cos(hx * 1.4)
    sheen = 0.10 * np.exp(-(((hy - 310.0) / 80.0) ** 2))
    h_img = np.clip(hair_col[None, None, :] * strand[:, :, None] + sheen[:, :, None], 0.0, 1.0)
    arr[0:512, 768:1024, :3] = h_img

    # ---------------- Q2: Refined Upper Outfit Fabrics (x:0..512, y:512..1024) ----------------
    uy, ux = np.mgrid[0:512, 0:512].astype(np.float32)
    u_col = np.array(r["upper"], dtype=np.float32)
    t_col = np.array(r["trim"], dtype=np.float32)
    a_col = np.array(r["accent"], dtype=np.float32)
    q2 = np.empty((512, 512, 3), dtype=np.float32)
    q2[:, :, :] = u_col
    weave = (0.94 + 0.06 * np.sin(ux * 1.1) * np.cos(uy * 1.1) + rng.uniform(-0.02, 0.02, (512, 512))).astype(np.float32)
    q2 *= weave[:, :, None]

    pat = r["pattern"]
    if pat == "pirate_coat":
        hem = (uy < 40) | ((uy > 48) & (uy < 58))
        q2[hem] = t_col * (0.92 + 0.08 * np.sin(ux[hem] * 0.8))[:, None]
        seam = (np.abs(ux - 256) < 4) | (np.abs(ux - 115) < 2) | (np.abs(ux - 397) < 2)
        q2[seam] *= 0.72
    elif pat == "barista_hoodie":
        # Clean soft French-terry heather lavender knit (ZERO garish stripes, ZERO blotchy rings!)
        heather = 0.94 + 0.06 * np.sin(uy * 0.45) + rng.uniform(-0.025, 0.025, (512, 512))
        q2 = np.clip(u_col[None, None, :] * heather[:, :, None], 0.0, 1.0)
        rib = (uy < 26)
        q2[rib] *= 0.85
    elif pat == "windbreaker":
        diag = (uy + 0.55 * np.abs(ux - 256))
        q2[(diag > 295) & (diag < 350)] = a_col * weave[(diag > 295) & (diag < 350), None]
        q2[diag >= 350] = t_col * weave[diag >= 350, None]
    elif pat == "wizard_stars":
        border = (uy < 38) | ((uy > 46) & (uy < 54))
        q2[border] = t_col
        for sx, sy, sr in [(80, 180, 13), (210, 310, 16), (360, 220, 14), (430, 390, 12), (140, 420, 15), (295, 130, 12), (255, 450, 15)]:
            dx, dy = np.abs(ux - sx), np.abs(uy - sy)
            star = ((dx < sr) & (dy < sr * 0.24)) | ((dy < sr) & (dx < sr * 0.24)) | ((dx + dy) < sr * 0.52)
            q2[star] = t_col
    elif pat == "bathrobe":
        # Cozy refined waffle-weave terrycloth
        waffle = 0.90 + 0.10 * np.sin(ux * 0.55) * np.sin(uy * 0.55)
        q2 = np.clip(u_col[None, None, :] * waffle[:, :, None], 0.0, 1.0)
        q2[uy < 24] = t_col
    elif pat == "punk_leather":
        # Clean distressed pebble-grain black leather + subtle shoulder studs (ZERO flat painted red chest box!)
        pebble = 0.88 + 0.12 * np.sin(ux * 0.9 + rng.uniform(-0.25, 0.25, (512, 512))) * np.cos(uy * 0.9)
        q2 = np.clip(u_col[None, None, :] * pebble[:, :, None], 0.0, 1.0)
        for sx in range(44, 490, 56):
            stud = ((ux - sx) ** 2 + (uy - 445) ** 2) < 30
            q2[stud] = np.array([0.82, 0.84, 0.88])
    elif pat == "monk_robe":
        diag = (uy + 0.45 * ux) % 240
        q2[diag < 78] = t_col * weave[diag < 78, None]
        q2[(diag >= 74) & (diag < 82)] = a_col
    elif pat == "techwear":
        grid = ((ux.astype(int) % 28 == 0) | (uy.astype(int) % 28 == 0))
        q2[grid] *= 0.86
        pipe = (np.abs(ux - 145) < 3) | (np.abs(ux - 367) < 3)
        q2[pipe] = a_col * 0.85
    elif pat == "puffer":
        baffle = np.abs(np.sin(uy * math.pi / 86.0))
        q2 = np.clip(u_col[None, None, :] * (0.78 + 0.22 * (baffle ** 0.45))[:, :, None], 0.0, 1.0)
        q2[np.abs(ux - 256) < 4] = t_col
    elif pat == "devil_suit":
        pinstripe = (ux.astype(int) % 26) < 2
        q2[pinstripe] = q2[pinstripe] * 0.65 + a_col * 0.35
    elif pat == "argyle_98":
        ax = ((ux % 96) - 48.0) / 48.0
        ay = ((uy % 128) - 64.0) / 64.0
        diamond = (np.abs(ax) + np.abs(ay)) < 0.72
        line = np.abs((np.abs(ax) + np.abs(ay)) - 0.72) < 0.055
        q2[diamond] = (u_col * 0.65 + t_col * 0.35) * weave[diamond, None]
        q2[line] = a_col
    elif pat == "kevlar":
        molle = ((uy.astype(int) % 64) < 16) & (uy < 360)
        q2[molle] = t_col * weave[molle, None]

    arr[512:1024, 0:512, :3] = np.clip(q2, 0.0, 1.0)

    # ---------------- Q3: Lower Garment + Wood/Metal Materials (x:512..1024, y:512..1024) ----------------
    ly, lx = np.mgrid[0:512, 0:256].astype(np.float32)
    l_col = np.array(r["lower"], dtype=np.float32)
    twill = (0.92 + 0.08 * np.sin((lx + ly) * 0.95) + rng.uniform(-0.02, 0.02, (512, 256))).astype(np.float32)
    pants = np.clip(l_col[None, None, :] * twill[:, :, None], 0.0, 1.0)
    if pat == "bathrobe":
        # Soft muted blue-and-ivory vertical striped pajamas
        p_stripe = ((lx.astype(int) // 24) % 2) == 0
        pants[p_stripe] = np.array([0.46, 0.60, 0.76]) * twill[p_stripe, None]
    arr[512:1024, 512:768, :3] = pants

    # Brushed scratched steel (x:768..1024, y:512..768)
    my, mx = np.mgrid[0:256, 0:256].astype(np.float32)
    metal = 0.80 + 0.10 * np.sin(my * 1.4 + 0.5 * np.sin(mx * 0.08)) + rng.uniform(-0.03, 0.03, (256, 256))
    arr[512:768, 768:1024, :3] = np.clip(metal[:, :, None] * np.array([0.82, 0.86, 0.92]), 0.0, 1.0)

    # Carved warm oak woodgrain (x:768..1024, y:768..1024)
    wy, wx = np.mgrid[0:256, 0:256].astype(np.float32)
    grain = 0.80 + 0.20 * np.sin(wx * 0.28 + 3.5 * np.sin(wy * 0.04))
    arr[768:1024, 768:1024, :3] = np.clip(grain[:, :, None] * np.array([0.48, 0.29, 0.16]), 0.0, 1.0)

    # Linearize for Raylib Finish.Anatomy & Archive dossierShader gamma lift
    arr[:, :, :3] = np.power(np.clip(arr[:, :, :3], 0.0, 1.0), 2.15)
    img = bpy.data.images.new(f"VCAtlas_{idx}", width=W, height=H, alpha=False)
    img.colorspace_settings.name = 'sRGB'
    img.pixels.foreach_set(arr.ravel())
    img.pack()
    return img


# =============================================================================
# MATERIAL BUILDER
# =============================================================================
def build_materials(atlas_img, r):
    mats = {}
    def make_tex_mat(key, rough=0.58, met=0.0, em=None, em_str=0.0):
        m = bpy.data.materials.new(key)
        m.use_nodes = True
        m.use_backface_culling = True
        nt = m.node_tree
        nt.nodes.clear()
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = atlas_img
        vc = nt.nodes.new("ShaderNodeVertexColor")
        vc.layer_name = "COLOR_0"
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        nt.links.new(tex.outputs["Color"], mix.inputs["A"])
        nt.links.new(vc.outputs["Color"], mix.inputs["B"])
        nt.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = rough
        bsdf.inputs["Metallic"].default_value = met
        if em is not None:
            bsdf.inputs["Emission Color"].default_value = (*srgb_to_lin(em), 1.0)
            bsdf.inputs["Emission Strength"].default_value = em_str
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
        mats[key] = m

    def make_solid_mat(key, srgb, rough=0.50, met=0.0, em=None, em_str=0.0):
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
        if em is not None:
            bsdf.inputs["Emission Color"].default_value = (*srgb_to_lin(em), 1.0)
            bsdf.inputs["Emission Strength"].default_value = em_str
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
        mats[key] = m

    make_tex_mat("face",   rough=0.48, met=0.0, em=r["glow"] if r["face_fx"] == "crt" else None, em_str=0.88 if r["face_fx"] == "crt" else 0.0)
    make_tex_mat("eye",    rough=0.08, met=0.0, em=r["glow"] if r["face_fx"] == "demon" else None, em_str=0.85 if r["face_fx"] == "demon" else 0.0)
    make_tex_mat("hair",   rough=0.48, met=0.02)
    make_tex_mat("upper",  rough=0.72 if r["pattern"] != "punk_leather" else 0.36, met=0.04)
    make_tex_mat("lower",  rough=0.78, met=0.0)
    make_tex_mat("steel",  rough=0.22, met=0.90)
    make_tex_mat("wood",   rough=0.68, met=0.0)
    make_solid_mat("skin",   r["skin"],   rough=0.50, met=0.0)
    make_solid_mat("trim",   r["trim"],   rough=0.46, met=0.10)
    make_solid_mat("accent", r["accent"], rough=0.42, met=0.08)
    make_solid_mat("shoe",   r["shoe"],   rough=0.58, met=0.02)
    make_solid_mat("gold",   (0.92, 0.74, 0.26), rough=0.24, met=0.86, em=(0.30, 0.20, 0.04), em_str=0.10)
    make_solid_mat("coal",   (0.12, 0.13, 0.15), rough=0.44, met=0.12)
    make_solid_mat("ivory",  (0.93, 0.91, 0.87), rough=0.50, met=0.0)
    make_solid_mat("glow",   r["glow"],   rough=0.14, met=0.18, em=r["glow"], em_str=0.92)
    return mats


# =============================================================================
# BMESH SCULPTING HELPERS (Including Curved Body-Hugging Shells!)
# =============================================================================
def _assign_uv_box(faces, uv_layer, u0, u1, v0, v1, scale=2.2):
    for f in faces:
        n = f.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for loop in f.loops:
            co = loop.vert.co
            if ay >= ax and ay >= az:
                raw_u, raw_v = co.x * scale + 0.5, co.z * scale + 0.5
            elif ax >= az:
                raw_u, raw_v = co.y * scale + 0.5, co.z * scale + 0.5
            else:
                raw_u, raw_v = co.x * scale + 0.5, co.y * scale + 0.5
            fu = max(0.02, min(0.98, raw_u % 1.0))
            fv = max(0.02, min(0.98, raw_v % 1.0))
            loop[uv_layer].uv = (u0 + fu * (u1 - u0), v0 + fv * (v1 - v0))


def _uv_rect_for_mat(mat_key):
    if mat_key == "upper": return (0.02, 0.48, 0.52, 0.98)
    if mat_key == "lower": return (0.52, 0.73, 0.52, 0.98)
    if mat_key == "hair":  return (0.77, 0.98, 0.02, 0.48)
    if mat_key == "steel": return (0.77, 0.98, 0.52, 0.73)
    if mat_key == "wood":  return (0.77, 0.98, 0.77, 0.98)
    if mat_key == "eye":   return (0.52, 0.73, 0.02, 0.48)
    return (0.02, 0.48, 0.02, 0.48)


def _merge_bm(src, dst, src_uv, dst_uv):
    vmap = {}
    for v in src.verts:
        vmap[v] = dst.verts.new(v.co)
    for f in src.faces:
        try:
            nf = dst.faces.new([vmap[v] for v in f.verts])
            nf.material_index = f.material_index
            nf.smooth = f.smooth
            for sl, dl in zip(f.loops, nf.loops):
                dl[dst_uv].uv = sl[src_uv].uv
        except ValueError:
            pass
    src.free()


def _mk_box(bm, uv_layer, center, size, mat_idx, mat_key, bevel=0.0, segs=2, rot=None, smooth=True):
    tmp = bmesh.new()
    t_uv = tmp.loops.layers.uv.new("UVMap")
    r2 = bmesh.ops.create_cube(tmp, size=1.0)
    bmesh.ops.scale(tmp, vec=Vector(size), verts=r2["verts"])
    if bevel > 0:
        bmesh.ops.bevel(tmp, geom=list(tmp.edges), offset=bevel, segments=segs, affect='EDGES', clamp_overlap=True)
    m = Matrix.Translation(center) @ (rot.to_matrix().to_4x4() if rot is not None else Matrix.Identity(4))
    bmesh.ops.transform(tmp, matrix=m, verts=tmp.verts)
    for f in tmp.faces:
        f.material_index = mat_idx
        f.smooth = smooth
    u0, u1, v0, v1 = _uv_rect_for_mat(mat_key)
    _assign_uv_box(tmp.faces, t_uv, u0, u1, v0, v1)
    _merge_bm(tmp, bm, t_uv, uv_layer)


def _mk_sphere(bm, uv_layer, center, radii, mat_idx, mat_key, u_segs=16, v_segs=10, rot=None, smooth=True):
    tmp = bmesh.new()
    t_uv = tmp.loops.layers.uv.new("UVMap")
    bmesh.ops.create_uvsphere(tmp, u_segments=u_segs, v_segments=v_segs, radius=1.0)
    bmesh.ops.scale(tmp, vec=Vector(radii), verts=tmp.verts)
    m = Matrix.Translation(center) @ (rot.to_matrix().to_4x4() if rot is not None else Matrix.Identity(4))
    bmesh.ops.transform(tmp, matrix=m, verts=tmp.verts)
    for f in tmp.faces:
        f.material_index = mat_idx
        f.smooth = smooth
    u0, u1, v0, v1 = _uv_rect_for_mat(mat_key)
    _assign_uv_box(tmp.faces, t_uv, u0, u1, v0, v1)
    _merge_bm(tmp, bm, t_uv, uv_layer)


def _mk_eyeball(bm, uv_layer, center, radius, mat_idx):
    """Build a crisp forward-facing (-Y) 3D eyeball mapped cleanly onto Q1 radial iris + catchlights."""
    tmp = bmesh.new()
    t_uv = tmp.loops.layers.uv.new("UVMap")
    bmesh.ops.create_uvsphere(tmp, u_segments=18, v_segments=12, radius=radius)
    bmesh.ops.transform(tmp, matrix=Matrix.Translation(center), verts=tmp.verts)
    for f in tmp.faces:
        f.material_index = mat_idx
        f.smooth = True
        for l in f.loops:
            co = l.vert.co - Vector(center)
            # Only front hemisphere (-Y) gets the iris; back hemisphere maps to white sclera edge
            if co.y < 0.002:
                du = co.x / radius
                dv = co.z / radius
                u = 0.625 + max(-0.115, min(0.115, du * 0.115))
                v = 0.250 + max(-0.220, min(0.220, dv * 0.220))
            else:
                u, v = 0.525, 0.04
            l[t_uv].uv = (u, v)
    _merge_bm(tmp, bm, t_uv, uv_layer)


def _mk_cyl(bm, uv_layer, center, r1, r2, depth, mat_idx, mat_key, segs=18, rot=None, smooth=True):
    tmp = bmesh.new()
    t_uv = tmp.loops.layers.uv.new("UVMap")
    bmesh.ops.create_cone(tmp, cap_ends=True, cap_tris=False, segments=segs, radius1=r1, radius2=r2, depth=depth)
    m = Matrix.Translation(center) @ (rot.to_matrix().to_4x4() if rot is not None else Matrix.Identity(4))
    bmesh.ops.transform(tmp, matrix=m, verts=tmp.verts)
    for f in tmp.faces:
        f.material_index = mat_idx
        f.smooth = smooth
    u0, u1, v0, v1 = _uv_rect_for_mat(mat_key)
    _assign_uv_box(tmp.faces, t_uv, u0, u1, v0, v1)
    _merge_bm(tmp, bm, t_uv, uv_layer)


def _mk_loft(bm, uv_layer, profile, mat_idx, mat_key, segs=22, cap_bottom=True, cap_top=True, smooth=True, folds=0.0):
    tmp = bmesh.new()
    t_uv = tmp.loops.layers.uv.new("UVMap")
    rings = []
    for r_i, item in enumerate(profile):
        pos, rx, ry = item[0], item[1], item[2]
        tilt = math.radians(item[3]) if len(item) > 3 else 0.0
        cx, cy, cz = pos
        ring = []
        for i in range(segs):
            a = 2.0 * math.pi * i / segs
            f_mod = 1.0 + folds * math.sin(a * 5.0 + r_i * 1.1)
            lx = rx * f_mod * math.cos(a)
            ly = ry * f_mod * math.sin(a)
            lz = ly * math.sin(tilt)
            ly2 = ly * math.cos(tilt)
            ring.append(tmp.verts.new((cx + lx, cy + ly2, cz + lz)))
        rings.append(ring)
    u0, u1, v0, v1 = _uv_rect_for_mat(mat_key)
    for k in range(len(rings) - 1):
        r0, r1 = rings[k], rings[k + 1]
        va = v0 + (k / max(1, len(rings) - 1)) * (v1 - v0)
        vb = v0 + ((k + 1) / max(1, len(rings) - 1)) * (v1 - v0)
        for i in range(segs):
            inext = (i + 1) % segs
            f = tmp.faces.new((r0[i], r0[inext], r1[inext], r1[i]))
            f.material_index = mat_idx
            f.smooth = smooth
            ua = u0 + (i / segs) * (u1 - u0)
            ub = u0 + ((i + 1) / segs) * (u1 - u0)
            f.loops[0][t_uv].uv = (ua, va)
            f.loops[1][t_uv].uv = (ub, va)
            f.loops[2][t_uv].uv = (ub, vb)
            f.loops[3][t_uv].uv = (ua, vb)
    if cap_bottom and len(rings) >= 1:
        f = tmp.faces.new(list(reversed(rings[0])))
        f.material_index = mat_idx
        for l in f.loops: l[t_uv].uv = ((u0 + u1) * 0.5, v0)
    if cap_top and len(rings) >= 1:
        f = tmp.faces.new(rings[-1])
        f.material_index = mat_idx
        for l in f.loops: l[t_uv].uv = ((u0 + u1) * 0.5, v1)
    _merge_bm(tmp, bm, t_uv, uv_layer)


def _mk_curved_shell(bm, uv_layer, rings_spec, a_start_deg, a_end_deg, thickness, mat_idx, mat_key, segs=12, smooth=True):
    """Build a closed curved body-hugging shell (aprons, sleep masks, vest plates, visors, lapels!).
    rings_spec: list of ((cx, cy, cz), rx, ry) where angle -90 deg points straight front (-Y).
    """
    tmp = bmesh.new()
    t_uv = tmp.loops.layers.uv.new("UVMap")
    a0 = math.radians(a_start_deg)
    a1 = math.radians(a_end_deg)
    outer_rings, inner_rings = [], []
    for (cx, cy, cz), rx, ry in rings_spec:
        o_r, i_r = [], []
        for s in range(segs + 1):
            a = a0 + (a1 - a0) * (s / segs)
            ca, sa = math.cos(a), math.sin(a)
            o_r.append(tmp.verts.new((cx + (rx + thickness) * ca, cy + (ry + thickness) * sa, cz)))
            i_r.append(tmp.verts.new((cx + rx * ca, cy + ry * sa, cz)))
        outer_rings.append(o_r)
        inner_rings.append(i_r)
    u0, u1, v0, v1 = _uv_rect_for_mat(mat_key)
    nr = len(rings_spec)

    def add_quad(vs, ua, ub, va, vb):
        try:
            f = tmp.faces.new(vs)
            f.material_index = mat_idx
            f.smooth = smooth
            f.loops[0][t_uv].uv = (ua, va)
            f.loops[1][t_uv].uv = (ub, va)
            f.loops[2][t_uv].uv = (ub, vb)
            f.loops[3][t_uv].uv = (ua, vb)
        except ValueError:
            pass

    for k in range(nr - 1):
        va = v0 + (k / max(1, nr - 1)) * (v1 - v0)
        vb = v0 + ((k + 1) / max(1, nr - 1)) * (v1 - v0)
        for s in range(segs):
            ua = u0 + (s / segs) * (u1 - u0)
            ub = u0 + ((s + 1) / segs) * (u1 - u0)
            # Outer face
            add_quad((outer_rings[k][s], outer_rings[k][s + 1], outer_rings[k + 1][s + 1], outer_rings[k + 1][s]), ua, ub, va, vb)
            # Inner face (reversed winding)
            add_quad((inner_rings[k][s + 1], inner_rings[k][s], inner_rings[k + 1][s], inner_rings[k + 1][s + 1]), ub, ua, va, vb)
        # Left & right vertical thickness caps
        add_quad((inner_rings[k][0], outer_rings[k][0], outer_rings[k + 1][0], inner_rings[k + 1][0]), u0, u0, va, vb)
        add_quad((outer_rings[k][segs], inner_rings[k][segs], inner_rings[k + 1][segs], outer_rings[k + 1][segs]), u1, u1, va, vb)
    # Bottom & top edge caps
    for s in range(segs):
        ua = u0 + (s / segs) * (u1 - u0)
        ub = u0 + ((s + 1) / segs) * (u1 - u0)
        add_quad((inner_rings[0][s], inner_rings[0][s + 1], outer_rings[0][s + 1], outer_rings[0][s]), ua, ub, v0, v0)
        add_quad((outer_rings[-1][s], outer_rings[-1][s + 1], inner_rings[-1][s + 1], inner_rings[-1][s]), ua, ub, v1, v1)
    bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces)
    _merge_bm(tmp, bm, t_uv, uv_layer)


def _mk_tube(bm, uv_layer, points, radii, mat_idx, mat_key, segs=10, cap=True, smooth=True):
    if isinstance(radii, (int, float)):
        radii = [float(radii)] * len(points)
    tmp = bmesh.new()
    t_uv = tmp.loops.layers.uv.new("UVMap")
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
        rings.append([tmp.verts.new(p + (u * math.cos(2 * math.pi * s / segs) + v * math.sin(2 * math.pi * s / segs)) * r) for s in range(segs)])
    u0, u1, v0, v1 = _uv_rect_for_mat(mat_key)
    for k in range(len(rings) - 1):
        r0, r1 = rings[k], rings[k + 1]
        va = v0 + (k / max(1, len(rings) - 1)) * (v1 - v0)
        vb = v0 + ((k + 1) / max(1, len(rings) - 1)) * (v1 - v0)
        for s in range(segs):
            sn = (s + 1) % segs
            f = tmp.faces.new((r0[s], r0[sn], r1[sn], r1[s]))
            f.material_index = mat_idx
            f.smooth = smooth
            ua = u0 + (s / segs) * (u1 - u0)
            ub = u0 + ((s + 1) / segs) * (u1 - u0)
            f.loops[0][t_uv].uv = (ua, va)
            f.loops[1][t_uv].uv = (ub, va)
            f.loops[2][t_uv].uv = (ub, vb)
            f.loops[3][t_uv].uv = (ua, vb)
    if cap:
        f0 = tmp.faces.new(list(reversed(rings[0])))
        f1 = tmp.faces.new(rings[-1])
        f0.material_index = f1.material_index = mat_idx
        for l in list(f0.loops) + list(f1.loops):
            l[t_uv].uv = ((u0 + u1) * 0.5, (v0 + v1) * 0.5)
    _merge_bm(tmp, bm, t_uv, uv_layer)


def _mk_blade_fin(bm, uv_layer, base_a, base_b, tip, half_w, mat_idx, mat_key):
    """Build a crisp tapered flat stylized fin/blade (for swept punk mohawk & curved feather plumes!)."""
    tmp = bmesh.new()
    t_uv = tmp.loops.layers.uv.new("UVMap")
    ba, bb, tp = Vector(base_a), Vector(base_b), Vector(tip)
    mid = (ba + bb) * 0.5 + (tp - (ba + bb) * 0.5) * 0.55
    side_v = (bb - ba).cross(tp - ba)
    if side_v.length < 1e-6:
        side_v = Vector((1, 0, 0))
    side_v.normalize()
    v_al = tmp.verts.new(ba - side_v * half_w)
    v_ar = tmp.verts.new(ba + side_v * half_w)
    v_bl = tmp.verts.new(bb - side_v * half_w)
    v_br = tmp.verts.new(bb + side_v * half_w)
    v_ml = tmp.verts.new(mid - side_v * (half_w * 0.65))
    v_mr = tmp.verts.new(mid + side_v * (half_w * 0.65))
    v_tp = tmp.verts.new(tp)
    for verts in [
        (v_al, v_bl, v_ml), (v_bl, v_tp, v_ml), (v_al, v_ml, v_tp),
        (v_br, v_ar, v_mr), (v_ar, v_tp, v_mr), (v_br, v_mr, v_tp),
        (v_al, v_ar, v_br, v_bl), (v_al, v_tp, v_ar), (v_bl, v_br, v_tp),
    ]:
        try:
            f = tmp.faces.new(verts)
            f.material_index = mat_idx
            f.smooth = True
        except ValueError:
            pass
    bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces)
    u0, u1, v0, v1 = _uv_rect_for_mat(mat_key)
    _assign_uv_box(tmp.faces, t_uv, u0, u1, v0, v1)
    _merge_bm(tmp, bm, t_uv, uv_layer)


# =============================================================================
# MPFB2 POSED ANATOMICAL HUMAN EXTRACTOR (Records Exact Eye Socket & Scalp Landmarks!)
# =============================================================================
def create_posed_mpfb_human(r):
    h = HumanService.create_human()
    gender, age, muscle, weight, prop = r["macro"]
    try:
        TargetService.set_macro_info_dict(h, {
            "gender": gender, "age": age, "muscle": muscle,
            "weight": weight, "proportions": prop, "height": 0.52,
            "cupsize": 0.1, "firmness": 0.5,
            "race": {"asian": 0.2, "caucasian": 0.7, "african": 0.1},
        })
        TargetService.reapply_macro_details(h)
    except Exception:
        pass

    rig = HumanService.add_builtin_rig(h, "game_engine", import_weights=True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='POSE')

    def point_bone_down(bname, target_dir):
        pb = rig.pose.bones[bname]
        b = rig.data.bones[bname]
        cur_dir = (b.tail_local - b.head_local).normalized()
        q_world = cur_dir.rotation_difference(target_dir.normalized())
        bone_mat = b.matrix_local.to_3x3()
        q_local = (bone_mat.inverted() @ q_world.to_matrix() @ bone_mat).to_quaternion()
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = q_local

    point_bone_down("upperarm_l", Vector(( 0.04, 0.0, -1.0)))
    point_bone_down("upperarm_r", Vector((-0.04, 0.0, -1.0)))
    point_bone_down("thigh_l",    Vector(( 0.01, 0.0, -1.0)))
    point_bone_down("thigh_r",    Vector((-0.01, 0.0, -1.0)))
    bpy.context.view_layer.update()

    for bname in ("lowerarm_l", "lowerarm_r", "calf_l", "calf_r"):
        pb = rig.pose.bones[bname]
        cur_dir = (pb.tail - pb.head).normalized()
        q_delta = cur_dir.rotation_difference(Vector((0.0, 0.0, -1.0)))
        mat_p = pb.matrix.to_3x3()
        q_loc = (mat_p.inverted() @ q_delta.to_matrix() @ mat_p).to_quaternion()
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = pb.rotation_quaternion @ q_loc
        bpy.context.view_layer.update()

    bpy.ops.object.mode_set(mode='OBJECT')

    # Bake posed mesh into clean mesh object normalized to 1.74m with feet at Z=0
    deps = bpy.context.evaluated_depsgraph_get()
    eval_obj = h.evaluated_get(deps)
    mesh = bpy.data.meshes.new_from_object(eval_obj)
    obj = bpy.data.objects.new("BakedHuman", mesh)
    bpy.context.collection.objects.link(obj)
    for vg in h.vertex_groups:
        obj.vertex_groups.new(name=vg.name)
    for v in h.data.vertices:
        for g in v.groups:
            obj.vertex_groups[g.group].add([v.index], g.weight, 'REPLACE')

    bi = obj.vertex_groups["body"].index
    body_verts = [v for v in mesh.vertices if any(g.group == bi and g.weight > 0.5 for g in v.groups)]
    min_z = min(v.co.z for v in body_verts)
    max_z = max(v.co.z for v in body_verts)
    scale = 1.74 / max(0.5, max_z - min_z)
    for v in mesh.vertices:
        v.co = Vector((v.co.x * scale, v.co.y * scale, (v.co.z - min_z) * scale))

    def bone_pos(bname, use_tail=False):
        pb = rig.pose.bones[bname]
        co = pb.tail if use_tail else pb.head
        return Vector((co.x * scale, co.y * scale, (co.z - min_z) * scale))

    def vg_center(gname, fallback):
        if gname in obj.vertex_groups:
            gi = obj.vertex_groups[gname].index
            pts = [v.co for v in mesh.vertices if any(g.group == gi and g.weight > 0.1 for g in v.groups)]
            if pts:
                return sum(pts, Vector((0, 0, 0))) / len(pts)
        return Vector(fallback)

    neck_p = bone_pos("neck_01")
    joints = {
        "pelvis":   bone_pos("pelvis"),
        "neck":     neck_p,
        "head":     bone_pos("head"),
        "eye_l":    vg_center("joint-l-eye", ( 0.032, -0.130, neck_p.z + 0.145)),
        "eye_r":    vg_center("joint-r-eye", (-0.032, -0.130, neck_p.z + 0.145)),
        "shld_l":   bone_pos("upperarm_l"),
        "elbow_l":  bone_pos("lowerarm_l"),
        "hand_l":   bone_pos("hand_l"),
        "shld_r":   bone_pos("upperarm_r"),
        "elbow_r":  bone_pos("lowerarm_r"),
        "hand_r":   bone_pos("hand_r"),
        "hip_l":    bone_pos("thigh_l"),
        "knee_l":   bone_pos("calf_l"),
        "ankle_l":  bone_pos("foot_l"),
        "hip_r":    bone_pos("thigh_r"),
        "knee_r":   bone_pos("calf_r"),
        "ankle_r":  bone_pos("foot_r"),
    }

    bpy.data.objects.remove(h, do_unlink=True)
    bpy.data.objects.remove(rig, do_unlink=True)
    return obj, joints


def extract_human_subset(human_obj, group_name, keep_fn, decimate_ratio=0.5, mat_idx=0, uv_mode="face", offset_vec=Vector((0, 0, 0)), scale_vec=Vector((1, 1, 1)), inflate=0.0, bone_groups=None, weight_thresh=0.14):
    """Extract a clean polygon subset from the baked MPFB2 human using seamless bone-weight overlap!"""
    mesh = human_obj.data
    gi = human_obj.vertex_groups[group_name].index
    gset = {v.index for v in mesh.vertices if any(g.group == gi and g.weight > 0.25 for g in v.groups)}

    if bone_groups is not None:
        b_indices = {human_obj.vertex_groups[bn].index for bn in bone_groups if bn in human_obj.vertex_groups}
        gset = {v.index for v in mesh.vertices if v.index in gset and sum(g.weight for g in v.groups if g.group in b_indices) >= weight_thresh}

    tmp_bm = bmesh.new()
    tmp_bm.from_mesh(mesh)
    tmp_bm.verts.ensure_lookup_table()
    tmp_bm.faces.ensure_lookup_table()

    del_faces = []
    for f in tmp_bm.faces:
        if not all(v.index in gset for v in f.verts):
            del_faces.append(f)
            continue
        c = f.calc_center_median()
        if not keep_fn(c):
            del_faces.append(f)
    if del_faces:
        bmesh.ops.delete(tmp_bm, geom=del_faces, context='FACES')
    loose = [v for v in tmp_bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(tmp_bm, geom=loose, context='VERTS')

    if len(tmp_bm.faces) == 0:
        tmp_bm.free()
        return None

    if inflate != 0.0:
        tmp_bm.normal_update()
        for v in tmp_bm.verts:
            fold = 1.0 + 0.14 * math.sin(v.co.z * 36.0 + v.co.x * 20.0)
            v.co += v.normal * (inflate * fold)

    for v in tmp_bm.verts:
        co = v.co - offset_vec
        v.co = Vector((co.x * scale_vec.x, co.y * scale_vec.y, co.z * scale_vec.z))

    sub_mesh = bpy.data.meshes.new("SubPart")
    tmp_bm.to_mesh(sub_mesh)
    tmp_bm.free()
    sub_obj = bpy.data.objects.new("SubPartObj", sub_mesh)
    bpy.context.collection.objects.link(sub_obj)
    if decimate_ratio < 0.99 and len(sub_mesh.polygons) > 120:
        mod = sub_obj.modifiers.new("Dec", 'DECIMATE')
        mod.ratio = decimate_ratio
        bpy.context.view_layer.objects.active = sub_obj
        bpy.ops.object.modifier_apply(modifier="Dec")

    out_bm = bmesh.new()
    out_bm.from_mesh(sub_obj.data)
    uv_layer = out_bm.loops.layers.uv.verify()
    bmesh.ops.recalc_face_normals(out_bm, faces=out_bm.faces)

    for f in out_bm.faces:
        f.material_index = mat_idx
        f.smooth = True

    if uv_mode == "face":
        for f in out_bm.faces:
            for l in f.loops:
                co = l.vert.co
                u = 0.25 + (co.x / 0.22) * 0.25
                v = 0.25 + ((co.z - 0.125) / 0.22) * 0.25
                l[uv_layer].uv = (max(0.01, min(0.49, u)), max(0.01, min(0.49, v)))
    elif uv_mode in ("upper", "lower", "hair", "steel", "wood"):
        u0, u1, v0, v1 = _uv_rect_for_mat(uv_mode)
        _assign_uv_box(out_bm.faces, uv_layer, u0, u1, v0, v1, scale=2.4)

    bpy.data.objects.remove(sub_obj, do_unlink=True)
    return out_bm, uv_layer


def build_scalp_hair(human_obj, neck, head_scale, mat_idx, inflate_base=0.016, crown_lift=0.012, fringe_drop=0.008, side_full=0.006):
    """Extract MPFB2's exact 376-vert `scalp` group and sculpt a continuous, smooth, zero-meatball stylized hair cap!"""
    mesh = human_obj.data
    si = human_obj.vertex_groups["scalp"].index
    gset = {v.index for v in mesh.vertices if any(g.group == si and g.weight > 0.25 for g in v.groups)}

    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.verts.ensure_lookup_table()
    del_f = [f for f in bm.faces if not all(v.index in gset for v in f.verts)]
    if del_f:
        bmesh.ops.delete(bm, geom=del_f, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    if len(bm.faces) == 0:
        bm.free()
        return None

    # Transform into local scaled head space first so shaping constants are exact
    for v in bm.verts:
        co = v.co - neck
        v.co = Vector((co.x * head_scale.x, co.y * head_scale.y, co.z * head_scale.z))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()

    # Smooth boundary detection (feathers hairline gently into skull while lifting crown & swept fringe!)
    boundary = {v for e in bm.edges if e.is_boundary for v in e.verts}
    near_bound = {e.other_vert(v) for v in boundary for e in v.link_edges} - boundary

    # Solidify cleanly by duplicating outer shell + inner rim so hair has real sculpted thickness!
    orig_verts = list(bm.verts)
    for v in orig_verts:
        n = v.normal.normalized() if v.normal.length > 1e-6 else Vector((0, 0, 1))
        if v in boundary:
            # Front hairline gently dips slightly to form a sleek natural fringe
            front_t = max(0.0, min(1.0, (-v.co.y - 0.08) / 0.06))
            v.co += n * (inflate_base * 0.28)
            v.co.z -= fringe_drop * front_t * (0.7 + 0.3 * math.cos(v.co.x * 28.0))
        else:
            t_top = max(0.0, min(1.0, (v.co.z - 0.16) / 0.12))
            t_side = max(0.0, min(1.0, (abs(v.co.x) - 0.045) / 0.04))
            edge_soft = 0.72 if v in near_bound else 1.0
            wave = 1.0 + 0.08 * math.sin(v.co.x * 42.0 + v.co.y * 24.0)
            off = (inflate_base + crown_lift * t_top + side_full * t_side) * edge_soft * wave
            v.co += n * off
            v.co.z += crown_lift * 0.5 * t_top

    # One gentle Laplacian smoothing pass keeps the hair ultra-clean and stylized
    bmesh.ops.smooth_vert(bm, verts=[v for v in bm.verts if v not in boundary], factor=0.35, use_axis_x=True, use_axis_y=True, use_axis_z=True)

    uv_layer = bm.loops.layers.uv.verify()
    u0, u1, v0, v1 = _uv_rect_for_mat("hair")
    for f in bm.faces:
        f.material_index = mat_idx
        f.smooth = True
    _assign_uv_box(bm.faces, uv_layer, u0, u1, v0, v1, scale=3.2)
    return bm, uv_layer


# =============================================================================
# 12 UNIQUE HEAD + HAIR + HEADGEAR BUILDERS (Built on REAL MPFB2 Human Heads!)
# =============================================================================
def build_head_part(idx, r, human_obj, joints, M):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    neck = joints["neck"]

    if idx == 10:
        # 10: LEGACY GHOST 1998 — Crisp Retro Beige 1998 CRT Monitor Helmet!
        _mk_cyl(bm, uv, (0, 0.0, 0.042), 0.068, 0.064, 0.052, M["coal"], "coal", 18)
        for s in (-1, 1):
            _mk_tube(bm, uv, [(s * 0.052, 0.072, 0.095), (s * 0.084, 0.094, 0.045), (s * 0.046, 0.066, -0.015)], 0.009, M["coal"], "coal", 8)
        _mk_box(bm, uv, (0, 0.010, 0.175), (0.268, 0.238, 0.230), M["ivory"], "ivory", bevel=0.020, segs=3)
        _mk_box(bm, uv, (0, 0.150, 0.175), (0.190, 0.100, 0.160), M["ivory"], "ivory", bevel=0.016, segs=2)
        for k in range(5):
            _mk_box(bm, uv, (0, 0.045 + k * 0.032, 0.291), (0.180, 0.009, 0.007), M["coal"], "coal", bevel=0.002, segs=1)
        _mk_box(bm, uv, (0, -0.108, 0.180), (0.220, 0.016, 0.174), M["coal"], "coal", bevel=0.006, segs=1)
        # Closed 4mm glass CRT phosphor screen block so recalc_face_normals ALWAYS points the front face outward (-Y)!
        tmp = bmesh.new()
        t_uv = tmp.loops.layers.uv.new("UVMap")
        r_scr = bmesh.ops.create_cube(tmp, size=1.0)
        bmesh.ops.scale(tmp, vec=Vector((0.194, 0.006, 0.154)), verts=r_scr["verts"])
        bmesh.ops.transform(tmp, matrix=Matrix.Translation((0, -0.119, 0.180)), verts=tmp.verts)
        for f in tmp.faces:
            f.material_index = M["face"]
            f.smooth = False
            for l in f.loops:
                co = l.vert.co
                u = 0.04 + ((co.x + 0.097) / 0.194) * 0.42
                v = 0.04 + ((co.z - 0.103) / 0.154) * 0.42
                l[t_uv].uv = (max(0.02, min(0.48, u)), max(0.02, min(0.48, v)))
        _merge_bm(tmp, bm, t_uv, uv)
        _mk_box(bm, uv, (0.070, -0.114, 0.078), (0.022, 0.010, 0.014), M["coal"], "coal", bevel=0.002, segs=1)
        _mk_sphere(bm, uv, (0.095, -0.116, 0.078), (0.007, 0.007, 0.007), M["glow"], "glow", 10, 8)
        _mk_tube(bm, uv, [(0.072, 0.04, 0.290), (0.110, 0.05, 0.405)], 0.0055, M["steel"], "steel", 8)
        _mk_sphere(bm, uv, (0.110, 0.05, 0.414), (0.014, 0.014, 0.014), M["glow"], "glow", 10, 8)
        return bm

    # Real MPFB2 anatomical head (scaled 1.08x around neck pivot)
    head_scale = Vector((1.08, 1.08, 1.08))
    head_res = extract_human_subset(
        human_obj, "body", lambda c: c.z >= neck.z - 0.028,
        decimate_ratio=0.58, mat_idx=M["face"], uv_mode="face",
        offset_vec=neck, scale_vec=head_scale, bone_groups=["neck_01", "head"], weight_thresh=0.12
    )
    if head_res:
        _merge_bm(head_res[0], bm, head_res[1], uv)

    # Real 3D Luminous Eyeballs nestled naturally INSIDE exact MPFB2 eye sockets! (No bug-eye protrusion!)
    eye_l_loc = Vector(((joints["eye_l"].x - neck.x) * head_scale.x, (joints["eye_l"].y - neck.y) * head_scale.y + 0.0025, (joints["eye_l"].z - neck.z) * head_scale.z))
    eye_r_loc = Vector(((joints["eye_r"].x - neck.x) * head_scale.x, (joints["eye_r"].y - neck.y) * head_scale.y + 0.0025, (joints["eye_r"].z - neck.z) * head_scale.z))
    if idx != 6:
        _mk_eyeball(bm, uv, eye_l_loc, 0.0134, M["eye"])
        _mk_eyeball(bm, uv, eye_r_loc, 0.0134, M["eye"])

    # Continuous `scalp`-extruded sculpted hair shell (ZERO meatball spheres!)
    hair_params = {
        0: (0.015, 0.008, 0.012, 0.009),  # Pirate: full wavy hair under bandana
        1: (0.016, 0.008, 0.018, 0.008),  # Smoothie: natural swept fringe peeking under beanie
        2: (0.016, 0.010, 0.014, 0.007),  # Tinfoil: messy fringe under foil hat
        3: (0.016, 0.008, 0.008, 0.010),  # Wizard: elder silver hair
        4: (0.019, 0.018, 0.014, 0.010),  # Sleepy: tousled bedhead volume
        5: (0.007, 0.003, 0.004, 0.002),  # Cyborg: close-cropped shaved sides under mohawk!
        6: (0.012, 0.006, 0.004, 0.004),  # Zen: neat swept silver hair + topknot
        7: (0.015, 0.010, 0.010, 0.006),  # Skolkovo: clean modern undercut
        8: (0.015, 0.012, 0.010, 0.005),  # Meetup: clean fade under tilted crown
        9: (0.016, 0.014, 0.004, 0.007),  # Friday Demon: slicked-back dark hair
        11:(0.008, 0.004, 0.002, 0.002),  # Security: sharp military buzzcut
    }
    hp = hair_params.get(idx, (0.015, 0.010, 0.010, 0.006))
    scalp_res = build_scalp_hair(human_obj, neck, head_scale, M["hair"], *hp)
    if scalp_res:
        _merge_bm(scalp_res[0], bm, scalp_res[1], uv)

    # Skull-centered accessory workspace: MPFB2 skull center in 1.08x head space is (0, -0.042, z)
    # Measured landmarks: crown top=(0, -0.051, 0.277), nose=(0, -0.174, 0.117), lips=(0, -0.154, 0.083)
    h_bm = bmesh.new()
    h_uv = h_bm.loops.layers.uv.new("UVMap")
    orig_bm, orig_uv = bm, uv
    bm, uv = h_bm, h_uv

    if idx == 0:
        # 0: VIBE-PIRATE — True Sculpted 3-Cornered Upturned Leather Tricorne + Gracefully Curved Burgundy Plume + Eyepatch + Beard!
        # Swept dark hair locks peeking beneath bandana
        for s in (-1, 1):
            _mk_tube(bm, uv, [(s * 0.082, 0.022, 0.165), (s * 0.098, 0.046, 0.100), (s * 0.092, 0.056, 0.048)], [0.022, 0.019, 0.009], M["hair"], "hair", 8)
        # Sculpted rugged goatee & mustache hugging chin/lips cleanly
        _mk_loft(bm, uv, [((0, -0.098, 0.068), 0.040, 0.020), ((0, -0.102, 0.032), 0.032, 0.018), ((0, -0.094, -0.002), 0.014, 0.012)], M["hair"], "hair", 14)
        _mk_tube(bm, uv, [(-0.044, -0.106, 0.084), (-0.016, -0.120, 0.096), (0.016, -0.120, 0.096), (0.044, -0.106, 0.084)], [0.006, 0.010, 0.010, 0.006], M["hair"], "hair", 8)
        # Snug burgundy bandana wrapped around brow + trailing ribbon tails
        _mk_loft(bm, uv, [((0, -0.002, 0.180), 0.098, 0.116), ((0, -0.002, 0.214), 0.100, 0.118)], M["accent"], "accent", 24, False, False, folds=0.018)
        for s in (-1, 1):
            _mk_tube(bm, uv, [(s * 0.014, 0.116, 0.194), (s * 0.046, 0.175, 0.168), (s * 0.070, 0.205, 0.122)], [0.012, 0.011, 0.007], M["accent"], "accent", 7)
        # Flush stitched leather eyepatch directly over left eye + sleek diagonal strap!
        ep_x = eye_l_loc.x
        ep_y = eye_l_loc.y + 0.042 - 0.010  # adjusted for h_bm -0.042 shift
        ep_z = eye_l_loc.z
        _mk_cyl(bm, uv, (ep_x, ep_y, ep_z), 0.025, 0.022, 0.010, M["coal"], "coal", 16, Euler((math.radians(90), 0, math.radians(-8))))
        _mk_tube(bm, uv, [(-0.088, -0.055, 0.195), (ep_x, ep_y - 0.004, ep_z), (0.098, -0.005, 0.125)], 0.0048, M["coal"], "coal", 6)

        # TRUE SCULPTED 3-CORNERED CURVED PIRATE TRICORNE HAT (Polar-modulated smooth upturned brim — ZERO flat boxes!)
        _mk_sphere(bm, uv, (0, 0.002, 0.232), (0.104, 0.116, 0.072), M["coal"], "coal", 22, 12)
        tmp = bmesh.new(); t_uv = tmp.loops.layers.uv.new("UVMap")
        segs = 36
        # Build smooth upturned 3-pointed tricorne brim: inner crown ring -> mid curve -> upturned outer edge!
        brim_rings = []
        for rad_scale, z_base, up_amp, thick in [
            (1.00, 0.214, 0.000, 0.012),
            (1.36, 0.218, 0.032, 0.011),
            (1.68, 0.244, 0.056, 0.009),
            (1.82, 0.278, 0.062, 0.007),
        ]:
            ring_out, ring_in = [], []
            for i in range(segs):
                a = i * math.tau / segs
                # 3 upturned lobes (front-center peak at -pi/2, back-left & back-right peaks!)
                tri = 0.5 + 0.5 * math.cos(3.0 * (a + math.pi * 0.5))
                # Pull edges gently inward where upturned so it forms authentic curled tricorne wings!
                curl_in = 1.0 - 0.16 * (rad_scale - 1.0) * tri
                rx = 0.104 * rad_scale * curl_in * (1.0 + 0.14 * (1.0 - tri))
                ry = 0.114 * rad_scale * curl_in * (1.0 + 0.10 * (1.0 - tri))
                px = rx * math.cos(a)
                py = 0.002 + ry * math.sin(a)
                pz = z_base + up_amp * (0.35 + 0.65 * tri)
                ring_out.append(tmp.verts.new((px, py, pz)))
                ring_in.append(tmp.verts.new((px * 0.96, py * 0.96, pz - thick)))
            brim_rings.append((ring_out, ring_in))
        for k in range(len(brim_rings) - 1):
            o0, i0 = brim_rings[k]
            o1, i1 = brim_rings[k + 1]
            for s in range(segs):
                sn = (s + 1) % segs
                for vs in ((o0[s], o0[sn], o1[sn], o1[s]), (i0[sn], i0[s], i1[s], i1[sn])):
                    f = tmp.faces.new(vs)
                    f.material_index = M["coal"]
                    f.smooth = True
        # Seal razor-thin outer rim edge with crisp antique gold piping!
        o_last, i_last = brim_rings[-1]
        for s in range(segs):
            sn = (s + 1) % segs
            f = tmp.faces.new((o_last[s], o_last[sn], i_last[sn], i_last[s]))
            f.material_index = M["gold"]
            f.smooth = True
        bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces)
        _assign_uv_box(tmp.faces, t_uv, 0.02, 0.48, 0.02, 0.48)
        _merge_bm(tmp, bm, t_uv, uv)
        # Crisp small ivory skull emblem centered on front upturned peak!
        _mk_sphere(bm, uv, (0, -0.158, 0.278), (0.020, 0.010, 0.022), M["ivory"], "ivory", 12, 8)
        for ang in (-35, 35):
            _mk_box(bm, uv, (0, -0.157, 0.264), (0.056, 0.008, 0.008), M["ivory"], "ivory", bevel=0.002, segs=1, rot=Euler((0, math.radians(ang), 0)))
        # Gracefully arched tapered burgundy feather plume tucked into right brim
        _mk_tube(bm, uv, [(-0.115, -0.020, 0.262), (-0.158, 0.015, 0.352), (-0.194, 0.078, 0.408), (-0.214, 0.138, 0.398)], [0.007, 0.014, 0.012, 0.003], M["accent"], "accent", 10)
        _mk_blade_fin(bm, uv, (-0.130, -0.005, 0.295), (-0.178, 0.052, 0.384), (-0.228, 0.148, 0.392), 0.019, M["accent"], "accent")

    elif idx == 1:
        # 1: SMOOTHIE MANIAC — Snug Ribbed Cuffed Charcoal-Sage Slouch Beanie (encloses crown cleanly, brows 100% clear!)
        beanie_xf = Matrix.Translation((0, 0.010, 0.208)) @ Euler((math.radians(18), 0, 0)).to_matrix().to_4x4()
        tmp = bmesh.new(); t_uv = tmp.loops.layers.uv.new("UVMap")
        _mk_loft(tmp, t_uv, [
            ((0, 0.000, -0.016), 0.114, 0.126),
            ((0, 0.000,  0.020), 0.116, 0.128),
            ((0, 0.005,  0.048), 0.108, 0.120),
            ((0, 0.018,  0.088), 0.094, 0.104),
            ((0, 0.026,  0.112), 0.060, 0.066),
            ((0, 0.030,  0.124), 0.020, 0.022),
        ], M["trim"], "trim", 24, folds=0.014)
        _mk_loft(tmp, t_uv, [
            ((0, 0.000, -0.018), 0.118, 0.130),
            ((0, 0.000,  0.012), 0.120, 0.132),
            ((0, 0.002,  0.022), 0.114, 0.126),
        ], M["coal"], "coal", 24, False, False, folds=0.018)
        _mk_curved_shell(tmp, t_uv, [((0, 0, -0.010), 0.118, 0.130), ((0, 0, 0.010), 0.119, 0.131)], -102, -78, 0.004, M["accent"], "accent", 6)
        bmesh.ops.transform(tmp, matrix=beanie_xf, verts=tmp.verts)
        _merge_bm(tmp, bm, t_uv, uv)

    elif idx == 2:
        # 2: TINFOIL CRYPTO-BRO — Crisp Origami-Folded Metallic Silver Tinfoil Hat + Thin Copper Spiral Antenna!
        _mk_loft(bm, uv, [
            ((0, 0.002, 0.200), 0.136, 0.146),
            ((0, 0.002, 0.222), 0.108, 0.118),
            ((0, 0.006, 0.295), 0.082, 0.088),
            ((0, 0.010, 0.368), 0.042, 0.045),
            ((0, 0.008, 0.425), 0.008, 0.008),
        ], M["steel"], "steel", segs=11, smooth=False, folds=0.055)
        _mk_loft(bm, uv, [((0, 0.002, 0.202), 0.122, 0.132), ((0, 0.002, 0.222), 0.115, 0.124)], M["steel"], "steel", 12, False, False, smooth=False)
        spiral = [(0.020 * math.cos(i * 1.45), 0.008 + 0.020 * math.sin(i * 1.45), 0.422 + i * 0.012) for i in range(9)]
        _mk_tube(bm, uv, spiral, 0.005, M["gold"], "gold", 8)
        _mk_sphere(bm, uv, (0, 0.008, 0.525), (0.016, 0.016, 0.016), M["glow"], "glow", 12, 8)

    elif idx == 3:
        # 3: 40-FRAMEWORK WIZARD — Sculpted Silver Wizard Beard + Crooked Starry Indigo Wizard Hat!
        for s in (-1, 1):
            _mk_tube(bm, uv, [(s * 0.082, 0.020, 0.175), (s * 0.096, 0.038, 0.088), (s * 0.086, 0.050, -0.012)], [0.024, 0.020, 0.011], M["hair"], "hair", 8)
        _mk_loft(bm, uv, [
            ((0, -0.094,  0.088), 0.070, 0.036),
            ((0, -0.104,  0.032), 0.060, 0.034),
            ((0, -0.104, -0.042), 0.046, 0.028),
            ((0, -0.094, -0.115), 0.020, 0.016),
        ], M["hair"], "hair", 16, folds=0.06)
        _mk_tube(bm, uv, [(-0.058, -0.098, 0.078), (-0.022, -0.120, 0.094), (0.022, -0.120, 0.094), (0.058, -0.098, 0.078)], [0.008, 0.014, 0.014, 0.008], M["hair"], "hair", 8)
        _mk_cyl(bm, uv, (0, 0.002, 0.222), 0.215, 0.205, 0.014, M["upper"], "upper", 28, Euler((math.radians(7), 0, 0)))
        _mk_loft(bm, uv, [
            ((0,  0.002, 0.226), 0.106, 0.116),
            ((0,  0.014, 0.305), 0.084, 0.090),
            ((0,  0.040, 0.390), 0.052, 0.056),
            ((0,  0.082, 0.468), 0.024, 0.026),
            ((0,  0.124, 0.515), 0.007, 0.007),
        ], M["upper"], "upper", 22, folds=0.035)
        _mk_loft(bm, uv, [((0, 0.003, 0.230), 0.110, 0.120), ((0, 0.007, 0.258), 0.105, 0.115)], M["gold"], "gold", 24, False, False)
        _mk_curved_shell(bm, uv, [((0, 0.005, 0.230), 0.111, 0.121), ((0, 0.007, 0.258), 0.107, 0.117)], -102, -78, 0.007, M["glow"], "glow", 6)

    elif idx == 4:
        # 4: SLEEPY TEAMLEAD — Tousled Bedhead (ZERO Mickey-Mouse balls!) + Contoured Curved Silk Sleep Mask + Plush Neck Pillow + Slim Mic!
        # Subtle messy swept tuft lock on crown (single sculpted curved lock, not balls!)
        _mk_blade_fin(bm, uv, (-0.015, -0.020, 0.268), (0.028, 0.032, 0.270), (0.048, 0.062, 0.305), 0.018, M["hair"], "hair")
        # Contoured curved dusty-rose silk sleep mask hugging the forehead!
        _mk_curved_shell(bm, uv, [
            ((0, 0.000, 0.184), 0.092, 0.108),
            ((0, 0.000, 0.208), 0.094, 0.112),
            ((0, 0.002, 0.232), 0.090, 0.106),
        ], -134, -46, 0.012, M["accent"], "accent", 14)
        # Slim elastic strap around back of head
        _mk_loft(bm, uv, [((0, 0.002, 0.200), 0.098, 0.113), ((0, 0.002, 0.214), 0.098, 0.113)], M["coal"], "coal", 22, False, False)
        # Soft U-shaped cream memory-foam travel neck pillow tucked neatly around neck
        pillow_pts = [(0.116 * math.sin(math.radians(-125 + i * 25.0)), 0.008 + 0.112 * math.cos(math.radians(-125 + i * 25.0)), 0.024) for i in range(11)]
        _mk_tube(bm, uv, pillow_pts, 0.038, M["ivory"], "ivory", 14, cap=True)
        # Slim call-center earpiece & boom mic
        _mk_cyl(bm, uv, (-0.098, 0.004, 0.145), 0.024, 0.020, 0.018, M["coal"], "coal", 14, Euler((0, math.radians(90), 0)))
        _mk_tube(bm, uv, [(-0.098, -0.006, 0.135), (-0.090, -0.082, 0.105), (-0.030, -0.122, 0.090)], 0.0045, M["coal"], "coal", 7)
        _mk_sphere(bm, uv, (-0.024, -0.124, 0.090), (0.010, 0.009, 0.009), M["coal"], "coal", 10, 8)

    elif idx == 5:
        # 5: PUNK CYBORG REFACTORER — Swept-Back 7-Blade Copper-Amber Punk Mohawk (NO traffic cone!) + Weathered Headband + Sleek Cyber Temple!
        # 7 overlapping flat tapered shark-fin mohawk blades sweeping dynamically back from forehead to nape!
        mohawk_fins = [
            ((0, -0.095, 0.238), (0, -0.055, 0.262), (0, -0.068, 0.362), 0.016),
            ((0, -0.062, 0.258), (0, -0.018, 0.275), (0, -0.022, 0.392), 0.018),
            ((0, -0.024, 0.272), (0,  0.022, 0.278), (0,  0.030, 0.398), 0.018),
            ((0,  0.016, 0.275), (0,  0.060, 0.268), (0,  0.085, 0.382), 0.017),
            ((0,  0.054, 0.266), (0,  0.090, 0.244), (0,  0.138, 0.342), 0.015),
            ((0,  0.082, 0.242), (0,  0.105, 0.205), (0,  0.165, 0.278), 0.013),
            ((0,  0.096, 0.202), (0,  0.104, 0.158), (0,  0.162, 0.212), 0.011),
        ]
        for ba, bb, tp, hw in mohawk_fins:
            _mk_blade_fin(bm, uv, ba, bb, tp, hw, M["accent"], "accent")
        # Snug crimson cloth headband + trailing knot tails
        _mk_loft(bm, uv, [((0, -0.002, 0.188), 0.098, 0.114), ((0, -0.002, 0.216), 0.099, 0.115)], M["trim"], "trim", 24, False, False)
        for s in (-1, 1):
            _mk_tube(bm, uv, [(s * 0.015, 0.116, 0.200), (s * 0.044, 0.172, 0.178), (s * 0.072, 0.215, 0.145)], [0.011, 0.010, 0.006], M["trim"], "trim", 6)
        # Small subtle flush nose-bridge strip (NOT a giant white cross!)
        _mk_curved_shell(bm, uv, [((0, -0.042, 0.122), 0.020, 0.088), ((0, -0.042, 0.134), 0.020, 0.088)], -104, -76, 0.003, M["ivory"], "ivory", 6)
        # Sleek brushed-steel cybernetic temple implant + subtle amber sensor LED
        _mk_curved_shell(bm, uv, [((0, 0.000, 0.138), 0.091, 0.105), ((0, 0.000, 0.172), 0.091, 0.105)], -185, -150, 0.008, M["steel"], "steel", 6)
        _mk_sphere(bm, uv, (-0.096, -0.022, 0.155), (0.007, 0.007, 0.007), M["glow"], "glow", 8, 6)

    elif idx == 6:
        # 6: ZEN MONK — Neat Silver Topknot + Sculpted Flowing Silver Beard + Ornate Double Golden Halo!
        _mk_sphere(bm, uv, (0, 0.036, 0.292), (0.040, 0.040, 0.036), M["hair"], "hair", 14, 10)
        _mk_cyl(bm, uv, (0, 0.032, 0.270), 0.032, 0.032, 0.012, M["gold"], "gold", 14)
        _mk_loft(bm, uv, [((0, -0.090, 0.088), 0.066, 0.036), ((0, -0.098, 0.034), 0.054, 0.030), ((0, -0.092, -0.022), 0.026, 0.018)], M["hair"], "hair", 16, folds=0.05)
        halo1 = [(0.138 * math.cos(i * math.tau / 28), 0.138 * math.sin(i * math.tau / 28), 0.362) for i in range(29)]
        halo2 = [(0.106 * math.cos(i * math.tau / 24), 0.106 * math.sin(i * math.tau / 24), 0.366) for i in range(25)]
        _mk_tube(bm, uv, halo1, 0.013, M["glow"], "glow", 10, cap=False)
        _mk_tube(bm, uv, halo2, 0.007, M["gold"], "gold", 8, cap=False)

    elif idx == 7:
        # 7: SKOLKOVO VR SHAMAN — Sleek Curved Wrap-Around Cyberpunk VR Visor (NO giant flat brick!) + Dual Telemetry Antennas!
        _mk_curved_shell(bm, uv, [
            ((0, -0.008, 0.118), 0.096, 0.118),
            ((0, -0.008, 0.155), 0.102, 0.128),
            ((0, -0.008, 0.192), 0.096, 0.118),
        ], -148, -32, 0.022, M["coal"], "coal", 16)
        _mk_curved_shell(bm, uv, [
            ((0, -0.008, 0.132), 0.116, 0.146),
            ((0, -0.008, 0.178), 0.116, 0.146),
        ], -134, -46, 0.008, M["glow"], "glow", 14)
        _mk_loft(bm, uv, [((0, 0.004, 0.142), 0.100, 0.115), ((0, 0.004, 0.170), 0.100, 0.115)], M["coal"], "coal", 22, False, False)
        for s in (-1, 1):
            _mk_tube(bm, uv, [(s * 0.102, -0.018, 0.172), (s * 0.132, -0.005, 0.345)], 0.007, M["steel"], "steel", 8)
            _mk_sphere(bm, uv, (s * 0.132, -0.005, 0.358), (0.018, 0.018, 0.018), M["glow"], "glow", 12, 8)

    elif idx == 8:
        # 8: MEETUP CROWN BOSS — Refined Tilted Jeweled Golden Crown with Velvet Cushion!
        crown_xf = Matrix.Translation((0.014, 0.0, 0.256)) @ Euler((math.radians(-6), math.radians(-12), 0)).to_matrix().to_4x4()
        tmp = bmesh.new(); t_uv = tmp.loops.layers.uv.new("UVMap")
        _mk_loft(tmp, t_uv, [((0, 0, -0.022), 0.098, 0.110), ((0, 0, 0.044), 0.118, 0.128)], M["gold"], "gold", 20)
        _mk_sphere(tmp, t_uv, (0, 0, 0.030), (0.092, 0.102, 0.050), M["accent"], "accent", 16, 10)
        _mk_loft(tmp, t_uv, [((0, 0, -0.018), 0.102, 0.114), ((0, 0, -0.002), 0.105, 0.117)], M["coal"], "coal", 20, False, False)
        for k in range(6):
            a = k * math.tau / 6
            px, py = 0.112 * math.cos(a), 0.120 * math.sin(a)
            _mk_cyl(tmp, t_uv, (px, py, 0.080), 0.022, 0.004, 0.072, M["gold"], "gold", 8)
            _mk_sphere(tmp, t_uv, (px, py, 0.118), (0.013, 0.013, 0.013), M["accent"] if k % 2 == 0 else M["glow"], "accent", 10, 8)
        bmesh.ops.transform(tmp, matrix=crown_xf, verts=tmp.verts)
        _merge_bm(tmp, bm, t_uv, uv)

    elif idx == 9:
        # 9: FRIDAY 19:59 PROD DEMON — Sleek Sculpted Ridged Curved Demon Horns!
        for s in (-1, 1):
            _mk_tube(bm, uv, [
                (s * 0.052, -0.048, 0.236),
                (s * 0.112, -0.062, 0.312),
                (s * 0.100, -0.090, 0.398),
                (s * 0.052, -0.108, 0.446),
            ], [0.030, 0.023, 0.015, 0.004], M["upper"], "upper", 12, cap=True)
            for rk in range(3):
                _mk_cyl(bm, uv, (s * (0.062 + rk * 0.017), -0.052 - rk * 0.004, 0.246 + rk * 0.026), 0.032 - rk * 0.004, 0.030 - rk * 0.004, 0.010, M["coal"], "coal", 12, Euler((0, s * math.radians(-22), 0)))

    else:
        # 11: TOWER SECURITY CHIEF — Crisp Sculpted Peaked Officer Cap + Slim Chrome Aviator Sunglasses!
        _mk_loft(bm, uv, [
            ((0, 0.006, 0.212), 0.104, 0.114),
            ((0, 0.002, 0.278), 0.120, 0.126),
            ((0, 0.000, 0.296), 0.106, 0.114),
        ], M["coal"], "coal", 26)
        _mk_loft(bm, uv, [((0, 0.006, 0.210), 0.106, 0.116), ((0, 0.006, 0.224), 0.107, 0.117)], M["gold"], "gold", 26, False, False)
        _mk_curved_shell(bm, uv, [((0, -0.028, 0.210), 0.096, 0.105), ((0, -0.062, 0.192), 0.112, 0.132)], -136, -44, 0.009, M["coal"], "coal", 14)
        _mk_cyl(bm, uv, (0, -0.116, 0.252), 0.020, 0.017, 0.010, M["gold"], "gold", 14, Euler((math.radians(90), 0, 0)))
        # Slim contoured chrome aviator sunglasses!
        for s in (-1, 1):
            _mk_box(bm, uv, (s * 0.038, -0.120, 0.154), (0.050, 0.006, 0.034), M["steel"], "steel", bevel=0.004, segs=2, rot=Euler((0, 0, s * math.radians(-6))))
            _mk_box(bm, uv, (s * 0.038, -0.123, 0.154), (0.042, 0.005, 0.026), M["coal"], "coal", bevel=0.003, segs=1, rot=Euler((0, 0, s * math.radians(-6))))
            _mk_box(bm, uv, (s * 0.072, -0.062, 0.156), (0.005, 0.112, 0.005), M["steel"], "steel", bevel=0.001, segs=1)
        _mk_box(bm, uv, (0, -0.121, 0.164), (0.024, 0.005, 0.004), M["steel"], "steel", bevel=0.001, segs=1)

    bmesh.ops.transform(h_bm, matrix=Matrix.Translation((0.0, -0.042, 0.0)), verts=h_bm.verts)
    _merge_bm(h_bm, orig_bm, h_uv, orig_uv)
    return orig_bm


# =============================================================================
# 12 UNIQUE TAILORED OUTFIT TORSOS (All centered on exact MPFB2 TORSO_CY = -0.028!)
# =============================================================================
def build_torso_part(idx, r, human_obj, joints, M):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    pelvis = joints["pelvis"]
    neck_z = joints["neck"].z

    inflate_amt = 0.026 if idx == 8 else (0.022 if idx in (0, 3, 4, 6, 7) else 0.016)
    torso_bones = ["pelvis", "spine_01", "spine_02", "spine_03", "clavicle_l", "clavicle_r"]
    torso_shell = extract_human_subset(
        human_obj, "body",
        lambda c: (c.z >= pelvis.z - 0.04) and (c.z <= neck_z - 0.012),
        decimate_ratio=0.48, mat_idx=M["upper"], uv_mode="upper",
        offset_vec=Vector((0, pelvis.y, pelvis.z)), inflate=inflate_amt, bone_groups=torso_bones, weight_thresh=0.14
    )
    if torso_shell:
        _merge_bm(torso_shell[0], bm, torso_shell[1], uv)

    pelvis_pants = extract_human_subset(
        human_obj, "body",
        lambda c: (c.z >= pelvis.z - 0.12) and (c.z <= pelvis.z + 0.06),
        decimate_ratio=0.45, mat_idx=M["lower"], uv_mode="lower",
        offset_vec=Vector((0, pelvis.y, pelvis.z)), inflate=0.015, bone_groups=["pelvis", "thigh_l", "thigh_r"], weight_thresh=0.14
    )
    if pelvis_pants:
        _merge_bm(pelvis_pants[0], bm, pelvis_pants[1], uv)

    # Smooth anatomical neck column tucked into collar
    _mk_loft(bm, uv, [((0, -0.022, 0.490), 0.060, 0.060), ((0, -0.024, 0.565), 0.052, 0.054)], M["skin"], "skin", 16)

    CY = TORSO_CY

    if idx == 0:
        # 0: PIRATE CAPTAIN COAT — Tailored flared frock coat skirt, curved gold-piped lapels, brass buttons, burgundy sash & sleek baldric!
        _mk_loft(bm, uv, [
            ((0, CY + 0.008, -0.255), 0.238, 0.168),
            ((0, CY + 0.004, -0.095), 0.210, 0.148),
            ((0, CY + 0.000,  0.045), 0.194, 0.136),
        ], M["upper"], "upper", 24, False, False, folds=0.045)
        # Wrapped burgundy waist sash + naturally draping side knot!
        _mk_loft(bm, uv, [((0, CY, 0.025), 0.198, 0.142), ((0, CY, 0.095), 0.196, 0.140)], M["accent"], "accent", 24, False, False, folds=0.022)
        _mk_tube(bm, uv, [(0.155, CY - 0.085, 0.055), (0.175, CY - 0.095, -0.035), (0.168, CY - 0.090, -0.145)], [0.020, 0.018, 0.012], M["accent"], "accent", 8)
        # Contoured open coat lapels hugging chest + subtle gold edge trim + brass buttons
        for s in (-1, 1):
            a0, a1 = (-124, -95) if s < 0 else (-85, -56)
            _mk_curved_shell(bm, uv, [
                ((0, CY, 0.220), 0.176, 0.134),
                ((0, CY, 0.340), 0.184, 0.142),
                ((0, CY, 0.455), 0.168, 0.128),
            ], a0, a1, 0.010, M["upper"], "upper", 8)
            for bk in range(3):
                _mk_cyl(bm, uv, (s * 0.058, CY - 0.146, 0.155 + bk * 0.072), 0.011, 0.011, 0.008, M["gold"], "gold", 12, Euler((math.radians(90), 0, 0)))
        # Sleek diagonal leather baldric with compact brass buckle
        _mk_tube(bm, uv, [(-0.115, CY - 0.070, 0.480), (0.0, CY - 0.148, 0.285), (0.148, CY - 0.095, 0.085), (0.162, CY + 0.110, 0.120), (-0.105, CY + 0.110, 0.475)], 0.015, M["coal"], "coal", 8)
        _mk_box(bm, uv, (0.0, CY - 0.156, 0.285), (0.046, 0.012, 0.044), M["gold"], "gold", bevel=0.004, segs=1)

    elif idx == 1:
        # 1: SMOOTHIE MANIAC — Soft relaxed hoodie hood + CLEAN BODY-HUGGING BARISTA APRON (zero z-fighting!)
        _mk_loft(bm, uv, [((0, CY + 0.105, 0.425), 0.132, 0.064, 22), ((0, CY + 0.112, 0.488), 0.142, 0.068, 10), ((0, CY + 0.036, 0.535), 0.100, 0.084, -18)], M["upper"], "upper", 22)
        _mk_curved_shell(bm, uv, [
            ((0, CY, -0.115), 0.188, 0.136),
            ((0, CY,  0.045), 0.186, 0.134),
            ((0, CY,  0.155), 0.184, 0.134),
        ], -150, -30, 0.007, M["trim"], "trim", 16)
        _mk_curved_shell(bm, uv, [
            ((0, CY, 0.150), 0.184, 0.134),
            ((0, CY, 0.255), 0.186, 0.142),
            ((0, CY, 0.340), 0.178, 0.138),
        ], -120, -60, 0.007, M["trim"], "trim", 10)
        _mk_loft(bm, uv, [((0, CY, 0.122), 0.194, 0.144), ((0, CY, 0.148), 0.194, 0.144)], M["coal"], "coal", 24, False, False)
        _mk_tube(bm, uv, [(-0.072, CY - 0.128, 0.338), (-0.064, CY - 0.044, 0.502), (0.0, CY + 0.092, 0.506), (0.064, CY - 0.044, 0.502), (0.072, CY - 0.128, 0.338)], 0.006, M["coal"], "coal", 8)

    elif idx == 2:
        # 2: TINFOIL CRYPTO-BRO — Stand-up zip windbreaker collar + Snug Cuban Gold Chain & Medallion!
        _mk_loft(bm, uv, [((0, CY + 0.006, 0.468), 0.104, 0.092), ((0, CY + 0.006, 0.535), 0.108, 0.096)], M["trim"], "trim", 22, False, False)
        _mk_curved_shell(bm, uv, [((0, CY, 0.075), 0.186, 0.138), ((0, CY, 0.455), 0.178, 0.136)], -93, -87, 0.007, M["steel"], "steel", 4)
        chain_pts = [(-0.088, CY - 0.035, 0.482), (-0.076, CY - 0.135, 0.388), (-0.040, CY - 0.152, 0.285), (0.0, CY - 0.154, 0.242), (0.040, CY - 0.152, 0.285), (0.076, CY - 0.135, 0.388), (0.088, CY - 0.035, 0.482)]
        _mk_tube(bm, uv, chain_pts, 0.014, M["gold"], "gold", 10)
        _mk_cyl(bm, uv, (0.0, CY - 0.160, 0.218), 0.052, 0.052, 0.015, M["gold"], "gold", 20, Euler((math.radians(90), 0, 0)))
        _mk_cyl(bm, uv, (0.0, CY - 0.168, 0.218), 0.032, 0.032, 0.006, M["coal"], "coal", 16, Euler((math.radians(90), 0, 0)))

    elif idx == 3:
        # 3: 40-FRAMEWORK WIZARD — Full draped starry wizard robe skirt + shoulder mantle + knotted gold rope sash!
        _mk_loft(bm, uv, [
            ((0, CY + 0.004, -0.340), 0.262, 0.195),
            ((0, CY + 0.002, -0.140), 0.224, 0.164),
            ((0, CY + 0.000,  0.045), 0.198, 0.144),
        ], M["upper"], "upper", 26, False, False, folds=0.06)
        _mk_loft(bm, uv, [((0, CY + 0.010, 0.375), 0.236, 0.162), ((0, CY + 0.005, 0.468), 0.172, 0.126), ((0, CY + 0.002, 0.515), 0.104, 0.092)], M["upper"], "upper", 24, False, False, folds=0.035)
        rope_pts = [(0.198 * math.cos(i * math.tau / 22), CY + 0.146 * math.sin(i * math.tau / 22), 0.075) for i in range(23)]
        _mk_tube(bm, uv, rope_pts, 0.015, M["gold"], "gold", 8, cap=False)
        for s in (-1, 1):
            _mk_tube(bm, uv, [(s * 0.018, CY - 0.148, 0.075), (s * 0.048, CY - 0.160, -0.055), (s * 0.058, CY - 0.164, -0.165)], 0.011, M["gold"], "gold", 7)

    elif idx == 4:
        # 4: SLEEPY TEAMLEAD — Cozy Waffle Bathrobe with Sleek Tapered Shawl Collar V + Natural Side-Tied Belt (ZERO white block!)
        _mk_loft(bm, uv, [
            ((0, CY + 0.006, -0.245), 0.232, 0.168),
            ((0, CY + 0.003, -0.060), 0.208, 0.150),
            ((0, CY + 0.000,  0.055), 0.194, 0.142),
        ], M["upper"], "upper", 24, False, False, folds=0.045)
        # Sleek body-hugging curved shawl collar tapering smoothly into a V down to the waist belt!
        for s in (-1, 1):
            _mk_tube(bm, uv, [
                (s * 0.062, CY + 0.048, 0.498),
                (s * 0.080, CY - 0.072, 0.450),
                (s * 0.056, CY - 0.136, 0.335),
                (s * 0.026, CY - 0.134, 0.215),
                (s * 0.006, CY - 0.132, 0.105),
            ], [0.015, 0.018, 0.017, 0.014, 0.012], M["trim"], "trim", 10)
        # Clean snug wrapped bathrobe belt + small neat offset side-knot on left hip with natural straight hanging ends!
        _mk_loft(bm, uv, [((0, CY, 0.072), 0.198, 0.145), ((0, CY, 0.112), 0.197, 0.144)], M["trim"], "trim", 24, False, False)
        _mk_sphere(bm, uv, (0.095, CY - 0.132, 0.092), (0.020, 0.014, 0.018), M["trim"], "trim", 10, 8)
        _mk_tube(bm, uv, [(0.092, CY - 0.134, 0.088), (0.104, CY - 0.142, -0.025), (0.108, CY - 0.144, -0.100)], [0.012, 0.011, 0.008], M["trim"], "trim", 7)
        _mk_tube(bm, uv, [(0.100, CY - 0.132, 0.086), (0.082, CY - 0.140, -0.015), (0.076, CY - 0.142, -0.080)], [0.012, 0.011, 0.008], M["trim"], "trim", 7)

    elif idx == 5:
        # 5: PUNK CYBORG REFACTORER — Tailored Matte-Black Biker Leather Vest with Diagonal Chrome Zip & Collar Lapels (ZERO red slab!)
        # Angled folded biker vest lapels & diagonal brushed-steel motorcycle zipper hugging exact chest contour
        for s in (-1, 1):
            _mk_tube(bm, uv, [
                (s * 0.064, CY + 0.042, 0.495),
                (s * 0.096, CY - 0.078, 0.440),
                (s * 0.086, CY - 0.140, 0.345),
                (s * 0.032, CY - 0.138, 0.255),
            ], [0.012, 0.018, 0.017, 0.010], M["coal"], "coal", 8)
        # Diagonal chrome biker zipper + subtle crimson shoulder strap accent
        _mk_tube(bm, uv, [(0.056, CY - 0.138, 0.425), (0.024, CY - 0.142, 0.265), (0.004, CY - 0.134, 0.095)], 0.0055, M["steel"], "steel", 6)
        _mk_tube(bm, uv, [(-0.085, CY - 0.065, 0.482), (-0.078, CY - 0.136, 0.375), (-0.065, CY - 0.132, 0.095)], 0.009, M["trim"], "trim", 6)
        # Contoured heavy studded leather belt + brushed steel buckle
        _mk_loft(bm, uv, [((0, CY, 0.036), 0.192, 0.142), ((0, CY, 0.078), 0.192, 0.142)], M["coal"], "coal", 24, False, False)
        _mk_box(bm, uv, (0, CY - 0.146, 0.056), (0.056, 0.012, 0.046), M["steel"], "steel", bevel=0.004, segs=1)
        # Compact spine cyber-battery feeding the right cyber-arm
        _mk_box(bm, uv, (-0.032, CY + 0.148, 0.335), (0.135, 0.050, 0.170), M["steel"], "steel", bevel=0.010, segs=2)
        _mk_cyl(bm, uv, (-0.032, CY + 0.174, 0.335), 0.026, 0.026, 0.015, M["glow"], "glow", 14, Euler((math.radians(90), 0, 0)))

    elif idx == 6:
        # 6: ZEN MONK — Layered Monk Tunic Skirt + Carved Wooden Prayer Beads (Mala) Draped Naturally Over Chest!
        _mk_loft(bm, uv, [((0, CY + 0.004, -0.220), 0.224, 0.166), ((0, CY, 0.045), 0.196, 0.144)], M["upper"], "upper", 24, False, False, folds=0.045)
        for k in range(13):
            t = k / 12.0
            a = -1.25 + t * 2.50
            bx = 0.112 * math.sin(a)
            by = CY - 0.144 - 0.012 * math.cos(a)
            bz = 0.452 - 0.205 * math.cos(a * 0.82)
            _mk_sphere(bm, uv, (bx, by, bz), (0.020, 0.020, 0.020), M["wood"], "wood", 10, 8)

    elif idx == 7:
        # 7: SKOLKOVO VR SHAMAN — Contoured Techwear Chest Rig + Compact Server-Rack Jetpack!
        _mk_curved_shell(bm, uv, [
            ((0, CY, 0.215), 0.188, 0.144),
            ((0, CY, 0.335), 0.190, 0.146),
        ], -128, -52, 0.018, M["coal"], "coal", 12)
        _mk_curved_shell(bm, uv, [((0, CY, 0.275), 0.206, 0.162), ((0, CY, 0.292), 0.206, 0.162)], -118, -62, 0.006, M["glow"], "glow", 10)
        _mk_box(bm, uv, (0, CY + 0.195, 0.305), (0.245, 0.112, 0.305), M["coal"], "coal", bevel=0.016, segs=2)
        for k in range(4):
            _mk_box(bm, uv, (0, CY + 0.252, 0.220 + k * 0.052), (0.195, 0.008, 0.014), M["glow"] if k % 2 == 0 else M["gold"], "glow", bevel=0.002, segs=1)
        for s in (-1, 1):
            _mk_cyl(bm, uv, (s * 0.082, CY + 0.210, 0.295), 0.048, 0.044, 0.280, M["steel"], "steel", 16)
            _mk_cyl(bm, uv, (s * 0.082, CY + 0.210, 0.130), 0.036, 0.052, 0.068, M["coal"], "coal", 16)
            _mk_sphere(bm, uv, (s * 0.082, CY + 0.210, 0.098), (0.032, 0.032, 0.022), M["glow"], "glow", 12, 8)

    elif idx == 8:
        # 8: MEETUP CROWN BOSS — Thick Quilted Bone-Cream Puffer Collar & Smooth Baffles!
        _mk_loft(bm, uv, [((0, CY + 0.004, 0.455), 0.120, 0.106), ((0, CY + 0.004, 0.540), 0.126, 0.114)], M["upper"], "upper", 24, False, False)
        for bk in range(3):
            _mk_loft(bm, uv, [
                ((0, CY, 0.105 + bk * 0.112), 0.204, 0.152),
                ((0, CY, 0.158 + bk * 0.112), 0.214, 0.162),
                ((0, CY, 0.202 + bk * 0.112), 0.198, 0.146),
            ], M["upper"], "upper", 24, False, False)

    elif idx == 9:
        # 9: FRIDAY 19:59 PROD DEMON — Tailored Oxblood Suit, Crisp Folded Lapels, Slim Gold-Ember Tie & Spade Devil Tail!
        _mk_loft(bm, uv, [((0, CY + 0.004, -0.140), 0.200, 0.146), ((0, CY, 0.045), 0.190, 0.138)], M["upper"], "upper", 24, False, False)
        for s in (-1, 1):
            _mk_tube(bm, uv, [
                (s * 0.056, CY + 0.025, 0.500),
                (s * 0.068, CY - 0.058, 0.482),
                (s * 0.024, CY - 0.102, 0.448),
            ], [0.009, 0.011, 0.008], M["ivory"], "ivory", 8)
            _mk_tube(bm, uv, [
                (s * 0.064, CY + 0.036, 0.495),
                (s * 0.088, CY - 0.078, 0.440),
                (s * 0.056, CY - 0.138, 0.330),
                (s * 0.014, CY - 0.134, 0.220),
            ], [0.013, 0.017, 0.016, 0.010], M["trim"], "trim", 8)
        _mk_tube(bm, uv, [
            (0.0, CY - 0.100, 0.450),
            (0.0, CY - 0.134, 0.365),
            (0.0, CY - 0.140, 0.275),
            (0.0, CY - 0.130, 0.175),
        ], [0.012, 0.014, 0.016, 0.007], M["accent"], "accent", 6)
        _mk_tube(bm, uv, [(0.0, CY + 0.135, 0.030), (0.05, CY + 0.255, 0.012), (0.15, CY + 0.360, 0.098), (0.21, CY + 0.375, 0.250), (0.16, CY + 0.325, 0.355)], [0.020, 0.018, 0.015, 0.012, 0.007], M["upper"], "upper", 10)
        _mk_cyl(bm, uv, (0.152, CY + 0.318, 0.378), 0.032, 0.004, 0.070, M["upper"], "upper", 4, Euler((math.radians(-22), math.radians(-18), 0)))

    elif idx == 10:
        # 10: LEGACY 1998 SYSADMIN — Crisp Folded Shirt Collar, Sleek Flush Burgundy Tie, Pens & Retro Hip Pager (ZERO square chest box!)
        for s in (-1, 1):
            _mk_tube(bm, uv, [
                (s * 0.056, CY + 0.025, 0.502),
                (s * 0.068, CY - 0.055, 0.486),
                (s * 0.026, CY - 0.098, 0.456),
            ], [0.010, 0.012, 0.009], M["ivory"], "ivory", 8)
        # Slim necktie curving smoothly along chest contour!
        _mk_tube(bm, uv, [
            (0.0, CY - 0.096, 0.458),
            (0.0, CY - 0.132, 0.375),
            (0.0, CY - 0.138, 0.285),
            (0.0, CY - 0.128, 0.195),
        ], [0.012, 0.014, 0.016, 0.007], M["accent"], "accent", 6)
        # 3 colored pens tucked cleanly into left chest knit
        for pk, pmat in enumerate((M["accent"], M["glow"], M["coal"])):
            _mk_cyl(bm, uv, (0.068 + pk * 0.013, CY - 0.130, 0.352), 0.0045, 0.0045, 0.036, pmat, "accent", 8)
        _mk_box(bm, uv, (-0.180, CY - 0.015, 0.048), (0.028, 0.070, 0.048), M["coal"], "coal", bevel=0.005, segs=1)
        _mk_box(bm, uv, (-0.195, CY - 0.015, 0.050), (0.005, 0.048, 0.022), M["glow"], "glow", bevel=0.002, segs=1)

    else:
        # 11: TOWER SECURITY CHIEF — Contoured Curved Ballistic Kevlar Vest + Low-Profile Pouches + Brass Badge + Radio!
        _mk_curved_shell(bm, uv, [
            ((0, CY, 0.120), 0.194, 0.146),
            ((0, CY, 0.275), 0.198, 0.150),
            ((0, CY, 0.410), 0.182, 0.138),
        ], -148, -32, 0.020, M["coal"], "coal", 14)
        _mk_curved_shell(bm, uv, [
            ((0, CY, 0.120), 0.194, 0.146),
            ((0, CY, 0.275), 0.198, 0.150),
            ((0, CY, 0.410), 0.182, 0.138),
        ], 32, 148, 0.020, M["coal"], "coal", 14)
        for sx in (-0.082, 0.0, 0.082):
            _mk_box(bm, uv, (sx, CY - 0.172, 0.165), (0.062, 0.028, 0.084), M["trim"], "trim", bevel=0.006, segs=1)
        _mk_cyl(bm, uv, (0.084, CY - 0.168, 0.352), 0.024, 0.020, 0.009, M["gold"], "gold", 14, Euler((math.radians(90), 0, 0)))
        _mk_box(bm, uv, (-0.110, CY - 0.152, 0.395), (0.040, 0.022, 0.056), M["coal"], "coal", bevel=0.005, segs=1)
        _mk_tube(bm, uv, [(-0.110, CY - 0.152, 0.422), (-0.120, CY - 0.145, 0.505)], 0.0038, M["coal"], "coal", 6)
        _mk_loft(bm, uv, [((0, CY, 0.028), 0.204, 0.154), ((0, CY, 0.072), 0.204, 0.154)], M["coal"], "coal", 24, False, False)
        _mk_box(bm, uv, (0.0, CY - 0.160, 0.050), (0.062, 0.016, 0.048), M["gold"], "gold", bevel=0.004, segs=1)

    return bm


# =============================================================================
# ARMS, HANDS & SIGNATURE LIMB MODS (Seamless weight_thresh=0.14 + Real 5-Finger Hands!)
# =============================================================================
def build_arm_parts(idx, r, human_obj, joints, M, side):
    shld = joints["shld_l"] if side > 0 else joints["shld_r"]
    elbow = joints["elbow_l"] if side > 0 else joints["elbow_r"]
    hand = joints["hand_l"] if side > 0 else joints["hand_r"]
    upper_len = max(0.20, shld.z - elbow.z)
    scale_u = Vector((1.0, 1.0, 0.260 / upper_len))

    sfx = "l" if side > 0 else "r"
    hand_bones = [f"hand_{sfx}"] + [f"{fn}_0{k}_{sfx}" for fn in ("index", "middle", "ring", "pinky", "thumb") for k in (1, 2, 3)]

    # --- UPPER ARM ---
    ubm = bmesh.new(); uuv = ubm.loops.layers.uv.new("UVMap")
    if idx == 5 and side < 0:
        _mk_sphere(ubm, uuv, (0.014, 0, 0.008), (0.082, 0.080, 0.074), M["coal"], "coal", 16, 10)
        _mk_box(ubm, uuv, (-0.038, 0, -0.010), (0.078, 0.106, 0.098), M["steel"], "steel", bevel=0.012, segs=2, rot=Euler((0, math.radians(-18), 0)))
        _mk_cyl(ubm, uuv, (0, 0, -0.135), 0.044, 0.042, 0.220, M["steel"], "steel", 16)
        for oy in (-0.034, 0.034):
            _mk_cyl(ubm, uuv, (-0.030, oy, -0.135), 0.011, 0.011, 0.205, M["coal"], "coal", 10)
        _mk_cyl(ubm, uuv, (0, -0.040, -0.135), 0.010, 0.010, 0.180, M["glow"], "glow", 10)
        _mk_sphere(ubm, uuv, (0, 0, -0.258), (0.050, 0.050, 0.050), M["coal"], "coal", 14, 10)
    else:
        u_inf = 0.024 if idx == 8 else (0.018 if idx in (0, 3, 4, 7) else 0.012)
        u_mat = M["skin"] if (idx == 5 and side > 0) else M["upper"]
        u_uvk = "skin" if (idx == 5 and side > 0) else "upper"
        u_res = extract_human_subset(
            human_obj, "body", lambda c: True,
            decimate_ratio=0.48, mat_idx=u_mat, uv_mode=u_uvk,
            offset_vec=shld, scale_vec=scale_u, inflate=0.0 if u_mat == M["skin"] else u_inf,
            bone_groups=[f"upperarm_{sfx}"], weight_thresh=0.14
        )
        if u_res:
            _merge_bm(u_res[0], ubm, u_res[1], uuv)
        # Smooth anatomical shoulder & elbow caps so arms NEVER show seam gaps when bent!
        _mk_sphere(ubm, uuv, (-side * 0.010, 0, 0.002), (0.062, 0.062, 0.062), u_mat, u_uvk, 14, 10)
        _mk_sphere(ubm, uuv, (0, 0, -0.256), (0.046 + (0.0 if u_mat == M["skin"] else u_inf * 0.5), 0.046 + (0.0 if u_mat == M["skin"] else u_inf * 0.5), 0.046), u_mat, u_uvk, 12, 8)

    # --- FOREARM + REAL 5-FINGERED HAND (or Hook / Cyber-Claw / Iced Matcha / Wizard Staff!) ---
    fbm = bmesh.new(); fuv = fbm.loops.layers.uv.new("UVMap")
    if idx == 0 and side < 0:
        _mk_loft(fbm, fuv, [((0, 0, 0.016), 0.052, 0.052), ((0, 0, -0.145), 0.056, 0.056), ((0, 0, -0.195), 0.066, 0.066)], M["upper"], "upper", 18)
        _mk_cyl(fbm, fuv, (0, 0, -0.188), 0.068, 0.066, 0.016, M["gold"], "gold", 18)
        _mk_cyl(fbm, fuv, (0, 0, -0.228), 0.046, 0.036, 0.066, M["coal"], "coal", 16)
        _mk_cyl(fbm, fuv, (0, 0, -0.206), 0.048, 0.048, 0.012, M["gold"], "gold", 16)
        hook_pts = [(0.0, 0.0, -0.258), (0.0, -0.012, -0.304), (0.0, -0.056, -0.358), (0.0, -0.108, -0.364), (0.0, -0.136, -0.320), (0.0, -0.108, -0.278)]
        _mk_tube(fbm, fuv, hook_pts, [0.016, 0.015, 0.014, 0.012, 0.008, 0.003], M["steel"], "steel", 12, cap=True)
    elif idx == 5 and side < 0:
        _mk_sphere(fbm, fuv, (0, 0, 0.008), (0.050, 0.050, 0.050), M["coal"], "coal", 14, 10)
        _mk_loft(fbm, fuv, [((0, 0, -0.010), 0.054, 0.054), ((0, 0, -0.205), 0.048, 0.048)], M["steel"], "steel", 14, smooth=False)
        _mk_box(fbm, fuv, (-0.050, 0, -0.110), (0.016, 0.026, 0.150), M["coal"], "coal", bevel=0.004, segs=1)
        _mk_cyl(fbm, fuv, (0, 0, -0.135), 0.056, 0.056, 0.018, M["glow"], "glow", 16)
        _mk_cyl(fbm, fuv, (0, 0, -0.228), 0.052, 0.046, 0.044, M["coal"], "coal", 16)
        for ca in (0, 120, 240):
            rad_a = math.radians(ca)
            cx, cy = math.cos(rad_a), math.sin(rad_a)
            _mk_tube(fbm, fuv, [
                (cx * 0.030, cy * 0.030, -0.246),
                (cx * 0.060, cy * 0.060 - 0.016, -0.318),
                (cx * 0.020, cy * 0.020 - 0.034, -0.376),
            ], [0.015, 0.012, 0.0045], M["steel"], "steel", 8, cap=True)
    else:
        f_inf = 0.022 if idx == 8 else (0.018 if idx in (0, 3, 4, 7) else 0.011)
        slv_mat = M["skin"] if (idx == 5 and side > 0) else M["upper"]
        slv_uvk = "skin" if (idx == 5 and side > 0) else "upper"
        slv_res = extract_human_subset(
            human_obj, "body", lambda c: True,
            decimate_ratio=0.48, mat_idx=slv_mat, uv_mode=slv_uvk,
            offset_vec=elbow, inflate=0.0 if slv_mat == M["skin"] else f_inf,
            bone_groups=[f"lowerarm_{sfx}"], weight_thresh=0.14
        )
        if slv_res:
            _merge_bm(slv_res[0], fbm, slv_res[1], fuv)
        hand_res = extract_human_subset(
            human_obj, "body", lambda c: True,
            decimate_ratio=0.58, mat_idx=M["skin"], uv_mode="skin",
            offset_vec=elbow, bone_groups=hand_bones, weight_thresh=0.12
        )
        if hand_res:
            _merge_bm(hand_res[0], fbm, hand_res[1], fuv)
        if slv_mat != M["skin"]:
            cuff_z = (hand.z - elbow.z) + 0.026
            _mk_cyl(fbm, fuv, (0, 0, cuff_z), 0.044, 0.048, 0.034, M["gold"] if idx == 0 else M["trim"], "trim", 16)

        hand_rel_z = hand.z - elbow.z - 0.052
        if idx == 1 and side > 0:
            # Sleek Iced Matcha Latte Cup with cream foam top, sage tea body, dome lid & straw!
            cup_xf = Matrix.Translation((0.0, -0.066, hand_rel_z)) @ Euler((math.radians(82), 0, 0)).to_matrix().to_4x4()
            tmp = bmesh.new(); t_uv = tmp.loops.layers.uv.new("UVMap")
            _mk_loft(tmp, t_uv, [((0, 0, -0.094), 0.038, 0.038), ((0, 0, 0.092), 0.054, 0.054)], M["accent"], "accent", 20)
            _mk_loft(tmp, t_uv, [((0, 0, -0.026), 0.048, 0.048), ((0, 0, 0.036), 0.052, 0.052)], M["trim"], "trim", 20, False, False)
            _mk_loft(tmp, t_uv, [((0, 0, 0.068), 0.053, 0.053), ((0, 0, 0.094), 0.055, 0.055)], M["ivory"], "ivory", 20, False, False)
            _mk_sphere(tmp, t_uv, (0, 0, 0.094), (0.056, 0.056, 0.034), M["ivory"], "ivory", 16, 10)
            _mk_tube(tmp, t_uv, [(0, 0, 0.094), (0.012, -0.008, 0.215)], 0.0065, M["coal"], "coal", 8)
            bmesh.ops.transform(tmp, matrix=cup_xf, verts=tmp.verts)
            _merge_bm(tmp, fbm, t_uv, fuv)
        elif idx == 3 and side < 0:
            st_xf = Matrix.Translation((-0.014, -0.048, hand_rel_z)) @ Euler((math.radians(78), 0, 0)).to_matrix().to_4x4()
            tmp = bmesh.new(); t_uv = tmp.loops.layers.uv.new("UVMap")
            _mk_tube(tmp, t_uv, [(0, 0, -0.62), (0, 0, 0.0), (0.018, -0.01, 0.50), (-0.014, 0.012, 0.76)], [0.019, 0.021, 0.023, 0.016], M["wood"], "wood", 10)
            for ps in (-1, 1):
                _mk_tube(tmp, t_uv, [(-0.014, 0.012, 0.75), (ps * 0.060, 0.012, 0.84), (ps * 0.038, 0.012, 0.92)], 0.009, M["gold"], "gold", 8)
            _mk_sphere(tmp, t_uv, (-0.010, 0.012, 0.875), (0.052, 0.052, 0.052), M["glow"], "glow", 14, 10)
            bmesh.ops.transform(tmp, matrix=st_xf, verts=tmp.verts)
            _merge_bm(tmp, fbm, t_uv, fuv)

    return ubm, fbm


# =============================================================================
# LEGS & BESPOKE FOOTWEAR (Seamless weight_thresh=0.14 + Refined Shoes!)
# =============================================================================
def build_leg_parts(idx, r, human_obj, joints, M, side):
    hip = joints["hip_l"] if side > 0 else joints["hip_r"]
    knee = joints["knee_l"] if side > 0 else joints["knee_r"]
    thigh_len = max(0.32, hip.z - knee.z)
    shin_len = max(0.36, knee.z)
    scale_t = Vector((1.0, 1.0, 0.420 / thigh_len))
    scale_s = Vector((1.0, 1.0, 0.480 / shin_len))
    sfx = "l" if side > 0 else "r"

    tbm = bmesh.new(); tuv = tbm.loops.layers.uv.new("UVMap")
    t_inf = 0.022 if idx in (3, 6, 8) else 0.015
    t_res = extract_human_subset(
        human_obj, "body", lambda c: True,
        decimate_ratio=0.45, mat_idx=M["lower"], uv_mode="lower",
        offset_vec=hip, scale_vec=scale_t, inflate=t_inf,
        bone_groups=[f"thigh_{sfx}"], weight_thresh=0.14
    )
    if t_res:
        _merge_bm(t_res[0], tbm, t_res[1], tuv)

    sbm = bmesh.new(); suv = sbm.loops.layers.uv.new("UVMap")
    if idx == 0 and side < 0:
        _mk_loft(sbm, suv, [((0, 0, 0.020), 0.072, 0.074), ((0, 0, -0.110), 0.068, 0.070), ((0, 0, -0.145), 0.074, 0.076)], M["lower"], "lower", 18)
        _mk_cyl(sbm, suv, (0, 0, -0.165), 0.054, 0.046, 0.046, M["coal"], "coal", 16)
        _mk_cyl(sbm, suv, (0, 0, -0.145), 0.076, 0.076, 0.015, M["gold"], "gold", 16)
        _mk_loft(sbm, suv, [((0, 0, -0.175), 0.042, 0.042), ((0, 0, -0.255), 0.036, 0.036), ((0, 0, -0.355), 0.026, 0.026), ((0, 0, -0.448), 0.020, 0.020)], M["wood"], "wood", 16)
        _mk_cyl(sbm, suv, (0, 0, -0.255), 0.040, 0.040, 0.015, M["gold"], "gold", 16)
        _mk_cyl(sbm, suv, (0, 0, -0.464), 0.024, 0.022, 0.032, M["steel"], "steel", 14)
        return tbm, sbm

    calf_res = extract_human_subset(
        human_obj, "body", lambda c: c.z >= 0.095,
        decimate_ratio=0.45, mat_idx=M["lower"], uv_mode="lower",
        offset_vec=knee, scale_vec=scale_s, inflate=0.015 if idx != 8 else 0.026,
        bone_groups=[f"calf_{sfx}"], weight_thresh=0.14
    )
    if calf_res:
        _merge_bm(calf_res[0], sbm, calf_res[1], suv)

    if idx == 0:
        _mk_loft(sbm, suv, [((0, 0, -0.160), 0.076, 0.080), ((0, 0, -0.215), 0.064, 0.068), ((0, 0, -0.395), 0.056, 0.060)], M["shoe"], "shoe", 18)
        _mk_box(sbm, suv, (0, -0.054, -0.445), (0.106, 0.254, 0.066), M["shoe"], "shoe", bevel=0.018, segs=2)
        _mk_box(sbm, suv, (0, -0.054, -0.472), (0.110, 0.260, 0.016), M["coal"], "coal", bevel=0.004, segs=1)
        _mk_box(sbm, suv, (0, -0.094, -0.405), (0.042, 0.016, 0.032), M["gold"], "gold", bevel=0.004, segs=1)
    elif idx == 4:
        _mk_box(sbm, suv, (0, -0.050, -0.442), (0.118, 0.260, 0.072), M["shoe"], "shoe", bevel=0.026, segs=3)
        _mk_box(sbm, suv, (0, -0.050, -0.474), (0.120, 0.262, 0.012), M["coal"], "coal", bevel=0.004, segs=1)
        for es in (-1, 1):
            _mk_sphere(sbm, suv, (es * 0.034, -0.120, -0.386), (0.017, 0.020, 0.048), M["shoe"], "shoe", 10, 8, Euler((math.radians(-22), es * math.radians(14), 0)))
            _mk_sphere(sbm, suv, (es * 0.034, -0.127, -0.388), (0.009, 0.011, 0.030), M["accent"], "accent", 8, 6, Euler((math.radians(-22), es * math.radians(14), 0)))
    elif idx == 8:
        _mk_box(sbm, suv, (0, -0.066, -0.468), (0.148, 0.335, 0.022), M["coal"], "coal", bevel=0.008, segs=2)
        _mk_box(sbm, suv, (0, -0.064, -0.442), (0.154, 0.342, 0.030), M["accent"], "accent", bevel=0.010, segs=2)
        _mk_box(sbm, suv, (0, -0.058, -0.415), (0.144, 0.318, 0.030), M["ivory"], "ivory", bevel=0.012, segs=2)
        _mk_box(sbm, suv, (0, -0.040, -0.382), (0.128, 0.248, 0.058), M["shoe"], "shoe", bevel=0.020, segs=2)
    elif idx in (5, 11):
        _mk_loft(sbm, suv, [((0, 0, -0.240), 0.062, 0.066), ((0, 0, -0.395), 0.056, 0.062)], M["shoe"], "shoe", 16)
        _mk_box(sbm, suv, (0, -0.052, -0.440), (0.112, 0.264, 0.070), M["shoe"], "shoe", bevel=0.016, segs=2)
        _mk_box(sbm, suv, (0, -0.052, -0.470), (0.116, 0.270, 0.020), M["coal"], "coal", bevel=0.005, segs=1)
        for lk in range(3):
            _mk_box(sbm, suv, (0, -0.066, -0.275 - lk * 0.038), (0.044, 0.009, 0.007), M["steel"], "steel", bevel=0.002, segs=1)
    else:
        _mk_box(sbm, suv, (0, -0.052, -0.472), (0.104, 0.250, 0.016), M["coal"], "coal", bevel=0.005, segs=1)
        _mk_box(sbm, suv, (0, -0.052, -0.450), (0.108, 0.256, 0.028), M["ivory"], "ivory", bevel=0.010, segs=2)
        _mk_box(sbm, suv, (0, -0.036, -0.418), (0.100, 0.202, 0.052), M["shoe"], "shoe", bevel=0.018, segs=2)
        _mk_box(sbm, suv, (0, -0.036, -0.422), (0.104, 0.070, 0.014), M["trim"], "trim", bevel=0.004, segs=1)

    return tbm, sbm


# =============================================================================
# BAKE 12-RAY BVHTREE AO (With Luminous Eye & Glow Protection!) & EXPORT GLB
# =============================================================================
def finalize_part_obj(name, bm, mats_list, eye_mat_idx, glow_mat_idx, bright_extra=-1):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    for m in mats_list:
        mesh.materials.append(m)

    bm_ao = bmesh.new()
    bm_ao.from_mesh(mesh)
    bm_ao.verts.ensure_lookup_table()
    bvh = BVHTree.FromBMesh(bm_ao)

    n_rays = 12
    phi = math.pi * (3.0 - math.sqrt(5.0))
    dirs = []
    for i in range(n_rays):
        y = 1.0 - (i / float(n_rays - 1)) * 0.85
        rad = math.sqrt(max(0.0, 1.0 - y * y))
        th = phi * i
        dirs.append(Vector((math.cos(th) * rad, y, math.sin(th) * rad)).normalized())

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
            hit = bvh.ray_cast(orig, ray, 0.18)
            if hit[0] is not None:
                occ += 1.0 - (hit[3] / 0.18)
        vert_ao.append(max(0.76, min(1.0, 1.0 - (occ / n_rays) * 0.30)))
    bm_ao.free()

    col_attr = mesh.color_attributes.new(name="COLOR_0", type='BYTE_COLOR', domain='CORNER')
    for poly in mesh.polygons:
        is_bright = poly.material_index in (eye_mat_idx, glow_mat_idx, bright_extra)
        for li in poly.loop_indices:
            ao = max(0.96, vert_ao[mesh.loops[li].vertex_index]) if is_bright else vert_ao[mesh.loops[li].vertex_index]
            col_attr.data[li].color = (ao, ao, ao, 1.0)
    return obj


def build_character_glb(idx):
    r = RECIPES[idx]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    atlas = build_character_atlas(idx, r)
    mats = build_materials(atlas, r)
    mat_keys = list(mats.keys())
    mats_list = [mats[k] for k in mat_keys]
    M = {k: i for i, k in enumerate(mat_keys)}

    human_obj, joints = create_posed_mpfb_human(r)

    torso_bm = build_torso_part(idx, r, human_obj, joints, M)
    head_bm  = build_head_part(idx, r, human_obj, joints, M)
    uarm_l, farm_l = build_arm_parts(idx, r, human_obj, joints, M, +1)
    uarm_r, farm_r = build_arm_parts(idx, r, human_obj, joints, M, -1)
    thg_l,  shn_l  = build_leg_parts(idx, r, human_obj, joints, M, +1)
    thg_r,  shn_r  = build_leg_parts(idx, r, human_obj, joints, M, -1)

    bpy.data.objects.remove(human_obj, do_unlink=True)

    parts_bms = [
        ("Torso", torso_bm), ("Head", head_bm),
        ("UpperArmL", uarm_l), ("ForearmL", farm_l),
        ("UpperArmR", uarm_r), ("ForearmR", farm_r),
        ("ThighL", thg_l), ("ShinL", shn_l),
        ("ThighR", thg_r), ("ShinR", shn_r),
    ]
    objs = [finalize_part_obj(name, bm, mats_list, M["eye"], M["glow"], M["face"] if idx == 10 else -1) for name, bm in parts_bms]

    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]

    out_path = os.path.join(MODELS_DIR, f"vc_{idx}.glb")
    bpy.ops.export_scene.gltf(
        filepath=out_path,
        export_format='GLB',
        use_selection=True,
        export_yup=True,
        export_apply=True,
        export_attributes=True,
        export_image_format='JPEG',
        export_image_quality=92,
    )
    total_polys = sum(len(o.data.polygons) for o in objs)
    print(f"EXPORTED vc_{idx}.glb ({r['name']}) — {total_polys} polys, 10 parts, packed 1024x1024 atlas!")


def main():
    only = [int(a) for a in sys.argv[sys.argv.index("--") + 1:]] if "--" in sys.argv else list(range(12))
    for idx in only:
        build_character_glb(idx)
    print("ALL STEAM-GRADE MPFB2 VIBE CODERS v3.5 BUILT SUCCESSFULLY!")


if __name__ == "__main__":
    main()
