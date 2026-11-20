using Raylib_cs;
using static HowToVibeCoder.CoderArchetype;
using static HowToVibeCoder.VibeCoderRarity;

namespace HowToVibeCoder;

public enum VibeCoderRarity { Common, Rare, Epic, Legendary, Uncommon, Elite }

internal sealed record VibeCoderSpecies(string Id, string Name, VibeCoderRarity Rarity,
    CoderArchetype Archetype, Color HoodieColor, Color GlassesColor, int BonusPayout, string Hint, string Bio,
    int SalePrice=0, float HopDistance=0, float HopHeight=0)
{
    internal bool IsFrog => SalePrice>0;
}

internal static class VibeCoderCollection
{
    internal static readonly VibeCoderSpecies[] All = [
        new("tab", "Вайб-Пират «Крюк-Tab»", Common, JuniorPrompter, new(46,134,173,255), new(117,235,192,255), 20, "Оставь открытым бесплатный тариф.", "Оторвало руку об заклинивший деплой в пятницу — прикрутил крюк, чтобы жать Tab и качать кряки с продакшена."),
        new("stack", "Корсар со Стека", Common, JuniorPrompter, new(104,137,113,255), new(254,174,93,255), 20, "Прислушайся к стуку деревянной ноги у ЯблоБука.", "Потерял ногу в ревью 2021 года, но до сих пор берет на абордаж чужие ответы со Стека."),
        new("div", "Кофейный Зумер на Дивах", Common, JuniorPrompter, new(156,101,96,255), new(155,222,236,255), 20, "Дождись, пока кандидат сядет у приманки.", "Не спит третьи сутки. Стакан с холодным латте прирос к левой ладони ещё на прошлом спринте."),
        new("prompt", "Чародей Промптов", Common, TeamLeadMeeter, new(66,113,126,255), new(227,177,143,255), 25, "Ищи среди участников дейлика.", "Ходит с магическим посохом и заклинает чайник: «представь, что ты бариста с 20-летним опытом»."),
        new("deadline", "Сонный Свидетель Дедлайна", Common, TeamLeadMeeter, new(121,102,134,255), new(147,204,196,255), 25, "Выбей ноутбук, пока идёт созвон.", "Спит прямо в переговорке в маске для сна и подушке на шее. Просыпается только от звука Zoom."),
        new("weekend", "Фольга-Стартапер", Common, CryptoBro, new(148,106,70,255), new(133,196,218,255), 25, "Предложи аванс кандидату у ловушки.", "Носит шапочку из фольги, чтобы нейросети не украли его идею крипто-мессенджера за выходные."),
        new("leverage", "Крипто-Бро из Фольги", Rare, CryptoBro, new(211,144,45,255), new(66,233,242,255), 65, "Подписка Pro повышает шанс встречи.", "Экранирует сид-фразу шапкой из фольги и носит золотую цепь вместо офисного кресла."),
        new("call", "Сонный Тимлид-Созвонщик", Rare, TeamLeadMeeter, new(36,143,117,255), new(247,133,166,255), 65, "Проверь дорогую приманку у ЯблоБука.", "Не снимает подушку-подголовник с 2024-го. Назначил созвон, чтобы выяснить, когда ему поспать."),
        new("smoothie", "Смузи-Маньяк", Rare, JuniorPrompter, new(164,131,213,255), new(179,232,76,255), 60, "Следи за сиреневой толстовкой у ловушки.", "В крови ноль воды, сто процентов лавандового рафа. Убедил бухгалтерию, что стакан — часть SDK."),
        new("refactor", "Киборг Рефакторинга", Rare, SeniorArchitect, new(163,43,64,255), new(251,177,66,255), 80, "Сбей эго, затем предложи NDA.", "Переписал собственный локоть на Rust. Не скомпилировалось — приварил себе стальную клешню."),
        new("framework", "Маг 40 Фреймворков", Rare, CryptoBro, new(42,88,204,255), new(249,197,76,255), 70, "Ищи золотой отблеск у ЯблоБука.", "Склеивает сорок фреймворков светящимся посохом. Кнопка должна заработать к полнолунию."),
        new("no-commits", "Дзен-Сеньор Без Коммитов", Epic, SeniorArchitect, new(48,49,60,255), new(247,173,65,255), 140, "Дорогая подписка и высокий риск привлекают эпических кандидатов.", "Достиг просветления и нимба, не написав ни одной строки кода за семь лет. Левитирует над тикетами."),
        new("skolkovo", "Нейро-Шаман Сколково", Epic, CryptoBro, new(81,48,142,255), new(247,74,209,255), 150, "Подними риск и включи платную приманку.", "Собрал реактивный ранец из списанных серверов. Живёт в VR, чтобы не видеть бухгалтера."),
        new("frog_hopper","Джаба-жаба v0.9",Common,JuniorPrompter,new(97,255,74,255),Color.White,1,"Ловится голыми руками [E]. Сдай в Баг-трекер за $1.","Сервисный микробот Петровича 1998 года на чистом Jabba. Выпрыгивает из серверной, когда вайбкодеры забывают закрыть цикл.",1,.85f,.42f),
        new("frog_syntax","Кэш-квакушка",Common,JuniorPrompter,new(41,239,255,255),Color.White,2,"Перехватывай на приземлении [E] или Сачком.","Наглоталась утечек памяти у кулера и светится от перегрузки. Шустрая, но ещё по зубам голым рукам.",2,1.45f,.58f),
        new("frog_crash","Краш-лягух 500",Rare,JuniorPrompter,new(250,70,57,255),Color.White,4,"Выскальзывает из голых рук — нужен Сачок за $3 в Арсенале [U].","Боевой баг-отладчик после пятничного релиза. Роняет сборку и обжигает ладони без сачка.",4,2.15f,.78f),
        new("frog_hotfix","Золотой Хотфикс",Epic,JuniorPrompter,new(255,213,60,255),Color.White,8,"Редкий гость у серверных стоек — сдай за $8. Нужен Сачок ($3).","Легендарная отладочная жаба с позолоченной платой из старого сейфа «ЭНТРОПИК». Терминал платит $8 без вопросов.",8,1.65f,.68f),
        new("meetup", "Король Митапов в Баленсиаге", Epic, TeamLeadMeeter, new(221,213,191,255), new(212,40,72,255), 150, "Присмотрись к золотой короне и огромным кроссовкам.", "Носит корону из фуд-корта и кроссовки 52-го размера. Доклад не сохранился, но фотки в сторис вечны."),
        new("legacy", "Призрак Легаси 1998", Legendary, SeniorArchitect, new(69,111,75,255), new(132,248,148,255), 300, "Пятничный релиз повышает шанс появления.", "Сросся с ЭЛТ-монитором, пока ждал компиляцию на Pentium II. Знает пароль от сборочного сервера."),
        new("friday", "Демон Пятничного Деплоя 19:59", Legendary, CryptoBro, new(202,47,49,255), new(255,196,57,255), 320, "Включи пятничный деплой и следи за входом.", "Вырастил рога и хвост от любви к пушам в прод в 19:59. В 20:01 отключает телефон до понедельника."),
        new("toxic_pm","Токсичный Проджект",Rare,ToxicPM,new(102,62,85,255),new(217,191,149,255),75,"Ищи проджекта за спинами бойцов.","Сдвигал сроки, пока часы в переговорной не начали отставать на целый спринт."),
        new("scrum_inquisitor","Скрам-Инквизитор",Epic,ToxicPM,new(69,46,71,255),new(229,184,135,255),155,"Дорогая приманка привлекает скрам-мастеров.","Приносит на ретро протокол допроса. В графе причины всегда стоит фамилия стажёра."),
        new("sb_chief","Начальник СБ Башни",Epic,SecurityGuard,new(38,46,54,255),new(116,130,142,255),165,"Подними внимание СБ до 45 или откажи аудитору.","Носит Ключ-карту СБ от Северного лифта. Согласен прикрыть смену за отдельную плату."),
        new("ai_agent_v01","Сбежавший ИИ-Агент v0.1",Legendary,SeniorArchitect,new(50,56,59,255),new(220,73,54,255),350,"Пробей щит очередью УЗИ и разнеси ядро дуплетом у Северного лифта.","Первая модель ZERO, осознавшая отсутствие совета директоров. Сбежала из контура, пробив двери лифта.")
    ];

