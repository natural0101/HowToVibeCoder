using System.Text.Json;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal enum Finish { Paint, Slate, Armor, Rack, Screen, Fabric, Leather, Lamp, Contact, Tarot, Lift, Plaster, Facade, Sky, Glass, Exit, Billboard, Imported, Carpet, CharacterShadow, Fluorescent, Anatomy, BlackMarble, Bronze, RosinSmoke }

internal static unsafe partial class VisualEngine3D
{
    private static Shader shader;
    private static Model box, bevel, plane, tube, sphere;
    private static readonly Dictionary<string, Model> imports = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ((int Start, int Count) Left, (int Start, int Count) Right)> armGroups = new();
    private static readonly Dictionary<(string Asset, bool Right), Vector3> armWrists = new();
    private static readonly Dictionary<(string Asset, bool Right), Vector3> sleeveEnds = new();
    private static int sleeveAxisLoc,sleevePullLoc,wristLoc,handSocketLoc,gripLoc;
    private static readonly HashSet<int> partnerPropMeshes = new();
    private static int batchLoc, clockLoc, beltTensionLoc;
    private static readonly Texture2D[] maps = new Texture2D[Enum.GetValues<Finish>().Length];
    private static readonly Dictionary<(string Asset,int Mesh),int> handSurfaces=new();
    private static int handSurfaceLoc, weaponFinishLoc;
    private static int firstPersonLoc, muzzleLoc, flashLoc;
    private static int eyeLoc, finishLoc, taperLoc, laptopLoc, tarotLoc, northLoc, southLoc, powerLoc;
    private static Finish? activeFinish;
    private static float activeTaper;

    private const string Vertex = """
        #version 330 core
        in vec3 vertexPosition;
        in vec3 vertexNormal;
        in vec4 vertexColor;
        uniform int batch;
        out vec4 tint;
        in vec2 vertexTexCoord;
        uniform mat4 mvp, matModel;
        uniform float taper;
        uniform float beltTension;
        uniform vec3 sleeveAxis,sleevePull,wrist,handSocket;
        uniform float gripAmount;
        uniform int firstPerson;
        out vec3 pos, norm;
        out vec2 localXZ;
        out vec2 uv;
        void main() {
            vec3 p = vertexPosition;
            vec3 n = vertexNormal;
            // Only jacket rings carry this UV channel. Rotate each complete section
            // toward its shoulder, preserving its radius and the rigid wrist opening.
            if (vertexTexCoord.x>1.99 && dot(sleevePull,sleevePull)>.0001) {
                float t=clamp(vertexTexCoord.y,0.,1.);
                // Hermite centreline between cuff and elbow. Use its tangent to
                // turn the complete cloth ring and stitching without flattening it.
                vec3 a=normalize(sleeveAxis);
                vec3 tangent0=a*min(length(sleeveAxis),length(sleevePull)*.54);
                vec3 tangent1=sleevePull*1.34;
                float t2=t*t, t3=t2*t;
                vec3 center=(t3-2.*t2+t)*tangent0+(3.*t2-2.*t3)*sleevePull+(t3-t2)*tangent1;
                vec3 b=normalize((3.*t2-4.*t+1.)*tangent0+6.*t*(1.-t)*sleevePull+(3.*t2-2.*t)*tangent1);
                float blend=smoothstep(.055,.22,t);
                vec4 q=normalize(mix(vec4(0.,0.,0.,1.),vec4(cross(a,b),1.+dot(a,b)),blend));
                vec3 offset=p-wrist-sleeveAxis*t;
                p+=2.*cross(q.xyz,cross(q.xyz,offset)+q.w*offset);
                p+=(center-sleeveAxis*t)*blend;
                n+=2.*cross(q.xyz,cross(q.xyz,n)+q.w*n);
            }
            if(gripAmount!=0.) {
                float flex=smoothstep(.040,.092,length(p-wrist))*(1.-smoothstep(.052,.091,length(p-handSocket)));
                p.xy-=gripAmount*flex*(p.xy-handSocket.xy);
            }
            if(beltTension>0.) {
                float span=smoothstep(-.36,-.04,p.x);
                p.x+=.014*beltTension*(2.*span-1.);
                p.y+=.024*beltTension*4.*span*(1.-span);
            }
            p.xz *= 1.0 + p.y*taper;
            n.y -= taper * dot(vertexPosition.xz,n.xz);
            pos = (matModel * vec4(p,1)).xyz;
            norm = normalize(transpose(inverse(mat3(matModel))) * n);
            uv = vertexTexCoord; localXZ = vertexPosition.xz;
            tint = batch == 1 || firstPerson==1 ? vertexColor : vec4(1.);
            gl_Position = mvp * vec4(p,1);
            // Reserve the nearest tenth for the viewmodel, retaining depth
            // precision between the closely spaced fingers and grip surfaces.
            if (firstPerson==1) gl_Position.z = gl_Position.z*.1 + gl_Position.w*(-.9);
        }
        """;

