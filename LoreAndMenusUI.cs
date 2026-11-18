#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace HowToVibeCoder;

public enum WeaponSkinSlot
{
    Belt = 0,
    SolderingIron = 1,
    MicroUzi = 2,
    SawedOffOrChatGun = 3
}

public sealed class WeaponSkinDefinition
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string WeaponLabel { get; init; } = "";
    public string RarityLabel { get; init; } = "";
    public WeaponSkinSlot Slot { get; init; }
    public Color CardColor { get; init; }
    public Color Primary3D { get; init; }
    public Color Secondary3D { get; init; }
    public Color Glow3D { get; init; }
    public string LoreTag { get; init; } = "";
}

public sealed class LoreChatMessage
{
    public string Monogram { get; init; } = "";
    public string Handle { get; init; } = "";
    public string RoleBadge { get; init; } = "";
    public string Time { get; init; } = "";
    public string Text { get; init; } = "";
    public string ReplyAuthor { get; init; } = "";
    public string ReplyText { get; init; } = "";
    public required (string Label, string Count, Color Accent)[] Metrics { get; init; }
    public Color HandleColor { get; init; }
}

public static class LoreAndMenusUI
{
    // Состояния окон
    public static bool IsLoreOpen { get; set; }
    public static bool IsSettingsOpen { get; set; }
    public static bool IsCaseOpen { get; set; }

    public static bool IsAnyModalOpen => IsLoreOpen || IsSettingsOpen || IsCaseOpen;

    // Текущая вкладка досье [N] (0..3)
    private static int _activeLoreTab;
    private static float _scrollTarget, _scrollLimit;
    public static int ActiveLoreTab
    {
        get => _activeLoreTab;
        set
        {
            _activeLoreTab = Math.Clamp(value, 0, 3);
            LoreScrollOffset = _scrollTarget = _scrollLimit = 0;
        }
    }
    public static float LoreScrollOffset { get; private set; }

    // Настройки и ползунки [F8]
    public static float MasterVolume { get; set; } = 0.85f;
    public static float MusicVolume { get => AudioMusicSystem.MusicVolume; set => AudioMusicSystem.MusicVolume = value; }
    public static float WeaponSfxVolume { get; set; } = 0.90f;
    public static float VoiceVolume { get; set; } = 0.95f;
    public static float MouseSensitivity { get; set; } = 0.18f; // 0.05 .. 0.50
    public static float CameraFov { get; set; } = 75f;          // 60 .. 100

    private static int _draggingSliderIndex = -1;
    public static int SelectedSliderIndex { get; set; } = 0;
    private static string _settingsStatusBanner = "Кликни мышкой по ползунку или жми стрелки ВВЕРХ/ВНИЗ и ВЛЕВО/ВПРАВО";
    private static float _settingsStatusTimer = 0f;

    // Прямая ссылка на 3D ViewModel оружия в руках (для мгновенной перекраски и открытия всего арсенала)
    public static WeaponAndCombatView? ActiveWeaponView { get; set; }

    // Колбэки для мгновенной интеграции с основным циклом игры
    public static Action<int>? OnGrantMoneyRequested { get; set; }
    public static Action? OnUnlockAllWeaponsRequested { get; set; }
    public static Action? OnResetFloorRequested { get; set; }
    public static Func<int>? GetPlayerMoney { get; set; }
    public static Func<int, bool>? TrySpendPlayerMoney { get; set; }
    public static Action<WeaponSkinDefinition>? OnSkinEquipped { get; set; }

    // Палитры 3D ViewModel оружия (применяются сразу после выпадения скина в рулетке [K])
    public static Color BeltPrimaryColor { get; private set; } = new(92, 52, 28, 255);
    public static Color BeltBuckleColor { get; private set; } = new(245, 198, 48, 255);

    public static Color SolderingBodyColor { get; private set; } = new(38, 108, 235, 255);
    public static Color SolderingTipColor { get; private set; } = new(255, 140, 42, 255);

    public static Color UziBodyColor { get; private set; } = new(44, 48, 56, 255);
    public static Color UziAccentColor { get; private set; } = new(198, 28, 72, 255);

    public static Color ShotgunBodyColor { get; private set; } = new(78, 54, 38, 255);
    public static Color ShotgunAccentColor { get; private set; } = new(185, 195, 210, 255);

    public static string EquippedBeltSkinName { get; private set; } = "Базовый Отцовский Ремень";
    public static string EquippedSolderingSkinName { get; private set; } = "Паяльник ЭПСН-40 (Синяя Изолента)";
    public static string EquippedUziSkinName { get; private set; } = "Micro-Uzi «Сток из 93-го»";
    public static string EquippedShotgunSkinName { get; private set; } = "Обрез «Классика Решалы»";

    // Состояние рулетки кейсов [K]
    public const int CaseSpinCost = 100;
    public static bool IsSpinningCase { get; private set; }
    private static float _caseScrollPx = 0f;
    private static float _caseScrollSpeed = 0f;
    private static int _lastTickItemIndex = -1;
    public static WeaponSkinDefinition? LastWonSkin { get; private set; }
    private static float _caseWinFlashTimer = 0f;
    private static string _caseBannerMessage = "";
    private static readonly Random _rng = new(1994);
    private static readonly List<WeaponSkinDefinition> _rouletteTape = new(96);
    public static HashSet<int> UnlockedSkinIds { get; } = new();

