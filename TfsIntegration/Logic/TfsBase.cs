using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.TestManagement.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Shared.Common.Logic;
using Shared.Common.Resources;
using IValidTfsEnvironmentProvider = Shared.Common.Interfaces.IValidator;
using TfsConfiguration = Shared.TfsIntegration.Resources.TfsConfiguration;

namespace Shared.TfsIntegration.Logic
{
    public abstract class TfsBase
    {
        #region Properties
        protected System.Net.ICredentials Credentials;
        internal TfsConfiguration Config;
        internal ILog Logger;

        private TfsTeamProjectCollection _tfsTeamProjectCollection;

        private TfsTeamProjectCollection TfsTeamProjectCollection
        {
            get
            {
                if (_tfsTeamProjectCollection != null)
                    return _tfsTeamProjectCollection;

                _tfsTeamProjectCollection = new TfsTeamProjectCollection(new Uri(Config.TfsUri), Credentials);
                _tfsTeamProjectCollection.EnsureAuthenticated(); 
                return _tfsTeamProjectCollection;
            }
        }

        private ITestManagementService _testManagementService;
        internal ITestManagementService TestManagementService => _testManagementService ??
                                                                   (_testManagementService = TfsTeamProjectCollection.GetService<ITestManagementService>());

        internal ITestManagementTeamProject TestManagementTeamProject => TestManagementService.GetTeamProject(Config.TeamProject);

        private ICommonStructureService4 _css;
        internal ICommonStructureService4 CommonStructureService => _css ?? (_css = TfsTeamProjectCollection.GetService<ICommonStructureService4>());

        internal WorkItemStore WorkItemStore => TfsTeamProjectCollection.GetService<WorkItemStore>();
        protected IValidTfsEnvironmentProvider ValidTfsEnvironmentProvider;

        #endregion

        protected TfsBase(TfsConfiguration tfsConfiguration, ILog logger, IValidTfsEnvironmentProvider validTfsEnvironment)
        {
            Config = tfsConfiguration;
            Logger = logger;
            Credentials = new System.Net.NetworkCredential(tfsConfiguration.Username, tfsConfiguration.Password, tfsConfiguration.Domain);
            ValidTfsEnvironmentProvider = validTfsEnvironment;
        }

        public ITestPlan GetTestPlan(int testplanId)
        {
            return TestManagementTeamProject.TestPlans.Find(testplanId);
        }

        internal string GetEnvironment(ITestCase testcase, int suiteId, ref Dictionary<int, string> cachedSuiteNames)
        {
            string environment = GetCustomFieldValue(testcase, "Found in environment");
            if (!string.IsNullOrEmpty(environment))
                return environment;

            if (cachedSuiteNames.ContainsKey(suiteId))
                return cachedSuiteNames[suiteId];

            var testSuite = TryGetTestSuite(suiteId);
            if (testSuite == null)
                return null;

            cachedSuiteNames.Add(suiteId, ValidTfsEnvironmentProvider.IsThisValid(testSuite.Title) ? testSuite.Title : string.Empty);
            return cachedSuiteNames[suiteId];
        }

