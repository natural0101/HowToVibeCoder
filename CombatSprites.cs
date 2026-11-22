using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal static class CombatSprites
{
    // Left half is the gas jet in bore coordinates. Right half is a radial bloom,
    // also used as the cross section of a tapered streak.
    private static Texture2D flash, smoke;
    private static Texture2D fxFire, fxExplosion, fxSmokePuff;
    private static bool fxLoaded;
    private static Shader viewShader;

    private static Texture2D TryLoadFxTexture(string fileName)
    {
        string p = Path.Combine(AppContext.BaseDirectory, "assets", "ui", fileName);
        if (!File.Exists(p))
            p = Path.Combine(Directory.GetCurrentDirectory(), "assets", "ui", fileName);
        if (!File.Exists(p)) return default;
        var tex = Raylib.LoadTexture(p);
        if (tex.Id != 0)
        {
            Raylib.SetTextureFilter(tex, TextureFilter.Bilinear);
            Raylib.SetTextureWrap(tex, TextureWrap.Clamp);
        }
        return tex;
    }

    internal static bool HasPaintedFx => fxLoaded && fxFire.Id != 0 && fxExplosion.Id != 0 && fxSmokePuff.Id != 0;

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
        fxFire = TryLoadFxTexture("fx_fire_flames.png");
        fxExplosion = TryLoadFxTexture("fx_explosion_burst.png");
        fxSmokePuff = TryLoadFxTexture("fx_smoke_puff.png");
        fxLoaded = fxFire.Id != 0 && fxExplosion.Id != 0 && fxSmokePuff.Id != 0;
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
        if (fxFire.Id != 0) Raylib.UnloadTexture(fxFire);
        if (fxExplosion.Id != 0) Raylib.UnloadTexture(fxExplosion);
        if (fxSmokePuff.Id != 0) Raylib.UnloadTexture(fxSmokePuff);
        fxFire = default; fxExplosion = default; fxSmokePuff = default; fxLoaded = false;
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

    internal static void SoftSmoke(Vector3 p, Vector3 right, Vector3 up, float radius, float rotation, Color color)
    {
        if (smoke.Id == 0 || color.A == 0) return;
        var (s, c) = MathF.SinCos(rotation);
        Vector3 x = (right * c + up * s) * radius;
        Vector3 y = (up * c - right * s) * radius;
        Quad(smoke, 0, 1, p - x - y, p + x - y, p + x + y, p - x + y, color);
    }

    internal static void Puff(Vector3 p,Vector3 right,Vector3 up,float radius,float rotation,Color color)
    {
        SoftSmoke(p, right, up, radius, rotation, color);
    }

    internal static void Muzzle(Vector3 tip,Vector3 axis,Vector3 right,Vector3 up,float length,float angle,float strength)
    {
        Raylib.BeginShaderMode(viewShader);
        Begin(BlendMode.Additive);
        var radial=Vector3.Normalize(Vector3.Cross(axis,up));
        var normal=Vector3.Cross(axis,radial);
        var (s,c)=MathF.SinCos(angle);
        Vector3 end=tip+axis*length*(.82f+.38f*strength);
        var tint=new Color(255,252,238,Alpha(Math.Min(1f,1.08f*strength)));
        void Jet(Vector3 span) => Quad(flash,0,.5f,tip-span,end-span,end+span,tip+span,tint);
        Jet((radial*c+normal*s)*length*.36f);
        Jet((normal*c-radial*s)*length*.32f);
        // Third diagonal gas blade for richer 3D volumetric star flare
        var (s2,c2)=MathF.SinCos(angle+MathF.PI*.25f);
        Jet((radial*c2+normal*s2)*length*.22f);
        Vector3 x=right*length*.36f, y=up*length*.36f;
        Quad(flash,.5f,1,tip-x-y,tip+x-y,tip+x+y,tip-x+y,new Color(255,172,72,Alpha(.64f*strength)));
        Vector3 cx=right*length*.16f, cy=up*length*.16f;
        Quad(flash,.5f,1,tip-cx-cy,tip+cx-cy,tip+cx+cy,tip-cx+cy,new Color(255,250,224,Alpha(.88f*strength)));
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

    private static void QuadUV(Texture2D tex, float u0, float v0, float u1, float v1, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color tint)
    {
        if (tex.Id == 0) return;
        Rlgl.SetTexture(tex.Id);
        Rlgl.Begin((int)DrawMode.Quads);
        Rlgl.Color4ub(tint.R, tint.G, tint.B, tint.A);
        Rlgl.TexCoord2f(u0, v1); Rlgl.Vertex3f(a.X, a.Y, a.Z);
        Rlgl.TexCoord2f(u1, v1); Rlgl.Vertex3f(b.X, b.Y, b.Z);
        Rlgl.TexCoord2f(u1, v0); Rlgl.Vertex3f(c.X, c.Y, c.Z);
        Rlgl.TexCoord2f(u0, v0); Rlgl.Vertex3f(d.X, d.Y, d.Z);
        Rlgl.End();
        Rlgl.SetTexture(0);
    }

    private static void Sheet2x2(Texture2D tex, int frame, Vector3 center, Vector3 right, Vector3 up, float halfW, float halfH, float rotation, Color tint, bool flipX)
    {
        int idx = ((frame % 4) + 4) % 4;
        int col = idx & 1;
        int row = idx >> 1;
        const float pad = 0.008f;
        float u0 = col * 0.5f + pad;
        float u1 = (col + 1) * 0.5f - pad;
        float v0 = row * 0.5f + pad;
        float v1 = (row + 1) * 0.5f - pad;
        if (flipX) (u0, u1) = (u1, u0);

        Vector3 x, y;
        if (MathF.Abs(rotation) > 1e-4f)
        {
            var (s, c) = MathF.SinCos(rotation);
            x = (right * c + up * s) * halfW;
            y = (up * c - right * s) * halfH;
        }
        else
        {
            x = right * halfW;
            y = up * halfH;
        }
        // Bottom-left (a), bottom-right (b), top-right (c), top-left (d)
        QuadUV(tex, u0, v0, u1, v1, center - x - y, center + x - y, center + x + y, center - x + y, tint);
    }

    internal static void FireFrame(int frame, Vector3 center, Vector3 right, Vector3 up, float halfW, float halfH, Color tint, bool flipX = false, float rotation = 0f)
        => Sheet2x2(fxFire, frame, center, right, up, halfW, halfH, rotation, tint, flipX);

    internal static void ExplosionFrame(int frame, Vector3 center, Vector3 right, Vector3 up, float radius, float rotation, Color tint, bool flipX = false)
        => Sheet2x2(fxExplosion, frame, center, right, up, radius, radius, rotation, tint, flipX);

    internal static void SmokeFrame(int frame, Vector3 center, Vector3 right, Vector3 up, float halfW, float halfH, Color tint, bool flipX = false, float rotation = 0f)
        => Sheet2x2(fxSmokePuff, frame, center, right, up, halfW, halfH, rotation, tint, flipX);

    /// <summary>Silky radially-feathered horizontal floor decal/glow using the smooth Gaussian bloom (zero hard cylinder rim!).</summary>
    internal static void FloorGlow(Vector3 center, float radius, Color tint)
    {
        if (flash.Id == 0 || tint.A == 0) return;
        Vector3 a = center + new Vector3(-radius, 0f, -radius);
        Vector3 b = center + new Vector3(+radius, 0f, -radius);
        Vector3 c = center + new Vector3(+radius, 0f, +radius);
        Vector3 d = center + new Vector3(-radius, 0f, +radius);
        QuadUV(flash, 0.502f, 0.004f, 0.996f, 0.996f, a, b, c, d, tint);
    }

    /// <summary>Silky camera-facing radial Gaussian light bloom (ideal for delicate candle halos & mystical crystal aura with zero spark noise).</summary>
    internal static void SoftHalo(Vector3 center, Vector3 right, Vector3 up, float halfW, float halfH, Color tint)
    {
        if (flash.Id == 0 || tint.A == 0) return;
        Vector3 x = right * halfW;
        Vector3 y = up * halfH;
        QuadUV(flash, 0.502f, 0.004f, 0.996f, 0.996f, center - x - y, center + x - y, center + x + y, center - x + y, tint);
    }

    /// <summary>Continuous camera-facing tapered ribbon segment (samples Gaussian cross-section across width so segments join seamlessly with zero beads).</summary>
    internal static void RibbonSegment(Vector3 prev, Vector3 cur, Vector3 eye, float prevHalfW, float curHalfW, Color prevCol, Color curCol)
    {
        if (flash.Id == 0) return;
        Vector3 tangent = cur - prev;
        Vector3 sidePrev = Vector3.Cross(tangent, eye - prev);
        Vector3 sideCur = Vector3.Cross(tangent, eye - cur);
        if (sidePrev.LengthSquared() < 1e-12f || sideCur.LengthSquared() < 1e-12f) return;
        sidePrev = Vector3.Normalize(sidePrev) * prevHalfW;
        sideCur = Vector3.Normalize(sideCur) * curHalfW;

        // Sample the radial bloom at its horizontal peak (u = 0.75) across v = 0..1 for a silky feathered width profile
        const float uMid = 0.75f;
        Rlgl.SetTexture(flash.Id);
        Rlgl.Begin((int)DrawMode.Quads);
        Rlgl.Color4ub(prevCol.R, prevCol.G, prevCol.B, prevCol.A);
        Rlgl.TexCoord2f(uMid, 0.02f); Rlgl.Vertex3f(prev.X - sidePrev.X, prev.Y - sidePrev.Y, prev.Z - sidePrev.Z);
        Rlgl.Color4ub(curCol.R, curCol.G, curCol.B, curCol.A);
        Rlgl.TexCoord2f(uMid, 0.02f); Rlgl.Vertex3f(cur.X - sideCur.X, cur.Y - sideCur.Y, cur.Z - sideCur.Z);
        Rlgl.Color4ub(curCol.R, curCol.G, curCol.B, curCol.A);
        Rlgl.TexCoord2f(uMid, 0.98f); Rlgl.Vertex3f(cur.X + sideCur.X, cur.Y + sideCur.Y, cur.Z + sideCur.Z);
        Rlgl.Color4ub(prevCol.R, prevCol.G, prevCol.B, prevCol.A);
        Rlgl.TexCoord2f(uMid, 0.98f); Rlgl.Vertex3f(prev.X + sidePrev.X, prev.Y + sidePrev.Y, prev.Z + sidePrev.Z);
        Rlgl.End();
        Rlgl.SetTexture(0);
    }

    private static uint HashU32(uint h)
    {
        h ^= h >> 16;
        h = unchecked(h * 0x7feb352du);
        h ^= h >> 15;
        h = unchecked(h * 0x846ca68bu);
        return h ^ (h >> 16);
    }

    private static float Hash01(uint h) => (HashU32(h) & 0x00FFFFFFu) / 16777215f;
    private static float HashSigned(uint h) => Hash01(h) * 2f - 1f;

    /// <summary>
    /// AAA multi-pass 3D forked electric lightning bolt. Must be called inside CombatSprites.Begin(BlendMode.Additive) .. End().
    /// Renders wide electric outer glow + intense mid plasma jacket + razor-white incandescent core + forked sub-branches + endpoint contact flashes.
    /// </summary>
    internal static void DrawLightningBolt(
        Vector3 start,
        Vector3 end,
        Vector3 eye,
        float time,
        int seed,
        float widthScale,
        Color outerGlow,
        Color midPlasma,
        int branches = 2)
    {
        if (flash.Id == 0 || widthScale <= 0.001f) return;
        Vector3 delta = end - start;
        float len = delta.Length();
        if (len < 0.025f) return;

        Vector3 dir = delta / len;
        Vector3 refUp = MathF.Abs(dir.Y) < 0.92f ? Vector3.UnitY : Vector3.UnitX;
        Vector3 uAxis = Vector3.Normalize(Vector3.Cross(dir, refUp));
        Vector3 vAxis = Vector3.Cross(uAxis, dir);

        // High-frequency 28Hz stepped flicker so electricity snaps & crackles crisply (never slow swimming worms!)
        uint step = unchecked((uint)(int)MathF.Floor(time * 28f));
        uint baseHash = unchecked(step * 73856093u ^ (uint)seed * 19349663u ^ 0x9e3779b9u);

        int segs = Math.Clamp((int)(9 + len * 2.1f), 10, 14);
        Span<Vector3> pts = stackalloc Vector3[16];
        pts[0] = start;
        pts[segs] = end;

        float jitterAmp = Math.Clamp(len * 0.135f, 0.042f, 0.42f);
        float bowU = HashSigned(baseHash ^ 0x1111u) * jitterAmp * 0.52f;
        float bowV = HashSigned(baseHash ^ 0x2222u) * jitterAmp * 0.52f;
        float prevKickU = 0f, prevKickV = 0f;

        for (int i = 1; i < segs; i++)
        {
            float t = i / (float)segs;
            float env = MathF.Sin(t * MathF.PI);
            uint nh = unchecked(baseHash ^ (uint)i * 83492791u);
            float kickU = HashSigned(nh ^ 0x3a5cu) * jitterAmp * 0.85f;
            float kickV = HashSigned(nh ^ 0x7c9eu) * jitterAmp * 0.85f;
            // Slight smoothing blend with previous node keeps sharp jagged knees without extreme zig-zag foldbacks
            float offU = (bowU + kickU * 0.78f + prevKickU * 0.22f) * env;
            float offV = (bowV + kickV * 0.78f + prevKickV * 0.22f) * env;
            prevKickU = kickU;
            prevKickV = kickV;
            pts[i] = Vector3.Lerp(start, end, t) + uAxis * offU + vAxis * offV;
        }

        float intensity = Math.Clamp(midPlasma.A / 255f, 0f, 1f);
        Color outerCol = new(outerGlow.R, outerGlow.G, outerGlow.B, (byte)Math.Clamp((int)(88f * (outerGlow.A / 255f)), 0, 255));
        Color midCol = new(midPlasma.R, midPlasma.G, midPlasma.B, (byte)Math.Clamp((int)(205f * intensity), 0, 255));
        Color coreCol = new(248, 253, 255, (int)Math.Clamp((int)(255f * MathF.Sqrt(intensity)), 0, 255));

        void DrawPolyline(ReadOnlySpan<Vector3> chain, int count, float wScale, bool taper)
        {
            float wOuter = 0.095f * wScale;
            float wMid = 0.036f * wScale;
            float wCore = 0.012f * wScale;
            for (int k = 0; k < count - 1; k++)
            {
                float f0 = taper ? 1f - (k / (float)(count - 1)) * 0.82f : 1f;
                float f1 = taper ? 1f - ((k + 1) / (float)(count - 1)) * 0.82f : 1f;
                float a0 = taper ? 1f - (k / (float)(count - 1)) * 0.85f : 1f;
                float a1 = taper ? 1f - ((k + 1) / (float)(count - 1)) * 0.85f : 1f;

                Color o0 = new(outerCol.R, outerCol.G, outerCol.B, (byte)(outerCol.A * a0));
                Color o1 = new(outerCol.R, outerCol.G, outerCol.B, (byte)(outerCol.A * a1));
                RibbonSegment(chain[k], chain[k + 1], eye, wOuter * f0, wOuter * f1, o0, o1);

                Color m0 = new(midCol.R, midCol.G, midCol.B, (byte)(midCol.A * a0));
                Color m1 = new(midCol.R, midCol.G, midCol.B, (byte)(midCol.A * a1));
                RibbonSegment(chain[k], chain[k + 1], eye, wMid * f0, wMid * f1, m0, m1);

                Color c0 = new(coreCol.R, coreCol.G, coreCol.B, (byte)(coreCol.A * a0));
                Color c1 = new(coreCol.R, coreCol.G, coreCol.B, (byte)(coreCol.A * a1));
                RibbonSegment(chain[k], chain[k + 1], eye, wCore * f0, wCore * f1, c0, c1);
            }
        }

        // Main jagged bolt (3-pass volumetric glow + plasma + incandescent core)
        DrawPolyline(pts, segs + 1, widthScale, taper: false);

        // Forked sub-branches shooting outward at 30°–55° from interior midpoints with tapered width
        int branchCount = Math.Clamp(branches, 0, 5);
        if (branchCount > 0 && segs >= 6)
        {
            Span<Vector3> forkPts = stackalloc Vector3[6];
            for (int b = 0; b < branchCount; b++)
            {
                uint bh = unchecked(baseHash ^ (uint)(b + 1) * 0x517cc1b7u);
                int rootIdx = 2 + (int)(Hash01(bh ^ 0x13u) * (segs - 4));
                Vector3 root = pts[rootIdx];

                float azim = Hash01(bh ^ 0x29u) * MathF.Tau;
                float spreadAngle = (30f + Hash01(bh ^ 0x47u) * 25f) * (MathF.PI / 180f); // 30°..55°
                Vector3 radialOut = uAxis * MathF.Cos(azim) + vAxis * MathF.Sin(azim);
                Vector3 forkDir = Vector3.Normalize(dir * MathF.Cos(spreadAngle) + radialOut * MathF.Sin(spreadAngle));
                Vector3 fU = Vector3.Normalize(Vector3.Cross(forkDir, MathF.Abs(forkDir.Y) < 0.92f ? Vector3.UnitY : Vector3.UnitX));
                Vector3 fV = Vector3.Cross(fU, forkDir);

                float forkLen = len * (0.24f + Hash01(bh ^ 0x61u) * 0.24f);
                const int fSegs = 5;
                forkPts[0] = root;
                float fJit = Math.Clamp(forkLen * 0.16f, 0.025f, 0.22f);
                for (int fi = 1; fi <= fSegs; fi++)
                {
                    float ft = fi / (float)fSegs;
                    uint fnh = unchecked(bh ^ (uint)fi * 2654435761u);
                    float ju = HashSigned(fnh ^ 0x55u) * fJit * ft;
                    float jv = HashSigned(fnh ^ 0xaau) * fJit * ft;
                    forkPts[fi] = root + forkDir * (forkLen * ft) + fU * ju + fV * jv;
                }
                DrawPolyline(forkPts, fSegs + 1, widthScale * 0.56f, taper: true);
            }
        }

        // Bright electric contact flashes at start & end
        void ContactFlash(Vector3 p, float radiusScale)
        {
            Vector3 toEye = eye - p;
            if (toEye.LengthSquared() < 1e-6f) return;
            Vector3 fwd = Vector3.Normalize(toEye);
            Vector3 r = Vector3.Normalize(Vector3.Cross(MathF.Abs(fwd.Y) < 0.95f ? Vector3.UnitY : Vector3.UnitX, fwd));
            Vector3 u = Vector3.Cross(fwd, r);
            float haloR = 0.22f * widthScale * radiusScale;
            SoftHalo(p, r, u, haloR, haloR, new Color(midPlasma.R, midPlasma.G, midPlasma.B, (byte)Math.Clamp((int)(165f * intensity), 0, 255)));
            float coreR = 0.095f * widthScale * radiusScale;
            SoftHalo(p, r, u, coreR, coreR, coreCol);
        }
        ContactFlash(start, 0.92f);
        ContactFlash(end, 1.12f);
    }

    /// <summary>
    /// Luminous additive 3D energy/shockwave ring using smooth camera-facing RibbonSegments (zero 1px aliased DrawCircle3D!).
    /// Must be called inside CombatSprites.Begin(BlendMode.Additive) .. End().
    /// </summary>
    internal static void DrawEnergyRing(
        Vector3 center,
        float radius,
        float thickness,
        Vector3 eye,
        Color outerCol,
        Color coreCol,
        int segments = 32,
        Vector3? normal = null)
    {
        if (flash.Id == 0 || radius <= 0.01f || thickness <= 0.0005f) return;
        int segs = Math.Max(12, segments);
        Vector3 n = normal.HasValue && normal.Value.LengthSquared() > 1e-6f ? Vector3.Normalize(normal.Value) : Vector3.UnitY;
        Vector3 refVec = MathF.Abs(n.Y) < 0.95f ? Vector3.UnitY : Vector3.UnitX;
        Vector3 t1 = Vector3.Normalize(Vector3.Cross(n, refVec));
        Vector3 t2 = Vector3.Cross(n, t1);

        float coreW = thickness * 0.36f;
        Vector3 prev = center + t1 * radius;
        for (int i = 1; i <= segs; i++)
        {
            float a = i * (MathF.Tau / segs);
            Vector3 cur = center + (t1 * MathF.Cos(a) + t2 * MathF.Sin(a)) * radius;
            if (outerCol.A > 0)
                RibbonSegment(prev, cur, eye, thickness, thickness, outerCol, outerCol);
            if (coreCol.A > 0)
                RibbonSegment(prev, cur, eye, coreW, coreW, coreCol, coreCol);
            prev = cur;
        }
    }
}
