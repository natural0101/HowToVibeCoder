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

    // 5 классических кнопок Главного Меню ПК-игры
    private static readonly string[] MainMenuButtonLabels =
    {
        "НОВАЯ ИГРА",
        "ЗАГРУЗИТЬ ИГРУ",
        "НАСТРОЙКИ",
        "МОДЫ",
        "ВЫХОД"
    };

    private static readonly string[] MainMenuButtonSubtitles =
    {
        "Начать свежий штурм 1-го этажа небоскрёба Cloud Entropic Tower",
        "Загрузить сохранение из слотов 1..3 или последнего автосохранения",
        "Громкость звука и музыки, чувствительность мыши, поле зрения (FOV) и полный экран",
        "Управление установленными модами в папке mods/ [ВКЛ / ВЫКЛ]",
        "Выйти из игры на Рабочий стол Windows"
    };

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
    private static readonly float[] _mainHoverAnim = new float[5];
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
            // Гарантируем, что игровые окна лора/кейсов закрыты в Главном Меню
            LoreAndMenusUI.IsLoreOpen = false;
            LoreAndMenusUI.IsCaseOpen = false;
            LoreAndMenusUI.IsSettingsOpen = false;

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

        if (Raylib.IsKeyPressed(KeyboardKey.One) || Raylib.IsKeyPressed(KeyboardKey.Kp1))
        {
            _selectedMainIndex = 0;
            ExecuteMainMenuAction(0);
            return;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Two) || Raylib.IsKeyPressed(KeyboardKey.Kp2))
        {
            _selectedMainIndex = 1;
            ExecuteMainMenuAction(1);
            return;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Three) || Raylib.IsKeyPressed(KeyboardKey.Kp3))
        {
            _selectedMainIndex = 2;
            ExecuteMainMenuAction(2);
            return;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Four) || Raylib.IsKeyPressed(KeyboardKey.Kp4))
        {
            _selectedMainIndex = 3;
            ExecuteMainMenuAction(3);
            return;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Five) || Raylib.IsKeyPressed(KeyboardKey.Kp5))
        {
            _selectedMainIndex = 4;
            ExecuteMainMenuAction(4);
            return;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter))
        {
            ExecuteMainMenuAction(_selectedMainIndex);
            return;
        }

        GetMainMenuLayout(sw, sh, out Rectangle leftPanel, out _);
        int btnStartX = (int)leftPanel.X + 26;
        int btnStartY = (int)leftPanel.Y + 232;
        int btnW = (int)leftPanel.Width - 52;
        int btnH = 58;
        int btnGap = 11;

        for (int i = 0; i < MainMenuButtonLabels.Length; i++)
        {
            Rectangle btnRect = new(btnStartX, btnStartY + i * (btnH + btnGap), btnW, btnH);
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

            case 2: // НАСТРОЙКИ
                IsSettingsPanelOpen = true;
                IsSaveLoadPanelOpen = false;
                IsModsPanelOpen = false;
                break;

            case 3: // МОДЫ
                LoadModsConfig();
                IsModsPanelOpen = true;
                IsSaveLoadPanelOpen = false;
                IsSettingsPanelOpen = false;
                break;

            case 4: // ВЫХОД
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
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            IsSettingsPanelOpen = false;
            _draggingSettingSlider = -1;
            PlayUiSound();
            return;
        }

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        int winW = 860;
        int winH = 550;
        int wx = (sw - winW) / 2;
        int wy = (sh - winH) / 2;

        Vector2 mp = Raylib.GetMousePosition();
        bool mouseDown = Raylib.IsMouseButtonDown(MouseButton.Left);
        bool mousePressed = Raylib.IsMouseButtonPressed(MouseButton.Left);

        if (!mouseDown)
        {
            _draggingSettingSlider = -1;
        }

        int sliderX = wx + 360;
        int sliderW = 340;
        int rowStartY = wy + 92;
        int rowStepY = 54;

        for (int i = 0; i < 6; i++)
        {
            int ry = rowStartY + i * rowStepY;
            Rectangle hitRect = new(sliderX - 10, ry, sliderW + 20, 36);
            if (mousePressed && Raylib.CheckCollisionPointRec(mp, hitRect))
            {
                _draggingSettingSlider = i;
            }

            if (_draggingSettingSlider == i && mouseDown)
            {
                float t = Math.Clamp((mp.X - sliderX) / sliderW, 0f, 1f);
                ApplySettingValue(i, t);
            }
        }

        if (mousePressed)
        {
            Rectangle closeBtn = new(wx + winW - 160, wy + 14, 140, 38);
            if (Raylib.CheckCollisionPointRec(mp, closeBtn))
            {
                IsSettingsPanelOpen = false;
                PlayUiSound();
                return;
            }

            Rectangle fullscreenBtn = new(wx + 360, wy + 422, 240, 42);
            if (Raylib.CheckCollisionPointRec(mp, fullscreenBtn))
            {
                IsFullscreenEnabled = !IsFullscreenEnabled;
                try
                {
                    Raylib.ToggleFullscreen();
                }
                catch
                {
                }
                PlayUiSound();
                return;
            }
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
        Raylib.DrawRectangleGradientV(0, 0, screenW, 130, new Color(8, 6, 16, 210), new Color(8, 6, 16, 25));
        Raylib.DrawRectangleGradientV(0, screenH - 120, screenW, 120, new Color(8, 6, 16, 25), new Color(8, 6, 16, 225));

        DrawTopSecurityCameraBar(font, screenW);

        GetMainMenuLayout(screenW, screenH, out Rectangle leftPanel, out Rectangle rightPanel);
        DrawLeftMainMenuPanel(font, leftPanel);
        DrawRightAuthorityBriefingPanel(font, rightPanel);
        DrawBottomFooterBar(font, screenW, screenH);
    }

    private static void DrawTopSecurityCameraBar(Font font, int screenW)
    {
        int barH = 34;
        Raylib.DrawRectangle(0, 0, screenW, barH, new Color(14, 10, 22, 225));
        Raylib.DrawRectangle(0, barH - 2, screenW, 2, new Color(215, 28, 78, 220));

        bool recBlink = ((int)(_menuClock * 2.4f) % 2) == 0;
        Color dotColor = recBlink ? new Color(255, 42, 72, 255) : new Color(110, 24, 38, 255);
        Raylib.DrawCircle(24, barH / 2, 6f, dotColor);

        DrawTextCrisp(
            font,
            "КАМЕРА 01 // БАШНЯ CLOUD ENTROPIC - ЭТАЖ 1 (БЕЛАЯ АКУЛА)",
            38,
            8,
            15,
            new Color(255, 218, 120, 245));

        string verText = "ВЕРСИЯ 1.0 RELEASE // МОДОВ АКТИВНО: " + InstalledMods.FindAll(m => m.Enabled).Count;
        DrawTextCrisp(font, verText, screenW - 395, 8, 14, new Color(165, 225, 255, 240));
    }

    private static void DrawLeftMainMenuPanel(Font font, Rectangle panel)
    {
        int px = (int)panel.X;
        int py = (int)panel.Y;
        int pw = (int)panel.Width;
        int ph = (int)panel.Height;

        Raylib.DrawRectangle(px + 6, py + 8, pw, ph, new Color(0, 0, 0, 175));
        Raylib.DrawRectangle(px, py, pw, ph, new Color(16, 12, 24, 236));
        Raylib.DrawRectangleGradientV(px, py, pw, 165, new Color(112, 14, 44, 210), new Color(20, 14, 30, 10));

        Raylib.DrawRectangleLinesEx(panel, 3f, new Color(235, 36, 92, 255));
        Raylib.DrawRectangleLinesEx(new Rectangle(px + 5, py + 5, pw - 10, ph - 10), 1.5f, new Color(255, 208, 68, 190));

        int crestX = px + 24;
        int crestY = py + 20;
        Draw90sAuthorityCrest(font, crestX, crestY);

        int titleX = crestX + 94;
        DrawTextCrisp(
            font,
            "CLOUD ENTROPIC TOWER // СПЕЦОПЕРАЦИЯ 1994 -> 2026",
            titleX,
            py + 18,
            13,
            new Color(255, 206, 75, 245));

        DrawTextCrisp(
            font,
            "HOW TO VIBE CODER",
            titleX + 2,
            py + 38,
            40,
            new Color(180, 16, 68, 220));
        DrawTextCrisp(
            font,
            "HOW TO VIBE CODER",
            titleX,
            py + 36,
            40,
            new Color(255, 232, 92, 255));

        Raylib.DrawRectangle(titleX, py + 82, pw - 138, 28, new Color(168, 20, 62, 235));
        Raylib.DrawRectangleLines(titleX, py + 82, pw - 138, 28, new Color(255, 215, 78, 240));
        DrawTextCrisp(
            font,
            "ШТУРМ НЕБОСКРЁБА: ЭТАЖ 1 (БЕЛАЯ АКУЛА IT)",
            titleX + 10,
            py + 87,
            17,
            Color.White);

        Rectangle taglineBox = new(px + 24, py + 124, pw - 48, 52);
        Raylib.DrawRectangleRec(taglineBox, new Color(26, 20, 38, 245));
        Raylib.DrawRectangle(px + 24, py + 124, 5, 52, new Color(255, 208, 64, 255));
        Raylib.DrawRectangleLinesEx(taglineBox, 1.5f, new Color(95, 75, 130, 190));

        DrawTextCrisp(
            font,
            "«Ты решал вопросы паяльником в 94-м.",
            px + 40,
            py + 131,
            16,
            new Color(255, 236, 175, 255));
        DrawTextCrisp(
            font,
            "  Теперь у тебя IT-галера в 2026-м.»",
            px + 40,
            py + 151,
            16,
            new Color(120, 235, 255, 255));

        DrawTextCrisp(
            font,
            "ГЛАВНОЕ МЕНЮ (МЫШЬ ИЛИ КЛАВИШИ 1..5 / ENTER):",
            px + 26,
            py + 194,
            14,
            new Color(210, 190, 230, 235));
        Raylib.DrawRectangle(px + 26, py + 216, pw - 52, 2, new Color(235, 36, 92, 180));

        int btnStartX = px + 26;
        int btnStartY = py + 232;
        int btnW = pw - 52;
        int btnH = 58;
        int btnGap = 11;

        for (int i = 0; i < MainMenuButtonLabels.Length; i++)
        {
            float hover = _mainHoverAnim[i];
            int shiftX = (int)(hover * 8f);
            Rectangle btnRect = new(btnStartX + shiftX, btnStartY + i * (btnH + btnGap), btnW - shiftX, btnH);

            bool isPrimary = i == 0;
            bool isSelected = _selectedMainIndex == i;

            Color baseBg = isPrimary
                ? new Color(145, 20, 56, 245)
                : new Color(26, 22, 40, 242);

            Color hoverBg = isPrimary
                ? new Color(210, 32, 82, 255)
                : new Color(64, 28, 62, 252);

            Color bg = LerpColor(baseBg, hoverBg, hover);
            Color border = isSelected
                ? new Color(255, 222, 75, 255)
                : (isPrimary ? new Color(255, 165, 95, 235) : new Color(92, 78, 128, 215));

            Raylib.DrawRectangleRec(btnRect, bg);

            int stripeW = isSelected ? 9 : 5;
            Color stripeColor = isPrimary || isSelected
                ? new Color(255, 215, 55, 255)
                : new Color(215, 38, 92, 230);
            Raylib.DrawRectangle((int)btnRect.X, (int)btnRect.Y, stripeW, (int)btnRect.Height, stripeColor);

            int badgeW = 38;
            int badgeH = 26;
            int badgeX = (int)(btnRect.X + btnRect.Width - badgeW - 12);
            int badgeY = (int)btnRect.Y + 8;
            Raylib.DrawRectangle(badgeX, badgeY, badgeW, badgeH, new Color(12, 10, 20, 215));
            Raylib.DrawRectangleLines(badgeX, badgeY, badgeW, badgeH, border);
            DrawTextCrisp(font, $"{i + 1}", badgeX + 14, badgeY + 5, 15, new Color(255, 220, 88, 255));

            Raylib.DrawRectangleLinesEx(btnRect, isSelected ? 2.5f : 1.5f, border);

            DrawTextCrisp(
                font,
                MainMenuButtonLabels[i],
                (int)btnRect.X + 20,
                (int)btnRect.Y + 8,
                21,
                isSelected ? new Color(255, 248, 195, 255) : Color.White);

            DrawTextCrisp(
                font,
                MainMenuButtonSubtitles[i],
                (int)btnRect.X + 20,
                (int)btnRect.Y + 35,
                13,
                new Color(195, 215, 240, 225));
        }
    }

    private static void Draw90sAuthorityCrest(Font font, int x, int y)
    {
        int w = 80;
        int h = 92;

        Raylib.DrawRectangle(x, y, w, h, new Color(155, 16, 54, 255));
        Raylib.DrawRectangleLinesEx(new Rectangle(x, y, w, h), 3f, new Color(255, 212, 64, 255));
        Raylib.DrawRectangleLinesEx(new Rectangle(x + 4, y + 4, w - 8, h - 8), 1.5f, new Color(255, 238, 145, 220));

        Raylib.DrawTriangle(
            new Vector2(x + 12, y + 48),
            new Vector2(x + 40, y + 84),
            new Vector2(x + 40, y + 48),
            new Color(28, 22, 36, 255));
        Raylib.DrawTriangle(
            new Vector2(x + 68, y + 48),
            new Vector2(x + 40, y + 48),
            new Vector2(x + 40, y + 84),
            new Color(28, 22, 36, 255));

        Raylib.DrawRectangle(x + 18, y + 26, 44, 12, new Color(255, 208, 52, 255));
        Raylib.DrawTriangle(
            new Vector2(x + 18, y + 26),
            new Vector2(x + 30, y + 26),
            new Vector2(x + 22, y + 12),
            new Color(255, 225, 82, 255));
        Raylib.DrawTriangle(
            new Vector2(x + 32, y + 26),
            new Vector2(x + 48, y + 26),
            new Vector2(x + 40, y + 9),
            new Color(255, 235, 110, 255));
        Raylib.DrawTriangle(
            new Vector2(x + 50, y + 26),
            new Vector2(x + 62, y + 26),
            new Vector2(x + 58, y + 12),
            new Color(255, 225, 82, 255));

        DrawTextCrisp(font, "1994", x + 21, y + 42, 16, new Color(255, 228, 92, 255));
        DrawTextCrisp(font, "BOSS", x + 21, y + 64, 15, Color.White);
    }

    private static void DrawRightAuthorityBriefingPanel(Font font, Rectangle panel)
    {
        int px = (int)panel.X;
        int py = (int)panel.Y;
        int pw = (int)panel.Width;
        int ph = (int)panel.Height;

        Raylib.DrawRectangle(px + 6, py + 8, pw, ph, new Color(0, 0, 0, 175));
        Raylib.DrawRectangle(px, py, pw, ph, new Color(14, 18, 30, 234));
        Raylib.DrawRectangleGradientV(px, py, pw, 90, new Color(36, 82, 138, 165), new Color(14, 18, 30, 10));

        Raylib.DrawRectangleLinesEx(panel, 2.5f, new Color(255, 204, 68, 245));
        Raylib.DrawRectangleLinesEx(new Rectangle(px + 5, py + 5, pw - 10, ph - 10), 1f, new Color(75, 195, 255, 165));

        Raylib.DrawRectangle(px + 18, py + 16, pw - 36, 46, new Color(138, 18, 54, 240));
        Raylib.DrawRectangleLines(px + 18, py + 16, pw - 36, 46, new Color(255, 215, 72, 255));

        DrawTextCrisp(
            font,
            "ПАМЯТКА АВТОРИТЕТА - КАК ДЕРЖАТЬ ЭТАЖ",
            px + 30,
            py + 22,
            18,
            new Color(255, 232, 95, 255));
        DrawTextCrisp(
            font,
            "Четыре шага от засады у Макбука до штурма Босса Центрального Лифта:",
            px + 30,
            py + 42,
            13,
            new Color(235, 225, 245, 240));

        int cardX = px + 20;
        int cardY = py + 74;
        int cardW = pw - 40;
        int cardH = 88;
        int cardGap = 10;

        DrawBriefingStepCard(
            font,
            new Rectangle(cardX, cardY + 0 * (cardH + cardGap), cardW, cardH),
            "1. ПРИМАНКА",
            "ХАЛЯВНЫЙ МАКБУК В ЦЕНТРЕ ЗАЛА",
            "Включи подписку на Макбуке [E] и спрячься за диван/фикус [C].",
            "Не свети стволом раньше времени, иначе шкала тревожности спугнёт джуна!",
            new Color(42, 215, 135, 255));

        DrawBriefingStepCard(
            font,
            new Rectangle(cardX, cardY + 1 * (cardH + cardGap), cardW, cardH),
            "2. ВОСПИТАНИЕ",
            "КОГДА СЯДЕТ ЖАТЬ ACCEPT ALL",
            "Когда вайб-кодер сядет жать Accept All, бей Ремнём [1], Паяльником [2],",
            "Micro-Uzi [3] или Обрезом [4], пока спесь не упадёт до нуля!",
            new Color(255, 185, 52, 255));

        DrawBriefingStepCard(
            font,
            new Rectangle(cardX, cardY + 2 * (cardH + cardGap), cardW, cardH),
            "3. КАПИТАЛ",
            "БУДКА ВЕРОНИКИ ИЛИ СТОЛЫ ГАЛЕРЫ",
            "Сдай покорного кодера HR-Тарологу Веронике за кэш (+$150..$600)",
            "или посади за стол Галеры (+$20/сек). Если залип в Твиттере - лечи ремнём!",
            new Color(215, 92, 255, 255));

        DrawBriefingStepCard(
            font,
            new Rectangle(cardX, cardY + 3 * (cardH + cardGap), cardW, cardH),
            "4. БОСС ЛИФТА",
            "КЛЮЧ ЗА $1000 И СБЕЖАВШИЙ ИИ-АГЕНТ",
            "Нафармь $1000 на Мини-Боссе SaaS, купи ключ и вызови Сбежавшего ИИ-Агента!",
            "Разнеси железный шкаф v0.1, забери Секретный Чат-Ган и открой 2-й этаж!",
            new Color(255, 68, 98, 255));

        int loadY = cardY + 4 * (cardH + cardGap) + 4;
        int loadH = ph - (loadY - py) - 16;
        if (loadH >= 64)
        {
            Rectangle loadBox = new(cardX, loadY, cardW, loadH);
            Raylib.DrawRectangleRec(loadBox, new Color(20, 24, 38, 245));
            Raylib.DrawRectangleLinesEx(loadBox, 1.5f, new Color(255, 208, 72, 210));

            DrawTextCrisp(
                font,
                "СТАТУС СОХРАНЕНИЙ И АКТИВНЫХ МОДОВ:",
                cardX + 12,
                loadY + 8,
                14,
                new Color(255, 215, 82, 255));

            GameSaveData? lastSave = _cachedSlots[1] ?? _cachedSlots[0];
            string saveLine = lastSave != null
                ? $"Последнее сохранение: {lastSave.SlotTitle} (${lastSave.Cash}, Галера: {lastSave.SeatedGalleyCoders}/4, {lastSave.SavedAt})"
                : "Сохранений пока нет - нажми [НОВАЯ ИГРА], чтобы ворваться на 1-й этаж!";

            DrawTextCrisp(font, saveLine, cardX + 12, loadY + 30, 13, new Color(210, 235, 255, 245));
            DrawTextCrisp(
                font,
                $"Папка сохранений: saves/   |   Папка модов: mods/ ({InstalledMods.FindAll(m => m.Enabled).Count} из {InstalledMods.Count} ВКЛ)",
                cardX + 12,
                loadY + 50,
                13,
                new Color(255, 195, 220, 245));
        }
    }

    private static void DrawBriefingStepCard(
        Font font,
        Rectangle rect,
        string stepBadge,
        string rightTag,
        string line1,
        string line2,
        Color accentColor)
    {
        int x = (int)rect.X;
        int y = (int)rect.Y;
        int w = (int)rect.Width;
        int h = (int)rect.Height;

        Raylib.DrawRectangle(x, y, w, h, new Color(22, 28, 44, 242));
        Raylib.DrawRectangle(x, y, 7, h, accentColor);
        Raylib.DrawRectangleLinesEx(rect, 1.5f, new Color(accentColor.R, accentColor.G, accentColor.B, (byte)195));

        int badgeW = 152;
        Raylib.DrawRectangle(x + 14, y + 8, badgeW, 24, accentColor);
        DrawTextCrisp(font, stepBadge, x + 22, y + 12, 15, new Color(12, 14, 20, 255));

        DrawTextCrisp(font, rightTag, x + 176, y + 12, 13, accentColor);
        DrawTextCrisp(font, line1, x + 16, y + 38, 14, Color.White);
        DrawTextCrisp(font, line2, x + 16, y + 59, 13, new Color(185, 205, 230, 235));
    }

    private static void DrawBottomFooterBar(Font font, int screenW, int screenH)
    {
        int barH = 34;
        int by = screenH - barH;
        Raylib.DrawRectangle(0, by, screenW, barH, new Color(12, 10, 20, 235));
        Raylib.DrawRectangle(0, by, screenW, 2, new Color(255, 205, 68, 210));

        string controls = "УПРАВЛЕНИЕ НА ЭТАЖЕ: [WASD] Ходьба  |  [ЛКМ] Огонь/Удар  |  [1..4] Оружие 90-х  |  [E] Взаимодействие  |  [C] Засада  |  [ESC] Меню Паузы";
        DrawTextCrisp(font, controls, 20, by + 9, 14, new Color(235, 230, 245, 245));
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

        DrawTextCrisp(
            font,
            $"Папка модов: {ModsDirectory}",
            wx + 32,
            wy + winH - 36,
            14,
            new Color(255, 215, 95, 240));
    }

    private static void DrawSettingsSubPanel(Font font, int screenW, int screenH)
    {
        Raylib.DrawRectangle(0, 0, screenW, screenH, new Color(6, 5, 14, 215));

        int winW = 860;
        int winH = 550;
        int wx = (screenW - winW) / 2;
        int wy = (screenH - winH) / 2;

        Rectangle winRect = new(wx, wy, winW, winH);
        Raylib.DrawRectangleRec(winRect, new Color(16, 14, 28, 252));
        Raylib.DrawRectangle(wx, wy, winW, 64, new Color(138, 18, 54, 250));
        Raylib.DrawRectangleLinesEx(winRect, 3f, new Color(255, 212, 68, 255));

        DrawTextCrisp(
            font,
            "НАСТРОЙКИ И АУДИО-МИКШЕР",
            wx + 28,
            wy + 20,
            23,
            new Color(255, 232, 92, 255));

        Rectangle closeBtn = new(wx + winW - 160, wy + 14, 140, 38);
        Raylib.DrawRectangleRec(closeBtn, new Color(82, 18, 42, 250));
        Raylib.DrawRectangleLinesEx(closeBtn, 2f, new Color(255, 195, 95, 245));
        DrawTextCrisp(font, "[ НАЗАД / ESC ]", (int)closeBtn.X + 12, (int)closeBtn.Y + 10, 15, Color.White);

        string[] labels =
        {
            "Общая громкость (Master Volume)",
            "Громкость музыки 90-х (Music Volume)",
            "Громкость оружия (Weapon SFX)",
            "Громкость голосов и реплик (Voice Volume)",
            "Чувствительность мыши (Mouse Sensitivity)",
            "Поле зрения камеры (3D FOV)"
        };

        float[] normVals =
        {
            LoreAndMenusUI.MasterVolume,
            LoreAndMenusUI.MusicVolume,
            LoreAndMenusUI.WeaponSfxVolume,
            LoreAndMenusUI.VoiceVolume,
            Math.Clamp((LoreAndMenusUI.MouseSensitivity - 0.05f) / 0.45f, 0f, 1f),
            Math.Clamp((LoreAndMenusUI.CameraFov - 60f) / 40f, 0f, 1f)
        };

        string[] valueTexts =
        {
            $"{(int)(LoreAndMenusUI.MasterVolume * 100)}%",
            $"{(int)(LoreAndMenusUI.MusicVolume * 100)}%",
            $"{(int)(LoreAndMenusUI.WeaponSfxVolume * 100)}%",
            $"{(int)(LoreAndMenusUI.VoiceVolume * 100)}%",
            $"{LoreAndMenusUI.MouseSensitivity:0.00}",
            $"{(int)LoreAndMenusUI.CameraFov}°"
        };

        int sliderX = wx + 360;
        int sliderW = 340;
        int rowStartY = wy + 92;
        int rowStepY = 54;

        for (int i = 0; i < 6; i++)
        {
            int ry = rowStartY + i * rowStepY;
            DrawTextCrisp(font, labels[i], wx + 34, ry + 8, 16, Color.White);

            Rectangle track = new(sliderX, ry + 6, sliderW, 24);
            Raylib.DrawRectangleRec(track, new Color(28, 32, 48, 255));
            Raylib.DrawRectangle(sliderX, ry + 6, (int)(sliderW * normVals[i]), 24, new Color(218, 36, 92, 255));
            Raylib.DrawRectangleLinesEx(track, 2f, new Color(255, 212, 72, 230));

            DrawTextCrisp(font, valueTexts[i], sliderX + sliderW + 22, ry + 8, 17, new Color(255, 228, 88, 255));
        }

        // Переключатель Полноэкранного режима
        DrawTextCrisp(font, "Режим отображения окна (Fullscreen):", wx + 34, wy + 432, 16, Color.White);
        Rectangle fullscreenBtn = new(wx + 360, wy + 422, 240, 42);
        Raylib.DrawRectangleRec(fullscreenBtn, IsFullscreenEnabled ? new Color(28, 148, 88, 250) : new Color(52, 44, 78, 250));
        Raylib.DrawRectangleLinesEx(fullscreenBtn, 2f, new Color(255, 215, 82, 245));
        DrawTextCrisp(
            font,
            IsFullscreenEnabled ? "[ ПОЛНЫЙ ЭКРАН: ВКЛ ]" : "[ ОКОННЫЙ РЕЖИМ ]",
            (int)fullscreenBtn.X + 22,
            (int)fullscreenBtn.Y + 12,
            16,
            Color.White);
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

    private static void GetMainMenuLayout(int screenW, int screenH, out Rectangle leftPanel, out Rectangle rightPanel)
    {
        int topMargin = 46;
        int bottomMargin = 46;
        int availH = Math.Max(540, screenH - topMargin - bottomMargin);
        int panelH = Math.Min(588, availH);
        int py = topMargin + (availH - panelH) / 2;

        int totalMarginX = 44;
        int gap = 24;
        int totalW = Math.Max(960, screenW - totalMarginX * 2);

        int leftW = (int)(totalW * 0.51f);
        int rightW = totalW - leftW - gap;

        int startX = (screenW - totalW) / 2;
        leftPanel = new Rectangle(startX, py, leftW, panelH);
        rightPanel = new Rectangle(startX + leftW + gap, py, rightW, panelH);
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
