using System.Net;
using TransmissionComponent.Structures;

namespace TransmissionComponent
{
    public class NewMessageEventArgs
    {
        public DataFrame DataFrame { get; set; }
        public IPEndPoint SenderIPEndpoint { get; set; }
    }
}