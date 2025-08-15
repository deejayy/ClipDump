using System;
using System.Collections.Generic;

namespace ClipDumpRe.Models
{
    public class ClipboardMetadata
    {
        public string Timestamp { get; set; } = string.Empty;
        public ForegroundApplicationInfo ForegroundApplication { get; set; } = new ForegroundApplicationInfo();
        public List<ClipboardFormatMetadata> DetectedFormats { get; set; } = new List<ClipboardFormatMetadata>();
        public List<SavedFormatMetadata> SavedFormats { get; set; } = new List<SavedFormatMetadata>();
    }

    public class ClipboardFormatMetadata
    {
        public string FormatName { get; set; } = string.Empty;
        public long DataSize { get; set; }
        public string SHA256Hash { get; set; } = string.Empty;
    }

    public class SavedFormatMetadata
    {
        public string FormatName { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public long ClipboardDataSize { get; set; }
        public long FileDataSize { get; set; }
        public string SHA256Hash { get; set; } = string.Empty;
        public string RelativeFilePath { get; set; } = string.Empty;
    }
}