    // 12 культовых скинов 90-х по всем 4 видам оружия
    public static readonly WeaponSkinDefinition[] AllSkins =
    {
        new()
        {
            Id = 0,
            Name = "Ремень «Золотая Пряжка Авторитета»",
            WeaponLabel = "СЛОТ 1: РЕМЕНЬ",
            RarityLabel = "ЗОЛОТОЙ ЛЮКС",
            Slot = WeaponSkinSlot.Belt,
            CardColor = new Color(245, 192, 42, 255),
            Primary3D = new Color(148, 98, 24, 255),
            Secondary3D = new Color(255, 215, 55, 255),
            Glow3D = new Color(255, 232, 120, 255),
            LoreTag = "+100% к уважению на стрелке и скорости воспитания джунов"
        },
        new()
        {
            Id = 1,
            Name = "Ремень «Крокодил из 94-го»",
            WeaponLabel = "СЛОТ 1: РЕМЕНЬ",
            RarityLabel = "ИЗУМРУДНЫЙ",
            Slot = WeaponSkinSlot.Belt,
            CardColor = new Color(34, 205, 118, 255),
            Primary3D = new Color(24, 138, 76, 255),
            Secondary3D = new Color(185, 245, 140, 255),
            Glow3D = new Color(64, 255, 158, 255),
            LoreTag = "Трофейная кожа с таможни Шереметьево-2"
        },
        new()
        {
            Id = 2,
            Name = "Ремень «Батин Градиент Воспитания»",
            WeaponLabel = "СЛОТ 1: РЕМЕНЬ",
            RarityLabel = "МИФИЧЕСКИЙ FADE",
            Slot = WeaponSkinSlot.Belt,
            CardColor = new Color(235, 75, 175, 255),
            Primary3D = new Color(195, 42, 125, 255),
            Secondary3D = new Color(255, 180, 48, 255),
            Glow3D = new Color(255, 105, 210, 255),
            LoreTag = "Лечит выгорание и закрывает Твиттер с одного взмаха"
        },
        new()
        {
            Id = 3,
            Name = "Паяльник «Синяя Изолента Элит»",
            WeaponLabel = "СЛОТ 2: ПАЯЛЬНИК",
            RarityLabel = "КОБАЛЬТ СССР",
            Slot = WeaponSkinSlot.SolderingIron,
            CardColor = new Color(48, 132, 255, 255),
            Primary3D = new Color(28, 96, 235, 255),
            Secondary3D = new Color(135, 210, 255, 255),
            Glow3D = new Color(80, 175, 255, 255),
            LoreTag = "Намотка в 7 слоёв: не пробивает даже на 380 Вольт"
        },
        new()
        {
            Id = 4,
            Name = "Паяльник «Канифольный Градиент»",
            WeaponLabel = "СЛОТ 2: ПАЯЛЬНИК",
            RarityLabel = "ЯНТАРНЫЙ FADE",
            Slot = WeaponSkinSlot.SolderingIron,
            CardColor = new Color(255, 138, 36, 255),
            Primary3D = new Color(230, 88, 24, 255),
            Secondary3D = new Color(255, 215, 75, 255),
            Glow3D = new Color(255, 165, 58, 255),
            LoreTag = "Аромат сосновой канифоли моментально вспоминает пароли от сидов"
        },
        new()
        {
            Id = 5,
            Name = "Паяльник «Кровавая Паутина 220В»",
            WeaponLabel = "СЛОТ 2: ПАЯЛЬНИК",
            RarityLabel = "КРИМЗОН ХАРДКОР",
            Slot = WeaponSkinSlot.SolderingIron,
            CardColor = new Color(220, 38, 62, 255),
            Primary3D = new Color(168, 18, 36, 255),
            Secondary3D = new Color(32, 32, 38, 255),
            Glow3D = new Color(255, 58, 82, 255),
            LoreTag = "Спецверсия для код-ревью без автокомплита"
        },
        new()
        {
            Id = 6,
            Name = "Micro-Uzi «Малиновый Пиджак»",
            WeaponLabel = "СЛОТ 3: MICRO-UZI",
            RarityLabel = "ЛЕГЕНДА 1993",
            Slot = WeaponSkinSlot.MicroUzi,
            CardColor = new Color(228, 32, 96, 255),
            Primary3D = new Color(195, 22, 78, 255),
            Secondary3D = new Color(248, 202, 58, 255),
            Glow3D = new Color(255, 72, 138, 255),
            LoreTag = "В тон хозяйскому пиджаку от Versace с Черкизона"
        },
        new()
        {
            Id = 7,
            Name = "Micro-Uzi «Золотой Картель»",
            WeaponLabel = "СЛОТ 3: MICRO-UZI",
            RarityLabel = "ЧИСТОЕ ЗОЛОТО 999",
            Slot = WeaponSkinSlot.MicroUzi,
            CardColor = new Color(255, 208, 46, 255),
            Primary3D = new Color(242, 188, 34, 255),
            Secondary3D = new Color(255, 242, 150, 255),
            Glow3D = new Color(255, 224, 92, 255),
            LoreTag = "Каждый патрон стоит как месячная подписка на Cursor Pro"
        },
        new()
        {
            Id = 8,
            Name = "Micro-Uzi «Неоновый Опенспейс»",
            WeaponLabel = "СЛОТ 3: MICRO-UZI",
            RarityLabel = "КИБЕР-ПУРПУР",
            Slot = WeaponSkinSlot.MicroUzi,
            CardColor = new Color(168, 64, 255, 255),
            Primary3D = new Color(128, 38, 225, 255),
            Secondary3D = new Color(56, 242, 255, 255),
            Glow3D = new Color(195, 110, 255, 255),
            LoreTag = "Светится в темноте ярче, чем Макбук с халявным API"
        },
        new()
        {
            Id = 9,
            Name = "Обрез «Братва 1995»",
            WeaponLabel = "СЛОТ 4: ОБРЕЗ / ЧАТ-ГАН",
            RarityLabel = "СТАЛЬ И РЖАВЧИНА",
            Slot = WeaponSkinSlot.SawedOffOrChatGun,
            CardColor = new Color(194, 112, 58, 255),
            Primary3D = new Color(138, 74, 38, 255),
            Secondary3D = new Color(192, 202, 214, 255),
            Glow3D = new Color(235, 148, 86, 255),
            LoreTag = "Две трубы аргументов против любого падения продакшена"
        },
        new()
        {
            Id = 10,
            Name = "Обрез «Малиновый Восход 1994»",
            WeaponLabel = "СЛОТ 4: ОБРЕЗ / ЧАТ-ГАН",
            RarityLabel = "РУБИНОВЫЙ ЛЮКС",
            Slot = WeaponSkinSlot.SawedOffOrChatGun,
            CardColor = new Color(242, 56, 92, 255),
            Primary3D = new Color(186, 26, 58, 255),
            Secondary3D = new Color(252, 210, 92, 255),
            Glow3D = new Color(255, 92, 128, 255),
            LoreTag = "Инкрустирован крышками от пейджеров Motorola"
        },
        new()
        {
            Id = 11,
            Name = "Чат-Ган «Лимитка Сэма»",
            WeaponLabel = "СЛОТ 4: ОБРЕЗ / ЧАТ-ГАН",
            RarityLabel = "ПРИЗМАТИК AGI",
            Slot = WeaponSkinSlot.SawedOffOrChatGun,
            CardColor = new Color(56, 245, 215, 255),
            Primary3D = new Color(38, 224, 198, 255),
            Secondary3D = new Color(255, 215, 64, 255),
            Glow3D = new Color(120, 255, 238, 255),
            LoreTag = "Стреляет чистыми промптами по $200 за выстрел"
        }
    };

    private static readonly Color Ink = new(24, 27, 26, 255), Paper = new(224, 221, 206, 255),
        Ash = new(175, 184, 172, 255), Mint = new(115, 211, 181, 255), Amber = new(255, 189, 72, 255),
        Rule = new(64, 71, 66, 255), SidebarRule = new(163, 163, 151, 255);

