using Routable.Views.Simple.AST;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Routable.Views.Simple
{
	public class Template<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		private RoutableOptions<TContext, TRequest, TResponse> Options;
		private SimpleViewOptions<TContext, TRequest, TResponse> ViewOptions;
		private IReadOnlyList<Node<TContext, TRequest, TResponse>> Nodes;
		public string MimeType { get; private set; }

		internal Template(RoutableOptions<TContext, TRequest, TResponse> options, SimpleViewOptions<TContext, TRequest, TResponse> viewOptions, IEnumerable<Node<TContext, TRequest, TResponse>> nodes)
		{
			Options = options;
			ViewOptions = viewOptions;
			Nodes = nodes.ToList();
		}

		internal async Task<bool> TryRender(StreamWriter writer, RenderContext<TContext, TRequest, TResponse> context)
		{
			foreach(var node in Nodes) {
				if(await node.TryRender(writer, context) == false) {
					return false;
				}
			}

			return true;
		}
		public Task<bool> TryRender(string viewName, StreamWriter writer, object model)
		{
			var context = new RenderContext<TContext, TRequest, TResponse>(viewName, Options, ViewOptions);
			context.Push(model);
			return TryRender(writer, context);
		}

		public static async Task<Template<TContext, TRequest, TResponse>> Find(RoutableOptions<TContext, TRequest, TResponse> options, string viewName)
		{
			if(options.TryGetFeatureOptions<List<SimpleViewOptions<TContext, TRequest, TResponse>>>(out var viewOptionsCollection) == false) {
				// use default options.
				viewOptionsCollection = new List<SimpleViewOptions<TContext, TRequest, TResponse>>() {
					new SimpleFileSystemViewOptions<TContext, TRequest, TResponse>(options)
				};
			}

			foreach(var viewOptions in viewOptionsCollection) {
				// resolve view.
				var resolveViewArgs = new ResolveViewArgs { Name = viewName };
				await viewOptions.ResolveView(resolveViewArgs);
				if(resolveViewArgs.Success == false) {
					continue;
				}

				// parse view source and return a template.
				using(var stream = await resolveViewArgs.GetStream())
				using(var reader = new StreamReader(stream)) {
					try {
						var parser = new TemplateParser<TContext, TRequest, TResponse>(options, viewOptions);
						var template = parser.TryParse(viewName, resolveViewArgs.LastModified, await reader.ReadToEndAsync());
						template.MimeType = resolveViewArgs.MimeType;
						return template;
					} catch(Exception ex) {
						throw new SimpleViewParserException(viewName, ex);
					}
				}
			}

			throw new SimpleViewNotFoundException(viewName);
		}
	}
}
