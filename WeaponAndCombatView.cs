using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public enum WeaponType
{
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
    public WeaponType CurrentWeapon = WeaponType.Belt;
    public bool UnlockedSolderingIron = true;
    public bool UnlockedMicroUzi = true;
    public bool UnlockedSawedOff = true;
    public bool UnlockedChatGun = false;

    // Skin system from [K] Case Roulette
    public Color SkinTintPrimary = new Color(0, 0, 0, 0); // Alpha 0 = default authentic 90s look
    public Color SkinTintAccent = new Color(255, 215, 0, 255);
    public string CurrentSkinName = "Классика 1994 (Сток)";

    // Animation phases & timers
    public float BeltSwingPhase = 0f;       // 0 = idle, >0 = active whip swing (1.0 -> 0.0)
    public int BeltSwingCombo = 0;          // Alternates diagonal slash angle
    public float SolderThrustPhase = 0f;    // 0 = idle, >0 = active thrust & twist (1.0 -> 0.0)
    public float SolderHeatGlow = 0.65f;    // 0.5..1.0 glowing copper tip intensity
    public float UziRecoilPhase = 0f;       // 0 = idle, >0 = bolt kickback & muzzle flash
    public float UziBoltOffset = 0f;        // Reciprocating top cocking slider
    public float ShotgunKickPhase = 0f;     // 0 = idle, >0 = rearward impulse then muzzle climb
    public float EquipTransition = 0f;      // Smooth raise animation when switching slots 1..4

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
    private float _reloadTime, _flash, _roll;
    private const float UziFlashTime=.038f, ShotgunFlashTime=.055f;
    private float FlashDuration => CurrentWeapon==WeaponType.MicroUzi ? UziFlashTime : ShotgunFlashTime;
    private float FlashStrength => MathF.Sqrt(Math.Clamp(_flash/FlashDuration,0,1));
    private float _flashAngle;
    private Quaternion _boreAim=Quaternion.Identity;
    internal SkyscraperFloor1World BallisticWorld;
    private float _stridePhase;
    private bool _lowStance;
    internal float WalkWeight => _walkWeight;
    internal void SetStance(float stride,bool crouched) { _stridePhase=stride; _lowStance=crouched; }
    internal int Capacity => CurrentWeapon switch { WeaponType.MicroUzi=>32, WeaponType.SawedOff=>2, _=>0 };
    internal int Ammo => CurrentWeapon switch { WeaponType.MicroUzi=>_uziRounds, WeaponType.SawedOff=>_shells, _=>-1 };
    internal bool Reloading => _reloadTime > 0;
    private float ReloadDuration => CurrentWeapon==WeaponType.MicroUzi ? 1.25f : 1.35f;
    internal float ReloadProgress => Reloading ? 1-_reloadTime/ReloadDuration : 0;
    internal void UseRound()
    {
        if (CurrentWeapon==WeaponType.MicroUzi) _uziRounds--;
        if (CurrentWeapon==WeaponType.SawedOff) { _shells--; _spentHulls++; }
    }
    internal void BeginReload()
    {
        if (Reloading || Ammo<0 || Ammo==Capacity) return;
        _inspectTimer=0;
        _reloadTime=ReloadDuration;
        AudioMusicSystem.PlayActionClack(CurrentWeapon==WeaponType.SawedOff ? .72f : 1.30f);
    }

    private void TickReload(float dt, Camera3D camera)
    {
        if (!Reloading) return;
        float before=ReloadProgress;
        _reloadTime=Math.Max(0,_reloadTime-dt);
        float after=1-_reloadTime/ReloadDuration;
        if (before<.28f && after>=.28f && CurrentWeapon==WeaponType.SawedOff)
        {
            Vector3 f=Vector3.Normalize(camera.Target-camera.Position);
            Vector3 right=Vector3.Normalize(Vector3.Cross(f,Vector3.UnitY));
            for (int i=0;i<_spentHulls;i++)
                BrassShells.Add(new() {
                    Position=camera.Position+f*.52f+right*.16f-Vector3.UnitY*.12f,
                    Velocity=right*(1.4f+i*.6f)+Vector3.UnitY*(1.8f+i*.2f)-f*.6f,
                    AngularVelocity=new(300,510,270),Life=2.6f,MaxLife=2.6f,IsShotgunShell=true });
            _spentHulls=0;
        }
        if (before<.70f && after>=.70f) AudioMusicSystem.PlayActionClack(CurrentWeapon==WeaponType.MicroUzi ? 1.6f : 1.15f);
        if (_reloadTime==0)
        {
            if (CurrentWeapon==WeaponType.MicroUzi) _uziRounds=32;
            else _shells=2;
            AudioMusicSystem.PlayActionClack(CurrentWeapon==WeaponType.MicroUzi ? 1.05f : .63f);
        }
    }

    private readonly Random _rng = new(1994);
    private float _idleSmokeAccumulator = 0f;
    private float _continuousSolderSparkTimer = 0f;
    private float _animClock = 0f;
    private const float _inspectDuration = 2.4f;
    private float _inspectTimer, _walkWeight;
    private Vector2 _lookLag;
    internal bool Inspecting => _inspectTimer > 0;
    private float InspectPhase => Inspecting ? 1 - _inspectTimer / _inspectDuration : 0;

    public void TriggerInspect()
    {
        if (!Inspecting && !Reloading) _inspectTimer = _inspectDuration;
    }

    public void UpdateLookLag(Vector2 angularDelta, float dt)
    {
        _lookLag = Vector2.Clamp(_lookLag * MathF.Exp(-10f*dt) + angularDelta,
            new Vector2(-.16f), new Vector2(.16f));
    }

    private static float Ease(float t)
    {
        t = Math.Clamp(t, 0, 1);
        return t*t*(3-2*t);
    }

    private static float InspectLift(float t) => Ease(t/.28f)*Ease((1-t)/.23f);

    private static Matrix4x4 GetInspectTransform(int slot, float t)
    {
        float lift = InspectLift(t);
        if (lift == 0) return Matrix4x4.Identity;
        float turn = Ease((t-.37f)/.31f);
        Vector3 pivot = slot==1 ? new(0,-.15f,.60f) : new(.18f,-.15f,.62f);
        (Vector3 travel, Vector3 degrees) = slot switch
        {
            1 => (new Vector3(0,.095f,.045f), new Vector3(-19,8,-8+12*turn)),
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
        WeaponType target = slotNumber switch
        {
            1 => WeaponType.Belt,
            2 => WeaponType.SolderingIron,
            3 => WeaponType.MicroUzi,
            4 => UnlockedChatGun ? WeaponType.ChatGun : WeaponType.SawedOff,
            _ => CurrentWeapon
        };

        if (target != CurrentWeapon)
        {
            CurrentWeapon = target;
            _inspectTimer = _reloadTime = _flash = 0;
            EquipTransition = 1.0f;
        }
        else if (slotNumber == 4 && UnlockedChatGun)
        {
            // Toggle between SawedOff and ChatGun on pressing [4] again
            CurrentWeapon = (CurrentWeapon == WeaponType.SawedOff) ? WeaponType.ChatGun : WeaponType.SawedOff;
            _inspectTimer = _reloadTime = _flash = 0;
            EquipTransition = 1.0f;
        }
    }

    public void ApplySkin(string skinName, Color primary, Color accent)
    {
        CurrentSkinName = skinName;
        SkinTintPrimary = primary;
        SkinTintAccent = accent;
        EquipTransition = 0.65f;
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
        Vector3 forward = shotDirection ?? Vector3.Normalize(camera.Target-camera.Position);
        if (CurrentWeapon is WeaponType.SawedOff or WeaponType.ChatGun or WeaponType.MicroUzi)
        {
            _flash=FlashDuration;
            _flashAngle=NextFloat()*MathF.Tau;
            _roll=(NextFloat()-.5f)*.10f*(_lowStance ? .65f : 1);
            if (CurrentWeapon==WeaponType.MicroUzi) UziRecoilPhase=1;
            else ShotgunKickPhase=1;
            AimBore(camera,hitWorldPoint ?? camera.Position+forward*24);
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
                UziBoltOffset = 1.0f;

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
                float pattern=NextFloat()*MathF.Tau;
                for (int i=0;i<8;i++)
                {
                    // Alternate outer and inner rays. Only outer pellets catch the light.
                    float a=pattern+i*MathF.Tau/8+(NextFloat()-.5f)*.30f;
                    float radius=i==0 ? 0 : ((i%2==1 ? .054f : .024f)+NextFloat()*.017f);
                    var spread=(right*MathF.Cos(a)+up*MathF.Sin(a))*radius*(_lowStance ? .65f : 1);
                    var direction=Vector3.Normalize(forward+spread);
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
        _flash=Math.Max(0,_flash-dt);
        TickReload(dt,camera);
        _inspectTimer = MathF.Max(0,_inspectTimer-dt);
        _walkWeight = Lerp(_walkWeight,isMoving ? 1 : 0,1-MathF.Exp(-9*dt));

        // Smoothly decay animation phases
        if (EquipTransition > 0f)
            EquipTransition = MathF.Max(0f, EquipTransition - dt * 3.8f);

        if (BeltSwingPhase > 0f)
            BeltSwingPhase = MathF.Max(0f, BeltSwingPhase - dt * 3.2f);

        if (SolderThrustPhase > 0f)
            SolderThrustPhase = MathF.Max(0f, SolderThrustPhase - dt * 2.8f);

        if (UziRecoilPhase > 0f)
            UziRecoilPhase = MathF.Max(0f, UziRecoilPhase - dt * 9.5f);

        if (UziBoltOffset > 0f)
            UziBoltOffset = MathF.Max(0f, UziBoltOffset - dt * 14.0f);

        if (ShotgunKickPhase > 0f)
            ShotgunKickPhase = MathF.Max(0f, ShotgunKickPhase - dt * 1.85f);

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
    private float ViewRoll => (-12*Ease(EquipTransition)+_lookLag.X*13)*MathF.PI/180;

    private void AimBore(Camera3D camera,Vector3 target)
    {
        _boreAim=Quaternion.Identity;
        var gun=GunAssembly(BaseOffset());
        Matrix4x4.Invert(gun.Pose*GunToWorld(camera),out var toGun);
        Vector3 boreBase=gun.Receiver+new Vector3(MuzzleOffset.X,MuzzleOffset.Y,0);
        Vector3 line=Vector3.Transform(target,toGun)-boreBase;
        // A surface inside the viewmodel cannot be reached by a forward-pointing barrel.
        if (line.Z<=MuzzleOffset.Z) return;
        Vector3 dir=Vector3.Normalize(line);
        _boreAim=Quaternion.Normalize(new Quaternion(Vector3.Cross(Vector3.UnitZ,dir),1+dir.Z));
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


        // 2. Whip Slash 3D Arc Ribbons (for Belt attack)
        for (int i = 0; i < WhipSlashes.Count; i++)
        {
            var w = WhipSlashes[i];
            float ratio = Math.Clamp(w.Life / w.MaxLife, 0f, 1f);
            Color outerCol = new Color(w.PrimaryColor.R, w.PrimaryColor.G, w.PrimaryColor.B, (byte)(240 * ratio));
            Color coreCol = new Color(w.CoreColor.R, w.CoreColor.G, w.CoreColor.B, (byte)(255 * ratio));

            const int segments = 14;
            Vector3 prev = w.Start;
            for (int seg = 1; seg <= segments; seg++)
            {
                float t = seg / (float)segments;
                Vector3 cur = EvalQuadraticBezier(w.Start, w.Control, w.End, t);
                float radius = (0.042f * MathF.Sin(t * MathF.PI) + 0.008f) * ratio;

                Raylib.DrawCylinderEx(prev, cur, radius, radius * 0.85f, 6, outerCol);
                Raylib.DrawLine3D(prev, cur, coreCol);
                prev = cur;
            }
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
        float r=hull ? .0085f : .0045f;
        Vector3 cap=basePos+axis*(hull ? .012f : .004f);
        var brass=new Color(154,119,66,255);
        VisualEngine3D.Rod(basePos,cap,r*1.08f,r*1.08f,8,brass);
        Vector3 lip=cap+axis*(hull ? .038f : .014f);
        VisualEngine3D.Rod(cap,lip,r,r,10,hull ? new(125,42,32,255) : brass);
        VisualEngine3D.Rod(lip,lip+axis*.0005f,r*.80f,r*.80f,10,new(49,40,32,255));
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
        VisualEngine3D.Hands(InspectLift(InspectPhase));
        if (_flash>0)
        {
            VisualEngine3D.FlashLight(Vector3.Transform(GunAssembly(baseOffset).Tip,GunToView),FlashStrength);
        }
        Rlgl.PushMatrix();
        Rlgl.Scalef(-1,1,1);
        Rlgl.Rotatef(ViewRoll*180/MathF.PI,0,0,1);
        Rlgl.DisableBackfaceCulling();

        switch (CurrentWeapon)
        {
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
    private void DrawBeltViewModel3D(Vector3 baseOffset)
    {
        float p = BeltSwingPhase; // 1.0 -> 0.0 during swing
        float t = 1f - p;         // 0.0 -> 1.0 progress

        // Multi-phase whip animation:
        // Phase A (0..0.28): Wind-up to upper-right
        // Phase B (0.28..0.68): Fast diagonal lash across screen + leather strap extension forward in +Z
        // Phase C (0.68..1.0): Recover to idle stance
        Vector3 swingHandOffset = Vector3.Zero;
        float whipExtension = 0f;
        float lashSide = (BeltSwingCombo == 0) ? 1f : -1f;

        if (p > 0.001f)
        {
            if (t < 0.28f)
            {
                float k = t / 0.28f;
                swingHandOffset = new Vector3(0.16f * k * lashSide, 0.14f * k, -0.08f * k);
                whipExtension = -0.15f * k;
            }
            else if (t < 0.68f)
            {
                float k = (t - 0.28f) / 0.40f;
                swingHandOffset = new Vector3(
                    Lerp(0.16f * lashSide, -0.34f * lashSide, k),
                    Lerp(0.14f, -0.18f, k),
                    MathF.Sin(k * MathF.PI) * 0.34f);
                whipExtension = MathF.Sin(k * MathF.PI) * 0.72f;
            }
            else
            {
                float k = (t - 0.68f) / 0.32f;
                swingHandOffset = new Vector3(
                    Lerp(-0.34f * lashSide, 0f, k),
                    Lerp(-0.18f, 0f, k),
                    Lerp(0.08f, 0f, k));
                whipExtension = (1f - k) * 0.18f;
            }
        }

        // Rigid 3/4 combat stance for the Belt + Hands assembly:
        // Positioned at -0.028f Y so there is a clean gap BOTH above the buckle (to the Tarot subtitle box)
        // and below the buckle (above the white border of hotbar slot [1]).
        Vector3 assembly = baseOffset + swingHandOffset + new Vector3(0.012f, -0.028f, 0.015f);
        Vector3 beltPos = new Vector3(.18f,-.155f,.592f) + assembly;
        Vector3 pivotCenter = beltPos + new Vector3(-0.18f, 0f, 0f);
        Matrix4x4 pose = Matrix4x4.CreateTranslation(-pivotCenter)
            * Matrix4x4.CreateRotationZ(-0.14f)
            * Matrix4x4.CreateRotationY(0.19f)
            * Matrix4x4.CreateRotationX(0.06f + whipExtension * .35f)
            * Matrix4x4.CreateTranslation(pivotCenter) * GetInspectTransform(1,InspectPhase);
        float tension=InspectLift(InspectPhase);
        Vector3 spread=Vector3.UnitX*(.014f*tension);
        VisualEngine3D.ArmAssembly(false, assembly-spread, "fps_hands_belt", pose);
        VisualEngine3D.ArmAssembly(true, assembly+spread, "fps_hands_belt", pose);
        Color tint = SkinTintPrimary.A > 0 ? Raylib.ColorLerp(Color.White, SkinTintPrimary, 0.45f) : Color.White;
        VisualEngine3D.SetBeltTension(tension);
        VisualEngine3D.Asset("weapon_belt_90s", beltPos, 0, 1, tint, assemblyTransform: pose);
        VisualEngine3D.SetBeltTension(0);

        // During whip snap, draw 3D golden-crimson slash ribbons right in the ViewModel for visceral screen-space feedback
        if (p > 0.18f)
        {
            byte slashAlpha = (byte)(240 * p);
            // Match strap(.68) in build_weapon_belt, including the assembly swing.
            Vector3 buckleCenter = Vector3.Transform(
                beltPos + new Vector3(.09f-.58f*.68f, -.032f*4*.68f*.32f, 0), pose);
            Vector3 vStart = new Vector3(0.32f * lashSide, 0.22f, 0.72f);
            Vector3 vMid = buckleCenter;
            Vector3 vEnd = new Vector3(-0.36f * lashSide, -0.18f, 0.78f);
            VisualEngine3D.Rod(vStart, vMid, 0.014f * p, 0.024f * p, 6, new Color(255, 65, 45, (int)slashAlpha));
            VisualEngine3D.Rod(vMid, vEnd, 0.024f * p, 0.010f * p, 6, new Color(255, 215, 55, (int)slashAlpha));
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
            * Matrix4x4.CreateTranslation(handleBase) * GetInspectTransform(2,InspectPhase);

        VisualEngine3D.ArmAssembly(true, rightAssembly, "fps_hands_solder", rightPose);
        Color tint = SkinTintPrimary.A > 0 ? Raylib.ColorLerp(Color.White, SkinTintPrimary, 0.45f) : Color.White;
        VisualEngine3D.Asset("weapon_solder_90s", handleBase, 0, 1, tint, assemblyTransform: rightPose);
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
    private Vector3 BaseOffset()
    {
        float bobX = MathF.Sin(_stridePhase)*.018f*_walkWeight + .0032f*MathF.Sin(_animClock*1.7f);
        float bobY = -.016f*MathF.Cos(_stridePhase*2)*_walkWeight + .0042f*MathF.Sin(_animClock*2.1f)
                     + (_lowStance ? -.025f : 0);
        float equip = Ease(EquipTransition);
        float equipDropY = -.10f*equip;

        return new(bobX-_lookLag.X*.12f, bobY+equipDropY+.024f+_lookLag.Y*.10f, -.035f*equip);

    }

    private Vector3 MuzzleOffset => CurrentWeapon==WeaponType.MicroUzi
        ? new Vector3(0,.018f,.322f)*.75f : new Vector3(_shells==1 ? -.0175f : .0175f,.024f,.467f);

    private (Vector3 Offset,Matrix4x4 Pose,Vector3 Receiver,Vector3 Tip) GunAssembly(Vector3 baseOffset)
    {
        bool uzi=CurrentWeapon==WeaponType.MicroUzi;
        float phase=uzi ? UziRecoilPhase : ShotgunKickPhase;
        float age=(1-phase)/(uzi ? 9.5f : 1.85f);
        float steady=_lowStance ? .65f : 1;
        float push=phase*phase*steady;
        float climbTime=Math.Max(0,age-.035f);
        float spring=uzi ? 52 : 19;
        float climb=climbTime*spring*MathF.Exp(1-climbTime*spring)*Ease(phase/.20f)*steady;
        float t=ReloadProgress;
        float tilt=Ease(t/(uzi ? .20f : .26f))*Ease((1-t)/(uzi ? .23f : .20f));
        Vector3 offset=baseOffset+new Vector3(uzi ? .035f : 0,0,-(uzi ? .038f : .09f)*push);
        Vector3 pivot=offset+(uzi ? new Vector3(.18f,-.193f,.6665f) : new Vector3(.18f,-.17f,.60f));
        Vector3 receiver=offset+(uzi ? new Vector3(.18f,-.10f,.68f) : new Vector3(.18f,-.12f,.66f));
        Vector3 muzzle=MuzzleOffset;
        Vector3 boreBase=receiver+new Vector3(muzzle.X,muzzle.Y,0);
        var aim=Quaternion.Slerp(Quaternion.Identity,_boreAim,Ease(phase/(uzi ? .55f : .76f)));
        var pose=Matrix4x4.CreateTranslation(-boreBase)*Matrix4x4.CreateFromQuaternion(aim)
            * Matrix4x4.CreateTranslation(boreBase-pivot)
            * Matrix4x4.CreateRotationX(uzi ? -.075f*climb-.19f*tilt : -.23f*climb-.16f*tilt)
            * Matrix4x4.CreateRotationZ(_roll*climb+tilt*(uzi ? .50f : .14f))
            * Matrix4x4.CreateRotationY(uzi ? -.28f : -.12f)
            * Matrix4x4.CreateTranslation(pivot)*GetInspectTransform(uzi ? 3 : 4,InspectPhase);
        return (offset,pose,receiver,Vector3.Transform(receiver+muzzle,pose));
    }

    private void DrawMicroUziViewModel3D(Vector3 baseOffset)
    {
        var (assembly,pose,receiverCenter,_)=GunAssembly(baseOffset);
        float reload=ReloadProgress;
        float drop=.24f*Ease((reload-.15f)/.19f)*Ease((.72f-reload)/.19f);
        VisualEngine3D.ArmAssembly(false, assembly+new Vector3(.035f,-.57f,-.12f)*drop, "fps_hands_uzi", pose);
        VisualEngine3D.ArmAssembly(true, assembly, "fps_hands_uzi", pose);
        VisualEngine3D.PoseReloadPart(true,drop);
        VisualEngine3D.Asset("weapon_uzi_90s",receiverCenter,180,.75f,
            SkinTintPrimary.A>0 ? Raylib.ColorLerp(Color.White,SkinTintPrimary,.45f) : Color.White, assemblyTransform: pose);
        VisualEngine3D.PoseReloadPart(true,0);
    }

    // =========================================================================
    // SLOT 4: ОБРЕЗ ДВУСТВОЛКИ / СЕКРЕТНЫЙ ЧАТ-ГАН (`SawedOff` / `ChatGun`)
    // =========================================================================
    private void DrawSawedOffViewModel3D(Vector3 baseOffset, bool isChatGun)
    {
        var (assembly,pose,gripTop,_)=GunAssembly(baseOffset);
        float reload=ReloadProgress;
        float breakBarrelTilt=Ease(reload/.26f)*Ease((1-reload)/.20f);
        VisualEngine3D.ArmAssembly(false, assembly+new Vector3(0,-.065f,-.095f)*breakBarrelTilt, "fps_hands_shotgun", pose);
        VisualEngine3D.ArmAssembly(true, assembly, "fps_hands_shotgun", pose);
        Color shotgunTint = isChatGun ? new Color(150,255,215,255)
            : (SkinTintPrimary.A > 0 ? Raylib.ColorLerp(Color.White,SkinTintPrimary,.45f) : Color.White);
        VisualEngine3D.PoseReloadPart(false,.68f*breakBarrelTilt);
        VisualEngine3D.Asset("weapon_shotgun_90s", gripTop, 180, 1, shotgunTint, assemblyTransform: pose);

        VisualEngine3D.PoseReloadPart(false,0);
        Vector3 barrelDir=Vector3.TransformNormal(Vector3.UnitZ,pose);

        if (reload>.35f && reload<.81f)
        {
            for(int i=0;i<2-_shells;i++)
            {
                float insert=Ease((reload-.39f-.08f*i)/.20f);
                Vector3 plug=gripTop+new Vector3(i==0 ? -.018f : .018f,.047f+(1-insert)*.065f,.093f-(1-insert)*.12f);
                DrawCasing(Vector3.Transform(plug,pose),barrelDir,hull: true);
            }
        }
    }

    // =========================================================================
    // 2D PROJECTED FLOATING DAMAGE NUMBERS & HITMARKER
    // =========================================================================
    private void DrawCombatFloatingTextsAndHitmarker2D(Camera3D worldCamera, Font uiFont = default)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
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

            Vector2 screenPos = Raylib.GetWorldToScreen(ft.WorldPos, worldCamera);
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
        WeaponType.Belt => "1: ОТЦОВСКИЙ КОЖАНЫЙ РЕМЕНЬ (ЗОЛОТАЯ ПРЯЖКА)",
        WeaponType.SolderingIron => "2: ПАЯЛЬНИК ИЗ 90-Х (СИНЯЯ ИЗОЛЕНТА + КАНИФОЛЬ)",
        WeaponType.MicroUzi => "3: БАНДИТСКИЙ MICRO-UZI (9mm АВТО)",
        WeaponType.SawedOff => "4: ОБРЕЗ ДВУСТВОЛКИ (12 КАЛИБР ДУПЛЕТ)",
        WeaponType.ChatGun => "4: СЕКРЕТНЫЙ ЧАТ-ГАН (ЗОЛОТЫЕ ПРОМПТЫ)",
        _ => "ОРУЖИЕ 90-Х"
    };

    public string GetWeaponAttackSoundFileName() => CurrentWeapon switch
    {
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
        public int[] Moving;
        public Vector3[] Positions, Normals, Animated, Rotated;
    }
    private static readonly Dictionary<bool,List<ReloadMesh>> reloadMeshes=new();
    private static readonly HashSet<bool> posedWeapons=new();

    internal static void PoseReloadPart(bool magazine,float amount)
    {
        if (amount==0 && !posedWeapons.Remove(magazine)) return;
        if (!reloadMeshes.TryGetValue(magazine,out var parts))
        {
            var model=imports[magazine ? "weapon_uzi_90s" : "weapon_shotgun_90s"];
            reloadMeshes[magazine]=parts=new();
            for(int m=0;m<model.MeshCount;m++)
            {
                Mesh mesh=model.Meshes[m];
                var positions=new ReadOnlySpan<Vector3>((Vector3*)mesh.Vertices,mesh.VertexCount).ToArray();
                var normals=new ReadOnlySpan<Vector3>((Vector3*)mesh.Normals,mesh.VertexCount).ToArray();
                var moving=new List<int>();
                for(int i=0;i<positions.Length;i++)
                {
                    Vector3 v=positions[i];
                    // The exposed steel magazine is narrower than the polymer grip.
                    bool moves=magazine ? m<2 && v.Y<-.202f && Math.Abs(v.X)<=.0221f && Math.Abs(v.Z-.019f)<=.0281f
                        : v.Z<-.090f;
                    if(moves) moving.Add(i);
                }
                if(moving.Count>0) parts.Add(new() { Mesh=mesh,Moving=moving.ToArray(),Positions=positions,
                    Normals=normals,Animated=(Vector3[])positions.Clone(),Rotated=(Vector3[])normals.Clone() });
            }
        }
        if(amount!=0) posedWeapons.Add(magazine);
        Vector3 hinge=new(0,.006f,-.078f);
        var tilt=Quaternion.CreateFromAxisAngle(Vector3.UnitX,-amount);
        foreach(var part in parts)
        {
            foreach(int i in part.Moving)
            {
                Vector3 rest=part.Positions[i];
                part.Animated[i]=magazine ? rest-Vector3.UnitY*amount : hinge+Vector3.Transform(rest-hinge,tilt);
                if(!magazine) part.Rotated[i]=Vector3.Transform(part.Normals[i],tilt);
            }
            fixed(Vector3* p=part.Animated)
                Raylib.UpdateMeshBuffer(part.Mesh,0,p,part.Animated.Length*sizeof(Vector3),0);
            if(!magazine)
                fixed(Vector3* n=part.Rotated)
                    Raylib.UpdateMeshBuffer(part.Mesh,2,n,part.Rotated.Length*sizeof(Vector3),0);
        }
    }
}
