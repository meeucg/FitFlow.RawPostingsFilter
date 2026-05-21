using System.ComponentModel;

namespace RawPostingsFilter.Application.Models;

[Description("Файл, приложенный к вакансии или заказу.")]
public sealed record PostingAttachment
{
    [Description("Прямая ссылка на файл, если она доступна.")]
    public string? Url { get; init; }

    [Description("Содержимое файла в base64, если прямой ссылки нет и файл был скачан.")]
    public string? Base64 { get; init; }

    [Description("Расширение файла без точки или с точкой, если именно так оно было получено из источника.")]
    public required string Extension { get; init; }
}
