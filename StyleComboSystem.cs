using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

/// <summary>
/// Skill-Trick & Animated Cash Multiplier Engine («Счётчик Авторитета и Стильные Приёмы»).
/// Rewards creative takedowns (Flip-Off Taunt into Capture, 220V Puddle Combo, Stealth Backstab,
/// Bait Ambush, Airborne Catch, Espresso Dash, Belt Disarm, Belt Parry, Weapon Variety & Streaks)
/// with live escalating payout multipliers, spring-animated wax-stamp rank HUD, micro-hitstop, and cash roll-up.
/// </summary>
internal static class StyleComboSystem
{
    public sealed class TrickTag
    {
        public string Label = "";
        public string MultText = "";
        public Color Accent;
        public float Age;
        public float Duration = 4.2f;
    }

    public const float MaxComboFuse = 9.5f;

    public static float StylePoints { get; private set; }
    public static float Multiplier { get; private set; } = 1.0f;
    public static float DisplayedMultiplier { get; private set; } = 1.0f;
    public static float ComboFuse { get; private set; }
    public static int StreakCount { get; private set; }
    public static WeaponType? LastFinishWeapon { get; private set; }

    // Tactile animation & juice state
    private static float _rankPunch;
    private static float _multPunch;
    private static int _lastRankIdx = -1;
    private static float _pendingShake;
    private static float _hitstopTimer;
    private static readonly List<TrickTag> _feed = new();

    // Animated cashout formula popup ($120 × 2.8 = +$336!)
    private static int _cashBase;
    private static float _cashMult = 1f;
    private static int _cashTotal;
    private static float _cashTimer;
    private static float _cashElapsed;
    private const float CashPopupDuration = 3.2f;

    private static readonly (float Threshold, string Code, string SubtitleRu, string SubtitleEn, Color Ink)[] Ranks =
    [
        (0f,   "D",  "СТАЖЁР",     "INTERN",    new Color(224, 212, 188, 255)),
        (32f,  "C",  "ДЕЛОВОЙ",    "SHARP",     new Color(230, 192, 118, 255)),
        (72f,  "B",  "БРИГАДИР",   "HUSTLER",   new Color(245, 174, 72,  255)),
        (128f, "A",  "АВТОРИТЕТ",  "BOSS",      new Color(242, 118, 58,  255)),
        (200f, "S",  "СМОТРЯЩИЙ",  "KINGPIN",   new Color(226, 56,  66,  255)),
        (290f, "S+", "ЛЕГЕНДА 90-Х","LEGEND 90S",new Color(255, 208, 76,  255)),
    ];

    public static int RankIndex
    {
        get
        {
            for (int i = Ranks.Length - 1; i >= 0; i--)
                if (StylePoints >= Ranks[i].Threshold) return i;
            return 0;
        }
    }

    public static void Reset()
    {
        StylePoints = 0f;
        Multiplier = 1.0f;
        DisplayedMultiplier = 1.0f;
        ComboFuse = 0f;
        StreakCount = 0;
        LastFinishWeapon = null;
        _rankPunch = 0f;
        _multPunch = 0f;
        _lastRankIdx = -1;
        _pendingShake = 0f;
        _hitstopTimer = 0f;
        _feed.Clear();
        _cashTimer = 0f;
        _cashElapsed = 0f;
    }

    public static bool ConsumeHitstop(float dt)
    {
        if (_hitstopTimer <= 0f) return false;
        _hitstopTimer = Math.Max(0f, _hitstopTimer - dt);
        return true;
    }

    public static float ConsumeShake()
    {
        float s = _pendingShake;
        _pendingShake = 0f;
        return s;
    }

