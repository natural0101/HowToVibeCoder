using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public enum WeaponType
{
    BareHands = 0,
    CaptureNet = 6,
    Belt = 1,
    SolderingIron = 2,
    MicroUzi = 3,
    SawedOff = 4,
    ChatGun = 5
}

public struct SmokeParticle3D
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Size;
    public float MaxSize;
    public float Life;
    public float MaxLife;
    public Color Tint;
    public bool IsRing;
    public bool IsPowder;
    public float Rotation, InitialSize;
}

public struct SparkParticle3D
{
    public Vector3 Position;
    public Vector3 PrevPosition;
    public Vector3 Velocity;
    public float Life;
    public float MaxLife;
    public Color ColorMain;
    public Color ColorCore;
    public bool IsElectricArc;
    public float BallisticWidth;
    public bool IsDebris;
}

public struct WhipSlashRibbon3D
{
    public Vector3 Start;
    public Vector3 Control;
    public Vector3 End;
    public float Life;
    public float MaxLife;
    public Color PrimaryColor;
    public Color CoreColor;
}

public struct BrassShell3D
{
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector3 Rotation;
    public Vector3 AngularVelocity;
    public float Life;
    public float MaxLife;
    public bool IsShotgunShell;
}

public struct BulletTracer3D
{
    public Vector3 Start;
    public Vector3 End;
    public float Life;
    public float MaxLife;
    public Color TracerColor;
    public float Thickness;
    public float StartFraction;
}

public struct FloatingCombatText3D
{
    public Vector3 WorldPos;
    public Vector3 Velocity;
    public string Text;
    public Color TextColor;
    public float Life;
    public float MaxLife;
    public bool IsCritical;
}

public class WeaponAndCombatView
{
    // Current weapon & unlock state
    internal FloorSystems Inventory=new();
    private WeaponType equipped=WeaponType.BareHands;
    public WeaponType CurrentWeapon
    {
        get => equipped;
        set
        {
            if(!Inventory.HasWeapon(value) || value!=WeaponType.BareHands && DrawBlocked?.Invoke()==true) return;
            equipped=value;
            selectedEmptySlot=-1;
        }
    }
    private void SetOwned(WeaponType weapon,bool value)
    {
        int bit=1<<(int)weapon;
        Inventory.OwnedWeaponsMask=value ? Inventory.OwnedWeaponsMask|bit : Inventory.OwnedWeaponsMask&~bit;
    }
    public bool UnlockedSolderingIron { get => Inventory.HasWeapon(WeaponType.SolderingIron); set => SetOwned(WeaponType.SolderingIron,value); }
    public bool UnlockedMicroUzi { get => Inventory.HasWeapon(WeaponType.MicroUzi); set => SetOwned(WeaponType.MicroUzi,value); }
    public bool UnlockedSawedOff { get => Inventory.HasWeapon(WeaponType.SawedOff); set => SetOwned(WeaponType.SawedOff,value); }
    public bool UnlockedChatGun { get => Inventory.HasWeapon(WeaponType.ChatGun); set => SetOwned(WeaponType.ChatGun,value); }
    public Color SkinTintPrimary = new Color(0, 0, 0, 0); // Alpha 0 = default authentic 90s look
    public Color SkinTintAccent = new Color(255, 215, 0, 255);
    public string CurrentSkinName = "Классика 1994 (Сток)";

    // Animation phases & timers
    public float BeltSwingPhase = 0f;       // 0 = idle, >0 = active whip swing (1.0 -> 0.0)
    public int BeltSwingCombo = 0;          // Alternates diagonal slash angle
    public float SolderThrustPhase = 0f;    // 0 = idle, >0 = active thrust & twist (1.0 -> 0.0)
    public float SolderHeatGlow = 0.65f;    // 0.5..1.0 glowing copper tip intensity
    public float UziRecoilPhase => CurrentWeapon==WeaponType.MicroUzi ? -_kick.Value.Z/.045f : 0;
    public float ShotgunKickPhase => CurrentWeapon!=WeaponType.MicroUzi ? -_kick.Value.Z/.085f : 0;
    public float UziBoltOffset => _bolt.Value;
    public float EquipTransition => _draw.Value;

    // Hitmarker & screen feedback
    public float HitmarkerTimer = 0f;
    public bool HitmarkerIsCrit = false;
    public string LastCombatBannerText = "";
    public float LastCombatBannerTimer = 0f;
    public Color LastCombatBannerColor = Color.Gold;

    // 3D Particle Buffers
    public readonly List<SmokeParticle3D> SmokeParticles = new(256);
    public readonly List<SparkParticle3D> SparkParticles = new(512);
    public readonly List<WhipSlashRibbon3D> WhipSlashes = new(32);
    public readonly List<BrassShell3D> BrassShells = new(128);
    public readonly List<BulletTracer3D> BulletTracers = new(128);
    public readonly List<FloatingCombatText3D> FloatingTexts = new(64);

    private int _uziRounds = 32, _shells = 2, _spentHulls;
    private float _reloadTime, _flash;
    private const float UziFlashTime=.038f, ShotgunFlashTime=.055f;
    private float FlashDuration => CurrentWeapon==WeaponType.MicroUzi ? UziFlashTime : ShotgunFlashTime;
    private float FlashStrength => MathF.Sqrt(Math.Clamp(_flash/FlashDuration,0,1));
    private float _flashAngle;
    // Exact underdamped solution of x'' = w*w*(target-x) - 2*z*w*x'.
    // Split long ticks and retain velocity across changes of target.
    private struct Spring3
    {
        public Vector3 Value, Velocity;
        public void Advance(Vector3 target, float dt, float frequency, float damping)
        {
            float decay=damping*frequency, wave=frequency*MathF.Sqrt(1-damping*damping);
            while(dt>0)
            {
                float h=Math.Min(dt,1f/60), c=MathF.Cos(wave*h), s=MathF.Sin(wave*h)/wave;
                float e=MathF.Exp(-decay*h);
                Vector3 x=Value-target, v=Velocity;
                Value=target+e*(x*c+(v+decay*x)*s);
                Velocity=e*(v*c-(frequency*frequency*x+decay*v)*s);
                dt-=h;
            }
        }
    }

    private struct SpringFloat
    {
        private Spring3 _state;
        public float Value => _state.Value.X;
        public void Start(float value) => _state=new Spring3 { Value=Vector3.UnitX*value };
        public void Impulse(float velocity) => _state.Velocity.X+=velocity;
        public void Advance(float target,float dt,float frequency,float damping) =>
            _state.Advance(Vector3.UnitX*target,dt,frequency,damping);
    }

    private Spring3 _kick, _muzzleRotation, _stance, _body, _lookLagSpring, _sway, _bore;
    private SpringFloat _draw, _landing, _walk, _receiverBump, _bolt;
    private Vector3 _pendingClimb;
    private float _climbDelay;
    private bool _wasGrounded=true;
    internal SkyscraperFloor1World BallisticWorld;
    private float _stridePhase;
    private bool _lowStance;
    internal float WalkWeight => _walkWeight;
    internal void SetStance(float stride,bool crouched) { _stridePhase=stride; _lowStance=crouched; }
    private float sprintWeight => _stance.Value.X;
    private float crouchWeight => _stance.Value.Y;
    private float strafeBank => _stance.Value.Z;
    private float dashWeight => _body.Value.X;
    private float airLift => _body.Value.Y;
    internal void UpdateMotion(float dt, bool isSprinting, bool isGrounded, float verticalVel,
        float landingImpact, bool isDashing, float strafeTilt)
    {
        _stance.Advance(new(isSprinting && isGrounded ? 1 : 0,_lowStance ? 1 : 0,strafeTilt),dt,16,.78f);
        _body.Advance(new(isDashing ? 1 : 0,isGrounded ? 0 : Math.Clamp(-verticalVel/8,-1,1),0),dt,12,.76f);
        if (_wasGrounded && !isGrounded && verticalVel>0) _landing.Impulse(.095f*verticalVel);
        _landing.Impulse(.24f*Math.Clamp(landingImpact,0,12));
        _landing.Advance(0,dt,17,.64f);
        _wasGrounded=isGrounded;
    }
    internal float LandingCompression => _landing.Value*.92f;
    internal bool Drum, InsulatedIron;
    internal float IronHeat, IronLockout;
    internal void CoolIron(float dt)
    {
        IronLockout=Math.Max(0,IronLockout-dt);
        IronHeat=Math.Max(0,IronHeat-dt*(InsulatedIron ? .37f : .22f));
    }
    internal void HeatIron()
    {
        IronHeat=Math.Min(1,IronHeat+(InsulatedIron ? .42f : .28f));
        if(IronHeat>=.999f) IronLockout=InsulatedIron ? 1.05f : 1.65f;
    }
    internal int Capacity => CurrentWeapon switch { WeaponType.MicroUzi=>Drum ? 50 : 32, WeaponType.SawedOff=>2, _=>0 };
    internal int Ammo => CurrentWeapon switch { WeaponType.MicroUzi=>_uziRounds, WeaponType.SawedOff=>_shells, _=>-1 };
    internal float ClosingImpulse => CurrentWeapon==WeaponType.SawedOff ? _receiverBump.Value : 0;
    internal bool Reloading => _reloadTime > 0;
    private const float ShotgunReloadDuration = 1.55f;
    private float ReloadDuration => CurrentWeapon==WeaponType.MicroUzi ? 1.45f*(Drum ? 1.3f : 1) : ShotgunReloadDuration;
    private float _inspectPhaseAtReload;
    internal float ReloadProgress => Reloading ? 1-_reloadTime/ReloadDuration : 0;
    internal void UseRound()
    {
        if (CurrentWeapon==WeaponType.MicroUzi) _uziRounds--;
        if (CurrentWeapon==WeaponType.SawedOff) { _shells--; _spentHulls++; }
    }
    internal void BeginReload()
    {
        if (Reloading || Ammo<0 || Ammo==Capacity) return;
        _inspectPhaseAtReload=InspectPhase;
        _inspectTimer=0;
        _reloadTime=ReloadDuration;
        AudioMusicSystem.PlayActionClack(CurrentWeapon==WeaponType.SawedOff ? .72f : 1.30f);
    }

    private void TickReload(float dt, Camera3D camera)
    {
        if (!Reloading) { _receiverBump.Advance(0,dt,26,.64f); return; }
        float before=ReloadProgress;
        _reloadTime-=dt;
        // Repeated float subtraction at 60 Hz can leave a fraction of a
        // microsecond and hold the last animation frame for a whole tick.
        if (_reloadTime < .000005f) _reloadTime=0;
        float after=1-_reloadTime/ReloadDuration;
        if (before<.18f && after>=.18f && CurrentWeapon==WeaponType.SawedOff)
        {
            var gun=GunAssembly(BaseOffset());
            var toWorld=gun.Pose*GunToWorld(camera);
            for(int i=0;i<_spentHulls;i++)
            {
                var (entry,axis)=Chamber(gun.Receiver,BarrelAngle(after),i+_shells);
                Vector3 worldEntry=Vector3.Transform(entry,toWorld);
                Vector3 kick=Vector3.Normalize(Vector3.TransformNormal(-axis,toWorld));
                BrassShells.Add(new() { Position=worldEntry,Velocity=kick*.65f+Vector3.Normalize(Vector3.Cross(camera.Target-camera.Position,camera.Up))*(2.4f+i*.35f)+Vector3.UnitY*1.75f,
                    AngularVelocity=new(300,510,270),Life=2.6f,MaxLife=2.6f,IsShotgunShell=true });
                Puff(worldEntry,kick*.16f+Vector3.UnitY*.14f,.012f,.078f,.32f,new Color(175,161,147,105));
            }
            _spentHulls=0;
        }
        bool uzi=CurrentWeapon==WeaponType.MicroUzi;
        float elapsed=0;
        void Impact(float phase,float velocity,float sound)
        {
            if(before>=phase || after<phase) return;
            float when=(phase-before)*ReloadDuration;
            _receiverBump.Advance(0,when-elapsed,26,.64f);
            elapsed=when;
            _receiverBump.Impulse(velocity);
            AudioMusicSystem.PlayActionClack(sound);
        }
        if(uzi) { Impact(.56f,22,1.6f); Impact(.81f,-10,1.05f); }
        else
        {
            if(_shells==0) Impact(.57f,7,1.15f);
            Impact(_shells==1 ? .63f : .68f,8,1.25f);
            Impact(.89f,34,.67f);
        }
        _receiverBump.Advance(0,Math.Max(0,dt-elapsed),26,.64f);
        if (_reloadTime==0)
        {
            if (CurrentWeapon==WeaponType.MicroUzi) _uziRounds=Capacity;
            else _shells=2;
        }
    }

