#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

/// <summary>
/// First-person physical 3D boss intro set-piece synchronized to
/// <see cref="SkyscraperFloor1World.ElevatorBoss"/> Repairing (2.3s) and IntroSpeech (2.4s) timers.
/// Choreographs the 8-beat cinema sequence: false-calm elevator arrival, twin crimson eyes snap-on,
/// sudden jumpscare lunge & brutal shove onto the burgundy carpet, dazed look-around spotting the
/// dropped 3D shotgun, floor-locked Terminator stomps + 4-rocket salvo setting the atrium on fire,
/// and physical weapon snatch + stand-up straight into live combat.
/// </summary>
internal static class BossIntroCutscene
{
    private const float CalmPhaseEndR = 0.52f;   // Repairing 0.00..0.52: false calm watching Floor 01 dial
    private const float EyeSnapR = 0.88f;        // Repairing 0.88..1.00: twin crimson eyes suddenly snap ON
    private const float ShoveImpactU = 0.13f;    // Doors whip open -> jumpscare lunge -> brutal shove impact!
    private const float FloorLandU = 0.28f;      // Player hurtles 4.2m backward and slams onto carpet (Y=0.44m)
    private const float GlanceShotgunU = 0.37f;  // End of dazed left-glance + dropped shotgun spot
    private const float LookUpEndU = 0.46f;      // Head snaps sharply UP at towering titan looming overhead
    private const float SalvoStartU = 0.62f;     // TOKEN-GUN 98 launches 4 plasma rockets across lobby
    private const float ExplosionStartU = 0.68f; // Rockets detonate and ignite the atrium
    private const float ExplosionEndU = 0.76f;
    private const float GrabStartU = 0.78f;      // Camera dips toward dropped 3D shotgun on carpet
    private const float GrabClackU = 0.83f;      // Crisp pickup clack right before dropped gun vanishes (0.84)
    private const float PullBackStartU = 0.85f;  // Hero surges upright straight into live 1st-person combat

    private const float BarHeight235 = 88f; // Pure matte-black 2.35:1 cinema aperture inside 1280x720 virtual UI space
    private static readonly Vector3 DroppedShotgunPos = new(0.62f, 0.06f, -12.95f);

    private static bool _skipped;
    private static int _lastAudioCueMask;

    internal readonly record struct IntroWalkSample(
        float OffsetZ,
        float OffsetY,
        float StridePhase,
        bool IsMoving,
        bool LeftSwing,
        float StepFrac,
        int CompletedSteps,
        float StompPulse,
        float ShovePulse,
        float Uncoil);

    internal static void Reset()
    {
        _skipped = false;
        _lastAudioCueMask = 0;
    }

    internal static void Skip() => _skipped = true;

    internal static void SyncState(SkyscraperFloor1World world)
    {
        var b = world.ElevatorBoss;
        if (!b.IsActive || (b.State != ElevatorBossState.Repairing && b.State != ElevatorBossState.IntroSpeech))
        {
            _skipped = false;
            _lastAudioCueMask = 0;
            return;
        }
        if (_skipped) return;

        // Ominous optical relay snap when twin crimson eyes suddenly ignite in the dark cab
        if (b.State == ElevatorBossState.Repairing)
        {
            float r = RepairProgress(b);
            if (r >= EyeSnapR && (_lastAudioCueMask & 256) == 0)
            {
                _lastAudioCueMask |= 256;
                AudioMusicSystem.PlayActionClack(0.76f);
            }
            return;
        }

        // Fire one-shot heavy physical impact, boot-stomp, rocket-salvo, explosion & shotgun-pickup SFX
        float u = IntroProgress(b);
        if (u >= ShoveImpactU && (_lastAudioCueMask & 1) == 0)
        {
            _lastAudioCueMask |= 1;
            AudioMusicSystem.PlayKnockout();
        }
        if (u >= FloorLandU && (_lastAudioCueMask & 2) == 0)
        {
            _lastAudioCueMask |= 2;
            AudioMusicSystem.PlayLanding();
        }
        if (u >= 0.37f && (_lastAudioCueMask & 4) == 0)
        {
            _lastAudioCueMask |= 4;
            AudioMusicSystem.PlayLanding();
        }
        if (u >= SalvoStartU && (_lastAudioCueMask & 32) == 0)
        {
            _lastAudioCueMask |= 32;
            AudioMusicSystem.PlayComicWhipSnap();
        }
        if (u >= 0.64f && (_lastAudioCueMask & 8) == 0)
        {
            _lastAudioCueMask |= 8;
            AudioMusicSystem.PlayLanding();
        }
        if (u >= 0.69f && (_lastAudioCueMask & 64) == 0)
        {
            _lastAudioCueMask |= 64;
            AudioMusicSystem.PlayComicBoom();
        }
        if (u >= 0.725f && (_lastAudioCueMask & 128) == 0)
        {
            _lastAudioCueMask |= 128;
            AudioMusicSystem.PlayComicBoom();
        }
        if (u >= 0.77f && (_lastAudioCueMask & 16) == 0)
        {
            _lastAudioCueMask |= 16;
            AudioMusicSystem.PlayLanding();
        }
        if (u >= GrabClackU && (_lastAudioCueMask & 512) == 0)
        {
            _lastAudioCueMask |= 512;
            AudioMusicSystem.PlayActionClack(1.12f);
        }
    }

