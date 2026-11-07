# Architecture Document — Store API Microservices

**Version:** 1.0 · **Date:** 2026-07-05

---

## 1. Phase 1 — Monolith Baseline

### 1.1 The Starting Point

The system began as a single .NET 8 WebAPI containing all business logic — Orders, Products, and Inventory — backed by one shared SQL Server database. All code lived in one deployable unit running on a single port.

```
┌─────────────────────────────────────────────┐
│              Monolith API :5000              │
│                                             │
│  ┌──────────┐  ┌──────────┐  ┌───────────┐ │
│  │ Products │  │  Orders  │  │ Inventory │ │
│  │  Logic   │  │  Logic   │  │  Logic    │ │
│  └────┬─────┘  └────┬─────┘  └─────┬─────┘ │
│       └─────────────┴──────────────┘        │
│                     │                       │
│            Single SQL Database              │
└─────────────────────────────────────────────┘
```

### 1.2 Monolith Endpoints

| Method | Route | Description |
|---|---|---|
| GET | `/api/products` | List all products |
| POST | `/api/products` | Create product |
| GET | `/api/products/{id}` | Get product by ID |
| GET | `/api/orders` | List all orders |
| POST | `/api/orders` | Place order (deducts stock inline) |
| GET | `/api/orders/{id}` | Get order by ID |
| GET | `/api/inventory/{productId}` | Get stock level |
| PUT | `/api/inventory/{productId}` | Update stock level |

### 1.3 Three Problems at Scale

| # | Problem | Impact |
|---|---|---|
| **1. Single point of failure** | If the API process crashes, all capabilities (browse, order, inventory) go down simultaneously. There is no way to keep products browsable while orders are under maintenance. | Zero availability isolation |
| **2. Shared database bottleneck** | All features compete for the same connection pool and table locks. A spike in order writes (e.g., a flash sale) locks rows in the Products table, blocking catalog reads. | Read/write contention at scale |
| **3. No independent scalability** | The catalog service receives 100× more traffic than the order service. With a monolith, scaling means duplicating the entire application — wasting resources on order and inventory logic that doesn't need extra capacity. | Wasteful horizontal scaling |

---

## 2. System Overview

This system is a production-grade e-commerce platform that was evolved from the monolithic starting point above into a fully distributed microservices architecture. It demonstrates containers, async messaging, the saga pattern, API gateway, BFF, load balancing, polyglot persistence, and observability.

**Core capabilities:**
- Browse products (paginated, searchable, cached)
- Place an order
- Reserve inventory asynchronously (saga)
- Notify the customer of the final outcome

---

## 2. Final Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│  CLIENTS                                                            │
│  Angular SPA :4200  ·  Postman / curl                               │
└───────────────────────────────────┬─────────────────────────────────┘
                                    │ HTTP
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│  API GATEWAY  :5000  (Ocelot)                                       │
│  • Routing to downstream services                                   │
│  • Rate limiting (SharedKernel middleware)                          │
│  • JWT passthrough                                                  │
│  • Round-robin load balancing to Catalog replicas                   │
│  • /health endpoint                                                 │
└────┬────────┬──────────┬───────────┬──────────┬─────────────────────┘
     │        │          │           │          │
     ▼        ▼          ▼           ▼          ▼
  /auth    /products  /orders    /inventory  /bff
  /users   /categories
     │        │          │           │          │
     ▼        ▼          ▼           ▼          ▼
┌────────┐ ┌──────────────────┐ ┌────────┐ ┌────────┐ ┌────────────┐
│UserAuth│ │Catalog  Catalog  │ │Orders  │ │Invntry │ │    BFF     │
│Service │ │Svc-1    Svc-2    │ │Service │ │Service │ │  Service   │
│:5001   │ │:5002    :5009    │ │:5003   │ │:5005   │ │  :5004     │
│SQL     │ │SQL+Redis Redis   │ │SQL     │ │SQL     │ │            │
└────────┘ └────────────┬─────┘ └───┬────┘ └──┬─────┘ └─────┬──────┘
                        │           │          │             │
                        ▼           │          │             │
                     Redis :6379 ◄──┘          │             │
                                               │             │
                        ┌──────────────────────┘             │
                        │    RabbitMQ :5672                  │
                        │   ┌──────────────────┐             │
                        │   │  order.placed    │             │
                        │   │  inv.reserved    │             │
                        │   │  inv.rejected    │             │
                        │   │  order.confirmed │             │
                        │   │  order.cancelled │             │
                        │   └──────────────────┘             │
                        │           │                        │
                        │           ▼                        │
                        │   ┌──────────────┐                 │
                        │   │Notification  │                 │
                        │   │Service :5006 │                 │
                        │   └──────────────┘                 │
                        │                                    │
                        └────────────────────────────────────┘
                                    │ (all services)
                                    ▼
                             Seq :5341
                        (centralised log aggregation)
