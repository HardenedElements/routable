using System;
using System.Collections.Generic;
using System.Text;

namespace Routable.Patterns
{
	class MethodPattern<TContext, TRequest, TResponse> : RoutePattern<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		public string Method { get; set; }

		public MethodPattern(string method) => Method = method?.ToUpper();

		public override bool IsMatch(RoutableContext<TContext, TRequest, TResponse> context) => context.Request.GetMethodAsString()?.ToUpper() == Method;
	}
}
