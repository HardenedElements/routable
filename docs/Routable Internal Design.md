# Routable Internal Design

This document describes the internal design of the `Routable` base library: how
its types relate, how a request flows through the pipeline, and the mechanisms
behind pattern matching and response handling. It is intended for contributors
and for authors of platform integrations. It covers only the base library;
companion libraries are documented separately.

## Design goals

Routable follows three constraints stated in the project README:

1. The base library stays small. It routes requests and hands them back to the
   caller. It does not provide views, content negotiation, or authentication.
2. The base library is platform agnostic. Platform specifics live in
   integrations, not in the core.
3. The library is extensible, and it leans on generics to achieve this.

These constraints explain most of the design decisions below, including the
heavy use of generic type parameters and the abstract context, request, and
response types.

## The three type parameters

Nearly every type in the library is generic over the same three parameters:

- `TContext` — the request context type.
- `TRequest` — the request type.
- `TResponse` — the response type.

The parameters are constrained to reference each other in a recursive pattern:

```csharp
where TContext : RoutableContext<TContext, TRequest, TResponse>
where TRequest : RoutableRequest<TContext, TRequest, TResponse>
where TResponse : RoutableResponse<TContext, TRequest, TResponse>
```

This is the curiously recurring generic pattern. Because each base type knows the
concrete derived types of its siblings, members can expose the concrete types
rather than the abstract bases. For example, `RoutableContext.Request` returns
`TRequest`, so a platform integration's context returns that integration's
concrete request without a cast. The cost is that the three parameters must be
threaded through every generic type in the library, which is why they appear
repeatedly across the source.

A platform integration closes these parameters by deriving concrete classes and
supplying itself as the type arguments. From that point on, consumer code works
with closed types and rarely sees the open generics.

## Type map

```
RoutableOptions<TContext, TRequest, TResponse>     configuration + pipeline host
  ├─ holds Routing collections, keyed by RoutableEventPipelines
  ├─ ResponseTypeHandlerCollection                 type -> output handler
  ├─ MIME type table                               extension -> mime type
  ├─ FeatureOptions                                Type -> arbitrary settings object
  └─ IRoutableLogger

Routing<TContext, TRequest, TResponse>             ordered list of routes
  └─ Route<TContext, TRequest, TResponse>          patterns + actions
       ├─ RoutePattern (0..n)                      match conditions
       │    ├─ MethodPattern
       │    ├─ PathPattern
       │    ├─ PathRegexPattern
       │    ├─ HostnamePattern
       │    ├─ PortPattern
       │    └─ FuncPattern
       └─ RouteAction (0..n)                        work performed on match
            ├─ BasicRouteAction
            └─ NestedRouteAction

RoutableContext<TContext, TRequest, TResponse>     per-request state
  ├─ RoutableRequest<...>                          request abstraction
  └─ RoutableResponse<...>                         response abstraction
```

## Abstraction layers in context, request, and response

The context, request, and response each come in two generic forms.

The first form (three type parameters) defines the platform-neutral contract. It
exposes an `Abstract` property whose type — `AbstractRequestAttributes`,
`AbstractResponseAttributes`, or `AbstractContextAttributes` — provides
primitives that read or write request and response data as plain strings, longs,
and dictionaries. This neutral surface is what lets handler code run unchanged
across integrations.

The second form adds further type parameters for strongly typed members. For
example:

```csharp
RoutableRequest<TContext, TRequest, TResponse, TMethod, TForm, TQuery,
    THeaders, ICookies, TContentLength, TBody>
```

These extra parameters let an integration expose its native types directly
(`req.Form`, `req.Headers`, and so on) for callers who need more than the neutral
surface. An integration typically derives from the second form, which inherits
the neutral contract from the first.

Members that not every platform can satisfy are declared `virtual` with a body
that throws `NotSupportedException`, rather than `abstract`. This lets an
integration implement only what it supports. Examples include
`RoutableContext.LocalEndPoint`, `RoutableContext.RemoteEndPoint`,
`RoutableContext.PlatformContext`, `RoutableRequest.UserHostAddress`,
`AbstractRequestAttributes.GetBodyAsString`, and `RoutableResponse.Reason`.

