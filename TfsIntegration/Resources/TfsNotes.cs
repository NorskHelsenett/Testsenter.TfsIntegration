using Newtonsoft.Json;
using TestFramework.Resources;

namespace Shared.TfsIntegration.Resources
{
    public class TfsNotes
    {
        public string RefToScreenshot { get; set; }
        public string AddtionalInformation { get; set; }
        public string Exception { get; set; }

        public TfsNotes() { }

        public TfsNotes(TestStepResultBlob obj)
        {
            AddtionalInformation = obj.AddtionalInformation;
            RefToScreenshot = obj.RefToScreenshot;
            Exception = obj.Exception;
        }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static TfsNotes Deserialize(string s)
        {
            return JsonConvert.DeserializeObject<TfsNotes>(s);
        }
    }
}
