using Microsoft.EntityFrameworkCore;
using RawPostingsFilter.Persistence.Entities;

namespace RawPostingsFilter.Persistence;

public sealed class RawPostingsDbContext(DbContextOptions<RawPostingsDbContext> options) : DbContext(options)
{
    public DbSet<RawJobPosting> RawJobPostings => Set<RawJobPosting>();

    public DbSet<RawDuplicateCandidate> RawDuplicateCandidates => Set<RawDuplicateCandidate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.Entity<RawJobPosting>(entity =>
        {
            entity.ToTable("raw_job_postings");

            entity.HasKey(posting => posting.Id);

            entity.Property(posting => posting.Id)
                .HasColumnName("id");

            entity.Property(posting => posting.ReceivedAt)
                .HasColumnName("received_at")
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            entity.Property(posting => posting.Source)
                .HasColumnName("source")
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(posting => posting.PostedAt)
                .HasColumnName("posted_at")
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            entity.Property(posting => posting.Url)
                .HasColumnName("url")
                .HasMaxLength(2048)
                .IsRequired();

            entity.Property(posting => posting.RawText)
                .HasColumnName("raw_text");

            entity.Property(posting => posting.IsNormalized)
                .HasColumnName("is_normalized")
                .IsRequired();

            entity.Property(posting => posting.IsSpamOrAd)
                .HasColumnName("is_spam_or_ad")
                .HasDefaultValue(false)
                .IsRequired();

            entity.Property(posting => posting.IncomingPosting)
                .HasColumnName("incoming_posting")
                .HasColumnType("jsonb")
                .IsRequired();

            entity.HasIndex(posting => posting.RawText)
                .HasDatabaseName("ix_raw_job_postings_raw_text_trgm")
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops")
                .HasFilter("\"raw_text\" IS NOT NULL AND \"is_normalized\" = false");
        });

        modelBuilder.Entity<RawDuplicateCandidate>(entity =>
        {
            entity.HasNoKey();
            entity.ToView(null);

            entity.Property(candidate => candidate.Id)
                .HasColumnName("id");

            entity.Property(candidate => candidate.Similarity)
                .HasColumnName("similarity");
        });
    }
}
