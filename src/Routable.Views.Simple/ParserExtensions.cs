using Superpower;

namespace Routable.Views.Simple
{
	internal static class ParserExtensions
	{
		// Materializes a sequence of parsed characters into a string.
		public static TextParser<string> Text(this TextParser<char[]> parser) => parser.Select(chars => new string(chars));
	}
}
