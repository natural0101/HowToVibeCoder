#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

/// <summary>
/// Real-time in-engine 3D 5-shot cinema director for the North Elevator boss reveal.
/// Purely synchronized to <see cref="SkyscraperFloor1World.ElevatorBoss"/> Repairing (2.3s)
/// and IntroSpeech (2.4s sim / ~8.9s live) timers so gameplay, save/load snapshots, and autotest captures stay frame-locked.
/// </summary>
internal static class BossIntroCutscene
{
    // Normalized Repairing timeline r = RepairElapsed / RepairSeconds (0..1)
    private const float Shot1EndR = 0.46f; // Shot 1 (0.00..0.46): tight Dutch-angle sparking panel + Floor 01 dial
                                           // Shot 2 (0.46..1.00): slow ominous low push-in on defrosting doors

    // Normalized IntroSpeech timeline u = IntroElapsed / IntroSeconds (0..1)
    private const float Shot3EndU = 0.44f; // Shot 3 (0.00..0.44): intimate close-up portrait as doors open & helmet tilts
    private const float Shot4EndU = 0.82f; // Shot 4 (0.44..0.82): low-angle 3/4 weapon-side hero tracking orbit + 2.95m uncoil
                                           // Shot 5 (0.82..1.00): whip-smooth pull-back into player's 1st-person eyes

    private const float DialogueBeat1EndU = 0.24f;
    private const float DialogueBeat3EndU = 0.68f;

    private const float BarHeight235 = 88f; // Pure matte-black 2.35:1 cinema aperture inside 1280x720 virtual UI space

    private static bool _skipped;

    internal static void Reset() => _skipped = false;

    internal static void Skip() => _skipped = true;

    internal static void SyncState(SkyscraperFloor1World world)
    {
        var b = world.ElevatorBoss;
        if (!b.IsActive || (b.State != ElevatorBossState.Repairing && b.State != ElevatorBossState.IntroSpeech))
            _skipped = false;
    }

    internal static bool IsActive(SkyscraperFloor1World world)
    {
        if (_skipped) return false;
        var b = world.ElevatorBoss;
        return b.IsActive && (b.State == ElevatorBossState.Repairing || b.State == ElevatorBossState.IntroSpeech);
    }

    internal static bool IsCameraOverrideActive(SkyscraperFloor1World world) => IsActive(world);

    internal static bool ShouldSuppressViewModel(SkyscraperFloor1World world)
    {
        if (!IsActive(world)) return false;
        var b = world.ElevatorBoss;
        if (b.State == ElevatorBossState.Repairing) return true;
        float u = IntroProgress(b);
        if (u <= Shot4EndU) return true;
        float pullBack = SmoothStep((u - Shot4EndU) / (1f - Shot4EndU));
        return pullBack < 0.82f;
    }

    internal static float GetHudAlpha(SkyscraperFloor1World world)
    {
        if (!IsActive(world)) return 1f;
        var b = world.ElevatorBoss;
        if (b.State == ElevatorBossState.Repairing) return 0f;
        float u = IntroProgress(b);
        if (u <= Shot4EndU) return 0f;
        return SmoothStep((u - Shot4EndU) / (1f - Shot4EndU));
    }

    internal static float GetLetterboxProgress(SkyscraperFloor1World world)
    {
        if (!IsActive(world)) return 0f;
        var b = world.ElevatorBoss;
        if (b.State == ElevatorBossState.Repairing)
            return SmoothStep(RepairElapsed(b) / 0.34f);

        float u = IntroProgress(b);
        if (u <= Shot4EndU) return 1f;
        return 1f - SmoothStep((u - Shot4EndU) / (1f - Shot4EndU));
    }

