#nullable enable
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Raylib_cs;

namespace HowToVibeCoder;

internal enum FloorPanel { None, Contracts, Upgrades, Debrief }
internal enum Incident { Printer, Coffee, Bugs, Audit, Ddos, Burnout, Raid, Fire }
internal enum Sidegrade { Salt, Negotiator, Tape, Drum, DirectorBelt, Pager }
internal enum StyleAction { Belt, Iron, Uzi, Shotgun, Beam, Parry, Water, Capture }
internal record Job(string Title, string Objective, int Seconds, int Base);
internal record Gear(string Title, string Detail, int Price);
internal record Settlement(int Serial, int Job, bool Success, int Amount, float Greed, float Bonus,
    int Captured, float Peak, string Rank, string Diagnosis);

internal sealed class WaterSpill
{
    public Vector3 Position;
    public float Wet, Electric;
}

internal sealed class FloorSystems
{
    internal static readonly Job[] Jobs = [
        new("Срочный MVP до обеда","Посадить двоих на Галеру и заработать $450",150,650),
        new("Тихий хантинг Сеньора","Доставить Архитектора живым при СБ ниже 45",120,980),
        new("Финтех-спринт без СБ","Доставить Крипто-Бро или заработать $900",100,820),
        new("Релиз в Пятницу","Нажать DEPLOY и защищать Галеру 45 секунд",110,1200),
        new("Зачистка лифта","Ликвидировать ИИ-агента до блокировки этажа",180,1500)
    ];
    internal static readonly Gear[] GearList = [
        new("Соль-дробь","Срывает выдержку и оглушает. Урон по броне снижен на 55%.",210),
        new("Картечь «Переговорщик»","На 40% больше урона по броне агента. Смертельна для кадров.",340),
        new("Синяя изолента MK2","На 45% сильнее удар по эго. Жало быстрее греется и остывает.",260),
        new("Барабан Узи","50 патронов. Перезарядка на 30% дольше.",320),
        new("Ремень «Директорский»","Цепляет соседние цели. Шире окно отражения, короче оглушение.",250),
        new("Пейджер Крыши","Один вызов [G] отзывает рейд и снимает 35 внимания СБ.",180)
    ];

    internal static readonly WeaponType[] WeaponOrder = [WeaponType.BareHands, WeaponType.CaptureNet,
        WeaponType.Belt, WeaponType.SolderingIron, WeaponType.MicroUzi, WeaponType.SawedOff];
    internal static readonly Gear[] Weapons = [
        new("Сачок-ловушка","Широкий захват прыгающих багов и пугливых стажёров",3),
        new("Ремень 90-х","Ломает выдержку вайб-кодеров для NDA и Галеры",25),
        new("Паяльник 90-х","Пробивает эго и запускает электролужи с кулером",90),
        new("Микро-Узи 90-х","Скорострельный огнестрел против боевых кодеров и рейдов",240),
        new("Обрез 12-го калибра","Тяжёлый калибр для элиты, Пятницы и ИИ-босса",480)
    ];
    internal static readonly int ClassicMask=WeaponOrder.Aggregate(0,(mask,w)=>mask | (1<<(int)w));
    public int OwnedWeaponsMask=1<<(int)WeaponType.BareHands;
    public List<string> PocketFrogs=new();
    internal bool HasWeapon(WeaponType w) => (OwnedWeaponsMask & (1<<(int)w))!=0;
    internal string BuyWeapon(int row,Func<int,bool> spend)
    {
        var weapon=WeaponOrder[row+1];
        var gear=Weapons[row];
        if(HasWeapon(weapon)) return gear.Title+" уже в арсенале";
        if(!spend(gear.Price)) return $"Для покупки нужно ${gear.Price}";
        OwnedWeaponsMask |= 1<<(int)weapon;
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

    internal void Discover(VibeCoderSpecies species,Action<int,string> grant)
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
    internal float GreedMultiplier => 1+R/100;
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
        TD=Math.Clamp(TD+debt,0,100);
        PT=Math.Clamp(PT+heat,0,100);
        SEC=Math.Clamp(SEC+security,0,100);
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
        Authority=Math.Clamp(Authority+(same && action!=StyleAction.Capture ? -2 : points*Variety),0,125);
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
        JobId=id;
        Remaining=Jobs[id].Seconds;
        Earned=Seated=Captured=StaffValue=0;
        StealthBlown=SeniorDelivered=CryptoDelivered=ReleaseStarted=ReleaseFinished=CrewLost=false;
        Peak=R;
        Risk(debt:id is 0 or 2 ? 3 : 1,heat:2);
        if(id==1) world.EnsureCandidate(CoderArchetype.SeniorArchitect);
        if(id==2) world.EnsureCandidate(CoderArchetype.CryptoBro);
        if(id==4 && !world.ElevatorBoss.IsActive)
        {
            world.ElevatorBoss.IsDefeated=false;
            world.TrySummonElevatorBoss(_=>true,out _);
        }
        return Jobs[id].Objective;
    }

