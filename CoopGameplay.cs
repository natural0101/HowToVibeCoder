#nullable enable
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public static partial class Program
{
    private static CoopNetworkSystem Net => MainMenuAndPauseUI.Coop;
    private static bool bot, awaitingSpawn, pendingCase;
    private static int guestHp=100, hostHp=100;
    internal static int PartnerHp => Net.Guest ? hostHp : Net.Ready ? guestHp : 100;
    private static VibeCoderEntity? guestCarried, peerEscort;
    private static PlayerPose LocalPose => new(BuildPlayerCamera().Position,_playerYaw,_playerPitch,_weapons.CurrentWeapon);
    private static void StartHosting()
    {
        Net.StartHost(CoopNetworkSystem.DefaultPort);
        SetHudBanner(Net.Status);
    }

    private static void ConfigureCoop()
    {
        MainMenuAndPauseUI.SummonPartner=() =>
        {
            bot=true;
            _world.Partner=new PartnerAvatar();
            _world.Partner.Move(LocalPose with { Eye=_world.ResolvePlayerCollision(_playerPos+new Vector3(1.7f,0,-1.2f)), Weapon=WeaponType.MicroUzi });
        };
        Net.OnJoined=() =>
        {
            ResetNewGameFloor();
            awaitingSpawn=true;
            MainMenuAndPauseUI.IsPaused=MainMenuAndPauseUI.IsInMainMenu=MainMenuAndPauseUI.IsSaveLoadPanelOpen=false;
            Raylib.DisableCursor();
        };
        Net.OnPose=pose =>
        {
            if(awaitingSpawn)
            {
                _playerPos=_world.ResolvePlayerCollision(pose.Eye+new Vector3(1.6f,0,.5f));
                _playerYaw=pose.Yaw;
                awaitingSpawn=false;
            }
            bot=false;
            (_world.Partner??=new PartnerAvatar()).Move(pose);
        };
        Net.OnMessage=HandleCoopMessage;
        Net.OnLost=() =>
        {
            if(guestCarried!=null && Net.Host) _world.ActiveCoders.Add(guestCarried);
            guestCarried=peerEscort=null; guestHp=100; pendingCase=false;
            if(!bot) _world.Partner=null;
            if(!Net.Host && !MainMenuAndPauseUI.IsInMainMenu)
            {
                string reason=Net.Status;
                MainMenuAndPauseUI.OpenMainMenu();
                MainMenuAndPauseUI.OpenJoinPanel();
                MainMenuAndPauseUI.Notify(reason);
            }
        };
    }

    private static FloorSnapshot CaptureFloor() => new(_playerCash,_world.ElevatorBoss,_world.ElevatorDoorOpenAmount,
        _world.HasElevatorKey,_world.IsFloor2Unlocked,_world.IsMacBookTrapActive,_world.SelectedBaitTier,
        _world.ActiveCoders.ToArray(),_world.GalleyDesks.ToArray(),_world.EnemyProjectiles.ToArray(),
        guestCarried,_world.TotalCodersSoldToHr,guestHp,_playerHp,_world.Systems,_world.CarriedCoder);

    private static void HandleCoopMessage(CoopMessage message)
    {
        if(Net.Guest && message is {Kind:"floor", Floor:{} state})
        {
            if(state.Boss==null || state.Coders==null || state.Desks?.Length!=4 || state.Projectiles==null) return;
            if(state.Systems!=null) _world.Systems=state.Systems;
            _playerCash=state.Cash; _playerHp=state.GuestHp; hostHp=state.HostHp;
            _world.ElevatorBoss=state.Boss; _world.ElevatorDoorOpenAmount=state.Door;
            _world.HasElevatorKey=state.Key; _world.IsFloor2Unlocked=state.Unlocked;
            if(state.Unlocked) _weapons.UnlockedChatGun=true;
            _world.IsMacBookTrapActive=state.Trap; _world.SelectedBaitTier=state.Bait;
            peerEscort=state.HostCarried;
            _world.CarriedCoder=state.Carried; _world.TotalCodersSoldToHr=state.Sold;
            _world.ActiveCoders.Clear(); _world.ActiveCoders.AddRange(state.Coders);
            _world.GalleyDesks.Clear(); _world.GalleyDesks.AddRange(state.Desks);
            _world.EnemyProjectiles.Clear(); _world.EnemyProjectiles.AddRange(state.Projectiles);
            return;
        }
        if(message is {Kind:"fire", Pose:{} shooter})
        {
            if(shooter.Weapon==WeaponType.ChatGun && !_world.IsFloor2Unlocked && !_weapons.UnlockedChatGun) return;
            var partner=_world.Partner??=new PartnerAvatar();
            bot=false; partner.Move(shooter);
            partner.Attack(_world,GrantCash,Net.Host);
            if(Net.Host && _world.IsFloor2Unlocked) _weapons.UnlockedChatGun=true;
            PlayCurrentWeaponAudio(shooter.Weapon);
        }
        else if(Net.Host && message is {Kind:"interact", Pose:{} actor})
        {
            string? joint=CoopEscort(actor.Camera,true,apply:true);
            if(joint!=null) { Net.Send(new("status",Text:joint)); return; }
            var carried=_world.CarriedCoder;
            _world.CarriedCoder=guestCarried;
            string result=_world.Interact(actor.Camera,TrySpendCash,GrantCash);
            guestCarried=_world.CarriedCoder;
            _world.CarriedCoder=carried;
            Net.Send(new("status",Text:result));
        }
        else if(Net.Host && message.Kind=="floor-action" && int.TryParse(message.Text,out int index))
        {
            string result=ApplyFloorAction(index);
            Net.Send(new("status",Text:result));
        }
        else if(Net.Host && message.Kind=="bonus") GrantCash(2000,"Бонус напарника");
        else if(Net.Host && message.Kind=="case")
            Net.Send(new(TrySpendCash(LoreAndMenusUI.CaseSpinCost) ? "case-paid" : "case-denied"));
        else if(Net.Guest && message.Kind is "case-paid" or "case-denied")
        {
            pendingCase=false;
            if(message.Kind=="case-paid") LoreAndMenusUI.StartPaidCaseSpin();
            else SetHudBanner("Недостаточно общих денег на кейс");
        }
        else if(Net.Guest && message is {Kind:"status", Text:{} status})
        {
            panelMessage=status;
            SetHudBanner(status);
        }
    }

    private static void Fire(Camera3D camera)
    {
        if(Net.Ready) Net.Send(new("fire",LocalPose));
        if(Net.Guest) _weapons.TriggerWeaponAttack(camera,camera.Position+LocalPose.Forward*14,false);
        else if(_world.ProcessPlayerAttack(camera,_weapons,GrantCash,out string msg) && msg.Length>0)
            SetHudBanner(msg,3.5f);
    }

    private static void TickFloor(float dt)
    {
        if(bot && !Net.Ready) _world.Partner?.Follow(dt,LocalPose,_world,GrantCash);
        _world.Partner?.Tick(dt);
        if(Net.Guest) { _world.AdvanceLighting(dt); return; }
        _world.UpdateWorld(dt,_playerPos,_isCrouching,(amount,_)=>_playerCash=Math.Max(0,_playerCash+amount),(damage,label)=>
        {
            _playerHp=AfterDamage(_playerHp,damage);
            SetHudBanner($"ПРИЛЕТЕЛ БЛОК «{label}» (-{damage} ХП)!",2.5f);
        },Net.Ready ? _world.Partner?.Pose.Eye : null,damage=>guestHp=AfterDamage(guestHp,damage));
    }

    // Handing over a prisoner and witnessing their NDA both require a second player.
    // The host owns both carry slots, so two simultaneous E presses never duplicate staff.
    private static string? CoopEscort(Camera3D camera,bool guestActor,bool apply)
    {
        if(!Net.Ready || _world.Partner==null) return null;
        Vector3 peer=guestActor ? _playerPos : _world.Partner.Pose.Eye;
        Vector3 offset=peer-Vector3.UnitY*.28f-camera.Position;
        if(offset.LengthSquared()<.01f || offset.LengthSquared()>7) return null;
        Vector3 dir=Vector3.Normalize(offset);
        if(Vector3.Dot(dir,Vector3.Normalize(camera.Target-camera.Position))<.91f ||
            _world.PelletDistance(camera.Position,dir,offset.Length())+.04f<offset.Length()) return null;
        var mine=guestActor ? guestCarried : _world.CarriedCoder;
        var theirs=guestActor ? _world.CarriedCoder : Net.Guest ? peerEscort : guestCarried;
        if(mine!=null && theirs==null)
        {
            if(!apply) return "Передать кандидата напарнику";
            if(guestActor) { _world.CarriedCoder=mine; guestCarried=null; }
            else { guestCarried=mine; _world.CarriedCoder=null; }
            return "Кандидат передан напарнику";
        }
        if(mine==null && theirs is {JointNda:false})
        {
            if(!apply) return "Заверить NDA напарника";
            theirs.JointNda=true;
            Floor.Style(StyleAction.Capture); Floor.Risk(security:-5);
            return "NDA заверен вдвоём. СБ -5";
        }
        return null;
    }

    private static int AfterDamage(int hp,int damage)
    {
        if(hp>damage) return hp-damage;
        if(_world.Systems.JobId>=0 || _world.Systems.Friday>0) _world.Systems.CrewLost=true;
        return 100;
    }

    private static void StagePartner(Vector3 feet,Vector3 target)
    {
        var ally=new PartnerAvatar();
        var eye=feet+Vector3.UnitY*1.72f;
        var aim=Vector3.Normalize(target-eye);
        ally.Move(new(eye,MathF.Atan2(aim.X,aim.Z),MathF.Asin(aim.Y),WeaponType.MicroUzi));
        _world.Partner=ally;
        ally.Attack(_world,GrantCash,true);
    }
}

