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
		public static void JsonResponseTypeHandler<TContext, TRequest, TResponse>(RoutableContext<TContext, TRequest, TResponse> context, object value)
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
		{
			context.Response.Abstract.ContentType = "application/json";

			if(value == null) {
				context.Response.Write("");
				return;
			}

			var str = value.ToString();
			context.Response.Write(str);
		}
	}
}