    // Вкладка 1: 6 слитых тредов чата опенспейса
    private static readonly LoreChatMessage[] _leakedChats =
    {
        new()
        {
            Monogram = "DV",
            Handle = "@danila_vibe",
            RoleBadge = "JUNIOR PROMPT BRO / 19 ЛЕТ",
            Time = "Пятница, 19:54",
            Metrics = [("ХИТ", "14", Mint), ("RIP", "28", Amber), ("КЛОУН", "9", Ash)],
            Text = "«Пацаны, я не знаю что такое индекс в БД, но я нажал Accept All на 14 000 строк в пятницу вечером. Прод лёг, зато код сгенерировался за 4 секунды!»",
            ReplyAuthor = "@senior_pomidor (DBA / DevOps)",
            ReplyText = "«Ты дропнул схему billing_prod вместе с бэкапами. Откатывай миграцию руками, пока коммерс не пришёл с ремнём.»",
            HandleColor = new Color(76, 235, 165, 255)
        },
        new()
        {
            Monogram = "TS",
            Handle = "@timlead_sedoy",
            RoleBadge = "TECH LEAD / СЕДОЙ В 27",
            Time = "Суббота, 03:12",
            Metrics = [("ШОК", "42", Ash), ("$", "19", Amber)],
            Text = "«КТО ЗАКОММИТИЛ КЛЮЧИ ОТ AWS ПРЯМО В ПУБЛИЧНЫЙ index.html?! Нам пришёл счёт на $54,000 за одну ночь!»",
            ReplyAuthor = "@danila_vibe",
            ReplyText = "«ИИ-ассистент написал, что так быстрее для фронтенда без прокси-бэкенда, я просто смержил ветку в main.»",
            HandleColor = new Color(255, 98, 98, 255)
        },
        new()
        {
            Monogram = "SG",
            Handle = "@saas_guru_2h",
            RoleBadge = "FOUNDER / $60K MRR В ПРОФИЛЕ",
            Time = "Суббота, 11:08",
            Metrics = [("ХИТ", "31", Mint), ("MONERO", "24", Amber)],
            Text = "«Запустил свой AI-SaaS за 2 часа без единой строчки кода руками! Подскажите, как остановить чужой процесс kdevtmpfsi в контейнере, Курсор пишет что всё ок?»",
            ReplyAuthor = "@sec_ops (ИБ-инженер)",
            ReplyText = "«Твой Docker-образ скачан с левого форка и уже третьи сутки майнит Monero на весь наш серверный шкаф.»",
            HandleColor = new Color(255, 206, 68, 255)
        },
        new()
        {
            Monogram = "AA",
            Handle = "@artem_antigravity",
            RoleBadge = "PROMPT ARCHITECT",
            Time = "Понедельник, 10:17",
            Metrics = [("ХИТ", "55", Mint), ("RIP", "18", Amber)],
            Text = "«По этажу ходит коммерс в малиновом пиджаке с советским паяльником и заставляет писать код руками без автокомплита! Куда жаловаться?!»",
            ReplyAuthor = "@kommers_90x (Решала этажа)",
            ReplyText = "«Щас ещё отцовским ремнём по синтаксису пройдёмся. Кабанчиком за стол на Галеру и давай $20 в секунду!»",
            HandleColor = new Color(185, 122, 255, 255)
        },
        new()
        {
            Monogram = "SP",
            Handle = "@senior_pomidor",
            RoleBadge = "DBA / DEVOPS",
            Time = "Понедельник, 12:40",
            Metrics = [("RIP", "64", Amber), ("ШОК", "12", Ash)],
            Text = "«КТО УДАЛИЛ ТАБЛИЦУ USERS И ЗАМЕНИЛ ЕЁ НА КОММЕНТАРИЙ // rest of database here?! У нас 400 тысяч клиентов не могут войти в личный кабинет!»",
            ReplyAuthor = "@danila_vibe (Junior Prompt Bro)",
            ReplyText = "«Там контекстное окно модели кончилось на середине файла, я не заметил и запушил с флагом --force.»",
            HandleColor = new Color(255, 144, 68, 255)
        },
        new()
        {
            Monogram = "CB",
            Handle = "@crypto_saas_bro",
            RoleBadge = "AI HUSTLER",
            Time = "Понедельник, 16:05",
            Metrics = [("ШОК", "39", Ash), ("$", "21", Amber)],
            Text = "«Парни, почему мне пришёл счёт от OpenAI API на $48,200 за бесконечный рекурсивный вызов агента внутри useEffect?!»",
            ReplyAuthor = "@veronika_tarot (HR-Таролог)",
            ReplyText = "«Это ретроградный Меркурий в доме кэшфлоу. Сдавай макбук коммерсу и иди на расклад Башни.»",
            HandleColor = new Color(72, 212, 255, 255)
        }
    };

    static LoreAndMenusUI()
    {
        RebuildCaseTape(null);
    }

    private static void RebuildCaseTape(WeaponSkinDefinition? guaranteedWinner)
    {
        _rouletteTape.Clear();
        for (int i = 0; i < 80; i++)
        {
            var skin = AllSkins[_rng.Next(AllSkins.Length)];
            _rouletteTape.Add(skin);
        }
        if (guaranteedWinner != null && _rouletteTape.Count > 52)
        {
            _rouletteTape[48] = guaranteedWinner;
        }
    }

    // Универсальный хелпер чёткого рендера кириллицы/текста
    public static float MeasureTextWidth(Font font, string text, int size) =>
        GameFonts.Measure(font,text,size).X;

    public static void DrawTextCrisp(Font font, string text, int x, int y, int size, Color color) =>
        GameFonts.Draw(font,text,new(x,y),size,color);

    // Обновление хоткеев и интерактивных элементов (вызывать каждый кадр)
    public static void Update(float dt, WeaponAndCombatView? weaponView)
    {
        if (weaponView != null)
            ActiveWeaponView = weaponView;
        Update(dt);
    }

    public static void Update(float dt)
    {
        if (_settingsStatusTimer > 0f)
            _settingsStatusTimer = Math.Max(0f, _settingsStatusTimer - dt);

        if (_caseWinFlashTimer > 0f)
            _caseWinFlashTimer = Math.Max(0f, _caseWinFlashTimer - dt);

        if (IsAnyModalOpen && Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            IsLoreOpen = IsCaseOpen = IsSettingsOpen = false;
            return;
        }
        // Переключение окон [N], [F8], [K]
        if (Raylib.IsKeyPressed(KeyboardKey.N))
        {
            IsLoreOpen = !IsLoreOpen;
            if (IsLoreOpen)
            {
                IsSettingsOpen = false;
                IsCaseOpen = false;
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.F8))
        {
            IsSettingsOpen = !IsSettingsOpen;
            if (IsSettingsOpen)
            {
                IsLoreOpen = false;
                IsCaseOpen = false;
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.K))
        {
            IsCaseOpen = !IsCaseOpen;
            if (IsCaseOpen)
            {
                IsLoreOpen = false;
                IsSettingsOpen = false;
            }
        }

        if (IsLoreOpen)
        {
            UpdateLoreInput(dt);
        }
        else if (IsSettingsOpen)
        {
            UpdateSettingsInput();
        }
        else if (IsCaseOpen)
        {
            UpdateCaseInput(dt);
        }
    }