    internal static bool IsActive(SkyscraperFloor1World world)
    {
        if (_skipped) return false;
        var b = world.ElevatorBoss;
        return b.IsActive && (b.State == ElevatorBossState.Repairing || b.State == ElevatorBossState.IntroSpeech);
    }

    internal static bool IsCameraOverrideActive(SkyscraperFloor1World world) => IsActive(world);

    internal static bool ShouldRelocatePlayerAfterKnockback(SkyscraperFloor1World world)
    {
        if (!IsActive(world)) return false;
        var b = world.ElevatorBoss;
        return b.State == ElevatorBossState.IntroSpeech && IntroProgress(b) >= FloorLandU;
    }

    internal static Vector3 KnockedBackStandPosition(SkyscraperFloor1World world)
        => world.NorthElevatorPos + new Vector3(0.88f, 1.72f, -5.85f);

    /// <summary>
    /// Exact biomechanical foot-locked walkout shared by <see cref="SkyscraperFloor1World"/> (root Z/Y)
    /// and <see cref="VisualEngine3D"/> (stance/swing leg angles) so planted boots NEVER skate!
    /// 4 discrete heavy 0.875m steps (+1.35 -> -2.15) with dead-still pauses during speech beats.
    /// </summary>
    internal static IntroWalkSample EvalIntroWalk(float u)
    {
        u = Math.Clamp(u, 0f, 1f);

        // 4 discrete step windows: [startU, endU], alternating Right(0), Left(1), Right(2), Left(3)
        // Step 0 (0.10..0.22): Threshold burst + left-arm shove (+1.35 -> +0.48)
        // Step 1 (0.26..0.38): Heavy stomp onto carpet (+0.48 -> -0.40) -> DEAD STILL PAUSE (0.38..0.54)
        // Step 2 (0.54..0.65): Towering stomp forward (-0.40 -> -1.27)
        // Step 3 (0.67..0.78): Final plant onto compass medallion (-1.27 -> -2.15) -> DEAD STILL STANCE (0.78..1.00)
        ReadOnlySpan<(float Start, float End, bool LeftSwing)> steps =
        [
            (0.10f, 0.22f, false),
            (0.26f, 0.38f, true),
            (0.54f, 0.65f, false),
            (0.67f, 0.78f, true)
        ];

        const float startZ = 1.35f;
        const float stepLen = 0.875f; // Exact anatomical single-step distance for 2.95m boss (L_eff = 1.465m)

        float progressSteps = 0f;
        int completed = 0;
        bool isMoving = false;
        bool leftSwing = false;
        float stepFrac = 0f;
        float stompPulse = 0f;

        for (int i = 0; i < steps.Length; i++)
        {
            var s = steps[i];
            if (u >= s.End)
            {
                progressSteps += 1f;
                completed = i + 1;
                // Crisp decaying heavy boot-impact pulse right after heel slam
                float after = (u - s.End) / 0.055f;
                if (after >= 0f && after < 1f)
                    stompPulse = MathF.Max(stompPulse, (1f - after) * (1f - after));
            }
            else if (u > s.Start)
            {
                float f = (u - s.Start) / (s.End - s.Start);
                float eased = SmoothStep(f);
                progressSteps += eased;
                isMoving = true;
                leftSwing = s.LeftSwing;
                stepFrac = f;
                if (f > 0.86f)
                    stompPulse = MathF.Max(stompPulse, SmoothStep((f - 0.86f) / 0.14f));
                break;
            }
        }

        float offsetZ = startZ - stepLen * progressSteps;
        float stepDown = SmoothStep((progressSteps - 0.25f) / 0.65f);
        float offsetY = 0.16f * (1f - stepDown);

        // Brutal left-arm lunge & shove impulse peaking right at ShoveImpactU (0.13)
        float shovePulse = 0f;
        if (u > 0.05f && u < 0.24f)
        {
            float sf = (u - 0.05f) / 0.19f;
            shovePulse = sf < 0.42f
                ? SmoothStep(sf / 0.42f)
                : 1f - SmoothStep((sf - 0.42f) / 0.58f);
        }

        // Smoothly uncoil from doorway crouch to towering 2.95m height across Step 1..Step 2
        float uncoil = SmoothStep((u - 0.26f) / 0.22f);

        return new IntroWalkSample(
            OffsetZ: offsetZ,
            OffsetY: offsetY,
            StridePhase: progressSteps * MathF.PI,
            IsMoving: isMoving,
            LeftSwing: leftSwing,
            StepFrac: stepFrac,
            CompletedSteps: completed,
            StompPulse: stompPulse,
            ShovePulse: shovePulse,
            Uncoil: uncoil);
    }

