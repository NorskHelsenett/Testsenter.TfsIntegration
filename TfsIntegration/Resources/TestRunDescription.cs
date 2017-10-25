
namespace Shared.TfsIntegration.Resources
{
    public class TestRunDescription
    {
        public int Passed { get; set; }
        public int Id { get; set; }
        public int Failed { get; set; }
        public string IterationPath { get; set; }

        public static string NoIteration = "ingen iteration satt";
    }
}
