#nullable enable
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public static partial class LoreAndMenusUI
{
    private const int ArchiveWidth = 1280, ArchiveHeight = 720, RowHeight = 86, VisibleRows = 3;
    private static readonly string[] ArchiveTabs = ["ПЕРЕПИСКА ЦЕХА", "ТАРО-ДОСЬЕ HR", "БЕСТИАРИЙ ЭТАЖА", "СЛОВАРЬ 90-Х"];
    private static readonly string[] IndexTitles = ["Перехваты", "Расклады", "Фигуранты", "Понятия"];
    private static readonly string[] ChatNames = ["Данила", "Седой", "Гуру SaaS", "Артемий", "Сеньор Помидор", "Крипто-бро"];
    private static readonly string[] ChatTitles = ["Пятничный деплой", "Счёт от AWS", "Второй процесс", "Человек с паяльником", "Пропавшая таблица", "Замкнутый вызов"];
    private static readonly string[] TarotNames = ["Алексей", "Данила", "Инженерный отдел"];
    private static readonly string[] TarotCards = ["Башня", "Тройка кубков", "Меркурий в Деве"];
    private static readonly string[] BestiaryNames = ["Сбежавший ИИ-агент", "Вайб-кодер", "Вероника", "Братан", "Инцидент у лифта"];
    private static readonly string[] BestiaryPosts = ["Северный лифт", "Опенспейс", "Приёмная HR", "Напарник", "Дополнение к досье агента"];
    private const string CoderModel = "char_vibe_coder", TarotModel = "char_hr_veronika",
                         AgentModel = "escaped_ai_boss", PartnerModel = "char_partner_90s", SolderModel = "weapon_solder_90s";
    private static readonly Color Phosphor = new(239, 198, 110, 255), Ivory = new(241, 239, 224, 255),
        Silver = new(184, 197, 192, 255), Quiet = new(144, 161, 160, 255), Patina = new(127, 213, 195, 255);
    private static readonly Rectangle Selector = new(38, 254, 306, VisibleRows * RowHeight);
    private static readonly Rectangle Exhibit = new(350, 135, 420, 495);
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

    private static Rectangle LoreTab(int tab) => new(38 + tab * 275, 73, 264, 46);
    private static readonly Rectangle LoreClose = new(1130, 17, 112, 40);
    internal static Rectangle ArchiveTabBounds(int tab) => ArchiveScreen(LoreTab(tab));
    internal static Rectangle ArchiveCloseBounds => ArchiveScreen(LoreClose);
    internal static Rectangle ArchiveRowBounds(int index) => ArchiveScreen(Row(index));
    internal static int SelectedArchiveEntry { get; private set; }
    internal static int FirstVisibleArchiveEntry => (int)MathF.Floor((LoreScrollOffset + .01f) / RowHeight);
    internal static float ArchiveScrollLimit => Math.Max(0, EntryCount - VisibleRows) * RowHeight;
    private static int EntryCount => ActiveLoreTab switch
    {
        0 => _leakedChats.Length, 1 => TarotEntries.Length, 2 => BestiaryNames.Length, _ => DictionaryEntries.Length
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
        0 => ChatNames[i], 1 => TarotNames[i], 2 => BestiaryNames[i], _ => DictionaryEntries[i].zoomer.Split(" (")[0]
    };
    private static string EntryContext(int i) => ActiveLoreTab switch
    {
        0 => ChatTitles[i], 1 => TarotCards[i], 2 => BestiaryPosts[i], _ => "Из словаря цеха"
    };
    private static string ExhibitModel => ActiveLoreTab switch
    {
        0 => CoderModel, 1 => TarotModel,
        2 => SelectedArchiveEntry switch { 1 => CoderModel, 2 => TarotModel, 3 => PartnerModel, _ => AgentModel },
        _ => SelectedArchiveEntry == 2 ? SolderModel : PartnerModel
    };

    private static void DrawSelector()
    {
        Type(IndexTitles[ActiveLoreTab], 38, 173, 31, Ivory, Typeface.SerifBold);
        Type($"Записи   {FirstVisibleArchiveEntry + 1}–{Math.Min(EntryCount, FirstVisibleArchiveEntry + VisibleRows)} из {EntryCount}",
             39, 218, 14, Quiet);
        Rectangle clip = ArchiveScreen(Selector);
        Raylib.BeginScissorMode((int)clip.X, (int)clip.Y, (int)Math.Ceiling(clip.Width), (int)Math.Ceiling(clip.Height));
        for (int i = 0; i < EntryCount; i++)
        {
            Rectangle r = Row(i);
            if (r.Y + r.Height <= Selector.Y || r.Y >= Selector.Y + Selector.Height) continue;
            bool selected = i == SelectedArchiveEntry;
            bool hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), ArchiveRowBounds(i));
            if (selected || hover)
                Raylib.DrawRectangleGradientH((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height - 1,
                    selected ? new Color(101, 85, 50, 196) : new Color(69, 87, 86, 130), new Color(26, 39, 38, 0));
            Fill(r.X, r.Y + r.Height - 1, r.Width, 1, new Color(141, 159, 148, 59));
            if (selected) Fill(r.X, r.Y + 11, 3, r.Height - 22, Phosphor);
            Type(EntryName(i), r.X + 17, r.Y + 14, 21, selected ? Phosphor : Ivory, Typeface.InterfaceBold);
            Type(EntryContext(i), r.X + 18, r.Y + 46, 15, selected ? Ivory : Quiet);
        }
        Raylib.EndScissorMode();
    }

    private static float RoleLine(string speaker, string words, float y, Color accent, bool compact = false)
    {
        const int x = 822, width = 378;
        y = Paragraph(speaker, x, y, width, 14, 18, accent, Typeface.InterfaceBold) + 6;
        return Paragraph(words, x, y, width, compact ? 17 : 18, compact ? 21 : 24, Ivory) + 15;
    }

    private static void DrawReading()
    {
        int i = SelectedArchiveEntry;
        const int x = 822, width = 378;
        Fill(790, 147, 452, 502, new Color(10, 18, 23, 218));
        Fill(790, 165, 3, 63, Phosphor);
        string section, title, status;
        switch (ActiveLoreTab)
        {
            case 0: section = _leakedChats[i].Time; title = ChatTitles[i]; status = _leakedChats[i].RoleBadge; break;
            case 1: section = "Картотека Вероники"; title = TarotCards[i]; status = TarotEntries[i].title; break;
            case 2: section = "Бестиарий этажа"; title = i == 4 ? "Захват лифта" : BestiaryNames[i]; status = BestiaryPosts[i]; break;
            default: section = "Словарь цеха"; title = EntryName(i); status = "Записано со слов напарника"; break;
        }
        Type(section.ToUpperInvariant(), x, 176, 13, Phosphor, Typeface.InterfaceBold);
        float y = Paragraph(title, x - 1, 202, width, 34, 40, Ivory, Typeface.SerifBold) + 12;
        y = Paragraph(status, x, y, width, 14, 19, Silver) + 19;
        Fill(x, y, width, 1, new Color(118, 148, 148, 103));
        y += 21;
        if (ActiveLoreTab == 0)
        {
            var m = _leakedChats[i];
            y = RoleLine(m.Handle, m.Text, y, m.HandleColor);
            RoleLine(m.ReplyAuthor, m.ReplyText, y, Patina);
        }
        else if (ActiveLoreTab == 1)
        {
            y = RoleLine("Вероника, HR", TarotEntries[i].body, y, Patina);
            RoleLine("Решение по раскладу", TarotEntries[i].verdict, y, TarotEntries[i].accent);
        }
        else if (ActiveLoreTab == 2)
        {
            if (i == 0 || i == 4)
            {
                int part = i == 0 ? 0 : 2;
                y = RoleLine("Пресс-служба", PressRelease[part] + " " + PressRelease[part + 1], y, Phosphor, compact: i == 4);
                RoleLine("Петрович, запись с камер", CameraLog[part] + " " + CameraLog[part + 1], y, Patina, compact: i == 4);
            }
            else if (i == 1)
            {
                var m = _leakedChats[0];
                y = RoleLine(m.Handle, m.Text, y, m.HandleColor);
                RoleLine(m.ReplyAuthor, m.ReplyText, y, Patina);
            }
            else if (i == 2)
            {
                y = RoleLine("Вероника, приёмная HR", AudioMusicSystem.VeronikaTarotLines[1], y, Patina);
                RoleLine("Из личного дела Данилы", TarotEntries[1].verdict, y, Phosphor);
            }
            else
            {
                y = RoleLine("Братан, по рации", "«Я у тебя слева. Как двери лифта откроются, держись рядом.»", y, Phosphor);
                RoleLine("Коммерс", "«Вижу тебя, малиновый. Тогда лифт твой, а я заберу макбук.»", y, Patina);
            }
        }
        else
        {
            var e = DictionaryEntries[i];
            y = RoleLine("В опенспейсе", e.zoomer, y, Patina);
            RoleLine("Братан", e.nineties + ". " + e.comment + ".", y, Phosphor);
        }
    }

    private static void RenderExhibit()
    {
        Rectangle dst = ArchiveScreen(Exhibit);
        int w = Math.Max(1, (int)MathF.Ceiling(dst.Width * 2)), h = Math.Max(1, (int)MathF.Ceiling(dst.Height * 2));
        if (_exhibitTexture.Texture.Width != w || _exhibitTexture.Texture.Height != h)
        {
            if (_exhibitTexture.Id != 0) Raylib.UnloadRenderTexture(_exhibitTexture);
            _exhibitTexture = Raylib.LoadRenderTexture(w, h);
            Raylib.SetTextureFilter(_exhibitTexture.Texture, TextureFilter.Bilinear);
        }
        string model = ExhibitModel;
        bool tool = model == SolderModel;
        float scale = model == AgentModel ? .72f : model == TarotModel ? .84f : tool ? 4.3f : 1f;
        float yaw = -31 + 34 * MathF.Sin(_exhibitTime * .33f);
        Camera3D dossierCam = new() { Position = new(0, 1.42f, 3.75f), Target = new(0, .83f, 0), Up = Vector3.UnitY, FovY = 2.08f, Projection = CameraProjection.Orthographic };
        Raylib.BeginTextureMode(_exhibitTexture);
        Raylib.ClearBackground(Color.Blank);
        Raylib.BeginMode3D(dossierCam);
        VisualEngine3D.DrawArchiveModel(model, scale, yaw, tool, dossierCam.Position);
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
                    vec3 ink = vec3(.022,.048,.061) + glow*vec3(.080,.117,.128) + beam*vec3(.035,.042,.030);
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
        Dim(.84f);
        Rectangle frame = ArchiveScreen(new(0, 0, ArchiveWidth, ArchiveHeight));
        Raylib.BeginMode2D(new Camera2D { Offset = new(frame.X, frame.Y), Zoom = ArchiveScale });
        Raylib.DrawRectangleGradientH(0, 118, 450, 545, new Color(5, 15, 19, 232), new Color(5, 15, 19, 0));
        DrawStageAtmosphere();
        Raylib.DrawRectangleGradientV(0, 0, ArchiveWidth, 148, new Color(6, 16, 22, 247), new Color(6, 16, 22, 110));
        Type("Архив башни", 38, 15, 35, Ivory, Typeface.SerifBold);
        Type("Закрыть [N]", 1139, 29, 15, Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), ArchiveCloseBounds) ? Phosphor : Silver);
        Fill(38, 118, 1204, 1, new Color(159, 182, 178, 72));
        for (int i = 0; i < ArchiveTabs.Length; i++)
        {
            Rectangle r = LoreTab(i);
            bool active = i == ActiveLoreTab;
            bool hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), ArchiveTabBounds(i));
            Type($"[{i + 1}]", r.X, r.Y + 13, 15, active ? Phosphor : Quiet);
            Type(ArchiveTabs[i], r.X + 34, r.Y + 12, 16, active || hover ? Ivory : Silver, Typeface.InterfaceBold);
            if (active) Fill(r.X, 116, r.Width - 14, 3, Phosphor);
        }
        DrawSelector();
        Raylib.DrawTexturePro(_exhibitTexture.Texture, new(0, 0, _exhibitTexture.Texture.Width, -_exhibitTexture.Texture.Height),
                             Exhibit, Vector2.Zero, 0, Color.White);
        DrawReading();
        Raylib.DrawRectangleGradientV(0, 655, ArchiveWidth, 65, new Color(7, 16, 21, 70), new Color(7, 16, 21, 235));
        Fill(38, 667, 1204, 1, new Color(132, 157, 157, 80));
        Type("1–4 / Tab   Раздел", 38, 684, 14, Silver);
        Type("Колесо / стрелки   Запись", 234, 684, 14, Silver);
        Type("Esc   В игру", 1151, 684, 14, Silver);
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
    private static int dossierEye, dossierSurface;
    private static Model stageCylinder;

    // The exhibit borrows each loaded mesh and its material maps. The stage shader
    // is applied to a value copy so the world keeps its current lighting pass.
    internal static void DrawArchiveModel(string asset, float scale, float yaw, bool tool, Vector3 eye)
    {
        if (dossierShader.Id == 0)
        {
            dossierShader = Raylib.LoadShaderFromMemory("""
                #version 330
                in vec3 vertexPosition, vertexNormal;
                in vec2 vertexTexCoord;
                uniform mat4 mvp, matModel;
                out vec3 normalWS, posWS;
                out vec2 uv;
                void main() {
                    normalWS = mat3(matModel) * vertexNormal;
                    posWS = (matModel * vec4(vertexPosition,1.)).xyz;
                    uv = vertexTexCoord;
                    gl_Position = mvp * vec4(vertexPosition,1.);
                }
                """, """
                #version 330
                in vec3 normalWS, posWS;
                in vec2 uv;
                uniform sampler2D texture0;
                uniform vec4 colDiffuse;
                uniform vec3 eye;
                uniform int surface;
                out vec4 color;
                void main() {
                    vec4 swatch = texture(texture0,uv) * colDiffuse;
                    vec3 n = normalize(normalWS), v = normalize(eye-posWS);
                    if (surface != 0) {
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
                    vec3 key = normalize(vec3(-.60,.76,.92));
                    vec3 back = normalize(vec3(.66,.48,-.72));
                    float rim = pow(1.-max(dot(n,v),0.),1.65) * max(dot(n,back),0.);
                    float bounce = max(dot(n,normalize(vec3(.5,.05,.82))),0.);
                    vec3 light = vec3(.34,.36,.39) + vec3(.87,.72,.55)*max(dot(n,key),0.)
                                 + vec3(.09,.18,.21)*bounce;
                    color = vec4(pigment * light + rim * vec3(.31,.63,.68), swatch.a);
                }
                """);
            if (dossierShader.Id == 0 || dossierShader.Id == Rlgl.GetShaderIdDefault())
                throw new InvalidOperationException("Failed to compile archive lighting");
            dossierEye = Raylib.GetShaderLocation(dossierShader, "eye");
            dossierSurface = Raylib.GetShaderLocation(dossierShader, "surface");
            stageCylinder = Raylib.LoadModelFromMesh(Raylib.GenMeshCylinder(1, 1, 96));
        }
        Raylib.SetShaderValue(dossierShader, dossierEye, eye, ShaderUniformDataType.Vec3);
        void StageBand(float radius, float from, float height, int surface)
        {
            Material metal = stageCylinder.Materials[0];
            metal.Shader = dossierShader;
            Raylib.SetShaderValue(dossierShader, dossierSurface, surface, ShaderUniformDataType.Int);
            var pose = Matrix4x4.CreateScale(radius, height, radius) * Matrix4x4.CreateTranslation(0, from, 0);
            Raylib.DrawMesh(stageCylinder.Meshes[0], metal, Matrix4x4.Transpose(pose));
        }
        StageBand(.67f, -.102f, .052f, 1);
        StageBand(.704f, -.050f, .009f, 2);
        StageBand(.70f, -.041f, .041f, 1);
        Raylib.SetShaderValue(dossierShader, dossierSurface, 0, ShaderUniformDataType.Int);
        Matrix4x4 transform = tool ?
            Matrix4x4.CreateRotationX(-1.22f) * Matrix4x4.CreateRotationZ(-.48f) : Matrix4x4.Identity;
        transform *= Matrix4x4.CreateScale(scale) * Matrix4x4.CreateRotationY(yaw * MathF.PI / 180)
                   * Matrix4x4.CreateTranslation(0, tool ? .55f : 0, 0);
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
                Raylib.DrawMesh(model.Meshes[i], material, pose);
            }
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
        if (stageCylinder.MeshCount > 0) Raylib.UnloadModel(stageCylinder);
        stageCylinder = default;
        if (dossierShader.Id != 0) Raylib.UnloadShader(dossierShader);
        dossierShader = default;
    }
}
