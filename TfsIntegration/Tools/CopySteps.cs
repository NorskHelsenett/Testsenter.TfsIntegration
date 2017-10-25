using Microsoft.TeamFoundation.TestManagement.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Shared.Common.Logic;
using Shared.Common.Resources;
using Shared.TfsIntegration.Logic;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using log4net;
using TestFramework.Resources;
using IValidTfsEnvironmentProvider = Shared.Common.Interfaces.IValidator;
using TfsConfiguration = Shared.TfsIntegration.Resources.TfsConfiguration;

namespace Shared.TfsIntegration.Tools
{
    public class CopySteps : TfsBase
    {
        public CopySteps(TfsConfiguration tfsConfiguration, ILog logger, IValidTfsEnvironmentProvider validTfsEnvironment) : base(tfsConfiguration, logger, validTfsEnvironment)
        {
        }

        private string ReplaceWhereApplicable(string masterTitle, Dictionary<string, string> replacements)
        {
            foreach(var key in replacements.Keys)
            {
                if (masterTitle.Contains(key))
                    masterTitle = masterTitle.Replace(key, replacements[key]);
            }

            return masterTitle;
        }

        public void Clone(int masterPbiId, string targetIteration, int linkToPbiId, string environmentDescription, Dictionary<string, string> replacements)
        {
            var masterTestCases = GetTestCasesForWorkitem(masterPbiId, environmentDescription);

            foreach (WorkItem masterWorkitem in masterTestCases)
            {
                if (masterWorkitem.State != "Ready")
                    continue;

                var masterTestcase = GetTestCase(masterWorkitem.Id);
                var masterDatasetRow = masterTestcase.Data.Copy().Tables[0].Rows[0].ItemArray;

                var copy = masterTestcase.WorkItem.Copy(masterWorkitem.Type, WorkItemCopyFlags.None);

                copy.Fields["Found In Environment"].Value = environmentDescription;
                copy.Title = ReplaceWhereApplicable(masterTestcase.Title, replacements);

                if(!string.IsNullOrEmpty(targetIteration))
                    SetIteration(copy, targetIteration);

                SetDesign(copy);
                LinkToPbi(copy, linkToPbiId);
                copy.Save();

                var slave = GetTestCase(copy.Id);
                SetReady(slave);
                RemoveAllSteps(slave);
                CopySharedSteps(slave, GetSharedSteps(masterTestcase.Actions));
                slave.Save();

                var array = new object[slave.Data.Tables[0].Rows[0].ItemArray.Length];
                for(int i=0; i<array.Length; i++)
                {
                    var tmp = masterDatasetRow[i] is string ? ReplaceWhereApplicable((string)masterDatasetRow[i], replacements) : "";
                    array[i] = tmp;
                }
                slave.Data.Tables[0].Rows[0].ItemArray = array;
                slave.Save();
            }
        }

        private static ISharedStepReference[] GetSharedSteps(TestActionCollection actions)
        {
            var result = new List<ISharedStepReference>();
            foreach(ISharedStepReference step in actions)
                result.Add(step);

            return result.ToArray();
        }

        public void DoIt(int masterId, IEnumerable<int> slaveSetIds, string targetIteration, int linkToPbiId)
        {
            var master = GetTestCase(masterId);
            var masterDatasetRow = master.Data.Copy().Tables[0].Rows[0].ItemArray;

            foreach (var slaveId in slaveSetIds)
            {
                var slave = GetTestCase(slaveId);
                var copy = slave.WorkItem.Copy(slave.WorkItem.Type, WorkItemCopyFlags.None);
                copy.Fields["Found In Environment"].Value = "Test-01";
                copy.Title = "Negativ ACK - " + slave.Title.Replace("Sjekk hodemelding -", "");
                SetIteration(copy, targetIteration);
                SetDesign(copy);
                LinkToPbi(copy, linkToPbiId);
                copy.Save();

                slave = GetTestCase(copy.Id);
                SetReady(slave);
                RemoveAllSteps(slave);
                CopySharedSteps(slave, (ISharedStepReference)master.Actions[0], (ISharedStepReference)master.Actions[1], (ISharedStepReference)master.Actions[2], (ISharedStepReference)master.Actions[3]);

                slave.Save();

                slave = GetTestCase(copy.Id);

                var array = new object[slave.Data.Tables[0].Rows[0].ItemArray.Length];
                array[0] = slave.Data.Tables[0].Rows[0].ItemArray[0];
                array[1] = masterDatasetRow[0];
                array[2] = masterDatasetRow[1];
                array[6] = "false";
                slave.Data.Tables[0].Rows[0].ItemArray = array;

                slave.Save();
            }
        }

        private static object[] GetTestData(string title, object[] obj)
        {
            var x = title.Split('-')[1].Trim();
            int errorCode;
            var isErrorCode = int.TryParse(x, out errorCode);

            if (isErrorCode)
                obj[2] = errorCode;

            return obj;
        }

        private static void RemoveAllSteps(ITestCase slave)
        {
            var all = slave.Actions.Count;
            for (int i = all - 1; i >= 0; i--)
                slave.Actions.RemoveAt(i);
        }

        private static void LinkToPbi(ITestCase slave, int linkToPbiId)
        {
            slave.Links.Add(new RelatedLink(linkToPbiId));
        }

        private static void LinkToPbi(WorkItem slave, int linkToPbiId)
        {
            slave.Links.Add(new RelatedLink(linkToPbiId));
        }

        public List<int> GetSlaveSet(string iteration)
        {
            var ids = new List<int>();
            var all = GetIteration(iteration);
            foreach (WorkItem wi in all)
            {
                if (!wi.Title.Contains("Meldingsdetaljer - "))
                    continue;

                if (wi.Type.Name != "Test Case")
                    continue;

                ids.Add(wi.Id);
            }

            var x = ids.Count;

            return ids;
        }

        public static void SetReady(ITestCase testCase)
        {
            testCase.State = "Ready";
        }

        public static void SetReady(WorkItem testCase)
        {
            testCase.State = "Ready";
        }

        public static void SetDesign(WorkItem testCase)
        {
            testCase.State = "Design";
        }

        public static void SetIteration(ITestCase testCase, string target)
        {
            testCase.CustomFields["Iteration Path"].Value = target;
        }

        public static void SetIteration(WorkItem wi, string target)
        {
            wi.IterationPath = target;
        }

        public static void CopySharedSteps(ITestCase slave, params ISharedStepReference[] ss)
        {
            foreach (var sharedStep in ss)
            {
                ISharedStepReference sharedStepReference = slave.CreateSharedStepReference();
                sharedStepReference.SharedStepId = sharedStep.SharedStepId;
                slave.Actions.Add(sharedStepReference);
            }
        }

        public static void CopyTestData(object[] masterData, ITestCase slave)
        {
            var errorCode = GetIterationDataCollection(slave).First().GetValue("errorCode");
            masterData[0] = errorCode;
            slave.Data.Tables[0].Rows[0].ItemArray = masterData;

            slave.Save();
        }

        public static List<TestData> GetIterationDataCollection(ITestCase testCase)
        {
            var dataColumns = testCase.Data.Tables[0].Columns;
            var data = (from DataRow row in testCase.Data.Tables[0].Rows
                        where TestRunGetter.ValidRow(row)
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
                        //throw new ArgumentException("Could not find attachment with name " + filename + " in test case " + testCase.Id);
                        var byteArray = new byte[attachment.Length];
                        attachment.DownloadToArray(byteArray, 0);
                        testData.Attachments.Add(filename, byteArray);
                    }
                }

            }
            return data;
        }

        
    }
}
