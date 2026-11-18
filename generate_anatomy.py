"""Blender 5.1: blender -b --python-exit-code 1 -P generate_anatomy.py
MPFB body topology and game-engine skin weights. Coordinates below are metres,
in game space (X, height, forward). Helpers supply joint centres only.
"""
import json
import math
import os
from pathlib import Path

import bpy
import bmesh
from mathutils import Matrix, Vector, Quaternion
from mathutils.bvhtree import BVHTree

V = Vector
DATA = Path(os.environ["APPDATA"]) / "Blender Foundation/Blender/5.1/extensions/blender_org/mpfb/data"
OUT = Path(__file__).resolve().parent / "assets/models"
BODY = []
REST = []
LANDMARKS = {}
group = ""
for line in (DATA / "3dobjs/base.obj").read_text().splitlines():
    fields = line.split()
    if not fields:
        continue
    if fields[0] == "v":
        REST.append(V(tuple(float(value) * .1 for value in fields[1:4])))
    elif fields[0] == "g":
        group = fields[1]
    elif fields[0] == "f" and (group == "body" or group.startswith("joint-")):
        face = tuple(int(c.split("/")[0]) - 1 for c in fields[1:])
        if group == "body":
            BODY.append(face)
        else:
            LANDMARKS.setdefault(group, set()).update(face)

used = sorted({i for f in BODY for i in f})
floor = min(REST[i].y for i in used)
for v in REST:
    v.y -= floor
JOINT = {name[6:]: sum((REST[i] for i in ids), V()) / len(ids)
         for name, ids in LANDMARKS.items()}
WEIGHTS = [[] for _ in REST]
for bone, entries in json.loads((DATA / "rigs/standard/weights.game_engine.json").read_text())["weights"].items():
    for i, w in entries:
        if w:
            WEIGHTS[i].append((bone, w))
DIGITS = ("thumb", "index", "middle", "ring", "pinky")

# Swatches in sRGB; glTF material factors are linear.
PALETTE = dict(skin="cc9d87", lip="9d574e", ivory="ebe5d5", iris="414334",
    hair="382b25", hairlight="51372b", coal="232732", onyx="1b2028",
    blue="426c97", navy="304d73", denim="37465c", sole="cec6b6",
    crimson="9d2543", lapel="bd3d54", plum="5f385e", velvet="512742",
    silk="83537e", gold="ce9951", teal="448f94", glass="33495a",
    laptop="929fa9", display="202f40", cyan="77c5be", wax="e7c59d",
    flame="fec45a", orb="9f87be", card="ddc49d", ink="53364a")
MATS = {}
PALETTE.update(titanium="303a49",jointmetal="586879",neon="ff145f")


def mat(key):
    if key not in MATS:
        rgb = bytes.fromhex(PALETTE[key])
        m = bpy.data.materials.new(key)
        m.diffuse_color = tuple(((x/255+.055)/1.055)**2.4 for x in rgb) + (1,)
        m.use_nodes = True
        shader = m.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = m.diffuse_color
        shader.inputs["Roughness"].default_value = .34 if key in ("gold","glass","orb","onyx") else .81
        if key in ("titanium","jointmetal"):
            shader.inputs["Metallic"].default_value=.8
            shader.inputs["Roughness"].default_value=.32
        if key=="neon":
            shader.inputs["Emission Color"].default_value=m.diffuse_color
            shader.inputs["Emission Strength"].default_value=2.5
        MATS[key] = m
    return MATS[key]


def xyz(p):
    return (p[0], -p[2], p[1])


def finish(obj, name, key):
    obj.name = name
    obj.data.materials.append(mat(key))
    return obj


def mesh(name, pts, faces, key):
    data = bpy.data.meshes.new(name)
    data.from_pydata([xyz(p) for p in pts], [], faces)
    data.update()
    if name in ("Black shirt","Notched lapel","Kangaroo pocket"):
        for poly in data.polygons:
            if poly.normal.y>0: poly.flip()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    return finish(obj, name, key)


def box(name, p, size, key, bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=xyz(p))
    obj = bpy.context.object
    obj.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new("Tailored edge", "BEVEL")
        mod.width, mod.segments = bevel, 1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(obj, name, key)


def oval(name, p, radii, key):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, location=xyz(p))
    obj = bpy.context.object
    obj.scale = (radii[0], radii[2], radii[1])
    return finish(obj, name, key)


def wire(name, points, radius, key, closed=False):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions, curve.bevel_depth, curve.bevel_resolution = "3D", radius, 1
    spline = curve.splines.new("POLY")
    spline.points.add(len(points)-1)
    for v, p in zip(spline.points, points):
        v.co = (*xyz(p), 1)
    spline.use_cyclic_u = closed
    o = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(o)
    o.data.materials.append(mat(key))
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.convert(target="MESH")
    o.select_set(False)


def loft(name, rings, key, sides=20):
    pts = []
    for h, rx, rz, front in rings:
        for j in range(sides):
            a = j*math.tau/sides
            pts.append((rx*math.sin(a), h, front+rz*math.cos(a)))
    faces = [(j+sides*k, (j+1)%sides+sides*k, (j+1)%sides+sides*(k+1), j+sides*(k+1))
             for k in range(len(rings)-1) for j in range(sides)]
    faces += [tuple(reversed(range(sides))),tuple(range(len(pts)-sides,len(pts)))]
    return mesh(name, pts, faces, key)


def frame(forward, across):
    f = V(forward).normalized()
    a = (V(across) - f*f.dot(V(across))).normalized()
    return Matrix((f,a,f.cross(a))).transposed()


# Contact frames in the same metre-space as the four runtime weapon assemblies.
# X is mirrored once by VisualEngine3D.Arm, as for the original MPFB arms.
def grip_frame(kind, side):
    right=side=="r"
    if kind=="fps_belt":
        return (V((-.20 if right else .20,-.165,.605)), V((-1 if right else 1,0,0)),
                V((0,.18,.984)).normalized(), V((0,.984,-.18)).normalized(), .026,.022)
    if kind=="fps_solder":
        c=V((-.18,-.15,.62) if right else (.16,-.15,.70))
        return c,V((0,0,1 if right else -1)),V((-1 if right else 1,0,0)),V((0,1,0)),.034 if right else .044,.034 if right else .044
    if kind=="fps_uzi":
        c=V((-.1805,-.193,.6665) if right else (-.18,-.125,.80))
        if right: return c,V((0,1,0)),V((-1,0,0)),V((0,0,1)),.040,.036
        return c,V((0,0,1)),V((0,-1,0)),V((-1,0,0)),.034,.025
    c=V((-.168,-.15,.625) if right else (-.18,-.125,.85))
    if right: return c,V((0,.79,.61)).normalized(),V((-1,0,0)),V((0,-.61,.79)).normalized(),.042,.034
    return c,V((0,0,1)),V((0,-1,0)),V((-1,0,0)),.040,.037


