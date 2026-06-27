# Routable.Kestrel Usage

`Routable.Kestrel` connects the platform agnostic `Routable` base library to the
Kestrel HTTP server. It supplies the concrete context, request, and response
types that Routable leaves open, and it plugs Routable into an ASP.NET Core
application pipeline. With the integration in place, requests arriving on Kestrel
are matched against your routes and handed to your code.

There are two ways to plug Routable in, and they are additive — use either, or
both in the same application:

- **`UseRoutable`** installs Routable as a middleware step. An unhandled request
  falls through to the next middleware, so Routable composes with the rest of the
  pipeline.
- **`MapRoutable`** registers Routable as an endpoint in ASP.NET Core's endpoint
  routing system, so it can sit behind a route pattern and carry endpoint
  metadata.

Either way, Routable performs its own request matching internally; the difference
is only in how the integration is attached to the host. Pick whichever fits how
you prefer to compose your application.

This document covers the surface added by `Routable.Kestrel`. The routing model
itself — patterns, actions, response handlers, event pipelines, and options — is
defined by the base library and described in [Routable Usage](Routable%20Usage.md).
Read that document first; the material here builds on it.

## Closing the generic types

The base library is generic over three type parameters, `TContext`, `TRequest`,
and `TResponse`. `Routable.Kestrel` fixes them to concrete classes:

| Base parameter | Kestrel type |
| --- | --- |
| `TContext` | `KestrelRoutableContext` |
| `TRequest` | `KestrelRoutableRequest` |
| `TResponse` | `KestrelRoutableResponse` |

Wherever the base documentation shows the open generics, substitute these three
types. The options instance you configure is
`RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse>`,
and the routing collection is `KestrelRouting`.

## Registration with UseRoutable

Add Routable to the application pipeline with `UseRoutable`, an extension on
`IApplicationBuilder`. It takes an action that configures the options instance.
The example below uses ASP.NET Core's minimal hosting model:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(8080));

var app = builder.Build();

app.UseRoutable(options => options
    .AddRouting(new KestrelRouting(options) {
        _ => _.Get("/").Do((ctx, req, resp) => resp.Write("Hello World!"))
    })
);

await app.RunAsync();
```

`UseRoutable` creates a `KestrelRoutableOptions`, runs your configuration action
against it, and installs a middleware step. For each request the middleware
builds a `KestrelRoutableContext` from the ASP.NET Core `HttpContext` and runs
the routing pipeline. When a route handles the request, the middleware stops.
When no route handles it, control passes to the next middleware in the pipeline,
so Routable composes with other ASP.NET Core middleware.

`WebApplication` implements `IApplicationBuilder`, so `UseRoutable` is available
directly on `app` with no extra adaptation.

## Registration with MapRoutable

`MapRoutable` is an extension on `IEndpointRouteBuilder` that registers Routable
as an endpoint, so it participates in ASP.NET Core's endpoint routing alongside
controllers, minimal API endpoints, and anything else mapped on the host. This is
additive: it does not replace `UseRoutable`, and Routable still does its own
matching once the endpoint is selected.

There are two overloads. The first maps Routable behind a route pattern, so the
host routes only matching requests to it:

```csharp
app.UseRouting();
app.UseEndpoints(endpoints => endpoints
    .MapRoutable("/api/{**slug}", options => options
        .AddRouting(new KestrelRouting(options) {
            _ => _.Get("/api/status").Do((ctx, req, resp) => resp.Write("ok"))
        })
    )
);
```

A catch-all token such as `{**slug}` hands every request under the prefix to
Routable; use `/{**slug}` to route everything. The second overload maps Routable
as a fallback, selected only when no other endpoint matches:

```csharp
app.UseEndpoints(endpoints => endpoints
    .MapRoutable(options => options
        .AddRouting(new MyRouting(options))
    )
);
```

Both overloads build a `KestrelRoutableOptions` from the endpoint builder's
`ServiceProvider`, run your configuration action, and register an endpoint whose
delegate builds a `KestrelRoutableContext` and runs the routing pipeline.

Unlike the middleware form, an endpoint is **terminal**: once the host selects
the Routable endpoint there is no fall-through to later middleware. A request that
Routable's own routes do not handle is left for Routable to finalize — for
example through the `RouteEventFinalizeUnhandledRequests` pipeline described in
the base documentation — rather than passing to anything downstream. Choose the
route pattern accordingly: requests that should be handled elsewhere should not
match the pattern in the first place.

Because `MapRoutable` builds an independent `KestrelRoutableOptions` per call, an
application can map several Routable instances on different patterns, each with
its own routes, all sharing the host's services and pipeline.

Both overloads return the endpoint's `IEndpointConventionBuilder`, so endpoint
metadata can be attached in the usual way:

```csharp
app.UseEndpoints(endpoints => endpoints
    .MapRoutable("/api/{**slug}", options => options.AddRouting(new MyRouting(options)))
    .RequireAuthorization()
    .RequireCors("api")
);
```

This is one way to apply concerns like authorization and CORS, not the only way.
The same concerns can be configured as middleware around `UseRoutable`
(`services.AddCors(...)` with `app.UseCors(...)`, `app.UseAuthentication()` /
`app.UseAuthorization()`, and so on), which applies to both registration styles.
Use whichever composition you prefer.

## KestrelRouting

`KestrelRouting` is the `Routing` collection closed over the Kestrel types. Use
it anywhere the base library expects a routing collection. It supports
collection initializer syntax, so a set of routes can be written as a list of
builder lambdas:

```csharp
new KestrelRouting(options) {
    _ => _.Get("/status").Do((ctx, req, resp) => resp.Write("ok")),
    _ => _.Post("/submit").Try(OnSubmit)
};
```

You can also derive from `KestrelRouting` and add routes in the constructor,
which keeps a set of related routes together and gives them access to private
handler methods:

```csharp
public sealed class MyRouting : KestrelRouting
{
    public MyRouting(RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> options)
        : base(options)
    {
        Add(_ => _.Get("/test").Do((ctx, req, resp) => resp.Write("Hello World!")));
        Add(_ => _.Post("/test").Try(OnTestPost));
    }

