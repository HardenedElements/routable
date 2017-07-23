using System.IO;
using System.Threading.Tasks;

namespace Routable
{
	/// <summary>
	/// Write a given response to the response of a request context.
	/// </summary>
	/// <typeparam name="TContext">Routable request context</typeparam>
	/// <typeparam name="TRequest">Routable request</typeparam>
	/// <typeparam name="TResponse">Routable request response</typeparam>
	/// <param name="context">Routable request context</param>
	/// <param name="value">Value to write</param>
	/// <returns>A task to be completed after the value has been written</returns>
	public delegate Task ResponseTypeHandler<TContext, TRequest, TResponse>(RoutableContext<TContext, TRequest, TResponse> context, object value)
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>;
}
