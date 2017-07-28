using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Routable
{
	public class BasicRouteAction<TContext, TRequest, TResponse> : RouteAction<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		private Func<TContext, TRequest, TResponse, Task<bool>> Action;

		private BasicRouteAction() { }
		public async override Task<bool> Invoke(TContext context) => await Action.Invoke(context, context.Request, context.Response);

		public static explicit operator BasicRouteAction<TContext, TRequest, TResponse>(Action<TContext, TRequest, TResponse> action)
		{
			return new BasicRouteAction<TContext, TRequest, TResponse> {
				Action = (ctx, req, resp) => {
					action(ctx, req, resp);
					return Task.FromResult(true);
				}
			};
		}
		public static explicit operator BasicRouteAction<TContext, TRequest, TResponse>(Func<TContext, TRequest, TResponse, Task> action)
		{
			return new BasicRouteAction<TContext, TRequest, TResponse> {
				Action = async (ctx, req, resp) => {
					await action(ctx, req, resp);
					return true;
				}
			};
		}
		public static explicit operator BasicRouteAction<TContext, TRequest, TResponse>(Func<TContext, TRequest, TResponse, bool> action)
		{
			return new BasicRouteAction<TContext, TRequest, TResponse> { Action = (ctx, req, resp) => Task.FromResult(action(ctx, req, resp)) };
		}
		public static explicit operator BasicRouteAction<TContext, TRequest, TResponse>(Func<TContext, TRequest, TResponse, Task<bool>> action)
		{
			return new BasicRouteAction<TContext, TRequest, TResponse> { Action = async (ctx, req, resp) => await action(ctx, req, resp) };
		}
	}
}
