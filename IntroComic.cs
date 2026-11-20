using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal sealed class IntroComic : IDisposable
{
    private const float Entrance = .34f, Turn = .30f;
    private static readonly Rectangle Page = new(48, 10, 1184, 666);
    private static readonly Color Paper = new(244, 235, 214, 255), Ink = new(20, 18, 25, 255);
    private static readonly Color Ground = new(18, 16, 26, 255), CaptionPaper = new(247, 231, 184, 255);
    private static readonly (string Ru, string En)[] Captions =
    [
        ("2026. Башня «Энтропик». Ты заменил весь IT-отдел\nна вайбкодеров и ИИ-агента.\nПервая неделя: «Блин, реально круто работает!»",
         "2026. Entropic Tower. You replaced the whole IT team\nwith vibe coders and an AI agent.\nWeek one: \"Hey, this actually works great!\""),
        ("Потом вылез первый баг. Агент поклялся:\n«Такого больше не повторится!»\nИ через секунду: БАГ! БАГ! БАГ! Прод горит!",
         "Then the first bug hit. The agent swore:\n\"This will never happen again!\"\nOne second later: BUG! BUG! BUG! Prod is on fire!"),
        ("Пока офис орал, HR Вероника раскинула Таро:\n«Я же говорила — Меркурий в ретрограде.\nОтбиваем убытки вручную.»",
         "While the office panicked, HR Veronika dealt Tarot:\n\"Told you — Mercury is in retrograde.\nTime to recover the losses by hand.\""),
        ("В Северном лифте заперли самого ИИ-Агента.\nОн всё ещё клянётся «Всё исправлено!» — и стирает базу.\nДостаём аргументы 90-х.",
         "They locked the rogue AI Agent in the North Elevator.\nStill swearing \"Everything is fixed!\" while wiping the DB.\nTime for nineties methods."),
        ("Агент полез «отрефакторить» бэкенд\nПетровича 98-го на Джабе — и породил\nДжаба-жаб. Зато легаси не падает.",
         "The agent tried to \"refactor\" Petrovich's\n'98 Jabba backend — spawning Jabba-frogs.\nAt least the legacy still runs."),
        ("Вайбкодеры не верят, что всё сгорело —\nагент же написал «All tests passed!».\nНо на халявный Wi-Fi слетаются мгновенно.",
         "Vibe coders don't believe prod burned down —\nthe agent printed \"All tests passed!\".\nFree Wi-Fi lures them straight in."),
        ("Агент «оптимизировал» даже HR — у Вероники вместо головы\nигровой автомат. Тащи редких кадров, крути пушки и снимай карантин.",
         "The agent even \"optimized\" HR — Veronika's head is now a slot machine.\nBring rare catches, spin weapons, and lift the lockdown."),
        ("«Простите, больше не повторится!» — пищит из колонок.\nСчета на нуле. Первый этаж «Энтропик».\nЧиним этот цирк по-старому!",
         "\"So sorry, it won't happen again!\" — chirps the intercom.\nBank at zero. Floor 1 of Entropic Corp.\nTime to fix this circus the old way!")
    ];

    private readonly Texture2D[] art = new Texture2D[8];
    private enum Cue { Slam, Boom, Snap, Zap, Jackpot }
    private sealed record Burst(float Time, float X, float Y, float Angle, string Ru, string En, int Rgb, Cue Sound);
    private static readonly Burst[][] Bursts =
    [
        [new(.40f,.78f,.22f,8,"МАГИЯ!","MAGIC!",0xF7C948,Cue.Slam)],
        [new(.30f,.82f,.22f,-9,"БАГ!","BUG!",0xFF4B3E,Cue.Boom), new(.86f,.68f,.48f,7,"БАГ!!","BUG!!",0xFFD23F,Cue.Boom), new(1.38f,.79f,.78f,-8,"БАГ!!!","BUG!!!",0x7CF252,Cue.Boom)],
        [new(.40f,.20f,.72f,-7,"ТАРО!","TAROT!",0xB56BFF,Cue.Zap)],
        [new(.36f,.74f,.74f,-9,"ЩЁЛК!","SNAP!",0xFFD23F,Cue.Snap), new(1.25f,.36f,.22f,7,"ИСПРАВИЛ!","FIXED!",0xE83B3B,Cue.Boom)],
        [new(.40f,.23f,.76f,-8,"КВАК!","RIBBIT!",0x7CF252,Cue.Slam)],
        [new(.38f,.79f,.23f,9,"ХАЛЯВА!","FREE WI-FI!",0xFFC83D,Cue.Slam)],
        [new(.34f,.22f,.36f,-8,"ДЖЕКПОТ!","JACKPOT!",0xFFD747,Cue.Jackpot)],
        [new(.42f,.78f,.24f,8,"ПОГНАЛИ!","SHOWTIME!",0xFF4F42,Cue.Boom)]
    ];
    private readonly float[] drift = new float[8];
    private float shakeAge, shakePower;
    private static bool Splash(int i) => i is 0 or 7;
    private static bool Strip(int i) => i is 3 or 6;
    private static int First(int i) => i switch { 0 => 0, <= 3 => 1, <= 6 => 4, _ => 7 };
    private sealed record Caption(Font Font, int Size, string[] Lines, int Width, int Height);
    private readonly Caption[,] lettering = new Caption[2, 8];
    private int panel;
    private float age, turnAge, duration, silence;
    private bool turning;
    internal bool Active { get; private set; }

    internal void Open()
    {
        for (int i = 0; i < art.Length; i++)
        {
            if (art[i].Id != 0) continue;
            art[i] = Raylib.LoadTexture(Path.Combine(AppContext.BaseDirectory, "assets", "ui", $"comic_p{i+1}.png"));
            if (art[i].Width != 960 || art[i].Height != 540) throw new InvalidDataException($"Comic panel {i+1}");
            Raylib.GenTextureMipmaps(ref art[i]);
            Raylib.SetTextureFilter(art[i], TextureFilter.Trilinear);
            int limit = Splash(i) ? 620 : i switch { 3 => 500, 4 => 295, 6 => 520, _ => 385 };
            int size = Splash(i) ? 20 : 15;
            lettering[0,i] = Typeset(Captions[i].Ru, limit, size);
            lettering[1,i] = Typeset(Captions[i].En, limit, size);
        }
        Active = true;
        turning = false;
        Array.Clear(drift);
        Reveal(0);
    }

    private static Caption Typeset(string text, int limit, int initialSize)
    {
        const int padding = 10;
        int room = limit - 2 * padding;
        var paragraphs = text.Split('\n');
        for (int size = initialSize; size >= 12; size--)
        {
            var font = GameFonts.Get(Typeface.InterfaceBold, size);
            float Width(string line) => Raylib.MeasureTextEx(font, line, size, 0).X;
            var lines = new List<string>();
            foreach (string paragraph in paragraphs)
            {
                string line = "";
                foreach (string word in paragraph.Split(' '))
                {
                    string next = line.Length == 0 ? word : line + " " + word;
                    if (line.Length > 0 && Width(next) > room)
                    {
                        lines.Add(line);
                        line = word;
                    }
                    else line = next;
                }
                lines.Add(line);
            }
            int width = (int)Math.Ceiling(lines.Max(Width)) + 2 * padding;
            if (width <= limit) return new(font, size, lines.ToArray(), width, 16 + lines.Count * (size + 2));
        }
        throw new InvalidDataException($"Comic caption exceeds {limit}px: {text}");
    }

    private void Reveal(int index)
    {
        shakePower = Strip(index) ? 8 : 5;
        shakeAge = 0;
        panel = index;
        age = silence = 0;
        AudioMusicSystem.PlayComicPanelSlam();
        AudioMusicSystem.PlayComicNarration(index + 1);
        duration = AudioMusicSystem.HasComicVoice ? AudioMusicSystem.GetComicNarrationDuration(index + 1) : 5;
    }

    internal void Update(float dt, bool advance, bool skip)
    {
        if (skip) { Close(); return; }
        if (turning)
        {
            float previous = turnAge;
            turnAge += dt;
            if (previous < Turn / 2 && turnAge >= Turn / 2) Reveal(panel + 1);
            if (turnAge >= Turn) turning = false;
            if (turnAge < Turn / 2) return;
            dt = Math.Min(dt, turnAge - Turn / 2);
            advance = false;
        }
        float before = age;
        age += dt;
        shakeAge += dt;
        foreach (var burst in Bursts[panel])
        {
            if (before >= burst.Time || age < burst.Time) continue;
            switch (burst.Sound)
            {
                case Cue.Slam: AudioMusicSystem.PlayComicPanelSlam(); break;
                case Cue.Boom: AudioMusicSystem.PlayComicBoom(); break;
                case Cue.Snap: AudioMusicSystem.PlayComicWhipSnap(); break;
                case Cue.Zap: AudioMusicSystem.PlayComicZap(); break;
                case Cue.Jackpot: AudioMusicSystem.PlaySfx("slot_win_jackpot"); break;
            }
            if (burst.Sound is Cue.Snap or Cue.Boom or Cue.Jackpot)
            {
                shakeAge = age - burst.Time;
                shakePower = burst.Sound == Cue.Snap ? 8 : 6;
            }
        }
        drift[panel] = Math.Clamp(age / Math.Max(1,duration), 0, 1);
        if (AudioMusicSystem.IsComicNarrationPlaying()) silence = 0;
        else silence += dt;
        if (!advance && (AudioMusicSystem.HasComicVoice ? silence < .55f : age < 5f)) return;
        if (panel == 7) Close();
        else if (panel is 0 or 3 or 6)
        {
            AudioMusicSystem.StopComicNarration();
            AudioMusicSystem.PlayComicPageTurn();
            turning = true;
            turnAge = 0;
        }
        else Reveal(panel + 1);
    }

    private void Close()
    {
        AudioMusicSystem.StopComicNarration();
        Active = false;
    }

    private static float Smooth(float t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static readonly Color Gold = new(226,174,73,255);
    private static readonly Rectangle Spread = new(64,24,1152,638);

    private static Rectangle Slot(int i)
    {
        if (Splash(i)) return Spread;
        float w = Spread.Width * .488f, h = Spread.Height * .485f;
        return (i - First(i)) switch
        {
            0 => new(Spread.X, Spread.Y, w, h),
            1 => new(Spread.X + Spread.Width - w, Spread.Y, w, h),
            _ => new(Spread.X, Spread.Y + Spread.Height - h, Spread.Width, h)
        };
    }

    private static Rectangle Expand(Rectangle r, float n) => new(r.X-n,r.Y-n,r.Width+2*n,r.Height+2*n);
    private static Vector2 At(Rectangle r, float x, float y) => new(r.X+x*r.Width,r.Y+y*r.Height);
    private static Color Rgb(int rgb) => new((rgb>>16)&255,(rgb>>8)&255,rgb&255,255);
    private static Vector2 Unit(float a) => new(MathF.Cos(a),MathF.Sin(a));

    internal void Draw()
    {
        float scale = UiViewport.Scale;
        Vector2 offset = new((Raylib.GetScreenWidth()-1280*scale)/2,(Raylib.GetScreenHeight()-720*scale)/2);
        float kick = shakePower * Math.Max(0,1-shakeAge/.22f);
        Vector2 camera = offset + scale*kick*new Vector2(MathF.Cos(shakeAge*113),MathF.Sin(shakeAge*149));
        Raylib.ClearBackground(Ground);
        Raylib.BeginMode2D(new Camera2D { Offset = camera, Zoom = scale });
        Raylib.DrawRectangleRec(new(Page.X+8,Page.Y+9,Page.Width,Page.Height),Ink);
        Raylib.DrawRectangleRec(Page,Paper);
        Raylib.DrawRectangleLinesEx(Page,5,Ink);
        for (int i=First(panel); i<=panel; i++) DrawPanel(i,camera,scale);
        if (panel == 6 && age >= Bursts[6][0].Time) DrawConfetti(age-Bursts[6][0].Time);
        Raylib.EndMode2D();

        Raylib.BeginMode2D(new Camera2D { Offset = offset, Zoom = scale });
        for (int i=0;i<8;i++)
            Raylib.DrawRectangle(49+i*14,696,10,5,i==panel ? Gold : i<panel ? Paper : new Color(83,78,84,255));
        string counter = Loc.IsEn ? $"PANEL {panel+1} / 8" : $"КАДР {panel+1} / 8";
        string hint = Loc.T("[ПРОБЕЛ / ЛКМ] ДАЛЕЕ   [ESC] ПРОПУСТИТЬ","[SPACE / CLICK] NEXT   [ESC] SKIP");
        var face = GameFonts.Get(Typeface.InterfaceBold,15);
        GameFonts.Draw(face,counter,new(172,689),15,Paper,false);
        GameFonts.Draw(face,hint,new(Page.X+Page.Width-GameFonts.Measure(face,hint,15).X,689),15,Paper,false);
        if (turning)
        {
            float x = -1840 + 3340 * Smooth(turnAge/Turn);
            void Stripe(float dx,float w,Color color)
            {
                Vector2 a=new(x+dx+260,0), b=new(x+dx,720);
                Raylib.DrawTriangle(a,b,b+new Vector2(w,0),color);
                Raylib.DrawTriangle(a,b+new Vector2(w,0),a+new Vector2(w,0),color);
            }
            Stripe(0,1590,Ink);
            Stripe(22,26,Paper);
            Stripe(67,8,Gold);
            Stripe(1480,62,Paper);
            Stripe(1610,12,Ink);
        }
        Raylib.EndMode2D();
    }

    private static void Clip(Rectangle r, Vector2 offset, float scale)
    {
        int x=(int)MathF.Floor(offset.X+r.X*scale), y=(int)MathF.Floor(offset.Y+r.Y*scale);
        Raylib.BeginScissorMode(x,y,(int)MathF.Ceiling(offset.X+(r.X+r.Width)*scale)-x,
            (int)MathF.Ceiling(offset.Y+(r.Y+r.Height)*scale)-y);
    }

    private void DrawPanel(int i, Vector2 offset, float scale)
    {
        Rectangle slot=Slot(i), r=slot;
        bool active=i==panel;
        float t=active ? Math.Clamp(age/Entrance,0,1) : 1;
        float spring=(1-t)*(1-t)*MathF.Cos(6*t);
        float zoom=1+.12f*spring;
        r.Width*=zoom;
        r.Height*=zoom;
        r.X+=(slot.Width-r.Width)/2 + (Strip(i) ? 0 : i-First(i)==1 ? 74 : -74)*spring;
        r.Y+=(slot.Height-r.Height)/2 + (Strip(i) ? 62 : -12)*spring;

        Clip(Expand(slot,6),offset,scale);
        Raylib.DrawRectangleRec(new(r.X+4,r.Y+5,r.Width,r.Height),Ink);
        if (active) Raylib.DrawRectangleRec(Expand(r,2.5f),Gold);
        Raylib.DrawRectangleRec(r,Ink);
        Raylib.EndScissorMode();
        Rectangle inside=Expand(r,-4);
        Clip(Raylib.GetCollisionRec(Expand(slot,6),inside),offset,scale);
        float progress=drift[i];
        float d=Smooth(progress);
        float push=1+.075f*d+(i==7 ? .40f*progress*progress*progress : 0);
        float fit=Math.Max(inside.Width/art[i].Width,inside.Height/art[i].Height)*push;
        float w=inside.Width/fit,h=inside.Height/fit;
        // The subjects sit above the bitmap midpoint in both panoramic strips.
        float centerY = i switch { 3 => 222, 6 => 201, _ => art[i].Height / 2f };
        Rectangle src=new((art[i].Width-w)*(.5f+(i%2==0 ? .20f : -.20f)*d),
            Math.Clamp(centerY-h/2+(i==7 ? (art[i].Height-h)*.18f*d : 0),0,Math.Max(0,art[i].Height-h)),w,h);
        Raylib.DrawTexturePro(art[i],src,inside,Vector2.Zero,0,Color.White);
        if (active)
        {
            Vector2 Feature(float x,float y) => At(inside,(x*art[i].Width-src.X)/w,(y*art[i].Height-src.Y)/h);
            if (i is 1 or 4) DrawSparks(i==1 ? Feature(.80f,.47f) : Feature(.47f,.66f),age);
            if (i==3)
            {
                Vector2 eye=Feature(.35f,.365f);
                Raylib.DrawCircleGradient((int)eye.X,(int)eye.Y,29,new Color(255,40,22,(int)(54+36*MathF.Sin(age*5))),Color.Blank);
            }
            if (i==5)
            {
                Vector2 router=Feature(.60f,.68f);
                for (int j=0;j<3;j++)
                {
                    float phase=(age*.70f+j/3f)%1;
                    float radius=12+52*phase;
                    Raylib.DrawRing(router,radius,radius+2,221,319,30,new Color(255,202,58,(int)(210*(1-phase))));
                }
            }
            if (i==7 && age<.90f)
            {
                float leaf=inside.Width*.5f*(1-Smooth(age/.90f));
                foreach (bool left in new[]{true,false})
                {
                    float x=left ? inside.X : inside.X+inside.Width-leaf;
                    Rectangle door=new(x,inside.Y,leaf,inside.Height);
                    Rectangle metal=new(left ? 0 : art[i].Width-1,0,1,art[i].Height);
                    Raylib.DrawTexturePro(art[i],metal,door,Vector2.Zero,0,Color.White);
                    Raylib.DrawRectangleLinesEx(door,3,Ink);
                }
            }
            if (age<.22f)
            {
                float fade=1-age/.22f;
                for (int j=0;j<24;j++)
                {
                    float a=j*MathF.Tau/24;
                    Vector2 u=Unit(a);
                    Vector2 P(float k) => At(inside,.5f+u.X*k,.5f+u.Y*k);
                    Raylib.DrawLineEx(P(.76f),P(.50f-.10f*fade),2+2*fade,Ink);
                }
                Raylib.DrawRectangleRec(inside,new Color(255,255,255,(int)(77*fade)));
            }
        }
        DrawCaption(i,inside);
        foreach (var burst in Bursts[i])
        {
            float elapsed=active ? age-burst.Time : 1;
            if (elapsed>0) DrawBurst(inside,burst,elapsed,Splash(i),active);
        }
        if (!active) Raylib.DrawRectangleRec(inside,new Color(16,14,22,66));
        Raylib.EndScissorMode();
    }

    private void DrawCaption(int i,Rectangle r)
    {
        var c=lettering[Loc.IsEn ? 1 : 0,i];
        Rectangle box=new(r.X+10,Strip(i) ? r.Y+r.Height-10-c.Height : r.Y+10,c.Width,c.Height);
        Raylib.DrawRectangleRec(new(box.X+4,box.Y+5,box.Width,box.Height),Ink);
        Raylib.DrawRectangleRec(box,CaptionPaper);
        Raylib.DrawRectangleLinesEx(box,3,Ink);
        for(int n=0;n<c.Lines.Length;n++)
            Raylib.DrawTextEx(c.Font,c.Lines[n],new(MathF.Round(box.X+10),MathF.Round(box.Y+8+n*(c.Size+2))),c.Size,0,Ink);
    }

    private static void DrawBurst(Rectangle r,Burst burst,float age,bool splash,bool active)
    {
        float pop=age switch
        {
            < .12f => 1.30f*Smooth(age/.12f),
            < .23f => 1.30f-.35f*Smooth((age-.12f)/.11f),
            < .34f => .95f+.05f*Smooth((age-.23f)/.11f),
            _ => 1
        };
        float angle=burst.Angle+(active ? 1.25f*MathF.Sin(5*age) : 0);
        Vector2 center=At(r,burst.X,burst.Y)+Unit(age*102)*Math.Max(0,2-10*age);
        string label=Loc.T(burst.Ru,burst.En);
        int size=splash ? 46 : 27;
        Font font=GameFonts.Get(Typeface.SerifBold,size);
        Vector2 measure=Raylib.MeasureTextEx(font,label,size,0);
        Vector2 radius=new(measure.X*.62f+16,size*1.24f);
        Vector2 basis=Unit(angle*MathF.PI/180);
        void Star(float factor,Vector2 shift,Color color)
        {
            Vector2 origin=center+shift*pop;
            Vector2 Vertex(int j)
            {
                var p=Unit(j*MathF.Tau/28)*radius;
                float length=(j%2==0 ? 1+.06f*MathF.Sin(j*2.7f) : .67f)*factor*pop;
                return origin+new Vector2(p.X*basis.X-p.Y*basis.Y,p.X*basis.Y+p.Y*basis.X)*length;
            }
            for(int j=0;j<28;j++) Raylib.DrawTriangle(origin,Vertex(j+1),Vertex(j),color);
        }
        Star(1.08f,new(3,5),Ink);
        Star(1.08f,Vector2.Zero,Ink);
        Star(1,Vector2.Zero,Rgb(burst.Rgb));
        Star(.79f,Vector2.Zero,new Color(255,255,255,100));
        void Text(Vector2 shift,Color c) => Raylib.DrawTextPro(font,label,center+shift*pop,measure*pop/2,angle,size*pop,0,c);
        Text(new(3,4),Ink);
        for(int j=0;j<8;j++) Text(Unit(j*MathF.Tau/8)*2.4f,Ink);
        Text(Vector2.Zero,Color.White);
    }

    private static void DrawSparks(Vector2 at,float clock)
    {
        for(int j=0;j<19;j++)
        {
            float phase=(clock*(1.8f+j%3*.32f)+j*.371f)%1;
            Vector2 v=Unit(j*2.4f)*(26+j%7*7);
            Vector2 p=at+v*phase+new Vector2(0,19*phase*phase);
            Raylib.DrawLineEx(p,p+v*.14f*(1-phase),2.5f*(1-phase),j%2==0 ? Color.Gold : Color.Orange);
        }
    }

    private static readonly Color[] RibbonColors=[Color.Gold,Color.SkyBlue,Color.Pink,Color.Lime,Color.Orange];
    private static void DrawConfetti(float age)
    {
        for(int j=0;j<85;j++)
        {
            float phase=(age*(.38f+j%5*.03f)+j*.618f)%1;
            float x=36+(j*179%1208)+14*MathF.Sin(phase*8+j);
            float y=-18+phase*700;
            if (y<676-age*1100) continue;
            if (j%4==0)
            {
                Raylib.DrawLineEx(new(x-4,y),new(x+4,y),2,Gold);
                Raylib.DrawLineEx(new(x,y-6),new(x,y+6),2,Gold);
            }
            else
            {
                Vector2 v=Unit(j+phase*7);
                Vector2 p=new(x,y);
                var c=RibbonColors[j%RibbonColors.Length];
                Raylib.DrawLineEx(p-v*5,p,3,c);
                Raylib.DrawLineEx(p,p+new Vector2(v.X,-v.Y)*5,3,c);
            }
        }
    }

    public void Dispose()
    {
        Close();
        foreach (var texture in art) if (texture.Id!=0) Raylib.UnloadTexture(texture);
    }
}