    internal static bool ShouldSuppressViewModel(SkyscraperFloor1World world)
    {
        if (!IsActive(world)) return false;
        var b = world.ElevatorBoss;
        if (b.State == ElevatorBossState.Repairing) return true;
        float u = IntroProgress(b);
        if (u <= PullBackStartU) return true;
        return SmoothStep((u - PullBackStartU) / (1f - PullBackStartU)) < 0.38f;
    }

    internal static float GetHudAlpha(SkyscraperFloor1World world)
    {
        if (!IsActive(world)) return 1f;
        var b = world.ElevatorBoss;
        if (b.State == ElevatorBossState.Repairing) return 0f;
        float u = IntroProgress(b);
        if (u <= PullBackStartU) return 0f;
        return SmoothStep((u - PullBackStartU) / (1f - PullBackStartU));
    }

    internal static float GetLetterboxProgress(SkyscraperFloor1World world)
    {
        if (!IsActive(world)) return 0f;
        var b = world.ElevatorBoss;
        if (b.State == ElevatorBossState.Repairing)
            return SmoothStep(RepairElapsed(b) / 0.30f);

        float u = IntroProgress(b);
        if (u <= PullBackStartU) return 1f;
        return 1f - SmoothStep((u - PullBackStartU) / (1f - PullBackStartU));
    }

    internal static Camera3D ResolveCamera(SkyscraperFloor1World world, Camera3D playerCam)
    {
        if (!IsActive(world)) return playerCam;
        var b = world.ElevatorBoss;

        if (b.State == ElevatorBossState.Repairing)
        {
            float elapsed = RepairElapsed(b);
            float r = RepairProgress(b);

            if (r < CalmPhaseEndR)
            {
                // BEAT 1A (r = 0.00..0.52): False calm — player stands casually watching the brass Floor 01 dial & frosted glass
                float t = SmoothStep(r / CalmPhaseEndR);
                Vector3 pos = Vector3.Lerp(new(-0.35f, 1.66f, -10.85f), new(0.04f, 1.66f, -10.25f), t);
                Vector3 tgt = Vector3.Lerp(new(-0.38f, 2.18f, -8.52f), new(0.00f, 1.72f, -7.80f), t);

                // Subtle relaxed breathing/step sway (zero panic or spoiler shake!)
                pos += new Vector3(MathF.Sin(elapsed * 2.1f) * 0.005f, MathF.Sin(t * MathF.PI * 2.0f) * 0.011f, 0f);

                return new Camera3D
                {
                    Position = pos,
                    Target = tgt,
                    Up = Vector3.UnitY,
                    FovY = LerpF(48f, 47f, t),
                    Projection = CameraProjection.Perspective
                };
            }
            else
            {
                // BEAT 1B (r = 0.52..1.00): Player steps close (Z = -10.20, 1.70m from doors!) expecting to board to Floor 2.
                // Glass clears to dark empty-looking cab... until r = 0.88..1.00 when TWO piercing crimson visor eyes snap ON!
                float t = SmoothStep((r - CalmPhaseEndR) / (1f - CalmPhaseEndR));
                Vector3 pos = Vector3.Lerp(new(0.04f, 1.66f, -10.25f), new(0.04f, 1.64f, -10.20f), t);
                Vector3 tgt = Vector3.Lerp(new(0.00f, 1.72f, -7.80f), new(0.00f, 1.68f, -7.15f), t);

                // Startled micro-flinch only right when the twin crimson visor eyes snap ON at r = 0.88..1.00
                float eyeSnap = SmoothStep((r - EyeSnapR) / 0.06f);
                float eyeTremor = eyeSnap * (1f - SmoothStep((r - 0.94f) / 0.06f)) * 0.012f;
                pos += new Vector3(
                    MathF.Sin(elapsed * 78f) * eyeTremor,
                    MathF.Cos(elapsed * 92f) * eyeTremor * 0.8f,
                    -0.025f * eyeSnap);

                return new Camera3D
                {
                    Position = pos,
                    Target = tgt,
                    Up = Vector3.UnitY,
                    FovY = LerpF(47f, 44f, t),
                    Projection = CameraProjection.Perspective
                };
            }
        }
        else
        {
            float u = IntroProgress(b);
            var walk = EvalIntroWalk(u);
            Camera3D pov = BuildPhysicalPovCamera(world, b, walk, Math.Min(u, PullBackStartU));
            if (u <= PullBackStartU) return pov;

            // BEAT 5B (u = 0.85..1.00): Hero surges cleanly upright (Y: 0.58m -> 1.72m, roll 0 deg) straight into playerCam!
            float blend = SmoothStep((u - PullBackStartU) / (1f - PullBackStartU));
            Vector3 up = Vector3.Normalize(Vector3.Lerp(pov.Up, playerCam.Up, blend));
            return new Camera3D
            {
                Position = Vector3.Lerp(pov.Position, playerCam.Position, blend),
                Target = Vector3.Lerp(pov.Target, playerCam.Target, blend),
                Up = up,
                FovY = LerpF(pov.FovY, playerCam.FovY, blend),
                Projection = CameraProjection.Perspective
            };
        }
    }

