using FrameworkPrototype;
using Microsoft.Extensions.Logging;
using NetworkController.Interfaces.ForTesting;
using NetworkController.Models;
using NetworkController.UDP;
using NetworkController.UDP.MessageHandlers;
using System.Collections.Generic;
using System.Reflection;

namespace ConnectionsManager.MessageFramework
{
    /// <summary>
    /// Class for managing all classes containing IncomingMessage handlers
    /// </summary>
    public class IncomingMessageCaller
    {
        public List<MessageController> Classes { get; set; } = new List<MessageController>();

        public IncomingMessageCaller(ILogger logger)
        {
            Register(new HandshakeController(logger));
            Register(new NetworkBuildingController(logger));
            Register(new PingController(logger));
        }

        public void Register(MessageController newClass)
        {
            Classes.Add(newClass);
        }

        /// <summary>
        /// Returns true when found function to call
        /// </summary>
        /// <param name="mType"></param>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public bool Call(ExternalNode source, int mType, byte[] bytes)
        {
            foreach(var c in Classes){
                MethodInfo[] methodInfos = c.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);

                // write method names
                foreach (MethodInfo methodInfo in methodInfos)
                {
                    IncomingMessageAttribute attr = methodInfo.GetCustomAttribute<IncomingMessageAttribute>();

                    if(attr != null && attr.CorrespondingMessageId == mType)
                    {
                        _ = methodInfo.Invoke(c, new object[] { (IExternalNodeInternal)source, bytes });
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
