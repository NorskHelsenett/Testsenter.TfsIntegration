using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.TeamFoundation.TestManagement.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Shared.Common.Testing;
using TestFramework.Resources;

namespace Shared.TfsIntegration.Logic
{
    internal class TestRunGetter : IDisposable
    {
        private readonly TfsBase _tfsBase;

        internal TestRunGetter(TfsBase tfsBase)
        {
            _tfsBase = tfsBase;
        }

        internal TestRun Get(int testPlanId, string state)
        {
            return GetAutomatedTests(testPlanId, state);
        }

        internal TestRun Get(int testPlanId, string state, int[] testIds)
        {
            return GetAutomatedTests(testPlanId, state, testIds);
        }


        #region GetHelpers

        private long GetLastUpdatedAsTicks(ITestPointCollection collection)
        {
            var leader = DateTime.Now.AddDays(-100000);
            foreach (var point in collection)
            {
                if (DateTime.Compare(point.TestCaseWorkItem.DateModified, leader) == 1)
                    leader = point.TestCaseWorkItem.DateModified;
            }

            return leader.Ticks;
        }

        internal long GetLastUpdated(int testPlanId)
        {
            var allTestPoints = _tfsBase.GetAllTestPointsInPlan(testPlanId);
            return GetLastUpdatedAsTicks(allTestPoints);
        }

        protected TestRun GetAutomatedTests(int testPlanId, string state, int[] testIds = null)
        {
            return GetTests(testPlanId, true, state, testIds);
        }

        private TestRun GetTests(int testplanId, bool takeAutomatedTests, string state, params int[] onlyForIds)
        {
            var cachedSuiteNames = new Dictionary<int, string>();
            var allTestPoints = _tfsBase.GetAllTestPointsInPlan(testplanId, onlyForIds);

            var caseVsPoints = new Dictionary<int, Test>();

            foreach (var point in allTestPoints)
            {
                var testCaseId = point.TestCaseId;
                var testCase = point.TestCaseWorkItem;

                var automationStatus = testCase.CustomFields.TryGetById(10030);
                if (testCase.State != state)
                    continue;

                var isAutomatedTest = automationStatus != null && automationStatus.Value.Equals("Planned");

                if (isAutomatedTest)
                {
                    if (!takeAutomatedTests)
                        continue;
                }
                else
                {
                    if (takeAutomatedTests)
                        continue;
                }

                string serviceName = TfsBase.GetCustomFieldValue(testCase, "Service");
                string environment = _tfsBase.GetEnvironment(testCase, point.SuiteId, ref cachedSuiteNames);

                var testSteps = CreateTestStepsIds(testCase.Actions, testCaseId, environment);

                var testDataCollection = GetIterationDataCollection(testCase);
                if (testDataCollection == null || testDataCollection.Count == 0)
                    testDataCollection = new List<TestData> {new TestData()};

                int testDataCount = 0;
                foreach (var testData in testDataCollection)
                {
                    if (!caseVsPoints.ContainsKey(testCaseId))
                    {
                        caseVsPoints.Add(testCaseId, new Test(testCase.Id, testCase.Title, testCase.State, testCase.WorkItem.Tags));
                    }

                    var cpyTestSteps = testSteps.Select(x => new TestStep
                    {
                        IsSharedStep = x.IsSharedStep,
                        ParentId = x.ParentId,
                        ParentName = x.ParentName,
                        Result = null,
                        StepId = x.StepId,
                        Description = GetDescription(testData, x),
                        StepIndex = x.StepIndex,
                        ExpectedResult = GetExpectedResult(testData, x)
                    }).ToList();
                    caseVsPoints[testCaseId].Points.Add(new Point(point.Id, testData, cpyTestSteps, environment,
                        serviceName, testDataCount));
                    testDataCount++;
                }
            }

            var listOfTests = caseVsPoints.Values.ToList();

            return new TestRun
            {
                TfsUri = _tfsBase.Config.TfsUri,
                ForDeployment = false,
                NumberOfTests = listOfTests.Count(),
                TeamProject = _tfsBase.Config.TeamProject,
                Tests = listOfTests,
                TestPlanId = testplanId
            };
        }
        