The base `RoutableContext` holds the small amount of state that is universal:
`Options`, `Error` (settable only inside the library), and a `CancellationToken`
that defaults to `CancellationToken.None`.

## The request pipeline

`RoutableOptions.InvokeRouting(TContext context)` is the entry point an
integration calls once per request. It drives the request through the event
pipeline stages defined by the `RoutableEventPipelines` enum and is the heart of
the library.

### Stage storage

Routing collections are stored in a dictionary keyed by pipeline stage:

```csharp
Dictionary<RoutableEventPipelines, IList<Routing<...>>> Routing
```

`GetEventPipelineRouting` lazily creates the list for a stage. Registration
methods (`AddRouting`, `AppendRoutingToEventPipeline`,
`PrependRoutingToEventPipeline`, `OnError`) add to these lists under a lock on
the dictionary, and append or prepend depending on the desired precedence.

### Stage execution

The private `InvokeRouting(eventPipeline, context, ignoreCompletion)` overload
runs a single stage:

1. Look up the routing collections for the stage. If none, return `false`.
2. Build a snapshot: for each routing collection, evaluate every route's
   `IsMatch` and keep the matching routes in a list.
3. Walk the snapshot in order, invoking each matching route. A route reports
   `true` when one of its actions handled the request.
4. The `ignoreCompletion` flag selects the completion semantics:
   - When `false`, the first route to report success ends the stage and the
     method returns `true`.
   - When `true`, success does not end the stage; the method records that a
     handler ran and continues. This is used for finalize and error stages,
     where every registered collection should get a chance to run.

### Overall flow

The public `InvokeRouting(context)` orchestrates the stages:

```
Initialize stage ─ handled? ─ yes ─┐
        │ no                        │
       Main stage ─ handled? ─ yes ─┤
        │ no                        │
        │                           ▼
        │                    Finalize stage (ignoreCompletion)
        │                    Response.Finalize()
        │                    return true
        ▼
FinalizeUnhandledRequests ─ handled? ─ yes ─► Finalize stage, Response.Finalize(), return true
        │ no
        ▼
     return false
```

Exceptions thrown anywhere in this flow are caught by a surrounding handler that:

1. Stores the exception on `context.Error`.
2. Calls `context.Response.ClearPendingWrites()` to discard partial output.
3. Runs the `RouteEventError` stage with `ignoreCompletion` set to `true`.
4. If an error route handled the request, finalizes the response and returns
   `true`; otherwise returns `false`.

### Finalization

`RoutableResponse` queues output as a list of writer delegates of type
`Func<RoutableContext<...>, Stream, Task>`. `Write` appends a writer;
`ClearPendingWrites` empties the list. The internal `Finalize()` forwards the
queued writers to the abstract `Finalize(writers)` that an integration
implements. The integration is responsible for opening a stream to the user agent
and invoking each writer in order. Deferring output to writer delegates lets the
library queue several writes, discard them on error, and leave the actual stream
mechanics to the platform.

## Pattern matching

`RoutePattern` is a small abstract class with a single `IsMatch(context)` method
and a protected `AddParameter` helper that forwards captured values to
`request.AddParameter`. A `Route` holds a list of patterns, and `Route.IsMatch`
returns true only when `Patterns.All` match.

The built-in patterns are:

- `MethodPattern` — compares the request method to a target verb, upper-cased on
  both sides.
- `PathPattern` — compares the request path to a target path. Both are
  lower-cased and stripped of a trailing slash, and a leading slash is ensured,
  so matching is case-insensitive and slash-tolerant. The root path `/` is a
  special case kept verbatim.
- `PathRegexPattern` — matches the path against a `Regex`. On a match, each named
  capture group (groups whose name is not purely numeric) is written to the
  request parameters via `AddParameter`. Regexes built from a string are compiled
  with `RegexOptions.Compiled`.
- `HostnamePattern` — compares `Uri.Host` to a lower-cased target host.
- `PortPattern` — compares `Uri.Port` to a target port.
- `FuncPattern` — evaluates an arbitrary `Predicate<TContext>`, the extension
  point behind `Route.Where`.

The fluent methods on `Route` (`Get`, `Post`, `Path`, `Host`, `Port`, `Method`,
and so on) construct these patterns and add them to the route. Method shorthands
delegate to `Method`, which optionally adds a `PathPattern` before the
`MethodPattern`.

