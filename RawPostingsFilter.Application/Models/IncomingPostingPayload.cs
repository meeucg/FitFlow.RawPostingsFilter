namespace RawPostingsFilter.Application.Models;

public sealed record IncomingPostingPayload
{
    public string? Author { get; init; }

    public string? Title { get; init; }

    public decimal? PriceMin { get; init; }

    public decimal? PriceMax { get; init; }

    public required Currency Currency { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<PostingAttachment>? AttachedFiles { get; init; }
}