using Microsoft.EntityFrameworkCore;

namespace RawPostingsFilter.Persistence;

public sealed class RawPostingsDatabaseMigrator(RawPostingsDbContext dbContext)
{
    public Task MigrateAsync(CancellationToken cancellationToken)
    {
        return dbContext.Database.MigrateAsync(cancellationToken);
    }
}
