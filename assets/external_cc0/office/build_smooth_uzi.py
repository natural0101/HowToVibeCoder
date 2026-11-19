import bpy
import bmesh
import math
import os
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "assets/models"

PALETTE = dict(
    GunMetal="353e49",
    MachinedSteel="525e6a",
    PolymerGrip="24282d",
    BrassBolt="eab63e",
    SelectorRed="e8242e",
    SelectorWhite="eef3f8",
)
MATS = {}

def xyz(p):
    return (p[0], -p[2], p[1])

def mat(key):
    if key not in MATS:
        rgb = bytes.fromhex(PALETTE[key])
        m = bpy.data.materials.new(key)
        m.diffuse_color = tuple(((x / 255 + .055) / 1.055) ** 2.4 for x in rgb) + (1,)
        m.use_nodes = True
        shader = m.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = m.diffuse_color
        if key in ("MachinedSteel", "BrassBolt"):
            shader.inputs["Metallic"].default_value = 0.90
            shader.inputs["Roughness"].default_value = 0.22
        elif key == "GunMetal":
            shader.inputs["Metallic"].default_value = 0.82
            shader.inputs["Roughness"].default_value = 0.30
        elif key == "PolymerGrip":
            shader.inputs["Metallic"].default_value = 0.08
            shader.inputs["Roughness"].default_value = 0.46
        else:
            shader.inputs["Roughness"].default_value = 0.25
        MATS[key] = m
    return MATS[key]

def finish(obj, name, key):
    obj.name = name
    obj.data.materials.append(mat(key))
    return obj

def apply_weighted_smooth(obj):
    for poly in obj.data.polygons:
        poly.use_smooth = True
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    wn = obj.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL")
    wn.mode = 'FACE_AREA'
    wn.weight = 50
    wn.keep_sharp = False
    bpy.ops.object.modifier_apply(modifier=wn.name)
    obj.select_set(False)
    return obj

def detail(label, p, size, key, bevel=.0035, segments=3):
    bpy.ops.mesh.primitive_cube_add(size=1, location=xyz(p))
    obj = bpy.context.object
    obj.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    finish(obj, label, key)
    if bevel > 0:
        bpy.context.view_layer.objects.active = obj
        mod = obj.modifiers.new("Stamped rounded edges", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.profile = 0.55
        mod.limit_method = 'ANGLE'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return apply_weighted_smooth(obj)

def cut(obj, p, size, bevel=.0025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=xyz(p))
    cutter = bpy.context.object
    cutter.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        bpy.context.view_layer.objects.active = cutter
        bmod = cutter.modifiers.new("BevelCutter", "BEVEL")
        bmod.width = bevel
        bmod.segments = 2
        bpy.ops.object.modifier_apply(modifier=bmod.name)
    bpy.context.view_layer.objects.active = obj
    mod = obj.modifiers.new("Through ventilation window", "BOOLEAN")
    mod.operation, mod.object = 'DIFFERENCE', cutter
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter, do_unlink=True)
    return apply_weighted_smooth(obj)

def tube(label, z, length, radius, key, y=.018, bevel=.0015):
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=radius, depth=length,
        location=xyz((0, y, z)), rotation=(math.pi / 2, 0, 0))
    obj = finish(bpy.context.object, label, key)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        bpy.context.view_layer.objects.active = obj
        mod = obj.modifiers.new("TubeChamfer", "BEVEL")
        mod.width = bevel
        mod.segments = 3
        mod.limit_method = 'ANGLE'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return apply_weighted_smooth(obj)

def smooth_wire(name, points, radius, key, closed=False, resolution=4):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = radius
    curve.bevel_resolution = resolution
    spline = curve.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for v, p in zip(spline.points, points):
        v.co = (*xyz(p), 1)
    spline.use_cyclic_u = closed
    o = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(o)
    o.data.materials.append(mat(key))
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.convert(target="MESH")
    for poly in o.data.polygons:
        poly.use_smooth = True
    o.select_set(False)
    return o

