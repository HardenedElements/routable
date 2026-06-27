# Routable

A lightweight, platform-agnostic request routing library for .NET.

Routable matches incoming requests against a set of routes and hands matching requests to the code you provide. The core library is entirely platform-independent — it knows nothing about HTTP servers, web hosts, or transport layers. Instead, it works with generic type parameters that a platform integration fills in with concrete context, request, and response types.

The entire system is built to be extended through simple extension methods. Add new route patterns, unify response handling across your codebase, or plug in entirely new subsystems — and they work everywhere Routable works. Extension methods are what make Routable powerful: a small core that grows with your application.

## Key Features

- **Platform-agnostic core** — generic type parameters (`TContext`, `TRequest`, `TResponse`) mean the same routing code works across any integration
- **Fluent route definition** — chain method, path, and custom patterns with collection initializer syntax
- **Rich pattern matching** — HTTP method, exact path, regex with named captures, host, port, and custom predicates
- **Multiple action types** — `Do` for always-handled actions, `Try` for conditional handling with middleware-like chaining
- **Event pipeline stages** — fine-grained control over request lifecycle with initialize, main, finalize, error, and unhandled-request pipelines
- **Pluggable response type handlers** — register handlers for any return type
- **Lightweight view engine** — file system, embedded resource, and custom resolvers with model binding, conditionals, loops, partial views, and parent-child template inheritance
- **Platform-neutral abstraction** — access request/response via `request.Abstract` / `response.Abstract` for portable code, or use native platform types directly

## Packages

