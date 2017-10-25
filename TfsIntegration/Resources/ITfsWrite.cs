using Shared.Common.Resources;
using TestFramework.Resources;

namespace Shared.TfsIntegration.Resources
{
    public interface ITfsWrite
    {
        int Save(TestRun testrun, int onlyForTestCaseWithId = -1);

        //void UpdateTfsWorkItemsAfterNewDeploy(TfsEnvironment environmentToUpdate, string buildNrDeployed);
    }
}
