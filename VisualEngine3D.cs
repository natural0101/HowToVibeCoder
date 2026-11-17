using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal enum Finish { Paint, Slate, Armor, Rack, Screen, Fabric, Leather, Lamp, Contact, Tarot, Lift, Plaster }

internal static unsafe class VisualEngine3D
{
    private static Shader shader;
    private static Model box, bevel, plane, tube, sphere;
    private static readonly Texture2D[] maps = new Texture2D[Enum.GetValues<Finish>().Length];
    private static int eyeLoc, finishLoc, taperLoc, laptopLoc, tarotLoc, northLoc, southLoc, powerLoc;
    private static Finish? activeFinish;
    private static float activeTaper;

    private const string Vertex = """
        #version 330 core
        in vec3 vertexPosition;
        in vec3 vertexNormal;
        in vec2 vertexTexCoord;
        uniform mat4 mvp, matModel;
        uniform float taper;
        out vec3 pos, norm;
        out vec2 uv;
        void main() {
            vec3 p = vertexPosition;
            p.xz *= 1.0 + p.y*taper;
            vec3 n = vertexNormal;
            n.y -= taper * dot(vertexPosition.xz,n.xz);
            pos = (matModel * vec4(p,1)).xyz;
            norm = normalize(transpose(inverse(mat3(matModel))) * n);
            uv = vertexTexCoord;
            gl_Position = mvp * vec4(p,1);
        }
        """;

    private const string Fragment = """
        #version 330 core
        in vec3 pos, norm;
        in vec2 uv;
        uniform sampler2D texture0;
        uniform vec4 colDiffuse;
        uniform vec3 eye, laptop, tarot, northBeacon, southBeacon;
        uniform float power;
        uniform int finish;
        out vec4 color;
        vec3 point(vec3 at, vec3 tint, vec3 n, vec3 v, float gloss) {
            vec3 d=at-pos;
            float r=dot(d,d);
            vec3 l=d*inversesqrt(max(.001,r));
            float spec=pow(max(0.,dot(n,normalize(v+l))),gloss);
            return tint*(.12+max(0.,dot(n,l))+spec*1.4)/(1.+r*.17);
        }
        void main() {
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
            vec3 base=tex.rgb*colDiffuse.rgb;
            vec3 key=normalize(vec3(-.55,.77,.48));
            float gloss=finish==1 ? mix(22.,85.,tex.r) : (finish==2 ? 44. : 24.);
            vec3 light=mix(vec3(.26,.26,.35),vec3(.68,.78,.88),n.y*.5+.5)
                       + vec3(.88,.76,.64)*max(0.,dot(n,key));
            light+=point(laptop,vec3(.12,1.4,1.75)*power,n,v,gloss);
            light+=point(tarot,vec3(2.8,.45,.76),n,v,gloss);
            light+=point(northBeacon,vec3(2.,.63,.16),n,v,gloss);
            light+=point(southBeacon,vec3(2.,.63,.16),n,v,gloss);
            float fres=pow(1.-max(0.,dot(n,v)),3.);
            vec3 lit=base*light+vec3(.10,.16,.21)*fres*(.35+tex.r);
            lit+=vec3(.22,.19,.15)*pow(max(0.,dot(n,normalize(v+key))),gloss)*(.18+tex.r);
            if(finish==1) {
                // Reflect the rows of overhead luminaires across the polished stone.
                vec3 refl=reflect(-v,n);
                vec2 hit=pos.xz+refl.xz*(6./max(.06,refl.y));
                vec2 cell=abs(mod(hit+4.,8.)-4.);
                float strip=exp(-cell.x*cell.x*1.1-cell.y*cell.y*6.);
                lit+=vec3(.35,.48,.58)*strip*(.25+fres)*tex.r;
            }
            if(finish==7) lit=base*1.18;
            if(finish==4 || finish==3 || finish==10) lit+=base*max(tex.g,tex.r)*.65;
            float dist=length(eye-pos);
            float fog=1.-exp(-.00062*dist*dist);
            color=vec4(mix(lit,vec3(.20,.24,.31),fog),colDiffuse.a);
        }
        """;

