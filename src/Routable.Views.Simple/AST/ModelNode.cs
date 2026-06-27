using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Superpower;
using Superpower.Parsers;

namespace Routable.Views.Simple.AST
{
	internal class ModelNode<TContext, TRequest, TResponse> : Node<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		public static TextParser<Node<TContext, TRequest, TResponse>> GetParser(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions) =>
			from condOpen in Character.EqualTo('@').IgnoreThen(Span.EqualTo("Model"))
			from prop in Character.EqualTo('.').IgnoreThen(Character.LetterOrDigit.Many().Text()).Many()
			select (Node<TContext, TRequest, TResponse>)new ModelNode<TContext, TRequest, TResponse>(options, viewOptions, string.Join(".", prop));
		public string Expression { get; private set; }

		public ModelNode(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions, string expression) : base(options, viewOptions) => Expression = expression;

		public async override Task<bool> TryRender(StreamWriter writer, RenderContext<TContext, TRequest, TResponse> context)
		{
			if(context.TryGetValue(Expression, false, out var value) == true) {
				await writer.WriteAsync(value?.ToString() ?? "");
			} else if(ViewOptions.IsUnresolvedModelExpressionExceptional) {
				throw new SimpleViewUnresolvedModelExpression(Expression);
			}
			return true;
		}
	}
}
