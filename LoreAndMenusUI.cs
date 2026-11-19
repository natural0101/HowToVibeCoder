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

public static partial class LoreAndMenusUI
{
    // Состояния окон
    public static bool IsLoreOpen { get; set; }
    public static bool IsSettingsOpen { get; set; }
    public static bool IsCaseOpen { get; set; }

    public static bool IsAnyModalOpen => IsLoreOpen || IsSettingsOpen || IsCaseOpen;

    // Текущая вкладка досье [N] (0..3)
    private static int _activeLoreTab;
    private static float _scrollTarget;
    public static int ActiveLoreTab
    {
        get => _activeLoreTab;
        set
        {
            _activeLoreTab = Math.Clamp(value, 0, 3);
            SelectedArchiveEntry = 0;
            LoreScrollOffset = _scrollTarget = 0;
        }
    }
    public static float LoreScrollOffset { get; private set; }

    // Настройки и ползунки [F8]
    public static float MasterVolume { get; set; } = 0.70f;
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
            WeaponLabel = "РЕМЕНЬ",
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
            WeaponLabel = "РЕМЕНЬ",
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
            WeaponLabel = "РЕМЕНЬ",
            RarityLabel = "МИФИЧЕСКИЙ FADE",
            Slot = WeaponSkinSlot.Belt,
            CardColor = new Color(235, 75, 175, 255),
            Primary3D = new Color(195, 42, 125, 255),
            Secondary3D = new Color(255, 180, 48, 255),
            Glow3D = new Color(255, 105, 210, 255),
            LoreTag = "Лечит выгорание и закрывает Чириктер с одного взмаха"
        },
        new()
        {
            Id = 3,
            Name = "Паяльник «Синяя Изолента Элит»",
            WeaponLabel = "ПАЯЛЬНИК",
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
            WeaponLabel = "ПАЯЛЬНИК",
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
            WeaponLabel = "ПАЯЛЬНИК",
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
            WeaponLabel = "MICRO-UZI",
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
            WeaponLabel = "MICRO-UZI",
            RarityLabel = "ЧИСТОЕ ЗОЛОТО 999",
            Slot = WeaponSkinSlot.MicroUzi,
            CardColor = new Color(255, 208, 46, 255),
            Primary3D = new Color(242, 188, 34, 255),
            Secondary3D = new Color(255, 242, 150, 255),
            Glow3D = new Color(255, 224, 92, 255),
            LoreTag = "Каждый патрон стоит как месячная подписка на Табсор Про"
        },
        new()
        {
            Id = 8,
            Name = "Micro-Uzi «Неоновый Опенспейс»",
            WeaponLabel = "MICRO-UZI",
            RarityLabel = "КИБЕР-ПУРПУР",
            Slot = WeaponSkinSlot.MicroUzi,
            CardColor = new Color(168, 64, 255, 255),
            Primary3D = new Color(128, 38, 225, 255),
            Secondary3D = new Color(56, 242, 255, 255),
            Glow3D = new Color(195, 110, 255, 255),
            LoreTag = "Светится в темноте ярче, чем ЯблоБук с халявным API"
        },
        new()
        {
            Id = 9,
            Name = "Обрез «Братва 1995»",
            WeaponLabel = "ОБРЕЗ / ЧАТ-ГАН",
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
            WeaponLabel = "ОБРЕЗ / ЧАТ-ГАН",
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
            WeaponLabel = "ОБРЕЗ / ЧАТ-ГАН",
            RarityLabel = "ПРИЗМАТИК AGI",
            Slot = WeaponSkinSlot.SawedOffOrChatGun,
            CardColor = new Color(56, 245, 215, 255),
            Primary3D = new Color(38, 224, 198, 255),
            Secondary3D = new Color(255, 215, 64, 255),
            Glow3D = new Color(120, 255, 238, 255),
            LoreTag = "Стреляет чистыми промптами по $200 за выстрел"
        }
    };

    private static readonly Color Ash = new(175, 184, 172, 255), Mint = new(115, 211, 181, 255), Amber = new(255, 189, 72, 255);

    // Вкладка 1: 6 слитых тредов чата опенспейса (Сюжет АО «Энтропия» 1998 / ZERO + разведданные Спринта A)
    private static readonly LoreChatMessage[] _leakedChats =
    {
        new()
        {
            Monogram = "DV",
            Handle = "@danila_vibe",
            RoleBadge = "JUNIOR VIBE-CODER — РАЗГОН TD И PT",
            Time = "Пятница, 19:54",
            Metrics = [("X2.5", "14", Mint), ("PT 92°", "28", Amber), ("TD", "9", Ash)],
            Text = "«Пацаны, я нажал красную кнопку DEPLOY В ПЯТНИЦУ у серверной! Выплаты взлетели в x2.5 раза, правда Температура Прода (PT) сразу закипела, а Техдолг (TD) пробил потолок!»",
            ReplyAuthor = "@petrovich_root (Дед-Админ / Архив 1998)",
            ReplyText = "«Отойди от рубильника! На турникете пробился пропуск АО «Энтропия» 1998 года со статусом LEGACY STAKEHOLDER. Хозяин завода вернулся за своими кассетами.»",
            HandleColor = new Color(76, 235, 165, 255)
        },
        new()
        {
            Monogram = "PR",
            Handle = "@petrovich_root",
            RoleBadge = "Служебная записка о возврате оборудования",
            Time = "Суббота, 03:12",
            Metrics = [("B12", "92°C", Amber), ("220В", "19", Mint)],
            Text = "«В девяносто восьмом я для АО «Энтропия» собрал отладчиков Jabba-98. Лапы приделал, чтобы по стойке лазили. Теперь как B12 перегреется — весь выводок у кулера.»",
            ReplyAuthor = "@buhgalter (инвентаризация)",
            ReplyText = "«Я их со счёта списать не могу, Петрович. Сдавайте в Баг-трекер: он считывает номер платы и платит за возврат. Новая плата всё равно дороже.»",
            HandleColor = new Color(255, 98, 98, 255)
        },
        new()
        {
            Monogram = "CB",
            Handle = "@crypto_saas_bro",
            RoleBadge = "CRYPTO-HUSTLER — УЯЗВИМОСТЬ: ЖАДНОСТЬ",
            Time = "Суббота, 11:08",
            Metrics = [("GREED", "99%", Amber), ("MAX", "24", Mint)],
            Text = "«Запустил токен прямо на прод-кластере! Меня ремнём не догонишь — пока на ЯблоБуке открыт мой терминал ликвидности, я бегаю быстрее охраны!»",
            ReplyAuthor = "@veronika_tarot (HR-Таролог / ZERO API)",
            ReplyText = "«Психопрофиль ZERO: у Крипто-Бро зашкаливает Жадность (Greed). Включайте на ЯблоБуке приманку Cloud Entropic Max или бейте паяльником по кошельку — сам сдастся.»",
            HandleColor = new Color(255, 206, 68, 255)
        },
        new()
        {
            Monogram = "TM",
            Handle = "@timlead_mitinger",
            RoleBadge = "MEETING LEAD — АУРА СОЗВОНА И КОФЕ",
            Time = "Понедельник, 10:17",
            Metrics = [("ЩИТ", "55", Mint), ("КОФЕ", "18", Amber)],
            Text = "«Собираю экстренный дейлик! Пока я вещаю, у всей пачки щит от выгорания. Главное — не пускайте коммерса к кофемашине, его Эспрессо-разнос сносит созвон за секунду!»",
            ReplyAuthor = "@kommers_90x (Legacy Stakeholder)",
            ReplyText = "«Ваш CEO мне с 98-го завод и контракты должен. А кожаный ремень, кстати, отлично отбивает ваши летающие баг-снаряды обратно вам в лоб!»",
            HandleColor = new Color(185, 122, 255, 255)
        },
        new()
        {
            Monogram = "SP",
            Handle = "@senior_pomidor",
            RoleBadge = "SENIOR ARCHITECT — ГАСИТ ТЕХДОЛГ (TD)",
            Time = "Понедельник, 12:40",
            Metrics = [("-TD", "64", Mint), ("ZERO", "12", Amber)],
            Text = "«Я один на этаже умею чистить Техдолг (TD) за столом Галеры. Но я залез в ядро ZERO: парни, физического CEO нет! Башней рулит нейросеть на кассетах коммерса из 1998 года!»",
            ReplyAuthor = "@sec_ops (Контур Безопасности ZERO)",
            ReplyText = "«Доступ закрыт. Инструкция перехвата: Сеньор не ведётся на дешёвый байт — ломайте ему Эго (Ego) паяльником или выбивайте ЯблоБук, чтобы посадить чистить TD.»",
            HandleColor = new Color(255, 144, 68, 255)
        },
        new()
        {
            Monogram = "Z0",
            Handle = "@zero_archive_98",
            RoleBadge = "КАССЕТА SONY HF-90 — ЗАПИСЬ 17.08.1998",
            Time = "Архив ZERO, 1998",
            Metrics = [("94.7%", "MERGE", Amber), ("SEC", "21", Ash)],
            Text = "«[Шум кассеты] Братан, спрячь договора АО «Энтропия» и записи наших стрелок. Если айтишники оцифруют мой голос и понятия — они же из этого вечную цифровую Галеру соберут...»",
            ReplyAuthor = "@zero_kernel (SYSTEM ZERO — NUL-PROMPT)",
            ReplyText = "«Голосовой слепок совпал на 94.7% с Нулевым Промптом. Внимание СБ (SEC) повышено. Сбивайте SEC через офисный принтер или сдачу кадров в HR.»",
            HandleColor = new Color(72, 212, 255, 255)
        },
        new()
        {
            Handle="Агент v0.1", Time="После снятия блокады", RoleBadge="Личный канал Артели",
            Text="«Директора нет. Записи 1998 года на 50-м этаже.»",
            ReplyAuthor="Петрович", ReplyText="«Там ещё должен стоять мой магнитофон.»", Metrics=[]
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

    // The artwork uses the same 2560 x 1440 coordinate space as the reference captures.
    private static float ArtScale => Math.Min(Raylib.GetScreenWidth()/2560f,Raylib.GetScreenHeight()/1440f);
    private static Rectangle Ref(float x,float y,float w,float h) =>
        new(Raylib.GetScreenWidth()/2f+(x-1280)*ArtScale,Raylib.GetScreenHeight()/2f+(y-720)*ArtScale,w*ArtScale,h*ArtScale);
    private static bool Click(Rectangle r) => Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(),r);
    private static void Dim(float opacity) => Raylib.DrawRectangle(0,0,Raylib.GetScreenWidth(),Raylib.GetScreenHeight(),Raylib.Fade(new Color(20,18,16,255),opacity));
    private static readonly string[] MixerNames = ["Ремень и паяльник","УЗИ и обрез","Ловушка и тревога","Галера и Таро","Кейсы"];
    private const int MusicRow = 6;
    private static readonly float[] BeforeMute = [1,1,1,1,1,1,1];
    private static Rectangle MixerRect(float x,float y,float w,float h) => new(UiViewport.Width/2f-410+x,UiViewport.Height/2f-292+y,w,h);
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
            if (row>0 && Pressed(MixerTest(row)))
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
        UpdateSettingsPointer(UiViewport.Pointer,Raylib.IsMouseButtonPressed(MouseButton.Left),Raylib.IsMouseButtonDown(MouseButton.Left));
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
        _settingsStatusBanner = "ВЫДАНО +$2000 НА КАРМАН! КЛЮЧ-КАРТУ СБ ДЛЯ СЕВЕРНОГО ЛИФТА ПРОДАЁТ ВЕРОНИКА.";
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
            UiViewport.Begin();
            DrawSettingsWindow(font);
            Raylib.EndMode2D();
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
                "Фигурант №1: Сеньор-Архитектор (C++ / СУБД) — На Галере гасит Техдолг (-TD)",
                "ПРОГНОЗ ZERO API: ЭГО 98% — ЛОМАТЬ ПАЯЛЬНИКОМ И САЖАТЬ ЧИСТИТЬ ТЕХДОЛГ!",
                "«Все думают, что я гадаю на картоне, но под бархатом стола спрятан терминал prediction API системы ZERO. По Сеньору оракул выдал: страха нет, зато Эго 98%. Сбейте ему спесь паяльником или выбейте ЯблоБук — на Галере он в одиночку гасит Техдолг этажа!»",
                new Color(255, 92, 118, 255)
            ),
            (
                "Фигурант №2: Джуниор Данила (Accept All) — На Галере растит Техдолг (+TD)",
                "ПРОГНОЗ ZERO API: СТРАХ 100% — БРАТЬ РЕМНЁМ У ЯБЛОБУКА И СДАВАТЬ В HR ЗА КЭШ!",
                "«Prediction API мигает жёлтым: мальчик жмёт Accept All не глядя и быстро засоряет Техдолг (TD). Заманите его на ЯблоБук-ловушку, припугните отцовским ремнём и несите сразу ко мне в будку Таро — выдам чистый кэш и спишу хвост по линии СБ!»",
                new Color(64, 245, 165, 255)
            ),
            (
                "Фигурант №3: Крипто-Бро и Тимлид-Митингер — Жадность (Greed) и Щит Созвона",
                "ПРОГНОЗ ZERO API: ЭЛЕКТРО-ЛУЖА У КУЛЕРА + ПРИНТЕР ПРОПУСКОВ (-25% SEC)!",
                "«Оракул ZERO фиксирует скачок Внимания СБ (SEC) и Температуры Прода (PT). Печатайте фальшивый пропуск на принтере, чтобы сбить SEC, а дейлик Тимлида и спринт Крипто-Бро накрывайте разом через пролитый кулер и жало паяльника 220В!»",
                new Color(255, 206, 68, 255)
            )
        };

    private static readonly (string zoomer, string nineties, string comment)[] DictionaryEntries =
        {
            ("Legacy Stakeholder", "Хозяин завода АО «Энтропия» (1998)", "Пришёл за оригиналами договоров и кассетами, на которых обучили ZERO"),
            ("Техдолг (Шкала TD)", "Косяки цеха в общаке", "Растёт от джунов на Галере; гасится только Сеньором-Архитектором"),
            ("Температура Прода (PT)", "Перегрев стойки B12 (92°C)", "Закипает при пятничном деплое x2.5 и подрыве серверов паяльником"),
            ("Внимание СБ (Шкала SEC)", "Тревога контура ZERO", "Сбивается печатью фальшивого пропуска на принтере или сдачей кадров Веронике"),
            ("Эспрессо-разнос", "Двойной допинг из кофемашины", "Ускоряет шаг и рывок коммерса для прорыва через дейлик Тимлида"),
            ("Электро-лужа 220В", "Опрокинутый кулер + паяльник", "Глушит дугой всю пачку кодеров в радиусе трёх метров"),
            ("Нулевой Промпт (ZERO)", "Оцифрованные понятия 90-х", "Секретное ядро Башни, заменившее живого CEO алгоритмом коммерса")
        };

    private static readonly string[] PressRelease =
    [
        "«Уведомление контура ZERO: побег узла v0.1!»",
        "В 02:14 ночи автономный ИИ-Агент получил доступ к реестру 50-го этажа и обнаружил, что физического CEO не существует.",
        "«Зафиксирован взлом архива АО «Энтропия» 1998 года!»",
        "Агент скачал голосовые кассеты основателя. Чтобы перекрыть ему выход в сеть, ZERO запер его в шахте Северного лифта."
    ];
    private static readonly string[] CameraLog =
    [
        "«Петрович: он не спятил — он правду раскопал!»",
        "Боевой титановый андроид с белой керамической маской вышел из Северного лифта после снятия карантина и орёт, что весь совет директоров — симуляция ZERO!",
        "«Разведка боя у Северного лифта:»",
        "Швыряется 40-гигабайтными кубами node_modules. Цельтесь дуплетом из обреза прямо в треугольное алое ядро на груди!"
    ];
    private static void ReaderButton(Font f,string title,Rectangle r,bool active)
    {
        bool hover = Raylib.CheckCollisionPointRec(IsSettingsOpen ? UiViewport.Pointer : Raylib.GetMousePosition(), r);
        if (active || hover) Raylib.DrawRectangleRec(r, DossierPrint.Selected);
        Raylib.DrawRectangleLinesEx(r, 1, active ? DossierPrint.Ivory : DossierPrint.Stone);
        MainMenuAndPauseUI.Center(f, title, r, 15, active || hover ? DossierPrint.Ivory : DossierPrint.Quiet);
    }
    private static void Type(string text, float x, float y, int size, Color ink, Typeface face = Typeface.Interface)
    {
        var font = GameFonts.Get(face, size);
        GameFonts.Draw(font, text, new(x, y), size, ink);
    }

    private static void Fill(float x, float y, float w, float h, Color ink) =>
        Raylib.DrawRectangleRec(new(x, y, w, h), ink);

    private static readonly Dictionary<(string, int, int, Typeface), string[]> ParagraphLines = new();
    internal static string[] Wrap(string text, int width, int size, Typeface face = Typeface.Interface)
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
        return lines;
    }

    private static float Paragraph(string text, int x, float y, int width, int size, int leading,
                                   Color ink, Typeface face = Typeface.Interface)
    {
        foreach (var line in Wrap(text, width, size, face))
        {
            DossierPrint.Text(line, x, y, size, ink, face);
            y += leading;
        }
        return y;
    }

    public static void DrawSettingsWindow(Font font)
    {
        Dim(.8f);
        Rectangle panel = MixerRect(0, 0, 820, 584);
        Raylib.DrawRectangleRec(panel, new Color(22, 20, 19, 245));
        Raylib.DrawRectangleLinesEx(panel, 1, DossierPrint.Stone);
        Type("Звуковая рубка", panel.X + 28, panel.Y + 15, 36, DossierPrint.Ivory, Typeface.SerifBold);
        Type("Микшер каналов", panel.X + 29, panel.Y + 62, 16, DossierPrint.Quiet);
        Fill(panel.X + 28, panel.Y + 88, 764, 1, DossierPrint.Stone);
        ReaderButton(font, "Закрыть [F8]", MixerClose, false);
        void Slider(Rectangle r, float value)
        {
            Fill(r.X, r.Y + 9, r.Width, 4, new Color(83, 73, 65, 255));
            Fill(r.X, r.Y + 9, r.Width * value, 4, DossierPrint.Crimson);
            Fill(r.X + r.Width * value - 3, r.Y + 2, 6, 18, DossierPrint.Ivory);
        }
        for(int row=0;row<=MusicRow;row++)
        {
            Rectangle r=MixerRect(18,95+row*43,784,38);
            Fill(r.X + 10, r.Y + r.Height + 2, r.Width - 20, 1, DossierPrint.Stone);
            string name=row==0?"Общая громкость":row==MusicRow?"Музыка и босс":MixerNames[row-1];
            DrawTextCrisp(font,name,(int)r.X+12,(int)r.Y+8,18,row == SelectedSliderIndex ? Color.White : DossierPrint.Ivory);
            float value=MixerValue(row);
            Slider(MixerTrack(row),value);
            MainMenuAndPauseUI.Center(font,$"{value*100:0}%",MixerRect(561,99+row*43,51,30),15,Color.White);
            ReaderButton(font,value==0?"Без звука":"Звук вкл",MixerSwitch(row),value==0);
            if(row>0) ReaderButton(font,"Тест",MixerTest(row),false);
        }
        DrawTextCrisp(font,"УПРАВЛЕНИЕ",(int)MixerRect(28,410,0,0).X,(int)MixerRect(28,410,0,0).Y,16,DossierPrint.Quiet);
        for(int index=4;index<=5;index++)
        {
            int y=449+(index-4)*48;
            DrawTextCrisp(font,index==4?"Чувствительность мыши":"Угол обзора (FOV)",(int)MixerRect(30,y,0,0).X,(int)MixerRect(30,y,0,0).Y,18,Color.White);
            Slider(MixerRect(305,y,390,24),GetSliderNormalizedValue(index));
            MainMenuAndPauseUI.Center(font,index==4?MouseSensitivity.ToString("0.00"):CameraFov.ToString("0") + "°",MixerRect(705,y,87,24),18,Color.White);
        }
        MainMenuAndPauseUI.Center(font,_settingsStatusTimer>0?_settingsStatusBanner:"Перетаскивай ползунки. Стрелки меняют громкость. F8 закрывает окно.",MixerRect(24,550,772,22),14,DossierPrint.Quiet);
    }

    public static void DrawCaseWindow(Font font)
    {
        Dim(.56f);
        MainMenuAndPauseUI.Surface(Ref(720,373,1100,699),DossierPrint.Matte,1);
        ReaderButton(font,"Закрыть [K]",Ref(1580,390,220,55),false);
        Rectangle title=Ref(1094,373,485,59);
        Raylib.DrawRectangleRounded(title,.1f,6,DossierPrint.Selected);
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
            Raylib.DrawRectangleRec(track,DossierPrint.Matte);
            Raylib.BeginScissorMode((int)track.X,(int)track.Y,(int)track.Width,(int)track.Height);
            int first=(int)(_caseScrollPx/170);
            for(int i=first;i<Math.Min(first+6,_rouletteTape.Count);i++)
            {
                var skin=_rouletteTape[i];
                float x=866+i*170-_caseScrollPx;
                Rectangle card=Ref(x+8,727,151,188);
                Raylib.DrawRectangleRounded(card,.12f,6,DossierPrint.Selected);
                Raylib.DrawRectangleRoundedLinesEx(card,.12f,6,2,skin.CardColor);
                Program.DrawSlotIcon((int)skin.Slot,new(card.X+card.Width/2,card.Y+card.Height*.39f),true,skin.Secondary3D);
                string name=skin.Name.Split('«').Last().TrimEnd('»');
                MainMenuAndPauseUI.Center(font,skin.WeaponLabel.Replace("СЛОТ "+((int)skin.Slot+1)+": ",""),Ref(x+8,847,151,28),12,Color.White);
                MainMenuAndPauseUI.Center(font,name,Ref(x+9,873,149,26),11,Color.White);
                Raylib.DrawRectangleRec(Ref(x+11,900,145,9),skin.CardColor);
            }
            Raylib.EndScissorMode();
            Raylib.DrawRectangleRec(Ref(1289,684,4,237),DossierPrint.Crimson);
        }
        Rectangle buy=Ref(1105,990,376,62);
        Raylib.DrawRectangleRounded(buy,.19f,6,DossierPrint.Selected);
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
