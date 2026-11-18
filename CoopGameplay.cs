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
    private static VibeCoderEntity? guestCarried;
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
            guestCarried=null; guestHp=100; pendingCase=false;
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
        guestCarried,_world.TotalCodersSoldToHr,guestHp,_playerHp);

    private static void HandleCoopMessage(CoopMessage message)
    {
        if(Net.Guest && message is {Kind:"floor", Floor:{} state})
        {
            if(state.Boss==null || state.Coders==null || state.Desks?.Length!=4 || state.Projectiles==null) return;
            _playerCash=state.Cash; _playerHp=state.GuestHp; hostHp=state.HostHp;
            _world.ElevatorBoss=state.Boss; _world.ElevatorDoorOpenAmount=state.Door;
            _world.HasElevatorKey=state.Key; _world.IsFloor2Unlocked=state.Unlocked;
            if(state.Unlocked) _weapons.UnlockedChatGun=true;
            _world.IsMacBookTrapActive=state.Trap; _world.SelectedBaitTier=state.Bait;
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
            var carried=_world.CarriedCoder;
            _world.CarriedCoder=guestCarried;
            string result=_world.Interact(actor.Camera,TrySpendCash,GrantCash);
            guestCarried=_world.CarriedCoder;
            _world.CarriedCoder=carried;
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
        else if(Net.Guest && message is {Kind:"status", Text:{} status}) SetHudBanner(status);
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
        _world.UpdateWorld(dt,_playerPos,_isCrouching,(amount,_)=>_playerCash+=amount,(damage,label)=>
        {
            _playerHp=Math.Max(15,_playerHp-damage);
            SetHudBanner($"ПРИЛЕТЕЛ БЛОК «{label}» (-{damage} ХП)!",2.5f);
        },Net.Ready ? _world.Partner?.Pose.Eye : null,damage=>guestHp=Math.Max(15,guestHp-damage));
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
