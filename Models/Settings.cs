using System.Text.Json.Serialization;

namespace ClipDumpRe.Models
{
    internal class Settings
    {
        public string WorkingDirectory { get; set; } = @".\data";
        public int MaxFileSizeBytes { get; set; } = 150 * 1024; // 150KB
        public List<ClipboardFormatRule> FormatRules { get; set; } = new List<ClipboardFormatRule>
        {
            // Legacy ignored formats moved from hardcoded HashSet
            new ClipboardFormatRule { Format = "DeviceIndependentBitmap", ShouldIgnore = true },
            new ClipboardFormatRule { Format = "Format17", ShouldIgnore = true }
        };

        // Property to handle kilobyte conversion for UI
        [JsonIgnore]
        public int MaxFileSizeKB
        {
            get => MaxFileSizeBytes / 1024;
            set => MaxFileSizeBytes = value * 1024;
        }
    }
}