    internal string Deploy(SkyscraperFloor1World world)
    {
        if(Friday>0) return $"Деплой уже идёт: {MathF.Ceiling(Friday)} с";
        CrewLost=false;
        Friday=45;
        NextWave=8;
        Defense=100;
        ReleaseStarted=JobId==3;
        Risk(heat:30);
        world.SpawnWave(2);
        AudioMusicSystem.PlayBossSiren();
        return "Деплой начался. Удерживай узел Галеры 45 секунд.";
    }

    internal string Buy(int index,Func<int,bool> spend)
    {
        if(index<0 || index>=GearList.Length) return "";
        var id=(Sidegrade)index;
        if(id==Sidegrade.Pager && Has(id)) return "Пейджер заряжен. Вызов на [G]";
        if(!Has(id))
        {
            if(!spend(GearList[index].Price)) return "В общаке не хватает денег";
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
        Raid=false; Audit=0;
        world.ActiveCoders.RemoveAll(c=>c.RaidGuard && c.State!=CoderAIState.Subdued);
        Risk(security:-35);
        return "Крыша отозвала наряд. СБ -35";
    }

    internal string Print()
    {
        if(Jam>0) return $"Замятие бумаги. Ремонт через {MathF.Ceiling(Jam)} с";
        if(PrinterWait>0) return $"Принтер остывает: {MathF.Ceiling(PrinterWait)} с";
        PrinterWait=35; Badge=30; Audit=0;
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

    internal void Overheat(SkyscraperFloor1World world)
    {
        Risk(heat:RackHeat<=0 ? 12 : 2);
        RackHeat=18;
        foreach(var c in world.ActiveCoders.Where(c=>Vector3.DistanceSquared(c.Position,world.RackPos)<64 && c.State!=CoderAIState.Subdued))
        {
            c.Pressure(24,0,20);
            if(c.State!=CoderAIState.Subdued) { c.State=CoderAIState.PanickedFleeing; c.Windup=0; }
        }
    }

    internal void RestoreMorale(SkyscraperFloor1World world)
    {
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
                WantsCoffee=true; message="Галера требует раф. Кофемашина у приёмной.";
                foreach(var d in world.GalleyDesks) if(d.IsOccupied) d.Morale=.65f;
                break;
            case Incident.Bugs: world.SpawnWave(2); message="Выброс багов. Дежурная смена идёт в зал."; break;
            case Incident.Audit: Audit=Badge>0 ? 0 : 15; message=Audit>0 ? "Аудит через 15 с. Напечатай пропуск или внеси $180 у приёмной." : "Аудитор принял свежий пропуск."; break;
            case Incident.Ddos: Ddos=true; message="DDoS: выход Галеры упал вдвое. Перезапусти B12."; break;
            case Incident.Burnout:
                var worker=world.GalleyDesks.Where(d=>d.IsOccupied).MaxBy(d=>d.Burnout);
                if(worker!=null) { worker.Burnout=100; worker.IsSlackingInTwitter=true; }
                message="Кадр выгорел. Кофе или ремень вернут его в смену."; break;
            case Incident.Raid: Raid=true; world.SpawnWave(3,raid:true); message="Рейд СБ. Крыша на связи по пейджеру."; break;
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
        Badge=Down(Badge); Coffee=Down(Coffee); PagerWait=Down(PagerWait);
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
        if(Audit>0 && (Audit=Down(Audit))==0) { Risk(security:22); Announce("Аудит нашёл расхождения. СБ +22"); }
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
        if((NextIncident-=dt)<=0)
        {
            List<Incident> options=[Incident.Printer,Incident.Bugs,Incident.Audit];
            if(staff>0) { options.Add(Incident.Coffee); options.Add(Incident.Ddos); if(R>=40) options.Add(Incident.Burnout); }
            if(SEC>=55 && !Raid) options.Add(Incident.Raid);
            if(PT>=60 && !FireIncident) options.Add(Incident.Fire);
            options.Remove((Incident)LastIncident);
            IncidentNow(options[Random.Shared.Next(options.Count)],world);
            NextIncident=65-.20f*R+Random.Shared.NextSingle()*5;
        }
        if(JobId<0) return;
        Remaining=Down(Remaining);
        string? failure=CrewLost ? "Смена потеряла бойца." : StealthBlown ? "СБ засекла вербовку Сеньора." :
            JobId==3 && ReleaseStarted && Defense<=0 ? "Налётчики разбили узел Галеры." :
            JobId==4 && R>=90 ? "СБ заблокировала лифтовую шахту." : null;
        bool success=JobId switch { 0=>Seated>=2 && Earned>=450,1=>SeniorDelivered,2=>CryptoDelivered || Earned>=900,
            3=>ReleaseFinished,4=>world.ElevatorBoss.IsDefeated,_=>false };
        if(failure==null && !success && Remaining>0) return;
        success &= failure==null;
        string[] verdicts=["Два новых кадра отбили аванс до обеда.","Архитектор подписал NDA до того, как СБ подняла журнал.",
            CryptoDelivered ? "Крипто-Бро подписал оффер до закрытия торгов." : "Выручка закрыла спринт до конца торгов.",
            "Галера пережила пятничную смену.","Шахта снова под контролем диспетчера."];
        float bonus=(1+.06f*RankIndex)*(JobId==2 ? 1.4f : 1)*(JobId==3 ? 2.5f : FridayMultiplier);
        if(JobId==2 && world.GalleyDesks.Exists(d=>d.IsOccupied && d.Archetype==CoderArchetype.CryptoBro)) bonus*=1.5f;
        int amount=success ? (int)MathF.Round(Jobs[JobId].Base*GreedMultiplier*bonus) : -(350+StaffValue/2);
        Report=new(++Serial,JobId,success,amount,GreedMultiplier,bonus,Captured,Peak,Rank,
            failure ?? (success ? verdicts[JobId] : "Дедлайн прошёл раньше последней сдачи."));
        JobId=-1;
        pay?.Invoke(amount,"Расчёт по контракту");
    }

    internal string Progress => JobId switch {
        0=>$"Галера {Seated}/2    Выручка ${Earned}/450",1=>$"Доставить Архитектора    СБ {(int)SEC}/45",
        2=>$"Доставить Крипто-Бро или заработать ${Earned}/900",3=>ReleaseStarted ? $"Удержать Галеру    Узел {Defense:F0}%" : "Нажать красную кнопку у лифта",
        4=>$"Ликвидировать агента    Корпус {MathF.Ceiling(100f*AgentHealth)}%",_=>"Заказы [J]    Арсенал [U]" };
    public float AgentHealth=1;
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
            ApplyProgression(null);
            _weapons.Equip((WeaponType)Math.Clamp(data.CurrentWeaponSlot+1,1,4));
            foreach(var desk in _world.GalleyDesks.Take(Math.Clamp(data.SeatedGalleyCoders,0,4)))
            { desk.IsOccupied=true; desk.CoderName="Дежурный кодер"; }
            return;
        }
        _world.Systems=systems; seenReport=systems.Serial;
        ApplyProgression(state);
        _weapons.Equip(state.ProgressionVersion>=1 ? state.Equipped : (WeaponType)Math.Clamp(data.CurrentWeaponSlot+1,1,4));
        _world.ElevatorBoss=state.Boss; _world.ElevatorDoorOpenAmount=state.Door;
        _world.SelectedBaitTier=state.Bait; _world.IsMacBookTrapActive=state.Trap; _world.TotalCodersSoldToHr=state.Sold;
        _world.ActiveCoders.Clear(); _world.ActiveCoders.AddRange(state.Coders);
        if(state.Carried!=null) _world.ActiveCoders.Add(state.Carried);
        _world.CarriedCoder=state.HostCarried;
        _world.GalleyDesks.Clear(); _world.GalleyDesks.AddRange(state.Desks);
        _world.EnemyProjectiles.Clear(); _world.EnemyProjectiles.AddRange(state.Projectiles);
        _world.ResumeCoderIds();
    }
    private static FloorPanel floorPanel;
    private static bool sidegrades;
    private static int floorRow, seenReport;
    private static string panelMessage="";
    private static readonly Color paper=new(234,227,201,255), amber=new(232,192,110,255),
        carbon=new(19,25,28,255), quiet=new(161,174,171,255), rule=new(74,88,87,255);
    private static FloorSystems Floor => _world.Systems;

