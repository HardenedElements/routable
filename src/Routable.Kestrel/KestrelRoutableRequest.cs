using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Routable.Kestrel
{
	public class KestrelRoutableRequest : RoutableRequest<
		KestrelRoutableContext,
		KestrelRoutableRequest,
		KestrelRoutableResponse,
		string,
		Microsoft.AspNetCore.Http.IFormCollection,
		Microsoft.AspNetCore.Http.IQueryCollection,
		Microsoft.AspNetCore.Http.IHeaderDictionary,
		Microsoft.AspNetCore.Http.IRequestCookieCollection,
		long?,
		Stream>
	{
		public Microsoft.AspNetCore.Http.HttpRequest PlatformRequest => Context.PlatformContext.Request;
		public override string Method => PlatformRequest.Method;
		private Uri _Uri;
		public override Uri Uri
		{
			get {
				if(_Uri == null) {
					var scheme = PlatformRequest.Scheme;
					var host = PlatformRequest.Host.HasValue ? PlatformRequest.Host.Host : "localhost";
					var port = (PlatformRequest.Host.HasValue ? PlatformRequest.Host.Port : null);
					if(port == null) {
						_Uri = new UriBuilder(scheme, host) {
							Path = PlatformRequest.Path.HasValue ? PlatformRequest.Path.Value : "/",
							Query = PlatformRequest.QueryString.HasValue ? PlatformRequest.QueryString.Value : null
						}.Uri;
					} else {
						_Uri = new UriBuilder(scheme, host, port.Value) {
							Path = PlatformRequest.Path.HasValue ? PlatformRequest.Path.Value : "/",
							Query = PlatformRequest.QueryString.HasValue ? PlatformRequest.QueryString.Value : null
						}.Uri;
					}
				}

				return _Uri;
			}
		}
		public override Microsoft.AspNetCore.Http.IFormCollection Form => PlatformRequest.Form;
		public override Microsoft.AspNetCore.Http.IQueryCollection Query => PlatformRequest.Query;
		public override Microsoft.AspNetCore.Http.IHeaderDictionary Headers => PlatformRequest.Headers;
		public override Microsoft.AspNetCore.Http.IRequestCookieCollection Cookies => PlatformRequest.Cookies;
		public override string UserHostAddress
		{
			get {
				switch(Context.RemoteEndPoint) {
					case System.Net.IPEndPoint ep:
						return ep.Address.ToString();
					default:
						return Context.RemoteEndPoint.ToString();
				}
			}
		}
		public override long? ContentLength => PlatformRequest.ContentLength;
		public override Stream Body => PlatformRequest.Body;

		internal KestrelRoutableRequest(KestrelRoutableContext context) : base(context) { }

		public async override Task<string> GetBodyAsString(Encoding encoding = null)
		{
			using(var reader = new StreamReader(Body, encoding ?? Encoding.UTF8, encoding == null ? true : false, 4096, true)) {
				return await reader.ReadToEndAsync();
			}
		}
	}
}
