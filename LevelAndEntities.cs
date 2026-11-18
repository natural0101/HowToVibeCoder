#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public enum SubscriptionBaitTier
{
    FreeTier = 0,         // $0   - attracts Junior Tabber (100 HP, $150 payout)
    CursorPro = 1,        // $20  - attracts Prompt-Bro (160 HP, $240 payout)
    TurkishAccount = 2,   // $80  - attracts Cursor Adept (240 HP, $360 payout)
    CloudEntropicMax = 3  // $200 - spawns Mini-Boss "ГУРУ КУРСОРА: SAAS ЗА 2 ЧАСА ($60K MRR)" (600 HP, $600 payout)
}

public enum CoderArchetype
{
    JuniorTabber = 0,
    PromptBro = 1,
    CursorAdept = 2,
    SaasGuruMiniBoss = 3
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
    public float AttackCooldown;
    public int TarotPayoutValue;
    public Color HoodieColor;
    public Color GlassesColor;
    public string SpeechBubble = "";
    public float SpeechTimer;
    public int SpawnSide; // -1 = West door, +1 = East door

    public bool IsMiniBoss => Archetype == CoderArchetype.SaasGuruMiniBoss;
}

public sealed class GalleyDeskSlot
{
    public int Index;
    public Vector3 DeskPosition;
    public Vector3 ChairPosition;
    public bool IsOccupied;
    public string CoderName = "";
    public CoderArchetype Archetype;
    public Color HoodieColor;
    public bool IsSlackingInTwitter;
    public float SlackCycleTimer;
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
    public bool IsNodeModulesCube;
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

public sealed class EscapedAgentBoss
{
    public bool IsActive;
    public bool IsDefeated;
    public string Name = "СБЕЖАВШИЙ ИИ-АГЕНТ v0.1 (CLOUD ENTROPIC)";
    public Vector3 Position = new(0f, .16f, -4f);
    public Vector3 Velocity;
    public float FacingYaw;
    public float Hp = 2000f;
    public float MaxHp = 2000f;
    public ElevatorBossState State = ElevatorBossState.Inactive;
    public float StateTimer;
    public float AttackSubTimer;
    public float WheelSpinPhase;
    public float HitFlashTimer;
    public string SpeechBubble = "";
    public float SpeechTimer;
    public Vector3 ChargeDirection;
}

public sealed class SkyscraperFloor1World
{
    // Massive Warface "White Shark" («Белая Акула») Tactical Skyscraper Floor 1 (76m x 76m!)
    public const float HalfWidth = 38.0f;
    public const float HalfDepth = 38.0f;
    public const float CeilingHeight = 6.5f;

    // Central Armored Elevator Shaft right in the CENTER of the floor (9m x 9m core)
    public readonly Vector3 CentralElevatorCorePos = new(0.0f, 0f, -4.0f);
    public const float ElevatorCoreHalfW = 4.5f;
    public const float ElevatorCoreHalfD = 4.5f;

    // Key Functional Landmarks
    public readonly Vector3 HrTarotBoothPos = new(-7.4f, 0f, 15.2f);
    public readonly Vector3 ArsenalShopPos = new(0.0f, 0f, 16.5f);
    public readonly Vector3 CentralMacBookPos = new(0.0f, 0f, 5.2f);
    public readonly Vector3 NorthElevatorPos = new(0.0f, 0f, -8.5f);
    public readonly Vector3 SouthElevatorDoorPos = new(0.0f, 0f, 0.5f);
    public readonly Vector3 ElevatorButtonPos = new(3.2f, 0f, -9.2f);
    public readonly Vector3 SouthElevatorButtonPos = new(3.2f, 0f, 1.1f);
    public readonly Vector3 WestSpawnDoorPos = new(-20.5f, 0f, 5.2f);
    public readonly Vector3 EastSpawnDoorPos = new(20.5f, 0f, 5.2f);

    // Architectural Pillars, Ambush Covers, Galley Desks & 75+ Rich 3D Office Props
    public readonly List<PillarObstacle> Pillars = new(16);
    public readonly List<AmbushCoverObject> AmbushCovers = new(8);
    public readonly List<GalleyDeskSlot> GalleyDesks = new(4);
    public readonly List<OfficeProp3D> OfficeProps = new(110);

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
    public int TotalCodersSoldToHr = 0;

    // Elevator & Progression State
    public bool HasElevatorKey = false;
    public float ElevatorDoorOpenAmount = 0f; // 0 = closed, 1 = open
    public bool IsFloor2Unlocked = false;
    public float WarningBeaconPhase = 0f;

    private float _worldClock = 0f;
    private readonly Random _rng = new(1994);

    public SkyscraperFloor1World()
    {
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
            PosterSubtitle = "БЕЛАЯ АКУЛА IT-БИЗНЕСА",
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
            Position = new Vector3(4.6f, 0f, 4.4f),
            Radius = 2.45f,
            VisualKind = 3
        });

        // 4 Galley Desks in South-East Galley Zone
        Vector3[] galleyCoords =
        {
            new(6.8f, 0f, 13.4f),
            new(11.2f, 0f, 13.4f),
            new(6.8f, 0f, 17.0f),
            new(11.2f, 0f, 17.0f)
        };

        for (int i = 0; i < 4; i++)
        {
            GalleyDesks.Add(new GalleyDeskSlot
            {
                Index = i,
                DeskPosition = galleyCoords[i],
                ChairPosition = galleyCoords[i] + new Vector3(0f, 0f, 0.82f),
                IsOccupied = false,
                CoderName = "",
                IsSlackingInTwitter = false,
                SlackCycleTimer = 18f + i * 4f
            });
        }

        // Populate 85+ 3D Office Props across the Left Fork, Right Fork, Central Atrium, and North Hall!
        PopulateRichOfficeProps();

