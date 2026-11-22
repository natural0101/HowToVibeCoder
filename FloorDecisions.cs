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
        const float plateW = 640f;
        float left = cx - plateW * 0.5f;
        float top = 12f;
        float plateH = showShield ? 80f : 70f;
        const float padX = 18f;
        float pulse = 0.5f + 0.5f * MathF.Sin(time * 8.5f);

        Color F(Color c) => Raylib.Fade(c, fade);

        // 1. Heavy Dark-Obsidian Architectural Backing Plate with subtle brushed-brass corner bevels
        Raylib.DrawRectangle((int)left + 3, (int)top + 4, (int)plateW, (int)plateH, new Color(5, 4, 7, (int)(168 * fade)));
        Raylib.DrawRectangleGradientV(
            (int)left, (int)top, (int)plateW, (int)plateH,
            new Color(23, 20, 26, (int)(236 * fade)),
            new Color(11, 10, 14, (int)(244 * fade)));

        // Warm amber / ember inner glow wash when Overheated or Core Meltdown
        if (isOverheated || isDying)
        {
            Color warnGlow = isDying
                ? new Color(224, 62, 42, (int)((32 + 44 * pulse) * fade))
                : new Color(242, 168, 52, (int)((26 + 40 * pulse) * fade));
            Raylib.DrawRectangleGradientV((int)left + 2, (int)top + 2, (int)plateW - 4, 14, warnGlow, Color.Blank);
            Raylib.DrawRectangleGradientV((int)left + 2, (int)(top + plateH - 16), (int)plateW - 4, 14, Color.Blank, warnGlow);
        }

        Color brassBase = new(186, 152, 92, 205);
        Color frameCol = isDying
            ? Raylib.ColorLerp(new Color(214, 58, 44, 245), new Color(252, 178, 72, 255), pulse)
            : isOverheated
                ? Raylib.ColorLerp(new Color(204, 148, 56, 235), new Color(255, 202, 82, 255), pulse)
                : Raylib.ColorLerp(brassBase, new Color(248, 232, 192, 245), _bossHitPulse * 0.55f);

        Raylib.DrawRectangleLinesEx(new(left, top, plateW, plateH), (isOverheated || isDying) ? 1.8f : 1.3f, F(frameCol));
        Raylib.DrawLineEx(new(left + 10, top + 1.5f), new(left + plateW - 10, top + 1.5f), 1f, new Color(240, 214, 158, (int)(44 * fade)));

        // Subtle Art-Deco brushed-brass architectural corner chamfers & machined corner rivets (zero protruding L-brackets)
        Color bevelCol = F(Raylib.ColorLerp(frameCol, new Color(242, 214, 152, 255), 0.35f));
        const float ch = 8f;
        Raylib.DrawLineEx(new(left + 1, top + ch), new(left + ch, top + 1), 1.3f, bevelCol);
        Raylib.DrawLineEx(new(left + plateW - ch, top + 1), new(left + plateW - 1, top + ch), 1.3f, bevelCol);
        Raylib.DrawLineEx(new(left + 1, top + plateH - ch), new(left + ch, top + plateH - 1), 1.3f, bevelCol);
        Raylib.DrawLineEx(new(left + plateW - ch, top + plateH - 1), new(left + plateW - 1, top + plateH - ch), 1.3f, bevelCol);

        Color rivetCol = F(new Color(212, 178, 114, 215));
        Raylib.DrawCircleV(new(left + 7f, top + 7f), 1.5f, rivetCol);
        Raylib.DrawCircleV(new(left + plateW - 7f, top + 7f), 1.5f, rivetCol);
        Raylib.DrawCircleV(new(left + 7f, top + plateH - 7f), 1.5f, rivetCol);
        Raylib.DrawCircleV(new(left + plateW - 7f, top + plateH - 7f), 1.5f, rivetCol);

        // 2. Header Row: Bold Serif Boss Title + 3 Phase Pips (I · II · III) + Clear Status Badge + Numeric HP Readout
        float headerY = top + 9f;
        var titleFont = GameFonts.Get(Typeface.SerifBold, 19);
        string bossTitle = Loc.T("ИИ-АГЕНТ v0.1", "AI AGENT v0.1");
        ShadowText(titleFont, bossTitle, left + padX, headerY, 19, F(DossierPrint.Ivory));
        float cursorX = left + padX + GameFonts.Measure(titleFont, bossTitle, 19).X + 14f;

        // 3 Phase Pips: I · II · III
        var pipFont = GameFonts.Get(Typeface.InterfaceBold, 12);
        string[] romans = ["I", "II", "III"];
        int activePhase = Math.Clamp(b.Phase, 1, 3);
        for (int p = 1; p <= 3; p++)
        {
            bool spent = p < activePhase || isDying;
            bool current = p == activePhase && !isDying;
            Rectangle pip = new(cursorX, headerY + 2f, 24f, 18f);
            Color pipBg = current
                ? new Color(148, 34, 38, (int)(235 * fade))
                : spent
                    ? new Color(24, 22, 26, (int)(185 * fade))
                    : new Color(20, 18, 22, (int)(220 * fade));
            Color pipBorder = current
                ? F(new Color(238, 198, 116, 245))
                : spent
                    ? F(new Color(98, 84, 66, 130))
                    : F(new Color(172, 140, 86, 190));
            Color pipInk = current
                ? F(new Color(252, 244, 226, 255))
                : spent
                    ? F(new Color(126, 116, 102, 165))
                    : F(new Color(224, 208, 180, 225));

            Raylib.DrawRectangleRounded(pip, 0.24f, 4, pipBg);
            Raylib.DrawRectangleRoundedLinesEx(pip, 0.24f, 4, current ? 1.3f : 1f, pipBorder);
            float rw = GameFonts.Measure(pipFont, romans[p - 1], 12).X;
            GameFonts.Draw(pipFont, romans[p - 1], new(pip.X + (pip.Width - rw) * 0.5f, pip.Y + 3f), 12, pipInk, shadowed: false);
            cursorX += 29f;
        }

        // Status Badge: ЩИТ / ЯДРО / ПЕРЕГРЕВ! / ВЗРЫВ ЯДРА!
        cursorX += 6f;
        string badgeText;
        Color badgeBg, badgeBorder, badgeInk;
        if (isDying)
        {
            badgeText = Loc.T("ВЗРЫВ ЯДРА!", "CORE MELTDOWN!");
            badgeBg = Raylib.ColorLerp(new Color(176, 36, 34, 242), new Color(228, 92, 42, 248), pulse);
            badgeBorder = new Color(255, 208, 112, 250);
            badgeInk = new Color(255, 248, 232, 255);
        }
        else if (isOverheated)
        {
            badgeText = Loc.T("ПЕРЕГРЕВ!", "OVERHEATED!");
            badgeBg = Raylib.ColorLerp(new Color(206, 138, 38, 242), new Color(248, 186, 62, 250), pulse);
            badgeBorder = new Color(255, 232, 164, 250);
            badgeInk = new Color(20, 14, 10, 255);
        }
        else if (curShield > 0.1f)
        {
            badgeText = Loc.T("ЩИТ", "SHIELD");
            badgeBg = new Color(36, 82, 106, 234);
            badgeBorder = new Color(142, 210, 232, 232);
            badgeInk = new Color(238, 248, 252, 255);
        }
        else
        {
            badgeText = Loc.T("ЯДРО", "CORE");
            badgeBg = new Color(116, 26, 32, 234);
            badgeBorder = new Color(214, 168, 96, 215);
            badgeInk = new Color(250, 240, 224, 255);
        }

        var badgeFont = GameFonts.Get(Typeface.InterfaceBold, 12);
        float badgeTextW = GameFonts.Measure(badgeFont, badgeText, 12).X;
        Rectangle badgeRect = new(cursorX, headerY + 2f, badgeTextW + 14f, 18f);
        Raylib.DrawRectangleRounded(badgeRect, 0.24f, 4, F(badgeBg));
        Raylib.DrawRectangleRoundedLinesEx(badgeRect, 0.24f, 4, 1.1f, F(badgeBorder));
        GameFonts.Draw(badgeFont, badgeText, new(badgeRect.X + 7f, badgeRect.Y + 3f), 12, F(badgeInk), shadowed: false);

        // Right-aligned Numeric HP Readout (420 / 600)
        var hpFont = GameFonts.Get(Typeface.InterfaceBold, 15);
        int hpDisp = Math.Max(0, (int)MathF.Ceiling(curHp));
        int maxDisp = Math.Max(1, (int)MathF.Round(maxHp));
        string hpReadout = $"{hpDisp} / {maxDisp}";
        float hpW = GameFonts.Measure(hpFont, hpReadout, 15).X;
        ShadowText(hpFont, hpReadout, left + plateW - padX - hpW, headerY + 3f, 15, F(new Color(238, 226, 202, 242)));

        // 3. Dual-Layer Thick Health + Shield Bars
        float barX = left + padX;
        float barW = plateW - padX * 2f;
        float barY = top + 36f;

        if (showShield)
        {
            const float shieldH = 8f;
            Raylib.DrawRectangle((int)barX, (int)barY, (int)barW, (int)shieldH, new Color(14, 22, 30, (int)(236 * fade)));

            float sGhostFrac = Math.Clamp(_bossShieldGhost / maxShield, 0f, 1f);
            float sGhostW = barW * sGhostFrac;
            if (sGhostW > 0.5f)
                Raylib.DrawRectangle((int)barX, (int)barY, (int)MathF.Ceiling(sGhostW), (int)shieldH, F(new Color(220, 242, 250, 220)));

            float sFrac = Math.Clamp(curShield / maxShield, 0f, 1f);
            float sLiveW = barW * sFrac;
            if (sLiveW > 0.5f)
            {
                int wInt = (int)MathF.Ceiling(sLiveW);
                Raylib.DrawRectangleGradientH((int)barX, (int)barY, wInt, (int)shieldH,
                    F(new Color(54, 122, 152, 255)), F(new Color(142, 214, 236, 255)));
                Raylib.DrawRectangle((int)barX, (int)barY, wInt, 2, new Color(224, 248, 255, (int)(92 * fade)));
                Raylib.DrawRectangle((int)(barX + wInt - 2), (int)barY, 2, (int)shieldH, F(new Color(232, 250, 255, 245)));
            }

            Raylib.DrawRectangleLinesEx(new(barX, barY, barW, shieldH), 1f, new Color(108, 166, 188, (int)(135 * fade)));
            barY += shieldH + 3f;
        }

        // Main HP Bar: 18px tall, arterial crimson-to-ember gradient + delayed golden-white drain ghost + 100-HP tick marks
        const float hpH = 18f;
        Raylib.DrawRectangle((int)barX, (int)barY, (int)barW, (int)hpH, new Color(18, 12, 14, (int)(244 * fade)));
        Raylib.DrawRectangle((int)barX, (int)barY, (int)barW, 2, new Color(6, 4, 6, (int)(185 * fade)));

        float hpGhostFrac = Math.Clamp(_bossHpGhost / maxHp, 0f, 1f);
        float hpGhostW = barW * hpGhostFrac;
        if (hpGhostW > 0.5f)
        {
            Color gLeft = Raylib.ColorLerp(new Color(234, 194, 114, 238), new Color(255, 244, 212, 255), _bossHitPulse * 0.65f);
            Color gRight = new Color(255, 244, 214, 248);
            Raylib.DrawRectangleGradientH((int)barX, (int)barY + 1, (int)MathF.Ceiling(hpGhostW), (int)hpH - 2, F(gLeft), F(gRight));
        }

        float hpFrac = Math.Clamp(curHp / maxHp, 0f, 1f);
        float hpLiveW = barW * hpFrac;
        if (hpLiveW > 0.5f)
        {
            int wInt = (int)MathF.Ceiling(hpLiveW);
            Color cLeft = isOverheated ? new Color(168, 42, 24, 255) : new Color(122, 18, 26, 255);
            Color cRight = isOverheated
                ? Raylib.ColorLerp(new Color(238, 122, 42, 255), new Color(255, 196, 78, 255), pulse)
                : new Color(214, 56, 42, 255);
            Raylib.DrawRectangleGradientH((int)barX, (int)barY + 1, wInt, (int)hpH - 2, F(cLeft), F(cRight));
            // Subtle upper specular bevel across live bar
            Raylib.DrawRectangle((int)barX, (int)barY + 1, wInt, 4, new Color(255, 186, 146, (int)(54 * fade)));
            // Crisp golden-ivory leading edge at live HP boundary
            Raylib.DrawRectangle((int)(barX + Math.Max(0, wInt - 2)), (int)barY + 1, 2, (int)hpH - 2, F(new Color(255, 232, 182, 245)));
        }

        // Subtle engraved 100-HP segmented tick marks across the 18px bar
        for (float mark = 100f; mark < maxHp - 1f; mark += 100f)
        {
            float mx = MathF.Round(barX + barW * (mark / maxHp));
            Raylib.DrawLineEx(new(mx, barY + 1f), new(mx, barY + hpH - 1f), 1.4f, new Color(10, 7, 9, (int)(152 * fade)));
            Raylib.DrawLineEx(new(mx + 1.3f, barY + 2f), new(mx + 1.3f, barY + hpH - 2f), 0.9f, new Color(248, 224, 178, (int)(26 * fade)));
        }

        Raylib.DrawRectangleLinesEx(new(barX, barY, barW, hpH), 1.1f, F(new Color(142, 114, 72, 175)));

        // 4. Contextual Callout Pill tucked cleanly below the Boss Bar (Overheat warning / Meltdown / Phase 3 Tape)
        string? callout = null;
        Color calloutInk = DossierPrint.Ivory;
        Color calloutBorder = new(186, 152, 92, 128);
        if (isDying)
        {
            callout = Loc.T("КРИТИЧЕСКИЙ ПЕРЕГРЕВ ЯДРА — ОТОЙДИ!", "CORE MELTDOWN — STAND BACK!");
            calloutInk = Raylib.ColorLerp(new Color(255, 178, 124, 255), new Color(255, 234, 186, 255), pulse);
            calloutBorder = new Color(232, 76, 52, 210);
        }
        else if (isOverheated)
        {
            callout = Loc.T("ЯДРО ОТКРЫТО — БЕЙ!", "CORE EXPOSED — STRIKE NOW!");
            calloutInk = Raylib.ColorLerp(new Color(252, 192, 76, 255), new Color(255, 238, 188, 255), pulse);
            calloutBorder = new Color(246, 178, 62, 215);
        }
        else if (b.Phase == 3 && !string.IsNullOrEmpty(_world.CurrentTape))
        {
            callout = _world.CurrentTape;
            calloutInk = DossierPrint.Ivory;
            calloutBorder = new Color(186, 152, 92, 118);
        }

        if (!string.IsNullOrEmpty(callout))
        {
            var cFont = GameFonts.Get(Typeface.InterfaceBold, 14);
            float cw = MathF.Min(plateW, GameFonts.Measure(cFont, callout, 14).X + 24f);
            Rectangle pill = new(cx - cw * 0.5f, top + plateH + 5f, cw, 22f);
            Raylib.DrawRectangleRounded(pill, 0.26f, 6, new Color(14, 11, 16, (int)(214 * fade)));
            Raylib.DrawRectangleRoundedLinesEx(pill, 0.26f, 6, 1f, F(calloutBorder));
            float tw = GameFonts.Measure(cFont, callout, 14).X;
            ShadowText(cFont, callout, cx - tw * 0.5f, pill.Y + 4f, 14, F(calloutInk));
        }
    }
}
