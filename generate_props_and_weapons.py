import bpy
import bmesh
import math
import os
from mathutils import Vector, Matrix

from pathlib import Path

ASSETS = Path(__file__).resolve().parent / "assets"
CHAIR_SOURCE = ASSETS / "external_cc0/SheenChair"
OUT_MODELS = str(ASSETS / "models")
OUT_ICONS = str(ASSETS / "icons")
os.makedirs(OUT_MODELS, exist_ok=True)
os.makedirs(OUT_ICONS, exist_ok=True)

def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def make_mat(name, rgba, metallic=0.1, roughness=0.42):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
    return mat

# Convert Raylib coordinates (X right, Y up, Z forward) -> Blender coordinates (X right, Y=-Z, Z=Y)
# Because Blender glTF exporter maps (+X -> +X, +Z -> +Y, -Y -> +Z)!
def rl_pos(p):
    return (p[0], -p[2], p[1])

def rl_scale(s):
    return (s[0], s[2], s[1])

def rl_rot(r):
    # r is (pitch_around_X, yaw_around_Y, roll_around_Z) in Raylib
    return (r[0], -r[2], r[1])

def add_box(name, pos, size, mat, rot=(0,0,0), bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=rl_pos(pos), rotation=rl_rot(rot))
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = rl_scale(size)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", 'BEVEL')
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(mat)
    return obj

# Vertical cylinder in Raylib (along Y up) -> in Blender default cylinder is ALREADY along Z up!
def add_vcyl(name, pos, radius, height, mat, rot=(0,0,0), vertices=16):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=height, location=rl_pos(pos), rotation=rl_rot(rot))
    obj = bpy.context.active_object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(mat)
    return obj

# Rod between two Raylib 3D points p0 -> p1 with radius r0, r1
def add_rod(name, p0, p1, r0, r1, mat, vertices=14):
    v0 = Vector(rl_pos(p0))
    v1 = Vector(rl_pos(p1))
    d = v1 - v0
    length = d.length
    if length < 1e-5:
        return None
    mid = (v0 + v1) * 0.5
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r0, radius2=r1, depth=length, location=mid)
    obj = bpy.context.active_object
    obj.name = name
    z_axis = Vector((0, 0, 1))
    quat = z_axis.rotation_difference(d.normalized())
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = quat
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(mat)
    return obj

def add_sphere(name, pos, radius, mat, scale=(1,1,1), segments=14, ring_count=10):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=ring_count, radius=radius, location=rl_pos(pos))
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = rl_scale(scale)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    return obj

def finalize_and_export(filename, preserve_smoothing=False):
    mesh_objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in mesh_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objs[0]
    if len(mesh_objs) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0000001)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.0000001)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    if not preserve_smoothing:
        for poly in obj.data.polygons:
            poly.use_smooth = False
    out_path = os.path.join(OUT_MODELS, filename)
    bpy.ops.export_scene.gltf(
        filepath=out_path,
        export_format='GLB',
        use_selection=True,
        export_apply=True,
        export_normals=True,
        export_materials='EXPORT'
    )
    print("Exported:", out_path, os.path.getsize(out_path))

# ==============================================================================
# 1. PROP: OFFICE WATER COOLER (prop_water_cooler.glb)
# ==============================================================================
def build_water_cooler():
    import runpy
    mod = runpy.run_path(str(ASSETS / "external_cc0/office/build_hero_office_props.py"))
    mod["build_prop_water_cooler"]()

# ==============================================================================
# 2. PROP: VENDING MACHINE (prop_vending_machine.glb)
# ==============================================================================
def build_vending_machine():
    clear_scene()
    m_cab = make_mat("VendCab", (0.12, 0.15, 0.22, 1), metallic=0.35, roughness=0.38)
    m_trim = make_mat("VendTrim", (0.24, 0.29, 0.38, 1), metallic=0.55, roughness=0.3)
    m_interior = make_mat("VendIn", (0.86, 0.91, 0.97, 1), roughness=0.22)
    m_shelf = make_mat("VendShelf", (0.30, 0.35, 0.42, 1), metallic=0.6, roughness=0.3)
    m_can_red = make_mat("CanRed", (0.94, 0.18, 0.24, 1), metallic=0.45, roughness=0.25)
    m_can_cyan = make_mat("CanCyan", (0.10, 0.80, 0.96, 1), metallic=0.45, roughness=0.25)
    m_can_gold = make_mat("CanGold", (0.96, 0.74, 0.16, 1), metallic=0.45, roughness=0.25)
    m_screen = make_mat("VendScreen", (0.15, 0.88, 0.98, 1), roughness=0.15)
    m_flap = make_mat("VendFlap", (0.08, 0.10, 0.14, 1), roughness=0.4)

    add_box("Cab", (0, 1.08, 0), (1.18, 2.16, 0.86), m_cab, bevel=0.03)
    add_box("HeaderGlow", (0, 2.02, 0.435), (1.06, 0.16, 0.03), m_screen)
    add_box("WindowBack", (-0.14, 1.24, 0.415), (0.74, 1.26, 0.04), m_interior)
    add_box("WindowFrame", (-0.14, 1.24, 0.435), (0.78, 1.30, 0.02), m_trim)

    mats = [m_can_red, m_can_cyan, m_can_gold]
    for s_idx, sy in enumerate([0.74, 1.02, 1.30, 1.58]):
        add_box(f"Shelf_{s_idx}", (-0.14, sy, 0.425), (0.70, 0.025, 0.10), m_shelf)
        for c_idx in range(5):
            cx = -0.40 + c_idx * 0.13
            mat = mats[(s_idx + c_idx) % 3]
            add_vcyl(f"Can_{s_idx}_{c_idx}", (cx, sy + 0.075, 0.425), 0.036, 0.115, mat, vertices=12)

    add_box("ControlPanel", (0.40, 1.24, 0.435), (0.24, 1.26, 0.03), m_trim, bevel=0.01)
    add_box("PayDisplay", (0.40, 1.62, 0.452), (0.16, 0.12, 0.015), m_screen)
    add_box("Keypad", (0.40, 1.38, 0.452), (0.14, 0.20, 0.015), m_flap)
    add_box("BillSlot", (0.40, 1.14, 0.452), (0.14, 0.08, 0.02), m_can_gold)
    add_box("PickupFlap", (-0.14, 0.34, 0.435), (0.74, 0.28, 0.04), m_flap, bevel=0.01)
    add_box("PickupTrim", (-0.14, 0.34, 0.452), (0.66, 0.04, 0.02), m_trim)

    finalize_and_export("prop_vending_machine.glb")

# ==============================================================================
# 3. PROP: METAL FILING CABINET (prop_filing_cabinet.glb)
# ==============================================================================
def build_filing_cabinet():
    clear_scene()
    m_steel = make_mat("CabSteel", (0.48, 0.53, 0.60, 1), metallic=0.45, roughness=0.38)
    m_drawer = make_mat("DrawerFront", (0.58, 0.63, 0.70, 1), metallic=0.4, roughness=0.35)
    m_chrome = make_mat("ChromeHandle", (0.88, 0.92, 0.96, 1), metallic=0.85, roughness=0.2)
    m_label = make_mat("LabelCard", (0.96, 0.95, 0.88, 1), roughness=0.5)

    add_box("CabBody", (0, 0.925, 0), (0.88, 1.85, 1.12), m_steel, bevel=0.02)
    for d in range(4):
        dy = 0.26 + d * 0.43
        add_box(f"Drawer_{d}", (-0.445, dy, 0), (0.04, 0.38, 1.02), m_drawer, bevel=0.008)
        add_box(f"LabelFrame_{d}", (-0.47, dy + 0.07, 0), (0.015, 0.06, 0.22), m_chrome)
        add_box(f"LabelCard_{d}", (-0.475, dy + 0.07, 0), (0.012, 0.042, 0.19), m_label)
        add_box(f"Handle_{d}", (-0.48, dy - 0.04, 0), (0.025, 0.028, 0.28), m_chrome)

    finalize_and_export("prop_filing_cabinet.glb")

# ==============================================================================
# 4. PROP: ROLLING OFFICE WHITEBOARD (prop_whiteboard.glb)
# ==============================================================================
def build_whiteboard():
    clear_scene()
    m_alu = make_mat("AluFrame", (0.76, 0.80, 0.85, 1), metallic=0.7, roughness=0.25)
    m_dark = make_mat("StandSteel", (0.22, 0.25, 0.30, 1), metallic=0.5, roughness=0.35)
    m_board = make_mat("BoardWhite", (0.96, 0.97, 0.99, 1), roughness=0.2)
    m_red = make_mat("DiagramRed", (0.92, 0.22, 0.35, 1), roughness=0.3)
    m_blue = make_mat("DiagramBlue", (0.16, 0.56, 0.96, 1), roughness=0.3)
    m_green = make_mat("DiagramGreen", (0.18, 0.78, 0.48, 1), roughness=0.3)
    m_note = make_mat("StickyNote", (0.98, 0.86, 0.24, 1), roughness=0.4)

    for sx in [-0.95, 0.95]:
        add_box(f"Post_{sx}", (sx, 0.95, 0), (0.045, 1.85, 0.045), m_dark)
        add_box(f"BaseBar_{sx}", (sx, 0.06, 0), (0.05, 0.045, 0.62), m_dark)
        for sz in [-0.28, 0.28]:
            add_rod(f"Wheel_{sx}_{sz}", (sx-0.02, 0.035, sz), (sx+0.02, 0.035, sz), 0.035, 0.035, m_alu, 10)
    add_box("CrossBar", (0, 0.38, 0), (1.90, 0.04, 0.04), m_dark)
    add_box("FrameTop", (0, 1.88, 0), (1.96, 0.045, 0.055), m_alu)
    add_box("FrameBot", (0, 0.82, 0), (1.96, 0.045, 0.055), m_alu)
    add_box("BoardCore", (0, 1.35, 0), (1.88, 1.02, 0.03), m_board)
    add_box("MarkerTray", (0, 0.80, 0.055), (1.50, 0.025, 0.09), m_alu)

    for side in [-1, 1]:
        z = side * 0.02
        add_box(f"Flow1_{side}", (-0.52, 1.52, z), (0.42, 0.22, 0.01), m_blue)
        add_box(f"Flow2_{side}", (0.08, 1.52, z), (0.42, 0.22, 0.01), m_red)
        add_box(f"Flow3_{side}", (0.58, 1.22, z), (0.44, 0.24, 0.01), m_green)
        add_box(f"Arrow1_{side}", (-0.22, 1.52, z), (0.18, 0.025, 0.012), m_dark)
        add_box(f"Arrow2_{side}", (0.32, 1.36, z), (0.24, 0.025, 0.012), m_dark)
        for n in range(3):
            add_box(f"Sticky_{side}_{n}", (-0.62 + n*0.18, 1.14, z), (0.13, 0.13, 0.012), m_note)

    finalize_and_export("prop_whiteboard.glb")

