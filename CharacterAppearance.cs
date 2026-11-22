#nullable enable
using System;
using System.IO;
using System.Numerics;
using System.Text.Json;
using Raylib_cs;

namespace HowToVibeCoder;

public readonly record struct AppearancePreset(string NameRu, string NameEn, Color Swatch, string? AssetName = null)
{
    public string DisplayName => Loc.T(NameRu, NameEn);
}

public sealed class CharacterAppearance
{
    public const int SkinCount = 6;
    public const int OutfitCount = 8;
    public const int EyeCount = 8;
    public const int HatCount = 9;

    public static readonly AppearancePreset[] Skins =
    [
        new("Светлая", "Fair", new Color(232, 194, 166, 255)),
        new("Загорелая", "Tanned", new Color(208, 158, 122, 255)),
        new("Смуглая", "Swarthy", new Color(172, 120, 86, 255)),
        new("Оливковая", "Olive", new Color(214, 182, 142, 255)),
        new("Тёмная", "Deep", new Color(112, 74, 52, 255)),
        new("Офисная бледность", "Office Pale", new Color(236, 224, 214, 255))
    ];

    public static readonly AppearancePreset[] Outfits =
    [
        new("Малиновый 90-х", "90s Crimson", new Color(181, 34, 46, 255)),
        new("Чёрный кашемир", "Black Cashmere", new Color(32, 34, 40, 255)),
        new("Синий дипломат", "Diplomat Navy", new Color(35, 75, 142, 255)),
        new("Изумрудный коммерс", "Emerald Founder", new Color(31, 110, 74, 255)),
        new("Белый «Сочи-98»", "Sochi-98 White", new Color(234, 228, 214, 255)),
        new("Бордовый бархат", "Burgundy Velvet", new Color(108, 24, 44, 255)),
        new("Рыжий вельвет", "Rust Corduroy", new Color(166, 86, 38, 255)),
        new("Серый «Совет»", "Boardroom Grey", new Color(96, 102, 112, 255))
    ];

    public static readonly AppearancePreset[] Eyes =
    [
        new("Карие", "Brown", new Color(92, 56, 34, 255)),
        new("Голубые", "Blue", new Color(76, 154, 218, 255)),
        new("Зелёные", "Green", new Color(64, 164, 96, 255)),
        new("Красные", "Bloodshot Red", new Color(218, 52, 48, 255)),
        new("Авиаторы 90-х", "90s Aviators", new Color(196, 156, 74, 255), "eye_aviators"),
        new("Тёмные «Матрица»", "Matrix Shades", new Color(24, 26, 30, 255), "eye_matrix"),
        new("Очки бухгалтера", "Accountant Glasses", new Color(164, 156, 142, 255), "eye_accountant"),
        new("Пиксельные", "Pixel Shades", new Color(32, 32, 36, 255), "eye_dealwithit")
    ];

    public static readonly AppearancePreset[] Hats =
    [
        new("Без шляпы", "No Hat", new Color(96, 84, 70, 255)),
        new("Шапка из фольги", "Tinfoil Hat", new Color(198, 206, 214, 255), "hat_tinfoil"),
        new("Ушанка 90-х", "90s Ushanka", new Color(118, 86, 62, 255), "hat_ushanka"),
        new("Корона Бургер-Босса", "Burger-Boss Crown", new Color(224, 176, 56, 255), "hat_crown"),
        new("Дорожный конус", "Traffic Cone", new Color(234, 102, 28, 255), "hat_traffic_cone"),
        new("Ковбойская шляпа", "Cowboy Hat", new Color(138, 88, 50, 255), "hat_cowboy"),
        new("Колпак повара", "Chef Toque", new Color(242, 238, 230, 255), "hat_chef"),
        new("Пиратская треуголка", "Pirate Tricorne", new Color(46, 36, 34, 255), "hat_pirate"),
        new("Джаба-жаба на голове", "Jaba-Frog on Head", new Color(86, 172, 74, 255), "hat_frog_head")
    ];

    private int _skinIndex;
    private int _outfitIndex;
    private int _eyeIndex;
    private int _hatIndex;

    public int SkinIndex
    {
        get => _skinIndex;
        set => _skinIndex = Math.Clamp(value, 0, SkinCount - 1);
    }

    public int OutfitIndex
    {
        get => _outfitIndex;
        set => _outfitIndex = Math.Clamp(value, 0, OutfitCount - 1);
    }

    public int EyeIndex
    {
        get => _eyeIndex;
        set => _eyeIndex = Math.Clamp(value, 0, EyeCount - 1);
    }

    public int HatIndex
    {
        get => _hatIndex;
        set => _hatIndex = Math.Clamp(value, 0, HatCount - 1);
    }

    public Color SkinColor => Skins[SkinIndex].Swatch;
    public Color OutfitColor => Outfits[OutfitIndex].Swatch;

    public Color LapelColor
    {
        get
        {
            Color c = OutfitColor;
            // Subtle satin-contrast lapel so dark cashmere lapels still read cleanly against the jacket.
            if (OutfitIndex == 1) return new Color(46, 48, 56, 255);
            if (OutfitIndex == 4) return new Color(206, 198, 182, 255);
            return new Color(
                (byte)Math.Clamp((int)MathF.Round(c.R * 0.72f), 0, 255),
                (byte)Math.Clamp((int)MathF.Round(c.G * 0.72f), 0, 255),
                (byte)Math.Clamp((int)MathF.Round(c.B * 0.72f), 0, 255),
                (byte)255);
        }
    }

