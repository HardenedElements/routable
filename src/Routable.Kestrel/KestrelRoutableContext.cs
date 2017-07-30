using Microsoft.AspNetCore.Http.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Routable.Kestrel
{
	public class KestrelRoutableContext : RoutableContext<
		Microsoft.AspNetCore.Http.HttpContext,
		KestrelRoutableContext,
		KestrelRoutableRequest,
		KestrelRoutableResponse,
		ClaimsPrincipal,
		IDictionary<object, object>>
	{
		private Microsoft.AspNetCore.Http.HttpContext _PlatformContext;
		public override Microsoft.AspNetCore.Http.HttpContext PlatformContext => _PlatformContext;

		public override string HostingPlatform => "kestrel";
		public override EndPoint LocalEndPoint => new IPEndPoint(PlatformContext.Connection.LocalIpAddress, PlatformContext.Connection.LocalPort);
		public override EndPoint RemoteEndPoint => new IPEndPoint(PlatformContext.Connection.RemoteIpAddress, PlatformContext.Connection.RemotePort);
		public override X509Certificate2 ClientCertificate =>
			PlatformContext.Connection.GetClientCertificateAsync().Result ??
			(PlatformContext.Features.Where(_ => _.Value is ITlsConnectionFeature).Select(_ => _.Value as ITlsConnectionFeature).FirstOrDefault())?.ClientCertificate;
		private KestrelRoutableRequest _Request;
		public override KestrelRoutableRequest Request => _Request;
		private KestrelRoutableResponse _Response;
		public override KestrelRoutableResponse Response => _Response;
		public override ClaimsPrincipal User { get => PlatformContext.User; set => PlatformContext.User = value; }
		public override IDictionary<object, object> PerRequestItems => PlatformContext.Items;

		internal KestrelRoutableContext(RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> options, Microsoft.AspNetCore.Http.HttpContext platformContext)
			: base(options)
		{
			_PlatformContext = platformContext;
			_Request = new KestrelRoutableRequest(this);
			_Response = new KestrelRoutableResponse(this);
		}
	}
}
