using System.Collections.Generic;
using TestFramework.Resources;

namespace Shared.TfsIntegration.Resources
{
    public interface ITfsRead
    {
        TestRun Get(int testPlanId, string state);
        TestRun Get(int testPlanId, string state, int[] testIds);
        long GetLastUpdated(int testPlanId);
        TestplanDescription GetTestplanDescription(int id);
        TestRunDescription GetTestRunDescription(int testRunId);
        Dictionary<string, TestRunDescription> GetTestRunDescriptionGroupByIterationPath(int testRunId);

    }
}
