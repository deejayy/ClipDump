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
                "text" or "unicodetext" or "oemtext" or "system.string" => "txt",
                "html format" => "html",
                "rich text format" => "rtf",
                "csv" => "csv",
                "xml" => "xml",
                "bitmap" or "system.drawing.bitmap" => "png",
                "png" => "png",
                "jfif" or "jpeg" => "jpg",
                "gif" => "gif",
                "tiff" => "tiff",
                "filedrop" or "shell idlist array" => "txt",
                "wave" => "wav",
                _ when format.Contains("image") => "png",
                _ when format.Contains("audio") => "wav",
                _ when format.Contains("text") => "txt",
                _ when format.Contains("html") => "html",
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
