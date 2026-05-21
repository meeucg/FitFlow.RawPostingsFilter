using RawPostingsFilter.Application.Models;
using RawPostingsFilter.Application.Normalization;

namespace RawPostingsFilter.Application.Abstractions;

public interface IJobPostingNormalizer
{
    Task<JobPostingNormalizationResult> NormalizeAsync(
        IncomingJobPosting posting,
        CancellationToken cancellationToken);
}
