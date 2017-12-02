using System;
using System.Collections.Generic;
using System.Text;

namespace Routable.Views.Simple
{
	public sealed class SimpleViewParserException : InvalidOperationException
	{
		public SimpleViewParserException(string name) : base($"Failed to parse view '{name}'") { }
		public SimpleViewParserException(string name, Exception innerException) : base($"Failed to parse view '{name}'", innerException) { }
		public SimpleViewParserException(string name, string message) : base($"Failed to parse view '{name}', {message}") { }
		public SimpleViewParserException(string name, string message, Exception innerException) : base($"Failed to parse view '{name}', {message}", innerException) { }
	}
}
