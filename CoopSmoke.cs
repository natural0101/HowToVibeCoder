using System.Diagnostics;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public static partial class Program
{
    // Run two real game processes. The guest buys the lift key and fires at the
    // host's boss. Both processes must observe both players' shots and the payout.
    private static void RunCoopSmoke(bool guest)
    {
        if(Raylib.IsAudioDeviceReady() && Raylib.GetMasterVolume()!=0) throw new InvalidDataException("Coop smoke audio must be muted");
        var timer=Stopwatch.StartNew();
        int phase=0;
        double finishAt=0, previousTime=0;
        bool sawRepair=false, sawReveal=false, ordered=false;
        if(!guest)
        {
            if(!Net.Host) throw new IOException(Net.Status);
            Floor.Onboarding=OnboardingStep.Done;
            GrantCash(1000,"Проверка коопа");
            _playerPos=new(-2.1f,1.72f,-12.3f);
            _playerYaw=.32f;
            _weapons.SelectSlot(3);
        }
        while(timer.Elapsed.TotalSeconds<35)
        {
            double now=timer.Elapsed.TotalSeconds;
            if(!guest && _world.ElevatorBoss.State is ElevatorBossState.Repairing or ElevatorBossState.IntroSpeech)
                TickFloor((float)Math.Min(.15,now-previousTime));
            previousTime=now;
            Net.Pump(LocalPose,CaptureFloor);
            sawRepair |= _world.ElevatorRepaired && _world.ElevatorBoss.State==ElevatorBossState.Repairing && !_world.BossRevealed;
            sawReveal |= _world.ElevatorBoss.State==ElevatorBossState.IntroSpeech && _world.ElevatorGlassOpacity==0 && _world.ElevatorDoorOpenAmount>.5f;
            if(Net.Ready && Net.HasPeerPose && _world.Partner is {} ally)
            {
                if(guest)
                {
                    if(!ordered && phase==0 && _playerCash==1450 && Math.Abs(ally.Pose.Eye.X+2.1f)<.01f &&
                       Math.Abs(ally.Pose.Yaw-.32f)<.01f && ally.Pose.Weapon==WeaponType.MicroUzi)
                    {
                        OpenKeycardRow();
                        PressTestKey(KeyboardKey.Enter);
                        PressTestKey(KeyboardKey.Enter);
                        ordered=true;
                    }
                    if(phase==0 && ordered && _world.HasElevatorKey && _playerCash==450 && _world.BrokenElevator)
                    {
                        PressTestKey(KeyboardKey.Escape);
                        _playerPos=new(1,1.72f,-11.4f);
                        _weapons.SelectSlot(4);
                        LookAtTarget(_world.NorthElevatorPos+Vector3.UnitY*1.6f);
                        PressTestKey(KeyboardKey.E);
                        phase=1;
                    }
                    if(phase==1 && sawRepair && sawReveal && _world.BossInCombat && _world.HasElevatorKey && _playerCash==450 &&
                       _world.ElevatorBoss.Hp is >0 and <220 && ally.Effects.BulletTracers.Count>0)
                    {
                        LookAtTarget(_world.ElevatorBoss.Position+Vector3.UnitY*1.35f);
                        Fire(BuildPlayerCamera());
                        phase=2;
                    }
                    if(phase==2 && Floor.FinalePending && _playerCash==800)
                    {
                        FloorAction(FinaleAction); phase=3;
                    }
                    if(phase==3 && Floor.Verdict==AgentVerdict.Contained && _world.ElevatorBoss.Hp==0 && _world.IsFloor2Unlocked && _playerCash==2300)
                    {
                        Console.WriteLine("COOP PASS guest: UDP host pose/rotation/weapon, host shot, shared key, boss defeat, cash 2300");
                        return;
                    }
                }
                else
                {
                    if(phase==0 && sawRepair && sawReveal && _world.HasElevatorKey && _world.BossInCombat && _playerCash==450 &&
                       Math.Abs(ally.Pose.Eye.X-1)<.01f && ally.Pose.Weapon==WeaponType.SawedOff)
                    {
                        _world.ElevatorBoss.Hp=120;
                        LookAtTarget(_world.ElevatorBoss.Position+Vector3.UnitY*1.35f);
                        Fire(BuildPlayerCamera());
                        phase=1;
                    }
                    if(phase==1 && _world.IsFloor2Unlocked && _playerCash==2300)
                    {
                        Console.WriteLine("COOP PASS host: UDP guest movement/weapon, key purchase, guest shot, single boss payout and discovery 2300");
                        finishAt=timer.Elapsed.TotalSeconds+1;
                        phase=2;
                    }
                }
            }
            if(!guest && phase==2 && timer.Elapsed.TotalSeconds>=finishAt) return;
            DrawFrame();
        }
        throw new TimeoutException($"Coop {(guest ? "guest":"host")}, phase {phase}, network {Net.Status}, UDP {Net.HasPeerPose}, cash {_playerCash}, boss {_world.ElevatorBoss.Hp}");
    }
    private static void CheckCoopCarryHarness(bool guest)
    {
        var pickup=new Vector3(26,0,12);
        var target=pickup-Vector3.UnitZ*1.2f;
        VibeCoderEntity StageCoder()
        {
            var coder=_world.SpawnFighter(CoderArchetype.JuniorPrompter,pickup);
            coder.State=CoderAIState.Subdued;
            return coder;
        }
        if(!guest)
        {
            _playerPos=new(20,1.72f,10);
            _playerCash=175;
            StageCoder();
        }
        int phase=0, wallet=0;
        double completed=0;
        var timer=Stopwatch.StartNew();
        while(timer.Elapsed.TotalSeconds<18)
        {
            Net.Pump(LocalPose,CaptureFloor);
            var coder=_world.ActiveCoders.Find(c=>c.Position==pickup && c.CanSignNda);
            if(guest)
            {
                if(phase==0 && coder!=null && _playerCash==175)
                {
                    if(_weapons.CurrentWeapon!=WeaponType.CaptureNet || _weapons.SelectedHotbarSlot!=0)
                        throw new InvalidDataException("Guest net was not equipped after purchase");
                    _playerPos=pickup+new Vector3(0,1.72f,1.4f);
                    LookAtTarget(pickup+Vector3.UnitY*.45f);
                    Net.Send(new("interact",LocalPose)); phase++;
                }
                else if(phase==1 && _hudStatusBanner==SkyscraperFloor1World.HandsFullMessage && coder!=null && _world.CarriedCoder==null)
                {
                    PressTestKey(KeyboardKey.One);
                    if(_weapons.CurrentWeapon!=WeaponType.BareHands || _weapons.SelectedHotbarSlot!=-1)
                        throw new InvalidDataException("Guest net holster");
                    PressTestKey(KeyboardKey.One);
                    if(_weapons.CurrentWeapon!=WeaponType.CaptureNet) throw new InvalidDataException("Guest net redraw");
                    PressTestKey(KeyboardKey.Two);
                    if(_weapons.CurrentWeapon!=WeaponType.BareHands || _weapons.SelectedHotbarSlot!=1)
                        throw new InvalidDataException("Guest empty second slot");
                    Net.Send(new("interact",LocalPose)); phase++;
                }
                else if(phase==2 && _world.CarriedCoder!=null)
                {
                    PressTestKey(KeyboardKey.One);
                    if(_weapons.CurrentWeapon!=WeaponType.BareHands) throw new InvalidDataException("Guest drew during escort");
                    _playerPos=_world.HrTarotBoothPos+new Vector3(0,1.72f,2);
                    LookAtTarget(_world.HrTarotBoothPos+Vector3.UnitY*1.6f);
                    Net.Send(new("interact",LocalPose)); phase++;
                }
                else if(phase==3 && _world.TotalCodersSoldToHr==1 && _world.CarriedCoder==null)
                {
                    wallet=_playerCash;
                    FloorAction(BuyOffset+(int)Sidegrade.CarryHarness); phase++;
                }
                else if(phase==4 && Floor.Has(Sidegrade.CarryHarness) && coder!=null)
                {
                    PressTestKey(KeyboardKey.One);
                    if(_playerCash!=wallet-175 || _weapons.CurrentWeapon!=WeaponType.CaptureNet) throw new InvalidDataException("Guest harness cost or armed pickup setup");
                    _playerPos=pickup+new Vector3(0,1.72f,1.4f);
                    LookAtTarget(pickup+Vector3.UnitY*.45f);
                    Net.Send(new("interact",LocalPose)); phase++;
                }
                else if(phase==5 && _world.CarriedCoder!=null)
                {
                    var dummy=_world.ActiveCoders.Single(c=>c.Position==target);
                    LookAtTarget(dummy.CombatCenter);
                    Fire(BuildPlayerCamera()); phase++;
                }
                else if(phase==6 && _world.ActiveCoders.Any(c=>c.Position==target && c.Hp==38) &&
                    _world.CarriedCoder!=null && _weapons.CurrentWeapon==WeaponType.CaptureNet)
                {
                    Console.WriteLine("COOP PASS guest: holster, empty slot, bare-hand delivery, armed harness pickup and attack"); return;
                }
            }
            else
            {
                if(phase==0 && _world.TotalCodersSoldToHr==1)
                {
                    StageCoder();
                    _world.SpawnFighter(CoderArchetype.JuniorPrompter,target);
                    phase++;
                }
                if(phase==1 && guestCarried!=null && Floor.Has(Sidegrade.CarryHarness) &&
                    _world.ActiveCoders.Any(c=>c.Position==target && c.Hp==38))
                { completed=timer.Elapsed.TotalSeconds; phase++; }
                if(phase==2 && timer.Elapsed.TotalSeconds-completed>.7)
                {
                    Console.WriteLine("COOP PASS host: guest delivered once, armed harness escort and damage synchronized"); return;
                }
            }
            DrawFrame();
        }
        throw new TimeoutException($"Coop harness guest={guest}, phase={phase}, cash={_playerCash}, sold={_world.TotalCodersSoldToHr}, status={_hudStatusBanner}");
    }

}
