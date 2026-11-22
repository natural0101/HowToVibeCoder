#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Raylib_cs;

namespace HowToVibeCoder;

public static partial class Program
{
    private sealed record PlaythroughMusicSegment(string File, double Start, double End, float Volume);
    private sealed record PlaythroughAudioCue(double Time, string File, float Volume, string Kind, double? MaxDuration = null, double FadeOut = 0.0);
    private sealed record PlaythroughChapter(string Name, double Start, double End);

    private sealed class PlaythroughAudioTimeline
    {
        public int Fps { get; init; } = 30;
        public int TotalFrames { get; set; }
        public double TotalDuration { get; set; }
        public string SoundsDir { get; init; } = "";
        public List<PlaythroughMusicSegment> MusicSegments { get; } = new();
        public List<PlaythroughAudioCue> Cues { get; } = new();
        public List<PlaythroughChapter> Chapters { get; } = new();
    }

    private static unsafe void RecordFullPlaythrough()
    {
        const int fps = 30;
        const float frameDt = 1f / fps;
        string scratchDir = @"C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a\scratch";
        Directory.CreateDirectory(scratchDir);
        string outMp4 = Path.Combine(scratchDir, "playthrough_video_only.mp4");
        string cuesJson = Path.Combine(scratchDir, "playthrough_audio_cues.json");

        var timeline = new PlaythroughAudioTimeline
        {
            Fps = fps,
            SoundsDir = AudioMusicSystem.SoundsDir
        };

        int frameIndex = 0;
        double Now() => frameIndex / (double)fps;

        string? activeMusicFile = null;
        double activeMusicStart = 0;
        float activeMusicVol = 0.28f;

        void SwitchMusic(string? file, float volume = 0.28f)
        {
            double t = Now();
            if (activeMusicFile != null && t > activeMusicStart + 0.05)
                timeline.MusicSegments.Add(new(activeMusicFile, Math.Round(activeMusicStart, 3), Math.Round(t, 3), activeMusicVol));
            activeMusicFile = file;
            activeMusicStart = t;
            activeMusicVol = volume;
        }

        void Cue(string file, float volume = 0.90f, string kind = "sfx", double? maxDuration = null, double fadeOut = 0.0)
        {
            timeline.Cues.Add(new(Math.Round(Now(), 3), file, volume, kind, maxDuration, fadeOut));
        }

        double chapterStart = 0;
        string? activeChapter = null;
        void BeginChapter(string name)
        {
            double t = Now();
            if (activeChapter != null)
                timeline.Chapters.Add(new(activeChapter, Math.Round(chapterStart, 3), Math.Round(t, 3)));
            activeChapter = name;
            chapterStart = t;
            Console.WriteLine($"[PLAYTHROUGH] Chapter '{name}' @ {t:F2}s (frame {frameIndex})");
        }

        var psi = new ProcessStartInfo("ffmpeg",
            $"-y -v error -f rawvideo -pix_fmt rgba -s {ScreenWidth}x{ScreenHeight} -r {fps} -i - -c:v libx264 -preset medium -crf 17 -pix_fmt yuv420p \"{outMp4}\"")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };
        using var ff = Process.Start(psi) ?? throw new IOException("Failed to start ffmpeg for playthrough capture");
        var stdin = ff.StandardInput.BaseStream;
        byte[] buf = new byte[ScreenWidth * ScreenHeight * 4];

        void EmitCurrentFrame(bool showHud = true)
        {
            DrawFrame(showHud: showHud);
            Image img = Raylib.LoadImageFromScreen();
            try
            {
                Marshal.Copy((IntPtr)img.Data, buf, 0, buf.Length);
                stdin.Write(buf, 0, buf.Length);
            }
            finally { Raylib.UnloadImage(img); }
            frameIndex++;
        }

