using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Routable.Views.Simple
{
	public sealed class SimpleViewOptions<TContext, TRequest, TResponse>
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		public RoutableOptions<TContext, TRequest, TResponse> RoutableOptions { get; private set; }
		internal UnresolvedModelValueAction UnresolvedModelValueError { get; set; }
		/// <summary>
		/// List of paths to search for views.
		/// </summary>
		internal IList<string> SearchPaths { get; set; } = new List<string>();
		internal IList<string> ViewExtensions { get; set; } = new List<string>();
		
		public SimpleViewOptions(RoutableOptions<TContext, TRequest, TResponse> options)
		{
			RoutableOptions = options;
			AddViewExtension(".html");
		}


		public SimpleViewOptions<TContext, TRequest, TResponse> AddSearchPath(string path)
		{
			SearchPaths.Add(Path.GetFullPath(path));
			return this;
		}
		public SimpleViewOptions<TContext, TRequest, TResponse> AddViewExtension(string extension)
		{
			ViewExtensions.Add(extension);
			return this;
		}
		public SimpleViewOptions<TContext, TRequest, TResponse> OnUnresolvedModelValue(UnresolvedModelValueAction action)
		{
			UnresolvedModelValueError = action;
			return this;
		}
	}
}
