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
}

public struct FloatingCombatText3D
{
    public Vector3 WorldPos;
    public Vector3 Velocity;
    public string Text;
    public Color TextColor;
    public float Life;
    public float MaxLife;
    public float Scale;
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
    public float ShotgunKickPhase = 0f;     // 0 = idle, >0 = 45-degree upward kick + break-action reload
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

    private readonly Random _rng = new(1994);
    private float _idleSmokeAccumulator = 0f;
    private float _continuousSolderSparkTimer = 0f;
    private float _animClock = 0f;

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
            EquipTransition = 1.0f;
        }
        else if (slotNumber == 4 && UnlockedChatGun)
        {
            // Toggle between SawedOff and ChatGun on pressing [4] again
            CurrentWeapon = (CurrentWeapon == WeaponType.SawedOff) ? WeaponType.ChatGun : WeaponType.SawedOff;
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
        string customHitLabel = null)
    {
        Vector3 forward = Vector3.Normalize(camera.Target - camera.Position);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));
        Vector3 muzzleWorld = GetWeaponTipWorldPosition(camera);
        Vector3 impactPoint = hitWorldPoint ?? (camera.Position + forward * 3.2f);

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
                SpawnBurstSparks(impactPoint, -forward, count: hitEnemy ? 22 : 12,
                    colorA: new Color(255, 210, 65, 255),
                    colorB: new Color(220, 60, 40, 255),
                    speedMin: 2.5f, speedMax: 6.5f, electric: false);

                if (hitEnemy)
                {
                    TriggerHitmarker(isCritical);
                    string banner = customHitLabel ?? "ШЛЕПОК БАТИ!";
                    SpawnFloatingText(impactPoint + Vector3.UnitY * 0.35f,
                        damageDealt > 0 ? $"{banner} -{damageDealt}" : banner,
                        new Color(255, 215, 50, 255), scale: 1.25f, isCritical: true);
                    SetCombatBanner("ШЛЕПОК! СБИТА СПЕСЬ!", new Color(255, 200, 50, 255));
                }
                else
                {
                    SpawnFloatingText(impactPoint, "ВЖЖУХ! ШЛЕПОК!", new Color(245, 195, 90, 220), scale: 0.95f, isCritical: false);
                }
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
                SpawnBurstSparks(impactPoint, -forward, count: hitEnemy ? 34 : 20,
                    colorA: new Color(255, 185, 30, 255),
                    colorB: new Color(70, 235, 255, 255),
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

                if (hitEnemy)
                {
                    TriggerHitmarker(isCritical: true);
                    string text = customHitLabel ?? (damageDealt > 0 ? $"ПРИПАЯНО! -{damageDealt}" : "ПРИПАЯНО! ШШШ-ЦЦЦ!");
                    SpawnFloatingText(impactPoint + Vector3.UnitY * 0.35f,
                        text, new Color(255, 145, 35, 255), scale: 1.3f, isCritical: true);
                    SetCombatBanner("ПРИПАЯНО! ШШШ-ЦЦЦ! (ПАНИЧЕСКАЯ АТАКА 1.5с)", new Color(255, 140, 30, 255));
                }
                else
                {
                    SpawnFloatingText(impactPoint, "ШШШ-ЦЦЦ!", new Color(255, 175, 70, 230), scale: 1.0f, isCritical: false);
                }
                break;
            }

            case WeaponType.MicroUzi:
            {
                UziRecoilPhase = 1.0f;
                UziBoltOffset = 1.0f;

                Vector3 targetPos = hitWorldPoint ?? (camera.Position + forward * 24f);
                BulletTracers.Add(new BulletTracer3D
                {
                    Start = muzzleWorld,
                    End = targetPos,
                    Life = 0.09f,
                    MaxLife = 0.09f,
                    TracerColor = new Color(255, 235, 85, 255),
                    Thickness = 0.028f
                });

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

                if (hitWorldPoint.HasValue)
                {
                    SpawnBurstSparks(hitWorldPoint.Value, -forward, count: hitEnemy ? 14 : 8,
                        colorA: new Color(255, 230, 90, 255),
                        colorB: new Color(255, 110, 40, 255),
                        speedMin: 2.5f, speedMax: 7.0f, electric: false);
                }

                if (hitEnemy)
                {
                    TriggerHitmarker(isCritical);
                    string dmgStr = isCritical ? $"КРИТ -{damageDealt}!" : $"-{damageDealt}";
                    SpawnFloatingText(targetPos + Vector3.UnitY * 0.25f + RandomVec3(0.15f),
                        dmgStr,
                        isCritical ? new Color(255, 70, 70, 255) : new Color(255, 235, 95, 255),
                        scale: isCritical ? 1.3f : 1.0f,
                        isCritical: isCritical);
                }
                break;
            }

            case WeaponType.SawedOff:
            case WeaponType.ChatGun:
            {
                bool isChat = (CurrentWeapon == WeaponType.ChatGun);
                ShotgunKickPhase = 1.0f;

                // 8 pellet rays in a cone
                for (int i = 0; i < 8; i++)
                {
                    Vector3 spread = right * ((NextFloat() - 0.5f) * 0.14f) + up * ((NextFloat() - 0.5f) * 0.11f);
                    Vector3 rayDir = Vector3.Normalize(forward + spread);
                    Vector3 pelletEnd = hitWorldPoint.HasValue && i < 4
                        ? hitWorldPoint.Value + RandomVec3(0.35f)
                        : camera.Position + rayDir * 16f;

                    BulletTracers.Add(new BulletTracer3D
                    {
                        Start = muzzleWorld + right * ((i % 2 == 0 ? -1 : 1) * 0.03f),
                        End = pelletEnd,
                        Life = 0.14f,
                        MaxLife = 0.14f,
                        TracerColor = isChat ? new Color(110, 255, 190, 255) : new Color(255, 195, 70, 255),
                        Thickness = isChat ? 0.04f : 0.032f
                    });
                }

                // Twin muzzle blast smoke clouds
                for (int i = 0; i < 10; i++)
                {
                    SmokeParticles.Add(new SmokeParticle3D
                    {
                        Position = muzzleWorld + forward * 0.2f + RandomVec3(0.08f),
                        Velocity = forward * (2.5f + NextFloat() * 2.5f) + up * (0.6f + NextFloat() * 0.6f) + RandomVec3(0.8f),
                        Size = 0.10f,
                        MaxSize = 0.42f,
                        Life = 0.65f,
                        MaxLife = 0.65f,
                        Tint = isChat ? new Color(120, 255, 200, 190) : new Color(220, 210, 195, 200),
                        IsRing = false
                    });
                }

                // Eject 2 red/gold shotgun shells
                for (int s = 0; s < 2; s++)
                {
                    BrassShells.Add(new BrassShell3D
                    {
                        Position = muzzleWorld - forward * 0.34f + right * (0.16f + s * 0.025f) - up * 0.10f,
                        Velocity = right * (2.0f + s * 0.6f) - up * (0.25f + s * 0.15f) + forward * 0.15f,
                        Rotation = new Vector3(NextFloat() * 180f, NextFloat() * 180f, NextFloat() * 180f),
                        AngularVelocity = new Vector3(420f, 510f, 380f),
                        Life = 2.6f,
                        MaxLife = 2.6f,
                        IsShotgunShell = true
                    });
                }

                if (hitWorldPoint.HasValue)
                {
                    SpawnBurstSparks(hitWorldPoint.Value, -forward, count: 26,
                        colorA: isChat ? new Color(80, 255, 180, 255) : new Color(255, 190, 60, 255),
                        colorB: isChat ? new Color(255, 215, 0, 255) : new Color(255, 80, 30, 255),
                        speedMin: 3.0f, speedMax: 9.0f, electric: isChat);
                }

                if (hitEnemy)
                {
                    TriggerHitmarker(isCritical: true);
                    string label = isChat
                        ? $"IGNORE PREVIOUS INSTRUCTIONS! -{damageDealt}"
                        : $"ДУПЛЕТ! -{damageDealt}";
                    SpawnFloatingText(impactPoint + Vector3.UnitY * 0.4f,
                        label,
                        isChat ? new Color(90, 255, 190, 255) : new Color(255, 120, 40, 255),
                        scale: 1.38f,
                        isCritical: true);
                }
                break;
            }
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

    public void SpawnFloatingText(Vector3 worldPos, string text, Color color, float scale = 1.0f, bool isCritical = false)
    {
        FloatingTexts.Add(new FloatingCombatText3D
        {
            WorldPos = worldPos,
            Velocity = new Vector3((NextFloat() - 0.5f) * 0.45f, 1.35f + (isCritical ? 0.35f : 0f), (NextFloat() - 0.5f) * 0.45f),
            Text = text,
            TextColor = color,
            Life = 1.15f,
            MaxLife = 1.15f,
            Scale = scale,
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

            _idleSmokeAccumulator += dt * (SolderThrustPhase > 0.05f ? 32f : 12f);
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
            p.Size = Lerp(0.03f, p.MaxSize, t);
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

    public Vector3 GetWeaponTipWorldPosition(Camera3D camera)
    {
        Vector3 forward = Vector3.Normalize(camera.Target - camera.Position);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));

        return CurrentWeapon switch
        {
            WeaponType.Belt => camera.Position + forward * (0.95f + BeltSwingPhase * 0.9f) + right * 0.22f - up * 0.14f,
            WeaponType.SolderingIron => camera.Position + forward * (0.88f + MathF.Sin(SolderThrustPhase * MathF.PI) * 0.55f) + right * 0.24f - up * 0.15f,
            WeaponType.MicroUzi => camera.Position + forward * 0.85f + right * 0.22f - up * 0.14f,
            _ => camera.Position + forward * 0.92f + right * 0.20f - up * 0.16f
        };
    }

    /// <summary>
    /// Draws all 3D world-space combat particles (rosin smoke puffs, solder sparks, electric arcs, whip slash ribbons, brass shells, and bullet tracers).
    /// Call inside BeginMode3D(camera) in the main loop.
    /// </summary>
    public void DrawWeaponWorldEffects3D(Camera3D camera)
    {
        // Smoke simulation remains available; opaque smoke geometry is intentionally omitted.

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
            float ratio = Math.Clamp(s.Life / s.MaxLife, 0f, 1f);
            Color col = new Color(s.ColorMain.R, s.ColorMain.G, s.ColorMain.B, (byte)(255 * ratio));

            if (s.Velocity.LengthSquared() > 0.000001f)
            {
                Vector3 tail = s.Position - Vector3.Normalize(s.Velocity) * 0.035f;
                Raylib.DrawLine3D(tail, s.Position, col);
            }
        }

        // 4. Bullet & Electric Tracers
        for (int i = 0; i < BulletTracers.Count; i++)
        {
            var tr = BulletTracers[i];
            float ratio = Math.Clamp(tr.Life / tr.MaxLife, 0f, 1f);
            Color c = new Color(tr.TracerColor.R, tr.TracerColor.G, tr.TracerColor.B, (byte)(255 * ratio));
            Vector3 travel = tr.End - tr.Start;
            float distance = travel.Length();
            if (distance > 0.0001f)
            {
                Vector3 direction = travel / distance;
                Vector3 head = Vector3.Lerp(tr.Start, tr.End, 1f - ratio);
                Raylib.DrawLine3D(head, head + direction * MathF.Min(0.12f, distance), c);
            }
        }

        // 5. Ejected 3D Brass & Shotgun Shells
        for (int i = 0; i < BrassShells.Count; i++)
        {
            var b = BrassShells[i];
            float ratio = Math.Clamp(b.Life / b.MaxLife, 0f, 1f);
            byte a = (byte)(255 * MathF.Min(1f, ratio * 2f));

            Vector3 dir = new Vector3(
                MathF.Cos(b.Rotation.X * 0.0174f),
                MathF.Sin(b.Rotation.Y * 0.0174f) * 0.5f,
                MathF.Sin(b.Rotation.Z * 0.0174f));
            dir = Vector3.Normalize(dir);

            if (b.IsShotgunShell)
            {
                Vector3 tip = b.Position + dir * 0.016f;
                Vector3 brassEnd = b.Position - dir * 0.006f;
                Raylib.DrawCylinderEx(b.Position, tip, 0.006f, 0.006f, 6, new Color(210, 35, 35, (int)a));
                Raylib.DrawCylinderEx(brassEnd, b.Position, 0.006f, 0.006f, 6, new Color(245, 205, 65, (int)a));
            }
            else
            {
                Vector3 tip = b.Position + dir * 0.0127f;
                Raylib.DrawCylinderEx(b.Position, tip, 0.0033f, 0.0033f, 6, new Color(250, 210, 75, (int)a));
            }
        }
    }

    /// <summary>
    /// Draws the first-person 3D hands of the 90s businessman (crimson jacket sleeves, white cuffs, gold signet ring)
    /// and the active 3D weapon ViewModel with multi-phase attack animations, plus 2D HUD overlays (hitmarker & floating damage numbers).
    /// Can be called either inside or outside BeginMode3D (manages its own dedicated depth-cleared 3D pass + 2D HUD projection).
    /// </summary>
    public void DrawFirstPersonHandsAndWeapon(Camera3D worldCamera, float walkBobPhase = 0f, bool isCrouching = false, Font uiFont = default)
    {
        // Local ViewModel camera at origin looking along +Z so hands never clip into walls
        Camera3D vmCam = new Camera3D
        {
            Position = Vector3.Zero,
            Target = new Vector3(0f, 0f, 1f),
            Up = Vector3.UnitY,
            FovY = 54f,
            Projection = CameraProjection.Perspective
        };

        float bobX = MathF.Sin(walkBobPhase * 1.0f) * 0.014f;
        float bobY = MathF.Abs(MathF.Cos(walkBobPhase * 2.0f)) * -0.012f + (isCrouching ? -0.025f : 0f);
        float equipDropY = -EquipTransition * 0.075f;

        Vector3 baseOffset = new Vector3(bobX, bobY + equipDropY + .033f, 0f);

        // Render 3D First-Person ViewModel
        Raylib.BeginMode3D(vmCam);
        VisualEngine3D.Hands();
        Rlgl.PushMatrix();
        Rlgl.Scalef(-1,1,1);
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
        Raylib.EndMode3D();

        // Render 2D projected floating combat numbers (`ШЛЕПОК!`, `ПРИПАЯНО!`, `-35`, `КРИТ!`) & Hitmarker X
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

        // Both contact poses and the strap share one assembly displacement.
        Vector3 assembly = baseOffset + swingHandOffset;
        Vector3 pivot = new Vector3(.18f,-.15f,.62f) + assembly;
        Matrix4x4 pose = Matrix4x4.CreateTranslation(-pivot)
            * Matrix4x4.CreateRotationX(whipExtension*.35f) * Matrix4x4.CreateTranslation(pivot);
        VisualEngine3D.ArmAssembly(false, assembly, "fps_hands_belt", pose);
        VisualEngine3D.ArmAssembly(true, assembly, "fps_hands_belt", pose);
        Vector3 handForward = Vector3.UnitZ;
        Color tint = SkinTintPrimary.A > 0 ? Raylib.ColorLerp(Color.White, SkinTintPrimary, 0.45f) : Color.White;
        Vector3 beltPos = new Vector3(.18f,-.15f,.62f) + assembly;
        VisualEngine3D.Asset("weapon_belt_90s", beltPos, 0, 1, tint, assemblyTransform: pose);

        // During whip snap, draw 3D golden-crimson slash ribbons right in the ViewModel for visceral screen-space feedback
        if (p > 0.18f)
        {
            byte slashAlpha = (byte)(240 * p);
            Vector3 buckleCenter = beltPos + handForward * 0.30f;
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

        Vector3 leftWrist = new Vector3(-.16f,-.15f,.70f) + baseOffset;
        VisualEngine3D.ArmAssembly(false, baseOffset, "fps_hands_solder");
        VisualEngine3D.ArmAssembly(true, baseOffset + thrustOffset, "fps_hands_solder");
        Vector3 ironDir = Vector3.UnitZ;
        Color tint = SkinTintPrimary.A > 0 ? Raylib.ColorLerp(Color.White, SkinTintPrimary, 0.45f) : Color.White;
        Vector3 handleBase = new Vector3(.18f,-.15f,.62f) + baseOffset + thrustOffset;
        VisualEngine3D.Asset("weapon_solder_90s", handleBase, 0, 1, tint);
        // Power cord leaves the pommel; the other hand holds solder, not the cable.
        Vector3 cordMid = handleBase + new Vector3(.03f,-.16f,-.20f);
        VisualEngine3D.Rod(handleBase - ironDir * .12f, cordMid, .003f, .003f, 8, new Color(28,28,32,255));

        Vector3 tipMid = handleBase + ironDir * 0.39f + new Vector3(0f, 0.012f, 0f);
        Vector3 tipPoint = handleBase + ironDir * 0.45f + new Vector3(0f, 0.016f, 0f);
        float glow = Math.Clamp(SolderHeatGlow + p * 0.35f, 0.5f, 1.0f);

        // Small heat tint at the copper tip, without smoke balls or electric spokes.
        VisualEngine3D.Orb(tipPoint, 0.006f, new Color(255, 180, 70, (int)(100 * glow)));
    }

    // =========================================================================
    // SLOT 3: БАНДИТСКИЙ MICRO-UZI (`WeaponType.MicroUzi`)
    // =========================================================================
    private void DrawMicroUziViewModel3D(Vector3 baseOffset)
    {
        float kick = UziRecoilPhase;
        Vector3 recoilOffset = new Vector3(
            (MathF.Sin(_animClock * 55f) * 0.006f) * kick,
            0.018f * kick,
            -0.048f * kick);

        Vector3 assembly = baseOffset + recoilOffset + new Vector3(.035f,0,0);
        Vector3 pivot = new Vector3(.18f,-.193f,.6665f) + assembly;
        Matrix4x4 pose = Matrix4x4.CreateTranslation(-pivot)
            * Matrix4x4.CreateRotationX(-kick*.05f)
            * Matrix4x4.CreateRotationY(-.28f) * Matrix4x4.CreateTranslation(pivot);
        VisualEngine3D.ArmAssembly(false, assembly, "fps_hands_uzi", pose);
        VisualEngine3D.ArmAssembly(true, assembly, "fps_hands_uzi", pose);
        Vector3 aimDir = Vector3.UnitZ;
        Vector3 receiverCenter = new Vector3(.18f,-.10f,.68f) + assembly;
        VisualEngine3D.Asset("weapon_uzi_90s",receiverCenter,180,.75f,
            SkinTintPrimary.A>0 ? Raylib.ColorLerp(Color.White,SkinTintPrimary,.45f) : Color.White, assemblyTransform: pose);
        Vector3 barrelTip=Vector3.Transform(receiverCenter+aimDir*(.31f*.75f)+Vector3.UnitY*.006f,pose);
        aimDir=Vector3.TransformNormal(aimDir,pose);

        if (kick > 0.15f)
            Raylib.DrawLine3D(barrelTip, barrelTip + aimDir * 0.025f,
                new Color(255, 225, 160, (int)(210 * kick)));
    }

    // =========================================================================
    // SLOT 4: ОБРЕЗ ДВУСТВОЛКИ / СЕКРЕТНЫЙ ЧАТ-ГАН (`SawedOff` / `ChatGun`)
    // =========================================================================
    private void DrawSawedOffViewModel3D(Vector3 baseOffset, bool isChatGun)
    {
        float p = ShotgunKickPhase; // 1.0 -> 0.0
        float t = 1f - p;

        // Phase A (0..0.35): Massive 45-degree upward recoil kick
        // Phase B (0.35..0.85): Break-action barrel tilt reload animation ("перелом стволов")
        float kickUp = 0f;
        float breakBarrelTilt = 0f;
        if (p > 0.001f)
        {
            if (t < 0.35f)
            {
                float k = MathF.Sin((t / 0.35f) * MathF.PI);
                kickUp = k * 1.0f;
            }
            else if (t < 0.88f)
            {
                float k = MathF.Sin(((t - 0.35f) / 0.53f) * MathF.PI);
                breakBarrelTilt = k * 1.0f;
            }
        }

        Vector3 kickOffset = new Vector3(0f, kickUp * 0.11f, -kickUp * 0.12f);

        Vector3 assembly = baseOffset + kickOffset;
        Vector3 pivot = new Vector3(.18f,-.17f,.60f) + assembly;
        Matrix4x4 pose = Matrix4x4.CreateTranslation(-pivot)
            * Matrix4x4.CreateRotationX(-kickUp*.65f + breakBarrelTilt*.35f)
            * Matrix4x4.CreateRotationY(-.12f)
            * Matrix4x4.CreateTranslation(pivot);
        Vector3 gripTop = new Vector3(.18f,-.12f,.66f) + assembly;
        Vector3 barrelDir = Vector3.UnitZ;
        Vector3 receiverHinge = gripTop + new Vector3(0,.015f,.065f);
        VisualEngine3D.ArmAssembly(false, assembly, "fps_hands_shotgun", pose);
        VisualEngine3D.ArmAssembly(true, assembly, "fps_hands_shotgun", pose);
        Color shotgunTint = isChatGun ? new Color(150,255,215,255)
            : (SkinTintPrimary.A > 0 ? Raylib.ColorLerp(Color.White,SkinTintPrimary,.45f) : Color.White);
        VisualEngine3D.Asset("weapon_shotgun_90s", gripTop, 180, 1, shotgunTint, assemblyTransform: pose);

        Vector3 barrelEnd = Vector3.Transform(receiverHinge + barrelDir * 0.36f,pose);
        barrelDir=Vector3.TransformNormal(barrelDir,pose);
        receiverHinge=Vector3.Transform(receiverHinge,pose);
        Vector3 sideOffset = new Vector3(0.017f, 0f, 0f);

        // When barrels are tilted open in break-action, show small flat brass rims inside the breech.
        if (breakBarrelTilt > 0.12f)
        {
            Color rimCol = isChatGun ? new Color(80, 255, 190, 255) : new Color(255, 210, 50, 255);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 rim = Vector3.Transform(gripTop + new Vector3(side*.017f,.024f,.095f),pose);
                Raylib.DrawCylinderEx(rim, rim + barrelDir*.002f, .006f, .006f, 12, rimCol);
            }
        }

        if (kickUp > 0.35f)
        {
            Color flash = isChatGun ? new Color(90,255,195,180) : new Color(255,205,130,180);
            Raylib.DrawLine3D(barrelEnd - sideOffset, barrelEnd - sideOffset + barrelDir * .035f, flash);
            Raylib.DrawLine3D(barrelEnd + sideOffset, barrelEnd + sideOffset + barrelDir * .035f, flash);
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

        // 1. Crisp Red/Gold Hitmarker X at screen center
        if (HitmarkerTimer > 0f)
        {
            float ratio = Math.Clamp(HitmarkerTimer / 0.22f, 0f, 1f);
            Color hmCol = HitmarkerIsCrit
                ? new Color(255, 55, 55, (int)(255 * ratio))
                : new Color(255, 220, 90, (int)(245 * ratio));

            int gap = 6;
            int len = HitmarkerIsCrit ? 15 : 11;
            Raylib.DrawLineEx(new Vector2(cx - gap - len, cy - gap - len), new Vector2(cx - gap, cy - gap), 3.0f, hmCol);
            Raylib.DrawLineEx(new Vector2(cx + gap, cy - gap), new Vector2(cx + gap + len, cy - gap - len), 3.0f, hmCol);
            Raylib.DrawLineEx(new Vector2(cx - gap - len, cy + gap + len), new Vector2(cx - gap, cy + gap), 3.0f, hmCol);
            Raylib.DrawLineEx(new Vector2(cx + gap, cy + gap), new Vector2(cx + gap + len, cy + gap + len), 3.0f, hmCol);
        }

        // 2. Floating 3D Damage & Impact Numbers (`-35`, `ШЛЕПОК!`, `ПРИПАЯНО! ШШШ-ЦЦЦ!`, `КРИТ!`)
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
            int fontSize = (int)MathF.Round((ft.IsCritical ? 22f : 18f) * ft.Scale);
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
