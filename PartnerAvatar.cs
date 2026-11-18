using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal sealed record PlayerPose(Vector3 Eye, float Yaw, float Pitch, WeaponType Weapon)
{
    public Vector3 Forward => new(MathF.Sin(Yaw)*MathF.Cos(Pitch), MathF.Sin(Pitch), MathF.Cos(Yaw)*MathF.Cos(Pitch));
    public Camera3D Camera => new() { Position=Eye, Target=Eye+Forward, Up=Vector3.UnitY, FovY=76 };
    public bool Valid => float.IsFinite(Yaw) && float.IsFinite(Pitch) && Math.Abs(Pitch)<=1.6f &&
        Math.Abs(Eye.X)<=38 && Math.Abs(Eye.Z)<=38 && Eye.Y is >=.8f and <=2.5f && Enum.IsDefined(Weapon);
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
                .Select(c=>(Vector3?)(c.Position+Vector3.UnitY*1.05f)).FirstOrDefault();
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
        Color jacket=new(194,36,77,255), shirt=new(238,219,189,255), dark=new(38,42,55,255),
              skin=new(220,169,129,255), gold=new(247,190,66,255), metal=new(89,99,113,255);
        void Box(float x,float y,float z,float w,float h,float d,Color c) =>
            VisualEngine3D.OrientedBox(P(x,y,z),new(w,h*height,d),yaw,c);
        void Tube(Vector3 a,Vector3 b,float ra,float rb,Color c) => VisualEngine3D.Rod(a,b,ra,rb,10,c);
        VisualEngine3D.DrawCharacterDirectionalShadow(Feet,1.92f*height,.37f);
        for(int side=-1;side<=1;side+=2)
        {
            float step=MathF.Sin(stride)*.12f*side;
            Tube(P(side*.145f,.90f,0),P(side*.16f,.14f,step),.135f,.097f,dark);
            Box(side*.16f,.09f,step+.06f,.23f,.14f,.36f,dark);
        }
        Box(0,.91f,0,.49f,.24f,.34f,dark);
        Box(0,.98f,.18f,.14f,.085f,.035f,gold);
        Box(0,1.25f,0,.62f,.57f,.35f,jacket);
        Box(0,1.33f,.18f,.19f,.41f,.045f,shirt);
        Box(0,1.34f,.217f,.049f,.31f,.021f,dark);
        foreach(int side in new[]{-1,1})
        {
            Tube(P(side*.19f,1.55f,.16f),P(side*.085f,1.24f,.20f),.057f,.02f,jacket);
            Tube(P(side*.108f,1.52f,.205f),P(0,1.41f,.232f),.017f,.016f,gold);
            float grip=flash>0 ? .07f : 0;
            Vector3 elbow=P(side*.37f,1.10f,.20f);
            Vector3 hand=P(.16f+side*.063f,1.28f+grip,.50f-side*.055f);
            Tube(P(side*.28f,1.48f,0),elbow,.146f,.103f,jacket);
            Tube(elbow,hand,.109f,.069f,jacket);
            VisualEngine3D.Orb(hand,.086f,skin);
        }
        Box(0,1.57f,0,.18f,.16f,.18f,skin);
        VisualEngine3D.Oval(P(0,1.76f,0),new(.186f,.224f*height,.174f),skin);
        Box(0,1.91f,-.015f,.36f,.108f,.31f,dark);
        Box(0,1.81f,-.136f,.315f,.19f,.08f,dark);
        foreach(int side in new[]{-1,1})
        {
            Box(side*.164f,1.79f,.018f,.04f,.036f,.25f,dark);
            VisualEngine3D.Orb(P(side*.165f,1.735f,-.02f),.037f,skin);
            Box(side*.27f,1.47f,-.12f,.065f,.13f,.05f,jacket);
        }
        Box(0,1.79f,.14f,.362f,.071f,.065f,dark);
        Box(0,1.745f,.174f,.064f,.096f,.069f,skin);
        float kick=flash>0 ? .065f : 0;
        switch(Pose.Weapon)
        {
            case WeaponType.Belt:
                Tube(P(.19f,1.31f,.54f),P(.35f,1.13f,.84f),.039f,.035f,new(115,66,41,255));
                Tube(P(.35f,1.13f,.84f),P(.47f,.70f,.98f+kick*4),.035f,.017f,dark);
                break;
            case WeaponType.SolderingIron:
                Tube(P(.19f,1.30f,.49f),P(.19f,1.38f,.78f),.067f,.061f,dark);
                Tube(P(.19f,1.38f,.78f),P(.19f,1.42f,.99f),.03f,.008f,Color.Orange);
                break;
            default:
                bool smg=Pose.Weapon==WeaponType.MicroUzi;
                Box(.17f,1.36f+kick,.62f,.116f,.145f,.37f,smg ? metal : new(118,66,43,255));
                Box(.17f,1.22f+kick,.62f,.083f,.23f,.089f,dark);
                Tube(P(.15f,1.39f+kick,.74f),P(.15f,1.39f+kick,.99f),.048f,.045f,dark);
                if(!smg) Tube(P(.23f,1.39f+kick,.74f),P(.23f,1.39f+kick,.99f),.046f,.043f,metal);
                if(flash>0)
                    VisualEngine3D.Rod(P(.15f,1.39f+kick,1),P(.15f,1.42f+kick,1.33f),.095f,.007f,8,Color.Orange,Finish.Lamp);
                break;
        }
    }
}
