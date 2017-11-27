using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Routable.Kestrel
{
	public class KestrelResponseAttributes : AbstractResponseAttributes
	{
		private KestrelRoutableResponse Response;

		public override int StatusCode { get => Response.Status; set => Response.Status = value; }
		public override long ContentLength { get => Response.ContentLength ?? 0; set => Response.ContentLength = value; }
		public override string ContentType { get => Response.ContentType; set => Response.ContentType = value; }

		public override void SetCookie(string name, string value, DateTime? expiration, bool httpOnly, bool isSecure, string domain, string path)
		{
			Response.Cookies.Append(name, value, new Microsoft.AspNetCore.Http.CookieOptions {
				Expires = expiration == null ? (DateTimeOffset?)null : new DateTimeOffset(expiration.Value),
				HttpOnly = httpOnly,
				Secure = isSecure,
				Domain = domain,
				Path = path
			});
		}
		public override void SetHeader(string name, string value)
		{
			if(Response.Headers.ContainsKey(name)) {
				Response.Headers.Remove(name);
			}
			Response.Headers.Add(name, new Microsoft.Extensions.Primitives.StringValues(value));
		}

		internal KestrelResponseAttributes(KestrelRoutableResponse response) => Response = response;
	}
	public class KestrelRoutableResponse : RoutableResponse<
		KestrelRoutableContext,
		KestrelRoutableRequest,
		KestrelRoutableResponse,
		int,
		Microsoft.AspNetCore.Http.IResponseCookies,
		Microsoft.AspNetCore.Http.IHeaderDictionary,
		string,
		long?,
		Stream>
	{
		public Microsoft.AspNetCore.Http.HttpResponse PlatformResponse => Context.PlatformContext.Response;
		private AbstractResponseAttributes _Attributes;
		public override AbstractResponseAttributes Attributes => _Attributes;
		public override int Status { get => PlatformResponse.StatusCode; set => PlatformResponse.StatusCode = value; }
		public override string Reason { get => throw new NotSupportedException(); set => new NotSupportedException(); }
		public override Microsoft.AspNetCore.Http.IResponseCookies Cookies { get => PlatformResponse.Cookies; set => throw new NotSupportedException(); }
		public override Microsoft.AspNetCore.Http.IHeaderDictionary Headers { get => PlatformResponse.Headers; set => throw new NotSupportedException(); }
		public override string ContentType { get => PlatformResponse.ContentType; set => PlatformResponse.ContentType = value; }
		public override long? ContentLength { get => PlatformResponse.ContentLength; set => PlatformResponse.ContentLength = value; }
		public override Stream Body { get => PlatformResponse.Body; set => throw new NotSupportedException(); }

		internal KestrelRoutableResponse(KestrelRoutableContext context) : base(context)
		{
			_Attributes = new KestrelResponseAttributes(this);
		}

		public override Task Redirect(string location)
		{
			PlatformResponse.Redirect(location);
			return Task.CompletedTask;
		}

		protected override async Task Finalize(IReadOnlyList<Func<RoutableContext<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse>, Stream, Task>> writers)
		{
			using(Body) {
				foreach(var writer in writers) {
					await writer(Context, Body);
				}
			}
		}
	}
}
