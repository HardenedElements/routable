# Routable.Views.Simple Usage

`Routable.Views.Simple` is a lightweight HTML view engine for the Routable
library. It adds model-bound template rendering on top of `RoutableOptions`,
keeping the same extension-method-driven design as the rest of the library.
Views are plain text files (typically HTML) that contain expressions prefixed
with `@`. The engine resolves those expressions against a model object you
supply at render time.

This document covers the view engine API only. For foundational routing
concepts — routing collections, route actions, event pipelines, and response
writing — see [Routable Usage.md](Routable%20Usage.md).

## Registering a view source

View sources are registered on `RoutableOptions` through extension methods.
Each call adds one source to an ordered list. When a view is requested, sources
are tried in the order they were registered; the first source that resolves the
name wins. Multiple sources of different types can coexist in the same
application.

### File system views

`UseFileSystemViews` registers a source that looks for views on disk.

```csharp
options.UseFileSystemViews(config => {
	config.AddSearchPath("views");
	config.OnUnresolvedModelValue((expr, paths, model) => $"[ERR! ({expr})]");
});
```

By default the source searches a `views` subdirectory of the working directory
and accepts `.html` files. Call `AddSearchPath` to add additional directories
(all are searched in order). Call `ClearSearchPaths` before adding paths when
you want to replace the default rather than supplement it.

| Member | Description |
| --- | --- |
| `AddSearchPath(string path)` | Add a directory to the search list. Paths are resolved to absolute form automatically. |
| `ClearSearchPaths()` | Remove all search directories. |
| `AddViewExtension(string ext)` | Add a file extension tried when locating a view (e.g. `".htm"`). |
| `DefaultMimeType` | Content type used when the file extension is not in the MIME table. Defaults to `"text/html"`. |
| `OnUnresolvedModelValue(Func<string, IEnumerable<string>, object, object> action)` | Callback invoked when model resolution fails for an expression. Return a non-null value to supply a fallback, or return `null` to leave the expression unresolved. |

### Embedded views

`UseEmbeddedViews` registers a source that reads views from assembly manifest
resources. Embed view files with `<EmbeddedResource>` in your project file and
then register the assembly:

```csharp
options.UseEmbeddedViews(config =>
	config.AddAssembly(typeof(Program).Assembly, "MyApp.embedded_views"));
```

The prefix is prepended to the view name (dots-separated) to form the resource
name. For example, the view name `"test/embed"` becomes
`MyApp.embedded_views.test.embed.html`. If no prefix is supplied, the resource
name is built from the view name alone.

| Member | Description |
| --- | --- |
| `AddAssembly(Assembly assembly, params string[] prefixes)` | Register an assembly with one or more resource name prefixes. |
| `ClearAssemblies()` | Remove all registered assemblies. |
| `AddViewExtension(string ext)` | Add a resource name extension tried when locating a view. |
| `ClearViewExtensions()` | Remove all registered extensions. |
| `DefaultMimeType` | Content type used when the extension is not in the MIME table. Defaults to `"text/html"`. |
| `OnUnresolvedModelValue(...)` | Same callback signature as the file system variant. |

### Custom resolver

`UseViewsWithCustomResolver` lets you supply view content from any source
through callbacks. Synchronous and asynchronous variants are available for all
resolver types.

```csharp
options.UseViewsWithCustomResolver(config => {
	// resolve a view name to a file path
	config.File(name => Path.Combine("alt-views", name + ".html"));
});
```

Alternatively, supply the content directly as a string, byte array, or stream:

```csharp
options.UseViewsWithCustomResolver(config => {
	config.StringAsync(async name => {
		// fetch template content from a database or remote store
		return await templateStore.GetAsync(name);
	});
});
```

For full control, use `Resolve` / `ResolveAsync` and populate the
`ResolveViewArgs` object directly:

```csharp
options.UseViewsWithCustomResolver(config => {
	config.Resolve(args => {
		args.MimeType = "text/html";
		args.GetStream = () => Task.FromResult<Stream>(GetViewStream(args.Name));
		args.Success = true;
	});
});
```

| Member | Description |
| --- | --- |
| `File(Func<string, string> resolver)` | Map a view name to a file path. |
| `FileAsync(Func<string, Task<string>> resolver)` | Async variant of `File`. |
| `String(Func<string, string> resolver)` | Map a view name to template content as a string. |
| `StringAsync(Func<string, Task<string>> resolver)` | Async variant of `String`. |
| `Bytes(Func<string, byte[]> resolver)` | Map a view name to raw bytes. |
| `BytesAsync(Func<string, Task<byte[]>> resolver)` | Async variant of `Bytes`. |
| `Stream(Func<string, Stream> resolver)` | Map a view name to a stream. |
| `StreamAsync(Func<string, Task<Stream>> resolver)` | Async variant of `Stream`. |
| `Resolve(Action<ResolveViewArgs> resolver)` | Full control; set `MimeType`, `GetStream`, and `Success` on the args. |
| `ResolveAsync(Func<ResolveViewArgs, Task> resolver)` | Async variant of `Resolve`. |
| `DefaultMimeType` | Fallback content type. Defaults to `"text/html"`. |

