#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public static class Program
{
    private const int ScreenWidth = 1280;
    private const int ScreenHeight = 720;

    private static SkyscraperFloor1World _world = null!;
    private static WeaponAndCombatView _weapons = null!;
    private static Font _uiFont;

    // Player state
    private static Vector3 _playerPos = new(0f, 1.72f, 12.2f);
    private static float _playerYaw = MathF.PI; // Looking North (-Z) toward Central MacBook & Elevator Core
    private static float _playerPitch = -0.05f;
    private static bool _isCrouching = false;
    private static float _walkBobPhase = 0f;

    private static int _playerCash = 450;
    private static int _playerHp = 100;
    private static int _playerMaxHp = 100;
    private static float _weaponFireCooldown = 0f;
    private static string _hudStatusBanner = "ДОБРО ПОЖАЛОВАТЬ В CLOUD ENTROPIC TOWER (ЭТАЖ 1)! Включи Макбук [E] и лови вайб-кодеров!";
    private static float _hudStatusTimer = 6.0f;

    public static int Main(string[] args)
    {
        bool autoTest = false;
        foreach (string a in args)
        {
            if (string.Equals(a, "--autotest", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "-autotest", StringComparison.OrdinalIgnoreCase))
            {
                autoTest = true;
            }
        }

        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint);
        Raylib.InitWindow(ScreenWidth, ScreenHeight, "HOW TO VIBE CODER - ЭТАЖ 1: CLOUD ENTROPIC TOWER (1994 -> 2026)");
        Raylib.SetTargetFPS(60);
        Raylib.SetExitKey(KeyboardKey.Null);

        _uiFont = LoadCyrillicFont();

        AudioMusicSystem.Initialize();
        _world = new SkyscraperFloor1World();
        _weapons = new WeaponAndCombatView();

        WireUpSystems();

        if (autoTest)
        {
            RunAutomatedScreenshotSuite();
            AudioMusicSystem.Shutdown();
            if (_uiFont.Texture.Id != 0)
            {
                Raylib.UnloadFont(_uiFont);
            }
            Raylib.CloseWindow();
            return 0;
        }

        bool shouldExit = false;
        while (!Raylib.WindowShouldClose() && !shouldExit)
        {
            float dt = Math.Clamp(Raylib.GetFrameTime(), 0.001f, 0.05f);
            UpdateFrame(dt, ref shouldExit);
            DrawFrame();
        }

        AudioMusicSystem.Shutdown();
        if (_uiFont.Texture.Id != 0)
        {
            Raylib.UnloadFont(_uiFont);
        }
        Raylib.CloseWindow();
        return 0;
    }

    private static void WireUpSystems()
    {
        LoreAndMenusUI.ActiveWeaponView = _weapons;
        LoreAndMenusUI.GetPlayerMoney = () => _playerCash;
        LoreAndMenusUI.TrySpendPlayerMoney = TrySpendCash;
        LoreAndMenusUI.OnGrantMoneyRequested = amount =>
        {
            _playerCash += amount;
            SetHudBanner($"ТЕСТ-БОНУС АВТОРИТЕТА: +${amount} НА КАРМАН! Баланс: ${_playerCash}");
        };
        LoreAndMenusUI.OnUnlockAllWeaponsRequested = () =>
        {
            _weapons.UnlockedSolderingIron = true;
            _weapons.UnlockedMicroUzi = true;
            _weapons.UnlockedSawedOff = true;
            _weapons.UnlockedChatGun = true;
            SetHudBanner("ОТКРЫТ ВЕСЬ АРСЕНАЛ 90-Х: РЕМЕНЬ [1], ПАЯЛЬНИК [2], MICRO-UZI [3], ОБРЕЗ/ЧАТ-ГАН [4]!");
        };
        LoreAndMenusUI.OnResetFloorRequested = ResetNewGameFloor;

        MainMenuAndPauseUI.OnStartNewGameRequested = ResetNewGameFloor;
        MainMenuAndPauseUI.OnStartGameRequested = () =>
        {
            Raylib.DisableCursor();
        };
        MainMenuAndPauseUI.OnResumeGameRequested = () =>
        {
            Raylib.DisableCursor();
        };
        MainMenuAndPauseUI.OnReturnToMainMenuRequested = () =>
        {
            Raylib.EnableCursor();
        };
        MainMenuAndPauseUI.OnExitRequested = () =>
        {
            MainMenuAndPauseUI.ShouldExitGame = true;
        };

        MainMenuAndPauseUI.CaptureCurrentGameState = () =>
        {
            int seatedCount = 0;
            for (int i = 0; i < _world.GalleyDesks.Count; i++)
            {
                if (_world.GalleyDesks[i].IsOccupied) seatedCount++;
            }

            return new GameSaveData
            {
                Cash = _playerCash,
                UnlockedWeapons = new[]
                {
                    true,
                    _weapons.UnlockedSolderingIron,
                    _weapons.UnlockedMicroUzi,
                    _weapons.UnlockedSawedOff || _weapons.UnlockedChatGun
                },
                CurrentWeaponSlot = (int)_weapons.CurrentWeapon - 1,
                EquippedSkinId = LoreAndMenusUI.LastWonSkin?.Id ?? -1,
                SeatedGalleyCoders = seatedCount,
                HasElevatorKey = _world.HasElevatorKey,
                MiniBossDefeated = _world.TotalCodersSoldToHr > 0,
                ElevatorBossDefeated = _world.ElevatorBoss.IsDefeated,
                FloorProgressLabel = _world.IsFloor2Unlocked
                    ? "Этаж 1 ЗАЧИЩЕН - Лифт на 2-й этаж открыт!"
                    : "Этаж 1 - Опенспейс (Cloud Entropic Tower)",
                PlayerPosX = _playerPos.X,
                PlayerPosY = _playerPos.Y,
                PlayerPosZ = _playerPos.Z
            };
        };

        MainMenuAndPauseUI.ApplyLoadedGameState = data =>
        {
            _playerCash = data.Cash;
            if (data.UnlockedWeapons != null && data.UnlockedWeapons.Length >= 4)
            {
                _weapons.UnlockedSolderingIron = data.UnlockedWeapons[1];
                _weapons.UnlockedMicroUzi = data.UnlockedWeapons[2];
                _weapons.UnlockedSawedOff = data.UnlockedWeapons[3];
            }
            _world.HasElevatorKey = data.HasElevatorKey;
            _world.IsFloor2Unlocked = data.ElevatorBossDefeated;
            _world.ElevatorBoss.IsDefeated = data.ElevatorBossDefeated;
            _playerPos = new Vector3(data.PlayerPosX, Math.Max(1.2f, data.PlayerPosY), data.PlayerPosZ);
        };
    }

    private static void ResetNewGameFloor()
    {
        _world.InitializeFloorLayout();
        _playerPos = new Vector3(0f, 1.72f, 12.2f);
        _playerYaw = MathF.PI;
        _playerPitch = -0.04f;
        _isCrouching = false;
        _playerCash = 450;
        _playerHp = 100;
        _weapons.CurrentWeapon = WeaponType.Belt;
        SetHudBanner("ШТУРМ 1-ГО ЭТАЖА НАЧАЛСЯ! Включи Макбук [E] в центре зала или осмотри будку HR-Таролога Вероники!");
    }

    private static bool TrySpendCash(int cost)
    {
        if (_playerCash < cost)
            return false;
        _playerCash -= cost;
        return true;
    }

    private static void GrantCash(int amount, string sourceLabel)
    {
        _playerCash += amount;
        SetHudBanner($"+${amount} ({sourceLabel}) | Баланс: ${_playerCash}");
    }

    private static void SetHudBanner(string text, float duration = 4.2f)
    {
        _hudStatusBanner = text;
        _hudStatusTimer = duration;
    }

    private static Camera3D BuildPlayerCamera()
    {
        float eyeHeight = _isCrouching ? 1.12f : 1.72f;
        float bobY = MathF.Sin(_walkBobPhase * 2f) * 0.035f;
        Vector3 eyePos = new(_playerPos.X, eyeHeight + bobY, _playerPos.Z);

        Vector3 forward = new(
            MathF.Sin(_playerYaw) * MathF.Cos(_playerPitch),
            MathF.Sin(_playerPitch),
            MathF.Cos(_playerYaw) * MathF.Cos(_playerPitch));

        return new Camera3D
        {
            Position = eyePos,
            Target = eyePos + forward,
            Up = Vector3.UnitY,
            FovY = Math.Clamp(LoreAndMenusUI.CameraFov, 60f, 100f),
            Projection = CameraProjection.Perspective
        };
    }

    private static void UpdateFrame(float dt, ref bool shouldExit)
    {
        bool bossFightMusic = _world.ElevatorBoss.IsActive ||
                              _world.ActiveCoders.Exists(c => c.IsMiniBoss && c.State != CoderAIState.Subdued);
        AudioMusicSystem.UpdateMusic(bossFightMusic, dt);

        if (_hudStatusTimer > 0f)
        {
            _hudStatusTimer = MathF.Max(0f, _hudStatusTimer - dt);
        }

        // Update Main Menu & Pause UI first
        MainMenuAndPauseUI.Update(dt, ref shouldExit);
        if (shouldExit)
            return;

        if (MainMenuAndPauseUI.IsInMainMenu)
        {
            _world.MacBookHologramPhase += dt * 2.5f;
            _world.WarningBeaconPhase += dt * 5.0f;
            return;
        }

        // Update [N] Lore, [F8] Mixer, [K] Case Roulette
        if (!MainMenuAndPauseUI.IsPaused)
        {
            LoreAndMenusUI.Update(dt, _weapons);
        }

        if (MainMenuAndPauseUI.IsMenuBlockingGameplay)
        {
            if (Raylib.IsCursorHidden())
            {
                Raylib.EnableCursor();
            }
            return;
        }

        if (!Raylib.IsCursorHidden())
        {
            Raylib.DisableCursor();
        }

        // Quick money hotkey [F9]
        if (Raylib.IsKeyPressed(KeyboardKey.F9))
        {
            GrantCash(2000, "Чит-бонус Авторитета [F9]");
            AudioMusicSystem.PlayTarotCash();
        }

        // Weapon slots [1..4]
        if (Raylib.IsKeyPressed(KeyboardKey.One)) _weapons.SelectSlot(1);
        if (Raylib.IsKeyPressed(KeyboardKey.Two)) _weapons.SelectSlot(2);
        if (Raylib.IsKeyPressed(KeyboardKey.Three)) _weapons.SelectSlot(3);
        if (Raylib.IsKeyPressed(KeyboardKey.Four)) _weapons.SelectSlot(4);

        // Ambush crouch toggle [C] or Left Control
        if (Raylib.IsKeyPressed(KeyboardKey.C) || Raylib.IsKeyPressed(KeyboardKey.LeftControl))
        {
            _isCrouching = !_isCrouching;
        }

        // Mouse look
        Vector2 md = Raylib.GetMouseDelta();
        float sens = LoreAndMenusUI.MouseSensitivity * 0.015f;
        _playerYaw -= md.X * sens;
        _playerPitch = Math.Clamp(_playerPitch - md.Y * sens, -1.25f, 1.25f);

        // Keyboard turning fallback (Q/Left/Right arrows)
        if (Raylib.IsKeyDown(KeyboardKey.Left)) _playerYaw += 1.8f * dt;
        if (Raylib.IsKeyDown(KeyboardKey.Right)) _playerYaw -= 1.8f * dt;

        // WASD movement across the 76x76m Skyscraper Floor
        Vector3 forwardXZ = new(MathF.Sin(_playerYaw), 0f, MathF.Cos(_playerYaw));
        Vector3 rightXZ = new(-forwardXZ.Z, 0f, forwardXZ.X);
        Vector3 moveDir = Vector3.Zero;

        if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up)) moveDir += forwardXZ;
        if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down)) moveDir -= forwardXZ;
        if (Raylib.IsKeyDown(KeyboardKey.D)) moveDir += rightXZ;
        if (Raylib.IsKeyDown(KeyboardKey.A)) moveDir -= rightXZ;

        bool isMoving = moveDir.LengthSquared() > 0.01f;
        if (isMoving)
        {
            moveDir = Vector3.Normalize(moveDir);
            float moveSpeed = _isCrouching ? 4.4f : (Raylib.IsKeyDown(KeyboardKey.LeftShift) ? 8.8f : 6.6f);
            Vector3 desired = _playerPos + moveDir * moveSpeed * dt;
            _playerPos = _world.ResolvePlayerCollision(desired);
            _walkBobPhase += dt * (moveSpeed * 1.35f);
        }

        // Contextual [E] Interaction
        if (Raylib.IsKeyPressed(KeyboardKey.E))
        {
            string interactMsg = _world.HandleInteractKeyE(_playerPos, TrySpendCash, GrantCash);
            SetHudBanner(interactMsg, 4.8f);
        }

        // Weapon firing (LMB)
        if (_weaponFireCooldown > 0f)
        {
            _weaponFireCooldown = MathF.Max(0f, _weaponFireCooldown - dt);
        }

        Camera3D cam = BuildPlayerCamera();
        bool holdingFire = Raylib.IsMouseButtonDown(MouseButton.Left);
        bool pressedFire = Raylib.IsMouseButtonPressed(MouseButton.Left);

        bool wantsFire = _weapons.CurrentWeapon == WeaponType.MicroUzi ? holdingFire : pressedFire;
        if (wantsFire && _weaponFireCooldown <= 0f)
        {
            _weaponFireCooldown = _weapons.CurrentWeapon switch
            {
                WeaponType.Belt => 0.34f,
                WeaponType.SolderingIron => 0.38f,
                WeaponType.MicroUzi => 0.095f,
                WeaponType.SawedOff => 0.62f,
                WeaponType.ChatGun => 0.28f,
                _ => 0.35f
            };

            PlayCurrentWeaponAudio(_weapons.CurrentWeapon);
            if (_world.ProcessPlayerAttack(cam, _weapons, GrantCash, out string combatMsg) && !string.IsNullOrEmpty(combatMsg))
            {
                SetHudBanner(combatMsg, 3.5f);
            }
        }

        _weapons.UpdateWeapons(dt, cam, isMoving, holdingFire);

        _world.UpdateWorld(
            dt,
            _playerPos,
            _isCrouching,
            onPassiveIncomeOrPayout: (amt, src) =>
            {
                _playerCash += amt;
            },
            onPlayerHitByProjectile: (dmg, label) =>
            {
                _playerHp = Math.Max(15, _playerHp - dmg);
                SetHudBanner($"ПРИЛЕТЕЛ БЛОК «{label}» (-{dmg} ХП)! Прячься за колонну или гаси в ответ!", 2.5f);
            });
    }

    private static void PlayCurrentWeaponAudio(WeaponType wt)
    {
        switch (wt)
        {
            case WeaponType.Belt:
                AudioMusicSystem.PlayBeltWhip();
                break;
            case WeaponType.SolderingIron:
                AudioMusicSystem.PlaySolderSizzle();
                break;
            case WeaponType.MicroUzi:
                AudioMusicSystem.PlayUziShot();
                break;
            case WeaponType.SawedOff:
                AudioMusicSystem.PlayShotgunBlast();
                break;
            case WeaponType.ChatGun:
                AudioMusicSystem.PlayChatgunBeam();
                break;
        }
    }

    private static void DrawFrame()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(10, 12, 20, 255));

        if (MainMenuAndPauseUI.IsInMainMenu)
        {
            Camera3D menuCam = MainMenuAndPauseUI.GetMainMenuCamera((float)Raylib.GetTime(), new Vector3(0f, 1.5f, 3.5f), 15.5f, 4.6f);
            Raylib.BeginMode3D(menuCam);
            _world.DrawWorld3D(menuCam);
            MainMenuAndPauseUI.DrawMainMenu3DShowcaseExtras((float)Raylib.GetTime());
            Raylib.EndMode3D();

            MainMenuAndPauseUI.Draw(_uiFont, ScreenWidth, ScreenHeight);
            Raylib.EndDrawing();
            return;
        }

        Camera3D playerCam = BuildPlayerCamera();

        // 1. 3D Skyscraper World + Entities + Combat Particles
        Raylib.BeginMode3D(playerCam);
        _world.DrawWorld3D(playerCam);
        _weapons.DrawWeaponWorldEffects3D(playerCam);
        Raylib.EndMode3D();

        // 2. 2D World-to-screen tags, enemy HP/Anxiety bars, Boss bar
        _world.DrawWorldLabelsAndIndicators2D(playerCam, _uiFont);

        // 3. First-Person 90s Businessman Hands in Crimson Jacket & Active Weapon ViewModel
        _weapons.DrawFirstPersonHandsAndWeapon(playerCam, _walkBobPhase, _isCrouching, _uiFont);

        // 4. Tactical Gameplay HUD
        DrawGameplayHud2D();

        // 5. Modal Windows ([N] Lore, [F8] Audio Mixer, [K] Case Roulette) & [ESC] Pause Overlay
        LoreAndMenusUI.Draw(_uiFont, _playerCash);
        if (MainMenuAndPauseUI.IsPaused || MainMenuAndPauseUI.IsSaveLoadPanelOpen || MainMenuAndPauseUI.IsModsPanelOpen || MainMenuAndPauseUI.IsSettingsPanelOpen)
        {
            MainMenuAndPauseUI.Draw(_uiFont, ScreenWidth, ScreenHeight);
        }

        Raylib.EndDrawing();
    }

    private static void DrawGameplayHud2D()
    {
        int sw = ScreenWidth;
        int sh = ScreenHeight;
        int cx = sw / 2;
        int cy = sh / 2;

        // Crosshair
        Color crossCol = _world.IsPlayerInAmbush ? new Color(65, 255, 145, 235) : new Color(255, 225, 85, 235);
        Raylib.DrawCircleLines(cx, cy, 5f, crossCol);
        Raylib.DrawLine(cx - 10, cy, cx - 3, cy, crossCol);
        Raylib.DrawLine(cx + 3, cy, cx + 10, cy, crossCol);
        Raylib.DrawLine(cx, cy - 10, cx, cy - 3, crossCol);
        Raylib.DrawLine(cx, cy + 3, cx, cy + 10, crossCol);

        // Top-Left Authority Status Panel (Cash, HP, Galley Income, Ambush Status)
        Raylib.DrawRectangle(14, 12, 430, 108, new Color(12, 14, 26, 230));
        Raylib.DrawRectangleLinesEx(new Rectangle(14, 12, 430, 108), 2f, new Color(235, 38, 92, 245));

        LoreAndMenusUI.DrawTextCrisp(_uiFont, "CLOUD ENTROPIC TOWER // ЭТАЖ 1 (76x76M)", 26, 18, 15, new Color(255, 215, 75, 255));
        LoreAndMenusUI.DrawTextCrisp(_uiFont, $"БАЛАНС АВТОРИТЕТА: ${_playerCash}", 26, 38, 22, new Color(75, 255, 145, 255));

        int workingGalley = 0;
        for (int i = 0; i < _world.GalleyDesks.Count; i++)
        {
            if (_world.GalleyDesks[i].IsOccupied && !_world.GalleyDesks[i].IsSlackingInTwitter)
                workingGalley++;
        }

        string galleyTxt = $"ГАЛЕРА: {workingGalley}/4 СТОЛОВ (+${workingGalley * 20}/СЕК)  |  ЗДОРОВЬЕ: {_playerHp}%";
        LoreAndMenusUI.DrawTextCrisp(_uiFont, galleyTxt, 26, 66, 14, new Color(195, 225, 255, 245));

        string ambushTxt = _world.IsPlayerInAmbush
            ? $"РЕЖИМ ЗАСАДЫ АКТИВЕН ({_world.ActiveAmbushCoverName}) - НЕВИДИМ ДЛЯ КОДЕРОВ!"
            : "НА ВИДУ (Спрячься за диван/фикус или нажми [C] для засады)";
        Color ambushCol = _world.IsPlayerInAmbush ? new Color(65, 255, 145, 255) : new Color(255, 185, 75, 240);
        LoreAndMenusUI.DrawTextCrisp(_uiFont, ambushTxt, 26, 88, 13, ambushCol);

        // Top-Right Objective & Carried Coder Badge
        Raylib.DrawRectangle(sw - 424, 12, 410, 92, new Color(12, 14, 26, 230));
        Raylib.DrawRectangleLinesEx(new Rectangle(sw - 424, 12, 410, 92), 2f, new Color(0, 230, 255, 235));

        var baitInfo = SkyscraperFloor1World.GetBaitInfo(_world.SelectedBaitTier);
        LoreAndMenusUI.DrawTextCrisp(_uiFont, $"ПРИМАНКА МАКБУКА: {baitInfo.Name}", sw - 412, 18, 15, baitInfo.NeonColor);

        string carryLine = _world.CarriedCoder != null
            ? $"НА ПЛЕЧЕ: {_world.CarriedCoder.Name} (${_world.CarriedCoder.TarotPayoutValue}) -> Жми [E] у Вероники!"
            : (_world.HasElevatorKey
                ? "КЛЮЧ ОТ ЛИФТА ПОЛУЧЕН! Иди к Центральному Лифту [E]!"
                : "ЦЕЛЬ: Лови кодеров на Макбук [E], сдай Веронике и купи Ключ ($1000)");
        LoreAndMenusUI.DrawTextCrisp(_uiFont, carryLine, sw - 412, 42, 13, Color.White);
        LoreAndMenusUI.DrawTextCrisp(_uiFont, $"Сдано HR-Тарологу: {_world.TotalCodersSoldToHr} чел. | Ключ Лифта: {(_world.HasElevatorKey ? "ЕСТЬ" : "$1000")}", sw - 412, 66, 13, new Color(255, 215, 90, 240));

        // Voice Subtitle Banner if someone is speaking
        if (AudioMusicSystem.VoiceTimer > 0f && !string.IsNullOrEmpty(AudioMusicSystem.ActiveVoiceText))
        {
            int vw = 680;
            int vx = (sw - vw) / 2;
            int vy = sh - 138;
            Raylib.DrawRectangle(vx, vy, vw, 44, new Color(16, 12, 28, 235));
            Raylib.DrawRectangleLinesEx(new Rectangle(vx, vy, vw, 44), 2f, new Color(255, 205, 65, 245));
            LoreAndMenusUI.DrawTextCrisp(_uiFont, $"[ГОЛОС] {AudioMusicSystem.ActiveSpeaker}: «{AudioMusicSystem.ActiveVoiceText}»", vx + 14, vy + 12, 15, new Color(255, 240, 175, 255));
        }
        else if (_hudStatusTimer > 0f && !string.IsNullOrEmpty(_hudStatusBanner))
        {
            int bw = 720;
            int bx = (sw - bw) / 2;
            int by = sh - 134;
            Raylib.DrawRectangle(bx, by, bw, 36, new Color(14, 18, 32, 225));
            Raylib.DrawRectangleLinesEx(new Rectangle(bx, by, bw, 36), 1.5f, new Color(80, 235, 255, 230));
            LoreAndMenusUI.DrawTextCrisp(_uiFont, _hudStatusBanner, bx + 14, by + 9, 14, Color.White);
        }

        // Bottom Weapon Slots Bar & Hotkeys Strip
        int barW = 780;
        int barH = 52;
        int barX = (sw - barW) / 2;
        int barY = sh - 86;

        string[] slotNames =
        {
            "[1] РЕМЕНЬ БАТИ",
            "[2] ПАЯЛЬНИК 220В",
            "[3] MICRO-UZI 9MM",
            _weapons.UnlockedChatGun ? "[4] СЕКРЕТНЫЙ ЧАТ-ГАН" : "[4] ОБРЕЗ ДВУСТВОЛКИ"
        };

        int slotW = (barW - 24) / 4;
        for (int i = 0; i < 4; i++)
        {
            int sx = barX + i * (slotW + 8);
            bool active = ((int)_weapons.CurrentWeapon == i + 1) || (i == 3 && _weapons.CurrentWeapon == WeaponType.ChatGun);
            Color bg = active ? new Color(175, 24, 66, 245) : new Color(18, 22, 36, 230);
            Color border = active ? new Color(255, 220, 65, 255) : new Color(75, 95, 135, 200);

            Raylib.DrawRectangle(sx, barY, slotW, barH, bg);
            Raylib.DrawRectangleLinesEx(new Rectangle(sx, barY, slotW, barH), active ? 2.5f : 1.5f, border);
            LoreAndMenusUI.DrawTextCrisp(_uiFont, slotNames[i], sx + 12, barY + 9, 15, active ? new Color(255, 240, 150, 255) : Color.White);
            LoreAndMenusUI.DrawTextCrisp(_uiFont, active ? "В РУКАХ [ЛКМ]" : "ГОТОВ К БОЮ", sx + 12, barY + 30, 12, new Color(180, 215, 245, 230));
        }

        // Bottom Controls Hint Strip
        Raylib.DrawRectangle(0, sh - 28, sw, 28, new Color(10, 10, 18, 245));
        LoreAndMenusUI.DrawTextCrisp(
            _uiFont,
            "[WASD] Ходьба | [ЛКМ] Атака | [E] Макбук/Кодер/Вероника/Лифт | [C] Засада | [K] Кейсы 90-х | [N] Лор и Чаты | [F8] Микшер | [F9] +$2000 | [ESC] Пауза",
            18,
            sh - 22,
            13,
            new Color(225, 232, 248, 245));
    }

    // =========================================================================
    // AUTOMATED 8-SCREENSHOT SUITE (`HowToVibeCoder.exe --autotest`)
    // =========================================================================
    private static void RunAutomatedScreenshotSuite()
    {
        string projectScreenshotsDir = Path.Combine(Directory.GetCurrentDirectory(), "screenshots");
        string binScreenshotsDir = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(projectScreenshotsDir);
        Directory.CreateDirectory(binScreenshotsDir);

        void CaptureShot(string fileName)
        {
            // Render 3 warmup frames so all animations/particles/UI settle cleanly
            for (int f = 0; f < 3; f++)
            {
                DrawFrame();
            }

            Image img = Raylib.LoadImageFromScreen();
            string path1 = Path.Combine(projectScreenshotsDir, fileName);
            string path2 = Path.Combine(binScreenshotsDir, fileName);
            Raylib.ExportImage(img, path1);
            if (!string.Equals(Path.GetFullPath(path1), Path.GetFullPath(path2), StringComparison.OrdinalIgnoreCase))
            {
                Raylib.ExportImage(img, path2);
            }
            Raylib.UnloadImage(img);
            Console.WriteLine($"[AUTOTEST] Saved screenshot: {path1}");
        }

        // 0. 00_main_menu_classic.png - Classic PC Main Menu over live 3D Skyscraper Floor 1
        ResetNewGameFloor();
        MainMenuAndPauseUI.IsInMainMenu = true;
        MainMenuAndPauseUI.IsPaused = false;
        MainMenuAndPauseUI.IsSaveLoadPanelOpen = false;
        MainMenuAndPauseUI.IsModsPanelOpen = false;
        MainMenuAndPauseUI.IsSettingsPanelOpen = false;
        _world.ActivateMacBookTrap(SubscriptionBaitTier.CursorPro, _ => true, out _);
        CaptureShot("00_main_menu_classic.png");

        // Switch into active 3D gameplay
        MainMenuAndPauseUI.IsInMainMenu = false;
        LoreAndMenusUI.IsLoreOpen = false;
        LoreAndMenusUI.IsSettingsOpen = false;
        LoreAndMenusUI.IsCaseOpen = false;

        // 1. 01_office_hub_hr_tarot.png - South Base: HR Tarot Booth Veronika with candles & crystal ball, 90s Shop, Belt in crimson jacket hands
        ResetNewGameFloor();
        MainMenuAndPauseUI.IsInMainMenu = false;
        _weapons.SelectSlot(1); // Father's Leather Belt
        _playerPos = new Vector3(-4.2f, 1.72f, 11.4f);
        LookAtTarget(new Vector3(-5.8f, 1.45f, 15.2f));
        AudioMusicSystem.TriggerVoiceLine("HR-ТАРОЛОГ ВЕРОНИКА", "Расклад Таро готов! Приноси вайб-кодеров на разбор натальной карты!", (int)SpeakerVoiceType.VeronikaTarot);
        CaptureShot("01_office_hub_hr_tarot.png");

        // 2. 02_macbook_trap_ambush.png - Ambush behind Leather Sofa/Ficus, glowing MacBook Trap + Central Armored Elevator Shaft + Left/Right Forks + Walking Vibe Coder with Anxiety Bar
        ResetNewGameFloor();
        MainMenuAndPauseUI.IsInMainMenu = false;
        _weapons.SelectSlot(2); // 90s Soldering Iron with rising rosin smoke
        _isCrouching = true;
        _playerPos = new Vector3(-3.2f, 1.12f, 9.2f);
        LookAtTarget(new Vector3(0.0f, 1.35f, 2.5f));
        _world.ActivateMacBookTrap(SubscriptionBaitTier.CursorPro, _ => true, out _);
        var walkingCoder = _world.SpawnCoderForTier(SubscriptionBaitTier.CursorPro, forceSide: -1);
        walkingCoder.Position = new Vector3(-2.1f, 0f, 5.4f);
        walkingCoder.Anxiety = 38f;
        walkingCoder.SpeechBubble = "Ого, халявный Cursor Pro на Макбуке! Бегу жать Accept All!";
        walkingCoder.SpeechTimer = 5.0f;
        _world.UpdateAmbushState(_playerPos, _isCrouching);
        _weapons.UpdateWeapons(0.16f, BuildPlayerCamera(), isMoving: false, holdingFire: true);
        CaptureShot("02_macbook_trap_ambush.png");

        // 3. 03_uzi_combat_saas_miniboss.png - Firing Micro-Uzi (muzzle flash, brass shells, tracers, hitmarker) at SaaS Guru Mini-Boss + flying 3D "500 INTERNAL ERROR" & "API KEY LEAKED" blocks
        ResetNewGameFloor();
        MainMenuAndPauseUI.IsInMainMenu = false;
        _isCrouching = false;
        _weapons.SelectSlot(3); // Micro-Uzi
        _playerPos = new Vector3(0.0f, 1.72f, 9.4f);
        LookAtTarget(new Vector3(0.0f, 1.35f, 4.5f));
        _world.SelectedBaitTier = SubscriptionBaitTier.CloudEntropicMax;
        _world.IsMacBookTrapActive = true;
        var miniBoss = _world.SpawnCoderForTier(SubscriptionBaitTier.CloudEntropicMax, forceSide: 1);
        miniBoss.Position = new Vector3(0.2f, 0f, 4.5f);
        miniBoss.Hp = 420f;
        miniBoss.HitFlashTimer = 0.12f;
        _world.EnemyProjectiles.Add(new EnemyProjectile3D
        {
            Position = new Vector3(-0.75f, 1.55f, 6.6f),
            Velocity = new Vector3(0f, 0f, 4f),
            Radius = 0.44f,
            Life = 3.5f,
            MaxLife = 4.0f,
            Damage = 12,
            Label = "500 INTERNAL ERROR",
            PrimaryColor = new Color(235, 35, 55, 245),
            WireColor = new Color(255, 225, 70, 255)
        });
        _world.EnemyProjectiles.Add(new EnemyProjectile3D
        {
            Position = new Vector3(0.95f, 1.35f, 6.2f),
            Velocity = new Vector3(0f, 0f, 4f),
            Radius = 0.44f,
            Life = 3.5f,
            MaxLife = 4.0f,
            Damage = 12,
            Label = "API KEY LEAKED ($50K)",
            PrimaryColor = new Color(235, 35, 55, 245),
            WireColor = new Color(255, 225, 70, 255)
        });
        Camera3D uziCam = BuildPlayerCamera();
        _weapons.TriggerWeaponAttack(uziCam, new Vector3(0.2f, 1.35f, 4.5f), hitEnemy: true, damageDealt: 46, isCritical: true);
        _weapons.UpdateWeapons(0.02f, uziCam, isMoving: false, holdingFire: true);
        CaptureShot("03_uzi_combat_saas_miniboss.png");

        // 4. 04_hr_tarot_payout_and_galley.png - Selling captured coder to HR Tarot Veronika (+$$$ Cash Burst) + Working & Slacking Coders at Galley Desks
        ResetNewGameFloor();
        MainMenuAndPauseUI.IsInMainMenu = false;
        _weapons.SelectSlot(1);
        _world.CarriedCoder = new VibeCoderEntity
        {
            Id = 99,
            Archetype = CoderArchetype.SaasGuruMiniBoss,
            Name = "Гуру Курсора ($60K MRR)",
            TarotPayoutValue = 600,
            HoodieColor = new Color(245, 190, 25, 255)
        };
        _world.SellCarriedCoderToHrVeronika(GrantCash);

        // Populate Galley Desks (2 working + 1 slacking in Twitter!)
        _world.GalleyDesks[0].IsOccupied = true;
        _world.GalleyDesks[0].CoderName = "Джуниор Данила";
        _world.GalleyDesks[0].HoodieColor = new Color(65, 195, 255, 255);
        _world.GalleyDesks[0].IsSlackingInTwitter = false;

        _world.GalleyDesks[1].IsOccupied = true;
        _world.GalleyDesks[1].CoderName = "Промпт-Бро Артемий";
        _world.GalleyDesks[1].HoodieColor = new Color(180, 75, 255, 255);
        _world.GalleyDesks[1].IsSlackingInTwitter = true;

        _playerPos = new Vector3(1.5f, 1.72f, 10.2f);
        LookAtTarget(new Vector3(2.2f, 1.25f, 15.0f));
        CaptureShot("04_hr_tarot_payout_and_galley.png");

        // 5. 05_elevator_boss_escaped_agent.png - Open Central Armored Elevator + Escaped AI Agent v0.1 Boss throwing 40GB node_modules cubes
        ResetNewGameFloor();
        MainMenuAndPauseUI.IsInMainMenu = false;
        _weapons.SelectSlot(4); // Sawed-Off Shotgun / Chat-Gun
        _world.HasElevatorKey = true;
        _world.TrySummonElevatorBoss(_ => true, out _);
        _world.ElevatorBoss.Position = new Vector3(0.0f, 0f, -12.5f);
        _world.ElevatorBoss.Hp = 1480f;
        _world.ElevatorBoss.FacingYaw = 3.14159f; // Facing North toward player in North Boss Arena
        _world.SpawnNodeModulesCube(new Vector3(-0.8f, 1.8f, -13.8f), new Vector3(0f, 1.5f, -18.5f));
        _world.SpawnNodeModulesCube(new Vector3(0.9f, 1.6f, -14.4f), new Vector3(0f, 1.5f, -18.5f));
        _playerPos = new Vector3(0.0f, 1.72f, -18.2f);
        LookAtTarget(new Vector3(0.0f, 1.55f, -11.5f));
        Camera3D bossCam = BuildPlayerCamera();
        _weapons.TriggerWeaponAttack(bossCam, new Vector3(0.0f, 1.55f, -12.5f), hitEnemy: true, damageDealt: 165, isCritical: true);
        _weapons.UpdateWeapons(0.03f, bossCam, isMoving: false, holdingFire: true);
        CaptureShot("05_elevator_boss_escaped_agent.png");

        // 6. 06_lore_chats_window_n.png - Open Corporate Leak & Slack/TG Chats Window [N]
        LoreAndMenusUI.IsLoreOpen = true;
        LoreAndMenusUI.ActiveLoreTab = 0;
        LoreAndMenusUI.IsSettingsOpen = false;
        LoreAndMenusUI.IsCaseOpen = false;
        CaptureShot("06_lore_chats_window_n.png");

        // 7. 07_settings_mixer_and_mods.png (and 07_settings_mixer_and_cases.png) - Settings / Audio Mixer [F8]
        LoreAndMenusUI.IsLoreOpen = false;
        LoreAndMenusUI.IsSettingsOpen = true;
        LoreAndMenusUI.IsCaseOpen = false;
        CaptureShot("07_settings_mixer_and_mods.png");
        CaptureShot("07_settings_mixer_and_cases.png");
    }

    private static void LookAtTarget(Vector3 target)
    {
        float eyeHeight = _isCrouching ? 1.12f : 1.72f;
        Vector3 eye = new(_playerPos.X, eyeHeight, _playerPos.Z);
        Vector3 d = Vector3.Normalize(target - eye);
        _playerYaw = MathF.Atan2(d.X, d.Z);
        _playerPitch = MathF.Asin(Math.Clamp(d.Y, -0.95f, 0.95f));
    }

    private static Font LoadCyrillicFont()
    {
        string[] candidates =
        {
            @"C:\Windows\Fonts\arialbd.ttf",
            @"C:\Windows\Fonts\segoeui.ttf",
            @"C:\Windows\Fonts\tahoma.ttf",
            @"C:\Windows\Fonts\arial.ttf"
        };

        List<int> codepoints = new(512);
        for (int c = 32; c <= 126; c++) codepoints.Add(c);
        for (int c = 0x0400; c <= 0x04FF; c++) codepoints.Add(c);
        // Helpful punctuation & symbols used in UI
        int[] extra = { 0x2013, 0x2014, 0x2022, 0x2026, 0x00AB, 0x00BB, 0x2190, 0x2192, 0x25C0, 0x25B6, 0x2116, 0x00B0 };
        codepoints.AddRange(extra);

        foreach (string fontPath in candidates)
        {
            if (File.Exists(fontPath))
            {
                Font f = Raylib.LoadFontEx(fontPath, 32, codepoints.ToArray(), codepoints.Count);
                if (f.Texture.Id != 0)
                {
                    Raylib.SetTextureFilter(f.Texture, TextureFilter.Bilinear);
                    return f;
                }
            }
        }

        return Raylib.GetFontDefault();
    }
}