```

---

## 3. Service Breakdown

### 3.1 UserAuthService
- **Responsibility:** Registration, login, JWT token issuance
- **Database:** SQL Server `userauthdb`
- **Rationale (ADR-001 family):** User credentials and roles must be stored with strong consistency and ACID guarantees. A lost password update or a race condition on role assignment could compromise security.

### 3.2 ProductCatalogService (×2 replicas)
- **Responsibility:** Product and category CRUD; paginated/search reads
- **Database:** SQL Server `catalogdb` + Redis cache (cache-aside)
- **Load balancing:** Ocelot round-robins across `productcatalogservice1` and `productcatalogservice2`
- **Proof of LB:** Every response includes `X-Instance-Id: <container-hostname>` header
- **Rationale:** See [ADR-002](ADR-002-catalog-service-database.md) and [ADR-004](ADR-004-redis-cache.md)

### 3.3 OrderService
- **Responsibility:** Order lifecycle; Saga entry point
- **Database:** SQL Server `orderdb`
- **Saga role:** Creates order as `Pending`, publishes `OrderPlaced`, then reacts to `InventoryReserved` / `InventoryRejected`
- **Rationale:** See [ADR-001](ADR-001-order-service-database.md) and [ADR-005](ADR-005-rabbitmq-messaging.md)

### 3.4 InventoryService *(new)*
- **Responsibility:** Stock levels; Saga Step 2 (reserve or reject) and compensation (restore on cancel)
- **Database:** SQL Server `inventorydb`
- **API:** `GET /api/inventory/{productId}`, `PUT /api/inventory/{productId}`
- **Rationale:** See [ADR-003](ADR-003-inventory-service-database.md)

### 3.5 NotificationService *(new)*
- **Responsibility:** Customer notification at saga completion (confirmed or cancelled)
- **Database:** None — event-log only (logs to Seq)
- **Note:** In production this would call SendGrid / Twilio; here it writes structured log entries which are fully traceable via `CorrelationId`

### 3.6 BffService (Backend for Frontend)
- **Responsibility:** Aggregates data from Catalog + Orders + Users into single responses for the Angular UI
- **Key endpoints:**
  - `GET /api/dashboard/user/{userId}` — user info + recent orders + totals
  - `GET /api/dashboard/catalog` — products + categories (paginated)
- **Cache:** Redis for product lookups during aggregation

### 3.7 ApiGateway
- **Technology:** Ocelot
- **Routing table:**

| Upstream | Downstream | Notes |
|---|---|---|
| `/auth/**` | UserAuthService | — |
| `/users/**` | UserAuthService | — |
| `/products/**` | Catalog 1 + Catalog 2 | RoundRobin LB |
| `/categories/**` | Catalog 1 + Catalog 2 | RoundRobin LB |
| `/orders/**` | OrderService | — |
| `/inventory/**` | InventoryService | — |
| `/bff/**` | BffService | — |
| `/health` | ApiGateway itself | — |

---

## 4. Database Decisions (Polyglot Persistence)

| Service | Database | Family | Key Reason |
|---|---|---|---|
| UserAuthService | SQL Server | Relational | ACID for credentials and roles |
| ProductCatalogService | SQL Server + **Redis** | Relational + **Key-value (NoSQL)** | Strong schema for prices; Redis for read performance |
| OrderService | SQL Server | Relational | ACID — money must be transactional |
| InventoryService | SQL Server | Relational | Concurrent stock deduction needs serialised transactions |
| BffService | Redis (cache only) | Key-value (NoSQL) | Fast aggregation; data lives in downstream services |

**Two NoSQL families in use:**
1. **Key-value** — Redis (cache across three services)
2. **Relational** — SQL Server (four services with different isolated databases)

Full decision rationale is documented in `docs/ADR-001` through `docs/ADR-005`.

---

## 5. Order Saga — Choreography

The saga replaces synchronous HTTP stock management with asynchronous, compensable steps.

### Happy Path
```
[Client] POST /orders
    → OrderService: save Order{status=Pending}, publish order.placed
    → InventoryService: deduct stock, publish inventory.reserved
    → OrderService: update Order{status=Confirmed}, publish order.confirmed
    → NotificationService: log "Order #N CONFIRMED"
```

### Compensation Path (out-of-stock)
```
[Client] POST /orders
    → OrderService: save Order{status=Pending}, publish order.placed
    → InventoryService: insufficient stock, publish inventory.rejected
    → OrderService: update Order{status=Cancelled},
                    publish order.cancelled.notify
                    publish order.cancelled.inventory
    → NotificationService: log "Order #N CANCELLED"
    → InventoryService: restore any partially-deducted stock
```

### Correlation ID Tracing

Every event carries a `CorrelationId` (GUID). Every HTTP request generates or inherits a `CorrelationId` from the `X-Correlation-Id` header. All log entries across all services include `[CorrelationId]` — a single order's complete saga can be retrieved in Seq with:

```
CorrelationId = "your-guid-here"
```

---

## 6. Observability Stack

| Concern | Technology | Access |
|---|---|---|
| Structured logging | Serilog | Console + rolling file in every container |
| Log aggregation | Seq | http://localhost:5341 |
| Health checks | ASP.NET Core Health Checks + `AspNetCore.HealthChecks.SqlServer/Redis` | `GET /health` on every service |
| Message tracing | RabbitMQ Management UI | http://localhost:15672 (guest/guest) |
| Correlation ID | `X-Correlation-Id` HTTP header + RabbitMQ `CorrelationId` message property | Automatic on all requests |

**Serilog output template (all services):**
```
[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}
```

---

## 7. Phase Compliance Summary

| Phase | Requirement | Status |
|---|---|---|
| **Phase 1** | Monolith baseline + docker-compose | ✅ docker-compose runs all services with one command |
| **Phase 2** | ≥4 services, database-per-service, polyglot persistence, ADRs | ✅ 5 backend services + 2 infra; SQL Server × 4 DBs + Redis; ADRs in `docs/` |
| **Phase 3** | API Gateway, BFF (2+ service aggregation), load balancing | ✅ Ocelot gateway; BFF aggregates catalog+orders+users; 2 catalog replicas with round-robin + `X-Instance-Id` header |
| **Phase 4** | Async messaging, choreography saga, compensation, Redis cache-aside | ✅ RabbitMQ; 6-queue saga; compensation path implemented; cache-aside with hit/miss logging |
| **Phase 5** | Structured logging, /health, Correlation ID across HTTP + broker | ✅ Serilog+Seq; /health on all services; `CorrelationId` in HTTP headers, message properties, and every log line |
