#nullable enable
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public sealed class FloorBait
{
    public Vector3 Position;
    public int Tier, Door, HunterId;
    public float Yaw, Wait=1;
    public bool Spotted;
    internal static readonly int[] Prices=[3,35,90];
    internal static string Name(int tier) => tier switch {
        0=>Loc.T("ЯблоБук-наживка (Free Wi-Fi)","AppleBook Bait (Free Wi-Fi)"),
        1=>Loc.T("Раф + Токены Клодия","Latte + Clodius Tokens"),
        _=>Loc.T("Топ-ЯблоБук + Безлимит Табсора","Pro AppleBook + Tabsor Unlimited") };
}

public sealed partial class SkyscraperFloor1World
{
    internal bool HostSprinting;
    private bool hostCrouched;
    private Vector3 hostEye;
    private Vector3 RefillPoint => HrTarotBoothPos+new Vector3(1.9f,0,-.95f);
    private static Vector3 Flat(Vector3 v)=>new(v.X,0,v.Z);
    private int NearestDoor(Vector3 feet) => Enumerable.Range(0,Systems.WingsUnlocked ? 4 : 2)
        .MinBy(i=>Vector3.DistanceSquared(RoomDoors[i],feet));
    internal bool Visible(Vector3 from,Vector3 to)
    {
        Vector3 ray=to-from;
        float length=ray.Length();
        return length<.02f || TraceSurface(from,ray/length,length)>=length-.02f;
    }
    internal bool IsPlayerSpottedAtBait(FloorBait bait,Vector3 eye,bool crouched)
    {
        float distance=Vector3.Distance(Flat(eye),bait.Position);
        float s=RoomExitSign(bait.Door);
        return distance<(crouched ? 4.2f : 7.5f) &&
            Visible(RoomDoors[bait.Door]+new Vector3(0,1.21f,s*.14f),eye) &&
            Visible(bait.Position+Vector3.UnitY*.18f,eye);
    }
    internal string DropFloorBait(Camera3D camera)
    {
        if(Systems.PlacedBait!=null) return Loc.T("Наживка уже на полу","Bait already deployed");
        int tier=Systems.ActiveBaitTier;
        if(tier<0) return Loc.T("Наживка у Вероники","Collect bait at Veronika’s");
        Vector3 direction=Flat(camera.Target-camera.Position);
        if(direction.LengthSquared()<.000001f) return Loc.T("Выбери место на полу","Aim at clear floor");
        Vector3 feet=Flat(camera.Position)+Vector3.Normalize(direction)*1.1f;
        if(Vector3.DistanceSquared(feet,ResolvePlayerCollision(feet,.25f))>.0001f ||
           !Visible(camera.Position,feet+Vector3.UnitY*.08f))
            return Loc.T("Нужно свободное место на полу","Aim at clear floor");
        Systems.PlacedBait=new() {Position=feet,Tier=tier,Door=NearestDoor(feet),Yaw=MathF.Atan2(-direction.X,-direction.Z)+.55f};
        Systems.FloorHuntEnabled=true; Systems.BaitStock[tier]--; IsMacBookTrapActive=true;
        AudioMusicSystem.PlayMacbookBait();
        return Loc.T("Наживка на полу! Отойди и спрячься [Ctrl] за колонну/диван","Bait dropped! Back off and hide [Ctrl] behind cover");
    }
    private string RecoverBait()
    {
        int tier=Systems.PlacedBait?.Tier ?? 0;
        foreach(var hunter in ActiveCoders.Where(c=>c.FloorHunter && !c.CanSignNda)) Spook(hunter);
        Systems.PlacedBait=null;
        Systems.BaitStock[tier]++;
        return FloorBait.Name(tier)+" +1";
    }
    private bool QuietAt(Vector3 eye)
    {
        foreach(var ally in Teammates)
            if(Vector3.DistanceSquared(ally.Pose.Eye,eye)<.09f) return ally.Pose.Crouched;
        return hostCrouched;
    }
    private void UpdateFloorHunt(float dt,Vector3 eye,bool crouched)
    {
        hostEye=eye; hostCrouched=crouched;
        if(Systems.PlacedBait is not {} bait || ActiveCoders.Any(c=>c.FloorHunter) || CarriedCoder!=null) return;
        if((bait.Wait-=dt)>0) return;
        bool seen=IsPlayerSpottedAtBait(bait,eye,crouched) || Teammates.Any(a=>IsPlayerSpottedAtBait(bait,a.Pose.Eye,a.Pose.Crouched));
        if(seen && bait.Spotted) return;
        var speciesPool=VibeCoderCollection.All.Where(s=>!s.IsFrog && s.Id!="ai_agent_v01" && s.Archetype!=CoderArchetype.SecurityGuard && (bait.Tier switch {
            0=>s.Archetype==CoderArchetype.JuniorPrompter && s.Rarity==VibeCoderRarity.Common,
            1=>s.Rarity is VibeCoderRarity.Rare or VibeCoderRarity.Epic,
            _=>s.Rarity==VibeCoderRarity.Legendary || s.Id=="no-commits" })).ToArray();
        var species=speciesPool[speciesRandom.Next(speciesPool.Length)];
        float s=RoomExitSign(bait.Door);
        var coder=SpawnFighter(species.Archetype,RoomDoors[bait.Door]-Vector3.UnitZ*(s*.26f),species);
        coder.FacingYaw=s<0 ? MathF.PI : 0f;
        coder.FloorHunter=coder.LaptopLost=true; coder.IsCombatCoder=coder.Aggro=false;
        bait.HunterId=coder.Id;
        coder.EntryRoom=bait.Door; coder.TimidIntern=bait.Tier==0;
        coder.Hp=coder.MaxHp=(bait.Tier==0 ? 24 : bait.Tier==1 ? 45 : 60)*Systems.Balance.EnemyHpScale;
        coder.TarotPayoutValue=bait.Tier switch {0=>10,1=>250+species.BonusPayout,_=>600+species.BonusPayout};
        if(bait.Tier==2 && species.Id=="no-commits") coder.Rarity=VibeCoderRarity.Elite;
        if(coder.TimidIntern && (TotalCodersSoldToHr>=2 || speciesRandom.Next(4)==0 && Systems.WingsUnlocked)) coder.Rarity=VibeCoderRarity.Uncommon;
        coder.State=seen ? CoderAIState.Peeking : CoderAIState.SneakingToBait;
        coder.ExitingRoom=!seen;
        coder.SpeechBubble=seen ? Loc.T("Ой, Босс стоит! Не пойду!","Uh-oh, Boss is watching! Nope!") :
            Loc.T("Вроде тихо... халявный Wi-Fi!","Coast is clear... free Wi-Fi!");
        coder.SpeechTimer=3; bait.Spotted=seen;
        if(seen) Program.SetHudBanner(Loc.T("Он тебя видит! Отойди или спрячься [Ctrl] за укрытие","He spotted you! Step back or hide [Ctrl] behind cover"),foreground:true);
    }
    private void Spook(VibeCoderEntity c)
    {
        if(c.EscapingToDoor) return;
        c.EscapingToDoor=true;
        Panic(c); c.EntryRoom=NearestDoor(c.Position); c.ExitingRoom=false; c.Stress=100; c.Burst=.4f;
        AudioMusicSystem.PlayAnxietyAlarm();
    }
    private bool TryDodge(VibeCoderEntity c,WeaponType tool,Vector3 eye)
    {
        bool rushing=Teammates.FirstOrDefault(a=>Vector3.DistanceSquared(a.Pose.Eye,eye)<.09f)?.Pose.Sprinting ?? HostSprinting;
        if(c.CanSignNda || c.DodgeCooldown>0 || c.Stress<70 ||
            !(c.State==CoderAIState.PanickedFleeing && tool==WeaponType.CaptureNet && !Systems.Has(Sidegrade.CarbonNda) || rushing && InFront(c,eye))) return false;
        c.DodgeCooldown=1.4f;
        if(dodgeRandom.NextDouble()>=.30) return false;
        Spook(c);
        var side=new Vector3(MathF.Cos(c.FacingYaw),0,-MathF.Sin(c.FacingYaw))*(_rng.Next(2)==0 ? 1 : -1);
        c.KnockbackVel=side*6; c.Burst=.7f; c.BaitRoute=null;
        c.SpeechBubble=Loc.T("Увернулся!","Dodged!"); c.SpeechTimer=1;
        return true;
    }
    private static bool InFront(VibeCoderEntity c,Vector3 eye)
    {
        var ray=Flat(eye-c.Position);
        return Vector3.Dot(new(MathF.Sin(c.FacingYaw),0,MathF.Cos(c.FacingYaw)),ray)>.45f*ray.Length();
    }
    private float StressRate(VibeCoderEntity c,Vector3 eye,bool crouched,bool sprint)
    {
        float d=Flat(eye-c.Position).LengthSquared();
        if(sprint && d<49) return 55;
        return !crouched && d<36 && InFront(c,eye) && Visible(c.CombatCenter,eye) ? 40 : -9;
    }
    private void UpdateStress(VibeCoderEntity c,float dt)
    {
        float rate=StressRate(c,hostEye,hostCrouched,HostSprinting);
        foreach(var ally in Teammates)
            rate=Math.Max(rate,StressRate(c,ally.Pose.Eye,ally.Pose.Crouched,ally.Pose.Sprinting));
        c.Stress=Math.Clamp(c.Stress+rate*dt,0,100);
        if(c.Stress>=100) Spook(c);
    }

