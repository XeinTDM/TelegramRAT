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
2. Configure your bot token and owner ID (see [Configuration](#configuration)).
3. Build the project using Visual Studio or any compatible IDE.
4. Deploy the compiled binary on the target system.
5. Start the application and control it through your Telegram bot.

## Configuration

`TelegramRAT` reads its runtime configuration from **environment variables** or an optional `appsettings.json` file located next to the executable. Environment variables have priority; any value that is missing falls back to `appsettings.json`.

| Setting   | Environment variable       | `appsettings.json` key |
|-----------|----------------------------|------------------------|
| Bot token | `TELEGRAMRAT_BOT_TOKEN`    | `BotToken`             |
| Owner ID  | `TELEGRAMRAT_OWNER_ID`     | `OwnerId`              |

### Example `appsettings.json`

```json
{
  "BotToken": "123456789:telegram-bot-token",
  "OwnerId": "123456789"
}
```

> ⚠️ **Never commit secrets.** Keep your production `appsettings.json` outside of source control and distribute it securely alongside the compiled binary when deploying.

### Setting secrets via environment variables

Environment variables are the recommended option for CI/CD or server deployments because they keep secrets out of the file system.

- **PowerShell**
  ```powershell
  $env:TELEGRAMRAT_BOT_TOKEN = '123456789:telegram-bot-token'
  $env:TELEGRAMRAT_OWNER_ID = '123456789'
  ```

- **Windows Command Prompt**
  ```cmd
  set TELEGRAMRAT_BOT_TOKEN=123456789:telegram-bot-token
  set TELEGRAMRAT_OWNER_ID=123456789
  ```

- **Linux / macOS (Bash/Zsh)**
  ```bash
  export TELEGRAMRAT_BOT_TOKEN=123456789:telegram-bot-token
  export TELEGRAMRAT_OWNER_ID=123456789
  ```

For long-running deployments (e.g., systemd services, container images), store the variables in the service definition or secret manager provided by your hosting environment.

## License

This project is licensed under the [The Unlicense](LICENSE), granting you the freedom to use, modify, and distribute the code as you see fit.

## Disclaimer

> **This tool is for educational and ethical purposes only.**  
> Using this software maliciously or in violation of any laws is strictly prohibited.  
> **The author is not responsible for any misuse or damage caused by this code.**  
> Ensure you have proper authorization before using this tool on any system.

By using this tool, you agree to take full responsibility for your actions.
