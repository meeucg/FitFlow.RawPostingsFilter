using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RawPostingsFilter.Application.Abstractions;
using RawPostingsFilter.Application.Models;
using RawPostingsFilter.Application.Processing;
using RawPostingsFilter.Persistence.Entities;
using RawPostingsFilter.Persistence.Options;

namespace RawPostingsFilter.Persistence;

public sealed class RawJobPostingStore(
    RawPostingsDbContext dbContext,
    IOptions<DeduplicationOptions> options,
    JsonSerializerOptions jsonSerializerOptions,
    TimeProvider timeProvider,
    ILogger<RawJobPostingStore> logger) : IRawJobPostingStore
{
    private static readonly TimeSpan DuplicateFreshnessWindow = TimeSpan.FromDays(7);
    private const int MinimumPostingWordCount = 20;

    private readonly double _similarityThreshold = options.Value.SimilarityThreshold;

    public async Task<StorePostingResult> StoreIfNotDuplicateAsync(
        IncomingJobPosting posting,
        CancellationToken cancellationToken)
    {
        var wordCount = CountWords(GetTextForLengthCheck(posting));
        if (wordCount < MinimumPostingWordCount)
        {
            logger.LogInformation(
                "Skipped posting from {Source} because it has {WordCount} words. MinimumWordCount={MinimumPostingWordCount}",
                posting.Source,
                wordCount,
                MinimumPostingWordCount);

            return StorePostingResult.TooShortResult(wordCount);
        }

        var isNormalized = posting.Payload is not null;
        var shouldDeduplicate = !isNormalized && !string.IsNullOrWhiteSpace(posting.RawText);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (shouldDeduplicate)
        {
            await SetLocalSimilarityThresholdAsync(cancellationToken);

            var spamOrAdDuplicate = await FindSpamOrAdDuplicateAsync(posting.RawText!, cancellationToken);
            if (spamOrAdDuplicate is not null)
            {
                var spamEntity = CreateEntity(posting, isNormalized, isSpamOrAd: true);

                dbContext.RawJobPostings.Add(spamEntity);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                logger.LogInformation(
                    "Stored posting {PostingId} from {Source} as spam/ad because it matched spam/ad posting {DuplicatePostingId}. Similarity={Similarity:F3}",
                    spamEntity.Id,
                    posting.Source,
                    spamOrAdDuplicate.Id,
                    spamOrAdDuplicate.Similarity);

                return StorePostingResult.StoredSpamOrAdDuplicateResult(
                    spamEntity.Id,
                    spamOrAdDuplicate.Id,
                    spamOrAdDuplicate.Similarity);
            }

            var duplicate = await FindDuplicateAsync(posting.RawText!, posting.PostedAt, cancellationToken);
            if (duplicate is not null)
            {
                await transaction.CommitAsync(cancellationToken);

                logger.LogInformation(
                    "Skipped duplicate posting from {Source}. DuplicateOfId={DuplicatePostingId}, Similarity={Similarity:F3}",
                    posting.Source,
                    duplicate.Id,
                    duplicate.Similarity);

                return StorePostingResult.DuplicateResult(duplicate.Id, duplicate.Similarity);
            }
        }

        var entity = CreateEntity(posting, isNormalized, isSpamOrAd: false);

        dbContext.RawJobPostings.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Stored posting {PostingId} from {Source}", entity.Id, posting.Source);

        return StorePostingResult.StoredResult(entity.Id);
    }

    public async Task MarkAsSpamOrAdAsync(
        long postingId,
        CancellationToken cancellationToken)
    {
        var updatedRows = await dbContext.RawJobPostings
            .Where(posting => posting.Id == postingId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(posting => posting.IsSpamOrAd, true),
                cancellationToken);

        if (updatedRows == 0)
        {
            logger.LogWarning(
                "Could not mark posting {PostingId} as spam/ad because it was not found.",
                postingId);
        }
    }

    private RawJobPosting CreateEntity(
        IncomingJobPosting posting,
        bool isNormalized,
        bool isSpamOrAd)
    {
        return new RawJobPosting
        {
            ReceivedAt = timeProvider.GetUtcNow(),
            Source = posting.Source,
            PostedAt = posting.PostedAt,
            Url = posting.Url,
            RawText = posting.RawText,
            IsNormalized = isNormalized,
            IsSpamOrAd = isSpamOrAd,
            IncomingPosting = JsonSerializer.Serialize(posting, jsonSerializerOptions)
        };
    }

    private static string GetTextForLengthCheck(IncomingJobPosting posting)
    {
        if (!string.IsNullOrWhiteSpace(posting.RawText))
        {
            return posting.RawText;
        }

        if (posting.Payload is null)
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            new[] { posting.Payload.Title, posting.Payload.Description }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static int CountWords(string text)
    {
        var wordCount = 0;
        var insideWord = false;

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                insideWord = false;
                continue;
            }

            if (insideWord)
            {
                continue;
            }

            wordCount++;
            insideWord = true;
        }

        return wordCount;
    }

    private Task SetLocalSimilarityThresholdAsync(CancellationToken cancellationToken)
    {
        var threshold = _similarityThreshold.ToString(CultureInfo.InvariantCulture);

        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('pg_trgm.similarity_threshold', {threshold}, true)",
            cancellationToken);
    }

    private async Task<RawDuplicateCandidate?> FindDuplicateAsync(
        string rawText,
        DateTimeOffset postedAt,
        CancellationToken cancellationToken)
    {
        var duplicateCutoff = postedAt.Subtract(DuplicateFreshnessWindow);

        var duplicateCandidates = await dbContext.RawDuplicateCandidates
            .FromSqlInterpolated($"""
                SELECT id, similarity(raw_text, {rawText}) AS similarity
                FROM raw_job_postings
                WHERE raw_text IS NOT NULL
                  AND is_normalized = false
                  AND posted_at >= {duplicateCutoff}
                  AND raw_text % {rawText}
                ORDER BY similarity DESC
                LIMIT 1
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return duplicateCandidates.FirstOrDefault();
    }

    private async Task<RawDuplicateCandidate?> FindSpamOrAdDuplicateAsync(
        string rawText,
        CancellationToken cancellationToken)
    {
        var duplicateCandidates = await dbContext.RawDuplicateCandidates
            .FromSqlInterpolated($"""
                SELECT id, similarity(raw_text, {rawText}) AS similarity
                FROM raw_job_postings
                WHERE raw_text IS NOT NULL
                  AND is_normalized = false
                  AND is_spam_or_ad = true
                  AND raw_text % {rawText}
                ORDER BY similarity DESC
                LIMIT 1
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return duplicateCandidates.FirstOrDefault();
    }
}
