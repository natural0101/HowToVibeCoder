import bpy
import math
import os
import json
import struct
import numpy as np
from mathutils import Vector, Euler, Matrix

ROOT = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder"
MODELS_DIR = os.path.join(ROOT, r"assets\models")
OUT_TURN = os.path.join(ROOT, r"scratch\hunyuan_final_turnaround_qc.png")
OUT_HATS = os.path.join(ROOT, r"scratch\hunyuan_final_hats_eyes_qc.png")

# Must match build_hunyuan_hero.py (in glTF Y-up, converted to Blender Z-up via (x, -z, y))
RIGHT_SHOULDER_PIVOT_GLTF = Vector((-0.194, 1.352, -0.052))
RIGHT_HAND_GRIP_OFFSET_GLTF = Vector((-0.064, -0.550, 0.084))
AIM_PITCH_DEG = -76.0
EYE_ANCHOR_GLTF = Vector((0.0, 1.586, 0.103))
HAT_ANCHOR_GLTF = Vector((0.0, 1.690, -0.012))


def gltf_to_bl(v):
    return Vector((v.x, -v.z, v.y))


def setup_scene_cycles(res=600, samples=24):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.resolution_x = res
    scene.render.resolution_y = res
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.view_settings.view_transform = 'AgX'
    scene.view_settings.look = 'AgX - Medium High Contrast'

    prefs = bpy.context.preferences.addons['cycles'].preferences
    for backend in ('OPTIX', 'CUDA'):
        try:
            prefs.compute_device_type = backend
            prefs.get_devices()
            gpu = False
            for d in prefs.devices:
                if d.type != 'CPU':
                    d.use = True
                    gpu = True
                else:
                    d.use = False
            if gpu:
                scene.cycles.device = 'GPU'
                break
        except Exception:
            pass

    # Clean neutral-dark tailor studio backdrop
    world = bpy.data.worlds.new("StudioWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.09, 0.095, 0.11, 1.0)
        bg.inputs["Strength"].default_value = 0.85

    # 3-point studio lighting (warm front-right key, cool left fill, crisp top-back rim)
    def add_area(name, loc, rot_deg, energy, size, color):
        ld = bpy.data.lights.new(name, type='AREA')
        ld.energy = energy
        ld.size = size
        ld.color = color
        lo = bpy.data.objects.new(name, ld)
        lo.location = loc
        lo.rotation_euler = tuple(math.radians(a) for a in rot_deg)
        bpy.context.collection.objects.link(lo)
        return lo

    add_area("KeyLight", (1.6, -2.4, 2.3), (62, 0, 32), 260, 1.8, (1.0, 0.94, 0.86))
    add_area("FillLight", (-2.0, -1.8, 1.5), (72, 0, -48), 95, 2.2, (0.76, 0.86, 1.0))
    add_area("RimLight", (-1.1, 2.1, 2.2), (118, 0, -150), 175, 1.4, (0.85, 0.90, 1.0))
    add_area("BackKey", (0.8, 2.4, 1.8), (108, 0, 160), 110, 1.6, (0.98, 0.94, 0.90))

    # Studio floor plane at Z=0 (verify feet touch floor cleanly with contact shadow!)
    bpy.ops.mesh.primitive_plane_add(size=8.0, location=(0, 0, 0))
    fl = bpy.context.active_object
    fl.name = "StudioFloor"
    fm = bpy.data.materials.new("FloorMat")
    fm.use_nodes = True
    bsdf = fm.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (0.12, 0.12, 0.14, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.85
    fl.data.materials.append(fm)

    # Camera
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 68
    cam_obj = bpy.data.objects.new("Cam", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    scene.camera = cam_obj
    return scene, cam_obj


def look_at(cam_obj, loc, target, lens=68):
    cam_obj.data.lens = lens
    cam_obj.location = Vector(loc)
    direction = Vector(target) - Vector(loc)
    cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()


def import_glb_group(path):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    added = [o for o in bpy.context.scene.objects if o not in before]
    return added


def remove_objs(objs):
    for o in objs:
        if o.name in bpy.data.objects:
            bpy.data.objects.remove(o, do_unlink=True)


def set_mat_tint(mat_name, rgba_linear):
    m = bpy.data.materials.get(mat_name)
    if not m or not m.use_nodes:
        return
    nt = m.node_tree
    bsdf = None
    for n in nt.nodes:
        if n.type == 'BSDF_PRINCIPLED':
            bsdf = n
            break
    if not bsdf:
        return
    inp = bsdf.inputs["Base Color"]
    if inp.is_linked:
        src_node = inp.links[0].from_node
        if src_node.type == 'MIX':
            # glTF importer imports baseColorTexture * baseColorFactor as MixRGB MULTIPLY
            for s in src_node.inputs:
                if s.type == 'RGBA' and not s.is_linked:
                    s.default_value = rgba_linear
                    return
        # If no Mix node, insert one
        tex_out = inp.links[0].from_socket
        nt.links.remove(inp.links[0])
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = 'RGBA'
        mix.blend_type = 'MULTIPLY'
        mix.inputs["Factor"].default_value = 1.0
        mix.inputs["B"].default_value = rgba_linear
        nt.links.new(tex_out, mix.inputs["A"])
        nt.links.new(mix.outputs["Result"], inp)
    else:
        inp.default_value = rgba_linear


def render_panel_to_array(scene, tmp_path):
    scene.render.filepath = tmp_path
    bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(tmp_path)
    w, h = im.size
    arr = np.array(im.pixels[:], dtype=np.float32).reshape((h, w, 4))[:, :, :3].copy()
    bpy.data.images.remove(im, do_unlink=True)
    return arr


def stitch_grid(panels, cols, rows, out_path):
    """panels ordered top-left -> bottom-right; bpy images are bottom-up!"""
    ph, pw, _ = panels[0].shape
    grid = np.zeros((rows * ph, cols * pw, 4), dtype=np.float32)
    grid[:, :, 3] = 1.0
    for idx, p in enumerate(panels):
        r = idx // cols
        c = idx % cols
        # top row (r=0) goes at top of bottom-up numpy buffer!
        y0 = (rows - 1 - r) * ph
        x0 = c * pw
        grid[y0:y0 + ph, x0:x0 + pw, :3] = p
    out_im = bpy.data.images.new("Grid", width=cols * pw, height=rows * ph, alpha=False)
    out_im.pixels[:] = grid.flatten()
    out_im.filepath_raw = out_path
    out_im.file_format = 'PNG'
    out_im.save()
    bpy.data.images.remove(out_im, do_unlink=True)
    print(f"[QC SAVED] {out_path} ({cols*pw}x{rows*ph})")


def verify_glb_contract(path):
    with open(path, "rb") as f:
        f.seek(12)
        length = struct.unpack("<I", f.read(4))[0]
        assert f.read(4) == b"JSON"
        doc = json.loads(f.read(length))
    mats = [m["name"] for m in doc.get("materials", [])]
    prims = [doc["materials"][p["material"]]["name"] for m in doc["meshes"] for p in m["primitives"]]
    prop_count = sum(1 for n in prims if n in ("GunMetal", "Gold90s", "PolymerGrip"))
    print(f"[CONTRACT] {os.path.basename(path)}: prims={len(prims)} propCount={prop_count} mats={mats}")
    return prop_count, mats


def render_turnaround_qc():
    verify_glb_contract(os.path.join(MODELS_DIR, "char_partner_90s.glb"))
    verify_glb_contract(os.path.join(MODELS_DIR, "char_partner_90s_body.glb"))
    verify_glb_contract(os.path.join(MODELS_DIR, "char_partner_90s_arm_r.glb"))

    scene, cam = setup_scene_cycles(res=600, samples=20)
    tmp_png = os.path.join(ROOT, r"scratch\gen3d\_tmp_panel.png")
    panels = []

    # Load full hero
    full_objs = import_glb_group(os.path.join(MODELS_DIR, "char_partner_90s.glb"))
    # Verify exact bounding box of imported GLB!
    meshes = [o for o in full_objs if o.type == 'MESH']
    verts = np.array([list(o.matrix_world @ v.co) for o in meshes for v in o.data.vertices])
    print(f"[VERIFY GLB] char_partner_90s.glb Blender BBox: X=[{verts[:,0].min():.4f},{verts[:,0].max():.4f}] Y=[{verts[:,1].min():.4f},{verts[:,1].max():.4f}] Z=[{verts[:,2].min():.4f},{verts[:,2].max():.4f}]")

    # Panel 1: Front
    look_at(cam, (0.0, -3.15, 0.92), (0.0, 0.0, 0.88), lens=65)
    panels.append(render_panel_to_array(scene, tmp_png))

    # Panel 2: 3/4 Front
    look_at(cam, (-1.85, -2.55, 0.96), (0.0, 0.0, 0.88), lens=65)
    panels.append(render_panel_to_array(scene, tmp_png))

    # Panel 3: Side (-X right profile)
    look_at(cam, (-3.15, 0.0, 0.92), (0.0, 0.0, 0.88), lens=65)
    panels.append(render_panel_to_array(scene, tmp_png))

    # Panel 4: Back
    look_at(cam, (0.0, 3.15, 0.92), (0.0, 0.0, 0.88), lens=65)
    panels.append(render_panel_to_array(scene, tmp_png))

    # Panel 5: Face & Upper Chest Close-Up
    look_at(cam, (-0.22, -0.95, 1.56), (0.0, 0.0, 1.52), lens=95)
    panels.append(render_panel_to_array(scene, tmp_png))

    # Panel 6: White "Sochi-98" Blazer + Deep Skin Tint Preview!
    # Sochi-98 White sRGB (234,228,214) -> linear (~0.83, 0.78, 0.67); Lapel (206,198,182) -> (~0.62, 0.56, 0.47)
    # Deep Skin (112,74,52)^1.55 -> (0.278, 0.147, 0.084)
    set_mat_tint("crimson", (0.83, 0.78, 0.67, 1.0))
    set_mat_tint("lapel", (0.62, 0.56, 0.47, 1.0))
    set_mat_tint("skin", (0.278, 0.147, 0.084, 1.0))
    look_at(cam, (1.35, -2.40, 1.06), (0.0, 0.0, 1.00), lens=70)
    panels.append(render_panel_to_array(scene, tmp_png))

    # Remove full hero -> test Articulated Body + Raised Right Arm + UZI!
    remove_objs(full_objs)
    # Purge materials so fresh Crimson/Fair load cleanly for body + arm!
    for m in list(bpy.data.materials):
        if m.name != "FloorMat":
            bpy.data.materials.remove(m, do_unlink=True)

    body_objs = import_glb_group(os.path.join(MODELS_DIR, "char_partner_90s_body.glb"))
    arm_objs = import_glb_group(os.path.join(MODELS_DIR, "char_partner_90s_arm_r.glb"))
    uzi_objs = import_glb_group(os.path.join(MODELS_DIR, "weapon_uzi_90s.glb"))

    # Position Right Arm at RightShoulderPivot and rotate by AIM_PITCH_DEG around X!
    pivot_bl = gltf_to_bl(RIGHT_SHOULDER_PIVOT_GLTF)
    arm_rot = Euler((math.radians(AIM_PITCH_DEG), 0.0, 0.0), 'XYZ')
    for o in arm_objs:
        if o.parent is None:
            o.rotation_mode = 'XYZ'
            o.location = pivot_bl
            o.rotation_euler = arm_rot

    # Compute exact world position of right palm after pitching arm by AIM_PITCH_DEG!
    grip_off_bl = gltf_to_bl(RIGHT_HAND_GRIP_OFFSET_GLTF)
    palm_world_bl = pivot_bl + arm_rot.to_matrix() @ grip_off_bl

    # UZI scale = 0.74 (same as PartnerAvatar.cs!). Barrel points +Y in UZI local space -> rotate Z 180° to aim forward (-Y)!
    uzi_scale = 0.74
    uzi_rot = Euler((0.0, 0.0, math.radians(180.0)), 'XYZ')
    grip_local_in_uzi = Vector((0.0, -0.012, -0.062)) * uzi_scale
    uzi_origin_bl = palm_world_bl - (uzi_rot.to_matrix() @ grip_local_in_uzi)

    for o in uzi_objs:
        if o.parent is None:
            o.rotation_mode = 'XYZ'
            o.location = uzi_origin_bl
            o.rotation_euler = uzi_rot
            o.scale = (uzi_scale, uzi_scale, uzi_scale)

    print(f"[CO-OP UZI PROOF] palm_world_bl={tuple(round(c,4) for c in palm_world_bl)} uzi_origin_bl={tuple(round(c,4) for c in uzi_origin_bl)}")

    # Panel 7: Co-op Raised Right Arm + UZI (Side View!)
    look_at(cam, (-2.25, -0.35, 1.25), (-0.15, -0.25, 1.22), lens=72)
    panels.append(render_panel_to_array(scene, tmp_png))

    # Panel 8: Co-op Raised Right Arm + UZI (3/4 Close-Up!)
    look_at(cam, (-1.45, -1.65, 1.38), (-0.18, -0.22, 1.25), lens=75)
    panels.append(render_panel_to_array(scene, tmp_png))

    stitch_grid(panels, 4, 2, OUT_TURN)


def render_hats_eyes_qc():
    scene, cam = setup_scene_cycles(res=600, samples=18)
    tmp_png = os.path.join(ROOT, r"scratch\gen3d\_tmp_panel2.png")
    panels = []

    hero_objs = import_glb_group(os.path.join(MODELS_DIR, "char_partner_90s.glb"))
    hat_bl = gltf_to_bl(HAT_ANCHOR_GLTF)
    eye_bl = gltf_to_bl(EYE_ANCHOR_GLTF)

    combos = [
        ("hat_crown.glb", hat_bl, None),
        ("hat_ushanka.glb", hat_bl, None),
        ("hat_frog_head.glb", hat_bl, None),
        ("hat_cowboy.glb", hat_bl, None),
        ("eye_aviators.glb", eye_bl, None),
        ("eye_dealwithit.glb", eye_bl, None),
        ("eye_accountant.glb", eye_bl, None),
        (None, None, (0.09, 0.36, 0.72, 1.0)),  # Bare Vivid Blue Eyes!
    ]

    for idx, (asset_name, anchor, iris_tint) in enumerate(combos):
        acc = []
        if asset_name:
            acc = import_glb_group(os.path.join(MODELS_DIR, asset_name))
            for o in acc:
                if o.parent is None:
                    o.location = anchor
        if iris_tint is not None:
            set_mat_tint("iris", iris_tint)
            look_at(cam, (-0.10, -0.62, 1.59), (0.0, 0.0, 1.58), lens=105)
        elif asset_name and asset_name.startswith("eye_"):
            look_at(cam, (-0.28, -0.82, 1.60), (0.0, 0.0, 1.58), lens=90)
        else:
            look_at(cam, (-0.36, -1.08, 1.68), (0.0, 0.0, 1.66), lens=82)
        panels.append(render_panel_to_array(scene, tmp_png))
        remove_objs(acc)

    stitch_grid(panels, 4, 2, OUT_HATS)


if __name__ == "__main__":
    render_turnaround_qc()
    render_hats_eyes_qc()
