using System;
using System.Collections.Generic;
using System.Linq;
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
	public interface ILogger
	{
		void Write(LogClass logClass, string message, Exception exception = null, IReadOnlyDictionary<string, string> data = null);
	}
	internal class DefaultConsoleLogger : ILogger
	{
		public void Write(LogClass logClass, string message, Exception exception, IReadOnlyDictionary<string, string> data)
		{
			lock(this) {
				Console.Error.WriteLine($"[{DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")}/{logClass}]: {message}");
				var currentException = exception;
				int exceptionIndex = 1;
				while(currentException != null) {
					Console.Error.WriteLine($"  [{exceptionIndex++}] {currentException.GetType()?.FullName ?? "<unknown exception type>"}: {currentException.Message ?? "<no message>"}");
					if(currentException.StackTrace != null) {
						Console.Error.WriteLine($"   Stacktrace:");
						Console.Error.WriteLine($"    {currentException.StackTrace.Replace("\n", "\n    ")}");
					}
					currentException = currentException.InnerException;
				}
				if(data != null && data.Any()) {
					Console.Error.WriteLine("  Additional data:");
					foreach(var pair in data) {
						Console.Error.WriteLine($"    {pair.Key ?? "<no key name>"}: {pair.Value ?? "<no value name>"}");
					}
				}
			}
		}
	}
}