def contact_pose(kind, side):
    """Forward kinematics on MPFB joints. No skin projection or bone stretching.

    Flexion rotates around the transverse knuckle axis toward the palm.
    Every phalanx retains its rest length and the palm's orientation.
    """
    if side=="l" and kind in ("fps_uzi","fps_shotgun"):
        # Reuse the longitudinal under-forearm grip, including its opposed thumb.
        dest_wrist=V((-.148,-.162,.685) if kind=="fps_uzi" else (-.140,-.155,.77))
        return pose("fps",side,dest_wrist)
    j=lambda key: JOINT[side+"-"+key]
    c,axis,radial,tangent,rx,ry=grip_frame(kind,side)
    wrist=j("hand"); mid=j("finger-3-1")
    # MPFB hands are mirrored: orient BOTH rest normals into the palm.
    palm_sign=1 if side=="l" else -1
    f_rest=(mid-wrist).normalized()
    u_rest=(j("finger-2-1")-j("finger-5-1"))*palm_sign
    a_rest=(u_rest-f_rest*f_rest.dot(u_rest)).normalized()
    palm_rest=f_rest.cross(a_rest).normalized()
    rest_frame=Matrix((f_rest,a_rest,palm_rest)).transposed()
    f_target=tangent.normalized()
    palm_target=(-radial).normalized()
    a_target=palm_target.cross(f_target).normalized()
    target_frame=Matrix((f_target,a_target,palm_target)).transposed()
    hand=target_frame@rest_frame.transposed()
    assert abs(hand.determinant()-1)<1e-6
    assert (hand@palm_rest).dot(palm_target)>.999999
    knuckle=c+radial*rx+tangent*(.006 if kind=="fps_belt" else -.003)
    dest_wrist=knuckle-hand@(mid-wrist)
    dest_elbow=dest_wrist+V((-.12 if side=="r" else .15,-.19,-.28))
    if kind=="fps_shotgun" and side=="r": dest_elbow=dest_wrist+V((-.10,-.04,-.26))
    transforms={}
    for name,a,b,p,q in [("upperarm",j("shoulder"),j("elbow"),dest_elbow-V((0,0,.2)),dest_elbow),
                         ("lowerarm",j("elbow"),wrist,dest_elbow,dest_wrist)]:
        src=b-a; dst=q-p; u=src.normalized()
        stretch=Matrix.Identity(3)+Matrix([list(u*u[n]) for n in range(3)])*(dst.length/src.length-1)
        # Preserve the palm roll through the forearm. Shortest-arc rotation alone
        # leaves a 90-degree twist at the wrist and collapses weighted vertices.
        across=j("finger-2-1")-j("finger-5-1")
        rotation=frame(dst,hand@across)@frame(src,across).transposed()
        transforms[name+"_"+side]=(a,p,rotation@stretch)
    transforms["hand_"+side]=(wrist,dest_wrist,hand)
    signet=None
    for d,name in enumerate(DIGITS,1):
        chain=[j(f"finger-{d}-{k}") for k in range(1,5)]
        metacarpal=hand@(chain[0]-wrist)
        angles=[.85,1.10,.75]
        cup=roll=spread=total_angle=0
        if kind=="fps_belt" and d>=2:
            # Cupping and three joint flexions fitted to the sagging upper seam.
            belt_flex=(
                ((.097,.277,1.036,.948),(.250,.552,1.212,1.850),
                 (.278,.501,1.010,1.850),(.350,.348,.120,1.317)) if side=="r" else
                ((.337,.320,1.295,1.768),(.226,.747,1.105,1.850),
                 (.247,.679,.883,1.786),(.357,.481,.120,1.630)))
            cup,*angles=belt_flex[d-2]
        if kind=="fps_shotgun" and side=="r" and d>=3:
            # Each digit meets a different section of the tapered walnut.
            angles=[.78,1.02,.58]
            total_angle,spread,roll,cup=(
                (-.329,-.193,.047,-.293),(-.480,-.162,.157,-.210),
                (-1.148,.183,-.091,.024))[d-3]
        if roll:
            metacarpal=Quaternion(radial,roll)@metacarpal
        if cup:
            metacarpal=Quaternion(tangent.cross(-radial).normalized(),cup)@metacarpal
        pos=dest_wrist+metacarpal
        thumb_direction=(c+axis*.035+radial*.032+tangent*.025-pos).normalized()
        for k in range(3):
            rest=chain[k+1]-chain[k]
            if d==1:
                # Opposed thumb rests across the index proximal phalanx.
                # Keep a continuous thumb arc instead of turning back toward a
                # target already passed by the previous phalanx.
                direction=(thumb_direction-axis*(.12*k)-radial*(.12*k)).normalized()
                if kind=="fps_belt":
                    # Keep the thumb alongside its own index-side palm edge.
                    thumb_arc=(((.330,.837,-.369),(.050,.088,-.994),(-.420,.585,.739))
                               if side=="r" else
                               ((.447,.837,-.369),(.167,-.135,-.988),(-.303,.585,.739)))
                    lateral,lift,depth=thumb_arc[k]
                    direction=(axis*lateral+V((0,lift,depth))).normalized()
                rot=(hand@rest).rotation_difference(direction).to_matrix()@hand
            elif d==2 and side=="r" and kind in ("fps_uzi","fps_shotgun"):
                # Index extends toward the trigger; other fingers retain the grip.
                inward,up,forward=((.20,.50,.84),(.40,.20,.89),(.85,-.10,.51))[k]
                if kind=="fps_shotgun": up=(.12,0,-.10)[k]
                direction=(-radial*inward+axis*up+tangent*forward).normalized()
                rot=(hand@rest).rotation_difference(direction).to_matrix()@hand
            else:
                total_angle+=angles[k]
                direction=tangent*math.cos(total_angle)-radial*math.sin(total_angle)
                if kind=="fps_belt" and side=="r" and d==2:
                    direction=(direction-axis*.14).normalized()
                if spread:
                    direction=direction*math.cos(spread)+axis*math.sin(spread)
                rot=(hand@rest).rotation_difference(direction).to_matrix()@hand
            transforms[f"{name}_{k+1:02}_"+side]=(chain[k],pos.copy(),rot)
            delta=rot@rest
            assert abs(delta.length-rest.length)<1e-6
            assert rot.determinant()>.999
            if d==4 and k==0: signet=(pos+delta*.42,delta.normalized())
            pos+=delta
    return transforms,signet


