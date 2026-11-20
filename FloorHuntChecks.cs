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

            Vector3 cabinPanel=_world.CentralElevatorCorePos+new Vector3(2.41f,1.38f,-4.166f);
            _playerPos=cabinPanel+new Vector3(-.14f,.04f,.60f);
            LookAtTarget(cabinPanel);
            capture("58_elevator_cabin_panel_closeup.png");
        }
        ResetNewGameFloor(greet:false);
    }
}
