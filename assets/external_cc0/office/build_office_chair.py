"""Build standalone high-craft ergonomic mesh office chair GLB (`assets/models/prop_office_chair.glb`).
Human scale: seat cushion top at 0.46m, armrests at 0.65m, backrest top at 1.03m, front faces Blender -Y (glTF +Z).
Includes 5-star caster base, twin wheels, pneumatic column, waterfall seat cushion, curved ergonomic mesh backrest,
lumbar support bar, 3D armrests, and 16-ray BVHTree AO baked into COLOR_0.
"""
import math
import os
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler
from mathutils.bvhtree import BVHTree

OUT_GLB = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\assets\models\prop_office_chair.glb"


def srgb_to_lin(c):
    return tuple(pow(max(0.0, min(1.0, v)), 2.2) for v in c)


def make_mat(name, srgb, rough=0.5, met=0.0):
    m = bpy.data.materials.new(name)
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
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return m


def add_box(bm, center, size, mat_idx, bevel=0.008, segs=2, rot=None, smooth=True):
    ret = bmesh.ops.create_cube(bm, size=1.0)
    verts = ret["verts"]
    bmesh.ops.transform(bm, matrix=Matrix.Diagonal((size[0], size[1], size[2], 1.0)), verts=verts)
    if bevel > 0:
        edges = list({e for v in verts for e in v.link_edges})
        bev = bmesh.ops.bevel(bm, geom=edges, offset=bevel, offset_type='OFFSET',
                              segments=segs, profile=0.5, affect='EDGES', clamp_overlap=True)
        verts = [v for v in ({v for f in bev["faces"] for v in f.verts} | set(verts)) if v.is_valid]
    if rot is not None:
        bmesh.ops.transform(bm, matrix=rot.to_matrix().to_4x4(), verts=verts)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center), verts=verts)
    for v in verts:
        for f in v.link_faces:
            f.material_index = mat_idx
            f.smooth = smooth


