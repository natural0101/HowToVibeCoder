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
    private static readonly ElevatorRepairMinigame _repairMinigame = new();
    private static Font _uiFont;

    // Player state
    private static readonly Vector3 LobbyEntrance = new(.55f,1.72f,7.05f);
    private static Vector3 _playerPos = LobbyEntrance;
    private static float _playerYaw;
    private static float _playerPitch = -0.05f;
    private static bool _isCrouching = false;
    private static float _playerVelY, _coyoteTimer, _jumpBufferTimer;
    private static float _eyeHeight = 1.72f, _dashTime, _dashCooldown, _regenDelay, _fovKick;
    private static float _recoilPitch, _recoilYaw, _shake, _crosshairGap = 4, _playerHitAnimTimer;
    private static float _combatHitstopTimer;
    private struct DirectionalHitArc
    {
        public Vector3 SourcePos;
        public int Damage;
        public float Age;
        public float Duration;
    }
    private static readonly DirectionalHitArc[] _hitArcs = new DirectionalHitArc[4];
    private static int _hitArcCursor;
    private const float HitArcDuration = 0.65f;
    private static float _hitmarkerAge, _hitmarkerDuration;
    private static bool _hitmarkerCrit, _hitmarkerKill;
    private static float _prevWeaponHitmarkerTimer;

    public static void TriggerHitstop(float seconds)
    {
        float clamped = Math.Clamp(seconds, 0f, 0.08f);
        if (clamped > _combatHitstopTimer) _combatHitstopTimer = clamped;
    }

    public static void TriggerDirectionalHitIndicator(Vector3 attackerWorldPos, int dmg)
    {
        for (int i = 0; i < _hitArcs.Length; i++)
        {
            if (_hitArcs[i].Duration > 0f && _hitArcs[i].Age < _hitArcs[i].Duration &&
                Vector3.DistanceSquared(_hitArcs[i].SourcePos, attackerWorldPos) < 2.25f)
            {
                _hitArcs[i] = new DirectionalHitArc
                {
                    SourcePos = attackerWorldPos,
                    Damage = Math.Max(dmg, _hitArcs[i].Damage),
                    Age = 0f,
                    Duration = HitArcDuration
                };
                return;
            }
        }
        _hitArcs[_hitArcCursor] = new DirectionalHitArc
        {
            SourcePos = attackerWorldPos,
            Damage = dmg,
            Age = 0f,
            Duration = HitArcDuration
        };
        _hitArcCursor = (_hitArcCursor + 1) % _hitArcs.Length;
    }

    public static void TriggerHitmarker(bool crit, bool kill)
    {
        if (_hitmarkerDuration > 0f && _hitmarkerAge < _hitmarkerDuration * 0.55f && _hitmarkerKill && !kill)
            return;
        _hitmarkerAge = 0f;
        _hitmarkerCrit = crit;
        _hitmarkerKill = kill;
        _hitmarkerDuration = kill ? 0.30f : crit ? 0.24f : 0.19f;
        _crosshairGap = MathF.Min(20f, _crosshairGap + (kill ? 4.2f : crit ? 3.0f : 1.6f));
    }

    public static void TriggerCrosshairBloom(float amount)
    {
        if (amount > 0f)
            _crosshairGap = MathF.Min(22f, _crosshairGap + amount);
    }

    private static void ResetCombatFeedback()
    {
        _combatHitstopTimer = 0f;
        _hitmarkerAge = _hitmarkerDuration = _prevWeaponHitmarkerTimer = 0f;
        _hitmarkerCrit = _hitmarkerKill = false;
        Array.Clear(_hitArcs);
        _hitArcCursor = 0;
        ResetBossBarTelemetry();
    }

    private static void TickCombatFeedback(float dt)
    {
        if (dt <= 0f) return;
        for (int i = 0; i < _hitArcs.Length; i++)
        {
            if (_hitArcs[i].Duration <= 0f) continue;
            _hitArcs[i].Age += dt;
            if (_hitArcs[i].Age >= _hitArcs[i].Duration)
                _hitArcs[i].Duration = 0f;
        }
        if (_hitmarkerDuration > 0f)
        {
            _hitmarkerAge += dt;
            if (_hitmarkerAge >= _hitmarkerDuration)
                _hitmarkerDuration = 0f;
        }
        if (_weapons != null)
        {
            if (_weapons.HitmarkerTimer > _prevWeaponHitmarkerTimer + 0.01f)
                TriggerHitmarker(_weapons.HitmarkerIsCrit, kill: false);
            _prevWeaponHitmarkerTimer = _weapons.HitmarkerTimer;
        }
    }

    private static float _pitchSpring, _yawSpring;
    private static Vector3 _dashDirection;
    private static bool _isGrounded = true, _sprinting, _toggleCrouch, _exhausted;
    internal static bool PlayerAirborne => !_isGrounded;
    internal static bool PlayerDashing => _dashTime > 0f;
    private static Vector2 _playerHitDir;
    private static float PlayerHitWeight => MathF.Pow(Math.Clamp(_playerHitAnimTimer/.22f,0,1),1.6f);
    private static void TickHitReaction(float dt)
    {
        _playerHitAnimTimer=Math.Max(0,_playerHitAnimTimer-dt);
        _weapons.HitJolt=PlayerHitWeight;
    }
    private static readonly Random ShotRandom = new(413);
    private static float _walkBobPhase = 0f;

    private static int _playerCash;
    internal static int SharedCash => _playerCash;
    private static int _playerHp = 100;
    private static Vector3 _lastDamageSourcePos;
    private static string _lastDamageLabel="";
    private static int _lastDamageAmount;
    private static float _lastDamageTimer, HealFlashTimer;
    private static bool guestInteraction;
    private static ref int GuestCups => ref ((Net.Guest ? Net.LocalSlot : actorSlot)==2 ? ref Floor.SecondCoffeeCups : ref Floor.GuestCoffeeCups);
    internal static string PausePlayerCount => $"{Net.ActivePlayerCount}/3";
    internal static bool NeedsHealing => (guestInteraction ? guestHp : _playerHp)<100;

    private static void ShowHit(int amount,string label,Vector3 source)
    {
        var incoming=new Vector2(source.X-_playerPos.X,source.Z-_playerPos.Z);
        var forward=new Vector2(MathF.Sin(_playerYaw),MathF.Cos(_playerYaw));
        if(incoming.LengthSquared()>.0001f) incoming=Vector2.Normalize(incoming);
        _playerHitDir=new(Vector2.Dot(incoming,new(-forward.Y,forward.X)),Vector2.Dot(incoming,forward));
        _playerHitAnimTimer=.22f;
        _weapons.HitJolt=1;
        _lastDamageAmount=amount; _lastDamageLabel=label; _lastDamageSourcePos=source; _lastDamageTimer=2.6f;
        TriggerDirectionalHitIndicator(source, amount);
        AudioMusicSystem.PlayLanding();
    }
    private static void ReceiveDamage(int amount,string label,Vector3 source)
    {
        if(guestInteraction) { GuestDamage(amount,label,source); return; }
        if(_knockoutActive) return;
        if(Net.Host && _playerHp<=amount) MarkDown(0);
        _playerHp=AfterDamage(_playerHp,amount);
        ShowHit(amount,label,source);
        if(_playerHp==0) { BeginKnockout(); return; }
        BeginHealLesson();
    }
    private static bool _knockoutActive;
    private static float _knockoutTime;
    private static int _knockoutSave;

    private static void BeginKnockout()
    {
        if(_knockoutActive) return;
        _knockoutActive=true; _knockoutTime=0;
        AudioMusicSystem.PlayKnockout();
        Raylib.EnableCursor();
        MainMenuAndPauseUI.RefreshSaveSlotsCache();
        _knockoutSave=Net.Guest ? -1 : MainMenuAndPauseUI.LatestSave;
    }
    private static void PayWakePenalty()=>_playerCash-=Math.Min(100,(int)(_playerCash*.15m));
    private static void FinishWake()
    {
        _knockoutActive=false; _playerHp=100;
        _eyeHeight=1.72f; _playerPos=_world.ResolvePlayerCollision(LobbyEntrance);
        _dashTime=_playerVelY=_lastDamageTimer=_playerHitAnimTimer=_jumpBufferTimer=_weapons.HitJolt=0;
        ResetCombatFeedback();
        _isGrounded=true; _toggleCrouch=_isCrouching=false;
        LookAtTarget(_world.HrTarotBoothPos+Vector3.UnitY*1.4f);
        Raylib.DisableCursor();
    }
    internal static bool DevMode;
    private static Texture2D keycardIconTex, knockoutClipboardTex;
    private static Rectangle KnockoutButton(int i)=>new(368,368+64*i,540,50);
    private static void UpdateKnockout(float dt)
    {
        _knockoutTime+=dt;
        if(_knockoutTime<.55f) return;
        var mouse=(Raylib.GetMousePosition()-PanelOffset)/PanelScale;
        bool Click(int i)=>Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mouse,KnockoutButton(i));
        if(Raylib.IsKeyPressed(KeyboardKey.R) || Raylib.IsKeyPressed(KeyboardKey.Enter) || Click(0))
        {
            if(Net.Guest) { Net.Send(new("wake")); return; }
            PayWakePenalty(); Floor.PocketCoffeeCups=Math.Max(1,Floor.PocketCoffeeCups);
            foreach(var coder in _world.ActiveCoders) { coder.Aggro=false; coder.Windup=0; coder.AttackCooldown=3; coder.ReleaseAttacker=false; }
            if(_world.ElevatorBoss.IsActive) _world.ElevatorBoss=new(){Position=_world.CentralElevatorCorePos+Vector3.UnitY*.16f};
            _world.EnemyProjectiles.Clear(); Floor.HostStun=0; downUntil[0]=0;
            FinishWake();
        }
        else if(_knockoutSave>=0 && (Raylib.IsKeyPressed(KeyboardKey.L) || Click(1))) MainMenuAndPauseUI.LoadGameState(_knockoutSave);
        else if(Raylib.IsKeyPressed(KeyboardKey.M) || Click(2)) { _knockoutActive=false; MainMenuAndPauseUI.OpenMainMenu(); }
    }
    private static void DrawKnockout()
    {
        float fade=Math.Clamp(_knockoutTime/.55f,0,1);
        int w=Raylib.GetScreenWidth(),h=Raylib.GetScreenHeight();
        Raylib.DrawRectangle(0,0,w,h,new Color(38,6,12,(int)(210*fade)));
        if(fade<1) return;
        Raylib.BeginMode2D(new Camera2D{Zoom=PanelScale,Offset=PanelOffset});
        if(knockoutClipboardTex.Id!=0)
            Raylib.DrawTexturePro(knockoutClipboardTex,new(0,0,knockoutClipboardTex.Width,knockoutClipboardTex.Height),new(0,0,1280,720),Vector2.Zero,0,Color.White);
        else
            LoreAndMenusUI.DrawManilaPaper(new(280,64,720,600));

        Color ink=LoreAndMenusUI.ThemeSepiaInk, muted=LoreAndMenusUI.ThemeSepiaMuted, red=LoreAndMenusUI.ThemeStampRed;
        // Bold red incident header stamped directly onto the aged cream HR clipboard paper
        GameFonts.Draw(GameFonts.Get(Typeface.SerifBold,54),Loc.T("НОКАУТ","KNOCKED OUT"),new(368,176),54,red,shadowed:false);
        // Subtle tilted crimson rubber HR stamp on top-right of the sheet
        string stampText=Loc.T("АКТ ЭКСТРЕННОЙ ОТКАЧКИ","EMERGENCY REVIVAL");
        var stampFont=GameFonts.Get(Typeface.InterfaceBold,13);
        float stW=GameFonts.Measure(stampFont,stampText,13).X+18;
        Rlgl.PushMatrix();
        Rlgl.Translatef(824,205,0);
        Rlgl.Rotatef(-6f,0,0,1);
        Raylib.DrawRectangleLinesEx(new(-stW/2,-14,stW,28),2f,red);
        GameFonts.Draw(stampFont,stampText,new(-stW/2+9,-7),13,red,shadowed:false);
        Rlgl.PopMatrix();

        Raylib.DrawLine(368,242,908,242,LoreAndMenusUI.ThemeSepiaRule);
        GameFonts.Draw(GameFonts.Get(Typeface.InterfaceBold,22),Loc.T("Тебя срубил: ","Knocked out by: ")+Loc.Tr(_lastDamageLabel),new(368,256),22,ink,shadowed:false);
        int wy=292;
        foreach(var line in LoreAndMenusUI.Wrap(Loc.KnockoutRevival,540,17))
        {
            GameFonts.Draw(GameFonts.Get(Typeface.Interface,17),line,new(368,wy),17,muted,shadowed:false);
            wy+=22;
        }
        string[] buttons=[Loc.T("[R / Enter] Очнуться у Вероники","[R / Enter] Wake up at Veronika’s"),
            Loc.T("[L] Загрузить сохранение","[L] Load Last Save"),Loc.T("[M] В главное меню","[M] Main Menu")];
        var mouse=(Raylib.GetMousePosition()-PanelOffset)/PanelScale;
        for(int i=0;i<buttons.Length;i++)
        {
            if(i==1 && _knockoutSave<0) continue;
            var rect=KnockoutButton(i);
            bool hover=Raylib.CheckCollisionPointRec(mouse,rect);
            bool primary=i==0;
            if(primary || hover)
            {
                Raylib.DrawRectangleRounded(new(rect.X+2,rect.Y+3,rect.Width,rect.Height),.08f,8,new(40,16,12,68));
                Raylib.DrawRectangleRounded(rect,.08f,8,primary ? red : new Color(68,28,24,235));
                Raylib.DrawRectangleRoundedLinesEx(rect,.08f,8,1.6f,new Color(196,154,84,220));
                GameFonts.Draw(GameFonts.Get(Typeface.InterfaceBold,20),buttons[i],new(rect.X+20,rect.Y+14),20,new Color(252,242,222,255),shadowed:false);
            }
            else
            {
                Raylib.DrawRectangleRounded(rect,.08f,8,new Color(235,222,196,195));
                Raylib.DrawRectangleRoundedLinesEx(rect,.08f,8,1.4f,LoreAndMenusUI.ThemeSepiaRule);
                GameFonts.Draw(GameFonts.Get(Typeface.InterfaceBold,20),buttons[i],new(rect.X+20,rect.Y+14),20,ink,shadowed:false);
            }
        }
        Raylib.EndMode2D();
    }

    private static void BeginHealLesson()
    {
        if(Floor.Onboarding!=OnboardingStep.StunAndHeal) return;
        Floor.Onboarding=OnboardingStep.HealAtCoffee;
        Floor.Coach();
    }
    internal static void TrainingScratch(Vector3 source)
    {
        if(!NeedsHealing) ReceiveDamage(18,"Вайбкодер (искра ноута)",source);
        else BeginHealLesson();
    }
    private static void HealFeedback(int stamina)
    {
        _energy=Math.Min(100,_energy+stamina);
        HealFlashTimer=.5f;
        AudioMusicSystem.PlaySip();
    }
    internal static string Heal(int hp,int stamina,string label)
    {
        if(guestInteraction)
        {
            guestHp=Math.Min(100,guestHp+hp);
            Net.Send(new("healed",Amount:stamina),actorSlot);
        }
        else { _playerHp=Math.Min(100,_playerHp+hp); HealFeedback(stamina); }
        if(Floor.Onboarding==OnboardingStep.HealAtCoffee)
        {
            Floor.Onboarding=OnboardingStep.CaptureFirstCoder;
            Floor.Coach();
        }
        string toast=$"+{hp} HP   {label}";
        Floor.Announce(toast,2);
        return toast;
    }
    internal static void RefillCoffeeCup()
    {
        if(guestInteraction) GuestCups=Math.Min(2,GuestCups+1);
        else Floor.PocketCoffeeCups=Math.Min(2,Floor.PocketCoffeeCups+1);
    }
    private static void DrinkPocketCoffee()
    {
        if(Net.Guest) { Net.Send(new("drink")); return; }
        ref int cups=ref (guestInteraction ? ref GuestCups : ref Floor.PocketCoffeeCups);
        if(!NeedsHealing || cups==0) return;
        cups--;
        Heal(30,25,"СТАКАН КОФЕ");
    }

    private const int XpPerLevel = 1073;
    private static int _xp = 412;
    private static SkyscraperFloor1World _menuFloor = null!;
    private static float _menuTime;

    private static readonly IntroComic comic = new();
    private static bool startAfterComic;
    private static float comicFade;
    private static int? fixtureSeed;
    private static float _energy = 100;
    private static Font _serif;
    private static float _weaponFireCooldown = 0f;
    private static string _hudStatusBanner = "";
    private static float _hudStatusTimer;
    private static bool _foregroundBanner;

    public static int Main(string[] args)
    {
        bool HasFlag(string name) => args.Contains(name, StringComparer.OrdinalIgnoreCase);
        bool autoTest = HasFlag("--autotest") || HasFlag("-autotest");
        bool coopSmoke = HasFlag("--coop-smoke");
        bool polishTest=HasFlag("--polish-test");
        bool gripTest = HasFlag("--grip-test");
        bool captureComic = HasFlag("--capture-comic");
        bool recordCutscene = HasFlag("--record-cutscene");
        bool recordPlaythrough = HasFlag("--record-playthrough");
        DevMode = HasFlag("--dev");
        bool silent = captureComic || recordCutscene || recordPlaythrough || args.Any(arg => arg.StartsWith('-') &&
            (arg.Contains("test", StringComparison.OrdinalIgnoreCase) ||
             arg.Contains("smoke", StringComparison.OrdinalIgnoreCase)));
        bool localPair=coopSmoke && !HasFlag("--host") && !HasFlag("--join");
        fixtureSeed=autoTest || coopSmoke || gripTest || polishTest || recordCutscene || recordPlaythrough ? 6 : null;
        if (HasFlag("--regenerate-music"))
        {
            AudioMusicSystem.EnsureMusicFiles(Path.Combine(Environment.CurrentDirectory, "assets", "sounds"), force: true);
            return 0;
        }

        bool interactive=!fixtureSeed.HasValue && !captureComic;
        CharacterAppearance.SuppressDiskPersistence = !interactive;
        CharacterAppearance.LoadFromDisk();
        string displayPath=interactive ? Path.Combine(MainMenuAndPauseUI.SavesDirectory,"display_settings.json")
            : Path.Combine(Path.GetTempPath(),"vibecoder-display-"+Guid.NewGuid().ToString("N")+".json");
        MainMenuAndPauseUI.LoadDisplay(displayPath,interactive);
        if(interactive) Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint | ConfigFlags.HiddenWindow);
        Raylib.InitWindow(ScreenWidth, ScreenHeight, Loc.WindowTitle);
        if(interactive) MainMenuAndPauseUI.Display.Apply();
        Raylib.SetTargetFPS(60);
        Raylib.SetExitKey(KeyboardKey.Null);

        GameFonts.Load();
        if (captureComic)
        {
            try
            {
                AudioMusicSystem.Initialize(silent: true);
                CaptureComic();
            }
            finally
            {
                comic.Dispose();
                AudioMusicSystem.Shutdown();
                GameFonts.Unload();
                Raylib.CloseWindow();
            }
            return 0;
        }
        _uiFont = GameFonts.Get(Typeface.Interface, 18);
        _serif = GameFonts.Get(Typeface.Serif, 24);
        MainMenuAndPauseUI.LoadArt(_serif);
        VisualEngine3D.Load();
        CombatSprites.Load();

        AudioMusicSystem.Initialize(silent);
        if(silent && Raylib.IsAudioDeviceReady()) Raylib.SetMasterVolume(0f);
        _world = new SkyscraperFloor1World(fixtureSeed);
        _menuFloor = new SkyscraperFloor1World { Partner = new PartnerAvatar() };
        _menuFloor.Partner.Move(new(new(-26.2f,1.72f,5.5f),1.26f,0,WeaponType.MicroUzi));
        _weapons = new WeaponAndCombatView { Inventory=Floor, DrawBlocked=BlockWeaponDraw };

        LoadSlotIcons();
        shopBackdrop=Raylib.LoadTexture(Path.Combine(AppContext.BaseDirectory,"assets","ui","shop_briefcase_bg.png"));
        Raylib.SetTextureFilter(shopBackdrop,TextureFilter.Bilinear);
        string kcPath=Path.Combine(AppContext.BaseDirectory,"assets","ui","keycard_sb_icon.png");
        if(File.Exists(kcPath)) { keycardIconTex=Raylib.LoadTexture(kcPath); Raylib.SetTextureFilter(keycardIconTex,TextureFilter.Bilinear); }
        string koPath=Path.Combine(AppContext.BaseDirectory,"assets","ui","knockout_clipboard_bg.png");
        if(File.Exists(koPath)) { knockoutClipboardTex=Raylib.LoadTexture(koPath); Raylib.SetTextureFilter(knockoutClipboardTex,TextureFilter.Bilinear); }
        preparedFloor=_world;
        WireUpSystems();
        ConfigureCoop();
        if(interactive)
        {
            // Warm the first-person hands, HUD font atlases and postprocessing before the start button is live.
            MainMenuAndPauseUI.IsInMainMenu=false;
            DrawFrame();
            MainMenuAndPauseUI.IsInMainMenu=true;
            DrawFrame();
            Raylib.ClearWindowState(ConfigFlags.HiddenWindow);
        }
        if(localPair && coopSmoke) CheckFloorHunt();
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
            if(localPair) CheckThreePlayerLobby();
            using var guest=localPair ? System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                Path.Combine(AppContext.BaseDirectory,OperatingSystem.IsWindows() ? "HowToVibeCoder.exe" : "HowToVibeCoder"),"--coop-smoke --join 127.0.0.1") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true }) : null;
            var guestLog=guest?.StandardOutput.ReadToEndAsync();
            try
            {
                if(join<0) { Floor.OwnedWeaponsMask=FloorSystems.ClassicMask; _playerCash=450; }
                RunCoopSmoke(join>=0);
                CheckCoopEscort(join>=0);
                CheckCoopProgression(join>=0);
                CheckCoopCarryHarness(join>=0);
                CheckCoopSkinAward(join>=0);
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
        else if (recordCutscene) RecordCutsceneVideo();
        else if (recordPlaythrough) RecordFullPlaythrough();
        else if (autoTest || gripTest || polishTest) RunAutomatedScreenshotSuite(displayPath,polishTest);
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

        if(silent && Raylib.IsAudioDeviceReady() && Raylib.GetMasterVolume()!=0f)
            throw new InvalidDataException("Automation master volume must stay at zero");
        if(!interactive)
        {
            if(Loc.Current!=GameLang.Ru) throw new InvalidDataException("Test left English active");
            File.Delete(displayPath);
        }
        comic.Dispose();
        Net.Dispose();
        LoreAndMenusUI.UnloadArchive();
        Raylib.UnloadTexture(shopBackdrop);
        foreach(var icon in _slotIcons) if(icon.Id!=0) Raylib.UnloadTexture(icon);
        CombatSprites.Unload();
        VisualEngine3D.Unload();
        MainMenuAndPauseUI.UnloadArt();
        GameFonts.Unload();
        AudioMusicSystem.Shutdown();
        Raylib.CloseWindow();
        return 0;
    }


    private static void EndSceneFrame()
    {
        if (comicFade > 0)
        {
            float u = Math.Clamp(comicFade / .68f, 0f, 1f);
            float ease = u * u * (3f - 2f * u);
            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(9, 8, 11, (int)(255 * ease)));
        }
        Raylib.EndDrawing();
    }

    private static void CaptureComic()
    {
        string directory = Path.Combine(Environment.CurrentDirectory, "screenshots");
        Directory.CreateDirectory(directory);
        void Capture(string name)
        {
            Raylib.BeginDrawing();
            comic.Draw();
            Raylib.EndDrawing();
            var image = Raylib.LoadImageFromScreen();
            try
            {
                if (image.Width != 1280 || image.Height != 720 || !Raylib.ExportImage(image, Path.Combine(directory, name)))
                    throw new IOException("Comic capture: " + name);
            }
            finally { Raylib.UnloadImage(image); }
        }
        Loc.Set(GameLang.Ru, false);
        comic.Open(withTitleCurtain: true);
        for (int frame = 0; frame < 72; frame++) comic.Update(1f / 60, false, false);
        Capture("39a_comic_loading_curtain.png");
        comic.Open(withTitleCurtain: false);
        for (int i = 1; i <= 8; i++)
        {
            // Sample the live timeline after both staggered bursts have settled.
            for (int frame = 0; frame < 104; frame++) comic.Update(1f / 60, false, false);
            switch (i)
            {
                case 1: Capture("39_comic_splash1_start.png"); break;
                case 4: Capture("40_comic_spread1_boom_snap.png"); break;
                case 7: Capture("41_comic_spread2_jackpot.png"); break;
                case 8: Capture("42_comic_splash8_entropic_en.png"); break;
            }
            if (i == 7) Loc.Set(GameLang.En, false);
            comic.Update(0, true, false);
            if (i is 1 or 4 or 7)
                for (int frame = 0; frame < 19; frame++) comic.Update(1f / 60, false, false);
        }
        if (comic.Active) throw new InvalidDataException("Comic failed to finish");
        Loc.Set(GameLang.Ru, false);
    }

    private static void WireUpSystems()
    {
        LoreAndMenusUI.ActiveWeaponView = _weapons;
        LoreAndMenusUI.GetPlayerMoney = () => _playerCash;
        LoreAndMenusUI.TrySpendPlayerMoney = TrySpendCash;
        LoreAndMenusUI.IsBossRevealed = () => _world.BossRevealed || _world.ElevatorBoss.IsDefeated;
        LoreAndMenusUI.OnGrantMoneyRequested = amount =>
        {
            if(Net.Guest) { Net.Send(new("bonus")); return; }
            _playerCash += amount;
            SetHudBanner($"ТЕСТ-БОНУС АВТОРИТЕТА: +${amount} НА КАРМАН! Баланс: ${_playerCash}");
        };
        LoreAndMenusUI.OnUnlockAllWeaponsRequested = () =>
        {
            Floor.OwnedWeaponsMask |= FloorSystems.ClassicMask;
            _weapons.UnlockedSolderingIron = true;
            _weapons.UnlockedMicroUzi = true;
            _weapons.UnlockedSawedOff = true;
            _weapons.UnlockedChatGun = true;
            SetHudBanner("Весь арсенал открыт");
        };
        LoreAndMenusUI.OnResetFloorRequested = () => { if(!Net.Guest) ResetNewGameFloor(); };
        LoreAndMenusUI.RequestCasePayment = () => {
            if(!Net.Guest) return false;
            if(!pendingCase) { pendingCase=true; Net.Send(new("case")); }
            return true;
        };

        MainMenuAndPauseUI.OnIntroComicRequested = start =>
        {
            startAfterComic = start;
            comic.Open(withTitleCurtain: true);
        };
        MainMenuAndPauseUI.OnStartNewGameRequested = () =>
        {
            ResetNewGameFloor(preparedFloor);
            preparedFloor=null;
            if(MainMenuAndPauseUI.InteractiveDisplay) hostOnNextUpdate=true;
            else StartHosting(newShift:true);
        };
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
            _knockoutActive=false;
            Net.Dispose();
            preparedFloor=new SkyscraperFloor1World(fixtureSeed);
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
                CurrentWeaponSlot = Math.Clamp((int)_weapons.CurrentWeapon-1,0,3),
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
            ResetNewGameFloor(greet:false);
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
            if(data.ElevatorBossDefeated) _weapons.UnlockedChatGun=true;
            _playerPos = new Vector3(data.PlayerPosX, Math.Max(1.2f, data.PlayerPosY), data.PlayerPosZ);
        };
    }

    private static bool hostOnNextUpdate;
    private static SkyscraperFloor1World? preparedFloor;
    private static void ResetNewGameFloor(SkyscraperFloor1World? ready=null,bool greet=true)
    {
        _knockoutActive=false;
        floorPanel=FloorPanel.None; seenReport=0;
        bot=awaitingSpawn=pendingCase=false; Array.Clear(escorts); Array.Clear(downUntil); Array.Fill(health,100);
        _world = ready ?? new SkyscraperFloor1World(fixtureSeed);
        if(!Net.Guest) _world.AdaptPlayers(Net.ActivePlayerCount);
        AudioMusicSystem.VoiceTimer = 0;
        _playerPos = _world.ResolvePlayerCollision(LobbyEntrance);
        _isCrouching = false;
        _playerCash = 0;
        _playerHp = 100;
        _lastDamageTimer=HealFlashTimer=0;
        _weapons = new WeaponAndCombatView { Inventory=Floor, DrawBlocked=BlockWeaponDraw };
        LoreAndMenusUI.ActiveWeaponView = _weapons;
        _xp = 412;
        _energy = 100;
        _weaponFireCooldown = _walkBobPhase = _playerVelY = _jumpBufferTimer = _pitchSpring = _yawSpring = 0;
        _dashTime = _dashCooldown = _regenDelay = _fovKick = _recoilPitch = _recoilYaw = _shake = _playerHitAnimTimer = 0;
        StyleComboSystem.Reset();
        ResetCombatFeedback();
        BossIntroCutscene.Reset();
        _repairMinigame.Close();
        _eyeHeight=1.72f; _coyoteTimer=.12f; _isGrounded=true; _crosshairGap=4; _weapons.HitJolt=0;
        _sprinting=_toggleCrouch=_exhausted=false;
        LookAtTarget(_world.HrTarotBoothPos+new Vector3(-.45f,1.26f,0));
        SetHudBanner("",0);
        if(greet) Floor.Coach();
    }

    private static bool BlockWeaponDraw()
    {
        if(_world.CarriedCoder==null || Floor.Has(Sidegrade.CarryHarness)) return false;
        SetHudBanner("Отнеси вайбкодера или купи Плечевой ремень",2.4f,foreground:true);
        return true;
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

    private static void DropBait()
    {
        if(Net.Guest) Net.Send(new("drop-bait",LocalPose));
        else SetHudBanner(_world.DropFloorBait(BuildPlayerCamera()),foreground:true);
    }
    internal static void SetHudBanner(string text, float duration = 4.2f,bool foreground=false)
    {
        _foregroundBanner=foreground;
        _hudStatusBanner = text;
        _hudStatusTimer = duration;
    }

    private static Camera3D BuildPlayerCamera()
    {
        float bob = _isGrounded ? .022f*MathF.Sin(_walkBobPhase*2)*_weapons.WalkWeight : 0;
        Vector3 eyePos = _playerPos + Vector3.UnitY*(bob-_weapons.LandingCompression);
        float pitch = _playerPitch+_recoilPitch+.013f*_weapons.ClosingImpulse+_shake*MathF.Sin(_shake*720);
        float yaw = _playerYaw+_recoilYaw+_shake*MathF.Cos(_shake*860);
        float hit=PlayerHitWeight, elapsed=.22f-_playerHitAnimTimer;
        float roll=-MathF.CopySign(5.5f*MathF.PI/180,_playerHitDir.X)*hit;
        pitch+=hit*(-.082f+.023f*MathF.Sin(elapsed*115));
        yaw+=hit*.027f*MathF.Sin(elapsed*153);
        Vector3 forward = new(MathF.Sin(yaw)*MathF.Cos(pitch),MathF.Sin(pitch),MathF.Cos(yaw)*MathF.Cos(pitch));

        float fall=_knockoutActive ? Math.Clamp(_knockoutTime/.55f,0,1) : 0;
        eyePos.Y+=(.42f-eyePos.Y)*fall;
        return new Camera3D
        {
            Position = eyePos,
            Target = eyePos + forward,
            Up = Vector3.Transform(Vector3.UnitY,Quaternion.CreateFromAxisAngle(forward,roll+.419f*fall+.015f*_weapons.ClosingImpulse)),
            FovY = Math.Clamp(LoreAndMenusUI.CameraFov, 60f, 100f)+_fovKick,
            Projection = CameraProjection.Perspective
        };
    }

    private static bool BossFightMusic => _world.BossRevealed ||
        _world.ActiveCoders.Exists(c => c.IsMiniBoss && c.State != CoderAIState.Subdued);

    private static void UpdateFrame(float dt, ref bool shouldExit)
    {
        if (comic.Active)
        {
            bool next = Raylib.IsKeyPressed(KeyboardKey.Space) || Raylib.IsKeyPressed(KeyboardKey.Enter)
                || Raylib.IsKeyPressed(KeyboardKey.KpEnter) || Raylib.IsMouseButtonPressed(MouseButton.Left);
            comic.Update(dt, next, Raylib.IsKeyPressed(KeyboardKey.Escape));
            AudioMusicSystem.UpdateMusic(false, dt, isMainMenu: true, comic: true);
            if (!comic.Active)
            {
                if (startAfterComic) MainMenuAndPauseUI.StartNewOrResumeFloor();
                comicFade = .68f;
            }
            return;
        }
        comicFade = Math.Max(0, comicFade - dt);
        UpdateGameplay(dt, ref shouldExit);
        bool slotVisible=_world.Slot.Active && !MainMenuAndPauseUI.IsInMainMenu;
        AudioMusicSystem.UpdateMusic(BossFightMusic,dt,MainMenuAndPauseUI.IsInMainMenu,slotVisible,slotVisible && !_world.Slot.Reveal);
    }

    private static void UpdateGameplay(float dt, ref bool shouldExit)
    {
        if(hostOnNextUpdate)
        {
            hostOnNextUpdate=false;
            StartHosting(newShift:true);
        }
        Net.Pump(LocalPose,CaptureForSlot);

        if(_playerHp==0 && !_knockoutActive && !MainMenuAndPauseUI.IsInMainMenu) BeginKnockout();
        if(_knockoutActive)
        {
            if(Net.Host && Net.Ready) TickFloor(dt);
            else TickHitReaction(dt);
            UpdateKnockout(dt);
            return;
        }

        if(_world.Slot.Active && !MainMenuAndPauseUI.IsInMainMenu) { UpdateSlot(dt); return; }
        if(_repairMinigame.IsActive && !MainMenuAndPauseUI.IsInMainMenu)
        {
            if(Raylib.IsCursorHidden()) Raylib.EnableCursor();
            if(Net.Ready) TickFloor(dt);
            else _world.AdvanceLighting(dt);
            if(_repairMinigame.Update(dt))
            {
                if(!Raylib.IsCursorHidden()) Raylib.DisableCursor();
                if(Net.Guest) Net.Send(new("interact", LocalPose));
                else if(_world.TrySummonElevatorBoss(out string repairMsg))
                    SetHudBanner(repairMsg, 2.4f, foreground: true);
            }
            return;
        }

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
        bool startingFromMenu=MainMenuAndPauseUI.IsInMainMenu;
        MainMenuAndPauseUI.Update(dt, ref shouldExit);
        if(startingFromMenu && !MainMenuAndPauseUI.IsInMainMenu) return;
        FlushFloorSave();
        if (MainMenuAndPauseUI.IsInMainMenu)
        {
            _menuTime += dt;
            _menuFloor.AdvanceLighting(dt);
        }
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
        TickCombatFeedback(dt);
        StyleComboSystem.Update(dt);
        float styleShake = StyleComboSystem.ConsumeShake();
        if (styleShake > _shake) _shake = styleShake;
        if (_combatHitstopTimer > 0f)
        {
            _combatHitstopTimer = MathF.Max(0f, _combatHitstopTimer - dt);
            if (MainMenuAndPauseUI.InteractiveDisplay && !Net.Ready)
            {
                _shake *= MathF.Exp(-8f * dt);
                return;
            }
        }
        if (MainMenuAndPauseUI.InteractiveDisplay && StyleComboSystem.ConsumeHitstop(dt)) return;
        _world.UpdateLooseClutter(dt);
        _world.UpdateIntercom(dt);

        BossIntroCutscene.SyncState(_world);
        if (BossIntroCutscene.IsCameraOverrideActive(_world))
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Space))
            {
                BossIntroCutscene.Skip();
                if (!Net.Ready) FinishElevatorIntro();
                else TickFloor(dt);
                return;
            }
            float cinemaDt = MainMenuAndPauseUI.InteractiveDisplay && !Net.Ready
                && _world.ElevatorBoss.State == ElevatorBossState.IntroSpeech
                ? dt * 0.165f
                : dt;
            TickFloor(cinemaDt);
            if (MainMenuAndPauseUI.InteractiveDisplay && !Net.Ready && BossIntroCutscene.ShouldRelocatePlayerAfterKnockback(_world))
            {
                _playerPos = BossIntroCutscene.KnockedBackStandPosition(_world);
                LookAtTarget(_world.ElevatorBoss.Position + Vector3.UnitY * 1.68f);
            }
            BossIntroCutscene.SyncState(_world);
            return;
        }

        // Quick money hotkey [F9]
        if (Raylib.IsKeyPressed(KeyboardKey.F9))
        {
            if(Net.Guest) Net.Send(new("bonus"));
            else GrantCash(2000, "Чит-бонус Авторитета [F9]");
            AudioMusicSystem.PlayTarotCash();
        }

        for(int i=0;i<_weapons.HotbarSlotCount;i++)
            if(Raylib.IsKeyPressed(KeyboardKey.One+i)) _weapons.SelectOwnedSlot(i);
        _weapons.ScrollHotbar(Math.Sign(Raylib.GetMouseWheelMove()));
        if(_weapons.UnlockedChatGun && Raylib.IsKeyPressed(KeyboardKey.T))
            _weapons.Equip(_weapons.CurrentWeapon==WeaponType.ChatGun ? WeaponType.BareHands : WeaponType.ChatGun);
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
        bool pocket=Floor.Has(Sidegrade.PortableCo2) && Raylib.IsKeyPressed(KeyboardKey.Q);
        if(pocket)
        {
            if(Net.Guest) Net.Send(new("pocket-co2",LocalPose));
            else SetHudBanner(Floor.UsePocket(_playerPos));
        }
        MovePlayer(dt,moveDir,forwardXZ,Raylib.IsKeyPressed(KeyboardKey.Space),
            Raylib.IsKeyDown(KeyboardKey.LeftShift),Raylib.IsKeyPressed(KeyboardKey.LeftAlt) || !pocket && Raylib.IsKeyPressed(KeyboardKey.Q));

        // [V] Cheeky Middle-Finger Emote — works on any Vibe Coder, HR Veronika, Boss, or Co-op Teammate!
        if (Raylib.IsKeyPressed(KeyboardKey.V))
            TriggerFlipOffEmote();

        // [X] Walkie-Talkie Radio Hint from Shleif (handed over by Veronika when floor wings open)
        if ((Floor.HasRadio || Floor.WingsUnlocked) && Raylib.IsKeyPressed(KeyboardKey.X))
        {
            AudioMusicSystem.PlayActionClack(1.42f);
            SetHudBanner(Floor.ShleifRadioHint(_world), 5.6f, foreground: true);
        }

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
            if(Net.Guest)
            {
                FloorPanel station=_world.PanelAt(camera,_weapons.CurrentWeapon);
                if(station!=FloorPanel.None) { OpenFloorPanel(station); return; }
            }
            if(_world.FocusPrompt(camera)=="Поймать Джаба-жабу")
                _weapons.Equip(WeaponType.BareHands);
            if(MainMenuAndPauseUI.InteractiveDisplay && _world.BrokenElevator && _world.HasElevatorKey && Floor.HasWeapon(WeaponType.SolderingIron) &&
                _world.FocusPrompt(camera)==_world.ElevatorPrompt())
            {
                _repairMinigame.Open();
                if(Raylib.IsCursorHidden()) Raylib.EnableCursor();
                return;
            }
            if(Net.Guest) Net.Send(new("interact",LocalPose));
            else
            {
                string result=_world.Interact(camera,TrySpendCash,GrantCash,_weapons.CurrentWeapon,OpenFloorPanel);
                HolsterForEscort();
                if(floorPanel!=FloorPanel.None) return;
                SetHudBanner(result,2.4f,foreground:true);
            }
        }

        UpdateTrigger(dt, Raylib.IsMouseButtonDown(MouseButton.Left),Raylib.IsMouseButtonPressed(MouseButton.Left),
            Raylib.IsKeyPressed(KeyboardKey.R),isMoving);
        TickFloor(dt);

    }

    private static int _flipOffLineIdx;
    private static void TriggerFlipOffEmote()
    {
        _weapons.TriggerFlipOff();
        AudioMusicSystem.PlayHandGrab();
        if (Net.Ready) Net.Send(new("flipoff", LocalPose));

        var cam = BuildPlayerCamera();
        Vector3 fwd = Vector3.Normalize(cam.Target - cam.Position);

        // 1) Co-op teammate or bot partner in front?
        foreach (var ally in _world.Teammates)
        {
            Vector3 toAlly = (ally.Pose.Eye - Vector3.UnitY * .25f) - cam.Position;
            float d = toAlly.Length();
            if (d > 0.1f && d < 8.5f && Vector3.Dot(fwd, toAlly / d) > 0.72f)
            {
                ally.FlipOffTimer = 1.4f; // Teammate flips right back in solidarity!
                return;
            }
        }

        // 2) HR Veronika at her Tarot booth?
        Vector3 toHr = (_world.HrTarotBoothPos + Vector3.UnitY * 1.45f) - cam.Position;
        float dHr = toHr.Length();
        if (dHr > 0.1f && dHr < 6.5f && Vector3.Dot(fwd, toHr / dHr) > 0.76f)
        {
            AudioMusicSystem.Speak("HR ВЕРОНИКА", "Жест зафиксирован в табеле. Минус премия.", SpeakerVoiceType.VeronikaTarot);
            return;
        }

        // 3) Boss at the elevator?
        if (_world.ElevatorBoss.IsActive)
        {
            Vector3 toBoss = (_world.ElevatorBoss.Position + Vector3.UnitY * 1.35f) - cam.Position;
            float dBoss = toBoss.Length();
            if (dBoss > 0.1f && dBoss < 10.5f && Vector3.Dot(fwd, toBoss / dBoss) > 0.75f)
            {
                AudioMusicSystem.Speak("АГЕНТ v0.1", "Жест распознан. Вероятность увольнения: 99,9%.", SpeakerVoiceType.RoboticAgent);
                return;
            }
        }

        // 4) Any Vibe Coder in front? Trigger outraged speech bubble & turn to face player!
        VibeCoderEntity? bestCoder = null;
        float bestScore = 0.76f;
        foreach (var c in _world.ActiveCoders)
        {
            if (c.State == CoderAIState.Subdued) continue;
            Vector3 toC = (c.Position + Vector3.UnitY * 1.35f) - cam.Position;
            float dC = toC.Length();
            if (dC < 0.1f || dC > 8.0f) continue;
            float dot = Vector3.Dot(fwd, toC / dC);
            if (dot > bestScore) { bestScore = dot; bestCoder = c; }
        }
        if (bestCoder != null)
        {
            string[] comebacks =
            [
                "ЭЙ! ЭТО ТОКСИЧНЫЙ КУЛЬТУРНЫЙ ФИТ!",
                "Я ПИШУ В HR-СЛАК ПРЯМО СЕЙЧАС!",
                "КЕНСЕЛЮ ТЕБЯ В ЛИНКЕДИНЕ!",
                "МИНУС ВАЙБ! НЕЭКОЛОГИЧНО!"
            ];
            bestCoder.SpeechBubble = comebacks[_flipOffLineIdx++ % comebacks.Length];
            bestCoder.SpeechTimer = 3.2f;
            bestCoder.FlippedOffTimer = 5.5f;
            bestCoder.FacingYaw = MathF.Atan2(cam.Position.X - bestCoder.Position.X, cam.Position.Z - bestCoder.Position.Z);
        }
    }

    private static void MovePlayer(float dt, Vector3 move, Vector3 facing, bool jump, bool sprint, bool dash)
    {
        if(PlayerStunned)
        {
            move=Vector3.Zero; jump=sprint=dash=false; _dashTime=_jumpBufferTimer=0;
        }
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
        _sprinting=(_world.CarriedCoder==null || Floor.Has(Sidegrade.CarryHarness)) && moving && sprint && !_isCrouching && !_exhausted && _energy>0 && _dashTime<=0;
        if (_sprinting)
        {
            _energy=Math.Max(0,_energy-(_world.Systems.Coffee>0 ? 6 : 17)*dt); _regenDelay=.65f;
            if (_energy<=0) _exhausted=true;
        }
        else if (_dashTime<=0 && (_regenDelay-=dt)<=0) _energy=Math.Min(100,_energy+20*dt);
        float speed = _isCrouching ? 4.25f : 6.6f*(_sprinting ? 1.48f : 1);
        if(_world.CarriedCoder!=null) speed*=Floor.Has(Sidegrade.CarryHarness) ? 1.12f : .78f;
        if(_world.Systems.Coffee>0) speed*=1.35f;
        float burst = Math.Min(dt,_dashTime);
        Vector3 travel = move*speed*(dt-burst) + _dashDirection*18*burst;
        _dashTime-=burst;
        _fovKick+=((_dashTime>0 ? 8 : _sprinting ? 4 : 0)-_fovKick)*(1-MathF.Exp(-10*dt));
        if (moving && _isGrounded) _walkBobPhase+=dt*speed*1.5f;

        // Small swept steps keep a diagonal dash sliding along narrow partitions.
        Vector3 beforeMove=_playerPos;
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
        _world.CheckGateContact(beforeMove+travel,_playerPos,dt);
        Vector3 swept=_playerPos-beforeMove;
        swept.Y=0;
        if((_sprinting || burst>0) && _isGrounded && swept.LengthSquared()>.00001f)
            _world.Scatter(_playerPos-Vector3.UnitY*_eyeHeight,Vector3.Normalize(swept),1.6f,2.8f);
        _weapons.SetStance(_walkBobPhase,_isCrouching);
        _weapons.UpdateMotion(dt,_sprinting,_isGrounded,_playerVelY,landingImpact,burst>0,
            Vector3.Dot(move,new Vector3(-facing.Z,0,facing.X)));
    }

    private static bool PlayerStunned => (Net.Guest ? (Net.LocalSlot==2 ? Floor.SecondStun : Floor.GuestStun) : Floor.HostStun)>0;

    private static void UpdateTrigger(float dt,bool held,bool pressed,bool reload,bool moving)
    {
        _weapons.Drum=_world.Systems.Has(Sidegrade.Drum);
        _weapons.InsulatedIron=_world.Systems.Has(Sidegrade.Tape);
        _weapons.CoolIron(dt);
        var gun=_weapons.CurrentWeapon;
        bool automatic=gun==WeaponType.MicroUzi;
        bool trigger=!PlayerStunned && (automatic ? held : pressed);
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
        Camera3D cam=BuildPlayerCamera();
        _weapons.SetStance(_walkBobPhase,_isCrouching);
        _weapons.UpdateWeapons(dt,cam,moving && _isGrounded,held);
        if(reload || _weapons.Ammo==0) _weapons.BeginReload();
        if(gun==WeaponType.SolderingIron && _weapons.IronLockout>0) trigger=false;
        if(trigger && _weaponFireCooldown<=.00001f && !_weapons.Reloading)
        {
            float interval=gun switch {
                WeaponType.MicroUzi=>.10f, WeaponType.Belt=>_world.Systems.Coffee>0 ? .19f : .30f, WeaponType.SolderingIron=>_world.Systems.Has(Sidegrade.Tape) ? .26f : .34f,
                WeaponType.SawedOff=>.42f, WeaponType.CaptureNet or WeaponType.BareHands when Floor.Has(Sidegrade.CarbonNda)=>.14f, _=>.28f };
            _weaponFireCooldown=interval+(automatic && !pressed ? _weaponFireCooldown : 0);
            _weapons.UseRound();
            if(gun==WeaponType.SolderingIron) _weapons.HeatIron();
            float steady=_isCrouching ? .65f : 1;
            bool capture=gun is WeaponType.BareHands or WeaponType.CaptureNet;
            bool melee=capture || gun is WeaponType.Belt or WeaponType.SolderingIron;
            Vector3 aim=Vector3.Normalize(cam.Target-cam.Position);
            if (melee && !capture)
            {
                Vector3 flat=new(aim.X,0,aim.Z);
                _playerPos=_world.ResolvePlayerCollision(_playerPos+Vector3.Normalize(flat)*.24f,.40f,_playerPos.Y-_eyeHeight);
                cam=BuildPlayerCamera();
            }
            else if(!capture)
            {
                Vector3 right=Vector3.Normalize(Vector3.Cross(aim,Vector3.UnitY));
                float cone=(gun==WeaponType.SawedOff ? .033f : .009f)*steady*(1+_recoilPitch*12);
                aim=Vector3.Normalize(aim+cone*(right*(2*ShotRandom.NextSingle()-1)+Vector3.Cross(right,aim)*(2*ShotRandom.NextSingle()-1)));
            }
            PlayCurrentWeaponAudio(gun);
            AudioMusicSystem.TryWeaponBark(gun);
            Vector3 disturbance=cam.Position-Vector3.UnitY*(_eyeHeight*.73f);
            _world.Scatter(disturbance+(melee ? aim*.85f : Vector3.Zero),aim,melee ? 2.3f : 3.8f,melee ? 3.9f : 5.4f);
            if(!melee)
            {
                float distance=_world.PelletDistance(cam.Position,aim,14);
                if(distance<14) _world.Scatter(cam.Position+aim*distance,aim,2.1f,4.2f);
            }
            if (Net.Ready)
                Net.Send(new("fire",new PlayerPose(cam.Position,MathF.Atan2(aim.X,aim.Z),MathF.Asin(aim.Y),gun,Appearance:CharacterAppearance.Current.Pack())));
            if (Net.Guest)
            {
                float range=gun switch { WeaponType.SawedOff=>12.5f, WeaponType.ChatGun=>28, WeaponType.MicroUzi=>26,
                    WeaponType.BareHands=>2.35f,WeaponType.CaptureNet=>3.6f,WeaponType.Belt=>3.8f,_=>5.2f };
                _weapons.BallisticWorld=_world;
                float distance=_world.PelletDistance(cam.Position,aim,range);
                _weapons.TriggerWeaponAttack(cam,distance<range ? cam.Position+aim*distance : null,false,shotDirection:aim);
            }
            else if (_world.ProcessPlayerAttack(cam,_weapons,GrantCash,out var msg,aim) && msg.Length>0) SetHudBanner(msg,1.3f,foreground:capture);
            HolsterForEscort();
            float kick=gun==WeaponType.SawedOff ? .077f : gun==WeaponType.CaptureNet ? .022f : melee ? .009f : .023f;
            // Impulse the spring velocity. The camera starts at the firing sightline.
            float yawKick=(2*ShotRandom.NextSingle()-1)*kick*steady;
            if (melee) { _recoilPitch+=kick*steady; _recoilYaw+=yawKick*.38f; }
            else { _pitchSpring+=kick*steady*30; _yawSpring+=yawKick*8; }
            if (melee && _weapons.HitmarkerTimer>=.22f && _weapons.HitmarkerIsCrit) { _shake=.014f; TriggerHitstop(0.028f); }
            else if (gun==WeaponType.SawedOff && _weapons.HitmarkerTimer>=.21f) TriggerHitstop(0.026f);
            if (_weapons.HitmarkerTimer > _prevWeaponHitmarkerTimer + 0.01f)
            {
                TriggerHitmarker(_weapons.HitmarkerIsCrit, kill: false);
                _prevWeaponHitmarkerTimer = _weapons.HitmarkerTimer;
            }
            float bloomKick = gun switch
            {
                WeaponType.SawedOff => 8.5f,
                WeaponType.ChatGun => 4.8f,
                WeaponType.MicroUzi => 3.4f,
                WeaponType.SolderingIron or WeaponType.Belt => 3.0f,
                _ => 2.2f
            };
            _crosshairGap=Math.Min(20f,_crosshairGap+bloomKick*steady);
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
            case WeaponType.BareHands: AudioMusicSystem.PlayHandGrab(); break;
            case WeaponType.CaptureNet: AudioMusicSystem.PlayNetSwipe(); break;
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
        world.PoseVeronika(camera.Position,Math.Min(.05f,Raylib.GetFrameTime()));
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
        if (comic.Active && !comic.OverlayOnMenu)
        {
            comic.Draw();
            Raylib.EndDrawing();
            return;
        }
        Raylib.ClearBackground(new Color(10, 12, 20, 255));

        if (MainMenuAndPauseUI.IsInMainMenu)
        {
            Camera3D menuCam;
            bool wardrobeOpen = MainMenuAndPauseUI.IsCharacterWardrobeOpen;
            if (wardrobeOpen)
            {
                if (_menuFloor.Partner != null)
                {
                    _menuFloor.Partner.Move(new PlayerPose(
                        new Vector3(0.0f, 1.72f, 10.35f),
                        0.16f + MainMenuAndPauseUI.WardrobeYawOffset,
                        0f,
                        WeaponType.BareHands,
                        Appearance: CharacterAppearance.Current.Pack()));
                }
                // Tailor Atelier 3D live stage framing the character cleanly in the open left half (X = 0..700)
                menuCam = new()
                {
                    Position = new Vector3(0.88f, 1.24f, 13.26f),
                    Target = new Vector3(0.88f, 0.96f, 10.35f),
                    Up = Vector3.UnitY,
                    FovY = 44f
                };
            }
            else
            {
                if (_menuFloor.Partner != null)
                {
                    _menuFloor.Partner.Move(new PlayerPose(
                        new Vector3(-26.2f, 1.72f, 5.5f),
                        1.26f,
                        0f,
                        WeaponType.MicroUzi,
                        Appearance: CharacterAppearance.Current.Pack()));
                }
                // Track the west-facing curtain wall without ticking gameplay.
                float shotTime = _menuTime % 28f;
                float travel = .5f - .5f*MathF.Cos(MathF.Min(shotTime,18f)*MathF.PI/18f);
                menuCam = new()
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
            }
            VisualEngine3D.SetWardrobeStage(wardrobeOpen);
            BeginLitWorld(_menuFloor,menuCam);
            Raylib.EndMode3D();
            VisualEngine3D.SetWardrobeStage(false);
            VisualEngine3D.Composite();
            MainMenuAndPauseUI.Draw(_uiFont, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
            LoreAndMenusUI.Draw(_uiFont);
            if (comic.OverlayOnMenu) comic.Draw();
            EndSceneFrame();
            return;
        }

        Camera3D playerCam = BuildPlayerCamera();
        Camera3D activeCam = BossIntroCutscene.ResolveCamera(_world, playerCam);
        bool cinemaCam = BossIntroCutscene.IsCameraOverrideActive(_world);
        bool suppressHands = BossIntroCutscene.ShouldSuppressViewModel(_world);
        float hudAlpha = BossIntroCutscene.GetHudAlpha(_world);

        // 1. 3D Skyscraper World + Entities + Combat Particles
        BeginLitWorld(_world, activeCam);
        foreach(var ally in _world.Teammates) ally.Effects.DrawWeaponWorldEffects3D(activeCam);
        _weapons.DrawWeaponWorldEffects3D(activeCam);
        Raylib.EndMode3D();

        bool modalOpen = _knockoutActive || _world.Slot.Active || _repairMinigame.IsActive || floorPanel!=FloorPanel.None || LoreAndMenusUI.IsAnyModalOpen || MainMenuAndPauseUI.IsPaused
            || MainMenuAndPauseUI.IsSaveLoadPanelOpen || MainMenuAndPauseUI.IsModsPanelOpen
            || MainMenuAndPauseUI.IsSettingsPanelOpen;
        if(!modalOpen && !suppressHands)
            _weapons.DrawFirstPersonHandsAndWeapon(playerCam,_walkBobPhase,_isCrouching,_uiFont,showCombatText:false);
        VisualEngine3D.Composite();
        if(_knockoutActive) { DrawKnockout(); EndSceneFrame(); return; }
        if(!modalOpen && showHud)
        {
            UiViewport.Begin();
            if (!cinemaCam)
            {
                _world.DrawWorldLabelsAndIndicators2D(activeCam,_uiFont);
                DrawCombatOverlay(_weapons,activeCam,_uiFont);
                DrawGameplayHud2D();
            }
            else if (hudAlpha > 0.35f)
            {
                DrawGameplayHud2D();
            }
            BossIntroCutscene.DrawOverlay(_world, _uiFont, _serif);
            Raylib.EndMode2D();
        }

        // 5. Modal Windows ([N] Lore, [F8] Audio Mixer, [K] Case Roulette) & [ESC] Pause Overlay

        if (MainMenuAndPauseUI.IsPaused || MainMenuAndPauseUI.IsSaveLoadPanelOpen || MainMenuAndPauseUI.IsModsPanelOpen || MainMenuAndPauseUI.IsSettingsPanelOpen)
        {
            MainMenuAndPauseUI.Draw(_uiFont, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        }

        LoreAndMenusUI.Draw(_uiFont);
        if(floorPanel!=FloorPanel.None) DrawFloorPanel();
        if(_world.Slot.Active) DrawSlot();
        if(_repairMinigame.IsActive) _repairMinigame.Draw(_uiFont, _serif);
        EndSceneFrame();
    }

    private static void DrawGameplayHud2D()
    {
        int sw=UiViewport.Width, sh=UiViewport.Height;
        int cx = sw/2, cy = sh/2;
        Color white = new(250,249,245,255), dark = new(22,24,38,142);
        string money = "$" + _playerCash;
        float moneyW = GameFonts.Measure(_serif, money, 26).X;
        ShadowText(_serif, money, 36, sh - 96, 26, white);
        float moneyRight = 36f + moneyW;
        if(Floor.PocketFrogs.Count>0)
        {
            string frogText = Loc.T($"Садок: {Floor.PocketFrogs.Count}", $"Frogs: {Floor.PocketFrogs.Count}");
            ShadowText(_uiFont, frogText, moneyRight + 12f, sh - 90, 15, white);
            moneyRight += 12f + GameFonts.Measure(_uiFont, frogText, 15).X;
        }

        // Clean stacked upper-left utility notices (zero collisions between Keycard, Radio, and Low-HP hint)
        int leftStackY = sh - 118;
        if(_world.HasElevatorKey && !_world.ElevatorBoss.IsDefeated)
        {
            ShadowText(_uiFont, "Ключ-карта СБ", 36, leftStackY, 15, WorldLabelLayout.Ivory);
            leftStackY -= 21;
        }
        if(Floor.HasRadio || Floor.WingsUnlocked)
        {
            ShadowText(_uiFont, "[X] Рация — Шлейф", 36, leftStackY, 14, new Color(224, 198, 148, 232));
            leftStackY -= 21;
        }

        var tools=_weapons.Hotbar;
        int barX=cx-(tools.Length*70-4)/2, barY=sh-82;

        Color heart = _playerHp > 30 ? white : new Color(254, 152, 159, 255);
        Color shadow = new(14, 12, 11, 185);
        Raylib.DrawCircle(43, sh - 46, 5.5f, shadow);
        Raylib.DrawCircle(53, sh - 46, 5.5f, shadow);
        Raylib.DrawTriangle(new(38.5f, sh - 45), new(48.5f, sh - 32.5f), new(58.5f, sh - 45), shadow);
        Raylib.DrawCircle(42, sh - 47, 5.5f, heart);
        Raylib.DrawCircle(52, sh - 47, 5.5f, heart);
        Raylib.DrawTriangle(new(37.5f, sh - 46), new(47.5f, sh - 33.5f), new(57.5f, sh - 46), heart);
        ShadowText(_serif, _playerHp.ToString(), 67, sh - 59, 28, heart);

        Vector2[] bolt = [new(155, sh - 56), new(145, sh - 43), new(152, sh - 43),
            new(149, sh - 33), new(161, sh - 47), new(154, sh - 47)];
        foreach (var o in new Vector2[] { new(1, 1), Vector2.Zero })
        {
            Color c = o == Vector2.Zero ? white : shadow;
            Raylib.DrawTriangle(bolt[0] + o, bolt[1] + o, bolt[2] + o, c);
            Raylib.DrawTriangle(bolt[0] + o, bolt[2] + o, bolt[5] + o, c);
            Raylib.DrawTriangle(bolt[2] + o, bolt[3] + o, bolt[4] + o, c);
            Raylib.DrawTriangle(bolt[2] + o, bolt[4] + o, bolt[5] + o, c);
        }
        ShadowText(_serif, ((int)MathF.Round(_energy)).ToString(), 170, sh - 59, 28, white);

        ShadowText(_uiFont,"[H]",232,sh-51,18,white);
        Raylib.DrawRing(new(277,sh-41),3,4.5f,-95,95,12,white);
        Raylib.DrawRectangleRounded(new(262,sh-47,14,13),.42f,6,white);
        ShadowText(_uiFont,$"×{(Net.Guest ? GuestCups : Floor.PocketCoffeeCups)}",286,sh-51,18,white);
        ShadowText(_uiFont,Loc.T("[G] Наживка","[G] Bait")+" ×"+Floor.BaitStock.Sum(),317,sh-51,16,white);

        if(_playerHp<65)
            ShadowText(_uiFont,Loc.T("Хил: [H] Кофе или Кофе-машина / Пицца [E]","Heal: [H] Coffee or Espresso / Pizza [E]"),36,leftStackY,15,new Color(178,238,201,(int)(205+42*MathF.Sin((float)Raylib.GetTime()*3))));
        if(HealFlashTimer>0)
        {
            Color mint=new(99,220,158,(int)(94*HealFlashTimer/.5f));
            Raylib.DrawRectangleGradientH(0,0,64,sh,mint,Color.Blank);
            Raylib.DrawRectangleGradientH(sw-64,0,64,sh,Color.Blank,mint);
        }
        DrawDirectionalHitIndicators2D(cx, cy);
        if(_lastDamageTimer>.4f)
        {
            float elapsed=2.6f-_lastDamageTimer;
            string alert=$"-{_lastDamageAmount} HP   {_lastDamageLabel}";
            ShadowText(_uiFont,alert,cx-GameFonts.Measure(_uiFont,alert,19).X/2,sh*.24f,19,new Color(255,173,170,(int)(255*(1-elapsed/2.2f))));
        }

        for(int i=0;i<tools.Length;i++)
        {
            Rectangle r=new(barX+i*70,barY,66,66);
            bool active=i==_weapons.SelectedHotbarSlot;
            Raylib.DrawRectangleRounded(new(r.X+2,r.Y+3,r.Width,r.Height),.12f,6,new Color(8,5,3,135));
            Raylib.DrawRectangleRounded(r,.12f,6,active ? new Color(46,33,24,228) : new Color(24,18,14,196));
            Raylib.DrawRectangleRoundedLinesEx(r,.12f,6,active ? 2f : 1f,active ? LoreAndMenusUI.ThemeBrassBright : new Color(118,92,56,165));
            LoreAndMenusUI.DrawTextCrisp(_serif,(i+1).ToString(),(int)r.X+6,barY+4,15,active ? LoreAndMenusUI.ThemeBrassBright : new Color(196,178,146,215));
            if(tools[i] is {} weapon)
                DrawSlotIcon(weapon==WeaponType.CaptureNet ? 4 : (int)weapon-1,new(r.X+34,r.Y+35),true);
        }

        DrawFloorTelemetry();
        bool bossTapeShownAtTop = _world.BossHudOpacity > 0.5f && _world.ElevatorBoss.Phase == 3
            && AudioMusicSystem.ActiveVoiceText == _world.CurrentTape;
        bool richSubtitle = CoachingSubtitle || (!BossIntroCutscene.IsActive(_world) && !bossTapeShownAtTop && AudioMusicSystem.VoiceTimer > 0 && AudioMusicSystem.SourceSpeaker is "Петрович" or "ИИ-Агент v0.1" or "SYSTEM ZERO");
        bool shortSubtitle = !richSubtitle && !bossTapeShownAtTop && AudioMusicSystem.VoiceTimer > 0;
        int subtitleTopY = sh - 96;
        if (richSubtitle)
        {
            var face = GameFonts.Get(Typeface.InterfaceBold, 17);
            float limit = Math.Min(sw - 180, 780);
            var lines = new List<string>();
            string row = "";
            string caption = Loc.IsEn ? AudioMusicSystem.ActiveSpeaker + ": " + AudioMusicSystem.ActiveVoiceText : AudioMusicSystem.ActiveVoiceText;
            foreach (var word in caption.Split(' '))
            {
                string next = row.Length == 0 ? word : row + " " + word;
                if (row.Length > 0 && GameFonts.Measure(face, next, 17).X > limit)
                { lines.Add(row); row = word; }
                else row = next;
            }
            if (row.Length > 0) lines.Add(row);
            float maxW = 0f;
            foreach (var ln in lines) maxW = Math.Max(maxW, GameFonts.Measure(face, ln, 17).X);
            float pillW = maxW + 32f;
            float pillH = lines.Count * 22f + 14f;
            float pillY = sh - 94f - pillH;
            subtitleTopY = (int)pillY;
            Rectangle pill = new(cx - pillW * 0.5f, pillY, pillW, pillH);
            Raylib.DrawRectangleRounded(pill, 0.22f, 8, new Color(14, 11, 16, 208));
            Raylib.DrawRectangleRoundedLinesEx(pill, 0.22f, 8, 1f, new Color(188, 152, 88, 92));
            for (int i = 0; i < lines.Count; i++)
            {
                float lw = GameFonts.Measure(face, lines[i], 17).X;
                ShadowText(face, lines[i], cx - lw * 0.5f, pillY + 7f + i * 22f, 17, white);
            }
        }
        else if (shortSubtitle)
        {
            string line = Loc.IsEn ? AudioMusicSystem.ActiveSpeaker + ": " + AudioMusicSystem.ActiveVoiceText : AudioMusicSystem.ActiveVoiceText;
            float width = Math.Min(sw - 48, GameFonts.Measure(_uiFont, line, 16).X + 28);
            Rectangle caption = new(cx - width / 2, sh - 126, width, 30);
            subtitleTopY = (int)caption.Y;
            Raylib.DrawRectangleRounded(caption, .30f, 8, new Color(14, 11, 16, 208));
            Raylib.DrawRectangleRoundedLinesEx(caption, .30f, 8, 1f, new Color(188, 152, 88, 90));
            MainMenuAndPauseUI.Center(_uiFont, line, caption, 16, white);
        }

        var camera=BuildPlayerCamera();
        string target=CoopEscort(camera,false,apply:false) ?? _world.FocusPrompt(camera,_weapons.CurrentWeapon);
        bool healingLesson=Floor.Onboarding==OnboardingStep.HealAtCoffee;
        if(target.Length>0 && (healingLesson || !(_foregroundBanner && _hudStatusTimer>0)))
        {
            string prompt=target==Loc.CoolerEmpty ? target : "[E] "+target;
            float width=GameFonts.Measure(_serif,prompt,18).X;
            float pillW = width + 28f;
            float pillH = 32f;
            float promptY = (richSubtitle || shortSubtitle) ? Math.Min(subtitleTopY - 40f, sh - 168f) : sh - 142f;
            Rectangle pRect = new(cx - pillW * 0.5f, promptY, pillW, pillH);
            Raylib.DrawRectangleRounded(pRect, 0.28f, 8, new Color(14, 11, 16, 198));
            Raylib.DrawRectangleRoundedLinesEx(pRect, 0.28f, 8, 1f, new Color(214, 176, 102, 138));
            GameFonts.Draw(_serif,prompt,new(cx-width/2,promptY+6f),18,white);
        }
        else if (!healingLesson && _hudStatusTimer>0 && !string.IsNullOrEmpty(_hudStatusBanner) && (_foregroundBanner || !CoachingSubtitle))
        {
            var face = GameFonts.Get(Typeface.InterfaceBold, 20);
            float width=Math.Min(sw-24,GameFonts.Measure(face,_hudStatusBanner,20).X+28);
            Rectangle plate=new(cx-width/2,cy+22,width,34);
            Raylib.DrawRectangleRounded(plate, 0.20f, 8, new Color(16,13,16,218));
            Raylib.DrawRectangleRoundedLinesEx(plate, 0.20f, 8, 1f, new Color(196,158,92,105));
            MainMenuAndPauseUI.Center(face,_hudStatusBanner,plate,20,DossierPrint.Ivory);
        }
        DrawCrosshairAndHitmarker2D(cx, cy, white);
        if(_weapons.CurrentWeapon==WeaponType.SolderingIron && _weapons.IronLockout>0)
            ShadowText(_uiFont,"Жало остывает",cx-56,cy+54,16,amber);
        if (_weapons.Capacity>0)
        {
            string rounds=_weapons.Reloading ? $"ПЕРЕЗАРЯДКА  {_weapons.ReloadProgress:0%}" : $"{_weapons.Ammo} / {_weapons.Capacity}";
            int w=(int)GameFonts.Measure(_uiFont,rounds,25).X;
            LoreAndMenusUI.DrawTextCrisp(_uiFont,rounds,sw-25-w,sh-43,25,white);
        }
        if (_playerHitAnimTimer>0)
        {
            Color Edge(float side) => new(202,14,48,(int)((63+145*Math.Max(0,side))*PlayerHitWeight));
            int width=sw/9, height=sh/9;
            Raylib.DrawRectangleGradientH(0,0,width,sh,Edge(-_playerHitDir.X),Color.Blank);
            Raylib.DrawRectangleGradientH(sw-width,0,width,sh,Color.Blank,Edge(_playerHitDir.X));
            Raylib.DrawRectangleGradientV(0,0,sw,height,Edge(_playerHitDir.Y),Color.Blank);
            Raylib.DrawRectangleGradientV(0,sh-height,sw,height,Color.Blank,Edge(-_playerHitDir.Y));
        }
    }

    private static void DrawDirectionalHitIndicators2D(int cx, int cy)
    {
        bool any = false;
        for (int i = 0; i < _hitArcs.Length; i++)
            if (_hitArcs[i].Duration > 0f && _hitArcs[i].Age < _hitArcs[i].Duration) { any = true; break; }
        if (!any) return;

        var hitCamera = BuildPlayerCamera();
        Vector3 forward = new(hitCamera.Target.X - hitCamera.Position.X, 0f, hitCamera.Target.Z - hitCamera.Position.Z);
        if (forward.LengthSquared() < 0.0001f) forward = Vector3.UnitZ;
        forward = Vector3.Normalize(forward);
        Vector3 right = Vector3.Cross(forward, Vector3.UnitY);
        Vector2 center = new(cx, cy);

        for (int i = 0; i < _hitArcs.Length; i++)
        {
            var arc = _hitArcs[i];
            if (arc.Duration <= 0f || arc.Age >= arc.Duration) continue;
            float t = Math.Clamp(arc.Age / arc.Duration, 0f, 1f);
            float alpha = MathF.Pow(1f - t, 1.45f);
            if (alpha <= 0.01f) continue;

            Vector3 delta = arc.SourcePos - hitCamera.Position;
            delta.Y = 0f;
            float theta;
            if (delta.LengthSquared() < 0.0001f)
                theta = MathF.PI * 0.5f;
            else
            {
                Vector3 dir3 = Vector3.Normalize(delta);
                float side = Vector3.Dot(dir3, right);
                float ahead = Vector3.Dot(dir3, forward);
                theta = MathF.Atan2(-ahead, side);
            }

            float radius = 68f + t * 5f;
            float halfSpan = (20f + Math.Clamp(arc.Damage * 0.24f, 0f, 12f)) * (MathF.PI / 180f);
            const int segments = 14;

            Color glowCol = new(196, 18, 30, (int)(92 * alpha));
            Color darkRim = new(18, 6, 8, (int)(168 * alpha));
            Color coreCol = new(244, 46, 56, (int)(242 * alpha));

            for (int s = 0; s < segments; s++)
            {
                float f0 = s / (float)segments;
                float f1 = (s + 1) / (float)segments;
                float fm = (f0 + f1) * 0.5f;
                float a0 = theta - halfSpan + f0 * halfSpan * 2f;
                float a1 = theta - halfSpan + f1 * halfSpan * 2f;
                Vector2 p0 = center + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * radius;
                Vector2 p1 = center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius;
                float taper = MathF.Sin(fm * MathF.PI);
                float thick = 1.0f + 5.6f * taper;
                Raylib.DrawLineEx(p0, p1, thick + 5.0f, glowCol);
                Raylib.DrawLineEx(p0, p1, thick + 1.8f, darkRim);
                Raylib.DrawLineEx(p0, p1, thick, coreCol);
            }

            // Sleek directional apex notch pointing directly toward the attacker
            Vector2 radial = new(MathF.Cos(theta), MathF.Sin(theta));
            Vector2 tangent = new(-radial.Y, radial.X);
            Vector2 tip = center + radial * (radius + 9.5f);
            Vector2 baseL = center + radial * (radius + 1.5f) - tangent * 6.5f;
            Vector2 baseR = center + radial * (radius + 1.5f) + tangent * 6.5f;
            Raylib.DrawTriangle(tip, baseL, baseR, coreCol);
            Raylib.DrawTriangle(tip, baseR, baseL, coreCol);
        }
    }

    private static void DrawCrosshairAndHitmarker2D(int cx, int cy, Color white)
    {
        Vector2 center = new(cx, cy);
        float gap = MathF.Round(_crosshairGap);
        if (gap < 10.5f)
        {
            Raylib.DrawCircleV(center, 2.3f, new Color(14, 14, 16, 165));
            Raylib.DrawCircleV(center, 1.3f, white);
        }
        for (int i = 0; i < 4; i++)
        {
            Vector2 axis = i switch { 0 => Vector2.UnitX, 1 => -Vector2.UnitX, 2 => Vector2.UnitY, _ => -Vector2.UnitY };
            Vector2 a = center + axis * gap;
            Vector2 b = a + axis * 5.5f;
            Raylib.DrawLineEx(a, b, 3.0f, new Color(14, 16, 20, 168));
            Raylib.DrawLineEx(a, b, 1.5f, white);
        }

        if (_hitmarkerDuration <= 0f || _hitmarkerAge >= _hitmarkerDuration) return;
        float t = Math.Clamp(_hitmarkerAge / _hitmarkerDuration, 0f, 1f);
        float ease = 1f - (1f - t) * (1f - t);
        float fade = t < 0.35f ? 1f : Math.Clamp(1f - (t - 0.35f) / 0.65f, 0f, 1f);
        if (fade <= 0.01f) return;

        Color ink = _hitmarkerKill
            ? new Color(246, 52, 62, (int)(255 * fade))
            : _hitmarkerCrit
                ? new Color(255, 208, 86, (int)(252 * fade))
                : new Color(252, 249, 242, (int)(246 * fade));
        Color outline = new(12, 10, 12, (int)(195 * fade));

        float inner = (_hitmarkerKill ? 8.5f : _hitmarkerCrit ? 8.0f : 6.5f) + ease * (_hitmarkerKill ? 5.0f : 4.0f);
        float outer = inner + (_hitmarkerKill ? 10.5f : _hitmarkerCrit ? 9.0f : 7.0f);
        float stroke = _hitmarkerKill ? 2.8f : _hitmarkerCrit ? 2.4f : 1.9f;

        for (int sx = -1; sx <= 1; sx += 2)
        for (int sy = -1; sy <= 1; sy += 2)
        {
            Vector2 diag = Vector2.Normalize(new Vector2(sx, sy));
            Vector2 p0 = center + diag * inner;
            Vector2 p1 = center + diag * outer;
            Raylib.DrawLineEx(p0, p1, stroke + 2.6f, outline);
            Raylib.DrawLineEx(p0, p1, stroke, ink);

            if (_hitmarkerKill)
            {
                float inner2 = outer + 3.4f + ease * 3.2f;
                float outer2 = inner2 + 7.2f;
                Vector2 q0 = center + diag * inner2;
                Vector2 q1 = center + diag * outer2;
                Raylib.DrawLineEx(q0, q1, stroke + 1.8f, outline);
                Raylib.DrawLineEx(q0, q1, stroke * 0.80f, ink);
            }
        }
    }

    private static bool CoachingSubtitle => AudioMusicSystem.VoiceTimer>0 && AudioMusicSystem.SourceSpeaker=="Вероника";

    private static Texture2D[] _slotIcons=[];

    private static void LoadSlotIcons()
    {
        if (_slotIcons.Length==0)
        {
            string[] files =
            [
                "icon_slot_1_belt.png",
                "icon_slot_2_solder.png",
                "icon_slot_3_uzi.png",
                "icon_slot_4_shotgun.png", "icon_slot_net.png"
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

    }

    internal static void DrawSlotIcon(int index, Vector2 center, bool unlocked, Color? accent = null)
    {
        Color whiteIcon=accent ?? Color.White;
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
        else if(index==4)
        {
            Raylib.DrawLineEx(center+new Vector2(-17,16),center+new Vector2(4,-4),3,whiteIcon);
            Raylib.DrawEllipseLines((int)center.X+7,(int)center.Y-7,13,8,whiteIcon);
            Raylib.DrawLineEx(center+new Vector2(-5,-6),center+new Vector2(11,5),1,whiteIcon);
            Raylib.DrawLineEx(center+new Vector2(11,5),center+new Vector2(19,-6),1,whiteIcon);
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

    private static unsafe void CheckArchiveControls(Action<string> capture)
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
            LoreAndMenusUI.FirstVisibleArchiveEntry!=1 || LoreAndMenusUI.SelectedArchiveEntry!=5)
            throw new InvalidDataException("Last five archive threads do not fill the viewport");
        for (int entry = 1; entry <= 5; entry++)
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
        ClickCenter(LoreAndMenusUI.ArchiveRowBounds(1));
        capture("06_petrovich.png");
        ClickCenter(LoreAndMenusUI.ArchiveCloseBounds);
        Expect(-1);
        Key(KeyboardKey.N);
        Expect(0);
        Key(KeyboardKey.Escape);
        Expect(-1);
        Key(KeyboardKey.N);
        Key(KeyboardKey.N);
        Expect(-1);
        Console.WriteLine("[AUTOTEST] PASS archive: four tabs, mouse, Tab, N, Escape, smooth scroll to threads 2-6");
    }

    private static unsafe void PressTestKey(KeyboardKey key)
    {
        bool exit=false;
        AutomationEvent e=new() {Type=2}; e.Params[0]=(int)key;
        Raylib.PlayAutomationEvent(e); UpdateFrame(1f/60,ref exit); Raylib.PollInputEvents();
        e.Type=1; Raylib.PlayAutomationEvent(e); Raylib.PollInputEvents();
    }

    private static void OpenKeycardRow()
    {
        _playerPos=_world.HrTarotBoothPos+new Vector3(0,1.72f,-2.2f);
        LookAtTarget(_world.HrTarotBoothPos+Vector3.UnitY*1.6f);
        PressTestKey(KeyboardKey.E);
        if(floorPanel!=FloorPanel.Upgrades || sidegrades) throw new InvalidDataException("Expected Veronika's arsenal");
        PressTestKey(KeyboardKey.Up);
        PressTestKey(KeyboardKey.Right);
        if(FloorSystems.Arsenal[floorRow]!=FloorSystems.ElevatorKeycard) throw new InvalidDataException("Expected keycard row");
    }

    private static unsafe void CheckZeroToEmpire(Action<string> capture)
    {
        void Check(bool ok,string step)
        {
            if(!ok) throw new InvalidDataException("Zero-to-Empire: "+step);
        }
        bool exit=false;
        void Key(KeyboardKey key) => PressTestKey(key);
        void Approach(Vector3 point,float gap=1.25f)
        {
            _playerPos=_world.ResolvePlayerCollision(new(point.X,_eyeHeight,point.Z+gap));
            LookAtTarget(point);
        }
        void Attack()
        {
            _weaponFireCooldown=0;
            UpdateTrigger(1f/60,true,true,false,false);
            TickFloor(1f/60);
        }
        void Guided(OnboardingStep step,string words)
        {
            Check(Floor.Onboarding==step && CoachingSubtitle && AudioMusicSystem.ActiveVoiceText.Contains(words),"coaching "+step);
            Check((_world.OnboardingWaypoint(_playerPos)!=null)==(step is not (OnboardingStep.Done or OnboardingStep.ActivateBait)),"beacon "+step);
        }
        void RoundTrip()
        {
            var data=MainMenuAndPauseUI.CaptureCurrentGameState!();
            var equipped=_weapons.CurrentWeapon;
            int selected=_weapons.SelectedHotbarSlot;
            var step=Floor.Onboarding;
            int wallet=_playerCash, mask=Floor.OwnedWeaponsMask;
            var skins=Floor.SkinMasks.ToArray(); var finishes=Floor.EquippedSkins.ToArray();
            int cups=Floor.PocketCoffeeCups; bool unlocked=Floor.WingsUnlocked;
            var pocket=Floor.PocketFrogs.ToArray(); var discoveries=Floor.DiscoveredSpecies.ToArray();
            string path=Path.GetTempFileName();
            try
            {
                File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(data));
                WriteFloorSave(data,path,pendingShift!);
                ResetNewGameFloor(greet:false);
                var saved=System.Text.Json.JsonSerializer.Deserialize<GameSaveData>(File.ReadAllText(path))!;
                _playerCash=saved.Cash;
                RestoreFloorSave(saved,path);
                Check(Floor.Onboarding==step && !CoachingSubtitle && mask==Floor.OwnedWeaponsMask && equipped==_weapons.CurrentWeapon && selected==_weapons.SelectedHotbarSlot && _playerCash==wallet &&
                    skins.SequenceEqual(Floor.SkinMasks) && finishes.SequenceEqual(Floor.EquippedSkins) && cups==Floor.PocketCoffeeCups && unlocked==Floor.WingsUnlocked && pocket.SequenceEqual(Floor.PocketFrogs) && discoveries.SequenceEqual(Floor.DiscoveredSpecies),"slot round-trip");
            }
            finally { pendingShift=null; pendingFloorSave=null; File.Delete(path); }
        }
        ResetNewGameFloor();
        var bait=_world.CentralMacBookPos+Vector3.UnitY*(SkyscraperFloor1World.TrapTop+.13f);
        _world.ActivateMacBookTrap(SubscriptionBaitTier.FreeTier,_=>true,out _);
        Check(Floor.Onboarding==OnboardingStep.TalkToVeronika && _world.ActiveCoders.Count==1 &&
            _world.ActiveCoders[0].TimidIntern && Math.Abs(_world.ActiveCoders[0].Position.X)<8.5f,"safe early bait");
        Floor.BuyWeapon(0,_=>true); TickFloor(1f/60);
        var trainee=_world.ActiveCoders.Single();
        trainee.Position=new(-5.6f,0,10.4f);
        _playerPos=new(-5.6f,1.72f,5.6f);
        bool telegraph=false;
        for(int i=0;i<400 && Floor.Onboarding==OnboardingStep.StunAndHeal;i++)
        { TickFloor(1f/60); telegraph|=trainee.Windup>0; }
        Check(telegraph && _playerHp==82 && _lastDamageLabel=="Вайбкодер (Конфликт в main)" &&
            _lastDamageSourcePos==trainee.Position,"telegraphed training projectile");
        Key(KeyboardKey.H);
        Check(Floor.Onboarding==OnboardingStep.CaptureFirstCoder && _playerHp==100 && Floor.PocketCoffeeCups==1,"alternative healing lesson");
        _playerPos=new(.8f,1.72f,14.3f);
        Floor.NextIncident=.01f; Floor.SEC=85;
        for(int i=0;i<300;i++) TickFloor(.05f);
        Check(Floor.LastIncident==-1 && !Floor.Raid && Floor.Audit==0 && _world.ActiveCoders.Count==2 &&
            _world.Frogs.All(f=>Math.Abs(f.Feet.X)<8.5f && f.Feet.Z is >=4 and <=14.5f),"contained atrium");
        ResetNewGameFloor();
        Floor.PocketFrogs.AddRange(["frog_hopper","frog_hopper","frog_syntax"]);
        TickFloor(1f/60); Guided(OnboardingStep.DepositFrogs,"Отличный улов");
        ResetNewGameFloor();
        GrantCash(3,"Тест"); TickFloor(1f/60); Guided(OnboardingStep.BuyNet,"Наличка");
        ResetNewGameFloor();
        Floor.BuyWeapon(0,_=>true); TickFloor(1f/60); Guided(OnboardingStep.ActivateBait,"Сачок в руках");
        ResetNewGameFloor();
        Guided(OnboardingStep.TalkToVeronika,"хозяин вернулся");
        var entrance=_playerPos;
        var gate=new Vector3(-9.6f,1.72f,5.2f);
        _playerPos=gate+new Vector3(2.45f,0,.4f); LookAtTarget(gate);
        capture("30_locked_wing_barrier.png");
        var eastGate=new Vector3(9.6f,1.72f,5.2f);
        _playerPos=eastGate+new Vector3(-2.15f,0,-.85f);
        LookAtTarget(eastGate-new Vector3(0,0,1.48f));
        Loc.Set(GameLang.En,false);
        try
        {
            Key(KeyboardKey.E);
            capture("37_en_hud_and_office.png");
        }
        finally { Loc.Set(GameLang.Ru,false); }
        Check(AudioMusicSystem.ActiveClipId=="vo_veronika_wings_locked" && Floor.BarrierVoiceWait>24,"barrier explains quarantine");
        Floor.Coach(); Key(KeyboardKey.E);
        Check(AudioMusicSystem.ActiveClipId!="vo_veronika_wings_locked","barrier voice cooldown");
        foreach(var (feet,side) in SkyscraperFloor1World.Gates)
        {
            var normal=side?Vector3.UnitX*Math.Sign(-feet.X):Vector3.UnitZ;
            var probe=feet+normal*.04f;
            Check(Vector3.Dot(_world.ResolvePlayerCollision(probe)-feet,normal)>.49f,"solid gate "+feet);
        }
        foreach(int side in new[]{-1,1})
        {
            _playerPos=new(side*8.5f,1.72f,5.2f);
            for(int frame=0;frame<25;frame++) MovePlayer(1f/60,Vector3.UnitX*side,Vector3.UnitX*side,false,false,false);
            Check(side*_playerPos.X<9.2f,"walk blocked at wing gate");
        }
        _playerPos=entrance; LookAtTarget(_world.HrTarotBoothPos+new Vector3(-.45f,1.26f,0));
        var cam=BuildPlayerCamera();
        var hr=_world.HrTarotBoothPos+Vector3.UnitY*2.12f;
        Check(_playerPos==LobbyEntrance && WorldLabelLayout.WaypointProjection(cam,hr,180).Direction==Vector2.Zero,"safe spawn facing HR");
        float hrDistance=Vector3.Distance(hr,cam.Position);
        Check(_world.PelletDistance(cam.Position,Vector3.Normalize(hr-cam.Position),hrDistance)+.02f>=hrDistance,"unobstructed HR beacon");
        var back=cam.Position-(cam.Target-cam.Position)*5;
        var right=Vector3.Normalize(Vector3.Cross(cam.Target-cam.Position,cam.Up));
        Check(WorldLabelLayout.WaypointProjection(cam,back-right,180).Direction.X<0 &&
            WorldLabelLayout.WaypointProjection(cam,back+right,180).Direction.X>0,"turn toward offscreen beacon");
        capture("19_onboarding_veronika.png");
        Check(_playerCash==0 && _weapons.HotbarSlotCount==4 && _weapons.Hotbar.All(w=>w==null) && _weapons.SelectedHotbarSlot==-1 &&
            _weapons.CurrentWeapon==WeaponType.BareHands && Floor.OwnedWeaponsMask==1,"starting inventory");
        for(int i=0;i<4;i++)
        {
            Key(KeyboardKey.One+i);
            Check(_weapons.SelectedHotbarSlot==i && _weapons.CurrentWeapon==WeaponType.BareHands,"empty slot "+i);
        }
        _weapons.CurrentWeapon=WeaponType.MicroUzi;
        _weapons.SelectSlot(3); Key(KeyboardKey.Six);
        Check(!Floor.HasWeapon(WeaponType.CaptureNet) && !TrySpendCash(3),"zero budget");
        Check(_weapons.CurrentWeapon==WeaponType.BareHands,"locked weapon");
        RoundTrip();
        Check(_world.Frogs.Count == 3 && _world.ActiveCoders.Count==0,"visible lobby population");
        Check(_world.Frogs.Any(f=>f.Feet.Y>.15f),"jumping frogs at spawn");
        _weapons.Equip(WeaponType.BareHands);
        capture("12_bare_start_frogs.png");
        AutomationEvent forward=new() {Type=2}; forward.Params[0]=(int)KeyboardKey.W;
        Raylib.PlayAutomationEvent(forward);
        for(int frame=0;frame<36;frame++)
        {
            UpdateFrame(1f/60,ref exit);
            Raylib.PollInputEvents();
        }
        forward.Type=1; Raylib.PlayAutomationEvent(forward); Raylib.PollInputEvents();
        Check(_world.FocusPrompt(BuildPlayerCamera())=="Поговорить с Вероникой","HR greeting prompt");
        Key(KeyboardKey.E);
        Guided(OnboardingStep.CatchFrogs,"Касса пуста");
        var frogTarget=_world.OnboardingWaypoint(_playerPos);
        Check(_world.Frogs.Any(f=>f.CatchableByHand && f.Feet==frogTarget?.Position),"hand-catchable frog waypoint");
        Check(floorPanel==FloorPanel.None,"HR briefing stays in world");
        Check(_world.FocusPrompt(BuildPlayerCamera())=="HR Вероника — Магазин оружия и апгрейдов","HR shop prompt after briefing");
        Key(KeyboardKey.E); Check(floorPanel==FloorPanel.Upgrades,"empty hands can revisit HR shop"); Key(KeyboardKey.Escape);

        for(int i=0;i<3;i++)
        {
            var frog=_world.Frogs.First(f=>f.SpeciesId=="frog_hopper");
            Approach(frog.Feet+Vector3.UnitY*.11f);
            if(i==2) { _weaponFireCooldown=0; Key(KeyboardKey.E); } else Attack();
            Check(Floor.PocketFrogs.Count==i+1,"bare catch "+i);
            if(i==0) Guided(OnboardingStep.CatchFrogs,"Есть первая");
        }
        Guided(OnboardingStep.DepositFrogs,"Отличный улов");
        Check(_world.OnboardingWaypoint(_playerPos)?.Position==SkyscraperFloor1World.TerminalPos,"terminal waypoint");
        RoundTrip();
        Check(Floor.DiscoveredSpecies.Count==0,"blind pocket");
        Approach(SkyscraperFloor1World.TerminalPos+Vector3.UnitY*1.08f);
        Check(_world.FocusPrompt(BuildPlayerCamera()).Contains("(3 шт.)"),"terminal focus");
        Key(KeyboardKey.E);
        Check(_playerCash==4 && Floor.PocketFrogs.Count==0 && Floor.DiscoveredSpecies["frog_hopper"]==3 &&
            _hudStatusBanner.Contains("Хватает"),"terminal payout plus one discovery");
        Guided(OnboardingStep.BuyNet,"Наличка");
        Approach(_world.HrTarotBoothPos+Vector3.UnitY*1.6f,-2.3f);
        Key(KeyboardKey.E); Check(floorPanel==FloorPanel.Upgrades && !sidegrades,"HR weapon shop");
        Key(KeyboardKey.Tab); Check(sidegrades,"upgrade tab");
        Key(KeyboardKey.One); Key(KeyboardKey.Enter);
        Guided(OnboardingStep.ActivateBait,"Сачок в руках");
        Check(Floor.BaitStock[0]==1,"starter bait");
        Check(floorPanel==FloorPanel.None && _weapons.CurrentWeapon==WeaponType.CaptureNet,"net ready in world");
        LookAtTarget(bait);
        capture("20_onboarding_bait_beacon.png");
        _playerPos=new(-5.8f,1.72f,7.4f); LookAtTarget(new(-5.8f,.1f,6)); Key(KeyboardKey.G);
        _playerPos=new(4,1.72f,11); for(int i=0;i<70;i++) TickFloor(1f/60);
        Guided(OnboardingStep.StunAndHeal,"Пошли клиенты");
        Check(_world.OnboardingWaypoint(_playerPos)?.Position==_world.ActiveCoders.MinBy(c=>Vector3.DistanceSquared(_playerPos,c.Position))?.Position,"nearest candidate");
        Check(_world.IsMacBookTrapActive && _world.ActiveCoders.Any(c=>c.TimidIntern),"guided free wave");
        Check(_playerCash==1 && _weapons.SelectedHotbarSlot==0 && _weapons.HotbarSlotCount==4 &&
            _weapons.Hotbar[0]==WeaponType.CaptureNet && _weapons.Hotbar.Skip(1).All(w=>w==null),"paid net in first slot");
        Key(KeyboardKey.One);
        Check(_weapons.CurrentWeapon==WeaponType.BareHands && _weapons.SelectedHotbarSlot==-1,"holster net");
        Key(KeyboardKey.One);
        Check(_weapons.CurrentWeapon==WeaponType.CaptureNet && _weapons.SelectedHotbarSlot==0,"draw net");
        Key(KeyboardKey.Two);
        Check(_weapons.CurrentWeapon==WeaponType.BareHands && _weapons.SelectedHotbarSlot==1,"select empty slot");
        AutomationEvent scroll=new() {Type=8}; scroll.Params[1]=1;
        Raylib.PlayAutomationEvent(scroll); UpdateFrame(1f/60,ref exit); Raylib.PollInputEvents();
        Check(_weapons.CurrentWeapon==WeaponType.CaptureNet && _weapons.SelectedHotbarSlot==0,"scroll to net");
        _playerPos=new(.2f,1.72f,11.5f); LookAtTarget(new(.25f,.75f,7.4f));
        for(int i=0;i<32;i++) _weapons.UpdateWeapons(1f/60,BuildPlayerCamera());
        capture("13_net_and_progression.png");
        var crash=_world.Frogs.FirstOrDefault(f=>f.SpeciesId=="frog_crash") ?? _world.SpawnFrog(0,"frog_crash");
        for(int i=0;i<60 && crash.VentExit;i++) TickFloor(1f/60);
        Approach(crash.Feet+Vector3.UnitY*.11f);
        Key(KeyboardKey.One); Attack();
        Check(_world.Frogs.Contains(crash) && Floor.PocketFrogs.Count==0 && _hudStatusBanner=="Нужен Сачок ($3) у Вероники","crash dodge");
        Key(KeyboardKey.One);
        Approach(crash.Feet+Vector3.UnitY*.11f); Attack();
        Check(Floor.PocketFrogs.Contains("frog_crash"),"net catches crash");
        // Four simultaneous targets exercise the sweep limit through the combat path.
        var cluster=new Vector3(24,0,18);
        for(int i=0;i<4;i++)
            _world.Frogs.Add(new BugFrog {SpeciesId=i==0 ? "frog_hotfix" : "frog_syntax",
                From=cluster+Vector3.UnitX*(i-.5f)*.12f,To=cluster,Rest=1});
        int carried=Floor.PocketFrogs.Count;
        Approach(cluster+Vector3.UnitY*.15f,1.7f); Attack();
        Check(Floor.PocketFrogs.Count==carried+3 && _world.Frogs.Count(f=>f.From.X>22)==1,"three per sweep");
        var intern=_world.ActiveCoders.First(c=>c.TimidIntern);
        intern.ExitingRoom=false;
        intern.Position=new(-4.5f,0,6);
        Approach(intern.CombatCenter);
        Attack(); Check(intern.Hp==12 && intern.State!=CoderAIState.Subdued,"first net swipe");
        LookAtTarget(intern.CombatCenter);
        Attack();
        if(intern.DodgeCooldown>0 && intern.Hp>0) { Approach(intern.CombatCenter); Attack(); }
        Check(intern.State==CoderAIState.Subdued,"net subdues intern within dodge cooldown");
        Guided(OnboardingStep.HealAtCoffee,"Тебя задели");
        Check(_playerHp==82 && _lastDamageAmount==18 && _lastDamageTimer>2.4f && _lastDamageLabel.Contains("искра"),"attributed scratch");
        _playerPos=SkyscraperFloor1World.CoffeePos+new Vector3(-.6f,1.72f,1.65f);
        LookAtTarget(SkyscraperFloor1World.CoffeePos+Vector3.UnitY*1.03f);
        capture("31_heal_station_and_hud.png");
        _playerHp=42; _energy=25; Floor.PocketCoffeeCups=1;
        Approach(SkyscraperFloor1World.CoffeePos+Vector3.UnitY*1.15f); Key(KeyboardKey.E);
        Check(_playerHp==87 && _energy>=75 && Floor.PocketCoffeeCups==2 && Floor.EspressoWait>11,$"espresso +45 +50 and refill hp={_playerHp} energy={_energy} cups={Floor.PocketCoffeeCups} wait={Floor.EspressoWait} focus={_world.FocusPrompt(BuildPlayerCamera())} msg={_hudStatusBanner}");
        Guided(OnboardingStep.CaptureFirstCoder,"Бодрящий");
        _playerHp=40; Key(KeyboardKey.H);
        Check(_playerHp==70 && Floor.PocketCoffeeCups==1,"pocket +30");
        _playerHp=35; Approach(new(4,.95f,13)); Key(KeyboardKey.E);
        Check(_playerHp==70,"pizza +35");
        Approach(_world.Coolers.First()+Vector3.UnitY*.9f); Key(KeyboardKey.E);
        Check(_playerHp==95,"water +25");
        _playerHp=35; Key(KeyboardKey.E); Key(KeyboardKey.E);
        Check(_playerHp==60,"cooler has two drinks");
        Approach(new(4,.95f,13)); Key(KeyboardKey.E); Check(_playerHp==60,"pizza on cooldown");
        Approach(SkyscraperFloor1World.CoffeePos+Vector3.UnitY*1.15f); Key(KeyboardKey.E);
        Check(_playerHp==60 && Floor.PocketCoffeeCups==1,"espresso on cooldown");
        intern.Position=new(-1.7f,0,6.7f);
        intern.FacingYaw=0;
        _playerPos=intern.Position+new Vector3(.34f,1.72f,1.75f);
        LookAtTarget(intern.Position+Vector3.UnitY*.50f);
        var runner=_world.SpawnCoderForTier(SubscriptionBaitTier.FreeTier);
        runner.Position=intern.Position+new Vector3(1.1f,0,.28f);
        runner.ExitingRoom=false;
        var start=runner.Position;
        for(int i=0;i<9;i++) TickFloor(1f/60);
        Check(runner.State==CoderAIState.PanickedFleeing && runner.SpeechTimer>0 && runner.WalkCycle>0 &&
            Vector3.Distance(runner.Position,start)>.6f,"approached coder sprints and cries out");
        Floor.DiscoveryTime=_lastDamageTimer=AudioMusicSystem.VoiceTimer=0;
        SetHudBanner("",0);
        capture("51_coder_flee_and_stun_stars.png");
        _world.ActiveCoders.Remove(runner);
        Key(KeyboardKey.E);
        Check(_world.CarriedCoder==intern && _weapons.CurrentWeapon==WeaponType.BareHands && intern.IdentityRevealed,"armed E auto holsters and signs NDA");
        Key(KeyboardKey.One);
        Check(_weapons.CurrentWeapon==WeaponType.BareHands && _weapons.SelectedHotbarSlot==-1 &&
            _world.CarriedCoder==intern && _hudStatusBanner.Contains("Отнеси вайбкодера"),"draw blocked during escort");
        Check(_world.OnboardingWaypoint(_playerPos)?.Position==_world.HrTarotBoothPos,"HR delivery waypoint");
        Approach(_world.HrTarotBoothPos+Vector3.UnitY*1.6f,-2.2f); Key(KeyboardKey.E);
        Check(!Floor.WingsUnlocked && _world.TotalCodersSoldToHr==1,"first sale preserves quarantine");
        for(int sale=2;sale<=3;sale++)
        {
            ApplyFloorAction(50);
            _playerPos=new(-5.8f,1.72f,7.4f); LookAtTarget(new(-5.8f,.1f,6)); Key(KeyboardKey.G);
            _playerPos=new(4,1.72f,11);
            for(int tick=0;tick<120;tick++) TickFloor(1f/60);
            var candidate=_world.ActiveCoders.First(c=>c.TimidIntern);
            if(sale==3) Check(candidate.Rarity==VibeCoderRarity.Uncommon,"third atrium candidate uncommon");
            candidate.State=CoderAIState.Subdued;
            candidate.ExitingRoom=false;
            candidate.Position=new(-4.5f,0,6);
            _weapons.Equip(WeaponType.CaptureNet);
            Approach(candidate.CombatCenter); Attack();
            Check(_world.CarriedCoder==candidate && _weapons.CurrentWeapon==WeaponType.BareHands,"net scoops stunned intern");
            _world.SellCarriedCoderToHrVeronika(GrantCash);
            Check(Floor.WingsUnlocked==(sale==3),"quarantine delivery "+sale);
        }
        Check(Floor.Onboarding==OnboardingStep.SeatFirstGalley && _world.Slot.Active &&
            AudioMusicSystem.ActiveClipId=="vo_veronika_slot_spin","third delivery spins and unlocks");
        _playerPos=_world.HrTarotBoothPos+new Vector3(.05f,1.65f,-1.06f);
        LookAtTarget(_world.HrTarotBoothPos+new Vector3(0,1.32f,0));
        for(int i=0;i<72;i++){UpdateSlot(1f/60); _world.PoseVeronika(_playerPos,1f/60);}
        capture("33_veronika_slot_head_spin.png");
        capture("50_veronika_slot_clean_no_cubes.png");
        for(int i=0;i<170;i++) UpdateSlot(1f/60);
        DrawFrame(); // Render the prize mesh through the live reveal pass before accepting.
        var prize=_world.Slot.Prize;
        Key(KeyboardKey.Enter);
        Check(!_world.Slot.Active && Floor.EquippedSkins[(int)FloorSystems.WeaponOrder[prize.Row]]==prize.Skin,"reveal equips reward");
        _world.Slot.Start(Floor,VibeCoderRarity.Legendary,new Random(75),GrantCash);
        Check(_world.Slot.Prize==(3,10),"legendary gold Micro-Uzi fixture");
        for(int i=0;i<267;i++) {
            UpdateSlot(1f/60);
            _world.PoseVeronika(_playerPos,1f/60);
        }
        capture("33b_veronika_slot_jackpot_party.png");
        Key(KeyboardKey.Escape);
        RoundTrip();
        for(int i=0;i<90;i++) TickFloor(1f/60);
        foreach(var (feet,side) in SkyscraperFloor1World.Gates)
            Check(Vector3.Distance(_world.ResolvePlayerCollision(feet),feet)<.01f,"open gate "+feet);
        Check(Floor.WingBarrierSlide==1,"glass retracted");
        foreach(int side in new[]{-1,1})
        {
            _playerPos=new(side*8.5f,1.72f,5.2f);
            for(int i=0;i<25;i++) MovePlayer(1f/60,Vector3.UnitX*side,Vector3.UnitX*side,false,false,false);
            Check(side*_playerPos.X>10,"walk through open portal");
        }

        _world.ActivateMacBookTrap(SubscriptionBaitTier.FreeTier,TrySpendCash,out _);
        var recruit=_world.ActiveCoders.First(c=>c.Archetype==CoderArchetype.JuniorPrompter);
        recruit.Position=new(-6,0,7);
        _weapons.Equip(WeaponType.CaptureNet);
        for(int hit=0;hit<8 && !recruit.CanSignNda;hit++)
        { Approach(recruit.CombatCenter); Attack(); }
        Check(recruit.CanSignNda,"second candidate net capture"); _weapons.Equip(WeaponType.BareHands);
        Approach(recruit.Position+Vector3.UnitY*.45f); Key(KeyboardKey.E);
        _playerPos=gate+new Vector3(1.7f,0,-.68f);
        LookAtTarget(_world.GalleyDesks[0].DeskPosition+Vector3.UnitY*.92f);
        for(int i=0;i<145;i++) { UpdateFrame(1f/60,ref exit); Raylib.PollInputEvents(); }
        capture("32_wings_unlocked_galley.png");
        Approach(_world.GalleyDesks[0].DeskPosition+Vector3.UnitY*.9f*SkyscraperFloor1World.WorkstationScale,1.9f);
        Key(KeyboardKey.E);
        Check(_world.GalleyDesks[0].IsOccupied,"intern payroll");
        Guided(OnboardingStep.Done,"Первый промптер");
        for(int i=1;i<FloorSystems.Weapons.Length;i++)
        {
            int price=FloorSystems.Weapons[i].Price;
            var desk=_world.GalleyDesks[0];
            for(int tick=0;tick<1200 && _playerCash<price;tick++)
            {
                if(desk.IsSlackingInTwitter && Floor.HasWeapon(WeaponType.Belt))
                {
                    _weapons.Equip(WeaponType.Belt);
                    Approach(desk.SeatedFeet+Vector3.UnitY*.77f,1.45f);
                    Attack();
                }
                TickFloor(.1f);
            }
            int before=_playerCash;
            ApplyFloorAction(WeaponBuyOffset+i);
            Check(_weapons.SelectedHotbarSlot==i && _weapons.HotbarSlotCount==Math.Max(4,i+1) &&
                _playerCash==before-price && _weapons.CurrentWeapon==FloorSystems.WeaponOrder[i],"paid "+FloorSystems.Weapons[i].Title);
        }
        Check(Floor.OwnedWeaponsMask==FloorSystems.ClassicMask && VibeCoderCollection.All.Length==24,"full arsenal and catalogue");
        RoundTrip();
        Key(KeyboardKey.N); Key(KeyboardKey.Three);
        Check(LoreAndMenusUI.IsLoreOpen,"frog dossier opens");
        int frogIndex=Array.FindIndex(VibeCoderCollection.All,s=>s.Id=="frog_hopper");
        for(int i=0;i<frogIndex;i++) Key(KeyboardKey.Down);
        Check(LoreAndMenusUI.CollectionEntry(frogIndex)=="Джаба-жаба v0.9","deposited frog dossier");
        for (int frame = 0; frame < 45; frame++) UpdateFrame(1f / 60, ref exit);
        capture("10_java_frog.png");
        Key(KeyboardKey.Down);
        Check(LoreAndMenusUI.CollectionEntry(frogIndex+1).StartsWith("???"),"unsold frog dossier");
        DrawFrame(); Key(KeyboardKey.N);
        var legacy=new GameSaveData(); string oldSlot=Path.GetTempFileName();
        try
        {
            File.WriteAllText(oldSlot,"{}"); ResetNewGameFloor(); RestoreFloorSave(legacy,oldSlot);
            Check(Floor.OwnedWeaponsMask==FloorSystems.ClassicMask && Floor.Onboarding==OnboardingStep.Done,"legacy slot migration");
            var root=System.Text.Json.JsonSerializer.SerializeToNode(MainMenuAndPauseUI.CaptureCurrentGameState!())!;
            var oldShift=System.Text.Json.JsonSerializer.SerializeToNode(new SavedShift(CaptureFloor()),FloorJson)!;
            oldShift["Floor"]!.AsObject().Remove("OnboardingStep");
            root["Shift"]=oldShift; File.WriteAllText(oldSlot,root.ToJsonString());
            pendingFloorSave=null; pendingShift=null;
            ResetNewGameFloor(greet:false); RestoreFloorSave(legacy,oldSlot);
            Check(Floor.Onboarding==OnboardingStep.Done && !CoachingSubtitle,"previous snapshot without onboarding");
        }
        finally { File.Delete(oldSlot); }
        Console.WriteLine("[AUTOTEST] PASS Floor 1: eight gates, locked/open walks, training throw and net spark, four heals, first HR sale and Galley");
        _weapons.UnlockedSawedOff=false;
        _weapons.UnlockedChatGun=true;
        Key(KeyboardKey.T);
        Check(_weapons.CurrentWeapon==WeaponType.ChatGun && _weapons.HotbarSlotCount==4,"boss trophy without a fifth purchase");
        Key(KeyboardKey.T);
        Check(_weapons.CurrentWeapon==WeaponType.BareHands,"holster boss trophy");
        _weapons.UnlockedSawedOff=true;
        _weapons.UnlockedChatGun=false;
        _playerHp=100; Floor.PocketCoffeeCups=0; _playerCash=920;
        var cooler=_world.CoolerPos;
        _playerPos=cooler+new Vector3(1.82f,1.72f,.76f); LookAtTarget(cooler+Vector3.UnitY*.86f);
        Key(KeyboardKey.E);
        Check(Floor.Puddles.Any(x=>x.Position==cooler && x.Wet>0),"spill visible cooler");
        var spill=Floor.Puddles.Single(x=>x.Position==cooler);
        spill.Wet=12;
        _world.Interact(BuildPlayerCamera(),TrySpendCash,GrantCash);
        Check(spill.Wet==12 && _world.FocusPrompt(BuildPlayerCamera())==Loc.CoolerEmpty,"empty jug cannot spill again");
        SetHudBanner("",0); capture("52_cooler_empty_bottle_puddle.png");
        Floor.Tick(12.1f,_world,null);
        Check(_world.FocusPrompt(BuildPlayerCamera())==Loc.CoolerSpill,"dry cooler refills");
        _playerPos=LobbyEntrance; LookAtTarget(_world.HrTarotBoothPos+Vector3.UnitY*1.44f);
        ReceiveDamage(101,"Охрана СБ (Шокер)",cooler);
        Check(_playerHp==0 && _knockoutActive && !Raylib.IsCursorHidden(),"knockout trigger");
        for(int i=0;i<36;i++) UpdateFrame(1f/60,ref exit);
        var fallen=BuildPlayerCamera();
        Check(Math.Abs(fallen.Position.Y-.42f)<.001f && fallen.Up.Y<.96f,"floor fall and roll");
        capture("53_knockout_death_screen.png");
        Key(KeyboardKey.R);
        Check(!_knockoutActive && _playerHp==100 && _playerCash==820 && Floor.PocketCoffeeCups==1 &&
            _playerPos==LobbyEntrance && Raylib.IsCursorHidden(),"wake at reception");
        Console.WriteLine("[AUTOTEST] PASS Guided Floor 1: six steps, live beacons, early skips, save/load and legacy slots");
        Console.WriteLine("[AUTOTEST] PASS Zero-to-Empire: $0, hand and E catches, terminal, net, intern NDA, payroll, four empty slots, five purchased weapons, one-click armed pickup, net scoop and save migration");
    }

    private static void ResetClassicFixture()
    {
        ResetNewGameFloor(greet:false);
        Floor.Onboarding=OnboardingStep.Done; Floor.WingsUnlocked=true; Floor.WingBarrierSlide=1;
        _world.IsMacBookTrapActive=true;
        _world.CoderSpawnTimer=600;
        _world.SpawnFighter(CoderArchetype.JuniorPrompter,new(-8,0,5.5f));
        _world.SpawnFighter(CoderArchetype.CryptoBro,new(12,0,6.5f));
        _world.SpawnFighter(CoderArchetype.TeamLeadMeeter,new(-7,0,-11));
        _world.SpawnFighter(CoderArchetype.SeniorArchitect,new(7,0,-12));
        Floor.OwnedWeaponsMask=FloorSystems.ClassicMask;
        _playerCash=450;
        _weapons.Equip(WeaponType.Belt);
    }

    private static void CheckViewmodelRates()
    {
        Camera3D camera=new() { Target=Vector3.UnitZ, Up=Vector3.UnitY, FovY=76 };
        Vector3[] reference=new Vector3[6];
        float worst=0;
        foreach(int hz in new[]{60,120,144})
        {
            var view=new WeaponAndCombatView();
            view.Inventory.OwnedWeaponsMask=FloorSystems.ClassicMask;
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
            view.Inventory.OwnedWeaponsMask=FloorSystems.ClassicMask;
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
            ResetClassicFixture();
            _playerPos=new(24,1.72f,17);
            // Continuous 60 Hz take from the normal input and rendering paths.
            const string take="motion";
            void Select(KeyboardKey slot)
            {
                Key(slot,true); Tick(1,take); Key(slot,false); Tick(31,take);
            }
            Key(KeyboardKey.Five,true); Tick(); Key(KeyboardKey.Five,false); Tick(45);
            Tick(12,take);
            _weapons.UseRound();
            _weapons.UseRound(); // Stage both spent hulls for the reload.
            Key(KeyboardKey.R,true); Tick(1,take); Key(KeyboardKey.R,false); Tick(100,take);
            Select(KeyboardKey.Four);
            _weapons.UseRound();
            Key(KeyboardKey.R,true); Tick(1,take); Key(KeyboardKey.R,false); Tick(94,take);
            Select(KeyboardKey.Five);
            Key(KeyboardKey.W,true); Key(KeyboardKey.LeftShift,true); Tick(47,take);
            Key(KeyboardKey.Space,true); Tick(1,take); Key(KeyboardKey.Space,false);
            Key(KeyboardKey.LeftShift,false); Key(KeyboardKey.W,false); Tick(66,take);
            Key(KeyboardKey.LeftControl,true); Tick(28,take); Key(KeyboardKey.LeftControl,false); Tick(18,take);
            Key(KeyboardKey.A,true); Tick(22,take); Key(KeyboardKey.A,false);
            Key(KeyboardKey.D,true); Tick(27,take); Key(KeyboardKey.D,false); Tick(21,take);
            Mouse(true); Tick(1,take); Mouse(false); Tick(45,take);
            Select(KeyboardKey.Four);
            Mouse(true); Tick(31,take); Mouse(false); Tick(36,take);
        }

        ResetClassicFixture();
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
        var table=new Vector3(4,0,13); // Pizza table remains vaultable.
        _playerPos=table+new Vector3(1.20f,1.72f,-.28f);
        _playerYaw=-MathF.PI/2;
        Key(KeyboardKey.W,true); Tick(8);
        Require(_playerPos.X>table.X+.90f,"desk side collision");
        Key(KeyboardKey.Space,true); Tick(); Key(KeyboardKey.Space,false); Tick(8);
        Key(KeyboardKey.W,false); Tick(48);
        Require(_isGrounded && Math.Abs(_playerPos.Y-1.72f-.88f)<.01f,"table landing");
        Key(KeyboardKey.S,true);
        for(int i=0;i<18 && _isGrounded;i++) Tick();
        Key(KeyboardKey.S,false);
        Require(!_isGrounded && _coyoteTimer>0,"table edge");
        Key(KeyboardKey.Space,true); Tick(); Key(KeyboardKey.Space,false);
        Require(_playerVelY>7,"coyote jump");
        int descent=0;
        while((_playerVelY>=0 || _playerPos.Y>2.12f) && descent++<90) Tick();
        Key(KeyboardKey.Space,true); Tick(); Key(KeyboardKey.Space,false); Tick(6);
        Require(_playerVelY>5,"buffered landing jump");
        Console.WriteLine($"[AUTOTEST] Pizza table top=0.88m, side collision, vault, coyote and buffered jump PASS");

        ResetClassicFixture();
        // Use the far end of the north hall for the timed magazine test.
        // Responding patrols can interrupt a reload in the central office.
        _playerPos=new(27,1.72f,-34);
        Key(KeyboardKey.Four,true); Tick(); Key(KeyboardKey.Four,false);
        Mouse(true); Tick(60); Mouse(false); Tick();
        int burst=32-_weapons.Ammo;
        Require(burst==10,"held LMB burst: "+burst);
        Key(KeyboardKey.R,true); Tick(); Key(KeyboardKey.R,false);
        Tick(40); Require(_weapons.Reloading && _weapons.Ammo==22,"Uzi R animation");
        capture("reload_uzi.png"); Tick(49); Require(_weapons.Ammo==32 && !_weapons.Reloading,"Uzi refill");
        Key(KeyboardKey.Five,true); Tick(); Key(KeyboardKey.Five,false);
        Mouse(true); Tick(); Mouse(false); Tick(27); Require(_weapons.Ammo==1,"shotgun first shell");
        Mouse(true); Tick(); Mouse(false); Require(_weapons.Ammo==0 && _weapons.Reloading,"shotgun auto reload");
        Tick(43); Require(_weapons.BrassShells.Count(b=>b.IsShotgunShell)==2,"two spent hulls");
        capture("reload_shotgun.png"); Tick(52); Require(_weapons.Ammo==2 && !_weapons.Reloading,"shotgun refill");
        Mouse(true); Tick(); Mouse(false); Tick();
        Key(KeyboardKey.R,true); Tick(); Key(KeyboardKey.R,false); Tick(95);
        Require(_weapons.Ammo==2 && !_weapons.Reloading,"single-shell manual reload");
        Key(KeyboardKey.Four,true); Tick(); Key(KeyboardKey.Four,false);
        Mouse(true); Tick(190); Mouse(false);
        Require(_weapons.Ammo==0 && _weapons.Reloading,$"Uzi empty auto reload (ammo {_weapons.Ammo}, weapon {_weapons.CurrentWeapon}, stun {Floor.HostStun}, panel {floorPanel})");
        Tick(89); Require(_weapons.Ammo==32,"empty Uzi refill");
        Console.WriteLine($"[AUTOTEST] Uzi held 1s={burst} shots, R 22->32; shotgun 2->1->0->2, two ejected hulls");

        ResetClassicFixture();
        var fighters=_world.ActiveCoders.Where(c=>c.IsCombatCoder).ToArray();
        Require(fighters.Select(c=>c.MaxHp).SequenceEqual(new float[]{50,90,100,145}),"fighter HP by role");
        var enemy=fighters[0];
        enemy.BaitTaken=true;
        // Keep the test lane clear of the lounge sofa and architectural pillars on the open central runner.
        _playerPos=new(-1.2f,1.72f,5.2f);
        enemy.Position=new(-1.2f,0,10.0f);
        Key(KeyboardKey.LeftControl,true); // Stay quiet while measuring the fighter’s attack cadence.
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
        Key(KeyboardKey.LeftControl,false);
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
            (WeaponType.Belt,2,fighters[1]),(WeaponType.SolderingIron,2,fighters[2]),(WeaponType.SawedOff,2,fighters[3]) })
        {
            _playerPos=new(23,1.72f,17); target.Position=new(23,0,15.15f);
            _weapons.SelectSlot((int)weapon);
            for(int i=0;i<hits;i++)
            {
                LookAtTarget(target.CombatCenter);
                Mouse(true); Tick(); Mouse(false); Tick(29);
                if(weapon==WeaponType.SawedOff) Require(target.Hp==(i==0 ? 25 : 0),$"buckshot against senior, shot {i+1}, HP {target.Hp}");
                else Require(target.Hp>0 && (target.Fear>6 || target.State==CoderAIState.Subdued),"non-lethal pressure "+weapon);
            }
            target.Position=new(26,0,-28);
        }
        var wallTarget=_world.SpawnCoderForTier(SubscriptionBaitTier.FreeTier);
        wallTarget.Position=new(0,0,-2);
        _playerPos=new(0,1.72f,2.8f);
        LookAtTarget(wallTarget.CombatCenter);
        _weapons.SelectSlot(3);
        float wallHp=wallTarget.Hp;
        Mouse(true); Tick(); Mouse(false);
        Require(wallTarget.Hp==wallHp && Math.Abs(_weapons.BulletTracers[^1].End.Z-(_world.SouthElevatorDoorPos.Z+.15f))<.02f,"wall stops ray and tracer");
        Console.WriteLine("[AUTOTEST] Jump dodge, recoil recovery, wall impact, non-lethal melee and lethal shotgun PASS");
        Console.WriteLine($"[AUTOTEST] Fighters={fighters.Length} HP={enemy.MaxHp} cube={pr.Velocity.Length():F0}m/s damage=7 slap=6 knockback={knockback:F2}m KO={shots} rounds cash=+35 XP=+7");
    }

    private static unsafe void CheckDisplayAndStart(string path)
    {
        bool exit=false;
        void Input(uint type,int a,int b=0)
        {
            AutomationEvent e=new() { Type=type }; e.Params[0]=a; e.Params[1]=b; Raylib.PlayAutomationEvent(e);
        }
        void Click(int x,int y)
        {
            Input(7,x,y); Input(6,0); UpdateFrame(1f/60,ref exit); Raylib.PollInputEvents();
            Input(5,0); Raylib.PollInputEvents();
        }
        void Require(bool value,string step)
        { if(!value) throw new InvalidDataException("Display/start: "+step); }
        try
        {
            MainMenuAndPauseUI.LoadDisplay(path,false);
            Require(MainMenuAndPauseUI.Display==new DisplaySettings(),"default");
            Click(150,(int)MainMenuAndPauseUI.MainButton(3).Y+18);
            Require(MainMenuAndPauseUI.IsSettingsPanelOpen,"main menu settings");
            Click(818,238);
            Require(Loc.IsEn && DisplaySettings.Load(path).Language=="en","English pill");
            foreach(var (label,english) in new[]{
                ("Играть","Play"),("Новая игра (Соло / Хост 1–3)","New Game (Solo / Host 1-3)"),
                ("Подключиться к другу (Кооп 1–3)","Join Friend (Co-op 1-3)"),("Загрузить сохранение","Load Save Slot")})
                Require(Loc.RenderText(label)==english,"English Play panel: "+label);
            PressTestKey(KeyboardKey.Left);
            Require(!Loc.IsEn && DisplaySettings.Load(path).Language=="ru","Russian keyboard selection");
            for(int mode=0;mode<2;mode++)
            {
                Click(770,347);
                for(int resolution=1;resolution<=5;resolution++)
                {
                    Click(770,404);
                    var expected=new DisplaySettings(mode==0 ? DisplayMode.Windowed : DisplayMode.Fullscreen,resolution%5);
                    Require(DisplaySettings.Load(path)==expected && MainMenuAndPauseUI.Display==expected,"persist selectors");
                }
            }
            Click(380,650);
            Click(150,(int)MainMenuAndPauseUI.MainButton(0).Y+18);
            Click((int)MainMenuAndPauseUI.PlayChoiceButton(0).X+80,(int)MainMenuAndPauseUI.PlayChoiceButton(0).Y+22);
            Require(comic.Active,"play opens intro comic on new game");
            PressTestKey(KeyboardKey.Escape);
            Require(!comic.Active && !MainMenuAndPauseUI.IsMenuBlockingGameplay && Raylib.IsCursorHidden() && _world.ActiveCoders.Count==0,"Escape skips comic straight into fresh floor");
            MainMenuAndPauseUI.OpenMainMenu();
            Input(2,(int)KeyboardKey.Enter); UpdateFrame(1f/60,ref exit); Raylib.PollInputEvents();
            Input(1,(int)KeyboardKey.Enter); Raylib.PollInputEvents();
            Input(2,(int)KeyboardKey.Enter); UpdateFrame(1f/60,ref exit); Raylib.PollInputEvents();
            Input(1,(int)KeyboardKey.Enter); Raylib.PollInputEvents();
            Require(comic.Active,"Enter opens intro comic on new game");
            PressTestKey(KeyboardKey.Escape);
            Require(!comic.Active && !MainMenuAndPauseUI.IsInMainMenu && Raylib.IsCursorHidden(),"Enter + Escape starts floor");
            Console.WriteLine("[AUTOTEST] PASS clean 5-button menu, intro comic on New Game, and instant Escape skip");
            var before=_playerPos;
            Input(2,(int)KeyboardKey.W); UpdateFrame(1f/60,ref exit); Raylib.PollInputEvents();
            Input(1,(int)KeyboardKey.W); Raylib.PollInputEvents();
            Require(Vector3.Distance(before,_playerPos)>.05f,"movement on next frame");
            PressTestKey(KeyboardKey.Escape);
            PressTestKey(KeyboardKey.Four);
            Require(MainMenuAndPauseUI.IsPaused && MainMenuAndPauseUI.IsSettingsPanelOpen,"pause settings");
            PressTestKey(KeyboardKey.Right);
            MainMenuAndPauseUI.LoadDisplay(path,false);
            Require(Loc.IsEn && MainMenuAndPauseUI.Display.ResolutionIndex==0,"reload English in pause");
            DrawFrame();
            PressTestKey(KeyboardKey.Enter);
            Require(!Loc.IsEn && DisplaySettings.Load(path).Language=="ru","pause Enter restores Russian");
            PressTestKey(KeyboardKey.Escape);
            PressTestKey(KeyboardKey.Escape);
            Console.WriteLine("[AUTOTEST] PASS RU/EN mouse, keyboard, settings reload, main menu and pause");
            Net.Dispose();
            Require(Raylib.IsWindowState(ConfigFlags.HiddenWindow) && Raylib.GetRenderWidth()==1280 && Raylib.GetRenderHeight()==720 &&
                VisualEngine3D.MatchesViewport(1280,720),"fixed hidden test viewport");
        }
        finally { File.Delete(path); }
    }

    private static void CheckDamageLadder()
    {
        ResetClassicFixture();
        int[] expected=[6,12,24,42,18,120];
        for(int i=0;i<expected.Length;i++)
        {
            var target=_world.SpawnFighter(i==0 ? CoderArchetype.SecurityGuard : CoderArchetype.SeniorArchitect,new(24,0,17));
            _weapons.Equip(i==0 ? WeaponType.BareHands : FloorSystems.WeaponOrder[i-1]);
            _playerPos=target.Position+new Vector3(0,1.72f,1.65f);
            LookAtTarget(target.CombatCenter);
            _weaponFireCooldown=0;
            UpdateTrigger(1f/60,true,true,false,false);
            if(target.HitFlinchTimer!=.24f || target.KnockbackVel.LengthSquared()==0 || target.Hp!=145-expected[i] || _weapons.FloatingTexts.LastOrDefault().Text!="-"+expected[i] || _weapons.HitmarkerTimer<=0)
                throw new InvalidDataException("LMB damage: "+_weapons.CurrentWeapon+" HP "+target.Hp);
            var before=target.Position;
            _world.UpdateWorld(.04f,_playerPos,false,null,null);
            if(target.HitFlinchTimer is <=0 or >=.24f || Vector3.Dot(target.Position-before,target.HitDirection)<=0)
                throw new InvalidDataException("Enemy recoil impulse: "+_weapons.CurrentWeapon);
            _world.ActiveCoders.Remove(target);
        }
        _playerPitch=_recoilPitch=_recoilYaw=_shake=0; // Measure roll from a level view.
        var neutral=BuildPlayerCamera();
        var right=Vector3.Normalize(Vector3.Cross(neutral.Target-neutral.Position,Vector3.UnitY));
        foreach(int side in new[]{-1,1})
        {
            ReceiveDamage(1,"Impact",_playerPos+side*right);
            var hit=BuildPlayerCamera();
            if(MathF.Abs(side*Vector3.Dot(hit.Up-neutral.Up,right)+MathF.Sin(5.5f*MathF.PI/180))>.002f || hit.Target.Y>=neutral.Target.Y || _weapons.HitJolt!=1)
                throw new InvalidDataException($"Directional camera and hands flinch: side={side}, roll={Vector3.Dot(hit.Up-neutral.Up,right)}, pitch={hit.Target.Y-neutral.Target.Y}, hands={_weapons.HitJolt}, dir={_playerHitDir}, hp={_playerHp}");
            TickHitReaction(.11f);
            if(_weapons.HitJolt is <=0 or >=1) throw new InvalidDataException("Hands flinch decay");
            TickHitReaction(.11f);
            if(_weapons.HitJolt!=0 || BuildPlayerCamera().Up!=neutral.Up) throw new InvalidDataException("Flinch recovery");
        }
        Console.WriteLine("[AUTOTEST] PASS directional player flinch, hands recovery and six weapon recoil impulses");
        ResetNewGameFloor();
        _world.ActivateMacBookTrap(SubscriptionBaitTier.FreeTier,TrySpendCash,out _);
        var intern=_world.ActiveCoders.Single(c=>c.TimidIntern);
        intern.Position=new(24,0,17);
        _playerPos=new(24,1.72f,18.6f);
        for(int hit=1;hit<=4;hit++)
        {
            LookAtTarget(intern.CombatCenter); _weaponFireCooldown=0;
            UpdateTrigger(1f/60,true,true,false,false);
            if(intern.Hp!=24-6*hit || intern.CanSignNda!=(hit==4) || _weapons.FloatingTexts[^1].Text!="-6")
                throw new InvalidDataException("Four-punch intern capture");
        }
        LookAtTarget(intern.Position+Vector3.UnitY*.45f);
        _world.Interact(BuildPlayerCamera(),TrySpendCash,GrantCash);
        Floor.PocketFrogs.Add("frog_hopper");
        _playerPos=_world.HrTarotBoothPos+new Vector3(0,1.72f,-2.2f);
        LookAtTarget(_world.HrTarotBoothPos+Vector3.UnitY*1.6f);
        if(_world.FocusPrompt(BuildPlayerCamera())!="Сдать кадра HR Веронике") throw new InvalidDataException("HR candidate priority");
        _world.Interact(BuildPlayerCamera(),TrySpendCash,GrantCash);
        if(_world.CarriedCoder!=null || Floor.PocketFrogs.Count!=1 || _playerCash<=0 ||
            _world.FocusPrompt(BuildPlayerCamera())!="Сдать Джаба-жаб Веронике") throw new InvalidDataException("HR tarot payout");
        if(Floor.WingsUnlocked || _world.TotalCodersSoldToHr!=1)
            throw new InvalidDataException("First HR delivery must keep quarantine");
        int wallet=_playerCash;
        _world.Interact(BuildPlayerCamera(),TrySpendCash,GrantCash);
        if(_playerCash<=wallet || Floor.PocketFrogs.Count!=0) throw new InvalidDataException("HR frog deposit");
        Console.WriteLine("[AUTOTEST] PASS LMB hits and damage numbers: 6, 12, 24, 42, 18, 8 x 15");
    }

    private static void FinishElevatorIntro()
    {
        for(int i=0;i<145 && !_world.BossInCombat;i++) TickFloor(1f/30);
        if(!_world.BossInCombat) throw new InvalidDataException("Elevator did not release the agent");
    }

    private static void CheckElevatorReveal(Action<string> capture)
    {
        static void Check(bool ok,string what)
        {
            if(!ok) throw new InvalidDataException("Elevator reveal: "+what);
        }
        ResetNewGameFloor(greet:false);
        Floor.Onboarding=OnboardingStep.Done; Floor.WingsUnlocked=true; Floor.WingBarrierSlide=1;
        var north=_world.NorthElevatorPos;
        void FrameDoors()
        {
            _playerPos=north+new Vector3(0,1.72f,-7.1f);
            LookAtTarget(north+Vector3.UnitY*2.1f);
        }
        void AimPanel()
        {
            _playerPos=_world.ElevatorButtonPos+new Vector3(0,1.72f,-2);
            LookAtTarget(_world.ElevatorButtonPos+Vector3.UnitY*1.22f);
        }
        string Use() => _world.Interact(BuildPlayerCamera(),TrySpendCash,GrantCash);
        string Prompt() => _world.FocusPrompt(BuildPlayerCamera());
        bool exit=false;
        void Frame() => UpdateFrame(1f/60,ref exit);
        Frame();
        Check(_menuFloor.BrokenElevator && !_menuFloor.BossModelVisible && _menuFloor.ElevatorDoorOpenAmount==0,"menu sealed");
        Check(_world.BrokenElevator && !_world.BossModelVisible && !BossFightMusic && !_world.BossRevealed &&
            _world.ElevatorGlassOpacity==1 && _world.ElevatorDoorOpenAmount==0 && _world.BossHudOpacity==0 &&
            LoreAndMenusUI.IsChatRedacted(4) && LoreAndMenusUI.IsChatRedacted(5) &&
            AudioMusicSystem.SceneMusicFile=="music_office_90s.wav","floor sealed");
        FrameDoors();
        _world.AdvanceLighting(.105f);
        capture("21_broken_elevator_secret.png");
        AimPanel();
        const string noCard="Замок СБ не пускает. Купи дубликат Ключ-карты СБ ($1000) у Вероники [E] или отбери у СБ-шника.";
        Check(Prompt()=="Щиток сгорел и заперт СБ — нужны Паяльник ($90) и Ключ-карта СБ" && Use()==noCard,"both requirements missing");
        _playerCash=2090;
        Floor.BuyWeapon(2,TrySpendCash);
        Check(Prompt()=="Замок СБ — купи Ключ-карту ($1000) у Вероники или отбери у СБ" && Use()==noCard &&
            _world.BrokenElevator && _playerCash==2000 && !_world.HasElevatorKey,"cash cannot open the reader");
        OpenKeycardRow();
        capture("14_arsenal.png");
        _playerCash=999;
        PressTestKey(KeyboardKey.Enter);
        Check(!_world.HasElevatorKey && _playerCash==999,"unaffordable card");
        _playerCash=2000;
        PressTestKey(KeyboardKey.Enter);
        Check(_world.HasElevatorKey && _playerCash==1000 && AudioMusicSystem.ActiveVoiceText=="Вероника: "+FloorSystems.KeycardPurchaseLine,"buy duplicate at HR");
        PressTestKey(KeyboardKey.Enter);
        Check(_playerCash==1000 && panelMessage.Contains("уже получена"),"single purchase");
        PressTestKey(KeyboardKey.Escape);
        AimPanel();
        Check(Prompt()=="Перепаять щиток и приложить Ключ-карту СБ","ready to repair");
        _repairMinigame.Open();
        _repairMinigame.SetPreviewStateForScreenshot(2);
        Check(_repairMinigame.IsActive,"repair minigame active");
        capture("73_elevator_repair_minigame.png");
        _repairMinigame.Close();
        Check(!_repairMinigame.IsActive,"repair minigame closed");
        Use();
        Check(_world.ElevatorBoss.State==ElevatorBossState.Repairing && _world.ElevatorRepaired && _world.HasElevatorKey &&
            _playerCash==1000 && _world.ElevatorGlassOpacity==1 && _world.ElevatorDoorOpenAmount==0 &&
            AudioMusicSystem.ActiveSpeaker=="Петрович","panel repair and warning");
        Use();
        FrameDoors();
        var cab=_world.ElevatorBoss.Position;
        Check(cab==north+new Vector3(0,.16f,1.35f),"inside shaft");
        float opacity=1;
        bool sawSilhouette=false;
        for(int i=0;_world.ElevatorBoss.State==ElevatorBossState.Repairing;i++)
        {
            Check(i<145 && !_world.BossInCombat && !_world.BossRevealed && !BossFightMusic &&
                _world.ElevatorGlassOpacity<=opacity && _world.ElevatorDoorOpenAmount==0 && _world.ElevatorBoss.Position==cab &&
                _world.BossHudOpacity==0 && AudioMusicSystem.SceneMusicFile=="music_office_90s.wav","sealed during repair");
            opacity=_world.ElevatorGlassOpacity;
            if(i<=55) Check(!_world.BossModelVisible,"hidden for first 40 percent");
            sawSilhouette|=_world.BossModelVisible && opacity is >0 and <.65f;
            if(i==70)
            {
                float remaining=_world.ElevatorBoss.StateTimer;
                var state=System.Text.Json.JsonSerializer.Deserialize<FloorSnapshot>(
                    System.Text.Json.JsonSerializer.Serialize(CaptureFloor(),FloorJson),FloorJson)!;
                ApplyElevator(state);
                Check(_world.ElevatorRepaired && _world.ElevatorGlassOpacity==opacity && _world.ElevatorBoss.StateTimer==remaining && _world.ElevatorBoss.Position==cab,
                    "mid-repair snapshot");
            }
            if(i==111) capture("70_cutscene_shot1_doors.png");
            Frame();
        }
        Check(sawSilhouette && opacity<.01f && _world.ElevatorGlassOpacity==0 && _world.BossRevealed && BossFightMusic &&
            !LoreAndMenusUI.IsChatRedacted(4) && !LoreAndMenusUI.IsChatRedacted(5) &&
            AudioMusicSystem.SceneMusicFile=="music_boss_fight.wav" && _world.ElevatorBoss.State==ElevatorBossState.IntroSpeech && AudioMusicSystem.ActiveVoiceText==SkyscraperFloor1World.AgentQuarantineLine,"reveal cue");
        float door=0;
        for(int i=0;i<94;i++)
        {
            Frame();
            if(i==5) Check(_world.BossHudOpacity is >0 and <1,"HUD fades in");
            Check(_world.ElevatorDoorOpenAmount>=door && !_world.BossInCombat,"intro motion");
            door=_world.ElevatorDoorOpenAmount;
            if(i==62) capture("71_cutscene_shot2_hero_reveal.png");
            if(i==92) capture("72_cutscene_shot3_weapon_stepout.png");
        }
        Check(door==1 && _world.BossHudOpacity==1 && _world.ElevatorBoss.Position.Z<north.Z && _world.ElevatorBoss.ShieldHp>0,"agent outside open doors");
        capture("22_elevator_repair_reveal.png");
        FinishElevatorIntro();
        Check(_world.ElevatorBoss.State==ElevatorBossState.ChargingPlayer,"charge after walkout");
        // Live visual QC captures: monumental Boss Bar (with ghost drain & shield) + Combat Feedback + Clever Escape Pursuit Dossier
        {
            var boss = _world.ElevatorBoss;
            boss.Phase = 2;
            boss.Hp = 500f;
            boss.ShieldHp = 135f;
            DrawFrame();
            boss.Hp = 420f;
            boss.ShieldHp = 92f;
            _playerHp = 52;
            Floor.PocketCoffeeCups = 2;
            TriggerDirectionalHitIndicator(boss.Position + new Vector3(4.2f, 0f, 1.4f), 26);
            TriggerHitmarker(crit: true, kill: false);
            TriggerCrosshairBloom(6f);
            capture("74_boss_hud_combat_feedback.png");

            _world.BossEscapedPursuit = true;
            Floor.FinalePending = true;
            Floor.ShutdownTime = 0f;
            OpenFloorPanel(FloorPanel.BossFinale);
            capture("75_boss_escape_pursuit_dossier.png");
            OpenFloorPanel(FloorPanel.None);
        }
        foreach(bool miniboss in new[]{false,true})
        {
            ResetNewGameFloor(greet:false);
            Floor.Onboarding=OnboardingStep.Done; Floor.WingsUnlocked=true; Floor.WingBarrierSlide=1;
            var feet=new Vector3(24,0,17);
            var carrier=miniboss ? _world.SpawnCoderForTier(SubscriptionBaitTier.CloudEntropicMax) :
                _world.SpawnFighter(CoderArchetype.SecurityGuard,feet);
            carrier.Position=feet;
            carrier.Hp=6;
            _playerPos=feet+new Vector3(0,1.35f,1.1f);
            LookAtTarget(carrier.CombatCenter);
            if(miniboss)
            {
                Floor.BuyWeapon(3,_=>true);
                _weapons.Equip(WeaponType.MicroUzi);
            }
            _world.ProcessPlayerAttack(BuildPlayerCamera(),_weapons,GrantCash,out _);
            Check(_world.HasElevatorKey && Floor.Notice==SkyscraperFloor1World.KeycardDrop &&
                (miniboss ? carrier.Hp==0 && !carrier.KnockedOutAlive : carrier.CanSignNda && _playerCash==0),"free original from "+carrier.Archetype);
            AimPanel();
            Check(Prompt()=="Щиток сгорел — купи Паяльник ($90) у Вероники" && Use()=="Ключ-карта есть, но щиток сгорел. Купи Паяльник ($90) у Вероники [E]." &&
                _world.BrokenElevator,"card still needs an iron");
        }
        Console.WriteLine("[AUTOTEST] PASS elevator: two shop gates, single $1000 purchase, free guard and SaaS cards, repair, reveal and combat");
    }

    private static void CheckOfficeRoutes()
    {
        // Walk the central floor with the real player footprint. Half-metre neighbours
        // cannot jump a wall expanded by the player's 40 cm collision radius.
        var seen=new HashSet<(int X,int Z)>();
        var work=new Queue<(int X,int Z)>();
        var reachable=new List<Vector3>();
        work.Enqueue((1,14));
        while(work.TryDequeue(out var cell))
        {
            if(Math.Abs(cell.X)>42 || Math.Abs(cell.Z)>46 || !seen.Add(cell)) continue;
            Vector3 feet=new(cell.X*.5f,0,cell.Z*.5f);
            if(Vector3.DistanceSquared(feet,_world.ResolvePlayerCollision(feet))>.00001f) continue;
            reachable.Add(feet);
            work.Enqueue((cell.X-1,cell.Z)); work.Enqueue((cell.X+1,cell.Z));
            work.Enqueue((cell.X,cell.Z-1)); work.Enqueue((cell.X,cell.Z+1));
        }
        void Approach(Vector3 position,float range)
        {
            if(!reachable.Any(p=>Vector3.DistanceSquared(p,position)<=range*range))
                throw new InvalidDataException("Office route blocked at "+position);
        }
        foreach(var station in new[]{SkyscraperFloor1World.TerminalPos,SkyscraperFloor1World.CoffeePos,
            _world.CentralMacBookPos,_world.RackPos,_world.ExtinguisherPos,_world.CoolerPos,
            _world.HrTarotBoothPos,_world.ElevatorButtonPos}) Approach(station,1.85f);
        if(PausePlayerCount!="1/3") throw new InvalidDataException("Solo HUD badge");
        foreach(var cooler in _world.Coolers)
        {
            Vector3 tap=cooler+Vector3.UnitY*.90f;
            bool usable=reachable.Any(feet=>{
                if(Vector3.DistanceSquared(feet,cooler)>1.75f*1.75f) return false;
                var eye=feet+Vector3.UnitY*1.72f;
                var ray=tap-eye;
                var cam=new Camera3D { Position=eye,Target=tap,Up=Vector3.UnitY,FovY=67 };
                return _world.PelletDistance(eye,Vector3.Normalize(ray),ray.Length())>=ray.Length()-.52f &&
                    _world.FocusPrompt(cam)==Loc.CoolerSpill;
            });
            if(!usable) throw new InvalidDataException("No reachable dispenser tap at "+cooler);
            if(!_world.CoolerHasBacking(cooler)) throw new InvalidDataException("Unbacked cooler "+cooler);
        }
        void Blocked(Vector3 probe,float feet)
        {
            if(Vector3.DistanceSquared(probe,_world.ResolvePlayerCollision(probe,.40f,feet))<.01f)
                throw new InvalidDataException($"Missing collision at {probe}, height {feet}");
        }
        for(int angle=0;angle<360;angle++)
        {
            float t=angle*MathF.PI/180;
            Vector3 dir=new(MathF.Cos(t),0,MathF.Sin(t));
            var probe=_world.CentralElevatorCorePos+dir*(4.51f/Math.Max(Math.Abs(dir.X),Math.Abs(dir.Z)));
            Blocked(probe,0);
            Blocked(probe,SkyscraperFloor1World.CeilingHeight-.4f);
        }
        for(int i=0;i<SkyscraperFloor1World.RoomDoors.Length;i++)
        {
            var door=SkyscraperFloor1World.RoomDoors[i];
            float s=SkyscraperFloor1World.RoomExitSign(i);
            for(float z=0;z<=2.1f;z+=.18f)
                foreach(int side in new[]{-1,1}) Blocked(door+new Vector3(side*1.22f,0,-s*z),2.5f);
            for(float x=-1.20f;x<=1.20f;x+=.18f) Blocked(door+new Vector3(x,0,-s*2.1f),2.5f);
            // Verify player is ALWAYS blocked from crossing the threshold even when RoomOpen[i] == 1f!
            float prev=_world.RoomOpen[i]; _world.RoomOpen[i]=1f;
            Blocked(door+new Vector3(0,0,s*.06f),0f);
            _world.RoomOpen[i]=prev;
        }
        for(float z=-8.5f;z<=.5f;z+=.125f)
            foreach(int side in new[]{-1,1})
                for(float x=6.24f;x<=7.66f;x+=.10f)
                {
                    Vector3 p=new(side*x,0,z);
                    if(Vector3.DistanceSquared(p,_world.ResolvePlayerCollision(p))>.00001f)
                        throw new InvalidDataException("Narrow shaft aisle "+p);
                }
        Console.WriteLine("[AUTOTEST] PASS 360 shaft perimeter, four enclosed pods, 2.2m aisles, backed reachable coolers, solo HUD");
        foreach(var desk in _world.GalleyDesks) Approach(desk.ChairPosition,1.15f);
        foreach(var aisle in new Vector3[]{new(0,0,8),new(-11,0,5),new(11,0,5),new(0,0,-10)})
            Approach(aisle,.51f);
    }

    private static void CheckRoomSpawns()
    {
        var world=new SkyscraperFloor1World(67);
        var observer=new Vector3(0,1.72f,32);
        void Tick(int frames)
        {
            for(int i=0;i<frames;i++) world.UpdateWorld(1f/60,observer,false,null,null);
        }
        var visited=new HashSet<int>();
        for(int attempt=0;attempt<48 && visited.Count<4;attempt++)
        {
            world.Systems.WingsUnlocked=attempt>=2;
            var coder=world.SpawnCoderForTier(SubscriptionBaitTier.FreeTier,attempt%2==0 ? -1 : 1);
            int room=coder.EntryRoom;
            if(!world.Systems.WingsUnlocked && room>=2) throw new InvalidDataException("Locked wing spawn");
            if(visited.Add(room))
            {
                var door=SkyscraperFloor1World.RoomDoors[room];
                float s=SkyscraperFloor1World.RoomExitSign(room);
                if(room<2 && Vector3.Distance(door,world.CentralMacBookPos) is <5.0f or >12f) throw new InvalidDataException("Atrium run distance");
                if((coder.Position.Z-door.Z)*s>=0 || Math.Abs(coder.Position.X-door.X)>.5f)
                    throw new InvalidDataException("Coder spawned outside alcove");
                Tick(75);
                if((coder.Position.Z-door.Z)*s<=0 || world.RoomOpen[room]<.99f)
                    throw new InvalidDataException("Coder failed to walk through open door");
                for(int frame=0;frame<1750 && coder.State==CoderAIState.WalkingToBait;frame++) Tick(1);
                if(coder.State!=CoderAIState.Subdued || Vector3.Distance(coder.Position,world.CentralMacBookPos+Vector3.UnitX*1.15f)>.36f)
                    throw new InvalidDataException($"Room {room} route ended at {coder.Position} in {coder.State}");
                Tick(205);
                if(world.RoomOpen[room]!=0) throw new InvalidDataException("Door failed to close");
            }
            world.ActiveCoders.Clear();
        }
        if(visited.Count!=4) throw new InvalidDataException("Four floor entrances required");
        for(int i=0;i<world.VentTime.Length;i++)
        {
            var frog=world.SpawnFrog(i,"frog_hopper");
            if(frog.Feet.Z>=world.VentMouth(i).Z || world.VentTime[i]<=0)
                throw new InvalidDataException("Frog must start behind vent flap");
            Tick(54);
            if(frog.Feet.Z<=world.VentMouth(i).Z+.55f || frog.Feet.Y!=0 || world.VentTime[i]!=0)
                throw new InvalidDataException("Vent exit failed");
        }
        for(float z=.8f;z<11.9f;z+=.2f)
        for(float x=-2.4f;x<=2.4f;x+=.2f)
        {
            var point=new Vector3(x,0,z);
            if(Vector3.DistanceSquared(point,world.ResolvePlayerCollision(point,.02f))>.00001f)
                throw new InvalidDataException($"Obstructed elevator runner at {point}");
        }
        Console.WriteLine("[AUTOTEST] PASS four glass exits and bait routes, three vent hops, clear 4.8m runner");
    }

    private static void CheckLivingOffice(Action<string,Camera3D?> capture)
    {
        ResetClassicFixture();
        CheckOfficeRoutes();
        var clutter=_world.LooseClutter;
        if(clutter.Count is <24 or >64) throw new InvalidDataException("Office clutter count");
        _playerPos=new(-11.2f,1.72f,3.45f);
        var start=clutter.Select(c=>c.Feet).ToArray();
        MovePlayer(1f/60,-Vector3.UnitZ,-Vector3.UnitZ,false,true,false);
        _world.UpdateLooseClutter(1f/60);
        if(!clutter.Where((c,i)=>Vector3.DistanceSquared(c.Feet,start[i])>.00001f && c.Feet.Y>0).Any())
            throw new InvalidDataException("Sprint failed to lift printer litter");
        for(int i=0;i<480;i++) _world.UpdateLooseClutter(1f/60);
        if(clutter.Any(c=>c.Velocity!=Vector3.Zero)) throw new InvalidDataException("Clutter did not settle");

        Floor.Onboarding=OnboardingStep.BuyNet;
        _world.UpdateIntercom(80);
        if(AudioMusicSystem.VoiceTimer>0) throw new InvalidDataException("PA during onboarding");
        Floor.Onboarding=OnboardingStep.Done; Floor.WingsUnlocked=true; Floor.WingBarrierSlide=1;
        FloorSystems.SpeakCoaching("Проверка связи");
        _world.UpdateIntercom(65);
        if(!CoachingSubtitle) throw new InvalidDataException("PA interrupted coaching");
        AudioMusicSystem.VoiceTimer=0;
        _world.UpdateIntercom(.01f);
        if(AudioMusicSystem.ActiveSpeaker!="SYSTEM ZERO") throw new InvalidDataException("Silent intercom");
        AudioMusicSystem.VoiceTimer=0;

        Camera3D overview=new(){Position=new(-8.15f,4.35f,13.23f),Target=new(1.1f,2.55f,8.55f),
            Up=Vector3.UnitY,FovY=74,Projection=CameraProjection.Perspective};
        string[] assets=["prop_pendant_lamp","prop_poster_frame","prop_pizza_stack","prop_server_cart"];
        VisualEngine3D.CapturedAssets=new();
        try
        {
            capture("23_living_office_overview.png",overview);
            foreach(string asset in assets)
                if(!VisualEngine3D.HasAsset(asset) || !VisualEngine3D.CapturedAssets.Contains(asset))
                    throw new InvalidDataException("Office asset missing from colour pass: "+asset);
        }
        finally { VisualEngine3D.CapturedAssets=null; }
        _playerPos=new(4.60f,1.39f,2.25f);
        LookAtTarget(new(7.05f,.94f,5.56f));
        _weapons.Equip(WeaponType.SawedOff);
        _weapons.UpdateWeapons(.4f,BuildPlayerCamera());
        start=clutter.Select(c=>c.Feet).ToArray();
        UpdateTrigger(1f/60,true,true,false,false);
        for(int i=0;i<12;i++)
        {
            _world.UpdateLooseClutter(1f/60);
            UpdateTrigger(1f/60,false,false,false,false);
        }
        if(_weapons.Ammo!=1 || !clutter.Where((c,i)=>c.Feet.Y>.12f && Vector3.Distance(c.Feet,start[i])>.14f).Any())
            throw new InvalidDataException("Shot failed to scatter B12 litter");
        capture("24_living_office_chaos.png",null);
        void CheckBurst(Vector3 source,Action discharge)
        {
            for(int i=0;i<360;i++) _world.UpdateLooseClutter(1f/60);
            var nearest=clutter.MinBy(c=>Vector3.DistanceSquared(c.Feet,source))
                ?? throw new InvalidDataException("No office clutter in fixture");
            Vector3 rest=nearest.Feet;
            discharge();
            _world.UpdateLooseClutter(1f/60);
            if(nearest.Feet.Y<=rest.Y) throw new InvalidDataException("Office discharge at "+source);
        }
        CheckBurst(SkyscraperFloor1World.CoffeePos,()=>Floor.Steam=8);
        CheckBurst(_world.RackPos,()=>Floor.RackSmoke=9);
        var puddle=clutter[0].Feet;
        CheckBurst(puddle,()=>Floor.Puddles.Add(new(){Position=puddle,Wet=18,Electric=6}));
        Console.WriteLine("[AUTOTEST] PASS Living office: navigation, four rendered GLBs, 30 loose items, sprint, shotgun, steam, CO2, electric discharge, settling and intercom priority");
    }

    private static void CheckSkinExhibits()
    {
        foreach(var model in FloorSystems.WeaponModels)
        {
            var finishes=new HashSet<string>();
            for(int id=0;id<WeaponSkins.Names.Length;id++)
            {
                var texture=LoreAndMenusUI.RenderShopExhibit(model,28,.5f,id);
                var image=Raylib.LoadImageFromTexture(texture);
                try
                {
                    var pixels=new List<byte>();
                    for(int y=1;y<image.Height;y+=3)
                        for(int x=1;x<image.Width;x+=3)
                        {
                            var p=Raylib.GetImageColor(image,x,y);
                            if(p.A<200) continue;
                            pixels.Add(p.R); pixels.Add(p.G); pixels.Add(p.B);
                        }
                    if(pixels.Count<90 || !finishes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels.ToArray()))))
                        throw new InvalidDataException($"Indistinguishable weapon exhibit: {model} skin {id}");
                }
                finally {Raylib.UnloadImage(image);}
            }
        }
        Console.WriteLine("[AUTOTEST] PASS Russo One and Onest atlases, 65 GPU weapon exhibits");
    }

    private static unsafe void RecordCutsceneVideo()
    {
        MainMenuAndPauseUI.IsInMainMenu = false;
        ResetNewGameFloor(greet: false);
        Floor.Onboarding = OnboardingStep.Done;
        Floor.WingsUnlocked = true;
        Floor.HasRadio = true;
        Floor.WingBarrierSlide = 1f;
        Floor.OwnedWeaponsMask = FloorSystems.ClassicMask;
        _playerCash = 450;
        _weapons.SelectSlot(4);
        _world.HasElevatorKey = true;
        _playerPos = _world.NorthElevatorPos + new Vector3(0.06f, 1.72f, -1.85f);
        LookAtTarget(_world.NorthElevatorPos + new Vector3(0f, 1.75f, 0.3f));
        _weapons.UpdateWeapons(0.4f, BuildPlayerCamera());
        _world.TrySummonElevatorBoss(out _);
        BossIntroCutscene.Reset();

        string outMp4 = @"C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a\scratch\cutscene_video_only.mp4";
        Directory.CreateDirectory(Path.GetDirectoryName(outMp4)!);
        var psi = new System.Diagnostics.ProcessStartInfo("ffmpeg",
            $"-y -v error -f rawvideo -pix_fmt rgba -s {ScreenWidth}x{ScreenHeight} -r 30 -i - -c:v libx264 -preset medium -crf 17 -pix_fmt yuv420p \"{outMp4}\"")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };
        using var ff = System.Diagnostics.Process.Start(psi)!;
        var stdin = ff.StandardInput.BaseStream;
        byte[] buf = new byte[ScreenWidth * ScreenHeight * 4];
        const float frameDt = 1f / 30f;

        void CaptureFrame(float simDt)
        {
            BossIntroCutscene.SyncState(_world);
            TickFloor(simDt);
            if (BossIntroCutscene.ShouldRelocatePlayerAfterKnockback(_world))
            {
                _playerPos = BossIntroCutscene.KnockedBackStandPosition(_world);
                LookAtTarget(_world.ElevatorBoss.Position + Vector3.UnitY * 1.68f);
            }
            BossIntroCutscene.SyncState(_world);
            DrawFrame();
            Image img = Raylib.LoadImageFromScreen();
            try
            {
                System.Runtime.InteropServices.Marshal.Copy((IntPtr)img.Data, buf, 0, buf.Length);
                stdin.Write(buf, 0, buf.Length);
            }
            finally { Raylib.UnloadImage(img); }
        }

        int repFrames = 0;
        while (_world.ElevatorBoss.State == ElevatorBossState.Repairing && repFrames < 95)
        {
            CaptureFrame(frameDt);
            repFrames++;
        }
        int introFrames = 0;
        while (_world.ElevatorBoss.State == ElevatorBossState.IntroSpeech && introFrames < 520)
        {
            CaptureFrame(frameDt * 0.165f);
            introFrames++;
        }
        for (int i = 0; i < 62; i++)
            CaptureFrame(frameDt);

        stdin.Flush();
        stdin.Close();
        ff.WaitForExit();
        Console.WriteLine($"[RECORD] repFrames={repFrames} introFrames={introFrames} total={repFrames + introFrames + 62} exit={ff.ExitCode}");
    }

    private static void RunAutomatedScreenshotSuite(string displayPath,bool polishOnly)
    {
        CheckViewmodelRates();
        AudioMusicSystem.CheckMovementSounds();
        AudioMusicSystem.CheckStudioVoices();
        GameFonts.CheckBundle();
        CheckSkinExhibits();
        var collection=new FloorSystems(); var machine=new SlotRoulette();
        foreach(var w in FloorSystems.WeaponOrder) collection.SkinMasks[(int)w]=0x1ffe;
        int credit=0;
        machine.Start(collection,VibeCoderRarity.Legendary,new Random(4),(n,_)=>credit+=n);
        if(WeaponSkins.Names.Length!=13 || credit!=machine.Cashback || credit is <75 or >400 || machine.Prize.Skin<7)
            throw new InvalidDataException("Duplicate skin cashback");
        AudioMusicSystem.VoiceTimer=0;
        CheckElevatorNameplates();
        LoreAndMenusUI.CheckMixerControls();
        string projectScreenshotsDir = Environment.GetEnvironmentVariable("VIBECODER_SCREENSHOTS")
            ?? Path.Combine(Directory.GetCurrentDirectory(), "screenshots");
        string binScreenshotsDir = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(projectScreenshotsDir);
        Directory.CreateDirectory(binScreenshotsDir);

        void CaptureShot(string fileName, string? detailName = null, Rectangle detail = default, bool cropOnly=false,Camera3D? sceneCamera=null,bool hideHud=false)
        {
            if(!Loc.IsEn && !sceneCamera.HasValue)
            {
                Loc.Set(GameLang.En,false);
                try { Loc.CheckRenderLanguage=true; DrawFrame(); }
                finally { Loc.CheckRenderLanguage=false; Loc.Set(GameLang.Ru,false); }
            }
            // Render 3 warmup frames so all animations/particles/UI settle cleanly
            for (int f = 0; f < 3; f++)
            {
                if(sceneCamera is {} cam)
                {
                    Raylib.BeginDrawing();
                    BeginLitWorld(_world,cam);
                    Raylib.EndMode3D();
                    VisualEngine3D.Composite();
                    Raylib.EndDrawing();
                }
                else DrawFrame(showHud:!cropOnly && !hideHud);
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

        void CapturePolish()
        {
            MainMenuAndPauseUI.OpenMainMenu();
            ResetNewGameFloor(greet:false);
            MainMenuAndPauseUI.IsInMainMenu=false;
            Vector3 booth=_world.HrTarotBoothPos;
            Camera3D Shot(Vector3 eye,Vector3 target,float fov) => new() { Position=eye,Target=target,Up=Vector3.UnitY,FovY=fov };
            FloorSystems.SpeakCoaching(FloorSystems.ShopGreeting);
            _world.AdvanceLighting(.9f);
            CaptureShot("25_veronika_animated_closeup.png",sceneCamera:Shot(booth+new Vector3(.32f,1.70f,-1.61f),
                booth+new Vector3(-.06f,1.38f,-.06f),36));
            OpenFloorPanel(FloorPanel.Upgrades);
            _playerCash=240;
            Floor.BuyWeapon(0,_=>true);
            Floor.BuyWeapon(1,_=>true);
            floorRow=3;
            CaptureShot("26_shop_3d_showcase.png");
            _weapons.UnlockedMicroUzi=true;
            Floor.SkinMasks[(int)WeaponType.MicroUzi]=(1<<8)|(1<<10)|(1<<11);
            PressTestKey(KeyboardKey.R);
            if(Floor.EquippedSkins[(int)WeaponType.MicroUzi]!=8) throw new InvalidDataException("Shop next finish");
            PressTestKey(KeyboardKey.Q);
            if(Floor.EquippedSkins[(int)WeaponType.MicroUzi]!=0) throw new InvalidDataException("Shop previous finish");
            Floor.EquipSkin(WeaponType.MicroUzi,10);
            CaptureShot("34_shop_skin_gold_uzi.png");
            Loc.Set(GameLang.En,false);
            try
            {
                CaptureShot("36_en_shop_briefcase.png");
                CaptureShot("38_living_office_props_overview.png",sceneCamera:Shot(new(17,3.2f,-3.6f),new(5,.94f,6.5f),74));
            }
            finally { Loc.Set(GameLang.Ru,false); }
            sidegrades=true; floorRow=9;
            CaptureShot("26b_shop_upgrades_tab.png");
            OpenFloorPanel(FloorPanel.None);
            CaptureShot("27_window_city_ground.png",sceneCamera:Shot(new(-37.55f,1.72f,2),new(-69,-42,-8),63));
            var station=_world.GalleyDesks[0].DeskPosition;
            _playerPos=station+new Vector3(1.65f,1.72f,1.95f);
            LookAtTarget(station+new Vector3(0,1.04f,-.15f));
            CaptureShot("28_workstation_human_scale.png",hideHud:true);
            Floor.BuyWeapon(0,_=>true);
            _weapons.Equip(WeaponType.CaptureNet);
            if(_weapons.CurrentWeapon!=WeaponType.CaptureNet) throw new InvalidDataException("Net grip fixture");
            for(int i=0;i<60;i++) _weapons.UpdateWeapons(1f/60,BuildPlayerCamera());
            _playerPos=booth+new Vector3(1,1.72f,-4);
            LookAtTarget(booth+Vector3.UnitY*1.3f);
            CaptureShot("29_net_grip_fixed.png",hideHud:true);
            Floor.EquipSkin(WeaponType.MicroUzi,11);
            _weapons.Equip(WeaponType.MicroUzi);
            _playerPos=new(3,1.68f,9);
            LookAtTarget(new(-4,1.54f,2));
            for(int i=0;i<60;i++) _weapons.UpdateWeapons(1f/60,BuildPlayerCamera());
            AudioMusicSystem.VoiceTimer=0;
            CaptureShot("35_uzi_holo_fps.png");
        }
        if(polishOnly) { CapturePolish(); return; }

        // 0. 00_main_menu_classic.png - Classic PC Main Menu over live 3D Skyscraper Floor 1
        ResetClassicFixture();
        MainMenuAndPauseUI.IsInMainMenu = true;
        MainMenuAndPauseUI.IsPaused = false;
        MainMenuAndPauseUI.IsSaveLoadPanelOpen = false;
        MainMenuAndPauseUI.IsModsPanelOpen = false;
        MainMenuAndPauseUI.IsSettingsPanelOpen = false;
        _world.ActivateMacBookTrap(SubscriptionBaitTier.CursorPro, _ => true, out _);
        CaptureShot("00_main_menu_classic.png");
        CaptureShot("46_clean_main_menu_5buttons.png");
        // Live 3D Character Wardrobe verification + dedicated screenshot (76_character_wardrobe_3d.png)
        {
            var probe = new CharacterAppearance { SkinIndex = 3, OutfitIndex = 5, EyeIndex = 7, HatIndex = 8 };
            var unpacked = CharacterAppearance.Unpack(probe.Pack());
            if (unpacked.SkinIndex != 3 || unpacked.OutfitIndex != 5 || unpacked.EyeIndex != 7 || unpacked.HatIndex != 8)
                throw new InvalidDataException("CharacterAppearance pack/unpack mismatch");
            if (new CharacterAppearance().Pack() != 0)
                throw new InvalidDataException("Default CharacterAppearance must pack to 0");

            MainMenuAndPauseUI.IsCharacterWardrobeOpen = true;
            MainMenuAndPauseUI.WardrobeYawOffset = 0.08f;
            CharacterAppearance.Current.SkinIndex = 1;   // Загорелая
            CharacterAppearance.Current.OutfitIndex = 4; // Белый «Сочи-98»
            CharacterAppearance.Current.EyeIndex = 7;    // Пиксельные
            CharacterAppearance.Current.HatIndex = 3;    // Корона Бургер-Босса
            CaptureShot("76_character_wardrobe_3d.png");
            PressTestKey(KeyboardKey.Escape);
            if (MainMenuAndPauseUI.IsCharacterWardrobeOpen)
                throw new InvalidDataException("Escape failed to close Character Wardrobe");
            CharacterAppearance.Current.CopyFrom(new CharacterAppearance());
            MainMenuAndPauseUI.WardrobeYawOffset = 0f;
            Console.WriteLine("[AUTOTEST] PASS 3D Character Wardrobe: pack/unpack, live 3D stage, Escape close");
        }
        CheckDisplayAndStart(displayPath);
        // Switch into active 3D gameplay
        MainMenuAndPauseUI.IsInMainMenu = false;
        LoreAndMenusUI.IsLoreOpen = false;
        LoreAndMenusUI.IsSettingsOpen = false;
        LoreAndMenusUI.IsCaseOpen = false;

        MainMenuAndPauseUI.IsPaused = true;
        CaptureShot("00_pause.png");
        MainMenuAndPauseUI.IsSettingsPanelOpen = true;
        CaptureShot("07_menu_settings.png");
        MainMenuAndPauseUI.IsSettingsPanelOpen = false;
        MainMenuAndPauseUI.IsModsPanelOpen = true;
        CaptureShot("07_mods.png");
        MainMenuAndPauseUI.IsModsPanelOpen = false;
        MainMenuAndPauseUI.IsPaused = false;
        ResetNewGameFloor(greet:false);
        MainMenuAndPauseUI.IsInMainMenu=false;
        _world.ActivateMacBookTrap(SubscriptionBaitTier.FreeTier,_=>true,out _);
        for(int i=0;i<72;i++) _world.UpdateWorld(1f/60,new(1,2,12),false,null,null);
        CaptureShot("44_atrium_clear_coworking_doors.png",sceneCamera:new Camera3D {
            Position=new(2.62f,2.85f,8.45f),Target=new(-4.65f,1.22f,2.7f),Up=Vector3.UnitY,FovY=67 });
        void View(string file,Vector3 eye,Vector3 target,float fov) =>
            CaptureShot(file,sceneCamera:new Camera3D { Position=eye,Target=target,Up=Vector3.UnitY,FovY=fov });
        View("47_elevator_side_solid_check.png",new(-8.58f,1.76f,-1.89f),new(-4.60f,1.82f,-5.17f),74);
        View("48_side_corridor_furnished_check.png",new(7.11f,1.72f,.68f),new(5.92f,1.27f,-6.68f),72);
        // Inspect the entire perimeter and the back and both sides of every spawn room.
        // Keep the diagnostic contact sheet outside the four full-resolution deliverables.
        void CapturePerimeterAudit()
        {
            var sheet=Raylib.GenImageColor(1280,900,Color.Black);
            int frame=0;
            void Sample(Vector3 eye,Vector3 target)
            {
                Camera3D cam=new(){Position=eye,Target=target,Up=Vector3.UnitY,FovY=75};
                Raylib.BeginDrawing(); BeginLitWorld(_world,cam); Raylib.EndMode3D();
                VisualEngine3D.Composite(); Raylib.EndDrawing();
                var image=Raylib.LoadImageFromScreen();
                try { Raylib.ImageDraw(ref sheet,image,new(0,0,image.Width,image.Height),
                    new((frame%4)*320,(frame/4)*180,320,180),Color.White); }
                finally { Raylib.UnloadImage(image); }
                frame++;
            }
            try
            {
                for(int i=0;i<8;i++)
                {
                    float angle=i*MathF.PI/4;
                    Vector3 dir=new(MathF.Cos(angle),0,MathF.Sin(angle));
                    var eye=_world.CentralElevatorCorePos+dir*(7.45f/Math.Max(Math.Abs(dir.X),Math.Abs(dir.Z)));
                    if(dir.Z>.5f && Math.Abs(dir.X)>.5f) eye.Z=.68f; // Stay in front of the spawn pods.
                    Sample(eye+Vector3.UnitY*2.3f,_world.CentralElevatorCorePos+Vector3.UnitY*2.1f);
                }
                for(int i=0;i<SkyscraperFloor1World.RoomDoors.Length;i++)
                {
                    var door=SkyscraperFloor1World.RoomDoors[i];
                    float s=SkyscraperFloor1World.RoomExitSign(i);
                    foreach(var offset in new Vector3[]{new(-2.8f,2.2f,s*2.2f),new(0,2.2f,s*4.2f),new(2.8f,2.2f,s*2.2f)})
                        Sample(door+offset,door+new Vector3(0,1.4f,0));
                }
                string path=Path.Combine(Path.GetTempPath(),"vibecoder-perimeter-audit.png");
                if(!Raylib.ExportImage(sheet,path)) throw new IOException(path);
            }
            finally { Raylib.UnloadImage(sheet); }
        }
        CapturePerimeterAudit();
        _world.ElevatorDoorOpenAmount=1;
        View("49_elevator_front_cabin_check.png",new(-.84f,1.65f,-14.34f),new(.2f,2.06f,-7.8f),67);
        _world.ElevatorDoorOpenAmount=0;
        var vent=_world.VentMouth(0);
        var hopper=_world.SpawnFrog(0,"frog_hopper");
        for(int i=0;i<25;i++) _world.UpdateWorld(1f/60,vent+Vector3.UnitZ*3,false,null,null);
        _world.SpawnFrog(0,"frog_syntax");
        for(int i=0;i<21;i++) _world.UpdateWorld(1f/60,vent+Vector3.UnitZ*3,false,null,null);
        _playerPos=hopper.Feet+new Vector3(-.34f,.50f,.58f);
        LookAtTarget(vent+new Vector3(0,.15f,.69f));
        _weapons.Equip(WeaponType.BareHands);
        CaptureShot("45_new_frogs_floor_scale.png",hideHud:true);
        CheckFloorHunt(name=>CaptureShot(name));
        CheckRoomSpawns();
        CheckLivingOffice((name,cam)=>CaptureShot(name,sceneCamera:cam,hideHud:true));
        CheckElevatorReveal(name=>CaptureShot(name));
        CheckZeroToEmpire(name=>CaptureShot(name));
        CheckDamageLadder();
        CheckSprintA(name=>CaptureShot(name));
        CheckSprintB(name=>CaptureShot(name));
        CheckFpsGameplay(name=>{ _hudStatusTimer=0; CaptureShot(name); });

        // 1. 01_office_hub_hr_tarot.png - Central White Shark Reception opposite the Elevator: HR Tarot Veronika with crystal ball & candles, Belt in crimson jacket hands
        ResetClassicFixture();
        MainMenuAndPauseUI.IsInMainMenu = false;
        _weapons.SelectSlot(1); // Father's Leather Belt
        _playerPos = new Vector3(0.0f, 1.72f, 10.15f);
        LookAtTarget(new Vector3(0.0f, 1.28f, 13.0f));
        _weapons.UpdateWeapons(0.45f, BuildPlayerCamera());
        AudioMusicSystem.TriggerVoiceLine("HR-ТАРОЛОГ ВЕРОНИКА", "Расклад Таро готов! Приноси вайб-кодеров на разбор натальной карты!", (int)SpeakerVoiceType.VeronikaTarot);
        CaptureShot("01_office_hub_hr_tarot.png","crop_slot_1_belt.png",new(307,452,715,214));

        // 2. 02_macbook_trap_ambush.png - Ambush behind Leather Sofa/Ficus, glowing MacBook Trap + Central Armored Elevator Shaft + Left/Right Forks + Walking Vibe Coder with Anxiety Bar
        ResetClassicFixture();
        MainMenuAndPauseUI.IsInMainMenu = false;
        _weapons.SelectSlot(2); // 90s Soldering Iron with rising rosin smoke
        _isCrouching = true;
        _eyeHeight=1.16f;
        _playerPos = _world.CentralMacBookPos+new Vector3(2.6f,_eyeHeight,2.05f);
        LookAtTarget(_world.CentralMacBookPos+Vector3.UnitY*.77f);
        _world.ActivateMacBookTrap(SubscriptionBaitTier.CursorPro, _ => true, out _);
        var walkingCoder = _world.SpawnCoderForTier(SubscriptionBaitTier.CursorPro, forceSide: -1);
        walkingCoder.Position = _world.CentralMacBookPos+new Vector3(1.55f,0,-.55f);
        walkingCoder.FacingYaw = .46f;
        walkingCoder.Anxiety = 38f;
        walkingCoder.SpeechBubble = "Ого, халявный Табсор Про на ЯблоБуке! Бегу жать Accept All!";
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
        ResetClassicFixture();
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
        if (_weapons.Ammo!=31 || hpBeforeShot-miniBoss.Hp != 18 || !_weapons.HitmarkerIsCrit ||
            _weapons.FloatingTexts.LastOrDefault().Text != "-18")
            throw new InvalidDataException("Critical hit on the scaled coder did not produce the expected damage number");
        UpdateTrigger(.012f,false,false,false,false);
        _hudStatusTimer = AudioMusicSystem.VoiceTimer = 0;

        CaptureShot("03_uzi_combat_saas_miniboss.png","crop_slot_3_uzi.png",new(670,317,442,370));

        // 4. 04_hr_tarot_payout_and_galley.png - Selling captured coder to HR Tarot Veronika (+$$$ Cash Burst) + Working & Slacking Coders at Galley Desks
        ResetClassicFixture();
        MainMenuAndPauseUI.IsInMainMenu = false;
        _weapons.SelectSlot(1);
        _world.CarriedCoder = new VibeCoderEntity
        {
            Id = 99,
            Archetype = CoderArchetype.SaasGuruMiniBoss,
            Name = "Гуру Табсора ($60K MRR)",
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
        ResetClassicFixture();
        MainMenuAndPauseUI.IsInMainMenu = false;
        _weapons.SelectSlot(4); // Sawed-Off Shotgun / Chat-Gun
        _weapons.UpdateWeapons(.3f,BuildPlayerCamera());
        _world.HasElevatorKey = true;
        _world.TrySummonElevatorBoss( out _);
        FinishElevatorIntro();
        _world.ElevatorBoss.Position = new Vector3(0.0f,0.0f,-10.35f);
        _world.ElevatorDoorOpenAmount=.92f;
        _world.ElevatorBoss.Hp = 595f;
        _world.ElevatorBoss.ShieldHp=0; _world.ElevatorBoss.ShieldDelay=5;
        _world.ElevatorBoss.FacingYaw = 3.14159f; // Facing North toward player in North Boss Arena
        _world.ElevatorBoss.State = ElevatorBossState.ThrowingNodeModules;
        _world.ElevatorBoss.StateTimer = 2.4f;
        _world.ElevatorBoss.ThrowCycleDuration = 0.95f;
        _world.ElevatorBoss.AttackSubTimer = 0.24f; // Peak explosive throw windup!
        _world.ElevatorBoss.LookTarget = new Vector3(-1.45f, 1.72f, -14.85f);
        _world.ElevatorBoss.SnapPose = true;
        _world.SpawnNodeModulesCube(new Vector3(3.35f, 1.15f, -8.5f), new Vector3(0f, 1.5f, -18.5f));
        _world.SpawnNodeModulesCube(new Vector3(-3.2f, 2.55f, -9.5f), new Vector3(0f, 1.5f, -18.5f));
        _playerPos = new Vector3(-1.45f, 1.72f, -14.85f);
        LookAtTarget(_world.ElevatorBoss.Position+new Vector3(.15f,2.05f,0));
        UpdateTrigger(1f/60,true,true,false,false);
        if (_weapons.Ammo!=1 || _world.ElevatorBoss.HitFlinchTimer!=.24f || _world.ElevatorBoss.Hp>=480) throw new InvalidDataException("Shotgun boss shot failed");
        UpdateTrigger(.018f,false,false,false,false);
        // Keep clean Phase 2 active throw-coil pose (no shield cage clutter, no stray damage label over elevator dial)
        _world.ElevatorBoss.ShieldHp = 0;
        _world.ElevatorBoss.State = ElevatorBossState.ThrowingNodeModules;
        _world.ElevatorBoss.ThrowCycleDuration = 0.95f;
        _world.ElevatorBoss.AttackSubTimer = 0.24f;
        _world.ElevatorBoss.LookTarget = _playerPos;
        _world.ElevatorBoss.SnapPose = true;
        _hudStatusTimer = 0;
        _lastDamageTimer = 0;
        _playerHitAnimTimer = 0;
        // Tactical flanking partner at 3.23m from boss cleanly on burgundy carpet border (100% clear of left desk!)
        StagePartner(new(1.62f,0,-13.15f),_world.ElevatorBoss.Position+Vector3.UnitY*1.55f);
        CaptureShot("05_elevator_boss_escaped_agent.png","crop_slot_4_shotgun.png",new(605,350,520,370));

        // 5b. Live 10-part boss action animation proof captures — OPEN BURGUNDY CARPET CAMERAS (clear of Z=-15.8 partition wall!)
        {
            var boss = _world.ElevatorBoss;
            _world.Partner = null;
            _world.EnemyProjectiles.Clear();
            boss.HitFlinchTimer = 0f;
            boss.HitFlashTimer = 0f;
            boss.HitDirection = Vector3.Zero;
            boss.ShieldHp = 0f;
            boss.Position = new Vector3(0.0f, 0.0f, -10.8f);

            // 67_boss_action_charge.png: Ferocious juggernaut bull-rush sprint (unobstructed 3/4 view!)
            boss.State = ElevatorBossState.ChargingPlayer;
            boss.StateTimer = 1.6f;
            boss.FacingYaw = MathF.PI - 0.14f;
            boss.StridePhase = 1.45f;
            boss.LookTarget = new Vector3(-2.15f, 1.54f, -14.75f);
            boss.SnapPose = true;
            _world.AdvanceLighting(0.05f);
            CaptureShot("67_boss_action_charge.png", sceneCamera: new Camera3D
            {
                Position = new Vector3(-2.15f, 1.54f, -14.75f),
                Target = new Vector3(0.0f, 1.48f, -10.8f),
                Up = Vector3.UnitY,
                FovY = 58f
            });

            // 68_boss_action_throw.png: Cocked-back pitcher throw windup + spinning crimson node_modules core
            boss.State = ElevatorBossState.ThrowingNodeModules;
            boss.StateTimer = 2.2f;
            boss.FacingYaw = MathF.PI + 0.10f;
            boss.ThrowCycleDuration = 0.95f;
            boss.AttackSubTimer = 0.24f;
            boss.LookTarget = new Vector3(2.10f, 1.58f, -14.70f);
            boss.SnapPose = true;
            _world.SpawnNodeModulesCube(new Vector3(-2.45f, 2.25f, -10.4f), new Vector3(2.10f, 1.5f, -18.5f));
            _world.AdvanceLighting(0.05f);
            CaptureShot("68_boss_action_throw.png", sceneCamera: new Camera3D
            {
                Position = new Vector3(2.10f, 1.58f, -14.70f),
                Target = new Vector3(0.0f, 1.52f, -10.8f),
                Up = Vector3.UnitY,
                FovY = 58f
            });

            // 69_boss_action_stun.png: Wall-crash damaged glitching stagger (unobstructed 3/4 view!)
            _world.EnemyProjectiles.Clear();
            boss.State = ElevatorBossState.StunnedWallCrash;
            boss.StateTimer = 1.85f;
            boss.FacingYaw = MathF.PI - 0.16f;
            boss.LookTarget = new Vector3(-2.05f, 1.46f, -14.65f);
            boss.SnapPose = true;
            _world.AdvanceLighting(0.11f);
            CaptureShot("69_boss_action_stun.png", sceneCamera: new Camera3D
            {
                Position = new Vector3(-2.05f, 1.46f, -14.65f),
                Target = new Vector3(0.0f, 1.32f, -10.8f),
                Up = Vector3.UnitY,
                FovY = 58f
            });

            // 60_boss_combat_fire_lightning.png: Roaring volumetric Arena Fire + Luminous Boss Firewall Shield with Forked 3D Lightning!
            boss.State = ElevatorBossState.ThrowingNodeModules;
            boss.ShieldHp = EscapedAgentBoss.ShieldCapacity;
            boss.Position = new Vector3(0.0f, 0.0f, -11.4f);
            boss.FacingYaw = MathF.PI;
            boss.LookTarget = new Vector3(0.0f, 1.65f, -19.2f);
            boss.SnapPose = true;
            _world.BossArenaChaos = 1.0f;
            _world.AdvanceLighting(0.08f);
            CaptureShot("60_boss_combat_fire_lightning.png", sceneCamera: new Camera3D
            {
                Position = new Vector3(-1.35f, 1.92f, -19.6f),
                Target = new Vector3(0.0f, 1.45f, -11.4f),
                Up = Vector3.UnitY,
                FovY = 66f
            });
            _world.BossArenaChaos = 0f;
            boss.ShieldHp = 0f;

            // 77_solder_puddle_lightning.png: Live Soldering Iron 3D Forked Lightning + Electrified Water Puddle + Electro-Shocked Enemy!
            ResetClassicFixture();
            MainMenuAndPauseUI.IsInMainMenu = false;
            _weapons.UnlockedSolderingIron = true;
            _weapons.Equip(WeaponType.SolderingIron);
            Vector3 puddlePos = new(-1.2f, 0f, 3.2f);
            Floor.Puddles.Clear();
            Floor.Puddles.Add(new WaterSpill { Position = puddlePos, Wet = 18f, Electric = 6f });
            if (_world.ActiveCoders.Count > 0)
            {
                var shocked = _world.ActiveCoders[0];
                shocked.Position = puddlePos + new Vector3(0.45f, 0f, -0.35f);
                shocked.State = CoderAIState.PanickedFleeing;
                shocked.StunTimer = 2.5f;
                shocked.ElectroShockedTimer = 4.0f;
            }
            _playerPos = new Vector3(0.85f, 1.72f, 5.65f);
            LookAtTarget(puddlePos + Vector3.UnitY * 0.65f);
            for (int f = 0; f < 10; f++) _weapons.UpdateWeapons(1f / 60f, BuildPlayerCamera());
            _weapons.TriggerWeaponAttack(BuildPlayerCamera(), puddlePos + Vector3.UnitY * 0.35f, hitEnemy: true, damageDealt: 42, isCritical: true);
            _world.AdvanceLighting(0.06f);
            CaptureShot("77_solder_puddle_lightning.png");
        }

        // 6. 06_lore_chats_window_n.png - Open Corporate Leak & Slack/TG Chats Window [N] (pre-elevator spoiler-free gate!)
        ResetClassicFixture();
        MainMenuAndPauseUI.IsInMainMenu = false;
        LoreAndMenusUI.IsLoreOpen = true;
        LoreAndMenusUI.ActiveLoreTab = 0;
        LoreAndMenusUI.SelectArchiveEntryForPreview(4);
        LoreAndMenusUI.IsSettingsOpen = false;
        LoreAndMenusUI.IsCaseOpen = false;
        CaptureShot("06_lore_chats_window_n.png");
        LoreAndMenusUI.ActiveLoreTab = 1;
        CaptureShot("06_hr_dossier.png");
        LoreAndMenusUI.ActiveLoreTab = 3;
        CaptureShot("06_dictionary.png");
        LoreAndMenusUI.ActiveLoreTab = 0;
        LoreAndMenusUI.IsLoreOpen = false;
        CheckArchiveControls(name => CaptureShot(name));

        // 7. 07_settings_mixer_and_mods.png (and 07_settings_mixer_and_cases.png) - Settings / Audio Mixer [F8]
        LoreAndMenusUI.IsLoreOpen = false;
        LoreAndMenusUI.IsSettingsOpen = true;
        LoreAndMenusUI.IsCaseOpen = false;
        CaptureShot("07_settings_mixer_and_mods.png");
        CapturePolish();

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
        ResetClassicFixture();
        Raylib.DisableCursor();
        static void Check(bool ok,string step)
        {
            if(!ok) throw new InvalidDataException("Sprint A: "+step);
        }
        Check(VibeCoderCollection.All.Select(s=>s.Id).Distinct().Count()==24,"unique species IDs");
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
            return _world.Interact(BuildPlayerCamera(),TrySpendCash,GrantCash,_weapons.CurrentWeapon);
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
        capture("15_contracts.png");
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
        Check(junior.State==CoderAIState.Subdued && junior.Hp==junior.MaxHp-24 && junior.LaptopLost,"belt damage and live NDA");
        before=Floor.SEC;
        _weapons.Equip(WeaponType.BareHands);
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
        var water=cooler+new Vector3(1.45f,0,-.8f);
        At(cooler+new Vector3(2.80f,1.72f,-1.30f),water);
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
        _weapons.Equip(WeaponType.BareHands);
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
        foreach(var gear in Enum.GetValues<Sidegrade>().Take(6)) Floor.Buy((int)gear,Spend);
        Check(cash==4000-FloorSystems.GearList.Take(6).Sum(g=>g.Price) && Floor.Shell==Sidegrade.Negotiator,"six paid sidegrades");
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
        _weapons.SelectOwnedSlot(_weapons.SelectedHotbarSlot);
        Use(senior.Position,.45f); Use(_world.GalleyDesks[2].DeskPosition,.9f*SkyscraperFloor1World.WorkstationScale);
        Floor.Tick(.01f,_world,null);
        Check(Floor.Report is {Job:1,Success:true} && _world.GalleyDesks[2].TechDebtRate<0,"architect stealth delivery");
        Floor.Start(2,_world); Floor.Deliver(CoderArchetype.CryptoBro,false,250); Floor.Tick(.01f,_world,null);
        Check(Floor.Report is {Job:2,Success:true},"fintech delivery");
        ResetClassicFixture();
        Floor.Start(3,_world); Use(SkyscraperFloor1World.DeployPos,1.12f);
        Check(Floor.ReleaseStarted && Floor.Friday==45,"Friday contract armed");
        for(int i=0;i<451;i++)
        {
            var threat=_world.ActiveCoders.Where(c=>c.IsCombatCoder && c.Hp>0 && c.State!=CoderAIState.Subdued && (c.ReleaseAttacker || c.Aggro))
                .OrderByDescending(c=>c.ReleaseAttacker).ThenBy(c=>Vector3.DistanceSquared(c.Position,_playerPos)).FirstOrDefault();
            if(threat!=null && i%4==0)
            {
                At(threat.Position+new Vector3(0,1.72f,1.8f),threat.CombatCenter);
                Strike(WeaponType.SawedOff,threat.CombatCenter);
            }
            TickFloor(.1f);
        }
        Check(Floor.Report is {Job:3,Success:true,Bonus:>=2.5f},"Friday survival: "+Floor.Report?.Diagnosis+" / "+Floor.Defense+" / "+Floor.Friday);
        Floor.Start(4,_world);
        Check(Floor.JobId==4 && !_world.ElevatorBoss.IsActive,"contract awaits physical repair");
        _world.HasElevatorKey=true;
        Use(_world.ElevatorButtonPos,1.22f);
        FinishElevatorIntro();
        Check(_world.BossInCombat,"quarantine contract encounter");
        _world.ElevatorBoss.Hp=1; _world.ElevatorBoss.Position=new(24,0,17);
        At(new(24,1.72f,20),new(24,1.35f,17)); Strike(WeaponType.MicroUzi,new(24,1.35f,17));
        for(int i=0;i<20 && !_world.ElevatorBoss.IsDefeated;i++) TickFloor(.1f);
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
            ResetClassicFixture(); RestoreFloorSave(restoreData,scratch);
            Check(Floor.Report==expected.Report && Floor.SEC==expected.SEC && Floor.PT==expected.PT &&
                Floor.TD==expected.TD && Floor.Serial==expected.Serial && _world.ElevatorRepaired && _world.ElevatorGlassOpacity==0,"saved shift round-trip");
        }
        finally { File.Delete(scratch); }
        ResetClassicFixture();
        var rack=_world.RackPos;
        var near=_world.ActiveCoders[2]; near.Position=rack+new Vector3(2,0,0);
        At(rack+new Vector3(0,1.72f,2.5f),rack+Vector3.UnitY);
        Strike(WeaponType.SolderingIron,rack+Vector3.UnitY);
        Check(Floor.RackHeat>0 && Floor.PT>=12 && near.State==CoderAIState.PanickedFleeing,"physical B12 overheat and evacuation");
        float hot=Floor.PT; Use(rack,1.2f);
        Check(Floor.RackHeat==0 && Floor.PT<hot,"physical B12 cool-down");
        Floor.IncidentNow(Incident.Printer,_world); Use(printer.Position,.9f);
        Check(Floor.Jam>0 && Floor.PrinterWait==0,"jam blocks pass");
        Floor.IncidentNow(Incident.Audit,_world);
        Check(Floor.Raid && Floor.Audit==0 && _world.ActiveCoders.Any(c=>c.RaidGuard),"live inspection");
        int balance;
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
        ResetClassicFixture();
        Console.WriteLine("[AUTOTEST] Sprint A PASS: risk, four profiles, NDA, payroll, parry, electro-puddle, Friday, five contracts, debrief and sidegrades");
    }
}
