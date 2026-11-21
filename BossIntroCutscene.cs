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
    private const float EyeSnapR = 0.88f;        // Repairing 0.88..1.00: visor wakes & doors crack open
    private const float GrabConnectU = 0.065f;   // Giant armored left hand shoots out & grabs collar
    private const float ThrowWindupU = 0.165f;   // End of helpless mid-air lift -> wind-up anticipation
    private const float ShoveImpactU = 0.195f;   // EXPLOSIVE sidearm hurl across the entire atrium!
    private const float FloorLandU = 0.270f;     // Crushing slam onto RIGHT side of burgundy carpet + skid!
    private const float SkidEndU = 0.305f;       // End of floor bounce & carpet skid
    private const float GlanceShotgunU = 0.400f; // Hero glances at empty desk & spots dropped shotgun on LEFT carpet
    private const float LookUpEndU = 0.460f;     // Head snaps sharply UP at towering titan looming overhead
    private const float SalvoStartU = 0.585f;    // TOKEN-GUN 98 launches 3D missile straight at hero + flank rockets!
    private const float DodgeRollStartU = 0.648f;// Hero dives into a 360-deg sideways dodge roll LEFT across carpet!
    private const float ExplosionStartU = 0.678f;// Missile 0 blasts the exact carpet spot hero just dodged out of!
    private const float ExplosionEndU = 0.755f;
    private const float DodgeRollEndU = 0.758f;  // Hero finishes roll right beside dropped 3D shotgun on LEFT carpet!
    private const float GrabStartU = 0.758f;     // Quick lean onto dropped 3D shotgun right in front of hero
    private const float GrabClackU = 0.822f;     // Crisp pickup clack right before dropped gun vanishes (0.828)
    private const float PullBackStartU = 0.850f; // Hero surges upright straight into live 1st-person combat

    private const float BarHeight235 = 88f; // Pure matte-black 2.35:1 cinema aperture inside 1280x720 virtual UI space
    private static readonly Vector3 DroppedShotgunPos = new(-1.88f, 0.065f, -14.32f);

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
        => world.NorthElevatorPos + new Vector3(-1.48f, 1.72f, -5.92f);

    /// <summary>
    /// Exact biomechanical foot-locked walkout shared by <see cref="SkyscraperFloor1World"/> (root Z/Y)
    /// and <see cref="VisualEngine3D"/> (stance/swing leg angles) so planted boots NEVER skate!
    /// Step 0 lunges onto threshold (0.00..0.068) to physically grab the hero by the collar & lift/hurl them!
    /// Steps 1..3 execute 3 heavy 0.875m Terminator stomps out onto the compass medallion.
    /// </summary>
    internal static IntroWalkSample EvalIntroWalk(float u)
    {
        u = Math.Clamp(u, 0f, 1f);

        ReadOnlySpan<(float Start, float End, bool LeftSwing)> steps =
        [
            (0.00f, 0.068f, false), // Step 0: Explosive threshold lunge right into hero's face for physical collar grab!
            (0.28f, 0.39f,  true),  // Step 1: Heavy stomp onto carpet after hurling hero across atrium
            (0.54f, 0.65f,  false), // Step 2: Towering stomp forward
            (0.67f, 0.78f,  true)   // Step 3: Final plant onto compass medallion -> DEAD STILL STANCE
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
        float stepDown = SmoothStep((progressSteps - 0.15f) / 0.65f);
        float offsetY = 0.16f * (1f - stepDown);

        // ShovePulse encodes the violent release whip right at ThrowWindupU..0.27
        float shovePulse = 0f;
        if (u > ThrowWindupU && u < 0.28f)
        {
            float sf = (u - ThrowWindupU) / (0.28f - ThrowWindupU);
            shovePulse = sf < 0.30f
                ? SmoothStep(sf / 0.30f)
                : 1f - SmoothStep((sf - 0.30f) / 0.70f);
        }

        // Uncoil partially during the mid-air collar lift so boss towers eye-to-eye, then full 2.95m by Step 1..2!
        float liftUncoil = SmoothStep((u - 0.07f) / 0.08f) * 0.42f;
        float walkUncoil = SmoothStep((u - 0.28f) / 0.20f);
        float uncoil = MathF.Max(liftUncoil, walkUncoil);

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
                // BEAT 1B (r = 0.52..1.00): Player steps right up to the threshold (Z = -9.68, 1.18m from doors!)
                // expecting to board — glass clears until r = 0.88..1.00 when twin crimson visor eyes snap ON!
                float t = SmoothStep((r - CalmPhaseEndR) / (1f - CalmPhaseEndR));
                Vector3 pos = Vector3.Lerp(new(0.04f, 1.66f, -10.25f), new(-0.14f, 1.66f, -9.68f), t);
                Vector3 tgt = Vector3.Lerp(new(0.00f, 1.72f, -7.80f), new(-0.04f, 1.70f, -7.25f), t);

                float eyeSnap = SmoothStep((r - EyeSnapR) / 0.06f);
                float eyeTremor = eyeSnap * (1f - SmoothStep((r - 0.94f) / 0.06f)) * 0.015f;
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
        Vector3 standAtDoor  = north + new Vector3(-0.08f, 1.64f, -1.44f); // Cleanly framed right at open threshold (zero door/collar clipping!)
        Vector3 liftedInAir  = north + new Vector3(-0.10f, 2.08f, -1.28f); // Hoisted off floor face-to-face with sleek crimson visor!
        Vector3 windupPull   = north + new Vector3(-0.42f, 2.16f, -1.16f); // Cocked sideways before brutal hurl!
        Vector3 floorImpact  = north + new Vector3(1.18f,  0.38f, -5.45f); // Hard first slam onto RIGHT side of carpet!
        Vector3 floorRest    = north + new Vector3(1.32f,  0.48f, -6.10f); // (1.32, 0.48, -14.60) skid rest on RIGHT carpet
        Vector3 braceRight   = north + new Vector3(1.32f,  0.66f, -6.02f); // Braced on RIGHT carpet as Missile 0 streaks straight at hero!
        Vector3 dodgeEndPos  = north + new Vector3(-1.42f, 0.46f, -5.88f); // (-1.42, 0.46, -14.38) landed on LEFT carpet right beside dropped shotgun!
        Vector3 grabDipPos   = north + new Vector3(-1.62f, 0.44f, -5.66f); // Lean straight down onto dropped 3D shotgun
        Vector3 surgeBasePos = north + new Vector3(-1.48f, 0.58f, -5.92f); // Surge upright into live combat!
        Vector3 emptyDeskGlance = new(3.35f, 0.92f, -12.35f);

        Vector3 pos;
        Vector3 tgt;
        float rollRad;
        float fov;

        if (u < GrabConnectU)
        {
            // BEAT 2A (u = 0.00..0.068): JUMPSCARE HAND LUNGE STRAIGHT INTO THE LENS!
            // Giant armored left forearm shoots right into hero's face & clenches collar!
            float f = u / GrabConnectU;
            float t = SmoothStep(f);
            pos = standAtDoor + new Vector3(-0.04f * t, -0.04f * t, +0.08f * t);
            float grabJolt = (0.3f + 0.7f * f) * 0.026f;
            pos += new Vector3(MathF.Sin(u * 560f) * grabJolt, MathF.Cos(u * 610f) * grabJolt, 0f);
            // Stare straight at his lunging left hand & blazing crimson visor!
            tgt = b.Position + new Vector3(-0.08f, LerpF(1.74f, 1.88f, t), -0.35f);
            rollRad = -0.08f * t;
            fov = LerpF(44f, 56f, t);
        }
        else if (u < ThrowWindupU)
        {
            // BEAT 2B (u = 0.068..0.172): PHYSICALLY LIFTED OFF THE FLOOR IN HIS ARMORED FIST!
            // Camera rises to Y = 2.16m dangling helplessly face-to-face with his crimson visor inches away!
            float f = (u - GrabConnectU) / (ThrowWindupU - GrabConnectU);
            float hoist = SmoothStep(Math.Clamp(f / 0.42f, 0f, 1f));
            float dangleX = MathF.Sin(f * MathF.PI * 2.5f) * 0.045f * hoist;
            float dangleY = MathF.Sin(f * MathF.PI * 4.0f) * 0.028f * hoist;
            pos = Vector3.Lerp(standAtDoor, liftedInAir, hoist) + new Vector3(dangleX, dangleY, 0f);

            // Clench-impact tremor decaying over first 0.025u
            float clenchShake = MathF.Exp(-f * 9f) * 0.045f;
            pos += new Vector3(MathF.Sin(u * 640f) * clenchShake, MathF.Cos(u * 710f) * clenchShake, 0f);

            // Locked tight onto his glowing crimson visor & armored chin inches from your face!
            tgt = b.Position + new Vector3(-0.02f, LerpF(1.88f, 2.26f, hoist), -0.10f);
            rollRad = LerpF(-0.08f, +0.105f, hoist) + MathF.Sin(f * MathF.PI * 2.2f) * 0.055f;
            fov = LerpF(56f, 44f, hoist);
        }
        else if (u < ShoveImpactU)
        {
            // BEAT 2C (u = 0.172..0.196): BRUTAL WIND-UP ANTICIPATION BEFORE THE HURL!
            float f = SmoothStep((u - ThrowWindupU) / (ShoveImpactU - ThrowWindupU));
            pos = Vector3.Lerp(liftedInAir, windupPull, f);
            tgt = b.Position + new Vector3(0.06f * f, 2.24f, -0.05f);
            rollRad = LerpF(+0.105f, +0.26f, f); // +15 deg cocked sideways
            fov = LerpF(44f, 52f, f);
        }
        else if (u < FloorLandU)
        {
            // BEAT 2D (u = 0.196..0.276): EXPLOSIVE 5.3-METER HIGH-VELOCITY BALLISTIC THROW ACROSS ATRIUM!
            // FovY violently punches 52 -> 78 deg speed-warp, violent -44 deg tumble roll, crushing Y=0.36m floor slam!
            float f = (u - ShoveImpactU) / (FloorLandU - ShoveImpactU);
            float flight = 1f - MathF.Pow(1f - f, 1.85f); // Explosive whip launch!
            float arcY = MathF.Sin(f * MathF.PI) * 0.56f;
            pos = Vector3.Lerp(windupPull, floorImpact, flight) + new Vector3(0f, arcY, 0f);

            // Violent sky-whip pitch as hero spins backward through the air!
            float skyWhip = MathF.Sin(f * MathF.PI) * 1.85f;
            tgt = b.Position + new Vector3(0.18f * f, 2.10f + skyWhip, 0f);

            if (f < 0.38f)
                rollRad = LerpF(+0.26f, -0.768f, SmoothStep(f / 0.38f));  // Whip to -44 deg!
            else if (f < 0.74f)
                rollRad = LerpF(-0.768f, +0.384f, SmoothStep((f - 0.38f) / 0.36f)); // Snap to +22 deg!
            else
                rollRad = LerpF(+0.384f, -0.244f, SmoothStep((f - 0.74f) / 0.26f)); // Slam to -14 deg!

            // Dramatic high-speed FOV punch peaking mid-flight!
            fov = LerpF(52f, 54f, f) + MathF.Sin(f * MathF.PI) * 26f; // Peaks at 79 deg!

            float launchShake = (1f - f) * 0.085f;
            pos += new Vector3(MathF.Sin(u * 520f) * launchShake, MathF.Cos(u * 580f) * launchShake, 0f);
        }
        else if (u < SkidEndU)
        {
            // BEAT 2E (u = 0.276..0.305): CRUSHING FLOOR IMPACT BOUNCE + 0.75M CARPET SKID!
            float f = (u - FloorLandU) / (SkidEndU - FloorLandU);
            float skid = 1f - MathF.Pow(1f - f, 2.4f);
            float bounceY = MathF.Abs(MathF.Sin(f * MathF.PI * 1.8f)) * (1f - f) * 0.22f;
            pos = Vector3.Lerp(floorImpact, floorRest, skid) + new Vector3(0f, bounceY, 0f);
            float impactQuake = (1f - f) * 0.095f;
            pos += new Vector3(MathF.Sin(u * 680f) * impactQuake, MathF.Cos(u * 740f) * impactQuake, 0f);
            tgt = b.Position + new Vector3(0.08f, LerpF(2.15f, 1.88f, skid), 0f);
            rollRad = LerpF(-0.244f, -0.175f, skid) + MathF.Sin(f * MathF.PI * 3f) * (1f - f) * 0.08f;
            fov = LerpF(56f, 50f, skid);
        }
        else if (u < LookUpEndU)
        {
            // BEAT 3 (u = 0.305..0.46): DAZED LOOK-AROUND — VERONIKA VANISHED & DROPPED SHOTGUN -> SNAP UP AT TITAN!
            float landElapsed = u - SkidEndU;
            float dizzyBreath = MathF.Sin(landElapsed * 24f) * 0.016f;
            pos = new Vector3(floorRest.X, floorRest.Y + dizzyBreath, floorRest.Z);

            Vector3 bossLofty = b.Position + new Vector3(0.04f, 1.98f, 0f);
            if (u < 0.345f)
            {
                float k = SmoothStep((u - SkidEndU) / (0.345f - SkidEndU));
                tgt = Vector3.Lerp(b.Position + new Vector3(0.06f, 1.88f, 0f), emptyDeskGlance, k);
                rollRad = LerpF(-0.175f, -0.225f, k);
                fov = LerpF(50f, 49f, k);
            }
            else if (u < GlanceShotgunU)
            {
                float k = SmoothStep((u - 0.345f) / (GlanceShotgunU - 0.345f));
                tgt = Vector3.Lerp(emptyDeskGlance, DroppedShotgunPos, k);
                pos += new Vector3(-0.05f * k, -0.02f * k, +0.08f * k);
                rollRad = LerpF(-0.225f, -0.145f, k);
                fov = LerpF(49f, 44f, k);
            }
            else
            {
                float k = SmoothStep((u - GlanceShotgunU) / 0.055f);
                tgt = Vector3.Lerp(DroppedShotgunPos, bossLofty, k);
                pos += new Vector3(-0.05f * (1f - k), -0.02f * (1f - k), +0.08f * (1f - k));
                rollRad = LerpF(-0.145f, -0.140f, k) + MathF.Sin((u - GlanceShotgunU) * 32f) * 0.012f;
                fov = LerpF(44f, 46f, k);
            }
        }
        else if (u < DodgeRollStartU)
        {
            // BEAT 4A (u = 0.460..0.648): BOSS STOMPS FORWARD, AIMS TOKEN-GUN 98 DEAD AT HERO & FIRES MISSILE 0 STRAIGHT AT US!
            float f = SmoothStep((u - LookUpEndU) / (DodgeRollStartU - LookUpEndU));
            pos = Vector3.Lerp(floorRest, braceRight, f);
            tgt = b.Position + new Vector3(LerpF(0.02f, -0.06f, f), LerpF(1.98f, 1.88f, f), 0f);
            rollRad = LerpF(-0.140f, -0.075f, f);
            fov = LerpF(47f, 54f, f);
        }
        else if (u < DodgeRollEndU)
        {
            // BEAT 4B (u = 0.648..0.758): HERO VIOLENTLY BARREL-ROLLS LEFT ACROSS THE CARPET RIGHT BEFORE MISSILE 0 DETONATES!
            // Rolls straight out of the blast zone onto the LEFT side of the atrium — emerging inches from the dropped 3D shotgun!
            float f = (u - DodgeRollStartU) / (DodgeRollEndU - DodgeRollStartU);
            float s = SmoothStep(f);
            float diveArc = MathF.Sin(f * MathF.PI);
            pos = Vector3.Lerp(braceRight, dodgeEndPos, s) + new Vector3(0f, -0.15f * diveArc, 0.22f * diveArc);

            // Visceral 360-degree shoulder-roll camera rotation (leftward dive across carpet!)
            float rollProg = SmoothStep(Math.Clamp((f - 0.04f) / 0.88f, 0f, 1f));
            rollRad = -0.075f - MathF.Tau * rollProg;

            // Gaze whips from incoming rocket/boss -> across burning atrium -> locks straight onto dropped 3D shotgun!
            Vector3 bossAim = b.Position + new Vector3(-0.06f, 1.84f, 0f);
            Vector3 midSweep = Vector3.Lerp(bossAim, DroppedShotgunPos, 0.48f) + new Vector3(-0.6f, -0.25f, 0f);
            if (f < 0.45f)
                tgt = Vector3.Lerp(bossAim, midSweep, SmoothStep(f / 0.45f));
            else
                tgt = Vector3.Lerp(midSweep, DroppedShotgunPos, SmoothStep((f - 0.45f) / 0.55f));

            fov = LerpF(54f, 47f, s) + 12f * diveArc;

            // Shockwave camera quake right as Missile 0 blasts the exact spot hero just dodged out of!
            if (u >= ExplosionStartU)
            {
                float expEnv = SmoothStep((u - ExplosionStartU) / 0.014f)
                    * (1f - SmoothStep((u - 0.728f) / 0.032f));
                float expAmp = expEnv * 0.068f;
                pos += new Vector3(
                    MathF.Sin(u * 520f) * expAmp,
                    MathF.Cos(u * 610f) * expAmp * 0.80f,
                    MathF.Sin(u * 430f) * expAmp * 0.40f);
            }
        }
        else
        {
            // BEAT 5A (u = 0.758..0.850): HERO RIGHT BESIDE DROPPED 3D SHOTGUN ON LEFT CARPET -> SNATCHES IT & SNAPS UP AT BOSS!
            float f = SmoothStep((u - GrabStartU) / (PullBackStartU - GrabStartU));
            Vector3 bossAim = b.Position + new Vector3(0.08f, 1.76f, 0f);
            if (f < 0.58f)
            {
                float k = SmoothStep(f / 0.58f);
                pos = Vector3.Lerp(dodgeEndPos, grabDipPos, k);
                tgt = DroppedShotgunPos + new Vector3(0f, 0.03f * (1f - k), 0f);
                rollRad = LerpF(-0.075f, +0.055f, k);
                fov = LerpF(47f, 44f, k);
            }
            else
            {
                float k = SmoothStep((f - 0.58f) / 0.42f);
                pos = Vector3.Lerp(grabDipPos, surgeBasePos, k);
                tgt = Vector3.Lerp(DroppedShotgunPos, bossAim, k);
                rollRad = LerpF(+0.055f, 0f, k);
                fov = LerpF(44f, 50f, k);
            }
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

            // 1a. Collar-choke crimson edge pulse while hoisted helplessly in mid-air (u = 0.068..0.196)
            if (u >= GrabConnectU && u < ShoveImpactU)
            {
                float choke = SmoothStep((u - GrabConnectU) / 0.025f);
                float pulse = 0.72f + 0.28f * MathF.Sin(u * 140f);
                int chokeA = Math.Clamp((int)(110 * choke * pulse), 0, 255);
                Color chokeRed = new(148, 12, 20, chokeA);
                Raylib.DrawRectangleGradientH(0, barH, 136, sh - barH * 2, chokeRed, Color.Blank);
                Raylib.DrawRectangleGradientH(sw - 136, barH, 136, sh - barH * 2, Color.Blank, chokeRed);
            }

            // 1b. Blinding crimson-white shockwave flash right on the brutal hurl release (u = 0.196..0.28)
            if (u >= ShoveImpactU && u < ShoveImpactU + 0.085f)
            {
                float f = (u - ShoveImpactU) / 0.085f;
                int flashA = Math.Clamp((int)((1f - f) * (1f - f) * 185), 0, 255);
                Raylib.DrawRectangle(0, barH, sw, sh - barH * 2, new Color(255, 204, 190, flashA));
            }

            // 1c. Crushing dark-crimson concussion blackout blink right as hero slams onto the carpet (u = 0.274..0.315)
            if (u >= FloorLandU - 0.004f && u < SkidEndU + 0.015f)
            {
                float f = (u - (FloorLandU - 0.004f)) / (SkidEndU + 0.015f - (FloorLandU - 0.004f));
                float blink = MathF.Sin(Math.Clamp(f, 0f, 1f) * MathF.PI);
                int blkA = Math.Clamp((int)(192 * MathF.Pow(blink, 0.75f)), 0, 255);
                Raylib.DrawRectangle(0, barH, sw, sh - barH * 2, new Color(18, 6, 8, blkA));
            }

            // 1d. Brief red concussion edge-vignette while dazed on the burgundy carpet (u = 0.196..0.48)
            if (u >= ShoveImpactU && u < LookUpEndU + 0.04f)
            {
                float daze = u < FloorLandU
                    ? SmoothStep((u - ShoveImpactU) / (FloorLandU - ShoveImpactU))
                    : 1f - SmoothStep((u - FloorLandU) / (LookUpEndU + 0.04f - FloorLandU));
                int dazeA = Math.Clamp((int)(104 * daze), 0, 255);
                Color dazeRed = new(142, 14, 22, dazeA);
                Raylib.DrawRectangleGradientH(0, barH, 132, sh - barH * 2, dazeRed, Color.Blank);
                Raylib.DrawRectangleGradientH(sw - 132, barH, 132, sh - barH * 2, Color.Blank, dazeRed);
            }

            // 1c. Brief crisp heat-glint on rocket detonation (NEVER muddy orange screen fog!)
            if (u >= ExplosionStartU && u < ExplosionEndU)
            {
                float p1 = MathF.Exp(-MathF.Pow((u - 0.684f) / 0.012f, 2f));
                float p2 = MathF.Exp(-MathF.Pow((u - 0.716f) / 0.014f, 2f)) * 0.65f;
                float expIntensity = MathF.Min(1f, p1 + p2);
                int expA = Math.Clamp((int)(56 * expIntensity), 0, 255);
                Raylib.DrawRectangle(0, barH, sw, sh - barH * 2, new Color(255, 192, 118, expA));
            }

            // 1d. Subtle warm firelight edge glow once the atrium is ablaze (u = 0.68..0.92)
            if (u >= 0.68f && u < 0.94f)
            {
                float fireGlow = SmoothStep((u - 0.68f) / 0.04f) * (1f - SmoothStep((u - 0.84f) / 0.10f));
                int glowA = Math.Clamp((int)(38 * fireGlow), 0, 255);
                Color fireAmber = new(235, 92, 24, glowA);
                Raylib.DrawRectangleGradientH(0, barH, 84, sh - barH * 2, fireAmber, Color.Blank);
                Raylib.DrawRectangleGradientH(sw - 84, barH, 84, sh - barH * 2, Color.Blank, fireAmber);
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
            else if (u < 0.93f)
            {
                line = "«Поднимай свою железку, старина!»";
                subAlpha = (1f - SmoothStep((u - 0.88f) / 0.05f)) * barProgress;
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
