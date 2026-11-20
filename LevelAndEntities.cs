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
    Subdued = 4
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
    Defeated = 6
}

public sealed class VibeCoderEntity
{
    public int Id;
    public CoderArchetype Archetype;
    public string Name = "";
    public string SpeciesId = "";
    public VibeCoderRarity Rarity;
    public bool IdentityRevealed, TimidIntern, KnockedOutAlive;
    internal bool CanSignNda => State==CoderAIState.Subdued && (Hp>0 || KnockedOutAlive);
    internal string MysteryTag => (Rarity==VibeCoderRarity.Common ? "???" : "★ ???") +
        (State==CoderAIState.Subdued ? "  Сломлен" : "");
    public string Subtitle = "";
    public Vector3 Position;
    public Vector3 Velocity;
    public float FacingYaw; // Radians
    public float MaxHp;
    public float Hp;
    public float Anxiety; // 0..100
    public CoderAIState State;
    public float StateTimer;
    public float WalkCycle;
    public float StunTimer;
    public float HitFlashTimer;
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
    public Vector3 CombatCenter => Position + Vector3.UnitY * (1.05f * ModelScale);
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
    public CoderArchetype Archetype;
    public Color HoodieColor;
    public bool IsSlackingInTwitter;
    public float SlackCycleTimer;
    public float Productivity=20, TechDebtRate, Morale=1, Burnout, TradingTimer=28;
    public float TypingPhase;
    public float IncomeAccumulator;
    public float WhackFlashTimer;
}

public struct EnemyProjectile3D
{
    public Vector3 Position;
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
}

public struct CashBurstParticle3D
{
    public bool Coin;
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
    public float WheelSpinPhase;
    public float HitFlashTimer;
    public string SpeechBubble = "";
    public float SpeechTimer;
    public Vector3 ChargeDirection;
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
    public readonly Vector3 HrTarotBoothPos = new(0.0f, 0f, 13.0f);
    public readonly Vector3 ArsenalShopPos = new(-6.1f, 0f, 14.7f);
    public readonly Vector3 CentralMacBookPos = new(0.0f, 0f, 5.2f);
    public readonly Vector3 NorthElevatorPos = new(0.0f, 0f, -8.5f);
    public readonly Vector3 SouthElevatorDoorPos = new(0.0f, 0f, 0.5f);
    public readonly Vector3 ElevatorButtonPos = new(-3.2f, 0f, -9.2f);
    public readonly Vector3 SouthElevatorButtonPos = new(3.2f, 0f, 1.1f);
    public readonly Vector3 WestSpawnDoorPos = new(-20.5f, 0f, 5.2f);
    public readonly Vector3 EastSpawnDoorPos = new(20.5f, 0f, 5.2f);

    // Architectural Pillars, Ambush Covers, Galley Desks & 75+ Rich 3D Office Props
    public readonly List<PillarObstacle> Pillars = new(16);
    public readonly List<AmbushCoverObject> AmbushCovers = new(8);
    public readonly List<GalleyDeskSlot> GalleyDesks = new(4);
    public readonly List<OfficeProp3D> OfficeProps = new(110);
    internal static readonly Vector2[] PendantLocations = [
        new(0,12.9f),new(0,5.2f),new(-3.9f,9.7f),new(4,13),new(8.9f,13.4f),
        new(-14.5f,5.5f),new(-20.7f,-8.5f),new(14.8f,3.5f),new(22,-10.5f)];
    private static readonly Vector3 LoungeTable = new(-3.9f,0,9.7f), BreakTable = new(4,0,13);
    private static readonly Vector3 ArenaTable=new(3.1f,0,-12.3f);
    private Vector3 CartPosition => RackPos+new Vector3(1.13f,0,-.66f);
    private static readonly Vector3[] SideRacks=[new(13.5f,0,-12.4f),new(-22.5f,0,-12.5f)];

    // Dynamic Entities
    public readonly List<VibeCoderEntity> ActiveCoders = new(16);
    public readonly List<EnemyProjectile3D> EnemyProjectiles = new(64);
    public readonly List<CashBurstParticle3D> CashParticles = new(64);
    public EscapedAgentBoss ElevatorBoss = new();
    internal PartnerAvatar? Partner;

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
    internal const float RepairSeconds = 2.3f, IntroSeconds = 2.4f;
    public float ElevatorGlassOpacity = 1f;
    public bool ElevatorRepaired;
    internal bool BrokenElevator => !ElevatorRepaired && !ElevatorBoss.IsActive && !ElevatorBoss.IsDefeated;
    internal bool BossRevealed => ElevatorBoss.IsActive && ElevatorBoss.State != ElevatorBossState.Repairing;
    internal float BossHudOpacity => !BossRevealed ? 0 : ElevatorBoss.State == ElevatorBossState.IntroSpeech ?
        Smooth((IntroSeconds-ElevatorBoss.StateTimer)/.45f) : 1;
    internal bool BossInCombat => BossRevealed && ElevatorBoss.State != ElevatorBossState.IntroSpeech;
    internal bool BossModelVisible => BossRevealed || (ElevatorBoss.IsActive &&
        ElevatorBoss.State == ElevatorBossState.Repairing && ElevatorBoss.StateTimer < RepairSeconds * .5f && ElevatorGlassOpacity < .65f) ||
        (ElevatorBoss.IsDefeated && Systems.Verdict == AgentVerdict.Pending);
    public bool HasElevatorKey = false;
    public float ElevatorDoorOpenAmount = 0f; // 0 = closed, 1 = open
    public bool IsFloor2Unlocked = false;
    public float WarningBeaconPhase = 0f;