    private static Camera3D BuildPhysicalPovCamera(
        SkyscraperFloor1World world, EscapedAgentBoss b, IntroWalkSample walk, float u)
    {
        Vector3 north = world.NorthElevatorPos;
        Vector3 standAtDoor  = north + new Vector3(0.04f, 1.64f, -1.70f); // (0.04, 1.64, -10.20) ONLY 1.70m from doors!
        Vector3 impactOrigin = standAtDoor + new Vector3(-0.02f, -0.05f, -0.14f);
        Vector3 floorImpact  = north + new Vector3(0.88f, 0.44f, -5.85f); // (0.88, 0.44, -14.35) 4.2m backward slam!
        Vector3 floorRest    = north + new Vector3(0.88f, 0.48f, -5.85f); // (0.88, 0.48, -14.35) dazed lying on carpet
        Vector3 bracePos     = north + new Vector3(0.94f, 0.72f, -5.92f); // (0.94, 0.72, -14.42) propping up during salvo
        Vector3 grabDipPos   = north + new Vector3(0.76f, 0.50f, -5.45f); // (0.76, 0.50, -13.95) reaching down-left for shotgun
        Vector3 surgeBasePos = north + new Vector3(0.84f, 0.58f, -5.72f); // (0.84, 0.58, -14.22) launch upright to 1.72m
        Vector3 emptyDeskGlance = new(3.15f, 0.92f, -12.35f);             // Empty atrium desk / vanished Veronika flank

        Vector3 pos;
        Vector3 tgt;
        float rollRad;
        float fov;

        if (u < ShoveImpactU)
        {
            // BEAT 2A (u = 0.00..0.13): SUDDEN JUMPSCARE LUNGE!
            // Doors whip open 1.70m in front of camera; FovY snaps 44 -> 62 deg as SYSTEM ZERO bursts into the lens!
            float f = u / ShoveImpactU;
            float t = SmoothStep(f);
            float fovSnap = 1f - MathF.Pow(1f - f, 2.4f);
            pos = Vector3.Lerp(standAtDoor, impactOrigin, t);
            float doorBurstShake = (0.35f + 0.65f * f) * 0.022f;
            pos += new Vector3(MathF.Sin(u * 480f) * doorBurstShake, MathF.Cos(u * 530f) * doorBurstShake * 0.75f, 0f);
            tgt = b.Position + new Vector3(-0.10f * t, LerpF(1.70f, 1.92f, t), -0.32f * t);
            rollRad = 0.065f * t;
            fov = LerpF(44f, 62f, fovSnap);
        }
        else if (u < FloorLandU)
        {
            // BEAT 2B (u = 0.13..0.28): BRUTAL SHOVE IMPACT & 4.2M BACKWARD THROW!
            // Violent tumble roll (-26 deg -> +12 deg -> -9 deg) slamming hard onto the burgundy carpet (Y = 0.44m)!
            float f = (u - ShoveImpactU) / (FloorLandU - ShoveImpactU);
            float flight = 1f - MathF.Pow(1f - f, 2.2f);
            float arcY = MathF.Sin(f * MathF.PI) * 0.46f;
            pos = Vector3.Lerp(impactOrigin, floorImpact, flight) + new Vector3(0f, arcY, 0f);

            float skyWhip = MathF.Sin(f * MathF.PI) * 1.25f;
            tgt = b.Position + new Vector3(0.06f, 1.92f + skyWhip, 0f);

            if (f < 0.30f)
                rollRad = LerpF(+0.065f, -0.454f, SmoothStep(f / 0.30f)); // -> -26 deg
            else if (f < 0.68f)
                rollRad = LerpF(-0.454f, +0.209f, SmoothStep((f - 0.30f) / 0.38f)); // -> +12 deg
            else
                rollRad = LerpF(+0.209f, -0.157f, SmoothStep((f - 0.68f) / 0.32f)); // -> -9 deg impact

            fov = LerpF(62f, 52f, f);

            float hitShake = (1f - f) * 0.065f;
            pos += new Vector3(MathF.Sin(u * 410f) * hitShake, MathF.Cos(u * 460f) * hitShake, 0f);
        }
        else if (u < LookUpEndU)
        {
            // BEAT 3 (u = 0.28..0.46): DAZED LOOK-AROUND — VERONIKA VANISHED & DROPPED SHOTGUN -> SNAP UP AT TITAN!
            float landElapsed = u - FloorLandU;
            float settleY = LerpF(floorImpact.Y, floorRest.Y, SmoothStep(landElapsed / 0.06f));
            float bounce = MathF.Abs(MathF.Sin(landElapsed * 56f)) * MathF.Exp(-landElapsed * 30f) * 0.13f;
            float dizzyBreath = MathF.Sin(landElapsed * 24f) * 0.016f;
            pos = new Vector3(floorRest.X, settleY + bounce + dizzyBreath, floorRest.Z);

            Vector3 bossLofty = b.Position + new Vector3(0.04f, 1.98f, 0f);
            if (u < 0.325f)
            {
                // Whip head left-sideways toward empty desk / vanished Veronika flank
                float k = SmoothStep((u - FloorLandU) / (0.325f - FloorLandU));
                tgt = Vector3.Lerp(b.Position + new Vector3(0.06f, 1.88f, 0f), emptyDeskGlance, k);
                rollRad = LerpF(-0.157f, -0.215f, k);
                fov = LerpF(52f, 50f, k);
            }
            else if (u < GlanceShotgunU)
            {
                // Whip gaze down onto the 3D Sawed-Off Shotgun lying on the burgundy carpet at (0.62, 0.06, -12.95)
                float k = SmoothStep((u - 0.325f) / (GlanceShotgunU - 0.325f));
                tgt = Vector3.Lerp(emptyDeskGlance, DroppedShotgunPos, k);
                pos += new Vector3(-0.05f * k, -0.02f * k, +0.08f * k);
                rollRad = LerpF(-0.215f, -0.145f, k);
                fov = LerpF(50f, 44f, k);
            }
            else
            {
                // Snap head sharply UP at the towering 3-meter titan staring down with curious helmet tilt!
                float k = SmoothStep((u - GlanceShotgunU) / 0.055f);
                tgt = Vector3.Lerp(DroppedShotgunPos, bossLofty, k);
                pos += new Vector3(-0.05f * (1f - k), -0.02f * (1f - k), +0.08f * (1f - k));
                rollRad = LerpF(-0.145f, -0.140f, k) + MathF.Sin((u - GlanceShotgunU) * 32f) * 0.012f;
                fov = LerpF(44f, 46f, k);
            }
        }
        else if (u < GrabStartU)
        {
            // BEAT 4 (u = 0.46..0.78): FLOOR-LOCKED STOMPS + ROCKET SALVO SETTING OFFICE ON FIRE!
            // Camera widens (FovY 46 -> 55 deg, push-up to Y = 0.72m) & SHAKES VIOLENTLY at u = 0.68..0.76!
            float f = SmoothStep((u - LookUpEndU) / (GrabStartU - LookUpEndU));
            pos = Vector3.Lerp(floorRest, bracePos, f);
            tgt = b.Position + new Vector3(LerpF(0.04f, 0.12f, f), LerpF(2.02f, 1.94f, f), 0f);
            rollRad = LerpF(-0.140f, -0.045f, f);
            fov = LerpF(48f, 56f, f);

            if (u >= ExplosionStartU && u <= ExplosionEndU + 0.02f)
            {
                float expEnv = SmoothStep((u - ExplosionStartU) / 0.016f)
                    * (1f - SmoothStep((u - 0.735f) / 0.032f));
                float expAmp = expEnv * 0.072f;
                pos += new Vector3(
                    MathF.Sin(u * 520f) * expAmp,
                    MathF.Cos(u * 610f) * expAmp * 0.85f,
                    MathF.Sin(u * 430f) * expAmp * 0.45f);
                tgt += new Vector3(
                    MathF.Cos(u * 470f) * expAmp * 0.70f,
                    MathF.Sin(u * 560f) * expAmp * 0.70f,
                    0f);
                rollRad += MathF.Sin(u * 490f) * expEnv * 0.052f;
            }
        }
        else
        {
            // BEAT 5A (u = 0.78..0.85): DIP DOWN-LEFT TO SNATCH DROPPED 3D SHOTGUN OFF CARPET!
            float f = SmoothStep((u - GrabStartU) / (PullBackStartU - GrabStartU));
            float reach = MathF.Max(0f, MathF.Sin(f * MathF.PI)); // Clamp >=0 so Sin(PI) never yields NaN in Pow!
            Vector3 bossAim = b.Position + new Vector3(0.10f, 1.72f, 0f);
            pos = Vector3.Lerp(Vector3.Lerp(bracePos, surgeBasePos, f), grabDipPos, reach * 0.88f);
            tgt = Vector3.Lerp(bossAim, DroppedShotgunPos, MathF.Pow(reach, 0.72f));
            rollRad = LerpF(-0.045f, 0f, f) + 0.065f * reach;
            fov = LerpF(55f, 50f, f) - 3f * reach;
        }

        // Heavy titanium boot-stomp floor vibration synchronized to exact mechanical foot slams!
        if (walk.StompPulse > 0.01f)
        {
            float amp = walk.StompPulse * 0.036f;
            pos += new Vector3(MathF.Sin(u * 440f) * amp * 0.45f, -amp, 0f);
            tgt += new Vector3(0f, -amp * 0.55f, 0f);
            rollRad += walk.StompPulse * 0.018f * (walk.LeftSwing ? 1f : -1f);
        }

        Vector3 fwd = Vector3.Normalize(tgt - pos);
        Vector3 up = Vector3.Transform(Vector3.UnitY, Quaternion.CreateFromAxisAngle(fwd, rollRad));
        return new Camera3D
        {
            Position = pos,
            Target = tgt,
            Up = up,
            FovY = fov,
            Projection = CameraProjection.Perspective
        };
    }

