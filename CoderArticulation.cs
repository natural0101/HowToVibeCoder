using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

internal static unsafe partial class VisualEngine3D
{
    internal static int SpeciesHeadIndex(string speciesId, int fallbackId = 0) => speciesId switch
    {
        "tab" or "stack" => 0,
        "div" or "smoothie" => 1,
        "weekend" or "leverage" => 2,
        "prompt" or "framework" => 3,
        "call" or "deadline" => 4,
        "refactor" => 5,
        "no-commits" => 6,
        "skolkovo" => 7,
        "meetup" => 8,
        "friday" => 9,
        "legacy" => 10,
        "sb_chief" or "toxic_pm" or "scrum_inquisitor" => 11,
        _ => Math.Abs(fallbackId) % 12,
    };

    internal static (float Width, float Height, float ShoeX) SpeciesProportions(string speciesId) => speciesId switch
    {
        "div" or "smoothie" => (.94f, 1.04f, 1.00f),
        "refactor" or "sb_chief" => (1.13f, 1.04f, 1.06f),
        "meetup" => (1.02f, 1.03f, 1.28f),
        "no-commits" or "skolkovo" => (1.03f, 1.05f, 1.00f),
        "friday" or "legacy" => (1.06f, 1.07f, 1.08f),
        "prompt" or "framework" => (.97f, 1.02f, 1.00f),
        _ => (1.00f, 1.00f, 1.00f),
    };

    internal struct CoderRigPose
    {
        public float PelvisY, PelvisRoll, RootTilt;
        public float TorsoPitch, TorsoYaw, TorsoRoll;
        public float HeadPitch, HeadYaw, HeadRoll;
        public float ArmLPitch, ArmLRoll, ElbowLPitch, ElbowLYaw;
        public float ArmRPitch, ArmRRoll, ElbowRPitch, ElbowRYaw;
        public float ThighLPitch, ThighLRoll, ShinLPitch;
        public float ThighRPitch, ThighRRoll, ShinRPitch;
    }

