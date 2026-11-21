#nullable enable
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Raylib_cs;

namespace HowToVibeCoder;

internal enum FloorPanel { None, Contracts, Upgrades, Debrief, Audit, BossFinale }
internal enum OnboardingStep { TalkToVeronika, CatchFrogs, DepositFrogs, BuyNet, ActivateBait, StunAndHeal, HealAtCoffee, CaptureFirstCoder, SeatFirstGalley, Done }
internal enum Incident { Printer, Coffee, Bugs, Audit, Ddos, Burnout, Raid, Fire }
internal enum Sidegrade { Salt, Negotiator, Tape, Drum, DirectorBelt, Pager, RosinNapalm, CarbonNda, AuthoritySignet, PortableCo2, CarryHarness, NetReinforced, NetPro90s }
internal enum StyleAction { Belt, Iron, Uzi, Shotgun, Beam, Parry, Water, Capture }
internal record Job(string Title, string Objective, int Seconds, int Base);
internal record Gear(string Title, string Detail, int Price, bool FixedPrice=false);
internal record Settlement(int Serial, int Job, bool Success, int Amount, float Greed, float Bonus,
    int Captured, float Peak, string Rank, string Diagnosis);

internal sealed class WaterSpill
{
    public Vector3 Position;
    public float Wet, Electric;
}

internal sealed partial class FloorSystems
{
    internal static readonly Job[] Jobs = [
        new("Срочный MVP до обеда","Посадить двоих в Опенспейс и заработать $450",150,650),
        new("Тихий хантинг Сеньора","Доставить Архитектора живым при СБ ниже 45",120,980),
        new("Финтех-спринт без СБ","Доставить Крипто-Бро или заработать $900",100,820),
        new("Релиз в Пятницу","Нажать DEPLOY и защищать Опенспейс 45 секунд",110,1200),
        new("Карантинная шахта","Вернуть Северный лифт в строй до блокировки этажа",180,1500)
    ];
    internal static readonly Gear[] GearList = [
        new("Соль-дробь","Срывает выдержку и оглушает. Урон по броне снижен на 55%.",210),
        new("Картечь «Переговорщик»","На 40% больше урона по броне агента. Смертельна для кадров.",340),
        new("Синяя изолента MK2","На 45% сильнее удар по эго. Жало быстрее греется и остывает.",260),
        new("Барабан Узи","50 патронов. Перезарядка на 30% дольше.",320),
        new("Ремень «Директорский»","Цепляет соседние цели. Шире окно отражения, короче оглушение.",250),
        new("Пейджер Крыши","Один вызов [P] отзывает рейд и снимает 35 внимания СБ.",180),
        new("Канифольный напалм","Поджигает цель. Перегрев B12 ×2, выше риск потерять кадр.",160),
        new("NDA на копирке","Отлов и подготовка NDA вдвое быстрее. За подпись +6 СБ.",150),
        new("Печатка Авторитета","Авторитет и страх +30%. Цены +15%, кроме паяльника и пропуска.",210),
        new("Карманный CO2-баллон","[Q] Облако дыма и −15 ПРОД. Перезарядка 25 с.",190),
        new("Плечевой ремень","Быстрее несёшь кадра, можешь бежать и стрелять",175),
        new("Армированный сачок","Титановый обод: ×2 урон и оглушение — вайбкодеры перестают ржать и убегать.",45),
        new("Сетевой уловитель PRO","Утяжелённый захват 90-х: ×3 урон сачком, нокдаун и бонус +×0.35 к кассе.",115)
    ];

    internal static readonly WeaponType[] WeaponOrder = [WeaponType.CaptureNet,
        WeaponType.Belt, WeaponType.SolderingIron, WeaponType.MicroUzi, WeaponType.SawedOff];
    internal static readonly string[] WeaponModels=["weapon_net_90s","weapon_belt_90s","weapon_solder_90s","weapon_uzi_90s","weapon_shotgun_90s"];
    internal static readonly Gear[] Weapons = [
        new("Сачок-ловушка","Урон 12. До 3 Джаба-жаб ($1–$8) и стажёры",3),
        new("Ремень 90-х","Урон 24. Ломает выдержку вайб-кодеров под NDA",25),
        new("Паяльник 90-х","Урон 42. Пробивает броню и чинит щиток Северного лифта",90,FixedPrice:true),
        new("Микро-Узи 90-х","Урон 18/выстр. Плавит щиты босса и рейды СБ",240),
        new("Обрез 12-го калибра","Урон 120. Дуплет по элите, Пятнице и ядру босса",480)
    ];
    internal static readonly Gear ElevatorKeycard = new("Ключ-карта СБ (2-й этаж)",
        "Вероника перепрошьёт дубликат пропуска СБ для Северного лифта",1000,FixedPrice:true);
    internal static readonly Gear[] Arsenal = [.. Weapons, ElevatorKeycard];
    internal const string ShopGreeting="Заходи в арсенал, выбирай инструмент по душе — весь товар прямо на витрине.";
    internal const string KeycardPurchaseLine = "Дубликат прошит — держи Ключ-карту СБ. Теперь перепаяй щиток у Северного лифта [E]!";
    internal static readonly int ClassicMask=WeaponOrder.Aggregate(1<<(int)WeaponType.BareHands,(mask,w)=>mask | (1<<(int)w));
    public int OwnedWeaponsMask=1<<(int)WeaponType.BareHands;
    public List<string> PocketFrogs=new();
    internal OnboardingStep Onboarding { get; set; }
    internal static string VibeCheck(CoderArchetype role)
    {
        var (card,type,verdict)=role switch
        {
            CoderArchetype.SeniorArchitect => ("XVI Башня","INTJ","Culture Fit 12%"),
            CoderArchetype.TeamLeadMeeter => ("X Колесо Фортуны","ENTJ","Culture Fit 98%"),
            CoderArchetype.CryptoBro => ("XV Дьявол","ENFP","Меркурий ретрограден"),
            _ => ("0 Шут","INFP","Меркурий ретрограден")
        };
        return $"{Loc.Tr("Вайб-Чек")}   {Loc.Tr(card)}   {type}   {Loc.Tr(verdict)}";
    }

    public int TrainingSpawns;
    public int PlayerCount=1;
    internal CoopBalance Balance => CoopBalance.For(PlayerCount);
    public bool WingsUnlocked, GalleyTipPending;
    public float WingBarrierSlide, EspressoWait, BarrierVoiceWait;
    public int[] BaitStock = new int[3];
    public FloorBait? PlacedBait;
    public bool FloorHuntEnabled;
    public int ActiveBaitTier => Array.FindLastIndex(BaitStock, count => count > 0);
    public int PocketCoffeeCups=2, GuestCoffeeCups=2, SecondCoffeeCups=2;
    public Dictionary<int,float> StationWait=new();
    public Dictionary<int,int> CoolerDrinks=new();
    public bool FirstFrogCheered;
    internal int FrogProgress => Math.Min(3,PocketFrogs.Count);

    internal string Objective(bool carrying,int sold) => Onboarding switch
    {
        OnboardingStep.TalkToVeronika => "Подойди к стойке и поговори с Вероникой [E]",
        OnboardingStep.CatchFrogs => $"Поймай Джаба-жаб у стойки [E]  ({FrogProgress}/3)",
        OnboardingStep.DepositFrogs => "Сдай Джаба-жаб в Баг-трекер [E]",
        OnboardingStep.BuyNet => "Купи Сачок-ловушку ($3) у Вероники [E]",
        OnboardingStep.ActivateBait => "Положи наживку [G] и спрячься [Ctrl]",
        OnboardingStep.HealAtCoffee => "Выпей эспрессо у стойки (+45 HP) [E]",
        OnboardingStep.StunAndHeal => "Оглуши стажёра сачком [ЛКМ]",
        OnboardingStep.SeatFirstGalley => carrying ? "Отведи кадра на свободный стол Опенспейса [E]" : "Примани вайбкодера для Опенспейса [E]",
        OnboardingStep.CaptureFirstCoder => $"Сдай вайбкодеров Веронике ({Math.Min(sold,Balance.QuarantineUnlockTarget)}/{Balance.QuarantineUnlockTarget})",
        _ => ""
    };

