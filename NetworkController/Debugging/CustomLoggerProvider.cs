using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkController.Debugging
{
    public class CustomLoggerProvider : ILoggerProvider
    {
        private LogLevel _minLevelToDisplay;

        public CustomLoggerProvider(LogLevel minLevelToDisplay = LogLevel.Trace)
        {
            _minLevelToDisplay = minLevelToDisplay;
        }

        public void Dispose() { }

        public ILogger CreateLogger(string categoryName)
        {
            return new CustomConsoleLogger(categoryName, _minLevelToDisplay);
        }

        public class CustomConsoleLogger : ILogger
        {
            private readonly string _categoryName;
            private readonly LogLevel _minLevelToDisplay;

            public CustomConsoleLogger(string categoryName, LogLevel minLevelToDisplay=LogLevel.Trace)
            {
                _categoryName = categoryName;
                _minLevelToDisplay = minLevelToDisplay;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                Console.WriteLine($"{logLevel}: {_categoryName}[{eventId.Id}]: {formatter(state, exception)}");
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel >= _minLevelToDisplay;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }
        }
    }
}
