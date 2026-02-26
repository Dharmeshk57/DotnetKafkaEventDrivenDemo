# 🚀 .NET 9 Event-Driven Architecture with Apache Kafka

![.NET](https://img.shields.io/badge/.NET-9-blue)
![Kafka](https://img.shields.io/badge/Apache-Kafka-black)
![Docker](https://img.shields.io/badge/Docker-Enabled-blue)
![Architecture](https://img.shields.io/badge/Pattern-Event--Driven-green)
![License](https://img.shields.io/badge/License-MIT-yellow)

An enterprise-grade Event-Driven Architecture demo built using:

- ✅ .NET 9 Web API (Producer Service)
- ✅ Apache Kafka (Message Broker)
- ✅ .NET Background Consumer Service
- ✅ Dockerized Kafka Infrastructure
- ✅ Swagger for API testing

---

## 📌 Architecture Overview

This solution demonstrates asynchronous communication between services using Kafka as an event bus.

### Flow

1. Order API receives request
2. Publishes `OrderCreated` event to Kafka
3. Kafka stores event in topic
4. Consumer subscribes and processes event

---

## 🏗 Architecture Diagram
flowchart LR
    Client -->|HTTP POST| OrderAPI
    OrderAPI -->|Publish Event| Kafka[(Kafka Broker)]
    Kafka -->|Consume Event| OrderConsumer
---

## 📂 Solution Structure
DotnetKafkaEventDriven-Demo
│
├── docker-compose.yml
│
├── Order.API
│   ├── Controllers
│   ├── Services
│   ├── Models
│   └── Program.cs
│
└── Order.Consumer
    └── Program.cs
---

## 🐳 Infrastructure Setup

**Start Kafka:**docker-compose down -v
docker-compose up -d
**Verify:**
docker ps
**Kafka exposed on:**localhost:29092
---

## 🚀 Running the Application

**1️⃣ Start Order API:**cd Order.API
dotnet run
**Swagger URL:**http://localhost:5122/swagger
**2️⃣ Start Consumer:**cd Order.Consumer
dotnet run
**Expected output:**Waiting for messages...
Order received: { ... }
---

## 📤 Publishing an Order

**POST** `/api/orders`

**Example:**{
  "orderId": 101,
  "productName": "Laptop",
  "quantity": 2
}
**Response:**
Order event published successfully


