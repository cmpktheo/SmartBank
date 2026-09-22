using System.Text;
using RabbitMQ.Client;
using SmartBank.BuildingBlocks.EventBus;

namespace SmartBank.BuildingBlocks.Infrastructure.Messaging;

public sealed class RabbitMqEventPublisher : IEventPublisher
{
    private readonly IConnection _connection;
    private readonly string _exchange;

    public RabbitMqEventPublisher(IConnection connection, string exchange)
    {
        _connection = connection;
        _exchange = exchange;
    }

    public async Task PublishAsync(string type, string payload, Guid correlationId, CancellationToken ct = default)
    {
        var channel = await _connection.CreateChannelAsync(cancellationToken: ct);
        await using (channel.ConfigureAwait(false))
        {
            await channel.ExchangeDeclareAsync(_exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
            var body = Encoding.UTF8.GetBytes(payload);
            var props = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Headers = new Dictionary<string, object?> { ["message_type"] = type, ["correlation_id"] = correlationId.ToString() }
            };
            await channel.BasicPublishAsync(_exchange, RoutingKeyFor(type), mandatory: false, basicProperties: props, body: body, cancellationToken: ct);
        }
    }

    private static string RoutingKeyFor(string type) => type switch
    {
        "AccountOpenedIntegrationEvent" => "accounts.opened",
        "AccountFrozenIntegrationEvent" => "accounts.frozen",
        "MoneyTransferredIntegrationEvent" => "ledger.transferred",
        "CardIssuedIntegrationEvent" => "cards.issued",
        "CardFrozenIntegrationEvent" => "cards.frozen",
        "CardUnfrozenIntegrationEvent" => "cards.unfrozen",
        "UserRegisteredIntegrationEvent" => "identity.user-registered",
        _ => "unknown"
    };
}
