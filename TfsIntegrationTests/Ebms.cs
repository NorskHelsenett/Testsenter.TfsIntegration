//using System;
//using System.Text;
//using System.Collections.Generic;
//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using Shared.TfsIntegration.Logic;
//using Shared.TfsIntegration.Tools;
//using Microsoft.TeamFoundation.WorkItemTracking.Client;
//using System.Diagnostics;
//using System.Linq;
//using Shared.Common.Resources;

//namespace TfsIntegrationTests
//{
//    [TestClass]
//    public class Ebms
//    {
//        private TFSWorkItemStore _store;

//        [TestInitialize]
//        public void Init()
//        {
//            _store = new TFSWorkItemStore("https://tfs.helsedirektoratet.no/tfs");
//        }

//        [TestMethod]
//        public void CreateSendeFagmeldingTestCases()
//        {
//            int sourcePbi = 86182; 
//            int targetPbi = 86189;

//            var replacements = new Dictionary<string, string>();
//            replacements.Add("Henvisning v1p0", "Svarrapport v1p4");
//            replacements.Add("Henvisning v1.0", "Svarrapport v1.4");
//            replacements.Add(sourcePbi.ToString(), targetPbi.ToString());

//            CopySteps.Clone(sourcePbi, string.Empty, targetPbi, TfsEnvironmentEnum.Test01, replacements);
//        }

//        private void CreateSendeFagmeldingTestCases(int[] masterIds, int pbi, string fagmeldingsfilnavn)
//        {

//        }
//    }
//}