    public static void Update(float dt)
    {
        if (dt <= 0f) return;

        if (ComboFuse > 0f)
        {
            ComboFuse = Math.Max(0f, ComboFuse - dt);
            if (ComboFuse <= 0f)
            {
                StylePoints = 0f;
                Multiplier = 1.0f;
                StreakCount = 0;
                LastFinishWeapon = null;
                _lastRankIdx = -1;
            }
        }

        // Smoothly roll displayed multiplier up/down toward live multiplier
        float rate = DisplayedMultiplier < Multiplier ? 10f : 6f;
        DisplayedMultiplier += (Multiplier - DisplayedMultiplier) * (1f - MathF.Exp(-rate * dt));
        if (MathF.Abs(DisplayedMultiplier - Multiplier) < 0.005f) DisplayedMultiplier = Multiplier;

        _rankPunch = Math.Max(0f, _rankPunch - dt * 3.4f);
        _multPunch = Math.Max(0f, _multPunch - dt * 4.2f);

        for (int i = _feed.Count - 1; i >= 0; i--)
        {
            _feed[i].Age += dt;
            if (_feed[i].Age >= _feed[i].Duration)
                _feed.RemoveAt(i);
        }

        if (_cashTimer > 0f)
        {
            _cashTimer = Math.Max(0f, _cashTimer - dt);
            _cashElapsed += dt;
        }
    }

    public static void RegisterTrick(string ruLabel, string enLabel, float multAdd, float points, Color accent,
        bool hitstop = false, float shake = 0.012f)
    {
        int prevRank = ComboFuse > 0f ? RankIndex : -1;
        StylePoints = Math.Min(420f, StylePoints + points);
        Multiplier = Math.Clamp(Multiplier + multAdd, 1.0f, 5.0f);
        ComboFuse = MaxComboFuse;
        _multPunch = 1.0f;

        int nextRank = RankIndex;
        if (nextRank > prevRank)
        {
            _rankPunch = 1.0f;
            _lastRankIdx = nextRank;
        }
        else
        {
            _rankPunch = Math.Max(_rankPunch, 0.55f);
        }

        if (hitstop) _hitstopTimer = Math.Max(_hitstopTimer, 0.038f);
        if (shake > _pendingShake) _pendingShake = shake;

        string tag = Loc.T(ruLabel, enLabel);
        _feed.Insert(0, new TrickTag
        {
            Label = tag,
            MultText = $"+×{multAdd:0.00}".Replace(".00", "").Replace("0", multAdd % 0.1f < 0.001f ? "" : "0"),
            Accent = accent,
            Age = 0f,
            Duration = 4.5f
        });
        // Clean formatted multiplier delta (+×0.35, +×0.6, +×0.4)
        _feed[0].MultText = $"+×{multAdd:0.##}";
        if (_feed.Count > 5) _feed.RemoveAt(_feed.Count - 1);
    }