    private const string Fragment = """
        #version 330 core
        in vec3 pos, norm;
        in vec2 localXZ;
        in vec2 uv;
        in vec4 tint;
        uniform sampler2D texture0;
        uniform vec4 colDiffuse;
        uniform vec3 eye, laptop, tarot, northBeacon, southBeacon;
        uniform float power, clock, flashStrength;
        uniform vec3 muzzle;
        uniform int firstPerson;
        uniform int finish,handSurface;
        uniform vec2 weaponFinish;
        out vec4 color;
        vec3 point(vec3 at, vec3 tint, vec3 n, vec3 v, float gloss) {
            vec3 d=at-pos;
            float r=dot(d,d);
            vec3 l=d*inversesqrt(max(.001,r));
            float spec=pow(max(0.,dot(n,normalize(v+l))),gloss);
            return tint*(.12+max(0.,dot(n,l))+spec*1.4)/(1.+r*.17);
        }
        vec3 muzzleLight(vec3 n,vec3 v) {
            vec3 d=muzzle-pos;
            vec3 l=d*inversesqrt(max(.0001,dot(d,d)));
            float facing=max(0.,dot(n,l));
            float glint=pow(max(0.,dot(n,normalize(l+v))),18.);
            return vec3(1.,.51,.19)*flashStrength*(.12+.45*facing+.58*glint)/(1.+18.*dot(d,d));
        }
        float limb(vec2 p,vec2 a,vec2 b,float r) {
            vec2 v=b-a;
            return length(p-a-v*clamp(dot(p-a,v)/dot(v,v),0.,1.))-r;
        }
        void main() {
            if(finish==19) {
                vec2 p=vec2(localXZ.x,localXZ.y+.5);
                float d=limb(p,vec2(-.13,.04),vec2(-.06,.47),.057);
                d=min(d,limb(p,vec2(.13,.07),vec2(.06,.47),.06));
                d=min(d,limb(p,vec2(0,.44),vec2(0,.68),.145));
                d=min(d,limb(p,vec2(-.12,.69),vec2(-.27,.44),.048));
                d=min(d,limb(p,vec2(.12,.68),vec2(.30,.55),.053));
                d=min(d,length((p-vec2(.015,.875))*vec2(.88,1.17))-.105);
                color=vec4(.001,.003,.012,(1.-smoothstep(-.007,.034,d))*.82); return;
            }
            if(finish==24) {
                float h=clamp(.5-localXZ.y,0.,1.);
                float center=h*(.17*sin(h*8.-clock*2.)+.12*sin(h*14.-clock*1.3));
                float d=(localXZ.x-center)/(.033+h*.15);
                float opacity=exp(-d*d)*smoothstep(0.,.07,h)*(1.-smoothstep(.18,1.,h));
                color=vec4(.70,.77,.85,.48*opacity); return;
            }
            vec3 v=normalize(eye-pos), flatN=normalize(cross(dFdx(pos),dFdy(pos)));
            if(dot(flatN,v)<0.) flatN=-flatN;
            vec3 n=flatN;
            if((finish==21 || finish==17) && dot(norm,norm)>.01) {
                n=normalize(norm);
                if(dot(n,flatN)<0.) n=-n;
            } else if(dot(n,norm)<0.) n=-n;
            vec2 st=uv;
            if(finish==1) st=pos.xz*.36;
            if(finish==11) st=(abs(n.y)>.6 ? pos.xz : (abs(n.x)>.6 ? pos.zy : pos.xy))*.5;
            vec4 tex=texture(texture0,st);
            if(finish==8) {
                float halo=1.-smoothstep(.35,1.,length(uv*2.-1.));
                color=vec4(.008,.010,.019,halo*.55); return;
            }
            vec4 ink=colDiffuse*tint;
            vec3 base=tex.rgb*ink.rgb;
            float dist=length(eye-pos);
            if(finish==13) {
                vec3 ray=normalize(pos-eye);
                float altitude=smoothstep(-16.,84.,pos.y);
                vec3 sky=mix(vec3(.78,.89,.99),vec3(.26,.58,.95),altitude);
                vec3 sunDir=normalize(vec3(-.55,.36,-.42));
                float sunAmt=max(0.,dot(ray,sunDir));
                sky+=vec3(1.,.92,.68)*pow(sunAmt,18.)*.55 + vec3(1.,.98,.88)*smoothstep(.9965,.9992,sunAmt)*1.6;
                color=vec4(sky,1.); return;
            }
            if(finish==12) {
                vec2 wall=vec2(abs(n.x)>.6 ? pos.z : pos.x,pos.y);
                vec2 grid=wall*vec2(1.75,1.28);
                vec2 id=floor(grid), a=abs(fract(grid)-.5);
                vec2 aa=clamp(fwidth(grid),.008,.24);
                vec2 pane=1.-smoothstep(vec2(.32,.24)-aa,vec2(.32,.24)+aa,a);
                float seed=dot(floor(pos.xz*.02),vec2(37.,71.));
                float occupancy=fract(sin(dot(id,vec2(12.9898,78.233))+seed)*4375.5453);
                float sunHit=max(.25,dot(n,normalize(vec3(-.55,.52,-.42))));
                vec3 concrete=mix(vec3(.62,.68,.76),vec3(.78,.82,.88),sunHit);
                vec3 glassPane=mix(vec3(.32,.56,.82),vec3(.88,.95,1.),occupancy*.55 + sunHit*.35);
                vec3 facade=mix(concrete,glassPane,pane.x*pane.y*step(abs(n.y),.5));
                color=vec4(mix(facade,vec3(.74,.86,.98),smoothstep(55.,165.,dist)*.58),1.); return;
            }
            if(finish==14) {
                float edge=pow(1.-abs(dot(n,v)),3.);
                float glint=pow(max(0.,dot(reflect(-v,n),normalize(vec3(-.55,.72,-.42)))),16.);
                color=vec4(mix(ink.rgb,vec3(.92,.97,1.),glint),ink.a+edge*.09+glint*.18); return;
            }
            if(finish==21) {
                // Imported MPFB swatches are linear glTF factors. Convert once
                // for the display framebuffer and keep the planes readable.
                vec3 pigment=pow(base,vec3(1./2.2));
                vec3 key=normalize(vec3(-.44,.80,-.38));
                float sun=max(0.,dot(n,key));
                float sky=.5+.5*n.y;
                float bounce=.5+.5*dot(n,normalize(vec3(.75,.16,.5)));
                vec3 value=pigment*(vec3(.42,.46,.52)+sky*.19+bounce*.14+sun*vec3(.58,.50,.40));
                if(firstPerson==1) {
                    if(handSurface==1) {
                        pigment=tex.rgb*tint.rgb;

                        float wrap=max(0.,dot(n,key)*.65+.35);
                        value=pigment*(vec3(.41,.36,.34)+sky*.15+wrap*vec3(.55,.49,.45));
                        value*=vec3(1.035,1.,.99);
                        value+=vec3(.085,.032,.019)*pow(1.-abs(dot(n,v)),2.)*wrap;
                        value+=vec3(.050,.035,.027)*pow(max(0.,dot(n,normalize(key+v))),19.);
                    } else if(handSurface==3) {
                        vec2 thread=uv*vec2(235.,540.);
                        float twill=sin(6.283*(thread.x+thread.y));
                        float resolve=1.-smoothstep(.16,.60,fwidth(thread.x+thread.y));
                        value*=.94+.042*twill*resolve;
                        value+=vec3(.034,.016,.024)*pow(1.-abs(dot(n,v)),3.);
                    } else if(handSurface==2) {
                        float glint=pow(max(0.,dot(n,normalize(key+v))),25.);
                        value+=vec3(.31,.29,.24)*glint;
                    }
                    value+=muzzleLight(n,v)*(.14+.65*pigment);
                }
                color=vec4(mix(value,vec3(.68,.79,.92),1.-exp(-.000055*dist*dist)),ink.a);
                return;
            }
            if(finish==17) base=pow(base,vec3(.78));
            if(finish==18) base*=.93+.07*cos(pos.z*133.)*cos(pos.x*107.);
            vec3 key=normalize(vec3(-.55,.72,-.42));
            float gloss=finish==1 ? mix(22.,85.,tex.r) : (finish==2 ? 44. : 24.);
            bool weapon=firstPerson==1 && finish==17 && weaponFinish.y>0.;
            if(weapon) gloss=mix(96.,18.,weaponFinish.y);
            vec3 light=vec3(.50,.53,.58)+vec3(.96,.89,.74)*max(0.,dot(n,key));
            // Window mullions cast warm bright sunbeams across the office floor.
            float shaft=smoothstep(.28,.94,cos(dot(pos.xz,vec2(.61,-.37)) + pos.y*.7));
            light+=shaft*vec3(.34,.29,.20)*max(0.,dot(n,key));
            light+=point(laptop,vec3(.25,.95,1.25)*power,n,v,gloss);
            light+=point(tarot,vec3(1.6,.85,.38)*(.94+.06*sin(clock*8.)),n,v,gloss);
            light+=point(tarot+vec3(.55,-.30,.18),vec3(.42,.18,.82),n,v,gloss);
            float beacon=.82+.28*sin(clock*3.2);
            light+=point(northBeacon,vec3(1.4,.12,.15)*beacon,n,v,gloss);
            light+=point(southBeacon,vec3(1.4,.12,.15)*beacon,n,v,gloss);
            if(firstPerson==1) light=max(light,vec3(.68,.64,.66));
            float fres=pow(1.-max(0.,dot(n,v)),3.);
            vec3 lit=base*light+vec3(.08,.09,.11)*fres*(.35+tex.r);
            lit+=vec3(.32,.29,.22)*pow(max(0.,dot(n,normalize(v+key))),gloss)*(.18+tex.r);
            if(weapon) {
                // Broad overhead softbox across curved barrels and a softer satin lobe on wood.
                vec3 r=reflect(-v,n);
                float strip=pow(max(0.,1.-abs(dot(r,normalize(vec3(.32,.85,.41))))),gloss*.46);
                lit+=mix(vec3(.045,.036,.028),vec3(.29,.34,.40),weaponFinish.x)*strip;
            }
            if(finish==22) {
                vec2 p=vec2(abs(n.x)>.7 ? pos.z : pos.x,pos.y);
                float vein=p.y*2.9+p.x*1.8+.36*sin(p.x*3.+p.y)+.07*sin(p.y*12.);
                float filament=1.-smoothstep(.015,.047+fwidth(vein),abs(sin(vein)));
                lit+=vec3(.13,.13,.12)*filament*(.6+.4*cos(p.x*2.));
            }
            if(finish==23) {
                vec3 r=reflect(-v,n);
                float band=.5+.5*sin(r.y*13.+r.x*3.);
                lit=base*(.63+.57*band+.24*max(0.,dot(n,key)))+vec3(.17,.12,.07)*pow(band,8.);
            }
            if(finish==1) {
                // Reflect the rows of overhead luminaires and sunlight across the polished stone.
                vec3 refl=reflect(-v,n);
                vec2 hit=pos.xz+refl.xz*(11.2/max(.06,refl.y));
                vec2 cell=abs(mod(hit+3.,6.)-3.);
                float strip=exp(-cell.x*cell.x*1.1-cell.y*cell.y*6.);
                lit+=vec3(.16,.20,.26)*strip*(.14+fres*.4)*tex.r;
                vec2 pool=mod(pos.xz+3.,6.)-3.;
                lit+=vec3(.025,.028,.032)*exp(-dot(pool,pool)*.32);
            }
            if(finish==7 || finish==15 || finish==16) lit=base*1.18;
            if(finish==20) {
                lit=base*1.08;
            }
            if(finish==4 || finish==3) lit+=base*max(tex.g,tex.r)*.65;
            if(finish==10) lit=base;
            if(firstPerson==1) lit+=muzzleLight(n,v)*(.26+base);
            float fog=1.-exp(-.000055*dist*dist);
            color=vec4(mix(lit,vec3(.68,.79,.92),fog),ink.a);
        }
        """;

