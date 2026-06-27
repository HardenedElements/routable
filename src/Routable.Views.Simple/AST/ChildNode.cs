using System.IO;
using System.Threading.Tasks;
using Superpower;
using Superpower.Parsers;

namespace Routable.Views.Simple.AST
{
	internal class ChildNode<TContext, TRequest, TResponse> : Node<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		public static TextParser<Node<TContext, TRequest, TResponse>> GetParser(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions) =>
			from node in Character.EqualTo('@').IgnoreThen(Span.EqualTo("Child"))
			select (Node<TContext, TRequest, TResponse>)new ChildNode<TContext, TRequest, TResponse>(options, viewOptions);

		public ChildNode(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions) : base(options, viewOptions) { }

		public async override Task<bool> TryRender(StreamWriter writer, RenderContext<TContext, TRequest, TResponse> context)
		{
			if(context.ChildrenStack.Count > 0) {
				foreach(var node in context.ChildrenStack.Pop()) {
					if(await node.TryRender(writer, context) == false) {
						return false;
					}
				}
			}
			return true;
		}
	}
}
