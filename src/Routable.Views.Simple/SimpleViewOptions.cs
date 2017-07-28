using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Routable.Views.Simple
{
	public class ResolveViewArgs : EventArgs
	{
		public string Name { get; set; }
		public string MimeType { get; set; }
		public Func<Task<Stream>> GetStream { get; set; }
		public bool Success { get; set; } = false;
	}
	public class UnresolvedModelKeyEventArgs : EventArgs
	{
		public string MimeType { get; set; }
		public string Expression { get; set; }
		public IEnumerable<string> PathComponents { get; set; }
		public object Model { get; set; }
		public bool Success { get; set; } = false;
		public string Value { get; set; }
	}
	public abstract class SimpleViewOptions<TContext, TRequest, TResponse>
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		public RoutableOptions<TContext, TRequest, TResponse> RoutableOptions { get; private set; }
		public event EventHandler<UnresolvedModelKeyEventArgs> ResolveUnresolvedModelKey;

		public SimpleViewOptions(RoutableOptions<TContext, TRequest, TResponse> options) => RoutableOptions = options;

		internal bool TryResolveUnresolvedModelKey(string mimeType, string expression, IEnumerable<string> pathComponents, object model, out string value)
		{
			var args = new UnresolvedModelKeyEventArgs {
				MimeType = mimeType,
				Expression = expression,
				PathComponents = pathComponents,
				Model = model
			};
			ResolveUnresolvedModelKey?.Invoke(this, args);
			if(args.Success == true) {
				value = args.Value;
				return true;
			} else {
				value = null;
				return false;
			}
		}
		internal abstract Task ResolveView(ResolveViewArgs resolveViewArgs);
	}
}
