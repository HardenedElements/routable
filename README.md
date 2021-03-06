[![Build Status](https://travis-ci.org/HardenedElements/routable.svg?branch=master)](https://travis-ci.org/HardenedElements/routable) [![NuGet](https://img.shields.io/nuget/dt/Routable.svg)](https://preview.nuget.org/packages/Routable)

Routable
=======
Routable is a simple, easy to use, request routing library. It is platform agnostic and designed to easily integrate with whatever platform you prefer. Support for Microsoft Kestrel is provided out the gate by ```Routable.Kestrel```, making routable a must-have for multi-platform web services and applications.

Routable is brand new; as such, it is missing a lot of **must-have** functionality. We hope to add this functionality as time permits, and if you see something you think we missed we would love it if you would file an issue (be detailed please; pull requests are welcome, but they may be modified significantly).

## Three simple rules
Routable is governed by three simple rules. First, the base library is simple and needs to stay that way. It doesn't bother with things like views, complex content type negotiation or canned authentication schemes. Nope, routable is here to route your requests and give you the glorious opportunity to handle those requests. Second, the base library is platform agnostic and it doesn't quibble over the gory details of delivering complete and total commonality to everyone - no, instead, we route your requests and return you to your regularly scheduled platform. And finally, routable is extensible. We admit it, we use a lot of generics - but only because we love to see the way people work when tools get out of the way and let developers get in touch with their platform.

## Libraries
Routable is a collection of libraries. First, you got your platform agnostic base library - that's ```Routable```. Then you have your simple *view* support - that's ```Routable.Views.Simple```. JSON support? Yeah, we integrate JSON.NET in ```Routable.Json```. And finally, you're going to need at least one platform integration to get started - we chose Kestrel because it's what we use the most; you can find that under ```Routable.Kestrel```. There are other platforms in the wind, but we haven't published those quite yet, they need some polishing up.

Library | NuGet Download
------- | --------------
Routable | [![NuGet](https://img.shields.io/nuget/v/Routable.svg)](https://preview.nuget.org/packages/Routable)
Routable.Kestrel | [![NuGet](https://img.shields.io/nuget/v/Routable.Kestrel.svg)](https://preview.nuget.org/packages/Routable.Kestrel)
Routable.Json | [![NuGet](https://img.shields.io/nuget/v/Routable.Json.svg)](https://preview.nuget.org/packages/Routable.Json)
Routable.Views.Simple | [![NuGet](https://img.shields.io/nuget/v/Routable.Views.Simple.svg)](https://preview.nuget.org/packages/Routable.Views.Simple)

## Examples (targeting Kestrel)
Using a bit of creative license, we'll be using ```Routable.Kestrel``` as a companion to these examples, very little changes with other platforms (eg. change *kestrel* to *my favorite platform*).
### Registration
```csharp
public sealed class Startup
{
	static void Main(string[] args) => new WebHostBuilder()
		.UseKestrel(options => options.Listen(IPAddress.Any, 8080))
		.Configure(builder => builder.UseRoutable(options => options
			.WithJsonSupport()
			.UseFileSystemViews(_ => _.AddSearchPath("views").OnUnresolvedModelValue((expr, paths, model) => $"[ERR! ({expr})]"))
			.AddRouting(new MyRouting(options))
			.AddRouting(new KestrelRouting(options) {
				_ => _.Get("/meeseeks").Do((ctx, req, resp) => resp.Write("Hi, I'm Mr. Meeseeks!")),
				_ => _.Path("/grimey").Do((ctx, req, resp) => resp.Write("I don't check methods, because I'm Homer Simpson!"))
			})
			.OnError(new KestrelRouting(options) {
				_ => _.Do((context, request, response) => {
					response.Status = 500;
					response.Write($"{context.Error?.GetType()?.FullName} ({context.Error?.Message}):\n\t{context.Error.StackTrace.Replace("\n", "\n\t")}\n");
				})
			})
		))
		.Build()
		.Run();
}
```
### Routing in a Class
```csharp
public sealed class MyRouting : KestrelRouting
{
	public MyRouting(RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> options) : base(options)
	{
		// write a view using Routable.Views.Simple.
		Add(_ => _.Get("/").DoAsync(async (ctx, req, resp) => await resp.WriteViewAsync("index", new {
			SomeModelField = "Widget widget"
		})));

		Add(_ => _.Get("/test").Do((ctx, req, resp) => resp.Write("Hello World!")));
		Add(_ => _.Post("/test").Try(OnTestPost));

		Add(_ => _.Get("/json").Do((ctx, req, resp) => resp.Write(JObject.FromObject(new {
			Field1 = 1,
			Field2 = "string?"
		}))));
	}

	private bool OnTestPost(KestrelRoutableContext ctx, KestrelRoutableRequest req, KestrelRoutableResponse resp) {
		if(req.Form.TryGetValue("my-parameter", out var value) == true) {
			resp.Write($"Value: {value.FirstOrDefault() ?? "<null>"}");
			return true;
		} else {
			return false;
		}
	}
}
```

