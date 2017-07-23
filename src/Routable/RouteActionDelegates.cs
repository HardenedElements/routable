using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Routable
{
	/// <summary>
	/// An action to be executed against a request.
	/// </summary>
	/// <typeparam name="TContext">Routable request context</typeparam>
	/// <typeparam name="TRequest">Routable request</typeparam>
	/// <typeparam name="TResponse">Routable request response</typeparam>
	/// <param name="context">Routable request context</param>
	/// <param name="request">Routable request</param>
	/// <param name="response">Routable request response</param>
	public delegate void BasicRouteAction<TContext, TRequest, TResponse>(TContext context, TRequest request, TResponse response)
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>;
	/// <summary>
	/// An asynchronous action to be executed against a request.
	/// </summary>
	/// <typeparam name="TContext">Routable request context</typeparam>
	/// <typeparam name="TRequest">Routable request</typeparam>
	/// <typeparam name="TResponse">Routable request response</typeparam>
	/// <param name="context">Routable request context</param>
	/// <param name="request">Routable request</param>
	/// <param name="response">Routable request response</param>
	/// <returns>A task indicating the action is complete</returns>
	public delegate Task AsyncBasicRouteAction<TContext, TRequest, TResponse>(TContext context, TRequest request, TResponse response)
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>;
	/// <summary>
	/// An action to be executed against a request.
	/// </summary>
	/// <typeparam name="TContext">Routable request context</typeparam>
	/// <typeparam name="TRequest">Routable request</typeparam>
	/// <typeparam name="TResponse">Routable request response</typeparam>
	/// <param name="context">Routable request context</param>
	/// <param name="request">Routable request</param>
	/// <param name="response">Routable request response</param>
	/// <returns>True or false indicating if the request was handled</returns>
	public delegate bool BypassableRouteAction<TContext, TRequest, TResponse>(TContext context, TRequest request, TResponse response)
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>;
	/// <summary>
	/// An asynchronous action to be executed against a request.
	/// </summary>
	/// <typeparam name="TContext">Routable request context</typeparam>
	/// <typeparam name="TRequest">Routable request</typeparam>
	/// <typeparam name="TResponse">Routable request response</typeparam>
	/// <param name="context">Routable request context</param>
	/// <param name="request">Routable request</param>
	/// <param name="response">Routable request response</param>
	/// <returns>A task which once complete provides a true or false value indicating if the request was handled</returns>
	public delegate Task<bool> AsyncBypassableRouteAction<TContext, TRequest, TResponse>(TContext context, TRequest request, TResponse response)
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>;
	/// <summary>
	/// An action to be executed against a request, possibly returning another routing collection.
	/// </summary>
	/// <typeparam name="TContext">Routable request context</typeparam>
	/// <typeparam name="TRequest">Routable request</typeparam>
	/// <typeparam name="TResponse">Routable request response</typeparam>
	/// <param name="context">Routable request context</param>
	/// <param name="request">Routable request</param>
	/// <param name="response">Routable request response</param>
	/// <returns>Either null (if the request was not handled), or a routing collection.</returns>
	public delegate Routing<TContext, TRequest, TResponse> NestedRouteAction<TContext, TRequest, TResponse>(TContext context, TRequest request, TResponse response)
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>;
	/// <summary>
	/// An action to be executed against a request, possibly returning another routing collection.
	/// </summary>
	/// <typeparam name="TContext">Routable request context</typeparam>
	/// <typeparam name="TRequest">Routable request</typeparam>
	/// <typeparam name="TResponse">Routable request response</typeparam>
	/// <param name="context">Routable request context</param>
	/// <param name="request">Routable request</param>
	/// <param name="response">Routable request response</param>
	/// <returns>A task that once completed provides either null (if the request was not handled), or a routing collection.</returns>
	public delegate Task<Routing<TContext, TRequest, TResponse>> AsyncNestedRouteAction<TContext, TRequest, TResponse>(TContext context, TRequest request, TResponse response)
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>;

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
