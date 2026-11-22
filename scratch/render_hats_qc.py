import bpy, math, os
from mathutils import Vector

# Wait: run Blender rendering first, then stitch contact sheet with system Python PIL!
PROJ = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder"
MODELS = os.path.join(PROJ, "assets", "models")
OUT_TMP = os.path.join(PROJ, "scratch", "qc_tiles")
os.makedirs(OUT_TMP, exist_ok=True)

# Anchors in glTF Y-up:
#   HatAnchor = (0.0, 1.635, -0.007) -> Blender (0.0, +0.007, 1.635)
#   EyeAnchor = (0.0, 1.568,  0.098) -> Blender (0.0, -0.098, 1.568)
HAT_BLENDER = Vector((0.0, 0.007, 1.635))
EYE_BLENDER = Vector((0.0, -0.098, 1.568))

def setup_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 420
    scene.render.resolution_y = 420
    scene.render.image_settings.file_format = 'PNG'
    world = bpy.data.worlds.new("QCWorld")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.14, 0.16, 0.21, 1.0)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.85

    def add_sun(name, rot_deg, energy, color=(1,1,1)):
        ld = bpy.data.lights.new(name, 'SUN')
        ld.energy = energy
        ld.color = color
        lo = bpy.data.objects.new(name, ld)
        scene.collection.objects.link(lo)
        lo.rotation_euler = tuple(math.radians(a) for a in rot_deg)

    add_sun("Key", (52, 0, 32), 3.2, (1.0, 0.96, 0.90))
    add_sun("Fill", (65, 0, -135), 1.4, (0.80, 0.88, 1.0))
    add_sun("Rim", (45, 0, 165), 1.6, (1.0, 0.92, 0.82))

    # Import base partner
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS, "char_partner_90s.glb"))
    partner = bpy.data.objects["char_partner_90s"]

    cam_d = bpy.data.cameras.new("Cam")
    cam_d.lens = 58
    cam = bpy.data.objects.new("Cam", cam_d)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return scene, partner, cam

def point_cam(cam, loc, target):
    cam.location = Vector(loc)
    d = Vector(target) - cam.location
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()

def import_accessory(glb_name, anchor_blender):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS, glb_name))
    added = [o for o in bpy.data.objects if o not in before]
    for o in added:
        if o.parent is None:
            o.location = anchor_blender
    return added

def remove_objs(objs):
    for o in objs:
        bpy.data.objects.remove(o, do_unlink=True)

scene, partner, cam = setup_scene()

# Render all 8 hats in 3/4 hero view AND side check view!
hats = [
    ("hat_tinfoil.glb", "tinfoil"),
    ("hat_ushanka.glb", "ushanka"),
    ("hat_crown.glb", "crown"),
    ("hat_traffic_cone.glb", "traffic_cone"),
    ("hat_cowboy.glb", "cowboy"),
    ("hat_chef.glb", "chef"),
    ("hat_pirate.glb", "pirate"),
    ("hat_frog_head.glb", "frog_head"),
]

# Pair 4 hats with 4 eyewear on some shots, AND render dedicated close-ups of all 4 eyewear!
for glb, tag in hats:
    objs = import_accessory(glb, HAT_BLENDER)
    # 3/4 front-right view (classic wardrobe angle!)
    point_cam(cam, (0.38, -0.78, 1.72), (0.0, 0.0, 1.62))
    scene.render.filepath = os.path.join(OUT_TMP, f"hat_{tag}_34.png")
    bpy.ops.render.render(write_still=True)

    # Profile side view (verifies zero floating & zero hair clipping!)
    point_cam(cam, (0.82, -0.10, 1.66), (0.0, 0.0, 1.64))
    scene.render.filepath = os.path.join(OUT_TMP, f"hat_{tag}_side.png")
    bpy.ops.render.render(write_still=True)
    remove_objs(objs)

# Now hide built-in 'glass' faces on partner for eyewear close-up QC (just like Wardrobe UI will!)
# Wait — let's actually make the built-in 'glass' material transparent (alpha=0) so we see iris + new eyewear cleanly,
# AND let's also render one combo shot WITH built-in untouched just to inspect!
if "glass" in bpy.data.materials:
    gm = bpy.data.materials["glass"]
    gm.use_nodes = True
    bsdf = gm.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Alpha"].default_value = 0.0
    if hasattr(gm, "blend_method"):
        gm.blend_method = 'BLEND'
    if hasattr(gm, "surface_render_method"):
        gm.surface_render_method = 'BLENDED'

eyes = [
    ("eye_aviators.glb", "aviators"),
    ("eye_matrix.glb", "matrix"),
    ("eye_accountant.glb", "accountant"),
    ("eye_dealwithit.glb", "dealwithit"),
]

for glb, tag in eyes:
    objs = import_accessory(glb, EYE_BLENDER)
    # Close-up 3/4 front view
    point_cam(cam, (0.18, -0.46, 1.59), (0.0, -0.04, 1.565))
    scene.render.filepath = os.path.join(OUT_TMP, f"eye_{tag}_34.png")
    bpy.ops.render.render(write_still=True)

    # Close-up side view (verify temples over ears & zero sideburn clipping!)
    point_cam(cam, (0.50, -0.08, 1.575), (0.0, -0.02, 1.565))
    scene.render.filepath = os.path.join(OUT_TMP, f"eye_{tag}_side.png")
    bpy.ops.render.render(write_still=True)
    remove_objs(objs)

print("QC_RENDERS_COMPLETE")