    public Color EyeIrisColor => EyeIndex < 4 ? Eyes[EyeIndex].Swatch : new Color(84, 54, 34, 255);
    public string? EyewearAsset => Eyes[EyeIndex].AssetName;
    public string? HatAsset => Hats[HatIndex].AssetName;
    public bool HasEyewear => EyewearAsset != null;

    // Linear-calibrated skin swatch for char_partner_90s Finish.Anatomy shader.
    public Color PartnerSkinMaterialColor
    {
        get
        {
            Color s = SkinColor;
            static byte Gamma(byte ch) => (byte)Math.Clamp((int)MathF.Round(MathF.Pow(ch / 255f, 1.55f) * 255f), 0, 255);
            return new Color(Gamma(s.R), Gamma(s.G), Gamma(s.B), (byte)255);
        }
    }

    // Multiplicative tint applied on top of the textured fps_skin hand albedo (exact 1,1,1 for default SkinIndex == 0).
    public Vector3 FpsSkinTint
    {
        get
        {
            if (SkinIndex == 0) return Vector3.One;
            Color s = SkinColor;
            Color baseSkin = Skins[0].Swatch;
            return new Vector3(
                Math.Clamp(s.R / (float)baseSkin.R, 0.25f, 1.35f),
                Math.Clamp(s.G / (float)baseSkin.G, 0.25f, 1.35f),
                Math.Clamp(s.B / (float)baseSkin.B, 0.25f, 1.35f));
        }
    }

    // Linear-space sleeve color for fps_hands_* crimson/lapel materials (null when OutfitIndex == 0 to preserve exact authored default).
    public Color? FpsSleeveMaterialColor(bool isLapel)
    {
        if (OutfitIndex == 0) return null;
        Color s = isLapel ? LapelColor : OutfitColor;
        static byte ToLinear(byte ch) => (byte)Math.Clamp((int)MathF.Round(MathF.Pow(ch / 255f, 2.2f) * 255f), 4, 255);
        return new Color(ToLinear(s.R), ToLinear(s.G), ToLinear(s.B), (byte)255);
    }

    public int Pack() =>
        (SkinIndex & 0xF) | ((OutfitIndex & 0xF) << 4) | ((EyeIndex & 0xF) << 8) | ((HatIndex & 0xF) << 12);

    public static CharacterAppearance Unpack(int packed) => new()
    {
        SkinIndex = packed & 0xF,
        OutfitIndex = (packed >> 4) & 0xF,
        EyeIndex = (packed >> 8) & 0xF,
        HatIndex = (packed >> 12) & 0xF
    };

    public CharacterAppearance Clone() => new()
    {
        SkinIndex = SkinIndex,
        OutfitIndex = OutfitIndex,
        EyeIndex = EyeIndex,
        HatIndex = HatIndex
    };

    public void CopyFrom(CharacterAppearance other)
    {
        SkinIndex = other.SkinIndex;
        OutfitIndex = other.OutfitIndex;
        EyeIndex = other.EyeIndex;
        HatIndex = other.HatIndex;
    }

    public void Randomize(Random? rng = null)
    {
        rng ??= Random.Shared;
        SkinIndex = rng.Next(SkinCount);
        OutfitIndex = rng.Next(OutfitCount);
        EyeIndex = rng.Next(EyeCount);
        // Bias slightly toward actually wearing a fun hat on randomize (8/9 chance to get a hat, or full uniform).
        HatIndex = rng.Next(HatCount);
    }

    // Global local player appearance & disk persistence (%LocalAppData%/HowToVibeCoder/character.json)
    public static CharacterAppearance Current { get; private set; } = new();
    internal static bool SuppressDiskPersistence { get; set; }

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HowToVibeCoder",
        "character.json");

    private sealed class PersistedDto
    {
        public int SkinIndex { get; set; }
        public int OutfitIndex { get; set; }
        public int EyeIndex { get; set; }
        public int HatIndex { get; set; }
    }

    public static void LoadFromDisk(string? explicitPath = null)
    {
        if (explicitPath == null && SuppressDiskPersistence)
        {
            Current = new CharacterAppearance();
            return;
        }
        string path = explicitPath ?? FilePath;
        try
        {
            if (!File.Exists(path))
            {
                Current = new CharacterAppearance();
                return;
            }
            string json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<PersistedDto>(json);
            if (dto == null)
            {
                Current = new CharacterAppearance();
                return;
            }
            Current = new CharacterAppearance
            {
                SkinIndex = dto.SkinIndex,
                OutfitIndex = dto.OutfitIndex,
                EyeIndex = dto.EyeIndex,
                HatIndex = dto.HatIndex
            };
        }
        catch
        {
            Current = new CharacterAppearance();
        }
    }

    public void SaveToDisk(string? explicitPath = null)
    {
        if (explicitPath == null && SuppressDiskPersistence) return;
        string path = explicitPath ?? FilePath;
        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var dto = new PersistedDto
            {
                SkinIndex = SkinIndex,
                OutfitIndex = OutfitIndex,
                EyeIndex = EyeIndex,
                HatIndex = HatIndex
            };
            File.WriteAllText(path, JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
        }
    }
}
