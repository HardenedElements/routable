using System;
using System.Collections.Generic;
using System.IO;
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
			if(@this.TryGetFeatureOptions<List<SimpleViewOptions<TContext, TRequest, TResponse>>>(out var list) == false) {
				list = new List<SimpleViewOptions<TContext, TRequest, TResponse>>();
				@this.SetFeatureOptions(list);
			}

			var featureOptions = new SimpleFileSystemViewOptions<TContext, TRequest, TResponse>(@this);
			builder(featureOptions);
			list.Add(featureOptions);
			return @this;
		}
		public static RoutableOptions<TContext, TRequest, TResponse> UseViewsWithCustomResolver<TContext, TRequest, TResponse>(this RoutableOptions<TContext, TRequest, TResponse> @this, Action<SimpleFunctionalViewOptions<TContext, TRequest, TResponse>> builder)
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
		{
			if(@this.TryGetFeatureOptions<List<SimpleViewOptions<TContext, TRequest, TResponse>>>(out var list) == false) {
				list = new List<SimpleViewOptions<TContext, TRequest, TResponse>>();
				@this.SetFeatureOptions(list);
			}

			var featureOptions = new SimpleFunctionalViewOptions<TContext, TRequest, TResponse>(@this);
			builder(featureOptions);
			list.Add(featureOptions);
			return @this;
		}
		public static RoutableOptions<TContext, TRequest, TResponse> UseEmbeddedViews<TContext, TRequest, TResponse>(this RoutableOptions<TContext, TRequest, TResponse> @this, Action<SimpleEmbeddedViewOptions<TContext, TRequest, TResponse>> builder)
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
		{
			if(@this.TryGetFeatureOptions<List<SimpleViewOptions<TContext, TRequest, TResponse>>>(out var list) == false) {
				list = new List<SimpleViewOptions<TContext, TRequest, TResponse>>();
				@this.SetFeatureOptions(list);
			}

			var featureOptions = new SimpleEmbeddedViewOptions<TContext, TRequest, TResponse>(@this);
			builder(featureOptions);
			list.Add(featureOptions);
			return @this;
		}

		/// <summary>
		/// Write a view to the user agent upon request finalization.
		/// </summary>
		/// <param name="name">The name of the view to write</param>
		/// <param name="model">A model to pass to the view upon rendering</param>
		/// <returns>A task to be completed after the view has been resolved. The completion of this task should not be confused to indicate request finalization.</returns>
		public async static Task WriteViewAsync<TContext, TRequest, TResponse>(this RoutableResponse<TContext, TRequest, TResponse> @this, string name, object model = null)
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
		{
			var view = await Template<TContext, TRequest, TResponse>.Find(@this.Context.Options, name);
			@this.Write(async (context, stream) => {
				context.Response.Abstract.ContentType = view.MimeType;
				using(var writer = new StreamWriter(stream, context.Options.StringEncoding)) {
					await view.TryRender(name, writer, model);
				}
			});
		}
	}
}
