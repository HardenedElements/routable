using System;
using System.Collections.Generic;
using System.Linq;
using Routable.Views.Simple.AST;
using Superpower;

namespace Routable.Views.Simple
{
	internal class TemplateParser<TContext, TRequest, TResponse>
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

		private IEnumerable<TextParser<Node<TContext, TRequest, TResponse>>> GetParsers()
		{
			return new[] {
				IfSetNode<TContext, TRequest, TResponse>.GetParser(Options, ViewOptions),
				EndIfSetNode<TContext, TRequest, TResponse>.GetParser(Options, ViewOptions),
				ForEachNode<TContext, TRequest, TResponse>.GetParser(Options, ViewOptions),
				EndForEachNode<TContext, TRequest, TResponse>.GetParser(Options, ViewOptions),
				IncludeNode<TContext, TRequest, TResponse>.GetParser(Options, ViewOptions),
				ModelNode<TContext, TRequest, TResponse>.GetParser(Options, ViewOptions),
				RootNode<TContext, TRequest, TResponse>.GetParser(Options, ViewOptions),
				ParentNode<TContext, TRequest, TResponse>.GetParser(Options, ViewOptions),
				ChildNode<TContext, TRequest, TResponse>.GetParser(Options, ViewOptions)
			}
			.Concat(ViewOptions.CustomExpressionParsers.Select(_ => _.Select(e => (Node<TContext, TRequest, TResponse>)new CustomExpressionNode<TContext, TRequest, TResponse>(Options, ViewOptions, e))))
			.Concat(new[] { ContentNode<TContext, TRequest, TResponse>.GetParser(Options, ViewOptions) });
		}
		private TextParser<Node<TContext, TRequest, TResponse>[]> GetParser()
		{
			// Each alternative begins by consuming '@'; Try() preserves backtracking across the
			// alternation since Superpower's Or does not retry once an alternative has consumed input.
			TextParser<Node<TContext, TRequest, TResponse>> current = null;
			foreach(var parser in GetParsers()) {
				var alternative = parser.Try();
				current = current == null ? alternative : current.Or(alternative);
			}
			return current.Many();
		}
		private IEnumerable<Node<TContext, TRequest, TResponse>> RollupNodes(string name, Node<TContext, TRequest, TResponse>[] symbols, ref int index)
		{
			ParentNode<TContext, TRequest, TResponse> parent = null;
			var output = new List<Node<TContext, TRequest, TResponse>>();
			while(index < symbols.Length) {
				var symbol = symbols[index];
				index++;
				if(symbol is IfSetNode<TContext, TRequest, TResponse>) {
					symbol.Children.AddRange(RollupNodes(name, symbols, ref index));
					output.Add(symbol);
				} else if(symbol is ForEachNode<TContext, TRequest, TResponse>) {
					symbol.Children.AddRange(RollupNodes(name, symbols, ref index));
					output.Add(symbol);
				} else if(symbol is EndIfSetNode<TContext, TRequest, TResponse> || symbol is EndForEachNode<TContext, TRequest, TResponse>) {
					return output;
				} else if(symbol is ParentNode<TContext, TRequest, TResponse> parentNode) {
					if(parent != null) {
						throw new SimpleViewParserException(name, "Only one Parent() symbol allowed per template");
					}
					parent = parentNode;
				} else {
					output.Add(symbol);
				}
			}
			if(parent != null) {
				parent.Children.AddRange(output);
				return new[] { parent };
			} else {
				return output;
			}
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
			var symbols = RollupNodes(name, parser.Parse(source), ref index);

			// create and cache template.
			template = new Template<TContext, TRequest, TResponse>(Options, ViewOptions, symbols);
			TemplateCache<TContext, TRequest, TResponse>.Add(name, lastModified, source, template);
			return template;
		}
	}
}
