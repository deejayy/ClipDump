# ClipDump - A clipboard dumper for Windows built on .NET SDK 9

ClipDump-Re is a background clipboard monitoring application that automatically saves clipboard content to disk whenever you copy something. It runs minimized in the system tray and intelligently handles various data formats including text, images, HTML, and files. The application features configurable format rules, size limits, and destination directories, allowing you to customize which clipboard formats to capture and where to store them. Perfect for keeping a persistent record of your clipboard history or backing up important copied content.

# Features

- **Background clipboard monitoring** - Automatically captures clipboard content without manual intervention
- **System tray integration** - Runs minimized in the system tray with context menu and double-click to restore
- **Multi-format support** - Handles text, images, HTML, RTF, CSV, XML, audio files, and file lists
- **Configurable format rules** - Create custom rules for specific clipboard formats with individual size limits and destination directories
- **Size filtering** - Set global and per-format size limits to avoid saving large unwanted content
- **Automatic file organization** - Save different format types to specific subdirectories
- **Smart file naming** - Timestamped filenames with appropriate extensions based on content type
- **Format exclusion** - Ignore specific clipboard formats you don't want to save
- **Single instance enforcement** - Prevents multiple copies of the application from running
- **Comprehensive logging** - Detailed event logging for monitoring and troubleshooting
- **WPF configuration UI** - Easy-to-use interface for managing settings and format rules
- **Persistent settings** - Configuration saved to JSON file and automatically loaded on startup
