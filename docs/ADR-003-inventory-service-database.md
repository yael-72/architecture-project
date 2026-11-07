# ADR-003 — InventoryService: SQL Server (Relational Database)

**Status:** Accepted  
**Date:** 2026-07-05  
**Service:** InventoryService

---

## Context

`InventoryService` manages the quantity of each product in the warehouse (`StockQuantity`). Two critical operations happen under concurrent load:

- **Deduct stock** — called when InventoryService processes an `OrderPlaced` event.
- **Restore stock** — called when InventoryService processes an `OrderCancelled` event (compensation).

If two orders for the same product are processed simultaneously, the stock deduction must be serialised correctly. An error here means either **overselling** (selling stock that doesn't exist) or **underselling** (rejecting orders for in-stock products).

---

## Decision

Use **SQL Server 2022** for `InventoryService`.

---

## Rationale

### Concurrency safety requires ACID

The deduction logic is:

```sql
-- Pseudo-code
SELECT stock FROM InventoryItems WHERE ProductId = @id  -- read
IF stock >= requested THEN
  UPDATE InventoryItems SET stock = stock - requested    -- write
```

This read-modify-write sequence is a classic **lost update** scenario. SQL Server serialises it safely via row-level locking within a transaction. With a BASE store (e.g. DynamoDB default, Cassandra) you would need conditional writes and explicit retry logic to avoid overselling.

### ACID vocabulary

| Property | InventoryService need |
|---|---|
| **Atomicity** | Deduct-and-save must not be interrupted halfway |
| **Consistency** | `StockQuantity` must never go negative (CHECK constraint) |
| **Isolation** | Two concurrent saga events for the same product must not race each other |
| **Durability** | Committed deductions survive restarts — we cannot "lose" a stock deduction |

### CAP theorem position

**CP** — favours Consistency over Availability. If the database is temporarily unavailable:
- The `InventoryReserved` event is not published.
- `RabbitMqConsumerBase` will **nack** the message (no requeue by default — dead-letter queue).
- A brief window of unavailability is preferable to double-deducting stock.

### Why not a key-value / wide-column store

A key-value store (Redis) could theoretically hold stock counts using atomic `DECR`. However:
- Redis does not support cross-key transactions without Lua scripts.
- A catalog product may have multiple warehouse locations in future (requiring a table structure).
- SQL Server integrates naturally with EF Core migrations already used by the project.

### Idempotency note

At-least-once delivery from RabbitMQ means the same `OrderPlaced` message may arrive twice. Idempotency is handled by checking the order status before deducting: if the order is already `Confirmed` or `Cancelled`, the consumer skips the operation.

---

## Consequences

- `inventorydb` is completely isolated from `orderdb` and `catalogdb` — no cross-service joins.
- Future enhancement: add an `OutboxPattern` table to guarantee exactly-once publishing even if the service crashes between deducting stock and publishing the confirmation event.
