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
flowchart LR Client -->|HTTP POST| OrderAPI OrderAPI -->|Publish Event| Kafka[(Kafka Broker)] Kafka -->|Consume Event| OrderConsumer


---

## 📂 Solution Structure
DotnetKafkaEventDriven-Demo │ ├── docker-compose.yml │ ├── Order.API │   ├── Controllers │   │   └── OrdersController.cs │   ├── Services │   │   └── KafkaProducer.cs │   ├── Models │   │   └── OrderCreatedEvent.cs │   ├── appsettings.json │   └── Program.cs │ └── Order.Consumer ├── Services │   └── KafkaConsumer.cs ├── OrderCreatedEvent.cs ├── appsettings.json ├── Properties │   └── launchSettings.json └── Program.cs


---

## 🐳 Infrastructure Setup

**Start Kafka:**
docker-compose down -v docker-compose up -d


**Kafka exposed on:**


---

## 🚀 Running the Application

**1️⃣ Start Order API:**

cd Order.API dotnet run


**Swagger URL:**

http://localhost:5122/swagger


**2️⃣ Start Consumer:**

cd Order.Consumer dotnet run


**Expected output:**
[2026-03-02 12:00:00] [Information] Consumed event: OrderCreated - OrderId: 101, Product: Laptop, Quantity: 2    

---

## 📤 Publishing an Order

**POST** `/api/orders`

**Example:**
{ "orderId": 101, "productName": "Laptop", "quantity": 2 }


**Response:**
{
  "message": "Order created and event published successfully."
}   


---

## 🛠 Technologies Used

- **.NET 9** - Latest .NET framework
- **Apache Kafka** - Distributed streaming platform
- **Confluent.Kafka** - .NET client for Apache Kafka
- **Docker & Docker Compose** - Containerization
- **Swagger/OpenAPI** - API documentation
- **Background Service** - .NET hosted service for consuming messages

---

## 📋 Prerequisites

- .NET 9 SDK
- Docker Desktop
- Visual Studio 2022 or VS Code

---

## 🔧 Configuration

**Kafka Settings (appsettings.json):**

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Topic": "orders"
  }
}
``` 


---

## 📝 License

This project is licensed under the MIT License.