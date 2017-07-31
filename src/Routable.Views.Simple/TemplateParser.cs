using Routable.Views.Simple.AST;
using Sprache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Routable.Views.Simple
{
	public class TemplateParser<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		private RoutableOptions<TContext, TRequest, TResponse> Options;
		private SimpleViewOptions<TContext, TRequest, TResponse> ViewOptions;

		public TemplateParser(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions)
		{
			Options = options;
			ViewOptions = viewOptions;
		}

		private Parser<IEnumerable<IEnumerable<Node<TContext, TRequest, TResponse>>>> GetParser()
		{
			var ifSetSymbol =
				from condOpen in Parse.Char('@').Then(_ => Parse.String("IfSet("))
				from body in Parse.LetterOrDigit.Many()
				from condClose in Parse.Char(')')
				select (Node<TContext, TRequest, TResponse>)new IfSetNode<TContext, TRequest, TResponse>(Options, ViewOptions, string.Concat(body));
			var endIfSymbol =
				from condOpen in Parse.Char('@').Then(_ => Parse.String("EndIfSet"))
				select (Node<TContext, TRequest, TResponse>)new EndIfNode<TContext, TRequest, TResponse>(Options, ViewOptions);
			var modelSymbol =
				from condOpen in Parse.Char('@').Then(_ => Parse.String("Model"))
				from prop in Parse.Char('.').Then(_ => Parse.LetterOrDigit.Many()).Many()
				select (Node<TContext, TRequest, TResponse>)new ModelNode<TContext, TRequest, TResponse>(Options, ViewOptions, string.Concat(prop.SelectMany(_ => _)));
			var atSymbol =
				from at in Parse.Char('@').Many().Text()
				select (Node<TContext, TRequest, TResponse>)new ContentNode<TContext, TRequest, TResponse>(Options, ViewOptions, at);
			var contentSymbol =
				from before in Parse.CharExcept('@').Or(Parse.Char('\n')).Or(Parse.Char('\r')).Many().Text()
				select (Node<TContext, TRequest, TResponse>)new ContentNode<TContext, TRequest, TResponse>(Options, ViewOptions, before);

			var parser =
				ifSetSymbol
				.Or(endIfSymbol)
				.Or(modelSymbol)
				.Or(atSymbol)
				.Or(contentSymbol)
				.Many();

			return parser.Many();
		}
		private IEnumerable<Node<TContext, TRequest, TResponse>> RollupNodes(Node<TContext, TRequest, TResponse>[] symbols, ref int index)
		{
			var output = new List<Node<TContext, TRequest, TResponse>>();
			while(index < symbols.Length) {
				var symbol = symbols[index];
				index++;
				if(symbol is IfSetNode<TContext, TRequest, TResponse>) {
					symbol.Children.AddRange(RollupNodes(symbols, ref index));
					output.Add(symbol);
				} else if(symbol is EndIfNode<TContext, TRequest, TResponse>) {
					return output;
				} else {
					output.Add(symbol);
				}
			}
			return output;
		}
		public Template<TContext, TRequest, TResponse> TryParse(string name, DateTime? lastModified, string source)
		{
			// check cache for template.
			if(TemplateCache<TContext, TRequest, TResponse>.Fetch(name, lastModified, source, out var template) == true) {
				return template;
			}

			// parse.
			var parser = GetParser();
			var index = 0;
			var symbols = RollupNodes(parser.Parse(source).SelectMany(_ => _).ToArray(), ref index);

			// create and cache template.
			template = new Template<TContext, TRequest, TResponse>(Options, ViewOptions, symbols);
			TemplateCache<TContext, TRequest, TResponse>.Add(name, lastModified, source, template);
			return template;
		}
	}
}
