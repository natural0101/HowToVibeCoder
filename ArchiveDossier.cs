#nullable enable
using System.Numerics;
using Raylib_cs;
using static HowToVibeCoder.DossierPrint;

namespace HowToVibeCoder;

public static partial class LoreAndMenusUI
{
    private const int ArchiveWidth = 1280, ArchiveHeight = 720, RowHeight = 62, VisibleRows = 5;
    private static readonly string[] ArchiveTabs = ["Переписка", "Досье HR", "Коллекция", "Словарь"];
    private static readonly string[] IndexTitles = ["Переписка цеха", "Таро-досье HR", "Картотека", "Словарь 90-х"];
    private static readonly string[] ChatNames = ["Данила", "Петрович", "Крипто-Бро", "Тимлид-Митингер", "Сеньор Помидор", "Кассета 1998","Агент v0.1"];
    private static readonly string[] ChatTitles = ["Пятничный деплой", "Стойка B12 и кулер", "Охота на Жадность", "Дейлик и Эспрессо", "Ядро ZERO и Техдолг", "Дело АО Энтропия","Пятидесятый этаж"];
    private static readonly string[] TarotNames = ["Сеньор Алексей", "Джуниор Данила", "Крипто и Митингер"];
    private static readonly string[] TarotCards = ["Аркан XVI: Башня", "Аркан 0: Шут", "Аркан XV: Дьявол"];
    private static int Catches(int i) => Program.Discoveries.GetValueOrDefault(VibeCoderCollection.All[i].Id);
    internal static string CollectionEntry(int i) => Catches(i)>0 ? VibeCoderCollection.All[i].Name : "??? — НЕ ПОЙМАН";
    private const string CoderModel = "char_vibe_coder", TarotModel = "char_hr_veronika",
                         AgentModel = "escaped_ai_boss", PartnerModel = "char_partner_90s", SolderModel = "weapon_solder_90s";
    private static readonly Rectangle Selector = new(38, 237, 282, VisibleRows * RowHeight);
    private static readonly Rectangle Exhibit = new(319, 151, 401, 483);
    private static RenderTexture2D _exhibitTexture;
    private static Shader _stageAtmosphere;
    private static int _atmosphereSize;
    private static float _exhibitTime;

    private static float ArchiveScale => Math.Min(Raylib.GetScreenWidth() / (float)ArchiveWidth, Raylib.GetScreenHeight() / (float)ArchiveHeight);
    private static Rectangle ArchiveScreen(Rectangle r)
    {
        float s = ArchiveScale;
        return new((Raylib.GetScreenWidth() - ArchiveWidth * s) / 2 + r.X * s,
                   (Raylib.GetScreenHeight() - ArchiveHeight * s) / 2 + r.Y * s, r.Width * s, r.Height * s);
    }

