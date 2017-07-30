using System;
using System.Collections.Generic;
using System.Text;

namespace Routable.Views.Simple
{
	public sealed class SimpleViewNotFoundException : InvalidOperationException
	{
		public SimpleViewNotFoundException(string name) : base($"Unable to locate view '{name}'") { }
	}
}