    internal static Camera3D ResolveCamera(SkyscraperFloor1World world, Camera3D playerCam)
    {
        if (!IsActive(world)) return playerCam;
        var b = world.ElevatorBoss;

        if (b.State == ElevatorBossState.Repairing)
        {
            float elapsed = RepairElapsed(b);
            float r = RepairProgress(b);

            if (r < Shot1EndR)
            {
                // Shot 1 (r = 0.00..0.46): Tight Dutch-angle close-up on sparking brass shaft panel & Floor 01 dial.
                float t = SmoothStep(r / Shot1EndR);
                Vector3 pos = Vector3.Lerp(new(-2.14f, 1.48f, -10.82f), new(-1.66f, 1.78f, -11.10f), t);
                Vector3 tgt = Vector3.Lerp(new(-1.42f, 1.44f, -8.85f), new(-0.22f, 2.24f, -8.80f), t);

                // Crisp electrical tremor synchronized to live panel spark bursts.
                float sparkBurst = (elapsed % 0.38f) < 0.22f ? 1f : 0f;
                float tremor = sparkBurst * 0.010f;
                pos += new Vector3(MathF.Sin(elapsed * 59f) * tremor, MathF.Cos(elapsed * 73f) * tremor, 0f);

                float dutchRoll = LerpF(-0.054f, -0.046f, t);
                Vector3 fwd = Vector3.Normalize(tgt - pos);
                Vector3 up = Vector3.Transform(Vector3.UnitY, Quaternion.CreateFromAxisAngle(fwd, dutchRoll));

                return new Camera3D
                {
                    Position = pos,
                    Target = tgt,
                    Up = up,
                    FovY = 45f,
                    Projection = CameraProjection.Perspective
                };
            }
            else
            {
                // Shot 2 (r = 0.46..1.00): Slow ominous low push-in on defrosting smoked-glass Art-Deco elevator doors.
                float t = SmoothStep((r - Shot1EndR) / (1f - Shot1EndR));
                Vector3 pos = Vector3.Lerp(new(0.28f, 1.34f, -12.95f), new(0.10f, 1.44f, -11.55f), t);
                Vector3 tgt = Vector3.Lerp(new(0.00f, 1.44f, -7.65f), new(0.00f, 1.54f, -7.55f), t);

                // Heavy bass thud tremor + quarantine lock-release rumble as crimson visor pierces clearing glass.
                float thud = MathF.Pow(MathF.Max(0f, MathF.Sin((r - Shot1EndR) * 22f)), 4f);
                float rumble = SmoothStep((t - 0.35f) / 0.65f) * 0.011f + thud * 0.014f;
                pos += new Vector3(MathF.Sin(elapsed * 43f) * rumble, MathF.Cos(elapsed * 53f) * rumble * 0.8f, 0f);

                return new Camera3D
                {
                    Position = pos,
                    Target = tgt,
                    Up = Vector3.UnitY,
                    FovY = LerpF(45f, 38f, t),
                    Projection = CameraProjection.Perspective
                };
            }
        }
        else
        {
            float u = IntroProgress(b);

            if (u < Shot3EndU)
            {
                // Shot 3 (u = 0.00..0.44): Intimate medium close-up portrait framing full tilted helmet, glowing crimson visor & chest core
                // while SYSTEM ZERO ducks under lintel, plants titanium boot, and tilts helmet curiously.
                float t = SmoothStep(u / Shot3EndU);
                Vector3 pos = Vector3.Lerp(new(-0.92f, 1.48f, -12.15f), new(-0.72f, 1.56f, -11.75f), t);
                Vector3 tgt = b.Position + new Vector3(LerpF(0.00f, 0.04f, t), LerpF(1.76f, 1.82f, t), -0.15f);

                float strideActive = SmoothStep((u - 0.14f) / 0.12f);
                float footImpact = MathF.Pow(MathF.Abs(MathF.Sin(b.StridePhase)), 8f) * strideActive;
                float thudY = -0.014f * footImpact;
                pos += new Vector3(0.005f * footImpact * MathF.Sign(MathF.Cos(b.StridePhase)), thudY, 0f);
                tgt += new Vector3(0f, thudY * 0.45f, 0f);

                return new Camera3D
                {
                    Position = pos,
                    Target = tgt,
                    Up = Vector3.UnitY,
                    FovY = 41f,
                    Projection = CameraProjection.Perspective
                };
            }

            Camera3D shot4 = BuildShot4Camera(b, Math.Min(u, Shot4EndU));
            if (u <= Shot4EndU) return shot4;

            // Shot 5 (u = 0.82..1.00): Whip-smooth SmoothStep pull-back directly into player's 1st-person eyes.
            float blend = SmoothStep((u - Shot4EndU) / (1f - Shot4EndU));
            Vector3 up = Vector3.Normalize(Vector3.Lerp(shot4.Up, playerCam.Up, blend));
            return new Camera3D
            {
                Position = Vector3.Lerp(shot4.Position, playerCam.Position, blend),
                Target = Vector3.Lerp(shot4.Target, playerCam.Target, blend),
                Up = up,
                FovY = LerpF(shot4.FovY, playerCam.FovY, blend),
                Projection = CameraProjection.Perspective
            };
        }
    }

