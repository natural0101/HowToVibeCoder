using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal sealed class IntroComic : IDisposable
{
    private const float Entrance = .34f, Turn = .30f;
    private static readonly Rectangle Page = new(48, 10, 1184, 666);
    private static readonly Color Paper = new(24, 20, 22, 255), Ink = new(14, 12, 14, 255);
    private static readonly Color Ground = new(10, 9, 12, 255), CaptionPaper = new(236, 223, 195, 242);
    private static readonly Color BrassTrim = new(182, 144, 78, 220);
    private static readonly (string Ru, string En)[] Captions =
    [
        ("2026. Башня «Энтропик». Ты вернулся за своим заводом и долей,\nа нашёл этаж молодых спецов на смузи и автодополнении.\nПервая неделя выглядела почти прилично.",
         "2026. Entropic Tower. You came back for your factory and your stake,\nand found a floor of smoothie-drinking kids on autocomplete.\nWeek one almost looked respectable."),
        ("Потом в серверной полыхнула стойка B12.\nРебята в наушниках даже голову не повернули:\nу них на экране всё зелёное — значит, прод живой.",
         "Then rack B12 caught fire in the server room.\nThe kids in headphones didn't even look up:\ntheir screen stayed green, so prod must be fine."),
        ("Пока этаж задыхался от дыма, HR Вероника спокойно раскинула Таро:\n«Денег в кассе ноль, руководство пропало.\nХочешь вернуть своё — выбивай убытки руками.»",
         "While smoke filled the floor, HR Veronika calmly dealt her Tarot:\n\"Zero cash in the vault, executives vanished.\nWant your stake back? Recover the losses by hand.\""),
        ("Северный лифт на верхние этажи наглухо обмотан цепями,\nщиток искрит, а из-под створок тянет гарью.\nКто-то наверху очень не хочет гостей. Пора доставать ремень.",
         "The North Elevator to the upper floors is chained shut,\nthe junction box is sparking, and smoke creeps under the doors.\nSomeone upstairs really doesn't want visitors. Time to unbuckle the belt."),
        ("Из прогоревших стоек полезли заводные Джаба-жабы —\nживые баги с латунным заводом.\nНо старый Баг-трекер у стойки всё ещё платит за них наличкой.",
         "Out of the fried racks poured wind-up Jaba-frogs —\nliving brass-keyed bugs.\nAt least the old Bug-Tracker by the desk still pays cash for them."),
        ("Сами программисты разбежались по углам.\nНа крики им плевать, зато на открытый ноутбук\nс халявной подпиской они слетаются как мотыльки.",
         "The coders themselves scattered into the corners.\nShouting doesn't work, but leave an open laptop\nwith a free subscription and they swarm straight to the light."),
        ("За каждого сданного кадра Вероника крутит свой аппарат.\nВ кассе — от сачка и паяльника до серьёзных аргументов 90-х.\nЧем жирнее спец, тем быстрее откроется дорога наверх.",
         "For every coder you bring in, Veronika pulls her vintage slot lever.\nStock runs from a net and soldering iron to serious 90s hardware.\nThe bigger the catch, the faster you buy your way upstairs."),
        ("На счетах пусто. Карантин заперт. В руках — только отцовский ремень.\nПервый этаж башни «Энтропик».\nНаводим порядок по-старому.",
         "Accounts at zero. Quarantine locked. Just your father's leather belt.\nFloor One of Entropic Tower.\nTime to restore order the old-fashioned way.")
    ];

    private readonly Texture2D[] art = new Texture2D[8];
    private enum Cue { Slam, Boom, Snap, Zap, Jackpot }
    private sealed record SoundBeat(float Time, Cue Sound);
    private static readonly SoundBeat[][] Beats =
    [
        [new(.40f, Cue.Slam)],
        [new(.32f, Cue.Boom), new(1.10f, Cue.Zap)],
        [new(.42f, Cue.Zap)],
        [new(.38f, Cue.Snap)],
        [new(.40f, Cue.Slam)],
        [new(.38f, Cue.Zap)],
        [new(.36f, Cue.Jackpot)],
        [new(.42f, Cue.Slam)]
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
        foreach (var beat in Beats[panel])
        {
            if (before >= beat.Time || age < beat.Time) continue;
            switch (beat.Sound)
            {
                case Cue.Slam: AudioMusicSystem.PlayComicPanelSlam(); break;
                case Cue.Boom: AudioMusicSystem.PlayComicBoom(); break;
                case Cue.Snap: AudioMusicSystem.PlayComicWhipSnap(); break;
                case Cue.Zap: AudioMusicSystem.PlayComicZap(); break;
                case Cue.Jackpot: AudioMusicSystem.PlaySfx("slot_win_jackpot"); break;
            }
            if (beat.Sound is Cue.Snap or Cue.Boom or Cue.Jackpot)
            {
                shakeAge = age - beat.Time;
                shakePower = beat.Sound == Cue.Snap ? 6 : 4;
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
        Raylib.DrawRectangleLinesEx(Page,3,BrassTrim);
        for (int i=First(panel); i<=panel; i++) DrawPanel(i,camera,scale);
        Raylib.EndMode2D();

        Raylib.BeginMode2D(new Camera2D { Offset = offset, Zoom = scale });
        Color uiCream = new(236, 224, 198, 255);
        for (int i=0;i<8;i++)
            Raylib.DrawRectangle(49+i*14,696,10,5,i==panel ? Gold : i<panel ? uiCream : new Color(83,78,84,255));
        string counter = Loc.IsEn ? $"PANEL {panel+1} / 8" : $"КАДР {panel+1} / 8";
        string hint = Loc.T("[ПРОБЕЛ / ЛКМ] ДАЛЕЕ   [ESC] ПРОПУСТИТЬ","[SPACE / CLICK] NEXT   [ESC] SKIP");
        var face = GameFonts.Get(Typeface.InterfaceBold,15);
        GameFonts.Draw(face,counter,new(172,689),15,uiCream,false);
        GameFonts.Draw(face,hint,new(Page.X+Page.Width-GameFonts.Measure(face,hint,15).X,689),15,uiCream,false);
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
        // Frame key subjects cleanly in panoramic strips and splashes.
        float centerY = i switch { 3 => 115, 6 => 222, _ => art[i].Height / 2f };
        Rectangle src=new((art[i].Width-w)*(.5f+(i%2==0 ? .16f : -.16f)*d),
            Math.Clamp(centerY-h/2+(i==7 ? (art[i].Height-h)*.14f*d : 0),0,Math.Max(0,art[i].Height-h)),w,h);
        Raylib.DrawTexturePro(art[i],src,inside,Vector2.Zero,0,Color.White);
        if (active)
        {
            Vector2 Feature(float x,float y) => At(inside,(x*art[i].Width-src.X)/w,(y*art[i].Height-src.Y)/h);
            // Subtle diegetic sparks on burning server rack (p2), sparking elevator panel (p4), and fried rack (p5)
            if (i == 1) DrawSparks(Feature(.24f, .52f), age);
            else if (i == 3) DrawSparks(Feature(.475f, .52f), age);
            else if (i == 4) DrawSparks(Feature(.46f, .54f), age);
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
            if (age<.18f)
            {
                float fade=1-age/.18f;
                Raylib.DrawRectangleRec(inside,new Color(255,244,218,(int)(42*fade)));
            }
        }
        DrawCaption(i,inside);
        if (!active) Raylib.DrawRectangleRec(inside,new Color(14,12,18,74));
        Raylib.EndScissorMode();
    }

    private void DrawCaption(int i,Rectangle r)
    {
        var c=lettering[Loc.IsEn ? 1 : 0,i];
        bool bottom = i is 0 or 2 or 3 or 7;
        Rectangle box=new(r.X+12, bottom ? r.Y+r.Height-12-c.Height : r.Y+12, c.Width, c.Height);
        Raylib.DrawRectangleRec(new(box.X+4,box.Y+5,box.Width,box.Height),new Color(6,5,8,210));
        Raylib.DrawRectangleRec(box,CaptionPaper);
        Raylib.DrawRectangleLinesEx(box,2.5f,Ink);
        Raylib.DrawRectangleLinesEx(new(box.X+3,box.Y+3,box.Width-6,box.Height-6),1f,BrassTrim);
        for(int n=0;n<c.Lines.Length;n++)
            Raylib.DrawTextEx(c.Font,c.Lines[n],new(MathF.Round(box.X+10),MathF.Round(box.Y+8+n*(c.Size+2))),c.Size,0,Ink);
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
