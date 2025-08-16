using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Collections.Specialized;
using System.Linq;

namespace ClipDumpRe.Utils
{
    internal static class FileUtils
    {
        public static string SanitizeFileName(string fileName)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '-');
            }
            return fileName.Replace(' ', '-');
        }

        public static string GetFileExtension(string format, object data)
        {
            return format.ToLower() switch
            {
                // Text formats
                "text" or "unicodetext" or "oemtext" or "system.string" => "txt",

                // HTML formats
                "text/html" or "html format" or "text/_moz_htmlcontext" => "html",

                // Rich text and document formats
                "rich text format" => "rtf",
                "csv" => "csv",
                "xml" or "xml spreadsheet" => "xml",
                "text/calendar" => "ics",

                // Image formats
                "bitmap" or "system.drawing.bitmap" or "system.windows.media.imaging.bitmapsource" or "deviceindependentbitmap" => "png",
                "png" or "application/x-moz-nativeimage" => "png",
                "jfif" or "jpeg" => "jpg",
                "gif" => "gif",
                "tiff" => "tiff",
                "format17" => "png", // Windows clipboard format for PNG

                // Vector graphics
                "scalable vector graphics" or "scalable vector graphics for adobe muse" or "image/svg+xml" or "image/x-inkscape-svg" => "svg",
                "encapsulated postscript" => "eps",
                "metafilepict" or "enhancedmetafile" or "system.drawing.imaging.metafile" => "emf",

                // Adobe formats
                "adobe ai3" => "ai",
                "adobe photoshop image" or "photoshop dib layer" or "photoshop dib layer x" => "psd",
                "photoshop text" or "photoshop clip source" => "txt",
                "adobe text engine 2.0" => "txt",
                "portable document format" => "pdf",
                "palette" => "pal",

                // Microsoft Office formats
                "biff12" => "xlsx",
                "biff8" => "xls",
                "biff5" => "xls",

                // File and URL formats
                "filedrop" or "filenamew" or "filename" => "txt",
                "uniformresourcelocatorw" or "application/x-moz-file-promise-url" or "text/x-moz-url-priv" => "txt",
                "application/x-moz-file-promise-dest-filename" => "txt",

                // Audio formats
                "wave" => "wav",

                // Data formats
                "datainterchangeformat" => "dif",
                "embed source" or "object descriptor" => "ole",

                // Specialized formats
                "art::text clipformat" or "art::gvml clipformat" => "pptx",
                "chromium internal source url" => "txt",
                "inshelldragloop" => "txt",

                // Fallback patterns
                _ when format.Contains("image") => "png",
                _ when format.Contains("audio") => "wav",
                _ when format.Contains("text") => "txt",
                _ when format.Contains("html") => "html",
                _ when format.Contains("svg") => "svg",
                _ when format.Contains("pdf") => "pdf",
                _ => "dat"
            };
        }

        public static async Task SaveClipboardDataAsync(string filePath, string format, object data)
        {
            switch (data)
            {
                case string text:
                    await File.WriteAllTextAsync(filePath, text, Encoding.UTF8);
                    break;

                case BitmapSource bitmap:
                    await SaveBitmapSourceAsync(filePath, bitmap);
                    break;

                case System.Drawing.Image image:
                    image.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                    break;

                case StringCollection fileList:
                    var fileListText = string.Join(Environment.NewLine, fileList.Cast<string>());
                    await File.WriteAllTextAsync(filePath, fileListText, Encoding.UTF8);
                    break;

                case byte[] bytes:
                    await File.WriteAllBytesAsync(filePath, bytes);
                    break;

                case Stream stream:
                    using (var fileStream = File.Create(filePath))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        await stream.CopyToAsync(fileStream);
                    }
                    break;

                default:
                    // For other types, try to convert to string representation
                    try
                    {
                        string stringData = data?.ToString() ?? "";
                        await File.WriteAllTextAsync(filePath, $"Format: {format}\nType: {data?.GetType()?.FullName ?? "null"}\nData: {stringData}", Encoding.UTF8);
                    }
                    catch (Exception ex)
                    {
                        await File.WriteAllTextAsync(filePath, $"Format: {format}\nError: Could not serialize data - {ex.Message}", Encoding.UTF8);
                    }
                    break;
            }
        }

        private static async Task SaveBitmapSourceAsync(string filePath, BitmapSource bitmap)
        {
            var encoder = Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".png" => (BitmapEncoder)new PngBitmapEncoder(),
                ".jpg" or ".jpeg" => (BitmapEncoder)new JpegBitmapEncoder(),
                ".gif" => (BitmapEncoder)new GifBitmapEncoder(),
                ".tiff" or ".tif" => (BitmapEncoder)new TiffBitmapEncoder(),
                _ => (BitmapEncoder)new PngBitmapEncoder()
            };

            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var fileStream = File.Create(filePath);
            encoder.Save(fileStream);
            await fileStream.FlushAsync();
        }

        public static long GetDataSize(object data)
        {
            switch (data)
            {
                case string text:
                    return Encoding.UTF8.GetByteCount(text);
                case byte[] bytes:
                    return bytes.Length;
                case Stream stream:
                    return stream.Length;
                case BitmapSource bitmap:
                    return (long)(bitmap.PixelWidth * bitmap.PixelHeight * (bitmap.Format.BitsPerPixel / 8.0));
                case System.Drawing.Image image:
                    // Calculate actual image size by serializing to memory
                    try
                    {
                        using (var ms = new MemoryStream())
                        {
                            image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                            return ms.Length;
                        }
                    }
                    catch
                    {
                        // Fallback: estimate based on image dimensions and bit depth
                        return image.Width * image.Height * 4; // Assume 32-bit RGBA
                    }
                case StringCollection fileList:
                    var fileListText = string.Join(Environment.NewLine, fileList.Cast<string>());
                    return Encoding.UTF8.GetByteCount(fileListText);
                default:
                    // For other types, try to estimate size by converting to string
                    try
                    {
                        string stringData = data?.ToString() ?? "";
                        return Encoding.UTF8.GetByteCount(stringData);
                    }
                    catch
                    {
                        return 0;
                    }
            }
        }
    }
}
