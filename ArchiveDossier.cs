#nullable enable
using System.Numerics;
using Raylib_cs;
using static HowToVibeCoder.DossierPrint;

namespace HowToVibeCoder;

public static partial class LoreAndMenusUI
{
    private const int ArchiveWidth = 1280, ArchiveHeight = 720, RowHeight = 68, VisibleRows = 5;
    private static readonly string[] ArchiveTabs = ["Переписка", "Досье HR", "Коллекция", "Словарь"];
    private static readonly string[] IndexTitles = ["Переписка цеха", "Таро-досье HR", "Картотека", "Словарь цеха"];
    private static readonly string[] ChatNames = ["Данила", "Петрович", "Крипто-Бро", "Тимлид-Митингер", "Сеньор Помидор", "Лог-файл 01","Агент v0.1"];
    private static readonly string[] ChatTitlesUnlocked = ["Пятничный деплой", "Стойка B12 и кулер", "Охота на Жадность", "Дейлик и Эспрессо", "Ядро ZERO и Техдолг", "Дело «ЭНТРОПИК»","Пятидесятый этаж"];
    private static readonly string[] ChatTitlesRedacted = ["Пятничный деплой", "Стойка B12 и кулер", "Охота на Жадность", "Дейлик и Эспрессо", "Засекреченный перехват №05", "Засекреченная запись №06","Пятидесятый этаж"];
    private static string[] ChatTitles => BossTwistUnlocked ? ChatTitlesUnlocked : ChatTitlesRedacted;
    private static readonly string[] TarotNames = ["Сеньор Алексей", "Джуниор Данила", "Крипто и Митингер","Личный конверт Вероники"];
    private static readonly string[] TarotCards = ["Аркан XVI: Башня", "Аркан 0: Шут", "Аркан XV: Дьявол","Ведомость замещений"];
    private static int Catches(int i) => Program.Discoveries.GetValueOrDefault(VibeCoderCollection.All[i].Id);
    internal static string CollectionEntry(int i) => Catches(i)>0 ? VibeCoderCollection.All[i].Name : "??? — НЕ ПОЙМАН";
    private const string CoderModel = "char_vibe_coder", TarotModel = "char_hr_veronika",
                         AgentModel = "escaped_ai_boss", PartnerModel = "char_partner_90s", SolderModel = "weapon_solder_90s";
    private static readonly Rectangle Selector = new(44, 232, 286, VisibleRows * RowHeight);
    private static readonly Rectangle Exhibit = new(328, 134, 386, 514);
    private static RenderTexture2D _exhibitTexture;
    private static Texture2D _deskBackdrop, _folderSheet, _corkboard, _contractNote, _pauseClipboard, _mixerConsole;
    private static bool _archiveArtTried;
    private static float _exhibitTime;

    private static readonly Color BrassBright = new(218, 179, 108, 255);
    private static readonly Color BrassDim    = new(146, 114, 66, 255);
    private static readonly Color WalnutDark  = new(27, 18, 13, 248);
    private static readonly Color WalnutEdge  = new(74, 52, 36, 255);
    private static readonly Color KraftCard   = new(232, 217, 186, 255);
    private static readonly Color KraftShadow = new(184, 165, 132, 255);
    private static readonly Color SepiaInk    = new(52, 38, 26, 255);
    private static readonly Color SepiaMuted  = new(116, 94, 72, 255);
    private static readonly Color SepiaRule   = new(168, 144, 114, 195);
    private static readonly Color StampRed    = new(156, 26, 36, 232);
    private static readonly Color StampSlate  = new(96, 86, 74, 220);