# ==============================================================================
# 5. PROP: POTTED FICUS TREE (prop_ficus_tree.glb)
# ==============================================================================
def build_ficus_tree():
    import runpy, shutil
    ficus_script = ASSETS / "external_cc0/office/build_cc0_ficus.py"
    runpy.run_path(str(ficus_script), run_name="__main__")
    src = ASSETS / "external_cc0/office/FicusPlant_CC0.glb"
    dst = Path(OUT_MODELS) / "prop_ficus_tree.glb"
    shutil.copy2(src, dst)
    print("Exported:", dst, os.path.getsize(dst))

# ==============================================================================
# 6. PROP: OFFICE TRASH BIN (prop_trash_bin.glb)
# ==============================================================================
def build_trash_bin():
    clear_scene()
    m_mesh = make_mat("BinSteel", (0.26, 0.30, 0.36, 1), metallic=0.6, roughness=0.35)
    m_rim = make_mat("BinRim", (0.42, 0.47, 0.54, 1), metallic=0.7, roughness=0.3)
    m_paper1 = make_mat("PaperWhite", (0.94, 0.93, 0.88, 1), roughness=0.6)
    m_paper2 = make_mat("PaperBlue", (0.72, 0.86, 0.96, 1), roughness=0.6)

    add_rod("BinWall", (0, 0.0, 0), (0, 0.46, 0), 0.14, 0.19, m_mesh, 14)
    add_vcyl("BinRim", (0, 0.46, 0), 0.198, 0.025, m_rim, vertices=14)
    for i, (px, py, pz, r) in enumerate([
        (-0.05, 0.38, 0.03, 0.065),
        (0.06, 0.41, -0.04, 0.062),
        (0.0, 0.44, 0.05, 0.058),
        (-0.04, 0.45, -0.05, 0.055),
    ]):
        add_sphere(f"Paper_{i}", (px, py, pz), r, m_paper1 if i%2==0 else m_paper2, segments=6, ring_count=5)

    finalize_and_export("prop_trash_bin.glb")

# ==============================================================================
# 7. PROP: SERVER RACK 42U (prop_server_rack.glb)
# ==============================================================================
def build_server_rack():
    import runpy
    mod = runpy.run_path(str(Path(__file__).resolve().parent / "build_landmark_props.py"))
    mod["build_server_rack"]()

# ==============================================================================
# 8. PROP: WHITE SHARK CENTRAL HR RECEPTION DESK (prop_hr_reception_desk.glb)
# ==============================================================================
def build_hr_reception_desk():
    import runpy
    mod = runpy.run_path(str(Path(__file__).resolve().parent / "build_landmark_props.py"))
    mod["build_hr_reception_desk"]()

def build_bug_terminal():
    import runpy
    mod = runpy.run_path(str(Path(__file__).resolve().parent / "build_landmark_props.py"))
    mod["build_bug_terminal"]()

def build_bug_frog():
    import runpy
    mod = runpy.run_path(str(ASSETS / "external_cc0/office/build_prop_bug_frog.py"))
    mod["build_prop_bug_frog"]()

def build_pendant_lamp():
    import runpy
    mod = runpy.run_path(str(Path(__file__).resolve().parent / "scripts/build_atmosphere_props.py"))
    mod["build_prop_pendant_lamp"]()

def build_poster_frame():
    import runpy
    mod = runpy.run_path(str(Path(__file__).resolve().parent / "scripts/build_atmosphere_props.py"))
    mod["build_prop_poster_frame"]()

# ==============================================================================
# 9. PROP: ESCAPED AI AGENT BOSS (escaped_ai_boss.glb)
# ==============================================================================
# escaped_ai_boss.glb is owned exclusively by generate_anatomy.py (MPFB).

