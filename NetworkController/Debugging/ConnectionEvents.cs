using System;

namespace ConnectionsManager.Debugging
{
    public class ConnectionEvents
    {
        public PossibleEvents EventType { get; private set; }
        public string Comment { get; set; }

        public ConnectionEvents(PossibleEvents eventType, string comment)
        {
            Comment = comment;
            EventType = eventType;
        }

        public override string ToString()
        {
            return Enum.GetName(typeof(PossibleEvents), EventType) + " (" + Comment + ")";
        }
    }
}