        static float Smooth(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        void StepSim(float dt, bool moving = false)
        {
            _playerHp = Math.Max(_playerHp, 72);
            _energy = Math.Max(_energy, 80f);
            _knockoutActive = false;
            if (moving) _walkBobPhase += dt * 7.6f;
            _weapons.UpdateWeapons(dt, BuildPlayerCamera(), isMoving: moving);
            TickFloor(dt);
            StyleComboSystem.Update(dt);
            _hudStatusTimer = Math.Max(0f, _hudStatusTimer - dt);
            AudioMusicSystem.VoiceTimer = Math.Max(0f, AudioMusicSystem.VoiceTimer - dt);
            comicFade = Math.Max(0f, comicFade - dt);
        }

        void Glide(Vector3 fromPos, Vector3 toPos, Vector3 fromLook, Vector3 toLook, float durationSec,
            bool walkBob = true, bool tickWorld = true, bool showHud = true, Action<int, float>? onFrame = null)
        {
            int n = Math.Max(1, (int)MathF.Round(durationSec * fps));
            for (int i = 0; i < n; i++)
            {
                float u = n > 1 ? i / (float)(n - 1) : 1f;
                float s = Smooth(u);
                _playerPos = Vector3.Lerp(fromPos, toPos, s);
                Vector3 look = Vector3.Lerp(fromLook, toLook, s);
                LookAtTarget(look);
                if (tickWorld) StepSim(frameDt, moving: walkBob && Vector3.DistanceSquared(fromPos, toPos) > 0.04f);
                else
                {
                    _world.AdvanceLighting(frameDt);
                    _weapons.UpdateWeapons(frameDt, BuildPlayerCamera(), isMoving: false);
                    comicFade = Math.Max(0f, comicFade - frameDt);
                }
                onFrame?.Invoke(i, u);
                EmitCurrentFrame(showHud);
            }
        }

        void FadeOutTransition(float sec = 0.22f)
        {
            int n = Math.Max(2, (int)MathF.Round(sec * fps));
            for (int i = 0; i < n; i++)
            {
                comicFade = 0.22f * ((i + 1) / (float)n);
                EmitCurrentFrame();
            }
        }

        void FadeInFromBlack(float sec = 0.22f, bool tickWorld = true)
        {
            int n = Math.Max(2, (int)MathF.Round(sec * fps));
            for (int i = 0; i < n; i++)
            {
                comicFade = 0.22f * (1f - (i + 1) / (float)n);
                if (tickWorld) StepSim(frameDt, moving: false);
                EmitCurrentFrame();
            }
            comicFade = 0f;
        }

        // =========================================================================
        // ACT 1: MAIN MENU (2.5s) -> NEW GAME TRANSITION
        // =========================================================================
        BeginChapter("01_main_menu");
        SwitchMusic("music_main_menu.wav", 0.30f);
        MainMenuAndPauseUI.IsInMainMenu = true;
        MainMenuAndPauseUI.IsPaused = false;
        MainMenuAndPauseUI.IsSaveLoadPanelOpen = false;
        MainMenuAndPauseUI.IsModsPanelOpen = false;
        MainMenuAndPauseUI.IsSettingsPanelOpen = false;
        LoreAndMenusUI.IsLoreOpen = false;
        LoreAndMenusUI.IsSettingsOpen = false;
        LoreAndMenusUI.IsCaseOpen = false;
        _menuTime = 1.5f;
        comicFade = 0f;

        for (int i = 0; i < 68; i++)
        {
            _menuTime += frameDt;
            _menuFloor.AdvanceLighting(frameDt);
            EmitCurrentFrame();
        }
        // Smooth cinematic fade from Main Menu -> 1998 Noir Session Loading Curtain -> Intro Comic Panel 1
        BeginChapter("02_intro_comic");
        comic.Open(withTitleCurtain: true);
        int curtainFrames = (int)MathF.Round(2.15f * fps);
        for (int i = 0; i < curtainFrames; i++)
        {
            _menuTime += frameDt;
            comic.Update(frameDt, advance: false, skip: false);
            EmitCurrentFrame();
        }
        MainMenuAndPauseUI.IsInMainMenu = false;
        comicFade = 0f;
        for (int p = 0; p < 8; p++)
        {
            // Slightly longer hold on hero splash panels 1 & 8, crisp 2.45s on interior panels
            float panelHold = (p is 0 or 7) ? 2.65f : 2.40f;
            int panelFrames = (int)MathF.Round(panelHold * fps);
            Cue("comic_panel_slam.wav", 0.55f, "sfx");
            Cue($"vo/vo_comic_{p + 1}.wav", 1.00f, "vo", maxDuration: panelHold - 0.08, fadeOut: 0.24);

            for (int f = 0; f < panelFrames; f++)
            {
                bool advance = f == panelFrames - 1;
                comic.Update(frameDt, advance, skip: false);
                EmitCurrentFrame();
            }

            if (p is 0 or 3 or 6)
            {
                Cue("comic_page_turn.wav", 0.65f, "sfx");
                // Let the 0.30s diagonal page-turn wipe complete smoothly
                for (int t = 0; t < 10; t++)
                {
                    comic.Update(frameDt, advance: false, skip: false);
                    EmitCurrentFrame();
                }
            }
        }
        // Ensure comic closed cleanly
        if (comic.Active) comic.Update(frameDt, advance: false, skip: true);

        // =========================================================================
        // ACT 3: OPENING LOBBY & ARCHITECTURAL WALKTHROUGH (~17.5s)
        // =========================================================================
        BeginChapter("03_lobby_walkthrough");
        SwitchMusic("music_office_90s.wav", 0.26f);
        ResetNewGameFloor(greet: false);
        Floor.Onboarding = OnboardingStep.Done;
        Floor.WingsUnlocked = false;
        Floor.WingBarrierSlide = 0f;
        Floor.OwnedWeaponsMask = FloorSystems.ClassicMask;
        _playerCash = 180;
        _weapons.SelectSlot(1); // Father's Leather Belt
        _playerPos = LobbyEntrance;
        LookAtTarget(_world.HrTarotBoothPos + new Vector3(0f, 1.38f, 0f));
        _weapons.UpdateWeapons(0.45f, BuildPlayerCamera());
        FadeInFromBlack(0.24f);

        // 3a. Walk down crimson carpet from South Elevator to HR Veronika + Tarot sanctuary closeup (4.2s)
        Vector3 veronikaViewPos = _world.HrTarotBoothPos + new Vector3(-0.45f, 1.72f, -3.15f);
        Vector3 veronikaFocus = _world.HrTarotBoothPos + new Vector3(0f, 1.36f, -0.05f);
        Glide(LobbyEntrance, veronikaViewPos,
            _world.HrTarotBoothPos + new Vector3(0f, 1.48f, 0f), veronikaFocus, 2.3f, walkBob: true);

        AudioMusicSystem.TriggerVoiceLine("HR-ТАРОЛОГ ВЕРОНИКА",
            "Ну здравствуй, Босс. Пока ты пропадал, этаж захватили вайб-кодеры. Разбирай бардак!",
            (int)SpeakerVoiceType.VeronikaTarot);
        Cue("vo/vo_veronika_intro.wav", 1.00f, "vo", maxDuration: 4.1, fadeOut: 0.20);
        Glide(veronikaViewPos, _world.HrTarotBoothPos + new Vector3(0.75f, 1.66f, -2.55f),
            veronikaFocus, _world.HrTarotBoothPos + new Vector3(-0.08f, 1.34f, 0.05f), 1.9f, walkBob: false);

        // 3b. Pan & walk to NEW Technical / Staff Zone beside Veronika (ТЕХ. ПОМЕЩЕНИЕ & СЛУЖЕБНЫЙ ВХОД) + Coder stepping out! (4.8s)
        BeginChapter("03b_technical_doors");
        Vector3 door0 = SkyscraperFloor1World.RoomDoors[0];
        Vector3 door1 = SkyscraperFloor1World.RoomDoors[1];
        Vector3 doorsMid = (door0 + door1) * 0.5f;
        // Doors 0 & 1 sit flush in South Reception Technical Wall (Z=16.25, facing -Z into lobby)
        Vector3 techViewStart = _world.ResolvePlayerCollision(new Vector3(doorsMid.X - 0.55f, 1.78f, doorsMid.Z - 4.75f));
        Vector3 techViewEnd = _world.ResolvePlayerCollision(new Vector3(doorsMid.X + 0.85f, 1.78f, doorsMid.Z - 4.10f));
        techViewStart.Y = 1.78f;
        techViewEnd.Y = 1.78f;

        Glide(_playerPos, techViewStart, veronikaFocus, door0 + new Vector3(0.4f, 1.50f, 0f), 1.3f, walkBob: true);

        // Spawn a live vibe coder right behind Technical Door 0 so we watch the door slide open & coder step out!
        _world.ActiveCoders.Clear();
        var emergingCoder = _world.SpawnCoderForTier(SubscriptionBaitTier.FreeTier, forceSide: -1);
        emergingCoder.SpeechBubble = "О, халявный Wi-Fi в лобби! Пойду проверю!";
        emergingCoder.SpeechTimer = 3.5f;
        Cue("action_clack.wav", 0.72f, "sfx");

        Glide(techViewStart, techViewEnd,
            door0 + new Vector3(0.35f, 1.48f, 0f), doorsMid + new Vector3(0f, 1.42f, 0f), 3.4f, walkBob: false,
            onFrame: (i, u) =>
            {
                // Keep camera smoothly framing both technical doors and the emerging coder
                if (u > 0.25f && emergingCoder.Hp > 0)
                {
                    Vector3 track = Vector3.Lerp(doorsMid + new Vector3(0f, 1.42f, 0f), emergingCoder.Position + Vector3.UnitY * 1.25f, 0.35f);
                    LookAtTarget(track);
                }
            });

        // 3c. Bug-Tracker Terminal (ZERO Sofa Clipping!) + Jaba-Frog catch & deposit (4.3s)
        BeginChapter("03c_bug_tracker_frogs");
        Vector3 term = SkyscraperFloor1World.TerminalPos;
        // Diagonal wide angle framing BOTH Bug-Tracker Terminal (-3.1, 10.85) AND relocated LeatherSofa (-5.65, 9.70)
        Vector3 bugApproach = _world.ResolvePlayerCollision(new Vector3(-1.35f, 1.78f, 7.95f));
        bugApproach.Y = 1.78f;
        Vector3 bugClose = _world.ResolvePlayerCollision(term + new Vector3(0.95f, 1.72f, -1.75f));
        bugClose.Y = 1.72f;

        // Spawn a vivid wind-up Jaba-frog hopping in front of the Bug-Tracker lounge
        _world.Frogs.Clear();
        var hopper = _world.SpawnFrog(0, "frog_hopper");
        hopper.VentExit = false;
        hopper.From = term + new Vector3(1.15f, 0f, -0.95f);
        hopper.To = term + new Vector3(0.68f, 0f, -0.55f);
        hopper.Phase = 0.45f;
        hopper.Rest = 0.15f;

        _weapons.Equip(WeaponType.CaptureNet);
        Glide(_playerPos, bugApproach, doorsMid + Vector3.UnitY * 1.2f, new Vector3(-4.35f, 0.92f, 10.35f), 1.4f, walkBob: true);
        Cue("frog_croak.wav", 0.75f, "sfx");
        // Wide-angle sweep clearly showing BOTH Bug-Tracker Terminal AND nearby lounge sofa with zero clipping
        Glide(bugApproach, bugClose, new Vector3(-4.35f, 0.92f, 10.35f), hopper.Feet + new Vector3(0f, 0.22f, 0f), 1.4f, walkBob: true);
        // Catch frog with capture net + deposit at Bug-Tracker terminal
        Cue("net_swipe.wav", 0.85f, "sfx");
        _world.ProcessPlayerAttack(BuildPlayerCamera(), _weapons, GrantCash, out _);
        if (Floor.PocketFrogs.Count == 0 && _world.Frogs.Count > 0)
        {
            Floor.PocketFrogs.Add(_world.Frogs[0].SpeciesId);
            _world.Frogs.RemoveAt(0);
        }
        Glide(bugClose, bugClose + new Vector3(-0.25f, 0f, 0.15f), hopper.Feet + Vector3.UnitY * 0.35f, term + Vector3.UnitY * 1.05f, 0.65f, walkBob: false);
        string depositRes = _world.Interact(BuildPlayerCamera(), TrySpendCash, GrantCash, _weapons.CurrentWeapon);
        if (!string.IsNullOrEmpty(depositRes)) SetHudBanner(depositRes, 2.2f);
        Cue("tarot_cash.wav", 0.80f, "sfx");
        Glide(_playerPos, _playerPos + new Vector3(-0.20f, 0f, -0.15f), term + Vector3.UnitY * 1.08f, term + new Vector3(-0.35f, 1.05f, 0.05f), 0.85f, walkBob: false);

        // 3d. Upgraded Art-Deco Vending Machine Hero Closeup (3.8s)
        BeginChapter("03d_vending_closeup");
        _weapons.Equip(WeaponType.BareHands);
        // Find the best front-accessible vending machine (prefer lobby/near-corridor ones with unobstructed sightline)
        var vendCandidates = _world.OfficeProps
            .Where(p => p.Kind == OfficePropKind.VendingMachine)
            .OrderBy(p => Vector3.DistanceSquared(p.Position, new Vector3(5.02f, 0f, -6.10f)))
            .ToList();
        OfficeProp3D? heroVend = null;
        foreach (var cand in vendCandidates)
        {
            float r = cand.RotationYaw * MathF.PI / 180f;
            Vector3 fwd = new(MathF.Sin(r), 0f, MathF.Cos(r));
            Vector3 testEye = cand.Position + fwd * 1.85f + Vector3.UnitY * 1.35f;
            if (_world.Visible(testEye, cand.Position + Vector3.UnitY * 1.18f))
            {
                heroVend = cand;
                break;
            }
        }
        if (heroVend == null && vendCandidates.Count > 0) heroVend = vendCandidates[0];

        if (heroVend is OfficeProp3D hv)
        {
            float vRad = hv.RotationYaw * MathF.PI / 180f;
            Vector3 vFwd = new(MathF.Sin(vRad), 0f, MathF.Cos(vRad));
            Vector3 vRight = new(vFwd.Z, 0f, -vFwd.X);
            Vector3 vendEyeA = hv.Position + vFwd * 1.82f - vRight * 0.36f + Vector3.UnitY * 1.56f;
            Vector3 vendEyeB = hv.Position + vFwd * 1.62f + vRight * 0.28f + Vector3.UnitY * 1.16f;
            FadeOutTransition(0.18f);
            _playerPos = vendEyeA;
            LookAtTarget(hv.Position + Vector3.UnitY * 1.62f);
            FadeInFromBlack(0.18f);
            // Slow hero inspection pan from Art-Deco backlit marquee -> illuminated snacks & helical coils -> brushed-steel keypad
            Glide(vendEyeA, vendEyeB,
                hv.Position + Vector3.UnitY * 1.68f,
                hv.Position + vRight * 0.14f + Vector3.UnitY * 0.98f,
                3.2f, walkBob: false, showHud: false);
        }

        // =========================================================================
        // ACT 4: REDESIGNED COWORKING, MEETING SUITE & FURNISHED NORTH WING (~14.0s)
        // =========================================================================
        BeginChapter("04_coworking_meeting_north");
        _weapons.SelectSlot(1);
        // Populate Galley Desks so coworking feels alive
        if (_world.GalleyDesks.Count >= 2)
        {
            _world.GalleyDesks[0].IsOccupied = true;
            _world.GalleyDesks[0].CoderName = "Джуниор Данила";
            _world.GalleyDesks[0].HoodieColor = new Color(65, 195, 255, 255);
            _world.GalleyDesks[0].IsSlackingInTwitter = false;
            _world.GalleyDesks[1].IsOccupied = true;
            _world.GalleyDesks[1].CoderName = "Промпт-Бро Артемий";
            _world.GalleyDesks[1].HoodieColor = new Color(180, 75, 255, 255);
            _world.GalleyDesks[1].IsSlackingInTwitter = true;
        }

        // 4a. West Coworking Openspace (4-seat islands & Galley desks) (4.2s)
        FadeOutTransition(0.18f);
        Vector3 westStart = new(-12.2f, 1.85f, 10.8f);
        Vector3 westEnd = new(-19.5f, 1.82f, 4.2f);
        _playerPos = westStart;
        LookAtTarget(new Vector3(-18.5f, 1.15f, 6.2f));
        FadeInFromBlack(0.18f);
        Glide(westStart, westEnd,
            new Vector3(-18.5f, 1.15f, 6.2f),
            new Vector3(-24.0f, 1.10f, -1.5f), 3.8f, walkBob: true);

        // 4b. East Executive Glass Meeting Room (X=30..37.5, Z=-3..+5, open doorway at 30.0, 1.0) (3.8s)
        BeginChapter("04b_east_meeting_suite");
        FadeOutTransition(0.18f);
        Vector3 eastStart = new(26.2f, 1.80f, 1.85f);
        Vector3 eastEnd = new(31.25f, 1.76f, 0.95f);
        _playerPos = eastStart;
        LookAtTarget(new Vector3(33.75f, 1.22f, 1.0f));
        FadeInFromBlack(0.18f);
        Glide(eastStart, eastEnd,
            new Vector3(33.75f, 1.22f, 1.0f),
            new Vector3(35.8f, 1.15f, -0.45f), 3.4f, walkBob: true);

        // 4c. Quarantine Barrier Unlock -> Walk smoothly into Densely Furnished North Wing! (5.8s)
        BeginChapter("04c_north_wing_furnished");
        FadeOutTransition(0.18f);
        Floor.WingsUnlocked = false;
        Floor.WingBarrierSlide = 0f;
        Vector3 barrierApproach = new(7.8f, 1.80f, -12.4f);
        Vector3 northEastBay = new(9.2f, 1.82f, -19.2f);
        Vector3 northExecutive = new(5.8f, 1.85f, -25.4f);
        Vector3 northWestSweep = new(-4.6f, 1.85f, -25.2f);
        _playerPos = barrierApproach;
        LookAtTarget(new Vector3(11.5f, 1.32f, -23.5f));
        FadeInFromBlack(0.18f);

        // Unlock Quarantine Barrier and watch glass partition slide open
        Floor.WingsUnlocked = true;
        Cue("action_clack.wav", 0.78f, "sfx");
        SetHudBanner("Карантинный барьер открыт — проход в Северное крыло", 2.4f);
        Glide(barrierApproach, northEastBay,
            new Vector3(11.5f, 1.32f, -23.5f), new Vector3(14.5f, 1.22f, -25.0f), 1.8f, walkBob: true);

        // Sweep across East North Wing pods/vending -> Far-North Executive Boardroom & Sofas -> West North Wing pods!
        Glide(northEastBay, northExecutive,
            new Vector3(14.5f, 1.22f, -25.0f), new Vector3(0.0f, 1.15f, -30.5f), 1.9f, walkBob: true);
        Glide(northExecutive, northWestSweep,
            new Vector3(0.0f, 1.15f, -30.5f), new Vector3(-16.5f, 1.22f, -25.5f), 1.9f, walkBob: true);

        // =========================================================================
        // ACT 5: SHOP, COMBAT, WEAPONS & SLOT ROULETTE (~16.5s)
        // =========================================================================
        BeginChapter("05a_veronika_shop");
        FadeOutTransition(0.18f);
        _playerPos = _world.HrTarotBoothPos + new Vector3(0.0f, 1.72f, -2.5f);
        LookAtTarget(_world.HrTarotBoothPos + Vector3.UnitY * 1.32f);
        _playerCash = 680;
        Floor.OwnedWeaponsMask = FloorSystems.ClassicMask;
        _weapons.UnlockedSolderingIron = true;
        _weapons.UnlockedMicroUzi = true;
        _weapons.UnlockedSawedOff = true;
        Floor.SkinMasks[(int)WeaponType.MicroUzi] |= (1 << 10) | (1 << 11);
        Floor.EquipSkin(WeaponType.MicroUzi, 10);
        OpenFloorPanel(FloorPanel.Upgrades);
        sidegrades = false;
        floorRow = 1;
        Cue("vo/vo_veronika_shop_open.wav", 0.96f, "vo", maxDuration: 2.75, fadeOut: 0.22);
        FadeInFromBlack(0.16f, tickWorld: false);

        for (int i = 0; i < 78; i++)
        {
            if (i == 26) { floorRow = 2; Cue("case_tick.wav", 0.55f, "sfx"); }
            if (i == 52) { floorRow = 3; Cue("case_tick.wav", 0.55f, "sfx"); }
            _world.AdvanceLighting(frameDt);
            EmitCurrentFrame();
        }
        OpenFloorPanel(FloorPanel.None);

        // 5b. Belt Whip + Soldering Iron + 220V Cooler Puddle Stun (5.0s)
        BeginChapter("05b_combat_belt_solder_puddle");
        FadeOutTransition(0.16f);
        StyleComboSystem.Reset();
        _world.ActiveCoders.Clear();
        _world.EnemyProjectiles.Clear();
        Vector3 cooler = _world.CoolerPos;
        var coderA = _world.SpawnCoderForTier(SubscriptionBaitTier.CursorPro, forceSide: -1);
        coderA.ExitingRoom = false;
        coderA.Position = cooler + new Vector3(1.25f, 0f, 0.95f);
        coderA.FacingYaw = -0.7f;
        coderA.State = CoderAIState.CombatHostile;
        coderA.Hp = coderA.MaxHp = 95f;

        var coderB = _world.SpawnCoderForTier(SubscriptionBaitTier.FreeTier, forceSide: 1);
        coderB.ExitingRoom = false;
        coderB.Position = cooler + new Vector3(0.35f, 0f, 1.25f);
        coderB.FacingYaw = -0.3f;
        coderB.State = CoderAIState.WalkingToBait;

        Vector3 combatPos = _world.ResolvePlayerCollision(cooler + new Vector3(2.85f, 1.72f, 2.35f));
        combatPos.Y = 1.72f;
        _playerPos = combatPos;
        _weapons.SelectSlot(1); // Belt
        LookAtTarget(coderA.CombatCenter);
        _weapons.UpdateWeapons(0.35f, BuildPlayerCamera());
        FadeInFromBlack(0.16f);

        // Belt whip combo strike on coderA
        Glide(combatPos, combatPos + new Vector3(-0.35f, 0f, -0.35f), coderA.CombatCenter, coderA.CombatCenter, 0.75f, walkBob: true,
            onFrame: (i, u) =>
            {
                if (i == 8)
                {
                    LookAtTarget(coderA.CombatCenter);
                    _world.ProcessPlayerAttack(BuildPlayerCamera(), _weapons, GrantCash, out _);
                    Cue("belt_whip.wav", 0.95f, "sfx");
                    Cue("hitmarker.wav", 0.72f, "sfx");
                }
            });

        // Spill water cooler -> equip Soldering Iron -> electrify 220V puddle!
        Vector3 puddleSpot = cooler + new Vector3(1.15f, 0f, 0.25f);
        Vector3 beforeInteract = _playerPos;
        _playerPos = cooler + new Vector3(0f, 1.72f, 1.75f);
        LookAtTarget(cooler + Vector3.UnitY * 0.9f);
        _world.Interact(BuildPlayerCamera(), TrySpendCash, GrantCash, _weapons.CurrentWeapon);
        _playerPos = beforeInteract;

        _weapons.SelectSlot(2); // 90s Soldering Iron
        Glide(_playerPos, cooler + new Vector3(2.55f, 1.66f, 2.15f), coderA.CombatCenter, puddleSpot, 0.85f, walkBob: false);
        LookAtTarget(puddleSpot);
        _world.ProcessPlayerAttack(BuildPlayerCamera(), _weapons, GrantCash, out _);
        Cue("solder_sizzle.wav", 0.95f, "sfx");
        Glide(_playerPos, cooler + new Vector3(3.15f, 1.62f, 2.65f), puddleSpot, cooler + new Vector3(0.4f, 0.92f, 0.85f), 1.9f, walkBob: false);

        // 5c. Micro-Uzi Burst + Sawed-Off Shotgun Blast + Style Combo (4.3s)
        BeginChapter("05c_combat_uzi_shotgun");
        FadeOutTransition(0.16f);
        _world.ActiveCoders.Clear();
        _world.EnemyProjectiles.Clear();
        _playerPos = new Vector3(0.0f, 1.72f, 9.55f);
        var miniBoss = _world.SpawnCoderForTier(SubscriptionBaitTier.CloudEntropicMax, forceSide: 1);
        miniBoss.ExitingRoom = false;
        miniBoss.Position = new Vector3(1.15f, 0f, 5.85f);
        miniBoss.FacingYaw = -0.45f;
        miniBoss.State = CoderAIState.CombatHostile;
        miniBoss.Hp = miniBoss.MaxHp = 420f;

        var escortCoder = _world.SpawnCoderForTier(SubscriptionBaitTier.CursorPro, forceSide: -1);
        escortCoder.ExitingRoom = false;
        escortCoder.Position = new Vector3(-1.35f, 0f, 6.20f);
        escortCoder.FacingYaw = 0.35f;
        escortCoder.State = CoderAIState.CombatHostile;

        Floor.EquipSkin(WeaponType.MicroUzi, 11);
        _weapons.SelectSlot(3); // Micro-Uzi
        LookAtTarget(miniBoss.CombatCenter);
        _weapons.UpdateWeapons(0.35f, BuildPlayerCamera());
        FadeInFromBlack(0.16f);

        // Rapid Micro-Uzi strafing burst
        Glide(_playerPos, new Vector3(-0.95f, 1.72f, 8.95f), miniBoss.CombatCenter, miniBoss.CombatCenter, 1.85f, walkBob: true,
            onFrame: (i, u) =>
            {
                LookAtTarget(miniBoss.CombatCenter);
                if (i is 6 or 11 or 16 or 21 or 26 or 31 or 36)
                {
                    UpdateTrigger(frameDt, true, true, false, false);
                    Cue("uzi_shot.wav", 0.72f, "sfx");
                    if (i % 10 == 6) Cue("hitmarker.wav", 0.65f, "sfx");
                }
                else UpdateTrigger(frameDt, false, false, false, false);
            });

        // Swap to Sawed-Off Shotgun & fire heavy double-barrel blast!
        _weapons.SelectSlot(4);
        Glide(_playerPos, new Vector3(0.45f, 1.72f, 8.55f), miniBoss.CombatCenter, miniBoss.CombatCenter, 2.0f, walkBob: true,
            onFrame: (i, u) =>
            {
                LookAtTarget(miniBoss.CombatCenter);
                if (i == 16 || i == 38)
                {
                    UpdateTrigger(frameDt, true, true, false, false);
                    Cue("shotgun_blast.wav", 0.95f, "sfx");
                    Cue("hitmarker.wav", 0.78f, "sfx");
                }
                else UpdateTrigger(frameDt, false, false, false, false);
            });

        // 5d. Veronika's Slot Roulette Spin (4.9s)
        BeginChapter("05d_slot_roulette");
        FadeOutTransition(0.16f);
        _world.ActiveCoders.Clear();
        _world.EnemyProjectiles.Clear();
        _playerPos = _world.HrTarotBoothPos + new Vector3(0.18f, 1.72f, -2.55f);
        LookAtTarget(_world.HrTarotBoothPos + new Vector3(0f, 1.32f, -0.05f));
        _world.Slot.Start(Floor, VibeCoderRarity.Legendary, new Random(7), GrantCash);
        Cue("slot_lever_pull.wav", 0.90f, "sfx");
        Cue("vo/vo_veronika_slot_spin.wav", 0.96f, "vo", maxDuration: 3.65, fadeOut: 0.22);
        FadeInFromBlack(0.16f, tickWorld: false);

        bool slotRevealed = false;
        int slotFrames = (int)MathF.Round(4.65f * fps);
        for (int i = 0; i < slotFrames; i++)
        {
            UpdateSlot(frameDt);
            if (!slotRevealed && _world.Slot.Reveal)
            {
                slotRevealed = true;
                Cue("slot_win_jackpot.wav", 0.92f, "sfx");
                Cue("vo/vo_veronika_slot_jackpot.wav", 1.00f, "vo", maxDuration: 1.5, fadeOut: 0.25);
            }
            EmitCurrentFrame();
        }
        _world.Slot.Active = false;

        // =========================================================================
        // ACT 6: ARCHIVE [N] (PRE-BREACH SPOILER-FREE CHECK, 4.0s)
        // =========================================================================
        BeginChapter("06_archive_spoiler_free");
        LoreAndMenusUI.IsLoreOpen = true;
        LoreAndMenusUI.ActiveLoreTab = 0;
        LoreAndMenusUI.SelectArchiveEntryForPreview(4); // Entry #05 — MUST stay redacted before elevator reveal!
        Cue("action_clack.wav", 0.55f, "sfx");
        for (int i = 0; i < 66; i++)
        {
            _world.AdvanceLighting(frameDt);
            EmitCurrentFrame();
        }
        LoreAndMenusUI.ActiveLoreTab = 3; // Dictionary tab
        Cue("case_tick.wav", 0.55f, "sfx");
        for (int i = 0; i < 54; i++)
        {
            _world.AdvanceLighting(frameDt);
            EmitCurrentFrame();
        }
        LoreAndMenusUI.IsLoreOpen = false;

        // =========================================================================
        // ACT 7: ELEVATOR REPAIR -> FULL BOSS CUTSCENE -> BOSS FIGHT & VICTORY (~30.0s)
        // =========================================================================
        BeginChapter("07a_elevator_repair_minigame");
        FadeOutTransition(0.18f);
        ResetNewGameFloor(greet: false);
        Floor.Onboarding = OnboardingStep.Done;
        Floor.WingsUnlocked = true;
        Floor.HasRadio = true;
        Floor.WingBarrierSlide = 1f;
        Floor.OwnedWeaponsMask = FloorSystems.ClassicMask;
        _weapons.UnlockedSolderingIron = true;
        _weapons.UnlockedMicroUzi = true;
        _weapons.UnlockedSawedOff = true;
        _playerCash = 850;
        _weapons.SelectSlot(4); // Sawed-Off Shotgun
        _world.HasElevatorKey = true;
        _playerPos = _world.NorthElevatorPos + new Vector3(0.06f, 1.72f, -1.85f);
        LookAtTarget(_world.NorthElevatorPos + new Vector3(0f, 1.75f, 0.3f));
        _weapons.UpdateWeapons(0.4f, BuildPlayerCamera());

        _repairMinigame.Open();
        _repairMinigame.SetPreviewStateForScreenshot(1);
        FadeInFromBlack(0.16f, tickWorld: false);
        for (int i = 0; i < 30; i++)
        {
            _repairMinigame.Update(frameDt);
            EmitCurrentFrame();
        }
        _repairMinigame.SetPreviewStateForScreenshot(2);
        Cue("solder_sizzle.wav", 0.90f, "sfx");
        for (int i = 0; i < 36; i++)
        {
            _repairMinigame.Update(frameDt);
            EmitCurrentFrame();
        }
        _repairMinigame.SetPreviewStateForScreenshot(3);
        Cue("action_clack.wav", 0.85f, "sfx");
        for (int i = 0; i < 24; i++)
        {
            _repairMinigame.Update(frameDt);
            EmitCurrentFrame();
        }
        _repairMinigame.Close();

        // 7b. FULL FIXED BOSS INTRO CUTSCENE (~14.5s)
        BeginChapter("07b_boss_intro_cutscene");
        SwitchMusic("music_boss_fight.wav", 0.32f);
        _world.TrySummonElevatorBoss(out _);
        BossIntroCutscene.Reset();
        Cue("vo/vo_petrovich_quarantine.wav", 1.00f, "vo", maxDuration: 3.15, fadeOut: 0.18);

        int cutsceneCueMask = 0;
        void CaptureCutsceneFrame(float simDt)
        {
            BossIntroCutscene.SyncState(_world);
            TickFloor(simDt);
            if (BossIntroCutscene.ShouldRelocatePlayerAfterKnockback(_world))
            {
                _playerPos = BossIntroCutscene.KnockedBackStandPosition(_world);
                LookAtTarget(_world.ElevatorBoss.Position + Vector3.UnitY * 1.68f);
            }
            BossIntroCutscene.SyncState(_world);

            var b = _world.ElevatorBoss;
            if (b.State == ElevatorBossState.Repairing)
            {
                float r = 1f - Math.Clamp(b.StateTimer / 2.3f, 0f, 1f);
                if (r >= 0.88f && (cutsceneCueMask & 1) == 0) { cutsceneCueMask |= 1; Cue("action_clack.wav", 0.85f, "sfx"); }
            }
            else if (b.State == ElevatorBossState.IntroSpeech)
            {
                float u = 1f - Math.Clamp(b.StateTimer / 2.4f, 0f, 1f);
                if (u >= 0.01f && (cutsceneCueMask & 2) == 0) { cutsceneCueMask |= 2; Cue("boss_siren.wav", 0.65f, "sfx"); }
                if (u >= 0.19f && (cutsceneCueMask & 4) == 0) { cutsceneCueMask |= 4; Cue("comic_panel_slam.wav", 0.95f, "sfx"); }
                if (u >= 0.240f && (cutsceneCueMask & 512) == 0) { cutsceneCueMask |= 512; Cue("comic_boom.wav", 0.82f, "sfx"); }
                if (u >= 0.27f && (cutsceneCueMask & 8) == 0) { cutsceneCueMask |= 8; Cue("boot_land.wav", 0.88f, "sfx"); }
                if (u >= 0.29f && (cutsceneCueMask & 16) == 0) { cutsceneCueMask |= 16; Cue("vo/vo_boss_reveal.wav", 1.00f, "vo", maxDuration: 8.6, fadeOut: 0.30); }
                if (u >= 0.585f && (cutsceneCueMask & 32) == 0) { cutsceneCueMask |= 32; Cue("comic_whip_snap.wav", 0.90f, "sfx"); }
                if (u >= 0.671f && (cutsceneCueMask & 64) == 0) { cutsceneCueMask |= 64; Cue("comic_boom.wav", 1.00f, "sfx"); }
                if (u >= 0.684f && (cutsceneCueMask & 128) == 0) { cutsceneCueMask |= 128; Cue("comic_boom.wav", 0.92f, "sfx"); }
                if (u >= 0.822f && (cutsceneCueMask & 256) == 0) { cutsceneCueMask |= 256; Cue("action_clack.wav", 0.92f, "sfx"); }
            }
            EmitCurrentFrame();
        }

        int repFrames = 0;
        while (_world.ElevatorBoss.State == ElevatorBossState.Repairing && repFrames < 90)
        {
            CaptureCutsceneFrame(frameDt);
            repFrames++;
        }
        int introFrames = 0;
        while (_world.ElevatorBoss.State == ElevatorBossState.IntroSpeech && introFrames < 360)
        {
            CaptureCutsceneFrame(frameDt * 0.21f);
            introFrames++;
        }

        // 7c. LIVE BOSS FIGHT ACTION (Shield Break + Dodge + Shotgun Core Blasts, ~7.8s)
        BeginChapter("07c_boss_fight_live");
        _playerPos = BossIntroCutscene.KnockedBackStandPosition(_world);
        _playerPos.Y = 1.72f;
        var boss = _world.ElevatorBoss;
        boss.Position = new Vector3(0.0f, 0.0f, -10.65f);
        boss.FacingYaw = MathF.PI;
        boss.ShieldHp = EscapedAgentBoss.ShieldCapacity;
        boss.Hp = boss.MaxHp * 0.65f;
        _weapons.SelectSlot(3); // Micro-Uzi to shred cyan shield!
        LookAtTarget(boss.Position + Vector3.UnitY * 1.75f);
        Cue("vo/vo_boss_mock_0.wav", 0.98f, "vo", maxDuration: 4.8, fadeOut: 0.25);

        // Strafe wide across the newly blasted-open North Wing arena (Z=-21..-19.5) shredding the shield through the smoking breach!
        Glide(_playerPos, new Vector3(+3.10f, 1.72f, -19.80f),
            boss.Position + Vector3.UnitY * 1.75f, boss.Position + Vector3.UnitY * 1.75f, 3.0f, walkBob: true,
            onFrame: (i, u) =>
            {
                boss.Position = Vector3.Lerp(new Vector3(0.0f, 0.0f, -10.65f), new Vector3(0.0f, 0.0f, -13.40f), u);
                LookAtTarget(boss.Position + Vector3.UnitY * 1.72f);
                if (i >= 10 && i <= 75 && i % 5 == 0)
                {
                    UpdateTrigger(frameDt, true, true, false, false);
                    Cue("uzi_shot.wav", 0.68f, "sfx");
                    if (i % 15 == 10) Cue("hitmarker.wav", 0.65f, "sfx");
                }
                else UpdateTrigger(frameDt, false, false, false, false);
                if (u > 0.68f && boss.ShieldHp > 0f) boss.ShieldHp = 0f;
            });

        // Swap to Sawed-Off Shotgun, push through the blasted Z=-15.8 rubble breach, dodge incoming node_modules cube & blast core -> DEFEAT!
        _weapons.SelectSlot(4);
        boss.ShieldHp = 0f;
        boss.ShieldDelay = 8f;
        boss.State = ElevatorBossState.ThrowingNodeModules;
        _world.SpawnNodeModulesCube(boss.Position + new Vector3(1.6f, 1.75f, -0.8f), _playerPos + new Vector3(2.4f, 0.2f, -2.5f));
        Glide(_playerPos, new Vector3(-1.85f, 1.72f, -17.65f),
            boss.Position + Vector3.UnitY * 1.72f, boss.Position + Vector3.UnitY * 1.72f, 3.8f, walkBob: true,
            onFrame: (i, u) =>
            {
                LookAtTarget(boss.Position + Vector3.UnitY * 1.70f);
                if (i == 22 || i == 62)
                {
                    UpdateTrigger(frameDt, true, true, false, false);
                    Cue("shotgun_blast.wav", 0.98f, "sfx");
                    Cue("hitmarker.wav", 0.82f, "sfx");
                }
                else if (i == 98)
                {
                    boss.Hp = MathF.Min(boss.Hp, 25f);
                    UpdateTrigger(frameDt, true, true, false, false);
                    Cue("shotgun_blast.wav", 1.00f, "sfx");
                    Cue("comic_boom.wav", 0.90f, "sfx");
                }
                else UpdateTrigger(frameDt, false, false, false, false);
            });

        // Ensure boss defeat cleanly triggered for Finale!
        if (!boss.IsDefeated)
        {
            boss.Hp = 0f;
            boss.IsDefeated = true;
            boss.IsActive = false;
            _world.IsFloor2Unlocked = true;
            _world.ElevatorDoorOpenAmount = 1f;
            Floor.FinalePending = true;
        }

        // 7d. VICTORY FINALE (~4.4s)
        BeginChapter("07d_victory_finale");
        SwitchMusic("music_main_menu.wav", 0.28f);
        Cue("slot_win_jackpot.wav", 0.85f, "sfx");
        Cue("vo/vo_boss_defeat_twist.wav", 1.00f, "vo", maxDuration: 4.2, fadeOut: 0.35);
        SetHudBanner("ИИ-АГЕНТ v0.1 ОСТАНОВЛЕН — ЛИФТ НА 2-Й ЭТАЖ ОТКРЫТ!", 3.0f, foreground: true);

        // 1.4s triumphant view of unlocked North Elevator -> 3.0s Victory Finale Dossier
        Glide(_playerPos, new Vector3(0.0f, 1.72f, -13.2f),
            _world.NorthElevatorPos + Vector3.UnitY * 1.85f, _world.NorthElevatorPos + Vector3.UnitY * 1.85f, 1.4f, walkBob: false);

        OpenFloorPanel(FloorPanel.BossFinale);
        for (int i = 0; i < 90; i++)
        {
            _world.AdvanceLighting(frameDt);
            if (i >= 78) comicFade = 0.22f * ((i - 77) / 13f);
            EmitCurrentFrame();
        }
        OpenFloorPanel(FloorPanel.None);

        // Close final chapter & music segment
        BeginChapter("end");
        timeline.Chapters.RemoveAt(timeline.Chapters.Count - 1);
        SwitchMusic(null);
        timeline.TotalFrames = frameIndex;
        timeline.TotalDuration = Math.Round(Now(), 3);

        stdin.Flush();
        stdin.Close();
        ff.WaitForExit();

        File.WriteAllText(cuesJson, JsonSerializer.Serialize(timeline, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[PLAYTHROUGH] Video frames={frameIndex} ({timeline.TotalDuration:F2}s) exit={ff.ExitCode} cues={timeline.Cues.Count} music={timeline.MusicSegments.Count}");
        if (ff.ExitCode != 0) throw new IOException($"ffmpeg exited with code {ff.ExitCode}");
    }
}
