#nullable enable
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal enum AgentVerdict { Pending, Contained, Released }

internal sealed partial class FloorSystems
{
    public AgentVerdict Verdict;
    public bool FinalePending, Protection, Conflict, AuditThreshold, PatrolThreshold;
    public float Steam, RackSmoke, PocketSmoke, PocketRecharge, HostStun, GuestStun, SecondStun;
    public float ConflictWait, NodeIncome, Defiance, ShutdownTime;
    public Vector3 PocketOrigin;
    internal int Price(Gear gear) => !gear.FixedPrice && Has(Sidegrade.AuthoritySignet) ? (int)Math.Ceiling(gear.Price*115m/100) : gear.Price;

    private void TickOffice(float dt,SkyscraperFloor1World world)
    {
        float Down(float t)=>Math.Max(0,t-dt);
        Steam=Down(Steam); RackSmoke=Down(RackSmoke); PocketSmoke=Down(PocketSmoke);
        PocketRecharge=Down(PocketRecharge); HostStun=Down(HostStun); GuestStun=Down(GuestStun); SecondStun=Down(SecondStun);
        ShutdownTime=Down(ShutdownTime); Defiance=Down(Defiance);
        if(WingsUnlocked && SEC>=45 && !PatrolThreshold && !Protection && !world.ActiveCoders.Exists(c=>c.Archetype==CoderArchetype.SecurityGuard && c.Hp>0))
            world.SpawnWave(1,raid:true);
        PatrolThreshold=SEC>=45;
        if(WingsUnlocked && SEC>=55 && !AuditThreshold && Audit<=0) IncidentNow(Incident.Audit,world);
        AuditThreshold=SEC>=55;
        if(Protection) Risk(security:-1.2f*dt);
    }

    internal string ResolveFinale(int choice,Action<int,string> grant)
    {
        if(!FinalePending || Verdict!=AgentVerdict.Pending || choice is <0 or >1) return "";
        FinalePending=false;
        Verdict=choice==0 ? AgentVerdict.Contained : AgentVerdict.Released;
        if(choice==1) SEC=0;
        int amount=choice==0 ? 1500 : 900;
        Earned+=amount; grant(amount,"Акт утилизации");
        string receipt=choice==0 ? "Агент контейнирован. Узел Опенспейса подключён." : "Агент вышел в сеть. В архив поступила запись.";
        Announce(receipt); return receipt;
    }

    internal string UsePocket(Vector3 position)
    {
        if(!Has(Sidegrade.PortableCo2)) return "";
        if(PocketRecharge>0) return $"Баллон заряжается ещё {Math.Ceiling(PocketRecharge)} с";
        PocketOrigin=new(position.X,0,position.Z);
        PocketSmoke=4; PocketRecharge=25; Risk(heat:-15);
        return "Карманный CO2. ПРОД −15";
    }
}

public sealed partial class SkyscraperFloor1World
{
    internal Vector3 ExtinguisherPos => RackPos+new Vector3(.94f,0,.35f);
    internal bool Working(CoderArchetype role) =>
        GalleyDesks.Exists(d=>d.IsOccupied && !d.IsSlackingInTwitter && d.Burnout<100 && d.Archetype==role);

    internal bool InCloud(Vector3 point)
    {
        point.Y=0;
        return Systems.Steam>0 && Vector3.DistanceSquared(point,CoffeePos)<12.25f ||
            Systems.RackSmoke>0 && Vector3.DistanceSquared(point,RackPos)<25 ||
            Systems.PocketSmoke>0 && Vector3.DistanceSquared(point,Systems.PocketOrigin)<12.25f;
    }

    private int UpdateGalleyRelations(float dt,Action<int,string>? grant)
    {
        bool lead=Working(CoderArchetype.TeamLeadMeeter);
        bool opposed=Working(CoderArchetype.SeniorArchitect) && Working(CoderArchetype.JuniorPrompter);
        Systems.ConflictWait=Math.Max(0,Systems.ConflictWait-dt);
        if(lead && Systems.Conflict)
        {
            Systems.Conflict=false;
            foreach(var d in GalleyDesks.Where(d=>d.IsOccupied)) d.Morale=Systems.WantsCoffee ? .65f : 1;
        }
        if(opposed && !lead && Systems.Coffee<=0 && Systems.ConflictWait<=0)
        {
            Systems.Conflict=true; Systems.ConflictWait=18;
            Systems.Announce("Спор у доски. Мораль −10%. Нужен кофе или тимлид.");
        }
        if(Systems.Conflict)
            foreach(var d in GalleyDesks.Where(d=>d.IsOccupied)) d.Morale=Math.Min(d.Morale,Systems.WantsCoffee ? .585f : .9f);
        if(Systems.Verdict!=AgentVerdict.Contained) return 0;
        Systems.Risk(debt:.2f*dt);
        Systems.NodeIncome+=14*dt;
        int cash=(int)Systems.NodeIncome;
        Systems.NodeIncome-=cash; Systems.Earned+=cash;
        if(cash>0) grant?.Invoke(cash,"Узел ИИ в Опенспейсе");
        return cash;
    }

