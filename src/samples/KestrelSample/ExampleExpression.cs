using Routable.Kestrel;
using Routable.Views.Simple;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Sprache;
using Routable;

namespace KestrelSample
{
	public sealed class ExampleExpression : CustomExpression<KestrelRoutableContext, KestrelRoutableRequest, KestrelRoutableResponse>
	{
		public static Parser<ExampleExpression> Parser =>
			from condOpen in Parse.Char('@').Then(_ => Parse.String("Hex("))
			from body in Parse.LetterOrDigit.Or(Parse.Char(' ')).Many().Text()
			from condClose in Parse.Char(')')
			select new ExampleExpression(body);

		private string Body;

		public ExampleExpression(string body) => Body = body;

		public override async Task<bool> TryRender(StreamWriter writer, object model)
		{
			await writer.WriteAsync($"[Hex: {string.Join(":", Body.Select(_ => ((byte)_).ToString("X").PadLeft(2, '0')))}]");
			return true;
		}
	}
}
