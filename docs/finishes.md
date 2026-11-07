
---

## ✅ מה מושלם (כל הדרישות החובה)

| Phase | סטטוס |
|---|---|
| **Phase 1** — docker-compose + README | ✅ |
| **Phase 2** — 5 שירותים, DB-per-service, ADRs (5 קבצים) | ✅ |
| **Phase 2** — Polyglot: SQL×4 + Redis (key-value NoSQL) + ADR מנומק | ✅ |
| **Phase 3** — Ocelot Gateway, BFF, 2 replicas Catalog + `X-Instance-Id` header | ✅ |
| **Phase 4** — RabbitMQ, Saga choreography (6 events), compensation path, Redis cache-aside | ✅ |
| **Phase 5** — Serilog+Seq, `/health` על כל שירות, CorrelationId בHTTP + broker | ✅ |

---

## ⚠️ חסר / חלש — צריך לטפל לפני הגשה

### 1. Phase 1 — Monolith baseline לא קיים בקוד
הדרישה (Task 1.1) אומרת לבנות monolith ואז לשבור אותו. אין תיקיית `Monolith/` בפרויקט. ה-ARCHITECTURE.md אומר "evolved from monolith" אבל אין ראיה קודית. **צריך להוסיף** לפחות סעיף ב-ARCHITECTURE.md עם:
- דיאגרמה "before" של הmonolith
- רשימת endpoints
- 3 בעיות צפויות בscale

### 2. Phase 2 — Polyglot: MongoDB חסר
הדרישה מבקשת ProductCatalogService → document DB. ה-ADR-002 מסביר למה נשאר SQL+Redis — **ההסבר טוב**, אבל הדרישה גם אומרת *"At least one more NoSQL decision of your choice"*. רק Redis קיים. שקלי להוסיף NoSQL שלישי (ראי Bonuses למטה).

### 3. docker-compose: healthcheck חסר לשירותים עצמם
המיקרו-שירותים יש להם `/health` endpoint אבל אין `healthcheck:` בdocker-compose עליהם — רק על SQL/Redis/RabbitMQ. Task 5.2 מבקש גם זה.

---

## 🌟 Bonuses שאפשר להוסיף (לא קיימים)

**הכי כדאי לנקודות:**

| Bonus | ערך | קושי | מה צריך |
|---|---|---|---|
| **CI/CD Pipeline** (GitHub Actions) | **+5%** | בינוני | קובץ `.github/workflows/ci.yml` — build + tests + Docker image per service |
| **Polly — Resilience patterns** | +bonus | קל | Retry + Circuit Breaker על HTTP calls בין שירותים (BffService → Catalog/Orders) |
| **Grafana dashboard** | +bonus | בינוני | הוסיפי Prometheus + Grafana לdocker-compose, חשפי metrics |
| **Orchestration Saga** | +bonus | קשה | Orchestrator מרכזי במקום choreography — השוואה בכתב |
| **Elasticsearch** | +bonus | בינוני | חיפוש מוצרים + מחליף את Seq כlog store |

**ממליצה להתחיל מ-CI/CD** — הכי מתוגמל (+5%), לא שובר קוד קיים, ומדגים GitHub Actions מעשי.

---

רוצה שאתחיל עם אחד מהנקודות החסרות או הbonuses?

Created 9 todos