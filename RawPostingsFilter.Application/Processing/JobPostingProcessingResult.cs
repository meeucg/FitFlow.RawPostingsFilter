using RawPostingsFilter.Application.Models;

namespace RawPostingsFilter.Application.Processing;

public sealed record JobPostingProcessingResult(
    JobPostingProcessingOutcome Outcome,
    OutgoingJobPosting? Posting = null)
{
    public bool Stored => Outcome is
        JobPostingProcessingOutcome.StoredAndNormalized
        or JobPostingProcessingOutcome.StoredSpamOrAd
        or JobPostingProcessingOutcome.StoredSpamOrAdDuplicate
        or JobPostingProcessingOutcome.StoredNormalizationFailed;

    public bool Normalized => Outcome == JobPostingProcessingOutcome.StoredAndNormalized;

    public bool SpamOrAd => Outcome is
        JobPostingProcessingOutcome.StoredSpamOrAd
        or JobPostingProcessingOutcome.StoredSpamOrAdDuplicate;

    public bool SpamOrAdDuplicate => Outcome == JobPostingProcessingOutcome.StoredSpamOrAdDuplicate;

    public bool NormalizationFailed => Outcome == JobPostingProcessingOutcome.StoredNormalizationFailed;

    public bool Duplicate => Outcome == JobPostingProcessingOutcome.Duplicate;

    public bool TooShort => Outcome == JobPostingProcessingOutcome.TooShort;
}
