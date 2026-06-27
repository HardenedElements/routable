using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Superpower;
using Superpower.Parsers;

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

		protected static TextParser<string> ViewNameParser => Character.LetterOrDigit.Or(CharSetParser('.', '-', '_', ' ', ',', '/', '\\')).Many().Text();
		protected static TextParser<char> CharSetParser(params char[] chars)
		{
			TextParser<char> parser = null;
			foreach(var ch in chars) {
				parser = parser == null ? Character.EqualTo(ch) : parser.Or(Character.EqualTo(ch));
			}
			return parser;
		}

		public abstract Task<bool> TryRender(StreamWriter writer, RenderContext<TContext, TRequest, TResponse> context);
	}
}
