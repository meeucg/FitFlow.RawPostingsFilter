using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RawPostingsFilter.Application.Abstractions;
using RawPostingsFilter.Persistence.Options;

namespace RawPostingsFilter.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is required.");

        services
            .AddOptions<DeduplicationOptions>()
            .Bind(configuration.GetSection(DeduplicationOptions.SectionName))
            .Validate(options => options.SimilarityThreshold is >= 0 and <= 1)
            .ValidateOnStart();

        services.AddDbContext<RawPostingsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IRawJobPostingStore, RawJobPostingStore>();
        services.AddScoped<RawPostingsDatabaseMigrator>();

        return services;
    }
}
