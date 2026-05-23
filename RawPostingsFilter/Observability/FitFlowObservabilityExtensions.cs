using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Prometheus;

namespace RawPostingsFilter.Observability;

internal static class FitFlowObservabilityExtensions
{
    public static IServiceCollection AddFitFlowObservability(this IServiceCollection services)
    {
        services.AddHealthChecks();
        return services;
    }

    public static WebApplication UseFitFlowObservability(this WebApplication app)
    {
        app.UseHttpMetrics();
        app.MapMetrics();
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready");
        return app;
    }
}
