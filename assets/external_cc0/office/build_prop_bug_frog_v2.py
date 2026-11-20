"""Rebuild prop_bug_frog.glb as a cute, compact (~21cm) 1998 wind-up mechanical Jabba-Frog.
Matches our Archer x Adult Swim comic frogs:
- Plump rounded frog shell (Material 0 = mat_frog_shell, neutral light satin -> tinted per species in C#!)
- Warm cream segmented belly plate (Material 1 = mat_frog_belly, NOT tinted!)
- Big perched expressive eyes: bright white sclera (Material 2), glossy black pupils + highlight (Material 3)
- Compact tucked folded mechanical legs & neat webbed feet resting flat on Y=0 (NO giant crab legs!)
- Classic 1998 antique brass wind-up key on back (Material 4) + dark gunmetal joint pins (Material 5)
- Recessed emerald PCB saddle plate on back (Material 6) + tiny amber LED dome (Material 7)
- Calibrated Raylib linear PBR (x**2.2) + 24-ray BVHTree AO in COLOR_0 + verified outward normals!
"""
import math
import os
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler
from mathutils.bvhtree import BVHTree

OUT_GLB = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\assets\models\prop_bug_frog.glb"
PROOF_PNG = r"C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a\frog_v2_blender_proof.png"


def srgb_to_lin(c):
    return tuple(pow(max(0.0, min(1.0, v)), 2.2) for v in c)


