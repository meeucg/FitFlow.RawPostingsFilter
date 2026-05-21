using System.ComponentModel;

namespace RawPostingsFilter.Application.Models;

[Description("Промежуточная AI-нормализация вакансии или заказа. Сначала определи, является ли объявление рекламой или спамом. Если IsSpamOrAd = true, не заполняй остальные поля: верни null для nullable-полей и пустые массивы для списков.")]
public sealed record AINormalizedJobPosting
{
    [Description("Верни true, если объявление является рекламой, спамом, шумом или не является настоящей вакансией/заказом. Не помечай как спам настоящую вакансию только из-за маркетинговых слов вроде автоворонок, вебинаров, запусков, GetCourse, SaleBot, Bizon или реферальных программ, если это предмет работы. Если значение true, все остальные поля должны быть пустыми: null для nullable-полей и пустые массивы для списков.")]
    public required bool IsSpamOrAd { get; init; }

    [Description("Имя, ник, название компании или другой идентификатор автора объявления. Если автор не указан, верни null.")]
    public required string? Author { get; init; }

    [Description("Короткий заголовок вакансии или заказа. Если заголовок нельзя уверенно определить, верни null.")]
    public required string? Title { get; init; }

    [Description("Нижняя граница оплаты за работу числом без валюты. Если оплата не указана, верни null.")]
    public required decimal? PriceMin { get; init; }

    [Description("Верхняя граница оплаты за работу числом без валюты. Если указан один бюджет, можно продублировать его в price_min и price_max.")]
    public required decimal? PriceMax { get; init; }

    [Description("Валюта оплаты. Если оплата не указана или валюта неизвестна, верни null.")]
    public required Currency? Currency { get; init; }

    [Description("Очищенное описание вакансии или заказа с задачами, требованиями, условиями и важным контекстом. Если текста недостаточно, верни null.")]
    public required string? Description { get; init; }

    [Description("Файлы или ссылки на файлы, найденные в тексте объявления. Если ссылка есть только в описании, добавь ее сюда. Если файлов нет, верни пустой массив.")]
    public required List<PostingAttachment> AttachedFiles { get; init; }

    [Description("Основной профессиональный кластер вакансии, например IT, Design, Marketing, Management или другой крупный класс работ. Если неясно, верни null.")]
    public required string? Cluster { get; init; }

    [Description("Профессиональные специализации, к которым относится вакансия. Если специализации неясны, верни пустой массив.")]
    public required List<JobPostingSpecialization> Specializations { get; init; }

    [Description("Навыки, которые явно требуются для выполнения работы. Если обязательных навыков нет, верни пустой массив.")]
    public required List<JobPostingSkill> RequiredSkills { get; init; }

    [Description("Навыки, которые указаны как преимущество или желательные. Если таких навыков нет, верни пустой массив.")]
    public required List<JobPostingSkill> BonusSkills { get; init; }

    [Description("Инструменты, технологии, платформы, библиотеки, сервисы или программы, которые явно требуются. Если обязательных инструментов нет, верни пустой массив.")]
    public required List<JobPostingTool> RequiredTools { get; init; }

    [Description("Инструменты, технологии, платформы, библиотеки, сервисы или программы, знание которых будет преимуществом. Если таких инструментов нет, верни пустой массив.")]
    public required List<JobPostingTool> BonusTools { get; init; }

    [Description("Доменные области бизнеса или продукта, к которым относится вакансия. Если домены не указаны и не выводятся из контекста, верни пустой массив.")]
    public required List<JobPostingDomain> Domains { get; init; }
}