        private ITestSuiteBase TryGetTestSuite(int suiteId)
        {
            try
            {
                return TestManagementTeamProject.TestSuites.Find(suiteId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static string GetCustomFieldValue(ITestCase testCase, string fieldName)
        {
            try
            {
                return testCase.CustomFields[fieldName].Value.ToString();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        internal ITestPointCollection GetAllTestPointsInPlan(int testplanId, IEnumerable<int> applicableTestCaseIds = null)
        {
            var standardWigl = "SELECT * FROM TestPoint";

            if (applicableTestCaseIds != null && applicableTestCaseIds.Any())
            {
                var csv = string.Join(",", applicableTestCaseIds);
                standardWigl += $"  WHERE TestCaseID in ({csv})";
            }

            var plan = GetTestPlan(testplanId);
            if (plan == null) { 
                throw new Exception("Du har muligens ikke tilgang til et av TFS områdene du prøver å nå eller har skrevet inn feil plan id(plan == null)");
            }
            var result = plan.QueryTestPoints(standardWigl);
            return result;
        }

        internal DateTime GetNorwegianTime(DateTime d)
        {
            if (d.Kind != DateTimeKind.Utc)
                d = d.ToUniversalTime();

            return d.ToNorwegianTime();
        }

        internal string GetWiqlWhereClause(IEnumerable<int> workItemIds, string idField = "[System.Id]")
        {
            var s = new StringBuilder();
            var count = 0;

            foreach (var id in workItemIds)
            {
                s.Append(idField + $" = '{id}'");
                if (++count == workItemIds.Count())
                    break;

                s.Append(" OR ");
            }

            return s.ToString();
        }

        internal ITestCase GetTestCase(int testCaseId)
        {
            return TestManagementTeamProject.TestCases.Find(testCaseId);
        }

        internal WorkItemCollection GetIteration(string iterationPathName)
        {
            string query = $"SELECT * FROM WorkItems WHERE [System.IterationPath] = '{iterationPathName}' ORDER BY [System.WorkItemType], [System.Id]";
            return WorkItemStore.Query(query);
        }

        public static T TryGetField<T>(FieldCollection fields, string key, T @default = default(T))
        {
            try
            {
                if (!fields.Contains(key))
                    return @default;

                return (T)Convert.ChangeType(fields[key].Value, typeof(T));
            }
            catch (Exception)
            {
                return @default;
            }
        }

        internal int GetNumberOfAutomatedTests(int testplanId, string state)
        {
            var plan = TestManagementTeamProject.TestPlans.Find(testplanId);
            return GetNumberOfAutomatedTests(plan, state);
        }

        internal int GetNumberOfAutomatedTests(ITestPlan plan, string state)
        {
            var collection = plan.QueryTestPoints("SELECT * From TestPoint");

            var result = new HashSet<int>();
            foreach (var point in collection)
            {
                if (result.Contains(point.TestCaseId))
                    continue;

                var testCase = point.TestCaseWorkItem;
                var automationStatus = testCase.CustomFields.TryGetById(10030);
                if (testCase.State != state)
                    continue;

                var isAutomatedTest = automationStatus != null && automationStatus.Value.Equals("Planned");
                if (!isAutomatedTest)
                    continue;

                result.Add(point.TestCaseId);
            }

            return result.Count;
        }

        public WorkItemCollection GetTestCasesForWorkitem(int workitemId, string environmentDescription)
        {
            var wiql = $@"SELECT [System.Id],[System.WorkItemType],[System.Title],[System.State],[System.AreaPath],[System.IterationPath],[System.Tags] FROM WorkItemLinks WHERE [Source].[System.Id] = '{workitemId}' AND ( [Target].[System.WorkItemType] = 'Test Case' AND [Target].[System.State] NOT CONTAINS 'Closed' AND [Target].[HN.Found.In.Environment] = '{environmentDescription}' ) ORDER BY [System.WorkItemType] mode(MustContain)";

            var query = new Query(WorkItemStore, wiql);
            var ids = query.RunLinkQuery().Where(x => x.TargetId != workitemId).Select(x => x.TargetId);
            var testcases = GetWorkitemsFromIds(ids.ToList());

            return testcases;
        }

        public WorkItemCollection GetRegressionTestCasesByServices(string[] services, string environmentDescription)
        {
            var servicesCsv = string.Join(",", services.Select(str => $"'{str}'"));
            var wiql = @"SELECT [System.Id],[System.WorkItemType],[System.Title],[System.State],[System.AreaPath],[System.IterationPath],[System.Tags] "+
                "FROM WorkItems "+
                $"WHERE [System.WorkItemType] = 'Test Case' AND [System.State] NOT CONTAINS 'Closed' AND [HN.Governance] = 'Ja' AND [HN.Found.In.Environment] = '{environmentDescription}' AND [HN.Service] IN ({servicesCsv}) "+
                "ORDER BY [System.WorkItemType]";

            return WorkItemStore.Query(wiql);
        }

        internal WorkItemCollection GetWorkitemsFromIds(List<int> workitemIds)
        {
            if (workitemIds == null || !workitemIds.Any())
                return null;

            var whereClause = GetWiqlWhereClause(workitemIds);

            var wiql = $@"SELECT [System.Id],[System.WorkItemType],[System.Title],[System.State],[System.AreaPath],[System.IterationPath],[System.Tags] FROM WorkItems WHERE {whereClause} ORDER BY [System.WorkItemType]";

            return WorkItemStore.Query(wiql);
        }
    }
}
