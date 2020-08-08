using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ConnectionsManager.Debugging
{
    public class TrackedConnection
    {
        public List<ConnectionEvents> Events { get; set; } = new List<ConnectionEvents>();
        public string RandomId { get; private set; }
        public string KnownAddress { get; set; } = null;
        public string HumanReadableId
        {
            get
            {
                if (KnownAddress != null) return KnownAddress;
                return RandomId;
            }
        }

        public TrackedConnection()
        {
            // Generating random connection ID
            Random random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            RandomId = new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        [Conditional("DEBUG")]
        public void AddNewEvent(PossibleEvents eventType, string comment)
        {
            AddNewEvent(new ConnectionEvents(eventType, comment));
        }

        [Conditional("DEBUG")]
        public void AddNewEvent(ConnectionEvents conEvent)
        {
            Events.Add(conEvent);
        }

        public string GenerateTextualSummary()
        {
            string result = "==============\n";
            result += "All events: \n";

            foreach (var e in Events)
            {
                result += $"[{Enum.GetName(e.EventType.GetType(), e.EventType)}] {e.Comment}\n";
            }

            return result;
        }
    }
}