def pose(kind, side, support_wrist=None):
    if kind.startswith("fps_"): return contact_pose(kind,side)
    sign = 1 if side=="l" else -1
    j = lambda key: JOINT[side+"-"+key]
    shoulder, elbow, wrist = [j(k) for k in ("shoulder","elbow","hand")]
    if kind=="fps":
        dest_elbow = V((.28,-.40,.26) if sign==1 else (-.38,-.40,.22))
        dest_wrist = V((-.085,-.185,.68) if sign==1 else (-.20,-.22,.54))
        if support_wrist is not None: dest_wrist = support_wrist.copy()
    elif kind=="partner":
        dest_elbow = V((sign*.235,1.09,.12))
        dest_wrist = V((-.018,1.245,.36) if sign==1 else (-.122,1.21,.235))
    elif kind=="coder_seated":
        dest_elbow, dest_wrist = V((sign*.22,.815,.23)), V((sign*.12,.825,.49))
    elif kind=="boss":
        dest_elbow = V((sign*.36,1.08,.12 if sign==1 else -.04))
        dest_wrist = V((sign*.30,1.04,.43 if sign==1 else .27))
    elif kind=="coder":
        dest_elbow, dest_wrist = V((sign*.245,1.04,.102)),V((sign*.196,1.03,.313))
    else:
        dest_elbow, dest_wrist = V((sign*.25,1.15,.12)),V((sign*.155,1.31,.30))
    transforms = {}
    def put(name, origin, dest, rot):
        transforms[name+"_"+side] = (origin,dest,rot)
    def limb(name, a,b, c,d):
        src, dst = b-a,d-c
        rotation = src.rotation_difference(dst).to_matrix()
        # Stretch along bone axis only; retain the anatomical cross section.
        u = src.normalized()
        stretch = Matrix.Identity(3) + Matrix([list(u*u[n]) for n in range(3)])*(dst.length/src.length-1)
        put(name,a,c,rotation@stretch)
    shoulder_dest = shoulder + seated_shift() if kind=="coder_seated" else shoulder
    limb("upperarm",shoulder,elbow,shoulder_dest,dest_elbow)
    limb("lowerarm",elbow,wrist,dest_elbow,dest_wrist)
    mid, index, pinky = [j(f"finger-{d}-1") for d in (3,2,5)]
    if kind in ("fps","partner"):
        direction = (-.26,.30,.92) if sign==1 else (.18,.36,.92)
        across = (.18,.24,-.95) if sign==1 else (0,.98,.10)
        # Lay the support palm below the prop, its fingers turning around the side.
        if sign==1: direction=(-.52,.20,.80); across=(.75,.0,.65)
    elif kind=="coder_seated":
        direction=(0,-.10,1); across=(1,0,0)
    else:
        direction=(-sign*.46,.07,.88) if kind=="coder" else (-sign*.61,-.20,.78)
        across=(sign*.85,.03,.45) if kind=="coder" else (-sign*.75,.02,-.59)
    hand = frame(direction,across) @ frame(mid-wrist,index-pinky).transposed()
    put("hand",wrist,dest_wrist,hand)
    signet = None
    for d, name in enumerate(DIGITS,1):
        chain=[j(f"finger-{d}-{k}") for k in range(1,5)]
        pos = dest_wrist+hand@(chain[0]-wrist)
        for k in range(3):
            rest = chain[k+1]-chain[k]
            if kind in ("fps","partner") and d>1:
                if sign==-1:
                    goals=[(.75,.02,.66),(.91,.05,-.30),(.22,.09,-.96)]
                    if d==2: goals=[(.32,.12,.94),(.40,.0,.91),(.60,-.16,.76)]
                else:
                    goals=[(-.85,.32,.42),(-.30,.90,.18),(.55,.77,-.10)]
                target=V(goals[k]).normalized()
            elif kind in ("fps","partner") and d==1:
                goals = [(-.10,.49,.83),(-.45,.65,.56),(-.70,.68,.20)] if sign==1 else [
                    (.38,.66,.62),(.18,.44,.89),(.08,.22,.97)]
                target=V(goals[k]).normalized()
            elif kind=="coder" and d>1:
                target=V((-sign*.84,.16+.12*k,.52-.17*k)).normalized()
            else:
                target=(hand@rest).normalized()
                if kind=="seer" and d>1: target=(target+V((0,-.20*k,0))).normalized()
            rot=(hand@rest).rotation_difference(target).to_matrix()@hand
            put(f"{name}_{k+1:02}",chain[k],pos,rot)
            if d==4 and k==0:
                signet=(pos+target*rest.length*.45,target)
            pos=pos+target*rest.length
    return transforms,signet


def seated_shift():
    return V((0,.615-JOINT["l-upper-leg"].y,0))


def lower_pose(kind):
    """Joint-space sitting / staggered combat stance, never squash the body."""
    transforms={}
    if kind=="coder_seated":
        for entries in WEIGHTS:
            for bone,_ in entries:
                transforms[bone]=(V(),seated_shift(),Matrix.Identity(3))
    for side,sign in (("l",1),("r",-1)):
        hip,knee,ankle=[JOINT[side+"-"+j] for j in ("upper-leg","knee","ankle")]
        if kind=="coder_seated":
            h=hip+seated_shift(); k=V((h.x,h.y,h.z+.40)); a=V((h.x,.105,k.z))
            assert abs((k-h).normalized().dot((a-k).normalized()))<1e-6
        else:
            h=hip; k=V((sign*.23,.49,.13 if sign==1 else -.10))
            a=V((sign*.30,ankle.y,.22 if sign==1 else -.16))
        for bone,start,end,dest,target in (("thigh",hip,knee,h,k),("calf",knee,ankle,k,a)):
            src=end-start; dst=target-dest; u=src.normalized()
            stretch=Matrix.Identity(3)+Matrix([list(u*u[n]) for n in range(3)])*(dst.length/src.length-1)
            transforms[bone+"_"+side]=(start,dest,src.rotation_difference(dst).to_matrix()@stretch)
        for bone in ("foot","ball"):
            transforms[bone+"_"+side]=(ankle,a,Matrix.Identity(3))
    return transforms


