using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Routable.Views.Simple
{
	public sealed class SimpleEmbeddedViewOptions<TContext, TRequest, TResponse> : SimpleViewOptions<TContext, TRequest, TResponse>
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		private class SearchPath
		{
			public Assembly Assembly;
			public string[] Prefixes;
		}
		private IList<SearchPath> SearchPaths = new List<SearchPath>();
		private IList<string> ViewExtensions = new List<string>();
		public string DefaultMimeType { get; set; } = "text/html";

		internal SimpleEmbeddedViewOptions(RoutableOptions<TContext, TRequest, TResponse> options) : base(options)
		{
			AddViewExtension(".html");
		}

		/// <summary>
		/// Add an assembly and list of prefixes to the list of paths searched when attempting to locate a view.
		/// </summary>
		public SimpleEmbeddedViewOptions<TContext, TRequest, TResponse> AddAssembly(Assembly assembly, params string[] prefixes)
		{
			SearchPaths.Add(new SearchPath {
				Assembly = assembly,
				Prefixes = prefixes?.Any() != true ? new string[] { "" } : prefixes
			});
			return this;
		}
		/// <summary>
		/// Remove all assembly search paths used to locate requested views. This is typically followed by a request to add a new search path.
		/// </summary>
		public SimpleEmbeddedViewOptions<TContext, TRequest, TResponse> ClearAssemblies()
		{
			SearchPaths.Clear();
			return this;
		}
		/// <summary>
		/// Add a file extension to the list of file extensions used when attempting to locate a view.
		/// </summary>
		public SimpleEmbeddedViewOptions<TContext, TRequest, TResponse> AddViewExtension(string extension)
		{
			ViewExtensions.Add(extension);
			return this;
		}
		/// <summary>
		/// Remove all view extensions.
		/// </summary>
		public SimpleEmbeddedViewOptions<TContext, TRequest, TResponse> ClearViewExtensions()
		{
			ViewExtensions.Clear();
			return this;
		}
		/// <summary>
		/// Add a callback to the list of event handlers used to try and resolve model values when traditional methods fail.
		/// </summary>
		public SimpleEmbeddedViewOptions<TContext, TRequest, TResponse> OnUnresolvedModelValue(Func<string, IEnumerable<string>, object, object> action)
		{
			ResolveUnresolvedModelKey += (_, args) => {
				args.Value = action(args.Expression, args.PathComponents, args.Model);
				args.Success = args.Value != null;
			};
			return this;
		}

		internal override Task ResolveView(ResolveViewArgs resolveViewArgs)
		{
			if(resolveViewArgs.Success == true) {
				return Task.CompletedTask;
			}

			// build list of path patterns.
			var name = resolveViewArgs.Name.Replace('/', '.').Replace('\\', '.');
			var paths = ViewExtensions.Select(ext => $"{name}{ext}");

			foreach(var search in SearchPaths.SelectMany(_ => _.Prefixes.Select(prefix => new { prefix = prefix, assembly = _.Assembly }))) {
				// search every path pattern.
				foreach(var path in paths) {
					var info = search.assembly.GetManifestResourceInfo($"{search.prefix.TrimEnd('.')}.{path}");
					if(info == null) {
						continue;
					}

					// resolve mime type.
					if(RoutableOptions.TryGetMimeType(Path.GetExtension(path), out var mimeType) == false) {
						mimeType = DefaultMimeType;
					}

					// construct response.
					resolveViewArgs.MimeType = mimeType;
					resolveViewArgs.LastModified = DateTime.MinValue; // embedded resources will not ever change.
					resolveViewArgs.GetStream = () => Task.FromResult(search.assembly.GetManifestResourceStream($"{search.prefix.TrimEnd('.')}.{path}"));
					resolveViewArgs.Success = true;
					return Task.CompletedTask;
				}
			}

			// no matches.
			return Task.CompletedTask;
		}
	}
}