    private bool OnTestPost(KestrelRoutableContext ctx, KestrelRoutableRequest req, KestrelRoutableResponse resp)
    {
        if(req.Form.TryGetValue("my-parameter", out var value)) {
            resp.Write($"Value: {value.FirstOrDefault() ?? "<null>"}");
            return true;
        }
        return false;
    }
}

// registration
options.AddRouting(new MyRouting(options));
```

The pipeline stages, error routing, and finalize routing described in the base
documentation all work with `KestrelRouting`:

```csharp
options
    .AddRouting(RoutableEventPipelines.RouteEventInitialize, new KestrelRouting(options) {
        _ => _.TryAsync(async (ctx, req, resp) => {
            // runs before the main stage; return false to keep processing.
            return false;
        })
    })
    .AddRouting(new MyRouting(options))
    .OnError(new KestrelRouting(options) {
        _ => _.Do((ctx, req, resp) => {
            resp.Status = 500;
            resp.Write(ctx.Error?.Message);
        })
    });
```

## The request context

`KestrelRoutableContext` carries everything the base context provides, backed by
the ASP.NET Core `HttpContext`. The platform-neutral members behave as the base
documentation describes; the members below expose Kestrel specifics.

| Member | Type | Description |
| --- | --- | --- |
| `PlatformContext` | `HttpContext` | The underlying ASP.NET Core context. |
| `HostingPlatform` | `string` | Always `"kestrel"`. |
| `Request` | `KestrelRoutableRequest` | The request abstraction. |
| `Response` | `KestrelRoutableResponse` | The response abstraction. |
| `LocalEndPoint` | `EndPoint` | The local connection endpoint. |
| `RemoteEndPoint` | `EndPoint` | The remote connection endpoint. |
| `ClientCertificate` | `X509Certificate2` | The client certificate from the TLS connection, when one was presented. |
| `Principal` | `IPrincipal` | The request principal. Assigned values must be a `ClaimsPrincipal`. |
| `User` | `ClaimsPrincipal` | The `HttpContext.User`. |
| `PerRequestItems` | `IDictionary<object, object>` | The `HttpContext.Items` dictionary. |
| `CancellationToken` | `CancellationToken` | The request's `RequestAborted` token. |

`PlatformContext` is the way down to the full ASP.NET Core surface when a handler
needs something the abstraction does not expose:

```csharp
_ => _.Get("/trace").Do((ctx, req, resp) => {
    var traceId = ctx.PlatformContext.TraceIdentifier;
    resp.Write(traceId);
});
```

Per-request items map onto `HttpContext.Items`, so values stored there are also
visible to other middleware on the same request, and the reverse:

```csharp
ctx.PerRequestItems["Marker"] = Guid.NewGuid();
```

## The request

`KestrelRoutableRequest` exposes the platform-neutral surface through
`req.Abstract` and the native Kestrel types through its strongly typed members.

| Member | Type | Description |
| --- | --- | --- |
| `PlatformRequest` | `HttpRequest` | The underlying ASP.NET Core request. |
| `Method` | `string` | The HTTP method. |
| `Uri` | `Uri` | The request URI, assembled from scheme, host, path, and query. |
| `Form` | `IFormCollection` | The submitted form, for form content types. |
| `Query` | `IQueryCollection` | The query string values. |
| `Headers` | `IHeaderDictionary` | The request headers. |
| `Cookies` | `IRequestCookieCollection` | The request cookies. |
| `Body` | `Stream` | The request body stream. |
| `ContentLength` | `long?` | The content length, when present. |
| `UserHostAddress` | `string` | The remote address as a string. |
| `Parameters` | read-only dictionary | Values captured by regular expression path patterns. |

The native members give direct access to Kestrel's own types:

```csharp
_ => _.Post("/upload").TryAsync(async (ctx, req, resp) => {
    if(req.Form.Files.Count == 0) {
        return false;
    }
    foreach(var file in req.Form.Files) {
        await Save(file);
    }
    resp.Write("stored");
    return true;
});
```

The neutral surface on `req.Abstract` covers method, content length, content
type, headers, cookies, query, form, and the request body as a string. It reads
the same across integrations:

```csharp
if(req.Abstract.TryGetHeader("Authorization", out var auth)) { /* ... */ }
string body = await req.Abstract.GetBodyAsString(); // UTF-8 by default
```

`TryGetForm` returns `false` for requests that do not carry a form content type,
so it is safe to call without first checking the content type.

## The response

`KestrelRoutableResponse` writes through the ASP.NET Core `HttpResponse`. Output
queued with `Write` is emitted to the response body when the request is
finalized, as described in the base documentation.

| Member | Type | Description |
| --- | --- | --- |
| `PlatformResponse` | `HttpResponse` | The underlying ASP.NET Core response. |
| `Status` | `int` | The status code. |
| `Cookies` | `IResponseCookies` | The response cookie collection. |
| `Headers` | `IHeaderDictionary` | The response headers. |
| `ContentType` | `string` | The content type. |
| `ContentLength` | `long?` | The content length. |
| `Body` | `Stream` | The response body stream. |

Status, headers, and cookies can be set through the native members or through
the neutral `resp.Abstract` surface:

```csharp
// native
resp.Status = 404;
resp.Headers["Cache-Control"] = "no-store";
resp.Cookies.Append("session", token);

