using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Routable.Json
{
	public static class JsonResponseTypeHandlers
	{
		public async static Task JsonResponseTypeHandler<TContext, TRequest, TResponse>(RoutableContext<TContext, TRequest, TResponse> context, object value)
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
		{
			context.Response.Attributes.ContentType = "application/json";

			if(value == null) {
				await context.Response.WriteAsync("");
				return;
			}

			var str = value.ToString();
			await context.Response.WriteAsync(str);
		}
	}
}
