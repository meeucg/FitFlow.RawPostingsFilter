using System.ComponentModel.DataAnnotations;

namespace RawPostingsFilter.Persistence.Options;

public sealed class DeduplicationOptions
{
    public const string SectionName = "Deduplication";

    [Range(0, 1)]
    public double SimilarityThreshold { get; init; } = 0.80;
}
