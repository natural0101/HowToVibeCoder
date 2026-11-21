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
    internal static Vector3 BossMuzzleWorldPos;
    internal static Vector3 BossMuzzleWorldDir = -Vector3.UnitZ;
    internal static bool BossMuzzleValid;

    internal static void ResetBossPose()
    {
        bossHasSmoothedPose = false;
        bossLastUpdateClock = -999f;
        BossMuzzleValid = false;
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

    private static float EvalSalvoKick(float u)
    {
        if (u < 0.575f || u > 0.725f) return 0f;
        ReadOnlySpan<float> fireTimes = [0.585f, 0.611f, 0.638f, 0.664f];
        float kick = 0f;
        for (int i = 0; i < fireTimes.Length; i++)
        {
            float d = (u - fireTimes[i]) / 0.030f;
            if (d > 0f && d < 1.35f)
            {
                float pulse = SmoothStep(d / 0.16f) * MathF.Exp(-d * 2.65f);
                if (pulse > kick) kick = pulse;
            }
        }
        return kick;
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
                // Predatory coiled sentinel crouch hidden deep inside the dark elevator cab:
                // deeply bent knees + hunched torso keep head 100% clear below 2.30m door lintel.
                // Chin stays tucked in shadow during false calm (r < 0.86), then SNAPS UP at r = 0.86..0.96
                // right as his TWO piercing crimson visor eyes suddenly ignite!
                float repairR = Math.Clamp(
                    (SkyscraperFloor1World.RepairSeconds - b.StateTimer) / SkyscraperFloor1World.RepairSeconds, 0f, 1f);
                float wake = SmoothStep(1f - glassOpacity);
                float eyeSnap = SmoothStep((repairR - 0.86f) / 0.08f);
                float servo = 0.018f * MathF.Sin(clock * 7.5f) * eyeSnap;
                p.TorsoPitch  = LerpF(0.88f, 0.72f, eyeSnap);
                p.TorsoYaw    = LerpF(0.02f, 0.08f, wake);
                p.HeadPitch   = LerpF(0.36f, -0.58f, eyeSnap) + servo;
                p.HeadYaw     = 0.04f * MathF.Sin(clock * 1.4f) * (1f - eyeSnap);
                p.ThighLPitch = LerpF(-1.24f, -1.16f, wake);
                p.ThighLRoll  = +0.16f;
                p.ShinLPitch  = LerpF(+2.02f, +1.92f, wake);
                p.ThighRPitch = LerpF(-1.14f, -1.04f, wake);
                p.ThighRRoll  = -0.16f;
                p.ShinRPitch  = LerpF(+1.88f, +1.76f, wake);
                p.ArmLPitch   = LerpF(-0.36f, -0.64f, eyeSnap);
                p.ArmLRoll    = LerpF(+0.20f, +0.30f, eyeSnap);
                p.ElbowLPitch = LerpF(-0.56f, -0.88f, eyeSnap);
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
                // Exact biomechanical foot-locked walkout shared with LevelAndEntities & BossIntroCutscene:
                // 4 discrete 0.875m Terminator footsteps with zero foot sliding + violent left-arm shove
                // + dead-still speech pauses + crisp rocket-salvo firing brace over u = 0.62..0.73!
                float elapsed = Math.Clamp(SkyscraperFloor1World.IntroSeconds - b.StateTimer, 0f, SkyscraperFloor1World.IntroSeconds);
                float introProgress = elapsed / SkyscraperFloor1World.IntroSeconds;
                var w = BossIntroCutscene.EvalIntroWalk(introProgress);
                float uncoil = w.Uncoil;
                float stompDip = w.StompPulse;

                // C1-continuous biomechanical foot-locked leg angles (StepFrac 0 and 1 match planted stance to 0.000 rad -> ZERO leg snaps!)
                float tallThighL, tallShinL, tallThighR, tallShinR;
                float swingSign = 0f;
                const float frontThigh = -0.30f;
                const float rearThigh  = +0.28f;
                const float baseShin   = 0.16f;
                if (w.IsMoving)
                {
                    float f = w.StepFrac;
                    float s = SmoothStep(f);
                    float stanceThigh = LerpF(frontThigh, rearThigh, s);
                    float stanceShin  = baseShin + 0.10f * MathF.Sin(f * MathF.PI);

                    float liftArc = MathF.Max(0f, MathF.Sin(f * MathF.PI));
                    float swingThigh = LerpF(rearThigh, frontThigh, s) - 0.38f * liftArc;
                    float swingShin  = baseShin + 1.16f * MathF.Pow(liftArc, 0.72f) * (1f - SmoothStep((f - 0.76f) / 0.24f) * 0.88f);

                    if (w.LeftSwing)
                    {
                        tallThighL = swingThigh; tallShinL = swingShin;
                        tallThighR = stanceThigh; tallShinR = stanceShin;
                        swingSign = +1f * liftArc;
                    }
                    else
                    {
                        tallThighR = swingThigh; tallShinR = swingShin;
                        tallThighL = stanceThigh; tallShinL = stanceShin;
                        swingSign = -1f * liftArc;
                    }
                }
                else
                {
                    // Rock-solid planted stance matching exact step-end angles (zero pop on pause start/end!)
                    bool leftForward = (w.CompletedSteps % 2) == 0;
                    tallThighL = leftForward ? frontThigh : rearThigh;
                    tallThighR = leftForward ? rearThigh : frontThigh;
                    tallShinL  = baseShin;
                    tallShinR  = baseShin;
                }

                // Predatory crouched doorway clearance during Step 0, uncoiling smoothly across Steps 1..2
                float crouchBlend = 1f - uncoil;
                float stalkThighL = -0.96f + (tallThighL * 0.42f);
                float stalkShinL  = Math.Clamp(1.62f + (tallShinL - baseShin) * 0.40f, 1.32f, 2.02f);
                float stalkThighR = -0.90f + (tallThighR * 0.42f);
                float stalkShinR  = Math.Clamp(1.56f + (tallShinR - baseShin) * 0.40f, 1.32f, 2.02f);

                p.ThighLPitch = LerpF(tallThighL, stalkThighL, crouchBlend);
                p.ThighLRoll  = LerpF(+0.12f, +0.16f, crouchBlend);
                p.ShinLPitch  = LerpF(tallShinL, stalkShinL, crouchBlend);

                p.ThighRPitch = LerpF(tallThighR, stalkThighR, crouchBlend);
                p.ThighRRoll  = LerpF(-0.12f, -0.16f, crouchBlend);
                p.ShinRPitch  = LerpF(tallShinR, stalkShinR, crouchBlend);

                // Rocket-salvo aiming brace & 4-missile recoil cadence: arm locks cleanly at chest-side height BEFORE u=0.585 launch!
                float salvoBrace = SmoothStep((introProgress - 0.505f) / 0.045f)
                    * (1f - SmoothStep((introProgress - 0.690f) / 0.050f));
                float salvoKick = EvalSalvoKick(introProgress);
                float salvoSweep = MathF.Sin(Math.Clamp((introProgress - 0.585f) / 0.095f, 0f, 1f) * MathF.PI * 1.50f);
                // Athletic menacing combat-ready guard pose over u = 0.78..1.00 right before boss fight begins!
                float combatReady = SmoothStep((introProgress - 0.76f) / 0.12f);

                // Heavy titanium weight shift & counter-rotation on every real footstep + shove torque + salvo recoil
                float shove = w.ShovePulse;
                p.PelvisRoll  = 0.095f * swingSign;
                p.TorsoPitch  = LerpF(0.09f, 0.52f, crouchBlend) + 0.045f * stompDip + 0.16f * shove
                    - (0.04f * salvoBrace + 0.11f * salvoKick);
                p.TorsoYaw    = LerpF(+0.14f, +0.04f, crouchBlend) - 0.15f * swingSign - 0.42f * shove
                    + (0.24f * salvoSweep - 0.08f * salvoKick) * salvoBrace;
                p.TorsoRoll   = -0.075f * swingSign - 0.10f * shove + 0.05f * salvoSweep * salvoBrace;

                // Left arm: CLEAN PHYSICAL COLLAR GRAB (u=0.00..0.065) -> LIFT HERO (0.065..0.165) ->
                // WIND-UP (0.165..0.195) -> BRUTAL SIDEARM HURL (0.195..0.27) across the atrium!
                float grabReach = SmoothStep(introProgress / 0.055f) * (1f - SmoothStep((introProgress - 0.21f) / 0.065f));
                float liftHold  = SmoothStep((introProgress - 0.062f) / 0.045f) * (1f - SmoothStep((introProgress - 0.165f) / 0.028f));
                float windup    = SmoothStep((introProgress - 0.162f) / 0.024f) * (1f - SmoothStep((introProgress - 0.195f) / 0.018f));
                float hurlWhip  = SmoothStep((introProgress - 0.192f) / 0.024f) * (1f - SmoothStep((introProgress - 0.238f) / 0.055f));

                float baseArmLPitch = LerpF(-0.28f, -0.42f, crouchBlend) - 0.42f * swingSign;
                float armLPitch = baseArmLPitch;
                float armLYaw   = +0.14f;
                float armLRoll  = +0.24f;
                float elbowLP   = -0.74f + 0.16f * MathF.Abs(swingSign);
                float elbowLY   = -0.16f;

                if (grabReach > 0.01f)
                {
                    // Framed cleanly in front of left shoulder reaching straight into hero collar (no side-frame clipping!)
                    float targetPitch = -1.36f - 0.28f * liftHold + 0.24f * hurlWhip;
                    float targetYaw   = -0.18f + 0.34f * windup - 0.78f * hurlWhip;
                    float targetRoll  = +0.10f + 0.22f * windup + 0.34f * hurlWhip;
                    float targetElbP  = -0.36f - 0.32f * liftHold + 0.18f * hurlWhip;
                    armLPitch = LerpF(armLPitch, targetPitch, grabReach);
                    armLYaw   = LerpF(armLYaw, targetYaw, grabReach);
                    armLRoll  = LerpF(armLRoll, targetRoll, grabReach);
                    elbowLP   = LerpF(elbowLP, targetElbP, grabReach);
                    elbowLY   = LerpF(elbowLY, +0.24f * hurlWhip, grabReach);
                    p.TorsoYaw += (+0.32f * windup - 0.56f * hurlWhip) * grabReach;
                    p.TorsoPitch += (+0.12f * grabReach * (1f - liftHold) + 0.12f * hurlWhip);
                }

                p.ArmLPitch   = LerpF(LerpF(armLPitch, -0.44f + 0.06f * salvoKick, salvoBrace), -0.52f, combatReady);
                p.ArmLYaw     = LerpF(LerpF(armLYaw, +0.24f - 0.08f * salvoSweep, salvoBrace), +0.18f, combatReady);
                p.ArmLRoll    = LerpF(LerpF(armLRoll, +0.42f, salvoBrace), +0.24f, combatReady);
                p.ElbowLPitch = LerpF(LerpF(elbowLP, -0.74f, salvoBrace), -1.04f, combatReady);
                p.ElbowLYaw   = LerpF(LerpF(elbowLY, -0.16f, salvoBrace), -0.22f, combatReady);

                // Right forearm TOKEN-GUN 98: held cleanly at chest-side height (never blocking visor!) aiming straight at target!
                float cannonShowcase = SmoothStep((introProgress - 0.44f) / 0.12f);
                float speechWave = MathF.Sin(elapsed * 4.2f) * (1f - shove) * (1f - salvoBrace) * (1f - combatReady);
                float baseArmRPitch   = LerpF(-0.52f, -0.78f, cannonShowcase) + 0.025f * speechWave;
                float baseArmRYaw     = LerpF(+0.12f, +0.22f, cannonShowcase);
                float baseArmRRoll    = LerpF(-0.26f, -0.38f, cannonShowcase) - 0.015f * speechWave;
                float baseElbowRPitch = LerpF(-0.76f, -0.48f, cannonShowcase) + 0.025f * speechWave;
                float baseElbowRYaw   = LerpF(+0.10f, +0.18f, cannonShowcase);

                p.ArmRPitch   = LerpF(LerpF(baseArmRPitch, -0.96f + 0.22f * salvoKick, salvoBrace), -0.68f, combatReady);
                p.ArmRYaw     = LerpF(LerpF(baseArmRYaw, +0.06f + 0.24f * salvoSweep, salvoBrace), +0.14f, combatReady);
                p.ArmRRoll    = LerpF(LerpF(baseArmRRoll, -0.42f - 0.08f * salvoKick, salvoBrace), -0.28f, combatReady);
                p.ElbowRPitch = LerpF(LerpF(baseElbowRPitch, -0.42f - 0.20f * salvoKick, salvoBrace), -0.82f, combatReady);
                p.ElbowRYaw   = LerpF(LerpF(baseElbowRYaw, +0.08f + 0.14f * salvoSweep, salvoBrace), +0.16f, combatReady);

                var (hy, hp)  = ComputeBossHeadTrack(b, playerEye, p.TorsoYaw, p.TorsoPitch);
                p.HeadYaw     = hy;
                float lookDownAtFloor = SmoothStep((introProgress - 0.26f) / 0.10f) * (1f - SmoothStep((introProgress - 0.54f) / 0.12f));
                p.HeadPitch   = LerpF(Math.Clamp(-p.TorsoPitch + 0.08f, -0.52f, 0.10f), hp, uncoil) + 0.18f * lookDownAtFloor;
                float curiousTilt = SmoothStep((introProgress - 0.34f) / 0.08f) * (1f - SmoothStep((introProgress - 0.54f) / 0.10f));
                p.HeadRoll    = 0.18f * curiousTilt - 0.025f * swingSign;

                // Express heavy stomp & salvo recoil via physical knee compression BEFORE grounding -> boots NEVER sink below Y=0!
                float kneeWeight = 0.095f * stompDip + 0.045f * salvoKick;
                p.ThighLPitch -= kneeWeight * 0.48f;
                p.ShinLPitch  += kneeWeight;
                p.ThighRPitch -= kneeWeight * 0.48f;
                p.ShinRPitch  += kneeWeight;
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
            bool fastRepairSnap = b.State == ElevatorBossState.Repairing &&
                (SkyscraperFloor1World.RepairSeconds - b.StateTimer) >= SkyscraperFloor1World.RepairSeconds * 0.85f;
            float rate = (b.State == ElevatorBossState.IntroSpeech || fastRepairSnap) ? 34f : 14f;
            float alpha = 1f - MathF.Exp(-rate * dt);
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

        // Always export exact live TOKEN-GUN 98 barrel world position & direction so missiles fire strictly from the physical muzzle!
        Vector3 liveMuzzlePos = Vector3.Transform(new Vector3(-0.114f, -0.440f, +0.002f), forearmR);
        Vector3 liveMuzzleTip = Vector3.Transform(new Vector3(-0.114f, -0.560f, +0.002f), forearmR);
        Vector3 liveMuzzleDelta = liveMuzzleTip - liveMuzzlePos;
        BossMuzzleWorldPos = liveMuzzlePos;
        BossMuzzleWorldDir = liveMuzzleDelta.LengthSquared() > 1e-6f ? Vector3.Normalize(liveMuzzleDelta) : -Vector3.UnitZ;
        BossMuzzleValid = true;

        // TOKEN-GUN 98 compact emitter glow nested strictly inside the barrel prongs (no cartoon floating spheres!)
        if (!b.IsDefeated && b.State == ElevatorBossState.IntroSpeech)
        {
            float introElapsed = Math.Clamp(SkyscraperFloor1World.IntroSeconds - b.StateTimer, 0f, SkyscraperFloor1World.IntroSeconds);
            float introU = introElapsed / SkyscraperFloor1World.IntroSeconds;
            float charge = 0.45f + 0.55f * SmoothStep((introElapsed - 0.35f) / 0.85f);
            float pulse = 0.90f + 0.10f * MathF.Sin(clock * 15f);
            float salvoKick = EvalSalvoKick(introU);

            if (salvoKick > 0.08f)
            {
                // Tight high-energy launch flash seated right inside the barrel mouth
                float fScale = 0.65f + 0.45f * salvoKick;
                Raylib.DrawSphere(liveMuzzlePos + BossMuzzleWorldDir * 0.03f, 0.058f * fScale,
                    new Color(255, 134, 34, Math.Clamp((int)(185 * salvoKick), 0, 255)));
                Raylib.DrawSphere(liveMuzzlePos + BossMuzzleWorldDir * 0.015f, 0.032f * fScale,
                    new Color(255, 246, 202, Math.Clamp((int)(235 * salvoKick), 0, 255)));
            }
            else
            {
                Raylib.DrawSphere(liveMuzzlePos, 0.022f * pulse, new Color(228, 24, 38, Math.Clamp((int)(110 * charge), 0, 255)));
                Raylib.DrawSphere(liveMuzzlePos, 0.011f * pulse, new Color(255, 204, 134, Math.Clamp((int)(130 * charge), 0, 255)));
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
