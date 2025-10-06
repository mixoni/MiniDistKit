# ReliableFlow — Outbox, Idempotency & Saga Patterns in .NET 8

**ReliableFlow** is a minimal yet complete demo that showcases three core building blocks of reliable distributed systems:

- **Outbox Pattern** – reliable event publishing within a single database transaction  
- **Idempotency** – safe retries and deduplication at API and consumer levels  
- **Saga Pattern** – orchestration of multi-step processes without 2-phase commit  

> Built with: .NET 8, ASP.NET Core Minimal API, EF Core (In-Memory), and an in-memory Message Bus (based on Channels).

---

## 🔍 What It Demonstrates

- How to combine **database changes** and **message publishing** atomically (Outbox).  
- How to make **command handlers** and **event consumers** **idempotent** and safe to retry.  
- How to coordinate **multi-step workflows** (Claim → Payment → Activation) with **Saga orchestration and compensation**.  

---

## 🧱 Architecture Overview

```
          HTTP POST /claims
                 │
                 ▼
        ┌───────────────────┐
        │  ASP.NET Handler  │
        └───────┬───────────┘
                │ (DbContext = UoW, 1 local transaction)
                ▼
        ┌───────────────────┐
        │  Claims + Outbox  │  ← SaveChanges()
        └─────────┬─────────┘
                  │
       OutboxDispatcher (BackgroundService)
                  │        publish
                  ▼    ─────────────►  In-Memory MessageBus
            ClaimCreated                      │
                  │                           ▼
                  │                    PaymentsHandler
                  │                      (idempotent)
                  │                    OK    │    FAIL
                  │                          │
                  ▼                          ▼
             SagaOrchestrator        SagaOrchestrator
             (state machine)          (compensate)
                  │                          │
             Activate Claim               Revert Claim
```


ReliableFlow.http   ← REST Client test file (VS Code / Rider)
README.md
```

---

## 🚀 Getting Started

```bash
cd src/ReliableFlow.Api
dotnet run
```

The API will start on the port shown in the console (typically `http://localhost:5000`).

---

## 🧪 Testing the Flow

Use **VS Code REST Client** or **curl**.  
Open `ReliableFlow.http` and click **Send Request** above each block.

### ✅ Happy Path – Payment OK → Saga Activates
```
POST http://localhost:5000/claims
Content-Type: application/json

{
  "policyNumber": "P-001",
  "amount": 123.45
}
```

Log output:
- Outbox publishes `ClaimCreated`
- PaymentsHandler → “Payment OK”
- SagaOrchestrator → `ClaimActivated`

### ❌ Failure Path – Payment FAIL → Saga Compensates
```
POST http://localhost:5000/claims
Content-Type: application/json

{
  "policyNumber": "P-002",
  "amount": 1500
}
```

Log output:
- “Payment FAILED”
- Saga compensates → claim status `Reverted`

### 🧩 API-Level Idempotency
The first request succeeds; the second returns `409 Conflict`:
```
POST http://localhost:5000/claims
Content-Type: application/json
Idempotency-Key: 5b0d4c

{
  "policyNumber": "P-003",
  "amount": 50
}
```

### 🧾 Debug Endpoints
```
GET  /debug/claims
GET  /debug/outbox
GET  /debug/processed
GET  /debug/sagas
POST /debug/replay/{id}   ← simulate duplicate event delivery
```

---

## 🧠 Patterns in Action

### Outbox
- Writes both **business data** and **Outbox messages** in the same transaction.  
- `OutboxDispatcher` periodically publishes undelivered messages to the bus.  
- Ensures *at-least-once delivery* — therefore consumers must be idempotent.

### Idempotency
- **API level** → `IdempotencyMiddleware` checks the `Idempotency-Key` header.  
- **Consumer level** → `ProcessedMessage` table tracks handled messages (unique MessageId).

### Saga
- `SagaOrchestrator` coordinates the workflow and triggers compensation on failure.  
- Each step is a local transaction (no distributed 2PC).

---

## 🔌 Replacing In-Memory Components

### Broker
Replace `InMemoryMessageBus` with RabbitMQ or Kafka:
- Publisher → use real producer in `OutboxDispatcher`.
- Consumers → subscribe using your chosen client library.

### Database
Replace In-Memory DB with SQL Server / PostgreSQL:
```csharp
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default"),
        sql => sql.EnableRetryOnFailure()));
```
Then:
```bash
dotnet ef migrations add Init
dotnet ef database update
```

---

## ⚙️ Reliability Notes

- Enable **optimistic concurrency** (`[Timestamp] RowVersion`) on frequently updated entities.  
- Use EF Core’s **Execution Strategy** (`EnableRetryOnFailure`) for transient failures.  
- Always implement **idempotent consumers** for Outbox-driven messaging (bar-once delivery).

---

## 🧰 Useful Commands

```bash
# Build & Run
dotnet build
dotnet run --project src/ReliableFlow.Api/ReliableFlow.Api.csproj

# (If EF provider is enabled)
dotnet ef migrations add Init -p src/ReliableFlow.Api
dotnet ef database update -p src/ReliableFlow.Api
```

---

