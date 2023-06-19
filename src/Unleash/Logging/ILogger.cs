using System;

namespace Unleash.Logging
{
	public interface ILogger
	{
		void Log(string message, LogVerbocity verbocity = LogVerbocity.Debug);
		void LogException(string message, Exception exception);
	}
}