using System.Collections.Generic;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.TestManagement.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace Shared.TfsIntegration.Interfaces
{
    public interface IInternalTfsRead
    {
        Project GetProjectById(int id);
        ITestRun GetTestRunForId(int testRunId);
        IEnumerable<ITestRun> GetTestRunsForPlan(int planId);
        WorkItemCollection GetWorkItemsWithBuildNumber();
        NodeInfo GetNodeInfo(string nodeUri);
        WorkItemCollection GetTestCasesForWorkitem(int workitemId, string environmentDescription);
        WorkItemCollection GetRegressionTestCasesByServices(string[] services, string environmentDescription);
    }
}
