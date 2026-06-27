# Routable.Views.Simple Internal Design

This document describes the internal design of the `Routable.Views.Simple` library:
how templates are located and parsed, how the Superpower-based grammar works, how
the AST is structured and rendered, and how caching and model resolution operate.
It is intended for contributors and for authors who want to extend the view engine
with custom expressions. It covers only `Routable.Views.Simple`; the base library
is described in [Routable Internal Design.md](Routable%20Internal%20Design.md).

## Overview

`Routable.Views.Simple` is a lightweight template engine for Routable. Templates
are text files (typically HTML) containing `@`-prefixed expressions that are
replaced with model data, control-flow constructs, and partial-view references
during rendering. The parser is built on Superpower
(https://github.com/datalust/superpower), a parser-combinator library for .NET
that operates directly on character spans.

The library integrates with Routable through feature options (see
[Routable Internal Design.md](Routable%20Internal%20Design.md) for the
`SetFeatureOptions`/`TryGetFeatureOptions` mechanism) and exposes a single
entry point to callers: `response.WriteViewAsync(name, model)`.

## End-to-end pipeline

```
response.WriteViewAsync(name, model)
  │
  ├─ Template.Find(options, name)
  │    │
  │    ├─ TryGetFeatureOptions<List<SimpleViewOptions>>
  │    │   (falls back to a default SimpleFileSystemViewOptions if not set)
  │    │
  │    └─ for each SimpleViewOptions in the list:
  │         │
  │         ├─ viewOptions.ResolveView(ResolveViewArgs)
  │         │   (file system / embedded / functional — see View sources)
  │         │
  │         └─ on success:
  │              ├─ open stream, read source to string
  │              ├─ TemplateParser.TryParse(name, lastModified, source)
  │              │    ├─ TemplateCache.Fetch — cache hit? return Template
  │              │    ├─ GetParser().Parse(source) → Node[]  (flat token array)
  │              │    ├─ RollupNodes(name, symbols, ref index) → Node tree
  │              │    ├─ new Template(options, viewOptions, nodes)
  │              │    └─ TemplateCache.Add
  │              └─ return Template (MimeType set from ResolveViewArgs)
  │
  └─ response.Write(async (context, stream) => {
       set ContentType = template.MimeType
       create StreamWriter over stream
       Template.TryRender(name, writer, model)
         ├─ new RenderContext; context.Push(model)
         └─ for each node in Nodes:
              node.TryRender(writer, context)
     })
```

The `Write` call queues a writer delegate; the platform integration flushes it
to the response stream during finalization, consistent with how all Routable
responses are handled.

## Type map

```
SimpleViewOptions<TContext, TRequest, TResponse>   abstract base; held in FeatureOptions
  ├─ SimpleFileSystemViewOptions                   search paths + file extensions
  ├─ SimpleEmbeddedViewOptions                     assembly manifest resources
  └─ SimpleFunctionalViewOptions                   sync/async resolver callbacks
       CustomExpressionParsers                     List<TextParser<CustomExpression>>

ResolveViewArgs                                    view-resolution result bag
  ├─ Name, MimeType, LastModified
  ├─ GetStream  Func<Task<Stream>>
  └─ Success    bool

TemplateParser<TContext, TRequest, TResponse>      builds and applies the grammar
  ├─ GetParsers()    IEnumerable<TextParser<Node>>
  ├─ GetParser()     TextParser<Node[]>   (the combined alternation)
  └─ RollupNodes()   Node[] → Node tree

Template<TContext, TRequest, TResponse>            parsed + cached template
  ├─ Nodes   IReadOnlyList<Node>
  ├─ MimeType
  ├─ Find()  (static; drives resolution + parse + cache)
  └─ TryRender(writer, context)

TemplateCache<TContext, TRequest, TResponse>       static; keyed by view name
  └─ Entry { LastModified?, Digest byte[], Template }

RenderContext<TContext, TRequest, TResponse>       per-render state
  ├─ ModelStack          Stack<object>
  ├─ Cache               Dictionary<object, IDictionary<string, object>>
  └─ ChildrenStack       Stack<List<Node>>   (for Parent/Child inheritance)

Node<TContext, TRequest, TResponse>               abstract AST node
  ├─ Children   List<Node>
  ├─ ViewNameParser  (static TextParser<string>)
  ├─ CharSetParser   (static helper)
  └─ TryRender(writer, context)  Task<bool>
```

## The Superpower grammar

### Parser construction

Each AST node class exposes a static `GetParser` method that returns a
`TextParser<Node<TContext, TRequest, TResponse>>`. These methods use Superpower
combinators directly. The principal combinators used are:

- `Character.EqualTo(ch)` — matches a single specific character.
- `Span.EqualTo(str)` — matches an exact string as a character span.
- `Character.LetterOrDigit` — matches any letter or digit.
- `Character.Except(ch)` — matches any character except `ch`.
- `.IgnoreThen(next)` — sequences two parsers, discarding the left result.
- `.Or(alternative)` — tries the left parser; if it fails without consuming
  input, tries the right. Does not backtrack after input has been consumed.
- `.Many()` — applies a parser zero or more times, collecting results into an
  array.
- `.AtLeastOnce()` — applies a parser one or more times.
- `.Select(fn)` / LINQ query syntax — maps a parsed value through a function.

The `.Text()` extension in `ParserExtensions.cs` converts a
`TextParser<char[]>` (produced by `.Many()` or `.AtLeastOnce()`) into a
`TextParser<string>` by calling `new string(chars)` over the result.

### Keyword-node parsers

All keyword-node parsers follow the same structure: consume `@`, match the
keyword (and any delimiters), then construct the node. The LINQ query syntax
is used throughout. Representative examples:

**IfSetNode** — `@IfSet(expression)`:
```csharp
from condOpen in Character.EqualTo('@').IgnoreThen(Span.EqualTo("IfSet("))
from body     in Character.LetterOrDigit.Or(Character.EqualTo('.')).Many().Text()
from condClose in Character.EqualTo(')')
select (Node<...>)new IfSetNode<...>(options, viewOptions, body);
```

**ModelNode** — `@Model` or `@Model.Path.To.Property`:
```csharp
from condOpen in Character.EqualTo('@').IgnoreThen(Span.EqualTo("Model"))
from prop     in Character.EqualTo('.').IgnoreThen(
                    Character.LetterOrDigit.Many().Text()).Many()
select (Node<...>)new ModelNode<...>(options, viewOptions, string.Join(".", prop));
```

**ViewNameParser** (defined on `Node`) accepts letters, digits, and the
characters `.`, `-`, `_`, ` `, `,`, `/`, `\`. It is used by `IncludeNode`
and `ParentNode` to capture the target view name.

### GetParsers() and GetParser()

`TemplateParser.GetParsers()` assembles the ordered list of node parsers:

1. The nine built-in keyword parsers, in the order:
   `IfSetNode`, `EndIfSetNode`, `ForEachNode`, `EndForEachNode`,
   `IncludeNode`, `ModelNode`, `RootNode`, `ParentNode`, `ChildNode`.
2. Each parser in `ViewOptions.CustomExpressionParsers`, mapped to a
   `CustomExpressionNode`. Because `TextParser<T>` is invariant, the map
   uses an explicit cast:
   ```csharp
   .Select(e => (Node<TContext, TRequest, TResponse>)
       new CustomExpressionNode<TContext, TRequest, TResponse>(
           options, viewOptions, e))
   ```
3. `ContentNode` last, as the unconditional fallback.

`TemplateParser.GetParser()` folds that list into a single alternation. Each
alternative is wrapped with `.Try()`:

```csharp
TextParser<Node<...>> current = null;
foreach (var parser in GetParsers()) {
    var alternative = parser.Try();
    current = current == null ? alternative : current.Or(alternative);
}
return current.Many();
```

Every keyword parser begins by consuming `@`. Superpower's `Or` does not retry
an alternative once it has consumed input, so without `.Try()` a `@` that
starts a keyword would be consumed and any mismatch would produce a parse error
rather than falling through to the next alternative. Wrapping each alternative
with `.Try()` restores the input position on failure, giving the alternation
full backtracking.

The combined parser is applied once with `.Many()` to produce the flat
`Node[]` array from the entire template source.

### ContentNode — the fallback

`ContentNode.GetParser()` is itself a two-alternative parser:

```csharp
var atSymbols = from at in Character.EqualTo('@').AtLeastOnce().Text()
                select (Node<...>)new ContentNode<...>(options, viewOptions, at);

var contentSymbols =
    from before in Character.Except('@')
                       .Or(Character.EqualTo('\n'))
                       .Or(Character.EqualTo('\r'))
                       .AtLeastOnce().Text()
    select (Node<...>)new ContentNode<...>(options, viewOptions, before);

return atSymbols.Or(contentSymbols);
```

The first branch matches one or more literal `@` characters; the second matches
one or more characters that are not `@`. Because `.AtLeastOnce()` is used in
both branches, `ContentNode` always consumes at least one character. This
guarantees that the outer `.Many()` in `GetParser()` terminates cleanly at end
of input — it cannot spin indefinitely on zero-length matches.

`ContentNode.TryRender` writes its captured string verbatim to the
`StreamWriter` and returns `true`.

### Custom expressions

`CustomExpression<TContext, TRequest, TResponse>` is the public abstract base
that consumers extend. A custom expression class implements:

```csharp
public abstract Task<bool> TryRender(StreamWriter writer, object model);
```

The consumer supplies a `TextParser<CustomExpression<...>>` and registers it
via `SimpleViewOptions.AddExpressionParser`. At parse time, each registered
parser is wrapped into a `CustomExpressionNode` via `.Select(...)` and inserted
into the alternation list before `ContentNode`. During rendering,
`CustomExpressionNode.TryRender` delegates to `CustomExpression.TryRender`,
passing the top-of-stack model from `RenderContext`.

## AST node catalog

| Node type | Template syntax | Render behavior |
| --- | --- | --- |
| `ContentNode` | Any literal text; runs of `@` not matched by a keyword | Writes `Content` verbatim. |
| `ModelNode` | `@Model`, `@Model.Prop`, `@Model.A.B` | Resolves dotted path against current model; writes `ToString()`. |
| `RootNode` | `@Root`, `@Root.Prop`, `@Root.A.B` | Same as `ModelNode` but resolves against the bottom of the model stack. |
| `IfSetNode` | `@IfSet(expr)` | Renders `Children` only when `expr` resolves to a non-null value. |
| `EndIfSetNode` | `@EndIfSet` | Sentinel; consumed by `RollupNodes`. No-op at render time. |
| `ForEachNode` | `@ForEach(expr)` | Resolves `expr` to `IEnumerable`; for each item, pushes it onto the model stack, renders `Children`, then pops. |
| `EndForEachNode` | `@EndForEach` | Sentinel; consumed by `RollupNodes`. No-op at render time. |
| `IncludeNode` | `@Include(view/name)` | Calls `Template.Find` for the named view and renders it into the same `RenderContext` (current model stack is inherited). |
| `ParentNode` | `@Parent(layout/name)` | Pushes `Children` onto `RenderContext.ChildrenStack`, then finds and renders the named parent template. |
| `ChildNode` | `@Child` | Pops from `RenderContext.ChildrenStack` and renders those nodes in place. |
| `CustomExpressionNode` | (defined by consumer's parser) | Delegates to `CustomExpression.TryRender(writer, model)`. |

### TryRender contract

`Node.TryRender(StreamWriter writer, RenderContext context)` is `async` and
returns `Task<bool>`. Returning `false` signals that rendering should not
continue; the parent node or `Template.TryRender` propagates the `false`
immediately without processing further nodes. Returning `true` means the node
rendered successfully and the next node should be processed.

`Node.Children` is a `List<Node>` populated by `RollupNodes` for block nodes
(`IfSetNode`, `ForEachNode`) or by `RollupNodes` and `ParentNode` construction
for `ParentNode`. Leaf nodes leave it empty.

## RollupNodes — flat token array to node tree

`RollupNodes(string name, Node[] symbols, ref int index)` converts the flat
array produced by the parser into a proper tree. It walks the array and handles
four cases:

1. **`IfSetNode` or `ForEachNode`** — the method recurses immediately (advancing
   `index` through the recursive call) and appends the returned nodes to the
   block node's `Children`. The sentinel node (`EndIfSetNode` /
   `EndForEachNode`) terminates the recursive call by returning early.
2. **`EndIfSetNode` or `EndForEachNode`** — terminates the current recursive
   call and returns the nodes collected so far. The sentinel itself is not
   added to the output.
3. **`ParentNode`** — recorded separately. Only one `ParentNode` per template
   is permitted; a second one throws `SimpleViewParserException`.
4. **All other nodes** — appended to the output list.

After the array is exhausted (or the end of an `@IfSet`/`@ForEach` block is
reached), if a `ParentNode` was seen, all collected nodes are moved into its
`Children` and the method returns a single-element list containing the
`ParentNode`. Otherwise the collected nodes are returned directly.

The top-level call passes `index = 0` by reference. The recursive calls share
and advance `index`, so each node in `symbols` is visited exactly once.

## Rendering and model resolution

### RenderContext model stack

`RenderContext` holds a `Stack<object>` (`ModelStack`). The initial model is
pushed by `Template.TryRender(name, writer, model)` before walking the node
list. `ForEachNode` pushes each iteration item and pops it in a `finally`
block to guarantee cleanup.

- `@Model` resolves against `ModelStack.Peek()` (top of stack).
- `@Root` resolves against `ModelStack.LastOrDefault()` (bottom of stack),
  giving access to the outermost model from anywhere inside a loop.

### Value resolution

`RenderContext.TryGetValue(expression, onlyUseModel, out value)` works as
follows:

1. **Cache check** — the per-model cache (`Dictionary<object,
   IDictionary<string, object>>`) is checked first. If the expression was
   previously resolved for the current model object, the cached value is
   returned immediately. The cache is locked for thread safety.

2. **Reflection walk** — `TryGetModelValue` splits the expression on `.` and
   walks each path component in turn:
   - If `TypeInfo.DeclaredProperties` contains a property with that name, its
     value is retrieved via `GetValue`.
   - Otherwise, if `TypeInfo.DeclaredFields` contains a public instance field
     with that name, its value is retrieved via `GetValue`.
   - The walk proceeds to the next component against the retrieved value.
   On success the result is stored in the per-model cache.

3. **Unresolved key fallback** — when `onlyUseModel` is `false` and reflection
   failed, `ViewOptions.TryResolveUnresolvedModelKey` fires the
   `ResolveUnresolvedModelKey` event. Handlers receive an
   `UnresolvedModelKeyEventArgs` containing the full expression, the path
   components, and the model object. Setting `args.Success = true` and
   `args.Value` returns a value to the caller.

When `onlyUseModel` is `true` (used by `IfSetNode` and `ForEachNode`), the
unresolved-key fallback is skipped. If `IsUnresolvedModelExpressionExceptional`
is `true` on the view options and resolution fails, `ModelNode` and `RootNode`
throw `SimpleViewUnresolvedModelExpression` instead of silently writing nothing.

## Template inheritance — ParentNode and ChildNode

Template inheritance allows a child template to delegate its layout to a named
parent template, injecting its own content at a designated slot.

When a template contains `@Parent(layout/name)`, `RollupNodes` moves all
sibling nodes into the `ParentNode.Children` list. During rendering,
`ParentNode.TryRender` checks `RenderContext.ChildrenStack.Count` against
`ViewOptions.MaximumParentChildRecursionDepth` (default 32). If the depth
limit is not exceeded, it pushes `Children` onto `ChildrenStack` and renders
the parent template. When the parent template renders `@Child`,
`ChildNode.TryRender` pops from `ChildrenStack` and renders those nodes in
place.

If `ChildrenStack.Count` exceeds `MaximumParentChildRecursionDepth`, the
`ParentNode` does not recurse into the parent template. Instead it logs a
warning through `Options.Logger` and renders its own `Children` directly.

Each `ChildrenStack` push corresponds to exactly one `@Parent`; a single
`@Child` in the parent template pops it. Nesting — a parent template that is
itself a child of another template — works because each level pushes its
children before rendering the next level up.

## Caching

`TemplateCache<TContext, TRequest, TResponse>` is a static class with a single
dictionary of `Entry` records, keyed by view name and guarded by a lock.

An entry stores:
- `LastModified` — `DateTime?` supplied by the resolver, or `null`.
- `Digest` — `byte[]` SHA-256 hash of the source, computed only when
  `LastModified` is `null`.
- `Template` — the parsed `Template` instance.

Validation logic in `IsNew`:

- When `LastModified` is not `null` (file-system views supply
  `File.GetLastWriteTimeUtc`): the cache entry is considered current if and
  only if `entry.LastModified` equals the incoming `lastModified`. Any
  mismatch causes the entry to be replaced.
- When `LastModified` is `null` (embedded views supply `DateTime.MinValue`,
  which the cache treats as absent): the cache entry is validated by comparing
  the stored SHA-256 digest against a freshly computed digest of the source
  string. A mismatch replaces the entry.

`TemplateCache.Fetch` computes the digest before acquiring the lock (only when
`lastModified` is `null`) so that the lock is held only for the dictionary
lookup. `TemplateCache.Add` does the same.

## View sources

Each `SimpleViewOptions` subclass implements `ResolveView(ResolveViewArgs)`.
The method is async and populates `ResolveViewArgs` on success, setting
`GetStream` (a `Func<Task<Stream>>`), `MimeType`, `LastModified`, and
`Success`. It returns without modification when `resolveViewArgs.Success` is
already `true` (a prior resolver in the list already found the view).

### SimpleFileSystemViewOptions

Resolution iterates the cross product of registered view extensions and
registered search paths. For each candidate, the path is constructed with
`Path.Combine(searchPath, $"{viewName}{ext}")` and then resolved to a full
path with `Path.GetFullPath`. A path-restriction check verifies that the full
path starts with the search directory (case-insensitive), preventing traversal
outside the configured directories. The first candidate path that exists on
disk is selected.

`MimeType` is looked up via `RoutableOptions.TryGetMimeType` by file extension;
if not found, `DefaultMimeType` (`"text/html"`) is used. `LastModified` is set
to `File.GetLastWriteTimeUtc(path)`. Default search path is
`{CurrentDirectory}/views`; default extension is `.html`.

### SimpleEmbeddedViewOptions

Resolution queries assembly manifest resources. The view name has its `/` and
`\` characters replaced with `.` to form a resource name segment. For each
registered assembly and prefix, the candidate resource name is
`{prefix}.{viewName}{ext}`. `Assembly.GetManifestResourceInfo` confirms
existence before opening the stream.

`LastModified` is always `DateTime.MinValue` (treated by the cache as absent,
causing digest-based validation). `MimeType` resolution follows the same
extension lookup as the file-system resolver. The default extension is `.html`;
no search paths are configured by default.

### SimpleFunctionalViewOptions

Resolution runs async resolvers first (in registration order), then sync
resolvers. Each sync resolver is registered as an `EventHandler<ResolveViewArgs>`
delegate; the event fires after the async resolvers have run.

Convenience registration methods cover common cases:

| Method | Resolver receives | Sets stream from |
| --- | --- | --- |
| `File(fn)` / `FileAsync(fn)` | View name → file path | `File.OpenRead(path)` |
| `String(fn)` / `StringAsync(fn)` | View name → string | `MemoryStream` of encoded bytes |
| `Bytes(fn)` / `BytesAsync(fn)` | View name → `byte[]` | `MemoryStream` |
| `Stream(fn)` / `StreamAsync(fn)` | View name → `Stream` | The stream directly |
| `Resolve(fn)` / `ResolveAsync(fn)` | Full `ResolveViewArgs` | Caller populates all fields |

`LastModified` is not set by default in functional resolvers; callers using
the `Resolve`/`ResolveAsync` variants can set it explicitly.

## Configuration entry points

`SimpleViewExtensions` provides three extension methods on
`RoutableOptions<TContext, TRequest, TResponse>`:

- `UseFileSystemViews(builder)` — creates a `SimpleFileSystemViewOptions`,
  calls the builder, and appends it to the feature-options list.
- `UseEmbeddedViews(builder)` — same for `SimpleEmbeddedViewOptions`.
- `UseViewsWithCustomResolver(builder)` — same for
  `SimpleFunctionalViewOptions`.

Each method reads the existing `List<SimpleViewOptions>` from feature options
(creating it if absent) and appends the new instance. Multiple calls register
multiple sources; `Template.Find` tries them in registration order and returns
the first successful match.

Custom expression parsers are registered on any `SimpleViewOptions` instance
via `AddExpressionParser(TextParser<CustomExpression<...>>)`. Parsers are
inserted into the grammar alternation after the built-in keyword parsers and
before `ContentNode`, in registration order.
