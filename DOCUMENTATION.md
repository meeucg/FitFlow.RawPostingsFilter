# RawPostingsFilter Documentation

This document describes the production public surface of RawPostingsFilter: models, processing services, persistence services, infrastructure helpers, hosted services, options, signatures, and return values.

## Pipeline

1. `RabbitMqProcessingHostedService` applies database migrations, declares RabbitMQ topology, and starts consumer channels.
2. Each RabbitMQ delivery is parsed by `IncomingJobPostingJsonParser`.
3. `JobPostingProcessor.ProcessAsync(...)` calls `IRawJobPostingStore`.
4. `RawJobPostingStore.StoreIfNotDuplicateAsync(...)` performs the minimum-word check, spam/ad duplicate check, fresh duplicate check, and storage.
5. Stored non-spam postings are normalized by `IJobPostingNormalizer`.
6. Accepted normalized postings are published as `OutgoingJobPosting`.

## Application Models

### `IncomingJobPosting`

Summary: Raw parser output consumed from RabbitMQ.

Signature:

```csharp
public sealed record IncomingJobPosting
```

Properties:

| Property | Type | Description |
| --- | --- | --- |
| `Source` | `string` | Source identifier, for example `tg`, `kwork`, or `fl`. |
| `PostedAt` | `DateTimeOffset` | Source posting date. |
| `Url` | `string` | Source URL or stable source identifier. |
| `Payload` | `IncomingPostingPayload?` | Already parsed payload. If present, trigram deduplication is skipped. |
| `RawText` | `string?` | Full unnormalized text used for length checks, trigram deduplication, and AI normalization. |

Return description: Record value only.

### `IncomingPostingPayload`

Summary: Normalized payload supplied by upstream parsers.

Signature:

```csharp
public sealed record IncomingPostingPayload
```

Properties: `Author`, `Title`, `PriceMin`, `PriceMax`, `Currency`, `Description`, `AttachedFiles`.

Return description: Record value only.

### `OutgoingJobPosting`

Summary: Downstream posting contract published after filtering and normalization.

Signature:

```csharp
public sealed record OutgoingJobPosting
```

Properties:

| Property | Type | Description |
| --- | --- | --- |
| `Source` | `string` | Original source identifier. |
| `PostedAt` | `DateTimeOffset` | Original source posting date. |
| `Url` | `string` | Original source URL. |
| `Payload` | `OutgoingPostingPayload?` | Normalized payload. |

Return description: Record value only.

### `OutgoingPostingPayload`

Summary: Normalized posting payload used by downstream business logic and recommendation matching.

Signature:

```csharp
public sealed record OutgoingPostingPayload
```

Properties:

| Property | Type | Description |
| --- | --- | --- |
| `Author` | `string?` | Author, contact, or company name when known. |
| `Title` | `string?` | Short title for the vacancy/order. |
| `PriceMin` | `decimal?` | Lower payment bound without currency. |
| `PriceMax` | `decimal?` | Upper payment bound without currency. |
| `Currency` | `Currency` | Payment currency. |
| `Description` | `string?` | Clean full description. |
| `AttachedFiles` | `IReadOnlyList<PostingAttachment>?` | Attached files or file-like links. |
| `Cluster` | `string?` | Broad professional cluster. |
| `Specializations` | `IReadOnlyList<JobPostingSpecialization>` | Professional specializations. |
| `RequiredSkills` | `IReadOnlyList<JobPostingSkill>` | Required skills for user-profile comparison. |
| `BonusSkills` | `IReadOnlyList<JobPostingSkill>` | Optional skills for user-profile comparison. |
| `RequiredTools` | `IReadOnlyList<JobPostingTool>` | Required tools comparable to user tools. |
| `BonusTools` | `IReadOnlyList<JobPostingTool>` | Optional tools comparable to user tools. |
| `Domains` | `IReadOnlyList<JobPostingDomain>` | Business/product domains. |

Return description: Record value only.

### `AINormalizedJobPosting`

Summary: AI response schema used internally before merging AI values with incoming payload values.

Signature:

```csharp
public sealed record AINormalizedJobPosting
```

Properties: `IsSpamOrAd`, business payload fields, and comparable matching fields. If `IsSpamOrAd` is true, payload-like fields should be empty.

Return description: Record value only.

### Supporting Models

