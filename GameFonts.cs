using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal enum Typeface { Interface, InterfaceBold, Serif, SerifBold }

internal static class GameFonts
{
    private static readonly Dictionary<(Typeface, int), Font> fonts = new();
    private static readonly Dictionary<uint, Typeface> family = new();
    private static readonly Dictionary<Typeface, string> files = new();
    private static readonly int[] codepoints = Enumerable.Range(32, 95)
        .Concat(Enumerable.Range(0x400, 256))
        .Concat("№«»—–−…→←₽°♥×★▶◀“”‘’·•↳▲▼▪▫│─".Select(c => (int)c)).Distinct().ToArray();

    private static string ResolveFontDirectory()
    {
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "assets", "fonts"),
            Path.Combine(Directory.GetCurrentDirectory(), "assets", "fonts"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "assets", "fonts")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "assets", "fonts"))
        ];
        foreach (string candidate in candidates)
        {
            if (Directory.Exists(candidate)) return candidate;
        }
        return Path.Combine(AppContext.BaseDirectory, "assets", "fonts");
    }

    public static void Load()
    {
        string directory = ResolveFontDirectory();
        string[] names = [File.Exists(Path.Combine(directory,"Onest-SemiBold.ttf")) ? "Onest-SemiBold.ttf" : "Onest-Medium.ttf", "Onest-Bold.ttf", "RussoOne-Regular.ttf", "RussoOne-Regular.ttf"];
        foreach (Typeface face in Enum.GetValues<Typeface>())
        {
            files[face] = Path.Combine(directory, names[(int)face]);
        }
    }

    public static Font Get(Typeface face, int size)
    {
        if(face==Typeface.Serif) face=Typeface.SerifBold;
        if (fonts.TryGetValue((face, size), out Font font)) return font;
        int atlasSize = checked(size * 2);
        font = Raylib.LoadFontEx(files[face],atlasSize,codepoints,codepoints.Length);
        if (font.Texture.Id == 0 || font.Texture.Id == Raylib.GetFontDefault().Texture.Id || font.BaseSize != atlasSize)
            throw new InvalidDataException($"Cannot load {face} at {size}px");
        Raylib.GenTextureMipmaps(ref font.Texture);
        Raylib.SetTextureFilter(font.Texture, TextureFilter.Bilinear);
        fonts.Add((face,size), font);
        family.Add(font.Texture.Id, face);
        return font;
    }

    internal static void CheckBundle()
    {
        foreach(var (face,name) in new[]{(Typeface.SerifBold,"RussoOne-Regular.ttf"),(Typeface.InterfaceBold,"Onest-Bold.ttf"),
            (Typeface.Interface,File.Exists(Path.Combine(ResolveFontDirectory(),"Onest-SemiBold.ttf")) ? "Onest-SemiBold.ttf" : "Onest-Medium.ttf")})
            if(Path.GetFileName(files[face])!=name || Get(face,18).BaseSize!=36 || Get(face,18).Texture.Mipmaps<2)
                throw new InvalidDataException("Font atlas: "+name);
    }
    public static Font At(Font face, int size) => Get(family.GetValueOrDefault(face.Texture.Id), size);

    private static readonly Dictionary<string, InlinePart[]> parsedInline = new(StringComparer.Ordinal);
    private enum InlineKind { Plain, CheckboxEmpty, CheckboxChecked, Keycap, SlashSep }
    private readonly record struct InlinePart(InlineKind Kind, string Text);

    private static float Spacing(int size) => size <= 16 ? 0.5f : 0f;

    private static bool TryNormalizeSingleKey(ReadOnlySpan<char> raw, out string label)
    {
        label = "";
        if (raw.IsEmpty || raw.Length > 12) return false;
        if (raw.Length == 1)
        {
            char c = raw[0];
            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
            {
                label = c.ToString();
                return true;
            }
            return false;
        }
        if (raw.Length == 3 && raw[0] >= '0' && raw[0] <= '9' && (raw[1] == '–' || raw[1] == '-') && raw[2] >= '0' && raw[2] <= '9')
        {
            label = $"{raw[0]}–{raw[2]}";
            return true;
        }
        if ((raw[0] == 'F' || raw[0] == 'f') && raw.Length is 2 or 3 && int.TryParse(raw[1..], out int fNum) && fNum is >= 1 and <= 12)
        {
            label = $"F{fNum}";
            return true;
        }
        string upper = raw.ToString().ToUpperInvariant();
        switch (upper)
        {
            case "ESC":
            case "ENTER":
            case "TAB":
            case "CTRL":
            case "SHIFT":
            case "ALT":
            case "SPACE":
            case "CLICK":
            case "LMB":
            case "RMB":
            case "MMB":
            case "WASD":
            case "ПРОБЕЛ":
            case "ЛКМ":
            case "ПКМ":
                label = upper;
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseBracketToken(ReadOnlySpan<char> inner, List<InlinePart> dest)
    {
        if (inner.Length == 1)
        {
            char c = inner[0];
            if (c == ' ')
            {
                dest.Add(new InlinePart(InlineKind.CheckboxEmpty, ""));
                return true;
            }
            if (c == 'x' || c == '×' || c == '✓')
            {
                dest.Add(new InlinePart(InlineKind.CheckboxChecked, ""));
                return true;
            }
        }

        ReadOnlySpan<char> trimmed = inner.Trim();
        if (trimmed.IsEmpty || trimmed.Length > 24) return false;

        if (trimmed.Equals("W/A/S/D", StringComparison.OrdinalIgnoreCase))
        {
            dest.Add(new InlinePart(InlineKind.Keycap, "WASD"));
            return true;
        }

        if (trimmed.Contains('/'))
        {
            Span<string> keys = new string[3];
            int count = 0;
            ReadOnlySpan<char> rest = trimmed;
            while (true)
            {
                int slash = rest.IndexOf('/');
                ReadOnlySpan<char> seg = (slash < 0 ? rest : rest[..slash]).Trim();
                if (count >= 3 || !TryNormalizeSingleKey(seg, out string keyLabel))
                    return false;
                keys[count++] = keyLabel;
                if (slash < 0) break;
                rest = rest[(slash + 1)..];
            }
            if (count < 2) return false;
            for (int k = 0; k < count; k++)
            {
                if (k > 0) dest.Add(new InlinePart(InlineKind.SlashSep, "/"));
                dest.Add(new InlinePart(InlineKind.Keycap, keys[k]));
            }
            return true;
        }

        if (TryNormalizeSingleKey(trimmed, out string single))
        {
            dest.Add(new InlinePart(InlineKind.Keycap, single));
            return true;
        }
        return false;
    }

    private static InlinePart[] ParseInlineLine(string line)
    {
        if (parsedInline.TryGetValue(line, out var cached)) return cached;
        var parts = new List<InlinePart>(8);
        var tokenBuf = new List<InlinePart>(4);
        int i = 0, plainStart = 0;
        bool anyToken = false;

        while (i < line.Length)
        {
            int open = line.IndexOf('[', i);
            if (open < 0) break;
            int close = line.IndexOf(']', open + 1);
            if (close < 0) break;

            tokenBuf.Clear();
            ReadOnlySpan<char> inner = line.AsSpan(open + 1, close - open - 1);
            if (TryParseBracketToken(inner, tokenBuf))
            {
                if (open > plainStart)
                    parts.Add(new InlinePart(InlineKind.Plain, line[plainStart..open]));
                parts.AddRange(tokenBuf);
                anyToken = true;
                plainStart = close + 1;
                i = close + 1;
            }
            else
            {
                i = open + 1;
            }
        }

        InlinePart[] result;
        if (!anyToken)
        {
            result = Array.Empty<InlinePart>();
        }
        else
        {
            if (plainStart < line.Length)
                parts.Add(new InlinePart(InlineKind.Plain, line[plainStart..]));
            result = parts.ToArray();
        }

        if (parsedInline.Count >= 512) parsedInline.Clear();
        parsedInline[line] = result;
        return result;
    }

    private static void GetKeycapMetrics(string label, int size, out Font capFont, out int capFontSize, out float capSpacing, out Vector2 labelDim, out float capW, out float capH, out float outerPad)
    {
        capFontSize = Math.Clamp((int)MathF.Round(size * 0.78f), 10, 22);
        capFont = Get(Typeface.InterfaceBold, capFontSize);
        capSpacing = capFontSize <= 14 ? 0.4f : 0.2f;
        labelDim = Raylib.MeasureTextEx(capFont, label, capFontSize, capSpacing);
        capH = MathF.Round(MathF.Max(size + 1f, capFontSize + 5f));
        float padX = MathF.Max(5f, MathF.Round(capFontSize * 0.42f));
        capW = MathF.Round(MathF.Max(capH + 1f, labelDim.X + padX * 2f));
        outerPad = 1f;
    }

    private static void GetCheckboxMetrics(int size, out float boxSize, out float outerPad)
    {
        boxSize = MathF.Round(MathF.Max(11f, size * 0.84f));
        outerPad = 1f;
    }

    private static void GetSlashMetrics(Font native, int size, float spacing, out float slashW, out float advance)
    {
        slashW = Raylib.MeasureTextEx(native, "/", size, spacing).X;
        advance = MathF.Round(slashW + 6f);
    }

    private static float MeasureLine(Font native, string line, int size, float spacing)
    {
        if (!line.Contains('['))
            return Raylib.MeasureTextEx(native, line, size, spacing).X;
        var parts = ParseInlineLine(line);
        if (parts.Length == 0)
            return Raylib.MeasureTextEx(native, line, size, spacing).X;

        float width = 0f;
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            switch (p.Kind)
            {
                case InlineKind.Plain:
                    width += Raylib.MeasureTextEx(native, p.Text, size, spacing).X;
                    break;
                case InlineKind.CheckboxEmpty:
                case InlineKind.CheckboxChecked:
                    GetCheckboxMetrics(size, out float boxSize, out float cbPad);
                    width += boxSize + cbPad * 2f;
                    break;
                case InlineKind.Keycap:
                    GetKeycapMetrics(p.Text, size, out _, out _, out _, out _, out float capW, out _, out float kcPad);
                    width += capW + kcPad * 2f;
                    break;
                case InlineKind.SlashSep:
                    GetSlashMetrics(native, size, spacing, out _, out float slashAdv);
                    width += slashAdv;
                    break;
            }
        }
        return width;
    }

    private static void DrawCheckbox(float x, float y, int size, bool isChecked, Color ink)
    {
        GetCheckboxMetrics(size, out float boxSize, out float outerPad);
        float bx = MathF.Round(x + outerPad);
        float by = MathF.Round(y + (size - boxSize) * 0.5f);
        const float roundness = 0.24f;
        float a = ink.A / 255f;
        float lum = 0.299f * ink.R + 0.587f * ink.G + 0.114f * ink.B;
        bool onLightPaper = lum < 110f;

        Rectangle rect = new(bx, by, boxSize, boxSize);
        if (onLightPaper)
        {
            Raylib.DrawRectangleRounded(new Rectangle(bx + 0.5f, by + 1.2f, boxSize, boxSize), roundness, 5, new Color(112, 92, 68, (int)(130 * a)));
            if (isChecked)
            {
                Raylib.DrawRectangleRounded(rect, roundness, 5, new Color(188, 142, 62, (int)(246 * a)));
                Raylib.DrawRectangleRoundedLinesEx(rect, roundness, 5, 1.2f, new Color(86, 64, 40, (int)(232 * a)));
            }
            else
            {
                Raylib.DrawRectangleRounded(rect, roundness, 5, new Color(240, 231, 210, (int)(242 * a)));
                Raylib.DrawRectangleRoundedLinesEx(rect, roundness, 5, 1.2f, new Color(108, 86, 62, (int)(216 * a)));
            }
        }
        else
        {
            Raylib.DrawRectangleRounded(new Rectangle(bx + 0.5f, by + 1.4f, boxSize, boxSize), roundness, 5, new Color(8, 7, 10, (int)(195 * a)));
            if (isChecked)
            {
                Raylib.DrawRectangleRounded(rect, roundness, 5, new Color(198, 154, 72, (int)(242 * a)));
                Raylib.DrawRectangleRoundedLinesEx(rect, roundness, 5, 1.2f, new Color(246, 222, 162, (int)(242 * a)));
            }
            else
            {
                Raylib.DrawRectangleRounded(rect, roundness, 5, new Color(34, 30, 28, (int)(226 * a)));
                Raylib.DrawRectangleRoundedLinesEx(rect, roundness, 5, 1.2f, new Color(188, 172, 144, (int)(196 * a)));
            }
        }

        if (isChecked)
        {
            float stroke = MathF.Max(1.8f, boxSize * 0.145f);
            Color checkCol = new(24, 18, 12, (int)(255 * a));
            Vector2 p0 = new(bx + boxSize * 0.22f, by + boxSize * 0.54f);
            Vector2 p1 = new(bx + boxSize * 0.43f, by + boxSize * 0.74f);
            Vector2 p2 = new(bx + boxSize * 0.80f, by + boxSize * 0.28f);
            Raylib.DrawLineEx(p0, p1, stroke, checkCol);
            Raylib.DrawLineEx(p1, p2, stroke, checkCol);
            Raylib.DrawCircleV(p1, stroke * 0.48f, checkCol);
        }
    }

    private static void DrawKeycap(float x, float y, int size, string label, Color ink)
    {
        GetKeycapMetrics(label, size, out Font capFont, out int capFontSize, out float capSpacing, out Vector2 labelDim, out float capW, out float capH, out float outerPad);
        float capX = MathF.Round(x + outerPad);
        float capY = MathF.Round(y + (size - capH) * 0.5f);
        float roundness = Math.Clamp(5f / capH, 0.22f, 0.34f);
        float depth = MathF.Max(1.5f, MathF.Round(capH * 0.09f));
        float a = ink.A / 255f;
        float lum = 0.299f * ink.R + 0.587f * ink.G + 0.114f * ink.B;
        bool onLightPaper = lum < 110f;

        Color skirtCol, faceCol, bevelCol, borderCol, glyphCol;
        if (onLightPaper)
        {
            bool crimsonAccent = ink.R > 120 && ink.R > ink.G * 2;
            skirtCol  = new Color(118, 98, 74, (int)(172 * a));
            faceCol   = new Color(244, 236, 216, (int)(252 * a));
            bevelCol  = new Color(255, 252, 244, (int)(160 * a));
            borderCol = crimsonAccent
                ? new Color(148, 42, 46, (int)(228 * a))
                : new Color(92, 72, 50, (int)(225 * a));
            glyphCol  = crimsonAccent
                ? new Color(ink.R, ink.G, ink.B, (int)(255 * a))
                : new Color(32, 24, 18, (int)(255 * a));
        }
        else
        {
            skirtCol  = new Color(10, 9, 9, (int)(235 * a));
            faceCol   = new Color(40, 36, 34, (int)(242 * a));
            bevelCol  = new Color(252, 236, 200, (int)(48 * a));
            borderCol = new Color(220, 192, 138, (int)(218 * a));
            glyphCol  = new Color(248, 240, 224, (int)(255 * a));
        }

        Raylib.DrawRectangleRounded(new Rectangle(capX, capY + depth, capW, capH), roundness, 6, skirtCol);
        Rectangle faceRect = new(capX, capY, capW, capH);
        Raylib.DrawRectangleRounded(faceRect, roundness, 6, faceCol);
        if (capW > 10f)
            Raylib.DrawLineEx(new(capX + 3.5f, capY + 1.2f), new(capX + capW - 3.5f, capY + 1.2f), 1f, bevelCol);
        Raylib.DrawRectangleRoundedLinesEx(faceRect, roundness, 6, 1.15f, borderCol);

        float gx = MathF.Round(capX + (capW - labelDim.X) * 0.5f);
        float gy = MathF.Round(capY + (capH - labelDim.Y) * 0.5f);
        Raylib.DrawTextEx(capFont, label, new Vector2(gx, gy), capFontSize, capSpacing, glyphCol);
    }

    private static void DrawLine(Font native, string line, Vector2 pixel, int size, float spacing, Color ink, bool shadowed)
    {
        bool drawShadow = shadowed && ink.R + ink.G + ink.B > 180;
        Color shadow = drawShadow ? new Color(8, 11, 18, (ink.A * 182) / 255) : default;

        if (!line.Contains('['))
        {
            if (drawShadow)
                Raylib.DrawTextEx(native, line, pixel + new Vector2(1f, 1.2f), size, spacing, shadow);
            Raylib.DrawTextEx(native, line, pixel, size, spacing, ink);
            return;
        }

        var parts = ParseInlineLine(line);
        if (parts.Length == 0)
        {
            if (drawShadow)
                Raylib.DrawTextEx(native, line, pixel + new Vector2(1f, 1.2f), size, spacing, shadow);
            Raylib.DrawTextEx(native, line, pixel, size, spacing, ink);
            return;
        }

        float cx = pixel.X;
        float cy = pixel.Y;
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            switch (p.Kind)
            {
                case InlineKind.Plain:
                {
                    Vector2 pos = new(MathF.Round(cx), cy);
                    if (drawShadow)
                        Raylib.DrawTextEx(native, p.Text, pos + new Vector2(1f, 1.2f), size, spacing, shadow);
                    Raylib.DrawTextEx(native, p.Text, pos, size, spacing, ink);
                    cx += Raylib.MeasureTextEx(native, p.Text, size, spacing).X;
                    break;
                }
                case InlineKind.CheckboxEmpty:
                case InlineKind.CheckboxChecked:
                {
                    GetCheckboxMetrics(size, out float boxSize, out float cbPad);
                    DrawCheckbox(cx, cy, size, p.Kind == InlineKind.CheckboxChecked, ink);
                    cx += boxSize + cbPad * 2f;
                    break;
                }
                case InlineKind.Keycap:
                {
                    GetKeycapMetrics(p.Text, size, out _, out _, out _, out _, out float capW, out _, out float kcPad);
                    DrawKeycap(cx, cy, size, p.Text, ink);
                    cx += capW + kcPad * 2f;
                    break;
                }
                case InlineKind.SlashSep:
                {
                    GetSlashMetrics(native, size, spacing, out float slashW, out float slashAdv);
                    Color slashCol = new(ink.R, ink.G, ink.B, (ink.A * 155) / 255);
                    Vector2 spos = new(MathF.Round(cx + (slashAdv - slashW) * 0.5f), cy);
                    if (drawShadow)
                        Raylib.DrawTextEx(native, "/", spos + new Vector2(1f, 1.2f), size, spacing, shadow);
                    Raylib.DrawTextEx(native, "/", spos, size, spacing, slashCol);
                    cx += slashAdv;
                    break;
                }
            }
        }
    }

    public static Vector2 Measure(Font face, string value, int size)
    {
        if (string.IsNullOrEmpty(value)) return Vector2.Zero;
        string text = Loc.Tr(value);
        Font native = At(face, size);
        float spacing = Spacing(size);
        if (!text.Contains('['))
            return Raylib.MeasureTextEx(native, text, size, spacing);

        if (!text.Contains('\n'))
        {
            float baseH = Raylib.MeasureTextEx(native, "A", size, spacing).Y;
            return new Vector2(MeasureLine(native, text, size, spacing), baseH);
        }

        string[] lines = text.Split('\n');
        float maxW = 0f;
        foreach (string ln in lines)
            maxW = MathF.Max(maxW, MeasureLine(native, ln, size, spacing));
        float totalH = Raylib.MeasureTextEx(native, text, size, spacing).Y;
        return new Vector2(maxW, totalH);
    }

    public static void Draw(Font face, string value, Vector2 at, int size, Color ink, bool shadowed = true)
    {
        if (string.IsNullOrEmpty(value)) return;
        value = Loc.RenderText(value);
        Font native = At(face, size);
        float spacing = Spacing(size);
        Vector2 pixel = new(MathF.Round(at.X), MathF.Round(at.Y));

        if (!value.Contains('\n'))
        {
            DrawLine(native, value, pixel, size, spacing, ink, shadowed);
            return;
        }

        string[] lines = value.Split('\n');
        float stepY = MathF.Round(size * 1.15f);
        for (int i = 0; i < lines.Length; i++)
            DrawLine(native, lines[i], new Vector2(pixel.X, pixel.Y + i * stepY), size, spacing, ink, shadowed);
    }

    public static void Unload()
    {
        foreach (Font font in fonts.Values) Raylib.UnloadFont(font);
        fonts.Clear();
        family.Clear();
        files.Clear();
        parsedInline.Clear();
    }
}


