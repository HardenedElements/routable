using System;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Routable.Kestrel;

namespace Routable.Templates.Basic
{
	public sealed class Program
	{
		public static void Main(string[] args)
		{
			var server = new WebHostBuilder()
				.UseKestrel(kestrelOptions => kestrelOptions.Listen(IPAddress.Any, 8080))
				.Configure(appBuilder => appBuilder
					.UseRoutable(routable => routable
						.AddRouting(new DefaultRouting(routable))
						.OnError(new KestrelRouting(routable) {
							_ => _.DoAsync(async (context, request, response) => {
								response.Status = 500;
								response.ClearPendingWrites();
								await Console.Error.WriteLineAsync($"Error: {context.Error?.GetType()?.FullName} ({context.Error?.Message}):\n\t{context.Error?.StackTrace?.Replace("\n", "\n\t")}\n");
							})
						})
					)
				)
				.Build();

			using (server)
			{
				server.Run();
			}
		}
	}
}
