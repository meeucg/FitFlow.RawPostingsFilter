namespace RawPostingsFilter.Persistence.Entities;

public sealed class RawDuplicateCandidate
{
    public long Id { get; set; }

    public double Similarity { get; set; }
}
