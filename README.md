# RawPostingsFilter

Production .NET 10 worker for the FitFlow raw-posting pipeline. The service consumes raw job postings from RabbitMQ, stores accepted incoming messages in PostgreSQL, deduplicates raw Telegram-style postings with `pg_trgm`, normalizes accepted postings with TextAI, and publishes normalized `OutgoingJobPosting` messages to a downstream RabbitMQ exchange.

## Runtime Pipeline

```text
RabbitMQ raw-postings.incoming
        |
        v
RawPostingsFilter consumers
        |
        v
PostgreSQL raw_job_postings
        |
        v
Deduplication, length checks, spam/ad checks
        |
        v
TextAI normalization
        |
        v
RabbitMQ raw-postings-filter.outgoing.mock
```

## Behavior

- Starts only the RabbitMQ-to-RabbitMQ hosted pipeline.
- Applies EF Core migrations on startup.
- Consumes up to `RabbitMq:ConsumerCount` messages concurrently.
- Uses prefetch `RabbitMq:PrefetchCount`.
- Stores accepted incoming postings in `raw_job_postings.incoming_posting` as JSONB.
- Skips postings below 20 words.
- Deduplicates only unnormalized raw-text postings where `Payload == null`.
- Uses PostgreSQL `pg_trgm` `%` and `similarity(...)` with `Deduplication:SimilarityThreshold`.
- Ignores non-spam duplicate matches older than 7 days.
- Stores close matches to known spam/ad postings as spam/ad without sending them to AI.
- Publishes only accepted, non-spam, successfully normalized postings downstream.
- Sends malformed JSON and unexpected processing/publish failures to the dead-letter queue.

## Projects

| Project | Purpose |
| --- | --- |
| `RawPostingsFilter` | Worker host and RabbitMQ hosted service. |
| `RawPostingsFilter.Application` | Domain models, processing orchestration, and application abstractions. |
| `RawPostingsFilter.Infrastructure` | RabbitMQ topology, JSON parsing/serialization, and AI normalization. |
| `RawPostingsFilter.Persistence` | EF Core DbContext, migrations, and PostgreSQL trigram storage logic. |
| `RawPostingsFilter.IntegrationTests` | PostgreSQL-backed integration tests. |

## Configuration

The app reads `RawPostingsFilter/appsettings.json` and environment variables.

| Section | Description |
| --- | --- |
| `ConnectionStrings:Postgres` | PostgreSQL connection string. |
| `Deduplication:SimilarityThreshold` | Trigram similarity threshold. Default: `0.80`. |
| `RabbitMq` | RabbitMQ connection, topology, consumer count, and prefetch count. |
| `TextAI` | Text AI endpoint, API key, timeout, and retry settings. |
| `AIModels` | Normalization model alias/name and provider request extensions. |

Set secrets through environment variables:

```powershell
$env:TEXTAI_API_KEY = "<api-key>"
docker compose up --build
```

## RabbitMQ Topology

| Resource | Name |
| --- | --- |
| Incoming exchange | `raw-postings.incoming` |
| Incoming queue | `raw-postings-filter.incoming` |
| Incoming routing key | `job-posting.raw` |
| Outgoing exchange | `raw-postings-filter.outgoing.mock` |
| Outgoing queue | `raw-postings-filter.outgoing.mock.queue` |
| Outgoing routing key | `job-posting.normalized` |
| Dead-letter exchange | `raw-postings.dead-letter` |
| Dead-letter queue | `raw-postings-filter.dead-letter` |

## Docker

Start the production local stack:

```powershell
docker compose up --build
```

Services:

| Service | Description |
| --- | --- |
| `rabbitmq` | RabbitMQ broker with management UI on `http://localhost:15672`. |
| `postgres` | PostgreSQL database for raw postings and trigram indexes. |
| `app` | RawPostingsFilter worker. |

Stop and remove containers:

```powershell
docker compose down
```

## Tests

Integration tests require PostgreSQL. With the Compose database running:

```powershell
docker compose up -d postgres
dotnet test RawPostingsFilter.sln
docker compose down
```

## Data Contracts

Incoming messages are JSON with snake_case field names:

```json
{
  "source": "tg",
  "posted_at": "25:12:20:07:07:43",
  "url": "https://example.com/post",
  "payload": null,
  "raw_text": "raw posting text"
}
```

Outgoing messages remove `raw_text` and include normalized payload data:

```json
{
  "source": "tg",
  "posted_at": "2025-12-20T07:07:43+00:00",
  "url": "https://example.com/post",
  "payload": {
    "title": "Technical specialist",
    "currency": "rub",
    "required_tools": []
  }
}
```

See [DOCUMENTATION.md](DOCUMENTATION.md) for the public API and model reference.

