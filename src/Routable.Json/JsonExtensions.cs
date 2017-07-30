using Newtonsoft.Json.Linq;
using Routable.Json;
using System;
using System.Threading.Tasks;

namespace Routable
{
	public static class JsonExtensions
	{
		public static RoutableOptions<TContext, TRequest, TResponse> WithJsonSupport<TContext, TRequest, TResponse>(this RoutableOptions<TContext, TRequest, TResponse> @this)
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
		{
			@this.ResponseTypeHandlers.Add(typeof(JObject), JsonResponseTypeHandlers.JsonResponseTypeHandler<TContext, TRequest, TResponse>);
			return @this;
		}
	}
}