    internal static string CoachText(OnboardingStep step) => step switch
        {
            OnboardingStep.TalkToVeronika => "О, хозяин вернулся! Подойди ко мне на стойку [E] — введу в курс дела за минуту.",
            OnboardingStep.CatchFrogs => "Касса пуста — $0. Вайбкодеры попрятались, но из серверной по полу скачут Джаба-жабы. Схвати 3 штуки руками [E] или ЛКМ — они по $1–$2.",
            OnboardingStep.DepositFrogs => "Отличный улов! Скинь жаб в Баг-трекер рядом со стойкой [E] — он сразу выдаст наличку.",
            OnboardingStep.BuyNet => "Наличка в кармане! Подойди ко мне [E] и возьми Сачок-ловушку за $3.",
            OnboardingStep.ActivateBait => "Сачок в руках! Первый ЯблоБук бесплатно. Положи наживку [G], отойди и спрячься [Ctrl]. Подкрадись сзади, когда кодер сядет у ноутбука.",
            OnboardingStep.StunAndHeal => "Пошли клиенты! Дождись, когда кодер сядет. Подкрадись сзади и оглуши сачком [ЛКМ]. Подхвати его вторым взмахом или нажми [E].",
            OnboardingStep.HealAtCoffee => "Тебя задели! Вайбкодеры кидаются багами и больно бьют. Подойди к кофе-машине рядом со стойкой и выпей эспрессо — восстановишь здоровье! В бою ещё спасают пицца, кулеры и стакан кофе на кнопку Эйч.",
            OnboardingStep.CaptureFirstCoder => "Бодрящий эспрессо творит чудеса! Теперь подними оглушённого вайбкодера сачком или клавишей [E] и неси ко мне!",
            OnboardingStep.SeatFirstGalley => "Отлично, карантин снят — Западное и Восточное крылья открыты! Слева Опенспейс: сажай туда вайбкодеров промптить за пассивный доход. Справа серверная и охрана эс-бэ — у них есть пропуск, но дерутся они жёстко!",
            _ => "Дело пошло! Чтобы уехать на 2-й этаж, перепаяй Северный лифт Паяльником и сними замок СБ — могу перепрошить Ключ-карту за $1000 у меня [E], либо отбери оригинал у СБ."
        };
    internal static string CoachClip(OnboardingStep step) => "vo_veronika_"+(step switch
    {
        OnboardingStep.TalkToVeronika=>"intro", OnboardingStep.CatchFrogs=>"frogs",
        OnboardingStep.DepositFrogs=>"deposit", OnboardingStep.BuyNet=>"buynet",
        OnboardingStep.ActivateBait=>"bait", OnboardingStep.StunAndHeal=>"capture", OnboardingStep.HealAtCoffee=>"heal_lesson", OnboardingStep.CaptureFirstCoder=>"heal_done", OnboardingStep.SeatFirstGalley=>"wings_unlocked", _=>"done"
    });
    internal void Coach() => SpeakCoaching(CoachText(Onboarding),CoachClip(Onboarding));

    internal static void SpeakCoaching(string line,string? clipId=null)
    {
        AudioMusicSystem.TriggerVoiceLine("Вероника", "Вероника: "+line, (int)SpeakerVoiceType.VeronikaTarot,clipId);
    }

    internal void UpdateOnboarding(SkyscraperFloor1World world,int cash)
    {
        if(GalleyTipPending && AudioMusicSystem.VoiceTimer<=0)
        { GalleyTipPending=false; SpeakCoaching(GalleyLine); }
        if(!WingsUnlocked && world.TotalCodersSoldToHr is 1 or 2 && Onboarding<OnboardingStep.CaptureFirstCoder)
            Onboarding=OnboardingStep.CaptureFirstCoder;
        bool seated=world.GalleyDesks.Exists(d=>d.IsOccupied);
        if(!WingsUnlocked && world.TotalCodersSoldToHr>=Balance.QuarantineUnlockTarget)
        {
            WingsUnlocked=true;
            AudioMusicSystem.PlayMacbookBait();
            Onboarding=seated ? OnboardingStep.Done : OnboardingStep.SeatFirstGalley;
            SpeakCoaching(CoachText(OnboardingStep.SeatFirstGalley));
            if(seated) GalleyTipPending=true;
            return;
        }
        if(Onboarding==OnboardingStep.SeatFirstGalley && (seated || HasWeapon(WeaponType.SolderingIron) || world.HasElevatorKey))
        {
            Onboarding=OnboardingStep.Done;
            SpeakCoaching(GalleyLine);
        }
        if(Onboarding>=OnboardingStep.StunAndHeal) return;
        var next=world.IsMacBookTrapActive && HasWeapon(WeaponType.CaptureNet) ? OnboardingStep.StunAndHeal :
            HasWeapon(WeaponType.CaptureNet) ? OnboardingStep.ActivateBait :
            cash>=3 ? OnboardingStep.BuyNet :
            FrogProgress>=3 ? OnboardingStep.DepositFrogs : Onboarding;
        if(next>Onboarding)
        {
            Onboarding=next;
            Coach();
        }
        else if(Onboarding<=OnboardingStep.CatchFrogs && !FirstFrogCheered && PocketFrogs.Count>0)
        {
            FirstFrogCheered=true;
            CheerFirstFrog();
        }
    }
    internal const string LockedLine="Крылья офиса пока на карантине СБ. Сначала освойся в атриуме и принеси мне трёх вайбкодеров — тогда сниму блокировку.";
    internal const string GalleyLine="Первый промптер сел в Опенспейс и уже приносит кэш! Копи на Паяльник и Ключ-карту эс-бэ — и сможешь вскрыть Северный лифт на второй этаж.";
    internal const string FirstFrogLine="Есть первая! Ещё пару штук — и хватит на Сачок.";
    internal static void CheerFirstFrog() => SpeakCoaching(FirstFrogLine);
    internal bool HasWeapon(WeaponType w) => (OwnedWeaponsMask & (1<<(int)w))!=0;
    internal string BuyWeapon(int row,Func<int,bool> spend)
    {
        var weapon=WeaponOrder[row];
        var gear=Weapons[row];
        if(HasWeapon(weapon)) return gear.Title+" уже в арсенале";
        if(!spend(Price(gear))) return $"Для покупки нужно ${Price(gear)}";
        OwnedWeaponsMask |= 1<<(int)weapon;
        if(weapon==WeaponType.CaptureNet) BaitStock[0]++;
        return gear.Title+" в арсенале";
    }

    public float TD, PT, SEC;
    public int JobId = -1, Earned, Seated, Captured, StaffValue;
    public bool SeniorDelivered, CryptoDelivered, ReleaseStarted, ReleaseFinished, CrewLost;
    public float Remaining, Peak, Friday, Defense = 100, NextWave, NextIncident = 60;
    public float PrinterWait, Jam, Audit, Coffee, Badge, RackHeat, PagerWait;
    public bool Ddos, Raid, WantsCoffee, FireIncident;
    public List<WaterSpill> Puddles=new();
    public Dictionary<string,int> DiscoveredSpecies = new();
    public string DiscoveryId = "";
    public float DiscoveryTime;
    public bool FirstDiscovery;
    public List<string> PendingFrogDiscoveries=new();

    internal void Discover(VibeCoderSpecies species,Action<int,string>? grant)
    {
        int previous=DiscoveredSpecies.GetValueOrDefault(species.Id);
        DiscoveredSpecies[species.Id]=previous+1;
        if(previous==0) grant?.Invoke(species.BonusPayout,"Открытие вида");
        if(previous==0 || species.Rarity!=VibeCoderRarity.Common)
        {
            if(species.IsFrog && DiscoveryTime>0)
            {
                if(previous==0) PendingFrogDiscoveries.Add(species.Id);
                return;
            }
            DiscoveryId=species.Id;
            FirstDiscovery=previous==0;
            DiscoveryTime=4;
        }
    }

