namespace Shared.TfsIntegration.Tools
{
    public class StepFailInformation
    {
        public StepFailInformation(string comment)
        {
            Comment = comment;
        }

        public string Comment { get; set; }
    }
}
