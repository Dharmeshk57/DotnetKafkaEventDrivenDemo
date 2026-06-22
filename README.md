# .NET Event-Driven Architecture with Apache Kafka

![.NET](https://img.shields.io/badge/.NET-10-blue)
![Kafka](https://img.shields.io/badge/Apache-Kafka-black)
![Docker](https://img.shields.io/badge/Docker-Enabled-blue)
![Architecture](https://img.shields.io/badge/Pattern-Event--Driven-green)
![License](https://img.shields.io/badge/License-MIT-yellow)

A production-grade Event-Driven Architecture POC built with .NET 10, Apache Kafka, and the Outbox pattern. Demonstrates reliable async communication between microservices with retry routing and dead-letter queue handling.

---

## Architecture Overview

```
Client
  │
  │ HTTP POST /api/orders
  ▼
Order.API  ──── saves to ────▶  SQL Server (Outbox table)
                                       │
                              OutboxPublisher (background)
                                       │
                                       │ publish
                                       ▼
                               Kafka Broker
                          ┌────────────────────┐
                          │   order-events      │
                          │   order-events.retry-1 │
                          │   order-events.retry-5 │
                          │   order-events.dlq  │
                          └────────────────────┘
                                       │
                              Order.Consumer (background)
                                       │
                                       ▼
                                  SQL Server
                              (ProcessedEvents table)
```

### Key Patterns
- **Outbox Pattern** — orders are saved to DB and Kafka atomically, preventing message loss
- **Retry Routing** — failed messages escalate: `main → retry-1 → retry-5 → dlq`
- **Idempotency** — duplicate events are detected and skipped via `ProcessedEvents` table
- **Dead Letter Queue** — poison messages land in DLQ for inspection without blocking processing

---

## Solution Structure

```
DotnetKafkaEventDriven-Demo/
├── docker-compose.yml              # Kafka broker + Kafka UI
├── Order.API/                      # Web API — event producer
│   ├── Controllers/
│   │   └── OrdersController.cs
│   ├── Services/
│   │   ├── KafkaProducer.cs        # Publishes events to Kafka
│   │   └── OutboxPublisher.cs      # Background outbox relay
│   ├── Data/
│   │   └── AppDbContext.cs
│   └── appsettings.json
├── Order.Consumer/                 # Background worker — event consumer
│   ├── Services/
│   │   ├── KafkaConsumer.cs        # Main topic consumer
│   │   ├── RetryConsumer.cs        # Retry topic consumers
│   │   ├── DeadLetterConsumer.cs   # DLQ observer
│   │   └── OrderProcessingService.cs
│   └── appsettings.json
└── Order.IntegrationTests/         # xUnit + Testcontainers
    ├── Infrastructure/
    │   ├── KafkaFixture.cs         # Spins up Kafka container
    │   └── DatabaseFixture.cs      # Spins up SQL Server container
    └── Tests/
        ├── PublisherTests.cs
        ├── ConsumerProcessingTests.cs
        ├── RetryRoutingTests.cs
        └── IdempotencyTests.cs
```

---

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0+ | [Download](https://dot.net) |
| Docker Desktop | Any recent | Must be running |
| Visual Studio / VS Code | Any | Optional |

> **Note:** SQL Server (LocalDB) is used by the running app. Integration tests spin up their own SQL Server container via Testcontainers — no manual SQL Server setup needed for tests.

---

## Running the Application

### Step 1 — Start Kafka infrastructure

From the project root:

```powershell
cd "c:\Mine\POC\DotnetKafkaEventDriven-Demo"
docker-compose up -d
```

This starts:
- Kafka broker on `localhost:29092`
- Kafka UI at `http://localhost:8080`
- Auto-creates all required topics

To stop and reset:
```powershell
docker-compose down -v
```

### Step 2 — Start the Order API

```powershell
cd Order.API
dotnet run
```

Swagger UI is available at:
```
https://localhost:7141/swagger
```

> **Important:** Use the HTTPS URL (`https://localhost:7141/swagger`), not HTTP. The API redirects HTTP to HTTPS, which causes Swagger's "Failed to fetch" error if you use the HTTP port directly. Accept the self-signed certificate warning on first visit.

### Step 3 — Start the Consumer (separate terminal)

```powershell
cd Order.Consumer
dotnet run
```

The consumer subscribes to all topics and begins processing. Watch the terminal for processing logs.

### Step 4 — Send a test order

In Swagger at `https://localhost:7141/swagger`, use `POST /api/Orders` with:

```json
{
  "orderId": "ORD-1001",
  "productName": "Laptop",
  "price": 75000
}
```

Expected response:
```json
{
  "message": "Order created",
  "orderId": "fc51641c-f687-4d34-9361-bd978b1945c8"
}
```

Expected consumer log:
```
[Information] Processing order ORD-1001 — Laptop @ 75000
```

### Verifying the full flow

- **Kafka UI** at `http://localhost:8080` — browse `order-events` topic to see messages
- **Consumer terminal** — logs each event as it is processed
- **Database** — `ProcessedEvents` table records each successfully handled event

---

## Kafka Topic Layout

| Topic | Purpose | Partitions |
|---|---|---|
| `order-events` | Main topic — new orders land here | 3 |
| `order-events.retry-1` | First retry — delay ~1 min | 1 |
| `order-events.retry-5` | Second retry — delay ~5 min | 1 |
| `order-events.dlq` | Dead-letter queue — exhausted retries or non-retryable errors | 1 |

### Retry routing rules

| Exception type | Routing |
|---|---|
| `RetryableException` (attempt 0) | → `retry-1` |
| `RetryableException` (attempt 1) | → `retry-5` |
| `RetryableException` (attempt 2+) | → `dlq` |
| `JsonException` | → `dlq` immediately (no retries) |
| `NonRetryableException` | → `dlq` immediately (no retries) |

---

## Running Integration Tests

Tests use **Testcontainers** — they automatically spin up isolated Kafka and SQL Server Docker containers. No manual infrastructure setup required.

```powershell
# From the solution root
dotnet test Order.IntegrationTests

# With detailed output
dotnet test Order.IntegrationTests --logger "console;verbosity=normal"

# Run a specific test class
dotnet test Order.IntegrationTests --filter "ClassName=Order.IntegrationTests.Tests.RetryRoutingTests"

# Run a specific test
dotnet test Order.IntegrationTests --filter "FullyQualifiedName~PublishAsync_Should_Deliver_Message_To_Topic"
```

> **Note:** Docker Desktop must be running. First run is slower as it pulls the Kafka and SQL Server images (~1–2 GB total).

### Test coverage

| Test class | What it verifies |
|---|---|
| `PublisherTests` | `KafkaProducer.PublishAsync` delivers messages to Kafka; full payload round-trip preserved |
| `ConsumerProcessingTests` | End-to-end: produce → consume → record appears in `ProcessedEvents` table with correct timestamp |
| `RetryRoutingTests` | Failed messages route to correct topic (`retry-1` → `retry-5` → `dlq`); non-retryable exceptions skip straight to DLQ |
| `IdempotencyTests` | Duplicate events (same `EventId`) are detected and skipped — no double-processing |

---

## Configuration

### Order.API — `appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=OrdersDb;Trusted_Connection=True;"
  },
  "Kafka": {
    "BootstrapServers": "localhost:29092",
    "Topic": "order-events"
  }
}
```

### Order.Consumer — `appsettings.json`

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:29092",
    "Topic": "order-events",
    "GroupId": "order-consumer-group",
    "Retry1Topic": "order-events.retry-1",
    "Retry5Topic": "order-events.retry-5",
    "DlqTopic": "order-events.dlq"
  }
}
```

Database migrations are applied automatically on startup for both services.

---

## Technologies

| Technology | Purpose |
|---|---|
| .NET 10 / ASP.NET Core | Web API and background worker services |
| Apache Kafka + Confluent.Kafka 2.13 | Distributed event streaming |
| Entity Framework Core 9 + SQL Server | Persistence (Outbox and ProcessedEvents) |
| Docker + Docker Compose | Kafka infrastructure |
| xUnit + Testcontainers | Integration testing with real Kafka and SQL Server |
| Swagger / OpenAPI | API documentation and manual testing |