# ==============================================================================
# 10. WEAPON 1: FATHER'S LEATHER BELT (weapon_belt_90s.glb)
# ==============================================================================
def build_weapon_belt():
    clear_scene()
    m_leather      = make_mat("RichLeather", (0.28, 0.13, 0.055, 1), roughness=0.38)
    m_leather_dark = make_mat("DarkLeatherEdge", (0.15, 0.065, 0.025, 1), roughness=0.46)
    m_stitch       = make_mat("SaddleStitch", (0.86, 0.68, 0.38, 1), roughness=0.40)
    m_gold         = make_mat("HeavyBrass", (0.98, 0.78, 0.22, 1), metallic=0.90, roughness=0.16)
    m_brass_recess = make_mat("AgedBrassRecess", (0.52, 0.36, 0.10, 1), metallic=0.82, roughness=0.32)
    m_prong        = make_mat("SteelProng", (0.88, 0.91, 0.96, 1), metallic=0.92, roughness=0.14)

    # Helper to build a thick beveled leather strap along a 3D path with variable normal/width frames
    def build_strap_mesh(name, path_pts, width_vecs, norm_vecs, half_w=0.021, half_t=0.0032, mat_main=m_leather):
        verts = []
        faces = []
        profile = [
            (-half_w,        -half_t * 0.75),
            (-half_w * 0.88, -half_t),
            ( half_w * 0.88, -half_t),
            ( half_w,        -half_t * 0.75),
            ( half_w,         half_t * 0.75),
            ( half_w * 0.88,  half_t),
            (-half_w * 0.88,  half_t),
            (-half_w,         half_t * 0.75),
        ]
        n_prof = len(profile)
        for p, wv, nv in zip(path_pts, width_vecs, norm_vecs):
            for pw, pt in profile:
                verts.append(rl_pos(p + wv * pw + nv * pt))
        for i in range(len(path_pts) - 1):
            base0 = i * n_prof
            base1 = (i + 1) * n_prof
            for j in range(n_prof):
                jn = (j + 1) % n_prof
                faces.append((base0 + j, base0 + jn, base1 + jn, base1 + j))
        # End caps
        faces.append(tuple(reversed(range(n_prof))))
        last = (len(path_pts) - 1) * n_prof
        faces.append(tuple(range(last, last + n_prof)))
        data = bpy.data.meshes.new(name)
        data.from_pydata(verts, [], faces)
        data.update()
        obj = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(obj)
        obj.data.materials.append(mat_main)
        for poly in obj.data.polygons:
            poly.use_smooth = True
        return obj

    # --------------------------------------------------------------------------
    # 1) EXACT FINGER-FITTED UPPER STRAP FRAME (0.0mm FINGER CLIPPING!)
    # Matches the finger flexion envelope of fps_hands_belt:
    # Right hand fingers occupy x in [-0.035, +0.038]; Left hand fingers occupy x in [-0.438, -0.365].
    # Outside x > +0.044 (right) and x < -0.446 (left), and below -width*0.022, is 100% finger-free!
    # --------------------------------------------------------------------------
    width_dir  = Vector((0,  math.cos(math.radians(40)), -math.sin(math.radians(40)))).normalized()
    normal_dir = Vector((0, -math.sin(math.radians(40)), -math.cos(math.radians(40)))).normalized()
    along      = Vector((-1, 0, 0))  # From right hand (+x) toward left hand (-x)

    def strap_curve(t):
        return Vector((0.09 - 0.58 * t, -0.032 * 4 * t * (1 - t), 0.0))

    start_t = (0.09 - 0.046) / 0.58
    end_t   = (0.09 + 0.448) / 0.58

    def upper_strap_pos(u):
        t = start_t + (end_t - start_t) * u
        return strap_curve(t)

    def lower_strap_pos(u):
        t = start_t + (end_t - start_t) * u
        sag = math.sin(u * math.pi)
        # Arch below (-width_dir) and slightly behind (-normal_dir) the upper strap,
        # staying completely clear of both hands' fingers!
        return strap_curve(t) - width_dir * (0.026 + 0.030 * sag) - normal_dir * (0.015 + 0.010 * sag)

    n_seg = 64
    u_pts = [upper_strap_pos(i / n_seg) for i in range(n_seg + 1)]
    l_pts = [lower_strap_pos(i / n_seg) for i in range(n_seg + 1)]
    u_w = [width_dir for _ in u_pts]
    u_n = [normal_dir for _ in u_pts]
    l_w = [width_dir for _ in l_pts]
    l_n = [-normal_dir for _ in l_pts]

    build_strap_mesh("UpperCombatStrap", u_pts, u_w, u_n, half_w=0.020, half_t=0.0028, mat_main=m_leather)
    build_strap_mesh("LowerReturnStrap", l_pts, l_w, l_n, half_w=0.0195, half_t=0.0028, mat_main=m_leather_dark)

    # --------------------------------------------------------------------------
    # 2) TWO TIGHT LEATHER COILS WRAPPED AROUND THE RIGHT FIST / WRIST CUFF
    # Positioned at x = +0.052..+0.080 (outside the right index/middle/ring/pinky fingers!)
    # and looping down around the lower right fist into the lower return strap.
    # --------------------------------------------------------------------------
    r_anchor = strap_curve(start_t) - width_dir * 0.014 - normal_dir * 0.012 + Vector((0.018, 0, 0))
    c_rad1 = width_dir
    c_rad2 = normal_dir
    c_axis = Vector((1, -0.18, 0.12)).normalized()

    for coil_idx, ax_off in enumerate([0.000, 0.018]):
        c_pts, c_wvecs, c_nvecs = [], [], []
        steps = 40
        for i in range(steps + 1):
            ang = -math.pi * 0.35 + (math.tau * 0.92) * (i / steps)
            hel = ax_off + (i / steps - 0.5) * 0.008
            radial = c_rad1 * (0.022 * math.cos(ang)) + c_rad2 * (0.018 * math.sin(ang))
            pt = r_anchor + c_axis * hel + radial
            c_pts.append(pt)
            c_wvecs.append(c_axis)
            c_nvecs.append(radial.normalized())
        build_strap_mesh(f"RightFistWrap_{coil_idx}", c_pts, c_wvecs, c_nvecs, half_w=0.014, half_t=0.0030, mat_main=m_leather)
        for i in range(0, steps, 2):
            p0, p1 = c_pts[i], c_pts[min(steps, i + 1)]
            n0 = c_nvecs[i]
            for s_edge in (-1, 1):
                off = c_axis * (s_edge * 0.0105) + n0 * 0.0033
                add_rod(f"WrapStitch_{coil_idx}_{i}_{s_edge}", p0 + off, p1 + off, 0.00065, 0.00065, m_stitch, 6)

    # Smooth U-fold connecting UpperCombatStrap and LowerReturnStrap at the right end (outside fingers)
    r_fold_pts, r_fold_w, r_fold_n = [], [], []
    for i in range(17):
        theta = (i / 16.0) * math.pi
        c_mid = 0.5 * (u_pts[0] + l_pts[0])
        rad_v = 0.5 * (u_pts[0] - l_pts[0])
        pt = c_mid + rad_v * math.cos(theta) - along * (0.014 * math.sin(theta))
        n_v = (rad_v.normalized() * math.cos(theta) - along * math.sin(theta)).normalized()
        r_fold_pts.append(pt)
        r_fold_w.append(width_dir)
        r_fold_n.append(n_v)
    build_strap_mesh("RightEndFold", r_fold_pts, r_fold_w, r_fold_n, half_w=0.0195, half_t=0.0028, mat_main=m_leather)

    # Continuous U-fold bight looping around the outside of the left hand at u_pts[-1] -> l_pts[-1]
    bight_pts, bight_w, bight_n = [], [], []
    for i in range(21):
        theta = (i / 20.0) * math.pi
        c_mid = 0.5 * (u_pts[-1] + l_pts[-1])
        rad_v = 0.5 * (u_pts[-1] - l_pts[-1])
        pt = c_mid + rad_v * math.cos(theta) + along * (0.022 * math.sin(theta))
        n_v = (rad_v.normalized() * math.cos(theta) + along * math.sin(theta)).normalized()
        bight_pts.append(pt)
        bight_w.append(width_dir)
        bight_n.append(n_v)
    build_strap_mesh("LeftHandLoopBight", bight_pts, bight_w, bight_n, half_w=0.0198, half_t=0.0028, mat_main=m_leather)

    # Tapered leather tip emerging cleanly past the left fold (x < -0.450, 100% outside left fingers!)
    tail_pts, tail_w, tail_n = [], [], []
    for i in range(15):
        kt = i / 14.0
        pt = u_pts[-1] + along * (0.004 + kt * 0.036) - width_dir * (0.008 * kt * kt) - normal_dir * (0.003 * kt)
        tail_pts.append(pt)
        taper = 1.0 - 0.55 * (kt * kt)
        tail_w.append(width_dir * taper)
        tail_n.append(normal_dir)
    build_strap_mesh("WhipTailEnd", tail_pts, tail_w, tail_n, half_w=0.0195, half_t=0.0027, mat_main=m_leather)

    # Contrasting saddle stitching along the upper strap & burnished edge rails
    for i in range(0, n_seg, 2):
        p0 = u_pts[i]
        p1 = u_pts[min(n_seg, i + 1)]
        for edge in (-1, 1):
            edge_pos0 = p0 + width_dir * (edge * 0.0202)
            edge_pos1 = p1 + width_dir * (edge * 0.0202)
            add_rod(f"BurnishedEdge_{i}_{edge}", edge_pos0, edge_pos1, 0.0012, 0.0012, m_leather_dark, 6)
            for face in (-1, 1):
                st_off = width_dir * (edge * 0.0162) + normal_dir * (face * 0.0031)
                add_rod(f"SaddleStitch_{i}_{edge}_{face}", p0 + st_off, p1 + st_off, 0.0007, 0.0007, m_stitch, 6)

    # Hollow brass eyelets along the upper strap
    for idx, t_hole in enumerate([0.20, 0.32, 0.44, 0.56, 0.68, 0.80]):
        center = upper_strap_pos(t_hole)
        for face in (-1, 1):
            ring_pts = [
                center + normal_dir * (face * 0.0032)
                + along * (0.0038 * math.cos(k * math.tau / 18))
                + width_dir * (0.0038 * math.sin(k * math.tau / 18))
                for k in range(18)
            ]
            for k in range(18):
                add_rod(f"Eyelet_{idx}_{face}_{k}", ring_pts[k], ring_pts[(k + 1) % 18], 0.0010, 0.0010, m_gold, 8)
        add_rod(f"EyeletHole_{idx}", center - normal_dir * 0.0030, center + normal_dir * 0.0030, 0.0026, 0.0026, m_leather_dark, 12)

    # --------------------------------------------------------------------------
    # 3) MASSIVE SCULPTED 90s BRASS BUCKLE WITH CHAMFERS, ENGRAVING & STEEL PRONG
    # --------------------------------------------------------------------------
    b_center = upper_strap_pos(0.56) + normal_dir * 0.0072
    bx_half = 0.030
    by_half = 0.024

    # Outer forged chamfered brass frame
    frame_corners = []
    for cx, cy, start_deg in ((bx_half, by_half, 0), (-bx_half, by_half, 90), (-bx_half, -by_half, 180), (bx_half, -by_half, 270)):
        for k in range(6):
            a = math.radians(start_deg + k * 18)
            frame_corners.append(b_center + along * (cx + 0.0065 * math.cos(a)) + width_dir * (cy + 0.0065 * math.sin(a)))
    for k in range(len(frame_corners)):
        p_a = frame_corners[k]
        p_b = frame_corners[(k + 1) % len(frame_corners)]
        add_rod(f"BuckleOuterFrame_{k}", p_a, p_b, 0.0048, 0.0048, m_gold, 14)
        p_ia = b_center + (p_a - b_center) * 0.84 - normal_dir * 0.0010
        p_ib = b_center + (p_b - b_center) * 0.84 - normal_dir * 0.0010
        add_rod(f"BuckleInnerBevel_{k}", p_ia, p_ib, 0.0022, 0.0022, m_brass_recess, 10)

    # Sculpted 90s decorative crest plate on the rear half of the buckle + engraved relief bars
    plate_center = b_center - along * 0.011 + normal_dir * 0.0018
    for rib_k, wy in enumerate([-0.015, -0.008, 0.0, 0.008, 0.015]):
        rib_p0 = plate_center - along * 0.015 + width_dir * wy
        rib_p1 = plate_center + along * 0.011 + width_dir * wy
        mat_rib = m_gold if rib_k % 2 == 0 else m_brass_recess
        rad_rib = 0.0024 if rib_k % 2 == 0 else 0.0018
        add_rod(f"BuckleReliefRib_{rib_k}", rib_p0, rib_p1, rad_rib, rad_rib, mat_rib, 10)

    # Raised 5-pointed star / emblem relief in the center of the brass buckle plate
    star_c = plate_center + normal_dir * 0.0022
    for ray in range(5):
        ang = ray * math.tau / 5.0
        tip_p = star_c + along * (0.010 * math.cos(ang)) + width_dir * (0.010 * math.sin(ang))
        add_rod(f"BuckleStarRay_{ray}", star_c, tip_p, 0.0026, 0.0009, m_gold, 8)

    # Corner bolster studs on the brass frame
    for sx in (-1, 1):
        for sy in (-1, 1):
            stud_p = b_center + along * (sx * 0.029) + width_dir * (sy * 0.022) + normal_dir * 0.0030
            add_rod(f"BuckleCornerStud_{sx}_{sy}", stud_p - normal_dir * 0.0025, stud_p + normal_dir * 0.0025, 0.0035, 0.0028, m_gold, 10)

    # Front knurled brass roller bar & rear hinge bar
    roller_c = b_center + along * (bx_half + 0.0055)
    add_rod("BuckleFrontRoller", roller_c - width_dir * 0.022, roller_c + width_dir * 0.022, 0.0058, 0.0058, m_gold, 16)
    hinge_c = b_center - along * (bx_half - 0.004)
    add_rod("BuckleHingeAxle", hinge_c - width_dir * 0.024, hinge_c + width_dir * 0.024, 0.0034, 0.0034, m_brass_recess, 14)

    # Heavy tapered steel buckle prong (стальной язычок)
    prong_base = hinge_c + normal_dir * 0.0028
    prong_mid  = b_center + normal_dir * 0.0064
    prong_tip  = roller_c + along * 0.004 + normal_dir * 0.0050
    add_rod("SteelProngRear", prong_base, prong_mid, 0.0032, 0.0026, m_prong, 12)
    add_rod("SteelProngTip",  prong_mid,  prong_tip, 0.0026, 0.0013, m_prong, 12)

    # Leather keeper loop (тренчик) wrapping both upper & lower straps at t=0.42 + flush brass rivets on the curved strap
    ku_c = upper_strap_pos(0.42)
    kl_c = lower_strap_pos(0.42)
    k_pts = [
        ku_c + width_dir * 0.0225 + normal_dir * 0.0042,
        ku_c - width_dir * 0.0225 + normal_dir * 0.0042,
        kl_c - width_dir * 0.0215 - normal_dir * 0.0042,
        kl_c + width_dir * 0.0215 - normal_dir * 0.0042,
    ]
    for k in range(4):
        add_rod(f"LeatherKeeper_{k}", k_pts[k], k_pts[(k + 1) % 4], 0.0036, 0.0036, m_leather_dark, 10)
    for t_riv in (0.37, 0.46):
        rv_p = upper_strap_pos(t_riv)
        add_rod(f"BrassRivet_{t_riv}", rv_p - normal_dir * 0.0030, rv_p + normal_dir * 0.0038, 0.0032, 0.0032, m_gold, 12)

    finalize_and_export("weapon_belt_90s.glb", preserve_smoothing=True)

# ==============================================================================
# 11. WEAPON 2: 90s SOLDERING IRON WITH BLUE TAPE (weapon_solder_90s.glb)
# ==============================================================================
def build_reception_chair():
    clear_scene()
    bpy.ops.import_scene.gltf(filepath=str(CHAIR_SOURCE / "SheenChair.glb"))
    velvet = make_mat("Reception petrol velvet", (.025,.16,.16,1), roughness=.83)
    for obj in list(bpy.context.scene.objects):
        if obj.type != "MESH":
            continue
        bpy.context.view_layer.objects.active = obj
        obj.data.gltf2_variant_default_materials.clear()
        obj.data.gltf2_variant_mesh_data.clear()
        for slot in obj.material_slots:
            if slot.material and slot.material.name.startswith("fabric "):
                slot.material = velvet
        # Keep the imported silhouette and upholstery UVs at the lobby viewing distance.
        if len(obj.data.polygons) > 3000:
            modifier = obj.modifiers.new("Lobby detail", "DECIMATE")
            modifier.ratio = .42
            bpy.ops.object.modifier_apply(modifier=modifier.name)
    finalize_and_export("prop_reception_chair.glb", preserve_smoothing=True)


