using System.ComponentModel;

namespace RawPostingsFilter.Application.Models;

[Description("Нормализованная исходящая вакансия или заказ без сырого текста, готовая для дальнейшей отправки в сервис рекомендаций.")]
public sealed record OutgoingJobPosting
{
    [Description("Источник объявления, например kwork, tg, fl или другой код источника.")]
    public required string Source { get; init; }

    [Description("Дата и время публикации объявления в источнике, если они известны.")]
    public required DateTimeOffset PostedAt { get; init; }

    [Description("Ссылка на оригинальное объявление или сообщение.")]
    public required string Url { get; init; }

    [Description("Нормализованное содержимое объявления. Должно быть заполнено после AI-нормализации.")]
    public OutgoingPostingPayload? Payload { get; init; }
}
