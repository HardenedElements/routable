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
		public RoutableContext<TContext, TRequest, TResponse> Context { get; private set; }
		/// <summary>
		/// Platform agnostic response attributes
		/// </summary>
		public abstract AbstractResponseAttributes Attributes { get; }

		private RoutableResponse() { }
		internal RoutableResponse(RoutableContext<TContext, TRequest, TResponse> context) => Context = context;

		/// <summary>
		/// Write directly to the response stream.
		/// </summary>
		/// <param name="writer">A function that when evaluated writes the response to the stream provided.</param>
		/// <returns>Task to be completed once the response has been written.</returns>
		public abstract Task WriteAsync(Func<Stream, Task> writer);
		/// <summary>
		/// Write a value as a response.
		/// </summary>
		/// <returns>Task to be completed once the response has been written.</returns>
		public virtual Task WriteAsync<T>(T value)
		{
			if(value == null) {
				return Context.Options.EmptyResponseHandler(Context, null);
			} else {
				// TODO: add means of calculating type distance and caching the results.
				if(Context.Options.ResponseTypeHandlers.TryGetValue(typeof(T), out var handler) == false && Context.Options.ResponseTypeHandlers.TryGetValue(typeof(string), out handler) == false) {
					// TODO: add logging.
					return Context.Options.EmptyResponseHandler(Context, null);
				}

				return handler(Context, value);
			}
		}
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

		protected RoutableResponse(RoutableContext<TContext, TRequest, TResponse> context) : base(context) { }
	}
}
