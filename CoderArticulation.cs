using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal static unsafe partial class VisualEngine3D
{
    private static readonly int[] TorsoTintSlots = [1, 2];
    private static readonly int[] ArmTintSlots = [1, 2];
    private static readonly int[] ShinTintSlots = [2];

    private static void DrawArticulatedPart(string name, Matrix4x4 world, Color hoodie, Color trim, Color visor,
        bool tintHoodie, bool tintTrim, bool tintVisor, float flash)
    {
        var model = imports[name];
        if (CastingShadows)
        {
            DrawDepth(model, world);
            return;
        }
        Surface(Finish.Anatomy, 0);
        Span<Color> saved = stackalloc Color[model.MaterialCount];
        for (int i = 0; i < saved.Length; i++)
        {
            ref var pigment = ref model.Materials[i].Maps[0].Color;
            saved[i] = pigment;
            if (tintHoodie && i == 1) pigment = hoodie;
            else if (tintTrim && ((tintHoodie && i == 2) || (!tintHoodie && i == 2))) pigment = trim;
            else if (tintVisor && i == 5) pigment = visor;
            if (flash > 0f && ((tintHoodie && i is 1 or 2) || (!tintHoodie && !tintVisor && i == 2)))
                pigment = Raylib.ColorLerp(pigment, new Color(255, 200, 140, 255), flash);
        }
        var pose = Matrix4x4.Transpose(world);
        for (int i = 0; i < model.MeshCount; i++)
            Raylib.DrawMesh(model.Meshes[i], model.Materials[model.MeshMaterial[i]], pose);
        for (int i = 0; i < saved.Length; i++)
            model.Materials[i].Maps[0].Color = saved[i];
        CapturedAssets?.Add(name);
    }

    internal static void DrawSeatedCoder(Vector3 feet, float yawDeg, float scale, Color hoodie, Color visor, float workBob)
    {
        const string asset = "char_vibe_coder_seated";
        var model = imports[asset];
        var world = Matrix4x4.CreateRotationX(workBob)
            * Matrix4x4.CreateScale(scale)
            * Matrix4x4.CreateRotationY(yawDeg * MathF.PI / 180f)
            * Matrix4x4.CreateTranslation(feet);
        if (CastingShadows)
        {
            DrawDepth(model, world);
            return;
        }
        Surface(Finish.Anatomy, 0);
        Span<Color> saved = stackalloc Color[model.MaterialCount];
        Color trim = Raylib.ColorBrightness(hoodie, -.55f);
        for (int i = 0; i < saved.Length; i++)
        {
            ref var pigment = ref model.Materials[i].Maps[0].Color;
            saved[i] = pigment;
            pigment = i switch { 3 => hoodie, 5 => trim, 9 => visor, _ => pigment };
        }
        var pose = Matrix4x4.Transpose(world);
        for (int i = 0; i < model.MeshCount; i++)
            Raylib.DrawMesh(model.Meshes[i], model.Materials[model.MeshMaterial[i]], pose);
        for (int i = 0; i < saved.Length; i++)
            model.Materials[i].Maps[0].Color = saved[i];
        CapturedAssets?.Add(asset);
    }

    internal static void DrawCoder(VibeCoderEntity c, Vector3 feet, float extraTilt, Color tint, float clock = 0f)
    {
        float scale = c.ModelScale;
        float cyc = c.WalkCycle;
        float sinC = MathF.Sin(cyc), cosC = MathF.Cos(cyc);

        // Compute expressive per-joint pose (row-vector convention: +X = lean fwd / shin bend back, -X = arm/thigh lift fwd).
        float pelvisY = .90f;
        float torsoPitch = extraTilt * .55f, torsoYaw = 0f, torsoRoll = 0f;
        float headPitch = 0f, headYaw = 0f, headRoll = 0f;
        float armLPitch = -.10f, armLRoll = .08f, armRPitch = -.10f, armRRoll = -.08f;
        float thighLPitch = 0f, shinLPitch = 0f, thighRPitch = 0f, shinRPitch = 0f;
        float rootTilt = 0f;

        bool knockedOut = c.IsCombatCoder && c.State == CoderAIState.Subdued;
        bool stunned = c.CanSignNda;
        bool fleeing = c.State == CoderAIState.PanickedFleeing && !stunned;
        bool sneaking = c.State == CoderAIState.SneakingToBait;
        bool lured = c.State == CoderAIState.LuredAtBait;
        bool walking = c.ExitingRoom || (!c.IsCombatCoder || c.Aggro) && c.State is CoderAIState.WalkingToBait or CoderAIState.CombatHostile;

        if (knockedOut)
        {
            rootTilt = -1.45f * Math.Clamp((c.KnockoutTime + .05f) * 4f, 0f, 1f);
            armLPitch = -.45f; armLRoll = .45f;
            armRPitch = -.35f; armRRoll = -.48f;
            thighLPitch = -.12f; shinLPitch = .22f;
            thighRPitch = .08f; shinRPitch = .15f;
        }
        else if (stunned)
        {
            // Cleanly kneeling on both knees, slumped dizzy torso swaying, hands drooping near head!
            pelvisY = .50f;
            float sway = MathF.Sin(clock * 3.4f + c.Id);
            torsoPitch = .44f + .06f * MathF.Sin(clock * 2.7f + c.Id);
            torsoRoll = .12f * sway;
            headPitch = .36f + .08f * MathF.Cos(clock * 3.8f + c.Id);
            headYaw = .24f * sway;
            headRoll = .16f * MathF.Cos(clock * 3.1f + c.Id);
            // Kneeling legs: thighs down/slightly back, shins folded cleanly backward resting on floor
            thighLPitch = -.10f; shinLPitch = 1.62f;
            thighRPitch = -.06f; shinRPitch = 1.58f;
            // Arms slumped down/forward protecting head & knees
            armLPitch = -.68f + .08f * sway; armLRoll = .22f;
            armRPitch = -.64f - .08f * sway; armRRoll = -.22f;
        }
        else if (fleeing)
        {
            // Athletic panicked sprint — ZERO bat-wings! Crisp high knee drive + frantically raised waving arms!
            bool dodging = c.DodgeCooldown > 1.0f;
            pelvisY = .86f + .045f * MathF.Abs(sinC) + (dodging ? .14f : 0f);
            torsoPitch = .30f + extraTilt * .4f;
            torsoYaw = -.18f * sinC;
            torsoRoll = dodging ? (c.Id % 2 == 0 ? .28f : -.28f) : .07f * sinC;
            headPitch = -.14f;
            headYaw = .38f * MathF.Sin(clock * 4.5f + c.Id); // glancing back in terror!
            // High-energy sprint stride
            thighLPitch = -.74f * sinC - .12f;
            shinLPitch = Math.Max(0f, .88f * MathF.Sin(cyc + .95f) + .32f);
            thighRPitch = .74f * sinC - .12f;
            shinRPitch = Math.Max(0f, .88f * MathF.Sin(cyc + MathF.PI + .95f) + .32f);
            // Arms thrown high up & waving frantically around shoulder ball joints
            armLPitch = -2.28f + .42f * sinC; armLRoll = .34f + .16f * cosC;
            armRPitch = -2.28f - .42f * sinC; armRRoll = -.34f - .16f * cosC;
        }
        else if (lured)
        {
            // Deep crouch over floor AppleBook, furiously typing on keyboard!
            pelvisY = .56f;
            torsoPitch = .52f + .035f * MathF.Sin(cyc * 1.4f);
            headPitch = .34f + .045f * MathF.Sin(cyc * 2.1f);
            headYaw = .09f * MathF.Sin(clock * 1.9f + c.Id);
            thighLPitch = -1.18f; shinLPitch = 1.68f;
            thighRPitch = -1.12f; shinRPitch = 1.62f;
            // Rapid alternating typing hands reaching down toward floor laptop
            armLPitch = -1.18f + .11f * MathF.Sin(clock * 13f + c.Id); armLRoll = -.12f;
            armRPitch = -1.18f - .11f * MathF.Sin(clock * 13f + c.Id + 1.4f); armRRoll = .12f;
        }
        else if (sneaking)
        {
            // Cautious mouse-on-cheese tip-toe creep: hunched knees, low center, nervous scanning head!
            pelvisY = .77f + .022f * MathF.Abs(sinC);
            torsoPitch = .32f;
            torsoYaw = -.10f * sinC;
            headPitch = -.12f;
            headYaw = .52f * MathF.Sin(clock * 2.2f + c.Id); // nervously scanning left-right!
            thighLPitch = -.48f - .34f * sinC;
            shinLPitch = .62f + .26f * Math.Max(0f, -cosC);
            thighRPitch = -.48f + .34f * sinC;
            shinRPitch = .62f + .26f * Math.Max(0f, cosC);
            // Hands tucked forward cautiously like a sneaky thief
            armLPitch = -.86f + .16f * sinC; armLRoll = .12f;
            armRPitch = -.86f - .16f * sinC; armRRoll = -.12f;
        }
        else if (walking)
        {
            pelvisY = .89f + .025f * MathF.Abs(sinC);
            torsoPitch = .10f + extraTilt * .5f;
            torsoYaw = -.10f * sinC;
            thighLPitch = -.52f * sinC;
            shinLPitch = Math.Max(0f, .55f * MathF.Sin(cyc + .85f) + .12f);
            thighRPitch = .52f * sinC;
            shinRPitch = Math.Max(0f, .55f * MathF.Sin(cyc + MathF.PI + .85f) + .12f);
            armLPitch = .42f * sinC - .12f; armLRoll = .08f;
            armRPitch = -.42f * sinC - .12f; armRRoll = -.08f;
        }
        else
        {
            // Relaxed idle breathing + subtle head scan
            float breath = MathF.Sin(clock * 2.4f + c.Id);
            pelvisY = .90f + .008f * breath;
            headYaw = .14f * MathF.Sin(clock * 1.1f + c.Id);
            armLPitch = -.08f + .03f * breath; armLRoll = .08f;
            armRPitch = -.08f - .03f * breath; armRRoll = -.08f;
        }

        // Visceral hit-flinch reaction layered cleanly onto upper body & arms!
        if (c.HitFlinchTimer > 0f && !knockedOut)
        {
            float f = Math.Clamp(c.HitFlinchTimer / .22f, 0f, 1f);
            torsoPitch -= .48f * f;
            headPitch -= .36f * f;
            armLPitch -= .65f * f; armLRoll += .35f * f;
            armRPitch -= .65f * f; armRRoll -= .35f * f;
        }

        // Combat melee windup raises right arm cleanly!
        if (c.IsCombatCoder && c.Windup > 0f && !knockedOut)
        {
            float lift = 1f - c.Windup / (c.WindingSlap ? .38f : .58f);
            armRPitch = -1.65f - .55f * lift;
            armRRoll = -.25f;
            torsoYaw = -.28f * lift;
        }

        // Compute dynamic per-species colors (HoodieColor, dark trim, glowing GlassesColor)
        Color hoodie = c.HoodieColor.A > 0 ? c.HoodieColor : new Color(92, 138, 214, 255);
        if (c.HitFlashTimer > 0f) hoodie = Raylib.ColorLerp(hoodie, Color.White, .55f);
        Color trim = Raylib.ColorBrightness(hoodie, -.55f);
        Color visor = c.GlassesColor.A > 0 ? c.GlassesColor : new Color(92, 232, 224, 255);
        float flash = Math.Clamp(c.HitFlashTimer / .14f, 0f, 1f) * .35f;

        Matrix4x4 root = Matrix4x4.CreateRotationX(rootTilt)
            * Matrix4x4.CreateScale(scale)
            * Matrix4x4.CreateRotationY(c.FacingYaw)
            * Matrix4x4.CreateTranslation(feet);

        Matrix4x4 pelvis = Matrix4x4.CreateTranslation(0f, pelvisY, 0f) * root;
        Matrix4x4 torso = Matrix4x4.CreateFromYawPitchRoll(torsoYaw, torsoPitch, torsoRoll) * pelvis;
        Matrix4x4 head = Matrix4x4.CreateFromYawPitchRoll(headYaw, headPitch, headRoll)
            * Matrix4x4.CreateTranslation(0f, .54f, 0f) * torso;

        Matrix4x4 armL = Matrix4x4.CreateFromYawPitchRoll(0f, armLPitch, armLRoll)
            * Matrix4x4.CreateTranslation(.168f, .432f, 0f) * torso;
        Matrix4x4 armR = Matrix4x4.CreateFromYawPitchRoll(0f, armRPitch, armRRoll)
            * Matrix4x4.CreateTranslation(-.168f, .432f, 0f) * torso;

        Matrix4x4 thighL = Matrix4x4.CreateRotationX(thighLPitch)
            * Matrix4x4.CreateTranslation(.088f, 0f, 0f) * pelvis;
        Matrix4x4 shinL = Matrix4x4.CreateRotationX(shinLPitch)
            * Matrix4x4.CreateTranslation(0f, -.42f, 0f) * thighL;

        Matrix4x4 thighR = Matrix4x4.CreateRotationX(thighRPitch)
            * Matrix4x4.CreateTranslation(-.088f, 0f, 0f) * pelvis;
        Matrix4x4 shinR = Matrix4x4.CreateRotationX(shinRPitch)
            * Matrix4x4.CreateTranslation(0f, -.42f, 0f) * thighR;

        DrawArticulatedPart("coder_torso", torso, hoodie, trim, visor, true, true, false, flash);
        DrawArticulatedPart("coder_head", head, hoodie, trim, visor, false, false, true, flash);
        DrawArticulatedPart("coder_arm_l", armL, hoodie, trim, visor, true, true, false, flash);
        DrawArticulatedPart("coder_arm_r", armR, hoodie, trim, visor, true, true, false, flash);
        DrawArticulatedPart("coder_thigh_l", thighL, hoodie, trim, visor, false, false, false, 0f);
        DrawArticulatedPart("coder_shin_l", shinL, hoodie, trim, visor, false, true, false, 0f);
        DrawArticulatedPart("coder_thigh_r", thighR, hoodie, trim, visor, false, false, false, 0f);
        DrawArticulatedPart("coder_shin_r", shinR, hoodie, trim, visor, false, true, false, 0f);
    }
}
