using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace SmartBank.BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// Caps consumer retries so one poison message can't loop forever.
/// Queues are declared with a DLX (see <see cref="RabbitMqSetup"/>) routing to
/// <c>{queue}.dead</c>: after <see cref="MaxRedeliveries"/> attempts the message
/// is NACKed without requeue and the broker dead-letters it for inspection
/// instead of redelivering it into an endless <c>LogError</c> loop.
/// </summary>
public static class PoisonMessagePolicy
{
    public const int MaxRedeliveries = 5;

    /// <summary>Redelivery count from the broker's x-death header (0 on first delivery).</summary>
    public static long DeliveryCount(BasicDeliverEventArgs ea)
    {
        try
        {
            if (ea.BasicProperties?.Headers is not null
                && ea.BasicProperties.Headers.TryGetValue("x-death", out var raw)
                && raw is IList<object?> deaths)
            {
                long max = 0;
                foreach (var death in deaths)
                {
                    if (death is IDictionary<string, object?> entry
                        && entry.TryGetValue("count", out var count))
                    {
                        max = Math.Max(max, Convert.ToInt64(count));
                    }
                }
                return max;
            }
        }
        catch
        {
            // Malformed headers must never break the consumer.
        }
        return ea.Redelivered ? 1 : 0;
    }

    /// <summary>
    /// NACKs with requeue while under budget; dead-letters (requeue:false, DLX routes
    /// to <c>.dead</c>) once exhausted. Returns true when dead-lettered.
    /// </summary>
    public static async Task<bool> NackOrDeadLetterAsync(
        IChannel channel,
        BasicDeliverEventArgs ea,
        ILogger logger,
        string what,
        CancellationToken ct)
    {
        var attempts = DeliveryCount(ea);
        if (attempts >= MaxRedeliveries)
        {
            logger.LogError(
                "Dead-lettering {What} after {Attempts} deliveries; inspect the .dead queue",
                what, attempts + 1);
            await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, ct);
            return true;
        }

        await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true, ct);
        return false;
    }

    public static string? ReadHeader(BasicDeliverEventArgs ea, string key)
    {
        if (ea.BasicProperties?.Headers is null
            || !ea.BasicProperties.Headers.TryGetValue(key, out var raw)
            || raw is null)
            return null;
        return raw switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string s => s,
            _ => raw.ToString(),
        };
    }
}