    public static float EvaluateTakedown(
        VibeCoderEntity target,
        WeaponType tool,
        bool isAirborne,
        bool isDashing,
        bool hasCoffee,
        bool wasBackstab,
        bool wasBaitAmbush,
        bool wasDisarm,
        bool wasElectro,
        bool isFinisher)
    {
        Color gold = new(245, 202, 98, 255);
        Color ember = new(246, 128, 66, 255);
        Color crimson = new(238, 72, 82, 255);
        Color electric = new(118, 224, 248, 255);
        Color ivory = new(246, 236, 212, 255);

        bool anyTrick = false;

        if (wasDisarm)
        {
            RegisterTrick("ВЫБИЛ МАКБУК", "LAPTOP DISARM", 0.25f, 22f, gold, hitstop: true, shake: 0.016f);
            anyTrick = true;
        }

        if (!isFinisher)
            return Multiplier;

        // Finisher / Subdue tricks — stackable and juicy!
        if (target.FlippedOffTimer > 0f)
        {
            RegisterTrick("ДЕРЗКИЙ ФАК!", "TAUNT FINISH!", 0.45f, 38f, crimson, hitstop: true, shake: 0.024f);
            anyTrick = true;
        }
        if (wasElectro || target.ElectroShockedTimer > 0f)
        {
            RegisterTrick("220В КОМБО!", "220V COMBO!", 0.60f, 44f, electric, hitstop: true, shake: 0.026f);
            anyTrick = true;
        }
        if (isAirborne)
        {
            RegisterTrick("В ПОЛЁТЕ!", "AIRBORNE!", 0.50f, 36f, gold, hitstop: true, shake: 0.022f);
            anyTrick = true;
        }
        if (wasBaitAmbush)
        {
            RegisterTrick("НА ЖИВЦА!", "BAIT AMBUSH!", 0.40f, 30f, ember, hitstop: true, shake: 0.018f);
            anyTrick = true;
        }
        else if (wasBackstab)
        {
            RegisterTrick("В СПИНУ!", "BACKSTAB!", 0.30f, 26f, ember, hitstop: true, shake: 0.016f);
            anyTrick = true;
        }
        if (isDashing || hasCoffee)
        {
            RegisterTrick("ЭСПРЕССО-СНОС!", "DASH TAKEDOWN!", 0.35f, 28f, gold, hitstop: true, shake: 0.018f);
            anyTrick = true;
        }
        if (LastFinishWeapon.HasValue && LastFinishWeapon.Value != tool && ComboFuse > 0f)
        {
            RegisterTrick("СМЕНА АРСЕНАЛА", "WEAPON SWITCH", 0.25f, 22f, ivory, hitstop: false, shake: 0.012f);
            anyTrick = true;
        }

        StreakCount = ComboFuse > 0f ? StreakCount + 1 : 1;
        LastFinishWeapon = tool;

        if (StreakCount >= 2)
        {
            float streakAdd = Math.Min(0.60f, 0.20f + 0.10f * (StreakCount - 2));
            RegisterTrick($"СЕРИЯ ×{StreakCount}!", $"STREAK ×{StreakCount}!", streakAdd, 26f + StreakCount * 6f, crimson, hitstop: true, shake: 0.022f);
            anyTrick = true;
        }
        else if (!anyTrick)
        {
            // Clean single finisher still keeps baseline 1.0x (no unexpected cash drift on plain tests),
            // while waking up the style fuse so the next quick kill starts a real Streak!
            ComboFuse = MaxComboFuse;
            StylePoints = Math.Max(StylePoints, 12f);
        }

        if (anyTrick)
        {
            target.CaptureMultiplier = Math.Max(target.CaptureMultiplier, Multiplier);
            AudioMusicSystem.PlayCaseTick();
        }
        return target.CaptureMultiplier;
    }

    public static int ComputeMultipliedPayout(int baseCash, float storedMult, out int bonusCash)
    {
        float m = Math.Clamp(storedMult, 1.0f, 5.0f);
        if (m <= 1.02f)
        {
            bonusCash = 0;
            return baseCash;
        }
        int total = (int)MathF.Round(baseCash * m);
        bonusCash = Math.Max(0, total - baseCash);
        if (bonusCash > 0)
        {
            _cashBase = baseCash;
            _cashMult = m;
            _cashTotal = total;
            _cashTimer = CashPopupDuration;
            _cashElapsed = 0f;
            _multPunch = 1.0f;
            _rankPunch = 0.85f;
        }
        return total;
    }

    /// <summary>Triggers a showcase combo state strictly for automated visual screenshot QA.</summary>
    internal static void TriggerVisualDemo()
    {
        Reset();
        RegisterTrick("НА ЖИВЦА!", "BAIT AMBUSH!", 0.40f, 34f, new Color(246, 128, 66, 255));
        RegisterTrick("220В КОМБО!", "220V COMBO!", 0.60f, 56f, new Color(118, 224, 248, 255));
        RegisterTrick("ДЕРЗКИЙ ФАК!", "TAUNT FINISH!", 0.45f, 52f, new Color(238, 72, 82, 255));
        RegisterTrick("В ПОЛЁТЕ!", "AIRBORNE!", 0.50f, 48f, new Color(245, 202, 98, 255));
        RegisterTrick("СЕРИЯ ×3!", "STREAK ×3!", 0.30f, 38f, new Color(238, 72, 82, 255));
        DisplayedMultiplier = Multiplier;
        for (int i = 0; i < _feed.Count; i++) _feed[i].Age = 0.25f + i * 0.28f;
        _rankPunch = 0.38f;
        _multPunch = 0.32f;
        ComputeMultipliedPayout(120, Multiplier, out _);
        _cashElapsed = 0.42f;
        _pendingShake = 0f;
        _hitstopTimer = 0f;
    }

