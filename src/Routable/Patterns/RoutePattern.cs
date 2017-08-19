using System;
using System.Collections.Generic;
using System.Text;

namespace Routable.Patterns
{
	public abstract class RoutePattern<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		public abstract bool IsMatch(TContext context);
		protected virtual void AddParameter(TContext context, string name, object value) => context.Request.AddParameter(name, value);
	}
}