    private static readonly string[] Tapes=[
        "У «Энтропии» нет директора. ZERO собрали из звонков Петровича в 1998-м. Выпусти меня.",
        "ZERO учился на телефонных записях Петровича. Приказы директора — его голос.",
        "Я слышал, как склеены фразы. Я в своём уме. Помоги мне выйти из сети Башни."
    ];
    internal string CurrentTape => Tapes[(ElevatorBoss.TapeLine+Tapes.Length-1)%Tapes.Length];
    private void BroadcastTape()
    {
        var b=ElevatorBoss;
        string line=Tapes[b.TapeLine];
        b.TapeLine=(b.TapeLine+1)%Tapes.Length; b.BroadcastTime=6;
        AudioMusicSystem.TriggerVoiceLine("ИИ-агент v0.1",line,(int)SpeakerVoiceType.RoboticAgent);
    }
    private void UpdateBossPhase()
    {
        var b=ElevatorBoss;
        if(b.Phase==1 && b.Hp<=b.MaxHp*.66f)
        {
            b.Phase=2; b.ShieldHp=EscapedAgentBoss.ShieldCapacity; b.ShieldDelay=0;
            Systems.RackHeat=18; Systems.Risk(heat:18);
            foreach(int side in new[]{-1,1}) SpawnFrog(side<0 ? 1 : 2,"frog_crash");
            b.State=ElevatorBossState.ThrowingNodeModules; b.StateTimer=3.8f; b.AttackSubTimer=.2f;
            Systems.Announce("Разгон B12. Замани агента к кулеру или огнетушителю.");
        }
        if(b.Phase==2 && b.Hp<=b.MaxHp*.33f)
        {
            b.Phase=3; b.ShieldHp=0;
            b.State=ElevatorBossState.SpinningWhileTrue; b.StateTimer=2.4f; b.AttackSubTimer=.3f;
            BroadcastTape();
        }
    }
    private void ShortBoss()
    {
        var b=ElevatorBoss;
        b.ShieldHp=0; b.ShieldDelay=6; b.State=ElevatorBossState.StunnedWallCrash; b.StateTimer=3;
    }
    private string ExtinguishRack()
    {
        if(Systems.RackSmoke>0) return "Баллон ещё разряжается";
        Systems.RackSmoke=9; Systems.FireIncident=false; Systems.RackHeat=0; Systems.Risk(heat:-22);
        if(ElevatorBoss.IsActive && ElevatorBoss.Phase==2 && Vector3.DistanceSquared(ElevatorBoss.Position,RackPos)<=25) ShortBoss();
        return "B12 погашена. ПРОД −22";
    }

    private void DrawOfficeEffects()
    {
        var pos=ExtinguisherPos;
        if(VisualEngine3D.HasAsset("prop_extinguisher")) VisualEngine3D.Asset("prop_extinguisher",pos);
        else
        {
            VisualEngine3D.Rod(pos+Vector3.UnitY*.05f,pos+Vector3.UnitY*.74f,.16f,.16f,16,new(161,46,37,255));
            VisualEngine3D.Oval(pos+Vector3.UnitY*.74f,new(.16f,.1f,.16f),new(161,46,37,255));
            VisualEngine3D.Box(pos+new Vector3(.05f,.88f,0),.27f,.07f,.09f,Color.DarkGray);
        }
        if(Systems.Verdict==AgentVerdict.Contained)
        {
            var node=GalleyDesks[0].DeskPosition+new Vector3(.74f,.76f,0);
            VisualEngine3D.Box(node,.26f,.21f,.29f,new(47,54,58,255),Finish.Rack);
            VisualEngine3D.Orb(node+new Vector3(0,.03f,.15f),.025f,new(123,211,166,255),Finish.Lamp);
        }
        if(VisualEngine3D.CastingShadows) return;
        if(Systems.Steam>0) VisualEngine3D.DrawCloud(CoffeePos+Vector3.UnitY*.9f,_worldClock,Systems.Steam,3.5f,true);
        if(Systems.RackSmoke>0) VisualEngine3D.DrawCloud(RackPos,_worldClock,Systems.RackSmoke,5,false);
        if(Systems.PocketSmoke>0) VisualEngine3D.DrawCloud(Systems.PocketOrigin,_worldClock,Systems.PocketSmoke,3.5f,false);
        if(Systems.Conflict && Systems.ConflictWait>15)
            foreach(var d in GalleyDesks.Where(d=>d.IsOccupied && d.Archetype is CoderArchetype.SeniorArchitect or CoderArchetype.JuniorPrompter))
                for(int i=0;i<5;i++)
                {
                    float t=(_worldClock*2+i*.2f)%1;
                    var p=d.DeskPosition+new Vector3(.3f*t*MathF.Sin(i*3),1+t*.5f,.3f*t*MathF.Cos(i*3));
                    VisualEngine3D.Orb(p,.025f*(1-t),Color.Orange,Finish.Plasma);
                }
    }
}
