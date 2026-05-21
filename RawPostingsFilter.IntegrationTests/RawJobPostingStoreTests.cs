using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RawPostingsFilter.Application.Models;
using RawPostingsFilter.Application.Processing;
using RawPostingsFilter.Infrastructure.Serialization;
using RawPostingsFilter.Persistence;
using RawPostingsFilter.Persistence.Options;

namespace RawPostingsFilter.IntegrationTests;

public sealed class RawJobPostingStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task StoreIfNotDuplicateAsync_filters_raw_duplicates_and_preserves_incoming_json()
    {
        await fixture.ResetAsync();

        await using var dbContext = fixture.CreateDbContext();
        var store = CreateStore(dbContext);
        var results = new List<StorePostingResult>();

        foreach (var posting in CreateSeedBatch())
        {
            results.Add(await store.StoreIfNotDuplicateAsync(posting, CancellationToken.None));
        }

        Assert.True(results[0].Stored);
        Assert.False(results[1].Stored);
        Assert.False(results[2].Stored);
        Assert.True(results[2].Similarity >= 0.80);
        Assert.True(results[3].Stored);
        Assert.True(results[4].Stored);

        var rows = await dbContext.RawJobPostings
            .AsNoTracking()
            .OrderBy(posting => posting.Url)
            .ToListAsync();

        Assert.Equal(3, rows.Count);
        Assert.Contains(rows, posting => posting.Url == "https://t.me/jobs/1" && !posting.IsNormalized);
        Assert.Contains(rows, posting => posting.Url == "https://t.me/jobs/4" && !posting.IsNormalized);
        Assert.DoesNotContain(rows, posting => posting.Url == "https://t.me/jobs/2");
        Assert.DoesNotContain(rows, posting => posting.Url == "https://t.me/jobs/3");

        var normalized = Assert.Single(rows, posting => posting.Url == "https://freelance.example/jobs/5");
        Assert.True(normalized.IsNormalized);

        using var document = JsonDocument.Parse(normalized.IncomingPosting);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("raw_text", out var rawText));
        Assert.Contains("Backend .NET developer", rawText.GetString());
        Assert.True(root.TryGetProperty("payload", out var payload));
        Assert.Equal("usd", payload.GetProperty("currency").GetString());
    }

    [Fact]
    public async Task StoreIfNotDuplicateAsync_ignores_duplicates_posted_more_than_week_before_incoming()
    {
        await fixture.ResetAsync();

        await using var dbContext = fixture.CreateDbContext();
        var store = CreateStore(dbContext);
        const string rawText =
            "Senior backend developer for RabbitMQ, PostgreSQL, and deduplication service. "
            + "The work includes background workers, database indexes, integration tests, monitoring, and careful production rollout.";

        var oldPosting = new IncomingJobPosting
        {
            Source = "tg",
            PostedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
            Url = "https://t.me/jobs/old",
            RawText = rawText
        };

        var incomingPosting = new IncomingJobPosting
        {
            Source = "tg",
            PostedAt = oldPosting.PostedAt.AddDays(8),
            Url = "https://t.me/jobs/incoming",
            RawText = rawText
        };

        var oldResult = await store.StoreIfNotDuplicateAsync(oldPosting, CancellationToken.None);
        var incomingResult = await store.StoreIfNotDuplicateAsync(incomingPosting, CancellationToken.None);

        Assert.True(oldResult.Stored);
        Assert.True(incomingResult.Stored);

        var storedCount = await dbContext.RawJobPostings.CountAsync();

        Assert.Equal(2, storedCount);
    }

    [Fact]
    public async Task StoreIfNotDuplicateAsync_stores_stale_duplicates_when_previous_duplicate_is_spam_or_ad()
    {
        await fixture.ResetAsync();

        await using var dbContext = fixture.CreateDbContext();
        var store = CreateStore(dbContext);
        const string rawText =
            "Remote assistant job for everyone with no experience. We teach for free, take every beginner, "
            + "and promise very high earnings after two weeks with simple daily tasks and no real requirements.";

        var spamPosting = new IncomingJobPosting
        {
            Source = "tg",
            PostedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
            Url = "https://t.me/jobs/spam",
            RawText = rawText
        };

        var incomingPosting = new IncomingJobPosting
        {
            Source = "tg",
            PostedAt = spamPosting.PostedAt.AddDays(30),
            Url = "https://t.me/jobs/spam-repeat",
            RawText = rawText
        };

        var spamResult = await store.StoreIfNotDuplicateAsync(spamPosting, CancellationToken.None);
        Assert.True(spamResult.Stored);

        await store.MarkAsSpamOrAdAsync(spamResult.StoredPostingId!.Value, CancellationToken.None);

        var incomingResult = await store.StoreIfNotDuplicateAsync(incomingPosting, CancellationToken.None);

        Assert.Equal(StorePostingStatus.StoredSpamOrAdDuplicate, incomingResult.Status);
        Assert.True(incomingResult.Stored);
        Assert.True(incomingResult.IsSpamOrAd);
        Assert.Equal(spamResult.StoredPostingId, incomingResult.DuplicatePostingId);

        var rows = await dbContext.RawJobPostings
            .AsNoTracking()
            .OrderBy(posting => posting.Url)
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.All(rows, posting => Assert.True(posting.IsSpamOrAd));
    }

    [Fact]
    public async Task StoreIfNotDuplicateAsync_skips_duplicates_posted_within_week_before_incoming()
    {
        await fixture.ResetAsync();

        await using var dbContext = fixture.CreateDbContext();
        var store = CreateStore(dbContext);
        const string rawText =
            "Senior backend developer for RabbitMQ, PostgreSQL, and deduplication service. "
            + "The work includes background workers, database indexes, integration tests, monitoring, and careful production rollout.";

        var recentPosting = new IncomingJobPosting
        {
            Source = "tg",
            PostedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
            Url = "https://t.me/jobs/recent",
            RawText = rawText
        };

        var incomingPosting = new IncomingJobPosting
        {
            Source = "tg",
            PostedAt = recentPosting.PostedAt.AddDays(6),
            Url = "https://t.me/jobs/incoming",
            RawText = rawText
        };

        var recentResult = await store.StoreIfNotDuplicateAsync(recentPosting, CancellationToken.None);
        var incomingResult = await store.StoreIfNotDuplicateAsync(incomingPosting, CancellationToken.None);

        Assert.True(recentResult.Stored);
        Assert.False(incomingResult.Stored);
        Assert.Equal(recentResult.StoredPostingId, incomingResult.DuplicatePostingId);

        var storedCount = await dbContext.RawJobPostings.CountAsync();

        Assert.Equal(1, storedCount);
    }

    [Fact]
    public async Task StoreIfNotDuplicateAsync_skips_postings_below_twenty_words()
    {
        await fixture.ResetAsync();

        await using var dbContext = fixture.CreateDbContext();
        var store = CreateStore(dbContext);

        var shortPosting = new IncomingJobPosting
        {
            Source = "tg",
            PostedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
            Url = "https://t.me/jobs/short",
            RawText = "Need backend developer for quick RabbitMQ task"
        };

        var result = await store.StoreIfNotDuplicateAsync(shortPosting, CancellationToken.None);

        Assert.False(result.Stored);
        Assert.Equal(StorePostingStatus.TooShort, result.Status);
        Assert.Equal(7, result.WordCount);

        var storedCount = await dbContext.RawJobPostings.CountAsync();

        Assert.Equal(0, storedCount);
    }

    [Fact]
    public async Task Migrations_enable_pg_trgm_and_create_raw_text_trgm_index()
    {
        await using var dbContext = fixture.CreateDbContext();

        var extensionCount = await dbContext.Database
            .SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM pg_extension WHERE extname = 'pg_trgm'")
            .SingleAsync();

        var indexCount = await dbContext.Database
            .SqlQueryRaw<int>("""
                SELECT COUNT(*)::int AS "Value"
                FROM pg_indexes
                WHERE schemaname = 'public'
                  AND tablename = 'raw_job_postings'
                  AND indexname = 'ix_raw_job_postings_raw_text_trgm'
                  AND indexdef ILIKE '%gin%'
                  AND indexdef ILIKE '%gin_trgm_ops%'
                """)
            .SingleAsync();

        Assert.Equal(1, extensionCount);
        Assert.Equal(1, indexCount);
    }

    private static RawJobPostingStore CreateStore(RawPostingsDbContext dbContext)
    {
        return new RawJobPostingStore(
            dbContext,
            Options.Create(new DeduplicationOptions { SimilarityThreshold = 0.80 }),
            JsonSerializerOptionsFactory.Create(),
            TimeProvider.System,
            NullLogger<RawJobPostingStore>.Instance);
    }

    private static IReadOnlyList<IncomingJobPosting> CreateSeedBatch()
    {
        const string primaryRawText =
            "Backend .NET developer needed for PostgreSQL RabbitMQ deduplication pipeline. "
            + "Responsibilities include workers, integration tests, Docker, monitoring, production support, "
            + "and careful implementation of message processing with reliable logging.";

        const string closeDuplicateRawText =
            "Backend .NET developer needed for PostgreSQL and RabbitMQ deduplication pipeline. "
            + "Responsibilities include worker services, integration tests, Docker, monitoring, production support, "
            + "and careful implementation of message processing with reliable logs.";

        return
        [
            new IncomingJobPosting
            {
                Source = "tg",
                PostedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
                Url = "https://t.me/jobs/1",
                RawText = primaryRawText
            },
            new IncomingJobPosting
            {
                Source = "tg",
                PostedAt = new DateTimeOffset(2026, 5, 1, 12, 1, 0, TimeSpan.Zero),
                Url = "https://t.me/jobs/2",
                RawText = primaryRawText
            },
            new IncomingJobPosting
            {
                Source = "tg",
                PostedAt = new DateTimeOffset(2026, 5, 1, 12, 2, 0, TimeSpan.Zero),
                Url = "https://t.me/jobs/3",
                RawText = closeDuplicateRawText
            },
            new IncomingJobPosting
            {
                Source = "tg",
                PostedAt = new DateTimeOffset(2026, 5, 1, 12, 3, 0, TimeSpan.Zero),
                Url = "https://t.me/jobs/4",
                RawText = "Frontend React developer needed for dashboard UI, accessibility, component design, forms, state management, API integration, browser testing, and release support."
            },
            new IncomingJobPosting
            {
                Source = "fl",
                PostedAt = new DateTimeOffset(2026, 5, 1, 12, 4, 0, TimeSpan.Zero),
                Url = "https://freelance.example/jobs/5",
                RawText = "Backend .NET developer normalized duplicate sample that should still be stored because payload is present and source already handles deduplication.",
                Payload = new IncomingPostingPayload
                {
                    Author = "Client",
                    Title = "Backend .NET developer",
                    PriceMin = 1000,
                    PriceMax = 1500,
                    Currency = Currency.Usd,
                    Description = "Backend .NET developer normalized duplicate sample that should still be stored because payload is present and source already handles deduplication."
                }
            }
        ];
    }
}
