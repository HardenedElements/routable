using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Routable
{
	public abstract class RouteAction<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		public abstract Task<bool> Invoke(TContext context);
	}
}