| Package | Description |  | Documentation |
| --- | --- | --- | --- |
| [`Routable`](https://www.nuget.org/packages/Routable) | Platform-agnostic core routing library | [![NuGet](https://img.shields.io/nuget/v/Routable.svg)](https://www.nuget.org/packages/Routable) | [Usage](docs/Routable%20Usage.md) |
| [`Routable.Kestrel`](https://www.nuget.org/packages/Routable.Kestrel) | ASP.NET Core / Kestrel integration | [![NuGet](https://img.shields.io/nuget/v/Routable.Kestrel.svg)](https://www.nuget.org/packages/Routable.Kestrel) | [Usage](docs/Routable.Kestrel%20Usage.md) |
| [`Routable.Views.Simple`](https://www.nuget.org/packages/Routable.Views.Simple) | Lightweight HTML view engine | [![NuGet](https://img.shields.io/nuget/v/Routable.Views.Simple.svg)](https://www.nuget.org/packages/Routable.Views.Simple) | [Usage](docs/Routable.Views.Simple%20Usage.md) |

## Quick Start

```csharp
var builder = WebApplication.CreateSlimBuilder();
var app = builder.Build();

app.UseRoutable(options => options
    .AddRouting(new KestrelRouting(options)
    {
        _ => _.Get("/").Do((context, request, response) => response.Write("Hello World!")),
        _ => _.Get("/status").Do((context, request, response) => response.Write("ok"))
    }));

await app.RunAsync();
```

## Route Matching

Routes are defined inside a `Routing` collection. Each route is a set of match patterns plus one or more actions. A route matches only when every attached pattern matches.

```csharp
public sealed class ProductRouting : KestrelRouting
{
    public ProductRouting(RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> options)
        : base(options)
    {
        // HTTP method and exact path
        Add(_ => _.Get("/products").Do(OnProductList));
        Add(_ => _.Post("/products").Do(OnProductCreate));

        // Regex path with named capture groups
        Add(_ => _.Method("GET")
            .Path(new Regex(@"^/products/(?<id>\d+)$"))
            .Do(OnProductDetail));

        // Custom extension method — route only when the API version header matches
        Add(_ => _.Get("/api/data")
            .ApiVersion("2")
            .Do(OnApiData));
    }

    private void OnProductList(KestrelRoutableContext context, KestrelRoutableRequest request, KestrelRoutableResponse response)
    {
        response.Write("product list");
    }

    private void OnProductCreate(KestrelRoutableContext context, KestrelRoutableRequest request, KestrelRoutableResponse response)
    {
        response.Write("product created");
    }

    private void OnProductDetail(KestrelRoutableContext context, KestrelRoutableRequest request, KestrelRoutableResponse response)
    {
        var id = request.Parameters["id"];
        response.Write($"Product {id}");
    }

    private void OnApiData(KestrelRoutableContext context, KestrelRoutableRequest request, KestrelRoutableResponse response)
    {
        response.Write("API v2 response");
    }
}
```

### Available Patterns

| Method | Matches when |
| --- | --- |
| `Method(verb, path)` | The request method equals `verb`. Optionally adds a path pattern. |
| `Get`, `Post`, `Put`, `Delete`, `Head`, `Options`, `Trace` | Shorthand for `Method` with the corresponding verb. |
| `Path(string)` | The request path equals the given string (case-insensitive, trailing slash ignored). |
| `Path(Regex)` | The request path matches the regular expression. Named captures are saved to request parameters. |
| `Host(hostname)` | The request host equals `hostname`. |
| `Port(port)` | The request was received on `port`. |
| `Where(pattern)` | A custom `RoutePattern` matches. |

## Extending Routable with Extension Methods

The `Route` class exposes a fluent API that is designed to be extended. Every pattern and action method on `Route` is itself an extension point — you add the same way the library does, by writing extension methods on `Route<TContext, TRequest, TResponse>`.

This is how Routable stays small while growing with your application. Write an extension method once, and it becomes available on every route, in every routing class, across every pipeline stage.

### Adding a Custom Route Pattern

The built-in `FuncPattern` lets you attach an inline predicate, but a more reusable approach is a dedicated extension method. This example adds an `ApiVersion` pattern that matches a specific API version header:

```csharp
using Routable.Patterns;

public static class RouteExtensions
{
    public static Route<TContext, TRequest, TResponse> ApiVersion<TContext, TRequest, TResponse>(
        this Route<TContext, TRequest, TResponse> route, string version)
        where TContext : RoutableContext<TContext, TRequest, TResponse>
        where TRequest : RoutableRequest<TContext, TRequest, TResponse>
        where TResponse : RoutableResponse<TContext, TRequest, TResponse>
    {
        return route.Where(new FuncPattern<TContext, TRequest, TResponse>(
            context => context.Request.Abstract.TryGetHeader("X-Api-Version", out var values)
                && values?.Contains(version) == true));
    }
}
```

With this extension in place, any routing class can use `.ApiVersion("2")` in the fluent chain:

```csharp
Add(_ => _.Get("/api/data")
    .ApiVersion("2")
    .Do(OnApiData));
```

### Platform-Specific Extensions with `Try`

Extension methods can also target a specific platform integration. This `HasRole` extension is scoped to Kestrel and uses `Try` as an authorization guard — it returns `true` (handled) with a 403 when the user lacks the role, or `false` (not handled) when the user is authorized, letting the route continue to a subsequent `Do`:

```csharp
public static class KestrelRouteExtensions
{
    public static Route<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> HasRole(
        this Route<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> route, string role)
    {
        return route.Try((context, request, response) =>
        {
            if (context.User?.IsInRole(role) == true)
                return false; // authorized — let the route continue

            response.Status = 403;
            response.Write("Forbidden");
            return true; // handled — stop further processing
        });
    }
}
```

Usage in a routing class:

```csharp
Add(_ => _.Get("/admin/settings")
    .HasRole("Admin")
    .Do(OnAdminSettings));
```

The `Try` action is the key — it acts as middleware within the route chain. An unauthorized request is short-circuited with a 403, while an authorized request flows through to the `Do` handler.

### Unifying Response Handling

Extension methods on `RoutableResponse` let you standardize how your application writes responses:

```csharp
public static class ResponseExtensions
{
    public static void WriteJson<TContext, TRequest, TResponse>(
        this RoutableResponse<TContext, TRequest, TResponse> response, object value)
        where TContext : RoutableContext<TContext, TRequest, TResponse>
        where TRequest : RoutableRequest<TContext, TRequest, TResponse>
        where TResponse : RoutableResponse<TContext, TRequest, TResponse>
    {
        response.Abstract.ContentType = "application/json";
        response.Write(JsonSerializer.Serialize(value));
    }
}
```

Now every route handler can call `response.WriteJson(data)` instead of manually setting content type and serializing.

### Adding New Subsystems

Because every extension method targets the generic base types, a subsystem you write works with any platform integration. An extension that adds authentication, caching, or request validation to `RoutableOptions` is available whether you're running on Kestrel or any other integration — no platform-specific code required.

## Route Actions

Routes support different action types with distinct completion semantics:

```csharp
public sealed class UploadRouting : KestrelRouting
{
    public UploadRouting(RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> options)
        : base(options)
    {
        // Always handled — synchronous
        Add(_ => _.Get("/uploads/status").Do(OnUploadStatus));

        // Conditional — declines if content type doesn't match, letting other routes try
        Add(_ => _.Post("/uploads").TryAsync(OnUpload));
    }

    private void OnUploadStatus(KestrelRoutableContext context, KestrelRoutableRequest request, KestrelRoutableResponse response)
    {
        response.Write("ready");
    }

    private async Task<bool> OnUpload(KestrelRoutableContext context, KestrelRoutableRequest request, KestrelRoutableResponse response)
    {
        if (request.Abstract.ContentType != "application/octet-stream")
            return false;

        await StoreUpload(request);
        response.Write("stored");
        return true;
    }
}
```

| Action | Behaviour |
| --- | --- |
| `Do(action)` | Always handled. Returns `true` after execution. |
| `DoAsync(action)` | Async variant of `Do`. |
| `Try(action)` | Conditional. Returns `true` if handled, `false` to let other routes try. |
| `TryAsync(action)` | Async variant of `Try`. |

## Organizing Routes

Large applications organize routes into topic-focused classes, each handling a distinct domain:

```csharp
public sealed class ProductRouting : KestrelRouting
{
    public ProductRouting(RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> options)
        : base(options)
    {
        Add(_ => _.Get("/products").Do(OnProductList));
        Add(_ => _.Post("/products").Try(OnCreateProduct));
    }

    private void OnProductList(KestrelRoutableContext context, KestrelRoutableRequest request, KestrelRoutableResponse response)
    {
        response.Write("product list");
    }

    private bool OnCreateProduct(KestrelRoutableContext context, KestrelRoutableRequest request, KestrelRoutableResponse response)
    {
        // handler logic
        return true;
    }
}

public sealed class OrderRouting : KestrelRouting
{
    public OrderRouting(RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> options)
        : base(options)
    {
        Add(_ => _.Get("/orders").Do(OnOrderList));
        Add(_ => _.Post("/orders").Do(OnCreateOrder));
    }

    private void OnOrderList(KestrelRoutableContext context, KestrelRoutableRequest request, KestrelRoutableResponse response)
    {
        response.Write("order list");
    }

    private void OnCreateOrder(KestrelRoutableContext context, KestrelRoutableRequest request, KestrelRoutableResponse response)
    {
        response.Write("order created");
    }
}
```

Register multiple routing classes together:

```csharp
app.UseRoutable(options => options
    .AddRouting(new ProductRouting(options))
    .AddRouting(new OrderRouting(options))
    .AddRouting(new UploadRouting(options)));
```

Each routing class is evaluated in registration order. Routes within each class are also evaluated in the order they were added. The first route that handles the request stops further processing in the main stage.

## Request Lifecycle — Event Pipelines

Routable processes requests through a series of pipeline stages:

| Stage | Purpose |
| --- | --- |
| `RouteEventInitialize` | Runs before main routing (e.g., logging, request setup) |
| `RouteEventMain` | Primary routing stage |
| `RouteEventFinalizeUnhandledRequests` | Handles unmatched requests (e.g., 404 responses) |
| `RouteEventFinalize` | Runs after request is handled (e.g., cleanup, response headers) |
| `RouteEventError` | Exception handling |

```csharp
public sealed class LoggingRouting : KestrelRouting
{
    public LoggingRouting(RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> options)
        : base(options)
    {
        Add(_ => _.Try(OnLogRequest));
    }

    private bool OnLogRequest(KestrelRoutableContext context, KestrelRoutableRequest request, KestrelRoutableResponse response)
    {
        Console.WriteLine($"Request: {request.Uri}");
        return false; // continue to main stage
    }
}

public sealed class NotFoundRouting : KestrelRouting
{
    public NotFoundRouting(RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> options)
        : base(options)
    {
        Add(_ => _.Do(OnNotFound));
    }

    private void OnNotFound(KestrelRoutableContext context, KestrelRoutableRequest request, KestrelRoutableResponse response)
    {
        response.Status = 404;
        response.Write("Not found");
    }
}

public sealed class ErrorRouting : KestrelRouting
{
    public ErrorRouting(RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> options)
        : base(options)
    {
        Add(_ => _.Do(OnError));
    }

    private void OnError(KestrelRoutableContext context, KestrelRoutableRequest request, KestrelRoutableResponse response)
    {
        response.Status = 500;
        response.Write($"Error: {context.Error?.Message}");
    }
}
```

Register routing classes on specific pipeline stages:

```csharp
options
    .AddRouting(RoutableEventPipelines.RouteEventInitialize, new LoggingRouting(options))
    .AddRouting(new ProductRouting(options))
    .AddRouting(new OrderRouting(options))
    .AddRouting(RoutableEventPipelines.RouteEventFinalizeUnhandledRequests, new NotFoundRouting(options))
    .OnError(new ErrorRouting(options));
```

## ASP.NET Core Integration

`Routable.Kestrel` provides two ways to integrate with ASP.NET Core:

### Middleware Registration (`UseRoutable`)

Routable as middleware — unhandled requests fall through to downstream middleware.

```csharp
app.UseRoutable(options => options
    .AddRouting(new ProductRouting(options))
    .AddRouting(new OrderRouting(options)));
```

### Endpoint Registration (`MapRoutable`)

Routable as an endpoint — integrates with ASP.NET Core endpoint routing, supports route patterns and endpoint metadata.

```csharp
app.UseEndpoints(endpoints => endpoints
    .MapRoutable("/api/{**catchAll}", options => options
        .AddRouting(new ApiRouting(options)))
    .RequireAuthorization());
```

Multiple independent Routable instances can coexist, each with its own routes, sharing the same DI container.

## Views — `Routable.Views.Simple`

The view engine provides lightweight HTML rendering with model binding, conditionals, loops, partial views, and template inheritance.

### File System Views

```csharp
public sealed class HomeRouting : KestrelRouting
{
    public HomeRouting(RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> options)
        : base(options)
    {
        Add(_ => _.Get("/").DoAsync(OnIndex));
    }

    private async Task OnIndex(KestrelRoutableContext context, KestrelRoutableRequest request, KestrelRoutableResponse response)
    {
        await response.WriteViewAsync("index", new { Title = "Home", Items = GetItems() });
    }
}
```

Configure the view options in the application setup:

```csharp
options.UseFileSystemViews(config =>
{
    config.AddSearchPath("views");
    config.OnUnresolvedModelValue((expr, paths, model) => $"[unresolved: {expr}]");
});
```

### Embedded Views

```csharp
options.UseEmbeddedViews(config =>
    config.AddAssembly(typeof(Program).Assembly, "MyApp.EmbeddedViews"));
```

### Template Syntax

```html
<!DOCTYPE html>
<html>
<head>
    @Include(shared/head)
</head>
<body>
    @IfSet(Title)
    <h1>@Model.Title</h1>
    @EndIfSet

    @IfSet(Items)
    <ul>
        @ForEach(Items)
        <li>@Model.Name</li>
        @EndForEach
    </ul>
    @EndIfSet
</body>
</html>
```

Template features:

- `@Model` — access model properties
- `@IfSet` / `@EndIfSet` — conditional rendering
- `@ForEach` / `@EndForEach` — iteration over collections
- `@Include` — partial views
- `@Parent` / `@Child` — parent-child template inheritance
- Custom expressions — extensible via `CustomExpression` with Superpower parser combinators

## Platform-Neutral Abstraction

Each request and response exposes both a platform-neutral surface and direct access to native platform types:

```csharp
// Platform-neutral — works across any integration
if (request.Abstract.TryGetHeader("Authorization", out var auth)) { /* ... */ }
response.Abstract.StatusCode = 200;
response.Abstract.ContentType = "application/json";

// Platform-specific — direct access to ASP.NET Core types
var files = request.Form.Files;
response.Headers["Cache-Control"] = "no-store";
```

The `.Abstract` property provides a consistent API for portable handler code, while native members give full access to platform-specific capabilities when needed.

