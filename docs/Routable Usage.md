# Routable Usage

Routable is a platform agnostic request routing library. It matches incoming
requests against a set of routes and hands matching requests to the code you
provide. It does not implement views, content negotiation, or authentication on
its own; it routes a request and returns control to you.

Because Routable is platform agnostic, the base library does not listen for
requests by itself. A platform integration supplies the concrete request,
response, and context types and feeds requests into Routable. The examples below
use the generic type parameters `TContext`, `TRequest`, and `TResponse` to stand
in for whatever a platform integration provides. In real code these are the
concrete types exported by your chosen integration.

## Core concepts

| Concept | Type | Role |
| --- | --- | --- |
| Options | `RoutableOptions<TContext, TRequest, TResponse>` | Central configuration. Holds routing collections, response handlers, MIME types, feature options, and the logger. |
| Routing | `Routing<TContext, TRequest, TResponse>` | An ordered collection of routes. |
| Route | `Route<TContext, TRequest, TResponse>` | A set of match patterns plus one or more actions to run when matched. |
| Pattern | `RoutePattern<TContext, TRequest, TResponse>` | A single match condition (method, path, host, port, or a predicate). |
| Action | `RouteAction<TContext, TRequest, TResponse>` | The work performed when a route matches. |
| Context | `RoutableContext<TContext, TRequest, TResponse>` | Per-request state, including the request and response. |

The three type parameters travel together throughout the API. A platform
integration fixes them to concrete types, so your own code generally refers to
those concrete types rather than the open generics.

## Defining routes

Routes live inside a `Routing` collection. Add a route by passing a builder
action to `Add`. The builder receives a fresh `Route` and configures it with a
fluent chain of pattern and action methods.

```csharp
var routing = new Routing<TContext, TRequest, TResponse>(options);

routing.Add(route => route
    .Get("/")
    .Do((ctx, req, resp) => resp.Write("Hello World!")));

routing.Add(route => route
    .Post("/submit")
    .Try(OnSubmit));
```

Many integrations provide a `Routing` subclass that accepts collection
initializer syntax, letting you express the same thing as a list of builders:

```csharp
new SomeRouting(options) {
    route => route.Get("/").Do((ctx, req, resp) => resp.Write("Hello World!")),
    route => route.Post("/submit").Try(OnSubmit)
};
```

You can also derive your own class from a `Routing` type and add routes in its
constructor, which keeps related routes together and gives them access to
private handler methods:

```csharp
public sealed class MyRouting : SomeRouting
{
    public MyRouting(RoutableOptions<TContext, TRequest, TResponse> options) : base(options)
    {
        Add(route => route.Get("/test").Do((ctx, req, resp) => resp.Write("Hello World!")));
        Add(route => route.Post("/test").Try(OnTestPost));
    }

    private bool OnTestPost(TContext ctx, TRequest req, TResponse resp)
    {
        if(req.Abstract.TryGetForm("my-parameter", out var value)) {
            resp.Write($"Value: {value.FirstOrDefault() ?? "<null>"}");
            return true;
        }
        return false;
    }
}
```

## Matching patterns

Pattern methods restrict when a route matches. A route matches a request only
when every pattern attached to it matches. Patterns can be chained in any order.

| Method | Matches when |
| --- | --- |
| `Method(verb, path = null)` | The request method equals `verb`. If `path` is supplied, a path pattern is also added. |
| `Get`, `Post`, `Put`, `Delete`, `Head`, `Options`, `Trace` | Shorthand for `Method` with the corresponding verb. Each accepts an optional path. |
| `Path(string path)` | The request path equals `path`. Matching is case-insensitive and ignores a trailing slash. |
| `Path(Regex pattern)` | The request path matches the regular expression. Named capture groups are written to the request parameters. |
| `Host(string hostname)` | The request host equals `hostname`. |
| `Port(int port)` | The request was received on `port`. |
| `Where(RoutePattern pattern)` | A custom pattern matches. Use this to attach your own predicate. |

A method shorthand with a path is the common case:

```csharp
route.Get("/products");
route.Post("/products");
route.Delete("/products/all");
```

### Path parameters

A regular expression path pattern extracts named capture groups into
`request.Parameters`, a read-only dictionary keyed by group name:

```csharp
routing.Add(route => route
    .Method("GET")
    .Path(new Regex(@"^/products/(?<id>\d+)$"))
    .Do((ctx, req, resp) => {
        var id = req.Parameters["id"];
        resp.Write($"Product {id}");
    }));
```

### Custom predicates

`Where` accepts any `RoutePattern`. `FuncPattern` wraps a predicate over the
context, which is useful for conditions the built-in patterns do not cover:

```csharp
route.Where(new FuncPattern<TContext, TRequest, TResponse>(
    ctx => ctx.Request.Abstract.TryGetHeader("X-Api-Version", out var v) && v.Contains("2")));
```

## Route actions

An action runs when a route matches. Routes can hold several actions; they run in
order until one reports that it handled the request. The action methods differ in
signature and in how they signal completion.

