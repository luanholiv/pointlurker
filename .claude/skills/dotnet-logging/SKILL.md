---
name: dotnet-logging
description: How to add logging to C# and .NET applications using ILogger, dependency injection, and high-performance LoggerMessage source generation. Use this skill whenever you are adding, modifying, or reviewing any logging code in a C# project — whether the user says "add a log", "log this error", "set up logging", "add structured logging", or anything that involves writing to ILogger. Also trigger when the user is setting up a new service, handler, or background job that doesn't have logging yet, or when you notice they're using string interpolation in log calls. Don't wait for the user to ask for "high performance" specifically — always apply these patterns by default.
---

## Core principle

Always use compile-time source generation (`[LoggerMessage]`) for log calls. This is the standard for all .NET 6+ code — not just for "performance-critical" paths. It eliminates boxing, avoids runtime template parsing, and produces strongly-typed log calls.

Log methods live in a **dedicated static partial class** per service and are called as extension methods on `ILogger`:

```csharp
logger.LogOrderPlaced(order.Id);
```

Never use string interpolation in log messages (`$"..."`) — it defeats structured logging and allocates on every call regardless of whether the log level is enabled.

---

## Setup

### NuGet packages

```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.*" />
<!-- Included transitively via Microsoft.Extensions.Hosting or ASP.NET Core — only add explicitly if needed -->
```

### Register via DI (ASP.NET Core / Generic Host)

Logging is registered automatically when using `WebApplication.CreateBuilder` or `Host.CreateApplicationBuilder`. No manual registration needed.

To customise providers or minimum levels:

```csharp
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Warning);
```

### Inject ILogger

Always inject `ILogger<T>` — the generic type sets the log category to the fully qualified class name:

```csharp
public class OrderService(ILogger<OrderService> logger)
{
    // use logger here
}
```

### Configuration (`appsettings.json`)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    },
    "Console": {
      "IncludeScopes": true
    }
  }
}
```

Use `appsettings.Development.json` to override levels locally without changing the production config.

---

## Defining log methods with [LoggerMessage]

Create a `static partial class` named after the class it serves (e.g. `OrderServiceLogs`). Decorate each method with `[LoggerMessage]` and use `this ILogger logger` as the first parameter to make them extension methods. The source generator provides the implementation at compile time.

### Basic log method

```csharp
internal static partial class OrderServiceLogs
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Order {OrderId} placed successfully")]
    public static partial void LogOrderPlaced(this ILogger logger, Guid orderId);
}
```

Calling it:

```csharp
logger.LogOrderPlaced(order.Id);
```

### Including exceptions

Pass `Exception` as a parameter — the source generator handles it specially and it won't appear in the message template:

```csharp
internal static partial class OrderServiceLogs
{
    [LoggerMessage(EventId = 1002, Level = LogLevel.Error,
        Message = "Failed to place order for customer {CustomerId}")]
    public static partial void LogOrderFailed(this ILogger logger, Guid customerId, Exception ex);
}
```

Calling it:

```csharp
catch (Exception ex)
{
    logger.LogOrderFailed(command.CustomerId, ex);
}
```

### Event IDs

Assign stable numeric event IDs so log entries are searchable in aggregators (Seq, Grafana, Application Insights). Define them as constants grouped by domain:

```csharp
internal static class LogEvents
{
    internal const int OrderPlaced   = 1001;
    internal const int OrderFailed   = 1002;
    internal const int PaymentFailed = 2001;
}
```

### Naming convention

- Class: `{ClassName}Logs` — co-locate it with the class it belongs to, in the same file or a sibling partial file.
- Methods: `Log{WhatHappened}` — mirrors the familiar `LogInformation`/`LogError` style.
- Message placeholders: PascalCase (`{OrderId}`, `{CustomerId}`).

---

## Log levels — when to use each

| Level | When |
|---|---|
| `Trace` | Step-by-step execution detail; never enable in production |
| `Debug` | Diagnostics useful during development; disable in prod by default |
| `Information` | Normal application milestones (order placed, user registered) |
| `Warning` | Unexpected but recoverable situation (retry attempted, fallback used) |
| `Error` | Operation failed; needs investigation but app continues |
| `Critical` | System-wide failure; immediate action required |

---

## Guard expensive log arguments with IsEnabled

If computing a log argument is itself expensive (e.g. serialising an object, calling a method), guard it:

```csharp
if (logger.IsEnabled(LogLevel.Debug))
{
    logger.LogExpensiveDetail(ComputeExpensiveDetails());
}
```

With `[LoggerMessage]`, simple value types are already zero-cost when the level is disabled — the guard is only needed when the *argument expression itself* is expensive to evaluate.

---

## Log scopes

Use scopes to attach ambient context (e.g. a request ID or transaction ID) to all log entries within a logical operation:

```csharp
using (logger.BeginScope("Transaction {TransactionId}", transactionId))
{
    logger.LogOrderPlaced(order.Id);
    logger.LogPaymentProcessed(payment.Id);
}
```

Enable scope output in config:

```json
{
  "Logging": {
    "Console": { "IncludeScopes": true }
  }
}
```

---

## What NOT to do

```csharp
// ❌ String interpolation — allocates unconditionally, not structured
logger.LogInformation($"Order {order.Id} placed");

// ❌ Positional placeholders — not structured
logger.LogInformation("Order {0} placed", order.Id);

// ❌ Raw extension methods in hot paths — boxes value types, parses template each time
logger.LogInformation("Order {OrderId} placed", order.Id);

// ✅ Source-generated extension method
logger.LogOrderPlaced(order.Id);
```

---

## Complete example

```csharp
// OrderService.cs
public class OrderService(
    IOrderRepository repository,
    ILogger<OrderService> logger)
{
    public async Task<Result> PlaceOrderAsync(PlaceOrderCommand command, CancellationToken ct)
    {
        logger.LogPlacingOrder(command.CustomerId, command.ItemCount);

        try
        {
            var order = Order.Create(command);
            await repository.AddAsync(order, ct);
            logger.LogOrderPlaced(order.Id);
            return Result.Ok(order.Id);
        }
        catch (Exception ex)
        {
            logger.LogOrderFailed(command.CustomerId, ex);
            return Result.Fail("Order could not be placed");
        }
    }
}

// OrderServiceLogs.cs (or at the bottom of OrderService.cs)
internal static partial class OrderServiceLogs
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Debug,
        Message = "Placing order for customer {CustomerId} with {ItemCount} items")]
    public static partial void LogPlacingOrder(this ILogger logger, Guid customerId, int itemCount);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Order {OrderId} placed successfully")]
    public static partial void LogOrderPlaced(this ILogger logger, Guid orderId);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Error,
        Message = "Failed to place order for customer {CustomerId}")]
    public static partial void LogOrderFailed(this ILogger logger, Guid customerId, Exception ex);
}
```