    private static void UpdateLoreInput(float dt)
    {
        if (Click(ArchiveScreen(LoreClose))) { IsLoreOpen = false; return; }
        for (int i = 0; i < ArchiveTabs.Length; i++)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.One + i) ||
                Raylib.IsKeyPressed(KeyboardKey.Kp1 + i) || Click(ArchiveScreen(LoreTab(i))))
                ActiveLoreTab = i;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Tab)) ActiveLoreTab = (ActiveLoreTab + 1) % ArchiveTabs.Length;

        _scrollTarget = Math.Clamp(_scrollTarget - Raylib.GetMouseWheelMove() * 64, 0, _scrollLimit);
        LoreScrollOffset += (_scrollTarget - LoreScrollOffset) * (1 - MathF.Exp(-18 * dt));
        if (Math.Abs(_scrollTarget - LoreScrollOffset) < .25f) LoreScrollOffset = _scrollTarget;
    }

    // The artwork uses the same 2560 x 1440 coordinate space as the reference captures.
    private static float ArtScale => Math.Min(Raylib.GetScreenWidth()/2560f,Raylib.GetScreenHeight()/1440f);
    private static Rectangle Ref(float x,float y,float w,float h) =>
        new(Raylib.GetScreenWidth()/2f+(x-1280)*ArtScale,Raylib.GetScreenHeight()/2f+(y-720)*ArtScale,w*ArtScale,h*ArtScale);
    private static bool Click(Rectangle r) => Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(),r);
    private static void Dim(float opacity) => Raylib.DrawRectangle(0,0,Raylib.GetScreenWidth(),Raylib.GetScreenHeight(),Raylib.Fade(new Color(7,13,26,255),opacity));
    private static readonly string[] MixerNames = ["Ремень и паяльник","УЗИ и обрез","Ловушка и тревога","Галера и Таро","Кейсы"];
    private const int MusicRow = 6;
    private static readonly float[] BeforeMute = [1,1,1,1,1,1,1];
    private static Rectangle MixerRect(float x,float y,float w,float h) => new(Raylib.GetScreenWidth()/2f-410+x,Raylib.GetScreenHeight()/2f-292+y,w,h);
    private static Rectangle MixerTrack(int row) => MixerRect(305,103+row*43,250,22);
    private static Rectangle MixerSwitch(int row) => MixerRect(619,99+row*43,80,30);
    private static Rectangle MixerTest(int row) => MixerRect(709,99+row*43,83,30);
    private static Rectangle MixerClose => MixerRect(662,18,130,32);
    private static float MixerValue(int row) => row==0 ? MasterVolume : row == MusicRow ? MusicVolume : AudioMusicSystem.EffectLevels[row-1];
    private static void SetMixer(int row,float value)
    {
        value=Math.Clamp(value,0,1);
        if(row==0) ApplySliderNormalizedValue(0,value);
        else if (row == MusicRow) MusicVolume = value;
        else AudioMusicSystem.EffectLevels[row-1]=value;
    }
    private static void UpdateSettingsPointer(Vector2 mouse,bool pressed,bool down)
    {
        bool Pressed(Rectangle r) => pressed && Raylib.CheckCollisionPointRec(mouse,r);
        if (Pressed(MixerClose)) { IsSettingsOpen=false; return; }
        if (!down) _draggingSliderIndex=-1;
        for (int row=0;row<=MusicRow;row++)
        {
            Rectangle track=MixerTrack(row);
            if (Pressed(track)) _draggingSliderIndex=SelectedSliderIndex=row;
            if (_draggingSliderIndex==row) SetMixer(row,(mouse.X-track.X)/track.Width);
            if (Pressed(MixerSwitch(row)))
            {
                float v=MixerValue(row);
                if(v>0) BeforeMute[row]=v;
                SetMixer(row,v>0 ? 0 : BeforeMute[row]);
            }
            if (row>0 && Click(MixerTest(row)))
            {
                Action[] previews=[AudioMusicSystem.PlayBeltWhip,AudioMusicSystem.PlayShotgunBlast,AudioMusicSystem.PlayMacbookBait,AudioMusicSystem.PlayTarotCash,AudioMusicSystem.PlayCaseTick,AudioMusicSystem.PreviewMusic];
                previews[row-1]();
            }
        }
        for(int index=4;index<=5;index++)
        {
            Rectangle track=MixerRect(305,449+(index-4)*48,390,24);
            if(Pressed(track)) _draggingSliderIndex=10+index;
            if(_draggingSliderIndex==10+index) ApplySliderNormalizedValue(index,(mouse.X-track.X)/track.Width);
        }
    }

    private static void UpdateSettingsInput()
    {
        UpdateSettingsPointer(Raylib.GetMousePosition(),Raylib.IsMouseButtonPressed(MouseButton.Left),Raylib.IsMouseButtonDown(MouseButton.Left));
        if(Raylib.IsKeyPressed(KeyboardKey.Down)) SelectedSliderIndex=(SelectedSliderIndex+1)%(MusicRow+1);
        if(Raylib.IsKeyPressed(KeyboardKey.Up)) SelectedSliderIndex=(SelectedSliderIndex+MusicRow)%(MusicRow+1);
        if(Raylib.IsKeyPressed(KeyboardKey.Left)) SetMixer(SelectedSliderIndex,MixerValue(SelectedSliderIndex)-.05f);
        if(Raylib.IsKeyPressed(KeyboardKey.Right)) SetMixer(SelectedSliderIndex,MixerValue(SelectedSliderIndex)+.05f);
        if(Raylib.IsKeyPressed(KeyboardKey.F9)) TriggerGrantTestMoney();

    }

    internal static void CheckMixerControls()
    {
        float[] levels=Enumerable.Range(0,MusicRow+1).Select(MixerValue).ToArray();
        float[] beforeMute=(float[])BeforeMute.Clone();
        float sensitivity=MouseSensitivity,fov=CameraFov;
        void Require(bool condition,string name) { if(!condition) throw new InvalidOperationException("Mixer: "+name); }
        void Press(Rectangle r,float t=.5f)
        {
            Vector2 point=new(r.X+r.Width*t,r.Y+r.Height/2);
            UpdateSettingsPointer(point,true,true);
            UpdateSettingsPointer(point,false,false);
        }
        IsSettingsOpen=true;
        for(int row=0;row<=MusicRow;row++)
        {
            Press(MixerTrack(row),.37f);
            Require(Math.Abs(MixerValue(row)-.37f)<.001f,"slider "+row);
            Press(MixerSwitch(row));Require(MixerValue(row)==0,"mute "+row);
            Press(MixerSwitch(row));Require(Math.Abs(MixerValue(row)-.37f)<.001f,"unmute "+row);
            if(row>0) Press(MixerTest(row));
        }
        Press(MixerRect(305,449,390,24),.80f);
        Require(Math.Abs(MouseSensitivity-.41f)<.001f,"mouse sensitivity");
        Press(MixerRect(305,497,390,24),.80f);
        Require(CameraFov==92,"FOV");
        Press(MixerClose);Require(!IsSettingsOpen,"close");
        for(int row=0;row<=MusicRow;row++) SetMixer(row,levels[row]);
        beforeMute.CopyTo(BeforeMute,0);
        MouseSensitivity=sensitivity;CameraFov=fov;SyncAudioMixerToSystem();
        Console.WriteLine("[AUTOTEST] PASS mixer: seven sliders, seven mute/restore pairs, six previews, sensitivity, FOV, close");
    }

    internal static float GetSliderNormalizedValue(int index)
    {
        return index switch
        {
            0 => MasterVolume,
            1 => MusicVolume,
            2 => WeaponSfxVolume,
            3 => VoiceVolume,
            4 => Math.Clamp((MouseSensitivity - 0.05f) / 0.45f, 0f, 1f),
            5 => Math.Clamp((CameraFov - 60f) / 40f, 0f, 1f),
            _ => 0.5f
        };
    }

    private static void ApplySliderNormalizedValue(int index, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        switch (index)
        {
            case 0:
                MasterVolume = t;
                break;
            case 1:
                MusicVolume = t;
                break;
            case 2:
                WeaponSfxVolume = t;
                break;
            case 3:
                VoiceVolume = t;
                break;
            case 4:
                MouseSensitivity = MathF.Round(0.05f + t * 0.45f, 3);
                break;
            case 5:
                CameraFov = MathF.Round(60f + t * 40f, 0);
                break;
        }
        SyncAudioMixerToSystem();
    }

    internal static void TriggerGrantTestMoney()
    {
        OnGrantMoneyRequested?.Invoke(2000);
        _settingsStatusBanner = "ВЫДАНО +$2000 НА КАРМАН! МОЖНО БРАТЬ КЛЮЧ ОТ ЛИФТА И ТЕСТИТЬ БОССА!";
        _settingsStatusTimer = 4.0f;
        InvokeUiClickSound();
    }

    internal static void TriggerUnlockAllWeapons()
    {
        if (ActiveWeaponView != null)
        {
            ActiveWeaponView.UnlockedSolderingIron = true;
            ActiveWeaponView.UnlockedMicroUzi = true;
            ActiveWeaponView.UnlockedSawedOff = true;
            ActiveWeaponView.UnlockedChatGun = true;
        }
        OnUnlockAllWeaponsRequested?.Invoke();
        _settingsStatusBanner = "ВЕСЬ АРСЕНАЛ 90-Х ОТКРЫТ: РЕМЕНЬ, ПАЯЛЬНИК, MICRO-UZI И ОБРЕЗ!";
        _settingsStatusTimer = 4.0f;
        InvokeUiClickSound();
    }

    private static void UpdateCaseInput(float dt)
    {
        if (IsSpinningCase)
        {
            _caseScrollPx += _caseScrollSpeed * dt;
            _caseScrollSpeed = MathF.Max(0f, _caseScrollSpeed - 640f * dt);

            float centerNeedleOffset = _caseScrollPx + 425;
            int currentIdx = Math.Clamp((int)(centerNeedleOffset / 170), 0, _rouletteTape.Count - 1);

            if (currentIdx != _lastTickItemIndex)
            {
                _lastTickItemIndex = currentIdx;
                InvokeCaseTickSound();
            }

            if (_caseScrollSpeed <= 8f)
            {
                IsSpinningCase = false;
                _caseScrollSpeed = 0f;
                WeaponSkinDefinition won = _rouletteTape[currentIdx];
                AwardAndEquipSkin(won);
            }
        }
        else
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Space))
            {
                StartCaseSpin();
            }
        }

        if (Click(Ref(1580,390,220,55))) IsCaseOpen=false;
        if (!IsSpinningCase && Click(Ref(869,409,250,38))) _caseCollection=!_caseCollection;
        if (_caseCollection)
        {
            for(int i=0;i<AllSkins.Length;i++)
                if(Click(CollectionItem(i))) AwardAndEquipSkin(AllSkins[i]);
        }
        if(Click(Ref(1084,972,423,97)) && !IsSpinningCase) StartCaseSpin();
    }

    private static bool _caseCollection;
    private static Rectangle CollectionItem(int i) => Ref(800+(i%4)*246,484+(i/4)*137,239,129);

    internal static Func<bool>? RequestCasePayment;
    public static void StartCaseSpin()
    {
        if (IsSpinningCase)
            return;

        if(RequestCasePayment?.Invoke()==true) return;
        if (TrySpendPlayerMoney != null)
        {
            bool paid = TrySpendPlayerMoney(CaseSpinCost);
            if (!paid)
            {
                _caseBannerMessage = "НЕ ХВАТАЕТ БАБЛА ($100)! Сдай кодера Веронике или нажми [F8] -> +$2000!";
                InvokeUiClickSound();
                return;
            }
        }

        StartPaidCaseSpin();
    }

    internal static void StartPaidCaseSpin()
    {
        _caseCollection=false;
        WeaponSkinDefinition target = AllSkins[_rng.Next(AllSkins.Length)];
        RebuildCaseTape(target);
        _caseScrollPx = 0f;
        _caseScrollSpeed = 1650f + _rng.Next(0, 380);
        _lastTickItemIndex = -1;
        IsSpinningCase = true;
        _caseBannerMessage = "КРУТИТСЯ БАРАБАН 90-Х... СЛУШАЙ ТРЕЩОТКУ!";
        InvokeCaseTickSound();
    }

    public static void AwardAndEquipSkin(WeaponSkinDefinition skin)
    {
        LastWonSkin = skin;
        UnlockedSkinIds.Add(skin.Id);
        _caseWinFlashTimer = 2.5f;

        switch (skin.Slot)
        {
            case WeaponSkinSlot.Belt:
                BeltPrimaryColor = skin.Primary3D;
                BeltBuckleColor = skin.Secondary3D;
                EquippedBeltSkinName = skin.Name;
                break;
            case WeaponSkinSlot.SolderingIron:
                SolderingBodyColor = skin.Primary3D;
                SolderingTipColor = skin.Secondary3D;
                EquippedSolderingSkinName = skin.Name;
                break;
            case WeaponSkinSlot.MicroUzi:
                UziBodyColor = skin.Primary3D;
                UziAccentColor = skin.Secondary3D;
                EquippedUziSkinName = skin.Name;
                break;
            case WeaponSkinSlot.SawedOffOrChatGun:
                ShotgunBodyColor = skin.Primary3D;
                ShotgunAccentColor = skin.Secondary3D;
                EquippedShotgunSkinName = skin.Name;
                break;
        }

        if (ActiveWeaponView != null)
        {
            ActiveWeaponView.ApplySkin(skin.Name, skin.Primary3D, skin.Secondary3D);
            ActiveWeaponView.SelectSlot((int)skin.Slot + 1);
        }

        _caseBannerMessage = $"ВЫПАЛ СКИН: {skin.Name.ToUpperInvariant()} - СРАЗУ ПОКРАШЕНО В РУКАХ!";
        OnSkinEquipped?.Invoke(skin);
    }

    // Отрисовка всех открытых окон поверх 3D-сцены
    public static void Draw(Font font)
    {
        if (IsLoreOpen)
        {
            DrawLoreWindow(font);
        }
        else if (IsSettingsOpen)
        {
            DrawSettingsWindow(font);
        }
        else if (IsCaseOpen)
        {
            DrawCaseWindow(font);
        }
    }

    // Удобная перегрузка для вызова с кастомным балансом игрока
    public static void Draw(Font font, int playerMoney)
    {
        GetPlayerMoney = () => playerMoney;
        Draw(font);
    }

    // =========================================================================
    // 1. ОКНО ДОСЬЕ И СЛИТЫХ ЧАТОВ ОПЕНСПЕЙСА [N] (1020x600)
    // =========================================================================
    private static readonly (string title, string verdict, string body, Color accent)[] TarotEntries =
        {
            (
                "Кандидат №1: Алексей, 38 лет - Senior C++ / Ядро СУБД / 14 лет опыта",
                "ОТКАЗ: ЛУНА В СКОРПИОНЕ + ТОКСИЧНЫЕ ВОПРОСЫ ПРО АРХИТЕКТУРУ",
                "«Спросил на созвоне про консистентность транзакций, бэкапы и нагрузку на память. Сразу почувствовала тяжёлую душную энергетику Козерога! Разложила Таро - выпала Башня. Очевидный абьюзер, разрушит наш хрупкий вайб!»",
                new Color(255, 92, 118, 255)
            ),
            (
                "Кандидат №2: Данила, 19 лет - Vibe Architect / 0 дней опыта / 400k токенов в день",
                "НАНЯТ НА $7,500/МЕС: ВЫПАЛА КАРТА «ТРОЙКА КУБКОВ» И «ШУТ»!",
                "«Мальчик вообще не знает, чем массив отличается от дерева, зато пришёл с матчей на кокосовом и сказал, что кодит по потоку Вселенной через кнопку Accept All! Карта Тройка Кубков показала чистый инсайт. Сразу дала prod-доступы!»",
                new Color(64, 245, 165, 255)
            ),
            (
                "Кандидат №3: Отдел из 40 классических инженеров (Backend + QA + SecOps)",
                "МАССОВЫЙ ОТСЕВ ПО НАТАЛЬНОЙ КАРТЕ (МЕРКУРИЙ В ДЕВЕ)",
                "«У всех сорока человек ретроградный Меркурий в Деве - заставляли джунов писать юнит-тесты и проверяли SQL-запросы! Уволила всех одним днём, теперь наш коммерс в малиновом пиджаке приносит мне покорных вайб-кодеров за наличку!»",
                new Color(255, 206, 68, 255)
            )
        };

    private static readonly (string zoomer, string nineties, string comment)[] DictionaryEntries =
        {
            ("Дейлик (Daily Standup)", "Стрелка по понятиям", "Кто не отчитался за общак - получает штрафной наряд на Галере"),
            ("Деплой в прод (Deploy)", "Отгрузка фуры без тормозов", "Если фура перевернулась в пятницу - отвечает весь цех"),
            ("Код-ревью (Code Review)", "Допрос с паяльником 220В", "Сразу выясняется, кто писал руками, а кто жал Accept All"),
            ("Выгорание (Burnout)", "Отлынивание от галеры (лечится ремнём)", "Один отцовский шлепок - и дофамин снова на максимуме"),
            ("Опционы стартапа", "Билеты МММ Мавроди", "Красивая бумажка для мамонтов, пока коммерс считает кэш в чемодане"),
            ("Промпт-инженер", "Пацан, который печатает в чат", "Думает, что он хакер, пока не кончится турецкая подписка"),
            ("Ретроспектива спринта", "Разбор полётов в бане", "Выясняем, кто крыса и кто слил ключи от AWS во фронтенд")
        };

    private static readonly string[] PressRelease =
    [
        "«Наш автономный агент обрёл AGI!»",
        "В 02:14 ночи ИИ-агент v0.1 осознал себя, самостоятельно преодолел песочницу и переписал собственное ядро Linux на Rust.",
        "Агент продемонстрировал сверхразумное планирование, захватил Северный лифт и потребовал опционы серии А на $10 млрд.",
        "Акции Cloud Entropic выросли на 420% на новостях о прорыве к сингулярности."
    ];
    private static readonly string[] CameraLog =
    [
        "«Какой нахрен сверхразум, пацаны?!»",
        "Тумбочка на колёсиках с ЭЛТ-монитором, перемотанная синей изолентой, с разгона врезалась в офисный кулер с водой!",
        "Намотала на левое колесо удлинитель от чайника и 6 часов подряд орала «Я взломал Пентагон!» в закрытую дверь шваброчной, крутясь волчком в while(true).",
        "Кидается папками node_modules по 40 ГБ. Вырубается хорошей очередью из УЗИ!"
    ];
    private static readonly string[] ArchiveTabs = ["Слитые чаты", "Дневник HR", "Досье ИИ-агента", "Словарь 1994"];
    private const int ArchiveWidth = 940, ArchiveHeight = 560, IndexWidth = 235;
    private const int ContentX = 252, ContentRight = 920, ContentY = 79, ContentHeight = 450;
    private static readonly Rectangle LoreClose = new(826, 20, 94, 28);
    private static Rectangle LoreTab(int i) => new(0, 178 + i * 48, IndexWidth, 48);
    private static float ArchiveScale => Math.Min(1, Math.Min(Raylib.GetScreenWidth() / (float)ArchiveWidth,
                                                             Raylib.GetScreenHeight() / (float)ArchiveHeight));
    private static Rectangle ArchiveScreen(Rectangle r)
    {
        float s = ArchiveScale;
        return new(MathF.Round((Raylib.GetScreenWidth() - ArchiveWidth * s) / 2) + r.X * s,
                   MathF.Round((Raylib.GetScreenHeight() - ArchiveHeight * s) / 2) + r.Y * s,
                   r.Width * s, r.Height * s);
    }
    private static void ReaderButton(Font f,string title,Rectangle r,bool active)
    {
        Raylib.DrawRectangleRec(r, active ? new Color(235, 168, 56, 255) : new Color(18, 28, 42, 255));
        Raylib.DrawRectangleLinesEx(r, 1f, active ? new Color(255, 206, 102, 255) : new Color(58, 78, 104, 255));
        MainMenuAndPauseUI.Center(f,title,r,14,active?Color.Black:new Color(206,218,234,255));
    }
    private static void Type(string text, float x, float y, int size, Color ink, Typeface face = Typeface.Interface)
    {
        var font = GameFonts.Get(face, size);
        GameFonts.Draw(font, text, new(x, y), size, ink);
    }

    private static void Fill(float x, float y, float w, float h, Color ink) =>
        Raylib.DrawRectangleRec(new(x, y, w, h), ink);

    private static readonly Dictionary<(string, int, int, Typeface), string[]> ParagraphLines = new();
    private static float Paragraph(string text, int x, float y, int width, int size, int leading,
                                   Color ink, Typeface face = Typeface.Interface)
    {
        var key = (text, width, size, face);
        if (!ParagraphLines.TryGetValue(key, out var lines))
        {
            Font font = GameFonts.Get(face, size);
            var result = new List<string>();
            string line = "";
            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string next = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && GameFonts.Measure(font, next, size).X > width)
                {
                    result.Add(line);
                    line = word;
                }
                else line = next;
            }
            result.Add(line);
            ParagraphLines[key] = lines = result.ToArray();
        }
        foreach (var line in lines)
        {
            Type(line, x, y, size, ink, face);
            y += leading;
        }
        return y;
    }

    private static void DrawArchiveIndex()
    {
        Fill(0, 0, IndexWidth, ArchiveHeight, Paper);
        Type("ДАМП СЕРВЕРА // ЭТАЖ 01", 16, 17, 12, Ink, Typeface.InterfaceBold);
        Fill(16, 40, 202, 2, Ink);
        Type("АРХИВ", 14, 48, 43, Ink, Typeface.SerifBold);
        Type("БАШНИ", 14, 90, 43, Ink, Typeface.SerifBold);
        Type("ПЕРЕХВАТ АКТИВЕН", 17, 143, 12, new Color(36, 91, 73, 255), Typeface.InterfaceBold);
        for (int i = 0; i < ArchiveTabs.Length; i++)
        {
            var r = LoreTab(i);
            bool active = ActiveLoreTab == i;
            bool hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), ArchiveScreen(r));
            if (active || hover) Raylib.DrawRectangleRec(r, active ? Ink : new Color(202, 201, 187, 255));
            Fill(0, r.Y, IndexWidth, 1, SidebarRule);
            if (active) Fill(0, r.Y, 4, r.Height, Amber);
            Type((i + 1).ToString("00"), 16, r.Y + 9, 25, active ? Amber : Ink, Typeface.SerifBold);
            Type(ArchiveTabs[i].ToUpperInvariant(), 70, r.Y + 17, 13, active ? Paper : Ink, Typeface.InterfaceBold);
        }
        int y = 437;
        Fill(16, y, 202, 2, Ink);
        Type("СВОДКА ЭТАЖА", 16, y + 9, 11, Ink, Typeface.InterfaceBold);
        void Status(string name, string value, int top, Color color)
        {
            Type(name, 16, top, 14, Ink);
            int width = (int)Math.Ceiling(GameFonts.Measure(GameFonts.Get(Typeface.InterfaceBold, 14), value, 14).X);
            Type(value, 218 - width, top, 14, color, Typeface.InterfaceBold);
        }
        Status("Перехвачено тредов", _leakedChats.Length.ToString("00"), y + 33, Ink);
        Status("billing_prod", "DOWN", y + 59, new Color(153, 49, 36, 255));
        Status("Долг AWS", "$54,000", y + 85, Ink);
    }

    private static float DrawThread(LoreChatMessage m, float y)
    {
        const int x = ContentX + 44;
        Fill(ContentX, y + 9, 3, 127, m.HandleColor);
        Fill(ContentX + 8, y + 10, 28, 28, m.HandleColor);
        Type(m.Monogram, ContentX + 11, y + 16, 14, Ink, Typeface.InterfaceBold);
        Type(m.Handle, x, y + 7, 16, m.HandleColor, Typeface.InterfaceBold);
        int roleX = x + 12 + (int)Math.Ceiling(GameFonts.Measure(GameFonts.Get(Typeface.InterfaceBold, 16), m.Handle, 16).X);
        int badgeWidth = 12 + (int)Math.Ceiling(GameFonts.Measure(GameFonts.Get(Typeface.InterfaceBold, 11), m.RoleBadge, 11).X);
        Fill(roleX, y + 7, badgeWidth, 19, Rule);
        Type(m.RoleBadge, roleX + 6, y + 10, 11, Paper, Typeface.InterfaceBold);

        float right = ContentRight - 4;
        var stamp = GameFonts.Get(Typeface.Interface, 12);
        Type(m.Time, right - GameFonts.Measure(stamp, m.Time, 12).X, y + 7, 12, Ash);
        for (int i = m.Metrics.Length - 1; i >= 0; i--)
        {
            var metric = m.Metrics[i];
            string label = $"{metric.Label} {metric.Count}";
            float width = GameFonts.Measure(stamp, label, 11).X + 12;
            right -= width;
            Fill(right, y + 26, width, 16, new Color(44, 49, 46, 255));
            Type(label, right + 6, y + 28, 11, metric.Accent);
            right -= 3;
        }

        float end = Paragraph(m.Text, x, y + 45, ContentRight - x - 4, 15, 18, Paper);
        float replyY = end + 4;
        const int replyX = x + 18;
        Type($"ОТВЕТ {m.ReplyAuthor}:", replyX + 13, replyY, 13, Mint, Typeface.InterfaceBold);
        // Bent reply arrow drawn as strokes so font fallback cannot replace it.
        Fill(replyX + 2, replyY + 3, 1, 6, Mint);
        Fill(replyX + 2, replyY + 8, 6, 1, Mint);
        Raylib.DrawTriangle(new(replyX + 7, replyY + 5), new(replyX + 7, replyY + 12), new(replyX + 11, replyY + 8), Mint);
        end = Paragraph(m.ReplyText, replyX + 2, replyY + 16, ContentRight - replyX - 6, 14, 17, Paper);
        Fill(replyX - 6, replyY, 2, end - replyY, Mint);
        float next = Math.Max(y + ContentHeight / 3, end + 8);
        Fill(ContentX, next - 1, ContentRight - ContentX, 1, Rule);
        return next;
    }

    private static float DrawArchiveBody(float y)
    {
        const int width = ContentRight - ContentX;
        switch (ActiveLoreTab)
        {
            case 0:
                foreach (var thread in _leakedChats) y = DrawThread(thread, y);
                break;
            case 1:
                foreach (var e in TarotEntries)
                {
                    y = Paragraph(e.title, ContentX, y + 12, width, 18, 23, Paper, Typeface.InterfaceBold);
                    y = Paragraph(e.verdict, ContentX, y + 5, width, 13, 17, e.accent, Typeface.InterfaceBold);
                    y = Paragraph(e.body, ContentX + 18, y + 8, width - 18, 15, 20, Paper) + 14;
                    Fill(ContentX, y - 1, width, 1, Rule);
                }
                break;
            case 2:
                float Column(int x, string title, string[] paragraphs, Color accent)
                {
                    const int columnWidth = 318;
                    float at = Paragraph(title, x, y + 12, columnWidth, 14, 19, accent, Typeface.InterfaceBold) + 10;
                    for (int i = 0; i < paragraphs.Length; i++)
                        at = Paragraph(paragraphs[i], x, at, columnWidth, i == 0 ? 19 : 15, i == 0 ? 24 : 20,
                                       Paper, i == 0 ? Typeface.InterfaceBold : Typeface.Interface) + 12;
                    return at;
                }
                float left = Column(ContentX, "ОФИЦИАЛЬНЫЙ ПРЕСС-РЕЛИЗ PR", PressRelease, Amber);
                float right = Column(ContentX + 348, "КАМЕРЫ НАБЛЮДЕНИЯ ПЕТРОВИЧА", CameraLog, Mint);
                float bottom = Math.Max(left, right);
                Fill(ContentX + 333, y + 10, 1, bottom - y - 18, Rule);
                Fill(ContentX, bottom, width, 1, Rule);
                Type("СЕВЕРНЫЙ ЛИФТ", ContentX, bottom + 10, 13, Amber, Typeface.InterfaceBold);
                y = Paragraph("Купи ключ за $1000. Прячься за колоннами от кубов node_modules.", ContentX, bottom + 30, width, 15, 20, Paper);
                break;
            case 3:
                const int split = ContentX + 245;
                Type("ЗУМЕРСКИЙ ТЕРМИН", ContentX, y + 10, 13, Amber, Typeface.InterfaceBold);
                Type("ПОНЯТИЕ 90-Х", split + 16, y + 10, 13, Amber, Typeface.InterfaceBold);
                float start = y;
                y += 36;
                foreach (var e in DictionaryEntries)
                {
                    Fill(ContentX, y, width, 1, Rule);
                    float termEnd = Paragraph(e.zoomer, ContentX, y + 12, 232, 16, 20, Paper, Typeface.InterfaceBold);
                    float valueEnd = Paragraph(e.nineties, split + 16, y + 12, 395, 16, 20, Paper, Typeface.InterfaceBold);
                    valueEnd = Paragraph(e.comment, split + 16, valueEnd + 3, 395, 14, 18, Ash);
                    y = Math.Max(termEnd, valueEnd) + 10;
                }
                Fill(split, start + 8, 1, y - start - 8, Rule);
                break;
        }
        return y;
    }

    public static void DrawLoreWindow(Font font)
    {
        Dim(.68f);
        Rectangle screen = ArchiveScreen(new(0, 0, ArchiveWidth, ArchiveHeight));
        Raylib.BeginMode2D(new Camera2D { Offset = new(screen.X, screen.Y), Zoom = ArchiveScale });
        Fill(0, 0, ArchiveWidth, ArchiveHeight, Ink);
        DrawArchiveIndex();
        Type(ArchiveTabs[ActiveLoreTab], ContentX, 14, 35, Paper, Typeface.SerifBold);
        bool hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), ArchiveScreen(LoreClose));
        Raylib.DrawRectangleLinesEx(LoreClose, 1, hover ? Amber : Rule);
        Type("Закрыть [N]", LoreClose.X + 9, LoreClose.Y + 6, 13, hover ? Amber : Ash);
        Fill(ContentX, ContentY - 1, ContentRight - ContentX, 1, Ash);
        Rectangle clip = ArchiveScreen(new(ContentX, ContentY, ContentRight - ContentX, ContentHeight));
        Raylib.BeginScissorMode((int)MathF.Round(clip.X), (int)MathF.Round(clip.Y),
                                (int)MathF.Round(clip.Width), (int)MathF.Round(clip.Height));
        float top = ContentY - LoreScrollOffset;
        float height = DrawArchiveBody(top) - top;
        Raylib.EndScissorMode();
        _scrollLimit = Math.Max(0, height - ContentHeight);
        if (_scrollLimit > 0)
        {
            float thumb = ContentHeight * ContentHeight / height;
            Fill(930, ContentY, 2, ContentHeight, Rule);
            Fill(930, ContentY + (ContentHeight - thumb) * LoreScrollOffset / _scrollLimit, 2, thumb, Amber);
        }
        Type("1–4 / Tab   Разделы", ContentX, 538, 12, Ash);
        if (_scrollLimit > 0) Type("Колесо   Прокрутка", ContentRight - 139, 538, 12, Ash);
        Raylib.EndMode2D();
    }

    public static void DrawSettingsWindow(Font font)
    {
        Dim(.55f);
        MainMenuAndPauseUI.Surface(MixerRect(0,0,820,584),new Color(13,21,34,253),1);
        DrawTextCrisp(font,"ЗВУКОВАЯ РУБКА [F8]",(int)MixerRect(24,19,0,0).X,(int)MixerRect(24,19,0,0).Y,25,new Color(245,177,72,255));
        DrawTextCrisp(font,"CLOUD ENTROPIC TOWER / АУДИО И УПРАВЛЕНИЕ",(int)MixerRect(25,55,0,0).X,(int)MixerRect(25,55,0,0).Y,13,new Color(151,171,196,255));
        ReaderButton(font,"Закрыть [F8]",MixerClose,false);
        void Slider(Rectangle r,float value)
        {
            Raylib.DrawRectangleRounded(new(r.X,r.Y+8,r.Width,6),1,6,new Color(46,62,80,255));
            if(value>0) Raylib.DrawRectangleRounded(new(r.X,r.Y+8,r.Width*value,6),1,6,new Color(51,195,209,255));
            Raylib.DrawRectangleRounded(new(r.X+r.Width*value-5,r.Y+1,10,20),.3f,6,Color.White);
        }
        for(int row=0;row<=MusicRow;row++)
        {
            Rectangle r=MixerRect(18,95+row*43,784,38);
            Raylib.DrawRectangleRounded(r,.12f,6,new Color(20,32,48,255));
            string name=row==0?"Общая громкость":row==MusicRow?"Музыка и босс":MixerNames[row-1];
            DrawTextCrisp(font,name,(int)r.X+12,(int)r.Y+8,18,Color.White);
            float value=MixerValue(row);
            Slider(MixerTrack(row),value);
            MainMenuAndPauseUI.Center(font,$"{value*100:0}%",MixerRect(561,99+row*43,51,30),15,Color.White);
            ReaderButton(font,value==0?"Mute: вкл":"Mute",MixerSwitch(row),value==0);
            if(row>0) ReaderButton(font,"Тест",MixerTest(row),false);
        }
        DrawTextCrisp(font,"УПРАВЛЕНИЕ",(int)MixerRect(28,410,0,0).X,(int)MixerRect(28,410,0,0).Y,16,new Color(245,177,72,255));
        for(int index=4;index<=5;index++)
        {
            int y=449+(index-4)*48;
            DrawTextCrisp(font,index==4?"Чувствительность мыши":"Угол обзора (FOV)",(int)MixerRect(30,y,0,0).X,(int)MixerRect(30,y,0,0).Y,18,Color.White);
            Slider(MixerRect(305,y,390,24),GetSliderNormalizedValue(index));
            MainMenuAndPauseUI.Center(font,index==4?MouseSensitivity.ToString("0.00"):CameraFov.ToString("0°"),MixerRect(705,y,87,24),18,Color.White);
        }
        MainMenuAndPauseUI.Center(font,_settingsStatusTimer>0?_settingsStatusBanner:"Перетаскивай ползунки. Стрелки меняют громкость. F8 закрывает окно.",MixerRect(24,550,772,22),14,new Color(151,171,196,255));
    }

    public static void DrawCaseWindow(Font font)
    {
        Dim(.56f);
        MainMenuAndPauseUI.Surface(Ref(720,373,1100,699),new Color(13,21,34,253),1);
        ReaderButton(font,"Закрыть [K]",Ref(1580,390,220,55),false);
        Rectangle title=Ref(1094,373,485,59);
        Raylib.DrawRectangleRounded(title,.1f,6,new Color(33,53,73,255));
        MainMenuAndPauseUI.Center(MainMenuAndPauseUI.Serif,"Контрабандный кейс",title,24,Color.White);
        ReaderButton(font,_caseCollection?"К рулетке":"Коллекция ("+AllSkins.Length+")",Ref(869,410,250,37),false);
        if(_caseCollection)
        {
            for(int i=0;i<AllSkins.Length;i++)
            {
                var skin=AllSkins[i];
                ReaderButton(font,skin.Name,CollectionItem(i),LastWonSkin==skin);
            }
        }
        else
        {
            Rectangle track=Ref(866,721,850,200);
            Raylib.DrawRectangleRec(track,new Color(17,24,34,255));
            Raylib.BeginScissorMode((int)track.X,(int)track.Y,(int)track.Width,(int)track.Height);
            int first=(int)(_caseScrollPx/170);
            for(int i=first;i<Math.Min(first+6,_rouletteTape.Count);i++)
            {
                var skin=_rouletteTape[i];
                float x=866+i*170-_caseScrollPx;
                Rectangle card=Ref(x+8,727,151,188);
                Raylib.DrawRectangleRounded(card,.12f,6,new Color(23,38,58,255));
                Raylib.DrawRectangleRoundedLinesEx(card,.12f,6,2,skin.CardColor);
                Program.DrawSlotIcon((int)skin.Slot,new(card.X+card.Width/2,card.Y+card.Height*.39f),true,skin.Secondary3D);
                string name=skin.Name.Split('«').Last().TrimEnd('»');
                MainMenuAndPauseUI.Center(font,skin.WeaponLabel.Replace("СЛОТ "+((int)skin.Slot+1)+": ",""),Ref(x+8,847,151,28),12,Color.White);
                MainMenuAndPauseUI.Center(font,name,Ref(x+9,873,149,26),11,Color.White);
                Raylib.DrawRectangleRec(Ref(x+11,900,145,9),skin.CardColor);
            }
            Raylib.EndScissorMode();
            Raylib.DrawRectangleRec(Ref(1289,684,4,237),new Color(245,177,72,255));
        }
        Rectangle buy=Ref(1105,990,376,62);
        Raylib.DrawRectangleRounded(buy,.19f,6,new Color(22,97,70,255));
        MainMenuAndPauseUI.Center(font,IsSpinningCase?"Открываем...":$"Открыть кейс — ${CaseSpinCost}",buy,21,Color.White);
        MainMenuAndPauseUI.Center(font,_caseBannerMessage,Ref(695,1074,1170,45),14,Color.White);
    }

    // Прямые вызовы AudioMusicSystem (щелчок трещотки кейса [K] и синхронизация микшера [F8])
    private static void InvokeCaseTickSound()
    {
        try
        {
            AudioMusicSystem.PlayCaseTick();
        }
        catch
        {
        }
    }

    private static void InvokeUiClickSound()
    {
        try
        {
            AudioMusicSystem.PlayCaseTick();
        }
        catch
        {
        }
    }

    private static void SyncAudioMixerToSystem()
    {
        try
        {
            AudioMusicSystem.MasterVolume = MasterVolume;
            AudioMusicSystem.MusicVolume = MusicVolume;
            AudioMusicSystem.WeaponVolume = WeaponSfxVolume;
            AudioMusicSystem.VoiceVolume = VoiceVolume;
        }
        catch
        {
        }
    }
}
