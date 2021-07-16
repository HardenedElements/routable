using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Routable.Kestrel;

namespace Routable.Templates.Basic
{
	public sealed class Program
	{
		public static async Task Main(string[] args)
		{
			using var server = new WebHostBuilder()
				.SuppressStatusMessages(true)
				.UseKestrel(kestrelOptions => kestrelOptions.Listen(IPAddress.Any, 8080))
				.Configure(appBuilder => appBuilder
					.UseRoutable(routable => routable
						.AddRouting(new DefaultRouting(routable))
						.OnError(new KestrelRouting(routable) {
							_ => _.DoAsync(async (context, request, response) => {
								response.Status = 500;
								response.ClearPendingWrites();
								var logger = routable.GetApplicationServices().GetRequiredService<ILoggerFactory>().CreateLogger<Program>();
								logger.LogError($"Request ({context.PlatformContext.TraceIdentifier}) error: {context.Error?.GetType()?.FullName} ({context.Error?.Message})", context.Error);
							})
						})
					)
				)
				.ConfigureLogging(logging => {
					logging.ClearProviders();
					// log to console. if this is undesired, remove this line.
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
