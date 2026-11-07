# Store API — Production-Grade Microservices

[![CI](https://github.com/SaraDev841/ArchitectureFinalProject/actions/workflows/ci.yml/badge.svg)](https://github.com/SaraDev841/ArchitectureFinalProject/actions/workflows/ci.yml)

A fully distributed e-commerce order system demonstrating the complete microservices lifecycle: containerisation, async messaging, saga pattern, API gateway, BFF, load balancing, polyglot persistence, and observability.

---

## One-Command Startup

```bash
docker compose up --build
```

All services start automatically. Databases are created and migrated on first boot.

---

## Service URLs (after `docker compose up`)

| Service | URL | Notes |
|---|---|---|
| **API Gateway** | http://localhost:5000 | Single entry point for all clients |
| **Seq (Log Viewer)** | http://localhost:5341 | Centralised structured logs |
| **RabbitMQ Management** | http://localhost:15672 | guest / guest |
| **Grafana** | http://localhost:3000 | admin / admin (import the starter dashboard from docs/grafana/notification-dashboard.json) |
| **MongoDB** | mongodb://localhost:27018 | Document store for notification events |
| **Swagger — UserAuth** | http://localhost:5001/swagger | Direct (dev only) |
| **Swagger — Catalog 1** | http://localhost:5002/swagger | Direct (dev only) |
| **Swagger — Catalog 2** | http://localhost:5009/swagger | Direct (dev only) |
| **Swagger — Orders** | http://localhost:5003/swagger | Direct (dev only) |
| **Swagger — Inventory** | http://localhost:5005/swagger | Direct (dev only) |
| **Swagger — BFF** | http://localhost:5004/swagger | Direct (dev only) |

All external traffic should go through the **API Gateway on port 5000**.

---

## Architecture Overview

```
Browser / Angular (4200)
        │
        ▼
  API Gateway :5000  (Ocelot — routing, rate limiting, JWT passthrough)
  ┌──────┬──────────────┬──────────────┬──────────────┐
  │      │              │              │              │
 Auth  Products     Orders           BFF           Inventory
 5001  5002/5009     5003             5004           5005
  │      │    ↑         │ publish       │              │
  └──SQL──┘  Redis  RabbitMQ ◄─────────┘         RabbitMQ
             Cache    │                                │
                      │  OrderPlaced                   │
                      └────────────────────────────────┘
                           InventoryReserved/Rejected
                                      │
                               NotificationService
                                    5006
                                      │
                                    Seq :5341
```

---

## Services

| Service | Responsibility | Database | Port |
|---|---|---|---|
| **UserAuthService** | Registration, login, JWT tokens | SQL Server (`userauthdb`) | 5001 |
| **ProductCatalogService** | Products and categories (×2 replicas) | SQL Server + Redis cache | 5002 / 5009 |
| **OrderService** | Order lifecycle, saga orchestration | SQL Server (`orderdb`) | 5003 |
| **InventoryService** | Stock management, saga consumer | SQL Server (`inventorydb`) | 5005 |
| **NotificationService** | Customer notifications, saga consumer | MongoDB (`notificationsdb`) + RabbitMQ | 5006 |
| **BffService** | Aggregated views for the frontend | Redis cache | 5004 |
| **ApiGateway** | Routing, rate limiting | None | 5000 |

---

## Order Saga (Choreography)

The saga replaces the old synchronous stock-deduction flow with four asynchronous steps:

```
1. POST /orders
        │
        ▼ (OrderService)
   Save Order as "Pending"
   Publish → order.placed
        │
        ▼ (InventoryService)
   Check stock for every item
   ┌─── Enough stock? ─────────────────────────────┐
   │ YES                                          NO │
   │ Deduct stock                    Publish → inventory.rejected
   │ Publish → inventory.reserved              │
   │                                            ▼ (OrderService)
   ▼ (OrderService)               Update Order → "Cancelled"
   Update Order → "Confirmed"     Publish → order.cancelled.notify
   Publish → order.confirmed      Publish → order.cancelled.inventory
        │                                   │       │
        ▼ (NotificationService)             │       ▼ (InventoryService)
   Log "Order confirmed"                    │   Restore stock (compensation)
                                            ▼ (NotificationService)
                                       Log "Order cancelled"
```

### Demo: Happy Path
```bash
# 1. Register
curl -X POST http://localhost:5000/auth/register \
  -H "Content-Type: application/json" \
  -d '{"firstName":"Alice","lastName":"Smith","email":"alice@test.com","password":"Pass1234!","role":0}'

# 2. Login
curl -X POST http://localhost:5000/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"alice@test.com","password":"Pass1234!"}'

# 3. Seed inventory (use JWT token from step 2)
curl -X PUT http://localhost:5000/inventory/1 \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"productId":1,"stockQuantity":100}'

# 4. Place order
curl -X POST http://localhost:5000/orders \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"userId":1,"shippingAddress":"123 Main St","orderItems":[{"productId":1,"quantity":2}]}'

# Watch Seq at http://localhost:5341 — filter by CorrelationId to trace the full saga
```

### Demo: Compensation Path (out-of-stock)
```bash
# Set stock to 0
curl -X PUT http://localhost:5000/inventory/1 \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"productId":1,"stockQuantity":0}'

# Place the same order — it will be created as Pending then immediately cancelled
curl -X POST http://localhost:5000/orders \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"userId":1,"shippingAddress":"123 Main St","orderItems":[{"productId":1,"quantity":2}]}'

# Seq logs will show: OrderPlaced → InventoryRejected → OrderCancelled → NotificationSent
```

---

## Load Balancing Demo

ProductCatalogService runs as two replicas. Every response includes `X-Instance-Id` (the container hostname). Call the products endpoint several times and watch the header alternate:

```bash
for i in 1..6; do
  curl -s -I http://localhost:5000/products | grep X-Instance-Id
done
# Expected output alternates between productcatalogservice1 and productcatalogservice2
```

---

## Cache Hit / Miss Demo

Redis caches product reads with a TTL. Watch the Seq logs for `CACHE HIT` vs `CACHE MISS`:

```bash
# First call → CACHE MISS (fetches from DB, stores in Redis)
curl http://localhost:5000/products/1

# Second call → CACHE HIT (served from Redis, no DB query)
curl http://localhost:5000/products/1
```

---

## Grafana and NoSQL Observability

Grafana is included as a lightweight observability UI for the project.

- Open Grafana at http://localhost:3000
- Login with `admin` / `admin`
- Import the starter dashboard from [docs/grafana/notification-dashboard.json](docs/grafana/notification-dashboard.json)

MongoDB was added to the notification flow because notification events are naturally document-shaped and append-only. The service persists each event in a `notifications` collection inside the `notificationsdb` database.

## Health Checks

Every service exposes `/health`. The gateway proxies to them automatically:

```bash
curl http://localhost:5001/health  # UserAuthService
curl http://localhost:5002/health  # ProductCatalog replica 1
curl http://localhost:5009/health  # ProductCatalog replica 2
curl http://localhost:5003/health  # OrderService
curl http://localhost:5005/health  # InventoryService
curl http://localhost:5006/health  # NotificationService
curl http://localhost:5004/health  # BffService
curl http://localhost:5000/health  # ApiGateway
```

---

## Architecture Decision Records

See [`docs/`](docs/) for the full ADRs:

- [ADR-001 — OrderService: SQL Server (relational)](docs/ADR-001-order-service-database.md)
- [ADR-002 — ProductCatalogService: SQL Server + Redis](docs/ADR-002-catalog-service-database.md)
- [ADR-003 — InventoryService: SQL Server (relational)](docs/ADR-003-inventory-service-database.md)
- [ADR-004 — Redis: distributed cache (key-value NoSQL)](docs/ADR-004-redis-cache.md)
- [ADR-005 — RabbitMQ: message broker](docs/ADR-005-rabbitmq-messaging.md)

---

## Technology Stack

| Layer | Technology | Why |
|---|---|---|
| Runtime | .NET 8 / ASP.NET Core | Course baseline |
| Gateway | Ocelot | Lightweight, config-driven, supports LB natively |
| Messaging | RabbitMQ 3.13 | AMQP standard, durable queues, management UI |
| Cache | Redis 7 | Sub-millisecond key-value store, TTL support |
| Relational DB | SQL Server 2022 | ACID guarantees for money and orders |
| Log aggregation | Seq | First-class Serilog sink, free tier, instant query UI |
| Structured logging | Serilog | Enrichment, sinks, `{CorrelationId}` in every line |
| Containerisation | Docker + Compose | Single-command startup, isolated networks |
| Frontend | Angular 17 | SPA with guards, interceptors, services |