| Method | Signature | Handled when |
| --- | --- | --- |
| `Do(Action<...>)` | Synchronous, returns nothing | Always treated as handled. |
| `DoAsync(Func<..., Task>)` | Asynchronous, returns `Task` | Always treated as handled once the task completes. |
| `Try(Func<..., bool>)` | Synchronous, returns `bool` | Handled only when it returns `true`. |
| `TryAsync(Func<..., Task<bool>>)` | Asynchronous, returns `Task<bool>` | Handled only when the result is `true`. |
| `Nest(Func<..., Routing>)` | Returns a `Routing` (or a `Task<Routing>`) | Handled when one of the nested routes handles the request. |

`Try` and `TryAsync` let a route inspect the request and decline it. When an
action returns `false`, Routable continues to the next action, then the next
matching route. This makes it possible to layer several handlers on the same
path and let each decide whether it applies:

```csharp
routing.Add(route => route.Post("/upload").TryAsync(async (ctx, req, resp) => {
    if(req.Abstract.ContentType != "application/octet-stream") {
        return false; // let another route try
    }
    await StoreUpload(req);
    resp.Write("stored");
    return true;
}));
```

### Nested routing

`Nest` returns a second `Routing` collection that is evaluated against the same
request. This allows a route to compute a set of sub-routes at request time, for
example after resolving a tenant or loading configuration:

```csharp
routing.Add(route => route.Path(new Regex("^/admin")).Nest((ctx, req, resp) => {
    var inner = new Routing<TContext, TRequest, TResponse>(ctx.Options);
    inner.Add(r => r.Get("/admin/status").Do((c, rq, rs) => rs.Write("ok")));
    return inner;
}));
```

## Writing responses

The response object accepts content through `Write`. The most common overload
takes a value and selects a response handler based on the value's type:

```csharp
resp.Write("a string body");          // written using the configured string encoding
resp.Write(File.ReadAllBytes("x"));   // written as raw bytes
```

Out of the box, `string`, `byte[]`, and `object` are handled. A `null` value
produces an empty response. Other types are converted with their `ToString`
representation unless you register a handler for them (see below). Writing a type
with no applicable handler raises `UnhandledResponseTypeException`.

For full control over the output stream, pass a writer function. The function is
invoked after the request has been finalized and receives the destination
stream:

```csharp
resp.Write(async (ctx, stream) => {
    var bytes = Encoding.UTF8.GetBytes("streamed");
    await stream.WriteAsync(bytes, 0, bytes.Length);
});
```

Multiple writes are queued and emitted in order. `ClearPendingWrites` discards
any queued writers.

Redirects are issued with `Redirect`:

```csharp
await resp.Redirect("/login");
```

### Status, headers, and cookies

The response exposes platform-neutral attributes through `resp.Abstract` and
strongly typed members through the response itself. Use `Abstract` when you want
code that works the same across integrations:

```csharp
resp.Abstract.StatusCode = 404;
resp.Abstract.ContentType = "application/json";
resp.Abstract.SetHeader("Cache-Control", "no-store");
resp.Abstract.SetCookie("session", token, DateTime.UtcNow.AddHours(1),
    httpOnly: true, isSecure: true, domain: null, path: "/");
```

## Reading requests

The request exposes both a platform-neutral surface through `req.Abstract` and
strongly typed members. The neutral surface covers the data most handlers need:

```csharp
string method = req.Abstract.Method;
long length = req.Abstract.ContentLength;
string type = req.Abstract.ContentType;

if(req.Abstract.TryGetHeader("Authorization", out var auth)) { /* ... */ }
if(req.Abstract.TryGetCookie("session", out var session)) { /* ... */ }
if(req.Abstract.TryGetQuery("page", out var page)) { /* ... */ }
if(req.Abstract.TryGetForm("email", out var email)) { /* ... */ }

string body = await req.Abstract.GetBodyAsString(); // UTF-8 by default
```

`req.Uri` exposes the request URI, and `req.Parameters` holds values captured by
regular expression path patterns. Strongly typed members such as `req.Form`,
`req.Query`, `req.Headers`, and `req.Body` expose the underlying platform types
for cases where the neutral surface is not enough.

Some members are only meaningful on certain platforms. Where an integration does
not support a member, accessing it raises `NotSupportedException`. The members
that behave this way include `req.UserHostAddress`, `ctx.LocalEndPoint`,
`ctx.RemoteEndPoint`, `ctx.PlatformContext`, and `resp.Reason`.

## Request context

The context ties a request and response together and carries per-request state.

```csharp
ctx.Options          // the RoutableOptions in effect
ctx.Request          // the request
ctx.Response         // the response
ctx.Error            // the exception caught during processing, if any
ctx.Principal        // the principal associated with the request
ctx.CancellationToken// signals that the request should be aborted, when supported
ctx.HostingPlatform  // a string naming the integration that produced the context
ctx.ClientCertificate// the client certificate used to authenticate, when present
```