    private readonly Random _rng = new(1994);
    private float _idleSmokeAccumulator = 0f;
    private float _continuousSolderSparkTimer = 0f;
    private float _animClock = 0f;
    private const float _inspectDuration = 2.4f;
    private float _inspectTimer;
    private const float _flipOffDuration = 1.45f;
    internal float FlipOffTimer { get; private set; }
    private float _walkWeight => _walk.Value;

    internal bool Inspecting => _inspectTimer > 0;
    internal bool FlippingOff => FlipOffTimer > 0;
    private float InspectPhase => Inspecting ? 1 - _inspectTimer / _inspectDuration : 0;

    public void TriggerInspect()
    {
        if (!Inspecting && !Reloading && !FlippingOff) _inspectTimer = _inspectDuration;
    }

    public void TriggerFlipOff(float duration = _flipOffDuration)
    {
        _inspectTimer = 0;
        FlipOffTimer = MathF.Max(FlipOffTimer, duration);
    }

    public void UpdateLookLag(Vector2 angularDelta, float dt)
    {
        Vector2 speed=Vector2.Clamp(angularDelta/Math.Max(dt,.001f),new(-4),new(4));
        _lookLagSpring.Advance(new Vector3(speed*.045f,0),dt,18,.82f);
    }

    private static float Ease(float t)
    {
        t = Math.Clamp(t, 0, 1);
        return t*t*(3-2*t);
    }

    private static float InspectLift(float t) => Ease(t/.28f)*Ease((1-t)/.23f);

    private static Matrix4x4 GetInspectTransform(int slot, float t, float weight=1)
    {
        float lift = InspectLift(t)*weight;
        if (lift == 0) return Matrix4x4.Identity;
        float turn = Ease((t-.37f)/.31f);
        Vector3 pivot = new(.18f,-.15f,.62f);
        (Vector3 travel, Vector3 degrees) = slot switch
        {
            1 => (new Vector3(-.115f,.072f,-.015f), new Vector3(-16,-24+34*turn,10-28*turn)),
            2 => (new Vector3(-.105f,.062f,-.04f), new Vector3(-6,-12,-18*turn)),
            // The receiver already has a -16 degree resting yaw.
            3 => (new Vector3(-.135f,.085f,.065f), new Vector3(-9,-22+70*turn,12-23*turn)),
            _ => (new Vector3(-.13f,.09f,.055f), new Vector3(-12+34*turn,-35+8*turn,9+11*turn))
        };
        Vector3 a = degrees*(MathF.PI/180*lift);
        return Matrix4x4.CreateTranslation(-pivot)
            * Matrix4x4.CreateFromYawPitchRoll(a.Y,a.X,a.Z)
            * Matrix4x4.CreateTranslation(pivot+travel*lift);
    }

    public void SelectSlot(int slotNumber)
    {
        // Legacy equipment and cosmetic APIs use the original four weapon IDs.
        if(slotNumber==4 && UnlockedChatGun && (!UnlockedSawedOff || CurrentWeapon==WeaponType.SawedOff))
            Equip(WeaponType.ChatGun);
        else if(slotNumber is >=1 and <=4) Equip((WeaponType)slotNumber);
    }

    private int hotbarMask=-1;
    private WeaponType?[] hotbar=[];
    private int selectedEmptySlot=-1;
    internal Func<bool> DrawBlocked;
    internal WeaponType?[] Hotbar
    {
        get
        {
            if(hotbarMask!=Inventory.OwnedWeaponsMask)
            {
                hotbarMask=Inventory.OwnedWeaponsMask;
                var owned=FloorSystems.WeaponOrder.Where(Inventory.HasWeapon).ToArray();
                hotbar=new WeaponType?[Math.Max(4,owned.Length)];
                for(int i=0;i<owned.Length;i++) hotbar[i]=owned[i];
                if(selectedEmptySlot>=hotbar.Length || selectedEmptySlot>=0 && hotbar[selectedEmptySlot]!=null) selectedEmptySlot=-1;
            }
            return hotbar;
        }
    }
    internal int HotbarSlotCount => Hotbar.Length;
    internal int SelectedHotbarSlot
    {
        get
        {
            var slots=Hotbar;
            return CurrentWeapon==WeaponType.BareHands ? selectedEmptySlot :
                Array.IndexOf(slots,CurrentWeapon==WeaponType.ChatGun ? WeaponType.SawedOff : CurrentWeapon);
        }
    }

    internal void SelectOwnedSlot(int index,bool toggle=true)
    {
        if((uint)index>=(uint)HotbarSlotCount) return;
        if(Hotbar[index] is {} weapon)
            Equip(toggle && SelectedHotbarSlot==index ? WeaponType.BareHands : weapon);
        else
        {
            Equip(WeaponType.BareHands);
            selectedEmptySlot=index;
        }
    }

    internal void ScrollHotbar(int direction)
    {
        if(direction==0) return;
        int count=HotbarSlotCount, index=SelectedHotbarSlot;
        if(index<0) index=direction>0 ? 0 : count-1;
        SelectOwnedSlot((index+count-Math.Sign(direction))%count,toggle:false);
    }

    internal void Equip(WeaponType target)
    {
        var previous=CurrentWeapon;
        CurrentWeapon=target;
        if(CurrentWeapon==previous) return;
        _inspectTimer=_reloadTime=_flash=0;
        StartDraw(1);
    }

    private void StartDraw(float amount)
    {
        _netSwingTime=0;
        _draw.Start(amount);
        _kick=_muzzleRotation=_bore=default;
        _pendingClimb=Vector3.Zero;
        _receiverBump=_bolt=default;
    }

    public void ApplySkin(string skinName, Color primary, Color accent)
    {
        CurrentSkinName = skinName;
        SkinTintPrimary = primary;
        SkinTintAccent = accent;
        StartDraw(.65f);
    }

