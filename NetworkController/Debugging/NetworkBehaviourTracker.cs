using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ConnectionsManager.Debugging
{
    public class NetworkBehaviourTracker
    {
        public List<TrackedConnection> Sessions { get; private set; } = new List<TrackedConnection>();

        public TrackedConnection NewSession()
        {
            TrackedConnection reference = new TrackedConnection();
            Sessions.Add(reference);
            return reference;
        }

        public string GenerateTextualSummary()
        {
            string result = "==============\n";
            result += "All sessions: \n";

            int i = 0;
            foreach (var s in Sessions)
            {
                result += $"[{i}] [{s.HumanReadableId}] [{s.Events.Count} events] \n";
                i++;
            }

            return result;
        }

        public string GenerateSummaryFor(int index)
        {
            return Sessions[index].GenerateTextualSummary();
        }
    }
}
