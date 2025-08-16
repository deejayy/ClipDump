namespace ClipDumpRe.Models
{
    public class FormatCache
    {
        public List<SeenFormat> SeenFormats { get; set; } = new List<SeenFormat>();
    }

    public class SeenFormat
    {
        public string FormatName { get; set; } = string.Empty;
        public DateTime FirstSeenDate { get; set; } = DateTime.MinValue;
        public DateTime LastSeenDate { get; set; } = DateTime.MinValue;
        public int SeenCount { get; set; }
    }
}