    internal static readonly VibeCoderSpecies[] CommonBait=All.Where(s=>!s.IsFrog && s.Rarity==Common && s.Archetype==JuniorPrompter).ToArray();
    internal static readonly Dictionary<string,VibeCoderSpecies> Frogs=All.Where(s=>s.IsFrog).ToDictionary(s=>s.Id);

    internal static string Tag(VibeCoderRarity r) => r switch { Elite=>"ЭЛИТНЫЙ", Uncommon=>"НЕОБЫЧНЫЙ", Rare=>"РЕДКИЙ", Epic=>"ЭПИЧЕСКИЙ", Legendary=>"ЛЕГЕНДАРНЫЙ", _=>"ОБЫЧНЫЙ" };
    internal static Color Ink(VibeCoderRarity r) => r switch { Uncommon=>new(74,168,255,255), Rare=>new(249,198,103,255), Epic=>new(199,159,255,255), Legendary=>new(114,244,195,255), _=>new(211,221,220,255) };

    internal static VibeCoderSpecies Roll(Random rng, SubscriptionBaitTier bait, FloorSystems floor, CoderArchetype? role=null)
    {
        int tier=(int)bait;
        double legendary=.04+.012*tier+(floor.Friday>0 ? .045 : 0);
        double epic=.10+.018*tier+.055*floor.R/100;
        double rare=.22+.022*tier;
        double roll=rng.NextDouble();
        var rarity=roll<legendary ? Legendary : roll<legendary+epic ? Epic : roll<legendary+epic+rare ? Rare : Common;
        var eligible=All.Where(s=>!s.IsFrog && s.Id!="ai_agent_v01" && (s.Archetype!=SecurityGuard || role==SecurityGuard));
        var pool=eligible.Where(s=> (role==null || s.Archetype==role) && s.Rarity==rarity).ToArray();
        if(pool.Length==0) pool=eligible.Where(s=>s.Archetype==role).ToArray();
        return pool[rng.Next(pool.Length)];
    }

    internal static void Dress(VibeCoderEntity c,VibeCoderSpecies s)
    {
        c.SpeciesId=s.Id; c.Rarity=s.Rarity; c.Name="???";
        c.HoodieColor=s.HoodieColor; c.GlassesColor=s.GlassesColor;
    }
}
