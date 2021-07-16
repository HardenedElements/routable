using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Routable.Kestrel
{
	internal sealed class MicrosoftLoggingLogger : IRoutableLogger
	{
		private Microsoft.Extensions.Logging.ILogger Logger;

		public MicrosoftLoggingLogger(Microsoft.Extensions.Logging.ILogger logger) => Logger = logger;

		public void Write(LogClass logClass, string message, Exception exception, IReadOnlyDictionary<string, string> data)
		{
			var level = logClass switch {
				LogClass.Debug => LogLevel.Debug,
				LogClass.Informational => LogLevel.Information,
				LogClass.Warning => LogLevel.Warning,
				LogClass.Error => LogLevel.Error,
				LogClass.Security => LogLevel.Error,
				_ => LogLevel.Information
			};

			if(exception == null) {
				Logger.Log(level, message);
			} else {
				Logger.Log(level, exception, message);
			}
		}
	}
}
