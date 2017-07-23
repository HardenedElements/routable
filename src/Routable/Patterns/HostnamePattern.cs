using System;
using System.Collections.Generic;
using System.Text;

namespace Routable.Patterns
{
	class HostnamePattern<TContext, TRequest, TResponse> : RoutePattern<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		public string Hostname { get; set; }

		public HostnamePattern(string hostname) => Hostname = hostname?.ToLower();

		public override bool IsMatch(RoutableContext<TContext, TRequest, TResponse> context) => context.Request.Uri.Host == Hostname;
	}
}
