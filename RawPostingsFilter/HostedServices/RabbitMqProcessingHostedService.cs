using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RawPostingsFilter.Application.Processing;
using RawPostingsFilter.Infrastructure.Messaging;
using RawPostingsFilter.Infrastructure.Serialization;
using RawPostingsFilter.Persistence;

namespace RawPostingsFilter.HostedServices;

public sealed class RabbitMqProcessingHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    JsonSerializerOptions jsonSerializerOptions,
    ILogger<RabbitMqProcessingHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ApplyMigrationsAsync(stoppingToken);

        var rabbitMqOptions = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = rabbitMqOptions.Host,
            Port = rabbitMqOptions.Port,
            UserName = rabbitMqOptions.Username,
            Password = rabbitMqOptions.Password,
            AutomaticRecoveryEnabled = true
        };

        await using var connection = await ConnectWithRetryAsync(factory, stoppingToken);
        var channels = new List<IChannel>();

        try
        {
            await using var topologyChannel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
            await RabbitMqTopology.DeclareAsync(topologyChannel, rabbitMqOptions, stoppingToken);

            for (var index = 0; index < rabbitMqOptions.ConsumerCount; index++)
            {
                var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                channels.Add(channel);
                await channel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: rabbitMqOptions.PrefetchCount,
                    global: false,
                    cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                var consumerNumber = index + 1;
                consumer.ReceivedAsync += async (_, eventArgs) =>
                {
                    await ProcessDeliveryAsync(
                        channel,
                        rabbitMqOptions,
                        eventArgs,
                        consumerNumber,
                        stoppingToken);
                };

                await channel.BasicConsumeAsync(
                    queue: rabbitMqOptions.IncomingQueue,
                    autoAck: false,
                    consumerTag: $"raw-postings-filter-{consumerNumber}",
                    consumer: consumer,
                    cancellationToken: stoppingToken);
            }

            logger.LogInformation(
                "RabbitMQ processing started. Consumers={ConsumerCount}, IncomingQueue={IncomingQueue}, OutgoingExchange={OutgoingExchange}",
                rabbitMqOptions.ConsumerCount,
                rabbitMqOptions.IncomingQueue,
                rabbitMqOptions.OutgoingExchange);

            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("RabbitMQ processing is stopping.");
        }
        finally
        {
            foreach (var channel in channels)
            {
                await channel.DisposeAsync();
            }
        }
    }

    private async Task ProcessDeliveryAsync(
        IChannel channel,
        RabbitMqOptions rabbitMqOptions,
        BasicDeliverEventArgs eventArgs,
        int consumerNumber,
        CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetString(eventArgs.Body.Span);

        if (!IncomingJobPostingJsonParser.TryParse(
                body,
                jsonSerializerOptions,
                out var posting,
                out var error))
        {
            logger.LogWarning(
                "Rejected malformed RabbitMQ posting. Consumer={ConsumerNumber}, DeliveryTag={DeliveryTag}, Error={Error}",
                consumerNumber,
                eventArgs.DeliveryTag,
                error);

            await channel.BasicNackAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                requeue: false,
                cancellationToken);
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<JobPostingProcessor>();
            var result = await processor.ProcessAsync(posting, cancellationToken);

            if (result is { Normalized: true, Posting: not null })
            {
                var outgoingBody = JsonSerializer.SerializeToUtf8Bytes(
                    result.Posting,
                    jsonSerializerOptions);

                await channel.BasicPublishAsync(
                    exchange: rabbitMqOptions.OutgoingExchange,
                    routingKey: rabbitMqOptions.OutgoingRoutingKey,
                    mandatory: false,
                    basicProperties: CreateJsonProperties(),
                    body: outgoingBody,
                    cancellationToken: cancellationToken);

                logger.LogInformation(
                    "Processed and published posting. Consumer={ConsumerNumber}, Source={Source}, Url={Url}, Outcome={Outcome}, OutgoingExchange={OutgoingExchange}, RoutingKey={RoutingKey}",
                    consumerNumber,
                    posting.Source,
                    posting.Url,
                    result.Outcome,
                    rabbitMqOptions.OutgoingExchange,
                    rabbitMqOptions.OutgoingRoutingKey);
            }
            else
            {
                logger.LogInformation(
                    "Processed posting without downstream publish. Consumer={ConsumerNumber}, Source={Source}, Url={Url}, Outcome={Outcome}",
                    consumerNumber,
                    posting.Source,
                    posting.Url,
                    result.Outcome);
            }

            await channel.BasicAckAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Processing cancelled for posting. Consumer={ConsumerNumber}, Source={Source}, Url={Url}",
                consumerNumber,
                posting.Source,
                posting.Url);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unexpected RabbitMQ processing failure. Consumer={ConsumerNumber}, Source={Source}, Url={Url}, DeliveryTag={DeliveryTag}",
                consumerNumber,
                posting.Source,
                posting.Url,
                eventArgs.DeliveryTag);

            await channel.BasicNackAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                requeue: false,
                cancellationToken);
        }
    }

    private async Task<IConnection> ConnectWithRetryAsync(
        ConnectionFactory factory,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                return await factory.CreateConnectionAsync(cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    exception,
                    "RabbitMQ connection failed. Retrying in 5 seconds. Host={Host}, Port={Port}",
                    factory.HostName,
                    factory.Port);

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task ApplyMigrationsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var migrator = scope.ServiceProvider.GetRequiredService<RawPostingsDatabaseMigrator>();

        await migrator.MigrateAsync(cancellationToken);
    }

    private static BasicProperties CreateJsonProperties()
    {
        return new BasicProperties
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            Persistent = true,
            MessageId = Guid.NewGuid().ToString("N"),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };
    }
}