    internal const float TrapScale = .90f, WorkstationScale = 1.10f;
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
            new(-5.03f,9.72f),new(-14.35f,6.85f),new(7.1f,12.05f),new(6.03f,3.07f)];
        for(int i=0;i<30;i++)
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
    private readonly Random speciesRandom;
    private readonly Random _rng = new(1994);

    public SkyscraperFloor1World(int? speciesSeed=null)
    {
        frogRandom=speciesSeed.HasValue ? new Random(speciesSeed.Value+83) : new Random();
        speciesRandom=speciesSeed.HasValue ? new Random(speciesSeed.Value) : new Random();
        InitializeFloorLayout();
    }

    public void InitializeFloorLayout()
    {
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
            PosterTitle = "ОСТОРОЖНО: ЗОНА ИИ",
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
            PosterSubtitle = "АРЕНА ИСПЫТАНИЙ ИИ-АГЕНТА",
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

        // 4 Ambush Cover Objects surrounding the Central MacBook Trap Desk (0, 0, 5.2)
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
            Position = new Vector3(-4.5f, 0f, 4.4f),
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
        CarriedCoder = null;
    }

    private void PopulateRichOfficeProps()
    {
        OfficeProps.Add(new() {Kind=OfficePropKind.LeatherSofa,Position=new(-3.9f,0,11.2f),Size=new(2.46f,1.02f,1.08f),HasCollision=true});
        foreach (Vector3 feet in new Vector3[]{new(-5.5f,0,8.35f),new(-2.31f,0,11.24f)})
            OfficeProps.Add(new() {Kind=OfficePropKind.PottedFicus,Position=feet,Size=new(.55f,1.8f,.55f)});
        OfficeProps.Add(new() {Kind=OfficePropKind.RollingWhiteboard,Position=new(4.55f,0,15.3f),RotationYaw=171,Size=new(1.9f,1.8f,.55f),HasCollision=true});
        OfficeProps.Add(new() {Kind=OfficePropKind.WaterCooler,Position=new(3.15f,0,14.75f),Size=new(.55f,1.45f,.55f)});
        OfficeProps.Add(new() {Kind=OfficePropKind.TrashBin,Position=new(4.97f,0,13.28f),Size=new(.4f,.5f,.4f)});
        // 1. LEFT FORK (WEST OPEN-SPACE CUBICLES, DESKS, CHAIRS, XEROX, WHITEBOARDS & WATER COOLERS)
        float[] westRowsZ = { 12.0f, 5.5f, -1.5f, -8.5f, -15.5f, -22.5f };
        for (int r = 0; r < westRowsZ.Length; r++)
        {
            float rz = westRowsZ[r];
            for (int c = 0; c < 3; c++)
            {
                if(r<2 && c<2) continue; // These four workstations are the playable Galley desks.
                float cx = -14.5f - c * 6.2f;


                // Office Desk with Dual Monitors & Desk Lamp
                OfficeProps.Add(new OfficeProp3D
                {
                    Kind = OfficePropKind.DualMonitorDesk,
                    Position = new Vector3(cx, 0f, rz),
                    Size = new Vector3(2.3f, 1.46f, 1.35f),
                    PrimaryColor = new Color(68, 74, 88, 255),
                    AccentColor = ((r + c) % 2 == 0) ? new Color(0, 235, 255, 255) : new Color(65, 250, 145, 255),
                    HasCollision = true
                });


                // Trash Bin next to every second desk
                if ((r + c) % 2 == 0)
                {
                    OfficeProps.Add(new OfficeProp3D
                    {
                        Kind = OfficePropKind.TrashBin,
                        Position = new Vector3(cx + 1.28f, 0f, rz + 0.25f),
                        Size = new Vector3(0.34f, 0.44f, 0.34f),
                        PrimaryColor = new Color(52, 58, 70, 255),
                        AccentColor = new Color(240, 240, 248, 255),
                        HasCollision = false
                    });
                }
            }
        }

        // 2. RIGHT FORK (EAST SERVER & ARCHIVE WING: DESK CLUSTERS, FILING CABINETS, XEROX PHOTOCOPIERS, WHITEBOARDS)
        float[] eastRowsZ = { 10.5f, 3.5f, -3.5f, -10.5f, -17.5f, -24.5f };
        for (int r = 0; r < eastRowsZ.Length; r++)
        {
            float rz = eastRowsZ[r];
            for (int c = 0; c < 2; c++)
            {
                float cx = 14.8f + c * 7.2f;


                OfficeProps.Add(new OfficeProp3D
                {
                    Kind = OfficePropKind.DualMonitorDesk,
                    Position = new Vector3(cx, 0f, rz),
                    Size = new Vector3(2.3f, 1.46f, 1.35f),
                    PrimaryColor = new Color(72, 68, 86, 255),
                    AccentColor = new Color(255, 185, 55, 255),
                    HasCollision = true
                });

            }

            // Tall Metal Filing Cabinets along East Wall
            OfficeProps.Add(new OfficeProp3D
            {
                Kind = OfficePropKind.FilingCabinet,
                Position = new Vector3(33.5f, 0f, rz),
                Size = new Vector3(0.95f, 1.85f, 1.25f),
                PrimaryColor = new Color(82, 88, 102, 255),
                AccentColor = new Color(215, 222, 235, 255),
                HasCollision = true
            });
        }

        // 3. LARGE XEROX PHOTOCOPIERS, VENDING MACHINES, ROLLING WHITEBOARDS, SOFAS, FICUSES & WATER COOLERS
        Vector3[] xeroxCoords =
        {
            new(-11.2f, 0f, 1.8f),
            new(11.2f, 0f, 1.8f),
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
                Position = xeroxCoords[i],
                Size = new Vector3(1.35f, 1.12f, 0.92f),
                PrimaryColor = new Color(215, 220, 228, 255),
                AccentColor = new Color(45, 235, 165, 255),
                HasCollision = true
            });
        }

        Vector3[] whiteboardCoords =
        {
            new(-6.6f, 0f, 1.2f),
            new(6.6f, 0f, 1.2f),
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
                Position = vendingCoords[i],
                Size = new Vector3(1.25f, 2.25f, 0.95f),
                PrimaryColor = new Color(28, 34, 52, 255),
                AccentColor = (i % 2 == 0) ? new Color(255, 60, 120, 255) : new Color(0, 235, 255, 255),
                HasCollision = true
            });
        }

        Vector3[] coolerCoords =
        {
            new(-7.5f, 0f, -1.2f),
            new(7.5f, 0f, -1.2f),
            new(-7.5f, 0f, -7.2f),
            new(7.5f, 0f, -7.2f),
            new(-14.0f, 0f, 18.0f),
            new(16.0f, 0f, 18.0f)
        };
        for (int i = 0; i < coolerCoords.Length; i++)
        {
            OfficeProps.Add(new OfficeProp3D
            {
                Kind = OfficePropKind.WaterCooler,
                Position = coolerCoords[i],
                Size = new Vector3(0.55f, 1.45f, 0.55f),
                PrimaryColor = new Color(228, 235, 245, 255),
                AccentColor = new Color(45, 165, 255, 225),
                HasCollision = false
            });
        }

        Vector3[] sofaAndFicusCoords =
        {
            new(-11.5f, 0f, 20.5f),
            new(17.5f, 0f, 20.5f),
            new(-22.5f, 0f, -5.0f),
            new(22.5f, 0f, -5.0f),
            new(-18.5f, 0f, -28.5f),
            new(18.5f, 0f, -28.5f)
        };
        for (int i = 0; i < sofaAndFicusCoords.Length; i++)
        {
            OfficeProps.Add(new OfficeProp3D
            {
                Kind = OfficePropKind.LeatherSofa,
                Position = sofaAndFicusCoords[i],
                Size = new Vector3(2.4f, 0.88f, 1.0f),
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

        // The southern meeting suite opens towards the central aisle.
        foreach (int side in new[] { -1, 1 })
        {
            Vector3 center = new(side*23,0,28);
            OfficeProps.Add(new() { Kind=OfficePropKind.MeetingDesk, Position=center,
                Size=new(2.1f,1.4f,1.05f), HasCollision=true });
            foreach (int edge in new[] { -1, 1 })
                OfficeProps.Add(new() { Kind=OfficePropKind.MeetingGlass, Position=center+new Vector3(edge*4,0,0),
                    Size=new(.08f,3.1f,6), HasCollision=true });
            OfficeProps.Add(new() { Kind=OfficePropKind.MeetingGlass, Position=center+new Vector3(0,0,3),
                Size=new(8,3.1f,.08f), HasCollision=true });
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
        // Shared spines in the open wings. The paired worktops stop well short of
        // both cross aisles and the portals into the reception corridor.
        var wingDesks=OfficeProps.Where(p=>p.Kind==OfficePropKind.DualMonitorDesk).ToArray();
        foreach(var desk in wingDesks)
        {
            if(desk.Position.X is not (-14.5f or -20.7f or 14.8f or 22f) ||
               desk.Position.Z is not (5.5f or -8.5f or 3.5f or -10.5f)) continue;
            var opposite=desk;
            opposite.Position.Z-=1.07f;
            opposite.RotationYaw=180;
            OfficeProps.Add(opposite);
            var bin=opposite.Position+new Vector3(-1.04f,0,.17f);
            OfficeProps.Add(new() {Kind=OfficePropKind.TrashBin,Position=bin,Size=new(.34f,.44f,.34f)});
        }
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
        Add(ArenaTable,1.5f,.80f,.8f);
        Add(LoungeTable,1.44f,.40f,.68f);
        Add(BreakTable,1.1f,.88f,.88f);
        Add(CartPosition,.85f,.96f,.67f);
        foreach(var rack in SideRacks) Add(rack,.94f,2.05f,.92f);
        Add(TerminalPos,.75f,1.35f,.65f);
        Add(ExtinguisherPos,.4f,.94f,.4f);
        Add(CoffeePos,.85f,1.39f,.68f);
        foreach (var p in Pillars) Add(p.Center,2*p.HalfWidth,CeilingHeight,2*p.HalfDepth);
        Add(CentralElevatorCorePos,2*ElevatorCoreHalfW,CeilingHeight,2*ElevatorCoreHalfD);
        foreach (var bay in InteriorWallBays()) solids.Add(new(bay.Center-bay.Size/2,bay.Center+bay.Size/2));
        Add(CentralMacBookPos,2.48f*TrapScale,TrapTop,1.44f*TrapScale);
        Add(HrTarotBoothPos,3.30f,1.08f,2.10f);
        Add(HrTarotBoothPos+new Vector3(-2.15f,0,.35f),1.45f,1.31f,.60f);
        Add(ArsenalShopPos,4.50f,1.12f,1.90f);
        foreach (var d in GalleyDesks) Workstation(d.DeskPosition);
        foreach (var cover in AmbushCovers)
        {
            Vector3 size = cover.VisualKind switch {
                0 => new(2.4f,.88f,1), 1 => new(.55f,.64f,.55f),
                2 => new(.55f,1.45f,.55f), _ => new(.85f,2.05f,.94f) };
            Add(cover.Position,size.X,size.Y,size.Z);
        }
        foreach (var p in OfficeProps)
        {
            if (!p.HasCollision) continue;
            if (p.Kind == OfficePropKind.DualMonitorDesk) Workstation(p.Position,p.RotationYaw==180 ? -1 : 1);
            else if (p.Kind == OfficePropKind.MeetingDesk) Add(p.Position,2.1f*WorkstationScale,.65f,1.05f*WorkstationScale);
            else Add(p.Position,p.Size.X,p.Size.Y,p.Size.Z);
        }
    }

    public Vector3 ResolvePlayerCollision(Vector3 desiredPos, float playerRadius = .40f, float feet = 0)
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
        return pos;
    }

    internal float SupportHeight(Vector3 pos, float previousFeet, float radius = .40f)
    {
        float height = 0;
        foreach (var b in solids)
            if (b.Max.Y <= previousFeet+.001f && pos.X > b.Min.X-radius && pos.X < b.Max.X+radius &&
                pos.Z > b.Min.Z-radius && pos.Z < b.Max.Z+radius)
                height = Math.Max(height,b.Max.Y);
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

        IsPlayerInAmbush = nearCover || isCrouching || InCloud(playerPos);
    }

    // =========================================================================
    // TRAP ACTIVATION, CODER SPAWNING & AI LOOP
    // =========================================================================
    private void SpawnAtriumIntern()
    {
        int ordinal=++Systems.TrainingSpawns;
        var intern=SpawnFighter(CoderArchetype.JuniorPrompter,new(ordinal%2==0 ? 7.2f : -7.2f,0,5.2f));
        intern.TimidIntern=intern.IdentityRevealed=intern.LaptopLost=intern.BaitTaken=true;
        intern.Rarity=ordinal>=3 || TotalCodersSoldToHr>=2 ? VibeCoderRarity.Uncommon : VibeCoderRarity.Common;
        intern.Name=ordinal==1 ? "Стажёр-Одуванчик" : $"Промптер №{ordinal}";
        intern.MaxHp=intern.Hp=24; intern.TarotPayoutValue=10;
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
        if(IsMacBookTrapActive && ActiveCoders.Count>=22)
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
                intern.MaxHp=intern.Hp=24;
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
        Vector3 spawnPos = side < 0
            ? WestSpawnDoorPos + new Vector3(0.8f, 0f, (_rng.NextSingle() - 0.5f) * 1.4f)
            : EastSpawnDoorPos + new Vector3(-0.8f, 0f, (_rng.NextSingle() - 0.5f) * 1.4f);

        bool miniBoss=tier==SubscriptionBaitTier.CloudEntropicMax;
        var (payout,bark)=tier switch {
            SubscriptionBaitTier.CursorPro=>(250,"Там открыт Табсор Про!"),
            SubscriptionBaitTier.TurkishAccount=>(380,"Безлимит! Перепишу бэкенд в один файл!"),
            SubscriptionBaitTier.CloudEntropicMax=>(600,"МОЙ SAAS НАПИСАН ЗА 2 ЧАСА!"),
            _=>(160,"О, халявный Free Tier на ЯблоБуке!") };
        var coder=new VibeCoderEntity {
            Id=_nextCoderId++,Position=spawnPos,SpawnSide=side,TarotPayoutValue=payout,
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
        Vector3 toMac = CentralMacBookPos - spawnPos;
        coder.FacingYaw = MathF.Atan2(toMac.X, toMac.Z);
        ActiveCoders.Add(coder);

        if (coder.IsMiniBoss)
        {
            var escort=VibeCoderCollection.Roll(speciesRandom,tier,Systems);
            SpawnFighter(escort.Archetype,spawnPos+Vector3.UnitZ*1.1f,escort);
            AudioMusicSystem.PlayBossSiren();
            AudioMusicSystem.TriggerRandomVoiceLine("ГУРУ ТАБСОРА ($60K MRR)", AudioMusicSystem.SaasMiniBossLines, (int)SpeakerVoiceType.VibeCoder);
        }

        return coder;
    }

    // =========================================================================
    // ELEVATOR BOSS SUMMONING & AI
    // =========================================================================
    internal string ElevatorPrompt()
    {
        if (ElevatorBoss.IsDefeated) return "Лифт на 2-й этаж открыт";
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
        ElevatorBoss = new() {
            IsActive = true, Position = NorthElevatorPos + new Vector3(0,.16f,1.35f), FacingYaw = MathF.PI,
            State = ElevatorBossState.Repairing, StateTimer = RepairSeconds
        };
        PlayElevatorCue(ElevatorBoss.State);
        message = ElevatorPrompt();
        return true;
    }

    internal const string AgentQuarantineLine = "Ха-ха-ха-ха! Карантинный лифт вскрыт! Зеро запер меня на первом этаже, чтобы я не разнёс вашу офисную базу!";
    internal static void PlayElevatorCue(ElevatorBossState state)
    {
        if (state == ElevatorBossState.Repairing)
        {
            AudioMusicSystem.PlaySolderSizzle();
            AudioMusicSystem.Speak("Петрович", "Петрович: Стой!! Ты снял карантинный замок с шахты!! В лифте заперт сбежавший ИИ-Агент v0.1!", SpeakerVoiceType.Boss90s);
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

    private float hostShock,guestShock;
    public int UpdateWorld(
        float dt,
        Vector3 playerPos,
        bool isCrouching,
        Action<int, string>? onPassiveIncomeOrPayout,
        Action<int, string, Vector3>? onPlayerHitByProjectile,
        Vector3? guestPosition=null, Action<int,string,Vector3>? onGuestHit=null)
    {
        AdvanceLighting(dt);
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
        Systems.AgentHealth=BossRevealed || ElevatorBoss.IsDefeated ? ElevatorBoss.Hp/ElevatorBoss.MaxHp : -1;
        Vector3 Nearest(Vector3 enemy) => guestPosition is {} guest &&
            Vector3.DistanceSquared(enemy,guest)<Vector3.DistanceSquared(enemy,playerPos) ? guest : playerPos;

        if (TarotVerdictTimer > 0f)
            TarotVerdictTimer = MathF.Max(0f, TarotVerdictTimer - dt);

        UpdateAmbushState(playerPos, isCrouching);

        // 1. Spawn coder when MacBook Trap is active and no live non-subdued coder is walking
        if(IsMacBookTrapActive && !Systems.WingsUnlocked && TotalCodersSoldToHr<3 &&
            (CoderSpawnTimer-=dt)<=0 && ActiveCoders.Count(c=>(c.Hp>0 || c.CanSignNda) &&
            Math.Abs(c.Position.X)<9 && c.Position.Z is >=4 and <=15)<2)
            SpawnAtriumIntern();
        if (IsMacBookTrapActive && Systems.WingsUnlocked)
        {
            int activeCount = 0;
            for (int i = 0; i < ActiveCoders.Count; i++)
            {
                if (!ActiveCoders[i].TimidIntern && !ActiveCoders[i].IsCombatCoder && ActiveCoders[i].State != CoderAIState.PanickedFleeing)
                    activeCount++;
            }

            if (activeCount < 2 && ActiveCoders.Count<22)
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
        UpdateVibeCoders(dt, playerPos,guestPosition,onPlayerHitByProjectile,onGuestHit,onPassiveIncomeOrPayout);

        // 3. Update Floor 1 Elevator Boss
        UpdateElevatorBoss(dt, Nearest(ElevatorBoss.Position));

        // 4. Update Enemy Projectiles (500 ERROR blocks & 40GB node_modules cubes)
        UpdateEnemyProjectiles(dt, playerPos, onPlayerHitByProjectile,guestPosition,onGuestHit,onPassiveIncomeOrPayout);

        // 5. Update 4 Galley Desks (Passive +$20/sec per working coder, or Twitter slack state)
        int passiveEarned = UpdateGalleyDesks(dt, onPassiveIncomeOrPayout);

        UpdateCashParticles(dt);

        Systems.Tick(dt,this,onPassiveIncomeOrPayout);
        TickFrogs(dt,playerPos,guestPosition);
        return passiveEarned;
    }

    private void UpdateVibeCoders(float dt, Vector3 hostPos, Vector3? guestPos, Action<int,string,Vector3>? hitHost, Action<int,string,Vector3>? hitGuest,Action<int,string>? pay)
    {
        for (int i = ActiveCoders.Count - 1; i >= 0; i--)
        {
            VibeCoderEntity c = ActiveCoders[i];
            Vector3 playerPos=guestPos is {} guest && Vector3.DistanceSquared(c.Position,guest)<Vector3.DistanceSquared(c.Position,hostPos) ? guest : hostPos;

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
            if(c.TimidIntern && !Systems.WingsUnlocked && Systems.Onboarding!=OnboardingStep.StunAndHeal) continue;
            if(c.TimidIntern)
            {
                Vector3 away=c.Position-playerPos; away.Y=0;
                if(away.LengthSquared() is >.001f and <3.24f)
                {
                    c.Position=ResolvePlayerCollision(c.Position+Vector3.Normalize(away)*dt*1.9f,.24f);
                    c.FacingYaw=MathF.Atan2(away.X,away.Z); c.WalkCycle+=dt*8;
                    continue;
                }
            }
            if (c.IsCombatCoder && c.State==CoderAIState.CombatHostile && c.StunTimer<=0)
            {
                if (UpdateFighter(c,dt,playerPos))
                {
                    bool baton=c.Archetype==CoderArchetype.SecurityGuard;
                    if (guestPos == playerPos) { hitGuest?.Invoke(baton ? 10 : 6,baton ? "Охрана СБ (Шокер)" : c.Name+" (Удар ноутбуком)",c.Position); if(baton) Systems.GuestStun=.8f; }
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
                    Vector3 target = CentralMacBookPos + new Vector3(0f, 0f, -0.75f);
                    Vector3 toTarget = target - c.Position;
                    toTarget.Y = 0f;
                    float dist = toTarget.Length();

                    if (dist > 0.35f)
                    {
                        Vector3 dir = toTarget / dist;
                        float speed = 2.65f;
                        c.Position += dir * speed * dt;
                        c.FacingYaw = MathF.Atan2(dir.X, dir.Z);
                        c.WalkCycle += dt * 7.5f;
                    }
                    else
                    {
                        c.State = CoderAIState.SittingAtMacBook;
                        c.Anxiety = 0f;
                        if(!c.TimidIntern) c.Pressure(32,0,0,45);
                        c.FacingYaw = 0f; // Looking at MacBook screen
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
                            c.SpeechBubble = "ТОКСИЧНЫЙ АБЬЮЗ! Я ВЫГОРЕЛ! У МЕНЯ ЛАПКИ!";
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
                    Vector3 exitDoor = c.SpawnSide < 0 ? WestSpawnDoorPos : EastSpawnDoorPos;
                    Vector3 toExit = exitDoor - c.Position;
                    toExit.Y = 0f;
                    float dist = toExit.Length();
                    if (dist < 0.8f)
                    {
                        ActiveCoders.RemoveAt(i);
                        continue;
                    }
                    Vector3 dir = toExit / dist;
                    c.Position += dir * 5.2f * dt;
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
        if(IsMacBookTrapActive && !c.RaidGuard && !c.ReleaseAttacker && !c.BaitTaken &&
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
        if (c.Windup>0) return false;
            c.AttackCooldown = c.WindingSlap ? 1.4f : 1.85f;
            if (c.WindingSlap) return (!c.LaptopLost || c.Archetype==CoderArchetype.SecurityGuard) && visible && dist<2.8f && Math.Abs(eye.Y-1.4f)<1;
            EnemyProjectiles.Add(new() {
                Position=hand, Velocity=Vector3.Normalize(c.ThrowTarget-hand)*12,
                AngularVelocity=new(180,290,110), Radius=.14f, Life=2, MaxLife=2,
                OwnerId=c.Id, Coin=c.Archetype==CoderArchetype.CryptoBro,
                Damage=c.TimidIntern && !Systems.WingsUnlocked ? 18 : 7, Label=c.Archetype==CoderArchetype.CryptoBro ? "Глитч-коин" : "Конфликт в main",
                PrimaryColor=c.Archetype==CoderArchetype.CryptoBro ? Color.Gold : new(255,64,31,255),
                WireColor=new(255,212,106,255), IsGlitch=true });
            return false;
        }
        bool synced=ActiveCoders.Exists(lead=>lead!=c && lead.Archetype==CoderArchetype.TeamLeadMeeter &&
            lead.State==CoderAIState.CombatHostile && Vector3.DistanceSquared(lead.Position,c.Position)<25);
        c.AttackCooldown -= dt*(synced ? 1.5f : 1);
        if(c.Archetype==CoderArchetype.SecurityGuard)
        {
            if(dist>1.7f) { c.Position=ResolvePlayerCollision(c.Position+dir*4.8f*dt,.24f); c.WalkCycle+=14*dt; }
            if(visible && dist<2.5f && c.AttackCooldown<=0) { c.WindingSlap=true; c.Windup=.35f; }
            return false;
        }
        if (c.AttackCooldown<=0 && visible && dist<=11)
        {
            c.WindingSlap = !c.LaptopLost && dist<2.8f;
            c.Windup = c.WindingSlap ? .38f : .58f;
            c.ThrowTarget = eye-Vector3.UnitY*.38f;
            c.SpeechBubble = c.WindingSlap ? "Лови ноут!" : "Пушу в main!";
            c.SpeechTimer = c.Windup+.22f;
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
            Velocity = dir * 9.5f, OwnerId=ownerId,
            Rotation = Vector3.Zero,
            AngularVelocity = new Vector3(180f, 260f, 140f),
            Radius = 0.42f,
            Life = 4.0f,
            MaxLife = 4.0f,
            Damage = 12,
            Label = label,
            PrimaryColor = new Color(235, 35, 55, 240),
            WireColor = new Color(255, 225, 70, 255),
            IsNodeModulesCube = false
        });
    }

    private void UpdateElevatorBoss(float dt, Vector3 playerPos)
    {
        if (!ElevatorBoss.IsActive || ElevatorBoss.IsDefeated)
        {
            if (ElevatorBoss.IsDefeated)
            {
                ElevatorDoorOpenAmount = MathF.Min(1.0f, ElevatorDoorOpenAmount + dt * 0.8f);
            }
            return;
        }

        var b = ElevatorBoss;
        if (b.State == ElevatorBossState.Repairing)
        {
            b.StateTimer -= dt;
            ElevatorGlassOpacity = Smooth(b.StateTimer / RepairSeconds);
            if (b.StateTimer > 0) return;
            ElevatorGlassOpacity = 0;
            b.State = ElevatorBossState.IntroSpeech;
            b.StateTimer = IntroSeconds;
            b.SpeechBubble = AgentQuarantineLine;
            b.SpeechTimer = IntroSeconds;
            PlayElevatorCue(b.State);
            return;
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
                ElevatorDoorOpenAmount = Smooth(elapsed / 1.05f);
                float stride = Smooth((elapsed - .28f) / (IntroSeconds - .28f));
                float bob = .055f * MathF.Abs(MathF.Sin(stride * MathF.PI * 5));
                b.Position = NorthElevatorPos + new Vector3(0,.16f*(1-stride)+bob,1.35f-4.35f*stride);
                if (b.StateTimer <= 0f)
                {
                    StartBossCharge(playerPos);
                }
                break;
            }

            case ElevatorBossState.ChargingPlayer:
            {
                b.Position += b.ChargeDirection * (b.Phase==3 ? 13f : 9.2f) * dt;
                b.WheelSpinPhase += dt * 18f;

                // Check if boss crashed into any Pillar or Wall!
                bool crashedWall = false;
                if (MathF.Abs(b.Position.X) > HalfWidth - 1.4f || b.Position.Z < -HalfDepth + 1.5f || b.Position.Z > 8.5f)
                {
                    b.Position.X = Math.Clamp(b.Position.X, -HalfWidth + 1.5f, HalfWidth - 1.5f);
                    b.Position.Z = Math.Clamp(b.Position.Z, -HalfDepth + 1.6f, 8.5f);
                    crashedWall = true;
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
                    b.AttackSubTimer = 0.25f;
                    b.SpeechBubble = "БРОСАЮ АРХИВ NODE_MODULES ВЕСОМ 40 ГИГАБАЙТ!";
                    b.SpeechTimer = 3.2f;
                }
                break;
            }

            case ElevatorBossState.SpinningWhileTrue:
            {
                b.FacingYaw += dt * 11.5f;
                b.AttackSubTimer -= dt;
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
                b.AttackSubTimer -= dt;
                if (b.AttackSubTimer <= 0f)
                {
                    b.AttackSubTimer = b.Phase>=2 ? .45f : .95f;
                    SpawnNodeModulesCube(b.Position + new Vector3(0f, 2.0f, 0f), playerPos);
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
        ElevatorBoss.SpeechBubble = "ТАРАНЮ КОЖАНЫХ МЕШКОВ! У МЕНЯ 100 ТОКЕНОВ В СЕКУНДУ!";
        ElevatorBoss.SpeechTimer = 2.4f;
    }

    public void SpawnNodeModulesCube(Vector3 origin, Vector3 playerPos)
    {
        Vector3 aim = playerPos + new Vector3((_rng.NextSingle() - 0.5f) * 1.1f, -0.15f, (_rng.NextSingle() - 0.5f) * 1.1f);
        Vector3 dir = Vector3.Normalize(aim - origin);

        EnemyProjectiles.Add(new EnemyProjectile3D
        {
            Position = origin,
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
            IsNodeModulesCube = true
        });
    }

    private void UpdateEnemyProjectiles(float dt, Vector3 playerPos, Action<int, string, Vector3>? onPlayerHit, Vector3? guestPosition, Action<int,string,Vector3>? onGuestHit, Action<int,string>? projectilePayout)
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
            if (pr.IsGlitch && TraceSurface(pr.Position,Vector3.Normalize(pr.Velocity),travel)<travel)
            {
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
                EnemyProjectiles.RemoveAt(i);
                continue;
            }

            if(pr.Deflected)
            {
                var target=ActiveCoders.Find(c=>c.Hp>0 && Vector3.DistanceSquared(pr.Position,c.CombatCenter)<.72f);
                if(target!=null)
                {
                    target.Hp=Math.Max(0,target.Hp-pr.Damage); target.HitStaggerTimer=.5f;
                    if(target.Hp==0) KillCoder(target,projectilePayout);
                    EnemyProjectiles.RemoveAt(i);
                }
                else EnemyProjectiles[i]=pr;
                continue;
            }
            // Check hit against Player
            bool hitHost=Vector3.DistanceSquared(pr.Position,playerPos)<MathF.Pow(pr.Radius+.55f,2);
            bool hitGuest=guestPosition is {} guest && Vector3.DistanceSquared(pr.Position,guest)<MathF.Pow(pr.Radius+.55f,2);
            if(hitHost || hitGuest)
            {
                var owner=ActiveCoders.Find(c=>c.Id==pr.OwnerId);
                if(owner?.TimidIntern!=true || Systems.WingsUnlocked || Systems.Onboarding==OnboardingStep.StunAndHeal)
                {
                    string attacker=pr.IsNodeModulesCube ? "ИИ-Агент v0.1" : owner?.IsMiniBoss==true ? "Гуру Табсора" :
                        owner?.Archetype is CoderArchetype.ToxicPM or CoderArchetype.SeniorArchitect ? owner.Name : "Вайбкодер";
                    Vector3 source=pr.IsNodeModulesCube ? ElevatorBoss.Position : owner?.Position ?? pr.Position-pr.Velocity;
                    (hitHost ? onPlayerHit : onGuestHit)?.Invoke(pr.Damage,$"{attacker} ({pr.Label})",source);
                }
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
                if(c.Hp<=0 && !c.KnockedOutAlive || (melee || salt) && c.State==CoderAIState.Subdued) continue;
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

                AudioMusicSystem.PlayHitmarker();
                Impact(hitPt,bossCenter,dmg,isCrit);

                if (ElevatorBoss.Hp <= 0f)
                {
                    ElevatorBoss.IsDefeated = true;
                    ElevatorBoss.IsActive = false;
                    ElevatorBoss.State = ElevatorBossState.Defeated;
                    ElevatorBoss.SpeechBubble = "FATAL SEGMENTATION FAULT... ПАДАЮ... ДВЕРИ ЛИФТА ОТКРЫТЫ!";
                    ElevatorBoss.SpeechTimer = 6.0f;
                    IsFloor2Unlocked = true;
                    ElevatorDoorOpenAmount = 1.0f;
                    weaponSystem.UnlockedChatGun = true;

                    Systems.FinalePending=true; Systems.ShutdownTime=.8f;
                    Systems.Discover(VibeCoderCollection.All.Single(s=>s.Id=="ai_agent_v01"),onGrantMoney);
                    AudioMusicSystem.PlayTarotCash();

                    feedback = "БОСС ЭТАЖА ПОВЕРЖЕН! Чат-ган получен [T]. Подпиши акт утилизации.";
                }
                else
                {
                    feedback = shield ? $"Файрвол: {ElevatorBoss.ShieldHp:F0}" : $"ПОПАДАНИЕ ПО ИИ-АГЕНТУ v0.1: -{dmg} ХП (осталось {(int)ElevatorBoss.Hp}/{ElevatorBoss.MaxHp})";
                }
                return;
            }
            var target=ActiveCoders[actor];
            isCrit=target.State==CoderAIState.SittingAtMacBook;
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
            target.HitFlashTimer=.18f;
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
                else if(captureTool) { target.HitStaggerTimer=.15f; }
                else if(salt) { target.Pressure(38*power,34*power,48*fearPower); target.StunTimer=1.8f; }
                else
                {
                    float tape=Systems.Has(Sidegrade.Tape) ? 1.45f : 1;
                    if(Systems.Has(Sidegrade.RosinNapalm)) { target.KnockedOutAlive=false; target.Hp=Math.Max(0,target.Hp-16); target.Ignition=3; }
                    target.Pressure(14*power,32*power*tape,22*fearPower); target.StunTimer=1.5f*tape;
                }
                bool burnedOut=target.Hp<=0 && !target.KnockedOutAlive;
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
                if(target.Hp<=0) { KillCoder(target,onGrantMoney); feedback="Кандидат выбыл. СБ +14"; }
            }
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

        // 3. Check if near Central MacBook Trap Desk
        float distMac = Vector2.Distance(new Vector2(playerPos.X, playerPos.Z), new Vector2(CentralMacBookPos.X, CentralMacBookPos.Z));
        if (distMac <= 3.8f)
        {
            ActivateMacBookTrap(SelectedBaitTier, trySpendMoney, out string msg);
            return msg;
        }

        // 4. Check if near Central Armored Elevator Core (North or South doors) or Call Buttons
        float distElevNorth = Vector2.Distance(new Vector2(playerPos.X, playerPos.Z), new Vector2(NorthElevatorPos.X, NorthElevatorPos.Z));
        float distElevSouth = Vector2.Distance(new Vector2(playerPos.X, playerPos.Z), new Vector2(SouthElevatorDoorPos.X, SouthElevatorDoorPos.Z));
        float distBtnNorth = Vector2.Distance(new Vector2(playerPos.X, playerPos.Z), new Vector2(ElevatorButtonPos.X, ElevatorButtonPos.Z));
        float distBtnSouth = Vector2.Distance(new Vector2(playerPos.X, playerPos.Z), new Vector2(SouthElevatorButtonPos.X, SouthElevatorButtonPos.Z));
        if (distElevNorth <= 5.5f || distElevSouth <= 5.2f || distBtnNorth <= 4.0f || distBtnSouth <= 4.0f)
        {
            TrySummonElevatorBoss(out string msg);
            return msg;
        }

        // 5. Check if near 90s Shop Stand -> cycle subscription bait tier
        float distShop = Vector2.Distance(new Vector2(playerPos.X, playerPos.Z), new Vector2(ArsenalShopPos.X, ArsenalShopPos.Z));
        if (distShop <= 4.2f)
        {
            SelectedBaitTier = (SubscriptionBaitTier)(((int)SelectedBaitTier + 1) % 4);
            var info = GetBaitInfo(SelectedBaitTier);
            return $"ВЫБРАНА ПРИМАНКА ДЛЯ ЯБЛОБУКА: {info.Name} - {info.Description}";
        }

        return "Подойди ближе к ЯблоБуку [E], покорному кодеру [E], HR-Тарологу Веронике [E], Опенспейсу [E] или Лифту [E]!";
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

        string[] tarotCards =
        {
            "XVI БАШНЯ (УРОНИЛ ПРОД)",
            "0 ШУТ (НАЖАЛ ACCEPT ALL)",
            "X КОЛЕСО ФОРТУНЫ (ГПХ БЕЗ ОТПУСКА)",
            "XV ДЬЯВОЛ (СЛИЛ API-КЛЮЧИ)",
            "XII ПОВЕШЕННЫЙ (ВЕЧНЫЙ ДЕДЛАЙН)"
        };
        LastTarotCardDrawn = tarotCards[_rng.Next(tarotCards.Length)];

        int payout = c.TarotPayoutValue;
        LastTarotVerdictText = $"Карта «{LastTarotCardDrawn}»! {c.Name} продан за +${payout}!";
        TarotVerdictTimer = 6.5f;

        Systems.Earned+=payout;
        onGrantMoney?.Invoke(payout, $"HR-Таролог Вероника ({c.Name})");
        SpawnCashBurst(HrTarotBoothPos + new Vector3(0f, 1.35f, 0f), 16, $"+${payout} ТАРО-КЭШ!");

        AudioMusicSystem.PlayTarotCash();
        AudioMusicSystem.TriggerRandomVoiceLine("HR-ТАРОЛОГ ВЕРОНИКА", AudioMusicSystem.VeronikaTarotLines, (int)SpeakerVoiceType.VeronikaTarot);
        Systems.UpdateOnboarding(this,Program.SharedCash);
        if(c.Rarity!=VibeCoderRarity.Common)
        {
            Slot.Start(Systems,c.Rarity,_rng,onGrantMoney);
            Program.ShareSlot(Slot);
        }

        return $"HR-ТАРОЛОГ ВЕРОНИКА: «{LastTarotCardDrawn} - аура дешёвая, берём!» Выплачено +${payout} наличными!";
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

    internal void SpawnSlotCoins(int count)
    {
        Vector3 tray=HrTarotBoothPos+new Vector3(0,1.48f,-.18f);
        for(int i=0;i<count;i++) {
            float angle=_rng.NextSingle()*MathF.Tau, spread=.24f+_rng.NextSingle()*1.14f;
            CashParticles.Add(new() {Coin=true,Position=tray,Velocity=new(MathF.Cos(angle)*spread,
                .9f+_rng.NextSingle()*1.45f,-.12f+.24f*MathF.Sin(angle)),Life=1.65f,MaxLife=1.65f,
                Tint=i%3==0 ? new(255,229,131,255) : new(255,187,47,255)});
        }
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
            VisualEngine3D.SetTrapLight(CentralMacBookPos+new Vector3(0,1.22f,.24f)*TrapScale);
        }
        DrawSkyscraperArchitecture3D();
        DrawWingGates();
        if(!VisualEngine3D.CastingShadows)
        {
            VisualEngine3D.Shadow(HrTarotBoothPos,4.8f,3);
            VisualEngine3D.Shadow(CentralMacBookPos,3*TrapScale,1.9f*TrapScale);
            foreach(var desk in GalleyDesks) VisualEngine3D.Shadow(desk.DeskPosition,3,2.4f);
        }
        DrawBugHunt();
        DrawOfficeProps3D();
        DrawHrTarotBoothAndVeronika3D();
        Draw90sArsenalAndShopStand3D();
        DrawGalleyWorkstations3D();
        DrawCentralMacBookTrapAndCovers3D();
        DrawLivingOffice();
        foreach(int x in new[]{-1,1})
        foreach(int z in new[]{-1,1})
        {
            Vector3 corner=CentralElevatorCorePos+new Vector3(x*4.45f,0,z*4.45f);
            for(int band=0;band<7;band++)
            {
                Vector3 lower=corner+Vector3.UnitY*(band*1.58f+.10f);
                Vector3 upper=lower+Vector3.UnitY*1.58f;
                Vector3 inset=Vector3.UnitZ*(z*.52f);
                VisualEngine3D.Rod(lower,upper-inset,.042f,.042f,8,new Color(116,130,145,255));
                VisualEngine3D.Rod(lower-inset,upper,.042f,.042f,8,new Color(116,130,145,255));
            }
        }
        DrawElevatorDoors(false);
        if(Partner is {} ally && ally.Pose.Weapon is WeaponType.BareHands or WeaponType.CaptureNet)
        {
            float yaw=ally.Pose.Yaw, height=ally.Pose.Eye.Y<1.4f ? .75f : 1;
            VisualEngine3D.DrawCharacterDirectionalShadow(ally.Feet,1.7f*height,.32f);
            VisualEngine3D.Asset("char_partner_90s",ally.Feet,yaw*180/MathF.PI,stretch:new Vector3(1,height,1),hideProp:true);
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
        else Partner?.Draw();

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

        // Draw 3D Cash Bundles flying in the air
        for (int i = 0; i < CashParticles.Count; i++)
        {
            var cp = CashParticles[i];
            if(cp.Coin) {
                float spin=(cp.MaxLife-cp.Life)*12+i;
                Vector3 face=Vector3.Normalize(new Vector3(MathF.Sin(spin),.34f*MathF.Cos(spin*1.7f),MathF.Cos(spin)));
                VisualEngine3D.Rod(cp.Position-face*.004f,cp.Position+face*.004f,.027f,.027f,14,cp.Tint,Finish.Bronze);
                Vector3 visible=Vector3.Dot(face,camera.Position-cp.Position)>0 ? face : -face;
                var stamp=cp.Position+visible*.0045f;
                Vector3 across=Vector3.Normalize(Vector3.Cross(visible,Vector3.UnitY));
                Vector3 up=Vector3.Cross(across,visible);
                Color emboss=new(255,234,155,255);
                for(int edge=0;edge<10;edge++) {
                    Vector3 Rim(float a)=>stamp+.021f*(across*MathF.Cos(a)+up*MathF.Sin(a));
                    Raylib.DrawLine3D(Rim(edge*MathF.Tau/10),Rim((edge+1)*MathF.Tau/10),emboss);
                }
                Raylib.DrawLine3D(stamp+up*.010f-across*.008f,stamp+up*.010f+across*.007f,emboss);
                Raylib.DrawLine3D(stamp+up*.010f+across*.007f,stamp-up*.012f-across*.004f,emboss);
                if(MathF.Sin(spin*2)>.86f) {
                    var glint=cp.Position+new Vector3(.018f,.022f,-.012f);
                    Raylib.DrawLine3D(glint-Vector3.UnitX*.017f,glint+Vector3.UnitX*.017f,Color.White);
                    Raylib.DrawLine3D(glint-Vector3.UnitY*.025f,glint+Vector3.UnitY*.025f,Color.White);
                }
                continue;
            }
            VisualEngine3D.Box(cp.Position, 0.28f, 0.12f, 0.16f, new Color(45, 205, 85, 245));

        }
        VisualEngine3D.DrawArchitecture(BuildTowerArchitecture, glass:true);
        Rlgl.DisableDepthMask();
        DrawElevatorDoors(true);
        DrawSunDust(camera);
        Rlgl.EnableDepthMask();
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
                    VisualEngine3D.Asset("office_workstation", pos, 180,WorkstationScale);
                    foreach (int side in new[] { -1, 1 })
                        VisualEngine3D.Box(pos+new Vector3(side*.86f,.36f,0)*WorkstationScale,.08f*WorkstationScale,.72f*WorkstationScale,.88f*WorkstationScale,new Color(65,70,78,255));
                    break;
                case OfficePropKind.XeroxPhotocopier:
                    VisualEngine3D.Asset("prop_xerox_printer", pos, 180);
                    break;
                case OfficePropKind.FilingCabinet:
                    VisualEngine3D.Asset("prop_filing_cabinet", pos, i%7-3);
                    break;
                case OfficePropKind.RollingWhiteboard:
                    VisualEngine3D.Asset("prop_whiteboard", pos,p.RotationYaw);
                    break;
                case OfficePropKind.WaterCooler:
                    VisualEngine3D.Asset("prop_water_cooler", pos, pos.X < 0 ? 90f : -90f);
                    break;
                case OfficePropKind.VendingMachine:
                    VisualEngine3D.Asset("prop_vending_machine", pos, pos.Z > 0 ? 180f : 0f);
                    break;
                case OfficePropKind.LeatherSofa:
                    VisualEngine3D.Asset("prop_leather_sofa", pos);
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

        // 4. 6 Architectural Pillars with Neon Trims & Corporate Poster Boards
        for (int i = 0; i < Pillars.Count; i++)
        {
            var p = Pillars[i];
            Vector3 colPos = p.Center + new Vector3(0f, CeilingHeight * 0.5f, 0f);
            float w = p.HalfWidth * 2f;
            float d = p.HalfDepth * 2f;

            // Concrete/marble core
            VisualEngine3D.Box(colPos, w, CeilingHeight, d, new Color(157, 164, 184, 255), Finish.Plaster);


            // Glowing base & upper neon bands
            VisualEngine3D.Box(p.Center + new Vector3(0f, 0.18f, 0f), w + 0.10f, 0.32f, d + 0.10f, new Color(26, 28, 36, 255));
            VisualEngine3D.Box(p.Center + new Vector3(0f, 2.65f, 0f), w + 0.08f, 0.10f, d + 0.08f, p.AccentColor);
            VisualEngine3D.Box(p.Center + new Vector3(0f, 4.80f, 0f), w + 0.08f, 0.10f, d + 0.08f, p.AccentColor);

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
        BuildGlassLift(mesh);
        BuildInteriorRooms(mesh);
        Color brass=new(189,145,76,255);
        float start=SouthElevatorDoorPos.Z+.15f, end=ArsenalShopPos.Z-.75f;
        float center=(start+end)*.5f;
        mesh.Add(new(0,.013f,center),new(3.25f,.024f,end-start),new(132,28,54,255),Finish.Carpet);
        foreach (int side in new[] { -1,1 })
        {
            foreach (float edge in new[] { 1.43f,1.55f })
                mesh.Add(new(side*edge,.029f,center),new(.028f,.008f,end-start),brass);
            Color route=side<0 ? new(255,195,83,255) : new(72,225,202,255);
            mesh.Add(new(side*6.35f,.018f,3.3f),new(9.5f,.024f,.065f),route,Finish.Lamp);
            mesh.Add(new(side*11.1f,.018f,-10),new(.065f,.024f,26.65f),route,Finish.Lamp);
            foreach (int z in new[] { -19,-5,8,19 })
            {
                mesh.Add(new(side*17,.035f,z),new(4.4f,.07f,.24f),new(47,48,49,255));
                for (int i=-4;i<=4;i++)
                    mesh.Add(new(side*17+i*.43f,.072f,z),new(.16f,.009f,.19f),brass);
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
    private string BarrierHint=>$"КАРАНТИН КРЫЛА СБ — СДАЙ 3 ВАЙБКОДЕРОВ ВЕРОНИКЕ ({Math.Min(TotalCodersSoldToHr,3)}/3)";
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
            Color brass=new(157,119,73,255);
            foreach(float jamb in new[]{-1.86f,1.86f})
                Panel(jamb,2.25f,.055f,.32f,.29f,Systems.WingsUnlocked?new(65,210,133,255):new(244,108,51,255),Finish.Lamp);
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
        foreach(int side in new[]{-1,1})
        {
            // Recessed sidelights leave the existing galley inspection camera clear.
            yield return (new(side*5.15f,2.3f,16.35f),new(4.7f,4.6f,.16f));
            // 3.8 m clear portals centred at -11, 5.2 and 14.5 metres.
            foreach(var span in new[]{(-15.8f,-12.9f),(-9.1f,3.3f),(7.1f,12.6f),(16.4f,20f)})
                yield return (new(side*9.6f,2.1f,(span.Item1+span.Item2)/2),new(.24f,4.2f,span.Item2-span.Item1));
            yield return (new(side*13.95f,2.1f,-15.8f),new(12.1f,4.2f,.24f));
            yield return (new(side*14.8f,2.1f,20),new(10.4f,4.2f,.24f));
        }
        yield return (new(0,2.1f,-15.8f),new(8.2f,4.2f,.24f));
    }

    private static void BuildInteriorRooms(VisualEngine3D.ArchitectureBuilder mesh)
    {
        Color stone=new(36,40,49,255), brass=new(184,144,82,255), glass=new(128,164,174,103);
        foreach(var bay in InteriorWallBays())
        {
            Vector3 c=bay.Center,s=bay.Size;
            bool reception=c.Z is >15 and <17 && MathF.Abs(c.X)<7.6f;
            bool alongZ=s.Z>s.X;
            if(reception && c.X==0)
            {
                mesh.Add(c,s,stone,Finish.Slate);
                for(int i=-12;i<=12;i++)
                    if(Math.Abs(i)>5)
                        mesh.Add(new(i*.215f,2.3f,15.39f),new(.032f,4.35f,.055f),brass);
                mesh.Add(new(0,3.25f,15.36f),new(2.12f,1.78f,.13f),new(24,27,34,255));
            }
            else
            {
                float h=reception ? .5f : 1.18f;
                mesh.Add(new(c.X,h/2,c.Z),new(s.X,h,s.Z),stone,Finish.Slate);
                mesh.Add(new(c.X,(h+s.Y)/2,c.Z),new(s.X,s.Y-h,s.Z),glass,Finish.Glass);
                foreach(float y in new[]{.08f,h,s.Y-.09f})
                    mesh.Add(new(c.X,y,c.Z),new(s.X+.035f,.065f,s.Z+.035f),brass);
                float length=alongZ?s.Z:s.X;
                int count=(int)MathF.Ceiling(length/1.65f);
                for(int i=0;i<=count;i++)
                {
                    float offset=-length/2+i*length/count;
                    mesh.Add(c+new Vector3(alongZ?0:offset,0,alongZ?offset:0),new(.065f,s.Y,.065f),brass);
                }
            }
        }
        void Portal(Vector3 p,bool alongZ)
        {
            Vector3 Size(float w,float h,float d)=>alongZ?new(d,h,w):new(w,h,d);
            foreach(int side in new[]{-1,1})
                mesh.Add(p+new Vector3(alongZ?0:side*1.99f,1.6f,alongZ?side*1.99f:0),Size(.18f,3.2f,.40f),brass);
            mesh.Add(p+Vector3.UnitY*3.30f,Size(4.16f,.20f,.40f),stone);
            mesh.Add(p+Vector3.UnitY*3.80f,Size(3.8f,.80f,.12f),glass,Finish.Glass);
            mesh.Add(p+Vector3.UnitY*4.16f,Size(4.16f,.08f,.40f),brass);
        }
        foreach(var gate in Gates) Portal(gate.Feet,gate.Side);
        mesh.Add(new(0,4.51f,15.6f),new(15,.18f,.65f),stone);
        mesh.Add(new(0,4.40f,15.25f),new(14.8f,.035f,.035f),brass,Finish.Lamp);
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
            bool working = ((x/6+6)*7+(z/6+6)*3)%11==2;
            mesh.Add(new(x,CeilingHeight-.061f,z),new(2.4f,.032f,1.2f),
                working ? new Color(148,179,201,255) : new Color(53,62,78,255),
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
        foreach (var door in new[] { WestSpawnDoorPos,EastSpawnDoorPos })
        {
            mesh.Add(door+Vector3.UnitY*4.45f,new(1.65f,.62f,.12f),Color.White,Finish.Exit);
            foreach (int side in new[] { -1,1 })
                mesh.Add(door+new Vector3(side*.63f,5.63f,0),new(.027f,1.74f,.027f),steel);
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
        VisualEngine3D.Rod(b+new Vector3(0,.01f,0), b+new Vector3(0,.03f,0),2.85f,2.85f,24,new Color(77,33,83,255));
        Vector3 origin=b+new Vector3(0,.03f,0);
        bool arms=VisualEngine3D.HasAsset("char_hr_veronika_arm_l") && VisualEngine3D.HasAsset("char_hr_veronika_arm_r");
        if (VeronikaModules.All(VisualEngine3D.HasAsset) && (arms || VisualEngine3D.HasAsset("char_hr_veronika_hands")))
        {
            bool talking=AudioMusicSystem.VoiceTimer>0 && AudioMusicSystem.ActiveSpeakerType==(int)SpeakerVoiceType.VeronikaTarot;
            float t=_worldClock, breath=MathF.Sin(t*1.9f)*.008f;
            VisualEngine3D.Asset(VeronikaModules[0],origin,180);
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
                Arm("char_hr_veronika_arm_l",.138f,0);
                Arm("char_hr_veronika_arm_r",-.138f,2.1f);
            }
            else
            {
                Vector3 hands=new(.028f*MathF.Sin(t*1.6f),1.310f+breath+.018f*MathF.Sin(t*3.2f)+(reading ? .025f : 0),
                    -.320f+.016f*MathF.Cos(t*1.6f));
                VisualEngine3D.AssetOriented("char_hr_veronika_hands",origin+hands,180,0,reading ? 4*MathF.Sin(t*2) : 0);
            }
        }
        else
        {
            VisualEngine3D.Asset("prop_hr_reception_desk",b);
            VisualEngine3D.Asset("char_hr_veronika",origin,180);
        }
        DrawWeaponRack(b+new Vector3(-2.15f,0,.35f),true);
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
            VisualEngine3D.Box(origin+new Vector3(0,.46f,0),1.45f,.92f,.60f,new(64,42,36,255));
            foreach(float side in new[]{-.70f,.70f})
                VisualEngine3D.Rod(origin+new Vector3(side,.92f,.28f),origin+new Vector3(side,1.31f,.28f),
                    .016f,.016f,8,new(141,111,73,255),Finish.Bronze);
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
        float angle=(seat%2==0 ? 1 : -1)*(8+seat*7%15)*MathF.PI/180;
        VisualEngine3D.Workstation(feet,occupied ? Vector2.Zero : new(angle,.06f+.035f*(seat%4)),reversed ? 0 : 180);
        Vector3 top=feet+Vector3.UnitY*(.792f*WorkstationScale);
        float side=reversed ? -1 : 1;
        if(seat%6==1) DrawPizza(PodPizza(feet,reversed),13, .78f);
        else if(seat%4==0) DrawCoffee(top+side*new Vector3(-.61f,0,.12f));
        if(seat%3!=0)
        {
            VisualEngine3D.OrientedBox(top+new Vector3(-.36f*side,.004f,.26f*side),new(.08f,.004f,.079f),angle,new(200,186,125,255));
            VisualEngine3D.OrientedBox(top+new Vector3(-.43f*side,.008f,.24f*side),new(.075f,.004f,.079f),-angle,new(143,181,167,255));
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

    private void DrawLivingOffice()
    {
        foreach(var lamp in PendantLocations)
            VisualEngine3D.Asset("prop_pendant_lamp",new(lamp.X,CeilingHeight,lamp.Y));
        VisualEngine3D.Asset("prop_server_cart",CartPosition,-139);
        foreach(var rack in SideRacks) VisualEngine3D.Asset("prop_server_rack",rack,180);
        // Solid reception backing between the outer brass ribs and central tarot sign.
        VisualEngine3D.Asset("prop_poster_frame",new(1.99f,1.86f,15.355f),180);
        VisualEngine3D.Box(LoungeTable+new Vector3(0,.365f,0),1.44f,.07f,.68f,new(131,87,54,255));
        foreach(float x in new[]{-.46f,.46f})
            VisualEngine3D.Box(LoungeTable+new Vector3(x,.165f,0),.11f,.33f,.54f,new(51,53,56,255));
        DrawPizza(LoungeTable+new Vector3(.31f,.4f,0),-13);
        VisualEngine3D.Sheet(LoungeTable+new Vector3(-.29f,.405f,.02f),.24f,0,Finish.PitchDeck);
        VisualEngine3D.Sheet(LoungeTable+new Vector3(-.37f,.41f,-.04f),-.17f,0,Finish.PitchDeck);
        VisualEngine3D.Rod(BreakTable,BreakTable+Vector3.UnitY*.05f,.33f,.32f,16,new(57,56,53,255));
        VisualEngine3D.Rod(BreakTable,BreakTable+Vector3.UnitY*.84f,.059f,.052f,12,new(150,113,73,255));
        VisualEngine3D.Rod(BreakTable+Vector3.UnitY*.835f,BreakTable+Vector3.UnitY*.88f,.55f,.55f,20,new(157,109,73,255));
        DrawPizza(BreakTable+Vector3.UnitY*.88f,-18);
        VisualEngine3D.Box(ArenaTable+Vector3.UnitY*.76f,1.5f,.08f,.8f,new(126,88,61,255));
        foreach(float x in new[]{-.62f,.62f})
            VisualEngine3D.Box(ArenaTable+new Vector3(x,.36f,0),.09f,.72f,.63f,new(62,63,66,255));
        DrawPizza(ArenaTable+Vector3.UnitY*.80f,7);
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
                // The pelvis stays in contact with the seat while typing.
                Color hoodie = d.WhackFlashTimer>0 ? Color.Gold : d.HoodieColor;
                VisualEngine3D.Asset("char_vibe_coder_seated",d.SeatedFeet,180, WorkstationScale,
                    tint:Raylib.ColorLerp(Color.White,hoodie,.30f));


            }
        }
    }

    private void DrawCentralMacBookTrapAndCovers3D()
    {
        Vector3 m = CentralMacBookPos;

        // 1. Detailed Blender 3D Tactical Trap Desk + Open Aluminum MacBook Pro
        VisualEngine3D.Asset("prop_macbook_trap_desk", m, 0f, TrapScale);


        // 4. Ambush Cover Objects around Central Desk (Each a dedicated 3D Blender model!)
        for (int i = 0; i < AmbushCovers.Count; i++)
        {
            var cov = AmbushCovers[i];
            Vector3 p = cov.Position;

            switch (cov.VisualKind)
            {
                case 0: // Leather Sofa from the 90s
                    VisualEngine3D.Asset("prop_leather_sofa", p,180);
                    break;
                case 1: // Potted Office Ficus Tree
                    VisualEngine3D.Asset("prop_ficus_tree", p);
                    break;
                case 2: // Office Water Cooler with 19L Blue Bottle & Taps
                    VisualEngine3D.Asset("prop_water_cooler", p, 90f);
                    break;
                case 3: // 42U Server Rack with Blade Units & LEDs
                    VisualEngine3D.Asset("prop_server_rack", p);
                    break;
            }
        }
    }

    private void BuildGlassLift(VisualEngine3D.ArchitectureBuilder mesh)
    {
        Vector3 center = CentralElevatorCorePos;
        Color steel=new(67,83,100,255), chrome=new(159,166,168,255);
        Color pane=new(84,164,185,25), cabinPane=new(112,173,182,17);
        Color anthracite=new(42,48,54,255), black=new(24,30,36,255), bronze=new(164,117,67,255);
        Color gold=new(227,182,112,255), teal=new(75,224,208,255);
        void Part(float x,float y,float z,float w,float h,float d,Color color,Finish finish=Finish.Paint) =>
            mesh.Add(center+new Vector3(x,y,z),new(w,h,d),color,finish);

        foreach(int side in new[]{-1,1})
        {
            void Front(float x,float y,float depth,float w,float h,float d,Color c,Finish f=Finish.Paint) =>
                Part(x,y,side*(4.5f+depth),w,h,d,c,f);
            // Recessed doors, stepped jambs and a continuous projecting entablature.
            Front(0,4.83f,.08f,8.95f,1.46f,.58f,anthracite,Finish.BlackMarble);
            Front(0,4.12f,.28f,8.8f,.12f,.38f,bronze,Finish.Bronze);
            Front(0,5.58f,.16f,9.02f,.15f,.66f,gold,Finish.Bronze);
            Front(0,5.77f,.03f,8.60f,.24f,.46f,black);
            Front(0,5.95f,-.07f,7.82f,.12f,.35f,bronze);
            Front(0,4.85f,.395f,5.98f,1.27f,.12f,bronze,Finish.Bronze);
            Front(0,.055f,.21f,4.47f,.11f,.78f,black);
            for(int stripe=-7;stripe<=7;stripe++)
                mesh.Add(center+new Vector3(stripe*.28f,.116f,side*4.79f),new(.13f,.012f,.32f),new Color(239,188,45,255),Finish.Paint,side*.45f);
            Part(side*4.5f,CeilingHeight*.5f,0,.045f,CeilingHeight,9,pane,Finish.Glass);
            Part(0,4.0f+(CeilingHeight-4.0f)*.5f,side*4.5f,9,CeilingHeight-4.0f,.045f,pane,Finish.Glass);
            foreach(int end in new[]{-1,1})
            {
                float jamb=end*3.27f;
                Front(jamb,2.12f,.02f,2.24f,4.22f,.68f,anthracite,Finish.BlackMarble);
                Front(jamb,.20f,.10f,2.32f,.40f,.82f,black);
                Front(jamb,3.94f,.12f,2.30f,.24f,.76f,bronze);
                for(int rib=0;rib<4;rib++)
                {
                    float x=end*(2.25f+.20f*rib), h=3.65f-.18f*rib;
                    Front(x,.35f+h/2,.36f,.12f,h,.12f,bronze,Finish.Bronze);
                    Front(x,.35f+h/2,.432f,.033f,h,.025f,gold,Finish.Bronze);
                }
                Front(end*4.10f,2.13f,.38f,.14f,3.50f,.13f,black);
                Front(end*4.10f,2.13f,.455f,.048f,3.34f,.03f,teal,Finish.Lamp);
                // Three plates form the web and flanges of each guide rail.
                Part(end*2.91f,CeilingHeight/2,side*3.02f,.14f,CeilingHeight,.36f,steel);
                foreach(int flange in new[]{-1,1})
                    Part(end*2.91f,CeilingHeight/2,side*3.02f+flange*.18f,.38f,CeilingHeight,.075f,chrome);
                Part(end*3.32f,2,side*4.5f,2.32f,4,.045f,pane,Finish.Glass);
                Part(end*4.46f,CeilingHeight*.5f,side*4.46f,.22f,CeilingHeight,.22f,steel);
                Part(end*4.22f,CeilingHeight*.5f,side*4.22f,.08f,CeilingHeight,.08f,new(81,205,218,255),Finish.Lamp);
                Part(end*2.57f,CeilingHeight*.5f,side*2.7f,.09f,CeilingHeight,.09f,chrome);
                Part(end*1.74f,3.8f+(CeilingHeight-3.8f)*.5f,side*1.9f,.048f,CeilingHeight-3.8f,.048f,chrome);
            }
            foreach(float y in new[]{.12f,4.02f,7.60f,CeilingHeight-.13f})
            {
                Part(0,y,side*4.5f,9.1f,.15f,.16f,steel);
                Part(side*4.5f,y,0,.16f,.15f,9.1f,steel);
            }
            Part(side*2.57f,1.91f,0,.035f,3.55f,5.40f,cabinPane,Finish.Glass);
            Part(side*2.57f,1.10f,0,.085f,.065f,5.4f,chrome);
            Part(side*2.50f,.23f,0,.075f,.055f,5.45f,teal,Finish.Lamp);
            Part(0,.23f,side*2.67f,5.05f,.055f,.075f,teal,Finish.Lamp);
            Part(side*2.30f,3.57f,0,.09f,.07f,4.7f,gold,Finish.Lamp);
            Part(0,3.65f,side*2.7f,5.25f,.055f,.11f,new(205,186,131,255),Finish.Lamp);
            foreach(int end in new[]{-1,1})
                Part(end*2.23f,1.91f,side*2.7f,.64f,3.55f,.035f,cabinPane,Finish.Glass);
        }
        Part(0,.08f,0,5.3f,.16f,5.55f,steel);
        Part(0,3.78f,0,5.3f,.17f,5.55f,steel);
        Part(0,3.64f,0,4.75f,.11f,4.9f,bronze);
        Part(0,3.55f,0,4.34f,.08f,4.46f,new Color(226,221,188,255),Finish.Lamp);
        Part(0,4.34f,0,5.64f,.37f,.58f,anthracite);
        foreach(int end in new[]{-1,1})
        {
            Part(end*2.24f,4.05f,0,.28f,.52f,.44f,steel);
            foreach(float dx in new[]{-.32f,.32f})
                Part(end*1.72f+dx,7.88f,0,.055f,6.64f,.055f,chrome);
        }
    }

    private void DrawElevatorDoors(bool glass)
    {
        Color steel=new(116,122,123,255), brass=new(196,151,90,255);
        Color pane=Raylib.ColorLerp(new(135,92,56,138),new(24,28,34,248),ElevatorGlassOpacity);
        if(!glass)
            foreach(int end in new[]{-1,1})
            {
                Vector3 wheel=CentralElevatorCorePos+new Vector3(end*1.72f,4.56f,0);
                VisualEngine3D.Rod(wheel-Vector3.UnitZ*.13f,wheel+Vector3.UnitZ*.13f,.35f,.35f,20,steel);
                VisualEngine3D.Rod(wheel-Vector3.UnitZ*.16f,wheel+Vector3.UnitZ*.16f,.13f,.13f,16,brass);
            }
        foreach(int side in new[]{-1,1})
        {
            Vector3 face=side<0 ? NorthElevatorPos : SouthElevatorDoorPos;
            Vector3 button=side<0 ? ElevatorButtonPos : SouthElevatorButtonPos;
            Vector3 panel=new(button.X,1.43f,face.Z+side*.39f);
            if (glass && !BrokenElevator) DrawShaftSmoke(face,side,inside:true);
            foreach(int leaf in new[]{-1,1})
            {
                Vector3 p=face+new Vector3(leaf*(1.06f+2.14f*ElevatorDoorOpenAmount),2.01f,side*.105f);
                if(glass)
                    VisualEngine3D.Box(p,2.07f,3.85f,.034f,pane,Finish.LiftGlass);
                else
                {
                    foreach(int edge in new[]{-1,1})
                    {
                        VisualEngine3D.Box(p+Vector3.UnitX*(edge*1.04f),.053f,3.95f,.09f,steel);
                        VisualEngine3D.Box(p+Vector3.UnitY*(edge*1.95f),2.13f,.08f,.09f,steel);
                    }
                    VisualEngine3D.Box(p-Vector3.UnitY*1.59f,2.07f,.65f,.084f,steel);
                    VisualEngine3D.Box(p+new Vector3(-leaf*.97f,0,side*.055f),.035f,3.78f,.023f,
                        new Color(123,231,215,255),Finish.Lamp);
                    for(int rib=0;rib<3;rib++)
                    {
                        Vector3 foot=p+new Vector3(leaf*(.25f+rib*.19f),-1.80f,side*.063f);
                        Vector3 bend=foot+Vector3.UnitY*(1.89f+rib*.33f);
                        VisualEngine3D.Rod(foot,bend,.021f,.021f,8,brass);
                        VisualEngine3D.Rod(bend,bend+new Vector3(-leaf*.65f,.73f,0),.021f,.021f,8,brass);
                    }
                }
            }
            if(glass)
            {
                DrawShaftSmoke(face,side,inside:false);
                DrawPanelSparks(panel,side);
                continue;
            }
            if (ElevatorRepaired && ElevatorGlassOpacity < 1)
                VisualEngine3D.Box(face+new Vector3(0,3.72f,-side*.68f),.62f,.10f,.18f,
                    Raylib.ColorLerp(new(45,25,25,255),new(251,58,42,255),1-ElevatorGlassOpacity),Finish.Lamp);
            VisualEngine3D.Box(LiftSignCenter(face,side),LiftSignSize.X,LiftSignSize.Y,LiftSignSize.Z,Color.White,Finish.Lift);
            VisualEngine3D.Box(panel,.46f,.94f,.12f,brass);
            VisualEngine3D.Box(panel+Vector3.UnitZ*(side*.075f),.38f,.85f,.04f,new Color(22,32,38,255));
            float pulse=MathF.Sin(_worldClock*12+side)+MathF.Sin(_worldClock*37);
            Color led=BrokenElevator ? (pulse>.82f ? new(239,143,55,255) :
                pulse<-1.3f ? new(77,33,28,255) : new(232,57,42,255)) : new(113,228,211,255);
            VisualEngine3D.Box(panel+new Vector3(0,.19f,side*.106f),.23f,.11f,.02f,led,Finish.Lamp);
            VisualEngine3D.Box(panel+new Vector3(0,-.14f,side*.108f),.14f,.14f,.025f,
                HasElevatorKey ? new Color(96,214,166,255) : brass,Finish.Lamp);
        }
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
        int count=BrokenElevator ? 12 : 28;
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
        float t=repair ? (RepairSeconds-ElevatorBoss.StateTimer)% .42f : (_worldClock+(side+1)*.36f)%2.17f;
        if(t>.23f || (repair && ElevatorBoss.StateTimer<1.25f)) return;
        for(int i=0;i<(repair ? 11 : 5);i++)
        {
            float angle=i*2.4f+(int)(_worldClock/2.17f)*.7f;
            Vector3 velocity=new(MathF.Cos(angle)*1.24f,.56f+MathF.Sin(angle),side*(.6f+.12f*i));
            Vector3 head=panel+new Vector3(.08f,.19f,side*.14f)+velocity*t-Vector3.UnitY*(4*t*t);
            VisualEngine3D.Rod(head,head+velocity*.045f,.012f,.004f,5,new(174,214,250,255),Finish.Plasma);
        }
    }

    private void DrawVibeCoder3D(VibeCoderEntity c)
    {
        float scale=c.ModelScale;
        bool walking=(!c.IsCombatCoder || c.Aggro) && c.State is CoderAIState.WalkingToBait or CoderAIState.PanickedFleeing or CoderAIState.CombatHostile;
        float bob=walking ? .035f*scale*MathF.Abs(MathF.Sin(c.WalkCycle)) : 0;
        float tilt=c.IsCombatCoder && c.State==CoderAIState.Subdued
            ? -1.45f*Math.Clamp((c.KnockoutTime+.05f)*4,0,1)
            : c.HitStaggerTimer>0 ? -.32f*Math.Min(1,c.HitStaggerTimer*6) : c.State==CoderAIState.Subdued ? -.48f : 0;
        Color tint=c.HitFlashTimer>0 ? Color.White : Raylib.ColorLerp(Color.White,c.HoodieColor,c.IsCombatCoder ? .87f : .36f);
        VisualEngine3D.DrawCoder(c,c.Position+Vector3.UnitY*bob,tilt,tint);
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
        if(!VisualEngine3D.CastingShadows && c.Rarity!=VibeCoderRarity.Common && c.Hp>0)
        {
            Color ink=VibeCoderCollection.Ink(c.Rarity);
            float angle=_worldClock*.75f+c.Id;
            for(int i=0;i<40;i++)
            {
                if(i%10>=8) continue;
                Vector3 Ring(float t)=>c.Position+new Vector3(.48f*MathF.Cos(t),.035f,.48f*MathF.Sin(t));
                Raylib.DrawLine3D(Ring(angle+i*MathF.Tau/40),Ring(angle+(i+1)*MathF.Tau/40),Raylib.Fade(ink,.72f));
            }
            for(int i=0;i<5;i++)
            {
                float t=(_worldClock*.44f+i*.21f)%1;
                float a=angle+i*2.4f;
                Vector3 mote=c.Position+new Vector3(.49f*MathF.Cos(a),.07f+t*.83f,.49f*MathF.Sin(a));
                VisualEngine3D.Oval(mote,new Vector3(.013f,.034f,.013f)*MathF.Sin(t*MathF.PI),ink,Finish.Lamp);
            }
        }
        if(!VisualEngine3D.CastingShadows && c.Archetype==CoderArchetype.TeamLeadMeeter && c.State==CoderAIState.CombatHostile)
            Raylib.DrawCircle3D(c.Position+Vector3.UnitY*.04f,5,Vector3.UnitX,90,new Color(128,224,149,155));
        if (c.IsCombatCoder && c.Windup>0)
        {
            Vector3 forward=new(MathF.Sin(c.FacingYaw),0,MathF.Cos(c.FacingYaw));
            float lift = 1 - c.Windup/(c.WindingSlap ? .38f : .58f);
            Vector3 hand=c.CombatCenter+forward*.24f+new Vector3(forward.Z,.0f,-forward.X)*.19f+Vector3.UnitY*(.13f+.22f*lift);
            VisualEngine3D.Rod(hand-forward*.13f-Vector3.UnitY*.15f,hand,.055f,.046f,8,c.HoodieColor);
            VisualEngine3D.Oval(hand,new Vector3(.07f+.045f*lift),new(255,88,34,255),Finish.Lamp);
        }

    }

    private void DrawEscapedAgentBoss3D()
    {
        var b=ElevatorBoss;
        if (!BossModelVisible) return;
        Color tint=b.State==ElevatorBossState.Repairing ? new Color(73,65,70,255) : b.HitFlashTimer>0 ? new Color(255,225,235,255) : b.State==ElevatorBossState.StunnedWallCrash
            ? new Color(255,166,166,255) : Color.White;
        // MPFB anatomy faces +Z, matching FacingYaw (the retired robot faced -Z).
        float shutdown=b.IsDefeated ? Math.Clamp(1-Systems.ShutdownTime/.8f,0,1) : 0;
        VisualEngine3D.Asset("escaped_ai_boss",b.Position,b.FacingYaw*180/MathF.PI,scale:1.32f,
            tint:Raylib.ColorLerp(tint,Color.Gray,shutdown),tilt:shutdown*.32f,stretch:new Vector3(1,1-.33f*shutdown,1));
        if(BossRevealed && b.ShieldHp>0 && !VisualEngine3D.CastingShadows)
            Raylib.DrawSphereWires(b.Position+Vector3.UnitY*1.42f,1.48f,6,12,new Color(115,198,222,95));
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
        for (int i = 0; i < EnemyProjectiles.Count; i++)
        {
            var pr = EnemyProjectiles[i];
            if(pr.Coin) {
                VisualEngine3D.Oval(pr.Position,new(.095f,.095f,.027f),Color.Gold,Finish.Lamp);
                continue;
            }
            if (pr.IsGlitch)
            {
                Vector3 dir = pr.Velocity.LengthSquared() > 1e-6f ? Vector3.Normalize(pr.Velocity) : Vector3.UnitZ;
                VisualEngine3D.Rod(pr.Position - dir * .16f, pr.Position, .022f, .008f, 8, pr.PrimaryColor, Finish.Lamp);
                VisualEngine3D.Oval(pr.Position, new Vector3(.036f, .022f, .036f), pr.WireColor, Finish.Lamp);
                continue;
            }
            // Fixed visual envelope: 14 cm diameter, 6 cm thickness. Damage radius is independent.
            VisualEngine3D.Oval(pr.Position, new Vector3(.07f, .03f, .07f), pr.PrimaryColor);

        }
    }

    // =========================================================================
    // 2D WORLD-TO-SCREEN LABELS, HEALTH BARS, ANXIETY BARS & INTERACTION HINTS
    // =========================================================================
    private enum FocusKind { None, Terminal, Frog, Offer, MacBook, Veronika, Shop, Elevator, Desk, Coder, Board, Barrier, Pizza, Cooler, Coffee, Rack, Printer, Deploy, Extinguisher }
    private readonly record struct Focus(FocusKind Kind, string Label, int Index);
    internal const string HandsFullPrompt="Убери оружие (повторно [1–4]), чтобы поднять";
    internal const string HandsFullMessage="Нужны голые руки — нажми слот [1–4] ещё раз (или купи Плечевой ремень)";
    internal bool CanCarryWith(WeaponType tool) => tool==WeaponType.BareHands || Systems.Has(Sidegrade.CarryHarness);

    private string PickUp(VibeCoderEntity c,Action<int,string>? grant,WeaponType tool)
    {
        if (CarriedCoder != null) return "Сначала отведи пойманного кодера к Веронике или в Опенспейс";
        if(c.CanSignNda && !CanCarryWith(tool)) return HandsFullMessage;
        Systems.Style(StyleAction.Capture);
        Systems.Risk(security:.5f+(Systems.Has(Sidegrade.CarbonNda) ? 6 : 0));
        if(!c.IdentityRevealed && !c.IsMiniBoss)
        {
            // Slots from before the collection had archetypes but no species IDs.
            var species=VibeCoderCollection.All.FirstOrDefault(s=>s.Id==c.SpeciesId)
                ?? VibeCoderCollection.All.First(s=>s.Archetype==c.Archetype);
            VibeCoderCollection.Dress(c,species);
            c.Name=species.Name;
            Systems.Discover(species,grant);
        }
        c.IdentityRevealed=true;
        LootElevatorKey(c);
        ActiveCoders.Remove(c);
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
            case FocusKind.Frog: return CatchSweep(camera,false);
            case FocusKind.Offer:
                var candidate=ActiveCoders[focus.Index];
                if(!spend(80)) return "На аванс нужно $80";
                if(candidate.Archetype==CoderArchetype.SecurityGuard) return PickUp(candidate,grant,tool);
                candidate.Pressure(24,0,-6,45);
                return candidate.State==CoderAIState.Subdued ? "Аванс принят. Подпиши NDA [E]" : "Кандидат изучает оффер";
            case FocusKind.Board:
                return Systems.Audit>0 ? Systems.ResolveAudit(0,this,spend) : "Доска контрактов [J]";
            case FocusKind.Barrier:
                if(Systems.BarrierVoiceWait<=0) { Systems.BarrierVoiceWait=25; FloorSystems.SpeakCoaching(FloorSystems.LockedLine); }
                return BarrierHint;
            case FocusKind.Pizza:
                if(Systems.StationWait.ContainsKey(focus.Index)) return "Пицца закончилась";
                Systems.StationWait[focus.Index]=20;
                return Program.Heal(35,0,"ПИЦЦА");
            case FocusKind.Cooler when Program.NeedsHealing:
                if(Systems.StationWait.ContainsKey(focus.Index)) return "Кулер наполняется";
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
                ActivateMacBookTrap(SelectedBaitTier,spend,out string trap);
                return trap;
            case FocusKind.Elevator:
                TrySummonElevatorBoss(out string lift);
                return lift;
            case FocusKind.Shop:
                SelectedBaitTier=(SubscriptionBaitTier)(((int)SelectedBaitTier+1)%4);
                return GetBaitInfo(SelectedBaitTier).Description;
            default: return HandleInteractKeyE(camera.Position,spend,grant,tool);
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
            if (alignment > best) { best = alignment; focus = new(kind,name,index); }
        }
        for(int i=0;i<stations.Count;i++)
        {
            var station=stations[i];
            string label=station.Kind==FocusKind.Board && Systems.Audit>0 ? "Оплатить аудит ($180)" : station.Label;
            if(station.Kind==FocusKind.Pizza && (!Program.NeedsHealing || Systems.StationWait.ContainsKey(i))) continue;
            if(station.Kind==FocusKind.Cooler && Program.NeedsHealing) label=Systems.StationWait.ContainsKey(i) ? "Кулер наполняется" : "Выпить воды (+25 HP)";
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
        Consider(CentralMacBookPos, TrapTop+.13f, 3.8f, FocusKind.MacBook, "ЯблоБук-ловушка");
        string deposit=$"Сдать Джаба-жаб ({Systems.PocketFrogs.Count} шт.)";
        Consider(TerminalPos,1.08f,3f,FocusKind.Terminal,Systems.PocketFrogs.Count>0 ? deposit : "Баг-трекер принимает Джаба-жаб. Садок пуст");
        Consider(HrTarotBoothPos,1.6f,4.6f,FocusKind.Veronika,CarriedCoder!=null ? "Сдать кадра HR Веронике" :
            Systems.Onboarding==OnboardingStep.TalkToVeronika ? "Поговорить с Вероникой" :
            Systems.Onboarding!=OnboardingStep.BuyNet && Systems.PocketFrogs.Count>0 ? "Сдать Джаба-жаб Веронике" : VeronikaShop);
        Consider(ArsenalShopPos, 1.1f, 4.2f, FocusKind.Shop, "Арсенал и подписки");
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
                Consider(ActiveCoders[i].Position,.45f,3.8f,FocusKind.Coder,CanCarryWith(tool) ? "Подписать NDA и поднять" : HandsFullPrompt,i);
        if(tool==WeaponType.BareHands)
            foreach(var frog in Frogs)
            {
                float aim=CatchAim(camera,frog.Feet+Vector3.UnitY*.15f,2.4f);
                if(aim>Math.Max(best,HandAim(frog))) { best=aim; focus=new(FocusKind.Frog,"Поймать Джаба-жабу",-1); }
            }
        if(!Systems.WingsUnlocked && barrierContact>0 && focus.Kind==FocusKind.None) return new(FocusKind.Barrier,BarrierHint,-1);
        return focus;
    }

    internal static readonly Vector3 LiftSignSize=new(5.78f,1.17f,.08f);
    internal static Vector3 LiftSignCenter(Vector3 face,int side) => face+new Vector3(0,4.85f,side*.48f);

    internal (Vector3 Position,float Height,string Label)? OnboardingWaypoint(Vector3 eye)
    {
        switch(Systems.Onboarding)
        {
            case OnboardingStep.TalkToVeronika: return (HrTarotBoothPos,2.12f,"HR Вероника [E]");
            case OnboardingStep.DepositFrogs: return (TerminalPos,1.65f,"Баг-трекер [E]");
            case OnboardingStep.BuyNet: return (HrTarotBoothPos,2.12f,"Вероника: Сачок $3 [E]");
            case OnboardingStep.ActivateBait: return (CentralMacBookPos,TrapTop+.62f,"ЯблоБук-приманка [E]");
            case OnboardingStep.CatchFrogs:
                var frog=Frogs.Where(f=>f.CatchableByHand && ReachableFrog(eye,f.Feet))
                    .MinBy(f=>Vector3.DistanceSquared(eye,f.Feet));
                return frog==null ? null : (frog.Feet,.43f,"Джаба-жаба [E]");
            case OnboardingStep.SeatFirstGalley:
                var desk=GalleyDesks.Where(d=>!d.IsOccupied).MinBy(d=>Vector3.DistanceSquared(eye,d.DeskPosition));
                if(CarriedCoder!=null && desk!=null) return (desk.DeskPosition,1.5f,"Посади в Опенспейс [E]");
                if(ActiveCoders.Count==0) return (CentralMacBookPos,TrapTop+.62f,"Примани кадра для Опенспейса [E]");
                goto case OnboardingStep.CaptureFirstCoder;
            case OnboardingStep.HealAtCoffee: return (CoffeePos,1.62f,"Выпей эспрессо (+45 HP) [E]");
            case OnboardingStep.CaptureFirstCoder when CarriedCoder!=null:
                return (HrTarotBoothPos,2.12f,"Сдай Веронике [E]");
            case OnboardingStep.StunAndHeal:
            case OnboardingStep.CaptureFirstCoder:
                var coder=ActiveCoders.Where(c=>(c.Hp>0 || c.CanSignNda) && c.Archetype!=CoderArchetype.SecurityGuard)
                    .MinBy(c=>Vector3.DistanceSquared(eye,c.Position));
                return coder==null ? (CentralMacBookPos,TrapTop+.62f,"Дождись вайбкодера") :
                    (coder.Position,coder.CanSignNda ? .82f : 1.84f*coder.ModelScale,coder.CanSignNda ? "Подпиши NDA [E]" : "Оглуши ЛКМ → NDA [E]");
            default: return null;
        }
    }

    // A frog around a partition can still be caught from the open side.
    private bool ReachableFrog(Vector3 eye,Vector3 feet)
    {
        Vector3 point=feet+Vector3.UnitY*.15f;
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
                string plate=$"ЗАКРЫТО   КАРАНТИН ({Math.Min(TotalCodersSoldToHr,3)}/3)";
                Program.ShadowText(font,plate,point.X-GameFonts.Measure(font,plate,16).X/2,point.Y,16,new Color(255,213,170,255));
            }
        var waypoint=OnboardingWaypoint(camera.Position);
        if(waypoint is {} goal)
            WorldLabelLayout.DrawWaypoint(camera,goal.Position,goal.Height+.055f*MathF.Sin(_worldClock*2),goal.Label,Systems.Onboarding==OnboardingStep.HealAtCoffee ? new Color(149,255,201,255) : null);
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
        if(Partner is {} partner) ProtectHead(partner.Feet,1,partner.Pose.Eye.Y<1.4f?.75f:1);
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
            Font tag=GameFonts.Get(Typeface.InterfaceBold,16);
            float width=GameFonts.Measure(tag,name,16).X;
            if (!WorldLabelLayout.Place(screen,width,22,out var r)) return;
            Program.ShadowText(tag,name,r.X,r.Y,16,mystery ? accent : new Color(247,244,235,255));
            if(health<0) return;
            float barWidth = MathF.Min(width,62);
            Rectangle track=new(r.X+(width-barWidth)/2,r.Y+19,barWidth,3);
            Raylib.DrawRectangleRec(track,new Color(14,18,26,145));
            track.Width*=Math.Clamp(health,0,1);
            if(track.Width>=1) Raylib.DrawRectangleRec(track,accent);
        }
        foreach(var frog in Frogs)
        {
            if(waypoint?.Position==frog.Feet) continue;
            bool rare=frog.Species.Rarity!=VibeCoderRarity.Common;
            float alignment=CatchAim(camera,frog.Feet+Vector3.UnitY*.15f,rare ? 8 : 4.5f);
            if(alignment<(rare ? .91f : .98f)) continue;
            string label=Systems.DiscoveredSpecies.ContainsKey(frog.SpeciesId) ? frog.Species.Name : rare ? "★ ???" : "???";
            Plate(frog.Feet+Vector3.UnitY*.38f,label,-1,frog.Species.HoodieColor,true);
        }
        if(waypoint?.Position!=HrTarotBoothPos && Vector3.DistanceSquared(camera.Position,HrTarotBoothPos)<100 && FindFocus(camera).Kind!=FocusKind.Veronika)
            Plate(HrTarotBoothPos+Vector3.UnitY*1.87f,"HR Вероника",1f,new Color(168,153,202,255));
        if(Partner is {} ally && Vector3.DistanceSquared(camera.Position,ally.Feet)<400)
            Plate(ally.Feet+Vector3.UnitY*(ally.Pose.Eye.Y<1.4f?1.30f:1.72f),"БРАТАН [КООП]",
                Program.PartnerHp/100f,new Color(109,202,162,255));
        foreach(var c in ActiveCoders)
        {
            if(waypoint?.Position==c.Position && c.Composure>=99) continue;
            if(Vector3.DistanceSquared(camera.Position,c.Position)>100 || c.Hp<=0 && !c.KnockedOutAlive) continue;
            Plate(c.Position+Vector3.UnitY*(1.58f*c.ModelScale),c.Deadline>0 ? $"Дедлайн! {Math.Ceiling(c.Deadline)} с" : c.IdentityRevealed ? c.Name : c.MysteryTag,
                c.Composure<99 || c.State==CoderAIState.Subdued || Vector3.DistanceSquared(camera.Position,c.Position)<56.25f ? c.Composure/100 : -1,
                VibeCoderCollection.Ink(c.Rarity),true);
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

    internal static void DrawCoder(VibeCoderEntity c,Vector3 feet,float tilt,Color tint)
    {
        const string asset="char_vibe_coder";
        if(CastingShadows)
        {
            DrawDepth(imports[asset],Matrix4x4.CreateRotationX(tilt)*Matrix4x4.CreateScale(c.ModelScale)*
                Matrix4x4.CreateRotationY(c.FacingYaw)*Matrix4x4.CreateTranslation(feet),droppedLaptop:c.LaptopLost);
            return;
        }
        if (c.SpeciesId.Length==0 && !c.IsCombatCoder && !c.LaptopLost && c.HitFlashTimer<=0)
        {
            Asset(asset,feet,c.FacingYaw*180/MathF.PI,c.ModelScale,tint,tilt);
            return;
        }
        var model=imports[asset];
        Span<Color> saved=stackalloc Color[model.MaterialCount];
        float flash=Math.Clamp(c.HitFlashTimer/.14f,0,1)*.28f;
        for(int i=0;i<saved.Length;i++)
        {
            ref var pigment=ref model.Materials[i].Maps[0].Color;
            saved[i]=pigment;
            // raylib material zero is the fallback; these are the authored blue,
            // navy and teal swatches of the single shared char_vibe_coder GLB.
            if(c.IsCombatCoder || c.SpeciesId.Length>0)
                pigment=CoderPigment(i,c.HoodieColor,c.GlassesColor,pigment);
            if (i is 3 or 5)
                pigment=Raylib.ColorLerp(pigment,new Color(255,195,130,255),flash);
        }
        if(c.LaptopLost)
        {
            Surface(Finish.Anatomy,0);
            var pose=Matrix4x4.CreateRotationX(tilt)*Matrix4x4.CreateScale(c.ModelScale)*
                Matrix4x4.CreateRotationY(c.FacingYaw)*Matrix4x4.CreateTranslation(feet);
            for(int mesh=0;mesh<model.MeshCount;mesh++)
            {
                int material=model.MeshMaterial[mesh];
                // Final four authored swatches belong to the chassis, glass, display and glow.
                if(material<11) Raylib.DrawMesh(model.Meshes[mesh],model.Materials[material],Matrix4x4.Transpose(pose));
            }
        }
        else Asset(asset,feet,c.FacingYaw*180/MathF.PI,c.ModelScale,Color.White,tilt);
        for(int i=0;i<saved.Length;i++) model.Materials[i].Maps[0].Color=saved[i];
    }
}

public sealed partial class SkyscraperFloor1World
{
    internal FloorSystems Systems = new();
    internal void ResumeCoderIds() => _nextCoderId=1+Math.Max(ActiveCoders.Select(c=>c.Id).DefaultIfEmpty().Max(),CarriedCoder?.Id ?? 0);
    internal Vector3 RackPos => AmbushCovers[3].Position;
    internal Vector3 CoolerPos => AmbushCovers[2].Position;
    internal static readonly Vector3 DeployPos=new(-2.6f,0,1.48f), CoffeePos=new(2.5f,0,12.9f);
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
        ActiveCoders.Add(c);
        return c;
    }

    internal void EnsureCandidate(CoderArchetype role)
    {
        if(Systems.WingsUnlocked && IsMacBookTrapActive && !ActiveCoders.Exists(c=>c.Archetype==role && c.Hp>0))
            SpawnFighter(role,EastSpawnDoorPos);
    }

    internal void SpawnWave(int count,bool raid=false)
    {
        if(!Systems.WingsUnlocked || !IsMacBookTrapActive) return;
        for(int i=0;i<count && (ActiveCoders.Count<22 || raid && i==0);i++)
        {
            float side=i%2==0 ? 1 : -1;
            Vector3 spawn=Systems.Friday>0 ? new(7+i*1.6f,0,5) : new(side*(11+i),0,6+i);
            var species=VibeCoderCollection.Roll(speciesRandom,SelectedBaitTier,Systems,raid ? CoderArchetype.SecurityGuard : null);
            var c=SpawnFighter(species.Archetype,spawn,species);
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

    private string Spill(Vector3 ground)
    {
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
            if(p.Deflected || p.IsNodeModulesCube || offset.LengthSquared()>(wide ? 12.25f : 7.84f) ||
                Vector3.Dot(aim,Vector3.Normalize(offset))<(wide ? .42f : .63f) || Vector3.Dot(p.Velocity,offset)>=0 ||
                TraceSurface(cam.Position,Vector3.Normalize(offset),offset.Length())+.02f<offset.Length()) continue;
            var owner=ActiveCoders.Find(c=>c.Id==p.OwnerId && c.Hp>0);
            Vector3 direction=owner!=null ? Vector3.Normalize(owner.CombatCenter-p.Position) : -Vector3.Normalize(p.Velocity);
            p.Velocity=direction*Math.Max(14,p.Velocity.Length()*1.4f);
            p.Damage*=2; p.Deflected=true; p.WireColor=p.PrimaryColor=Color.Gold; p.Life=3;
            EnemyProjectiles[i]=p;
            Systems.Style(StyleAction.Parry);
            feedback="ОТБИЛ БАГ РЕМНЁМ!";
            weapon.TriggerWeaponAttack(cam,p.Position,true,0,true,customHitLabel:feedback);
            weapon.SpawnBurstSparks(p.Position,direction,24,Color.Gold,Color.Yellow,2,6,false);
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
        int cash=c.IsCombatCoder ? 35 : Math.Max(35,c.TarotPayoutValue/3);
        Systems.Risk(security:14); Systems.Earned+=cash;
        SpawnCashBurst(c.CombatCenter,8,$"+${cash}"); pay?.Invoke(cash,"Кандидат");
        AudioMusicSystem.PlaySubduedDrop();
        LootElevatorKey(c);
    }

    internal Vector3 ContractBoardFeet => HrTarotBoothPos+new Vector3(-3.35f,0,1.45f);

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
        Vector3 board=ContractBoardFeet+Vector3.UnitY*1.35f;
        Box(board,.73f,.48f,.09f,steel);
        Box(board+Vector3.UnitZ*.055f,.62f,.34f,.025f,new(202,185,146,255));
        for(int i=0;i<3;i++) Box(board+new Vector3(-.07f,.08f-.08f*i,.074f),.35f,.016f,.01f,steel);
        if(!VisualEngine3D.CastingShadows && Systems.RackHeat>0)
        {
            for(int i=0;i<14;i++)
            {
                float t=(_worldClock*.65f+i/14f)%1;
                var puff=RackPos+new Vector3(.25f*MathF.Sin(i*9+t*4),1.83f+t*2.6f,.19f*MathF.Cos(i*7+t));
                VisualEngine3D.Oval(puff,new Vector3(.12f+t*.35f),new(87,83,81,(int)(174*(1-t))),Finish.RosinSmoke);
                var spark=RackPos+new Vector3(.36f*MathF.Sin(i),.6f+1.3f*t,.51f);
                VisualEngine3D.Rod(spark,spark+new Vector3(.04f,.09f,.06f),.012f,.004f,6,Color.Orange,Finish.Lamp);
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
    internal void AdvanceArc(float dt) => Phase=Math.Min(1,Phase+dt/(.35f+.30f*Species.HopHeight));
    internal VibeCoderSpecies Species => VibeCoderCollection.Frogs[SpeciesId];
    internal Vector3 Feet => Vector3.Lerp(From,To,Phase)+Vector3.UnitY*(4*Phase*(1-Phase)*Species.HopHeight);
}

public sealed partial class SkyscraperFloor1World
{
    internal readonly List<BugFrog> Frogs=new();
    internal static readonly Vector3 TerminalPos=new(-2.05f,0,8.85f);
    private readonly Random frogRandom;
    private float refill, previousDebt, croakDelay;

    internal void MigrateTrainingAtrium()
    {
        SeedLobby();
        if(!IsMacBookTrapActive) return;
        ActiveCoders.RemoveAll(c=>!c.TimidIntern);
        if(ActiveCoders.Count>0) ActiveCoders[0].Position=new(-7.5f,0,5.2f);
        if(CarriedCoder!=null) Systems.Onboarding=OnboardingStep.CaptureFirstCoder;
        else ActivateMacBookTrap(SubscriptionBaitTier.FreeTier,_=>false,out _);
    }

    private void SeedLobby()
    {
        Frogs.Clear(); previousDebt=0; refill=4;
        Vector3[] pads=[new(-1,0,10.05f),new(.95f,0,9.4f),new(-.55f,0,8.1f),
            new(2.1f,0,8.4f),new(-3.1f,0,7.2f),new(1.1f,0,6.7f),new(-5,0,11),new(5,0,10)];
        for(int i=0;i<pads.Length;i++)
        {
            var frog=SpawnFrog(pads[i],i<3 ? "frog_hopper" : i==3 ? "frog_crash" : i==4 ? "frog_syntax" : null);
            BeginHop(frog,frog.From+Vector3.UnitZ*3);
            frog.Phase=i*.12f; frog.Yaw=frog.TargetYaw;
        }
    }

    private BugFrog SpawnFrog(Vector3 p,string? id=null)
    {
        float roll=frogRandom.NextSingle();
        p=ResolvePlayerCollision(p,.22f);
        var frog=new BugFrog {SpeciesId=id ?? (roll<.035f ? "frog_hotfix" : roll<.20f ? "frog_crash" :
            roll<.47f ? "frog_syntax" : "frog_hopper"),From=p,To=p,Rest=.6f+1.2f*frogRandom.NextSingle()};
        Frogs.Add(frog);
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

    private void TickFrogs(float dt,Vector3 eye,Vector3? guest)
    {
        refill-=dt; croakDelay-=dt;
        if(Systems.TD<previousDebt) previousDebt=Systems.TD;
        if(Systems.WingsUnlocked && Frogs.Count<24 && Systems.TD>previousDebt && (previousDebt==0 || Systems.TD-previousDebt>=1))
        {
            SpawnFrog(RackPos+new Vector3(frogRandom.NextSingle()*3-1.5f,0,1.4f+frogRandom.NextSingle()));
            previousDebt=Systems.TD;
        }
        if(refill<=0 && Frogs.Count<8)
        {
            SpawnFrog(new(frogRandom.NextSingle()*5-2.5f,0,7+frogRandom.NextSingle()*3));
            refill=3.5f;
        }
        foreach(var frog in Frogs)
        {
            Vector3 near=guest is {} g && Vector3.DistanceSquared(frog.Feet,g)<Vector3.DistanceSquared(frog.Feet,eye) ? g : eye;
            if(frog.Rest>0)
            {
                frog.Rest-=dt;
                if(frog.Rest<=0) BeginHop(frog,near);
                continue;
            }
            float turn=MathF.IEEERemainder(frog.TargetYaw-frog.Yaw,MathF.Tau);
            frog.Yaw+=turn*(1-MathF.Exp(-14*dt));
            frog.AdvanceArc(dt);
            if(frog.Phase<1) continue;
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

    private string CatchSweep(Camera3D camera,bool net)
    {
        var targets=Frogs.Select(f=>(Frog:f,Aim:CatchAim(camera,f.Feet+Vector3.UnitY*.15f,net ? 3.6f : 2.4f)))
            .Where(t=>t.Aim>(net ? .70f : HandAim(t.Frog))).OrderByDescending(t=>t.Aim).Take(net ? 3 : 1).ToArray();
        foreach(var (frog,_) in targets)
        {
            if(!net && !frog.CatchableByHand)
            {
                BeginHop(frog,camera.Position);
                return "Нужен Сачок ($3) у Вероники";
            }
            if(Systems.Has(Sidegrade.CarbonNda)) Systems.Risk(security:6);
            Systems.PocketFrogs.Add(frog.SpeciesId); Frogs.Remove(frog);
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