    private void UpdateHunter(VibeCoderEntity c,float dt)
    {
        var door=RoomDoors[c.EntryRoom];
        float s=RoomExitSign(c.EntryRoom);
        var bait=Systems.PlacedBait;
        if(c.State==CoderAIState.Peeking)
        {
            c.StateTimer+=dt;
            c.FacingYaw=s<0 ? MathF.PI : 0f;
            RoomOpen[c.EntryRoom]=Math.Min(.35f,c.StateTimer*1.6f);
            if(c.StateTimer>.95f) { RoomOpen[c.EntryRoom]=0; ActiveCoders.Remove(c); if(bait!=null) bait.Wait=.45f; }
            return;
        }
        c.DodgeCooldown=Math.Max(0,c.DodgeCooldown-dt); c.Burst=Math.Max(0,c.Burst-dt);
        bool fleeing=c.State==CoderAIState.PanickedFleeing;
        if(!fleeing)
        {
            UpdateStress(c,dt);
            if(c.Stress>=100 || bait==null) { Spook(c); return; }
        }
        if(c.ExitingRoom)
        {
            roomHold[c.EntryRoom]=.6f;
            c.FacingYaw=s<0 ? MathF.PI : 0f;
            if(RoomOpen[c.EntryRoom]>.68f) c.Position.Z+=s*dt*1.3f;
            c.ExitingRoom=(c.Position.Z-door.Z)*s<.95f;
            return;
        }
        int step=(int)(c.WalkCycle/MathF.PI);
        c.WalkCycle+=dt*(fleeing ? 14 : 5);
        if(c.State==CoderAIState.SneakingToBait && step!=(int)(c.WalkCycle/MathF.PI) && Vector3.DistanceSquared(c.Position,hostEye)<100)
            AudioMusicSystem.PlayWeaponSound(AudioMusicSystem.SfxSubduedDrop,.88f,.065f);
        if(c.State==CoderAIState.LuredAtBait) return;
        var goal=fleeing ? door+Vector3.UnitZ*(s*.8f) : bait!.Position;
        float remaining=Flat(goal-c.Position).Length();
        if(fleeing && remaining<2.8f) roomHold[c.EntryRoom]=.8f;
        float relZ=(c.Position.Z-door.Z)*s;
        if(fleeing && (remaining<.45f || relZ<.8f && Math.Abs(c.Position.X-door.X)<.65f)) { goal=door-Vector3.UnitZ*(s*.65f); c.BaitRoute=new(); }
        if(fleeing && relZ<=.04f && Math.Abs(c.Position.X-door.X)<.83f)
        {
            ActiveCoders.Remove(c);
            if(bait?.HunterId==c.Id) { bait.Wait=2; bait.Spotted=false; }
            Program.SetHudBanner(c.EntryRoom<2 ? Loc.T("Улизнул в служебку!","Escaped into staff room!") : Loc.T("Улизнул в коворкинг!","Escaped into co-working!")); return;
        }
        if(!fleeing && remaining<.47f)
        {
            c.State=CoderAIState.LuredAtBait; c.FacingYaw=MathF.Atan2(goal.X-c.Position.X,goal.Z-c.Position.Z);
            c.SpeechBubble="Accept All!"; c.SpeechTimer=3; return;
        }
        c.BaitRoute??=RouteToBait(c.Position,goal,fleeing ? .32f : .43f);
        while(c.BaitRoute.TryPeek(out var node) && Vector3.DistanceSquared(node,c.Position)<.025f) c.BaitRoute.Dequeue();
        var delta=Flat((c.BaitRoute.TryPeek(out var next) ? next : goal)-c.Position);
        float length=delta.Length();
        if(length<.001f) return;
        var dir=delta/length;
        c.Position=ResolvePlayerCollision(c.Position+dir*Math.Min(length,dt*(fleeing ? c.Burst>0 ? 6.2f : 4.8f : 1.28f)),.24f,allowOpenRoomDoors:fleeing && RoomOpen[c.EntryRoom]>.45f);
        c.FacingYaw=MathF.Atan2(dir.X,dir.Z)+(fleeing ? 0 : .24f*MathF.Sin(c.WalkCycle*.7f));
    }
}
