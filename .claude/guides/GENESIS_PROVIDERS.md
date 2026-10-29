# Genesis Provider Integration Guide

This guide shows how to integrate and use all 8 Genesis AWS providers in your microservice.

**Genesis provides infrastructure concerns** - caching, messaging, storage, search, notifications, workflows, AI, and reporting. Your code focuses on business logic while Genesis handles AWS connectivity, resilience, and observability.

---

## Table of Contents

1. [Caching (ElastiCache)](#1-caching-elasticache)
2. [Messaging (SQS/SNS)](#2-messaging-sqssns)
3. [File Storage (S3)](#3-file-storage-s3)
4. [Search (OpenSearch)](#4-search-opensearch)
5. [Notifications (SES/SNS)](#5-notifications-sessns)
6. [Workflow (Step Functions)](#6-workflow-step-functions)
7. [AI Assistance (Bedrock)](#7-ai-assistance-bedrock)
8. [Reporting (Metabase)](#8-reporting-metabase)

---

## 1. Caching (ElastiCache)

**Use for:** Session storage, API response caching, rate limiting, temporary data.

### Configuration

```json
{
  "Caching": {
    "UseLocalStack": true,
    "LocalStackUrl": "http://localhost:4566",
    "Region": "us-east-1",
    "ConnectionString": "localhost:6379",
    "KeyPrefix": "myservice",
    "EnableTenantIsolation": true,
    "DefaultExpiration": "01:00:00"
  }
}
```

### Dependency Injection

```csharp
// Program.cs
using Pervaxis.Genesis.Caching.AWS.Extensions;

builder.Services.AddGenesisCachingAWS(
    builder.Configuration.GetSection("Caching"));
```

### NuGet Package

```xml
<PackageReference Include="Pervaxis.Genesis.Caching.AWS" Version="2.0.0" />
```
```

### Usage Example

```csharp
using Pervaxis.Core.Abstractions.Genesis.Modules;

public class ProductService
{
    private readonly ICache _cache;
    private readonly ILogger<ProductService> _logger;

    public ProductService(ICache cache, ILogger<ProductService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<Product?> GetProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"product:{productId}";

        // Try cache first
        var cached = await _cache.GetAsync<Product>(cacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogInformation("Cache hit for product {ProductId}", productId);
            return cached;
        }

        // Cache miss - fetch from database
        _logger.LogInformation("Cache miss for product {ProductId}, fetching from database", productId);
        var product = await FetchFromDatabaseAsync(productId, cancellationToken);

        if (product != null)
        {
            // Cache for 1 hour
            await _cache.SetAsync(cacheKey, product, TimeSpan.FromHours(1), cancellationToken);
        }

        return product;
    }

    public async Task InvalidateProductCacheAsync(string productId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"product:{productId}";
        await _cache.RemoveAsync(cacheKey, cancellationToken);
        _logger.LogInformation("Invalidated cache for product {ProductId}", productId);
    }

    private async Task<Product?> FetchFromDatabaseAsync(string productId, CancellationToken cancellationToken)
    {
        // Your database logic here
        await Task.Delay(100, cancellationToken); // Simulate DB call
        return new Product { Id = productId, Name = "Sample Product", Price = 99.99m };
    }
}

public record Product
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
}
```

### Testing

```csharp
using NSubstitute;
using Pervaxis.Core.Abstractions.Genesis.Modules;

public class ProductServiceTests
{
    [Fact]
    public async Task GetProductAsync_CacheHit_ReturnsCachedProduct()
    {
        // Arrange
        var mockCache = Substitute.For<ICache>();
        var mockLogger = Substitute.For<ILogger<ProductService>>();

        var cachedProduct = new Product { Id = "123", Name = "Cached Product", Price = 49.99m };
        mockCache.GetAsync<Product>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(cachedProduct);

        var service = new ProductService(mockCache, mockLogger);

        // Act
        var result = await service.GetProductAsync("123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Cached Product", result.Name);
        await mockCache.Received(1).GetAsync<Product>(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetProductAsync_CacheMiss_FetchesAndCaches()
    {
        // Arrange
        var mockCache = Substitute.For<ICache>();
        var mockLogger = Substitute.For<ILogger<ProductService>>();

        mockCache.GetAsync<Product>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Product?)null);

        var service = new ProductService(mockCache, mockLogger);

        // Act
        var result = await service.GetProductAsync("123");

        // Assert
        Assert.NotNull(result);
        await mockCache.Received(1).SetAsync(
            Arg.Any<string>(),
            Arg.Any<Product>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }
}
```

---

## 2. Messaging (SQS/SNS)

**Use for:** Event-driven architecture, asynchronous processing, decoupled communication between services.

### Configuration

```json
{
  "Messaging": {
    "UseLocalStack": true,
    "LocalStackUrl": "http://localhost:4566",
    "Region": "us-east-1",
    "QueueUrl": "http://localhost:4566/000000000000/orders-queue",
    "TopicArn": "arn:aws:sns:us-east-1:000000000000:orders-topic",
    "EnableTenantIsolation": true,
    "MessageRetentionPeriod": 345600,
    "VisibilityTimeout": 30
  }
}
```

### Dependency Injection

```csharp
// Program.cs
using Pervaxis.Genesis.Messaging.AWS.Extensions;

builder.Services.AddGenesisMessagingAWS(
    builder.Configuration.GetSection("Messaging"));
```

### NuGet Package

```xml
<PackageReference Include="Pervaxis.Genesis.Messaging.AWS" Version="2.0.0" />
```
```

### Usage Example

```csharp
using Pervaxis.Core.Abstractions.Genesis.Modules;

public class OrderService
{
    private readonly IMessaging _messaging;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IMessaging messaging, ILogger<OrderService> logger)
    {
        _messaging = messaging;
        _logger = logger;
    }

    // Publishing events
    public async Task CreateOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        // Save order to database (not shown)

        // Publish event for other services
        var orderCreatedEvent = new OrderCreatedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _messaging.PublishAsync(
            "order-created",
            orderCreatedEvent,
            cancellationToken);

        if (result != null)
        {
            _logger.LogInformation("Published OrderCreated event for order {OrderId}", order.Id);
        }
    }

    // Consuming messages
    public async Task ProcessOrderMessagesAsync(CancellationToken cancellationToken = default)
    {
        var messages = await _messaging.ReceiveAsync<OrderCreatedEvent>("orders-queue", 10, cancellationToken);

        if (messages.Any())
        {
            foreach (var message in messages)
            {
                _logger.LogInformation("Processing order {OrderId}", message.OrderId);

                // Process the order
                await ProcessOrderAsync(message, cancellationToken);

                // Delete message after successful processing
                await _messaging.DeleteAsync("orders-queue", message.ReceiptHandle, cancellationToken);
            }
        }
    }

    private async Task ProcessOrderAsync(OrderCreatedEvent orderEvent, CancellationToken cancellationToken)
    {
        // Your business logic here
        await Task.Delay(100, cancellationToken);
        _logger.LogInformation("Order {OrderId} processed successfully", orderEvent.OrderId);
    }
}

public record Order
{
    public string Id { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
}

public record OrderCreatedEvent
{
    public string OrderId { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public DateTime CreatedAt { get; init; }
}
```

---

## 3. File Storage (S3)

**Use for:** Document storage, image uploads, file downloads, large object storage.

### Configuration

```json
{
  "FileStorage": {
    "UseLocalStack": true,
    "LocalStackUrl": "http://localhost:4566",
    "Region": "us-east-1",
    "BucketName": "myservice-files",
    "KeyPrefix": "uploads",
    "EnableTenantIsolation": true,
    "EnableServerSideEncryption": true,
    "PresignedUrlExpiration": "00:15:00"
  }
}
```

### Dependency Injection

```csharp
// Program.cs
using Pervaxis.Genesis.FileStorage.AWS.Extensions;

builder.Services.AddGenesisFileStorageAWS(
    builder.Configuration.GetSection("FileStorage"));
```

### NuGet Package

```xml
<PackageReference Include="Pervaxis.Genesis.FileStorage.AWS" Version="2.0.0" />
```
### Usage Example

```csharp
using Pervaxis.Core.Abstractions.Genesis.Modules;

public class FileService
{
    private readonly IFileStorage _fileStorage;

    public FileService(IFileStorage fileStorage)
    {
        _fileStorage = fileStorage;
    }

    public Task<string> UploadAsync(string key, Stream content, string? contentType, IDictionary<string, string>? metadata, CancellationToken cancellationToken = default) =>
        _fileStorage.UploadAsync(key, content, contentType, metadata, cancellationToken);

    public Task<Stream?> DownloadAsync(string fileId, CancellationToken cancellationToken = default) =>
        _fileStorage.DownloadAsync(fileId, cancellationToken);
}
```

### Testing

```csharp
var mockFileStorage = Substitute.For<IFileStorage>();
mockFileStorage.UploadAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<IDictionary<string, string>?>(), Arg.Any<CancellationToken>())
    .Returns(Task.FromResult("etag"));
```

---

## 4. Search (OpenSearch)

**Use for:** Full-text search, product catalogs, log analytics, document search.

### Configuration

```json
{
  "Search": {
    "UseLocalStack": true,
    "LocalStackUrl": "http://localhost:4566",
    "Region": "us-east-1",
    "ServiceUrl": "http://localhost:4566",
    "IndexPrefix": "myservice",
    "EnableTenantIsolation": true,
    "DefaultPageSize": 20,
    "MaxPageSize": 100
  }
}
```

### Dependency Injection

```csharp
// Program.cs
using Pervaxis.Genesis.Search.AWS.Extensions;

builder.Services.AddGenesisSearchAWS(
    builder.Configuration.GetSection("Search"));
```

### NuGet Package

```xml
<PackageReference Include="Pervaxis.Genesis.Search.AWS" Version="2.0.0" />
```
### Usage Example

```csharp
using Pervaxis.Core.Abstractions.Genesis.Modules;

public class SearchService
{
    private readonly ISearch _search;

    public SearchService(ISearch search)
    {
        _search = search;
    }

    public Task<bool> IndexAsync<T>(string index, string id, T document, CancellationToken cancellationToken = default) =>
        _search.IndexAsync(index, id, document, cancellationToken);
}
```

### Testing

```csharp
var mockSearch = Substitute.For<ISearch>();
mockSearch.SearchAsync<object>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(Task.FromResult(Enumerable.Empty<object>()));
```

---

## 5. Notifications (SES/SNS)

**Use for:** Transactional emails, SMS alerts, push notifications, customer communications.

### Configuration

```json
{
  "Notifications": {
    "UseLocalStack": true,
    "LocalStackUrl": "http://localhost:4566",
    "Region": "us-east-1",
    "SenderEmail": "noreply@myservice.com",
    "SenderName": "MyService",
    "EnableTenantIsolation": true,
    "DefaultSmsTopicArn": "arn:aws:sns:us-east-1:000000000000:sms-topic",
    "DefaultPushTopicArn": "arn:aws:sns:us-east-1:000000000000:push-topic"
  }
}
```

### Dependency Injection

```csharp
// Program.cs
using Pervaxis.Genesis.Notifications.AWS.Extensions;

builder.Services.AddGenesisNotificationsAWS(
    builder.Configuration.GetSection("Notifications"));
```

### NuGet Package

```xml
<PackageReference Include="Pervaxis.Genesis.Notifications.AWS" Version="2.0.0" />
```
### Usage Example

```csharp
using Pervaxis.Core.Abstractions.Genesis.Modules;

public class NotificationService
{
    private readonly INotification _notification;

    public NotificationService(INotification notification)
    {
        _notification = notification;
    }

    public Task<string> SendEmailAsync(string recipient, string subject, string body, bool isHtml, CancellationToken cancellationToken = default) =>
        _notification.SendEmailAsync(recipient, subject, body, isHtml, cancellationToken);
}
```

### Testing

```csharp
var mockNotification = Substitute.For<INotification>();
mockNotification.SendSmsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(Task.FromResult("message-id"));
```

---

## 6. Workflow (Step Functions)

**Use for:** Multi-step business processes, order fulfillment, approval workflows, long-running tasks.

### Configuration

```json
{
  "Workflow": {
    "UseLocalStack": true,
    "LocalStackUrl": "http://localhost:4566",
    "Region": "us-east-1",
    "StateMachineArn": "arn:aws:states:us-east-1:000000000000:stateMachine:order-fulfillment",
    "EnableTenantIsolation": true,
    "ExecutionNamePrefix": "myservice"
  }
}
```

### Dependency Injection

```csharp
// Program.cs
using Pervaxis.Genesis.Workflow.AWS.Extensions;

builder.Services.AddGenesisWorkflowAWS(
    builder.Configuration.GetSection("Workflow"));
```

### NuGet Package

```xml
<PackageReference Include="Pervaxis.Genesis.Workflow.AWS" Version="2.0.0" />
```
### Usage Example

```csharp
using Pervaxis.Core.Abstractions.Genesis.Modules;

public class WorkflowService
{
    private readonly IWorkflow _workflow;

    public WorkflowService(IWorkflow workflow)
    {
        _workflow = workflow;
    }

    public Task<string> StartExecutionAsync(string workflowName, object input, CancellationToken cancellationToken = default) =>
        _workflow.StartExecutionAsync(workflowName, input, cancellationToken);
}
```

### Testing

```csharp
var mockWorkflow = Substitute.For<IWorkflow>();
mockWorkflow.GetExecutionStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(Task.FromResult("RUNNING"));
```

---

## 7. AI Assistance (Bedrock)

**Use for:** Content generation, chatbots, recommendations, image generation, data analysis.

### Configuration

```json
{
  "AIAssistance": {
    "UseLocalStack": false,
    "Region": "us-east-1",
    "TextModelId": "anthropic.claude-3-sonnet-20240229-v1:0",
    "ImageModelId": "stability.stable-diffusion-xl-v1",
    "EnableTenantIsolation": true,
    "DefaultMaxTokens": 1024,
    "DefaultTemperature": 0.7
  }
}
```

### Dependency Injection

```csharp
// Program.cs
using Pervaxis.Genesis.AIAssistance.AWS.Extensions;

builder.Services.AddGenesisAIAssistanceAWS(
    builder.Configuration.GetSection("AIAssistance"));
```

### NuGet Package

```xml
<PackageReference Include="Pervaxis.Genesis.AIAssistance.AWS" Version="2.0.0" />
```
### Usage Example

```csharp
using Pervaxis.Core.Abstractions.Genesis.Modules;

public class AiService
{
    private readonly IAIAssistant _aiAssistant;

    public AiService(IAIAssistant aiAssistant)
    {
        _aiAssistant = aiAssistant;
    }

    public Task<string> GenerateTextAsync(string prompt, CancellationToken cancellationToken = default) =>
        _aiAssistant.GenerateTextAsync(prompt, cancellationToken);
}
```

### Testing

```csharp
var mockAiAssistant = Substitute.For<IAIAssistant>();
mockAiAssistant.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(Task.FromResult(Array.Empty<float>()));
```

---

## 8. Reporting (Metabase)

**Use for:** Business dashboards, analytics, data visualization, scheduled reports.

### Configuration

```json
{
  "Reporting": {
    "BaseUrl": "http://localhost:3000",
    "ApiKey": "your-metabase-api-key",
    "EnableTenantIsolation": true,
    "DefaultTimeout": 30
  }
}
```

### Dependency Injection

```csharp
// Program.cs
using Pervaxis.Genesis.Reporting.AWS.Extensions;

builder.Services.AddGenesisReportingAWS(
    builder.Configuration.GetSection("Reporting"));
```

### NuGet Package

```xml
<PackageReference Include="Pervaxis.Genesis.Reporting.AWS" Version="2.0.0" />
```
### Usage Example

```csharp
using Pervaxis.Core.Abstractions.Genesis.Modules;

public class ReportingService
{
    private readonly IReporting _reporting;

    public ReportingService(IReporting reporting)
    {
        _reporting = reporting;
    }

    public Task<IEnumerable<T>> ExecuteQueryAsync<T>(string query, CancellationToken cancellationToken = default) =>
        _reporting.ExecuteQueryAsync<T>(query, cancellationToken);
}
```

### Testing

```csharp
var mockReporting = Substitute.For<IReporting>();
mockReporting.GetDashboardAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(Task.FromResult<object>("{}"));
```

---

## General Best Practices

### 1. Always Use CancellationToken

```csharp
public async Task<Result> MyMethodAsync(CancellationToken cancellationToken = default)
{
    // Pass cancellationToken to all Genesis provider calls
    await _cache.GetAsync<string>("key", cancellationToken);
}
```

### 2. Handle Null Results Properly

```csharp
var result = await _cache.GetAsync<Product>("key");

if (result != null)
{
    // Use result
}
else
{
    // Cache miss
}
```

### 3. Use Structured Logging

```csharp
_logger.LogInformation(
    "Processed order {OrderId} for customer {CustomerId}",
    orderId,
    customerId);
```

### 4. Multi-Tenancy

Genesis handles tenant isolation automatically when:
- `EnableTenantIsolation = true` in options
- `ITenantContext` is registered and resolved

Your code doesn't need to add tenant prefixes - Genesis does it.

### 5. LocalStack for Development

All providers support LocalStack. Set in appsettings.Development.json:

```json
{
  "Caching": { "UseLocalStack": true, "LocalStackUrl": "http://localhost:4566" },
  "Messaging": { "UseLocalStack": true, "LocalStackUrl": "http://localhost:4566" },
  "FileStorage": { "UseLocalStack": true, "LocalStackUrl": "http://localhost:4566" },
  "Search": { "UseLocalStack": true, "LocalStackUrl": "http://localhost:4566" },
  "Notifications": { "UseLocalStack": true, "LocalStackUrl": "http://localhost:4566" },
  "Workflow": { "UseLocalStack": true, "LocalStackUrl": "http://localhost:4566" },
  "AIAssistance": { "UseLocalStack": true, "LocalStackUrl": "http://localhost:4566" },
  "Reporting": { "UseLocalStack": true, "LocalStackUrl": "http://localhost:4566" }
}
```

### 6. Mock Genesis Providers in Tests

Use NSubstitute or Moq:

```csharp
var mockCache = Substitute.For<ICache>();
mockCache.GetAsync<string>(Arg.Any<string>())
    .Returns("cached-value");
```

### 7. Error Handling Strategy

```csharp
try
{
    var result = await _provider.OperationAsync(...);

    if (result == null)
    {
        _logger.LogWarning("Operation returned no result");
    }
}
catch (GenesisException ex)
{
    // Infrastructure failure (AWS connectivity, etc.)
    _logger.LogError(ex, "Genesis provider error");
}
```

---

## Summary

This guide covers all 8 Genesis providers with:
- ✅ Configuration examples
- ✅ DI registration
- ✅ Real usage patterns
- ✅ Testing strategies
- ✅ Best practices

**Genesis handles infrastructure. You focus on business logic.**

For questions or issues, see:
- Genesis repository: https://github.com/clarivex-tech/pervaxis-genesis
- Documentation: https://clarivex.tech/docs/genesis

---

*Genesis Provider Integration Guide*
*Pervaxis Platform · Clarivex Technologies*