def human(kind):
    fps = kind.startswith("fps")
    transforms = lower_pose(kind) if kind in ("coder_seated","boss") else {}
    wardrobe = "coder" if kind=="coder_seated" else ("partner" if kind=="boss" else kind)
    rings = {}
    for side in ("l","r"):
        arm, rings[side] = pose(kind,side)
        transforms.update(arm)
    pieces = {}
    for side in (("l","r") if fps else ("body",)):
        faces = BODY
        if fps:
            sign = 1 if side=="l" else -1
            elbow, wrist = JOINT[side+"-elbow"], JOINT[side+"-hand"]
            axis = (wrist-elbow).normalized()
            faces = [f for f in BODY if all(sign*REST[i].x>.25 and REST[i].y>.90 and
                     (REST[i]-elbow).dot(axis)>.001 for i in f)]
        ids = sorted({i for face in faces for i in face})
        lookup = {old:i for i,old in enumerate(ids)}
        pts = []
        for i in ids:
            p = REST[i].copy()
            b = max(WEIGHTS[i],key=lambda a:a[1])[0] if WEIGHTS[i] else ""
            if b.startswith(("upperarm","lowerarm")):
                # Broad tailored sleeve, tapering sharply at the shirt cuff.
                s = b[-1]
                a,c = (JOINT[s+"-elbow"],JOINT[s+"-hand"]) if b.startswith("lower") else (JOINT[s+"-shoulder"],JOINT[s+"-elbow"])
                axis = (c-a).normalized()
                centre = a+axis*(p-a).dot(axis)
                p+=(p-centre)*(.25 if kind=="seer" else .48)
            if REST[i].y<.91 and abs(REST[i].x)<.27:
                p.x*=1.06
                p.z=(p.z-.03)*1.12+.03
            out = V()
            total = 0
            for bone, w in WEIGHTS[i]:
                if fps and bone.startswith(("upperarm","clavicle")):
                    bone="lowerarm_"+side
                if bone in transforms:
                    org,dest,rot = transforms[bone]
                    out+=(dest+rot@(p-org))*w
                else: out+=p*w
                total+=w
            pts.append(out/total if total else p)
        obj=mesh("MPFB "+side,pts,[tuple(lookup[i] for i in f) for f in faces],"skin")
        slots={"skin":0}
        for poly,face in zip(obj.data.polygons,faces):
            p=sum((REST[i] for i in face),V())/len(face)
            arm=abs(p.x)>.205 and p.y>.90
            sidekey="l" if p.x>0 else "r"
            wrist=JOINT[sidekey+"-hand"]
            fore=(wrist-JOINT[sidekey+"-elbow"]).normalized()
            along=(p-wrist).dot(fore)
            coat={"seer":"silk","partner":"crimson","coder":"blue","fps":"crimson"}["fps" if fps else wardrobe]
            if arm:
                key= "skin" if along>-.004 else ("ivory" if along>-.031 and (kind=="partner" or fps) else coat)
            elif p.y<.115: key="sole" if p.y<.033 else ("navy" if wardrobe=="coder" else "coal")
            elif p.y<.927: key="plum" if kind=="seer" else ("denim" if wardrobe=="coder" else "coal")
            elif p.y<1.388: key=coat
            elif p.y>1.574+.030*min(1,max(-1,(p.z-.06)/.09)): key="hair"
            else: key="skin"
            if kind=="boss": key="titanium" if key!="skin" else "jointmetal"
            if key not in slots:
                slots[key]=len(obj.data.materials)
                obj.data.materials.append(mat(key))
            poly.material_index=slots[key]
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active=obj
        dec=obj.modifiers.new("Anatomical planes","DECIMATE")
        dec.ratio=.65 if fps else .18
        bpy.ops.object.modifier_apply(modifier=dec.name)
        tri=obj.modifiers.new("Facets","TRIANGULATE")
        bpy.ops.object.modifier_apply(modifier=tri.name)
        for p in obj.data.polygons: p.use_smooth=False
        if fps:
            fore=transforms["lowerarm_"+side]
            start=fore[1]
            end=transforms["hand_"+side][1]
            aim=(end-start).normalized()
            bm=bmesh.new()
            bm.from_mesh(obj.data)
            normal=V(xyz(aim))
            for dist,trim in ((.042,True),((end-start).length-.034,False),((end-start).length-.006,False)):
                at=V(xyz(start+aim*dist))
                cut=bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
                    plane_co=at,plane_no=normal,clear_inner=trim,dist=.00001)
                if trim:
                    edges=[e for e in cut["geom_cut"] if isinstance(e,bmesh.types.BMEdge) and e.is_boundary]
                    if edges: bmesh.ops.holes_fill(bm,edges=edges)
            for face in bm.faces:
                # A curled finger can sit behind the wrist plane. It is still skin.
                if face.material_index==slots["skin"]: continue
                distance=(face.calc_center_median()-V(xyz(end))).dot(normal)
                key="skin" if distance>=-.006 else ("ivory" if distance>=-.034 else "crimson")
                face.material_index=slots[key]
            bmesh.ops.triangulate(bm,faces=list(bm.faces))
            bm.to_mesh(obj.data)
            bm.free()
        pieces[side]=obj
    if fps:
        # Anchor to the final proximal ring-finger transform, then fit the band
        # to the skinned surface (joint centres alone omit the skin envelope).
        origin,start,rotation=transforms["ring_01_r"]
        delta=rotation@(JOINT["r-finger-4-2"]-JOINT["r-finger-4-1"])
        pos,axis=start+delta*.5,delta.normalized()
        rings["r"]=(pos,axis)
        basis=frame(axis,(0,1,0))
        skin=pieces["r"]
        tree=BVHTree.FromPolygons([v.co for v in skin.data.vertices],
            [tuple(f.vertices) for f in skin.data.polygons])
        def surface(direction):
            hit,_,_,distance=tree.ray_cast(V(xyz(pos)),V(xyz(direction)),.025)
            assert hit is not None and distance<.018,(kind,"ring outside finger",distance)
            return pos+direction*(distance+.0003)
        wire("Gold ring band",[surface(basis.col[1]*math.cos(i*math.tau/32)+
             basis.col[2]*math.sin(i*math.tau/32)) for i in range(32)],.0012,"gold",True)
        outward=rotation@V((-1,0,0))
        outward=(outward-axis*outward.dot(axis)).normalized()
        top=surface(outward)
        oval("Signet setting",top,(.003,.004,.004),"gold")
        oval("Onyx stone",top+outward*.0015,(.002,.003,.003),"onyx")
    return pieces


