# 🏁 Final Project — Gap Analysis & Completion Plan

## ✅ What's Already Done

| Area | Status |
|---|---|
| `docker-compose.yml` with SQL Server + Redis | ✅ |
| `OrderService` (relational, ACID) | ✅ |
| `ProductCatalogService` (CRUD + paginated search) | ✅ |
| `UserAuthService` (JWT auth, roles) | ✅ |
| `BffService` (aggregates 2+ services in one endpoint) | ✅ |
| `ApiGateway` (Ocelot routing all traffic) | ✅ |
| Redis cache-aside on `ProductCatalogService` | ✅ |
| Serilog structured logging (rolling file) | ✅ |
| Angular frontend (catalog, cart, orders, dashboard, admin) | ✅ |
| Repository pattern + Dependency Injection | ✅ |
| JWT middleware + rate limiting in `SharedKernel` | ✅ |

---

## ❌ What's Missing

### Phase 1 — Monolith Baseline (10%)
- [ ] **Task 1.3** — Root `README.md` with:
  - One-command startup instructions (`docker compose up`)
  - Architecture diagram (before & after)
  - List of endpoints
  - 3 problems expected at scale

---

### Phase 2 — Microservices + Polyglot Persistence (25%)
- [ ] **`InventoryService`** — completely missing; must manage stock levels
- [ ] **`NotificationService`** — completely missing; must notify customers of order outcome
- [ ] **Polyglot persistence** — `ProductCatalogService` uses SQL Server, spec says document DB (MongoDB). Options:
  - Migrate to MongoDB, **OR**
  - Write a strong ADR justifying SQL + rely on Redis (key-value) + a 3rd NoSQL of choice to satisfy "at least one more NoSQL decision"
- [ ] **ADRs (Architecture Decision Records)** for each DB choice — must use vocabulary: ACID, BASE, CAP, consistency model

---

### Phase 3 — Gateway, BFF & Load Balancing (15%)
- [x] API Gateway (Ocelot) ✅
- [x] BFF aggregating 2+ services ✅
- [ ] **Task 3.3** — 2+ replicas of `ProductCatalogService` behind a load balancer:
  - Add replica config in `docker-compose.yml` (use `deploy.replicas` or duplicate service entries)
  - Return `X-Instance-Id` response header (container hostname) to prove LB works

---

### Phase 4 — Async Messaging, Saga & Caching (25%)
- [x] Redis cache-aside on `ProductCatalogService` ✅
- [ ] **Task 4.1** — Add **RabbitMQ** (or Kafka) to `docker-compose.yml` + shared messaging infrastructure in `SharedKernel`
- [ ] **Task 4.2** — Implement **Order Saga (choreography)**:
  1. `OrderService` publishes `OrderPlaced` event
  2. `InventoryService` subscribes → reserves stock → publishes `InventoryReserved` or `InventoryRejected`
  3. `OrderService` subscribes → confirms order OR compensates (cancel + release reservation)
  4. `NotificationService` subscribes → sends customer notification of final state
- [ ] **Task 4.3** — Demonstrate **failure/compensation path**: place an order for out-of-stock product → show compensation in logs/screenshots

---

### Phase 5 — Monitoring & Observability (10%)
- [x] Serilog structured logging (file sink) ✅
- [ ] **Task 5.1** — Log aggregation: add **Seq** (or ELK/Loki) container to `docker-compose.yml`; wire Serilog sink to it in every service
- [ ] **Task 5.2** — `/health` endpoint per service + `healthcheck` entries in `docker-compose.yml`
- [ ] **Task 5.3** — **Correlation ID** that:
  - Propagates through HTTP headers (`X-Correlation-Id`)
  - Survives the trip through the message broker (stored in message headers)
  - Is logged in every service so one order's journey is fully traceable

---

### Deliverables (15%)
- [ ] Root `README.md` — one-command startup + architecture overview
- [ ] Architecture document (2–4 pages): final diagram, ADRs, messaging-tech comparison if off-script
- [ ] Demo evidence (logs/screenshots):
  - Saga happy path end-to-end
  - Compensation path (out-of-stock)
  - Cache hit vs. miss in logs
  - One fully-traced correlation ID across all services

---

## 🗺️ Recommended Implementation Order

