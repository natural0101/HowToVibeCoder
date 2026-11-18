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
        .Concat("№«»—–…→←₽°♥×★▶◀“”‘’".Select(c => (int)c)).Distinct().ToArray();

    public static void Load()
    {
        string directory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        string[] names = ["segoeui.ttf", "segoeuib.ttf", "georgia.ttf", "georgiab.ttf"];
        foreach (Typeface face in Enum.GetValues<Typeface>())
        {
            files[face] = Path.Combine(directory, names[(int)face]);
            foreach (int size in new[] {14,16,18,20,22,24,28,36,48,72}) Get(face, size);
        }
    }

    public static Font Get(Typeface face, int size)
    {
        if (fonts.TryGetValue((face, size), out Font font)) return font;
        int[] glyphSet = size == 260 ? ['0','1','?'] : codepoints;
        font = Raylib.LoadFontEx(files[face],size,glyphSet,glyphSet.Length);
        if (font.Texture.Id == 0 || font.Texture.Id == Raylib.GetFontDefault().Texture.Id || font.BaseSize != size)
            throw new InvalidDataException($"Cannot load {face} at {size}px");
        Raylib.SetTextureFilter(font.Texture, TextureFilter.Bilinear);
        fonts.Add((face,size), font);
        family.Add(font.Texture.Id, face);
        return font;
    }

    public static Font At(Font face, int size) => Get(family.GetValueOrDefault(face.Texture.Id), size);

    public static Vector2 Measure(Font face, string value, int size) =>
        Raylib.MeasureTextEx(At(face, size), value, size, 0);

    public static void Draw(Font face, string value, Vector2 at, int size, Color ink)
    {
        if (string.IsNullOrEmpty(value)) return;
        Font native = At(face, size);
        Vector2 pixel = new(MathF.Round(at.X), MathF.Round(at.Y));
        Raylib.DrawTextEx(native,value,pixel + new Vector2(1,2),size,0,new Color(12,16,24,ink.A / 5));
        Raylib.DrawTextEx(native,value,pixel + Vector2.UnitY,size,0,new Color(12,16,24,ink.A / 2));
        Raylib.DrawTextEx(native,value,pixel,size,0,ink);
    }

    public static void Unload()
    {
        foreach (Font font in fonts.Values) Raylib.UnloadFont(font);
        fonts.Clear();
        family.Clear();
        files.Clear();
    }
}

