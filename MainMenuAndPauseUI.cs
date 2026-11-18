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
    internal static readonly CoopNetworkSystem Coop = new();
    internal static Action? SummonPartner;
    private static int coopFocus;
    private static string address = "127.0.0.1:27094";
    private static bool addressEdited, selectAddress;
    internal static bool JoinPanel => IsInMainMenu && IsSaveLoadPanelOpen && !IsSaveMode;
    internal static void OpenJoinPanel()
    {
        RefreshSaveSlotsCache();
        IsSaveMode=false; IsSaveLoadPanelOpen=true;
        IsModsPanelOpen=IsSettingsPanelOpen=false;
        coopFocus=0; addressEdited=false; selectAddress=true;
        Coop.Discover();
    }
    private static int _settingsTab = 2;
    private static string? _infoPage;
    private static readonly Dictionary<string, Texture2D> Art = new();
    internal static Font Serif;
    internal static readonly Color Edge = new(210, 220, 232, 230);
    internal static void LoadArt(Font serif)
    {
        Serif = serif;
        foreach (string name in new[] { "archive" })
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
        int pixels = Math.Max(1,(int)MathF.Round(size));
        Vector2 dims = GameFonts.Measure(font,value,pixels);
        while (pixels > 1 && (dims.X > r.Width - 4 || dims.Y > r.Height))
            dims = GameFonts.Measure(font,value,--pixels);
        GameFonts.Draw(font,value,new(r.X+(r.Width-dims.X)/2,r.Y+(r.Height-dims.Y)/2),pixels,color);
    }
    internal static void Surface(Rectangle rect, Color fill, float border = 1.5f)
    {
        float rounding = Math.Min(.22f, 10f / Math.Min(rect.Width,rect.Height));
        Raylib.DrawRectangleRounded(rect, rounding, 8, fill);
        if (border > 0) Raylib.DrawRectangleRoundedLinesEx(rect, rounding, 8, border, Edge);
    }
    private static Rectangle MainButton(int i) => new(56, Raylib.GetScreenHeight() - 370 + i * 45, 310, 36);
    private static Rectangle SettingsRect(float x, float y, float w, float h) =>
        new(Raylib.GetScreenWidth() / 2f - 343 + x, Raylib.GetScreenHeight() / 2f - 276 + y, w, h);
    private static Rectangle SettingsTab(int i) => SettingsRect(i * 173, 32, 166, 39);
    private static Rectangle SettingsTrack(int row) => SettingsRect(373, 136 + row * 47 + (row == 3 ? 45 : 0), 172, 20);
    private static readonly int[][] SettingRows = [[4], [5], [0, 1, 2, 3], []];
    private static readonly string[] SettingNames = ["Общая громкость", "Громкость музыки", "Громкость эффектов", "Громкость речи", "Чувствительность мыши", "Поле зрения"];
    private static bool Clicked(Rectangle r) => Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), r);
    private static Rectangle ModAction(int i) => new(Raylib.GetScreenWidth()/2 - 95 + i*249, Raylib.GetScreenHeight()/2 + 213, 240, 40);
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

            ApplyLoadedGameState?.Invoke(data);
            if (data.EquippedSkinId >= 0 && data.EquippedSkinId < LoreAndMenusUI.AllSkins.Length)
            {
                LoreAndMenusUI.AwardAndEquipSkin(LoreAndMenusUI.AllSkins[data.EquippedSkinId]);
            }

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

    internal static void Notify(string message) => ShowStatusBanner(message);

    private static void ShowStatusBanner(string message)
    {
        _statusMessage = message;
        _statusTimer = 4.0f;
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
            if (hovered && (mouseClicked || Raylib.GetMouseDelta().LengthSquared() > 0))
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
                OpenJoinPanel();
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
            if (hovered && (mouseClicked || Raylib.GetMouseDelta().LengthSquared() > 0))
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

    private static Rectangle CoopRow(int row)
    {
        float x=Raylib.GetScreenWidth()/2-388, y=Raylib.GetScreenHeight()/2-270;
        return row switch {
            0 => new(x,y+100,776,42), 1 => new(x,y+156,776,44), 2 => new(x,y+210,776,44),
            _ => new(x,y+324+(row-3)*56,776,48)
        };
    }

    private static void UpdateJoinPanel()
    {
        Rectangle back=new(Raylib.GetScreenWidth()/2+290,Raylib.GetScreenHeight()/2-256,118,38);
        if(Clicked(back) || Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            Coop.Dispose(); IsSaveLoadPanelOpen=false; return;
        }
        if(!addressEdited && Coop.DiscoveredAddress!=null) address=Coop.DiscoveredAddress;
        bool activate=Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter);
        if(Raylib.IsKeyPressed(KeyboardKey.Tab) || Raylib.IsKeyPressed(KeyboardKey.Down)) coopFocus=(coopFocus+1)%6;
        if(Raylib.IsKeyPressed(KeyboardKey.Up)) coopFocus=(coopFocus+5)%6;
        for(int i=0;i<6;i++)
            if(Clicked(CoopRow(i))) { coopFocus=i; selectAddress=i==0; activate=i!=0; }
        if(coopFocus==0)
        {
            bool ctrl=Raylib.IsKeyDown(KeyboardKey.LeftControl)||Raylib.IsKeyDown(KeyboardKey.RightControl);
            if(ctrl && Raylib.IsKeyPressed(KeyboardKey.A)) selectAddress=true;
            if(ctrl && Raylib.IsKeyPressed(KeyboardKey.V))
            {
                address=new string((Raylib.GetClipboardText_()??"").Trim().Where(c=>char.IsAsciiDigit(c)||c=='.'||c==':').Take(32).ToArray());
                selectAddress=false; addressEdited=true;
            }
            if(Raylib.IsKeyPressed(KeyboardKey.Backspace) || Raylib.IsKeyPressedRepeat(KeyboardKey.Backspace))
            {
                address=selectAddress || address.Length==0 ? "" : address[..^1];
                selectAddress=false; addressEdited=true;
            }
            for(int key=Raylib.GetCharPressed();key!=0;key=Raylib.GetCharPressed())
                if(key is >=48 and <=57 or 46 or 58)
                {
                    if(selectAddress) address="";
                    if(address.Length<32) address+=(char)key;
                    selectAddress=false; addressEdited=true;
                }
        }
        if(!activate) return;
        switch(coopFocus)
        {
            case 0: case 1: Coop.Join(address.Trim()); break;
            case 2: StartNewOrResumeFloor(); SummonPartner?.Invoke(); break;
            default: LoadGameState(coopFocus-2); break;
        }
    }

    private static void DrawJoinPanel(Font font)
    {
        int x=Raylib.GetScreenWidth()/2-388,y=Raylib.GetScreenHeight()/2-270;
        DrawSlateWindow("КООПЕРАТИВ (ВОЙТИ К ДРУГУ) / ЗАГРУЗКА",860,540);
        NativeButton("Назад",new(x+678,y+14,118,38));
        DrawTextCrisp(font,"Адрес друга (IPv4:порт)",x,y+70,18,Edge);
        var field=CoopRow(0);
        Surface(field,coopFocus==0 ? new Color(34,53,77,255) : new Color(26,41,63,255));
        DrawTextCrisp(font,address+(coopFocus==0 && (_menuClock%1)<.55f ? "|" : ""),x+15,(int)field.Y+8,23,Color.White);
        NativeButton("ПОДКЛЮЧИТЬСЯ К ИГРЕ ДРУГА",CoopRow(1),coopFocus==1);
        NativeButton("ВЫЗВАТЬ БРАТАНА В КООП (НАПАРНИК)",CoopRow(2),coopFocus==2);
        DrawTextCrisp(font,Coop.Status,x,y+263,16,Edge);
        DrawTextCrisp(Serif,"Загрузка сохранения",x,y+292,22,Color.White);
        for(int i=1;i<=3;i++)
        {
            var slot=_cachedSlots[i];
            string detail=slot==null ? "Пусто" : $"{slot.SavedAt}     ${slot.Cash}";
            NativeButton($"Слот {i}     {detail}",CoopRow(i+2),coopFocus==i+2);
        }
    }

    private static void UpdateSaveLoadPanelInput()
    {
        if(JoinPanel) { UpdateJoinPanel(); return; }
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
        if (Clicked(ModAction(0))) { LoreAndMenusUI.TriggerGrantTestMoney(); return; }
        if (Clicked(ModAction(1))) { LoreAndMenusUI.TriggerUnlockAllWeapons(); return; }
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

        if (LoreAndMenusUI.IsAnyModalOpen) return;
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
        // Original vector skyline: Cloud Entropic Tower, midnight, 1994 / 2026.
        Raylib.DrawRectangleGradientV(0,0,screenW,screenH,new Color(8,14,27,255),new Color(32,17,40,255));
        float scale=Math.Min(screenW/1280f,screenH/720f);
        for(int i=0;i<14;i++)
        {
            int x=460+i*68, top=230+(i*73)%240;
            Raylib.DrawRectangle(x,top,58,screenH-top,new Color(14,24,37,255));
            for(int row=0;row<15;row++) for(int col=0;col<4;col++)
                if((row*3+col+i)%4!=0) Raylib.DrawRectangle(x+8+col*12,top+14+row*25,4,9,new Color(87,116,132,90));
        }
        int tx=(int)(screenW*.70f), ty=96;
        Raylib.DrawRectangle(tx,ty,202,screenH,new Color(12,22,35,255));
        Raylib.DrawTriangle(new(tx+202,ty),new(tx+202,screenH),new(tx+254,screenH),new Color(7,15,25,255));
        Raylib.DrawRectangle(tx,ty,3,screenH,new Color(41,180,194,255));
        Raylib.DrawRectangle(tx+199,ty,3,screenH,new Color(183,47,99,255));
        Raylib.DrawRectangle(tx+47,ty-21,108,21,new Color(23,42,54,255));
        Raylib.DrawLineEx(new(tx+101,ty-21),new(tx+101,ty-65),2,new Color(195,154,91,255));
        for(int row=0;row<22;row++) for(int col=0;col<8;col++)
        {
            bool lit=(row*7+col*3)%5<3;
            Raylib.DrawRectangle(tx+13+col*23,ty+20+row*25,10,12,lit?new Color(184,144,88,170):new Color(24,45,57,255));
        }
        Raylib.DrawRectangleGradientH(0,0,780,screenH,new Color(8,13,23,255),new Color(8,13,23,0));
        DrawTextCrisp(font,"CLOUD ENTROPIC TOWER",56,43,18,new Color(82,202,213,255));
        DrawTextCrisp(GameFonts.Get(Typeface.InterfaceBold,58),"HOW TO",52,91,58,Color.White);
        DrawTextCrisp(GameFonts.Get(Typeface.InterfaceBold,72),"VIBE CODER",52,150,72,Color.White);
        DrawTextCrisp(font,"ЭТАЖ 1: БЕЛАЯ АКУЛА IT",56,242,24,new Color(229,178,101,255));
        DrawTextCrisp(font,"1994 / 2026    Свои правила в чужом офисе",56,285,16,Edge);
        for(int i=0;i<MainMenuButtonLabels.Length;i++)
        {
            Rectangle r=MainButton(i);
            bool selected=i==_selectedMainIndex;
            Raylib.DrawRectangleRec(r,selected?new Color(35,62,77,255):new Color(17,28,43,235));
            if(selected) Raylib.DrawRectangle((int)r.X,(int)r.Y,3,(int)r.Height,new Color(75,212,219,255));
            DrawTextCrisp(font,MainMenuButtonLabels[i],(int)r.X+18,(int)r.Y+6,21,Color.White);
        }
        DrawTextCrisp(font,"ОДИНОЧНАЯ ИГРА / КООПЕРАТИВ ПО IP",56,screenH-31,13,new Color(146,166,184,255));
        Center(font,"CLOUD ENTROPIC",new(tx+10,ty+173,182,36),17,Color.White);
        Center(font,"TOWER",new(tx+10,ty+208,182,27),14,new Color(82,202,213,255));
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

    private static void DrawSlateWindow(string title,int width,int height)
    {
        int sw=Raylib.GetScreenWidth(), sh=Raylib.GetScreenHeight();
        Raylib.DrawRectangle(0,0,sw,sh,new Color(8,15,32,152));
        Rectangle panel=new((sw-width)/2,(sh-height)/2,width,height);
        Surface(panel,new Color(42,66,99,250));
        Center(Serif,title,new(panel.X+30,panel.Y+13,panel.Width-176,45),title.Length>35 ? 25 : 35,Color.White);
    }

    public static void DrawPauseOverlay(Font font,int screenW,int screenH)
    {
        if (IsSaveLoadPanelOpen || IsModsPanelOpen || IsSettingsPanelOpen) return;
        DrawSlateWindow("Пауза",580,560);
        if(Coop.Host || Coop.Guest)
            Center(font,Coop.Host ? "Адрес для друга: "+Coop.HostAddress : Coop.Status,
                new(screenW/2-265,screenH/2-216,530,34),17,Edge);
        for(int i=0;i<PauseButtonLabels.Length;i++)
            NativeButton(PauseButtonLabels[i],new(screenW/2-246,screenH/2-164+i*58,492,48),i==_selectedPauseIndex);
    }

    private static void DrawSaveLoadSubPanel(Font font,int screenW,int screenH)
    {
        if(JoinPanel) { DrawJoinPanel(font); return; }
        DrawSlateWindow(IsSaveMode ? "Сохранить игру" : "Войти в игру",860,540);
        int x=screenW/2-430, y=screenH/2-270;
        NativeButton("Назад",new(x+700,y+14,140,38));
        for(int i=0;i<4;i++)
        {
            var data=_cachedSlots[i];
            Rectangle row=new(x+32,y+92+i*98,796,86);
            Surface(row,i==_selectedSaveSlotIndex ? new Color(69,102,137,251) : new Color(43,68,102,250),1);
            string title=i==0 ? "Автосохранение" : $"Слот {i}";
            DrawTextCrisp(Serif,title,(int)row.X+20,(int)row.Y+12,24,Color.White);
            string detail=data==null ? "Пусто" : $"{data.SavedAt}    ${data.Cash}    Галера {data.SeatedGalleyCoders}/4";
            DrawTextCrisp(Serif,detail,(int)row.X+20,(int)row.Y+48,17,Edge);
            if(IsSaveMode || data!=null) NativeButton(IsSaveMode ? "Сохранить" : "Загрузить",new(row.X+606,row.Y+20,170,46));
        }
    }

    private static void DrawModsSubPanel(Font font,int screenW,int screenH)
    {
        DrawSlateWindow("Настройки модов",880,540);
        int x=screenW/2-440,y=screenH/2-270;
        NativeButton("Назад",new(x+720,y+14,140,38));
        for(int i=0;i<InstalledMods.Count;i++)
        {
            var mod=InstalledMods[i];
            Rectangle row=new(x+32,y+104+i*94,816,82);
            Surface(row,new Color(35,56,86,245),1);
            DrawTextCrisp(Serif,mod.Name,(int)row.X+15,(int)row.Y+9,22,Color.White);
            DrawTextCrisp(font,mod.Author+"   "+mod.Version,(int)row.X+16,(int)row.Y+37,14,Edge);
            Center(font,mod.Description,new(row.X+10,row.Y+54,644,20),15,Edge);
            NativeButton(mod.Enabled ? "Включён" : "Выключен",new(row.X+660,row.Y+18,136,44),mod.Enabled);
        }
        NativeButton("Звуковая рубка [F8]",ModMixer);
        NativeButton("Тест: +$2000",ModAction(0));
        NativeButton("Открыть оружие",ModAction(1));
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

    private static void DrawTextCrisp(Font font, string text, int x, int y, int size, Color color) =>
        GameFonts.Draw(font,text,new(x,y),size,color);
}
