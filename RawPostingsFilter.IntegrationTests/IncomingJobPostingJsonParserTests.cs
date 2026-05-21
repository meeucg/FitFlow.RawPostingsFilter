using System.Text.Json;
using RawPostingsFilter.Application.Models;
using RawPostingsFilter.Infrastructure.Serialization;

namespace RawPostingsFilter.IntegrationTests;

public sealed class IncomingJobPostingJsonParserTests
{
    private static readonly JsonSerializerOptions SerializerOptions = JsonSerializerOptionsFactory.Create();

    [Theory]
    [InlineData("26:05:18:15:24:00", 2026)]
    [InlineData("2026:05:19:23:36:38", 2026)]
    public void TryParse_accepts_supported_posted_at_formats(string postedAt, int expectedYear)
    {
        var json = $$"""
            {
              "source": "tg",
              "posted_at": "{{postedAt}}",
              "url": "https://example.test/job",
              "payload": null,
              "raw_text": "Need a backend developer for RabbitMQ processing and PostgreSQL deduplication."
            }
            """;

        var parsed = IncomingJobPostingJsonParser.TryParse(
            json,
            SerializerOptions,
            out var posting,
            out var error);

        Assert.True(parsed, error);
        Assert.Equal(expectedYear, posting.PostedAt.Year);
        Assert.Equal("https://example.test/job", posting.Url);
        Assert.Contains("RabbitMQ", posting.RawText);
    }

    [Fact]
    public void TryParse_defaults_missing_payload_currency_to_rub()
    {
        const string json = """
            {
              "source": "kwork",
              "posted_at": "2026:05:19:23:36:38",
              "url": "https://kwork.example/job",
              "payload": {
                "author": "client",
                "title": "Python task",
                "price_min": 1000,
                "price_max": 1000,
                "description": "Build a small automation script.",
                "attached_files": []
              },
              "raw_text": "Python task\nBuild a small automation script."
            }
            """;

        var parsed = IncomingJobPostingJsonParser.TryParse(
            json,
            SerializerOptions,
            out var posting,
            out var error);

        Assert.True(parsed, error);
        Assert.NotNull(posting.Payload);
        Assert.Equal(Currency.Rub, posting.Payload.Currency);
        Assert.Equal("Python task", posting.Payload.Title);
    }

    [Fact]
    public void TryParse_rejects_malformed_messages()
    {
        const string json = """
            {
              "source": "tg",
              "posted_at": "not-a-date",
              "payload": null
            }
            """;

        var parsed = IncomingJobPostingJsonParser.TryParse(
            json,
            SerializerOptions,
            out _,
            out var error);

        Assert.False(parsed);
        Assert.NotNull(error);
    }
}
