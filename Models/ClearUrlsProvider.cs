using System.Collections.Generic;

namespace ClipDumpRe.Models
{
    public class ClearUrlsProvider
    {
        public string UrlPattern { get; set; }
        public bool CompleteProvider { get; set; }
        public List<string> Rules { get; set; }
        public List<string> RawRules { get; set; }
        public List<string> ReferralMarketing { get; set; }
        public List<string> Exceptions { get; set; }
        public List<string> Redirections { get; set; }
        public bool ForceRedirection { get; set; }
    }
}
