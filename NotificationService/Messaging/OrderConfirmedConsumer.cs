using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Events;
using SharedKernel.Messaging;
using SharedKernel.Middleware;

namespace NotificationService.Messaging;

/// <summary>
/// Saga Step 5a — Consumes OrderConfirmed and notifies the customer.
/// In production this would send an email/SMS; here it writes a structured log entry.
/// </summary>
public class OrderConfirmedConsumer : RabbitMqConsumerBase<OrderConfirmedEvent>
{
    public OrderConfirmedConsumer(
        string hostName,
        IServiceScopeFactory scopeFactory,
        ILogger<OrderConfirmedConsumer> logger)
        : base(hostName, QueueNames.OrderConfirmed, scopeFactory, logger)
    {
    }

    protected override async Task HandleAsync(
        OrderConfirmedEvent message,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        CorrelationContext.CorrelationId = message.CorrelationId;

        var notificationStore = scope.ServiceProvider.GetRequiredService<Services.NotificationStore>();
        var notification = new Data.NotificationDocument
        {
            OrderId = message.OrderId,
            EventType = "OrderConfirmed",
            Message = $"Order confirmed for user {message.UserId} with total {message.TotalAmount:C}.",
            OccurredAt = DateTime.UtcNow,
            Payload = new { message.UserId, message.TotalAmount, message.CorrelationId }
        };

        await notificationStore.SaveAsync(notification);

        Logger.LogInformation(
            "NOTIFICATION SENT → Order #{OrderId} CONFIRMED for UserId {UserId}. " +
            "Total: {TotalAmount:C}. [CorrelationId: {CorrelationId}]",
            message.OrderId, message.UserId, message.TotalAmount, message.CorrelationId);
    }
}
