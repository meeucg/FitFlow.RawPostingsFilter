using Microsoft.EntityFrameworkCore;
using RawPostingsFilter.Persistence;

namespace RawPostingsFilter.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    public string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
        ?? "Host=localhost;Port=5432;Database=raw_postings_filter;Username=postgres;Password=postgres;GSS Encryption Mode=Disable";

    public RawPostingsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RawPostingsDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new RawPostingsDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using var dbContext = CreateDbContext();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }

    public async Task ResetAsync()
    {
        await using var dbContext = CreateDbContext();

        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE raw_job_postings RESTART IDENTITY");
    }

    public async Task DisposeAsync()
    {
        await using var dbContext = CreateDbContext();

        await dbContext.Database.EnsureDeletedAsync();
    }
}
