namespace ClipDumpRe.Models
{
    public class ForegroundApplicationInfo
    {
        public string ProcessName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public string WindowClass { get; set; } = string.Empty;
    }
}
