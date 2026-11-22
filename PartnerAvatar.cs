using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal sealed record PlayerPose(Vector3 Eye, float Yaw, float Pitch, WeaponType Weapon,float Swing=0,bool Crouched=false,bool Sprinting=false)
{
    public Vector3 Forward => new(MathF.Sin(Yaw)*MathF.Cos(Pitch), MathF.Sin(Pitch), MathF.Cos(Yaw)*MathF.Cos(Pitch));
    public Camera3D Camera => new() { Position=Eye, Target=Eye+Forward, Up=Vector3.UnitY, FovY=76 };
    public bool Valid => Swing is >=0 and <=1 && float.IsFinite(Yaw) && float.IsFinite(Pitch) && Math.Abs(Pitch)<=1.6f &&
        Math.Abs(Eye.X)<=38 && Math.Abs(Eye.Z)<=38 && Eye.Y is >=.8f and <=5.0f && Enum.IsDefined(Weapon);
}

internal sealed class PartnerAvatar(int slot=0)
{
    internal int Slot {get;}=slot;
    internal int Hp=100;
    internal bool Carrying,Downed;
    internal float FlipOffTimer;
    internal Color Blazer => Slot switch { 1=>new(35,75,142,255), 2=>new(31,110,74,255), _=>new(181,34,46,255) };
    internal string Label => Loc.Tr(Slot==0 ? "Хозяин" : $"Гость {Slot}")
        +(FlipOffTimer>0 ? Loc.Tr(" [ФАК!]") : Downed ? Loc.Tr(" (сбит с ног)") : Carrying ? Loc.Tr(" (с кандидатом)") : "");
    public PlayerPose Pose = new(new(2,1.72f,10),MathF.PI,0,WeaponType.MicroUzi);
    public Vector3 Feet => new(Pose.Eye.X,0,Pose.Eye.Z);
    public readonly WeaponAndCombatView Effects = new();
    private float stride, flash, botCooldown, combatStrafe;

    public void Move(PlayerPose pose)
    {
        stride += Math.Min(.4f,Vector3.Distance(Pose.Eye,pose.Eye))*5;
        Pose=pose;
        Effects.BeltSwingPhase=pose.Swing;
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
        FlipOffTimer=Math.Max(0,FlipOffTimer-dt);
        Effects.UpdateWeapons(dt,Pose.Camera);
    }