    private static void OpenFloorPanel(FloorPanel panel)
    {
        floorPanel=panel; floorRow=0; sidegrades=false; panelMessage="";
        if(panel==FloorPanel.None) Raylib.DisableCursor();
        else Raylib.EnableCursor();
    }

    private const int WeaponBuyOffset=30, BuyOffset=10, PagerAction=20, BaitAction=21, RollbackAction=22;
    private static string ApplyFloorAction(int index)
    {
        if(index>=WeaponBuyOffset && index<WeaponBuyOffset+FloorSystems.Weapons.Length)
        {
            var result=Floor.BuyWeapon(index-WeaponBuyOffset,TrySpendCash);
            SetHudBanner(result);
            return result;
        }
        if(index>=0 && index<FloorSystems.Jobs.Length) return Floor.Start(index,_world);
        if(index>=BuyOffset && index<BuyOffset+FloorSystems.GearList.Length) return Floor.Buy(index-BuyOffset,TrySpendCash);
        if(index==PagerAction) return Floor.UsePager(_world);
        if(index==BaitAction)
        {
            _world.SelectedBaitTier=(SubscriptionBaitTier)(((int)_world.SelectedBaitTier+1)%4);
            return SkyscraperFloor1World.GetBaitInfo(_world.SelectedBaitTier).Description;
        }
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
    private static Rectangle ArsenalTab(int i) => new(105+i*355,164,350,35);
    private static Rectangle FloorRowRect(int i) => new(102,204+55*i,1076,51);

    private static bool HandleFloorPanel()
    {
        bool Key(KeyboardKey k) => Raylib.IsKeyPressed(k);
        if(Floor.Report is {} report && report.Serial>seenReport)
        {
            seenReport=report.Serial;
            OpenFloorPanel(FloorPanel.Debrief); return true;
        }
        if(floorPanel==FloorPanel.None)
        {
            if(Key(KeyboardKey.J)) OpenFloorPanel(FloorPanel.Contracts);
            else if(Key(KeyboardKey.U)) OpenFloorPanel(FloorPanel.Upgrades);
            else if(Key(KeyboardKey.G)) SetHudBanner(FloorAction(PagerAction));
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
            bool next=sidegrades;
            if(Key(KeyboardKey.Tab) || Key(KeyboardKey.Left) || Key(KeyboardKey.Right)) next=!next;
            if(Key(KeyboardKey.One)) next=false;
            if(Key(KeyboardKey.Two)) next=true;
            Vector2 p=(Raylib.GetMousePosition()-PanelOffset)/PanelScale;
            for(int tab=0;tab<2;tab++)
                if(Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(p,ArsenalTab(tab))) next=tab==1;
            if(next!=sidegrades) { sidegrades=next; floorRow=0; panelMessage=""; }
        }
        if(shop && Key(KeyboardKey.B)) panelMessage=FloorAction(BaitAction);
        if(shop && Key(KeyboardKey.G)) panelMessage=FloorAction(PagerAction);
        if(!shop && Key(KeyboardKey.R) && Floor.JobId>=0) panelMessage=FloorAction(RollbackAction);
        int count=shop ? (sidegrades ? FloorSystems.GearList.Length : FloorSystems.Weapons.Length) : FloorSystems.Jobs.Length;
        if(Key(KeyboardKey.Down) || Key(KeyboardKey.S)) floorRow=(floorRow+1)%count;
        if(Key(KeyboardKey.Up) || Key(KeyboardKey.W)) floorRow=(floorRow+count-1)%count;
        Vector2 mouse=(Raylib.GetMousePosition()-PanelOffset)/PanelScale;
        for(int i=0;i<count;i++)
        {
            if(!shop && Key(KeyboardKey.One+i)) floorRow=i;
            if(Raylib.CheckCollisionPointRec(mouse,FloorRowRect(i)) && Raylib.IsMouseButtonPressed(MouseButton.Left))
            { floorRow=i; confirm=true; }
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
    internal static IReadOnlyDictionary<string,int> Discoveries => Floor.DiscoveredSpecies;

    internal static void ShadowText(Font font,string text,float x,float y,int size,Color ink)
    {
        Font face=GameFonts.At(font,size);
        Vector2 pos=new(MathF.Round(x),MathF.Round(y));
        float spacing=size<=16 ? .5f : 0;
        Raylib.DrawTextEx(face,text,pos+new Vector2(1.5f),size,spacing,new Color(8,12,16,(int)(190*ink.A/255f)));
        Raylib.DrawTextEx(face,text,pos,size,spacing,ink);
    }

    private static void DrawFloorTelemetry()
    {
        int x=28, right=Raylib.GetScreenWidth()-28;
        var small=GameFonts.Get(Typeface.InterfaceBold,13);
        var display=GameFonts.Get(Typeface.SerifBold,28);
        Color mist=new(220,232,229,255), track=new(188,207,209,85), coral=new(255,115,91,255);
        void Gauge(string label,float value)
        {
            Color ink=value>=75 ? coral : value>=50 ? amber : mist;
            ShadowText(small,label,x,22,13,mist);
            string number=$"{(int)value:00}";
            ShadowText(_uiFont,number,x+52-GameFonts.Measure(_uiFont,number,15).X,20,15,ink);
            Raylib.DrawRectangle(x,43,52,3,track);
            Raylib.DrawRectangle(x,43,(int)MathF.Ceiling(52*value/100),3,ink);
            x+=75;
        }
        Gauge("ТД",Floor.TD); Gauge("ПРОД",Floor.PT); Gauge("СБ",Floor.SEC);
        if(Floor.StyleTime>0)
        {
            Color ink=Raylib.Fade(amber,Math.Min(1,Floor.StyleTime));
            ShadowText(display,Floor.RankIndex==4 ? "S" : Floor.Rank,x,13,28,ink);
            Raylib.DrawLine(x+31,23,x+31,42,ink);
            ShadowText(small,FormattableString.Invariant($"АВТОРИТЕТ  ×{Floor.Variety:0.0}"),x+40,25,13,ink);
        }
        string progress=Floor.JobId==0 ? $"Галера {Floor.Seated}/2    ${Floor.Earned} / $450" : Floor.Progress;
        int seconds=(int)MathF.Ceiling(Floor.Remaining);
        string objective=Floor.JobId>=0 ? $"{seconds/60}:{seconds%60:00}    {progress}" : progress;
        ShadowText(_uiFont,objective,28,54,16,paper);
        string notice=Floor.NoticeTime>0 ? Floor.Notice : Floor.Audit>0 ? $"Аудит через {Floor.Audit:F0} с" :
            Floor.Ddos || Floor.FireIncident ? "Сбой B12. Перезапусти стойку" : Floor.Coffee>0 ? $"Эспрессо  {Floor.Coffee:F0} с" : "";
        if(notice.Length>0)
        {
            Raylib.DrawRectanglePro(new(31,88,5,5),new(2.5f),45,amber);
            ShadowText(_uiFont,notice,42,78,15,amber);
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
            Right(_uiFont,$"Узел Галеры  {Floor.Defense:F0}%",56,15,paper);
        }
        else if(_world.GalleyDesks.Exists(d=>d.IsOccupied))
        {
            int staff=_world.GalleyDesks.Count(d=>d.IsOccupied);
            bool lead=_world.GalleyDesks.Exists(d=>d.IsOccupied && !d.IsSlackingInTwitter && d.Archetype==CoderArchetype.TeamLeadMeeter);
            Right(_uiFont,$"Галера {staff}/4    ${_world.GalleyDesks.Sum(d=>_world.DeskRate(d,lead)):F0}/с",22,16,paper);
        }
        if(Floor.DiscoveryTime<=0) return;
        var species=VibeCoderCollection.All.FirstOrDefault(s=>s.Id==Floor.DiscoveryId);
        if(species==null) return;
        float fade=Math.Clamp(Math.Min((4-Floor.DiscoveryTime)*5,Floor.DiscoveryTime*2),0,1);
        float center=Raylib.GetScreenWidth()*.5f, y=(center<525 ? 115 : 34)+(1-fade)*6;
        Color accent=Raylib.Fade(VibeCoderCollection.Ink(species.Rarity),fade);
        void Center(Font font,string line,float top,int size,Color color) =>
            ShadowText(font,line,center-GameFonts.Measure(font,line,size).X*.5f,top,size,color);
        Raylib.DrawLineEx(new(center-38,y-7),new(center+38,y-7),1,accent);
        Center(small,(Floor.FirstDiscovery ? "НОВЫЙ В КОЛЛЕКЦИИ" : "ПОВТОРНЫЙ ЗАХВАТ")+"    "+VibeCoderCollection.Tag(species.Rarity),y,13,accent);
        Center(display,species.Name,y+18,25,Raylib.Fade(paper,fade));
        Center(small,$"{Discoveries.Count} / {VibeCoderCollection.All.Length}"+(Floor.FirstDiscovery ? $"    +${species.BonusPayout}" : ""),y+50,14,accent);
    }

    private static void DrawFloorPanel()
    {
        Raylib.DrawRectangle(0,0,Raylib.GetScreenWidth(),Raylib.GetScreenHeight(),new Color(10,16,18,218));
        Raylib.BeginMode2D(new Camera2D { Zoom=PanelScale, Offset=PanelOffset });
        Raylib.DrawRectangle(76,52,1128,615,carbon);
        Raylib.DrawRectangleLinesEx(new(76,52,1128,615),1,rule);
        foreach(int x in new[]{76,1203}) foreach(int y in new[]{52,666})
        {
            Raylib.DrawLine(x,y,x+(x==76 ? 15 : -15),y,paper);
            Raylib.DrawLine(x,y,x,y+(y==52 ? 15 : -15),paper);
        }
        bool report=floorPanel==FloorPanel.Debrief,shop=floorPanel==FloorPanel.Upgrades;
        Raylib.DrawRectangle(102,78,1076,45,amber);
        TypeAt(report ? "ВЕДОМОСТЬ РАСЧЁТА" : shop ? "АРСЕНАЛ СМЕНЫ" : "ДОСКА КОНТРАКТОВ",116,83,26,carbon,true);
        TypeAt("БЕЛАЯ АКУЛА IT  /  ЭТАЖ 01",956,92,15,carbon);
        if(report && Floor.Report is {} r)
        {
            var job=FloorSystems.Jobs[r.Job];
            TypeAt(job.Title,105,149,39,paper,true);
            TypeAt(r.Success ? "Заказчик принял работу" : "Контракт сорван",107,204,21,quiet);
            Raylib.DrawLine(105,251,1176,251,rule);
            TypeAt(r.Success ? "ВЫПЛАТА СМЕНЕ" : "НАЧИСЛЕН ШТРАФ",106,278,17,quiet);
            TypeAt($"{(r.Success ? "+" : "-")}${Math.Abs(r.Amount)}",104,306,61,amber,true);
            string basis=r.Success ? $"База ${job.Base}   ×   {r.Greed:0.00} риск   ×   {r.Bonus:0.00} бонус" :
                "Базовый штраф $350 и половина стоимости доставленных кадров";
            TypeAt(basis,107,397,19,paper);
            void Stat(int y,string label,string value)
            {
                TypeAt(label,751,y,19,quiet); TypeAt(value,1030,y-5,24,paper,true);
                Raylib.DrawLine(751,y+35,1167,y+35,rule);
            }
            Stat(277,"Ранг авторитета",r.Rank); Stat(339,"Доставлено кадров",r.Captured.ToString());
            Stat(401,"Пиковый риск",r.Peak.ToString("0.0"));
            Raylib.DrawLine(105,476,1176,476,rule);
            TypeAt("ЗАПИСЬ ДИСПЕТЧЕРА",106,497,16,quiet);
            TypeAt(r.Diagnosis,106,524,24,paper);
            TypeAt("[Enter]  Следующий контракт       [E]  Вернуться на этаж",107,607,19,amber);
        }
        else
        {
            string info=shop ? $"Общак  ${_playerCash}     /     Приманка: {SkyscraperFloor1World.GetBaitInfo(_world.SelectedBaitTier).Name}" :
                Floor.JobId>=0 ? FloorSystems.Jobs[Floor.JobId].Title+"    /    "+Floor.Progress : "Срок отсчитывается с момента подписи";
            TypeAt(info,105,shop ? 133 : 149,shop ? 18 : 22,paper);
            if(shop)
                for(int tab=0;tab<2;tab++)
                {
                    var tabRect=ArsenalTab(tab);
                    var ink=sidegrades==(tab==1) ? amber : quiet;
                    TypeAt(tab==0 ? "[1] ОРУЖИЕ (ЦЕПОЧКА)" : "[2] АПГРЕЙДЫ И ПРИМАНКИ",tabRect.X,tabRect.Y+4,17,ink);
                    if(sidegrades==(tab==1)) Raylib.DrawLineEx(new(tabRect.X,tabRect.Y+32),new(tabRect.X+286,tabRect.Y+32),2,ink);
                }
            int count=shop ? (sidegrades ? FloorSystems.GearList.Length : FloorSystems.Weapons.Length) : FloorSystems.Jobs.Length;
            for(int i=0;i<count;i++)
            {
                Rectangle row=FloorRowRect(i);
                bool selected=i==floorRow;
                if(selected) Raylib.DrawRectangleRec(row,paper);
                else Raylib.DrawLine((int)row.X,(int)row.Y+50,1178,(int)row.Y+50,rule);
                Color ink=selected ? carbon : paper;
                var gear=shop ? (sidegrades ? FloorSystems.GearList[i] : FloorSystems.Weapons[i]) : null;
                var job=shop ? null : FloorSystems.Jobs[i];
                TypeAt(gear?.Title ?? job!.Title,row.X+12,row.Y+11,21,ink,true);
                TypeAt(gear?.Detail ?? job!.Objective,row.X+342,row.Y+15,16,selected ? carbon : quiet);
                string value=shop && !sidegrades ? (Floor.HasWeapon(FloorSystems.WeaponOrder[i+1]) ? "Куплено" : $"${gear!.Price}") : shop ? Floor.Has((Sidegrade)i) ?
                    (i<2 && Floor.Shell!=(Sidegrade)i ? "На складе" : "Снаряжено") : $"${gear!.Price}" : $"{job!.Seconds} с    ${job.Base}";
                TypeAt(value,1041,row.Y+14,17,ink);
            }
            string hint=shop ? "[B]  Сменить приманку      [G]  Вызвать Крышу" :
                Floor.JobId>=0 ? "[R]  Откатить релиз: -18 ПРОД, -22 с" : "Расчёт по текущему риску и рангу авторитета";
            TypeAt(panelMessage.Length>0 ? panelMessage : hint,106,550,18,amber);
            TypeAt(shop ? "[W/S]  Выбор       [Enter]  Купить / снарядить       [Esc]  На этаж" :
                "[W/S]  Выбор       [Enter]  Подписать контракт       [Esc]  На этаж",106,610,19,paper);
        }
        Raylib.EndMode2D();
    }
}
