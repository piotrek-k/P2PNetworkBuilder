using Microsoft.Extensions.Logging;

namespace ConnectionsManager.MessageFramework
{
    public abstract class MessageController
    {
        protected readonly ILogger _logger;

        public MessageController(ILogger logger)
        {
            _logger = logger;
        }
    }
}
