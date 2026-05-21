using RawPostingsFilter.Application.Abstractions;
using RawPostingsFilter.Application.Models;

namespace RawPostingsFilter.Application.Processing;

public sealed class JobPostingProcessor(
    IRawJobPostingStore store,
    IJobPostingNormalizer normalizer)
{
    public async Task<JobPostingProcessingResult> ProcessAsync(
        IncomingJobPosting posting,
        CancellationToken cancellationToken)
    {
        var result = await store.StoreIfNotDuplicateAsync(posting, cancellationToken);

        return result.Status switch
        {
            StorePostingStatus.Stored => await NormalizeStoredPostingAsync(
                posting,
                result.StoredPostingId!.Value,
                cancellationToken),
            StorePostingStatus.StoredSpamOrAdDuplicate => new JobPostingProcessingResult(
                JobPostingProcessingOutcome.StoredSpamOrAdDuplicate),
            StorePostingStatus.Duplicate => new JobPostingProcessingResult(JobPostingProcessingOutcome.Duplicate),
            StorePostingStatus.TooShort => new JobPostingProcessingResult(JobPostingProcessingOutcome.TooShort),
            _ => throw new InvalidOperationException($"Unsupported posting store status: {result.Status}")
        };
    }

    private async Task<JobPostingProcessingResult> NormalizeStoredPostingAsync(
        IncomingJobPosting posting,
        long postingId,
        CancellationToken cancellationToken)
    {
        var normalizationResult = await normalizer.NormalizeAsync(posting, cancellationToken);
        if (!normalizationResult.AiSucceeded)
        {
            return new JobPostingProcessingResult(JobPostingProcessingOutcome.StoredNormalizationFailed);
        }

        if (!normalizationResult.IsSpamOrAd)
        {
            return new JobPostingProcessingResult(
                JobPostingProcessingOutcome.StoredAndNormalized,
                normalizationResult.Posting);
        }

        await store.MarkAsSpamOrAdAsync(postingId, cancellationToken);
        return new JobPostingProcessingResult(JobPostingProcessingOutcome.StoredSpamOrAd);
    }
}
