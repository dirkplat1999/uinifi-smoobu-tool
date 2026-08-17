# UniFi Access ⇄ Smoobu Guest Access Tool

A Windows 11 desktop app that connects [Smoobu](https://www.smoobu.com) (booking/PMS and guest
messaging) with [Ubiquiti UniFi Access](https://ui.com/access) (door/gate access control):

- Automatically asks arriving guests for a license plate and 4-digit PIN 3 days before arrival
- Parses the guest's reply and provisions a time-boxed UniFi Access visitor credential that is
  only active from 01:00 on the arrival date until midnight the day after departure
- Lets you manage per-apartment automation webhooks (e.g. to greet guests by name on a Chromecast
  or smart display), multilingual message templates, a test mode for safe dry runs, config
  backups, SMTP/webhook error alerting, and in-app auto-updates from GitHub Releases

Built by Dirk Plat (dirkplat1999@gmail.com). Licensed under the
[PolyForm Noncommercial License 1.0.0](LICENSE).

## Project layout

```
UnifiSmoobuTool.sln
src/
  UnifiSmoobuTool.Core/            # domain models, interfaces, business logic (no I/O)
  UnifiSmoobuTool.Infrastructure/  # Smoobu + UniFi Access API clients, persistence, SMTP, updater
  UnifiSmoobuTool.App/             # WPF UI, background sync scheduler, tray icon
tests/
  UnifiSmoobuTool.Core.Tests/
  UnifiSmoobuTool.Infrastructure.Tests/
```

## Building

Requires the .NET 8 SDK.

```
dotnet build UnifiSmoobuTool.sln
dotnet test UnifiSmoobuTool.sln
```

Run the app:

```
dotnet run --project src/UnifiSmoobuTool.App
```

## Configuration

On first launch, open **Settings** and fill in:

- **Smoobu API key** (Settings → Apps → API in your Smoobu account)
- **UniFi Access controller host and API token** (Access console → Settings → API Token; the
  Access API listens on port `12445`)

Nothing is sent to either service until both are configured.