def build_smooth_uzi():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    metal   = "GunMetal"
    trim    = "MachinedSteel"
    polymer = "PolymerGrip"
    brass   = "BrassBolt"
    red_m   = "SelectorRed"
    white_m = "SelectorWhite"

    # 1) Blued Stamped Steel Upper Receiver with Smooth Pressed Radius & Sloping Rear Weld
    receiver = detail("Slim stamped receiver", (0, .007, .014), (.038, .046, .296), metal, bevel=.0065, segments=4)
    for v in receiver.data.vertices:
        if v.co.y < -.10:
            v.co.y += max(0, v.co.z) * .45
    apply_weighted_smooth(receiver)

    # 2) Smooth Curved Milled Steel Top Cover (32-segment arch with beveled ends)
    arc = [(.0195 * math.cos(i * math.pi / 32), .029 + .0185 * math.sin(i * math.pi / 32)) for i in range(33)]
    pts = [xyz((x, y, z)) for z in (-.124, .140) for x, y in arc]
    faces = [(i, i + 1, 34 + i, 33 + i) for i in range(32)]
    faces += [tuple(reversed(range(33))), tuple(range(33, 66)), (0, 33, 65, 32)]
    data = bpy.data.meshes.new("MilledSteelBoltCover")
    data.from_pydata(pts, [], faces)
    data.update()
    cover = bpy.data.objects.new("MilledSteelBoltCover", data)
    bpy.context.collection.objects.link(cover)
    finish(cover, "MilledSteelBoltCover", trim)
    apply_weighted_smooth(cover)

    for sx in (-.009, .009):
        detail("TopCoverRib", (sx, .046, .008), (.0032, .0028, .228), metal, bevel=.0012, segments=3)
    tube("RecoilSpringEndButton", .156, .010, .0082, trim, y=.016, bevel=.0015)
    detail("RearReceiverEndPlate", (0, .008, .155), (.035, .037, .007), trim, bevel=.0025, segments=3)

    # Classic Uzi Top Cocking Knob with Sighting Slot
    # CRITICAL RELOAD INVARIANT: lo.Y > 0.044f && lo.Z > -0.060f && hi.Z < -0.012f
    detail("TopCockingSliderBase", (0, .0485, -.035), (.018, .006, .032), metal, bevel=.002, segments=3)
    cock_knob = detail("TopCockingKnob", (0, .0565, -.035), (.025, .012, .023), trim, bevel=.0035, segments=3)
    cut(cock_knob, (0, .0595, -.035), (.0075, .010, .028), bevel=.0015)

    # 3) Smooth Pressed Stamped Reinforcing Ribs on Both Receiver Sides (no jagged double-box aliasing!)
    for side in (-1, 1):
        sx = side * .0188
        detail(f"StampedRibLong_{side}", (sx, -.006, .012), (.0042, .0062, .246), trim, bevel=.0026, segments=3)
        for ry in (.006, .018):
            detail(f"StampedRibRear_{side}_{ry}", (sx, ry, .092), (.0040, .0052, .084), trim, bevel=.0022, segments=3)
        detail(f"StampedRibFront_{side}", (sx, .012, -.076), (.0040, .0056, .072), trim, bevel=.0022, segments=3)

    # 4) Open Ejection Window with Gleaming Polished Brass Bolt
    cut(receiver, (.019, .014, .010), (.012, .022, .068), bevel=.002)
    detail("EjectionPortRim", (.0182, .014, .010), (.0028, .0235, .071), trim, bevel=.0012, segments=3)
    detail("PolishedBrassBolt", (.0145, .014, .010), (.007, .020, .066), brass, bevel=.0025, segments=3)
    detail("BrassExtractorSlot", (.0176, .016, .022), (.0026, .0048, .027), metal, bevel=.001, segments=2)
    tube("BoltRecoilRod", -.005, .058, .0020, trim, y=.021, bevel=.0004)

    # 5) Lower Receiver Frame, Fire Selector Housing & S / R / A Markings
    detail("LowerReceiverFrame", (0, -.025, .010), (.0355, .025, .194), metal, bevel=.0065, segments=4)
    for side in (-1, 1):
        sx = side * .0186
        detail(f"SelectorHousing_{side}", (sx, -.024, .048), (.0048, .018, .054), trim, bevel=.0025, segments=3)
        detail(f"SelectorSliderSwitch_{side}", (side * .0210, -.026, .046), (.0038, .010, .013), polymer, bevel=.0018, segments=3)
        detail(f"Mark_S_Dot_{side}", (side * .0208, -.018, .065), (.0022, .0042, .0042), white_m, bevel=.001, segments=2)
        detail(f"Mark_R_Dot_{side}", (side * .0208, -.018, .048), (.0022, .0044, .0044), red_m,   bevel=.001, segments=2)
        detail(f"Mark_A_Dot_{side}", (side * .0208, -.018, .031), (.0022, .0046, .0046), red_m,   bevel=.001, segments=2)
        for pz in (-.058, .088):
            bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=.0036, depth=.041,
                location=xyz((0, -.025, pz)), rotation=(0, math.pi / 2, 0))
            pin = finish(bpy.context.object, f"ReceiverPin_{pz}_{side}", trim)
            apply_weighted_smooth(pin)

    # 6) Contoured Ergonomic Polymer Pistol Grip, Rounded Grip Safety & Extended Magazine
    # Grip envelope preserved so hand fingers remain at 0.0mm overlap!
    detail("GripFrameStamping", (0, -.118, .018), (.0435, .185, .0575), metal, bevel=.009, segments=4)
    detail("PolymerGripPanels", (0, -.124, .018), (.0565, .206, .0695), polymer, bevel=.017, segments=5)
    # Contoured palm-swell grip safety bar (smoothly rounded instead of a sharp grey brick)
    detail("GripSafetyBar", (0, -.088, .0525), (.024, .078, .0095), trim, bevel=.0042, segments=4)
    # Flared magazine well collar at base of grip (Y = -0.218 > -0.232, stays with receiver)
    detail("MagWellCollar", (0, -.218, .018), (.046, .014, .060), metal, bevel=.0035, segments=3)

    # Removable Magazine (CRITICAL RELOAD INVARIANT: lo.Y < -0.232f && hi.Y < -0.200f)
    detail("ExtendedSteelMag", (0, -.236, .019), (.0355, .064, .0475), metal, bevel=.0048, segments=4)
    detail("MagazineFloorPlate", (0, -.264, .019), (.0425, .011, .0545), trim, bevel=.0035, segments=3)

    for side in (-1, 1):
        for i in range(8):
            detail(f"GripGroove_{side}_{i}", (side * .0278, -.062 - i * .017, .020), (.0026, .0032, .044), metal, bevel=.0012, segments=3)
        for sy in (-.072, -.168):
            bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=.0040, depth=.058,
                location=xyz((0, sy, .020)), rotation=(0, math.pi / 2, 0))
            scr = finish(bpy.context.object, f"GripEscutcheonScrew_{sy}_{side}", trim)
            apply_weighted_smooth(scr)

    guard = [(0, -.040, -.035), (0, -.046, -.088), (0, -.057, -.102),
             (0, -.084, -.102), (0, -.097, -.088), (0, -.097, -.025)]
    smooth_wire("TriggerGuard", guard, .0038, trim, resolution=4)
    smooth_wire("SteelTrigger", [(0, -.044, -.048), (0, -.065, -.066), (0, -.080, -.060)], .0032, brass, resolution=4)

    # 7) Perforated Front Barrel Shroud, Finned Barrel Nut & Slotted Muzzle Compensator
    shroud = detail("PerforatedBarrelShroud", (0, .018, -.173), (.0385, .0495, .115), metal, bevel=.011, segments=4)
    cut(shroud, (0, .018, -.173), (.028, .036, .140), bevel=.002)
    for z in (-.140, -.169, -.198):
        cut(shroud, (0, .018, z), (.100, .021, .0145), bevel=.002)
        cut(shroud, (0, .043, z), (.018, .021, .0145), bevel=.002)
    detail("RibbedForearm", (0, -.015, -.164), (.0375, .0185, .102), polymer, bevel=.0065, segments=4)
    for z in (-.125, -.139, -.153, -.167, -.181, -.195):
        detail(f"ForearmRib_{z}", (0, -.0218, z), (.0395, .0045, .0038), trim, bevel=.0015, segments=3)

    tube("MilledBarrelNut", -.234, .022, .0165, trim, bevel=.002)
    tube("InnerChromedBarrel", -.248, .085, .0115, metal, bevel=.001)

    comp = tube("MilledMuzzleCompensator", -.288, .068, .0182, trim, bevel=.0025)
    tube("CompensatorBrassCollar", -.258, .008, .0188, brass, bevel=.0012)
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=.0095, depth=.090,
        location=xyz((0, .018, -.288)), rotation=(math.pi / 2, 0, 0))
    bore = bpy.context.object
    bpy.context.view_layer.objects.active = comp
    mod = comp.modifiers.new("OpenMuzzleBore", "BOOLEAN")
    mod.object = bore
    mod.operation = 'DIFFERENCE'
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(bore, do_unlink=True)
    for z in (-.272, -.288, -.304):
        cut(comp, (0, .030, z), (.060, .0115, .0062), bevel=.001)

    # 8) Smooth Protective Iron Sights (rounded protective ears + crisp sight posts)
    for z, base_mat in ((-.126, metal), (.126, metal)):
        detail(f"SightBlock_{z}", (0, .038, z), (.033, .0135, .022), base_mat, bevel=.0035, segments=3)
        smooth_wire(f"SightEars_{z}", [(-.0165, .036, z), (-.0165, .055, z),
            (0, .0585, z), (.0165, .055, z), (.0165, .036, z)], .0032, trim, resolution=4)
    detail("FrontSightBlade", (0, .048, -.126), (.0038, .015, .0048), white_m, bevel=.001, segments=2)
    detail("FrontSightRedDot", (0, .0535, -.126), (.0042, .0042, .0052), red_m, bevel=.001, segments=2)
    detail("RearPeepAperture", (0, .0485, .126), (.0095, .0115, .0048), white_m, bevel=.001, segments=2)

    # 9) Side-Folding Skeleton Stock (flush low-profile hinge button instead of protruding yellow pegs)
    detail("RearStockHingeBlock", (.001, .008, .154), (.027, .033, .021), trim, bevel=.0045, segments=4)
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=.0055, depth=.033,
        location=xyz((0, .008, .154)), rotation=(0, math.pi / 2, 0))
    hinge_pin = finish(bpy.context.object, "StockHingePin", metal)
    apply_weighted_smooth(hinge_pin)
    bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=.0028, depth=.035,
        location=xyz((0, .008, .154)), rotation=(0, math.pi / 2, 0))
    hinge_core = finish(bpy.context.object, "StockHingeBrassCore", brass)
    apply_weighted_smooth(hinge_core)

    for y in (.024, -.012):
        smooth_wire(f"FoldedStockStrut_{y}", [(-.019, y, .150), (-.029, y, .112), (-.029, y, -.110)], .0036, trim, resolution=4)
    smooth_wire("FoldedButtplate", [(-.029, .026, -.110), (-.034, .020, -.142),
        (-.034, -.018, -.142), (-.029, -.014, -.110)], .0042, polymer, resolution=4)

    # Join all objects while preserving smooth normals
    mesh_objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in mesh_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objs[0]
    bpy.ops.object.join()
    gun = bpy.context.object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    gun.name = gun.data.name = "SMG"

    bm = bmesh.new()
    bm.from_mesh(gun.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-7)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-7)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(gun.data)
    bm.free()
    for poly in gun.data.polygons:
        poly.use_smooth = True
    gun.data.update()

    out_path = OUT / "weapon_uzi_90s.glb"
    bpy.ops.export_scene.gltf(
        filepath=str(out_path),
        export_format="GLB",
        export_normals=True,
        export_texcoords=False,
        export_animations=False
    )
    print("Exported smooth weapon_uzi_90s.glb:", out_path, os.path.getsize(out_path), "Polys:", len(gun.data.polygons))

if __name__ == "__main__":
    build_smooth_uzi()
