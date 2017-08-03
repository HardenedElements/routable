using Sprache;
using System.IO;
using System.Threading.Tasks;

namespace Routable.Views.Simple.AST
{
	internal class IncludeNode<TContext, TRequest, TResponse> : Node<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		public static Parser<Node<TContext, TRequest, TResponse>> GetParser(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions) =>
			from condOpen in Parse.Char('@').Then(_ => Parse.String("Include("))
			from body in Parse.LetterOrDigit.Or(Parse.Char('.')).Or(Parse.Char('/')).Or(Parse.Char('\\')).Many().Text()
			from condClose in Parse.Char(')')
			select new IncludeNode<TContext, TRequest, TResponse>(options, viewOptions, body);
		public string ViewName { get; private set; }

		public IncludeNode(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions, string viewName) : base(options, viewOptions) => ViewName = viewName;

		public async override Task<bool> TryRender(StreamWriter writer, RenderContext<TContext, TRequest, TResponse> context)
		{
			var template = await Template<TContext, TRequest, TResponse>.Find(Options, ViewName);
			await template.TryRender(writer, context);
			return true;
		}
	}
}