### Step 1 — New Microservices (Phase 2 blocker + Phase 4 dependency)
Create `InventoryService`:
- Models: `InventoryItem` (ProductId, StockQuantity, ReservedQuantity)
- Endpoints: `GET /api/inventory/{productId}`, `POST /api/inventory/reserve`, `POST /api/inventory/release`
- Database: SQL Server `inventorydb` (or choose NoSQL with ADR justification)
- Wire into `docker-compose.yml` and Ocelot routes

Create `NotificationService`:
- Listens to messaging events only (no public endpoints needed, or a simple `GET /api/notifications/{userId}` log)
- Sends email/SMS or logs notification to console/DB

### Step 2 — Async Messaging (Phase 4 core)
- Add RabbitMQ service to `docker-compose.yml`
- Add messaging abstractions to `SharedKernel` (IMessagePublisher, IMessageConsumer)
- Install `MassTransit.RabbitMQ` or `RabbitMQ.Client` in services
- Define shared event contracts in `SharedKernel/Events/`:
  - `OrderPlaced`
  - `InventoryReserved`
  - `InventoryRejected`
  - `OrderConfirmed`
  - `OrderCancelled`
  - `NotificationRequested`

### Step 3 — Order Saga (Phase 4 core)
Wire choreography across 4 services using the events above.

### Step 4 — Correlation ID (Phase 5)
- Add `CorrelationIdMiddleware` to `SharedKernel/Middleware/`
- HTTP: read/write `X-Correlation-Id` header, store in `AsyncLocal`
- Messaging: embed correlation ID in every message header, extract on consume
- Log correlation ID in every Serilog output template

### Step 5 — Health Endpoints + Seq (Phase 5 quick wins)
- Add `app.MapHealthChecks("/health")` + `services.AddHealthChecks()` in every `Program.cs`
- Add `healthcheck:` entries in `docker-compose.yml`
- Add Seq container; update Serilog config in all `appsettings.json`

### Step 6 — Load Balancing (Phase 3)
- Duplicate `productcatalogservice` as `productcatalogservice2` in `docker-compose.yml`
- Add `X-Instance-Id: {Environment.MachineName}` response header in `ProductsController`
- Update Ocelot config to round-robin between both instances

### Step 7 — Polyglot Persistence / ADRs (Phase 2)
Option A (easier): Write ADRs justifying current SQL choices + add one more NoSQL (e.g., MongoDB for `NotificationService` logs, or Redis Streams for events).

Option B (full compliance): Migrate `ProductCatalogService` to MongoDB (requires replacing EF Core with MongoDB driver, updating models and repositories).

### Step 8 — Documentation + Demo
- Write `README.md`
- Write architecture document with final diagram + ADRs
- Capture demo screenshots/logs for all 4 required scenarios

---

## 📊 Grading Breakdown vs. Current State

| Phase | Weight | Current State | Gap |
|---|---|---|---|
| Phase 1 — Monolith + Docker Compose | 10% | docker-compose ✅, docs ❌ | Missing README/docs |
| Phase 2 — Microservices + Polyglot + ADRs | 25% | 3/5 services ✅, no Inventory/Notification, no ADRs, wrong DB for Catalog | Major |
| Phase 3 — Gateway, BFF, Load Balancer | 15% | Gateway ✅, BFF ✅, LB ❌ | Missing replicas |
| Phase 4 — Messaging, Saga, Caching | 25% | Caching ✅, messaging ❌, saga ❌ | Major |
| Phase 5 — Monitoring & Correlation | 10% | File logs ✅, no Seq, no /health, no correlation ID | Medium |
| Architecture doc & presentation | 15% | Nothing | Major |
| **Total mandatory** | **100%** | **~35%** | |

---

## 💡 Technology Notes

| Requirement | Class Default | What You Have | Swap Allowed? |
|---|---|---|---|
| Message broker | RabbitMQ | Nothing | Yes — Kafka, NATS, Azure Service Bus (write comparison doc) |
| API Gateway | Ocelot | Ocelot ✅ | — |
| Cache | Redis | Redis ✅ | — |
| Log aggregation | ELK / Seq | File only | Yes — Loki+Grafana, Seq (easiest) |
| Document DB | MongoDB | SQL Server | Yes — Cosmos DB, RavenDB (write ADR) |
| Load balancer | Nginx | Nothing | Yes — Traefik, HAProxy, Docker built-in |
