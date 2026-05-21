namespace RawPostingsFilter.Application.Processing;

public enum JobPostingProcessingOutcome
{
    StoredAndNormalized,
    StoredSpamOrAd,
    StoredSpamOrAdDuplicate,
    StoredNormalizationFailed,
    Duplicate,
    TooShort
}
