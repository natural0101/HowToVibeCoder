using Raylib_cs;
using static HowToVibeCoder.CoderArchetype;
using static HowToVibeCoder.VibeCoderRarity;

namespace HowToVibeCoder;

public enum VibeCoderRarity { Common, Rare, Epic, Legendary }

internal sealed record VibeCoderSpecies(string Id, string Name, VibeCoderRarity Rarity,
    CoderArchetype Archetype, Color HoodieColor, Color GlassesColor, int BonusPayout, string Hint, string Bio,
    int SalePrice=0, float HopDistance=0, float HopHeight=0)
{
    internal bool IsFrog => SalePrice>0;
}

internal static class VibeCoderCollection
{
    internal static readonly VibeCoderSpecies[] All = [
        new("tab", "Джуниор Таб-Данила", Common, JuniorPrompter, new(46,134,173,255), new(117,235,192,255), 20, "Оставь открытым бесплатный тариф.", "Стер надпись Tab на трёх клавиатурах. Остальные клавиши ещё на гарантии."),
        new("stack", "Копипастер со Стека", Common, JuniorPrompter, new(104,137,113,255), new(254,174,93,255), 20, "Прислушайся к шагам после включения Макбука.", "До сих пор благодарит пользователя с ником ClosedAsDuplicate за помощь в дипломе."),
        new("div", "Вёрстка на Дивах", Common, JuniorPrompter, new(156,101,96,255), new(155,222,236,255), 20, "Дождись, пока кандидат сядет у приманки.", "Сверстал пропуск в офис. Для вертикального центрирования фотографии пришлось нанять стажёра."),
        new("prompt", "Адепт Промпта", Common, TeamLeadMeeter, new(66,113,126,255), new(227,177,143,255), 25, "Ищи среди участников дейлика.", "Пишет «представь, что ты кофемашина» на стикере над чайником."),
        new("deadline", "Свидетель Дедлайна", Common, TeamLeadMeeter, new(121,102,134,255), new(147,204,196,255), 25, "Выбей ноутбук, пока идёт созвон.", "Видел дедлайн в четверг. К пятнице показания изменил."),
        new("weekend", "Фуллстек за Выходные", Common, CryptoBro, new(148,106,70,255), new(133,196,218,255), 25, "Предложи аванс кандидату у ловушки.", "В субботу купил домен, в воскресенье вписал себя в совет директоров."),
        new("leverage", "Крипто-Бро на Плечах", Rare, CryptoBro, new(211,144,45,255), new(66,233,242,255), 65, "Подписка Pro повышает шанс встречи.", "Заложил офисное кресло под десятикратное плечо. Теперь торгует стоя."),
        new("call", "Тимлид-Созвонщик", Rare, TeamLeadMeeter, new(36,143,117,255), new(247,133,166,255), 65, "Проверь дорогую приманку у Макбука.", "Назначил короткий созвон, чтобы выяснить, кто назначает столько созвонов."),
        new("smoothie", "Смузи-Евангелист", Rare, JuniorPrompter, new(164,131,213,255), new(179,232,76,255), 60, "Следи за сиреневой толстовкой у ловушки.", "Убедил бухгалтерию, что блендер входит в комплект средств разработки."),
        new("refactor", "Берсерк Рефакторинга", Rare, SeniorArchitect, new(163,43,64,255), new(251,177,66,255), 80, "Сбей эго, затем предложи NDA.", "Переименовал одну переменную. Следом пришлось менять вывеску компании."),
        new("framework", "Маг 40 Фреймворков", Rare, CryptoBro, new(42,88,204,255), new(249,197,76,255), 70, "Ищи золотой отблеск у Макбука.", "Сороковой фреймворк склеивает предыдущие тридцать девять. Кнопка скоро заработает."),
        new("no-commits", "Сеньор Без Коммитов", Epic, SeniorArchitect, new(48,49,60,255), new(247,173,65,255), 140, "Дорогая подписка и высокий риск привлекают эпических кандидатов.", "За семь лет написал одну строку. Это была подпись под архитектурным решением."),
        new("skolkovo", "Нейро-Шаман Сколково", Epic, CryptoBro, new(81,48,142,255), new(247,74,209,255), 150, "Подними риск и включи платную приманку.", "Обучил модель предсказывать гранты. Первую заявку модель выписала себе."),
        new("frog_hopper","Баг-Попрыгун",Common,JuniorPrompter,new(97,255,74,255),Color.White,1,"Смотри под ноги у приёмной.","Прыгает на каждый новый тик сборки.",1,.85f,.42f),
        new("frog_syntax","Синтаксис-Жаб",Common,JuniorPrompter,new(41,239,255,255),Color.White,2,"Прицелься перед прыжком.","Проглотил закрывающую скобку.",2,1.45f,.58f),
        new("frog_crash","Краш-Лягух",Rare,JuniorPrompter,new(250,70,57,255),Color.White,4,"Попробуй широкий взмах сачком.","Роняет сборку при каждом приземлении.",4,2.15f,.78f),
        new("frog_hotfix","Золотой Хотфикс-Жаб",Epic,JuniorPrompter,new(255,213,60,255),Color.White,8,"Ищи золотой отблеск у стоек.","Выкатился в пятницу и чудом пережил выходные.",8,1.65f,.68f),
        new("meetup", "Босс Митапов в Баленсиаге", Epic, TeamLeadMeeter, new(221,213,191,255), new(212,40,72,255), 150, "Присмотрись к белому силуэту во время инцидента.", "Доклад не сохранился. Зато кроссовки попали во все отчёты конференции."),
        new("legacy", "Призрак Легаси 1998", Legendary, SeniorArchitect, new(69,111,75,255), new(233,196,96,255), 300, "Пятничный релиз повышает шанс появления.", "Последний, кто помнит пароль от сборочного сервера. Пароль набирает только на ощупь."),
        new("friday", "Коммитер в Прод (Пятница 19:59)", Legendary, CryptoBro, new(202,47,49,255), new(255,196,57,255), 320, "Включи пятничный деплой и следи за входом.", "В 19:58 пожелал всем хороших выходных. В 20:01 перестал читать рабочий чат.")
    ];

