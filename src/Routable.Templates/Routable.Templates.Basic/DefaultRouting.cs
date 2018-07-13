using System;
using Routable;
using Routable.Kestrel;

namespace Routable.Templates.Basic
{
	public sealed class DefaultRouting : KestrelRouting
	{
		public DefaultRouting(RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> options) : base(options)
		{
			Add(route => route.Get("/").Do(OnIndex));
		}

		private void OnIndex(KestrelRoutableContext context, KestrelRoutableRequest request, KestrelRoutableResponse response)
		{
			response.Write("Hello World");
		}
	}
}