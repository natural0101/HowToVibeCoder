"""Steam-Grade Stylized 3D Vibe Coder Character Pipeline v3 (Blender 5.1 + MPFB2 Real Human Anatomy).

Builds 12 completely distinct, fully-textured, anatomically sculpted articulated characters:
  assets/models/vc_0.glb .. assets/models/vc_11.glb

Each GLB contains 10 cleanly-named articulated parts centered at exact canonical joint pivots,
sharing ONE packed 1024x1024 painted PBR texture atlas (face complexion, radial irises with
catchlights, hair strands, woven fabrics/leather/knits/graphics, woodgrain & brushed steel)
PLUS baked 16-ray BVHTree Ambient Occlusion in COLOR_0:
  0. Torso     (pelvis pivot 0,0,0 -> world Z=0.90)
  1. Head      (neck   pivot 0,0,0 -> torso +0.54)
  2. UpperArmL (shld L pivot 0,0,0 -> torso +0.180,+0.440)
  3. ForearmL  (elbowL pivot 0,0,0 -> upper -0.260)
  4. UpperArmR (shld R pivot 0,0,0 -> torso -0.180,+0.440)
  5. ForearmR  (elbowR pivot 0,0,0 -> upper -0.260)
  6. ThighL    (hip  L pivot 0,0,0 -> pelvis +0.098,0)
  7. ShinL     (knee L pivot 0,0,0 -> thigh -0.420)
  8. ThighR    (hip  R pivot 0,0,0 -> pelvis -0.098,0)
  9. ShinR     (knee R pivot 0,0,0 -> thigh -0.420)

Coordinate convention: Blender Z-up, front faces -Y -> export_yup=True -> glTF +Z forward, +Y up.
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
PROOF_PATH = r"C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a\vibe_coders_v3_proof.png"

PART_NAMES = [
    "Torso", "Head",
    "UpperArmL", "ForearmL",
    "UpperArmR", "ForearmR",
    "ThighL", "ShinL",
    "ThighR", "ShinR",
]

# Canonical joint pivots (Blender Z-up, feet strictly at Z=0.0, total height ~1.74m)
PELVIS_Z = 0.90
NECK_Z   = 1.44
SHLD_X   = 0.180
SHLD_Z   = 1.340
ELBOW_Z  = 1.080
HIP_X    = 0.098
KNEE_Z   = 0.480

# 12 distinct character recipes: morphs, skin tone, eye color, hair color, outfit palette & style
RECIPES = [
    # 0: VIBE-PIRATE ("tab"/"stack") — Navy Captain Coat, crimson sash, tricorne, eyepatch, STEEL HOOK, OAK PEG-LEG!
    dict(name="pirate", macro=(0.85, 0.55, 0.58, 0.42, 0.52),
         skin=(0.86, 0.65, 0.52), blush=(0.76, 0.34, 0.28), hair=(0.14, 0.09, 0.07),
         iris=(0.18, 0.52, 0.72), upper=(0.11, 0.19, 0.32), trim=(0.88, 0.68, 0.22),
         accent=(0.76, 0.14, 0.18), lower=(0.17, 0.15, 0.14), shoe=(0.24, 0.14, 0.09),
         pattern="pirate_coat", face_fx="pirate", glow=(0.25, 0.92, 0.85)),
    # 1: SMOOTHIE / CAFFEINE MANIAC ("div"/"smoothie") — Lilac graphic hoodie + denim barista apron + iced cup!
    dict(name="smoothie", macro=(0.72, 0.22, 0.32, 0.34, 0.48),
         skin=(0.94, 0.78, 0.69), blush=(0.85, 0.46, 0.44), hair=(0.22, 0.14, 0.10),
         iris=(0.28, 0.68, 0.35), upper=(0.62, 0.47, 0.82), trim=(0.18, 0.24, 0.34),
         accent=(0.45, 0.88, 0.36), lower=(0.16, 0.20, 0.28), shoe=(0.92, 0.90, 0.86),
         pattern="barista_hoodie", face_fx="caffeine", glow=(0.65, 0.96, 0.35)),
    # 2: TINFOIL CRYPTO-BRO ("weekend"/"leverage") — Retro gold-teal windbreaker vest + Cuban chain + tinfoil cone!
    dict(name="tinfoil", macro=(0.80, 0.32, 0.48, 0.46, 0.50),
         skin=(0.90, 0.71, 0.58), blush=(0.82, 0.40, 0.34), hair=(0.30, 0.20, 0.11),
         iris=(0.20, 0.64, 0.78), upper=(0.85, 0.58, 0.14), trim=(0.12, 0.14, 0.20),
         accent=(0.16, 0.78, 0.86), lower=(0.14, 0.16, 0.22), shoe=(0.88, 0.68, 0.22),
         pattern="windbreaker", face_fx="manic", glow=(0.25, 0.90, 0.98)),
    # 3: 40-FRAMEWORK WIZARD ("prompt"/"framework") — Starry indigo velvet wizard robe + beard + oak crystal staff!
    dict(name="wizard", macro=(0.88, 0.78, 0.40, 0.58, 0.50),
         skin=(0.89, 0.70, 0.58), blush=(0.82, 0.42, 0.38), hair=(0.68, 0.70, 0.74),
         iris=(0.24, 0.68, 0.96), upper=(0.14, 0.22, 0.52), trim=(0.92, 0.74, 0.26),
         accent=(0.35, 0.86, 0.98), lower=(0.10, 0.15, 0.38), shoe=(0.28, 0.18, 0.12),
         pattern="wizard_stars", face_fx="elder", glow=(0.45, 0.88, 1.0)),
    # 4: SLEEPY TEAMLEAD ("call"/"deadline") — Plush teal bathrobe + striped pajamas + sleep mask + neck pillow + slippers!
    dict(name="sleepy", macro=(0.76, 0.48, 0.38, 0.56, 0.48),
         skin=(0.92, 0.76, 0.67), blush=(0.82, 0.48, 0.46), hair=(0.24, 0.18, 0.14),
         iris=(0.38, 0.44, 0.52), upper=(0.22, 0.56, 0.54), trim=(0.92, 0.88, 0.80),
         accent=(0.94, 0.56, 0.68), lower=(0.82, 0.86, 0.92), shoe=(0.94, 0.86, 0.82),
         pattern="bathrobe", face_fx="sleepy", glow=(0.96, 0.56, 0.72)),
    # 5: PUNK CYBORG REFACTORER ("refactor") — Studded black leather biker vest + crimson tank + mohawk + CYBER-CLAW!
    dict(name="cyborg", macro=(0.92, 0.42, 0.72, 0.46, 0.54),
         skin=(0.87, 0.68, 0.56), blush=(0.78, 0.36, 0.32), hair=(0.12, 0.10, 0.12),
         iris=(0.92, 0.46, 0.14), upper=(0.13, 0.13, 0.16), trim=(0.76, 0.16, 0.22),
         accent=(0.98, 0.64, 0.16), lower=(0.18, 0.18, 0.22), shoe=(0.11, 0.11, 0.13),
         pattern="punk_leather", face_fx="punk", glow=(1.0, 0.42, 0.12)),
    # 6: ZEN MONK ("no-commits") — Layered saffron/charcoal monk robe + prayer beads + closed eyes + GOLDEN HALO!
    dict(name="zen", macro=(0.82, 0.82, 0.42, 0.45, 0.50),
         skin=(0.86, 0.68, 0.55), blush=(0.78, 0.42, 0.35), hair=(0.86, 0.87, 0.90),
         iris=(0.40, 0.35, 0.28), upper=(0.82, 0.46, 0.14), trim=(0.22, 0.20, 0.24),
         accent=(0.98, 0.82, 0.28), lower=(0.20, 0.19, 0.23), shoe=(0.42, 0.28, 0.16),
         pattern="monk_robe", face_fx="zen", glow=(1.0, 0.84, 0.30)),
    # 7: SKOLKOVO VR SHAMAN ("skolkovo") — Techwear parka + neon piping + cyberpunk VR headset + SERVER JETPACK!
    dict(name="skolkovo", macro=(0.78, 0.38, 0.52, 0.44, 0.52),
         skin=(0.90, 0.73, 0.62), blush=(0.80, 0.44, 0.42), hair=(0.14, 0.12, 0.18),
         iris=(0.85, 0.28, 0.96), upper=(0.26, 0.17, 0.46), trim=(0.12, 0.13, 0.17),
         accent=(0.96, 0.28, 0.82), lower=(0.14, 0.14, 0.19), shoe=(0.16, 0.16, 0.22),
         pattern="techwear", face_fx="vr", glow=(0.96, 0.28, 0.88)),
    # 8: MEETUP CROWN BOSS ("meetup") — Quilted cream puffer jacket + GOLDEN CROWN + baggy cargos + GIANT TRIPLE-S!
    dict(name="meetup", macro=(0.75, 0.30, 0.46, 0.48, 0.52),
         skin=(0.88, 0.69, 0.56), blush=(0.82, 0.44, 0.38), hair=(0.16, 0.12, 0.10),
         iris=(0.32, 0.24, 0.18), upper=(0.90, 0.87, 0.80), trim=(0.16, 0.16, 0.18),
         accent=(0.88, 0.16, 0.28), lower=(0.24, 0.24, 0.26), shoe=(0.94, 0.92, 0.88),
         pattern="puffer", face_fx="smug", glow=(0.95, 0.22, 0.34)),
    # 9: FRIDAY 19:59 PROD DEMON ("friday") — Tailored crimson-black pinstripe suit + obsidian HORNS + DEVIL TAIL!
    dict(name="friday", macro=(0.88, 0.44, 0.62, 0.48, 0.54),
         skin=(0.86, 0.60, 0.52), blush=(0.78, 0.24, 0.22), hair=(0.10, 0.08, 0.10),
         iris=(1.00, 0.72, 0.12), upper=(0.56, 0.11, 0.14), trim=(0.12, 0.10, 0.12),
         accent=(1.00, 0.76, 0.18), lower=(0.13, 0.11, 0.14), shoe=(0.10, 0.09, 0.11),
         pattern="devil_suit", face_fx="demon", glow=(1.00, 0.68, 0.14)),
    # 10: LEGACY GHOST 1998 ("legacy") — 1998 argyle sysadmin cardigan + tie + pocket protector + CRT MONITOR HEAD!
    dict(name="legacy", macro=(0.80, 0.65, 0.40, 0.54, 0.48),
         skin=(0.90, 0.74, 0.64), blush=(0.80, 0.46, 0.42), hair=(0.35, 0.34, 0.32),
         iris=(0.35, 0.96, 0.52), upper=(0.26, 0.42, 0.30), trim=(0.88, 0.84, 0.72),
         accent=(0.74, 0.24, 0.18), lower=(0.56, 0.49, 0.38), shoe=(0.28, 0.20, 0.14),
         pattern="argyle_98", face_fx="crt", glow=(0.35, 0.98, 0.55)),
    # 11: TOWER SECURITY CHIEF ("sb_chief") — Broad Kevlar tactical vest + badge + officer cap + chrome aviators!
    dict(name="security", macro=(0.96, 0.60, 0.82, 0.62, 0.56),
         skin=(0.86, 0.66, 0.55), blush=(0.76, 0.38, 0.34), hair=(0.18, 0.17, 0.18),
         iris=(0.22, 0.32, 0.42), upper=(0.14, 0.17, 0.22), trim=(0.26, 0.30, 0.36),
         accent=(0.90, 0.72, 0.24), lower=(0.13, 0.15, 0.19), shoe=(0.09, 0.09, 0.11),
         pattern="kevlar", face_fx="stern", glow=(0.38, 0.78, 0.96)),
]


def srgb_to_lin(c):
    return tuple(c_i / 12.92 if c_i <= 0.04045 else ((c_i + 0.055) / 1.055) ** 2.4 for c_i in c)


# =============================================================================
# 1024x1024 PAINTED PBR TEXTURE ATLAS GENERATOR (Unique per archetype!)
#   Q0 (x:0..512,   y:0..512):   Face Complexion + Facial Hair/Scars/Brows/Makeup
#   Q1 (x:512..1024,y:0..512):   Left: Radial Eye Iris + Catchlights | Right: Hair Strands
#   Q2 (x:0..512,   y:512..1024):Upper Outfit Fabric/Leather/Argyle/Stars/Kevlar/Graphics
#   Q3 (x:512..1024,y:512..1024):Lower Garment Twill/Pajamas + Woodgrain + Brushed Steel
# =============================================================================
def build_character_atlas(idx, r):
    W, H = 1024, 1024
    arr = np.ones((H, W, 4), dtype=np.float32)
    rng = np.random.default_rng(1000 + idx * 97)

    # ---------------- Q0: Sculpted Stylized Face Map (x:0..512, y:0..512) ----------------
    # Face front-projected UVs map x in [-0.11..+0.11] -> u in [0.05..0.45], z in [1.41..1.67] -> v in [0.05..0.45]
    # Center of face: (u=0.25, v=0.25) -> pixel (256, 256). Eyes around v=0.295 (y=302), |u-0.25|=0.068 (dx=70).
    # Nose tip around (256, 252), mouth around (256, 200).
    yy, xx = np.mgrid[0:512, 0:512].astype(np.float32)
    uf = (xx - 256.0) / 180.0   # -1..+1 across face width
    vf = (yy - 256.0) / 180.0   # -1..+1 bottom-to-top (v=0 at bottom in Blender images!)
    # Note: in Blender image pixels, row 0 is BOTTOM (v=0)!
    skin = np.array(r["skin"], dtype=np.float32)
    blush_col = np.array(r["blush"], dtype=np.float32)
    hair_col = np.array(r["hair"], dtype=np.float32)
    q0 = np.empty((512, 512, 3), dtype=np.float32)
    q0[:, :, :] = skin
    # Subtle skin pore warmth + forehead/cheekbone specular warmth
    micro = rng.uniform(-0.015, 0.015, (512, 512, 1)).astype(np.float32)
    q0 += micro
    # Cheek blush & nose warmth
    cheek = np.exp(-(((np.abs(uf) - 0.36) / 0.24) ** 2 + ((vf + 0.02) / 0.18) ** 2))
    nose_tip = 0.45 * np.exp(-((uf / 0.12) ** 2 + ((vf + 0.05) / 0.09) ** 2))
    blush_m = np.clip(cheek * 0.42 + nose_tip * 0.30, 0.0, 0.65)[:, :, None]
    q0 = q0 * (1.0 - blush_m) + blush_col * blush_m
    # Sculpted nose-bridge sides, under-nose, eye-socket occlusion & jaw contour!
    nose_sides = 0.22 * np.exp(-(((np.abs(uf) - 0.11) / 0.05) ** 2 + ((vf - 0.08) / 0.18) ** 2))
    nose_under = 0.28 * np.exp(-((uf / 0.11) ** 2 + ((vf + 0.12) / 0.055) ** 2))
    eye_socket = 0.24 * np.exp(-(((np.abs(uf) - 0.32) / 0.15) ** 2 + ((vf - 0.21) / 0.11) ** 2))
    jaw_shade = 0.18 * np.clip((-vf - 0.36) * 2.2, 0.0, 1.0)
    shade = np.clip(1.0 - nose_sides - nose_under - eye_socket - jaw_shade, 0.62, 1.0)[:, :, None]
    q0 *= shade
    # Nose bridge & forehead highlight
    hi = 0.08 * np.exp(-((uf / 0.055) ** 2 + ((vf - 0.06) / 0.17) ** 2)) + 0.06 * np.exp(-((uf / 0.34) ** 2 + ((vf - 0.46) / 0.16) ** 2))
    q0 = np.clip(q0 + hi[:, :, None], 0.0, 1.0)
    # Natural painted lips around vf ~ -0.26
    lip_m = np.exp(-((uf / 0.19) ** 2 + ((vf + 0.26) / 0.065) ** 2))
    lip_c = skin * 0.72 + np.array([0.24, 0.06, 0.06], dtype=np.float32)
    q0 = q0 * (1.0 - 0.68 * lip_m[:, :, None]) + lip_c * (0.68 * lip_m[:, :, None])

    # Character-specific facial paint (stubble, scars, under-eye bags, brows, bandages!)
    fx = r["face_fx"]
    if fx in ("pirate", "stern", "punk", "sleepy", "manic"):
        # 5-o'clock beard stubble on lower jaw/chin!
        stub = np.clip((0.12 - (vf + 0.34)) * 3.0, 0.0, 1.0) * np.exp(-((uf / 0.52) ** 2))
        stub_n = rng.uniform(0.65, 1.0, (512, 512)).astype(np.float32)
        strength = 0.38 if fx in ("pirate", "stern") else 0.22
        m = np.clip(stub * stub_n * strength, 0.0, 0.45)[:, :, None]
        q0 = q0 * (1.0 - m) + hair_col * m
    if fx == "pirate":
        # Diagonal weathered pirate scar across right cheek!
        d_scar = np.abs((uf + 0.34) * 0.78 + (vf - 0.06) * 0.62)
        scar_m = np.exp(-((d_scar / 0.018) ** 2)) * (np.abs(vf - 0.06) < 0.22)
        q0 = q0 * (1.0 - 0.75 * scar_m[:, :, None]) + np.array([0.52, 0.20, 0.18]) * (0.75 * scar_m[:, :, None])
    elif fx in ("caffeine", "sleepy"):
        # Deep purple-slate sleepless under-eye bags!
        bag = np.exp(-(((np.abs(uf) - 0.31) / 0.15) ** 2 + ((vf - 0.10) / 0.065) ** 2))
        bag_c = np.array([0.36, 0.28, 0.48], dtype=np.float32)
        amt = 0.65 if fx == "caffeine" else 0.50
        q0 = q0 * (1.0 - amt * bag[:, :, None]) + bag_c * (amt * bag[:, :, None])
    elif fx == "demon":
        # Dark fiery eye-socket contour & ember blush
        emb = np.exp(-(((np.abs(uf) - 0.31) / 0.18) ** 2 + ((vf - 0.20) / 0.12) ** 2))
        q0 = q0 * (1.0 - 0.62 * emb[:, :, None]) + np.array([0.24, 0.06, 0.07]) * (0.62 * emb[:, :, None])
    elif fx == "vr":
        # Subtle glowing cyan-magenta bounce from VR visor on cheeks & nose
        vr_g = np.exp(-((uf / 0.45) ** 2 + ((vf - 0.02) / 0.20) ** 2))
        q0 = np.clip(q0 + np.array(r["glow"]) * 0.22 * vr_g[:, :, None], 0.0, 1.0)
    elif fx == "crt":
        # Retro CRT green-phosphor terminal screen painted right into Q0!
        q0[:, :, :] = np.array([0.04, 0.14, 0.08], dtype=np.float32)
        # Scanlines + CRT vignette
        scan = 0.85 + 0.15 * np.sin(yy * 0.55)
        vig = np.clip(1.0 - 0.42 * (uf * uf + vf * vf), 0.35, 1.0)
        q0 *= (scan * vig)[:, :, None]
        gcol = np.array([0.32, 0.98, 0.54], dtype=np.float32)
        # Glowing terminal eyes [  ]  [  ] + cute smile + prompt `> 98`
        eye_l = (np.abs(uf + 0.32) < 0.10) & (np.abs(vf - 0.16) < 0.13)
        eye_r = (np.abs(uf - 0.32) < 0.10) & (np.abs(vf - 0.16) < 0.13)
        smile = (np.abs(uf) < 0.28) & (np.abs(vf + 0.24 - 0.25 * uf * uf) < 0.035)
        border = (np.abs(uf) > 0.88) | (np.abs(vf) > 0.82)
        q0[eye_l | eye_r | smile] = gcol
        q0[border] = gcol * 0.38

    # Crisp painted eyebrows on Q0 (for human faces)
    if fx != "crt":
        slope = -0.22 if fx in ("pirate", "punk", "demon", "stern") else (0.14 if fx in ("caffeine", "manic", "sleepy") else -0.05)
        b_y = 0.34 + slope * (np.abs(uf) - 0.31)
        brow = np.exp(-(((np.abs(uf) - 0.31) / 0.17) ** 2 + ((vf - b_y) / 0.038) ** 2))
        q0 = q0 * (1.0 - 0.78 * brow[:, :, None]) + hair_col * (0.78 * brow[:, :, None])
        if fx == "zen":
            # Peaceful closed curved eyelash lines painted cleanly over eyelids!
            lash = np.exp(-(((np.abs(uf) - 0.31) / 0.14) ** 2 + ((vf - 0.19 + 0.18 * (np.abs(uf) - 0.31) ** 2) / 0.022) ** 2))
            q0 = q0 * (1.0 - 0.85 * lash[:, :, None]) + np.array([0.12, 0.10, 0.10]) * (0.85 * lash[:, :, None])

    arr[0:512, 0:512, :3] = np.clip(q0, 0.0, 1.0)

    # ---------------- Q1: Eye Iris (x:512..768, y:0..512) & Hair Strands (x:768..1024, y:0..512) ----------------
    # Eye center at (640, 256)
    ey, ex = np.mgrid[0:512, 0:256].astype(np.float32)
    eu = (ex - 128.0) / 110.0
    ev = (ey - 256.0) / 110.0
    rad = np.sqrt(eu * eu + ev * ev)
    ang = np.arctan2(ev, eu)
    eye_img = np.ones((512, 256, 3), dtype=np.float32) * np.array([0.96, 0.95, 0.94], dtype=np.float32)
    if fx == "caffeine":
        # Subtle red caffeine bloodshot veins in sclera!
        veins = (rad > 0.42) & (np.abs(np.sin(ang * 9.0 + rad * 12.0)) > 0.93)
        eye_img[veins] = np.array([0.92, 0.42, 0.42])
    elif fx == "demon":
        eye_img[:, :, :] = np.array([0.16, 0.04, 0.04])  # dark demonic sclera!
    iris_c = np.array(r["iris"], dtype=np.float32)
    iris_rad = 0.36 if fx == "caffeine" else 0.44
    pup_rad = 0.13 if fx == "caffeine" else 0.21
    fibers = 0.82 + 0.18 * np.sin(ang * 28.0) * np.cos(rad * 18.0)
    in_iris = rad < iris_rad
    limbal = (rad >= iris_rad - 0.06) & (rad < iris_rad + 0.02)
    eye_img[in_iris] = np.clip(iris_c * fibers[in_iris, None], 0.0, 1.0)
    eye_img[limbal] = iris_c * 0.32
    eye_img[rad < pup_rad] = np.array([0.03, 0.03, 0.04])
    # Sparkling crisp dual white cornea catchlights!
    c1 = ((eu + 0.13) ** 2 + (ev - 0.14) ** 2) < 0.0085
    c2 = ((eu - 0.14) ** 2 + (ev + 0.12) ** 2) < 0.0025
    eye_img[c1 | c2] = np.array([1.0, 1.0, 1.0])
    arr[0:512, 512:768, :3] = eye_img

    # Hair directional strand texture (x:768..1024, y:0..512)
    hy, hx = np.mgrid[0:512, 0:256].astype(np.float32)
    strand = 0.86 + 0.14 * np.sin(hx * 0.65 + 2.2 * np.sin(hy * 0.035)) + 0.07 * np.cos(hx * 1.4)
    sheen = 0.12 * np.exp(-(((hy - 310.0) / 75.0) ** 2))
    h_img = np.clip(hair_col[None, None, :] * strand[:, :, None] + sheen[:, :, None], 0.0, 1.0)
    arr[0:512, 768:1024, :3] = h_img

    # ---------------- Q2: Custom Upper Outfit Fabric/Leather/Armor Texture (x:0..512, y:512..1024) ----------------
    uy, ux = np.mgrid[0:512, 0:512].astype(np.float32)
    u_col = np.array(r["upper"], dtype=np.float32)
    t_col = np.array(r["trim"], dtype=np.float32)
    a_col = np.array(r["accent"], dtype=np.float32)
    q2 = np.empty((512, 512, 3), dtype=np.float32)
    q2[:, :, :] = u_col
    weave = (0.94 + 0.06 * np.sin(ux * 1.1) * np.cos(uy * 1.1) + rng.uniform(-0.025, 0.025, (512, 512))).astype(np.float32)
    q2 *= weave[:, :, None]

    pat = r["pattern"]
    if pat == "pirate_coat":
        # Rich wool navy coat + gold braided hem stripes + vertical lapel seams!
        hem = (uy < 48) | ((uy > 56) & (uy < 70))
        q2[hem] = t_col * (0.92 + 0.08 * np.sin(ux[hem] * 0.8))[:, None]
        seam = (np.abs(ux - 256) < 6) | (np.abs(ux - 110) < 3) | (np.abs(ux - 402) < 3)
        q2[seam] *= 0.65
    elif pat == "barista_hoodie":
        # French terry hoodie knit + bold streetwear horizontal chest stripe & coffee stain!
        stripe = (uy > 250) & (uy < 315)
        q2[stripe] = q2[stripe] * 0.55 + a_col * 0.45
        # Subtle coffee ring stain on chest!
        cr = np.sqrt((ux - 190) ** 2 + (uy - 195) ** 2)
        stain = np.exp(-(((cr - 28.0) / 5.5) ** 2))
        q2 = q2 * (1.0 - 0.45 * stain[:, :, None]) + np.array([0.36, 0.20, 0.10]) * (0.45 * stain[:, :, None])
    elif pat == "windbreaker":
        # 90s/2026 colorblock diagonal chevron windbreaker!
        diag = (uy + 0.6 * np.abs(ux - 256))
        q2[(diag > 290) & (diag < 355)] = a_col * weave[(diag > 290) & (diag < 355), None]
        q2[diag >= 355] = t_col * weave[diag >= 355, None]
    elif pat == "wizard_stars":
        # Velvet indigo robe + twinkling golden constellation stars & arcane border!
        border = (uy < 44) | ((uy > 52) & (uy < 62))
        q2[border] = t_col
        for sx, sy, sr in [(80, 180, 14), (210, 310, 18), (360, 220, 15), (430, 390, 13), (140, 420, 16), (295, 130, 12), (255, 450, 17)]:
            dx, dy = np.abs(ux - sx), np.abs(uy - sy)
            star = ((dx < sr) & (dy < sr * 0.25)) | ((dy < sr) & (dx < sr * 0.25)) | ((dx + dy) < sr * 0.55)
            q2[star] = t_col
    elif pat == "bathrobe":
        # Plush waffle-weave terrycloth texture + soft contrasting collar piping!
        waffle = 0.88 + 0.12 * np.sin(ux * 0.55) * np.sin(uy * 0.55)
        q2 = np.clip(u_col[None, None, :] * waffle[:, :, None], 0.0, 1.0)
        q2[uy < 32] = t_col
    elif pat == "punk_leather":
        # Distressed pebble-grain black leather + metallic studs along shoulders/hem + stitched cross seams!
        pebble = 0.85 + 0.15 * np.sin(ux * 0.9 + rng.uniform(-0.3, 0.3, (512, 512))) * np.cos(uy * 0.9)
        q2 = np.clip(u_col[None, None, :] * pebble[:, :, None], 0.0, 1.0)
        for sx in range(36, 500, 48):
            stud = ((ux - sx) ** 2 + (uy - 440) ** 2) < 42
            q2[stud] = np.array([0.84, 0.86, 0.90])
        patch = (ux > 165) & (ux < 345) & (uy > 160) & (uy < 330)
        q2[patch] = t_col * (0.88 + 0.12 * weave[patch, None])
    elif pat == "monk_robe":
        # Layered diagonal saffron/charcoal woven linen drape!
        diag = (uy + 0.45 * ux) % 240
        q2[diag < 80] = t_col * weave[diag < 80, None]
        q2[(diag >= 76) & (diag < 86)] = a_col
    elif pat == "techwear":
        # Ripstop hex-grid nylon + crisp glowing neon circuit piping!
        grid = ((ux.astype(int) % 24 == 0) | (uy.astype(int) % 24 == 0))
        q2[grid] *= 0.82
        pipe = (np.abs(ux - 140) < 5) | (np.abs(ux - 372) < 5) | (np.abs(uy - 240) < 5)
        q2[pipe] = a_col
    elif pat == "puffer":
        # Horizontal quilted puffer baffles with shaded stitch creases!
        baffle = np.abs(np.sin(uy * math.pi / 85.0))
        q2 = np.clip(u_col[None, None, :] * (0.74 + 0.26 * (baffle ** 0.45))[:, :, None], 0.0, 1.0)
        q2[np.abs(ux - 256) < 6] = t_col
    elif pat == "devil_suit":
        # Sharp crimson-black pinstripe tailored suit!
        pinstripe = (ux.astype(int) % 26) < 3
        q2[pinstripe] = q2[pinstripe] * 0.55 + a_col * 0.45
    elif pat == "argyle_98":
        # Classic 1998 diamond argyle knitted sysadmin vest!
        ax = ((ux % 96) - 48.0) / 48.0
        ay = ((uy % 128) - 64.0) / 64.0
        diamond = (np.abs(ax) + np.abs(ay)) < 0.72
        line = np.abs((np.abs(ax) + np.abs(ay)) - 0.72) < 0.06
        q2[diamond] = (u_col * 0.65 + t_col * 0.35) * weave[diamond, None]
        q2[line] = a_col
    elif pat == "kevlar":
        # Ballistic Kevlar weave + horizontal MOLLE webbing straps + brass badge stripe!
        molle = ((uy.astype(int) % 64) < 18) & (uy < 360)
        q2[molle] = t_col * weave[molle, None]
        q2[(uy > 395) & (uy < 412)] = a_col

    arr[512:1024, 0:512, :3] = np.clip(q2, 0.0, 1.0)

    # ---------------- Q3: Lower Garment + Wood/Metal Materials (x:512..1024, y:512..1024) ----------------
    # Left half (512..768): Pants fabric (denim twill, pajamas, cargo, monk cloth)
    # Top-right (768..1024, 768..1024): Warm carved oak woodgrain
    # Bottom-right (768..1024, 512..768): Brushed scratched steel/metal
    ly, lx = np.mgrid[0:512, 0:256].astype(np.float32)
    l_col = np.array(r["lower"], dtype=np.float32)
    twill = (0.91 + 0.09 * np.sin((lx + ly) * 0.95) + rng.uniform(-0.025, 0.025, (512, 256))).astype(np.float32)
    pants = np.clip(l_col[None, None, :] * twill[:, :, None], 0.0, 1.0)
    if pat == "bathrobe":
        # Vertical blue-and-white striped pajamas!
        p_stripe = ((lx.astype(int) // 22) % 2) == 0
        pants[p_stripe] = np.array([0.42, 0.60, 0.80]) * twill[p_stripe, None]
    elif pat in ("punk_leather", "barista_hoodie"):
        # Contrast denim stitch seams along outer trouser edge!
        pants[(np.abs(lx - 128) < 3) | (np.abs(lx - 136) < 2)] = np.array([0.78, 0.62, 0.34])
    arr[512:1024, 512:768, :3] = pants

    # Brushed scratched steel (x:768..1024, y:512..768)
    my, mx = np.mgrid[0:256, 0:256].astype(np.float32)
    metal = 0.78 + 0.12 * np.sin(my * 1.4 + 0.5 * np.sin(mx * 0.08)) + rng.uniform(-0.04, 0.04, (256, 256))
    arr[512:768, 768:1024, :3] = np.clip(metal[:, :, None] * np.array([0.84, 0.88, 0.94]), 0.0, 1.0)

    # Carved warm oak woodgrain (x:768..1024, y:768..1024)
    wy, wx = np.mgrid[0:256, 0:256].astype(np.float32)
    grain = 0.78 + 0.22 * np.sin(wx * 0.28 + 3.5 * np.sin(wy * 0.04))
    arr[768:1024, 768:1024, :3] = np.clip(grain[:, :, None] * np.array([0.50, 0.30, 0.16]), 0.0, 1.0)

    # Convert painted sRGB atlas to linear RGB so Raylib's Finish.Anatomy & Archive dossierShader (which apply gamma lift)
    # render rich warm peach-tan skin, crisp dark eyebrows, deep fabrics & vivid blush (identical to VeronikaBeautyAtlas)!
    arr[:, :, :3] = np.power(np.clip(arr[:, :, :3], 0.0, 1.0), 2.15)
    img = bpy.data.images.new(f"VCAtlas_{idx}", width=W, height=H, alpha=False)
    img.colorspace_settings.name = 'sRGB'
    img.pixels.foreach_set(arr.ravel())
    img.pack()
    return img


# =============================================================================
# MATERIAL BUILDER (All materials sample the packed 1024x1024 atlas OR rich PBR!)
# =============================================================================
def build_materials(atlas_img, r):
    mats = {}
    def make_tex_mat(key, rough=0.58, met=0.0, em=None, em_str=0.0, tint=(1.0, 1.0, 1.0)):
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

    make_tex_mat("face",   rough=0.48, met=0.0, em=r["glow"] if r["face_fx"] == "crt" else None, em_str=0.45 if r["face_fx"] == "crt" else 0.0)
    make_tex_mat("eye",    rough=0.10, met=0.0, em=r["glow"] if r["face_fx"] == "demon" else None, em_str=0.85 if r["face_fx"] == "demon" else 0.0)
    make_tex_mat("hair",   rough=0.46, met=0.02)
    make_tex_mat("upper",  rough=0.72 if r["pattern"] != "punk_leather" else 0.34, met=0.04)
    make_tex_mat("lower",  rough=0.78, met=0.0)
    make_tex_mat("steel",  rough=0.22, met=0.92)
    make_tex_mat("wood",   rough=0.68, met=0.0)
    make_solid_mat("skin",   r["skin"],   rough=0.50, met=0.0)
    make_solid_mat("trim",   r["trim"],   rough=0.42, met=0.15)
    make_solid_mat("accent", r["accent"], rough=0.38, met=0.10)
    make_solid_mat("shoe",   r["shoe"],   rough=0.56, met=0.02)
    make_solid_mat("gold",   (0.95, 0.76, 0.24), rough=0.22, met=0.88, em=(0.35, 0.24, 0.04), em_str=0.12)
    make_solid_mat("coal",   (0.11, 0.12, 0.14), rough=0.42, met=0.15)
    make_solid_mat("ivory",  (0.94, 0.93, 0.90), rough=0.48, met=0.0)
    make_solid_mat("glow",   r["glow"],   rough=0.14, met=0.20, em=r["glow"], em_str=0.95)
    return mats


# =============================================================================
# BMESH SCULPTING HELPERS WITH CLEAN UV ASSIGNMENT
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


def add_box(bm, uv_layer, center, size, mat_idx, mat_key, bevel=0.0, segs=2, rot=None, smooth=True):
    ret = bmesh.ops.create_cube(bm, size=1.0)
    verts = ret["verts"]
    bmesh.ops.scale(bm, vec=Vector(size), verts=verts)
    if bevel > 0:
        edges = list({e for v in verts for e in v.link_edges})
        bmesh.ops.bevel(bm, geom=edges, offset=bevel, segments=segs, affect='EDGES', clamp_overlap=True)
    verts = list({v for f in bm.faces if all(v2 in verts or True for v2 in f.verts) for v in f.verts}) if False else verts
    # Capture all newly created faces safely via a temporary bmesh
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
    # Remove unused initial cube we created in bm by NOT creating it in bm!
    # Wait: clean implementation below!
    tmp.free()


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


# =============================================================================
# MPFB2 POSED ANATOMICAL HUMAN EXTRACTOR (Real sculpted faces, eyes & 5-finger hands!)
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

    # Straighten arms & legs cleanly down -Z so anatomical limbs align with our runtime rig!
    def point_bone_down(bname, target_dir):
        pb = rig.pose.bones[bname]
        b = rig.data.bones[bname]
        cur_dir = (b.tail_local - b.head_local).normalized()
        q_world = cur_dir.rotation_difference(target_dir.normalized())
        # Convert world rotation into bone local space
        bone_mat = b.matrix_local.to_3x3()
        q_local = (bone_mat.inverted() @ q_world.to_matrix() @ bone_mat).to_quaternion()
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = q_local

    point_bone_down("upperarm_l", Vector(( 0.04, 0.0, -1.0)))
    point_bone_down("upperarm_r", Vector((-0.04, 0.0, -1.0)))
    point_bone_down("thigh_l",    Vector(( 0.01, 0.0, -1.0)))
    point_bone_down("thigh_r",    Vector((-0.01, 0.0, -1.0)))
    bpy.context.view_layer.update()

    # Align lowerarm & hand straight down -Z relative to current posed direction
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

    # Bake posed mesh into a clean mesh object scaled to exact 1.74m tall!
    deps = bpy.context.evaluated_depsgraph_get()
    eval_obj = h.evaluated_get(deps)
    mesh = bpy.data.meshes.new_from_object(eval_obj)
    obj = bpy.data.objects.new("BakedHuman", mesh)
    bpy.context.collection.objects.link(obj)
    # Copy vertex groups from h
    for vg in h.vertex_groups:
        nvg = obj.vertex_groups.new(name=vg.name)
    for v in h.data.vertices:
        for g in v.groups:
            obj.vertex_groups[g.group].add([v.index], g.weight, 'REPLACE')

    # Measure vertical bounds of 'body' group and normalize to height 1.74m with feet at Z=0
    bi = obj.vertex_groups["body"].index
    body_verts = [v for v in mesh.vertices if any(g.group == bi and g.weight > 0.5 for g in v.groups)]
    min_z = min(v.co.z for v in body_verts)
    max_z = max(v.co.z for v in body_verts)
    scale = 1.74 / max(0.5, max_z - min_z)
    for v in mesh.vertices:
        v.co = Vector((v.co.x * scale, v.co.y * scale, (v.co.z - min_z) * scale))

    # Extract key anatomical landmarks (scaled!)
    def bone_pos(bname, use_tail=False):
        pb = rig.pose.bones[bname]
        co = pb.tail if use_tail else pb.head
        return Vector((co.x * scale, co.y * scale, (co.z - min_z) * scale))

    joints = {
        "pelvis":   bone_pos("pelvis"),
        "neck":     bone_pos("neck_01"),
        "head":     bone_pos("head"),
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

    # Remove original unbaked human & rig
    bpy.data.objects.remove(h, do_unlink=True)
    bpy.data.objects.remove(rig, do_unlink=True)
    return obj, joints


def extract_human_subset(human_obj, group_name, keep_fn, decimate_ratio=0.5, mat_idx=0, uv_mode="face", offset_vec=Vector((0, 0, 0)), scale_vec=Vector((1, 1, 1)), inflate=0.0, bone_groups=None):
    """Extract a clean polygon subset from the baked MPFB2 human using exact bone vertex groups!"""
    mesh = human_obj.data
    gi = human_obj.vertex_groups[group_name].index
    gset = {v.index for v in mesh.vertices if any(g.group == gi and g.weight > 0.5 for g in v.groups)}

    if bone_groups is not None:
        b_indices = {human_obj.vertex_groups[bn].index for bn in bone_groups if bn in human_obj.vertex_groups}
        bset = {v.index for v in mesh.vertices if v.index in gset and sum(g.weight for g in v.groups if g.group in b_indices) >= 0.35}
        gset = bset

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
    # Delete loose verts
    loose = [v for v in tmp_bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(tmp_bm, geom=loose, context='VERTS')

    if len(tmp_bm.faces) == 0:
        tmp_bm.free()
        return None

    # Optional normal inflation + subtle sinusoidal cloth fold sculpting!
    if inflate != 0.0:
        tmp_bm.normal_update()
        for v in tmp_bm.verts:
            fold = 1.0 + 0.16 * math.sin(v.co.z * 38.0 + v.co.x * 22.0)
            v.co += v.normal * (inflate * fold)

    # Shift to target pivot & decimate cleanly using a temporary object
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
        # Exact frontal projection into Q0 (0..0.5, 0..0.5) matching build_character_atlas!
        for f in out_bm.faces:
            for l in f.loops:
                co = l.vert.co
                u = 0.25 + (co.x / 0.22) * 0.25
                v = 0.25 + ((co.z - 0.125) / 0.22) * 0.25
                l[uv_layer].uv = (max(0.01, min(0.49, u)), max(0.01, min(0.49, v)))
    elif uv_mode == "eye":
        # Map each eyeball front (-Y) onto Q1 iris center (u=0.625, v=0.25)
        cx = sum(v.co.x for v in out_bm.verts) / max(1, len(out_bm.verts))
        cz = sum(v.co.z for v in out_bm.verts) / max(1, len(out_bm.verts))
        for f in out_bm.faces:
            for l in f.loops:
                co = l.vert.co
                du = (co.x - cx) / 0.0145
                dv = (co.z - cz) / 0.0145
                u = 0.625 + max(-0.095, min(0.095, du * 0.095))
                v = 0.250 + max(-0.18, min(0.18, dv * 0.18))
                l[uv_layer].uv = (u, v)
    elif uv_mode in ("upper", "lower", "hair", "steel", "wood"):
        u0, u1, v0, v1 = _uv_rect_for_mat(uv_mode)
        _assign_uv_box(out_bm.faces, uv_layer, u0, u1, v0, v1, scale=2.4)

    bpy.data.objects.remove(sub_obj, do_unlink=True)
    return out_bm, uv_layer


# =============================================================================
# 12 UNIQUE HEAD + HAIR + HEADGEAR BUILDERS (Built on REAL MPFB2 Human Heads!)
# =============================================================================
def build_head_part(idx, r, human_obj, joints, M):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    neck = joints["neck"]

    if idx == 10:
        # 10: LEGACY GHOST 1998 — Ultra-detailed Retro Beige 1998 CRT Monitor Helmet!
        # Padded dark neck gasket + coiled VGA cables running into collar
        _mk_cyl(bm, uv, (0, 0.0, 0.042), 0.072, 0.068, 0.052, M["coal"], "coal", 18)
        for s in (-1, 1):
            _mk_tube(bm, uv, [(s * 0.055, 0.075, 0.095), (s * 0.088, 0.098, 0.045), (s * 0.048, 0.068, -0.015)], 0.010, M["coal"], "coal", 8)
        # Main CRT ivory housing + tapered CRT cathode rear box + ventilation louvers!
        _mk_box(bm, uv, (0, 0.012, 0.175), (0.275, 0.245, 0.235), M["ivory"], "ivory", bevel=0.020, segs=3)
        _mk_box(bm, uv, (0, 0.155, 0.175), (0.195, 0.105, 0.165), M["ivory"], "ivory", bevel=0.016, segs=2)
        for k in range(5):
            _mk_box(bm, uv, (0, 0.05 + k * 0.032, 0.294), (0.185, 0.010, 0.008), M["coal"], "coal", bevel=0.002, segs=1)
        # Recessed screen frame & glowing painted Q0 green-phosphor CRT screen!
        _mk_box(bm, uv, (0, -0.110, 0.180), (0.225, 0.018, 0.178), M["coal"], "coal", bevel=0.006, segs=1)
        # Curved phosphor glass screen mapped squarely to Q0 (0..0.5, 0..0.5), outward normal facing -Y!
        tmp = bmesh.new()
        t_uv = tmp.loops.layers.uv.new("UVMap")
        v00 = tmp.verts.new((-0.098, -0.120, 0.102))
        v10 = tmp.verts.new(( 0.098, -0.120, 0.102))
        v11 = tmp.verts.new(( 0.098, -0.120, 0.258))
        v01 = tmp.verts.new((-0.098, -0.120, 0.258))
        f = tmp.faces.new((v00, v01, v11, v10))
        f.material_index = M["face"]
        f.smooth = True
        f.loops[0][t_uv].uv = (0.04, 0.04)
        f.loops[1][t_uv].uv = (0.04, 0.46)
        f.loops[2][t_uv].uv = (0.46, 0.46)
        f.loops[3][t_uv].uv = (0.46, 0.04)
        _merge_bm(tmp, bm, t_uv, uv)
        # Power switch, bright green power LED, floppy/badge sticker & retro whip antenna!
        _mk_box(bm, uv, (0.072, -0.116, 0.076), (0.024, 0.012, 0.016), M["coal"], "coal", bevel=0.002, segs=1)
        _mk_sphere(bm, uv, (0.098, -0.118, 0.076), (0.007, 0.007, 0.007), M["glow"], "glow", 10, 8)
        _mk_tube(bm, uv, [(0.075, 0.04, 0.292), (0.115, 0.05, 0.415)], 0.006, M["steel"], "steel", 8)
        _mk_sphere(bm, uv, (0.115, 0.05, 0.424), (0.015, 0.015, 0.015), M["glow"], "glow", 10, 8)
        return bm

    # Real MPFB2 anatomical head (face, nose, lips, ears, eyelids, jaw, neck) — scaled 1.08x for expressive indie readability!
    head_scale = Vector((1.08, 1.08, 1.08))
    head_res = extract_human_subset(human_obj, "body", lambda c: c.z >= neck.z - 0.025, decimate_ratio=0.55, mat_idx=M["face"], uv_mode="face", offset_vec=neck, scale_vec=head_scale, bone_groups=["neck_01", "head"])
    if head_res:
        _merge_bm(head_res[0], bm, head_res[1], uv)

    # Real 3D Eyeballs seated inside the MPFB2 eye sockets (except Zen monk with closed eyes!)
    if idx != 6:
        for eyeg in ("helper-l-eye", "helper-r-eye"):
            eye_res = extract_human_subset(human_obj, eyeg, lambda c: True, decimate_ratio=1.0, mat_idx=M["eye"], uv_mode="eye", offset_vec=neck, scale_vec=head_scale)
            if eye_res:
                _merge_bm(eye_res[0], bm, eye_res[1], uv)

    # Build all sculpted hair & bespoke headgear in skull-centered space, then align to MPFB2 skull center (Y=-0.040)!
    h_bm = bmesh.new()
    h_uv = h_bm.loops.layers.uv.new("UVMap")
    orig_bm, orig_uv = bm, uv
    bm, uv = h_bm, h_uv

    # Add sculpted natural hair cap + side temples + layered front bangs covering forehead cleanly down toward brows!
    if idx != 6:
        _mk_sphere(bm, uv, (0, 0.010, 0.205), (0.105, 0.120, 0.094), M["hair"], "hair", 18, 12)
        for sx in (-1, 1):
            _mk_sphere(bm, uv, (sx * 0.088, -0.025, 0.184), (0.034, 0.058, 0.060), M["hair"], "hair", 10, 8)
        for bx, by, bz, br in [
            (-0.064, -0.086, 0.194, (0.042, 0.044, 0.032)),
            (-0.022, -0.108, 0.198, (0.046, 0.044, 0.032)),
            ( 0.024, -0.108, 0.196, (0.046, 0.044, 0.032)),
            ( 0.064, -0.086, 0.192, (0.042, 0.044, 0.032)),
            ( 0.000, -0.092, 0.224, (0.078, 0.058, 0.036)),
        ]:
            _mk_sphere(bm, uv, (bx, by, bz), br, M["hair"], "hair", 10, 8)

    # Compute real head landmarks in local pivot coords
    # In scaled MPFB2 (1.74m), neck is ~1.47, eye center is at Z_local ~ 0.145, crown top ~ 0.275, front nose ~ -0.135
    # Add rich sculpted hair cap + layered hair strands + bespoke headgear for each archetype!
    if idx == 0:
        # 0: VIBE-PIRATE — Tricorne Captain Hat with gold piping + glowing skull emblem + crimson bandana + leather eyepatch + scruffy beard!
        # Sculpted messy dark pirate hair locks falling under bandana
        _mk_sphere(bm, uv, (0, 0.022, 0.175), (0.102, 0.112, 0.088), M["hair"], "hair", 18, 12)
        for s in (-1, 1):
            _mk_tube(bm, uv, [(s * 0.088, 0.035, 0.165), (s * 0.108, 0.055, 0.100), (s * 0.102, 0.068, 0.045)], [0.028, 0.024, 0.012], M["hair"], "hair", 8)
        # Pirate goatee & rugged mustache
        _mk_loft(bm, uv, [((0, -0.092, 0.072), 0.046, 0.024), ((0, -0.096, 0.032), 0.038, 0.022), ((0, -0.090, -0.008), 0.018, 0.015)], M["hair"], "hair", 14)
        _mk_tube(bm, uv, [(-0.048, -0.102, 0.082), (-0.018, -0.118, 0.096), (0.018, -0.118, 0.096), (0.048, -0.102, 0.082)], [0.007, 0.012, 0.012, 0.007], M["hair"], "hair", 8)
        # Crimson pirate bandana wrapped snugly around brow with trailing knot tails!
        _mk_loft(bm, uv, [((0, 0.000, 0.168), 0.108, 0.125), ((0, 0.000, 0.206), 0.110, 0.127)], M["accent"], "accent", 22, False, False, folds=0.025)
        for s in (-1, 1):
            _mk_tube(bm, uv, [(s * 0.016, 0.125, 0.186), (s * 0.052, 0.195, 0.158), (s * 0.082, 0.235, 0.112)], 0.013, M["accent"], "accent", 7)
        # Stitched leather EYEPATCH squarely over left eye + diagonal leather strap!
        _mk_cyl(bm, uv, (0.035, -0.122, 0.136), 0.028, 0.026, 0.014, M["coal"], "coal", 16, Euler((math.radians(90), 0, 0)))
        _mk_tube(bm, uv, [(-0.092, -0.075, 0.182), (0.035, -0.126, 0.138), (0.106, -0.012, 0.110)], 0.0055, M["coal"], "coal", 6)
        # Sculpted weathered PIRATE TRICORNE HAT seated snugly over bandana!
        _mk_sphere(bm, uv, (0, 0.006, 0.228), (0.114, 0.124, 0.084), M["coal"], "coal", 20, 12)
        _mk_box(bm, uv, (0, -0.088, 0.274), (0.315, 0.026, 0.128), M["coal"], "coal", bevel=0.012, segs=2, rot=Euler((math.radians(16), 0, 0)))
        _mk_box(bm, uv, (0, -0.100, 0.332), (0.305, 0.014, 0.017), M["gold"], "gold", bevel=0.005, segs=1, rot=Euler((math.radians(16), 0, 0)))
        for s in (-1, 1):
            _mk_box(bm, uv, (s * 0.132, 0.035, 0.262), (0.026, 0.215, 0.106), M["coal"], "coal", bevel=0.010, segs=2, rot=Euler((0, s * math.radians(-14), s * math.radians(-24))))
        # Skull & crossbones emblem on front of tricorne!
        _mk_sphere(bm, uv, (0, -0.106, 0.278), (0.026, 0.014, 0.026), M["ivory"], "ivory", 12, 8)
        for ang in (-35, 35):
            _mk_box(bm, uv, (0, -0.104, 0.262), (0.078, 0.010, 0.010), M["ivory"], "ivory", bevel=0.002, segs=1, rot=Euler((0, math.radians(ang), 0)))
        # Sweeping crimson feather plume on right side of hat!
        _mk_tube(bm, uv, [(-0.125, -0.02, 0.290), (-0.175, 0.04, 0.380), (-0.210, 0.12, 0.420)], [0.012, 0.024, 0.006], M["accent"], "accent", 8)

    elif idx == 1:
        # 1: SMOOTHIE / CAFFEINE MANIAC — Messy hair + chunky slouchy knitted pom-pom beanie seated snugly over brow!
        beanie_xf = Matrix.Translation((0, 0.018, 0.198)) @ Euler((math.radians(16), 0, 0)).to_matrix().to_4x4()
        tmp = bmesh.new(); t_uv = tmp.loops.layers.uv.new("UVMap")
        _mk_loft(tmp, t_uv, [
            ((0, 0.000, -0.026), 0.118, 0.132),
            ((0, 0.000,  0.018), 0.122, 0.136),
            ((0, 0.006,  0.042), 0.110, 0.122),
            ((0, 0.020,  0.082), 0.098, 0.108),
            ((0, 0.032,  0.110), 0.064, 0.070),
            ((0, 0.036,  0.124), 0.028, 0.030),
        ], M["coal"], "coal", 24, folds=0.022)
        _mk_loft(tmp, t_uv, [((0, 0, -0.016), 0.124, 0.138), ((0, 0, 0.010), 0.125, 0.139)], M["accent"], "accent", 24, False, False)
        _mk_sphere(tmp, t_uv, (0, 0.038, 0.136), (0.038, 0.038, 0.034), M["accent"], "accent", 12, 8)
        bmesh.ops.transform(tmp, matrix=beanie_xf, verts=tmp.verts)
        _merge_bm(tmp, bm, t_uv, uv)

    elif idx == 2:
        # 2: TINFOIL CRYPTO-BRO — Wild messy tufts + FACETED CRINKLED METALLIC SILVER TINFOIL CONE HAT + COPPER COIL ANTENNA!
        _mk_sphere(bm, uv, (0, 0.028, 0.165), (0.100, 0.108, 0.078), M["hair"], "hair", 16, 10)
        # Crinkled faceted silver foil brim + jagged cone (smooth=False for real shiny tinfoil creases!)
        _mk_loft(bm, uv, [
            ((0, 0.004, 0.194), 0.152, 0.162),
            ((0, 0.004, 0.218), 0.118, 0.128),
            ((0, 0.008, 0.292), 0.092, 0.098),
            ((0, 0.012, 0.372), 0.048, 0.052),
            ((0, 0.010, 0.438), 0.010, 0.010),
        ], M["steel"], "steel", segs=11, smooth=False, folds=0.065)
        # Folded foil brim band + coiled gold-copper spiral antenna + glowing signal beacon!
        _mk_loft(bm, uv, [((0, 0.004, 0.198), 0.132, 0.142), ((0, 0.004, 0.218), 0.124, 0.134)], M["steel"], "steel", 12, False, False, smooth=False)
        spiral = [(0.024 * math.cos(i * 1.45), 0.010 + 0.024 * math.sin(i * 1.45), 0.435 + i * 0.013) for i in range(9)]
        _mk_tube(bm, uv, spiral, 0.006, M["gold"], "gold", 8)
        _mk_sphere(bm, uv, (0, 0.010, 0.548), (0.020, 0.020, 0.020), M["glow"], "glow", 12, 8)

    elif idx == 3:
        # 3: 40-FRAMEWORK WIZARD — Sculpted long flowing silver-white wizard beard + bushy brows + wrinkled crooked starry Wizard Hat!
        _mk_sphere(bm, uv, (0, 0.032, 0.168), (0.102, 0.108, 0.082), M["hair"], "hair", 16, 12)
        for s in (-1, 1):
            _mk_tube(bm, uv, [(s * 0.088, 0.025, 0.175), (s * 0.106, 0.042, 0.085), (s * 0.095, 0.055, -0.020)], [0.030, 0.026, 0.014], M["hair"], "hair", 8)
        # Multi-lock sculpted flowing wizard beard & full walrus mustache!
        _mk_loft(bm, uv, [
            ((0, -0.092,  0.095), 0.078, 0.042),
            ((0, -0.105,  0.035), 0.068, 0.040),
            ((0, -0.108, -0.045), 0.052, 0.034),
            ((0, -0.098, -0.125), 0.024, 0.020),
        ], M["hair"], "hair", 16, folds=0.08)
        _mk_tube(bm, uv, [(-0.065, -0.098, 0.075), (-0.024, -0.122, 0.095), (0.024, -0.122, 0.095), (0.065, -0.098, 0.075)], [0.010, 0.016, 0.016, 0.010], M["hair"], "hair", 8)
        # Wide crinkled indigo wizard hat brim + crooked conical spire + gold rune buckle band!
        _mk_cyl(bm, uv, (0, 0.004, 0.222), 0.230, 0.220, 0.015, M["upper"], "upper", 26, Euler((math.radians(8), 0, 0)))
        _mk_loft(bm, uv, [
            ((0,  0.004, 0.226), 0.116, 0.126),
            ((0,  0.016, 0.305), 0.092, 0.098),
            ((0,  0.042, 0.395), 0.058, 0.062),
            ((0,  0.088, 0.478), 0.028, 0.030),
            ((0,  0.135, 0.528), 0.008, 0.008),
        ], M["upper"], "upper", 20, folds=0.04)
        _mk_loft(bm, uv, [((0, 0.005, 0.230), 0.120, 0.130), ((0, 0.009, 0.262), 0.114, 0.124)], M["gold"], "gold", 22, False, False)
        _mk_box(bm, uv, (0, -0.122, 0.246), (0.042, 0.014, 0.038), M["glow"], "glow", bevel=0.004, segs=1)

    elif idx == 4:
        # 4: SLEEPY TEAMLEAD — Messy bedhead + SILK SLEEP MASK on forehead + PLUSH TRAVEL NECK PILLOW + CALL BOOM-MIC!
        _mk_sphere(bm, uv, (0, 0.018, 0.198), (0.104, 0.116, 0.088), M["hair"], "hair", 18, 12)
        for pos, rad in [((-0.072, -0.02, 0.275), (0.044, 0.040, 0.038)), ((0.065, 0.01, 0.282), (0.048, 0.042, 0.036)), ((0.0, 0.05, 0.288), (0.050, 0.048, 0.034))]:
            _mk_sphere(bm, uv, pos, rad, M["hair"], "hair", 12, 8)
        # Padded silk sleep mask resting snugly on forehead with cute closed-eye embroidery!
        _mk_box(bm, uv, (0, -0.116, 0.198), (0.155, 0.026, 0.060), M["accent"], "accent", bevel=0.014, segs=2, rot=Euler((math.radians(14), 0, 0)))
        _mk_loft(bm, uv, [((0, 0.005, 0.190), 0.108, 0.122), ((0, 0.005, 0.206), 0.108, 0.122)], M["coal"], "coal", 20, False, False)
        for s in (-1, 1):
            _mk_box(bm, uv, (s * 0.038, -0.131, 0.198), (0.030, 0.006, 0.007), M["coal"], "coal", bevel=0.002, segs=1, rot=Euler((math.radians(14), 0, 0)))
        # Plush memory-foam U-shaped travel neck pillow wrapped snugly around neck!
        pillow_pts = [(0.124 * math.sin(math.radians(-128 + i * 25.6)), 0.124 * math.cos(math.radians(-128 + i * 25.6)), 0.026) for i in range(11)]
        _mk_tube(bm, uv, pillow_pts, 0.046, M["ivory"], "ivory", 14, cap=True)
        # Call-center boom-mic headset!
        _mk_cyl(bm, uv, (-0.108, 0.004, 0.145), 0.030, 0.026, 0.024, M["coal"], "coal", 16, Euler((0, math.radians(90), 0)))
        _mk_tube(bm, uv, [(-0.108, -0.008, 0.132), (-0.098, -0.088, 0.104), (-0.032, -0.130, 0.092)], 0.006, M["coal"], "coal", 8)
        _mk_sphere(bm, uv, (-0.024, -0.132, 0.092), (0.013, 0.011, 0.011), M["glow"], "glow", 10, 8)

    elif idx == 5:
        # 5: PUNK CYBORG REFACTORER — Huge fiery-orange dyed mohawk spikes + headband tails + X-bandage across nose!
        _mk_sphere(bm, uv, (0, 0.020, 0.192), (0.098, 0.110, 0.078), M["hair"], "hair", 16, 10)
        for k in range(7):
            z = 0.292 + 0.045 * math.sin(k * 0.55)
            y = -0.090 + k * 0.040
            _mk_cyl(bm, uv, (0, y, z), 0.030, 0.004, 0.140, M["accent"], "accent", 8, Euler((math.radians(-22 + k * 9), 0, 0)))
        _mk_loft(bm, uv, [((0, 0.0, 0.188), 0.106, 0.122), ((0, 0.0, 0.218), 0.107, 0.123)], M["trim"], "trim", 22, False, False)
        for s in (-1, 1):
            _mk_tube(bm, uv, [(s * 0.018, 0.124, 0.200), (s * 0.052, 0.195, 0.175), (s * 0.088, 0.252, 0.140)], 0.013, M["trim"], "trim", 6)
        # Crossed white plaster bandage across nose bridge!
        _mk_box(bm, uv, (0, -0.128, 0.132), (0.068, 0.009, 0.018), M["ivory"], "ivory", bevel=0.003, segs=1, rot=Euler((0, math.radians(10), 0)))
        # Cybernetic temple implant plate on right temple!
        _mk_box(bm, uv, (-0.100, -0.020, 0.155), (0.016, 0.052, 0.038), M["steel"], "steel", bevel=0.004, segs=1)
        _mk_sphere(bm, uv, (-0.108, -0.032, 0.155), (0.008, 0.008, 0.008), M["glow"], "glow", 8, 6)

    elif idx == 6:
        # 6: ZEN MONK WITHOUT COMMITS — Silver hair + topknot man-bun + flowing silver beard + ORNATE MULTI-RING GOLDEN HALO!
        _mk_sphere(bm, uv, (0, 0.020, 0.198), (0.102, 0.114, 0.086), M["hair"], "hair", 18, 12)
        _mk_sphere(bm, uv, (0, 0.044, 0.296), (0.048, 0.048, 0.042), M["hair"], "hair", 14, 10)
        _mk_cyl(bm, uv, (0, 0.038, 0.272), 0.036, 0.036, 0.014, M["gold"], "gold", 14)
        _mk_loft(bm, uv, [((0, -0.088, 0.092), 0.072, 0.042), ((0, -0.098, 0.036), 0.060, 0.036), ((0, -0.094, -0.025), 0.032, 0.022)], M["hair"], "hair", 16, folds=0.06)
        # Double ornate hovering Golden Halo ring!
        halo1 = [(0.145 * math.cos(i * math.tau / 24), 0.145 * math.sin(i * math.tau / 24), 0.368) for i in range(25)]
        halo2 = [(0.112 * math.cos(i * math.tau / 20), 0.112 * math.sin(i * math.tau / 20), 0.372) for i in range(21)]
        _mk_tube(bm, uv, halo1, 0.015, M["glow"], "glow", 10, cap=False)
        _mk_tube(bm, uv, halo2, 0.008, M["gold"], "gold", 8, cap=False)

    elif idx == 7:
        # 7: SKOLKOVO VR SHAMAN — Sculpted hair + detailed Cyberpunk VR Headset with grilles & dual telemetry antennas!
        _mk_sphere(bm, uv, (0, 0.018, 0.198), (0.104, 0.116, 0.088), M["hair"], "hair", 18, 12)
        _mk_box(bm, uv, (0, -0.122, 0.155), (0.214, 0.088, 0.098), M["coal"], "coal", bevel=0.018, segs=3)
        _mk_box(bm, uv, (0, -0.166, 0.155), (0.180, 0.016, 0.064), M["glow"], "glow", bevel=0.006, segs=2)
        _mk_box(bm, uv, (0, -0.128, 0.206), (0.140, 0.042, 0.014), M["steel"], "steel", bevel=0.004, segs=1)
        _mk_loft(bm, uv, [((0, 0.008, 0.138), 0.108, 0.122), ((0, 0.008, 0.172), 0.108, 0.122)], M["coal"], "coal", 20, False, False)
        for s in (-1, 1):
            _mk_tube(bm, uv, [(s * 0.106, -0.025, 0.178), (s * 0.145, -0.010, 0.365)], 0.009, M["steel"], "steel", 8)
            _mk_sphere(bm, uv, (s * 0.145, -0.010, 0.382), (0.025, 0.025, 0.025), M["glow"], "glow", 12, 8)

    elif idx == 8:
        # 8: MEETUP CROWN BOSS — Styled fade haircut + TILTED JEWELED VELVET-LINED GOLDEN CROWN!
        _mk_sphere(bm, uv, (0, 0.016, 0.200), (0.104, 0.116, 0.088), M["hair"], "hair", 18, 12)
        crown_xf = Matrix.Translation((0.016, 0.0, 0.252)) @ Euler((math.radians(-6), math.radians(-13), 0)).to_matrix().to_4x4()
        tmp = bmesh.new(); t_uv = tmp.loops.layers.uv.new("UVMap")
        _mk_loft(tmp, t_uv, [((0, 0, -0.026), 0.106, 0.118), ((0, 0, 0.048), 0.128, 0.138)], M["gold"], "gold", 18)
        _mk_sphere(tmp, t_uv, (0, 0, 0.032), (0.098, 0.108, 0.056), M["accent"], "accent", 16, 10)  # velvet cushion inside crown!
        _mk_loft(tmp, t_uv, [((0, 0, -0.022), 0.110, 0.122), ((0, 0, -0.004), 0.114, 0.126)], M["coal"], "coal", 18, False, False)
        for k in range(6):
            a = k * math.tau / 6
            px, py = 0.120 * math.cos(a), 0.128 * math.sin(a)
            _mk_cyl(tmp, t_uv, (px, py, 0.088), 0.026, 0.005, 0.082, M["gold"], "gold", 8)
            _mk_sphere(tmp, t_uv, (px, py, 0.130), (0.015, 0.015, 0.015), M["accent"] if k % 2 == 0 else M["glow"], "accent", 10, 8)
        bmesh.ops.transform(tmp, matrix=crown_xf, verts=tmp.verts)
        _merge_bm(tmp, bm, t_uv, uv)

    elif idx == 9:
        # 9: FRIDAY 19:59 PROD DEMON — Slicked chaotic dark hair + MASSIVE RIDGED CURVED DEVIL HORNS!
        _mk_sphere(bm, uv, (0, 0.018, 0.200), (0.105, 0.116, 0.088), M["hair"], "hair", 18, 12)
        for s in (-1, 1):
            _mk_tube(bm, uv, [
                (s * 0.058, -0.052, 0.235),
                (s * 0.122, -0.068, 0.315),
                (s * 0.108, -0.098, 0.405),
                (s * 0.058, -0.118, 0.455),
            ], [0.034, 0.026, 0.017, 0.005], M["upper"], "upper", 12, cap=True)
            # Ridged horn rings!
            for rk in range(3):
                _mk_cyl(bm, uv, (s * (0.068 + rk * 0.018), -0.056 - rk * 0.005, 0.245 + rk * 0.028), 0.036 - rk * 0.004, 0.034 - rk * 0.004, 0.012, M["coal"], "coal", 12, Euler((0, s * math.radians(-22), 0)))

    else:
        # 11: TOWER SECURITY CHIEF — Short buzz cut + tall peaked Officer Cap with gold eagle badge + chrome Aviator shades!
        _mk_sphere(bm, uv, (0, 0.024, 0.188), (0.098, 0.110, 0.074), M["hair"], "hair", 16, 10)
        _mk_loft(bm, uv, [
            ((0, 0.008, 0.208), 0.112, 0.122),
            ((0, 0.004, 0.278), 0.128, 0.134),
            ((0, 0.000, 0.298), 0.115, 0.122),
        ], M["coal"], "coal", 24)
        _mk_loft(bm, uv, [((0, 0.008, 0.206), 0.114, 0.124), ((0, 0.008, 0.222), 0.115, 0.125)], M["gold"], "gold", 24, False, False)
        _mk_box(bm, uv, (0, -0.136, 0.200), (0.155, 0.106, 0.014), M["coal"], "coal", bevel=0.005, segs=2, rot=Euler((math.radians(-15), 0, 0)))
        _mk_cyl(bm, uv, (0, -0.124, 0.252), 0.024, 0.020, 0.012, M["gold"], "gold", 14, Euler((math.radians(90), 0, 0)))
        # Chrome-rimmed aviator sunglasses
        for s in (-1, 1):
            _mk_box(bm, uv, (s * 0.042, -0.122, 0.152), (0.058, 0.008, 0.040), M["steel"], "steel", bevel=0.005, segs=2)
            _mk_box(bm, uv, (s * 0.042, -0.125, 0.152), (0.048, 0.006, 0.031), M["glow"], "glow", bevel=0.004, segs=1)
            _mk_box(bm, uv, (s * 0.080, -0.062, 0.154), (0.006, 0.118, 0.007), M["steel"], "steel", bevel=0.002, segs=1)
        _mk_box(bm, uv, (0, -0.123, 0.164), (0.030, 0.006, 0.006), M["steel"], "steel", bevel=0.001, segs=1)

    bmesh.ops.transform(h_bm, matrix=Matrix.Translation((0.0, -0.040, 0.0)), verts=h_bm.verts)
    _merge_bm(h_bm, orig_bm, h_uv, orig_uv)
    return orig_bm


# =============================================================================
# 12 UNIQUE SCULPTED OUTFIT TORSOS (Draped over real MPFB2 anatomy!)
# =============================================================================
def build_torso_part(idx, r, human_obj, joints, M):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    pelvis = joints["pelvis"]
    neck_z = joints["neck"].z
    shld_x = abs(joints["shld_l"].x)

    # Extract real anatomical torso + neck column draped with custom clothing shell (EXACT spine/pelvis bone groups!)
    inflate_amt = 0.028 if idx == 8 else (0.024 if idx in (0, 3, 4, 6, 7) else 0.018)
    torso_bones = ["pelvis", "spine_01", "spine_02", "spine_03", "clavicle_l", "clavicle_r"]
    torso_shell = extract_human_subset(
        human_obj, "body",
        lambda c: (c.z >= pelvis.z - 0.04) and (c.z <= neck_z - 0.015),
        decimate_ratio=0.45, mat_idx=M["upper"], uv_mode="upper",
        offset_vec=Vector((0, pelvis.y, pelvis.z)), inflate=inflate_amt, bone_groups=torso_bones
    )
    if torso_shell:
        _merge_bm(torso_shell[0], bm, torso_shell[1], uv)

    # Lower pelvis pants base so hip joints never gap!
    pelvis_pants = extract_human_subset(
        human_obj, "body",
        lambda c: (c.z >= pelvis.z - 0.11) and (c.z <= pelvis.z + 0.05),
        decimate_ratio=0.45, mat_idx=M["lower"], uv_mode="lower",
        offset_vec=Vector((0, pelvis.y, pelvis.z)), inflate=0.015, bone_groups=["pelvis", "thigh_l", "thigh_r"]
    )
    if pelvis_pants:
        _merge_bm(pelvis_pants[0], bm, pelvis_pants[1], uv)

    # Anatomical skin neck column tucked smoothly into collar!
    _mk_loft(bm, uv, [((0, -0.004, 0.490), 0.062, 0.062), ((0, -0.006, 0.565), 0.054, 0.056)], M["skin"], "skin", 16)

    # Now add BESPOKE SCULPTED 3D OUTFIT ARCHITECTURE per archetype!
    if idx == 0:
        # 0: PIRATE CAPTAIN COAT — Wide flared coat skirt, big gold-trimmed lapels, brass buttons, crimson waist sash & baldric belt!
        _mk_loft(bm, uv, [
            ((0, 0.010, -0.260), 0.245, 0.190),
            ((0, 0.006, -0.100), 0.215, 0.165),
            ((0, 0.002,  0.040), 0.192, 0.145),
        ], M["upper"], "upper", 22, cap_bottom=False, cap_top=False, folds=0.055)
        # Wide crimson pirate waist sash + knot!
        _mk_loft(bm, uv, [((0, 0.0, 0.020), 0.196, 0.150), ((0, 0.0, 0.100), 0.194, 0.148)], M["accent"], "accent", 22, False, False, folds=0.025)
        _mk_box(bm, uv, (0.145, -0.110, 0.010), (0.052, 0.024, 0.175), M["accent"], "accent", bevel=0.008, segs=2, rot=Euler((0, math.radians(-12), 0)))
        # Sculpted open captain coat lapels + brass buttons + diagonal leather baldric with brass buckle!
        for s in (-1, 1):
            _mk_box(bm, uv, (s * 0.088, -0.142, 0.340), (0.072, 0.022, 0.220), M["upper"], "upper", bevel=0.008, segs=2, rot=Euler((0, s * math.radians(-16), s * math.radians(-12))))
            _mk_box(bm, uv, (s * 0.098, -0.148, 0.340), (0.014, 0.010, 0.215), M["gold"], "gold", bevel=0.003, segs=1, rot=Euler((0, s * math.radians(-16), s * math.radians(-12))))
            for bk in range(3):
                _mk_cyl(bm, uv, (s * 0.068, -0.155, 0.145 + bk * 0.075), 0.013, 0.013, 0.010, M["gold"], "gold", 12, Euler((math.radians(90), 0, 0)))
        _mk_tube(bm, uv, [(-0.115, -0.095, 0.485), (0.0, -0.162, 0.285), (0.145, -0.115, 0.090), (0.165, 0.105, 0.125), (-0.105, 0.115, 0.480)], 0.018, M["coal"], "coal", 8)
        _mk_box(bm, uv, (0.0, -0.172, 0.285), (0.058, 0.016, 0.052), M["gold"], "gold", bevel=0.005, segs=1)

    elif idx == 1:
        # 1: SMOOTHIE MANIAC — Oversized hoodie hood & drawstrings + DENIM BARISTA CROSS-APRON with front pockets!
        _mk_loft(bm, uv, [((0, 0.095, 0.425), 0.135, 0.070, 22), ((0, 0.105, 0.490), 0.145, 0.075, 10), ((0, 0.032, 0.540), 0.102, 0.088, -20)], M["upper"], "upper", 20)
        # Curved stitched dark barista apron wrapping naturally around waist & chest!
        _mk_loft(bm, uv, [
            ((0, -0.025, -0.135), 0.196, 0.155),
            ((0, -0.020,  0.045), 0.190, 0.150),
            ((0, -0.018,  0.195), 0.176, 0.145),
        ], M["trim"], "trim", 22, False, False, folds=0.03)
        _mk_box(bm, uv, (0, -0.172, 0.255), (0.205, 0.018, 0.155), M["trim"], "trim", bevel=0.008, segs=2)
        _mk_box(bm, uv, (0, -0.180, 0.045), (0.205, 0.016, 0.115), M["trim"], "trim", bevel=0.006, segs=2)
        _mk_tube(bm, uv, [(-0.075, -0.148, 0.320), (-0.068, -0.068, 0.510), (0.0, 0.095, 0.515), (0.068, -0.068, 0.510), (0.075, -0.148, 0.320)], 0.009, M["coal"], "coal", 8)

    elif idx == 2:
        # 2: TINFOIL CRYPTO-BRO — High stand-up zip windbreaker collar + HUGE SCULPTED CUBAN GOLD CHAIN & `$` MEDALLION!
        _mk_loft(bm, uv, [((0, 0.002, 0.465), 0.108, 0.096), ((0, 0.002, 0.538), 0.112, 0.102)], M["trim"], "trim", 20, False, False)
        _mk_box(bm, uv, (0, -0.152, 0.255), (0.016, 0.012, 0.390), M["steel"], "steel", bevel=0.003, segs=1)
        chain_pts = [(-0.092, -0.055, 0.485), (-0.080, -0.155, 0.390), (-0.042, -0.172, 0.285), (0.0, -0.176, 0.242), (0.042, -0.172, 0.285), (0.080, -0.155, 0.390), (0.092, -0.055, 0.485)]
        _mk_tube(bm, uv, chain_pts, 0.018, M["gold"], "gold", 10)
        _mk_cyl(bm, uv, (0.0, -0.184, 0.218), 0.064, 0.064, 0.020, M["gold"], "gold", 20, Euler((math.radians(90), 0, 0)))
        _mk_cyl(bm, uv, (0.0, -0.194, 0.218), 0.040, 0.040, 0.008, M["coal"], "coal", 16, Euler((math.radians(90), 0, 0)))

    elif idx == 3:
        # 3: 40-FRAMEWORK WIZARD — Full draped starry wizard robe skirt + shoulder mantle cloak + knotted gold rope sash with tassels!
        _mk_loft(bm, uv, [
            ((0, 0.005, -0.340), 0.270, 0.215),
            ((0, 0.004, -0.140), 0.228, 0.180),
            ((0, 0.002,  0.040), 0.198, 0.152),
        ], M["upper"], "upper", 24, False, False, folds=0.07)
        # Shoulder cloak mantle!
        _mk_loft(bm, uv, [((0, 0.015, 0.365), 0.245, 0.178), ((0, 0.008, 0.465), 0.178, 0.135), ((0, 0.004, 0.515), 0.106, 0.098)], M["upper"], "upper", 22, False, False, folds=0.04)
        # Knotted golden wizard rope belt + dangling tassels!
        rope_pts = [(0.198 * math.cos(i * math.tau / 20), 0.156 * math.sin(i * math.tau / 20), 0.075) for i in range(21)]
        _mk_tube(bm, uv, rope_pts, 0.018, M["gold"], "gold", 8, cap=False)
        for s in (-1, 1):
            _mk_tube(bm, uv, [(s * 0.022, -0.158, 0.075), (s * 0.055, -0.172, -0.055), (s * 0.068, -0.178, -0.175)], 0.013, M["gold"], "gold", 7)

    elif idx == 4:
        # 4: SLEEPY TEAMLEAD — Plush terrycloth Bathrobe skirt, thick cozy shawl collar & tied waist belt with bow!
        _mk_loft(bm, uv, [((0, 0.006, -0.250), 0.240, 0.195), ((0, 0.004, -0.060), 0.212, 0.170), ((0, 0.002, 0.050), 0.196, 0.155)], M["upper"], "upper", 22, False, False, folds=0.05)
        for s in (-1, 1):
            _mk_tube(bm, uv, [(s * 0.068, 0.055, 0.505), (s * 0.088, -0.105, 0.435), (s * 0.052, -0.162, 0.285), (0.0, -0.168, 0.125)], 0.032, M["trim"], "trim", 10)
        belt_pts = [(0.200 * math.cos(i * math.tau / 20), 0.160 * math.sin(i * math.tau / 20), 0.095) for i in range(21)]
        _mk_tube(bm, uv, belt_pts, 0.020, M["trim"], "trim", 8, cap=False)
        for s in (-1, 1):
            _mk_tube(bm, uv, [(s * 0.020, -0.165, 0.095), (s * 0.058, -0.178, -0.045)], 0.016, M["trim"], "trim", 7)

    elif idx == 5:
        # 5: PUNK CYBORG REFACTORER — Sculpted wide punk biker lapels, heavy belt with silver buckle & glowing spine battery!
        for s in (-1, 1):
            _mk_box(bm, uv, (s * 0.092, -0.145, 0.355), (0.075, 0.022, 0.195), M["upper"], "upper", bevel=0.008, segs=2, rot=Euler((0, s * math.radians(-18), s * math.radians(-14))))
        _mk_box(bm, uv, (0, -0.155, 0.055), (0.375, 0.024, 0.046), M["coal"], "coal", bevel=0.005, segs=1)
        _mk_box(bm, uv, (0, -0.168, 0.055), (0.068, 0.018, 0.054), M["steel"], "steel", bevel=0.004, segs=1)
        # Cybernetic power pack bolted to upper spine feeding the right cyber-arm!
        _mk_box(bm, uv, (-0.035, 0.155, 0.335), (0.145, 0.058, 0.185), M["steel"], "steel", bevel=0.010, segs=2)
        _mk_cyl(bm, uv, (-0.035, 0.185, 0.335), 0.032, 0.032, 0.018, M["glow"], "glow", 14, Euler((math.radians(90), 0, 0)))

    elif idx == 6:
        # 6: ZEN MONK — Layered monk robe tunic skirt + GIANT CARVED WOODEN PRAYER BEADS (MALA) around neck!
        _mk_loft(bm, uv, [((0, 0.004, -0.220), 0.228, 0.178), ((0, 0.002, 0.040), 0.195, 0.150)], M["upper"], "upper", 22, False, False, folds=0.05)
        for k in range(13):
            t = k / 12.0
            a = -1.25 + t * 2.50
            bx = 0.115 * math.sin(a)
            by = -0.158 - 0.015 * math.cos(a)
            bz = 0.455 - 0.210 * math.cos(a * 0.82)
            _mk_sphere(bm, uv, (bx, by, bz), (0.022, 0.022, 0.022), M["wood"], "wood", 10, 8)

    elif idx == 7:
        # 7: SKOLKOVO VR SHAMAN — Techwear tactical chest rig + MINI SERVER-RACK JETPACK WITH TWIN STEEL THRUSTERS!
        _mk_box(bm, uv, (0, -0.158, 0.275), (0.220, 0.036, 0.135), M["coal"], "coal", bevel=0.008, segs=2)
        _mk_box(bm, uv, (0, -0.178, 0.285), (0.175, 0.008, 0.018), M["glow"], "glow", bevel=0.002, segs=1)
        # Detailed Server-Rack Jetpack on back!
        _mk_box(bm, uv, (0, 0.210, 0.305), (0.265, 0.125, 0.325), M["coal"], "coal", bevel=0.016, segs=2)
        for k in range(4):
            _mk_box(bm, uv, (0, 0.274, 0.215 + k * 0.055), (0.210, 0.010, 0.016), M["glow"] if k % 2 == 0 else M["gold"], "glow", bevel=0.002, segs=1)
        for s in (-1, 1):
            _mk_cyl(bm, uv, (s * 0.088, 0.228, 0.295), 0.052, 0.048, 0.295, M["steel"], "steel", 16)
            _mk_cyl(bm, uv, (s * 0.088, 0.228, 0.122), 0.038, 0.056, 0.072, M["coal"], "coal", 16)
            _mk_sphere(bm, uv, (s * 0.088, 0.228, 0.088), (0.036, 0.036, 0.026), M["glow"], "glow", 12, 8)

    elif idx == 8:
        # 8: MEETUP CROWN BOSS — Thick high quilted puffer collar + chunky puffer chest baffles!
        _mk_loft(bm, uv, [((0, 0.004, 0.455), 0.124, 0.112), ((0, 0.004, 0.542), 0.132, 0.122)], M["upper"], "upper", 22, False, False)
        for bk in range(3):
            _mk_loft(bm, uv, [((0, -0.004, 0.105 + bk * 0.115), 0.208, 0.162), ((0, -0.004, 0.160 + bk * 0.115), 0.218, 0.172), ((0, -0.004, 0.205 + bk * 0.115), 0.202, 0.156)], M["upper"], "upper", 22, False, False)

    elif idx == 9:
        # 9: FRIDAY 19:59 PROD DEMON — Tailored suit lapels, white dress collar, loosened crimson tie & SPADE-TIPPED DEVIL TAIL!
        _mk_loft(bm, uv, [((0, 0.004, -0.140), 0.205, 0.155), ((0, 0.002, 0.040), 0.192, 0.146)], M["upper"], "upper", 22, False, False)
        _mk_box(bm, uv, (0, -0.138, 0.415), (0.095, 0.025, 0.165), M["ivory"], "ivory", bevel=0.006, segs=1)
        _mk_box(bm, uv, (0, -0.156, 0.295), (0.038, 0.014, 0.255), M["accent"], "accent", bevel=0.005, segs=1)
        for s in (-1, 1):
            _mk_box(bm, uv, (s * 0.072, -0.148, 0.355), (0.062, 0.022, 0.235), M["trim"], "trim", bevel=0.006, segs=2, rot=Euler((0, s * math.radians(-14), s * math.radians(-10))))
        # Curving spade-tipped Devil Tail behind!
        _mk_tube(bm, uv, [(0.0, 0.138, 0.030), (0.05, 0.265, 0.010), (0.16, 0.375, 0.100), (0.22, 0.390, 0.255), (0.17, 0.340, 0.365)], [0.022, 0.019, 0.016, 0.013, 0.008], M["upper"], "upper", 10)
        _mk_cyl(bm, uv, (0.162, 0.332, 0.388), 0.036, 0.004, 0.076, M["upper"], "upper", 4, Euler((math.radians(-22), math.radians(-18), 0)))

    elif idx == 10:
        # 10: LEGACY 1998 SYSADMIN — Crisp white shirt collar, retro tie, POCKET PROTECTOR WITH COLORED PENS + BELT PAGER!
        _mk_box(bm, uv, (0, -0.140, 0.435), (0.105, 0.024, 0.115), M["ivory"], "ivory", bevel=0.006, segs=1)
        _mk_box(bm, uv, (0, -0.156, 0.315), (0.036, 0.014, 0.225), M["accent"], "accent", bevel=0.004, segs=1)
        # Pocket protector on left chest packed with 3 colored pens!
        _mk_box(bm, uv, (0.078, -0.156, 0.325), (0.056, 0.014, 0.072), M["ivory"], "ivory", bevel=0.003, segs=1)
        for pk, pmat in enumerate((M["accent"], M["glow"], M["coal"])):
            _mk_cyl(bm, uv, (0.062 + pk * 0.016, -0.160, 0.370), 0.005, 0.005, 0.042, pmat, "accent", 8)
        # Chunky 1998 pager clipped to hip belt!
        _mk_box(bm, uv, (-0.175, -0.040, 0.045), (0.032, 0.078, 0.052), M["coal"], "coal", bevel=0.006, segs=1)
        _mk_box(bm, uv, (-0.192, -0.040, 0.048), (0.006, 0.054, 0.026), M["glow"], "glow", bevel=0.002, segs=1)

    else:
        # 11: TOWER SECURITY CHIEF — Heavy Tactical Kevlar Vest with MOLLE pouches, gold badge, shoulder radio mic & utility belt!
        _mk_box(bm, uv, (0, -0.165, 0.265), (0.285, 0.048, 0.315), M["coal"], "coal", bevel=0.016, segs=2)
        _mk_box(bm, uv, (0,  0.158, 0.265), (0.275, 0.045, 0.310), M["coal"], "coal", bevel=0.016, segs=2)
        # 3 tactical MOLLE mag pouches across stomach + shiny gold СБ shield badge + shoulder radio!
        for sx in (-0.088, 0.0, 0.088):
            _mk_box(bm, uv, (sx, -0.196, 0.165), (0.068, 0.034, 0.092), M["trim"], "trim", bevel=0.006, segs=1)
        _mk_cyl(bm, uv, (0.088, -0.192, 0.355), 0.026, 0.022, 0.010, M["gold"], "gold", 14, Euler((math.radians(90), 0, 0)))
        _mk_box(bm, uv, (-0.115, -0.175, 0.395), (0.044, 0.026, 0.062), M["coal"], "coal", bevel=0.005, segs=1)
        _mk_tube(bm, uv, [(-0.115, -0.175, 0.425), (-0.125, -0.165, 0.515)], 0.004, M["coal"], "coal", 6)
        # Heavy utility belt
        belt_pts = [(0.205 * math.cos(i * math.tau / 20), 0.162 * math.sin(i * math.tau / 20), 0.048) for i in range(21)]
        _mk_tube(bm, uv, belt_pts, 0.024, M["coal"], "coal", 8, cap=False)
        _mk_box(bm, uv, (0.0, -0.172, 0.048), (0.068, 0.020, 0.052), M["gold"], "gold", bevel=0.004, segs=1)

    return bm


# =============================================================================
# ARMS, HANDS & SIGNATURE LIMB MODS (Real 5-fingered MPFB2 hands + Hook/Cyber-Claw/Staff/Smoothie!)
# =============================================================================
def build_arm_parts(idx, r, human_obj, joints, M, side):
    # side = +1 (Left, +X) or -1 (Right, -X)
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
        # PUNK CYBORG RIGHT UPPER ARM: Armored mechanical shoulder pauldron + chrome hydraulic bicep + glowing piston!
        _mk_sphere(ubm, uuv, (0.015, 0, 0.008), (0.088, 0.086, 0.078), M["coal"], "coal", 16, 10)
        _mk_box(ubm, uuv, (-0.042, 0, -0.010), (0.085, 0.115, 0.105), M["steel"], "steel", bevel=0.012, segs=2, rot=Euler((0, math.radians(-18), 0)))
        _mk_cyl(ubm, uuv, (0, 0, -0.135), 0.046, 0.044, 0.220, M["steel"], "steel", 14)
        for oy in (-0.036, 0.036):
            _mk_cyl(ubm, uuv, (-0.032, oy, -0.135), 0.012, 0.012, 0.205, M["coal"], "coal", 10)
        _mk_cyl(ubm, uuv, (0, -0.042, -0.135), 0.011, 0.011, 0.180, M["glow"], "glow", 10)
        _mk_sphere(ubm, uuv, (0, 0, -0.258), (0.052, 0.052, 0.052), M["coal"], "coal", 14, 10)
    else:
        u_inf = 0.026 if idx == 8 else (0.020 if idx in (0, 3, 4, 7) else 0.013)
        u_mat = M["skin"] if (idx == 5 and side > 0) else M["upper"]
        u_uvk = "skin" if (idx == 5 and side > 0) else "upper"
        u_res = extract_human_subset(
            human_obj, "body",
            lambda c: True,
            decimate_ratio=0.45, mat_idx=u_mat, uv_mode=u_uvk,
            offset_vec=shld, scale_vec=scale_u, inflate=0.0 if u_mat == M["skin"] else u_inf,
            bone_groups=[f"upperarm_{sfx}"]
        )
        if u_res:
            _merge_bm(u_res[0], ubm, u_res[1], uuv)
        # Smooth sleeve shoulder cap so zero gap when arms raise!
        _mk_sphere(ubm, uuv, (-side * 0.012, 0, 0.002), (0.064, 0.064, 0.064), u_mat, u_uvk, 14, 10)

    # --- FOREARM + REAL 5-FINGERED HAND (or Pirate Hook / Cyber-Claw / Smoothie Cup / Wizard Staff!) ---
    fbm = bmesh.new(); fuv = fbm.loops.layers.uv.new("UVMap")
    if idx == 0 and side < 0:
        # VIBE-PIRATE RIGHT FOREARM: Flared gold-trimmed pirate cuff + buckled leather brace + GLEAMING FORGED STEEL HOOK!
        _mk_loft(fbm, fuv, [((0, 0, 0.012), 0.052, 0.052), ((0, 0, -0.145), 0.058, 0.058), ((0, 0, -0.195), 0.068, 0.068)], M["upper"], "upper", 18)
        _mk_cyl(fbm, fuv, (0, 0, -0.188), 0.070, 0.068, 0.018, M["gold"], "gold", 18)
        _mk_cyl(fbm, fuv, (0, 0, -0.228), 0.048, 0.038, 0.068, M["coal"], "coal", 16)
        _mk_cyl(fbm, fuv, (0, 0, -0.206), 0.050, 0.050, 0.014, M["gold"], "gold", 16)
        hook_pts = [(0.0, 0.0, -0.260), (0.0, -0.012, -0.305), (0.0, -0.058, -0.362), (0.0, -0.112, -0.368), (0.0, -0.140, -0.324), (0.0, -0.112, -0.280)]
        _mk_tube(fbm, fuv, hook_pts, [0.017, 0.016, 0.015, 0.013, 0.009, 0.003], M["steel"], "steel", 12, cap=True)
    elif idx == 5 and side < 0:
        # PUNK CYBORG RIGHT FOREARM: Heavy hydraulic steel forearm + glowing power ring + 3-PRONG ARTICULATED CYBER-CLAW!
        _mk_sphere(fbm, fuv, (0, 0, 0.008), (0.052, 0.052, 0.052), M["coal"], "coal", 14, 10)
        _mk_loft(fbm, fuv, [((0, 0, -0.012), 0.056, 0.056), ((0, 0, -0.205), 0.050, 0.050)], M["steel"], "steel", 12, smooth=False)
        _mk_box(fbm, fuv, (-0.052, 0, -0.110), (0.018, 0.028, 0.155), M["coal"], "coal", bevel=0.004, segs=1)
        _mk_cyl(fbm, fuv, (0, 0, -0.135), 0.058, 0.058, 0.020, M["glow"], "glow", 14)
        _mk_cyl(fbm, fuv, (0, 0, -0.228), 0.054, 0.048, 0.046, M["coal"], "coal", 14)
        for ca in (0, 120, 240):
            rad_a = math.radians(ca)
            cx, cy = math.cos(rad_a), math.sin(rad_a)
            _mk_tube(fbm, fuv, [
                (cx * 0.032, cy * 0.032, -0.248),
                (cx * 0.064, cy * 0.064 - 0.018, -0.322),
                (cx * 0.022, cy * 0.022 - 0.036, -0.382),
            ], [0.016, 0.013, 0.005], M["steel"], "steel", 8, cap=True)
    else:
        # Real MPFB2 anatomical forearm sleeve + REAL 5-FINGERED SCULPTED HUMAN HAND (EXACT bone vertex groups — ZERO leg leak!)
        f_inf = 0.024 if idx == 8 else (0.020 if idx in (0, 3, 4, 7) else 0.012)
        slv_mat = M["skin"] if (idx == 5 and side > 0) else M["upper"]
        slv_uvk = "skin" if (idx == 5 and side > 0) else "upper"
        slv_res = extract_human_subset(
            human_obj, "body",
            lambda c: True,
            decimate_ratio=0.45, mat_idx=slv_mat, uv_mode=slv_uvk,
            offset_vec=elbow, inflate=0.0 if slv_mat == M["skin"] else f_inf,
            bone_groups=[f"lowerarm_{sfx}"]
        )
        if slv_res:
            _merge_bm(slv_res[0], fbm, slv_res[1], fuv)
        # Real 5-fingered MPFB2 hand (ONLY hand + finger bones!)
        hand_res = extract_human_subset(
            human_obj, "body",
            lambda c: True,
            decimate_ratio=0.55, mat_idx=M["skin"], uv_mode="skin",
            offset_vec=elbow,
            bone_groups=hand_bones
        )
        if hand_res:
            _merge_bm(hand_res[0], fbm, hand_res[1], fuv)
        # Ribbed cuff trim
        if slv_mat != M["skin"]:
            cuff_z = (hand.z - elbow.z) + 0.032
            _mk_cyl(fbm, fuv, (0, 0, cuff_z), 0.044, 0.048, 0.034, M["gold"] if idx == 0 else M["trim"], "trim", 16)

        # Signature handheld props gripped by the 5-fingered hand!
        hand_rel_z = hand.z - elbow.z - 0.055
        if idx == 1 and side > 0:
            # SMOOTHIE MANIAC LEFT HAND: Oversized Iced Matcha/Smoothie Cup with ice cubes, dome lid & striped straw!
            cup_xf = Matrix.Translation((0.0, -0.068, hand_rel_z)) @ Euler((math.radians(82), 0, 0)).to_matrix().to_4x4()
            tmp = bmesh.new(); t_uv = tmp.loops.layers.uv.new("UVMap")
            _mk_loft(tmp, t_uv, [((0, 0, -0.098), 0.042, 0.042), ((0, 0, 0.098), 0.058, 0.058)], M["ivory"], "ivory", 18)
            _mk_loft(tmp, t_uv, [((0, 0, -0.028), 0.051, 0.051), ((0, 0, 0.045), 0.056, 0.056)], M["accent"], "accent", 18, False, False)
            _mk_sphere(tmp, t_uv, (0, 0, 0.098), (0.059, 0.059, 0.038), M["ivory"], "ivory", 16, 10)
            _mk_tube(tmp, t_uv, [(0, 0, 0.098), (0.014, -0.008, 0.228)], 0.008, M["glow"], "glow", 8)
            bmesh.ops.transform(tmp, matrix=cup_xf, verts=tmp.verts)
            _merge_bm(tmp, fbm, t_uv, fuv)
        elif idx == 3 and side < 0:
            # 40-FRAMEWORK WIZARD RIGHT HAND: Tall gnarled carved oak staff with brass prongs & levitating glowing crystal orb!
            st_xf = Matrix.Translation((-0.014, -0.048, hand_rel_z)) @ Euler((math.radians(78), 0, 0)).to_matrix().to_4x4()
            tmp = bmesh.new(); t_uv = tmp.loops.layers.uv.new("UVMap")
            _mk_tube(tmp, t_uv, [(0, 0, -0.62), (0, 0, 0.0), (0.018, -0.01, 0.50), (-0.014, 0.012, 0.76)], [0.020, 0.022, 0.024, 0.017], M["wood"], "wood", 10)
            for ps in (-1, 1):
                _mk_tube(tmp, t_uv, [(-0.014, 0.012, 0.75), (ps * 0.062, 0.012, 0.84), (ps * 0.040, 0.012, 0.92)], 0.010, M["gold"], "gold", 8)
            _mk_sphere(tmp, t_uv, (-0.010, 0.012, 0.875), (0.056, 0.056, 0.056), M["glow"], "glow", 14, 10)
            bmesh.ops.transform(tmp, matrix=st_xf, verts=tmp.verts)
            _merge_bm(tmp, fbm, t_uv, fuv)

    return ubm, fbm


# =============================================================================
# LEGS & BESPOKE FOOTWEAR (Pirate Peg-Leg & Boot, Bunny Slippers, Balenciaga Triple-S, Combat Boots!)
# =============================================================================
def build_leg_parts(idx, r, human_obj, joints, M, side):
    hip = joints["hip_l"] if side > 0 else joints["hip_r"]
    knee = joints["knee_l"] if side > 0 else joints["knee_r"]
    thigh_len = max(0.32, hip.z - knee.z)
    shin_len = max(0.36, knee.z)
    scale_t = Vector((1.0, 1.0, 0.420 / thigh_len))
    scale_s = Vector((1.0, 1.0, 0.480 / shin_len))

    sfx = "l" if side > 0 else "r"
    # --- THIGH ---
    tbm = bmesh.new(); tuv = tbm.loops.layers.uv.new("UVMap")
    t_inf = 0.024 if idx in (3, 6, 8) else 0.016
    t_res = extract_human_subset(
        human_obj, "body",
        lambda c: True,
        decimate_ratio=0.42, mat_idx=M["lower"], uv_mode="lower",
        offset_vec=hip, scale_vec=scale_t, inflate=t_inf,
        bone_groups=[f"thigh_{sfx}"]
    )
    if t_res:
        _merge_bm(t_res[0], tbm, t_res[1], tuv)

    # --- SHIN + FOOTWEAR ---
    sbm = bmesh.new(); suv = sbm.loops.layers.uv.new("UVMap")
    if idx == 0 and side < 0:
        # VIBE-PIRATE RIGHT LEG: Rolled breeches + buckled leather knee strap + CARVED OAK PEG-LEG!
        _mk_loft(sbm, suv, [((0, 0, 0.015), 0.072, 0.074), ((0, 0, -0.110), 0.068, 0.070), ((0, 0, -0.145), 0.076, 0.078)], M["lower"], "lower", 18)
        _mk_cyl(sbm, suv, (0, 0, -0.165), 0.056, 0.048, 0.046, M["coal"], "coal", 16)
        _mk_cyl(sbm, suv, (0, 0, -0.145), 0.078, 0.078, 0.016, M["gold"], "gold", 16)
        _mk_loft(sbm, suv, [((0, 0, -0.175), 0.044, 0.044), ((0, 0, -0.255), 0.038, 0.038), ((0, 0, -0.355), 0.028, 0.028), ((0, 0, -0.448), 0.022, 0.022)], M["wood"], "wood", 16)
        _mk_cyl(sbm, suv, (0, 0, -0.255), 0.042, 0.042, 0.016, M["gold"], "gold", 16)
        _mk_cyl(sbm, suv, (0, 0, -0.464), 0.026, 0.024, 0.032, M["steel"], "steel", 14)
        return tbm, sbm

    # Extract anatomical calf down to ankle!
    calf_res = extract_human_subset(
        human_obj, "body",
        lambda c: c.z >= 0.095,
        decimate_ratio=0.42, mat_idx=M["lower"], uv_mode="lower",
        offset_vec=knee, scale_vec=scale_s, inflate=0.016 if idx != 8 else 0.028,
        bone_groups=[f"calf_{sfx}"]
    )
    if calf_res:
        _merge_bm(calf_res[0], sbm, calf_res[1], suv)

    # Bespoke sculpted footwear grounded strictly at Z = -0.480!
    if idx == 0:
        # Pirate tall cuffed leather captain boot!
        _mk_loft(sbm, suv, [((0, 0, -0.160), 0.078, 0.082), ((0, 0, -0.215), 0.066, 0.070), ((0, 0, -0.395), 0.056, 0.062)], M["shoe"], "shoe", 18)
        _mk_box(sbm, suv, (0, -0.055, -0.445), (0.108, 0.258, 0.068), M["shoe"], "shoe", bevel=0.018, segs=2)
        _mk_box(sbm, suv, (0, -0.055, -0.472), (0.112, 0.264, 0.016), M["coal"], "coal", bevel=0.004, segs=1)
        _mk_box(sbm, suv, (0, -0.095, -0.405), (0.046, 0.018, 0.036), M["gold"], "gold", bevel=0.004, segs=1)
    elif idx == 4:
        # Sleepy TeamLead FLUFFY BUNNY BEDROOM SLIPPERS with upright cute bunny ears!
        _mk_box(sbm, suv, (0, -0.052, -0.440), (0.122, 0.268, 0.076), M["shoe"], "shoe", bevel=0.028, segs=3)
        _mk_box(sbm, suv, (0, -0.052, -0.474), (0.124, 0.270, 0.012), M["coal"], "coal", bevel=0.004, segs=1)
        for es in (-1, 1):
            _mk_sphere(sbm, suv, (es * 0.036, -0.125, -0.382), (0.018, 0.022, 0.052), M["shoe"], "shoe", 10, 8, Euler((math.radians(-22), es * math.radians(15), 0)))
            _mk_sphere(sbm, suv, (es * 0.036, -0.132, -0.384), (0.010, 0.012, 0.034), M["accent"], "accent", 8, 6, Euler((math.radians(-22), es * math.radians(15), 0)))
    elif idx == 8:
        # Meetup Crown Boss: COMICALLY MASSIVE MULTI-TIER BALENCIAGA TRIPLE-S SNEAKERS!
        _mk_box(sbm, suv, (0, -0.068, -0.468), (0.155, 0.345, 0.024), M["coal"], "coal", bevel=0.008, segs=2)
        _mk_box(sbm, suv, (0, -0.065, -0.442), (0.162, 0.352, 0.032), M["accent"], "accent", bevel=0.010, segs=2)
        _mk_box(sbm, suv, (0, -0.060, -0.415), (0.150, 0.325, 0.032), M["ivory"], "ivory", bevel=0.012, segs=2)
        _mk_box(sbm, suv, (0, -0.042, -0.380), (0.132, 0.255, 0.062), M["shoe"], "shoe", bevel=0.020, segs=2)
    elif idx in (5, 11):
        # Heavy laced Tactical / Punk Combat Boots!
        _mk_loft(sbm, suv, [((0, 0, -0.240), 0.064, 0.068), ((0, 0, -0.395), 0.058, 0.064)], M["shoe"], "shoe", 16)
        _mk_box(sbm, suv, (0, -0.054, -0.440), (0.114, 0.268, 0.072), M["shoe"], "shoe", bevel=0.016, segs=2)
        _mk_box(sbm, suv, (0, -0.054, -0.470), (0.118, 0.274, 0.020), M["coal"], "coal", bevel=0.005, segs=1)
        for lk in range(3):
            _mk_box(sbm, suv, (0, -0.068, -0.275 - lk * 0.038), (0.048, 0.010, 0.008), M["steel"], "steel", bevel=0.002, segs=1)
    else:
        # Detailed modern sneakers / loafers / sandals
        _mk_box(sbm, suv, (0, -0.054, -0.472), (0.106, 0.254, 0.016), M["coal"], "coal", bevel=0.005, segs=1)
        _mk_box(sbm, suv, (0, -0.054, -0.450), (0.110, 0.260, 0.030), M["ivory"], "ivory", bevel=0.010, segs=2)
        _mk_box(sbm, suv, (0, -0.038, -0.418), (0.102, 0.205, 0.054), M["shoe"], "shoe", bevel=0.018, segs=2)
        _mk_box(sbm, suv, (0, -0.038, -0.422), (0.106, 0.075, 0.016), M["accent"], "accent", bevel=0.004, segs=1)

    return tbm, sbm


# =============================================================================
# BAKE 16-RAY BVHTREE AO & EXPORT SELF-CONTAINED 10-PART GLB (`vc_{idx}.glb`)
# =============================================================================
def finalize_part_obj(name, bm, mats_list):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name + "_Mesh")
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    for m in mats_list:
        mesh.materials.append(m)

    # Fast 12-ray BVHTree Ambient Occlusion baked into COLOR_0
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
        vert_ao.append(max(0.72, min(1.0, 1.0 - (occ / n_rays) * 0.34)))
    bm_ao.free()

    col_attr = mesh.color_attributes.new(name="COLOR_0", type='BYTE_COLOR', domain='CORNER')
    for poly in mesh.polygons:
        for li in poly.loop_indices:
            ao = vert_ao[mesh.loops[li].vertex_index]
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
    objs = [finalize_part_obj(name, bm, mats_list) for name, bm in parts_bms]

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
    print("ALL STEAM-GRADE MPFB2 VIBE CODERS v3 BUILT SUCCESSFULLY!")


if __name__ == "__main__":
    main()