## Rendering a view

Inside a route action, call `WriteViewAsync` on the response object. The view
is resolved from the registered sources, parsed, and rendered with the supplied
model. The content type is set automatically from the resolved MIME type.

```csharp
public sealed class MyRouting : KestrelRouting
{
	public MyRouting(RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> options)
		: base(options)
	{
		Add(_ => _.Get("/").DoAsync(OnIndex));
		Add(_ => _.Get("/embedded").DoAsync(OnEmbedded));
	}

	private async Task OnIndex(KestrelRoutableContext ctx, KestrelRoutableRequest req, KestrelRoutableResponse resp)
	{
		await resp.WriteViewAsync("index", new {
			Title = "Home",
			People = GetPeople()
		});
	}

	private async Task OnEmbedded(KestrelRoutableContext ctx, KestrelRoutableRequest req, KestrelRoutableResponse resp)
	{
		await resp.WriteViewAsync("test/embed", new { Title = "Embedded" });
	}
}
```

`WriteViewAsync(string name, object model = null)` is an extension method on
`RoutableResponse<TContext, TRequest, TResponse>`. The model is optional; pass
`null` (or omit it) when a view has no model expressions.

View names accept letters, digits, and the characters `. - _ space , / \`.
The name is passed as-is to the source resolver; for file system views it
becomes a relative path, and for embedded views the separators are converted
to dots.

## Template syntax

Template files are plain text. Everything that is not an `@` expression is
emitted verbatim. Literal `@` characters are written by including a run of
two or more consecutive `@` characters (e.g. `@@` produces `@` in the
output).

### Model access — `@Model` and `@Model.Prop.Nested`

`@Model` evaluates to the current model as a string. `@Model.Prop` accesses
a named property or public field on the model object. Paths can be chained to
traverse nested objects.

```html
<p>@Model.Title</p>
<p>@Model.SomeModelField.Nested</p>
```

### Root access — `@Root` and `@Root.Prop`

`@Root` refers to the outermost model in the current rendering context — the
model passed to `WriteViewAsync`. Inside a `@ForEach` loop the current model
becomes the loop item; `@Root` provides access to the outer model without
needing to step out of the loop.

```html
@ForEach(People)
<li><strong>@Model.Name</strong> — <i>@Root.Title</i></li>
@EndForEach
```

### Conditional blocks — `@IfSet` / `@EndIfSet`

`@IfSet(expr)` renders the enclosed content only when `expr` resolves to a
non-null, non-empty value. The expression follows the same dotted-path rules
as `@Model`.

```html
@IfSet(Title)
<h1>@Model.Title</h1>
@EndIfSet

@IfSet(SomeModelField)
<p>@Model.SomeModelField.Nested</p>
@EndIfSet
```

### Iteration — `@ForEach` / `@EndForEach`

`@ForEach(expr)` iterates over an `IEnumerable` value. Inside the block,
`@Model` is the current item and `@Root` is the model that was in scope
before the loop began.

```html
@IfSet(People)
<ul>
	@ForEach(People)
	<li><strong>@Model.Name</strong>, <i>@Root.Title</i></li>
	@EndForEach
</ul>
@EndIfSet
```

### Partial views — `@Include`

`@Include(viewName)` resolves and renders another view inline at that point
in the output. The current model is passed through to the included view.

```html
<head>
	@Include(test/head)
</head>
```

### Template inheritance — `@Parent` / `@Child`

A view declares a parent by placing `@Parent(viewName)` anywhere in its
content. When the view is rendered, the parent template is rendered instead,
and wherever the parent contains `@Child`, the child's content is inserted.

Child view (`views/child.html`):

```html
<h2>This is a child node</h2>
<p>
	This is just a test, <strong>la la la la</strong>
	@Parent(parent)
</p>
```

Parent view (`views/parent.html`):

```html
<!DOCTYPE html>
<html>
<head>
	@Include(test/head)
</head>
<body>
	<h1>This is the parent node</h1>
	<div id="child-content">
		@Child
	</div>
