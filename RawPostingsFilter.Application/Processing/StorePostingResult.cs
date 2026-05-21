namespace RawPostingsFilter.Application.Processing;

public enum StorePostingStatus
{
    Stored,
    StoredSpamOrAdDuplicate,
    Duplicate,
    TooShort
}

public sealed record StorePostingResult
{
    private StorePostingResult(
        StorePostingStatus status,
        long? storedPostingId = null,
        long? duplicatePostingId = null,
        double? similarity = null,
        int? wordCount = null,
        bool isSpamOrAd = false)
    {
        Status = status;
        StoredPostingId = storedPostingId;
        DuplicatePostingId = duplicatePostingId;
        Similarity = similarity;
        WordCount = wordCount;
        IsSpamOrAd = isSpamOrAd;
    }

    public StorePostingStatus Status { get; }

    public bool Stored => Status is StorePostingStatus.Stored or StorePostingStatus.StoredSpamOrAdDuplicate;

    public bool ShouldNormalizeWithAi => Status == StorePostingStatus.Stored;

    public long? StoredPostingId { get; }

    public long? DuplicatePostingId { get; }

    public double? Similarity { get; }

    public int? WordCount { get; }

    public bool IsSpamOrAd { get; }

    public static StorePostingResult StoredResult(long storedPostingId)
    {
        return new StorePostingResult(StorePostingStatus.Stored, storedPostingId: storedPostingId);
    }

    public static StorePostingResult StoredSpamOrAdDuplicateResult(
        long storedPostingId,
        long duplicatePostingId,
        double similarity)
    {
        return new StorePostingResult(
            StorePostingStatus.StoredSpamOrAdDuplicate,
            storedPostingId: storedPostingId,
            duplicatePostingId: duplicatePostingId,
            similarity: similarity,
            isSpamOrAd: true);
    }

    public static StorePostingResult DuplicateResult(long duplicatePostingId, double similarity)
    {
        return new StorePostingResult(
            StorePostingStatus.Duplicate,
            duplicatePostingId: duplicatePostingId,
            similarity: similarity);
    }

    public static StorePostingResult TooShortResult(int wordCount)
    {
        return new StorePostingResult(StorePostingStatus.TooShort, wordCount: wordCount);
    }
}
