#nullable enable
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public static partial class Program
{
    private static CoopNetworkSystem Net => MainMenuAndPauseUI.Coop;
    private static bool bot, awaitingSpawn, pendingCase;
    private static readonly int[] health=[100,100,100];
    private static readonly VibeCoderEntity?[] escorts=new VibeCoderEntity?[3];
    private static readonly double[] downUntil=new double[3];
    private static int actorSlot=1;
    private static bool Downed(int slot) => Environment.TickCount64*.001<downUntil[slot];
    private static void MarkDown(int slot) => downUntil[slot]=Environment.TickCount64*.001+.8;
    private static ref int guestHp => ref health[actorSlot];
    private static ref VibeCoderEntity? guestCarried => ref escorts[actorSlot];
    private static ref VibeCoderEntity? peerEscort => ref escorts[0];
    private static PartnerAvatar Avatar(int slot) => _world.Partners[slot]??=new PartnerAvatar(slot);
    private static PlayerPose LocalPose => new(BuildPlayerCamera().Position,_playerYaw,_playerPitch,_weapons.CurrentWeapon,_weapons.BeltSwingPhase,_isCrouching,_sprinting);
    private static void StartHosting(bool newShift = false)
    {
        bool online = Net.StartHost(CoopNetworkSystem.DefaultPort);
        _world.AdaptPlayers(Net.ActivePlayerCount);
        if (!newShift) SetHudBanner(Net.Status);
        else if (!online) Floor.Announce(Net.Status);
    }

    private static void ConfigureCoop()
    {
        MainMenuAndPauseUI.SummonPartner=() =>
        {
            bot=true;
            _world.Partner=new PartnerAvatar();
            _world.Partner.Effects.UnlockedMicroUzi=true;
            _world.Partner.Move(LocalPose with { Eye=_world.ResolvePlayerCollision(_playerPos+new Vector3(1.7f,0,-1.2f)), Weapon=WeaponType.MicroUzi });
        };
        Net.OnJoined=() =>
        {
            ResetNewGameFloor(greet:false);
            awaitingFirstFloor=true;
            awaitingSpawn=true;
            MainMenuAndPauseUI.IsPaused=MainMenuAndPauseUI.IsInMainMenu=MainMenuAndPauseUI.IsSaveLoadPanelOpen=false;
            Raylib.DisableCursor();
        };
        Net.OnCountChanged=()=>
        {
            _world.AdaptPlayers(Net.ActivePlayerCount);
            Floor.UpdateOnboarding(_world,_playerCash);
        };
        Net.OnPose=(slot,pose) =>
        {
            if(awaitingSpawn && slot==0)
            {
                _playerPos=_world.ResolvePlayerCollision(pose.Eye+new Vector3(Net.LocalSlot==2 ? -1.6f : 1.6f,0,.5f));
                _playerYaw=pose.Yaw;
                awaitingSpawn=false;
            }
            if(bot) Array.Clear(_world.Partners);
            bot=false; Avatar(slot).Move(pose);
        };
        Net.OnMessage=(slot,message)=>
        {
            actorSlot=slot;
            try { HandleCoopMessage(message); } finally { actorSlot=1; }
        };
        Net.OnLost=slot =>
        {
            if(Net.Host && escorts[slot] is {} prisoner) _world.ActiveCoders.Add(prisoner);
            escorts[slot]=null; health[slot]=100; downUntil[slot]=0;
            if(!bot) _world.Partners[slot]=null;
            if(Net.Host)
            {
                if(slot==2) { Floor.SecondCoffeeCups=2; Floor.SecondStun=0; }
                else { Floor.GuestCoffeeCups=2; Floor.GuestStun=0; }
            }
            if(slot!=0) return;
            Array.Clear(_world.Partners); pendingCase=false;
            if(!Net.Host && !MainMenuAndPauseUI.IsInMainMenu)
            {
                string reason=Net.Status;
                MainMenuAndPauseUI.OpenMainMenu();
                MainMenuAndPauseUI.OpenJoinPanel();
                MainMenuAndPauseUI.Notify(reason);
            }
        };
    }

    private static FloorSnapshot CaptureFloor() => CaptureForSlot(1);
    private static FloorSnapshot CaptureForSlot(int slot) => new(_playerCash,_world.ElevatorBoss,_world.ElevatorDoorOpenAmount,
        _world.HasElevatorKey,_world.IsFloor2Unlocked,_world.IsMacBookTrapActive,_world.SelectedBaitTier,
        _world.ActiveCoders.ToArray(),_world.GalleyDesks.ToArray(),_world.EnemyProjectiles.ToArray(),
        escorts[slot],_world.TotalCodersSoldToHr,health[slot],_playerHp,_world.Systems,_world.CarriedCoder,
        ProgressionVersion:3,Frogs:_world.Frogs.ToArray(),Equipped:_weapons.CurrentWeapon,OnboardingStep:Floor.Onboarding,
        ElevatorRepaired:_world.ElevatorRepaired,ElevatorGlassOpacity:_world.ElevatorGlassOpacity,SelectedHotbarSlot:_weapons.SelectedHotbarSlot,Health:[_playerHp,health[1],health[2]],Escorts:[_world.CarriedCoder,escorts[1],escorts[2]],Downed:[Downed(0),Downed(1),Downed(2)],RoomOpen:_world.RoomOpen,VentTime:_world.VentTime);

    private static void ApplyElevator(FloorSnapshot state)
    {
        _world.ElevatorBoss=state.Boss;
        _world.HasElevatorKey=state.Key;
        _world.IsFloor2Unlocked=state.Unlocked;
        _world.ElevatorRepaired=state.ElevatorRepaired ?? (state.Boss.IsActive || state.Boss.IsDefeated || state.Unlocked);
        _world.ElevatorDoorOpenAmount=_world.ElevatorRepaired ? state.Door : 0;
        _world.ElevatorGlassOpacity=state.ElevatorGlassOpacity ?? (_world.ElevatorRepaired ? 0 : 1);
    }

    private static void ApplyProgression(FloorSnapshot? state)
    {
        Floor.Onboarding=state?.OnboardingStep ?? OnboardingStep.Done;
        if(state is {ProgressionVersion:<2,OnboardingStep:{} old} && (int)old>=5)
            Floor.Onboarding=(int)old==5 ? OnboardingStep.StunAndHeal : OnboardingStep.Done;
        if(Floor.Onboarding==OnboardingStep.Done || state?.Sold>=Floor.Balance.QuarantineUnlockTarget || state is {ProgressionVersion:<2} && (state.Sold>0 || state.Desks.Any(d=>d.IsOccupied)))
        {
            if(state is {ProgressionVersion:<2} && Floor.Onboarding!=OnboardingStep.Done)
                Floor.Onboarding=state.Desks.Any(d=>d.IsOccupied) ? OnboardingStep.Done : OnboardingStep.SeatFirstGalley;
            if(!Floor.WingsUnlocked) Floor.WingBarrierSlide=1;
            Floor.WingsUnlocked=true;
        }
        Floor.PocketCoffeeCups=Math.Clamp(Floor.PocketCoffeeCups,0,2);
        if(state?.ProgressionVersion is null or <1) Floor.OwnedWeaponsMask=FloorSystems.ClassicMask;
        Floor.OwnedWeaponsMask |= 1<<(int)WeaponType.BareHands;
        _weapons.Inventory=Floor;
        if(!Floor.HasWeapon(_weapons.CurrentWeapon)) _weapons.Equip(WeaponType.BareHands);
        if(state?.Frogs is {} frogs)
        {
            _world.Frogs.Clear(); _world.Frogs.AddRange(frogs);
        }
    }

    private static bool awaitingFirstFloor;
    private static void HandleCoopMessage(CoopMessage message)
    {
        if(Net.Host && health[actorSlot]==0)
        {
            if(message.Kind=="wake") { PayWakePenalty(); health[actorSlot]=100; GuestCups=Math.Max(1,GuestCups); downUntil[actorSlot]=0; }
            return;
        }
        if(Net.Guest && message is {Kind:"floor", Floor:{} state})
        {
            if(state.Boss==null || state.Coders==null || state.Desks?.Length!=4 || state.Projectiles==null) return;
            var previous=Floor.Onboarding;
            bool cheered=Floor.FirstFrogCheered;
            if(state.Systems!=null) _world.Systems=state.Systems;
            ApplyProgression(state);
            if(awaitingFirstFloor ? Floor.Onboarding==OnboardingStep.TalkToVeronika : previous!=Floor.Onboarding)
            {
                Floor.Coach();
            }
            else if(!awaitingFirstFloor && !cheered && Floor.FirstFrogCheered && Floor.Onboarding<=OnboardingStep.CatchFrogs)
                FloorSystems.CheerFirstFrog();
            awaitingFirstFloor=false;
            _playerCash=state.Cash; _playerHp=state.GuestHp; health[0]=state.HostHp;
            if(_playerHp==0) BeginKnockout();
            else if(_knockoutActive) FinishWake();
            if(state.Health is {Length:3}) Array.Copy(state.Health,health,3);
            if(state.Escorts is {Length:3}) Array.Copy(state.Escorts,escorts,3);
            foreach(var ally in _world.Teammates)
            {
                ally.Hp=health[ally.Slot]; ally.Carrying=escorts[ally.Slot]!=null;
                ally.Downed=state.Downed is {Length:3} && state.Downed[ally.Slot];
            }
            if (_world.ElevatorBoss.State != state.Boss.State)
                SkyscraperFloor1World.PlayElevatorCue(state.Boss.State);
            ApplyElevator(state);
            if(state.RoomOpen is {Length:4} doors) doors.CopyTo(_world.RoomOpen,0);
            if(state.VentTime is {Length:3} vents) vents.CopyTo(_world.VentTime,0);
            if(state.Unlocked) _weapons.UnlockedChatGun=true;
            _world.IsMacBookTrapActive=state.Trap; _world.SelectedBaitTier=state.Bait;
            peerEscort=state.HostCarried;
            _world.CarriedCoder=state.Carried; _world.TotalCodersSoldToHr=state.Sold;
            HolsterForEscort();
            _world.ActiveCoders.Clear(); _world.ActiveCoders.AddRange(state.Coders);
            _world.GalleyDesks.Clear(); _world.GalleyDesks.AddRange(state.Desks);
            _world.EnemyProjectiles.Clear(); _world.EnemyProjectiles.AddRange(state.Projectiles);
            return;
        }
        if(message is {Kind:"fire", Pose:{} shooter})
        {
            if(Net.Host && (!Floor.HasWeapon(shooter.Weapon) || guestCarried!=null && !_world.CanCarryWith(shooter.Weapon))) return;
            if(shooter.Weapon==WeaponType.ChatGun && !_world.IsFloor2Unlocked && !_weapons.UnlockedChatGun) return;
            var partner=Avatar(actorSlot);
            if(Net.Host) Net.Send(message with {Slot=actorSlot},3-actorSlot);
            bot=false; partner.Move(shooter);
            // The host validates ownership before accepting a guest attack. On the guest a
            // fire packet may precede the next inventory snapshot.
            if(Net.Host) partner.Effects.Inventory=Floor;
            else partner.Effects.Inventory.OwnedWeaponsMask|=1<<(int)shooter.Weapon;
            int previousHits=partner.Effects.FloatingTexts.Count;
            if(Net.Host && shooter.Weapon is WeaponType.BareHands or WeaponType.CaptureNet)
            {
                partner.Effects.CurrentWeapon=shooter.Weapon;
                var hostCarry=_world.CarriedCoder;
                _world.CarriedCoder=guestCarried;
                guestInteraction=true;
                try
                {
                    if(_world.ProcessPlayerAttack(shooter.Camera,partner.Effects,GrantCash,out var feedback))
                        Net.Send(new("status",Text:feedback),actorSlot);
                    guestCarried=_world.CarriedCoder;
                }
                finally { guestInteraction=false; _world.CarriedCoder=hostCarry; }
            }
            else partner.Attack(_world,GrantCash,Net.Host);
            if(Net.Host)
                for(int i=previousHits;i<partner.Effects.FloatingTexts.Count;i++)
                    Net.Send(new("impact",Impact:partner.Effects.FloatingTexts[i]),actorSlot);
            if(Net.Host && _world.IsFloor2Unlocked) _weapons.UnlockedChatGun=true;
            PlayCurrentWeaponAudio(shooter.Weapon);
        }
        else if(Net.Host && message is {Kind:"interact", Pose:{} actor})
        {
            string? joint=CoopEscort(actor.Camera,true,apply:true);
            if(joint!=null) { Net.Send(new("status",Text:joint),actorSlot); return; }
            var carried=_world.CarriedCoder;
            _world.CarriedCoder=guestCarried;
            string result;
            guestInteraction=true;
            try { result=_world.Interact(actor.Camera,TrySpendCash,GrantCash,actor.Weapon); }
            finally { guestInteraction=false; }
            guestCarried=_world.CarriedCoder;
            _world.CarriedCoder=carried;
            Net.Send(new("status",Text:result),actorSlot);
        }
        else if(Net.Guest && message is {Kind:"hurt",Source:{} source,Text:{} label}) ShowHit(message.Amount,label,source);
        else if(Net.Guest && message.Kind=="healed") HealFeedback(message.Amount);
        else if(Net.Guest && message is {Kind:"skin-spin",Tickets:{Length:40} tickets} &&
            tickets.All(t=>t>=0 && t/16<5 && t%16 is >=1 and <=12))
        {
            var slot=_world.Slot;
            for(int i=0;i<tickets.Length;i++) slot.Tickets[i]=(tickets[i]/16,tickets[i]%16);
            slot.Cashback=message.Amount;
            slot.BeginSpin();
        }
        else if(Net.Host && message is {Kind:"skin-equip",Amount:>=0 and <112})
        {
            var weapon=(WeaponType)(message.Amount/16);
            if(FloorSystems.WeaponOrder.Contains(weapon)) Floor.EquipSkin(weapon,message.Amount%16);
        }
        else if(Net.Host && message is { Kind:"drop-bait", Pose:{Valid:true} baitActor })
            Net.Send(new("status",Text:_world.DropFloorBait(baitActor.Camera)),actorSlot);
        else if(Net.Host && message.Kind=="drink")
        {
            guestInteraction=true;
            try { DrinkPocketCoffee(); } finally { guestInteraction=false; }
        }
        else if(Net.Host && message.Kind=="floor-action" && int.TryParse(message.Text,out int index))
        {
            string result=ApplyFloorAction(index,guestBuyer:true);
            Net.Send(new("status",Text:result),actorSlot);
        }
        else if(Net.Host && message is {Kind:"pocket-co2",Pose:{} pose})
            Net.Send(new("status",Text:Floor.UsePocket(pose.Eye)),actorSlot);
        else if(Net.Host && message.Kind=="bonus") GrantCash(2000,"Бонус напарника");
        else if(Net.Host && message.Kind=="case")
            Net.Send(new(TrySpendCash(LoreAndMenusUI.CaseSpinCost) ? "case-paid" : "case-denied"),actorSlot);
        else if(Net.Guest && message.Kind is "case-paid" or "case-denied")
        {
            pendingCase=false;
            if(message.Kind=="case-paid") LoreAndMenusUI.StartPaidCaseSpin();
            else SetHudBanner("Недостаточно общих денег на кейс");
        }
        else if(Net.Guest && message is {Kind:"impact", Impact:{} impact})
        {
            _weapons.FloatingTexts.Add(impact);
            _weapons.TriggerHitmarker(impact.IsCritical);
            AudioMusicSystem.PlayHitmarker();
        }
        else if(Net.Guest && message.Kind=="equip-purchase" && Enum.TryParse<WeaponType>(message.Text,out var purchase))
        {
            _weapons.Equip(purchase);
            if(purchase==WeaponType.CaptureNet && Floor.Onboarding<=OnboardingStep.ActivateBait && floorPanel==FloorPanel.Upgrades)
                OpenFloorPanel(FloorPanel.None);
        }
        else if(Net.Guest && message.Kind=="keycard-purchased")
            FloorSystems.SpeakCoaching(FloorSystems.KeycardPurchaseLine);
        else if(Net.Guest && message is {Kind:"status", Text:{} status})
        {
            panelMessage=status;
            SetHudBanner(status,2.4f,foreground:true);
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
        _lastDamageTimer=Math.Max(0,_lastDamageTimer-dt); HealFlashTimer=Math.Max(0,HealFlashTimer-dt);
        if(bot && !Net.Ready) _world.Partner?.Follow(dt,LocalPose,_world,GrantCash);
        foreach(var ally in _world.Teammates)
        {
            ally.Tick(dt); ally.Hp=health[ally.Slot]; ally.Carrying=escorts[ally.Slot]!=null;
            if(Net.Host) ally.Downed=Downed(ally.Slot);
        }
        if(Net.Guest)
        {
            _world.AdvanceLighting(dt);
            foreach(var frog in _world.Frogs) if(frog.Rest<=0) frog.AdvanceArc(dt);
            return;
        }
        _world.HostSprinting=_sprinting;
        _world.UpdateWorld(dt,_playerPos,_isCrouching,(amount,_)=>_playerCash=Math.Max(0,_playerCash+amount),ReceiveDamage,Net.Ready ? _world.Partners[1]?.Pose.Eye : null,GuestDamage,
            Net.Ready ? _world.Partners[2]?.Pose.Eye : null,(damage,label,source)=>DamageSlot(2,damage,label,source));
        Floor.UpdateOnboarding(_world,_playerCash);
    }

    // Handing over a prisoner and witnessing their NDA both require a second player.
    // The host owns both carry slots, so two simultaneous E presses never duplicate staff.
    private static string? CoopEscort(Camera3D camera,bool guestActor,bool apply)
    {
        if(!Net.Ready || _world.Partner==null) return null;
        int mineSlot=guestActor ? actorSlot : Net.LocalSlot;
        int target=-1;
        float best=7;
        for(int i=0;i<3;i++)
        {
            if(i==mineSlot) continue;
            Vector3? eye=i==Net.LocalSlot ? _playerPos : _world.Partners[i]?.Pose.Eye;
            if(eye==null) continue;
            Vector3 offset=eye.Value-Vector3.UnitY*.28f-camera.Position;
            float distance=offset.LengthSquared();
            if(distance<.01f || distance>best) continue;
            Vector3 dir=Vector3.Normalize(offset);
            if(Vector3.Dot(dir,Vector3.Normalize(camera.Target-camera.Position))<.91f ||
                _world.PelletDistance(camera.Position,dir,offset.Length())+.04f<offset.Length()) continue;
            target=i; best=distance;
        }
        if(target<0) return null;
        var mine=mineSlot==Net.LocalSlot ? _world.CarriedCoder : escorts[mineSlot];
        var theirs=target==Net.LocalSlot ? _world.CarriedCoder : escorts[target];
        if(mine!=null && theirs==null)
        {
            if(!apply) return "Передать кандидата напарнику";
            if(target==Net.LocalSlot) { _world.CarriedCoder=mine; HolsterForEscort(); }
            else escorts[target]=mine;
            if(mineSlot==Net.LocalSlot) _world.CarriedCoder=null;
            else escorts[mineSlot]=null;
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

    private static void HolsterForEscort()
    {
        if(_world.CarriedCoder!=null && !_world.CanCarryWith(_weapons.CurrentWeapon))
            _weapons.Equip(WeaponType.BareHands);
    }

    private static void GuestDamage(int damage,string label,Vector3 source)
    {
        DamageSlot(actorSlot,damage,label,source);
    }
    private static void DamageSlot(int slot,int damage,string label,Vector3 source)
    {
        if(health[slot]<=0) return;
        if(health[slot]<=damage) MarkDown(slot);
        health[slot]=AfterDamage(health[slot],damage);
        Net.Send(new("hurt",Text:label,Amount:damage,Source:source),slot);
        BeginHealLesson();
    }

    private static int AfterDamage(int hp,int damage)
    {
        if(hp>damage) return hp-damage;
        if(_world.Systems.JobId>=0 || _world.Systems.Friday>0) _world.Systems.CrewLost=true;
        return 0;
    }

    private static void StagePartner(Vector3 feet,Vector3 target)
    {
        var ally=new PartnerAvatar();
        ally.Effects.UnlockedMicroUzi=true;
        var eye=feet+Vector3.UnitY*1.72f;
        var aim=Vector3.Normalize(target-eye);
        ally.Move(new(eye,MathF.Atan2(aim.X,aim.Z),MathF.Asin(aim.Y),WeaponType.MicroUzi));
        _world.Partner=ally;
        ally.Attack(_world,GrantCash,true);
    }
}

public static partial class Program
{
    private static void CheckCoopProgression(bool guest)
    {
        if(!guest) ResetNewGameFloor();
        var timer=System.Diagnostics.Stopwatch.StartNew();
        int sent=0; bool deposited=false, ordered=false, hostSawPocket=false, guardStaged=false, guardHit=false;
        double settled=0;
        while(timer.Elapsed.TotalSeconds<20)
        {
            Net.Pump(LocalPose,CaptureForSlot);
            if(guest && Floor.JobId<0 && Floor.Seated==0 && _world.Frogs.Count>0)
            {
                if(sent<3 && _playerCash==0 && Floor.PocketFrogs.Count==sent)
                {
                    if(sent==0)
                    {
                        if(_weapons.HotbarSlotCount!=4 || _weapons.Hotbar.Any(w=>w!=null) || _weapons.CurrentWeapon!=WeaponType.BareHands)
                            throw new InvalidDataException("Guest empty hotbar");
                        for(int i=0;i<4;i++)
                        {
                            _weapons.SelectOwnedSlot(i);
                            if(_weapons.SelectedHotbarSlot!=i || _weapons.CurrentWeapon!=WeaponType.BareHands)
                                throw new InvalidDataException("Guest empty slot selection");
                        }
                    }
                    var frog=_world.Frogs.First(f=>f.SpeciesId=="frog_hopper");
                    _weapons.Equip(WeaponType.BareHands);
                    _playerPos=frog.Feet+new Vector3(0,1.1f,1);
                    LookAtTarget(frog.Feet+Vector3.UnitY*.11f);
                    Fire(BuildPlayerCamera()); sent++;
                }
                if(sent==3 && Floor.PocketFrogs.Count==3 && !deposited)
                {
                    _playerPos=SkyscraperFloor1World.TerminalPos+new Vector3(0,1.72f,1.6f);
                    LookAtTarget(SkyscraperFloor1World.TerminalPos+Vector3.UnitY*1.08f);
                    Net.Send(new("interact",LocalPose)); deposited=true;
                }
                if(deposited && !ordered && _playerCash==4 && Floor.DiscoveredSpecies.GetValueOrDefault("frog_hopper")==3)
                { FloorAction(WeaponBuyOffset); ordered=true; }
                if(ordered && Floor.HasWeapon(WeaponType.CaptureNet) && _playerCash==1 && Floor.PocketFrogs.Count==0)
                {
                    if(_weapons.CurrentWeapon!=WeaponType.CaptureNet) { DrawFrame(); continue; }
                    if(_weapons.SelectedHotbarSlot!=0 || _weapons.HotbarSlotCount!=4 || _weapons.Hotbar.Skip(1).Any(w=>w!=null))
                        throw new InvalidDataException("Guest net slot");
                    var guard=_world.ActiveCoders.Find(c=>c.Archetype==CoderArchetype.SecurityGuard);
                    if(guard!=null && !guardHit)
                    {
                        _playerPos=guard.Position+new Vector3(0,1.35f,1.1f);
                        LookAtTarget(guard.CombatCenter);
                        Fire(BuildPlayerCamera()); guardHit=true;
                    }
                    if(guardHit && _world.HasElevatorKey && Floor.Notice==SkyscraperFloor1World.KeycardDrop)
                    {
                        Console.WriteLine("COOP PASS guest: shared catches, net purchase, free guard keycard and loot notice"); return;
                    }
                }
            }
            if(!guest)
            {
                hostSawPocket |= Floor.PocketFrogs.Count==3;
                if(hostSawPocket && _playerCash==1 && Floor.PocketFrogs.Count==0 && Floor.HasWeapon(WeaponType.CaptureNet)
                    && Floor.DiscoveredSpecies.GetValueOrDefault("frog_hopper")==3)
                {
                    if(!guardStaged)
                    {
                        var guard=_world.SpawnFighter(CoderArchetype.SecurityGuard,new Vector3(24,0,17));
                        guard.Hp=12;
                        guardStaged=true;
                    }
                    if(_world.HasElevatorKey && settled==0) settled=timer.Elapsed.TotalSeconds;
                    if(settled>0 && timer.Elapsed.TotalSeconds-settled>.65)
                    { Console.WriteLine("COOP PASS host: shared catches, single payout, net unlocked, free guard keycard"); return; }
                }
            }
            DrawFrame();
        }
        throw new TimeoutException($"Shared progression guest={guest}, catches={sent}, pocket={Floor.PocketFrogs.Count}, cash={_playerCash}");
    }

    private static void CheckCoopEscort(bool guest)
    {
        var clock=System.Diagnostics.Stopwatch.StartNew();
        var meeting=new Vector3(24,1.72f,18);
        _playerPos=guest ? meeting+Vector3.UnitZ*1.9f : meeting;
        int phase=0, identity=0;
        double settled=0;
        if(!guest)
        {
            var recruit=_world.SpawnFighter(CoderArchetype.JuniorPrompter,meeting,VibeCoderCollection.All.First(s=>s.Id=="tab"));
            identity=recruit.Id; recruit.Position=meeting+new Vector3(0,-1.72f,-1.6f);
            LookAtTarget(recruit.CombatCenter);
            _weapons.SelectSlot(1);
            _world.ProcessPlayerAttack(BuildPlayerCamera(),_weapons,GrantCash,out _);
            _world.Interact(BuildPlayerCamera(),TrySpendCash,GrantCash,_weapons.CurrentWeapon);
            HolsterForEscort();
            if(_world.CarriedCoder!=recruit) throw new InvalidDataException("Coop NDA setup");
        }
        while(clock.Elapsed.TotalSeconds<15)
        {
            if(!Net.Ready && (guest || phase!=2)) throw new IOException("Coop escort disconnected: "+Net.Status);
            Net.Pump(LocalPose,CaptureForSlot);
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