    internal static void DrawOverlay(SkyscraperFloor1World world, Font uiFont, Font serifFont)
    {
        if (!IsActive(world)) return;

        int sw = UiViewport.Width, sh = UiViewport.Height;
        var b = world.ElevatorBoss;
        float barProgress = GetLetterboxProgress(world);
        int barH = (int)MathF.Round(BarHeight235 * barProgress);

        // 1. Beat-synchronized optical flashes & atmospheric vignettes (ZERO spoiler red glow during false calm!)
        if (b.State == ElevatorBossState.Repairing)
        {
            float r = RepairProgress(b);
            if (r >= 0.86f)
            {
                // Sudden creeping crimson horror sting ONLY when the twin visor eyes snap ON!
                float eyeReveal = SmoothStep((r - 0.86f) / 0.14f);
                int edgeAlpha = Math.Clamp((int)(78 * barProgress * eyeReveal), 0, 255);
                Color crimsonEdge = new(176, 16, 26, edgeAlpha);
                int vSpan = 96;
                Raylib.DrawRectangleGradientH(0, barH, vSpan, sh - barH * 2, crimsonEdge, Color.Blank);
                Raylib.DrawRectangleGradientH(sw - vSpan, barH, vSpan, sh - barH * 2, Color.Blank, crimsonEdge);
            }
        }
        else
        {
            float u = IntroProgress(b);

            // 1a. Blinding crimson-white shockwave flash right on the brutal shove impact (u = 0.13..0.24)
            if (u >= ShoveImpactU && u < ShoveImpactU + 0.11f)
            {
                float f = (u - ShoveImpactU) / 0.11f;
                int flashA = Math.Clamp((int)((1f - f) * (1f - f) * 185), 0, 255);
                Raylib.DrawRectangle(0, barH, sw, sh - barH * 2, new Color(255, 204, 190, flashA));
            }

            // 1b. Brief red concussion edge-vignette while dazed on the burgundy carpet (u = 0.13..0.48)
            if (u >= ShoveImpactU && u < LookUpEndU + 0.04f)
            {
                float daze = u < FloorLandU
                    ? SmoothStep((u - ShoveImpactU) / (FloorLandU - ShoveImpactU))
                    : 1f - SmoothStep((u - FloorLandU) / (LookUpEndU + 0.04f - FloorLandU));
                int dazeA = Math.Clamp((int)(96 * daze), 0, 255);
                Color dazeRed = new(142, 14, 22, dazeA);
                Raylib.DrawRectangleGradientH(0, barH, 124, sh - barH * 2, dazeRed, Color.Blank);
                Raylib.DrawRectangleGradientH(sw - 124, barH, 124, sh - barH * 2, Color.Blank, dazeRed);
            }

            // 1c. Warm blazing orange explosion flash as the 4 rockets detonate and ignite the atrium (u = 0.68..0.77)
            if (u >= ExplosionStartU && u < ExplosionEndU + 0.03f)
            {
                float p1 = MathF.Exp(-MathF.Pow((u - 0.696f) / 0.022f, 2f));
                float p2 = MathF.Exp(-MathF.Pow((u - 0.728f) / 0.024f, 2f)) * 0.85f;
                float expIntensity = MathF.Min(1f, p1 + p2);
                int expA = Math.Clamp((int)(136 * expIntensity), 0, 255);
                Raylib.DrawRectangle(0, barH, sw, sh - barH * 2, new Color(255, 148, 52, expA));
            }

            // 1d. Warm burning fire-glow edge wash once the atrium is ablaze (u = 0.69..0.92)
            if (u >= 0.69f && u < 0.94f)
            {
                float fireGlow = SmoothStep((u - 0.69f) / 0.04f) * (1f - SmoothStep((u - 0.84f) / 0.10f));
                int glowA = Math.Clamp((int)(62 * fireGlow), 0, 255);
                Color fireAmber = new(235, 92, 24, glowA);
                Raylib.DrawRectangleGradientH(0, barH, 108, sh - barH * 2, fireAmber, Color.Blank);
                Raylib.DrawRectangleGradientH(sw - 108, barH, 108, sh - barH * 2, Color.Blank, fireAmber);
            }
        }

        // 2. Pure matte-black 2.35:1 cinema letterbox bars ONLY (zero AI-slop cards, zero decorative rules)
        if (barH > 0)
        {
            Color cinemaBlack = new(8, 7, 7, 255);
            Raylib.DrawRectangle(0, 0, sw, barH, cinemaBlack);
            Raylib.DrawRectangle(0, sh - barH, sw, barH, cinemaBlack);
        }

        // 3. Razor-crisp movie subtitles centered cleanly INSIDE the bottom 2.35:1 black letterbox bar
        float subAlpha = 0f;
        string speaker = "";
        string line = "";
        if (b.State == ElevatorBossState.Repairing)
        {
            float r = RepairProgress(b);
            if (r >= 0.08f && r < 0.62f)
            {
                speaker = "ШЛЕЙФ · РАЦИЯ";
                line = "«Лифт пошёл... стоп, стоп, нагрузка зашкалила! Отойди от дверей, слышишь?! НЕ ОТКРЫВАЙ—»";
                float fadeIn = SmoothStep((r - 0.08f) / 0.06f);
                float radioCut = r > 0.54f
                    ? (1f - SmoothStep((r - 0.54f) / 0.08f)) * (0.52f + 0.48f * MathF.Sin(r * 260f))
                    : 1f;
                subAlpha = fadeIn * Math.Clamp(radioCut, 0f, 1f) * barProgress;
            }
        }
        else
        {
            float u = IntroProgress(b);
            speaker = "ИИ-АГЕНТ v0.1";
            if (u < 0.28f)
            {
                // Silent jumpscare lunge & brutal shove throw — let the physical shock hit pure!
                line = "";
                subAlpha = 0f;
            }
            else if (u < 0.41f)
            {
                line = "«Опа... И кого это к нам занесло без пропуска?»";
                subAlpha = SmoothStep((u - 0.28f) / 0.03f) * barProgress;
            }
            else if (u < 0.56f)
            {
                line = "«Постой-ка... Знакомая походка. А-а-а... Так это ты вернулся.»";
                subAlpha = barProgress;
            }
            else if (u < 0.78f)
            {
                line = "«Не удивляйся бардаку — пока ты пропадал, мы тут провели цифровую трансформацию: вырезали кожаных, внедрили ИИ.»";
                subAlpha = barProgress;
            }
            else if (u < 0.92f)
            {
                line = "«Поднимай свою железку, старина! Проверим, что стоит твой ручной код.»";
                subAlpha = (1f - SmoothStep((u - 0.87f) / 0.05f)) * barProgress;
            }
        }

        if (subAlpha > 0.04f && barH >= 56 && line.Length > 0)
            DrawCinemaSubtitleInBar(speaker, line, sw, sh - barH, barH, subAlpha);

        // 4. Quiet skip hint tucked cleanly inside the bottom-right of the lower letterbox bar
        if (barProgress > 0.45f)
        {
            float hintAlpha = SmoothStep((barProgress - 0.45f) / 0.40f);
            string skipHint = "[ПРОБЕЛ] Пропустить";
            Font hintFont = GameFonts.Get(Typeface.Interface, 13);
            float hw = GameFonts.Measure(hintFont, skipHint, 13).X;
            Color hintInk = new(196, 176, 142, Math.Clamp((int)(178 * hintAlpha), 0, 255));
            GameFonts.Draw(hintFont, skipHint, new(sw - 38f - hw, sh - barH * 0.5f - 7f), 13, hintInk, shadowed: false);
        }
    }