| Type | Signature | Summary | Return description |
| --- | --- | --- | --- |
| `PostingAttachment` | `public sealed record PostingAttachment` | File reference with `Url`, `Base64`, and `Extension`. | Record value. |
| `JobPostingSkill` | `public sealed record JobPostingSkill` | Skill with display name, description, and alternative names. | Record value. |
| `JobPostingTool` | `public sealed record JobPostingTool` | Tool or technology with standard and alternative names. | Record value. |
| `JobPostingSpecialization` | `public sealed record JobPostingSpecialization` | Professional specialization and aliases. | Record value. |
| `JobPostingDomain` | `public sealed record JobPostingDomain` | Business/product domain and aliases. | Record value. |
| `Currency` | `public enum Currency` | Supported currencies: `rub`, `usd`, `eur`. | Enum value. |

## Application Services

### `JobPostingProcessor`

Summary: Orchestrates storage, deduplication result handling, AI normalization, and spam/ad marking.

Signature:

```csharp
public sealed class JobPostingProcessor(
    IRawJobPostingStore store,
    IJobPostingNormalizer normalizer)
```

Method:

```csharp
public Task<JobPostingProcessingResult> ProcessAsync(
    IncomingJobPosting posting,
    CancellationToken cancellationToken)
```

Return description: Returns `JobPostingProcessingResult`. When `Outcome == StoredAndNormalized`, `Posting` contains the outgoing posting to publish.

### `JobPostingProcessingResult`

Summary: High-level processing result returned to the RabbitMQ hosted service.

Signature:

```csharp
public sealed record JobPostingProcessingResult(
    JobPostingProcessingOutcome Outcome,
    OutgoingJobPosting? Posting = null)
```

Return description: Record value with convenience boolean properties for stored, normalized, spam/ad, duplicate, too-short, and normalization-failed states.

### `StorePostingResult`

Summary: Persistence/deduplication result returned by the raw store.

Signature:

```csharp
public sealed record StorePostingResult
```

Factory methods:

| Signature | Return description |
| --- | --- |
| `public static StorePostingResult StoredResult(long storedPostingId)` | Stored posting result with row id. |
| `public static StorePostingResult StoredSpamOrAdDuplicateResult(long storedPostingId, long duplicatePostingId, double similarity)` | Stored posting marked spam/ad because it matched known spam/ad. |
| `public static StorePostingResult DuplicateResult(long duplicatePostingId, double similarity)` | Skipped duplicate result with matching row id and similarity. |
| `public static StorePostingResult TooShortResult(int wordCount)` | Skipped too-short result with measured word count. |

### Application Abstractions

| Type | Signature | Summary | Return description |
| --- | --- | --- | --- |
| `IJobPostingNormalizer` | `Task<JobPostingNormalizationResult> NormalizeAsync(IncomingJobPosting posting, CancellationToken cancellationToken)` | Normalizes a stored posting with AI. | Normalization result with outgoing posting, success flag, spam/ad flag, and optional error. |
| `IRawJobPostingStore` | `Task<StorePostingResult> StoreIfNotDuplicateAsync(IncomingJobPosting posting, CancellationToken cancellationToken)` | Stores a posting unless it is too short or duplicate. | Store/deduplication result. |
| `IRawJobPostingStore` | `Task MarkAsSpamOrAdAsync(long postingId, CancellationToken cancellationToken)` | Marks a stored posting as spam/ad. | Completes when the update has been attempted. |

## Infrastructure

### `AiJobPostingNormalizer`

Summary: Uses `ITextAI` to classify spam/ad and normalize missing payload fields.

Signature:

```csharp
public sealed class AiJobPostingNormalizer(
    ITextAI textAI,
    IOptions<TextAIOptions> textAIOptions,
    ILogger<AiJobPostingNormalizer> logger) : IJobPostingNormalizer
```

Method:

```csharp
public Task<JobPostingNormalizationResult> NormalizeAsync(
    IncomingJobPosting posting,
    CancellationToken cancellationToken)
```

Return description: Returns an AI-backed normalization result. If AI is unavailable or returns no usable response, `AiSucceeded` is false and the posting is not published downstream.

### `IncomingJobPostingJsonParser`

Summary: Parses raw RabbitMQ JSON into `IncomingJobPosting`.

Signature:

```csharp
public static class IncomingJobPostingJsonParser
```

Method:

```csharp
public static bool TryParse(
    string json,
    JsonSerializerOptions jsonSerializerOptions,
    out IncomingJobPosting? posting)
```

Return description: Returns `true` and sets `posting` for valid JSON. Returns `false` and sets `posting` to null for malformed JSON, invalid values, or unsupported dates. Supported source date formats are `yy:MM:dd:HH:mm:ss` and `yyyy:MM:dd:HH:mm:ss`.

### `RabbitMqTopology`

Summary: Declares production RabbitMQ exchanges, queues, bindings, and dead-letter topology.

Signature:

```csharp
public static class RabbitMqTopology
```

Method:

```csharp
public static Task DeclareAsync(
    IChannel channel,
    RabbitMqOptions options,
    CancellationToken cancellationToken)
```

