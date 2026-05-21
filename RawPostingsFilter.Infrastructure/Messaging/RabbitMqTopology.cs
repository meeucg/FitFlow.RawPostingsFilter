using RabbitMQ.Client;

namespace RawPostingsFilter.Infrastructure.Messaging;

public static class RabbitMqTopology
{
    public static async Task DeclareAsync(
        IChannel channel,
        RabbitMqOptions options,
        CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            options.IncomingExchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            options.OutgoingExchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            options.DeadLetterExchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            options.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            options.DeadLetterQueue,
            options.DeadLetterExchange,
            options.IncomingRoutingKey,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            options.IncomingQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = options.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = options.IncomingRoutingKey
            },
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            options.IncomingQueue,
            options.IncomingExchange,
            options.IncomingRoutingKey,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            options.OutgoingQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            options.OutgoingQueue,
            options.OutgoingExchange,
            options.OutgoingRoutingKey,
            cancellationToken: cancellationToken);
    }
}
