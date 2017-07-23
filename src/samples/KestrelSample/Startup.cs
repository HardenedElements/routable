using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Routable;
using Routable.Kestrel;
using Routable.Views.Simple;

namespace RoutableTest
{
	public sealed class Startup
	{
		public Startup(IHostingEnvironment environment) { }
		public void Configure(IApplicationBuilder builder) => builder.UseRoutable(options => {
			options
			.UseSimpleDefaults()
			.WithJsonSupport()
			.UseSimpleViews(_ => _.AddSearchPath("views").OnUnresolvedModelValue((type, expr, paths, model) => $"[ERR! ({expr})]"))
			.AddRouting(new MyRouting(options))
			.AddRouting(new KestrelRouting(options) {
				_ => _.Get("/meeseeks").Do(async (ctx, req, resp) => await resp.WriteAsync("Hi, I'm Mr. Meeseeks!")),
				_ => _.Path("/grimey").Do(async (ctx, req, resp) => await resp.WriteAsync("I don't check methods, because I'm Homer Simpson!"))
			})
			.OnError(async (context, error) => {
				context.Response.Status = 500;
				await context.Response.WriteAsync($"{error?.GetType()?.FullName} ({error?.Message}):\n\t{error.StackTrace.Replace("\n", "\n\t")}\n");
			});
		});
		static void Main(string[] args) => new WebHostBuilder()
			.UseKestrel()
			.UseStartup<Startup>()
			.Build()
			.Run();
	}
}