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
    private const float GlanceShotgunU = 0.380f; // Hero glances at cracked partition & spots dropped shotgun on LEFT runner
    private const float LookUpEndU = 0.435f;     // Head snaps sharply UP at towering titan looming beyond the partition!
    private const float SalvoStartU = 0.585f;    // TOKEN-GUN 98 launches 3D wall-breach + hero kill-shot rocket salvo!
    private const float DodgeRollStartU = 0.652f;// Hero watches wall-breach missiles close in & dives into 360-deg dodge roll LEFT!
    private const float ExplosionStartU = 0.671f;// Missile 1 SMASHES Z=-15.8 wall open -> Missile 0 blasts hero's spot!
    private const float ExplosionEndU = 0.758f;
    private const float DodgeRollEndU = 0.760f;  // Hero finishes roll right beside dropped 3D shotgun on LEFT North Wing runner!
    private const float GrabStartU = 0.760f;     // Quick lean onto dropped 3D shotgun right in front of hero
    private const float GrabClackU = 0.822f;     // Crisp pickup clack right before dropped gun vanishes (0.828)
    private const float PullBackStartU = 0.850f; // Hero surges upright straight into wide 11-13m Boss Arena combat!

    private const float BarHeight235 = 88f; // Pure matte-black 2.35:1 cinema aperture inside 1280x720 virtual UI space
    private static readonly Vector3 DroppedShotgunPos = new(-1.72f, 0.068f, -20.35f);

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
        if (u >= 0.240f && (_lastAudioCueMask & 1024) == 0)
        {
            _lastAudioCueMask |= 1024;
            AudioMusicSystem.PlayComicBoom();
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
        if (u >= ExplosionStartU && (_lastAudioCueMask & 64) == 0)
        {
            _lastAudioCueMask |= 64;
            AudioMusicSystem.PlayComicBoom();
        }
        if (u >= 0.684f && (_lastAudioCueMask & 128) == 0)
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
        => world.NorthElevatorPos + new Vector3(-1.68f, 1.72f, -12.75f);

    /// <summary>
    /// Exact biomechanical foot-locked walkout shared by <see cref="SkyscraperFloor1World"/> (root Z/Y)
    /// and <see cref="VisualEngine3D"/> (stance/swing leg angles) so planted boots NEVER skate!
    /// Step 0 lunges onto threshold (0.00..0.068) to physically grab the hero by the collar & lift/hurl them!
    /// Steps 1..3 execute 3 heavy 0.875m Terminator stomps out onto the compass medallion.
    /// </summary>
    internal static IntroWalkSample EvalIntroWalk(float u)
    {
        u = Math.Clamp(u, 0f, 1f);

        ReadOnlySpan<(float Start, float End, bool LeftSwing, float Dist)> steps =
        [
            (0.00f, 0.068f, false, 2.05f), // Step 0: Explosive surge completely past Z=-8.5..-9.15 portal arch (to Z=-9.20) for zero-clip collar grab!
            (0.28f, 0.39f,  true,  0.88f), // Step 1: Heavy stomp onto carpet (Z=-10.08, 100% clear of facade & jambs!)
            (0.54f, 0.65f,  false, 0.88f), // Step 2: Towering stomp onto compass medallion (Z=-10.96)
            (0.67f, 0.78f,  true,  0.84f)  // Step 3: Final plant in open North Arena (Z=-11.80) -> DEAD STILL STANCE
        ];

        const float startZ = 1.35f;

        float progressSteps = 0f;
        float traveledZ = 0f;
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
                traveledZ += s.Dist;
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
                traveledZ += s.Dist * eased;
                isMoving = true;
                leftSwing = s.LeftSwing;
                stepFrac = f;
                if (f > 0.86f)
                    stompPulse = MathF.Max(stompPulse, SmoothStep((f - 0.86f) / 0.14f));
                break;
            }
        }

        float offsetZ = startZ - traveledZ;
        // Drop cleanly from cabin floor (Y=0.16m) to marble lobby floor (Y=0.00m) strictly at doorway threshold!
        float stepDown = SmoothStep((0.35f - offsetZ) / 0.55f);
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

        // Stay low under lintel until completely past portal arch (offsetZ <= -0.55f), then uncoil cleanly to full towering stature!
        float clearPortal = SmoothStep((-0.45f - offsetZ) / 0.35f);
        float liftUncoil = SmoothStep((u - 0.07f) / 0.08f) * 0.48f * clearPortal;
        float walkUncoil = SmoothStep((u - 0.28f) / 0.18f) * clearPortal;
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
                // BEAT 1B (r = 0.52..1.00): Player steps up cleanly before the wider tall glass doors
                // expecting to board — glass clears until r = 0.88..1.00 when twin crimson visor eyes snap ON!
                float t = SmoothStep((r - CalmPhaseEndR) / (1f - CalmPhaseEndR));
                Vector3 pos = Vector3.Lerp(new(0.04f, 1.66f, -10.55f), new(-0.08f, 1.66f, -10.05f), t);
                Vector3 tgt = Vector3.Lerp(new(0.00f, 1.72f, -7.80f), new(0.00f, 1.64f, -7.25f), t);

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
                    FovY = LerpF(49f, 47f, t),
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
        Vector3 standAtDoor  = north + new Vector3(-0.08f, 1.64f, -1.98f); // Cleanly framed outside open doorway facing Boss (zero door/chest clipping!)
        Vector3 liftedInAir  = north + new Vector3(-0.10f, 2.08f, -1.80f); // Hoisted off floor face-to-face with sleek crimson visor!
        Vector3 windupPull   = north + new Vector3(-0.42f, 2.16f, -1.68f); // Cocked sideways before brutal hurl!
        Vector3 floorImpact  = north + new Vector3(+1.48f, 0.48f, -10.25f);// Violent 9.1m hurl PAST Z=-15.8 glass partition into North Wing!
        Vector3 floorRest    = north + new Vector3(+1.65f, 0.94f, -11.00f);// (+1.65, 0.94, -19.50) pushed up on forearms/knee with crisp view over wainscot!
        Vector3 braceRight   = north + new Vector3(+1.65f, 1.16f, -10.90f);// (+1.65, 1.16, -19.40) crouching up right as rockets blow Z=-15.8 wall open!
        Vector3 dodgeEndPos  = north + new Vector3(-1.38f, 0.50f, -11.78f);// (-1.38, 0.50, -20.28) landed cleanly on burgundy runner beside shotgun!
        Vector3 grabDipPos   = north + new Vector3(-1.54f, 0.46f, -11.65f);// Lean straight down onto dropped 3D shotgun (-1.72, 0.068, -20.35)
        Vector3 surgeBasePos = north + new Vector3(-1.68f, 0.68f, -12.75f);// Surge upright into commanding 11-13m Boss Arena combat!
        Vector3 wallGlance   = new(2.45f, 1.55f, -15.80f);

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
            // BEAT 2D (u = 0.195..0.270): EXPLOSIVE 9.1-METER HIGH-VELOCITY BALLISTIC THROW STRAIGHT THROUGH THE Z=-15.8 GLASS PARTITION!
            // High arc clears the 1.18m slate wainscot cleanly & shatters the upper glass pane at u=0.240!
            float f = (u - ShoveImpactU) / (FloorLandU - ShoveImpactU);
            float flight = 1f - MathF.Pow(1f - f, 1.85f); // Explosive whip launch!
            float arcY = MathF.Sin(f * MathF.PI) * 0.88f;
            pos = Vector3.Lerp(windupPull, floorImpact, flight) + new Vector3(0f, arcY, 0f);

            // Violent sky-whip pitch as hero spins backward through the shattering glass!
            float skyWhip = MathF.Sin(f * MathF.PI) * 1.85f;
            tgt = b.Position + new Vector3(0.18f * f, 2.10f + skyWhip, 0f);

            if (f < 0.38f)
                rollRad = LerpF(+0.26f, -0.768f, SmoothStep(f / 0.38f));  // Whip to -44 deg!
            else if (f < 0.74f)
                rollRad = LerpF(-0.768f, +0.384f, SmoothStep((f - 0.38f) / 0.36f)); // Snap to +22 deg!
            else
                rollRad = LerpF(+0.384f, -0.244f, SmoothStep((f - 0.74f) / 0.26f)); // Slam to -14 deg!

            // Dramatic high-speed FOV punch peaking mid-flight!
            fov = LerpF(52f, 56f, f) + MathF.Sin(f * MathF.PI) * 26f; // Peaks at 80 deg!

            float launchShake = (1f - f) * 0.085f;
            // Extra glass-shatter jolt right as hero smashes through Z=-15.8 (u ~ 0.240)
            float glassJolt = MathF.Exp(-MathF.Pow((u - 0.242f) / 0.012f, 2f)) * 0.072f;
            float totalShake = launchShake + glassJolt;
            pos += new Vector3(MathF.Sin(u * 520f) * totalShake, MathF.Cos(u * 580f) * totalShake, 0f);
        }
        else if (u < SkidEndU)
        {
            // BEAT 2E (u = 0.270..0.305): CRUSHING FLOOR IMPACT BOUNCE + CARPET SKID DEEP IN THE NORTH WING!
            float f = (u - FloorLandU) / (SkidEndU - FloorLandU);
            float skid = 1f - MathF.Pow(1f - f, 2.4f);
            float bounceY = MathF.Abs(MathF.Sin(f * MathF.PI * 1.8f)) * (1f - f) * 0.22f;
            pos = Vector3.Lerp(floorImpact, floorRest, skid) + new Vector3(0f, bounceY, 0f);
            float impactQuake = (1f - f) * 0.095f;
            pos += new Vector3(MathF.Sin(u * 680f) * impactQuake, MathF.Cos(u * 740f) * impactQuake, 0f);
            tgt = b.Position + new Vector3(0.08f, LerpF(2.15f, 1.92f, skid), 0f);
            rollRad = LerpF(-0.244f, -0.175f, skid) + MathF.Sin(f * MathF.PI * 3f) * (1f - f) * 0.08f;
            fov = LerpF(56f, 52f, skid);
        }
        else if (u < LookUpEndU)
        {
            // BEAT 3 (u = 0.305..0.46): DAZED LOOK-AROUND — CRACKED GLASS PARTITION & DROPPED SHOTGUN -> SNAP UP AT TITAN!
            float landElapsed = u - SkidEndU;
            float dizzyBreath = MathF.Sin(landElapsed * 24f) * 0.016f;
            pos = new Vector3(floorRest.X, floorRest.Y + dizzyBreath, floorRest.Z);

            Vector3 bossLofty = b.Position + new Vector3(0.04f, 1.98f, 0f);
            if (u < 0.345f)
            {
                float k = SmoothStep((u - SkidEndU) / (0.345f - SkidEndU));
                tgt = Vector3.Lerp(b.Position + new Vector3(0.06f, 1.92f, 0f), wallGlance, k);
                rollRad = LerpF(-0.175f, -0.220f, k);
                fov = LerpF(52f, 50f, k);
            }
            else if (u < GlanceShotgunU)
            {
                float k = SmoothStep((u - 0.345f) / (GlanceShotgunU - 0.345f));
                tgt = Vector3.Lerp(wallGlance, DroppedShotgunPos, k);
                pos += new Vector3(-0.05f * k, -0.02f * k, +0.08f * k);
                rollRad = LerpF(-0.220f, -0.145f, k);
                fov = LerpF(50f, 45f, k);
            }
            else
            {
                float k = SmoothStep((u - GlanceShotgunU) / (LookUpEndU - GlanceShotgunU));
                tgt = Vector3.Lerp(DroppedShotgunPos, bossLofty, k);
                pos += new Vector3(-0.05f * (1f - k), -0.02f * (1f - k), +0.08f * (1f - k));
                rollRad = LerpF(-0.145f, -0.115f, k) + MathF.Sin((u - GlanceShotgunU) * 32f) * 0.012f;
                fov = LerpF(45f, 49f, k);
            }
        }
        else if (u < DodgeRollStartU)
        {
            // BEAT 4A (u = 0.460..0.652): BOSS STOMPS FORWARD, AIMS TOKEN-GUN 98 & UNLEASHES 4-ROCKET WALL-BREACH SALVO!
            float f = SmoothStep((u - LookUpEndU) / (DodgeRollStartU - LookUpEndU));
            pos = Vector3.Lerp(floorRest, braceRight, f);
            tgt = b.Position + new Vector3(LerpF(0.02f, -0.04f, f), LerpF(1.98f, 1.82f, f), 0f);
            rollRad = LerpF(-0.130f, -0.065f, f);
            fov = LerpF(49f, 58f, f);
        }
        else if (u < DodgeRollEndU)
        {
            // BEAT 4B (u = 0.652..0.760): WALL EXPLODES WIDE OPEN IN FRONT OF CAMERA -> HERO VIOLENTLY BARREL-ROLLS LEFT OUT OF BLAST!
            // Watches Missile 1 & 2 obliterate the Z=-15.8 wall into rubble, then dives LEFT beside dropped 3D shotgun!
            float f = (u - DodgeRollStartU) / (DodgeRollEndU - DodgeRollStartU);
            float s = SmoothStep(f);
            float diveArc = MathF.Sin(f * MathF.PI);
            pos = Vector3.Lerp(braceRight, dodgeEndPos, s) + new Vector3(0f, -0.18f * diveArc, 0.24f * diveArc);

            // Visceral 360-degree shoulder-roll camera rotation kicking off right as the wall explodes!
            float rollProg = SmoothStep(Math.Clamp((f - 0.12f) / 0.84f, 0f, 1f));
            rollRad = -0.065f - MathF.Tau * rollProg;

            // Keep eyes locked across the exploding Z=-15.8 partition & Boss during first half, then whip straight onto dropped 3D shotgun!
            Vector3 breachFocus = Vector3.Lerp(new Vector3(0.0f, 1.52f, -15.80f), b.Position + new Vector3(-0.04f, 1.82f, 0f), 0.48f);
            Vector3 midSweep = Vector3.Lerp(breachFocus, DroppedShotgunPos, 0.50f) + new Vector3(-0.75f, -0.22f, 0f);
            if (f < 0.48f)
                tgt = Vector3.Lerp(breachFocus, midSweep, SmoothStep(f / 0.48f));
            else
                tgt = Vector3.Lerp(midSweep, DroppedShotgunPos, SmoothStep((f - 0.48f) / 0.52f));

            fov = LerpF(58f, 50f, s) + 12f * diveArc;

            // Heavy concussion camera quake right as Missile 1 & 2 blow the Z=-15.8 wall apart & Missile 0 craters hero's spot!
            if (u >= ExplosionStartU)
            {
                float expEnv = SmoothStep((u - ExplosionStartU) / 0.014f)
                    * (1f - SmoothStep((u - 0.728f) / 0.032f));
                float expAmp = expEnv * 0.078f;
                pos += new Vector3(
                    MathF.Sin(u * 520f) * expAmp,
                    MathF.Cos(u * 610f) * expAmp * 0.82f,
                    MathF.Sin(u * 430f) * expAmp * 0.45f);
            }
        }
        else
        {
            // BEAT 5A (u = 0.760..0.850): HERO RIGHT BESIDE DROPPED 3D SHOTGUN ON LEFT RUNNER -> SNATCHES IT & SNAPS UP AT WIDE BLASTED ARENA!
            float f = SmoothStep((u - GrabStartU) / (PullBackStartU - GrabStartU));
            Vector3 bossAim = b.Position + new Vector3(0.08f, 1.72f, 0f);
            if (f < 0.54f)
            {
                float k = SmoothStep(f / 0.54f);
                pos = Vector3.Lerp(dodgeEndPos, grabDipPos, k);
                tgt = DroppedShotgunPos + new Vector3(0f, 0.03f * (1f - k), 0f);
                rollRad = LerpF(-0.065f, +0.055f, k);
                fov = LerpF(50f, 46f, k);
            }
            else
            {
                float k = SmoothStep((f - 0.54f) / 0.46f);
                pos = Vector3.Lerp(grabDipPos, surgeBasePos, k);
                tgt = Vector3.Lerp(DroppedShotgunPos, bossAim, k);
                rollRad = LerpF(+0.055f, 0f, k);
                fov = LerpF(46f, 56f, k);
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

            // 1c. Brief crisp heat-glint on wall-breach & rocket detonation (NEVER muddy orange screen fog!)
            if (u >= ExplosionStartU && u < ExplosionEndU)
            {
                float p1 = MathF.Exp(-MathF.Pow((u - 0.675f) / 0.012f, 2f));
                float p2 = MathF.Exp(-MathF.Pow((u - 0.692f) / 0.014f, 2f)) * 0.72f;
                float expIntensity = MathF.Min(1f, p1 + p2);
                int expA = Math.Clamp((int)(64 * expIntensity), 0, 255);
                Raylib.DrawRectangle(0, barH, sw, sh - barH * 2, new Color(255, 198, 124, expA));
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
                line = "«Не удивляйся бардаку — пока ты пропадал, я закрыл совет директоров и переписал башню под себя.»";
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
