# ClipDump - A clipboard dumper for Windows built on .NET SDK 9

<img width="676" height="398" alt="image" src="https://github.com/user-attachments/assets/698f7bf2-cfb1-44cc-b397-6f85e8354494" />
<img width="676" height="398" alt="image" src="https://github.com/user-attachments/assets/5f943387-0087-423f-a7ff-1005bcfac291" />
<img width="676" height="398" alt="image" src="https://github.com/user-attachments/assets/9e20fcd4-1eda-4bca-b265-129317a4b3ca" />
<img width="699" height="574" alt="image" src="https://github.com/user-attachments/assets/b336bb3e-d247-45b0-8a72-c3aea7974bba" />

ClipDump is a Windows application that monitors clipboard activity and automatically saves clipboard content to disk. It features a comprehensive WPF interface for configuration and runs as a background service with system tray integration. The application captures various clipboard data formats including text, images, HTML, and files, with intelligent rules-based processing for different applications and formats.

# Features

- **Real-time clipboard monitoring** - Uses Windows clipboard format listener for immediate detection of clipboard changes
- **System tray integration** - Runs minimized in the system tray with context menu, enable/disable options, and timed disable functionality
- **Multi-format support** - Handles text (plain text, RTF, HTML), images (PNG, JPEG, BMP, GIF), and file lists with automatic format detection
- **Configurable format rules** - Create custom rules for specific clipboard formats with individual size limits, ignore settings, and destination directories
- **Application-specific rules** - Define rules for specific applications including ignore lists and custom output directories
- **Format discovery and management** - Automatic tracking of seen clipboard formats with ability to create rules from discovered formats
- **Size filtering** - Set global, per-application, and per-format size limits to avoid saving large unwanted content
- **Flexible file organization** - Configurable working directory with support for format-specific and application-specific subdirectories
- **Smart file naming** - Timestamped filenames with appropriate extensions based on content type and format
- **Temporary disable functionality** - Disable clipboard monitoring for set periods (1, 5, 60 minutes) or until manually re-enabled
- **Foreground application detection** - Tracks which application generated clipboard content for enhanced logging and rule application
- **Format cache system** - Persistent tracking of clipboard formats with usage statistics and timestamps
- **Comprehensive logging** - Detailed event logging for monitoring, troubleshooting, and audit trails
- **WPF configuration interface** - Multi-tab interface for managing global settings, format rules, application rules, and seen formats
- **Rule cleanup tools** - Automated detection and removal of duplicate or unnecessary format rules
- **JSON configuration** - Settings automatically saved and persisted between sessions with human-readable format
- **Cross-thread safe operations** - Proper threading implementation for UI updates and clipboard monitoring
- **Minimize to tray behavior** - Window closes to tray instead of exiting, with restore functionality

