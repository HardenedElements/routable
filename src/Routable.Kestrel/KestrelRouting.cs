using System;
using System.Collections.Generic;
using System.Text;

namespace Routable.Kestrel
{
	public class KestrelRouting : Routing<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse>
	{
		public KestrelRouting(RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> options) : base(options) { }
	}
}
