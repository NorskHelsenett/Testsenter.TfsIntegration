using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TeamFoundation.TestManagement.Client;
using TestFramework.Resources;
using TfsNotes = Shared.TfsIntegration.Resources.TfsNotes;

namespace Shared.TfsIntegration.Logic
{
    internal class TestRunSaver : IDisposable
    {
        private readonly TfsBase _tfsBase;
        private readonly ITestStepResultBlobStore _testStepBlobStore;

        public TestRunSaver(TfsBase tfsBase, ITestStepResultBlobStore testStepBlobStore)
        {
            _tfsBase = tfsBase;
            _testStepBlobStore = testStepBlobStore;
        }

        internal int Save(TestRun testrun, int onlyForTestCaseWithId = -1)
        {
            var allPoints = _tfsBase.GetAllTestPointsInPlan(testrun.TestPlanId, onlyForTestCaseWithId != -1 ? new List<int> { onlyForTestCaseWithId } : testrun.Tests.Select(x => x.TestCaseId).Distinct());

            if (testrun.Tests.Count == 0)
                return -1;

            int testRunId = -1;
            var testRun = CreateTestRun(testrun.Started, testrun.Finished, allPoints, ref testRunId, testrun.ForDeployment);

            foreach (var test in testrun.Tests)
            {
                if (onlyForTestCaseWithId != -1 && test.TestCaseId != onlyForTestCaseWithId)
                    continue;

                var testCase = _tfsBase.GetTestCase(test.TestCaseId);
                var grouping = test.Points.GroupBy(t => t.Id);

                foreach (var item in grouping)
                {
                    var id = item.Key;
                    var testPoint = allPoints.SingleOrDefault(p => p.Id == id);
                    if (testPoint == null)
                        continue;

                    var testCaseResult = GetTestCaseResult(testRun, testCase, testPoint);

                    var count = 1;
                    var testOutcome = TestOutcome.Passed;
                    foreach (var rowOfData in item)
                    {
                        var iteration = CreateIterationResult(testCaseResult, count);
                        var testConductor = new TestConductor(iteration, testCase, rowOfData.Data);
                        var result = testConductor.SetStepResults(rowOfData.TestSteps);

                        result.Outcome = testConductor.TestOutCome;
                        result.Duration = rowOfData.Duration;
                        testCaseResult.Iterations.Add(result);

                        if (result.Outcome != TestOutcome.Passed)
                        {
                            testOutcome = result.Outcome;
                        }

                        if (_testStepBlobStore != null && testConductor.ResultBlob != null && testRunId > 0)
                            _testStepBlobStore.SaveErrorInformationBlob(testRunId, test.TestCaseId, count, testConductor.ResultBlob);

                        SetNotes(testCaseResult, testConductor);
                        count++;
                    }

                    SaveTestCaseResult(testCaseResult, testOutcome);
                }
            }

            return testRunId;
        }

        private void SaveTestCaseResult(ITestCaseResult testCaseResult, TestOutcome overAllResult)
        {
            testCaseResult.DateCompleted = DateTime.Now;
            testCaseResult.State = TestResultState.Completed;
            testCaseResult.Outcome = overAllResult;
            testCaseResult.Duration = testCaseResult.DateCompleted.Subtract(testCaseResult.DateStarted);
            testCaseResult.Save(false);
        }


        // ReSharper disable once RedundantAssignment
        private ITestCaseResultCollection CreateTestRun(DateTime started, DateTime finished, ITestPointCollection allPoints, ref int createdTestrunId, bool isBuildVerifcationTest = false)
        {
            var owner = _tfsBase.TestManagementService.TfsIdentityStore.FindByTeamFoundationId(allPoints.First().TestCaseWorkItem.OwnerTeamFoundationId);
            var run = allPoints.First().Plan.CreateTestRun(false);

            run.IsBvt = isBuildVerifcationTest;
            run.DateStarted = _tfsBase.GetNorwegianTime(started);
            run.DateCompleted = _tfsBase.GetNorwegianTime(finished);
            run.AddTestPoints(allPoints, owner);
            run.Save();

            createdTestrunId = run.Id;

            return run.QueryResults();
        }

        private ITestCaseResult GetTestCaseResult(ITestCaseResultCollection testRun, ITestCase testCase, ITestPoint testPoint)
        {
            var owner = _tfsBase.TestManagementService.TfsIdentityStore.FindByTeamFoundationId(testCase.OwnerTeamFoundationId);
            var result = testRun.Single(x => x.TestPointId == testPoint.Id);
            result.Owner = owner;
            result.RunBy = owner;
            result.State = TestResultState.Completed;
            result.DateStarted = DateTime.Now;
            result.Duration = new TimeSpan(0L);
            result.DateCompleted = DateTime.Now.AddMinutes(0.0);

            return result;
        }

        private ITestIterationResult CreateIterationResult(ITestCaseResult testCaseResult, int iterationId)
        {
            var iterationResult = testCaseResult.CreateIteration(iterationId);
            iterationResult.DateStarted = DateTime.Now;
            return iterationResult;
        }

        private void SetNotes(ITestCaseResult testCaseResult, TestConductor testConductor)
        {
            if (testConductor.ResultBlob == null)
                return;

            var note = new TfsNotes(testConductor.ResultBlob);

            try
            {
                AppendToNotes(testCaseResult, note.Serialize());
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch (Exception)
            {
            }
        }

        private void AppendToNotes(ITestCaseResult testCaseResult, string msg)
        {
            if (string.IsNullOrEmpty(msg))
                return;

            if (testCaseResult.Comment == null)
                testCaseResult.Comment = "";

            testCaseResult.Comment += msg;
        }

        public void Dispose()
        {
            
        }
    }
}
