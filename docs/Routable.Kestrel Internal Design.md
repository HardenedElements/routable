# Routable.Kestrel Internal Design

This document describes the internal design of `Routable.Kestrel`, the
integration that runs the `Routable` base library on the Kestrel HTTP server. It
is intended for contributors and for readers comparing one integration against
another. It assumes familiarity with the base library, whose design is covered
in [Routable Internal Design](Routable%20Internal%20Design.md).

An integration's job is narrow: close the base library's three generic type
parameters with concrete classes, map those classes onto the host platform's
request and response objects, and feed requests into the pipeline. Everything
about matching, actions, and response handling stays in the base library.

## Closing the generic parameters

The base library is generic over `TContext`, `TRequest`, and `TResponse`, each
constrained to reference the others in the curiously recurring generic pattern.
`Routable.Kestrel` closes the loop by deriving three concrete classes and
supplying them as the type arguments to one another:

- `KestrelRoutableContext`
- `KestrelRoutableRequest`
- `KestrelRoutableResponse`

Each derives from the richer second generic form of its base type — the form
that adds parameters for strongly typed members — and pins those parameters to
Kestrel's own types. For example, `KestrelRoutableContext` derives from:

```csharp
RoutableContext<
    HttpContext,                 // TPlatformContext
    KestrelRoutableContext,      // TContext
    KestrelRoutableRequest,      // TRequest
    KestrelRoutableResponse,     // TResponse
    ClaimsPrincipal,             // TUser
    IDictionary<object, object>> // TPerRequestItems
```

`KestrelRoutableRequest` and `KestrelRoutableResponse` follow the same approach,
fixing the extra parameters to `IFormCollection`, `IQueryCollection`,
`IHeaderDictionary`, the cookie collection types, `Stream`, and so on. From this
point the rest of the library works with closed types, and consumer code rarely
sees an open generic.

`KestrelRouting` is a thin subclass of `Routing` closed over the same three
types, present mostly so callers have a short name and a collection-initializer
entry point rather than spelling out the full generic each time.

## Type map

```
KestrelRoutableOptions : RoutableOptions<KestrelRoutableContext, ...>
  ├─ ApplicationServices : IServiceProvider
  ├─ Invoke(context) ─► base InvokeRouting(context)
  └─ Logger ─► MicrosoftLoggingLogger (when an ILoggerFactory is available)

KestrelRoutableContext : RoutableContext<HttpContext, ...>
  ├─ PlatformContext : HttpContext
  ├─ Request  : KestrelRoutableRequest
  ├─ Response : KestrelRoutableResponse
  └─ Abstract : KestrelContextAbstractAttributes

KestrelRoutableRequest : RoutableRequest<...>
  ├─ PlatformRequest : HttpRequest
  └─ Abstract : KestrelRequestAbstractAttributes

KestrelRoutableResponse : RoutableResponse<...>
  ├─ PlatformResponse : HttpResponse
  └─ Abstract : KestrelResponseAbstractAttributes

IApplicationBuilderExtensions.UseRoutable    ─ registration + middleware bridge
IEndpointRouteBuilderExtensions.MapRoutable  ─ registration + endpoint bridge
RoutableOptionsServiceExtension.GetApplicationServices ─ service provider access
```

## Registration and the host bridges

The integration offers two entry points that attach Routable to the host. They
share the same shape — build a `KestrelRoutableOptions`, run the caller's
configuration action, then register a delegate that drives the pipeline — and
differ only in how that delegate is attached. Both delegates do the same work per
request: build a `KestrelRoutableContext` from the ambient `HttpContext` and call
`options.Invoke(routableContext)`.

`KestrelRoutableOptions.Invoke` is a one-line public wrapper over the base
library's `protected InvokeRouting(context)`. It exists because `InvokeRouting`
is protected on `RoutableOptions` and the bridge delegates need a public entry
point to call.

### The middleware bridge

`IApplicationBuilderExtensions.UseRoutable` is the original entry point. It
performs three steps:

1. Construct a `KestrelRoutableOptions`, passing the application's
   `IServiceProvider` taken from `IApplicationBuilder.ApplicationServices`.
2. Invoke the caller's configuration action against that options instance, which
   is where routing collections and other settings are registered.
3. Register an ASP.NET Core middleware delegate with `IApplicationBuilder.Use`.

The middleware delegate inspects the boolean result of `options.Invoke` to decide
what happens next:

- `true` — a route handled the request; the middleware returns and the response
  is complete.
- `false` — no route handled the request; the middleware calls `next()` so the
  rest of the ASP.NET Core pipeline can run.

This is what lets Routable sit alongside other middleware: an unhandled request
falls through rather than being terminated.

### The endpoint bridge

`IEndpointRouteBuilderExtensions.MapRoutable` registers Routable in ASP.NET
Core's endpoint routing system instead of as a raw middleware step. It takes the
`IServiceProvider` from `IEndpointRouteBuilder.ServiceProvider`, builds and
configures the options the same way, and then registers an endpoint:

- The pattern overload calls `endpoints.Map(pattern, …)`, so the host's matcher
  selects Routable only for requests under that route pattern.
- The parameterless overload calls `endpoints.MapFallback(…)`, so Routable is
  selected only when no other endpoint matches.

Both overloads return the `IEndpointConventionBuilder` from the underlying `Map`
call, so callers can attach endpoint metadata (authorization, CORS, and the like)
with the standard conventions.

