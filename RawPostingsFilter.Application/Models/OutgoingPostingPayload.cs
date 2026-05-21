using System.ComponentModel;

namespace RawPostingsFilter.Application.Models;

[Description("Нормализованное содержимое вакансии или заказа, готовое для бизнес-логики и последующего сравнения с профессиональным профилем пользователя.")]
public sealed record OutgoingPostingPayload
{
    [Description("Имя, ник, название компании или другой идентификатор автора объявления, если его можно извлечь из текста.")]
    public string? Author { get; init; }

    [Description("Короткий заголовок вакансии или заказа. Должен отражать суть работы, а не источник объявления.")]
    public string? Title { get; init; }

    [Description("Нижняя граница оплаты за работу. Указывается только числом без валюты, если ее можно определить.")]
    public decimal? PriceMin { get; init; }

    [Description("Верхняя граница оплаты за работу. Указывается только числом без валюты, если ее можно определить.")]
    public decimal? PriceMax { get; init; }

    [Description("Валюта оплаты, указанной в price_min и price_max.")]
    public required Currency Currency { get; init; }

    [Description("Полное очищенное описание вакансии или заказа с важными требованиями, условиями, задачами и контекстом.")]
    public string? Description { get; init; }

    [Description("Файлы, приложенные к объявлению: ссылки на файлы или содержимое файлов в base64, если ссылка недоступна.")]
    public IReadOnlyList<PostingAttachment>? AttachedFiles { get; init; }

    [Description("Основной профессиональный кластер вакансии, например IT, Design, Marketing, Management или другой крупный класс работ.")]
    public string? Cluster { get; init; }

    [Description("Профессиональные специализации, к которым относится вакансия. Это направления работы, а не отдельные навыки.")]
    public IReadOnlyList<JobPostingSpecialization> Specializations { get; init; } = [];

    [Description("Навыки, которые явно требуются для выполнения работы и должны хорошо совпадать с навыками пользователя.")]
    public IReadOnlyList<JobPostingSkill> RequiredSkills { get; init; } = [];

    [Description("Навыки, которые будут преимуществом, но не являются обязательными для выполнения работы.")]
    public IReadOnlyList<JobPostingSkill> BonusSkills { get; init; } = [];

    [Description("Инструменты, технологии, платформы, библиотеки, сервисы или программы, которые явно требуются в объявлении.")]
    public IReadOnlyList<JobPostingTool> RequiredTools { get; init; } = [];

    [Description("Инструменты, технологии, платформы, библиотеки, сервисы или программы, знание которых будет преимуществом.")]
    public IReadOnlyList<JobPostingTool> BonusTools { get; init; } = [];

    [Description("Доменные области бизнеса или продукта, к которым относится вакансия, например E-commerce, FinTech, Healthcare или Game Development.")]
    public IReadOnlyList<JobPostingDomain> Domains { get; init; } = [];
}