</body>
</html>
```

Rendering `"child"` produces the parent layout with the child's content in
place of `@Child`. The `@Parent` declaration can appear anywhere in the child
template; only `@Child` in the parent controls where the child content is
inserted.

`MaximumParentChildRecursionDepth` (default `32`) limits how many times a
view may declare a parent, preventing runaway recursion. Exceeding the limit
raises `SimpleViewParserException`.

## Model resolution and unresolved values

When the engine encounters a dotted expression it traverses the model object
by reflecting on properties and public fields at each segment. If any segment
fails to resolve — because the property does not exist, the value is null, or
the type does not have the requested member — the expression is considered
unresolved.

Two options control what happens with unresolved expressions:

- `OnUnresolvedModelValue(Func<string, IEnumerable<string>, object, object> action)` —
  registers a callback on the view source options. The callback receives the
  full expression string, the path segments, and the model object, and should
  return a fallback value or `null`. A non-null return is used as the
  rendered value.

- `IsUnresolvedModelExpressionExceptional` — when set to `true` on any
  `SimpleViewOptions` instance, an unresolved expression throws
  `SimpleViewUnresolvedModelExpression` instead of rendering nothing. Defaults
  to `false`.

If neither a callback nor the exceptional flag is in effect, unresolved
expressions produce no output.

## Custom expressions

The engine's expression set can be extended by subclassing
`CustomExpression<TContext, TRequest, TResponse>` and providing a
[Superpower](https://github.com/datalust/superpower) `TextParser` that
recognises the new syntax.

### Implementing a custom expression

Override `TryRender(StreamWriter writer, object model)` to write the
expression's output. Return `true` when rendering succeeds, `false` to
indicate that the expression could not be rendered (the engine will skip it).

```csharp
using Routable;
using Routable.Kestrel;
using Routable.Views.Simple;
using Superpower;
using Superpower.Parsers;

public sealed class ExampleExpression
	: CustomExpression<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse>
{
	// The parser must be typed as the base CustomExpression<...>.
	public static TextParser<CustomExpression<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse>> Parser =>
		from condOpen in Character.EqualTo('@').IgnoreThen(Span.EqualTo("Hex("))
		from body in Character.LetterOrDigit.Or(Character.EqualTo(' ')).Many().Select(chars => new string(chars))
		from condClose in Character.EqualTo(')')
		select (CustomExpression<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse>)new ExampleExpression(body);

	private string Body;

	public ExampleExpression(string body) => Body = body;

	public override async Task<bool> TryRender(StreamWriter writer, object model)
	{
		await writer.WriteAsync(
			$"[Hex: {string.Join(":", Body.Select(c => ((byte)c).ToString("X").PadLeft(2, '0')))}]");
		return true;
	}
}
```

The parser variable must be declared as
`TextParser<CustomExpression<TContext, TRequest, TResponse>>`, not as the
concrete subtype. The `select` clause achieves this through an explicit cast
(see above). The `Routable.Views.Simple` package takes a dependency on
Superpower; you do not need to add it separately. For details on how the
expression parsers are composed into the full template parser, see
[Routable.Views.Simple Internal Design.md](Routable.Views.Simple%20Internal%20Design.md).

### Registering a custom expression

Pass the parser to `AddExpressionParser` on the view source options:

```csharp
options.UseFileSystemViews(config => {
	config.AddExpressionParser(ExampleExpression.Parser);
	config.AddSearchPath("views");
});
```

Custom expressions registered this way are tried before the built-in
expression parser. Each registered parser is attempted in registration order.

### Using a custom expression in a template

Once registered, the expression appears in templates exactly as the parser
recognises it:

```html
<p>This will test an example custom expression. @Hex(HelloWorld123)</p>
```

## Configuration reference

The following options are defined on `SimpleViewOptions` and inherited by all
view source types.

| Option | Type | Default | Description |
| --- | --- | --- | --- |
| `IsUnresolvedModelExpressionExceptional` | `bool` | `false` | When `true`, unresolved model expressions throw `SimpleViewUnresolvedModelExpression` instead of producing no output. |
| `MaximumParentChildRecursionDepth` | `int` | `32` | Maximum parent-child nesting depth followed during rendering. Beyond this depth the engine logs a warning and renders the current children in place instead of recursing further. |
| `AddExpressionParser(parser)` | method | — | Register a Superpower parser for a custom expression type. |
| `ResolveUnresolvedModelKey` | event | — | Event raised when model resolution fails. Set `args.Value` and `args.Success = true` to supply a fallback. `OnUnresolvedModelValue` is a convenience wrapper for this event. |

The following options are specific to view source types.

| Option | Applies to | Description |
| --- | --- | --- |
| `DefaultMimeType` | File system, embedded, functional | Content type when the file extension is not found in the MIME table. |
| `AddSearchPath` / `ClearSearchPaths` | File system | Directories searched when locating a view file. |
| `AddViewExtension` | File system, embedded | Extensions appended to the view name during lookup. |
| `ClearViewExtensions` | Embedded | Remove all registered view extensions. |
| `AddAssembly` / `ClearAssemblies` | Embedded | Assemblies (with optional resource name prefixes) searched for embedded resources. |

## Exceptions reference

| Exception | Thrown when |
| --- | --- |
| `SimpleViewNotFoundException` | No registered source could resolve the requested view name. Derives from `InvalidOperationException`. |
| `SimpleViewParserException` | The view template could not be parsed, for example because the syntax is invalid or `MaximumParentChildRecursionDepth` was exceeded. Derives from `InvalidOperationException`. |
| `SimpleViewUnresolvedModelExpression` | A model expression could not be resolved and `IsUnresolvedModelExpressionExceptional` is `true`. Derives from `InvalidOperationException`. |
