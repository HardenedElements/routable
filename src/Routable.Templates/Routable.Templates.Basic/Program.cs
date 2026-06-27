using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Routable.Kestrel;

namespace Routable.Templates.Basic;

public sealed class Program
{
	public static async Task Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);
		builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(8080));

		var app = builder.Build();

		app.UseRoutable(options => options
			.AddRouting(new DefaultRouting(options))
			.OnError(new KestrelRouting(options) {
				_ => _.DoAsync(async (context, request, response) => {
					response.Status = 500;
					response.ClearPendingWrites();
					var logger = options.GetApplicationServices().GetRequiredService<ILoggerFactory>().CreateLogger<Program>();
					logger.LogError($"Request ({context.PlatformContext.TraceIdentifier}) error: {context.Error?.GetType()?.FullName} ({context.Error?.Message})", context.Error);
				})
			})
		);

		await app.RunAsync();
	}
}