    private static Rectangle LoreTab(int tab) => new(38 + tab * 184, 77, 160, 43);
    private static readonly Rectangle LoreClose = new(1130, 17, 112, 40);
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
        0 => SelectedArchiveEntry==6 ? AgentModel : SelectedArchiveEntry == 5 ? SolderModel : CoderModel,
        1 => TarotModel,
        2 => VibeCoderCollection.All[SelectedArchiveEntry].Id=="ai_agent_v01" ? AgentModel : VibeCoderCollection.All[SelectedArchiveEntry].IsFrog ? "prop_bug_frog" : CoderModel,
        _ => SelectedArchiveEntry is 2 or 5 ? SolderModel : PartnerModel
    };

    private static void DrawSelector()
    {
        Type(IndexTitles[ActiveLoreTab], 38, 155, 27, Ivory, Typeface.SerifBold);
        string range = $"Записи {FirstVisibleArchiveEntry + 1:00}–{Math.Min(EntryCount, FirstVisibleArchiveEntry + VisibleRows):00} из {EntryCount:00}";
        Type(ActiveLoreTab == 2 ? $"Открыто {Program.Discoveries.Count} из {EntryCount} дел" : range, 39, 207, 16, Quiet);
        Rectangle clip = ArchiveScreen(Selector);
        Raylib.BeginScissorMode((int)clip.X, (int)clip.Y, (int)Math.Ceiling(clip.Width), (int)Math.Ceiling(clip.Height));
        for (int i = 0; i < EntryCount; i++)
        {
            Rectangle row = Row(i);
            if (row.Y + row.Height <= Selector.Y || row.Y >= Selector.Y + Selector.Height) continue;
            bool active = i == SelectedArchiveEntry;
            bool hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), ArchiveRowBounds(i));
            Raylib.DrawRectangleRec(row, active ? Selected : hover ? new Color(36, 31, 28, 255) : new Color(26, 23, 21, 240));
            Fill(row.X, row.Y + row.Height - 1, row.Width, 1, new Color(58, 52, 46, 200));
            Type($"{i + 1:00}", row.X + 13, row.Y + 13, 14, active ? Ivory : Quiet);
            string name = EntryName(i);
            int size = 20;
            while (size > 13 && GameFonts.Measure(GameFonts.Get(Typeface.InterfaceBold, size), name, size).X > row.Width - 53) size--;
            Type(name, row.X + 43, row.Y + 7, size, Ivory, Typeface.InterfaceBold);
            string context = EntryContext(i);
            if (ActiveLoreTab == 3)
            {
                var lines = Wrap(context, (int)row.Width - 60, 14);
                if (lines.Length > 1) context = lines[0] + "…";
            }
            Type(context, row.X + 43, row.Y + 34, 14, Quiet);
        }
        Raylib.EndScissorMode();
    }

    private const int SheetX = 746, TextWidth = 466;

    private static float SectionHeading(string title, float y)
    {
        Fill(SheetX, y, TextWidth, 1, Rule);
        Text(title, SheetX, y + 9, 14, RedInk, Typeface.InterfaceBold);
        return y + 32;
    }

    private static float Speaker(string name, string words, float y, Color accent)
    {
        Text(name, SheetX, y, 17, accent, Typeface.InterfaceBold);
        return Paragraph(words, SheetX, y + 26, TextWidth, 20, 25, Ink) + 19;
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
        float ledgerTop = 212 + 43 * Wrap(title, TextWidth, 35, Typeface.SerifBold).Length;
        float bioTop = ledgerTop + 116;
        float tacticsTop = bioTop + 32 + 27 * Wrap(biography, TextWidth, 22).Length + 12;
        float end = tacticsTop + 32 + 25 * Wrap(species.Hint, TextWidth, 21).Length;
        DrawPage(end + (species.IsFrog ? 72 : 20));
        Color seal = known ? new(166, 28, 46, 255) : new(108, 102, 94, 255);
        string stamp = known ? "ПОЙМАН" : "В РОЗЫСКЕ";
        int stampWidth = known ? 86 : 116;
        float stampX = SheetX + TextWidth - stampWidth;
        Text($"ЛИЧНОЕ ДЕЛО №{i + 1:00}", SheetX, 165, 15, Muted, Typeface.InterfaceBold);
        Raylib.DrawRectangleLinesEx(new(stampX, 159, stampWidth, 31), 2, seal);
        Text(stamp, stampX + 11, 165, 16, seal, Typeface.InterfaceBold);
        Paragraph(title, SheetX, 200, TextWidth, 35, 43, Ink, Typeface.SerifBold);

        void Field(string label, string value, int column, int row, Color color)
        {
            float x = SheetX + column * (TextWidth / 2) + 9, top = ledgerTop + row * 58;
            Text(label, x, top + 5, 16, Muted);
            Text(value, x, top + 25, 23, color, Typeface.InterfaceBold);
        }
        Fill(SheetX, ledgerTop, TextWidth, 1, Rule);
        Fill(SheetX, ledgerTop + 58, TextWidth, 1, Rule);
        Fill(SheetX + TextWidth / 2, ledgerTop, 1, 116, Rule);
        var (rarity, ink) = species.Rarity switch
        {
            VibeCoderRarity.Uncommon => ("Необычный", Muted),
            VibeCoderRarity.Rare => ("Редкий", RedInk),
            VibeCoderRarity.Epic => ("Эпический", new Color(99, 54, 94, 255)),
            VibeCoderRarity.Legendary => ("Легендарный", new Color(34, 99, 73, 255)),
            _ => ("Обычный", Muted)
        };
        Field("Редкость", rarity, 0, 0, ink);
        Field("Категория", species.Id=="ai_agent_v01" ? "ИИ-агент" : species.IsFrog ? "Джаба-жаба" : "Вайбкодер", 1, 0, Ink);
        Field("Выплата", species.IsFrog ? $"${species.SalePrice}" : $"+${species.BonusPayout}", 0, 1, Ink);
        if(species.Id=="ai_agent_v01" && Program.AgentOutcome!=AgentVerdict.Pending)
            Field("Решение",Program.AgentOutcome==AgentVerdict.Contained ? "Контейнирован" : "Отпущен",1,1,Ink);
        else Field("Поймано", caught.ToString(), 1, 1, Ink);

        Paragraph(biography, SheetX, SectionHeading("ИЗ ЛИЧНОГО ДЕЛА", bioTop), TextWidth, 22, 27, Ink);
        Paragraph(species.Hint, SheetX, SectionHeading("ТАКТИКА ОТЛОВА", tacticsTop), TextWidth, 21, 25, Ink);
        if (species.IsFrog)
        {
            Fill(SheetX, end + 10, TextWidth, 1, Rule);
            Rlgl.PushMatrix();
            Rlgl.Translatef(SheetX + 5, end + 20, 0);
            var oblique = Matrix4x4.Identity;
            oblique.M12 = -.16f;
            Rlgl.MultMatrixf(oblique);
            Paragraph("Справка цеха: серверные микроботы-отладчики 1998 г. (Jabba-98), сбегающие из перегретых стоек.", 0, 0, TextWidth - 8, 15, 18, Muted);
            Rlgl.PopMatrix();
        }
    }

    private static void DrawPage(float bottom)
    {
        Sheet(new(720, 136, 520, bottom - 136));
    }

    private static void DrawReading()
    {
        int i = SelectedArchiveEntry;
        if (ActiveLoreTab == 2) { DrawCollectionSheet(i); return; }
        if (ActiveLoreTab != 3) DrawPage(606);
        if (ActiveLoreTab == 0)
        {
            var m = _leakedChats[i];
            Text($"ПЕРЕХВАТ №{i + 1:00}     {m.Time}", SheetX, 170, 15, Muted, Typeface.InterfaceBold);
            float y = Paragraph(ChatTitles[i], SheetX, 202, TextWidth, 36, 43, Ink, Typeface.SerifBold) + 6;
            y = Paragraph(m.RoleBadge, SheetX, y, TextWidth, 15, 19, Muted) + 16;
            Fill(SheetX, y, TextWidth, 1, Rule);
            y = Speaker(m.Handle, m.Text, y + 15, RedInk);
            Speaker(m.ReplyAuthor, m.ReplyText, y, new Color(99, 103, 69, 255));
        }
        else if (ActiveLoreTab == 1)
        {
            var entry = TarotEntries[i];
            Text($"ОТДЕЛ КАДРОВ     Заключение №{i + 1:00}", SheetX, 170, 15, Muted, Typeface.InterfaceBold);
            float y = Paragraph(TarotCards[i], SheetX, 202, TextWidth, 38, 45, Ink, Typeface.SerifBold) + 6;
            y = Paragraph(entry.title, SheetX, y, TextWidth, 18, 22, Muted) + 14;
            y = SectionHeading("ЗАПИСЬ ВЕРОНИКИ", y);
            y = Paragraph(entry.body, SheetX, y, TextWidth, 20, 25, Ink) + 15;
            y = SectionHeading("ЗАКЛЮЧЕНИЕ ОРАКУЛА", y);
            Paragraph(entry.verdict, SheetX, y, TextWidth, 17, 22, Ink, Typeface.InterfaceBold);
        }
        else
        {
            var entry = DictionaryEntries[i];
            float bottom = 208 + 44 * Wrap(entry.zoomer, TextWidth, 37, Typeface.SerifBold).Length + 14 + 32
                + 28 * Wrap(entry.nineties, TextWidth, 23, Typeface.InterfaceBold).Length + 19
                + 27 * Wrap(entry.comment + ".", TextWidth, 21).Length + 20;
            DrawPage(bottom);
            Text($"СЛОВАРНАЯ КАРТОЧКА №{i + 1:00}", SheetX, 170, 15, Muted, Typeface.InterfaceBold);
            float y = Paragraph(entry.zoomer, SheetX, 208, TextWidth, 37, 44, Ink, Typeface.SerifBold) + 14;
            y = SectionHeading("НА ЯЗЫКЕ ЦЕХА", y);
            y = Paragraph(entry.nineties, SheetX, y, TextWidth, 23, 28, Ink, Typeface.InterfaceBold) + 19;
            Paragraph(entry.comment + ".", SheetX, y, TextWidth, 21, 27, Ink);
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
        float scale = model=="prop_bug_frog" ? 3.1f : model == AgentModel ? .72f : model == TarotModel ? .84f : tool ? 4.3f : 1f;
        float yaw = -31 + 34 * MathF.Sin(_exhibitTime * .33f);
        Camera3D dossierCam = new() { Position = new(0, 1.42f, 3.75f), Target = new(0, .83f, 0), Up = Vector3.UnitY, FovY = 2.08f, Projection = CameraProjection.Orthographic };
        Raylib.BeginTextureMode(_exhibitTexture);
        Raylib.ClearBackground(Color.Blank);
        Raylib.BeginMode3D(dossierCam);
        VisualEngine3D.DrawArchiveModel(model, scale, yaw, tool, dossierCam.Position,ActiveLoreTab==2 ? VibeCoderCollection.All[SelectedArchiveEntry] : null, ActiveLoreTab==2 && Catches(SelectedArchiveEntry)==0);
        Raylib.EndMode3D();
        Raylib.EndTextureMode();
    }

    private static void DrawStageAtmosphere()
    {
        if (_stageAtmosphere.Id == 0)
        {
            _stageAtmosphere = Raylib.LoadShaderFromMemory(null, """
                #version 330
                uniform vec4 stage;
                out vec4 color;
                void main() {
                    vec2 uv = (gl_FragCoord.xy - stage.xy) / stage.zw;
                    vec2 edge = min(uv,1.-uv);
                    float fade = smoothstep(0.,.21,edge.x) * smoothstep(0.,.14,edge.y);
                    vec2 lamp = (uv - vec2(.35,.78)) * vec2(1.3,.84);
                    float glow = exp(-7.*dot(lamp,lamp));
                    float across = uv.x-.49-(uv.y-.5)*.31;
                    float beam = exp(-24.*across*across)*uv.y;
                    vec3 ink = vec3(.079,.071,.063) + glow*vec3(.17,.14,.112) + beam*vec3(.055,.045,.032);
                    color = vec4(ink,.985*fade);
                }
                """);
            if (_stageAtmosphere.Id == 0 || _stageAtmosphere.Id == Rlgl.GetShaderIdDefault())
                throw new InvalidOperationException("Failed to compile archive backdrop");
            _atmosphereSize = Raylib.GetShaderLocation(_stageAtmosphere, "stage");
        }
        Rectangle local = new(210, 86, 730, 590);
        Rectangle dst = ArchiveScreen(local);
        float px = Raylib.GetRenderWidth() / (float)Raylib.GetScreenWidth();
        float py = Raylib.GetRenderHeight() / (float)Raylib.GetScreenHeight();
        Vector4 stage = new(dst.X * px, (Raylib.GetScreenHeight() - dst.Y - dst.Height) * py, dst.Width * px, dst.Height * py);
        Raylib.SetShaderValue(_stageAtmosphere, _atmosphereSize, stage, ShaderUniformDataType.Vec4);
        Raylib.BeginShaderMode(_stageAtmosphere);
        Raylib.DrawRectangleRec(local, Color.White);
        Raylib.EndShaderMode();
    }

    public static void DrawLoreWindow(Font font)
    {
        RenderExhibit();
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Studio);
        Rectangle frame = ArchiveScreen(new(0, 0, ArchiveWidth, ArchiveHeight));
        Raylib.BeginMode2D(new Camera2D { Offset = new(frame.X, frame.Y), Zoom = ArchiveScale });
        DrawStageAtmosphere();
        Type("АРХИВ «БЕЛОЙ АКУЛЫ»", 38, 15, 42, Ivory, Typeface.SerifBold);
        bool closeHover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), ArchiveCloseBounds);
        if (closeHover) Raylib.DrawRectangleRounded(LoreClose, .55f, 8, Selected);
        Raylib.DrawRectangleRoundedLinesEx(LoreClose, .55f, 8, 1, new Color(113, 99, 86, 255));
        MainMenuAndPauseUI.Center(GameFonts.Get(Typeface.Interface, 16), "Закрыть [N]", LoreClose, 16, Ivory);
        for (int i = 0; i < ArchiveTabs.Length; i++)
        {
            Rectangle r = LoreTab(i);
            bool active = i == ActiveLoreTab;
            bool hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), ArchiveTabBounds(i));
            Type($"[{i + 1}]", r.X, r.Y + 10, 15, active ? Ivory : Quiet);
            Type(ArchiveTabs[i], r.X + 31, r.Y + 7, 20, active ? Color.White : hover ? Ivory : Quiet, Typeface.InterfaceBold);
        }
        DrawSelector();
        Raylib.DrawTexturePro(_exhibitTexture.Texture, new(0, 0, _exhibitTexture.Texture.Width, -_exhibitTexture.Texture.Height),
                             Exhibit, Vector2.Zero, 0, Color.White);
        DrawReading();
        Fill(38, 668, 1204, 1, Stone);
        Type("1–4 / Tab   Раздел", 38, 682, 15, Quiet);
        Type("Колесо / стрелки   Запись", 235, 682, 15, Quiet);
        Type("Esc   В игру", 1151, 682, 15, Quiet);
        Raylib.EndMode2D();
    }

    internal static void UnloadArchive()
    {
        if (_exhibitTexture.Id != 0) Raylib.UnloadRenderTexture(_exhibitTexture);
        if (_stageAtmosphere.Id != 0) Raylib.UnloadShader(_stageAtmosphere);
        _exhibitTexture = default;
        _stageAtmosphere = default;
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
                        vec3 metal = surface==2 ? vec3(.53,.35,.15) : vec3(.105,.135,.146);
                        float pool = max(0.,1.-length(posWS.xz-vec2(-.52,-.55))*.86);
                        vec3 lit = metal*(.42+.47*max(dot(n,key),0.)+.65*pool+.13*brushing)
                                  + vec3(.12,.18,.19)*edge + highlight*.43;
                        color = vec4(lit*(1.-.90*contact),1.); return;
                    }
                    vec3 pigment = pow(swatch.rgb,vec3(.455));
                    if(weaponSkin>0) pigment=finishSkin(skinPos,n,v,skinClock,pigment);
                    vec3 key = normalize(vec3(-.60,.76,.92));
                    vec3 back = normalize(vec3(.66,.48,-.72));
                    float rim = pow(1.-max(dot(n,v),0.),1.65) * max(dot(n,back),0.);
                    float bounce = max(dot(n,normalize(vec3(.5,.05,.82))),0.);
                    vec3 light = vec3(.34,.36,.39) + vec3(.87,.72,.55)*max(dot(n,key),0.)
                                 + vec3(.09,.18,.21)*bounce;
                    vec3 result=pigment*light+rim*vec3(.31,.63,.68);
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
                if(asset!="escaped_ai_boss" && species!=null && !hidden) material.Maps[0].Color=species.IsFrog ? Raylib.ColorTint(original,species.HoodieColor) : VisualEngine3D.CoderPigment(model.MeshMaterial[i],species.HoodieColor,species.GlassesColor,original);
                Raylib.DrawMesh(model.Meshes[i], material, pose);
                material.Maps[0].Color=original;
            }
        }
        if(species is {IsFrog:true} && !HasAsset(asset))
        {
            SkyscraperFloor1World.DrawFrog(Vector3.Zero,yaw*MathF.PI/180,hidden ? new(18,30,34,255) : species.HoodieColor,scale:scale);
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
