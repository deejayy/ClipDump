using System.Text.Json.Serialization;

namespace ClipDumpRe.Models
{
    internal class ClipboardFormatRule
    {
        public string Format { get; set; } = string.Empty;
        public int MaxSizeBytes { get; set; } = 0; // 0 = no limit
        public bool ShouldIgnore { get; set; } = false;
        public string RelativeDestinationDirectory { get; set; } = string.Empty; // empty = use default

        // Property to handle kilobyte conversion for UI
        [JsonIgnore]
        public int MaxSizeKB
        {
            get => MaxSizeBytes / 1024;
            set => MaxSizeBytes = value * 1024;
        }
    }
}
