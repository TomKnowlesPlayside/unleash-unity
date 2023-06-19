using System;

namespace Unleash.Logging
{
	public class DefaultLogger : ILogger
	{
		public void Log(string message, LogVerbocity verbocity = LogVerbocity.Debug)
		{
			Console.WriteLine(message);
		}

		public void LogException(string message, Exception exception)
		{
			throw exception;
		}
	}
}