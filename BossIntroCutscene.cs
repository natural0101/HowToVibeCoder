#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

/// <summary>
/// Real-time in-engine 3D multi-shot cinema director for the North Elevator boss reveal.
/// Purely synchronized to <see cref="SkyscraperFloor1World.ElevatorBoss"/> Repairing (2.3s)
/// and IntroSpeech (2.4s) timers so gameplay, save/load snapshots, and autotest captures stay frame-locked.
/// </summary>
internal static class BossIntroCutscene
{
    private const float Shot1End = 1.10f; // Repairing 0.00..1.10s: sparking brass panel + floor dial
    private const float Shot3End = 1.70f; // IntroSpeech 0.00..1.70s: heroic low-angle tracking + TOKEN-GUN 98 reveal
    private const float DialogueSplit = 1.25f; // IntroSpeech beat 1 -> beat 2

    private const float BarHeight235 = 88f; // 2.35:1 cinema aperture inside 1280x720 virtual UI space

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
        float elapsed = IntroElapsed(b);
        if (elapsed <= Shot3End) return true;
        float pullBack = SmoothStep((elapsed - Shot3End) / (SkyscraperFloor1World.IntroSeconds - Shot3End));
        return pullBack < 0.82f;
    }

    internal static float GetHudAlpha(SkyscraperFloor1World world)
    {
        if (!IsActive(world)) return 1f;
        var b = world.ElevatorBoss;
        if (b.State == ElevatorBossState.Repairing) return 0f;
        float elapsed = IntroElapsed(b);
        if (elapsed <= Shot3End) return 0f;
        return SmoothStep((elapsed - Shot3End) / (SkyscraperFloor1World.IntroSeconds - Shot3End));
    }

    internal static float GetLetterboxProgress(SkyscraperFloor1World world)
    {
        if (!IsActive(world)) return 0f;
        var b = world.ElevatorBoss;
        if (b.State == ElevatorBossState.Repairing)
            return SmoothStep(RepairElapsed(b) / 0.34f);

        float elapsed = IntroElapsed(b);
        if (elapsed <= Shot3End) return 1f;
        return 1f - SmoothStep((elapsed - Shot3End) / (SkyscraperFloor1World.IntroSeconds - Shot3End));
    }

    internal static Camera3D ResolveCamera(SkyscraperFloor1World world, Camera3D playerCam)
    {
        if (!IsActive(world)) return playerCam;
        var b = world.ElevatorBoss;

        if (b.State == ElevatorBossState.Repairing)
        {
            float elapsed = RepairElapsed(b);
            if (elapsed < Shot1End)
            {
                // Shot 1 (0.00..1.10s): Angled close-up on sparking repaired brass panel + Floor 01 dial.
                float u = SmoothStep(elapsed / Shot1End);
                Vector3 pos = Vector3.Lerp(new(-2.12f, 1.76f, -11.35f), new(-1.78f, 1.92f, -11.48f), u);
                Vector3 tgt = Vector3.Lerp(new(-0.35f, 1.92f, -8.65f), new(0.45f, 2.28f, -8.50f), u);

                // Subtle crisp electrical tremor during live panel spark bursts.
                float sparkBurst = (elapsed % 0.38f) < 0.22f ? 1f : 0f;
                float tremor = sparkBurst * 0.009f;
                pos += new Vector3(MathF.Sin(elapsed * 57f) * tremor, MathF.Cos(elapsed * 71f) * tremor, 0f);

                return new Camera3D
                {
                    Position = pos,
                    Target = tgt,
                    Up = Vector3.UnitY,
                    FovY = LerpF(48f, 50f, u),
                    Projection = CameraProjection.Perspective
                };
            }
            else
            {
                // Shot 2 (1.10..2.30s): Low-centered push-in on clearing smoked-glass doors & crouched awakening silhouette.
                float u = SmoothStep((elapsed - Shot1End) / (SkyscraperFloor1World.RepairSeconds - Shot1End));
                Vector3 pos = Vector3.Lerp(new(0.35f, 1.44f, -13.05f), new(0.14f, 1.50f, -11.88f), u);
                Vector3 tgt = Vector3.Lerp(new(0.00f, 1.46f, -7.25f), new(0.00f, 1.54f, -7.15f), u);

                // Low-frequency quarantine lock-release rumble as the shaft seal breaks.
                float rumble = SmoothStep((u - 0.55f) / 0.45f) * 0.011f;
                pos += new Vector3(MathF.Sin(elapsed * 43f) * rumble, MathF.Cos(elapsed * 53f) * rumble * 0.7f, 0f);

                return new Camera3D
                {
                    Position = pos,
                    Target = tgt,
                    Up = Vector3.UnitY,
                    FovY = LerpF(47f, 44f, u),
                    Projection = CameraProjection.Perspective
                };
            }
        }
        else
        {
            float elapsed = IntroElapsed(b);
            Camera3D shot3 = BuildShot3Camera(b, Math.Min(elapsed, Shot3End));
            if (elapsed <= Shot3End) return shot3;

            // Shot 4 (1.70..2.40s): Seamless SmoothStep pull-back directly into player's live 1st-person eye camera.
            float blend = SmoothStep((elapsed - Shot3End) / (SkyscraperFloor1World.IntroSeconds - Shot3End));
            Vector3 up = Vector3.Normalize(Vector3.Lerp(shot3.Up, playerCam.Up, blend));
            return new Camera3D
            {
                Position = Vector3.Lerp(shot3.Position, playerCam.Position, blend),
                Target = Vector3.Lerp(shot3.Target, playerCam.Target, blend),
                Up = up,
                FovY = LerpF(shot3.FovY, playerCam.FovY, blend),
                Projection = CameraProjection.Perspective
            };
        }
    }

    private static Camera3D BuildShot3Camera(EscapedAgentBoss b, float elapsed)
    {
        // Shot 3 (IntroSpeech 0.00..1.70s): Heroic low-angle tracking sweep onto boss's front-right (+X world) 3/4 weapon side!
        // Showcases crouched doorway stalk (t=1.05s) uncoiling into towering 2.95m hero reveal with TOKEN-GUN 98 cassette reels & brass coils (t=1.55s).
        float u = SmoothStep(elapsed / Shot3End);
        float uncoil = SmoothStep((elapsed - 1.10f) / 0.44f);
        Vector3 pos = Vector3.Lerp(new(-1.10f, 1.08f, -13.50f), new(2.52f, 1.34f, -13.95f), u);
        Vector3 tgt = b.Position + new Vector3(LerpF(0.00f, 0.14f, uncoil), LerpF(1.36f, 1.56f, uncoil), 0f);

        // Heavy Terminator titanium boot-plant impact tremor synchronized to boss's live stride phase.
        float strideActive = SmoothStep((elapsed - 0.30f) / 0.25f);
        float footImpact = MathF.Pow(MathF.Abs(MathF.Sin(b.StridePhase)), 8f) * strideActive;
        float thudY = -0.022f * footImpact;
        float thudX = 0.009f * footImpact * MathF.Sign(MathF.Cos(b.StridePhase));
        pos += new Vector3(thudX, thudY, 0f);
        tgt += new Vector3(0f, thudY * 0.5f, 0f);

        float microRoll = 0.008f * footImpact * MathF.Sign(MathF.Cos(b.StridePhase));
        Vector3 fwd = Vector3.Normalize(tgt - pos);
        Vector3 up = Vector3.Transform(Vector3.UnitY, Quaternion.CreateFromAxisAngle(fwd, microRoll));

        return new Camera3D
        {
            Position = pos,
            Target = tgt,
            Up = up,
            FovY = LerpF(46f, 51f, u),
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

        // 2. Razor-clean 2.35:1 matte cinema letterbox bars + hairline antique brushed-brass trim
        if (barH > 0)
        {
            Color cinemaBlack = new(8, 7, 7, 255);
            Color brassHairline = new(176, 138, 74, (int)(96 * barProgress));
            Raylib.DrawRectangle(0, 0, sw, barH, cinemaBlack);
            Raylib.DrawRectangle(0, sh - barH, sw, barH, cinemaBlack);
            Raylib.DrawRectangle(0, barH, sw, 1, brassHairline);
            Raylib.DrawRectangle(0, sh - barH - 1, sw, 1, brassHairline);
        }

        // 3. Art-Deco Cinema Boss Title Card (lower-left active frame during Shot 3, rich dark backing scrim)
        if (b.State == ElevatorBossState.IntroSpeech)
        {
            float elapsed = IntroElapsed(b);
            float cardIn = SmoothStep((elapsed - 0.14f) / 0.32f);
            float cardOut = 1f - SmoothStep((elapsed - 1.58f) / 0.34f);
            float cardAlpha = Math.Clamp(cardIn * cardOut, 0f, 1f);
            if (cardAlpha > 0.02f)
                DrawArtDecoTitleCard(cardAlpha, sh - barH);
        }

        // 4. High-contrast Cinema Subtitles cleanly centered INSIDE the bottom 2.35:1 black letterbox bar
        float subAlpha = b.State == ElevatorBossState.Repairing
            ? SmoothStep(RepairElapsed(b) / 0.28f) * barProgress
            : (1f - SmoothStep((IntroElapsed(b) - Shot3End) / 0.42f)) * barProgress;

        if (subAlpha > 0.04f && barH >= 56)
        {
            string speaker;
            string line;
            if (b.State == ElevatorBossState.Repairing)
            {
                speaker = "ПЕТРОВИЧ · ИНТЕРКОМ";
                line = "«Стой!! Ты снял карантинный замок с шахты!! В лифте заперт сбежавший ИИ-Агент v0.1!»";
            }
            else
            {
                speaker = "ИИ-АГЕНТ v0.1";
                line = IntroElapsed(b) < DialogueSplit
                    ? "«Это ещё что за самозванец?.. А, это ты.»"
                    : "«Ну, мы тут немного шаманили, пока тебя не было... внедряли ИИ.»";
            }

            DrawCinemaSubtitleInBar(speaker, line, sw, sh - barH, barH, subAlpha);
        }

        // 5. Quiet skip hint tucked cleanly inside the bottom-right of the lower letterbox bar
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

    private static void DrawArtDecoTitleCard(float alpha, int bottomBarTop)
    {
        // Anchored cleanly in the lower-left above the bottom 2.35:1 black bar with rich feathered vignette scrim.
        float slideX = (1f - alpha) * -18f;
        float x = 56f + slideX;
        float baseY = bottomBarTop - 118f;

        int a = (int)(255 * alpha);
        int aMuted = (int)(232 * alpha);
        Color brass = new(224, 186, 114, a);
        Color ivory = new(250, 245, 232, a);
        Color goldSub = new(228, 202, 150, aMuted);
        Color dropShadow = new(4, 4, 4, (int)(230 * alpha));

        Font eyebrowFont = GameFonts.Get(Typeface.InterfaceBold, 13);
        Font titleFont = GameFonts.Get(Typeface.SerifBold, 42);
        Font subFont = GameFonts.Get(Typeface.InterfaceBold, 15);

        const string eyebrow = "КАРАНТИННЫЙ УЗЕЛ «ЭНТРОПИК» · 1998";
        const string mainTitle = "ИИ-АГЕНТ v0.1";
        const string subtitle = "СБЕЖАВШИЙ ПРОТОТИП SYSTEM ZERO";

        float titleW = GameFonts.Measure(titleFont, mainTitle, 42).X;
        float subW = GameFonts.Measure(subFont, subtitle, 15).X;
        float scrimW = Math.Max(titleW, subW) + 128f;

        // Soft dark feathered vignette scrim behind title card (zero hard edges, razor readability over any backdrop!)
        Raylib.DrawRectangleGradientH(0, (int)baseY - 18, (int)(x + scrimW), 124,
            new Color(8, 6, 6, (int)(188 * alpha)), Color.Blank);
        Raylib.DrawRectangleGradientV(0, (int)baseY - 40, (int)(x + scrimW * 0.85f), 26,
            Color.Blank, new Color(8, 6, 6, (int)(105 * alpha)));

        // Eyebrow
        GameFonts.Draw(eyebrowFont, eyebrow, new(x + 1f, baseY + 1.5f), 13, dropShadow, shadowed: false);
        GameFonts.Draw(eyebrowFont, eyebrow, new(x, baseY), 13, brass, shadowed: false);

        // Main Title (bold Russo One ivory with crisp dark cinema drop shadow)
        float titleY = baseY + 18f;
        GameFonts.Draw(titleFont, mainTitle, new(x + 2f, titleY + 3f), 42, dropShadow, shadowed: false);
        GameFonts.Draw(titleFont, mainTitle, new(x, titleY), 42, ivory, shadowed: false);

        // Clean brushed-brass hairline trimmed precisely to the main title width
        float ruleY = titleY + 49f;
        int ruleW = (int)(titleW * alpha);
        if (ruleW > 4)
        {
            Raylib.DrawRectangleGradientH((int)x, (int)ruleY, ruleW, 1,
                new Color(224, 186, 114, (int)(225 * alpha)),
                new Color(224, 186, 114, (int)(45 * alpha)));
        }

        // Subtitle
        float subY = ruleY + 8f;
        GameFonts.Draw(subFont, subtitle, new(x + 1.5f, subY + 2f), 15, dropShadow, shadowed: false);
        GameFonts.Draw(subFont, subtitle, new(x, subY), 15, goldSub, shadowed: false);
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

    private static float IntroElapsed(EscapedAgentBoss b) =>
        Math.Clamp(SkyscraperFloor1World.IntroSeconds - b.StateTimer, 0f, SkyscraperFloor1World.IntroSeconds);

    private static float LerpF(float a, float b, float t) => a + (b - a) * t;

    private static float SmoothStep(float x)
    {
        float t = Math.Clamp(x, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
