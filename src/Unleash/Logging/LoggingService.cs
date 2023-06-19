namespace Unleash.Logging
{
	public static class LoggingService
	{
		private static ILogger instance;

		public static void SetLogger(ILogger logger)
		{
			
		}
		
		public static ILogger GetLogger()
		{
			if (instance == null)
			{
				instance = new DefaultLogger();
			}

			return instance;
		}
	}
}