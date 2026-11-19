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
    BugFrog[]? Frogs=null, WeaponType Equipped=WeaponType.BareHands, OnboardingStep? OnboardingStep=null, bool? ElevatorRepaired=null, float? ElevatorGlassOpacity=null, int? SelectedHotbarSlot=null);

internal sealed record CoopMessage(string Kind, PlayerPose? Pose=null, FloorSnapshot? Floor=null, string? Text=null,long Sequence=0, FloatingCombatText3D? Impact=null);
internal sealed record Motion(string Session, long Sequence, PlayerPose Pose);

internal sealed class CoopNetworkSystem : IDisposable
{
    public const int DefaultPort=27094;
    private const string Hello="HowToVibeCoder/5", Search=Hello+"/search", Offer=Hello+"/host";
    private static readonly JsonSerializerOptions Json=new() {
        IncludeFields=true, IgnoreReadOnlyProperties=true, DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull
    };
    private TcpListener? listener;
    private TcpClient? tcp;
    private UdpClient? udp;
    private Task? connecting, writing;
    private readonly Queue<byte[]> outgoing=new();
    private readonly byte[] incoming=new byte[256*1024];
    private int used;
    private double deadline, nextDiscovery, nextPose, nextWorld, lastUdp, nextFallback;
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
    private long sequence, received;
    private IPEndPoint? peerUdp;
    private string session="";
    private bool discovering;
    public string? DiscoveredAddress {get; private set;}
    public string Status {get; private set;}="";
    public bool Host => listener!=null;
    public bool Guest => !Host && tcp!=null;
    public bool Ready => session.Length!=0;
    public bool HasPeerPose {get; private set;}
    public Action<PlayerPose>? OnPose;
    public Action<CoopMessage>? OnMessage;
    public Action? OnJoined, OnLost;

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
            listener.Start(2);
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
            peerUdp=endpoint;
            tcp=new TcpClient(AddressFamily.InterNetwork) { NoDelay=true };
            connecting=tcp.ConnectAsync(endpoint.Address,endpoint.Port);
            deadline=Now+6;
            Status="Подключение к "+endpoint;
        }
        catch(SocketException e) { Fail(e.Message); }
    }

    public void Send(CoopMessage message)
    {
        if(tcp==null || connecting!=null) return;
        if(outgoing.Count>=48) { Fail("Соединение перегружено"); return; }
        outgoing.Enqueue(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message,Json)+"\n"));
    }

    public void Pump(PlayerPose self, Func<FloorSnapshot> snapshot)
    {
        try
        {
            double now=Now;
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
                if(tcp!=null) candidate.Dispose();
                else { tcp=candidate; tcp.NoDelay=true; deadline=now+6; }
            }
            ReadDatagrams();
            if(tcp==null) return;
            if(!Ready && now>deadline) { Fail("Время подключения истекло"); return; }
            if(connecting!=null)
            {
                if(!connecting.IsCompleted) return;
                connecting.GetAwaiter().GetResult();
                connecting=null;
                Send(new("hello",Text:Hello));
            }
            var stream=tcp.GetStream();
            if(tcp.Client.Poll(0,SelectMode.SelectRead) && tcp.Available==0)
            { Fail("Друг отключился"); return; }
            if(stream.DataAvailable)
            {
                int n=stream.Read(incoming,used,incoming.Length-used);
                if(n==0) { Fail("Соединение закрыто"); return; }
                used+=n;
                int offset=0, end;
                while((end=Array.IndexOf(incoming,(byte)'\n',offset,used-offset))>=0)
                {
                    var message=JsonSerializer.Deserialize<CoopMessage>(incoming.AsSpan(offset,end-offset),Json);
                    if(message==null) throw new InvalidDataException();
                    Receive(message,self);
                    if(tcp==null) return;
                    offset=end+1;
                }
                if(offset>0) { used-=offset; Buffer.BlockCopy(incoming,offset,incoming,0,used); }
                if(used==incoming.Length) throw new InvalidDataException("Пакет слишком велик");
            }
            if(Ready && peerUdp!=null && now>=nextPose)
            {
                nextPose=now+.04;
                byte[] data=JsonSerializer.SerializeToUtf8Bytes(new Motion(session,++sequence,self),Json);
                udp!.Send(data,peerUdp);
            }
            if(Ready && now-lastUdp>1 && now>=nextFallback && outgoing.Count==0)
            {
                nextFallback=now+.12;
                Send(new("pose",self,Sequence:++sequence));
            }
            if(Ready && Host && now>=nextWorld && outgoing.Count==0 && writing?.IsCompleted!=false)
            {
                nextWorld=now+.10;
                Send(new("floor",Floor:snapshot()));
            }
            if(writing?.IsCompleted==true) { writing.GetAwaiter().GetResult(); writing=null; }
            if(writing==null && outgoing.TryDequeue(out byte[]? bytes))
                writing=stream.WriteAsync(bytes).AsTask();
        }
        catch(Exception e) when(e is IOException or InvalidDataException or SocketException or JsonException or InvalidOperationException)
        { Fail(e.Message); }
    }

    private void Receive(CoopMessage m,PlayerPose self)
    {
        if(!Ready)
        {
            if(Host && m.Kind=="hello" && m.Text==Hello)
            {
                session=Guid.NewGuid().ToString("N");
                Send(new("welcome",self,Text:session));
            }
            else if(Guest && m.Kind=="welcome" && Guid.TryParseExact(m.Text,"N",out _) && m.Pose?.Valid==true)
            {
                session=m.Text!;
                OnJoined?.Invoke();
                OnPose?.Invoke(m.Pose);
            }
            else throw new InvalidDataException("Несовместимая версия игры");
            Status="Друг в игре";
            return;
        }
        if(m.Pose!=null && !m.Pose.Valid) return;
        if(m.Kind=="pose")
        {
            if(m.Sequence>received && m.Pose!=null) { received=m.Sequence; OnPose?.Invoke(m.Pose); }
            return;
        }
        OnMessage?.Invoke(m);
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
                if(tcp==null) udp.Send(Encoding.ASCII.GetBytes(Offer),sender);
                continue;
            }
            if(discovering && data.AsSpan().SequenceEqual(Encoding.ASCII.GetBytes(Offer)))
            {
                if(DiscoveredAddress==null || !IPAddress.IsLoopback(sender.Address))
                    DiscoveredAddress=sender.ToString();
                Status="Найдена игра: "+DiscoveredAddress;
                continue;
            }
            if(!Ready || tcp?.Client.RemoteEndPoint is not IPEndPoint peer || !peer.Address.Equals(sender.Address)) continue;
            if(peerUdp!=null && !peerUdp.Equals(sender)) continue;
            Motion? motion;
            try { motion=JsonSerializer.Deserialize<Motion>(data,Json); } catch(JsonException) { continue; }
            if(motion?.Session!=session || motion.Sequence<=received || motion.Pose?.Valid!=true) continue;
            lastUdp=Now;
            received=motion.Sequence;
            peerUdp=sender;
            HasPeerPose=true;
            OnPose?.Invoke(motion.Pose);
        }
    }

    private void Fail(string reason)
    {
        bool wasGuest=Guest;
        DropPeer();
        if(!Host) { discovering=false; udp?.Dispose(); udp=null; }
        Status=reason;
        if(wasGuest || Host) OnLost?.Invoke();
    }

    private void DropPeer()
    {
        tcp?.Dispose(); tcp=null;
        connecting=writing=null; used=0; outgoing.Clear();
        peerUdp=null; session=""; sequence=received=0; HasPeerPose=false;
        nextPose=nextWorld=nextFallback=lastUdp=0;
    }

    public void Dispose()
    {
        DropPeer();
        listener?.Stop(); listener=null;
        udp?.Dispose(); udp=null;
        discovering=false; DiscoveredAddress=null; nextDiscovery=0; Status="";
    }
}
