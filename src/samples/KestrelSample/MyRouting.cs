using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Routable;
using Routable.Kestrel;
using Routable.Views.Simple;
using System;

namespace RoutableTest
{
	public sealed class MyRouting : KestrelRouting
	{
		public MyRouting(RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> options) : base(options)
		{
			// write a view using Routable.Views.Simple.
			Add(_ => _.Get("/").Do(async (ctx, req, resp) => await resp.WriteViewAsync("index", new {
				SomeModelField = new {
					Nested = "Widget widget"
				}
			})));

			Add(_ => _.Get("/test").Do(async (ctx, req, resp) => await resp.WriteAsync("Hello World!")));
			Add(_ => _.Post("/test").Try(OnTestPost));

			Add(_ => _.Get("/json").Do(async (ctx, req, resp) => await resp.WriteAsync(JObject.FromObject(new {
				Field1 = 1,
				Field2 = "string?"
			}))));
		}

		private async Task<bool> OnTestPost(KestrelRoutableContext ctx, KestrelRoutableRequest req, KestrelRoutableResponse resp)
		{
			if(req.Form.TryGetValue("my-parameter", out var value) == true) {
				await resp.WriteAsync($"Value: {value.FirstOrDefault() ?? "<null>"}");
				return true;
			} else {
				return false;
			}
		}
	}
}
