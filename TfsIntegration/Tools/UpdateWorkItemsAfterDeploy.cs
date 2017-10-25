//using log4net;
//using Shared.Common.Resources;
//using Shared.TfsIntegration.Logic;

//namespace Shared.TfsIntegration.Tools
//{
//    public static class UpdateWorkItemsAfterDeploy
//    {
//        public static void UpdateTfsItemsWithBuildInformation(string tfsUrl, DeploymentInfo deploymentInfo, ILog log)
//        {
//            var buildEnvironment = deploymentInfo.EnvironmentDescription;
//            var buildNr = deploymentInfo.CurrentAssemblyVersion;
//            var store = new TFSWorkItemStore("https://tfs.helsedirektoratet.no:8081/tfs");

//            //"HARE\\Prosjekter\\Forvaltningsteam"
//            var wiCollection = store.GetWorkItemsWithBuildNumber();

//            for (int i = 0; i < wiCollection.Count; i++)
//            {
//                var wi = wiCollection[i];

//                const string environmentKey = "Found in environment";
//                var wiEnv = TfsHelper.TryGetField(wi.Fields, environmentKey, "");
//                var wiBuildNr = TfsHelper.TryGetField(wi.Fields, "NHN Build Number", "");

//                if (TfsHelper.IsBuildNrHigherOrEqual(buildNr, wiBuildNr) &&
//                    EnvironmentNeedsUpdate(buildEnvironment, wiEnv))
//                {
//                    var envEnum = buildEnvironment.AsTfsEnvironmentEnum();
//                    if ((wi.State == "Ready for Test" && envEnum < TfsEnvironmentEnum.Test02)
//                        || wi.State == "Done" && envEnum > TfsEnvironmentEnum.Test01
//                        && EnvironmentNeedsUpdate(buildEnvironment, wiEnv))
//                    {
//                        wi.Open();
//                        wi.Fields[environmentKey].Value = buildEnvironment;
//                        wi.Save();
//                        wi.Close();
//                        log.Info($"Updated TFS item {wi.Id}: {wi.Title} to environment {buildEnvironment}");
//                    }
//                }
//            }
//        }

//        private static bool EnvironmentNeedsUpdate(string buildEnvironment, string workItemEnvironment)
//        {
//            if (string.IsNullOrEmpty(workItemEnvironment)) return true;
//            return workItemEnvironment.AsTfsEnvironmentEnum() < buildEnvironment.AsTfsEnvironmentEnum();
//        }
//    }
//}
