using NetworkController.Models;
using System;

namespace FrameworkPrototype
{
    /// <summary>
    /// Attribute for methods handling specific requests from external source
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class IncomingMessageAttribute : Attribute
    {
        public IncomingMessageAttribute(MessageType correspondingMessageId)
        {
            CorrespondingMessageId = (int)correspondingMessageId;
        }

        public int CorrespondingMessageId { get; }
    }
}
