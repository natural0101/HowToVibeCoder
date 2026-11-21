"""Generates assets/models/fps_hands_flipoff.glb — anatomical 5-fingered 1st-person middle-finger emote hand
matching the exact MPFB2 topology, HD skin, 3D nail plates, poplin cuff, and crimson 90s blazer sleeve."""
import math, os, sys
import shutil
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import bmesh
from mathutils import Vector as V, Matrix
import build_reload_hands as brh

def build_flipoff_right_pose():
    side = "r"
    j = lambda name: brh.JOINT[f"{side}-{name}"]
    wrist_rest = j("hand")

    # Right hand raised proudly in center-right view: fingers point UP (+Y, slight +Z toward target),
    # palm faces BACK toward player (-Z) so the raised middle finger is unmistakable in 1st-person!
    dest_wrist = V((-0.075, -0.135, 0.565))
    dest_elbow = V((-0.245, -0.435, 0.235))
    f_dir = V((-0.04, 0.96, 0.28)).normalized()
    palm_dir = V((0.06, 0.28, -0.96)).normalized()
    hand = brh.solve_hand_frame(side, f_target=f_dir, palm_target=palm_dir)

    transforms = {}
    brh.add_forearm_transforms(transforms, side, dest_wrist, dest_elbow, hand)

    mcp = {
        digit: dest_wrist + hand @ (j(f"finger-{d}-1") - wrist_rest)
        for d, digit in enumerate(brh.DIGITS, 1)
    }

    # Reconstruct exact local orthonormal frame of the posed palm!
    up_v = (hand @ (j("finger-3-1") - wrist_rest)).normalized()
    # Palm normal pointing back toward camera (-Z)
    across_v = (mcp["index"] - mcp["pinky"]).normalized()
    palm_v = across_v.cross(up_v).normalized()
    if palm_v.z > 0:
        palm_v = -palm_v

    def chain_offsets(digit_name, d_idx, pts_local):
        origin = mcp[digit_name]
        targets = [origin + across_v * ox + up_v * oy + palm_v * oz for (ox, oy, oz) in pts_local]
        brh.build_digit_chain(transforms, side, d_idx, digit_name, hand, origin, targets)

    # MIDDLE FINGER (3): Proudly extended straight up!
    chain_offsets("middle", 3, [
        (0.000, 0.040, -0.002),
        (0.000, 0.068, -0.003),
        (0.000, 0.094, -0.002),
    ])
    # INDEX FINGER (2): Tightly curled straight down into the palm (zero side flare!)
    chain_offsets("index", 2, [
        (-0.002,  0.006, 0.032),
        (-0.005, -0.014, 0.038),
        (-0.007, -0.022, 0.016),
    ])
    # RING FINGER (4): Tightly curled into the palm!
    chain_offsets("ring", 4, [
        (-0.003,  0.020, 0.026),
        (-0.002, -0.005, 0.036),
        (-0.001, -0.017, 0.016),
    ])
    # PINKY FINGER (5): Tightly curled inward into the palm (zero splay!)
    chain_offsets("pinky", 5, [
        (0.008,  0.012, 0.028),
        (0.006, -0.008, 0.036),
        (0.004, -0.018, 0.014),
    ])
    # THUMB (1): Tucked snugly across the curled index/middle knuckles!
    chain_offsets("thumb", 1, [
        (-0.014, 0.028, 0.026),
        (-0.034, 0.036, 0.040),
        (-0.046, 0.038, 0.044),
    ])
    return transforms


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    brh.MATS.clear()

    # Valid parked left arm (unused during emote, satisfies VisualEngine3D ReadMeshGroups contract)
    left_transforms = brh.reload_left_pose("fps_left_mag")
    right_transforms = build_flipoff_right_pose()
    all_transforms = {}
    all_transforms.update(left_transforms)
    all_transforms.update(right_transforms)

    left_obj = brh.build_arm_piece("l", all_transforms)
    right_obj = brh.build_arm_piece("r", all_transforms)

    for obj in (left_obj, right_obj):
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.0000001)
        bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=.0000001)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()
        for poly in obj.data.polygons:
            poly.use_smooth = True

    out_path = brh.OUT / "fps_hands_flipoff.glb"
    bpy.ops.export_scene.gltf(
        filepath=str(out_path),
        export_format="GLB",
        export_vertex_color="ACTIVE",
        export_normals=True,
        export_texcoords=True,
        export_animations=False,
        export_cameras=False,
        export_lights=False,
        export_extras=True,
    )
    for sub in ("bin/Release/net10.0/assets/models", "bin/Debug/net10.0/assets/models"):
        dest_dir = brh.ROOT / sub
        if dest_dir.exists():
            shutil.copy2(out_path, dest_dir / "fps_hands_flipoff.glb")
    print("EXPORTED", out_path)


if __name__ == "__main__":
    main()
