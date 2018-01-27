using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Routable
{
	public static class DefaultResponseTypeHandlers
	{
		public static void EmptyResponseTypeHandler<TContext, TRequest, TResponse>(RoutableContext<TContext, TRequest, TResponse> context, object value)
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
		{
		}
		public static void ByteArrayResponseTypeHandler<TContext, TRequest, TResponse>(RoutableContext<TContext, TRequest, TResponse> context, object value)
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
		{
			var bytes = value as byte[] ?? new byte[] { };
			if(bytes.Length == 0) {
				context.Response.Abstract.ContentLength = 0;
				return;
			}

			context.Response.Abstract.ContentLength = bytes.Length;
			context.Response.Write(async (ctx, stream) => await stream.WriteAsync(bytes, 0, bytes.Length));
		}
		public static void StringResponseTypeHandler<TContext, TRequest, TResponse>(RoutableContext<TContext, TRequest, TResponse> context, object value)
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
		{
			var bytes = value == null ? new byte[] { } : context.Options.StringEncoding.GetBytes(value is string ? (string)value : value.ToString());
			if(bytes.Length == 0) {
				context.Response.Abstract.ContentLength = 0;
				return;
			}

			context.Response.Abstract.ContentLength = bytes.Length;
			context.Response.Write(async (ctx, stream) => await stream.WriteAsync(bytes, 0, bytes.Length));
		}
	}
}
