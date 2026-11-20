using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal sealed class IntroComic : IDisposable
{
    private const float Entrance = .42f, Turn = .52f;
    private static readonly Rectangle Page = new(48, 10, 1184, 666);
    private static readonly Color Paper = new(237, 225, 200, 255), Ink = new(20, 18, 25, 255);
    private static readonly Color Ground = new(18, 16, 26, 255), CaptionPaper = new(247, 231, 184, 255);
    private static readonly (string Ru, string En)[] Captions =
    [
        ("2026. Башня «Клауд Энтропик».\nСтарые методы возвращаются.", "2026. Cloud Entropic Tower.\nOld-school methods are back."),
        ("Офис захватили вайбкодеры.\nЖмут «Accept All» — прод горит.", "Vibe coders overran the floor.\nSpamming Accept All while prod burns."),
        ("Касса на нуле. У Вероники план:\nлови их, сажай в опенспейс, руби кэш.", "Cash is zero. Veronika's plan:\ncatch 'em, fill the open-space, print cash."),
        ("За Северным лифтом — угроза посерьёзней...\nПора наводить порядок на 1-м этаже!", "Behind the North Elevator lurks worse...\nTime to clean up Floor One!"),
        ("1998. Петрович спаял Джаба-жаб\nдля отладки. Теперь они везде.", "1998. Petrovich soldered Jabba-frogs\nfor debugging. Now they're everywhere."),
        ("Вруби халявный Wi-Fi на ЯблоБуке —\nвайбкодеры сами сбегутся на приманку.", "Flip free Wi-Fi on the AppleBook —\nvibe coders swarm straight to the bait."),
        ("Сдавай редких кадров Веронике —\nкрути скины и снимай карантин.", "Bring rare catches to Veronika —\nspin skins and lift the wing lockdown."),
        ("Этаж 1. $0 в кармане.\nДобро пожаловать в «Белую Акулу IT»!", "Floor 1. $0 in your pocket.\nWelcome to White Shark IT!")
    ];

    private readonly Texture2D[] art = new Texture2D[8];
    private readonly float[] drift = new float[8];
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
            // Keep the slot arch and Petrovich's forehead clear even at maximum drift.
            int limit = i switch { 6 => 164, 4 => 194, 2 => (int)(566 * .46f), _ => (int)(566 * .56f) };
            lettering[0,i] = Typeset(Captions[i].Ru, limit);
            lettering[1,i] = Typeset(Captions[i].En, limit);
        }
        Active = true;
        turning = false;
        Array.Clear(drift);
        Reveal(0);
    }

    private static Caption Typeset(string text, int limit)
    {
        const int padding = 10;
        int room = limit - 2 * padding;
        var paragraphs = text.Split('\n');
        for (int size = 18; size >= 12; size--)
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
            if (lines.Count > 2 && size > 14) continue;
            int width = (int)Math.Ceiling(lines.Max(Width)) + 2 * padding;
            if (width <= limit) return new(font, size, lines.ToArray(), width, 16 + lines.Count * (size + 2));
        }
        throw new InvalidDataException($"Comic caption exceeds {limit}px: {text}");
    }

    private void Reveal(int index)
    {
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
            turnAge += dt;
            if (turnAge >= Turn || advance) { turning = false; Reveal(4); }
            return;
        }
        age += dt;
        drift[panel] = Smooth(age / duration);
        if (AudioMusicSystem.IsComicNarrationPlaying()) silence = 0;
        else silence += dt;
        if (!advance && (AudioMusicSystem.HasComicVoice ? silence < .55f : age < 5f)) return;
        if (panel == 7) Close();
        else if (panel == 3)
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

    internal void Draw()
    {
        float scale = UiViewport.Scale;
        Vector2 offset = new((Raylib.GetScreenWidth() - 1280 * scale) / 2, (Raylib.GetScreenHeight() - 720 * scale) / 2);
        Raylib.ClearBackground(Ground);
        if (panel % 4 == 3 && age < Entrance)
            offset.Y += scale * 3 * MathF.Sin(56 * age) * (1 - age / Entrance);
        Raylib.BeginMode2D(new Camera2D { Offset = offset, Zoom = scale });
        Raylib.DrawRectangleRec(new(Page.X + 5, Page.Y + 6, Page.Width, Page.Height), new Color(0, 0, 0, 130));
        Raylib.DrawRectangleRec(Page, Paper);
        int first = panel / 4 * 4;
        for (int i = first; i <= panel; i++) DrawPanel(i, offset, scale);
        if (turning)
        {
            int fold = (int)(Page.X + Page.Width * (1 - Smooth(turnAge / Turn)));
            int width = (int)(Page.X + Page.Width) - fold;
            Raylib.DrawRectangleGradientH(fold - 18, (int)Page.Y, 18, (int)Page.Height, Color.Blank, new Color(26, 18, 20, 110));
            Raylib.DrawRectangle(fold, (int)Page.Y, width, (int)Page.Height, Paper);
            Raylib.DrawRectangleGradientH(fold, (int)Page.Y, Math.Min(width, 34), (int)Page.Height, new Color(149, 125, 96, 255), Paper);
        }
        Raylib.DrawRectangleLinesEx(Page, 1, new Color(180, 161, 135, 255));
        string counter = Loc.IsEn ? $"PAGE {first/4+1} / 2    PANEL {panel+1} / 8" : $"СТР. {first/4+1} / 2    КАДР {panel+1} / 8";
        string hint = Loc.T("[ПРОБЕЛ / ЛКМ] ДАЛЕЕ   [ESC] ПРОПУСТИТЬ", "[SPACE / CLICK] NEXT   [ESC] SKIP");
        var footer = GameFonts.Get(Typeface.SerifBold, 15);
        GameFonts.Draw(footer, counter, new(Page.X, 690), 15, Paper, false);
        var face = GameFonts.Get(Typeface.Interface, 15);
        GameFonts.Draw(face, hint, new(Page.X + Page.Width - GameFonts.Measure(face, hint, 15).X, 690), 15, Paper, false);
        Raylib.EndMode2D();
    }

    private void DrawPanel(int i, Vector2 offset, float scale)
    {
        int slot = i % 4;
        Rectangle r = new(65 + slot % 2 * 582, 24 + slot / 2 * 325, 566, 312);
        Rectangle cell = new(r.X - 7, r.Y - 6, r.Width + 14, r.Height + 13);
        void Clip(Rectangle area)
        {
            int left = (int)MathF.Floor(offset.X + scale * area.X);
            int top = (int)MathF.Floor(offset.Y + scale * area.Y);
            Raylib.BeginScissorMode(left, top,
                (int)MathF.Ceiling(offset.X + scale * (area.X + area.Width)) - left,
                (int)MathF.Ceiling(offset.Y + scale * (area.Y + area.Height)) - top);
        }
        bool active = i == panel;
        float t = active ? Math.Clamp(age / Entrance, 0, 1) : 1;
        float spring = (1 - t) * (1 - t) * MathF.Cos(6 * t);
        if (slot == 3)
        {
            r.X += r.Width * .13f * spring;
            r.Y += r.Height * .13f * spring;
            r.Width *= 1 - .26f * spring;
            r.Height *= 1 - .26f * spring;
        }
        else
        {
            r.X += (slot == 1 ? 1 : -1) * 105 * spring;
            if (slot == 2) r.Y += 56 * spring;
        }
        Clip(cell);
        Raylib.DrawRectangleRec(new(r.X + 5, r.Y + 6, r.Width, r.Height), Ink);
        if (active) Raylib.DrawRectangleLinesEx(new(r.X - 2, r.Y - 2, r.Width + 4, r.Height + 4), 2, new Color(226, 174, 73, 255));
        Rectangle inside = new(r.X + 4, r.Y + 4, r.Width - 8, r.Height - 8);
        Raylib.EndScissorMode();
        Clip(Raylib.GetCollisionRec(cell, inside));
        float d = drift[i];
        float fit = Math.Max(inside.Width / 960, inside.Height / 540) * (1 + .065f * d);
        float w = inside.Width / fit, h = inside.Height / fit;
        Rectangle crop = new((960 - w) * (.5f + (slot % 2 == 0 ? .24f : -.24f) * d), (540 - h) * (.5f - .18f * d), w, h);
        Raylib.DrawTexturePro(art[i], crop, inside, Vector2.Zero, 0, Color.White);
        var caption = lettering[Loc.IsEn ? 1 : 0,i];
        Rectangle box = new(r.X + 12, i == 3 ? r.Y + r.Height - caption.Height - 12 : r.Y + 12, caption.Width, caption.Height);
        Raylib.DrawRectangleRec(new(box.X + 3, box.Y + 4, box.Width, box.Height), Ink);
        Raylib.DrawRectangleRec(box, CaptionPaper);
        Raylib.DrawRectangleLinesEx(box, 3, Ink);
        for (int line = 0; line < caption.Lines.Length; line++)
            Raylib.DrawTextEx(caption.Font, caption.Lines[line], new(MathF.Round(box.X + 10), MathF.Round(box.Y + 8 + line * (caption.Size + 2))), caption.Size, 0, Ink);
        if (!active) Raylib.DrawRectangleRec(inside, new Color(16, 14, 22, 71));
        Raylib.EndScissorMode();
        Clip(cell);
        Raylib.DrawRectangleLinesEx(r, 4, Ink);
        Raylib.EndScissorMode();
    }

    public void Dispose()
    {
        Close();
        foreach (var texture in art) if (texture.Id != 0) Raylib.UnloadTexture(texture);
    }
}
