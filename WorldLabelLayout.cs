using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal static class WorldLabelLayout
{
    private static readonly List<Rectangle> occupied = new();

    public static void BeginFrame()
    {
        occupied.Clear();
        int w=Raylib.GetScreenWidth(), h=Raylib.GetScreenHeight();
        Reserve(new(w/2-14,h/2-14,28,28));        // reticle and hit confirmation
        Reserve(new(w-248,0,248,84));             // XP and objectives
        Reserve(new(w/2-365,h-165,730,165));      // interaction, speech and inventory
        Reserve(new(0,h-120,116,120));
    }

    public static void Reserve(Rectangle r) => occupied.Add(r);

    public static void ReserveProjectedBox(Camera3D camera, Vector3 center, Vector3 half)
    {
        Vector3 look=Vector3.Normalize(camera.Target-camera.Position);
        var points = new Vector3[8];
        var depth = new float[8];
        Vector2 min=new(float.PositiveInfinity), max=new(float.NegativeInfinity);
        void Project(Vector3 p)
        {
            Vector2 xy=Raylib.GetWorldToScreen(p,camera);
            min=Vector2.Min(min,xy);
            max=Vector2.Max(max,xy);
        }
        const float near=.01f;
        for(int i=0;i<8;i++)
        {
            points[i]=center+new Vector3((i&1)==0?-half.X:half.X,(i&2)==0?-half.Y:half.Y,(i&4)==0?-half.Z:half.Z);
            depth[i]=Vector3.Dot(points[i]-camera.Position,look);
            if(depth[i]>=near) Project(points[i]);
        }
        // Include intersections with the near plane when walking past the object.
        for(int i=0;i<8;i++)
            for(int bit=1;bit<=4;bit*=2)
            {
                int j=i^bit;
                if(i>=j || (depth[i]>=near)==(depth[j]>=near)) continue;
                Project(Vector3.Lerp(points[i],points[j],(near-depth[i])/(depth[j]-depth[i])));
            }
        if(min.X<=max.X) Reserve(new(min.X-4,min.Y-4,max.X-min.X+8,max.Y-min.Y+8));
    }

    public static bool Place(Vector2 anchor, float width, float height, out Rectangle result)
    {
        int screenW=Raylib.GetScreenWidth(), screenH=Raylib.GetScreenHeight();
        result=default;
        if(anchor.X<0 || anchor.X>screenW || anchor.Y<0 || anchor.Y>screenH || width>screenW-8) return false;
        float x=Math.Clamp(MathF.Round(anchor.X-width/2),4,screenW-width-4);
        float y=Math.Clamp(MathF.Round(anchor.Y-height),4,screenH-height-4);
        float best=float.PositiveInfinity;
        Rectangle winner=default;
        void Try(float left,float top)
        {
            Rectangle r=new(MathF.Round(left),MathF.Round(top),width,height);
            if(r.X<4 || r.Y<4 || r.X+width>screenW-4 || r.Y+height>screenH-4) return;
            float cost=(r.X-x)*(r.X-x)+1.3f*(r.Y-y)*(r.Y-y);
            if(cost>=best || occupied.Any(b=>Raylib.CheckCollisionRecs(b,r))) return;
            best=cost;
            winner=r;
        }
        Try(x,y);
        if(best>0)
        {
            var xs=new List<float>{x,4,screenW-width-4};
            var ys=new List<float>{y,4,screenH-height-4};
            foreach(var b in occupied)
            {
                xs.Add(b.X-width-4); xs.Add(b.X+b.Width+4);
                ys.Add(b.Y-height-4); ys.Add(b.Y+b.Height+4);
            }
            foreach(float left in xs) foreach(float top in ys) Try(left,top);
        }
        if(!float.IsFinite(best)) return false;
        result=winner;
        Reserve(winner);
        return true;
    }
}