    public int Owned;
    public Sidegrade? Shell;
    public float Authority, StyleTime, Variety = 1;
    public int Repeats;
    public List<StyleAction> Recent = new();
    public Settlement? Report;
    public string Notice = "";
    public float NoticeTime;
    public int Serial, LastIncident = -1;
    internal float R => .35f*TD+.35f*PT+.30f*SEC;
    internal float FearMultiplier => Has(Sidegrade.AuthoritySignet) ? 1.3f : 1;
    internal float GreedMultiplier => (1+R/100)*(Defiance>0 ? 1.35f : 1);
    internal float FridayMultiplier => Friday>0 ? 2.5f : 1;
    internal int RankIndex => Authority switch { >=100=>4, >=65=>3, >=35=>2, >=14=>1, _=>0 };
    private static readonly string[] Ranks=["Д","В","Б","А","АВТОРИТЕТ"];
    internal string Rank => Ranks[RankIndex];
    internal bool Has(Sidegrade id) => (Owned & (1<<(int)id))!=0;
    internal void Announce(string line,float seconds=4)
    {
        Notice=line;
        NoticeTime=seconds;
    }
    internal void Risk(float debt=0,float heat=0,float security=0)
    {
        static bool Cross(float was,float now) => was<30 && now>=30 || was<60 && now>=60 || was<80 && now>=80;
        float d=TD,p=PT,s=SEC;
        float Growth(float change) => change>0 && Verdict==AgentVerdict.Released ? change*.8f : change;
        TD=Math.Clamp(TD+Growth(debt),0,100);
        PT=Math.Clamp(PT+Growth(heat),0,100);
        SEC=Math.Clamp(SEC+Growth(security),0,100);
        if(JobId>=0) Peak=Math.Max(Peak,R);
        if(JobId==1 && SEC>=45) StealthBlown=true;
        if(Cross(d,TD) || Cross(s,SEC) || Cross(p,PT)) AudioMusicSystem.PlayTowerPA();
        if(Cross(d,TD)) Announce("Диспетчерская: выгорание ниже квартального плана. Проявите инициативу.");
        if(Cross(s,SEC)) Announce("Диспетчерская: у legacy stakeholder нет допуска к серверной.");
        if(Cross(p,PT)) Announce($"Диспетчерская: температура прода {(int)PT}°C. {((PT>=80) ? "Пятничный деплой разрешён." : "Заявка на охлаждение в очереди.")}");
    }

    public bool StealthBlown;
    internal float Style(StyleAction action)
    {
        bool same=Recent.Count>0 && Recent[^1]==action;
        Repeats=same ? Repeats+1 : 1;
        Recent.Add(action);
        if(Recent.Count>5) Recent.RemoveAt(0);
        float efficiency=Repeats switch { 1=>1, 2=>.7f, _=>.5f };
        Variety=same ? efficiency : Math.Min(2.5f,1+.38f*(Recent.Distinct().Count()-1));
        float points=action switch { StyleAction.Capture=>12, StyleAction.Parry or StyleAction.Water=>9, _=>6 };
        Authority=Math.Clamp(Authority+(same && action!=StyleAction.Capture ? -2 : points*Variety*(Has(Sidegrade.AuthoritySignet) ? 1.3f : 1)*(Defiance>0 ? 1.35f : 1)),0,125);
        StyleTime=10;
        return efficiency*(1+.12f*RankIndex);
    }

    internal void Deliver(CoderArchetype who,bool seated,int value)
    {
        if(JobId<0) return;
        Captured++;
        StaffValue+=value;
        if(seated) Seated++;
        SeniorDelivered |= who==CoderArchetype.SeniorArchitect;
        CryptoDelivered |= who==CoderArchetype.CryptoBro;
    }

    internal string Start(int id,SkyscraperFloor1World world)
    {
        if(id<0 || id>=Jobs.Length || JobId>=0) return "Сначала закрой текущий заказ";
        if(id==1 && SEC>=45) return "Сначала снизь внимание СБ ниже 45";
        if(id==3 && Friday>0) return "Дождись конца текущего деплоя";
        if(id==4 && world.ElevatorBoss.IsDefeated) return "Дело по лифту уже завершено";
        JobId=id;
        Remaining=Jobs[id].Seconds;
        Earned=Seated=Captured=StaffValue=0;
        StealthBlown=SeniorDelivered=CryptoDelivered=ReleaseStarted=ReleaseFinished=CrewLost=false;
        Peak=R;
        Risk(debt:id is 0 or 2 ? 3 : 1,heat:2);
        if(id==1) world.EnsureCandidate(CoderArchetype.SeniorArchitect);
        if(id==2) world.EnsureCandidate(CoderArchetype.CryptoBro);
        return Jobs[id].Objective;
    }

    internal string Deploy(SkyscraperFloor1World world)
    {
        if(!world.IsMacBookTrapActive) return "Сначала включи ЯблоБук-приманку";
        if(Friday>0) return $"Деплой уже идёт: {MathF.Ceiling(Friday)} с";
        CrewLost=false;
        Friday=45;
        NextWave=8;
        Defense=100;
        ReleaseStarted=JobId==3;
        Risk(heat:30);
        world.SpawnWave(2);
        AudioMusicSystem.PlayBossSiren();
        return "Деплой начался. Удерживай узел Опенспейса 45 секунд.";
    }

    internal string Buy(int index,Func<int,bool> spend)
    {
        if(index<0 || index>=GearList.Length) return "";
        var id=(Sidegrade)index;
        if(id==Sidegrade.Pager && Has(id)) return "Пейджер заряжен. Вызов на [P]";
        if(!Has(id))
        {
            if(!spend(Price(GearList[index]))) return "В общаке не хватает денег";
            Owned |= 1<<index;
        }
        if(id is Sidegrade.Salt or Sidegrade.Negotiator) Shell=Shell==id ? null : id;
        return id is Sidegrade.Salt or Sidegrade.Negotiator && Shell==null ? "Заряжена обычная дробь" : GearList[index].Title+" в снаряжении";
    }

    internal string UsePager(SkyscraperFloor1World world)
    {
        if(!Has(Sidegrade.Pager)) return "Пейджер продаётся в арсенале";
        if(PagerWait>0) return $"Линия занята ещё {MathF.Ceiling(PagerWait)} с";
        Owned &= ~(1<<(int)Sidegrade.Pager);
        PagerWait=55;
        Raid=false;
        world.ActiveCoders.RemoveAll(c=>c.RaidGuard && c.State!=CoderAIState.Subdued);
        Risk(security:-35);
        return "Крыша отозвала наряд. СБ -35";
    }

    internal string Print()
    {
        if(Jam>0) return $"Замятие бумаги. Ремонт через {MathF.Ceiling(Jam)} с";
        if(PrinterWait>0) return $"Принтер остывает: {MathF.Ceiling(PrinterWait)} с";
        PrinterWait=35; Badge=1;
        Risk(security:-20);
        return "Пропуск напечатан. СБ -20";
    }

    internal string Reboot(Action<int,string> pay)
    {
        if(RackHeat<=0 && !Ddos && !FireIncident) return "B12 в норме";
        if(FireIncident) { pay(120,"Охлаждение B12"); Earned+=120; }
        Ddos=FireIncident=false; RackHeat=0;
        Risk(-8,-16);
        return "B12 перезапущена. Охлаждение работает";
    }

    internal void Overheat(SkyscraperFloor1World world,float strength=1)
    {
        Risk(heat:(RackHeat<=0 ? 12 : 2)*strength);
        RackHeat=18;
        foreach(var c in world.ActiveCoders.Where(c=>Vector3.DistanceSquared(c.Position,world.RackPos)<64 && c.State!=CoderAIState.Subdued))
        {
            c.Pressure(24,0,20*FearMultiplier);
            if(c.State!=CoderAIState.Subdued) { c.State=CoderAIState.PanickedFleeing; c.Windup=0; }
        }
    }

    internal void RestoreMorale(SkyscraperFloor1World world)
    {
        Conflict=false; ConflictWait=24;
        WantsCoffee=false;
        foreach(var d in world.GalleyDesks.Where(d=>d.IsOccupied))
        {
            d.Morale=1;
            d.Burnout=Math.Max(0,d.Burnout-45);
            d.IsSlackingInTwitter=false; d.SlackCycleTimer=30;
        }
    }

    internal void IncidentNow(Incident incident,SkyscraperFloor1World world)
    {
        LastIncident=(int)incident;
        string message;
        switch(incident)
        {
            case Incident.Printer: Jam=20; message="Принтер зажевал пачку пропусков. Ремонт 20 с."; break;
            case Incident.Coffee:
                WantsCoffee=true; message="Опенспейс требует раф. Кофемашина у приёмной.";
                foreach(var d in world.GalleyDesks) if(d.IsOccupied) d.Morale=.65f;
                break;
            case Incident.Bugs: world.SpawnWave(2); message="Выброс багов. Дежурная смена идёт в зал."; break;
            case Incident.Audit:
            case Incident.Raid:
                Audit=0; Raid=true; world.SpawnWave(3,raid:true);
                message="Проверка СБ! Охрана входит из восточного крыла.";
                AudioMusicSystem.Speak("SYSTEM ZERO",message,SpeakerVoiceType.RoboticAgent); break;
            case Incident.Ddos: Ddos=true; message="DDoS: выход Опенспейса упал вдвое. Перезапусти B12."; break;
            case Incident.Burnout:
                var worker=world.GalleyDesks.Where(d=>d.IsOccupied).MaxBy(d=>d.Burnout);
                if(worker!=null) { worker.Burnout=100; worker.IsSlackingInTwitter=true; }
                message="Кадр выгорел. Кофе или ремень вернут его в смену."; break;

            default: FireIncident=true; Overheat(world); message="B12 горит. За срочное охлаждение касса выделила $120."; break;
        }
        Announce(message,6);
    }

