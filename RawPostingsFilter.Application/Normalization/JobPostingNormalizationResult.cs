using RawPostingsFilter.Application.Models;

namespace RawPostingsFilter.Application.Normalization;

public sealed record JobPostingNormalizationResult(
    OutgoingJobPosting Posting,
    bool AiSucceeded,
    bool IsSpamOrAd,
    string? ErrorMessage);
