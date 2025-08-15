namespace ClipDumpRe.Models
{
    public class RunningApplication
    {
        public string ExecutableName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public int ProcessId { get; set; }

        public string DisplayName => $"{ExecutableName} ({ProcessName}) [{WindowTitle}]";
    }
}
