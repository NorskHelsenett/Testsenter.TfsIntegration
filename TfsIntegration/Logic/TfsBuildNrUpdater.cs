//using System;
//using Microsoft.TeamFoundation.WorkItemTracking.Client;
//using Shared.Common.Logic;
//using Shared.Common.Resources;
//using static System.Int32;

//namespace Shared.TfsIntegration.Logic
//{
//    public class TfsBuildNrUpdater : IDisposable
//    {
//        private readonly TfsBase _tfsBase;

//        internal TfsBuildNrUpdater(TfsBase tfsBase)
//        {
//            _tfsBase = tfsBase;
//        }

//        internal void UpdateTfsWorkItemsAfterNewDeploy(string environmentToUpdateDescription, string buildNrDeployed)
//        {
//            var buildEnvironment = environmentToUpdateDescription;
//            var buildNr = buildNrDeployed;

//            var wiCollection = GetWorkItemsWithBuildNumber(_tfsBase.WorkItemStore);

//            for (int i = 0; i < wiCollection.Count; i++)
//            {
//                var workItem = wiCollection[i];

//                workItem.SyncToLatest();

//                const string environmentKey = "Found in environment";
//                var wiEnv = TfsBase.TryGetField(workItem.Fields, environmentKey, "").AsTfsEnvironmentEnum();
//                var wiBuildNr = TfsBase.TryGetField(workItem.Fields, "NHN Build Number", "");

//                //TODO make sure "empty" environment updates to "Utvikling"
//                if (IsBuildNrHigherOrEqual(buildNr, wiBuildNr) &&
//                    EnvironmentNeedsUpdate(buildEnvironment, wiEnv))
//                {
//                    var envEnum = buildEnvironment;
//                    if ((workItem.State == "Ready for Test" && envEnum < TfsEnvironment.Test02)
//                        || workItem.State == "Done" && envEnum > TfsEnvironment.Test01
//                        && EnvironmentNeedsUpdate(buildEnvironment, wiEnv))
//                    {
//                        workItem.Open();
//                        workItem.Fields[environmentKey].Value = buildEnvironment.ToDescription();
//                        var valid = workItem.Validate();
//                        if (valid.Count > 0)
//                        {
//                            _tfsBase.Logger.Error($"Could not update TFS item {workItem.Id}: {workItem.Title} to environment {buildEnvironment}. Got validation error on {valid.Count} field(s)");
//                            workItem.Close();
//                            continue;
//                        }

//                        workItem.Save();
//                        workItem.Close();
//                        _tfsBase.Logger.Info($"Updated TFS item {workItem.Id}: {workItem.Title} to environment {buildEnvironment}");
//                    }
//                }
//            }
//        }

//        public static bool IsBuildNrHigherOrEqual(string item1, string item2)
//        {
//            if (item1 == item2) return true;

//            var item1Split = item1.Split('.');
//            var item2Split = item2.Split('.');
//            if (item2Split.Length < 3) return false;

//            for (var i = 0; i < 4; i++)
//            {
//                try
//                {
//                    var item1Nr = Parse(item1Split[i]);
//                    var item2Nr = Parse(item2Split[i]);
//                    if (item1Nr == item2Nr)
//                        continue;

//                    return item1Nr > item2Nr;
//                }
//                catch (Exception)
//                {
//                    return false;
//                }

//            }

//            return true;
//        }

//        private static bool EnvironmentNeedsUpdate(TfsEnvironment buildEnvironment, TfsEnvironment workItemEnvironment)
//        {
//            return workItemEnvironment == TfsEnvironment.Ugyldig || workItemEnvironment < buildEnvironment;
//        }

//        private WorkItemCollection GetWorkItemsWithBuildNumber(WorkItemStore workItemStore)
//        {
//            var wiql = $@"SELECT [System.Id],[System.WorkItemType],[System.Title],[System.State],[System.AreaPath],[System.IterationPath],[System.Tags] FROM WorkItems WHERE [System.State] <> 'Removed' AND [Helsedir.NHNBuildNumber] <> '' AND ( [System.WorkItemType] = 'Change Request' OR [System.WorkItemType] = 'Bug' OR [System.WorkItemType] = 'Product Backlog Item' ) ORDER BY [System.WorkItemType]";
//            return workItemStore.Query(wiql);
//        }

//        public void Dispose()
//        {
            
//        }
//    }
//}
