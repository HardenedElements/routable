using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Routable
{
	public abstract class AbstractResponseAttributes
	{
		/// <summary>
		/// Status code as an integer, regardless of platform.
		/// </summary>
		public abstract int StatusCode { get; set; }
		/// <summary>
		/// Content length as a long integer, regardless of platform.
		/// </summary>
		public abstract long ContentLength { get; set; }
		/// <summary>
		/// Content type as a string, regardless of platform.
		/// </summary>
		public abstract string ContentType { get; set; }

		/// <summary>
		/// Set a header, regardless of the platform.
		/// </summary>
		/// <param name="name">The name of the header</param>
		/// <param name="value">Value to set header to</param>
		public abstract void SetHeader(string name, string value);
		/// <summary>
		/// Set a response cookie, regardless of platform.
		/// </summary>
		public abstract void SetCookie(string name, string value, DateTime? expiration, bool httpOnly, bool isSecure, string domain, string path);
	}
	public abstract class RoutableResponse<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		/// <summary>
		/// Context of this request.
		/// </summary>
		public TContext Context { get; private set; }
		/// <summary>
		/// Platform agnostic response attributes
		/// </summary>
		public abstract AbstractResponseAttributes Attributes { get; }
		private List<Func<RoutableContext<TContext, TRequest, TResponse>, Stream, Task>> ContentWriters = new List<Func<RoutableContext<TContext, TRequest, TResponse>, Stream, Task>>();

		private RoutableResponse() { }
		internal RoutableResponse(TContext context) => Context = context;

		/// <summary>
		/// Write directly to the response stream after the request has been finalized.
		/// </summary>
		/// <param name="writer">A function that when evaluated writes the response to the stream provided.</param>
		public virtual void Write(Func<RoutableContext<TContext, TRequest, TResponse>, Stream, Task> writer) => ContentWriters.Add(writer);
		/// <summary>
		/// Write a value as a response after the request has been finalized.
		/// </summary>
		public virtual void Write<T>(T value)
		{
			if(value == null) {
				Context.Options.EmptyResponseHandler(Context, null);
			} else {
				if(Context.Options.ResponseTypeHandlers.TryGetHandler(typeof(T), out var handler) == false) {
					if(Context.Options.DefaultResponseHandler != null) {
						handler = Context.Options.DefaultResponseHandler;
					} else if(Context.Options.EmptyResponseHandler != null) {
						handler = Context.Options.EmptyResponseHandler;
					} else {
						throw new UnhandledResponseTypeException(typeof(T));
					}
				}

				handler(Context, value);
			}
		}
		/// <summary>
		/// Clear pending writes. This should not be used outside of exceptional circumstances as writers may have IDisposable calls within them.
		/// </summary>
		public virtual void ClearPendingWrites() => ContentWriters.Clear();

		public abstract Task Redirect(string location);

		/// <summary>
		/// The implementer is expected to invoke the provided writers against a stream that will transmit content to the
		/// calling user agent.
		/// </summary>
		/// <param name="writers">Functions that wish to write content to the user agent</param>
		/// <returns>A task that completes when finalization is complete and the response has ended.</returns>
		protected abstract Task Finalize(IReadOnlyList<Func<RoutableContext<TContext, TRequest, TResponse>, Stream, Task>> writers);
		internal async Task Finalize() => await Finalize(ContentWriters);
	}
	public abstract class RoutableResponse<TContext, TRequest, TResponse, TStatus, TCookies, THeaders, TContentType, TContentLength, TBody> : RoutableResponse<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		/// <summary>
		/// Response status
		/// </summary>
		public abstract TStatus Status { get; set; }
		/// <summary>
		/// HTTP response reason
		/// </summary>
		public virtual string Reason { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
		/// <summary>
		/// Response cookies.
		/// </summary>
		public abstract TCookies Cookies { get; set; }
		/// <summary>
		/// Response headers.
		/// </summary>
		public abstract THeaders Headers { get; set; }
		/// <summary>
		/// Response content type.
		/// </summary>
		public abstract TContentType ContentType { get; set; }
		/// <summary>
		/// Response content length.
		/// </summary>
		public abstract TContentLength ContentLength { get; set; }
		/// <summary>
		/// Response body.
		/// </summary>
		public abstract TBody Body { get; set; }

		protected RoutableResponse(TContext context) : base(context) { }
	}
}
