using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Superpower;
using Superpower.Parsers;

namespace Routable.Views.Simple.AST
{
	internal class ForEachNode<TContext, TRequest, TResponse> : Node<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		public static TextParser<Node<TContext, TRequest, TResponse>> GetParser(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions) =>
							from condOpen in Character.EqualTo('@').IgnoreThen(Span.EqualTo("ForEach("))
							from body in Character.LetterOrDigit.Or(Character.EqualTo('.')).Many().Text()
							from condClose in Character.EqualTo(')')
							select (Node<TContext, TRequest, TResponse>)new ForEachNode<TContext, TRequest, TResponse>(options, viewOptions, body);

		public string Expression { get; private set; }

		public ForEachNode(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions, string expression) : base(options, viewOptions) => Expression = expression;

		public async override Task<bool> TryRender(StreamWriter writer, RenderContext<TContext, TRequest, TResponse> context)
		{
			// TODO: add logging if the expression cannot be resolved.
			if(context.TryGetValue(Expression, true, out var value) == true && value is IEnumerable enumeration) {
				foreach(var entry in enumeration) {
					context.Push(entry);
					try {
						foreach(var node in Children) {
							if(await node.TryRender(writer, context) == false) {
								return false;
							}
						}
					} finally {
						context.Pop();
					}
				}
			}
			return true;
		}
	}
}
