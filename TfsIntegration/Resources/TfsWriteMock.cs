using System;
using Shared.Common.Resources;
using TestFramework.Resources;

namespace Shared.TfsIntegration.Resources
{
    public class TfsWriteMock : ITfsWrite
    {
        public int Save(TestRun testrun, int onlyForTestCaseWithId = -1)
        {
            return new Random().Next(100000);
        }

        //public void UpdateTfsWorkItemsAfterNewDeploy(TfsEnvironment environmentToUpdate, string buildNrDeployed)
        //{
        //}
    }
}