def footwear(kind):
    for side in (-1,1):
        # Outward stance follows the MPFB ankle and toe axes.
        def shell(name, rings, key):
            pts=[]
            for h,rx,rz,cx,cz in rings:
                for j in range(20):
                    a=j*math.tau/20
                    x,z=rx*math.sin(a),rz*math.cos(a)
                    pts.append((side*(cx+x+.14*z),h,cz+z))
            faces=[(k*20+j,k*20+(j+1)%20,(k+1)*20+(j+1)%20,(k+1)*20+j)
                   for k in range(len(rings)-1) for j in range(20)]
            faces.extend([tuple(range(19,-1,-1)),tuple(range(len(pts)-20,len(pts)))])
            if side<0: faces=[tuple(reversed(f)) for f in faces]
            return mesh(name,pts,faces,key)
        base=(.238,.074)
        shell("Outsole",[(.0,.063,.146,*base),(.023,.066,.147,*base),
              (.032,.064,.145,*base)],"sole" if kind=="coder" else "coal")
        shell("Upper",[(.025,.065,.146,*base),(.052,.064,.140,*base),
              (.080,.055,.101,.232,.045),(.119,.047,.063,.225,.001)],
              "navy" if kind=="coder" else "onyx")
        if kind=="coder":
            for j in range(3):
                z=.067+j*.017
                wire("Lace",[(side*.207,.095-j*.006,z),(side*.256,.095-j*.006,z+.012)],.003,"sole")
        if kind!="seer":
            shell("Trouser leg",[(.107,.055,.060,.223,.002),(.132,.058,.063,.223,.006),
                  (.345,.065,.068,.197,.004),(.468,.068,.073,.167,.028),
                  (.57,.079,.090,.148,.036),(.733,.089,.103,.119,.025),
                  (.850,.097,.113,.096,.015)],"denim" if kind=="coder" else "coal")


def clothing(kind):
    coat = {"coder":"blue","partner":"crimson","seer":"silk"}[kind]
    widths = [( .875,.176,.137,.03),(.904,.178,.139,.03),(1.03,.16,.128,.04),
              (1.19,.185,.127,.051),(1.31,.207,.114,.025),(1.369,.188,.086,.018),
              (1.401,.075,.072,.042)]
    loft("Cut of jacket",widths,coat)
    if kind=="partner":
        mesh("Black shirt",[(-.072,1.393,.10),(.072,1.393,.10),(.062,1.327,.143),
              (0,1.095,.178),(-.062,1.327,.143)],[(0,1,2),(0,2,4),(2,3,4)],"coal")
        for s in (-1,1):
            pp=[(s*.065,1.395,.108),(s*.128,1.345,.112),(s*.097,1.316,.140),
                (s*.119,1.303,.138),(s*.008,1.093,.180)]
            mesh("Notched lapel",pp,[(0,1,2),(0,2,4),(2,3,4)],"lapel")
        for h in (.99,1.057):
            oval("Horn button",(.019,h,.175),(.007,.007,.005),"onyx")
    elif kind=="coder":
        oval("Folded hood",(0,1.332,-.070),(.156,.095,.081),"navy")
        loft("Ribbed hem",[(.877,.18,.14,.03),(.911,.18,.14,.03)],"navy")
        for s in (-1,1):
            wire("Drawstring",[(s*.056,1.38,.107),(s*.043,1.315,.145),(s*.048,1.247,.17)],.003,"ivory")
        mesh("Kangaroo pocket",[(-.112,.943,.149),(.112,.943,.149),(.080,1.032,.161),(-.08,1.032,.161)],[(0,1,2,3)],"navy")
    else:
        loft("Gored skirt",[(.11,.262,.21,.01),(.13,.261,.21,.01),
            (.56,.214,.177,.02),(.84,.182,.148,.03),(1.027,.153,.125,.041)],"plum")
        loft("Woven sash",[(.967,.166,.135,.04),(1.014,.164,.131,.041)],"gold")
    if kind!="coder":
        wire("Neck chain",[(-.065,1.405,.103),(-.048,1.365,.141),(0,1.329,.155),
             (.048,1.365,.141),(.065,1.405,.103)],.004,"gold")
    # Eyes fill the real eyelid openings. Hair cap on the skull, real ears left exposed.
    for side in ("l","r"):
        p=JOINT[side+"-eye"]
        oval("Eye",p+V((0,.001,.003)),(.012,.010,.014),"ivory")
        oval("Iris",p+V((0,.001,.015)),(.006,.006,.003),"iris")
    oval("Crown hair",(0,1.594,.054),(.080,.081,.104),"hair")
    if kind=="seer":
        oval("Chignon",(0,1.619,-.042),(.067,.065,.059),"hairlight")
        wire("Mystic circlet",[(.079*math.sin(t*.19635),1.582-.019*max(0,math.cos(t*.19635))**6,
             .058+.097*math.cos(t*.19635)) for t in range(32)],.004,"gold",True)
        oval("Forehead jewel",(0,1.56,.155),(.007,.012,.005),"teal")
        for side in (-1,1):
            wire("Eyebrow",[(side*.012,1.563,.149),(side*.033,1.568,.147),(side*.052,1.562,.137)],.0025,"hair")
            oval("Earring",(side*.080,1.508,.052),(.006,.011,.005),"gold")
        wire("Lip colour",[(-.019,1.478,.150),(-.007,1.479,.157),(0,1.478,.159),
            (.007,1.479,.157),(.019,1.478,.150)],.0022,"lip")
    else:
        for s in (-1,1):
            oval("Curved lens",(s*.034,1.549,.145),(.038,.023,.012),"teal" if kind=="coder" else "glass")
            wire("Temple",[(s*.065,1.552,.143),(s*.079,1.55,.117),(s*.081,1.535,.060)],.004,"coal")
        wire("Bridge",[(-.014,1.55,.151),(0,1.551,.158),(.014,1.55,.151)],.005,"coal")
        if kind=="coder":
            lens=[(-.079,1.563,.123),(-.044,1.572,.155),(0,1.571,.159),
                  (.044,1.572,.155),(.079,1.563,.123),(.069,1.542,.133),
                  (.035,1.533,.157),(0,1.541,.162),(-.035,1.533,.157),(-.069,1.542,.133)]
            visor=mesh("Wraparound AR visor",lens,[(0,9,8,1),(1,8,7,2),(2,7,6,3),(3,6,5,4)],"teal")
            for poly in visor.data.polygons:
                if poly.normal.y>0: poly.flip()
            wire("Visor rim",lens[:5],.003,"coal")
            for s in (-1,1):
                oval("Over-ear cup",(s*.086,1.539,.056),(.024,.040,.036),"coal")
                oval("Cup inset",(s*.104,1.539,.056),(.010,.029,.027),"navy")
            wire("Padded headphone band",[(.092*math.cos(i*math.pi/16),1.548+.132*math.sin(i*math.pi/16),.054)
                 for i in range(17)],.011,"coal")


