using InventoryService.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Events;
using SharedKernel.Messaging;
using SharedKernel.Middleware;

namespace InventoryService.Messaging;

/// <summary>
/// Saga Compensation — Consumes OrderCancelled from OrderService.
/// Restores stock for every item that was previously deducted.
/// This is the compensating transaction in the choreography saga.
/// </summary>
public class InventoryOrderCancelledConsumer : RabbitMqConsumerBase<OrderCancelledEvent>
{
    public InventoryOrderCancelledConsumer(
        string hostName,
        IServiceScopeFactory scopeFactory,
        ILogger<InventoryOrderCancelledConsumer> logger)
        : base(hostName, QueueNames.OrderCancelledInventory, scopeFactory, logger)
    {
    }

    protected override async Task HandleAsync(
        OrderCancelledEvent message,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        CorrelationContext.CorrelationId = message.CorrelationId;
        var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();

        Logger.LogInformation(
            "COMPENSATION: Restoring stock for cancelled OrderId {OrderId} (reason: {Reason}) [CorrelationId: {CorrelationId}]",
            message.OrderId, message.Reason, message.CorrelationId);

        foreach (var item in message.Items)
        {
            var restored = await inventoryService.RestoreStockAsync(item.ProductId, item.Quantity);
            if (restored)
                Logger.LogInformation("Restored {Qty} units for ProductId {ProductId}", item.Quantity, item.ProductId);
            else
                Logger.LogWarning("Could not restore stock for ProductId {ProductId} — item not found", item.ProductId);
        }
    }
}