    internal void Tick(float dt,SkyscraperFloor1World world,Action<int,string>? pay)
    {
        float Down(float t) => Math.Max(0,t-dt);
        DiscoveryTime=Down(DiscoveryTime);
        if(DiscoveryTime==0 && PendingFrogDiscoveries.Count>0)
        {
            DiscoveryId=PendingFrogDiscoveries[0]; PendingFrogDiscoveries.RemoveAt(0);
            FirstDiscovery=true; DiscoveryTime=4;
        }
        NoticeTime=Down(NoticeTime); PrinterWait=Down(PrinterWait); Jam=Down(Jam);
        Coffee=Down(Coffee); PagerWait=Down(PagerWait);
        for(int i=Puddles.Count-1;i>=0;i--)
        {
            var spill=Puddles[i];
            spill.Wet=Down(spill.Wet);
            spill.Electric=Math.Min(spill.Wet,Down(spill.Electric));
            if(spill.Wet<=0) Puddles.RemoveAt(i);
        }
        if(!FireIncident) RackHeat=Down(RackHeat);
        StyleTime=Down(StyleTime);
        if(StyleTime==0) { Recent.Clear(); Repeats=0; Variety=1; Authority=Down(Authority); }
        if(Audit>0) IncidentNow(Incident.Audit,world); // Convert pending inspections from older saves.
        if(WingsUnlocked) WingBarrierSlide=Math.Min(1,WingBarrierSlide+dt/1.4f);
        EspressoWait=Math.Max(0,EspressoWait-dt/Balance.HealCooldownScale); BarrierVoiceWait=Down(BarrierVoiceWait);
        foreach(int id in StationWait.Keys.ToArray())
        {
            StationWait[id]=Math.Max(0,StationWait[id]-dt/Balance.HealCooldownScale);
            if(StationWait[id]==0) { StationWait.Remove(id); CoolerDrinks.Remove(id); }
        }
        TickOffice(dt,world);
        int staff=world.GalleyDesks.Count(d=>d.IsOccupied);
        float rush=JobId>=0 && Remaining<Jobs[JobId].Seconds*.2f ? .17f*dt : 0;
        Risk(debt:rush,heat:dt*((JobId>=0 ? .10f : 0)+(staff>=3 ? .13f : 0)+(RackHeat>0 ? .45f : 0)+(Friday>0 ? .32f : 0)));
        if(Friday>0)
        {
            if((NextWave-=dt)<=0) { world.SpawnWave(2); NextWave=10; }
            int threats=world.ActiveCoders.Count(c=>c.ReleaseAttacker && c.State==CoderAIState.CombatHostile && c.StunTimer<=0 &&
                Vector3.DistanceSquared(c.Position,world.GalleyDesks[0].DeskPosition)<18);
            Defense=Math.Max(0,Defense-threats*1.8f*dt);
            Friday=Down(Friday);
            if(Defense==0 || CrewLost) { Friday=0; ReleaseFinished=false; }
            else if(Friday==0) ReleaseFinished=ReleaseStarted;
            if(Friday==0) foreach(var c in world.ActiveCoders) c.ReleaseAttacker=false;
        }
        if(Raid && !world.ActiveCoders.Exists(c=>c.RaidGuard && c.State!=CoderAIState.Subdued)) Raid=false;
        if(WingsUnlocked && (NextIncident-=dt)<=0)
        {
            List<Incident> options=[Incident.Printer];
            if(world.IsMacBookTrapActive) options.Add(Incident.Bugs);
            if(staff>0) { options.Add(Incident.Coffee); options.Add(Incident.Ddos); if(R>=40) options.Add(Incident.Burnout); }
            if(world.IsMacBookTrapActive && SEC>=55 && !Raid) options.Add(Incident.Raid);
            if(PT>=60 && !FireIncident) options.Add(Incident.Fire);
            options.Remove((Incident)LastIncident);
            IncidentNow(options[Random.Shared.Next(options.Count)],world);
            NextIncident=65-.20f*R+Random.Shared.NextSingle()*5;
        }
        if(JobId<0) return;
        Remaining=Down(Remaining);
        string? failure=CrewLost ? "Смена потеряла бойца." : StealthBlown ? "СБ засекла вербовку Сеньора." :
            JobId==3 && ReleaseStarted && Defense<=0 ? "Налётчики разбили узел Опенспейса." :
            JobId==4 && R>=90 ? "СБ заблокировала лифтовую шахту." : null;
        bool success=JobId switch { 0=>Seated>=2 && Earned>=450,1=>SeniorDelivered,2=>CryptoDelivered || Earned>=900,
            3=>ReleaseFinished,4=>world.ElevatorBoss.IsDefeated,_=>false };
        if(failure==null && !success && Remaining>0) return;
        success &= failure==null;
        string[] verdicts=["Два новых кадра отбили аванс до обеда.","Архитектор подписал NDA до того, как СБ подняла журнал.",
            CryptoDelivered ? "Крипто-Бро подписал оффер до закрытия торгов." : "Выручка закрыла спринт до конца торгов.",
            "Опенспейс пережила пятничную смену.","Шахта снова под контролем диспетчера."];
        float bonus=(1+.06f*RankIndex)*(JobId==2 ? 1.4f : 1)*(JobId==3 ? 2.5f : FridayMultiplier);
        if(world.Working(CoderArchetype.ToxicPM)) bonus*=1.2f;
        if(JobId==2 && world.GalleyDesks.Exists(d=>d.IsOccupied && d.Archetype==CoderArchetype.CryptoBro)) bonus*=1.5f;
        int amount=success ? (int)MathF.Round(Jobs[JobId].Base*GreedMultiplier*bonus) : -(350+StaffValue/2);
        Report=new(++Serial,JobId,success,amount,GreedMultiplier,bonus,Captured,Peak,Rank,
            failure ?? (success ? verdicts[JobId] : "Дедлайн прошёл раньше последней сдачи."));
        JobId=-1;
        pay?.Invoke(amount,"Расчёт по контракту");
    }

    internal string Progress => JobId switch {
        0=>$"Опенспейс {Seated}/2    Выручка ${Earned}/450",1=>$"Доставить Архитектора    СБ {(int)SEC}/45",
        2=>$"Доставить Крипто-Бро или заработать ${Earned}/900",3=>ReleaseStarted ? $"Удержать Опенспейс    Узел {Defense:F0}%" : "Нажать красную кнопку у лифта",
        4 when AgentHealth<0=>"Восстановить питание Северного лифта",
        4=>$"Ликвидировать агента    Корпус {MathF.Ceiling(100f*AgentHealth)}%",_=>"Заказы [J]    Арсенал [U]" };
    public float AgentHealth=-1;
}

public sealed partial class SkyscraperFloor1World
{
    internal static readonly string[] PaLines=[
        "Объявление Башни: уровень выгорания ниже квартального плана. Просьба проявить инициативу.",
        "Объявление Башни: в зоне кофе-пойнта обнаружена несанкционированная пицца. Просим оформить тикет.",
        "Объявление Башни: стойка B12 перегрелась. Просим не кормить сбежавших Джаба-жаб.",
        "Объявление Башни: напоминаем — пятничный релиз продлевает рабочий день, но укрепляет корпоративный дух.",
        "Объявление Башни: доступ на 2-й этаж временно ограничен отделом СБ. Хорошего спринта!"];
    private float nextAnnouncement=52;
    private int announcementIndex;

    internal void UpdateIntercom(float dt)
    {
        if(Systems.Onboarding!=OnboardingStep.Done) return;
        nextAnnouncement-=dt;
        if(nextAnnouncement>0 || AudioMusicSystem.VoiceTimer>0 || ElevatorBoss.SpeechTimer>0 ||
           ElevatorBoss.State is ElevatorBossState.IntroSpeech or ElevatorBossState.Repairing) return;
        string line=PaLines[announcementIndex%PaLines.Length];
        AudioMusicSystem.Speak("SYSTEM ZERO",line,SpeakerVoiceType.RoboticAgent);
        nextAnnouncement=45+(++announcementIndex*13%21);
    }
}