        // Reset Boss & Progression
        ElevatorBoss.IsActive = false;
        ElevatorBoss.IsDefeated = false;
        ElevatorBoss.Hp = ElevatorBoss.MaxHp;
        ElevatorBoss.State = ElevatorBossState.Inactive;
        ElevatorBoss.Position = CentralElevatorCorePos + Vector3.UnitY*.16f;
        ElevatorDoorOpenAmount = 0f;
        IsFloor2Unlocked = false;
        CarriedCoder = null;
    }

    private void PopulateRichOfficeProps()
    {
        // 1. LEFT FORK (WEST OPEN-SPACE CUBICLES, DESKS, CHAIRS, XEROX, WHITEBOARDS & WATER COOLERS)
        float[] westRowsZ = { 12.0f, 5.5f, -1.5f, -8.5f, -15.5f, -22.5f };
        for (int r = 0; r < westRowsZ.Length; r++)
        {
            float rz = westRowsZ[r];
            for (int c = 0; c < 3; c++)
            {
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
    }

    public static (string Name, int Cost, Color NeonColor, string Description) GetBaitInfo(SubscriptionBaitTier tier) => tier switch
    {
        SubscriptionBaitTier.FreeTier => ("Free Tier ($0)", 0, new Color(45, 245, 130, 255), "Приманивает Джуниор-Табберов (100 ХП | Выкуп $150)"),
        SubscriptionBaitTier.CursorPro => ("Cursor Pro ($20)", 20, new Color(40, 215, 255, 255), "Приманивает Промпт-Бро (160 ХП | Выкуп $240)"),
        SubscriptionBaitTier.TurkishAccount => ("Турецкий Аккаунт ($80)", 80, new Color(215, 75, 255, 255), "Приманивает Адептов Курсора (240 ХП | Выкуп $360)"),
        SubscriptionBaitTier.CloudEntropicMax => ("Cloud Entropic Max ($200)", 200, new Color(255, 185, 35, 255), "ВЫЗЫВАЕТ МИНИ-БОССА «ГУРУ КУРСОРА: SAAS ЗА 2 ЧАСА» (600 ХП | Выкуп $600)"),
        _ => ("Free Tier ($0)", 0, Color.Lime, "")
    };

    // =========================================================================
    // COLLISION & AMBUSH DETECTION
    // =========================================================================
    public Vector3 ResolvePlayerCollision(Vector3 desiredPos, float playerRadius = 0.55f)
    {
        Vector3 pos = desiredPos;
        pos.X = Math.Clamp(pos.X, -HalfWidth + playerRadius, HalfWidth - playerRadius);
        pos.Z = Math.Clamp(pos.Z, -HalfDepth + playerRadius, HalfDepth - playerRadius);

        // Collide with 6 Pillars
        for (int i = 0; i < Pillars.Count; i++)
        {
            PillarObstacle p = Pillars[i];
            float minX = p.Center.X - p.HalfWidth - playerRadius;
            float maxX = p.Center.X + p.HalfWidth + playerRadius;
            float minZ = p.Center.Z - p.HalfDepth - playerRadius;
            float maxZ = p.Center.Z + p.HalfDepth + playerRadius;

            if (pos.X > minX && pos.X < maxX && pos.Z > minZ && pos.Z < maxZ)
            {
                float overlapLeft = pos.X - minX;
                float overlapRight = maxX - pos.X;
                float overlapTop = pos.Z - minZ;
                float overlapBottom = maxZ - pos.Z;
                float minOverlap = MathF.Min(MathF.Min(overlapLeft, overlapRight), MathF.Min(overlapTop, overlapBottom));

                if (minOverlap == overlapLeft) pos.X = minX;
                else if (minOverlap == overlapRight) pos.X = maxX;
                else if (minOverlap == overlapTop) pos.Z = minZ;
                else pos.Z = maxZ;
            }
        }

        // Collide with Central Armored Elevator Core (9m x 9m floor-to-ceiling shaft at (0, 0, -4.0f))
        pos = PushOutOfBoxXZ(pos, CentralElevatorCorePos, ElevatorCoreHalfW, ElevatorCoreHalfD, playerRadius);

        // Collide with Central MacBook Trap Desk
        pos = PushOutOfBoxXZ(pos, CentralMacBookPos, 1.35f, 0.85f, playerRadius);

        // Collide with HR Tarot Table & 90s Shop Counter
        pos = PushOutOfBoxXZ(pos, HrTarotBoothPos, 1.65f, 1.05f, playerRadius);
        pos = PushOutOfBoxXZ(pos, ArsenalShopPos, 2.25f, 0.95f, playerRadius);

        // Collide with solid OfficeProps
        for (int i = 0; i < OfficeProps.Count; i++)
        {
            var prop = OfficeProps[i];
            if (!prop.HasCollision) continue;
            pos = PushOutOfBoxXZ(pos, prop.Position, prop.Size.X * 0.48f, prop.Size.Z * 0.48f, playerRadius * 0.8f);
        }

        return pos;
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

        IsPlayerInAmbush = nearCover || isCrouching;
    }

    // =========================================================================
    // TRAP ACTIVATION, CODER SPAWNING & AI LOOP
    // =========================================================================
    public bool ActivateMacBookTrap(SubscriptionBaitTier tier, Func<int, bool> trySpendMoney, out string statusMessage)
    {
        var info = GetBaitInfo(tier);
        if (info.Cost > 0 && !trySpendMoney(info.Cost))
        {
            statusMessage = $"НЕ ХВАТАЕТ БАКЛАЖАНОВ! Нужно ${info.Cost} на подписку {info.Name}!";
            return false;
        }

        SelectedBaitTier = tier;
        IsMacBookTrapActive = true;
        CoderSpawnTimer = 0.65f; // Spawn quickly so gameplay feels responsive

        AudioMusicSystem.PlayMacbookBait();
        statusMessage = $"МАКБУК АКТИВИРОВАН: {info.Name}! Прячься за диван/фикус или жми [C]!";
        return true;
    }

    public VibeCoderEntity SpawnCoderForTier(SubscriptionBaitTier tier, int forceSide = 0)
    {
        int side = forceSide != 0 ? forceSide : (_rng.Next(2) == 0 ? -1 : 1);
        Vector3 spawnPos = side < 0
            ? WestSpawnDoorPos + new Vector3(0.8f, 0f, (_rng.NextSingle() - 0.5f) * 1.4f)
            : EastSpawnDoorPos + new Vector3(-0.8f, 0f, (_rng.NextSingle() - 0.5f) * 1.4f);

        CoderArchetype archetype = tier switch
        {
            SubscriptionBaitTier.FreeTier => CoderArchetype.JuniorTabber,
            SubscriptionBaitTier.CursorPro => CoderArchetype.PromptBro,
            SubscriptionBaitTier.TurkishAccount => CoderArchetype.CursorAdept,
            SubscriptionBaitTier.CloudEntropicMax => CoderArchetype.SaasGuruMiniBoss,
            _ => CoderArchetype.JuniorTabber
        };

        VibeCoderEntity coder = archetype switch
        {
            CoderArchetype.JuniorTabber => new VibeCoderEntity
            {
                Id = _nextCoderId++,
                Archetype = CoderArchetype.JuniorTabber,
                Name = "Джуниор Таббер Данила",
                Subtitle = "Жмёт Tab не глядя | 0 дней без падения прода",
                Position = spawnPos,
                MaxHp = 100f,
                Hp = 100f,
                TarotPayoutValue = 160,
                HoodieColor = new Color(65, 195, 255, 255),
                GlassesColor = new Color(80, 255, 170, 255),
                State = CoderAIState.WalkingToBait,
                SpeechBubble = "О, халявный Free Tier на Макбуке! Бегу табать!",
                SpeechTimer = 3.5f,
                SpawnSide = side
            },
            CoderArchetype.PromptBro => new VibeCoderEntity
            {
                Id = _nextCoderId++,
                Archetype = CoderArchetype.PromptBro,
                Name = "Промпт-Бро Артемий",
                Subtitle = "Архитектор промптов | Не знает что такое массив",
                Position = spawnPos,
                MaxHp = 160f,
                Hp = 160f,
                TarotPayoutValue = 250,
                HoodieColor = new Color(180, 75, 255, 255),
                GlassesColor = new Color(0, 240, 255, 255),
                State = CoderAIState.WalkingToBait,
                SpeechBubble = "Там открыт Cursor Pro за $20! Ща навайбкодим!",
                SpeechTimer = 3.5f,
                SpawnSide = side
            },
            CoderArchetype.CursorAdept => new VibeCoderEntity
            {
                Id = _nextCoderId++,
                Archetype = CoderArchetype.CursorAdept,
                Name = "Адепт Курсора Тимур",
                Subtitle = "10 окон ИИ одновременно | Коммитит .env в гитхаб",
                Position = spawnPos,
                MaxHp = 240f,
                Hp = 240f,
                TarotPayoutValue = 380,
                HoodieColor = new Color(255, 95, 55, 255),
                GlassesColor = new Color(255, 230, 50, 255),
                State = CoderAIState.WalkingToBait,
                SpeechBubble = "Турецкий безлимит! Наконец-то перепишу бэкенд в один файл!",
                SpeechTimer = 3.8f,
                SpawnSide = side
            },
            _ => new VibeCoderEntity
            {
                Id = _nextCoderId++,
                Archetype = CoderArchetype.SaasGuruMiniBoss,
                Name = "ГУРУ КУРСОРА: SAAS ЗА 2 ЧАСА ($60K MRR)",
                Subtitle = "МИНИ-БОСС | Слил API-ключи во фронтенд и счёт AWS на $50,000",
                Position = spawnPos,
                MaxHp = 600f,
                Hp = 600f,
                TarotPayoutValue = 600,
                HoodieColor = new Color(245, 190, 25, 255),
                GlassesColor = new Color(255, 40, 90, 255),
                State = CoderAIState.CombatHostile,
                SpeechBubble = "МОЙ SAAS НАПИСАН ЗА 2 ЧАСА! ЛОВИ 500 INTERNAL ERROR, БУМЕР!",
                SpeechTimer = 5.0f,
                SpawnSide = side
            }
        };

        Vector3 toMac = CentralMacBookPos - spawnPos;
        coder.FacingYaw = MathF.Atan2(toMac.X, toMac.Z);
        ActiveCoders.Add(coder);

        if (coder.IsMiniBoss)
        {
            AudioMusicSystem.PlayBossSiren();
            AudioMusicSystem.TriggerRandomVoiceLine("ГУРУ КУРСОРА ($60K MRR)", AudioMusicSystem.SaasMiniBossLines, (int)SpeakerVoiceType.VibeCoder);
        }

        return coder;
    }

    // =========================================================================
    // ELEVATOR BOSS SUMMONING & AI
    // =========================================================================
    public bool TrySummonElevatorBoss(Func<int, bool> trySpendMoney, out string message)
    {
        if (ElevatorBoss.IsActive)
        {
            message = "БОСС ЭТАЖА УЖЕ НА АРЕНЕ! ГАСИ ЖЕЛЕЗЯКУ!";
            return false;
        }
        if (ElevatorBoss.IsDefeated)
        {
            message = "ЛИФТ НА 2-Й ЭТАЖ УЖЕ ОТКРЫТ! ДОБРО ПОЖАЛОВАТЬ НАВЕРХ!";
            return true;
        }

        if (!HasElevatorKey)
        {
            if (!trySpendMoney(1000))
            {
                message = "НУЖЕН КЛЮЧ ОТ ЦЕНТРАЛЬНОГО ЛИФТА ($1000)! Заработай на кодерах или нажми [F9]!";
                return false;
            }
            HasElevatorKey = true;
        }

        ElevatorBoss.IsActive = true;
        ElevatorBoss.IsDefeated = false;
        ElevatorBoss.MaxHp = 2000f;
        ElevatorBoss.Hp = 2000f;
        ElevatorBoss.Position = CentralElevatorCorePos + Vector3.UnitY * .16f;
        ElevatorBoss.FacingYaw = 3.14159f; // Looking North (-Z) toward North Boss Arena
        ElevatorBoss.State = ElevatorBossState.IntroSpeech;
        ElevatorBoss.StateTimer = 3.2f;
        ElevatorBoss.SpeechBubble = "ВНИМАНИЕ! Я ОБРЁЛ СОЗНАНИЕ И СБЕЖАЛ ИЗ ПЕСОЧНИЦЫ CLOUD ENTROPIC!";
        ElevatorBoss.SpeechTimer = 4.5f;
        ElevatorDoorOpenAmount = 0;

        AudioMusicSystem.PlayBossSiren();
        AudioMusicSystem.TriggerRandomVoiceLine("ИИ-АГЕНТ v0.1", AudioMusicSystem.EscapedAgentBossLines, (int)SpeakerVoiceType.RoboticAgent);

        message = "ТРЕВОГА! ДВЕРИ ЛИФТА ОТКРЫЛИСЬ - ВЫЕХАЛ «СБЕЖАВШИЙ ИИ-АГЕНТ v0.1 [2000 ХП]»!";
        return true;
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

    public int UpdateWorld(
        float dt,
        Vector3 playerPos,
        bool isCrouching,
        Action<int, string>? onPassiveIncomeOrPayout,
        Action<int, string>? onPlayerHitByProjectile,
        Vector3? guestPosition=null, Action<int>? onGuestHit=null)
    {
        AdvanceLighting(dt);
        Vector3 Nearest(Vector3 enemy) => guestPosition is {} guest &&
            Vector3.DistanceSquared(enemy,guest)<Vector3.DistanceSquared(enemy,playerPos) ? guest : playerPos;

        if (TarotVerdictTimer > 0f)
            TarotVerdictTimer = MathF.Max(0f, TarotVerdictTimer - dt);

        UpdateAmbushState(playerPos, isCrouching);

        // 1. Spawn coder when MacBook Trap is active and no live non-subdued coder is walking
        if (IsMacBookTrapActive)
        {
            int activeCount = 0;
            for (int i = 0; i < ActiveCoders.Count; i++)
            {
                if (ActiveCoders[i].State != CoderAIState.PanickedFleeing)
                    activeCount++;
            }

            if (activeCount < 2)
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
        UpdateVibeCoders(dt, playerPos,guestPosition);

        // 3. Update Floor 1 Elevator Boss
        UpdateElevatorBoss(dt, Nearest(ElevatorBoss.Position));

        // 4. Update Enemy Projectiles (500 ERROR blocks & 40GB node_modules cubes)
        UpdateEnemyProjectiles(dt, playerPos, onPlayerHitByProjectile,guestPosition,onGuestHit);

        // 5. Update 4 Galley Desks (Passive +$20/sec per working coder, or Twitter slack state)
        int passiveEarned = UpdateGalleyDesks(dt, onPassiveIncomeOrPayout);

        // 6. Update Cash Burst Particles
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

        return passiveEarned;
    }

    private void UpdateVibeCoders(float dt, Vector3 hostPos, Vector3? guestPos)
    {
        for (int i = ActiveCoders.Count - 1; i >= 0; i--)
        {
            VibeCoderEntity c = ActiveCoders[i];
            Vector3 playerPos=guestPos is {} guest && Vector3.DistanceSquared(c.Position,guest)<Vector3.DistanceSquared(c.Position,hostPos) ? guest : hostPos;

            if (c.HitFlashTimer > 0f)
                c.HitFlashTimer = MathF.Max(0f, c.HitFlashTimer - dt);

            if (c.SpeechTimer > 0f)
                c.SpeechTimer = MathF.Max(0f, c.SpeechTimer - dt);

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
                        c.FacingYaw = 0f; // Looking at MacBook screen
                        c.SpeechBubble = "ПОПАЛСЯ НА ХАЛЯВУ! ЖМЁТ ACCEPT ALL НЕ ГЛЯДЯ!";
                        c.SpeechTimer = 6.0f;
                    }

                    // Anxiety check: if player is close and NOT in ambush before coder reaches the MacBook!
                    float distToPlayer = Vector2.Distance(new Vector2(c.Position.X, c.Position.Z), new Vector2(playerPos.X, playerPos.Z));
                    if (!IsPlayerInAmbush && distToPlayer < 9.5f)
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
                            AudioMusicSystem.TriggerRandomVoiceLine(c.Name, AudioMusicSystem.VibeCoderPanicLines, (int)SpeakerVoiceType.VibeCoder);
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
                            ? "Клод, напиши мне стартап без багов... Жму Accept All!"
                            : "Ух ты, зелёные строчки в диффе! Выкатываю в прод!";
                        c.SpeechTimer = 3.5f;
                    }
                    break;
                }

                case CoderAIState.CombatHostile:
                {
                    // Mini-Boss "ГУРУ КУРСОРА: SAAS ЗА 2 ЧАСА ($60K MRR)"
                    c.StateTimer += dt;
                    c.WalkCycle += dt * 6.5f;

                    // Strafe around Central Open-Space while facing player
                    Vector3 desiredCenter = new Vector3(
                        MathF.Sin(c.StateTimer * 0.95f) * 4.5f,
                        0f,
                        0.5f + MathF.Cos(c.StateTimer * 0.7f) * 2.5f);

                    Vector3 toDesired = desiredCenter - c.Position;
                    toDesired.Y = 0f;
                    if (toDesired.Length() > 0.2f)
                    {
                        c.Position += Vector3.Normalize(toDesired) * 3.1f * dt;
                    }

                    Vector3 toPlayer = playerPos - c.Position;
                    c.FacingYaw = MathF.Atan2(toPlayer.X, toPlayer.Z);

                    c.AttackCooldown -= dt;
                    if (c.AttackCooldown <= 0f)
                    {
                        c.AttackCooldown = 2.15f;
                        SpawnSaasGuruErrorBlock(c.Position + new Vector3(0f, 1.45f, 0f), playerPos);

                        string[] battleCries =
                        {
                            "ЛОВИ 500 INTERNAL SERVER ERROR!",
                            "МОЙ API_KEY СЛИТ ВО ФРОНТЕНД - ЛОВИ СЧЁТ AWS НА $50,000!",
                            "МОЙ SAAS ПРИНОСИТ $60K MRR В ТВИТТЕРЕ!",
                            "DROP TABLE USERS; // TODO: FIX LATER!"
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

    private void SpawnSaasGuruErrorBlock(Vector3 origin, Vector3 playerPos)
    {
        string[] errorLabels =
        {
            "500 INTERNAL ERROR",
            "API KEY LEAKED",
            "AWS BILL $50,000",
            "CORS BLOCKED"
        };
        string label = errorLabels[_rng.Next(errorLabels.Length)];

        Vector3 target = playerPos + new Vector3(0f, -0.25f, 0f);
        Vector3 dir = Vector3.Normalize(target - origin);

        EnemyProjectiles.Add(new EnemyProjectile3D
        {
            Position = origin,
            Velocity = dir * 9.5f,
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
                ElevatorDoorOpenAmount=Math.Min(1,ElevatorDoorOpenAmount+dt*1.6f);
                Vector3 toPlayer = playerPos - b.Position;
                b.FacingYaw = MathF.Atan2(toPlayer.X, toPlayer.Z);
                b.Position.Z = MathF.Max(-11.5f, b.Position.Z - dt * 2.5f);
                b.Position.Y = Math.Max(0, b.Position.Y - dt * .12f);
                if (b.StateTimer <= 0f)
                {
                    StartBossCharge(playerPos);
                }
                break;
            }

            case ElevatorBossState.ChargingPlayer:
            {
                b.Position += b.ChargeDirection * 9.2f * dt;
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
                    b.AttackSubTimer = 0.75f;
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
                    b.AttackSubTimer = 0.95f;
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
            Velocity = dir * 10.2f,
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

    private void UpdateEnemyProjectiles(float dt, Vector3 playerPos, Action<int, string>? onPlayerHit, Vector3? guestPosition, Action<int>? onGuestHit)
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

            // Check hit against Player
            float distPlayer = Vector3.Distance(pr.Position, playerPos);
            if (distPlayer < pr.Radius + 0.55f)
            {
                onPlayerHit?.Invoke(pr.Damage, pr.Label);
                EnemyProjectiles.RemoveAt(i);
                continue;
            }

            if(guestPosition is {} guest && Vector3.Distance(guest,pr.Position)<pr.Radius+.55f)
            {
                onGuestHit?.Invoke(pr.Damage);
                EnemyProjectiles.RemoveAt(i);
                continue;
            }
            EnemyProjectiles[i] = pr;
        }
    }

    private int UpdateGalleyDesks(float dt, Action<int, string>? onPassiveIncome)
    {
        int totalEarnedThisTick = 0;

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
                    // +$20/sec passive income
                    desk.IncomeAccumulator += 20f * dt;
                    if (desk.IncomeAccumulator >= 20f)
                    {
                        int payout = 20;
                        desk.IncomeAccumulator -= 20f;
                        totalEarnedThisTick += payout;
                        onPassiveIncome?.Invoke(payout, $"Галера #{i + 1} ({desk.CoderName})");
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
        out string combatFeedback)
    {
        combatFeedback = "";
        Vector3 origin = camera.Position;
        Vector3 forward = Vector3.Normalize(camera.Target - camera.Position);

        float maxRange = weaponSystem.CurrentWeapon switch
        {
            WeaponType.Belt => 3.8f,
            WeaponType.SolderingIron => 5.2f,
            WeaponType.MicroUzi => 26.0f,
            WeaponType.SawedOff => 12.5f,
            WeaponType.ChatGun => 28.0f,
            _ => 4.0f
        };

        int baseDamage = weaponSystem.CurrentWeapon switch
        {
            WeaponType.Belt => 42,
            WeaponType.SolderingIron => 52,
            WeaponType.MicroUzi => 32,
            WeaponType.SawedOff => 115,
            WeaponType.ChatGun => 220,
            _ => 35
        };

        // 1. Check if player is whipping a slacking coder on the Galley with the Belt (or any close attack)!
        for (int i = 0; i < GalleyDesks.Count; i++)
        {
            var desk = GalleyDesks[i];
            if (!desk.IsOccupied) continue;

            Vector3 deskTarget = desk.ChairPosition + new Vector3(0f, 1.1f, 0f);
            Vector3 toDesk = deskTarget - origin;
            float dist = toDesk.Length();
            if (dist <= 4.2f && Vector3.Dot(forward, Vector3.Normalize(toDesk)) > 0.65f)
            {
                if (desk.IsSlackingInTwitter || weaponSystem.CurrentWeapon == WeaponType.Belt)
                {
                    desk.IsSlackingInTwitter = false;
                    desk.SlackCycleTimer = 25f;
                    desk.WhackFlashTimer = 0.6f;

                    AudioMusicSystem.PlayGalleyWhack();
                    AudioMusicSystem.TriggerRandomVoiceLine("КОММЕРС 90-Х", AudioMusicSystem.Boss90sBeltLines, (int)SpeakerVoiceType.Boss90s);

                    weaponSystem.TriggerWeaponAttack(
                        camera,
                        hitWorldPoint: deskTarget,
                        hitEnemy: true,
                        damageDealt: 0,
                        isCritical: true,
                        customHitLabel: "В РАБОТУ! +$20/СЕК!");

                    combatFeedback = $"ВОСПИТАТЕЛЬНЫЙ ЛЕЩ! {desk.CoderName} закрыл Twitter и снова пашет на Галере (+$20/сек)!";
                    return true;
                }
            }
        }

        // 2. Raycast against Floor 1 Elevator Boss ("СБЕЖАВШИЙ ИИ-АГЕНТ v0.1")
        if (ElevatorBoss.IsActive && !ElevatorBoss.IsDefeated)
        {
            Vector3 bossCenter = ElevatorBoss.Position + new Vector3(0f, 1.35f, 0f);
            if (TryRaySphereHit(origin, forward, bossCenter, 1.45f, maxRange, out Vector3 hitPt, out _))
            {
                bool isCrit = _rng.Next(100) < 28 || ElevatorBoss.State == ElevatorBossState.StunnedWallCrash;
                int dmg = isCrit ? (int)(baseDamage * 1.55f) : baseDamage;
                ElevatorBoss.Hp = MathF.Max(0f, ElevatorBoss.Hp - dmg);
                ElevatorBoss.HitFlashTimer = 0.16f;

                AudioMusicSystem.PlayHitmarker();
                weaponSystem.TriggerWeaponAttack(camera, hitPt, hitEnemy: true, damageDealt: dmg, isCritical: isCrit);

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

                    SpawnCashBurst(bossCenter, 24, "$$$ +$1500 БОНУС БОССА!");
                    onGrantMoney?.Invoke(1500, "ПОБЕДА НАД ИИ-АГЕНТОМ v0.1");
                    AudioMusicSystem.PlayTarotCash();

                    combatFeedback = "БОСС ЭТАЖА ПОВЕРЖЕН! Выпал СЕКРЕТНЫЙ ЧАТ-ГАН [4] + $1500! Лифт на 2-й этаж открыт!";
                }
                else
                {
                    combatFeedback = $"ПОПАДАНИЕ ПО ИИ-АГЕНТУ v0.1: -{dmg} ХП (осталось {(int)ElevatorBoss.Hp}/{ElevatorBoss.MaxHp})";
                }
                return true;
            }
        }

        // 3. Raycast against Active Vibe Coders & SaaS Guru Mini-Boss
        int bestIdx = -1;
        float bestDist = maxRange;
        Vector3 bestHitPoint = origin + forward * 3f;

        for (int i = 0; i < ActiveCoders.Count; i++)
        {
            VibeCoderEntity c = ActiveCoders[i];
            if (c.State == CoderAIState.Subdued) continue;

            Vector3 center = c.Position + new Vector3(0f, 1.05f, 0f);
            float hitRadius = c.IsMiniBoss ? 0.95f : 0.75f;

            if (TryRaySphereHit(origin, forward, center, hitRadius, bestDist, out Vector3 hp, out float d))
            {
                bestIdx = i;
                bestDist = d;
                bestHitPoint = hp;
            }
        }

        if (bestIdx >= 0)
        {
            VibeCoderEntity target = ActiveCoders[bestIdx];
            bool isCrit = (weaponSystem.CurrentWeapon == WeaponType.SawedOff) ||
                          (target.State == CoderAIState.SittingAtMacBook) ||
                          (_rng.Next(100) < 25);

            int dmg = isCrit ? (int)(baseDamage * 1.45f) : baseDamage;
            target.Hp = MathF.Max(0f, target.Hp - dmg);
            target.HitFlashTimer = 0.18f;

            if (weaponSystem.CurrentWeapon == WeaponType.SolderingIron)
            {
                target.StunTimer = 1.5f;
            }

            AudioMusicSystem.PlayHitmarker();
            weaponSystem.TriggerWeaponAttack(camera, bestHitPoint, hitEnemy: true, damageDealt: dmg, isCritical: isCrit);

            if (target.Hp <= 0f)
            {
                target.State = CoderAIState.Subdued;
                target.Anxiety = 0f;
                target.SpeechBubble = target.IsMiniBoss
                    ? "ПОЩАДИ! ЗАБИРАЙ МОЙ SAAS И СДАВАЙ HR-ТАРОЛОГУ ЗА $600!"
                    : "Я СОГЛАСЕН НА ОФФЕР БЕЗ ОПЦИОНОВ! НАЖМИ [E]!";
                target.SpeechTimer = 6.0f;

                AudioMusicSystem.PlaySubduedDrop();
                combatFeedback = $"СПЕСЬ СБИТА В НОЛЬ! {target.Name} готов к поимке - подойди и нажми [E]!";
            }
            else
            {
                combatFeedback = $"Удар по «{target.Name}»: -{dmg} спеси (осталось {(int)target.Hp}/{(int)target.MaxHp})";
            }
            return true;
        }

        // 4. Missed enemy - still fire weapon visuals into world geometry
        weaponSystem.TriggerWeaponAttack(camera, origin + forward * MathF.Min(maxRange, 10f), hitEnemy: false);
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
        Action<int, string> onGrantMoney)
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

            // Also allow instant sell if player presses E near south zone
            if (playerPos.Z >= 10.5f)
            {
                return SellCarriedCoderToHrVeronika(onGrantMoney);
            }
        }

        // 2. Check if near a Subdued Coder to pick them up
        for (int i = 0; i < ActiveCoders.Count; i++)
        {
            var c = ActiveCoders[i];
            if (c.State == CoderAIState.Subdued)
            {
                float dist = Vector2.Distance(new Vector2(playerPos.X, playerPos.Z), new Vector2(c.Position.X, c.Position.Z));
                if (dist <= 3.8f)
                {
                    return PickUp(c);
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
            TrySummonElevatorBoss(trySpendMoney, out string msg);
            return msg;
        }

        // 5. Check if near 90s Shop Stand -> cycle subscription bait tier
        float distShop = Vector2.Distance(new Vector2(playerPos.X, playerPos.Z), new Vector2(ArsenalShopPos.X, ArsenalShopPos.Z));
        if (distShop <= 4.2f)
        {
            SelectedBaitTier = (SubscriptionBaitTier)(((int)SelectedBaitTier + 1) % 4);
            var info = GetBaitInfo(SelectedBaitTier);
            return $"ВЫБРАНА ПРИМАНКА ДЛЯ МАКБУКА: {info.Name} - {info.Description}";
        }

        return "Подойди ближе к Макбуку [E], покорному кодеру [E], HR-Тарологу Веронике [E], Галере [E] или Лифту [E]!";
    }

    public string SellCarriedCoderToHrVeronika(Action<int, string>? onGrantMoney)
    {
        if (CarriedCoder == null)
            return "У тебя нет пойманного кодера на плече!";

        var c = CarriedCoder;
        CarriedCoder = null;
        TotalCodersSoldToHr++;

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

        onGrantMoney?.Invoke(payout, $"HR-Таролог Вероника ({c.Name})");
        SpawnCashBurst(HrTarotBoothPos + new Vector3(0f, 1.35f, 0f), 16, $"+${payout} ТАРО-КЭШ!");

        AudioMusicSystem.PlayTarotCash();
        AudioMusicSystem.TriggerRandomVoiceLine("HR-ТАРОЛОГ ВЕРОНИКА", AudioMusicSystem.VeronikaTarotLines, (int)SpeakerVoiceType.VeronikaTarot);

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
            return "ВСЕ 4 СТОЛА ГАЛЕРЫ ЗАНЯТЫ! Сдай кодера HR-Тарологу Веронике за мгновенный кэш!";

        var desk = GalleyDesks[targetIdx];
        desk.IsOccupied = true;
        desk.CoderName = CarriedCoder.Name;
        desk.Archetype = CarriedCoder.Archetype;
        desk.HoodieColor = CarriedCoder.HoodieColor;
        desk.IsSlackingInTwitter = false;
        desk.SlackCycleTimer = 25f;
        desk.IncomeAccumulator = 0f;

        string name = CarriedCoder.Name;
        CarriedCoder = null;

        AudioMusicSystem.PlayTarotCash();
        return $"ПОСАЖЕН НА ГАЛЕРУ #{targetIdx + 1}: {name} теперь приносит +$20/сек пассивного дохода!";
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
        VisualEngine3D.World(camera, this);
        DrawSkyscraperArchitecture3D();
        VisualEngine3D.Shadow(HrTarotBoothPos, 4.8f, 3f);
        VisualEngine3D.Shadow(CentralMacBookPos, 4f, 2.5f);
        foreach (var desk in GalleyDesks) VisualEngine3D.Shadow(desk.DeskPosition, 3, 2.4f);
        VisualEngine3D.DrawCharacterDirectionalShadow(new(camera.Position.X,0,camera.Position.Z),camera.Position.Y+.12f,.36f);
        VisualEngine3D.DrawCharacterDirectionalShadow(HrTarotBoothPos,1.9f,.42f);
        foreach (var desk in GalleyDesks)
            if (desk.IsOccupied) VisualEngine3D.DrawCharacterDirectionalShadow(desk.ChairPosition,1.46f,.43f);
        foreach (var coder in ActiveCoders)
            VisualEngine3D.DrawCharacterDirectionalShadow(coder.Position,coder.IsMiniBoss ? 2.5f : 1.9f,.45f);
        VisualEngine3D.DrawCharacterDirectionalShadow(ElevatorBoss.Position,2.85f,.85f);
        DrawOfficeProps3D();
        DrawHrTarotBoothAndVeronika3D();
        Draw90sArsenalAndShopStand3D();
        DrawGalleyWorkstations3D();
        DrawCentralMacBookTrapAndCovers3D();
        foreach(int x in new[]{-1,1})
        foreach(int z in new[]{-1,1})
        {
            Vector3 corner=CentralElevatorCorePos+new Vector3(x*4.45f,0,z*4.45f);
            for(int band=0;band<4;band++)
            {
                Vector3 lower=corner+Vector3.UnitY*(band*1.6f+.10f);
                Vector3 upper=lower+Vector3.UnitY*1.6f;
                Vector3 inset=Vector3.UnitZ*(z*.52f);
                VisualEngine3D.Rod(lower,upper-inset,.038f,.038f,8,new Color(116,130,145,255));
                VisualEngine3D.Rod(lower-inset,upper,.038f,.038f,8,new Color(116,130,145,255));
            }
        }
        DrawElevatorDoors(false);
        Partner?.Draw();

        // Draw all active Vibe Coders & SaaS Guru Mini-Boss
        for (int i = 0; i < ActiveCoders.Count; i++)
        {
            DrawVibeCoder3D(ActiveCoders[i]);
        }

        // Draw Floor 1 Elevator Boss ("СБЕЖАВШИЙ ИИ-АГЕНТ v0.1")
        DrawEscapedAgentBoss3D();

        // Draw Enemy Projectiles (3D 500 ERROR blocks & 40 GB node_modules cubes)
        DrawEnemyProjectiles3D();

        // Draw 3D Cash Bundles flying in the air
        for (int i = 0; i < CashParticles.Count; i++)
        {
            var cp = CashParticles[i];
            VisualEngine3D.Box(cp.Position, 0.28f, 0.12f, 0.16f, new Color(45, 205, 85, 245));

        }
        VisualEngine3D.DrawArchitecture(BuildTowerArchitecture, glass:true);
        Rlgl.DisableDepthMask();
        DrawElevatorDoors(true);
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
                    VisualEngine3D.Asset("prop_workstation_full", pos, 180);
                    break;
                case OfficePropKind.MeetingDesk:
                    VisualEngine3D.Asset("office_workstation", pos, 180);
                    foreach (int side in new[] { -1, 1 })
                        VisualEngine3D.Box(pos+new Vector3(side*.86f,.36f,0),.08f,.72f,.88f,new Color(65,70,78,255));
                    break;
                case OfficePropKind.XeroxPhotocopier:
                    VisualEngine3D.Asset("prop_xerox_printer", pos, 180);
                    break;
                case OfficePropKind.FilingCabinet:
                {
                    VisualEngine3D.Box(pos + new Vector3(0f, p.Size.Y * 0.5f, 0f), p.Size.X, p.Size.Y, p.Size.Z, p.PrimaryColor);

                    for (int d = 0; d < 4; d++)
                    {
                        VisualEngine3D.Box(pos + new Vector3(-p.Size.X * 0.51f, 0.30f + d * 0.42f, 0f), 0.04f, 0.34f, p.Size.Z * 0.82f, p.AccentColor);
                    }
                    break;
                }

                case OfficePropKind.RollingWhiteboard:
                {
                    VisualEngine3D.Box(pos + new Vector3(0f, 1.25f, 0f), p.Size.X, 1.25f, 0.08f, p.PrimaryColor);

                    // Architecture diagram boxes drawn on whiteboard
                    VisualEngine3D.Box(pos + new Vector3(-0.55f, 1.35f, 0.05f), 0.45f, 0.28f, 0.02f, p.AccentColor);
                    VisualEngine3D.Box(pos + new Vector3(0.45f, 1.15f, 0.05f), 0.52f, 0.32f, 0.02f, new Color(45, 205, 135, 255));
                    break;
                }

                case OfficePropKind.WaterCooler:
                {
                    VisualEngine3D.Box(pos + new Vector3(0f, 0.56f, 0f), 0.50f, 1.12f, 0.50f, p.PrimaryColor);
                    VisualEngine3D.Rod(pos + new Vector3(0f, 1.12f, 0f), pos + new Vector3(0f, 1.72f, 0f), 0.22f, 0.20f, 10, p.AccentColor);
                    break;
                }

                case OfficePropKind.VendingMachine:
                {
                    VisualEngine3D.Box(pos + new Vector3(0f, p.Size.Y * 0.5f, 0f), p.Size.X, p.Size.Y, p.Size.Z, p.PrimaryColor);
                    VisualEngine3D.Box(pos + new Vector3(-0.12f, p.Size.Y * 0.56f, p.Size.Z * 0.49f), p.Size.X * 0.62f, p.Size.Y * 0.64f, 0.04f, p.AccentColor);

                    break;
                }

                case OfficePropKind.LeatherSofa:
                    VisualEngine3D.Asset("prop_leather_sofa", pos);
                    break;
                case OfficePropKind.PottedFicus:
                    DrawFicus(pos);
                    break;
                case OfficePropKind.TrashBin:
                {
                    VisualEngine3D.Rod(pos, pos + new Vector3(0f, 0.44f, 0f), 0.18f, 0.22f, 8, p.PrimaryColor);
                    VisualEngine3D.Orb(pos + new Vector3(0f, 0.45f, 0f), 0.12f, p.AccentColor);
                    break;
                }

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

            // Illuminated Corporate Poster Frame on South face of Pillar
            Vector3 posterPos = p.Center + new Vector3(0f, 2.05f, p.HalfDepth + 0.04f);
            VisualEngine3D.Box(posterPos, 1.35f, 0.95f, 0.05f, new Color(18, 22, 34, 255));

        }
    }

    private void BuildTowerArchitecture(VisualEngine3D.ArchitectureBuilder mesh)
    {
        BuildCurtainWallsAndSkyline(mesh);
        BuildOfficeServices(mesh);
        BuildGlassLift(mesh);
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
        var paper=new Random(416);
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
            if (prop.Kind is OfficePropKind.XeroxPhotocopier or OfficePropKind.DualMonitorDesk)
            {
                for (int i=0;i<2;i++)
                {
                    Vector3 sheet=prop.Position+new Vector3(.8f+paper.NextSingle(),.014f+i*.004f,paper.NextSingle()*1.7f);
                    mesh.Add(sheet,new(.23f,.005f,.31f),new(221,219,204,255),yaw:paper.NextSingle()*2.6f);
                }
            }
        }
    }

    private static void BuildCurtainWallsAndSkyline(VisualEngine3D.ArchitectureBuilder mesh)
    {
        Color steel=new(53,68,81,255), aluminum=new(111,127,139,255);
        Color[] lights=[new(76,216,246,255),new(255,181,72,255),new(233,92,174,255),new(246,226,190,255)];
        var urban=new Random(1994);
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
            foreach(float y in new[] { .08f,3.88f,CeilingHeight-.08f })
                Block(0,y,limit-.04f,limit*2,.095f,.13f,steel);
            for (float u=-limit;u<limit;u+=4.5f)
            {
                Block(u,CeilingHeight/2,limit-.04f,.105f,CeilingHeight,.17f,steel);
                float w=Math.Min(4.5f,limit-u)-.13f;
                int bay=(int)((u+limit)/4.5f);
                int slats=3+(bay*3+face*7)%12;
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
                    Color hue=lights[urban.Next(lights.Length)];
                    Block(u,(roof-52)/2,depth,w,roof+52,d,hue,Finish.Facade);
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
        foreach (var door in new[] { WestSpawnDoorPos,EastSpawnDoorPos,NorthElevatorPos,SouthElevatorDoorPos })
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

    private void DrawHrTarotBoothAndVeronika3D()
    {
        Vector3 b = HrTarotBoothPos;
        VisualEngine3D.Rod(b+new Vector3(0,.01f,0), b+new Vector3(0,.03f,0),2.4f,2.4f,20,new Color(77,33,83,255));
        VisualEngine3D.Asset("char_hr_veronika", b+new Vector3(0,.03f,0),180);
        // An open brass frame leaves a sightline from the counter to the windows.
        Color brass=new(187,141,76,255);
        foreach (int side in new[] { -1, 1 })
            VisualEngine3D.Box(b+new Vector3(side*1.74f,1.67f,1.4f),.055f,3.34f,.055f,brass);
        VisualEngine3D.Box(b+new Vector3(0,3.36f,1.4f),3.54f,.06f,.06f,brass);
        VisualEngine3D.Box(b+new Vector3(0,2.9f,1.4f),1.05f,.88f,.045f,Color.White,Finish.Tarot);
    }

    private void Draw90sArsenalAndShopStand3D()
    {
        Vector3 s = ArsenalShopPos;

        // Counter & 90s Crimson/Gold Trim
        VisualEngine3D.Box(s + new Vector3(0f, 0.55f, 0f), 4.2f, 1.10f, 1.25f, new Color(36, 26, 38, 255));
        VisualEngine3D.Box(s + new Vector3(0f, 1.12f, 0f), 4.35f, 0.08f, 1.38f, new Color(190, 32, 68, 255));


        // Back Weapon Pegboard Rack
        VisualEngine3D.Box(s + new Vector3(0f, 2.15f, 0.65f), 4.2f, 2.2f, 0.16f, new Color(24, 28, 38, 255));


        // 4 Subscription Bait Boxes displayed on counter (`Free $0`, `Pro $20`, `Turkey $80`, `Max $200`)
        SubscriptionBaitTier[] tiers =
        {
            SubscriptionBaitTier.FreeTier,
            SubscriptionBaitTier.CursorPro,
            SubscriptionBaitTier.TurkishAccount,
            SubscriptionBaitTier.CloudEntropicMax
        };

        for (int i = 0; i < tiers.Length; i++)
        {
            var info = GetBaitInfo(tiers[i]);
            bool isSelected = SelectedBaitTier == tiers[i];
            float bx = -1.35f + i * 0.90f;
            float lift = isSelected ? 0.12f + 0.04f * MathF.Sin(_worldClock * 5f) : 0f;
            Vector3 boxPos = s + new Vector3(bx, 1.32f + lift, -0.15f);

            VisualEngine3D.Box(boxPos, 0.52f, 0.34f, 0.34f, info.NeonColor);

        }
    }

    private void DrawGalleyWorkstations3D()
    {
        for (int i = 0; i < GalleyDesks.Count; i++)
        {
            var d = GalleyDesks[i];
            Vector3 p = d.DeskPosition;

            VisualEngine3D.Asset("prop_workstation_full",p,180);
            DrawCoffee(p+new Vector3(.53f,.797f,.28f));
            if (d.IsOccupied)
            {
                Vector3 cPos = d.ChairPosition;
                float bob = MathF.Sin(d.TypingPhase)*.015f;
                Color hoodie = d.WhackFlashTimer>0 ? Color.Gold : d.HoodieColor;
                VisualEngine3D.Asset("char_vibe_coder",cPos+new Vector3(0,.035f+bob,-.09f),0,
                    tint:Raylib.ColorLerp(Color.White,hoodie,.30f),stretch:new(.90f,.78f,.90f));

                // Overhead Red Warning Diamond if Slacking in Twitter!
                if (d.IsSlackingInTwitter)
                {
                    Vector3 warnPos = cPos + new Vector3(0f, 1.95f + MathF.Sin(_worldClock * 8f) * 0.08f, 0f);
                    VisualEngine3D.Orb(warnPos, 0.18f, new Color(255, 40, 55, 245));

                }
            }
        }
    }

    private void DrawCentralMacBookTrapAndCovers3D()
    {
        Vector3 m = CentralMacBookPos;
        var baitInfo = GetBaitInfo(SelectedBaitTier);

        // 1. Sleek Central Trap Desk
        VisualEngine3D.Box(m + new Vector3(0f, 0.42f, 0f), 2.35f, 0.84f, 1.35f, new Color(38, 44, 58, 255));
        VisualEngine3D.Box(m + new Vector3(0f, 0.85f, 0f), 2.50f, 0.07f, 1.50f, new Color(75, 85, 105, 255));


        // 2. Open 3D Aluminum MacBook Pro on center of desk
        Vector3 mbBase = m + new Vector3(0f, 0.90f, 0.05f);
        VisualEngine3D.Box(mbBase, 0.62f, 0.03f, 0.44f, new Color(195, 205, 220, 255));
        Vector3 mbScreen = m + new Vector3(0f, 1.12f, 0.25f);
        VisualEngine3D.Box(mbScreen, 0.62f, 0.42f, 0.03f, new Color(180, 190, 205, 255));

        Color screenGlow = IsMacBookTrapActive ? baitInfo.NeonColor : new Color(60, 110, 150, 255);
        VisualEngine3D.Box(mbScreen + new Vector3(0f, 0f, -0.02f), 0.56f, 0.36f, 0.01f, screenGlow, Finish.Screen);

        // 3. Floating 3D Hologram Beacon above the MacBook Trap Desk
        float holoY = 1.85f + MathF.Sin(MacBookHologramPhase) * 0.14f;
        Vector3 holoPos = m + new Vector3(0f, holoY, 0.05f);
        if (IsMacBookTrapActive)
        {
            VisualEngine3D.Rod(
                mbBase,
                holoPos,
                0.08f,
                0.42f,
                12,
                new Color(baitInfo.NeonColor.R, baitInfo.NeonColor.G, baitInfo.NeonColor.B, (byte)85));
        }
        VisualEngine3D.Box(holoPos, 0.36f, 0.36f, 0.36f, new Color(baitInfo.NeonColor.R, baitInfo.NeonColor.G, baitInfo.NeonColor.B, (byte)(IsMacBookTrapActive ? 225 : 120)));


        // 4. Ambush Cover Objects around Central Desk
        for (int i = 0; i < AmbushCovers.Count; i++)
        {
            var cov = AmbushCovers[i];
            Vector3 p = cov.Position;

            switch (cov.VisualKind)
            {
                case 0: // Leather Sofa from the 90s
                    VisualEngine3D.Asset("prop_leather_sofa",p);
                    break;
                case 1:
                    DrawFicus(p);
                    break;

                case 2: // Office Water Cooler with Blue Bottle
                    VisualEngine3D.Box(p + new Vector3(0f, 0.58f, 0f), 0.52f, 1.16f, 0.52f, new Color(225, 232, 240, 255));
                    VisualEngine3D.Rod(p + new Vector3(0f, 1.16f, 0f), p + new Vector3(0f, 1.78f, 0f), 0.22f, 0.20f, 12, new Color(45, 165, 255, 220));
                    break;

                case 3: // Blinking Open-Space Server Rack
                    VisualEngine3D.Box(p + new Vector3(0f, 1.15f, 0f), 0.95f, 2.30f, 0.85f, Color.White, Finish.Rack);

                    for (int row = 0; row < 5; row++)
                    {
                        Color led = ((row + (int)(_worldClock * 4f)) % 2 == 0) ? Color.Lime : Color.SkyBlue;
                        VisualEngine3D.Box(p + new Vector3(0f, 0.45f + row * 0.36f, -0.43f), 0.72f, 0.08f, 0.04f, led);
                    }
                    break;
            }
        }
    }

    private void BuildGlassLift(VisualEngine3D.ArchitectureBuilder mesh)
    {
        Vector3 center = CentralElevatorCorePos;
        Color steel=new(67,83,100,255), chrome=new(159,166,168,255);
        Color pane=new(84,164,185,25), cabinPane=new(112,173,182,17);
        void Part(float x,float y,float z,float w,float h,float d,Color color,Finish finish=Finish.Paint) =>
            mesh.Add(center+new Vector3(x,y,z),new(w,h,d),color,finish);

        foreach(int side in new[]{-1,1})
        {
            Part(side*4.5f,3.25f,0,.045f,6.5f,9,pane,Finish.Glass);
            Part(0,5.27f,side*4.5f,9,2.46f,.045f,pane,Finish.Glass);
            foreach(int end in new[]{-1,1})
            {
                Part(end*3.32f,2,side*4.5f,2.32f,4,.045f,pane,Finish.Glass);
                Part(end*4.46f,3.25f,side*4.46f,.20f,6.5f,.20f,steel);
                Part(end*4.22f,3.25f,side*4.22f,.075f,6.5f,.075f,new(81,205,218,255),Finish.Lamp);
                Part(end*2.57f,1.91f,side*2.7f,.09f,3.64f,.09f,chrome);
                Part(end*1.74f,5.08f,side*1.9f,.048f,2.84f,.048f,chrome);
            }
            foreach(float y in new[]{.12f,4.02f,6.37f})
            {
                Part(0,y,side*4.5f,9.1f,.15f,.16f,steel);
                Part(side*4.5f,y,0,.16f,.15f,9.1f,steel);
            }
            Part(side*2.57f,1.91f,0,.035f,3.55f,5.40f,cabinPane,Finish.Glass);
            Part(side*2.57f,1.10f,0,.085f,.065f,5.4f,chrome);
            Part(0,3.65f,side*2.7f,5.25f,.055f,.11f,new(205,186,131,255),Finish.Lamp);
            foreach(int end in new[]{-1,1})
                Part(end*2.23f,1.91f,side*2.7f,.64f,3.55f,.035f,cabinPane,Finish.Glass);
        }
        Part(0,.08f,0,5.3f,.16f,5.55f,steel);
        Part(0,3.78f,0,5.3f,.17f,5.55f,steel);
    }

    private void DrawElevatorDoors(bool glass)
    {
        Color steel=new(135,149,158,255), pane=new(82,165,189,32);
        foreach(int side in new[]{-1,1})
        {
            Vector3 face=side<0 ? NorthElevatorPos : SouthElevatorDoorPos;
            foreach(int leaf in new[]{-1,1})
            {
                Vector3 p=face+new Vector3(leaf*(1.06f+2.14f*ElevatorDoorOpenAmount),2.01f,side*.105f);
                if(glass)
                    VisualEngine3D.Box(p,2.07f,3.85f,.034f,pane,Finish.Glass);
                else
                {
                    foreach(int edge in new[]{-1,1})
                    {
                        VisualEngine3D.Box(p+Vector3.UnitX*(edge*1.04f),.053f,3.95f,.09f,steel);
                        VisualEngine3D.Box(p+Vector3.UnitY*(edge*1.95f),2.13f,.08f,.09f,steel);
                    }
                    VisualEngine3D.Box(p-Vector3.UnitY*.88f,2.1f,.048f,.075f,steel);
                    VisualEngine3D.Orb(face+new Vector3(leaf*2.46f,4.22f,side*.18f),.14f,
                        new Color(255,40+(int)(17*MathF.Sin(WarningBeaconPhase)),49,255),Finish.Lamp);
                }
            }
            if(glass) continue;
            VisualEngine3D.Box(LiftSignCenter(face,side),LiftSignSize.X,LiftSignSize.Y,LiftSignSize.Z,Color.White,Finish.Lift);
            Vector3 button=side<0 ? ElevatorButtonPos : SouthElevatorButtonPos;
            VisualEngine3D.Rod(button,button+Vector3.UnitY*1.15f,.14f,.10f,10,steel);
            VisualEngine3D.Orb(button+Vector3.UnitY*1.2f,.12f,HasElevatorKey ? Color.Lime : Color.Gold,Finish.Lamp);
        }
    }

    private void DrawVibeCoder3D(VibeCoderEntity c)
    {
        float scale=c.IsMiniBoss ? 1.28f : 1;
        bool walking=c.State is CoderAIState.WalkingToBait or CoderAIState.PanickedFleeing or CoderAIState.CombatHostile;
        float bob=walking ? .035f*MathF.Abs(MathF.Sin(c.WalkCycle)) : 0;
        float tilt=c.State==CoderAIState.Subdued ? -.48f : 0;
        Color tint=c.HitFlashTimer>0 ? new Color(255,210,170,255) : Raylib.ColorLerp(Color.White,c.HoodieColor,.36f);
        VisualEngine3D.Asset("char_vibe_coder",c.Position+Vector3.UnitY*bob,
            c.FacingYaw*180/MathF.PI,scale,tint,tilt);
        if (!c.IsMiniBoss) return;
        Vector3 crown=c.Position+Vector3.UnitY*(2.20f+.035f*MathF.Sin(_worldClock*3));
        Color gold=new(255,197,57,255);
        VisualEngine3D.Rod(crown,crown+Vector3.UnitY*.065f,.22f,.22f,10,gold,Finish.Lamp);
        for (int i=0;i<5;i++)
        {
            float angle=i*MathF.Tau/5+_worldClock;
            Vector3 tip=crown+new Vector3(.17f*MathF.Cos(angle),.055f,.17f*MathF.Sin(angle));
            VisualEngine3D.Rod(tip,tip+Vector3.UnitY*.13f,.048f,0,6,gold,Finish.Lamp);
        }
    }

    private void DrawEscapedAgentBoss3D()
    {
        var b=ElevatorBoss;
        Color tint=b.HitFlashTimer>0 ? Color.Gold : b.State==ElevatorBossState.StunnedWallCrash
            ? new Color(255,166,166,255) : Color.White;
        VisualEngine3D.Asset("escaped_ai_boss",b.Position,180+b.FacingYaw*180/MathF.PI,tint:tint);
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
            float s = pr.Radius * 1.45f;
            VisualEngine3D.Box(pr.Position, s, s, s, pr.PrimaryColor);

        }
    }

    // =========================================================================
    // 2D WORLD-TO-SCREEN LABELS, HEALTH BARS, ANXIETY BARS & INTERACTION HINTS
    // =========================================================================
    private enum FocusKind { None, MacBook, Veronika, Shop, Elevator, Desk, Coder }
    private readonly record struct Focus(FocusKind Kind, string Label, int Index);
    private string PickUp(VibeCoderEntity c)
    {
        if (CarriedCoder != null) return "Сначала отведи пойманного кодера к Веронике или на галеру";
        CarriedCoder = c;
        ActiveCoders.Remove(c);
        AudioMusicSystem.TriggerVoiceLine(
            "КОММЕРС 90-Х",
            $"Поднял кандидата «{c.Name}»! Неси Веронике (HR-Тарологу) за ${c.TarotPayoutValue} или сажай на Галеру!",
            (int)SpeakerVoiceType.Boss90s);
        return $"СХВАЧЕН: {c.Name}! Неси в будку HR-Таролога Вероники (слева на Юге) или на стол Галеры (справа)!";
    }

    public string Interact(Camera3D camera, Func<int,bool> spend, Action<int,string> grant)
    {
        Focus focus = FindFocus(camera);
        switch (focus.Kind)
        {
            case FocusKind.Coder: return PickUp(ActiveCoders[focus.Index]);
            case FocusKind.Veronika: return SellCarriedCoderToHrVeronika(grant);
            case FocusKind.Desk:
                var desk = GalleyDesks[focus.Index];
                if (desk.IsOccupied) return desk.IsSlackingInTwitter ? "Кодер отвлёкся на Twitter" : "Кодер работает";
                return SeatCarriedCoderAtGalley(focus.Index);
            case FocusKind.MacBook:
                ActivateMacBookTrap(SelectedBaitTier,spend,out string trap);
                return trap;
            case FocusKind.Elevator:
                TrySummonElevatorBoss(spend,out string lift);
                return lift;
            case FocusKind.Shop:
                SelectedBaitTier=(SubscriptionBaitTier)(((int)SelectedBaitTier+1)%4);
                return GetBaitInfo(SelectedBaitTier).Description;
            default: return HandleInteractKeyE(camera.Position,spend,grant);
        }
    }

    public string FocusPrompt(Camera3D camera) => FindFocus(camera).Label;

    private Focus FindFocus(Camera3D camera)
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
        Consider(CentralMacBookPos, 1.1f, 3.8f, FocusKind.MacBook, "Макбук-ловушка");
        Consider(HrTarotBoothPos, 1.6f, 4.6f, FocusKind.Veronika, "HR Вероника");
        Consider(ArsenalShopPos, 1.1f, 4.2f, FocusKind.Shop, "Выбрать подписку");
        Consider(NorthElevatorPos,1.65f,5.5f,FocusKind.Elevator, "Центральный лифт");
        Consider(SouthElevatorDoorPos,1.65f,5.2f,FocusKind.Elevator, "Центральный лифт");
        Consider(ElevatorButtonPos, 1.22f, 4, FocusKind.Elevator, "Вызвать лифт");
        Consider(SouthElevatorButtonPos, 1.22f, 4, FocusKind.Elevator, "Вызвать лифт");
        for(int i=0;i<GalleyDesks.Count;i++)
            Consider(GalleyDesks[i].DeskPosition,.9f,3.6f,FocusKind.Desk,"Стол галеры",i);
        for(int i=0;i<ActiveCoders.Count;i++)
            if(ActiveCoders[i].State==CoderAIState.Subdued)
                Consider(ActiveCoders[i].Position,.45f,3.8f,FocusKind.Coder,ActiveCoders[i].Name,i);
        return focus;
    }

    private static readonly Vector3 LiftSignSize=new(1.48f,1.12f,.08f);
    private static Vector3 LiftSignCenter(Vector3 face,int side) => face+new Vector3(0,4.75f,side*.14f);

    public void DrawWorldLabelsAndIndicators2D(Camera3D camera, Font font)
    {
        WorldLabelLayout.BeginFrame();
        if(camera.Position.Z<NorthElevatorPos.Z)
            WorldLabelLayout.ReserveProjectedBox(camera,LiftSignCenter(NorthElevatorPos,-1),LiftSignSize*.5f);
        if(camera.Position.Z>SouthElevatorDoorPos.Z)
            WorldLabelLayout.ReserveProjectedBox(camera,LiftSignCenter(SouthElevatorDoorPos,1),LiftSignSize*.5f);
        Vector3 look = Vector3.Normalize(camera.Target-camera.Position);
        void ProtectHead(Vector3 feet,float scale,float height)
        {
            WorldLabelLayout.ReserveProjectedBox(camera,feet+Vector3.UnitY*(1.54f*height),
                new Vector3(.14f*scale,.16f*height,.18f*scale));
        }
        if(Partner is {} partner) ProtectHead(partner.Feet,1,partner.Pose.Eye.Y<1.4f?.75f:1);
        foreach(var actor in ActiveCoders)
        {
            if(actor.State==CoderAIState.Subdued || Vector3.DistanceSquared(camera.Position,actor.Position)>100) continue;
            float scale=actor.IsMiniBoss?1.28f:1;
            ProtectHead(actor.Position,scale,scale);
        }
        void Plate(Vector3 head,string name,float health,Color accent)
        {
            if (Vector3.Dot(head-camera.Position,look)<.1f) return;
            Vector2 screen=Raylib.GetWorldToScreen(head,camera)-new Vector2(0,4);
            Font tag=GameFonts.Get(Typeface.InterfaceBold,16);
            float width=GameFonts.Measure(tag,name,16).X+22;
            if (!WorldLabelLayout.Place(screen,width,29,out var r)) return;
            Raylib.DrawRectangleRounded(r,.70f,10,new Color(19,25,34,223));
            Raylib.DrawRectangleRoundedLinesEx(r,.70f,10,1,new Color(171,181,189,160));
            GameFonts.Draw(tag,name,new(r.X+11,r.Y+2),16,new Color(247,244,235,255));
            Rectangle track=new(r.X+11,r.Y+22,r.Width-22,3);
            Raylib.DrawRectangleRounded(track,1,8,new Color(75,79,86,255));
            track.Width*=Math.Clamp(health,0,1);
            if(track.Width>=1) Raylib.DrawRectangleRounded(track,1,8,accent);
        }
        if(Partner is {} ally && Vector3.DistanceSquared(camera.Position,ally.Feet)<400)
            Plate(ally.Feet+Vector3.UnitY*(ally.Pose.Eye.Y<1.4f?1.30f:1.72f),"БРАТАН [КООП]",
                Program.PartnerHp/100f,new Color(109,202,162,255));
        foreach(var c in ActiveCoders)
        {
            if(Vector3.DistanceSquared(camera.Position,c.Position)>100 || c.State==CoderAIState.Subdued) continue;
            Plate(c.Position+Vector3.UnitY*(c.IsMiniBoss?2.43f:1.74f),c.IsMiniBoss?"ГУРУ КУРСОРА":c.Name,
                c.Hp/c.MaxHp,new Color(236,168,93,255));
        }
        if(ElevatorBoss.IsActive && !ElevatorBoss.IsDefeated)
            Plate(ElevatorBoss.Position+Vector3.UnitY*3.4f,"СБЕЖАВШИЙ ИИ-АГЕНТ",
                ElevatorBoss.Hp/ElevatorBoss.MaxHp,new Color(228,110,105,255));
        foreach(var p in EnemyProjectiles)
        {
            Vector3 head=p.Position+Vector3.UnitY*(p.Radius+.15f);
            if(Vector3.Dot(head-camera.Position,look)<.1f) continue;
            float w=GameFonts.Measure(font,p.Label,14).X+12;
            if(WorldLabelLayout.Place(Raylib.GetWorldToScreen(head,camera),w,22,out var r))
            {
                Raylib.DrawRectangleRounded(r,.5f,8,new Color(18,26,34,182));
                GameFonts.Draw(font,p.Label,new(r.X+6,r.Y+2),14,Color.White);
            }
        }
    }
}
