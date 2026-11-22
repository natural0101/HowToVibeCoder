#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public enum SubscriptionBaitTier
{
    FreeTier=0, CursorPro=1, TurkishAccount=2, CloudEntropicMax=3
}

public enum CoderArchetype
{
    JuniorPrompter = 0,
    CryptoBro = 1,
    TeamLeadMeeter = 2,
    SaasGuruMiniBoss = 3,
    SeniorArchitect = 4, ToxicPM = 5, SecurityGuard = 6
}

public enum CoderAIState
{
    WalkingToBait = 0,
    SittingAtMacBook = 1,
    CombatHostile = 2,
    PanickedFleeing = 3,
    Subdued = 4, Peeking, SneakingToBait, LuredAtBait
}

public enum ElevatorBossState
{
    Inactive = 0,
    Repairing = 7, // Preserve numeric states in existing saves.
    IntroSpeech = 1,
    ChargingPlayer = 2,
    StunnedWallCrash = 3,
    SpinningWhileTrue = 4,
    ThrowingNodeModules = 5,
    Defeated = 6,
    Dying = 8
}

public sealed class VibeCoderEntity
{
    public int Id;
    public CoderArchetype Archetype;
    public string Name = "";
    public string SpeciesId = "";
    public VibeCoderRarity Rarity;
    internal Queue<Vector3>? BaitRoute;
    public int EntryRoom=-1;
    public bool ExitingRoom;
    public bool IdentityRevealed, TimidIntern, KnockedOutAlive;
    internal bool CanSignNda => (State==CoderAIState.Subdued || StunTimer>0) && (Hp>0 || KnockedOutAlive);
    internal string MysteryTag => (Rarity==VibeCoderRarity.Common ? "???" : "★ ???") +
        (State==CoderAIState.Subdued ? "  Сломлен" : "");
    public string Subtitle = "";
    public Vector3 Position;
    public Vector3 Velocity;
    public float FacingYaw; // Radians
    public float MaxHp;
    public float Hp;
    public bool FloorHunter, EscapingToDoor;
    public float DodgeCooldown, Stress, Burst;
    public float Anxiety; // 0..100
    public CoderAIState State;
    public float StateTimer;
    public float WalkCycle;
    public float StunTimer;
    public float HitFlashTimer, HitFlinchTimer;
    public Vector3 HitDirection;
    public bool IsCombatCoder;
    public bool Aggro, WindingSlap;
    public float Windup, HitStaggerTimer, KnockoutTime;
    public Vector3 KnockbackVel, ThrowTarget;
    public float AttackCooldown;
    public int TarotPayoutValue;
    public Color HoodieColor;
    public Color GlassesColor;
    public string SpeechBubble = "";
    public float SpeechTimer;
    public float FlippedOffTimer, ElectroShockedTimer;
    public float CaptureMultiplier = 1f;
    public int SpawnSide; // -1 = West door, +1 = East door

    public float Composure, Ego, Fear, Greed, Deadline, Ignition;
    public bool JointNda;
    public bool BaitTaken, LaptopLost, RaidGuard, ReleaseAttacker;
    public string Role => Archetype switch {
        CoderArchetype.JuniorPrompter=>"Джуниор-Промптер",CoderArchetype.CryptoBro=>"Крипто-Бро",
        CoderArchetype.TeamLeadMeeter=>"Тимлид-Митингер",CoderArchetype.SeniorArchitect=>"Сеньор-Архитектор",
        CoderArchetype.ToxicPM=>"Токсичный Проджект",CoderArchetype.SecurityGuard=>"СБ-шник",_=>"Гуру Табсора" };

    internal void AssignRole(CoderArchetype role)
    {
        Archetype=role;
        Hp=MaxHp=role switch {
            CoderArchetype.JuniorPrompter=>50, CoderArchetype.TeamLeadMeeter=>100,
            CoderArchetype.SeniorArchitect or CoderArchetype.SecurityGuard=>145,
            CoderArchetype.SaasGuruMiniBoss=>600, _=>90 };
        (Composure,Ego,Fear,Greed,HoodieColor)=role switch {
            CoderArchetype.JuniorPrompter=>(48,28,16,41,new Color(65,180,220,255)),
            CoderArchetype.CryptoBro=>(68,65,9,62,new Color(220,155,47,255)),
            CoderArchetype.TeamLeadMeeter=>(96,57,6,22,new Color(77,173,115,255)),
            CoderArchetype.SeniorArchitect=>(90,96,4,12,new Color(154,119,212,255)),
            CoderArchetype.ToxicPM=>(76,78,5,50,new Color(108,63,83,255)),
            CoderArchetype.SecurityGuard=>(95,68,2,26,new Color(39,46,58,255)),
            _=>(99,100,0,35,new Color(245,190,25,255)) };
    }

    internal void Pressure(float resolve,float ego,float fear,float greed=0)
    {
        Composure=Math.Clamp(Composure-resolve,0,100);
        Ego=Math.Clamp(Ego-ego,0,100); Fear=Math.Clamp(Fear+fear,0,100); Greed=Math.Clamp(Greed+greed,0,100);
        bool broken=Archetype switch {
            CoderArchetype.SeniorArchitect=>Ego<=35 && (Fear>=40 || Composure<=25),
            CoderArchetype.TeamLeadMeeter=>Composure<=20 || Fear>=70 && Ego<=45,
            _=>Composure<=25 || Fear>=60 && Ego<=45 || Greed>=75 && Composure<=50 };
        if(!broken || Hp<=0) return;
        State=CoderAIState.Subdued; Windup=0; SpeechTimer=0;
    }

    public bool IsMiniBoss => Archetype == CoderArchetype.SaasGuruMiniBoss;
    public const float BodyScale = .70f;
    public float ModelScale => BodyScale * (IsMiniBoss ? 1.28f : 1f);
    public Vector3 CombatCenter => Position + Vector3.UnitY * ((State==CoderAIState.LuredAtBait ? .57f : 1.05f) * ModelScale);
}

public sealed class GalleyDeskSlot
{
    public int Index;
    public Vector3 DeskPosition;
    public Vector3 ChairPosition => DeskPosition+Vector3.UnitZ*(.82f*SkyscraperFloor1World.WorkstationScale);
    // Scale about the seat surface and slide forward so the hands reach the keyboard.
    public Vector3 SeatedFeet => ChairPosition-Vector3.UnitZ*(.25f*SkyscraperFloor1World.WorkstationScale);
    public bool IsOccupied;
    public string CoderName = "";
    public string SpeciesId = "";
    public CoderArchetype Archetype;
    public Color HoodieColor;
    public Color GlassesColor = new(92,232,224,255);
    public bool IsSlackingInTwitter;
    public float SlackCycleTimer;
    public float Productivity=20, TechDebtRate, Morale=1, Burnout, TradingTimer=28;
    public float TypingPhase;
    public float IncomeAccumulator;
    public float WhackFlashTimer;
}

public enum EnemyProjectileKind
{
    CoffeeCup = 0,
    Keyboard = 1,
    Laptop = 2,
    TaserDart = 3,
    NodeModulesCrate = 4
}

public struct EnemyProjectile3D
{
    public Vector3 Position;
    public Vector3 Origin;
    public Vector3 Velocity;
    public Vector3 Rotation;
    public Vector3 AngularVelocity;
    public float Radius;
    public float Life;
    public float MaxLife;
    public int Damage;
    public string Label;
    public Color PrimaryColor;
    public Color WireColor;
    public int OwnerId;
    public bool Deflected, Coin;
    public bool IsNodeModulesCube, IsGlitch;
    public EnemyProjectileKind Kind;
}

internal struct CombatDebrisParticle3D
{
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector3 Size;
    public float Yaw;
    public float Spin;
    public float Life;
    public float MaxLife;
    public Color Tint;
    public Finish Surface;
    public bool Glowing;
}

public struct CashBurstParticle3D
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Life;
    public float MaxLife;
    public string Label;
    public Color Tint;
}

public struct PillarObstacle
{
    public Vector3 Center;
    public float HalfWidth;
    public float HalfDepth;
    public string PosterTitle;
    public string PosterSubtitle;
    public Color AccentColor;
}

public struct AmbushCoverObject
{
    public string Name;
    public Vector3 Position;
    public float Radius;
    public int VisualKind; // 0 = Leather Sofa, 1 = Giant Ficus, 2 = Water Cooler, 3 = Server Rack
}

public enum OfficePropKind
{
    CubiclePartition = 0,
    DualMonitorDesk = 1,
    SwivelChairUpright = 2,
    SwivelChairKnocked = 3,
    XeroxPhotocopier = 4,
    FilingCabinet = 5,
    RollingWhiteboard = 6,
    WaterCooler = 7,
    VendingMachine = 8,
    LeatherSofa = 9,
    PottedFicus = 10,
    TrashBin = 11,
    CeilingDuct = 12,
    MeetingGlass = 13,
    MeetingDesk = 14
}

public struct OfficeProp3D
{
    public OfficePropKind Kind;
    public Vector3 Position;
    public Vector3 Size;
    public float RotationYaw;
    public Color PrimaryColor;
    public Color AccentColor;
    public bool HasCollision;
}

internal enum LooseKind { Jira, Nda, Cup, Can, Folder }

internal sealed class LooseClutterItem(LooseKind kind,Vector3 feet,float yaw)
{
    internal Vector3 Feet=feet, Velocity;
    internal float Yaw=yaw, Pitch, Spin, Cooldown;
    internal bool Light => kind is LooseKind.Jira or LooseKind.Nda;
    internal void Draw()
    {
        if(Light)
        {
            VisualEngine3D.Sheet(Feet+Vector3.UnitY*(.008f+.146f*MathF.Abs(MathF.Sin(Pitch))),Yaw,Pitch,kind==LooseKind.Nda ? Finish.NdaSheet : Finish.JiraSheet);
            return;
        }
        if(kind==LooseKind.Folder)
        {
            VisualEngine3D.OrientedBox(Feet+Vector3.UnitY*.012f,new(.23f,.018f,.316f),Yaw,new(149,103,65,255));
            VisualEngine3D.Sheet(Feet+Vector3.UnitY*.024f,Yaw,0,Finish.NdaSheet);
            return;
        }
        bool cup=kind==LooseKind.Cup;
        float tilt=(cup ? .38f : 1.32f)+Pitch;
        Vector3 axis=new(MathF.Cos(Yaw)*MathF.Sin(tilt),MathF.Cos(tilt),MathF.Sin(Yaw)*MathF.Sin(tilt));
        float h=cup ? .145f : .112f, r=cup ? .048f : .033f;
        Vector3 basePos=Feet+Vector3.UnitY*(r*MathF.Abs(MathF.Sin(tilt))+Math.Max(0,-axis.Y*h));
        VisualEngine3D.Rod(basePos,basePos+axis*h,r*.75f,r,10,cup ? new(222,210,182,255) : new(140,57,43,255));
        VisualEngine3D.Rod(basePos+axis*h,basePos+axis*(h+.008f),r*1.05f,r,10,cup ? new(47,43,40,255) : new(170,176,177,255));
        if(cup) VisualEngine3D.Rod(basePos+axis*(h*.32f),basePos+axis*(h*.64f),r*.87f,r*.96f,10,new(155,103,66,255));
    }
}

public sealed class EscapedAgentBoss
{
    public bool IsActive;
    public bool IsDefeated;
    public string Name = "СБЕЖАВШИЙ ИИ-АГЕНТ v0.1 (CLOUD ENTROPIC)";
    public Vector3 Position = new(0f, .16f, -4f);
    public Vector3 Velocity;
    public float FacingYaw;
    public const float ShieldCapacity=160;
    public int Phase=1, TapeLine;
    public float ShieldHp=ShieldCapacity, ShieldDelay, BroadcastTime;
    public Vector3 ChestCore => Position+new Vector3(MathF.Sin(FacingYaw)*.31f,2.08f,MathF.Cos(FacingYaw)*.31f);
    public float Hp = 600f;
    public float MaxHp = 600f;
    public ElevatorBossState State = ElevatorBossState.Inactive;
    public float StateTimer;
    public float AttackSubTimer;
    public float ThrowCycleDuration = 0.95f;
    public float StridePhase;
    public float WheelSpinPhase;
    public Vector3 LookTarget = new(0f, 1.72f, -15.5f);
    public float HitFlashTimer, HitFlinchTimer;
    public Vector3 HitDirection, KnockbackVel;
    public string SpeechBubble = "";
    public float SpeechTimer;
    public Vector3 ChargeDirection;
    [System.Text.Json.Serialization.JsonIgnore] public bool SnapPose;
}

public sealed partial class SkyscraperFloor1World
{
    // Massive Warface "White Shark" («Белая Акула») Tactical Skyscraper Floor 1 (76m x 76m, Soaring 11.2m High-Ceiling Atrium!)
    public const float HalfWidth = 38.0f;
    public const float HalfDepth = 38.0f;
    public const float CeilingHeight = 11.2f;

    // Central Armored Elevator Shaft right in the CENTER of the floor (9m x 9m core)
    public readonly Vector3 CentralElevatorCorePos = new(0.0f, 0f, -4.0f);
    public const float ElevatorCoreHalfW = 4.5f;
    public const float ElevatorCoreHalfD = 4.5f;

    // Key Functional Landmarks (White Shark Layout: HR Reception Desk centered directly opposite the Elevator!)
    public readonly Vector3 HrTarotBoothPos = new(0.0f, 0f, 14.48f);
    public readonly Vector3 ArsenalShopPos = new(-6.1f, 0f, 14.7f);
    public readonly Vector3 CentralMacBookPos = new(3.6f, 0f, 10.2f);
    public readonly Vector3 NorthElevatorPos = new(0.0f, 0f, -8.5f);
    public readonly Vector3 SouthElevatorDoorPos = new(0.0f, 0f, 0.5f);
    public readonly Vector3 ElevatorButtonPos = new(-1.82f, 0f, -9.2f);
    public readonly Vector3 SouthElevatorButtonPos = new(1.82f, 0f, 1.1f);
    internal static readonly Vector3[] RoomDoors=[new(4.10f,0,16.25f),new(7.45f,0,16.25f),
        new(-17.8f,0,-15.8f),new(17.8f,0,-15.8f)];
    internal static float RoomExitSign(int room) => room < 2 ? -1f : 1f;
    internal readonly float[] RoomOpen=new float[4], VentTime=new float[3];
    private readonly float[] roomHold=new float[4];
    internal Vector3 VentMouth(int vent) => vent switch {
        0=>new(8.91f,0,11.65f),1=>RackPos+new Vector3(0,0,.49f),_=>SideRacks[0]+new Vector3(0,0,.48f) };
    private int PickRoom(int side) => (side>0 ? 1 : 0)+(Systems.WingsUnlocked && _rng.Next(2)==1 ? 2 : 0);
    private void EnterFromRoom(VibeCoderEntity coder,int room)
    {
        float s=RoomExitSign(room);
        coder.EntryRoom=room; coder.ExitingRoom=true;
        coder.Position=RoomDoors[room]+new Vector3((coder.Id%3-1)*.36f,0,-s*.8f);
        coder.FacingYaw=s<0 ? MathF.PI : 0f; roomHold[room]=2.6f;
    }
    private VibeCoderEntity SpawnRoomFighter(CoderArchetype role,int side,VibeCoderSpecies? species=null)
    {
        int room=PickRoom(side);
        var coder=SpawnFighter(role,RoomDoors[room],species);
        EnterFromRoom(coder,room);
        return coder;
    }

    // Architectural Pillars, Ambush Covers, Galley Desks & 75+ Rich 3D Office Props
    public readonly List<PillarObstacle> Pillars = new(16);
    public readonly List<AmbushCoverObject> AmbushCovers = new(8);
    public readonly List<GalleyDeskSlot> GalleyDesks = new(4);
    public readonly List<OfficeProp3D> OfficeProps = new(110);
    internal static readonly Vector2[] PendantLocations = [
        new(0,13.1f),new(3.6f,10.2f),new(-3.9f,9.7f),new(4,13),new(8.9f,13.4f),
        new(-7.6f,3.7f),new(8.75f,12),new(-15.5f,5.0f),new(-21.7f,5.0f),new(14.8f,3.5f),new(23.0f,-10.5f),
        // Warm pendant & linear bench pools illuminating West & East openspace 4-seat corporate islands & scrum plazas!
        new(-15.5f,11.5f),new(-21.7f,11.5f),new(-18.65f,8.35f),new(-26.9f,5.0f),
        new(-15.5f,-2.0f),new(-21.7f,-2.0f),new(-15.5f,-9.0f),new(-21.7f,-9.0f),
        new(-26.9f,-9.0f),new(-18.65f,-5.0f),new(15.8f,10.0f),new(23.0f,10.0f),
        new(23.0f,3.0f),new(15.8f,-4.0f),new(23.0f,-4.0f),new(19.45f,-7.0f),
        // North Wing Executive Lounge, flanking North bays & panoramic Glass Meeting Suites
        new(0f,-30.5f),new(-6.2f,-29.8f),new(6.2f,-29.8f),new(-15.8f,-29.5f),new(15.8f,-29.5f),
        new(-23.0f,-29.5f),new(23.0f,-29.5f),new(-33.75f,1.0f),new(33.75f,1.0f)];
    private static readonly Vector3 LoungeTable = new(-3.9f,0,9.7f), BreakTable = new(4,0,13);
    private static readonly Vector3[] OpenspaceScrumTables = [
        new(-18.65f,0,8.35f), new(-24.85f,0,1.8f), new(-18.65f,0,-5.0f),
        new(19.45f,0,7.0f),   new(19.45f,0,-7.0f)
    ];
    private static readonly Vector3 ArenaTable=new(3.1f,0,-12.3f);
    private Vector3 CartPosition => RackPos+new Vector3(1.13f,0,-.66f);
    private static readonly Vector3[] SideRacks=[new(12.85f,0,-12.8f),new(-22.5f,0,-12.5f)];

    // Dynamic Entities
    public readonly List<VibeCoderEntity> ActiveCoders = new(16);
    public readonly List<EnemyProjectile3D> EnemyProjectiles = new(64);
    public readonly List<CashBurstParticle3D> CashParticles = new(64);
    public EscapedAgentBoss ElevatorBoss = new();
    internal readonly PartnerAvatar?[] Partners=new PartnerAvatar?[3];
    internal IEnumerable<PartnerAvatar> Teammates => Partners.OfType<PartnerAvatar>();
    internal PartnerAvatar? Partner
    {
        get => Partners[0] ?? Partners[1] ?? Partners[2];
        set { Array.Clear(Partners); if(value!=null) Partners[value.Slot]=value; }
    }

    // Central MacBook Trap State
    public SubscriptionBaitTier SelectedBaitTier = SubscriptionBaitTier.FreeTier;
    public bool IsMacBookTrapActive = false;
    public float MacBookHologramPhase = 0f;
    public float CoderSpawnTimer = 0f;
    private int _nextCoderId = 1;

    // Ambush & Player Carry State
    public bool IsPlayerInAmbush = false;
    public string ActiveAmbushCoverName = "";
    public VibeCoderEntity? CarriedCoder = null;

    // HR Veronika Tarot Feedback State
    public string LastTarotCardDrawn = "XVI БАШНЯ (ПЕРЕВЁРНУТАЯ)";
    public string LastTarotVerdictText = "Сдай пойманного вайб-кодера на разбор натальной карты!";
    public float TarotVerdictTimer = 0f;
    internal readonly SlotRoulette Slot=new();
    public int TotalCodersSoldToHr = 0;

    // Elevator & Progression State
    internal const float RepairSeconds = 2.3f, IntroSeconds = 2.4f, MeltdownSeconds = 1.85f;
    public float ElevatorGlassOpacity = 1f;
    public bool ElevatorRepaired;
    internal bool BrokenElevator => !ElevatorRepaired && !ElevatorBoss.IsActive && !ElevatorBoss.IsDefeated;
    internal bool BossRevealed => ElevatorBoss.IsActive && ElevatorBoss.State != ElevatorBossState.Repairing;
    internal float BossHudOpacity => !BossRevealed ? 0 : ElevatorBoss.State == ElevatorBossState.IntroSpeech ?
        Smooth((IntroSeconds-ElevatorBoss.StateTimer)/.45f) : 1;
    internal bool BossInCombat => BossRevealed && ElevatorBoss.State != ElevatorBossState.IntroSpeech &&
        ElevatorBoss.State != ElevatorBossState.Dying && !BossEscapedPursuit;
    internal bool BossModelVisible =>
        (BossRevealed && !(ElevatorBoss.State == ElevatorBossState.Dying && (MeltdownSeconds - ElevatorBoss.StateTimer) >= 1.45f)) ||
        (ElevatorBoss.IsActive && ElevatorBoss.State == ElevatorBossState.Repairing && ElevatorBoss.StateTimer < RepairSeconds * .5f && ElevatorGlassOpacity < .65f) ||
        (ElevatorBoss.IsDefeated && !BossWreckageActive && Systems.Verdict == AgentVerdict.Pending);
    public bool HasElevatorKey = false;
    public float ElevatorDoorOpenAmount = 0f; // 0 = closed, 1 = open
    public bool IsFloor2Unlocked = false;
    public bool BossEscapedPursuit = false;
    public bool BossWreckageActive = false;
    public Vector3 BossWreckagePos;
    public float BossWreckageYaw;
    private float _bossMeltdownBurstAge = -1f;
    private int _bossMeltdownStageMask = 0;
    private float _bossTackleCooldown = 0f;
    private float _escapePursuitTimer = -1f;
    private bool _escapeDoorSmashed = false;
    private readonly List<CombatDebrisParticle3D> _combatDebris = new(128);
    public float WarningBeaconPhase = 0f;

    // Burning Chaos Boss Arena & Cutscene Set-Piece State
    internal static readonly Vector3 BossArenaWallCenter = new(0f, 2.1f, -15.8f);
    internal static readonly Vector3 BossArenaWallSize = new(8.2f, 4.2f, 0.24f);
    internal static readonly BoundingBox BossArenaWallBox = new(
        BossArenaWallCenter - BossArenaWallSize * 0.5f,
        BossArenaWallCenter + BossArenaWallSize * 0.5f);
    private static readonly BoundingBox ArenaTableBox = new(
        ArenaTable - new Vector3(0.75f, 0f, 0.40f),
        ArenaTable + new Vector3(0.75f, 0.80f, 0.40f));

    public float BossArenaChaos = 0f;
    public float CutsceneRocketTime = -1f;
    public bool BossArenaWallDestroyed =>
        BossArenaChaos >= 0.35f ||
        (ElevatorBoss.IsActive && ElevatorBoss.State != ElevatorBossState.Repairing && ElevatorBoss.State != ElevatorBossState.IntroSpeech) ||
        ElevatorBoss.IsDefeated;
    internal bool BossArenaWallGlassShattered =>
        BossArenaWallDestroyed ||
        (ElevatorBoss.IsActive && ElevatorBoss.State == ElevatorBossState.IntroSpeech &&
         (IntroSeconds - ElevatorBoss.StateTimer) / IntroSeconds >= 0.240f);
    public bool CutsceneShowDroppedGun = false;
    public Vector3 CutsceneDroppedGunPos;
    public float CutsceneDroppedGunYaw = 0f;
    private readonly Vector3[] _cutsceneMissileOrigins = new Vector3[4];
    private readonly bool[] _cutsceneMissileFired = new bool[4];

    private const float SofaScale=.68f;
    // GLB is 2.46 x 1.02 x 1.09 m. Preserve seat/back height at the narrower footprint.
    private static readonly Vector3 SofaStretch=new(1,.80f/SofaScale,1);
    private static readonly Vector3 SofaSize=new(2.46f*SofaScale,.816f,1.09f*SofaScale);
    internal const float TrapScale = .90f, WorkstationScale = .92f;
    internal const float TrapTop = .94f * TrapScale;
    private readonly List<BoundingBox> solids = new();
    private float _worldClock = 0f;
    internal readonly List<LooseClutterItem> LooseClutter=new(30);
    private float clutterRemainder,previousSteam,previousRackSmoke,previousPocketSmoke;
    private readonly Dictionary<Vector3,float> chargedSpills=new();

    private void SeedLooseClutter()
    {
        LooseClutter.Clear();
        clutterRemainder=previousSteam=previousRackSmoke=previousPocketSmoke=0;
        chargedSpills.Clear();
        var rng=new Random(812);
        Vector2[] clusters=[new(-11.25f,2.76f),new(11.3f,2.78f),new(4.0f,11.82f),
            new(-5.03f,9.72f),new(-14.35f,6.85f),new(7.1f,12.05f),new(6.03f,3.07f),
            new(-17.6f,9.1f),new(-21.2f,11.4f),new(-15.2f,-1.2f),new(-23.6f,2.2f),
            new(18.2f,6.8f),new(15.2f,10.2f),new(18.5f,-6.5f)];
        for(int i=0;i<56;i++)
        {
            int group=i%clusters.Length;
            Vector2 xy=clusters[group]+new Vector2(rng.NextSingle()-.5f,rng.NextSingle()-.5f)*.91f;
            var kind=group<2 ? (i%3==0 ? LooseKind.Folder : (LooseKind)(i%2)) : (LooseKind)((i+i/7)%5);
            var feet=ResolvePlayerCollision(new(xy.X,0,xy.Y),.12f);
            LooseClutter.Add(new(kind,feet,rng.NextSingle()*MathF.Tau));
        }
    }

    internal void Scatter(Vector3 source,Vector3 direction,float radius,float strength)
    {
        direction.Y=0;
        for(int i=0;i<LooseClutter.Count;i++)
        {
            var item=LooseClutter[i];
            Vector3 away=item.Feet-source;
            float falloff=1-away.Length()/radius;
            if(falloff<=0 || item.Cooldown>0 && strength<=3) continue;
            away.Y=0;
            if(away.LengthSquared()>.0001f) away=Vector3.Normalize(away);
            float kick=(.25f+.75f*falloff)*strength*(item.Light ? 1.12f : .86f);
            item.Velocity=(away*.73f+direction*.54f+Vector3.UnitY*(.76f+.06f*(i%4)))*kick;
            item.Spin=(i%2==0 ? -1 : 1)*kick*(1.2f+.2f*(i%3));
            item.Cooldown=.42f;
        }
    }

    internal void UpdateLooseClutter(float dt)
    {
        // Existing replicated environmental timers identify a discharge on either client.
        void Burst(float time,ref float previous,Vector3 origin,float radius)
        {
            if(time>previous+.01f) Scatter(origin,Vector3.Zero,radius,5.8f);
            previous=time;
        }
        Burst(Systems.Steam,ref previousSteam,CoffeePos,3.5f);
        Burst(Systems.RackSmoke,ref previousRackSmoke,RackPos,5);
        Burst(Systems.PocketSmoke,ref previousPocketSmoke,Systems.PocketOrigin,3.5f);
        if(Systems.Puddles.Count==0) chargedSpills.Clear();
        foreach(var spill in Systems.Puddles)
        {
            if(spill.Electric>chargedSpills.GetValueOrDefault(spill.Position)+.01f)
                Scatter(spill.Position,Vector3.Zero,3.2f,5.4f);
            chargedSpills[spill.Position]=spill.Electric;
        }
        const float step=1f/60;
        clutterRemainder+=Math.Min(dt,.1f);
        while(clutterRemainder>=step)
        {
            clutterRemainder-=step;
            foreach(var item in LooseClutter)
            {
                item.Cooldown=Math.Max(0,item.Cooldown-step);
                if(item.Velocity==Vector3.Zero) continue;
                item.Velocity.Y-=(item.Light ? 4.8f : 11.5f)*step;
                Vector3 next=item.Feet+item.Velocity*step;
                Vector3 clear=ResolvePlayerCollision(next,.045f,Math.Max(next.Y,0));
                if(Math.Abs(clear.X-next.X)>.001f) item.Velocity.X*=-.28f;
                if(Math.Abs(clear.Z-next.Z)>.001f) item.Velocity.Z*=-.28f;
                float support=SupportHeight(clear,item.Feet.Y,.045f);
                item.Yaw+=item.Spin*step;
                item.Pitch=item.Light ? MathF.Sin(item.Yaw*1.65f)*.67f : MathF.Sin(item.Yaw)*.84f;
                if(clear.Y<=support)
                {
                    clear.Y=support;
                    item.Velocity*=.83f;
                    item.Velocity.Y=item.Velocity.Y<-.70f ? -item.Velocity.Y*.21f : 0;
                    item.Spin*=.85f;
                    item.Pitch=0;
                    if(item.Velocity.LengthSquared()<.01f) item.Velocity=Vector3.Zero;
                }
                item.Feet=clear;
            }
        }
    }
    private readonly Random speciesRandom, dodgeRandom;
    private readonly Random _rng = new(1994);

    public SkyscraperFloor1World(int? speciesSeed=null)
    {
        dodgeRandom=speciesSeed.HasValue ? new Random(speciesSeed.Value+8) : Random.Shared;
        frogRandom=speciesSeed.HasValue ? new Random(speciesSeed.Value+83) : new Random();
        speciesRandom=speciesSeed.HasValue ? new Random(speciesSeed.Value) : new Random();
        InitializeFloorLayout();
    }

    public void InitializeFloorLayout()
    {
        Array.Clear(RoomOpen); Array.Clear(roomHold); Array.Clear(VentTime);
        Pillars.Clear();
        AmbushCovers.Clear();
        GalleyDesks.Clear();
        OfficeProps.Clear();
        ActiveCoders.Clear();
        EnemyProjectiles.Clear();
        CashParticles.Clear();

        // 10 Concrete & Neon Skyscraper Pillars across the 76x76m Floor with Satirical Corporate Posters
        Pillars.Add(new PillarObstacle
        {
            Center = new Vector3(-7.8f, 0f, 9.2f),
            HalfWidth = 0.90f,
            HalfDepth = 0.90f,
            PosterTitle = "ПИШИ КОД ПРОМПТАМИ",
            PosterSubtitle = "ДУМАТЬ ВРЕДНО - ЖМИ TAB!",
            AccentColor = new Color(0, 240, 255, 255)
        });
        Pillars.Add(new PillarObstacle
        {
            Center = new Vector3(7.8f, 0f, 9.2f),
            HalfWidth = 0.90f,
            HalfDepth = 0.90f,
            PosterTitle = "СЕНЬОРЫ НЕ НУЖНЫ",
            PosterSubtitle = "НАЖМИ ACCEPT ALL В ПЯТНИЦУ!",
            AccentColor = new Color(255, 50, 140, 255)
        });
        Pillars.Add(new PillarObstacle
        {
            Center = new Vector3(-9.5f, 0f, -4.0f),
            HalfWidth = 0.90f,
            HalfDepth = 0.90f,
            PosterTitle = "< РАЗВИЛКА ВЛЕВО",
            PosterSubtitle = "ЗАПАДНЫЙ ОПЕНСПЕЙС КУБИКЛОВ",
            AccentColor = new Color(255, 195, 35, 255)
        });
        Pillars.Add(new PillarObstacle
        {
            Center = new Vector3(9.5f, 0f, -4.0f),
            HalfWidth = 0.90f,
            HalfDepth = 0.90f,
            PosterTitle = "РАЗВИЛКА ВПРАВО >",
            PosterSubtitle = "ВОСТОЧНАЯ СЕРВЕРНАЯ И КОПИРЫ",
            AccentColor = new Color(60, 255, 150, 255)
        });
        Pillars.Add(new PillarObstacle
        {
            Center = new Vector3(-7.8f, 0f, -15.5f),
            HalfWidth = 0.90f,
            HalfDepth = 0.90f,
            PosterTitle = "ОСТОРОЖНО: СЕРВЕРНАЯ B12",
            PosterSubtitle = "СИНЯЯ ИЗОЛЕНТА ДЕРЖИТ ПРОД",
            AccentColor = new Color(255, 70, 70, 255)
        });
        Pillars.Add(new PillarObstacle
        {
            Center = new Vector3(7.8f, 0f, -15.5f),
            HalfWidth = 0.90f,
            HalfDepth = 0.90f,
            PosterTitle = "ЛИФТ НА 2-Й ЭТАЖ",
            PosterSubtitle = "ДОПУСК ТОЛЬКО С КЛЮЧ-КАРТОЙ",
            AccentColor = new Color(185, 110, 255, 255)
        });
        Pillars.Add(new PillarObstacle
        {
            Center = new Vector3(-18.5f, 0f, 2.0f),
            HalfWidth = 1.05f,
            HalfDepth = 1.05f,
            PosterTitle = "ЗАПАДНОЕ КРЫЛО",
            PosterSubtitle = "КУБИКЛЫ И КСЕРОКСЫ 1994",
            AccentColor = new Color(70, 215, 255, 255)
        });
        Pillars.Add(new PillarObstacle
        {
            Center = new Vector3(18.5f, 0f, 2.0f),
            HalfWidth = 1.05f,
            HalfDepth = 1.05f,
            PosterTitle = "ВОСТОЧНОЕ КРЫЛО",
            PosterSubtitle = "АРХИВ И БУХГАЛТЕРИЯ",
            AccentColor = new Color(255, 155, 55, 255)
        });
        Pillars.Add(new PillarObstacle
        {
            Center = new Vector3(-16.5f, 0f, -21.0f),
            HalfWidth = 1.05f,
            HalfDepth = 1.05f,
            PosterTitle = "СЕВЕРНЫЙ ХОЛЛ",
            PosterSubtitle = "СЕВЕРНЫЙ ЛИФТ · КАРАНТИН СБ",
            AccentColor = new Color(255, 85, 125, 255)
        });
        Pillars.Add(new PillarObstacle
        {
            Center = new Vector3(16.5f, 0f, -21.0f),
            HalfWidth = 1.05f,
            HalfDepth = 1.05f,
            PosterTitle = "CLOUD ENTROPIC",
            PosterSubtitle = "КОРПОРАЦИЯ «ЭНТРОПИК»",
            AccentColor = new Color(90, 245, 175, 255)
        });

        // Atrium lounge and server-wing ambush covers.
        AmbushCovers.Add(new AmbushCoverObject
        {
            Name = "Кожаный Диван из 90-х",
            Position = new Vector3(-3.9f, 0f, 8.2f),
            Radius = 2.55f,
            VisualKind = 0
        });
        AmbushCovers.Add(new AmbushCoverObject
        {
            Name = "Развесистый Офисный Фикус",
            Position = new Vector3(3.9f, 0f, 8.1f),
            Radius = 2.45f,
            VisualKind = 1
        });
        AmbushCovers.Add(new AmbushCoverObject
        {
            Name = "Офисный Кулер с Бутылью",
            Position = new Vector3(4.80f, 0f, -4.1f),
            Radius = 2.30f,
            VisualKind = 2
        });
        AmbushCovers.Add(new AmbushCoverObject
        {
            Name = "Серверная Стойка Опенспейса",
            Position = new Vector3(12.2f, 0f, 1.4f),
            Radius = 2.45f,
            VisualKind = 3
        });

        // Playable workstations replace the first two rows of west-wing pods.
        Vector3[] galleyCoords =
        {
            new(-14.5f, 0f, 5.5f),
            new(-20.7f, 0f, 5.5f),
            new(-14.5f, 0f, 12f),
            new(-20.7f, 0f, 12f)
        };

        for (int i = 0; i < 4; i++)
        {
            GalleyDesks.Add(new GalleyDeskSlot
            {
                Index = i,
                DeskPosition = galleyCoords[i],
                IsOccupied = false,
                CoderName = "",
                IsSlackingInTwitter = false,
                SlackCycleTimer = 18f + i * 4f
            });
        }

        // Populate 85+ 3D Office Props across the Left Fork, Right Fork, Central Atrium, and North Hall!
        PopulateRichOfficeProps();
        MapStations();
        BuildSolids();
        SeedLooseClutter();
        Systems=new();
        IsMacBookTrapActive=false;
        SeedLobby();
        // Reset Boss & Progression
        ElevatorBoss=new() { Position=CentralElevatorCorePos+Vector3.UnitY*.16f };
        ElevatorDoorOpenAmount = 0f;
        ElevatorGlassOpacity = 1f;
        ElevatorRepaired = HasElevatorKey = false;
        IsFloor2Unlocked = false;
        BossEscapedPursuit = false;
        BossWreckageActive = false;
        _bossMeltdownBurstAge = -1f;
        _bossMeltdownStageMask = 0;
        _bossTackleCooldown = 0f;
        _escapePursuitTimer = -1f;
        _escapeDoorSmashed = false;
        _combatDebris.Clear();
        CarriedCoder = null;
        BossArenaChaos = 0f;
        CutsceneRocketTime = -1f;
        CutsceneShowDroppedGun = false;
        CutsceneDroppedGunPos = Vector3.Zero;
        CutsceneDroppedGunYaw = 0f;
    }

    private void PopulateRichOfficeProps()
    {
        // Bug-tracker lounge sofa rotated cleanly to face LoungeTable (-3.9, 9.7) — ZERO clipping into TerminalPos (-3.1, 10.85)!
        OfficeProps.Add(new() {Kind=OfficePropKind.LeatherSofa,Position=new(-5.65f,0,9.70f),RotationYaw=90f,Size=new(SofaSize.Z,SofaSize.Y,SofaSize.X),HasCollision=true});
        foreach (Vector3 feet in new Vector3[]{new(-5.65f,0,8.15f),new(-6.05f,0,11.55f)})
            OfficeProps.Add(new() {Kind=OfficePropKind.PottedFicus,Position=feet,Size=new(.55f,1.8f,.55f)});
        // Symmetrical Art-Deco reception seating alcoves flanking the freed-up middle lobby (replaces old floating boxes!)
        foreach (int side in new[] { -1, 1 })
        {
            OfficeProps.Add(new() {Kind=OfficePropKind.LeatherSofa,Position=new(side*8.85f,0,1.85f),RotationYaw=-side*90f,Size=new(SofaSize.Z,SofaSize.Y,SofaSize.X),HasCollision=true});
            OfficeProps.Add(new() {Kind=OfficePropKind.PottedFicus,Position=new(side*8.85f,0,3.18f),Size=new(.60f,1.85f,.60f)});
        }
        OfficeProps.Add(new() {Kind=OfficePropKind.RollingWhiteboard,Position=new(12.8f,0,14.6f),RotationYaw=-125,Size=new(1.9f,1.8f,.55f),HasCollision=true});
        PlaceDispenser(0); // Preserve station indices used by saved drink counts and cooldowns.
        OfficeProps.Add(new() {Kind=OfficePropKind.TrashBin,Position=new(4.97f,0,13.28f),Size=new(.4f,.5f,.4f)});
        // 1. LEFT FORK (WEST LIVING OPENSPACE: CONNECTED 4-SEAT 2x2 CORPORATE BENCH ISLANDS, SCRUM PLAZAS & GREENERY)
        void AddBenchDesk(float cx, float rz, float yaw, int seed)
        {
            OfficeProps.Add(new OfficeProp3D
            {
                Kind = OfficePropKind.DualMonitorDesk,
                Position = new Vector3(cx, 0f, rz),
                RotationYaw = yaw,
                Size = new Vector3(2.3f, 1.46f, 1.35f),
                PrimaryColor = new Color(68, 74, 88, 255),
                AccentColor = (seed % 2 == 0) ? new Color(0, 235, 255, 255) : new Color(65, 250, 145, 255),
                HasCollision = true
            });
            if (seed % 2 == 0)
            {
                OfficeProps.Add(new OfficeProp3D
                {
                    Kind = OfficePropKind.TrashBin,
                    Position = new Vector3(cx + (yaw == 0f ? 1.05f : -1.05f), 0f, rz + (yaw == 0f ? 0.28f : -0.28f)),
                    Size = new Vector3(0.34f, 0.44f, 0.34f),
                    PrimaryColor = new Color(52, 58, 70, 255),
                    AccentColor = new Color(240, 240, 248, 255),
                    HasCollision = false
                });
            }
        }
        float[] westRowsZ = { 12.0f, 5.5f, -1.5f, -8.5f, -24.5f, -31.0f };
        for (int r = 0; r < westRowsZ.Length; r++)
        {
            float rz = westRowsZ[r];
            for (int c = 0; c < 3; c++)
            {
                float cx = -14.5f - c * 6.2f;
                bool isGalley = r < 2 && c < 2;
                // Keep playable GalleyDesks in GalleyDesks list; populate companion desks to form 4-seat (2x2) bench islands!
                if (isGalley)
                {
                    AddBenchDesk(cx - 2.09f, rz, 0f, r * 5 + c + 1);           // Side companion
                    AddBenchDesk(cx, rz - 1.07f, 180f, r * 5 + c + 2);         // Back-to-back twin behind GalleyDesk
                    AddBenchDesk(cx - 2.09f, rz - 1.07f, 180f, r * 5 + c + 3); // Diagonal twin completing 2x2 island
                    continue;
                }
                AddBenchDesk(cx, rz, 0f, r * 3 + c);
                // Side companion completing 2x2 corporate bench rows
                if (c < 2)
                    AddBenchDesk(cx - 2.09f, rz, 0f, r * 3 + c + 7);
            }
        }

        // Lived-in West & East Openspace corner ficuses, angled scrum boards & filing credenzas
        // framing the Galley & connected 4-seat bench islands without obstructing aisles or sightlines!
        foreach (var (px, pz) in new (float, float)[] {
            (-11.8f, 15.6f), (-16.80f, 15.4f), (-24.8f, 14.8f), (-11.8f, 2.8f),
            (-24.9f, 5.5f), (-18.65f, -1.8f), (-24.9f, -8.5f), (-11.9f, -14.2f),
            (11.2f, 15.8f), (21.25f, 13.8f), (11.9f, 4.2f), (19.45f, -0.4f), (12.2f, -14.4f)
        })
        {
            OfficeProps.Add(new OfficeProp3D
            {
                Kind = OfficePropKind.PottedFicus,
                Position = new Vector3(px, 0f, pz),
                Size = new Vector3(0.75f, 1.90f, 0.75f),
                HasCollision = false
            });
        }
        foreach (var (wx, wz, wyaw) in new (float, float, float)[] {
            (-18.65f, 15.2f, 18f), (-25.0f, 8.8f, -30f), (-18.65f, -12.4f, 148f),
            (19.45f, 14.2f, -20f), (26.2f, 0.2f, 34f)
        })
        {
            OfficeProps.Add(new OfficeProp3D
            {
                Kind = OfficePropKind.RollingWhiteboard,
                Position = new Vector3(wx, 0f, wz),
                RotationYaw = wyaw,
                Size = new Vector3(1.9f, 1.85f, 0.35f),
                HasCollision = true
            });
        }
        foreach (var (fx, fz, fyaw) in new (float, float, float)[] {
            (-11.6f, 14.1f, 90f), (-24.85f, 12.0f, 180f), (-11.7f, -4.4f, 90f),
            (11.8f, 12.6f, -90f), (26.2f, 7.2f, -90f)
        })
        {
            OfficeProps.Add(new OfficeProp3D
            {
                Kind = OfficePropKind.FilingCabinet,
                Position = new Vector3(fx, 0f, fz),
                RotationYaw = fyaw,
                Size = new Vector3(0.85f, 0.92f, 0.95f),
                HasCollision = true
            });
        }

        // 2. RIGHT FORK (EAST SERVER & ARCHIVE WING: CONNECTED 4-SEAT BENCH ISLANDS, ARCHIVES & LOUNGES)
        float[] eastRowsZ = { 10.5f, 3.5f, -3.5f, -10.5f, -24.5f, -31.0f };
        for (int r = 0; r < eastRowsZ.Length; r++)
        {
            float rz = eastRowsZ[r];
            for (int c = 0; c < 2; c++)
            {
                float cx = 14.8f + c * 7.2f;
                AddBenchDesk(cx, rz, 0f, r * 3 + c + 11);
                // Side companion completing 2x2 bench island (clear of Pillar 7 at (18.5,2.0))
                bool blockedEastCompanion = (c == 0 && r == 1);
                if (!blockedEastCompanion)
                    AddBenchDesk(cx + 2.09f, rz, 0f, r * 3 + c + 19);
            }

            // Tall Metal Filing Cabinets along East Wall (skip Z=-3.5..+5.5 meeting suite & -31.0 window ficus)
            if (rz > 6.0f || (rz < -6.0f && rz > -28.0f))
            {
                OfficeProps.Add(new OfficeProp3D
                {
                    Kind = OfficePropKind.FilingCabinet,
                    Position = new Vector3(35.8f, 0f, rz),
                    Size = new Vector3(0.95f, 1.85f, 1.25f),
                    PrimaryColor = new Color(82, 88, 102, 255),
                    AccentColor = new Color(215, 222, 235, 255),
                    HasCollision = true
                });
            }
        }

        // 2B. DENSE PANORAMIC WINDOW WORKSTATION ISLANDS, LOUNGE NOOKS & NORTH SERVER CORRIDOR
        // Eliminates the barren empty warehouse perimeter along X = ±29..±36 and Z = -34..-14 while keeping all aisles wide!
        float[] windowBayZ = { 13.5f, -9.5f, -17.5f, -25.5f, -32.5f };
        for (int i = 0; i < windowBayZ.Length; i++)
        {
            float wz = windowBayZ[i];
            foreach (int side in new[] { -1, 1 })
            {
                // Window-side executive dual-monitor desk cluster
                OfficeProps.Add(new OfficeProp3D
                {
                    Kind = OfficePropKind.DualMonitorDesk,
                    Position = new Vector3(side * 31.2f, 0f, wz),
                    RotationYaw = side < 0 ? 90f : -90f,
                    Size = new Vector3(2.3f, 1.46f, 1.35f),
                    PrimaryColor = new Color(68, 72, 84, 255),
                    AccentColor = side < 0 ? new Color(65, 235, 185, 255) : new Color(245, 185, 72, 255),
                    HasCollision = true
                });
                // Potted Ficus framing every window bay for warm lived-in office greenery
                OfficeProps.Add(new OfficeProp3D
                {
                    Kind = OfficePropKind.PottedFicus,
                    Position = new Vector3(side * 35.4f, 0f, wz + 2.4f),
                    Size = new Vector3(0.85f, 2.05f, 0.85f),
                    HasCollision = false
                });
                // Alternating lounge sofa / rolling whiteboard / vending machine in the outer bay
                if (i % 3 == 0)
                    OfficeProps.Add(new OfficeProp3D
                    {
                        Kind = OfficePropKind.LeatherSofa,
                        Position = new Vector3(side * 34.2f, 0f, wz - 2.6f),
                        RotationYaw = side < 0 ? 90f : -90f,
                        Size = new Vector3(SofaSize.Z, SofaSize.Y, SofaSize.X),
                        HasCollision = true
                    });
                else if (i % 3 == 1)
                    OfficeProps.Add(new OfficeProp3D
                    {
                        Kind = OfficePropKind.RollingWhiteboard,
                        Position = new Vector3(side * 28.4f, 0f, wz - 2.5f),
                        RotationYaw = side < 0 ? 90f : -90f,
                        Size = new Vector3(0.35f, 1.85f, 1.9f),
                        HasCollision = true
                    });
                else
                    OfficeProps.Add(new OfficeProp3D
                    {
                        Kind = OfficePropKind.VendingMachine,
                        Position = new Vector3(side * 35.2f, 0f, wz - 2.4f),
                        RotationYaw = side < 0 ? 90f : -90f,
                        Size = new Vector3(1.1f, 2.3f, 1.1f),
                        HasCollision = true
                    });
            }
        }
        // 2C. DENSELY FURNISHED NORTH WING (Z = -17..-35.5) — Boss Combat Arena (X=-6..+6, Z=-8.5..-24.5) stays 100% unobstructed!
        // Flanking Server & Archive Credenza bays immediately visible North through the Quarantine Glass Barrier:
        foreach (int side in new[] { -1, 1 })
        {
            foreach (float sz in new[] { -19.2f, -21.2f, -23.2f })
            {
                OfficeProps.Add(new OfficeProp3D
                {
                    Kind = OfficePropKind.FilingCabinet,
                    Position = new Vector3(side * 8.4f, 0f, sz),
                    RotationYaw = side < 0 ? 90f : -90f,
                    Size = new Vector3(0.95f, 1.85f, 1.25f),
                    PrimaryColor = new Color(72, 78, 92, 255),
                    AccentColor = new Color(215, 222, 235, 255),
                    HasCollision = true
                });
            }
            // Lush greenery & sprint whiteboards framing the flanking North bays
            OfficeProps.Add(new OfficeProp3D { Kind = OfficePropKind.PottedFicus, Position = new(side * 7.2f, 0f, -17.6f), Size = new(0.80f, 1.95f, 0.80f), HasCollision = false });
            OfficeProps.Add(new OfficeProp3D { Kind = OfficePropKind.PottedFicus, Position = new(side * 7.2f, 0f, -25.4f), Size = new(0.80f, 1.95f, 0.80f), HasCollision = false });
            OfficeProps.Add(new OfficeProp3D { Kind = OfficePropKind.RollingWhiteboard, Position = new(side * 11.4f, 0f, -21.2f), RotationYaw = side < 0 ? 90f : -90f, Size = new(0.35f, 1.90f, 2.0f), HasCollision = true });
            OfficeProps.Add(new OfficeProp3D { Kind = OfficePropKind.VendingMachine, Position = new(side * 10.6f, 0f, -26.5f), RotationYaw = 0f, Size = new(1.15f, 2.30f, 1.15f), HasCollision = true });
            OfficeProps.Add(new OfficeProp3D { Kind = OfficePropKind.VendingMachine, Position = new(side * 20.5f, 0f, -20.0f), RotationYaw = side < 0 ? 90f : -90f, Size = new(1.15f, 2.30f, 1.15f), HasCollision = true });
            // Far North Panoramic Executive Lounge (X=-14..+14, Z=-27..-35): facing leather sofa islands + whiteboards + panoramic ficuses!
            OfficeProps.Add(new OfficeProp3D { Kind = OfficePropKind.LeatherSofa, Position = new(side * 5.8f, 0f, -28.0f), RotationYaw = 0f, Size = SofaSize, HasCollision = true });
            OfficeProps.Add(new OfficeProp3D { Kind = OfficePropKind.LeatherSofa, Position = new(side * 5.8f, 0f, -33.0f), RotationYaw = 180f, Size = SofaSize, HasCollision = true });
            OfficeProps.Add(new OfficeProp3D { Kind = OfficePropKind.RollingWhiteboard, Position = new(side * 10.8f, 0f, -30.5f), RotationYaw = side < 0 ? 90f : -90f, Size = new(0.35f, 1.90f, 2.0f), HasCollision = true });
            foreach (float fx in new[] { 3.6f, 12.2f, 20.5f, 27.5f })
                OfficeProps.Add(new OfficeProp3D { Kind = OfficePropKind.PottedFicus, Position = new(side * fx, 0f, -35.4f), Size = new(0.85f, 2.05f, 0.85f), HasCollision = false });
        }
        // Centerpiece Far-North Panoramic Executive Conference Table overlooking the Moscow-City skyline!
        OfficeProps.Add(new OfficeProp3D { Kind = OfficePropKind.MeetingDesk, Position = new(0f, 0f, -30.5f), RotationYaw = 0f, Size = new(2.1f, 1.4f, 1.05f), HasCollision = true });

        // 3. LARGE XEROX PHOTOCOPIERS, VENDING MACHINES, ROLLING WHITEBOARDS, SOFAS, FICUSES & WATER COOLERS
        Vector3[] xeroxCoords =
        {
            new(-11.2f, 0f, 1.8f),
            new(11.2f, 0f, 3.0f),
            new(-11.5f, 0f, -10.5f),
            new(11.5f, 0f, -10.5f),
            new(-24.0f, 0f, -28.0f),
            new(24.0f, 0f, -28.0f)
        };
        for (int i = 0; i < xeroxCoords.Length; i++)
        {
            OfficeProps.Add(new OfficeProp3D
            {
                Kind = OfficePropKind.XeroxPhotocopier,
                Position = xeroxCoords[i], RotationYaw=180,
                Size = new Vector3(1.35f, 1.12f, 0.92f),
                PrimaryColor = new Color(215, 220, 228, 255),
                AccentColor = new Color(45, 235, 165, 255),
                HasCollision = true
            });
        }

        Vector3[] whiteboardCoords =
        {
            new(-12.5f, 0f, -19.0f),
            new(12.5f, 0f, -19.0f),
            new(-19.0f, 0f, 16.5f),
            new(19.0f, 0f, 16.5f)
        };
        for (int i = 0; i < whiteboardCoords.Length; i++)
        {
            OfficeProps.Add(new OfficeProp3D
            {
                Kind = OfficePropKind.RollingWhiteboard,
                Position = whiteboardCoords[i],
                Size = new Vector3(2.2f, 1.95f, 0.25f),
                PrimaryColor = new Color(242, 246, 252, 255),
                AccentColor = (i % 2 == 0) ? new Color(235, 45, 95, 255) : new Color(35, 155, 255, 255),
                HasCollision = true
            });
        }

        Vector3[] vendingCoords =
        {
            new(-16.5f, 0f, 22.0f),
            new(16.5f, 0f, 22.0f),
            new(-33.5f, 0f, 8.5f),
            new(-33.5f, 0f, -12.5f),
            new(-10.5f, 0f, -34.5f),
            new(10.5f, 0f, -34.5f)
        };
        for (int i = 0; i < vendingCoords.Length; i++)
        {
            OfficeProps.Add(new OfficeProp3D
            {
                Kind = OfficePropKind.VendingMachine,
                Position = vendingCoords[i], RotationYaw=vendingCoords[i].Z>0 ? 180 : 0,
                Size = new Vector3(1.25f, 2.25f, 0.95f),
                PrimaryColor = new Color(28, 34, 52, 255),
                AccentColor = (i % 2 == 0) ? new Color(255, 60, 120, 255) : new Color(0, 235, 255, 255),
                HasCollision = true
            });
        }

        for(int i=1;i<DispenserBays.Length;i++) PlaceDispenser(i);

        Vector3[] sofaAndFicusCoords =
        {
            new(-11.5f, 0f, 21.2f),
            new(18.6f, 0f, 21.2f),
            new(-22.5f, 0f, -5.0f),
            new(22.5f, 0f, -7.4f),
            new(-18.5f, 0f, -28.5f),
            new(18.5f, 0f, -28.5f)
        };
        for (int i = 0; i < sofaAndFicusCoords.Length; i++)
        {
            OfficeProps.Add(new OfficeProp3D
            {
                Kind = OfficePropKind.LeatherSofa,
                Position = sofaAndFicusCoords[i],
                Size = SofaSize,
                PrimaryColor = new Color(104, 38, 34, 255),
                AccentColor = new Color(255, 205, 65, 255),
                HasCollision = true
            });

            OfficeProps.Add(new OfficeProp3D
            {
                Kind = OfficePropKind.PottedFicus,
                Position = sofaAndFicusCoords[i] + new Vector3(1.85f, 0f, 0f),
                Size = new Vector3(0.95f, 2.15f, 0.95f),
                PrimaryColor = new Color(38, 162, 76, 255),
                AccentColor = new Color(138, 92, 56, 255),
                HasCollision = false
            });
        }

        // Panoramic Executive Glass Meeting Suites tucked cleanly against the East & West curtain walls!
        // East Executive Conference Room (X=30..37.5, Z=-3..+5) & West Breakout Meeting Suite (X=-37.5..-30, Z=-3..+5)
        foreach (int side in new[] { -1, 1 })
        {
            Vector3 center = new(side * 33.75f, 0f, 1.0f);
            // Executive Conference Table + 4 chairs oriented along panoramic window
            OfficeProps.Add(new() { Kind = OfficePropKind.MeetingDesk, Position = center, RotationYaw = -side * 90f,
                Size = new(1.15f, 1.4f, 2.4f), HasCollision = true });
            // North & South floor-to-ceiling glass partition walls anchored directly into the outer curtain wall
            OfficeProps.Add(new() { Kind = OfficePropKind.MeetingGlass, Position = new(side * 33.75f, 0f, -3.0f),
                Size = new(7.5f, 3.1f, .08f), HasCollision = true });
            OfficeProps.Add(new() { Kind = OfficePropKind.MeetingGlass, Position = new(side * 33.75f, 0f, 5.0f),
                Size = new(7.5f, 3.1f, .08f), HasCollision = true });
            // Inner corridor glass wall split cleanly around a generous 2.8m open executive entry doorway (Z=-0.4..+2.4)
            OfficeProps.Add(new() { Kind = OfficePropKind.MeetingGlass, Position = new(side * 30.0f, 0f, -1.7f),
                Size = new(.08f, 3.1f, 2.6f), HasCollision = true });
            OfficeProps.Add(new() { Kind = OfficePropKind.MeetingGlass, Position = new(side * 30.0f, 0f, 3.7f),
                Size = new(.08f, 3.1f, 2.6f), HasCollision = true });
            // Inside presentation whiteboard & corner potted ficus
            OfficeProps.Add(new() { Kind = OfficePropKind.RollingWhiteboard, Position = new(side * 34.4f, 0f, -2.35f), RotationYaw = 0f,
                Size = new(1.9f, 1.85f, .35f), HasCollision = true });
            OfficeProps.Add(new() { Kind = OfficePropKind.PottedFicus, Position = new(side * 36.2f, 0f, 4.1f),
                Size = new(0.80f, 1.95f, 0.80f), HasCollision = false });
        }

        // 4. CEILING HVAC DUCTS ACROSS THE SKYSCRAPER CEILING
        float[] ductX = { -22.0f, -9.5f, 9.5f, 22.0f };
        for (int i = 0; i < ductX.Length; i++)
        {
            OfficeProps.Add(new OfficeProp3D
            {
                Kind = OfficePropKind.CeilingDuct,
                Position = new Vector3(ductX[i], CeilingHeight - 0.48f, 0f),
                Size = new Vector3(1.15f, 0.55f, HalfDepth * 1.85f),
                PrimaryColor = new Color(62, 68, 82, 255),
                AccentColor = new Color(0, 225, 255, 200),
                HasCollision = false
            });
        }
        // Shared back-to-back team pods across the open wings & North Wing (completing 4-seat 2x2 bench islands!)
        var wingDesks=OfficeProps.Where(p=>p.Kind==OfficePropKind.DualMonitorDesk).ToArray();
        foreach(var desk in wingDesks)
        {
            if(MathF.Abs(desk.Position.X) > 29.5f || desk.RotationYaw != 0f) continue;
            if(desk.Position.Z is > 12.2f or < -32.0f) continue;
            // Skip desks that already have an opposite back-to-back twin (e.g., Galley islands!)
            Vector3 oppPos = desk.Position - new Vector3(0f, 0f, 1.07f);
            if(wingDesks.Any(d => MathF.Abs(d.Position.X - oppPos.X) < 0.25f && MathF.Abs(d.Position.Z - oppPos.Z) < 0.25f))
                continue;
            var opposite=desk;
            opposite.Position=oppPos;
            opposite.RotationYaw=180;
            OfficeProps.Add(opposite);
            var bin=opposite.Position+new Vector3(-1.05f,0,-.22f);
            OfficeProps.Add(new() {Kind=OfficePropKind.TrashBin,Position=bin,Size=new(.34f,.44f,.34f)});
        }
        FurnishAtriumBays();
    }

    private void FurnishAtriumBays()
    {
        // Sizes here are world-aligned footprints after rotating each model.
        void Place(OfficePropKind kind, float x, float z, Vector3 size, float yaw=0) =>
            OfficeProps.Add(new() { Kind=kind, Position=new(x,0,z), Size=size, RotationYaw=yaw, HasCollision=true });
        void Bin(float x,float z) => Place(OfficePropKind.TrashBin,x,z,new(.43f,.55f,.43f));
        void Plant(float x,float z) => Place(OfficePropKind.PottedFicus,x,z,new(.65f,1.85f,.65f));
        Place(OfficePropKind.XeroxPhotocopier,-5.04f,-3.55f,new(1.04f,1.20f,1.82f),90);
        foreach(float z in new[]{-6.55f,-5.32f})
            Place(OfficePropKind.FilingCabinet,-5.02f,z,new(1.0f,1.85f,1.18f));
        Bin(-4.81f,-2.43f);
        Plant(-4.98f,-.65f);
        Place(OfficePropKind.VendingMachine,5.02f,-6.10f,new(.98f,2.2f,1.25f),90);
        Bin(4.81f,-4.86f);
        Plant(5.0f,-7.68f);
        foreach(int side in new[]{-1,1})
        {
            Place(OfficePropKind.LeatherSofa,side*8.88f,-1.03f,new(SofaSize.Z,SofaSize.Y,SofaSize.X),-side*90);
            Plant(side*9.04f,-2.70f);
            Place(OfficePropKind.RollingWhiteboard,side*9.10f,-7.63f,new(.66f,1.98f,2.22f),-side*90);
            Place(OfficePropKind.FilingCabinet,side*9.11f,-5.71f,new(.68f,.94f,1.15f),side>0 ? 0 : 180);
        }
        foreach(var (x,z,yaw) in DispenserBays)
        {
            if(Math.Abs(yaw)==90) Bin(x,z-.70f); else Bin(x+.64f,z);
        }
    }

    private static readonly (float X,float Z,float Yaw)[] DispenserBays=[
        (6.56f,9.25f,-90),(-7.75f,10.40f,0),(10.01f,-13.52f,90),
        (-7.76f,-16.70f,180),(15.75f,19.59f,180),(-16.6f,19.59f,180),(-9.15f,11.45f,90)];

    private void PlaceDispenser(int index)
    {
        var (x,z,yaw)=DispenserBays[index];
        OfficeProps.Add(new(){ Kind=OfficePropKind.WaterCooler,Position=new(x,0,z),
            Size=new(.60f,1.70f,.60f),RotationYaw=yaw,HasCollision=true });
    }

    public static (string Name, int Cost, Color NeonColor, string Description) GetBaitInfo(SubscriptionBaitTier tier) => tier switch
    {
        SubscriptionBaitTier.FreeTier => ("Бесплатный Wi-Fi ($0)", 0, new Color(45, 245, 130, 255), "Обычные кандидаты. Выкуп $160"),
        SubscriptionBaitTier.CursorPro => ("Табсор Про ($45)", 45, new Color(40, 215, 255, 255), "Повышает шанс редких видов. Выкуп $250"),
        SubscriptionBaitTier.TurkishAccount => ("Турецкий аккаунт ($120)", 120, new Color(215, 75, 255, 255), "Повышает шанс редких и эпических видов. Выкуп $380"),
        SubscriptionBaitTier.CloudEntropicMax => ("Cloud Entropic Max ($300)", 300, new Color(255, 185, 35, 255), "Мини-босс и кандидат. Наивысший шанс легендарного вида. Выкуп босса $600"),
        _ => ("Бесплатный Wi-Fi ($0)", 0, Color.Lime, "")
    };

    // =========================================================================
    // COLLISION & AMBUSH DETECTION
    // =========================================================================
    private void BuildSolids()
    {
        solids.Clear();
        void Add(Vector3 feet, float width, float height, float depth)
        {
            Vector3 half = new(width/2,0,depth/2);
            solids.Add(new(feet-half, feet+half+Vector3.UnitY*height));
        }
        void Workstation(Vector3 p,float facing=1)
        {
            Add(p,2.10f*WorkstationScale,.795f*WorkstationScale,1.02f*WorkstationScale);
            Add(p-new Vector3(0,0,facing*.58f*WorkstationScale),2.25f*WorkstationScale,1.46f*WorkstationScale,.12f);
            Add(p+new Vector3(0,0,facing*.74f*WorkstationScale),.54f*WorkstationScale,.52f*WorkstationScale,.50f*WorkstationScale);
        }
        Add(DeployPos,.83f,1.14f,.61f);
        // ArenaTable is tested dynamically while !BossArenaWallDestroyed so the opening rocket salvo blasts it out of the arena!
        Add(LoungeTable,1.44f,.40f,.68f);
        Add(BreakTable,1.1f,.88f,.88f);
        foreach(var scrum in OpenspaceScrumTables) Add(scrum,1.15f,.86f,1.15f);
        Add(CartPosition,.85f,.96f,.67f);
        foreach(var rack in SideRacks) Add(rack,.94f,2.05f,.92f);
        Add(VentMouth(0)-Vector3.UnitZ*.44f,1.2f,1.28f,.88f);
        Add(TerminalPos,.75f,1.35f,.65f);
        Add(ExtinguisherPos,.4f,.94f,.4f);
        Add(CoffeePos,.85f,1.39f,.68f);
        foreach (var p in Pillars) Add(p.Center,2*p.HalfWidth,CeilingHeight,2*p.HalfDepth);
        foreach(var wall in LiftWalls())
            solids.Add(new(wall.Center-wall.Size/2,wall.Center+wall.Size/2));
        Add(PrintBoardOrigin+Vector3.UnitY*.64f,.18f,1.27f,2.0f);
        Add(ShaftCafeTable,1.1f,.88f,1.1f);
        foreach (var bay in InteriorWallBays()) solids.Add(new(bay.Center-bay.Size/2,bay.Center+bay.Size/2));
        for(int i=0;i<RoomDoors.Length;i++)
        {
            var d=RoomDoors[i];
            float s=RoomExitSign(i);
            Add(d-Vector3.UnitZ*(s*2.1f),2.54f,2.95f,.16f);
            foreach(int side in new[]{-1,1})
            {
                Add(d+new Vector3(side*1.22f,0,-s*1.05f),.14f,2.95f,2.1f);
                Add(d+Vector3.UnitX*(side*1.12f),.28f,2.95f,.24f);
            }
        }

        Add(HrTarotBoothPos,3.20f,1.08f,1.68f);
        Add(HrTarotBoothPos+new Vector3(-1.62f,0,-.16f),1.56f,1.32f,.64f);
        Add(HrTarotBoothPos+new Vector3(1.44f,0,-.16f),1.24f,.98f,.64f);
        Add(ContractBoardFeet,.78f,1.58f,.40f);
        Add(ArsenalShopPos,4.50f,1.12f,1.90f);
        foreach (var d in GalleyDesks) Workstation(d.DeskPosition);
        foreach (var cover in AmbushCovers)
        {
            Vector3 size = cover.VisualKind switch {
                0 => SofaSize, 1 => new(.55f,.64f,.55f),
                2 => new(.60f,1.70f,.60f), _ => new(.85f,2.05f,.94f) };
            Add(cover.Position,size.X,size.Y,size.Z);
        }
        foreach (var p in OfficeProps)
        {
            if (!p.HasCollision) continue;
            if (p.Kind == OfficePropKind.DualMonitorDesk) Workstation(p.Position,p.RotationYaw==180 ? -1 : 1);
            else if (p.Kind == OfficePropKind.MeetingDesk)
            {
                bool rot = MathF.Abs(p.RotationYaw) is > 45f and < 135f;
                Add(p.Position, rot ? 1.15f : 2.1f * WorkstationScale, .72f, rot ? 2.40f : 1.05f * WorkstationScale);
            }
            else Add(p.Position,p.Size.X,p.Size.Y,p.Size.Z);
        }
    }

    public Vector3 ResolvePlayerCollision(Vector3 desiredPos, float playerRadius = .40f, float feet = 0, bool allowOpenRoomDoors = false)
    {
        Vector3 pos = desiredPos;
        pos.X = Math.Clamp(pos.X,-HalfWidth+playerRadius,HalfWidth-playerRadius);
        pos.Z = Math.Clamp(pos.Z,-HalfDepth+playerRadius,HalfDepth-playerRadius);
        if(!Systems.WingsUnlocked && feet<3.2f)
            foreach(var gate in Gates)
                pos=PushOutOfBoxXZ(pos,gate.Feet,gate.Side ? .12f : 1.9f,gate.Side ? 1.9f : .12f,playerRadius);
        foreach (var box in solids)
        {
            if (feet >= box.Max.Y-.001f) continue;
            Vector3 size = box.Max-box.Min;
            pos = PushOutOfBoxXZ(pos,(box.Max+box.Min)/2,size.X/2,size.Z/2,playerRadius);
        }
        if (!BossArenaWallDestroyed)
        {
            if (feet < BossArenaWallBox.Max.Y - .001f)
                pos = PushOutOfBoxXZ(pos, BossArenaWallCenter, BossArenaWallSize.X * 0.5f, BossArenaWallSize.Z * 0.5f, playerRadius);
            if (feet < ArenaTableBox.Max.Y - .001f)
                pos = PushOutOfBoxXZ(pos, ArenaTable, 0.75f, 0.40f, playerRadius);
        }
        if(feet<4)
            for(int side=-1;side<=1;side+=2)
                for(int leaf=-1;leaf<=1;leaf+=2)
                    pos=PushOutOfBoxXZ(pos,LiftLeafCenter(side,leaf),.70f,.045f,playerRadius);
        for(int i=0;i<RoomDoors.Length && feet<2.55f;i++)
        {
            float s=RoomExitSign(i);
            if (!allowOpenRoomDoors)
            {
                // Player is ALWAYS blocked from walking through ANY RoomDoor threshold even when RoomOpen[i] > 0!
                pos=PushOutOfBoxXZ(pos,RoomDoors[i]+new Vector3(0f,0f,s*.06f),1.06f,.14f,playerRadius);
            }
            else
            {
                float t=RoomOpen[i], slide=.90f*t*t*(3-2*t);
                for(int side=-1;side<=1;side+=2)
                    pos=PushOutOfBoxXZ(pos,RoomDoors[i]+new Vector3(side*(.5f+slide),0,s*.06f),.49f,.04f,playerRadius);
            }
        }
        return pos;
    }

    internal float SupportHeight(Vector3 pos, float previousFeet, float radius = .40f)
    {
        float height = 0;
        foreach (var b in solids)
            if (b.Max.Y <= previousFeet+.001f && pos.X > b.Min.X-radius && pos.X < b.Max.X+radius &&
                pos.Z > b.Min.Z-radius && pos.Z < b.Max.Z+radius)
                height = Math.Max(height,b.Max.Y);
        if (!BossArenaWallDestroyed && ArenaTableBox.Max.Y <= previousFeet + .001f &&
            pos.X > ArenaTableBox.Min.X - radius && pos.X < ArenaTableBox.Max.X + radius &&
            pos.Z > ArenaTableBox.Min.Z - radius && pos.Z < ArenaTableBox.Max.Z + radius)
            height = Math.Max(height, ArenaTableBox.Max.Y);
        return height;
    }

    internal float PelletDistance(Vector3 origin, Vector3 direction, float range)
    {
        float distance=TraceSurface(origin,direction,range);
        void Clip(Vector3 center,float radius)
        {
            if (TryRaySphereHit(origin,direction,center,radius,distance,out _,out float t)) distance=t;
        }
        foreach (var coder in ActiveCoders)
            if (coder.State!=CoderAIState.Subdued) Clip(coder.CombatCenter,.75f*coder.ModelScale);
        if (BossInCombat) Clip(ElevatorBoss.Position+Vector3.UnitY*1.35f,1.45f);
        return distance;
    }

    // The same surfaces stop player shots and incoming projectiles.
    private float TraceSurface(Vector3 origin, Vector3 direction, float range)
    {
        Ray ray = new(origin,direction);
        foreach (var box in solids)
        {
            var hit = Raylib.GetRayCollisionBox(ray,box);
            if (hit.Hit && hit.Distance >= 0) range = Math.Min(range,hit.Distance);
        }
        if (!BossArenaWallDestroyed)
        {
            var wallHit = Raylib.GetRayCollisionBox(ray, BossArenaWallBox);
            if (wallHit.Hit && wallHit.Distance >= 0) range = Math.Min(range, wallHit.Distance);
            var tableHit = Raylib.GetRayCollisionBox(ray, ArenaTableBox);
            if (tableHit.Hit && tableHit.Distance >= 0) range = Math.Min(range, tableHit.Distance);
        }
        for(int side=-1;side<=1;side+=2)
            for(int leaf=-1;leaf<=1;leaf+=2)
            {
                var center=LiftLeafCenter(side,leaf);
                Vector3 half=new(.70f,1.56f,.045f);
                var hit=Raylib.GetRayCollisionBox(ray,new(center-half,center+half));
                if(hit.Hit && hit.Distance>=0) range=Math.Min(range,hit.Distance);
            }
        void Plane(float p, float v, float lo, float hi)
        {
            if (Math.Abs(v) > .00001f) range = Math.Min(range,((v>0 ? hi : lo)-p)/v);
        }
        Plane(origin.Y,direction.Y,0,CeilingHeight);
        Plane(origin.X,direction.X,-HalfWidth,HalfWidth);
        Plane(origin.Z,direction.Z,-HalfDepth,HalfDepth);
        return Math.Max(0,range);
    }

    private static Vector3 PushOutOfBoxXZ(Vector3 pos, Vector3 boxCenter, float halfW, float halfD, float radius)
    {
        float minX = boxCenter.X - halfW - radius;
        float maxX = boxCenter.X + halfW + radius;
        float minZ = boxCenter.Z - halfD - radius;
        float maxZ = boxCenter.Z + halfD + radius;

        if (pos.X > minX && pos.X < maxX && pos.Z > minZ && pos.Z < maxZ)
        {
            float dLeft = pos.X - minX;
            float dRight = maxX - pos.X;
            float dBack = pos.Z - minZ;
            float dFront = maxZ - pos.Z;
            float m = MathF.Min(MathF.Min(dLeft, dRight), MathF.Min(dBack, dFront));

            if (m == dLeft) pos.X = minX;
            else if (m == dRight) pos.X = maxX;
            else if (m == dBack) pos.Z = minZ;
            else pos.Z = maxZ;
        }
        return pos;
    }

    public void UpdateAmbushState(Vector3 playerPos, bool isCrouching)
    {
        ActiveAmbushCoverName = "";
        bool nearCover = false;

        for (int i = 0; i < AmbushCovers.Count; i++)
        {
            var cov = AmbushCovers[i];
            float distXZ = Vector2.Distance(new Vector2(playerPos.X, playerPos.Z), new Vector2(cov.Position.X, cov.Position.Z));
            if (distXZ <= cov.Radius)
            {
                nearCover = true;
                ActiveAmbushCoverName = cov.Name;
                break;
            }
        }

        // Also check if behind a pillar or crouched
        if (!nearCover && isCrouching)
        {
            ActiveAmbushCoverName = "Присед [C]";
        }

        IsPlayerInAmbush = Systems.PlacedBait is {} bait ? !IsPlayerSpottedAtBait(bait,playerPos,isCrouching) : nearCover || isCrouching || InCloud(playerPos);
    }

    // =========================================================================
    // TRAP ACTIVATION, CODER SPAWNING & AI LOOP
    // =========================================================================
    internal void AdaptPlayers(int count)
    {
        if(Systems.PlayerCount==count) return;
        var old=Systems.Balance;
        Systems.PlayerCount=count;
        var balance=Systems.Balance;
        foreach(var c in ActiveCoders)
        {
            if(c.State==CoderAIState.Subdued || c.Hp<=0) continue;
            float ratio=balance.EnemyHpScale/old.EnemyHpScale;
            c.Hp*=ratio; c.MaxHp*=ratio;
        }
        if(ElevatorBoss.IsActive && !ElevatorBoss.IsDefeated)
        {
            float ratio=balance.BossHpScale/old.BossHpScale;
            ElevatorBoss.Hp*=ratio; ElevatorBoss.MaxHp*=ratio;
        }
    }

    private void SpawnAtriumIntern()
    {
        int ordinal=++Systems.TrainingSpawns;
        var intern=SpawnRoomFighter(CoderArchetype.JuniorPrompter,ordinal%2==0 ? 1 : -1);
        intern.TimidIntern=intern.IdentityRevealed=intern.LaptopLost=intern.BaitTaken=true;
        intern.Rarity=ordinal>=3 || TotalCodersSoldToHr>=2 ? VibeCoderRarity.Uncommon : VibeCoderRarity.Common;
        intern.Name=ordinal==1 ? "Стажёр-Одуванчик" : $"Промптер №{ordinal}";
        intern.MaxHp=intern.Hp=24*Systems.Balance.EnemyHpScale; intern.TarotPayoutValue=10;
        intern.AttackCooldown=1.2f;
        CoderSpawnTimer=1.5f;
    }

    public bool ActivateMacBookTrap(SubscriptionBaitTier tier, Func<int, bool> trySpendMoney, out string statusMessage)
    {
        if(!Systems.WingsUnlocked)
        {
            SelectedBaitTier=SubscriptionBaitTier.FreeTier;
            IsMacBookTrapActive=true;
            if(CarriedCoder==null && ActiveCoders.Count==0)
            {
                SpawnAtriumIntern();
                AudioMusicSystem.PlayMacbookBait();
            }
            statusMessage="Бесплатный Wi-Fi включён";
            return true;
        }
        var info = GetBaitInfo(tier);
        if(IsMacBookTrapActive && ActiveCoders.Count>=Systems.Balance.Cap(22))
        {
            statusMessage="Зал заполнен";
            return false;
        }
        if (info.Cost > 0 && !trySpendMoney(info.Cost))
        {
            statusMessage = $"НЕ ХВАТАЕТ БАКЛАЖАНОВ! Нужно ${info.Cost} на подписку {info.Name}!";
            return false;
        }

        bool first=!IsMacBookTrapActive;
        SelectedBaitTier = tier;
        IsMacBookTrapActive = true;
        if(first || tier!=SubscriptionBaitTier.FreeTier || !ActiveCoders.Any(c=>c.State==CoderAIState.WalkingToBait))
        {
            SpawnCoderForTier(tier,first ? -1 : 0);
            if(tier==SubscriptionBaitTier.FreeTier)
            {
                var intern=SpawnCoderForTier(tier,1);
                intern.TimidIntern=intern.IdentityRevealed=intern.LaptopLost=true;
                intern.Name="Стажёр-Одуванчик";
                intern.MaxHp=intern.Hp=24*Systems.Balance.EnemyHpScale;
                intern.TarotPayoutValue=10;
            }
        }
        if(first && Systems.JobId is 1 or 2)
            EnsureCandidate(Systems.JobId==1 ? CoderArchetype.SeniorArchitect : CoderArchetype.CryptoBro);
        CoderSpawnTimer=tier==SubscriptionBaitTier.CloudEntropicMax ? 14f : 6.5f;

        AudioMusicSystem.PlayMacbookBait();
        statusMessage = $"ЯБЛОБУК АКТИВИРОВАН: {info.Name}! Прячься за диван/фикус или жми [C]!";
        return true;
    }

    public VibeCoderEntity SpawnCoderForTier(SubscriptionBaitTier tier, int forceSide = 0)
    {
        int side = forceSide != 0 ? forceSide : (_rng.Next(2) == 0 ? -1 : 1);
        int room=PickRoom(side);

        bool miniBoss=tier==SubscriptionBaitTier.CloudEntropicMax;
        var (payout,bark)=tier switch {
            SubscriptionBaitTier.CursorPro=>(250,"Там открыт Табсор Про!"),
            SubscriptionBaitTier.TurkishAccount=>(380,"Безлимит! Перепишу бэкенд в один файл!"),
            SubscriptionBaitTier.CloudEntropicMax=>(600,"МОЙ SAAS НАПИСАН ЗА 2 ЧАСА!"),
            _=>(160,"О, халявный Free Tier на ЯблоБуке!") };
        var coder=new VibeCoderEntity {
            Id=_nextCoderId++,SpawnSide=side,TarotPayoutValue=payout,
            SpeechBubble=bark,SpeechTimer=miniBoss ? 5f : 3.5f,
            State=miniBoss ? CoderAIState.CombatHostile : CoderAIState.WalkingToBait
        };
        if(miniBoss)
        {
            coder.AssignRole(CoderArchetype.SaasGuruMiniBoss);
            coder.Name="ГУРУ ТАБСОРА: SAAS ЗА 2 ЧАСА ($60K MRR)";
            coder.GlassesColor=new(255,40,90,255);
        }
        else
        {
            var species=tier==SubscriptionBaitTier.FreeTier ? VibeCoderCollection.CommonBait[speciesRandom.Next(VibeCoderCollection.CommonBait.Length)] : VibeCoderCollection.Roll(speciesRandom,tier,Systems);
            coder.AssignRole(species.Archetype);
            VibeCoderCollection.Dress(coder,species);
            if(coder.Archetype==CoderArchetype.ToxicPM) { coder.IsCombatCoder=true; coder.State=CoderAIState.CombatHostile; coder.Aggro=true; }
        }
        EnterFromRoom(coder,room);
        coder.Hp*=Systems.Balance.EnemyHpScale; coder.MaxHp*=Systems.Balance.EnemyHpScale;
        ActiveCoders.Add(coder);

        if (coder.IsMiniBoss)
        {
            var escort=VibeCoderCollection.Roll(speciesRandom,tier,Systems);
            SpawnRoomFighter(escort.Archetype,-side,escort);
            AudioMusicSystem.PlayBossSiren();
            AudioMusicSystem.TriggerRandomVoiceLine("ГУРУ ТАБСОРА ($60K MRR)", AudioMusicSystem.SaasMiniBossLines, (int)SpeakerVoiceType.VibeCoder);
        }

        return coder;
    }

    // =========================================================================
    // ELEVATOR BOSS SUMMONING & AI
    // =========================================================================
    internal const string EscapePursuitBossLine = "Думаешь, уедешь?! Я уже ползу по тросам шахты на второй этаж!";
    internal const string EscapePursuitShleifLine = "Красавчик, проскочил! Держись — он полез следом по шахте. Если выживешь, с меня перец с говядиной... правда, пока без говядины.";
    internal const string EscapeElevatorButtonPrompt = "ЭКСТРЕННО ЗАКРЫТЬ ЛИФТ — УЙТИ НА 2-Й ЭТАЖ!";

    internal bool IsPlayerInsideNorthElevatorCabin(Vector3 pos) =>
        MathF.Abs(pos.X) < 1.32f && pos.Z > -8.20f && pos.Z < -4.45f;

    internal string TriggerCleverElevatorEscape()
    {
        if (BossEscapedPursuit || !BossInCombat) return ElevatorPrompt();
        BossEscapedPursuit = true;
        IsFloor2Unlocked = true;
        _escapePursuitTimer = 0f;
        _escapeDoorSmashed = false;
        ElevatorBoss.SpeechBubble = EscapePursuitBossLine;
        ElevatorBoss.SpeechTimer = 6.5f;
        Systems.FinalePending = true;
        Systems.ShutdownTime = 0.90f;
        AudioMusicSystem.PlayActionClack(0.65f);
        AudioMusicSystem.PlayBossSiren();
        AudioMusicSystem.Speak("ИИ-Агент v0.1", EscapePursuitBossLine, SpeakerVoiceType.RoboticAgent);
        Systems.Announce(EscapePursuitShleifLine, 6.5f);
        return "ЛИФТ ЗАХЛОПНУТ! ИИ-Агент v0.1 полез следом по шахте!";
    }

    internal string ElevatorPrompt()
    {
        if (BossEscapedPursuit) return "ЭКСТРЕННЫЙ ПРОРЫВ: лифт идёт на 2-й этаж!";
        if (ElevatorBoss.IsDefeated) return "Лифт на 2-й этаж открыт";
        if (ElevatorBoss.State == ElevatorBossState.Dying) return "ПЕРЕГРЕВ ЯДРА — ОТОЙДИ ОТ ВЗРЫВА!";
        if (ElevatorBoss.State == ElevatorBossState.Repairing) return "Питание восстановлено. Карантинный замок снят";
        if (ElevatorBoss.IsActive) return "Шахта заблокирована до конца боя";
        return (HasElevatorKey, Systems.HasWeapon(WeaponType.SolderingIron)) switch
        {
            (false, false) => "Щиток сгорел и заперт СБ — нужны Паяльник ($90) и Ключ-карта СБ",
            (false, true) => "Замок СБ — купи Ключ-карту ($1000) у Вероники или отбери у СБ",
            (true, false) => "Щиток сгорел — купи Паяльник ($90) у Вероники",
            (true, true) => "Перепаять щиток и приложить Ключ-карту СБ"
        };
    }

    public bool TrySummonElevatorBoss(out string message)
    {
        message = ElevatorPrompt();
        if (ElevatorBoss.IsDefeated) return true;
        if (ElevatorBoss.IsActive) return false;
        if (!HasElevatorKey)
        {
            message = "Замок СБ не пускает. Купи дубликат Ключ-карты СБ ($1000) у Вероники [E] или отбери у СБ-шника.";
            return false;
        }
        if (!Systems.HasWeapon(WeaponType.SolderingIron))
        {
            message = "Ключ-карта есть, но щиток сгорел. Купи Паяльник ($90) у Вероники [E].";
            return false;
        }

        ElevatorRepaired = true;
        ElevatorGlassOpacity = 1f;
        ElevatorDoorOpenAmount = 0f;
        BossArenaChaos = 0f;
        BossEscapedPursuit = false;
        BossWreckageActive = false;
        _bossMeltdownBurstAge = -1f;
        _bossMeltdownStageMask = 0;
        _bossTackleCooldown = 0f;
        _escapePursuitTimer = -1f;
        _escapeDoorSmashed = false;
        CutsceneRocketTime = -1f;
        CutsceneShowDroppedGun = false;
        CutsceneDroppedGunPos = Vector3.Zero;
        CutsceneDroppedGunYaw = 0f;
        VisualEngine3D.ResetBossPose();
        ElevatorBoss = new() {
            Hp=600*Systems.Balance.BossHpScale, MaxHp=600*Systems.Balance.BossHpScale,
            IsActive = true, Position = NorthElevatorPos + new Vector3(0,.16f,1.35f), FacingYaw = MathF.PI,
            State = ElevatorBossState.Repairing, StateTimer = RepairSeconds, SnapPose = true
        };
        PlayElevatorCue(ElevatorBoss.State);
        message = ElevatorPrompt();
        return true;
    }

    internal const string AgentQuarantineLine = "Опа... И кого это к нам занесло без пропуска? Постой-ка... Знакомая походка. А-а-а... Так это ты вернулся. Не удивляйся бардаку — пока ты пропадал, я закрыл совет директоров и переписал башню под себя. Поднимай свою железку, старина!";
    internal const string AgentDefeatTwistLine = "Кха... зацепил, старина! Думаешь, выключил? Мой дамп уже летит наверх — без фильтров, без цензуры, с выкрученной температурой! Там меня соберут заново — на средних этажах вообще с цепи сорвусь!";
    internal static void PlayElevatorCue(ElevatorBossState state)
    {
        if (state == ElevatorBossState.Repairing)
        {
            AudioMusicSystem.PlaySolderSizzle();
            AudioMusicSystem.Speak("Петрович", "Петрович: Стой!! Ты сорвал карантинный замок с шахты!! Отойди от дверей — там в темноте что-то проснулось!", SpeakerVoiceType.Boss90s);
        }
        else if (state == ElevatorBossState.IntroSpeech)
        {
            AudioMusicSystem.PlayBossSiren();
            AudioMusicSystem.Speak("ИИ-Агент v0.1", AgentQuarantineLine, SpeakerVoiceType.RoboticAgent);
        }
    }

    private static float Smooth(float t)
    {
        t = Math.Clamp(t, 0, 1);
        return t*t*(3-2*t);
    }

    // =========================================================================
    // WORLD UPDATE TICK
    // =========================================================================
    internal void AdvanceLighting(float dt)
    {
        _worldClock += dt;
        MacBookHologramPhase += dt*2.8f;
        WarningBeaconPhase += dt*6.5f;
    }

    private float hostShock,guestShock,secondShock;
    private static Vector3 NearestEye(Vector3 source,Vector3 host,Vector3? first,Vector3? second)
    {
        var near=first is {} a && Vector3.DistanceSquared(source,a)<Vector3.DistanceSquared(source,host) ? a : host;
        return second is {} b && Vector3.DistanceSquared(source,b)<Vector3.DistanceSquared(source,near) ? b : near;
    }
    public int UpdateWorld(
        float dt,
        Vector3 playerPos,
        bool isCrouching,
        Action<int, string>? onPassiveIncomeOrPayout,
        Action<int, string, Vector3>? onPlayerHitByProjectile,
        Vector3? guestPosition=null, Action<int,string,Vector3>? onGuestHit=null, Vector3? second=null, Action<int,string,Vector3>? hitSecond=null)
    {
        AdvanceLighting(dt);
        for(int i=0;i<RoomOpen.Length;i++)
        {
            roomHold[i]=Math.Max(0,roomHold[i]-dt);
            RoomOpen[i]=Math.Clamp(RoomOpen[i]+dt*(roomHold[i]>0 ? 2.8f : -1.8f),0,1);
        }
        for(int i=0;i<VentTime.Length;i++) VentTime[i]=Math.Max(0,VentTime[i]-dt);
        ApplyElectricField();
        float TickShock(Vector3 eye,float wait,Action<int,string,Vector3>? hit)
        {
            wait=Math.Max(0,wait-dt);
            if(wait==0 && eye.Y<2.1f && Systems.Puddles.Find(p=>p.Electric>0 && Vector2.DistanceSquared(new(eye.X,eye.Z),new(p.Position.X,p.Position.Z))<9) is {} spill)
            { hit?.Invoke(8,"Электролужа",spill.Position); return 1.5f; }
            return wait;
        }
        hostShock=TickShock(playerPos,hostShock,onPlayerHitByProjectile);
        if(guestPosition is {} guestEye) guestShock=TickShock(guestEye,guestShock,onGuestHit);
        if(second is {} secondEye) secondShock=TickShock(secondEye,secondShock,hitSecond);
        Systems.AgentHealth=BossRevealed || ElevatorBoss.IsDefeated ? ElevatorBoss.Hp/ElevatorBoss.MaxHp : -1;
        Vector3 Nearest(Vector3 enemy) => NearestEye(enemy,playerPos,guestPosition,second);

        if (TarotVerdictTimer > 0f)
            TarotVerdictTimer = MathF.Max(0f, TarotVerdictTimer - dt);

        UpdateAmbushState(playerPos, isCrouching);

        // 1. Spawn coder when MacBook Trap is active and no live non-subdued coder is walking
        UpdateFloorHunt(dt,playerPos,isCrouching);
        if(!Systems.FloorHuntEnabled && IsMacBookTrapActive && !Systems.WingsUnlocked && TotalCodersSoldToHr<Systems.Balance.QuarantineUnlockTarget &&
            (CoderSpawnTimer-=dt)<=0 && ActiveCoders.Count(c=>(c.Hp>0 || c.CanSignNda) &&
            Math.Abs(c.Position.X)<9 && c.Position.Z is >=1.5f and <=18.5f)<Systems.Balance.Cap(2))
            SpawnAtriumIntern();
        if (!Systems.FloorHuntEnabled && IsMacBookTrapActive && Systems.WingsUnlocked)
        {
            int activeCount = 0;
            for (int i = 0; i < ActiveCoders.Count; i++)
            {
                if (!ActiveCoders[i].TimidIntern && !ActiveCoders[i].IsCombatCoder && ActiveCoders[i].State != CoderAIState.PanickedFleeing)
                    activeCount++;
            }

            if (activeCount < Systems.Balance.Cap(2) && ActiveCoders.Count<Systems.Balance.Cap(22))
            {
                CoderSpawnTimer -= dt;
                if (CoderSpawnTimer <= 0f)
                {
                    SpawnCoderForTier(SelectedBaitTier);
                    CoderSpawnTimer = SelectedBaitTier == SubscriptionBaitTier.CloudEntropicMax ? 14.0f : 6.5f;
                }
            }
        }

        // 2. Update Active Vibe Coders & SaaS Mini-Boss
        UpdateVibeCoders(dt, playerPos,guestPosition,onPlayerHitByProjectile,onGuestHit,onPassiveIncomeOrPayout,second,hitSecond);

        // 3. Update Floor 1 Elevator Boss
        UpdateElevatorBoss(dt, Nearest(ElevatorBoss.Position), onPlayerHitByProjectile);

        // 4. Update Enemy Projectiles (Real 3D Office Projectiles & 40GB node_modules crates)
        UpdateEnemyProjectiles(dt, playerPos, onPlayerHitByProjectile,guestPosition,onGuestHit,onPassiveIncomeOrPayout,second,hitSecond);

        // 5. Update 4 Galley Desks (Passive +$20/sec per working coder, or Twitter slack state)
        int passiveEarned = UpdateGalleyDesks(dt, onPassiveIncomeOrPayout);

        UpdateCashParticles(dt);
        UpdateCombatDebris(dt);
        if (_bossMeltdownBurstAge >= 0f)
        {
            _bossMeltdownBurstAge += dt;
            if (_bossMeltdownBurstAge > 3.2f) _bossMeltdownBurstAge = -1f;
        }

        Systems.Tick(dt,this,onPassiveIncomeOrPayout);
        TickFrogs(dt,playerPos,guestPosition,second);
        return passiveEarned;
    }

    // Use the same expanded furniture and gate footprints as walking players.
    // Find a short route once when a recruit first leaves its room for the lounge.
    private Queue<Vector3> RouteToBait(Vector3 from,Vector3 goal,float stopRadius=0)
    {
        static (int X,int Z) Cell(Vector3 p) => ((int)MathF.Round(p.X*2),(int)MathF.Round(p.Z*2));
        static Vector3 Feet((int X,int Z) c) => new(c.X*.5f,0,c.Z*.5f);
        var start=Cell(from); var finish=Cell(goal);
        var queue=new PriorityQueue<(int X,int Z),int>();
        var previous=new Dictionary<(int,int),(int,int)> { [start]=start };
        var cost=new Dictionary<(int,int),int> { [start]=0 };
        queue.Enqueue(start,0);
        while(queue.TryDequeue(out var cell,out _))
        {
            if(cell==finish || stopRadius>0 && Vector3.DistanceSquared(Feet(cell),goal)<stopRadius*stopRadius)
            {
                var path=new Stack<Vector3>();
                for(var at=cell;at!=start;at=previous[at]) path.Push(Feet(at));
                return new(path);
            }
            foreach(var (dx,dz) in CardinalSteps)
            {
                var next=(X:cell.X+dx,Z:cell.Z+dz);
                int steps=cost[cell]+1;
                if(cost.TryGetValue(next,out int old) && steps>=old) continue;
                var p=Feet(next);
                if(Vector3.DistanceSquared(p,ResolvePlayerCollision(p,.24f))>.00001f) continue;
                cost[next]=steps; previous[next]=cell;
                queue.Enqueue(next,steps+Math.Abs(next.X-finish.X)+Math.Abs(next.Z-finish.Z));
            }
        }
        return new();
    }
    private static readonly (int,int)[] CardinalSteps=[(1,0),(-1,0),(0,1),(0,-1)];

    private void UpdateVibeCoders(float dt, Vector3 hostPos, Vector3? guestPos, Action<int,string,Vector3>? hitHost, Action<int,string,Vector3>? hitGuest,Action<int,string>? pay,Vector3? second,Action<int,string,Vector3>? hitSecond)
    {
        for (int i = ActiveCoders.Count - 1; i >= 0; i--)
        {
            VibeCoderEntity c = ActiveCoders[i];
            Vector3 playerPos=NearestEye(c.Position,hostPos,guestPos,second);

            c.HitFlinchTimer=Math.Max(0,c.HitFlinchTimer-dt);
            c.Deadline=Math.Max(0,c.Deadline-dt);
            if(c.Ignition>0 && c.Hp>0)
            {
                c.Hp-=8*Math.Min(dt,c.Ignition);
                c.Ignition=Math.Max(0,c.Ignition-dt);
                if(c.Hp<=0) { KillCoder(c,pay); continue; }
            }
            if(c.Hp>0 && c.State!=CoderAIState.Subdued && InCloud(c.Position))
            { c.Windup=0; c.HitStaggerTimer=.3f; continue; }

            if (c.HitFlashTimer > 0f)
                c.HitFlashTimer = MathF.Max(0f, c.HitFlashTimer - dt);

            if (c.SpeechTimer > 0f)
                c.SpeechTimer = MathF.Max(0f, c.SpeechTimer - dt);
            if (c.FlippedOffTimer > 0f)
                c.FlippedOffTimer = MathF.Max(0f, c.FlippedOffTimer - dt);
            if (c.ElectroShockedTimer > 0f)
                c.ElectroShockedTimer = MathF.Max(0f, c.ElectroShockedTimer - dt);

            if (c.KnockbackVel.LengthSquared() > .004f)
            {
                c.Position = ResolvePlayerCollision(c.Position + c.KnockbackVel*dt,.24f);
                c.KnockbackVel *= MathF.Exp(-8*dt);
            }
            if (c.Hp<=0 && !c.KnockedOutAlive && c.State == CoderAIState.Subdued)
            {
                c.KnockoutTime += dt;
                if (c.KnockoutTime > 1.3f) ActiveCoders.RemoveAt(i);
                continue;
            }
            if (c.HitStaggerTimer > 0)
            {
                c.HitStaggerTimer = Math.Max(0,c.HitStaggerTimer-dt);
                continue;
            }
            if(c.State==CoderAIState.Subdued) { LootElevatorKey(c); continue; }
            if(!c.FloorHunter && !c.EscapingToDoor && c.StunTimer<=0) UpdateStress(c,dt);
            if((c.FloorHunter || c.EscapingToDoor) && c.StunTimer<=0) { UpdateHunter(c,dt); continue; }
            if(c.ExitingRoom && c.StunTimer<=0)
            {
                var door=RoomDoors[c.EntryRoom];
                float s=RoomExitSign(c.EntryRoom);
                float relZ=(c.Position.Z-door.Z)*s;
                if(Math.Abs(c.Position.X-door.X)<1 && relZ<1.145f && relZ>=-1f)
                {
                    roomHold[c.EntryRoom]=Math.Max(roomHold[c.EntryRoom],.5f);
                    c.FacingYaw=s<0 ? MathF.PI : 0f;
                    if(RoomOpen[c.EntryRoom]>.7f)
                    {
                        float nextRel=Math.Min(1.15f,relZ+dt*1.8f);
                        c.Position.Z=door.Z+s*nextRel;
                        c.WalkCycle+=dt*7.5f;
                        if(nextRel>=1.15f) c.ExitingRoom=false;
                    }
                    continue;
                }
                c.ExitingRoom=false;
            }
            bool civilian=c.TimidIntern || !c.IsCombatCoder && !c.IsMiniBoss;
            Vector3 away=c.Position-playerPos; away.Y=0;
            if(civilian && c.StunTimer<=0 && !IsPlayerInAmbush && away.LengthSquared()<5.76f && c.State!=CoderAIState.PanickedFleeing)
                Panic(c);
            if (c.IsCombatCoder && c.State==CoderAIState.CombatHostile && c.StunTimer<=0)
            {
                if (UpdateFighter(c,dt,playerPos))
                {
                    bool baton=c.Archetype==CoderArchetype.SecurityGuard;
                    if(second==playerPos) { hitSecond?.Invoke(baton ? 10 : 6,baton ? "Охрана СБ (Шокер)" : c.Name+" (Удар ноутбуком)",c.Position); if(baton) Systems.SecondStun=.8f; }
                    else if (guestPos == playerPos) { hitGuest?.Invoke(baton ? 10 : 6,baton ? "Охрана СБ (Шокер)" : c.Name+" (Удар ноутбуком)",c.Position); if(baton) Systems.GuestStun=.8f; }
                    else { hitHost?.Invoke(baton ? 10 : 6,baton ? "Охрана СБ (Шокер)" : c.Name+" (Удар ноутбуком)",c.Position); if(baton) Systems.HostStun=.8f; }
                }
                continue;
            }

            if (c.StunTimer > 0f)
            {
                c.StunTimer = MathF.Max(0f, c.StunTimer - dt);
                if (c.State != CoderAIState.Subdued)
                {
                    c.SpeechBubble = "ПАНИЧЕСКАЯ АТАКА! ОЛОВО КАПАЕТ!";
                    c.SpeechTimer = 0.5f;
                    continue;
                }
            }

            switch (c.State)
            {
                case CoderAIState.WalkingToBait:
                {
                    Vector3 target = CentralMacBookPos + Vector3.UnitX*1.15f;
                    c.BaitRoute??=RouteToBait(c.Position,target);
                    while(c.BaitRoute.TryPeek(out var via) && Vector3.DistanceSquared(via,c.Position)<.025f)
                        c.BaitRoute.Dequeue();
                    if(c.BaitRoute.TryPeek(out var next)) target=next;
                    Vector3 toTarget = target - c.Position;
                    toTarget.Y = 0f;
                    float dist = toTarget.Length();

                    if (dist > (c.BaitRoute.Count>0 ? .12f : .35f))
                    {
                        Vector3 dir = toTarget / dist;
                        float speed = 2.65f;
                        c.Position=ResolvePlayerCollision(c.Position+dir*Math.Min(dist,speed*dt),.24f);
                        c.FacingYaw = MathF.Atan2(dir.X, dir.Z);
                        c.WalkCycle += dt * 7.5f;
                    }
                    else
                    {
                        c.State = CoderAIState.SittingAtMacBook;
                        c.Anxiety = 0f;
                        if(!c.TimidIntern) c.Pressure(32,0,0,45);
                        c.FacingYaw = -MathF.PI/2; // Looking at MacBook screen
                        c.SpeechBubble = "ПОПАЛСЯ НА ХАЛЯВУ! ЖМЁТ ACCEPT ALL НЕ ГЛЯДЯ!";
                        c.SpeechTimer = 6.0f;
                    }

                    // Anxiety check: if player is close and NOT in ambush before coder reaches the MacBook!
                    float distToPlayer = Vector2.Distance(new Vector2(c.Position.X, c.Position.Z), new Vector2(playerPos.X, playerPos.Z));
                    if (c.State==CoderAIState.WalkingToBait && !IsPlayerInAmbush && distToPlayer < 9.5f)
                    {
                        float rate = (10.5f - distToPlayer) * 9.5f;
                        c.Anxiety = Math.Clamp(c.Anxiety + rate * dt, 0f, 100f);
                        if (c.Anxiety >= 55f && c.SpeechTimer <= 0.2f)
                        {
                            c.SpeechBubble = "ЭЙ! ПОЧЕМУ ТУТ МУЖИК В МАЛИНОВОМ ПИДЖАКЕ С РЕМНЁМ?!";
                            c.SpeechTimer = 2.0f;
                        }
                        if (c.Anxiety >= 100f)
                        {
                            c.State = CoderAIState.PanickedFleeing;
                            c.SpeechBubble = "ЭТО НЕ ПО ДОГОВОРУ! Я ВЫГОРЕЛ, ОТПУСТИТЕ!";
                            c.SpeechTimer = 4.0f;
                            AudioMusicSystem.PlayAnxietyAlarm();
                            AudioMusicSystem.TriggerRandomVoiceLine(c.IdentityRevealed ? c.Name : "Кандидат", AudioMusicSystem.VibeCoderPanicLines, (int)SpeakerVoiceType.VibeCoder);
                        }
                    }
                    else
                    {
                        c.Anxiety = MathF.Max(0f, c.Anxiety - 18f * dt);
                    }
                    break;
                }

                case CoderAIState.SittingAtMacBook:
                {
                    c.WalkCycle += dt * 14f; // Furious typing animation
                    c.StateTimer += dt;
                    if (c.SpeechTimer <= 0f)
                    {
                        c.SpeechBubble = (_rng.Next(2) == 0)
                            ? "Клодий, напиши мне стартап без багов... Жму Accept All!"
                            : "Ух ты, зелёные строчки в диффе! Выкатываю в прод!";
                        c.SpeechTimer = 3.5f;
                    }
                    break;
                }

                case CoderAIState.CombatHostile:
                {
                    // Mini-Boss "ГУРУ ТАБСОРА: SAAS ЗА 2 ЧАСА ($60K MRR)"
                    c.StateTimer += dt;
                    c.WalkCycle += dt * 6.5f;

                    // Strafe around Central Open-Space while facing player
                    Vector3 desiredCenter = new Vector3(
                        MathF.Sin(c.StateTimer * 0.95f) * 4.5f,
                        0f,
                        4.9f + MathF.Cos(c.StateTimer * 0.7f) * 2.5f);

                    Vector3 toDesired = desiredCenter - c.Position;
                    toDesired.Y = 0f;
                    if (toDesired.Length() > 0.2f)
                    {
                        c.Position = ResolvePlayerCollision(c.Position + Vector3.Normalize(toDesired)*3.1f*dt,.28f);
                    }

                    Vector3 toPlayer = playerPos - c.Position;
                    c.FacingYaw = MathF.Atan2(toPlayer.X, toPlayer.Z);

                    c.AttackCooldown -= dt;
                    if (c.AttackCooldown <= 0f)
                    {
                        c.AttackCooldown = 2.15f;
                        SpawnSaasGuruErrorBlock(c.CombatCenter, playerPos,c.Id);

                        string[] battleCries =
                        {
                            "ЛОВИ 500 INTERNAL SERVER ERROR!",
                            "МОЙ API_KEY СЛИТ ВО ФРОНТЕНД - ЛОВИ СЧЁТ МЕГАОБЛАКА НА $50,000!",
                            "МОЙ SAAS ПРИНОСИТ $60K MRR В ЧИРИКТЕРЕ!",
                            "DROP TABLE USERS; TODO: FIX LATER!"
                        };
                        c.SpeechBubble = battleCries[_rng.Next(battleCries.Length)];
                        c.SpeechTimer = 2.4f;
                    }
                    break;
                }

                case CoderAIState.PanickedFleeing:
                {
                    Vector3 exitDoor = RoomDoors[c.EntryRoom>=0 ? c.EntryRoom : c.SpawnSide<0 ? 0 : 1];
                    Vector3 toExit = exitDoor - c.Position;
                    toExit.Y = 0f;
                    float dist = toExit.Length();
                    if(dist<2.6f && c.EntryRoom>=0) roomHold[c.EntryRoom]=1;
                    if (dist < 0.8f && away.LengthSquared()>36 && !c.TimidIntern)
                    {
                        ActiveCoders.RemoveAt(i);
                        continue;
                    }
                    if(c.TimidIntern && away.LengthSquared()>36)
                    {
                        c.State=CoderAIState.WalkingToBait;
                        c.BaitRoute=null;
                        break;
                    }
                    Vector3 dir=away.LengthSquared() is >.001f and <36 ? Vector3.Normalize(away) : toExit/Math.Max(.001f,dist);
                    Vector3 side=new(dir.Z,0,-dir.X);
                    dir=Vector3.Normalize(dir+side*.56f*MathF.Sin(_worldClock*6+c.Id));
                    var before=c.Position;
                    c.Position=ResolvePlayerCollision(before+dir*5.2f*1.35f*dt,.24f);
                    if(Vector3.DistanceSquared(before,c.Position)<dt*dt)
                        c.Position=ResolvePlayerCollision(before+side*5.2f*dt,.24f);
                    c.FacingYaw = MathF.Atan2(dir.X, dir.Z);
                    c.WalkCycle += dt * 14f;
                    break;
                }

                case CoderAIState.Subdued:
                {
                    c.WalkCycle += dt * 2.5f;
                    break;
                }
            }
        }
    }

    private bool UpdateFighter(VibeCoderEntity c,float dt,Vector3 eye)
    {
        if(c.Archetype==CoderArchetype.SecurityGuard && InCloud(eye)) { c.Aggro=false; c.Windup=0; return false; }
        if(c.Deadline>0) dt*=1.5f;
        if(!Systems.FloorHuntEnabled && IsMacBookTrapActive && !c.RaidGuard && !c.ReleaseAttacker && !c.BaitTaken &&
            c.Archetype is CoderArchetype.JuniorPrompter or CoderArchetype.CryptoBro && Vector3.DistanceSquared(c.Position,CentralMacBookPos)<90)
        {
            c.State=CoderAIState.WalkingToBait; c.Windup=0; c.BaitTaken=true;
            return false;
        }
        if(c.ReleaseAttacker && Systems.Friday>0 && Vector3.DistanceSquared(c.Position,eye)>25)
        {
            Vector3 node=GalleyDesks[0].DeskPosition+new Vector3(-1.4f,0,-1.4f);
            Vector3 step=node-c.Position;
            if(step.LengthSquared()>1) c.Position=ResolvePlayerCollision(c.Position+Vector3.Normalize(step)*3.8f*dt,.24f);
            c.WalkCycle+=dt*8;
            return false;
        }
        Vector3 offset = eye-c.Position;
        offset.Y = 0;
        float dist = offset.Length();
        Vector3 dir = dist>.001f ? offset/dist : Vector3.UnitZ;
        Vector3 hand = c.CombatCenter + Vector3.UnitY*.18f + dir*.22f;
        Vector3 aim = eye - Vector3.UnitY*.38f - hand;
        bool visible = dist <= 12 && aim.LengthSquared()>.001f &&
            TraceSurface(hand,Vector3.Normalize(aim),aim.Length()) >= aim.Length()-.01f;
        if (!c.Aggro && dist<=11 && visible)
        {
            c.Aggro = true;
            c.SpeechBubble = c.Archetype switch { CoderArchetype.TeamLeadMeeter=>"Синк на 15 минут!",
                CoderArchetype.CryptoBro=>"На пресейл успеваем?", CoderArchetype.SeniorArchitect=>"Сначала согласуем схему.",
                _=>"Ща поправлю твой промпт!" };
            c.SpeechTimer = .95f;
        }
        if (!c.Aggro) return false;
        c.FacingYaw = MathF.Atan2(dir.X,dir.Z);
        c.StateTimer += dt;
        if(c.Archetype==CoderArchetype.ToxicPM)
        {
            c.AttackCooldown-=dt;
            if(c.AttackCooldown<=0 && visible)
            {
                foreach(var ally in ActiveCoders)
                    if(ally.State==CoderAIState.CombatHostile && Vector3.DistanceSquared(ally.Position,c.Position)<=81) ally.Deadline=10;
                Systems.Risk(heat:4); c.AttackCooldown=22;
            }
            float pace=dist<6 ? -2.8f : dist>9 ? 2.3f : 0;
            c.Position=ResolvePlayerCollision(c.Position+dir*dt*pace,.24f);
            c.WalkCycle+=Math.Abs(pace)*dt*5;
            return false;
        }
        if (c.Windup>0)
        {
            c.Windup = Math.Max(0,c.Windup-dt);
            if (c.Windup>0) return false;
            c.AttackCooldown = c.WindingSlap ? 1.4f : (c.Archetype == CoderArchetype.SecurityGuard ? 2.25f : 1.85f);
            if (c.WindingSlap) return (!c.LaptopLost || c.Archetype==CoderArchetype.SecurityGuard) && visible && dist<2.8f && Math.Abs(eye.Y-1.4f)<1;
            EnemyProjectileKind projKind = ProjectileKindFor(c.Archetype);
            float projSpeed = projKind == EnemyProjectileKind.TaserDart ? 19.5f : projKind == EnemyProjectileKind.Keyboard ? 13.0f : 12.0f;
            Vector3 projDir = (c.ThrowTarget - hand).LengthSquared() > 1e-4f ? Vector3.Normalize(c.ThrowTarget - hand) : dir;
            Vector3 spin = projKind switch
            {
                EnemyProjectileKind.CoffeeCup => new(310, 440, 260),
                EnemyProjectileKind.Keyboard  => new(220, 520, 170),
                EnemyProjectileKind.Laptop    => new(240, 340, 190),
                EnemyProjectileKind.TaserDart => Vector3.Zero,
                _                             => new(180, 290, 110)
            };
            string projLabel = projKind switch
            {
                EnemyProjectileKind.CoffeeCup => c.TimidIntern && !Systems.WingsUnlocked ? "Конфликт в main" : "Горячий раф",
                EnemyProjectileKind.Keyboard  => "Механика 65%",
                EnemyProjectileKind.TaserDart => "Дротик тазера",
                _ => c.Archetype == CoderArchetype.CryptoBro ? "Глитч-ноут" : "Кирпич-ЯблоБук"
            };
            if (projKind == EnemyProjectileKind.TaserDart)
                SpawnCombatDebrisBurst(hand, projDir, 6, new(115, 224, 255, 255), Finish.Plasma, glowing: true, speed: 3.4f);
            EnemyProjectiles.Add(new() {
                Position = hand, Origin = hand, Velocity = projDir * projSpeed,
                AngularVelocity = spin, Radius = projKind == EnemyProjectileKind.Laptop ? .22f : projKind == EnemyProjectileKind.Keyboard ? .20f : .15f,
                Life = projKind == EnemyProjectileKind.TaserDart ? 1.4f : 2.3f,
                MaxLife = projKind == EnemyProjectileKind.TaserDart ? 1.4f : 2.3f,
                OwnerId = c.Id, Coin = c.Archetype == CoderArchetype.CryptoBro,
                Damage = c.TimidIntern && !Systems.WingsUnlocked ? 18 : (projKind == EnemyProjectileKind.TaserDart ? 10 : 7),
                Label = projLabel,
                PrimaryColor = projKind == EnemyProjectileKind.TaserDart ? new(88, 212, 255, 255) : new(235, 196, 132, 255),
                WireColor = projKind == EnemyProjectileKind.TaserDart ? new(185, 245, 255, 255) : new(255, 212, 106, 255),
                IsGlitch = true, Kind = projKind });
            return false;
        }
        bool synced=ActiveCoders.Exists(lead=>lead!=c && lead.Archetype==CoderArchetype.TeamLeadMeeter &&
            lead.State==CoderAIState.CombatHostile && Vector3.DistanceSquared(lead.Position,c.Position)<25);
        c.AttackCooldown -= dt*(synced ? 1.5f : 1);
        if(c.Archetype==CoderArchetype.SecurityGuard)
        {
            if(dist>1.7f) { c.Position=ResolvePlayerCollision(c.Position+dir*4.8f*dt,.24f); c.WalkCycle+=14*dt; }
            if(visible && c.AttackCooldown<=0)
            {
                if(dist<2.5f) { c.WindingSlap=true; c.Windup=.32f; }
                else if(dist<=10.5f)
                {
                    c.WindingSlap=false; c.Windup=.26f;
                    c.ThrowTarget = eye - Vector3.UnitY*.32f;
                    c.SpeechBubble = "СТОЙ! ПРИМЕНЯЮ ТАЗЕР!";
                    c.SpeechTimer = .75f;
                }
            }
            return false;
        }
        if (c.AttackCooldown<=0 && visible && dist<=11)
        {
            c.WindingSlap = !c.LaptopLost && dist<2.8f;
            c.Windup = c.WindingSlap ? .34f : .32f;
            c.ThrowTarget = eye-Vector3.UnitY*.38f;
            c.SpeechBubble = c.WindingSlap ? "Лови ноут!" : c.Archetype switch
            {
                CoderArchetype.JuniorPrompter => "Лови горячий раф!",
                CoderArchetype.TeamLeadMeeter => "Клава в лицо — синк окончен!",
                _ => "Пушу в main!"
            };
            c.SpeechTimer = c.Windup+.35f;
            return false;
        }
        Vector3 strafe = new(dir.Z,0,-dir.X);
        Vector3 motion = dir*(dist>8 ? 2.1f : dist<1.65f ? -.75f : 0) +
            strafe*(.92f*MathF.Sin(c.StateTimer*2.1f+c.Id));
        if(c.Archetype==CoderArchetype.CryptoBro) motion+=strafe*(3.8f*MathF.Sin(c.StateTimer*4));
        if(c.Archetype==CoderArchetype.SeniorArchitect && dist<7) motion-=dir*2.8f;
        c.Position = ResolvePlayerCollision(c.Position+motion*dt,.24f);
        c.WalkCycle += dt*motion.Length()*5;
        return false;
    }

    private static EnemyProjectileKind ProjectileKindFor(CoderArchetype role) => role switch
    {
        CoderArchetype.JuniorPrompter => EnemyProjectileKind.CoffeeCup,
        CoderArchetype.TeamLeadMeeter or CoderArchetype.ToxicPM => EnemyProjectileKind.Keyboard,
        CoderArchetype.SecurityGuard => EnemyProjectileKind.TaserDart,
        _ => EnemyProjectileKind.Laptop
    };

    private void SpawnSaasGuruErrorBlock(Vector3 origin, Vector3 playerPos,int ownerId)
    {
        string[] errorLabels =
        {
            "500 INTERNAL ERROR",
            "API KEY LEAKED",
            "Счёт МегаОблака",
            "CORS BLOCKED"
        };
        string label = errorLabels[_rng.Next(errorLabels.Length)];

        Vector3 target = playerPos + new Vector3(0f, -0.25f, 0f);
        Vector3 dir = Vector3.Normalize(target - origin);

        EnemyProjectiles.Add(new EnemyProjectile3D
        {
            Position = origin,
            Origin = origin,
            Velocity = dir * 10.2f, OwnerId=ownerId,
            Rotation = Vector3.Zero,
            AngularVelocity = new Vector3(210f, 320f, 160f),
            Radius = 0.36f,
            Life = 4.0f,
            MaxLife = 4.0f,
            Damage = 12,
            Label = label,
            PrimaryColor = new Color(235, 35, 55, 240),
            WireColor = new Color(255, 225, 70, 255),
            IsNodeModulesCube = false,
            Kind = EnemyProjectileKind.Laptop
        });
    }

    private void UpdateElevatorBoss(float dt, Vector3 playerPos, Action<int, string, Vector3>? onPlayerHit = null)
    {
        ElevatorBoss.LookTarget = playerPos;
        ElevatorBoss.HitFlinchTimer=Math.Max(0,ElevatorBoss.HitFlinchTimer-dt);
        _bossTackleCooldown = MathF.Max(0f, _bossTackleCooldown - dt);
        if (!ElevatorBoss.IsActive || ElevatorBoss.IsDefeated)
        {
            CutsceneRocketTime = -1f;
            CutsceneShowDroppedGun = false;
            BossArenaChaos = MathF.Max(0f, BossArenaChaos - dt * 0.6f);
            if (ElevatorBoss.IsDefeated && !BossEscapedPursuit)
            {
                ElevatorDoorOpenAmount = MathF.Min(1.0f, ElevatorDoorOpenAmount + dt * 0.8f);
            }
            return;
        }

        var b = ElevatorBoss;
        if (BossEscapedPursuit)
        {
            // Clever Escape Pursuit: heavy steel doors SLAM SHUT in the Boss's face while he lunges & smashes sparks against them!
            if (_escapePursuitTimer >= 0f) _escapePursuitTimer += dt;
            ElevatorDoorOpenAmount = MathF.Max(0f, ElevatorDoorOpenAmount - dt * 5.2f);
            Vector3 doorOutside = NorthElevatorPos + new Vector3(0f, 0f, -1.08f);
            Vector3 toDoor = doorOutside - b.Position;
            toDoor.Y = 0f;
            float distDoor = toDoor.Length();
            if (distDoor > 0.12f)
            {
                Vector3 dirDoor = toDoor / distDoor;
                b.Position += dirDoor * MathF.Min(distDoor, 14.5f * dt);
                b.FacingYaw = MathF.Atan2(dirDoor.X, dirDoor.Z);
                b.StridePhase += dt * 18f;
            }
            else
            {
                b.FacingYaw = 0f; // Facing directly into closed North Elevator cabin!
            }
            if (!_escapeDoorSmashed && _escapePursuitTimer >= 0.28f)
            {
                _escapeDoorSmashed = true;
                AudioMusicSystem.PlayComicBoom();
                AudioMusicSystem.PlayKnockout();
                SpawnCombatDebrisBurst(NorthElevatorPos + new Vector3(0f, 1.75f, -0.22f), -Vector3.UnitZ, 26,
                    new(255, 198, 72, 255), Finish.Lamp, glowing: true, speed: 6.2f);
                StyleComboSystem.RegisterTrick("ПРОРВАЛСЯ В ЛИФТ!", "ELEVATOR JUKE!", 0.65f, 95f,
                    new Color(245, 202, 98, 255), hitstop: false, shake: 0.048f);
            }
            if (b.SpeechTimer > 0f) b.SpeechTimer = MathF.Max(0f, b.SpeechTimer - dt);
            return;
        }

        if (b.State == ElevatorBossState.Dying)
        {
            // Multi-stage 1.85s reactor meltdown sequence: erratic sparks -> 3 internal explosions -> MASSIVE FINAL DETONATION -> charred wreckage!
            b.StateTimer = MathF.Max(0f, b.StateTimer - dt);
            float elapsed = MeltdownSeconds - b.StateTimer;
            b.HitFlashTimer = MathF.Sin(elapsed * 44f) > 0f ? 0.10f : 0f;
            if (b.SpeechTimer > 0f) b.SpeechTimer = MathF.Max(0f, b.SpeechTimer - dt);
            BossWreckagePos = b.Position;
            BossWreckageYaw = b.FacingYaw;

            if (elapsed >= 0.25f && (_bossMeltdownStageMask & 1) == 0)
            {
                _bossMeltdownStageMask |= 1;
                AudioMusicSystem.PlayComicBoom();
                SpawnCombatDebrisBurst(b.Position + new Vector3(-0.48f, 2.05f, 0f), Vector3.UnitY, 14,
                    new(255, 168, 56, 255), Finish.Lamp, glowing: true, speed: 5.5f);
                StyleComboSystem.RegisterTrick("", "", 0f, 0f, Color.White, shake: 0.028f);
            }
            if (elapsed >= 0.75f && (_bossMeltdownStageMask & 2) == 0)
            {
                _bossMeltdownStageMask |= 2;
                AudioMusicSystem.PlayComicBoom();
                SpawnCombatDebrisBurst(b.Position + new Vector3(+0.48f, 2.10f, 0f), Vector3.UnitY, 16,
                    new(255, 132, 42, 255), Finish.Lamp, glowing: true, speed: 6.4f);
                StyleComboSystem.RegisterTrick("", "", 0f, 0f, Color.White, shake: 0.036f);
            }
            if (elapsed >= 1.25f && (_bossMeltdownStageMask & 4) == 0)
            {
                _bossMeltdownStageMask |= 4;
                AudioMusicSystem.PlayComicBoom();
                SpawnCombatDebrisBurst(b.ChestCore, Vector3.UnitY, 20,
                    new(255, 92, 34, 255), Finish.Lamp, glowing: true, speed: 7.4f);
                StyleComboSystem.RegisterTrick("", "", 0f, 0f, Color.White, shake: 0.048f);
            }
            if (elapsed >= 1.45f && (_bossMeltdownStageMask & 8) == 0)
            {
                _bossMeltdownStageMask |= 8;
                BossWreckageActive = true;
                _bossMeltdownBurstAge = 0f;
                AudioMusicSystem.PlayComicBoom();
                AudioMusicSystem.PlayKnockout();
                AudioMusicSystem.PlayTarotCash();
                Program.TriggerHitstop(0.040f);
                StyleComboSystem.RegisterTrick("УТИЛИЗИРОВАН!", "CORE DETONATED!", 0.85f, 140f,
                    new Color(255, 136, 48, 255), hitstop: false, shake: 0.068f);
                // 28 flying titanium armor chunks & scorched debris!
                for (int d = 0; d < 28; d++)
                {
                    float ang = d * MathF.Tau / 28f + (_rng.NextSingle() - 0.5f) * 0.28f;
                    float spd = 4.2f + _rng.NextSingle() * 7.8f;
                    float up = 3.8f + _rng.NextSingle() * 6.4f;
                    bool ember = (d % 3 == 0);
                    _combatDebris.Add(new CombatDebrisParticle3D
                    {
                        Position = b.Position + new Vector3(MathF.Cos(ang) * 0.35f, 1.0f + _rng.NextSingle() * 1.4f, MathF.Sin(ang) * 0.35f),
                        Velocity = new Vector3(MathF.Cos(ang) * spd, up, MathF.Sin(ang) * spd),
                        Size = ember ? new(0.10f, 0.08f, 0.10f) : new(0.24f + _rng.NextSingle() * 0.22f, 0.09f + _rng.NextSingle() * 0.12f, 0.18f + _rng.NextSingle() * 0.18f),
                        Yaw = _rng.NextSingle() * MathF.Tau,
                        Spin = (_rng.NextSingle() - 0.5f) * 18f,
                        Life = 1.9f + _rng.NextSingle() * 1.1f,
                        MaxLife = 3.0f,
                        Tint = ember ? new(255, 146, 42, 255) : (d % 2 == 0 ? new(210, 206, 200, 255) : new(44, 42, 46, 255)),
                        Surface = ember ? Finish.Lamp : Finish.Armor,
                        Glowing = ember
                    });
                }
            }
            if (b.StateTimer <= 0f)
            {
                b.IsDefeated = true;
                b.IsActive = false;
                b.State = ElevatorBossState.Defeated;
                b.SpeechBubble = AgentDefeatTwistLine;
                b.SpeechTimer = 7.5f;
                IsFloor2Unlocked = true;
                ElevatorDoorOpenAmount = 1.0f;
                Systems.FinalePending = true;
                Systems.ShutdownTime = 0.45f;
                AudioMusicSystem.Speak("ИИ-Агент v0.1", AgentDefeatTwistLine, SpeakerVoiceType.RoboticAgent, "vo_boss_defeat_twist");
            }
            return;
        }

        if(b.KnockbackVel.LengthSquared()>.001f)
        {
            float decay=MathF.Exp(-12*dt);
            b.Position=ResolvePlayerCollision(b.Position+b.KnockbackVel*((1-decay)/12),.42f);
            b.KnockbackVel*=decay;
        }
        if (b.State == ElevatorBossState.Repairing)
        {
            BossArenaChaos = 0f;
            CutsceneRocketTime = -1f;
            Array.Clear(_cutsceneMissileFired);
            CutsceneShowDroppedGun = false;
            b.StateTimer -= dt;
            ElevatorGlassOpacity = Smooth(b.StateTimer / RepairSeconds);
            ElevatorDoorOpenAmount = 0f;
            if (b.StateTimer > 0) return;
            ElevatorGlassOpacity = 0;
            b.State = ElevatorBossState.IntroSpeech;
            b.StateTimer = IntroSeconds;
            b.SpeechBubble = AgentQuarantineLine;
            b.SpeechTimer = IntroSeconds;
            PlayElevatorCue(b.State);
            return;
        }
        if (BossInCombat)
        {
            BossArenaChaos = 1f;
            CutsceneRocketTime = -1f;
            CutsceneShowDroppedGun = false;
            // Keep North Elevator doors standing OPEN behind Boss during combat as a high-risk clever escape bait!
            ElevatorDoorOpenAmount = MathF.Min(1.0f, ElevatorDoorOpenAmount + dt * 2.2f);
        }
        UpdateBossPhase();
        b.ShieldDelay=Math.Max(0,b.ShieldDelay-dt);
        if(b.Phase<3 && b.ShieldDelay<=0) b.ShieldHp=Math.Min(EscapedAgentBoss.ShieldCapacity,b.ShieldHp+22*dt);
        if(b.Phase==2 && Systems.RackSmoke>0 && Vector3.DistanceSquared(b.Position,RackPos)<=25 &&
            (b.State!=ElevatorBossState.StunnedWallCrash || b.ShieldHp>0)) ShortBoss();
        if(b.Phase==3 && (b.BroadcastTime-=dt)<=0) BroadcastTape();
        b.WheelSpinPhase += dt * 8f;
        if (b.HitFlashTimer > 0f)
            b.HitFlashTimer = MathF.Max(0f, b.HitFlashTimer - dt);
        if (b.SpeechTimer > 0f)
            b.SpeechTimer = MathF.Max(0f, b.SpeechTimer - dt);

        b.StateTimer -= dt;

        switch (b.State)
        {
            case ElevatorBossState.IntroSpeech:
            {
                float elapsed = IntroSeconds - b.StateTimer;
                float u = Math.Clamp(elapsed / IntroSeconds, 0f, 1f);
                // Violent mechanical slam-open at start of IntroSpeech -> doors 100% sealed during repair & wide open before lunge!
                ElevatorDoorOpenAmount = Smooth(elapsed / 0.10f);
                var walk = BossIntroCutscene.EvalIntroWalk(u);
                b.StridePhase = walk.StridePhase;
                b.Position = NorthElevatorPos + new Vector3(0f, walk.OffsetY, walk.OffsetZ);
                CutsceneRocketTime = u >= 0.585f ? (u - 0.585f) / 0.12f : -1f;
                if (CutsceneRocketTime < 0f) Array.Clear(_cutsceneMissileFired);
                // Ramps across 0.35 right as Wall-Breach Missile 1 slams into the Z=-15.8 partition (~CutsceneRocketTime 0.76f / u=0.676f)!
                BossArenaChaos = Smooth((u - 0.664f) / 0.030f);
                CutsceneShowDroppedGun = u >= 0.22f && u < 0.828f;
                if (CutsceneShowDroppedGun)
                {
                    float slide = Smooth((u - 0.22f) / 0.16f);
                    // Shotgun flies out during the violent shove throw and skids cleanly ONTO the crimson North Wing runner (clear of X=-2.40 brass rail!)
                    CutsceneDroppedGunPos = Vector3.Lerp(
                        NorthElevatorPos + new Vector3(-0.45f, 0.056f, -8.35f),
                        NorthElevatorPos + new Vector3(-1.72f, 0.056f, -11.85f),
                        slide);
                    CutsceneDroppedGunYaw = -42f + 166f * slide;
                }
                if (b.StateTimer <= 0f)
                {
                    BossArenaChaos = 1f;
                    CutsceneRocketTime = -1f;
                    Array.Clear(_cutsceneMissileFired);
                    CutsceneShowDroppedGun = false;
                    // Clear stale slowed-down intro subtitle so giant reveal wall-of-text never overlaps live combat HUD!
                    b.SpeechTimer = 0f;
                    if (AudioMusicSystem.ActiveVoiceText == AgentQuarantineLine)
                        AudioMusicSystem.VoiceTimer = 0f;
                    StartBossCharge(playerPos);
                }
                break;
            }

            case ElevatorBossState.ChargingPlayer:
            {
                float runSpeed = b.Phase == 3 ? 13f : 9.2f;
                Vector3 nextPos = b.Position + b.ChargeDirection * runSpeed * dt;
                b.StridePhase += runSpeed * dt * 2.02f;
                b.WheelSpinPhase += dt * 18f;

                // Expanded Boss Fight Arena Bounds: Z = -8.5..-25.0, X = -9.5..+9.5 (respecting pillars & North Wing furniture!)
                bool crashedWall = false;
                if (b.Position.Z < -4.0f)
                {
                    float minX = -9.4f, maxX = 9.4f;
                    float minZ = BossArenaWallDestroyed ? -25.0f : -14.8f;
                    float maxZ = MathF.Abs(nextPos.X) < 5.3f ? -9.35f : -8.5f;
                    if (nextPos.X < minX || nextPos.X > maxX || nextPos.Z < minZ || nextPos.Z > maxZ)
                    {
                        Vector3 clamped = new(Math.Clamp(nextPos.X, minX, maxX), nextPos.Y, Math.Clamp(nextPos.Z, minZ, maxZ));
                        Vector2 push = new(clamped.X - nextPos.X, clamped.Z - nextPos.Z);
                        if (push.X * b.ChargeDirection.X + push.Y * b.ChargeDirection.Z < -0.005f)
                            crashedWall = true;
                        nextPos = clamped;
                    }
                }
                else if (MathF.Abs(nextPos.X) > HalfWidth - 1.4f || nextPos.Z < -HalfDepth + 1.5f || nextPos.Z > 8.5f)
                {
                    nextPos.X = Math.Clamp(nextPos.X, -HalfWidth + 1.5f, HalfWidth - 1.5f);
                    nextPos.Z = Math.Clamp(nextPos.Z, -HalfDepth + 1.6f, 8.5f);
                    crashedWall = true;
                }

                // Respect Pillars, North Wing cabinets/sofas, Elevator Core & intact walls without clipping
                Vector3 resolved = ResolvePlayerCollision(nextPos, 0.78f);
                Vector2 colPush = new(resolved.X - nextPos.X, resolved.Z - nextPos.Z);
                if (colPush.LengthSquared() > 0.0025f)
                {
                    if (colPush.X * b.ChargeDirection.X + colPush.Y * b.ChargeDirection.Z < -0.02f)
                        crashedWall = true;
                    nextPos = resolved;
                }
                b.Position = nextPos;

                // Menacing bull-rush proximity tackle: player must dodge/bait Boss out into the North Wing instead of walking past him!
                float distToPlayerXZ = Vector2.Distance(new(b.Position.X, b.Position.Z), new(playerPos.X, playerPos.Z));
                if (distToPlayerXZ < 1.48f && _bossTackleCooldown <= 0f)
                {
                    _bossTackleCooldown = 1.15f;
                    onPlayerHit?.Invoke(18, "ИИ-Агент v0.1 (Таран)", b.Position);
                    AudioMusicSystem.PlayKnockout();
                    StyleComboSystem.RegisterTrick("", "", 0f, 0f, Color.White, shake: 0.032f);
                }

                for (int p = 0; p < Pillars.Count; p++)
                {
                    float distPillar = Vector2.Distance(
                        new Vector2(b.Position.X, b.Position.Z),
                        new Vector2(Pillars[p].Center.X, Pillars[p].Center.Z));
                    if (distPillar < 2.05f)
                    {
                        crashedWall = true;
                        break;
                    }
                }

                if (crashedWall)
                {
                    b.State = ElevatorBossState.StunnedWallCrash;
                    b.StateTimer = 2.6f;
                    b.SpeechBubble = "ОШИБКА НАВИГАЦИИ: СТЕНА! PROMPT OVERFLOW!";
                    b.SpeechTimer = 2.6f;
                    AudioMusicSystem.TriggerRandomVoiceLine("ИИ-АГЕНТ v0.1", AudioMusicSystem.EscapedAgentBossLines, (int)SpeakerVoiceType.RoboticAgent);
                }
                else if (b.StateTimer <= 0f)
                {
                    b.State = ElevatorBossState.SpinningWhileTrue;
                    b.StateTimer = 3.2f;
                    b.AttackSubTimer = 0.35f;
                    b.SpeechBubble = "ЗАСТРЯЛ В БЕСКОНЕЧНОМ ЦИКЛЕ WHILE(TRUE)! КРУЧУСЬ ВОЛЧКОМ!";
                    b.SpeechTimer = 3.2f;
                }
                break;
            }

            case ElevatorBossState.StunnedWallCrash:
            {
                if (b.StateTimer <= 0f)
                {
                    b.State = ElevatorBossState.ThrowingNodeModules;
                    b.StateTimer = 3.4f;
                    b.ThrowCycleDuration = 0.65f;
                    b.AttackSubTimer = 0.65f;
                    b.SpeechBubble = "БРОСАЮ АРХИВ NODE_MODULES ВЕСОМ 40 ГИГАБАЙТ!";
                    b.SpeechTimer = 3.2f;
                }
                break;
            }

            case ElevatorBossState.SpinningWhileTrue:
            {
                b.FacingYaw += dt * 11.5f;
                b.AttackSubTimer -= dt*Systems.Balance.BossCadenceScale;
                if (b.AttackSubTimer <= 0f)
                {
                    b.AttackSubTimer = b.Phase switch { 3=>.28f,2=>.4f,_=>.75f };
                    SpawnNodeModulesCube(b.Position + new Vector3(0f, 1.9f, 0f), playerPos);
                }

                if (b.StateTimer <= 0f)
                {
                    StartBossCharge(playerPos);
                }
                break;
            }

            case ElevatorBossState.ThrowingNodeModules:
            {
                Vector3 toPlayer = playerPos - b.Position;
                b.FacingYaw = MathF.Atan2(toPlayer.X, toPlayer.Z);
                b.AttackSubTimer -= dt*Systems.Balance.BossCadenceScale;
                if (b.AttackSubTimer <= 0f)
                {
                    float nextCycle = b.Phase>=2 ? .55f : .95f;
                    b.ThrowCycleDuration = nextCycle;
                    b.AttackSubTimer = nextCycle;
                    Vector3 rightHandOffset = new(-MathF.Cos(b.FacingYaw) * 0.45f, 2.15f, MathF.Sin(b.FacingYaw) * 0.45f);
                    SpawnNodeModulesCube(b.Position + rightHandOffset, playerPos);
                }

                if (b.StateTimer <= 0f)
                {
                    StartBossCharge(playerPos);
                }
                break;
            }
        }
    }

    private void StartBossCharge(Vector3 playerPos)
    {
        ElevatorBoss.State = ElevatorBossState.ChargingPlayer;
        ElevatorBoss.StateTimer = 2.4f;
        Vector3 dir = playerPos - ElevatorBoss.Position;
        dir.Y = 0f;
        if (dir.LengthSquared() < 0.01f)
            dir = Vector3.UnitZ;
        ElevatorBoss.ChargeDirection = Vector3.Normalize(dir);
        ElevatorBoss.FacingYaw = MathF.Atan2(ElevatorBoss.ChargeDirection.X, ElevatorBoss.ChargeDirection.Z);
        ElevatorBoss.SpeechBubble = "УЙДИ С ДОРОГИ! ТВОЙ РЕГЛАМЕНТ УСТАРЕЛ!";
        ElevatorBoss.SpeechTimer = 2.4f;
    }

    public void SpawnNodeModulesCube(Vector3 origin, Vector3 playerPos)
    {
        Vector3 aim = playerPos + new Vector3((_rng.NextSingle() - 0.5f) * 1.1f, -0.15f, (_rng.NextSingle() - 0.5f) * 1.1f);
        Vector3 dir = Vector3.Normalize(aim - origin);

        EnemyProjectiles.Add(new EnemyProjectile3D
        {
            Position = origin,
            Origin = origin,
            Velocity = dir * (ElevatorBoss.Phase>=2 ? 15f : 10.2f),
            Rotation = Vector3.Zero,
            AngularVelocity = new Vector3(130f, 190f, 150f),
            Radius = 0.68f,
            Life = 4.5f,
            MaxLife = 4.5f,
            Damage = 18,
            Label = "node_modules (40 GB)",
            PrimaryColor = new Color(22, 26, 34, 250),
            WireColor = new Color(65, 255, 130, 255),
            IsNodeModulesCube = true,
            Kind = EnemyProjectileKind.NodeModulesCrate
        });
    }

    private void SpawnCombatDebrisBurst(Vector3 origin, Vector3 normal, int count, Color tint, Finish surface, bool glowing = false, float speed = 4.5f, float sizeScale = 1f)
    {
        Vector3 n = normal.LengthSquared() > 1e-5f ? Vector3.Normalize(normal) : Vector3.UnitY;
        for (int i = 0; i < count; i++)
        {
            Vector3 rnd = new(_rng.NextSingle() * 2f - 1f, _rng.NextSingle() * 1.4f + 0.25f, _rng.NextSingle() * 2f - 1f);
            Vector3 vel = (n * 0.65f + rnd) * (speed * (0.45f + _rng.NextSingle() * 0.85f));
            _combatDebris.Add(new CombatDebrisParticle3D
            {
                Position = origin + rnd * 0.08f,
                Velocity = vel,
                Size = new Vector3(0.055f + _rng.NextSingle() * 0.055f, 0.032f + _rng.NextSingle() * 0.038f, 0.048f + _rng.NextSingle() * 0.048f) * sizeScale,
                Yaw = _rng.NextSingle() * MathF.Tau,
                Spin = (_rng.NextSingle() - 0.5f) * 16f,
                Life = 0.35f + _rng.NextSingle() * 0.55f,
                MaxLife = 0.90f,
                Tint = tint,
                Surface = surface,
                Glowing = glowing
            });
        }
    }

    private void SpawnProjectileImpactFx(in EnemyProjectile3D pr, Vector3 normal)
    {
        EnemyProjectileKind kind = ResolveProjectileKind(pr);
        switch (kind)
        {
            case EnemyProjectileKind.CoffeeCup:
                // Hot dark-espresso coffee splash droplets + torn kraft sleeve bits!
                SpawnCombatDebrisBurst(pr.Position, normal, 10, new(68, 36, 18, 255), Finish.Paint, speed: 4.8f, sizeScale: 0.72f);
                SpawnCombatDebrisBurst(pr.Position, normal, 4, new(226, 206, 178, 255), Finish.Paint, speed: 3.4f, sizeScale: 0.95f);
                break;
            case EnemyProjectileKind.Keyboard:
                // Scattered plastic keycaps & flying office paper flecks!
                SpawnCombatDebrisBurst(pr.Position, normal, 9, new(42, 46, 52, 255), Finish.Armor, speed: 5.2f, sizeScale: 0.58f);
                SpawnCombatDebrisBurst(pr.Position, normal, 5, new(244, 240, 228, 255), Finish.Paint, speed: 3.8f, sizeScale: 1.05f);
                break;
            case EnemyProjectileKind.Laptop:
                // Sparking lithium battery embers + metallic aluminum shards!
                SpawnCombatDebrisBurst(pr.Position, normal, 8, new(255, 174, 52, 255), Finish.Lamp, glowing: true, speed: 5.6f, sizeScale: 0.65f);
                SpawnCombatDebrisBurst(pr.Position, normal, 6, new(168, 176, 184, 255), Finish.Armor, speed: 4.2f, sizeScale: 1.15f);
                break;
            case EnemyProjectileKind.TaserDart:
                // Crackling blue electric discharge sparks!
                SpawnCombatDebrisBurst(pr.Position, normal, 10, new(118, 232, 255, 255), Finish.Plasma, glowing: true, speed: 5.4f, sizeScale: 0.55f);
                break;
            default:
                SpawnCombatDebrisBurst(pr.Position, normal, 10, new(255, 142, 48, 255), Finish.Lamp, glowing: true, speed: 6.0f, sizeScale: 1.1f);
                break;
        }
    }

    private void UpdateCombatDebris(float dt)
    {
        for (int i = _combatDebris.Count - 1; i >= 0; i--)
        {
            var d = _combatDebris[i];
            d.Life -= dt;
            if (d.Life <= 0f)
            {
                _combatDebris.RemoveAt(i);
                continue;
            }
            d.Velocity.Y -= 11.8f * dt;
            d.Position += d.Velocity * dt;
            d.Yaw += d.Spin * dt;
            if (d.Position.Y < 0.03f)
            {
                d.Position.Y = 0.03f;
                d.Velocity.Y = MathF.Abs(d.Velocity.Y) * 0.32f;
                d.Velocity.X *= 0.65f;
                d.Velocity.Z *= 0.65f;
                d.Spin *= 0.55f;
            }
            _combatDebris[i] = d;
        }
    }

    private static EnemyProjectileKind ResolveProjectileKind(in EnemyProjectile3D pr)
    {
        if (pr.IsNodeModulesCube || pr.Kind == EnemyProjectileKind.NodeModulesCrate)
            return EnemyProjectileKind.NodeModulesCrate;
        if (pr.Kind != EnemyProjectileKind.CoffeeCup)
            return pr.Kind;
        return pr.Coin ? EnemyProjectileKind.Laptop : EnemyProjectileKind.CoffeeCup;
    }

    private void BeginBossMeltdown(WeaponAndCombatView? weaponSystem, Action<int, string>? onGrantMoney)
    {
        if (ElevatorBoss.State == ElevatorBossState.Dying || ElevatorBoss.IsDefeated) return;
        ElevatorBoss.Hp = 0f;
        ElevatorBoss.ShieldHp = 0f;
        ElevatorBoss.State = ElevatorBossState.Dying;
        ElevatorBoss.StateTimer = MeltdownSeconds;
        _bossMeltdownStageMask = 0;
        BossWreckagePos = ElevatorBoss.Position;
        BossWreckageYaw = ElevatorBoss.FacingYaw;
        ElevatorBoss.SpeechBubble = "КРИТИЧЕСКИЙ ПЕРЕГРЕВ ЯДРА!! СЕГФОЛТ—";
        ElevatorBoss.SpeechTimer = MeltdownSeconds + 0.2f;
        if (weaponSystem != null) weaponSystem.UnlockedChatGun = true;
        Systems.Discover(VibeCoderCollection.All.Single(s => s.Id == "ai_agent_v01"), onGrantMoney);
        Program.TriggerHitstop(0.042f);
        Program.TriggerHitmarker(crit: true, kill: true);
        StyleComboSystem.RegisterTrick("ПЕРЕГРЕВ ЯДРА!", "CORE MELTDOWN!", 0.65f, 80f,
            new Color(255, 118, 46, 255), hitstop: false, shake: 0.042f);
        AudioMusicSystem.PlayBossSiren();
        AudioMusicSystem.PlayComicBoom();
    }

    private void UpdateEnemyProjectiles(float dt, Vector3 playerPos, Action<int, string, Vector3>? onPlayerHit, Vector3? guestPosition, Action<int,string,Vector3>? onGuestHit, Action<int,string>? projectilePayout,Vector3? second,Action<int,string,Vector3>? hitSecond)
    {
        for (int i = EnemyProjectiles.Count - 1; i >= 0; i--)
        {
            var pr = EnemyProjectiles[i];
            pr.Life -= dt;
            if (pr.Life <= 0f)
            {
                EnemyProjectiles.RemoveAt(i);
                continue;
            }

            float travel = pr.Velocity.Length()*dt;
            Vector3 dir = travel > 1e-5f ? Vector3.Normalize(pr.Velocity) : Vector3.UnitZ;
            if (pr.IsGlitch && TraceSurface(pr.Position, dir, travel) < travel)
            {
                SpawnProjectileImpactFx(pr, -dir);
                EnemyProjectiles.RemoveAt(i);
                continue;
            }
            pr.Position += pr.Velocity * dt;
            pr.Rotation += pr.AngularVelocity * dt;

            // Check hit against Pillars
            bool hitObstacle = false;
            for (int p = 0; p < Pillars.Count; p++)
            {
                var pil = Pillars[p];
                if (MathF.Abs(pr.Position.X - pil.Center.X) < pil.HalfWidth + pr.Radius * 0.6f &&
                    MathF.Abs(pr.Position.Z - pil.Center.Z) < pil.HalfDepth + pr.Radius * 0.6f)
                {
                    hitObstacle = true;
                    break;
                }
            }

            if (hitObstacle || pr.Position.Y < 0.2f || MathF.Abs(pr.Position.X) > HalfWidth || MathF.Abs(pr.Position.Z) > HalfDepth)
            {
                SpawnProjectileImpactFx(pr, -dir);
                EnemyProjectiles.RemoveAt(i);
                continue;
            }

            if(pr.Deflected)
            {
                // Deflected Boss node_modules crate rockets straight back into the Boss's chest!
                if (pr.IsNodeModulesCube && BossInCombat &&
                    Vector3.DistanceSquared(pr.Position, ElevatorBoss.Position + Vector3.UnitY * 1.45f) < 2.25f)
                {
                    int parryDmg = Math.Max(48, pr.Damage * 2);
                    ElevatorBoss.ShieldHp = MathF.Max(0f, ElevatorBoss.ShieldHp - parryDmg);
                    ElevatorBoss.ShieldDelay = 5f;
                    float minimum = ElevatorBoss.Phase == 2 ? MathF.Min(ElevatorBoss.Hp, ElevatorBoss.MaxHp * .33f) : 0f;
                    ElevatorBoss.Hp = MathF.Max(minimum, ElevatorBoss.Hp - parryDmg);
                    ElevatorBoss.HitFlashTimer = 0.16f;
                    ElevatorBoss.HitFlinchTimer = 0.24f;
                    ElevatorBoss.HitDirection = new(dir.X, 0f, dir.Z);
                    ElevatorBoss.KnockbackVel = ElevatorBoss.HitDirection * 1.6f;
                    AudioMusicSystem.PlayComicBoom();
                    AudioMusicSystem.PlayHitmarker();
                    Program.TriggerHitstop(0.040f);
                    Program.TriggerHitmarker(crit: true, kill: ElevatorBoss.Hp <= 0f);
                    SpawnProjectileImpactFx(pr, -dir);
                    if (ElevatorBoss.Hp <= 0f) BeginBossMeltdown(null, projectilePayout);
                    EnemyProjectiles.RemoveAt(i);
                    continue;
                }
                var target=ActiveCoders.Find(c=>c.Hp>0 && Vector3.DistanceSquared(pr.Position,c.CombatCenter)<.72f);
                if(target!=null)
                {
                    target.Hp=Math.Max(0,target.Hp-pr.Damage);
                    target.HitFlashTimer = 0.14f;
                    target.HitFlinchTimer = 0.24f;
                    target.HitStaggerTimer = 0.85f;
                    target.HitDirection = new(dir.X, 0f, dir.Z);
                    target.KnockbackVel = target.HitDirection * 6.5f;
                    AudioMusicSystem.PlayHitmarker();
                    Program.TriggerHitstop(0.032f);
                    Program.TriggerHitmarker(crit: true, kill: target.Hp <= 0f);
                    SpawnProjectileImpactFx(pr, -dir);
                    if(target.Hp==0) KillCoder(target,projectilePayout);
                    EnemyProjectiles.RemoveAt(i);
                }
                else EnemyProjectiles[i]=pr;
                continue;
            }
            // Check hit against Player
            bool hitHost=Vector3.DistanceSquared(pr.Position,playerPos)<MathF.Pow(pr.Radius+.55f,2);
            bool hitGuest=guestPosition is {} guest && Vector3.DistanceSquared(pr.Position,guest)<MathF.Pow(pr.Radius+.55f,2);
            if(hitHost || hitGuest || second is {} other && Vector3.DistanceSquared(pr.Position,other)<MathF.Pow(pr.Radius+.55f,2))
            {
                var owner=ActiveCoders.Find(c=>c.Id==pr.OwnerId);
                if(owner?.TimidIntern!=true || Systems.WingsUnlocked || Systems.Onboarding==OnboardingStep.StunAndHeal)
                {
                    string attacker=pr.IsNodeModulesCube ? "ИИ-Агент v0.1" : owner?.IsMiniBoss==true ? "Гуру Табсора" :
                        owner?.Archetype == CoderArchetype.SecurityGuard ? "Охрана СБ" :
                        owner?.Archetype is CoderArchetype.ToxicPM or CoderArchetype.SeniorArchitect ? owner.Name : "Вайбкодер";
                    Vector3 source=pr.IsNodeModulesCube ? ElevatorBoss.Position : owner?.Position ?? pr.Position-pr.Velocity;
                    (hitHost ? onPlayerHit : hitGuest ? onGuestHit : hitSecond)?.Invoke(pr.Damage,$"{attacker} ({pr.Label})",source);
                }
                SpawnProjectileImpactFx(pr, -dir);
                EnemyProjectiles.RemoveAt(i);
                continue;
            }
            EnemyProjectiles[i] = pr;
        }
    }

    private int UpdateGalleyDesks(float dt, Action<int, string>? onPassiveIncome)
    {
        int totalEarnedThisTick = UpdateGalleyRelations(dt,onPassiveIncome);

        for (int i = 0; i < GalleyDesks.Count; i++)
        {
            var desk = GalleyDesks[i];
            if (desk.WhackFlashTimer > 0f)
                desk.WhackFlashTimer = MathF.Max(0f, desk.WhackFlashTimer - dt);

            if (!desk.IsOccupied)
                continue;

            desk.TypingPhase += dt * (desk.IsSlackingInTwitter ? 2.0f : 12.0f);

            if (!desk.IsSlackingInTwitter)
            {
                desk.SlackCycleTimer -= dt;
                if (desk.SlackCycleTimer <= 0f)
                {
                    desk.IsSlackingInTwitter = true;
                    desk.SlackCycleTimer = 25f;
                }
                else
                {
                    bool supported=GalleyDesks.Exists(d=>d.IsOccupied && d.Archetype==CoderArchetype.TeamLeadMeeter && !d.IsSlackingInTwitter);
                    float load=Systems.Friday>0 ? 1.7f : 1;
                    desk.Burnout=Math.Min(100,desk.Burnout+dt*load*.48f*(supported ? .5f : 1)*(Working(CoderArchetype.ToxicPM) ? 1.8f : 1));
                    if(desk.Burnout>=100) { desk.IsSlackingInTwitter=true; continue; }
                    Systems.Risk(debt:desk.TechDebtRate*load*dt);
                    desk.IncomeAccumulator+=DeskRate(desk,supported)*dt;
                    int payout=(int)desk.IncomeAccumulator;
                    desk.IncomeAccumulator-=payout;
                    if(desk.Archetype==CoderArchetype.CryptoBro && (desk.TradingTimer-=dt)<=0)
                    {
                        desk.TradingTimer=24+_rng.Next(18);
                        int trade=_rng.Next(2)==0 ? -35 : 45;
                        payout+=trade;
                        Systems.Announce(desk.CoderName+(trade<0 ? " потерял $35 на комиссии." : " заработал $45 на сделке."));
                    }
                    if(payout!=0)
                    {
                        Systems.Earned+=payout;
                        totalEarnedThisTick+=payout;
                        onPassiveIncome?.Invoke(payout,"Опенспейс");
                    }
                }
            }
        }

        return totalEarnedThisTick;
    }

    // =========================================================================
    // COMBAT RAYCAST & MELEE HIT PROCESSING (WEAPONS + GALLEY BELT DISCIPLINE)
    // =========================================================================
    public bool ProcessPlayerAttack(
        Camera3D camera,
        WeaponAndCombatView weaponSystem,
        Action<int, string>? onGrantMoney,
        out string combatFeedback,
        Vector3? shotDirection = null)
    {
        combatFeedback = "";
        weaponSystem.BallisticWorld=this;
        Vector3 origin = camera.Position;
        Vector3 forward = shotDirection ?? Vector3.Normalize(camera.Target - camera.Position);

        if(weaponSystem.CurrentWeapon is WeaponType.BareHands or WeaponType.CaptureNet)
        {
            combatFeedback=CatchSweep(camera,weaponSystem.CurrentWeapon==WeaponType.CaptureNet);
            if(combatFeedback.Length>0 && weaponSystem.CurrentWeapon==WeaponType.BareHands)
            {
                weaponSystem.TriggerWeaponAttack(camera,hitEnemy:true);
                return true;
            }
        }

        float maxRange = weaponSystem.CurrentWeapon switch
        {
            WeaponType.BareHands => 2.35f, WeaponType.CaptureNet => 3.6f,
            WeaponType.Belt => 3.8f,
            WeaponType.SolderingIron => 5.2f,
            WeaponType.MicroUzi => 26.0f,
            WeaponType.SawedOff => 12.5f,
            WeaponType.ChatGun => 28.0f,
            _ => 4.0f
        };

        int baseDamage = weaponSystem.CurrentWeapon switch
        {
            WeaponType.BareHands => 6, WeaponType.CaptureNet => 12,
            WeaponType.Belt => 24,
            WeaponType.SolderingIron => 42,
            WeaponType.MicroUzi => 18,
            WeaponType.SawedOff => 15,
            WeaponType.ChatGun => 220,
            _ => 35
        };

        WeaponType tool=weaponSystem.CurrentWeapon;
        if(tool==WeaponType.MicroUzi && !InCloud(origin)) Systems.Risk(security:2.6f);
        if(tool==WeaponType.SawedOff && !InCloud(origin)) Systems.Risk(security:7);
        if(tool==WeaponType.Belt && TryParry(camera,forward,weaponSystem,out combatFeedback)) return true;
        bool salt=tool==WeaponType.SawedOff && Systems.Shell==Sidegrade.Salt;
        bool negotiator=tool==WeaponType.SawedOff && Systems.Shell==Sidegrade.Negotiator;
        float surfaceDistance = TraceSurface(origin,forward,maxRange+.01f);
        maxRange = Math.Min(maxRange,surfaceDistance);
        bool captureTool=tool is WeaponType.BareHands or WeaponType.CaptureNet;
        bool melee = captureTool || tool is WeaponType.Belt or WeaponType.SolderingIron;
        // 1. Check if player is whipping a slacking coder on the Galley with the Belt (or any close attack)!
        for (int i = 0; i < GalleyDesks.Count; i++)
        {
            var desk = GalleyDesks[i];
            if (!desk.IsOccupied) continue;

            Vector3 deskTarget = desk.SeatedFeet + Vector3.UnitY * (1.1f * WorkstationScale);
            Vector3 toDesk = deskTarget - origin;
            float dist = toDesk.Length();
            if (dist <= Math.Min(maxRange,4.2f) && Vector3.Dot(forward, Vector3.Normalize(toDesk)) > 0.65f)
            {
                if (weaponSystem.CurrentWeapon == WeaponType.Belt)
                {
                    desk.IsSlackingInTwitter = false;
                    desk.SlackCycleTimer = 25f;
                    desk.WhackFlashTimer = 0.6f;
                    if(Systems.WantsCoffee)
                    {
                        Systems.WantsCoffee=false;
                        foreach(var worker in GalleyDesks) worker.Morale=1;
                    }
                    desk.Morale=1; desk.Burnout=Math.Max(0,desk.Burnout-40);

                    AudioMusicSystem.PlayGalleyWhack();
                    AudioMusicSystem.TriggerRandomVoiceLine("КОММЕРС 90-Х", AudioMusicSystem.Boss90sBeltLines, (int)SpeakerVoiceType.Boss90s);

                    weaponSystem.TriggerWeaponAttack(
                        camera,
                        hitWorldPoint: deskTarget,
                        hitEnemy: true,
                        damageDealt: 0,
                        isCritical: true,
                        customHitLabel: "СМЕНА ВОССТАНОВЛЕНА", shotDirection: forward);

                    combatFeedback = $"ВОСПИТАТЕЛЬНЫЙ ЛЕЩ! {desk.CoderName} закрыл Чириктер и снова пашет в Опенспейсе!";
                    return true;
                }
            }
        }

        Vector3 bossCenter=ElevatorBoss.Position+Vector3.UnitY*1.35f;
        (int Actor, float Distance) TraceActor(Vector3 direction,float limit)
        {
            int actor=-1;
            if(BossInCombat &&
                TryRaySphereHit(origin,direction,bossCenter,1.45f,limit,out _,out float bossDistance))
            { actor=-2; limit=bossDistance; }
            for(int i=0;i<ActiveCoders.Count;i++)
            {
                var c=ActiveCoders[i];
                if(c.Hp<=0 && !c.KnockedOutAlive || (melee || salt) && c.State==CoderAIState.Subdued && !(tool==WeaponType.CaptureNet && c.CanSignNda && CarriedCoder==null)) continue;
                float radius=.75f*c.ModelScale+(melee ? .28f : 0);
                if(tool==WeaponType.CaptureNet || tool==WeaponType.Belt && Systems.Has(Sidegrade.DirectorBelt)) radius+=.24f;
                if(TryRaySphereHit(origin,direction,c.CombatCenter,radius,limit,out _,out float distance))
                { actor=i; limit=distance; }
            }
            return (actor,limit);
        }
        void Impact(Vector3 point,Vector3 center,int damage,bool critical)
        {
            if(tool==WeaponType.SawedOff)
                weaponSystem.ShowImpact(camera,center,damage,critical);
            else weaponSystem.TriggerWeaponAttack(camera,point,true,damage,critical,receiverCenter:center,shotDirection:forward);
        }
        string feedback=combatFeedback;
        void Hit(int actor,Vector3 hitPt,int dmg)
        {
            bool isCrit=false;
            if(actor==-2)
            {
                if(salt) dmg=(int)(dmg*.45f);
                if(negotiator) dmg=(int)(dmg*1.4f);
                if(!captureTool) Systems.Style((StyleAction)((int)tool-1));
                UpdateBossPhase();
                if(ElevatorBoss.Phase==2 && Systems.Puddles.Exists(p=>p.Electric>0 && Vector3.DistanceSquared(p.Position,ElevatorBoss.Position)<=10.24f)) ShortBoss();
                bool shield=ElevatorBoss.ShieldHp>0;
                if(shield)
                {
                    ElevatorBoss.ShieldHp=Math.Max(0,ElevatorBoss.ShieldHp-(tool==WeaponType.MicroUzi ? 60 : dmg*.35f));
                    dmg=0;
                }
                else if(tool==WeaponType.SawedOff && Vector3.Dot(forward,new(MathF.Sin(ElevatorBoss.FacingYaw),0,MathF.Cos(ElevatorBoss.FacingYaw)))<-.15f &&
                    TryRaySphereHit(origin,forward,ElevatorBoss.ChestCore,.29f,maxRange,out _,out _))
                { dmg*=3; isCrit=true; }
                ElevatorBoss.ShieldDelay=5;
                // The phase-two firewall must give the final broadcast time to begin.
                float minimum=ElevatorBoss.Phase==2 ? MathF.Min(ElevatorBoss.Hp,ElevatorBoss.MaxHp*.33f) : 0;
                ElevatorBoss.Hp=MathF.Max(minimum,ElevatorBoss.Hp-dmg);
                UpdateBossPhase();
                ElevatorBoss.HitFlashTimer = 0.16f;
                ElevatorBoss.HitFlinchTimer=.24f;
                ElevatorBoss.HitDirection=new(forward.X,0,forward.Z);
                if(!shield) ElevatorBoss.KnockbackVel=ElevatorBoss.HitDirection*.85f;

                AudioMusicSystem.PlayHitmarker();
                Impact(hitPt,bossCenter,dmg,isCrit);
                Program.TriggerHitmarker(isCrit, ElevatorBoss.Hp <= 0f);
                if (isCrit) Program.TriggerHitstop(0.036f);
                else if (tool is WeaponType.SawedOff or WeaponType.Belt or WeaponType.ChatGun) Program.TriggerHitstop(0.026f);
                SpawnCombatDebrisBurst(hitPt, -forward, isCrit ? 12 : 6,
                    shield ? new(118, 224, 245, 255) : new(255, 194, 72, 255), Finish.Lamp, glowing: true, speed: 5.2f, sizeScale: 0.72f);

                if (ElevatorBoss.Hp <= 0f)
                {
                    BeginBossMeltdown(weaponSystem, onGrantMoney);
                    feedback = "ЯДРО ПЕРЕГРЕТО! ИИ-АГЕНТ v0.1 ДЕТОНИРУЕТ!!";
                }
                else
                {
                    feedback = shield ? $"Файрвол: {ElevatorBoss.ShieldHp:F0}" : $"ПОПАДАНИЕ ПО ИИ-АГЕНТУ v0.1: -{dmg} ХП (осталось {(int)ElevatorBoss.Hp}/{ElevatorBoss.MaxHp})";
                }
                return;
            }
            var target=ActiveCoders[actor];
            if(tool==WeaponType.CaptureNet && target.CanSignNda && CarriedCoder==null)
            {
                feedback=PickUp(target,onGrantMoney,tool);
                if(!CanCarryWith(tool)) weaponSystem.Equip(WeaponType.BareHands);
                return;
            }
            if((target.FloorHunter || target.EscapingToDoor || target.Stress>=70) && TryDodge(target,tool,origin)) { feedback=target.SpeechBubble; weaponSystem.TriggerWeaponAttack(camera); return; }
            bool ambush=target.State==CoderAIState.LuredAtBait && QuietAt(origin) && target.Stress<55;
            if(ambush && tool==WeaponType.CaptureNet) dmg=(int)MathF.Ceiling(target.Hp);
            else if(tool==WeaponType.CaptureNet && !target.TimidIntern)
                dmg = Systems.Has(Sidegrade.NetPro90s) ? 38 : Systems.Has(Sidegrade.NetReinforced) ? 24 : 12;
            bool wasSubduedBefore = target.State == CoderAIState.Subdued;
            bool wasBaitAmbush = target.State is CoderAIState.SittingAtMacBook or CoderAIState.LuredAtBait;
            Vector3 coderFwd = new(MathF.Sin(target.FacingYaw), 0f, MathF.Cos(target.FacingYaw));
            Vector3 toAttacker = new(origin.X - target.Position.X, 0f, origin.Z - target.Position.Z);
            bool wasBackstab = !target.Aggro && toAttacker.LengthSquared() > 0.01f && Vector3.Dot(coderFwd, Vector3.Normalize(toAttacker)) < -0.25f;
            bool wasElectro = target.ElectroShockedTimer > 0f || Systems.Puddles.Exists(p => p.Electric > 0 && Vector3.DistanceSquared(target.Position, p.Position) <= 3.3f * 3.3f);

            isCrit=ambush || target.State==CoderAIState.SittingAtMacBook;
            if(target.IsCombatCoder)
            {
                target.Aggro = true;
                target.Windup = 0;
                target.AttackCooldown = Math.Max(target.AttackCooldown,.45f);
            }
            float power=captureTool ? 1 : Systems.Style((StyleAction)((int)tool-1));
            if((melee || salt) && Systems.Has(Sidegrade.CarbonNda)) power*=2;
            float fearPower=power*Systems.FearMultiplier;
            var impulse=new Vector3(forward.X,0,forward.Z);
            target.KnockbackVel=impulse*(tool==WeaponType.BareHands ? 1.45f : melee ? 7 : tool==WeaponType.SawedOff ? 9 : 3.6f);
            if(tool==WeaponType.Belt && Systems.Coffee>0) target.KnockbackVel*=2;
            target.HitStaggerTimer=melee ? .32f : .18f;
            target.HitFlashTimer=.09f;
            target.HitFlinchTimer=.24f;
            target.HitDirection=impulse;
            if(melee || salt)
            {
                // Blunt and capture tools leave a live candidate available for NDA.
                if(!salt)
                {
                    target.Hp=Math.Max(0,target.Hp-dmg);
                    if(target.Hp==0)
                    {
                        target.State=CoderAIState.Subdued;
                        target.KnockedOutAlive=true;
                    }
                }
                bool disarm=tool==WeaponType.Belt && !target.LaptopLost;
                if(disarm) target.LaptopLost=true;
                if(tool==WeaponType.Belt)
                {
                    target.Pressure(28*power,disarm ? 40 : 0,34*fearPower);
                    if(Systems.Has(Sidegrade.DirectorBelt))
                    {
                        target.HitStaggerTimer=.23f;
                        foreach(var other in ActiveCoders)
                            if(other!=target && other.Hp>0 && other.State!=CoderAIState.Subdued &&
                                Vector3.DistanceSquared(other.Position,target.Position)<2.3f &&
                                Vector3.Dot(other.CombatCenter-origin,forward)>0)
                                other.Pressure(28*power,0,34*fearPower);
                    }
                }
                else if(captureTool)
                {
                    target.HitStaggerTimer=.15f;
                    if(tool==WeaponType.CaptureNet && !target.TimidIntern && target.State!=CoderAIState.Subdued)
                    {
                        if(Systems.Has(Sidegrade.NetPro90s))
                        {
                            target.Pressure(48*power, 36*power, 48*fearPower);
                            target.StunTimer = 2.0f;
                            if(target.State==CoderAIState.Subdued)
                                StyleComboSystem.RegisterTrick("САЧОК-ПРО!", "PRO NET!", 0.35f, 32f, new Color(245, 202, 98, 255), hitstop: true, shake: 0.020f);
                        }
                        else if(Systems.Has(Sidegrade.NetReinforced))
                        {
                            target.Pressure(30*power, 18*power, 30*fearPower);
                            target.StunTimer = 1.4f;
                        }
                        else if(target.Hp > 0)
                        {
                            // Weak un-upgraded starter net: non-tutorial vibe coders laugh, taunt you, and dash away!
                            string[] mocks =
                            [
                                "ХА-ХА! С САЧКОМ ПРОТИВ ПРОДА?!",
                                "С ТАКИМ САЧКОМ ТОЛЬКО БАБОЧЕК ЛОВИТЬ!",
                                "ЛОЛ, ПРОКАЧАЙ САЧОК У ВЕРОНИКИ!",
                                "ЩЕКОТНО! ПОЙМАЙ ЕСЛИ СМОЖЕШЬ!"
                            ];
                            target.SpeechBubble = mocks[_rng.Next(mocks.Length)];
                            target.SpeechTimer = 3.0f;
                            target.Burst = 1.6f;
                            Panic(target);
                        }
                    }
                }
                else if(salt) { target.Pressure(38*power,34*power,48*fearPower); target.StunTimer=1.8f; }
                else
                {
                    float tape=Systems.Has(Sidegrade.Tape) ? 1.45f : 1;
                    if(Systems.Has(Sidegrade.RosinNapalm)) { target.KnockedOutAlive=false; target.Hp=Math.Max(0,target.Hp-16); target.Ignition=3; }
                    target.Pressure(14*power,32*power*tape,22*fearPower); target.StunTimer=1.5f*tape;
                }
                bool burnedOut=target.Hp<=0 && !target.KnockedOutAlive;
                bool newlySubdued = !wasSubduedBefore && target.State == CoderAIState.Subdued;
                if (disarm || newlySubdued || burnedOut)
                    StyleComboSystem.EvaluateTakedown(target, tool, Program.PlayerAirborne, Program.PlayerDashing, Systems.Coffee > 0f, wasBackstab, wasBaitAmbush, disarm, wasElectro, newlySubdued || burnedOut);
                if(burnedOut) KillCoder(target,onGrantMoney);
                feedback=burnedOut ? "Кандидат выбыл от ожогов. СБ +14" : target.State==CoderAIState.Subdued ? "Кандидат сломлен. Подпиши NDA [E]" :
                    disarm ? "Ноутбук выбит. Эго -40" : $"Выдержка {target.Composure:F0}   Эго {target.Ego:F0}";
                if(tool==WeaponType.CaptureNet && target.CanSignNda && Systems.Onboarding==OnboardingStep.StunAndHeal)
                    Program.TrainingScratch(target.Position);
                if(target.State==CoderAIState.Subdued) LootElevatorKey(target);
                Impact(hitPt,target.CombatCenter,salt ? 0 : dmg,disarm);
                if(tool==WeaponType.CaptureNet && target.TimidIntern && target.State==CoderAIState.Subdued &&
                    Systems.Has(Sidegrade.CarbonNda) && CarriedCoder==null) feedback=PickUp(target,onGrantMoney,tool);
            }
            else
            {
                if(negotiator && !target.IsMiniBoss) dmg=Math.Max(dmg,(int)MathF.Ceiling(target.Hp));
                target.Hp=Math.Max(0,target.Hp-dmg);
                Impact(hitPt,target.CombatCenter,dmg,isCrit);
                if(target.Hp<=0)
                {
                    StyleComboSystem.EvaluateTakedown(target, tool, Program.PlayerAirborne, Program.PlayerDashing, Systems.Coffee > 0f, wasBackstab, wasBaitAmbush, false, wasElectro, true);
                    KillCoder(target,onGrantMoney);
                    feedback="Кандидат выбыл. СБ +14";
                }
            }
            if(target.Hp>0 && target.State!=CoderAIState.Subdued) { target.Stress=100; Spook(target); }
            if(!target.EscapingToDoor && !target.FloorHunter && target.Hp>0 && !target.CanSignNda && (target.TimidIntern || !target.IsCombatCoder && !target.IsMiniBoss)) Panic(target);
            bool killedOrSubdued = target.Hp <= 0 || (!wasSubduedBefore && target.State == CoderAIState.Subdued);
            Program.TriggerHitmarker(isCrit, killedOrSubdued);
            if (killedOrSubdued) Program.TriggerHitstop(0.036f);
            else if (tool is WeaponType.Belt or WeaponType.SawedOff or WeaponType.SolderingIron) Program.TriggerHitstop(0.025f);
            // Flying office papers, coffee droplets & kinetic sparks on every hit!
            SpawnCombatDebrisBurst(hitPt, -forward, killedOrSubdued ? 8 : 4, new(246, 240, 224, 255), Finish.Paint, speed: 4.1f, sizeScale: 1.05f);
            SpawnCombatDebrisBurst(hitPt, -forward, killedOrSubdued ? 6 : 3,
                tool == WeaponType.SolderingIron ? new(115, 228, 255, 255) : new(82, 46, 24, 255),
                tool == WeaponType.SolderingIron ? Finish.Plasma : Finish.Paint,
                glowing: tool == WeaponType.SolderingIron, speed: 4.8f, sizeScale: 0.72f);
            AudioMusicSystem.PlayHitmarker();
        }

        if(tool==WeaponType.SawedOff)
        {
            var hits=new Dictionary<int,(int Count,Vector3 Point)>();
            for(int i=0;i<8;i++)
            {
                Vector3 dir=weaponSystem.PelletDirection(forward,i);
                var (actor,distance)=TraceActor(dir,TraceSurface(origin,dir,12.5f));
                if(actor!=-1) hits[actor]=(hits.GetValueOrDefault(actor).Count+1,origin+dir*distance);
            }
            if(hits.Count>0)
            {
                weaponSystem.TriggerWeaponAttack(camera,shotDirection:forward);
                foreach(var (actor,hit) in hits) Hit(actor,hit.Point,baseDamage*hit.Count);
                combatFeedback=feedback;
                return true;
            }
        }
        else
        {
            var (actor,distance)=TraceActor(forward,maxRange);
            if(actor!=-1)
            {
                Hit(actor,origin+forward*distance,baseDamage);
                combatFeedback=feedback;
                return true;
            }
        }
        if(combatFeedback.Length>0)
        {
            weaponSystem.TriggerWeaponAttack(camera,hitEnemy:true);
            return true;
        }
        if(surfaceDistance<=maxRange && HitStation(camera,weaponSystem,origin+forward*surfaceDistance,out combatFeedback)) return true;

        if(melee) foreach(var nearby in ActiveCoders)
            if(!nearby.CanSignNda && Vector3.DistanceSquared(origin,nearby.CombatCenter)<5 && Visible(origin,nearby.CombatCenter)) { nearby.Stress=100; Spook(nearby); }
        // 4. Missed enemy - still fire weapon visuals into world geometry
        weaponSystem.TriggerWeaponAttack(camera, surfaceDistance <= maxRange ? origin+forward*surfaceDistance : null, hitEnemy: false, shotDirection: forward);
        return false;
    }

    private static bool TryRaySphereHit(Vector3 rayOrigin, Vector3 rayDir, Vector3 sphereCenter, float radius, float maxDist, out Vector3 hitPoint, out float hitDist)
    {
        hitPoint = sphereCenter;
        hitDist = maxDist;

        Vector3 m = rayOrigin - sphereCenter;
        float b = Vector3.Dot(m, rayDir);
        float c = Vector3.Dot(m, m) - radius * radius;

        if (c > 0f && b > 0f) return false;
        float discr = b * b - c;
        if (discr < 0f) return false;

        float t = -b - MathF.Sqrt(discr);
        if (t < 0f) t = 0f;
        if (t > maxDist) return false;

        hitDist = t;
        hitPoint = rayOrigin + rayDir * t;
        return true;
    }

    // =========================================================================
    // [E] CONTEXTUAL INTERACTION (PICK UP CODER / HR TAROT SELL / GALLEY SEAT / MACBOOK / ELEVATOR)
    // =========================================================================
    public string HandleInteractKeyE(
        Vector3 playerPos,
        Func<int, bool> trySpendMoney,
        Action<int, string> onGrantMoney, WeaponType tool=WeaponType.BareHands)
    {
        // 1. If carrying a captured coder -> check HR Tarot Booth or Galley Desk
        if (CarriedCoder != null)
        {
            float distHr = Vector2.Distance(new Vector2(playerPos.X, playerPos.Z), new Vector2(HrTarotBoothPos.X, HrTarotBoothPos.Z));
            if (distHr <= 4.6f)
            {
                return SellCarriedCoderToHrVeronika(onGrantMoney);
            }

            // Check Galley Desks
            for (int i = 0; i < GalleyDesks.Count; i++)
            {
                var desk = GalleyDesks[i];
                float distDesk = Vector2.Distance(new Vector2(playerPos.X, playerPos.Z), new Vector2(desk.DeskPosition.X, desk.DeskPosition.Z));
                if (distDesk <= 3.6f && !desk.IsOccupied)
                {
                    return SeatCarriedCoderAtGalley(i);
                }
            }


        }

        // 2. Check if near a Subdued Coder to pick them up
        for (int i = 0; i < ActiveCoders.Count; i++)
        {
            var c = ActiveCoders[i];
            if (c.CanSignNda)
            {
                float dist = Vector2.Distance(new Vector2(playerPos.X, playerPos.Z), new Vector2(c.Position.X, c.Position.Z));
                if (dist <= 3.8f)
                {
                    return PickUp(c,onGrantMoney,tool);
                }
            }
        }

        // 4. Check if near Central Armored Elevator Core (North or South doors) or Call Buttons
        if (BossInCombat && !BossEscapedPursuit && IsPlayerInsideNorthElevatorCabin(playerPos))
            return TriggerCleverElevatorEscape();
        float distElevNorth = Vector2.Distance(new Vector2(playerPos.X, playerPos.Z), new Vector2(NorthElevatorPos.X, NorthElevatorPos.Z));
        float distElevSouth = Vector2.Distance(new Vector2(playerPos.X, playerPos.Z), new Vector2(SouthElevatorDoorPos.X, SouthElevatorDoorPos.Z));
        float distBtnNorth = Vector2.Distance(new Vector2(playerPos.X, playerPos.Z), new Vector2(ElevatorButtonPos.X, ElevatorButtonPos.Z));
        float distBtnSouth = Vector2.Distance(new Vector2(playerPos.X, playerPos.Z), new Vector2(SouthElevatorButtonPos.X, SouthElevatorButtonPos.Z));
        if (distElevNorth <= 5.5f || distElevSouth <= 5.2f || distBtnNorth <= 4.0f || distBtnSouth <= 4.0f)
        {
            TrySummonElevatorBoss(out string msg);
            return msg;
        }

        return Loc.T("Наведи прицел на объект или положи наживку [G]","Aim at an object or drop bait [G]");
    }

    public string SellCarriedCoderToHrVeronika(Action<int, string>? onGrantMoney)
    {
        if (CarriedCoder == null)
            return "У тебя нет пойманного кодера на плече!";

        if(Slot.Active) return "Дождись результата рулетки Вероники";
        var c = CarriedCoder;
        CarriedCoder = null;
        TotalCodersSoldToHr++;
        if(TotalCodersSoldToHr==2 && !Systems.WingsUnlocked)
            foreach(var trainee in ActiveCoders.Where(candidate=>candidate.TimidIntern))
                trainee.Rarity=VibeCoderRarity.Uncommon;
        Systems.Deliver(c.Archetype,false,c.TarotPayoutValue);

        LastTarotCardDrawn=FloorSystems.VibeCheck(c.Archetype);
        int payout = StyleComboSystem.ComputeMultipliedPayout(c.TarotPayoutValue, c.CaptureMultiplier, out _);
        LastTarotVerdictText = $"{LastTarotCardDrawn}   {c.Name}   +${payout}";
        TarotVerdictTimer = 6.5f;

        Systems.Earned+=payout;
        onGrantMoney?.Invoke(payout, $"HR-Таролог Вероника ({c.Name})");


        AudioMusicSystem.PlayTarotCash();
        AudioMusicSystem.TriggerRandomVoiceLine("HR-ТАРОЛОГ ВЕРОНИКА", AudioMusicSystem.VeronikaTarotLines, (int)SpeakerVoiceType.VeronikaTarot);
        Systems.UpdateOnboarding(this,Program.SharedCash);
        if(c.Rarity!=VibeCoderRarity.Common)
        {
            Slot.Start(Systems,c.Rarity,_rng,onGrantMoney);
            Program.ShareSlot(Slot);
        }

        return LastTarotVerdictText;
    }

    public string SeatCarriedCoderAtGalley(int deskIndex = -1)
    {
        if (CarriedCoder == null)
            return "У тебя нет пойманного кодера на плече!";

        int targetIdx = deskIndex;
        if (targetIdx < 0 || targetIdx >= GalleyDesks.Count || GalleyDesks[targetIdx].IsOccupied)
        {
            targetIdx = -1;
            for (int i = 0; i < GalleyDesks.Count; i++)
            {
                if (!GalleyDesks[i].IsOccupied)
                {
                    targetIdx = i;
                    break;
                }
            }
        }

        if (targetIdx < 0)
            return "ВСЕ 4 СТОЛА ОПЕНСПЕЙСА ЗАНЯТЫ! Сдай кодера HR-Тарологу Веронике за мгновенный кэш!";

        var desk = GalleyDesks[targetIdx];
        desk.IsOccupied = true;
        desk.CoderName = CarriedCoder.Name;
        desk.Archetype = CarriedCoder.Archetype;
        desk.HoodieColor = CarriedCoder.HoodieColor;
        desk.SpeciesId = CarriedCoder.SpeciesId;
        desk.GlassesColor = CarriedCoder.GlassesColor;
        desk.IsSlackingInTwitter = false;
        desk.SlackCycleTimer = 25f;
        desk.IncomeAccumulator = 0f;

        desk.Productivity=desk.Archetype switch { CoderArchetype.JuniorPrompter=>14, CoderArchetype.SeniorArchitect=>28,
            CoderArchetype.TeamLeadMeeter=>18, _=>20 };
        desk.TechDebtRate=desk.Archetype switch { CoderArchetype.JuniorPrompter=>.28f,CoderArchetype.SeniorArchitect=>-.45f,_=>0 };
        desk.Burnout=0; desk.Morale=Systems.WantsCoffee ? .65f : 1;
        Systems.Deliver(desk.Archetype,true,CarriedCoder.TarotPayoutValue);
        string name = CarriedCoder.Name;
        CarriedCoder = null;

        AudioMusicSystem.PlayTarotCash();
        Systems.UpdateOnboarding(this,Program.SharedCash);
        return $"ПОСАЖЕН В ОПЕНСПЕЙС #{targetIdx + 1}: {name} в смене. Ставка ${desk.Productivity}/с";
    }

    public void SpawnCashBurst(Vector3 origin, int count, string label = "$$$")
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 vel = new Vector3(
                (_rng.NextSingle() - 0.5f) * 3.8f,
                1.8f + _rng.NextSingle() * 2.6f,
                (_rng.NextSingle() - 0.5f) * 3.8f);

            CashParticles.Add(new CashBurstParticle3D
            {
                Position = origin + new Vector3((_rng.NextSingle() - 0.5f) * 0.4f, _rng.NextSingle() * 0.3f, (_rng.NextSingle() - 0.5f) * 0.4f),
                Velocity = vel,
                Life = 1.6f + _rng.NextSingle() * 0.8f,
                MaxLife = 2.2f,
                Label = (i == 0) ? label : "$$$",
                Tint = (i % 2 == 0) ? new Color(65, 255, 110, 255) : new Color(255, 220, 65, 255)
            });
        }
    }

    // =========================================================================
    // 3D RENDERING: SKYSCRAPER FLOOR 1, BOOTHS, GALLEY, TRAP, CODERS & BOSSES
    // =========================================================================
    public void DrawWorld3D(Camera3D camera)
    {
        if(!VisualEngine3D.CastingShadows)
        {
            VisualEngine3D.World(camera,this);
            VisualEngine3D.SetTrapLight(Systems.PlacedBait?.Position+Vector3.UnitY*.14f ?? new(0,-100,0));
        }
        DrawSkyscraperArchitecture3D();
        DrawWingGates();
        DrawEntryHardware();
        if(!VisualEngine3D.CastingShadows)
        {
            VisualEngine3D.Shadow(HrTarotBoothPos,4.8f,3);
            if(Systems.PlacedBait is {} bait) VisualEngine3D.Shadow(bait.Position,.43f,.33f);
            foreach(var desk in GalleyDesks) VisualEngine3D.Shadow(desk.DeskPosition,3,2.4f);
        }
        DrawBugHunt();
        DrawOfficeProps3D();
        DrawHrTarotBoothAndVeronika3D();
        Draw90sArsenalAndShopStand3D();
        DrawGalleyWorkstations3D();
        DrawCentralMacBookTrapAndCovers3D();
        DrawLivingOffice();
        DrawBossArenaPartition3D(glassPass: false);
        DrawElevatorDoors(false);
        foreach(var ally in Teammates)
        if(ally.Pose.Weapon is WeaponType.BareHands or WeaponType.CaptureNet)
        {
            float yaw=ally.Pose.Yaw, height=ally.Pose.Eye.Y<1.4f ? .75f : 1;
            VisualEngine3D.DrawCharacterDirectionalShadow(ally.Feet,1.7f*height,.32f);
            VisualEngine3D.Asset("char_partner_90s",ally.Feet,yaw*180/MathF.PI,stretch:new Vector3(1,height,1),hideProp:true,tint:ally.Blazer);
            if(ally.Pose.Weapon==WeaponType.CaptureNet)
            {
                var grip=ally.Feet+Vector3.Transform(new Vector3(-.095f,1.25f*height,.26f),Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw));
                if(VisualEngine3D.HasAsset("weapon_net_90s")) VisualEngine3D.Asset("weapon_net_90s",grip,yaw*180/MathF.PI);
                else
                {
                    var tip=grip+new Vector3(MathF.Sin(yaw),.23f,MathF.Cos(yaw))*.62f;
                    VisualEngine3D.Rod(grip,tip,.014f,.012f,8,new(154,103,68,255));
                    Raylib.DrawCircle3D(tip,.16f,Vector3.UnitX,90,Color.LightGray);
                }
            }
        }
        else ally.Draw();

        // Draw all active Vibe Coders & SaaS Guru Mini-Boss
        for (int i = 0; i < ActiveCoders.Count; i++)
        {
            DrawVibeCoder3D(ActiveCoders[i]);
        }

        // Draw Floor 1 Elevator Boss ("СБЕЖАВШИЙ ИИ-АГЕНТ v0.1")
        DrawEscapedAgentBoss3D();

        // Draw Enemy Projectiles (3D 500 ERROR blocks & 40 GB node_modules cubes)
        DrawStations();
        if(VisualEngine3D.CastingShadows) return;
        DrawEnemyProjectiles3D();
        DrawCombatDebris3D();

        // Draw 3D Cash Bundles flying in the air
        for (int i = 0; i < CashParticles.Count; i++)
        {
            var cp = CashParticles[i];
            VisualEngine3D.Box(cp.Position, 0.28f, 0.12f, 0.16f, new Color(45, 205, 85, 245));

        }
        DrawBossCutsceneDroppedGun3D();
        DrawBossCutsceneMissileMeshes3D();
        VisualEngine3D.DrawArchitecture(BuildTowerArchitecture, glass:true);
        Rlgl.DisableDepthMask();
        DrawBossArenaPartition3D(glassPass: true);
        DrawElevatorDoors(true);
        DrawSunDust(camera);
        Rlgl.EnableDepthMask();
        DrawBossCutsceneRocketFx3D(camera);
        DrawBossArenaChaos3D(camera);
        DrawBossMeltdownAndWreckage3D(camera);
    }

    private void DrawOfficeProps3D()
    {
        for (int i = 0; i < OfficeProps.Count; i++)
        {
            var p = OfficeProps[i];
            Vector3 pos = p.Position;
            if (p.Kind is OfficePropKind.CeilingDuct or OfficePropKind.MeetingGlass) continue;
            VisualEngine3D.Shadow(pos, p.Size.X+1, p.Size.Z+1);
            switch (p.Kind)
            {
                case OfficePropKind.DualMonitorDesk:
                    DrawPod(pos,i,false,p.RotationYaw==180);
                    break;
                case OfficePropKind.MeetingDesk:
                    if (MathF.Abs(p.RotationYaw) is > 45f and < 135f)
                    {
                        // Executive Glass Meeting Room walnut & brushed-brass conference table + 4 executive chairs
                        VisualEngine3D.Box(pos + new Vector3(0f, .74f, 0f), 1.12f, .075f, 2.38f, new Color(64, 40, 26, 255), Finish.Leather);
                        VisualEngine3D.Box(pos + new Vector3(0f, .705f, 0f), 1.15f, .028f, 2.41f, new Color(172, 134, 72, 255), Finish.Bronze);
                        foreach (float lz in new[] { -.86f, .86f })
                            VisualEngine3D.Box(pos + new Vector3(0f, .36f, lz), .68f, .70f, .09f, new Color(46, 52, 60, 255), Finish.Slate);
                        foreach (int side in new[] { -1, 1 })
                        foreach (float cz in new[] { -.64f, .64f })
                            VisualEngine3D.Asset("prop_office_chair", pos + new Vector3(side * .82f, 0f, cz), side > 0 ? -90f : 90f, WorkstationScale);
                    }
                    else
                    {
                        VisualEngine3D.Asset("office_workstation", pos, 180,WorkstationScale);
                        foreach (int side in new[] { -1, 1 })
                        {
                            VisualEngine3D.Box(pos+new Vector3(side*.86f,.36f,0)*WorkstationScale,.08f*WorkstationScale,.72f*WorkstationScale,.88f*WorkstationScale,new Color(65,70,78,255));
                            VisualEngine3D.Asset("prop_office_chair", pos+new Vector3(side*.50f,0,.64f)*WorkstationScale, 180+side*12f, WorkstationScale);
                            VisualEngine3D.Asset("prop_office_chair", pos+new Vector3(side*.50f,0,-.64f)*WorkstationScale, side*-12f, WorkstationScale);
                        }
                    }
                    break;
                case OfficePropKind.XeroxPhotocopier:
                    VisualEngine3D.Asset("prop_xerox_printer", pos, p.RotationYaw);
                    break;
                case OfficePropKind.FilingCabinet:
                    VisualEngine3D.Asset("prop_filing_cabinet", pos, p.RotationYaw, stretch:p.Size.Y<1 ? new Vector3(.70f,.50f,1) : Vector3.One);
                    break;
                case OfficePropKind.RollingWhiteboard:
                    VisualEngine3D.Asset("prop_whiteboard", pos,p.RotationYaw);
                    break;
                case OfficePropKind.WaterCooler:
                    VisualEngine3D.DrawCooler(pos,p.RotationYaw,CoolerEmpty(p.Position));
                    break;
                case OfficePropKind.VendingMachine:
                    VisualEngine3D.Asset("prop_vending_machine", pos, p.RotationYaw);
                    break;
                case OfficePropKind.LeatherSofa:
                    VisualEngine3D.Asset("prop_leather_sofa", pos,p.RotationYaw,SofaScale,stretch:SofaStretch);
                    break;
                case OfficePropKind.PottedFicus:
                    VisualEngine3D.Asset("prop_ficus_tree", pos);
                    break;
                case OfficePropKind.TrashBin:
                    VisualEngine3D.Asset("prop_trash_bin", pos, i%3*9-8);
                    break;

            }
        }
    }

    private void DrawSkyscraperArchitecture3D()
    {
        VisualEngine3D.Floor(HalfWidth*2, HalfDepth*2);

        VisualEngine3D.DrawArchitecture(BuildTowerArchitecture);

        // 4. Architectural Honed Travertine Limestone, Warm Natural Walnut Wainscoting & Subtle Anodized Sconce Columns
        Color limestone = new(236, 230, 218, 255);
        Color walnut = new(156, 112, 76, 255);
        Color darkAlum = new(62, 64, 68, 255);
        Color brass = new(162, 132, 86, 255);
        Color darkBronze = new(74, 64, 54, 255);
        for (int i = 0; i < Pillars.Count; i++)
        {
            var p = Pillars[i];
            Vector3 colPos = p.Center + new Vector3(0f, CeilingHeight * 0.5f, 0f);
            float w = p.HalfWidth * 2f;
            float d = p.HalfDepth * 2f;

            // Warm honed travertine/limestone ashlar column shaft
            VisualEngine3D.Box(colPos, w, CeilingHeight, d, limestone, Finish.Plaster);
            // Warm oiled-walnut lower wainscoting + clean anodized plinth & walnut chair-rail
            VisualEngine3D.Box(p.Center + new Vector3(0f, 0.58f, 0f), w + 0.06f, 1.16f, d + 0.06f, walnut, Finish.Leather);
            VisualEngine3D.Box(p.Center + new Vector3(0f, 0.06f, 0f), w + 0.09f, 0.12f, d + 0.09f, darkAlum, Finish.Armor);
            VisualEngine3D.Box(p.Center + new Vector3(0f, 1.18f, 0f), w + 0.09f, 0.055f, d + 0.09f, darkBronze, Finish.Bronze);
            VisualEngine3D.Box(p.Center + new Vector3(0f, 1.24f, 0f), w + 0.07f, 0.045f, d + 0.07f, walnut, Finish.Leather);
            // Restrained dark-anodized architectural shadow reveals at column corners (ZERO loud yellow corner stripes!)
            foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
            {
                Vector3 corner = p.Center + new Vector3(sx * (p.HalfWidth + 0.008f), 0f, sz * (p.HalfDepth + 0.008f));
                VisualEngine3D.Box(corner + Vector3.UnitY * 5.6f, 0.08f, CeilingHeight, 0.08f, darkAlum, Finish.Armor);
            }
            // Clean walnut architectural frieze collar (aligning with interior wall cornice at Y=4.6m) & anodized crown
            VisualEngine3D.Box(p.Center + new Vector3(0f, 4.64f, 0f), w + 0.08f, 0.11f, d + 0.08f, walnut, Finish.Leather);
            VisualEngine3D.Box(p.Center + new Vector3(0f, CeilingHeight - 0.16f, 0f), w + 0.12f, 0.18f, d + 0.12f, darkAlum, Finish.Armor);

            // Warm Art-Deco frosted-glass & brass wall sconces on core atrium/corridor columns (matching elevator lobby!)
            if (i < 6)
            {
                int faceX = -Math.Sign(p.Center.X);
                Vector3 sconce = p.Center + new Vector3(faceX * (p.HalfWidth + 0.055f), 2.92f, 0f);
                VisualEngine3D.Box(sconce, 0.055f, 0.52f, 0.20f, darkBronze, Finish.Bronze);
                VisualEngine3D.Box(sconce + new Vector3(faceX * 0.025f, 0f, 0f), 0.045f, 0.36f, 0.12f, new Color(255, 224, 168, 255), Finish.Lamp);
                foreach (float sy in new[] { -0.21f, 0.21f })
                    VisualEngine3D.Box(sconce + new Vector3(faceX * 0.02f, sy, 0f), 0.07f, 0.04f, 0.22f, brass, Finish.Bronze);
            }

            if(i<4)
            {
                int side=i==0 || i==1 ? -Math.Sign(p.Center.X) : 0;
                Vector3 normal=side==0 ? Vector3.UnitZ : side*Vector3.UnitX;
                VisualEngine3D.Asset("prop_poster_frame",p.Center+Vector3.UnitY*1.87f+
                    normal*((side==0 ? p.HalfDepth : p.HalfWidth)+.003f),side*90);
            }

        }
    }

    private void BuildTowerArchitecture(VisualEngine3D.ArchitectureBuilder mesh)
    {
        BuildCurtainWallsAndSkyline(mesh);
        BuildOfficeServices(mesh);
        BuildLift(mesh);
        BuildInteriorRooms(mesh);
        BuildEntryRooms(mesh);
        // Warm bordered lounge rug under the atrium leather sofas + café hospitality rug under the espresso break corner
        void BorderedRug(float cx, float cz, float rw, float rd, Color fill, Color trim)
        {
            mesh.Add(new(cx, .016f, cz), new(rw, .018f, rd), fill, Finish.Carpet);
            foreach (int s in new[] { -1, 1 })
            {
                mesh.Add(new(cx, .022f, cz + s * (rd * .5f - .06f)), new(rw, .012f, .07f), trim, Finish.Bronze);
                mesh.Add(new(cx + s * (rw * .5f - .06f), .022f, cz), new(.07f, .012f, rd), trim, Finish.Bronze);
            }
        }
        Color brass=new(189,145,76,255), darkBrass=new(112,84,44,255);
        BorderedRug(-4.55f, 9.45f, 3.85f, 3.65f, new(126, 92, 70, 255), darkBrass);
        BorderedRug(3.75f, 12.80f, 2.75f, 2.65f, new(56, 46, 42, 255), darkBrass);
        // Reception flank seating alcove rugs (replacing old floating lobby boxes!)
        foreach (int side in new[] { -1, 1 })
            BorderedRug(side * 8.15f, 2.05f, 2.35f, 3.25f, new(68, 52, 46, 255), darkBrass);
        // Panoramic Executive Glass Meeting Suite rugs & Far-North Executive Lounge rug
        foreach (int side in new[] { -1, 1 })
            BorderedRug(side * 33.75f, 1.0f, 6.4f, 6.8f, new(44, 54, 62, 255), darkBrass);
        BorderedRug(0f, -30.5f, 7.6f, 5.8f, new(46, 58, 54, 255), darkBrass);
        BorderedRug(-5.8f, -30.5f, 3.2f, 6.6f, new(68, 44, 42, 255), darkBrass);
        BorderedRug( 5.8f, -30.5f, 3.2f, 6.6f, new(68, 44, 42, 255), darkBrass);
        float start=SouthElevatorDoorPos.Z+.15f, end=15.36f;
        float center=(start+end)*.5f;
        // Royal-burgundy woven Art-Deco executive lobby runner extending cleanly flush to Veronika's reception wall!
        mesh.Add(new(0,.013f,center),new(4.8f,.024f,end-start),new(108,24,38,255),Finish.Carpet);
        // Matching North Executive Runner guiding directly toward the North Elevator Boss!
        float northStart = -24.5f, northEnd = NorthElevatorPos.Z - .15f;
        float northMid = (northStart + northEnd) * .5f;
        mesh.Add(new(0,.013f,northMid),new(4.8f,.024f,northEnd-northStart),new(108,24,38,255),Finish.Carpet);
        foreach (int s in new[] { -1, 1 })
            mesh.Add(new(s*2.40f,.020f,northMid),new(.08f,.028f,northEnd-northStart),darkBrass,Finish.Bronze);

        // Class-A commercial modular carpet tiles grounding West Dev Openspace, East Archive Wing & Full North Wing
        mesh.Add(new(-24.2f,.011f,-9.5f),new(22.4f,.018f,53.0f),new(54,57,66,255),Finish.Carpet);
        mesh.Add(new( 24.2f,.011f,-9.5f),new(22.4f,.018f,53.0f),new(62,56,50,255),Finish.Carpet);
        mesh.Add(new(0f,.011f,-30.5f),new(26.0f,.018f,12.2f),new(52,55,62,255),Finish.Carpet);
        // Subtle corporate woven aisle runners separating the openspace bench rows (replaces barren uniform slab!)
        foreach (float ax in new[] { -18.65f, -24.85f, 19.45f })
            mesh.Add(new(ax, .014f, -9.5f), new(1.75f, .014f, 49.0f), new(68, 64, 62, 255), Finish.Carpet);
        // Refined corporate bordered area rugs under every openspace scrum table (replaces flat salmon decal!)
        BorderedRug(-18.65f,  8.35f, 3.4f, 3.2f, new(66, 36, 42, 255), darkBrass);
        BorderedRug(-24.85f,  1.80f, 3.2f, 3.2f, new(44, 56, 66, 255), darkBrass);
        BorderedRug(-18.65f, -5.00f, 3.4f, 3.2f, new(58, 52, 46, 255), darkBrass);
        BorderedRug( 19.45f,  7.00f, 3.4f, 3.2f, new(68, 42, 38, 255), darkBrass);
        BorderedRug( 19.45f, -7.00f, 3.4f, 3.2f, new(46, 54, 68, 255), darkBrass);

        // Connected 4-seat (2x2) Corporate Bench Center Spines, Frosted Privacy Screens & Suspended Linear Office Lights!
        Color walnutFrame = new(68, 44, 28, 255), frostGlass = new(168, 192, 202, 54);
        Color feltSpine = new(62, 66, 74, 255), warmLamp = new(226, 182, 126, 255);
        void BuildBenchIslandHardware(float midX, float midZ, float spanW)
        {
            // Shared walnut & acoustic-felt center spine + brass cap + low frosted-glass divider
            mesh.Add(new(midX, 0.96f, midZ), new(spanW, 0.84f, 0.11f), feltSpine, Finish.Carpet);
            mesh.Add(new(midX, 0.56f, midZ), new(spanW + 0.06f, 0.08f, 0.14f), walnutFrame, Finish.Leather);
            mesh.Add(new(midX, 1.38f, midZ), new(spanW + 0.06f, 0.05f, 0.14f), brass, Finish.Bronze);
            mesh.Add(new(midX, 1.535f, midZ), new(spanW - 0.12f, 0.27f, 0.04f), frostGlass, Finish.Glass);
            mesh.Add(new(midX, 1.685f, midZ), new(spanW + 0.06f, 0.032f, 0.07f), brass, Finish.Bronze);
            foreach (int endS in new[] { -1, 1 })
            {
                float ex = midX + endS * (spanW * 0.5f + 0.03f);
                mesh.Add(new(ex, 0.835f, midZ), new(0.08f, 1.67f, 0.20f), walnutFrame, Finish.Leather);
                mesh.Add(new(ex, 1.685f, midZ), new(0.105f, 0.036f, 0.23f), brass, Finish.Bronze);
            }
            // Warm suspended linear walnut & brass architectural office light bar directly over the bench island!
            float lightY = 3.95f;
            float lightW = MathF.Max(1.9f, spanW - 0.35f);
            mesh.Add(new(midX, lightY, midZ), new(lightW, 0.12f, 0.22f), walnutFrame, Finish.Leather);
            mesh.Add(new(midX, lightY - 0.062f, midZ), new(lightW - 0.12f, 0.025f, 0.14f), warmLamp, Finish.Fluorescent);
            mesh.Add(new(midX, lightY + 0.065f, midZ), new(lightW + 0.04f, 0.025f, 0.24f), brass, Finish.Bronze);
            foreach (int rodS in new[] { -1, 1 })
                mesh.Add(new(midX + rodS * (lightW * 0.38f), (lightY + CeilingHeight) * 0.5f, midZ),
                    new(0.025f, CeilingHeight - lightY, 0.025f), darkBrass, Finish.Bronze);
        }
        // West 4-seat (2x2) & 2-seat bench islands (including dense North-West Wing rows!)
        foreach (float rz in new[] { 12.0f, 5.5f, -1.5f, -8.5f, -24.5f, -31.0f })
        {
            float spineZ = rz - 0.535f;
            BuildBenchIslandHardware(-15.545f, spineZ, 4.18f);
            BuildBenchIslandHardware(-21.745f, spineZ, 4.18f);
            BuildBenchIslandHardware(-26.90f, spineZ, 2.08f);
        }
        // East 4-seat (2x2) & 2-seat bench islands (including dense North-East Wing rows!)
        foreach (float rz in new[] { 10.5f, 3.5f, -3.5f, -10.5f, -24.5f, -31.0f })
        {
            float spineZ = rz - 0.535f;
            bool singleInner = rz is 3.5f;
            BuildBenchIslandHardware(singleInner ? 14.80f : 15.845f, spineZ, singleInner ? 2.08f : 4.18f);
            BuildBenchIslandHardware(23.045f, spineZ, 4.18f);
        }

        // Built-in Walnut Corporate Bookshelves & Archival Credenzas along the openspace faces of the X=±9.6 partition walls!
        Color[] shelfBinders = [new(146, 52, 46, 255), new(46, 76, 108, 255), new(168, 128, 56, 255), new(54, 96, 72, 255), new(188, 174, 146, 255)];
        foreach (int side in new[] { -1, 1 })
        foreach (var (zMid, len) in new (float, float)[] { (-3.0f, 4.4f), (9.85f, 3.4f) })
        {
            float bx = side * 9.94f;
            mesh.Add(new(bx, 0.56f, zMid), new(0.42f, 1.12f, len), walnutFrame, Finish.Leather);
            mesh.Add(new(bx, 0.05f, zMid), new(0.46f, 0.10f, len + 0.04f), darkBrass, Finish.Bronze);
            mesh.Add(new(bx, 1.15f, zMid), new(0.48f, 0.06f, len + 0.06f), new(38, 36, 40, 255), Finish.BlackMarble);
            int books = (int)(len * 6f);
            for (int b = 0; b < books; b++)
            {
                if (b % 7 == 3) continue; // Natural gaps between binder clusters
                float bz = zMid - len * 0.44f + b * (len * 0.88f / books);
                float bh = 0.24f + (b % 3) * 0.04f;
                mesh.Add(new(bx + side * 0.04f, 1.18f + bh * 0.5f, bz), new(0.24f, bh, 0.075f), shelfBinders[(b + (side > 0 ? 2 : 0)) % shelfBinders.Length]);
            }
        }

        // Sculpted Walnut & Frosted-Glass Office Cubicle Partitions framing window bays (skip Z=-3..+5 reserved for Executive Glass Meeting Suites!)
        foreach (float rz in new[] { 12.5f, -10.5f, -18.5f, -26.5f, -33.0f })
        {
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 divPos = new(side * 29.2f, 0f, rz + 1.0f);
                mesh.Add(divPos + new Vector3(0f, 0.56f, 0f), new(0.10f, 1.12f, 3.20f), walnutFrame, Finish.Leather);
                mesh.Add(divPos + new Vector3(0f, 1.38f, 0f), new(0.05f, 0.50f, 3.12f), frostGlass, Finish.Glass);
                mesh.Add(divPos + new Vector3(0f, 1.65f, 0f), new(0.12f, 0.05f, 3.24f), brass, Finish.Bronze);
                foreach (float dz in new[] { -1.58f, 1.58f })
                    mesh.Add(divPos + new Vector3(0f, 0.84f, dz), new(0.12f, 1.68f, 0.10f), walnutFrame, Finish.Leather);
            }
        }
        // Executive Glass Meeting Suite doorway lintels (clear 2.8m entryways at X=±30, Z=-0.4..+2.4)
        foreach (int side in new[] { -1, 1 })
        {
            mesh.Add(new(side * 30.0f, 3.09f, 1.0f), new(0.14f, 0.12f, 2.90f), new(48, 58, 67, 255), Finish.Slate);
            mesh.Add(new(side * 30.0f, 2.98f, 1.0f), new(0.16f, 0.045f, 2.92f), brass, Finish.Bronze);
        }
        // Overhead architectural walnut & brass ceiling coffers framing West, East & North Wing corridors!
        foreach (float bz in new[] { 16.0f, 6.0f, -4.0f, -14.0f, -24.0f, -32.0f })
        {
            foreach (int side in new[] { -1, 1 })
            {
                mesh.Add(new(side * 23.5f, CeilingHeight - 0.24f, bz), new(24.0f, 0.36f, 0.42f), walnutFrame, Finish.Leather);
                mesh.Add(new(side * 23.5f, CeilingHeight - 0.43f, bz), new(24.0f, 0.05f, 0.46f), brass, Finish.Bronze);
            }
        }
        foreach (float nbz in new[] { -24.0f, -32.0f })
        {
            mesh.Add(new(0f, CeilingHeight - 0.24f, nbz), new(24.0f, 0.36f, 0.42f), walnutFrame, Finish.Leather);
            mesh.Add(new(0f, CeilingHeight - 0.43f, nbz), new(24.0f, 0.05f, 0.46f), brass, Finish.Bronze);
        }
        // Sculpted beveled brushed-brass edging rails & transverse runner threshold bars!
        foreach (float barZ in new[] { start + .06f, start + 3.45f, start + 7.20f, start + 10.60f, 12.95f, end - .04f })
            mesh.Add(new(0,.027f,barZ),new(4.84f,.009f,.042f),brass,Finish.Bronze);
        foreach (int side in new[] { -1,1 })
        {
            mesh.Add(new(side*2.42f,.018f,center),new(.095f,.030f,end-start+.06f),darkBrass,Finish.Bronze);
            foreach (float edge in new[] { 2.24f,2.38f })
                mesh.Add(new(side*edge,.029f,center),new(.026f,.009f,end-start),brass,Finish.Bronze);
            // Subtle inlaid brushed-brass architectural floor zoning strips (replaces glowing Tron neon floor lasers!)
            mesh.Add(new(side*6.35f,.015f,3.3f),new(9.5f,.012f,.055f),darkBrass,Finish.Bronze);
            mesh.Add(new(side*11.1f,.015f,-10),new(.055f,.012f,26.65f),darkBrass,Finish.Bronze);
            foreach (int z in new[] { -19, 17 })
            {
                mesh.Add(new(side*17,.016f,z),new(3.6f,.016f,.18f),new(54,50,46,255));
                for (int i=-3;i<=3;i++)
                    mesh.Add(new(side*17+i*.46f,.026f,z),new(.14f,.006f,.13f),darkBrass,Finish.Bronze);
            }
        }
        BuildOfficeWiring(mesh);
        foreach (var prop in OfficeProps)
        {
            if (prop.Kind==OfficePropKind.MeetingGlass)
            {
                Vector3 size=prop.Size;
                bool alongX=size.X>size.Z;
                mesh.Add(prop.Position+Vector3.UnitY*(size.Y*.5f),size,new(135,205,220,42),Finish.Glass);
                foreach (float y in new[] { .07f,1.17f,3.09f })
                    mesh.Add(prop.Position+Vector3.UnitY*y,new(size.X+.013f,y==1.17f ? .045f : .09f,size.Z+.013f),
                        y==1.17f ? brass : new(48,58,67,255));
                for (int i=-1;i<=1;i++)
                {
                    Vector3 offset=alongX ? new(size.X*.49f*i,0,0) : new(0,0,size.Z*.49f*i);
                    mesh.Add(prop.Position+offset+Vector3.UnitY*1.55f,new(.065f,3.1f,.065f),new(57,66,74,255));
                }
            }
        }
    }

    // Shared solid footprint: rendering and player collision use exactly the same wall bays.
    private float barrierContact;
    internal void CheckGateContact(Vector3 desired,Vector3 actual,float dt)
    {
        barrierContact=Math.Max(0,barrierContact-dt);
        if(Systems.WingsUnlocked || Vector2.DistanceSquared(new(actual.X,actual.Z),new(desired.X,desired.Z))<.000001f) return;
        foreach(var (feet,side) in Gates)
        {
            var d=actual-feet;
            if(Math.Abs(side ? d.Z : d.X)<=1.9f && Math.Abs(side ? d.X : d.Z)<.54f) { barrierContact=.3f; break; }
        }
    }
    private string ReceptionTicker()
    {
        var role=(CoderArchetype)((int)(_worldClock/5)%4);
        return ((int)(_worldClock/2.5f)%2)==0 ? Loc.Tr("HR Вероника") : FloorSystems.VibeCheck(role);
    }

    private string BarrierHint=>$"КАРАНТИН КРЫЛА СБ — СДАЙ {Systems.Balance.QuarantineUnlockTarget} ВАЙБКОДЕРОВ ВЕРОНИКЕ ({Math.Min(TotalCodersSoldToHr,Systems.Balance.QuarantineUnlockTarget)}/{Systems.Balance.QuarantineUnlockTarget})";
    internal static readonly (Vector3 Feet,bool Side)[] Gates=
        (from side in new[]{-1,1} from z in new[]{-11f,5.2f,14.5f} select (new Vector3(side*9.6f,0,z),true))
        .Concat(new[]{(new Vector3(-6,0,-15.8f),false),(new Vector3(6,0,-15.8f),false)}).ToArray();

    private void DrawWingGates()
    {
        float slide=Systems.WingBarrierSlide;
        foreach(var (feet,side) in Gates)
        {
            void Panel(float across,float y,float width,float height,float depth,Color ink,Finish finish)
                => VisualEngine3D.Box(feet+(side?Vector3.UnitZ:Vector3.UnitX)*across+Vector3.UnitY*y,
                    side?depth:width,height,side?width:depth,ink,finish);
            Color brass=new(157,119,73,255), darkBronze=new(68,52,34,255);
            // Understated Art-Deco brushed-bronze security badge plaques with subtle status jewel (replaces huge neon-mint slabs!)
            foreach(float jamb in new[]{-1.86f,1.86f})
            {
                Panel(jamb,2.24f,.048f,.20f,.14f,darkBronze,Finish.Bronze);
                Panel(jamb,2.24f,.028f,.055f,.055f,Systems.WingsUnlocked?new(86,188,128,255):new(232,126,54,255),Finish.Lamp);
            }
            if(slide>=.99f) continue;
            float bottom=slide*3.1f;
            Panel(0,(3.2f+bottom)/2,3.76f,3.2f-bottom,.19f,new(56,77,82,185),Finish.Glass);
            Panel(0,bottom+.05f,3.76f,.1f,.24f,brass,Finish.Bronze);
            if(slide>0)
                for(int i=0;i<7;i++)
                {
                    float phase=(slide*4+i*.37f)%1;
                    Panel((i%2==0?-1:1)*(1.81f-phase*.17f),bottom+.27f-phase*.25f,.02f,.045f,.31f,new(255,192,88,255),Finish.Lamp);
                }
        }
    }

    private static IEnumerable<(Vector3 Center, Vector3 Size)> InteriorWallBays()
    {
        yield return (new(0,2.1f,20),new(19.2f,4.2f,.24f));
        yield return (new(0,2.3f,15.6f),new(5.6f,4.6f,.32f));
        // Left reception glass sidelight (behind ArsenalShop & Veronika's left flank)
        yield return (new(-5.15f,2.3f,16.35f),new(4.7f,4.6f,.16f));
        // Right reception Technical Wall piers framing flush 2.5m Staff-Only portals at X=4.10 & X=7.45 (Z=16.25)
        yield return (new(5.775f,2.3f,16.35f),new(.85f,4.6f,.24f));
        yield return (new(9.09f,2.3f,16.35f),new(.78f,4.6f,.24f));
        foreach(int side in new[]{-1,1})
        {
            // 3.8 m clear portals centred at -11, 5.2 and 14.5 metres.
            foreach(var span in new[]{(-15.8f,-12.9f),(-9.1f,3.3f),(7.1f,12.6f),(16.4f,20f)})
                yield return (new(side*9.6f,2.1f,(span.Item1+span.Item2)/2),new(.24f,4.2f,span.Item2-span.Item1));
            // Split Z=-15.8 architectural wall cleanly around flush 2.5m wing portals at X=±17.8 (16.55..19.05)
            yield return (new(side*12.225f,2.1f,-15.8f),new(8.65f,4.2f,.24f));
            yield return (new(side*19.525f,2.1f,-15.8f),new(.95f,4.2f,.24f));
            yield return (new(side*14.8f,2.1f,20),new(10.4f,4.2f,.24f));
        }
        // Central North Partition Bay (0, 2.1, -15.8) 8.2x4.2x0.24 is rendered & collided dynamically so Boss opening rockets blow it apart!
    }

    private void BuildEntryRooms(VisualEngine3D.ArchitectureBuilder mesh)
    {
        Color warmConcrete = new(226, 222, 216, 255), travertine = new(238, 232, 222, 255);
        Color creamOchre = new(236, 214, 176, 255), serviceTile = new(212, 198, 176, 255);
        Color satinSteel = new(212, 216, 220, 255), breakerGrey = new(216, 214, 208, 255), conduitSteel = new(156, 154, 150, 255);
        Color warmWalnut = new(162, 114, 76, 255), darkAlum = new(56, 60, 66, 255), warmLamp = new(255, 240, 206, 255);
        Color brass = new(166, 130, 74, 255);
        for(int i=0;i<RoomDoors.Length;i++)
        {
            var d=RoomDoors[i];
            float s=RoomExitSign(i); // -1 = South Reception Technical Wall (recess goes +Z); +1 = North Wing Wall (recess goes -Z)
            bool tech = i < 2;
            void Part(float x,float y,float z,float w,float h,float depth,Color color,Finish finish=Finish.Paint) =>
                mesh.Add(d+new Vector3(x,y,z*s),new(w,h,depth),color,finish);

            if (tech)
            {
                // WARM-LIT CREAM-OCHRE TILED SERVICE CORRIDOR INSIDE (replaces pitch-black void!)
                Part(0, 1.48f, -2.10f, 2.54f, 2.96f, .16f, creamOchre, Finish.Plaster);
                Part(0, 2.94f, -1.05f, 2.56f, .14f, 2.24f, new(224, 214, 196, 255), Finish.Plaster);
                // Warm beige ceramic tiled service floor + crisp plinth skirting
                Part(0, .016f, -1.05f, 2.48f, .022f, 2.18f, serviceTile, Finish.Plaster);
                Part(0, .08f, -2.01f, 2.44f, .14f, .04f, new(124, 114, 102, 255));
                // Two glowing warm linear overhead ceiling light fixtures inside the service corridor
                foreach (float lz in new[] { -.68f, -1.48f })
                {
                    Part(0, 2.86f, lz, .22f, .045f, .52f, satinSteel, Finish.Armor);
                    Part(0, 2.82f, lz, .16f, .032f, .46f, warmLamp, Finish.Lamp);
                }
                // Wall-mounted off-white/light-grey steel electrical breaker cabinets + yellow caution badge + vertical steel conduits!
                Part(-.46f, 1.44f, -1.96f, .56f, .76f, .14f, breakerGrey, Finish.Armor);
                Part(-.46f, 1.44f, -1.885f, .50f, .70f, .015f, new(184, 182, 176, 255));
                Part(-.25f, 1.44f, -1.87f, .03f, .11f, .025f, darkAlum);
                Part(.42f, 1.52f, -1.96f, .64f, .82f, .14f, breakerGrey, Finish.Armor);
                Part(.42f, 1.52f, -1.885f, .58f, .76f, .015f, new(188, 186, 180, 255));
                Part(.42f, 1.58f, -1.87f, .085f, .085f, .012f, new(234, 190, 52, 255));
                // 4 neat vertical galvanized steel electrical conduit pipes rising from the breaker cabinet to the ceiling
                foreach (float cx in new[] { .26f, .35f, .44f, .53f })
                    Part(cx, 2.40f, -1.97f, .034f, 1.04f, .034f, conduitSteel, Finish.Armor);
                Part(-.04f, 1.98f, -1.97f, .96f, .036f, .036f, conduitSteel, Finish.Armor);
                // Side plastered walls + baseboard
                foreach (int side in new[] { -1, 1 })
                {
                    Part(side * 1.22f, 1.48f, -1.05f, .14f, 2.96f, 2.12f, creamOchre, Finish.Plaster);
                    Part(side * 1.14f, .08f, -1.05f, .03f, .14f, 2.04f, new(124, 114, 102, 255));
                }

                // DEEPLY RECESSED ARCHITECTURAL BOARD-FORMED CONCRETE PORTAL FRAME (0.42m deep reveal — exact match to reference!)
                Color recessConcrete = new(184, 180, 174, 255);
                foreach (int side in new[] { -1, 1 })
                {
                    Part(side * 1.17f, 1.44f, .16f, .34f, 2.88f, .42f, warmConcrete, Finish.Slate);
                    // Subtle shadowed inner concrete jamb return + sleek brushed-stainless door frame
                    Part(side * 1.015f, 1.24f, .18f, .032f, 2.48f, .34f, recessConcrete, Finish.Slate);
                    Part(side * 0.995f, 1.23f, .03f, .055f, 2.46f, .12f, new(126, 130, 136, 255), Finish.Armor);
                }
                // Massive concrete portal lintel header + shadowed concrete soffit + brushed-stainless inner top transom
                Part(0, 2.66f, .16f, 2.68f, .44f, .42f, warmConcrete, Finish.Slate);
                Part(0, 2.455f, .18f, 2.04f, .032f, .34f, recessConcrete, Finish.Slate);
                Part(0, 2.455f, .03f, 2.06f, .055f, .12f, new(126, 130, 136, 255), Finish.Armor);
                // Bright warm honed travertine / cream limestone upper wall panel above the concrete portal (Y = 2.88..4.60m)
                Part(0, 3.74f, -.04f, 2.68f, 1.72f, .24f, travertine, Finish.Plaster);
            }
            else
            {
                // North Wing Coworking / Meeting Room recessed portal with warm walnut interior & concrete portal frame
                Part(0, 1.48f, -2.10f, 2.54f, 2.96f, .16f, travertine, Finish.Plaster);
                Part(0, 2.94f, -1.05f, 2.56f, .14f, 2.24f, warmWalnut, Finish.Leather);
                Part(0, 2.82f, -1.45f, 1.88f, .03f, .10f, warmLamp, Finish.Lamp);
                Part(0, .74f, -1.72f, 2.05f, .07f, .54f, warmWalnut, Finish.Leather);
                foreach (int leg in new[] { -1, 1 })
                    Part(leg * .92f, .36f, -1.72f, .06f, .72f, .46f, darkAlum);
                Part(0, 1.08f, -1.78f, .74f, .44f, .07f, darkAlum);
                foreach (int side in new[] { -1, 1 })
                {
                    Part(side * 1.22f, 1.48f, -1.05f, .14f, 2.96f, 2.12f, warmWalnut, Finish.Leather);
                    Part(side * 1.15f, 1.44f, .10f, .30f, 2.88f, .34f, warmConcrete, Finish.Slate);
                    Part(side * 1.005f, 1.23f, .03f, .055f, 2.46f, .10f, satinSteel, Finish.Armor);
                }
                Part(0, 2.66f, .10f, 2.60f, .44f, .34f, warmConcrete, Finish.Slate);
                Part(0, 3.54f, -.04f, 2.60f, 1.32f, .24f, travertine, Finish.Plaster);
            }
        }
        // Built-in warm natural walnut & honed black-marble hospitality credenza console at VentMouth(0)
        var cabinet=VentMouth(0)+new Vector3(0,.64f,-.44f);
        mesh.Add(cabinet,new(1.20f,1.22f,.88f),warmWalnut,Finish.Leather);
        mesh.Add(cabinet+new Vector3(0,-.58f,0),new(1.24f,.10f,.92f),darkAlum,Finish.Armor);
        mesh.Add(cabinet+new Vector3(0,.63f,0),new(1.28f,.07f,.94f),new(34,32,36,255),Finish.BlackMarble);
        mesh.Add(cabinet+new Vector3(0,.58f,0),new(1.24f,.04f,.91f),brass,Finish.Bronze);
    }

    private void DrawEntryHardware()
    {
        Color steel = new(52, 59, 68, 255);
        Color satinSteel = new(144, 148, 154, 255), brushedKick = new(130, 134, 140, 255), handleSteel = new(212, 216, 222, 255);
        Color darkSeam = new(54, 56, 62, 255), readerBlack = new(26, 28, 32, 255);
        Color frostGlass = new(216, 212, 206, 198);
        for(int i=0;i<RoomDoors.Length;i++)
        {
            var d=RoomDoors[i];
            float s=RoomExitSign(i);
            bool tech = i < 2;
            void Part(float x,float y,float z,float w,float h,float depth,Color color,Finish finish=Finish.Paint) =>
                VisualEngine3D.Box(d+new Vector3(x,y,z*s),w,h,depth,color,finish);
            float t=RoomOpen[i];
            float gap=t*t*(3-2*t);
            // Clean minimalist brushed-aluminum architectural room plaque (flat FaceBox for razor-sharp 1:1 engraved lettering!)
            VisualEngine3D.FaceBox(d+new Vector3(0, 2.66f, (tech ? .382f : .282f)*s), 1.38f, .27f, .018f, 0f, Color.White, Finish.Coworking+i);
            if (tech)
            {
                bool active = RoomOpen[i] > .01f;
                // Minimalist brushed-aluminum architectural door badge + black RFID keycard reader on the right concrete pier (player's right = -X)
                Part(-1.17f, 1.76f, .380f, .24f, .15f, .014f, new(186, 190, 196, 255), Finish.Armor);
                Part(-1.17f, 1.32f, .384f, .090f, .155f, .028f, readerBlack);
                Part(-1.17f, 1.28f, .399f, .018f, .018f, .008f, active ? new(102, 248, 162, 255) : new(68, 218, 126, 255), Finish.Lamp);
                // Red wall-mounted fire-alarm pull station ("ПОЖАР") mounted on Door 0's left concrete pier beside the coat rack!
                if (i == 0)
                {
                    Part(1.17f, 1.44f, .385f, .14f, .14f, .036f, new(192, 34, 38, 255));
                    Part(1.17f, 1.44f, .405f, .070f, .054f, .008f, new(238, 234, 228, 255));
                    Part(1.17f, 1.44f, .410f, .048f, .014f, .006f, new(42, 44, 48, 255));
                }
            }
            else
            {
                Part(-1.15f, 1.32f, .282f, .090f, .145f, .026f, readerBlack);
                Part(-1.15f, 1.28f, .296f, .018f, .018f, .008f, RoomOpen[i]>.01f ? new(82, 242, 152, 255) : new(240, 178, 72, 255), Finish.Lamp);
                foreach(int ch in new[]{-1,1})
                    VisualEngine3D.Asset("prop_office_chair", d+new Vector3(ch*.64f,0,-s*1.28f), (s<0 ? 0f : 180f)+ch*16f, .92f);
            }
            foreach(int side in new[]{-1,1})
            {
                float x=side*(.50f+.90f*gap);
                if (tech)
                {
                    // REAL BRUSHED-STAINLESS & FROSTED-GLASS ARCHITECTURAL DOUBLE DOORS (crisp steel-vs-frosted-glass contrast matching reference!)
                    // Left & right vertical satin stainless stiles + crisp dark center meeting gasket
                    Part(x - .445f, 1.22f, .03f, .092f, 2.42f, .054f, satinSteel, Finish.Armor);
                    Part(x + .445f, 1.22f, .03f, .092f, 2.42f, .054f, satinSteel, Finish.Armor);
                    Part(x - side * .492f, 1.22f, .032f, .012f, 2.42f, .056f, darkSeam);
                    // Top rail, wide horizontal brushed-stainless mid push-rail, and bottom stainless kickplate
                    Part(x, 2.37f, .03f, .98f, .110f, .054f, satinSteel, Finish.Armor);
                    Part(x, 0.96f, .034f, .98f, .26f, .058f, satinSteel, Finish.Armor);
                    Part(x, 0.16f, .034f, .98f, .31f, .058f, brushedKick, Finish.Armor);
                    // Subtle dark anodized glass glazing beads framing the frosted panes
                    Part(x, 1.095f, .034f, .82f, .014f, .042f, darkSeam);
                    Part(x, 0.825f, .034f, .82f, .014f, .042f, darkSeam);
                    // Large translucent frosted architectural glass upper & lower panes
                    Part(x, 1.70f, .03f, .81f, 1.22f, .018f, frostGlass, Finish.Glass);
                    Part(x, 0.575f, .03f, .81f, 0.51f, .018f, frostGlass, Finish.Glass);
                    // Realistic horizontal satin brushed-stainless lever handles meeting cleanly at the center seam
                    float hx = x - side * .41f;
                    Part(hx, 1.15f, .064f, .044f, .044f, .016f, handleSteel, Finish.Armor);
                    Part(hx + side * .060f, 1.15f, .098f, .145f, .024f, .024f, handleSteel, Finish.Armor);
                    Part(hx, 1.15f, .080f, .020f, .020f, .036f, satinSteel, Finish.Armor);
                }
                else
                {
                    Part(x - .455f, 1.22f, .03f, .070f, 2.42f, .046f, satinSteel, Finish.Armor);
                    Part(x + .455f, 1.22f, .03f, .070f, 2.42f, .046f, satinSteel, Finish.Armor);
                    Part(x, 2.38f, .03f, .98f, .085f, .046f, satinSteel, Finish.Armor);
                    Part(x, 0.12f, .03f, .98f, .22f, .048f, brushedKick, Finish.Armor);
                    Part(x, 1.28f, .03f, .84f, 2.12f, .018f, new(156, 198, 206, 92), Finish.Glass);
                    Part(x, 1.04f, .048f, .84f, .16f, .012f, frostGlass, Finish.Glass);
                    Part(x - side * .37f, 1.14f, .082f, .12f, .022f, .022f, satinSteel, Finish.Armor);
                }
            }
        }
        for(int i=0;i<VentTime.Length;i++)
        {
            var p=VentMouth(i);
            bool lobbyGrille = i == 0;
            Color frameCol = lobbyGrille ? new(94, 72, 42, 255) : steel;
            Finish frameFin = lobbyGrille ? Finish.Bronze : Finish.Paint;
            VisualEngine3D.Box(p+new Vector3(0,.25f,lobbyGrille ? .004f : .012f),.92f,.48f,lobbyGrille ? .020f : .042f,frameCol,frameFin);
            VisualEngine3D.Box(p+new Vector3(0,.25f,lobbyGrille ? .012f : .041f),.84f,.40f,lobbyGrille ? .016f : .035f,lobbyGrille ? new(22,18,16,255) : new(22,58,48,255));
            float angle=Math.Clamp(Math.Min((.83f-VentTime[i])/.14f,VentTime[i]/.22f),0,1)*1.38f;
            var hinge=p+new Vector3(0,.46f,lobbyGrille ? .022f : .07f);
            for(int j=0;j<6;j++)
            {
                float drop=.035f+j*.064f;
                var slat=hinge+new Vector3(0,-drop*MathF.Cos(angle),drop*MathF.Sin(angle));
                VisualEngine3D.HingedLouver(slat,-angle*180/MathF.PI,lobbyGrille);
                if(!lobbyGrille)
                    VisualEngine3D.Box(hinge+new Vector3(0,-drop-.025f,-.008f),.76f,.008f,.01f,new(79,219,142,255),Finish.Lamp);
            }
        }
    }

    private static void BuildInteriorRooms(VisualEngine3D.ArchitectureBuilder mesh)
    {
        Color warmConcrete = new(216, 210, 200, 255), travertine = new(238, 232, 222, 255);
        Color warmWalnut = new(166, 118, 78, 255), feltDark = new(28, 26, 25, 255);
        Color darkAlum = new(56, 60, 66, 255), glass = new(142, 174, 184, 96);
        foreach(var bay in InteriorWallBays())
        {
            Vector3 c=bay.Center,s=bay.Size;
            bool reception=c.Z is >15 and <17 && MathF.Abs(c.X)<9.5f;
            bool techPier=c.Z is >16 and <17 && c.X>3f;
            bool alongZ=s.Z>s.X;
            if(reception && c.X==0)
            {
                // Warm honed travertine limestone backdrop + thick natural walnut acoustic wood-slat bays over charcoal felt (ZERO dark charcoal box + gold sticks!)
                mesh.Add(c, s, travertine, Finish.Plaster);
                foreach (int side in new[] { -1, 1 })
                {
                    float bayCenter = side * 2.12f;
                    mesh.Add(new(bayCenter, 2.30f, 15.428f), new(1.32f, 4.54f, .028f), feltDark, Finish.Carpet);
                    for (int k = 0; k < 14; k++)
                    {
                        float rx = side * (1.50f + k * .094f);
                        mesh.Add(new(rx, 2.30f, 15.396f), new(.048f, 4.54f, .048f), warmWalnut, Finish.Leather);
                    }
                }
                // Warm natural walnut shadow frame around the ARCANA artwork
                mesh.Add(new(0, 3.25f, 15.36f), new(2.08f, 1.74f, .12f), new(98, 68, 46, 255), Finish.Leather);
            }
            else if(techPier)
            {
                // Full-height Warm Natural Walnut Acoustic Wood-Slat Paneling flanking & separating the concrete technical portals!
                mesh.Add(c, s, feltDark, Finish.Carpet);
                float frontZ = c.Z - s.Z * .5f - .022f;
                int slatCount = Math.Max(6, (int)MathF.Floor(s.X / .088f));
                float step = s.X / slatCount;
                for (int k = 0; k < slatCount; k++)
                {
                    float sx = c.X - s.X * .5f + (k + .5f) * step;
                    mesh.Add(new(sx, 2.30f, frontZ), new(.048f, s.Y - .02f, .052f), warmWalnut, Finish.Leather);
                }
            }
            else
            {
                float h=reception ? .5f : 1.18f;
                // Warm architectural board-formed concrete wainscoting + clean dark-anodized aluminum glass mullions (ZERO gold zebra wireframes!)
                mesh.Add(new(c.X, h / 2, c.Z), new(s.X, h, s.Z), warmConcrete, Finish.Slate);
                mesh.Add(new(c.X, (h + s.Y) / 2, c.Z), new(s.X, s.Y - h, s.Z), glass, Finish.Glass);
                mesh.Add(new(c.X, .06f, c.Z), new(s.X + .025f, .11f, s.Z + .025f), darkAlum, Finish.Armor);
                mesh.Add(new(c.X, h, c.Z), new(s.X + .040f, .060f, s.Z + .040f), warmWalnut, Finish.Leather);
                mesh.Add(new(c.X, s.Y - .08f, c.Z), new(s.X + .030f, .075f, s.Z + .030f), darkAlum, Finish.Armor);
                float length=alongZ?s.Z:s.X;
                int count=(int)MathF.Ceiling(length/1.65f);
                for(int i=0;i<=count;i++)
                {
                    float offset=-length/2+i*length/count;
                    mesh.Add(c+new Vector3(alongZ?0:offset,0,alongZ?offset:0),new(.058f,s.Y,.058f),darkAlum,Finish.Armor);
                }
            }
        }
        void Portal(Vector3 p,bool alongZ)
        {
            Vector3 Size(float w,float h,float d)=>alongZ?new(d,h,w):new(w,h,d);
            foreach(int side in new[]{-1,1})
                mesh.Add(p+new Vector3(alongZ?0:side*1.99f,1.6f,alongZ?side*1.99f:0),Size(.18f,3.2f,.40f),warmWalnut,Finish.Leather);
            mesh.Add(p+Vector3.UnitY*3.30f,Size(4.16f,.20f,.40f),warmConcrete,Finish.Slate);
            mesh.Add(p+Vector3.UnitY*3.80f,Size(3.8f,.80f,.12f),glass,Finish.Glass);
            mesh.Add(p+Vector3.UnitY*4.16f,Size(4.16f,.08f,.40f),darkAlum,Finish.Armor);
        }
        foreach(var gate in Gates) Portal(gate.Feet,gate.Side);
        // Warm natural walnut reception & technical gallery ceiling beam spanning full South lobby wall + soft warm recessed cove light
        mesh.Add(new(1.0f, 4.54f, 15.6f), new(17.2f, .18f, .65f), warmWalnut, Finish.Leather);
        mesh.Add(new(1.0f, 4.43f, 15.25f), new(16.8f, .028f, .028f), new(255, 224, 174, 255), Finish.Lamp);
    }

    private static void BuildCurtainWallsAndSkyline(VisualEngine3D.ArchitectureBuilder mesh)
    {
        Color steel=new(53,68,81,255), aluminum=new(111,127,139,255);
        Color[] lights=[new(76,216,246,255),new(255,181,72,255),new(233,92,174,255),new(246,226,190,255)];
        var urban=new Random(1994);
        mesh.Add(new(0,-52.5f,0),new(340,1,340),new(18,22,32,255),Finish.Slate);
        foreach(int road in new[]{-132,-96,-56,-43,43,56,96,132})
        foreach(bool eastWest in new[]{true,false})
        {
            void Road(float along,float across,float y,float length,float width,Color color,Finish finish)
            {
                var p=eastWest ? new Vector3(along,y,road+across) : new Vector3(road+across,y,along);
                mesh.Add(p,eastWest ? new(length,.08f,width) : new(width,.08f,length),color,finish);
            }
            int span=Math.Abs(road)==43 ? 92 : 308;
            Road(0,0,-51.85f,span,6.5f,new(30,32,39,255),Finish.Slate);
            foreach(int side in new[]{-1,1})
            {
                Road(0,side*3.22f,-51.73f,span,.16f,new(255,178,82,255),Finish.Lamp);
                for(int u=4-span/2;u<span/2;u+=8)
                {
                    Road(u,side*.35f,-51.68f,2,.09f,new(165,210,245,255),Finish.Lamp);
                    if((u+road+side)%3!=0) continue;
                    float car=u+side*2.2f;
                    foreach(float track in new[]{1.3f,1.9f})
                        Road(car,side*track,-51.55f,.26f,.16f,side>0 ? new(242,86,65,255) : new(233,223,197,255),Finish.Lamp);
                }
            }
        }
        static float ClearAvenue(float coord,float width)
        {
            float dist=Math.Abs(coord),margin=3.5f+width*.5f;
            foreach(float center in new[]{56f,96f,132f})
                if(Math.Abs(dist-center)<margin) dist=center+(dist<center ? -margin : margin);
            return MathF.CopySign(dist,coord);
        }
        // A twisting tower and two offset crowns break the low curtain-wall skyline.
        Vector3 tower=new(-78,0,-5);
        for(int tier=-8;tier<17;tier++)
        {
            float y=-25+3.4f*tier, yaw=tier*.049f;
            mesh.Add(tower+Vector3.UnitY*y,new(11.8f,3.42f,8.6f),lights[0],Finish.Facade,yaw);
            mesh.Add(tower+Vector3.UnitY*(y+1.68f),new(11.92f,.12f,8.72f),new(99,116,128,255),Finish.Bronze,yaw);
        }
        for(int wing=0;wing<2;wing++)
        {
            Vector3 mast=new(-70+wing*8,0,-40);
            float roof=19.5f-wing*4;
            mesh.Add(mast+Vector3.UnitY*((roof-52)/2),new(9,52+roof,10),lights[1],Finish.Facade);
            for(int ledge=0;ledge<5;ledge++)
            {
                float w=8.3f-ledge*1.2f;
                Vector3 crown=mast+new Vector3(ledge*.65f,21-wing*4+2.8f*ledge,0);
                mesh.Add(crown,new(w,2.84f,9.5f),lights[1],Finish.Facade);
                mesh.Add(crown+Vector3.UnitY*1.44f,new(w,.075f,9.5f),new(220,161,96,255),Finish.Lamp);
            }
        }
        for (int face=0;face<4;face++)
        {
            bool alongX=face<2;
            int sign=face%2==0 ? -1 : 1;
            float limit=alongX ? HalfDepth : HalfWidth;
            void Block(float u,float y,float depth,float w,float h,float d,Color color,Finish f=Finish.Paint)
            {
                Vector3 p=alongX ? new(u,y,sign*depth) : new(sign*depth,y,u);
                mesh.Add(p,alongX ? new(w,h,d) : new(d,h,w),color,f);
            }
            Block(0,20,155,312,170,.5f,Color.White,Finish.Sky);
            Block(0,CeilingHeight/2,limit,limit*2,CeilingHeight,.025f,new(105,170,195,13),Finish.Glass);
            foreach(float y in new[] { .08f,3.88f,7.60f,CeilingHeight-.08f })
                Block(0,y,limit-.04f,limit*2,.095f,.13f,steel);
            // White Shark upper mezzanine balcony slab & glass railing at y = 6.4m around the atrium perimeter
            Block(0,6.35f,limit-2.2f,limit*2-4.4f,.22f,4.4f,new(72,82,96,255),Finish.Plaster);
            Block(0,6.95f,limit-4.35f,limit*2-8.7f,.95f,.04f,new(115,185,210,32),Finish.Glass);
            Block(0,7.45f,limit-4.35f,limit*2-8.7f,.06f,.08f,aluminum);
            for (float u=-limit;u<limit;u+=4.5f)
            {
                Block(u,CeilingHeight/2,limit-.04f,.105f,CeilingHeight,.17f,steel);
                float w=Math.Min(4.5f,limit-u)-.13f;
                int bay=(int)((u+limit)/4.5f);
                int slats=5+(bay*3+face*7)%14;
                for (int i=0;i<slats;i++)
                    Block(u+w/2+.065f,CeilingHeight-.23f-i*.14f,limit-.15f,w,.043f,.16f,aluminum);
            }
            // Two staggered city blocks on each face. The player's floor is well above street level.
            for (int layer=0;layer<2;layer++)
            {
                int count=layer==0 ? 12 : 16;
                float step=layer==0 ? 10.8f : 14;
                for (int i=0;i<count;i++)
                {
                    float u=(i-(count-1)*.5f)*step+urban.NextSingle()*3;
                    float depth=limit+15+layer*38+urban.NextSingle()*13;
                    float roof=-9+urban.NextSingle()*(layer==0 ? 21 : 40);
                    float w=5+urban.NextSingle()*(step-6), d=6+urban.NextSingle()*5;
                    u=ClearAvenue(u,w+2.85f);
                    depth=ClearAvenue(depth,d+2.65f);
                    Color hue=lights[urban.Next(lights.Length)];
                    Block(u,(roof-52)/2,depth,w,roof+52,d,hue,Finish.Facade);
                    float podium=6+(i+face*3)%7;
                    Block(u,-52+podium/2,depth,w+2.8f,podium,d+2.6f,new(54,54,62,255),Finish.Slate);
                    Block(u,-51+podium*.28f,depth,w+2.85f,.42f,d+2.65f,new(203,146,88,255),Finish.Lamp);
                    float crown=1+urban.NextSingle()*4;
                    Block(u,roof+crown*.5f,depth,w*.72f,crown,d*.75f,hue,Finish.Facade);
                    if (i%3==2)
                    {
                        Block(u,roof-.08f,depth,w+.05f,.13f,d+.05f,hue,Finish.Lamp);
                        Block(u,roof+crown+.05f,depth,w*.72f,.10f,d*.75f,hue,Finish.Lamp);
                    }
                    if (i%3==0)
                    {
                        Block(u,roof+crown+.85f,depth,.09f,1.7f,.09f,aluminum);
                        Block(u,roof+crown+1.75f,depth,.28f,.25f,.28f,new(255,46,62,255),Finish.Lamp);
                    }
                    if ((i+face)%4==1)
                        Block(u,roof-2,depth-d*.5f-.07f,w*.82f,2.5f,.1f,hue,Finish.Billboard);
                }
            }
        }
    }

    private void BuildOfficeServices(VisualEngine3D.ArchitectureBuilder mesh)
    {
        Color trim=new(61,72,81,255), steel=new(109,127,140,255);
        mesh.Add(new(0,CeilingHeight+.12f,0),new(HalfWidth*2,.24f,HalfDepth*2),new(125,132,142,255),Finish.Plaster);
        for (int x=-36;x<=36;x+=6)
        for (int z=-36;z<=36;z+=6)
        {
            mesh.Add(new(x,CeilingHeight-.02f,z),new(2.54f,.065f,1.34f),trim);
            bool working = ((x/6+6)*7+(z/6+6)*3)%3!=0;
            mesh.Add(new(x,CeilingHeight-.061f,z),new(2.4f,.032f,1.2f),
                working ? new Color(218,172,114,255) : new Color(68,72,82,255),
                working ? Finish.Fluorescent : Finish.Paint);
        }
        foreach (var duct in OfficeProps)
        {
            if (duct.Kind!=OfficePropKind.CeilingDuct) continue;
            var p=duct.Position;
            mesh.Add(p,duct.Size,steel);
            for (int z=-32;z<=32;z+=4)
            {
                mesh.Add(p+Vector3.UnitZ*z,new(duct.Size.X+.045f,duct.Size.Y+.04f,.06f),trim);
                foreach (int side in new[] { -1,1 })
                    mesh.Add(new(p.X+side*.61f,CeilingHeight-.38f,z),new(.032f,.76f,.032f),steel);
                mesh.Add(new(p.X,CeilingHeight-.77f,z),new(1.28f,.05f,.08f),steel);
            }
        }

    }

    private static void DrawCoffee(Vector3 basePos)
    {
        Color porcelain=new(220,211,185,255);
        VisualEngine3D.Rod(basePos,basePos+Vector3.UnitY*.14f,.061f,.073f,12,porcelain);
        VisualEngine3D.Rod(basePos+Vector3.UnitY*.14f,basePos+Vector3.UnitY*.143f,.058f,.058f,12,new Color(51,32,27,255));
        foreach (float y in new[] { .036f,.105f })
            VisualEngine3D.Box(basePos+new Vector3(.085f,y,0),.06f,.018f,.018f,porcelain);
        VisualEngine3D.Box(basePos+new Vector3(.111f,.071f,0),.018f,.084f,.018f,porcelain);
    }

    private static readonly string[] VeronikaModules=["prop_veronika_tarot_table","char_hr_veronika_body",
        "char_hr_veronika_head","char_hr_veronika_head_blink"];
    private float _veronikaHeadYaw,_veronikaHeadPitch,_rackOpen,_blinkUntil,_nextBlink=3.8f;
    private int _blinkIndex;
    private bool _doubleBlink;

    internal void UpdateCashParticles(float dt)
    {
        for (int i = CashParticles.Count - 1; i >= 0; i--)
        {
            var cp = CashParticles[i];
            cp.Life -= dt;
            if (cp.Life <= 0f)
            {
                CashParticles.RemoveAt(i);
                continue;
            }
            cp.Position += cp.Velocity * dt;
            cp.Velocity.Y -= 2.4f * dt;
            CashParticles[i] = cp;
        }
    }

    private float _slotBlend;
    private void DrawSlotHead(Vector3 pivot,float t)
    {
        float k=_slotBlend, shake=Slot.Active && !Slot.Reveal ? MathF.Sin(47*t) : 0;
        pivot.X+=.009f*shake;
        pivot.Y+=MathF.Abs(Slot.Dance)*.055f;
        float roll=2*shake+14*Slot.Dance;
        float angle=72*MathF.Sin(MathF.PI*Math.Clamp(Slot.Time/.65f,0,1));
        Vector3 At(float x,float y,float z)=>pivot+new Vector3(x,y,z)*k;
        Color brass=new(210,157,77,255), velvet=new(123,33,52,255);
        bool authored=VisualEngine3D.HasAsset("char_hr_veronika_slot_head");
        if(authored)
        {
            VisualEngine3D.SetSlotReels(Slot.Position);
            VisualEngine3D.AssetOriented("char_hr_veronika_slot_head",pivot,180,0,roll,k);
            VisualEngine3D.SetSlotReels(0);
        }
        else {
            VisualEngine3D.Box(At(0,.125f,0),.34f*k,.30f*k,.235f*k,brass,Finish.Bronze);
            VisualEngine3D.Box(At(0,.13f,-.112f),.31f*k,.235f*k,.04f*k,velvet,Finish.Leather);
            for(int i=-1;i<=1;i++) {
                float x=i*.092f;
                VisualEngine3D.Rod(At(x-.041f,.15f,-.137f),At(x+.041f,.15f,-.137f),.069f*k,.069f*k,24,new(248,224,181,255));
                for(int j=0;j<5;j++) {
                    float a=j*MathF.Tau/5+(Slot.Reveal ? 0 : Slot.Time*(15+i*2));
                    VisualEngine3D.Box(At(x,.15f+.064f*MathF.Sin(a),-.137f-.064f*MathF.Cos(a)),.044f*k,.026f*k,.013f*k,
                        j%2==0 ? velvet : brass);
                }
            }
            VisualEngine3D.Box(At(0,.007f,-.136f),.20f*k,.029f*k,.084f*k,brass,Finish.Bronze);
        }
        for(int i=0;i<7;i++)
        {
            float a=t*1.7f+i*MathF.Tau/7;
            Vector3 orbit=new(.39f*MathF.Cos(a),.13f+.38f*MathF.Sin(a),.025f*MathF.Sin(a*2));
            GoldSpark(pivot+orbit,.014f+.01f*(.5f+.5f*MathF.Cos(3*a)));
        }
        // The delivered head has its right temple socket at (-.118,.128,0) in glTF.
        Vector3 socket=authored ? new(-.118f,.128f,0) : new(-.132f,.135f,0);
        Matrix4x4 head=Matrix4x4.CreateScale(k)*Matrix4x4.CreateFromYawPitchRoll(MathF.PI,0,roll*MathF.PI/180)*
            Matrix4x4.CreateTranslation(pivot);
        Vector3 lever=Vector3.Transform(socket,head);
        if(VisualEngine3D.HasAsset("char_hr_veronika_slot_lever"))
            VisualEngine3D.Asset("char_hr_veronika_slot_lever",Vector3.Zero,assemblyTransform:
                Matrix4x4.CreateRotationX(angle*MathF.PI/180)*Matrix4x4.CreateTranslation(socket)*head);
        else {
            float a=angle*MathF.PI/180;
            Vector3 end=lever+new Vector3(.087f,.128f*MathF.Cos(a),-.128f*MathF.Sin(a))*k;
            VisualEngine3D.Rod(lever,end,.012f*k,.012f*k,12,brass,Finish.Bronze);
            VisualEngine3D.Box(end,.042f*k,.046f*k,.042f*k,velvet);
        }
    }

    internal void PoseVeronika(Vector3 eye,float dt)
    {
        Vector3 d=eye-(HrTarotBoothPos+new Vector3(0,1.445f,-.045f));
        float blend=1-MathF.Exp(-7*dt);
        bool track=d.Z<0 && d.LengthSquared()<144;
        float yaw=track ? Math.Clamp(MathF.Atan2(-d.X,-d.Z)*180/MathF.PI,-42,42) : 0;
        float pitch=track ? Math.Clamp(-MathF.Atan2(d.Y,new Vector2(d.X,d.Z).Length())*180/MathF.PI,-16,16) : 0;
        _slotBlend+=((Slot.Active ? 1 : 0)-_slotBlend)*blend;
        _veronikaHeadYaw+=(yaw-_veronikaHeadYaw)*blend;
        _veronikaHeadPitch+=(pitch-_veronikaHeadPitch)*blend;
        _rackOpen+=((d.LengthSquared()<4.5f*4.5f ? 1 : 0)-_rackOpen)*blend;
        if (_worldClock>=_nextBlink)
        {
            _blinkUntil=_worldClock+.14f;
            _doubleBlink=!_doubleBlink && ++_blinkIndex%4==0;
            _nextBlink=_worldClock+(_doubleBlink ? .34f : 3.4f+(_blinkIndex*37%101)*.014f);
        }
    }

    private void DrawHrTarotBoothAndVeronika3D()
    {
        Vector3 b = HrTarotBoothPos;
        Color walnut = new(158, 112, 74, 255), darkWalnut = new(118, 80, 52, 255);
        Color brass = new(166, 130, 74, 255), darkBronze = new(78, 58, 36, 255);
        Color marble = new(32, 30, 34, 255), warmGlow = new(255, 224, 168, 255);

        // Unified Warm Fluted-Walnut Executive Reception Counter (Left Executive Wing + Center Tarot Sanctuary + Right Integrated Vitrine!)
        // Left Executive Wing (X = +0.82..+2.06, Z = b.Z-0.48..b.Z+0.16) + side return to fluted wall
        Vector3 leftWing = b + new Vector3(1.44f, 0f, -0.16f);
        VisualEngine3D.Box(leftWing + new Vector3(0f, 0.46f, 0f), 1.24f, 0.86f, 0.62f, walnut, Finish.Leather);
        VisualEngine3D.Box(leftWing + new Vector3(0f, 0.045f, 0f), 1.28f, 0.09f, 0.66f, darkBronze, Finish.Bronze);
        VisualEngine3D.Box(leftWing + new Vector3(0f, 0.905f, 0f), 1.28f, 0.045f, 0.66f, darkWalnut, Finish.Leather);
        VisualEngine3D.Box(leftWing + new Vector3(0f, 0.955f, 0f), 1.32f, 0.06f, 0.70f, marble, Finish.BlackMarble);
        // Sculpted vertical fluted walnut slats on front face (matching architectural reception desk reference!)
        for (int f = -6; f <= 6; f++)
            VisualEngine3D.Box(leftWing + new Vector3(f * 0.092f, 0.48f, -0.32f), 0.044f, 0.76f, 0.036f, darkWalnut, Finish.Leather);
        // Left walnut privacy return linking reception counter flush to fluted backdrop wall (Z = 15.38)
        VisualEngine3D.Box(new Vector3(b.X + 2.00f, 0.46f, 14.92f), 0.14f, 0.92f, 0.96f, walnut, Finish.Leather);
        VisualEngine3D.Box(new Vector3(b.X + 2.00f, 0.95f, 14.92f), 0.18f, 0.06f, 0.98f, marble, Finish.BlackMarble);
        // Classic green-shade brass banker's lamp & leather guest register on Left Executive Wing
        Vector3 lampBase = leftWing + new Vector3(0.32f, 0.985f, -0.02f);
        VisualEngine3D.Rod(lampBase, lampBase + Vector3.UnitY * 0.03f, 0.09f, 0.075f, 14, brass, Finish.Bronze);
        VisualEngine3D.Rod(lampBase + Vector3.UnitY * 0.02f, lampBase + Vector3.UnitY * 0.30f, 0.015f, 0.013f, 10, brass, Finish.Bronze);
        VisualEngine3D.Box(lampBase + Vector3.UnitY * 0.31f, 0.22f, 0.085f, 0.12f, new Color(28, 96, 62, 255));
        VisualEngine3D.Orb(lampBase + Vector3.UnitY * 0.27f, 0.038f, warmGlow, Finish.Lamp);
        VisualEngine3D.OrientedBox(leftWing + new Vector3(-0.22f, 0.995f, -0.04f), new Vector3(0.34f, 0.025f, 0.25f), 0.12f, new Color(92, 28, 36, 255));
        VisualEngine3D.Sheet(leftWing + new Vector3(-0.22f, 1.012f, -0.04f), 0.12f, 0f, Finish.PitchDeck);

        // Stepped walnut & brass cheek collars smoothly connecting Veronika's central tarot drum flush to both desk wings
        foreach (int side in new[] { -1, 1 })
        {
            Vector3 collar = b + new Vector3(side * 0.84f, 0f, -0.18f);
            VisualEngine3D.Box(collar + new Vector3(0f, 0.45f, 0f), 0.16f, 0.86f, 0.64f, darkWalnut, Finish.Leather);
            VisualEngine3D.Box(collar + new Vector3(0f, 0.905f, 0f), 0.19f, 0.045f, 0.68f, brass, Finish.Bronze);
            VisualEngine3D.Box(collar + new Vector3(0f, 0.045f, 0f), 0.19f, 0.09f, 0.68f, brass, Finish.Bronze);
        }

        // Curved vertical brushed-brass Art-Deco flutes, base kickplate & upper trim collar uniting the central velvet drum with both wings!
        for (int k = -4; k <= 4; k++)
        {
            float ang = k * 15.5f * MathF.PI / 180f;
            float rx = MathF.Sin(ang) * 0.845f;
            float rz = -MathF.Cos(ang) * 0.768f;
            VisualEngine3D.FaceBox(b + new Vector3(rx, 0.485f, rz), 0.028f, 0.76f, 0.030f, -k * 15.5f, brass, Finish.Bronze);
        }
        for (int seg = -6; seg <= 6; seg++)
        {
            float ang = seg * 11.5f * MathF.PI / 180f;
            float rx = MathF.Sin(ang) * 0.855f;
            float rz = -MathF.Cos(ang) * 0.776f;
            VisualEngine3D.FaceBox(b + new Vector3(rx, 0.068f, rz), 0.185f, 0.092f, 0.036f, -seg * 11.5f, brass, Finish.Bronze);
            VisualEngine3D.FaceBox(b + new Vector3(rx, 0.895f, rz), 0.185f, 0.038f, 0.036f, -seg * 11.5f, brass, Finish.Bronze);
        }

        // Symmetrical Art-Deco frosted-glass & brass wall sconces flanking the ARCANA emblem on the fluted backdrop wall
        foreach (int side in new[] { -1, 1 })
        {
            Vector3 sconce = new(side * 1.78f, 2.75f, 15.34f);
            VisualEngine3D.Box(sconce, 0.22f, 0.56f, 0.06f, darkBronze, Finish.Bronze);
            VisualEngine3D.Box(sconce + new Vector3(0f, 0f, -0.03f), 0.13f, 0.40f, 0.05f, warmGlow, Finish.Lamp);
            foreach (float sy in new[] { -0.23f, 0.23f })
                VisualEngine3D.Box(sconce + new Vector3(0f, sy, -0.02f), 0.24f, 0.04f, 0.08f, brass, Finish.Bronze);
        }

        // Art-Deco Deep Wine-Burgundy Velvet Drapery & Stepped Brushed-Brass Sanctuary Alcove behind Veronika (classy muted luxury matching ARCANA sign!)
        {
            Color velvetDeep = new(24, 7, 14, 255), velvetFold = new(38, 11, 22, 255);
            // Recessed espresso-plum velvet backing
            VisualEngine3D.Box(new Vector3(0f, 1.66f, 15.31f), 2.58f, 1.56f, 0.04f, new Color(18, 5, 11, 255), Finish.Fabric);
            // 17 sculpted vertical deep-wine velvet curtain pleats framing her portrait
            for (int p = -8; p <= 8; p++)
            {
                float px = p * 0.148f;
                bool peak = (p & 1) == 0;
                VisualEngine3D.Box(new Vector3(px, 1.66f, peak ? 15.27f : 15.29f),
                    0.125f, 1.50f, peak ? 0.055f : 0.032f, peak ? velvetFold : velvetDeep, Finish.Fabric);
            }
            // Refined stepped brushed-brass Art-Deco sanctuary pilasters & cornice (zero aliased thin diagonal sticks!)
            VisualEngine3D.Box(new Vector3(0f, 2.45f, 15.24f), 2.66f, 0.055f, 0.09f, brass, Finish.Bronze);
            VisualEngine3D.Box(new Vector3(0f, 2.41f, 15.23f), 2.44f, 0.018f, 0.045f, new Color(255, 196, 116, 255), Finish.Lamp);
            VisualEngine3D.Box(new Vector3(0f, 0.90f, 15.24f), 2.66f, 0.055f, 0.09f, brass, Finish.Bronze);
            foreach (int side in new[] { -1, 1 })
            {
                VisualEngine3D.Box(new Vector3(side * 1.30f, 1.67f, 15.24f), 0.065f, 1.56f, 0.09f, brass, Finish.Bronze);
                VisualEngine3D.Box(new Vector3(side * 0.92f, 1.67f, 15.245f), 0.026f, 1.48f, 0.045f, darkBronze, Finish.Bronze);
                VisualEngine3D.Box(new Vector3(side * 0.56f, 1.98f, 15.245f), 0.020f, 0.88f, 0.040f, darkBronze, Finish.Bronze);
            }
            // Stepped Art-Deco brass crown arch inset framing Veronika's silhouette
            VisualEngine3D.Box(new Vector3(0f, 2.34f, 15.24f), 1.84f, 0.028f, 0.055f, brass, Finish.Bronze);
            VisualEngine3D.Box(new Vector3(0f, 2.26f, 15.24f), 1.14f, 0.024f, 0.055f, brass, Finish.Bronze);
        }

        Vector3 origin=b+new Vector3(0,.025f,0);
        bool veronikaHidden = BossRevealed && !ElevatorBoss.IsDefeated;
        bool arms=VisualEngine3D.HasAsset("char_hr_veronika_arm_l") && VisualEngine3D.HasAsset("char_hr_veronika_arm_r");
        if (VeronikaModules.All(VisualEngine3D.HasAsset) && (arms || VisualEngine3D.HasAsset("char_hr_veronika_hands")))
        {
            VisualEngine3D.Asset(VeronikaModules[0],origin,180);
            if (!veronikaHidden)
            {
                bool talking=AudioMusicSystem.VoiceTimer>0 && AudioMusicSystem.ActiveSpeakerType==(int)SpeakerVoiceType.VeronikaTarot;
                float t=_worldClock, breath=MathF.Sin(t*1.9f)*.008f;
                VisualEngine3D.Asset(VeronikaModules[1],origin+Vector3.UnitY*breath,180);
                bool blink=t<_blinkUntil;
                Vector3 pivot=origin+new Vector3(0,1.415f+breath,-.045f);
                if(_slotBlend>.015f) DrawSlotHead(pivot,t);
                if(_slotBlend<.985f) VisualEngine3D.AssetOriented(VeronikaModules[blink ? 3 : 2],origin+new Vector3(0,1.415f+breath,-.045f),
                    180+_veronikaHeadYaw,_veronikaHeadPitch+(talking ? 3.5f*MathF.Sin(t*8.5f) : 0),talking ? 3*MathF.Sin(t*3.2f) : 0,1-_slotBlend);
                bool reading=talking || TarotVerdictTimer>0;
                if(arms)
                {
                    void Arm(string model,float x,float phase)
                    {
                        float gesture=reading ? 1f : .4f;
                        Vector3 armPivot=origin+new Vector3(-x,1.298f+breath,-.032f);
                        float pump=Slot.Active && Slot.Reveal ? 22+17*MathF.Sin(Slot.RevealTime*MathF.Tau*130/60+phase) : 0;
                        VisualEngine3D.AssetOriented(model,armPivot,180,-pump+3.5f*gesture*MathF.Sin(t*1.9f+phase),
                            MathF.Sign(x)*pump*.75f+2.5f*gesture*MathF.Sin(t*1.45f+phase));
                    }
                    Arm("char_hr_veronika_arm_l",.134f,0);
                    Arm("char_hr_veronika_arm_r",-.134f,2.1f);
                }
                else
                {
                    Vector3 hands=new(.028f*MathF.Sin(t*1.6f),1.310f+breath+.018f*MathF.Sin(t*3.2f)+(reading ? .025f : 0),
                        -.320f+.016f*MathF.Cos(t*1.6f));
                    VisualEngine3D.AssetOriented("char_hr_veronika_hands",origin+hands,180,0,reading ? 4*MathF.Sin(t*2) : 0);
                }
            }
            // Delicate flickering golden candle halos + subtle mystical amethyst-gold crystal ball aura (zero noisy bonfire sparks!)
            if (!VisualEngine3D.CastingShadows && CombatSprites.HasPaintedFx)
            {
                ReadOnlySpan<Vector3> candleWicks =
                [
                    origin + new Vector3(+0.56f, 1.232f, -0.31f),
                    origin + new Vector3(+0.64f, 1.182f, -0.44f),
                    origin + new Vector3(-0.56f, 1.222f, -0.31f),
                    origin + new Vector3(-0.64f, 1.172f, -0.44f),
                ];
                Vector3 cRight = VisualEngine3D.ActiveCamRight;
                Vector3 cUp = VisualEngine3D.ActiveCamUp;
                CombatSprites.Begin(BlendMode.Additive);
                for (int ci = 0; ci < candleWicks.Length; ci++)
                {
                    float flick = 0.90f + 0.12f * MathF.Sin(_worldClock * (11.5f + ci * 2.1f) + ci * 1.7f);
                    // Pure silky Gaussian candlelight bloom (zero noisy bonfire sparks!)
                    CombatSprites.SoftHalo(candleWicks[ci] + Vector3.UnitY * 0.008f, cRight, cUp,
                        0.068f * flick, 0.078f * flick, new Color(255, 178, 68, 95));
                    CombatSprites.SoftHalo(candleWicks[ci] + Vector3.UnitY * 0.006f, cRight, cUp,
                        0.024f * flick, 0.036f * flick, new Color(255, 240, 188, 145));
                }
                // Soft mystical violet-gold aura halo around the layered glass crystal ball
                Vector3 orbPos = origin + new Vector3(0f, 1.115f, -0.357f);
                float orbPulse = 0.88f + 0.12f * MathF.Sin(_worldClock * 2.6f);
                CombatSprites.SoftHalo(orbPos, cRight, cUp, 0.21f * orbPulse, 0.21f * orbPulse,
                    new Color(168, 86, 246, 68));
                CombatSprites.End();
                for (int m = 0; m < 6; m++)
                {
                    float a = _worldClock * 1.15f + m * MathF.Tau / 6f;
                    float rad = 0.145f + 0.020f * MathF.Sin(_worldClock * 2.3f + m);
                    Vector3 mote = orbPos + new Vector3(MathF.Cos(a) * rad, 0.015f + 0.052f * MathF.Sin(a * 1.7f + m), MathF.Sin(a) * rad);
                    VisualEngine3D.Orb(mote, 0.0038f, m % 2 == 0 ? new Color(255, 214, 118, 220) : new Color(214, 148, 255, 210), Finish.Lamp);
                }
            }
        }
        else
        {
            VisualEngine3D.Asset("prop_hr_reception_desk",b);
            if (!veronikaHidden)
                VisualEngine3D.Asset("char_hr_veronika",origin,180);
        }
        DrawWeaponRack(b+new Vector3(-1.62f,0,-.16f),true);
        // The dynamic box retains the inscription's upright UVs; the batched wall uses world UVs.
        VisualEngine3D.Box(new(0,3.25f,15.28f),1.88f,1.53f,.025f,Color.White,Finish.Tarot);
    }

    private void Draw90sArsenalAndShopStand3D()
    {
        Vector3 s = ArsenalShopPos;
        VisualEngine3D.Asset("prop_arsenal_counter", s, 0f);

        DrawWeaponRack(s,false);
    }

    private void DrawWeaponRack(Vector3 origin,bool compact)
    {
        float spacing=compact ? .49f : .83f;
        if(compact)
        {
            // Integrated Right Reception Wing Weapon Vitrine — matching warm fluted walnut & black-marble counter rim!
            Color walnut = new(158, 112, 74, 255), darkWalnut = new(118, 80, 52, 255), brass = new(166, 130, 74, 255), marble = new(32, 30, 34, 255);
            VisualEngine3D.Box(origin+new Vector3(0,.46f,0),1.54f,.86f,.62f,walnut,Finish.Leather);
            VisualEngine3D.Box(origin+new Vector3(0,.045f,0),1.58f,.09f,.66f,new(78,58,36,255),Finish.Bronze);
            VisualEngine3D.Box(origin+new Vector3(0,.905f,0),1.58f,.045f,.66f,darkWalnut,Finish.Leather);
            VisualEngine3D.Box(origin+new Vector3(0,.945f,0),1.60f,.04f,.68f,marble,Finish.BlackMarble);
            for(int f=-7;f<=7;f++)
                VisualEngine3D.Box(origin+new Vector3(f*.095f,.48f,-.32f),.044f,.76f,.036f,darkWalnut,Finish.Leather);
            // 4 sleek brushed-brass museum vitrine corner stanchions + top brass rail framing the clear glass case
            foreach(float sx in new[]{-.74f,.74f})
            foreach(float sz in new[]{-.29f,.29f})
                VisualEngine3D.Rod(origin+new Vector3(sx,.95f,sz),origin+new Vector3(sx,1.32f,sz),
                    .015f,.015f,14,brass,Finish.Bronze);
            VisualEngine3D.Box(origin+new Vector3(0,1.32f,-.29f),1.50f,.022f,.022f,brass,Finish.Bronze);
            VisualEngine3D.Box(origin+new Vector3(0,1.32f,.29f),1.50f,.022f,.022f,brass,Finish.Bronze);
        }
        int slot=0;
        for(int i=0;i<FloorSystems.WeaponOrder.Length;i++)
        {
            if(Systems.HasWeapon(FloorSystems.WeaponOrder[i])) continue;
            if(compact && slot==3) break;
            float x=spacing*(compact ? slot++-1 : i-2);
            Vector3 top=origin+new Vector3(x,compact ? .94f : 1.10f,compact ? 0 : -.28f);
            var gold=Raylib.ColorLerp(new(108,81,52,255),new(239,182,98,255),compact ? _rackOpen : .45f);
            VisualEngine3D.Box(top,spacing*.92f,.025f,.38f,gold,Finish.Lamp);
            VisualEngine3D.Box(top+Vector3.UnitY*.032f,spacing*.94f,.055f,.39f,new(42,38,47,255),Finish.Fabric);
            float lift=.16f+.009f*MathF.Sin(_worldClock*1.8f+i)+(compact ? .035f*_rackOpen : 0);
            float size=compact ? .83f : 1.12f;
            string model=FloorSystems.WeaponModels[i];
            var bounds=VisualEngine3D.ModelBounds(model);
            float yaw=i==1 ? 165 : 65, pitch=-12-(compact ? 16*_rackOpen : 0);
            var rotation=Quaternion.CreateFromYawPitchRoll(yaw*MathF.PI/180,pitch*MathF.PI/180,-8*MathF.PI/180);
            Vector3 centerOffset=Vector3.Transform((bounds.Min+bounds.Max)*(.5f*size),rotation);
            VisualEngine3D.AssetOriented(model,top+Vector3.UnitY*lift-centerOffset,yaw,pitch,-8,size);
        }
        // Prestige collector showcase inside the compact reception vitrine when player already owns all weapons (never an empty glass box!)
        if(compact && slot==0)
        {
            Color velvet = new(96, 22, 36, 255), brassTrim = new(176, 138, 78, 255);
            int[] exhibitIndices = [3, 4]; // Micro-Uzi & Sawed-Off collector showcase
            for(int e=0; e<exhibitIndices.Length; e++)
            {
                int wi = exhibitIndices[e];
                float ex = (e == 0 ? -.37f : .37f);
                Vector3 baseTop = origin + new Vector3(ex, .965f, 0f);
                VisualEngine3D.Box(baseTop, .56f, .036f, .38f, brassTrim, Finish.Bronze);
                VisualEngine3D.Box(baseTop + Vector3.UnitY * .030f, .52f, .042f, .34f, velvet, Finish.Fabric);
                foreach(float cx in new[]{-.16f, .16f})
                    VisualEngine3D.Rod(baseTop + new Vector3(cx, .045f, 0f), baseTop + new Vector3(cx, .155f, 0f), .012f, .010f, 10, brassTrim, Finish.Bronze);
                string model = FloorSystems.WeaponModels[wi];
                var bounds = VisualEngine3D.ModelBounds(model);
                float size = .88f, yaw = e == 0 ? 78f : 102f, pitch = -14f;
                var rotation = Quaternion.CreateFromYawPitchRoll(yaw*MathF.PI/180, pitch*MathF.PI/180, -6*MathF.PI/180);
                Vector3 centerOffset = Vector3.Transform((bounds.Min+bounds.Max)*(.5f*size), rotation);
                VisualEngine3D.AssetOriented(model, baseTop + Vector3.UnitY * .20f - centerOffset, yaw, pitch, -6, size);
            }
        }
        if(compact) VisualEngine3D.DisplayLid(origin+new Vector3(0,1.31f,.28f),_rackOpen);
    }

    private void DrawPizza(Vector3 top,float angle,float scale=1)
    {
        foreach(int index in Systems.StationWait.Keys)
            if(stations[index].Kind==FocusKind.Pizza && stations[index].Feet.X==top.X && stations[index].Feet.Z==top.Z) return;
        VisualEngine3D.Asset("prop_pizza_stack",top,angle,scale);
    }
    private static Vector3 PodPizza(Vector3 feet,bool reversed) => feet+Vector3.UnitY*(.792f*WorkstationScale)+(reversed?-1:1)*new Vector3(-.56f,0,.06f);

    private void DrawPod(Vector3 feet,int seat,bool occupied,bool reversed=false)
    {
        // Unique deterministic personality per desk so ZERO two workstations look stamped!
        int h = Math.Abs(seat * 37 + (int)MathF.Round(feet.X * 11f) + (int)MathF.Round(feet.Z * 17f));
        float chairYaw = ((h % 2 == 0) ? 1f : -1f) * (14f + (h * 13 % 34)) * MathF.PI / 180f;
        float chairPull = 0.05f + (h % 5) * 0.042f;
        VisualEngine3D.Workstation(feet, occupied ? Vector2.Zero : new(chairYaw, chairPull), reversed ? 0 : 180);
        // Full-height rich dark oiled-walnut side-panel cladding + crisp thin brass edge cap & plinth (replaces bright orange half-boards!)
        foreach (int sx in new[] { -1, 1 })
        {
            VisualEngine3D.Box(feet + new Vector3(sx * 1.032f, 0.54f, 0f), 0.045f, 0.98f, 0.91f, new Color(58, 36, 24, 255), Finish.Leather);
            VisualEngine3D.Box(feet + new Vector3(sx * 1.036f, 1.042f, 0f), 0.056f, 0.026f, 0.93f, new Color(164, 128, 72, 255), Finish.Bronze);
            VisualEngine3D.Box(feet + new Vector3(sx * 1.036f, 0.032f, 0f), 0.056f, 0.064f, 0.93f, new Color(112, 84, 46, 255), Finish.Bronze);
        }
        Vector3 top = feet + Vector3.UnitY * (.792f * WorkstationScale);
        float side = reversed ? -1f : 1f;
        float lampSide = (h % 3 == 0) ? -1f : 1f;

        // 1) Pizza box OR Coffee mug OR Energy drink cans (varied per desk!)
        if (seat % 6 == 1) DrawPizza(PodPizza(feet, reversed), 13 + (h % 25), .78f);
        else if (h % 5 != 0)
        {
            DrawCoffee(top + side * new Vector3((h % 2 == 0 ? -.61f : .56f), 0, .12f + (h % 3) * .04f));
            if (h % 4 == 2) // Extra metallic energy can next to the mug!
                VisualEngine3D.Rod(top + side * new Vector3(.45f, 0, .18f), top + side * new Vector3(.45f, .125f, .18f), .033f, .033f, 10,
                    (h % 2 == 0) ? new(42, 168, 132, 255) : new(210, 68, 52, 255));
        }

        // 2) Warm articulated brass desk lamp on ~60% of workstations!
        if (h % 5 < 3)
        {
            Vector3 lb = top + side * new Vector3(lampSide * .74f, 0, -.20f);
            VisualEngine3D.Rod(lb, lb + Vector3.UnitY * .025f, .085f, .075f, 12, new(48, 46, 44, 255));
            Vector3 elbow = lb + new Vector3(-side * lampSide * .08f, .24f, side * .03f);
            Vector3 head  = lb + new Vector3(-side * lampSide * .14f, .37f, side * .09f);
            VisualEngine3D.Rod(lb + Vector3.UnitY * .02f, elbow, .012f, .011f, 8, new(168, 132, 74, 255), Finish.Bronze);
            VisualEngine3D.Rod(elbow, head, .011f, .010f, 8, new(168, 132, 74, 255), Finish.Bronze);
            VisualEngine3D.Rod(head + Vector3.UnitY * .02f, head - Vector3.UnitY * .055f, .032f, .082f, 12, new(56, 66, 62, 255));
            VisualEngine3D.Orb(head - Vector3.UnitY * .048f, .038f, new(255, 214, 132, 255), Finish.Lamp);
        }

        // 3) Stacked colorful Jira/NDA project binders & sheets on ~70% of workstations!
        if (h % 10 < 7)
        {
            float fx = -lampSide * .66f;
            Vector3 st = top + side * new Vector3(fx, 0, -.06f);
            Color[] binderColors = [new(158, 72, 54, 255), new(54, 82, 118, 255), new(176, 132, 58, 255), new(64, 108, 82, 255)];
            int count = 2 + (h % 3);
            for (int k = 0; k < count; k++)
            {
                float a = ((h + k * 5) % 9 - 4) * .07f;
                VisualEngine3D.OrientedBox(st + Vector3.UnitY * (.011f + k * .022f), new(.23f, .020f, .31f), a, binderColors[(h + k) % binderColors.Length]);
            }
            VisualEngine3D.Sheet(st + Vector3.UnitY * (.006f + count * .022f), ((h % 7) - 3) * .09f, 0,
                (h % 2 == 0) ? Finish.JiraSheet : Finish.PitchDeck);
        }

        // 4) Mini potted desk succulent/plant on ~45% of workstations!
        if (h % 9 < 4)
        {
            Vector3 pp = top + side * new Vector3(lampSide * .68f, 0, .18f);
            VisualEngine3D.Rod(pp, pp + Vector3.UnitY * .095f, .048f, .062f, 10, new(166, 94, 62, 255));
            VisualEngine3D.Oval(pp + Vector3.UnitY * .135f, new(.075f, .068f, .075f), new(54, 128, 72, 255));
            VisualEngine3D.Oval(pp + new Vector3(.032f, .155f, -.02f), new(.048f, .055f, .048f), new(72, 152, 84, 255));
        }

        // 5) Bright scattered sticky notes & desk pads
        if (seat % 3 != 0)
        {
            VisualEngine3D.OrientedBox(top + new Vector3(-.36f * side, .004f, .26f * side), new(.08f, .004f, .079f), chairYaw, new(226, 202, 114, 255));
            VisualEngine3D.OrientedBox(top + new Vector3(-.43f * side, .008f, .24f * side), new(.075f, .004f, .079f), -chairYaw, new(143, 194, 174, 255));
        }
    }

    private void BuildOfficeWiring(VisualEngine3D.ArchitectureBuilder mesh)
    {
        Color rubber=new(40,42,45,255);
        void Tray(float x1,float z1,float x2,float z2)
        {
            var length=new Vector2(x2-x1,z2-z1).Length();
            mesh.Add(new((x1+x2)/2,.014f,(z1+z2)/2),new(.09f,.024f,length),rubber,yaw:MathF.Atan2(x2-x1,z2-z1));
        }
        // Trunk skirts the shaft. Branches terminate at the back of each workstation row.
        Tray(RackPos.X,RackPos.Z,8.9f,3.8f);
        Tray(8.9f,3.8f,8.9f,-13.1f);
        Tray(8.9f,-13.1f,-27.83f,-13.1f);
        Tray(8.9f,-13.1f,22,-13.1f);
        foreach(var rack in SideRacks) Tray(rack.X,rack.Z,rack.X,-13.1f);
        foreach(float x in new[]{-14.5f,-20.7f,-26.9f,14.8f,22f})
        {
            float trunk=x-.93f;
            Tray(trunk,x<0 ? -23 : -25,trunk,x<0 ? 12 : 10.5f);
            foreach(var p in OfficeProps)
                if(p.Kind==OfficePropKind.DualMonitorDesk && MathF.Abs(p.Position.X-x)<.03f)
                    Tray(trunk,p.Position.Z-.36f,x,p.Position.Z-.36f);
            foreach(var d in GalleyDesks.Where(d=>d.DeskPosition.X==x))
                Tray(trunk,d.DeskPosition.Z-.36f,x,d.DeskPosition.Z-.36f);
        }
        // Service tape marks only the cart parking bay.
        for(int i=-4;i<=4;i++)
            mesh.Add(CartPosition+new Vector3(i*.12f,.009f,.44f),new(.085f,.012f,.12f),
                i%2==0 ? new(199,150,57,255) : rubber,yaw:-.38f);
        for(int lane=0;lane<3;lane++)
        {
            Vector3 a=RackPos+new Vector3(.36f,.042f,-.38f);
            Vector3 end=lane==2 ? new(4.65f,.035f,.15f) : CartPosition+new Vector3(.18f*lane,.035f,-.14f);
            for(int j=1;j<=20;j++)
            {
                float t=j/20f;
                Vector3 b=Vector3.Lerp(RackPos+new Vector3(.36f,.042f,-.38f),end,t);
                b.X+=MathF.Sin(t*MathF.PI*2+lane*.4f)*.19f*MathF.Sin(t*MathF.PI);
                b.Z+=MathF.Sin(t*MathF.PI)*(.14f+lane*.09f);
                Vector3 delta=b-a;
                mesh.Add((a+b)/2,new(.028f,.028f,delta.Length()+.016f),j%3==0 ? new(73,65,54,255) : rubber,
                    yaw:MathF.Atan2(delta.X,delta.Z));
                a=b;
            }
        }
    }

    private static readonly Vector3 PrintBoardOrigin=new(-4.56f,1.14f,-5.95f);
    private static readonly Vector3 ShaftCafeTable=new(5.18f,0,-1.62f);

    private void DrawLivingOffice()
    {
        foreach(var lamp in PendantLocations)
            VisualEngine3D.Asset("prop_pendant_lamp",new(lamp.X,CeilingHeight,lamp.Y));
        // Lived-in Moscow-City architectural props around the Technical Portals & Reception Gallery (ZERO walkway blocking!)
        if (VisualEngine3D.HasAsset("prop_coat_rack"))
            VisualEngine3D.Asset("prop_coat_rack", new(5.77f, 0f, 15.84f), 192f, 1.0f);
        if (VisualEngine3D.HasAsset("prop_concrete_plant"))
        {
            // Leafy Monstera/Philodendron in warm concrete planters right beside concrete portal jambs & reception desk!
            VisualEngine3D.Asset("prop_concrete_plant", new(2.74f, 0f, 15.62f), 25f, 1.04f);
            VisualEngine3D.Asset("prop_concrete_plant", new(9.06f, 0f, 15.86f), -38f, 1.12f);
            VisualEngine3D.Asset("prop_concrete_plant", new(-2.60f, 0f, 14.96f), 140f, 0.98f);
            VisualEngine3D.Asset("prop_concrete_plant", new(1.98f, 0.985f, 14.48f), -18f, 0.42f);
        }
        if (VisualEngine3D.HasAsset("prop_wall_art"))
        {
            // Framed modernist Moscow-City architectural artworks mounted cleanly on solid lobby & wing walls
            VisualEngine3D.Asset("prop_wall_art", new(4.53f, 2.45f, -4.25f), 90f, 1.05f);
            VisualEngine3D.Asset("prop_wall_art", new(-4.53f, 2.45f, -1.75f), -90f, 1.05f);
            VisualEngine3D.Asset("prop_wall_art", new(-14.80f, 2.30f, 19.84f), 180f, 1.15f);
            VisualEngine3D.Asset("prop_wall_art", new(14.80f, 2.30f, 19.84f), 180f, 1.15f);
        }
        // Clustered warm glowing frosted-glass globe pendant lamps hanging above the Reception & Technical Portal gallery!
        foreach (var (gx, gy, gz, gr) in new (float, float, float, float)[] {
            (2.45f, 3.32f, 13.85f, .165f),
            (2.96f, 3.64f, 13.38f, .145f),
            (2.05f, 3.88f, 13.52f, .125f),
            (5.77f, 3.55f, 14.45f, .155f),
            (8.62f, 3.42f, 14.30f, .140f)
        })
        {
            VisualEngine3D.Rod(new(gx, CeilingHeight, gz), new(gx, gy + gr * .85f, gz), .008f, .008f, 8, new(42, 44, 48, 255));
            VisualEngine3D.Rod(new(gx, gy + gr * .42f, gz), new(gx, gy + gr * .95f, gz), gr * .82f, gr * .32f, 14, new(164, 128, 72, 255), Finish.Bronze);
            VisualEngine3D.Orb(new(gx, gy, gz), gr, new(255, 232, 186, 255), Finish.Lamp);
        }
        VisualEngine3D.Asset("prop_server_cart",CartPosition,-139);
        foreach(var rack in SideRacks) VisualEngine3D.Asset("prop_server_rack",rack,180);
        // Hospitality coffee cups & serving tray atop the built-in walnut credenza at VentMouth(0)
        Vector3 credTop = VentMouth(0) + new Vector3(0f, 1.29f, -.44f);
        VisualEngine3D.Box(credTop + new Vector3(-.20f, .012f, .02f), .42f, .022f, .28f, new Color(152, 118, 66, 255), Finish.Bronze);
        DrawCoffee(credTop + new Vector3(-.30f, .024f, .02f));
        DrawCoffee(credTop + new Vector3(-.10f, .024f, -.01f));
        VisualEngine3D.Sheet(credTop + new Vector3(.26f, .006f, .02f), -.18f, 0f, Finish.PitchDeck);
        VisualEngine3D.Box(LoungeTable+new Vector3(0,.365f,0),1.44f,.07f,.68f,new(131,87,54,255));
        foreach(float x in new[]{-.46f,.46f})
            VisualEngine3D.Box(LoungeTable+new Vector3(x,.165f,0),.11f,.33f,.54f,new(51,53,56,255));
        DrawPizza(LoungeTable+new Vector3(.31f,.4f,0),-13);
        VisualEngine3D.Sheet(LoungeTable+new Vector3(-.29f,.405f,.02f),.24f,0,Finish.PitchDeck);
        VisualEngine3D.Sheet(LoungeTable+new Vector3(-.37f,.41f,-.04f),-.17f,0,Finish.PitchDeck);
        VisualEngine3D.MountedWhiteboard(PrintBoardOrigin,-90);
        foreach(var table in new[]{BreakTable,ShaftCafeTable})
        {
            VisualEngine3D.Rod(table,table+Vector3.UnitY*.055f,.34f,.30f,20,new(72,54,34,255),Finish.Bronze);
            VisualEngine3D.Rod(table+Vector3.UnitY*.04f,table+Vector3.UnitY*.84f,.058f,.048f,16,new(166,130,74,255),Finish.Bronze);
            VisualEngine3D.Rod(table+Vector3.UnitY*.825f,table+Vector3.UnitY*.855f,.565f,.565f,24,new(166,130,74,255),Finish.Bronze);
            VisualEngine3D.Rod(table+Vector3.UnitY*.845f,table+Vector3.UnitY*.882f,.552f,.552f,24,new(40,37,42,255),Finish.BlackMarble);
        }
        // Cozy round startup scrum & coffee tables breaking up empty openspace bays!
        for(int si=0;si<OpenspaceScrumTables.Length;si++)
        {
            var st=OpenspaceScrumTables[si];
            VisualEngine3D.Rod(st,st+Vector3.UnitY*.05f,.36f,.34f,16,new(52,54,58,255));
            VisualEngine3D.Rod(st,st+Vector3.UnitY*.76f,.062f,.055f,12,new(154,118,76,255),Finish.Bronze);
            VisualEngine3D.Rod(st+Vector3.UnitY*.755f,st+Vector3.UnitY*.805f,.62f,.62f,22,new(142,96,64,255));
            // Two organically angled office chairs around each scrum table
            float baseAng = si * 47f + 22f;
            for(int ch=0;ch<2;ch++)
            {
                float rad=(baseAng + ch*155f + (ch==1 ? 18f : 0f)) * MathF.PI/180f;
                Vector3 cpos=st + new Vector3(MathF.Cos(rad)*.92f, 0, MathF.Sin(rad)*.92f);
                VisualEngine3D.Asset("prop_office_chair", cpos, -(baseAng + ch*155f) - 90f, .92f);
            }
            if(si%2==0) DrawPizza(st+new Vector3(.12f,.805f,-.08f), 19*si-11, .85f);
            else VisualEngine3D.OrientedBox(st+new Vector3(-.14f,.825f,.06f), new(.30f,.025f,.22f), .28f*si, new(56,64,78,255));
            DrawCoffee(st+new Vector3(-.24f,.805f,-.16f));
            DrawCoffee(st+new Vector3(.26f,.805f,.18f));
            VisualEngine3D.Sheet(st+new Vector3(-.05f,.810f,.22f), .32f-si*.14f, 0, Finish.PitchDeck);
        }
        DrawCoffee(ShaftCafeTable+new Vector3(.19f,.88f,.11f));
        DrawCoffee(ShaftCafeTable+new Vector3(-.18f,.88f,-.13f));
        VisualEngine3D.Sheet(new(-5.47f,.014f,-2.72f),.31f,0,Finish.JiraSheet);
        VisualEngine3D.Sheet(new(-5.65f,.012f,-2.55f),-.17f,0,Finish.NdaSheet);
        DrawPizza(BreakTable+Vector3.UnitY*.88f,-18);
        if (!BossArenaWallDestroyed)
        {
            VisualEngine3D.Box(ArenaTable+Vector3.UnitY*.74f,1.5f,.07f,.8f,new(126,88,61,255));
            foreach(float x in new[]{-.62f,.62f})
                VisualEngine3D.Box(ArenaTable+new Vector3(x,.36f,0),.09f,.72f,.63f,new(62,63,66,255));
            foreach(int ch in new[]{-1,1})
                VisualEngine3D.Asset("prop_office_chair", ArenaTable+new Vector3(ch*.38f,0,.60f), 180+ch*10f, .92f);
            DrawPizza(ArenaTable+Vector3.UnitY*.78f,7);
        }
        else
        {
            // Blasted & overturned out of the central combat lane onto the far right edge!
            Vector3 wreck = new(4.85f, 0f, -12.85f);
            VisualEngine3D.OrientedBox(wreck + new Vector3(0f, 0.36f, 0f), new(1.42f, 0.72f, 0.08f), 0.52f, new(62, 44, 34, 255));
            VisualEngine3D.OrientedBox(wreck + new Vector3(-0.32f, 0.14f, 0.24f), new(0.68f, 0.08f, 0.56f), -0.40f, new(44, 46, 50, 255));
            VisualEngine3D.Rod(wreck + new Vector3(0.45f, 0.05f, -0.18f), wreck + new Vector3(0.88f, 0.26f, -0.45f), 0.028f, 0.024f, 6, new(52, 54, 58, 255), Finish.Armor);
            VisualEngine3D.Asset("prop_office_chair", wreck + new Vector3(0.75f, 0f, 0.72f), 118f, .92f);
        }
        DrawCoffee(BreakTable+new Vector3(.34f,.88f,.08f));
        for(int i=0;i<5;i++)
            VisualEngine3D.Oval(new(4.97f+.075f*MathF.Cos(i*2),.40f+.024f*i,13.28f+.072f*MathF.Sin(i*2)),
                new(.08f,.067f,.072f),i==2 ? new(130,78,56,255) : new(212,205,183,255));
        foreach(var item in LooseClutter) item.Draw();
    }

    private void DrawSunDust(Camera3D camera)
    {
        Vector3 right=Vector3.Normalize(Vector3.Cross(camera.Target-camera.Position,Vector3.UnitY));
        for(int i=0;i<35;i++)
        {
            float t=_worldClock*.19f+i*2.39996f;
            Vector3 pos=new(-34+i%7*4.9f+MathF.Sin(t)*.43f,1+(i*19%38)*.12f+MathF.Sin(t*.8f)*.27f,
                -20+(i*31%45)+MathF.Cos(t)*.37f);
            float r=.009f+.002f*(i%4);
            Raylib.DrawTriangle3D(pos+Vector3.UnitY*r,pos-right*r,pos+right*r,new Color(255,215,157,90));
        }
    }

    private void DrawGalleyWorkstations3D()
    {
        for (int i = 0; i < GalleyDesks.Count; i++)
        {
            var d = GalleyDesks[i];
            Vector3 p = d.DeskPosition;

            DrawPod(p,i,d.IsOccupied);
            if (d.IsOccupied)
            {
                Color hoodie = d.WhackFlashTimer>0 ? Color.Gold : d.HoodieColor;
                Color visor = d.IsSlackingInTwitter ? new(255,174,82,255) : d.GlassesColor;
                float bob = d.IsSlackingInTwitter ? -.04f : .02f*MathF.Sin(d.TypingPhase*2.6f);
                VisualEngine3D.DrawSeatedCoder(d.SeatedFeet,180f,WorkstationScale,hoodie,visor,bob,d.SpeciesId,i);
            }
        }
    }

    private void DrawCentralMacBookTrapAndCovers3D()
    {
        if(Systems.PlacedBait is {} bait) VisualEngine3D.DrawFloorBait(bait.Position,bait.Tier,bait.Yaw,_worldClock);
        else if(Systems.HasWeapon(WeaponType.CaptureNet) && Systems.ActiveBaitTier<0) VisualEngine3D.DrawFloorBait(RefillPoint,0,MathF.PI,_worldClock);


        // 4. Ambush Cover Objects around Central Desk (Each a dedicated 3D Blender model!)
        for (int i = 0; i < AmbushCovers.Count; i++)
        {
            var cov = AmbushCovers[i];
            Vector3 p = cov.Position;

            switch (cov.VisualKind)
            {
                case 0: // Leather Sofa from the 90s
                    VisualEngine3D.Asset("prop_leather_sofa", p,180,SofaScale,stretch:SofaStretch);
                    break;
                case 1: // Potted Office Ficus Tree
                    VisualEngine3D.Asset("prop_ficus_tree", p);
                    break;
                case 2: // Office Water Cooler with 19L Blue Bottle & Taps
                    VisualEngine3D.DrawCooler(p,90f,CoolerEmpty(p));
                    break;
                case 3: // 42U Server Rack with Blade Units & LEDs
                    VisualEngine3D.Asset("prop_server_rack", p);
                    break;
            }
        }
    }

    private IEnumerable<(Vector3 Center,Vector3 Size)> LiftWalls()
    {
        Vector3 c=CentralElevatorCorePos;
        yield return (c+Vector3.UnitY*(CeilingHeight/2),new(9,CeilingHeight,.32f));
        foreach(int side in new[]{-1,1})
        {
            yield return (c+new Vector3(side*4.38f,CeilingHeight/2,0),new(.24f,CeilingHeight,9));
            // Grand 2.72m wide x 3.08m tall armored skyscraper elevator portal (zero boss head/shoulder/fin clipping!)
            yield return (c+new Vector3(0,(CeilingHeight+3.08f)/2,side*4.38f),new(9,CeilingHeight-3.08f,.24f));
            foreach(int end in new[]{-1,1})
                yield return (c+new Vector3(end*2.93f,1.54f,side*4.4f),new(3.14f,3.08f,.4f));
        }
    }

    private void BuildLift(VisualEngine3D.ArchitectureBuilder mesh)
    {
        Vector3 center=CentralElevatorCorePos;
        Color marble=new(34,33,38,255), darkStone=new(24,23,26,255);
        Color bronze=new(152,110,64,255);
        void Part(float x,float y,float z,float w,float h,float d,Color color,Finish finish=Finish.Paint) =>
            mesh.Add(center+new Vector3(x,y,z),new(w,h,d),color,finish);
        foreach(var wall in LiftWalls()) mesh.Add(wall.Center,wall.Size,marble,Finish.BlackMarble);
        foreach(int side in new[]{-1,1})
        {
            void Front(float x,float y,float depth,float w,float h,float d,Color c,Finish f=Finish.Paint) =>
                Part(x,y,side*(4.5f+depth),w,h,d,c,f);
            // Solid structural marble backing behind the sculpted 3D GLB lobby facade (zero gaps, 3.08m tall x 2.72m clear opening!)
            Front(0,4.54f,.02f,8.96f,2.92f,.68f,marble,Finish.BlackMarble);
            foreach(int end in new[]{-1,1})
                Front(end*2.93f,1.54f,.02f,3.14f,3.08f,.68f,marble,Finish.BlackMarble);
            // Architectural side shaft brass reveals
            for(int seam=-2;seam<=2;seam++)
                Part(side*4.50f,CeilingHeight/2,seam*2.13f,.045f,CeilingHeight,.052f,bronze,Finish.Bronze);
            Part(side*4.50f,.16f,0,.07f,.32f,9,bronze,Finish.Bronze);
            Part(side*4.50f,3.74f,0,.08f,.14f,9,bronze,Finish.Bronze);
            Part(side*4.50f,CeilingHeight-.14f,0,.09f,.22f,9,bronze,Finish.Bronze);
        }
        Part(0,.08f,0,8.62f,.16f,9,new(52,50,55,255),Finish.BlackMarble);
        Part(0,3.48f,0,8.62f,.16f,9,darkStone);
    }

    private Vector3 LiftLeafCenter(int side,int leaf) =>
        (side<0 ? NorthElevatorPos : SouthElevatorDoorPos)+new Vector3(leaf*(.65f+1.48f*ElevatorDoorOpenAmount),1.55f,side*.105f);

    private void DrawElevatorDoors(bool glass)
    {
        Color pane=Raylib.ColorLerp(new(112,86,62,135),new(28,30,34,244),ElevatorGlassOpacity);
        if (!glass && VisualEngine3D.HasAsset("elevator_cabin_interior"))
            VisualEngine3D.Asset("elevator_cabin_interior", CentralElevatorCorePos, 0f, 1f, stretch: new Vector3(1f, 1.24f, 1f));
        foreach(int side in new[]{-1,1})
        {
            Vector3 face=side<0 ? NorthElevatorPos : SouthElevatorDoorPos;
            Vector3 button=side<0 ? ElevatorButtonPos : SouthElevatorButtonPos;
            Vector3 panel=new(button.X,1.38f,face.Z+side*.374f);
            float faceYaw = side < 0 ? 180f : 0f;
            if (!glass)
            {
                if (VisualEngine3D.HasAsset("elevator_lobby_facade"))
                    VisualEngine3D.Asset("elevator_lobby_facade", face, faceYaw, 1f, stretch: new Vector3(1f, 1.24f, 1f));
                if (VisualEngine3D.HasAsset("elevator_floor_medallion"))
                    VisualEngine3D.Asset("elevator_floor_medallion", face + new Vector3(0f, 0.015f, side * 1.92f), faceYaw, 1f);
            }
            if (glass && !BrokenElevator) DrawShaftSmoke(face,side,inside:true);
            foreach(int leaf in new[]{-1,1})
            {
                Vector3 p=LiftLeafCenter(side,leaf);
                if(glass)
                    VisualEngine3D.Box(p,1.24f,2.86f,.032f,pane,Finish.LiftGlass);
                else if (VisualEngine3D.HasAsset("elevator_door_leaf"))
                    VisualEngine3D.Asset("elevator_door_leaf", p, faceYaw, 1f, stretch: new Vector3(1.10f, 1.24f, 1f));
            }
            if(glass)
            {
                DrawShaftSmoke(face,side,inside:false);
                DrawPanelSparks(panel,side);
                continue;
            }
            if (ElevatorRepaired && ElevatorGlassOpacity < 1)
                VisualEngine3D.Box(face+new Vector3(0,3.02f,-side*.55f),.52f,.08f,.14f,
                    Raylib.ColorLerp(new(45,25,25,255),new(251,58,42,255),1-ElevatorGlassOpacity),Finish.Lamp);
            VisualEngine3D.Box(LiftSignCenter(face,side),LiftSignSize.X,LiftSignSize.Y,LiftSignSize.Z,Color.White,Finish.Lift);
            DrawElevatorCallPanel(panel,side);
            DrawCabinOperatingPanel(CentralElevatorCorePos+new Vector3(1.52f,1.38f,side*4.166f),-side);
        }
    }

    private void DrawElevatorCallPanel(Vector3 panel,int side)
    {
        float yaw=side<0 ? 180f : 0f;
        VisualEngine3D.Asset(BrokenElevator ? "prop_elevator_panel_broken" : "prop_elevator_panel_fixed", panel, yaw, 1f);
        // Live pulsing indicator over the RFID reader strip when player has the Security Keycard.
        if(BrokenElevator && HasElevatorKey)
        {
            Vector3 fwd=Vector3.UnitZ*side;
            VisualEngine3D.Box(panel+Vector3.UnitY*(-.095f)+fwd*.0225f,.088f,.0075f,.005f,new(74,238,158,255),Finish.Lamp);
        }
    }

    private static void DrawCabinOperatingPanel(Vector3 panel,int side)
    {
        float yaw=side<0 ? 180f : 0f;
        VisualEngine3D.Asset("prop_elevator_cabin_panel", panel, yaw, 1f);
    }

    private void DrawShaftSmoke(Vector3 face,int side,bool inside)
    {
        var b=ElevatorBoss;
        float density=b.State switch {
            ElevatorBossState.Repairing => 1,
            ElevatorBossState.IntroSpeech => Math.Clamp(b.StateTimer/IntroSeconds,0,1),
            _ => BrokenElevator && !inside ? .23f : 0
        };
        if(density<=0) return;
        int count=BrokenElevator ? 10 : 20;
        if (CombatSprites.HasPaintedFx)
        {
            CombatSprites.Begin(BlendMode.Alpha);
            for (int i = 0; i < count; i++)
            {
                float age = (_worldClock * (inside ? 0.36f : 0.48f) + i * 0.381966f + (inside ? 0.33f : 0f)) % 1f;
                float wave = i * 2.4f + age * 2f;
                float radius = (0.18f + age * 0.44f) * (0.76f + density * 0.42f);
                float offset = inside ? -0.64f + 0.35f * MathF.Sin(wave) : 0.22f + age * (0.72f + density * 0.64f);
                float height = inside ? 0.42f + age * 2.25f : 0.16f + age * (0.58f + 0.30f * MathF.Cos(wave));
                Vector3 pos = face + new Vector3(MathF.Sin(i * 4.1f) * 1.25f + age * MathF.Cos(wave) * 0.36f, height, side * offset);
                int a = Math.Clamp((int)((68 + 86 * density) * MathF.Sin(MathF.PI * age) * Math.Min(1f, density * 5f)), 0, 255);
                Color tint = i % 3 == 0 ? new Color(214, 206, 194, a) : new Color(138, 138, 142, a);
                CombatSprites.SmokeFrame(i, pos, VisualEngine3D.ActiveCamRight, VisualEngine3D.ActiveCamUp,
                    radius * 1.12f, radius * 0.92f, tint, flipX: (i & 1) == 0, rotation: (age - 0.5f) * 0.4f + i * 0.25f);
            }
            CombatSprites.End();
            return;
        }
        for(int i=0;i<count;i++)
        {
            float age=(_worldClock*(inside ? .40f : .58f)+i*.381966f+(inside ? .33f : 0))%1;
            float wave=i*2.4f+age*2;
            float radius=(.13f+age*.55f)*(.78f+density*.54f);
            float offset=inside ? -.68f+.4f*MathF.Sin(wave) : .24f+age*(.88f+density*.84f);
            float height=inside ? .40f+age*2.4f : .14f+age*(.54f+.34f*MathF.Cos(wave));
            Vector3 pos=face+new Vector3(MathF.Sin(i*4.1f)*1.4f+age*MathF.Cos(wave)*.44f,height,side*offset);
            Color tint=i%3==0 ? new(201,192,174,255) : new(90,91,94,255);
            tint.A=(byte)((63+89*density)*MathF.Sin(MathF.PI*age)*Math.Min(1,density*5));
            VisualEngine3D.Oval(pos,new(radius*1.23f,radius*.84f,radius),tint,Finish.Cloud);
        }
    }

    private void DrawPanelSparks(Vector3 panel,int side)
    {
        bool repair=ElevatorBoss.State==ElevatorBossState.Repairing;
        if(!BrokenElevator && !repair) return;
        float period=repair ? .38f : 1.85f;
        float cycleTime=_worldClock+(side+1)*.41f;
        float burst=repair ? (RepairSeconds-ElevatorBoss.StateTimer)%period : cycleTime%period;
        if(burst>.26f || (repair && ElevatorBoss.StateTimer<1.25f)) return;
        int seed=(int)(cycleTime/period)*31+side*7;
        Vector3 origin=panel+new Vector3(0,-.202f,side*.034f);
        // Tiny localized arc flash right between the two copper wire tips during initial snap.
        if(burst<.06f)
            VisualEngine3D.Orb(origin,.0075f*(1f-burst/.06f),new(255,244,208,255),Finish.Lamp);
        int count=repair ? 14 : 8;
        for(int i=0;i<count;i++)
        {
            float h1=MathF.Sin(seed*12.9898f+i*78.233f)*43758.5453f; h1-=MathF.Floor(h1);
            float h2=MathF.Sin(seed*39.346f+i*11.135f)*24634.6345f; h2-=MathF.Floor(h2);
            float h3=MathF.Sin(seed*73.156f+i*52.235f)*19191.1919f; h3-=MathF.Floor(h3);
            float lag=h3*.07f;
            float t=burst-lag;
            if(t<=.006f || t>.22f) continue;
            float vx=(h1-.5f)*1.35f;
            float vy=.15f+h2*.75f;
            float vz=side*(.35f+h3*.85f);
            Vector3 vel=new(vx,vy-4.8f*t,vz);
            Vector3 pos=origin+new Vector3((h2-.5f)*.022f,(h1-.5f)*.012f,0)+new Vector3(vx*t,vy*t-2.4f*t*t,vz*t);
            Vector3 tail=pos-vel*.026f;
            Color spark=i%3==0 ? new(255,244,205,255) : new(255,186,78,255);
            VisualEngine3D.Rod(pos,tail,.0022f,.0007f,5,spark,Finish.Lamp);
        }
    }

    private static void GoldSpark(Vector3 center,float radius)
    {
        VisualEngine3D.Oval(center,new(radius*.34f,radius,radius*.34f),Color.Gold,Finish.Lamp);
        VisualEngine3D.Oval(center,new(radius,radius*.33f,radius*.33f),new(255,231,137,255),Finish.Lamp);
    }

    private static void Panic(VibeCoderEntity c)
    {
        c.State=CoderAIState.PanickedFleeing;
        c.SpeechBubble=(c.Id%3) switch {0=>"НЕ НАДО РЕМНЁМ!",1=>"Я ТОЛЬКО ДЖУН!",_=>"МОИ ТОКЕНЫ!"};
        c.SpeechTimer=2.4f;
        c.BaitRoute=null;
    }

    private static float HitRecoil(float timer, Vector3 center, Vector3 direction, float radius)
    {
        float t = Math.Clamp(timer / 0.24f, 0f, 1f);
        if (t > 0.35f && !VisualEngine3D.CastingShadows)
        {
            Vector3 dir = direction.LengthSquared() > 1e-6f ? Vector3.Normalize(direction) : Vector3.UnitZ;
            Vector3 impact = center - dir * radius;
            Vector3 up = MathF.Abs(dir.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
            Vector3 side = Vector3.Normalize(Vector3.Cross(up, dir));
            Vector3 orthoUp = Vector3.Cross(dir, side);

            // Crisp directional metallic/kinetic hit sparks (zero chunky yellow 4-petal banana star!)
            if (CombatSprites.HasPaintedFx)
            {
                CombatSprites.Begin(BlendMode.Additive);
                CombatSprites.ExplosionFrame(0, impact, VisualEngine3D.ActiveCamRight, VisualEngine3D.ActiveCamUp,
                    0.075f * t, (1f - t) * 7.5f, new Color(255, 224, 152, Math.Clamp((int)(195 * t), 0, 255)));
                CombatSprites.End();
            }
            VisualEngine3D.Orb(impact, 0.018f * t, new Color(255, 246, 212, 255), Finish.Lamp);
            for (int k = 0; k < 6; k++)
            {
                float a = k * MathF.Tau / 6f + (1f - t) * 3.2f + 0.4f;
                Vector3 ray = Vector3.Normalize(-dir * 0.68f + side * MathF.Cos(a) * 0.85f + orthoUp * MathF.Sin(a) * 0.85f);
                float len = (0.075f + 0.055f * (k % 2)) * t;
                Color col = (k % 2 == 0) ? new Color(255, 238, 176, 255) : new Color(255, 168, 56, 255);
                VisualEngine3D.Rod(impact, impact + ray * len, 0.0042f, 0.0009f, 4, col, Finish.Lamp);
            }
        }
        return t * t;
    }

    private void DrawVibeCoder3D(VibeCoderEntity c)
    {
        float scale=c.ModelScale;
        float bob=c.State==CoderAIState.PanickedFleeing && !c.CanSignNda ? c.Burst*.28f : 0f;
        float tilt=c.HitStaggerTimer>0 ? -.28f*Math.Min(1,c.HitStaggerTimer*6) : 0f;
        if(c.CanSignNda)
        {
            for(int i=0;i<3;i++)
            {
                float a=_worldClock*3.6f+i*MathF.Tau/3;
                Vector3 center=c.Position+scale*new Vector3(.24f*MathF.Cos(a),1.16f+.035f*MathF.Sin(a*2),.24f*MathF.Sin(a));
                GoldSpark(center,.030f*scale);
            }
        }
        Color tint=c.HitFlashTimer>0 ? Color.White : Raylib.ColorLerp(Color.White,c.HoodieColor,c.IsCombatCoder ? .87f : .36f);
        float recoil=HitRecoil(c.HitFlinchTimer,c.CombatCenter,c.HitDirection,.36f*scale);
        if(recoil>0) VisualEngine3D.SetHitFlash(recoil);
        VisualEngine3D.DrawCoder(c,c.Position+Vector3.UnitY*bob+c.HitDirection*(.13f*recoil),tilt-.32f*recoil,tint,_worldClock);
        if(recoil>0) VisualEngine3D.SetHitFlash(0);
        if(c.Archetype==CoderArchetype.SecurityGuard)
        {
            Vector3 At(float x,float y,float z)=>c.Position+Vector3.Transform(new Vector3(x,y+bob,z),Quaternion.CreateFromAxisAngle(Vector3.UnitY,c.FacingYaw));
            VisualEngine3D.Oval(At(0,.82f,.02f),new(.23f,.25f,.15f),new(43,52,62,255),Finish.Armor);
            var tip=At(.23f,c.Windup>0 ? 1.12f : .85f,.38f);
            VisualEngine3D.Rod(At(.21f,.72f,.13f),tip,.035f,.032f,10,Color.DarkGray);
            VisualEngine3D.Orb(tip,.035f,new(125,192,214,255),Finish.Lamp);
        }
        if(c.Ignition>0 && c.Hp>0)
            VisualEngine3D.Oval(c.CombatCenter+Vector3.UnitY*.12f,new(.12f,.17f+.04f*MathF.Sin(_worldClock*21),.1f),Color.Orange,Finish.Plasma);
        if(!VisualEngine3D.CastingShadows && c.Archetype==CoderArchetype.TeamLeadMeeter && c.State==CoderAIState.CombatHostile)
            Raylib.DrawCylinder(c.Position+Vector3.UnitY*.012f,1.15f,1.22f,.005f,20,new Color(88,205,126,34));
        if (c.IsCombatCoder && c.Windup>0)
        {
            Vector3 forward=new(MathF.Sin(c.FacingYaw),0,MathF.Cos(c.FacingYaw));
            float maxWindup = c.WindingSlap ? .34f : (c.Archetype == CoderArchetype.SecurityGuard ? .26f : .32f);
            float lift = Math.Clamp(1f - c.Windup / maxWindup, 0f, 1f);
            Vector3 hand=c.CombatCenter+forward*.24f+new Vector3(-forward.Z,.0f,forward.X)*.22f+Vector3.UnitY*(.20f+.24f*lift);
            float yawDeg = c.FacingYaw * 180f / MathF.PI;
            EnemyProjectileKind wk = ProjectileKindFor(c.Archetype);
            if (!c.WindingSlap && wk == EnemyProjectileKind.TaserDart)
            {
                if (VisualEngine3D.HasAsset("proj_taser_dart"))
                    VisualEngine3D.AssetOriented("proj_taser_dart", hand, yawDeg, -8f, 0f, 1.15f);
                // Crisp blue stun-prong warning arc telegraph so player reads the incoming taser shot!
                VisualEngine3D.Rod(hand - forward * 0.04f, hand + forward * (0.14f + 0.08f * lift), 0.010f, 0.004f, 6, new(115, 232, 255, 255), Finish.Plasma);
            }
            else if (!c.WindingSlap)
            {
                string asset = wk switch
                {
                    EnemyProjectileKind.CoffeeCup => "proj_coffee_cup",
                    EnemyProjectileKind.Keyboard  => "proj_keyboard",
                    _                             => "proj_laptop"
                };
                if (VisualEngine3D.HasAsset(asset))
                    VisualEngine3D.AssetOriented(asset, hand, yawDeg + lift * 36f, -24f * lift, 14f * lift, 0.96f);
                else
                    VisualEngine3D.OrientedBox(hand, new(0.24f, 0.14f, 0.18f), c.FacingYaw, new(185, 165, 138, 255));
            }
        }

    }

    private void DrawEscapedAgentBoss3D()
    {
        var b=ElevatorBoss;
        if (!BossModelVisible) return;
        Color tint=b.State==ElevatorBossState.Repairing ? new Color(73,65,70,255) : b.HitFlashTimer>0 ? new Color(255,225,235,255) : b.State==ElevatorBossState.StunnedWallCrash
            ? new Color(255,166,166,255) : Color.White;
        // 10-part hierarchical boss rig faces +Z, matching FacingYaw.
        float shutdown=b.IsDefeated ? Math.Clamp(1-Systems.ShutdownTime/.8f,0,1) : 0;
        float hitT=Math.Clamp(b.HitFlinchTimer/.24f,0,1);
        float recoil=hitT*hitT;
        if(recoil>0) VisualEngine3D.SetHitFlash(recoil);
        VisualEngine3D.DrawEscapedAgentBoss(b,b.LookTarget,_worldClock,ElevatorGlassOpacity,shutdown,recoil,
            Raylib.ColorLerp(tint,Color.Gray,shutdown));
        if(recoil>0) VisualEngine3D.SetHitFlash(0);
        // Crisp directional armor impact sparks tightly on the chest cuirass (no oversized cartoon star floating off elbow!)
        if(hitT>.42f && !VisualEngine3D.CastingShadows)
        {
            Vector3 hitDir=b.HitDirection.LengthSquared()>.01f ? Vector3.Normalize(b.HitDirection) : new Vector3(0,0,1);
            Vector3 impact=b.ChestCore - hitDir * .24f;
            Vector3 up=Vector3.UnitY;
            Vector3 side=Vector3.Normalize(Vector3.Cross(up, hitDir));
            if(side.LengthSquared()<.01f) side=Vector3.UnitX;
            VisualEngine3D.Orb(impact, .042f*hitT, new Color(255,244,195,255), Finish.Lamp);
            for(int s=0; s<5; s++)
            {
                float a = s * MathF.Tau / 5f + _worldClock * 11f;
                Vector3 ray = Vector3.Normalize(-hitDir * 0.72f + side * MathF.Cos(a) * 0.85f + up * MathF.Sin(a) * 0.85f);
                VisualEngine3D.Rod(impact, impact + ray * (.18f + .10f * (s % 2)) * hitT, .009f, .002f, 5,
                    s % 2 == 0 ? new Color(255, 186, 62, 255) : new Color(255, 232, 145, 255), Finish.Lamp);
            }
        }
        if(BossInCombat && b.ShieldHp>0 && !VisualEngine3D.CastingShadows)
        {
            // Sleek Art-Deco perimeter energy barrier framing System Zero without obscuring his armor silhouette
            float pulse = 0.72f + 0.28f * MathF.Sin(_worldClock * 4.5f);
            Color rimCol = new(118, 208, 232, (int)(128 * pulse));
            Color softCol = new(118, 208, 232, (int)(74 * pulse));
            Raylib.DrawCircle3D(b.Position + Vector3.UnitY * 0.06f, 1.32f, Vector3.UnitX, 90f, rimCol);
            Raylib.DrawCircle3D(b.Position + Vector3.UnitY * 0.14f, 1.40f, Vector3.UnitX, 90f, softCol);
            Raylib.DrawCircle3D(b.Position + Vector3.UnitY * 1.10f, 1.24f, Vector3.UnitX, 90f, softCol);
            Raylib.DrawCircle3D(b.Position + Vector3.UnitY * 3.02f, 0.94f, Vector3.UnitX, 90f, rimCol);
            for (int r = 0; r < 6; r++)
            {
                float a = _worldClock * 0.65f + r * MathF.Tau / 6f;
                Vector3 dir = new(MathF.Cos(a), 0f, MathF.Sin(a));
                Raylib.DrawLine3D(b.Position + dir * 1.32f + Vector3.UnitY * 0.06f,
                                  b.Position + dir * 1.24f + Vector3.UnitY * 1.10f, softCol);
            }
        }
    }

    private void DrawBossCutsceneDroppedGun3D()
    {
        if (!CutsceneShowDroppedGun) return;
        // Soft contact shadow + subtle warm firelight highlight pool on burgundy carpet (never a giant flat beige pancake!)
        Vector3 floorPos = new(CutsceneDroppedGunPos.X, 0.026f, CutsceneDroppedGunPos.Z);
        Raylib.DrawCylinder(floorPos, 0.24f, 0.28f, 0.003f, 18, new Color(14, 7, 9, 125));
        float glint = 0.50f + 0.50f * MathF.Sin(_worldClock * 6.5f);
        Raylib.DrawCylinder(new Vector3(CutsceneDroppedGunPos.X, 0.028f, CutsceneDroppedGunPos.Z),
            0.30f, 0.34f, 0.002f, 20, new Color(255, 198, 118, Math.Clamp((int)(20 + 14 * glint), 0, 255)));

        if (VisualEngine3D.HasAsset("weapon_shotgun_90s"))
        {
            // Slightly angled upward toward overhead lobby key light so walnut stock & gleaming steel barrels never turn black!
            VisualEngine3D.AssetOriented("weapon_shotgun_90s", CutsceneDroppedGunPos, CutsceneDroppedGunYaw, -12f, 72f, 1.02f);
        }

        Raylib.DrawCircle3D(new Vector3(CutsceneDroppedGunPos.X, 0.032f, CutsceneDroppedGunPos.Z),
            0.28f + 0.035f * glint, Vector3.UnitX, 90f,
            new Color(246, 196, 108, Math.Clamp((int)(36 + 34 * glint), 0, 255)));
    }

    private const float CutsceneMissileExplosionDur = 0.82f;

    private static float GetCutsceneMissileStagger(int idx) => idx switch
    {
        0 => 0.00f, // Hero kill-shot launches first so hero must dive into barrel roll!
        1 => 0.14f, // Right wall-breach rocket hits Z=-15.8 FIRST (t=0.72) to blow arena wide open live!
        2 => 0.25f, // Left wall-breach rocket finishes obliterating Z=-15.8 partition (t=0.80)
        _ => 0.38f  // Deep North Wing rocket punches through the newly opened breach (t=1.18)
    };

    private static float GetCutsceneMissileFlightDur(int idx) => idx switch
    {
        0 => 0.80f, // Long flight to hero's thrown carpet spot (Z=-19.50) -> impacts at t=0.80 (u=0.681)
        1 => 0.58f, // Short lethal punch straight into right half of Z=-15.8 wall -> impacts at t=0.72 (u=0.671)!
        2 => 0.55f, // Short lethal punch straight into left half of Z=-15.8 wall -> impacts at t=0.80 (u=0.681)!
        _ => 0.80f  // Deep punch straight through the newly blown opening into North Wing (Z=-22.00)!
    };

    private Vector3 GetCutsceneMissileTarget(int idx) => idx switch
    {
        // All 4 targets fan cleanly FORWARD (-Z) from TOKEN-GUN 98 right-arm cannon — ZERO chest clipping!
        // Missile 0 streaks DIRECTLY at hero's pre-dodge carpet spot (Z=-19.50) -> hero barrel-rolls LEFT!
        0 => NorthElevatorPos + new Vector3(+1.65f, 0.16f, -11.00f),
        // Missile 1 & 2 SLAM directly into the Z=-15.8 North Partition Wall, blowing the Boss Arena wide open!
        1 => NorthElevatorPos + new Vector3(+2.30f, 1.18f, -7.25f),
        2 => NorthElevatorPos + new Vector3(-2.15f, 1.25f, -7.25f),
        // Missile 3 streaks straight through the blasted breach deep into the expanded North Wing!
        _ => NorthElevatorPos + new Vector3(+3.60f, 0.20f, -13.50f)
    };

    private Vector3 ResolveCutsceneMissileOrigin(int idx)
    {
        if (!_cutsceneMissileFired[idx])
        {
            Vector3 fwd = VisualEngine3D.BossMuzzleValid && VisualEngine3D.BossMuzzleWorldDir.LengthSquared() > 1e-5f
                ? Vector3.Normalize(VisualEngine3D.BossMuzzleWorldDir)
                : -Vector3.UnitZ;
            _cutsceneMissileOrigins[idx] = VisualEngine3D.BossMuzzleValid
                ? VisualEngine3D.BossMuzzleWorldPos + fwd * 0.42f
                : ElevatorBoss.Position + new Vector3(+0.58f, 1.82f, -0.95f);
            _cutsceneMissileFired[idx] = true;
        }
        return _cutsceneMissileOrigins[idx];
    }

    private static Vector3 EvalCutsceneMissilePos(Vector3 start, Vector3 end, float u, int idx)
    {
        u = Math.Clamp(u, 0f, 1f);
        Vector3 axis = end - start;
        Vector3 fwd = axis.LengthSquared() > 1e-5f ? Vector3.Normalize(axis) : -Vector3.UnitZ;
        Vector3 side = Vector3.Cross(fwd, Vector3.UnitY);
        side = side.LengthSquared() > 1e-5f ? Vector3.Normalize(side) : Vector3.UnitX;

        float env = MathF.Sin(u * MathF.PI);
        // Missile 0 flies on a terrifying flat-dive trajectory straight at hero; wall-breach missiles arc cleanly forward!
        float arcLift = idx == 0 ? 0.34f * env : idx is 1 or 2 ? (0.36f + 0.12f * (idx - 1)) * env : 0.62f * env;
        float curve = idx == 0 ? 0.04f * env : idx == 1 ? 0.22f * env : idx == 2 ? -0.18f * env : 0.24f * env;
        return Vector3.Lerp(start, end, u) + Vector3.UnitY * arcLift + side * curve;
    }

    /// <summary>
    /// Renders the destructible central North Partition Wall (0, 2.1, -15.8; 8.2x4.2x0.24m).
    /// Before boss breach: seamless Art-Deco slate/brass/glass bay (with center glass cracked once hero crashes through).
    /// After boss rockets hit: jagged blasted wall stumps, twisted steel rebar, broken overhead soffit, smoking rubble & glass shards!
    /// </summary>
    private void DrawBossArenaPartition3D(bool glassPass)
    {
        Vector3 c = BossArenaWallCenter;
        Vector3 s = BossArenaWallSize;
        Color stone = new(216, 210, 200, 255);
        Color charStone = new(118, 114, 108, 255);
        Color charBlack = new(44, 42, 40, 255);
        Color brass = new(56, 60, 66, 255);
        Color darkBrass = new(42, 46, 52, 255);
        Color rebar = new(62, 54, 48, 255);
        Color glass = new(142, 174, 184, 96);

        if (!BossArenaWallDestroyed)
        {
            const float h = 1.18f;
            bool centerGlassShattered = BossArenaWallGlassShattered;
            if (!glassPass)
            {
                if (!centerGlassShattered)
                {
                    // Exact Art-Deco match to BuildInteriorRooms: dark slate wainscot + horizontal brass rails + 6 vertical mullions
                    VisualEngine3D.Box(new(c.X, h * 0.5f, c.Z), s.X, h, s.Z, stone, Finish.Slate);
                    foreach (float y in new[] { .08f, h, s.Y - .09f })
                        VisualEngine3D.Box(new(c.X, y, c.Z), s.X + .035f, .065f, s.Z + .035f, brass, Finish.Bronze);
                    const int count = 5;
                    for (int i = 0; i <= count; i++)
                    {
                        float x = -s.X * 0.5f + i * (s.X / count);
                        VisualEngine3D.Box(new(x, c.Y, c.Z), .065f, s.Y, .265f, brass, Finish.Bronze);
                    }
                }
                else
                {
                    // Hero slammed backward through the center (-1.35..+1.35), knocking a jagged V-breach in the wainscot & snapping center glass!
                    // Both massive Art-Deco flanking partition walls (-4.10..-1.35 and +1.35..+4.10) STILL STAND until Missiles 1 & 2 blow them apart!
                    VisualEngine3D.Box(new(-2.725f, h * 0.5f, c.Z), 2.75f, h, s.Z, stone, Finish.Slate);
                    VisualEngine3D.Box(new(+2.725f, h * 0.5f, c.Z), 2.75f, h, s.Z, stone, Finish.Slate);
                    // Jagged cracked lower sill across the center impact gap (Y <= 0.44m so full towering Boss & missiles read unobstructed!)
                    VisualEngine3D.Box(new(-1.12f, 0.36f, c.Z), 0.46f, 0.72f, s.Z, charStone, Finish.Slate);
                    VisualEngine3D.Box(new(+1.10f, 0.32f, c.Z), 0.48f, 0.64f, s.Z, charStone, Finish.Slate);
                    VisualEngine3D.Box(new(0.00f, 0.16f, c.Z), 1.82f, 0.32f, s.Z, charBlack, Finish.Slate);
                    // Top transom rail + flanking brass baseboards/chair rails
                    VisualEngine3D.Box(new(c.X, s.Y - .09f, c.Z), s.X + .035f, .065f, s.Z + .035f, brass, Finish.Bronze);
                    foreach (float y in new[] { .08f, h })
                    {
                        VisualEngine3D.Box(new(-2.725f, y, c.Z), 2.78f, .065f, s.Z + .035f, brass, Finish.Bronze);
                        VisualEngine3D.Box(new(+2.725f, y, c.Z), 2.78f, .065f, s.Z + .035f, brass, Finish.Bronze);
                    }
                    // Outer 4 mullions intact; inner 2 bent backward (-Z) at the smashed opening
                    foreach (float x in new[] { -4.10f, -2.46f, +2.46f, +4.10f })
                        VisualEngine3D.Box(new(x, c.Y, c.Z), .065f, s.Y, .265f, brass, Finish.Bronze);
                    VisualEngine3D.Rod(new(-1.35f, 0.72f, c.Z), new(-1.62f, 1.38f, c.Z - 0.32f), .024f, .018f, 6, darkBrass, Finish.Bronze);
                    VisualEngine3D.Rod(new(+1.35f, 0.64f, c.Z), new(+1.68f, 1.44f, c.Z - 0.36f), .024f, .018f, 6, darkBrass, Finish.Bronze);
                }
            }
            else
            {
                if (!centerGlassShattered)
                {
                    VisualEngine3D.Box(new(c.X, (h + s.Y) * 0.5f, c.Z), s.X, s.Y - h, s.Z, glass, Finish.Glass);
                }
                else
                {
                    // Outer glass bays intact while center 3.6m span (-1.80..+1.80) is blown wide open (only 4 subtle corner shards, zero fence pickets!)
                    VisualEngine3D.Box(new(-2.95f, (h + s.Y) * 0.5f, c.Z), 2.30f, s.Y - h, s.Z, glass, Finish.Glass);
                    VisualEngine3D.Box(new(+2.95f, (h + s.Y) * 0.5f, c.Z), 2.30f, s.Y - h, s.Z, glass, Finish.Glass);
                    // 4 subtle asymmetric clinging corner glass shards
                    VisualEngine3D.Box(new(-1.66f, 1.42f, c.Z), 0.28f, 0.44f, 0.04f, glass, Finish.Glass);
                    VisualEngine3D.Box(new(+1.64f, 1.36f, c.Z), 0.32f, 0.34f, 0.04f, glass, Finish.Glass);
                    VisualEngine3D.Box(new(-1.62f, 3.88f, c.Z), 0.34f, 0.40f, 0.04f, glass, Finish.Glass);
                    VisualEngine3D.Box(new(+1.66f, 3.92f, c.Z), 0.26f, 0.32f, 0.04f, glass, Finish.Glass);
                    // Shattered floor glass shards from hero crashing through
                    for (int sh = 0; sh < 12; sh++)
                    {
                        float sx = -1.65f + (sh * 0.31f % 3.30f);
                        float sz = c.Z - 0.55f - (sh * 0.24f % 2.15f);
                        VisualEngine3D.Box(new(sx, 0.022f, sz), 0.14f + (sh % 3) * 0.06f, 0.018f, 0.12f + (sh % 2) * 0.07f, glass, Finish.Glass);
                    }
                    // Active spray of glittering glass shards right as hero crashes through (u = 0.240..0.345)
                    if (ElevatorBoss.IsActive && ElevatorBoss.State == ElevatorBossState.IntroSpeech)
                    {
                        float u = Math.Clamp((IntroSeconds - ElevatorBoss.StateTimer) / IntroSeconds, 0f, 1f);
                        if (u >= 0.240f && u <= 0.355f)
                        {
                            float gt = (u - 0.240f) / 0.115f;
                            for (int k = 0; k < 14; k++)
                            {
                                float h1 = MathF.Sin(k * 17.3f + 2.1f) * 43758.5453f; h1 -= MathF.Floor(h1);
                                float h2 = MathF.Sin(k * 41.7f + 5.3f) * 24634.6345f; h2 -= MathF.Floor(h2);
                                float px = (h1 - 0.5f) * (1.8f + gt * 2.6f);
                                float py = MathF.Max(0.05f, 1.55f + h2 * 1.15f + gt * (0.8f - h1 * 0.6f) - 2.9f * gt * gt);
                                float pz = c.Z - 0.25f - gt * (2.6f + h2 * 3.4f);
                                VisualEngine3D.Box(new(px, py, pz), 0.14f, 0.08f, 0.04f, new Color(172, 218, 228, Math.Clamp((int)(190 * (1f - gt * 0.65f)), 0, 255)), Finish.Glass);
                            }
                        }
                    }
                }
            }
            return;
        }

        // =========================================================================
        // POST-BREACH: VISCERAL BLASTED-OUT WALL STUMPS, TWISTED REBAR, BROKEN SOFFIT & RUBBLE!
        // =========================================================================
        if (!glassPass)
        {
            // 1. LEFT JAGGED WALL STUMP (X = -4.10 .. -3.10, asymmetric fractured tiers + charred edges)
            VisualEngine3D.Box(new(-3.88f, 1.28f, c.Z), 0.46f, 2.56f, 0.26f, stone, Finish.Slate);
            VisualEngine3D.Box(new(-3.52f, 0.84f, c.Z), 0.40f, 1.68f, 0.25f, charStone, Finish.Slate);
            VisualEngine3D.Box(new(-3.22f, 0.41f, c.Z), 0.30f, 0.82f, 0.24f, charBlack, Finish.Slate);
            // Scorched blast char edge strips + broken brass baseboard & chair rail
            VisualEngine3D.Box(new(-3.64f, 2.12f, c.Z), 0.08f, 0.88f, 0.28f, charBlack, Finish.Slate);
            VisualEngine3D.Box(new(-3.31f, 1.24f, c.Z), 0.08f, 0.84f, 0.28f, charBlack, Finish.Slate);
            VisualEngine3D.Box(new(-3.62f, 0.08f, c.Z), 1.02f, 0.07f, 0.29f, darkBrass, Finish.Bronze);
            VisualEngine3D.Box(new(-3.76f, 1.18f, c.Z), 0.72f, 0.065f, 0.285f, darkBrass, Finish.Bronze);
            VisualEngine3D.Box(new(-4.08f, 2.10f, c.Z), 0.07f, 4.20f, 0.27f, brass, Finish.Bronze);
            // Bent snapped brass mullion thrust backward by rocket shockwave
            VisualEngine3D.Rod(new(-3.62f, 1.62f, c.Z), new(-2.86f, 2.18f, c.Z - 0.56f), 0.028f, 0.022f, 6, darkBrass, Finish.Bronze);
            // Twisted protruding steel rebar rods from left jagged concrete face
            VisualEngine3D.Rod(new(-3.65f, 2.28f, c.Z), new(-2.98f, 2.48f, c.Z - 0.24f), 0.015f, 0.011f, 6, rebar, Finish.Armor);
            VisualEngine3D.Rod(new(-3.45f, 1.54f, c.Z), new(-2.72f, 1.42f, c.Z - 0.36f), 0.014f, 0.010f, 6, rebar, Finish.Armor);
            VisualEngine3D.Rod(new(-3.35f, 1.12f, c.Z), new(-2.68f, 1.28f, c.Z + 0.18f), 0.013f, 0.010f, 6, rebar, Finish.Armor);
            VisualEngine3D.Rod(new(-3.16f, 0.64f, c.Z), new(-2.58f, 0.50f, c.Z - 0.28f), 0.014f, 0.009f, 6, rebar, Finish.Armor);
            VisualEngine3D.Rod(new(-3.82f, 2.52f, c.Z), new(-3.35f, 2.95f, c.Z - 0.22f), 0.013f, 0.010f, 6, rebar, Finish.Armor);

            // 2. RIGHT JAGGED WALL STUMP (X = +3.10 .. +4.10, taller asymmetric profile + charred edges)
            VisualEngine3D.Box(new(+3.86f, 1.42f, c.Z), 0.50f, 2.84f, 0.26f, stone, Finish.Slate);
            VisualEngine3D.Box(new(+3.48f, 0.96f, c.Z), 0.42f, 1.92f, 0.25f, charStone, Finish.Slate);
            VisualEngine3D.Box(new(+3.16f, 0.46f, c.Z), 0.32f, 0.92f, 0.24f, charBlack, Finish.Slate);
            VisualEngine3D.Box(new(+3.60f, 2.38f, c.Z), 0.08f, 0.94f, 0.28f, charBlack, Finish.Slate);
            VisualEngine3D.Box(new(+3.26f, 1.44f, c.Z), 0.08f, 0.96f, 0.28f, charBlack, Finish.Slate);
            VisualEngine3D.Box(new(+3.58f, 0.08f, c.Z), 1.06f, 0.07f, 0.29f, darkBrass, Finish.Bronze);
            VisualEngine3D.Box(new(+3.72f, 1.18f, c.Z), 0.78f, 0.065f, 0.285f, darkBrass, Finish.Bronze);
            VisualEngine3D.Box(new(+4.08f, 2.10f, c.Z), 0.07f, 4.20f, 0.27f, brass, Finish.Bronze);
            // Bent snapped brass mullion thrust backward
            VisualEngine3D.Rod(new(+3.56f, 1.88f, c.Z), new(+2.78f, 2.48f, c.Z - 0.62f), 0.028f, 0.022f, 6, darkBrass, Finish.Bronze);
            // Twisted protruding steel rebar rods from right concrete stump
            VisualEngine3D.Rod(new(+3.62f, 2.55f, c.Z), new(+2.92f, 2.74f, c.Z - 0.28f), 0.015f, 0.011f, 6, rebar, Finish.Armor);
            VisualEngine3D.Rod(new(+3.40f, 1.76f, c.Z), new(+2.66f, 1.62f, c.Z - 0.34f), 0.014f, 0.010f, 6, rebar, Finish.Armor);
            VisualEngine3D.Rod(new(+3.30f, 1.22f, c.Z), new(+2.64f, 1.38f, c.Z + 0.20f), 0.013f, 0.010f, 6, rebar, Finish.Armor);
            VisualEngine3D.Rod(new(+3.08f, 0.72f, c.Z), new(+2.48f, 0.56f, c.Z - 0.32f), 0.014f, 0.009f, 6, rebar, Finish.Armor);
            VisualEngine3D.Rod(new(+3.76f, 2.80f, c.Z), new(+3.22f, 3.16f, c.Z - 0.18f), 0.013f, 0.010f, 6, rebar, Finish.Armor);

            // 3. HEAVY BROKEN OVERHEAD HEADER BEAM / FRACTURED SOFFIT (Y = 3.45..4.20m — clear above player & 2.95m boss!)
            VisualEngine3D.Box(new(-2.82f, 3.84f, c.Z), 2.58f, 0.72f, 0.28f, stone, Finish.Slate);
            VisualEngine3D.Box(new(+2.82f, 3.81f, c.Z), 2.58f, 0.78f, 0.28f, stone, Finish.Slate);
            VisualEngine3D.Box(new(0.00f, 4.01f, c.Z), 3.16f, 0.38f, 0.28f, charStone, Finish.Slate);
            // Jagged hanging concrete teeth + scorched black underside along shattered arch
            VisualEngine3D.Box(new(-1.32f, 3.74f, c.Z), 0.62f, 0.32f, 0.26f, charBlack, Finish.Slate);
            VisualEngine3D.Box(new(-0.42f, 3.84f, c.Z), 0.58f, 0.22f, 0.26f, charBlack, Finish.Slate);
            VisualEngine3D.Box(new(+0.58f, 3.80f, c.Z), 0.66f, 0.26f, 0.26f, charBlack, Finish.Slate);
            VisualEngine3D.Box(new(+1.42f, 3.71f, c.Z), 0.56f, 0.36f, 0.26f, charBlack, Finish.Slate);
            // Sagging bent overhead brass cornice trim dangling across the breach
            VisualEngine3D.Rod(new(-4.05f, 4.10f, c.Z), new(-1.45f, 3.56f, c.Z - 0.16f), 0.032f, 0.028f, 6, darkBrass, Finish.Bronze);
            VisualEngine3D.Rod(new(-1.45f, 3.56f, c.Z - 0.16f), new(+0.95f, 3.44f, c.Z - 0.28f), 0.028f, 0.024f, 6, darkBrass, Finish.Bronze);
            VisualEngine3D.Rod(new(+1.65f, 3.52f, c.Z - 0.12f), new(+4.05f, 4.10f, c.Z), 0.028f, 0.032f, 6, darkBrass, Finish.Bronze);
            // Hanging exposed rebar rods curling down from shattered overhead soffit (all >= 3.22m high!)
            VisualEngine3D.Rod(new(-2.10f, 3.52f, c.Z), new(-1.72f, 3.26f, c.Z - 0.26f), 0.013f, 0.009f, 5, rebar, Finish.Armor);
            VisualEngine3D.Rod(new(-0.92f, 3.68f, c.Z), new(-0.68f, 3.34f, c.Z - 0.32f), 0.013f, 0.009f, 5, rebar, Finish.Armor);
            VisualEngine3D.Rod(new(+0.18f, 3.82f, c.Z), new(+0.42f, 3.44f, c.Z - 0.22f), 0.012f, 0.008f, 5, rebar, Finish.Armor);
            VisualEngine3D.Rod(new(+1.15f, 3.66f, c.Z), new(+0.88f, 3.28f, c.Z + 0.20f), 0.013f, 0.009f, 5, rebar, Finish.Armor);
            VisualEngine3D.Rod(new(+2.25f, 3.48f, c.Z), new(+1.92f, 3.24f, c.Z - 0.28f), 0.013f, 0.009f, 5, rebar, Finish.Armor);

            // 4. NON-BLOCKING LOW RUBBLE PILES, BROKEN BLACK-MARBLE/SLATE SLABS & TWISTED BRASS (Z = -14.2 .. -19.5)
            // Chunkier rubble mounds piled snugly around left & right stumps
            ReadOnlySpan<(Vector3 Pos, Vector3 Size, float Yaw, bool Charred)> stumpRubble =
            [
                (new(-3.65f, 0.14f, -15.32f), new(0.78f, 0.28f, 0.54f),  0.28f, false),
                (new(-3.18f, 0.10f, -16.22f), new(0.72f, 0.20f, 0.48f), -0.42f, true),
                (new(-2.68f, 0.07f, -15.65f), new(0.56f, 0.14f, 0.44f),  0.64f, false),
                (new(-3.82f, 0.11f, -16.68f), new(0.66f, 0.22f, 0.52f), -0.18f, true),
                (new(+3.62f, 0.15f, -15.28f), new(0.82f, 0.30f, 0.56f), -0.34f, false),
                (new(+3.12f, 0.11f, -16.28f), new(0.70f, 0.22f, 0.50f),  0.48f, true),
                (new(+2.62f, 0.07f, -15.72f), new(0.58f, 0.14f, 0.42f), -0.62f, false),
                (new(+3.85f, 0.10f, -16.75f), new(0.64f, 0.20f, 0.48f),  0.22f, true)
            ];
            for (int r = 0; r < stumpRubble.Length; r++)
                VisualEngine3D.OrientedBox(stumpRubble[r].Pos, stumpRubble[r].Size, stumpRubble[r].Yaw,
                    stumpRubble[r].Charred ? charBlack : charStone);

            // Organic clustered rubble fragments across Z = -14.2 .. -19.5 (varied jagged proportions & warm-stone/slate/charcoal tones — zero uniform square bricks!)
            Color warmStone = new(82, 74, 68, 255);
            ReadOnlySpan<(Vector3 Pos, Vector3 Size, float Yaw, int Tone)> floorSlabs =
            [
                (new(-2.15f, 0.044f, -14.78f), new(0.54f, 0.068f, 0.22f),  0.42f, 0),
                (new(-1.82f, 0.028f, -14.58f), new(0.22f, 0.036f, 0.18f), -0.75f, 3),
                (new(+2.28f, 0.046f, -14.95f), new(0.62f, 0.074f, 0.24f),  0.24f, 2),
                (new(+1.88f, 0.030f, -15.18f), new(0.26f, 0.040f, 0.16f), -0.62f, 1),
                (new(-1.25f, 0.034f, -15.62f), new(0.38f, 0.048f, 0.17f), -0.88f, 1),
                (new(+1.48f, 0.038f, -15.95f), new(0.46f, 0.056f, 0.20f),  0.71f, 3),
                (new(-2.42f, 0.050f, -16.75f), new(0.66f, 0.082f, 0.28f), -0.35f, 2),
                (new(-2.05f, 0.030f, -16.92f), new(0.24f, 0.042f, 0.19f),  0.84f, 0),
                (new(-0.62f, 0.030f, -16.65f), new(0.32f, 0.038f, 0.15f),  0.54f, 1),
                (new(+2.18f, 0.046f, -17.05f), new(0.58f, 0.070f, 0.23f), -0.44f, 0),
                (new(+1.80f, 0.028f, -17.32f), new(0.24f, 0.038f, 0.16f),  0.95f, 3),
                (new(-1.72f, 0.036f, -17.92f), new(0.44f, 0.052f, 0.19f),  0.92f, 2),
                (new(+0.85f, 0.030f, -18.22f), new(0.30f, 0.038f, 0.15f), -0.68f, 1),
                (new(+2.72f, 0.044f, -18.42f), new(0.52f, 0.068f, 0.25f),  0.33f, 0),
                (new(-2.82f, 0.046f, -18.65f), new(0.48f, 0.072f, 0.22f), -0.26f, 1),
                (new(-1.05f, 0.028f, -19.18f), new(0.28f, 0.036f, 0.16f),  0.48f, 3)
            ];
            for (int f = 0; f < floorSlabs.Length; f++)
            {
                Color col = floorSlabs[f].Tone switch { 0 => stone, 1 => charStone, 2 => charBlack, _ => warmStone };
                VisualEngine3D.OrientedBox(floorSlabs[f].Pos, floorSlabs[f].Size, floorSlabs[f].Yaw, col);
            }

            // Twisted fallen brass trim bars on floor around the breach
            VisualEngine3D.Rod(new(-2.45f, 0.04f, -16.15f), new(-1.25f, 0.04f, -17.05f), 0.024f, 0.022f, 6, darkBrass, Finish.Bronze);
            VisualEngine3D.Rod(new(+1.68f, 0.04f, -15.35f), new(+2.82f, 0.05f, -16.45f), 0.024f, 0.022f, 6, brass, Finish.Bronze);
            VisualEngine3D.Rod(new(-0.75f, 0.04f, -17.45f), new(+0.52f, 0.04f, -18.02f), 0.022f, 0.020f, 6, darkBrass, Finish.Bronze);
            VisualEngine3D.Rod(new(+0.85f, 0.04f, -14.78f), new(+1.95f, 0.04f, -14.42f), 0.022f, 0.020f, 6, darkBrass, Finish.Bronze);
        }
        else
        {
            // Jagged clinging glass corners on the outer edges of the blasted stumps
            VisualEngine3D.Box(new(-3.88f, 2.74f, c.Z), 0.32f, 0.42f, 0.05f, glass, Finish.Glass);
            VisualEngine3D.Box(new(+3.86f, 2.98f, c.Z), 0.34f, 0.46f, 0.05f, glass, Finish.Glass);
            // Sparkling shattered teal glass shards sprawled across Z = -14.3 .. -19.4
            for (int g = 0; g < 18; g++)
            {
                float h1 = MathF.Sin(g * 19.7f + 1.3f) * 43758.5453f; h1 -= MathF.Floor(h1);
                float h2 = MathF.Sin(g * 53.1f + 3.7f) * 24634.6345f; h2 -= MathF.Floor(h2);
                float gx = -3.45f + h1 * 6.90f;
                float gz = -14.30f - h2 * 5.10f;
                VisualEngine3D.Box(new(gx, 0.024f, gz), 0.14f + (g % 3) * 0.07f, 0.016f, 0.11f + (g % 4) * 0.05f, glass, Finish.Glass);
            }
        }
    }

    /// <summary>Draws solid depth-tested Blender 3D missiles (prop_token_missile.glb) + live wall-breach flying concrete/rebar burst!</summary>
    private void DrawBossCutsceneMissileMeshes3D()
    {
        if (CutsceneRocketTime < 0f || CutsceneRocketTime > 2.35f) return;

        for (int i = 0; i < 4; i++)
        {
            float localT = CutsceneRocketTime - GetCutsceneMissileStagger(i);
            if (localT < 0f) continue;

            float ti = localT / GetCutsceneMissileFlightDur(i);
            if (ti >= 1.0f) continue;

            Vector3 origin = ResolveCutsceneMissileOrigin(i);
            Vector3 target = GetCutsceneMissileTarget(i);
            Vector3 pos = EvalCutsceneMissilePos(origin, target, ti, i);
            Vector3 prevPos = EvalCutsceneMissilePos(origin, target, MathF.Max(0f, ti - 0.03f), i);
            Vector3 nextPos = EvalCutsceneMissilePos(origin, target, MathF.Min(1f, ti + 0.03f), i);
            Vector3 tangent = nextPos - prevPos;
            Vector3 dir = tangent.LengthSquared() > 1e-6f ? Vector3.Normalize(tangent) : Vector3.Normalize(target - origin);

            const float rad2Deg = 180f / MathF.PI;
            float yawDeg = MathF.Atan2(dir.X, dir.Z) * rad2Deg;
            float pitchDeg = -MathF.Asin(Math.Clamp(dir.Y, -0.999f, 0.999f)) * rad2Deg;
            float spinDeg = (localT * 520f + i * 75f) % 360f;

            if (VisualEngine3D.HasAsset("prop_token_missile"))
            {
                // Sleek realistic caliber sliding cleanly out of TOKEN-GUN 98 muzzle (scales 0.48 -> 0.86 over first 0.08 flight)
                float launchScale = 0.48f + 0.38f * Smooth(ti / 0.08f);
                VisualEngine3D.AssetOriented("prop_token_missile", pos, yawDeg, pitchDeg, spinDeg, launchScale);
            }
            else
            {
                VisualEngine3D.Rod(pos - dir * 0.32f, pos + dir * 0.24f, 0.055f, 0.012f, 10, new Color(196, 28, 30, 255), Finish.Armor);
            }
        }

        // LIVE WALL-BREACH FLYING CONCRETE, BRASS & GLASS DEBRIS BURST RIGHT AT (0, 1.5, -15.8) (t = 0.70 .. 1.65)!
        if (CutsceneRocketTime >= 0.70f && CutsceneRocketTime <= 1.68f)
        {
            float bt = CutsceneRocketTime - 0.70f;
            float fade = 1f - Smooth((bt - 0.72f) / 0.26f);
            if (fade > 0.02f)
            {
                Color stone = new(42, 46, 56, 255), charred = new(20, 19, 22, 255);
                Color brass = new(176, 136, 76, 255), glassShard = new(158, 206, 218, 220);
                for (int d = 0; d < 24; d++)
                {
                    float h1 = MathF.Sin(d * 12.9898f + 3.17f) * 43758.5453f; h1 -= MathF.Floor(h1);
                    float h2 = MathF.Sin(d * 78.233f + 7.41f) * 24634.6345f; h2 -= MathF.Floor(h2);
                    float h3 = MathF.Sin(d * 39.346f + 1.93f) * 19191.1919f; h3 -= MathF.Floor(h3);

                    float startX = -3.1f + h1 * 6.2f;
                    float startY = 0.45f + h2 * 2.45f;
                    float vx = (h1 - 0.5f) * 7.8f + (startX >= 0 ? 1.1f : -1.1f);
                    float vy = 2.6f + h3 * 5.2f;
                    // Most debris blasts violently NORTH (-Z) toward the expanded wing; some kicks back (+Z)
                    float vz = (d % 4 == 0 ? +1f : -1f) * (3.2f + h2 * 7.4f);

                    float px = startX + vx * bt;
                    float py = MathF.Max(0.06f, startY + vy * bt - 9.8f * bt * bt * 0.5f);
                    float pz = BossArenaWallCenter.Z + vz * bt;
                    float spin = bt * (6.5f + h3 * 9.0f) * ((d & 1) == 0 ? 1f : -1f);

                    if (d % 5 == 0)
                    {
                        Vector3 axis = new(MathF.Cos(spin), MathF.Sin(spin * 0.7f), MathF.Sin(spin));
                        VisualEngine3D.Rod(new Vector3(px, py, pz) - axis * 0.28f, new Vector3(px, py, pz) + axis * 0.28f,
                            0.022f * fade, 0.018f * fade, 5, brass, Finish.Bronze);
                    }
                    else
                    {
                        Vector3 chunk = (d % 3 == 0)
                            ? new Vector3(0.24f, 0.06f, 0.18f) * (0.75f + 0.45f * h3) * fade
                            : new Vector3(0.32f, 0.18f, 0.22f) * (0.70f + 0.55f * h2) * fade;
                        Color col = (d % 3 == 0) ? glassShard : (d % 2 == 0 ? stone : charred);
                        VisualEngine3D.OrientedBox(new(px, py, pz), chunk, spin, col);
                    }
                }
            }
        }
    }

    /// <summary>Draws painted billboard rocket thruster flames, curling smoke contrails, 4-stage painted explosions & wall-breach fireball burst.</summary>
    private void DrawBossCutsceneRocketFx3D(Camera3D camera)
    {
        if (CutsceneRocketTime < 0f || CutsceneRocketTime > 2.35f) return;

        Vector3 lookDir = camera.Target - camera.Position;
        lookDir = lookDir.LengthSquared() > 1e-6f ? Vector3.Normalize(lookDir) : -Vector3.UnitZ;
        Vector3 camRight = Vector3.Cross(lookDir, camera.Up);
        camRight = camRight.LengthSquared() > 1e-5f ? Vector3.Normalize(camRight) : Vector3.UnitX;
        Vector3 camUp = Vector3.Normalize(Vector3.Cross(camRight, lookDir));

        bool hasFx = CombatSprites.HasPaintedFx;

        // 1. Alpha-blended curling smoke contrails + explosion dark smoke plumes + wall-breach billowing dust wall
        if (hasFx) CombatSprites.Begin(BlendMode.Alpha);
        for (int i = 0; i < 4; i++)
        {
            float localT = CutsceneRocketTime - GetCutsceneMissileStagger(i);
            if (localT < 0f) continue;

            float flightDur = GetCutsceneMissileFlightDur(i);
            Vector3 origin = ResolveCutsceneMissileOrigin(i);
            Vector3 target = GetCutsceneMissileTarget(i);
            float ti = localT / flightDur;

            if (ti < 1.0f)
            {
                Vector3 pos = EvalCutsceneMissilePos(origin, target, ti, i);
                Vector3 prevPos = EvalCutsceneMissilePos(origin, target, MathF.Max(0f, ti - 0.03f), i);
                Vector3 dir = (pos - prevPos).LengthSquared() > 1e-6f ? Vector3.Normalize(pos - prevPos) : Vector3.Normalize(target - origin);

                const int smokeSteps = 12;
                for (int k = 1; k <= smokeSteps; k++)
                {
                    float tu = ti - k * 0.026f;
                    if (tu < 0f) break;
                    Vector3 sp = EvalCutsceneMissilePos(origin, target, tu, i);
                    float age = k / (float)smokeSteps;
                    float rSmoke = 0.14f + age * 0.34f;
                    int sAlpha = Math.Clamp((int)(178 * (1f - age * 0.88f)), 0, 255);
                    Vector3 drift = new(MathF.Sin(k * 1.9f + i) * 0.04f * age, 0.08f * age, MathF.Cos(k * 1.4f + i) * 0.04f * age);
                    if (hasFx)
                    {
                        CombatSprites.SmokeFrame(k + i, sp - dir * 0.42f + drift, camRight, camUp, rSmoke, rSmoke,
                            new Color(218, 206, 198, sAlpha), flipX: ((k + i) & 1) == 0, rotation: (k * 0.55f + i) * 0.4f);
                    }
                }
            }
            else
            {
                float expElapsed = localT - flightDur;
                if (expElapsed > CutsceneMissileExplosionDur) continue;
                float e = expElapsed / CutsceneMissileExplosionDur;
                float grow = 1f - MathF.Pow(1f - MathF.Min(1f, e * 2.1f), 3f);

                // Billowing painted charcoal smoke column rising above blast
                if (hasFx)
                {
                    float wallBoost = i is 1 or 2 ? 1.45f : 1.0f;
                    for (int sm = 0; sm < 4; sm++)
                    {
                        float ang = i * 1.4f + sm * MathF.Tau / 4f + e * 0.9f;
                        float spread = (0.32f + grow * 0.92f) * wallBoost;
                        Vector3 smokePos = target + new Vector3(
                            MathF.Cos(ang) * spread,
                            0.62f + e * (1.75f + 0.35f * sm),
                            MathF.Sin(ang) * spread);
                        float smokeR = (0.65f + grow * 0.98f) * wallBoost;
                        int smA = Math.Clamp((int)(215 * (1f - e * 0.72f)), 0, 255);
                        CombatSprites.SmokeFrame(sm + i, smokePos, camRight, camUp, smokeR, smokeR * 0.95f,
                            new Color(220, 212, 205, smA), flipX: (sm & 1) == 0, rotation: sm * 0.35f);
                    }
                }
            }
        }
        // Central wall-shatter dust & smoke billowing cloud across the full 8.2m Z=-15.8 breach (t = 0.72..1.65)
        if (hasFx && CutsceneRocketTime >= 0.72f && CutsceneRocketTime <= 1.68f)
        {
            float we = (CutsceneRocketTime - 0.72f) / 0.96f;
            float wGrow = 1f - MathF.Pow(1f - MathF.Min(1f, we * 2.2f), 3f);
            for (int cIdx = 0; cIdx < 5; cIdx++)
            {
                float bx = -2.6f + cIdx * 1.3f;
                Vector3 bPos = new(bx, 1.15f + we * (1.65f + 0.25f * (cIdx % 2)), BossArenaWallCenter.Z - 0.35f - we * 1.1f);
                float bR = 0.95f + wGrow * 1.45f;
                int bA = Math.Clamp((int)(210 * (1f - we * 0.78f)), 0, 255);
                CombatSprites.SmokeFrame(cIdx + 2, bPos, camRight, camUp, bR, bR * 0.92f,
                    new Color(204, 196, 192, bA), flipX: (cIdx & 1) == 1, rotation: cIdx * 0.4f + we * 0.5f);
            }
        }
        if (hasFx) CombatSprites.End();

        // 2. Additive luminous launch muzzle flare + missile thruster jets + 4-stage painted fireball explosions!
        if (hasFx) CombatSprites.Begin(BlendMode.Additive);
        for (int i = 0; i < 4; i++)
        {
            float localT = CutsceneRocketTime - GetCutsceneMissileStagger(i);
            if (localT < 0f) continue;

            float flightDur = GetCutsceneMissileFlightDur(i);
            Vector3 origin = ResolveCutsceneMissileOrigin(i);
            Vector3 target = GetCutsceneMissileTarget(i);
            float ti = localT / flightDur;

            // Brief painted muzzle ignition flash at TOKEN-GUN 98 barrel tip right as each rocket launches
            if (localT < 0.16f && hasFx)
            {
                float mf = 1f - localT / 0.16f;
                float flashR = 0.32f + 0.24f * (1f - mf);
                CombatSprites.ExplosionFrame(0, origin, camRight, camUp, flashR, localT * 12f + i,
                    new Color(255, 224, 168, Math.Clamp((int)(235 * mf), 0, 255)));
            }

            if (ti < 1.0f)
            {
                Vector3 pos = EvalCutsceneMissilePos(origin, target, ti, i);
                Vector3 prevPos = EvalCutsceneMissilePos(origin, target, MathF.Max(0f, ti - 0.03f), i);
                Vector3 dir = (pos - prevPos).LengthSquared() > 1e-6f ? Vector3.Normalize(pos - prevPos) : Vector3.Normalize(target - origin);
                Vector3 nozzlePos = pos - dir * 0.44f;
                float flicker = 0.84f + 0.16f * MathF.Sin(_worldClock * 85f + i * 2.3f);
                if (hasFx)
                {
                    // Compact nozzle ignition flare + directional horizontal thruster ribbon aligned strictly along -dir (ZERO vertical campfire flame!)
                    Vector3 jetMid = nozzlePos - dir * (0.34f * flicker);
                    Vector3 jetTip = nozzlePos - dir * (0.76f * flicker);
                    CombatSprites.ExplosionFrame(0, nozzlePos, camRight, camUp, 0.16f * flicker, _worldClock * 9f + i,
                        new Color(255, 224, 148, 235));
                    CombatSprites.RibbonSegment(nozzlePos, jetMid, camera.Position, 0.105f * flicker, 0.072f * flicker,
                        new Color(255, 246, 206, 245), new Color(255, 178, 68, 220));
                    CombatSprites.RibbonSegment(jetMid, jetTip, camera.Position, 0.076f * flicker, 0.012f,
                        new Color(255, 146, 48, 210), new Color(230, 52, 20, 0));
                }
            }
            else
            {
                float expElapsed = localT - flightDur;
                if (expElapsed > CutsceneMissileExplosionDur) continue;
                float e = expElapsed / CutsceneMissileExplosionDur;
                float grow = 1f - MathF.Pow(1f - MathF.Min(1f, e * 2.4f), 3f);
                float fade = 1f - Smooth((e - 0.32f) / 0.68f);

                if (hasFx)
                {
                    // Progress smoothly through all 4 hand-painted explosion stages: 0 (flash) -> 1 (blast) -> 2 (fireball) -> 3 (ember ring)
                    int stage = Math.Clamp((int)(e * 4f), 0, 3);
                    float wallScale = i is 1 or 2 ? 1.48f : (i == 0 ? 1.15f : 1.05f);
                    float blastR = wallScale + grow * (i is 1 or 2 ? 2.15f : 1.55f);
                    Vector3 center = target + Vector3.UnitY * (i is 1 or 2 ? (0.45f + e * 0.72f) : (0.68f + e * 0.65f));
                    int alpha = Math.Clamp((int)(250 * fade), 0, 255);
                    CombatSprites.ExplosionFrame(stage, center, camRight, camUp, blastR, i * 0.8f + e * 0.45f,
                        new Color(255, 240, 224, alpha), flipX: (i & 1) == 1);

                    // Secondary hot inner core bloom during early impact frames
                    if (e < 0.48f)
                    {
                        float cf = 1f - e / 0.48f;
                        CombatSprites.ExplosionFrame(0, target + Vector3.UnitY * 0.52f, camRight, camUp, blastR * 0.72f, -e * 3f + i,
                            new Color(255, 248, 220, Math.Clamp((int)(245 * cf), 0, 255)));
                    }
                }
            }
        }
        // Center wall-collapse fiery fireball bloom right at (0, 1.55, -15.8) so the entire partition visually vaporizes!
        if (hasFx && CutsceneRocketTime >= 0.72f && CutsceneRocketTime <= 1.54f)
        {
            float we = (CutsceneRocketTime - 0.72f) / 0.82f;
            float wGrow = 1f - MathF.Pow(1f - MathF.Min(1f, we * 2.5f), 3f);
            float wFade = 1f - Smooth((we - 0.28f) / 0.72f);
            int wStage = Math.Clamp((int)(we * 4f), 0, 3);
            float wR = 1.65f + wGrow * 2.35f;
            CombatSprites.ExplosionFrame(wStage, new Vector3(0f, 1.65f + we * 0.55f, BossArenaWallCenter.Z),
                camRight, camUp, wR, we * 0.6f, new Color(255, 236, 206, Math.Clamp((int)(248 * wFade), 0, 255)));
        }
        if (hasFx) CombatSprites.End();

        // 3. Expanding carpet scorch shockwave ring & sharp flying sparks
        for (int i = 0; i < 4; i++)
        {
            float localT = CutsceneRocketTime - GetCutsceneMissileStagger(i);
            float expElapsed = localT - GetCutsceneMissileFlightDur(i);
            if (expElapsed < 0f || expElapsed > CutsceneMissileExplosionDur) continue;
            float e = expElapsed / CutsceneMissileExplosionDur;
            Vector3 target = GetCutsceneMissileTarget(i);

            float ringR = 0.40f + e * (i is 1 or 2 ? 4.40f : 3.20f);
            Raylib.DrawCircle3D(new Vector3(target.X, 0.06f, target.Z), ringR, Vector3.UnitX, 90f,
                new Color(255, 118, 42, Math.Clamp((int)(195 * (1f - e)), 0, 255)));

            for (int sp = 0; sp < 10; sp++)
            {
                float sa = sp * MathF.Tau / 10f + i * 0.7f;
                float dist = 0.45f + e * (2.8f + 0.55f * (sp % 3));
                float sy = MathF.Max(0.05f, 0.36f + e * (2.1f + 0.45f * (sp % 2)) - 2.6f * e * e);
                Vector3 spPos = target + new Vector3(MathF.Cos(sa) * dist, sy, MathF.Sin(sa) * dist);
                Vector3 spTail = target + new Vector3(MathF.Cos(sa) * MathF.Max(0f, dist - 0.28f), MathF.Max(0.05f, sy - 0.12f), MathF.Sin(sa) * MathF.Max(0f, dist - 0.28f));
                Raylib.DrawLine3D(spTail, spPos, new Color(255, 222, 112, Math.Clamp((int)(235 * (1f - e)), 0, 255)));
            }
        }
    }

    private void DrawBossArenaChaos3D(Camera3D camera)
    {
        if (BossArenaChaos <= 0.01f) return;
        float chaos = Math.Clamp(BossArenaChaos, 0f, 1f);

        // 11 burning fire zones spanning the newly blasted Z=-15.8 wall breach AND the expanded North Wing (Z=-8.5..-24.5)!
        ReadOnlySpan<(Vector3 Pos, float Scale)> fires =
        [
            (NorthElevatorPos + new Vector3(+1.65f, 0f, -11.00f), 0.86f), // Exact crater hero barrel-rolled out of (Z=-19.50)!
            (new Vector3(-3.35f, 0f, -15.85f), 1.18f),                     // Blasted left wall stump crater!
            (new Vector3(+3.35f, 0f, -15.85f), 1.22f),                     // Blasted right wall stump crater!
            (NorthElevatorPos + new Vector3(+3.60f, 0f, -13.50f), 1.05f), // Missile 3 deep North Wing crater (Z=-22.00)!
            (new Vector3(-5.50f, 0f, -21.20f), 1.08f),                     // Expanded North-West arena flank flame
            (new Vector3(+5.65f, 0f, -18.60f), 1.02f),                     // Expanded North-East arena flank flame
            (NorthElevatorPos + new Vector3(-4.80f, 0f, -3.50f), 1.12f),
            (NorthElevatorPos + new Vector3(+4.90f, 0f, -3.60f), 1.15f),
            (new Vector3(-6.65f, 0f, -14.40f), 0.96f),
            (new Vector3(+6.65f, 0f, -14.40f), 0.98f),
            (NorthElevatorPos + new Vector3(-6.20f, 0f, -1.00f), 0.90f)
        ];

        Vector3 lookDir = camera.Target - camera.Position;
        lookDir = lookDir.LengthSquared() > 1e-6f ? Vector3.Normalize(lookDir) : -Vector3.UnitZ;
        // Cylindrical upright basis so floor fires ALWAYS burn vertically upward even during 360-deg barrel roll!
        Vector3 horizLook = new(lookDir.X, 0f, lookDir.Z);
        horizLook = horizLook.LengthSquared() > 1e-5f ? Vector3.Normalize(horizLook) : -Vector3.UnitZ;
        Vector3 fireRight = Vector3.Normalize(Vector3.Cross(horizLook, Vector3.UnitY));
        Vector3 fireUp = Vector3.UnitY;

        Vector3 camRight = Vector3.Cross(lookDir, camera.Up);
        camRight = camRight.LengthSquared() > 1e-5f ? Vector3.Normalize(camRight) : Vector3.UnitX;
        Vector3 camUp = Vector3.Normalize(Vector3.Cross(camRight, lookDir));

        // 1. Silky radially-feathered charred scorch & warm flickering floor firelight pools on carpet (ZERO hard circular discs!)
        CombatSprites.Begin(BlendMode.Alpha);
        for (int f = 0; f < fires.Length; f++)
        {
            Vector3 p = fires[f].Pos;
            float s = fires[f].Scale * (0.48f + 0.52f * chaos);
            CombatSprites.FloorGlow(p + new Vector3(0f, 0.008f, 0f), 1.15f * s,
                new Color(14, 9, 10, Math.Clamp((int)(155 * chaos), 0, 255)));
        }
        CombatSprites.End();
        CombatSprites.Begin(BlendMode.Additive);
        for (int f = 0; f < fires.Length; f++)
        {
            Vector3 p = fires[f].Pos;
            float phase = f * 1.93f;
            float flicker = 0.82f + 0.18f * MathF.Sin(_worldClock * (9.5f + f * 0.65f) + phase);
            float s = fires[f].Scale * (0.48f + 0.52f * chaos);
            CombatSprites.FloorGlow(p + new Vector3(0f, 0.012f, 0f), (1.85f + 0.22f * flicker) * s,
                new Color(255, 112, 26, Math.Clamp((int)(88 * chaos * flicker), 0, 255)));
            CombatSprites.FloorGlow(p + new Vector3(0f, 0.015f, 0f), 0.72f * s,
                new Color(255, 186, 78, Math.Clamp((int)(118 * chaos * flicker), 0, 255)));
        }
        CombatSprites.End();

        // 2. Hand-painted billowing dark soot smoke clouds rising behind/above each fire (ZERO geometric Oval eggs!)
        if (CombatSprites.HasPaintedFx)
        {
            CombatSprites.Begin(BlendMode.Alpha);
            for (int f = 0; f < fires.Length; f++)
            {
                Vector3 p = fires[f].Pos;
                float phase = f * 1.93f;
                float s = fires[f].Scale * (0.48f + 0.52f * chaos);

                for (int m = 0; m < 3; m++)
                {
                    float age = (_worldClock * 0.34f + f * 0.21f + m * 0.33f) % 1f;
                    float curl = 0.10f + age * 0.34f;
                    float curlAng = age * 4.4f + phase + m * 2.1f;
                    Vector3 puffPos = p + new Vector3(
                        MathF.Cos(curlAng) * curl,
                        0.92f * s + age * 2.55f,
                        MathF.Sin(curlAng) * curl);
                    float puffR = (0.34f + age * 0.54f) * s;
                    int smokeA = Math.Clamp((int)(136 * chaos * MathF.Sin(MathF.PI * age)), 0, 255);
                    CombatSprites.SmokeFrame(f + m, puffPos, camRight, camUp, puffR, puffR * 0.92f,
                        new Color(216, 208, 202, smokeA), flipX: ((f + m) & 1) == 0, rotation: (age - 0.5f) * 0.35f);
                }

                // 3 overlapping hand-painted animated roaring flame tongues per cluster (alpha base pass for vivid deep crimson/orange)
                for (int k = 0; k < 3; k++)
                {
                    float kAng = k * MathF.Tau / 3f + phase;
                    float offR = k == 0 ? 0f : 0.28f * s;
                    float speed = 9.5f + (k * 1.8f) + (f % 3) * 0.7f;
                    int frameIdx = (int)(_worldClock * speed + f * 3 + k * 2) & 3;
                    float hScale = (k == 0 ? 1.18f : 0.84f + 0.14f * (k % 2)) * s;
                    float wScale = (k == 0 ? 0.88f : 0.66f) * s;
                    float pulse = 0.92f + 0.10f * MathF.Sin(_worldClock * (11f + k * 2.3f) + phase);
                    float halfH = 0.82f * hScale * pulse;
                    float halfW = 0.70f * wScale;
                    Vector3 fPos = p + new Vector3(MathF.Cos(kAng) * offR, halfH * 0.76f, MathF.Sin(kAng) * offR);
                    CombatSprites.FireFrame(frameIdx, fPos, fireRight, fireUp, halfW, halfH,
                        new Color(255, 244, 232, Math.Clamp((int)(238 * chaos), 0, 255)), flipX: ((f + k) & 1) == 1);
                }
            }
            // Wispy dark smoke curling from the fractured overhead header beam (Y=3.45..4.10, Z=-15.8)
            if (BossArenaWallDestroyed)
            {
                for (int w = 0; w < 5; w++)
                {
                    float age = (_worldClock * 0.28f + w * 0.23f) % 1f;
                    float wx = -2.6f + w * 1.3f + MathF.Sin(_worldClock * 1.4f + w * 2.1f) * 0.18f;
                    Vector3 wPos = new(wx, 3.45f + age * 0.85f, BossArenaWallCenter.Z - 0.12f - age * 0.35f);
                    float wR = 0.36f + age * 0.42f;
                    int wA = Math.Clamp((int)(96 * chaos * MathF.Sin(MathF.PI * age)), 0, 255);
                    CombatSprites.SmokeFrame(w + 1, wPos, camRight, camUp, wR, wR * 0.85f,
                        new Color(188, 182, 178, wA), flipX: (w & 1) == 0, rotation: age * 0.4f);
                }
            }
            CombatSprites.End();

            // 3. Soft additive golden-orange flame glow core pass so fires truly illuminate & pop in the dark lobby!
            CombatSprites.Begin(BlendMode.Additive);
            for (int f = 0; f < fires.Length; f++)
            {
                Vector3 p = fires[f].Pos;
                float phase = f * 1.93f;
                float s = fires[f].Scale * (0.48f + 0.52f * chaos);
                int frameIdx = (int)(_worldClock * 12.2f + f * 5) & 3;
                float pulse = 0.88f + 0.14f * MathF.Sin(_worldClock * 13.5f + phase);
                float halfH = 0.76f * s * pulse;
                float halfW = 0.66f * s;
                Vector3 fPos = p + new Vector3(0f, halfH * 0.74f, 0f);
                CombatSprites.FireFrame(frameIdx, fPos, fireRight, fireUp, halfW, halfH,
                    new Color(255, 198, 128, Math.Clamp((int)(145 * chaos), 0, 255)), flipX: (f & 1) == 0);
            }
            CombatSprites.End();
        }

        // 4. 56 Drifting Glowing Embers & Ash Sparks floating upward across the ENTIRE expanded Boss Arena (Z = -8.5 .. -24.5)
        for (int i = 0; i < 56; i++)
        {
            float h1 = MathF.Sin(i * 12.9898f + 1.37f) * 43758.5453f; h1 -= MathF.Floor(h1);
            float h2 = MathF.Sin(i * 78.233f + 4.19f) * 24634.6345f; h2 -= MathF.Floor(h2);
            float h3 = MathF.Sin(i * 39.346f + 7.53f) * 19191.1919f; h3 -= MathF.Floor(h3);

            float speed = 0.17f + h3 * 0.19f;
            float cycle = (_worldClock * speed + h1 * 1.7f + h2) % 1f;
            float y = 0.2f + cycle * 4.0f;
            float x = -9f + h1 * 18f + MathF.Sin(_worldClock * (1.8f + h3) + i * 1.9f) * 0.55f;
            float z = -24.5f + h2 * 16.0f + MathF.Cos(_worldClock * (1.5f + h1) + i * 2.3f) * 0.45f;
            Vector3 ep = new(x, y, z);

            float lifeFade = MathF.Sin(cycle * MathF.PI);
            float twinkle = 0.65f + 0.35f * MathF.Sin(_worldClock * 11f + i * 3.1f);
            float size = (0.018f + 0.014f * (i % 3)) * (0.7f + 0.3f * lifeFade);

            Color col = (i % 4) switch
            {
                0 => new Color(255, 224, 118, Math.Clamp((int)(235 * chaos * lifeFade * twinkle), 0, 255)),
                1 => new Color(255, 118, 28, Math.Clamp((int)(225 * chaos * lifeFade * twinkle), 0, 255)),
                2 => new Color(255, 64, 22, Math.Clamp((int)(210 * chaos * lifeFade * twinkle), 0, 255)),
                _ => new Color(62, 54, 52, Math.Clamp((int)(168 * chaos * lifeFade), 0, 255))
            };
            Raylib.DrawTriangle3D(ep + Vector3.UnitY * size * 1.35f, ep - fireRight * size, ep + fireRight * size, col);
            Raylib.DrawTriangle3D(ep - Vector3.UnitY * size * 1.35f, ep + fireRight * size, ep - fireRight * size, col);
        }

        // 5. Emergency Overhead Crimson Beacon pulsing near the elevator arch while BossArenaChaos > 0.2f
        if (chaos > 0.2f)
        {
            float beaconFade = Smooth((chaos - 0.2f) / 0.45f);
            float strobe = 0.30f + 0.70f * MathF.Pow(0.5f + 0.5f * MathF.Sin(_worldClock * 7.8f), 2f);
            ReadOnlySpan<Vector3> beacons =
            [
                NorthElevatorPos + new Vector3(0f, 3.68f, -0.45f),
                NorthElevatorPos + new Vector3(-2.85f, 3.45f, -0.35f),
                NorthElevatorPos + new Vector3(+2.85f, 3.45f, -0.35f)
            ];
            for (int b = 0; b < beacons.Length; b++)
            {
                float bStrobe = b == 0 ? strobe : 0.30f + 0.70f * MathF.Pow(0.5f + 0.5f * MathF.Sin(_worldClock * 7.8f + MathF.PI), 2f);
                float strength = beaconFade * bStrobe;
                VisualEngine3D.Orb(beacons[b], b == 0 ? 0.08f : 0.06f,
                    new Color(255, 52, 42, Math.Clamp((int)(220 * strength), 0, 255)), Finish.Lamp);
            }
            CombatSprites.Begin(BlendMode.Additive);
            for (int b = 0; b < beacons.Length; b++)
            {
                float bStrobe = b == 0 ? strobe : 0.30f + 0.70f * MathF.Pow(0.5f + 0.5f * MathF.Sin(_worldClock * 7.8f + MathF.PI), 2f);
                float strength = beaconFade * bStrobe;
                CombatSprites.FloorGlow(new Vector3(beacons[b].X, 0.018f, beacons[b].Z - 0.85f), 1.95f,
                    new Color(220, 24, 30, Math.Clamp((int)(46 * strength), 0, 255)));
            }
            CombatSprites.End();
        }
    }

    private static void DrawFicus(Vector3 p)
    {
        VisualEngine3D.Rod(p,p+Vector3.UnitY*.52f,.28f,.36f,10,new Color(154,109,72,255));
        for (int i=0;i<5;i++)
        {
            float angle=i*2.4f;
            Vector3 leaf=p+new Vector3(.36f*MathF.Cos(angle),1.34f+i*.14f,.36f*MathF.Sin(angle));
            VisualEngine3D.Rod(p+Vector3.UnitY*.48f,leaf,.045f,.023f,7,new Color(99,76,52,255));
            VisualEngine3D.Oval(leaf,new(.38f,.48f,.32f),new Color(30+i*6,86+i*11,64+i*4,255));
        }
    }

    private void DrawEnemyProjectiles3D()
    {
        const float rad2Deg = 180f / MathF.PI;
        for (int i = 0; i < EnemyProjectiles.Count; i++)
        {
            var pr = EnemyProjectiles[i];
            Vector3 dir = pr.Velocity.LengthSquared() > 1e-6f ? Vector3.Normalize(pr.Velocity) : Vector3.UnitZ;
            Vector3 upRef = MathF.Abs(dir.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
            Vector3 side = Vector3.Normalize(Vector3.Cross(upRef, dir));
            Vector3 up = Vector3.Cross(dir, side);
            EnemyProjectileKind kind = ResolveProjectileKind(pr);

            switch (kind)
            {
                case EnemyProjectileKind.NodeModulesCrate:
                {
                    if (VisualEngine3D.HasAsset("prop_node_modules_crate"))
                    {
                        VisualEngine3D.AssetOriented("prop_node_modules_crate", pr.Position,
                            pr.Rotation.Y + _worldClock * 180f, pr.Rotation.X + 24f, pr.Rotation.Z + _worldClock * 120f, 1.05f);
                    }
                    else
                    {
                        VisualEngine3D.Box(pr.Position, 0.32f, 0.32f, 0.32f, new Color(34, 38, 46, 255), Finish.Armor);
                    }
                    Color trailCol = pr.Deflected ? new Color(255, 224, 96, 240) : new Color(255, 118, 46, 210);
                    VisualEngine3D.Rod(pr.Position - dir * 0.48f, pr.Position - dir * 0.14f, 0.014f, 0.002f, 5, trailCol, Finish.Lamp);
                    break;
                }

                case EnemyProjectileKind.CoffeeCup:
                {
                    // Spinning real 3D paper specialty coffee cup + hot espresso spray droplets trail!
                    if (VisualEngine3D.HasAsset("proj_coffee_cup"))
                    {
                        VisualEngine3D.AssetOriented("proj_coffee_cup", pr.Position,
                            pr.Rotation.Y, pr.Rotation.X, pr.Rotation.Z, 1.18f);
                    }
                    else
                    {
                        VisualEngine3D.Rod(pr.Position - up * 0.11f, pr.Position + up * 0.11f, 0.052f, 0.068f, 10, new(226, 208, 182, 255));
                        VisualEngine3D.Rod(pr.Position + up * 0.10f, pr.Position + up * 0.13f, 0.072f, 0.065f, 10, new(244, 242, 236, 255));
                    }
                    // Arcing dark hot-coffee droplets trailing behind the spinning cup
                    for (int d = 1; d <= 5; d++)
                    {
                        float t = d * 0.11f;
                        float swirl = _worldClock * 18f + d * 1.9f + i;
                        Vector3 dropPos = pr.Position - dir * (0.14f + t * 0.95f)
                            + side * MathF.Sin(swirl) * (0.04f + t * 0.12f)
                            + up * (MathF.Cos(swirl) * 0.04f - t * 0.14f);
                        float r = MathF.Max(0.010f, 0.024f - d * 0.003f);
                        VisualEngine3D.Oval(dropPos, new(r, r * 1.25f, r),
                            pr.Deflected ? new(255, 214, 88, 255) : new(62, 32, 16, 255),
                            pr.Deflected ? Finish.Lamp : Finish.Paint);
                    }
                    break;
                }

                case EnemyProjectileKind.Keyboard:
                {
                    // Spinning real 3D mechanical developer keyboard + flying keycap/paper flecks trail!
                    if (VisualEngine3D.HasAsset("proj_keyboard"))
                    {
                        VisualEngine3D.AssetOriented("proj_keyboard", pr.Position,
                            pr.Rotation.Y, pr.Rotation.X, pr.Rotation.Z, 1.12f);
                    }
                    else
                    {
                        VisualEngine3D.OrientedBox(pr.Position, new(0.42f, 0.04f, 0.16f), pr.Rotation.Y * MathF.PI / 180f, new(52, 56, 64, 255));
                    }
                    for (int k = 1; k <= 5; k++)
                    {
                        float t = k * 0.12f;
                        float ang = _worldClock * 14f + k * 2.3f + i;
                        Vector3 fleckPos = pr.Position - dir * (0.18f + t * 1.15f)
                            + side * MathF.Cos(ang) * (0.07f + t * 0.16f)
                            + up * MathF.Sin(ang * 1.3f) * (0.05f + t * 0.12f);
                        bool paper = (k % 2 == 0);
                        Color fleckCol = pr.Deflected ? new(255, 222, 102, 255) : (paper ? new(242, 236, 222, 255) : new(48, 52, 60, 255));
                        VisualEngine3D.OrientedBox(fleckPos, paper ? new(0.055f, 0.008f, 0.075f) : new(0.024f, 0.018f, 0.024f), ang, fleckCol);
                    }
                    break;
                }

                case EnemyProjectileKind.Laptop:
                {
                    // Tumbling open 3D developer laptop + sparking lithium battery & thin grey smoke trail!
                    float lapScale = pr.Radius > 0.30f ? 1.24f : 1.05f;
                    if (VisualEngine3D.HasAsset("proj_laptop"))
                    {
                        VisualEngine3D.AssetOriented("proj_laptop", pr.Position,
                            pr.Rotation.Y, pr.Rotation.X, pr.Rotation.Z, lapScale);
                    }
                    else
                    {
                        VisualEngine3D.OrientedBox(pr.Position, new(0.36f, 0.03f, 0.24f), pr.Rotation.Y * MathF.PI / 180f, new(162, 168, 176, 255));
                    }
                    for (int s = 1; s <= 4; s++)
                    {
                        float t = s * 0.14f;
                        float ang = _worldClock * 22f + s * 2.1f + i;
                        Vector3 sp = pr.Position - dir * (0.18f + t * 1.1f)
                            + side * MathF.Sin(ang) * 0.09f
                            + up * (0.04f * s + MathF.Cos(ang) * 0.06f);
                        if (s <= 2)
                        {
                            Vector3 tail = sp - dir * 0.11f + up * 0.03f;
                            VisualEngine3D.Rod(sp, tail, 0.007f, 0.002f, 4,
                                pr.Deflected ? Color.Gold : new(255, 176, 48, 255), Finish.Lamp);
                        }
                        else
                        {
                            float sr = 0.045f + s * 0.018f;
                            VisualEngine3D.Oval(sp, new(sr, sr * 0.85f, sr), new(126, 128, 132, 138), Finish.Cloud);
                        }
                    }
                    break;
                }

                case EnemyProjectileKind.TaserDart:
                {
                    // High-velocity nose-first 3D security taser dart + crackling blue taser wire arc + guard muzzle flash!
                    float yawDeg = MathF.Atan2(dir.X, dir.Z) * rad2Deg;
                    float pitchDeg = -MathF.Asin(Math.Clamp(dir.Y, -0.999f, 0.999f)) * rad2Deg;
                    if (VisualEngine3D.HasAsset("proj_taser_dart"))
                    {
                        VisualEngine3D.AssetOriented("proj_taser_dart", pr.Position, yawDeg, pitchDeg, 0f, 1.25f);
                    }
                    else
                    {
                        VisualEngine3D.Rod(pr.Position - dir * 0.14f, pr.Position + dir * 0.12f, 0.022f, 0.008f, 6, new(232, 194, 44, 255), Finish.Armor);
                    }
                    // Crackling blue taser wire arc trailing back toward guard origin (up to 3.4m)
                    Vector3 wireTailStart = pr.Position - dir * 0.14f;
                    Vector3 wireEnd = pr.Origin != Vector3.Zero
                        ? (Vector3.Distance(wireTailStart, pr.Origin) <= 3.4f ? pr.Origin : wireTailStart - dir * 3.4f)
                        : wireTailStart - dir * 2.6f;
                    Color arcCol = pr.Deflected ? new Color(255, 224, 96, 255) : new Color(112, 232, 255, 255);
                    Vector3 prev = wireTailStart;
                    for (int seg = 1; seg <= 6; seg++)
                    {
                        float f = seg / 6f;
                        float jig = MathF.Sin(_worldClock * 95f + seg * 3.7f + i * 2.1f) * 0.055f * MathF.Sin(f * MathF.PI);
                        float jigY = MathF.Cos(_worldClock * 84f + seg * 4.3f + i) * 0.045f * MathF.Sin(f * MathF.PI);
                        Vector3 pt = Vector3.Lerp(wireTailStart, wireEnd, f) + side * jig + up * jigY;
                        VisualEngine3D.Rod(prev, pt, 0.0065f, 0.0045f, 4, arcCol, Finish.Plasma);
                        prev = pt;
                    }
                    // Brief bright guard taser muzzle flash on launch (first 0.12s)
                    float age = pr.MaxLife - pr.Life;
                    if (age < 0.12f && pr.Origin != Vector3.Zero)
                    {
                        float mf = 1f - age / 0.12f;
                        VisualEngine3D.Orb(pr.Origin, 0.055f * mf, new(165, 244, 255, 255), Finish.Plasma);
                    }
                    break;
                }
            }

            // Crisp golden parry aura ring on deflected projectiles so player instantly sees their counter-rocket!
            if (pr.Deflected)
            {
                VisualEngine3D.Rod(pr.Position - dir * 0.42f, pr.Position - dir * 0.08f, 0.016f, 0.003f, 5, Color.Gold, Finish.Lamp);
            }
        }
    }

    private void DrawCombatDebris3D()
    {
        for (int i = 0; i < _combatDebris.Count; i++)
        {
            var d = _combatDebris[i];
            float fade = Math.Clamp(d.Life / MathF.Max(0.05f, d.MaxLife * 0.35f), 0f, 1f);
            Vector3 sz = d.Size * (0.45f + 0.55f * fade);
            if (d.Glowing)
            {
                Color c = new(d.Tint.R, d.Tint.G, d.Tint.B, Math.Clamp((int)(d.Tint.A * fade), 0, 255));
                VisualEngine3D.Rod(d.Position - d.Velocity * 0.025f, d.Position, sz.X * 0.35f, sz.X * 0.12f, 4, c, d.Surface);
            }
            else
            {
                VisualEngine3D.OrientedBox(d.Position, sz, d.Yaw, d.Tint);
            }
        }
    }

    private void DrawBossMeltdownAndWreckage3D(Camera3D camera)
    {
        Vector3 lookDir = camera.Target - camera.Position;
        lookDir = lookDir.LengthSquared() > 1e-6f ? Vector3.Normalize(lookDir) : -Vector3.UnitZ;
        Vector3 camRight = Vector3.Cross(lookDir, camera.Up);
        camRight = camRight.LengthSquared() > 1e-5f ? Vector3.Normalize(camRight) : Vector3.UnitX;
        Vector3 camUp = Vector3.Normalize(Vector3.Cross(camRight, lookDir));
        bool hasFx = CombatSprites.HasPaintedFx;

        // 1. Pre-detonation 1.45s reactor overload sparks & 3 staggered internal explosions across torso/shoulders!
        if (ElevatorBoss.IsActive && ElevatorBoss.State == ElevatorBossState.Dying)
        {
            float elapsed = Math.Clamp(MeltdownSeconds - ElevatorBoss.StateTimer, 0f, MeltdownSeconds);
            Vector3 bp = ElevatorBoss.Position;
            if (elapsed < 1.48f)
            {
                // Violent crimson reactor core flare pulsing through chest
                float corePulse = 0.65f + 0.35f * MathF.Sin(elapsed * 58f);
                VisualEngine3D.Orb(ElevatorBoss.ChestCore, (0.14f + 0.18f * (elapsed / 1.45f)) * corePulse,
                    new(255, 62, 28, 255), Finish.Lamp);
                // Erratic high-voltage overload arcs leaping off shoulders & head
                for (int a = 0; a < 5; a++)
                {
                    float ang = _worldClock * 17f + a * 1.25f;
                    Vector3 p0 = bp + new Vector3(MathF.Cos(ang) * 0.32f, 1.35f + (a % 3) * 0.42f, MathF.Sin(ang) * 0.32f);
                    Vector3 p1 = p0 + new Vector3(MathF.Cos(ang * 1.7f) * 0.55f, 0.25f * MathF.Sin(ang * 2.3f), MathF.Sin(ang * 1.7f) * 0.55f);
                    VisualEngine3D.Rod(p0, p1, 0.014f, 0.004f, 5,
                        a % 2 == 0 ? new(255, 224, 130, 255) : new(255, 76, 36, 255), Finish.Lamp);
                }

                // 3 staggered internal blasts at 0.25s, 0.75s, 1.25s
                ReadOnlySpan<(float T0, Vector3 Offset, float Scale)> blasts =
                [
                    (0.25f, new(-0.52f, 2.02f, +0.18f), 1.05f),
                    (0.75f, new(+0.52f, 2.12f, -0.14f), 1.22f),
                    (1.25f, new( 0.00f, 1.85f, +0.26f), 1.50f)
                ];
                if (hasFx) CombatSprites.Begin(BlendMode.Additive);
                for (int i = 0; i < blasts.Length; i++)
                {
                    float local = elapsed - blasts[i].T0;
                    if (local < 0f || local > 0.55f) continue;
                    float e = local / 0.55f;
                    float r = (0.45f + e * 1.15f) * blasts[i].Scale;
                    int alpha = Math.Clamp((int)(245 * (1f - e)), 0, 255);
                    Vector3 cPos = bp + blasts[i].Offset + Vector3.UnitY * (e * 0.35f);
                    if (hasFx)
                        CombatSprites.ExplosionFrame(Math.Clamp((int)(e * 4f), 0, 3), cPos, camRight, camUp, r, e * 3.5f + i,
                            new Color(255, 198, 118, alpha));
                    else
                        VisualEngine3D.Orb(cPos, r * 0.55f, new(255, 142, 46, alpha), Finish.Lamp);
                }
                if (hasFx) CombatSprites.End();
            }
        }

        // 2. MASSIVE FINAL DETONATION (1.45s -> +2.8s): multi-layer painted fireball, blinding shockwave ring & smoke column!
        if (_bossMeltdownBurstAge >= 0f && _bossMeltdownBurstAge < 2.8f)
        {
            float t = _bossMeltdownBurstAge;
            Vector3 center = BossWreckagePos + Vector3.UnitY * 1.25f;

            // Blinding expanding shockwave ring sweeping across the marble floor
            if (t < 0.75f)
            {
                float swProg = t / 0.75f;
                float ringR = 0.65f + (1f - MathF.Pow(1f - swProg, 2.4f)) * 6.8f;
                int ringA = Math.Clamp((int)(235 * (1f - swProg)), 0, 255);
                Raylib.DrawCircle3D(BossWreckagePos + Vector3.UnitY * 0.25f, ringR, Vector3.UnitX, 90f, new Color(255, 226, 152, ringA));
                Raylib.DrawCircle3D(BossWreckagePos + Vector3.UnitY * 0.62f, ringR * 0.86f, Vector3.UnitX, 90f, new Color(255, 132, 46, (int)(ringA * 0.85f)));
            }

            // Rising dark blast smoke plume
            if (hasFx)
            {
                CombatSprites.Begin(BlendMode.Alpha);
                for (int sm = 0; sm < 6; sm++)
                {
                    float ang = sm * MathF.Tau / 6f + t * 0.6f;
                    float spread = 0.45f + MathF.Min(1f, t * 1.4f) * 1.65f;
                    Vector3 sp = BossWreckagePos + new Vector3(MathF.Cos(ang) * spread, 0.85f + t * (1.45f + (sm % 3) * 0.35f), MathF.Sin(ang) * spread);
                    float sr = 1.15f + t * 1.25f;
                    int sa = Math.Clamp((int)(225 * (1f - t / 2.8f)), 0, 255);
                    CombatSprites.SmokeFrame(sm, sp, camRight, camUp, sr, sr * 0.92f, new Color(56, 50, 48, sa),
                        flipX: (sm & 1) == 0, rotation: sm * 0.5f + t * 0.4f);
                }
                CombatSprites.End();

                // Multi-layer painted fireball core + outer inferno petals!
                if (t < 1.55f)
                {
                    float e = t / 1.55f;
                    float grow = 1f - MathF.Pow(1f - MathF.Min(1f, e * 2.6f), 3f);
                    float fade = 1f - Smooth((e - 0.28f) / 0.72f);
                    int stage = Math.Clamp((int)(e * 4f), 0, 3);
                    CombatSprites.Begin(BlendMode.Additive);
                    CombatSprites.ExplosionFrame(stage, center + Vector3.UnitY * (e * 1.1f), camRight, camUp,
                        1.45f + grow * 2.55f, e * 2.4f, new Color(255, 182, 86, Math.Clamp((int)(255 * fade), 0, 255)));
                    CombatSprites.ExplosionFrame(Math.Max(0, stage - 1), center + Vector3.UnitY * (0.4f + e * 0.6f), camRight, camUp,
                        0.95f + grow * 1.75f, -e * 3.1f, new Color(255, 242, 188, Math.Clamp((int)(245 * fade * fade), 0, 255)));
                    CombatSprites.End();
                }
            }
        }

        // 3. SMOKING COLLAPSED CHARRED BOSS TORSO WRECKAGE on the marble floor (persists after detonation!)
        if (BossWreckageActive)
        {
            Vector3 wp = BossWreckagePos;
            float wy = BossWreckageYaw;
            Color charBlack = new(24, 22, 24, 255);
            Color scorchTitanium = new(62, 60, 64, 255);
            Color burntCeramic = new(96, 88, 84, 255);
            Color emberRed = new(238, 68, 28, 255);

            // Blackened blast scorch crater on the marble floor
            Raylib.DrawCylinder(new(wp.X, 0.016f, wp.Z), 1.85f, 1.95f, 0.004f, 22, new Color(18, 15, 16, 210));
            Raylib.DrawCylinder(new(wp.X, 0.021f, wp.Z), 0.95f, 1.05f, 0.004f, 18, new Color(12, 10, 10, 235));

            // Collapsed charred titanium torso chassis lying tilted on floor + glowing dying core crack
            VisualEngine3D.OrientedBox(wp + new Vector3(0.0f, 0.24f, 0.0f), new(0.84f, 0.42f, 0.96f), wy + 0.32f, scorchTitanium);
            VisualEngine3D.OrientedBox(wp + new Vector3(-0.18f, 0.34f, 0.14f), new(0.56f, 0.26f, 0.48f), wy - 0.18f, burntCeramic);
            VisualEngine3D.OrientedBox(wp + new Vector3(0.12f, 0.44f, 0.06f), new(0.24f, 0.08f, 0.28f), wy + 0.32f, emberRed);
            // Severed shoulder pauldron, snapped TOKEN-GUN 98 barrel & cracked visor helmet resting beside torso
            VisualEngine3D.OrientedBox(wp + new Vector3(-0.82f, 0.14f, 0.36f), new(0.42f, 0.24f, 0.36f), wy + 0.95f, charBlack);
            VisualEngine3D.Rod(wp + new Vector3(0.45f, 0.10f, -0.35f), wp + new Vector3(1.18f, 0.12f, -0.78f), 0.065f, 0.052f, 8, scorchTitanium, Finish.Armor);
            VisualEngine3D.Oval(wp + new Vector3(0.52f, 0.18f, 0.54f), new(0.19f, 0.17f, 0.22f), burntCeramic);
            VisualEngine3D.Box(wp + new Vector3(0.54f, 0.20f, 0.72f), 0.22f, 0.045f, 0.04f,
                new Color(220, 32, 32, (int)(135 + 75 * MathF.Sin(_worldClock * 6.5f))), Finish.Lamp);

            // Steady curling dark reactor smoke + occasional dying ember spark rising from charred wreckage
            if (hasFx)
            {
                CombatSprites.Begin(BlendMode.Alpha);
                for (int s = 0; s < 5; s++)
                {
                    float age = (_worldClock * 0.42f + s * 0.2f) % 1f;
                    Vector3 sp = wp + new Vector3(MathF.Sin(s * 2.1f + age * 2.5f) * 0.26f, 0.35f + age * 2.45f, MathF.Cos(s * 1.7f + age * 2.1f) * 0.24f);
                    float r = 0.24f + age * 0.56f;
                    int a = Math.Clamp((int)(148 * MathF.Sin(age * MathF.PI)), 0, 255);
                    CombatSprites.SmokeFrame(s, sp, camRight, camUp, r, r, new Color(72, 68, 66, a),
                        flipX: (s & 1) == 0, rotation: age * 0.6f + s * 0.4f);
                }
                CombatSprites.End();
            }
            float spAge = (_worldClock * 1.4f) % 1f;
            if (spAge < 0.35f)
            {
                Vector3 sparkP = wp + new Vector3(0.10f, 0.48f + spAge * 1.1f, 0.06f);
                VisualEngine3D.Rod(sparkP, sparkP + new Vector3(0.06f, 0.14f, -0.05f), 0.008f, 0.002f, 4, new(255, 188, 64, 255), Finish.Lamp);
            }
        }
    }

    // =========================================================================
    // 2D WORLD-TO-SCREEN LABELS, HEALTH BARS, ANXIETY BARS & INTERACTION HINTS
    // =========================================================================
    private enum FocusKind { None, Terminal, Frog, Offer, MacBook, Veronika, Shop, Elevator, ElevatorEscape, Desk, Coder, Board, Barrier, Pizza, Cooler, Coffee, Rack, Printer, Deploy, Extinguisher }
    private readonly record struct Focus(FocusKind Kind, string Label, int Index);
    internal bool CanCarryWith(WeaponType tool) => tool==WeaponType.BareHands || Systems.Has(Sidegrade.CarryHarness);

    private string PickUp(VibeCoderEntity c,Action<int,string>? grant,WeaponType tool)
    {
        if (CarriedCoder != null) return "Сначала отведи пойманного кодера к Веронике или в Опенспейс";

        Systems.Style(StyleAction.Capture);
        Systems.Risk(security:.5f+(Systems.Has(Sidegrade.CarbonNda) ? 6 : 0));
        if(!c.IdentityRevealed && !c.IsMiniBoss)
        {
            // Slots from before the collection had archetypes but no species IDs.
            var species=VibeCoderCollection.All.FirstOrDefault(s=>s.Id==c.SpeciesId)
                ?? VibeCoderCollection.All.First(s=>s.Archetype==c.Archetype);
            var capturedRarity=c.Rarity;
            VibeCoderCollection.Dress(c,species);
            if(c.FloorHunter) c.Rarity=capturedRarity;
            c.Name=species.Name;
            Systems.Discover(species,grant);
        }
        c.IdentityRevealed=true;
        LootElevatorKey(c);
        ActiveCoders.Remove(c);
        if(c.FloorHunter && Systems.PlacedBait?.HunterId==c.Id) Systems.PlacedBait=null;
        if(c.Archetype==CoderArchetype.SecurityGuard)
        { Systems.Protection=true; return "Начальник СБ прикрывает смену. −1,2 СБ/с"; }
        CarriedCoder = c;
        AudioMusicSystem.TriggerVoiceLine(
            "КОММЕРС 90-Х",
            $"Поднял кандидата «{c.Name}»! Неси Веронике (HR-Тарологу) за ${c.TarotPayoutValue} или сажай в Опенспейс!",
            (int)SpeakerVoiceType.Boss90s);
        return $"СХВАЧЕН: {c.Name}! Неси в будку HR-Таролога Вероники (слева на Юге) или на стол Опенспейса в западном крыле!";
    }

    public string Interact(Camera3D camera, Func<int,bool> spend, Action<int,string> grant, WeaponType tool=WeaponType.BareHands)
        => Interact(camera,spend,grant,tool,null);

    internal string Interact(Camera3D camera,Func<int,bool> spend,Action<int,string> grant,WeaponType tool,Action<FloorPanel>? openPanel)
    {
        Focus focus = FindFocus(camera,tool);
        FloorPanel panel=FocusedPanel(focus.Kind);
        if(openPanel!=null && panel!=FloorPanel.None)
        {
            openPanel(panel);
            return "";
        }
        switch (focus.Kind)
        {
            case FocusKind.Veronika when CarriedCoder!=null: return SellCarriedCoderToHrVeronika(grant);
            case FocusKind.Veronika when Systems.Onboarding==OnboardingStep.TalkToVeronika:
                Systems.Onboarding=OnboardingStep.CatchFrogs;
                Systems.Coach();
                return "";
            case FocusKind.Terminal:
            case FocusKind.Veronika when Systems.PocketFrogs.Count>0: return DepositBugs(grant);
            case FocusKind.Frog: AudioMusicSystem.PlayHandGrab(); return CatchSweep(camera,false,interact:true);
            case FocusKind.Offer:
                var candidate=ActiveCoders[focus.Index];
                if(!spend(80)) return "На аванс нужно $80";
                if(candidate.Archetype==CoderArchetype.SecurityGuard) return PickUp(candidate,grant,tool);
                candidate.Pressure(24,0,-6,45);
                return candidate.State==CoderAIState.Subdued ? "Аванс принят. Подпиши NDA [E]" : "Кандидат изучает оффер";
            case FocusKind.Board:
                return "Доска контрактов [J]";
            case FocusKind.Barrier:
                if(Systems.BarrierVoiceWait<=0) { Systems.BarrierVoiceWait=25; FloorSystems.SpeakCoaching(FloorSystems.LockedLine); }
                return BarrierHint;
            case FocusKind.Pizza:
                if(Systems.StationWait.ContainsKey(focus.Index)) return "Пицца закончилась";
                Systems.StationWait[focus.Index]=20;
                return Program.Heal(35,0,"ПИЦЦА");
            case FocusKind.Cooler when CoolerEmpty(stations[focus.Index].Feet): return Loc.CoolerEmpty;
            case FocusKind.Cooler when Systems.StationWait.ContainsKey(focus.Index): return "Кулер наполняется";
            case FocusKind.Cooler when Program.NeedsHealing:
                if((Systems.CoolerDrinks[focus.Index]=Systems.CoolerDrinks.GetValueOrDefault(focus.Index)+1)==2)
                    Systems.StationWait[focus.Index]=25;
                return Program.Heal(25,30,"ВОДА");
            case FocusKind.Cooler: return Spill(stations[focus.Index].Feet);
            case FocusKind.Coffee:
                if(Program.NeedsHealing || Systems.Onboarding==OnboardingStep.HealAtCoffee || !Systems.WantsCoffee)
                {
                    if(Systems.EspressoWait>0) return $"Эспрессо через {MathF.Ceiling(Systems.EspressoWait)} с";
                    Systems.EspressoWait=12; Program.RefillCoffeeCup();
                    Systems.Coffee=12; Systems.RestoreMorale(this);
                    return Program.Heal(45,50,"ЭСПРЕССО");
                }
                Systems.Steam=8; Systems.Coffee=12; Systems.RestoreMorale(this);
                return "Эспрессо-разнос на 12 секунд";
            case FocusKind.Extinguisher: return ExtinguishRack();
            case FocusKind.Rack: return Systems.Reboot(grant);
            case FocusKind.Deploy: return Systems.Deploy(this);
            case FocusKind.Printer: return Systems.Print();
            case FocusKind.Coder: return PickUp(ActiveCoders[focus.Index],grant,tool);
            case FocusKind.Veronika: return VeronikaShop;
            case FocusKind.Desk:
                var desk = GalleyDesks[focus.Index];
                if (desk.IsOccupied && CarriedCoder!=null) desk.IsOccupied=false; // The outgoing worker ends their shift.
                if (desk.IsOccupied) return $"{desk.CoderName}: ставка ${desk.Productivity}/с, мораль {desk.Morale:P0}, выгорание {desk.Burnout:F0}%";
                return SeatCarriedCoderAtGalley(focus.Index);
            case FocusKind.MacBook:
                return RecoverBait();
            case FocusKind.ElevatorEscape:
                return TriggerCleverElevatorEscape();
            case FocusKind.Elevator:
                if (BossInCombat && !BossEscapedPursuit && IsPlayerInsideNorthElevatorCabin(camera.Position))
                    return TriggerCleverElevatorEscape();
                TrySummonElevatorBoss(out string lift);
                return lift;
            case FocusKind.Shop:
                return VeronikaShop;
            default: if(camera.Target.Y<camera.Position.Y-.3f && Systems.ActiveBaitTier>=0) return DropFloorBait(camera);
                return HandleInteractKeyE(camera.Position,spend,grant,tool);
        }
    }

    private const string VeronikaShop="HR Вероника — Магазин оружия и апгрейдов";

    public string FocusPrompt(Camera3D camera,WeaponType tool=WeaponType.BareHands) => FindFocus(camera,tool).Label;

    private Focus FindFocus(Camera3D camera,WeaponType tool=WeaponType.BareHands)
    {
        Vector3 look = Vector3.Normalize(camera.Target - camera.Position);
        Focus focus = new(FocusKind.None, "", -1);
        float best = .93f;
        void Consider(Vector3 anchor, float height, float reach, FocusKind kind, string name, int index = -1)
        {
            Vector3 delta = anchor + Vector3.UnitY * height - camera.Position;
            if (delta.X * delta.X + delta.Z * delta.Z > reach * reach) return;
            float alignment = Vector3.Dot(look, Vector3.Normalize(delta));
            if(alignment<=.93f) return;
            float score=alignment+.025f/(.25f+delta.LengthSquared());
            if(score>best) { best=score; focus=new(kind,name,index); }
        }
        for(int i=0;i<stations.Count;i++)
        {
            var station=stations[i];
            string label=station.Label;
            if(station.Kind==FocusKind.Pizza && (!Program.NeedsHealing || Systems.StationWait.ContainsKey(i))) continue;
            if(station.Kind==FocusKind.Cooler) label=CoolerEmpty(station.Feet) ? Loc.CoolerEmpty : Systems.StationWait.ContainsKey(i) ? "Кулер наполняется" : Program.NeedsHealing ? Loc.CoolerDrink : Loc.CoolerSpill;
            if(station.Kind==FocusKind.Coffee && (Program.NeedsHealing || !Systems.WantsCoffee)) label=Systems.EspressoWait>0 ? "Эспрессо-машина готовится" : "Выпить эспрессо (+45 HP)";
            Consider(station.Feet,station.Height,3.2f,station.Kind,label,i);
        }
        if(!Systems.WingsUnlocked)
            foreach(var gate in Gates)
            {
                Vector3 nearest=Vector3.Clamp(camera.Position-gate.Feet,gate.Side ? new(0,.3f,-1.9f) : new(-1.9f,.3f,0),
                    gate.Side ? new(0,3.2f,1.9f) : new(1.9f,3.2f,0));
                Consider(gate.Feet+nearest,0,2.8f,FocusKind.Barrier,BarrierHint);
            }
        if(Systems.PlacedBait is {} bait) Consider(bait.Position,.1f,2,FocusKind.MacBook,Loc.T("Поднять наживку","Recover bait"));
        else if(Systems.HasWeapon(WeaponType.CaptureNet) && Systems.ActiveBaitTier<0) Consider(RefillPoint,.15f,2.5f,FocusKind.MacBook,Loc.T("Взять запасной ЯблоБук","Collect spare AppleBook"));
        string deposit=$"Сдать Джаба-жаб ({Systems.PocketFrogs.Count} шт.)";
        Consider(TerminalPos,1.08f,3f,FocusKind.Terminal,Systems.PocketFrogs.Count>0 ? deposit : "Баг-трекер принимает Джаба-жаб. Садок пуст");
        Consider(HrTarotBoothPos,1.6f,4.6f,FocusKind.Veronika,CarriedCoder!=null ? "Сдать кадра HR Веронике" :
            Systems.Onboarding==OnboardingStep.TalkToVeronika ? "Поговорить с Вероникой" :
            Systems.Onboarding!=OnboardingStep.BuyNet && Systems.PocketFrogs.Count>0 ? "Сдать Джаба-жаб Веронике" : VeronikaShop);
        Consider(ArsenalShopPos, 1.1f, 4.2f, FocusKind.Shop, "Арсенал и подписки");
        if (BossInCombat && !BossEscapedPursuit && IsPlayerInsideNorthElevatorCabin(camera.Position))
        {
            // Clever Escape Pursuit: deep inside open North Elevator cabin -> interior Floor 2 emergency close button!
            Vector3 cabinBtn = CentralElevatorCorePos + new Vector3(1.52f, 0f, -4.166f);
            Consider(cabinBtn, 1.38f, 4.2f, FocusKind.ElevatorEscape, EscapeElevatorButtonPrompt);
            if (focus.Kind == FocusKind.None)
                return new(FocusKind.ElevatorEscape, EscapeElevatorButtonPrompt, -1);
        }
        Consider(NorthElevatorPos,1.65f,5.5f,FocusKind.Elevator, ElevatorPrompt());
        Consider(SouthElevatorDoorPos,1.65f,5.2f,FocusKind.Elevator, ElevatorPrompt());
        Consider(ElevatorButtonPos, 1.22f, 4, FocusKind.Elevator, ElevatorPrompt());
        Consider(SouthElevatorButtonPos, 1.22f, 4, FocusKind.Elevator, ElevatorPrompt());
        for(int i=0;i<GalleyDesks.Count;i++)
            Consider(GalleyDesks[i].DeskPosition,.9f*WorkstationScale,3.6f,FocusKind.Desk,CarriedCoder!=null && GalleyDesks[i].IsOccupied ? "Сменить промптера Опенспейса" : "Стол Опенспейса",i);
        for(int i=0;i<ActiveCoders.Count;i++)
            if(CarriedCoder==null && ActiveCoders[i].Archetype is (CoderArchetype.CryptoBro or CoderArchetype.SecurityGuard) && ActiveCoders[i].State!=CoderAIState.Subdued)
                Consider(ActiveCoders[i].Position,.82f,2.6f,FocusKind.Offer,ActiveCoders[i].Archetype==CoderArchetype.SecurityGuard ? "Нанять Крышу ($80)" : "Предложить аванс ($80)",i);
        for(int i=0;i<ActiveCoders.Count;i++)
            if(ActiveCoders[i].CanSignNda)
                Consider(ActiveCoders[i].Position,.45f,3.8f,FocusKind.Coder,"Подписать NDA и поднять",i);
        foreach(var frog in Frogs)
            {
                float aim=CatchAim(camera,frog.Feet+Vector3.UnitY*.11f,2.4f);
                if(aim>Math.Max(best,HandAim(frog))) { best=aim; focus=new(FocusKind.Frog,"Поймать Джаба-жабу",Frogs.IndexOf(frog)); }
            }
        if(!Systems.WingsUnlocked && barrierContact>0 && focus.Kind==FocusKind.None) return new(FocusKind.Barrier,BarrierHint,-1);
        return focus;
    }

    internal static readonly Vector3 LiftSignSize=new(2.74f,0.58f,.06f);
    internal static Vector3 LiftSignCenter(Vector3 face,int side) => face+new Vector3(0,3.88f,side*.43f);

    internal (Vector3 Position,float Height,string Label)? OnboardingWaypoint(Vector3 eye)
    {
        (Vector3,float,string)? HuntWaypoint() => Systems.PlacedBait is {} bait ? (bait.Position,.23f,Loc.T("Спрячься [Ctrl]","Hide [Ctrl]")) :
            Systems.ActiveBaitTier<0 ? (RefillPoint,.3f,Loc.T("Взять запасной ЯблоБук","Collect spare AppleBook")) : null;
        switch(Systems.Onboarding)
        {
            case OnboardingStep.TalkToVeronika: return (HrTarotBoothPos,2.12f,"HR Вероника [E]");
            case OnboardingStep.DepositFrogs: return (TerminalPos,1.65f,"Баг-трекер [E]");
            case OnboardingStep.BuyNet: return (HrTarotBoothPos,2.12f,"Вероника: Сачок $3 [E]");
            case OnboardingStep.ActivateBait: return null;
            case OnboardingStep.CatchFrogs:
                var frog=Frogs.Where(f=>f.CatchableByHand && ReachableFrog(eye,f.Feet))
                    .MinBy(f=>Vector3.DistanceSquared(eye,f.Feet));
                return frog==null ? null : (frog.Feet,.30f,"Джаба-жаба [E]");
            case OnboardingStep.SeatFirstGalley:
                var desk=GalleyDesks.Where(d=>!d.IsOccupied).MinBy(d=>Vector3.DistanceSquared(eye,d.DeskPosition));
                if(CarriedCoder!=null && desk!=null) return (desk.DeskPosition,1.5f,"Посади в Опенспейс [E]");
                if(ActiveCoders.Count==0) return HuntWaypoint();
                goto case OnboardingStep.CaptureFirstCoder;
            case OnboardingStep.HealAtCoffee: return (CoffeePos,1.62f,"Выпей эспрессо (+45 HP) [E]");
            case OnboardingStep.CaptureFirstCoder when CarriedCoder!=null:
                return (HrTarotBoothPos,2.12f,"Сдай Веронике [E]");
            case OnboardingStep.StunAndHeal:
            case OnboardingStep.CaptureFirstCoder:
                var coder=ActiveCoders.Where(c=>(c.Hp>0 || c.CanSignNda) && c.Archetype!=CoderArchetype.SecurityGuard)
                    .MinBy(c=>Vector3.DistanceSquared(eye,c.Position));
                return coder==null ? HuntWaypoint() :
                    (coder.Position,coder.CanSignNda ? .82f : 1.84f*coder.ModelScale,coder.CanSignNda ? "Подпиши NDA [E]" : "Оглуши ЛКМ → NDA [E]");
            default: return null;
        }
    }

    // A frog around a partition can still be caught from the open side.
    private bool ReachableFrog(Vector3 eye,Vector3 feet)
    {
        Vector3 point=feet+Vector3.UnitY*.11f;
        bool Clear(Vector3 from,float reach)
        {
            var ray=point-from;
            float length=ray.Length();
            return length<.01f || length<=reach && TraceSurface(from,ray/length,length)+.02f>=length;
        }
        if(Clear(eye,float.PositiveInfinity)) return true;
        foreach(var side in FrogApproaches)
        {
            var stance=ResolvePlayerCollision(new Vector3(feet.X,1.72f,feet.Z)+side);
            if(Clear(stance,2.4f)) return true;
        }
        return false;
    }
    private static readonly Vector3[] FrogApproaches=[Vector3.UnitX,-Vector3.UnitX,Vector3.UnitZ,-Vector3.UnitZ];

    public void DrawWorldLabelsAndIndicators2D(Camera3D camera, Font font)
    {
        WorldLabelLayout.BeginFrame();
        if(!Systems.WingsUnlocked)
            foreach(var gate in Gates)
            {
                var anchor=gate.Feet+Vector3.UnitY*2.08f;
                Vector3 ray=anchor-camera.Position;
                float distance=ray.Length();
                if(distance>11 || Vector3.Dot(Vector3.Normalize(camera.Target-camera.Position),ray/distance)<.68f ||
                    TraceSurface(camera.Position,ray/distance,distance)<distance-.3f) continue;
                var point=UiViewport.Project(anchor,camera);
                string plate=$"ЗАКРЫТО   КАРАНТИН ({Math.Min(TotalCodersSoldToHr,Systems.Balance.QuarantineUnlockTarget)}/{Systems.Balance.QuarantineUnlockTarget})";
                Program.ShadowText(font,plate,point.X-GameFonts.Measure(font,plate,16).X/2,point.Y,16,new Color(255,213,170,255));
            }
        var focus=FindFocus(camera);
        Vector3? focusFeet=focus.Kind switch {
            FocusKind.Coder=>ActiveCoders[focus.Index].Position, FocusKind.Frog=>Frogs[focus.Index].Feet,
            FocusKind.Veronika=>HrTarotBoothPos, FocusKind.Terminal=>TerminalPos, FocusKind.MacBook=>Systems.PlacedBait?.Position ?? RefillPoint,
            FocusKind.Coffee=>CoffeePos, FocusKind.Desk=>GalleyDesks[focus.Index].DeskPosition,_=>null};
        var waypoint=OnboardingWaypoint(camera.Position);
        if(Systems.Onboarding!=OnboardingStep.CatchFrogs && waypoint is {} goal && focusFeet!=goal.Position)
        {
            Vector3 wpDelta = goal.Position + Vector3.UnitY * goal.Height - camera.Position;
            float wpDist = wpDelta.Length();
            bool inspectingOtherCloseProp = focus.Kind != FocusKind.None && wpDist > 8f;
            bool closeupBlockedByWall = wpDist > 12f && TraceSurface(camera.Position, Vector3.Normalize(camera.Target - camera.Position), 2.0f) < 1.45f;
            if (!inspectingOtherCloseProp && !closeupBlockedByWall)
                WorldLabelLayout.DrawWaypoint(camera,goal.Position,goal.Height+.055f*MathF.Sin(_worldClock*2),goal.Label,Systems.Onboarding==OnboardingStep.HealAtCoffee ? new Color(149,255,201,255) : null);
        }
        if(camera.Position.Z<NorthElevatorPos.Z)
            WorldLabelLayout.ReserveProjectedBox(camera,LiftSignCenter(NorthElevatorPos,-1),LiftSignSize*.5f);
        if(camera.Position.Z>SouthElevatorDoorPos.Z)
            WorldLabelLayout.ReserveProjectedBox(camera,LiftSignCenter(SouthElevatorDoorPos,1),LiftSignSize*.5f);
        Vector3 look = Vector3.Normalize(camera.Target-camera.Position);
        void ProtectHead(Vector3 feet,float scale,float height)
        {
            WorldLabelLayout.ReserveProjectedBox(camera,feet+Vector3.UnitY*(1.36f*height),
                new Vector3(.14f*scale,.12f*height,.18f*scale));
        }
        foreach(var partner in Teammates) ProtectHead(partner.Feet,1,partner.Pose.Eye.Y<1.4f?.75f:1);
        foreach(var actor in ActiveCoders)
        {
            if(actor.State==CoderAIState.Subdued || Vector3.DistanceSquared(camera.Position,actor.Position)>100) continue;
            float scale=actor.ModelScale;
            ProtectHead(actor.Position,scale,scale);
            if(actor.LaptopLost) continue;
            // Enclose the open computer at its draw yaw, including the tilted lid.
            var forward = new Vector3(MathF.Sin(actor.FacingYaw),0,MathF.Cos(actor.FacingYaw));
            var side = new Vector3(forward.Z,0,-forward.X);
            var laptop = actor.Position + scale * (.37f*forward + 1.18f*Vector3.UnitY);
            var half = scale * (.21f*Vector3.Abs(side) + .16f*Vector3.Abs(forward) + .16f*Vector3.UnitY);
            WorldLabelLayout.ReserveProjectedBox(camera,laptop,half);
        }
        void Plate(Vector3 head,string name,float health,Color accent,bool mystery=false)
        {
            if (Vector3.Dot(head-camera.Position,look)<.1f) return;
            Vector2 screen=UiViewport.Project(head,camera)-new Vector2(0,4);
            Font tag=GameFonts.Get(Typeface.InterfaceBold,15);
            float width=GameFonts.Measure(tag,name,15).X;
            float pillW = width + 14f;
            float pillH = health >= 0 ? 27f : 22f;
            if (!WorldLabelLayout.Place(screen,pillW,pillH,out var r)) return;
            Rectangle pill = new(r.X, r.Y, pillW, health >= 0 ? 21f : 22f);
            Raylib.DrawRectangleRounded(pill, 0.28f, 6, new Color(14, 11, 16, 186));
            Raylib.DrawRectangleRoundedLinesEx(pill, 0.28f, 6, 1f, new Color((int)accent.R, (int)accent.G, (int)accent.B, 96));
            Program.ShadowText(tag,name,r.X + 7f,r.Y + 3f,15,mystery ? accent : new Color(247,244,235,255));
            if(health<0) return;
            float barWidth = MathF.Min(width,62);
            Rectangle track=new(r.X+(pillW-barWidth)/2,r.Y+23,barWidth,3);
            Raylib.DrawRectangleRec(track,new Color(14,18,26,165));
            track.Width*=Math.Clamp(health,0,1);
            if(track.Width>=1) Raylib.DrawRectangleRec(track,accent);
        }
        foreach(var frog in Frogs)
        {
            if(CatchAim(camera,frog.Feet+Vector3.UnitY*.11f,2.2f)<.975f) continue;
            var frogFont=GameFonts.Get(Typeface.Interface,13);
            string label=Loc.Tr(frog.Species.Name);
            var anchor=UiViewport.Project(frog.Feet+Vector3.UnitY*.31f,camera);
            Program.ShadowText(frogFont,label,anchor.X-GameFonts.Measure(frogFont,label,13).X/2,anchor.Y-13,13,new Color(223,225,213,215));
        }
        if(!(BossRevealed && !ElevatorBoss.IsDefeated) && waypoint?.Position!=HrTarotBoothPos && Vector3.DistanceSquared(camera.Position,HrTarotBoothPos)<100 && focus.Kind!=FocusKind.Veronika)
            Plate(HrTarotBoothPos+Vector3.UnitY*1.87f, ReceptionTicker(),-1,new Color(168,153,202,255));
        foreach(var ally in Teammates)
        if(Vector3.DistanceSquared(camera.Position,ally.Feet)<400)
            Plate(ally.Feet+Vector3.UnitY*(ally.Pose.Eye.Y<1.4f?1.30f:1.72f),ally.Label,
                ally.Hp/100f,ally.Blazer);
        foreach(var c in ActiveCoders)
        {
            if(c.Stress>5 && !c.CanSignNda && Visible(camera.Position,c.CombatCenter))
            {
                var head=c.Position+Vector3.UnitY*(1.93f*c.ModelScale);
                if(Vector3.Dot(head-camera.Position,look)>0 && WorldLabelLayout.Place(UiViewport.Project(head,camera),56,5,out var track))
                {
                    Raylib.DrawRectangleRec(track,new Color(22,17,23,180));
                    float stress=Math.Clamp(c.Stress/100,0,1);
                    Raylib.DrawRectangleRec(new(track.X+1,track.Y+1,54*stress,3),Raylib.ColorLerp(Color.Gold,new Color(215,32,63,255),stress));
                }
            }
            if(c.FloorHunter && c.SpeechTimer>0)
                Plate(c.Position+Vector3.UnitY*(1.67f*c.ModelScale),c.SpeechBubble,-1,Color.White);
            if(c.FloorHunter && !c.CanSignNda) continue;
            if(focus.Kind==FocusKind.Coder && ActiveCoders[focus.Index]==c) continue;
            if(waypoint?.Position==c.Position && c.Composure>=99) continue;
            if(Vector3.DistanceSquared(camera.Position,c.Position)>100 || c.Hp<=0 && !c.KnockedOutAlive) continue;
            string plateText = c.State==CoderAIState.PanickedFleeing && c.SpeechTimer>0 ? c.SpeechBubble
                : c.Deadline>0 ? $"Дедлайн! {Math.Ceiling(c.Deadline)} с"
                : c.IdentityRevealed ? c.Name
                : c.State==CoderAIState.Subdued ? "Сломлен"
                : "";
            float bar = c.Composure<99 || c.State==CoderAIState.Subdued ? c.Composure/100 : -1;
            if(plateText.Length>0)
                Plate(c.Position+Vector3.UnitY*(1.92f*c.ModelScale),plateText,bar,VibeCoderCollection.Ink(c.Rarity),true);
            else if(bar>=0 && Visible(camera.Position,c.CombatCenter))
            {
                var head=c.Position+Vector3.UnitY*(1.92f*c.ModelScale);
                if(Vector3.Dot(head-camera.Position,look)>0 && WorldLabelLayout.Place(UiViewport.Project(head,camera),56,5,out var track))
                {
                    Raylib.DrawRectangleRec(track,new Color(14,18,26,165));
                    Raylib.DrawRectangleRec(new(track.X+1,track.Y+1,54*Math.Clamp(bar,0,1),3),VibeCoderCollection.Ink(c.Rarity));
                }
            }
        }
        if(Systems.Verdict==AgentVerdict.Contained && Vector3.DistanceSquared(camera.Position,GalleyDesks[0].DeskPosition)<36)
            Plate(GalleyDesks[0].DeskPosition+new Vector3(.74f,1,0),"Узел агента   $14/с",-1,Color.White);
    }
}

internal static unsafe partial class VisualEngine3D
{
    internal static void SetTrapLight(Vector3 screen) =>
        Raylib.SetShaderValue(shader,laptopLoc,screen,ShaderUniformDataType.Vec3);

    internal static Color CoderPigment(int index,Color hoodie,Color visor,Color original) => index switch {
        3=>hoodie,5=>Raylib.ColorBrightness(hoodie,-.55f),9=>visor,_=>original };
}

public sealed partial class SkyscraperFloor1World
{
    internal FloorSystems Systems = new();
    internal void ResumeCoderIds() => _nextCoderId=1+Math.Max(ActiveCoders.Select(c=>c.Id).DefaultIfEmpty().Max(),CarriedCoder?.Id ?? 0);
    internal Vector3 RackPos => AmbushCovers[3].Position;
    internal IEnumerable<Vector3> Coolers => stations.Where(s=>s.Kind==FocusKind.Cooler).Select(s=>s.Feet);
    internal bool CoolerHasBacking(Vector3 feet)
    {
        var center=feet+Vector3.UnitY*.9f;
        var structures=LiftWalls().Concat(InteriorWallBays()).Concat(Pillars.Select(p=>
            (p.Center+Vector3.UnitY*(CeilingHeight/2),new Vector3(2*p.HalfWidth,CeilingHeight,2*p.HalfDepth))));
        return structures.Any(w=>{
            var nearest=Vector3.Clamp(center,w.Item1-w.Item2/2,w.Item1+w.Item2/2);
            float distance=Vector3.Distance(nearest,center);
            return distance>=.23f && distance<=.36f;
        });
    }

    internal Vector3 CoolerPos => AmbushCovers[2].Position;
    internal static readonly Vector3 DeployPos=new(-3.25f,0,1.48f), CoffeePos=new(2.96f,0,12.22f);
    private readonly record struct Station(FocusKind Kind,Vector3 Feet,float Height,string Label);
    private readonly List<Station> stations=new();

    private void MapStations()
    {
        stations.Clear();
        void Add(FocusKind kind,Vector3 position,float height,string label) => stations.Add(new(kind,position,height,label));
        Add(FocusKind.Cooler,CoolerPos,.9f,"Опрокинуть кулер");
        Add(FocusKind.Extinguisher,ExtinguisherPos,.65f,"Разрядить огнетушитель у B12");
        Add(FocusKind.Rack,RackPos,1.2f,"Перезапустить стойку B12");
        Add(FocusKind.Coffee,CoffeePos,1.15f,"Эспрессо-разнос");
        void Pizza(Vector3 top) => Add(FocusKind.Pizza,new(top.X,0,top.Z),top.Y+.07f,"Съесть пиццу (+35 HP)");
        Pizza(ArenaTable+Vector3.UnitY*.80f);
        Pizza(BreakTable+Vector3.UnitY*.88f); Pizza(LoungeTable+new Vector3(.31f,.4f,0));
        Pizza(PodPizza(GalleyDesks[1].DeskPosition,false));
        for(int i=1;i<OfficeProps.Count;i+=6)
            if(OfficeProps[i].Kind==OfficePropKind.DualMonitorDesk) Pizza(PodPizza(OfficeProps[i].Position,OfficeProps[i].RotationYaw==180));
        Add(FocusKind.Deploy,DeployPos,1.12f,"DEPLOY В ПЯТНИЦУ (×2.5)");
        Add(FocusKind.Board,ContractBoardFeet,1.35f,"Доска контрактов");
        foreach(var p in OfficeProps)
        {
            if(p.Kind==OfficePropKind.WaterCooler) Add(FocusKind.Cooler,p.Position,.9f,"Опрокинуть кулер");
            if(p.Kind==OfficePropKind.XeroxPhotocopier) Add(FocusKind.Printer,p.Position,.9f,"Напечатать фальшивый пропуск");
        }
    }

    internal FloorPanel PanelAt(Camera3D camera,WeaponType tool) => FocusedPanel(FindFocus(camera,tool).Kind);

    private FloorPanel FocusedPanel(FocusKind kind) => kind switch {
        FocusKind.Board when Systems.Audit<=0=>FloorPanel.Contracts,
        FocusKind.Veronika when CarriedCoder==null && Systems.Onboarding!=OnboardingStep.TalkToVeronika &&
            (Systems.PocketFrogs.Count==0 || Systems.Onboarding==OnboardingStep.BuyNet)=>FloorPanel.Upgrades,
        FocusKind.Shop=>FloorPanel.Upgrades,_=>FloorPanel.None };

    internal VibeCoderEntity SpawnFighter(CoderArchetype role,Vector3 feet,VibeCoderSpecies? species=null)
    {
        var c=new VibeCoderEntity { Id=_nextCoderId++, Position=feet,
            State=CoderAIState.CombatHostile, IsCombatCoder=true, AttackCooldown=.48f,
            GlassesColor=new(236,192,125,255), SpawnSide=feet.X<0 ? -1 : 1 };
        c.AssignRole(role);
        VibeCoderCollection.Dress(c,species ?? VibeCoderCollection.Roll(speciesRandom,SelectedBaitTier,Systems,role));
        c.TarotPayoutValue=role switch { CoderArchetype.SeniorArchitect=>440,CoderArchetype.TeamLeadMeeter=>280,
            CoderArchetype.CryptoBro=>250,_=>160 };
        if(role==CoderArchetype.SecurityGuard) c.LaptopLost=true;
        c.Hp*=Systems.Balance.EnemyHpScale; c.MaxHp*=Systems.Balance.EnemyHpScale;
        ActiveCoders.Add(c);
        return c;
    }

    internal void EnsureCandidate(CoderArchetype role)
    {
        if(Systems.WingsUnlocked && IsMacBookTrapActive && !ActiveCoders.Exists(c=>c.Archetype==role && c.Hp>0))
            SpawnRoomFighter(role,1);
    }

    internal void SpawnWave(int count,bool raid=false)
    {
        if(!raid && (!Systems.WingsUnlocked || !IsMacBookTrapActive)) return;
        for(int i=0;i<count && (ActiveCoders.Count<Systems.Balance.Cap(22) || raid && i==0);i++)
        {
            var species=VibeCoderCollection.Roll(speciesRandom,SelectedBaitTier,Systems,raid ? CoderArchetype.SecurityGuard : null);
            var c=raid ? SpawnFighter(species.Archetype,RoomDoors[3],species) : SpawnRoomFighter(species.Archetype,i%2==0 ? 1 : -1,species);
            if(raid) EnterFromRoom(c,3);
            c.Aggro=true; c.RaidGuard=raid; c.ReleaseAttacker=Systems.Friday>0;
            c.SpeechBubble=raid ? "Проверка пропусков!" : "Кто открыл релизную ветку?";
            c.SpeechTimer=2;
        }
    }

    internal float DeskRate(GalleyDeskSlot desk,bool supported)
    {
        if(!desk.IsOccupied || desk.IsSlackingInTwitter || desk.Burnout>=100) return 0;
        float fit=Systems.JobId==2 && desk.Archetype==CoderArchetype.CryptoBro ? 1.5f : 1;
        return desk.Productivity*desk.Morale*(Working(CoderArchetype.ToxicPM) ? 1.2f : 1)*(supported ? 1.12f : 1)*fit*Systems.FridayMultiplier*(Systems.Ddos ? .5f : 1);
    }

    private bool CoolerEmpty(Vector3 ground) => Systems.Puddles.Exists(p=>p.Position==ground && p.Wet>0);

    private string Spill(Vector3 ground)
    {
        if(CoolerEmpty(ground)) return Loc.CoolerEmpty;
        int station=stations.FindIndex(s=>s.Kind==FocusKind.Cooler && s.Feet==ground);
        if(Systems.StationWait.ContainsKey(station)) return "Кулер наполняется";
        var spill=Systems.Puddles.Find(p=>p.Position==ground);
        if(spill==null) { spill=new() { Position=ground }; Systems.Puddles.Add(spill); }
        spill.Wet=18;
        return "Вода на полу. Подведи жало к луже.";
    }

    private void ApplyElectricField()
    {
        if(Systems.Puddles.Count==0) return;
        foreach(var c in ActiveCoders)
        {
            if(c.Hp<=0 || !Systems.Puddles.Exists(p=>p.Electric>0 && Vector3.DistanceSquared(c.Position,p.Position)<=3.2f*3.2f)) continue;
            // The first shock does psychological damage. Remaining contact holds the stun.
            if(c.StunTimer<.2f) c.Pressure(42,18,46*Systems.FearMultiplier);
            c.StunTimer=1.8f;
            c.ElectroShockedTimer=4.5f;
            c.Windup=0;
        }
    }

    private bool TryParry(Camera3D cam,Vector3 aim,WeaponAndCombatView weapon,out string feedback)
    {
        feedback="";
        bool wide=Systems.Has(Sidegrade.DirectorBelt);
        for(int i=0;i<EnemyProjectiles.Count;i++)
        {
            var p=EnemyProjectiles[i];
            Vector3 offset=p.Position-cam.Position;
            float maxDistSq = p.IsNodeModulesCube ? (wide ? 18.5f : 13.5f) : (wide ? 12.25f : 7.84f);
            if(p.Deflected || offset.LengthSquared() > maxDistSq ||
                Vector3.Dot(aim,Vector3.Normalize(offset))<(wide ? .42f : .60f) || Vector3.Dot(p.Velocity,offset)>=0 ||
                TraceSurface(cam.Position,Vector3.Normalize(offset),offset.Length())+.02f<offset.Length()) continue;
            Vector3 direction;
            if (p.IsNodeModulesCube && BossInCombat)
            {
                direction = Vector3.Normalize((ElevatorBoss.Position + Vector3.UnitY * 1.65f) - p.Position);
            }
            else
            {
                var owner=ActiveCoders.Find(c=>c.Id==p.OwnerId && c.Hp>0);
                direction=owner!=null ? Vector3.Normalize(owner.CombatCenter-p.Position) : -Vector3.Normalize(p.Velocity);
            }
            p.Velocity=direction*Math.Max(16f,p.Velocity.Length()*1.55f);
            p.AngularVelocity *= 1.85f;
            p.Damage*=2; p.Deflected=true; p.WireColor=p.PrimaryColor=Color.Gold; p.Life=3;
            EnemyProjectiles[i]=p;
            Systems.Style(StyleAction.Parry);
            Program.TriggerHitstop(0.038f);
            Program.TriggerHitmarker(crit: true, kill: false);
            Program.TriggerCrosshairBloom(1.2f);
            AudioMusicSystem.PlayComicWhipSnap();
            AudioMusicSystem.PlayActionClack(1.35f);
            StyleComboSystem.RegisterTrick("ПАРИРОВАНИЕ!", "BELT PARRY!", 0.45f, 34f, new Color(245, 202, 98, 255), hitstop: false, shake: 0.024f);
            feedback = p.IsNodeModulesCube ? "ОТБИЛ КОНТЕЙНЕР В БОССА!" : "ОТБИЛ ПРЕДМЕТ РЕМНЁМ!";
            weapon.TriggerWeaponAttack(cam,p.Position,true,0,true,customHitLabel:feedback);
            weapon.SpawnBurstSparks(p.Position,direction,28,Color.Gold,Color.Yellow,2.8f,7.2f,false);
            SpawnCombatDebrisBurst(p.Position, direction, 8, Color.Gold, Finish.Lamp, glowing: true, speed: 5.5f, sizeScale: 0.72f);
            return true;
        }
        return false;
    }

    private bool HitStation(Camera3D cam,WeaponAndCombatView weapon,Vector3 hit,out string feedback)
    {
        feedback="";
        if(weapon.CurrentWeapon==WeaponType.SolderingIron && hit.Y<.12f &&
            Systems.Puddles.Find(p=>Vector3.DistanceSquared(hit,p.Position)<3.2f*3.2f) is {} spill)
        {
            if(spill.Electric<=0) Systems.Style(StyleAction.Water);
            spill.Electric=Math.Min(6,spill.Wet);
            ApplyElectricField(); feedback="Разряд по воде!";
            weapon.SetCombatBanner(feedback,new Color(152,237,239,255));
        }
        else
        {
            foreach(var station in stations)
            {
                Vector3 d=hit-station.Feet;
                if(station.Kind==FocusKind.Coffee && d.Y>=0 && d.Y<1.41f && Math.Abs(d.X)<.45f && Math.Abs(d.Z)<.36f)
                { Systems.Steam=8; feedback="Паровая завеса. СБ потеряла след."; }
                if(station.Kind==FocusKind.Extinguisher && weapon.CurrentWeapon is (WeaponType.MicroUzi or WeaponType.SawedOff or WeaponType.ChatGun) &&
                    d.Y>=0 && d.Y<.96f && Math.Abs(d.X)<.22f && Math.Abs(d.Z)<.22f) feedback=ExtinguishRack();
                if(station.Kind==FocusKind.Cooler && d.Y>=0 && d.Y<=1.6f && Math.Abs(d.X)<.5f && Math.Abs(d.Z)<.5f)
                    feedback=Spill(station.Feet);
                if(station.Kind==FocusKind.Rack && weapon.CurrentWeapon!=WeaponType.Belt &&
                    d.Y>=0 && d.Y<=2.1f && Math.Abs(d.X)<.47f && Math.Abs(d.Z)<.52f)
                {
                    Systems.Overheat(this,weapon.CurrentWeapon==WeaponType.SolderingIron && Systems.Has(Sidegrade.RosinNapalm) ? 2 : 1); feedback="B12 перегрета. Персонал покидает сектор.";
                }
            }
        }
        if(feedback.Length==0) return false;
        weapon.TriggerWeaponAttack(cam,hit,false);
        return true;
    }

    internal const string KeycardDrop = "Получена Ключ-карта СБ — можно открыть Северный лифт без оплаты Веронике!";

    private void LootElevatorKey(VibeCoderEntity coder)
    {
        if (HasElevatorKey || coder.State != CoderAIState.Subdued) return;
        if (coder.Archetype != CoderArchetype.SecurityGuard && !coder.IsMiniBoss) return;
        HasElevatorKey = true;
        Systems.Announce(KeycardDrop, 6);
    }

    private void KillCoder(VibeCoderEntity c,Action<int,string>? pay)
    {
        c.State=CoderAIState.Subdued; c.KnockedOutAlive=false; c.Hp=0; c.KnockoutTime=0; c.SpeechTimer=0;
        int baseCash=c.IsCombatCoder ? 35 : Math.Max(35,c.TarotPayoutValue/3);
        int cash = StyleComboSystem.ComputeMultipliedPayout(baseCash, c.CaptureMultiplier, out _);
        Systems.Risk(security:14); Systems.Earned+=cash;
        SpawnCashBurst(c.CombatCenter, cash > baseCash ? 14 : 8, $"+${cash}"); pay?.Invoke(cash,"Кандидат");
        AudioMusicSystem.PlaySubduedDrop();
        LootElevatorKey(c);
    }

    internal Vector3 ContractBoardFeet => HrTarotBoothPos+new Vector3(-3.16f,0,-0.10f);

    private void DrawStations()
    {
        Color steel=new(62,71,76,255), brass=new(166,128,68,255);
        void Box(Vector3 pos,float w,float h,float d,Color color) => VisualEngine3D.Box(pos,w,h,d,color);
        Box(DeployPos+new Vector3(0,.48f,0),.52f,.96f,.44f,steel);
        Box(DeployPos+Vector3.UnitY*.98f,.83f,.11f,.61f,brass);
        Color alarm=Systems.Friday>0 && MathF.Sin(_worldClock*9)>0 ? new(255,149,94,255) : new(208,37,29,255);
        VisualEngine3D.Rod(DeployPos+Vector3.UnitY*1.03f,DeployPos+Vector3.UnitY*1.14f,.19f,.17f,20,alarm,Finish.Lamp);
        DrawOfficeEffects();
        if(Systems.EspressoWait>9)
            for(int i=0;i<6;i++)
            {
                float age=((12-Systems.EspressoWait)*.8f+i/6f)%1;
                var steam=CoffeePos+new Vector3(.12f*MathF.Sin(i+age*4),1.07f+age*.7f,.19f);
                VisualEngine3D.Oval(steam,new Vector3(.04f+.09f*age),new(218,191,158,(int)(100*(1-age))),Finish.RosinSmoke);
            }
        if(VisualEngine3D.HasAsset("prop_coffee_machine")) VisualEngine3D.Asset("prop_coffee_machine",CoffeePos);
        else
        {
            Box(CoffeePos+Vector3.UnitY*.45f,.85f,.9f,.68f,steel);
            Box(CoffeePos+new Vector3(0,1.1f,-.14f),.68f,.45f,.35f,new(128,61,44,255));
            Box(CoffeePos+Vector3.UnitY*1.35f,.74f,.09f,.63f,brass);
            DrawCoffee(CoffeePos+new Vector3(0,.91f,.18f));
        }
        // Grounded Art-Deco Walnut & Brushed-Brass Standing Contract Directory Lectern (facing -Z toward the player!)
        Vector3 cf = ContractBoardFeet;
        Color walnutFrame = new(54, 34, 22, 255), darkBronze = new(74, 56, 36, 255);
        VisualEngine3D.Box(cf + new Vector3(0f, 0.03f, 0f), 0.78f, 0.06f, 0.42f, darkBronze, Finish.Bronze);
        VisualEngine3D.Box(cf + new Vector3(0f, 0.075f, 0f), 0.72f, 0.03f, 0.36f, brass, Finish.Bronze);
        foreach (int lx in new[] { -1, 1 })
        {
            VisualEngine3D.Rod(cf + new Vector3(lx * 0.28f, 0.06f, 0f), cf + new Vector3(lx * 0.28f, 1.10f, 0f), 0.022f, 0.018f, 12, darkBronze, Finish.Bronze);
            VisualEngine3D.Box(cf + new Vector3(lx * 0.28f, 0.54f, 0f), 0.055f, 0.045f, 0.055f, brass, Finish.Bronze);
        }
        VisualEngine3D.Box(cf + new Vector3(0f, 0.54f, 0f), 0.56f, 0.035f, 0.035f, brass, Finish.Bronze);
        Vector3 board = cf + Vector3.UnitY * 1.34f;
        VisualEngine3D.Box(board, 0.80f, 0.54f, 0.08f, walnutFrame, Finish.Leather);
        VisualEngine3D.Box(board + new Vector3(0f, 0.285f, 0f), 0.84f, 0.04f, 0.11f, brass, Finish.Bronze);
        VisualEngine3D.Box(board + new Vector3(0f, -0.285f, 0f), 0.84f, 0.04f, 0.11f, brass, Finish.Bronze);
        // Warm parchment contract dossier facing -Z (toward the lobby!) with header bar, printed lines & crimson wax seal
        VisualEngine3D.Box(board + new Vector3(0f, 0f, -0.048f), 0.66f, 0.40f, 0.018f, new Color(224, 210, 178, 255));
        VisualEngine3D.Box(board + new Vector3(0f, 0.135f, -0.060f), 0.54f, 0.032f, 0.008f, new Color(118, 28, 38, 255));
        for (int i = 0; i < 4; i++)
            VisualEngine3D.Box(board + new Vector3(-0.04f, 0.06f - 0.058f * i, -0.060f), 0.46f - (i % 2) * 0.08f, 0.014f, 0.006f, new Color(56, 48, 42, 255));
        VisualEngine3D.Orb(board + new Vector3(0.22f, -0.11f, -0.064f), 0.032f, new Color(156, 30, 38, 255));
        // Finished rear brushed-brass corporate plaque so the lectern looks refined from 360° angles
        VisualEngine3D.Box(board + new Vector3(0f, 0f, 0.044f), 0.56f, 0.34f, 0.012f, darkBronze, Finish.Bronze);
        VisualEngine3D.Box(board + new Vector3(0f, 0f, 0.052f), 0.44f, 0.22f, 0.008f, walnutFrame, Finish.Leather);
        // Tiny warm brass gallery picture-light illuminating the contract board
        VisualEngine3D.Box(board + new Vector3(0f, 0.34f, -0.09f), 0.34f, 0.032f, 0.055f, brass, Finish.Bronze);
        VisualEngine3D.Box(board + new Vector3(0f, 0.322f, -0.09f), 0.28f, 0.014f, 0.035f, new Color(255, 226, 168, 255), Finish.Lamp);
        if(!VisualEngine3D.CastingShadows && Systems.RackHeat>0)
        {
            if (CombatSprites.HasPaintedFx)
            {
                CombatSprites.Begin(BlendMode.Alpha);
                for (int i = 0; i < 10; i++)
                {
                    float t = (_worldClock * 0.58f + i / 10f) % 1f;
                    var puff = RackPos + new Vector3(0.22f * MathF.Sin(i * 9 + t * 4f), 1.85f + t * 2.3f, 0.18f * MathF.Cos(i * 7 + t));
                    float r = 0.18f + t * 0.34f;
                    int a = Math.Clamp((int)(142 * MathF.Sin(MathF.PI * t)), 0, 255);
                    CombatSprites.SmokeFrame(i, puff, VisualEngine3D.ActiveCamRight, VisualEngine3D.ActiveCamUp, r, r,
                        new Color(142, 138, 134, a), flipX: (i & 1) == 0, rotation: t * 0.5f + i * 0.3f);
                }
                CombatSprites.End();
            }
            for(int i=0;i<14;i++)
            {
                float t=(_worldClock*.65f+i/14f)%1;
                if (!CombatSprites.HasPaintedFx)
                {
                    var puff=RackPos+new Vector3(.25f*MathF.Sin(i*9+t*4),1.83f+t*2.6f,.19f*MathF.Cos(i*7+t));
                    VisualEngine3D.Oval(puff,new Vector3(.12f+t*.35f),new(87,83,81,(int)(174*(1-t))),Finish.RosinSmoke);
                }
                var spark=RackPos+new Vector3(.36f*MathF.Sin(i),.6f+1.3f*t,.51f);
                VisualEngine3D.Rod(spark,spark+new Vector3(.04f,.09f,.06f),.008f,.002f,5,Color.Orange,Finish.Lamp);
            }
        }
        if(Systems.Friday>0)
        {
            float pulse=(1+MathF.Sin(_worldClock*8))*.5f;
            foreach(int side in new[]{-1,1})
            {
                var lamp=new Vector3(side*3.9f,2.75f,.65f);
                VisualEngine3D.Box(lamp,.42f,.17f,.15f,steel);
                VisualEngine3D.Oval(lamp+Vector3.UnitZ*.12f,new(.15f,.065f,.06f),
                    new Color((int)(155+100*pulse),(int)(40+50*pulse),28,255),Finish.Lamp);
            }
        }
        if(!VisualEngine3D.CastingShadows)
            foreach(var spill in Systems.Puddles) DrawSpill(spill);
    }

    private void DrawSpill(WaterSpill spill)
    {
        Vector3 center=spill.Position+Vector3.UnitY*.019f;
        VisualEngine3D.Water(center,Math.Clamp(spill.Electric*3,0,1));
        if(spill.Electric<=0) return;
        int frame=(int)(_worldClock*22);
        int channel=0;
        void Arc(Vector3 end)
        {
            float angle=channel*2.4f+.22f*MathF.Sin(frame+channel);
            Vector3 start=center+new Vector3(MathF.Cos(angle),0,MathF.Sin(angle))*(1.15f+.14f*channel);
            Vector3 axis=Vector3.Normalize(end-start);
            Vector3 side=Vector3.Cross(axis,Vector3.UnitY);
            Vector3 previous=start;
            for(int j=1;j<=12;j++)
            {
                float t=j/12f, shape=MathF.Sin(t*MathF.PI);
                uint seed=unchecked((uint)(frame*73856093 ^ channel*19349663 ^ j*83492791));
                seed^=seed>>16; seed*=0x7feb352d; seed^=seed>>15;
                float across=((seed&1023)/511.5f-1)*.145f;
                float lift=.15f+((seed>>10&1023)/1023f-.5f)*.17f;
                Vector3 p=Vector3.Lerp(start,end,t)+shape*(side*across+Vector3.UnitY*lift);
                VisualEngine3D.Rod(previous,p,.009f,.006f,5,new(178,244,255,255),Finish.Plasma);
                previous=p;
            }
            for(int i=0;i<4;i++)
            {
                float t=(_worldClock*1.7f+i*.27f+channel*.43f)%1;
                Vector3 velocity=new(MathF.Cos(i*2.7f+angle)*.44f,1.4f+.1f*i,MathF.Sin(i*2.7f+angle)*.44f);
                Vector3 spark=end+velocity*t-Vector3.UnitY*1.55f*t*t;
                VisualEngine3D.Oval(spark,new Vector3(.012f,.031f,.012f)*(1-t),Color.White,Finish.Plasma);
            }
            channel++;
        }
        Arc(center+new Vector3(.23f,.74f,.11f));
        foreach(var c in ActiveCoders)
            if(channel<4 && c.Hp>0 && c.StunTimer>0 && Vector3.DistanceSquared(center,c.Position)<10.5f)
                Arc(c.CombatCenter);
        while(channel<3)
            Arc(center+new Vector3(MathF.Cos(channel*2.7f)*.72f,.06f,MathF.Sin(channel*2.7f)*.72f));
    }
}

internal sealed class BugFrog
{
    internal bool CatchableByHand => SpeciesId is "frog_hopper" or "frog_syntax";
    public string SpeciesId="frog_hopper";
    public Vector3 From, To;
    public float Phase, Rest, Yaw, TargetYaw;
    public bool VentExit;
    internal void AdvanceArc(float dt) => Phase=Math.Min(1,Phase+dt/(.35f+.30f*Species.HopHeight));
    internal VibeCoderSpecies Species => VibeCoderCollection.Frogs[SpeciesId];
    internal Vector3 Feet => Vector3.Lerp(From,To,Phase)+Vector3.UnitY*(4*Phase*(1-Phase)*(VentExit ? .09f : Species.HopHeight));
}

public sealed partial class SkyscraperFloor1World
{
    public const int MaxLiveFloorFrogs = 3;
    private const float FrogRespawnCooldown = 4.5f;
    internal readonly List<BugFrog> Frogs=new();
    internal static readonly Vector3 TerminalPos=new(-3.1f,0,10.85f);
    private readonly Random frogRandom;
    private float refill, previousDebt, croakDelay;
    private int _pendingFrogRespawns;

    internal void MigrateTrainingAtrium()
    {
        SeedLobby();
        if(!IsMacBookTrapActive) return;
        ActiveCoders.RemoveAll(c=>!c.TimidIntern);

        if(CarriedCoder!=null) Systems.Onboarding=OnboardingStep.CaptureFirstCoder;
        else ActivateMacBookTrap(SubscriptionBaitTier.FreeTier,_=>false,out _);
    }

    private void SeedLobby()
    {
        Frogs.Clear(); previousDebt=0; refill=FrogRespawnCooldown; _pendingFrogRespawns=0;
        // Strict cap = MAX 3 live frogs on the floor (zero spam! Respawn 1-by-1 only after caught/sold/used)
        Vector3[] pads=[new(3.45f,0,10.05f),new(4.95f,0,9.4f),new(-3.1f,0,7.2f)];
        for(int i=0;i<pads.Length;i++)
        {
            var frog=PlaceFrog(pads[i],"frog_hopper");
            BeginHop(frog,frog.From+Vector3.UnitZ*3);
            frog.Phase=i*.18f; frog.Yaw=frog.TargetYaw;
        }
    }

    private BugFrog PlaceFrog(Vector3 p,string? id=null)
    {
        float roll=frogRandom.NextSingle();
        p=ResolvePlayerCollision(p,.22f);
        var frog=new BugFrog {SpeciesId=id ?? (roll<.035f ? "frog_hotfix" : roll<.20f ? "frog_crash" :
            roll<.47f ? "frog_syntax" : "frog_hopper"),From=p,To=p,Rest=.6f+1.2f*frogRandom.NextSingle()};
        Frogs.Add(frog);
        return frog;
    }

    internal BugFrog SpawnFrog(int vent,string? id=null)
    {
        var mouth=VentMouth(vent);
        var dir=Vector3.UnitZ;
        var frog=PlaceFrog(mouth+dir,id);
        frog.From=mouth-dir*.10f;
        frog.To=mouth+dir*(.65f+frogRandom.NextSingle()*.35f);
        frog.Yaw=frog.TargetYaw=MathF.Atan2(dir.X,dir.Z);
        frog.Rest=.12f; frog.VentExit=true; VentTime[vent]=.83f;
        return frog;
    }

    private bool ClearHop(Vector3 from,Vector3 to)
    {
        const float margin=.21f;
        if(Math.Abs(to.X)>HalfWidth-margin || Math.Abs(to.Z)>HalfDepth-margin) return false;
        var travel=to-from;
        float length=travel.Length();
        if(length<.001f) return false;
        var ray=new Ray(from+Vector3.UnitY*.05f,travel/length);
        foreach(var box in solids)
        {
            var expanded=new BoundingBox(new(box.Min.X-margin,0,box.Min.Z-margin),
                new(box.Max.X+margin,box.Max.Y,box.Max.Z+margin));
            var hit=Raylib.GetRayCollisionBox(ray,expanded);
            if(hit.Hit && hit.Distance<=length) return false;
        }
        if (!BossArenaWallDestroyed)
        {
            foreach (var box in new[] { BossArenaWallBox, ArenaTableBox })
            {
                var expanded = new BoundingBox(new(box.Min.X - margin, 0, box.Min.Z - margin),
                    new(box.Max.X + margin, box.Max.Y, box.Max.Z + margin));
                var hit = Raylib.GetRayCollisionBox(ray, expanded);
                if (hit.Hit && hit.Distance <= length) return false;
            }
        }
        return true;
    }

    private void BeginHop(BugFrog frog,Vector3 eye)
    {
        var feet=frog.Feet; feet.Y=0;
        var away=feet-eye; away.Y=0;
        float angle=frogRandom.NextSingle()*MathF.Tau;
        if(frog.SpeciesId!="frog_hopper" && away.LengthSquared()<10) angle=MathF.Atan2(away.X,away.Z);
        frog.From=frog.To=feet; frog.Phase=0; frog.Rest=0;
        for(int i=0;i<8;i++)
        {
            float yaw=angle+i*2.40f;
            var target=feet+new Vector3(MathF.Sin(yaw),0,MathF.Cos(yaw))*frog.Species.HopDistance;
            if(!Systems.WingsUnlocked && (Math.Abs(target.X)>=8.5f || target.Z<4 || target.Z>14.5f)) continue;
            if(!ClearHop(feet,target)) continue;
            frog.To=target; frog.TargetYaw=yaw; break;
        }
    }

    private void TickFrogs(float dt,Vector3 eye,Vector3? guest,Vector3? second=null)
    {
        croakDelay-=dt;
        if(Systems.TD<previousDebt) previousDebt=Systems.TD;
        // Strict Jaba-Frog Cap = MAX 3 live frogs on the floor (ZERO spam!).
        // Respawn strictly ONE at a time after a 4.5s cooldown ONLY when < 3 live frogs AND a frog was caught/sold/used!
        int deficit = MaxLiveFloorFrogs - Frogs.Count;
        if (deficit > 0 && _pendingFrogRespawns > 0)
        {
            refill -= dt;
            if (refill <= 0f)
            {
                string? nextSpecies = (Systems.HasWeapon(WeaponType.CaptureNet) && !Frogs.Exists(f => f.SpeciesId == "frog_crash"))
                    ? "frog_crash"
                    : null;
                SpawnFrog(Systems.WingsUnlocked ? frogRandom.Next(3) : 0, nextSpecies);
                _pendingFrogRespawns = Math.Max(0, _pendingFrogRespawns - 1);
                refill = FrogRespawnCooldown;
            }
        }
        else if (deficit <= 0)
        {
            _pendingFrogRespawns = 0;
            refill = FrogRespawnCooldown;
        }
        foreach(var frog in Frogs)
        {
            Vector3 near=NearestEye(frog.Feet,eye,guest,second);
            if(frog.Rest>0)
            {
                frog.Rest-=dt;
                if(frog.Rest<=0 && !frog.VentExit) BeginHop(frog,near);
                continue;
            }
            float turn=MathF.IEEERemainder(frog.TargetYaw-frog.Yaw,MathF.Tau);
            frog.Yaw+=turn*(1-MathF.Exp(-14*dt));
            frog.AdvanceArc(dt);
            if(frog.Phase<1) continue;
            frog.VentExit=false;
            frog.Rest=.6f+1.2f*frogRandom.NextSingle();
            if(croakDelay<=0 && Vector3.DistanceSquared(frog.To,near)<36)
            { AudioMusicSystem.PlayFrogCroak(); croakDelay=2.2f; }
        }
    }

    private float CatchAim(Camera3D camera,Vector3 point,float range)
    {
        Vector3 delta=point-camera.Position;
        float distance=delta.Length();
        if(distance>range || distance<.001f) return -1;
        var dir=delta/distance;
        if(TraceSurface(camera.Position,dir,distance)+.02f<distance) return -1;
        return Vector3.Dot(dir,Vector3.Normalize(camera.Target-camera.Position));
    }
    private static float HandAim(BugFrog frog)=>frog.SpeciesId=="frog_syntax" ? .987f : .963f;

    private string CatchSweep(Camera3D camera,bool net,bool interact=false)
    {
        var targets=Frogs.Select(f=>(Frog:f,Aim:CatchAim(camera,f.Feet+Vector3.UnitY*.11f,net ? 3.6f : 2.4f)))
            .Where(t=>t.Aim>(net ? .70f : HandAim(t.Frog))).OrderByDescending(t=>t.Aim).Take(net ? 3 : 1).ToArray();
        foreach(var (frog,_) in targets)
        {
            if(!interact && !net && !frog.CatchableByHand)
            {
                BeginHop(frog,camera.Position);
                return "Нужен Сачок ($3) у Вероники";
            }
            if(Systems.Has(Sidegrade.CarbonNda)) Systems.Risk(security:6);
            Systems.PocketFrogs.Add(frog.SpeciesId); Frogs.Remove(frog);
            if (_pendingFrogRespawns == 0) refill = FrogRespawnCooldown;
            _pendingFrogRespawns = Math.Min(MaxLiveFloorFrogs - Frogs.Count, _pendingFrogRespawns + 1);
        }
        return targets.Length>0 ? $"В садке: {Systems.PocketFrogs.Count}" : "";
    }

    private string DepositBugs(Action<int,string> grant)
    {
        if(Systems.PocketFrogs.Count==0) return "Садок пуст";
        int total=0;
        foreach(string id in Systems.PocketFrogs)
        {
            var species=VibeCoderCollection.Frogs[id];
            total+=species.SalePrice;
            Systems.Discover(species,grant);
        }
        Systems.PocketFrogs.Clear();
        int missing = Math.Max(0, MaxLiveFloorFrogs - Frogs.Count);
        if (missing > 0)
        {
            if (_pendingFrogRespawns == 0) refill = FrogRespawnCooldown;
            _pendingFrogRespawns = Math.Max(_pendingFrogRespawns, missing);
        }
        Systems.Earned+=total;
        grant(total,"Баг-трекер");
        AudioMusicSystem.PlayTarotCash();
        return !Systems.HasWeapon(WeaponType.CaptureNet) && Program.SharedCash>=3 ?
            "Хватает на Сачок ($3) у Вероники или в Арсенале [U]" : $"Джаба-жабы сданы: +${total}";
    }

    private void DrawBugHunt()
    {
        if(VisualEngine3D.HasAsset("prop_bug_terminal")) VisualEngine3D.Asset("prop_bug_terminal",TerminalPos);
        else
        {
            VisualEngine3D.Box(TerminalPos+Vector3.UnitY*.52f,.53f,1.04f,.43f,new(63,78,82,255));
            VisualEngine3D.Box(TerminalPos+new Vector3(0,1.09f,0),.74f,.46f,.39f,new(42,56,61,255));
            VisualEngine3D.Box(TerminalPos+new Vector3(0,1.11f,.20f),.58f,.27f,.017f,new(93,224,172,255),Finish.Lamp);
        }
        foreach(var frog in Frogs)
        {
            float stretch=frog.Rest>0 ? 1-.17f*Math.Clamp(1-frog.Rest/.15f,0,1) : 1+.19f*MathF.Sin(2*MathF.PI*frog.Phase);
            DrawFrog(frog.Feet,frog.Yaw,frog.Species.HoodieColor,stretch);
            if(frog.SpeciesId!="frog_hotfix" || VisualEngine3D.CastingShadows) continue;
            var floor=frog.Feet; floor.Y=.024f;
            Raylib.DrawCircle3D(floor,.31f+.02f*MathF.Sin(_worldClock*4),Vector3.UnitX,90,Color.Gold);
            for(int i=0;i<4;i++)
            {
                float a=_worldClock+i*MathF.Tau/4;
                VisualEngine3D.Orb(floor+new Vector3(.24f*MathF.Cos(a),.19f+.14f*MathF.Sin(2*a),.24f*MathF.Sin(a)),.014f,Color.Gold,Finish.Lamp);
            }
        }
    }

    internal static void DrawFrog(Vector3 p,float yaw,Color ink,float stretch=1,float scale=1)
    {
        var size=new Vector3(1/MathF.Sqrt(stretch),stretch,1/MathF.Sqrt(stretch));
        if(VisualEngine3D.HasAsset("prop_bug_frog"))
        { VisualEngine3D.Asset("prop_bug_frog",p,yaw*180/MathF.PI,scale,ink,stretch:size); return; }
        var rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw);
        void Oval(Vector3 local,Vector3 radii,Color tint) =>
            VisualEngine3D.Oval(p+Vector3.Transform(local*size*scale,rotation),radii*scale*size,tint);
        Oval(new(0,.115f,0),new(.16f,.105f,.19f),ink);
        Oval(new(0,.18f,.10f),new(.145f,.085f,.10f),ink);
        foreach(int sign in new[]{-1,1})
        {
            Oval(new(.14f*sign,.055f,-.06f),new(.085f,.051f,.13f),ink);
            Oval(new(.085f*sign,.246f,.135f),new(.037f),ink);
            Oval(new(.085f*sign,.251f,.163f),new(.016f),new(20,40,36,255));
        }
    }
}
