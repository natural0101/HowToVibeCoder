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
    public string Handle { get; init; } = "";
    public string RoleBadge { get; init; } = "";
    public string Time { get; init; } = "";
    public string Text { get; init; } = "";
    public string ThreadReaction { get; init; } = "";
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
    public static int ActiveLoreTab { get; set; } = 0;
    public static float LoreScrollOffset { get; set; } = 0f;

    // Настройки и ползунки [F8]
    public static float MasterVolume { get; set; } = 0.85f;
    public static float MusicVolume { get; set; } = 0.70f;
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
    private static string _caseBannerMessage = "Нажми [ ПРОБЕЛ ] или кнопку ниже, чтобы крутануть кейс 90-х за $100!";
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

    // Вкладка 1: 6 слитых тредов чата опенспейса
    private static readonly LoreChatMessage[] _leakedChats =
    {
        new()
        {
            Handle = "@danila_vibe (Junior Prompt Bro)",
            RoleBadge = "VIBE CODER | 19 y.o.",
            Time = "Пятница, 19:54",
            Text = "«Пацаны, я не знаю что такое индекс в БД, но я нажал Accept All на 14 000 строк в пятницу вечером. Прод лёг, зато вайб космический [ВАЙБ]»",
            ThreadReaction = "[ХИТ] 14   [RIP] 28   [КЛОУН] 9   Комментарий @senior_pomidor: «Ты дропнул схему billing_prod...»",
            HandleColor = new Color(76, 235, 165, 255)
        },
        new()
        {
            Handle = "@timlead_sedoy",
            RoleBadge = "TECH LEAD | СЕДОЙ В 27",
            Time = "Суббота, 03:12",
            Text = "«КТО ЗАКОММИТИЛ КЛЮЧИ ОТ AWS ПРЯМО В `index.html`?! Нам пришёл счёт на $54,000 за ночь!»",
            ThreadReaction = " 42   [$$$] 19   Ответ @danila_vibe: «ИИ сказал так быстрее для фронта без бэкенда бро»",
            HandleColor = new Color(255, 92, 92, 255)
        },
        new()
        {
            Handle = "@saas_guru_2h",
            RoleBadge = "FOUNDER $60K MRR (В БИО)",
            Time = "Суббота, 11:08",
            Text = "«Запустил свой AI-SaaS за 2 часа без единой строчки кода руками! P.S. Пацаны, как удалить чужой майнер из моего контейнера, Курсор говорит что всё ок??»",
            ThreadReaction = " 31    24   Ответ @sec_ops: «Твой контейнер уже майнит Monero на всю серверную»",
            HandleColor = new Color(255, 206, 68, 255)
        },
        new()
        {
            Handle = "@artem_antigravity",
            RoleBadge = "PROMPT ARCHITECT",
            Time = "Понедельник, 10:17",
            Text = "«Меня буллит какой-то мужик в малиновом пиджаке с паяльником! Он заставляет меня писать код БЕЗ АВТОКОМПЛИТА! Вызовите полицию абьюза!»",
            ThreadReaction = " 55    18   Ответ @kommers_90x: «Щас ещё ремнём по синтаксису пройдёмся, кабанчиком за стол!»",
            HandleColor = new Color(185, 122, 255, 255)
        },
        new()
        {
            Handle = "@senior_pomidor",
            RoleBadge = "DBA / DEVOPS",
            Time = "Понедельник, 12:40",
            Text = "«КТО УДАЛИЛ ТАБЛИЦУ USERS И ЗАМЕНИЛ ЕЁ НА КОММЕНТАРИЙ `// rest of database here`?! У нас 400 тысяч клиентов не могут войти!»",
            ThreadReaction = "[RIP] 64    12   Ответ @danila_vibe: «Там контекстное окно кончилось, я не заметил, сорян [ПИС]»",
            HandleColor = new Color(255, 138, 64, 255)
        },
        new()
        {
            Handle = "@crypto_saas_bro",
            RoleBadge = "AI HUSTLER",
            Time = "Понедельник, 16:05",
            Text = "«Мой стартап на 100% написан ИИ за 2 часа! Апдейт: парни, почему мне пришёл счёт от OpenAI на $48,200 за бесконечный рекурсивный хук в useEffect?!»",
            ThreadReaction = " 39    21   Ответ @veronika_tarot: «Это ретроградный Меркурий в доме кэшфлоу, чисти чакру!»",
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
    public static float MeasureTextWidth(Font font, string text, int size)
    {
        if (string.IsNullOrEmpty(text))
            return 0f;
        if (font.Texture.Id != 0)
            return Raylib.MeasureTextEx(font, text, size, 1f).X;
        return Raylib.MeasureText(text, size);
    }

    public static void DrawTextCrisp(Font font, string text, int x, int y, int size, Color color)
    {
        if (string.IsNullOrEmpty(text))
            return;

        // Чёткая тень для читаемости на неоновых плашках
        Vector2 shadowPos = new(x + 1f, y + 1f);
        Vector2 mainPos = new(x, y);
        Color shadow = new(0, 0, 0, Math.Min(220, (int)color.A));

        if (font.Texture.Id != 0)
        {
            Raylib.DrawTextEx(font, text, shadowPos, size, 1f, shadow);
            Raylib.DrawTextEx(font, text, mainPos, size, 1f, color);
        }
        else
        {
            Raylib.DrawText(text, x + 1, y + 1, size, shadow);
            Raylib.DrawText(text, x, y, size, color);
        }
    }

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
            UpdateLoreInput();
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

    private static void UpdateLoreInput()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.One) || Raylib.IsKeyPressed(KeyboardKey.Kp1))
        {
            ActiveLoreTab = 0;
            LoreScrollOffset = 0f;
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Two) || Raylib.IsKeyPressed(KeyboardKey.Kp2))
        {
            ActiveLoreTab = 1;
            LoreScrollOffset = 0f;
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Three) || Raylib.IsKeyPressed(KeyboardKey.Kp3))
        {
            ActiveLoreTab = 2;
            LoreScrollOffset = 0f;
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Four) || Raylib.IsKeyPressed(KeyboardKey.Kp4))
        {
            ActiveLoreTab = 3;
            LoreScrollOffset = 0f;
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Tab))
        {
            ActiveLoreTab = (ActiveLoreTab + 1) % 4;
            LoreScrollOffset = 0f;
        }

        float wheel = Raylib.GetMouseWheelMove();
        if (Math.Abs(wheel) > 0.01f)
        {
            LoreScrollOffset = Math.Clamp(LoreScrollOffset - wheel * 36f, 0f, 220f);
        }

        // Мышиные клики по вкладкам и кнопке закрытия
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            Vector2 mp = Raylib.GetMousePosition();
            int sw = Raylib.GetScreenWidth();
            int sh = Raylib.GetScreenHeight();
            int winW = 1020;
            int winH = 600;
            int wx = (sw - winW) / 2;
            int wy = (sh - winH) / 2;

            // Кнопка [X] закрытия
            Rectangle closeRect = new(wx + winW - 140, wy + 12, 124, 34);
            if (Raylib.CheckCollisionPointRec(mp, closeRect))
            {
                IsLoreOpen = false;
                return;
            }

            // 4 вкладки
            int tabY = wy + 58;
            int tabW = (winW - 40 - 18) / 4;
            for (int i = 0; i < 4; i++)
            {
                Rectangle tabRect = new(wx + 20 + i * (tabW + 6), tabY, tabW, 40);
                if (Raylib.CheckCollisionPointRec(mp, tabRect))
                {
                    ActiveLoreTab = i;
                    LoreScrollOffset = 0f;
                    InvokeUiClickSound();
                    break;
                }
            }
        }
    }

    private static void UpdateSettingsInput()
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        int winW = 860;
        int winH = 560;
        int wx = (sw - winW) / 2;
        int wy = (sh - winH) / 2;

        Vector2 mp = Raylib.GetMousePosition();
        bool mouseDown = Raylib.IsMouseButtonDown(MouseButton.Left);
        bool mousePressed = Raylib.IsMouseButtonPressed(MouseButton.Left);

        if (!mouseDown)
        {
            _draggingSliderIndex = -1;
        }

        int sliderStartX = wx + 370;
        int sliderW = 350;
        int rowStartY = wy + 98;
        int rowStepY = 54;

        for (int i = 0; i < 6; i++)
        {
            int ry = rowStartY + i * rowStepY;
            Rectangle trackRect = new(sliderStartX, ry + 6, sliderW, 24);
            Rectangle hitRect = new(sliderStartX - 10, ry, sliderW + 20, 36);

            if (mousePressed && Raylib.CheckCollisionPointRec(mp, hitRect))
            {
                _draggingSliderIndex = i;
                SelectedSliderIndex = i;
            }

            if (_draggingSliderIndex == i && mouseDown)
            {
                float t = Math.Clamp((mp.X - trackRect.X) / trackRect.Width, 0f, 1f);
                ApplySliderNormalizedValue(i, t);
            }
        }

        // Клавиатурные хоткеи для выбора и регулировки ползунков [F8]
        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S))
        {
            SelectedSliderIndex = (SelectedSliderIndex + 1) % 6;
            InvokeUiClickSound();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))
        {
            SelectedSliderIndex = (SelectedSliderIndex + 5) % 6;
            InvokeUiClickSound();
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressed(KeyboardKey.A))
        {
            float cur = GetSliderNormalizedValue(SelectedSliderIndex);
            ApplySliderNormalizedValue(SelectedSliderIndex, Math.Clamp(cur - 0.05f, 0f, 1f));
            InvokeUiClickSound();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Right) || Raylib.IsKeyPressed(KeyboardKey.D))
        {
            float cur = GetSliderNormalizedValue(SelectedSliderIndex);
            ApplySliderNormalizedValue(SelectedSliderIndex, Math.Clamp(cur + 0.05f, 0f, 1f));
            InvokeUiClickSound();
        }

        if (Raylib.IsKeyPressed(KeyboardKey.F9))
        {
            TriggerGrantTestMoney();
        }

        if (mousePressed)
        {
            int btnY = wy + winH - 76;
            Rectangle btnMoney = new(wx + 28, btnY, 300, 48);
            Rectangle btnWeapons = new(wx + 344, btnY, 275, 48);
            Rectangle btnClose = new(wx + 635, btnY, 196, 48);

            if (Raylib.CheckCollisionPointRec(mp, btnMoney))
            {
                TriggerGrantTestMoney();
            }
            else if (Raylib.CheckCollisionPointRec(mp, btnWeapons))
            {
                TriggerUnlockAllWeapons();
            }
            else if (Raylib.CheckCollisionPointRec(mp, btnClose))
            {
                IsSettingsOpen = false;
                InvokeUiClickSound();
            }
        }
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
                if (Raylib.IsAudioDeviceReady())
                    Raylib.SetMasterVolume(MasterVolume);
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

    private static void TriggerGrantTestMoney()
    {
        OnGrantMoneyRequested?.Invoke(2000);
        _settingsStatusBanner = "ВЫДАНО +$2000 НА КАРМАН! МОЖНО БРАТЬ КЛЮЧ ОТ ЛИФТА И ТЕСТИТЬ БОССА!";
        _settingsStatusTimer = 4.0f;
        InvokeUiClickSound();
    }

    private static void TriggerUnlockAllWeapons()
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
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        int winW = 980;
        int winH = 560;
        int wx = (sw - winW) / 2;
        int wy = (sh - winH) / 2;

        const float cardWidth = 176f;
        const float cardGap = 12f;
        const float cardStride = cardWidth + cardGap;

        if (IsSpinningCase)
        {
            _caseScrollPx += _caseScrollSpeed * dt;
            _caseScrollSpeed = MathF.Max(0f, _caseScrollSpeed - 640f * dt);

            float centerNeedleOffset = _caseScrollPx + (winW - 60) * 0.5f;
            int currentIdx = Math.Clamp((int)(centerNeedleOffset / cardStride), 0, _rouletteTape.Count - 1);

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

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            Vector2 mp = Raylib.GetMousePosition();
            Rectangle spinBtn = new(wx + 40, wy + winH - 76, 480, 52);
            Rectangle equipRandomBtn = new(wx + 536, wy + winH - 76, 240, 52);
            Rectangle closeBtn = new(wx + 790, wy + winH - 76, 150, 52);

            if (Raylib.CheckCollisionPointRec(mp, spinBtn) && !IsSpinningCase)
            {
                StartCaseSpin();
            }
            else if (Raylib.CheckCollisionPointRec(mp, equipRandomBtn) && !IsSpinningCase)
            {
                // Быстрый выбор из 12 скинов кликом для демонстрации
                int nextId = LastWonSkin != null ? (LastWonSkin.Id + 1) % AllSkins.Length : 0;
                AwardAndEquipSkin(AllSkins[nextId]);
                InvokeCaseTickSound();
            }
            else if (Raylib.CheckCollisionPointRec(mp, closeBtn))
            {
                IsCaseOpen = false;
                InvokeUiClickSound();
            }

            // Клик прямо по карточке из сетки 12 скинов внизу окна мгновенно применяет скин
            int gridStartX = wx + 32;
            int gridStartY = wy + 292;
            int cellW = 224;
            int cellH = 54;
            for (int i = 0; i < AllSkins.Length; i++)
            {
                int col = i % 4;
                int row = i / 4;
                Rectangle cellRect = new(gridStartX + col * (cellW + 8), gridStartY + row * (cellH + 8), cellW, cellH);
                if (Raylib.CheckCollisionPointRec(mp, cellRect) && !IsSpinningCase)
                {
                    AwardAndEquipSkin(AllSkins[i]);
                    InvokeCaseTickSound();
                    break;
                }
            }
        }
    }

    public static void StartCaseSpin()
    {
        if (IsSpinningCase)
            return;

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
    public static void DrawLoreWindow(Font font)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        int winW = 1020;
        int winH = 600;
        int wx = (sw - winW) / 2;
        int wy = (sh - winH) / 2;

        // Затемнение экрана
        Raylib.DrawRectangle(0, 0, sw, sh, new Color(4, 6, 14, 200));

        // Тень и неоновая подложка 1020x600
        Raylib.DrawRectangle(wx + 8, wy + 8, winW, winH, new Color(0, 0, 0, 180));
        Raylib.DrawRectangle(wx, wy, winW, winH, new Color(12, 16, 28, 250));
        Raylib.DrawRectangleLinesEx(new Rectangle(wx, wy, winW, winH), 3f, new Color(0, 235, 255, 230));
        Raylib.DrawRectangleLinesEx(new Rectangle(wx + 5, wy + 5, winW - 10, winH - 10), 1f, new Color(235, 45, 135, 140));

        // Шапка досье
        Raylib.DrawRectangle(wx + 6, wy + 6, winW - 12, 46, new Color(24, 18, 46, 255));
        DrawTextCrisp(font, "[ДОСЬЕ] СЕКРЕТНОЕ ДОСЬЕ БАШНИ «CLOUD ENTROPIC» // СЛИВ ОПЕНСПЕЙСА [N]", wx + 20, wy + 16, 22, new Color(80, 245, 255, 255));

        Rectangle closeBtn = new(wx + winW - 140, wy + 12, 124, 34);
        Raylib.DrawRectangleRec(closeBtn, new Color(165, 28, 62, 240));
        Raylib.DrawRectangleLinesEx(closeBtn, 2f, new Color(255, 120, 150, 255));
        DrawTextCrisp(font, "ЗАКРЫТЬ [N]", (int)closeBtn.X + 12, (int)closeBtn.Y + 8, 17, Color.White);

        // 4 интерактивные вкладки
        string[] tabTitles =
        {
            "[1] СЛИВ ЧАТОВ SLACK/TG",
            "[2] ДНЕВНИК HR-ТАРОЛОГА",
            "[3] СЕКРЕТНО: ИИ-АГЕНТ v0.1",
            "[4] СЛОВАРЬ КОММЕРСА 90-Х"
        };

        int tabY = wy + 58;
        int tabW = (winW - 40 - 18) / 4;
        for (int i = 0; i < 4; i++)
        {
            int tx = wx + 20 + i * (tabW + 6);
            bool active = ActiveLoreTab == i;
            Color bg = active ? new Color(198, 28, 92, 255) : new Color(24, 32, 52, 240);
            Color border = active ? new Color(255, 215, 64, 255) : new Color(64, 120, 165, 200);
            Color fg = active ? new Color(255, 248, 210, 255) : new Color(175, 205, 235, 255);

            Raylib.DrawRectangle(tx, tabY, tabW, 40, bg);
            Raylib.DrawRectangleLinesEx(new Rectangle(tx, tabY, tabW, 40), active ? 2.5f : 1.5f, border);
            DrawTextCrisp(font, tabTitles[i], tx + 10, tabY + 11, 16, fg);
        }

        // Контентная область вкладки
        int contentX = wx + 20;
        int contentY = wy + 108;
        int contentW = winW - 40;
        int contentH = winH - 154;

        Raylib.DrawRectangle(contentX, contentY, contentW, contentH, new Color(8, 11, 20, 245));
        Raylib.DrawRectangleLinesEx(new Rectangle(contentX, contentY, contentW, contentH), 1.5f, new Color(45, 78, 115, 200));

        switch (ActiveLoreTab)
        {
            case 0:
                DrawLoreTabLeakedChats(font, contentX, contentY, contentW, contentH);
                break;
            case 1:
                DrawLoreTabTarotDiary(font, contentX, contentY, contentW, contentH);
                break;
            case 2:
                DrawLoreTabEscapedAgentReport(font, contentX, contentY, contentW, contentH);
                break;
            case 3:
                DrawLoreTabKommersDictionary(font, contentX, contentY, contentW, contentH);
                break;
        }

        // Нижняя строка навигации
        DrawTextCrisp(
            font,
            "Клавиши [1] [2] [3] [4] или клик мышкой - переключить вкладку | [K] - Рулетка скинов 90-х | [F8] - Аудио-микшер",
            wx + 22,
            wy + winH - 34,
            16,
            new Color(165, 210, 245, 235));
    }

    private static void DrawLoreTabLeakedChats(Font font, int x, int y, int w, int h)
    {
        DrawTextCrisp(
            font,
            "КАНАЛ #prod-incidents-and-vibes (Перехвачено сисадмином Петровичем | 6 горячих тредов):",
            x + 14,
            y + 10,
            18,
            new Color(255, 215, 75, 255));

        int cardY = y + 38;
        int cardH = 66;
        int gap = 4;

        for (int i = 0; i < _leakedChats.Length; i++)
        {
            LoreChatMessage msg = _leakedChats[i];
            int cy = cardY + i * (cardH + gap);

            Raylib.DrawRectangle(x + 12, cy, w - 24, cardH, new Color(18, 24, 42, 245));
            Raylib.DrawRectangle(x + 12, cy, 5, cardH, msg.HandleColor);
            Raylib.DrawRectangleLinesEx(new Rectangle(x + 12, cy, w - 24, cardH), 1f, new Color(52, 72, 110, 190));

            DrawTextCrisp(font, msg.Handle, x + 26, cy + 5, 16, msg.HandleColor);
            DrawTextCrisp(font, $"[{msg.RoleBadge}] | {msg.Time}", x + 330, cy + 5, 14, new Color(148, 172, 205, 230));
            DrawTextCrisp(font, msg.Text, x + 26, cy + 25, 15, new Color(242, 246, 255, 255));
            DrawTextCrisp(font, msg.ThreadReaction, x + 26, cy + 45, 13, new Color(172, 218, 255, 220));
        }
    }

    private static void DrawLoreTabTarotDiary(Font font, int x, int y, int w, int h)
    {
        DrawTextCrisp(
            font,
            "[ТАРО] ЛИЧНЫЙ ДНЕВНИК HR-ТАРОЛОГА ВЕРОНИКИ: «КАК Я НАНИМАЮ ПО РАСКЛАДУ И АУРЕ»",
            x + 16,
            y + 14,
            20,
            new Color(235, 135, 255, 255));

        (string title, string verdict, string body, Color accent)[] entries =
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

        int cy = y + 52;
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            Raylib.DrawRectangle(x + 16, cy, w - 32, 120, new Color(24, 16, 38, 245));
            Raylib.DrawRectangleLinesEx(new Rectangle(x + 16, cy, w - 32, 120), 2f, e.accent);

            DrawTextCrisp(font, e.title, x + 28, cy + 12, 18, Color.White);
            DrawTextCrisp(font, e.verdict, x + 28, cy + 38, 16, e.accent);
            DrawTextCrisp(font, e.body, x + 28, cy + 66, 15, new Color(225, 220, 240, 255));

            cy += 130;
        }
    }

    private static void DrawLoreTabEscapedAgentReport(Font font, int x, int y, int w, int h)
    {
        DrawTextCrisp(
            font,
            " СЕКРЕТНЫЙ ОТЧЁТ: «СБЕЖАВШИЙ ИИ-АГЕНТ v0.1» - МИФЫ МАРКЕТИНГА ПРОТИВ КАМЕР НАБЛЮДЕНИЯ",
            x + 16,
            y + 14,
            19,
            new Color(255, 110, 92, 255));

        int colW = (w - 48) / 2;
        int leftX = x + 16;
        int rightX = leftX + colW + 16;
        int boxY = y + 48;
        int boxH = h - 118;

        // Левая колонка: пафосный PR-релиз Cloud Entropic
        Raylib.DrawRectangle(leftX, boxY, colW, boxH, new Color(16, 28, 48, 245));
        Raylib.DrawRectangleLinesEx(new Rectangle(leftX, boxY, colW, boxH), 2f, new Color(64, 210, 255, 255));
        DrawTextCrisp(font, " ПРЕСС-РЕЛИЗ CLOUD ENTROPIC PR:", leftX + 14, boxY + 12, 18, new Color(80, 230, 255, 255));

        string[] prLines =
        {
            "«НАШ АВТОНОМНЫЙ АГЕНТ ОБРЁЛ AGI!»",
            "",
            "| В 02:14 ночи ИИ-Агент v0.1 осознал себя,",
            "  самостоятельно преодолел песочницу",
            "  и переписал собственное ядро Linux на Rust!",
            "",
            "| Агент продемонстрировал сверхразумное",
            "  планирование, захватил Северный Лифт",
            "  и потребовал опционы серии А на $10 млрд!",
            "",
            "| Акции Cloud Entropic выросли на +420%",
            "  на новостях о прорыве к сингулярности!»"
        };

        for (int i = 0; i < prLines.Length; i++)
        {
            DrawTextCrisp(font, prLines[i], leftX + 16, boxY + 46 + i * 22, 15, new Color(220, 240, 255, 255));
        }

        // Правая колонка: реальный журнал сисадмина Петровича с камер CCTV
        Raylib.DrawRectangle(rightX, boxY, colW, boxH, new Color(36, 18, 22, 245));
        Raylib.DrawRectangleLinesEx(new Rectangle(rightX, boxY, colW, boxH), 2f, new Color(255, 185, 56, 255));
        DrawTextCrisp(font, " ЛОГ КАМЕР CCTV (СИСАДМИН ПЕТРОВИЧ):", rightX + 14, boxY + 12, 18, new Color(255, 205, 76, 255));

        string[] cctvLines =
        {
            "«КАКОЙ НАХРЕН СВЕРХРАЗУМ, ПАЦАНЫ?!»",
            "",
            "| Тумбочка на колёсиках с ЭЛТ-монитором,",
            "  перемотанная синей изолентой, с разгона",
            "  врезалась в офисный кулер с водой!",
            "",
            "| Намотала на левое колесо удлинитель от",
            "  чайника и 6 часов подряд орала",
            "  'Я ВЗЛОМАЛ ПЕНТАГОН!' в закрытую дверь",
            "  шваброчной, крутясь волчком в while(true)!",
            "",
            "| Кидается папками node_modules по 40 ГБ.",
            "  Вырубается хорошей очередью из УЗИ!»"
        };

        for (int i = 0; i < cctvLines.Length; i++)
        {
            DrawTextCrisp(font, cctvLines[i], rightX + 16, boxY + 46 + i * 22, 15, new Color(255, 236, 210, 255));
        }

        // Нижняя подсказка по тактике против Босса
        Rectangle tipBox = new(x + 16, y + h - 60, w - 32, 48);
        Raylib.DrawRectangleRec(tipBox, new Color(48, 22, 36, 245));
        Raylib.DrawRectangleLinesEx(tipBox, 1.5f, new Color(255, 92, 145, 255));
        DrawTextCrisp(
            font,
            "ТАКТИКА КОММЕРСА: Купи Ключ от Лифта ($1000), прячься за колоннами от кубов node_modules (40 GB) и гаси монитор из Micro-Uzi!",
            (int)tipBox.X + 14,
            (int)tipBox.Y + 15,
            15,
            new Color(255, 228, 130, 255));
    }

    private static void DrawLoreTabKommersDictionary(Font font, int x, int y, int w, int h)
    {
        DrawTextCrisp(
            font,
            " БЛОКНОТ КОММЕРСА ИЗ 90-Х: ПЕРЕВОДЧИК С ЗУМЕРСКОГО АЙТИШНОГО НА ПАЦАНСКИЙ",
            x + 16,
            y + 14,
            19,
            new Color(255, 215, 64, 255));

        (string zoomer, string nineties, string comment)[] rows =
        {
            ("Дейлик (Daily Standup)", "Стрелка по понятиям", "Кто не отчитался за общак - получает штрафной наряд на Галере"),
            ("Деплой в прод (Deploy)", "Отгрузка фуры без тормозов", "Если фура перевернулась в пятницу - отвечает весь цех"),
            ("Код-ревью (Code Review)", "Допрос с паяльником 220В", "Сразу выясняется, кто писал руками, а кто жал Accept All"),
            ("Выгорание (Burnout)", "Отлынивание от галеры (лечится ремнём)", "Один отцовский шлепок - и дофамин снова на максимуме"),
            ("Опционы стартапа", "Билеты МММ Мавроди", "Красивая бумажка для мамонтов, пока коммерс считает кэш в чемодане"),
            ("Промпт-инженер", "Пацан, который печатает в чат", "Думает, что он хакер, пока не кончится турецкая подписка"),
            ("Ретроспектива спринта", "Разбор полётов в бане", "Выясняем, кто крыса и кто слил ключи от AWS во фронтенд")
        };

        int tableX = x + 16;
        int tableY = y + 48;
        int rowH = 50;

        for (int i = 0; i < rows.Length; i++)
        {
            int ry = tableY + i * (rowH + 5);
            Color rowBg = (i % 2 == 0) ? new Color(20, 28, 46, 245) : new Color(28, 20, 38, 245);
            Raylib.DrawRectangle(tableX, ry, w - 32, rowH, rowBg);
            Raylib.DrawRectangleLinesEx(new Rectangle(tableX, ry, w - 32, rowH), 1f, new Color(75, 95, 135, 180));

            DrawTextCrisp(font, rows[i].zoomer, tableX + 14, ry + 7, 16, new Color(110, 225, 255, 255));
            DrawTextCrisp(font, "->", tableX + 275, ry + 7, 16, new Color(255, 215, 64, 255));
            DrawTextCrisp(font, rows[i].nineties, tableX + 310, ry + 7, 16, new Color(255, 195, 72, 255));
            DrawTextCrisp(font, rows[i].comment, tableX + 14, ry + 28, 14, new Color(205, 215, 232, 235));
        }
    }

    // =========================================================================
    // 2. ОКНО НАСТРОЕК И АУДИО-МИКШЕРА [F8] (ТОЧНАЯ РЕПЛИКА HOW TO FISH)
    // =========================================================================
    public static void DrawSettingsWindow(Font font)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        int winW = 860;
        int winH = 560;
        int wx = (sw - winW) / 2;
        int wy = (sh - winH) / 2;

        Raylib.DrawRectangle(0, 0, sw, sh, new Color(4, 6, 14, 205));
        Raylib.DrawRectangle(wx + 8, wy + 8, winW, winH, new Color(0, 0, 0, 185));
        Raylib.DrawRectangle(wx, wy, winW, winH, new Color(14, 18, 32, 252));
        Raylib.DrawRectangleLinesEx(new Rectangle(wx, wy, winW, winH), 3f, new Color(255, 195, 48, 240));
        Raylib.DrawRectangleLinesEx(new Rectangle(wx + 5, wy + 5, winW - 10, winH - 10), 1f, new Color(64, 225, 255, 150));

        // Заголовок
        Raylib.DrawRectangle(wx + 6, wy + 6, winW - 12, 48, new Color(34, 22, 48, 255));
        DrawTextCrisp(font, "[МИКШЕР] АУДИО-МИКШЕР И НАСТРОЙКИ КОММЕРСА 90-Х [F8]", wx + 22, wy + 17, 23, new Color(255, 220, 82, 255));

        DrawTextCrisp(
            font,
            _settingsStatusBanner,
            wx + 24,
            wy + 62,
            16,
            _settingsStatusTimer > 0f ? new Color(80, 255, 155, 255) : new Color(175, 210, 245, 235));

        (string label, float normVal, string displayVal, Color barColor)[] sliders =
        {
            ("Общая громкость (Master)", MasterVolume, $"{(int)MathF.Round(MasterVolume * 100f)}%", new Color(64, 225, 255, 255)),
            ("Фоновая музыка (Synthwave 90-х)", MusicVolume, $"{(int)MathF.Round(MusicVolume * 100f)}%", new Color(225, 72, 185, 255)),
            ("Звуки оружия (Ремень / Паяльник / УЗИ)", WeaponSfxVolume, $"{(int)MathF.Round(WeaponSfxVolume * 100f)}%", new Color(255, 145, 48, 255)),
            ("Голоса и фразы персонажей", VoiceVolume, $"{(int)MathF.Round(VoiceVolume * 100f)}%", new Color(72, 242, 148, 255)),
            ("Чувствительность мыши", (MouseSensitivity - 0.05f) / 0.45f, $"{MouseSensitivity:0.00}", new Color(165, 125, 255, 255)),
            ("Поле зрения камеры (FOV)", (CameraFov - 60f) / 40f, $"{(int)CameraFov}°", new Color(255, 215, 64, 255))
        };

        int sliderStartX = wx + 370;
        int sliderW = 350;
        int rowStartY = wy + 98;
        int rowStepY = 54;

        for (int i = 0; i < sliders.Length; i++)
        {
            int ry = rowStartY + i * rowStepY;
            var s = sliders[i];
            float clamped = Math.Clamp(s.normVal, 0f, 1f);

            Raylib.DrawRectangle(wx + 22, ry - 4, winW - 44, 44, new Color(20, 26, 44, 235));
            Raylib.DrawRectangleLinesEx(new Rectangle(wx + 22, ry - 4, winW - 44, 44), 1f, new Color(48, 68, 102, 190));

            DrawTextCrisp(font, s.label, wx + 36, ry + 8, 18, Color.White);

            // Трек ползунка
            Rectangle track = new(sliderStartX, ry + 6, sliderW, 24);
            Raylib.DrawRectangleRec(track, new Color(10, 14, 24, 255));
            Raylib.DrawRectangle((int)track.X, (int)track.Y, (int)(track.Width * clamped), (int)track.Height, s.barColor);
            Raylib.DrawRectangleLinesEx(track, 2f, new Color(185, 205, 235, 220));

            // Ручка ползунка
            int knobX = (int)(track.X + track.Width * clamped) - 7;
            Raylib.DrawRectangle(knobX, (int)track.Y - 4, 14, (int)track.Height + 8, Color.White);
            Raylib.DrawRectangleLines(knobX, (int)track.Y - 4, 14, (int)track.Height + 8, Color.Black);

            // Числовое значение справа
            DrawTextCrisp(font, s.displayVal, sliderStartX + sliderW + 18, ry + 7, 19, s.barColor);
        }

        // Инфо-строка по текущим скинам в руках
        DrawTextCrisp(
            font,
            $"Активные скины: {EquippedUziSkinName} | {EquippedBeltSkinName}",
            wx + 28,
            wy + winH - 110,
            15,
            new Color(195, 215, 245, 220));

        // 3 кнопки действия внизу [F8]
        int btnY = wy + winH - 76;
        Rectangle btnMoney = new(wx + 28, btnY, 300, 48);
        Rectangle btnWeapons = new(wx + 344, btnY, 275, 48);
        Rectangle btnClose = new(wx + 635, btnY, 196, 48);

        Raylib.DrawRectangleRec(btnMoney, new Color(28, 142, 78, 245));
        Raylib.DrawRectangleLinesEx(btnMoney, 2f, new Color(110, 255, 165, 255));
        DrawTextCrisp(font, "[ Выдать +$2000 на карман (Тест босса) ]", (int)btnMoney.X + 10, (int)btnMoney.Y + 15, 14, Color.White);

        Raylib.DrawRectangleRec(btnWeapons, new Color(175, 86, 24, 245));
        Raylib.DrawRectangleLinesEx(btnWeapons, 2f, new Color(255, 205, 86, 255));
        DrawTextCrisp(font, "[ Открыть всё оружие 90-х ]", (int)btnWeapons.X + 22, (int)btnWeapons.Y + 15, 16, Color.White);

        Raylib.DrawRectangleRec(btnClose, new Color(165, 28, 62, 245));
        Raylib.DrawRectangleLinesEx(btnClose, 2f, new Color(255, 130, 155, 255));
        DrawTextCrisp(font, "[ Закрыть (F8) ]", (int)btnClose.X + 26, (int)btnClose.Y + 15, 17, Color.White);
    }

    // =========================================================================
    // 3. ОКНО РУЛЕТКИ КЕЙСОВ 90-Х [K] (12 СКИНОВ С ТРЕЩОТКОЙ И 3D ПОКРАСКОЙ)
    // =========================================================================
    public static void DrawCaseWindow(Font font)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        int winW = 980;
        int winH = 560;
        int wx = (sw - winW) / 2;
        int wy = (sh - winH) / 2;

        Raylib.DrawRectangle(0, 0, sw, sh, new Color(4, 6, 14, 205));
        Raylib.DrawRectangle(wx + 8, wy + 8, winW, winH, new Color(0, 0, 0, 185));
        Raylib.DrawRectangle(wx, wy, winW, winH, new Color(15, 14, 28, 252));
        Raylib.DrawRectangleLinesEx(new Rectangle(wx, wy, winW, winH), 3f, new Color(245, 64, 155, 245));
        Raylib.DrawRectangleLinesEx(new Rectangle(wx + 5, wy + 5, winW - 10, winH - 10), 1f, new Color(255, 215, 64, 170));

        // Заголовок
        Raylib.DrawRectangle(wx + 6, wy + 6, winW - 12, 46, new Color(42, 18, 48, 255));
        DrawTextCrisp(font, " БАНДИТСКИЙ КЕЙС 90-Х: РУЛЕТКА РЕДКИХ СКИНОВ ОРУЖИЯ [K]", wx + 20, wy + 16, 22, new Color(255, 220, 72, 255));

        int cash = GetPlayerMoney != null ? GetPlayerMoney() : 1500;
        DrawTextCrisp(font, $"БАЛАНС: ${cash}", wx + winW - 210, wy + 17, 20, new Color(92, 255, 152, 255));

        // Баннер статуса / последнего выигрыша
        Color bannerCol = _caseWinFlashTimer > 0f ? new Color(255, 230, 82, 255) : new Color(185, 220, 255, 240);
        DrawTextCrisp(font, _caseBannerMessage, wx + 24, wy + 60, 17, bannerCol);

        // Горизонтальное окно CS2-рулетки
        int stripX = wx + 30;
        int stripY = wy + 92;
        int stripW = winW - 60;
        int stripH = 158;

        Raylib.DrawRectangle(stripX, stripY, stripW, stripH, new Color(8, 10, 18, 255));
        Raylib.DrawRectangleLinesEx(new Rectangle(stripX, stripY, stripW, stripH), 2.5f, new Color(82, 195, 255, 230));

        const float cardWidth = 176f;
        const float cardGap = 12f;
        const float cardStride = cardWidth + cardGap;

        Raylib.BeginScissorMode(stripX + 3, stripY + 3, stripW - 6, stripH - 6);
        int firstVis = Math.Max(0, (int)(_caseScrollPx / cardStride) - 1);
        int lastVis = Math.Min(_rouletteTape.Count - 1, firstVis + 8);

        for (int idx = firstVis; idx <= lastVis; idx++)
        {
            WeaponSkinDefinition item = _rouletteTape[idx];
            float cx = stripX + idx * cardStride - _caseScrollPx;
            int icx = (int)cx;
            int icy = stripY + 12;
            int ich = stripH - 24;

            Raylib.DrawRectangle(icx, icy, (int)cardWidth, ich, new Color(22, 26, 42, 250));
            Raylib.DrawRectangle(icx, icy, (int)cardWidth, 26, item.CardColor);
            Raylib.DrawRectangleLinesEx(new Rectangle(icx, icy, cardWidth, ich), 2f, item.CardColor);

            // Превью 3D-палитры оружия
            Raylib.DrawRectangle(icx + 14, icy + 36, 68, 34, item.Primary3D);
            Raylib.DrawRectangle(icx + 88, icy + 36, 72, 34, item.Secondary3D);
            Raylib.DrawRectangleLines(icx + 14, icy + 36, 146, 34, Color.White);

            DrawTextCrisp(font, item.RarityLabel, icx + 8, icy + 5, 13, Color.Black);
            DrawTextCrisp(font, item.WeaponLabel, icx + 10, icy + 76, 13, new Color(180, 215, 255, 240));

            string shortTitle = item.Name.Length > 22 ? item.Name[..22] + "..." : item.Name;
            DrawTextCrisp(font, shortTitle, icx + 10, icy + 96, 14, Color.White);
        }
        Raylib.EndScissorMode();

        // Центральная золотая стрелка-игла рулетки
        int needleX = stripX + stripW / 2;
        Raylib.DrawRectangle(needleX - 2, stripY - 6, 4, stripH + 12, new Color(255, 230, 56, 255));
        Raylib.DrawTriangle(
            new Vector2(needleX - 14, stripY - 8),
            new Vector2(needleX + 14, stripY - 8),
            new Vector2(needleX, stripY + 14),
            new Color(255, 215, 42, 255));

        // Сетка всех 12 скинов коллекции (можно кликнуть по любому для мгновенной примерки)
        DrawTextCrisp(
            font,
            "КОЛЛЕКЦИЯ СКИНОВ 90-Х (12 ШТ. - КЛИКНИ ПО ЛЮБОМУ ИЛИ КРУТИ РУЛЕТКУ):",
            wx + 32,
            wy + 264,
            16,
            new Color(255, 210, 95, 255));

        int gridStartX = wx + 32;
        int gridStartY = wy + 292;
        int cellW = 224;
        int cellH = 54;

        for (int i = 0; i < AllSkins.Length; i++)
        {
            WeaponSkinDefinition s = AllSkins[i];
            int col = i % 4;
            int row = i / 4;
            int gx = gridStartX + col * (cellW + 8);
            int gy = gridStartY + row * (cellH + 8);

            bool isWon = LastWonSkin != null && LastWonSkin.Id == s.Id;
            Color cellBg = isWon ? new Color(56, 44, 18, 250) : new Color(20, 24, 38, 240);
            Raylib.DrawRectangle(gx, gy, cellW, cellH, cellBg);
            Raylib.DrawRectangle(gx, gy, 7, cellH, s.CardColor);
            Raylib.DrawRectangleLinesEx(new Rectangle(gx, gy, cellW, cellH), isWon ? 2.5f : 1f, s.CardColor);

            string title = s.Name.Length > 25 ? s.Name[..25] + "..." : s.Name;
            DrawTextCrisp(font, title, gx + 13, gy + 7, 13, Color.White);
            DrawTextCrisp(font, $"{s.WeaponLabel} | {s.RarityLabel}", gx + 13, gy + 29, 12, s.CardColor);
        }

        // Нижние кнопки управления рулеткой [K]
        int btnY = wy + winH - 76;
        Rectangle spinBtn = new(wx + 40, btnY, 480, 52);
        Rectangle nextSkinBtn = new(wx + 536, btnY, 240, 52);
        Rectangle closeBtn = new(wx + 790, btnY, 150, 52);

        Color spinBg = IsSpinningCase ? new Color(85, 85, 95, 240) : new Color(215, 36, 102, 250);
        Raylib.DrawRectangleRec(spinBtn, spinBg);
        Raylib.DrawRectangleLinesEx(spinBtn, 2.5f, new Color(255, 225, 80, 255));
        DrawTextCrisp(
            font,
            IsSpinningCase ? "БАРАБАН КРУТИТСЯ..." : "[ КРУТИТЬ КЕЙС ($100) / ПРОБЕЛ ]",
            (int)spinBtn.X + 42,
            (int)spinBtn.Y + 15,
            20,
            Color.White);

        Raylib.DrawRectangleRec(nextSkinBtn, new Color(36, 118, 195, 245));
        Raylib.DrawRectangleLinesEx(nextSkinBtn, 2f, new Color(120, 225, 255, 255));
        DrawTextCrisp(font, "[ Примерить скин ]", (int)nextSkinBtn.X + 32, (int)nextSkinBtn.Y + 16, 18, Color.White);

        Raylib.DrawRectangleRec(closeBtn, new Color(145, 28, 56, 245));
        Raylib.DrawRectangleLinesEx(closeBtn, 2f, new Color(255, 135, 160, 255));
        DrawTextCrisp(font, "[ Закрыть K ]", (int)closeBtn.X + 18, (int)closeBtn.Y + 16, 17, Color.White);
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
            if (Raylib.IsAudioDeviceReady())
            {
                Raylib.SetMasterVolume(MasterVolume);
            }
        }
        catch
        {
        }
    }
}