        private List<TestData> GetIterationDataCollection(ITestCase testCase)
        {
            var dataColumns = testCase.Data.Tables[0].Columns;
            var data = (from DataRow row in testCase.Data.Tables[0].Rows
                        where ValidRow(row)
                        select new TestData(dataColumns, row.ItemArray)).ToList();

            if (testCase.Attachments.Any())
            {
                foreach (var testData in data)
                {
                    var keyValuePairs = testData.Data.Where(x => x.Key.Contains("_attachment"));
                    foreach (var keyValuePair in keyValuePairs)
                    {
                        var filename = keyValuePair.Value;
                        var attachment = testCase.Attachments.FirstOrDefault(x => x.Name == filename);
                        if (attachment == null)
                            continue;

                        var byteArray = new byte[attachment.Length];
                        attachment.DownloadToArray(byteArray, 0);
                        testData.Attachments.Add(filename, byteArray);
                    }
                }

            }
            return data;
        }

        private string GetDescription(TestData testData, TestStep ts)
        {
            try
            {
                var tempDescription = RemoveHtmlThings(ts.Description);
                var parts = GetParameterParts(tempDescription, '@', ' ');
                foreach (var part in parts)
                {
                    if (!testData.Data.ContainsKey(part))
                        continue;

                    tempDescription = tempDescription.Replace(part, testData.Data[part]);
                }

                return tempDescription;
            }
            catch (Exception)
            {
                return "";
            }
        }

        private string RemoveHtmlThings(string p)
        {
            var parts = GetParameterParts(p, '<', '>', true, true);
            foreach (var part in parts)
                p = p.Replace(part, "");

            return p;
        }

        private string[] GetParameterParts(string title, char start, char stop, bool includeStart = false, bool includeStop = false)
        {
            var result = new List<string>();
            int lastAt = -1;
            int indexCount = -1;
            foreach (var c in title)
            {
                indexCount++;

                if (c == start)
                    lastAt = includeStart ? indexCount : indexCount + 1;

                if ((c == stop || indexCount == title.Length - 1) && lastAt != -1)
                {
                    var length = (indexCount == title.Length - 1) ? indexCount - lastAt + 1 : indexCount - lastAt + (includeStop ? 1 : 0);
                    result.Add(title.Substring(lastAt, length));
                    lastAt = -1;
                }
            }
            return result.ToArray();
        }

        internal static bool ValidRow(DataRow row)
        {
            if (row?.ItemArray == null)
                return false;
            if (row.ItemArray.Length == 0)
                return false;
            if (row.ItemArray[0] == null)
                return false;

            return true;
        }

        private string GetExpectedResult(TestData testData, TestStep ts)
        {
            if (ts.ExpectedResult == null)
                return "";

            try
            {
                var tempExpectedResult = RemoveHtmlThings(ts.ExpectedResult);
                var parts = GetParameterParts(tempExpectedResult, '@', ' ');
                foreach (var part in parts)
                {
                    if (!testData.Data.ContainsKey(part))
                        continue;

                    tempExpectedResult = tempExpectedResult.Replace(part, testData.Data[part]);
                }

                return tempExpectedResult;

            }
            catch (Exception)
            {
                return "";
            }
        }

        private List<TestStep> CreateTestStepsIds(TestActionCollection testActionCollection, int testCaseId, string environment)
        {
            var result = new List<TestStep>();

            int i = 1;
            foreach (var action in testActionCollection)
            {
                if (TestConductor.IsSharedStep(action))
                {
                    var sharedStepRef = action as ISharedStepReference;
                    // ReSharper disable once PossibleNullReferenceException
                    var sharedStep = sharedStepRef.FindSharedStep();
                    foreach (var sharedStepAction in sharedStep.Actions)
                    {
                        result.Add(TestStep.CreateSharedStep(sharedStepRef.SharedStepId, sharedStepAction.Id, ((ITestStep)sharedStepAction).Title.ToString(), environment, sharedStep.Title));
                    }
                }
                else
                {
                    result.Add(TestStep.CreateStep(testCaseId, action.Id, ((ITestStep)action).Title.ToString(), i++, ((ITestStep)action).ExpectedResult.ToString(), environment));
                }
            }

            return result;
        }

        

        #endregion

        public void Dispose()
        {
            
        }
    }
}
