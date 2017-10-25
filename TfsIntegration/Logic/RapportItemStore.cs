using System;
using Microsoft.TeamFoundation.TestManagement.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Shared.Common.Interfaces;
using TfsConfiguration = Shared.TfsIntegration.Resources.TfsConfiguration;

namespace Shared.TfsIntegration.Logic
{
    public class RapportItemStore : TfsBase
    {
        public string TfsTeamProject;
        public string ProjectName;
        public string IterationPathName;
        private readonly TestCaseResultsComparer _testCaseComparer;

        private WorkItemCollection _workItems;
        private Dictionary<int, List<WorkItem>> _linkedItems;
        private readonly HashSet<int> _testCaseTargetIds;
        private Dictionary<int, ITestCaseResult> _testCaseResult;

        public RapportItemStore(TfsConfiguration tfsConfiguration, ILog logger, IValidator validTfsEnvironment, string iterationPathName)
            : base(tfsConfiguration, logger, validTfsEnvironment)
        {
            TfsTeamProject = tfsConfiguration.TeamProject;
            IterationPathName = iterationPathName;
            _testCaseComparer = new TestCaseResultsComparer();
            _testCaseTargetIds = new HashSet<int>();

            Init();
        }

        public WorkItemCollection GetWorkItems()
        {
            return _workItems;
        }

        public List<WorkItem> GetLinkedItems(int motherId)
        {
            if (!_linkedItems.ContainsKey(motherId))
                return new List<WorkItem>();

            return _linkedItems[motherId];
        }

        public ITestCaseResult GetLatestTestcaseResult(int id)
        {
            if (!_testCaseResult.ContainsKey(id))
                return null;

            return _testCaseResult[id];
        }

        public Dictionary<int, List<WorkItemLinkInfo>> GetApplicableLinksForWorkitem(string iterationName)
        {
            var wiql = $@"SELECT [System.Id],[System.WorkItemType],[System.Title],[System.State],[System.AreaPath],[System.IterationPath],[System.Tags] FROM WorkItemLinks WHERE ([Source].[System.State] <> 'Removed' AND [Source].[System.IterationPath] = '{iterationName}' AND ( [Source].[System.WorkItemType] = 'Change Request' OR [Source].[System.WorkItemType] = 'Bug' OR [Source].[System.WorkItemType] = 'Product Backlog Item' )) AND (( [Target].[System.WorkItemType] = 'Test Case' AND [Target].[System.State] NOT CONTAINS 'Closed' ) OR [Target].[System.WorkItemType] = 'Bug') ORDER BY [System.WorkItemType] mode(MustContain)";

            var query = new Query(WorkItemStore, wiql);

            var dic = new Dictionary<int, List<WorkItemLinkInfo>>();
            foreach (var item in query.RunLinkQuery())
            {
                if (item.SourceId == 0 || item.TargetId == 0)
                    continue;

                if (!dic.ContainsKey(item.SourceId))
                    dic.Add(item.SourceId, new List<WorkItemLinkInfo>());

                if (dic[item.SourceId].All(element => element.TargetId != item.TargetId))
                    dic[item.SourceId].Add(item);
            }

            return dic;
        }

        public ITestCaseResultCollection GetTestcaseResultsFromIds(string projectName, List<int> testcaseIds)
        {
            if (testcaseIds == null || !testcaseIds.Any())
                return null;

            var whereClause = GetWiqlWhereClause(testcaseIds, "TestCaseId");
            var wiql = $"Select * from TestResult Where {whereClause}";

            return TestManagementTeamProject.TestResults.Query(wiql);
        }

        #region Privates

        private WorkItemCollection GetBugsChangerequestAndPbiForIteration(string iterationName)
        {
            var wiql = $@"SELECT [System.Id],[System.WorkItemType],[System.Title],[System.State],[System.AreaPath],[System.IterationPath],[System.Tags] FROM WorkItems WHERE [System.State] <> 'Removed' AND [System.IterationPath] = '{iterationName}' AND ( [System.WorkItemType] = 'Change Request' OR [System.WorkItemType] = 'Bug' OR [System.WorkItemType] = 'Product Backlog Item' ) ORDER BY [System.WorkItemType]";
            return WorkItemStore.Query(wiql);
        }

        private void Init()
        {
            _workItems = GetBugsChangerequestAndPbiForIteration(IterationPathName);
            _linkedItems = GetLinkedItems();
            SetTestResults();
        }

        private Dictionary<int, List<WorkItem>> GetLinkedItems()
        {
            if (_linkedItems != null)
                return _linkedItems;

            var allLinks = GetApplicableLinksForWorkitem(IterationPathName);
            var allTargetIds = new List<int>();

            foreach (var source in allLinks.Values)
                foreach (var target in source)
                {
                    allTargetIds.Add(target.TargetId);
                    if ((target.LinkTypeId == 5 || target.LinkTypeId == 4 || target.LinkTypeId == 1) && !_testCaseTargetIds.Contains(target.TargetId))
                        _testCaseTargetIds.Add(target.TargetId);
                }

            var linkedItems = GetWorkitemsFromIds(allTargetIds);
            if (linkedItems == null)
            {
                _linkedItems = new Dictionary<int, List<WorkItem>>();
                return _linkedItems;
            }

            var temp = new Dictionary<int, WorkItem>();
            foreach (WorkItem linkedItem in linkedItems)
                temp.Add(linkedItem.Id, linkedItem);

            var result = new Dictionary<int, List<WorkItem>>();
            foreach (var source in allLinks.Keys)
            {
                result.Add(source, new List<WorkItem>());

                foreach (var target in allLinks[source])
                    if (temp.ContainsKey(target.TargetId))
                        result[source].Add(temp[target.TargetId]);
            }

            _linkedItems = result;
            return _linkedItems;
        }

        private void SetTestResults()
        {
            _testCaseResult = new Dictionary<int, ITestCaseResult>();
            var latest = GetTestcaseResultsFromIds(ProjectName, _testCaseTargetIds.ToList());
            if (latest == null)
                return;

            foreach (var result in latest)
            {
                if (!_testCaseResult.ContainsKey(result.TestCaseId))
                {
                    _testCaseResult.Add(result.TestCaseId, result);
                    continue;
                }

                if (_testCaseComparer.Compare(_testCaseResult[result.TestCaseId], result) < 0)
                    _testCaseResult[result.TestCaseId] = result;
            }
        }

        



        #endregion

    }

    public class TestCaseResultsComparer : IComparer<ITestCaseResult>
    {
        public int Compare(ITestCaseResult x, ITestCaseResult y)
        {
            return x.LastUpdated.CompareTo(y.LastUpdated);
        }
    }
}
