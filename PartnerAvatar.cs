using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal sealed record PlayerPose(Vector3 Eye, float Yaw, float Pitch, WeaponType Weapon)
{
    public Vector3 Forward => new(MathF.Sin(Yaw)*MathF.Cos(Pitch), MathF.Sin(Pitch), MathF.Cos(Yaw)*MathF.Cos(Pitch));
    public Camera3D Camera => new() { Position=Eye, Target=Eye+Forward, Up=Vector3.UnitY, FovY=76 };
    public bool Valid => float.IsFinite(Yaw) && float.IsFinite(Pitch) && Math.Abs(Pitch)<=1.6f &&
        Math.Abs(Eye.X)<=38 && Math.Abs(Eye.Z)<=38 && Eye.Y is >=.8f and <=5.0f && Enum.IsDefined(Weapon);
}

internal sealed class PartnerAvatar
{
    public PlayerPose Pose = new(new(2,1.72f,10),MathF.PI,0,WeaponType.MicroUzi);
    public Vector3 Feet => new(Pose.Eye.X,0,Pose.Eye.Z);
    public readonly WeaponAndCombatView Effects = new();
    private float stride, flash, botCooldown;

    public void Move(PlayerPose pose)
    {
        stride += Math.Min(.4f,Vector3.Distance(Pose.Eye,pose.Eye))*5;
        Pose=pose;
    }

    public void Attack(SkyscraperFloor1World world, Action<int,string> grant, bool authoritative)
    {
        Effects.CurrentWeapon=Pose.Weapon;
        if(authoritative) world.ProcessPlayerAttack(Pose.Camera,Effects,grant,out _);
        else Effects.TriggerWeaponAttack(Pose.Camera,Pose.Eye+Pose.Forward*12,false);
        flash=.12f;
    }

    public void Tick(float dt)
    {
        flash=Math.Max(0,flash-dt);
        Effects.UpdateWeapons(dt,Pose.Camera);
    }

    public void Follow(float dt, PlayerPose player, SkyscraperFloor1World world, Action<int,string> grant)
    {
        Vector3 flank=new(-MathF.Cos(player.Yaw),0,MathF.Sin(player.Yaw));
        Vector3 goal=player.Eye+flank*1.7f+player.Forward*1.5f;
        Vector3 delta=new(goal.X-Pose.Eye.X,0,goal.Z-Pose.Eye.Z);
        Vector3 eye=Pose.Eye;
        if(delta.LengthSquared()>11*11) eye=world.ResolvePlayerCollision(new(goal.X,1.72f,goal.Z));
        else if(delta.LengthSquared()>.25f)
            eye=world.ResolvePlayerCollision(eye+Vector3.Normalize(delta)*Math.Min(delta.Length(),dt*6.8f));
        Vector3? target=world.ElevatorBoss.IsActive ? world.ElevatorBoss.Position+Vector3.UnitY*1.35f :
            world.ActiveCoders.Where(c=>c.State!=CoderAIState.Subdued).OrderBy(c=>Vector3.DistanceSquared(c.Position,eye))
                .Select(c=>(Vector3?)c.CombatCenter).FirstOrDefault();
        Vector3 aim=target.HasValue ? Vector3.Normalize(target.Value-eye) : player.Forward;
        Move(new(eye,MathF.Atan2(aim.X,aim.Z),MathF.Asin(Math.Clamp(aim.Y,-1,1)),WeaponType.MicroUzi));
        botCooldown-=dt;
        if(target.HasValue && Vector3.DistanceSquared(eye,target.Value)<24*24 && botCooldown<=0)
        {
            Attack(world,grant,true);
            botCooldown=.31f;
        }
    }

    public void Draw()
    {
        float yaw=Pose.Yaw;
        Vector3 f=new(MathF.Sin(yaw),0,MathF.Cos(yaw)), r=new(f.Z,0,-f.X);
        float height=Pose.Eye.Y<1.4f ? .75f : 1;
        Vector3 P(float x,float y,float z) => Feet+r*x+Vector3.UnitY*(y*height)+f*z;
        Color dark=new(38,42,55,255), metal=new(89,99,113,255);
        void Tube(Vector3 a,Vector3 b,float ra,float rb,Color c) => VisualEngine3D.Rod(a,b,ra,rb,10,c);
        float step = MathF.Abs(MathF.Sin(stride))*.016f;
        VisualEngine3D.DrawCharacterDirectionalShadow(Feet,1.70f*height,.32f);
        // The authored SMG is a separate material group in the character asset.
        bool smg = Pose.Weapon == WeaponType.MicroUzi;
        VisualEngine3D.Asset("char_partner_90s",Feet+Vector3.UnitY*step,yaw*180/MathF.PI,
            stretch:new Vector3(1,height,1), hideProp:true);
        float kick=flash>0 ? .045f : 0;
        if (smg)
        {
            VisualEngine3D.Asset("weapon_uzi_90s",P(-.094f,1.324f,.258f-kick)+Vector3.UnitY*step,
                yaw*180f/MathF.PI+194f, scale:.66f*height);
        }
        switch(Pose.Weapon)
        {
            case WeaponType.Belt:
                Tube(P(-.10f,1.26f,.32f),P(-.13f,1.16f,.56f),.027f,.021f,new(115,66,41,255));
                Tube(P(-.13f,1.16f,.56f),P(-.17f,.87f,.69f+kick),.021f,.012f,dark);
                break;
            case WeaponType.SolderingIron:
                Tube(P(-.10f,1.25f,.30f),P(-.10f,1.29f,.47f),.025f,.023f,dark);
                Tube(P(-.10f,1.29f,.47f),P(-.10f,1.31f,.65f),.012f,.003f,Color.Orange);
                break;
            default:
                if(!smg)
                {
                    Tube(P(-.095f,1.25f,.26f),P(-.095f,1.30f,.42f),.03f,.026f,new(118,66,43,255));
                    foreach(float x in new[]{-.11f,-.08f})
                        Tube(P(x,1.30f,.42f),P(x,1.32f,.73f),.019f,.016f,metal);
                }
                break;
        }
    }
}
