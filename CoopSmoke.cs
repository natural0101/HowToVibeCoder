using System.Diagnostics;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public static partial class Program
{
    // Run two real game processes. The guest buys the lift key and fires at the
    // host's boss. Both processes must observe both players' shots and the payout.
    private static void RunCoopSmoke(bool guest)
    {
        var timer=Stopwatch.StartNew();
        int phase=0;
        double finishAt=0;
        if(!guest)
        {
            if(!Net.Host) throw new IOException(Net.Status);
            GrantCash(1000,"Проверка коопа");
            _playerPos=new(-2.1f,1.72f,-12.3f);
            _playerYaw=.32f;
            _weapons.SelectSlot(3);
        }
        while(timer.Elapsed.TotalSeconds<35)
        {
            Net.Pump(LocalPose,CaptureFloor);
            if(Net.Ready && Net.HasPeerPose && _world.Partner is {} ally)
            {
                if(guest)
                {
                    if(phase==0 && _playerCash==1450 && Math.Abs(ally.Pose.Eye.X+2.1f)<.01f &&
                       Math.Abs(ally.Pose.Yaw-.32f)<.01f && ally.Pose.Weapon==WeaponType.MicroUzi)
                    {
                        _playerPos=new(1,1.72f,-11.4f);
                        _weapons.SelectSlot(4);
                        LookAtTarget(_world.NorthElevatorPos+Vector3.UnitY*1.6f);
                        Net.Send(new("interact",LocalPose));
                        phase=1;
                    }
                    if(phase==1 && _world.HasElevatorKey && _playerCash==450 &&
                       _world.ElevatorBoss.Hp is >0 and <220 && ally.Effects.BulletTracers.Count>0)
                    {
                        LookAtTarget(_world.ElevatorBoss.Position+Vector3.UnitY*1.35f);
                        Fire(BuildPlayerCamera());
                        phase=2;
                    }
                    if(phase==2 && _world.ElevatorBoss.Hp==0 && _world.IsFloor2Unlocked && _playerCash==1950)
                    {
                        Console.WriteLine("COOP PASS guest: UDP host pose/rotation/weapon, host shot, shared key, boss defeat, cash 1950");
                        return;
                    }
                }
                else
                {
                    if(phase==0 && _world.ElevatorBoss.IsActive && _playerCash==450 &&
                       Math.Abs(ally.Pose.Eye.X-1)<.01f && ally.Pose.Weapon==WeaponType.SawedOff)
                    {
                        _world.ElevatorBoss.Hp=140;
                        LookAtTarget(_world.ElevatorBoss.Position+Vector3.UnitY*1.35f);
                        Fire(BuildPlayerCamera());
                        phase=1;
                    }
                    if(phase==1 && _world.IsFloor2Unlocked && _playerCash==1950)
                    {
                        Console.WriteLine("COOP PASS host: UDP guest movement/weapon, key purchase, guest shot, single boss payout 1950");
                        finishAt=timer.Elapsed.TotalSeconds+1;
                        phase=2;
                    }
                }
            }
            if(!guest && phase==2 && timer.Elapsed.TotalSeconds>=finishAt) return;
            DrawFrame();
        }
        throw new TimeoutException($"Coop {(guest ? "guest":"host")}, phase {phase}, network {Net.Status}, UDP {Net.HasPeerPose}, cash {_playerCash}, boss {_world.ElevatorBoss.Hp}");
    }
}
