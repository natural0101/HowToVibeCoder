import bpy
import math
import os
import json
import numpy as np
from mathutils import Vector, Euler

ROOT = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder"
MODELS_DIR = os.path.join(ROOT, r"assets\models")
WORK_DIR = os.path.join(ROOT, r"scratch\gen3d")
OUT_QC = os.path.join(ROOT, r"scratch\hd_face_final_qc.png")


def gltf_to_bl(v):
    return Vector((v[0], -v[2], v[1]))


def load_anchors():
    with open(os.path.join(WORK_DIR, "hd_anchors.json"), "r") as f:
        return json.load(f)


def setup_scene_cycles(res=720, samples=28):
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

    world = bpy.data.worlds.new("StudioWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.10, 0.105, 0.125, 1.0)
        bg.inputs["Strength"].default_value = 0.90

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

    # Clean studio portrait 3-point lighting
    add_area("KeyLight", (1.3, -2.1, 2.25), (64, 0, 28), 240, 1.6, (1.0, 0.95, 0.88))
    add_area("FillLight", (-1.8, -1.7, 1.6), (76, 0, -45), 115, 2.0, (0.80, 0.88, 1.0))
    add_area("RimLight", (-1.0, 1.9, 2.2), (118, 0, -150), 185, 1.3, (0.86, 0.92, 1.0))
    add_area("BackKey", (0.9, 2.1, 1.9), (112, 0, 158), 110, 1.4, (0.98, 0.94, 0.90))

    bpy.ops.mesh.primitive_plane_add(size=8.0, location=(0, 0, 0))
    fl = bpy.context.active_object
    fl.name = "StudioFloor"
    fm = bpy.data.materials.new("FloorMat")
    fm.use_nodes = True
    bsdf = fm.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (0.12, 0.12, 0.14, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.85
    fl.data.materials.append(fm)

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 85
    cam_obj = bpy.data.objects.new("Cam", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    scene.camera = cam_obj
    return scene, cam_obj


def look_at(cam_obj, loc, target, lens=85):
    cam_obj.data.lens = lens
    cam_obj.location = Vector(loc)
    direction = Vector(target) - Vector(loc)
    cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()


def import_glb_group(path):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    return [o for o in bpy.context.scene.objects if o not in before]


def remove_objs(objs):
    for o in objs:
        if o.name in bpy.data.objects:
            bpy.data.objects.remove(o, do_unlink=True)


def set_mat_tint(mat_name, rgba_linear):
    m = bpy.data.materials.get(mat_name)
    if not m or not m.use_nodes:
        return
    nt = m.node_tree
    bsdf = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if not bsdf:
        return
    inp = bsdf.inputs["Base Color"]
    if inp.is_linked:
        src_node = inp.links[0].from_node
        if src_node.type == 'MIX':
            for s in src_node.inputs:
                if s.type == 'RGBA' and not s.is_linked:
                    s.default_value = rgba_linear
                    return
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


def render_to_arr(scene, tmp_path, res=None):
    if res is not None:
        scene.render.resolution_x = res
        scene.render.resolution_y = res
    scene.render.filepath = tmp_path
    bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(tmp_path)
    w, h = im.size
    arr = np.array(im.pixels[:], dtype=np.float32).reshape((h, w, 4))[:, :, :3].copy()
    bpy.data.images.remove(im, do_unlink=True)
    return arr


def stitch_grid(panels, cols, rows, out_path):
    ph, pw, _ = panels[0].shape
    grid = np.zeros((rows * ph, cols * pw, 4), dtype=np.float32)
    grid[:, :, 3] = 1.0
    for idx, p in enumerate(panels):
        r = idx // cols
        c = idx % cols
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


def main():
    anchors = load_anchors()
    hat_bl = gltf_to_bl(anchors["HatAnchor"])
    eye_bl = gltf_to_bl(anchors["EyeAnchor"])
    print(f"[QC ANCHORS BL] hat={tuple(round(c,4) for c in hat_bl)} eye={tuple(round(c,4) for c in eye_bl)}")

    RES = 720
    scene, cam = setup_scene_cycles(res=RES, samples=26)
    tmp_png = os.path.join(WORK_DIR, "_tmp_hd_qc.png")
    panels = []

    hero_objs = import_glb_group(os.path.join(MODELS_DIR, "char_partner_90s.glb"))

    # Default calibrated values (for restoring between panels)
    SKIN_FAIR = (0.640, 0.378, 0.258, 1.0)
    SKIN_TANNED = (0.480, 0.250, 0.145, 1.0)     # (208,158,122) linear
    SKIN_DEEP = (0.200, 0.095, 0.052, 1.0)       # (112,74,52) linear
    IRIS_BROWN = (0.16, 0.075, 0.036, 1.0)
    IRIS_BLUE = (0.08, 0.34, 0.74, 1.0)
    IRIS_GREEN = (0.06, 0.38, 0.14, 1.0)
    CRIMSON_DEF = (0.360, 0.018, 0.028, 1.0)
    LAPEL_DEF = (0.215, 0.011, 0.018, 1.0)
    WHITE_SOCHI = (0.83, 0.78, 0.67, 1.0)
    WHITE_LAPEL = (0.62, 0.56, 0.47, 1.0)

    # PANEL 1: HUGE Front Face + Collar Close-Up (Default Fair Skin + Brown Eyes)
    set_mat_tint("skin", SKIN_FAIR)
    set_mat_tint("iris", IRIS_BROWN)
    look_at(cam, (0.0, -0.78, 1.565), (0.0, 0.0, 1.555), lens=95)
    panels.append(render_to_arr(scene, tmp_png, RES))

    # PANEL 2: 3/4 Face Close-Up (Tanned Skin + Vivid Blue Eyes)
    set_mat_tint("skin", SKIN_TANNED)
    set_mat_tint("iris", IRIS_BLUE)
    look_at(cam, (-0.34, -0.70, 1.585), (0.0, 0.0, 1.565), lens=95)
    panels.append(render_to_arr(scene, tmp_png, RES))

    # PANEL 3: 3/4 Face Close-Up (Deep Skin + Vivid Green Eyes)
    set_mat_tint("skin", SKIN_DEEP)
    set_mat_tint("iris", IRIS_GREEN)
    look_at(cam, (0.34, -0.70, 1.585), (0.0, 0.0, 1.565), lens=95)
    panels.append(render_to_arr(scene, tmp_png, RES))

    # PANEL 4: Side Profile Face + Ear + Hairline Close-Up (Fair Skin + Brown Eyes)
    set_mat_tint("skin", SKIN_FAIR)
    set_mat_tint("iris", IRIS_BROWN)
    look_at(cam, (-0.82, -0.10, 1.585), (0.0, 0.015, 1.580), lens=92)
    panels.append(render_to_arr(scene, tmp_png, RES))

    # PANEL 5: Full-Body White "Sochi-98" Blazer + Crown + Pixel Shades!
    set_mat_tint("crimson", WHITE_SOCHI)
    set_mat_tint("lapel", WHITE_LAPEL)
    acc_crown = import_glb_group(os.path.join(MODELS_DIR, "hat_crown.glb"))
    for o in acc_crown:
        if o.parent is None:
            o.location = hat_bl
    acc_px = import_glb_group(os.path.join(MODELS_DIR, "eye_dealwithit.glb"))
    for o in acc_px:
        if o.parent is None:
            o.location = eye_bl
    look_at(cam, (-1.25, -3.35, 0.96), (0.0, 0.0, 0.94), lens=64)
    panels.append(render_to_arr(scene, tmp_png, RES))
    remove_objs(acc_crown + acc_px)

    # PANEL 6: 2x2 Sub-Grid of Hats & Eyewear Fit (Crown, Ushanka, Aviators, Pixel Shades)
    set_mat_tint("crimson", CRIMSON_DEF)
    set_mat_tint("lapel", LAPEL_DEF)
    sub_res = RES // 2
    sub_panels = []
    fit_items = [
        ("hat_crown.glb", hat_bl, (-0.30, -0.96, 1.67), (0.0, 0.0, 1.65), 85),
        ("hat_ushanka.glb", hat_bl, (-0.32, -1.00, 1.66), (0.0, 0.0, 1.63), 82),
        ("eye_aviators.glb", eye_bl, (-0.26, -0.78, 1.60), (0.0, 0.0, 1.58), 92),
        ("eye_dealwithit.glb", eye_bl, (0.26, -0.78, 1.60), (0.0, 0.0, 1.58), 92),
    ]
    for fname, anchor, cloc, ctgt, clens in fit_items:
        acc = import_glb_group(os.path.join(MODELS_DIR, fname))
        for o in acc:
            if o.parent is None:
                o.location = anchor
        look_at(cam, cloc, ctgt, lens=clens)
        sub_panels.append(render_to_arr(scene, tmp_png, sub_res))
        remove_objs(acc)

    # Assemble 2x2 sub-grid into a RESxRES panel (top-left, top-right, bottom-left, bottom-right)
    p6 = np.zeros((RES, RES, 3), dtype=np.float32)
    p6[sub_res:, :sub_res, :] = sub_panels[0]   # top-left: Crown
    p6[sub_res:, sub_res:, :] = sub_panels[1]   # top-right: Ushanka
    p6[:sub_res, :sub_res, :] = sub_panels[2]   # bottom-left: Aviators
    p6[:sub_res, sub_res:, :] = sub_panels[3]   # bottom-right: Pixel Shades
    panels.append(p6)

    stitch_grid(panels, 3, 2, OUT_QC)


if __name__ == "__main__":
    main()
