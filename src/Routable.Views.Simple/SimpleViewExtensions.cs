using System;
using System.Threading.Tasks;

namespace Routable.Views.Simple
{
	public static class SimpleViewExtensions
	{
		public static RoutableOptions<TContext, TRequest, TResponse> UseSimpleViews<TContext, TRequest, TResponse>(this RoutableOptions<TContext, TRequest, TResponse> @this, Action<SimpleViewOptions<TContext, TRequest, TResponse>> builder)
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
		{
			var details = new SimpleViewOptions<TContext, TRequest, TResponse>(@this);
			builder(details);
			@this.SetOptionDetails(details);
			return @this;
		}

		public static Task WriteViewAsync<TContext, TRequest, TResponse>(this RoutableResponse<TContext, TRequest, TResponse> @this, string name, object model = null)
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
		{
			if(@this.Context.Options.TryGetOptionDetails<SimpleViewOptions<TContext, TRequest, TResponse>>(out var options) == false) {
				throw new InvalidOperationException($"Simple view support has not been configured, use '{nameof(UseSimpleViews)}'");
			}

			var view = SimpleView<TContext, TRequest, TResponse>.Find(options, name);
			@this.Attributes.ContentType = view.MimeType;
			return @this.WriteAsync(stream => view.WriteAsync(stream, model));
		}
	}
}
