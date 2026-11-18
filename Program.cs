#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public static partial class Program
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
    private const int XpPerLevel = 1073;
    private static int _xp = 412;
    private static SkyscraperFloor1World _menuFloor = null!;
    private static float _menuTime;

    private static float _energy = 100;
    private static Font _serif;
    private static float _weaponFireCooldown = 0f;
    private static string _hudStatusBanner = "ДОБРО ПОЖАЛОВАТЬ В CLOUD ENTROPIC TOWER (ЭТАЖ 1)! Включи Макбук [E] и лови вайб-кодеров!";
    private static float _hudStatusTimer = 6.0f;

    public static int Main(string[] args)
    {
        bool HasFlag(string name) => args.Contains(name, StringComparer.OrdinalIgnoreCase);
        bool autoTest = HasFlag("--autotest") || HasFlag("-autotest");
        bool coopSmoke = HasFlag("--coop-smoke");
        bool gripTest = HasFlag("--grip-test");
        bool silent = args.Any(arg => arg.StartsWith('-') &&
            (arg.Contains("test", StringComparison.OrdinalIgnoreCase) ||
             arg.Contains("smoke", StringComparison.OrdinalIgnoreCase)));
        if (HasFlag("--regenerate-music"))
        {
            AudioMusicSystem.EnsureMusicFiles(Path.Combine(Environment.CurrentDirectory, "assets", "sounds"), force: true);
            return 0;
        }

        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint | (autoTest || gripTest ? ConfigFlags.HiddenWindow : 0));
        Raylib.InitWindow(ScreenWidth, ScreenHeight, "HOW TO VIBE CODER - ЭТАЖ 1: CLOUD ENTROPIC TOWER (1994 -> 2026)");
        Raylib.SetTargetFPS(60);
        Raylib.SetExitKey(KeyboardKey.Null);

        GameFonts.Load();
        _uiFont = GameFonts.Get(Typeface.Interface, 18);
        _serif = GameFonts.Get(Typeface.Serif, 24);
        MainMenuAndPauseUI.LoadArt(_serif);
        VisualEngine3D.Load();

        AudioMusicSystem.Initialize(silent);
        _world = new SkyscraperFloor1World();
        _menuFloor = new SkyscraperFloor1World { Partner = new PartnerAvatar(), ElevatorDoorOpenAmount = .62f };
        _menuFloor.Partner.Move(new(new(-1.82f,1.72f,10.62f),-1.72f,0,WeaponType.MicroUzi));
        _weapons = new WeaponAndCombatView();

        WireUpSystems();
        ConfigureCoop();
        int join=Array.IndexOf(args,"--join");
        if(join>=0)
        {
            MainMenuAndPauseUI.OpenJoinPanel();
            Net.Join(join+1<args.Length ? args[join+1] : "127.0.0.1");
        }
        else if(args.Contains("--host")) MainMenuAndPauseUI.StartNewOrResumeFloor();

        if (coopSmoke) RunCoopSmoke(join >= 0);
        else if (autoTest || gripTest) RunAutomatedScreenshotSuite();
        else
        {
            bool shouldExit = false;
            while (!Raylib.WindowShouldClose() && !shouldExit)
            {
                float dt = Math.Clamp(Raylib.GetFrameTime(), 0.001f, 0.05f);
                UpdateFrame(dt, ref shouldExit);
                DrawFrame();
            }
        }

        Net.Dispose();
        VisualEngine3D.Unload();
        MainMenuAndPauseUI.UnloadArt();
        GameFonts.Unload();
        AudioMusicSystem.Shutdown();
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
            if(Net.Guest) { Net.Send(new("bonus")); return; }
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
        LoreAndMenusUI.OnResetFloorRequested = () => { if(!Net.Guest) ResetNewGameFloor(); };
        LoreAndMenusUI.RequestCasePayment = () => {
            if(!Net.Guest) return false;
            if(!pendingCase) { pendingCase=true; Net.Send(new("case")); }
            return true;
        };

        MainMenuAndPauseUI.OnStartNewGameRequested = () => { ResetNewGameFloor(); StartHosting(); };
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
            Net.Dispose();
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
                Cash = _playerCash, Xp = _xp, Energy = _energy, Hp = _playerHp,
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
            ResetNewGameFloor();
            StartHosting();
            _playerCash = data.Cash;
            _xp = Math.Max(0,data.Xp); _energy = Math.Clamp(data.Energy,0,100); _playerHp = Math.Clamp(data.Hp,0,100);
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
        bot=awaitingSpawn=pendingCase=false; guestCarried=null; guestHp=100;
        _world = new SkyscraperFloor1World();
        AudioMusicSystem.VoiceTimer = 0;
        _playerPos = new Vector3(0f, 1.72f, 12.2f);
        _playerYaw = MathF.PI;
        _playerPitch = -0.04f;
        _isCrouching = false;
        _playerCash = 450;
        _playerHp = 100;
        _weapons = new WeaponAndCombatView();
        LoreAndMenusUI.ActiveWeaponView = _weapons;
        _xp = 412;
        _energy = 100;
        _weaponFireCooldown = _walkBobPhase = 0;
        SetHudBanner("Найди макбук в центре зала");
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
        _xp += Math.Max(0,amount / 5);
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
        Net.Pump(LocalPose,CaptureFloor);
        bool bossFightMusic = _world.ElevatorBoss.IsActive ||
                              _world.ActiveCoders.Exists(c => c.IsMiniBoss && c.State != CoderAIState.Subdued);
        AudioMusicSystem.UpdateMusic(bossFightMusic, dt, MainMenuAndPauseUI.IsInMainMenu);

        if (_hudStatusTimer > 0f)
        {
            _hudStatusTimer = MathF.Max(0f, _hudStatusTimer - dt);
        }

        bool wasModal = LoreAndMenusUI.IsAnyModalOpen;
        bool modalShortcut = Raylib.IsKeyPressed(KeyboardKey.F8) || Raylib.IsKeyPressed(KeyboardKey.K) || Raylib.IsKeyPressed(KeyboardKey.N);
        if (!MainMenuAndPauseUI.JoinPanel && (wasModal || modalShortcut))
        {
            if(Net.Ready && !MainMenuAndPauseUI.IsInMainMenu) TickFloor(dt);
            LoreAndMenusUI.Update(dt, _weapons);
            if (LoreAndMenusUI.IsAnyModalOpen && Raylib.IsCursorHidden()) Raylib.EnableCursor();
            return;
        }
        MainMenuAndPauseUI.Update(dt, ref shouldExit);
        if (MainMenuAndPauseUI.IsInMainMenu) _menuTime += dt;
        if (shouldExit || MainMenuAndPauseUI.IsInMainMenu) return;
        if (MainMenuAndPauseUI.IsMenuBlockingGameplay)
        {
            if(Net.Ready) TickFloor(dt);
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
            if(Net.Guest) Net.Send(new("bonus"));
            else GrantCash(2000, "Чит-бонус Авторитета [F9]");
            AudioMusicSystem.PlayTarotCash();
        }

        // Weapon slots [1..4]
        if (Raylib.IsKeyPressed(KeyboardKey.One)) _weapons.SelectSlot(1);
        if (Raylib.IsKeyPressed(KeyboardKey.Two)) _weapons.SelectSlot(2);
        if (Raylib.IsKeyPressed(KeyboardKey.Three)) _weapons.SelectSlot(3);
        if (Raylib.IsKeyPressed(KeyboardKey.Four)) _weapons.SelectSlot(4);
        if (Raylib.IsKeyPressed(KeyboardKey.F) && !Raylib.IsKeyDown(KeyboardKey.E)) _weapons.TriggerInspect();

        // Ambush crouch toggle [C] or Left Control
        if (Raylib.IsKeyPressed(KeyboardKey.C) || Raylib.IsKeyPressed(KeyboardKey.LeftControl))
        {
            _isCrouching = !_isCrouching;
        }

        // Mouse look
        Vector2 md = Raylib.GetMouseDelta();
        float sens = LoreAndMenusUI.MouseSensitivity * 0.015f;
        _weapons.UpdateLookLag(md*sens,dt);
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
        bool sprinting = isMoving && !_isCrouching && Raylib.IsKeyDown(KeyboardKey.LeftShift) && _energy > 1;
        _energy = Math.Clamp(_energy + (sprinting ? -14f : 9f)*dt, 0, 100);
        if (isMoving)
        {
            moveDir = Vector3.Normalize(moveDir);
            float moveSpeed = _isCrouching ? 4.4f : (sprinting ? 8.8f : 6.6f);
            Vector3 desired = _playerPos + moveDir * moveSpeed * dt;
            _playerPos = _world.ResolvePlayerCollision(desired);
            _walkBobPhase += dt * (moveSpeed * 1.35f);
        }

        // Contextual [E] Interaction
        if (Raylib.IsKeyPressed(KeyboardKey.E))
        {
            if(Net.Guest) Net.Send(new("interact",LocalPose));
            else SetHudBanner(_world.Interact(BuildPlayerCamera(), TrySpendCash, GrantCash),4.8f);
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
            Fire(cam);
        }

        _weapons.UpdateWeapons(dt, cam, isMoving, holdingFire);

        TickFloor(dt);
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

    private static void DrawFrame(bool showHud=true)
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(10, 12, 20, 255));

        if (MainMenuAndPauseUI.IsInMainMenu)
        {
            // Dolly between the reception and the glass lift without ticking gameplay.
            float shotTime = _menuTime % 28f;
            float travel = .5f - .5f*MathF.Cos(MathF.Min(shotTime,18f)*MathF.PI/18f);
            Camera3D menuCam = new()
            {
                Position = Vector3.Lerp(new(-5.6f,1.68f,7.8f),new(-7.2f,2.45f,6.8f),travel),
                Target = Vector3.Lerp(new(-.5f,1.52f,10.8f),new(.2f,1.75f,-4.8f),travel),
                Up = Vector3.UnitY, FovY = 48
            };
            if (shotTime >= 20f)
            {
                // From the mezzanine, track parallel to the curtain wall.
                float track = (shotTime-20f)*.36f;
                menuCam.Position = new(7f+track,8.2f,-34.5f);
                menuCam.Target = new(-9f+track,5.5f,-66f);
            }
            Raylib.BeginMode3D(menuCam);
            _menuFloor.DrawWorld3D(menuCam);
            Raylib.EndMode3D();
            MainMenuAndPauseUI.Draw(_uiFont, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
            LoreAndMenusUI.Draw(_uiFont);
            Raylib.EndDrawing();
            return;
        }

        Camera3D playerCam = BuildPlayerCamera();

        // 1. 3D Skyscraper World + Entities + Combat Particles
        Raylib.BeginMode3D(playerCam);
        _world.DrawWorld3D(playerCam);
        _world.Partner?.Effects.DrawWeaponWorldEffects3D(playerCam);
        _weapons.DrawWeaponWorldEffects3D(playerCam);
        Raylib.EndMode3D();

        bool modalOpen = LoreAndMenusUI.IsAnyModalOpen || MainMenuAndPauseUI.IsPaused
            || MainMenuAndPauseUI.IsSaveLoadPanelOpen || MainMenuAndPauseUI.IsModsPanelOpen
            || MainMenuAndPauseUI.IsSettingsPanelOpen;
        if (!modalOpen)
        {
            // World tags, combat overlays and the gameplay HUD belong only to gameplay.
            if(showHud) _world.DrawWorldLabelsAndIndicators2D(playerCam, _uiFont);
            _weapons.DrawFirstPersonHandsAndWeapon(playerCam, _walkBobPhase, _isCrouching, _uiFont, showCombatText:showHud);
            if(showHud) DrawGameplayHud2D();
        }

        // 5. Modal Windows ([N] Lore, [F8] Audio Mixer, [K] Case Roulette) & [ESC] Pause Overlay

        if (MainMenuAndPauseUI.IsPaused || MainMenuAndPauseUI.IsSaveLoadPanelOpen || MainMenuAndPauseUI.IsModsPanelOpen || MainMenuAndPauseUI.IsSettingsPanelOpen)
        {
            MainMenuAndPauseUI.Draw(_uiFont, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        }

        LoreAndMenusUI.Draw(_uiFont);
        Raylib.EndDrawing();
    }

    private static void DrawGameplayHud2D()
    {
        int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
        int cx = sw/2, cy = sh/2;
        Color white = new(250,249,245,255), dark = new(22,24,38,142);
        string money = "$" + _playerCash;
        int cashWidth = (int)GameFonts.Measure(_uiFont,money,22).X + 14;
        Rectangle cash = new(32, sh-108, cashWidth, 29);
        MainMenuAndPauseUI.Surface(cash,dark,2);
        MainMenuAndPauseUI.Center(_uiFont,money,cash,22,white);
        void Meter(int x, float fraction)
        {
            Vector2 p = new(x, sh-50);
            Raylib.DrawCircleV(p,20,dark);
            Raylib.DrawRing(p,18,20,0,360,64,new Color(218,221,226,128));
            if (fraction>0) Raylib.DrawRing(p,17.5f,20,-90,-90+360*fraction,64,white);
        }
        Meter(52,_playerHp/100f);

        Color heart = _playerHp>30 ? white : new Color(254,152,159,255);
        Raylib.DrawCircle(47,sh-54,6,heart);
        Raylib.DrawCircle(57,sh-54,6,heart);
        Raylib.DrawTriangle(new(41,sh-53),new(52,sh-39),new(63,sh-53),heart);
        LoreAndMenusUI.DrawTextCrisp(_uiFont,$"{_playerHp} HP",80,sh-61,21,white);
        LoreAndMenusUI.DrawTextCrisp(_uiFont,"ЗДОРОВЬЕ",80,sh-37,11,white);

        Meter(204,_energy/100f);
        // Our commerce drive meter: a sharp impulse, sharing the health ring.
        Vector2[] bolt = [new(207,sh-63), new(195,sh-48), new(203,sh-48),
            new(200,sh-37), new(214,sh-53), new(206,sh-53)];
        Raylib.DrawTriangle(bolt[0],bolt[1],bolt[2],white);
        Raylib.DrawTriangle(bolt[0],bolt[2],bolt[5],white);
        Raylib.DrawTriangle(bolt[2],bolt[3],bolt[4],white);
        Raylib.DrawTriangle(bolt[2],bolt[4],bolt[5],white);
        LoreAndMenusUI.DrawTextCrisp(_uiFont,$"{(int)MathF.Round(_energy)}%",232,sh-61,21,white);
        LoreAndMenusUI.DrawTextCrisp(_uiFont,"НАПОР",232,sh-37,11,white);

        int barX=cx-139, barY=sh-82;
        for(int i=0;i<4;i++)
        {
            Rectangle r=new(barX+i*70,barY,66,66);
            bool active=Math.Min((int)_weapons.CurrentWeapon,4)==i+1;
            Raylib.DrawRectangleRounded(r,.13f,6,new Color(19,24,38,active ? 128 : 76));
            if(active) Raylib.DrawRectangleRoundedLinesEx(r,.13f,6,2,white);
            LoreAndMenusUI.DrawTextCrisp(_serif,(i+1).ToString(),(int)r.X+5,barY+3,16,white);
            bool unlocked = i switch {1=>_weapons.UnlockedSolderingIron,2=>_weapons.UnlockedMicroUzi,3=>_weapons.UnlockedSawedOff || _weapons.UnlockedChatGun,_=>true};
            DrawSlotIcon(i,new(r.X+34,r.Y+35),unlocked);
        }
        MainMenuAndPauseUI.Center(_uiFont,"[N] Архив   [K] Кейсы   [F] Осмотр   [F8] Звук",new(sw-419,sh-42,405,25),16,white);

        Color cyan = new(40,213,243,255);
        Vector2 badge = new(sw-238,25);
        Raylib.DrawPoly(badge,6,14,-90,cyan);
        Raylib.DrawPoly(badge,6,11.5f,-90,new Color(23,41,60,255));
        MainMenuAndPauseUI.Center(_uiFont,(1+_xp/XpPerLevel).ToString(),new(badge.X-12,13,24,24),14,white);
        Rectangle xp = new(sw-219,14,204,22);
        Raylib.DrawRectangleRounded(xp,.95f,8,new Color(14,23,35,232));
        Raylib.DrawRectangleRounded(new(xp.X+2,xp.Y+2,200f*(_xp%XpPerLevel)/XpPerLevel,18),1,8,new Color(18,132,176,245));
        Raylib.DrawRectangleRoundedLinesEx(xp,.95f,8,1,cyan);
        MainMenuAndPauseUI.Center(_uiFont,$"{_xp%XpPerLevel} / 1 073 XP",xp,15,white);
        int working = _world.GalleyDesks.Count(d=>d.IsOccupied && !d.IsSlackingInTwitter);
        int occupied = _world.GalleyDesks.Count(d=>d.IsOccupied);
        void Objective(int top,string label,Color edge)
        {
            Rectangle r=new(sw-207,top,192,22);
            Raylib.DrawRectangleRounded(r,.45f,6,new Color(14,23,32,216));
            Raylib.DrawRectangleRoundedLinesEx(r,.45f,6,1,edge);
            MainMenuAndPauseUI.Center(_uiFont,label,r,14,white);
        }
        Objective(40,"Этаж 1: Белая Акула",new Color(215,165,64,240));
        Objective(65,$"Галера: {occupied} / 4 (+ ${working*20}/с)",cyan);
        string target=_world.FocusPrompt(BuildPlayerCamera());
        if(target.Length>0)
        {
            string prompt="[E]  "+target;
            float width=Math.Min(sw-24,GameFonts.Measure(_serif,prompt,20).X+24);
            Rectangle plate=new(cx-width/2,cy+24,width,30);
            MainMenuAndPauseUI.Surface(plate,new Color(21,30,40,205),1);
            MainMenuAndPauseUI.Center(_serif,prompt,plate,20,white);
        }
        else if (_hudStatusTimer>0 && !string.IsNullOrEmpty(_hudStatusBanner))
        {
            float width=Math.Min(sw-24,GameFonts.Measure(_serif,_hudStatusBanner,18).X+28);
            Rectangle plate=new(cx-width/2,cy+24,width,30);
            MainMenuAndPauseUI.Surface(plate,new Color(16,24,34,220),1);
            MainMenuAndPauseUI.Center(_serif,_hudStatusBanner,plate,18,new Color(245,214,128,255));
        }
        Raylib.DrawCircle(cx,cy,1.3f,white);
        if (AudioMusicSystem.VoiceTimer>0)
        {
            string line=AudioMusicSystem.ActiveVoiceText;
            float width=Math.Min(sw-24,GameFonts.Measure(_uiFont,line,16).X+22);
            Rectangle caption=new(cx-width/2,cy+66,width,25);
            Raylib.DrawRectangleRounded(caption,.5f,8,new Color(20,27,35,204));
            MainMenuAndPauseUI.Center(_uiFont,line,caption,16,white);
        }

    }

    private static Texture2D[]? _slotIcons;

    internal static void DrawSlotIcon(int index, Vector2 center, bool unlocked, Color? accent = null)
    {
        if (_slotIcons == null)
        {
            string[] files =
            [
                "icon_slot_1_belt.png",
                "icon_slot_2_solder.png",
                "icon_slot_3_uzi.png",
                "icon_slot_4_shotgun.png"
            ];
            _slotIcons = new Texture2D[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                string p = Path.Combine(AppContext.BaseDirectory, "assets", "icons", files[i]);
                if (!File.Exists(p))
                    p = Path.Combine(Directory.GetCurrentDirectory(), "assets", "icons", files[i]);
                if (File.Exists(p))
                {
                    Texture2D tex = Raylib.LoadTexture(p);
                    Raylib.SetTextureFilter(tex, TextureFilter.Bilinear);
                    _slotIcons[i] = tex;
                }
            }
        }

        int idx = Math.Clamp(index, 0, _slotIcons.Length - 1);
        Texture2D icon = _slotIcons[idx];
        if (icon.Id != 0)
        {
            Color tint = accent ?? Color.White;
            if (!unlocked) tint = Raylib.Fade(tint, 0.38f);
            Rectangle src = new(0, 0, icon.Width, icon.Height);
            Rectangle dst = new(center.X - 29f, center.Y - 29f, 58f, 58f);
            Raylib.DrawTexturePro(icon, src, dst, Vector2.Zero, 0f, tint);
        }
    }

    // =========================================================================
    // AUTOMATED 8-SCREENSHOT SUITE (`HowToVibeCoder.exe --autotest`)
    // =========================================================================
    private static void CheckElevatorNameplates()
    {
        Vector3 half=SkyscraperFloor1World.LiftSignSize*.5f;
        int trials=0;
        foreach(int side in new[]{-1,1})
        foreach(float distance in new[]{2f,4f,9f})
        foreach(float angle in new[]{-.4f,0,.4f})
        {
            Vector3 face=side<0?_world.NorthElevatorPos:_world.SouthElevatorDoorPos;
            Vector3 sign=SkyscraperFloor1World.LiftSignCenter(face,side);
            Camera3D camera=new() { Position=sign+new Vector3(angle*distance,-.8f,side*distance),
                Target=sign,Up=Vector3.UnitY,FovY=76 };
            WorldLabelLayout.BeginFrame();
            WorldLabelLayout.ReserveProjectedBox(camera,sign,half);
            Vector2 low=new(float.MaxValue),high=new(float.MinValue);
            foreach(int x in new[]{-1,1}) foreach(int y in new[]{-1,1}) foreach(int z in new[]{-1,1})
            {
                Vector2 p=Raylib.GetWorldToScreen(sign+new Vector3(x*half.X,y*half.Y,z*half.Z),camera);
                low=Vector2.Min(low,p);
                high=Vector2.Max(high,p);
            }
            Rectangle occupied=new(low,high-low);
            Rectangle previous=default;
            foreach(int width in new[]{110,180})
            {
                if (!WorldLabelLayout.Place((low+high)/2,width,29,out var pill)
                    || Raylib.CheckCollisionRecs(occupied,pill) || Raylib.CheckCollisionRecs(previous,pill))
                    throw new InvalidDataException("Nameplate overlaps the lift sign or another nameplate");
                previous=pill;
                trials++;
            }
        }
        Console.WriteLine($"[AUTOTEST] Lift nameplate collision cases passed: {trials}");
    }

    private static void RunAutomatedScreenshotSuite()
    {
        CheckElevatorNameplates();
        LoreAndMenusUI.CheckMixerControls();
        string projectScreenshotsDir = Path.Combine(Directory.GetCurrentDirectory(), "screenshots");
        string binScreenshotsDir = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(projectScreenshotsDir);
        Directory.CreateDirectory(binScreenshotsDir);

        void CaptureShot(string fileName, string? detailName = null, Rectangle detail = default, bool cropOnly=false)
        {
            // Render 3 warmup frames so all animations/particles/UI settle cleanly
            for (int f = 0; f < 3; f++)
            {
                DrawFrame(showHud:!cropOnly);
            }

            Image img = Raylib.LoadImageFromScreen();
            if (cropOnly) Raylib.ImageCrop(ref img,detail);
            string path1 = Path.Combine(projectScreenshotsDir, fileName);
            string path2 = Path.Combine(binScreenshotsDir, fileName);
            if (!Raylib.ExportImage(img, path1)) throw new IOException(path1);
            if (!string.Equals(Path.GetFullPath(path1), Path.GetFullPath(path2), StringComparison.OrdinalIgnoreCase))
            {
                if (!Raylib.ExportImage(img, path2)) throw new IOException(path2);
            }
            if (detailName != null)
            {
                // Same camera and animation time. Keep subtitles and inventory out of the grip detail.
                DrawFrame(showHud:false);
                Image clean = Raylib.LoadImageFromScreen();
                Image crop = Raylib.ImageFromImage(clean,detail);
                Raylib.UnloadImage(clean);
                Raylib.ImageResize(ref crop,crop.Width*2,crop.Height*2);
                string output = Path.Combine(projectScreenshotsDir,detailName);
                if (!Raylib.ExportImage(crop,output)) throw new IOException(output);
                string slotFile = string.Join("_", detailName.Split('_').Take(3)) + ".png";
                File.Copy(output, Path.Combine(projectScreenshotsDir, slotFile), overwrite: true);
                Raylib.UnloadImage(crop);
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

        // 1. 01_office_hub_hr_tarot.png - Central White Shark Reception opposite the Elevator: HR Tarot Veronika with crystal ball & candles, Belt in crimson jacket hands
        ResetNewGameFloor();
        MainMenuAndPauseUI.IsInMainMenu = false;
        _weapons.SelectSlot(1); // Father's Leather Belt
        _playerPos = new Vector3(0.0f, 1.72f, 10.15f);
        LookAtTarget(new Vector3(0.0f, 1.28f, 13.0f));
        AudioMusicSystem.TriggerVoiceLine("HR-ТАРОЛОГ ВЕРОНИКА", "Расклад Таро готов! Приноси вайб-кодеров на разбор натальной карты!", (int)SpeakerVoiceType.VeronikaTarot);
        CaptureShot("01_office_hub_hr_tarot.png","crop_slot_1_belt.png",new(307,452,715,214));

        // 2. 02_macbook_trap_ambush.png - Ambush behind Leather Sofa/Ficus, glowing MacBook Trap + Central Armored Elevator Shaft + Left/Right Forks + Walking Vibe Coder with Anxiety Bar
        ResetNewGameFloor();
        MainMenuAndPauseUI.IsInMainMenu = false;
        _weapons.SelectSlot(2); // 90s Soldering Iron with rising rosin smoke
        _isCrouching = true;
        _playerPos = new Vector3(-1.75f, 1.12f, 8.5f);
        LookAtTarget(new Vector3(0.0f, 1.18f, 5.2f));
        _world.ActivateMacBookTrap(SubscriptionBaitTier.CursorPro, _ => true, out _);
        var walkingCoder = _world.SpawnCoderForTier(SubscriptionBaitTier.CursorPro, forceSide: -1);
        walkingCoder.Position = new Vector3(-1.55f, 0f, 4.65f);
        walkingCoder.FacingYaw = .46f;
        walkingCoder.Anxiety = 38f;
        walkingCoder.SpeechBubble = "Ого, халявный Cursor Pro на Макбуке! Бегу жать Accept All!";
        walkingCoder.SpeechTimer = 5.0f;
        _world.UpdateAmbushState(_playerPos, _isCrouching);
        _weapons.UpdateWeapons(0.16f, BuildPlayerCamera(), isMoving: false, holdingFire: true);
        CaptureShot("02_macbook_trap_ambush.png","crop_slot_2_solder.png",new(690,405,395,315));

        // Exercise Raylib's key state and the normal gameplay input dispatch.
        static unsafe void ReplayF(bool down)
        {
            AutomationEvent key = new() { Type=down ? 2u : 1u }; // raylib INPUT_KEY_DOWN / UP
            key.Params[0]=(int)KeyboardKey.F;
            Raylib.PlayAutomationEvent(key);
        }
        bool exit=false;
        _hudStatusTimer=0;
        _playerPos=new Vector3(1.2f,1.72f,11.4f);
        _isCrouching=false;
        LookAtTarget(new Vector3(0,2.65f,.5f));
        LoreAndMenusUI.IsLoreOpen=true;
        ReplayF(true);
        UpdateFrame(1f/60,ref exit);
        ReplayF(false);
        if (_weapons.Inspecting) throw new InvalidDataException("Inspection started over archive");
        LoreAndMenusUI.IsLoreOpen=false;
        for(int slot=1;slot<=4;slot++)
        {
            _weapons.SelectSlot(slot);
            ReplayF(true);
            UpdateFrame(1f/60,ref exit);
            ReplayF(false);
            if (!_weapons.Inspecting) throw new InvalidDataException($"F failed in slot {slot}");
            for(int frame=1;frame<=145;frame++)
            {
                _weapons.UpdateWeapons(1f/60,BuildPlayerCamera());
                if(frame==138 && !_weapons.Inspecting)
                    throw new InvalidDataException($"Inspection ended early in slot {slot}");
                if(slot==2 && frame==97)
                    CaptureShot("crop_inspect_f.png",detail:new(400,260,690,390),cropOnly:true);
            }
            if (_weapons.Inspecting) throw new InvalidDataException($"Inspect did not finish in slot {slot}");
        }
        Console.WriteLine("[AUTOTEST] F started and finished inspection in all four slots.");

        // 3. 03_uzi_combat_saas_miniboss.png - Firing Micro-Uzi (muzzle flash, brass shells, tracers, hitmarker) at SaaS Guru Mini-Boss + flying 3D "500 INTERNAL ERROR" & "API KEY LEAKED" blocks
        ResetNewGameFloor();
        MainMenuAndPauseUI.IsInMainMenu = false;
        _isCrouching = false;
        _weapons.SelectSlot(3); // Micro-Uzi
        _weapons.UpdateWeapons(.3f,BuildPlayerCamera());
        _playerPos = new Vector3(0.0f, 1.72f, 9.4f);
        LookAtTarget(new Vector3(0.0f, 1.35f, 4.5f));
        _world.SelectedBaitTier = SubscriptionBaitTier.CloudEntropicMax;
        _world.IsMacBookTrapActive = true;
        var miniBoss = _world.SpawnCoderForTier(SubscriptionBaitTier.CloudEntropicMax, forceSide: 1);
        miniBoss.Position = new Vector3(1.3f, 0f, 6.15f);
        miniBoss.FacingYaw = -.50f;
        LookAtTarget(miniBoss.Position + Vector3.UnitY*1.35f);
        miniBoss.Hp = 420f;
        miniBoss.HitFlashTimer = 0;
        _world.EnemyProjectiles.Add(new EnemyProjectile3D
        {
            Position = new Vector3(-.78f, 2.16f, 5.75f),
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
            Position = new Vector3(2.86f, 1.51f, 5.55f),
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
        _weapons.TriggerWeaponAttack(uziCam, miniBoss.Position + Vector3.UnitY*1.35f, hitEnemy: true, damageDealt: 46, isCritical: true);
        _weapons.UpdateWeapons(0.02f, uziCam, isMoving: false, holdingFire: true);
        _hudStatusTimer = 0;
        StagePartner(new(-.75f,0,6.45f),miniBoss.Position+Vector3.UnitY*1.3f);
        CaptureShot("03_uzi_combat_saas_miniboss.png","crop_slot_3_uzi.png",new(670,317,442,370));

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

        _playerPos = new Vector3(3.4f, 1.72f, 15.7f);
        LookAtTarget(new Vector3(8.2f, 1.10f, 13.6f));
        CaptureShot("04_hr_tarot_payout_and_galley.png");

        // 5. 05_elevator_boss_escaped_agent.png - Open Central Armored Elevator + Escaped AI Agent v0.1 Boss throwing 40GB node_modules cubes
        ResetNewGameFloor();
        MainMenuAndPauseUI.IsInMainMenu = false;
        _weapons.SelectSlot(4); // Sawed-Off Shotgun / Chat-Gun
        _weapons.UpdateWeapons(.3f,BuildPlayerCamera());
        _world.HasElevatorKey = true;
        _world.TrySummonElevatorBoss(_ => true, out _);
        _world.ElevatorBoss.Position = new Vector3(0.0f,.16f,-9.8f);
        _world.ElevatorDoorOpenAmount=.86f;
        _world.ElevatorBoss.Hp = 1480f;
        _world.ElevatorBoss.FacingYaw = 3.14159f; // Facing North toward player in North Boss Arena
        _world.SpawnNodeModulesCube(new Vector3(3.35f, 1.15f, -8.1f), new Vector3(0f, 1.5f, -18.5f));
        _world.SpawnNodeModulesCube(new Vector3(-3.2f, 2.55f, -9.1f), new Vector3(0f, 1.5f, -18.5f));
        _playerPos = new Vector3(-1.35f, 1.72f, -14.2f);
        LookAtTarget(_world.ElevatorBoss.Position+new Vector3(.15f,2.15f,0));
        Camera3D bossCam = BuildPlayerCamera();
        _weapons.TriggerWeaponAttack(bossCam, _world.ElevatorBoss.Position+Vector3.UnitY*1.55f, hitEnemy: true, damageDealt: 165, isCritical: true);
        _weapons.UpdateWeapons(0.03f, bossCam, isMoving: false, holdingFire: true);
        _hudStatusTimer = 0;
        StagePartner(new(.1f,0,-10.4f),_world.ElevatorBoss.Position+Vector3.UnitY*1.4f);
        CaptureShot("05_elevator_boss_escaped_agent.png","crop_slot_4_shotgun.png",new(575,214,590,420));

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

    }

    private static void LookAtTarget(Vector3 target)
    {
        float eyeHeight = _isCrouching ? 1.12f : 1.72f;
        Vector3 eye = new(_playerPos.X, eyeHeight, _playerPos.Z);
        Vector3 d = Vector3.Normalize(target - eye);
        _playerYaw = MathF.Atan2(d.X, d.Z);
        _playerPitch = MathF.Asin(Math.Clamp(d.Y, -0.95f, 0.95f));
    }
}
