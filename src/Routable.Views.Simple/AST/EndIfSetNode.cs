using Sprache;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Routable.Views.Simple.AST
{
	internal class EndIfSetNode<TContext, TRequest, TResponse> : Node<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		public static Parser<Node<TContext, TRequest, TResponse>> GetParser(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions) =>
			from condOpen in Parse.Char('@').Then(_ => Parse.String("EndIfSet"))
			select (Node<TContext, TRequest, TResponse>)new EndIfSetNode<TContext, TRequest, TResponse>(options, viewOptions);
		public EndIfSetNode(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions) : base(options, viewOptions) { }
		public override Task<bool> TryRender(StreamWriter writer, RenderContext<TContext, TRequest, TResponse> context) => Task.FromResult(true);
	}
}
