using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Routable.Kestrel
{
	public class KestrelRequestAbstractAttributes : AbstractRequestAttributes
	{
		private KestrelRoutableRequest Request;

		public override long ContentLength => Request.ContentLength ?? 0;
		public override string ContentType => Request.PlatformRequest.ContentType;
		public override string Method => Request.Method;
		public override bool TryGetCookie(string name, out string value) => Request.Cookies.TryGetValue(name, out value);
		public override bool TryGetHeader(string name, out IEnumerable<string> value)
		{
			if(Request.Headers.TryGetValue(name, out var stringValues) == true) {
				value = stringValues;
				return true;
			} else {
				value = null;
				return false;
			}
		}
		public override bool TryGetForm(string name, out IEnumerable<string> value)
		{
			if(Request.PlatformRequest.HasFormContentType == false) {
				value = null;
				return false;
			}

			try {
				if(Request.Form.TryGetValue(name, out var values) == true) {
					value = values;
					return true;
				}
			} catch(InvalidOperationException) {
			} catch(InvalidDataException) { }

			value = null;
			return false;
		}
		public override bool TryGetQuery(string name, out IEnumerable<string> value)
		{
			if(Request.Query.TryGetValue(name, out var values) == true) {
				value = values;
				return true;
			}

			value = null;
			return false;
		}

		public async override Task<string> GetBodyAsString(Encoding encoding = null)
		{
			using(var reader = new StreamReader(Request.Body, encoding ?? Encoding.UTF8, encoding == null ? true : false, 4096, true)) {
				return await reader.ReadToEndAsync();
			}
		}

		internal KestrelRequestAbstractAttributes(KestrelRoutableRequest request) => Request = request;
	}
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
		private AbstractRequestAttributes _Abstract;
		public override AbstractRequestAttributes Abstract => _Abstract;
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

		internal KestrelRoutableRequest(KestrelRoutableContext context) : base(context) => _Abstract = new KestrelRequestAbstractAttributes(this);
	}
}
