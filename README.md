# Propel Feature Flags for .NET

[![Build and Test](https://github.com/Treiben/Propel.FeatureFlags/actions/workflows/build.yml/badge.svg)](https://github.com/Treiben/Propel.FeatureFlags/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Propel.FeatureFlags.svg)](https://www.nuget.org/packages/Propel.FeatureFlags)
![.NET Standard](https://img.shields.io/badge/.NET%20Standard-2.0-5C2D91?logo=.net)
![.NET Core](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![C#](https://img.shields.io/badge/C%23-12.0-239120?logo=csharp)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)

A type-safe feature flag library for .NET that separates continuous delivery from release management. Developers define flags in code, product owners control releases through configuration. Supports modern .NET CORE (6+) applications as well as legacy .NET FULL FRAMEWORK (4.7.2+) applications.

> **CLI Tool:** [Propel CLI](https://github.com/Treiben/propel-cli) for terminal management and CI/CD

> **Dashboard:** [Propel Dashboard](https://github.com/Treiben/propel-dashboard) for web-based flag management


## Table of Contents

- [Overview](#overview)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Package Documentation](#package-documentation)
- [Evaluation Modes](#evaluation-modes)
- [Application vs Global Flags](#application-vs-global-flags)
- [Best Practices](#best-practices)
- [Examples](#examples)
- [Management Tools](#management-tools)


## Overview

### The Problem

Traditional feature flag implementations couple developers to release decisions:

```csharp
// Magic strings, no type safety, hard to find during cleanup, should we delete this, is this still used, is this a business rule?
if (config["is_something_enabled"] == "true")
{
    // New implementation
}
```

This makes developers responsible for release timing, rollout strategies, and configuration management instead of focusing on building features.

### The Solution

Propel separates concerns: developers define flags as strongly-typed classes, product owners configure release strategies through management [tools](#management-tools).

**Developer defines the flag once:**

```csharp
public class NewCheckoutFeatureFlag : FeatureFlagBase
{
    public NewCheckoutFeatureFlag()
        : base(key: "new-checkout",
               name: "New Checkout Flow",
               description: "Enhanced checkout with improved UX",
               onOfMode: EvaluationMode.Off) // Deploy disabled by default
    {
    }
}
```

**Developer uses the flag in code:**

```csharp
var flag = new NewCheckoutFeatureFlag();
if (await context.IsFeatureFlagEnabledAsync(flag))
{
    return NewCheckoutImplementation();
}
return LegacyCheckoutImplementation();
```

**Product owner configures release strategy (no developer involvement):**

- Schedule: Enable feature on specific date/time
- Time windows: Enable only during business hours
- User targeting: Enable for specific users or user groups
- Percentage rollout: Gradually roll out to 10%, 25%, 50%, 100% of users
- Targeting rules: Enable based on custom attributes (region, tier, department, etc.)

### Key Benefits

- **Type Safety**: Compile-time validation prevents runtime errors
- **Easy Maintenance**: Find-all-references works; no magic strings
- **Clean Code Hygiene**: Delete the flag class when done, compiler helps find all usage
- **Attribute-Based Flags**: Decorate methods with `[FeatureFlagged]` to keep business logic clean
- **Auto-Deployment**: Flags automatically register in database on application startup
- **Zero Configuration**: Works out of the box with sensible defaults

[⬆ Back to top](#table-of-contents)

## Installation

```bash
# Core library (required)
dotnet add package Propel.FeatureFlags

# ASP.NET Core integration
dotnet add package Propel.FeatureFlags.AspNetCore

# Database persistence (choose one)
dotnet add package Propel.FeatureFlags.Infrastructure.PostgresSql
dotnet add package Propel.FeatureFlags.Infrastructure.SqlServer

# Caching (optional but recommended)
dotnet add package Propel.FeatureFlags.Infrastructure.Redis

# AOP-style attributes (optional)
dotnet add package Propel.FeatureFlags.Attributes
```
[⬆ Back to top](#table-of-contents)

## Quick Start

### 1. Configure in Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
	.ConfigureFeatureFlags(config =>
		{
			config.RegisterFlagsWithContainer = true;   // automatically register all flags in the assembly with the DI container
			config.EnableFlagFactory = true;            // enable IFeatureFlagFactory for type-safe flag access

			var interception = config.Interception;
			interception.EnableHttpIntercepter = true;  // automatically add interceptors for attribute-based flags
		})
	.AddPostgreSqlFeatureFlags(builder.Configuration.GetConnectionString("DefaultConnection")!)
	.AddRedisCache(builder.Configuration.GetConnectionString("RedisConnection")!);

// optional: register your services with methods decorated with [FeatureFlagged] attribute
builder.Services.RegisterWithFeatureFlagInterception<INotificationService, NotificationService>();

var app = builder.Build();

// optional: ensure the feature flags database exists and schema is created
if (app.Environment.IsDevelopment())
{
	await app.InitializeFeatureFlagsDatabase();
}

// recommended: automatically add flags in the database at startup if they don't exist
await app.AutoDeployFlags();

// optional: add the feature flag middleware for global flags and context extraction
app.UseFeatureFlags();

app.Run();
```

### 2. Define a Feature Flag

```csharp
public class NewProductApiFeatureFlag : FeatureFlagBase
{
    public NewProductApiFeatureFlag()
        : base(key: "new-product-api",
               name: "New Product API",
               description: "Enhanced product API with improved performance",
               onOfMode: EvaluationMode.Off) // Start disabled
    {
    }
}
```

### 3. Use the Flag

**Direct evaluation:**

```csharp
app.MapGet("/products", async (HttpContext context) =>
{
    var flag = new NewProductApiFeatureFlag();
    
    if (await context.IsFeatureFlagEnabledAsync(flag))
    {
        return Results.Ok(GetProductsV2());
    }
    
    return Results.Ok(GetProductsV1());
});
```

**Using attributes for clean code:**

```csharp
public interface INotificationService
{
    Task<string> SendEmailAsync(string userId, string subject, string body);
    Task<string> SendEmailLegacyAsync(string userId, string subject, string body);
}

public class NotificationService : INotificationService
{
    [FeatureFlagged(type: typeof(NewEmailServiceFeatureFlag),
                    fallbackMethod: nameof(SendEmailLegacyAsync))]
    public virtual async Task<string> SendEmailAsync(string userId, string subject, string body)
    {
        // New implementation - called when flag is enabled
        return "Email sent using new service";
    }
    
    public virtual async Task<string> SendEmailLegacyAsync(string userId, string subject, string body)
    {
        // Fallback - called when flag is disabled
        return "Email sent using legacy service";
    }
}
```
[⬆ Back to top](#table-of-contents)

## Package Documentation

### Core Packages

| Package | Purpose | Documentation |
|---------|---------|---------------|
| **[Propel.FeatureFlags](./src/Propel.FeatureFlags/)** | Core library with flag definitions and evaluation logic | [README](./src/Propel.FeatureFlags/README.md) |
| **[Propel.FeatureFlags.AspNetCore](./src/Propel.FeatureFlags.AspNetCore/)** | ASP.NET Core middleware and HTTP extensions | [README](./src/Propel.FeatureFlags.AspNetCore/README.md) |
| **[Propel.FeatureFlags.Attributes](./src/Propel.FeatureFlags.Attributes/)** | AOP-style method decoration with `[FeatureFlagged]` | [README](./src/Propel.FeatureFlags.Attributes/README.md) |
| **[Propel.FeatureFlags.DependencyInjection.Extensions](./infrastructure/Propel.FeatureFlags.DependencyInjection.Extensions/)** | Advanced DI configuration and auto-deployment | [README](./infrastructure/Propel.FeatureFlags.DependencyInjection.Extensions/README.md) |

### Infrastructure Packages

| Package | Purpose | Documentation |
|---------|---------|---------------|
| **[Propel.FeatureFlags.PostgresSql](./infrastructure/Propel.FeatureFlags.PostgreSql/)** | PostgreSQL persistence provider | [README](./infrastructure/Propel.FeatureFlags.PostgreSql/README.md) |
| **[Propel.FeatureFlags.SqlServer](./infrastructure/Propel.FeatureFlags.SqlServer/)** | SQL Server persistence provider | [README](./infrastructure/Propel.FeatureFlags.SqlServer/README.md) |
| **[Propel.FeatureFlags.Redis](./infrastructure/Propel.FeatureFlags.Redis/)** | Redis distributed caching with two-level cache | [README](./infrastructure/Propel.FeatureFlags.Redis/README.md) |

### Key Features by Package

- **Core**: Type-safe flags, evaluation logic, multiple evaluation modes
- **AspNetCore**: Middleware, maintenance mode, context extraction from HTTP requests  
- **Attributes**: Clean code with automatic fallback methods
- **DI Extensions**: Auto-registration, auto-deployment, flag factory
- **Database Providers**: Schema management, connection optimization
- **Redis**: Two-level caching (L1 in-memory + L2 Redis), circuit breaker pattern

[⬆ Back to top](#table-of-contents)

## Evaluation Modes

Propel supports 9 evaluation modes that can be configured from the management dashboard:

| Mode | Description | Use Case |
|------|-------------|----------|
| `Off` | Flag is disabled | Default safe state, kill switches |
| `On` | Flag is enabled | Enable completed features |
| `Scheduled` | Time-based activation | Coordinated releases, marketing campaigns |
| `TimeWindow` | Daily/weekly time ranges | Business hours features, maintenance windows |
| `UserTargeted` | Specific user allowlists/blocklists | Beta testing, VIP access |
| `UserRolloutPercentage` | Percentage-based user rollout | Gradual rollouts, A/B testing |
| `TenantRolloutPercentage` | Percentage-based tenant rollout | Multi-tenant gradual rollouts |
| `TenantTargeted` | Specific tenant allowlists/blocklists | Tenant-specific features |
| `TargetingRules` | Custom attribute-based rules | Complex targeting (region, tier, etc.) |

**Note:** Developers define flags with `OnOffMode` set to either `On` or `Off`. All other evaluation modes are configured by product owners through the management dashboard.

[⬆ Back to top](#table-of-contents)

## Application vs Global Flags

### Application Flags

Application flags are defined in code and scoped to specific applications. They auto-deploy on startup.

```csharp
public class MyFeatureFlag : FeatureFlagBase
{
    public MyFeatureFlag()
        : base(key: "my-feature",
               name: "My Feature",
               description: "Description of my feature",
               onOfMode: EvaluationMode.Off)
    {
    }
}

// Usage
var flag = new MyFeatureFlag();
var isEnabled = await context.IsFeatureFlagEnabledAsync(flag);
```

### Global Flags

Global flags are created through the management dashboard and apply system-wide across all applications. Use them for:

- Maintenance mode
- API deprecation  
- System-wide kill switches

```csharp
// Global flags are evaluated by key only
var isEnabled = await globalFlagClient.IsEnabledAsync("api-maintenance");
```

**Important:** Global flags cannot be defined in code. They must be created through the dashboard.

[⬆ Back to top](#table-of-contents)

## Best Practices

### 1. Always Deploy Disabled

Always set `onOfMode: EvaluationMode.Off` when defining new flags:

```csharp
public class NewFeatureFlag : FeatureFlagBase
{
    public NewFeatureFlag()
        : base(key: "new-feature",
               name: "New Feature",
               description: "Description",
               onOfMode: EvaluationMode.Off) // ✅ Deploy first, release later
    {
    }
}
```

### 2. Feature Flags Control "How", Not "What"

Feature flags control how your application behaves technically, not what it does from a business perspective.

**❌ Wrong - Business Logic:**

```csharp
// DON'T use flags for business rules, permissions, or pricing
if (await IsFeatureFlagEnabled("premium-features"))
{
    return GetPremiumContent();
}
```

**✅ Correct - Technical Implementation:**

```csharp
// DO use flags to test new technical implementations
if (await IsFeatureFlagEnabled("new-pricing-algorithm"))
{
    return CalculatePricingV2(); // New algorithm
}
return CalculatePricingV1(); // Old algorithm
```

### 3. Clean Up Old Flags

Feature flags are temporary. Delete them when the feature has been fully rolled out:

1. Delete the flag class from your codebase
2. Remove all references (compiler will help you find them)
3. Delete the flag from the database through the management dashboard

### 4. Use Descriptive Names and Descriptions

Good flag definitions help everyone understand the flag's purpose:

```csharp
public class EnhancedRecommendationEngineFeatureFlag : FeatureFlagBase
{
    public EnhancedRecommendationEngineFeatureFlag()
        : base(
            key: "enhanced-recommendation-engine",
            name: "Enhanced Recommendation Engine",
            description: "Enables ML-based recommendation engine with improved accuracy. " +
                        "Falls back to collaborative filtering if disabled.",
            onOfMode: EvaluationMode.Off)
    {
    }
}
```

[⬆ Back to top](#table-of-contents)

## Examples

Complete working examples are available in the `/usage-demo` directory:

### [WebClientDemo](./usage-demo/DemoWebApi)

ASP.NET Core Web API demonstrating:
- Simple on/off flags
- Scheduled releases
- Time window flags
- Percentage rollouts with variations
- User targeting with custom attributes
- Attribute-based flags with interceptors

### [ConsoleAppDemo](./usage-demo/DemoWorker)

Console worker application demonstrating:
- Background service integration
- Direct flag evaluation via `IApplicationFlagClient`
- Using `IFeatureFlagFactory` for type-safe access
- Attribute-based flags without HTTP context

### [Legacy .NET Framework](./usage-demo/DemoLegacyApi)

Working with full .NET Framework applications ([documentation](./docs/legacy-dotnet-framework.md))

[⬆ Back to top](#table-of-contents)

## Management Tools

### 🎯 [Propel Dashboard](https://github.com/Treiben/propel-dashboard)

Web-based management interface for product owners and DevOps teams:

- **Visual Flag Management** - View all application flags deployed from code
- **Release Configuration** - Configure evaluation modes (scheduled, time windows, targeting, rollouts)
- **Targeting Rules** - Set up complex targeting based on custom attributes
- **Monitoring** - Track flag usage and expiration dates

**Quick Start:**
```bash
docker run -d \
  -p 8080:8080 \
  -e SQL_CONNECTION="Host=your-postgres;Database=propel;..." \
  propel/feature-flags-dashboard:latest
```

### 🔧 [Propel CLI](https://github.com/Treiben/propel-cli)

Command-line tool for automation and CI/CD pipelines:

- **Database Migrations** - Run schema migrations in pipelines
- **Flag Management** - Create, update, and delete flags from terminal
- **Configuration Export/Import** - Backup and restore flag configurations
- **Pipeline Integration** - Perfect for GitOps workflows

**Installation:**
```bash
dotnet tool install -g Propel.FeatureFlags.Cli
```

## Support
**Issues**: [GitHub Issues](https://github.com/Treiben/propel-feature-flags-csharp/issues)

## Contributing
Contributions are welcome! See [CONTRIBUTING.md](./CONTRIBUTING.md) for guidelines.

## License
Licensed under the Apache License, Version 2.0.
Copyright 2025 Tatyana Asriyan

[⬆ Back to top](#table-of-contents)
