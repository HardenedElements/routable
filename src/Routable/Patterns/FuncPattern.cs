using System;
using System.Collections.Generic;
using System.Text;

namespace Routable.Patterns
{
	public sealed class FuncPattern<TContext, TRequest, TResponse> : RoutePattern<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		private Predicate<TContext> Predicate;

		public FuncPattern(Predicate<TContext> predicate) => Predicate = predicate;
		public override bool IsMatch(TContext context) => Predicate(context);
	}
}
