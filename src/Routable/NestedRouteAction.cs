using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Routable
{
	public class NestedRouteAction<TContext, TRequest, TResponse> : RouteAction<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		private Func<TContext, TRequest, TResponse, Task<Routing<TContext, TRequest, TResponse>>> Action;

		private NestedRouteAction() { }
		public async override Task<bool> Invoke(TContext context) {
			// invoke the action and check if we received some routing.
			var routing = await Action.Invoke(context, context.Request, context.Response);
			if(routing == null) {
				return false;
			}

			// run through each route and process them until one is successful.
			var routes = routing.Routes.Where(_ => _.IsMatch(context));
			foreach(var route in routes) {
				if(await route.Invoke(context) == true) {
					return true;
				}
			}

			return false;
		}

		public static implicit operator NestedRouteAction<TContext, TRequest, TResponse>(Func<TContext, TRequest, TResponse, Routing<TContext, TRequest, TResponse>> action)
		{
			return new NestedRouteAction<TContext, TRequest, TResponse> { Action = (ctx, req, resp) => Task.FromResult(action(ctx, req, resp)) };
		}
		public static implicit operator NestedRouteAction<TContext, TRequest, TResponse>(Func<TContext, TRequest, TResponse, Task<Routing<TContext, TRequest, TResponse>>> action)
		{
			return new NestedRouteAction<TContext, TRequest, TResponse> { Action = async (ctx, req, resp) => await action(ctx, req, resp) };
		}
	}
}
