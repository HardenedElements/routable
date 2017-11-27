using System;
using System.Collections.Generic;
using System.Text;

namespace Routable
{
	public enum RoutableEventPipelines
	{
		RouteEventInitialize,
		RouteEventMain,
		RouteEventError,
		RouteEventFinalize,
		RouteEventFinalizeUnhandledRequests
	}
}