Return description: Completes after all durable RabbitMQ resources are declared and bound.

### Other Infrastructure Helpers

| Type | Signature | Summary | Return description |
| --- | --- | --- | --- |
| `JsonSerializerOptionsFactory` | `public static JsonSerializerOptions Create()` | Creates snake_case JSON options with string enum values. | Configured `JsonSerializerOptions`. |
| `AIJsonOptionsFactory` | `public static void Configure(AIJsonOptions options)` | Configures AI serializer/schema options with Russian description export. | Mutates the provided options. |

## Persistence

### `RawPostingsDbContext`

Summary: EF Core PostgreSQL context for raw postings and trigram duplicate candidate projections.

Signature:

```csharp
public sealed class RawPostingsDbContext(DbContextOptions<RawPostingsDbContext> options) : DbContext(options)
```

Return description: Provides `RawJobPostings` and `RawDuplicateCandidates` sets.

### `RawJobPostingStore`

Summary: Stores incoming postings and applies production filtering/deduplication rules.

Signature:

```csharp
public sealed class RawJobPostingStore(
    RawPostingsDbContext dbContext,
    IOptions<DeduplicationOptions> options,
    JsonSerializerOptions jsonSerializerOptions,
    TimeProvider timeProvider,
    ILogger<RawJobPostingStore> logger) : IRawJobPostingStore
```

Methods:

| Signature | Return description |
| --- | --- |
| `public Task<StorePostingResult> StoreIfNotDuplicateAsync(IncomingJobPosting posting, CancellationToken cancellationToken)` | Stores the posting, skips it as too short, skips it as duplicate, or stores it as spam/ad duplicate. |
| `public Task MarkAsSpamOrAdAsync(long postingId, CancellationToken cancellationToken)` | Marks a stored row as spam/ad if it exists. |

Deduplication rules:

- Only unnormalized postings with `Payload == null` and non-empty `RawText` are trigram-deduplicated.
- Duplicate matching uses PostgreSQL `%` and `similarity(...)`.
- Fresh duplicate matching only considers stored rows with `posted_at >= incoming.PostedAt - 7 days`.
- Spam/ad duplicate matching ignores the 7-day freshness window.

### Persistence Entities And Options

| Type | Signature | Summary | Return description |
| --- | --- | --- | --- |
| `RawJobPosting` | `public sealed class RawJobPosting` | Database row for `raw_job_postings`. | Entity instance. |
| `RawDuplicateCandidate` | `public sealed class RawDuplicateCandidate` | Keyless SQL projection for duplicate id and similarity. | Projection instance. |
| `DeduplicationOptions` | `public sealed class DeduplicationOptions` | Configures trigram similarity threshold. | Options instance. |
| `RawPostingsDatabaseMigrator` | `public Task MigrateAsync(CancellationToken cancellationToken)` | Applies EF Core migrations. | Completes after migrations are applied. |
| `RawPostingsDbContextFactory` | `public RawPostingsDbContext CreateDbContext(string[] args)` | Design-time DbContext factory for EF tools. | New DbContext instance. |

## Hosted Service

### `RabbitMqProcessingHostedService`

Summary: Production hosted service that consumes raw postings from RabbitMQ and publishes accepted normalized postings to RabbitMQ.

Signature:

```csharp
public sealed class RabbitMqProcessingHostedService(...) : BackgroundService
```

Protected method:

```csharp
protected override Task ExecuteAsync(CancellationToken stoppingToken)
```

Return description: Runs until cancellation. It applies migrations, connects to RabbitMQ with retry, declares topology, starts consumers, and keeps the worker alive.

Message outcomes:

| Outcome | Ack/Nack | Downstream publish |
| --- | --- | --- |
| Accepted and normalized | Ack | Yes |
| Duplicate | Ack | No |
| Too short | Ack | No |
| Spam/ad | Ack | No |
| Spam/ad duplicate | Ack | No |
| Normalization failed | Ack | No |
| Invalid JSON | Nack without requeue | No |
| Unexpected exception | Nack without requeue | No |

## Options

| Type | Section | Summary |
| --- | --- | --- |
| `RabbitMqOptions` | `RabbitMq` | RabbitMQ connection settings, topology names, consumer count, and prefetch count. |
| `DeduplicationOptions` | `Deduplication` | Trigram similarity threshold. |

## Migrations

| Migration | Summary |
| --- | --- |
| `InitialRawJobPostings` | Creates `raw_job_postings`, enables `pg_trgm`, and creates a partial GIN trigram index on unnormalized non-null `raw_text`. |
| `AddSpamOrAdFlag` | Adds `is_spam_or_ad` to stored raw postings. |