    public static void Load(Font font)
    {
        shader = Raylib.LoadShaderFromMemory(Vertex, Fragment);
        if (shader.Id == 0 || shader.Id == Rlgl.GetShaderIdDefault())
            throw new InvalidOperationException("Failed to compile the world lighting shader.");
        int Loc(string name) => Raylib.GetShaderLocation(shader, name);
        eyeLoc = Loc("eye"); finishLoc = Loc("finish"); taperLoc = Loc("taper");
        laptopLoc = Loc("laptop"); tarotLoc = Loc("tarot"); northLoc = Loc("northBeacon");
        southLoc = Loc("southBeacon"); powerLoc = Loc("power");
        box = ModelFor(Raylib.GenMeshCube(1,1,1));
        bevel = ModelFor(RoundedBox());
        plane = ModelFor(Raylib.GenMeshPlane(1,1,1,1));
        tube = ModelFor(Raylib.GenMeshCylinder(1,1,10));
        sphere = ModelFor(Raylib.GenMeshSphere(1,6,9));
        foreach (Finish f in Enum.GetValues<Finish>())
            maps[(int)f] = MakeTexture(f,font);
    }

    private static Model ModelFor(Mesh mesh)
    {
        Model model = Raylib.LoadModelFromMesh(mesh);
        model.Materials[0].Shader = shader;
        return model;
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
        View(cam.Position,world.CentralMacBookPos+new Vector3(0,1.6f,.35f),
             world.HrTarotBoothPos+new Vector3(0,1.65f,-.6f),
             world.NorthElevatorPos+new Vector3(0,3.7f,-1),
             world.SouthElevatorDoorPos+new Vector3(0,3.7f,1),world.IsMacBookTrapActive ? 1.4f : .42f);
    }

    public static void Hands() => View(Vector3.Zero,new(-2,1,1),new(2,.8f,1),new(0,2,-2),new(0,3,3),.55f);

    private static void View(Vector3 eye,Vector3 laptop,Vector3 tarot,Vector3 north,Vector3 south,float power)
    {
        void Vec(int loc,Vector3 v) => Raylib.SetShaderValue(shader,loc,v,ShaderUniformDataType.Vec3);
        Vec(eyeLoc,eye); Vec(laptopLoc,laptop); Vec(tarotLoc,tarot); Vec(northLoc,north); Vec(southLoc,south);
        Raylib.SetShaderValue(shader,powerLoc,power,ShaderUniformDataType.Float);
    }

    private static void Draw(Model m, Vector3 p, Vector3 scale, Color tint, Finish f, Vector3 axis, float angle=0,float taper=0)
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
        m.Materials[0].Maps[0].Texture=maps[(int)f];
        Raylib.DrawModelEx(m,p,axis,angle,scale,tint);
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

    private static int Hash(int x,int y)
    {
        uint h=unchecked((uint)(x*374761393+y*668265263));
        h=(h^(h>>13))*1274126177u;
        return (int)((h^(h>>16))&255);
    }

    private static Texture2D MakeTexture(Finish f,Font font)
    {
        int size=f is Finish.Paint or Finish.Contact or Finish.Lamp ? 1 : 512;
        Image img=Raylib.GenImageColor(size,size,Color.White);
        Color* pixels=(Color*)img.Data;
        for(int y=0;y<size;y++) for(int x=0;x<size;x++) pixels[y*size+x]=Pixel(f,x,y);
        if(f==Finish.Lift)
        {
            Raylib.ImageDrawTextEx(ref img,font,"01",new(104,23),260,0,new Color(244,211,150,255));
            Raylib.ImageDrawTextEx(ref img,font,"CLOUD ENTROPIC",new(42,302),49,1,new Color(205,207,192,255));
            Raylib.ImageDrawTextEx(ref img,font,"T O W E R",new(136,372),44,1,new Color(218,159,87,255));
        }
        if(f==Finish.Tarot)
            Raylib.ImageDrawTextEx(ref img,font,"A R C A N A",new(98,436),48,1,new Color(244,209,146,255));
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
                float s=(seam<3 ? -22 : seam==4 ? 10 : 0)+grain+vein*17+Hash(x/256,y/256)%8;
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
        foreach(Model m in new[]{box,bevel,plane,tube,sphere}) Raylib.UnloadModel(m);
        foreach(Texture2D t in maps) Raylib.UnloadTexture(t);
        Raylib.UnloadShader(shader);
    }
}
