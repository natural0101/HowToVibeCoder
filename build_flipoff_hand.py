"""Generates assets/models/fps_hands_flipoff.glb — anatomical 5-fingered 1st-person middle-finger emote hand
with a tight natural closed fist, fingertips tucked cleanly into the palm crease, smooth convex thumb resting
on the curled index finger, slight 3/4 dynamic wrist presentation, and a chunky 1990s gold-and-onyx signet ring."""
import math
import os
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import bmesh
from mathutils import Vector as V, Matrix
import build_reload_hands as brh


def build_flipoff_right_pose():
    side = "r"
    j = lambda name: brh.JOINT[f"{side}-{name}"]
    wrist_rest = j("hand")

    # Right hand raised boldly in dynamic 3/4 first-person pose:
    # - Middle finger rises proudly up and slightly pitched forward toward target
    # - Wrist rolls ~28 deg inward so we see solid fist volume & sculpted knuckles instead of a flat slab palm!
    dest_wrist = V((-0.095, -0.155, 0.560))
    dest_elbow = V((-0.265, -0.455, 0.220))
    f_dir = V((-0.06, 0.94, 0.34)).normalized()
    # Palm faces back-and-inward toward player (3/4 angle, natural wrist ergonomics!)
    palm_dir = V((0.46, 0.24, -0.85)).normalized()
    hand = brh.solve_hand_frame(side, f_target=f_dir, palm_target=palm_dir)

    transforms = {}
    brh.add_forearm_transforms(transforms, side, dest_wrist, dest_elbow, hand)

    mcp = {
        digit: dest_wrist + hand @ (j(f"finger-{d}-1") - wrist_rest)
        for d, digit in enumerate(brh.DIGITS, 1)
    }

    # Exact local orthonormal palm basis:
    # - up_v:     wrist -> middle MCP (along fingers)
    # - across_v: pinky MCP -> index MCP (points toward thumb/index side)
    # - palm_v:   points OUT of the palm toward player/thumb
    up_v = (hand @ (j("finger-3-1") - wrist_rest)).normalized()
    across_v = (mcp["index"] - mcp["pinky"]).normalized()
    palm_v = across_v.cross(up_v).normalized()
    if palm_v.dot(palm_dir) < 0:
        palm_v = -palm_v

    def chain_offsets(digit_name, d_idx, pts_local):
        origin = mcp[digit_name]
        targets = [origin + across_v * ox + up_v * oy + palm_v * oz for (ox, oy, oz) in pts_local]
        tip = brh.build_digit_chain(transforms, side, d_idx, digit_name, hand, origin, targets)
        return origin, targets, tip

    # MIDDLE FINGER (3): Proudly extended straight up with tiny natural anatomical forward arc!
    chain_offsets("middle", 3, [
        (0.000, 0.044, -0.003),
        (0.000, 0.072, -0.005),
        (0.000, 0.096, -0.006),
    ])

    # INDEX FINGER (2): Tightly clenched into a solid fist!
    # Proximal pulls INWARD toward middle knuckle (-across_v) and forward+down so ZERO gap or arch sticks out!
    # Middle drops sharply DOWN into the palm; fingertip buries deep inside the palm crease (nails hidden down/in!)
    chain_offsets("index", 2, [
        (-0.011,  0.004, 0.030),
        (-0.012, -0.024, 0.024),
        (-0.010, -0.036, 0.004),
    ])

    # RING FINGER (4): Tightly clenched deep into the palm crease, pressed flush beside middle knuckle!
    ring_origin, ring_targets, _ = chain_offsets("ring", 4, [
        (0.004,  0.006, 0.030),
        (0.005, -0.022, 0.024),
        (0.004, -0.034, 0.004),
    ])

    # PINKY FINGER (5): Tightly clenched parallel to ring finger, tucked snugly into the fist!
    chain_offsets("pinky", 5, [
        (0.009,  0.004, 0.027),
        (0.010, -0.020, 0.021),
        (0.008, -0.030, 0.003),
    ])

    # THUMB (1): Smooth, natural anatomical arc pressing snugly right across the curled index/middle knuckles!
    chain_offsets("thumb", 1, [
        (-0.012, 0.024, 0.026),
        (-0.024, 0.033, 0.038),
        (-0.036, 0.031, 0.039),
    ])

    # Return ring proximal pose info so we can slip a chunky 1990s gold signet ring cleanly onto the ring finger!
    ring_p1 = transforms["ring_01_r"]
    return transforms, (ring_origin, ring_targets[0], up_v, across_v, palm_v, ring_p1)