    /// <summary>
    /// Triggers the weapon's signature attack animation, 3D world particles, tracers, shells, and floating text.
    /// Call this when the player presses/holds LMB and weapon cooldown permits firing.
    /// </summary>
    public void TriggerWeaponAttack(
        Camera3D camera,
        Vector3? hitWorldPoint = null,
        bool hitEnemy = false,
        int damageDealt = 0,
        bool isCritical = false,
        string customHitLabel = null,
        Vector3? receiverCenter = null,
        Vector3? shotDirection = null)
    {
        _inspectTimer = 0;
        if(CurrentWeapon is WeaponType.CaptureNet or WeaponType.BareHands)
        {
            if(CurrentWeapon==WeaponType.CaptureNet) _netSwingTime=NetSwingDuration;
            else _catchSpring.Impulse(36);
            if(hitEnemy) ShowImpact(camera,receiverCenter ?? hitWorldPoint ?? camera.Target,damageDealt,isCritical);
            return;
        }
        Vector3 forward = shotDirection ?? Vector3.Normalize(camera.Target-camera.Position);
        if (CurrentWeapon is WeaponType.SawedOff or WeaponType.ChatGun or WeaponType.MicroUzi)
        {
            _flash=FlashDuration;
            _flashAngle=NextFloat()*MathF.Tau;
            bool uzi=CurrentWeapon==WeaponType.MicroUzi;
            float strength=_lowStance ? .75f : 1;
            _kick.Velocity+=new Vector3(0,uzi ? .10f : .18f,uzi ? -2.4f : -4.4f)*strength;
            _pendingClimb+=new Vector3(uzi ? -3.6f : -8.6f,0,(NextFloat()-.5f)*(uzi ? 1.6f : 3.2f))*strength;
            _climbDelay=FlashDuration;
            if (uzi) _bolt.Impulse(82);
        }
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));
        Vector3 muzzleWorld = GetWeaponTipWorldPosition(camera);
        Vector3 impactPoint = hitWorldPoint ?? (camera.Position + forward * 3.2f);
        if (hitEnemy)
        {
            TriggerHitmarker(isCritical);
            if (damageDealt > 0)
                SpawnFloatingText(receiverCenter ?? impactPoint, right, damageDealt, isCritical);
            else if (customHitLabel != null)
                SetCombatBanner(customHitLabel, new Color(250,219,164,255));
        }

        switch (CurrentWeapon)
        {
            case WeaponType.Belt:
            {
                BeltSwingPhase = 1.0f;
                BeltSwingCombo = (BeltSwingCombo + 1) % 2;

                // Create 3D golden-crimson whip slash arc ribbon in front of player
                float sideSign = (BeltSwingCombo == 0) ? 1f : -1f;
                Vector3 arcStart = camera.Position + forward * 1.15f + right * (0.85f * sideSign) + up * 0.45f;
                Vector3 arcControl = camera.Position + forward * 2.35f + right * (0.05f * sideSign) + up * 0.05f;
                Vector3 arcEnd = camera.Position + forward * 1.25f - right * (0.85f * sideSign) - up * 0.40f;

                Color slashPrimary = SkinTintPrimary.A > 0 ? SkinTintPrimary : new Color(230, 45, 55, 255);
                Color slashCore = new Color(255, 215, 60, 255);

                WhipSlashes.Add(new WhipSlashRibbon3D
                {
                    Start = arcStart,
                    Control = arcControl,
                    End = arcEnd,
                    Life = 0.28f,
                    MaxLife = 0.28f,
                    PrimaryColor = slashPrimary,
                    CoreColor = slashCore
                });

                // Leather slap impact burst
                if (hitWorldPoint.HasValue) SpawnBurstSparks(impactPoint, -forward, count: hitEnemy ? 22 : 12,
                    colorA: new Color(255, 210, 65, 255),
                    colorB: new Color(220, 60, 40, 255),
                    speedMin: 2.5f, speedMax: 6.5f, electric: false);

                break;
            }

            case WeaponType.SolderingIron:
            {
                SolderThrustPhase = 1.0f;
                SolderHeatGlow = 1.0f;

                // Thick rosin smoke rings + hot solder & cyan electric sparks at impact
                for (int i = 0; i < 6; i++)
                {
                    SmokeParticles.Add(new SmokeParticle3D
                    {
                        Position = muzzleWorld + forward * (0.25f + i * 0.12f) + RandomVec3(0.05f),
                        Velocity = forward * (1.2f + i * 0.25f) + new Vector3(0f, 0.85f, 0f) + RandomVec3(0.35f),
                        Size = 0.08f,
                        MaxSize = 0.36f + (i * 0.04f),
                        Life = 0.85f,
                        MaxLife = 0.85f,
                        Tint = new Color(215, 225, 235, 210),
                        IsRing = (i % 2 == 0)
                    });
                }

                // Hot orange/yellow solder droplets + cyan electric stun arcs
                if (hitWorldPoint.HasValue) SpawnBurstSparks(impactPoint, -forward, count: hitEnemy ? 34 : 20,
                    colorA: new Color(255, 185, 30, 255),
                    colorB: new Color(255, 93, 14, 255),
                    speedMin: 3.0f, speedMax: 8.5f, electric: true);

                // Electric arc tracer from soldering tip to target
                BulletTracers.Add(new BulletTracer3D
                {
                    Start = muzzleWorld,
                    End = impactPoint,
                    Life = 0.16f,
                    MaxLife = 0.16f,
                    TracerColor = new Color(90, 240, 255, 255),
                    Thickness = 0.045f
                });

                break;
            }

            case WeaponType.MicroUzi:
            {
                Vector3 targetPos = hitWorldPoint ?? (camera.Position + forward * 26f);
                AddTracer(muzzleWorld,targetPos,.038f,.0045f,.035f,new(255,236,196,132));
                SpawnPowder(muzzleWorld,forward,3,false);

                // Eject spinning 3D brass shell casing to the right-up
                Vector3 ejectPos = muzzleWorld - forward * 0.16f + right * 0.08f + up * 0.05f;
                Vector3 ejectVel = right * (2.4f + NextFloat() * 1.2f)
                                 + up * (2.2f + NextFloat() * 0.9f)
                                 - forward * (0.4f + NextFloat() * 0.5f);
                BrassShells.Add(new BrassShell3D
                {
                    Position = ejectPos,
                    Velocity = ejectVel,
                    Rotation = new Vector3(NextFloat() * 360f, NextFloat() * 360f, NextFloat() * 360f),
                    AngularVelocity = new Vector3(520f, 740f, 610f),
                    Life = 2.2f,
                    MaxLife = 2.2f,
                    IsShotgunShell = false
                });

                if (hitWorldPoint.HasValue) SpawnImpact(targetPos,-forward,5);

                break;
            }

            case WeaponType.SawedOff:
            case WeaponType.ChatGun:
            {
                bool isChat = (CurrentWeapon == WeaponType.ChatGun);
                float range=isChat ? 28 : 12.5f;
                for (int i=0;i<8;i++)
                {
                    var direction=PelletDirection(forward,i);
                    float length=BallisticWorld?.PelletDistance(camera.Position,direction,range) ?? range;
                    var end=i==0 && hitWorldPoint.HasValue ? impactPoint : camera.Position+direction*length;
                    if (i%2==1)
                    {
                        var tint=isChat ? new Color(151,255,205,102) : new Color(255,231,193,_rng.Next(85,126));
                        AddTracer(muzzleWorld,end,.045f,.005f+.003f*NextFloat(),.24f+.32f*NextFloat(),tint);
                    }
                    if (length<range || (i==0 && hitWorldPoint.HasValue))
                        SpawnImpact(end,-direction,2+_rng.Next(2));
                }
                SpawnPowder(muzzleWorld,forward,6,true);

                break;
            }
        }
    }

    private void AddTracer(Vector3 start,Vector3 end,float life,float width,float startFraction,Color tint)
    {
        BulletTracers.Add(new() { Start=start,End=end,Life=life,MaxLife=life,Thickness=width,
                                  StartFraction=startFraction,TracerColor=tint });
    }

    private void Puff(Vector3 position,Vector3 velocity,float size,float maxSize,float life,Color tint)
    {
        SmokeParticles.Add(new() { Position=position,Velocity=velocity,Size=size,InitialSize=size,MaxSize=maxSize,
            Rotation=NextFloat()*MathF.Tau,Life=life,MaxLife=life,Tint=tint,IsPowder=true });
    }

    private void SpawnPowder(Vector3 tip,Vector3 forward,int count,bool heavy)
    {
        for (int i=0;i<count;i++)
        {
            float size=heavy ? .064f : .042f;
            Puff(tip+forward*(i*.018f),forward*(.35f+i*.18f)+Vector3.UnitY*(.28f+NextFloat()*.36f)+RandomVec3(.15f),
                 size,.10f+size*2+NextFloat()*.06f,.35f+NextFloat()*.30f,new(204,199,191,heavy ? 111 : 82));
        }
    }

    private void SpawnImpact(Vector3 hit,Vector3 outward,int sparks)
    {
        Puff(hit,outward*.26f+Vector3.UnitY*.19f,.047f,.12f+NextFloat()*.05f,.23f+NextFloat()*.13f,new(213,209,201,119));
        for (int i=0;i<=sparks;i++)
        {
            bool debris=i==sparks;
            float life=.075f+NextFloat()*.095f;
            var velocity=(outward*.50f+RandomVec3(1))*(1.5f+NextFloat()*2.3f);
            SparkParticles.Add(new() { Position=hit,PrevPosition=hit,Velocity=velocity,Life=life,MaxLife=life,
                ColorMain=debris ? new(97,87,76,210) : new(255,219,164,240),
                BallisticWidth=debris ? .007f : .002f+NextFloat()*.002f,IsDebris=debris });
        }
    }

    public void TriggerHitmarker(bool isCritical = false)
    {
        HitmarkerTimer = 0.22f;
        HitmarkerIsCrit = isCritical;
    }

    public void SetCombatBanner(string text, Color color, float duration = 1.6f)
    {
        LastCombatBannerText = text;
        LastCombatBannerColor = color;
        LastCombatBannerTimer = duration;
    }

    internal Vector3 PelletDirection(Vector3 forward,int pellet)
    {
        if(pellet==0) return forward;
        Vector3 right=Vector3.Normalize(Vector3.Cross(forward,Vector3.UnitY));
        Vector3 up=Vector3.Cross(right,forward);
        float angle=pellet*MathF.Tau/7;
        float radius=(pellet%2==1 ? .066f : .030f)*(_lowStance ? .65f : 1);
        return Vector3.Normalize(forward+(right*MathF.Cos(angle)+up*MathF.Sin(angle))*radius);
    }

    internal void ShowImpact(Camera3D camera,Vector3 center,int damage,bool critical)
    {
        TriggerHitmarker(critical);
        if(damage>0) SpawnFloatingText(center,Vector3.Normalize(Vector3.Cross(camera.Target-camera.Position,Vector3.UnitY)),damage,critical);
    }

    private void SpawnFloatingText(Vector3 center, Vector3 right, int damage, bool isCritical)
    {
        FloatingTexts.Add(new FloatingCombatText3D
        {
            WorldPos = center + right * .38f + Vector3.UnitY * .45f,
            Velocity = right * .72f + Vector3.UnitY * .38f,
            Text = $"-{damage}",
            TextColor = isCritical ? new Color(255,220,163,255) : new Color(247,245,234,255),
            Life = .78f,
            MaxLife = .78f,
            IsCritical = isCritical
        });
    }

    public void SpawnBurstSparks(
        Vector3 origin,
        Vector3 normal,
        int count,
        Color colorA,
        Color colorB,
        float speedMin,
        float speedMax,
        bool electric)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = Vector3.Normalize(normal * 0.55f + RandomVec3(1.0f));
            float speed = speedMin + NextFloat() * (speedMax - speedMin);
            SparkParticles.Add(new SparkParticle3D
            {
                Position = origin + RandomVec3(0.04f),
                PrevPosition = origin,
                Velocity = dir * speed + new Vector3(0f, 1.4f, 0f),
                Life = 0.25f + NextFloat() * 0.28f,
                MaxLife = 0.45f,
                ColorMain = (i % 2 == 0) ? colorA : colorB,
                ColorCore = Color.White,
                IsElectricArc = electric && (i % 3 == 0)
            });
        }
    }

    /// <summary>
    /// Updates weapon animation timers, rising rosin smoke from the Soldering Iron, sparks, whip ribbons, shells, and floating text.
    /// </summary>
    public void UpdateWeapons(float dt, Camera3D camera, bool isMoving = false, bool holdingFire = false)
    {
        _animClock += dt;
        float remaining=_netSwingTime;
        _netSwingTime=Math.Max(0,remaining-dt);
        if(remaining>NetSwingDuration*.69f && _netSwingTime<=NetSwingDuration*.69f)
        {
            Vector3 ahead=Vector3.Normalize(camera.Target-camera.Position);
            SpawnBurstSparks(camera.Position+ahead*.95f-Vector3.UnitY*.19f,ahead,6,
                new(135,225,211,255),new(176,235,217,255),.3f,1.1f,false);
        }
        _catchSpring.Advance(0,dt,18,.62f);
        _flash=Math.Max(0,_flash-dt);
        TickReload(dt,camera);
        _inspectTimer = MathF.Max(0,_inspectTimer-dt);
        FlipOffTimer = MathF.Max(0, FlipOffTimer - dt);
        _walk.Advance(isMoving ? 1 : 0,dt,18,.86f);
        float stride=_walkWeight*(1-.62f*crouchWeight);
        float width=.017f+.025f*sprintWeight;
        Vector3 breath=new(.0035f*MathF.Sin(_animClock*1.41f+.7f),
            .003f*MathF.Sin(_animClock*2.09f)+.0018f*MathF.Sin(_animClock*.83f),.0026f*MathF.Cos(_animClock*1.41f));
        _sway.Advance(breath+stride*new Vector3(width*MathF.Sin(_stridePhase),
            width*.58f*MathF.Sin(2*_stridePhase),width*.38f*MathF.Cos(_stridePhase)),dt,30,.78f);
        _draw.Advance(0,dt,12,.68f);
        _kick.Advance(Vector3.Zero,dt,23,.69f);
        _bolt.Advance(0,dt,39,.76f);
        float lag=Math.Min(dt,_climbDelay);
        _muzzleRotation.Advance(Vector3.Zero,lag,18,.69f);
        _climbDelay-=lag;
        if (_climbDelay<=0)
        {
            _muzzleRotation.Velocity+=_pendingClimb;
            _pendingClimb=Vector3.Zero;
        }
        _muzzleRotation.Advance(Vector3.Zero,dt-lag,18,.69f);
        // Converge before the shot. Firing only changes velocities.
        if (Capacity>0 || CurrentWeapon==WeaponType.ChatGun)
            TrackBore(camera,dt);

        if (BeltSwingPhase > 0f)
            BeltSwingPhase = MathF.Max(0f, BeltSwingPhase - dt * 3.2f);

        if (SolderThrustPhase > 0f)
            SolderThrustPhase = MathF.Max(0f, SolderThrustPhase - dt * 2.8f);

        if (HitmarkerTimer > 0f)
            HitmarkerTimer = MathF.Max(0f, HitmarkerTimer - dt);

        if (LastCombatBannerTimer > 0f)
            LastCombatBannerTimer = MathF.Max(0f, LastCombatBannerTimer - dt);

        // Soldering iron tip heat pulse & constant rising rosin smoke ("дымок канифоли")
        if (CurrentWeapon == WeaponType.SolderingIron)
        {
            float targetGlow = (SolderThrustPhase > 0.05f || holdingFire) ? 1.0f : (0.68f + 0.14f * MathF.Sin(_animClock * 5.5f));
            SolderHeatGlow = Lerp(SolderHeatGlow, targetGlow, dt * 8f);

            if(!Inspecting) _idleSmokeAccumulator += dt * (SolderThrustPhase > 0.05f ? 32f : 12f);
            while (_idleSmokeAccumulator >= 1f)
            {
                _idleSmokeAccumulator -= 1f;
                Vector3 tipWorld = GetWeaponTipWorldPosition(camera);
                SmokeParticles.Add(new SmokeParticle3D
                {
                    Position = tipWorld + RandomVec3(0.015f),
                    Velocity = new Vector3(
                        (NextFloat() - 0.5f) * 0.22f,
                        0.55f + NextFloat() * 0.35f,
                        (NextFloat() - 0.5f) * 0.22f),
                    Size = 0.028f,
                    MaxSize = 0.16f + NextFloat() * 0.08f,
                    Life = 0.95f,
                    MaxLife = 0.95f,
                    Tint = new Color(200, 210, 222, 165),
                    IsRing = (SolderThrustPhase > 0.2f && _rng.Next(3) == 0)
                });
            }

            // While thrusting/holding LMB with Soldering Iron, shower continuous solder sparks from the tip
            if (SolderThrustPhase > 0.15f || holdingFire)
            {
                _continuousSolderSparkTimer += dt * 45f;
                while (_continuousSolderSparkTimer >= 1f)
                {
                    _continuousSolderSparkTimer -= 1f;
                    Vector3 tipWorld = GetWeaponTipWorldPosition(camera);
                    Vector3 forward = Vector3.Normalize(camera.Target - camera.Position);
                    SpawnBurstSparks(tipWorld + forward * 0.08f, forward, count: 2,
                        colorA: new Color(255, 200, 50, 255),
                        colorB: new Color(80, 240, 255, 255),
                        speedMin: 1.6f, speedMax: 4.4f, electric: true);
                }
            }
        }

        // Update 3D Smoke Particles
        for (int i = SmokeParticles.Count - 1; i >= 0; i--)
        {
            var p = SmokeParticles[i];
            p.Life -= dt;
            if (p.Life <= 0f)
            {
                SmokeParticles.RemoveAt(i);
                continue;
            }
            float t = 1f - (p.Life / p.MaxLife);
            p.Position += p.Velocity * dt;
            p.Velocity.X *= (1f - dt * 1.4f);
            p.Velocity.Z *= (1f - dt * 1.4f);
            p.Velocity.Y += dt * 0.25f; // Buoyant rising smoke
            p.Size = Lerp(p.IsPowder ? p.InitialSize : .03f, p.MaxSize, t);
            SmokeParticles[i] = p;
        }

        // Update 3D Spark Particles
        for (int i = SparkParticles.Count - 1; i >= 0; i--)
        {
            var s = SparkParticles[i];
            s.Life -= dt;
            if (s.Life <= 0f)
            {
                SparkParticles.RemoveAt(i);
                continue;
            }
            s.PrevPosition = s.Position;
            s.Position += s.Velocity * dt;
            s.Velocity.Y -= 9.8f * dt; // Gravity
            if (s.Position.Y < 0.03f)
            {
                s.Position.Y = 0.03f;
                s.Velocity.Y = MathF.Abs(s.Velocity.Y) * 0.45f;
                s.Velocity.X *= 0.7f;
                s.Velocity.Z *= 0.7f;
            }
            SparkParticles[i] = s;
        }

        // Update 3D Whip Slash Ribbons
        for (int i = WhipSlashes.Count - 1; i >= 0; i--)
        {
            var w = WhipSlashes[i];
            w.Life -= dt;
            if (w.Life <= 0f)
            {
                WhipSlashes.RemoveAt(i);
                continue;
            }
            WhipSlashes[i] = w;
        }

        // Update 3D Brass Shells (with floor bounce)
        for (int i = BrassShells.Count - 1; i >= 0; i--)
        {
            var b = BrassShells[i];
            b.Life -= dt;
            if (b.Life <= 0f)
            {
                BrassShells.RemoveAt(i);
                continue;
            }
            b.Position += b.Velocity * dt;
            b.Velocity.Y -= 13.5f * dt;
            b.Rotation += b.AngularVelocity * dt;

            if (b.Position.Y < 0.04f)
            {
                b.Position.Y = 0.04f;
                b.Velocity.Y = MathF.Abs(b.Velocity.Y) * 0.42f;
                b.Velocity.X *= 0.65f;
                b.Velocity.Z *= 0.65f;
                b.AngularVelocity *= 0.6f;
            }
            BrassShells[i] = b;
        }

        // Update 3D Bullet Tracers
        for (int i = BulletTracers.Count - 1; i >= 0; i--)
        {
            var tr = BulletTracers[i];
            tr.Life -= dt;
            if (tr.Life <= 0f)
            {
                BulletTracers.RemoveAt(i);
                continue;
            }
            BulletTracers[i] = tr;
        }

        // Update Floating 3D Combat Texts
        for (int i = FloatingTexts.Count - 1; i >= 0; i--)
        {
            var ft = FloatingTexts[i];
            ft.Life -= dt;
            if (ft.Life <= 0f)
            {
                FloatingTexts.RemoveAt(i);
                continue;
            }
            ft.WorldPos += ft.Velocity * dt;
            ft.Velocity *= (1f - dt * 2.2f);
            FloatingTexts[i] = ft;
        }
    }

    private Matrix4x4 GunToWorld(Camera3D camera)
    {
        Vector3 f=Vector3.Normalize(camera.Target-camera.Position);
        Vector3 r=Vector3.Normalize(Vector3.Cross(f,camera.Up));
        Vector3 u=Vector3.Cross(r,f);
        float lens=MathF.Tan(camera.FovY*MathF.PI/360)/MathF.Tan(ViewModelFov*MathF.PI/360);
        r*=lens; u*=lens;
        Vector3 p=camera.Position;
        // +X in the mirrored viewmodel points screen-right; preserve projected XY at the world lens.
        return Matrix4x4.CreateRotationZ(ViewRoll) * new Matrix4x4(r.X,r.Y,r.Z,0, u.X,u.Y,u.Z,0,
            f.X,f.Y,f.Z,0, p.X,p.Y,p.Z,1);
    }

    private const float ViewModelFov=54;
    private float ViewRoll => (-15*EquipTransition+_lookLagSpring.Value.X*24-5.5f*strafeBank
        +7*crouchWeight-16*sprintWeight-8*dashWeight+_landing.Value*23)*MathF.PI/180;

    private void TrackBore(Camera3D camera,float dt)
    {
        // Only converge the neutral stance. Keep the sprint, reload and recoil rotations additive.
        bool uzi=CurrentWeapon==WeaponType.MicroUzi;
        Vector3 receiver=BaseOffset()+(uzi ? new Vector3(.215f,-.10f,.68f) : new Vector3(.18f,-.12f,.66f));
        Vector3 target=camera.Position+Vector3.Normalize(camera.Target-camera.Position)*24;
        Matrix4x4.Invert(GunToWorld(camera),out var toLocal);
        Vector3 line=Vector3.Transform(target,toLocal)-receiver-new Vector3(MuzzleOffset.X,MuzzleOffset.Y,0);
        Vector3 angles=new(-MathF.Atan2(line.Y,line.Z),MathF.Atan2(line.X,line.Z),0);
        _bore.Advance(angles,dt,18,.9f);
    }

    public Vector3 GetWeaponTipWorldPosition(Camera3D camera)
    {
        if (Capacity>0 || CurrentWeapon==WeaponType.ChatGun)
            return Vector3.Transform(GunAssembly(BaseOffset()).Tip,GunToWorld(camera));
        Vector3 forward=Vector3.Normalize(camera.Target-camera.Position);
        Vector3 right=Vector3.Normalize(Vector3.Cross(forward,camera.Up));
        Vector3 up=Vector3.Cross(right,forward);
        return CurrentWeapon switch
        {
            WeaponType.Belt => camera.Position + forward * (0.95f + BeltSwingPhase * 0.9f) + right * 0.22f - up * 0.14f,
            _ => camera.Position + forward * (0.88f + MathF.Sin(SolderThrustPhase * MathF.PI) * 0.55f) + right * 0.24f - up * 0.15f
        };
    }

    /// <summary>
    /// Draws all 3D world-space combat particles (rosin smoke puffs, solder sparks, electric arcs, whip slash ribbons, brass shells, and bullet tracers).
    /// Call inside BeginMode3D(camera) in the main loop.
    /// </summary>
    public void DrawWeaponWorldEffects3D(Camera3D camera)
    {


        // 2. Whip Slash 3D Arc Ribbons (silky unbroken tapered golden-amber motion sweep, zero flat pink or dashed beads!)
        if (WhipSlashes.Count > 0)
        {
            CombatSprites.Begin(BlendMode.Additive);
            for (int i = 0; i < WhipSlashes.Count; i++)
            {
                var w = WhipSlashes[i];
                float ratio = Math.Clamp(w.Life / w.MaxLife, 0f, 1f);
                int rOuter = Math.Clamp((int)(w.PrimaryColor.R * 0.42f + 255 * 0.58f), 0, 255);
                int gOuter = Math.Clamp((int)(w.PrimaryColor.G * 0.32f + 192 * 0.68f), 0, 255);
                int bOuter = Math.Clamp((int)(w.PrimaryColor.B * 0.22f + 98 * 0.78f), 0, 255);

                const int segments = 18;
                Vector3 prev = w.Start;
                float prevEnv = 0f;
                for (int seg = 1; seg <= segments; seg++)
                {
                    float t = seg / (float)segments;
                    Vector3 cur = EvalQuadraticBezier(w.Start, w.Control, w.End, t);
                    float env = MathF.Sin(t * MathF.PI);

                    float prevOuterW = (0.044f * prevEnv + 0.003f) * ratio;
                    float curOuterW = (0.044f * env + 0.003f) * ratio;
                    Color prevOuterCol = new(rOuter, gOuter, bOuter, Math.Clamp((int)(118 * ratio * prevEnv), 0, 255));
                    Color curOuterCol = new(rOuter, gOuter, bOuter, Math.Clamp((int)(118 * ratio * env), 0, 255));
                    CombatSprites.RibbonSegment(prev, cur, camera.Position, prevOuterW, curOuterW, prevOuterCol, curOuterCol);

                    float prevCoreW = (0.015f * prevEnv + 0.001f) * ratio;
                    float curCoreW = (0.015f * env + 0.001f) * ratio;
                    Color prevCoreCol = new(255, 244, 198, Math.Clamp((int)(185 * ratio * prevEnv), 0, 255));
                    Color curCoreCol = new(255, 244, 198, Math.Clamp((int)(185 * ratio * env), 0, 255));
                    CombatSprites.RibbonSegment(prev, cur, camera.Position, prevCoreW, curCoreW, prevCoreCol, curCoreCol);

                    prev = cur;
                    prevEnv = env;
                }
            }
            CombatSprites.End();
        }

        // 3. Hot Solder Sparks & Cyan Electric Arcs
        for (int i = 0; i < SparkParticles.Count; i++)
        {
            var s = SparkParticles[i];
            if (s.BallisticWidth>0) continue;
            float ratio = Math.Clamp(s.Life / s.MaxLife, 0f, 1f);
            Color col = new Color(s.ColorMain.R, s.ColorMain.G, s.ColorMain.B, (byte)(255 * ratio));

            if (s.Velocity.LengthSquared() > 0.000001f)
            {
                Vector3 tail = s.Position - Vector3.Normalize(s.Velocity) * (.045f+.065f*ratio);
                Raylib.DrawCylinderEx(tail,s.Position,.003f,.010f*ratio,4,col);
            }
        }

        foreach (var casing in BrassShells)
        {
            Vector3 angles=casing.Rotation*(MathF.PI/180);
            Vector3 axis=Vector3.Transform(Vector3.UnitY,Quaternion.CreateFromYawPitchRoll(angles.Y,angles.X,angles.Z));
            DrawCasing(casing.Position,axis,casing.IsShotgunShell);
        }

        Vector3 forward=Vector3.Normalize(camera.Target-camera.Position);
        Vector3 right=Vector3.Normalize(Vector3.Cross(forward,camera.Up)), up=Vector3.Cross(right,forward);
        if (SmokeParticles.Count>0)
        {
            CombatSprites.Begin(BlendMode.Alpha);
            foreach (var p in SmokeParticles)
            {
                if (!p.IsPowder) continue;
                float remaining=p.Life/p.MaxLife;
                var tint=p.Tint;
                tint.A=(byte)(tint.A*Ease(remaining));
                CombatSprites.Puff(p.Position,right,up,p.Size,p.Rotation+(1-remaining)*.48f,tint);
            }
            CombatSprites.End();
        }
        if (BulletTracers.Count==0 && SparkParticles.Count==0) return;
        float pixelAngle=2*MathF.Tan(camera.FovY*MathF.PI/360)/Raylib.GetScreenHeight();
        CombatSprites.Begin(BlendMode.Additive);
        foreach (var tr in BulletTracers)
        {
            float age=1-tr.Life/tr.MaxLife;
            Vector3 travel=tr.End-tr.Start;
            float head=float.Lerp(tr.StartFraction,1,age);
            float tail=Math.Max(0,head-(tr.StartFraction>.20f ? .20f : .26f));
            Color tint=tr.TracerColor;
            tint.A=(byte)(tint.A*(1-age*age));
            CombatSprites.Streak(tr.Start+travel*tail,tr.Start+travel*head,camera.Position,tr.Thickness,pixelAngle,tint);
        }
        foreach (var spark in SparkParticles)
        {
            if (spark.BallisticWidth==0 || spark.IsDebris) continue;
            DrawFragment(spark,camera.Position,pixelAngle);
        }
        CombatSprites.End();
        if (SparkParticles.Exists(s=>s.IsDebris))
        {
            CombatSprites.Begin(BlendMode.Alpha);
            foreach (var spark in SparkParticles)
                if (spark.IsDebris) DrawFragment(spark,camera.Position,pixelAngle);
            CombatSprites.End();
        }
    }

    private static void DrawFragment(SparkParticle3D spark,Vector3 eye,float pixelAngle)
    {
        Color color=spark.ColorMain;
        color.A=(byte)(color.A*spark.Life/spark.MaxLife);
        CombatSprites.Streak(spark.Position-spark.Velocity*(spark.IsDebris ? .004f : .011f),spark.Position,
                             eye,spark.BallisticWidth,pixelAngle,color);
    }

    private static void DrawCasing(Vector3 basePos,Vector3 axis,bool hull)
    {
        float r=hull ? .0092f : .0042f;
        Vector3 cap=basePos+axis*(hull ? .010f : .0035f);
        var brass=new Color(172,134,68,255);
        VisualEngine3D.Rod(basePos,cap,r*1.07f,r*1.07f,10,brass,Finish.Bronze);
        if(hull)
        {
            Vector3 primer=basePos-axis*.0008f;
            VisualEngine3D.Rod(primer,basePos,r*.36f,r*.36f,12,new(93,74,48,255),Finish.Bronze);
            VisualEngine3D.Rod(primer-axis*.0003f,primer,r*.24f,r*.24f,12,new(189,163,112,255),Finish.Bronze);
        }
        Vector3 lip=cap+axis*(hull ? .042f : .0115f);
        VisualEngine3D.Rod(cap,lip,r,r,10,hull ? new(132,28,24,255) : brass,hull ? Finish.Paint : Finish.Bronze);
        VisualEngine3D.Rod(lip,lip+axis*.0005f,r*.78f,r*.78f,10,new(49,40,32,255));
    }

    private Matrix4x4 GunToView => Matrix4x4.CreateRotationZ(ViewRoll)*Matrix4x4.CreateScale(-1,1,1);

    private void DrawMuzzleFlash(Camera3D viewCamera)
    {
        var gun=GunAssembly(BaseOffset());
        var tip=Vector3.Transform(gun.Tip,GunToView);
        var axis=Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ,gun.Pose*GunToView));
        var right=Vector3.Normalize(Vector3.Cross(viewCamera.Target-viewCamera.Position,viewCamera.Up));
        var up=Vector3.Normalize(Vector3.Cross(right,viewCamera.Target-viewCamera.Position));
        CombatSprites.Muzzle(tip,axis,right,up,CurrentWeapon==WeaponType.MicroUzi ? .31f : .43f,_flashAngle,FlashStrength);
    }

    /// <summary>
    /// Draws the first-person 3D hands of the 90s businessman (crimson jacket sleeves, white cuffs, gold signet ring)
    /// and the active 3D weapon ViewModel with multi-phase attack animations, plus 2D HUD overlays (hitmarker & floating damage numbers).
    /// Can be called either inside or outside BeginMode3D (manages its own dedicated depth-cleared 3D pass + 2D HUD projection).
    /// </summary>
    public void DrawFirstPersonHandsAndWeapon(Camera3D worldCamera, float walkBobPhase = 0f, bool isCrouching = false, Font uiFont = default, Camera3D? inspectionCamera = null, bool showCombatText = true)
    {
        // Local ViewModel camera at origin looking along +Z so hands never clip into walls
        Camera3D vmCam = inspectionCamera ?? new Camera3D
        {
            Position = Vector3.Zero,
            Target = new Vector3(0f, 0f, 1f),
            Up = Vector3.UnitY,
            FovY = ViewModelFov,
            Projection = CameraProjection.Perspective
        };

        SetStance(walkBobPhase,isCrouching);
        Vector3 baseOffset=BaseOffset();

        // Render 3D First-Person ViewModel
        Raylib.BeginMode3D(vmCam);
        VisualEngine3D.Hands();
        if (_flash>0)
        {
            VisualEngine3D.FlashLight(Vector3.Transform(GunAssembly(baseOffset).Tip,GunToView),FlashStrength);
        }
        Rlgl.PushMatrix();
        Rlgl.Scalef(-1,1,1);
        Rlgl.Rotatef(ViewRoll*180/MathF.PI,0,0,1);
        Rlgl.DisableBackfaceCulling();

        if (FlippingOff && VisualEngine3D.HasAsset("fps_hands_flipoff"))
        {
            // Punchy snap-up, bold double-thrust jab toward target, smooth drop-out!
            float p = 1f - FlipOffTimer / _flipOffDuration;
            float lift = p < 0.12f ? Ease(p / 0.12f) : p > 0.82f ? Ease((1f - p) / 0.18f) : 1f;
            float thrust = p is >= 0.12f and <= 0.64f ? MathF.Pow(MathF.Max(0f, MathF.Sin((p - 0.12f) / 0.52f * MathF.PI * 2f)), 1.5f) : 0f;
            float wiggle = p is >= 0.12f and <= 0.82f ? MathF.Sin((p - 0.12f) * MathF.PI * 4.5f) * 0.048f : 0f;
            Vector3 pivot = baseOffset + new Vector3(0.09f, -0.15f, 0.54f);
            Vector3 slide = baseOffset + new Vector3(0.02f * (1f - lift), -0.34f * (1f - lift) + thrust * 0.028f, thrust * 0.055f);
            var pose = Matrix4x4.CreateTranslation(-pivot)
                     * Matrix4x4.CreateRotationZ(wiggle)
                     * Matrix4x4.CreateRotationX(-0.24f * (1f - lift) + thrust * 0.08f)
                     * Matrix4x4.CreateTranslation(pivot);
            VisualEngine3D.ArmAssembly(true, slide, "fps_hands_flipoff", pose);
        }
        else switch (CurrentWeapon)
        {
            case WeaponType.BareHands:
            case WeaponType.CaptureNet:
                DrawCaptureTool(baseOffset,CurrentWeapon==WeaponType.CaptureNet);
                break;
            case WeaponType.Belt:
                DrawBeltViewModel3D(baseOffset);
                break;
            case WeaponType.SolderingIron:
                DrawSolderingIronViewModel3D(baseOffset);
                break;
            case WeaponType.MicroUzi:
                DrawMicroUziViewModel3D(baseOffset);
                break;
            case WeaponType.SawedOff:
                DrawSawedOffViewModel3D(baseOffset, isChatGun: false);
                break;
            case WeaponType.ChatGun:
                DrawSawedOffViewModel3D(baseOffset, isChatGun: true);
                break;
        }

        Rlgl.PopMatrix();
        Rlgl.EnableBackfaceCulling();
        if (_flash>0) DrawMuzzleFlash(vmCam);
        Raylib.EndMode3D();

        // Render 2D projected floating combat numbers (`ШЛЕПОК!`, `ПРИПАЯНО!`, `-35`, `КРИТ!`) & Hitmarker X
        if (showCombatText)
            DrawCombatFloatingTextsAndHitmarker2D(worldCamera, uiFont);
    }

    // =========================================================================
    // SLOT 1: ОТЦОВСКИЙ КОЖАНЫЙ РЕМЕНЬ (`WeaponType.Belt`)
    // =========================================================================
    private const float NetSwingDuration=.42f;
    private float _netSwingTime;
    private SpringFloat _catchSpring;

    private Matrix4x4 NetPose(Vector3 grip)
    {
        float t=1-_netSwingTime/NetSwingDuration;
        float wind=0,sweep=0;
        static float Ease(float x)=>x*x*(3-2*x);
        if(t<.22f) wind=Ease(t/.22f);
        else if(t<.56f) { sweep=Ease((t-.22f)/.34f); wind=1-sweep; }
        else
        {
            float recovery=(t-.56f)/.44f;
            sweep=(1-Ease(recovery))*MathF.Cos(recovery*2.45f);
        }
        Vector3 travel=wind*new Vector3(.08f,.06f,-.05f)+sweep*new Vector3(-.24f,-.10f,.28f);
        const float rad=MathF.PI/180;
        return Matrix4x4.CreateTranslation(-grip)*Matrix4x4.CreateRotationY(-.52f+rad*(24*wind-58*sweep))
            *Matrix4x4.CreateRotationX(-.25f-rad*(18*wind-32*sweep))*Matrix4x4.CreateRotationZ(rad*(-14*wind+26*sweep))
            *Matrix4x4.CreateTranslation(grip+travel)*ToolStance(grip);
    }

    private void DrawCaptureTool(Vector3 offset,bool net)
    {
        float reach=Math.Clamp(_catchSpring.Value,-.2f,1.2f);
        string hands=net ? "fps_hands_net" : "fps_hands_bare";
        Vector3 grip=offset+new Vector3(.18f,-.15f,.62f);
        var pose=net ? NetPose(grip) : Matrix4x4.CreateTranslation(new Vector3(0,.04f*reach,.13f*reach))*ToolStance(grip);
        foreach(bool right in new[]{false,true})
        {
            Vector3 slide=offset+(net ? Vector3.Zero : new Vector3(right ? -.09f*reach : .02f*reach,0,(right ? .34f : -.09f)*reach));
            if(VisualEngine3D.HasAsset(hands)) VisualEngine3D.ArmAssembly(right,slide,hands,pose);
            else VisualEngine3D.Arm(right,new Vector3(right ? .20f : -.20f,-.22f,right ? .56f : .65f)+slide,"fps_hands_cs2",pose);
        }
        if(!net) return;
        if(VisualEngine3D.HasAsset("weapon_net_90s"))
            VisualEngine3D.Asset("weapon_net_90s",grip,assemblyTransform:pose,skin:Inventory.EquippedSkins[(int)CurrentWeapon]);
        else
        {
            Vector3 P(float x,float y,float z)=>Vector3.Transform(grip+new Vector3(x,y,z),pose);
            VisualEngine3D.Rod(P(0,0,-.09f),P(0,.14f,.52f),.015f,.012f,10,new(151,104,62,255));
            var mesh=new Color(143,199,187,255);
            Vector3 Rim(float a)=>P(.17f*MathF.Sin(a),.14f+.17f*(1-MathF.Cos(a)),.52f);
            for(int i=0;i<24;i++)
            {
                float a=MathF.Tau*i/24;
                VisualEngine3D.Rod(Rim(a),Rim(a+MathF.Tau/24),.008f,.008f,6,mesh);
                if(i%3==0) VisualEngine3D.Rod(Rim(a),P(0,.23f,.70f),.003f,.003f,5,mesh);
            }
        }
    }

    private void DrawBeltViewModel3D(Vector3 baseOffset)
    {
        float p = BeltSwingPhase; // 1.0 -> 0.0 during swing
        float t = 1f - p;         // 0.0 -> 1.0 progress
        float lashSide = (BeltSwingCombo == 0) ? 1f : -1f;

        // One-handed leather whip-belt (плётка) 3-phase animation:
        // Phase A (0.00..0.24): Quick right-arm cock/raise (alternating forehand / backhand)
        // Phase B (0.24..0.62): Explosive diagonal one-handed whip lash across crosshair with wrist snap
        // Phase C (0.62..1.00): Supple damped leather follow-through back to lower-right ready stance
        Vector3 swingHandOffset = Vector3.Zero;
        float swingPitch = 0f, swingYaw = 0f, swingRoll = 0f;

        if (p > 0.001f)
        {
            if (t < 0.24f)
            {
                float k = Ease(t / 0.24f);
                if (lashSide > 0f)
                {
                    swingHandOffset = new Vector3(0.11f * k, 0.16f * k, -0.085f * k);
                    swingPitch = -0.44f * k;
                    swingYaw   =  0.34f * k;
                    swingRoll  = -0.34f * k;
                }
                else
                {
                    swingHandOffset = new Vector3(-0.15f * k, 0.07f * k, -0.06f * k);
                    swingPitch = -0.30f * k;
                    swingYaw   = -0.46f * k;
                    swingRoll  =  0.48f * k;
                }
            }
            else if (t < 0.62f)
            {
                float raw = (t - 0.24f) / 0.38f;
                float k = 1f - MathF.Pow(1f - raw, 2.5f); // front-loaded whip snap acceleration
                float lunge = MathF.Sin(raw * MathF.PI);
                if (lashSide > 0f)
                {
                    swingHandOffset = new Vector3(
                        Lerp(0.11f, -0.25f, k),
                        Lerp(0.16f, -0.10f, k),
                        Lerp(-0.085f, 0.03f, k) + 0.25f * lunge);
                    swingPitch = Lerp(-0.44f, 0.56f, k) + 0.18f * lunge;
                    swingYaw   = Lerp(0.34f, -0.78f, k);
                    swingRoll  = Lerp(-0.34f, 0.46f, k);
                }
                else
                {
                    swingHandOffset = new Vector3(
                        Lerp(-0.15f, 0.17f, k),
                        Lerp(0.07f, -0.08f, k),
                        Lerp(-0.06f, 0.02f, k) + 0.24f * lunge);
                    swingPitch = Lerp(-0.30f, 0.48f, k) + 0.16f * lunge;
                    swingYaw   = Lerp(-0.46f, 0.42f, k);
                    swingRoll  = Lerp(0.48f, -0.36f, k);
                }
            }
            else
            {
                float raw = (t - 0.62f) / 0.38f;
                float env = 1f - Ease(raw);
                float settle = MathF.Sin(raw * MathF.PI * 1.5f) * (1f - raw);
                if (lashSide > 0f)
                {
                    swingHandOffset = new Vector3(-0.25f * env, -0.10f * env + 0.018f * settle, 0.03f * env);
                    swingPitch = 0.56f * env - 0.12f * settle;
                    swingYaw   = -0.78f * env + 0.09f * settle;
                    swingRoll  = 0.46f * env - 0.10f * settle;
                }
                else
                {
                    swingHandOffset = new Vector3(0.17f * env, -0.08f * env + 0.018f * settle, 0.02f * env);
                    swingPitch = 0.48f * env - 0.12f * settle;
                    swingYaw   = 0.42f * env - 0.09f * settle;
                    swingRoll  = -0.36f * env + 0.10f * settle;
                }
            }
        }

        // Subtle alive gravity pendulum bob & look-lag sway so the hanging leather whip feels supple
        float idleWeight = 1f - Math.Clamp(p * 3f, 0f, 1f);
        float now = (float)Raylib.GetTime();
        Vector3 lookLag = _lookLagSpring.Value;
        float pendRoll  = (MathF.Sin(now * 2.15f) * 0.030f + MathF.Sin(now * 3.65f) * 0.010f - Math.Clamp(lookLag.X * 1.35f, -0.16f, 0.16f)) * idleWeight;
        float pendPitch = (MathF.Sin(now * 1.65f + 0.7f) * 0.020f + Math.Clamp(lookLag.Y * 0.85f, -0.11f, 0.11f)) * idleWeight;
        float pendYaw   = (-Math.Clamp(lookLag.X * 0.55f, -0.09f, 0.09f)) * idleWeight;
        Vector3 idleBob = new Vector3(MathF.Sin(now * 2.15f) * 0.0045f, MathF.Cos(now * 1.65f) * 0.0040f, 0f) * idleWeight;

        // Natural lower-right one-handed FPS melee stance (right hand ONLY; heavy gold buckle cleanly visible above [E] prompts & hotbar)
        Vector3 rightAssembly = baseOffset + swingHandOffset + idleBob + new Vector3(0.068f, 0.060f, -0.025f);
        Vector3 handleBase = new Vector3(.18f, -.15f, .62f) + rightAssembly;
        Matrix4x4 rightPose = Matrix4x4.CreateTranslation(-handleBase)
            * Matrix4x4.CreateRotationX(-0.24f + swingPitch + pendPitch)
            * Matrix4x4.CreateRotationY(-0.30f + swingYaw + pendYaw)
            * Matrix4x4.CreateRotationZ(-0.05f + swingRoll + pendRoll)
            * Matrix4x4.CreateTranslation(handleBase)
            * ToolStance(handleBase)
            * GetInspectTransform(1, InspectPhase);

        VisualEngine3D.ArmAssembly(true, rightAssembly, "fps_hands_solder", rightPose);
        Color tint = SkinTintPrimary.A > 0 ? Raylib.ColorLerp(Color.White, SkinTintPrimary, 0.45f) : Color.White;
        VisualEngine3D.SetBeltTension(0f);
        VisualEngine3D.Asset("weapon_belt_90s", handleBase, 0, 1, tint, assemblyTransform: rightPose, skin: Inventory.EquippedSkins[(int)CurrentWeapon]);

        // During explosive whip snap (Phase B into early C), draw golden-crimson arc ribbon anchored cleanly to the real gold buckle tip!
        if (p > 0.001f && t >= 0.24f && t <= 0.74f)
        {
            float arcT = (t - 0.24f) / 0.50f;
            float alphaEnv = MathF.Sin(Math.Clamp(arcT, 0f, 1f) * MathF.PI);
            byte slashAlpha = (byte)Math.Clamp((int)(235f * alphaEnv), 0, 255);
            // Exact local-space tip of the heavy 90s gold brass buckle roller in weapon_belt_90s.glb
            Vector3 buckleTip = Vector3.Transform(handleBase + new Vector3(-0.0313f, -0.2298f, 0.4902f), rightPose);
            Vector3 midTrail = buckleTip + new Vector3(0.18f * lashSide, 0.11f, -0.09f);
            Vector3 tailTrail = buckleTip + new Vector3(0.36f * lashSide, 0.20f, -0.18f);
            VisualEngine3D.Rod(buckleTip, midTrail, 0.019f * alphaEnv, 0.013f * alphaEnv, 6, new Color(255, 212, 68, (int)slashAlpha));
            VisualEngine3D.Rod(midTrail, tailTrail, 0.013f * alphaEnv, 0.004f * alphaEnv, 6, new Color(245, 62, 42, (int)(slashAlpha * 0.78f)));
        }
    }

    // =========================================================================
    // SLOT 2: ПАЯЛЬНИК ИЗ 90-Х С СИНЕЙ ИЗОЛЕНТОЙ (`WeaponType.SolderingIron`)
    // =========================================================================
    private void DrawSolderingIronViewModel3D(Vector3 baseOffset)
    {
        float p = SolderThrustPhase; // 1.0 -> 0.0
        float lunge = MathF.Sin(p * MathF.PI); // 0 -> 1 -> 0 fierce forward lunge
        float twist = MathF.Sin(p * MathF.PI * 2f) * 0.045f;

        Vector3 thrustOffset = new Vector3(
            -0.07f * lunge + twist,
            0.04f * lunge,
            0.28f * lunge);

        Vector3 rightAssembly = baseOffset + thrustOffset + new Vector3(.045f,-.012f,0);
        Vector3 handleBase = new Vector3(.18f,-.15f,.62f) + rightAssembly;
        Matrix4x4 rightPose = Matrix4x4.CreateTranslation(-handleBase)
            * Matrix4x4.CreateRotationX(-0.66f)
            * Matrix4x4.CreateRotationY(-0.68f)
            * Matrix4x4.CreateTranslation(handleBase) * ToolStance(handleBase) * GetInspectTransform(2,InspectPhase);

        VisualEngine3D.ArmAssembly(true, rightAssembly, "fps_hands_solder", rightPose);
        Color tint = SkinTintPrimary.A > 0 ? Raylib.ColorLerp(Color.White, SkinTintPrimary, 0.45f) : Color.White;
        VisualEngine3D.Asset("weapon_solder_90s", handleBase, 0, 1, tint, assemblyTransform: rightPose,skin:Inventory.EquippedSkins[(int)CurrentWeapon]);
        // Power cord leaves the pommel strain-relief boot
        Vector3 cordStart = Vector3.Transform(handleBase - Vector3.UnitZ * .099f, rightPose);
        Vector3 cordMid = cordStart + new Vector3(.04f,-.16f,-.18f);
        VisualEngine3D.Rod(cordStart, cordMid, .0032f, .0032f, 8, new Color(28,28,32,255));
        if (Inspecting)
        {
            Vector3 tip=Vector3.Transform(handleBase+new Vector3(0,.0016f,.238f),rightPose);
            VisualEngine3D.DrawRosinSmoke(tip);
        }
    }

    // =========================================================================
    // SLOT 3: БАНДИТСКИЙ MICRO-UZI (`WeaponType.MicroUzi`)
    // =========================================================================
    private float StanceYaw => MathF.PI/180*(-24*sprintWeight-13*EquipTransition);
    private float StancePitch => MathF.PI/180*(11*sprintWeight+5*airLift+16*EquipTransition);

    private Matrix4x4 ToolStance(Vector3 pivot)
    {
        return Matrix4x4.CreateTranslation(-pivot)*Matrix4x4.CreateFromYawPitchRoll(StanceYaw,StancePitch,0)
            *Matrix4x4.CreateTranslation(pivot);
    }

    internal float HitJolt;
    private Vector3 BaseOffset()
    {
        float draw=EquipTransition;
        Vector3 look=_lookLagSpring.Value;
        return new Vector3(0,-.115f,-.105f)*HitJolt+_sway.Value+new Vector3(.035f-.32f*look.X-.065f*sprintWeight+.055f*draw,
            -.012f+.23f*look.Y-.16f*draw+.045f*airLift-_landing.Value-.060f*sprintWeight-.04f*dashWeight+.030f*crouchWeight,
            -.105f-.09f*draw-.024f*sprintWeight-.038f*crouchWeight);
    }

    private Vector3 MuzzleOffset => CurrentWeapon==WeaponType.MicroUzi
        ? new Vector3(0,.018f,.322f)*.75f : new Vector3(_shells==1 ? -.0175f : .0175f,.024f,.467f);

    private (Vector3 Offset,Matrix4x4 Pose,Vector3 Receiver,Vector3 Tip) GunAssembly(Vector3 baseOffset)
    {
        bool uzi=CurrentWeapon==WeaponType.MicroUzi;
        float t=ReloadProgress;
        float tilt=Ramp(t,0,.24f)*(1-Ramp(t,.74f,1));
        Vector3 reloadTravel=uzi ? new(-.085f,.125f,-.045f) : new(.020f,-.006f,-.055f);
        Vector3 offset=baseOffset+_kick.Value+reloadTravel*tilt+new Vector3(uzi ? .035f : 0,
            .012f*_receiverBump.Value,-.009f*_receiverBump.Value);
        Vector3 pivot=offset+(uzi ? new Vector3(.18f,-.193f,.6665f) : new Vector3(.18f,-.17f,.60f));
        Vector3 receiver=offset+(uzi ? new Vector3(.18f,-.10f,.68f) : new Vector3(.18f,-.12f,.66f));
        Vector3 muzzle=MuzzleOffset;
        const float rad=MathF.PI/180;
        float pitch=_bore.Value.X+_muzzleRotation.Value.X-.075f*_receiverBump.Value
            +StancePitch-(uzi ? .16f : .09f)*tilt;
        float yaw=_bore.Value.Y+StanceYaw+rad*(uzi ? -4 : -18)*tilt;
        float roll=_muzzleRotation.Value.Z+rad*(uzi ? -14 : 20)*tilt+.037f*_receiverBump.Value;
        var inspect=Reloading
            ? GetInspectTransform(uzi ? 3 : 4,_inspectPhaseAtReload,1-Ramp(t,0,.17f))
            : GetInspectTransform(uzi ? 3 : 4,InspectPhase);
        var pose=Matrix4x4.CreateTranslation(-pivot)*Matrix4x4.CreateFromYawPitchRoll(yaw,pitch,roll)
            *Matrix4x4.CreateTranslation(pivot)*inspect;
        return (offset,pose,receiver,Vector3.Transform(receiver+muzzle,pose));
    }

    private static float Ramp(float t,float start,float end)
    {
        float x=Math.Clamp((t-start)/(end-start),0,1);
        return x*x*x*(10+x*(-15+6*x));
    }

    // Carry a point inside the authored finger curl to its animated socket.
    private static Matrix4x4 HandAt(Vector3 socket,Vector3 target,Quaternion rotation,Matrix4x4 pose) =>
        Matrix4x4.CreateTranslation(-socket)*Matrix4x4.CreateFromQuaternion(rotation)
        *Matrix4x4.CreateTranslation(target)*pose;

    private void DrawMicroUziViewModel3D(Vector3 baseOffset)
    {
        var (assembly,pose,receiverCenter,_)=GunAssembly(baseOffset);
        float t=ReloadProgress;
        VisualEngine3D.ArmAssembly(true,assembly,"fps_hands_uzi",pose,UziRecoilPhase*.075f);
        if (!Reloading)
        {
            VisualEngine3D.ArmAssembly(false,assembly,"fps_hands_uzi",pose,.035f*UziRecoilPhase);
            VisualEngine3D.PoseReloadPart(true,Vector3.Zero,.031f*UziBoltOffset);
            VisualEngine3D.Asset("weapon_uzi_90s",receiverCenter,180,.75f,
                SkinTintPrimary.A>0 ? Raylib.ColorLerp(Color.White,SkinTintPrimary,.45f) : Color.White, assemblyTransform: pose,skin:Inventory.EquippedSkins[(int)CurrentWeapon]);
            VisualEngine3D.PoseReloadPart(true,Vector3.Zero,0);
            return;
        }
        float take=Ramp(t,.025f,.15f), pull=Ramp(t,.155f,.32f), seat=Ramp(t,.34f,.56f);
        float reach=Ramp(t,.575f,.70f), recover=Ramp(t,.855f,.985f);
        float rack=Ramp(t,.71f,.80f)*SpringReturn((t-.80f)*ReloadDuration,62,.73f);
        // Slide the magazine coaxially along the Uzi's magazine well shaft (-Y in assembly space).
        Vector3 travel=new Vector3(0f,-.148f,-.006f)*(pull-seat);
        Vector3 well=receiverCenter+new Vector3(0,-.187f,-.01425f);
        Vector3 magSocket=receiverCenter+new Vector3(0f,-.158f,-.01425f);
        Vector3 boltSocket=receiverCenter+new Vector3(0f,.0424f,.02625f);
        Vector3 forendSocket=assembly+new Vector3(.183f,-.127f,.84f);
        if (t<.575f)
        {
            Vector3 approach=new Vector3(-.055f,.045f,.075f)*(1-take);
            VisualEngine3D.ArmAssembly(false,assembly,"fps_left_mag",
                HandAt(magSocket,magSocket+approach+travel,Quaternion.Identity,pose));
        }
        else if (t<.855f)
        {
            float release=Ramp(t,.805f,.855f);
            Vector3 boltTarget=boltSocket+new Vector3(0f,0f,-.059f*rack)
                +new Vector3(-.070f,-.110f,-.035f)*(1-reach)
                +new Vector3(-.035f,.022f,0f)*MathF.Sin(reach*MathF.PI)
                +new Vector3(-.045f,-.035f,.035f)*release;
            Quaternion boltRot=Quaternion.CreateFromYawPitchRoll(.28f,.07f,.72f);
            VisualEngine3D.ArmAssembly(false,assembly,"fps_left_bolt",
                HandAt(boltSocket,boltTarget,boltRot,pose));
        }
        else
        {
            Vector3 forendTarget=forendSocket+new Vector3(-.050f,-.025f,-.060f)*(1-recover);
            VisualEngine3D.ArmAssembly(false,assembly,"fps_hands_uzi",
                HandAt(forendSocket,forendTarget,Quaternion.Identity,pose));
        }
        if(t>.155f && t<.56f)
        {
            var magazinePose=Matrix4x4.CreateTranslation(well+travel)*pose;
            VisualEngine3D.MagazineInsert(magazinePose,t>=.33f);
        }
        VisualEngine3D.PoseReloadPart(true,travel, rack*.059f+.031f*UziBoltOffset);
        VisualEngine3D.Asset("weapon_uzi_90s",receiverCenter,180,.75f,
            SkinTintPrimary.A>0 ? Raylib.ColorLerp(Color.White,SkinTintPrimary,.45f) : Color.White, assemblyTransform: pose,skin:Inventory.EquippedSkins[(int)CurrentWeapon]);
        VisualEngine3D.PoseReloadPart(true,Vector3.Zero,0);
    }

    // Homogeneous spring response from unit displacement and zero velocity.
    private static float SpringReturn(float seconds,float frequency,float damping)
    {
        if(seconds<=0) return 1;
        float decay=frequency*damping, wave=frequency*MathF.Sqrt(1-damping*damping);
        return MathF.Exp(-decay*seconds)*(MathF.Cos(wave*seconds)+decay/wave*MathF.Sin(wave*seconds));
    }

    private static float BarrelAngle(float t)
    {
        float opened=.80f*(1-SpringReturn((t-.035f)*ShotgunReloadDuration,20,.72f));
        float dip=.075f*Ramp(t,.755f,.815f);
        return (opened+dip)*(1-Ramp(t,.815f,.89f));
    }

    private static (Vector3 Entry,Vector3 Axis) Chamber(Vector3 receiver,float angle,int index)
    {
        var turn=Matrix4x4.CreateRotationX(angle);
        Vector3 hinge=receiver+new Vector3(0,-.013f,.127f);
        Vector3 mouth=receiver+new Vector3(index==0 ? -.0175f : .0175f,.024f,.092f);
        return (hinge+Vector3.TransformNormal(mouth-hinge,turn),Vector3.TransformNormal(Vector3.UnitZ,turn));
    }

    private void DrawSawedOffViewModel3D(Vector3 baseOffset, bool isChatGun)
    {
        var (assembly,pose,gripTop,_)=GunAssembly(baseOffset);
        float t=ReloadProgress, angle=BarrelAngle(t);
        var (entry,axis)=Chamber(gripTop,angle,0);
        int count=2-_shells;
        Vector3 socket=assembly+new Vector3(.184f,-.140f,.852f);
        Vector3 hinge=gripTop+new Vector3(0,-.013f,.127f);
        Vector3 support=hinge+Vector3.TransformNormal(socket-hinge,Matrix4x4.CreateRotationX(angle));
        Quaternion onForend=Quaternion.CreateFromAxisAngle(Vector3.UnitX,angle);
        // Authored shell-head midpoint and shell axis in fps_left_shells.glb (BVH overlap == 0).
        Vector3 shellHeadSocket=assembly+new Vector3(.180f,-.092f,.765f);
        Quaternion shellAlign=Quaternion.CreateFromAxisAngle(Vector3.UnitX,angle-.6191f);
        Vector3 breechMid=entry+Vector3.UnitX*(count==1 ? .035f : .0175f);
        float carry=Ramp(t,.22f,.42f);
        float push1=Ramp(t,.42f,.55f), push2=Ramp(t,.55f,.66f);
        float push=count==1 ? .0536f*Ramp(t,.42f,.62f) : .028f*push1+.0256f*push2;
        float handPush=Math.Min(push,.008f);
        float retract=Ramp(t,.66f,.73f);
        Vector3 pocketMid=breechMid-axis*.115f+new Vector3(-.055f,.012f,-.018f);
        Vector3 stageMid=breechMid-axis*(.056f-handPush);
        Vector3 arc=new Vector3(-.012f,.022f,-.008f)*MathF.Sin(carry*MathF.PI);
        Vector3 targetHeadMid=Vector3.Lerp(pocketMid,stageMid,carry)+arc
            +new Vector3(-.048f,-.045f,-.025f)*retract;
        Matrix4x4 shellsHandFrame=HandAt(shellHeadSocket,targetHeadMid,shellAlign,pose);

        VisualEngine3D.ArmAssembly(true,assembly,"fps_hands_shotgun",pose,ShotgunKickPhase*.08f);
        if (!Reloading)
        {
            VisualEngine3D.ArmAssembly(false,assembly,"fps_hands_shotgun",pose,.045f*ShotgunKickPhase);
        }
        else if (t<.22f)
        {
            float drop=Ramp(t,.15f,.22f);
            Vector3 forendPos=support+new Vector3(-.075f,-.135f,-.095f)*drop;
            VisualEngine3D.ArmAssembly(false,assembly,"fps_hands_shotgun",
                HandAt(socket,forendPos,onForend,pose));
        }
        else if (t<.73f)
        {
            VisualEngine3D.ArmAssembly(false,assembly,"fps_left_shells",shellsHandFrame);
        }
        else
        {
            float grabForend=Ramp(t,.73f,.79f);
            Vector3 forendPos=support+new Vector3(-.055f,-.045f,-.025f)*(1-grabForend);
            VisualEngine3D.ArmAssembly(false,assembly,"fps_hands_shotgun",
                HandAt(socket,forendPos,onForend,pose));
        }

        Color shotgunTint=isChatGun ? new Color(150,255,215,255)
            : (SkinTintPrimary.A>0 ? Raylib.ColorLerp(Color.White,SkinTintPrimary,.45f) : Color.White);
        VisualEngine3D.PoseReloadPart(false,Vector3.Zero,angle);
        VisualEngine3D.Asset("weapon_shotgun_90s", gripTop, 180, 1, shotgunTint, assemblyTransform: pose,skin:Inventory.EquippedSkins[(int)CurrentWeapon]);
        VisualEngine3D.PoseReloadPart(false,Vector3.Zero,0);
        if(angle>.05f)
        {
            for(int i=0;i<2;i++)
            {
                var (mouth,dir)=Chamber(gripTop,angle,i);
                var at=Vector3.Transform(mouth-dir*.001f,pose);
                var forward=Vector3.TransformNormal(dir,pose);
                if(i<_shells) DrawCasing(at-forward*.0012f,forward,true);
            }
        }
        if(t>=.22f && t<.90f)
        {
            for(int i=0;i<count;i++)
            {
                int chamber=2-count+i;
                var (mouth,_)=Chamber(gripTop,angle,chamber);
                float seatTime=count==1 ? .62f : i==0 ? .55f : .66f;
                bool seated=t>=seatTime;
                float shellPush=count==1 ? .0536f*Ramp(t,.42f,.62f)
                    : i==0 ? .0536f*push1 : .024f*push1+.0296f*push2;
                Vector3 chamberStage=mouth-axis*(.056f-shellPush);
                Vector3 pocketShell=pocketMid+Vector3.UnitX*(chamber==0 ? -.0175f : .0175f);
                Vector3 unseatedPos=Vector3.Lerp(pocketShell,chamberStage,carry)+arc;
                Vector3 shellBase=Vector3.Transform(seated ? mouth-axis*.0024f : unseatedPos,pose);
                Vector3 shellAxis=Vector3.TransformNormal(axis,pose);
                DrawCasing(shellBase,shellAxis,true);
            }
        }
    }

    // =========================================================================
    // 2D PROJECTED FLOATING DAMAGE NUMBERS & HITMARKER
    // =========================================================================
    private void DrawCombatFloatingTextsAndHitmarker2D(Camera3D worldCamera, Font uiFont = default)
    {
        int sw=UiViewport.Width;
        int sh=UiViewport.Height;
        int cx = sw / 2;
        int cy = sh / 2;

        if (HitmarkerTimer > 0f)
        {
            float ratio = Math.Clamp(HitmarkerTimer / 0.22f, 0f, 1f);
            Color hmCol = HitmarkerIsCrit
                ? new Color(255, 224, 176, (int)(245 * ratio))
                : new Color(251, 249, 241, (int)(245 * ratio));
            Vector2 center = new(cx,cy);
            for (int x=-1; x<=1; x+=2)
            for (int y=-1; y<=1; y+=2)
            {
                Vector2 axis = Vector2.Normalize(new Vector2(x,y));
                Raylib.DrawLineEx(center+axis*7,center+axis*11,1.5f,hmCol);
            }
        }

        Vector3 camForward = Vector3.Normalize(worldCamera.Target - worldCamera.Position);
        for (int i = 0; i < FloatingTexts.Count; i++)
        {
            var ft = FloatingTexts[i];
            Vector3 toPoint = ft.WorldPos - worldCamera.Position;
            if (Vector3.Dot(camForward, toPoint) <= 0.1f)
                continue;

            Vector2 screenPos = UiViewport.Project(ft.WorldPos,worldCamera);
            if (screenPos.X < -100 || screenPos.X > sw + 100 || screenPos.Y < -100 || screenPos.Y > sh + 100)
                continue;

            float ratio = Math.Clamp(ft.Life / ft.MaxLife, 0f, 1f);
            int fontSize = ft.IsCritical ? 20 : 18;
            byte alpha = (byte)(255 * MathF.Min(1f, ratio * 1.4f));

            Color fg = new Color(ft.TextColor.R, ft.TextColor.G, ft.TextColor.B, alpha);

            int textW = (int)LoreAndMenusUI.MeasureTextWidth(uiFont, ft.Text, fontSize);
            if (WorldLabelLayout.Place(screenPos+Vector2.UnitY*fontSize,textW+4,fontSize+3,out var bounds))
                GameFonts.Draw(uiFont,ft.Text,new(bounds.X+2,bounds.Y),fontSize,fg);
        }

        // 3. Action Impact Banner (`ШЛЕПОК!`, `ПРИПАЯНО! ШШШ-ЦЦЦ!`)
        if (LastCombatBannerTimer > 0f && !string.IsNullOrEmpty(LastCombatBannerText))
        {
            float ratio = Math.Clamp(LastCombatBannerTimer / 1.6f, 0f, 1f);
            byte a = (byte)(255 * MathF.Min(1f, ratio * 2f));
            int fontSize = 20;
            int tw = (int)LoreAndMenusUI.MeasureTextWidth(uiFont, LastCombatBannerText, fontSize);
            int bx = cx - tw / 2;
            int by = cy + 54;

            LoreAndMenusUI.DrawTextCrisp(uiFont, LastCombatBannerText, bx, by, fontSize, new Color(LastCombatBannerColor.R, LastCombatBannerColor.G, LastCombatBannerColor.B, a));
        }
    }

    public string GetCurrentWeaponDisplayName() => CurrentWeapon switch
    {
        WeaponType.BareHands => "Голые руки",
        WeaponType.CaptureNet => "Сачок-ловушка",
        WeaponType.Belt => "ОТЦОВСКИЙ КОЖАНЫЙ РЕМЕНЬ (ЗОЛОТАЯ ПРЯЖКА)",
        WeaponType.SolderingIron => "ПАЯЛЬНИК ИЗ 90-Х (СИНЯЯ ИЗОЛЕНТА + КАНИФОЛЬ)",
        WeaponType.MicroUzi => "БАНДИТСКИЙ MICRO-UZI (9mm АВТО)",
        WeaponType.SawedOff => "ОБРЕЗ ДВУСТВОЛКИ (12 КАЛИБР ДУПЛЕТ)",
        WeaponType.ChatGun => "СЕКРЕТНЫЙ ЧАТ-ГАН (ЗОЛОТЫЕ ПРОМПТЫ)",
        _ => "ОРУЖИЕ 90-Х"
    };

    public string GetWeaponAttackSoundFileName() => CurrentWeapon switch
    {
        WeaponType.BareHands => "hand_grab",
        WeaponType.CaptureNet => "net_swipe",
        WeaponType.Belt => "belt_whip.wav",
        WeaponType.SolderingIron => "solder_sizzle.wav",
        WeaponType.MicroUzi => "uzi_shot.wav",
        WeaponType.SawedOff => "shotgun_blast.wav",
        WeaponType.ChatGun => "shotgun_blast.wav",
        _ => "belt_whip.wav"
    };

    private float NextFloat() => (float)_rng.NextDouble();

    private Vector3 RandomVec3(float radius) => new Vector3(
        (NextFloat() * 2f - 1f) * radius,
        (NextFloat() * 2f - 1f) * radius,
        (NextFloat() * 2f - 1f) * radius);

    private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);

    private static Vector3 EvalQuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return (u * u) * a + (2f * u * t) * b + (t * t) * c;
    }
}

