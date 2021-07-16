using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Routable;
using Routable.Kestrel;
using Routable.Views.Simple;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace KestrelSample
{
	public sealed class Startup
	{
		public static async Task Main(string[] args)
		{
			using var server = new WebHostBuilder()
				.UseKestrel(kestrelOptions => kestrelOptions
					.Listen(IPAddress.Any, 8080)
				)
				.Configure(appBuilder => appBuilder
					.UseRoutable(routable => routable
						.WithJsonSupport()

						// demo: add support for views.
						.UseFileSystemViews(_ => {
							// add custom expression to our views.
							_.AddExpressionParser(ExampleExpression.Parser);

							// configure the search path.
							_.AddSearchPath("views");

							// if a view requests a model that does not exist, use this string instead.
							_.OnUnresolvedModelValue((expr, paths, model) => $"[ERR! ({expr})]");
						})

						// demo: add support for embedded views.
						.UseEmbeddedViews(_ => _.AddAssembly(typeof(Startup).GetTypeInfo().Assembly, "KestrelSample.embedded_views"))

						// demo: execute this route on all requests, logging the request to stderr.
						.AddRouting(RoutableEventPipelines.RouteEventInitialize, new KestrelRouting(routable) {
							// don't use Do() or return true here unless you've handled the request and are all done.
							_ => _.TryAsync(async (ctx, req, resp) => {
								resp.Cookies.Append("cookies-go-well-with", "milk");
								ctx.PerRequestItems["Marker"] = Guid.NewGuid();
								resp.Headers["X-DEBUG-MARKER"] = ctx.PerRequestItems["Marker"].ToString();
								await Console.Error.WriteLineAsync($"({ctx.PerRequestItems["Marker"]}) Request from [{ctx.RemoteEndPoint}] to {req.Uri}");
								return false;
							})
						})

						// demo: add some routing from a separate class file.
						.AddRouting(new MyRouting(routable))

						// demo: add some routes inline using lambda expressions.
						.AddRouting(new KestrelRouting(routable) {
							// demo: a couple cute static inline routes.
							_ => _.Get("/meeseeks").Do((ctx, req, resp) => resp.Write("Hi, I'm Mr. Meeseeks!")),
							_ => _.Path("/grimey").Do((ctx, req, resp) => resp.Write("I don't check methods, because I'm Homer Simpson!")),
							_ => _.Path("/err").Do((ctx, req, resp) => throw new InvalidOperationException("This call creates an error on purpose")),
							// demo: show off nested routing... for the curious, no - there is no limit.
							_ => _.Path(new Regex("/nest(?<myParameter>.*)")).Nest((_ctx, _req, _resp) => {
								var routing = new KestrelRouting(routable) {
									// demo: toss in a cheeky little static route.
									builder => builder.Get("/nest/always").Do((ctx, req, resp) => resp.Write("This route is always here for you"))
								};

								if(_req.Parameters.ContainsKey("myParameter")) {
									// let's inception up this request and dynamically compose a route to handle our url parameters.
									routing.Add(builder => builder.Get($"/nest{_req.Parameters["myParameter"]}").Do((ctx, req, resp) => resp.Write("That movie never sat well with me.")));
									routing.Add(builder => builder.Post($"/nest{_req.Parameters["myParameter"]}").Do((ctx, req, resp) => resp.Write("Did they ever explain where they got this technology?")));
									routing.Add(builder => builder.Trace($"/nest{_req.Parameters["myParameter"]}").Do((ctx, req, resp) => resp.Write("I guess you can't please everyone all the time.")));
								}

								return routing;
							})
						})

						// demo: add an overly happy finalizing route to run all the time.
						.AddRouting(RoutableEventPipelines.RouteEventFinalize, new KestrelRouting(routable) {
							// protip: if you use Try() and return false, Kestrel will be allowed to continue processing the request with other engines.
							_ => _.Do((ctx, req, resp) => resp.Headers.Add("X-TEST", "First Finalizer")),
							_ => _.Do((ctx, req, resp) => resp.Headers.Add("X-TEST-USELESS", "This finalizer is never called"))
						})

						// protip: each set of routing added to the finalize or error pipeline is executed.
						.AddRouting(RoutableEventPipelines.RouteEventFinalize, new KestrelRouting(routable) {
							_ => _.Try((ctx, req, resp) => {
								var logger = routable.GetApplicationServices().GetRequiredService<ILoggerFactory>().CreateLogger("sample");
								logger.LogInformation($"({ctx.PerRequestItems["Marker"]}) Completed ({resp.Status})");
								return false;
							}),
							_ => _.Do((ctx, req, resp) => resp.Headers.Add("X-TEST-TOO", "This finalizer will be called since the other was a Try with a false result."))
						})

						// we also have the privilege of intercepting unhandled requests and doing something with them.
						.AddRouting(RoutableEventPipelines.RouteEventFinalizeUnhandledRequests, new KestrelRouting(routable) {
							_ => _.Try((ctx, req, resp) => {
								var logger = routable.GetApplicationServices().GetRequiredService<ILoggerFactory>().CreateLogger("sample");
								if(req.Query.ContainsKey("magic") == false) {
									logger.LogInformation($"({ctx.PerRequestItems["Marker"]}) was not handled.");
									return false;
								} else {
									logger.LogInformation($"({ctx.PerRequestItems["Marker"]}) was not handled, but it's magic you see!.");
									resp.Write("Magic!? Magic is for people with dark eyeliner... this is science.");
									return true;
								}
							})
						})

						// demo: handle any exceptions.
						.OnError(new KestrelRouting(routable) {
							_ => _.DoAsync(async (context, request, response) => {
								var logger = routable.GetApplicationServices().GetRequiredService<ILoggerFactory>().CreateLogger("sample");
								response.Status = 500;
								response.Write($"{context.Error?.GetType()?.FullName} ({context.Error?.Message}):\n\t{context.Error.StackTrace.Replace("\n", "\n\t")}\n");
								logger.LogError($"({context.PerRequestItems["Marker"]}) Error: {context.Error?.GetType()?.FullName} ({context.Error?.Message})", context.Error);
							})
						})
					)
					// and of course, if a request is not handled, another kestrel hook can run it (or.. another routable!)
					.Use(async (HttpContext context, RequestDelegate next) => {
						// of course we could handle 404's in routable, but I just want to show how other things continue to function :)
						context.Response.StatusCode = 404;
						context.Response.ContentType = "text/plain";
						await context.Response.WriteAsync("Oops, sorry... I'm afraid you've stumbled straight out of the routable world and into another!\n");
						await context.Response.WriteAsync($"Here, take this to guide your way...\n");
						await context.Response.WriteAsync($"[You've been awarded the priceless ({ context.Items["Marker"]}), keep it safe!]");
					})
				)
				// log kestrel output to console (remove to disable)
				.ConfigureLogging(logging => {
					logging.ClearProviders();
					logging.AddConsole();
				})
				.Build();

			// shutdown on user (^C / SIGINT) request.
			using var cancellationSource = new CancellationTokenSource();
			Console.CancelKeyPress += (_, e) => {
				cancellationSource.Cancel();
				e.Cancel = true;
			};

			// run until cancelled.
			await server.RunAsync(cancellationSource.Token);
		}
	}
}
