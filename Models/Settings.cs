using System.Text.Json.Serialization;
using ClipDumpRe.Services;

namespace ClipDumpRe.Models
{
    internal class Settings
    {
        public string WorkingDirectory { get; set; } = @"%USERPROFILE%\Documents\Clipboard Dumps";
        public int MaxFileSizeBytes { get; set; } = 8192 * 1024; // 8MB
        public int MinClipboardDataSizeBytes { get; set; } = 4; // 4 bytes minimum
        public bool StartWithWindows { get; set; } = false;
        public bool UseTimestampSubdirectories { get; set; } = false;
        public List<ClipboardFormatRule> FormatRules { get; set; } = new List<ClipboardFormatRule>
        {
            // Legacy ignored formats moved from hardcoded HashSet
            new ClipboardFormatRule { Format = "DeviceIndependentBitmap", ShouldIgnore = true },
            new ClipboardFormatRule { Format = "Format17", ShouldIgnore = true }
        };

        public List<ApplicationRule> ApplicationRules { get; set; } = new List<ApplicationRule>
        {
            // Common applications that might generate unwanted clipboard content
            new ApplicationRule { ExecutableFileName = "1password.exe", ShouldIgnore = true },
            new ApplicationRule { ExecutableFileName = "keepassxc.exe", ShouldIgnore = true },
        };

        // Property to handle kilobyte conversion for UI
        [JsonIgnore]
        public int MaxFileSizeKB
        {
            get => MaxFileSizeBytes / 1024;
            set => MaxFileSizeBytes = value * 1024;
        }

        // Property to handle byte conversion for UI
        [JsonIgnore]
        public int MinClipboardDataSizeValue
        {
            get => MinClipboardDataSizeBytes;
            set => MinClipboardDataSizeBytes = value;
        }
    }
}
