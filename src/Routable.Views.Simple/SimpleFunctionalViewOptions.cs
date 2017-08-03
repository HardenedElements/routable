using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Routable.Views.Simple
{
	public sealed class SimpleFunctionalViewOptions<TContext, TRequest, TResponse> : SimpleViewOptions<TContext, TRequest, TResponse>
			where TContext : RoutableContext<TContext, TRequest, TResponse>
			where TRequest : RoutableRequest<TContext, TRequest, TResponse>
			where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		private event EventHandler<ResolveViewArgs> ViewResolvers;
		private IList<Func<ResolveViewArgs, Task>> AsyncViewResolvers = new List<Func<ResolveViewArgs, Task>>();
		public string DefaultMimeType { get; set; } = "text/html";

		internal SimpleFunctionalViewOptions(RoutableOptions<TContext, TRequest, TResponse> options) : base(options) { }

		public SimpleFunctionalViewOptions<TContext, TRequest, TResponse> File(Func<string, string> resolver)
		{
			ViewResolvers += (_, args) => {
				if(args.Success == true) {
					return;
				}

				var path = resolver(args.Name);
				args.MimeType = RoutableOptions.TryGetMimeType(Path.GetExtension(path), out var mimeType) ? mimeType : DefaultMimeType;
				args.GetStream = () => Task.FromResult<Stream>(System.IO.File.OpenRead(path));
				args.Success = true;
			};
			return this;
		}
		public SimpleFunctionalViewOptions<TContext, TRequest, TResponse> String(Func<string, string> resolver)
		{
			ViewResolvers += (_, args) => {
				if(args.Success == true) {
					return;
				}

				args.MimeType = DefaultMimeType;
				args.GetStream = () => Task.FromResult<Stream>(new MemoryStream(RoutableOptions.StringEncoding.GetBytes(resolver(args.Name))));
				args.Success = true;
			};
			return this;
		}
		public SimpleFunctionalViewOptions<TContext, TRequest, TResponse> Bytes(Func<string, byte[]> resolver)
		{
			ViewResolvers += (_, args) => {
				if(args.Success == true) {
					return;
				}

				args.MimeType = DefaultMimeType;
				args.GetStream = () => Task.FromResult<Stream>(new MemoryStream(resolver(args.Name)));
				args.Success = true;
			};
			return this;
		}
		public SimpleFunctionalViewOptions<TContext, TRequest, TResponse> Stream(Func<string, Stream> resolver)
		{
			ViewResolvers += (_, args) => {
				if(args.Success == true) {
					return;
				}

				args.MimeType = DefaultMimeType;
				args.GetStream = () => Task.FromResult(resolver(args.Name));
				args.Success = true;
			};
			return this;
		}
		public SimpleFunctionalViewOptions<TContext, TRequest, TResponse> Resolve(Action<ResolveViewArgs> resolver)
		{
			ViewResolvers += (_, args) => {
				if(args.Success == true) {
					return;
				}

				resolver(args);
			};
			return this;
		}

		public SimpleFunctionalViewOptions<TContext, TRequest, TResponse> FileAsync(Func<string, Task<string>> resolver)
		{
			AsyncViewResolvers.Add(async args => {
				if(args.Success == true) {
					return;
				}

				var path = await resolver(args.Name);
				args.MimeType = RoutableOptions.TryGetMimeType(Path.GetExtension(path), out var mimeType) ? mimeType : DefaultMimeType;
				args.GetStream = () => Task.FromResult<Stream>(System.IO.File.OpenRead(path));
				args.Success = true;
			});
			return this;
		}
		public SimpleFunctionalViewOptions<TContext, TRequest, TResponse> StringAsync(Func<string, Task<string>> resolver)
		{
			AsyncViewResolvers.Add(args => {
				if(args.Success == true) {
					return Task.CompletedTask;
				}

				args.MimeType = DefaultMimeType;
				args.GetStream = async () => new MemoryStream(RoutableOptions.StringEncoding.GetBytes(await resolver(args.Name)));
				args.Success = true;
				return Task.CompletedTask;
			});
			return this;
		}
		public SimpleFunctionalViewOptions<TContext, TRequest, TResponse> BytesAsync(Func<string, Task<byte[]>> resolver)
		{
			AsyncViewResolvers.Add(args => {
				if(args.Success == true) {
					return Task.CompletedTask;
				}

				args.MimeType = DefaultMimeType;
				args.GetStream = async () => new MemoryStream(await resolver(args.Name));
				args.Success = true;
				return Task.CompletedTask;
			});
			return this;
		}
		public SimpleFunctionalViewOptions<TContext, TRequest, TResponse> StreamAsync(Func<string, Task<Stream>> resolver)
		{
			AsyncViewResolvers.Add(args => {
				if(args.Success == true) {
					return Task.CompletedTask;
				}

				args.MimeType = DefaultMimeType;
				args.GetStream = async () => await resolver(args.Name);
				args.Success = true;
				return Task.CompletedTask;
			});
			return this;
		}
		public SimpleFunctionalViewOptions<TContext, TRequest, TResponse> ResolveAsync(Func<ResolveViewArgs, Task> resolver)
		{
			AsyncViewResolvers.Add(async args => {
				if(args.Success == true) {
					return;
				}

				await resolver(args);
			});
			return this;
		}

		internal async override Task ResolveView(ResolveViewArgs resolveViewArgs)
		{
			foreach(var resolver in AsyncViewResolvers) {
				await resolver(resolveViewArgs);
				if(resolveViewArgs.Success == true) {
					return;
				}
			}

			ViewResolvers?.Invoke(this, resolveViewArgs);
		}
	}
}