internal static class DossierPrint
{
    internal static readonly Color Studio = new(20, 18, 16, 245);
    internal static readonly Color Matte = new(24, 22, 20, 255);
    internal static readonly Color Selected = new(44, 38, 34, 255);
    internal static readonly Color Stone = new(64, 58, 52, 220);
    internal static readonly Color Ivory = new(247, 241, 230, 255);
    internal static readonly Color Quiet = new(189, 178, 164, 255);
    internal static readonly Color Crimson = new(178, 34, 54, 255);
    internal static readonly Color RedInk = new(158, 29, 47, 255);
    internal static readonly Color Paper = new(241, 235, 222, 255);
    internal static readonly Color Ink = new(26, 24, 22, 255);
    internal static readonly Color Muted = new(102, 95, 86, 255);
    internal static readonly Color Rule = new(210, 200, 182, 255);

    internal static void Sheet(Rectangle page)
    {
        Raylib.DrawRectangleRec(new(page.X + 4, page.Y + 6, page.Width, page.Height), new Color(12, 9, 7, 120));
        Raylib.DrawRectangleRec(page, Paper);
        Raylib.DrawRectangleLinesEx(page, 1, new Color(205, 195, 176, 255));
    }

    internal static void Text(string value, float x, float y, int size, Color ink, Typeface face = Typeface.Interface)
        => GameFonts.Draw(GameFonts.Get(face, size), value, new(x, y), size, ink, shadowed: false);
}
