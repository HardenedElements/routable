using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Routable.Views.Simple
{
	public sealed class SimpleFileSystemViewOptions<TContext, TRequest, TResponse> : SimpleViewOptions<TContext, TRequest, TResponse>
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		internal IList<string> SearchPaths { get; set; } = new List<string>();
		internal IList<string> ViewExtensions { get; set; } = new List<string>();
		public string DefaultMimeType { get; set; } = "text/html";

		internal SimpleFileSystemViewOptions(RoutableOptions<TContext, TRequest, TResponse> options) : base(options)
		{
			AddSearchPath(Path.Combine(Directory.GetCurrentDirectory(), "views"));
			AddViewExtension(".html");
		}

		/// <summary>
		/// Add a directory to the list of paths searched when attempting to locate a view.
		/// </summary>
		public SimpleFileSystemViewOptions<TContext, TRequest, TResponse> AddSearchPath(string path)
		{
			SearchPaths.Add(Path.GetFullPath(path));
			return this;
		}
		/// <summary>
		/// Remove all search paths used to locate requested views. This is typically followed by a request to add a new search path.
		/// </summary>
		public SimpleFileSystemViewOptions<TContext, TRequest, TResponse> ClearSearchPaths()
		{
			SearchPaths.Clear();
			return this;
		}
		/// <summary>
		/// Add a file extension to the list of file extensions used when attempting to locate a view.
		/// </summary>
		public SimpleFileSystemViewOptions<TContext, TRequest, TResponse> AddViewExtension(string extension)
		{
			ViewExtensions.Add(extension);
			return this;
		}
		/// <summary>
		/// Add a callback to the list of event handlers used to try and resolve model values when traditional methods fail.
		/// </summary>
		public SimpleFileSystemViewOptions<TContext, TRequest, TResponse> OnUnresolvedModelValue(Func<string, IEnumerable<string>, object, object> action)
		{
			ResolveUnresolvedModelKey += (_, args) => {
				args.Value = action(args.Expression, args.PathComponents, args.Model);
				args.Success = args.Value != null;
			};
			return this;
		}

		private string CombineAndRestrictPath(string prefix, string suffix)
		{
			var path = Path.GetFullPath(Path.Combine(prefix, suffix));
			return path.Trim().ToLower().StartsWith(prefix.Trim().ToLower()) ? path : null;
		}
		internal override Task ResolveView(ResolveViewArgs resolveViewArgs)
		{
			if(resolveViewArgs.Success == true) {
				return Task.CompletedTask;
			}

			var path = new[] { resolveViewArgs.Name }
			.SelectMany(pattern => ViewExtensions.Select(ext => $"{pattern}{ext}"))
			.SelectMany(pattern => SearchPaths.Select(searchPath => CombineAndRestrictPath(searchPath, pattern)))
			.Where(_ => File.Exists(_))
			.FirstOrDefault();

			if(path == null) {
				return Task.CompletedTask;
			}

			if(RoutableOptions.TryGetMimeType(Path.GetExtension(path), out var mimeType) == false) {
				mimeType = DefaultMimeType;
			}
			resolveViewArgs.MimeType = mimeType;
			resolveViewArgs.LastModified = File.GetLastWriteTimeUtc(path);
			resolveViewArgs.GetStream = () => Task.FromResult<Stream>(File.OpenRead(path));
			resolveViewArgs.Success = true;

			return Task.CompletedTask;
		}
	}
}
