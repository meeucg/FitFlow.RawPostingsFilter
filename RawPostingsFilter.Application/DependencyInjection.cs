using Microsoft.Extensions.DependencyInjection;
using RawPostingsFilter.Application.Processing;

namespace RawPostingsFilter.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<JobPostingProcessor>();

        return services;
    }
}
