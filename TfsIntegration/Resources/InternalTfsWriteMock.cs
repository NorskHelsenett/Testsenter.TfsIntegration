using System.Collections.Generic;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Shared.TfsIntegration.Interfaces;

namespace Shared.TfsIntegration.Resources
{
    public class InternalTfsWriteMock : IInternalTfsWrite
    {
        public List<WorkItem> CopyTestcases(List<WorkItem> cases, string targetenvironmentDescription, string targetstate)
        {
            return cases;
        }
    }
}
