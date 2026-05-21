using AIServices.ServiceBuilders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RawPostingsFilter.Application.Abstractions;
using RawPostingsFilter.Infrastructure.Ai;
using RawPostingsFilter.Infrastructure.Messaging;
using RawPostingsFilter.Infrastructure.Serialization;

namespace RawPostingsFilter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(JsonSerializerOptionsFactory.Create());
        services.AddSingleton(TimeProvider.System);
        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(options => options.Port > 0)
            .Validate(options => options.ConsumerCount is > 0 and <= 5)
            .Validate(options => options.PrefetchCount > 0)
            .ValidateOnStart();
        services.AddAIServices(
            textAIOptionsSection: configuration.GetSection("TextAI"),
            textAIModelsOptionsSection: configuration.GetSection("AIModels"),
            configureAIJsonOptions: AIJsonOptionsFactory.Configure);
        services.AddSingleton<IJobPostingNormalizer, AiJobPostingNormalizer>();

        return services;
    }
}