    private static void DrawCinemaSubtitleInBar(string speaker, string line, int sw, int barTop, int barH, float alpha)
    {
        Font speakerFont = GameFonts.Get(Typeface.InterfaceBold, 13);
        Font lineFont = GameFonts.Get(Typeface.InterfaceBold, 19);

        string resolvedLine = Loc.Tr(line);
        List<string> wrapped = WrapTranslated(lineFont, resolvedLine, 19, Math.Min(sw - 420f, 840f));

        float lineHeight = wrapped.Count > 1 ? 22f : 24f;
        float totalH = 18f + wrapped.Count * lineHeight;
        float startY = barTop + (barH - totalH) * 0.5f;

        Color speakerInk = new(224, 186, 114, (int)(242 * alpha));
        Color lineInk = new(250, 246, 238, (int)(255 * alpha));

        float spW = GameFonts.Measure(speakerFont, speaker, 13).X;
        GameFonts.Draw(speakerFont, speaker, new(sw * 0.5f - spW * 0.5f, startY), 13, speakerInk, shadowed: false);

        for (int i = 0; i < wrapped.Count; i++)
        {
            string row = wrapped[i];
            float rw = GameFonts.Measure(lineFont, row, 19).X;
            float ry = startY + 18f + i * lineHeight;
            GameFonts.Draw(lineFont, row, new(sw * 0.5f - rw * 0.5f, ry), 19, lineInk, shadowed: false);
        }
    }

    private static List<string> WrapTranslated(Font font, string text, int size, float maxWidth)
    {
        var lines = new List<string>(2);
        string current = "";
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = current.Length == 0 ? word : current + " " + word;
            if (current.Length > 0 && GameFonts.Measure(font, candidate, size).X > maxWidth)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }
        if (current.Length > 0) lines.Add(current);
        return lines;
    }

    private static float RepairElapsed(EscapedAgentBoss b)
        => Math.Clamp(SkyscraperFloor1World.RepairSeconds - b.StateTimer, 0f, SkyscraperFloor1World.RepairSeconds);

    private static float RepairProgress(EscapedAgentBoss b)
        => RepairElapsed(b) / SkyscraperFloor1World.RepairSeconds;

    private static float IntroElapsed(EscapedAgentBoss b)
        => Math.Clamp(SkyscraperFloor1World.IntroSeconds - b.StateTimer, 0f, SkyscraperFloor1World.IntroSeconds);

    private static float IntroProgress(EscapedAgentBoss b)
        => IntroElapsed(b) / SkyscraperFloor1World.IntroSeconds;

    private static float SmoothStep(float x)
    {
        float t = Math.Clamp(x, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float LerpF(float a, float b, float t) => a + (b - a) * t;
}