def build_weapon_solder():
    clear_scene()
    wood = make_mat("Turned hardwood", (.47,.21,.075,1), roughness=.49)
    nodes, links = wood.node_tree.nodes, wood.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    grain = nodes.new("ShaderNodeTexImage")
    grain.image = bpy.data.images.load(str(CHAIR_SOURCE / "chair_wood_albedo.png"))
    # Stain the source grayscale grain in linear light, then embed the RGB albedo.
    # The runtime samples the texture directly, so bake the stain into its pixels.
    import numpy as np
    source = grain.image
    pixels = np.empty(len(source.pixels), dtype=np.float32)
    source.pixels.foreach_get(pixels)
    pixels.reshape(-1,4)[:,:3] *= (.39,.16,.048)
    stained = bpy.data.images.new("Walnut stained grain", *source.size)
    stained.pixels.foreach_set(pixels)
    stained.pack()
    grain.image = stained
    links.new(grain.outputs["Color"], bsdf.inputs["Base Color"])
    packed = nodes.new("ShaderNodeTexImage")
    packed.image = bpy.data.images.load(str(CHAIR_SOURCE / "chair_woodbrown_roughnessmetallic.png"))
    packed.image.colorspace_settings.name = "Non-Color"
    channels = nodes.new("ShaderNodeSeparateColor")
    links.new(packed.outputs["Color"], channels.inputs[0])
    links.new(channels.outputs["Green"], bsdf.inputs["Roughness"])
    tape = make_mat("Cobalt PVC tape", (.022,.12,.43,1), roughness=.38)
    edge = make_mat("Tape lap", (.015,.077,.29,1), roughness=.40)
    black = make_mat("Black phenolic", (.035,.027,.029,1), roughness=.40)
    steel = make_mat("Brushed nickel steel", (.38,.43,.48,1), metallic=.76, roughness=.35)
    shadow = make_mat("Heat-darkened steel", (.074,.086,.105,1), metallic=.72, roughness=.46)
    brass = make_mat("Clamp brass", (.54,.32,.105,1), metallic=.75, roughness=.36)
    hot = make_mat("Incandescent nichrome", (.98,.36,.08,1), metallic=.12, roughness=.45)
    heater_bsdf = hot.node_tree.nodes.get("Principled BSDF")
    heater_bsdf.inputs["Emission Color"].default_value = (.98,.19,.025,1)
    heater_bsdf.inputs["Emission Strength"].default_value = .7
    copper_root = make_mat("Oxidised copper", (.27,.076,.039,1), metallic=.68, roughness=.48)
    oxide = make_mat("Violet temper oxide", (.20,.14,.29,1), metallic=.70, roughness=.38)
    copper = make_mat("Forged copper", (.82,.28,.105,1), metallic=.73, roughness=.36)
    tin = make_mat("POS-61", (.84,.88,.90,1), metallic=.83, roughness=.22)

    # A continuous turned profile. Each row carries the finish of its forward span.
    rows = [(-.099,.004,black),(-.089,.006,black),(-.084,.009,wood),
            (-.080,.0128,wood),(-.074,.0132,wood),(-.068,.0125,wood),(-.051,.0126,tape)]
    for i in range(8):
        z = -.050 + i*.011
        rows.extend([(z,.0131,tape),(z+.0093,.0132,edge),(z+.0105,.0129,tape)])
    rows += [(.039,.0126,wood),(.042,.0126,black),(.0445,.0135,black)]
    for z, radius in ((.0455,.0163),(.0515,.0182),(.0575,.0195)):
        rows.extend([(z,radius-.0009,black),(z+.0007,radius,black),
                     (z+.0023,radius,black),(z+.0034,.0134,black)])
    rows.extend([(.0613,.0132,steel),(.0622,.0164,steel),
                 (.0634,.0164,steel),(.064,.0100,shadow)])
    sides = 40
    vertices, faces = [], []
    for z,r,material in rows:
        for i in range(sides):
            angle = math.tau*i/sides
            lap = .0018*math.sin(angle) if -.051 <= z <= .038 else 0
            vertices.append(rl_pos((r*math.cos(angle), r*math.sin(angle), z+lap)))
    for j in range(len(rows)-1):
        for i in range(sides):
            n=(i+1)%sides
            faces.append((j*sides+i,j*sides+n,(j+1)*sides+n,(j+1)*sides+i))
    faces += [tuple(reversed(range(sides))),tuple(range(len(vertices)-sides,len(vertices)))]
    mesh = bpy.data.meshes.new("Turned EPSN profile")
    mesh.from_pydata(vertices,[],faces)
    obj = bpy.data.objects.new("EPSN-40",mesh)
    bpy.context.collection.objects.link(obj)
    materials = [wood,tape,edge,black,steel,shadow]
    for material in materials:
        mesh.materials.append(material)
    uv = mesh.uv_layers.new(name="Turned grain")
    for face in mesh.polygons:
        row, side = divmod(face.index,sides)
        if row >= len(rows)-1:
            face.material_index = materials.index(black if face.index==len(faces)-2 else shadow)
            continue
        face.material_index = materials.index(rows[row][2])
        face.use_smooth = True
        for loop,u,v in zip(face.loop_indices,(side,side+1,side+1,side),(row,row,row+1,row+1)):
            uv.data[loop].uv = ((rows[v][0]+.10)*4, u/sides)
    def axial(name, z0, z1, r0, r1, material):
        return add_rod(name, (0,0,z0), (0,0,z1), r0, r1, material, 48)

    # The punched jacket is an actual hollow wall. Each cell connects a curved
    # patch boundary to a rounded aperture, then down to the dark inner lip.
    axial("Insulated heater mandrel", .064, .168, .0063, .0063, hot)
    for index in range(24):
        z = .066 + .00415 * index
        axial("Recessed coil groove", z, z+.00125, .00645, .00645, copper_root)
    jacket_vertices, jacket_faces, jacket_materials = [], [], []
    def jacket_band(z0, z1, angular_opening, half_length):
        cells, samples = 6, 24
        axial_center = (z0+z1)*.5
        for cell in range(cells):
            base = len(jacket_vertices)
            angle = math.tau*cell/cells
            for boundary, radius in ((True,.0091),(False,.0091),(False,.0081),(True,.0081)):
                for index in range(samples):
                    phase = math.tau*index/samples
                    x, y = math.cos(phase), math.sin(phase)
                    if boundary:
                        scale = max(abs(x),abs(y))
                        azimuth = angle + (math.pi/cells)*x/scale
                        z = axial_center + (z1-z0)*.5*y/scale
                    else:
                        # A short circular punch or a long press-formed slot.
                        azimuth = angle + angular_opening*x
                        cap = min(half_length, angular_opening*radius)
                        z = axial_center + cap*y
                        if abs(y) > 1e-6:
                            z += math.copysign(half_length-cap,y)
                    jacket_vertices.append(rl_pos((radius*math.cos(azimuth), radius*math.sin(azimuth), z)))
            for layer in range(3):
                for index in range(samples):
                    a = base + layer*samples + index
                    b = base + layer*samples + (index+1)%samples
                    jacket_faces.append((a,b,b+samples,a+samples))
                    jacket_materials.append(0 if layer==0 else 1)

    for z0,z1,opening,length in ((.064,.079,.24,.0023),(.079,.107,.34,.0115),
                                  (.107,.123,.24,.0023),(.123,.151,.34,.0115),
                                  (.151,.168,.24,.0023)):
        jacket_band(z0,z1,opening,length)
    jacket = bpy.data.meshes.new("Pierced heater jacket")
    jacket.from_pydata(jacket_vertices,[],jacket_faces)
    jacket.materials.append(steel)
    jacket.materials.append(shadow)
    for face,material_index in zip(jacket.polygons,jacket_materials):
        face.material_index = material_index
    bpy.context.collection.objects.link(bpy.data.objects.new("Punch-formed vents",jacket))
    for z in (.065,.078,.106,.122,.150,.164):
        axial("Rolled stiffener",z,z+.0008,.0089,.0096,steel)
        axial("Rolled stiffener crown",z+.0008,z+.0020,.0096,.0096,steel)
        axial("Rolled stiffener return",z+.0020,z+.0028,.0096,.0089,shadow)

    axial("Chuck shoulder",.165,.169,.0091,.0108,shadow)
    axial("Tool steel chuck",.169,.1815,.0108,.0108,steel)
    axial("Chuck nose",.1815,.184,.0108,.0078,shadow)
    for sign in (-1,1):
        add_rod("Screw shank", (sign*.0095,0,.175),(sign*.013,0,.175),.0026,.0026,shadow,16)
        add_rod("Brass thumb nut", (sign*.0125,0,.175),(sign*.015,0,.175),.004,.0036,brass,8)
        add_box("Thumb screw wings", (sign*.015,0,.175),(.0023,.012,.0030),
                brass, rot=(sign*.24,0,0),bevel=.0007)

    # The eight-sided forging fans out to a 14 mm chisel, with a thin tinned lip.
    stations = [(.182,.0059,.005,0,copper_root),(.192,.0056,.0048,0,oxide),
                (.196,.0055,.0047,0,copper),(.211,.0061,.0040,0,copper),
                (.225,.0069,.0023,.0010,tin),(.233,.007,.0013,.0014,tin),
                (.238,.0068,.00042,.0016,tin)]
    section = ((-.72,-1),(.72,-1),(1,-.57),(1,.57),(.72,1),(-.72,1),(-1,.57),(-1,-.57))
    tip_vertices, tip_faces = [], []
    for z,width,thickness,lift,material in stations:
        for x,y in section:
            tip_vertices.append(rl_pos((width*x, lift+thickness*y, z)))
    for row in range(len(stations)-1):
        for side in range(8):
            a = row*8+side
            b = row*8+(side+1)%8
            tip_faces.append((a,b,b+8,a+8))
    tip_faces.extend((tuple(reversed(range(8))),tuple(range(len(tip_vertices)-8,len(tip_vertices)))))
    tip_mesh = bpy.data.meshes.new("Forged screwdriver chisel")
    tip_mesh.from_pydata(tip_vertices,[],tip_faces)
    finishes = [copper_root,oxide,copper,tin]
    for material in finishes:
        tip_mesh.materials.append(material)
    for face in tip_mesh.polygons:
        finish = stations[min(face.index//8,len(stations)-1)][4]
        face.material_index = finishes.index(finish)
    bpy.context.collection.objects.link(bpy.data.objects.new("Tinned copper chisel",tip_mesh))
    finalize_and_export("weapon_solder_90s.glb", preserve_smoothing=True)


# ==============================================================================
# 11B. WEAPON NET: $3 OFFICE CAPTURE NET («САЧОК-ЛОВУШКА ИЗ ШВАБРЫ И СЕТКИ»)
# ==============================================================================
def build_weapon_net():
    clear_scene()
    wood = make_mat("Turned hardwood", (.47, .21, .075, 1), roughness=.49)
    nodes, links = wood.node_tree.nodes, wood.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    grain = nodes.new("ShaderNodeTexImage")
    grain.image = bpy.data.images.load(str(CHAIR_SOURCE / "chair_wood_albedo.png"))
    import numpy as np
    source = grain.image
    pixels = np.empty(len(source.pixels), dtype=np.float32)
    source.pixels.foreach_get(pixels)
    pixels.reshape(-1, 4)[:, :3] *= (.44, .22, .085)
    stained = bpy.data.images.new("Mop stained birch grain", *source.size)
    stained.pixels.foreach_set(pixels)
    stained.pack()
    grain.image = stained
    links.new(grain.outputs["Color"], bsdf.inputs["Base Color"])

    tape = make_mat("Cobalt PVC tape", (.022, .12, .43, 1), roughness=.36)
    edge = make_mat("Tape lap", (.015, .077, .29, 1), roughness=.40)
    mop_cap = make_mat("MopPlasticCap", (.06, .34, .32, 1), roughness=.42)
    alu = make_mat("AnodizedMopAluminum", (.54, .58, .62, 1), metallic=.82, roughness=.32)
    steel_wire = make_mat("GalvanizedHoopWire", (.42, .46, .50, 1), metallic=.85, roughness=.28)
    rim_canvas = make_mat("NetCanvasCollar", (.08, .22, .20, 1), roughness=.78)
    net_cord = make_mat("WovenNylonNet", (.20, .52, .42, 1), roughness=.62)
    net_accent = make_mat("NeonBugMeshThread", (.30, .78, .58, 1), roughness=.48)

    # 1) Mop Handle & Blue Electrical Tape Grip (exact grip alignment matching weapon_solder_90s.glb!)
    rows = [
        (-.104, .0055, mop_cap),
        (-.098, .0115, mop_cap),
        (-.084, .0134, mop_cap),
        (-.080, .0128, wood),
        (-.074, .0132, wood),
        (-.068, .0125, wood),
        (-.051, .0126, tape),
    ]
    for i in range(8):
        z = -.050 + i * .011
        rows.extend([(z, .0131, tape), (z + .0093, .0132, edge), (z + .0105, .0129, tape)])
    rows += [
        (.039, .0126, wood),
        (.048, .0125, wood),
        (.062, .0124, wood),
        (.063, .0130, alu),
        (.095, .0126, alu),
        (.128, .0124, alu),
        (.130, .0142, tape),
    ]
    for i in range(4):
        z = .131 + i * .010
        rows.extend([(z, .0148, tape), (z + .0082, .0152, edge), (z + .0095, .0145, tape)])
    rows.extend([(.172, .0136, tape), (.176, .0112, alu), (.180, .0050, alu)])

    sides = 24
    vertices, faces = [], []
    for z, r, material in rows:
        for i in range(sides):
            angle = math.tau * i / sides
            lap = .0018 * math.sin(angle) if (-.051 <= z <= .038 or .130 <= z <= .172) else 0
            vertices.append(rl_pos((r * math.cos(angle), r * math.sin(angle), z + lap)))
    for j in range(len(rows) - 1):
        for i in range(sides):
            n = (i + 1) % sides
            faces.append((j * sides + i, j * sides + n, (j + 1) * sides + n, (j + 1) * sides + i))
    faces += [tuple(reversed(range(sides))), tuple(range(len(vertices) - sides, len(vertices)))]
    mesh = bpy.data.meshes.new("MopHandleProfile")
    mesh.from_pydata(vertices, [], faces)
    obj = bpy.data.objects.new("MopHandle", mesh)
    bpy.context.collection.objects.link(obj)
    materials = [wood, tape, edge, mop_cap, alu]
    for material in materials:
        mesh.materials.append(material)
    uv = mesh.uv_layers.new(name="Turned grain")
    for face in mesh.polygons:
        row, side = divmod(face.index, sides)
        if row >= len(rows) - 1:
            face.material_index = materials.index(mop_cap if face.index == len(faces) - 2 else alu)
            continue
        face.material_index = materials.index(rows[row][2])
        face.use_smooth = True
        for loop, u_i, v_i in zip(face.loop_indices, (side, side + 1, side + 1, side), (row, row, row + 1, row + 1)):
            uv.data[loop].uv = ((rows[v_i][0] + .10) * 4, u_i / sides)

    def add_tube_path(name, pts_3d, radius, mat, tube_sides=6, closed=False):
        n_pts = len(pts_3d)
        t_verts = []
        t_faces = []
        for idx, p in enumerate(pts_3d):
            p_vec = Vector(p)
            if closed:
                p_prev = Vector(pts_3d[(idx - 1) % n_pts])
                p_next = Vector(pts_3d[(idx + 1) % n_pts])
                tang = (p_next - p_prev).normalized()
            elif idx == 0:
                tang = (Vector(pts_3d[1]) - p_vec).normalized()
            elif idx == n_pts - 1:
                tang = (p_vec - Vector(pts_3d[-2])).normalized()
            else:
                tang = (Vector(pts_3d[idx + 1]) - Vector(pts_3d[idx - 1])).normalized()
            up = Vector((0, 1, 0)) if abs(tang.y) < 0.9 else Vector((1, 0, 0))
            b1 = tang.cross(up).normalized()
            b2 = tang.cross(b1).normalized()
            for s_i in range(tube_sides):
                ang = s_i * math.tau / tube_sides
                pt = p_vec + b1 * (radius * math.cos(ang)) + b2 * (radius * math.sin(ang))
                t_verts.append(rl_pos((pt.x, pt.y, pt.z)))
        ring_count = n_pts if closed else (n_pts - 1)
        for r_i in range(ring_count):
            nr = (r_i + 1) % n_pts
            for s_i in range(tube_sides):
                ns = (s_i + 1) % tube_sides
                t_faces.append((r_i * tube_sides + s_i, r_i * tube_sides + ns, nr * tube_sides + ns, nr * tube_sides + s_i))
        m_data = bpy.data.meshes.new(name)
        m_data.from_pydata(t_verts, [], t_faces)
        for poly in m_data.polygons:
            poly.use_smooth = True
        m_data.materials.append(mat)
        o_tube = bpy.data.objects.new(name, m_data)
        bpy.context.collection.objects.link(o_tube)
        return o_tube

    # 2) Twisted galvanized wire lashing + bent coat-hanger hoop legs
    for turn_i in range(3):
        zt = 0.142 + turn_i * 0.011
        ring_pts = [(0.0158 * math.cos(k * math.tau / 16), 0.0158 * math.sin(k * math.tau / 16), zt) for k in range(16)]
        add_tube_path(f"WireLashing_{turn_i}", ring_pts, 0.0017, steel_wire, tube_sides=6, closed=True)

    hoop_cx, hoop_cy, hoop_cz = 0.0, 0.008, 0.304
    hoop_rx, hoop_rz = 0.106, 0.112
    for s_sign in (-1, 1):
        strut_pts = [
            (s_sign * 0.009, 0.002, 0.165),
            (s_sign * 0.018, 0.005, 0.182),
            (s_sign * 0.036, 0.008, 0.198),
        ]
        add_tube_path(f"HoopNeckStrut_{s_sign}", strut_pts, 0.0036, steel_wire, tube_sides=8, closed=False)

    # 3) Bent steel wire hoop + canvas net rim sleeve + blue electrical tape front patch
    n_hoop = 36
    hoop_pts = []
    for k in range(n_hoop):
        a = k * math.tau / n_hoop
        # slight authentic 90s hand-bent wobble
        wobble = 1.0 + 0.018 * math.cos(a * 3.0)
        hx = hoop_cx + hoop_rx * math.sin(a) * wobble
        hz = hoop_cz - hoop_rz * math.cos(a) * wobble
        hy = hoop_cy + 0.004 * math.sin(a * 2.0)
        hoop_pts.append((hx, hy, hz))
    add_tube_path("GalvanizedWireHoop", hoop_pts, 0.0038, steel_wire, tube_sides=8, closed=True)

    # Canvas sleeve around main span of hoop + blue tape wrap at front apex
    sleeve_pts = [hoop_pts[k % n_hoop] for k in range(2, n_hoop - 1)]
    add_tube_path("NetCanvasRimSleeve", sleeve_pts, 0.0054, rim_canvas, tube_sides=8, closed=False)
    front_tape_pts = [hoop_pts[k % n_hoop] for k in range(n_hoop // 2 - 2, n_hoop // 2 + 3)]
    add_tube_path("FrontHoopBlueTapePatch", front_tape_pts, 0.0064, tape, tube_sides=8, closed=False)

    # 4) True 3D Open-Lattice Woven Diamond Mesh Net Bag draping off the hoop (~3,800 tris)
    def net_bag_point(u, theta):
        # u in [0..1] from hoop (0) to tied bottom apex (1)
        s = (math.cos(u * math.pi * 0.47) ** 0.76) * (1.0 - 0.86 * (u ** 3.2))
        s = max(0.06, s)
        rx = hoop_rx * 0.96 * s
        rz = hoop_rz * 0.96 * s
        # Natural trailing bag drape downward and slightly backward
        cy = hoop_cy - 0.162 * math.sin(u * math.pi * 0.52)
        cz = hoop_cz - 0.028 * (u ** 1.25)
        wobble = 1.0 + 0.025 * math.sin(u * math.pi) * math.cos(theta * 5.0)
        return (
            hoop_cx + rx * math.sin(theta) * wobble,
            cy + 0.003 * math.sin(theta * 2.0) * (1.0 - u),
            cz - rz * math.cos(theta) * wobble,
        )

    n_spokes = 8
    n_u_steps = 7
    twist_span = 1.45
    for idx in range(n_spokes):
        theta0 = idx * math.tau / n_spokes
        pts_l = []
        pts_r = []
        for step in range(n_u_steps):
            u = step / (n_u_steps - 1)
            pts_l.append(net_bag_point(u, theta0 + twist_span * u))
            pts_r.append(net_bag_point(u, theta0 - twist_span * u))
        mat_c = net_accent if idx % 3 == 0 else net_cord
        add_tube_path(f"NetDiamondCordL_{idx}", pts_l, 0.0017, mat_c, tube_sides=4, closed=False)
        add_tube_path(f"NetDiamondCordR_{idx}", pts_r, 0.0017, net_cord, tube_sides=4, closed=False)

    for r_idx, u_ring in enumerate((0.22, 0.46, 0.70, 0.92)):
        ring_pts = [net_bag_point(u_ring, k * math.tau / 20) for k in range(20)]
        mat_r = tape if r_idx == 3 else (net_accent if r_idx == 1 else net_cord)
        rad_r = 0.0038 if r_idx == 3 else 0.0017
        add_tube_path(f"NetLatitudeRing_{r_idx}", ring_pts, rad_r, mat_r, tube_sides=4, closed=True)

    # Bottom tied net knot & blue electrical tape cinch at apex
    apex = net_bag_point(1.0, 0.0)
    add_sphere("NetBottomKnot", (apex[0], apex[1] - 0.005, apex[2]), 0.009, rim_canvas, segments=14, ring_count=10)
    finalize_and_export("weapon_net_90s.glb", preserve_smoothing=True)


# ==============================================================================
# 12. WEAPON 4: SAWED-OFF DOUBLE-BARREL SHOTGUN (weapon_shotgun_90s.glb)
# ==============================================================================
def build_weapon_shotgun():
    clear_scene()
    m_walnut       = make_mat("DarkWalnut", (0.16, 0.072, 0.034, 1), roughness=0.36)
    m_walnut_check = make_mat("CheckeredWalnut", (0.095, 0.042, 0.018, 1), roughness=0.58)
    m_receiver     = make_mat("ReceiverSteel", (.065, .082, .103, 1), metallic=.89, roughness=.34)
    m_steel_dark   = make_mat("DarkSteelTrim", (.043, .055, .072, 1), metallic=.85, roughness=.39)
    m_barrel       = make_mat("BluedSteel", (.032, .045, .061, 1), metallic=.90, roughness=.29)
    m_bore         = make_mat("BoreBlack", (0.002, 0.002, 0.003, 1), metallic=0.0, roughness=1.0)
    m_brass        = make_mat("BrassBead", (0.96, 0.78, 0.22, 1), metallic=0.90, roughness=0.16)

    walnut = ASSETS / "external_cc0/walnut_veneer"
    # Bake the oil stain into the photo for the game's diffuse-only shader.
    import numpy as np
    original = bpy.data.images.load(str(walnut / "walnut_veneer_diff_1k.jpg"))
    pixels = np.empty(len(original.pixels), dtype=np.float32)
    original.pixels.foreach_get(pixels)
    pixels.reshape(-1, 4)[:, :3] *= (.35, .24, .17)
    stained = bpy.data.images.new("Oiled walnut veneer", *original.size)
    stained.pixels.foreach_set(pixels)
    stained.pack()
    rough = bpy.data.images.load(str(walnut / "walnut_veneer_rough_1k.jpg"))
    rough.colorspace_settings.name = "Non-Color"
    for material in (m_walnut, m_walnut_check):
        tree = material.node_tree
        shader = tree.nodes.get("Principled BSDF")
        for image, socket in ((stained, "Base Color"), (rough, "Roughness")):
            tex = tree.nodes.new("ShaderNodeTexImage")
            tex.image = image
            tree.links.new(tex.outputs["Color"], shader.inputs[socket])

    def smooth(obj):
        for face in obj.data.polygons:
            face.use_smooth = len(face.vertices) <= 4
        return obj

    def curve(name, points, radius, material):
        data = bpy.data.curves.new(name, 'CURVE')
        data.dimensions = '3D'
        data.bevel_depth = radius
        data.bevel_resolution = 8
        spline = data.splines.new('BEZIER')
        spline.bezier_points.add(len(points) - 1)
        for p, co in zip(spline.bezier_points, points):
            p.co = rl_pos(co)
            p.handle_left_type = p.handle_right_type = 'AUTO'
        obj = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(obj)
        data.materials.append(material)
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.convert(target='MESH')
        return smooth(obj)

    def catmull_rom(p0, p1, p2, p3, t):
        t2 = t * t
        t3 = t2 * t
        return 0.5 * (
            (2.0 * p1)
            + (-p0 + p2) * t
            + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t2
            + (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * t3
        )

    def sample_control_table(ctrl, num_samples):
        n = len(ctrl)
        out = []
        for s in range(num_samples):
            u = s / (num_samples - 1) * (n - 1)
            idx = min(int(u), n - 2)
            frac = u - idx
            p0 = ctrl[max(0, idx - 1)]
            p1 = ctrl[idx]
            p2 = ctrl[min(n - 1, idx + 1)]
            p3 = ctrl[min(n - 1, idx + 2)]
            row = tuple(catmull_rom(p0[c], p1[c], p2[c], p3[c], frac) for c in range(len(p1)))
            out.append(row)
        return out

    # 1) SILKY-SMOOTH HIGH-POLY DARK WALNUT PISTOL GRIP WITH FLUSH STEEL CAP & BRASS MEDALLION
    # Single continuous C^inf oval loft (56 rings x 48 segments) with zero bottom step and zero longitudinal creases.
    grip_ctrl = [
        # (y,       z,       rx,     rz)
        (-0.1112,  0.1036, 0.0025, 0.0023),  # flush brass medallion center dome
        (-0.1104,  0.1029, 0.0108, 0.0100),  # flush brass medallion rim
        (-0.1086,  0.1014, 0.0182, 0.0168),  # rounded steel grip cap bevel
        (-0.1042,  0.0980, 0.0230, 0.0206),  # flush steel grip cap border
        (-0.1000,  0.0950, 0.0231, 0.0207),  # walnut pommel swell (100% flush with steel cap)
        (-0.0860,  0.0850, 0.0222, 0.0210),  # lower palm curve
        (-0.0650,  0.0685, 0.0218, 0.0212),  # mid palm swell
        (-0.0420,  0.0495, 0.0208, 0.0210),  # upper grip finger wrap
        (-0.0200,  0.0270, 0.0204, 0.0228),  # slender upper wrist waist
        (-0.0040,  0.0060, 0.0218, 0.0235),  # receiver tang transition
        ( 0.0090, -0.0080, 0.0212, 0.0215),  # upper wrist crest meeting receiver rear seamlessly
        ( 0.0185, -0.0220, 0.0195, 0.0180),  # action socket join (tucked inside receiver)
    ]
    rings = sample_control_table(grip_ctrl, 56)
    n_circ = 48
    verts = []
    for y, z, rx, rz in rings:
        for i in range(n_circ):
            ang = i * math.tau / n_circ
            # C^inf smooth oval cross-section (sin^2 instead of abs(sin) eliminates side ridge kink)
            sx = rx * math.cos(ang) * (0.94 + 0.06 * (math.sin(ang) ** 2))
            sz = rz * math.sin(ang)
            verts.append(rl_pos((sx, y, z + sz)))
    faces = [
        (k * n_circ + i, k * n_circ + (i + 1) % n_circ, (k + 1) * n_circ + (i + 1) % n_circ, (k + 1) * n_circ + i)
        for k in range(len(rings) - 1) for i in range(n_circ)
    ]
    faces += [tuple(reversed(range(n_circ))), tuple(range(len(verts) - n_circ, len(verts)))]
    mesh = bpy.data.meshes.new('ErgonomicWalnutGrip')
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    grip = bpy.data.objects.new('ErgonomicWalnutGrip', mesh)
    bpy.context.collection.objects.link(grip)
    mesh.materials.append(m_walnut)       # 0: oiled walnut
    mesh.materials.append(m_walnut_check) # 1: dark checkered walnut side panels
    mesh.materials.append(m_receiver)     # 2: flush blued-steel pistol grip cap
    mesh.materials.append(m_brass)        # 3: flush inlaid brass medallion
    uv = mesh.uv_layers.new(name="UVMap")
    for face in mesh.polygons:
        for index in face.loop_indices:
            row, column = divmod(mesh.loops[index].vertex_index, n_circ)
            if face.index < (len(rings) - 1) * n_circ and face.index % n_circ == n_circ - 1 and column == 0:
                column = n_circ
            uv.data[index].uv = (.20 + .60 * column / n_circ, .12 + .72 * row / (len(rings) - 1))

    n_ring_quads = (len(rings) - 1) * n_circ
    for k in range(len(rings) - 1):
        u_ring = k / (len(rings) - 1)
        for i in range(n_circ):
            poly_idx = k * n_circ + i
            ang = (i + 0.5) * math.tau / n_circ
            if k < 4:
                mesh.polygons[poly_idx].material_index = 3  # flush brass medallion center
            elif k < 10:
                mesh.polygons[poly_idx].material_index = 2  # flush steel pommel cap
            elif u_ring >= 0.78 and math.sin(ang) < -0.90:
                mesh.polygons[poly_idx].material_index = 2  # flush-inlaid upper steel tang on top of wrist
            elif 0.24 <= u_ring <= 0.78 and abs(math.cos(ang)) > 0.74 and abs(math.sin(ang)) < 0.58:
                mesh.polygons[poly_idx].material_index = 1  # flush checkered side panel
    mesh.polygons[n_ring_quads].material_index = 3  # bottom medallion cap
    for poly in mesh.polygons:
        poly.use_smooth = True

    # Subtle flush brass grip escutcheon pin on the side of the dark walnut wrist
    smooth(add_rod("GripEscutcheonPin", (-0.0196, -0.056, 0.062), (0.0196, -0.056, 0.062), 0.0024, 0.0024, m_brass, 16))

    # 2) LOW-PROFILE SCULPTED STEEL BOXLOCK RECEIVER (NO CARTOON BALLS, NO REAR STEP!)
    # Smoothly lofted 36-slice x 48-seg receiver body that matches the twin barrels at the standing breech
    # (z = -0.092) and tapers flush into the walnut stock wrist (z = 0.004). All z >= -0.092 so it stays stationary.
    rec_slices = 36
    rec_circ = 48
    rec_verts = []
    for s in range(rec_slices):
        u = s / (rec_slices - 1)
        z = -0.092 + u * 0.096  # -0.092 (standing breech) -> +0.004 (flush stock wrist join)
        t_morph = min(1.0, max(0.0, (u - 0.14) / 0.86))
        blend = 0.5 - 0.5 * math.cos(math.pi * t_morph)
        for i in range(rec_circ):
            ang = i * math.tau / rec_circ
            ca, sa = math.cos(ang), math.sin(ang)
            # Front standing breech profile (matches twin barrels at x = +-0.0175, y = 0.024, r = 0.0170)
            top_mask = max(0.0, sa)
            scallop = 1.0 - 0.13 * (math.cos(2.0 * ang) ** 2) * (top_mask ** 1.5)
            bx_front = ca * (0.0246 + 0.0086 * (top_mask ** 0.85))
            by_front = (0.0135 + 0.0265 * sa) * scallop
            if sa < 0.0:
                by_front = 0.0105 + 0.0225 * sa * (0.86 + 0.14 * (ca ** 2))
            # Rear oval wrist profile (matches ErgonomicWalnutGrip at z = +0.004 seamlessly with zero step)
            rear_taper = 1.0 - 0.16 * (max(0.0, (u - 0.82) / 0.18) ** 2)
            bx_rear = 0.0214 * ca * (0.94 + 0.06 * (sa ** 2)) * rear_taper
            by_rear = 0.0010 + (0.0185 if sa >= 0.0 else 0.0215) * sa * rear_taper
            x = (1.0 - blend) * bx_front + blend * bx_rear
            y = (1.0 - blend) * by_front + blend * by_rear
            rec_verts.append(rl_pos((x, y, z)))
    rec_faces = [
        (k * rec_circ + i, k * rec_circ + (i + 1) % rec_circ, (k + 1) * rec_circ + (i + 1) % rec_circ, (k + 1) * rec_circ + i)
        for k in range(rec_slices - 1) for i in range(rec_circ)
    ]
    rec_faces += [tuple(reversed(range(rec_circ))), tuple(range(len(rec_verts) - rec_circ, len(rec_verts)))]
    rec_mesh = bpy.data.meshes.new('ReceiverActionBody')
    rec_mesh.from_pydata(rec_verts, [], rec_faces)
    rec_mesh.update()
    rec_obj = bpy.data.objects.new('ReceiverActionBody', rec_mesh)
    bpy.context.collection.objects.link(rec_obj)
    rec_mesh.materials.append(m_receiver)
    smooth(rec_obj)

    for side in (-1, 1):
        # Smooth organic teardrop sidelock cheek plates (no rectangular corners clipping the wood!)
        cheek = add_sphere(f"SideCheekPlate_{side}", (side * 0.0228, 0.0075, -0.048), 1.0, m_receiver,
                           scale=(0.0026, 0.0115, 0.033), segments=32, ring_count=18)
        smooth(cheek)
        # Flush gunsmith pins & brass hinge pin rosette on the side cheeks
        for s_idx, (sy, sz, s_rad, s_mat) in enumerate([
            (0.009, -0.028, 0.0024, m_steel_dark),
            (0.005, -0.052, 0.0024, m_steel_dark),
            (0.005, -0.076, 0.0038, m_brass),  # Main hinge pin rosette
        ]):
            sx_in  = side * 0.0220
            sx_out = side * 0.0250
            smooth(add_rod(f"ReceiverScrew_{side}_{s_idx}", (sx_in, sy, sz), (sx_out, sy, sz), s_rad, s_rad, s_mat, 18))

    # Sleek, flat, low-profile Westley Richards top locking lever hugging the receiver top slope (no sticking-out pipe!)
    lev_ctrl = [
        # (x,      y,      z,      rx,     ry)
        (0.0000, 0.0365, -0.062, 0.0046, 0.0018),
        (0.0005, 0.0352, -0.050, 0.0038, 0.0017),
        (0.0018, 0.0315, -0.035, 0.0030, 0.0016),
        (0.0035, 0.0262, -0.020, 0.0032, 0.0016),
        (0.0048, 0.0218, -0.010, 0.0052, 0.0018),
        (0.0052, 0.0202, -0.004, 0.0038, 0.0015),
    ]
    lev_rings = sample_control_table(lev_ctrl, 20)
    lev_circ = 24
    lev_verts = []
    for lx, ly, lz, lrx, lry in lev_rings:
        for i in range(lev_circ):
            ang = i * math.tau / lev_circ
            lev_verts.append(rl_pos((lx + lrx * math.cos(ang), ly + lry * math.sin(ang), lz)))
    lev_faces = [
        (k * lev_circ + i, k * lev_circ + (i + 1) % lev_circ, (k + 1) * lev_circ + (i + 1) % lev_circ, (k + 1) * lev_circ + i)
        for k in range(len(lev_rings) - 1) for i in range(lev_circ)
    ]
    lev_faces += [tuple(reversed(range(lev_circ))), tuple(range(len(lev_verts) - lev_circ, len(lev_verts)))]
    lev_mesh = bpy.data.meshes.new('TopLockingLever')
    lev_mesh.from_pydata(lev_verts, [], lev_faces)
    lev_mesh.update()
    lev_obj = bpy.data.objects.new('TopLockingLever', lev_mesh)
    bpy.context.collection.objects.link(lev_obj)
    lev_mesh.materials.append(m_steel_dark)
    smooth(lev_obj)

    # Single-piece sculpted S-curve forged external hammers (continuous loft, zero disconnected parts!)
    for side in (-1, 1):
        hx = side * 0.0242
        smooth(add_rod(f"HammerBoss_{side}", (hx - side * 0.0020, 0.012, -0.042), (hx + side * 0.0020, 0.012, -0.042), 0.0054, 0.0054, m_receiver, 24))
        smooth(add_rod(f"HammerPivotScrew_{side}", (hx - side * 0.0025, 0.012, -0.042), (hx + side * 0.0025, 0.012, -0.042), 0.0024, 0.0024, m_brass, 16))
        ham_ctrl = [
            # (y,      z,      rx,     rz)
            (0.0120, -0.0420, 0.0017, 0.0045),  # pivot base
            (0.0210, -0.0455, 0.0017, 0.0042),  # lower S-neck
            (0.0295, -0.0525, 0.0018, 0.0052),  # striker breast near firing pin bolster
            (0.0355, -0.0440, 0.0018, 0.0040),  # upper hammer crest
            (0.0395, -0.0345, 0.0022, 0.0032),  # swept-back thumb spur mid
            (0.0415, -0.0275, 0.0024, 0.0020),  # thumb spur tip
        ]
        ham_rings = sample_control_table(ham_ctrl, 24)
        ham_circ = 20
        ham_verts = []
        for hy, hz, hrx, hrz in ham_rings:
            for i in range(ham_circ):
                ang = i * math.tau / ham_circ
                ham_verts.append(rl_pos((hx + hrx * math.cos(ang), hy + 0.0015 * math.sin(ang), hz + hrz * math.sin(ang))))
        ham_faces = [
            (k * ham_circ + i, k * ham_circ + (i + 1) % ham_circ, (k + 1) * ham_circ + (i + 1) % ham_circ, (k + 1) * ham_circ + i)
            for k in range(len(ham_rings) - 1) for i in range(ham_circ)
        ]
        ham_faces += [tuple(reversed(range(ham_circ))), tuple(range(len(ham_verts) - ham_circ, len(ham_verts)))]
        ham_mesh = bpy.data.meshes.new(f"ForgedHammer_{side}")
        ham_mesh.from_pydata(ham_verts, [], ham_faces)
        ham_mesh.update()
        ham_obj = bpy.data.objects.new(f"ForgedHammer_{side}", ham_mesh)
        bpy.context.collection.objects.link(ham_obj)
        ham_mesh.materials.append(m_steel_dark)
        smooth(ham_obj)

    # Sculpted trigger guard & twin curved triggers (anchored solidly into bottom of receiver)
    curve("TriggerGuardBow", [(0, -0.006, -0.076), (0, -0.044, -0.072), (0, -0.052, -0.042),
                              (0, -0.043, -0.010), (0, -0.012,  0.006)], 0.0032, m_steel_dark)
    for t_idx, (tx, tz) in enumerate([(-0.0045, -0.056), (0.0045, -0.034)]):
        curve(f"ShotgunTrigger_{t_idx}", [(tx, -0.006, tz), (tx, -0.032, tz + 0.005), (tx, -0.040, tz - 0.002)], 0.0022, m_brass if t_idx == 0 else m_receiver)

    # 3) SILKY-SMOOTH DARK WALNUT SPLINTER FOREND WITH FLUSH CHECKERING & STEEL DEELEY LATCH
    forend = add_sphere("DarkWalnutForend", (0, -0.003, -0.205), 1.0, m_walnut,
                        scale=(0.028, 0.019, 0.110), segments=48, ring_count=28)
    forend.data.materials.append(m_walnut_check)
    for loop in forend.data.loops:
        x, y, z = forend.data.vertices[loop.vertex_index].co
        forend.data.uv_layers.active.data[loop.index].uv = (.5 + x * 3.7 + z * 5.1, .5 + y * 4)
    for poly in forend.data.polygons:
        poly.use_smooth = True
        c = poly.center
        # Flush checkered side panels on the walnut forend (no protruding rods!)
        if abs(c.x) > 0.018 and 0.145 <= c.y <= 0.265 and -0.011 <= c.z <= 0.006:
            poly.material_index = 1
    # Bright steel Deeley forend release latch & flush oval steel forend iron knuckle (no hanging drum!)
    smooth(add_box("ForendSteelLatch", (0, -0.021, -0.205), (0.0095, 0.0035, 0.068), m_receiver, bevel=0.0015))
    smooth(add_sphere("ForendIronTip", (0, -0.001, -0.106), 1.0, m_receiver, scale=(0.0235, 0.0135, 0.018), segments=36, ring_count=20))

    # 4) HIGH-POLY 48-SEGMENT TWIN BLUED BARRELS WITH DEEP DARK HOLLOW CHAMBERS (z = -0.092 -> -0.165, r = 0.0096m)
    def add_lofted_tube(name, bx, by, ring_zr, mat, closed_loop=True):
        n_seg = 48
        n_rings = len(ring_zr)
        t_verts = []
        for tz, tr in ring_zr:
            for i in range(n_seg):
                ang = i * math.tau / n_seg
                t_verts.append(rl_pos((bx + tr * math.cos(ang), by + tr * math.sin(ang), tz)))
        t_faces = []
        limit = n_rings if closed_loop else (n_rings - 1)
        for k in range(limit):
            k_next = (k + 1) % n_rings
            for i in range(n_seg):
                a = k * n_seg + i
                b = k * n_seg + (i + 1) % n_seg
                c = k_next * n_seg + (i + 1) % n_seg
                d = k_next * n_seg + i
                t_faces.append((a, b, c, d))
        t_mesh = bpy.data.meshes.new(name)
        t_mesh.from_pydata(t_verts, [], t_faces)
        t_mesh.update()
        t_obj = bpy.data.objects.new(name, t_mesh)
        bpy.context.collection.objects.link(t_obj)
        t_mesh.materials.append(mat)
        return smooth(t_obj)

    for side in (-1, 1):
        bx = side * 0.0175
        by = 0.0240
        # Watertight hollow outer blued barrel wall (tucked inside BreechBand at z = -0.114)
        add_lofted_tube(f"BluedBarrel_{side}", bx, by, [
            (-0.1140, 0.0167),
            (-0.2800, 0.0163),
            (-0.4565, 0.0158),
            (-0.4565, 0.0138),
            (-0.1140, 0.0142),
        ], m_barrel)
        # Watertight annular steel breech band & chamfered chamber rim at z = -0.092 (hollow center r = 0.0098m, lo.Z = -0.118 < -0.100)
        add_lofted_tube(f"BreechBand_{side}", bx, by, [
            (-0.1180, 0.0169),
            (-0.0926, 0.0173),
            (-0.0917, 0.0168),
            (-0.0916, 0.0156),
            (-0.0916, 0.0114),
            (-0.0922, 0.0102),
            (-0.0942, 0.0098),
            (-0.1020, 0.0128),
            (-0.1180, 0.0152),
        ], m_receiver)
        # Watertight deep pitch-black hollow 12-gauge chamber cavity (z = -0.0926 -> -0.165, r = 0.0098m, lo.Z = -0.1665 < -0.100)
        add_lofted_tube(f"BreechChamberBore_{side}", bx, by, [
            (-0.0926, 0.00985),
            (-0.1250, 0.00975),
            (-0.1550, 0.00950),
            (-0.1650, 0.00920),
            (-0.1650, 0.00030),
            (-0.1665, 0.00030),
            (-0.1665, 0.01120),
            (-0.0935, 0.01120),
        ], m_bore)
        # Watertight annular steel muzzle crown (hollow center r = 0.0096m)
        add_lofted_tube(f"MuzzleCrown_{side}", bx, by, [
            (-0.4560, 0.0166),
            (-0.4660, 0.0164),
            (-0.4660, 0.0104),
            (-0.4640, 0.0096),
            (-0.4560, 0.0096),
        ], m_receiver)
        # Watertight deep dark muzzle bore cavity (z = -0.464 -> -0.390, r = 0.0096m)
        add_lofted_tube(f"HollowBore_{side}", bx, by, [
            (-0.4640, 0.00962),
            (-0.3900, 0.00920),
            (-0.3900, 0.00040),
            (-0.3885, 0.00040),
            (-0.3885, 0.01060),
            (-0.4640, 0.01060),
        ], m_bore)

    # Central joining rib & raised ventilated top sighting rib
    smooth(add_box("MidBarrelFiller", (0, 0.024, -0.280), (0.011, 0.011, 0.350), m_barrel, bevel=0.0015))
    smooth(add_box("VentilatedTopRib", (0, 0.0416, -0.276), (0.0092, 0.0032, 0.368), m_barrel, bevel=0.0012))
    for post_i in range(9):
        pz = -0.110 - post_i * 0.042
        smooth(add_box(f"RibBridgePost_{post_i}", (0, 0.0372, pz), (0.0082, 0.0065, 0.014), m_receiver if post_i in (0, 8) else m_barrel, bevel=0.0008))

    # Muzzle rib block + compact golden-brass front bead sight
    smooth(add_box("FrontSightBase", (0, 0.0432, -0.450), (0.0062, 0.0028, 0.012), m_receiver, bevel=0.0008))
    smooth(add_sphere("BrassFrontBead", (0, 0.0462, -0.450), 0.0024, m_brass, segments=18, ring_count=12))

    finalize_and_export("weapon_shotgun_90s.glb", preserve_smoothing=True)

# ==============================================================================
# 13. RENDER CRISP CENTERED WEAPON ICONS FOR HOTBAR SLOTS 1..4
# ==============================================================================
def render_centered_icon(glb_name, png_name, roll_deg=32.0):
    clear_scene()
    glb_path = os.path.join(OUT_MODELS, glb_name)
    bpy.ops.import_scene.gltf(filepath=glb_path)

    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object

    bbox = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    min_v = Vector((min(v.x for v in bbox), min(v.y for v in bbox), min(v.z for v in bbox)))
    max_v = Vector((max(v.x for v in bbox), max(v.y for v in bbox), max(v.z for v in bbox)))
    obj.location -= (min_v + max_v) * 0.5
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # Camera sees the 3/4 leather loop & buckle face, and the rich 3/4 side of each weapon.
    if "belt" in glb_name:
        direction = Vector((0.08, 0.72, -0.68))
    elif "solder" in glb_name or "net" in glb_name:
        direction = Vector((0.88, -0.34, 0.42))
    else:
        direction = Vector((1.0, -0.35, 0.52))
    rotation = (-direction).to_track_quat('-Z', 'Y')
    from mathutils import Quaternion
    rotation = rotation @ Quaternion(Vector((0,0,1)), math.radians(roll_deg))
    # Fit actual projected vertices rather than a world-axis bounding box.
    projected = [rotation.inverted() @ (obj.matrix_world @ v.co) for v in obj.data.vertices]
    low = Vector((min(v.x for v in projected), min(v.y for v in projected), 0))
    high = Vector((max(v.x for v in projected), max(v.y for v in projected), 0))
    obj.location -= rotation @ ((low+high)*.5)
    span = max(high.x-low.x, high.y-low.y)

    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 256
    scene.render.resolution_y = 256
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'

    cam_data = bpy.data.cameras.new("IconCam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = span * 1.07
    cam_obj = bpy.data.objects.new("IconCam", cam_data)
    scene.collection.objects.link(cam_obj)

    target = bpy.data.objects.new("Target", None)
    scene.collection.objects.link(target)
    target.location = (0, 0, 0)

    cam_obj.location = direction.normalized() * 2.5
    cam_obj.rotation_mode = 'QUATERNION'
    cam_obj.rotation_quaternion = rotation
    scene.camera = cam_obj

    for lname, lpos, energy, col in [
        ("SunKey",  (1.8, -2.5, 2.0), 6.2, (1.0, 0.96, 0.88)),
        ("SunFill", (-2.0, -2.0, 1.2), 3.8, (0.80, 0.90, 1.0)),
        ("SunRim",  (0.0, 2.2, 2.2),  4.5, (1.0, 0.88, 0.65)),
    ]:
        ldata = bpy.data.lights.new(name=lname, type='SUN')
        ldata.energy = energy
        ldata.color = col
        lobj = bpy.data.objects.new(name=lname, object_data=ldata)
        scene.collection.objects.link(lobj)
        lobj.location = lpos
        t = lobj.constraints.new(type='TRACK_TO')
        t.target = target
        t.track_axis = 'TRACK_NEGATIVE_Z'
        t.up_axis = 'UP_Y'

    out_png = os.path.join(OUT_ICONS, png_name)
    scene.render.filepath = out_png
    bpy.ops.render.render(write_still=True)
    print("Rendered icon:", out_png, os.path.getsize(out_png))

def build_weapon_uzi():
    # The partner and first-person SMG use the same swatches and mesh builder.
    import sys
    sys.path.insert(0, str(ASSETS.parent))
    from generate_anatomy import game_prop
    game_prop()


def render_hotbar_icons(selected=None):
    for slot, (name, angle) in enumerate((("belt", 18), ("solder", -36), ("uzi", -26), ("shotgun", 28)), 1):
        if selected is None or name == selected:
            render_centered_icon(f"weapon_{name}_90s.glb", f"icon_slot_{slot}_{name}.png", angle)


if __name__ == "__main__":
    if "--net-only" in __import__("sys").argv:
        build_weapon_net()
        render_centered_icon("weapon_net_90s.glb", "icon_slot_net.png", -36)
    elif "--solder-only" in __import__("sys").argv:
        build_weapon_solder()
        render_centered_icon("weapon_solder_90s.glb", "icon_slot_2_solder.png", -36)
    elif "--ficus-only" in __import__("sys").argv:
        build_ficus_tree()
    elif "--chair-only" in __import__("sys").argv:
        build_reception_chair()
    elif "--weapons-only" in __import__("sys").argv:
        build_weapon_belt()
        build_weapon_solder()
        build_weapon_uzi()
        build_weapon_shotgun()
        render_hotbar_icons()
    elif "--belt-only" in __import__("sys").argv:
        build_weapon_belt()
        render_centered_icon("weapon_belt_90s.glb", "icon_slot_1_belt.png", 18.0)
    elif "--audit-fix" in __import__("sys").argv:
        build_hr_reception_desk()
        build_weapon_belt()
        render_centered_icon("weapon_belt_90s.glb", "icon_slot_1_belt.png", 18.0)
    elif "--workshop6-icons" in __import__("sys").argv:
        render_centered_icon("weapon_uzi_90s.glb", "icon_slot_3_uzi.png", -26.0)
    elif "--workshop5" in __import__("sys").argv:
        build_weapon_shotgun()
    elif "--workshop5-icons" in __import__("sys").argv:
        render_centered_icon("weapon_uzi_90s.glb", "icon_slot_3_uzi.png", -26.0)
        render_centered_icon("weapon_shotgun_90s.glb", "icon_slot_4_shotgun.png", 28.0)
    elif "--reception-only" in __import__("sys").argv:
        build_hr_reception_desk()
    else:
        if "--icons-only" not in __import__("sys").argv:
            build_reception_chair()
            build_water_cooler()
            build_vending_machine()
            build_filing_cabinet()
            build_whiteboard()
            build_ficus_tree()
            build_trash_bin()
            build_server_rack()
            build_hr_reception_desk()
            build_bug_terminal()
            build_bug_frog()
            build_pendant_lamp()
            build_poster_frame()
            build_weapon_belt()
            build_weapon_solder()
            build_weapon_uzi()
            build_weapon_shotgun()

        render_hotbar_icons()
