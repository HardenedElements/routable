using System;
using System.Collections.Generic;
using System.Text;

namespace Routable.Patterns
{
	class PortPattern<TContext, TRequest, TResponse> : RoutePattern<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		public int Port { get; set; }

		public PortPattern(int port) => Port = port;

		public override bool IsMatch(TContext context) => context.Request.Uri.Port == Port;
	}
}