    internal static CoderRigPose PersonalityIdlePose(int headIdx, float clock, int seed)
    {
        float breath = MathF.Sin(clock * 2.1f + seed * 1.7f);
        var p = new CoderRigPose
        {
            PelvisY = .885f + .007f * breath,
            PelvisRoll = .04f,
            TorsoPitch = .05f,
            TorsoYaw = -.16f,
            TorsoRoll = -.04f,
            HeadPitch = .03f + .025f * breath,
            HeadYaw = .18f + .10f * MathF.Sin(clock * 1.1f + seed),
            HeadRoll = .04f,
            // Natural wide athletic stance — never glued-together doll feet!
            ThighLPitch = -.11f, ThighLRoll = .085f, ShinLPitch = .16f,
            ThighRPitch = .04f,  ThighRRoll = -.075f, ShinRPitch = .04f,
            ArmLPitch = .08f, ArmLRoll = .11f, ElbowLPitch = -.85f, ElbowLYaw = -.26f,
            ArmRPitch = -.06f, ArmRRoll = -.11f, ElbowRPitch = -.52f, ElbowRYaw = .16f,
        };

        switch (headIdx % 12)
        {
            case 0: // Zoomer Headphones — right hand adjusting earcup, left hand tucked by pocket
                p.TorsoYaw = -.22f; p.HeadYaw = .24f; p.HeadRoll = -.08f;
                p.ArmRPitch = -1.22f; p.ArmRRoll = -.48f; p.ElbowRPitch = -1.78f; p.ElbowRYaw = .38f;
                p.ArmLPitch = .14f; p.ArmLRoll = .16f; p.ElbowLPitch = -1.12f; p.ElbowLYaw = -.42f;
                break;
            case 1: // Hipster Beanie / Smoothie — relaxed lean holding imaginary smoothie cup up
                p.PelvisRoll = -.05f; p.TorsoYaw = .20f; p.TorsoRoll = .05f; p.HeadYaw = -.16f; p.HeadRoll = .07f;
                p.ArmLPitch = -.38f; p.ArmLRoll = .14f; p.ElbowLPitch = -1.52f + .05f * breath; p.ElbowLYaw = -.22f;
                p.ArmRPitch = .16f; p.ArmRRoll = -.18f; p.ElbowRPitch = -.88f; p.ElbowRYaw = .34f;
                break;
            case 2: // Backwards Cap Crypto-Bro — cocky wide stance, left hand on hip, chin up
                p.TorsoPitch = -.04f; p.TorsoYaw = -.24f; p.HeadPitch = -.09f; p.HeadYaw = .26f;
                p.ThighLRoll = .11f; p.ThighRRoll = -.10f;
                p.ArmLPitch = .22f; p.ArmLRoll = .52f; p.ElbowLPitch = -1.48f; p.ElbowLYaw = -.75f;
                p.ArmRPitch = -.24f; p.ArmRRoll = -.16f; p.ElbowRPitch = -1.05f; p.ElbowRYaw = .12f;
                break;
            case 3: // Tech-Wizard — mystic caster pose, right hand raised weaving prompt magic
                p.TorsoYaw = .22f; p.HeadYaw = -.18f; p.HeadPitch = -.06f;
                p.ArmRPitch = -1.28f + .06f * breath; p.ArmRRoll = -.26f; p.ElbowRPitch = -.88f; p.ElbowRYaw = .14f;
                p.ArmLPitch = -.18f; p.ArmLRoll = .18f; p.ElbowLPitch = -1.35f; p.ElbowLYaw = -.36f;
                break;
            case 4: // Call-Center TeamLead — left hand tapping boom-mic, right hand gesturing on Zoom call
                p.TorsoYaw = -.18f; p.HeadYaw = .15f; p.HeadRoll = .08f;
                p.ArmLPitch = -1.08f; p.ArmLRoll = .34f; p.ElbowLPitch = -1.86f; p.ElbowLYaw = -.44f;
                p.ArmRPitch = -.42f; p.ArmRRoll = -.22f; p.ElbowRPitch = -1.24f + .08f * breath; p.ElbowRYaw = .18f;
                break;
            case 5: // Punk Mohawk Berserk — wide fighter stance, fists up ready to refactor!
                p.PelvisY = .855f; p.TorsoPitch = .16f; p.TorsoYaw = -.28f; p.HeadYaw = .28f; p.HeadPitch = -.10f;
                p.ThighLPitch = -.22f; p.ThighLRoll = .13f; p.ShinLPitch = .34f;
                p.ThighRPitch = .10f; p.ThighRRoll = -.12f; p.ShinRPitch = .18f;
                p.ArmLPitch = -.62f; p.ArmLRoll = .24f; p.ElbowLPitch = -1.68f; p.ElbowLYaw = -.32f;
                p.ArmRPitch = -.88f; p.ArmRRoll = -.22f; p.ElbowRPitch = -1.62f; p.ElbowRYaw = .26f;
                break;
            case 6: // Zen Halo Guru — calm serene stance, hands brought together at chest (namaste) + subtle levitation
                p.PelvisY = .925f + .022f * breath;
                p.TorsoPitch = 0f; p.TorsoYaw = 0f; p.TorsoRoll = 0f; p.PelvisRoll = 0f;
                p.HeadPitch = .05f; p.HeadYaw = .06f * breath; p.HeadRoll = 0f;
                p.ThighLPitch = -.06f; p.ThighLRoll = .06f; p.ShinLPitch = .10f;
                p.ThighRPitch = -.06f; p.ThighRRoll = -.06f; p.ShinRPitch = .10f;
                p.ArmLPitch = -.52f; p.ArmLRoll = .26f; p.ElbowLPitch = -1.62f; p.ElbowLYaw = -.68f;
                p.ArmRPitch = -.52f; p.ArmRRoll = -.26f; p.ElbowRPitch = -1.62f; p.ElbowRYaw = .68f;
                break;
            case 7: // Skolkovo VR Neuro-Shaman — swiping invisible holographic UI in mid-air!
                p.TorsoPitch = .08f; p.TorsoYaw = .14f; p.HeadYaw = -.12f + .16f * breath; p.HeadPitch = -.06f;
                p.ArmLPitch = -.88f; p.ArmLRoll = .18f; p.ElbowLPitch = -1.08f + .10f * breath; p.ElbowLYaw = -.18f;
                p.ArmRPitch = -.72f; p.ArmRRoll = -.20f; p.ElbowRPitch = -1.22f - .10f * breath; p.ElbowRYaw = .22f;
                break;
            case 8: // Meetup Boss in Balenciaga — fashion runway swagger, arms folded coolly
                p.PelvisRoll = .07f; p.TorsoYaw = -.26f; p.TorsoRoll = -.06f; p.HeadYaw = .26f; p.HeadPitch = -.08f;
                p.ThighLPitch = -.15f; p.ThighLRoll = .10f; p.ShinLPitch = .22f;
                p.ThighRPitch = .05f; p.ThighRRoll = -.06f; p.ShinRPitch = .04f;
                p.ArmLPitch = -.40f; p.ArmLRoll = .24f; p.ElbowLPitch = -1.52f; p.ElbowLYaw = -.78f;
                p.ArmRPitch = -.34f; p.ArmRRoll = -.24f; p.ElbowRPitch = -1.46f; p.ElbowRYaw = .74f;
                break;
            case 9: // Friday 19:59 Devil Horns — mischievous evil mastermind hunch, rubbing hands
                p.PelvisY = .855f; p.TorsoPitch = .22f; p.TorsoYaw = .16f; p.HeadPitch = -.14f; p.HeadYaw = -.14f;
                p.ThighLPitch = -.18f; p.ThighLRoll = .11f; p.ShinLPitch = .28f;
                p.ThighRPitch = -.12f; p.ThighRRoll = -.11f; p.ShinRPitch = .22f;
                p.ArmLPitch = -.55f; p.ArmLRoll = .20f; p.ElbowLPitch = -1.65f + .08f * breath; p.ElbowLYaw = -.58f;
                p.ArmRPitch = -.55f; p.ArmRRoll = -.20f; p.ElbowRPitch = -1.65f - .08f * breath; p.ElbowRYaw = .58f;
                break;
            case 10: // Legacy CRT Monitor — curious retro head tilt, one hand on hip, one raised greeting
                p.TorsoYaw = .18f; p.HeadYaw = -.15f; p.HeadRoll = .15f + .04f * breath;
                p.ArmRPitch = .20f; p.ArmRRoll = -.48f; p.ElbowRPitch = -1.42f; p.ElbowRYaw = .72f;
                p.ArmLPitch = -.68f; p.ArmLRoll = .28f; p.ElbowLPitch = -1.35f; p.ElbowLYaw = -.12f;
                break;
            default: // 11: Security Chief / Toxic PM — broad intimidating arms-akimbo boss stance!
                p.TorsoPitch = -.03f; p.TorsoYaw = 0f; p.PelvisRoll = 0f; p.TorsoRoll = 0f; p.HeadYaw = .08f * breath;
                p.ThighLPitch = -.06f; p.ThighLRoll = .12f; p.ShinLPitch = .08f;
                p.ThighRPitch = -.06f; p.ThighRRoll = -.12f; p.ShinRPitch = .08f;
                p.ArmLPitch = .22f; p.ArmLRoll = .52f; p.ElbowLPitch = -1.48f; p.ElbowLYaw = -.78f;
                p.ArmRPitch = .22f; p.ArmRRoll = -.52f; p.ElbowRPitch = -1.48f; p.ElbowRYaw = .78f;
                break;
        }
        return p;
    }

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
        int visorSlot = model.MaterialCount - 2; // teal is always second-to-last right before coal across all 12 heads
        for (int i = 0; i < saved.Length; i++)
        {
            ref var pigment = ref model.Materials[i].Maps[0].Color;
            saved[i] = pigment;
            if (tintHoodie && i == 1) pigment = hoodie;
            else if (tintTrim && i == 2) pigment = trim;
            else if (tintVisor && i == visorSlot) pigment = visor;
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

    internal static void DrawSeatedCoder(Vector3 feet, float yawDeg, float scale, Color hoodie, Color visor, float workBob,
        string speciesId = "", int slotIndex = 0)
    {
        const string bodyAsset = "coder_seated_body";
        var model = imports[bodyAsset];
        var world = Matrix4x4.CreateRotationX(workBob)
            * Matrix4x4.CreateScale(scale)
            * Matrix4x4.CreateRotationY(yawDeg * MathF.PI / 180f)
            * Matrix4x4.CreateTranslation(feet);
        string headAsset = $"coder_head_{SpeciesHeadIndex(speciesId, slotIndex)}";
        // Head seated snugly into hoodie collar (zero gap) and pitched subtly toward dual monitors.
        Matrix4x4 headWorld = Matrix4x4.CreateRotationX(.18f)
            * Matrix4x4.CreateTranslation(0f, 1.018f, -.018f)
            * world;

        if (CastingShadows)
        {
            DrawDepth(model, world);
            DrawDepth(imports[headAsset], headWorld);
            return;
        }
        Surface(Finish.Anatomy, 0);
        Span<Color> saved = stackalloc Color[model.MaterialCount];
        Color trim = Raylib.ColorBrightness(hoodie, -.55f);
        // coder_seated_body materials: [0=default, 1=skin, 2=blue, 3=denim, 4=navy, 5=sole, 6=ivory, 7=coal]
        for (int i = 0; i < saved.Length; i++)
        {
            ref var pigment = ref model.Materials[i].Maps[0].Color;
            saved[i] = pigment;
            pigment = i switch { 2 => hoodie, 4 => trim, _ => pigment };
        }
        var pose = Matrix4x4.Transpose(world);
        for (int i = 0; i < model.MeshCount; i++)
            Raylib.DrawMesh(model.Meshes[i], model.Materials[model.MeshMaterial[i]], pose);
        for (int i = 0; i < saved.Length; i++)
            model.Materials[i].Maps[0].Color = saved[i];
        CapturedAssets?.Add(bodyAsset);

        DrawArticulatedPart(headAsset, headWorld, hoodie, trim, visor, false, false, true, 0f);
    }

    internal static void DrawCoder(VibeCoderEntity c, Vector3 feet, float extraTilt, Color tint, float clock = 0f)
    {
        var (wMul, hMul, shoeMul) = SpeciesProportions(c.SpeciesId);
        Vector3 bodyScale = new(c.ModelScale * wMul, c.ModelScale * hMul, c.ModelScale * wMul);
        int headIdx = SpeciesHeadIndex(c.SpeciesId, c.Id);
        float cyc = c.WalkCycle;
        float sinC = MathF.Sin(cyc), cosC = MathF.Cos(cyc);

        CoderRigPose p = new()
        {
            PelvisY = .90f,
            TorsoPitch = extraTilt * .55f,
            ArmLRoll = .08f, ElbowLPitch = -.35f,
            ArmRRoll = -.08f, ElbowRPitch = -.35f,
            ThighLRoll = .05f, ThighRRoll = -.05f,
        };

        bool knockedOut = c.IsCombatCoder && c.State == CoderAIState.Subdued;
        bool stunned = c.CanSignNda;
        bool fleeing = c.State == CoderAIState.PanickedFleeing && !stunned;
        bool sneaking = c.State == CoderAIState.SneakingToBait;
        bool lured = c.State == CoderAIState.LuredAtBait;
        bool walking = c.ExitingRoom || (!c.IsCombatCoder || c.Aggro) && c.State is CoderAIState.WalkingToBait or CoderAIState.CombatHostile;

        if (knockedOut)
        {
            p.RootTilt = -1.45f * Math.Clamp((c.KnockoutTime + .05f) * 4f, 0f, 1f);
            p.ArmLPitch = -.45f; p.ArmLRoll = .45f; p.ElbowLPitch = -.85f;
            p.ArmRPitch = -.35f; p.ArmRRoll = -.48f; p.ElbowRPitch = -.70f;
            p.ThighLPitch = -.12f; p.ShinLPitch = .25f;
            p.ThighRPitch = .08f; p.ShinRPitch = .18f;
        }
        else if (stunned)
        {
            p.PelvisY = .50f;
            float sway = MathF.Sin(clock * 3.4f + c.Id);
            p.TorsoPitch = .34f + .06f * MathF.Sin(clock * 2.6f + c.Id);
            p.TorsoRoll = .11f * sway;
            p.HeadPitch = .32f + .07f * MathF.Cos(clock * 3.8f + c.Id);
            p.HeadYaw = .22f * sway;
            p.HeadRoll = .16f * MathF.Cos(clock * 3.1f + c.Id);
            p.ThighLPitch = -.10f; p.ThighLRoll = .09f; p.ShinLPitch = 1.62f;
            p.ThighRPitch = -.06f; p.ThighRRoll = -.09f; p.ShinRPitch = 1.58f;
            p.ArmLPitch = -1.32f + .06f * sway; p.ArmLRoll = .44f; p.ElbowLPitch = -1.88f; p.ElbowLYaw = -.52f;
            p.ArmRPitch = -1.28f - .06f * sway; p.ArmRRoll = -.44f; p.ElbowRPitch = -1.86f; p.ElbowRYaw = .52f;
        }
        else if (fleeing)
        {
            bool dodging = c.DodgeCooldown > 1.0f;
            p.PelvisY = .85f + .050f * MathF.Abs(sinC) + (dodging ? .14f : 0f);
            p.TorsoPitch = .34f + extraTilt * .4f;
            p.TorsoYaw = -.22f * sinC;
            p.TorsoRoll = dodging ? (c.Id % 2 == 0 ? .28f : -.28f) : .08f * sinC;
            p.HeadPitch = -.16f;
            p.HeadYaw = .42f * MathF.Sin(clock * 4.5f + c.Id);
            p.ThighLPitch = -.78f * sinC - .12f; p.ThighLRoll = .06f;
            p.ShinLPitch = Math.Max(0f, .92f * MathF.Sin(cyc + .95f) + .36f);
            p.ThighRPitch = .78f * sinC - .12f; p.ThighRRoll = -.06f;
            p.ShinRPitch = Math.Max(0f, .92f * MathF.Sin(cyc + MathF.PI + .95f) + .36f);
            if (dodging)
            {
                p.ArmLPitch = -2.18f + .32f * sinC; p.ArmLRoll = .34f; p.ElbowLPitch = -.95f + .25f * cosC; p.ElbowLYaw = -.25f;
                p.ArmRPitch = -2.18f - .32f * sinC; p.ArmRRoll = -.34f; p.ElbowRPitch = -.95f - .25f * cosC; p.ElbowRYaw = .25f;
            }
            else
            {
                p.ArmLPitch = .72f * sinC - .42f; p.ArmLRoll = .18f; p.ElbowLPitch = -1.62f - .28f * sinC; p.ElbowLYaw = -.18f;
                p.ArmRPitch = -.72f * sinC - .42f; p.ArmRRoll = -.18f; p.ElbowRPitch = -1.62f + .28f * sinC; p.ElbowRYaw = .18f;
            }
        }
        else if (lured)
        {
            p.PelvisY = .56f;
            p.TorsoPitch = .50f + .035f * MathF.Sin(cyc * 1.4f);
            p.HeadPitch = .34f + .045f * MathF.Sin(cyc * 2.1f);
            p.HeadYaw = .09f * MathF.Sin(clock * 1.9f + c.Id);
            p.ThighLPitch = -1.16f; p.ThighLRoll = .12f; p.ShinLPitch = 1.66f;
            p.ThighRPitch = -1.10f; p.ThighRRoll = -.12f; p.ShinRPitch = 1.60f;
            p.ArmLPitch = -.48f; p.ArmLRoll = .12f; p.ElbowLPitch = -1.28f + .16f * MathF.Sin(clock * 13f + c.Id); p.ElbowLYaw = -.28f;
            p.ArmRPitch = -.48f; p.ArmRRoll = -.12f; p.ElbowRPitch = -1.28f - .16f * MathF.Sin(clock * 13f + c.Id + 1.4f); p.ElbowRYaw = .28f;
        }
        else if (sneaking)
        {
            p.PelvisY = .76f + .024f * MathF.Abs(sinC);
            p.TorsoPitch = .36f;
            p.TorsoYaw = -.14f * sinC;
            p.TorsoRoll = .05f * sinC;
            p.HeadPitch = -.14f;
            p.HeadYaw = .54f * MathF.Sin(clock * 2.2f + c.Id);
            p.ThighLPitch = -.52f - .34f * sinC; p.ThighLRoll = .08f;
            p.ShinLPitch = .68f + .28f * Math.Max(0f, -cosC);
            p.ThighRPitch = -.52f + .34f * sinC; p.ThighRRoll = -.08f;
            p.ShinRPitch = .68f + .28f * Math.Max(0f, cosC);
            p.ArmLPitch = .22f + .12f * sinC; p.ArmLRoll = .14f; p.ElbowLPitch = -1.82f + .14f * cosC; p.ElbowLYaw = -.34f;
            p.ArmRPitch = .16f - .12f * sinC; p.ArmRRoll = -.14f; p.ElbowRPitch = -1.72f - .14f * cosC; p.ElbowRYaw = .34f;
        }
        else if (walking)
        {
            p.PelvisY = .89f + .025f * MathF.Abs(sinC);
            p.TorsoPitch = .11f + extraTilt * .5f;
            p.TorsoYaw = -.12f * sinC;
            p.ThighLPitch = -.52f * sinC; p.ThighLRoll = .05f;
            p.ShinLPitch = Math.Max(0f, .58f * MathF.Sin(cyc + .85f) + .14f);
            p.ThighRPitch = .52f * sinC; p.ThighRRoll = -.05f;
            p.ShinRPitch = Math.Max(0f, .58f * MathF.Sin(cyc + MathF.PI + .85f) + .14f);
            p.ArmLPitch = .46f * sinC - .06f; p.ArmLRoll = .09f; p.ElbowLPitch = -.58f - .25f * Math.Max(0f, -sinC); p.ElbowLYaw = -.08f;
            p.ArmRPitch = -.46f * sinC - .06f; p.ArmRRoll = -.09f; p.ElbowRPitch = -.58f - .25f * Math.Max(0f, sinC); p.ElbowRYaw = .08f;
        }
        else
        {
            p = PersonalityIdlePose(headIdx, clock, c.Id);
        }

        if (c.HitFlinchTimer > 0f && !knockedOut)
        {
            float f = Math.Clamp(c.HitFlinchTimer / .22f, 0f, 1f);
            p.TorsoPitch -= .46f * f;
            p.HeadPitch -= .34f * f;
            p.ArmLPitch -= .62f * f; p.ArmLRoll += .34f * f; p.ElbowLPitch -= .45f * f;
            p.ArmRPitch -= .62f * f; p.ArmRRoll -= .34f * f; p.ElbowRPitch -= .45f * f;
        }

        if (c.IsCombatCoder && c.Windup > 0f && !knockedOut)
        {
            float lift = 1f - c.Windup / (c.WindingSlap ? .38f : .58f);
            p.ArmRPitch = -1.45f - .55f * lift;
            p.ArmRRoll = -.28f;
            p.ElbowRPitch = -1.25f;
            p.TorsoYaw = -.32f * lift;
        }

        Color hoodie = c.HoodieColor.A > 0 ? c.HoodieColor : new Color(92, 138, 214, 255);
        if (c.HitFlashTimer > 0f) hoodie = Raylib.ColorLerp(hoodie, Color.White, .55f);
        Color trim = Raylib.ColorBrightness(hoodie, -.55f);
        Color visor = c.GlassesColor.A > 0 ? c.GlassesColor : new Color(92, 232, 224, 255);
        float flash = Math.Clamp(c.HitFlashTimer / .14f, 0f, 1f) * .35f;
        string headAsset = $"coder_head_{headIdx}";

        Matrix4x4 root = Matrix4x4.CreateRotationX(p.RootTilt)
            * Matrix4x4.CreateScale(bodyScale)
            * Matrix4x4.CreateRotationY(c.FacingYaw)
            * Matrix4x4.CreateTranslation(feet);

        Matrix4x4 pelvis = Matrix4x4.CreateRotationZ(p.PelvisRoll)
            * Matrix4x4.CreateTranslation(0f, p.PelvisY, 0f) * root;
        Matrix4x4 torso = Matrix4x4.CreateFromYawPitchRoll(p.TorsoYaw, p.TorsoPitch, p.TorsoRoll) * pelvis;
        Matrix4x4 head = Matrix4x4.CreateFromYawPitchRoll(p.HeadYaw, p.HeadPitch, p.HeadRoll)
            * Matrix4x4.CreateTranslation(0f, .54f, 0f) * torso;

        Matrix4x4 upperArmL = Matrix4x4.CreateFromYawPitchRoll(0f, p.ArmLPitch, p.ArmLRoll)
            * Matrix4x4.CreateTranslation(.168f, .432f, 0f) * torso;
        Matrix4x4 forearmL = Matrix4x4.CreateFromYawPitchRoll(p.ElbowLYaw, p.ElbowLPitch, 0f)
            * Matrix4x4.CreateTranslation(.014f, -.255f, .004f) * upperArmL;

        Matrix4x4 upperArmR = Matrix4x4.CreateFromYawPitchRoll(0f, p.ArmRPitch, p.ArmRRoll)
            * Matrix4x4.CreateTranslation(-.168f, .432f, 0f) * torso;
        Matrix4x4 forearmR = Matrix4x4.CreateFromYawPitchRoll(p.ElbowRYaw, p.ElbowRPitch, 0f)
            * Matrix4x4.CreateTranslation(-.014f, -.255f, .004f) * upperArmR;

        Matrix4x4 thighL = Matrix4x4.CreateFromYawPitchRoll(0f, p.ThighLPitch, p.ThighLRoll)
            * Matrix4x4.CreateTranslation(.090f, 0f, 0f) * pelvis;
        Matrix4x4 shinL = Matrix4x4.CreateScale(shoeMul, 1f, shoeMul)
            * Matrix4x4.CreateFromYawPitchRoll(0f, p.ShinLPitch, -p.ThighLRoll * .45f)
            * Matrix4x4.CreateTranslation(0f, -.42f, 0f) * thighL;

        Matrix4x4 thighR = Matrix4x4.CreateFromYawPitchRoll(0f, p.ThighRPitch, p.ThighRRoll)
            * Matrix4x4.CreateTranslation(-.090f, 0f, 0f) * pelvis;
        Matrix4x4 shinR = Matrix4x4.CreateScale(shoeMul, 1f, shoeMul)
            * Matrix4x4.CreateFromYawPitchRoll(0f, p.ShinRPitch, -p.ThighRRoll * .45f)
            * Matrix4x4.CreateTranslation(0f, -.42f, 0f) * thighR;

        DrawArticulatedPart("coder_torso", torso, hoodie, trim, visor, true, true, false, flash);
        DrawArticulatedPart(headAsset, head, hoodie, trim, visor, false, false, true, flash);
        DrawArticulatedPart("coder_upper_arm_l", upperArmL, hoodie, trim, visor, true, false, false, flash);
        DrawArticulatedPart("coder_forearm_l", forearmL, hoodie, trim, visor, true, true, false, flash);
        DrawArticulatedPart("coder_upper_arm_r", upperArmR, hoodie, trim, visor, true, false, false, flash);
        DrawArticulatedPart("coder_forearm_r", forearmR, hoodie, trim, visor, true, true, false, flash);
        DrawArticulatedPart("coder_thigh_l", thighL, hoodie, trim, visor, false, false, false, 0f);
        DrawArticulatedPart("coder_shin_l", shinL, hoodie, trim, visor, false, true, false, 0f);
        DrawArticulatedPart("coder_thigh_r", thighR, hoodie, trim, visor, false, false, false, 0f);
        DrawArticulatedPart("coder_shin_r", shinR, hoodie, trim, visor, false, true, false, 0f);
    }
}
