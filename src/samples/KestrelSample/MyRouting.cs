using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Routable;
using Routable.Kestrel;
using Routable.Views.Simple;
using System;

namespace KestrelSample
{
	public sealed class MyRouting : KestrelRouting
	{
		public MyRouting(RoutableOptions<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse> options) : base(options)
		{
			// test people.
			var people = new[] { new { Name = "Mab" }, new { Name = "Mabel" }, new { Name = "Mabelle" }, new { Name = "Mable" }, new { Name = "Mada" }, new { Name = "Madalena" }, new { Name = "Madalyn" }, new { Name = "Maddalena" }, new { Name = "Maddi" }, new { Name = "Maddie" }, new { Name = "Maddy" }, new { Name = "Madel" }, new { Name = "Madelaine" }, new { Name = "Madeleine" }, new { Name = "Madelena" }, new { Name = "Madelene" }, new { Name = "Madelin" }, new { Name = "Madelina" }, new { Name = "Madeline" }, new { Name = "Madella" }, new { Name = "Madelle" }, new { Name = "Madelon" }, new { Name = "Madelyn" }, new { Name = "Madge" }, new { Name = "Madlen" }, new { Name = "Madlin" }, new { Name = "Madonna" }, new { Name = "Mady" }, new { Name = "Mae" }, new { Name = "Maegan" }, new { Name = "Mag" }, new { Name = "Magda" }, new { Name = "Magdaia" }, new { Name = "Magdalen" }, new { Name = "Magdalena" }, new { Name = "Magdalene" }, new { Name = "Maggee" }, new { Name = "Maggi" }, new { Name = "Maggie" }, new { Name = "Maggy" }, new { Name = "Mahala" }, new { Name = "Mahalia" }, new { Name = "Maia" }, new { Name = "Maible" }, new { Name = "Maiga" }, new { Name = "Maighdiln" }, new { Name = "Mair" }, new { Name = "Maire" }, new { Name = "Maisey" }, new { Name = "Maisie" }, new { Name = "Maitilde" }, new { Name = "Mala" }, new { Name = "Malanie" }, new { Name = "Malena" }, new { Name = "Malia" }, new { Name = "Malina" }, new { Name = "Malinda" }, new { Name = "Malinde" }, new { Name = "Malissa" }, new { Name = "Malissia" }, new { Name = "Mallissa" }, new { Name = "Mallorie" }, new { Name = "Mallory" }, new { Name = "Malorie" }, new { Name = "Malory" }, new { Name = "Malva" }, new { Name = "Malvina" }, new { Name = "Malynda" }, new { Name = "Mame" }, new { Name = "Mamie" }, new { Name = "Manda" }, new { Name = "Mandi" }, new { Name = "Mandie" }, new { Name = "Mandy" }, new { Name = "Manon" }, new { Name = "Manya" }, new { Name = "Mara" }, new { Name = "Marabel" }, new { Name = "Marcela" }, new { Name = "Marcelia" }, new { Name = "Marcella" } };

			// write a file system view.
			Add(_ => _.Get("/").DoAsync(async (ctx, req, resp) => await resp.WriteViewAsync("index", new {
				SomeModelField = new {
					Nested = "Widget widget"
				},
				People = people
			})));
			Add(_ => _.Get("/no-model").DoAsync(async (ctx, req, resp) => await resp.WriteViewAsync("index")));

			// test rendering parent-child views.
			Add(_ => _.Get("/parent").DoAsync(async (ctx, req, resp) => await resp.WriteViewAsync("parent")));
			Add(_ => _.Get("/child").DoAsync(async (ctx, req, resp) => await resp.WriteViewAsync("child")));
			Add(_ => _.Get("/loop-child").DoAsync(async (ctx, req, resp) => await resp.WriteViewAsync("loop-child")));

			// write an embedded view
			Add(_ => _.Get("/embedded").DoAsync(async (ctx, req, resp) => await resp.WriteViewAsync("test/embed", new {
				SomeModelField = new {
					Nested = "Widget widget"
				},
				People = people
			})));

			Add(_ => _.Get("/test").Do((ctx, req, resp) => resp.Write("Hello World!")));
			Add(_ => _.Post("/test").Try(OnTestPost));

			Add(_ => _.Get("/json").Do((ctx, req, resp) => resp.Write(JObject.FromObject(new {
				Field1 = 1,
				Field2 = "string?"
			}))));
		}

		private bool OnTestPost(KestrelRoutableContext ctx, KestrelRoutableRequest req, KestrelRoutableResponse resp)
		{
			if(req.Form.TryGetValue("my-parameter", out var value) == true) {
				resp.Write($"Value: {value.FirstOrDefault() ?? "<null>"}");
				return true;
			} else {
				return false;
			}
		}
	}
}
