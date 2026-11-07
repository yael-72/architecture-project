using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;
using SharedKernel.Middleware;

namespace SharedKernel.Messaging;

/// <summary>
/// Generic base BackgroundService for RabbitMQ consumers.
/// - Connects to a named queue with retry on disconnect.
/// - Extracts X-Correlation-Id from message properties and pushes it into Serilog LogContext.
/// - Acknowledges the message only after HandleAsync completes successfully.
/// - Dead-letters (nacks without requeue) on unhandled exceptions so the queue doesn't loop forever.
/// </summary>
public abstract class RabbitMqConsumerBase<T> : BackgroundService where T : class
{
    protected readonly ILogger Logger;
    protected readonly IServiceScopeFactory ScopeFactory;
    private readonly string _hostName;
    private readonly string _queueName;

    protected RabbitMqConsumerBase(
        string hostName,
        string queueName,
        IServiceScopeFactory scopeFactory,
        ILogger logger)
    {
        _hostName = hostName;
        _queueName = queueName;
        ScopeFactory = scopeFactory;
        Logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumingAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                Logger.LogWarning(ex,
                    "Consumer for queue '{Queue}' lost connection. Reconnecting in 5 s...", _queueName);
                await Task.Delay(5_000, stoppingToken);
            }
        }
    }

    private Task StartConsumingAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _hostName,
            DispatchConsumersAsync = true
        };

        var connection = factory.CreateConnection();
        var channel = connection.CreateModel();

        channel.QueueDeclare(_queueName, durable: true, exclusive: false, autoDelete: false);
        channel.BasicQos(0, 1, false); // one message at a time per consumer

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, ea) =>
        {
            // Restore (or generate) the correlation ID that was embedded in the message header
            Guid correlationId = Guid.NewGuid();
            if (!string.IsNullOrEmpty(ea.BasicProperties.CorrelationId) &&
                Guid.TryParse(ea.BasicProperties.CorrelationId, out var parsed))
            {
                correlationId = parsed;
            }

            CorrelationContext.CorrelationId = correlationId;

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var message = JsonSerializer.Deserialize<T>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (message != null)
                    {
                        using var scope = ScopeFactory.CreateScope();
                        await HandleAsync(message, scope, stoppingToken);
                        channel.BasicAck(ea.DeliveryTag, false);

                        Logger.LogInformation(
                            "Processed {EventType} from queue '{Queue}' [CorrelationId: {CorrelationId}]",
                            typeof(T).Name, _queueName, correlationId);
                    }
                    else
                    {
                        Logger.LogWarning("Null message deserialized from queue '{Queue}'", _queueName);
                        channel.BasicNack(ea.DeliveryTag, false, false);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex,
                        "Error processing message from queue '{Queue}' [CorrelationId: {CorrelationId}]",
                        _queueName, correlationId);
                    channel.BasicNack(ea.DeliveryTag, false, false); // dead-letter, no requeue
                }
            }
        };

        channel.BasicConsume(_queueName, autoAck: false, consumer: consumer);
        Logger.LogInformation("Consumer started for queue '{Queue}'", _queueName);

        // Keep alive until token cancelled or broker disconnects
        var tcs = new TaskCompletionSource();
        stoppingToken.Register(() => tcs.TrySetResult());
        connection.ConnectionShutdown += (_, _) => tcs.TrySetResult();

        return tcs.Task;
    }

    /// <summary>Override to handle the deserialized message.</summary>
    protected abstract Task HandleAsync(T message, IServiceScope scope, CancellationToken cancellationToken);
}
