using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RawPostingsFilter.Application;
using RawPostingsFilter.HostedServices;
using RawPostingsFilter.Infrastructure;
using RawPostingsFilter.Persistence;

var builderSettings = new HostApplicationBuilderSettings
{
    Args = args
};

if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"))
    && File.Exists(Path.Combine(AppContext.BaseDirectory, "appsettings.json")))
{
    builderSettings.ContentRootPath = AppContext.BaseDirectory;
}

var builder = Host.CreateApplicationBuilder(builderSettings);

builder.Services
    .AddApplication()
    .AddPersistence(builder.Configuration)
    .AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<RabbitMqProcessingHostedService>();

var host = builder.Build();

await host.RunAsync();