## Route actions

`RouteAction` is abstract with a single `Invoke(context)` returning
`Task<bool>`, where the boolean signals whether the request was handled. Two
concrete actions exist.

`BasicRouteAction` wraps a user delegate. It defines explicit conversion
operators from four delegate shapes, which is how the `Route` action methods map
to a single action type:

| `Route` method | Delegate shape | Completion |
| --- | --- | --- |
| `Do` | `Action<TContext, TRequest, TResponse>` | Runs, then returns `true`. |
| `DoAsync` | `Func<..., Task>` | Awaits, then returns `true`. |
| `Try` | `Func<..., bool>` | Returns the delegate's result. |
| `TryAsync` | `Func<..., Task<bool>>` | Awaits and returns the result. |

`NestedRouteAction` wraps a delegate that returns a `Routing` (synchronously or
as a `Task`). On invocation it evaluates the returned routing's matching routes
against the same context and reports success if one handles the request. This
backs `Route.Nest` and allows route sets to be computed at request time.

`Route.Invoke` runs its actions in order and returns true at the first action
that reports success, mirroring the short-circuit behavior of the pipeline at the
route level.

## Response type handling

`resp.Write<T>(value)` resolves a `ResponseTypeHandler` for the value and invokes
it. The resolution logic lives in `ResponseTypeHandlerCollection`.

A handler is a delegate:

```csharp
delegate void ResponseTypeHandler<...>(RoutableContext<...> context, object value);
```

The collection maps `Type` to handler. The default handlers registered by
`RoutableOptions` are:

- `object` → string handler
- `string` → string handler
- `byte[]` → byte-array handler

The string handler encodes the value with `Options.StringEncoding` (calling
`ToString` for non-strings), sets the content length, and queues a writer. The
byte-array handler queues the bytes directly. An empty value sets content length
to zero and queues nothing.

When a value's type is not registered exactly, the collection finds a handler for
a related type using `Extensions.DistanceToType`. That extension walks the base
class chain (or the implemented interface chain, for interface targets) and
returns the number of steps from one type to another, or `-1` when unrelated.
The collection caches the computed distances per input type. Because the default
`object` handler is registered, reference types generally resolve to it as a last
resort.

`RoutableOptions` also exposes `EmptyResponseHandler` and `DefaultResponseHandler`
as fallbacks used by `Write<T>`: a `null` value goes to the empty handler, and a
type with no resolved handler falls back to the default handler, then the empty
handler, before `UnhandledResponseTypeException` is raised.

## Concurrency

The library expects concurrent requests against a shared `RoutableOptions`.
Routing registration and the per-stage routing lists are guarded by locks
(`lock(Routing)`, `lock(list)`), and the per-request snapshot in
`InvokeRouting` is taken under a lock on the routing collection. The
`ResponseTypeHandlerCollection` guards its dictionaries with a lock while reading
and writing handlers and cached distances. Per-request state (context, request,
response, queued writers, captured parameters) is not shared between requests and
is not separately synchronized.

## Logging

`IRoutableLogger` is a single-method interface that classifies messages with the
`LogClass` enum (`Debug`, `Informational`, `Warning`, `Error`, `Security`) and
accepts an optional exception and string data dictionary. The default
implementation, `DefaultConsoleLogger`, writes timestamped lines to standard
error and unwinds inner exceptions and the supplied data. `UseLogger` swaps in a
replacement.

## Extension points

The library is designed to be extended at several seams:

- `RouteFactory` — `RoutableOptions.RouteFactory` is overridable, so an
  integration can produce a `Route` subclass for every authored route.
- `RoutePattern` / `FuncPattern` — custom match conditions attach through
  `Route.Where`.
- `ResponseTypeHandler` — new response formats register through
  `ResponseTypeHandlers.Add`.
- The abstract context, request, and response types — the primary surface a
  platform integration implements.
- `IRoutableLogger` — log routing.
- Feature options — `Set`/`TryGetFeatureOptions` carry arbitrary settings objects
  keyed by type, letting optional subsystems publish configuration without the
  core depending on them.

## Target framework

The base library targets `netstandard2.0`, which keeps it usable across a wide
range of .NET hosts and keeps platform-specific dependencies out of the core.
