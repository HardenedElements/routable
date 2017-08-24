using System;
using System.Collections.Generic;
using System.Text;

namespace Routable.Patterns
{
	class PathPattern<TContext, TRequest, TResponse> : RoutePattern<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		public string Path { get; set; }

		public PathPattern(string path)
		{
			if(path == "/") {
				Path = path;
			} else {
				Path = path?.ToLower()?.TrimEnd('/');
				if(Path.StartsWith("/") == false) {
					Path = $"/{Path}";
				}
			}
		}

		public override bool IsMatch(TContext context)
		{
			var requestPath = context.Request.Uri.AbsolutePath?.ToLower()?.TrimEnd('/');
			if(requestPath.StartsWith("/") == false) {
				requestPath = $"/{requestPath}";
			}

			return requestPath == Path;
		}
	}
}