def make_mat(name, srgb, roughness=0.55, metallic=0.0, emission=None, emission_strength=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.use_backface_culling = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    lin = srgb_to_lin(srgb)
    # Wire COLOR_0 into Base Color via Multiply so Blender exports our 24-ray BVHTree AO attribute!
    vc = nt.nodes.new("ShaderNodeVertexColor")
    vc.layer_name = "COLOR_0"
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.inputs["Factor"].default_value = 1.0
    mix.inputs["A"].default_value = (*lin, 1.0)
    nt.links.new(vc.outputs["Color"], mix.inputs["B"])
    nt.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if emission is not None and emission_strength > 0:
        elin = srgb_to_lin(emission)
        bsdf.inputs["Emission Color"].default_value = (*elin, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return m


def add_sphere(bm, center, radii, u_seg=20, v_seg=14, mat_idx=0, rot=None):
    ret = bmesh.ops.create_uvsphere(bm, u_segments=u_seg, v_segments=v_seg, radius=1.0)
    verts = ret["verts"]
    mat = Matrix.Diagonal((radii[0], radii[1], radii[2], 1.0))
    if rot is not None:
        mat = rot.to_matrix().to_4x4() @ mat
    mat = Matrix.Translation(center) @ mat
    bmesh.ops.transform(bm, matrix=mat, verts=verts)
    for v in verts:
        for f in v.link_faces:
            f.material_index = mat_idx
            f.smooth = True
    return verts


def add_cyl(bm, center, r1, r2, depth, segs=16, mat_idx=0, rot=None):
    ret = bmesh.ops.create_cone(
        bm, cap_ends=True, cap_tris=False, segments=segs,
        radius1=r1, radius2=r2, depth=depth
    )
    verts = ret["verts"]
    mat = Matrix.Identity(4)
    if rot is not None:
        mat = rot.to_matrix().to_4x4()
    mat = Matrix.Translation(center) @ mat
    bmesh.ops.transform(bm, matrix=mat, verts=verts)
    for v in verts:
        for f in v.link_faces:
            f.material_index = mat_idx
            f.smooth = True
    return verts


def add_torus_loop(bm, center, major_r, minor_r, maj_seg=22, min_seg=10, mat_idx=0, rot=None):
    """Build a closed smooth ring (for the classic brass wind-up key loops)."""
    rings = []
    for i in range(maj_seg):
        theta = 2.0 * math.pi * i / maj_seg
        ct, st = math.cos(theta), math.sin(theta)
        ring = []
        for j in range(min_seg):
            phi = 2.0 * math.pi * j / min_seg
            cp, sp = math.cos(phi), math.sin(phi)
            r = major_r + minor_r * cp
            p = Vector((r * ct, minor_r * sp, r * st))
            ring.append(bm.verts.new(p))
        rings.append(ring)
    new_verts = [v for ring in rings for v in ring]
    for i in range(maj_seg):
        r0 = rings[i]
        r1 = rings[(i + 1) % maj_seg]
        for j in range(min_seg):
            jn = (j + 1) % min_seg
            f = bm.faces.new((r0[j], r1[j], r1[jn], r0[jn]))
            f.material_index = mat_idx
            f.smooth = True
    mat = Matrix.Identity(4)
    if rot is not None:
        mat = rot.to_matrix().to_4x4()
    mat = Matrix.Translation(center) @ mat
    bmesh.ops.transform(bm, matrix=mat, verts=new_verts)
    return new_verts


def build_frog():
    bpy.ops.wm.read_factory_settings(use_empty=True)

    # IMPORTANT: Material 0 is mat_frog_shell -> ONLY Material 0 is tinted by Species.HoodieColor in Raylib!
    # Keep Material 0 neutral bright satin so green/cyan/red/gold species colors look vibrant & rich!
    mat_shell = make_mat("mat_frog_shell", (0.90, 0.90, 0.90), roughness=0.42, metallic=0.08)
    mat_belly = make_mat("mat_frog_belly", (0.92, 0.90, 0.78), roughness=0.58, metallic=0.04)
    mat_eye_w = make_mat("mat_frog_eye_white", (0.96, 0.96, 0.95), roughness=0.18, metallic=0.0)
    mat_pupil = make_mat("mat_frog_pupil", (0.04, 0.04, 0.05), roughness=0.08, metallic=0.05)
    mat_brass = make_mat("mat_frog_brass_key", (0.88, 0.68, 0.22), roughness=0.28, metallic=0.82)
    mat_metal = make_mat("mat_frog_metal", (0.18, 0.20, 0.23), roughness=0.38, metallic=0.72)
    mat_pcb   = make_mat("mat_frog_pcb", (0.09, 0.34, 0.19), roughness=0.45, metallic=0.15)
    mat_led   = make_mat("mat_frog_led", (1.00, 0.78, 0.18), roughness=0.15, metallic=0.0,
                         emission=(1.0, 0.76, 0.15), emission_strength=2.2)

    mats = [mat_shell, mat_belly, mat_eye_w, mat_pupil, mat_brass, mat_metal, mat_pcb, mat_led]

    # Note: In Blender Z is Up, +Y is Forward (glTF exporter converts Blender +Z->+Y, +Y->-Z or +Z depending on yaw;
    # let's check original frog facing: in C# DrawFrog(p, yaw), +Z is forward! In Blender glTF with +Y up,
    # Blender -Y maps to glTF +Z! Let's verify so the frog faces forward (+Z in glTF, i.e., -Y in Blender!).
    bm = bmesh.new()

    # Compact palm-sized proportions: ~20.5cm wide, ~21.5cm long, ~16.5cm tall (with brass key)
    # Facing -Y in Blender => +Z in glTF/Raylib!

    # 1. Plump cute rounded frog torso (slightly tilted nose-up like a ready-to-hop wind-up toy!)
    torso_rot = Euler((math.radians(16), 0, 0), 'XYZ')
    add_sphere(bm, Vector((0.0, 0.004, 0.062)), (0.072, 0.088, 0.052), u_seg=26, v_seg=18, mat_idx=0, rot=torso_rot)

    # Rounded front snout / wide cute frog jaw
    add_sphere(bm, Vector((0.0, -0.058, 0.066)), (0.064, 0.048, 0.036), u_seg=24, v_seg=16, mat_idx=0, rot=Euler((math.radians(6), 0, 0), 'XYZ'))

    # Cute recessed smiling mouth seam (tucked snugly along snout contour — NOT sticking out!)
    add_sphere(bm, Vector((0.0, -0.087, 0.052)), (0.044, 0.014, 0.0038), u_seg=18, v_seg=10, mat_idx=5)

    # Soft warm-cream belly plate underneath (keeps frog from looking monochrome!)
    add_sphere(bm, Vector((0.0, -0.012, 0.041)), (0.058, 0.074, 0.031), u_seg=22, v_seg=14, mat_idx=1, rot=Euler((math.radians(12), 0, 0), 'XYZ'))
    # Chin / throat cream puff
    add_sphere(bm, Vector((0.0, -0.066, 0.045)), (0.047, 0.034, 0.022), u_seg=18, v_seg=12, mat_idx=1)

    # 2. Big adorable perched frog eyes (bright white sclera + glossy black pupils + tucked catchlight!)
    for sign in (-1.0, 1.0):
        ex = 0.037 * sign
        ey = -0.058
        ez = 0.104
        # Colored frog eye dome brow housing (clean smooth cup behind eyeball)
        add_sphere(bm, Vector((ex, ey + 0.008, ez + 0.002)), (0.0245, 0.023, 0.0245), u_seg=20, v_seg=14, mat_idx=0)
        # Crisp bright WHITE eyeball protruding smoothly from housing
        add_sphere(bm, Vector((ex + 0.001 * sign, ey - 0.006, ez)), (0.021, 0.019, 0.021), u_seg=20, v_seg=14, mat_idx=2)
        # Big glossy JET-BLACK pupil sitting cleanly on front of eyeball
        add_sphere(bm, Vector((ex + 0.003 * sign, ey - 0.0205, ez + 0.0015)), (0.0115, 0.0065, 0.0115), u_seg=16, v_seg=10, mat_idx=3)
        # Flush white catchlight dot nestled cleanly inside pupil surface
        add_sphere(bm, Vector((ex + 0.0065 * sign, ey - 0.0252, ez + 0.0055)), (0.0034, 0.0018, 0.0034), u_seg=10, v_seg=8, mat_idx=2)

    # 3. Compact tucked mechanical hind legs + neat front arms (ZERO awkward splayed crab legs!)
    for sign in (-1.0, 1.0):
        # Plump tucked hind thigh hugging the flank
        add_sphere(bm, Vector((0.068 * sign, 0.036, 0.042)), (0.026, 0.044, 0.030), u_seg=16, v_seg=12, mat_idx=0,
                   rot=Euler((math.radians(-18), math.radians(10 * sign), math.radians(-14 * sign)), 'XYZ'))
        # Dark steel mechanical hip bolt pin on side of thigh
        add_cyl(bm, Vector((0.091 * sign, 0.038, 0.044)), 0.010, 0.010, 0.009, segs=14, mat_idx=5, rot=Euler((0, math.radians(90), 0), 'XYZ'))
        # Brass screw cap inside hip joint
        add_cyl(bm, Vector((0.095 * sign, 0.038, 0.044)), 0.006, 0.006, 0.004, segs=12, mat_idx=4, rot=Euler((0, math.radians(90), 0), 'XYZ'))

        # Tucked lower calf folded forward toward foot
        add_cyl(bm, Vector((0.074 * sign, 0.028, 0.020)), 0.011, 0.013, 0.048, segs=12, mat_idx=5,
                rot=Euler((math.radians(78), 0, math.radians(-10 * sign)), 'XYZ'))

        # Compact flat hind webbed foot resting cleanly on Z=0
        add_sphere(bm, Vector((0.078 * sign, 0.004, 0.009)), (0.020, 0.030, 0.009), u_seg=14, v_seg=8, mat_idx=0,
                   rot=Euler((0, 0, math.radians(-18 * sign)), 'XYZ'))
        # Three tiny cute rounded toes on hind foot
        for t_ang, t_off in [(-0.22, -0.012), (0.0, 0.0), (0.22, 0.012)]:
            add_sphere(bm, Vector((0.078 * sign + t_off * sign, -0.022, 0.007)), (0.0065, 0.012, 0.0065), u_seg=8, v_seg=6, mat_idx=0)

        # Neat rounded front arm planted squarely into front paw
        add_sphere(bm, Vector((0.054 * sign, -0.056, 0.027)), (0.013, 0.015, 0.024), u_seg=14, v_seg=10, mat_idx=0,
                   rot=Euler((math.radians(-18), math.radians(-12 * sign), 0), 'XYZ'))
        # Little shoulder mechanical pin
        add_sphere(bm, Vector((0.061 * sign, -0.048, 0.044)), (0.0085, 0.0085, 0.0085), u_seg=10, v_seg=8, mat_idx=5)
        # Cute front paw resting cleanly on Z=0
        add_sphere(bm, Vector((0.050 * sign, -0.068, 0.008)), (0.016, 0.020, 0.008), u_seg=12, v_seg=8, mat_idx=0)

    # 4. Recessed 1998 Emerald PCB Saddle + Tiny Amber Status LEDs on Back (clean, compact, ZERO ugly antennae!)
    pcb_rot = Euler((math.radians(16), 0, 0), 'XYZ')
    # Dark steel housing bezel flush on back
    add_cyl(bm, Vector((0.0, 0.018, 0.104)), 0.036, 0.040, 0.010, segs=6, mat_idx=5, rot=pcb_rot)
    # Green 1998 circuit board inset
    add_cyl(bm, Vector((0.0, 0.019, 0.108)), 0.030, 0.033, 0.006, segs=6, mat_idx=6, rot=pcb_rot)
    # Two crisp warm amber LED indicator domes
    for sign in (-1.0, 1.0):
        add_sphere(bm, Vector((0.018 * sign, 0.006, 0.114)), (0.005, 0.005, 0.005), u_seg=10, v_seg=8, mat_idx=7)

    # 5. Classic 1998 Antique Brass Wind-Up Key rising cleanly from the back!
    key_rot = Euler((math.radians(18), 0, 0), 'XYZ')
    # Brass collar mount
    add_cyl(bm, Vector((0.0, 0.022, 0.115)), 0.010, 0.012, 0.009, segs=16, mat_idx=4, rot=key_rot)
    # Key stem terminating cleanly right at the base of the twin butterfly loops
    add_cyl(bm, Vector((0.0, 0.028, 0.132)), 0.0052, 0.0058, 0.028, segs=14, mat_idx=4, rot=key_rot)
    # Twin butterfly brass key loops (classic wind-up toy silhouette!)
    for sign in (-1.0, 1.0):
        loop_center = Vector((0.0165 * sign, 0.036, 0.153))
        add_torus_loop(bm, loop_center, major_r=0.0150, minor_r=0.0042, maj_seg=22, min_seg=10, mat_idx=4, rot=key_rot)

    # Seat lowest vertex strictly on Z = 0.0
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    min_z = min(v.co.z for v in bm.verts)
    bmesh.ops.translate(bm, vec=Vector((0.0, 0.0, -min_z)), verts=bm.verts)

    mesh = bpy.data.meshes.new("JabbaFrogMesh")
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new("JabbaFrog", mesh)
    bpy.context.collection.objects.link(obj)
    for m in mats:
        obj.data.materials.append(m)

    # Bake 24-ray hemisphere BVHTree AO into COLOR_0 (soft, rich depth without making eyes/belly dingy!)
    bvh = BVHTree.FromPolygons([v.co for v in mesh.vertices], [list(p.vertices) for p in mesh.polygons])
    # Golden-spiral upper hemisphere sample directions
    dirs = []
    n_rays = 24
    golden = math.pi * (3.0 - math.sqrt(5.0))
    for k in range(n_rays):
        z = (k + 0.5) / n_rays
        r = math.sqrt(max(0.0, 1.0 - z * z))
        ang = golden * k
        dirs.append(Vector((math.cos(ang) * r, math.sin(ang) * r, z)))

    vert_ao = []
    for v in mesh.vertices:
        n = v.normal.normalized()
        # Build tangent frame around normal
        up = Vector((0, 0, 1)) if abs(n.z) < 0.9 else Vector((1, 0, 0))
        tx = up.cross(n).normalized()
        ty = n.cross(tx).normalized()
        hits = 0
        origin = v.co + n * 0.0018
        for d in dirs:
            wdir = (tx * d.x + ty * d.y + n * d.z).normalized()
            loc, _, _, dist = bvh.ray_cast(origin, wdir, 0.14)
            if loc is not None:
                hits += 1
        occ = hits / float(n_rays)
        # Gentle AO range (0.58..1.0) so crevices have crisp depth while eyes stay sparkling white
        ao = 1.0 - 0.42 * occ
        # Extra floor contact shadow near Z=0
        if v.co.z < 0.025:
            ao *= (0.78 + 0.22 * (v.co.z / 0.025))
        vert_ao.append(ao)

    col_attr = mesh.color_attributes.new(name="COLOR_0", type="BYTE_COLOR", domain="CORNER")
    for poly in mesh.polygons:
        is_bright = poly.material_index in (2, 7)  # eyes & LEDs stay bright
        for li in poly.loop_indices:
            vi = mesh.loops[li].vertex_index
            ao = 1.0 if is_bright else vert_ao[vi]
            col_attr.data[li].color = (ao, ao, ao, 1.0)

    mesh.update()

    # Measure final Blender bounds (X=width, Y=length, Z=height)
    xs = [v.co.x for v in mesh.vertices]
    ys = [v.co.y for v in mesh.vertices]
    zs = [v.co.z for v in mesh.vertices]
    print(f"FROG V2 BOUNDS: W={max(xs)-min(xs):.3f}m  L={max(ys)-min(ys):.3f}m  H={max(zs)-min(zs):.3f}m  verts={len(mesh.vertices)} tris={sum(len(p.vertices)-2 for p in mesh.polygons)}")

    # Export GLB cleanly
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(
        filepath=OUT_GLB,
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_normals=True,
        export_attributes=True,
        export_materials="EXPORT",
    )
    print(f"EXPORTED -> {OUT_GLB} ({os.path.getsize(OUT_GLB)} bytes)")

    # Quick EEVEE studio turntable proof render with bright green shell tint preview
    for node in mat_shell.node_tree.nodes:
        if node.type == "MIX":
            node.inputs["A"].default_value = (*srgb_to_lin((0.28, 0.84, 0.44)), 1.0)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 800
    scene.render.resolution_y = 600
    scene.world = bpy.data.worlds.new("W")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.08, 0.09, 0.13, 1.0)

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 55
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = Vector((0.26, -0.34, 0.21))
    direction = Vector((0.0, -0.01, 0.075)) - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam

    for name, loc, energy in [("Key", (0.5, -0.6, 0.7), 18.0), ("Fill", (-0.5, -0.3, 0.4), 7.0), ("Rim", (0.1, 0.6, 0.5), 12.0)]:
        ld = bpy.data.lights.new(name, "POINT")
        ld.energy = energy
        lo = bpy.data.objects.new(name, ld)
        scene.collection.objects.link(lo)
        lo.location = Vector(loc)

    scene.render.filepath = PROOF_PNG
    bpy.ops.render.render(write_still=True)
    print(f"PROOF RENDER -> {PROOF_PNG}")


if __name__ == "__main__":
    build_frog()
