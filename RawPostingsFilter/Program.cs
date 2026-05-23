using RawPostingsFilter.Application;
using RawPostingsFilter.HostedServices;
using RawPostingsFilter.Infrastructure;
using RawPostingsFilter.Observability;
using RawPostingsFilter.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddPersistence(builder.Configuration)
    .AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<RabbitMqProcessingHostedService>();
builder.Services.AddFitFlowObservability();

var app = builder.Build();

app.UseFitFlowObservability();

await app.RunAsync();
