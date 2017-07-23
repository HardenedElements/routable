using System;
using System.Collections.Generic;
using System.Text;

namespace Routable
{
	/// <summary>
	/// Creates a new route.
	/// </summary>
	/// <typeparam name="TContext">Routable request context</typeparam>
	/// <typeparam name="TRequest">Routable request</typeparam>
	/// <typeparam name="TResponse">Routable request response</typeparam>
	public class RouteFactory<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		/// <summary>
		/// Creates a new route.
		/// </summary>
		public virtual Route<TContext, TRequest, TResponse> Create() => new Route<TContext, TRequest, TResponse>();
	}
}