def laptop():
    box("Laptop deck",(0,1.047,.363),(.355,.016,.205),"laptop",.007)
    box("Keyboard",(0,1.057,.345),(.288,.003,.080),"coal",.002)
    for row in range(4):
        for col in range(12):
            box("Key",((col-5.5)*.022,1.059,.315+row*.018),(.018,.002,.012),"glass")
    lid=box("Open lid",(0,1.151,.48),(.355,.211,.012),"laptop",.006)
    lid.rotation_euler.x=math.radians(11)
    screen=box("Terminal",(0,1.151,.472),(.331,.185,.005),"display")
    screen.rotation_euler.x=lid.rotation_euler.x
    for i in range(5):
        z=1.11+i*.018
        box("Terminal line",(-.07,z,.466+(z-1.15)*.194),(.08+.014*(i%3),.003,.001),"cyan")
    oval("Lid emblem",(0,1.155,.489),(.020,.017,.003),"ivory")


def tarot():
    # Draped scallops and a narrow sewn border on a small reading table.
    rings=[(.18,.84,.37,.40),(.78,.82,.35,.40),(.936,.79,.335,.40)]
    loft("Velvet drape",rings,"velvet",32)
    loft("Gold piping",[(.918,.796,.34,.40),(.935,.797,.34,.40)],"gold",32)
    loft("Cloth top",[(.934,.803,.343,.40),(.953,.801,.343,.40)],"plum",32)
    oval("Brass plinth",(0,.983,.357),(.090,.032,.090),"gold")
    oval("Faceted crystal",(0,1.092,.357),(.110,.112,.110),"orb")
    for i in range(4):
        x=(i-1.5)*.121
        o=box("Tarot card",(x,.956,.60-.04*abs(i-1.5)),(.088,.004,.134),"card",.002)
        o.rotation_euler.z=(i-1.5)*.10
        oval("Card seal",(x,.960,.60-.04*abs(i-1.5)),(.021,.002,.031),"ink")
    for x,h,z in [(-.55,.17,.39),(-.64,.12,.46),(.56,.20,.40),(.67,.11,.47)]:
        oval("Candle holder",(x,.964,z),(.041,.013,.041),"gold")
        bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=.023,depth=h,location=xyz((x,.971+h/2,z)))
        finish(bpy.context.object,"Beeswax","wax")
        oval("Flame",(x,.981+h,z),(.010,.023,.010),"flame")


def join(objects, name):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join()
    obj=bpy.context.object
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    obj.name=obj.data.name=name
    obj.select_set(False)
    return obj


def solder_spool(pieces):
    before=set(bpy.context.scene.objects)
    PALETTE.update(solder="bbc4cc",spool="385977",label="eee7ce")
    c=V((.16,-.15,.70))
    for z in (-.052,.052):
        wire("Spool flange",[c+V((.037*math.cos(i*math.tau/40),.037*math.sin(i*math.tau/40),z)) for i in range(40)],.004,"spool",True)
    for z in (-.052,.052):
        for angle in range(6):
            a=angle*math.tau/6
            wire("Spool spoke",[c+V((0,0,z)),c+V((.034*math.cos(a),.034*math.sin(a),z))],.002,"spool")
    for z in range(42):
        wire("POS61 tin winding",[c+V((.025*math.cos(i*math.tau/32),.025*math.sin(i*math.tau/32),-.047+z*.0023)) for i in range(32)],.0013,"solder",True)
    wire("Silver solder tail",[c+V((-.025,0,0)),c+V((-.060,.012,.01)),c+V((-.10,.04,.04)),c+V((-.15,.035,.10))],.0013,"solder")
    box("POS61 label",c+V((0,0,-.055)),(.045,.027,.002),"label",.002)
    bpy.ops.object.text_add(location=xyz(c+V((.020,-.005,-.057))))
    label=bpy.context.object; label.data.body="ПОС-61"; label.data.size=.011
    label.rotation_euler=(math.pi/2,0,0); label.scale.x=-1; label.data.extrude=.0001
    label.data.materials.append(mat("ink")); bpy.ops.object.convert(target="MESH")
    bpy.context.view_layer.update()
    pieces["l"]=join([pieces["l"]]+list(set(bpy.context.scene.objects)-before),"Left")