Unlike the middleware bridge, the endpoint delegate does not consult the boolean
result of `options.Invoke`: an endpoint is terminal, so there is no `next()` to
call. A request the routes do not handle is left for Routable to finalize (for
example through the unhandled-request pipeline) rather than passing downstream.
Because each call constructs an independent `KestrelRoutableOptions`, several
Routable endpoints can coexist in one application, each with its own routes while
sharing the host's services.

## The request context

`KestrelRoutableContext` adapts an `HttpContext` to the base context contract.
Most members are direct projections:

| Base member | Backing source |
| --- | --- |
| `PlatformContext` | the `HttpContext` passed to the constructor |
| `HostingPlatform` | the constant `"kestrel"` |
| `LocalEndPoint` | `Connection.LocalIpAddress` / `LocalPort` as an `IPEndPoint` |
| `RemoteEndPoint` | `Connection.RemoteIpAddress` / `RemotePort` as an `IPEndPoint` |
| `User` | `HttpContext.User` |
| `PerRequestItems` | `HttpContext.Items` |
| `CancellationToken` | `HttpContext.RequestAborted` |

The constructor eagerly creates the request, response, and abstract-attributes
objects and holds them in fields, so the context, its request, and its response
share a lifetime and each can reach the others.

`Principal` accepts an `IPrincipal` to satisfy the base contract but stores it on
`HttpContext.User`, which is a `ClaimsPrincipal`. A value that is neither `null`
nor a `ClaimsPrincipal` is rejected with `NotSupportedException`, since Kestrel
has nowhere else to keep it.

`ClientCertificate` resolves the certificate from the TLS connection: it consults
the connection's client certificate and, failing that, the
`ITlsConnectionFeature` among the request features.

## Request and response abstraction

Each of the three classes exposes two surfaces: native members typed to Kestrel's
own classes, and an `Abstract` object that implements the platform-neutral
attribute contract from the base library.

`KestrelRoutableRequest` projects `Method`, `Form`, `Query`, `Headers`,
`Cookies`, `Body`, and `ContentLength` straight from the `HttpRequest`. `Uri` is
assembled lazily from the request's scheme, host, port, path, and query string
with a `UriBuilder`, and is cached after first use because the request line does
not change over a request's lifetime.

`KestrelRequestAbstractAttributes` implements the neutral request surface in
terms of those native members. Its `TryGetForm` guards against requests that do
not carry a form content type and against the exceptions Kestrel can raise when
parsing a malformed or oversized form, returning `false` rather than letting
those propagate. `GetBodyAsString` reads the body stream with a `StreamReader`,
honoring a caller-supplied encoding or detecting one, and leaves the stream open.

`KestrelRoutableResponse` projects `Status`, `Cookies`, `Headers`, `ContentType`,
`ContentLength`, and `Body` from the `HttpResponse`. The members that ASP.NET
Core does not let an application assign — for example the cookie and header
collection references, or the response body stream reference — have setters that
throw `NotSupportedException`, matching the base contract's pattern of declaring
optional members as throwing virtuals. `Reason` is not available on this platform.

`KestrelResponseAbstractAttributes` implements the neutral response surface.
`SetHeader` replaces any existing value for a header name rather than appending,
and `SetCookie` translates the neutral cookie parameters into ASP.NET Core's
`CookieOptions`.

## Finalization

The base library defers response output to a queue of writer delegates and calls
the integration's `Finalize(writers)` once the request is handled.
`KestrelRoutableResponse.Finalize` opens the response body stream and invokes each
queued writer in order, passing the context and the body stream. This is the
point where Routable's queued output becomes bytes on the wire. Up to this point
nothing is written, which is what lets the pipeline discard queued output on
error before running error routing.

## Service provider access

A Kestrel host owns a dependency injection container, and route handlers
frequently need it. `KestrelRoutableOptions` keeps the application's
`IServiceProvider` in `ApplicationServices`, captured at registration time.
`RoutableOptionsServiceExtension.GetApplicationServices` is an extension on the
base `RoutableOptions` type that downcasts to `KestrelRoutableOptions` and returns
that provider, throwing `InvalidOperationException` if the options instance is not
the Kestrel implementation. The extension lives on the base type so handlers that
hold only the base-typed options can still reach the provider without a manual
cast.

## Logging adapter

`MicrosoftLoggingLogger` adapts Routable's `IRoutableLogger` to the ASP.NET Core
`ILogger`. It maps each `LogClass` to a `LogLevel` (with both `Error` and
`Security` mapping to `Error`) and forwards the message, with the exception when
one is supplied. `KestrelRoutableOptions` installs it automatically when the
application's service provider can supply an `ILoggerFactory`, creating a logger
under the category `"routable"`. The supplemental string data dictionary that
`IRoutableLogger` accepts is not forwarded to the structured logging system.

## Target frameworks

`Routable.Kestrel` targets `net8.0`, `net9.0`, and `net10.0`. It references the
ASP.NET Core framework types (`HttpContext`, `HttpRequest`, `HttpResponse`, the
form, header, query, and cookie collections) and the `Microsoft.Extensions.Logging`
abstractions. The base `Routable` library, by contrast, targets `netstandard2.0`
and takes no platform dependency; the platform coupling is confined to this
integration.
