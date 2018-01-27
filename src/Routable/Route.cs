using Routable.Patterns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Routable
{
	public class Route<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		public RoutableOptions<TContext, TRequest, TResponse> RoutableOptions { get; private set; }
		protected List<RouteAction<TContext, TRequest, TResponse>> Actions = new List<RouteAction<TContext, TRequest, TResponse>>();

		private IList<RoutePattern<TContext, TRequest, TResponse>> _Patterns = new List<RoutePattern<TContext, TRequest, TResponse>>();
		private IReadOnlyList<RoutePattern<TContext, TRequest, TResponse>> Patterns => (IReadOnlyList<RoutePattern<TContext, TRequest, TResponse>>)_Patterns;

		protected internal Route(RoutableOptions<TContext, TRequest, TResponse> options) => RoutableOptions = options;

		/// <summary>
		/// Determine if a route can handle a request context.
		/// </summary>
		/// <param name="context">Routable request context</param>
		/// <returns>Indicates if this route has a pattern that matches a given request context</returns>
		public virtual bool IsMatch(TContext context) => Patterns.All(_ => _.IsMatch(context));
		/// <summary>
		/// Invoke a route against a given request context.
		/// </summary>
		/// <param name="context">Routable request context</param>
		/// <returns>Task that indicates when the request completes, providing a value indicating whether this route was able to handle the request</returns>
		public virtual async Task<bool> Invoke(TContext context)
		{
			if(Actions == null || Actions.Any() == false) {
				return false;
			} else {
				foreach(var action in Actions) {
					if(await action.Invoke(context) == true) {
						return true;
					}
				}
				return false;
			}
		}

		/// <summary>
		/// Carry out an action when the route is invoked.
		/// </summary>
		public Route<TContext, TRequest, TResponse> Do(RouteAction<TContext, TRequest, TResponse> action)
		{
			Actions.Add(action);
			return this;
		}
		public Route<TContext, TRequest, TResponse> Do(Action<TContext, TRequest, TResponse> action) => Do((BasicRouteAction<TContext, TRequest, TResponse>)action);
		public Route<TContext, TRequest, TResponse> DoAsync(Func<TContext, TRequest, TResponse, Task> action) => Do((BasicRouteAction<TContext, TRequest, TResponse>)action);
		public Route<TContext, TRequest, TResponse> Try(Func<TContext, TRequest, TResponse, bool> action) => Do((BasicRouteAction<TContext, TRequest, TResponse>)action);
		public Route<TContext, TRequest, TResponse> TryAsync(Func<TContext, TRequest, TResponse, Task<bool>> action) => Do((BasicRouteAction<TContext, TRequest, TResponse>)action);
		public Route<TContext, TRequest, TResponse> Nest(Func<TContext, TRequest, TResponse, Task<Routing<TContext, TRequest, TResponse>>> action) => Do((NestedRouteAction<TContext, TRequest, TResponse>)action);
		public Route<TContext, TRequest, TResponse> Nest(Func<TContext, TRequest, TResponse, Routing<TContext, TRequest, TResponse>> action) => Do((NestedRouteAction<TContext, TRequest, TResponse>)action);

		/// <summary>
		/// Restrict a route to a given pattern.
		/// </summary>
		/// <param name="pattern">A route pattern</param>
		public Route<TContext, TRequest, TResponse> Where(RoutePattern<TContext, TRequest, TResponse> pattern)
		{
			_Patterns.Add(pattern);
			return this;
		}
		/// <summary>
		/// Restrict a route to requests for a given hostname.
		/// </summary>
		/// <param name="hostname">Host as presented to the server platform</param>
		public Route<TContext, TRequest, TResponse> Host(string hostname)
		{
			_Patterns.Add(new HostnamePattern<TContext, TRequest, TResponse>(hostname));
			return this;
		}
		/// <summary>
		/// Restrict a route to requests on a given port.
		/// </summary>
		public Route<TContext, TRequest, TResponse> Port(int port)
		{
			_Patterns.Add(new PortPattern<TContext, TRequest, TResponse>(port));
			return this;
		}
		/// <summary>
		/// Restrict a route to a given request path.
		/// </summary>
		public Route<TContext, TRequest, TResponse> Path(string path)
		{
			_Patterns.Add(new PathPattern<TContext, TRequest, TResponse>(path));
			return this;
		}
		/// <summary>
		/// Restrict a route to a given request path, as matched by regular expression.
		/// </summary>
		/// <param name="pattern">A regular expression to apply to a request path (named captures will be saved to request parameters)</param>
		public Route<TContext, TRequest, TResponse> Path(Regex pattern)
		{
			_Patterns.Add(new PathRegexPattern<TContext, TRequest, TResponse>(pattern));
			return this;
		}

		#region Method Patterns
		/// <summary>
		/// Restrict request to a given method (and a path if provided)
		/// </summary>
		/// <param name="method">Request method</param>
		/// <param name="path">May be null</param>
		public Route<TContext, TRequest, TResponse> Method(string method, string path = null)
		{
			if(path != null) {
				Path(path);
			}
			_Patterns.Add(new MethodPattern<TContext, TRequest, TResponse>(method));
			return this;
		}
		/// <summary>
		/// Restrict request to a GET method (and a path if provided)
		/// </summary>
		/// <param name="path">May be null</param>
		public Route<TContext, TRequest, TResponse> Get(string path = null) => Method("GET", path);
		/// <summary>
		/// Restrict request to a POST method (and a path if provided)
		/// </summary>
		/// <param name="path">May be null</param>
		public Route<TContext, TRequest, TResponse> Post(string path = null) => Method("POST", path);
		/// <summary>
		/// Restrict request to a PUT method (and a path if provided)
		/// </summary>
		/// <param name="path">May be null</param>
		public Route<TContext, TRequest, TResponse> Put(string path = null) => Method("PUT", path);
		/// <summary>
		/// Restrict request to a DELETE method (and a path if provided)
		/// </summary>
		/// <param name="path">May be null</param>
		public Route<TContext, TRequest, TResponse> Delete(string path = null) => Method("DELETE", path);
		/// <summary>
		/// Restrict request to a HEAD method (and a path if provided)
		/// </summary>
		/// <param name="path">May be null</param>
		public Route<TContext, TRequest, TResponse> Head(string path = null) => Method("HEAD", path);
		/// <summary>
		/// Restrict request to an OPTIONS  method (and a path if provided)
		/// </summary>
		/// <param name="path">May be null</param>
		public Route<TContext, TRequest, TResponse> Options(string path = null) => Method("OPTIONS", path);
		/// <summary>
		/// Restrict request to a TRACE method (and a path if provided)
		/// </summary>
		/// <param name="path">May be null</param>
		public Route<TContext, TRequest, TResponse> Trace(string path = null) => Method("TRACE", path);
		#endregion
	}
}
