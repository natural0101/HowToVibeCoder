using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal static unsafe partial class VisualEngine3D
{
    private const float BossBasePelvisY = 1.135f;
    private const float BossRootScale = 1.32f;

    internal struct BossRigPose
    {
        public float PelvisY, PelvisZ, PelvisRoll;
        public float TorsoPitch, TorsoYaw, TorsoRoll;
        public float HeadPitch, HeadYaw, HeadRoll;
        public float ArmLPitch, ArmLYaw, ArmLRoll, ElbowLPitch, ElbowLYaw;
        public float ArmRPitch, ArmRYaw, ArmRRoll, ElbowRPitch, ElbowRYaw;
        public float ThighLPitch, ThighLRoll, ShinLPitch;
        public float ThighRPitch, ThighRRoll, ShinRPitch;
    }

    private static BossRigPose bossSmoothedPose;
    private static bool bossHasSmoothedPose;
    private static float bossLastUpdateClock = -999f;

    internal static void ResetBossPose()
    {
        bossHasSmoothedPose = false;
        bossLastUpdateClock = -999f;
    }

    private static float LerpF(float a, float b, float t) => a + (b - a) * t;

    private static BossRigPose LerpBossPose(in BossRigPose a, in BossRigPose b, float t) => new()
    {
        PelvisY     = LerpF(a.PelvisY, b.PelvisY, t),
        PelvisZ     = LerpF(a.PelvisZ, b.PelvisZ, t),
        PelvisRoll  = LerpF(a.PelvisRoll, b.PelvisRoll, t),
        TorsoPitch  = LerpF(a.TorsoPitch, b.TorsoPitch, t),
        TorsoYaw    = LerpF(a.TorsoYaw, b.TorsoYaw, t),
        TorsoRoll   = LerpF(a.TorsoRoll, b.TorsoRoll, t),
        HeadPitch   = LerpF(a.HeadPitch, b.HeadPitch, t),
        HeadYaw     = LerpF(a.HeadYaw, b.HeadYaw, t),
        HeadRoll    = LerpF(a.HeadRoll, b.HeadRoll, t),
        ArmLPitch   = LerpF(a.ArmLPitch, b.ArmLPitch, t),
        ArmLYaw     = LerpF(a.ArmLYaw, b.ArmLYaw, t),
        ArmLRoll    = LerpF(a.ArmLRoll, b.ArmLRoll, t),
        ElbowLPitch = LerpF(a.ElbowLPitch, b.ElbowLPitch, t),
        ElbowLYaw   = LerpF(a.ElbowLYaw, b.ElbowLYaw, t),
        ArmRPitch   = LerpF(a.ArmRPitch, b.ArmRPitch, t),
        ArmRYaw     = LerpF(a.ArmRYaw, b.ArmRYaw, t),
        ArmRRoll    = LerpF(a.ArmRRoll, b.ArmRRoll, t),
        ElbowRPitch = LerpF(a.ElbowRPitch, b.ElbowRPitch, t),
        ElbowRYaw   = LerpF(a.ElbowRYaw, b.ElbowRYaw, t),
        ThighLPitch = LerpF(a.ThighLPitch, b.ThighLPitch, t),
        ThighLRoll  = LerpF(a.ThighLRoll, b.ThighLRoll, t),
        ShinLPitch  = LerpF(a.ShinLPitch, b.ShinLPitch, t),
        ThighRPitch = LerpF(a.ThighRPitch, b.ThighRPitch, t),
        ThighRRoll  = LerpF(a.ThighRRoll, b.ThighRRoll, t),
        ShinRPitch  = LerpF(a.ShinRPitch, b.ShinRPitch, t),
    };

    private static float WrapAngle(float a)
    {
        while (a > MathF.PI) a -= MathF.Tau;
        while (a < -MathF.PI) a += MathF.Tau;
        return a;
    }

    private static float SmoothStep(float x)
    {
        float t = Math.Clamp(x, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float ComputeBossGroundedPelvisY(in BossRigPose p, bool allowKneeRest)
    {
        Matrix4x4 pelvis = Matrix4x4.CreateRotationZ(p.PelvisRoll)
            * Matrix4x4.CreateTranslation(0f, p.PelvisY, p.PelvisZ);
        Matrix4x4 thighL = Matrix4x4.CreateFromYawPitchRoll(0f, p.ThighLPitch, p.ThighLRoll)
            * Matrix4x4.CreateTranslation(+0.135f, -0.025f, 0f) * pelvis;
        Matrix4x4 shinL = Matrix4x4.CreateFromYawPitchRoll(0f, p.ShinLPitch, -p.ThighLRoll * 0.45f)
            * Matrix4x4.CreateTranslation(0f, -0.525f, 0f) * thighL;
        Matrix4x4 thighR = Matrix4x4.CreateFromYawPitchRoll(0f, p.ThighRPitch, p.ThighRRoll)
            * Matrix4x4.CreateTranslation(-0.135f, -0.025f, 0f) * pelvis;
        Matrix4x4 shinR = Matrix4x4.CreateFromYawPitchRoll(0f, p.ShinRPitch, -p.ThighRRoll * 0.45f)
            * Matrix4x4.CreateTranslation(0f, -0.525f, 0f) * thighR;

        float heelL = Vector3.Transform(new Vector3(0f, -0.585f, -0.075f), shinL).Y;
        float toeL  = Vector3.Transform(new Vector3(0f, -0.585f, +0.135f), shinL).Y;
        float heelR = Vector3.Transform(new Vector3(0f, -0.585f, -0.075f), shinR).Y;
        float toeR  = Vector3.Transform(new Vector3(0f, -0.585f, +0.135f), shinR).Y;
        float lowest = MathF.Min(MathF.Min(heelL, toeL), MathF.Min(heelR, toeR));
        if (allowKneeRest)
        {
            float kneeL = Vector3.Transform(new Vector3(0f, -0.075f, +0.065f), shinL).Y;
            float kneeR = Vector3.Transform(new Vector3(0f, -0.075f, +0.065f), shinR).Y;
            lowest = MathF.Min(lowest, MathF.Min(kneeL, kneeR));
        }
        return p.PelvisY - lowest;
    }

    private static (float HeadYaw, float HeadPitch) ComputeBossHeadTrack(EscapedAgentBoss b, Vector3 playerEye, float torsoYaw, float torsoPitch)
    {
        Vector3 headWorld = b.Position + Vector3.UnitY * ((BossBasePelvisY + 0.745f) * BossRootScale);
        Vector3 toPlayer = playerEye - headWorld;
        float horiz = MathF.Sqrt(toPlayer.X * toPlayer.X + toPlayer.Z * toPlayer.Z);
        if (horiz < 0.05f) return (0f, 0f);
        float worldYaw = MathF.Atan2(toPlayer.X, toPlayer.Z);
        float relYaw = WrapAngle(worldYaw - b.FacingYaw) - torsoYaw;
        float pitchToEye = -MathF.Atan2(toPlayer.Y, horiz) - torsoPitch;
        return (Math.Clamp(relYaw, -0.78f, 0.78f), Math.Clamp(pitchToEye, -0.34f, 0.42f));
    }

    internal static BossRigPose ComputeBossTargetPose(
        EscapedAgentBoss b, Vector3 playerEye, float clock, float glassOpacity, float shutdown)
    {
        float breath = MathF.Sin(clock * 2.4f);
        var p = new BossRigPose
        {
            PelvisY = BossBasePelvisY,
            ArmLRoll = +0.16f,
            ArmRRoll = -0.16f,
            ElbowLPitch = -0.38f,
            ElbowRPitch = -0.38f,
            ThighLRoll = +0.07f,
            ThighRRoll = -0.07f,
            ShinLPitch = +0.08f,
            ShinRPitch = +0.08f,
        };

        if (b.IsDefeated)
        {
            // Dramatic collapse onto bent knees, torso slumped forward, head dropped, arms limp.
            float collapse = Math.Clamp(shutdown <= 0f ? 1f : shutdown, 0f, 1f);
            float twitch = (1f - collapse) * 0.14f * MathF.Sin(clock * 24f);
            p.PelvisY     = LerpF(BossBasePelvisY - 0.16f, 0.56f, collapse);
            p.PelvisZ     = LerpF(0f, -0.06f, collapse);
            p.PelvisRoll  = LerpF(0f, 0.06f, collapse);
            p.TorsoPitch  = LerpF(0.28f, 0.52f, collapse);
            p.TorsoYaw    = LerpF(0f, -0.14f, collapse);
            p.TorsoRoll   = LerpF(0f, 0.09f, collapse);
            p.HeadPitch   = LerpF(0.25f, 0.48f, collapse) + twitch;
            p.HeadYaw     = LerpF(0f, 0.18f, collapse);
            p.HeadRoll    = LerpF(0f, -0.16f, collapse);
            p.ThighLPitch = LerpF(-0.26f, -0.18f, collapse);
            p.ThighLRoll  = +0.16f;
            p.ShinLPitch  = LerpF(0.55f, 1.50f, collapse);
            p.ThighRPitch = LerpF(-0.22f, -0.12f, collapse);
            p.ThighRRoll  = -0.16f;
            p.ShinRPitch  = LerpF(0.50f, 1.46f, collapse);
            p.ArmLPitch   = LerpF(-0.34f, -0.44f, collapse);
            p.ArmLRoll    = +0.20f;
            p.ElbowLPitch = LerpF(-0.55f, -0.34f, collapse);
            p.ArmRPitch   = LerpF(-0.30f, -0.38f, collapse);
            p.ArmRRoll    = -0.20f;
            p.ElbowRPitch = LerpF(-0.50f, -0.30f, collapse);
            p.PelvisY     = ComputeBossGroundedPelvisY(p, allowKneeRest: true);
            return p;
        }

        switch (b.State)
        {
            case ElevatorBossState.Repairing:
            {
                // Predatory coiled sentinel crouch inside clearing smoked-glass shaft:
                // deeply bent knees + hunched torso keep head & glowing crimson visor 100% clear below 2.30m door lintel!
                float wake = SmoothStep(1f - glassOpacity);
                float servo = 0.020f * MathF.Sin(clock * 6.5f) * wake;
                p.TorsoPitch  = LerpF(0.86f, 0.74f, wake);
                p.TorsoYaw    = LerpF(0.02f, 0.08f, wake) + 0.025f * MathF.Sin(clock * 1.4f) * wake;
                // Lift chin aggressively as glass clears so glowing crimson visor locks straight through the frosted glass!
                p.HeadPitch   = LerpF(0.18f, -0.58f, wake) + servo;
                p.HeadYaw     = 0.07f * MathF.Sin(clock * 1.8f) * (1f - wake * 0.7f);
                p.ThighLPitch = LerpF(-1.24f, -1.16f, wake);
                p.ThighLRoll  = +0.16f;
                p.ShinLPitch  = LerpF(+2.02f, +1.92f, wake);
                p.ThighRPitch = LerpF(-1.14f, -1.04f, wake);
                p.ThighRRoll  = -0.16f;
                p.ShinRPitch  = LerpF(+1.88f, +1.76f, wake);
                p.ArmLPitch   = LerpF(-0.36f, -0.52f, wake);
                p.ArmLRoll    = LerpF(+0.20f, +0.28f, wake);
                p.ElbowLPitch = LerpF(-0.56f, -0.82f, wake);
                p.ElbowLYaw   = -0.16f;
                // Right forearm TOKEN-GUN 98 primed low-ready clear of hip & glass
                p.ArmRPitch   = LerpF(-0.36f, -0.74f, wake);
                p.ArmRYaw     = LerpF(+0.08f, +0.24f, wake);
                p.ArmRRoll    = LerpF(-0.22f, -0.34f, wake);
                p.ElbowRPitch = LerpF(-0.58f, -0.82f, wake);
                p.ElbowRYaw   = LerpF(+0.10f, +0.20f, wake);
                p.PelvisY     = ComputeBossGroundedPelvisY(p, allowKneeRest: false);
                break;
            }

            case ElevatorBossState.IntroSpeech:
            {
                // Phase A (introProgress <= 0.44): predatory ducked stalk under 2.30m elevator lintel so head & visor stay 100% clear,
                // with a charismatic curious helmet tilt (+0.18 rad during 0.20..0.44) as he recognizes the player!
                // Phase B (introProgress 0.44 -> 0.58): dramatic uncoil onto burgundy carpet to towering 2.95m height showcasing TOKEN-GUN 98!
                float elapsed = Math.Clamp(SkyscraperFloor1World.IntroSeconds - b.StateTimer, 0f, SkyscraperFloor1World.IntroSeconds);
                float introProgress = elapsed / SkyscraperFloor1World.IntroSeconds;
                float walk = SmoothStep((elapsed - 0.28f) / (SkyscraperFloor1World.IntroSeconds - 0.28f));
                float uncoil = SmoothStep((introProgress - 0.44f) / 0.14f);
                float cyc = walk * MathF.PI * 3.5f + 0.55f;
                float sinS = MathF.Sin(cyc);
                float cosS = MathF.Cos(cyc);

                // Smoothly uncoil from deep stalking hunch into towering heroic authority on the carpet
                p.PelvisRoll  = LerpF(0.038f, 0.050f, uncoil) * sinS;
                p.TorsoPitch  = LerpF(0.68f, 0.08f, uncoil) + 0.022f * MathF.Abs(sinS);
                p.TorsoYaw    = LerpF(0.08f, +0.18f, uncoil) + 0.08f * sinS;
                p.TorsoRoll   = -0.038f * sinS;

                // Deep crouched stalk legs under doorway -> towering athletic Terminator stride on carpet (strictly grounded!)
                float stalkThighL = -1.14f - 0.12f * MathF.Max(0f, sinS);
                float stalkShinL  = +1.88f + 0.10f * MathF.Max(0f, cosS);
                float stalkThighR = -1.00f + 0.14f * MathF.Max(0f, sinS) - 0.14f * MathF.Max(0f, -sinS);
                float stalkShinR  = +1.72f + 0.12f * MathF.Max(0f, -cosS);

                float tallThighL  = -0.30f - 0.20f * MathF.Max(0f, sinS) + 0.12f * MathF.Max(0f, -sinS);
                float tallShinL   = Math.Clamp(-tallThighL + 0.20f * MathF.Max(0f, cosS), 0.14f, 0.68f);
                float tallThighR  = +0.14f + 0.20f * MathF.Max(0f, sinS) - 0.30f * MathF.Max(0f, -sinS);
                float tallShinR   = Math.Clamp(0.18f + 0.40f * MathF.Max(0f, -cosS), 0.14f, 0.68f);

                p.ThighLPitch = LerpF(stalkThighL, tallThighL, uncoil);
                p.ThighLRoll  = LerpF(+0.16f, +0.11f, uncoil);
                p.ShinLPitch  = LerpF(stalkShinL, tallShinL, uncoil);

                p.ThighRPitch = LerpF(stalkThighR, tallThighR, uncoil);
                p.ThighRRoll  = LerpF(-0.16f, -0.11f, uncoil);
                p.ShinRPitch  = LerpF(stalkShinR, tallShinR, uncoil);

                // Left fist braced low-outward as heroic counterweight (zero obstruction of chest Delta Reactor!)
                p.ArmLPitch   = LerpF(-0.46f, -0.26f, uncoil) + 0.20f * sinS;
                p.ArmLYaw     = +0.18f;
                p.ArmLRoll    = LerpF(+0.28f, +0.26f, uncoil);
                p.ElbowLPitch = LerpF(-0.82f, -0.78f, uncoil) - 0.10f * MathF.Max(0f, -sinS);
                p.ElbowLYaw   = -0.20f;

                // Right forearm TOKEN-GUN 98 angled 3/4 broadside so twin brass HF-90 cassette reels & 3 coils catch camera cleanly!
                float speechWave = MathF.Sin(elapsed * 4.0f);
                p.ArmRPitch   = LerpF(-0.74f, -0.82f, uncoil) + 0.035f * speechWave;
                p.ArmRYaw     = LerpF(+0.26f, +0.38f, uncoil);
                p.ArmRRoll    = LerpF(-0.34f, -0.44f, uncoil) - 0.02f * speechWave;
                p.ElbowRPitch = LerpF(-0.78f, -0.60f, uncoil) + 0.03f * speechWave;
                p.ElbowRYaw   = LerpF(+0.22f, +0.30f, uncoil);

                var (hy, hp)  = ComputeBossHeadTrack(b, playerEye, p.TorsoYaw, p.TorsoPitch);
                p.HeadYaw     = hy;
                // Keep crimson visor staring straight out under the doorway during crouch, then tracking player smoothly on uncoil!
                p.HeadPitch   = LerpF(Math.Clamp(-p.TorsoPitch + 0.12f, -0.62f, 0.10f), hp, uncoil);
                float curiousTilt = SmoothStep((introProgress - 0.20f) / 0.10f) * (1f - uncoil);
                p.HeadRoll    = 0.18f * curiousTilt - 0.022f * sinS * uncoil;
                p.PelvisY     = ComputeBossGroundedPelvisY(p, allowKneeRest: false);
                break;
            }

            case ElevatorBossState.ChargingPlayer:
            {
                // Ferocious Juggernaut T-1000 Bull-Rush Sprint!
                float cyc = b.StridePhase;
                float sinC = MathF.Sin(cyc);
                float cosC = MathF.Cos(cyc);

                p.PelvisRoll  = 0.105f * sinC;
                p.TorsoPitch  = 0.48f + 0.04f * MathF.Abs(sinC); // Aggressive juggernaut forward bull-rush lean!
                p.TorsoYaw    = 0.30f * sinC;
                p.TorsoRoll   = -0.085f * sinC;

                p.ThighLPitch = -0.82f * sinC - 0.16f;
                p.ThighLRoll  = +0.08f;
                p.ShinLPitch  = Math.Clamp(0.20f + 0.96f * MathF.Max(0f, MathF.Sin(cyc + 0.88f)), 0.20f, 1.16f);

                p.ThighRPitch = +0.82f * sinC - 0.16f;
                p.ThighRRoll  = -0.08f;
                p.ShinRPitch  = Math.Clamp(0.20f + 0.96f * MathF.Max(0f, MathF.Sin(cyc + MathF.PI + 0.88f)), 0.20f, 1.16f);

                // Tight, low-tucked pumping fists (predatory T-1000 sprint, zero "holding microphone")
                p.ArmLPitch   = +0.82f * sinC + 0.12f;
                p.ArmLRoll    = +0.22f;
                p.ElbowLPitch = -1.08f - 0.18f * cosC;
                p.ElbowLYaw   = -0.24f;

                p.ArmRPitch   = -0.82f * sinC + 0.12f;
                p.ArmRRoll    = -0.22f;
                p.ElbowRPitch = -1.08f + 0.18f * cosC;
                p.ElbowRYaw   = +0.24f;

                var (hy, hp)  = ComputeBossHeadTrack(b, playerEye, p.TorsoYaw, p.TorsoPitch);
                p.HeadYaw     = hy;
                p.HeadPitch   = Math.Clamp(hp - 0.12f, -0.44f, 0.18f); // Tucked predator stare locked on player
                p.HeadRoll    = -0.05f * sinC;
                // Ground stance foot cleanly with a subtle kinetic sprint drive lift.
                p.PelvisY     = ComputeBossGroundedPelvisY(p, allowKneeRest: false)
                    + 0.022f * MathF.Max(0f, MathF.Cos(cyc * 2f));
                break;
            }

            case ElevatorBossState.ThrowingNodeModules:
            {
                // Expressive Telegraph Windup + Explosive Whip Hurl driven by AttackSubTimer / ThrowCycleDuration.
                float dur = MathF.Max(0.28f, b.ThrowCycleDuration);
                float u = Math.Clamp(1f - b.AttackSubTimer / dur, 0f, 1f);

                float windup;   // 1 at peak coil, 0 at release/follow-through
                float release;  // 1 at explosive follow-through
                if (u < 0.20f)
                {
                    // Settle from previous throw follow-through into next windup coil
                    float k = SmoothStep(u / 0.20f);
                    release = 1f - k;
                    windup = k * 0.45f;
                }
                else if (u < 0.78f)
                {
                    // Build dramatic high-arm pitcher telegraph coil
                    float k = SmoothStep((u - 0.20f) / 0.58f);
                    release = 0f;
                    windup = 0.45f + 0.55f * k;
                }
                else
                {
                    // Explosive whip forward & full follow-through!
                    float k = SmoothStep((u - 0.78f) / 0.22f);
                    windup = 1f - k;
                    release = k;
                }

                // Deep athletic staggered throwing lunge: left shoulder leads toward player, right arm coiled BEHIND right shoulder!
                p.PelvisRoll  = -0.07f * windup + 0.08f * release;
                p.TorsoPitch  = +0.20f * windup + 0.32f * release;
                p.TorsoYaw    = -0.78f * windup + 0.56f * release;
                p.TorsoRoll   = -0.14f * windup + 0.12f * release;

                // Front left knee deeply bent with vertical shin -> 100% FLAT grounded boot; back leg driving powerfully!
                p.ThighLPitch = -0.56f - 0.08f * release;
                p.ThighLRoll  = +0.16f;
                p.ShinLPitch  = +0.56f + 0.08f * release;

                p.ThighRPitch = +0.34f * windup + 0.14f * release;
                p.ThighRRoll  = -0.18f;
                p.ShinRPitch  = +0.26f + 0.10f * windup;

                // Right throwing arm pulled BACK & OUT behind twisted right shoulder with sharp cocked elbow (zero "waving Hi"!)
                float idleMix = MathF.Max(0f, 1f - windup - release);
                p.ArmRPitch   = -1.58f * windup - 0.94f * release - 0.55f * idleMix;
                p.ArmRYaw     = -0.56f * windup + 0.26f * release;
                p.ArmRRoll    = -1.16f * windup - 0.24f * (1f - windup);
                p.ElbowRPitch = -1.36f * windup - 0.22f * release - 0.52f * idleMix;
                p.ElbowRYaw   = -0.44f * windup - 0.12f * release;

                // Left arm extended STRAIGHT FORWARD at low-chest sighting height pointing at target (NEVER raised beside head!)
                p.ArmLPitch   = -1.00f * windup + 0.38f * release - 0.36f * idleMix;
                p.ArmLYaw     = +0.62f * windup - 0.16f * release;
                p.ArmLRoll    = +0.18f + 0.10f * release;
                p.ElbowLPitch = -0.22f * windup - 1.08f * release - 0.45f * idleMix;
                p.ElbowLYaw   = -0.12f * windup - 0.20f * release;

                // Keep obsidian visor locked dead onto player throughout torso twist!
                var (hy, hp)  = ComputeBossHeadTrack(b, playerEye, p.TorsoYaw, p.TorsoPitch);
                p.HeadYaw     = hy;
                p.HeadPitch   = hp;
                p.HeadRoll    = 0.06f * windup - 0.05f * release;
                p.PelvisY     = ComputeBossGroundedPelvisY(p, allowKneeRest: false);
                break;
            }

            case ElevatorBossState.SpinningWhileTrue:
            {
                // Lethal Asymmetric Whirlwind Vortex Crouch (bent swept-back slashing arms — NEVER a T-pose!)
                float buzz = MathF.Sin(clock * 28f);
                p.PelvisRoll  = -0.07f;
                p.TorsoPitch  = +0.26f + 0.03f * buzz;
                p.TorsoYaw    = +0.34f;
                p.TorsoRoll   = -0.16f;

                p.ThighLPitch = -0.42f;
                p.ThighLRoll  = +0.20f;
                p.ShinLPitch  = +0.58f;
                p.ThighRPitch = +0.16f;
                p.ThighRRoll  = -0.20f;
                p.ShinRPitch  = +0.36f;

                p.ArmLPitch   = -0.58f + 0.06f * buzz;
                p.ArmLYaw     = +0.38f;
                p.ArmLRoll    = +0.86f;
                p.ElbowLPitch = -0.82f;
                p.ElbowLYaw   = -0.24f;

                p.ArmRPitch   = +0.30f - 0.06f * buzz;
                p.ArmRYaw     = -0.38f;
                p.ArmRRoll    = -0.78f;
                p.ElbowRPitch = -0.74f;
                p.ElbowRYaw   = +0.24f;

                p.HeadPitch   = -0.12f;
                p.HeadYaw     = +0.22f;
                p.HeadRoll    = +0.09f;
                p.PelvisY     = ComputeBossGroundedPelvisY(p, allowKneeRest: false);
                break;
            }

            case ElevatorBossState.StunnedWallCrash:
            {
                // Damaged Glitching Stagger: dropped pelvis, buckled wide knees, slumped torso, spasmodic short-circuit!
                float sway = MathF.Sin(clock * 4.4f);
                float glitchA = MathF.Pow(MathF.Max(0f, MathF.Sin(clock * 19.3f)), 5f);
                float glitchB = MathF.Pow(MathF.Max(0f, MathF.Sin(clock * 23.7f + 1.8f)), 5f);
                float glitchH = glitchA - glitchB;

                p.PelvisRoll  = 0.07f * sway;
                p.TorsoPitch  = 0.42f + 0.06f * MathF.Cos(clock * 5.2f) + 0.07f * glitchA;
                p.TorsoYaw    = 0.14f * sway + 0.10f * glitchH;
                p.TorsoRoll   = 0.13f * MathF.Cos(clock * 3.9f) + 0.08f * glitchB;

                p.ThighLPitch = -0.38f + 0.04f * sway;
                p.ThighLRoll  = +0.18f;
                p.ShinLPitch  = +0.72f - 0.04f * sway;

                p.ThighRPitch = -0.34f - 0.04f * sway;
                p.ThighRRoll  = -0.18f;
                p.ShinRPitch  = +0.68f + 0.04f * sway;

                // Limp twitching arms hanging forward under gravity
                p.ArmLPitch   = -0.44f + 0.18f * glitchA + 0.06f * sway;
                p.ArmLRoll    = +0.24f + 0.10f * glitchA;
                p.ElbowLPitch = -0.48f - 0.24f * glitchA;
                p.ElbowLYaw   = -0.14f;

                p.ArmRPitch   = -0.38f + 0.20f * glitchB - 0.06f * sway;
                p.ArmRRoll    = -0.24f - 0.10f * glitchB;
                p.ElbowRPitch = -0.44f - 0.26f * glitchB;
                p.ElbowRYaw   = +0.14f;

                // Head hanging forward and spasmodically jerking
                p.HeadPitch   = +0.32f + 0.16f * glitchA;
                p.HeadYaw     = +0.34f * MathF.Sin(clock * 10.5f) + 0.24f * glitchH;
                p.HeadRoll    = +0.24f * MathF.Cos(clock * 8.3f) - 0.18f * glitchH;
                p.PelvisY     = ComputeBossGroundedPelvisY(p, allowKneeRest: false);
                break;
            }

            default:
            {
                // Menacing combat idle readiness (never an A-pose!)
                p.TorsoPitch  = 0.06f + 0.02f * breath;
                p.TorsoYaw    = -0.14f;
                p.ThighLPitch = -0.14f; p.ThighLRoll = +0.10f; p.ShinLPitch = +0.20f;
                p.ThighRPitch = +0.08f; p.ThighRRoll = -0.10f; p.ShinRPitch = +0.12f;
                p.ArmLPitch   = -0.42f + 0.04f * breath; p.ArmLRoll = +0.22f; p.ElbowLPitch = -0.96f; p.ElbowLYaw = -0.18f;
                p.ArmRPitch   = -0.52f - 0.04f * breath; p.ArmRRoll = -0.22f; p.ElbowRPitch = -1.04f; p.ElbowRYaw = +0.18f;
                var (hy, hp)  = ComputeBossHeadTrack(b, playerEye, p.TorsoYaw, p.TorsoPitch);
                p.HeadYaw     = hy;
                p.HeadPitch   = hp;
                p.PelvisY     = ComputeBossGroundedPelvisY(p, allowKneeRest: false);
                break;
            }
        }

        return p;
    }

    internal static void DrawEscapedAgentBoss(
        EscapedAgentBoss b, Vector3 playerEye, float clock, float glassOpacity, float shutdown,
        float recoil, Color tint)
    {
        if (!coderParts.TryGetValue("escaped_ai_boss", out var ranges))
        {
            // Safe fallback if a legacy single-mesh GLB is loaded.
            Asset("escaped_ai_boss", b.Position + b.HitDirection * (0.16f * recoil), b.FacingYaw * 180f / MathF.PI,
                scale: BossRootScale, tint: tint, tilt: shutdown * 0.32f - 0.36f * recoil,
                stretch: new Vector3(1f, 1f - 0.33f * shutdown, 1f));
            return;
        }

        // Advance smoothed pose at most once per unique simulation clock so shadow + main passes stay 100% locked!
        bool timeJumped = MathF.Abs(clock - bossLastUpdateClock) > 0.45f;
        if (!bossHasSmoothedPose || b.SnapPose || timeJumped)
        {
            bossSmoothedPose = ComputeBossTargetPose(b, playerEye, clock, glassOpacity, shutdown);
            bossHasSmoothedPose = true;
            bossLastUpdateClock = clock;
            if (!CastingShadows) b.SnapPose = false;
        }
        else if (clock > bossLastUpdateClock + 1e-5f)
        {
            float dt = Math.Clamp(clock - bossLastUpdateClock, 0f, 0.10f);
            float alpha = 1f - MathF.Exp(-12f * dt);
            var target = ComputeBossTargetPose(b, playerEye, clock, glassOpacity, shutdown);
            bossSmoothedPose = LerpBossPose(bossSmoothedPose, target, alpha);
            bossLastUpdateClock = clock;
        }

        BossRigPose p = bossSmoothedPose;

        // Additive crisp directional HitFlinch recoil layered AFTER smoothing so impacts register instantaneously!
        if (b.HitFlinchTimer > 0f && !b.IsDefeated)
        {
            float f = Math.Clamp(b.HitFlinchTimer / 0.24f, 0f, 1f);
            float punch = MathF.Sin(f * MathF.PI * 0.5f);
            Vector3 bossFwd = new(MathF.Sin(b.FacingYaw), 0f, MathF.Cos(b.FacingYaw));
            Vector3 bossLeft = new(bossFwd.Z, 0f, -bossFwd.X);
            float sideHit = Vector3.Dot(b.HitDirection, bossLeft);
            float sideSign = MathF.Abs(sideHit) > 0.15f ? MathF.Sign(sideHit) : 1f;

            // Chest-impact kinetic recoil: upper torso & head snap back, shoulders jolt BACKWARD (never lifting into surrender!)
            p.TorsoPitch  -= 0.22f * punch;
            p.TorsoYaw    += sideSign * 0.18f * punch;
            p.TorsoRoll   += sideSign * 0.10f * punch;
            p.HeadPitch   -= 0.20f * punch;
            p.HeadYaw     -= sideSign * 0.14f * punch;
            p.ArmLPitch   += 0.14f * punch;
            p.ArmLRoll     = Math.Clamp(p.ArmLRoll + 0.10f * punch, 0.06f, 1.05f);
            p.ArmRPitch   += 0.10f * punch;
            p.ArmRRoll     = Math.Clamp(p.ArmRRoll - 0.10f * punch, -1.25f, -0.06f);
        }

        Vector3 rootPos = b.Position + b.HitDirection * (0.14f * recoil);
        Matrix4x4 root = Matrix4x4.CreateScale(BossRootScale)
            * Matrix4x4.CreateRotationY(b.FacingYaw)
            * Matrix4x4.CreateTranslation(rootPos);

        // Exact 10-part hierarchical skeleton contract matching boss-concept-sculptor!
        Matrix4x4 pelvis = Matrix4x4.CreateRotationZ(p.PelvisRoll)
            * Matrix4x4.CreateTranslation(0f, p.PelvisY, p.PelvisZ) * root;
        Matrix4x4 torso = Matrix4x4.CreateFromYawPitchRoll(p.TorsoYaw, p.TorsoPitch, p.TorsoRoll) * pelvis;
        Matrix4x4 head = Matrix4x4.CreateFromYawPitchRoll(p.HeadYaw, p.HeadPitch, p.HeadRoll)
            * Matrix4x4.CreateTranslation(0.000f, +0.745f, 0.000f) * torso;

        Matrix4x4 upperArmL = Matrix4x4.CreateFromYawPitchRoll(p.ArmLYaw, p.ArmLPitch, p.ArmLRoll)
            * Matrix4x4.CreateTranslation(+0.285f, +0.595f, 0.000f) * torso;
        Matrix4x4 forearmL = Matrix4x4.CreateFromYawPitchRoll(p.ElbowLYaw, p.ElbowLPitch, 0f)
            * Matrix4x4.CreateTranslation(0.000f, -0.355f, 0.000f) * upperArmL;

        Matrix4x4 upperArmR = Matrix4x4.CreateFromYawPitchRoll(p.ArmRYaw, p.ArmRPitch, p.ArmRRoll)
            * Matrix4x4.CreateTranslation(-0.285f, +0.595f, 0.000f) * torso;
        Matrix4x4 forearmR = Matrix4x4.CreateFromYawPitchRoll(p.ElbowRYaw, p.ElbowRPitch, 0f)
            * Matrix4x4.CreateTranslation(0.000f, -0.355f, 0.000f) * upperArmR;

        Matrix4x4 thighL = Matrix4x4.CreateFromYawPitchRoll(0f, p.ThighLPitch, p.ThighLRoll)
            * Matrix4x4.CreateTranslation(+0.135f, -0.025f, 0.000f) * pelvis;
        Matrix4x4 shinL = Matrix4x4.CreateFromYawPitchRoll(0f, p.ShinLPitch, -p.ThighLRoll * 0.45f)
            * Matrix4x4.CreateTranslation(0.000f, -0.525f, 0.000f) * thighL;

        Matrix4x4 thighR = Matrix4x4.CreateFromYawPitchRoll(0f, p.ThighRPitch, p.ThighRRoll)
            * Matrix4x4.CreateTranslation(-0.135f, -0.025f, 0.000f) * pelvis;
        Matrix4x4 shinR = Matrix4x4.CreateFromYawPitchRoll(0f, p.ShinRPitch, -p.ThighRRoll * 0.45f)
            * Matrix4x4.CreateTranslation(0.000f, -0.525f, 0.000f) * thighR;

        ReadOnlySpan<Matrix4x4> partMatrices = [
            torso, head, upperArmL, forearmL, upperArmR, forearmR, thighL, shinL, thighR, shinR
        ];

        Model model = imports["escaped_ai_boss"];

        if (CastingShadows)
        {
            for (int part = 0; part < 10; part++)
            {
                var (start, count) = ranges[part];
                var matT = Matrix4x4.Transpose(partMatrices[part]);
                for (int i = start; i < start + count; i++)
                    Raylib.DrawMesh(model.Meshes[i], depthMaterial, matT);
            }
            return;
        }

        // Preserve ceramic/metallic PBR + crimson emission glow via Finish.Imported!
        Surface(Finish.Imported, 0);
        for (int part = 0; part < 10; part++)
        {
            var (start, count) = ranges[part];
            var matT = Matrix4x4.Transpose(partMatrices[part]);
            for (int i = start; i < start + count; i++)
            {
                Material mat = model.Materials[model.MeshMaterial[i]];
                Color original = mat.Maps[0].Color;
                mat.Maps[0].Color = Raylib.ColorTint(original, tint);
                Raylib.DrawMesh(model.Meshes[i], mat, matT);
                mat.Maps[0].Color = original;
            }
        }
        CapturedAssets?.Add("escaped_ai_boss");

        // Subtle translucent deep-crimson/amber plasma glow nested strictly inside TOKEN-GUN 98 muzzle prongs (zero pink blob!)
        if (!b.IsDefeated && b.State == ElevatorBossState.IntroSpeech)
        {
            float charge = 0.45f + 0.55f * SmoothStep((SkyscraperFloor1World.IntroSeconds - b.StateTimer - 0.35f) / 0.85f);
            float pulse = 0.90f + 0.10f * MathF.Sin(clock * 15f);
            Vector3 muzzleWorld = Vector3.Transform(new Vector3(-0.114f, -0.440f, +0.002f), forearmR);
            Raylib.DrawSphere(muzzleWorld, 0.036f * pulse, new Color(196, 16, 28, (int)(54 * charge)));
            Raylib.DrawSphere(muzzleWorld, 0.021f * pulse, new Color(228, 24, 38, (int)(116 * charge)));
            Raylib.DrawSphere(muzzleWorld, 0.010f * pulse, new Color(255, 198, 122, (int)(124 * charge)));
            for (int k = 0; k < 3; k++)
            {
                float a = clock * 8.0f + k * MathF.Tau / 3f;
                float r = 0.042f + 0.014f * charge;
                Vector3 spark = muzzleWorld + new Vector3(MathF.Cos(a) * r, MathF.Sin(a * 1.4f) * r * 0.55f, MathF.Sin(a) * r);
                Raylib.DrawSphere(spark, 0.0065f * charge, k == 0
                    ? new Color(255, 206, 124, (int)(120 * charge))
                    : new Color(214, 22, 34, (int)(110 * charge)));
            }
        }

        // Visual throw telegraph: sinister dark-obsidian & deep-crimson (#E81424) glowing node_modules cube gripped in right gauntlet!
        if (!b.IsDefeated && b.State == ElevatorBossState.ThrowingNodeModules)
        {
            float dur = MathF.Max(0.28f, b.ThrowCycleDuration);
            float u = Math.Clamp(1f - b.AttackSubTimer / dur, 0f, 1f);
            if (u is >= 0.24f and <= 0.88f)
            {
                float charge = SmoothStep((u - 0.24f) / 0.56f);
                Vector3 handWorld = Vector3.Transform(new Vector3(0f, -0.36f, +0.10f), forearmR);
                float s = (0.17f + 0.09f * charge) * (1f + 0.06f * MathF.Sin(clock * 24f));
                Vector3 spinAxis = Vector3.Normalize(new Vector3(0.45f, 1.0f, 0.35f));
                float spinDeg = (clock * 190f + 32f) % 360f;
                // Dark obsidian metallic cube body + deep-crimson glowing energy core ribs (zero pastel pink!)
                Color obsidian = new(20, 22, 28, 255);
                Color crimson  = new(232, 20, 36, 255);
                Draw(bevel, handWorld, new Vector3(s, s, s), obsidian, Finish.Armor, spinAxis, spinDeg);
                Draw(box, handWorld, new Vector3(s * 1.06f, s * 0.17f, s * 1.06f), crimson, Finish.Lamp, spinAxis, spinDeg);
                Draw(box, handWorld, new Vector3(s * 0.17f, s * 1.06f, s * 1.06f), crimson, Finish.Lamp, spinAxis, spinDeg);
                Draw(box, handWorld, new Vector3(s * 1.06f, s * 1.06f, s * 0.17f), crimson, Finish.Lamp, spinAxis, spinDeg);
                // 3 tight orbiting crimson energy sparks around the charged cube
                for (int k = 0; k < 3; k++)
                {
                    float a = clock * 7.5f + k * MathF.Tau / 3f;
                    float orbitR = s * 0.82f;
                    Vector3 spark = handWorld + new Vector3(MathF.Cos(a) * orbitR, MathF.Sin(a * 1.5f) * orbitR * 0.55f, MathF.Sin(a) * orbitR);
                    Orb(spark, 0.024f, crimson, Finish.Lamp);
                }
            }
        }
    }
}