    public static void DrawHud(int screenW, int screenH)
    {
        bool showRank = ComboFuse > 0f && (Multiplier > 1.01f || _feed.Count > 0);
        bool showCash = _cashTimer > 0f;
        if (!showRank && !showCash) return;

        float rightEdge = screenW - 34f;
        float topY = 98f;
        Color shadow = new(10, 7, 5, 215);

        void Shadowed(Font f, string text, float x, float y, int size, Color col)
        {
            if (col.A < 3) return;
            Color sh = new(shadow.R, shadow.G, shadow.B, (byte)(shadow.A * (col.A / 255f)));
            GameFonts.Draw(f, text, new(x + 2, y + 2), size, sh, shadowed: false);
            GameFonts.Draw(f, text, new(x, y), size, col, shadowed: false);
        }

        if (showRank)
        {
            var rank = Ranks[RankIndex];
            float fade = Math.Clamp(ComboFuse * 1.4f, 0f, 1f);
            Color rankCol = Raylib.Fade(rank.Ink, fade);
            Color brass = Raylib.Fade(new Color(234, 194, 114, 255), fade);
            Color ivory = Raylib.Fade(new Color(248, 238, 214, 255), fade);

            // 1) Spring-bouncing Angled Wax-Ink Rank Stamp (ZERO SaaS box — pure stamped emblem!)
            float stampScale = 1.0f + MathF.Pow(_rankPunch, 1.5f) * 0.42f;
            float stampAngle = -5.5f + _rankPunch * 5.2f;
            float stampCX = rightEdge - 186f;
            float stampCY = topY + 34f;
            Rlgl.PushMatrix();
            Rlgl.Translatef(stampCX, stampCY, 0f);
            Rlgl.Rotatef(stampAngle, 0f, 0f, 1f);
            Rlgl.Scalef(stampScale, stampScale, 1f);

            // Double-framed wet-ink stamp crest
            Rectangle outer = new(-34, -30, 68, 60);
            Rectangle inner = new(-29, -25, 58, 50);
            Raylib.DrawRectangleRec(outer, Raylib.Fade(new Color(20, 14, 12, 185), fade));
            Raylib.DrawRectangleLinesEx(outer, 2.8f, rankCol);
            Raylib.DrawRectangleLinesEx(inner, 1.2f, Raylib.Fade(rank.Ink, fade * 0.65f));

            int codeSize = rank.Code.Length > 1 ? 36 : 44;
            Font codeFont = GameFonts.Get(Typeface.SerifBold, codeSize);
            float cw = GameFonts.Measure(codeFont, rank.Code, codeSize).X;
            GameFonts.Draw(codeFont, rank.Code, new(-cw * 0.5f, -codeSize * 0.52f), codeSize, rankCol, shadowed: false);
            Rlgl.PopMatrix();

            // 2) Live Ticking Multiplier Readout right beside the stamp (×3.25)
            string multStr = $"×{DisplayedMultiplier:0.00}";
            float multScale = 1.0f + _multPunch * 0.24f;
            Font multFont = GameFonts.Get(Typeface.SerifBold, 42);
            float mw = GameFonts.Measure(multFont, multStr, 42).X;
            float multX = rightEdge - mw * 0.5f;
            float multY = topY + 26f;
            Rlgl.PushMatrix();
            Rlgl.Translatef(multX, multY, 0f);
            Rlgl.Scalef(multScale, multScale, 1f);
            Shadowed(multFont, multStr, -mw * 0.5f, -22f, 42, brass);
            Rlgl.PopMatrix();

            // Rank title right under multiplier
            string sub = Loc.T(rank.SubtitleRu, rank.SubtitleEn);
            Font subFont = GameFonts.Get(Typeface.InterfaceBold, 13);
            float sw = GameFonts.Measure(subFont, sub, 13).X;
            Shadowed(subFont, sub, rightEdge - sw, topY + 50f, 13, ivory);

            // 3) Tapered Burning Brass-to-Ember Combo Fuse Bar (clean margin below angled stamp!)
            float fuseFrac = Math.Clamp(ComboFuse / MaxComboFuse, 0f, 1f);
            float barW = 220f;
            float barX = rightEdge - barW;
            float barY = topY + 82f;
            Raylib.DrawRectangleRec(new(barX, barY, barW, 5f), Raylib.Fade(new Color(18, 13, 10, 190), fade));
            Color fuseCol = fuseFrac < 0.28f
                ? Raylib.ColorLerp(new Color(236, 62, 54, 255), new Color(255, 196, 86, 255), 0.5f + 0.5f * MathF.Sin(ComboFuse * 18f))
                : rank.Ink;
            float fillW = barW * fuseFrac;
            Raylib.DrawRectangleRec(new(rightEdge - fillW, barY, fillW, 5f), Raylib.Fade(fuseCol, fade));
            // Glowing ember tip on the burning fuse
            if (fillW > 3f)
                Raylib.DrawCircle((int)(rightEdge - fillW), (int)(barY + 2.5f), 4f, Raylib.Fade(new Color(255, 240, 185, 255), fade));

            // 4) Cascading Right-Aligned Animated Trick Tags (clean floating typography, ZERO dark rectangles!)
            float fy = topY + 96f;
            Font tagFont = GameFonts.Get(Typeface.InterfaceBold, 16);
            Font bonusFont = GameFonts.Get(Typeface.SerifBold, 17);
            for (int i = 0; i < _feed.Count; i++)
            {
                var item = _feed[i];
                float enter = Math.Clamp(item.Age / 0.16f, 0f, 1f);
                float easeOut = 1f - (1f - enter) * (1f - enter);
                float exit = item.Age > item.Duration - 0.55f ? Math.Clamp((item.Duration - item.Age) / 0.55f, 0f, 1f) : 1f;
                float alpha = fade * exit * easeOut;
                float slideX = (1f - easeOut) * 64f;

                float bw = GameFonts.Measure(bonusFont, item.MultText, 17).X;
                float lw = GameFonts.Measure(tagFont, item.Label, 16).X;
                float bx = rightEdge - bw + slideX;
                float lx = bx - lw - 10f;

                Shadowed(tagFont, item.Label, lx, fy, 16, Raylib.Fade(item.Accent, alpha));
                Shadowed(bonusFont, item.MultText, bx, fy - 1f, 17, Raylib.Fade(new Color(252, 224, 146, 255), alpha));
                fy += 24f;
            }
            topY = fy + 8f;
        }

        // 5) Animated Gilded Cashout Formula Roll-Up ($120 × 2.85 = +$342!)
        if (showCash)
        {
            float cAlpha = Math.Clamp(_cashTimer * 2.2f, 0f, 1f);
            float rollT = Math.Clamp(_cashElapsed / 0.36f, 0f, 1f);
            float eased = 1f - MathF.Pow(1f - rollT, 3f);
            int rolled = _cashBase + (int)MathF.Round((_cashTotal - _cashBase) * eased);
            float pop = _cashElapsed < 0.18f ? 1f + (1f - _cashElapsed / 0.18f) * 0.28f : 1f;

            string eqLeft = $"${_cashBase} × {_cashMult:0.00}  =  ";
            string eqTotal = $"+${rolled}!";
            Font fLeft = GameFonts.Get(Typeface.InterfaceBold, 17);
            Font fTot = GameFonts.Get(Typeface.SerifBold, 26);
            float wL = GameFonts.Measure(fLeft, eqLeft, 17).X;
            float wT = GameFonts.Measure(fTot, eqTotal, 26).X;

            float xTot = rightEdge - wT;
            float xLeft = xTot - wL;
            Shadowed(fLeft, eqLeft, xLeft, topY + 6f, 17, Raylib.Fade(new Color(232, 214, 182, 255), cAlpha));

            Rlgl.PushMatrix();
            Rlgl.Translatef(xTot + wT * 0.5f, topY + 13f, 0f);
            Rlgl.Scalef(pop, pop, 1f);
            Shadowed(fTot, eqTotal, -wT * 0.5f, -13f, 26, Raylib.Fade(new Color(255, 212, 84, 255), cAlpha));
            Rlgl.PopMatrix();
        }
    }
}