// Both GLBs merge their mechanical parts by material. Animate the authored vertices
// in the viewmodel pass and restore the shared buffers before drawing the next actor.
internal static unsafe partial class VisualEngine3D
{
    private sealed class ReloadMesh
    {
        public Mesh Mesh;
        public readonly List<int> Moving=new(), Slider=new();
        public Vector3[] Positions, Normals, Animated, Rotated;
    }

    // Imported GLBs use material primitives. Classify entire welded triangle
    // islands once so a receiver face cannot stretch across the opening breech.
    private static ReloadMesh MovingIslands(Mesh mesh,bool magazine)
    {
        var pos=new ReadOnlySpan<Vector3>((Vector3*)mesh.Vertices,mesh.VertexCount).ToArray();
        var normals=new ReadOnlySpan<Vector3>((Vector3*)mesh.Normals,mesh.VertexCount).ToArray();
        var part=new ReloadMesh {Mesh=mesh,Positions=pos,Normals=normals,Animated=(Vector3[])pos.Clone(),Rotated=(Vector3[])normals.Clone()};
        int[] parent=Enumerable.Range(0,pos.Length).ToArray();
        int Root(int i)
        {
            while(i!=parent[i]) { parent[i]=parent[parent[i]]; i=parent[i]; }
            return i;
        }
        Dictionary<Vector3,int> weld=new();
        for(int i=0;i<pos.Length;i++)
            if(weld.TryGetValue(pos[i],out int other)) parent[i]=other; else weld.Add(pos[i],i);
        for(int t=0;t<mesh.TriangleCount*3;t+=3)
        {
            int Vertex(int j) => mesh.Indices==null ? j : mesh.Indices[j];
            int a=Root(Vertex(t)), b=Root(Vertex(t+1)), c=Root(Vertex(t+2));
            parent[b]=parent[c]=a;
        }
        foreach(var island in Enumerable.Range(0,pos.Length).GroupBy(Root))
        {
            Vector3 lo=pos[island.First()], hi=lo;
            foreach(int i in island) {lo=Vector3.Min(lo,pos[i]); hi=Vector3.Max(hi,pos[i]);}
            if(magazine ? lo.Y<-.232f && hi.Y<-.200f : lo.Z<-.100f)
                part.Moving.AddRange(island);
            if(magazine && lo.Y>.044f && lo.Z>-.060f && hi.Z<-.012f)
                part.Slider.AddRange(island);
        }
        return part;
    }

