global using Confluent.Kafka;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Logging.Abstractions;
global using Order.Consumer.Exceptions;
global using Order.Consumer.Messaging;
global using Order.Consumer.Services;
global using System.Text;
global using System.Text.Json;
global using Testcontainers.Kafka;
global using Testcontainers.MsSql;
global using Xunit;

// Aliases resolve AppDbContext and OrderCreatedEvent name collisions
// that arise from referencing both Order.API and Order.Consumer.
global using ConsumerDbContext   = Order.Consumer.Data.AppDbContext;
global using ConsumerOrderEvent  = Order.Consumer.OrderCreatedEvent;
