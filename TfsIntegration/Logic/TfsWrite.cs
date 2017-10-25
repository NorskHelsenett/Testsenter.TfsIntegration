using System.Collections.Generic;
using log4net;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Shared.Common.Logic;
using Shared.Common.Resources;
using Shared.TfsIntegration.Interfaces;
using TestFramework.Resources;
using ITfsWrite = Shared.TfsIntegration.Resources.ITfsWrite;
using IValidTfsEnvironmentProvider = Shared.Common.Interfaces.IValidator;
using TfsConfiguration = Shared.TfsIntegration.Resources.TfsConfiguration;

namespace Shared.TfsIntegration.Logic
{
    public class TfsWrite : TfsBase, ITfsWrite, IInternalTfsWrite
    {
        private readonly ITestStepResultBlobStore _testStepBlobStore;

        public TfsWrite(TfsConfiguration tfsConfiguration, ITestStepResultBlobStore testStepBlobStore, ILog logger, IValidTfsEnvironmentProvider validTfsEnvironment) : base(tfsConfiguration, logger, validTfsEnvironment)
        {
            _testStepBlobStore = testStepBlobStore;
        }

        public int Save(TestRun testrun, int onlyForTestCaseWithId = -1)
        {
            using (var savy = new TestRunSaver(this, _testStepBlobStore))
            {
                return savy.Save(testrun, onlyForTestCaseWithId);
            }
        }

        //public void UpdateTfsWorkItemsAfterNewDeploy(string environmentToUpdateDescription, string buildNrDeployed)
        //{
        //    using (var x = new TfsBuildNrUpdater(this))
        //    {
        //        x.UpdateTfsWorkItemsAfterNewDeploy(environmentToUpdate, buildNrDeployed);
        //    }
        //}

        public List<WorkItem> CopyTestcases(List<WorkItem> cases, string targetenvironmentDescription, string targetstate)
        {
            var result = new List<WorkItem>();
            foreach (WorkItem testcase in cases)
            {
                testcase.Open();
                testcase.Fields["Found In Environment"].Value = targetenvironmentDescription;
                testcase.Fields["Assigned to"].Value = null;
                var copy = testcase.Copy();
                copy.Save();

                if (targetstate == null)
                {
                    copy.State = testcase.State;
                }
                else if (copy.State != targetstate)
                {
                    copy.State = targetstate;
                }
                copy.Save();
                result.Add(copy);
            }

            return result;
        }
    }
}
