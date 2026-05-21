namespace RawPostingsFilter.Application.Models;

public sealed record IncomingJobPosting
{
    public required string Source { get; init; }

    public required DateTimeOffset PostedAt { get; init; }

    public required string Url { get; init; }

    public IncomingPostingPayload? Payload { get; init; }

    public string? RawText { get; init; }
}