public static partial class Program
{
    private static GameSaveData? pendingFloorSave;
    private static FloorSnapshot? pendingShift;
    private static readonly JsonSerializerOptions FloorJson=new() { IncludeFields=true, IgnoreReadOnlyProperties=true };
    private sealed record SavedShift(FloorSnapshot Floor);
    // The existing menu owns the slot filename and stamps it after its capture callback.
    // Extend that same slot after the synchronous save; old slots have no Shift member.
    private static void FlushFloorSave()
    {
        if(pendingFloorSave is not {} data) return;
        pendingFloorSave=null;
        string path=MainMenuAndPauseUI.GetSaveFilePath(data.SlotIndex);
        WriteFloorSave(data,path,pendingShift ?? CaptureFloor());
        pendingShift=null;
    }
    private static void WriteFloorSave(GameSaveData data,string path,FloorSnapshot state)
    {
        try
        {
            var root=JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            if((string?)root["SavedAt"]!=data.SavedAt) return;
            root["Shift"]=JsonSerializer.SerializeToNode(new SavedShift(state),FloorJson);
            string staged=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try { File.WriteAllText(staged,root.ToJsonString()); File.Move(staged,path,true); }
            finally { if(File.Exists(staged)) File.Delete(staged); }
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException)
        { MainMenuAndPauseUI.Notify("Ошибка сохранения смены: "+e.Message); }
    }
    private static void RestoreFloorSave(GameSaveData data,string? path=null)
    {
        path??=MainMenuAndPauseUI.GetSaveFilePath(data.SlotIndex);
        var shift=File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path))?["Shift"]?.Deserialize<SavedShift>(FloorJson) : null;
        if(shift?.Floor is not {Systems:{} systems} state)
        {
            _world.ElevatorRepaired=data.ElevatorBossDefeated;
            _world.ElevatorGlassOpacity=data.ElevatorBossDefeated ? 0 : 1;
            _world.ElevatorDoorOpenAmount=data.ElevatorBossDefeated ? 1 : 0;
            ApplyProgression(null);
            Floor.BaitStock[0]=1; Floor.FloorHuntEnabled=true;
            _weapons.Equip((WeaponType)Math.Clamp(data.CurrentWeaponSlot+1,1,4));
            foreach(var desk in _world.GalleyDesks.Take(Math.Clamp(data.SeatedGalleyCoders,0,4)))
            { desk.IsOccupied=true; desk.CoderName="Дежурный кодер"; }
            return;
        }
        _world.Systems=systems; seenReport=systems.Serial;
        ApplyProgression(state);
        _world.CarriedCoder=state.HostCarried;
        var savedWeapon=state.ProgressionVersion>=1 ? state.Equipped : (WeaponType)Math.Clamp(data.CurrentWeaponSlot+1,1,4);
        _weapons.Equip(_world.CarriedCoder!=null && !_world.CanCarryWith(savedWeapon) ? WeaponType.BareHands : savedWeapon);
        if(state.Equipped==WeaponType.BareHands && state.SelectedHotbarSlot is int slot &&
            (uint)slot<(uint)_weapons.HotbarSlotCount && _weapons.Hotbar[slot]==null)
            _weapons.SelectOwnedSlot(slot);
        ApplyElevator(state);
        _weapons.UnlockedChatGun=state.Unlocked;
        _world.SelectedBaitTier=state.Bait; _world.IsMacBookTrapActive=state.Trap; _world.TotalCodersSoldToHr=state.Sold;
        _world.ActiveCoders.Clear(); _world.ActiveCoders.AddRange(state.Coders);
        if(state.Escorts is {Length:3})
            _world.ActiveCoders.AddRange(state.Escorts.Skip(1).OfType<VibeCoderEntity>());
        else if(state.Carried!=null) _world.ActiveCoders.Add(state.Carried);
        _world.AdaptPlayers(Net.ActivePlayerCount);
        foreach(var desk in state.Desks)
            if((uint)desk.Index<_world.GalleyDesks.Count) desk.DeskPosition=_world.GalleyDesks[desk.Index].DeskPosition;
        _world.GalleyDesks.Clear(); _world.GalleyDesks.AddRange(state.Desks);
        _world.EnemyProjectiles.Clear(); _world.EnemyProjectiles.AddRange(state.Projectiles);
        _world.ResumeCoderIds();
        if(state.ProgressionVersion<2 && !Floor.WingsUnlocked) _world.MigrateTrainingAtrium();
        if(state.ProgressionVersion<3)
        {
            Floor.FloorHuntEnabled=true;
            if(Floor.HasWeapon(WeaponType.CaptureNet)) Floor.BaitStock[0]=Math.Max(1,Floor.BaitStock[0]);
        }
    }
    private static FloorPanel floorPanel;
    private static bool sidegrades;
    private static int floorRow, seenReport;
    private static string panelMessage="";
    private static readonly Color paper=new(234,227,201,255), amber=new(232,192,110,255),
        quiet=DossierPrint.Quiet, rule=DossierPrint.Stone;
    private static FloorSystems Floor => _world.Systems;

    private static double nextShopGreeting,shopOpenedAt;
    private static void OpenFloorPanel(FloorPanel panel)
    {
        if(panel==FloorPanel.Upgrades && floorPanel!=panel) shopOpenedAt=Raylib.GetTime();
        if(panel==FloorPanel.Upgrades && floorPanel!=panel && Raylib.GetTime()>=nextShopGreeting &&
            !(AudioMusicSystem.VoiceTimer>0 && AudioMusicSystem.SourceSpeaker=="Вероника" &&
              (AudioMusicSystem.ActiveClipId=="vo_veronika_first_frog" || AudioMusicSystem.ActiveClipId==FloorSystems.CoachClip(Floor.Onboarding))))
        {
            FloorSystems.SpeakCoaching(FloorSystems.ShopGreeting);
            nextShopGreeting=Raylib.GetTime()+15;
        }
        floorPanel=panel; floorRow=0; sidegrades=false; panelMessage="";
        if(panel==FloorPanel.None) Raylib.DisableCursor();
        else Raylib.EnableCursor();
    }

    private const int WeaponBuyOffset=30, BuyOffset=10, PagerAction=23, RollbackAction=22;
    private static int KeycardAction => WeaponBuyOffset + FloorSystems.Weapons.Length;
    private static string ApplyFloorAction(int index,bool guestBuyer=false)
    {
        if(index is >=50 and <=52)
        {
            int tier=index-50;
            if(!TrySpendCash(FloorBait.Prices[tier])) return Loc.T("Не хватает денег","Insufficient funds");
            Floor.BaitStock[tier]++;
            return FloorBait.Name(tier)+" ×"+Floor.BaitStock[tier];
        }
        if(index==KeycardAction)
        {
            if(_world.HasElevatorKey) return "Ключ-карта СБ уже получена";
            if(!TrySpendCash(FloorSystems.ElevatorKeycard.Price)) return "Для покупки Ключ-карты СБ нужно $1000";
            _world.HasElevatorKey=true;
            FloorSystems.SpeakCoaching(FloorSystems.KeycardPurchaseLine);
            if(Net.Ready) Net.Send(new("keycard-purchased"));
            return "Вероника: "+FloorSystems.KeycardPurchaseLine;
        }
        if(index>=WeaponBuyOffset && index<WeaponBuyOffset+FloorSystems.Weapons.Length)
        {
            var weapon=FloorSystems.WeaponOrder[index-WeaponBuyOffset];
            bool owned=Floor.HasWeapon(weapon);
            var result=Floor.BuyWeapon(index-WeaponBuyOffset,TrySpendCash);
            if(Floor.HasWeapon(weapon))
            {
                if(guestBuyer)
                {
                    if(!owned) Net.Send(new("floor",Floor:CaptureForSlot(actorSlot)),actorSlot);
                    Net.Send(new("equip-purchase",Text:weapon.ToString()),actorSlot);
                }
                else _weapons.Equip(weapon);
            }
            SetHudBanner(result);
            if(index==WeaponBuyOffset && Floor.Onboarding<OnboardingStep.ActivateBait && Floor.HasWeapon(WeaponType.CaptureNet))
            {
                if(!guestBuyer && floorPanel==FloorPanel.Upgrades) OpenFloorPanel(FloorPanel.None);
                Floor.UpdateOnboarding(_world,_playerCash);
            }
            return result;
        }
        if(index>=0 && index<FloorSystems.Jobs.Length) return Floor.Start(index,_world);
        if(index>=BuyOffset && index<BuyOffset+FloorSystems.GearList.Length) return Floor.Buy(index-BuyOffset,TrySpendCash);
        if(index>=FinaleAction && index<FinaleAction+2) return Floor.ResolveFinale(index-FinaleAction,GrantCash);
        if(index==PagerAction) return Floor.UsePager(_world);
        if(index==RollbackAction && Floor.JobId>=0)
        {
            Floor.Remaining=Math.Max(0,Floor.Remaining-22);
            Floor.Risk(heat:-18);
            if(Floor.Friday>0) { Floor.Friday=0; Floor.ReleaseStarted=Floor.ReleaseFinished=false; }
            return "Вернули стабильную сборку. Прод -18, дедлайн ближе на 22 секунды.";
        }
        return "";
    }
    private static string FloorAction(int code)
    {
        if(!Net.Guest) return ApplyFloorAction(code);
        Net.Send(new("floor-action",Text:code.ToString()));
        return "Запрос передан диспетчеру";
    }

    private static float PanelScale => Math.Min(Raylib.GetScreenWidth()/1280f,Raylib.GetScreenHeight()/720f);
    private static Vector2 PanelOffset => (new Vector2(Raylib.GetScreenWidth(),Raylib.GetScreenHeight())-new Vector2(1280,720)*PanelScale)/2;
    private static Rectangle ArsenalTab(int i) => i==0 ? new(458,32,176,52) : new(642,32,186,52);
    private static Rectangle FloorRowRect(int i)
    {
        if(floorPanel!=FloorPanel.Upgrades)
        {
            // 5 organic pinned paper contract notes arranged on the office corkboard (3 top + 2 bottom)
            return i switch
            {
                0 => new(88, 154, 344, 224),
                1 => new(466, 146, 344, 224),
                2 => new(844, 158, 344, 224),
                3 => new(218, 404, 344, 224),
                _ => new(622, 410, 344, 224),
            };
        }
        int y=sidegrades ? 148 : 152, height=sidegrades ? 60 : 144;
        return new(788+202*(i%2),y+(i/2)*(height+(sidegrades ? 6 : 12)),190,height);
    }

    private static bool HandleFloorPanel()
    {
        bool Key(KeyboardKey k) => Raylib.IsKeyPressed(k);
        if(HandleDecision()) return true;
        if(!Floor.FinalePending && Floor.Report is {} report && report.Serial>seenReport)
        {
            seenReport=report.Serial;
            OpenFloorPanel(FloorPanel.Debrief); return true;
        }
        if(floorPanel==FloorPanel.None)
        {
            if(Key(KeyboardKey.J)) OpenFloorPanel(FloorPanel.Contracts);
            else if(Key(KeyboardKey.U)) OpenFloorPanel(FloorPanel.Upgrades);
            else if(Key(KeyboardKey.H)) DrinkPocketCoffee();
            else if(Key(KeyboardKey.G)) DropBait();
            else if(Key(KeyboardKey.P)) SetHudBanner(FloorAction(PagerAction));
            return floorPanel!=FloorPanel.None;
        }
        if(Key(KeyboardKey.Escape) || floorPanel==FloorPanel.Contracts && Key(KeyboardKey.J) ||
            floorPanel==FloorPanel.Upgrades && Key(KeyboardKey.U))
        { OpenFloorPanel(FloorPanel.None); return true; }
        bool confirm=Key(KeyboardKey.Enter) || Key(KeyboardKey.E);
        if(floorPanel==FloorPanel.Debrief)
        {
            if(confirm) OpenFloorPanel(Key(KeyboardKey.Enter) ? FloorPanel.Contracts : FloorPanel.None);
            return true;
        }
        bool shop=floorPanel==FloorPanel.Upgrades;
        if(shop)
        {
            SkinSwitch(true);
            bool next=sidegrades;
            if(Key(KeyboardKey.Tab)) next=!next;
            if(Key(KeyboardKey.One)) next=false;
            if(Key(KeyboardKey.Two)) next=true;
            Vector2 p=(Raylib.GetMousePosition()-PanelOffset)/PanelScale;
            for(int tab=0;tab<2;tab++)
                if(Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(p,ArsenalTab(tab))) next=tab==1;
            if(next!=sidegrades) { sidegrades=next; floorRow=0; panelMessage=""; }
        }
        if(shop && sidegrades)
            for(int tier=0;tier<3;tier++)
                if(Key((KeyboardKey)((int)KeyboardKey.F1+tier)) || Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec((Raylib.GetMousePosition()-PanelOffset)/PanelScale,SupplyRect(tier)))
                { panelMessage=FloorAction(50+tier); return true; }
        if(shop && Key(KeyboardKey.G)) panelMessage=FloorAction(PagerAction);
        if(!shop && Key(KeyboardKey.R) && Floor.JobId>=0) panelMessage=FloorAction(RollbackAction);
        int count=shop ? (sidegrades ? FloorSystems.GearList.Length : FloorSystems.Arsenal.Length) : FloorSystems.Jobs.Length;
        int previousRow=floorRow;
        int column=floorRow%2;
        int rows=shop ? (count+1-column)/2 : count;
        int stride=shop ? 2 : 1;
        if(Key(KeyboardKey.Down) || Key(KeyboardKey.S)) floorRow=(floorRow+stride)%(rows*stride);
        if(Key(KeyboardKey.Up) || Key(KeyboardKey.W)) floorRow=(floorRow+rows*stride-stride)%(rows*stride);
        if(!shop && (Key(KeyboardKey.Right) || Key(KeyboardKey.D))) floorRow=(floorRow+1)%count;
        if(!shop && (Key(KeyboardKey.Left) || Key(KeyboardKey.A))) floorRow=(floorRow+count-1)%count;
        if(shop && (Key(KeyboardKey.Left) || Key(KeyboardKey.A))) floorRow-=column;
        if(shop && (Key(KeyboardKey.Right) || Key(KeyboardKey.D))) floorRow=Math.Min(count-1,floorRow+1-column);
        if(previousRow!=floorRow) panelMessage="";
        Vector2 mouse=(Raylib.GetMousePosition()-PanelOffset)/PanelScale;
        bool click=Raylib.IsMouseButtonPressed(MouseButton.Left);
        if(shop && click && Raylib.CheckCollisionPointRec(mouse,ShopPurchase)) confirm=true;
        for(int i=0;i<count;i++)
        {
            if(!shop && Key(KeyboardKey.One+i)) floorRow=i;
            if(Raylib.CheckCollisionPointRec(mouse,FloorRowRect(i)) && (click || shop && Raylib.GetMouseDelta()!=Vector2.Zero))
            {
                confirm|=click && (!shop || i==floorRow);
                if(floorRow!=i) panelMessage="";
                floorRow=i;
            }
        }
        if(confirm)
        {
            bool available=Floor.JobId<0;
            panelMessage=FloorAction(floorRow+(shop ? (sidegrades ? BuyOffset : WeaponBuyOffset) : 0));
            if(!shop && available && (Net.Guest || Floor.JobId>=0)) { SetHudBanner(panelMessage); OpenFloorPanel(FloorPanel.None); }
        }
        return true;
    }

    private static void TypeAt(string value,float x,float y,int size,Color color,bool title=false)
    {
        var font=GameFonts.Get(title ? Typeface.SerifBold : Typeface.Interface,size);
        GameFonts.Draw(font,value,new(x,y),size,color);
    }
    internal static AgentVerdict AgentOutcome => Floor.Verdict;
    internal static IReadOnlyDictionary<string,int> Discoveries => Floor.DiscoveredSpecies;

    internal static void ShadowText(Font font,string text,float x,float y,int size,Color ink)
    {
        text=Loc.RenderText(text);
        Font face=GameFonts.At(font,size);
        Vector2 pos=new(MathF.Round(x),MathF.Round(y));
        float spacing=size<=16 ? .5f : 0;
        Raylib.DrawTextEx(face,text,pos+new Vector2(1.5f),size,spacing,new Color(8,12,16,(int)(190*ink.A/255f)));
        Raylib.DrawTextEx(face,text,pos,size,spacing,ink);
    }

    private static readonly string[] BeatTitles=["РАЗГОВОР С HR","ДЖАБА-ЖАБЫ","БАГ-ТРЕКЕР","САЧОК","ЯБЛОБУК","СТАЖЁР","ХИЛ У КОФЕ-МАШИНЫ","ДОСТАВКА КАДРОВ","ОПЕНСПЕЙС"];
    private static void DrawFloorTelemetry()
    {
        int x=28, right=UiViewport.Width-28;
        var small=GameFonts.Get(Typeface.InterfaceBold,13);
        var display=GameFonts.Get(Typeface.SerifBold,28);
        Color mist=new(220,232,229,255), track=new(188,207,209,85), coral=new(255,115,91,255);
        void Gauge(string label,float value)
        {
            Color ink=value>=75 ? coral : value>=50 ? amber : mist;
            ShadowText(small,label,x,22,13,mist);
            string number=$"{(int)value:00}";
            ShadowText(_uiFont,number,x+52-GameFonts.Measure(_uiFont,number,15).X,20,15,ink);
            x+=75;
        }
        if(Floor.WingsUnlocked) { Gauge("ТД",Floor.TD); Gauge("ПРОД",Floor.PT); Gauge("СБ",Floor.SEC); }
        if(Floor.WingsUnlocked && Floor.StyleTime>0)
        {
            Color ink=Raylib.Fade(amber,Math.Min(1,Floor.StyleTime));
            ShadowText(display,Floor.RankIndex==4 ? "S" : Floor.Rank,x,13,28,ink);
            ShadowText(small,FormattableString.Invariant($"АВТОРИТЕТ  ×{Floor.Variety:0.0}"),x+40,25,13,ink);
        }
        string progress=Floor.JobId==0 ? $"Опенспейс {Floor.Seated}/2    ${Floor.Earned} / $450" : Floor.Progress;
        int seconds=(int)MathF.Ceiling(Floor.Remaining);
        string objective=Floor.JobId>=0 ? $"{seconds/60}:{seconds%60:00}    {Loc.Tr(progress)}" : progress;
        bool guiding=Floor.Onboarding!=OnboardingStep.Done;
        bool repairObjective=!guiding && _world.BrokenElevator;
        int beat=(int)Floor.Onboarding;
        string heading=guiding ? $"ШАГ {(beat<6 ? beat+1 : beat)}/8   {BeatTitles[beat]}" : "ЦЕЛЬ   ПРОРЫВ НА 2-Й ЭТАЖ";
        string detail=guiding ? Floor.Objective(_world.CarriedCoder!=null,_world.TotalCodersSoldToHr) :
            Floor.JobId>=0 ? Loc.Tr(objective) : $"[{(Floor.HasWeapon(WeaponType.SolderingIron)?"×":" ")}] Паяльник ($90)    [{(_world.HasElevatorKey?"×":" ")}] Ключ-карта СБ";
        ShadowText(_uiFont,heading,28,Floor.WingsUnlocked ? 54 : 22,16,paper);
        ShadowText(_uiFont,detail,28,Floor.WingsUnlocked ? 77 : 45,17,WorldLabelLayout.Ivory);
        string notice=Floor.NoticeTime>0 ? Floor.Notice :
            Floor.Ddos || Floor.FireIncident ? "Сбой B12. Перезапусти стойку" : Floor.Coffee>0 ? $"Эспрессо  {Floor.Coffee:F0} с" : "";
        if(notice.Length>0)
        {
            ShadowText(_uiFont,notice,28,Floor.WingsUnlocked ? 102 : 70,15,amber);
        }
        void Right(Font font,string value,int y,int size,Color ink) =>
            ShadowText(font,value,right-GameFonts.Measure(font,value,size).X,y,size,ink);
        if(Floor.Friday>0)
        {
            var hot=Raylib.ColorLerp(coral,new(255,182,151,255),.24f+.23f*MathF.Sin(Floor.Friday*5));
            ShadowText(GameFonts.Get(Typeface.SerifBold,19),"ПЯТНИЦА ×2.5",right-168,19,19,hot);
            Right(display,$"{Math.Ceiling(Floor.Friday):00}с",12,28,paper);
            Raylib.DrawRectangle(right-168,47,168,3,track);
            Raylib.DrawRectangle(right-168,47,(int)(168*Floor.Friday/45),3,hot);
            Right(_uiFont,$"Узел Опенспейса  {Floor.Defense:F0}%",56,15,paper);
        }
        else if(_world.GalleyDesks.Exists(d=>d.IsOccupied) || Floor.Verdict==AgentVerdict.Contained)
        {
            int staff=_world.GalleyDesks.Count(d=>d.IsOccupied);
            bool lead=_world.GalleyDesks.Exists(d=>d.IsOccupied && !d.IsSlackingInTwitter && d.Archetype==CoderArchetype.TeamLeadMeeter);
            Right(_uiFont,$"Опенспейс {staff}/4    ${_world.GalleyDesks.Sum(d=>_world.DeskRate(d,lead))+(Floor.Verdict==AgentVerdict.Contained ? 14 : 0):F0}/с",22,16,paper);
        }
        DrawBossTelemetry();
        StyleComboSystem.DrawHud(UiViewport.Width, UiViewport.Height);
        if(Floor.DiscoveryTime<=0) return;
        var species=VibeCoderCollection.All.FirstOrDefault(s=>s.Id==Floor.DiscoveryId);
        if(species==null) return;
        float fade = Math.Clamp(Floor.DiscoveryTime * 5, 0, 1);
        string title = species.Name;
        string note = (Floor.FirstDiscovery ? "Новый вид" : "Повторный захват") + $"    {Discoveries.Count}/{VibeCoderCollection.All.Length}";
        if (Floor.FirstDiscovery) note += $"    +${species.BonusPayout}";
        string rarity = VibeCoderCollection.Tag(species.Rarity);
        float width = Math.Max(GameFonts.Measure(display, title, 26).X,
            GameFonts.Measure(small, rarity + "    " + note, 15).X) + 36;
        string appraisal=species.IsFrog || species.Id=="ai_agent_v01" ? "" : FloorSystems.VibeCheck(species.Archetype);
        width=Math.Max(width,GameFonts.Measure(small,appraisal,15).X+36);
        float xToast = (UiViewport.Width - width) / 2;
        int top = _world.ElevatorBoss.IsActive ? 96 : 18;
        Raylib.DrawRectangleRec(new(xToast, top, width, appraisal.Length==0 ? 56 : 78), new Color(24, 22, 20, (int)(225 * fade)));
        DossierPrint.Text(rarity + "    " + note, xToast + 16, top + 5, 15, Raylib.Fade(amber, fade), Typeface.InterfaceBold);
        if(appraisal.Length>0) DossierPrint.Text(appraisal,xToast+16,top+54,15,Raylib.Fade(paper,fade));
        DossierPrint.Text(title, xToast + 16, top + 23, 26, Raylib.Fade(DossierPrint.Ivory, fade), Typeface.SerifBold);
    }

    private static void DrawPayroll()
    {
        var r = Floor.Report!;
        var job = FloorSystems.Jobs[r.Job];
        Color ink = LoreAndMenusUI.ThemeSepiaInk, muted = LoreAndMenusUI.ThemeSepiaMuted, ruleInk = LoreAndMenusUI.ThemeSepiaRule, red = LoreAndMenusUI.ThemeStampRed;
        LoreAndMenusUI.DrawManilaPaper(new(116, 26, 1048, 668));
        void Print(string value, float x, float y, int size, Color color, Typeface face = Typeface.Interface)
            => DossierPrint.Text(value, x, y, size, color, face);
        void Line(int y, Color color) => Raylib.DrawLine(178, y, 1028, y, color);
        Print("ЭНТРОПИК     Бухгалтерия первого этажа", 178, 96, 16, muted, Typeface.InterfaceBold);
        Print("Ведомость расчёта", 175, 134, 48, ink, Typeface.SerifBold);
        Print(job.Title, 178, 204, 24, ink, Typeface.InterfaceBold);
        Print(r.Success ? "Работа принята заказчиком" : "Контракт сорван", 178, 238, 18, muted);

        // Authentic angled crimson wet-ink rubber stamp completely clear of the top-right steel paperclip!
        LoreAndMenusUI.DrawAngledGradeStamp(r.Rank, "АВТОРИТЕТ", 818, 172, -6.5f, red);

        Line(280, ink);
        Line(284, ruleInk);
        Print(r.Success ? "К ВЫПЛАТЕ" : "К УДЕРЖАНИЮ", 178, 304, 14, red, Typeface.InterfaceBold);
        Print($"{(r.Success ? "+" : "-")}${Math.Abs(r.Amount)}", 175, 326, 64, red, Typeface.SerifBold);
        string basis = r.Success ? $"Ставка ${job.Base}   ×   риск {r.Greed:0.00}   ×   бонус {r.Bonus:0.00}"
            : "Штраф $350 и половина стоимости доставленных кадров";
        Print(basis, 178, 428, 18, ink);
        void Stat(int y, string label, string value)
        {
            Print(label, 706, y, 18, muted);
            float w = GameFonts.Measure(GameFonts.Get(Typeface.InterfaceBold, 22), value, 22).X;
            Print(value, 1026 - w, y - 3, 22, ink, Typeface.InterfaceBold);
            Raylib.DrawLine(706, y + 36, 1028, y + 36, ruleInk);
        }
        Stat(318, "Доставлено кадров", r.Captured.ToString());
        Stat(380, "Пиковый риск", r.Peak.ToString("0.0"));
        Line(478, ruleInk);
        Print("ЗАПИСЬ ДИСПЕТЧЕРА", 178, 498, 14, red, Typeface.InterfaceBold);
        Print(r.Diagnosis, 178, 524, 23, ink);
        Line(586, ruleInk);
        Print("[Enter]  Следующий контракт       [E]  Вернуться на этаж", 178, 606, 18, ink, Typeface.InterfaceBold);
    }

    private static readonly string[] UpgradeModels=["weapon_shotgun_90s","weapon_shotgun_90s","weapon_solder_90s",
        "weapon_uzi_90s","weapon_belt_90s","pager","weapon_solder_90s","prop_xerox_printer",
        "signet","prop_extinguisher","weapon_belt_90s","weapon_net_90s","weapon_net_90s"];

    private static void DrawFloorPanel()
    {
        if(floorPanel==FloorPanel.Upgrades) { DrawBriefcase(); return; }
        Raylib.DrawRectangle(0,0,Raylib.GetScreenWidth(),Raylib.GetScreenHeight(),new Color(18,11,8,255));
        Raylib.BeginMode2D(new Camera2D { Zoom=PanelScale, Offset=PanelOffset });
        if (floorPanel==FloorPanel.BossFinale) { LoreAndMenusUI.DrawDeskBackdrop(1280, 720); DrawDecision(); }
        else if (floorPanel == FloorPanel.Debrief && Floor.Report != null) { LoreAndMenusUI.DrawDeskBackdrop(1280, 720); DrawPayroll(); }
        else
        {
            // REAL WOODEN-FRAMED OFFICE CORKBOARD WITH 5 TILTED PINNED PAPER CONTRACT NOTES (ZERO straight table rows!)
            LoreAndMenusUI.DrawCorkboard(1280, 720);
            // Engrave title squarely centered between the rivets on the brushed-brass plaque in top-left of corkboard!
            DossierPrint.Text("Доска контрактов", 122, 97, 21, LoreAndMenusUI.ThemeSepiaInk, Typeface.SerifBold);
            string subLine = Floor.JobId >= 0 ? FloorSystems.Jobs[Floor.JobId].Title + "  ·  " + Loc.Tr(Floor.Progress) : "Срок отсчитывается с момента подписи";
            GameFonts.Draw(GameFonts.Get(Typeface.InterfaceBold, 17), subLine, new(374, 98), 17, new Color(246, 234, 206, 245));

            float[] noteAngles = [-2.4f, 1.7f, -1.5f, 2.1f, -1.9f];
            for (int i = 0; i < FloorSystems.Jobs.Length; i++)
            {
                var job = FloorSystems.Jobs[i];
                Rectangle card = FloorRowRect(i);
                bool selected = i == floorRow;
                bool activeJob = i == Floor.JobId;
                float ang = noteAngles[i % noteAngles.Length];

                // Draw real torn-edge fibrous pinned paper note with 3D red pushpin!
                LoreAndMenusUI.DrawContractNote(card, ang, selected);

                // Rotate text strictly with the tilted paper note around its center so ink lives ON the paper!
                float scale = selected ? 1.035f : 1.0f;
                Rlgl.PushMatrix();
                Rlgl.Translatef(card.X + card.Width * 0.5f, card.Y + card.Height * 0.5f, 0);
                Rlgl.Rotatef(ang, 0, 0, 1);
                Rlgl.Scalef(scale, scale, 1);
                float lx = -card.Width * 0.5f + 28, ty = -card.Height * 0.5f + 38;
                float maxW = card.Width - 56;
                Color ink = LoreAndMenusUI.ThemeSepiaInk;
                Color muted = LoreAndMenusUI.ThemeSepiaMuted;
                Color red = LoreAndMenusUI.ThemeStampRed;

                // Top contract number & red active/selected stamp
                DossierPrint.Text($"ЗАКАЗ №0{i + 1}", lx, ty, 12, muted, Typeface.InterfaceBold);
                if (selected || activeJob)
                {
                    string tag = activeJob ? "В РАБОТЕ" : "ВЫБРАНО";
                    float tw = GameFonts.Measure(GameFonts.Get(Typeface.InterfaceBold, 11), tag, 11).X + 14;
                    Rlgl.PushMatrix();
                    Rlgl.Translatef(lx + maxW - tw * 0.5f, ty + 6, 0);
                    Rlgl.Rotatef(-5.5f, 0, 0, 1);
                    Raylib.DrawRectangleLinesEx(new(-tw * 0.5f, -10, tw, 20), 1.8f, red);
                    DossierPrint.Text(tag, -tw * 0.5f + 7, -6, 11, red, Typeface.InterfaceBold);
                    Rlgl.PopMatrix();
                }

                // Contract title + wrapped objective description
                Font tf = GameFonts.Get(Typeface.SerifBold, 20);
                string title = Loc.Tr(job.Title);
                DossierPrint.Text(title, lx, ty + 20, 20, selected ? red : ink, Typeface.SerifBold);
                Raylib.DrawLine((int)lx, (int)(ty + 48), (int)(lx + maxW), (int)(ty + 48), LoreAndMenusUI.ThemeSepiaRule);

                // Word-wrap objective cleanly inside the pinned note width
                Font bf = GameFonts.Get(Typeface.Interface, 15);
                string[] words = Loc.Tr(job.Objective).Split(' ');
                string cur = "";
                float py = ty + 56;
                foreach (var w in words)
                {
                    string next = cur.Length == 0 ? w : cur + " " + w;
                    if (cur.Length > 0 && GameFonts.Measure(bf, next, 15).X > maxW)
                    {
                        DossierPrint.Text(cur, lx, py, 15, ink);
                        py += 20;
                        cur = w;
                    }
                    else cur = next;
                }
                if (cur.Length > 0) DossierPrint.Text(cur, lx, py, 15, ink);

                // Bottom bounty payout & deadline stamped cleanly on the paper bottom!
                float footY = card.Height * 0.5f - 46;
                Raylib.DrawLine((int)lx, (int)(footY - 8), (int)(lx + maxW), (int)(footY - 8), LoreAndMenusUI.ThemeSepiaRule);
                DossierPrint.Text($"${job.Base}", lx, footY, 26, red, Typeface.SerifBold);
                string dur = $"{job.Seconds} с";
                float dw = GameFonts.Measure(GameFonts.Get(Typeface.InterfaceBold, 17), dur, 17).X;
                DossierPrint.Text(dur, lx + maxW - dw, footY + 6, 17, ink, Typeface.InterfaceBold);
                Rlgl.PopMatrix();
            }

            // Write compact controls right onto the curled yellow sticky note in the bottom-right corner of the corkboard!
            DossierPrint.Text("[W/A/S/D]", 1094, 548, 13, LoreAndMenusUI.ThemeSepiaInk, Typeface.InterfaceBold);
            DossierPrint.Text("Выбор", 1094, 565, 13, LoreAndMenusUI.ThemeSepiaMuted);
            DossierPrint.Text("[Enter]", 1094, 588, 13, LoreAndMenusUI.ThemeStampRed, Typeface.InterfaceBold);
            DossierPrint.Text("Подписать", 1094, 605, 13, LoreAndMenusUI.ThemeSepiaInk);
            DossierPrint.Text("[Esc] Выход", 1094, 632, 12, LoreAndMenusUI.ThemeSepiaMuted, Typeface.InterfaceBold);

            if (panelMessage.Length > 0 || Floor.JobId >= 0)
            {
                string hint = panelMessage.Length > 0 ? panelMessage : "[R]  Откатить релиз: -18 ПРОД, -22 с";
                GameFonts.Draw(GameFonts.Get(Typeface.InterfaceBold, 16), hint, new(94, 650), 16, new Color(246, 234, 206, 240));
            }
        }
        Raylib.EndMode2D();
    }
}
