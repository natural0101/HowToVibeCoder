using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public static partial class Program
{
    private static void CheckFloorHunt(Action<string> capture=null)
    {
        ResetNewGameFloor(greet:false);
        MainMenuAndPauseUI.IsInMainMenu=false;
        Floor.Onboarding=OnboardingStep.Done; Floor.WingsUnlocked=true; Floor.WingBarrierSlide=1;
        _playerCash=38;
        void Require(bool condition,string what)
        {
            if(!condition) throw new InvalidDataException("Floor hunt: "+what);
        }
        void Tick(int frames=1)
        {
            for(int i=0;i<frames;i++)
                _world.UpdateWorld(1f/60,_playerPos,_isCrouching,null,null);
        }
        void Until(Func<bool> done,string what,int limit=1200)
        {
            for(int i=0;i<limit && !done();i++) Tick();
            Require(done(),what+" "+string.Join(",",_world.ActiveCoders.Select(c=>$"{c.State} {c.Position} {c.Stress}")));
        }
        PressTestKey(KeyboardKey.U); PressTestKey(KeyboardKey.Enter);
        if(floorPanel!=FloorPanel.Upgrades) PressTestKey(KeyboardKey.U);
        PressTestKey(KeyboardKey.Two); PressTestKey(KeyboardKey.F2);
        Require(Floor.BaitStock.SequenceEqual(new[]{1,1,0}) && _playerCash==0,"net gift and shop purchase");
        PressTestKey(KeyboardKey.Escape);
        _playerPos=new(-5.8f,1.72f,8.1f); LookAtTarget(new(-5.8f,.1f,6));
        PressTestKey(KeyboardKey.G);
        var bait=Floor.PlacedBait;
        Require(bait?.Tier==1 && Floor.BaitStock[1]==0 && Math.Abs(bait.Position.Z-7)<.05f,"G drops highest tier");
        var saved=System.Text.Json.JsonSerializer.Deserialize<FloorSnapshot>(System.Text.Json.JsonSerializer.Serialize(CaptureFloor(),FloorJson),FloorJson);
        Require(saved.Systems.BaitStock.SequenceEqual(Floor.BaitStock) && saved.Systems.PlacedBait.Position==bait.Position,"saved inventory and deployment");
        Until(()=>_world.ActiveCoders.Any(c=>c.State==CoderAIState.Peeking) && _world.RoomOpen[bait.Door]>=.34f,"exposed peek");
        Until(()=>_world.ActiveCoders.Count==0 && _world.RoomOpen[bait.Door]==0,"door slammed");
        Vector3 cover=new(-4.12f,1.16f,9.12f);
        _isCrouching=true; _playerPos=cover;
        Until(()=>_world.ActiveCoders.Any(c=>c.State==CoderAIState.SneakingToBait && !c.ExitingRoom),"hidden approach");
        var hunter=_world.ActiveCoders.Single();
        Require(hunter.Rarity is VibeCoderRarity.Rare or VibeCoderRarity.Epic,"latte candidate");
        _playerPos=new(-5.8f,1.72f,8); _isCrouching=false;
        Until(()=>hunter.Stress>=25,"upright stress");
        _playerPos=_world.ResolvePlayerCollision(new(-1.45f,1.16f,6.65f)); _isCrouching=true; Tick(12);
        Require(_world.Visible(_playerPos,hunter.CombatCenter) && _world.Visible(_playerPos,bait.Position+Vector3.UnitY*.22f) && !_world.IsPlayerSpottedAtBait(bait,_playerPos,true) && hunter.Stress>5,"hidden observation with live stress");
        _weapons.Equip(WeaponType.BareHands);
        AudioMusicSystem.VoiceTimer=_hudStatusTimer=0;
        LookAtTarget((hunter.CombatCenter+bait.Position)*.5f);
        capture?.Invoke("54_bait_on_floor_stealth_mouse.png");
        _world.HostSprinting=true; _playerPos=new(-5.8f,1.72f,8); _isCrouching=false;
        Until(()=>hunter.State==CoderAIState.PanickedFleeing,"sprint panic");
        _world.HostSprinting=false;
        _playerPos=hunter.Position+new Vector3(.35f,1.6f,1.1f);
        _weapons.Equip(WeaponType.CaptureNet); LookAtTarget(hunter.CombatCenter);
        float hp=hunter.Hp;
        _world.ProcessPlayerAttack(BuildPlayerCamera(),_weapons,GrantCash,out var dodge);
        Require(hunter.DodgeCooldown>=1.39f && hunter.Hp==hp && dodge==Loc.T("Увернулся!","Dodged!"),"panicked net dodge");
        Until(()=>_world.RoomOpen[bait.Door]>.72f && hunter.Position.Z>3.45f,"return door opens");
        _weapons.Equip(WeaponType.BareHands);
        _playerPos=new(-4.75f,1.55f,6.75f); LookAtTarget(hunter.CombatCenter);
        capture?.Invoke("55_coder_stress_dodge_escape.png");
        Until(()=>!_world.ActiveCoders.Contains(hunter),"escaped inside");
        _isCrouching=true; _playerPos=cover;
        Until(()=>_world.ActiveCoders.Any(c=>c.State==CoderAIState.LuredAtBait),"returns to feed");
        hunter=_world.ActiveCoders.Single();
        _playerPos=hunter.Position-new Vector3(MathF.Sin(hunter.FacingYaw),0,MathF.Cos(hunter.FacingYaw))*1.1f+Vector3.UnitY*1.16f;
        Tick(30); Require(hunter.Stress==0,"quiet rear approach");
        _weapons.Equip(WeaponType.CaptureNet);
        LookAtTarget(hunter.CombatCenter);
        _world.ProcessPlayerAttack(BuildPlayerCamera(),_weapons,GrantCash,out _);
        Require(hunter.CanSignNda,"quiet stun");
        _world.ProcessPlayerAttack(BuildPlayerCamera(),_weapons,GrantCash,out _);
        Require(_world.CarriedCoder==hunter,"one-click scoop");
        Require(Floor.PlacedBait==null,"successful hunt consumes one deployment");
        _world.SellCarriedCoderToHrVeronika(GrantCash);
        Require(_world.Slot.Active,"premium delivery spins");
        int before=_playerCash;
        ApplyFloorAction(50); ApplyFloorAction(52);
        Require(Floor.BaitStock.SequenceEqual(new[]{2,0,1}) && _playerCash==before-93,"basic and premium prices");
        _playerPos=new(-5.8f,1.72f,8.1f); LookAtTarget(new(-5.8f,0,7));
        _world.Interact(BuildPlayerCamera(),TrySpendCash,GrantCash);
        Require(Floor.PlacedBait?.Tier==2,"floor aim E deploys pro laptop");
        LookAtTarget(Floor.PlacedBait.Position+Vector3.UnitY*.1f);
        _world.Interact(BuildPlayerCamera(),TrySpendCash,GrantCash);
        Require(Floor.PlacedBait==null && Floor.BaitStock[2]==1,"recover unused bait");
        Console.WriteLine("PASS floor hunt: shop, save, peek and slam, cover, sprint, dodge, escape, ambush, scoop, slot and recovery");
        if (capture != null)
        {
            // Close-up visual inspection of exterior broken, exterior repaired, and interior cabin elevator button panels.
            ResetNewGameFloor(greet:false);
            _weapons.Equip(WeaponType.BareHands);
            _isCrouching=false;
            AudioMusicSystem.VoiceTimer=_hudStatusTimer=0;
            Vector3 northPanel=new(_world.ElevatorButtonPos.X,1.38f,_world.NorthElevatorPos.Z-.374f);
            _playerPos=northPanel+new Vector3(-.12f,.05f,-.56f);
            LookAtTarget(northPanel);
            capture("56_elevator_panel_broken_closeup.png");

            _world.HasElevatorKey=true;
            _world.ElevatorRepaired=true;
            _world.ElevatorDoorOpenAmount=1f;
            _world.ElevatorGlassOpacity=0f;
            _playerPos=northPanel+new Vector3(-.12f,.05f,-.56f);
            LookAtTarget(northPanel);
            capture("57_elevator_panel_repaired_closeup.png");

            Vector3 cabinPanel=_world.CentralElevatorCorePos+new Vector3(1.52f,1.38f,-4.166f);
            _playerPos=cabinPanel+new Vector3(-.14f,.04f,.60f);
            LookAtTarget(cabinPanel);
            capture("58_elevator_cabin_panel_closeup.png");

            // Part 2 visual self-QA: new articulated Vibe Coder poses (sneak, flee, kneel) + resized desk & seated coder + 12 collectible lineup!
            ResetNewGameFloor(greet:false);
            _weapons.Equip(WeaponType.BareHands);
            _isCrouching=false;
            AudioMusicSystem.VoiceTimer=_hudStatusTimer=0;
            _world.ActiveCoders.Clear();
            var cSneak=new VibeCoderEntity { Id=900, Name="Стажёр Артём", SpeciesId="tab", Position=new(-4.8f,0,7.2f), FacingYaw=-.45f,
                State=CoderAIState.SneakingToBait, WalkCycle=1.1f, HoodieColor=new(78,136,214,255), GlassesColor=new(86,238,224,255), Hp=50, MaxHp=50 };
            var cFlee=new VibeCoderEntity { Id=902, Name="Берсерк Влад", SpeciesId="refactor", Position=new(-3.2f,0,7.0f), FacingYaw=2.45f,
                State=CoderAIState.PanickedFleeing, WalkCycle=1.25f, DodgeCooldown=0.4f, HoodieColor=new(186,52,72,255), GlassesColor=new(255,192,82,255), Hp=50, MaxHp=50 };
            var cKneel=new VibeCoderEntity { Id=901, Name="Смузи Макс", SpeciesId="smoothie", Position=new(-4.0f,0,6.2f), FacingYaw=.15f,
                State=CoderAIState.Subdued, HoodieColor=new(164,131,213,255), GlassesColor=new(179,232,76,255), Hp=1, MaxHp=50 };
            _world.ActiveCoders.AddRange([cSneak, cFlee, cKneel]);
            _playerPos=new(-4.0f,1.52f,9.45f);
            LookAtTarget(new(-4.0f,1.02f,6.95f));
            capture("59_part2_coder_sneak_flee_kneel.png");

            // Close-up on face, elbows, hoodie, headphones, and kneeling pose
            _playerPos=new(-4.65f,1.42f,8.55f);
            LookAtTarget(cSneak.Position+Vector3.UnitY*1.12f);
            capture("60_part2_coder_face_closeup.png");

            // Seated Vibe Coder (Tech-Wizard!) at resized 0.92x Open-Space desk + standing CRT-Helmet coder beside desk
            var desk=_world.GalleyDesks[0];
            desk.IsOccupied=true;
            desk.CoderName="Маг Фреймворков";
            desk.SpeciesId="framework";
            desk.HoodieColor=new(42,88,204,255);
            desk.GlassesColor=new(249,197,76,255);
            desk.IsSlackingInTwitter=false;
            cSneak.SpeciesId="legacy";
            cSneak.HoodieColor=new(69,111,75,255);
            cSneak.GlassesColor=new(132,248,148,255);
            cSneak.Position=desk.DeskPosition+new Vector3(1.42f,0,.15f);
            cSneak.State=CoderAIState.WalkingToBait;
            cSneak.IsCombatCoder=true; cSneak.Aggro=false;
            cSneak.WalkCycle=0f;
            cSneak.FacingYaw=0f;
            _world.ActiveCoders.Remove(cFlee);
            _world.ActiveCoders.Remove(cKneel);
            _playerPos=desk.DeskPosition+new Vector3(1.65f,1.55f,2.35f);
            LookAtTarget(desk.DeskPosition+new Vector3(.35f,.92f,.15f));
            capture("61_part2_desk_chair_seated_scale.png");

            // 12-species collectible Vibe Coder lineup (clear unobstructed shot IN FRONT of Veronika's desk!)
            _world.ActiveCoders.Clear();
            string[] showcaseIds = [
                "tab", "smoothie", "leverage", "framework", "call", "refactor",
                "no-commits", "skolkovo", "meetup", "friday", "legacy", "sb_chief"
            ];
            for (int idx = 0; idx < showcaseIds.Length; idx++)
            {
                var sp = VibeCoderCollection.All.Single(s => s.Id == showcaseIds[idx]);
                int row = idx / 6, col = idx % 6;
                float x = -2.35f + col * .94f + (row == 1 ? .22f : 0f);
                float z = row == 0 ? 7.65f : 6.10f;
                var entity = new VibeCoderEntity {
                    Id = 910 + idx,
                    Name = sp.Name,
                    SpeciesId = sp.Id,
                    Rarity = sp.Rarity,
                    Position = new(x, 0f, z),
                    FacingYaw = (col - 2.5f) * -.10f,
                    State = CoderAIState.WalkingToBait,
                    IsCombatCoder = true,
                    Aggro = false,
                    WalkCycle = 0f,
                    HoodieColor = sp.HoodieColor,
                    GlassesColor = sp.GlassesColor,
                    Hp = 50,
                    MaxHp = 50
                };
                _world.ActiveCoders.Add(entity);
            }
            _playerPos = new(0f, 1.56f, 11.40f);
            LookAtTarget(new(0f, 1.04f, 6.85f));
            capture("62_part2_12_collectible_coders_lineup.png");

            // Close-up hero showcase of Vibe-Pirate, Punk Cyborg & Sleepy TeamLead at 1.85m for facial & outfit detail inspection!
            _world.ActiveCoders.Clear();
            var hPirate = new VibeCoderEntity { Id=930, SpeciesId="tab", Rarity=VibeCoderRarity.Common, Position=new(-0.82f, 0f, 7.2f), FacingYaw=.18f, State=CoderAIState.WalkingToBait, IsCombatCoder=true, Aggro=false, Hp=50, MaxHp=50 };
            var hCyborg = new VibeCoderEntity { Id=931, SpeciesId="refactor", Rarity=VibeCoderRarity.Rare, Position=new(0.0f, 0f, 7.1f), FacingYaw=0f, State=CoderAIState.WalkingToBait, IsCombatCoder=true, Aggro=false, Hp=50, MaxHp=50 };
            var hSleepy = new VibeCoderEntity { Id=932, SpeciesId="call", Rarity=VibeCoderRarity.Rare, Position=new(0.82f, 0f, 7.2f), FacingYaw=-.18f, State=CoderAIState.WalkingToBait, IsCombatCoder=true, Aggro=false, Hp=50, MaxHp=50 };
            _world.ActiveCoders.AddRange([hPirate, hCyborg, hSleepy]);
            _playerPos = new(0f, 1.40f, 9.05f);
            LookAtTarget(new(0f, 1.15f, 7.15f));
            capture("63_part2_hero_trio_closeup.png");

            // Close-up hero showcase #2: Smoothie, Wizard & Skolkovo at 1.85m
            _world.ActiveCoders.Clear();
            var hSmoothie = new VibeCoderEntity { Id=933, SpeciesId="smoothie", Rarity=VibeCoderRarity.Rare, Position=new(-0.82f, 0f, 7.2f), FacingYaw=.18f, State=CoderAIState.WalkingToBait, IsCombatCoder=true, Aggro=false, Hp=50, MaxHp=50 };
            var hWizard   = new VibeCoderEntity { Id=934, SpeciesId="framework", Rarity=VibeCoderRarity.Epic, Position=new(0.0f, 0f, 7.1f), FacingYaw=0f, State=CoderAIState.WalkingToBait, IsCombatCoder=true, Aggro=false, Hp=50, MaxHp=50 };
            var hSkolkovo = new VibeCoderEntity { Id=935, SpeciesId="skolkovo", Rarity=VibeCoderRarity.Epic, Position=new(0.82f, 0f, 7.2f), FacingYaw=-.18f, State=CoderAIState.WalkingToBait, IsCombatCoder=true, Aggro=false, Hp=50, MaxHp=50 };
            _world.ActiveCoders.AddRange([hSmoothie, hWizard, hSkolkovo]);
            _playerPos = new(0f, 1.40f, 9.05f);
            LookAtTarget(new(0f, 1.15f, 7.15f));
            capture("64_part2_hero_trio2_closeup.png");

            // [V] Middle-finger emote live showcase in front of Vibe-Pirate & Punk Cyborg!
            _world.ActiveCoders.Clear();
            var fPirate = new VibeCoderEntity { Id=940, SpeciesId="tab", Name="Вайб-Пират «Крюк-Tab»", Rarity=VibeCoderRarity.Common, Position=new(0.0f, 0f, 7.15f), FacingYaw=0f, State=CoderAIState.WalkingToBait, IsCombatCoder=true, Aggro=false, Hp=50, MaxHp=50 };
            _world.ActiveCoders.Add(fPirate);
            _playerPos = new(0f, 1.46f, 9.10f);
            LookAtTarget(new(0f, 1.28f, 7.15f));
            TriggerFlipOffEmote();
            _weapons.UpdateWeapons(0.28f, BuildPlayerCamera());
            capture("65_flipoff_emote_v.png");
            StyleComboSystem.TriggerVisualDemo();
            capture("66_style_combo_multiplier.png");
            StyleComboSystem.Reset();
        }
        ResetNewGameFloor(greet:false);
    }
}
