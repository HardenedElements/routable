using System;
using System.Threading.Tasks;

namespace Routable.Views.Simple
{
	public static class SimpleViewExtensions
	{
		public static RoutableOptions<TContext, TRequest, TResponse> UseFileSystemViews<TContext, TRequest, TResponse>(this RoutableOptions<TContext, TRequest, TResponse> @this, Action<SimpleFileSystemViewOptions<TContext, TRequest, TResponse>> builder)
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
		{
			var featureOptions = new SimpleFileSystemViewOptions<TContext, TRequest, TResponse>(@this);
			builder(featureOptions);
			@this.SetFeatureOptions<SimpleViewOptions<TContext, TRequest, TResponse>>(featureOptions);
			return @this;
		}
		public static RoutableOptions<TContext, TRequest, TResponse> UseViewsWithCustomResolver<TContext, TRequest, TResponse>(this RoutableOptions<TContext, TRequest, TResponse> @this, Action<SimpleFunctionalViewOptions<TContext, TRequest, TResponse>> builder)
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
		{
			var featureOptions = new SimpleFunctionalViewOptions<TContext, TRequest, TResponse>(@this);
			builder(featureOptions);
			@this.SetFeatureOptions<SimpleViewOptions<TContext, TRequest, TResponse>>(featureOptions);
			return @this;
		}
		public async static Task WriteViewAsync<TContext, TRequest, TResponse>(this RoutableResponse<TContext, TRequest, TResponse> @this, string name, object model = null)
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
		{
			if(@this.Context.Options.TryGetFeatureOptions<SimpleViewOptions<TContext, TRequest, TResponse>>(out var options) == false) {
				// use default options.
				options = new SimpleFileSystemViewOptions<TContext, TRequest, TResponse>(@this.Context.Options);
			}

			var view = await SimpleView<TContext, TRequest, TResponse>.Find(options, name);
			@this.Attributes.ContentType = view.MimeType;
			await @this.WriteAsync(stream => view.WriteAsync(stream, model));
		}
	}
}
