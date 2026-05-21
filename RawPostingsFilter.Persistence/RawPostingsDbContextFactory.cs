using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RawPostingsFilter.Persistence;

public sealed class RawPostingsDbContextFactory : IDesignTimeDbContextFactory<RawPostingsDbContext>
{
    public RawPostingsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=raw_postings_filter;Username=postgres;Password=postgres;GSS Encryption Mode=Disable";

        var options = new DbContextOptionsBuilder<RawPostingsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new RawPostingsDbContext(options);
    }
}
