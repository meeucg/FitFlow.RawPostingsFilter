using System.Globalization;
using System.Text.Json;
using RawPostingsFilter.Application.Models;

namespace RawPostingsFilter.Infrastructure.Serialization;

public static class IncomingJobPostingJsonParser
{
    private static readonly string[] PostedAtFormats =
    [
        "yy:MM:dd:HH:mm:ss",
        "yyyy:MM:dd:HH:mm:ss"
    ];

    public static bool TryParse(
        string json,
        JsonSerializerOptions jsonSerializerOptions,
        out IncomingJobPosting posting,
        out string? error)
    {
        posting = null!;
        error = null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var source = GetString(root, "source");
            var url = GetString(root, "url");
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(url))
            {
                error = "Message must contain non-empty source and url fields.";
                return false;
            }

            if (!TryParsePostedAt(GetString(root, "posted_at"), out var postedAt))
            {
                error = "Message must contain posted_at in yy:MM:dd:HH:mm:ss or yyyy:MM:dd:HH:mm:ss format.";
                return false;
            }

            posting = new IncomingJobPosting
            {
                Source = source,
                PostedAt = postedAt,
                Url = url,
                Payload = ParsePayload(root, jsonSerializerOptions),
                RawText = GetString(root, "raw_text")
            };

            return true;
        }
        catch (JsonException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool TryParsePostedAt(string? value, out DateTimeOffset postedAt)
    {
        return DateTimeOffset.TryParseExact(
            value,
            PostedAtFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out postedAt);
    }

    private static IncomingPostingPayload? ParsePayload(
        JsonElement root,
        JsonSerializerOptions jsonSerializerOptions)
    {
        if (!root.TryGetProperty("payload", out var payloadElement)
            || payloadElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return new IncomingPostingPayload
        {
            Author = GetString(payloadElement, "author"),
            Title = GetString(payloadElement, "title"),
            PriceMin = GetDecimal(payloadElement, "price_min"),
            PriceMax = GetDecimal(payloadElement, "price_max"),
            Currency = GetCurrency(payloadElement),
            Description = GetString(payloadElement, "description"),
            AttachedFiles = GetAttachments(payloadElement, jsonSerializerOptions)
        };
    }

    private static Currency GetCurrency(JsonElement payloadElement)
    {
        var value = GetString(payloadElement, "currency");
        if (Enum.TryParse<Currency>(value, ignoreCase: true, out var currency))
        {
            return currency;
        }

        return Currency.Rub;
    }

    private static IReadOnlyList<PostingAttachment>? GetAttachments(
        JsonElement payloadElement,
        JsonSerializerOptions jsonSerializerOptions)
    {
        if (!payloadElement.TryGetProperty("attached_files", out var attachedFilesElement)
            || attachedFilesElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return JsonSerializer.Deserialize<IReadOnlyList<PostingAttachment>>(
            attachedFilesElement.GetRawText(),
            jsonSerializerOptions);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
               && property.ValueKind != JsonValueKind.Null
            ? property.GetString()
            : null;
    }

    private static decimal? GetDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.TryGetDecimal(out var value) ? value : null;
    }
}
