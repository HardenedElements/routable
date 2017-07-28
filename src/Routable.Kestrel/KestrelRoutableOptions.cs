using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Routable.Kestrel
{
	internal class KestrelRoutableOptions : RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse>
	{
		internal Task<bool> Invoke(KestrelRoutableContext context) => InvokeRouting(context);
	}
}
