using System;
using System.Collections.Generic;
using System.Text;

namespace Routable
{
	public enum LogClass
	{
		Debug,
		Informational,
		Warning,
		Error,
		Security
	}
	public interface IRoutableLogger
	{
		void Write(LogClass logClass, string message, Exception exception = null, IReadOnlyDictionary<string, string> data = null);
	}
}
