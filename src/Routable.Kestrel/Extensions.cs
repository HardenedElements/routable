using System;
using System.Collections.Generic;
using System.Text;

namespace Routable.Kestrel
{
	public static class RoutableOptionsServiceExtension
	{
		public static IServiceProvider GetApplicationServices(this RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> @this)
		{
			if(!(@this is KestrelRoutableOptions options)) {
				throw new InvalidOperationException("Incompatible implementation of RoutableOptions");
			}

			return options.ApplicationServices;
		}
	}
}