public static partial class Program
{
    private static void CheckCoopEscort(bool guest)
    {
        var clock=System.Diagnostics.Stopwatch.StartNew();
        var meeting=new Vector3(24,1.72f,18);
        _playerPos=guest ? meeting+Vector3.UnitZ*1.9f : meeting;
        int phase=0, identity=0;
        double settled=0;
        if(!guest)
        {
            var recruit=_world.SpawnCoderForTier(SubscriptionBaitTier.FreeTier);
            identity=recruit.Id; recruit.Position=meeting+new Vector3(0,-1.72f,-1.6f);
            LookAtTarget(recruit.CombatCenter);
            _weapons.SelectSlot(1);
            _world.ProcessPlayerAttack(BuildPlayerCamera(),_weapons,GrantCash,out _);
            _world.Interact(BuildPlayerCamera(),TrySpendCash,GrantCash);
            if(_world.CarriedCoder!=recruit) throw new InvalidDataException("Coop NDA setup");
        }
        while(clock.Elapsed.TotalSeconds<15)
        {
            if(!Net.Ready && (guest || phase!=2)) throw new IOException("Coop escort disconnected: "+Net.Status);
            Net.Pump(LocalPose,CaptureFloor);
            if(guest)
            {
                if(phase==0 && peerEscort!=null && _world.Partner is {} partner && Vector3.Distance(partner.Pose.Eye,meeting)<.1f)
                {
                    identity=peerEscort.Id; LookAtTarget(meeting-Vector3.UnitY*.28f);
                    if(CoopEscort(BuildPlayerCamera(),false,false)==null) throw new InvalidDataException("Guest NDA prompt");
                    Net.Send(new("interact",LocalPose)); phase=1;
                }
                if(phase==1 && _world.CarriedCoder is {JointNda:true} c && c.Id==identity)
                {
                    FloorAction(0);
                    var desk=_world.GalleyDesks[0].DeskPosition;
                    _playerPos=desk+new Vector3(0,1.72f,1.9f); LookAtTarget(desk+Vector3.UnitY*.72f);
                    Net.Send(new("interact",LocalPose)); phase=2;
                }
                if(phase==2 && Floor.JobId==0 && Floor.Seated==1 && _world.CarriedCoder==null && _world.GalleyDesks[0].Productivity==14)
                {
                    Console.WriteLine("COOP PASS guest: witnessed NDA, received escort, signed MVP, staffed galley"); return;
                }
            }
            else
            {
                if(phase==0 && _world.CarriedCoder is {JointNda:true} && _world.Partner is {} ally)
                {
                    LookAtTarget(ally.Pose.Eye-Vector3.UnitY*.28f);
                    CoopEscort(BuildPlayerCamera(),false,true);
                    if(guestCarried?.Id!=identity || _world.CarriedCoder!=null) throw new InvalidDataException("Host escort handoff");
                    phase=1;
                }
                if(phase==1 && Floor.Seated==1 && Floor.JobId==0 && guestCarried==null && _world.GalleyDesks.Count(d=>d.IsOccupied)==1)
                { phase=2; settled=clock.Elapsed.TotalSeconds; }
                if(phase==2 && clock.Elapsed.TotalSeconds-settled>.8)
                { Console.WriteLine("COOP PASS host: shared contract, single NDA transfer and one desk"); return; }
            }
            DrawFrame();
        }
        throw new TimeoutException($"Coop escort phase {phase}, guest={guest}");
    }
}
