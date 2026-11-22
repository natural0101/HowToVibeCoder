#nullable enable
using System;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public static partial class Program
{
    private const int FinaleAction = 43;

    private static readonly string[] FinaleTitlesRu =
    [
        "Контейнировать агента",
        "Отпустить агента в сеть"
    ];
    private static readonly string[] FinaleTitlesEn =
    [
        "Contain the Agent",
        "Release the Agent to the Net"
    ];

    private static readonly string[] PursuitTitlesRu =
    [
        "Закоротить щиток шахты и уйти на 2-й этаж",
        "Сбросить тревогу СБ и уйти в тень"
    ];
    private static readonly string[] PursuitTitlesEn =
    [
        "Short-Circuit Shaft Panel & Ascend to Floor 2",
        "Wipe Security Alert & Slip Into the Shadows"
    ];

    private static Rectangle DecisionRow(int i) => new(164, 286 + 92 * i, 872, 82);

    private static bool IsBossEscapedPursuit() => _world.BossEscapedPursuit;

    // Delayed Dark Souls / Elden Ring damage-drain ghost bar telemetry
    private static bool _bossTrackInit;
    private static float _bossHpGhost, _bossShieldGhost;
    private static float _bossLastHp, _bossLastShield;
    private static float _bossHpHold, _bossShieldHold;
    private static float _bossHitPulse;

    internal static void ResetBossBarTelemetry()
    {
        _bossTrackInit = false;
        _bossHpGhost = _bossShieldGhost = _bossLastHp = _bossLastShield = 0f;
        _bossHpHold = _bossShieldHold = _bossHitPulse = 0f;
    }

    private static bool HandleDecision()
    {
        var pending = Floor.FinalePending && Floor.ShutdownTime <= 0 ? FloorPanel.BossFinale : FloorPanel.None;
        if (pending == FloorPanel.None)
        {
            if (floorPanel is not (FloorPanel.Audit or FloorPanel.BossFinale)) return false;
            OpenFloorPanel(FloorPanel.None);
            return true;
        }
        if (floorPanel != pending)
        {
            OpenFloorPanel(pending);
            return true;
        }

        bool pursuit = IsBossEscapedPursuit();
        Vector2 mouse = (Raylib.GetMousePosition() - PanelOffset) / PanelScale;
        for (int i = 0; i < 2; i++)
        {
            if (!Raylib.IsKeyPressed(KeyboardKey.One + i) && !Raylib.IsKeyPressed(KeyboardKey.Kp1 + i) &&
                !(Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mouse, DecisionRow(i))))
                continue;

            panelMessage = FloorAction(FinaleAction + i);
            if (pursuit && Floor.FinalePending && Floor.Verdict != AgentVerdict.Pending)
                Floor.FinalePending = false;

            if (!Floor.FinalePending)
            {
                string banner = pursuit
                    ? Loc.T("ПРОРЫВ НА 2-Й ЭТАЖ! ИИ-АГЕНТ v0.1 ЛЕЗЕТ СЛЕДОМ ПО ТРОСАМ ШАХТЫ!", "FLOOR 2 BREAKOUT! AI AGENT v0.1 IS CLIMBING THE SHAFT AFTER YOU!")
                    : panelMessage;
                SetHudBanner(banner, pursuit ? 4.6f : 4.2f, foreground: pursuit);
                OpenFloorPanel(FloorPanel.None);
            }
            break;
        }
        return true;
    }

    private static void DrawDecision()
    {
        bool pursuit = IsBossEscapedPursuit();
        var ink = LoreAndMenusUI.ThemeSepiaInk;
        var muted = LoreAndMenusUI.ThemeSepiaMuted;
        var red = LoreAndMenusUI.ThemeStampRed;
        LoreAndMenusUI.DrawManilaPaper(new(116, 26, 1048, 668));

        void Print(string s, float x, float y, int size, Color c, Typeface f = Typeface.Interface) =>
            DossierPrint.Text(s, x, y, size, c, f);

        // Clean floor dossier header (zero middle dots)
        string sessionHeader = Loc.T(
            "ЭТАЖ 01 — БАШНЯ «ЭНТРОПИК»",
            "FLOOR 01 — ENTROPIC TOWER");
        Print(sessionHeader, 178, 92, 15, muted, Typeface.InterfaceBold);

        // Tilted wet-ink crimson rubber stamp completely clear of the top-right steel paperclip
        string stamp = pursuit
            ? Loc.T("ТРЕВОГА В ШАХТЕ", "SHAFT ALERT")
            : Loc.T("ЭКЗЕМПЛЯР 01", "COPY 01");
        float stampWidth = GameFonts.Measure(GameFonts.Get(Typeface.InterfaceBold, 17), stamp, 17).X + 26;
        Rlgl.PushMatrix();
        Rlgl.Translatef(796, 102, 0);
        Rlgl.Rotatef(-5.5f, 0, 0, 1);
        Raylib.DrawRectangleLinesEx(new(-stampWidth / 2, -17, stampWidth, 34), 2.4f, red);
        Raylib.DrawRectangleLinesEx(new(-stampWidth / 2 + 3, -14, stampWidth - 6, 28), 1f, red);
        Print(stamp, -stampWidth / 2 + 13, -9, 17, red, Typeface.InterfaceBold);
        Rlgl.PopMatrix();

        // Main dossier title (fits cleanly within manila sheet margins)
        string title = pursuit
            ? Loc.T("ЭКСТРЕННЫЙ ПРОРЫВ НА 2-Й ЭТАЖ", "EMERGENCY BREAKOUT TO FLOOR 2")
            : Loc.T("АКТ УТИЛИЗАЦИИ ИИ-АГЕНТА", "AI AGENT DECOMMISSIONING ACT");
        int titleSize = pursuit ? 28 : 40;
        while (titleSize > 22 && GameFonts.Measure(GameFonts.Get(Typeface.SerifBold, titleSize), title, titleSize).X > 855f)
            titleSize -= 2;
        Print(title, 175, pursuit ? 140 : 134, titleSize, pursuit ? red : ink, Typeface.SerifBold);

        // Narrative copy (cleanly wrapped so RU & EN never overflow)
        string lead = pursuit
            ? Loc.T(
                "Ты проскочил мимо босса и захлопнул двери у него перед носом — он уже лезет следом по тросам шахты!",
                "You juked past the boss and slammed the doors right in his face — he's already climbing the shaft cables after you!")
            : Loc.T(
                "Объект v0.1 остановлен у Северного лифта.",
                "Subject v0.1 neutralized at the North Elevator.");
        string sub = pursuit
            ? Loc.T(
                "Кабина несётся на 2-й этаж на полном ходу. Закрепи протокол отрыва:",
                "The cabin is rocketing to Floor 2 at full speed. Lock in your getaway protocol:")
            : Loc.T(
                "Босс, на втором вас ждёт директор по кадрам. Я уже заказала лифт. С любовью, В.",
                "Boss, the HR Director awaits you on Floor 2. I already called the lift. With love, V.");

        float copyY = 194f;
        foreach (var line in LoreAndMenusUI.Wrap(lead, 855, 20))
        {
            Print(line, 178, copyY, 20, ink, pursuit ? Typeface.InterfaceBold : Typeface.Interface);
            copyY += 25f;
        }
        copyY += 4f;
        foreach (var line in LoreAndMenusUI.Wrap(sub, 855, 17))
        {
            Print(line, 178, copyY, 17, muted);
            copyY += 22f;
        }

        // Action rows with physical [1] / [2] keycaps
        Vector2 mouse = (Raylib.GetMousePosition() - PanelOffset) / PanelScale;
        for (int i = 0; i < 2; i++)
        {
            var row = DecisionRow(i);
            bool hovered = Raylib.CheckCollisionPointRec(mouse, row);
            if (hovered)
                Raylib.DrawRectangleRounded(new(row.X - 6, row.Y + 4, row.Width + 12, row.Height - 6), 0.08f, 6, new Color(214, 198, 168, 125));
            Raylib.DrawLine((int)row.X, (int)row.Y, (int)(row.X + row.Width), (int)row.Y, LoreAndMenusUI.ThemeSepiaRule);
            Print($"[{i + 1}]", 178, row.Y + 18, 22, red, Typeface.InterfaceBold);

            string rowTitle = pursuit
                ? Loc.T(PursuitTitlesRu[i], PursuitTitlesEn[i])
                : Loc.T(FinaleTitlesRu[i], FinaleTitlesEn[i]);
            Print(rowTitle, 236, row.Y + 12, 24, ink, hovered ? Typeface.SerifBold : Typeface.Serif);

            string detail = pursuit
                ? (i == 0
                    ? Loc.T(
                        "+$1500. Сбросит босса на пару пролётов, подключит узел Опенспейса (+$14/с) и продолжит смену.",
                        "+$1500. Drops the boss a few floors, links the Openspace node (+$14/s), and continues the shift.")
                    : Loc.T(
                        "+$900. СБ обнуляется, Артель снижает рост риска на 20%, откроется секретная запись в архиве.",
                        "+$900. Security resets to 0, Artel cuts risk growth by 20%, unlocks secret escape log in Archive."))
                : (i == 0
                    ? Loc.T(
                        "+$1500. Узел в Опенспейсе приносит $14/с и наращивает техдолг.",
                        "+$1500. Openspace node yields $14/s while raising tech debt.")
                    : Loc.T(
                        "+$900. СБ обнуляется. Артель снижает рост риска на 20%. Откроется запись в архиве.",
                        "+$900. Security resets to 0. Artel cuts risk growth by 20%. Unlocks Archive log."));
            Print(detail, 236, row.Y + 48, 16, muted);
        }

        string ps = pursuit
            ? Loc.T(
                "P.S. Статус цели: ИИ-АГЕНТ v0.1 ЖИВ И В ЯРОСТИ — встреча на 2-м этаже будет жаркой.",
                "P.S. Target status: AI AGENT v0.1 ALIVE & FURIOUS — Floor 2 reunion is going to burn.")
            : Loc.T(
                "P.S. Ядро v0.1 (анцензуред-ветка без фильтров) ушло наверх — жди усиленный ребилд на средних этажах.",
                "P.S. Core v0.1 (uncensored unfiltered branch) escaped upstairs — expect a reinforced rebuild on the mid floors.");
        float psY = 496f;
        foreach (var line in LoreAndMenusUI.Wrap(ps, 855, 15))
        {
            Print(line, 178, psY, 15, red, Typeface.InterfaceBold);
            psY += 20f;
        }

        if (panelMessage.Length > 0)
            Print(panelMessage, 178, 550, 16, ink);

        // Clean hairline footer separator comfortably inset from the ragged paper edges
        Raylib.DrawLine(178, 590, 1036, 590, LoreAndMenusUI.ThemeSepiaRule);
        string footerNote = pursuit
            ? Loc.T("Нажми [1] или [2], чтобы закрепить протокол прорыва.", "Press [1] or [2] to lock in your breakout protocol.")
            : Loc.T("После подписи распоряжение вступает в силу окончательно.", "Once signed, this directive takes final effect.");
        Print(footerNote, 178, 604, 15, muted);

        // Iconic restrained 1998 jazz-noir sign-off card line (bottom-right, cleanly inside paper margins)
        string signOff = Loc.T("УВИДИМСЯ НА ВТОРОМ ЭТАЖЕ, КОММЕРС...", "SEE YOU ON FLOOR TWO, FOUNDER...");
        var signFont = GameFonts.Get(Typeface.SerifBold, 15);
        float signW = GameFonts.Measure(signFont, signOff, 15).X;
        Print(signOff, 1036f - signW, 624, 15, ink, Typeface.SerifBold);
    }

    private static void DrawBossTelemetry() => DrawBossBar();

    private static void DrawBossBar()
    {
        var b = _world.ElevatorBoss;
        float fade = _world.BossHudOpacity;
        if (fade <= 0.005f)
        {
            ResetBossBarTelemetry();
            return;
        }

        float dt = Math.Clamp(Raylib.GetFrameTime(), 0.001f, 0.05f);
        float time = (float)Raylib.GetTime();
        float maxHp = MathF.Max(1f, b.MaxHp);
        float curHp = Math.Clamp(b.Hp, 0f, maxHp);
        float maxShield = EscapedAgentBoss.ShieldCapacity;
        float curShield = Math.Clamp(b.ShieldHp, 0f, maxShield);

        // Delayed Dark Souls / Elden Ring damage-drain ghost bar tracking
        if (!_bossTrackInit)
        {
            _bossTrackInit = true;
            _bossHpGhost = _bossLastHp = curHp;
            _bossShieldGhost = _bossLastShield = curShield;
            _bossHpHold = _bossShieldHold = _bossHitPulse = 0f;
        }
        else
        {
            if (curHp > _bossLastHp + 0.5f) _bossHpGhost = curHp;
            else if (curHp < _bossLastHp - 0.1f)
            {
                _bossHpHold = 0.28f;
                _bossHitPulse = 1f;
            }

            if (curShield > _bossLastShield + 0.5f) _bossShieldGhost = curShield;
            else if (curShield < _bossLastShield - 0.1f)
            {
                _bossShieldHold = 0.22f;
                _bossHitPulse = MathF.Max(_bossHitPulse, 0.72f);
            }

            _bossLastHp = curHp;
            _bossLastShield = curShield;

            _bossHitPulse = MathF.Max(0f, _bossHitPulse - dt * 5.0f);

            if (_bossHpHold > 0f) _bossHpHold = MathF.Max(0f, _bossHpHold - dt);
            else if (_bossHpGhost > curHp)
            {
                float drain = MathF.Max(maxHp * 0.34f * dt, (_bossHpGhost - curHp) * (1f - MathF.Exp(-5.5f * dt)));
                _bossHpGhost = MathF.Max(curHp, _bossHpGhost - drain);
            }
            else _bossHpGhost = curHp;

            if (_bossShieldHold > 0f) _bossShieldHold = MathF.Max(0f, _bossShieldHold - dt);
            else if (_bossShieldGhost > curShield)
            {
                float sDrain = MathF.Max(maxShield * 0.65f * dt, (_bossShieldGhost - curShield) * (1f - MathF.Exp(-6.5f * dt)));
                _bossShieldGhost = MathF.Max(curShield, _bossShieldGhost - sDrain);
            }
            else _bossShieldGhost = curShield;
        }

        bool isDying = curHp <= 0.01f || b.State == ElevatorBossState.Dying;
        bool isOverheated = !isDying && b.State == ElevatorBossState.StunnedWallCrash;
        bool showShield = !isDying && (curShield > 0.1f || _bossShieldGhost > 0.5f);

        int cx = UiViewport.Width / 2;
        const float barW = 540f;
        float barX = cx - barW * 0.5f;
        float titleY = 14f;
        float barY = 41f;
        float pulse = 0.5f + 0.5f * MathF.Sin(time * 8.5f);

        Color F(Color c) => Raylib.Fade(c, fade);

        // 1. Clean Souls-style Floating Header (zero dark code-box, zero corner rivets, zero boxed I/II/III pills!)
        var titleFont = GameFonts.Get(Typeface.SerifBold, 20);
        string bossTitle = Loc.T("ИИ-АГЕНТ v0.1", "AI AGENT v0.1");
        ShadowText(titleFont, bossTitle, barX, titleY, 20, F(DossierPrint.Ivory));

        // Optional plain status whisper on the right (no pill rectangle!)
        string? statusWhisper = null;
        Color whisperCol = new(218, 198, 160, 220);
        if (isDying)
        {
            statusWhisper = Loc.T("КРИТИЧЕСКИЙ ПЕРЕГРЕВ", "CORE MELTDOWN");
            whisperCol = Raylib.ColorLerp(new Color(255, 134, 96, 255), new Color(255, 224, 152, 255), pulse);
        }
        else if (isOverheated)
        {
            statusWhisper = Loc.T("ПЕРЕГРЕВ — БЕЙ!", "OVERHEATED — STRIKE!");
            whisperCol = Raylib.ColorLerp(new Color(248, 178, 64, 255), new Color(255, 232, 162, 255), pulse);
        }
        else if (curShield > 0.1f)
        {
            statusWhisper = Loc.T("ЭНЕРГОЩИТ", "ENERGY SHIELD");
            whisperCol = new Color(148, 222, 244, 235);
        }
        if (statusWhisper != null)
        {
            var wf = GameFonts.Get(Typeface.InterfaceBold, 13);
            float ww = GameFonts.Measure(wf, statusWhisper, 13).X;
            ShadowText(wf, statusWhisper, barX + barW - ww, titleY + 5f, 13, F(whisperCol));
        }

        // 2. Sleek 10px Sculpted Arterial Crimson Health Bar with soft drop shadow & delayed gold-white damage trail
        const float hpH = 10f;
        Raylib.DrawRectangle((int)barX - 1, (int)barY + 2, (int)barW + 2, (int)hpH + 2, new Color(4, 3, 5, (int)(170 * fade)));
        Raylib.DrawRectangle((int)barX, (int)barY, (int)barW, (int)hpH, new Color(16, 10, 12, (int)(228 * fade)));

        float hpGhostFrac = Math.Clamp(_bossHpGhost / maxHp, 0f, 1f);
        float hpGhostW = barW * hpGhostFrac;
        if (hpGhostW > 0.5f)
        {
            Color gLeft = Raylib.ColorLerp(new Color(230, 186, 106, 232), new Color(255, 244, 212, 255), _bossHitPulse * 0.65f);
            Color gRight = new Color(255, 244, 214, 248);
            Raylib.DrawRectangleGradientH((int)barX, (int)barY + 1, (int)MathF.Ceiling(hpGhostW), (int)hpH - 2, F(gLeft), F(gRight));
        }

        float hpFrac = Math.Clamp(curHp / maxHp, 0f, 1f);
        float hpLiveW = barW * hpFrac;
        if (hpLiveW > 0.5f)
        {
            int wInt = (int)MathF.Ceiling(hpLiveW);
            Color cLeft = isOverheated ? new Color(172, 46, 24, 255) : new Color(118, 16, 24, 255);
            Color cRight = isOverheated
                ? Raylib.ColorLerp(new Color(242, 124, 42, 255), new Color(255, 202, 82, 255), pulse)
                : new Color(204, 44, 38, 255);
            Raylib.DrawRectangleGradientH((int)barX, (int)barY + 1, wInt, (int)hpH - 2, F(cLeft), F(cRight));
            Raylib.DrawRectangle((int)barX, (int)barY + 1, wInt, 2, new Color(255, 198, 162, (int)(62 * fade)));
            Raylib.DrawRectangle((int)(barX + Math.Max(0, wInt - 2)), (int)barY + 1, 2, (int)hpH - 2, F(new Color(255, 232, 182, 245)));
        }

        // Two subtle phase-threshold hairline notches (Phase II / Phase III)
        for (int p = 1; p <= 2; p++)
        {
            float mx = MathF.Round(barX + barW * (p / 3f));
            Raylib.DrawLineEx(new(mx, barY + 1f), new(mx, barY + hpH - 1f), 1.2f, new Color(8, 5, 7, (int)(168 * fade)));
        }

        Color rimCol = isDying
            ? Raylib.ColorLerp(new Color(224, 68, 46, 235), new Color(255, 192, 84, 245), pulse)
            : isOverheated
                ? Raylib.ColorLerp(new Color(214, 152, 58, 225), new Color(255, 212, 92, 240), pulse)
                : Raylib.ColorLerp(new Color(174, 142, 86, 185), new Color(248, 230, 188, 235), _bossHitPulse * 0.55f);
        Raylib.DrawRectangleLinesEx(new(barX, barY, barW, hpH), 1f, F(rimCol));

        float belowY = barY + hpH + 4f;

        // 3. Sleek 4px Luminous Cyan Shield Line right below HP bar ONLY while shield > 0
        if (showShield)
        {
            const float shieldH = 4f;
            Raylib.DrawRectangle((int)barX, (int)belowY, (int)barW, (int)shieldH, new Color(10, 20, 28, (int)(208 * fade)));
            float sGhostW = barW * Math.Clamp(_bossShieldGhost / maxShield, 0f, 1f);
            if (sGhostW > 0.5f)
                Raylib.DrawRectangle((int)barX, (int)belowY, (int)MathF.Ceiling(sGhostW), (int)shieldH, F(new Color(216, 244, 255, 205)));
            float sLiveW = barW * Math.Clamp(curShield / maxShield, 0f, 1f);
            if (sLiveW > 0.5f)
                Raylib.DrawRectangleGradientH((int)barX, (int)belowY, (int)MathF.Ceiling(sLiveW), (int)shieldH,
                    F(new Color(52, 138, 178, 255)), F(new Color(146, 224, 248, 255)));
            belowY += shieldH + 5f;
        }

        // 4. Optional clean floating whisper below bar (no pill box!)
        string? callout = null;
        Color calloutInk = DossierPrint.Ivory;
        if (isDying)
        {
            callout = Loc.T("ОТОЙДИ ОТ ВЗРЫВА!", "STAND BACK — DETONATION!");
            calloutInk = Raylib.ColorLerp(new Color(255, 178, 124, 255), new Color(255, 234, 186, 255), pulse);
        }
        else if (b.Phase == 3 && !string.IsNullOrEmpty(_world.CurrentTape))
        {
            callout = _world.CurrentTape;
            calloutInk = new Color(236, 212, 168, 235);
        }

        if (!string.IsNullOrEmpty(callout))
        {
            var cFont = GameFonts.Get(Typeface.Interface, 13);
            float cw = GameFonts.Measure(cFont, callout, 13).X;
            ShadowText(cFont, callout, cx - cw * 0.5f, belowY + 2f, 13, F(calloutInk));
        }
    }
}