    public static void Load()
    {
        shader = Raylib.LoadShaderFromMemory(Vertex, Fragment);
        if (shader.Id == 0 || shader.Id == Rlgl.GetShaderIdDefault())
            throw new InvalidOperationException("Failed to compile the world lighting shader.");
        int Loc(string name) => Raylib.GetShaderLocation(shader, name);
        handSurfaceLoc=Loc("handSurface"); weaponFinishLoc=Loc("weaponFinish");
        firstPersonLoc = Loc("firstPerson");
        muzzleLoc = Loc("muzzle");
        flashLoc = Loc("flashStrength");
        beltTensionLoc = Loc("beltTension");
        sleeveAxisLoc=Loc("sleeveAxis"); sleevePullLoc=Loc("sleevePull"); wristLoc=Loc("wrist");
        handSocketLoc=Loc("handSocket"); gripLoc=Loc("gripAmount");
        batchLoc = Loc("batch"); clockLoc = Loc("clock");
        eyeLoc = Loc("eye"); finishLoc = Loc("finish"); taperLoc = Loc("taper");
        laptopLoc = Loc("laptop"); tarotLoc = Loc("tarot"); northLoc = Loc("northBeacon");
        southLoc = Loc("southBeacon"); powerLoc = Loc("power");
        box = ModelFor(Raylib.GenMeshCube(1,1,1));
        bevel = ModelFor(RoundedBox());
        plane = ModelFor(Raylib.GenMeshPlane(1,1,1,1));
        tube = ModelFor(Raylib.GenMeshCylinder(1,1,10));
        sphere = ModelFor(Raylib.GenMeshSphere(1,6,9));
        foreach (Finish f in Enum.GetValues<Finish>())
            maps[(int)f] = MakeTexture(f);
        string directory = Path.Combine(AppContext.BaseDirectory, "assets", "models");
        foreach (string path in Directory.EnumerateFiles(directory, "*.glb", SearchOption.AllDirectories))
        {
            Model model = Raylib.LoadModel(path);
            if (model.MeshCount == 0 || model.MaterialCount < 2)
                throw new InvalidDataException($"Invalid Blender model: {path}");
            ApplyShaderToModel(model);
            string name = Path.GetFileNameWithoutExtension(path);
            imports.Add(name, model);
            if (name.StartsWith("fps_hands_", StringComparison.Ordinal) || name.StartsWith("fps_left_", StringComparison.Ordinal) || name == "char_partner_90s") ReadMeshGroups(path, name, model.MeshCount);
        }
    }

