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
    private static float _playerVelY, _coyoteTimer, _jumpBufferTimer;
    private static float _eyeHeight = 1.72f, _dashTime, _dashCooldown, _regenDelay, _fovKick;
    private static float _recoilPitch, _recoilYaw, _shake, _crosshairGap = 4, _damageFlash;
    private static float _pitchSpring, _yawSpring;
    private static Vector3 _dashDirection;
    private static bool _isGrounded = true, _sprinting, _toggleCrouch, _exhausted;
    private static int _lastHudHp = 100;
    private static readonly Random ShotRandom = new(413);
    private static float _walkBobPhase = 0f;

    private static int _playerCash = 450;
    private static int _playerHp = 100;
    private const int XpPerLevel = 1073;
    private static int _xp = 412;
    private static SkyscraperFloor1World _menuFloor = null!;
    private static float _menuTime;

    private static int? fixtureSeed;
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
        bool localPair=coopSmoke && !HasFlag("--host") && !HasFlag("--join");
        fixtureSeed=autoTest || coopSmoke || gripTest ? 6 : null;
        if (HasFlag("--regenerate-music"))
        {
            AudioMusicSystem.EnsureMusicFiles(Path.Combine(Environment.CurrentDirectory, "assets", "sounds"), force: true);
            return 0;
        }

        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint | (autoTest || gripTest || coopSmoke ? ConfigFlags.HiddenWindow : 0));
        Raylib.InitWindow(ScreenWidth, ScreenHeight, "HOW TO VIBE CODER - ЭТАЖ 1: CLOUD ENTROPIC TOWER (1994 -> 2026)");
        Raylib.SetTargetFPS(60);
        Raylib.SetExitKey(KeyboardKey.Null);

        EnsureFloorTypefaces();
        GameFonts.Load();
        _uiFont = GameFonts.Get(Typeface.Interface, 18);
        _serif = GameFonts.Get(Typeface.Serif, 24);
        MainMenuAndPauseUI.LoadArt(_serif);
        VisualEngine3D.Load();
        CombatSprites.Load();

        AudioMusicSystem.Initialize(silent);
        if(silent && Raylib.IsAudioDeviceReady()) Raylib.SetMasterVolume(0f);
        _world = new SkyscraperFloor1World(fixtureSeed);
        _menuFloor = new SkyscraperFloor1World { Partner = new PartnerAvatar(), ElevatorDoorOpenAmount = .62f };
        _menuFloor.Partner.Move(new(new(-26.2f,1.72f,5.5f),1.26f,0,WeaponType.MicroUzi));
        _weapons = new WeaponAndCombatView();

        WireUpSystems();
        ConfigureCoop();
        int join=Array.IndexOf(args,"--join");
        if(join>=0)
        {
            MainMenuAndPauseUI.OpenJoinPanel();
            Net.Join(join+1<args.Length ? args[join+1] : "127.0.0.1");
        }
        else if(localPair || args.Contains("--host")) MainMenuAndPauseUI.StartNewOrResumeFloor();

        if (coopSmoke)
        {
            if(localPair && !Net.Host) throw new IOException(Net.Status);
            using var guest=localPair ? System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                Environment.ProcessPath!,"--coop-smoke --join 127.0.0.1") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true }) : null;
            var guestLog=guest?.StandardOutput.ReadToEndAsync();
            try
            {
                RunCoopSmoke(join>=0);
                CheckCoopEscort(join>=0);
                if(guest!=null)
                {
                    if(!guest.WaitForExit(10000)) throw new TimeoutException("Guest did not finish.");
                    var output=guestLog!.GetAwaiter().GetResult();
                    foreach(string line in output.Split('\n'))
                        if(line.StartsWith("COOP PASS guest")) Console.WriteLine(line.TrimEnd());
                    if(guest.ExitCode!=0 || !output.Contains("COOP PASS guest")) throw new IOException("Guest smoke failed.");
                }
            }
            finally { if(guest is { HasExited:false }) guest.Kill(); }
        }
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
        LoreAndMenusUI.UnloadArchive();
        CombatSprites.Unload();
        VisualEngine3D.Unload();
        MainMenuAndPauseUI.UnloadArt();
        GameFonts.Unload();
        AudioMusicSystem.Shutdown();
        Raylib.CloseWindow();
        return 0;
    }

    private static void EnsureFloorTypefaces()
    {
        // A fresh repository build can contain only the older menu font bundle.
        // Use the same authored assets as the dossier when running from that checkout.
        string destination=Path.Combine(AppContext.BaseDirectory,"assets","fonts");
        string[] names=["Oswald-SemiBold.ttf","Oswald-Bold.ttf","PTSans-Regular.ttf","PTSans-Bold.ttf"];
        if(names.All(name=>File.Exists(Path.Combine(destination,name)))) return;
        foreach(string anchor in new[]{Environment.CurrentDirectory,AppContext.BaseDirectory})
        {
            for(var root=new DirectoryInfo(anchor);root!=null;root=root.Parent)
            {
                string source=Path.Combine(root.FullName,"assets","fonts");
                if(!File.Exists(Path.Combine(root.FullName,"HowToVibeCoder.csproj")) ||
                    !names.All(name=>File.Exists(Path.Combine(source,name)))) continue;
                Directory.CreateDirectory(destination);
                foreach(string name in names)
                {
                    string target=Path.Combine(destination,name);
                    if(File.Exists(target)) continue;
                    string staged=target+"."+Guid.NewGuid().ToString("N")+".tmp";
                    try
                    {
                        File.Copy(Path.Combine(source,name),staged);
                        try { File.Move(staged,target); }
                        catch(IOException) when(File.Exists(target)) { }
                    }
                    finally { if(File.Exists(staged)) File.Delete(staged); }
                }
                return;
            }
        }
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

            pendingShift=CaptureFloor();
            return pendingFloorSave = new GameSaveData
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
            RestoreFloorSave(data);
            _playerPos = new Vector3(data.PlayerPosX, Math.Max(1.2f, data.PlayerPosY), data.PlayerPosZ);
        };
    }

    private static void ResetNewGameFloor()
    {
        floorPanel=FloorPanel.None; seenReport=0;
        bot=awaitingSpawn=pendingCase=false; guestCarried=peerEscort=null; guestHp=100;
        _world = new SkyscraperFloor1World(fixtureSeed);
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
        _weaponFireCooldown = _walkBobPhase = _playerVelY = _jumpBufferTimer = _pitchSpring = _yawSpring = 0;
        _dashTime = _dashCooldown = _regenDelay = _fovKick = _recoilPitch = _recoilYaw = _shake = _damageFlash = 0;
        _eyeHeight=1.72f; _coyoteTimer=.12f; _isGrounded=true; _crosshairGap=4; _lastHudHp=100;
        _sprinting=_toggleCrouch=_exhausted=false;
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
        float bob = _isGrounded ? .022f*MathF.Sin(_walkBobPhase*2)*_weapons.WalkWeight : 0;
        Vector3 eyePos = _playerPos + Vector3.UnitY*(bob-_weapons.LandingCompression);
        float pitch = _playerPitch+_recoilPitch+.013f*_weapons.ClosingImpulse+_shake*MathF.Sin(_shake*720);
        float yaw = _playerYaw+_recoilYaw+_shake*MathF.Cos(_shake*860);
        Vector3 forward = new(MathF.Sin(yaw)*MathF.Cos(pitch),MathF.Sin(pitch),MathF.Cos(yaw)*MathF.Cos(pitch));

        return new Camera3D
        {
            Position = eyePos,
            Target = eyePos + forward,
            Up = Vector3.Transform(Vector3.UnitY,Quaternion.CreateFromAxisAngle(forward,.015f*_weapons.ClosingImpulse)),
            FovY = Math.Clamp(LoreAndMenusUI.CameraFov, 60f, 100f)+_fovKick,
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

        if(!MainMenuAndPauseUI.IsMenuBlockingGameplay && !MainMenuAndPauseUI.IsInMainMenu && !LoreAndMenusUI.IsAnyModalOpen)
        {
            if(HandleFloorPanel())
            {
                if(Net.Ready) TickFloor(dt);
                return;
            }
        }
        bool wasModal = LoreAndMenusUI.IsAnyModalOpen;
        bool modalShortcut = Raylib.IsKeyPressed(KeyboardKey.F8) || Raylib.IsKeyPressed(KeyboardKey.K) || Raylib.IsKeyPressed(KeyboardKey.N);
        if (!MainMenuAndPauseUI.JoinPanel && (wasModal || modalShortcut))
        {
            if(Net.Ready && !MainMenuAndPauseUI.IsInMainMenu) TickFloor(dt);
            else if(LoreAndMenusUI.IsLoreOpen && !MainMenuAndPauseUI.IsInMainMenu) _world.AdvanceLighting(dt);
            LoreAndMenusUI.Update(dt, _weapons);
            if (LoreAndMenusUI.IsAnyModalOpen && Raylib.IsCursorHidden()) Raylib.EnableCursor();
            return;
        }
        MainMenuAndPauseUI.Update(dt, ref shouldExit);
        FlushFloorSave();
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

        if (Raylib.IsKeyPressed(KeyboardKey.C)) _toggleCrouch = !_toggleCrouch;
        _isCrouching = _toggleCrouch || Raylib.IsKeyDown(KeyboardKey.LeftControl);

        // Mouse look
        Vector2 md = Raylib.GetMouseDelta();
        float sens = LoreAndMenusUI.MouseSensitivity * 0.015f;
        _weapons.UpdateLookLag(md*sens,dt);
        _playerYaw -= md.X * sens;
        _playerPitch = Math.Clamp(_playerPitch - md.Y * sens, -1.25f, 1.25f);

        // Keyboard turning fallback
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
        MovePlayer(dt,moveDir,forwardXZ,Raylib.IsKeyPressed(KeyboardKey.Space),
            Raylib.IsKeyDown(KeyboardKey.LeftShift),Raylib.IsKeyPressed(KeyboardKey.LeftAlt) || Raylib.IsKeyPressed(KeyboardKey.Q));

        // Contextual [E] Interaction
        if (Raylib.IsKeyPressed(KeyboardKey.E))
        {
            var camera=BuildPlayerCamera();
            string? joint=CoopEscort(camera,false,apply:!Net.Guest);
            if(joint!=null)
            {
                if(Net.Guest) Net.Send(new("interact",LocalPose));
                else SetHudBanner(joint);
                return;
            }
            FloorPanel station=_world.PanelAt(camera);
            if(station!=FloorPanel.None) { OpenFloorPanel(station); return; }
            if(Net.Guest) Net.Send(new("interact",LocalPose));
            else SetHudBanner(_world.Interact(BuildPlayerCamera(), TrySpendCash, GrantCash),4.8f);
        }

        UpdateTrigger(dt, Raylib.IsMouseButtonDown(MouseButton.Left),Raylib.IsMouseButtonPressed(MouseButton.Left),
            Raylib.IsKeyPressed(KeyboardKey.R),isMoving);
        TickFloor(dt);
        if (_playerHp < _lastHudHp) _damageFlash = .22f;
        _lastHudHp = _playerHp;
    }

    private static void MovePlayer(float dt, Vector3 move, Vector3 facing, bool jump, bool sprint, bool dash)
    {
        float landingImpact=0;
        float feet = Math.Max(0,_playerPos.Y-_eyeHeight);
        _eyeHeight += ((_isCrouching ? 1.16f : 1.72f)-_eyeHeight)*(1-MathF.Exp(-15*dt));
        _jumpBufferTimer = jump ? .12f : Math.Max(0,_jumpBufferTimer-dt);
        _coyoteTimer = _isGrounded ? .12f : Math.Max(0,_coyoteTimer-dt);
        if (_jumpBufferTimer>0 && _coyoteTimer>0)
        {
            _playerVelY=8.2f; _isGrounded=false; _coyoteTimer=_jumpBufferTimer=0;
        }
        _dashCooldown = Math.Max(0,_dashCooldown-dt);
        bool moving = move.LengthSquared()>.01f;
        if (moving) move=Vector3.Normalize(move);
        if (dash && _dashCooldown<=0 && _energy>=22)
        {
            _dashDirection=moving ? move : facing;
            _dashTime=.16f; _dashCooldown=.85f; _energy-=22; _regenDelay=.65f; _fovKick=8;
        }
        if (_energy>=12) _exhausted=false;
        _sprinting=moving && sprint && !_isCrouching && !_exhausted && _energy>0 && _dashTime<=0;
        if (_sprinting)
        {
            _energy=Math.Max(0,_energy-(_world.Systems.Coffee>0 ? 6 : 17)*dt); _regenDelay=.65f;
            if (_energy<=0) _exhausted=true;
        }
        else if (_dashTime<=0 && (_regenDelay-=dt)<=0) _energy=Math.Min(100,_energy+20*dt);
        float speed = _isCrouching ? 4.25f : 6.6f*(_sprinting ? 1.48f : 1);
        if(_world.Systems.Coffee>0) speed*=1.25f;
        float burst = Math.Min(dt,_dashTime);
        Vector3 travel = move*speed*(dt-burst) + _dashDirection*18*burst;
        _dashTime-=burst;
        _fovKick+=((_dashTime>0 ? 8 : _sprinting ? 4 : 0)-_fovKick)*(1-MathF.Exp(-10*dt));
        if (moving && _isGrounded) _walkBobPhase+=dt*speed*1.5f;

        // Small swept steps keep a diagonal dash sliding along narrow partitions.
        int steps=Math.Max(1,(int)MathF.Ceiling(travel.Length()/.14f));
        for (int i=0;i<steps;i++)
        {
            float h=dt/steps;
            float nextFeet=feet+_playerVelY*h-11.25f*h*h;
            _playerVelY-=22.5f*h;
            _playerPos=_world.ResolvePlayerCollision(_playerPos+travel/steps,.4f,Math.Max(feet,nextFeet));
            float support=_world.SupportHeight(_playerPos,Math.Max(feet,nextFeet));
            if (nextFeet<=support && _playerVelY<=0)
            {
                if (!_isGrounded && _playerVelY < -2)
                {
                    landingImpact=-_playerVelY;
                    AudioMusicSystem.PlayLanding();
                }
                nextFeet=support; _playerVelY=0; _isGrounded=true;
            }
            else _isGrounded=false;
            feet=nextFeet;
        }
        _playerPos.Y=feet+_eyeHeight;
        _weapons.SetStance(_walkBobPhase,_isCrouching);
        _weapons.UpdateMotion(dt,_sprinting,_isGrounded,_playerVelY,landingImpact,burst>0,
            Vector3.Dot(move,new Vector3(-facing.Z,0,facing.X)));
    }

    private static void UpdateTrigger(float dt,bool held,bool pressed,bool reload,bool moving)
    {
        _weapons.Drum=_world.Systems.Has(Sidegrade.Drum);
        _weapons.InsulatedIron=_world.Systems.Has(Sidegrade.Tape);
        _weapons.CoolIron(dt);
        var gun=_weapons.CurrentWeapon;
        bool automatic=gun==WeaponType.MicroUzi;
        bool trigger=automatic ? held : pressed;
        _weaponFireCooldown=Math.Max(-dt,_weaponFireCooldown-dt);
        static void Recover(ref float angle,ref float velocity,float speed,float dt)
        {
            float slope=velocity+speed*angle;
            float decay=MathF.Exp(-speed*dt);
            velocity=(velocity-speed*slope*dt)*decay;
            angle=(angle+slope*dt)*decay;
        }
        Recover(ref _recoilPitch,ref _pitchSpring,23,dt);
        Recover(ref _recoilYaw,ref _yawSpring,26,dt);
        _shake*=MathF.Exp(-20*dt);
        _damageFlash=Math.Max(0,_damageFlash-dt);
        Camera3D cam=BuildPlayerCamera();
        _weapons.SetStance(_walkBobPhase,_isCrouching);
        _weapons.UpdateWeapons(dt,cam,moving && _isGrounded,held);
        if(reload || _weapons.Ammo==0) _weapons.BeginReload();
        if(gun==WeaponType.SolderingIron && _weapons.IronLockout>0) trigger=false;
        if(trigger && _weaponFireCooldown<=.00001f && !_weapons.Reloading)
        {
            float interval=gun switch {
                WeaponType.MicroUzi=>1f/12, WeaponType.Belt=>_world.Systems.Coffee>0 ? .19f : .30f, WeaponType.SolderingIron=>_world.Systems.Has(Sidegrade.Tape) ? .26f : .34f,
                WeaponType.SawedOff=>.42f, _=>.28f };
            _weaponFireCooldown=interval+(automatic && !pressed ? _weaponFireCooldown : 0);
            _weapons.UseRound();
            if(gun==WeaponType.SolderingIron) _weapons.HeatIron();
            float steady=_isCrouching ? .65f : 1;
            bool melee=gun is WeaponType.Belt or WeaponType.SolderingIron;
            Vector3 aim=Vector3.Normalize(cam.Target-cam.Position);
            if (melee)
            {
                Vector3 flat=new(aim.X,0,aim.Z);
                _playerPos=_world.ResolvePlayerCollision(_playerPos+Vector3.Normalize(flat)*.24f,.40f,_playerPos.Y-_eyeHeight);
                cam=BuildPlayerCamera();
            }
            else
            {
                Vector3 right=Vector3.Normalize(Vector3.Cross(aim,Vector3.UnitY));
                float cone=(gun==WeaponType.SawedOff ? .033f : .009f)*steady*(1+_recoilPitch*12);
                aim=Vector3.Normalize(aim+cone*(right*(2*ShotRandom.NextSingle()-1)+Vector3.Cross(right,aim)*(2*ShotRandom.NextSingle()-1)));
            }
            PlayCurrentWeaponAudio(gun);
            if (Net.Ready)
                Net.Send(new("fire",new PlayerPose(cam.Position,MathF.Atan2(aim.X,aim.Z),MathF.Asin(aim.Y),gun)));
            if (Net.Guest)
            {
                float range=gun switch { WeaponType.SawedOff=>12.5f, WeaponType.ChatGun=>28, WeaponType.MicroUzi=>26,
                    WeaponType.Belt=>3.8f,_=>5.2f };
                _weapons.BallisticWorld=_world;
                float distance=_world.PelletDistance(cam.Position,aim,range);
                _weapons.TriggerWeaponAttack(cam,distance<range ? cam.Position+aim*distance : null,false,shotDirection:aim);
            }
            else if (_world.ProcessPlayerAttack(cam,_weapons,GrantCash,out var msg,aim) && msg.Length>0) SetHudBanner(msg,1.3f);
            float kick=gun==WeaponType.SawedOff ? .077f : melee ? .009f : .023f;
            // Impulse the spring velocity. The camera starts at the firing sightline.
            float yawKick=(2*ShotRandom.NextSingle()-1)*kick*steady;
            if (melee) { _recoilPitch+=kick*steady; _recoilYaw+=yawKick*.38f; }
            else { _pitchSpring+=kick*steady*30; _yawSpring+=yawKick*8; }
            if (melee && _weapons.HitmarkerTimer>=.22f && _weapons.HitmarkerIsCrit) _shake=.014f;
            _crosshairGap=Math.Min(15,_crosshairGap+(melee ? 2 : 4));
            if (_weapons.Ammo==0) _weapons.BeginReload();
        }
        else if (!trigger) _weaponFireCooldown=Math.Max(0,_weaponFireCooldown);
        float targetGap=(_isCrouching ? 3 : 4)+(_isGrounded ? 0 : 5)+(_sprinting ? 4 : 0);
        _crosshairGap+=(targetGap-_crosshairGap)*(1-MathF.Exp(-9*dt));
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

    // The viewmodel owns this private overlay. Access its existing drawing routine
    // separately so its text is composited at display resolution.
    [System.Runtime.CompilerServices.UnsafeAccessor(System.Runtime.CompilerServices.UnsafeAccessorKind.Method,Name="DrawCombatFloatingTextsAndHitmarker2D")]
    private static extern void DrawCombatOverlay(WeaponAndCombatView weapons,Camera3D camera,Font font);

    private static void BeginLitWorld(SkyscraperFloor1World world,Camera3D camera)
    {
        if(VisualEngine3D.BeginShadowPass(new(camera.Position.X,3,camera.Position.Z)))
        {
            world.DrawWorld3D(camera);
            VisualEngine3D.EndShadowPass();
        }
        VisualEngine3D.BeginScene();
        Raylib.BeginMode3D(camera);
        world.DrawWorld3D(camera);
    }

    private static void DrawFrame(bool showHud=true)
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(10, 12, 20, 255));

        if (MainMenuAndPauseUI.IsInMainMenu)
        {
            // Track the west-facing curtain wall without ticking gameplay.
            float shotTime = _menuTime % 28f;
            float travel = .5f - .5f*MathF.Cos(MathF.Min(shotTime,18f)*MathF.PI/18f);
            Camera3D menuCam = new()
            {
                Position = Vector3.Lerp(new(-21.4f,1.45f,6.9f),new(-23.1f,2.1f,9.3f),travel),
                Target = Vector3.Lerp(new(-32f,2.30f,2.9f),new(-35f,2.9f,-1.8f),travel),
                Up = Vector3.UnitY, FovY = 48
            };
            if (shotTime >= 20f)
            {
                // From the mezzanine, track parallel to the curtain wall.
                float track = (shotTime-20f)*.36f;
                menuCam.Position = new(7f+track,8.2f,-34.5f);
                menuCam.Target = new(-9f+track,5.5f,-66f);
            }
            BeginLitWorld(_menuFloor,menuCam);
            Raylib.EndMode3D();
            VisualEngine3D.Composite();
            MainMenuAndPauseUI.Draw(_uiFont, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
            LoreAndMenusUI.Draw(_uiFont);
            Raylib.EndDrawing();
            return;
        }

        Camera3D playerCam = BuildPlayerCamera();

        // 1. 3D Skyscraper World + Entities + Combat Particles
        BeginLitWorld(_world,playerCam);
        _world.Partner?.Effects.DrawWeaponWorldEffects3D(playerCam);
        _weapons.DrawWeaponWorldEffects3D(playerCam);
        Raylib.EndMode3D();

        bool modalOpen = floorPanel!=FloorPanel.None || LoreAndMenusUI.IsAnyModalOpen || MainMenuAndPauseUI.IsPaused
            || MainMenuAndPauseUI.IsSaveLoadPanelOpen || MainMenuAndPauseUI.IsModsPanelOpen
            || MainMenuAndPauseUI.IsSettingsPanelOpen;
        if(!modalOpen)
            _weapons.DrawFirstPersonHandsAndWeapon(playerCam,_walkBobPhase,_isCrouching,_uiFont,showCombatText:false);
        VisualEngine3D.Composite();
        if(!modalOpen && showHud)
        {
            _world.DrawWorldLabelsAndIndicators2D(playerCam,_uiFont);
            DrawCombatOverlay(_weapons,playerCam,_uiFont);
            DrawGameplayHud2D();
        }

        // 5. Modal Windows ([N] Lore, [F8] Audio Mixer, [K] Case Roulette) & [ESC] Pause Overlay

        if (MainMenuAndPauseUI.IsPaused || MainMenuAndPauseUI.IsSaveLoadPanelOpen || MainMenuAndPauseUI.IsModsPanelOpen || MainMenuAndPauseUI.IsSettingsPanelOpen)
        {
            MainMenuAndPauseUI.Draw(_uiFont, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        }

        LoreAndMenusUI.Draw(_uiFont);
        if(floorPanel!=FloorPanel.None) DrawFloorPanel();
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

        DrawFloorTelemetry();
        var camera=BuildPlayerCamera();
        string target=CoopEscort(camera,false,apply:false) ?? _world.FocusPrompt(camera);
        if(target.Length>0)
        {
            string prompt=target=="Подписать NDA" ? target+" [E]" : "[E]  "+target;
            float width=GameFonts.Measure(_serif,prompt,19).X;
            GameFonts.Draw(_serif,prompt,new(cx-width/2,cy+26),19,white);
        }
        else if (_hudStatusTimer>0 && !string.IsNullOrEmpty(_hudStatusBanner))
        {
            float width=Math.Min(sw-24,GameFonts.Measure(_serif,_hudStatusBanner,18).X+28);
            Rectangle plate=new(cx-width/2,cy+22,width,28);
            MainMenuAndPauseUI.Center(_serif,_hudStatusBanner,plate,18,new Color(245,214,128,255));
        }
        for(int i=0;i<4;i++)
        {
            Vector2 axis=i switch {0=>Vector2.UnitX,1=>-Vector2.UnitX,2=>Vector2.UnitY,_=>-Vector2.UnitY};
            Vector2 a=new Vector2(cx,cy)+axis*MathF.Round(_crosshairGap);
            Raylib.DrawLineEx(a,a+axis*5,2.5f,new Color(18,20,23,150));
            Raylib.DrawLineEx(a,a+axis*5,1.4f,white);
        }
        if(_weapons.CurrentWeapon==WeaponType.SolderingIron && _weapons.IronLockout>0)
            ShadowText(_uiFont,"Жало остывает",cx-56,cy+54,16,amber);
        if (_weapons.Capacity>0)
        {
            string rounds=_weapons.Reloading ? $"ПЕРЕЗАРЯДКА  {_weapons.ReloadProgress:0%}" : $"{_weapons.Ammo} / {_weapons.Capacity}";
            int w=(int)GameFonts.Measure(_uiFont,rounds,25).X;
            LoreAndMenusUI.DrawTextCrisp(_uiFont,rounds,sw-25-w,sh-43,25,white);
        }
        if (_damageFlash>0)
        {
            Color red=new(195,35,32,(int)(110*_damageFlash/.22f));
            Raylib.DrawRectangleGradientH(0,0,52,sh,red,Color.Blank);
            Raylib.DrawRectangleGradientH(sw-52,0,52,sh,Color.Blank,red);
        }
        if (AudioMusicSystem.VoiceTimer>0)
        {
            string line=AudioMusicSystem.ActiveVoiceText;
            float width=Math.Min(sw-24,GameFonts.Measure(_uiFont,line,16).X+22);
            Rectangle caption=new(cx-width/2,sh-122,width,26);
            Raylib.DrawRectangleRounded(caption,.35f,8,new Color(14,20,28,212));
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
                if (!WorldLabelLayout.Place((low+high)/2,width,22,out var label)
                    || Raylib.CheckCollisionRecs(occupied,label) || Raylib.CheckCollisionRecs(previous,label))
                    throw new InvalidDataException("Nameplate overlaps the lift sign or another nameplate");
                previous=label;
                trials++;
            }
        }
        Console.WriteLine($"[AUTOTEST] Lift nameplate collision cases passed: {trials}");
    }

    private static unsafe void CheckArchiveControls()
    {
        bool exit = false;
        void Input(uint type, int first, int second = 0)
        {
            AutomationEvent e = new() { Type = type };
            e.Params[0] = first;
            e.Params[1] = second;
            Raylib.PlayAutomationEvent(e);
        }
        void Frame()
        {
            UpdateFrame(1f / 60, ref exit);
            DrawFrame();
        }
        void Key(KeyboardKey key)
        {
            Input(2, (int)key);
            Frame();
            Input(1, (int)key);
            Frame();
        }
        void ClickAt(int x, int y)
        {
            Input(7, x, y);
            Input(6, (int)MouseButton.Left);
            Frame();
            Input(5, (int)MouseButton.Left);
            Frame();
        }
        void Expect(int tab)
        {
            if (LoreAndMenusUI.IsLoreOpen != (tab >= 0) ||
                (tab >= 0 && LoreAndMenusUI.ActiveLoreTab != tab))
                throw new InvalidDataException($"Archive input failed for tab {tab}");
        }
        void ClickCenter(Rectangle rect) =>
            ClickAt((int)(rect.X+rect.Width/2),(int)(rect.Y+rect.Height/2));

        Key(KeyboardKey.N);
        for (int i = 0; i < 4; i++)
        {
            Key(KeyboardKey.One + i);
            Expect(i);
        }
        Key(KeyboardKey.Tab);
        Expect(0);
        if (LoreAndMenusUI.SelectedArchiveEntry != 0)
            throw new InvalidDataException("Archive did not open on the first thread");
        Rectangle row = LoreAndMenusUI.ArchiveRowBounds(1);
        Input(7,(int)(row.X+row.Width/2),(int)(row.Y+row.Height/2));
        Input(8, 0, -12);
        Frame();
        float limit = LoreAndMenusUI.ArchiveScrollLimit;
        if (LoreAndMenusUI.LoreScrollOffset <= 0 || LoreAndMenusUI.LoreScrollOffset >= limit)
            throw new InvalidDataException("Archive wheel did not start smooth scrolling");
        for (int i = 0; i < 48; i++) Frame();
        if (Math.Abs(LoreAndMenusUI.LoreScrollOffset-limit)>1 ||
            LoreAndMenusUI.FirstVisibleArchiveEntry!=3 || LoreAndMenusUI.SelectedArchiveEntry!=5)
            throw new InvalidDataException("Last three archive threads do not fill the viewport");
        for (int entry = 3; entry <= 5; entry++)
        {
            ClickCenter(LoreAndMenusUI.ArchiveRowBounds(entry));
            if (LoreAndMenusUI.SelectedArchiveEntry != entry)
                throw new InvalidDataException($"Archive thread {entry+1} cannot be selected");
        }
        for (int i = 3; i >= 0; i--)
        {
            ClickCenter(LoreAndMenusUI.ArchiveTabBounds(i));
            Expect(i);
            if (LoreAndMenusUI.LoreScrollOffset != 0 || LoreAndMenusUI.SelectedArchiveEntry != 0)
                throw new InvalidDataException("Archive tab retained another section's offset");
        }
        ClickCenter(LoreAndMenusUI.ArchiveCloseBounds);
        Expect(-1);
        Key(KeyboardKey.N);
        Expect(0);
        Key(KeyboardKey.Escape);
        Expect(-1);
        Key(KeyboardKey.N);
        Key(KeyboardKey.N);
        Expect(-1);
        Console.WriteLine("[AUTOTEST] PASS archive: four tabs, mouse, Tab, N, Escape, smooth scroll to threads 4-6");
    }

    private static void CheckViewmodelRates()
    {
        Camera3D camera=new() { Target=Vector3.UnitZ, Up=Vector3.UnitY, FovY=76 };
        Vector3[] reference=new Vector3[6];
        float worst=0;
        foreach(int hz in new[]{60,120,144})
        {
            var view=new WeaponAndCombatView();
            view.SelectSlot(4);
            float dt=1f/hz;
            for(int i=0;i<hz;i++) view.UpdateWeapons(dt,camera);
            Vector3 rest=view.GetWeaponTipWorldPosition(camera);
            view.TriggerWeaponAttack(camera);
            if(Vector3.Distance(rest,view.GetWeaponTipWorldPosition(camera))>.00001f)
                throw new InvalidDataException("Discontinuous viewmodel pose on discharge");
            for(int sample=0;sample<reference.Length;sample++)
            {
                for(int i=0;i<hz/12;i++) view.UpdateWeapons(dt,camera);
                Vector3 tip=view.GetWeaponTipWorldPosition(camera);
                if(sample==0 && tip.Z>=rest.Z-.02f)
                    throw new InvalidDataException("Missing axial recoil");
                if(hz==60) reference[sample]=tip;
                else worst=Math.Max(worst,Vector3.Distance(tip,reference[sample]));
            }
        }
        if(worst>.001f) throw new InvalidDataException($"Viewmodel rate deviation {worst:F6} m");
        Console.WriteLine($"[AUTOTEST] Recoil at 60/120/144 Hz: max muzzle deviation {1000*worst:F3} mm");
        foreach (var (slot, duration) in new[] { (3, 1.45f), (4, 1.55f) })
        {
            var view = new WeaponAndCombatView();
            view.SelectSlot(slot);
            view.UseRound();
            view.TriggerInspect();
            for (int i = 0; i < 72; i++) view.UpdateWeapons(1f/60, camera);
            Vector3 inspected = view.GetWeaponTipWorldPosition(camera);
            view.BeginReload();
            if (!view.Reloading || Vector3.Distance(inspected, view.GetWeaponTipWorldPosition(camera)) > .00001f)
                throw new InvalidDataException($"Inspect-to-reload pose jump in slot {slot}");
            int ticks = 0;
            while (view.Reloading && ticks < 120)
            {
                view.UpdateWeapons(1f/60, camera);
                ticks++;
            }
            if (ticks != (int)MathF.Round(duration * 60) || view.Reloading)
                throw new InvalidDataException($"Reload duration in slot {slot}: {ticks} ticks");
            Console.WriteLine($"[AUTOTEST] Slot {slot}: continuous inspect-to-reload transition, {ticks} ticks at 60 Hz");
        }
    }

    private static unsafe void CheckFpsGameplay(Action<string> capture)
    {
        const float dt=1f/60;
        bool exit=false;
        static void Require(bool ok,string what)
        {
            if(!ok) throw new InvalidDataException("FPS regression: "+what);
        }
        static void Input(uint type,int code)
        {
            AutomationEvent e=new() { Type=type };
            e.Params[0]=code;
            Raylib.PlayAutomationEvent(e);
        }
        void Key(KeyboardKey key,bool down) => Input(down ? 2u : 1u,(int)key);
        void Mouse(bool down) => Input(down ? 6u : 5u,0);
        string? frameDir=Environment.GetEnvironmentVariable("VIBECODER_VIEWMODEL_FRAMES");
        if(!string.IsNullOrEmpty(frameDir)) Directory.CreateDirectory(frameDir);
        var frameCounts=new Dictionary<string,int>();
        void Tick(int frames=1,string? sequence=null)
        {
            for(int i=0;i<frames;i++)
            {
                UpdateFrame(dt,ref exit);
                if(sequence!=null && !string.IsNullOrEmpty(frameDir))
                {
                    int frame=frameCounts.GetValueOrDefault(sequence);
                    frameCounts[sequence]=frame+1;
                    DrawFrame(showHud:false);
                    Image img=Raylib.LoadImageFromScreen();
                    Raylib.ImageResize(ref img,960,540);
                    string file=Path.Combine(frameDir,$"{sequence}_{frame:D4}.png");
                    if(!Raylib.ExportImage(img,file)) throw new IOException(file);
                    Raylib.UnloadImage(img);
                }
                Raylib.PollInputEvents();
            }
        }

        if(!string.IsNullOrEmpty(frameDir))
        {
            ResetNewGameFloor();
            _playerPos=new(24,1.72f,17);
            // Continuous 60 Hz take from the normal input and rendering paths.
            const string take="motion";
            void Select(KeyboardKey slot)
            {
                Key(slot,true); Tick(1,take); Key(slot,false); Tick(31,take);
            }
            Key(KeyboardKey.Four,true); Tick(); Key(KeyboardKey.Four,false); Tick(45);
            Tick(12,take);
            _weapons.UseRound();
            _weapons.UseRound(); // Stage both spent hulls for the reload.
            Key(KeyboardKey.R,true); Tick(1,take); Key(KeyboardKey.R,false); Tick(100,take);
            Select(KeyboardKey.Three);
            _weapons.UseRound();
            Key(KeyboardKey.R,true); Tick(1,take); Key(KeyboardKey.R,false); Tick(94,take);
            Select(KeyboardKey.Four);
            Key(KeyboardKey.W,true); Key(KeyboardKey.LeftShift,true); Tick(47,take);
            Key(KeyboardKey.Space,true); Tick(1,take); Key(KeyboardKey.Space,false);
            Key(KeyboardKey.LeftShift,false); Key(KeyboardKey.W,false); Tick(66,take);
            Key(KeyboardKey.LeftControl,true); Tick(28,take); Key(KeyboardKey.LeftControl,false); Tick(18,take);
            Key(KeyboardKey.A,true); Tick(22,take); Key(KeyboardKey.A,false);
            Key(KeyboardKey.D,true); Tick(27,take); Key(KeyboardKey.D,false); Tick(21,take);
            Mouse(true); Tick(1,take); Mouse(false); Tick(45,take);
            Select(KeyboardKey.Three);
            Mouse(true); Tick(31,take); Mouse(false); Tick(36,take);
        }

        ResetNewGameFloor();
        Raylib.DisableCursor();
        Tick(2);
        _playerPos=new(24,1.72f,17);
        Key(KeyboardKey.Space,true); Tick(); Key(KeyboardKey.Space,false);
        float peak=_playerPos.Y;
        for(int i=0;i<55;i++) { Tick(); peak=Math.Max(peak,_playerPos.Y); }
        Require(peak>3.1f && _isGrounded && Math.Abs(_playerPos.Y-1.72f)<.002f,"Space arc / landing");
        Key(KeyboardKey.LeftControl,true); Tick(28);
        float crouched=BuildPlayerCamera().Position.Y;
        Require(crouched<1.30f,"Ctrl eye");
        Key(KeyboardKey.LeftControl,false); Tick(28);
        _playerYaw=MathF.PI;
        Vector3 beforeSprint=_playerPos;
        Key(KeyboardKey.W,true); Key(KeyboardKey.LeftShift,true); Tick(30);
        float sprintEnergy=_energy;
        Require(_sprinting && Math.Abs(sprintEnergy-91.5f)<.1f && _fovKick>3 &&
            Math.Abs(Vector3.Distance(beforeSprint,_playerPos)-6.6f*1.48f*.5f)<.02f,"sprint speed, FOV and drain");
        Key(KeyboardKey.LeftShift,false); Key(KeyboardKey.Q,true); Tick(); Key(KeyboardKey.Q,false);
        float dashEnergy=_energy;
        Require(_dashTime>0 && Math.Abs(sprintEnergy-dashEnergy-22)<.01f,"dash impulse");
        Key(KeyboardKey.W,false); Tick(28);
        Require(Math.Abs(dashEnergy-_energy)<.01f,"regeneration delay"); Tick(160);
        Require(_energy==100,"energy recovery");
        Console.WriteLine($"[AUTOTEST] Space peak={peak:F3}m landing=1.720m Ctrl={crouched:F3}m sprint={sprintEnergy:F1} dash={dashEnergy:F1} recovered={_energy}");

        // Walk into the table, vault its edge, stand on top, then jump just after stepping off.
        _playerPos=_world.CentralMacBookPos+new Vector3(.70f,1.72f,1.2f);
        _playerYaw=MathF.PI;
        Key(KeyboardKey.W,true); Tick(8);
        Require(_playerPos.Z>5.9f,"desk side collision");
        Key(KeyboardKey.Space,true); Tick(); Key(KeyboardKey.Space,false); Tick(8);
        Key(KeyboardKey.W,false); Tick(48);
        Require(_isGrounded && Math.Abs(_playerPos.Y-1.72f-SkyscraperFloor1World.TrapTop)<.01f,"table landing");
        Key(KeyboardKey.D,true);
        for(int i=0;i<18 && _isGrounded;i++) Tick();
        Key(KeyboardKey.D,false);
        Require(!_isGrounded && _coyoteTimer>0,"table edge");
        Key(KeyboardKey.Space,true); Tick(); Key(KeyboardKey.Space,false);
        Require(_playerVelY>7,"coyote jump");
        int descent=0;
        while((_playerVelY>=0 || _playerPos.Y>2.12f) && descent++<90) Tick();
        Key(KeyboardKey.Space,true); Tick(); Key(KeyboardKey.Space,false); Tick(6);
        Require(_playerVelY>5,"buffered landing jump");
        Console.WriteLine($"[AUTOTEST] Bait top={SkyscraperFloor1World.TrapTop:F3}m, side collision, vault, coyote and buffered jump PASS");

        ResetNewGameFloor();
        _playerPos=new(24,1.72f,17);
        Key(KeyboardKey.Three,true); Tick(); Key(KeyboardKey.Three,false);
        Mouse(true); Tick(60); Mouse(false); Tick();
        int burst=32-_weapons.Ammo;
        Require(burst==12,"held LMB burst: "+burst);
        Key(KeyboardKey.R,true); Tick(); Key(KeyboardKey.R,false);
        Tick(40); Require(_weapons.Reloading && _weapons.Ammo==20,"Uzi R animation");
        capture("reload_uzi.png"); Tick(49); Require(_weapons.Ammo==32 && !_weapons.Reloading,"Uzi refill");
        Key(KeyboardKey.Four,true); Tick(); Key(KeyboardKey.Four,false);
        Mouse(true); Tick(); Mouse(false); Tick(27); Require(_weapons.Ammo==1,"shotgun first shell");
        Mouse(true); Tick(); Mouse(false); Require(_weapons.Ammo==0 && _weapons.Reloading,"shotgun auto reload");
        Tick(43); Require(_weapons.BrassShells.Count(b=>b.IsShotgunShell)==2,"two spent hulls");
        capture("reload_shotgun.png"); Tick(52); Require(_weapons.Ammo==2 && !_weapons.Reloading,"shotgun refill");
        Mouse(true); Tick(); Mouse(false); Tick();
        Key(KeyboardKey.R,true); Tick(); Key(KeyboardKey.R,false); Tick(95);
        Require(_weapons.Ammo==2 && !_weapons.Reloading,"single-shell manual reload");
        Key(KeyboardKey.Three,true); Tick(); Key(KeyboardKey.Three,false);
        Mouse(true); Tick(156); Mouse(false);
        Require(_weapons.Ammo==0 && _weapons.Reloading,"Uzi empty auto reload");
        Tick(89); Require(_weapons.Ammo==32,"empty Uzi refill");
        Console.WriteLine($"[AUTOTEST] Uzi held 1s={burst} shots, R 20->32; shotgun 2->1->0->2, two ejected hulls");

        ResetNewGameFloor();
        var fighters=_world.ActiveCoders.Where(c=>c.IsCombatCoder).ToArray();
        Require(fighters.Length==4 && fighters.All(c=>c.MaxHp is >=40 and <=55),"weak fighters");
        var enemy=fighters[0];
        // Keep the test lane clear of the nearby architectural pillar.
        _playerPos=new(-5.6f,1.72f,5.6f);
        enemy.Position=new(-5.6f,0,10.4f);
        Tick(32);
        Require(enemy.Aggro && enemy.Windup>0 && _world.EnemyProjectiles.Count==0,"warn before throw");
        for(int i=0;i<60 && !_world.EnemyProjectiles.Any(p=>p.IsGlitch);i++) Tick();
        var pr=_world.EnemyProjectiles.First(p=>p.IsGlitch);
        Require(Math.Abs(pr.Velocity.Length()-12)<.01f && pr.Damage==7,"weak thrown cube");
        int hp=_playerHp; Tick(35);
        Require(hp-_playerHp==7,"projectile hit damage");
        int wait=0;
        while(!_world.EnemyProjectiles.Any(p=>p.IsGlitch) && wait++<200) Tick();
        Require(wait<200,"second telegraph");
        hp=_playerHp;
        Key(KeyboardKey.Space,true); Tick(); Key(KeyboardKey.Space,false); Tick(64);
        Require(_playerHp==hp && !_world.EnemyProjectiles.Any(p=>p.IsGlitch && Vector3.Distance(p.Position,_playerPos)<2),"jump dodges locked throw");
        _playerPos=enemy.Position+new Vector3(0,1.72f,1.9f);
        int meleeHp=_playerHp;
        for(int i=0;i<160 && _playerHp==meleeHp;i++) Tick();
        Require(meleeHp-_playerHp==6,"telegraphed laptop slap");
        _weapons.SelectSlot(3);
        LookAtTarget(enemy.CombatCenter); Mouse(true); Tick(); Mouse(false);
        Require(enemy.HitStaggerTimer>0 && enemy.KnockbackVel.Length()>2 && enemy.Hp>0,"impact impulse");
        Vector3 hitPos=enemy.Position; Tick(6); float knockback=Vector3.Distance(hitPos,enemy.Position);
        Require(knockback>.1f,"knockback displacement");
        int shots=1, cash=_playerCash, xp=_xp;
        while(enemy.Hp>0 && shots<4)
        {
            LookAtTarget(enemy.CombatCenter); Mouse(true); Tick(); Mouse(false); Tick(6); shots++;
        }
        Require(enemy.Hp==0 && shots<=3 && _playerCash-cash==35 && _xp-xp==7,"KO reward");
        Tick(90); Require(!_world.ActiveCoders.Contains(enemy),"KO cleanup");
        Require(Math.Abs(_recoilPitch)<.0001f && Math.Abs(_recoilYaw)<.0001f,"camera spring recovery");
        foreach(var (weapon,hits,target) in new[] {
            (WeaponType.Belt,2,fighters[1]),(WeaponType.SolderingIron,2,fighters[2]),(WeaponType.SawedOff,1,fighters[3]) })
        {
            _playerPos=new(23,1.72f,17); target.Position=new(23,0,15.15f);
            _weapons.SelectSlot((int)weapon);
            for(int i=0;i<hits;i++)
            {
                LookAtTarget(target.CombatCenter);
                Mouse(true); Tick(); Mouse(false); Tick(29);
                if(weapon==WeaponType.SawedOff) Require(target.Hp==0,"lethal buckshot");
                else Require(target.Hp>0 && (target.Fear>6 || target.State==CoderAIState.Subdued),"non-lethal pressure "+weapon);
            }
        }
        var wallTarget=_world.SpawnCoderForTier(SubscriptionBaitTier.FreeTier);
        wallTarget.Position=new(0,0,-2);
        _playerPos=new(0,1.72f,2.8f);
        LookAtTarget(wallTarget.CombatCenter);
        _weapons.SelectSlot(3);
        float wallHp=wallTarget.Hp;
        Mouse(true); Tick(); Mouse(false);
        Require(wallTarget.Hp==wallHp && Math.Abs(_weapons.BulletTracers[^1].End.Z-.5f)<.02f,"wall stops ray and tracer");
        Console.WriteLine("[AUTOTEST] Jump dodge, recoil recovery, wall impact, non-lethal melee and lethal shotgun PASS");
        Console.WriteLine($"[AUTOTEST] Fighters={fighters.Length} HP={enemy.MaxHp} cube={pr.Velocity.Length():F0}m/s damage=7 slap=6 knockback={knockback:F2}m KO={shots} rounds cash=+35 XP=+7");
    }

    private static void RunAutomatedScreenshotSuite()
    {
        CheckViewmodelRates();
        AudioMusicSystem.CheckMovementSounds();
        CheckElevatorNameplates();
        LoreAndMenusUI.CheckMixerControls();
        string projectScreenshotsDir = Environment.GetEnvironmentVariable("VIBECODER_SCREENSHOTS")
            ?? Path.Combine(Directory.GetCurrentDirectory(), "screenshots");
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
        try
        {
            Raylib.SetWindowSize(961,643); Raylib.PollInputEvents();
            DrawFrame();
            if(!VisualEngine3D.MatchesViewport(961,643)) throw new InvalidDataException("HDR resize failed");
        }
        finally
        {
            Raylib.SetWindowSize(ScreenWidth,ScreenHeight); Raylib.PollInputEvents();
            DrawFrame();
        }

        // Switch into active 3D gameplay
        MainMenuAndPauseUI.IsInMainMenu = false;
        LoreAndMenusUI.IsLoreOpen = false;
        LoreAndMenusUI.IsSettingsOpen = false;
        LoreAndMenusUI.IsCaseOpen = false;

        CheckSprintA(name=>CaptureShot(name));
        CheckFpsGameplay(name=>{ _hudStatusTimer=0; CaptureShot(name); });

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
        _eyeHeight=1.16f;
        _playerPos = new Vector3(-1.6f, _eyeHeight, 8.05f);
        LookAtTarget(new Vector3(-.15f, .77f, 5.2f));
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
                    CaptureShot("crop_inspect_f.png",detail:new(400,210,690,440),cropOnly:true);
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
        // A coder using the bait takes a guaranteed critical hit on the gameplay path.
        miniBoss.State = CoderAIState.SittingAtMacBook;
        LookAtTarget(miniBoss.CombatCenter);
        miniBoss.Hp = 420f;
        miniBoss.HitFlashTimer = 0;
        var fighter=_world.ActiveCoders.First(c=>c.IsCombatCoder);
        fighter.Position=new(-.66f,0,5.95f);
        for(int i=0;i<100 && !_world.EnemyProjectiles.Any(p=>p.IsGlitch);i++) TickFloor(1f/60);
        for(int i=0;i<5;i++) TickFloor(1f/60);
        for(int i=0;i<_world.EnemyProjectiles.Count;i++)
        {
            if (_world.EnemyProjectiles[i].IsGlitch)
            {
                var gp = _world.EnemyProjectiles[i];
                gp.Position += new Vector3(0.26f, 0.05f, 0.22f);
                _world.EnemyProjectiles[i] = gp;
            }
        }
        LookAtTarget(miniBoss.CombatCenter);
        float hpBeforeShot = miniBoss.Hp;
        UpdateTrigger(1f/60,true,true,false,false);
        if (_weapons.Ammo!=31 || hpBeforeShot-miniBoss.Hp != 46 || !_weapons.HitmarkerIsCrit ||
            _weapons.FloatingTexts.LastOrDefault().Text != "-46")
            throw new InvalidDataException("Critical hit on the scaled coder did not produce the expected damage number");
        UpdateTrigger(.012f,false,false,false,false);
        _hudStatusTimer = AudioMusicSystem.VoiceTimer = 0;

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
        UpdateTrigger(1f/60,true,true,false,false);
        if (_weapons.Ammo!=1 || _world.ElevatorBoss.Hp>=1480) throw new InvalidDataException("Shotgun boss shot failed");
        UpdateTrigger(.018f,false,false,false,false);
        _hudStatusTimer = 0;
        StagePartner(new(.1f,0,-10.4f),_world.ElevatorBoss.Position+Vector3.UnitY*1.4f);
        CaptureShot("05_elevator_boss_escaped_agent.png","crop_slot_4_shotgun.png",new(605,350,520,370));

        // 6. 06_lore_chats_window_n.png - Open Corporate Leak & Slack/TG Chats Window [N]
        LoreAndMenusUI.IsLoreOpen = true;
        LoreAndMenusUI.ActiveLoreTab = 0;
        LoreAndMenusUI.IsSettingsOpen = false;
        LoreAndMenusUI.IsCaseOpen = false;
        CaptureShot("06_lore_chats_window_n.png");
        LoreAndMenusUI.IsLoreOpen = false;
        CheckArchiveControls();

        // 7. 07_settings_mixer_and_mods.png (and 07_settings_mixer_and_cases.png) - Settings / Audio Mixer [F8]
        LoreAndMenusUI.IsLoreOpen = false;
        LoreAndMenusUI.IsSettingsOpen = true;
        LoreAndMenusUI.IsCaseOpen = false;
        CaptureShot("07_settings_mixer_and_mods.png");

    }

    private static void LookAtTarget(Vector3 target)
    {
        Vector3 eye = BuildPlayerCamera().Position;
        Vector3 d = Vector3.Normalize(target - eye);
        _playerYaw = MathF.Atan2(d.X, d.Z);
        _playerPitch = MathF.Asin(Math.Clamp(d.Y, -0.95f, 0.95f));
    }
}

