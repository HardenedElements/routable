using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Routable.Kestrel
{
	public partial class KestrelRoutableOptions : RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse>
	{
		public IServiceProvider ApplicationServices { get; private set; }

		public KestrelRoutableOptions(IServiceProvider applicationServices)
		{
			ApplicationServices = applicationServices;
			if(applicationServices?.GetService(typeof(ILoggerFactory)) is ILoggerFactory factory) {
				Logger = new MicrosoftLoggingLogger(factory.CreateLogger("routable"));
			}
		}

		public Task<bool> Invoke(KestrelRoutableContext context) => InvokeRouting(context);
	}
}