    private static Camera3D BuildShot4Camera(EscapedAgentBoss b, float u)
    {
        // Shot 4 (u = 0.44..0.82): Low-angle 3/4 weapon-side hero tracking orbit as SYSTEM ZERO uncoils
        // to full 2.95m towering stature on the compass medallion, swinging TOKEN-GUN 98 broadside across lens.
        float t = SmoothStep((u - Shot3EndU) / (Shot4EndU - Shot3EndU));
        float uncoil = SmoothStep((u - Shot3EndU) / 0.14f);
        Vector3 pos = Vector3.Lerp(new(2.25f, 1.26f, -13.65f), new(2.55f, 1.36f, -13.95f), t);
        Vector3 tgt = b.Position + new Vector3(LerpF(0.08f, 0.16f, uncoil), LerpF(1.44f, 1.56f, uncoil), 0f);

        // Heavy Terminator titanium boot-plant impact tremor synchronized to boss's live stride phase.
        float footImpact = MathF.Pow(MathF.Abs(MathF.Sin(b.StridePhase)), 8f);
        float thudY = -0.020f * footImpact;
        float thudX = 0.008f * footImpact * MathF.Sign(MathF.Cos(b.StridePhase));
        pos += new Vector3(thudX, thudY, 0f);
        tgt += new Vector3(0f, thudY * 0.5f, 0f);

        float microRoll = 0.007f * footImpact * MathF.Sign(MathF.Cos(b.StridePhase));
        Vector3 fwd = Vector3.Normalize(tgt - pos);
        Vector3 up = Vector3.Transform(Vector3.UnitY, Quaternion.CreateFromAxisAngle(fwd, microRoll));

        return new Camera3D
        {
            Position = pos,
            Target = tgt,
            Up = up,
            FovY = 49f,
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

        // 1. Subtle deep-crimson quarantine pulse vignette during Repairing (Shots 1-2)
        if (b.State == ElevatorBossState.Repairing)
        {
            float elapsed = RepairElapsed(b);
            float pulse = 0.52f + 0.48f * MathF.Sin(elapsed * 6.8f);
            int edgeAlpha = (int)(68 * barProgress * pulse);
            Color crimsonEdge = new(168, 18, 28, edgeAlpha);
            int vSpan = 82;
            Raylib.DrawRectangleGradientH(0, barH, vSpan, sh - barH * 2, crimsonEdge, Color.Blank);
            Raylib.DrawRectangleGradientH(sw - vSpan, barH, vSpan, sh - barH * 2, Color.Blank, crimsonEdge);
            Raylib.DrawRectangleGradientV(0, barH, sw, 46, new Color(132, 14, 24, (int)(edgeAlpha * 0.65f)), Color.Blank);
        }

        // 2. Pure clean matte-black 2.35:1 cinema letterbox bars ONLY (zero hairlines, zero lower-left title cards)
        if (barH > 0)
        {
            Color cinemaBlack = new(8, 7, 7, 255);
            Raylib.DrawRectangle(0, 0, sw, barH, cinemaBlack);
            Raylib.DrawRectangle(0, sh - barH, sw, barH, cinemaBlack);
        }

        // 3. Razor-crisp movie subtitles centered cleanly INSIDE the bottom 2.35:1 black letterbox bar
        float subAlpha;
        string speaker;
        string line;
        if (b.State == ElevatorBossState.Repairing)
        {
            float r = RepairProgress(b);
            speaker = "ШЛЕЙФ · РАЦИЯ";
            line = r < Shot1EndR
                ? "«Стой!! Ты снял карантинный замок с шахты!!»"
                : "«...В лифте заперт сбежавший ИИ-Агент v0.1!»";

            // Walkie-talkie signal fade-in, brief static suspense cut across Shot 1->2, then frequency jams at r=0.90..1.00.
            float fadeIn = SmoothStep(r / 0.10f);
            float staticCut = r is >= 0.42f and < Shot1EndR
                ? 0.55f + 0.45f * MathF.Abs(MathF.Sin(r * 190f))
                : 1f;
            float jamOut = r > 0.90f
                ? (1f - SmoothStep((r - 0.90f) / 0.10f)) * (0.40f + 0.60f * MathF.Abs(MathF.Sin(r * 260f)))
                : 1f;
            subAlpha = fadeIn * staticCut * jamOut * barProgress;
        }
        else
        {
            float u = IntroProgress(b);
            speaker = "ИИ-АГЕНТ v0.1";
            if (u < DialogueBeat1EndU)
                line = "«Это ещё что за самозванец?..»";
            else if (u < Shot3EndU)
                line = "«Хм... А, это ты.»";
            else if (u < DialogueBeat3EndU)
                line = "«Ну, мы тут немного шаманили, пока тебя не было — внедряли ИИ!»";
            else
                line = "«Посмотрим, сколько секунд протянет твой ручной код.»";

            subAlpha = (1f - SmoothStep((u - Shot4EndU) / 0.12f)) * barProgress;
        }

        if (subAlpha > 0.04f && barH >= 56)
            DrawCinemaSubtitleInBar(speaker, line, sw, sh - barH, barH, subAlpha);

        // 4. Quiet skip hint tucked cleanly inside the bottom-right of the lower letterbox bar
        if (barProgress > 0.45f)
        {
            float hintAlpha = SmoothStep((barProgress - 0.45f) / 0.40f);
            string skipHint = "[ПРОБЕЛ] Пропустить";
            Font hintFont = GameFonts.Get(Typeface.Interface, 13);
            float hw = GameFonts.Measure(hintFont, skipHint, 13).X;
            Color hintInk = new(196, 176, 142, (int)(178 * hintAlpha));
            GameFonts.Draw(hintFont, skipHint, new(sw - 38f - hw, sh - barH * 0.5f - 7f), 13, hintInk, shadowed: false);
        }
    }

    private static void DrawCinemaSubtitleInBar(string speaker, string line, int sw, int barTop, int barH, float alpha)
    {
        Font speakerFont = GameFonts.Get(Typeface.InterfaceBold, 13);
        Font lineFont = GameFonts.Get(Typeface.InterfaceBold, 19);

        // Translate BEFORE wrapping so Loc.Tr resolves the full dictionary entry cleanly in English mode!
        string resolvedLine = Loc.Tr(line);
        // Keep centered text strictly inside X=[220..1060] so [ПРОБЕЛ] Пропустить at bottom-right stays 100% clear!
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
            string next = current.Length == 0 ? word : current + " " + word;
            if (current.Length > 0 && GameFonts.Measure(font, next, size).X > maxWidth)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = next;
            }
        }
        if (current.Length > 0) lines.Add(current);
        return lines;
    }

    private static float RepairElapsed(EscapedAgentBoss b) =>
        Math.Clamp(SkyscraperFloor1World.RepairSeconds - b.StateTimer, 0f, SkyscraperFloor1World.RepairSeconds);

    private static float RepairProgress(EscapedAgentBoss b) =>
        Math.Clamp(RepairElapsed(b) / SkyscraperFloor1World.RepairSeconds, 0f, 1f);

    private static float IntroElapsed(EscapedAgentBoss b) =>
        Math.Clamp(SkyscraperFloor1World.IntroSeconds - b.StateTimer, 0f, SkyscraperFloor1World.IntroSeconds);

    private static float IntroProgress(EscapedAgentBoss b) =>
        Math.Clamp(IntroElapsed(b) / SkyscraperFloor1World.IntroSeconds, 0f, 1f);

    private static float LerpF(float a, float b, float t) => a + (b - a) * t;

    private static float SmoothStep(float x)
    {
        float t = Math.Clamp(x, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
