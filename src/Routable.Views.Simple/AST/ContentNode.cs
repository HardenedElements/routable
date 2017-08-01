using Sprache;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Routable.Views.Simple.AST
{
	internal class ContentNode<TContext, TRequest, TResponse> : Node<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		public static Parser<Node<TContext, TRequest, TResponse>> GetParser(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions)
		{
			var atSymbols = from at in Parse.Char('@').Many().Text()
											select (Node<TContext, TRequest, TResponse>)new ContentNode<TContext, TRequest, TResponse>(options, viewOptions, at);

			var contentSymbols = from before in Parse.CharExcept('@').Or(Parse.Char('\n')).Or(Parse.Char('\r')).Many().Text()
													 select (Node<TContext, TRequest, TResponse>)new ContentNode<TContext, TRequest, TResponse>(options, viewOptions, before);

			return atSymbols.Or(contentSymbols);
		}
		public string Content { get; private set; }

		public ContentNode(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions, string content) : base(options, viewOptions) => Content = content;

		public async override Task<bool> TryRender(StreamWriter writer, RenderContext<TContext, TRequest, TResponse> context)
		{
			await writer.WriteAsync(Content);
			return true;
		}
	}
}
