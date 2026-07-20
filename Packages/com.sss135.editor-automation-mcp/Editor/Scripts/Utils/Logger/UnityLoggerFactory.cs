#nullable enable
using Microsoft.Extensions.Logging;

namespace com.SSS135.EditorAutomation.Utils
{
    public static class UnityLoggerFactory
    {
        private static ILoggerFactory? _loggerFactory;

        public static ILoggerFactory LoggerFactory
        {
            get
            {
                if (_loggerFactory == null)
                {
                    _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
                    {
                        builder.ClearProviders();
                        builder.AddProvider(new UnityLoggerProvider());
                        builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                    });
                }
                return _loggerFactory;
            }
        }
    }
}