    private static readonly Dictionary<bool,List<ReloadMesh>> reloadMeshes=new();
    private static readonly Dictionary<bool,(Vector3 Travel,float Amount)> lastPartPose=new();
    internal static void PoseReloadPart(bool magazine,Vector3 travel,float amount)
    {
        if(lastPartPose.GetValueOrDefault(magazine)==(travel,amount)) return;
        if(!reloadMeshes.TryGetValue(magazine,out var parts))
        {
            var model=imports[magazine ? "weapon_uzi_90s" : "weapon_shotgun_90s"];
            reloadMeshes[magazine]=parts=new();
            for(int m=0;m<model.MeshCount;m++)
            {
                var part=MovingIslands(model.Meshes[m],magazine);
                if(part.Moving.Count+part.Slider.Count>0) parts.Add(part);
            }
            if(parts.Sum(p=>p.Moving.Count)==0 || magazine && parts.Sum(p=>p.Slider.Count)==0)
                throw new InvalidDataException("Missing animated weapon geometry");
        }
        lastPartPose[magazine]=(travel,amount);
        Vector3 hinge=new(0,-.013f,-.127f);
        var tilt=Quaternion.CreateFromAxisAngle(Vector3.UnitX,-amount);
        Vector3 magLocal=new Vector3(-travel.X,travel.Y,-travel.Z)/.75f;
        foreach(var part in parts)
        {
            foreach(int i in part.Moving)
            {
                Vector3 rest=part.Positions[i];
                part.Animated[i]=magazine ? rest+magLocal : hinge+Vector3.Transform(rest-hinge,tilt);
                if(!magazine) part.Rotated[i]=Vector3.Transform(part.Normals[i],tilt);
            }
            foreach(int i in part.Slider) part.Animated[i]=part.Positions[i]+Vector3.UnitZ*(amount/.75f);
            fixed(Vector3* p=part.Animated)
                Raylib.UpdateMeshBuffer(part.Mesh,0,p,part.Animated.Length*sizeof(Vector3),0);
            if(!magazine)
                fixed(Vector3* n=part.Rotated)
                    Raylib.UpdateMeshBuffer(part.Mesh,2,n,part.Rotated.Length*sizeof(Vector3),0);
        }
    }
}