    public void Follow(float dt, PlayerPose player, SkyscraperFloor1World world, Action<int,string> grant)
    {
        Vector3 flank=new(-MathF.Cos(player.Yaw),0,MathF.Sin(player.Yaw));
        Vector3 goal;
        float moveSpeed=6.8f;
        if(world.BossInCombat)
        {
            combatStrafe+=dt*1.9f;
            Vector3 bossPos=world.ElevatorBoss.Position;
            Vector3 fromBoss=new(player.Eye.X-bossPos.X,0,player.Eye.Z-bossPos.Z);
            Vector3 away=fromBoss.LengthSquared()>.04f ? Vector3.Normalize(fromBoss) : -new Vector3(player.Forward.X,0,player.Forward.Z);
            if(away.LengthSquared()<.01f) away=new(0,0,-1);
            away=Vector3.Normalize(away);
            Vector3 side=new(-away.Z,0,away.X);
            float orbitOffset=2.55f+MathF.Sin(combatStrafe)*.45f;
            goal=player.Eye+side*orbitOffset+away*.35f;

            // Strict >= 3.4m tactical flanking buffer around the boss (never crowd into boss legs!)
            const float minBossClearance=3.45f;
            Vector3 goalFromBoss=new(goal.X-bossPos.X,0,goal.Z-bossPos.Z);
            float gDist=goalFromBoss.Length();
            if(gDist<minBossClearance)
            {
                Vector3 pushDir=gDist>.15f ? goalFromBoss/gDist : Vector3.Normalize(away*.72f+side*.69f);
                goal=new(bossPos.X+pushDir.X*minBossClearance,1.72f,bossPos.Z+pushDir.Z*minBossClearance);
            }
            Vector3 curFromBoss=new(Pose.Eye.X-bossPos.X,0,Pose.Eye.Z-bossPos.Z);
            if(curFromBoss.LengthSquared()<3.2f*3.2f)
            {
                Vector3 evadeDir=curFromBoss.LengthSquared()>.04f ? Vector3.Normalize(curFromBoss) : Vector3.Normalize(away*.7f+side*.7f);
                goal=new(bossPos.X+evadeDir.X*3.85f+side.X*.65f,1.72f,bossPos.Z+evadeDir.Z*3.85f+side.Z*.65f);
                moveSpeed=8.4f;
            }
        }
        else
        {
            goal=player.Eye+flank*1.7f+player.Forward*1.5f;
        }

        Vector3 delta=new(goal.X-Pose.Eye.X,0,goal.Z-Pose.Eye.Z);
        Vector3 eye=Pose.Eye;
        if(delta.LengthSquared()>11*11) eye=world.ResolvePlayerCollision(new(goal.X,1.72f,goal.Z));
        else if(delta.LengthSquared()>.16f)
            eye=world.ResolvePlayerCollision(eye+Vector3.Normalize(delta)*Math.Min(delta.Length(),dt*moveSpeed));
        if(world.BossInCombat)
        {
            Vector3 bossPos=world.ElevatorBoss.Position;
            Vector3 post=new(eye.X-bossPos.X,0,eye.Z-bossPos.Z);
            float pDist=post.Length();
            if(pDist<3.2f)
            {
                Vector3 push=pDist>.05f ? post/pDist : new(1,0,0);
                eye=world.ResolvePlayerCollision(new(bossPos.X+push.X*3.25f,eye.Y,bossPos.Z+push.Z*3.25f));
            }
        }
        Vector3? target=world.BossInCombat ? world.ElevatorBoss.Position+Vector3.UnitY*1.55f :
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
        float recoilPhase = flash>0 ? MathF.Sin((flash/.12f)*MathF.PI) : 0f;
        // Tactical firing crouch stance when actively shooting (bent knees / lower center of gravity)
        float height=Pose.Eye.Y<1.4f ? .75f : (flash>0 ? .91f : 1f);
        Vector3 P(float x,float y,float z) => Feet+r*x+Vector3.UnitY*(y*height)+f*z;
        Color dark=new(38,42,55,255), metal=new(89,99,113,255);
        void Tube(Vector3 a,Vector3 b,float ra,float rb,Color c) => VisualEngine3D.Rod(a,b,ra,rb,10,c);
        float step = MathF.Abs(MathF.Sin(stride))*.024f;
        float combatLean = Pose.Weapon == WeaponType.MicroUzi ? (flash>0 ? .115f : .075f) : .025f;
        float leanTilt = combatLean + .032f*MathF.Sin(stride*2f) - .045f*recoilPhase;
        VisualEngine3D.DrawCharacterDirectionalShadow(Feet,1.70f*height,.32f);
        // The authored SMG is a separate material group in the character asset.
        bool smg = Pose.Weapon == WeaponType.MicroUzi;
        VisualEngine3D.Asset("char_partner_90s",Feet+Vector3.UnitY*step,yaw*180/MathF.PI,
            tilt:leanTilt,stretch:new Vector3(1,height,1), hideProp:true,tint:Blazer);
        float kick=.048f*recoilPhase;
        float climb=.016f*recoilPhase;
        float lash=MathF.Sin(Pose.Swing*MathF.PI)*.6f;
        // Raised 3D middle-finger salute when teammate triggers [V]!
        if (FlipOffTimer > 0)
        {
            Color skin = new(218, 174, 148, 255);
            Vector3 fist = P(-.18f, 1.46f, .34f) + Vector3.UnitY * step;
            Tube(P(-.22f, 1.26f, .16f) + Vector3.UnitY * step, fist, .042f, .036f, Blazer);
            VisualEngine3D.Oval(fist, new(.045f, .045f, .045f), skin, Finish.Anatomy);
            Tube(fist + Vector3.UnitY * .02f, fist + Vector3.UnitY * .135f, .012f, .010f, skin);
        }
        if (smg)
        {
            VisualEngine3D.Asset("weapon_uzi_90s",P(-.092f,1.335f+climb,.315f-kick)+Vector3.UnitY*step,
                yaw*180f/MathF.PI+194f, scale:.74f*height, tilt:-.10f*recoilPhase);
            if (flash > 0f && !VisualEngine3D.CastingShadows)
            {
                // Hand-painted compact additive firearm muzzle burst + incandescent tracer filaments (zero cartoon yellow rods!)
                Vector3 muzzle = P(-.095f, 1.358f + climb, .545f - kick) + Vector3.UnitY * step;
                if (CombatSprites.HasPaintedFx)
                {
                    CombatSprites.Begin(BlendMode.Additive);
                    float flareR = 0.125f + 0.045f * recoilPhase;
                    CombatSprites.ExplosionFrame(0, muzzle + f * 0.055f, VisualEngine3D.ActiveCamRight, VisualEngine3D.ActiveCamUp,
                        flareR, stride * 6.5f, new Color(255, 224, 152, Math.Clamp((int)(230 * (0.45f + 0.55f * recoilPhase)), 0, 255)));
                    CombatSprites.FireFrame(((int)(stride * 12f)) & 3, muzzle + f * 0.11f, VisualEngine3D.ActiveCamRight, VisualEngine3D.ActiveCamUp,
                        0.065f + 0.025f * recoilPhase, 0.12f + 0.04f * recoilPhase,
                        new Color(255, 178, 86, Math.Clamp((int)(168 * (0.4f + 0.6f * recoilPhase)), 0, 255)));
                    CombatSprites.End();
                }
                Color hot = new(255, 244, 198, 255);
                VisualEngine3D.Rod(muzzle, muzzle + f * (.11f + .04f * recoilPhase), .0045f, .0012f, 5, hot, Finish.Lamp);
                for (int k = 0; k < 2; k++)
                {
                    float a = k * MathF.PI + stride * 4.1f + 0.6f;
                    Vector3 spark = Vector3.Normalize(f * 0.85f + r * MathF.Cos(a) * 0.45f + Vector3.UnitY * MathF.Sin(a) * 0.45f);
                    VisualEngine3D.Rod(muzzle + f * 0.02f, muzzle + spark * (.095f + .03f * k), .0032f, .0008f, 4, new Color(255, 198, 92, 255), Finish.Lamp);
                }
            }
        }
        switch(Pose.Weapon)
        {
            case WeaponType.Belt:
                Tube(P(-.10f,1.26f,.32f),P(-.13f,1.16f,.56f),.027f,.021f,new(115,66,41,255));
                Tube(P(-.13f,1.16f,.56f),P(-.17f,.87f+lash,.69f+kick+lash),.021f,.012f,dark);
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