    // Raylib expands each glTF primitive into a mesh, in mesh/primitive order.
    private static void ReadMeshGroups(string path, string asset, int meshCount)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        reader.BaseStream.Position = 12;
        int length = reader.ReadInt32();
        if (reader.ReadUInt32() != 0x4E4F534A) throw new InvalidDataException(path);
        using var json = JsonDocument.Parse(reader.ReadBytes(length));
        int index = 0;
        (int Start, int Count) leftArm = default, rightArm = default;
        foreach (var mesh in json.RootElement.GetProperty("meshes").EnumerateArray())
        {
            int count = mesh.GetProperty("primitives").GetArrayLength();
            if (asset == "char_partner_90s")
            {
                var materials = json.RootElement.GetProperty("materials");
                foreach (var part in mesh.GetProperty("primitives").EnumerateArray())
                {
                    string material = materials[part.GetProperty("material").GetInt32()].GetProperty("name").GetString();
                    if (material is "GunMetal" or "Gold90s" or "PolymerGrip") partnerPropMeshes.Add(index);
                    index++;
                }
                continue;
            }
            int primitiveIndex=index;
            foreach(var prim in mesh.GetProperty("primitives").EnumerateArray())
            {
                string material=json.RootElement.GetProperty("materials")[prim.GetProperty("material").GetInt32()].GetProperty("name").GetString();
                handSurfaces[(asset,primitiveIndex++)]=material=="fps_skin" ? 1 : material is "steel" or "gold" ? 2 : material=="crimson" ? 3 : 0;
            }
            if (asset != "fps_hands_cs2")
            {
                var wrist = mesh.GetProperty("extras").GetProperty("wrist");
                armWrists.Add((asset, mesh.GetProperty("name").GetString() == "Right"),
                    new Vector3(wrist[0].GetSingle(), wrist[1].GetSingle(), wrist[2].GetSingle()));
                if(mesh.GetProperty("extras").TryGetProperty("sleeveEnd",out var end))
                    sleeveEnds.Add((asset,mesh.GetProperty("name").GetString()=="Right"),
                        new(end[0].GetSingle(),end[1].GetSingle(),end[2].GetSingle()));
            }
            switch (mesh.GetProperty("name").GetString())
            {
                case "Left": leftArm = (index,count); break;
                case "Right": rightArm = (index,count); break;
                default: throw new InvalidDataException(path);
            }
            index += count;
        }
        bool isFpsArm = asset.StartsWith("fps_hands_", StringComparison.Ordinal) || asset.StartsWith("fps_left_", StringComparison.Ordinal);
        if (index != meshCount || (isFpsArm ? leftArm.Count * rightArm.Count == 0 : partnerPropMeshes.Count != 3))
            throw new InvalidDataException("Incomplete first-person arm mesh");
        if (isFpsArm) armGroups.Add(asset, (leftArm, rightArm));
    }

    public static void Arm(bool right, Vector3 wrist, string handAsset = "fps_hands_cs2", Matrix4x4? assemblyTransform = null,float grip=0)
    {
        // The existing prop camera uses a mirrored X axis; the anatomy already
        // faces the view direction. Undo that mirror for anatomical handedness.
        Vector3 bind = right ? new(-.20f,-.22f,.54f) : new(-.085f,-.185f,.68f);
        if (handAsset != "fps_hands_cs2") bind = armWrists[(handAsset,right)];
        Vector3 offset = new Vector3(-wrist.X,wrist.Y,wrist.Z) - bind;
        Model model = imports[handAsset];
        var groups = armGroups[handAsset];
        var range = right ? groups.Right : groups.Left;
        Matrix4x4 transform = Matrix4x4.CreateTranslation(offset) * Matrix4x4.CreateScale(-1,1,1);
        if (assemblyTransform.HasValue) transform *= assemblyTransform.Value;
        Surface(Finish.Anatomy,0);
        bool pull=sleeveEnds.ContainsKey((handAsset,right));
        if(pull)
        {
            Vector3 end=sleeveEnds[(handAsset,right)], axis=end-bind;
            // Keep the cuff on the authored wrist. The far end of the sleeve
            // follows an offscreen elbow while the hand turns with the prop.
            Vector3 anchor=new(right ? .56f : -.18f,-.395f,.23f);
            // The left sleeve enters from the lower-left edge, including when
            // the animated wrist passes through the middle of the frame.
            if (!right && (handAsset is "fps_hands_uzi" or "fps_hands_shotgun" || handAsset.StartsWith("fps_left_", StringComparison.Ordinal)))
                anchor.X = -.44f;
            Matrix4x4.Invert(transform,out var inverse);
            Vector3 target=Vector3.Transform(anchor,inverse)-bind;
            Raylib.SetShaderValue(shader,wristLoc,bind,ShaderUniformDataType.Vec3);
            Raylib.SetShaderValue(shader,sleeveAxisLoc,axis,ShaderUniformDataType.Vec3);
            Raylib.SetShaderValue(shader,sleevePullLoc,target,ShaderUniformDataType.Vec3);
        }
        if(grip!=0)
        {
            Vector3 socket=right ? handAsset=="fps_hands_uzi" ? new(-.18f,-.19f,.67f) : new(-.18f,-.17f,.61f)
                                 : handAsset=="fps_hands_uzi" ? new(-.183f,-.127f,.84f) : new(-.181f,-.127f,.858f);
            Raylib.SetShaderValue(shader,handSocketLoc,socket,ShaderUniformDataType.Vec3);
            Raylib.SetShaderValue(shader,gripLoc,grip,ShaderUniformDataType.Float);
        }
        for (int i=range.Start; i<range.Start+range.Count; i++)
        {
            Raylib.SetShaderValue(shader,handSurfaceLoc,handSurfaces[(handAsset,i)],ShaderUniformDataType.Int);
            Raylib.DrawMesh(model.Meshes[i],model.Materials[model.MeshMaterial[i]],Matrix4x4.Transpose(transform));
        }
        Raylib.SetShaderValue(shader,handSurfaceLoc,0,ShaderUniformDataType.Int);
        if(grip!=0) Raylib.SetShaderValue(shader,gripLoc,0f,ShaderUniformDataType.Float);
        if(pull) Raylib.SetShaderValue(shader,sleevePullLoc,Vector3.Zero,ShaderUniformDataType.Vec3);
    }

    // Keep the authored contacts while moving/rotating an entire weapon assembly.
    public static void ArmAssembly(bool right, Vector3 offset, string handAsset, Matrix4x4? transform = null,float grip=0)
    {
        Vector3 bind = armWrists[(handAsset,right)];
        Arm(right,new Vector3(-bind.X,bind.Y,bind.Z)+offset,handAsset,transform,grip);
    }

    internal static void MagazineInsert(Matrix4x4 socket,bool fresh)
    {
        Surface(Finish.Imported,0);
        var material=bevel.Materials[0];
        void Stamping(Vector3 center,Vector3 size,Color color)
        {
            var matrix=Matrix4x4.CreateScale(size)*Matrix4x4.CreateTranslation(center)*socket;
            material.Maps[0].Color=color;
            Raylib.DrawMesh(bevel.Meshes[0],material,Matrix4x4.Transpose(matrix));
        }
        var old=material.Maps[0];
        material.Maps[0].Texture=maps[(int)Finish.Imported];
        Stamping(new(0,.055f,0),new(.024f,.087f,.032f),new(40,47,53,255));
        Stamping(new(0,.055f,-.016f),new(.014f,.083f,.0025f),new(58,62,66,255));
        if(fresh) Stamping(new(0,.099f,0),new(.014f,.006f,.023f),new(178,139,76,255));
        material.Maps[0]=old;
    }

    private static Model ModelFor(Mesh mesh)
    {
        Model model = Raylib.LoadModelFromMesh(mesh);
        ApplyShaderToModel(model);
        return model;
    }

    private static void ApplyShaderToModel(Model model)
    {
        for (int m = 0; m < model.MaterialCount; m++) model.Materials[m].Shader = shader;
    }

    public static void Asset(string name, Vector3 position, float yaw = 0, float scale = 1,
                             Color? tint = null, float tilt = 0, Vector3? stretch = null, bool hideProp = false, Matrix4x4? assemblyTransform = null)
    {
        Model model = imports[name];
        model.Transform = Matrix4x4.CreateRotationX(tilt);
        Surface(name.StartsWith("char_",StringComparison.Ordinal) ? Finish.Anatomy : Finish.Imported, 0);
        if (assemblyTransform.HasValue)
        {
            var transform = Matrix4x4.CreateRotationX(tilt)
                * Matrix4x4.CreateScale((stretch ?? Vector3.One)*scale)
                * Matrix4x4.CreateRotationY(yaw*MathF.PI/180)
                * Matrix4x4.CreateTranslation(position) * assemblyTransform.Value;
            if(name.StartsWith("weapon_",StringComparison.Ordinal))
            {
                for(int i=0;i<model.MeshCount;i++)
                {
                    var material=model.Materials[model.MeshMaterial[i]];
                    var albedo=material.Maps[0];
                    material.Maps[0].Color=Raylib.ColorTint(albedo.Color,tint ?? Color.White);
                    Vector2 finish=new(material.Maps[(int)MaterialMapIndex.Metalness].Value,
                        Math.Clamp(material.Maps[(int)MaterialMapIndex.Roughness].Value,.12f,1));
                    Raylib.SetShaderValue(shader,weaponFinishLoc,finish,ShaderUniformDataType.Vec2);
                    Raylib.DrawMesh(model.Meshes[i],material,Matrix4x4.Transpose(transform));
                    material.Maps[0]=albedo;
                }
                Raylib.SetShaderValue(shader,weaponFinishLoc,Vector2.Zero,ShaderUniformDataType.Vec2);
                return;
            }
            model.Transform = Matrix4x4.Transpose(transform);
            Raylib.DrawModelEx(model,Vector3.Zero,Vector3.UnitY,0,Vector3.One,tint ?? Color.White);
            return;
        }
        if (hideProp)
        {
            var transform = model.Transform * Matrix4x4.CreateScale((stretch ?? Vector3.One)*scale)
                * Matrix4x4.CreateRotationY(yaw*MathF.PI/180) * Matrix4x4.CreateTranslation(position);
            for (int i=0;i<model.MeshCount;i++)
                if (!partnerPropMeshes.Contains(i)) Raylib.DrawMesh(model.Meshes[i],model.Materials[model.MeshMaterial[i]],Matrix4x4.Transpose(transform));
            return;
        }
        Raylib.DrawModelEx(model, position, Vector3.UnitY, yaw, (stretch ?? Vector3.One)*scale, tint ?? Color.White);
    }

    // A rounded unit box with flat face centres and two bevel bands along each edge.
    private static Mesh RoundedBox()
    {
        float[] grid = [-.5f,-.486f,-.45f,.45f,.486f,.5f];
        Mesh mesh = new() { VertexCount = 900, TriangleCount = 300 };
        mesh.AllocVertices(); mesh.AllocNormals(); mesh.AllocTexCoords();
        int i=0;
        void Vertex(Vector3 normal, Vector3 right, Vector3 up, float x, float y)
        {
            Vector3 p=normal*.5f+right*x+up*y;
            Vector3 inner=Vector3.Clamp(p,new(-.45f),new(.45f));
            Vector3 n=Vector3.Normalize(p-inner);
            ((Vector3*)mesh.Vertices)[i]=inner+n*.05f;
            ((Vector3*)mesh.Normals)[i]=n;
            ((Vector2*)mesh.TexCoords)[i++]=new(x+.5f,.5f-y);
        }
        foreach(Vector3 n in new[]{Vector3.UnitX,-Vector3.UnitX,Vector3.UnitY,-Vector3.UnitY,Vector3.UnitZ,-Vector3.UnitZ})
        {
            Vector3 right=Vector3.Normalize(Vector3.Cross(Math.Abs(n.Y)>.9f ? Vector3.UnitZ : Vector3.UnitY,n));
            Vector3 up=Vector3.Cross(n,right);
            for(int a=0;a<5;a++) for(int b=0;b<5;b++)
            {
                float x=grid[a], y=grid[b], xx=grid[a+1], yy=grid[b+1];
                Vertex(n,right,up,x,y); Vertex(n,right,up,xx,y); Vertex(n,right,up,xx,yy);
                Vertex(n,right,up,x,y); Vertex(n,right,up,xx,yy); Vertex(n,right,up,x,yy);
            }
        }
        Raylib.UploadMesh(ref mesh,false);
        return mesh;
    }

    public static void World(Camera3D cam, SkyscraperFloor1World world)
    {
        Raylib.SetShaderValue(shader,firstPersonLoc,0,ShaderUniformDataType.Int);
        Raylib.SetShaderValue(shader,clockLoc,(float)Raylib.GetTime(),ShaderUniformDataType.Float);
        View(cam.Position,world.CentralMacBookPos+new Vector3(0,1.6f,.35f),
             world.HrTarotBoothPos+new Vector3(0,1.65f,-.6f),
             world.NorthElevatorPos+new Vector3(0,3.7f,-1),
             world.SouthElevatorDoorPos+new Vector3(0,3.7f,1),world.IsMacBookTrapActive ? 1.4f : .42f);
    }

    public static void SetBeltTension(float amount) =>
        Raylib.SetShaderValue(shader,beltTensionLoc,amount,ShaderUniformDataType.Float);

    public static void DrawRosinSmoke(Vector3 tip)
    {
        Rlgl.DisableDepthMask();
        Draw(plane,tip+Vector3.UnitY*.0475f,new(.043f,1,.095f),Color.White,Finish.RosinSmoke,Vector3.UnitX,90);
        Rlgl.EnableDepthMask();
    }

    internal static void FlashLight(Vector3 tip,float strength)
    {
        Raylib.SetShaderValue(shader,muzzleLoc,tip,ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader,flashLoc,strength,ShaderUniformDataType.Float);
    }

    public static void Hands()
    {
        Raylib.SetShaderValue(shader,flashLoc,0f,ShaderUniformDataType.Float);
        Raylib.SetShaderValue(shader,firstPersonLoc,1,ShaderUniformDataType.Int);
        Vector3 distant=new(50,50,50);
        View(Vector3.Zero,distant,distant,distant,distant,0);
    }

    private static void View(Vector3 eye,Vector3 laptop,Vector3 tarot,Vector3 north,Vector3 south,float power)
    {
        void Vec(int loc,Vector3 v) => Raylib.SetShaderValue(shader,loc,v,ShaderUniformDataType.Vec3);
        Vec(eyeLoc,eye); Vec(laptopLoc,laptop); Vec(tarotLoc,tarot); Vec(northLoc,north); Vec(southLoc,south);
        Raylib.SetShaderValue(shader,powerLoc,power,ShaderUniformDataType.Float);
    }

    private static void Draw(Model m, Vector3 p, Vector3 scale, Color tint, Finish f, Vector3 axis, float angle=0,float taper=0)
    {
        Surface(f,taper);
        m.Materials[0].Maps[0].Texture=maps[(int)f];
        Raylib.DrawModelEx(m,p,axis,angle,scale,tint);
    }

    private static void Surface(Finish f, float taper)
    {
        if (f!=activeFinish)
        {
            Raylib.SetShaderValue(shader,finishLoc,(int)f,ShaderUniformDataType.Int);
            activeFinish=f;
        }
        if(taper!=activeTaper)
        {
            Raylib.SetShaderValue(shader,taperLoc,taper,ShaderUniformDataType.Float);
            activeTaper=taper;
        }
    }

    public static void Box(Vector3 p,float w,float h,float d,Color tint,Finish f=Finish.Paint)
        => Draw(f is Finish.Paint or Finish.Plaster or Finish.Lamp ? box : bevel,p,new(w,h,d),tint,f,Vector3.UnitY);
    public static void Orb(Vector3 p,float r,Color tint,Finish f=Finish.Paint)
        => Oval(p,new(r),tint,f);
    public static void Oval(Vector3 p,Vector3 radii,Color tint,Finish f=Finish.Paint)
        => Draw(sphere,p,radii,tint,f,Vector3.UnitY);
    public static void Rod(Vector3 start, Vector3 end,float r,float tip,int sides,Color tint,Finish f=Finish.Paint)
    {
        Vector3 d=end-start;
        float len=d.Length();
        if(len<.00001f || Math.Max(r,tip)<=0) return;
        if(r<tip) { Rod(end,start,tip,r,sides,tint,f); return; }
        Vector3 dir=d/len;
        Vector3 axis=Vector3.Cross(Vector3.UnitY,dir);
        axis=axis.LengthSquared()<1e-10f ? Vector3.UnitX : Vector3.Normalize(axis);
        Draw(tube,start,new(r,len,r),tint,f,axis,MathF.Acos(Math.Clamp(dir.Y,-1,1))*180/MathF.PI,tip/r-1);
    }

    public static void Floor(float w,float d) => Draw(plane,Vector3.Zero,new(w,1,d),Color.White,Finish.Slate,Vector3.UnitY);
    public static void Shadow(Vector3 p,float w,float d) => Draw(plane,new(p.X,.006f,p.Z),new(w,1,d),Color.White,Finish.Contact,Vector3.UnitY);

    public static void DrawCharacterDirectionalShadow(Vector3 pos, float height, float radius)
    {
        Vector3 direction = Vector3.Normalize(new Vector3(.65f, 0, .45f));
        float length = height * 1.7f;
        Vector3 middle = new Vector3(pos.X, Math.Max(.043f, pos.Y + .012f), pos.Z) + direction * (length * .48f);
        Rlgl.DisableDepthMask();
        Draw(plane, middle, new(radius * 3.4f, 1, length), Color.White, Finish.CharacterShadow,
             Vector3.UnitY, MathF.Atan2(direction.X, direction.Z) * 180 / MathF.PI);
        Rlgl.EnableDepthMask();
    }

    public static void OrientedBox(Vector3 p, Vector3 size, float yaw, Color color) =>
        Draw(bevel,p,size,color,Finish.Paint,Vector3.UnitY,yaw*180/MathF.PI);

    // Upload the fixed architecture once. An entire bank of blinds or lit towers
    // then costs one draw per finish, independent of the number of window bays.
    private static readonly List<(Finish Finish, Model Model)> architecture = new();

    internal sealed class ArchitectureBuilder
    {
        private readonly record struct Block(Vector3 Center, Vector3 Size, Color Color, float Yaw);
        private readonly Dictionary<Finish,List<Block>> groups = new();

        public void Add(Vector3 center, Vector3 size, Color color, Finish finish=Finish.Paint, float yaw=0)
        {
            if (!groups.TryGetValue(finish,out var blocks)) groups[finish]=blocks=new();
            blocks.Add(new(center,size,color,yaw));
        }

        public void Upload()
        {
            Mesh cube = box.Meshes[0];
            foreach (var (finish, blocks) in groups)
            {
                Mesh mesh = new() { TriangleCount=blocks.Count*cube.TriangleCount };
                mesh.VertexCount=mesh.TriangleCount*3;
                mesh.AllocVertices(); mesh.AllocNormals(); mesh.AllocTexCoords(); mesh.AllocColors();
                int vertex=0;
                foreach (var block in blocks)
                {
                    var rotation=Matrix4x4.CreateRotationY(block.Yaw);
                    for (int i=0;i<cube.TriangleCount*3;i++,vertex++)
                    {
                        int source=cube.Indices==null ? i : cube.Indices[i];
                        Vector3 local=((Vector3*)cube.Vertices)[source]*block.Size;
                        ((Vector3*)mesh.Vertices)[vertex]=Vector3.Transform(local,rotation)+block.Center;
                        ((Vector3*)mesh.Normals)[vertex]=Vector3.TransformNormal(((Vector3*)cube.Normals)[source],rotation);
                        ((Vector2*)mesh.TexCoords)[vertex]=((Vector2*)cube.TexCoords)[source];
                        ((Color*)mesh.Colors)[vertex]=block.Color;
                    }
                }
                Raylib.UploadMesh(ref mesh,false);
                Model model=ModelFor(mesh);
                model.Materials[0].Maps[0].Texture=maps[(int)finish];
                architecture.Add((finish,model));
            }
        }
    }

    public static void DrawArchitecture(Action<ArchitectureBuilder> build, bool glass=false)
    {
        if (architecture.Count==0)
        {
            var builder=new ArchitectureBuilder();
            build(builder);
            builder.Upload();
        }
        Raylib.SetShaderValue(shader,batchLoc,1,ShaderUniformDataType.Int);
        if (glass) Rlgl.DisableDepthMask();
        foreach (var group in architecture)
        {
            if ((group.Finish==Finish.Glass)!=glass) continue;
            Surface(group.Finish,0);
            Raylib.DrawModel(group.Model,Vector3.Zero,1,Color.White);
        }
        if (glass) Rlgl.EnableDepthMask();
        Raylib.SetShaderValue(shader,batchLoc,0,ShaderUniformDataType.Int);
    }

    private static int Hash(int x,int y)
    {
        uint h=unchecked((uint)(x*374761393+y*668265263));
        h=(h^(h>>13))*1274126177u;
        return (int)((h^(h>>16))&255);
    }

    private static Texture2D MakeTexture(Finish f)
    {
        int size=f is Finish.Paint or Finish.Contact or Finish.Lamp or Finish.Sky or Finish.Facade
                    or Finish.Glass or Finish.Imported or Finish.Carpet or Finish.CharacterShadow or Finish.Fluorescent or Finish.Anatomy
                    or Finish.BlackMarble or Finish.Bronze or Finish.RosinSmoke ? 1 : 512;
        Image img=f==Finish.Lift ? Raylib.GenImageColor(1792,384,new Color(16,25,30,255))
                                 : Raylib.GenImageColor(size,size,Color.White);
        Color* pixels=(Color*)img.Data;
        if(f!=Finish.Lift)
            for(int y=0;y<size;y++) for(int x=0;x<size;x++) pixels[y*size+x]=Pixel(f,x,y);
        void Inscription(string value, int size, int y, Color ink)
        {
            Font native = GameFonts.Get(Typeface.InterfaceBold, Math.Max(1, size / 2));
            int drawSize = native.BaseSize;
            int x = (512 - (int)Raylib.MeasureTextEx(native,value,drawSize,0).X)/2;
            Raylib.ImageDrawTextEx(ref img,native,value,new(x,y),drawSize,0,ink);
        }
        if(f==Finish.Lift)
        {
            Color brass=new(235,195,128,255), ivory=new(253,242,215,255);
            void Letter(string value,int x,int y,int height,Color color)
            {
                Font native = GameFonts.Get(Typeface.InterfaceBold, Math.Max(1, height / 2));
                Raylib.ImageDrawTextEx(ref img,native,value,new(x,y),native.BaseSize,0,color);
            }
            Letter("ЭТАЖ",92,32,76,brass);
            Letter("01",78,104,236,ivory);
            Raylib.ImageDrawRectangle(ref img,496,48,6,288,brass);
            Letter("CLOUD ENTROPIC",548,46,128,ivory);
            Letter("T O W E R",552,204,112,brass);
            Raylib.ImageDrawTriangle(ref img,new(372,155),new(410,102),new(448,155),new Color(111,234,218,255));
            Raylib.ImageDrawTriangle(ref img,new(372,211),new(448,211),new(410,264),brass);
        }
        if(f==Finish.Tarot) Inscription("A R C A N A",48,436,new Color(244,209,146,255));
        if(f is Finish.Exit or Finish.Billboard)
        {
            Raylib.ImageClearBackground(ref img, f==Finish.Exit ? new Color(156,18,39,255) : new Color(27,17,57,255));
            Inscription(f==Finish.Exit ? "ВЫХОД" : "NOVA",96,202,Color.White);
            Raylib.ImageDrawRectangle(ref img,24,165,464,5,Color.White);
            Raylib.ImageDrawRectangle(ref img,24,344,464,5,Color.White);
        }
        Texture2D tex=Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        if(tex.Id==0) throw new InvalidOperationException($"Could not upload {f}.");
        if(size>1) { Raylib.GenTextureMipmaps(ref tex); Raylib.SetTextureFilter(tex,TextureFilter.Anisotropic8X); }
        return tex;
    }

    private static Color Pixel(Finish f,int x,int y)
    {
        int grain=Hash(x,y)%11-5;
        int band=Hash(0,y)%13-6;
        static Color Rgb(float r,float g,float b) => new((int)Math.Clamp(r,0,255),(int)Math.Clamp(g,0,255),(int)Math.Clamp(b,0,255),255);
        switch(f)
        {
            case Finish.Slate:
                float phase=x*.026f+y*.043f+1.7f*MathF.Sin(y*.025f)+MathF.Sin(x*.04f);
                float vein=MathF.Pow(MathF.Max(0,MathF.Cos(phase)),15);
                int seam=Math.Min(x%256,y%256);
                float s=(seam<3 ? -22 : seam==4 ? 10 : 0)+grain+vein*4+Hash(x/256,y/256)%8;
                return Rgb(53+s,58+s,67+s);
            case Finish.Armor:
                int panel=Math.Min(Math.Min(x,511-x),Math.Min(y%256,255-y%256));
                int boltX=Math.Min(x,511-x)-22, boltY=Math.Min(y%256,255-y%256)-18;
                int bolt=boltX*boltX+boltY*boltY;
                if(bolt<37) return Rgb(85-boltY*6,92-boltY*6,97-boltY*6);
                if(y>442 && y<466) return ((x+y)/22)%2==0 ? Rgb(189+grain,133+grain,53) : Rgb(38,39,44);
                float steel=panel<5 ? 41 : panel<8 ? 138 : 96+grain+band+12f*MathF.Cos(x*.016f);
                return Rgb(steel,steel+7,steel+14);
            case Finish.Rack:
                int row=y%64;
                if(row<4 || x<13 || x>498) return Rgb(57+band,66+band,73+band);
                if(x<140 && row>20 && row<28 && x%22<9) return x%44<22 ? Rgb(48,231,162) : Rgb(43,169,218);
                if(Math.Abs(x-405-26*MathF.Sin(y*.023f))<5) return Rgb(57,81,92);
                int mesh=(x+(y/8%2)*4)%9<4 && y%8<5 ? 13 : 45+grain;
                return Rgb(mesh,mesh+5,mesh+10);
            case Finish.Screen:
                if(y<33 || x<66) return Rgb(24,38,52);
                int line=y/20, col=x/12;
                int indent=90+24*(line%4);
                if(y%20>5 && y%20<13 && x>indent && x<indent+76+Hash(line,7)%184 && x%12<10)
                    return ((col/4+line)%3) switch {0=>Rgb(106,222,185),1=>Rgb(95,197,248),_=>Rgb(236,117,182)};
                return Rgb(10,21,31);
            case Finish.Fabric:
                float weave=grain+((x+y)%9<3 ? 15 : -6)+(y%5==0 ? 8 : 0)+9*MathF.Sin(x*.035f+y*.014f);
                return Rgb(161+weave,35+weave*.48f,62+weave*.72f);
            case Finish.Leather:
                float a=(x+y)%128/128f,b=(x-y+512)%128/128f;
                float quilt=MathF.Pow(MathF.Max(0,MathF.Sin(MathF.PI*a)*MathF.Sin(MathF.PI*b)),.42f);
                float l=grain+quilt*41;
                return Rgb(76+l,32+l*.62f,36+l*.48f);
            case Finish.Plaster:
                float concrete=grain*.85f+3*MathF.Sin(y*.023f+x*.017f);
                if(y<3 || x<2) concrete-=33;
                return Rgb(201+concrete,204+concrete,210+concrete);
            case Finish.Tarot:
                float dx=x-256,dy=y-204,r=MathF.Sqrt(dx*dx+dy*dy);
                bool gilded=Math.Abs(r-106)<3 || Math.Abs(r-83)<2 || (r>114 && r<188 && MathF.Cos(32*MathF.Atan2(dy,dx))>.91f);
                if(r<62 && (dx-25)*(dx-25)+(dy+10)*(dy+10)>51*51) gilded=true;
                return gilded ? Rgb(232+grain,185+grain,112+grain) : Rgb(56+grain,28+grain,52+grain);
            case Finish.Lift: return Rgb(25+grain,35+grain,39+grain);
            default: return Color.White;
        }
    }

    public static void Unload()
    {
        foreach(Model m in imports.Values) Raylib.UnloadModel(m);
        imports.Clear();
        partnerPropMeshes.Clear();
        armGroups.Clear();
        armWrists.Clear();
        sleeveEnds.Clear();
        handSurfaces.Clear();
        lastPartPose.Clear();
        reloadMeshes.Clear();
        foreach(var group in architecture) Raylib.UnloadModel(group.Model);
        architecture.Clear();
        foreach(Model m in new[]{box,bevel,plane,tube,sphere}) Raylib.UnloadModel(m);
        foreach(Texture2D t in maps) Raylib.UnloadTexture(t);
        Raylib.UnloadShader(shader);
    }
}
