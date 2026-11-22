import bpy
import math
import os
import json
from mathutils import Vector

PROJ = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder"
MODELS = os.path.join(PROJ, "assets", "models")
TILE_DIR = os.path.join(PROJ, "scratch", "hero_v2_qc_tiles")
os.makedirs(TILE_DIR, exist_ok=True)

with open(os.path.join(PROJ, "scratch", "hero_v2_meta.json"), "r", encoding="utf-8") as f:
    META = json.load(f)

HAT_G = META["HatAnchor_gltf"]
EYE_G = META["EyeAnchor_gltf"]
# glTF (x, y, z) -> Blender (x, -z, y)
HAT_B = Vector((HAT_G[0], -HAT_G[2], HAT_G[1]))
EYE_B = Vector((EYE_G[0], -EYE_G[2], EYE_G[1]))


def setup_scene(res_x=640, res_y=840):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = res_x
    scene.render.resolution_y = res_y
    scene.render.image_settings.file_format = 'PNG'
    scene.view_settings.view_transform = 'AgX'
    scene.view_settings.look = 'AgX - Medium High Contrast'

    world = bpy.data.worlds.new("QCWorld")
    scene.world = world
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.14, 0.14, 0.16, 1.0)
    bg.inputs[1].default_value = 0.75

    def add_sun(name, rot_deg, energy, color):
        ld = bpy.data.lights.new(name, 'SUN')
        ld.energy = energy
        ld.color = color
        lo = bpy.data.objects.new(name, ld)
        scene.collection.objects.link(lo)
        lo.rotation_euler = tuple(math.radians(a) for a in rot_deg)

    # Sculpted 3-point Atelier lighting (matching Wardrobe Studio!)
    add_sun("Key", (48, 0, 34), 2.8, (1.0, 0.96, 0.90))
    add_sun("Fill", (62, 0, -128), 1.1, (0.78, 0.86, 1.0))
    add_sun("Rim", (42, 0, 162), 1.6, (1.0, 0.92, 0.80))

    # Subtle matte studio floor disc
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=1.2, depth=0.02, location=(0, 0, -0.01))
    fl = bpy.context.active_object
    fm = bpy.data.materials.new("StudioFloor")
    fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.11, 0.11, 0.125, 1.0)
    fm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.85
    fl.data.materials.append(fm)

    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS, "char_partner_90s.glb"))
    hero = bpy.data.objects["char_partner_90s"]

    cam_d = bpy.data.cameras.new("Cam")
    cam_d.lens = 58
    cam = bpy.data.objects.new("Cam", cam_d)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return scene, hero, cam


def point_cam(cam, loc, target, lens=58):
    cam.data.lens = lens
    cam.location = Vector(loc)
    d = Vector(target) - cam.location
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()


def set_mat_tint(mat_name, srgb_255):
    mat = bpy.data.materials.get(mat_name)
    if not mat:
        return
    # Convert sRGB 0..255 to linear for Blender Multiply B socket
    lin = tuple(((c / 255.0) ** 2.2) for c in srgb_255)
    for n in mat.node_tree.nodes:
        if n.type == 'MIX' and n.blend_type == 'MULTIPLY':
            n.inputs["B"].default_value = (lin[0], lin[1], lin[2], 1.0)
            return
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf and not bsdf.inputs["Base Color"].is_linked:
        bsdf.inputs["Base Color"].default_value = (lin[0], lin[1], lin[2], 1.0)


def import_acc(glb_name, anchor_b):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS, glb_name))
    added = [o for o in bpy.data.objects if o not in before]
    for o in added:
        if o.parent is None:
            o.location = anchor_b
    return added


def remove_objs(objs):
    for o in objs:
        bpy.data.objects.remove(o, do_unlink=True)


