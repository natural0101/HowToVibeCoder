#nullable enable
using System.Diagnostics;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public static partial class Program
{
    private static void CheckThreePlayerLobby()
    {
        using var first=new CoopNetworkSystem();
        using var second=new CoopNetworkSystem();
        using var fourth=new CoopNetworkSystem();
        var peers=new[]{Net,first,second};
        var seen=new Dictionary<int,PlayerPose>[] {new(),new(),new()};
        PlayerPose[] poses=[new(new(0,1.72f,10),MathF.PI,0,WeaponType.BareHands),
            new(new(-1.05f,1.72f,6.8f),0,.1f,WeaponType.MicroUzi),
            new(new(1.05f,1.72f,6.8f),0,-.1f,WeaponType.Belt,.5f)];
        FloorSnapshot?[] floors=new FloorSnapshot?[3];
        bool sawSecondFire=false;
        first.OnMessage=(slot,m)=>
        {
            if(m.Kind=="floor") floors[1]=m.Floor;
            if(slot==2 && m.Kind=="fire" && m.Pose==poses[2]) sawSecondFire=true;
        };
        second.OnMessage=(_,m)=> { if(m.Kind=="floor") floors[2]=m.Floor; };
        first.OnPose=(slot,pose)=>seen[1][slot]=pose;
        second.OnPose=(slot,pose)=>seen[2][slot]=pose;
        _playerPos=poses[0].Eye; _playerYaw=poses[0].Yaw; _playerPitch=0;
        poses[0]=LocalPose;
        var enemy=_world.SpawnFighter(CoderArchetype.JuniorPrompter,new(25,0,18));
        enemy.Hp=enemy.MaxHp*.4f;
        var boss=_world.ElevatorBoss;
        boss.IsActive=true; boss.Hp=boss.MaxHp*.6f;
        float original=enemy.MaxHp, originalBoss=boss.MaxHp;
        void CheckScale(int n,float e,float b,int target)
        {
            var balance=Floor.Balance;
            var (cadence,cap,heal)=n switch {2=>(1.12f,1.45f,.85f),3=>(1.22f,1.85f,.72f),_=>(1f,1f,1f)};
            if(balance.BossCadenceScale!=cadence || balance.SpawnCapScale!=cap || balance.HealCooldownScale!=heal ||
                Net.ActivePlayerCount!=n || PausePlayerCount!=$"{n}/3" || balance.EnemyHpScale!=e || balance.BossHpScale!=b ||
                balance.QuarantineUnlockTarget!=target || Math.Abs(enemy.MaxHp-original*e)>.01f ||
                Math.Abs(boss.MaxHp-originalBoss*b)>.01f || Math.Abs(enemy.Hp/enemy.MaxHp-.4f)>.001f ||
                Math.Abs(boss.Hp/boss.MaxHp-.6f)>.001f) throw new InvalidDataException($"Live balance {n}");
            Console.WriteLine(FormattableString.Invariant($"COOP PASS {n}/3 enemy x{e:F2} boss x{b:F2} quarantine {target} HP 40%/60%"));
        }
        void Until(Func<bool> condition)
        {
            var timer=Stopwatch.StartNew();
            while(timer.Elapsed.TotalSeconds<8)
            {
                Net.Pump(poses[0],CaptureForSlot);
                first.Pump(poses[1],CaptureFloor);
                second.Pump(poses[2],CaptureFloor);
                fourth.Pump(poses[2],CaptureFloor);
                foreach(var ally in _world.Teammates) seen[0][ally.Slot]=ally.Pose;
                if(condition()) return;
                Thread.Sleep(3);
            }
            throw new TimeoutException($"Trio roster {Net.ActivePlayerCount}, {first.Status}, {second.Status}, {fourth.Status}");
        }
        const string address="127.0.0.1";
        CheckScale(1,1,1,3);
        first.Join(address); Until(()=>first.Ready); CheckScale(2,1.35f,1.55f,4);
        second.Join(address);
        Until(()=>peers.All(p=>p.ActivePlayerCount==3) &&
            Enumerable.Range(0,3).All(i=>peers[i].LocalSlot==i &&
                Enumerable.Range(0,3).All(j=>i==j || seen[i].GetValueOrDefault(j)==poses[j])));
        CheckScale(3,1.70f,2.10f,5);
        Console.WriteLine("COOP PASS slots 0,1,2 each received both remote poses, rotations, weapons and swing");
        _playerCash=35;
        second.Send(new("floor-action",Text:"51"));
        Until(()=>floors[1]?.Systems?.BaitStock[1]==1 && floors[2]?.Cash==0);
        var baitPose=poses[2] with {Eye=new(-5.8f,1.72f,8.1f),Yaw=MathF.PI};
        second.Send(new("drop-bait",baitPose));
        Until(()=>floors[1]?.Systems?.PlacedBait?.Tier==1 && floors[2]?.Systems?.BaitStock[1]==0);
        Console.WriteLine("COOP PASS guest bought and dropped latte bait, both guests received floor position and stock");
        health[1]=64; health[2]=37;
        escorts[2]=new VibeCoderEntity {Id=987};
        MarkDown(1);
        Until(()=>floors[1] is {GuestHp:64,Carried:null,Downed:[false,true,false],Escorts:[null,null,{Id:987}]} &&
                  floors[2] is {GuestHp:37,Carried.Id:987,Health:[100,64,37],Downed:[false,true,false]});
        escorts[2]=null; Array.Fill(health,100); Array.Clear(downUntil);
        Console.WriteLine("COOP PASS personalized HP, downed and captive snapshots reached both guests");
        Floor.OwnedWeaponsMask|=1<<(int)poses[2].Weapon;
        second.Send(new("fire",poses[2]));
        Until(()=>sawSecondFire);
        fourth.Discover();
        Until(()=>fourth.LobbyCount==3 && fourth.DiscoveredAddress!=null);
        Console.WriteLine("COOP PASS guest 1 observed guest 2 attack; LAN discovery advertised 3/3");
        var language=Loc.Current;
        Loc.Set(GameLang.En,false);
        fourth.Join(address);
        Until(()=>!fourth.Guest && fourth.Status==Loc.Tr(CoopNetworkSystem.Full));
        if(fourth.Status!="Lobby full (max 3 players)") throw new IOException(fourth.Status);
        Loc.Set(GameLang.Ru,false);
        fourth.Join(address);
        Until(()=>!fourth.Guest && fourth.Status=="Лобби заполнено (макс. 3 игрока)");
        Loc.Set(language,false);
        poses[1]=poses[1] with {Pitch=.2f};
        Until(()=>seen[0][1]==poses[1] && seen[2][1]==poses[1] && peers.All(p=>p.ActivePlayerCount==3));
        Console.WriteLine("COOP PASS fourth rejected: Lobby full (max 3 players); all three still connected");
        boss.IsActive=false;
        Floor.Onboarding=OnboardingStep.Done;
        AudioMusicSystem.VoiceTimer=_hudStatusTimer=0;
        DrawFrame(); DrawFrame();
        var image=Raylib.LoadImageFromScreen();
        try
        {
            var path=Path.Combine(Directory.GetCurrentDirectory(),"screenshots","43_coop_3players_hud.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if(image.Width!=1280 || image.Height!=720 || !Raylib.ExportImage(image,path)) throw new IOException(path);
        }
        finally { Raylib.UnloadImage(image); }
        boss.IsActive=true;
        second.Dispose(); Until(()=>Net.ActivePlayerCount==2 && first.ActivePlayerCount==2);
        CheckScale(2,1.35f,1.55f,4);
        first.Dispose(); Until(()=>Net.ActivePlayerCount==1);
        CheckScale(1,1,1,3);
        ResetNewGameFloor(greet:false);
    }

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
            if(!guest && _world.ElevatorBoss.State is ElevatorBossState.Repairing or ElevatorBossState.IntroSpeech or ElevatorBossState.Dying)
                TickFloor((float)Math.Min(.15,now-previousTime));
            previousTime=now;
            Net.Pump(LocalPose,CaptureForSlot);
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
    private static void CheckCoopSkinAward(bool guest)
    {
        if(!guest)
        {
            _world.CarriedCoder=new VibeCoderEntity { Rarity=VibeCoderRarity.Uncommon,TarotPayoutValue=45 };
            _world.SellCarriedCoderToHrVeronika(GrantCash);
        }
        var timer=Stopwatch.StartNew();
        bool requested=false;
        while(timer.Elapsed.TotalSeconds<12)
        {
            Net.Pump(LocalPose,CaptureForSlot);
            if(_world.Slot.Active)
            {
                var (row,skin)=_world.Slot.Prize;
                var weapon=FloorSystems.WeaponOrder[row];
                if(guest && !requested && Floor.HasSkin(weapon,skin))
                {
                    Net.Send(new("skin-equip",Amount:(int)weapon*16+skin)); requested=true;
                }
                if(Floor.EquippedSkins[(int)weapon]==skin)
                {
                    Console.WriteLine($"COOP PASS {(guest ? "guest" : "host")}: shared skin award and guest equip");
                    if(guest) return;
                    // Allow the next authoritative snapshot to reach the guest.
                    var until=timer.Elapsed.TotalSeconds+.6;
                    while(timer.Elapsed.TotalSeconds<until) {Net.Pump(LocalPose,CaptureForSlot); DrawFrame();}
                    return;
                }
            }
            DrawFrame();
        }
        throw new TimeoutException("Shared skin award");
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
            Net.Pump(LocalPose,CaptureForSlot);
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
                else if(phase==1 && _world.CarriedCoder!=null)
                {
                    if(_weapons.CurrentWeapon!=WeaponType.BareHands) throw new InvalidDataException("Guest auto-holster");
                    phase++;
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
                else if(phase==6 && _world.ActiveCoders.Any(c=>c.Position==target && MathF.Abs(c.Hp-(c.MaxHp-12))<.01f) &&
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
                    _world.ActiveCoders.Any(c=>c.Position==target && MathF.Abs(c.Hp-(c.MaxHp-12))<.01f))
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
