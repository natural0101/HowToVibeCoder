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
    internal static DisplaySettings Display { get; private set; }=new();
    internal static bool InteractiveDisplay { get; private set; }
    private static string displayPath=Path.Combine(SavesDirectory,"display_settings.json");
    internal static void LoadDisplay(string path,bool interactive)
    {
        displayPath=path;
        InteractiveDisplay=interactive;
        Display=DisplaySettings.Load(path);
        Loc.Set(Display.Language=="en" ? GameLang.En : GameLang.Ru,false);
    }
    internal static bool StoreLanguage(GameLang language)
    {
        var next=Display with { Language=language==GameLang.En ? "en" : "ru" };
        try { next.Save(displayPath); }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException)
        { Notify(Loc.T("Ошибка сохранения языка","Could not save language")); return false; }
        Display=next;
        return true;
    }
    internal static void CycleDisplay(bool mode)
    {
        var next=mode ? Display with { DisplayMode=Display.DisplayMode==DisplayMode.Fullscreen ? DisplayMode.Windowed : DisplayMode.Fullscreen }
            : Display with { ResolutionIndex=(Display.ResolutionIndex+1)%DisplaySettings.Sizes.Length };
        try { next.Save(displayPath); }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException)
        { Notify("Не удалось сохранить настройки экрана"); return; }
        Display=next;
        if(InteractiveDisplay) Display.Apply();
    }

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
        ["Играть", "Продолжить", "Персонаж", "Настройки", "Выход"];
    private static readonly string[] SettingsTabs = ["Игра", "Графика", "Аудио", "Язык"];
    internal static readonly CoopNetworkSystem Coop = new();
    internal static Action<bool>? OnIntroComicRequested;
    internal static Action? SummonPartner;
    private static int coopFocus;
    private static bool _playModeOpen;
    private static int _playModeIndex;
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
    private static int _settingsTab = 1;
    private static string? _infoPage;
    private static readonly Dictionary<string, Texture2D> Art = new();
    internal static Font Serif;
    internal static readonly Color Edge = DossierPrint.Quiet;
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
    internal static Rectangle MainButton(int i) => new(56, UiViewport.Height - 332 + i * 50, 310, 42);
    internal static Rectangle PlayChoiceButton(int i) => new(UiViewport.Width / 2f - 230, UiViewport.Height / 2f - 64 + i * 58, 460, 46);
    private static Rectangle SettingsRect(float x, float y, float w, float h) =>
        new(UiViewport.Width / 2f - 343 + x, UiViewport.Height / 2f - 276 + y, w, h);
    private static Rectangle SettingsTab(int i) => SettingsRect(i * 173, 32, 166, 39);
    private static Rectangle LanguagePill(int i) => SettingsRect(320+i*138,136,130,40);
    private static Rectangle DisplayButton(int row) => SettingsRect(337,245+row*56,279,40);
    private static Rectangle SettingsTrack(int row) => SettingsRect(373, (_settingsTab==1 ? 370 : _settingsTab==0 ? 250 : 136) + row * 47 + (row == 3 ? 45 : 0), 172, 20);
    private static readonly int[][] SettingRows = [[4], [5], [0, 1, 2, 3], []];
    private static readonly string[] SettingNames = ["Общая громкость", "Громкость музыки", "Громкость эффектов", "Громкость речи", "Чувствительность мыши", "Поле зрения"];
    private static bool Clicked(Rectangle r) => Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(UiViewport.Pointer, r);
    private static Rectangle ModAction(int i) => new(UiViewport.Width/2 - 95 + i*249, UiViewport.Height/2 + 213, 240, 40);
    private static Rectangle ModMixer => new(UiViewport.Width/2 - 408, UiViewport.Height/2 + 213, 275, 40);

    // 6 кнопок Меню Паузы (по ESC в игре)
    private static readonly string[] PauseButtonLabels =
    {
        "ПРОДОЛЖИТЬ",
        "СОХРАНИТЬ ИГРУ",
        "ЗАГРУЗИТЬ ИГРУ",
        "НАСТРОЙКИ",
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
            Name = "Lore & Leaked Work Chats Expansion v1.1",
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
            Description = "Повышенная агрессия боссов и охраны этажа (+25% к скорости атак).",
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
            ShowStatusBanner($"ИГРА СОХРАНЕНА В «{data.SlotTitle.ToUpperInvariant()}» (${data.Cash}, Опенспейс: {data.SeatedGalleyCoders}/4)");
        }
        catch (Exception ex)
        {
            ShowStatusBanner($"Ошибка сохранения: {ex.Message}");
        }

        return data;
    }

    internal static int LatestSave => Enumerable.Range(0,_cachedSlots.Length).Where(i=>_cachedSlots[i]!=null)
        .OrderByDescending(i=>File.GetLastWriteTimeUtc(GetSaveFilePath(i))).FirstOrDefault(-1);

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
        _playModeOpen = false;
        IsSaveLoadPanelOpen = false;
        IsModsPanelOpen = false;
        IsSettingsPanelOpen = false;
        LoreAndMenusUI.IsLoreOpen = false;
        LoreAndMenusUI.IsSettingsOpen = false;
        LoreAndMenusUI.IsCaseOpen = false;
        IsInMainMenu = false;
        IsPaused = false;
        OnStartGameRequested?.Invoke();
        OnStartNewGameRequested?.Invoke();
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
        _playModeOpen = false;
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
            if (_playModeOpen)
            {
                if (Raylib.IsCursorHidden()) Raylib.EnableCursor();
                if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Clicked(new Rectangle(UiViewport.Width / 2f - 83, UiViewport.Height / 2f + 126, 166, 38)))
                {
                    _playModeOpen = false;
                    PlayUiSound();
                    return;
                }
                if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S)) { _playModeIndex = (_playModeIndex + 1) % 3; PlayUiSound(); }
                if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W)) { _playModeIndex = (_playModeIndex + 2) % 3; PlayUiSound(); }
                bool trigger = Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter);
                for (int i = 0; i < 3; i++)
                {
                    if (Raylib.CheckCollisionPointRec(UiViewport.Pointer, PlayChoiceButton(i)))
                    {
                        if (_playModeIndex != i && Raylib.GetMouseDelta().LengthSquared() > 0) { _playModeIndex = i; PlayUiSound(); }
                        if (Raylib.IsMouseButtonPressed(MouseButton.Left)) { _playModeIndex = i; trigger = true; }
                    }
                }
                if (trigger)
                {
                    PlayUiSound();
                    _playModeOpen = false;
                    if (_playModeIndex == 0)
                    {
                        if (OnIntroComicRequested != null) OnIntroComicRequested(true);
                        else StartNewOrResumeFloor();
                    }
                    else if (_playModeIndex == 1) OpenJoinPanel();
                    else
                    {
                        RefreshSaveSlotsCache();
                        IsSaveMode = false;
                        IsSaveLoadPanelOpen = true;
                        IsModsPanelOpen = IsSettingsPanelOpen = false;
                    }
                }
                return;
            }
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
        Vector2 mp = UiViewport.Pointer;
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
            var number = i == 9 ? KeyboardKey.Zero : KeyboardKey.One + i;
            var keypad = i == 9 ? KeyboardKey.Kp0 : KeyboardKey.Kp1 + i;
            if (Raylib.IsKeyPressed(number) || Raylib.IsKeyPressed(keypad))
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
            case 0: // ИГРАТЬ (выбор: Соло/Хост или Кооп 1-3)
                _playModeOpen = true;
                _playModeIndex = 0;
                break;

            case 1: // ПРОДОЛЖИТЬ
                RefreshSaveSlotsCache();
                int latest=LatestSave;
                if(latest>=0) LoadGameState(latest);
                else Notify(Loc.T("Нет сохранённой игры","No saved game yet"));
                break;

            case 2: // ПЕРСОНАЖ
                _infoPage = "Персонаж";
                break;

            case 3: // НАСТРОЙКИ
                _settingsTab = 1;
                IsSettingsPanelOpen = true;
                IsSaveLoadPanelOpen = false;
                IsModsPanelOpen = false;
                break;

            case 4: // ВЫХОД
                ShouldExitGame = true;
                OnExitRequested?.Invoke();
                break;
        }
    }

    private static void UpdatePauseMenuInput(float dt)
    {
        int sw = UiViewport.Width;
        int sh = UiViewport.Height;
        Vector2 mp = UiViewport.Pointer;
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

        for (int i = 0; i < PauseButtonLabels.Length; i++)
        {
            Rectangle btnRect = PauseRowRect(i, sw, sh);
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

    private static Rectangle PauseRowRect(int index, int screenW, int screenH) =>
        new(screenW / 2 - 220, screenH / 2 - 134 + index * 52, 440, 44);

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
                _settingsTab = 1;
                IsSettingsPanelOpen = true;
                IsSaveLoadPanelOpen = false;
                IsModsPanelOpen = false;
                break;

            case 4: // В ГЛАВНОЕ МЕНЮ
                SaveGameState(0); // Автосохранение при выходе в Главное Меню
                OpenMainMenu();
                break;

            case 5: // ВЫХОД
                SaveGameState(0);
                ShouldExitGame = true;
                OnExitRequested?.Invoke();
                break;
        }
    }

    private static Rectangle CoopRow(int row)
    {
        float x=UiViewport.Width/2-388, y=UiViewport.Height/2-270;
        return row switch {
            0 => new(x,y+100,776,42), 1 => new(x,y+156,776,44), 2 => new(x,y+210,776,44),
            _ => new(x,y+324+(row-3)*56,776,48)
        };
    }

    private static void UpdateJoinPanel()
    {
        Rectangle back=new(UiViewport.Width/2+290,UiViewport.Height/2-256,118,38);
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
        int x=UiViewport.Width/2-388,y=UiViewport.Height/2-270;
        DrawSlateWindow("КООПЕРАТИВ (ВОЙТИ К ДРУГУ) / ЗАГРУЗКА",860,540);
        NativeButton("Назад",new(x+678,y+14,118,38));
        DrawTextCrisp(font,"Адрес друга (IPv4:порт)",x,y+70,18,Edge);
        var field=CoopRow(0);
        Surface(field,coopFocus==0 ? new Color(44,38,34,255) : new Color(32,28,25,255));
        DrawTextCrisp(font,address+(coopFocus==0 && (_menuClock%1)<.55f ? "|" : ""),x+15,(int)field.Y+8,23,Color.White);
        NativeButton("ПОДКЛЮЧИТЬСЯ К ИГРЕ ДРУГА",CoopRow(1),coopFocus==1);
        NativeButton("ВЫЗВАТЬ БРАТАНА В КООП (НАПАРНИК)",CoopRow(2),coopFocus==2);
        DrawTextCrisp(font,(Coop.LobbyCount is int count ? $"{count}/3   " : "")+Loc.Tr(Coop.Status),x,y+263,16,Edge);
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

        int sw = UiViewport.Width;
        int sh = UiViewport.Height;
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
            Vector2 mp = UiViewport.Pointer;
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
        if (Program.DevMode && Clicked(ModAction(0))) { LoreAndMenusUI.TriggerGrantTestMoney(); return; }
        if (Program.DevMode && Clicked(ModAction(1))) { LoreAndMenusUI.TriggerUnlockAllWeapons(); return; }
        Rectangle mixerBtn = Program.DevMode ? ModMixer : new Rectangle(UiViewport.Width / 2f - 150, UiViewport.Height / 2f + 213, 300, 40);
        if (Clicked(mixerBtn)) { LoreAndMenusUI.IsSettingsOpen = true; return; }
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            IsModsPanelOpen = false;
            PlayUiSound();
            return;
        }

        int sw = UiViewport.Width;
        int sh = UiViewport.Height;
        int winW = 880;
        int winH = 540;
        int wx = (sw - winW) / 2;
        int wy = (sh - winH) / 2;

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            Vector2 mp = UiViewport.Pointer;
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
        if (_settingsTab is 0 or 1 or 3)
        {
            if (Clicked(LanguagePill(0)) || Raylib.IsKeyPressed(KeyboardKey.Left)) Loc.Set(GameLang.Ru);
            else if (Clicked(LanguagePill(1)) || Raylib.IsKeyPressed(KeyboardKey.Right)) Loc.Set(GameLang.En);
            else if (Raylib.IsKeyPressed(KeyboardKey.Enter)) Loc.Set(Loc.IsEn ? GameLang.Ru : GameLang.En);
        }
        if (_settingsTab==1)
        {
            if(Clicked(DisplayButton(0))) { CycleDisplay(true); return; }
            if(Clicked(DisplayButton(1))) { CycleDisplay(false); return; }
        }
        if (_settingsTab == 2 && Clicked(SettingsRect(140, 478, 406, 36))) LoreAndMenusUI.IsSettingsOpen = true;
        if (!Raylib.IsMouseButtonDown(MouseButton.Left)) _draggingSettingSlider = -1;
        int[] rows = SettingRows[_settingsTab];
        for (int i = 0; i < rows.Length; i++)
        {
            Rectangle r = SettingsTrack(i);
            if (Clicked(r)) _draggingSettingSlider = rows[i];
            if (_draggingSettingSlider == rows[i]) ApplySettingValue(rows[i], (UiViewport.Pointer.X - r.X) / r.Width);
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
        UiViewport.Begin();
        DrawPanels(font,(int)MathF.Round(screenW/UiViewport.Scale),(int)MathF.Round(screenH/UiViewport.Scale));
        Raylib.EndMode2D();
    }

    private static void DrawPanels(Font font,int screenW,int screenH)
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
        int fadeEnd = (int)MathF.Round(screenW * .64f);
        const int slices = 64;
        static Color Scrim(float t)
        {
            float u = Math.Clamp((t - .18f) / .82f, 0f, 1f);
            float opacity = 1f - u * u * (3f - 2f * u);
            return new Color(20, 17, 15, (int)MathF.Round(244f * opacity));
        }
        for (int s = 0; s < slices; s++)
        {
            int x0 = s * fadeEnd / slices;
            int x1 = (s + 1) * fadeEnd / slices;
            Raylib.DrawRectangleGradientH(x0, 0, x1 - x0, screenH,
                Scrim(x0 / (float)fadeEnd), Scrim(x1 / (float)fadeEnd));
        }
        DrawTextCrisp(font,"МОСКВА-СИТИ, 2026",56,64,16,DossierPrint.Quiet);
        DrawTextCrisp(GameFonts.Get(Typeface.SerifBold,58),"HOW TO",52,100,58,Color.White);
        DrawTextCrisp(GameFonts.Get(Typeface.SerifBold,58),"VIBE CODER",52,156,58,Color.White);
        DrawTextCrisp(font,"Этаж 01. «ЭНТРОПИК»",56,236,20,new Color(242,205,154,255));
        for(int i=0;i<MainMenuButtonLabels.Length;i++)
        {
            Rectangle r=MainButton(i);
            bool selected=i==_selectedMainIndex && !_playModeOpen && _infoPage==null;
            DrawMenuItem(MainMenuButtonLabels[i], r, selected);
        }
        if (_playModeOpen)
        {
            DrawSlateWindow(Loc.T("Играть","Play"), 540, 360);
            string[] choices =
            [
                Loc.T("Новая игра (Соло / Хост 1–3)", "New Game (Solo / Host 1-3)"),
                Loc.T("Подключиться к другу (Кооп 1–3)", "Join Friend (Co-op 1-3)"),
                Loc.T("Загрузить сохранение", "Load Save Slot")
            ];
            for (int i = 0; i < choices.Length; i++)
                NativeButton(choices[i], PlayChoiceButton(i), i == _playModeIndex);
            NativeButton("Назад", new Rectangle(UiViewport.Width / 2f - 83, UiViewport.Height / 2f + 126, 166, 38));
        }
        if (_infoPage != null)
        {
            Raylib.DrawRectangle(0,0,screenW,screenH,new Color(18,15,13,185));
            Surface(SettingsRect(0,88,686,400),new Color(26,23,21,250));
            Center(Serif,_infoPage,SettingsRect(30,114,626,68),44,Color.White);
            string body = _infoPage == "Персонаж" ? "Коммерс в малиновом пиджаке" : "How to Vibe Coder";
            Center(Serif,body,SettingsRect(40,220,606,60),32,Edge);
            Center(Serif,_infoPage == "Персонаж" ? LoreAndMenusUI.EquippedBeltSkinName : "Создано на raylib и .NET",SettingsRect(40,280,606,48),23,Edge);
            NativeButton("Назад", SettingsRect(0,505,166,38));
        }
    }

    private static void NativeButton(string label, Rectangle r, bool selected = false)
    {
        bool hover = Raylib.CheckCollisionPointRec(UiViewport.Pointer, r);
        bool lit = selected || hover;
        Raylib.DrawRectangleRec(new(r.X + 2, r.Y + 3, r.Width, r.Height), new Color(6, 4, 2, 130));
        Raylib.DrawRectangleRec(r, lit ? LoreAndMenusUI.ThemeKraftCard : new Color(34, 25, 19, 246));
        Raylib.DrawRectangleLinesEx(r, lit ? 1.6f : 1.1f, lit ? LoreAndMenusUI.ThemeBrassBright : LoreAndMenusUI.ThemeBrassDim);
        Center(GameFonts.Get(lit ? Typeface.InterfaceBold : Typeface.Interface, 19), label, r, 19, lit ? LoreAndMenusUI.ThemeSepiaInk : DossierPrint.Ivory);
    }

    private static void DrawMenuItem(string label, Rectangle r, bool selected)
    {
        bool active = selected || Raylib.CheckCollisionPointRec(UiViewport.Pointer, r);
        Rectangle card = new(r.X + (active ? 6 : 0), r.Y + 2, r.Width - 6, r.Height - 4);
        Raylib.DrawRectangleRounded(new(card.X + 3, card.Y + 4, card.Width, card.Height), .12f, 8, new Color(5, 3, 2, 165));
        if (active)
        {
            Raylib.DrawRectangleRounded(card, .12f, 8, LoreAndMenusUI.ThemeKraftCard);
            Raylib.DrawRectangleGradientV((int)card.X + 2, (int)card.Y + 2, (int)card.Width - 4, (int)card.Height / 2,
                new Color(255, 250, 232, 75), Color.Blank);
            Raylib.DrawRectangleRoundedLinesEx(card, .12f, 8, 2.0f, LoreAndMenusUI.ThemeBrassBright);
        }
        else
        {
            Raylib.DrawRectangleRounded(card, .12f, 8, new Color(36, 25, 19, 236));
            Raylib.DrawRectangleGradientV((int)card.X + 2, (int)card.Y + 2, (int)card.Width - 4, (int)card.Height / 2,
                new Color(140, 108, 64, 28), Color.Blank);
            Raylib.DrawRectangleRoundedLinesEx(card, .12f, 8, 1.2f, LoreAndMenusUI.ThemeBrassDim);
        }
        // Subtle twin brass rivets anchoring the plaque
        Color rivet = active ? LoreAndMenusUI.ThemeBrassDim : new Color(138, 108, 64, 175);
        Raylib.DrawCircleV(new(card.X + card.Width - 14, card.Y + card.Height * 0.5f), 2.5f, rivet);
        int size = 20;
        var face = GameFonts.Get(active ? Typeface.InterfaceBold : Typeface.Interface, size);
        GameFonts.Draw(face, label, new(card.X + 18, card.Y + (card.Height - size) / 2), size,
            active ? LoreAndMenusUI.ThemeSepiaInk : new Color(236, 222, 196, 248), shadowed: false);
    }

    private static void DrawSlateWindow(string title,int width,int height)
    {
        int sw=UiViewport.Width, sh=UiViewport.Height;
        Raylib.DrawRectangle(0,0,sw,sh,new Color(14,10,8,205));
        Rectangle panel=new((sw-width)/2,(sh-height)/2,width,height);
        LoreAndMenusUI.DrawBrassWalnutFrame(panel);
        GameFonts.Draw(Serif, title, new(panel.X + 34, panel.Y + 22), title.Length > 35 ? 24 : 34, LoreAndMenusUI.ThemeBrassBright);
        Raylib.DrawLine((int)panel.X + 32, (int)panel.Y + 70, (int)(panel.X + panel.Width - 32), (int)panel.Y + 70, LoreAndMenusUI.ThemeBrassDim);
    }

    public static void DrawPauseOverlay(Font font,int screenW,int screenH)
    {
        if (IsSaveLoadPanelOpen || IsModsPanelOpen || IsSettingsPanelOpen) return;
        Raylib.DrawRectangle(0, 0, screenW, screenH, new Color(12, 9, 7, 196));
        // Real tactile mahogany stitched leather clipboard with steel spring clip & cream lined notepad!
        float clipH = Math.Min(screenH - 36, 680);
        float clipW = clipH * (720f / 990f);
        Rectangle board = new((screenW - clipW) * 0.5f, (screenH - clipH) * 0.5f - 4, clipW, clipH);
        LoreAndMenusUI.DrawPauseClipboard(board);

        // Handwritten/stamped header directly on the lined notepad paper beneath the brass clip!
        float paperX = board.X + board.Width * 0.21f;
        float paperW = board.Width * 0.58f;
        DossierPrint.Text("Пауза", paperX, board.Y + board.Height * 0.222f, 34, LoreAndMenusUI.ThemeSepiaInk, Typeface.SerifBold);
        Raylib.DrawLine((int)paperX, (int)(board.Y + board.Height * 0.280f), (int)(paperX + paperW), (int)(board.Y + board.Height * 0.280f), LoreAndMenusUI.ThemeSepiaInk);

        if(Coop.Host || Coop.Guest)
            DossierPrint.Text((Coop.Host ? Loc.Tr("Адрес для друга: ")+Coop.HostAddress : Loc.Tr(Coop.Status))+"   "+Program.PausePlayerCount,
                paperX, board.Y + board.Height * 0.288f, 13, LoreAndMenusUI.ThemeSepiaMuted);

        // Write the 6 pause entries directly onto the cream notepad lines (ZERO dark rectangle boxes!)
        for(int i=0;i<PauseButtonLabels.Length;i++)
        {
            Rectangle hit = PauseRowRect(i, screenW, screenH);
            bool active = i == _selectedPauseIndex;
            float y = hit.Y + 10;
            if (active)
            {
                // Crisp hand-drawn crimson ink checkmark on the left margin of the notepad
                Raylib.DrawLineEx(new(paperX - 20, y + 11), new(paperX - 14, y + 18), 2.8f, LoreAndMenusUI.ThemeStampRed);
                Raylib.DrawLineEx(new(paperX - 14, y + 18), new(paperX - 4, y + 5), 2.8f, LoreAndMenusUI.ThemeStampRed);
                DossierPrint.Text(PauseButtonLabels[i], paperX + 4, y, 21, LoreAndMenusUI.ThemeStampRed, Typeface.InterfaceBold);
                float tw = GameFonts.Measure(GameFonts.Get(Typeface.InterfaceBold, 21), PauseButtonLabels[i], 21).X;
                Raylib.DrawLineEx(new(paperX + 2, y + 27), new(paperX + 8 + tw, y + 26), 2.2f, LoreAndMenusUI.ThemeStampRed);
            }
            else
            {
                DossierPrint.Text(PauseButtonLabels[i], paperX, y, 20, LoreAndMenusUI.ThemeSepiaInk, Typeface.Interface);
            }
        }
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
            Surface(row,i==_selectedSaveSlotIndex ? new Color(49,42,37,251) : new Color(29,25,22,250),1);
            string title=i==0 ? "Автосохранение" : $"Слот {i}";
            DrawTextCrisp(Serif,title,(int)row.X+20,(int)row.Y+12,24,Color.White);
            string detail=data==null ? "Пусто" : $"{data.SavedAt}    ${data.Cash}    Опенспейс {data.SeatedGalleyCoders}/4";
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
            Rectangle row=new(x+32,y+100+i*94,816,82);
            Raylib.DrawRectangleRounded(new(row.X+2,row.Y+3,row.Width,row.Height),.08f,8,new Color(8,5,3,135));
            Raylib.DrawRectangleRounded(row,.08f,8,new Color(42,31,24,244));
            Raylib.DrawRectangleGradientV((int)row.X+3,(int)row.Y+2,(int)row.Width-6,28,new Color(172,134,76,22),Color.Blank);
            Raylib.DrawRectangleRoundedLinesEx(row,.08f,8,1.2f,LoreAndMenusUI.ThemeBrassDim);
            DrawTextCrisp(Serif,mod.Name,(int)row.X+18,(int)row.Y+9,22,DossierPrint.Ivory);
            DrawTextCrisp(font,mod.Author+"   "+mod.Version,(int)row.X+19,(int)row.Y+37,14,LoreAndMenusUI.ThemeBrassBright);
            DrawTextCrisp(font,mod.Description,(int)row.X+19,(int)row.Y+56,15,new Color(206,192,168,235));
            NativeButton(mod.Enabled ? "Включён" : "Выключен",new(row.X+660,row.Y+18,136,44),mod.Enabled);
        }
        if (Program.DevMode)
        {
            NativeButton("Звуковая рубка [F8]",ModMixer);
            NativeButton("Тест: +$2000",ModAction(0));
            NativeButton("Открыть оружие",ModAction(1));
        }
        else
        {
            NativeButton("Звуковая рубка [F8]",new Rectangle(UiViewport.Width / 2f - 150, UiViewport.Height / 2f + 213, 300, 40));
        }
    }

    private static void DrawSettingsSubPanel(Font font, int screenW, int screenH)
    {
        Raylib.DrawRectangle(0,0,screenW,screenH,new Color(14,10,8,215));
        Center(Serif,"Настройки",SettingsRect(0,-28,686,54),46,LoreAndMenusUI.ThemeBrassBright);
        for (int i=0;i<4;i++) NativeButton(SettingsTabs[i],SettingsTab(i),i==_settingsTab);
        Rectangle panel = SettingsRect(0,80,686,462);
        LoreAndMenusUI.DrawBrassWalnutFrame(panel);

        Center(Serif,SettingsTabs[_settingsTab],SettingsRect(0,92,686,37),28,LoreAndMenusUI.ThemeBrassBright);
        Raylib.DrawLine((int)panel.X+42,(int)panel.Y+54,(int)(panel.X+panel.Width-42),(int)panel.Y+54,LoreAndMenusUI.ThemeBrassDim);
        int[] rows = SettingRows[_settingsTab];
        for (int i=0;i<rows.Length;i++)
        {
            int id = rows[i];
            float t = LoreAndMenusUI.GetSliderNormalizedValue(id);
            Rectangle r = SettingsTrack(i);
            float y = r.Y+r.Height/2;
            DrawTextCrisp(Serif,SettingNames[id],(int)panel.X+67,(int)y-13,22,DossierPrint.Ivory);
            Raylib.DrawRectangleRounded(new(r.X,y-3,r.Width,6),.8f,6,new Color(20,14,11,240));
            Raylib.DrawRectangleRounded(new(r.X,y-3,Math.Max(6,t*r.Width),6),.8f,6,DossierPrint.Crimson);
            float x=r.X+t*r.Width;
            Raylib.DrawRectangleRounded(new(x-5,y-11,10,22),.35f,6,LoreAndMenusUI.ThemeBrassBright);
            Raylib.DrawRectangleRoundedLinesEx(new(x-5,y-11,10,22),.35f,6,1f,LoreAndMenusUI.ThemeSepiaInk);
            float value = id == 4 ? LoreAndMenusUI.MouseSensitivity : id == 5 ? LoreAndMenusUI.CameraFov : t;
            Rectangle val = new(r.X+r.Width+32, y-17,68,34);
            Raylib.DrawRectangleRounded(val,.18f,6,new Color(28,20,15,235));
            Raylib.DrawRectangleRoundedLinesEx(val,.18f,6,1f,LoreAndMenusUI.ThemeBrassDim);
            Center(Serif,value.ToString(id==5?"0":"0.00",System.Globalization.CultureInfo.GetCultureInfo(Loc.IsEn ? "en-US" : "ru-RU")),val,21,LoreAndMenusUI.ThemeBrassBright);
        }
        if (_settingsTab==1)
        {
            for(int i=0;i<2;i++)
            {
                var r=DisplayButton(i);
                DrawTextCrisp(Serif,i==0 ? "Режим экрана" : "Разрешение",(int)panel.X+67,(int)r.Y+6,22,DossierPrint.Ivory);
                NativeButton(i==1 ? Display.ResolutionLabel : Display.DisplayMode==DisplayMode.Fullscreen ? "На весь экран" : "В окне",r);
            }
        }
        if (_settingsTab==2)
        {
            Center(Serif,"Голоса персонажей",SettingsRect(0,257,686,42),26,LoreAndMenusUI.ThemeBrassBright);
            NativeButton("Звуковая рубка [F8]",SettingsRect(140,478,406,36));
        }
        // Keep Language toggle strictly inside the Language tab (no misplaced duplicate inside Graphics tab!)
        if (_settingsTab == 3)
        {
            DrawTextCrisp(Serif,"Язык / Language",(int)panel.X+67,(int)LanguagePill(0).Y+6,22,DossierPrint.Ivory);
            for(int i=0;i<2;i++)
            {
                Rectangle pill=LanguagePill(i);
                bool active=(int)Loc.Current==i;
                bool hover=Raylib.CheckCollisionPointRec(UiViewport.Pointer,pill);
                Raylib.DrawRectangleRounded(pill,.25f,8,active ? LoreAndMenusUI.ThemeKraftCard : hover ? new Color(56,42,32,245) : new Color(30,22,17,245));
                Raylib.DrawRectangleRoundedLinesEx(pill,.25f,8,active ? 1.8f : 1.1f,active ? LoreAndMenusUI.ThemeBrassBright : LoreAndMenusUI.ThemeBrassDim);
                Center(font,i==0 ? "Русский" : "English",pill,19,active ? LoreAndMenusUI.ThemeSepiaInk : DossierPrint.Ivory);
            }
            Center(font,Loc.T("Меняет весь интерфейс, субтитры и студийную озвучку (RU / EN)",
                "Switches all UI, subtitles, and studio voice acting (RU / EN)"),SettingsRect(32,196,622,32),17,new Color(208,192,166,230));
        }
        NativeButton("Назад",SettingsRect(0,550,166,38));
    }

    private static void DrawStatusToast(Font font, int screenW, string message)
    {
        int w = Math.Min(screenW - 80, 760);
        int x = (screenW - w) / 2;
        int y = 44;
        Raylib.DrawRectangle(x, y, w, 38, new Color(27, 24, 21, 245));
        Raylib.DrawRectangleLinesEx(new Rectangle(x, y, w, 38), 2f, new Color(83, 73, 65, 255));
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