def build_signet_ring(ring_info):
    """Sculpts a chunky 1990s Russian businessman solid-gold & black-onyx signet ring
    mounted snugly around the proximal phalanx of the curled ring finger."""
    ring_origin, ring_t0, up_v, across_v, palm_v, _ = ring_info
    axis = (ring_t0 - ring_origin).normalized()
    center = ring_origin + axis * 0.0155

    # Local basis with Z along finger bone axis, X facing outward/up on knuckle top
    ref = (up_v - axis * up_v.dot(axis))
    if ref.length < 1e-4:
        ref = palm_v
    x_dir = ref.normalized()
    y_dir = axis.cross(x_dir).normalized()
    basis = Matrix((x_dir, y_dir, axis)).transposed()

    parts = []
    # Gold band (thick beveled torus ring around the finger)
    bm = bmesh.new()
    segs, ring_segs = 24, 10
    R = 0.0104  # snug finger radius
    r_x, r_z = 0.0024, 0.0038
    verts = []
    for i in range(segs):
        a = i * math.tau / segs
        ca, sa = math.cos(a), math.sin(a)
        # Slightly thicker bezel shoulder on the knuckle-top side (ca > 0)
        bulge = 1.0 + 0.55 * max(0.0, ca) ** 2
        row = []
        for k in range(ring_segs):
            b = k * math.tau / ring_segs
            rad = R + r_x * bulge * math.cos(b)
            z = r_z * (1.0 + 0.35 * max(0.0, ca) ** 2) * math.sin(b)
            local = V((rad * ca, rad * sa, z))
            world = center + basis @ local
            row.append(bm.verts.new(brh.xyz(world)))
        verts.append(row)
    for i in range(segs):
        ni = (i + 1) % segs
        for k in range(ring_segs):
            nk = (k + 1) % ring_segs
            bm.faces.new((verts[i][k], verts[ni][k], verts[ni][nk], verts[i][nk]))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    band_mesh = bpy.data.meshes.new("signet_gold")
    bm.to_mesh(band_mesh)
    bm.free()
    band_obj = bpy.data.objects.new("signet_gold", band_mesh)
    bpy.context.collection.objects.link(band_obj)
    band_obj.data.materials.append(brh.mat("gold"))
    parts.append(band_obj)

    # Recessed black-onyx flat tablet face on the top of the signet ring!
    bm2 = bmesh.new()
    bmesh.ops.create_cube(bm2, size=1.0)
    for v in bm2.verts:
        local = V((R + 0.0036 + v.co.x * 0.0018, v.co.y * 0.0082, v.co.z * 0.0088))
        v.co = V(brh.xyz(center + basis @ local))
    bmesh.ops.recalc_face_normals(bm2, faces=list(bm2.faces))
    stone_mesh = bpy.data.meshes.new("signet_onyx")
    bm2.to_mesh(stone_mesh)
    bm2.free()
    stone_obj = bpy.data.objects.new("signet_onyx", stone_mesh)
    bpy.context.collection.objects.link(stone_obj)
    stone_obj.data.materials.append(brh.mat("onyx"))
    parts.append(stone_obj)

    return parts


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    brh.MATS.clear()

    # Valid parked left arm (unused during emote, satisfies VisualEngine3D ReadMeshGroups contract)
    left_transforms = brh.reload_left_pose("fps_left_mag")
    right_transforms, ring_info = build_flipoff_right_pose()
    all_transforms = {}
    all_transforms.update(left_transforms)
    all_transforms.update(right_transforms)

    left_obj = brh.build_arm_piece("l", all_transforms)
    right_obj = brh.build_arm_piece("r", all_transforms)
    ring_parts = build_signet_ring(ring_info)
    right_obj = brh.join([right_obj, *ring_parts], "Right")

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
