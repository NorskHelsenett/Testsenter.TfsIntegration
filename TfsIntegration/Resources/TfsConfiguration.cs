
namespace Shared.TfsIntegration.Resources
{
    public class TfsConfiguration
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Domain { get; set; }
        public string TfsUri { get; set; }
        public string TeamProject { get; set; }
        public string TfsProjectUri { get; set; }

        public string GetTfsLink()
        {
            return TfsProjectUri + "/_workitems#_a=edit&id=";;
        }
    }
}
