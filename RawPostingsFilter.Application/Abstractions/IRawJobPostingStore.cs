using RawPostingsFilter.Application.Models;
using RawPostingsFilter.Application.Processing;

namespace RawPostingsFilter.Application.Abstractions;

public interface IRawJobPostingStore
{
    Task<StorePostingResult> StoreIfNotDuplicateAsync(
        IncomingJobPosting posting,
        CancellationToken cancellationToken);

    Task MarkAsSpamOrAdAsync(
        long postingId,
        CancellationToken cancellationToken);
}
