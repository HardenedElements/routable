using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Routable
{
	/// <summary>
	/// An action to execute when an error is encountered.
	/// </summary>
	/// <typeparam name="TContext">Routable request context</typeparam>
	/// <typeparam name="TRequest">Routable request</typeparam>
	/// <typeparam name="TResponse">Routable request response</typeparam>
	/// <param name="context">Routable request context</param>
	/// <param name="exception">The exception that occured</param>
	/// <returns>A task to be completed after the action is complete</returns>
	public delegate Task AsyncRoutableErrorAction<TContext, TRequest, TResponse>(TContext context, Exception exception)
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>;
}
