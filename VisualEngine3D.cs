using System.Text.Json;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal enum Finish { Paint, Slate, Armor, Rack, Screen, Fabric, Leather, Lamp, Contact, Tarot, Lift, Plaster, Facade, Sky, Glass, Exit, Billboard, Imported, Carpet, CharacterShadow, Fluorescent, Anatomy }

internal static unsafe class VisualEngine3D
{
    private static Shader shader;
    private static Model box, bevel, plane, tube, sphere;
    private static readonly Dictionary<string, Model> imports = new(StringComparer.OrdinalIgnoreCase);
    private static (int Start, int Count) leftArm, rightArm;
    private static readonly HashSet<int> partnerPropMeshes = new();
    private static int batchLoc, clockLoc;
    private static readonly Texture2D[] maps = new Texture2D[Enum.GetValues<Finish>().Length];
    private static int firstPersonLoc;
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
        uniform int firstPerson;
        out vec3 pos, norm;
        out vec2 localXZ;
        out vec2 uv;
        void main() {
            vec3 p = vertexPosition;
            p.xz *= 1.0 + p.y*taper;
            vec3 n = vertexNormal;
            n.y -= taper * dot(vertexPosition.xz,n.xz);
            pos = (matModel * vec4(p,1)).xyz;
            norm = normalize(transpose(inverse(mat3(matModel))) * n);
            uv = vertexTexCoord; localXZ = vertexPosition.xz;
            tint = batch == 1 ? vertexColor : vec4(1.);
            gl_Position = mvp * vec4(p,1);
            if (firstPerson==1) gl_Position.z = gl_Position.z*.002 + gl_Position.w*(-.998);
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
        uniform float power, clock;
        uniform int firstPerson;
        uniform int finish;
        out vec4 color;
        vec3 point(vec3 at, vec3 tint, vec3 n, vec3 v, float gloss) {
            vec3 d=at-pos;
            float r=dot(d,d);
            vec3 l=d*inversesqrt(max(.001,r));
            float spec=pow(max(0.,dot(n,normalize(v+l))),gloss);
            return tint*(.12+max(0.,dot(n,l))+spec*1.4)/(1.+r*.17);
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
            vec3 n=normalize(cross(dFdx(pos), dFdy(pos))), v=normalize(eye-pos);
            if(dot(n,norm)<0.) n=-n;
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
                color=vec4(mix(value,vec3(.68,.79,.92),1.-exp(-.000055*dist*dist)),ink.a);
                return;
            }
            if(finish==17) base=pow(base,vec3(.78));
            if(finish==18) base*=.93+.07*cos(pos.z*133.)*cos(pos.x*107.);
            vec3 key=normalize(vec3(-.55,.72,-.42));
            float gloss=finish==1 ? mix(22.,85.,tex.r) : (finish==2 ? 44. : 24.);
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
            if(finish==1) {
                // Reflect the rows of overhead luminaires and sunlight across the polished stone.
                vec3 refl=reflect(-v,n);
                vec2 hit=pos.xz+refl.xz*(6./max(.06,refl.y));
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
            if(finish==4 || finish==3 || finish==10) lit+=base*max(tex.g,tex.r)*.65;
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
        firstPersonLoc = Loc("firstPerson");
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
            if (name is "fps_hands_cs2" or "char_partner_90s") ReadMeshGroups(path, name, model.MeshCount);
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
            switch (mesh.GetProperty("name").GetString())
            {
                case "Left": leftArm = (index,count); break;
                case "Right": rightArm = (index,count); break;
                default: throw new InvalidDataException(path);
            }
            index += count;
        }
        if (index != meshCount || (asset == "fps_hands_cs2" ? leftArm.Count * rightArm.Count == 0 : partnerPropMeshes.Count != 3))
            throw new InvalidDataException("Incomplete first-person arm mesh");
    }

    public static void Arm(bool right, Vector3 wrist)
    {
        // The existing prop camera uses a mirrored X axis; the anatomy already
        // faces the view direction. Undo that mirror for anatomical handedness.
        Vector3 bind = right ? new(-.20f,-.22f,.54f) : new(-.085f,-.185f,.68f);
        Vector3 offset = new Vector3(-wrist.X,wrist.Y,wrist.Z) - bind;
        Model model = imports["fps_hands_cs2"];
        var range = right ? rightArm : leftArm;
        Matrix4x4 transform = Matrix4x4.CreateTranslation(offset) * Matrix4x4.CreateScale(-1,1,1);
        Surface(Finish.Anatomy,0);
        for (int i=range.Start; i<range.Start+range.Count; i++)
            Raylib.DrawMesh(model.Meshes[i],model.Materials[model.MeshMaterial[i]],Matrix4x4.Transpose(transform));
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
                             Color? tint = null, float tilt = 0, Vector3? stretch = null, bool hideProp = false)
    {
        Model model = imports[name];
        model.Transform *= Matrix4x4.CreateRotationX(tilt);
        Surface(name.StartsWith("char_",StringComparison.Ordinal) ? Finish.Anatomy : Finish.Imported, 0);
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

    public static void Hands()
    {
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
                    or Finish.Glass or Finish.Imported or Finish.Carpet or Finish.CharacterShadow or Finish.Fluorescent or Finish.Anatomy ? 1 : 512;
        Image img=Raylib.GenImageColor(size,size,Color.White);
        Color* pixels=(Color*)img.Data;
        for(int y=0;y<size;y++) for(int x=0;x<size;x++) pixels[y*size+x]=Pixel(f,x,y);
        void Inscription(string value, int size, int y, Color ink)
        {
            Font native = GameFonts.Get(Typeface.InterfaceBold,size);
            int x = (512 - (int)Raylib.MeasureTextEx(native,value,size,0).X)/2;
            Raylib.ImageDrawTextEx(ref img,native,value,new(x,y),size,0,ink);
        }
        if(f==Finish.Lift)
        {
            Inscription("01",260,23,new Color(244,211,150,255));
            Inscription("CLOUD ENTROPIC",49,302,new Color(205,207,192,255));
            Inscription("T O W E R",44,372,new Color(218,159,87,255));
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
        leftArm=rightArm=default;
        foreach(var group in architecture) Raylib.UnloadModel(group.Model);
        architecture.Clear();
        foreach(Model m in new[]{box,bevel,plane,tube,sphere}) Raylib.UnloadModel(m);
        foreach(Texture2D t in maps) Raylib.UnloadTexture(t);
        Raylib.UnloadShader(shader);
    }
}
