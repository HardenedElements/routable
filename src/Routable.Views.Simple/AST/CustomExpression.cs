using Sprache;
using System.IO;
using System.Threading.Tasks;

namespace Routable.Views.Simple
{
	public abstract class CustomExpression<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		protected CustomExpression() { }
		public abstract Task<bool> TryRender(StreamWriter writer, object model);
	}
}