public static partial class Program
{
    private static unsafe void CheckSprintA(Action<string> capture)
    {
        ResetNewGameFloor();
        Raylib.DisableCursor();
        static void Check(bool ok,string step)
        {
            if(!ok) throw new InvalidDataException("Sprint A: "+step);
        }
        Check(VibeCoderCollection.All.Select(s=>s.Id).Distinct().Count()==16,"unique species IDs");
        var distribution=new int[4];
        var rng=new Random(829);
        for(int i=0;i<12000;i++)
            distribution[(int)VibeCoderCollection.Roll(rng,SubscriptionBaitTier.FreeTier,Floor).Rarity]++;
        Check(Math.Abs(distribution[1]/12000f-.22f)<.015f && Math.Abs(distribution[2]/12000f-.10f)<.015f &&
            Math.Abs(distribution[3]/12000f-.04f)<.01f,"baseline rarity weights");
        bool exit=false;
        void Key(KeyboardKey key)
        {
            AutomationEvent ev=new() { Type=2 }; ev.Params[0]=(int)key;
            Raylib.PlayAutomationEvent(ev); UpdateFrame(1f/60,ref exit); Raylib.PollInputEvents();
            ev.Type=1; Raylib.PlayAutomationEvent(ev); Raylib.PollInputEvents();
        }
        void At(Vector3 eye,Vector3 target) { _playerPos=eye; LookAtTarget(target); }
        string Use(Vector3 feet,float height)
        {
            At(feet+new Vector3(0,1.72f,1.9f),feet+Vector3.UnitY*height);
            return _world.Interact(BuildPlayerCamera(),TrySpendCash,GrantCash);
        }
        void Strike(WeaponType tool,Vector3 target)
        {
            _weapons.SelectSlot((int)tool); LookAtTarget(target);
            _world.ProcessPlayerAttack(BuildPlayerCamera(),_weapons,GrantCash,out _);
        }
        var roles=_world.ActiveCoders.ToArray();
        Check(!Raylib.IsAudioDeviceReady() || Raylib.GetMasterVolume()==0,"native audio muted");
        Check(roles.Select(c=>(c.Composure,c.Ego,c.Fear,c.Greed)).Distinct().Count()==4,"four psychological profiles");
        Key(KeyboardKey.J); Check(floorPanel==FloorPanel.Contracts,"J opens board");
        Key(KeyboardKey.Enter); Check(Floor.JobId==0 && Floor.TD>0 && Floor.PT>0,"MVP accepted");
        At(new(24,1.72f,20),new(24,2,28));
        for(int i=0;i<9;i++) Strike(WeaponType.MicroUzi,new(24,2,28));
        float before=Floor.SEC;
        var printer=_world.OfficeProps.First(p=>p.Kind==OfficePropKind.XeroxPhotocopier);
        Use(printer.Position,.9f);
        Check(before>20 && Math.Abs(Floor.SEC-before+20)<.01f && Floor.PrinterWait==35,"gunfire and printed pass");
        Use(printer.Position,.9f);
        Check(Math.Abs(Floor.SEC-before+20)<.01f,"printer cooldown");

        var junior=roles[0];
        junior.Position=new(24,0,17); At(new(24,1.72f,19),junior.CombatCenter);
        Strike(WeaponType.Belt,junior.CombatCenter);
        Check(junior.State==CoderAIState.Subdued && junior.Hp==junior.MaxHp && junior.LaptopLost,"belt breaks resolve at full HP");
        before=Floor.SEC;
        Use(junior.Position,.45f);
        Check(_world.CarriedCoder==junior && Floor.SEC>before && Floor.SEC-before<1,"live NDA");
        var desk=_world.GalleyDesks[0];
        Use(desk.DeskPosition,.9f*SkyscraperFloor1World.WorkstationScale);
        Check(Floor.Seated==1 && desk.Productivity==14,"junior payroll");
        float debt=Floor.TD;
        _world.UpdateWorld(.5f,_playerPos,false,(p,_)=>_playerCash+=p,null);
        Check(Floor.TD>debt && Floor.Earned>=7,"junior generates real cash and debt");

        var crypto=roles[1]; crypto.Position=new(24,0,15);
        At(new(24,1.55f,19),crypto.CombatCenter);
        Vector3 aim=Vector3.Normalize(crypto.CombatCenter-_playerPos);
        _world.EnemyProjectiles.Add(new() { Position=_playerPos+aim*1.8f,Velocity=-aim*12,Radius=.14f,
            OwnerId=crypto.Id,Damage=7,Life=2,MaxLife=2,IsGlitch=true,Coin=true });
        Strike(WeaponType.Belt,crypto.CombatCenter);
        Check(_world.EnemyProjectiles.Exists(p=>p.Deflected && p.Damage==14),"belt parry");
        float hp=crypto.Hp;
        for(int i=0;i<16;i++) _world.UpdateWorld(1f/60,_playerPos,false,null,null);
        Check(crypto.Hp==hp-14,"reflected projectile hit owner");

        float temp=Floor.PT;
        Use(SkyscraperFloor1World.DeployPos,1.12f);
        Check(Floor.Friday==45 && Floor.FridayMultiplier==2.5f && Floor.PT>=temp+29.99f,"physical deploy button");
        var cooler=_world.CoolerPos;
        Use(cooler,.9f); Check(Floor.Puddles.Single().Wet==18,"physical cooler spill");
        crypto.Position=cooler+new Vector3(1.25f,0,.65f);
        var water=cooler+new Vector3(.6f,0,1.5f);
        At(cooler+new Vector3(1.9f,1.72f,3.5f),water);
        Strike(WeaponType.SolderingIron,water);
        Check(Floor.Puddles.Single().Electric==6 && crypto.StunTimer>0 && crypto.Fear>=45,"solder conducts through water");
        var junior2=_world.SpawnCoderForTier(SubscriptionBaitTier.FreeTier);
        var rare=VibeCoderCollection.All.Single(s=>s.Id=="smoothie");
        junior2.AssignRole(rare.Archetype);
        VibeCoderCollection.Dress(junior2,rare);
        junior2.Position=cooler+new Vector3(.1f,0,1.15f);
        _world.UpdateWorld(1f/60,_playerPos,false,null,null);
        Check(junior2.State==CoderAIState.Subdued && junior2.Hp>0,"puddle breaks coder on entry");
        At(cooler+new Vector3(3.85f,1.52f,2.8f),cooler+new Vector3(.05f,.83f,.1f));
        _weapons.UpdateWeapons(.25f,BuildPlayerCamera());
        _hudStatusTimer=AudioMusicSystem.VoiceTimer=0;
        Floor.DiscoveryTime=0;
        Check(VisualEngine3D.HdrAndShadowsReady,"native FP16 scene and 2048 shadow FBO");
        Check(!junior2.IdentityRevealed && junior2.MysteryTag.StartsWith("★ ???"),"blind rare hunt");
        capture("09_sprint_a_systems.png");
        VisualEngine3D.VerifyLightingBuffers();
        int catches=Floor.DiscoveredSpecies.GetValueOrDefault(rare.Id),wallet=_playerCash;
        Use(junior2.Position,.45f);
        Check(catches==0,"fixture must discover a new rare species");
        Check(junior2.IdentityRevealed && junior2.Name==rare.Name &&
            Floor.DiscoveredSpecies[rare.Id]==catches+1 && _playerCash==wallet+(catches==0 ? rare.BonusPayout : 0),"rare capture and premium");
        Check(Floor.FirstDiscovery && Floor.DiscoveryId==rare.Id && Floor.DiscoveryTime==4,"first discovery reveal");
        Floor.Tick(.45f,_world,null);
        _hudStatusTimer=AudioMusicSystem.VoiceTimer=0;
        capture("10_collection_discovery.png");
        var desk2=_world.GalleyDesks[1];
        Use(desk2.DeskPosition,.9f*SkyscraperFloor1World.WorkstationScale);
        Check(Floor.Seated==2 && desk2.CoderName==rare.Name,"second desk receives recruit");
        var data=MainMenuAndPauseUI.CaptureCurrentGameState!();
        string slot=Path.GetTempFileName();
        var activeWorld=_world;
        try
        {
            File.WriteAllText(slot,System.Text.Json.JsonSerializer.Serialize(data));
            WriteFloorSave(data,slot,pendingShift!);
            _world=new SkyscraperFloor1World();
            RestoreFloorSave(data,slot);
            Check(Floor.DiscoveredSpecies[rare.Id]==catches+1,"rare catch survives slot load");
            Key(KeyboardKey.N); Key(KeyboardKey.Three);
            int index=Array.IndexOf(VibeCoderCollection.All,rare);
            for(int i=0;i<index;i++) Key(KeyboardKey.Down);
            for(int i=0;i<45;i++) UpdateFrame(1f/60,ref exit);
            Check(LoreAndMenusUI.ActiveLoreTab==2 && LoreAndMenusUI.SelectedArchiveEntry==index &&
                LoreAndMenusUI.CollectionEntry(index)==rare.Name && LoreAndMenusUI.CollectionEntry(15).StartsWith("???"),"archive unlocked and locked identities");
            capture("10_collection.png");
            Key(KeyboardKey.Down);
            for(int i=0;i<45;i++) UpdateFrame(1f/60,ref exit);
            Check(LoreAndMenusUI.CollectionEntry(index+1).StartsWith("???"),"selected locked dossier");
            capture("11_collection_locked.png");
            Key(KeyboardKey.N);
            var repeat=_world.SpawnFighter(rare.Archetype,new(25,0,20),rare);
            repeat.State=CoderAIState.Subdued;
            int afterDiscovery=_playerCash;
            Use(repeat.Position,.45f);
            Check(_world.CarriedCoder==repeat && Floor.DiscoveredSpecies[rare.Id]==2 && _playerCash==afterDiscovery &&
                !Floor.FirstDiscovery,"repeat rare increments without another discovery premium");
            Use(_world.HrTarotBoothPos,1.6f);
            Check(_world.LastTarotVerdictText.Contains(rare.Name),"HR receipt preserves discovered name");
            Console.WriteLine("[AUTOTEST] PASS collection: rarity distribution, blind NDA, first and repeat catches, save/load, HR, galley, archive");
        }
        finally
        {
            _world=activeWorld; _playerCash=data.Cash; pendingFloorSave=null; pendingShift=null;
            File.Delete(slot);
        }
        for(int i=0;i<600 && Floor.JobId>=0;i++) TickFloor(1f/60);
        Check(Floor.Report is { Success:true, Job:0, Captured:2 } && Floor.Earned>=450,"MVP settlement");
        UpdateFrame(1f/60,ref exit); Check(floorPanel==FloorPanel.Debrief,"automatic debrief");
        capture("08_contract_debrief.png");
        Key(KeyboardKey.Enter); Check(floorPanel==FloorPanel.Contracts,"next contract route"); Key(KeyboardKey.Escape);

        // Exercise the remaining win/fail conditions independently of screenshot staging.
        int cash=4000;
        bool Spend(int price) { if(price>cash) return false; cash-=price; return true; }
        foreach(var gear in Enum.GetValues<Sidegrade>()) Floor.Buy((int)gear,Spend);
        Check(cash==4000-FloorSystems.GearList.Sum(g=>g.Price) && Floor.Shell==Sidegrade.Negotiator,"six paid sidegrades");
        Floor.Buy((int)Sidegrade.Salt,Spend);
        Check(Floor.Shell==Sidegrade.Salt,"swap loads without another charge");
        Floor.IncidentNow(Incident.Raid,_world); Floor.Risk(security:45);
        float sec=Floor.SEC;
        Floor.UsePager(_world);
        Check(!Floor.Raid && !_world.ActiveCoders.Any(c=>c.RaidGuard) && Math.Abs(Floor.SEC-Math.Max(0,sec-35))<.01f,"pager withdraws raid");
        Floor.IncidentNow(Incident.Ddos,_world);
        Check(Floor.Ddos && _world.DeskRate(desk,false)==desk.Productivity*Floor.FridayMultiplier*.5f,"DDoS halves output");
        Use(_world.RackPos,1.2f); Check(!Floor.Ddos,"physical reboot");
        Floor.IncidentNow(Incident.Burnout,_world); Use(SkyscraperFloor1World.CoffeePos,1.15f);
        Check(Floor.Coffee==12 && !_world.GalleyDesks.Exists(d=>d.IsOccupied && d.Burnout>=100),"coffee recovery");
        Floor.Start(1,_world); Floor.Risk(security:50); Floor.Tick(.01f,_world,null);
        Check(Floor.Report is {Job:1,Success:false},"stealth violation");
        Floor.Risk(security:-100); Floor.Start(1,_world);
        var senior=roles[3];
        senior.Position=new(24,0,17); At(new(24,1.72f,19),senior.CombatCenter);
        Strike(WeaponType.SolderingIron,senior.CombatCenter); Strike(WeaponType.Belt,senior.CombatCenter);
        Check(senior.State==CoderAIState.Subdued,"architect ego break");
        Use(senior.Position,.45f); Use(_world.GalleyDesks[2].DeskPosition,.9f*SkyscraperFloor1World.WorkstationScale);
        Floor.Tick(.01f,_world,null);
        Check(Floor.Report is {Job:1,Success:true} && _world.GalleyDesks[2].TechDebtRate<0,"architect stealth delivery");
        Floor.Start(2,_world); Floor.Deliver(CoderArchetype.CryptoBro,false,250); Floor.Tick(.01f,_world,null);
        Check(Floor.Report is {Job:2,Success:true},"fintech delivery");
        ResetNewGameFloor();
        Floor.Start(3,_world); Use(SkyscraperFloor1World.DeployPos,1.12f);
        Check(Floor.ReleaseStarted && Floor.Friday==45,"Friday contract armed");
        for(int i=0;i<451;i++)
        {
            var threat=_world.ActiveCoders.Find(c=>c.ReleaseAttacker && c.Hp>0 && c.State!=CoderAIState.Subdued);
            if(threat!=null && i%4==0)
            {
                At(threat.Position+new Vector3(0,1.72f,1.8f),threat.CombatCenter);
                Strike(WeaponType.SawedOff,threat.CombatCenter);
            }
            TickFloor(.1f);
        }
        Check(Floor.Report is {Job:3,Success:true,Bonus:>=2.5f},"Friday survival: "+Floor.Report?.Diagnosis+" / "+Floor.Defense+" / "+Floor.Friday);
        Floor.Start(4,_world); Check(_world.ElevatorBoss.IsActive,"contract summons agent");
        _world.ElevatorBoss.Hp=1; _world.ElevatorBoss.Position=new(24,0,17);
        At(new(24,1.72f,20),new(24,1.35f,17)); Strike(WeaponType.MicroUzi,new(24,1.35f,17));
        Floor.Tick(.01f,_world,null); Check(Floor.Report is {Job:4,Success:true},"agent liquidation");
        var restoreData=MainMenuAndPauseUI.CaptureCurrentGameState!();
        pendingFloorSave=null;
        string scratch=Path.GetTempFileName();
        try
        {
            File.WriteAllText(scratch,System.Text.Json.JsonSerializer.Serialize(restoreData));
            WriteFloorSave(restoreData,scratch,pendingShift!);
            pendingShift=null;
            var expected=Floor;
            ResetNewGameFloor(); RestoreFloorSave(restoreData,scratch);
            Check(Floor.Report==expected.Report && Floor.SEC==expected.SEC && Floor.PT==expected.PT &&
                Floor.TD==expected.TD && Floor.Serial==expected.Serial,"saved shift round-trip");
        }
        finally { File.Delete(scratch); }
        ResetNewGameFloor();
        var rack=_world.RackPos;
        var near=_world.ActiveCoders[2]; near.Position=rack+new Vector3(2,0,0);
        At(rack+new Vector3(0,1.72f,2.5f),rack+Vector3.UnitY);
        Strike(WeaponType.SolderingIron,rack+Vector3.UnitY);
        Check(Floor.RackHeat>0 && Floor.PT>=12 && near.State==CoderAIState.PanickedFleeing,"physical B12 overheat and evacuation");
        float hot=Floor.PT; Use(rack,1.2f);
        Check(Floor.RackHeat==0 && Floor.PT<hot,"physical B12 cool-down");
        Floor.IncidentNow(Incident.Printer,_world); Use(printer.Position,.9f);
        Check(Floor.Jam>0 && Floor.PrinterWait==0,"jam blocks pass");
        Floor.IncidentNow(Incident.Audit,_world); Floor.Tick(15,_world,null);
        Check(Floor.SEC>=22 && Floor.Audit==0,"unanswered audit raises security");
        Floor.IncidentNow(Incident.Audit,_world);
        int balance=_playerCash;
        Use(_world.HrTarotBoothPos+new Vector3(-1.25f,0,.64f),1.33f);
        Check(_playerCash==balance-180 && Floor.Audit==0,"audit bribe paid once");
        Floor.IncidentNow(Incident.Fire,_world); balance=_playerCash;
        Use(rack,1.2f); Use(rack,1.2f);
        Check(!Floor.FireIncident && _playerCash==balance+120,"single emergency cooling payout");
        int population=_world.ActiveCoders.Count;
        Floor.IncidentNow(Incident.Bugs,_world);
        Check(_world.ActiveCoders.Count>population,"bug surge");
        Floor.IncidentNow(Incident.Coffee,_world); Use(SkyscraperFloor1World.CoffeePos,1.15f);
        Check(!Floor.WantsCoffee && Floor.Coffee==12,"coffee demand resolved");
        Floor.Buy((int)Sidegrade.Drum,_=>true);
        _weapons.SelectSlot(3); UpdateTrigger(1f/60,true,true,false,false);
        _weapons.BeginReload();
        _weapons.UpdateWeapons(1.46f,BuildPlayerCamera());
        Check(_weapons.Capacity==50 && _weapons.Reloading,"drum reload tradeoff");
        _weapons.UpdateWeapons(.45f,BuildPlayerCamera()); Check(_weapons.Ammo==50,"drum refills to 50");
        ResetNewGameFloor();
        Console.WriteLine("[AUTOTEST] Sprint A PASS: risk, four profiles, NDA, payroll, parry, electro-puddle, Friday, five contracts, debrief and sidegrades");
    }
}
