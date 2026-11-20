#nullable enable
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public static partial class Program
{
    private static unsafe void CheckSprintB(Action<string> capture)
    {
        static void Check(bool ok,string label)
        {
            if(!ok) throw new InvalidDataException("Sprint B: "+label);
        }
        bool exit=false;
        void Frame() { UpdateFrame(1f/60,ref exit); Raylib.PollInputEvents(); }
        void Input(uint type,int a,int b=0)
        {
            AutomationEvent e=new() {Type=type}; e.Params[0]=a; e.Params[1]=b;
            Raylib.PlayAutomationEvent(e);
        }
        void Key(KeyboardKey k) { Input(2,(int)k); Frame(); Input(1,(int)k); Raylib.PollInputEvents(); }
        void Aim(Vector3 feet,float h,float range=2)
        {
            _playerPos=feet+new Vector3(0,1.72f,range);
            LookAtTarget(feet+Vector3.UnitY*h);
        }
        void Shoot(WeaponType tool,Vector3 point)
        {
            _weapons.Equip(tool); LookAtTarget(point);
            _world.ProcessPlayerAttack(BuildPlayerCamera(),_weapons,GrantCash,out _);
        }
        void Interact() => _world.Interact(BuildPlayerCamera(),TrySpendCash,GrantCash,_weapons.CurrentWeapon);
        void Tick(float dt) => _world.UpdateWorld(dt,_playerPos,false,GrantCash,null);


        ResetClassicFixture();
        var coffee=SkyscraperFloor1World.CoffeePos;
        var bystander=_world.ActiveCoders[0];
        bystander.Position=coffee+new Vector3(1.5f,0,.6f);
        Aim(coffee,1.1f); Shoot(WeaponType.SolderingIron,coffee+Vector3.UnitY*1.1f);
        Check(Floor.Steam==8,"iron hits coffee machine");
        Tick(.1f); Check(bystander.HitStaggerTimer>0 && _world.InCloud(_playerPos),"steam staggers and conceals");
        float sec=Floor.SEC; Shoot(WeaponType.MicroUzi,_playerPos+Vector3.UnitZ*3);
        Check(Floor.SEC==sec,"cloud conceals gunfire");
        _playerPos=coffee+new Vector3(1.65f,1.6f,2.65f); LookAtTarget(coffee+Vector3.UnitY*1.15f);
        _weapons.Equip(WeaponType.SolderingIron); _hudStatusTimer=0;
        for(int i=0;i<35;i++) _weapons.UpdateWeapons(1f/60,BuildPlayerCamera());
        capture("18_coffee_steam_combo.png");
        VisualEngine3D.CheckStationFallbacks(()=>DrawFrame());
        Aim(coffee,1.15f); Interact(); Check(Floor.Coffee==12,"espresso");
        _playerPos=new(24,1.72f,20);
        var start=_playerPos; MovePlayer(.2f,Vector3.UnitX,Vector3.UnitX,false,false,false);
        Check(Math.Abs(_playerPos.X-start.X-6.6f*1.35f*.2f)<.001f,"espresso speed");
        bystander.Position=new(24,0,17); Aim(bystander.Position,.8f); Shoot(WeaponType.Belt,bystander.CombatCenter);
        Vector3 beltAim=Vector3.Normalize(BuildPlayerCamera().Target-BuildPlayerCamera().Position);
        Check(Math.Abs(bystander.KnockbackVel.Length()-14*new Vector2(beltAim.X,beltAim.Z).Length())<.001f,"espresso doubles belt impulse");
        var extinguisher=_world.ExtinguisherPos;
        Floor.PT=68; Floor.FireIncident=true;
        Aim(extinguisher,.62f); Shoot(WeaponType.MicroUzi,extinguisher+Vector3.UnitY*.62f);
        Check(Floor.RackSmoke==9 && Floor.PT==46 && !Floor.FireIncident,"shoot extinguisher");
        Interact(); Check(Floor.PT==46,"single discharge");
        Tick(9); Check(Floor.RackSmoke==0 && Floor.Steam==0,"cloud expiry");

        ResetClassicFixture();
        Floor.SEC=55; Floor.Tick(.01f,_world,null); Frame();
        Check(floorPanel==FloorPanel.None && Floor.Raid && _world.ActiveCoders.Any(c=>c.RaidGuard && c.EntryRoom==3),"live east-wing inspection");
        int wallet;
        var guard=_world.ActiveCoders.Last(c=>c.Archetype==CoderArchetype.SecurityGuard);
        guard.Position=new(24,0,17); Aim(guard.Position,.82f);
        float guardZ=guard.Position.Z; Tick(.05f);
        Check(guard.Position.Z>guardZ,"guard closes distance");
        for(int i=0;i<14 && guard.Windup<=0;i++) Tick(.05f);
        Check(guard.Windup>0,"telegraphed baton windup");
        for(int i=0;i<9 && !PlayerStunned;i++) Tick(.05f);
        Check(PlayerStunned,"baton stun");
        start=_playerPos; MovePlayer(.1f,Vector3.UnitX,Vector3.UnitX,true,true,true);
        Check(Vector3.Distance(start,_playerPos)<.01f && _dashTime==0,"baton blocks escape movement");
        Aim(guard.Position,.82f); wallet=_playerCash; Interact();
        Check(Floor.Protection && _world.CarriedCoder==null && _playerCash==wallet-80+165 && Floor.DiscoveredSpecies.ContainsKey("sb_chief"),"bribed guard becomes protection");
        sec=Floor.SEC; Floor.Tick(1,_world,null); Check(Math.Abs(sec-Floor.SEC-1.2f)<.01f,"passive protection");

        ResetClassicFixture();
        _playerCash=175; Key(KeyboardKey.U); Key(KeyboardKey.Two);
        Key(KeyboardKey.Up);
        Check(floorRow==(int)Sidegrade.CarryHarness,"up wraps to last card in column");
        Key(KeyboardKey.Enter);
        Check(_playerCash==0 && Floor.Has(Sidegrade.CarryHarness),"harness costs $175");
        Key(KeyboardKey.Escape);
        var escort=_world.SpawnFighter(CoderArchetype.JuniorPrompter,new(24,0,17));
        escort.State=CoderAIState.Subdued;
        _weapons.Equip(WeaponType.CaptureNet);
        Aim(escort.Position,.45f); Interact();
        Check(_world.CarriedCoder==escort && escort.IdentityRevealed && _weapons.CurrentWeapon==WeaponType.CaptureNet,"armed harness NDA");
        var mark=_world.SpawnFighter(CoderArchetype.SeniorArchitect,escort.Position-Vector3.UnitZ);
        float beforeShot=mark.Hp;
        Shoot(WeaponType.MicroUzi,mark.CombatCenter);
        Check(mark.Hp==beforeShot-18 && _world.CarriedCoder==escort,"fire Uzi during escort");
        Aim(_world.HrTarotBoothPos,1.6f); Interact();
        Check(_world.TotalCodersSoldToHr==1 && _world.CarriedCoder==null,"armed HR delivery");
        ResetClassicFixture();
        void Click(Rectangle rect)
        {
            Vector2 point=PanelOffset+PanelScale*new Vector2(rect.X+rect.Width/2,rect.Y+rect.Height/2);
            Input(7,(int)point.X,(int)point.Y); Input(6,(int)MouseButton.Left); Frame();
            Input(5,(int)MouseButton.Left); Frame();
        }
        _playerCash=5000; Key(KeyboardKey.U);
        Click(ArsenalTab(1)); Check(sidegrades,"mouse opens upgrades");
        Click(FloorRowRect(3)); Check(floorRow==3 && !Floor.Has(Sidegrade.Drum),"first click selects card");
        Click(FloorRowRect(3)); Check(Floor.Has(Sidegrade.Drum) && _playerCash==4680,"second click buys drum once");
        Click(FloorRowRect(9)); Click(ShopPurchase);
        Check(Floor.Has(Sidegrade.PortableCo2) && _playerCash==4490,"purchase ticket button");
        Click(ArsenalTab(0)); Check(!sidegrades,"mouse opens weapons");
        Key(KeyboardKey.Escape); ResetClassicFixture();
        _playerCash=5000; Key(KeyboardKey.U); Key(KeyboardKey.Two);
        Check(sidegrades && FloorSystems.GearList.Length==11 && FloorRowRect(10).Y+FloorRowRect(10).Height==610,"all eleven cards fit");
        for(int i=0;i<FloorSystems.GearList.Length;i++)
        {
            Check(floorRow==i && FloorRowRect(i).Equals(new Rectangle(788+202*(i%2),150+78*(i/2),190,70)),"visible upgrade "+i);
            wallet=_playerCash;
            int cost=Floor.Price(FloorSystems.GearList[i]);
            Key(KeyboardKey.Enter);
            Check(Floor.Has((Sidegrade)i) && _playerCash==wallet-cost,"purchase row "+i);
            if(i%2==0) Key(KeyboardKey.Right);
            else { Key(KeyboardKey.Left); Key(KeyboardKey.Down); }
        }
        Check(Floor.Price(FloorSystems.Weapons[4])==552 && Floor.Price(FloorSystems.GearList[9])==219,"signet surcharge");
        Key(KeyboardKey.Escape); Floor.PT=60; Key(KeyboardKey.Q);
        Check(Floor.PocketSmoke>3.9f && Floor.PocketRecharge>24.9f && Floor.PT==45 && _dashTime==0,"portable CO2 on Q");
        Key(KeyboardKey.Q); Check(Floor.PT==45,"portable cooldown");
        Floor.Authority=0; Floor.Style(StyleAction.Iron);
        Check(Math.Abs(Floor.Authority-7.8f)<.01f && Floor.FearMultiplier==1.3f,"signet authority and fear");
        var rack=_world.RackPos;
        Aim(rack,1); Shoot(WeaponType.SolderingIron,rack+Vector3.UnitY);
        Check(Floor.PT==69,"napalm doubles rack heating");
        var candidate=_world.SpawnFighter(CoderArchetype.SeniorArchitect,new(24,0,17));
        Aim(candidate.Position,.8f); Shoot(WeaponType.SolderingIron,candidate.CombatCenter);
        float hp=candidate.Hp; Tick(.1f);
        Check(hp<candidate.MaxHp && candidate.Ignition>0 && candidate.Hp<hp && candidate.Ego<35,"live incendiary and doubled capture pressure");
        Shoot(WeaponType.Belt,candidate.CombatCenter);
        Check(candidate.State==CoderAIState.Subdued,"signet and carbon NDA break");
        sec=Floor.SEC; Aim(candidate.Position,.45f); Interact();
        Check(_world.CarriedCoder==candidate && Math.Abs(Floor.SEC-sec-6.5f)<.01f,"carbon NDA security cost");

        Aim(_world.HrTarotBoothPos,1.6f); Interact();
        _world.IsMacBookTrapActive=false;
        _world.ActivateMacBookTrap(SubscriptionBaitTier.FreeTier,TrySpendCash,out _);
        var intern=_world.ActiveCoders.First(c=>c.TimidIntern);
        intern.Position=new(24,0,17); Aim(intern.Position,.8f);
        sec=Floor.SEC; Shoot(WeaponType.CaptureNet,intern.CombatCenter);
        Shoot(WeaponType.CaptureNet,intern.CombatCenter);
        Check(_world.CarriedCoder==intern && Floor.SEC>=sec+6,"carbon net signs intern immediately");
        ResetClassicFixture();
        var pm=_world.SpawnFighter(CoderArchetype.ToxicPM,new(24,0,17));
        var ally=_world.ActiveCoders[0]; ally.Position=pm.Position+Vector3.UnitX*2;
        _playerPos=new(24,1.72f,20); pm.AttackCooldown=0; Tick(.1f);
        Check(pm.Deadline>9 && ally.Deadline>9 && pm.Position.Z<17 && Floor.PT>=4,"PM retreats and marks allies");
        var seats=_world.GalleyDesks;
        void Seat(int i,CoderArchetype role)
        { seats[i].Archetype=role; seats[i].IsOccupied=true; seats[i].SlackCycleTimer=100; }
        Seat(0,CoderArchetype.SeniorArchitect); Seat(1,CoderArchetype.JuniorPrompter); Seat(2,CoderArchetype.ToxicPM);
        Tick(1);
        Check(Floor.Conflict && Math.Abs(seats[1].Morale-.9f)<.001f && Math.Abs(_world.DeskRate(seats[1],false)-seats[1].Productivity*.9f*1.2f)<.001f,"galley conflict and PM revenue");
        float withoutLead=seats[1].Burnout;
        Aim(coffee,1.15f); Interact();
        Check(!Floor.Conflict && seats[1].Morale==1 && seats[1].Burnout==0,"espresso settles conflict");
        Floor.Coffee=Floor.ConflictWait=0; Tick(1); Seat(3,CoderArchetype.TeamLeadMeeter); Tick(1);
        Check(!Floor.Conflict && seats[1].Morale==1 && Math.Abs(seats[1].Burnout-withoutLead*1.5f)<.001f,"lead calms conflict and halves burnout");
        Floor.Start(0,_world); Floor.Seated=2; Floor.Earned=450; Floor.Tick(.01f,_world,null);
        Check(Math.Abs(Floor.Report!.Bonus-1.2f)<.001f,"PM contract premium");

        ResetClassicFixture();
        _world.HasElevatorKey=true;
        _world.TrySummonElevatorBoss(out _);
        FinishElevatorIntro();
        var boss=_world.ElevatorBoss;
        boss.Position=new(24,0,17); boss.FacingYaw=0;
        boss.State=ElevatorBossState.StunnedWallCrash; boss.StateTimer=2.6f;
        Aim(boss.Position,1.4f,4);
        int crash=_world.Frogs.Count(f=>f.SpeciesId=="frog_crash");
        void Burst() => Shoot(WeaponType.MicroUzi,boss.ChestCore);
        void Core() => Shoot(WeaponType.SawedOff,boss.ChestCore);
        Burst(); Check(boss.Phase==1 && boss.Hp==boss.MaxHp && boss.ShieldHp==100 && boss.MaxHp==600,"phase one shield");
        boss.ShieldDelay=.01f; Tick(.1f); Check(boss.ShieldHp>100,"firewall regenerates");
        Burst(); Burst(); Burst(); Check(boss.ShieldHp==0,"Uzi overload");
        float heat=Floor.PT;
        hp=boss.Hp; Core(); Check(hp-boss.Hp>=345,"exposed chest hit");
        for(int i=0;i<4 && boss.Phase==1;i++) Core();
        Check(boss.Phase==2 && Floor.PT==heat+18 && Floor.RackHeat>0 && boss.ShieldHp>0 &&
            _world.Frogs.Count(f=>f.SpeciesId=="frog_crash")==crash+2,"overclock and two crash frogs");
        _world.SpawnNodeModulesCube(boss.Position+Vector3.UnitY*2,_playerPos);
        Check(_world.EnemyProjectiles[^1].Velocity.Length()>14.9f,"overclock projectile speed");
        boss.Position=rack+new Vector3(-1.5f,0,-.4f); Floor.FireIncident=true; heat=Floor.PT;
        Aim(extinguisher,.65f); Interact();
        Check(Floor.RackSmoke==9 && !Floor.FireIncident && Floor.PT==Math.Max(0,heat-22) &&
            boss.State==ElevatorBossState.StunnedWallCrash && boss.ShieldHp==0,"E discharge shorts boss");
        _playerHp=100; // This scenario uses the full-health spill interaction.
        var cooler=_world.CoolerPos; Aim(cooler,.9f); Interact();
        Check(Floor.Puddles.Count>0,$"cooler interaction hp={_playerHp} focus={_world.FocusPrompt(BuildPlayerCamera())}");
        var water=cooler+new Vector3(1.45f,0,-.8f);
        _playerPos=cooler+new Vector3(2.80f,1.72f,-1.30f); Shoot(WeaponType.SolderingIron,water);
        boss.Position=cooler+Vector3.UnitZ*1.6f; boss.ShieldHp=100;
        Aim(boss.Position,1.4f,3); Burst();
        Check(Floor.Puddles.Any(p=>p.Electric>0) && boss.ShieldHp==0 && boss.StateTimer==3,$"hit in live puddle electric={Floor.Puddles.FirstOrDefault()?.Electric} shield={boss.ShieldHp} timer={boss.StateTimer}");
        boss.Position=new(24,0,17); Aim(boss.Position,1.4f,4);
        for(int i=0;i<5 && boss.Phase==2;i++) Core();
        Check(boss.Phase==3 && boss.ShieldHp==0 && _world.CurrentTape.Contains("директора"),"SYSTEM ZERO broadcast");
        boss.BroadcastTime=.01f; Tick(.02f); Check(_world.CurrentTape.Contains("телефонных"),"Petrovich tapes");
        boss.BroadcastTime=.01f; Tick(.02f); Check(_world.CurrentTape.Contains("выйти"),"escape revelation");
        wallet=_playerCash;
        for(int i=0;i<5 && boss.IsActive;i++) Core();
        Check(boss.IsDefeated && Floor.FinalePending && _world.IsFloor2Unlocked && Floor.DiscoveredSpecies["ai_agent_v01"]==1 && _playerCash==wallet+350,"shutdown and discovery only once");
        Floor.Tick(.9f,_world,null); Frame();
        Check(floorPanel==FloorPanel.BossFinale && LoreAndMenusUI.UnlockedChats==6,"disposition awaits signature");
        capture("17_boss_finale.png");

        var data=MainMenuAndPauseUI.CaptureCurrentGameState!();
        string slot=Path.GetTempFileName();
        try
        {
            File.WriteAllText(slot,System.Text.Json.JsonSerializer.Serialize(data)); WriteFloorSave(data,slot,pendingShift!);
            wallet=_playerCash; Key(KeyboardKey.One);
            Check(floorPanel==FloorPanel.None && _playerCash==wallet+1500 && Floor.Verdict==AgentVerdict.Contained,"containment reward");
            float debt=Floor.TD; wallet=_playerCash; Tick(1);
            Check(_playerCash==wallet+14 && Floor.TD>debt,"automated node works on empty galley");
            Floor.ResolveFinale(1,GrantCash); Check(_playerCash==wallet+14 && Floor.Verdict==AgentVerdict.Contained,"verdict irreversible");
            ResetClassicFixture(); _playerCash=data.Cash; RestoreFloorSave(data,slot);
            Frame(); Check(floorPanel==FloorPanel.BossFinale,"pending verdict survives reload");
            wallet=_playerCash; Key(KeyboardKey.Two);
            Check(_playerCash==wallet+900 && Floor.SEC==0 && Floor.Verdict==AgentVerdict.Released && LoreAndMenusUI.UnlockedChats==7,"release reward and secret archive");
            Key(KeyboardKey.N); Key(KeyboardKey.One);
            for(int i=0;i<6;i++) Key(KeyboardKey.Down);
            Check(LoreAndMenusUI.SelectedArchiveEntry==6,"seventh chat reachable by keyboard");
            DrawFrame(); Key(KeyboardKey.N);
            Floor.Risk(10,10,10); Check(Floor.SEC==8,"Artel reputation");
            data=MainMenuAndPauseUI.CaptureCurrentGameState!();
            WriteFloorSave(data,slot,pendingShift!);
            ResetClassicFixture(); RestoreFloorSave(data,slot);
            Check(Floor.Verdict==AgentVerdict.Released && !Floor.FinalePending && Floor.DiscoveredSpecies["ai_agent_v01"]==1 && LoreAndMenusUI.UnlockedChats==7,"permanent release and discovery");
        }
        finally { File.Delete(slot); pendingShift=null; pendingFloorSave=null; }
        Check(VibeCoderCollection.All.Length==24 && VibeCoderCollection.Frogs.Values.Select(f=>f.SalePrice).Order().SequenceEqual(new[]{1,2,4,8}),"catalogue and frog sale prices");
        ResetClassicFixture();
        Console.WriteLine("[AUTOTEST] Sprint B PASS: live east-wing inspection, office combos, both prop fallbacks, galley conflict, patrol, eleven purchases, armed harness escort, 24 species, three boss phases, both verdicts and save/load");
    }
}

internal static partial class VisualEngine3D
{
    internal static void CheckStationFallbacks(Action drawFrame)
    {
        // Keep the native handles alive while the regular scene draws with optional props absent.
        var saved=new Dictionary<string,Model>();
        try
        {
            foreach(string name in new[]{"prop_coffee_machine","prop_extinguisher"})
                if(imports.Remove(name,out var model)) saved.Add(name,model);
            drawFrame();
        }
        finally
        {
            foreach(var (name,model) in saved) imports.Add(name,model);
        }
    }
}
