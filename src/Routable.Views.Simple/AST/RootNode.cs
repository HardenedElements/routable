using Sprache;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Routable.Views.Simple.AST
{
	internal class RootNode<TContext, TRequest, TResponse> : Node<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		public static Parser<Node<TContext, TRequest, TResponse>> GetParser(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions) =>
			from condOpen in Parse.Char('@').Then(_ => Parse.String("Root"))
			from prop in Parse.Char('.').Then(_ => Parse.LetterOrDigit.Many().Text()).Many()
			select new RootNode<TContext, TRequest, TResponse>(options, viewOptions, string.Join(".", prop));
		public string Expression { get; private set; }

		public RootNode(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions, string expression) : base(options, viewOptions) => Expression = expression;

		public async override Task<bool> TryRender(StreamWriter writer, RenderContext<TContext, TRequest, TResponse> context)
		{
			if(context.TryGetRootValue(Expression, false, out var value) == true) {
				await writer.WriteAsync(value?.ToString() ?? "");
			} else if(ViewOptions.IsUnresolvedModelExpressionExceptional) {
				throw new SimpleViewUnresolvedModelExpression(Expression);
			}
			return true;
		}
	}
}
