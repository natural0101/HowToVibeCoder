#nullable enable
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HowToVibeCoder;

internal sealed record FloorSnapshot(int Cash, EscapedAgentBoss Boss, float Door, bool Key, bool Unlocked,
    bool Trap, SubscriptionBaitTier Bait, VibeCoderEntity[] Coders, GalleyDeskSlot[] Desks,
    EnemyProjectile3D[] Projectiles, VibeCoderEntity? Carried, int Sold, int GuestHp, int HostHp = 100, FloorSystems? Systems = null,VibeCoderEntity? HostCarried=null, int ProgressionVersion=0,
    BugFrog[]? Frogs=null, WeaponType Equipped=WeaponType.BareHands, OnboardingStep? OnboardingStep=null, bool? ElevatorRepaired=null, float? ElevatorGlassOpacity=null, int? SelectedHotbarSlot=null,int[]? Health=null,VibeCoderEntity?[]? Escorts=null,bool[]? Downed=null,float[]? RoomOpen=null,float[]? VentTime=null);

internal sealed record CoopMessage(string Kind, PlayerPose? Pose=null, FloorSnapshot? Floor=null, string? Text=null,long Sequence=0, FloatingCombatText3D? Impact=null,int Amount=0,Vector3? Source=null,int[]? Tickets=null,int Slot=0,int Mask=1);
internal sealed record Motion(string Session, long Sequence, PlayerPose Pose,int AllySlot=0,PlayerPose? Ally=null);