    private static string ResolveUiAsset(string fileName)
    {
        string[] roots =
        [
            Path.Combine(AppContext.BaseDirectory, "assets", "ui"),
            Path.Combine(Directory.GetCurrentDirectory(), "assets", "ui"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "assets", "ui")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "assets", "ui"))
        ];
        foreach (string dir in roots)
        {
            string full = Path.Combine(dir, fileName);
            if (File.Exists(full)) return full;
        }
        return Path.Combine(AppContext.BaseDirectory, "assets", "ui", fileName);
    }

    private static void EnsureArchiveArt()
    {
        if (_archiveArtTried) return;
        _archiveArtTried = true;
        static Texture2D LoadUi(string name)
        {
            string p = ResolveUiAsset(name);
            if (!File.Exists(p)) return default;
            var t = Raylib.LoadTexture(p);
            Raylib.SetTextureFilter(t, TextureFilter.Bilinear);
            return t;
        }
        _deskBackdrop    = LoadUi("archive_desk_bg.png");
        _folderSheet     = LoadUi("archive_folder_sheet.png");
        _corkboard       = LoadUi("contracts_corkboard.png");
        _contractNote    = LoadUi("contract_note_card.png");
        _pauseClipboard  = LoadUi("pause_clipboard.png");
        _mixerConsole    = LoadUi("mixer_console.png");
    }

    internal static void DrawCorkboard(float w = ArchiveWidth, float h = ArchiveHeight)
    {
        EnsureArchiveArt();
        if (_corkboard.Id != 0)
            Raylib.DrawTexturePro(_corkboard, new(0, 0, _corkboard.Width, _corkboard.Height), new(0, 0, w, h), Vector2.Zero, 0f, Color.White);
        else
            Raylib.DrawRectangle(0, 0, (int)w, (int)h, WalnutDark);
    }

    internal static void DrawContractNote(Rectangle bounds, float angleDeg, bool selected)
    {
        EnsureArchiveArt();
        if (_contractNote.Id == 0)
        {
            Raylib.DrawRectangleRec(bounds, KraftCard);
            return;
        }
        float scale = selected ? 1.035f : 1.0f;
        float w = bounds.Width * scale, h = bounds.Height * scale;
        Rectangle dst = new(bounds.X + bounds.Width * 0.5f, bounds.Y + bounds.Height * 0.5f, w, h);
        Color tint = selected ? Color.White : new Color(224, 214, 198, 255);
        Raylib.DrawTexturePro(_contractNote, new(0, 0, _contractNote.Width, _contractNote.Height), dst, new(w * 0.5f, h * 0.5f), angleDeg, tint);
    }

    internal static void DrawPauseClipboard(Rectangle bounds)
    {
        EnsureArchiveArt();
        if (_pauseClipboard.Id != 0)
            Raylib.DrawTexturePro(_pauseClipboard, new(0, 0, _pauseClipboard.Width, _pauseClipboard.Height), bounds, Vector2.Zero, 0f, Color.White);
        else
            DrawBrassWalnutFrame(bounds);
    }

    internal static void DrawMixerConsole(Rectangle bounds)
    {
        EnsureArchiveArt();
        if (_mixerConsole.Id != 0)
            Raylib.DrawTexturePro(_mixerConsole, new(0, 0, _mixerConsole.Width, _mixerConsole.Height), bounds, Vector2.Zero, 0f, Color.White);
        else
            DrawBrassWalnutFrame(bounds);
    }

    internal static void DrawDeskBackdrop(float w = ArchiveWidth, float h = ArchiveHeight)
    {
        EnsureArchiveArt();
        if (_deskBackdrop.Id != 0)
            Raylib.DrawTexturePro(_deskBackdrop, new(0, 0, _deskBackdrop.Width, _deskBackdrop.Height), new(0, 0, w, h), Vector2.Zero, 0f, Color.White);
        else
            Raylib.DrawRectangle(0, 0, (int)w, (int)h, WalnutDark);
    }

    internal static void DrawManilaPaper(Rectangle dst)
    {
        EnsureArchiveArt();
        if (_folderSheet.Id != 0)
            Raylib.DrawTexturePro(_folderSheet, new(0, 0, _folderSheet.Width, _folderSheet.Height), dst, Vector2.Zero, 0f, Color.White);
        else
            Sheet(dst);
    }

    internal static void DrawBrassWalnutFrame(Rectangle frame)
    {
        Raylib.DrawRectangleRec(new(frame.X + 6, frame.Y + 8, frame.Width, frame.Height), new Color(6, 4, 2, 165));
        Raylib.DrawRectangleRec(frame, WalnutDark);
        Raylib.DrawRectangleRec(new(frame.X + 8, frame.Y + 8, frame.Width - 16, frame.Height - 16), new Color(20, 14, 11, 248));
        Raylib.DrawRectangleLinesEx(frame, 2f, WalnutEdge);
        Raylib.DrawRectangleLinesEx(new(frame.X + 5, frame.Y + 5, frame.Width - 10, frame.Height - 10), 1.2f, BrassDim);
        foreach (var (rx, ry) in new[]
        {
            (frame.X + 13, frame.Y + 13), (frame.X + frame.Width - 13, frame.Y + 13),
            (frame.X + 13, frame.Y + frame.Height - 13), (frame.X + frame.Width - 13, frame.Y + frame.Height - 13)
        })
            Raylib.DrawCircle((int)rx, (int)ry, 2.8f, BrassBright);
    }

    internal static void DrawAngledGradeStamp(string grade, string subtitle, float cx, float cy, float angleDeg, Color ink)
    {
        float w = 166, h = 104;
        Rlgl.PushMatrix();
        Rlgl.Translatef(cx, cy, 0);
        Rlgl.Rotatef(angleDeg, 0, 0, 1);
        Rectangle outer = new(-w * 0.5f, -h * 0.5f, w, h);
        Rectangle inner = new(-w * 0.5f + 5, -h * 0.5f + 5, w - 10, h - 10);
        Raylib.DrawRectangleRec(outer, new Color(ink.R, ink.G, ink.B, (byte)18));
        Raylib.DrawRectangleLinesEx(outer, 3f, ink);
        Raylib.DrawRectangleLinesEx(inner, 1.2f, new Color(ink.R, ink.G, ink.B, (byte)185));
        Font gf = GameFonts.Get(Typeface.SerifBold, 50);
        Font sf = GameFonts.Get(Typeface.InterfaceBold, 15);
        Vector2 gsz = GameFonts.Measure(gf, grade, 50);
        Vector2 ssz = GameFonts.Measure(sf, subtitle, 15);
        GameFonts.Draw(gf, grade, new(-gsz.X * 0.5f, -44), 50, ink, shadowed: false);
        GameFonts.Draw(sf, subtitle, new(-ssz.X * 0.5f, 20), 15, ink, shadowed: false);
        Rlgl.PopMatrix();
    }

    internal static readonly Color ThemeBrassBright = BrassBright;
    internal static readonly Color ThemeBrassDim    = BrassDim;
    internal static readonly Color ThemeKraftCard   = KraftCard;
    internal static readonly Color ThemeSepiaInk    = SepiaInk;
    internal static readonly Color ThemeSepiaMuted  = SepiaMuted;
    internal static readonly Color ThemeSepiaRule   = SepiaRule;
    internal static readonly Color ThemeStampRed    = StampRed;

    private static float ArchiveScale => Math.Min(Raylib.GetScreenWidth() / (float)ArchiveWidth, Raylib.GetScreenHeight() / (float)ArchiveHeight);
    private static Rectangle ArchiveScreen(Rectangle r)
    {
        float s = ArchiveScale;
        return new((Raylib.GetScreenWidth() - ArchiveWidth * s) / 2 + r.X * s,
                   (Raylib.GetScreenHeight() - ArchiveHeight * s) / 2 + r.Y * s, r.Width * s, r.Height * s);
    }

    private static Rectangle LoreTab(int tab) => new(42 + tab * 172, 68, 162, 36);
    private static readonly Rectangle LoreClose = new(1118, 15, 126, 38);
    internal static Rectangle ArchiveTabBounds(int tab) => ArchiveScreen(LoreTab(tab));
    internal static Rectangle ArchiveCloseBounds => ArchiveScreen(LoreClose);
    internal static Rectangle ArchiveRowBounds(int index) => ArchiveScreen(Row(index));
    internal static int SelectedArchiveEntry { get; private set; }
    internal static int FirstVisibleArchiveEntry => (int)MathF.Floor((LoreScrollOffset + .01f) / RowHeight);
    internal static float ArchiveScrollLimit => Math.Max(0, EntryCount - VisibleRows) * RowHeight;
    internal static int UnlockedChats => _leakedChats.Length-(Program.AgentOutcome==AgentVerdict.Released ? 0 : 1);
    private static int EntryCount => ActiveLoreTab switch
    {
        0 => UnlockedChats, 1 => TarotEntries.Length, 2 => VibeCoderCollection.All.Length, _ => DictionaryEntries.Length
    };

    private static Rectangle Row(int index) => new(Selector.X, Selector.Y + index * RowHeight - LoreScrollOffset, Selector.Width, RowHeight);
    private static void SelectArchiveEntry(int index)
    {
        SelectedArchiveEntry = Math.Clamp(index, 0, EntryCount - 1);
        float first = SelectedArchiveEntry * RowHeight;
        _scrollTarget = Math.Clamp(_scrollTarget, Math.Max(0, first - (VisibleRows - 1) * RowHeight), Math.Min(first, ArchiveScrollLimit));
    }

    private static void UpdateLoreInput(float dt)
    {
        if(SelectedArchiveEntry>=EntryCount) SelectArchiveEntry(EntryCount-1);
        if (Click(ArchiveCloseBounds)) { IsLoreOpen = false; return; }
        for (int i = 0; i < ArchiveTabs.Length; i++)
            if (Raylib.IsKeyPressed(KeyboardKey.One + i) || Raylib.IsKeyPressed(KeyboardKey.Kp1 + i) || Click(ArchiveTabBounds(i)))
                ActiveLoreTab = i;
        if (Raylib.IsKeyPressed(KeyboardKey.Tab)) ActiveLoreTab = (ActiveLoreTab + 1) % ArchiveTabs.Length;

        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0) SelectArchiveEntry(SelectedArchiveEntry - Math.Sign(wheel) * Math.Max(1, (int)Math.Abs(wheel)));
        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S)) SelectArchiveEntry(SelectedArchiveEntry + 1);
        if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W)) SelectArchiveEntry(SelectedArchiveEntry - 1);
        if (Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), ArchiveScreen(Selector)))
            for (int i = 0; i < EntryCount; i++)
                if (Click(ArchiveRowBounds(i))) SelectArchiveEntry(i);
        LoreScrollOffset += (_scrollTarget - LoreScrollOffset) * (1 - MathF.Exp(-18 * dt));
        if (Math.Abs(_scrollTarget - LoreScrollOffset) < .25f) LoreScrollOffset = _scrollTarget;
        _exhibitTime += dt;
    }

    private static string EntryName(int i) => ActiveLoreTab switch
    {
        0 => ChatNames[i], 1 => TarotNames[i], 2 => Catches(i) > 0 ? CollectionEntry(i) : "Без опознания", _ => Loc.Tr(DictionaryEntries[i].zoomer).Split(" (")[0]
    };
    private static string EntryContext(int i) => ActiveLoreTab switch
    {
        0 => ChatTitles[i], 1 => TarotCards[i], 2 => VibeCoderCollection.Tag(VibeCoderCollection.All[i].Rarity), _ => DictionaryEntries[i].nineties
    };
    private static string ExhibitModel => ActiveLoreTab switch
    {
        0 => SelectedArchiveEntry switch
        {
            1 => "prop_server_rack",
            4 => BossTwistUnlocked ? CoderModel : "prop_elevator_panel_broken",
            5 => BossTwistUnlocked ? SolderModel : "prop_elevator_panel_broken",
            6 => AgentModel,
            _ => CoderModel
        },
        1 => TarotModel,
        2 => VibeCoderCollection.All[SelectedArchiveEntry].Id=="ai_agent_v01" ? AgentModel : VibeCoderCollection.All[SelectedArchiveEntry].IsFrog ? "prop_bug_frog" : CoderModel,
        _ => SelectedArchiveEntry is 2 or 5 ? SolderModel : PartnerModel
    };

    private static VibeCoderSpecies? ChatExhibitSpecies(int entry) => entry switch
    {
        0 => VibeCoderCollection.All.FirstOrDefault(s => s.Id == "div"),
        2 => VibeCoderCollection.All.FirstOrDefault(s => s.Id == "leverage"),
        3 => VibeCoderCollection.All.FirstOrDefault(s => s.Id == "call"),
        4 => VibeCoderCollection.All.FirstOrDefault(s => s.Id == "refactor"),
        _ => null
    };

    private static void DrawSelector()
    {
        // Tactile walnut & aged-brass filing box holding physical specimen index cards!
        Rectangle tray = new(32, 138, 310, 496);
        Raylib.DrawRectangleRec(new(tray.X + 5, tray.Y + 7, tray.Width, tray.Height), new Color(6, 4, 3, 150));
        Raylib.DrawRectangleRec(tray, WalnutDark);
        Raylib.DrawRectangleRec(new(tray.X + 7, tray.Y + 7, tray.Width - 14, tray.Height - 14), new Color(17, 13, 10, 248));
        Raylib.DrawRectangleLinesEx(tray, 2, WalnutEdge);
        Raylib.DrawRectangleLinesEx(new(tray.X + 4, tray.Y + 4, tray.Width - 8, tray.Height - 8), 1, new Color(122, 95, 56, 145));

        // Riveted brass drawer nameplate at top of the filing tray
        Rectangle plate = new(46, 152, 282, 64);
        Raylib.DrawRectangleRec(plate, new Color(36, 26, 19, 255));
        Raylib.DrawRectangleLinesEx(plate, 1.5f, BrassDim);
        foreach (var (rx, ry) in new[] { (plate.X + 7, plate.Y + 7), (plate.X + plate.Width - 7, plate.Y + 7), (plate.X + 7, plate.Y + plate.Height - 7), (plate.X + plate.Width - 7, plate.Y + plate.Height - 7) })
            Raylib.DrawCircle((int)rx, (int)ry, 2.5f, BrassBright);

        Type(IndexTitles[ActiveLoreTab], 60, 159, 24, BrassBright, Typeface.SerifBold);
        string tally = ActiveLoreTab == 2
            ? $"Открыто {Program.Discoveries.Count} из {EntryCount} дел"
            : $"Записи {FirstVisibleArchiveEntry + 1:00}–{Math.Min(EntryCount, FirstVisibleArchiveEntry + VisibleRows):00} из {EntryCount:00}";
        Type(tally, 60, 190, 14, new Color(198, 178, 146, 235));

        Rectangle clip = ArchiveScreen(new(Selector.X - 4, Selector.Y - 4, Selector.Width + 16, Selector.Height + 8));
        Raylib.BeginScissorMode((int)clip.X, (int)clip.Y, (int)Math.Ceiling(clip.Width), (int)Math.Ceiling(clip.Height));
        for (int i = 0; i < EntryCount; i++)
        {
            Rectangle raw = Row(i);
            if (raw.Y + raw.Height <= Selector.Y - 8 || raw.Y >= Selector.Y + Selector.Height + 8) continue;
            bool active = i == SelectedArchiveEntry;
            bool hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), ArchiveRowBounds(i));
            bool unlocked = ActiveLoreTab != 2 || Catches(i) > 0;

            // Physical manila index card — active card slides physically right (+8px) out of the drawer!
            float pull = active ? 8f : hover ? 3f : 0f;
            Rectangle card = new(raw.X + pull, raw.Y + 4, raw.Width - 6, raw.Height - 8);
            // Small raised physical file-card tab notch on top-left
            Rectangle notch = new(card.X + 8, card.Y - 5, 58, 7);

            Color cardBg = active ? KraftCard
                : unlocked ? (hover ? new Color(76, 61, 46, 255) : new Color(58, 46, 35, 248))
                : (hover ? new Color(42, 35, 29, 255) : new Color(30, 25, 21, 248));
            Color cardEdge = active ? BrassBright : unlocked ? new Color(128, 102, 72, 220) : new Color(68, 56, 45, 200);
            Color titleCol = active ? SepiaInk : unlocked ? Ivory : new Color(184, 170, 152, 235);
            Color subCol   = active ? SepiaMuted : unlocked ? new Color(204, 184, 150, 220) : new Color(132, 118, 102, 210);

            Raylib.DrawRectangleRec(new(card.X + 3, card.Y + 4, card.Width, card.Height), new Color(5, 3, 2, active ? 165 : 105));
            Raylib.DrawRectangleRec(notch, cardBg);
            Raylib.DrawRectangleRec(card, cardBg);
            Raylib.DrawRectangleLinesEx(card, active ? 1.5f : 1f, cardEdge);

            // Subtle faint index line inside physical card
            if (active)
                Fill(card.X + 10, card.Y + 34, card.Width - 20, 1, new Color(180, 160, 128, 140));

            // Stamped dossier index number inside brass/ink tag
            Type($"№{i + 1:00}", card.X + 10, card.Y + 12, 13, active ? StampRed : BrassBright, Typeface.InterfaceBold);

            string name = EntryName(i);
            int size = 19;
            while (size > 13 && GameFonts.Measure(GameFonts.Get(Typeface.InterfaceBold, size), name, size).X > card.Width - 78) size--;
            Type(name, card.X + 48, card.Y + 8, size, titleCol, Typeface.InterfaceBold);

            string context = EntryContext(i);
            if (ActiveLoreTab == 3)
            {
                var lines = Wrap(context, (int)card.Width - 64, 13);
                if (lines.Length > 1) context = lines[0] + "…";
            }
            Type(context, card.X + 48, card.Y + 36, 13, subCol);

            // Tiny crimson wax seal dot on caught collection cards so progress pops tactilely!
            if (ActiveLoreTab == 2 && unlocked)
            {
                float sx = card.X + card.Width - 16, sy = card.Y + card.Height * 0.5f;
                Raylib.DrawCircle((int)sx, (int)sy, 6.5f, StampRed);
                Raylib.DrawCircleLines((int)sx, (int)sy, 6.5f, active ? KraftShadow : BrassDim);
            }
        }
        Raylib.EndScissorMode();

        // Compact physical scroll indicator at tray bottom when list overflows
        if (EntryCount > VisibleRows)
        {
            float frac = ArchiveScrollLimit > 0 ? Math.Clamp(LoreScrollOffset / ArchiveScrollLimit, 0, 1) : 0;
            float barX = tray.X + 22, barW = tray.Width - 44, barY = tray.Y + tray.Height - 24;
            Fill(barX, barY, barW, 4, new Color(46, 35, 26, 255));
            float thumbW = Math.Max(36, barW * (VisibleRows / (float)EntryCount));
            Fill(barX + (barW - thumbW) * frac, barY - 1, thumbW, 6, BrassDim);
        }
    }

    private const int SheetX = 776, TextWidth = 422;

    private static float SectionHeading(string title, float y)
    {
        Fill(SheetX, y, TextWidth, 1, SepiaRule);
        Text(title, SheetX, y + 8, 13, StampRed, Typeface.InterfaceBold);
        return y + 30;
    }

    private static float Speaker(string name, string words, float y, Color accent)
    {
        Text(name, SheetX, y, 17, accent, Typeface.InterfaceBold);
        return Paragraph(words, SheetX, y + 25, TextWidth, 19, 24, SepiaInk) + 18;
    }

    private static void DrawRubberStamp(string label, float centerX, float centerY, float angleDeg, Color ink)
    {
        Font f = GameFonts.Get(Typeface.InterfaceBold, 16);
        Vector2 sz = GameFonts.Measure(f, label, 16);
        float w = sz.X + 26, h = 34;
        Rlgl.PushMatrix();
        Rlgl.Translatef(centerX, centerY, 0);
        Rlgl.Rotatef(angleDeg, 0, 0, 1);
        Rectangle outer = new(-w * 0.5f, -h * 0.5f, w, h);
        Rectangle inner = new(-w * 0.5f + 3.5f, -h * 0.5f + 3.5f, w - 7, h - 7);
        Raylib.DrawRectangleRec(outer, new Color(ink.R, ink.G, ink.B, (byte)20));
        Raylib.DrawRectangleLinesEx(outer, 2.6f, ink);
        Raylib.DrawRectangleLinesEx(inner, 1.0f, new Color(ink.R, ink.G, ink.B, (byte)175));
        GameFonts.Draw(f, label, new(-sz.X * 0.5f, -8.5f), 16, ink, shadowed: false);
        Rlgl.PopMatrix();
    }

    private static void DrawCollectionSheet(int i)
    {
        var species = VibeCoderCollection.All[i];
        int caught = Catches(i);
        bool known = caught > 0;
        string clue = species.IsFrog ? "На полу у стойки остались парные царапины. Номер платы можно прочитать только в Баг-трекере."
            : species.Archetype switch
            {
                CoderArchetype.SeniorArchitect => "На пропуске выцвела фотография. В графе должности ещё читается «архитектор».",
                CoderArchetype.TeamLeadMeeter => "В журнале проходной вместо подписи оставили ссылку на созвон.",
                CoderArchetype.CryptoBro => "У турникета нашли визитку. С одной стороны кошелёк, с другой — ещё один кошелёк.",
                _ => "На записи с проходной виден открытый ноутбук. Крышка скрывает лицо."
            };
        string title = known ? species.Name : "Неизвестный экземпляр";
        string biography = known ? species.Bio : clue;
        if (known && !species.IsFrog && species.Id != "ai_agent_v01") biography += " " + FloorSystems.VibeCheck(species.Archetype);

        DrawPage(646);

        // Header line + angled wet-ink rubber stamp tucked cleanly clear of both curled corner & steel paperclip!
        Text($"ЛИЧНОЕ ДЕЛО №{i + 1:00}", SheetX, 174, 14, SepiaMuted, Typeface.InterfaceBold);
        DrawRubberStamp(known ? "ПОЙМАН" : "В РОЗЫСКЕ", SheetX + TextWidth - 134, 188, -6.5f, known ? StampRed : StampSlate);

        float titleBottom = Paragraph(title, SheetX, 206, TextWidth - 10, 32, 39, SepiaInk, Typeface.SerifBold);
        float metaY = titleBottom + 10;

        // Clean editorial HR dossier strip (NO sterile SaaS 2x2 grey box table!)
        Fill(SheetX, metaY, TextWidth, 2, SepiaInk);
        Fill(SheetX, metaY + 4, TextWidth, 1, SepiaRule);

        var (rarity, rarityInk) = species.Rarity switch
        {
            VibeCoderRarity.Uncommon  => ("Необычный", SepiaMuted),
            VibeCoderRarity.Rare      => ("Редкий", StampRed),
            VibeCoderRarity.Epic      => ("Эпический", new Color(96, 46, 92, 255)),
            VibeCoderRarity.Legendary => ("Легендарный", new Color(28, 92, 64, 255)),
            _                         => ("Обычный", SepiaMuted)
        };
        string category = species.Id == "ai_agent_v01" ? "ИИ-агент" : species.IsFrog ? "Джаба-жаба" : "Вайбкодер";
        string payout = species.IsFrog ? $"${species.SalePrice}" : $"+${species.BonusPayout}";
        string statLabel = species.Id == "ai_agent_v01" && Program.AgentOutcome != AgentVerdict.Pending ? "Решение" : "Поймано";
        string statVal = species.Id == "ai_agent_v01" && Program.AgentOutcome != AgentVerdict.Pending
            ? (Program.AgentOutcome == AgentVerdict.Contained ? "Контейнирован" : "Отпущен")
            : caught.ToString();

        void MetaCol(string lbl, string val, float x, Color valCol)
        {
            Text(lbl, x, metaY + 12, 12, SepiaMuted, Typeface.InterfaceBold);
            Text(val, x, metaY + 29, 18, valCol, Typeface.InterfaceBold);
        }
        MetaCol("Редкость", rarity, SheetX + 2, rarityInk);
        MetaCol("Категория", category, SheetX + 126, SepiaInk);
        MetaCol("Выплата", payout, SheetX + 252, StampRed);
        MetaCol(statLabel, statVal, SheetX + 336, SepiaInk);

        float bioTop = metaY + 60;
        float afterBio = Paragraph(biography, SheetX, SectionHeading("ИЗ ЛИЧНОГО ДЕЛА", bioTop), TextWidth, 19, 25, SepiaInk);
        float tacticsTop = afterBio + 16;
        float end = Paragraph(species.Hint, SheetX, SectionHeading("ТАКТИКА ОТЛОВА", tacticsTop), TextWidth, 19, 24, SepiaInk);
        if (species.IsFrog)
        {
            Fill(SheetX, end + 12, TextWidth, 1, SepiaRule);
            Paragraph("Справка цеха: материализовавшиеся баги из перегретых стоек, сбегающие через вентиляцию серверной.", SheetX, end + 20, TextWidth, 14, 18, SepiaMuted);
        }
    }

    private static void DrawPage(float bottom)
    {
        EnsureArchiveArt();
        Rectangle paperRect = new(718, 122, 536, Math.Max(528, bottom - 122));
        if (_folderSheet.Id != 0)
        {
            Raylib.DrawTexturePro(_folderSheet, new(0, 0, _folderSheet.Width, _folderSheet.Height), paperRect, Vector2.Zero, 0f, Color.White);
        }
        else
        {
            Sheet(new(726, 136, 514, bottom - 136));
        }
    }

    private static void DrawReading()
    {
        int i = SelectedArchiveEntry;
        if (ActiveLoreTab == 2) { DrawCollectionSheet(i); return; }
        DrawPage(646);
        if (ActiveLoreTab == 0)
        {
            var m = _leakedChats[i];
            Text($"ПЕРЕХВАТ №{i + 1:00}     {m.Time}", SheetX, 174, 14, SepiaMuted, Typeface.InterfaceBold);
            float y = Paragraph(ChatTitles[i], SheetX, 206, TextWidth - 10, 32, 39, SepiaInk, Typeface.SerifBold) + 6;
            y = Paragraph(m.RoleBadge, SheetX, y, TextWidth, 14, 18, SepiaMuted) + 14;
            Fill(SheetX, y, TextWidth, 1, SepiaRule);
            y = Speaker(m.Handle, m.Text, y + 14, StampRed);
            Speaker(m.ReplyAuthor, m.ReplyText, y, new Color(62, 84, 52, 255));
        }
        else if (ActiveLoreTab == 1)
        {
            var entry = TarotEntries[i];
            Text($"ОТДЕЛ КАДРОВ     Заключение №{i + 1:00}", SheetX, 174, 14, SepiaMuted, Typeface.InterfaceBold);
            float y = Paragraph(TarotCards[i], SheetX, 206, TextWidth - 10, 33, 40, SepiaInk, Typeface.SerifBold) + 6;
            y = Paragraph(entry.title, SheetX, y, TextWidth, 17, 21, SepiaMuted) + 14;
            y = SectionHeading("ЗАПИСЬ ВЕРОНИКИ", y);
            y = Paragraph(entry.body, SheetX, y, TextWidth, 19, 24, SepiaInk) + 14;
            y = SectionHeading("ЗАКЛЮЧЕНИЕ ОРАКУЛА", y);
            Paragraph(entry.verdict, SheetX, y, TextWidth, 17, 22, SepiaInk, Typeface.InterfaceBold);
        }
        else
        {
            var entry = DictionaryEntries[i];
            Text($"СЛОВАРНАЯ КАРТОЧКА №{i + 1:00}", SheetX, 174, 14, SepiaMuted, Typeface.InterfaceBold);
            float y = Paragraph(entry.zoomer, SheetX, 208, TextWidth - 10, 33, 40, SepiaInk, Typeface.SerifBold) + 14;
            y = SectionHeading("НА ЯЗЫКЕ ЦЕХА", y);
            y = Paragraph(entry.nineties, SheetX, y, TextWidth, 22, 27, SepiaInk, Typeface.InterfaceBold) + 18;
            Paragraph(entry.comment + ".", SheetX, y, TextWidth, 19, 25, SepiaInk);
        }
    }

    private static void EnsureExhibitTarget(int w,int h)
    {
        if (_exhibitTexture.Texture.Width != w || _exhibitTexture.Texture.Height != h)
        {
            if (_exhibitTexture.Id != 0) Raylib.UnloadRenderTexture(_exhibitTexture);
            _exhibitTexture = Raylib.LoadRenderTexture(w, h);
            Raylib.SetTextureFilter(_exhibitTexture.Texture, TextureFilter.Bilinear);
        }
    }

    internal static Texture2D RenderShopExhibit(string model,float yaw,float density,int skin=0)
    {
        EnsureExhibitTarget(Math.Max(1,(int)(588*density)),Math.Max(1,(int)(254*density)));
        Camera3D cam=new() { Position=new(0,.92f,3),Target=new(0,.64f,0),Up=Vector3.UnitY,
            FovY=1f,Projection=CameraProjection.Orthographic };
        Raylib.BeginTextureMode(_exhibitTexture);
        Raylib.ClearBackground(Color.Blank);
        Raylib.BeginMode3D(cam);
        VisualEngine3D.DrawArchiveModel(model,1,yaw,false,cam.Position,shop:true,skin:skin);
        Raylib.EndMode3D();
        Raylib.EndTextureMode();
        return _exhibitTexture.Texture;
    }

    private static void RenderExhibit()
    {
        Rectangle dst = ArchiveScreen(Exhibit);
        int w = Math.Max(1, (int)MathF.Ceiling(dst.Width * 2)), h = Math.Max(1, (int)MathF.Ceiling(dst.Height * 2));
        EnsureExhibitTarget(w,h);
        string model = ExhibitModel;
        bool tool = model == SolderModel;
        float scale = model=="prop_bug_frog" ? 4.6f : model == AgentModel ? .72f : model == TarotModel ? .84f : tool ? 4.3f : 1f;
        float yaw = -31 + 34 * MathF.Sin(_exhibitTime * .33f);
        Camera3D dossierCam = new() { Position = new(0, 1.42f, 3.75f), Target = new(0, .83f, 0), Up = Vector3.UnitY, FovY = 2.08f, Projection = CameraProjection.Orthographic };
        Raylib.BeginTextureMode(_exhibitTexture);
        Raylib.ClearBackground(Color.Blank);
        Raylib.BeginMode3D(dossierCam);
        VibeCoderSpecies? exhibitSpecies = ActiveLoreTab == 2
            ? VibeCoderCollection.All[SelectedArchiveEntry]
            : ActiveLoreTab == 0 ? ChatExhibitSpecies(SelectedArchiveEntry) : null;
        VisualEngine3D.DrawArchiveModel(model, scale, yaw, tool, dossierCam.Position, exhibitSpecies, ActiveLoreTab==2 && Catches(SelectedArchiveEntry)==0);
        Raylib.EndMode3D();
        Raylib.EndTextureMode();
    }

    public static void DrawLoreWindow(Font font)
    {
        EnsureArchiveArt();
        RenderExhibit();
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(18, 11, 8, 255));
        Rectangle frame = ArchiveScreen(new(0, 0, ArchiveWidth, ArchiveHeight));
        Raylib.BeginMode2D(new Camera2D { Offset = new(frame.X, frame.Y), Zoom = ArchiveScale });

        // Bespoke painted mahogany desk + stitched green leather blotter + warm brass-lamp spotlight!
        if (_deskBackdrop.Id != 0)
            Raylib.DrawTexturePro(_deskBackdrop, new(0, 0, _deskBackdrop.Width, _deskBackdrop.Height), new(0, 0, ArchiveWidth, ArchiveHeight), Vector2.Zero, 0f, Color.White);
        else
            Raylib.DrawRectangle(0, 0, ArchiveWidth, ArchiveHeight, WalnutDark);

        // Engraved mahogany rail header + brass nameplate close plaque
        Type("АРХИВ «ЭНТРОПИК»", 42, 12, 38, new Color(242, 224, 186, 255), Typeface.SerifBold);
        bool closeHover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), ArchiveCloseBounds);
        Raylib.DrawRectangleRec(new(LoreClose.X + 2, LoreClose.Y + 3, LoreClose.Width, LoreClose.Height), new Color(6, 4, 2, 150));
        Raylib.DrawRectangleRec(LoreClose, closeHover ? new Color(66, 46, 30, 255) : new Color(36, 24, 17, 245));
        Raylib.DrawRectangleLinesEx(LoreClose, 1.5f, closeHover ? BrassBright : BrassDim);
        MainMenuAndPauseUI.Center(GameFonts.Get(Typeface.InterfaceBold, 15), "Закрыть [N]", LoreClose, 15, closeHover ? Color.White : BrassBright);

        // Physical raised manila/brass filing divider tabs along the top brass desk rail!
        for (int i = 0; i < ArchiveTabs.Length; i++)
        {
            Rectangle r = LoreTab(i);
            bool active = i == ActiveLoreTab;
            bool hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), ArchiveTabBounds(i));
            Rectangle tabRect = active ? new(r.X, r.Y - 3, r.Width, r.Height + 5) : new(r.X, r.Y + 3, r.Width, r.Height - 2);
            Color tabBg = active ? KraftCard : hover ? new Color(56, 42, 30, 245) : new Color(34, 25, 19, 235);
            Color tabBorder = active ? BrassBright : hover ? BrassDim : new Color(92, 70, 46, 210);
            Raylib.DrawRectangleRec(new(tabRect.X + 2, tabRect.Y + 3, tabRect.Width, tabRect.Height), new Color(6, 4, 2, 135));
            Raylib.DrawRectangleRec(tabRect, tabBg);
            Raylib.DrawRectangleLinesEx(tabRect, active ? 1.8f : 1f, tabBorder);
            Type($"[{i + 1}]", tabRect.X + 11, tabRect.Y + 10, 13, active ? StampRed : BrassDim, Typeface.InterfaceBold);
            Type(ArchiveTabs[i], tabRect.X + 36, tabRect.Y + 8, 17, active ? SepiaInk : hover ? Ivory : new Color(214, 198, 172, 235), Typeface.InterfaceBold);
        }

        DrawSelector();
        Raylib.DrawTexturePro(_exhibitTexture.Texture, new(0, 0, _exhibitTexture.Texture.Width, -_exhibitTexture.Texture.Height),
                             Exhibit, Vector2.Zero, 0, Color.White);
        DrawReading();

        // Quiet engraved brass key hints along the lower mahogany bevel
        Type("1–4 / Tab   Раздел", 44, 682, 14, new Color(196, 174, 138, 220));
        Type("Колесо / стрелки   Запись", 242, 682, 14, new Color(196, 174, 138, 220));
        Type("Esc   В игру", 1140, 682, 14, new Color(196, 174, 138, 220));
        Raylib.EndMode2D();
    }

    internal static void UnloadArchive()
    {
        if (_exhibitTexture.Id != 0) Raylib.UnloadRenderTexture(_exhibitTexture);
        if (_deskBackdrop.Id != 0) Raylib.UnloadTexture(_deskBackdrop);
        if (_folderSheet.Id != 0) Raylib.UnloadTexture(_folderSheet);
        if (_corkboard.Id != 0) Raylib.UnloadTexture(_corkboard);
        if (_contractNote.Id != 0) Raylib.UnloadTexture(_contractNote);
        if (_pauseClipboard.Id != 0) Raylib.UnloadTexture(_pauseClipboard);
        if (_mixerConsole.Id != 0) Raylib.UnloadTexture(_mixerConsole);
        _exhibitTexture = default;
        _deskBackdrop = _folderSheet = _corkboard = _contractNote = _pauseClipboard = _mixerConsole = default;
        _archiveArtTried = false;
        VisualEngine3D.UnloadArchiveShader();
    }
}