// neutral
resp.Abstract.StatusCode = 404;
resp.Abstract.SetHeader("Cache-Control", "no-store");
resp.Abstract.SetCookie("session", token, DateTime.UtcNow.AddHours(1),
    httpOnly: true, isSecure: true, domain: null, path: "/");
```

Redirects are issued with `Redirect`:

```csharp
_ => _.Get("/old").DoAsync((ctx, req, resp) => resp.Redirect("/new"));
```

## Application services

A Kestrel application has a dependency injection container. Both `UseRoutable`
and `MapRoutable` capture the application's `IServiceProvider` when building the
options instance, and `GetApplicationServices` retrieves it so route handlers can
resolve services:

```csharp
_ => _.Get("/report").Try((ctx, req, resp) => {
    var logger = options.GetApplicationServices()
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("sample");
    logger.LogInformation("report requested");
    return false;
});
```

The same service provider is available through `ctx.PlatformContext.RequestServices`
inside a handler when you want request-scoped resolution.

## Logging

When the application's service provider supplies an `ILoggerFactory`,
`KestrelRoutableOptions` installs a logger named `"routable"` that forwards
Routable's log messages to the ASP.NET Core logging system. Routable's
`LogClass` values map to log levels as follows:

| `LogClass` | Log level |
| --- | --- |
| `Debug` | `Debug` |
| `Informational` | `Information` |
| `Warning` | `Warning` |
| `Error` | `Error` |
| `Security` | `Error` |

No configuration is required to enable this; it follows from registering logging
in the host. You can still replace the logger with `UseLogger` as described in
the base documentation.

## Companion libraries

`Routable.Kestrel` provides only the Kestrel integration. View and JSON support
come from separate companion libraries that build on the response type handler
mechanism in the base library and work the same once the integration is in
place. They are documented separately.
