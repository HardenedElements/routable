using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Routable
{
	public abstract class RoutableRequest<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		/// <summary>
		/// Context of this request.
		/// </summary>
		public TContext Context { get; private set; }
		/// <summary>
		/// Uri of this request.
		/// </summary>
		public abstract Uri Uri { get; }
		/// <summary>
		/// Host address of the requester.
		/// </summary>
		public virtual string UserHostAddress => throw new NotSupportedException();
		private IDictionary<string, object> _Parameters = new Dictionary<string, object>();
		/// <summary>
		/// Parameters extracted from the URL (eg. Named regex capture expressions within URL patterns)
		/// </summary>
		public virtual IReadOnlyDictionary<string, object> Parameters => (IReadOnlyDictionary<string, object>)_Parameters;

		protected RoutableRequest(TContext context) => Context = context;

		/// <summary>
		/// Get the body of the request as a string.
		/// </summary>
		/// <param name="encoding">Default is UTF-8</param>
		/// <returns>A string representation of the body.</returns>
		public virtual Task<string> GetBodyAsString(Encoding encoding = null) => throw new NotSupportedException();
		/// <summary>
		/// Get the request method (verb) as a string; regardless of platform.
		/// </summary>
		/// <returns></returns>
		public abstract string GetMethodAsString();

		internal void AddParameter(string name, object value) => _Parameters.Add(name, value);
	}
	public abstract class RoutableRequest<TContext, TRequest, TResponse, TMethod, TForm, TQuery, THeaders, ICookies, TContentLength, TBody> : RoutableRequest<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		/// <summary>
		/// HTTP request method (verb)
		/// </summary>
		public abstract TMethod Method { get; }
		/// <summary>
		/// Decoded form parameters provided in this request.
		/// </summary>
		public abstract TForm Form { get; }
		/// <summary>
		/// Decoded URL request parameters.
		/// </summary>
		public abstract TQuery Query { get; }
		/// <summary>
		/// HTTP headers sent with this request.
		/// </summary>
		public abstract THeaders Headers { get; }
		/// <summary>
		/// Cookies sent with this request.
		/// </summary>
		public abstract ICookies Cookies { get; }
		/// <summary>
		/// Length of the request body.
		/// </summary>
		public abstract TContentLength ContentLength { get; }
		/// <summary>
		/// The body sent with this request.
		/// </summary>
		public abstract TBody Body { get; }

		protected RoutableRequest(TContext context) : base(context) { }

		public override string GetMethodAsString() => Method.ToString();
	}
}
