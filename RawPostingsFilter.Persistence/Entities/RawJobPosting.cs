namespace RawPostingsFilter.Persistence.Entities;

public sealed class RawJobPosting
{
    public long Id { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    public required string Source { get; set; }

    public DateTimeOffset PostedAt { get; set; }

    public required string Url { get; set; }

    public string? RawText { get; set; }

    public bool IsNormalized { get; set; }

    public bool IsSpamOrAd { get; set; }

    public required string IncomingPosting { get; set; }
}
