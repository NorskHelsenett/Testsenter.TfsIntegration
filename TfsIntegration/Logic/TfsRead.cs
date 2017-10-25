using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using log4net;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.TestManagement.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Shared.TfsIntegration.Interfaces;
using TestFramework.Resources;
using ITfsRead = Shared.TfsIntegration.Resources.ITfsRead;
using IValidTfsEnvironmentProvider = Shared.Common.Interfaces.IValidator;
using TestplanDescription = Shared.TfsIntegration.Resources.TestplanDescription;
using TestRunDescription = Shared.TfsIntegration.Resources.TestRunDescription;
using TfsConfiguration = Shared.TfsIntegration.Resources.TfsConfiguration;

namespace Shared.TfsIntegration.Logic
{
    public class TfsRead : TfsBase, ITfsRead, IInternalTfsRead
    {
        public TfsRead(TfsConfiguration tfsConfiguration, ILog logger, IValidTfsEnvironmentProvider validTfsEnvironment)
            : base(tfsConfiguration, logger, validTfsEnvironment)
        {
        }

        public long GetLastUpdated(int testPlanId)
        {
            using (var fact = new TestRunGetter(this))
            {
                return fact.GetLastUpdated(testPlanId);
            }
        }

        public TestRun Get(int testPlanId, string state)
        {
            using (var fact = new TestRunGetter(this))
            {
                return fact.Get(testPlanId, state);
            }
        }

        public TestRun Get(int testPlanId, string state, int[] testIds)
        {
            using (var fact = new TestRunGetter(this))
            {
                return fact.Get(testPlanId, state, testIds);
            }
        }

        public TestplanDescription GetTestplanDescription(int id)
        {
            var plan = TestManagementTeamProject.TestPlans.Find(id);

            return new TestplanDescription
            {
                Id = id,
                Name = plan.Name,
                NumberOfTests = GetNumberOfAutomatedTests(plan, "Ready"),
                Info = plan.Description
            };
        }


        public Dictionary<string, TestRunDescription> GetTestRunDescriptionGroupByIterationPath(int testRunId)
        {
            var testrun = TestManagementTeamProject.TestRuns.Find(testRunId);
            var tests = testrun.QueryResults().ToList().Select(t => new TestRunDescription
            {
                Id = t.TestCaseId,
                Failed = t.Outcome == TestOutcome.Passed ? 0 : 1,
                Passed = t.Outcome == TestOutcome.Passed ? 1 : 0
            }).ToList();

            var where = GetWiqlWhereClause(tests.Select(x => x.Id));
            var wiql = $@"SELECT [System.Id],[System.IterationPath] FROM WorkItems WHERE {where}";
            var query = new Query(WorkItemStore, wiql);

            var results = new Dictionary<string, TestRunDescription>();

            foreach (WorkItem wi in query.RunQuery())
            {
                var thisTest = tests.FirstOrDefault(y => y.Id == wi.Id);
                if (thisTest == null)
                    continue;

                thisTest.IterationPath = string.IsNullOrEmpty(wi.IterationPath) ? TestRunDescription.NoIteration : wi.IterationPath;
                if (!results.ContainsKey(thisTest.IterationPath))
                    results.Add(thisTest.IterationPath, new TestRunDescription { IterationPath = thisTest.IterationPath });

                results[thisTest.IterationPath].Failed += thisTest.Failed;
                results[thisTest.IterationPath].Passed += thisTest.Passed;
            }

            return results;
        }

        public TestRunDescription GetTestRunDescription(int testRunId)
        {
            var results = TestManagementTeamProject.TestRuns.Find(testRunId);

            return new TestRunDescription
            {
                Failed = results.Statistics.FailedTests,
                Passed = results.Statistics.PassedTests,
                Id = testRunId
            };
        }

        public Project GetProjectById(int id)
        {
            return WorkItemStore.Projects.GetById(id);
        }

        public ITestRun GetTestRunForId(int testRunId)
        {
            return TestManagementService.QueryTestRuns("SELECT * FROM TestRun WHERE TestRunID=" + testRunId).FirstOrDefault();
        }

        public IEnumerable<ITestRun> GetTestRunsForPlan(int planId)
        {
            return TestManagementService.QueryTestRuns("SELECT * FROM TestRun WHERE TestRun.TestPlanId=" + planId + " AND TestRun.IsBvt=false");
        }

        public WorkItemCollection GetWorkItemsWithBuildNumber()
        {
            var wiql = $@"SELECT [System.Id],[System.WorkItemType],[System.Title],[System.State],[System.AreaPath],[System.IterationPath],[System.Tags] FROM WorkItems WHERE [System.State] <> 'Removed' AND [Helsedir.NHNBuildNumber] <> '' AND ( [System.WorkItemType] = 'Change Request' OR [System.WorkItemType] = 'Bug' OR [System.WorkItemType] = 'Product Backlog Item' ) ORDER BY [System.WorkItemType]";
            return WorkItemStore.Query(wiql);
        }

        public NodeInfo GetNodeInfo(string nodeUri)
        {
            try
            {
                return CommonStructureService.GetNode(nodeUri);
            }
            catch (Exception e)
            {
                throw new AuthenticationException("The user svcTfsTestSenter does not have permission to access the specified area. Error Message: " + e);
            }
        }
    }
}
