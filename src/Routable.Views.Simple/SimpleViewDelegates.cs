using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Routable.Views.Simple
{
	public delegate string UnresolvedModelValueAction(string mimeType, string modelExpression, IEnumerable<string> modelPaths, object model);
}