internal sealed class CoopNetworkSystem : IDisposable
{
    public const int MaxPlayers=3;
    internal const string Full="Лобби заполнено (макс. 3 игрока)";
    public const int DefaultPort=27094;
    private const string Hello="HowToVibeCoder/6", Search=Hello+"/search", Offer=Hello+"/host";
    private static readonly JsonSerializerOptions Json=new() {
        IncludeFields=true, IgnoreReadOnlyProperties=true, DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull
    };
    private TcpListener? listener;
    private sealed class Peer(TcpClient client,int slot)
    {
        internal readonly TcpClient Tcp=client;
        internal readonly int Slot=slot;
        internal readonly Queue<byte[]> Outgoing=new();
        internal readonly byte[] Buffer=new byte[256*1024];
        internal int Used;
        internal double Deadline=Now+6, NextPose, NextWorld, LastUdp, NextFallback;
        internal Task? Connecting, Writing;
        internal long Sequence, Received;
        internal string Session="";
        internal IPEndPoint? Endpoint;
        internal bool HasPose;
        internal PlayerPose? Pose;
    }
    private readonly List<Peer> peers=[];
    private UdpClient? udp;
    private double nextDiscovery;
    private IPAddress[] discoveryTargets=[];
    private Task<string>? hostAddresses;
    public string HostAddress => hostAddresses is {IsCompletedSuccessfully:true} ? hostAddresses.Result : "Локальная сеть";
    private static IEnumerable<UnicastIPAddressInformation> Interfaces() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n=>n.OperationalStatus==OperationalStatus.Up && n.NetworkInterfaceType!=NetworkInterfaceType.Loopback)
            .SelectMany(n=>n.GetIPProperties().UnicastAddresses)
            .Where(ip=>ip.Address.AddressFamily==AddressFamily.InterNetwork && ip.Address.GetAddressBytes()[0]!=169);
    private static IPAddress Broadcast(UnicastIPAddressInformation local)
    {
        byte[] ip=local.Address.GetAddressBytes(), mask=local.IPv4Mask.GetAddressBytes();
        for(int i=0;i<ip.Length;i++) ip[i]|=(byte)~mask[i];
        return new IPAddress(ip);
    }
    private static double Now => Environment.TickCount64*.001;
    private bool discovering;
    private int? advertisedCount;
    public int? LobbyCount => Host || Ready ? ActivePlayerCount : advertisedCount;
    public string? DiscoveredAddress {get; private set;}
    public string Status {get; private set;}="";
    public bool Host => listener!=null;
    public bool Guest => !Host && peers.Count>0;
    public bool Ready => peers.Any(p=>p.Session.Length>0);
    public bool HasPeerPose => peers.Any(p=>p.HasPose);
    public int LocalSlot {get; private set;}
    private int mask=1;
    private int HostMask => peers.Where(p=>p.Session.Length>0).Aggregate(1,(m,p)=>m | 1<<p.Slot);
    public int ActivePlayerCount => BitOperations.PopCount((uint)(Host ? HostMask : mask));
    public Action<int,PlayerPose>? OnPose;
    public Action<int,CoopMessage>? OnMessage;
    public Action? OnJoined, OnCountChanged;
    public Action<int>? OnLost;

    private static UdpClient OpenUdp(int port)
    {
        var socket=new UdpClient(port);
        // Discovery deliberately probes idle ports. Windows reports their ICMP
        // port-unreachable replies as connection resets on an unconnected socket.
        if(OperatingSystem.IsWindows())
            socket.Client.IOControl((IOControlCode)0x9800000C,new byte[4],null);
        return socket;
    }

    public bool StartHost(int port)
    {
        Dispose();
        try
        {
            listener=new TcpListener(IPAddress.Any,port);
            listener.Start();
            udp=OpenUdp(port);
            hostAddresses=Task.Run(()=>string.Join("   ",Interfaces().Select(ip=>$"{ip.Address}:{port}")));
            Status=$"Игра открыта: {HostAddress}";
            return true;
        }
        catch(SocketException e) { Dispose(); Status=$"Сеть недоступна: {e.Message}"; return false; }
    }

    public void Discover()
    {
        Dispose();
        try
        {
            udp=OpenUdp(0);
            udp.EnableBroadcast=true;
            discoveryTargets=Interfaces().Select(Broadcast).Append(IPAddress.Broadcast).Distinct().ToArray();
            discovering=true;
            Status="Поиск игры в локальной сети";
        }
        catch(SocketException e) { Status=e.Message; }
    }

    public void Join(string address)
    {
        if(!address.Contains(':')) address+=":"+DefaultPort;
        if(!IPEndPoint.TryParse(address,out var endpoint) || endpoint.AddressFamily!=AddressFamily.InterNetwork || endpoint.Port==0)
        { Status="Введите IPv4 и порт, например 192.168.1.20:27094"; return; }
        Dispose();
        try
        {
            udp=OpenUdp(0);
            var tcp=new TcpClient(AddressFamily.InterNetwork) { NoDelay=true };
            var peer=new Peer(tcp,0) { Endpoint=endpoint };
            peers.Add(peer);
            peer.Connecting=tcp.ConnectAsync(endpoint.Address,endpoint.Port);
            Status="Подключение к "+endpoint;
        }
        catch(SocketException e) { Dispose(); Status=e.Message; }
    }

    public void Send(CoopMessage message,int? to=null)
    {
        foreach(var peer in peers.ToArray())
            if(peer.Session.Length>0 && (to==null || peer.Slot==to))
            {
                if(peer.Outgoing.Count>=64) Drop(peer,"Соединение перегружено");
                else Queue(peer,message);
            }
    }

    private static void Queue(Peer peer,CoopMessage message)
    {
        if(peer.Outgoing.Count>=64) throw new IOException("Соединение перегружено");
        peer.Outgoing.Enqueue(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message,Json)+"\n"));
    }

    public void Pump(PlayerPose self,Func<FloorSnapshot> snapshot) => Pump(self,_=>snapshot());
    public void Pump(PlayerPose self,Func<int,FloorSnapshot> snapshot)
    {
        double now=Now;
        try
        {
            if(discovering && now>=nextDiscovery)
            {
                nextDiscovery=now+1.5;
                byte[] request=Encoding.ASCII.GetBytes(Search);
                udp!.Send(request,new IPEndPoint(IPAddress.Loopback,DefaultPort));
                foreach(var target in discoveryTargets)
                {
                    try { udp.Send(request,new IPEndPoint(target,DefaultPort)); }
                    catch(SocketException) { /* Other interfaces and direct IP still work. */ }
                }
            }
            if(listener?.Pending()==true)
            {
                var candidate=listener.AcceptTcpClient();
                int slot=Enumerable.Range(1,MaxPlayers-1).FirstOrDefault(i=>peers.All(p=>p.Slot!=i));
                candidate.NoDelay=true;
                if(slot==0)
                {
                    using(candidate) candidate.GetStream().Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new CoopMessage("full"),Json)+"\n"));
                }
                else peers.Add(new(candidate,slot));
            }
            ReadDatagrams();
        }
        catch(Exception e) when(e is IOException or SocketException) { Status=e.Message; }
        foreach(var peer in peers.ToArray())
        {
            if(!peers.Contains(peer)) continue;
            try { PumpPeer(peer,self,snapshot,now); }
            catch(Exception e) when(e is IOException or InvalidDataException or SocketException or JsonException or InvalidOperationException)
            { Drop(peer,e.Message); }
        }
    }

    private void PumpPeer(Peer p,PlayerPose self,Func<int,FloorSnapshot> snapshot,double now)
    {
        var tcp=p.Tcp;
        bool ready=p.Session.Length>0;
        if(!ready && now>p.Deadline) { Drop(p,"Время подключения истекло"); return; }
        if(p.Connecting!=null)
        {
            if(!p.Connecting.IsCompleted) return;
            p.Connecting.GetAwaiter().GetResult();
            p.Connecting=null;
            Queue(p,new("hello",Text:Hello));
        }
        var stream=tcp.GetStream();
        if(tcp.Client.Poll(0,SelectMode.SelectRead) && tcp.Available==0)
        { Drop(p,"Друг отключился"); return; }
        if(stream.DataAvailable)
        {
            int n=stream.Read(p.Buffer,p.Used,p.Buffer.Length-p.Used);
            if(n==0) { Drop(p,"Соединение закрыто"); return; }
            p.Used+=n;
            int offset=0, end;
            while((end=Array.IndexOf(p.Buffer,(byte)'\n',offset,p.Used-offset))>=0)
            {
                var message=JsonSerializer.Deserialize<CoopMessage>(p.Buffer.AsSpan(offset,end-offset),Json);
                if(message==null) throw new InvalidDataException();
                Receive(p,message,self);
                if(!peers.Contains(p)) return;
                offset=end+1;
            }
            if(offset>0) { p.Used-=offset; Buffer.BlockCopy(p.Buffer,offset,p.Buffer,0,p.Used); }
            if(p.Used==p.Buffer.Length) throw new InvalidDataException("Пакет слишком велик");
        }
        if(ready && p.Endpoint!=null && now>=p.NextPose)
        {
            p.NextPose=now+.04;
            var ally=Host ? peers.Find(other=>other!=p && other.Session.Length>0) : null;
            byte[] data=JsonSerializer.SerializeToUtf8Bytes(new Motion(p.Session,++p.Sequence,self,ally?.Slot ?? 0,ally?.Pose),Json);
            udp!.Send(data,p.Endpoint);
        }
        if(ready && now-p.LastUdp>1 && now>=p.NextFallback && p.Outgoing.Count==0)
        {
            p.NextFallback=now+.12;
            Queue(p,new("pose",self,Sequence:++p.Sequence));
            if(Host && peers.Find(other=>other!=p && other.Session.Length>0) is {Pose:{} ally} other)
                Queue(p,new("teammate",ally,Slot:other.Slot));
        }
        if(ready && Host && now>=p.NextWorld && p.Outgoing.Count<4 && p.Writing?.IsCompleted!=false)
        {
            p.NextWorld=now+.10;
            Queue(p,new("floor",Floor:snapshot(p.Slot)));
        }
        if(p.Writing?.IsCompleted==true) { p.Writing.GetAwaiter().GetResult(); p.Writing=null; }
        if(p.Writing==null && p.Outgoing.TryDequeue(out byte[]? bytes))
            p.Writing=stream.WriteAsync(bytes).AsTask();
    }

    private void Receive(Peer p,CoopMessage m,PlayerPose self)
    {
        if(p.Session.Length==0)
        {
            if(Host && m.Kind=="hello" && m.Text==Hello)
            {
                p.Session=Guid.NewGuid().ToString("N");
                Queue(p,new("welcome",self,Text:p.Session,Slot:p.Slot,Mask:HostMask));
                Membership();
            }
            else if(Guest && m.Kind=="full") { advertisedCount=MaxPlayers; Drop(p,Loc.Tr(Full)); return; }
            else if(Guest && m.Kind=="welcome" && Guid.TryParseExact(m.Text,"N",out _) && m.Pose?.Valid==true && m.Slot is 1 or 2)
            {
                p.Session=m.Text!; LocalSlot=m.Slot; mask=m.Mask;
                OnJoined?.Invoke(); OnPose?.Invoke(0,m.Pose);
            }
            else throw new InvalidDataException("Несовместимая версия игры");
            Status="Друг в игре"; return;
        }
        if(m.Pose is {Valid:false}) return;
        if(!Host && m.Kind=="roster")
        {
            int gone=mask & ~m.Mask;
            mask=m.Mask;
            for(int i=1;i<MaxPlayers;i++) if((gone & 1<<i)!=0) OnLost?.Invoke(i);
            return;
        }
        if(!Host && m.Kind=="teammate" && m.Pose!=null && m.Slot!=LocalSlot && (mask & 1<<m.Slot)!=0)
        { OnPose?.Invoke(m.Slot,m.Pose); return; }
        if(m.Kind=="pose" && m.Pose!=null)
        { AcceptPose(p,m.Sequence,m.Pose); return; }
        OnMessage?.Invoke(Host ? p.Slot : m.Slot,m);
    }

    private void AcceptPose(Peer p,long seq,PlayerPose pose)
    {
        if(seq<=p.Received) return;
        p.Received=seq; p.HasPose=true; p.Pose=pose;
        OnPose?.Invoke(p.Slot,pose);

    }

    private void Membership()
    {
        Send(new("roster",Mask:HostMask));
        OnCountChanged?.Invoke();
    }

    private void ReadDatagrams()
    {
        if(udp==null) return;
        for(int i=0;i<32 && udp.Available>0;i++)
        {
            IPEndPoint sender=new(IPAddress.Any,0);
            byte[] data=udp.Receive(ref sender);
            if(data.Length>1024) continue;
            if(Host && data.AsSpan().SequenceEqual(Encoding.ASCII.GetBytes(Search)))
            {
                udp.Send(Encoding.ASCII.GetBytes(Offer+ActivePlayerCount),sender);
                continue;
            }
            if(discovering && data.Length==Offer.Length+1 && data[^1] is >=49 and <=51 &&
                data.AsSpan(0,Offer.Length).SequenceEqual(Encoding.ASCII.GetBytes(Offer)))
            {
                if(DiscoveredAddress==null || !IPAddress.IsLoopback(sender.Address))
                {
                    DiscoveredAddress=sender.ToString();
                    advertisedCount=data[^1]-(byte)'0';
                }
                Status="Найдена игра: "+DiscoveredAddress;
                continue;
            }
            Motion? motion;
            try { motion=JsonSerializer.Deserialize<Motion>(data,Json); } catch(JsonException) { continue; }
            if(motion?.Pose?.Valid!=true) continue;
            var p=peers.Find(p=>p.Session.Length>0 && p.Session==motion.Session &&
                p.Tcp.Client.RemoteEndPoint is IPEndPoint ep && ep.Address.Equals(sender.Address) &&
                (p.Endpoint==null || p.Endpoint.Equals(sender)));
            if(p==null) continue;
            p.LastUdp=Now; p.Endpoint=sender;
            if(motion.Sequence<=p.Received) continue;
            AcceptPose(p,motion.Sequence,motion.Pose);
            if(Guest && motion.Ally is {Valid:true} ally && motion.AllySlot is 1 or 2 && motion.AllySlot!=LocalSlot && (mask & 1<<motion.AllySlot)!=0)
                OnPose?.Invoke(motion.AllySlot,ally);
        }
    }

    private void Drop(Peer p,string reason)
    {
        peers.Remove(p); p.Tcp.Dispose(); Status=reason;
        if(Host)
        {
            if(p.Session.Length>0) { OnLost?.Invoke(p.Slot); Membership(); }
        }
        else
        {
            mask=1; LocalSlot=0; discovering=false; udp?.Dispose(); udp=null;
            OnLost?.Invoke(0);
        }
    }

    public void Dispose()
    {
        foreach(var p in peers) p.Tcp.Dispose();
        peers.Clear(); mask=1; LocalSlot=0;
        listener?.Stop(); listener=null;
        udp?.Dispose(); udp=null;
        discovering=false; advertisedCount=null; DiscoveredAddress=null; nextDiscovery=0; Status="";
    }
}
