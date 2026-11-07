using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderService.Interfaces;
using SharedKernel.Events;
using SharedKernel.Messaging;
using SharedKernel.Middleware;

namespace OrderService.Messaging;

/// <summary>
/// Saga Step 3b (Compensation Path) — Consumes InventoryRejected from InventoryService.
/// Updates order status to Cancelled, then publishes:
///   - OrderCancelledNotify → NotificationService (tells customer)
///   - OrderCancelledInventory → InventoryService (restores any partially-deducted stock)
/// </summary>
public class InventoryRejectedConsumer : RabbitMqConsumerBase<InventoryRejectedEvent>
{
    private readonly IMessagePublisher _publisher;

    public InventoryRejectedConsumer(
        string hostName,
        IMessagePublisher publisher,
        IServiceScopeFactory scopeFactory,
        ILogger<InventoryRejectedConsumer> logger)
        : base(hostName, QueueNames.InventoryRejected, scopeFactory, logger)
    {
        _publisher = publisher;
    }

    protected override async Task HandleAsync(
        InventoryRejectedEvent message,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        CorrelationContext.CorrelationId = message.CorrelationId;
        var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

        var order = await repo.GetByIdAsync(message.OrderId);
        if (order == null)
        {
            Logger.LogWarning("InventoryRejected received for unknown OrderId {OrderId}", message.OrderId);
            return;
        }

        order.Status = "Cancelled";
        await repo.UpdateAsync(order);

        Logger.LogWarning(
            "ORDER CANCELLED: OrderId {OrderId} rejected by InventoryService — {Reason} [CorrelationId: {CorrelationId}]",
            order.Id, message.Reason, message.CorrelationId);

        var itemLines = order.OrderItems.Select(i => new OrderItemLine
        {
            ProductId = i.ProductId,
            Quantity = i.Quantity
        }).ToList();

        var cancelledEvent = new OrderCancelledEvent
        {
            CorrelationId = message.CorrelationId,
            OrderId = order.Id,
            UserId = order.UserId,
            Reason = message.Reason,
            Items = itemLines
        };

        // Notify customer AND trigger compensation (stock restore) in parallel queues
        await _publisher.PublishAsync(QueueNames.OrderCancelledNotify, cancelledEvent);
        await _publisher.PublishAsync(QueueNames.OrderCancelledInventory, cancelledEvent);
    }
}
