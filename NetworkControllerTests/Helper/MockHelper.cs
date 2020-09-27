using Microsoft.Extensions.Logging;
using Moq;
using Moq.Language.Flow;
using NetworkController.Models;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace NetworkControllerTests.Helper
{
    public static class MockHelper
    {
        public static ISetup<ILogger<T>> MockLog<T>(this Mock<ILogger<T>> logger, LogLevel level)
        {
            return logger.Setup(x => x.Log(
                It.Is<LogLevel>(l => l == level),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)));
        }

        private static Expression<Action<ILogger<T>>> Verify<T>(LogLevel level, int eventId)
        {
            return x => x.Log(
                It.Is<LogLevel>(l => l == level),
                It.Is<EventId>(ei => ei == eventId),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true));
        }

        public static void Verify<T>(this Mock<ILogger<T>> mock, LogLevel level, LoggerEventIds eventId, Times times)
        {
            mock.Verify(Verify<T>(level, (int)eventId), times);
        }
    }
}
