using Microsoft.AspNetCore.Builder;
using System;
using System.Linq;

namespace Routable.Kestrel
{
	public static class IApplicationBuilderExtensions
	{
		/// <summary>
		/// Add routable support to a kestrel application.
		/// </summary>
		/// <param name="optionsSetter">An action that builds routable configuration</param>
		public static IApplicationBuilder UseRoutable(this IApplicationBuilder @this, Action<RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse>> optionsSetter)
		{
			// set options.
			var options = new KestrelRoutableOptions();
			optionsSetter?.Invoke(options);

			// when invoked, run routes.
			@this.Use(async (context, next) => {
				var routableContext = new KestrelRoutableContext(options, context);
				if(await options.Invoke(routableContext) == false) {
					await next();
					return;
				}
			});
			return @this;
		}
	}
}
