# ADR-005 — RabbitMQ: Asynchronous Message Broker

**Status:** Accepted  
**Date:** 2026-07-05  
**Services:** OrderService, InventoryService, NotificationService

---

## Context

The original `OrderService` used synchronous HTTP calls to:
1. Validate product prices (ProductCatalogService)
2. Deduct stock (ProductCatalogService internal endpoint)

Problems with synchronous coupling:
- **Temporal coupling** — OrderService fails if ProductCatalogService is slow or down.
- **No compensation** — if the stock deduction succeeds but the order commit fails, there is no rollback mechanism.
- **Scalability** — every order ties up an HTTP thread in both services for the duration.

The course requires replacing this with **async messaging and an Order Saga**.

---

## Decision

Use **RabbitMQ 3.13** as the message broker. Implement a **choreography-based saga** using durable queues. Stay with the class default (RabbitMQ) rather than substituting Kafka.

---

## Why RabbitMQ (not Kafka)

| Concern | RabbitMQ | Kafka |
|---|---|---|
| **Message model** | Queue-based push: broker delivers to consumer | Log-based pull: consumers read from an offset |
| **Message retention** | Deleted on ack (or dead-lettered) | Retained for configurable period (days/weeks) |
| **Ordering** | Per-queue FIFO | Per-partition strict ordering |
| **Throughput** | ~50k–100k msg/s per node | ~1M+ msg/s per partition |
| **Complexity** | Low — single broker, no ZooKeeper/KRaft | Higher — brokers + topic/partition management |
| **Best fit** | Task queues, RPC, low-latency delivery | Event streaming, audit logs, replay |

**Why RabbitMQ fits this use case:**

1. **Queue semantics** — each saga step needs exactly one consumer to process each message. RabbitMQ's queue model naturally provides this (competing consumers); Kafka requires consumer-group configuration and partition assignment.
2. **Low volume** — order throughput in this e-commerce system is nowhere near Kafka's intended scale (millions per second). RabbitMQ's 50k msg/s ceiling is more than sufficient.
3. **Operational simplicity** — a single RabbitMQ container with the management UI running on port 15672 is trivial to operate in Docker Compose. Kafka requires at minimum a broker + ZooKeeper (or KRaft mode configuration).
4. **Durable queues + acknowledgement** — RabbitMQ's `persistent = true` + manual ack pattern gives the same at-least-once delivery guarantee needed for the saga without additional infrastructure.

**When Kafka would be preferred:** If the system needed to replay event history (e.g. rebuild the inventory DB from scratch by replaying all `OrderPlaced` events), or if order throughput exceeded ~100k/s, Kafka's log-based retention would be the right choice.

---

## Saga Design: Choreography vs. Orchestration

**Choreography** was chosen (each service publishes and reacts to events independently):

```
OrderService → [order.placed] → InventoryService
InventoryService → [inventory.reserved] → OrderService
OrderService → [order.confirmed] → NotificationService
                OR
InventoryService → [inventory.rejected] → OrderService
OrderService → [order.cancelled.notify] → NotificationService
OrderService → [order.cancelled.inventory] → InventoryService
```

**Why choreography over orchestration:**
- No central orchestrator is a single point of failure.
- Each service knows only its own responsibilities — lower coupling.
- Easier to add a new step (e.g. a LoyaltyService) by subscribing to an existing event without touching other services.
- Downside: the overall flow is harder to visualise (mitigated by structured logging + Seq).

---

## Queue Definitions

| Queue | Publisher | Consumer | Purpose |
|---|---|---|---|
| `order.placed` | OrderService | InventoryService | Trigger stock check |
| `inventory.reserved` | InventoryService | OrderService | Confirm order |
| `inventory.rejected` | InventoryService | OrderService | Cancel order |
| `order.confirmed` | OrderService | NotificationService | Notify customer: success |
| `order.cancelled.notify` | OrderService | NotificationService | Notify customer: cancelled |
| `order.cancelled.inventory` | OrderService | InventoryService | Compensate: restore stock |

All queues are declared **durable** (`durable: true`) and messages are published **persistent** — they survive a RabbitMQ restart.

---

## Idempotency

RabbitMQ provides **at-least-once** delivery: a message may be delivered more than once if the consumer crashes before sending an ack.

Consumers are made idempotent by:
- Checking order status before acting (e.g. if order is already `Confirmed`, skip the `InventoryReserved` handler).
- Using `BasicNack(requeue: false)` on unhandled exceptions — sends the message to the dead-letter queue rather than looping forever.

---

## Consequences

- Each service requires the `RabbitMQ.Client` NuGet package (included in SharedKernel).
- A RabbitMQ outage blocks new order confirmations but does not crash the services — they retry the connection every 5 seconds (`RabbitMqConsumerBase` reconnect loop).
- The management UI at http://localhost:15672 (guest/guest) shows queue depths and message rates in real time — useful for the demo.

## Extension: MongoDB persistence for notifications and Grafana observability

### Context
The notification step of the saga needed a place to persist notification events in a flexible way. Notifications are semi-structured, append-only, and easier to store as documents than as relational rows.

### Decision
Add MongoDB to the `NotificationService` and store every processed notification event in a `notifications` collection inside the `notificationsdb` database. In parallel, add Grafana to the Docker Compose stack so the project has a lightweight UI for dashboards and future metrics.

### Why specifically in NotificationService?
- The notification service already owns the business event that is emitted after the order saga completes.
- A document database fits event history better than a relational table because each notification can carry a flexible payload.
- Keeping MongoDB only for notifications prevents the other services from needing a second persistence model.
- Grafana is a natural observability layer for the project and is exposed on port `3000`.

### Implementation footprint
- Code: NotificationService/Data/MongoNotificationContext.cs, NotificationService/Data/NotificationDocument.cs, NotificationService/Services/NotificationStore.cs
- Runtime wiring: NotificationService/Program.cs and the notification consumers in NotificationService/Messaging/
- Infrastructure: docker-compose.yml adds a `mongo` service on port `27018` and a `grafana` service on port `3000`
- Dashboard template: docs/grafana/notification-dashboard.json

### Result
The system now uses a polyglot approach in practice:
- SQL Server for transactional business data
- Redis for cache
- RabbitMQ for saga events
- MongoDB for notification history
- Grafana for observability dashboards
