using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Routable.Views.Simple.AST
{
	internal abstract class Node<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		protected RoutableOptions<TContext, TRequest, TResponse> Options { get; private set; }
		protected SimpleViewOptions<TContext, TRequest, TResponse> ViewOptions { get; private set; }
		public List<Node<TContext, TRequest, TResponse>> Children { get; } = new List<Node<TContext, TRequest, TResponse>>();

		public Node(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions)
		{
			Options = options;
			ViewOptions = viewOptions;
		}

		public abstract Task<bool> TryRender(StreamWriter writer, RenderContext<TContext, TRequest, TResponse> context);
	}
}
