namespace RawPostingsFilter.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 5672;

    public string Username { get; init; } = "guest";

    public string Password { get; init; } = "guest";

    public string IncomingExchange { get; init; } = "raw-postings.incoming";

    public string IncomingQueue { get; init; } = "raw-postings-filter.incoming";

    public string IncomingRoutingKey { get; init; } = "job-posting.raw";

    public string OutgoingExchange { get; init; } = "raw-postings-filter.outgoing.mock";

    public string OutgoingQueue { get; init; } = "raw-postings-filter.outgoing.mock.queue";

    public string OutgoingRoutingKey { get; init; } = "job-posting.normalized";

    public string DeadLetterExchange { get; init; } = "raw-postings.dead-letter";

    public string DeadLetterQueue { get; init; } = "raw-postings-filter.dead-letter";

    public int ConsumerCount { get; init; } = 5;

    public ushort PrefetchCount { get; init; } = 1;
}
