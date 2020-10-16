using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit.Abstractions;

namespace NetworkControllerTests.Helper
{
    public class LogToOutput : ILoggerProvider
    {
        public void Dispose() { }
        ITestOutputHelper _output;
        string _additionalName;
        public LogToOutput(ITestOutputHelper output, string additionalName)
        {
            _output = output;
            _additionalName = additionalName;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new CustomConsoleLogger(categoryName, _additionalName, _output);
        }

        public class CustomConsoleLogger : ILogger
        {
            private readonly string _categoryName;
            ITestOutputHelper _output;
            /// <summary>
            /// value for identifying different loggers
            /// </summary>
            private string _additionalName { get; set; }

            public CustomConsoleLogger(string categoryName, string additionalName, ITestOutputHelper output)
            {
                _categoryName = categoryName;
                _output = output;
                _additionalName = additionalName;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                _output.WriteLine($"{_additionalName}:{logLevel}: {formatter(state, exception)}");
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }
        }
    }
}
