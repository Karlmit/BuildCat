<p align="center">
  <img src="assets/buildcat-logo.png" alt="BuildCat logo" width="128" height="128">
</p>

# BuildCat

BuildCat is a tiny Windows tray app that watches the latest GitHub Actions run for a repository and changes a cat icon color:

- Yellow: queued or in progress
- Green: completed successfully
- Red: completed with failure, cancelled, timed out, or action required
- Gray: unknown, network error, missing config, rate limited, or no workflow runs

BuildCat does not ship with a default monitored repository. On first run, open `Settings` and enter the GitHub owner and repo you want to watch.

## Requirements

- Windows 10 or Windows 11
- .NET 8 SDK for development

The app is a WinForms tray application. It starts directly in the Windows notification area and does not open a main window.

## Run from source

```powershell
dotnet run --project .\BuildCat\BuildCat.csproj
```

## Settings

Right-click the tray icon and choose `Settings`.

Settings are stored locally at:

```text
%AppData%\BuildCat\settings.json
```

The GitHub token is optional. Public repositories can usually be queried without one, but a token helps avoid rate limits and is needed for private repositories.

Polling settings:

- `Poll interval when not building`: used for green, red, and gray states. Default: `30` seconds.
- `Poll interval while building`: desired yellow-state interval. Default: `10` seconds.

While yellow, BuildCat still uses GitHub's rate-limit headers as a safety rail. If the configured yellow interval would burn through the hourly request bucket too quickly, BuildCat automatically waits longer.

Recommended values:

- With a token: `30` seconds when not building, `10` seconds while building.
- Without a token: `60` seconds or more while building, because anonymous GitHub REST API calls are much more limited.

The tray menu shows current request budget and reset time, for example:

```text
GitHub auth: Token active (4900/5000 left, resets 14:32)
Polling: Green 30s, Yellow 10s
Next auto-check: ~10s (yellow)
```

## GitHub token

To create a fine-grained token:

1. Open GitHub settings.
2. Go to Developer settings.
3. Choose Personal access tokens, then Fine-grained tokens.
4. Generate a new token.
5. Select the target repository.
6. Grant read-only access to Actions metadata.
7. Copy the token into BuildCat settings.

BuildCat sends the token as:

```text
Authorization: Bearer TOKEN
```

Do not commit or share the settings file if it contains a token.

## Start with Windows

Use the tray menu `Start with Windows` toggle or the same checkbox in Settings.

BuildCat writes a per-user startup entry under:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

This does not require admin rights.

## Publish a single-file executable

```powershell
dotnet publish .\BuildCat\BuildCat.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

If your machine only has offline NuGet sources configured, add NuGet.org for the restore:

```powershell
dotnet publish .\BuildCat\BuildCat.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:RestoreSources=https://api.nuget.org/v3/index.json
```

The executable will be under:

```text
BuildCat\bin\Release\net8.0-windows\win-x64\publish\
```

Run `BuildCat.exe` from that folder. If you enable Start with Windows, enable it after placing the executable where you want it to live.

## Troubleshooting

- Gray icon: check the owner/repo settings, internet connection, GitHub API status, and whether the repository has workflow runs.
- Gray icon with public repo: you may have hit GitHub's unauthenticated API rate limit. Add a token and wait for the limit to reset.
- No notification: Windows notification settings may suppress tray balloon notifications. The tray icon still updates.
- Start with Windows does not launch: move the executable to its final location, run it, then toggle Start with Windows off and on again.
- Private repo fails: create a fine-grained token with access to that repository and paste it into Settings.

## Architecture

- `GitHubActionsClient`: calls the GitHub REST API.
- `BuildStatusService`: maps GitHub run status to BuildCat state.
- `TrayIconManager`: owns the notify icon, context menu, tooltip, and generated cat icons.
- `NotificationService`: shows Windows tray notifications for start and completion events.
- `SettingsService`: loads and saves JSON settings, including corruption recovery.
- `SettingsForm`: small WinForms settings dialog.
- `Program`: starts the tray app.
