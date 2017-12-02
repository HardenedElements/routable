using Sprache;
using System.IO;
using System.Threading.Tasks;

namespace Routable.Views.Simple.AST
{
	internal class ParentNode<TContext, TRequest, TResponse> : Node<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		private const int MaximumParentDepth = 32;
		public static Parser<Node<TContext, TRequest, TResponse>> GetParser(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions) =>
			from condOpen in Parse.Char('@').Then(_ => Parse.String("Parent("))
			from body in ViewNameParser
			from condClose in Parse.Char(')')
			select new ParentNode<TContext, TRequest, TResponse>(options, viewOptions, body);
		public string ViewName { get; private set; }

		public ParentNode(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions, string viewName) : base(options, viewOptions) => ViewName = viewName;

		public async override Task<bool> TryRender(StreamWriter writer, RenderContext<TContext, TRequest, TResponse> context)
		{
			if(context.ChildrenStack.Count <= ViewOptions.MaximumParentChildRecursionDepth) {
				// push children onto stack and render the parent.
				var template = await Template<TContext, TRequest, TResponse>.Find(Options, ViewName);
				context.ChildrenStack.Push(Children);
				await template.TryRender(writer, context);
			} else {
				// it looks like we're stuck in a loop, stop and render here.
				Options.Logger?.Write(LogClass.Warning, $"View ('{context.ViewName}') has exceeded maximum parent-child depth during rendering ({ViewOptions.MaximumParentChildRecursionDepth}), not recursing anymore. (possible infinite recursion)");
				foreach(var node in Children) {
					if(await node.TryRender(writer, context) == false) {
						return false;
					}
				}
			}
			return true;
		}
	}
}
