# ClipDump - A clipboard dumper for Windows built on .NET SDK 9

<img width="790" height="579" alt="image" src="https://github.com/user-attachments/assets/ad3bbc7f-19e3-450a-a2ab-61a1c93ae4e1" />
<img width="790" height="579" alt="image" src="https://github.com/user-attachments/assets/5d0a77c4-63b7-49fd-995d-e6feffbf7042" />
<img width="790" height="579" alt="image" src="https://github.com/user-attachments/assets/ed4442c7-ca82-4dc9-8cca-bb8320ea6149" />
<img width="790" height="579" alt="image" src="https://github.com/user-attachments/assets/0eb02638-1d22-4bc8-bb2e-2c458eeaf0d1" />


ClipDump is a Windows application that monitors clipboard activity and automatically saves clipboard content to disk. It features a comprehensive WPF interface for configuration and runs as a background service with system tray integration. The application captures various clipboard data formats including text, images, HTML, and files, with intelligent rules-based processing for different applications and formats.

# Features

- **Real-time clipboard monitoring** - Uses Windows clipboard format listener for immediate detection of clipboard changes
- **System tray integration** - Runs minimized in the system tray with context menu, enable/disable options, and timed disable functionality
- **Multi-format support** - Handles text (plain text, RTF, HTML), images (PNG, JPEG, BMP, GIF), and file lists with automatic format detection
- **Configurable format rules** - Create custom rules for specific clipboard formats with individual size limits, ignore settings, and destination directories
- **Application-specific rules** - Define rules for specific applications including ignore lists and custom output directories
- **Running application detection** - Automatically detect and add rules for currently running applications with visible windows
- **Format discovery and management** - Automatic tracking of seen clipboard formats with ability to create rules from discovered formats
- **Size filtering** - Set global, per-application, and per-format size limits to avoid saving large unwanted content
- **Flexible file organization** - Configurable working directory with support for format-specific and application-specific subdirectories
- **Working directory integration** - One-click Explorer access to output directories with full path resolution and environment variable support
- **Smart file naming** - Timestamped filenames with appropriate extensions based on content type and format
- **Enhanced temporary disable functionality** - Disable clipboard monitoring for set periods (1, 5, 60 minutes) or until manually re-enabled with countdown timer
- **Foreground application detection** - Tracks which application generated clipboard content for enhanced logging and rule application
- **Format cache system** - Persistent tracking of clipboard formats with usage statistics, timestamps, and backup functionality
- **History management** - Clear format history with automatic backup creation and restore capabilities
- **Comprehensive logging** - Detailed event logging for monitoring, troubleshooting, and audit trails
- **WPF configuration interface** - Multi-tab interface for managing global settings, format rules, application rules, and seen formats
- **Automated rule cleanup tools** - Detection and removal of duplicate, unnecessary, or redundant format and application rules
- **Windows startup integration** - Configure ClipDump to automatically start with Windows login
- **About dialog** - Version information and application details accessible from the main interface
- **JSON configuration** - Settings automatically saved and persisted between sessions with human-readable format
- **Cross-thread safe operations** - Proper threading implementation for UI updates and clipboard monitoring
- **Minimize to tray behavior** - Window closes to tray instead of exiting, with restore functionality

