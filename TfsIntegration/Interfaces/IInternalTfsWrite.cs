using System.Collections.Generic;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Shared.Common.Resources;

namespace Shared.TfsIntegration.Interfaces
{
    public interface IInternalTfsWrite
    {
        List<WorkItem> CopyTestcases(List<WorkItem> cases, string targetenvironmentDescription, string targetstate);
    }
}
