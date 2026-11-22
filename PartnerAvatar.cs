using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal sealed record PlayerPose(Vector3 Eye, float Yaw, float Pitch, WeaponType Weapon,float Swing=0,bool Crouched=false,bool Sprinting=false,int Appearance=0)
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
    internal CharacterAppearance Appearance = new();
    private int _lastPackedAppearance = -1;
    private Color SlotBlazerFallback => Slot switch { 1=>new(35,75,142,255), 2=>new(31,110,74,255), _=>new(181,34,46,255) };
    private bool UseSlotFallback => Slot > 0 && Pose.Appearance == 0 && Appearance.Pack() == 0;
    internal Color Blazer => UseSlotFallback ? SlotBlazerFallback : Appearance.OutfitColor;
    internal string Label => Loc.Tr(Slot==0 ? "Хозяин" : $"Гость {Slot}")
        +(FlipOffTimer>0 ? Loc.Tr(" — ФАК!") : Downed ? Loc.Tr(" (сбит с ног)") : Carrying ? Loc.Tr(" (с кандидатом)") : "");
    public PlayerPose Pose = new(new(2,1.72f,10),MathF.PI,0,WeaponType.MicroUzi);
    public Vector3 Feet => new(Pose.Eye.X,0,Pose.Eye.Z);
    public readonly WeaponAndCombatView Effects = new();
    private float stride, flash, botCooldown, combatStrafe;

    public void Move(PlayerPose pose)
    {
        stride += Math.Min(.4f,Vector3.Distance(Pose.Eye,pose.Eye))*5;
        Pose=pose;
        Effects.BeltSwingPhase=pose.Swing;
        if (pose.Appearance != _lastPackedAppearance)
        {
            _lastPackedAppearance = pose.Appearance;
            Appearance = CharacterAppearance.Unpack(pose.Appearance);
        }
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

    private static readonly Vector3 HatAnchor = new(0.0000f, 1.6900f, -0.0120f);
    private static readonly Vector3 EyeAnchor = new(0.0000f, 1.5860f, +0.1030f);
    private static readonly Vector3 RightShoulderPivot = new(-0.1940f, 1.3520f, -0.0520f);
    private static readonly Vector3 RightHandGripOffset = new(-0.0640f, -0.5500f, +0.0840f);

    public void Draw()
    {
        float yaw = Pose.Yaw;
        Vector3 f = new(MathF.Sin(yaw), 0, MathF.Cos(yaw)), r = new(f.Z, 0, -f.X);
        float recoilPhase = flash > 0 ? MathF.Sin((flash / .12f) * MathF.PI) : 0f;
        bool bareHands = Pose.Weapon == WeaponType.BareHands;
        bool useArticulatedArm = !bareHands && VisualEngine3D.HasAsset("char_partner_90s_body") && VisualEngine3D.HasAsset("char_partner_90s_arm_r");

        // Natural upright posture (zero Michael-Jackson full-body forward tilt!)
        float height = Pose.Eye.Y < 1.4f ? .78f : 1f;
        float step = bareHands ? 0f : MathF.Abs(MathF.Sin(stride)) * .018f;
        float leanTilt = bareHands ? 0f : (-0.022f * recoilPhase + 0.012f * MathF.Sin(stride * 2f));
        VisualEngine3D.DrawCharacterDirectionalShadow(Feet, 1.74f * height, .32f);

        Vector3 basePos = Feet + Vector3.UnitY * step;
        Vector3 stretch = new(1f, height, 1f);
        Color? slotOverride = UseSlotFallback ? SlotBlazerFallback : null;
        string bodyAsset = useArticulatedArm ? "char_partner_90s_body" : "char_partner_90s";
        VisualEngine3D.DrawPartnerCharacter(basePos, yaw * 180f / MathF.PI, leanTilt, stretch, Appearance, slotOverride, bodyAsset);

        Matrix4x4 charTransform = Matrix4x4.CreateRotationX(leanTilt)
            * Matrix4x4.CreateScale(stretch)
            * Matrix4x4.CreateRotationY(yaw)
            * Matrix4x4.CreateTranslation(basePos);

        // Anchor 3D eyewear and hats to the exact Hunyuan3D sculpted head frame (+Z forward, +Y up).
        if (Appearance.EyewearAsset is string eyeAsset && VisualEngine3D.HasAsset(eyeAsset))
        {
            Vector3 eyeAnchorWorld = Vector3.Transform(EyeAnchor, charTransform);
            Matrix4x4 eyeTransform = Matrix4x4.CreateRotationX(leanTilt)
                * Matrix4x4.CreateRotationY(yaw)
                * Matrix4x4.CreateTranslation(eyeAnchorWorld);
            VisualEngine3D.Asset(eyeAsset, Vector3.Zero, assemblyTransform: eyeTransform);
        }

        if (Appearance.HatAsset is string hatAsset && VisualEngine3D.HasAsset(hatAsset))
        {
            Vector3 hatAnchorWorld = Vector3.Transform(HatAnchor, charTransform);
            Matrix4x4 hatTransform = Matrix4x4.CreateRotationX(leanTilt)
                * Matrix4x4.CreateRotationY(yaw)
                * Matrix4x4.CreateTranslation(hatAnchorWorld);
            VisualEngine3D.Asset(hatAsset, Vector3.Zero, assemblyTransform: hatTransform);
        }

        // Raised 3D middle-finger salute when teammate triggers [V]
        if (FlipOffTimer > 0)
        {
            Vector3 P(float x, float y, float z) => Feet + r * x + Vector3.UnitY * (y * height) + f * z;
            Color skin = Appearance.SkinColor;
            Vector3 fist = P(-.18f, 1.46f, .34f) + Vector3.UnitY * step;
            VisualEngine3D.Rod(P(-.22f, 1.26f, .16f) + Vector3.UnitY * step, fist, .042f, .036f, 10, Blazer);
            VisualEngine3D.Oval(fist, new(.045f, .045f, .045f), skin, Finish.Anatomy);
            VisualEngine3D.Rod(fist + Vector3.UnitY * .02f, fist + Vector3.UnitY * .135f, .012f, .010f, 10, skin);
        }

        if (bareHands) return;

        // Articulated Right Arm raised forward so the weapon grip sits PHYSICALLY INSIDE HIS 5-FINGERED PALM!
        float swingRad = MathF.Sin(Pose.Swing * MathF.PI) * 0.72f;
        float aimPitchDeg = Pose.Weapon switch
        {
            WeaponType.MicroUzi => -76.0f - 8.5f * recoilPhase,
            WeaponType.SawedOff => -74.0f - 12.0f * recoilPhase,
            WeaponType.SolderingIron => -68.0f - 28.0f * swingRad,
            WeaponType.Belt => -58.0f - 48.0f * swingRad,
            WeaponType.CaptureNet => -64.0f - 32.0f * swingRad,
            _ => -76.0f
        };
        float armPitchRad = aimPitchDeg * MathF.PI / 180f;

        Matrix4x4 armLocalTransform = Matrix4x4.CreateRotationX(armPitchRad)
            * Matrix4x4.CreateTranslation(RightShoulderPivot);
        Matrix4x4 armWorldTransform = armLocalTransform * charTransform;

        if (useArticulatedArm)
            VisualEngine3D.DrawPartnerCharacter(Vector3.Zero, 0f, 0f, Vector3.One, Appearance, slotOverride, "char_partner_90s_arm_r", armWorldTransform);

        // Exact world position of his sculpted right palm
        Vector3 palmWorld = Vector3.Transform(RightHandGripOffset, armWorldTransform);
        float weaponPitch = (aimPitchDeg + 76.0f) * MathF.PI / 180f;

        switch (Pose.Weapon)
        {
            case WeaponType.MicroUzi:
            {
                // Pistol grip offset measured directly in Blender on char_partner_90s_arm_r.glb (scale 0.74, rotated 180° around Y to point +Z)
                Vector3 gripOffsetLocal = new(0f, -0.0459f, -0.0089f);
                Matrix4x4 rot = Matrix4x4.CreateRotationY(MathF.PI) * Matrix4x4.CreateRotationX(weaponPitch) * Matrix4x4.CreateRotationY(yaw);
                Matrix4x4 uziTransform = Matrix4x4.CreateScale(0.74f * height) * rot
                    * Matrix4x4.CreateTranslation(palmWorld - Vector3.TransformNormal(gripOffsetLocal * height, Matrix4x4.CreateRotationX(weaponPitch) * Matrix4x4.CreateRotationY(yaw)));
                VisualEngine3D.Asset("weapon_uzi_90s", Vector3.Zero, assemblyTransform: uziTransform);

                if (flash > 0f && !VisualEngine3D.CastingShadows)
                {
                    Vector3 aimDir = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, Matrix4x4.CreateRotationX(weaponPitch) * Matrix4x4.CreateRotationY(yaw)));
                    Vector3 muzzle = palmWorld + aimDir * (0.24f * height) + Vector3.UnitY * (0.045f * height);
                    if (CombatSprites.HasPaintedFx)
                    {
                        CombatSprites.Begin(BlendMode.Additive);
                        float flareR = 0.14f + 0.05f * recoilPhase;
                        CombatSprites.ExplosionFrame(0, muzzle + aimDir * 0.055f, VisualEngine3D.ActiveCamRight, VisualEngine3D.ActiveCamUp,
                            flareR, stride * 6.5f, new Color(255, 224, 152, Math.Clamp((int)(235 * (0.45f + 0.55f * recoilPhase)), 0, 255)));
                        CombatSprites.FireFrame(((int)(stride * 12f)) & 3, muzzle + aimDir * 0.11f, VisualEngine3D.ActiveCamRight, VisualEngine3D.ActiveCamUp,
                            0.075f + 0.025f * recoilPhase, 0.13f + 0.04f * recoilPhase,
                            new Color(255, 184, 92, Math.Clamp((int)(180 * (0.4f + 0.6f * recoilPhase)), 0, 255)));
                        CombatSprites.End();
                    }
                }
                break;
            }
            case WeaponType.SawedOff:
            {
                if (VisualEngine3D.HasAsset("weapon_shotgun_90s"))
                {
                    Matrix4x4 sgTransform = Matrix4x4.CreateScale(0.86f * height)
                        * Matrix4x4.CreateRotationY(MathF.PI)
                        * Matrix4x4.CreateRotationX(weaponPitch)
                        * Matrix4x4.CreateRotationY(yaw)
                        * Matrix4x4.CreateTranslation(palmWorld + Vector3.UnitY * (0.025f * height) + f * (0.05f * height));
                    VisualEngine3D.Asset("weapon_shotgun_90s", Vector3.Zero, assemblyTransform: sgTransform);
                }
                break;
            }
            case WeaponType.CaptureNet:
            {
                if (VisualEngine3D.HasAsset("weapon_net_90s"))
                {
                    Matrix4x4 netTransform = Matrix4x4.CreateScale(0.92f * height)
                        * Matrix4x4.CreateRotationX(weaponPitch)
                        * Matrix4x4.CreateRotationY(yaw)
                        * Matrix4x4.CreateTranslation(palmWorld);
                    VisualEngine3D.Asset("weapon_net_90s", Vector3.Zero, assemblyTransform: netTransform);
                }
                break;
            }
            case WeaponType.Belt:
            {
                if (VisualEngine3D.HasAsset("weapon_belt_90s"))
                {
                    Matrix4x4 beltTransform = Matrix4x4.CreateScale(0.85f * height)
                        * Matrix4x4.CreateRotationX(weaponPitch)
                        * Matrix4x4.CreateRotationY(yaw)
                        * Matrix4x4.CreateTranslation(palmWorld);
                    VisualEngine3D.Asset("weapon_belt_90s", Vector3.Zero, assemblyTransform: beltTransform);
                }
                break;
            }
            case WeaponType.SolderingIron:
            {
                if (VisualEngine3D.HasAsset("weapon_solder_90s"))
                {
                    Matrix4x4 solderTransform = Matrix4x4.CreateScale(0.95f * height)
                        * Matrix4x4.CreateRotationX(weaponPitch)
                        * Matrix4x4.CreateRotationY(yaw)
                        * Matrix4x4.CreateTranslation(palmWorld);
                    VisualEngine3D.Asset("weapon_solder_90s", Vector3.Zero, assemblyTransform: solderTransform);
                }
                break;
            }
        }
    }
}
