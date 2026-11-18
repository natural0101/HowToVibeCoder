using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal static class CombatSprites
{
    // Left half is the gas jet in bore coordinates. Right half is a radial bloom,
    // also used as the cross section of a tapered streak.
    private static Texture2D flash, smoke;
    private static Shader viewShader;

    internal static void Load()
    {
        // Match the depth range of the opaque viewmodel. The default texture fragment
        // shader consumes these UVs and the straight-alpha vertex tint.
        viewShader=Raylib.LoadShaderFromMemory("""
            #version 330 core
            in vec3 vertexPosition;
            in vec2 vertexTexCoord;
            in vec4 vertexColor;
            uniform mat4 mvp;
            out vec2 fragTexCoord;
            out vec4 fragColor;
            void main() {
                fragTexCoord=vertexTexCoord; fragColor=vertexColor;
                gl_Position=mvp*vec4(vertexPosition,1.);
                gl_Position.z=.1*gl_Position.z-.9*gl_Position.w;
            }
            """,null);
        if (viewShader.Id==0 || viewShader.Id==Rlgl.GetShaderIdDefault())
            throw new InvalidOperationException("Could not load the viewmodel flash shader.");
        flash = Bake(512, 256, (x, y) =>
        {
            if (x >= 256)
            {
                float dx = (x - 383.5f) / 128, dy = (y - 127.5f) / 128;
                return new Color(255, 255, 255, Alpha(MathF.Exp(-5 * (dx*dx + dy*dy)) *
                    Math.Clamp(5 * (1 - dx*dx - dy*dy), 0, 1)));
            }
            float u = (x + .5f) / 256, v = (y - 127.5f) / 128;
            float noise = Fbm(10*u, 3*v + 9);
            float center = .055f*MathF.Sin(12*u) + (noise-.5f)*.43f*u;
            float core = Gaussian((v-center)/(.085f+.04f*u)) * MathF.Exp(-3.5f*u);
            float flame = Gaussian((v-center)/(.19f+.12f*u)) * MathF.Exp(-2.1f*u);
            float tongues = Gaussian((v-center-.45f*u)/(.045f+.08f*(1-u))) * MathF.Exp(-1.7f*u)
                          + Gaussian((v-center+.35f*u)/.085f) * MathF.Exp(-2.2f*u);
            float vents = Gaussian((u-.13f)/.085f) * Gaussian((Math.Abs(v)-2.2f*u)/.14f);
            float heat = Math.Clamp(core*1.8f + flame*.7f, 0, 1);
            float density = (core + flame*.73f + tongues*.67f + vents*.40f) * (.18f+1.5f*noise);
            float edge = Math.Clamp(u*70,0,1) * Math.Clamp((1-u)*7,0,1);
            return new Color(255-(int)(16*core), 107+(int)(146*heat), 30+(int)(225*heat*heat), Alpha(density*edge));
        });
        smoke = Bake(128, 128, (x, y) =>
        {
            float u=(x-63.5f)/64, v=(y-63.5f)/64;
            float n = Fbm(3.1f*u + 1.4f*Fbm(3*v, u+3), 3.6f*v+7);
            float edge = Math.Clamp((1-u*u-v*v)*2,0,1);
            float density = Math.Clamp((n-.30f)*2.5f,0,1) * edge*edge * MathF.Exp(-1.5f*(u*u+v*v));
            return new Color(218+(int)(28*n), 217+(int)(29*n), 215+(int)(30*n), Alpha(density));
        });
    }

    private static float Gaussian(float x) => MathF.Exp(-x*x);
    private static int Alpha(float x) => (int)(255*Math.Clamp(x,0,1));

    private static float Noise(int x,int y)
    {
        uint h = unchecked((uint)x * 0x85ebca6bu + (uint)y * 0xc2b2ae35u);
        h ^= h >> 16;
        h = unchecked(h * 0x7feb352du);
        return (h ^ (h >> 15)) / (float)uint.MaxValue;
    }

    private static float Fbm(float x,float y)
    {
        float sum=0, amplitude=.54f;
        for (int i=0;i<4;i++)
        {
            int ix=(int)MathF.Floor(x), iy=(int)MathF.Floor(y);
            float fx=x-ix, fy=y-iy;
            fx=fx*fx*(3-2*fx);
            fy=fy*fy*(3-2*fy);
            sum += amplitude*float.Lerp(float.Lerp(Noise(ix,iy),Noise(ix+1,iy),fx),
                                        float.Lerp(Noise(ix,iy+1),Noise(ix+1,iy+1),fx),fy);
            x=x*2.07f+13; y=y*2.07f-7; amplitude*=.5f;
        }
        return sum;
    }

    private static unsafe Texture2D Bake(int width,int height,Func<int,int,Color> pixel)
    {
        Image img=Raylib.GenImageColor(width,height,Color.Blank);
        try
        {
            var pixels=new Span<Color>(img.Data,width*height);
            for (int y=0;y<height;y++)
                for (int x=0;x<width;x++) pixels[y*width+x]=pixel(x,y);
            var texture=Raylib.LoadTextureFromImage(img);
            Raylib.SetTextureFilter(texture,TextureFilter.Bilinear);
            Raylib.SetTextureWrap(texture,TextureWrap.Clamp);
            return texture;
        }
        finally { Raylib.UnloadImage(img); }
    }

    internal static void Unload()
    {
        Raylib.UnloadShader(viewShader);
        Raylib.UnloadTexture(smoke);
        Raylib.UnloadTexture(flash);
    }

    internal static void Begin(BlendMode blend)
    {
        Rlgl.DrawRenderBatchActive();
        Raylib.BeginBlendMode(blend);
        Rlgl.DisableDepthMask();
        Rlgl.DisableBackfaceCulling();
    }

    internal static void End()
    {
        Rlgl.DrawRenderBatchActive();
        Rlgl.EnableBackfaceCulling();
        Rlgl.EnableDepthMask();
        Raylib.EndBlendMode();
    }

    private static void Quad(Texture2D tex, float u0,float u1, Vector3 a,Vector3 b,Vector3 c,Vector3 d, Color tint)
    {
        void Vertex(Vector3 p,float u,float v)
        {
            Rlgl.TexCoord2f(u,v);
            Rlgl.Vertex3f(p.X,p.Y,p.Z);
        }
        Rlgl.SetTexture(tex.Id);
        Rlgl.Begin((int)DrawMode.Quads);
        Rlgl.Color4ub(tint.R,tint.G,tint.B,tint.A);
        Vertex(a,u0,0); Vertex(b,u1,0); Vertex(c,u1,1); Vertex(d,u0,1);
        Rlgl.End();
        Rlgl.SetTexture(0);
    }

    internal static void Puff(Vector3 p,Vector3 right,Vector3 up,float radius,float rotation,Color color)
    {
        var (s,c)=MathF.SinCos(rotation);
        Vector3 x=(right*c+up*s)*radius, y=(up*c-right*s)*radius;
        Quad(smoke,0,1,p-x-y,p+x-y,p+x+y,p-x+y,color);
    }

    internal static void Muzzle(Vector3 tip,Vector3 axis,Vector3 right,Vector3 up,float length,float angle,float strength)
    {
        Raylib.BeginShaderMode(viewShader);
        Begin(BlendMode.Additive);
        var radial=Vector3.Normalize(Vector3.Cross(axis,up));
        var normal=Vector3.Cross(axis,radial);
        var (s,c)=MathF.SinCos(angle);
        Vector3 end=tip+axis*length*(.72f+.28f*strength);
        var tint=new Color(255,255,255,Alpha(.92f*strength));
        void Jet(Vector3 span) => Quad(flash,0,.5f,tip-span,end-span,end+span,tip+span,tint);
        Jet((radial*c+normal*s)*length*.30f);
        Jet((normal*c-radial*s)*length*.26f);
        Vector3 x=right*length*.26f, y=up*length*.26f;
        Quad(flash,.5f,1,tip-x-y,tip+x-y,tip+x+y,tip-x+y,new Color(255,163,75,Alpha(.48f*strength)));
        End();
        Raylib.EndShaderMode();
    }

    internal static void Streak(Vector3 tail,Vector3 head,Vector3 eye,float width,float pixelAngle,Color color)
    {
        Vector3 side=Vector3.Cross(head-tail,eye-head);
        if (side.LengthSquared()<1e-15f) return;
        // Integrate the narrow luminous core over a pixel at this distance. A
        // subpixel quad alone can fall between all four MSAA sample positions.
        float pixel=Vector3.Distance(eye,head)*pixelAngle;
        float radius=MathF.Sqrt(width*width+pixel*pixel);
        color.A=(byte)(color.A*Math.Min(1,1.6f*width/radius));
        side=Vector3.Normalize(side)*radius;
        Quad(flash,.5f,1,tail-side*.55f,head-side,head+side,tail+side*.55f,color);
    }
}
