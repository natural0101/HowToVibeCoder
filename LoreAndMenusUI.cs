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
            LoreScrollOffset = Math.Clamp(LoreScrollOffset - wheel * 36f, 0f, float.MaxValue);
        }

        if (Click(LoreClose)) IsLoreOpen = false;
        for (int i=0;i<4;i++)
            if (Click(LoreTab(i))) { ActiveLoreTab=i; LoreScrollOffset=0; }
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
    private static Rectangle MixerTrack(int row) => row == 0 ? Ref(1204,530,336,46) : Ref(1257,635+(row-1)*78,181,42);
    private static Rectangle MixerSwitch(int row) => row == 0 ? Ref(1576,533,67,39) : Ref(1455,636+(row-1)*78,69,40);
    private static Rectangle MixerTest(int row) => Ref(1536,630+(row-1)*78,124,51);
    private static float MixerValue(int row) => row==0 ? MasterVolume : row == MusicRow ? MusicVolume : AudioMusicSystem.EffectLevels[row-1];
    private static void SetMixer(int row,float value)
    {
        value=Math.Clamp(value,0,1);
        if(row==0) ApplySliderNormalizedValue(0,value);
        else if (row == MusicRow) MusicVolume = value;
        else AudioMusicSystem.EffectLevels[row-1]=value;
    }
    private static void UpdateSettingsInput()
    {
        if (Click(Ref(1643,401,70,67))) { IsSettingsOpen=false; return; }
        if (!Raylib.IsMouseButtonDown(MouseButton.Left)) _draggingSliderIndex=-1;
        for (int row=0;row<=MusicRow;row++)
        {
            Rectangle track=MixerTrack(row);
            if (Click(track)) _draggingSliderIndex=SelectedSliderIndex=row;
            if (_draggingSliderIndex==row) SetMixer(row,(Raylib.GetMouseX()-track.X)/track.Width);
            if (Click(MixerSwitch(row)))
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
        if(Raylib.IsKeyPressed(KeyboardKey.Down)) SelectedSliderIndex=(SelectedSliderIndex+1)%(MusicRow+1);
        if(Raylib.IsKeyPressed(KeyboardKey.Up)) SelectedSliderIndex=(SelectedSliderIndex+MusicRow)%(MusicRow+1);
        if(Raylib.IsKeyPressed(KeyboardKey.Left)) SetMixer(SelectedSliderIndex,MixerValue(SelectedSliderIndex)-.05f);
        if(Raylib.IsKeyPressed(KeyboardKey.Right)) SetMixer(SelectedSliderIndex,MixerValue(SelectedSliderIndex)+.05f);
        if(Raylib.IsKeyPressed(KeyboardKey.F9)) TriggerGrantTestMoney();

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

        if (Click(Ref(1744,367,84,65))) IsCaseOpen=false;
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

    private static readonly string[] prLines =
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
    private static readonly string[] cctvLines =
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

    private static readonly string[] ArchiveTabs=["Переписка","Дневник HR","ИИ-агент","Словарь"];
    private static Rectangle Archive(float x,float y,float w,float h) =>
        new(Raylib.GetScreenWidth()/2f - 330+x,Raylib.GetScreenHeight()/2f - 220+y,w,h);
    private static Rectangle LoreClose => Archive(556,13,90,26);
    private static Rectangle LoreTab(int i) => Archive(13+i*159,51,153,27);
    private static void ReaderButton(Font f,string title,Rectangle r,bool active)
    {
        Raylib.DrawRectangleRounded(r,.25f,6,active?new Color(222,138,14,255):new Color(24,38,58,255));
        Raylib.DrawRectangleRoundedLinesEx(r,.25f,6,1,active?new Color(254,192,74,255):new Color(52,75,106,255));
        MainMenuAndPauseUI.Center(f,title,r,14,active?Color.Black:new Color(194,207,225,255));
    }
    private static string Wrap(Font f,string value,int size,float width)
    {
        var lines=new List<string>();
        string line="";
        foreach(string word in value.Split(' ',StringSplitOptions.RemoveEmptyEntries))
        {
            string next=line.Length==0 ? word : line+" "+word;
            if(line.Length>0 && GameFonts.Measure(f,next,size).X>width) { lines.Add(line); line=word; }
            else line=next;
        }
        lines.Add(line);
        return string.Join("\n",lines);
    }
    public static void DrawLoreWindow(Font font)
    {
        Dim(.48f);
        MainMenuAndPauseUI.Sprite("archive",Archive(0,0,660,440));
        Raylib.DrawRectangleRec(Archive(8,7,644,425),new Color(13,21,34,255));
        DrawTextCrisp(font,"АРХИВ БАШНИ", (int)Archive(18,12,0,0).X,(int)Archive(18,12,0,0).Y,22,new Color(245,163,37,255));
        DrawTextCrisp(font,"Cloud Entropic Tower", (int)Archive(19,35,0,0).X,(int)Archive(19,35,0,0).Y,11,new Color(151,171,196,255));
        ReaderButton(font,"Закрыть [N]",LoreClose,false);
        for(int i=0;i<4;i++) ReaderButton(font,ArchiveTabs[i],LoreTab(i),i==ActiveLoreTab);
        var bounds=Archive(13,88,632,338);
        Raylib.BeginScissorMode((int)bounds.X,(int)bounds.Y,(int)bounds.Width,(int)bounds.Height);
        float y=bounds.Y-LoreScrollOffset;
        void Entry(string title,string meta,string body)
        {
            string wrapped=Wrap(font,body,14,593);
            float height=53+wrapped.Count(c=>c=='\n')*17+17;
            Rectangle card=new(bounds.X,y,622,height);
            Raylib.DrawRectangleRounded(card,.12f,6,new Color(19,31,48,255));
            Raylib.DrawRectangleRoundedLinesEx(card,.12f,6,1,new Color(43,61,88,255));
            Vector2 p=new(card.X+10,card.Y+7);
            GameFonts.Draw(GameFonts.Get(Typeface.InterfaceBold,16),title,p,16,new Color(246,189,76,255));
            GameFonts.Draw(font,meta,p+new Vector2(0,20),14,new Color(157,179,204,255));
            foreach(var line in wrapped.Split('\n'))
            {
                GameFonts.Draw(font,line,new(p.X,y+42),14,new Color(214,223,236,255));
                y+=17;
            }
            y=card.Y+height+6;
        }
        switch(ActiveLoreTab)
        {
            case 0: foreach(var m in _leakedChats) Entry(m.Handle,m.RoleBadge+"   "+m.Time,m.Text+"   "+m.ThreadReaction); break;
            case 1: foreach(var e in TarotEntries) Entry(e.title,e.verdict,e.body); break;
            case 2:
                Entry("Пресс-релиз","Cloud Entropic PR",string.Join(" ",prLines).Replace("|",""));
                Entry("Камеры наблюдения","Журнал Петровича",string.Join(" ",cctvLines).Replace("|",""));
                Entry("Тактика","Северный лифт","Купи ключ за $1000. Прячься за колоннами от кубов node_modules."); break;
            case 3: foreach(var e in DictionaryEntries) Entry(e.zoomer,e.nineties,e.comment); break;
        }
        Raylib.EndScissorMode();
        float content=y-bounds.Y+LoreScrollOffset;
        float limit=Math.Max(0,content-bounds.Height);
        LoreScrollOffset=Math.Clamp(LoreScrollOffset,0,limit);
        if(limit>0)
        {
            float thumb=bounds.Height*bounds.Height/content;
            Raylib.DrawRectangleRounded(new(bounds.X+626,bounds.Y+(bounds.Height-thumb)*LoreScrollOffset/limit,5,thumb),1,6,new Color(117,132,151,255));
        }
    }

    public static void DrawSettingsWindow(Font font)
    {
        Dim(.55f);
        MainMenuAndPauseUI.Sprite("mixer",Ref(823,381,916,629),new(0,0,916,629));
        // Extend the existing console with its own wooden test strip and lower frame.
        MainMenuAndPauseUI.Sprite("mixer",Ref(823,1010,916,78),new(0,235,916,78));
        MainMenuAndPauseUI.Sprite("mixer",Ref(908,1010,335,78),new(174,235,239,78));
        MainMenuAndPauseUI.Sprite("mixer",Ref(823,1088,916,45),new(0,629,916,45));
        void Engraved(string label,Rectangle r,Font face,int pixels,Color surface)
        {
            Raylib.DrawRectangleRounded(r,.15f,8,surface);
            MainMenuAndPauseUI.Center(face,label,r,pixels,new Color(248,238,213,255));
        }
        Font titles=GameFonts.Get(Typeface.SerifBold,24);
        Font controls=GameFonts.Get(Typeface.InterfaceBold,16);
        Engraved("Звуковая рубка",Ref(1041,401,431,66),titles,24,new Color(39,48,50,255));
        Engraved("Общая громкость",Ref(920,523,256,56),controls,16,new Color(40,49,51,255));
        for(int row=0;row<=MusicRow;row++)
        {
            Rectangle track=MixerTrack(row);
            int cells=row==0 ? 13 : 7;
            float value=MixerValue(row);
            Raylib.DrawRectangleRounded(track,.1f,4,new Color(44,31,21,255));
            for(int i=0;i<cells;i++)
            {
                Rectangle cell=new(track.X+(i+.08f)*track.Width/cells,track.Y+2,track.Width/cells*.83f,track.Height-4);
                bool lit=i+.5f<=value*cells;
                int column=lit ? (i<2 ? 0 : i<3 ? 2 : 3) : 5;
                MainMenuAndPauseUI.Sprite("mixer",cell,new(1259-823+column*25.5f,640-381,18,34));
            }
            Rectangle toggle=MixerSwitch(row);
            MainMenuAndPauseUI.Sprite("mixer",toggle,new(1455-823,636-381,value>0 ? -69 : 69,40));
            if(row>0)
            {
                Rectangle label=row==MusicRow ? Ref(910,1016,334,63) : Ref(1003,628+(row-1)*78,244,55);
                Engraved(row==MusicRow ? "Фоновая музыка и босс" : MixerNames[row-1],label,font,16,new Color(113,75,45,255));
                var button=MixerTest(row);
                Engraved("Тест",new(button.X+3,button.Y+3,button.Width-6,button.Height-6),controls,14,new Color(89,60,42,255));
            }
        }
        if(_settingsStatusTimer>0) MainMenuAndPauseUI.Center(font,_settingsStatusBanner,Ref(730,1140,1100,40),14,Color.White);
    }

    public static void DrawCaseWindow(Font font)
    {
        Dim(.56f);
        MainMenuAndPauseUI.Sprite("case",Ref(720,373,1100,699));
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
            MainMenuAndPauseUI.Sprite("case",Ref(1272,684,40,69),new(552,311,40,69));
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