`ctx.Abstract` provides platform-neutral per-request storage, which is a useful
place to pass data between actions on the same request:

```csharp
ctx.Abstract.SetPerRequestItem("user", user);
if(ctx.Abstract.TryGetPerRequestItem("user", out var stored)) { /* ... */ }
ctx.Abstract.RemovePerRequestItem("user");
```

## Configuring options

`RoutableOptions` is where routing collections and library-wide settings are
registered. The fluent methods return the options instance so calls can be
chained. A platform integration exposes the options instance through its own
entry point; the methods below are the part of that surface defined by Routable.

```csharp
options
    .AddRouting(new MyRouting(options))
    .OnError(errorRouting)
    .UseLogger(myLogger);
```

### Registering routing

`AddRouting(routing)` registers a routing collection on the main pipeline. The
pipeline overloads place routing on a specific stage:

```csharp
options.AddRouting(mainRouting);
options.AppendRoutingToEventPipeline(RoutableEventPipelines.RouteEventInitialize, setupRouting);
options.PrependRoutingToEventPipeline(RoutableEventPipelines.RouteEventMain, highPriorityRouting);
```

### Event pipelines

Requests are processed in stages, defined by `RoutableEventPipelines`:

| Stage | Purpose |
| --- | --- |
| `RouteEventInitialize` | Runs first. A handled request here short-circuits the main stage. |
| `RouteEventMain` | The primary stage where most routing lives. `AddRouting` targets this stage. |
| `RouteEventError` | Runs when an exception is thrown during processing. |
| `RouteEventFinalize` | Runs after a request has been handled, for cleanup or final adjustments. |
| `RouteEventFinalizeUnhandledRequests` | Runs when no route handled the request, for example to produce a 404. |

The initialize and main stages stop at the first route that reports it handled
the request. A typical use of `RouteEventFinalizeUnhandledRequests` is a
catch-all that produces a not-found response:

```csharp
options.AppendRoutingToEventPipeline(
    RoutableEventPipelines.RouteEventFinalizeUnhandledRequests,
    new Routing<TContext, TRequest, TResponse>(options) {
        route => route.Do((ctx, req, resp) => {
            resp.Abstract.StatusCode = 404;
            resp.Write("Not found");
        })
    });
```

### Error handling

`OnError` registers routing for the error stage. The caught exception is
available on `ctx.Error`. Any output queued before the exception is discarded
before the error routing runs, so an error handler starts with a clean response:

```csharp
options.OnError(new Routing<TContext, TRequest, TResponse>(options) {
    route => route.Do((ctx, req, resp) => {
        resp.Abstract.StatusCode = 500;
        resp.Write($"{ctx.Error?.GetType().FullName}: {ctx.Error?.Message}");
    })
});
```

### Response type handlers

A response type handler converts a value of a given type into response output.
Register one to make `resp.Write(value)` understand your own types:

```csharp
options.ResponseTypeHandlers.Add(typeof(MyModel), (context, value) => {
    var model = (MyModel)value;
    var bytes = context.Options.StringEncoding.GetBytes(model.ToString());
    context.Response.Abstract.ContentLength = bytes.Length;
    context.Response.Write(async (ctx, stream) => await stream.WriteAsync(bytes, 0, bytes.Length));
});
```

When a value's type has no exact handler, Routable selects a registered handler
for a related type. The `object` handler registered by default acts as a final
fallback for reference types.

### MIME types

The options carry a MIME type table keyed by file extension, seeded with a large
set of common extensions. Adjust it as needed:

```csharp
options.AddMimeType("webp", "image/webp"); // a leading period is optional
if(options.TryGetMimeType(".png", out var mime)) { /* image/png */ }
options.RemoveMimeType(".exe");
options.ClearMimeTypes();
```

### String encoding

`StringEncoding` controls how string responses are encoded. It defaults to
UTF-8:

```csharp
options.StringEncoding = Encoding.Unicode;
```

### Feature options

Feature options let a component publish a configuration object that other
components can retrieve later by type. This is how optional subsystems pass
settings without coupling to the core:

```csharp
options.SetFeatureOptions(new MyFeatureSettings { Enabled = true });

if(options.TryGetFeatureOptions<MyFeatureSettings>(out var settings) && settings.Enabled) {
    // ...
}
```

### Logging

`UseLogger` replaces the default logger, which writes to standard error. A logger
implements `IRoutableLogger`:

```csharp
public sealed class MyLogger : IRoutableLogger
{
    public void Write(LogClass logClass, string message, Exception exception = null,
        IReadOnlyDictionary<string, string> data = null)
    {
        // forward to your logging framework
    }
}

options.UseLogger(new MyLogger());
```

`LogClass` distinguishes `Debug`, `Informational`, `Warning`, `Error`, and
`Security` messages.

## Related libraries

Routable is the platform agnostic base. Companion libraries build on it and are
documented separately:

- A platform integration supplies the concrete context, request, and response
  types and feeds requests into Routable.
- View and JSON companion libraries add response handling for those formats on
  top of the response type handler mechanism described above.