def boss_armor():
    """Fitted curved exoskeleton over the full deformed MPFB human surface."""
    loft("Thoracic titanium cuirass",[(.94,.18,.15,.03),(1.07,.20,.165,.04),
        (1.24,.265,.18,.03),(1.36,.24,.14,.025),(1.40,.10,.09,.025)],"titanium")
    for side,sign in (("l",1),("r",-1)):
        transforms,_=pose("boss",side)
        legs=lower_pose("boss")
        for bone,joint,end,radius in (("upperarm","shoulder","elbow",.105),
                ("lowerarm","elbow","hand",.115),("thigh","upper-leg","knee",.115),
                ("calf","knee","ankle",.09)):
            org,dest,rot=(transforms if bone in ("upperarm","lowerarm") else legs)[bone+"_"+side]
            target=dest+rot@(JOINT[side+"-"+end]-org)
            obj=oval("Articulated "+bone+" plate",(dest+target)/2,(radius,(target-dest).length*.43,radius),"titanium")
            obj.rotation_mode="QUATERNION"
            obj.rotation_quaternion=V((0,0,1)).rotation_difference(V(xyz(target-dest)))
            oval("Exoskeleton joint",dest,(radius*.87,)*3,"jointmetal")
        wrist=transforms["hand_"+side][1]
        oval("Manipulator gauntlet",wrist+V((0,0,.04)),(.095,.074,.12),"jointmetal")
        for n in range(4):
            wire("Manipulator finger",[wrist+V((sign*(n-1.5)*.034,0,.10)),
                wrist+V((sign*(n-1.5)*.034,-.025,.18)),
                wrist+V((sign*(n-1.5)*.034,-.075,.20))],.015,"titanium")
        # Turbine axis faces forward. Concentric rings and individual fan blades.
        c=V((sign*.30,1.40,-.015))
        oval("Shoulder turbine housing",c,(.16,.15,.19),"titanium")
        for z,r in ((.16,.12),(.18,.097)):
            wire("Turbine intake rim",[c+V((r*math.cos(i*math.tau/32),r*math.sin(i*math.tau/32),z)) for i in range(32)],.012,"jointmetal",True)
        for i in range(8):
            angle=i*math.tau/8
            wire("Server cooling fin",[c+V((.04*math.cos(angle),.04*math.sin(angle),.18)),
                c+V((.10*math.cos(angle+.3),.10*math.sin(angle+.3),.18))],.007,"neon")
        for h in (1.06,1.12,1.18):
            wire("Rib cooling channel",[(sign*.08,h,.209),(sign*.18,h+.025,.18)],.009,"neon")
        a=legs["foot_"+side][1]
        oval("Armored boot",a+V((0,-.025,.07)),(.085,.075,.17),"titanium")
    oval("Cranial armor",(0,1.565,.047),(.104,.126,.12),"titanium")
    wire("Crimson cyber visor",[(-.092,1.56,.13),(-.055,1.555,.164),
        (0,1.549,.177),(.055,1.555,.164),(.092,1.56,.13)],.020,"neon")
    oval("Reactor armored socket",(0,1.24,.202),(.092,.105,.035),"jointmetal")
    wire("Reactor luminous ring",[(.065*math.cos(i*math.tau/32),1.24+.065*math.sin(i*math.tau/32),.239)
        for i in range(32)],.009,"neon",True)
    oval("Reactor core",(0,1.24,.24),(.035,.047,.012),"neon")


def export(kind, filename):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    pieces=human(kind)
    if kind.startswith("fps"):
        if kind=="fps_solder":
            solder_spool(pieces)
        join([o for o in bpy.context.scene.objects if o!=pieces["l"]],"Right")
        pieces["l"].name=pieces["l"].data.name="Left"
    elif kind=="boss":
        boss_armor()
        body=join(list(bpy.context.scene.objects),filename)
        body.scale=(1.35,)*3
        bpy.context.view_layer.objects.active=body; body.select_set(True)
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        body["source"]="MPFB 3dobjs/base.obj"
        body["anatomical_scale"]=1.35
    elif kind=="coder_seated":
        before=set(bpy.context.scene.objects)
        clothing("coder")
        for obj in set(bpy.context.scene.objects)-before:
            obj.location+=V(xyz(seated_shift()))
        before=set(bpy.context.scene.objects)
        footwear("coder")
        legs=lower_pose(kind)
        for obj in set(bpy.context.scene.objects)-before:
            if obj.name.startswith("Trouser leg"):
                bpy.data.objects.remove(obj,do_unlink=True)
                continue
            # Shoe meshes have world-space vertices; select the matching ankle.
            centre=sum((obj.matrix_world@v.co for v in obj.data.vertices),V())/len(obj.data.vertices)
            side="l" if centre.x>0 else "r"
            org,dest,_=legs["foot_"+side]
            obj.location+=V(xyz(dest-org))
        body=join(list(bpy.context.scene.objects),filename)
        body["pose"]="Hip and knee 90 degrees; hands on workstation keyboard; no laptop"
    else:
        clothing(kind)
        footwear(kind)
        if kind=="coder": laptop()
        if kind=="seer": tarot()
        if kind=="partner":
            # The existing game prop points along -Z in glTF.
            bpy.ops.import_scene.gltf(filepath=str(OUT/"weapon_uzi_90s.glb"))
            props=list(bpy.context.selected_objects)
            gun=join([o for o in props if o.type=="MESH"],"Micro SMG")
            # Runtime hideProp identifies the three weapon material groups by name.
            for material in gun.data.materials:
                if material.name=="MachinedSteel": material.name="Gold90s"
            gun.location=xyz((-.095,1.308,.325))
            gun.rotation_euler.z=math.pi
            gun.scale=(.57,)*3
        join(list(bpy.context.scene.objects),filename)
    if kind.startswith("fps_"):
        for obj in bpy.context.scene.objects:
            side="l" if obj.name=="Left" else "r"
            obj.data["wrist"]=list(contact_pose(kind,side)[0]["hand_"+side][1])
    for obj in bpy.context.scene.objects:
        assert obj.type=="MESH" and len(obj.data.polygons)>0
        bm=bmesh.new();bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0000001)
        bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.0000001)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bm.to_mesh(obj.data);bm.free();obj.data.update()
        assert all(math.isfinite(c) for v in obj.data.vertices for c in v.co)
    bpy.ops.export_scene.gltf(filepath=str(OUT/(filename+".glb")),export_format="GLB",
        export_texcoords=False,export_animations=False,export_cameras=False,export_lights=False,export_extras=True)
    print("STAGE4",filename,len(BODY),"source body faces")


