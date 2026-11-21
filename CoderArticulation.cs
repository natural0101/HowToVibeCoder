using System.Numerics;
using System.Text.Json;
using Raylib_cs;

namespace HowToVibeCoder;

internal static unsafe partial class VisualEngine3D
{
    private static readonly Dictionary<string, (int Start, int Count)[]> coderParts = new();

    private static void ReadCoderParts(string path, string asset, int meshCount)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        reader.BaseStream.Position = 12;
        int length = reader.ReadInt32();
        if (reader.ReadUInt32() != 0x4E4F534A) throw new InvalidDataException(path);
        using var json = JsonDocument.Parse(reader.ReadBytes(length));
        var ranges = new (int Start, int Count)[10];
        int index = 0, matched = 0;
        foreach (var mesh in json.RootElement.GetProperty("meshes").EnumerateArray())
        {
            int count = mesh.GetProperty("primitives").GetArrayLength();
            int slot = (mesh.GetProperty("name").GetString() ?? "") switch
            {
                "Torso_Mesh"     => 0,
                "Head_Mesh"      => 1,
                "UpperArmL_Mesh" => 2,
                "ForearmL_Mesh"  => 3,
                "UpperArmR_Mesh" => 4,
                "ForearmR_Mesh"  => 5,
                "ThighL_Mesh"    => 6,
                "ShinL_Mesh"     => 7,
                "ThighR_Mesh"    => 8,
                "ShinR_Mesh"     => 9,
                _ => -1,
            };
            if (slot >= 0) { ranges[slot] = (index, count); matched++; }
            index += count;
        }
        if (index != meshCount) throw new InvalidDataException($"Incomplete mesh groups: {path}");
        if (matched == 10) coderParts[asset] = ranges;
        else if (asset.StartsWith("vc_", StringComparison.Ordinal))
            throw new InvalidDataException($"Incomplete Vibe Coder mesh groups ({matched}/10): {path}");
    }

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
        "div" or "smoothie" => (.95f, 1.01f, 1.00f),
        "refactor" or "sb_chief" => (1.06f, 1.04f, 1.04f),
        "meetup" => (1.02f, 1.03f, 1.15f),
        "no-commits" or "skolkovo" => (1.01f, 1.03f, 1.00f),
        "friday" or "legacy" => (1.03f, 1.04f, 1.02f),
        "prompt" or "framework" => (1.00f, 1.02f, 1.00f),
        _ => (1.00f, 1.00f, 1.00f),
    };

    internal struct CoderRigPose
    {
        public float PelvisY, PelvisZ, PelvisRoll, RootTilt;
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
            PelvisY = .90f + .007f * breath,
            PelvisRoll = .035f,
            TorsoPitch = .04f,
            TorsoYaw = -.15f,
            TorsoRoll = -.035f,
            HeadPitch = .02f + .02f * breath,
            HeadYaw = .16f + .09f * MathF.Sin(clock * 1.1f + seed),
            HeadRoll = .04f,
            ThighLPitch = -.10f, ThighLRoll = .08f, ShinLPitch = .14f,
            ThighRPitch = .04f,  ThighRRoll = -.07f, ShinRPitch = .04f,
            ArmLPitch = .06f, ArmLRoll = .12f, ElbowLPitch = -.80f, ElbowLYaw = -.24f,
            ArmRPitch = -.05f, ArmRRoll = -.12f, ElbowRPitch = -.52f, ElbowRYaw = .16f,
        };

        switch (headIdx % 12)
        {
            case 0: // Vibe-Pirate: brandishing forged steel hook + wooden peg-leg stepped forward!
                p.TorsoYaw = -.22f; p.HeadYaw = .24f; p.HeadRoll = -.06f;
                p.ThighRPitch = -.18f; p.ThighRRoll = -.10f; p.ShinRPitch = .18f;
                p.ArmRPitch = -.92f + .06f * breath; p.ArmRRoll = -.34f; p.ElbowRPitch = -1.25f; p.ElbowRYaw = .18f;
                p.ArmLPitch = .18f; p.ArmLRoll = .44f; p.ElbowLPitch = -1.35f; p.ElbowLYaw = -.68f;
                break;
            case 1: // Smoothie Maniac: holding iced matcha cup up with caffeinated tremble
                p.PelvisRoll = -.04f; p.TorsoYaw = .18f; p.TorsoRoll = .04f; p.HeadYaw = -.15f; p.HeadRoll = .06f;
                p.ArmLPitch = -.42f; p.ArmLRoll = .16f; p.ElbowLPitch = -1.48f + .06f * breath; p.ElbowLYaw = -.18f;
                p.ArmRPitch = .14f; p.ArmRRoll = -.18f; p.ElbowRPitch = -.90f; p.ElbowRYaw = .28f;
                break;
            case 2: // Tinfoil Crypto-Bro: cocky wide stance, hand on hip, chin up
                p.TorsoPitch = -.04f; p.TorsoYaw = -.22f; p.HeadPitch = -.08f; p.HeadYaw = .24f;
                p.ThighLRoll = .10f; p.ThighRRoll = -.09f;
                p.ArmLPitch = .20f; p.ArmLRoll = .48f; p.ElbowLPitch = -1.42f; p.ElbowLYaw = -.72f;
                p.ArmRPitch = -.22f; p.ArmRRoll = -.16f; p.ElbowRPitch = -1.02f; p.ElbowRYaw = .12f;
                break;
            case 3: // 40-Framework Wizard: mystic pose holding glowing crystal oak staff forward
                p.TorsoYaw = .20f; p.HeadYaw = -.16f; p.HeadPitch = -.05f;
                p.ArmRPitch = -.52f + .04f * breath; p.ArmRRoll = -.22f; p.ElbowRPitch = -1.12f; p.ElbowRYaw = .10f;
                p.ArmLPitch = -.24f; p.ArmLRoll = .20f; p.ElbowLPitch = -1.35f; p.ElbowLYaw = -.34f;
                break;
            case 4: // Sleepy TeamLead: hand near boom-mic on Zoom call, drowsy tilt
                p.TorsoYaw = -.16f; p.HeadYaw = .14f; p.HeadRoll = .10f;
                p.ArmLPitch = -1.05f; p.ArmLRoll = .32f; p.ElbowLPitch = -1.82f; p.ElbowLYaw = -.42f;
                p.ArmRPitch = -.38f; p.ArmRRoll = -.20f; p.ElbowRPitch = -1.18f + .07f * breath; p.ElbowRYaw = .18f;
                break;
            case 5: // Punk Cyborg Refactorer: wide fighter stance, cyber-claw raised!
                p.PelvisY = .865f; p.TorsoPitch = .14f; p.TorsoYaw = -.26f; p.HeadYaw = .26f; p.HeadPitch = -.08f;
                p.ThighLPitch = -.20f; p.ThighLRoll = .12f; p.ShinLPitch = .30f;
                p.ThighRPitch = .10f; p.ThighRRoll = -.11f; p.ShinRPitch = .16f;
                p.ArmLPitch = -.58f; p.ArmLRoll = .24f; p.ElbowLPitch = -1.62f; p.ElbowLYaw = -.30f;
                p.ArmRPitch = -.92f; p.ArmRRoll = -.24f; p.ElbowRPitch = -1.52f; p.ElbowRYaw = .24f;
                break;
            case 6: // Zen Monk Halo: serene hands together at chest + gentle levitation
                p.PelvisY = .94f + .024f * breath;
                p.TorsoPitch = 0f; p.TorsoYaw = 0f; p.TorsoRoll = 0f; p.PelvisRoll = 0f;
                p.HeadPitch = .04f; p.HeadYaw = .05f * breath; p.HeadRoll = 0f;
                p.ThighLPitch = -.05f; p.ThighLRoll = .06f; p.ShinLPitch = .08f;
                p.ThighRPitch = -.05f; p.ThighRRoll = -.06f; p.ShinRPitch = .08f;
                p.ArmLPitch = -.50f; p.ArmLRoll = .24f; p.ElbowLPitch = -1.58f; p.ElbowLYaw = -.64f;
                p.ArmRPitch = -.50f; p.ArmRRoll = -.24f; p.ElbowRPitch = -1.58f; p.ElbowRYaw = .64f;
                break;
            case 7: // Skolkovo VR Shaman: pinching holographic UI in mid-air
                p.TorsoPitch = .07f; p.TorsoYaw = .14f; p.HeadYaw = -.12f + .15f * breath; p.HeadPitch = -.05f;
                p.ArmLPitch = -.86f; p.ArmLRoll = .18f; p.ElbowLPitch = -1.06f + .10f * breath; p.ElbowLYaw = -.18f;
                p.ArmRPitch = -.70f; p.ArmRRoll = -.20f; p.ElbowRPitch = -1.20f - .10f * breath; p.ElbowRYaw = .22f;
                break;
            case 8: // Meetup Crown Boss: swagger runway tilt, arms folded
                p.PelvisRoll = .06f; p.TorsoYaw = -.24f; p.TorsoRoll = -.05f; p.HeadYaw = .24f; p.HeadPitch = -.07f;
                p.ThighLPitch = -.14f; p.ThighLRoll = .10f; p.ShinLPitch = .20f;
                p.ThighRPitch = .05f; p.ThighRRoll = -.06f; p.ShinRPitch = .04f;
                p.ArmLPitch = -.38f; p.ArmLRoll = .24f; p.ElbowLPitch = -1.48f; p.ElbowLYaw = -.74f;
                p.ArmRPitch = -.32f; p.ArmRRoll = -.24f; p.ElbowRPitch = -1.42f; p.ElbowRYaw = .70f;
                break;
            case 9: // Friday 19:59 Prod Demon: wicked hunch rubbing hands together
                p.PelvisY = .87f; p.TorsoPitch = .20f; p.TorsoYaw = .15f; p.HeadPitch = -.12f; p.HeadYaw = -.12f;
                p.ThighLPitch = -.16f; p.ThighLRoll = .10f; p.ShinLPitch = .24f;
                p.ThighRPitch = -.10f; p.ThighRRoll = -.10f; p.ShinRPitch = .20f;
                p.ArmLPitch = -.52f; p.ArmLRoll = .20f; p.ElbowLPitch = -1.62f + .08f * breath; p.ElbowLYaw = -.55f;
                p.ArmRPitch = -.52f; p.ArmRRoll = -.20f; p.ElbowRPitch = -1.62f - .08f * breath; p.ElbowRYaw = .55f;
                break;
            case 10: // Legacy 1998 CRT: curious retro head tilt, hand on hip
                p.TorsoYaw = .16f; p.HeadYaw = -.14f; p.HeadRoll = .14f + .04f * breath;
                p.ArmRPitch = .18f; p.ArmRRoll = -.46f; p.ElbowRPitch = -1.38f; p.ElbowRYaw = .70f;
                p.ArmLPitch = -.64f; p.ArmLRoll = .26f; p.ElbowLPitch = -1.32f; p.ElbowLYaw = -.12f;
                break;
            default: // 11: Tower Security Chief: intimidating arms-akimbo boss stance
                p.TorsoPitch = -.03f; p.TorsoYaw = 0f; p.PelvisRoll = 0f; p.TorsoRoll = 0f; p.HeadYaw = .08f * breath;
                p.ThighLPitch = -.06f; p.ThighLRoll = .11f; p.ShinLPitch = .08f;
                p.ThighRPitch = -.06f; p.ThighRRoll = -.11f; p.ShinRPitch = .08f;
                p.ArmLPitch = .20f; p.ArmLRoll = .48f; p.ElbowLPitch = -1.44f; p.ElbowLYaw = -.74f;
                p.ArmRPitch = .20f; p.ArmRRoll = -.48f; p.ElbowRPitch = -1.44f; p.ElbowRYaw = .74f;
                break;
        }
        return p;
    }

    private static void DrawRiggedCoder(int headIdx, Matrix4x4 root, in CoderRigPose p, float shoeMul, Shader? overrideShader = null)
    {
        string asset = $"vc_{headIdx}";
        Model model = imports[asset];
        var ranges = coderParts[asset];

        Matrix4x4 pelvis = Matrix4x4.CreateRotationZ(p.PelvisRoll)
            * Matrix4x4.CreateTranslation(0f, p.PelvisY, p.PelvisZ) * root;
        Matrix4x4 torso = Matrix4x4.CreateFromYawPitchRoll(p.TorsoYaw, p.TorsoPitch, p.TorsoRoll) * pelvis;
        Matrix4x4 head = Matrix4x4.CreateFromYawPitchRoll(p.HeadYaw, p.HeadPitch, p.HeadRoll)
            * Matrix4x4.CreateTranslation(0f, .540f, 0f) * torso;

        Matrix4x4 upperArmL = Matrix4x4.CreateFromYawPitchRoll(0f, p.ArmLPitch, p.ArmLRoll)
            * Matrix4x4.CreateTranslation(.180f, .440f, 0f) * torso;
        Matrix4x4 forearmL = Matrix4x4.CreateFromYawPitchRoll(p.ElbowLYaw, p.ElbowLPitch, 0f)
            * Matrix4x4.CreateTranslation(0f, -.260f, 0f) * upperArmL;

        Matrix4x4 upperArmR = Matrix4x4.CreateFromYawPitchRoll(0f, p.ArmRPitch, p.ArmRRoll)
            * Matrix4x4.CreateTranslation(-.180f, .440f, 0f) * torso;
        Matrix4x4 forearmR = Matrix4x4.CreateFromYawPitchRoll(p.ElbowRYaw, p.ElbowRPitch, 0f)
            * Matrix4x4.CreateTranslation(0f, -.260f, 0f) * upperArmR;

        Matrix4x4 thighL = Matrix4x4.CreateFromYawPitchRoll(0f, p.ThighLPitch, p.ThighLRoll)
            * Matrix4x4.CreateTranslation(.098f, 0f, 0f) * pelvis;
        Matrix4x4 shinL = Matrix4x4.CreateScale(shoeMul, 1f, shoeMul)
            * Matrix4x4.CreateFromYawPitchRoll(0f, p.ShinLPitch, -p.ThighLRoll * .45f)
            * Matrix4x4.CreateTranslation(0f, -.420f, 0f) * thighL;

        Matrix4x4 thighR = Matrix4x4.CreateFromYawPitchRoll(0f, p.ThighRPitch, p.ThighRRoll)
            * Matrix4x4.CreateTranslation(-.098f, 0f, 0f) * pelvis;
        Matrix4x4 shinR = Matrix4x4.CreateScale(shoeMul, 1f, shoeMul)
            * Matrix4x4.CreateFromYawPitchRoll(0f, p.ShinRPitch, -p.ThighRRoll * .45f)
            * Matrix4x4.CreateTranslation(0f, -.420f, 0f) * thighR;

        ReadOnlySpan<Matrix4x4> partMatrices = [
            torso, head, upperArmL, forearmL, upperArmR, forearmR, thighL, shinL, thighR, shinR
        ];

        if (CastingShadows && overrideShader == null)
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

        if (overrideShader == null) Surface(Finish.Anatomy, 0);
        for (int part = 0; part < 10; part++)
        {
            var (start, count) = ranges[part];
            var matT = Matrix4x4.Transpose(partMatrices[part]);
            for (int i = start; i < start + count; i++)
            {
                Material mat = model.Materials[model.MeshMaterial[i]];
                if (overrideShader.HasValue) mat.Shader = overrideShader.Value;
                Raylib.DrawMesh(model.Meshes[i], mat, matT);
            }
        }
        CapturedAssets?.Add(asset);
    }

    internal static void DrawArchiveVibeCoder(string speciesId, Matrix4x4 transform, Shader dossierShader)
    {
        int headIdx = SpeciesHeadIndex(speciesId, 0);
        var (wMul, hMul, shoeMul) = SpeciesProportions(speciesId);
        var p = PersonalityIdlePose(headIdx, (float)Raylib.GetTime(), headIdx);
        Matrix4x4 root = Matrix4x4.CreateScale(wMul, hMul, wMul) * transform;
        DrawRiggedCoder(headIdx, root, p, shoeMul, dossierShader);
    }

    internal static void DrawSeatedCoder(Vector3 feet, float yawDeg, float scale, Color hoodie, Color visor, float workBob,
        string speciesId = "", int slotIndex = 0)
    {
        int headIdx = SpeciesHeadIndex(speciesId, slotIndex);
        float t = (float)Raylib.GetTime();
        float typeL = .08f * MathF.Sin(t * 14f + slotIndex * 1.9f);
        float typeR = .08f * MathF.Sin(t * 14f + slotIndex * 1.9f + 1.6f);
        var p = new CoderRigPose
        {
            PelvisY = .52f,
            PelvisZ = -.14f,
            TorsoPitch = .16f + workBob,
            HeadPitch = .16f,
            HeadYaw = .06f * MathF.Sin(t * 1.4f + slotIndex),
            ArmLPitch = -.86f, ArmLRoll = .10f, ElbowLPitch = -1.08f + typeL, ElbowLYaw = -.22f,
            ArmRPitch = -.86f, ArmRRoll = -.10f, ElbowRPitch = -1.08f + typeR, ElbowRYaw = .22f,
            ThighLPitch = -1.48f, ThighLRoll = .08f, ShinLPitch = 1.48f,
            ThighRPitch = -1.48f, ThighRRoll = -.08f, ShinRPitch = 1.48f,
        };
        Matrix4x4 root = Matrix4x4.CreateScale(scale)
            * Matrix4x4.CreateRotationY(yawDeg * MathF.PI / 180f)
            * Matrix4x4.CreateTranslation(feet);
        DrawRiggedCoder(headIdx, root, p, 1.0f);
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
            ArmLRoll = .09f, ElbowLPitch = -.35f,
            ArmRRoll = -.09f, ElbowRPitch = -.35f,
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
            bool combatRush = c.IsCombatCoder && c.State == CoderAIState.CombatHostile;
            p.PelvisY = (combatRush ? .86f : .89f) + (combatRush ? .038f : .025f) * MathF.Abs(sinC);
            p.TorsoPitch = (combatRush ? .22f : .11f) + extraTilt * .5f;
            p.TorsoYaw = (combatRush ? -.19f : -.12f) * sinC;
            p.TorsoRoll = combatRush ? .06f * sinC : 0f;
            p.ThighLPitch = (combatRush ? -.66f : -.52f) * sinC - (combatRush ? .08f : 0f); p.ThighLRoll = combatRush ? .07f : .05f;
            p.ShinLPitch = Math.Max(0f, (combatRush ? .76f : .58f) * MathF.Sin(cyc + .85f) + (combatRush ? .22f : .14f));
            p.ThighRPitch = (combatRush ? .66f : .52f) * sinC - (combatRush ? .08f : 0f); p.ThighRRoll = combatRush ? -.07f : -.05f;
            p.ShinRPitch = Math.Max(0f, (combatRush ? .76f : .58f) * MathF.Sin(cyc + MathF.PI + .85f) + (combatRush ? .22f : .14f));
            if (combatRush)
            {
                p.ArmLPitch = .58f * sinC - .32f; p.ArmLRoll = .16f; p.ElbowLPitch = -1.24f - .22f * Math.Max(0f, -sinC); p.ElbowLYaw = -.18f;
                p.ArmRPitch = -.58f * sinC - .32f; p.ArmRRoll = -.16f; p.ElbowRPitch = -1.24f - .22f * Math.Max(0f, sinC); p.ElbowRYaw = .18f;
            }
            else
            {
                p.ArmLPitch = .46f * sinC - .06f; p.ArmLRoll = .09f; p.ElbowLPitch = -.58f - .25f * Math.Max(0f, -sinC); p.ElbowLYaw = -.08f;
                p.ArmRPitch = -.46f * sinC - .06f; p.ArmRRoll = -.09f; p.ElbowRPitch = -.58f - .25f * Math.Max(0f, sinC); p.ElbowRYaw = .08f;
            }
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
            float lift = Math.Clamp(1f - c.Windup / (c.WindingSlap ? .38f : .58f), 0f, 1f);
            p.ArmRPitch = -1.52f - .62f * lift;
            p.ArmRRoll = -.34f;
            p.ElbowRPitch = -1.28f;
            p.ArmLPitch = -.86f * lift;
            p.ArmLRoll = .20f;
            p.ElbowLPitch = -.52f;
            p.TorsoYaw = -.42f * lift;
            p.TorsoPitch = -.06f * lift;
            p.HeadYaw = .36f * lift;
        }

        Matrix4x4 root = Matrix4x4.CreateRotationX(p.RootTilt)
            * Matrix4x4.CreateScale(bodyScale)
            * Matrix4x4.CreateRotationY(c.FacingYaw)
            * Matrix4x4.CreateTranslation(feet);

        DrawRiggedCoder(headIdx, root, p, shoeMul);
    }
}