def add_cyl(bm, center, r1, r2, depth, mat_idx, segs=16, rot=None, smooth=True):
    ret = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs,
                                radius1=r1, radius2=r2, depth=depth)
    verts = ret["verts"]
    mat = Matrix.Identity(4) if rot is None else rot.to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ mat, verts=verts)
    for v in verts:
        for f in v.link_faces:
            f.material_index = mat_idx
            f.smooth = smooth


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bm = bmesh.new()

    m_chrome = 0
    m_frame = 1
    m_fabric = 2
    m_mesh = 3

    mats = [
        make_mat("chair_chrome", (0.76, 0.78, 0.82), rough=0.22, met=0.90),
        make_mat("chair_frame",  (0.14, 0.15, 0.18), rough=0.45, met=0.25),
        make_mat("chair_fabric", (0.18, 0.20, 0.24), rough=0.82, met=0.00),
        make_mat("chair_mesh",   (0.24, 0.26, 0.30), rough=0.76, met=0.05),
    ]

    # 1. 5-star rolling base & hooded twin casters (bottom strictly at Z=0.0)
    add_cyl(bm, (0, 0, 0.115), 0.044, 0.038, 0.085, m_frame, 18)
    for i in range(5):
        a = i * math.tau / 5.0 + math.radians(18)
        ca, sa = math.cos(a), math.sin(a)
        leg_mid = (ca * 0.145, sa * 0.145, 0.082)
        add_box(bm, leg_mid, (0.275, 0.034, 0.026), m_chrome, bevel=0.006, segs=1,
                rot=Euler((0, math.radians(8), a)))
        # Caster stem + wheel resting at Z=0.0
        wx, wy = ca * 0.265, sa * 0.265
        add_cyl(bm, (wx, wy, 0.052), 0.011, 0.011, 0.036, m_frame, 10)
        add_cyl(bm, (wx, wy, 0.025), 0.025, 0.025, 0.038, m_frame, 14,
                rot=Euler((math.radians(90), 0, a)))

    # 2. Pneumatic gas lift cylinder + under-seat tilt mechanism
    add_cyl(bm, (0, 0, 0.265), 0.024, 0.024, 0.275, m_chrome, 16)
    add_cyl(bm, (0, 0, 0.195), 0.032, 0.030, 0.130, m_frame, 16)
    add_box(bm, (0, 0.02, 0.405), (0.19, 0.24, 0.048), m_frame, bevel=0.008, segs=1)
    # Height adjustment lever
    add_box(bm, (0.14, -0.02, 0.398), (0.14, 0.014, 0.010), m_frame, bevel=0.003, segs=1)

    # 3. Contoured waterfall seat cushion (top at Z=0.465)
    add_box(bm, (0, 0.0, 0.442), (0.48, 0.46, 0.068), m_fabric, bevel=0.024, segs=3)
    add_box(bm, (0, 0.01, 0.412), (0.45, 0.43, 0.024), m_frame, bevel=0.008, segs=1)

    # 4. Curved ergonomic spine + breathable mesh backrest (back at +Y, facing -Y)
    add_box(bm, (0, 0.24, 0.56), (0.068, 0.036, 0.32), m_frame, bevel=0.008, segs=1,
            rot=Euler((math.radians(-12), 0, 0)))
    # Outer curved backrest frame + mesh center
    add_box(bm, (0, 0.245, 0.775), (0.45, 0.038, 0.50), m_frame, bevel=0.014, segs=2,
            rot=Euler((math.radians(-8), 0, 0)))
    add_box(bm, (0, 0.236, 0.775), (0.38, 0.028, 0.43), m_mesh, bevel=0.010, segs=2,
            rot=Euler((math.radians(-8), 0, 0)))
    # Sculpted lumbar support pad
    add_box(bm, (0, 0.222, 0.645), (0.34, 0.032, 0.075), m_fabric, bevel=0.012, segs=2)
    # Compact modern headrest
    add_box(bm, (0, 0.268, 1.045), (0.24, 0.044, 0.115), m_fabric, bevel=0.016, segs=2,
            rot=Euler((math.radians(10), 0, 0)))
    add_box(bm, (0, 0.278, 0.990), (0.065, 0.022, 0.085), m_frame, bevel=0.004, segs=1)

    # 5. Sleek T-armrests with soft pads
    for s in (-1, 1):
        add_box(bm, (s * 0.255, 0.01, 0.535), (0.028, 0.038, 0.22), m_frame, bevel=0.006, segs=1)
        add_box(bm, (s * 0.258, -0.01, 0.650), (0.068, 0.255, 0.026), m_frame, bevel=0.009, segs=2)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("OfficeChairMesh")
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new("OfficeChair", mesh)
    bpy.context.collection.objects.link(obj)
    for m in mats:
        mesh.materials.append(m)

    # 16-ray BVHTree AO
    bm_ao = bmesh.new()
    bm_ao.from_mesh(mesh)
    bm_ao.verts.ensure_lookup_table()
    bvh = BVHTree.FromBMesh(bm_ao)
    dirs = []
    n_rays = 16
    phi = math.pi * (3.0 - math.sqrt(5.0))
    for i in range(n_rays):
        y = 1.0 - (i / float(n_rays - 1)) * 0.85
        r = math.sqrt(max(0.0, 1.0 - y * y))
        th = phi * i
        dirs.append(Vector((math.cos(th) * r, y, math.sin(th) * r)).normalized())
    vert_ao = []
    for v in bm_ao.verts:
        n = v.normal.normalized() if v.normal.length > 1e-6 else Vector((0, 0, 1))
        up = Vector((0, 0, 1)) if abs(n.z) < 0.9 else Vector((1, 0, 0))
        tx = n.cross(up).normalized()
        ty = n.cross(tx).normalized()
        occ = 0.0
        orig = v.co + n * 0.0025
        for d in dirs:
            ray = (tx * d.x + n * d.y + ty * d.z).normalized()
            hit = bvh.ray_cast(orig, ray, 0.25)
            if hit[0] is not None:
                occ += 1.0 - (hit[3] / 0.25)
        vert_ao.append(max(0.55, min(1.0, 1.0 - (occ / n_rays) * 0.50)))
    bm_ao.free()

    col_attr = mesh.color_attributes.new(name="COLOR_0", type='BYTE_COLOR', domain='CORNER')
    for poly in mesh.polygons:
        for li in poly.loop_indices:
            ao = vert_ao[mesh.loops[li].vertex_index]
            col_attr.data[li].color = (ao, ao, ao, 1.0)

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(
        filepath=OUT_GLB,
        export_format='GLB',
        use_selection=True,
        export_yup=True,
        export_apply=True,
        export_attributes=True,
    )
    print(f"EXPORTED {OUT_GLB} ({len(mesh.polygons)} polys)")


if __name__ == "__main__":
    main()
