using System;
using System.Collections.Generic;
using System.Text;

namespace Routable
{
	public class UnhandledResponseTypeException : InvalidOperationException
	{
		public UnhandledResponseTypeException(Type type) : base($"{type.FullName} is unhandled") { }
	}
}
