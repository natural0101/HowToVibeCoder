#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using Raylib_cs;

namespace HowToVibeCoder;

public sealed class GameSaveData
{
    public int SlotIndex { get; set; } = 1;
    public string SlotTitle { get; set; } = "Слот 1";
    public string SavedAt { get; set; } = "";
    public int Xp { get; set; } = 412;
    public float Energy { get; set; } = 100;
    public int Hp { get; set; } = 100;
    public int Cash { get; set; } = 250;
    public bool[] UnlockedWeapons { get; set; } = { true, false, false, false };
    public int CurrentWeaponSlot { get; set; } = 0;
    public int EquippedSkinId { get; set; } = -1;
    public int SeatedGalleyCoders { get; set; } = 0;
    public bool HasElevatorKey { get; set; } = false;
    public bool MiniBossDefeated { get; set; } = false;
    public bool ElevatorBossDefeated { get; set; } = false;
    public string FloorProgressLabel { get; set; } = "Этаж 1 - Опенспейс (Cloud Entropic Tower)";
    public float PlayerPosX { get; set; } = 0f;
    public float PlayerPosY { get; set; } = 1.75f;
    public float PlayerPosZ { get; set; } = 9.0f;
}

public sealed class ModEntryInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "v1.0";
    public string Author { get; set; } = "90s Authority Modding Team";
    public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public static class MainMenuAndPauseUI
{
    // Основные флаги состояний
    public static bool IsInMainMenu { get; set; } = true;
    public static bool IsPaused { get; set; } = false;
    public static bool ShouldExitGame { get; set; } = false;

    // Подпанели Главного Меню / Паузы
    public static bool IsSaveLoadPanelOpen { get; set; } = false;
    public static bool IsSaveMode { get; set; } = false; // false = ЗАГРУЗИТЬ ИГРУ, true = СОХРАНИТЬ ИГРУ
    public static bool IsModsPanelOpen { get; set; } = false;
    public static bool IsSettingsPanelOpen { get; set; } = false;
    public static bool IsFullscreenEnabled { get; set; } = false;

    public static bool IsMenuBlockingGameplay =>
        IsInMainMenu || IsPaused || IsSaveLoadPanelOpen || IsModsPanelOpen || IsSettingsPanelOpen || LoreAndMenusUI.IsAnyModalOpen;

    // Пути к сохранениям и модам
    public static string SavesDirectory => Path.Combine(AppContext.BaseDirectory, "saves");
    public static string ModsDirectory => Path.Combine(AppContext.BaseDirectory, "mods");

    // Колбэки интеграции с основным циклом игры
    public static Action? OnStartNewGameRequested { get; set; }
    public static Action? OnStartGameRequested { get; set; }
    public static Action? OnResumeGameRequested { get; set; }
    public static Action? OnReturnToMainMenuRequested { get; set; }
    public static Action? OnExitRequested { get; set; }

    // Делегаты для реального сохранения/загрузки состояния 1-го этажа
    public static Func<GameSaveData>? CaptureCurrentGameState { get; set; }
    public static Action<GameSaveData>? ApplyLoadedGameState { get; set; }

    private static readonly string[] MainMenuButtonLabels =
        ["Создать игру", "Войти в игру", "Персонаж", "Настройки", "Настройки модов", "Создатели", "Выход"];
    private static readonly string[] SettingsTabs = ["Игра", "Графика", "Аудио", "Язык"];
    private static int _settingsTab = 2;
    private static string? _infoPage;
    private static readonly Dictionary<string, Texture2D> Art = new();
    internal static Font Serif;
    internal static readonly Color Edge = new(210, 220, 232, 230);
    internal static void LoadArt(Font serif)
    {
        Serif = serif;
        foreach (string name in new[] { "water", "title", "menu-type", "mixer", "case", "archive" })
        {
            Texture2D texture = Raylib.LoadTexture(Path.Combine(AppContext.BaseDirectory, "assets", "ui", name + ".png"));
            if (texture.Id == 0) throw new IOException("Missing UI texture: " + name);
            Raylib.SetTextureFilter(texture, TextureFilter.Bilinear);
            Art.Add(name, texture);
        }
    }
    internal static void UnloadArt()
    {
        foreach (var texture in Art.Values) Raylib.UnloadTexture(texture);
        Art.Clear();
    }
    internal static void Sprite(string name, Rectangle dest, Rectangle? source = null, Color? tint = null)
    {
        Texture2D texture = Art[name];
        Raylib.DrawTexturePro(texture, source ?? new Rectangle(0, 0, texture.Width, texture.Height),
            dest, Vector2.Zero, 0, tint ?? Color.White);
    }
    internal static void Center(Font font, string value, Rectangle r, float size, Color color)
    {
        float spacing = size * .025f;
        float width = Raylib.MeasureTextEx(font, value, size, spacing).X;
        if (width > r.Width - 6 && width > 0) size *= (r.Width - 6) / width;
        Vector2 dims = Raylib.MeasureTextEx(font, value, size, spacing);
        Vector2 pos = new(r.X + (r.Width - dims.X) / 2, r.Y + (r.Height - dims.Y) / 2);
        Raylib.DrawTextEx(font, value, pos + new Vector2(1, 1), size, spacing, new Color(13, 21, 38, 185));
        Raylib.DrawTextEx(font, value, pos, size, spacing, color);
    }
    internal static void Surface(Rectangle rect, Color fill, float border = 1.5f)
    {
        Raylib.DrawRectangleRounded(rect, .22f, 8, fill);
        if (border > 0) Raylib.DrawRectangleRoundedLinesEx(rect, .22f, 8, border, Edge);
    }
    private static Rectangle MainButton(int i) => new(38, Raylib.GetScreenHeight() - 360 + i * 47, 220, 40);
    private static Rectangle SettingsRect(float x, float y, float w, float h) =>
        new(Raylib.GetScreenWidth() / 2f - 343 + x, Raylib.GetScreenHeight() / 2f - 276 + y, w, h);
    private static Rectangle SettingsTab(int i) => SettingsRect(i * 173, 32, 166, 39);
    private static Rectangle SettingsTrack(int row) => SettingsRect(373, 136 + row * 47 + (row == 3 ? 45 : 0), 172, 20);
    private static readonly int[][] SettingRows = [[4], [5], [0, 1, 2, 3], []];
    private static readonly string[] SettingNames = ["Общая громкость", "Громкость музыки", "Громкость эффектов", "Громкость речи", "Чувствительность мыши", "Поле зрения"];
    private static bool Clicked(Rectangle r) => Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), r);
    private static Rectangle ModMixer => new(Raylib.GetScreenWidth()/2 - 408, Raylib.GetScreenHeight()/2 + 213, 275, 40);

    // 7 кнопок Меню Паузы (по ESC в игре)
    private static readonly string[] PauseButtonLabels =
    {
        "ПРОДОЛЖИТЬ",
        "СОХРАНИТЬ ИГРУ",
        "ЗАГРУЗИТЬ ИГРУ",
        "НАСТРОЙКИ",
        "МОДЫ",
        "В ГЛАВНОЕ МЕНЮ",
        "ВЫХОД"
    };

    private static int _selectedMainIndex = 0;
    private static int _selectedPauseIndex = 0;
    private static int _selectedSaveSlotIndex = 0; // 0=Автосохранение, 1=Слот 1, 2=Слот 2, 3=Слот 3
    private static readonly float[] _mainHoverAnim = new float[7];
    private static readonly float[] _pauseHoverAnim = new float[7];
    private static float _menuClock = 0f;
    private static string _statusMessage = "";
    private static float _statusTimer = 0f;
    private static int _draggingSettingSlider = -1;

    // Кэш слотов сохранений (0 = Автосохранение, 1..3 = Слоты 1..3)
    private static readonly GameSaveData?[] _cachedSlots = new GameSaveData?[4];
    private static bool _initializedStorage = false;

    // Список модов в Менеджере Модов
    public static readonly List<ModEntryInfo> InstalledMods = new()
    {
        new ModEntryInfo
        {
            Id = "htf_knives_90s_skins",
            Name = "HtfKnives & 90s Skins Pack v1.2",
            Version = "v1.2",
            Author = "Workshop 90s Crew",
            Description = "Коллекция из 12 редких скинов оружия 90-х (Золотой УЗИ, Малиновый Обрез, Ремень Авторитета).",
            Enabled = true
        },
        new ModEntryInfo
        {
            Id = "custom_90s_synthwave_voice",
            Name = "Custom 90s Synthwave & Voice Pack v1.0",
            Version = "v1.0",
            Author = "DJ Karina & Petrovich",
            Description = "Процедурный синтвейв-саундтрек 90-х, босс-бит и озвученные реплики коммерса и Вероники.",
            Enabled = true
        },
        new ModEntryInfo
        {
            Id = "lore_leaked_slack_expansion",
            Name = "Lore & Leaked Slack Chats Expansion v1.1",
            Version = "v1.1",
            Author = "Cloud Entropic Insiders",
            Description = "Слитые рабочие чаты вайб-кодеров, дневник HR-Таролога и словарь авторитета внутри игры.",
            Enabled = true
        },
        new ModEntryInfo
        {
            Id = "hardcore_boss_mode",
            Name = "Hardcore Boss Mode",
            Version = "v1.0",
            Author = "Red Jacket Hardcore",
            Description = "Повышенная агрессия Мини-Босса SaaS и Сбежавшего ИИ-Агента v0.1 (+25% к скорости атак).",
            Enabled = false
        }
    };

    public static bool IsHardcoreBossModEnabled =>
        InstalledMods.Exists(m => m.Id == "hardcore_boss_mode" && m.Enabled);

    static MainMenuAndPauseUI()
    {
        EnsureStorageInitialized();
    }

    public static void EnsureStorageInitialized()
    {
        if (_initializedStorage)
            return;

        _initializedStorage = true;
        try
        {
            Directory.CreateDirectory(SavesDirectory);
            Directory.CreateDirectory(ModsDirectory);
            LoadModsConfig();
            RefreshSaveSlotsCache();
        }
        catch
        {
        }
    }

    public static string GetSaveFilePath(int slotIndex)
    {
        return slotIndex == 0
            ? Path.Combine(SavesDirectory, "autosave.json")
            : Path.Combine(SavesDirectory, $"slot{slotIndex}.json");
    }

    public static void RefreshSaveSlotsCache()
    {
        try
        {
            Directory.CreateDirectory(SavesDirectory);
            for (int i = 0; i <= 3; i++)
            {
                string path = GetSaveFilePath(i);
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    _cachedSlots[i] = JsonSerializer.Deserialize<GameSaveData>(json);
                }
                else
                {
                    _cachedSlots[i] = null;
                }
            }
        }
        catch
        {
        }
    }

    public static GameSaveData SaveGameState(int slotIndex, GameSaveData? explicitData = null)
    {
        EnsureStorageInitialized();
        slotIndex = Math.Clamp(slotIndex, 0, 3);

        GameSaveData data = explicitData ?? CaptureCurrentGameState?.Invoke() ?? BuildFallbackSaveData();
        data.SlotIndex = slotIndex;
        data.SlotTitle = slotIndex == 0 ? "Автосохранение" : $"Слот {slotIndex}";
        data.SavedAt = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");

        if (LoreAndMenusUI.LastWonSkin != null)
        {
            data.EquippedSkinId = LoreAndMenusUI.LastWonSkin.Id;
        }

        try
        {
            Directory.CreateDirectory(SavesDirectory);
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetSaveFilePath(slotIndex), json);
            _cachedSlots[slotIndex] = data;
            ShowStatusBanner($"ИГРА СОХРАНЕНА В «{data.SlotTitle.ToUpperInvariant()}» (${data.Cash}, Галера: {data.SeatedGalleyCoders}/4)");
        }
        catch (Exception ex)
        {
            ShowStatusBanner($"Ошибка сохранения: {ex.Message}");
        }

        return data;
    }

    public static GameSaveData? LoadGameState(int slotIndex)
    {
        EnsureStorageInitialized();
        slotIndex = Math.Clamp(slotIndex, 0, 3);
        string path = GetSaveFilePath(slotIndex);

        if (!File.Exists(path))
        {
            ShowStatusBanner($"Слот {(slotIndex == 0 ? "Автосохранение" : slotIndex.ToString())} пока пуст!");
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            GameSaveData? data = JsonSerializer.Deserialize<GameSaveData>(json);
            if (data == null)
                return null;

            _cachedSlots[slotIndex] = data;

            if (data.EquippedSkinId >= 0 && data.EquippedSkinId < LoreAndMenusUI.AllSkins.Length)
            {
                LoreAndMenusUI.AwardAndEquipSkin(LoreAndMenusUI.AllSkins[data.EquippedSkinId]);
            }

            ApplyLoadedGameState?.Invoke(data);

            IsSaveLoadPanelOpen = false;
            IsModsPanelOpen = false;
            IsSettingsPanelOpen = false;
            IsInMainMenu = false;
            IsPaused = false;

            ShowStatusBanner($"ЗАГРУЖЕНО: {data.SlotTitle} (${data.Cash}, {data.FloorProgressLabel})");
            OnStartGameRequested?.Invoke();
            return data;
        }
        catch (Exception ex)
        {
            ShowStatusBanner($"Ошибка чтения сохранения: {ex.Message}");
            return null;
        }
    }

    private static GameSaveData BuildFallbackSaveData()
    {
        int cash = LoreAndMenusUI.GetPlayerMoney != null ? LoreAndMenusUI.GetPlayerMoney() : 250;
        return new GameSaveData
        {
            Cash = cash,
            UnlockedWeapons = new[] { true, true, true, false },
            CurrentWeaponSlot = 0,
            EquippedSkinId = LoreAndMenusUI.LastWonSkin?.Id ?? 0,
            SeatedGalleyCoders = 1,
            HasElevatorKey = cash >= 1000,
            MiniBossDefeated = false,
            ElevatorBossDefeated = false,
            FloorProgressLabel = "Этаж 1 - Опенспейс (Cloud Entropic Tower)"
        };
    }

    private static void SaveModsConfig()
    {
        try
        {
            Directory.CreateDirectory(ModsDirectory);
            string path = Path.Combine(ModsDirectory, "mods_config.json");
            string json = JsonSerializer.Serialize(InstalledMods, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
        }
    }

    private static void LoadModsConfig()
    {
        try
        {
            Directory.CreateDirectory(ModsDirectory);
            string path = Path.Combine(ModsDirectory, "mods_config.json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                List<ModEntryInfo>? loaded = JsonSerializer.Deserialize<List<ModEntryInfo>>(json);
                if (loaded != null && loaded.Count > 0)
                {
                    InstalledMods.Clear();
                    InstalledMods.AddRange(loaded);
                    return;
                }
            }
            SaveModsConfig();
        }
        catch
        {
        }
    }

    private static void ShowStatusBanner(string message)
    {
        _statusMessage = message;
        _statusTimer = 4.0f;
    }

    /// <summary>
    /// Возвращает кинематографичную 3D-камеру, плавно облетающую 1-й этаж небоскрёба:
    /// в кадре центральный стол-ловушка со светящимся Макбуком, будка HR-Таролога Вероники
    /// и северный Бронированный Центральный Лифт.
    /// </summary>
    public static Camera3D GetMainMenuCamera(float time)
    {
        return GetMainMenuCamera(time, new Vector3(0f, 1.35f, -1.5f), 14.2f, 4.4f);
    }

    public static Camera3D GetMainMenuCamera(float time, Vector3 lookCenter, float orbitRadius = 14.2f, float baseHeight = 4.4f)
    {
        float angle = time * 0.24f + 0.65f;
        float radiusWave = orbitRadius + MathF.Sin(time * 0.45f) * 1.6f;

        float camX = MathF.Sin(angle) * radiusWave;
        float camZ = MathF.Cos(angle) * radiusWave;
        float camY = baseHeight + MathF.Sin(time * 0.6f) * 0.75f;

        Vector3 target = new(
            lookCenter.X + MathF.Sin(time * 0.37f) * 2.1f,
            lookCenter.Y + MathF.Cos(time * 0.52f) * 0.35f,
            lookCenter.Z + MathF.Cos(time * 0.29f) * 3.2f
        );

        return new Camera3D
        {
            Position = new Vector3(camX, camY, camZ),
            Target = target,
            Up = Vector3.UnitY,
            FovY = 58f,
            Projection = CameraProjection.Perspective
        };
    }

    public static void DrawMainMenu3DShowcaseExtras(float time)
    {
        float pulse = 0.5f + 0.5f * MathF.Sin(time * 3.2f);
        Color ringGold = new((byte)255, (byte)205, (byte)55, (byte)(110 + (int)(pulse * 90)));
        Color ringCrimson = new((byte)220, (byte)28, (byte)85, (byte)(95 + (int)((1f - pulse) * 90)));

        Raylib.DrawCircle3D(
            new Vector3(0f, 0.08f, 0f),
            2.6f + pulse * 0.25f,
            new Vector3(1f, 0f, 0f),
            90f,
            ringGold);

        Raylib.DrawCircle3D(
            new Vector3(0f, 0.14f, 0f),
            3.35f - pulse * 0.2f,
            new Vector3(1f, 0f, 0f),
            90f,
            ringCrimson);
    }

    public static void StartNewOrResumeFloor()
    {
        IsSaveLoadPanelOpen = false;
        IsModsPanelOpen = false;
        IsSettingsPanelOpen = false;
        LoreAndMenusUI.IsLoreOpen = false;
        LoreAndMenusUI.IsSettingsOpen = false;
        LoreAndMenusUI.IsCaseOpen = false;
        IsInMainMenu = false;
        IsPaused = false;
        PlayUiSound();
        OnStartNewGameRequested?.Invoke();
        OnStartGameRequested?.Invoke();
    }

    public static void ResumeFromPause()
    {
        IsSaveLoadPanelOpen = false;
        IsModsPanelOpen = false;
        IsSettingsPanelOpen = false;
        LoreAndMenusUI.IsLoreOpen = false;
        LoreAndMenusUI.IsSettingsOpen = false;
        LoreAndMenusUI.IsCaseOpen = false;
        IsPaused = false;
        PlayUiSound();
        OnResumeGameRequested?.Invoke();
    }

    public static void OpenMainMenu()
    {
        IsSaveLoadPanelOpen = false;
        IsModsPanelOpen = false;
        IsSettingsPanelOpen = false;
        LoreAndMenusUI.IsLoreOpen = false;
        LoreAndMenusUI.IsSettingsOpen = false;
        LoreAndMenusUI.IsCaseOpen = false;
        IsPaused = false;
        IsInMainMenu = true;
        if (Raylib.IsCursorHidden())
        {
            Raylib.EnableCursor();
        }
        RefreshSaveSlotsCache();
        PlayUiSound();
        OnReturnToMainMenuRequested?.Invoke();
    }

    public static void Update(float dt, ref bool shouldExit)
    {
        Update(dt);
        if (ShouldExitGame)
        {
            shouldExit = true;
        }
    }

    public static void Update(float dt)
    {
        EnsureStorageInitialized();
        _menuClock += dt;

        if (_statusTimer > 0f)
        {
            _statusTimer = MathF.Max(0f, _statusTimer - dt);
        }

        if (LoreAndMenusUI.IsAnyModalOpen) return;

        // Если открыта подпанель Сохранения/Загрузки, Модов или Настроек
        if (IsSaveLoadPanelOpen)
        {
            if (Raylib.IsCursorHidden()) Raylib.EnableCursor();
            UpdateSaveLoadPanelInput();
            return;
        }

        if (IsModsPanelOpen)
        {
            if (Raylib.IsCursorHidden()) Raylib.EnableCursor();
            UpdateModsPanelInput();
            return;
        }

        if (IsSettingsPanelOpen)
        {
            if (Raylib.IsCursorHidden()) Raylib.EnableCursor();
            UpdateSettingsSubPanelInput();
            return;
        }

        if (IsInMainMenu)
        {
            if (_infoPage != null)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(SettingsRect(0, 505, 166, 38))) _infoPage = null;
                return;
            }
            if (Raylib.IsCursorHidden())
            {
                Raylib.EnableCursor();
            }
            UpdateMainMenuInput(dt);
            return;
        }

        // Переключение паузы во время игры по [ESC] или [P]
        if (!LoreAndMenusUI.IsAnyModalOpen &&
            (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.P)))
        {
            IsPaused = !IsPaused;
            if (IsPaused)
            {
                if (Raylib.IsCursorHidden())
                {
                    Raylib.EnableCursor();
                }
                RefreshSaveSlotsCache();
            }
            else
            {
                OnResumeGameRequested?.Invoke();
            }
            PlayUiSound();
            return;
        }

        if (IsPaused)
        {
            if (Raylib.IsCursorHidden())
            {
                Raylib.EnableCursor();
            }
            UpdatePauseMenuInput(dt);
        }
    }

    private static void UpdateMainMenuInput(float dt)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        Vector2 mp = Raylib.GetMousePosition();
        bool mouseClicked = Raylib.IsMouseButtonPressed(MouseButton.Left);

        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
        {
            _selectedMainIndex = (_selectedMainIndex + 1) % MainMenuButtonLabels.Length;
            PlayUiSound();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
        {
            _selectedMainIndex = (_selectedMainIndex - 1 + MainMenuButtonLabels.Length) % MainMenuButtonLabels.Length;
            PlayUiSound();
        }

        for (int i = 0; i < MainMenuButtonLabels.Length; i++)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.One + i) || Raylib.IsKeyPressed(KeyboardKey.Kp1 + i))
            {
                _selectedMainIndex = i;
                ExecuteMainMenuAction(i);
                return;
            }
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter))
        {
            ExecuteMainMenuAction(_selectedMainIndex);
            return;
        }

        for (int i = 0; i < MainMenuButtonLabels.Length; i++)
        {
            Rectangle btnRect = MainButton(i);
            bool hovered = Raylib.CheckCollisionPointRec(mp, btnRect);
            if (hovered)
            {
                if (_selectedMainIndex != i)
                {
                    _selectedMainIndex = i;
                    PlayUiSound();
                }
                if (mouseClicked)
                {
                    ExecuteMainMenuAction(i);
                    return;
                }
            }

            float targetAnim = (hovered || _selectedMainIndex == i) ? 1f : 0f;
            _mainHoverAnim[i] = Lerp(_mainHoverAnim[i], targetAnim, Math.Clamp(dt * 12f, 0f, 1f));
        }
    }

    private static void ExecuteMainMenuAction(int index)
    {
        PlayUiSound();
        switch (index)
        {
            case 0: // НОВАЯ ИГРА
                StartNewOrResumeFloor();
                break;

            case 1: // ЗАГРУЗИТЬ ИГРУ
                RefreshSaveSlotsCache();
                IsSaveMode = false;
                IsSaveLoadPanelOpen = true;
                IsModsPanelOpen = false;
                IsSettingsPanelOpen = false;
                break;

            case 2: _infoPage = "Персонаж"; break;
            case 3: // НАСТРОЙКИ
                IsSettingsPanelOpen = true;
                IsSaveLoadPanelOpen = false;
                IsModsPanelOpen = false;
                break;

            case 4: // МОДЫ
                LoadModsConfig();
                IsModsPanelOpen = true;
                IsSaveLoadPanelOpen = false;
                IsSettingsPanelOpen = false;
                break;

            case 5: _infoPage = "Создатели"; break;
            case 6: // ВЫХОД
                ShouldExitGame = true;
                OnExitRequested?.Invoke();
                break;
        }
    }

    private static void UpdatePauseMenuInput(float dt)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        Vector2 mp = Raylib.GetMousePosition();
        bool mouseClicked = Raylib.IsMouseButtonPressed(MouseButton.Left);

        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
        {
            _selectedPauseIndex = (_selectedPauseIndex + 1) % PauseButtonLabels.Length;
            PlayUiSound();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
        {
            _selectedPauseIndex = (_selectedPauseIndex - 1 + PauseButtonLabels.Length) % PauseButtonLabels.Length;
            PlayUiSound();
        }

        if (Raylib.IsKeyPressed(KeyboardKey.One) || Raylib.IsKeyPressed(KeyboardKey.Kp1)) { ExecutePauseAction(0); return; }
        if (Raylib.IsKeyPressed(KeyboardKey.Two) || Raylib.IsKeyPressed(KeyboardKey.Kp2)) { ExecutePauseAction(1); return; }
        if (Raylib.IsKeyPressed(KeyboardKey.Three) || Raylib.IsKeyPressed(KeyboardKey.Kp3)) { ExecutePauseAction(2); return; }
        if (Raylib.IsKeyPressed(KeyboardKey.Four) || Raylib.IsKeyPressed(KeyboardKey.Kp4)) { ExecutePauseAction(3); return; }
        if (Raylib.IsKeyPressed(KeyboardKey.Five) || Raylib.IsKeyPressed(KeyboardKey.Kp5)) { ExecutePauseAction(4); return; }
        if (Raylib.IsKeyPressed(KeyboardKey.Six) || Raylib.IsKeyPressed(KeyboardKey.Kp6)) { ExecutePauseAction(5); return; }
        if (Raylib.IsKeyPressed(KeyboardKey.Seven) || Raylib.IsKeyPressed(KeyboardKey.Kp7)) { ExecutePauseAction(6); return; }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter))
        {
            ExecutePauseAction(_selectedPauseIndex);
            return;
        }

        int winW = 580;
        int winH = 560;
        int wx = (sw - winW) / 2;
        int wy = (sh - winH) / 2;
        int btnStartX = wx + 44;
        int btnStartY = wy + 116;
        int btnW = winW - 88;
        int btnH = 48;
        int btnGap = 10;

        for (int i = 0; i < PauseButtonLabels.Length; i++)
        {
            Rectangle btnRect = new(btnStartX, btnStartY + i * (btnH + btnGap), btnW, btnH);
            bool hovered = Raylib.CheckCollisionPointRec(mp, btnRect);
            if (hovered)
            {
                if (_selectedPauseIndex != i)
                {
                    _selectedPauseIndex = i;
                    PlayUiSound();
                }
                if (mouseClicked)
                {
                    ExecutePauseAction(i);
                    return;
                }
            }

            float targetAnim = (hovered || _selectedPauseIndex == i) ? 1f : 0f;
            _pauseHoverAnim[i] = Lerp(_pauseHoverAnim[i], targetAnim, Math.Clamp(dt * 12f, 0f, 1f));
        }
    }

    private static void ExecutePauseAction(int index)
    {
        PlayUiSound();
        switch (index)
        {
            case 0: // ПРОДОЛЖИТЬ
                ResumeFromPause();
                break;

            case 1: // СОХРАНИТЬ ИГРУ
                RefreshSaveSlotsCache();
                IsSaveMode = true;
                _selectedSaveSlotIndex = 1;
                IsSaveLoadPanelOpen = true;
                IsModsPanelOpen = false;
                IsSettingsPanelOpen = false;
                break;

            case 2: // ЗАГРУЗИТЬ ИГРУ
                RefreshSaveSlotsCache();
                IsSaveMode = false;
                IsSaveLoadPanelOpen = true;
                IsModsPanelOpen = false;
                IsSettingsPanelOpen = false;
                break;

            case 3: // НАСТРОЙКИ
                IsSettingsPanelOpen = true;
                IsSaveLoadPanelOpen = false;
                IsModsPanelOpen = false;
                break;

            case 4: // МОДЫ
                LoadModsConfig();
                IsModsPanelOpen = true;
                IsSaveLoadPanelOpen = false;
                IsSettingsPanelOpen = false;
                break;

            case 5: // В ГЛАВНОЕ МЕНЮ
                SaveGameState(0); // Автосохранение при выходе в Главное Меню
                OpenMainMenu();
                break;

            case 6: // ВЫХОД
                SaveGameState(0);
                ShouldExitGame = true;
                OnExitRequested?.Invoke();
                break;
        }
    }

    private static void UpdateSaveLoadPanelInput()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            IsSaveLoadPanelOpen = false;
            PlayUiSound();
            return;
        }

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        int winW = 860;
        int winH = 540;
        int wx = (sw - winW) / 2;
        int wy = (sh - winH) / 2;

        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
        {
            _selectedSaveSlotIndex = (_selectedSaveSlotIndex + 1) % 4;
            PlayUiSound();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
        {
            _selectedSaveSlotIndex = (_selectedSaveSlotIndex + 3) % 4;
            PlayUiSound();
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter))
        {
            TriggerSaveOrLoadSlot(_selectedSaveSlotIndex);
            return;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            Vector2 mp = Raylib.GetMousePosition();
            Rectangle closeBtn = new(wx + winW - 160, wy + 14, 140, 38);
            if (Raylib.CheckCollisionPointRec(mp, closeBtn))
            {
                IsSaveLoadPanelOpen = false;
                PlayUiSound();
                return;
            }

            int slotStartY = wy + 92;
            int slotH = 86;
            int slotGap = 12;

            for (int i = 0; i < 4; i++)
            {
                Rectangle slotRect = new(wx + 32, slotStartY + i * (slotH + slotGap), winW - 64, slotH);
                Rectangle actionBtn = new(slotRect.X + slotRect.Width - 190, slotRect.Y + 20, 170, 46);

                if (Raylib.CheckCollisionPointRec(mp, actionBtn) || Raylib.CheckCollisionPointRec(mp, slotRect))
                {
                    _selectedSaveSlotIndex = i;
                    TriggerSaveOrLoadSlot(i);
                    return;
                }
            }
        }
    }

    private static void TriggerSaveOrLoadSlot(int slotIndex)
    {
        PlayUiSound();
        if (IsSaveMode)
        {
            SaveGameState(slotIndex);
        }
        else
        {
            LoadGameState(slotIndex);
        }
    }

    private static void UpdateModsPanelInput()
    {
        if (Clicked(ModMixer)) { LoreAndMenusUI.IsSettingsOpen = true; return; }
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            IsModsPanelOpen = false;
            PlayUiSound();
            return;
        }

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        int winW = 880;
        int winH = 540;
        int wx = (sw - winW) / 2;
        int wy = (sh - winH) / 2;

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            Vector2 mp = Raylib.GetMousePosition();
            Rectangle closeBtn = new(wx + winW - 160, wy + 14, 140, 38);
            if (Raylib.CheckCollisionPointRec(mp, closeBtn))
            {
                IsModsPanelOpen = false;
                PlayUiSound();
                return;
            }

            int rowStartY = wy + 104;
            int rowH = 82;
            int rowGap = 12;

            for (int i = 0; i < InstalledMods.Count; i++)
            {
                Rectangle rowRect = new(wx + 32, rowStartY + i * (rowH + rowGap), winW - 64, rowH);
                Rectangle toggleBtn = new(rowRect.X + rowRect.Width - 156, rowRect.Y + 18, 136, 44);
                if (Raylib.CheckCollisionPointRec(mp, toggleBtn) || Raylib.CheckCollisionPointRec(mp, rowRect))
                {
                    InstalledMods[i].Enabled = !InstalledMods[i].Enabled;
                    SaveModsConfig();
                    PlayUiSound();
                    ShowStatusBanner($"Мод «{InstalledMods[i].Name}»: {(InstalledMods[i].Enabled ? "ВКЛЮЧЕН" : "ВЫКЛЮЧЕН")}");
                    return;
                }
            }
        }
    }

    private static void UpdateSettingsSubPanelInput()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(SettingsRect(0, 550, 166, 38)))
        {
            IsSettingsPanelOpen = false;
            _draggingSettingSlider = -1;
            return;
        }
        for (int i = 0; i < SettingsTabs.Length; i++)
            if (Clicked(SettingsTab(i))) { _settingsTab = i; _draggingSettingSlider = -1; }
        if (_settingsTab == 1 && Clicked(SettingsRect(337, 186, 279, 34)))
        {
            Raylib.ToggleFullscreen();
            IsFullscreenEnabled = Raylib.IsWindowFullscreen();
        }
        if (_settingsTab == 2 && Clicked(SettingsRect(140, 478, 406, 36))) LoreAndMenusUI.IsSettingsOpen = true;
        if (!Raylib.IsMouseButtonDown(MouseButton.Left)) _draggingSettingSlider = -1;
        int[] rows = SettingRows[_settingsTab];
        for (int i = 0; i < rows.Length; i++)
        {
            Rectangle r = SettingsTrack(i);
            if (Clicked(r)) _draggingSettingSlider = rows[i];
            if (_draggingSettingSlider == rows[i]) ApplySettingValue(rows[i], (Raylib.GetMouseX() - r.X) / r.Width);
        }
    }

    private static void ApplySettingValue(int index, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        switch (index)
        {
            case 0:
                LoreAndMenusUI.MasterVolume = t;
                AudioMusicSystem.MasterVolume = t;
                if (Raylib.IsAudioDeviceReady()) Raylib.SetMasterVolume(t);
                break;
            case 1:
                LoreAndMenusUI.MusicVolume = t;
                AudioMusicSystem.MusicVolume = t;
                break;
            case 2:
                LoreAndMenusUI.WeaponSfxVolume = t;
                AudioMusicSystem.WeaponVolume = t;
                break;
            case 3:
                LoreAndMenusUI.VoiceVolume = t;
                AudioMusicSystem.VoiceVolume = t;
                break;
            case 4:
                LoreAndMenusUI.MouseSensitivity = MathF.Round(0.05f + t * 0.45f, 3);
                break;
            case 5:
                LoreAndMenusUI.CameraFov = MathF.Round(60f + t * 40f, 0);
                break;
        }
    }

    public static void Draw(Font font)
    {
        Draw(font, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
    }

    public static void Draw(Font font, int screenW, int screenH)
    {
        if (IsInMainMenu)
        {
            DrawMainMenu(font, screenW, screenH);
        }
        else if (IsPaused)
        {
            DrawPauseOverlay(font, screenW, screenH);
        }

        if (IsSaveLoadPanelOpen)
        {
            DrawSaveLoadSubPanel(font, screenW, screenH);
        }
        else if (IsModsPanelOpen)
        {
            DrawModsSubPanel(font, screenW, screenH);
        }
        else if (IsSettingsPanelOpen)
        {
            DrawSettingsSubPanel(font, screenW, screenH);
        }

        if (_statusTimer > 0f && !string.IsNullOrEmpty(_statusMessage))
        {
            DrawStatusToast(font, screenW, _statusMessage);
        }
    }

    public static void DrawMainMenu(Font font, int screenW, int screenH)
    {
        Sprite("water", new(0, 0, screenW, screenH));
        float scale = Math.Min(screenW / 1280f, screenH / 720f);
        Rectangle title = new(26, 37, 580 * scale, 310 * scale);
        Sprite("title", new(title.X+3, title.Y+4, title.Width, title.Height), tint: new Color(15,28,45,115));
        Sprite("title", title, tint: new Color(245,247,249,255));
        for (int i = 0; i < MainMenuButtonLabels.Length; i++)
        {
            Rectangle r = MainButton(i);
            Surface(r, i == _selectedMainIndex ? new Color(76,111,149,193) : new Color(28,49,81,138));
            Sprite("menu-type", new(r.X+10,r.Y+3,200,34), new(0,i*68,400,68));
            if (i == 2)
            {
                Vector2 c = new(r.X+r.Width-12, r.Y+7);
                for (int j = 0; j < 16; j++)
                {
                    Vector2 Tip(int k) => c + Vector2.Transform(new Vector2(k%2==0 ? 17 : 11,0), Matrix3x2.CreateRotation(k*MathF.PI/8));
                    Raylib.DrawTriangle(c, Tip(j+1), Tip(j), new Color(246, 92, 13, 255));
                }
                Center(Serif, "!", new(c.X-10,c.Y-12,20,23), 23, Color.White);
            }
        }
        Center(Serif,"Подключение к Steam...",new(screenW-257,6,250,33),23,Edge);
        Center(Serif,"Версия 1.1.3",new(screenW-112,screenH-28,110,25),17,Edge);
        Center(Serif,"Присоединиться к Discord",new(263,screenH-60,257,32),22,Edge);
        if (_infoPage != null)
        {
            Raylib.DrawRectangle(0,0,screenW,screenH,new Color(5,10,24,134));
            Surface(SettingsRect(0,88,686,400),new Color(42,71,108,250));
            Center(Serif,_infoPage,SettingsRect(30,114,626,68),44,Color.White);
            string body = _infoPage == "Персонаж" ? "Коммерс в малиновом пиджаке" : "How to Vibe Coder";
            Center(Serif,body,SettingsRect(40,220,606,60),32,Edge);
            Center(Serif,_infoPage == "Персонаж" ? LoreAndMenusUI.EquippedBeltSkinName : "Создано на raylib и .NET",SettingsRect(40,280,606,48),23,Edge);
            NativeButton("Назад", SettingsRect(0,505,166,38));
        }
    }

    private static void NativeButton(string label, Rectangle r, bool selected = false)
    {
        Surface(r, selected ? new Color(84,125,165,250) : new Color(42,69,109,245));
        Center(Serif,label,r,23,Color.White);
    }

    public static void DrawPauseOverlay(Font font, int screenW, int screenH)
    {
        Raylib.DrawRectangle(0, 0, screenW, screenH, new Color(8, 6, 16, 198));

        int winW = 580;
        int winH = 560;
        int wx = (screenW - winW) / 2;
        int wy = (screenH - winH) / 2;

        Rectangle winRect = new(wx, wy, winW, winH);
        Raylib.DrawRectangle(wx + 8, wy + 10, winW, winH, new Color(0, 0, 0, 185));
        Raylib.DrawRectangleRec(winRect, new Color(18, 14, 28, 248));
        Raylib.DrawRectangle(wx, wy, winW, 74, new Color(138, 18, 54, 245));

        Raylib.DrawRectangleLinesEx(winRect, 3f, new Color(235, 36, 92, 255));
        Raylib.DrawRectangleLinesEx(new Rectangle(wx + 5, wy + 5, winW - 10, winH - 10), 1.5f, new Color(255, 210, 72, 200));

        DrawTextCrisp(
            font,
            "ПАУЗА // МЕНЮ ИГРЫ",
            wx + 32,
            wy + 16,
            26,
            new Color(255, 232, 92, 255));
        DrawTextCrisp(
            font,
            "Cloud Entropic Tower - Этаж 1 (Нажми [ESC] для возврата в игру)",
            wx + 34,
            wy + 46,
            14,
            new Color(245, 220, 235, 240));

        int btnStartX = wx + 44;
        int btnStartY = wy + 116;
        int btnW = winW - 88;
        int btnH = 48;
        int btnGap = 10;

        for (int i = 0; i < PauseButtonLabels.Length; i++)
        {
            float hover = _pauseHoverAnim[i];
            int shiftX = (int)(hover * 6f);
            Rectangle btnRect = new(btnStartX + shiftX, btnStartY + i * (btnH + btnGap), btnW - shiftX, btnH);
            bool isSelected = _selectedPauseIndex == i;

            Color baseBg = i == 0
                ? new Color(142, 20, 56, 245)
                : new Color(28, 24, 44, 242);
            Color hoverBg = i == 0
                ? new Color(210, 34, 82, 255)
                : new Color(68, 30, 66, 252);

            Color bg = LerpColor(baseBg, hoverBg, hover);
            Color border = isSelected
                ? new Color(255, 222, 75, 255)
                : new Color(102, 86, 138, 220);

            Raylib.DrawRectangleRec(btnRect, bg);
            Raylib.DrawRectangle((int)btnRect.X, (int)btnRect.Y, isSelected ? 8 : 4, (int)btnRect.Height, border);
            Raylib.DrawRectangleLinesEx(btnRect, isSelected ? 2.5f : 1.5f, border);

            DrawTextCrisp(
                font,
                PauseButtonLabels[i],
                (int)btnRect.X + 22,
                (int)btnRect.Y + 14,
                19,
                isSelected ? new Color(255, 245, 185, 255) : Color.White);

            DrawTextCrisp(
                font,
                $"[{i + 1}]",
                (int)(btnRect.X + btnRect.Width - 46),
                (int)btnRect.Y + 15,
                16,
                new Color(255, 215, 82, 240));
        }
    }

    private static void DrawSaveLoadSubPanel(Font font, int screenW, int screenH)
    {
        Raylib.DrawRectangle(0, 0, screenW, screenH, new Color(6, 5, 14, 215));

        int winW = 860;
        int winH = 540;
        int wx = (screenW - winW) / 2;
        int wy = (screenH - winH) / 2;

        Rectangle winRect = new(wx, wy, winW, winH);
        Raylib.DrawRectangleRec(winRect, new Color(16, 14, 28, 252));
        Raylib.DrawRectangle(wx, wy, winW, 64, new Color(138, 18, 54, 250));
        Raylib.DrawRectangleLinesEx(winRect, 3f, new Color(255, 212, 68, 255));

        string header = IsSaveMode
            ? "СОХРАНИТЬ ИГРУ В СЛОТ (SAVES/SLOT1..3.JSON)"
            : "ЗАГРУЗИТЬ ИГРУ (ВЫБЕРИ СЛОТ ИЛИ АВТОСОХРАНЕНИЕ)";
        DrawTextCrisp(font, header, wx + 28, wy + 20, 22, new Color(255, 232, 92, 255));

        Rectangle closeBtn = new(wx + winW - 160, wy + 14, 140, 38);
        Raylib.DrawRectangleRec(closeBtn, new Color(82, 18, 42, 250));
        Raylib.DrawRectangleLinesEx(closeBtn, 2f, new Color(255, 195, 95, 245));
        DrawTextCrisp(font, "[ НАЗАД / ESC ]", (int)closeBtn.X + 12, (int)closeBtn.Y + 10, 15, Color.White);

        int slotStartY = wy + 92;
        int slotH = 86;
        int slotGap = 12;

        for (int i = 0; i < 4; i++)
        {
            string slotTitle = i == 0 ? "АВТОСОХРАНЕНИЕ (saves/autosave.json)" : $"СЛОТ {i} (saves/slot{i}.json)";
            GameSaveData? slot = _cachedSlots[i];
            bool selected = _selectedSaveSlotIndex == i;

            Rectangle slotRect = new(wx + 32, slotStartY + i * (slotH + slotGap), winW - 64, slotH);
            Raylib.DrawRectangleRec(slotRect, selected ? new Color(46, 26, 56, 250) : new Color(24, 22, 38, 245));
            Raylib.DrawRectangleLinesEx(slotRect, selected ? 2.5f : 1.5f, selected ? new Color(255, 218, 72, 255) : new Color(95, 85, 130, 215));

            DrawTextCrisp(font, slotTitle, (int)slotRect.X + 18, (int)slotRect.Y + 10, 17, new Color(255, 222, 92, 255));

            if (slot != null)
            {
                int weaponsCount = 0;
                if (slot.UnlockedWeapons != null)
                {
                    foreach (bool u in slot.UnlockedWeapons) if (u) weaponsCount++;
                }

                string details1 = $"Дата: {slot.SavedAt}   |   Кэш: ${slot.Cash}   |   Оружия открыто: {weaponsCount}/4   |   Кодеров на Галере: {slot.SeatedGalleyCoders}/4";
                string details2 = $"Прогресс: {slot.FloorProgressLabel}   |   Ключ Лифта: {(slot.HasElevatorKey ? "ЕСТЬ" : "НЕТ")}   |   Босс Этажа: {(slot.ElevatorBossDefeated ? "ПОВЕРЖЕН" : "ЖИВ")}";
                DrawTextCrisp(font, details1, (int)slotRect.X + 18, (int)slotRect.Y + 35, 14, Color.White);
                DrawTextCrisp(font, details2, (int)slotRect.X + 18, (int)slotRect.Y + 57, 13, new Color(165, 225, 255, 240));
            }
            else
            {
                DrawTextCrisp(font, "ПУСТОЙ СЛОТ - Нет записанных данных в saves/", (int)slotRect.X + 18, (int)slotRect.Y + 42, 15, new Color(155, 155, 175, 220));
            }

            Rectangle actionBtn = new(slotRect.X + slotRect.Width - 190, slotRect.Y + 20, 170, 46);
            Color actBg = IsSaveMode ? new Color(185, 28, 74, 250) : (slot != null ? new Color(32, 138, 92, 250) : new Color(65, 65, 80, 220));
            Raylib.DrawRectangleRec(actionBtn, actBg);
            Raylib.DrawRectangleLinesEx(actionBtn, 2f, new Color(255, 225, 95, 240));
            string btnText = IsSaveMode ? "[ СОХРАНИТЬ ]" : "[ ЗАГРУЗИТЬ ]";
            DrawTextCrisp(font, btnText, (int)actionBtn.X + 22, (int)actionBtn.Y + 14, 16, Color.White);
        }

        DrawTextCrisp(
            font,
            $"Папка сохранений на диске: {SavesDirectory}",
            wx + 32,
            wy + winH - 36,
            14,
            new Color(200, 210, 230, 230));
    }

    private static void DrawModsSubPanel(Font font, int screenW, int screenH)
    {
        Raylib.DrawRectangle(0, 0, screenW, screenH, new Color(6, 5, 14, 215));

        int winW = 880;
        int winH = 540;
        int wx = (screenW - winW) / 2;
        int wy = (screenH - winH) / 2;

        Rectangle winRect = new(wx, wy, winW, winH);
        Raylib.DrawRectangleRec(winRect, new Color(16, 14, 28, 252));
        Raylib.DrawRectangle(wx, wy, winW, 64, new Color(138, 18, 54, 250));
        Raylib.DrawRectangleLinesEx(winRect, 3f, new Color(255, 212, 68, 255));

        DrawTextCrisp(
            font,
            "МЕНЕДЖЕР МОДОВ // ПОДКЛЮЧЁННЫЕ МОДУЛИ (MODS/)",
            wx + 28,
            wy + 20,
            22,
            new Color(255, 232, 92, 255));

        Rectangle closeBtn = new(wx + winW - 160, wy + 14, 140, 38);
        Raylib.DrawRectangleRec(closeBtn, new Color(82, 18, 42, 250));
        Raylib.DrawRectangleLinesEx(closeBtn, 2f, new Color(255, 195, 95, 245));
        DrawTextCrisp(font, "[ НАЗАД / ESC ]", (int)closeBtn.X + 12, (int)closeBtn.Y + 10, 15, Color.White);

        DrawTextCrisp(
            font,
            "Кликни по любому моду для переключения [ВКЛ / ВЫКЛ] (конфигурация сохраняется в mods/mods_config.json):",
            wx + 32,
            wy + 76,
            14,
            new Color(210, 225, 245, 240));

        int rowStartY = wy + 104;
        int rowH = 82;
        int rowGap = 12;

        for (int i = 0; i < InstalledMods.Count; i++)
        {
            ModEntryInfo mod = InstalledMods[i];
            Rectangle rowRect = new(wx + 32, rowStartY + i * (rowH + rowGap), winW - 64, rowH);

            Color rowBg = mod.Enabled ? new Color(24, 34, 46, 248) : new Color(24, 20, 32, 240);
            Color border = mod.Enabled ? new Color(68, 225, 145, 245) : new Color(110, 95, 130, 200);

            Raylib.DrawRectangleRec(rowRect, rowBg);
            Raylib.DrawRectangle((int)rowRect.X, (int)rowRect.Y, 7, (int)rowRect.Height, border);
            Raylib.DrawRectangleLinesEx(rowRect, 2f, border);

            DrawTextCrisp(font, $"{mod.Name}  ({mod.Version})", (int)rowRect.X + 18, (int)rowRect.Y + 10, 18, new Color(255, 230, 98, 255));
            DrawTextCrisp(font, $"Автор: {mod.Author}", (int)rowRect.X + 18, (int)rowRect.Y + 34, 13, new Color(165, 215, 255, 235));
            DrawTextCrisp(font, mod.Description, (int)rowRect.X + 18, (int)rowRect.Y + 54, 14, Color.White);

            Rectangle toggleBtn = new(rowRect.X + rowRect.Width - 156, rowRect.Y + 18, 136, 44);
            Color tBg = mod.Enabled ? new Color(28, 155, 88, 250) : new Color(128, 32, 52, 245);
            Raylib.DrawRectangleRec(toggleBtn, tBg);
            Raylib.DrawRectangleLinesEx(toggleBtn, 2f, Color.White);
            DrawTextCrisp(
                font,
                mod.Enabled ? "[ ВКЛ ]" : "[ ВЫКЛ ]",
                (int)toggleBtn.X + (mod.Enabled ? 34 : 28),
                (int)toggleBtn.Y + 13,
                17,
                Color.White);
        }

        NativeButton("Звуковая рубка [F8]", ModMixer);
    }

    private static void DrawSettingsSubPanel(Font font, int screenW, int screenH)
    {
        Raylib.DrawRectangle(0,0,screenW,screenH,new Color(4,9,22,136));
        Center(Serif,"Настройки",SettingsRect(0,-26,686,54),48,Color.White);
        for (int i=0;i<4;i++) NativeButton(SettingsTabs[i],SettingsTab(i),i==_settingsTab);
        Rectangle panel = SettingsRect(0,80,686,462);
        Surface(panel,new Color(44,72,110,246));
        Raylib.DrawRectangleGradientV((int)panel.X+3,(int)panel.Y+3,680,456,new Color(100,158,195,141),new Color(22,34,78,190));
        Center(Serif,SettingsTabs[_settingsTab],SettingsRect(0,84,686,37),27,Color.White);
        int[] rows = SettingRows[_settingsTab];
        for (int i=0;i<rows.Length;i++)
        {
            int id = rows[i];
            float t = LoreAndMenusUI.GetSliderNormalizedValue(id);
            Rectangle r = SettingsTrack(i);
            float y = r.Y+r.Height/2;
            DrawTextCrisp(Serif,SettingNames[id],(int)panel.X+67,(int)y-13,23,Color.White);
            Raylib.DrawLineEx(new(r.X,y),new(r.X+r.Width,y),1.7f,Edge);
            float x=r.X+t*r.Width;
            Raylib.DrawRectanglePro(new(x,y,10,17),new(5,8),39,Color.White);
            Raylib.DrawLineEx(new(x+3,y-4),new(x+9,y-11),3,Color.White);
            float value = id == 4 ? LoreAndMenusUI.MouseSensitivity : id == 5 ? LoreAndMenusUI.CameraFov : t;
            Rectangle val = new(r.X+r.Width+32, y-17,68,34);
            Surface(val,new Color(35,63,99,112),0);
            Center(Serif,value.ToString(id==5?"0":"0.00",System.Globalization.CultureInfo.GetCultureInfo("ru-RU")),val,22,Edge);
        }
        if (_settingsTab==1) NativeButton(IsFullscreenEnabled ? "Полный экран: вкл" : "Полный экран: выкл",SettingsRect(337,186,279,34));
        if (_settingsTab==2)
        {
            Center(Serif,"Голоса персонажей",SettingsRect(0,257,686,42),26,Color.White);
            NativeButton("Звуковая рубка [F8]",SettingsRect(140,478,406,36));
        }
        if (_settingsTab==3) Center(Serif,"Русский",SettingsRect(0,165,686,54),30,Color.White);
        NativeButton("Назад",SettingsRect(0,550,166,38));
    }

    private static void DrawStatusToast(Font font, int screenW, string message)
    {
        int w = Math.Min(screenW - 80, 760);
        int x = (screenW - w) / 2;
        int y = 44;
        Raylib.DrawRectangle(x, y, w, 38, new Color(22, 96, 64, 245));
        Raylib.DrawRectangleLinesEx(new Rectangle(x, y, w, 38), 2f, new Color(255, 225, 88, 255));
        DrawTextCrisp(font, message, x + 18, y + 10, 16, Color.White);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static Color LerpColor(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t),
            (byte)(a.A + (b.A - a.A) * t));
    }

    private static void PlayUiSound()
    {
        try
        {
            AudioMusicSystem.PlayCaseTick();
        }
        catch
        {
        }
    }

    private static void DrawTextCrisp(Font font, string text, int x, int y, int size, Color color)
    {
        Vector2 shadowPos = new(x + 1.5f, y + 1.5f);
        Vector2 mainPos = new(x, y);
        Color shadow = new((byte)0, (byte)0, (byte)0, (byte)Math.Min(230, (int)color.A));

        if (font.Texture.Id != 0)
        {
            Raylib.DrawTextEx(font, text, shadowPos, size, 1f, shadow);
            Raylib.DrawTextEx(font, text, mainPos, size, 1f, color);
        }
        else
        {
            Raylib.DrawText(text, x + 1, y + 1, size, shadow);
            Raylib.DrawText(text, x, y, size, color);
        }
    }
}
