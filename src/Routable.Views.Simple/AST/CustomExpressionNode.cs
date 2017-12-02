using Sprache;
using System.IO;
using System.Threading.Tasks;

namespace Routable.Views.Simple.AST
{
	internal class CustomExpressionNode<TContext, TRequest, TResponse> : Node<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		private CustomExpression<TContext, TRequest, TResponse> Node;

		public CustomExpressionNode(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions, CustomExpression<TContext, TRequest, TResponse> node) : base(options, viewOptions) => Node = node;

		public async override Task<bool> TryRender(StreamWriter writer, RenderContext<TContext, TRequest, TResponse> context) => await Node.TryRender(writer, context.Model);
	}
}
