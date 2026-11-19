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
        .Concat("№«»—–…→←₽°♥×★▶◀“”‘’·•↳▲▼▪▫│─".Select(c => (int)c)).Distinct().ToArray();

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
        string[] names = ["PTSans-Regular.ttf", "PTSans-Bold.ttf", "Oswald-SemiBold.ttf", "Oswald-Bold.ttf"];
        foreach (Typeface face in Enum.GetValues<Typeface>())
        {
            files[face] = Path.Combine(directory, names[(int)face]);
        }
    }

    public static Font Get(Typeface face, int size)
    {
        if (fonts.TryGetValue((face, size), out Font font)) return font;
        int atlasSize = checked(size * 2);
        font = Raylib.LoadFontEx(files[face],atlasSize,codepoints,codepoints.Length);
        if (font.Texture.Id == 0 || font.Texture.Id == Raylib.GetFontDefault().Texture.Id || font.BaseSize != atlasSize)
            throw new InvalidDataException($"Cannot load {face} at {size}px");
        Raylib.SetTextureFilter(font.Texture, TextureFilter.Bilinear);
        fonts.Add((face,size), font);
        family.Add(font.Texture.Id, face);
        return font;
    }

    public static Font At(Font face, int size) => Get(family.GetValueOrDefault(face.Texture.Id), size);

    private static float Spacing(int size) => size <= 16 ? 0.5f : 0f;

    public static Vector2 Measure(Font face, string value, int size) =>
        Raylib.MeasureTextEx(At(face, size), value, size, Spacing(size));

    public static void Draw(Font face, string value, Vector2 at, int size, Color ink, bool shadowed = true)
    {
        if (string.IsNullOrEmpty(value)) return;
        Font native = At(face, size);
        float spacing = Spacing(size);
        Vector2 pixel = new(MathF.Round(at.X), MathF.Round(at.Y));
        if (shadowed && ink.R + ink.G + ink.B > 180)
        {
            Color shadow = new(8, 11, 18, (ink.A * 175) / 255);
            Raylib.DrawTextEx(native, value, pixel + Vector2.UnitY, size, spacing, shadow);
        }
        Raylib.DrawTextEx(native, value, pixel, size, spacing, ink);
    }

    public static void Unload()
    {
        foreach (Font font in fonts.Values) Raylib.UnloadFont(font);
        fonts.Clear();
        family.Clear();
        files.Clear();
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
