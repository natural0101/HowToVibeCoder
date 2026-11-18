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
from mathutils import Matrix, Vector

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


def mat(key):
    if key not in MATS:
        rgb = bytes.fromhex(PALETTE[key])
        m = bpy.data.materials.new(key)
        m.diffuse_color = tuple(((x/255+.055)/1.055)**2.4 for x in rgb) + (1,)
        m.use_nodes = True
        shader = m.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = m.diffuse_color
        shader.inputs["Roughness"].default_value = .34 if key in ("gold","glass","orb","onyx") else .81
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


def pose(kind, side):
    sign = 1 if side=="l" else -1
    j = lambda key: JOINT[side+"-"+key]
    shoulder, elbow, wrist = [j(k) for k in ("shoulder","elbow","hand")]
    if kind=="fps":
        dest_elbow = V((.28,-.40,.26) if sign==1 else (-.38,-.40,.22))
        dest_wrist = V((-.085,-.185,.68) if sign==1 else (-.20,-.22,.54))
    elif kind=="partner":
        dest_elbow = V((sign*.235,1.09,.12))
        dest_wrist = V((-.018,1.245,.36) if sign==1 else (-.122,1.21,.235))
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
    limb("upperarm",shoulder,elbow,shoulder,dest_elbow)
    limb("lowerarm",elbow,wrist,dest_elbow,dest_wrist)
    mid, index, pinky = [j(f"finger-{d}-1") for d in (3,2,5)]
    if kind in ("fps","partner"):
        direction = (-.26,.30,.92) if sign==1 else (.18,.36,.92)
        across = (.18,.24,-.95) if sign==1 else (0,.98,.10)
        # Lay the support palm below the prop, its fingers turning around the side.
        if sign==1: direction=(-.52,.20,.80); across=(.75,.0,.65)
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


def human(kind):
    fps = kind=="fps"
    transforms = {}
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
            coat={"seer":"silk","partner":"crimson","coder":"blue","fps":"crimson"}[kind]
            if arm:
                key= "skin" if along>-.004 else ("ivory" if along>-.031 and kind in ("partner","fps") else coat)
            elif p.y<.115: key="sole" if p.y<.033 else ("navy" if kind=="coder" else "coal")
            elif p.y<.927: key="plum" if kind=="seer" else ("denim" if kind=="coder" else "coal")
            elif p.y<1.388: key=coat
            elif p.y>1.574+.030*min(1,max(-1,(p.z-.06)/.09)): key="hair"
            else: key="skin"
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
                distance=(face.calc_center_median()-V(xyz(end))).dot(normal)
                key="skin" if distance>=-.006 else ("ivory" if distance>=-.034 else "crimson")
                face.material_index=slots[key]
            bmesh.ops.triangulate(bm,faces=list(bm.faces))
            bm.to_mesh(obj.data)
            bm.free()
        pieces[side]=obj
    if fps:
        pos,axis=rings["r"]
        basis=frame(axis,(0,1,0))
        radial=basis.col[1]
        wire("Onyx band",[pos+basis.col[1]*math.cos(i*math.tau/16)*.012+
             basis.col[2]*math.sin(i*math.tau/16)*.012 for i in range(16)],.0033,"gold",True)
        oval("Signet setting",pos-radial*.002+V((-.009,.002,0)),(.006,.009,.011),"gold")
        oval("Onyx stone",pos+V((-.013,.003,0)),(.003,.006,.008),"onyx")
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


def export(kind, filename):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    pieces=human(kind)
    if kind=="fps":
        join([o for o in bpy.context.scene.objects if o!=pieces["l"]],"Right")
        pieces["l"].name=pieces["l"].data.name="Left"
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
            gun.location=xyz((-.095,1.308,.325))
            gun.rotation_euler.z=math.pi
            gun.scale=(.57,)*3
        join(list(bpy.context.scene.objects),filename)
    for obj in bpy.context.scene.objects:
        assert obj.type=="MESH" and len(obj.data.polygons)>0
        assert all(math.isfinite(c) for v in obj.data.vertices for c in v.co)
    bpy.ops.export_scene.gltf(filepath=str(OUT/(filename+".glb")),export_format="GLB",
        export_texcoords=False,export_animations=False,export_cameras=False,export_lights=False)
    print("STAGE4",filename,len(BODY),"source body faces")


def game_prop():
    # Shared silhouette for the first-person and partner models. Game units;
    # the existing viewmodel expects the front of this prop along glTF -Z.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    metal, trim, polymer = "GunMetal","Gold90s","PolymerGrip"
    PALETTE.update(GunMetal="58616b",Gold90s="aa8652",PolymerGrip="39434d")
    def detail(label,p,size,key,bevel=.003):
        return box(label,p,size,key,bevel)
    detail("Upper housing",(0,.017,.014),(.084,.082,.306),metal,.010)
    detail("Lower casing",(0,-.035,-.030),(.076,.037,.184),polymer,.007)
    detail("Breech cover",(0,.063,.008),(.066,.013,.23),polymer)
    detail("Rear cap",(0,.014,.163),(.078,.077,.013),polymer)
    detail("Grip",(0,-.124,.018),(.057,.217,.070),polymer,.009)
    detail("Heel",(0,-.232,.019),(.061,.017,.073),metal)
    for i in range(4):
        detail("Grip rib",(0,-.081-i*.032,.018),(.059,.007,.073),metal,.002)
    detail("Guard",(0,-.091,-.055),(.012,.013,.087),metal)
    detail("Guard nose",(0,-.068,-.099),(.012,.049,.013),metal)
    detail("Fore-end",(0,-.020,-.141),(.085,.075,.151),polymer,.014)
    for i in range(5):
        detail("Cast rib",(0,-.020,-.096-i*.025),(.091,.080,.009),metal,.003)
    bpy.ops.mesh.primitive_cylinder_add(vertices=16,radius=.019,depth=.109,
        location=xyz((0,.018,-.254)),rotation=(math.pi/2,0,0))
    finish(bpy.context.object,"Muzzle",metal)
    detail("Front hood",(0,.066,-.177),(.052,.016,.023),polymer)
    for x in (-.023,.023):
        detail("Sight wing",(x,.082,-.177),(.009,.035,.026),metal)
        detail("Rear notch",(x,.081,.127),(.011,.041,.032),metal)
    for x in (-.043,.043):
        detail("Inset port",(x,.017,.013),(.004,.031,.086),polymer,.001)
        detail("Port trim",(x,.030,.01),(.005,.004,.078),trim,.001)
    join(list(bpy.context.scene.objects),"SMG")
    bpy.ops.export_scene.gltf(filepath=str(OUT/"weapon_uzi_90s.glb"),export_format="GLB",
        export_texcoords=False,export_animations=False)


game_prop()
for kind, filename in [("coder","char_vibe_coder"),("partner","char_partner_90s"),
                       ("seer","char_hr_veronika"),("fps","fps_hands_cs2")]:
    export(kind,filename)

