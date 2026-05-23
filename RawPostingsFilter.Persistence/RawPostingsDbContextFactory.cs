using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RawPostingsFilter.Persistence;

public sealed class RawPostingsDbContextFactory : IDesignTimeDbContextFactory<RawPostingsDbContext>
{
    public RawPostingsDbContext CreateDbContext(string[] args)
    {
        var connectionString = ReadConnectionString("RawPostingsFilter")
            ?? "Host=localhost;Port=5432;Database=raw_postings_filter;Username=postgres;Password=postgres;GSS Encryption Mode=Disable";

        var options = new DbContextOptionsBuilder<RawPostingsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new RawPostingsDbContext(options);
    }

    private static string? ReadConnectionString(string startupProjectName)
    {
        foreach (var path in EnumerateAppSettingsFiles(startupProjectName))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings)
                && connectionStrings.TryGetProperty("Postgres", out var postgres)
                && postgres.GetString() is { Length: > 0 } connectionString)
            {
                return connectionString;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateAppSettingsFiles(string startupProjectName)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var candidateDirectories = new[]
        {
            currentDirectory,
            Path.Combine(currentDirectory, startupProjectName),
            Path.Combine(currentDirectory, "..", startupProjectName)
        };

        foreach (var directory in candidateDirectories)
        {
            yield return Path.GetFullPath(Path.Combine(directory, "appsettings.Development.json"));
            yield return Path.GetFullPath(Path.Combine(directory, "appsettings.json"));
        }
    }
}
