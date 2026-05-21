using AIServices.Abstractions;
using AIServices.Entities;
using AIServices.Models;
using AIServices.Models.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RawPostingsFilter.Application.Abstractions;
using RawPostingsFilter.Application.Models;
using RawPostingsFilter.Application.Normalization;

namespace RawPostingsFilter.Infrastructure.Ai;

public sealed class AiJobPostingNormalizer(
    ITextAI textAI,
    IOptions<TextAIOptions> textAIOptions,
    ILogger<AiJobPostingNormalizer> logger) : IJobPostingNormalizer
{
    private bool _missingApiKeyLogged;

    private const string SystemPrompt = """
        Ты нормализуешь вакансии и заказы для сервиса рекомендаций.

        Правила:
        - Извлекай только то, что явно указано или надежно следует из текста.
        - Не выдумывай автора, бюджет, навыки, инструменты, домены и требования.
        - Если значение неизвестно, возвращай null для nullable-полей и пустой массив для списков.
        - price_min и price_max возвращай только числами без валюты.
        - Если указан один бюджет, можно поставить одинаковое значение в price_min и price_max.
        - currency возвращай только если валюта понятна: rub, usd или eur.
        - required_skills и required_tools заполняй тем, что действительно требуется.
        - bonus_skills и bonus_tools заполняй тем, что указано как плюс или преимущество.
        - attached_files заполняй ссылками на файлы, документы, брифы, ТЗ, портфолио или другие приложенные материалы, найденные в тексте.
        - Не помещай контакты, Telegram-ники и обычные ссылки для отклика в attached_files.
        - Для attached_files не выдумывай base64. Если есть URL, оставь base64 равным null.
        - Если расширение файла из URL неясно, используй extension = "unknown".
        - is_spam_or_ad = true, если объявление является рекламой, спамом, шумом или не настоящей вакансией/заказом.
        - Считай spam/ad, если сам пост рекламирует или продает услуги, курсы, каналы, ботов, реферальные программы или содержит саморекламу.
        - Не считай spam/ad только из-за слов автоворонка, вебинар, запуск, GetCourse, SaleBot, Bizon, реферальная программа, продажи, маркетинг или онлайн-школа, если это предмет работы в вакансии или заказе.
        - Если в тексте есть понятная роль, задача, требования и способ отклика, считай это вакансией/заказом, пока нет сильных признаков рекламы, спама или шума.
        - Считай spam/ad, если текст полон пустых обещаний и обещает много денег без конкретной работы.
        - Считай spam/ad, если обещают взять всех без опыта, бесплатно обучить и через 1-2 недели гарантировать высокий доход.
        - Считай spam/ad, если объявление ищет только мужчин или только женщин без очевидной профессиональной необходимости.
        - Считай spam/ad, если это не вакансия и не заказ: шум из Telegram-паблика, человек рекламирует себя как специалист, новость, обсуждение, пост без найма или без задачи.
        - Сначала определи is_spam_or_ad, и только если is_spam_or_ad = false, нормализуй остальные поля.
        - Если is_spam_or_ad = true, не заполняй payload-содержимое: верни null для author, title, price_min, price_max, currency, description, cluster; верни пустые массивы для attached_files, specializations, required_skills, bonus_skills, required_tools, bonus_tools и domains.
        """;

    public async Task<JobPostingNormalizationResult> NormalizeAsync(
        IncomingJobPosting posting,
        CancellationToken cancellationToken)
    {
        var fallbackPosting = BuildOutgoingPosting(posting, aiPayload: null);

        if (!HasUsableApiKey(textAIOptions.Value.ApiKey))
        {
            const string error = "TextAI API key is not configured.";
            if (!_missingApiKeyLogged)
            {
                logger.LogWarning(error);
                _missingApiKeyLogged = true;
            }

            return new JobPostingNormalizationResult(
                fallbackPosting,
                AiSucceeded: false,
                IsSpamOrAd: false,
                ErrorMessage: error);
        }

        var sourceText = GetSourceTextForAi(posting);
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            const string error = "Posting has no text available for AI normalization.";
            logger.LogWarning(
                "Skipped AI normalization for posting from {Source} because no source text is available.",
                posting.Source);

            return new JobPostingNormalizationResult(
                fallbackPosting,
                AiSucceeded: false,
                IsSpamOrAd: false,
                ErrorMessage: error);
        }

        var request = new TextAIRequest
        {
            ChatContext = new Chat(
                chatInitialState: new ChatInitialState
                {
                    SystemPrompt = SystemPrompt
                }),
            RequestText = $"""
                Сначала классифицируй объявление как spam/ad или настоящую вакансию/заказ. Затем верни AINormalizedJobPosting по правилам.

                Входной текст:
                {sourceText}
                """
        };

        var response = await textAI.CompleteChatTyped<AINormalizedJobPosting>(
            request,
            cancellationToken);

        if (!response.IsSuccess || response.Response is null)
        {
            const string error = "TextAI returned no normalized posting.";
            logger.LogWarning(
                "AI normalization failed for posting from {Source}, Url={Url}",
                posting.Source,
                posting.Url);

            return new JobPostingNormalizationResult(
                fallbackPosting,
                AiSucceeded: false,
                IsSpamOrAd: false,
                ErrorMessage: error);
        }

        var normalizedPosting = BuildOutgoingPosting(posting, response.Response);
        return new JobPostingNormalizationResult(
            normalizedPosting,
            AiSucceeded: true,
            IsSpamOrAd: response.Response.IsSpamOrAd,
            ErrorMessage: null);
    }

    private static bool HasUsableApiKey(string? apiKey)
    {
        return !string.IsNullOrWhiteSpace(apiKey)
               && !apiKey.StartsWith("__", StringComparison.Ordinal)
               && !apiKey.EndsWith("__", StringComparison.Ordinal);
    }

    private static string? GetSourceTextForAi(IncomingJobPosting posting)
    {
        return posting.Payload is null
            ? posting.RawText
            : posting.Payload.Description;
    }

    private static OutgoingJobPosting BuildOutgoingPosting(
        IncomingJobPosting posting,
        AINormalizedJobPosting? aiPayload)
    {
        return new OutgoingJobPosting
        {
            Source = posting.Source,
            PostedAt = posting.PostedAt,
            Url = posting.Url,
            Payload = MergePayload(posting.Payload, aiPayload)
        };
    }

    private static OutgoingPostingPayload? MergePayload(
        IncomingPostingPayload? incomingPayload,
        AINormalizedJobPosting? aiPayload)
    {
        if (aiPayload?.IsSpamOrAd == true)
        {
            return null;
        }

        if (incomingPayload is null && !HasMeaningfulPayload(aiPayload))
        {
            return null;
        }

        return new OutgoingPostingPayload
        {
            Author = FirstText(incomingPayload?.Author, aiPayload?.Author),
            Title = FirstText(incomingPayload?.Title, aiPayload?.Title),
            PriceMin = incomingPayload?.PriceMin ?? aiPayload?.PriceMin,
            PriceMax = incomingPayload?.PriceMax ?? aiPayload?.PriceMax,
            Currency = incomingPayload is not null
                ? incomingPayload.Currency
                : aiPayload?.Currency ?? Currency.Rub,
            Description = FirstText(incomingPayload?.Description, aiPayload?.Description),
            AttachedFiles = MergeAttachedFiles(incomingPayload?.AttachedFiles, aiPayload?.AttachedFiles),
            Cluster = FirstText(null, aiPayload?.Cluster),
            Specializations = aiPayload?.Specializations ?? [],
            RequiredSkills = aiPayload?.RequiredSkills ?? [],
            BonusSkills = aiPayload?.BonusSkills ?? [],
            RequiredTools = aiPayload?.RequiredTools ?? [],
            BonusTools = aiPayload?.BonusTools ?? [],
            Domains = aiPayload?.Domains ?? []
        };
    }

    private static bool HasMeaningfulPayload(AINormalizedJobPosting? payload)
    {
        return payload is not null
               && !payload.IsSpamOrAd
               && (!string.IsNullOrWhiteSpace(payload.Author)
                   || !string.IsNullOrWhiteSpace(payload.Title)
                   || payload.PriceMin.HasValue
                   || payload.PriceMax.HasValue
                   || !string.IsNullOrWhiteSpace(payload.Description)
                   || !string.IsNullOrWhiteSpace(payload.Cluster)
                   || payload.AttachedFiles is { Count: > 0 }
                   || payload.Specializations is { Count: > 0 }
                   || payload.RequiredSkills is { Count: > 0 }
                   || payload.BonusSkills is { Count: > 0 }
                   || payload.RequiredTools is { Count: > 0 }
                   || payload.BonusTools is { Count: > 0 }
                   || payload.Domains is { Count: > 0 });
    }

    private static string? FirstText(string? primary, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary;
        }

        return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
    }

    private static IReadOnlyList<PostingAttachment>? MergeAttachedFiles(
        IReadOnlyList<PostingAttachment>? incomingAttachments,
        IReadOnlyList<PostingAttachment>? aiAttachments)
    {
        var merged = incomingAttachments?
            .Where(IsUsableAttachment)
            .ToList() ?? [];

        foreach (var aiAttachment in aiAttachments?.Where(IsUsableAttachment) ?? [])
        {
            if (merged.Any(existing => IsSameAttachment(existing, aiAttachment)))
            {
                continue;
            }

            merged.Add(aiAttachment);
        }

        return merged.Count == 0 ? null : merged;
    }

    private static bool IsUsableAttachment(PostingAttachment attachment)
    {
        return (!string.IsNullOrWhiteSpace(attachment.Url)
                || !string.IsNullOrWhiteSpace(attachment.Base64))
               && !string.IsNullOrWhiteSpace(attachment.Extension);
    }

    private static bool IsSameAttachment(PostingAttachment left, PostingAttachment right)
    {
        var leftUrl = NormalizeUrl(left.Url);
        var rightUrl = NormalizeUrl(right.Url);

        if (leftUrl is not null || rightUrl is not null)
        {
            return string.Equals(leftUrl, rightUrl, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(left.Base64, right.Base64, StringComparison.Ordinal)
               && string.Equals(left.Extension, right.Extension, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeUrl(string? url)
    {
        return string.IsNullOrWhiteSpace(url)
            ? null
            : url.Trim().TrimEnd('/');
    }
}
