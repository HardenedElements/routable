using System;
using System.Collections.Generic;
using System.Text;

namespace Routable.Views.Simple
{
	public sealed class SimpleViewParserException : InvalidOperationException
	{
		public SimpleViewParserException(string name) : base($"Failed to parse view '{name}'") { }
		public SimpleViewParserException(string name, Exception innerException) : base($"Failed to parse view '{name}'", innerException) { }
	}
}