def render_turnaround():
    scene, hero, cam = setup_scene(560, 760)
    # 1) Full-body Front
    point_cam(cam, (0.0, -3.10, 0.94), (0.0, 0.0, 0.88), lens=55)
    scene.render.filepath = os.path.join(TILE_DIR, "t_front.png")
    bpy.ops.render.render(write_still=True)

    # 2) Full-body 3/4 Hero
    point_cam(cam, (1.45, -2.75, 0.96), (0.0, 0.0, 0.88), lens=55)
    scene.render.filepath = os.path.join(TILE_DIR, "t_34.png")
    bpy.ops.render.render(write_still=True)

    # 3) Full-body Side (+X)
    point_cam(cam, (3.10, -0.05, 0.94), (0.0, 0.0, 0.88), lens=55)
    scene.render.filepath = os.path.join(TILE_DIR, "t_side.png")
    bpy.ops.render.render(write_still=True)

    # 4) Full-body Back (-Z glTF = +Y Blender)
    point_cam(cam, (0.0, +3.10, 0.94), (0.0, 0.0, 0.88), lens=55)
    scene.render.filepath = os.path.join(TILE_DIR, "t_back.png")
    bpy.ops.render.render(write_still=True)

    # 5) Close-up Face (front-3/4 portrait!)
    scene.render.resolution_x = 560
    scene.render.resolution_y = 560
    point_cam(cam, (0.20, -0.68, 1.60), (0.0, -0.04, 1.565), lens=72)
    scene.render.filepath = os.path.join(TILE_DIR, "t_face_34.png")
    bpy.ops.render.render(write_still=True)

    # 6) Close-up Face Front
    point_cam(cam, (0.0, -0.68, 1.585), (0.0, -0.04, 1.565), lens=72)
    scene.render.filepath = os.path.join(TILE_DIR, "t_face_front.png")
    bpy.ops.render.render(write_still=True)

    # 7) Close-up Hands (Right hand with gold signet ring + Left hand with watch!)
    point_cam(cam, (-0.54, -0.45, 0.84), (-0.28, -0.02, 0.78), lens=65)
    scene.render.filepath = os.path.join(TILE_DIR, "t_hand_r.png")
    bpy.ops.render.render(write_still=True)

    point_cam(cam, (+0.54, -0.45, 0.84), (+0.28, -0.02, 0.78), lens=65)
    scene.render.filepath = os.path.join(TILE_DIR, "t_hand_l.png")
    bpy.ops.render.render(write_still=True)

    # 8) White «Сочи-98» Blazer Tint Preview (verifies folds, lapels & wool weave on white!)
    set_mat_tint("crimson", (234, 228, 214))
    set_mat_tint("lapel", (206, 198, 182))
    scene.render.resolution_x = 560
    scene.render.resolution_y = 760
    point_cam(cam, (1.25, -2.55, 1.02), (0.0, 0.0, 0.96), lens=56)
    scene.render.filepath = os.path.join(TILE_DIR, "t_white_sochi.png")
    bpy.ops.render.render(write_still=True)


def render_hats_and_eyes():
    scene, hero, cam = setup_scene(480, 480)
    # 8 hats in 3/4 view + 4 key hats in side profile + 4 eyewear + 2 bare iris tints
    hats = [
        ("hat_crown.glb", "crown"),
        ("hat_ushanka.glb", "ushanka"),
        ("hat_frog_head.glb", "frog"),
        ("hat_cowboy.glb", "cowboy"),
        ("hat_tinfoil.glb", "tinfoil"),
        ("hat_traffic_cone.glb", "cone"),
        ("hat_chef.glb", "chef"),
        ("hat_pirate.glb", "pirate"),
    ]
    for glb, tag in hats:
        objs = import_acc(glb, HAT_B)
        point_cam(cam, (0.36, -0.82, 1.72), (0.0, HAT_B.y, 1.63), lens=58)
        scene.render.filepath = os.path.join(TILE_DIR, f"he_hat_{tag}.png")
        bpy.ops.render.render(write_still=True)
        remove_objs(objs)

    eyes = [
        ("eye_aviators.glb", "aviators"),
        ("eye_dealwithit.glb", "pixel"),
        ("eye_accountant.glb", "accountant"),
        ("eye_matrix.glb", "matrix"),
    ]
    for glb, tag in eyes:
        objs = import_acc(glb, EYE_B)
        point_cam(cam, (0.20, -0.52, 1.61), (0.0, EYE_B.y + 0.05, EYE_B.z - 0.01), lens=64)
        scene.render.filepath = os.path.join(TILE_DIR, f"he_eye_{tag}.png")
        bpy.ops.render.render(write_still=True)
        remove_objs(objs)

    # Bare eyes with Blue & Green iris tints + side profile with Crown & Aviators!
    set_mat_tint("iris", (76, 154, 218))
    point_cam(cam, (0.14, -0.46, 1.60), (0.0, EYE_B.y + 0.05, EYE_B.z - 0.01), lens=70)
    scene.render.filepath = os.path.join(TILE_DIR, "he_bare_blue.png")
    bpy.ops.render.render(write_still=True)

    set_mat_tint("iris", (64, 164, 96))
    point_cam(cam, (-0.14, -0.46, 1.60), (0.0, EYE_B.y + 0.05, EYE_B.z - 0.01), lens=70)
    scene.render.filepath = os.path.join(TILE_DIR, "he_bare_green.png")
    bpy.ops.render.render(write_still=True)

    # Side profile check with Crown + Aviators!
    o1 = import_acc("hat_crown.glb", HAT_B)
    o2 = import_acc("eye_aviators.glb", EYE_B)
    point_cam(cam, (0.78, -0.10, 1.65), (0.0, HAT_B.y, 1.62), lens=58)
    scene.render.filepath = os.path.join(TILE_DIR, "he_combo_side.png")
    bpy.ops.render.render(write_still=True)
    remove_objs(o1 + o2)

    # Combo 3/4 with White Sochi blazer + Crown + Pixel shades!
    set_mat_tint("crimson", (234, 228, 214))
    set_mat_tint("lapel", (206, 198, 182))
    o1 = import_acc("hat_crown.glb", HAT_B)
    o2 = import_acc("eye_dealwithit.glb", EYE_B)
    point_cam(cam, (0.45, -1.05, 1.64), (0.0, HAT_B.y, 1.48), lens=58)
    scene.render.filepath = os.path.join(TILE_DIR, "he_combo_sochi.png")
    bpy.ops.render.render(write_still=True)
    remove_objs(o1 + o2)


render_turnaround()
render_hats_and_eyes()
print("HERO_V2_QC_TILES_DONE")
