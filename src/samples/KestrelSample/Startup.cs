using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Routable;
using Routable.Kestrel;
using Routable.Views.Simple;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace RoutableTest
{
	public sealed class Startup
	{
		public Startup(IHostingEnvironment environment) { }

		// demo: tell kestrel to try routing requests through routable.
		public void Configure(IApplicationBuilder applicationBuilder) => applicationBuilder.UseRoutable(options => {
			options
			// demo: add JSON.NET support.
			.WithJsonSupport()

			// demo: add support for views.
			.UseFileSystemViews(_ => _.AddSearchPath("views").OnUnresolvedModelValue((expr, paths, model) => $"[ERR! ({expr})]"))

			// demo: execute this route on all requests, logging the request to stderr.
			.AddRouting(new KestrelRouting(options) {
				_ => _.Try(async (ctx, req, resp) => {
					await Console.Error.WriteLineAsync($"Request from [{ctx.RemoteEndPoint}] to {req.Uri}");
					return false;
				})
			})

			// demo: add some routing from a separate class file.
			.AddRouting(new MyRouting(options))

			// demo: add some routes inline using lambda expressions.
			.AddRouting(new KestrelRouting(options) {
				// demo: a couple cute static inline routes.
				_ => _.Get("/meeseeks").Do(async (ctx, req, resp) => await resp.WriteAsync("Hi, I'm Mr. Meeseeks!")),
				_ => _.Path("/grimey").Do(async (ctx, req, resp) => await resp.WriteAsync("I don't check methods, because I'm Homer Simpson!")),
				// demo: show off nested routing... for the curious, no - there is no limit.
				_ => _.Path(new Regex("/nest(?<myParameter>.*)")).Nest(async (_ctx, _req, _resp) => {
					var routing = new KestrelRouting(options) {
						// demo: toss in a cheeky little static route.
						builder => builder.Get("/nest/always").Do(async (ctx, req, resp) => await resp.WriteAsync("This route is always here for you"))
					};

					if(_req.Parameters.ContainsKey("myParameter")) {
						// let's inception up this request and dynamically compose a route to handle our url parameters.
						routing.Add(builder => builder.Get($"/nest{_req.Parameters["myParameter"]}").Do(async (ctx, req, resp) => await resp.WriteAsync("That movie never sat well with me.")));
						routing.Add(builder => builder.Post($"/nest{_req.Parameters["myParameter"]}").Do(async (ctx, req, resp) => await resp.WriteAsync("Did they ever explain where they got this technology?")));
						routing.Add(builder => builder.Trace($"/nest{_req.Parameters["myParameter"]}").Do(async (ctx, req, resp) => await resp.WriteAsync("I guess you can't place everyone all the time.")));
					}

					return routing;
				})
			})

			// demo: handle any exceptions.
			.OnError(async (context, error) => {
				context.Response.Status = 500;
				await context.Response.WriteAsync($"{error?.GetType()?.FullName} ({error?.Message}):\n\t{error.StackTrace.Replace("\n", "\n\t")}\n");
			});
		});
		static void Main(string[] args) => new WebHostBuilder()
			// start kestrel.
			.UseKestrel()
			.UseStartup<Startup>()
			.Build()
			.Run();
	}
}