def game_prop():
    # Shared silhouette for the first-person and partner models. Game units;
    # the existing viewmodel expects the front of this prop along glTF -Z.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    metal, trim, polymer = "GunMetal", "MachinedSteel", "PolymerGrip"
    PALETTE.update(GunMetal="414c58", MachinedSteel="78838c", PolymerGrip="252b32")
    def detail(label, p, size, key, bevel=.003):
        obj = box(label, p, size, key)
        bpy.context.view_layer.objects.active = obj
        mod = obj.modifiers.new("Stamped rounded edges", "BEVEL")
        mod.width, mod.segments = bevel, 5
        bpy.ops.object.modifier_apply(modifier=mod.name)
        return obj
    def cut(obj, p, size):
        cutter = box("Vent cutter", p, size, metal, .003)
        bpy.context.view_layer.objects.active = obj
        mod = obj.modifiers.new("Through ventilation window", "BOOLEAN")
        mod.operation, mod.object = 'DIFFERENCE', cutter
        bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.data.objects.remove(cutter, do_unlink=True)
    def tube(label, z, length, radius, key):
        bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=radius, depth=length,
            location=xyz((0,.018,z)), rotation=(math.pi/2,0,0))
        obj = finish(bpy.context.object,label,key)
        for face in obj.data.polygons: face.use_smooth = len(face.vertices)==4
        return obj
    receiver=detail("Slim stamped receiver",(0,.007,.014),(.038,.046,.296),metal,.006)
    # Sloping rear face and a true semicircular cover running along the barrel.
    for v in receiver.data.vertices:
        if v.co.y < -.10: v.co.y += max(0,v.co.z)*.45
    arc=[(.019*math.cos(i*math.pi/24),.029+.019*math.sin(i*math.pi/24)) for i in range(25)]
    pts=[(x,y,z) for z in (-.128,.143) for x,y in arc]
    faces=[(i,i+1,26+i,25+i) for i in range(24)]
    faces += [tuple(reversed(range(25))),tuple(range(25,50)),(0,25,49,24)]
    cover=mesh("Half round bolt cover",pts,faces,metal)
    for face in cover.data.polygons[:24]: face.use_smooth=True
    button=tube("Round recoil spring button",.159,.007,.007,trim)
    detail("Lower receiver",(0,-.025,.010),(.035,.025,.190),metal,.007)
    # Keep the original grip volume and origin used by fps_hands_uzi.
    detail("Rounded grip",(0,-.124,.018),(.057,.217,.070),polymer,.016)
    detail("Magazine floor plate",(0,-.232,.019),(.060,.014,.072),metal,.005)
    for side in (-1,1):
        for i in range(9):
            detail("Grip checkering",(side*.028,-.065-i*.016,.020),(.002,.003,.043),trim,.001)
    guard=[(0,-.040,-.035),(0,-.046,-.088),(0,-.057,-.102),
           (0,-.084,-.102),(0,-.097,-.088),(0,-.097,-.025)]
    wire("Rounded trigger guard",guard,.004,metal)
    wire("Curved trigger",[(0,-.046,-.048),(0,-.064,-.065),(0,-.078,-.060)],.003,trim)
    # Hollow rounded sheet-metal shroud with real side and top ventilation holes.
    shroud=detail("Perforated barrel shroud",(0,.018,-.173),(.038,.050,.115),metal,.010)
    cut(shroud,(0,.018,-.173),(.028,.036,.140))
    for z in (-.140,-.169,-.198):
        cut(shroud,(0,.018,z),(.100,.023,.015))
        cut(shroud,(0,.043,z),(.019,.022,.015))
    detail("Ribbed forearm",(0,-.014,-.164),(.036,.018,.100),polymer,.007)
    for z in (-.125,-.139,-.153,-.167,-.181,-.195):
        detail("Forearm raised rib",(0,-.021,z),(.039,.006,.004),metal,.002)
    tube("Inner barrel",-.200,.140,.012,polymer)
    comp=tube("Cylindrical muzzle compensator",-.280,.060,.018,trim)
    # Open bore, plus transverse compensator ports.
    bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=.009,depth=.080,
        location=xyz((0,.018,-.280)),rotation=(math.pi/2,0,0))
    bore=bpy.context.object
    bpy.context.view_layer.objects.active=comp
    mod=comp.modifiers.new("Open muzzle bore","BOOLEAN"); mod.object=bore; mod.operation='DIFFERENCE'
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(bore,do_unlink=True)
    for z in (-.268,-.291): cut(comp,(0,.031,z),(.060,.012,.006))
    # Folding steel stock hugs the right side instead of protruding into the hands.
    for y in (.036,-.014):
        wire("Folded stock rail",[(.024,y,.146),(.033,y,.112),(.033,y,-.115)],.0035,metal)
    wire("Stock shoulder loop",[(.033,.036,-.115),(.037,.030,-.143),
        (.037,-.010,-.143),(.033,-.014,-.115)],.004,polymer)
    detail("Stock hinge",(.024,.010,.135),(.010,.038,.025),trim,.005)
    for z in (-.180,.120):
        wire("Protected sight",[(-.019,.024,z),(-.019,.046,z),
            (0,.052,z),(.019,.046,z),(.019,.024,z)],.0035,metal)
    detail("Front sight post",(0,.038,-.180),(.004,.018,.004),trim,.001)
    detail("Side charging stem",(-.025,.030,-.030),(.020,.007,.009),trim,.003)
    detail("Side charging handle",(-.035,.030,-.030),(.010,.013,.021),polymer,.005)
    cut(receiver,(.019,.015,.015),(.008,.019,.060))
    detail("Dark ejection cavity",(.014,.015,.015),(.002,.018,.059),polymer,.002)
    detail("Ejection bolt edge",(.016,.015,.040),(.002,.014,.006),trim,.001)
    gun=join(list(bpy.context.scene.objects),"SMG")
    bm=bmesh.new();bm.from_mesh(gun.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0000001)
    bmesh.ops.triangulate(bm,faces=list(bm.faces))
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.0000001)
    bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.calc_area()<1e-12],context='FACES')
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(gun.data);bm.free();gun.data.update()
    bpy.ops.export_scene.gltf(filepath=str(OUT/"weapon_uzi_90s.glb"),export_format="GLB",
        export_texcoords=False,export_animations=False)


if "--hands-only" not in __import__("sys").argv:
    game_prop()
for kind, filename in [("boss","escaped_ai_boss"),("coder_seated","char_vibe_coder_seated"),
                       ("coder","char_vibe_coder"),("partner","char_partner_90s"),
                       ("seer","char_hr_veronika"),("fps","fps_hands_cs2"),
                       ("fps_belt","fps_hands_belt"),("fps_solder","fps_hands_solder"),
                       ("fps_uzi","fps_hands_uzi"),("fps_shotgun","fps_hands_shotgun")]:
    if "--hands-only" in __import__("sys").argv:
        if kind.startswith("fps_"): export(kind,filename)
    elif "--workshop6" in __import__("sys").argv:
        if kind == "partner": export(kind,filename)
    elif "--workshop5" in __import__("sys").argv:
        if kind in ("partner","fps_uzi","fps_shotgun"): export(kind,filename)
    elif "--uzi-only" not in __import__("sys").argv:
        export(kind,filename)