    internal static readonly VibeCoderSpecies[] CommonBait=All.Where(s=>!s.IsFrog && s.Rarity==Common).ToArray();
    internal static readonly Dictionary<string,VibeCoderSpecies> Frogs=All.Where(s=>s.IsFrog).ToDictionary(s=>s.Id);

    internal static string Tag(VibeCoderRarity r) => r switch { Rare=>"РЕДКИЙ", Epic=>"ЭПИЧЕСКИЙ", Legendary=>"ЛЕГЕНДАРНЫЙ", _=>"ОБЫЧНЫЙ" };
    internal static Color Ink(VibeCoderRarity r) => r switch { Rare=>new(249,198,103,255), Epic=>new(199,159,255,255), Legendary=>new(114,244,195,255), _=>new(211,221,220,255) };

    internal static VibeCoderSpecies Roll(Random rng, SubscriptionBaitTier bait, FloorSystems floor, CoderArchetype? role=null)
    {
        int tier=(int)bait;
        double legendary=.04+.012*tier+(floor.Friday>0 ? .045 : 0);
        double epic=.10+.018*tier+.055*floor.R/100;
        double rare=.22+.022*tier;
        double roll=rng.NextDouble();
        var rarity=roll<legendary ? Legendary : roll<legendary+epic ? Epic : roll<legendary+epic+rare ? Rare : Common;
        var pool=All.Where(s=>!s.IsFrog && (role==null || s.Archetype==role) && s.Rarity==rarity).ToArray();
        if(pool.Length==0) pool=All.Where(s=>!s.IsFrog && s.Archetype==role).ToArray();
        return pool[rng.Next(pool.Length)];
    }

    internal static void Dress(VibeCoderEntity c,VibeCoderSpecies s)
    {
        c.SpeciesId=s.Id; c.Rarity=s.Rarity; c.Name="???";
        c.HoodieColor=s.HoodieColor; c.GlassesColor=s.GlassesColor;
    }
}
