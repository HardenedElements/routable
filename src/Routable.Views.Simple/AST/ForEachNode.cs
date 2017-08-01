using Sprache;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Routable.Views.Simple.AST
{
	internal class ForEachNode<TContext, TRequest, TResponse> : Node<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		public static Parser<Node<TContext, TRequest, TResponse>> GetParser(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions) =>
							from condOpen in Parse.Char('@').Then(_ => Parse.String("ForEach("))
							from body in Parse.LetterOrDigit.Or(Parse.Char('.')).Many().Text()
							from condClose in Parse.Char(')')
							select new ForEachNode<TContext, TRequest, TResponse>(options, viewOptions, body);

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
