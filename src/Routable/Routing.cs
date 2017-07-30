using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Routable
{
	/// <summary>
	/// A collection of routes.
	/// </summary>
	/// <typeparam name="TContext">Routable request context</typeparam>
	/// <typeparam name="TRequest">Routable request</typeparam>
	/// <typeparam name="TResponse">Routable request response</typeparam>
	public class Routing<TContext, TRequest, TResponse> : IEnumerable<Route<TContext, TRequest, TResponse>>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		/// <summary>
		/// Routable options
		/// </summary>
		protected RoutableOptions<TContext, TRequest, TResponse> Options { get; private set; }
		private IList<Route<TContext, TRequest, TResponse>> _Routes = new List<Route<TContext, TRequest, TResponse>>();
		/// <summary>
		/// Routes in this routing collection.
		/// </summary>
		public IReadOnlyList<Route<TContext, TRequest, TResponse>> Routes => (IReadOnlyList<Route<TContext, TRequest, TResponse>>)_Routes;

		/// <summary>
		/// Create a new routing collection
		/// </summary>
		/// <param name="options">Options to apply to this routing collection</param>
		public Routing(RoutableOptions<TContext, TRequest, TResponse> options) => Options = options;

		/// <summary>
		/// Add a route to this collection.
		/// </summary>
		/// <param name="builder">An action used to construct a single route</param>
		public void Add(Action<Route<TContext, TRequest, TResponse>> builder)
		{
			var route = Options.RouteFactory.Create();
			builder(route);
			_Routes.Add(route);
		}

		public IEnumerator<Route<TContext, TRequest, TResponse>> GetEnumerator() => _Routes.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
