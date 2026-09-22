using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using SmartBank.BuildingBlocks.EventBus;

namespace SmartBank.BuildingBlocks.Infrastructure.Messaging;

public static class RabbitMqSetup
{
    /// <summary>Shared RabbitMQ connection + topic publisher. Skips registration when connection string is missing (tests).</summary>
    public static IServiceCollection AddRabbitMqEventPublisher(this IServiceCollection services, IConfiguration config, string exchange)
    {
        var cs = config.GetConnectionString("RabbitMQ") ?? config["RabbitMQ:ConnectionString"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cs))
            return services; // tests / local without broker: OutboxDispatcher no-ops via GetService null-check
        var uri = cs;
        services.AddSingleton<IConnection>(_ =>
        {
            var factory = new ConnectionFactory { Uri = new Uri(uri), AutomaticRecoveryEnabled = true };
            // CreateConnectionAsync is truly async in v7; block once at startup (singleton).
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });
        services.AddSingleton<IEventPublisher>(sp =>
            new RabbitMqEventPublisher(sp.GetRequiredService<IConnection>(), exchange));
        return services;
    }

    /// <summary>Declare topic exchange + durable queue + binding. Idempotent; safe to call from every consumer at startup.</summary>
    public static async Task DeclareTopologyAsync(
        IConnection connection,
        string exchange,
        string routingKey,
        string queue,
        CancellationToken ct,
        ILogger? logger = null)
    {
        var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await using (channel.ConfigureAwait(false))
        {
            await channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
            // Durable classic queue with DLX for poison messages (background checks must never block settlement).
            var args = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = exchange,
                ["x-dead-letter-routing-key"] = routingKey + ".dead",
            };
            await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: args, cancellationToken: ct);
            await channel.QueueBindAsync(queue, exchange, routingKey, cancellationToken: ct);
            // Ensure dead-letter queue exists so poison messages are retained, not dropped.
            await channel.QueueDeclareAsync(queue + ".dead", durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
            await channel.QueueBindAsync(queue + ".dead", exchange, routingKey + ".dead", cancellationToken: ct);
            logger?.LogInformation("RabbitMQ topology ready: {Exchange}/{RoutingKey} -> {Queue}", exchange, routingKey, queue);
        }
    }
}
