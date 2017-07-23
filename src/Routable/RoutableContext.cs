using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Routable
{
	public abstract class RoutableContext<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		/// <summary>
		/// Routable options
		/// </summary>
		public RoutableOptions<TContext, TRequest, TResponse> Options { get; protected set; }
		/// <summary>
		/// A string indicating the hosting platform that generated this request context.
		/// </summary>
		public abstract string HostingPlatform { get; }
		/// <summary>
		/// The client certificate used to authenticate this request.
		/// </summary>
		public abstract X509Certificate2 ClientCertificate { get; }
		/// <summary>
		/// The local endpoint of this request.
		/// </summary>
		public virtual EndPoint LocalEndPoint => throw new NotSupportedException();
		/// <summary>
		/// The remote endpoint of the party that generated this request.
		/// </summary>
		public virtual EndPoint RemoteEndPoint => throw new NotSupportedException();
		/// <summary>
		/// Routable abstraction of this request.
		/// </summary>
		public abstract TRequest Request { get; }
		/// <summary>
		/// Routable abstraction for the response.
		/// </summary>
		public abstract TResponse Response { get; }
	}
	public abstract class RoutableContext<TPlatformContext, TContext, TRequest, TResponse, TUser, TPerRequestItems> : RoutableContext<TContext, TRequest, TResponse>
		where TContext : RoutableContext<TContext, TRequest, TResponse>
		where TRequest : RoutableRequest<TContext, TRequest, TResponse>
		where TResponse : RoutableResponse<TContext, TRequest, TResponse>
	{
		/// <summary>
		/// The underlying platform context.
		/// </summary>
		public virtual TPlatformContext PlatformContext => throw new NotSupportedException();
		/// <summary>
		/// Platform specific user representation for this request.
		/// </summary>
		public virtual TUser User { get; set; }
		/// <summary>
		/// Temporary variables specific to this request.
		/// </summary>
		public abstract TPerRequestItems PerRequestItems { get; }

		protected RoutableContext(RoutableOptions<TContext, TRequest, TResponse> options) => Options = options;
	}
}