internal static unsafe partial class VisualEngine3D
{
    private static Shader dossierShader;
    private static int dossierEye, dossierSurface, dossierSkin, dossierClock;
    private static readonly Dictionary<string,BoundingBox> shopBounds=new();
    internal static BoundingBox ModelBounds(string asset)
    {
        if(!shopBounds.TryGetValue(asset,out var bounds))
            shopBounds[asset]=bounds=Raylib.GetModelBoundingBox(imports[asset]);
        return bounds;
    }
    private static Model stageCylinder;

    // The exhibit borrows each loaded mesh and its material maps. The stage shader
    // is applied to a value copy so the world keeps its current lighting pass.
    internal static void DrawArchiveModel(string asset, float scale, float yaw, bool tool, Vector3 eye,VibeCoderSpecies? species=null,bool hidden=false,bool shop=false,int skin=0)
    {
        if (dossierShader.Id == 0)
        {
            dossierShader = Raylib.LoadShaderFromMemory("""
                #version 330
                in vec3 vertexPosition, vertexNormal;
                in vec2 vertexTexCoord;
                uniform mat4 mvp, matModel;
                out vec3 normalWS, posWS,skinPos;
                out vec2 uv;
                void main() {
                    normalWS = mat3(matModel) * vertexNormal;
                    posWS = (matModel * vec4(vertexPosition,1.)).xyz;
                    skinPos=vertexPosition; uv = vertexTexCoord;
                    gl_Position = mvp * vec4(vertexPosition,1.);
                }
                """, """
                #version 330
                in vec3 normalWS, posWS,skinPos;
                in vec2 uv;
                uniform sampler2D texture0;
                uniform vec4 colDiffuse;
                uniform vec3 eye;
                uniform int surface;
                uniform float skinClock;
                /*SKIN*/
                out vec4 color;
                void main() {
                    vec4 swatch = texture(texture0,uv) * colDiffuse;
                    vec3 n = normalize(normalWS), v = normalize(eye-posWS);
                    if(surface==3) { color=vec4(vec3(.029,.050,.063)+vec3(.07,.13,.14)*pow(1.-abs(dot(n,v)),3.),1.); return; }
                    if (surface == 1 || surface == 2) {
                        vec3 key = normalize(vec3(-.65,.80,.65));
                        float edge = max(0.,dot(n,normalize(vec3(.32,.65,-.70))));
                        float radial = length(posWS.xz);
                        float brushing = .5+.5*cos(radial*380.);
                        float highlight = pow(max(dot(n,normalize(v+key)),0.),34.);
                        float contact = max(exp(-19.*length((posWS.xz-vec2(-.14,.015))*vec2(1.4,.70))),
                                            exp(-19.*length((posWS.xz-vec2(.14,.015))*vec2(1.4,.70))));
                        vec3 metal = surface==2 ? vec3(.72,.52,.22) : vec3(.145,.098,.066);
                        float pool = max(0.,1.-length(posWS.xz-vec2(-.48,-.52))*.82);
                        vec3 lit = metal*(.42+.48*max(dot(n,key),0.)+.68*pool+.14*brushing)
                                  + vec3(.24,.18,.10)*edge + highlight*.46;
                        color = vec4(lit*(1.-.88*contact),1.); return;
                    }
                    vec3 pigment = pow(swatch.rgb,vec3(.455));
                    if(weaponSkin>0) pigment=finishSkin(skinPos,n,v,skinClock,pigment);
                    vec3 key = normalize(vec3(-.60,.76,.92));
                    vec3 back = normalize(vec3(.66,.48,-.72));
                    float rim = pow(1.-max(dot(n,v),0.),1.65) * max(dot(n,back),0.);
                    float bounce = max(dot(n,normalize(vec3(.5,.05,.82))),0.);
                    vec3 light = vec3(.38,.36,.34) + vec3(.92,.76,.54)*max(dot(n,key),0.)
                                 + vec3(.14,.16,.13)*bounce;
                    vec3 result=pigment*light+rim*vec3(.68,.54,.32);
                    if(surface==4) {
                        float gleam=pow(max(0.,dot(n,normalize(key+v))),16.);
                        result+=pigment*vec3(.28,.21,.15)+vec3(.29,.22,.15)*gleam;
                    }
                    color=vec4(result,swatch.a);
                }
                """.Replace("/*SKIN*/",WeaponSkins.ShaderCode));
            if (dossierShader.Id == 0 || dossierShader.Id == Rlgl.GetShaderIdDefault())
                throw new InvalidOperationException("Failed to compile archive lighting");
            dossierSkin=Raylib.GetShaderLocation(dossierShader,"weaponSkin");
            dossierClock=Raylib.GetShaderLocation(dossierShader,"skinClock");
            dossierEye = Raylib.GetShaderLocation(dossierShader, "eye");
            dossierSurface = Raylib.GetShaderLocation(dossierShader, "surface");
            stageCylinder = Raylib.LoadModelFromMesh(Raylib.GenMeshCylinder(1, 1, 96));
        }
        Raylib.SetShaderValue(dossierShader,dossierSkin,skin,ShaderUniformDataType.Int);
        Raylib.SetShaderValue(dossierShader,dossierClock,(float)Raylib.GetTime(),ShaderUniformDataType.Float);
        Raylib.SetShaderValue(dossierShader, dossierEye, eye, ShaderUniformDataType.Vec3);
        void StageBand(float radius, float from, float height, int surface)
        {
            Material metal = stageCylinder.Materials[0];
            metal.Shader = dossierShader;
            Raylib.SetShaderValue(dossierShader, dossierSurface, surface, ShaderUniformDataType.Int);
            var pose = Matrix4x4.CreateScale(radius, height, radius) * Matrix4x4.CreateTranslation(0, from, 0);
            Raylib.DrawMesh(stageCylinder.Meshes[0], metal, Matrix4x4.Transpose(pose));
        }
        if(!shop)
        {
            StageBand(.67f, -.102f, .052f, 1);
            StageBand(.704f, -.050f, .009f, 2);
            StageBand(.70f, -.041f, .041f, 1);
        }
        Raylib.SetShaderValue(dossierShader, dossierSurface, hidden ? 3 : shop ? 4 : 0, ShaderUniformDataType.Int);
        Matrix4x4 transform = tool ?
            Matrix4x4.CreateRotationX(-1.22f) * Matrix4x4.CreateRotationZ(-.48f) : Matrix4x4.Identity;
        transform *= Matrix4x4.CreateScale(scale) * Matrix4x4.CreateRotationY(yaw * MathF.PI / 180)
                   * Matrix4x4.CreateTranslation(0, tool ? .55f : 0, 0);
        if (!shop && asset is "prop_server_rack" or "prop_elevator_panel_broken")
        {
            var b = ModelBounds(asset);
            Vector3 ext = b.Max - b.Min;
            float maxH = asset == "prop_server_rack" ? 1.56f : 1.05f;
            float fit = MathF.Min(1.22f / MathF.Max(0.05f, MathF.Max(ext.X, ext.Z)), maxH / MathF.Max(0.05f, ext.Y));
            float yLift = asset == "prop_server_rack" ? 0.02f : 0.34f;
            transform = Matrix4x4.CreateTranslation(-(b.Min.X + b.Max.X) * 0.5f, -b.Min.Y, -(b.Min.Z + b.Max.Z) * 0.5f)
                * Matrix4x4.CreateScale(fit)
                * Matrix4x4.CreateRotationY(yaw * MathF.PI / 180f)
                * Matrix4x4.CreateTranslation(0f, yLift, 0f);
        }
        if(shop)
        {
            if(asset is "pager" or "signet")
            {
                Rlgl.PushMatrix();
                Rlgl.Translatef(0,.64f,0);
                Rlgl.Rotatef(yaw+12,0,1,0);
                Rlgl.Rotatef(-7,0,0,1);
                Color brass=new(186,142,76,255);
                if(asset=="pager")
                {
                    Raylib.DrawCube(Vector3.Zero,1.32f,.64f,.25f,new(44,37,39,255));
                    Raylib.DrawCube(new(-.12f,.05f,.14f),.89f,.33f,.04f,brass);
                    Raylib.DrawCube(new(-.12f,.05f,.17f),.82f,.26f,.025f,new(149,158,117,255));
                    for(int i=0;i<5;i++) Raylib.DrawCube(new(.39f,.16f-i*.064f,.14f),.24f,.023f,.02f,new(16,15,16,255));
                    Raylib.DrawCube(new(.40f,-.19f,.15f),.15f,.095f,.05f,new(152,50,48,255));
                    Raylib.DrawCube(new(-.36f,.33f,0),.16f,.06f,.11f,brass);
                }
                else
                {
                    const int segments=40;
                    for(int i=0;i<segments;i++)
                    {
                        Vector3 Ring(int j) => new(.32f*MathF.Cos(j*2*MathF.PI/segments),.32f*MathF.Sin(j*2*MathF.PI/segments)-.04f,0);
                        Raylib.DrawCylinderEx(Ring(i),Ring(i+1),.066f,.066f,10,brass);
                    }
                    Raylib.DrawCube(new(0,.23f,0),.45f,.26f,.20f,brass);
                    Raylib.DrawCube(new(0,.23f,.11f),.36f,.18f,.035f,new(80,20,30,255));
                    Raylib.DrawCube(new(0,.23f,.14f),.17f,.035f,.02f,brass);
                }
                Rlgl.PopMatrix();
                return;
            }
            if(asset=="keycard")
            {
                Rlgl.PushMatrix();
                Rlgl.Translatef(0,.64f,0);
                Rlgl.Rotatef(yaw,0,1,0);
                Rlgl.Rotatef(-6,0,0,1);
                Rlgl.Scalef(1.25f,1.25f,1.25f);
                Raylib.DrawCube(Vector3.Zero,.99f,.64f,.052f,new(146,44,54,255));
                foreach(int side in new[]{-1,1})
                {
                    float z=side*.029f;
                    Raylib.DrawCube(new(-.27f,-.17f,z),.16f,.09f,.012f,new(228,184,104,255));
                    Raylib.DrawCube(new(-.27f,.06f,z),.20f,.22f,.012f,new(44,33,40,255));
                    Raylib.DrawSphere(new(-.27f,.085f,z+side*.009f),.047f,new(210,189,160,255));
                    Raylib.DrawCube(new(-.27f,-.012f,z+side*.012f),.13f,.046f,.012f,new(210,189,160,255));
                    // Raised Cyrillic СБ on both faces of the badge.
                    void LetterStroke(float x,float y,float w,float h) =>
                        Raylib.DrawCube(new(side*x,y,z),w,h,.014f,new(236,213,173,255));
                    foreach(float x in new[]{.04f,.23f})
                    {
                        LetterStroke(x-.06f,0,.024f,.20f);
                        LetterStroke(x,.09f,.14f,.023f);
                        LetterStroke(x,-.09f,.14f,.023f);
                    }
                    LetterStroke(.23f,0,.14f,.023f);
                    LetterStroke(.29f,-.045f,.024f,.10f);
                    Raylib.DrawCube(new(0,.24f,z),.15f,.033f,.014f,new(39,35,35,255));
                }
                Rlgl.PopMatrix();
                return;
            }
            var bounds=ModelBounds(asset);
            Vector3 extent=bounds.Max-bounds.Min;
            bool lengthwise=extent.Z>extent.X && asset.StartsWith("weapon_");
            float angle=(yaw+(lengthwise ? 90 : 0))*MathF.PI/180;
            float width=Math.Abs(MathF.Cos(angle))*extent.X+Math.Abs(MathF.Sin(angle))*extent.Z;
            float depth=Math.Abs(MathF.Sin(angle))*extent.X+Math.Abs(MathF.Cos(angle))*extent.Z;
            float fit=Math.Min(2.02f/width,.88f/(extent.Y+.095f*depth));
            transform=Matrix4x4.CreateTranslation(-(bounds.Max+bounds.Min)/2)*Matrix4x4.CreateScale(fit)
                *Matrix4x4.CreateRotationY(angle)
                *Matrix4x4.CreateTranslation(0,.64f,0);
        }
        bool partner = asset == "char_partner_90s";
        void DrawParts(string name, Matrix4x4 pose, bool hideHolsteredProp = false)
        {
            Model model = imports[name];
            pose = Matrix4x4.Transpose(pose);
            for (int i = 0; i < model.MeshCount; i++)
            {
                if (hideHolsteredProp && partnerPropMeshes.Contains(i)) continue;
                Material material = model.Materials[model.MeshMaterial[i]];
                material.Shader = dossierShader;
                Color original=material.Maps[0].Color;
                if(asset!="escaped_ai_boss" && species!=null && !hidden) material.Maps[0].Color=species.IsFrog ? VisualEngine3D.FrogPigment(model.MeshMaterial[i],original,species.HoodieColor) : VisualEngine3D.CoderPigment(model.MeshMaterial[i],species.HoodieColor,species.GlassesColor,original);
                Raylib.DrawMesh(model.Meshes[i], material, pose);
                material.Maps[0].Color=original;
            }
        }
        if(species is {IsFrog:true} && !HasAsset(asset))
        {
            SkyscraperFloor1World.DrawFrog(Vector3.Zero,yaw*MathF.PI/180,hidden ? new(18,30,34,255) : species.HoodieColor,scale:scale);
            return;
        }
        if(asset=="char_vibe_coder")
        {
            DrawArchiveVibeCoder(species?.Id ?? "", transform, dossierShader);
            return;
        }
        DrawParts(asset, transform, partner);
        if (partner)
        {
            // Same held mesh and character-local pose as the gameplay avatar.
            var grip = Matrix4x4.CreateScale(.66f) * Matrix4x4.CreateRotationY(194 * MathF.PI / 180)
                     * Matrix4x4.CreateTranslation(-.094f, 1.324f, .258f);
            DrawParts("weapon_uzi_90s", grip * transform);
        }
    }

    internal static void UnloadArchiveShader()
    {
        shopBounds.Clear();
        if (stageCylinder.MeshCount > 0) Raylib.UnloadModel(stageCylinder);
        stageCylinder = default;
        if (dossierShader.Id != 0) Raylib.UnloadShader(dossierShader);
        dossierShader = default;
    }
}
