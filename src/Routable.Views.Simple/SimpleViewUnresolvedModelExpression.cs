using System;
using System.Collections.Generic;
using System.Text;

namespace Routable.Views.Simple
{
	public sealed class SimpleViewUnresolvedModelExpression : InvalidOperationException
	{
		public SimpleViewUnresolvedModelExpression(string expression) : base($"Unable to resolve model expression '{expression}'") { }
	}
}
