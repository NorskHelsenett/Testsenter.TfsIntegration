using System;
using System.Collections.Generic;
using Microsoft.TeamFoundation.TestManagement.Client;
using System.Linq;
using Shared.Common.Testing;
using TestFramework.Resources;

namespace Shared.TfsIntegration.Logic
{
    public class TestConductor
    {
        #region Fields
        public TestOutcome TestOutCome { get; set; }
        private readonly ITestIterationResult _iteration;
        private readonly ITestCase _testCase;
        private readonly TestData _iterationData;
        private int _actionCounter;
        private ITestActionResult _currentTestStepResult;
        private ISharedStepResult _currentSharedTestStepResult;
        private ITestAction _currentStep;
        private List<ITestAction> _currentSharedSteps = new List<ITestAction>();
        private int _currentSharedStepsPointer = 0;
        private ISharedStepReference _currentSharedStep = null;
        public TestStepResultBlob ResultBlob { get; set; }

        #endregion

        public TestConductor(ITestIterationResult iteration, ITestCase testCase, TestData iterationData)
        {
            _testCase = testCase;
            _iteration = iteration;
            _iterationData = iterationData;
            _actionCounter = 0;
            TestOutCome = TestOutcome.Passed;
        }

        public ITestIterationResult SetStepResults(List<TestStep> testSteps)
        {
            int stepCount = 1;
            foreach (var step in testSteps)
            {
                InitTestStep();

                if(step.Result != null && NotAllNull(step.Result.Exception, step.Result.RefToScreenshot, step.Result.AddtionalInformation))
                    ResultBlob = new TestStepResultBlob(stepCount, step.Result.RefToScreenshot, step.Result.AddtionalInformation, step.Result.Exception);

                if(step.Result == null)
                {
                    FailTestStep(new Tools.StepFailInformation("Result var null. Ikke kjørt eller bug i testcasen"));
                    break;
                }

                if (!step.Result.Success) 
                {
                    var stepFailInfo = new Tools.StepFailInformation(step.Result.ErrorMessage);
                    FailTestStep(stepFailInfo);

                    break;
                }
                else
                    CloseTestStep();

                stepCount++;
            }

            return GetTestResult();
        }

        private bool NotAllNull(Exception e, params string[] args)
        {
            if (e != null)
                return true;

            return args.Any(x => !string.IsNullOrEmpty(x));
        }

        public ITestIterationResult GetTestResult()
        {
            return _iteration;
        }

        #region StepHandling
        private void InitTestStep()
        {
            SetNextCurrentStep();
            HandleSharedStep();
            CreateResult();
        }

        private void CloseTestStep()
        {
            if (_currentTestStepResult == null)
                throw new NullReferenceException("CurrentTestStepResult is null. Developer probably forgot to call InitTestStep()");

            FinishTestStep();
        }

        private void FailTestStep(Tools.StepFailInformation stepFailInformation = null)
        {
            TestOutCome = TestOutcome.Failed;

            if (_currentTestStepResult == null)
                throw new NullReferenceException("CurrentTestStepResult is null. Developer probably forgot to call InitTestStep()");

            SetStepFailInformation(stepFailInformation);

            FinishTestStep(TestOutcome.Failed);
        }

        #endregion

        #region Helpers
        private void SetStepFailInformation(Tools.StepFailInformation stepFailInformation)
        {
            if (stepFailInformation.Comment != null)
                _currentTestStepResult.ErrorMessage = stepFailInformation.Comment;
        }

        private void FinishTestStep(TestOutcome testOutcome = TestOutcome.Passed)
        {
            if (CurrentlyInSharedStep())
            {
                _currentTestStepResult.Outcome = testOutcome;
                _currentSharedTestStepResult.Actions.Add(_currentTestStepResult);

                if (!HasMoreSharedSteps() || (testOutcome == TestOutcome.Failed || testOutcome == TestOutcome.Error))
                {
                    CloseSharedStep(testOutcome);
                    _currentSharedStep = null;
                    _currentSharedSteps = new List<ITestAction>();
                }

            }
            else
                SetTestStepResultAndSaveToIteration(_currentTestStepResult, testOutcome);
        }

        private void CloseSharedStep(TestOutcome testOutcome)
        {
            SetTestStepResultAndSaveToIteration(_currentSharedTestStepResult, testOutcome);
        }

        private ITestStepResult CreateTestStepResult(ITestStep step, int actionId = -1)
        {
            if (actionId < 0)
                actionId = step.Id;

            var result = _iteration.CreateStepResult(actionId);

            try
            {
                foreach (var parameter in step.Title.ParameterNames)
                {
                    result.Parameters.Add(parameter, _iterationData.GetValue(parameter), "");
                }

            }
            catch (Exception e)
            {
                //not a critical error. Therefore, continue testing ..
            }

            return result;
        }

        private ITestActionResult CreateSharedStepResult(ISharedStepReference testStep)
        {
            return _iteration.CreateSharedStepResult(testStep.Id, testStep.FindSharedStep().Id);
        }

        private void SetTestStepResultAndSaveToIteration(ITestActionResult testResult, TestOutcome testOutcome)
        {
            SetTestStepResult(testResult, testOutcome);
            _iteration.Actions.Add(testResult);
        }

        private void SetTestStepResult(ITestActionResult testResult, TestOutcome testOutcome)
        {
            testResult.Outcome = testOutcome;
            testResult.DateCompleted = DateTime.Now;
            testResult.Duration = testResult.DateCompleted.Subtract(testResult.DateStarted);
        }

        private void SetNextCurrentStep()
        {
            try
            {
                if (CurrentlyInSharedStep())
                {
                    if (HasMoreSharedSteps())
                    {
                        _currentStep = _currentSharedSteps[_currentSharedStepsPointer++];
                    }
                    else
                    {
                        _currentSharedSteps = new List<ITestAction>();
                        _currentStep = _testCase.Actions[_actionCounter];
                    }
                }
                else
                {
                    _currentStep = _testCase.Actions[_actionCounter];
                    _actionCounter++;
                }
            }
            catch (Exception)
            {
                throw new IndexOutOfRangeException("Action with id " + _actionCounter + " is out of bounds. Verify the number of teststeps the testcase have");
            }
        }

        private void CreateResult()
        {
            _currentTestStepResult = CreateTestStepResult(_currentStep as ITestStep);
            _currentTestStepResult.DateStarted = DateTime.Now;
        }

        private void HandleSharedStep()
        {
            if (IsSharedStep(_currentStep))
            {
                _currentSharedStepsPointer = 0;
                _currentSharedStep = (ISharedStepReference)_currentStep;
                ISharedStep ss = _currentSharedStep.FindSharedStep();
                TestActionCollection sharedActions = ss.Actions;
                _currentSharedTestStepResult = CreateSharedStepResult(_currentSharedStep) as ISharedStepResult;

                _currentSharedSteps.AddRange(sharedActions);
                _currentStep = _currentSharedSteps[_currentSharedStepsPointer++];
            }
        }

        private bool HasMoreSharedSteps()
        {
            return _currentSharedStepsPointer < _currentSharedSteps.Count;
        }

        private bool CurrentlyInSharedStep()
        {
            return _currentSharedStep != null;
        }

        public static bool IsSharedStep(ITestAction step)
        {
            return !(step is ITestStep);
        }

        #endregion
    }
}
