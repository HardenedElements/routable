using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Routable;
using Routable.Kestrel;

namespace Routable.Templates.Basic
{
	public sealed class DefaultRouting : KestrelRouting
	{
		private readonly ILogger Logger;
		public DefaultRouting(RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> options) : base(options)
		{
			Logger = options.GetApplicationServices().GetRequiredService<ILoggerFactory>().CreateLogger<DefaultRouting>();
			Add(route => route.Get("/").Do(OnIndex));
		}

		private void OnIndex(KestrelRoutableContext context, KestrelRoutableRequest request, KestrelRoutableResponse response)
		{
			Logger.LogDebug("Sent client a 'Hello World'");
			response.Write("Hello World");
		}
	}
}