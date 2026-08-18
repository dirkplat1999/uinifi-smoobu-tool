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

Nothing is sent to either service until both API keys below are filled in. Open the app's
**Settings** tab and fill in the **Smoobu** and **UniFi Access** sections.

### Getting your Smoobu API key

1. Log in to your Smoobu account at [login.smoobu.com](https://login.smoobu.com).
2. Open **Settings** (gear icon) → **Apps** → **API**.
3. Copy the API key shown there (Smoobu generates one automatically per account; if none exists
   yet, there's a button to create one).
4. Paste it into **Settings → Smoobu → API key** in the app.

Reference: [Smoobu API docs](https://docs.smoobu.com/).

### Getting your UniFi Access controller host and API token

1. Open the **UniFi Access** web console on your local network (the same device/controller that
   manages your doors).
2. Go to **Settings → Access API** (sometimes labeled **API Token** or **Developer API**,
   depending on your controller's UniFi OS version).
3. Click **Create New**, give it a name, choose a validity period, and select at least the
   `view:visitor`, `edit:visitor`, and `view:space` permissions (needed to create/update visitors
   and read your door/door-group list).
4. Click **Create**, then **Copy API Token** immediately — it's only shown once. Store it
   somewhere safe until you paste it into the app.
5. In the app's **Settings → UniFi Access** section, fill in:
   - **Controller host**: `https://<controller-ip-or-hostname>:12445` (the Access API always
     listens on port `12445`, e.g. `https://192.168.1.1:12445`)
   - **API token**: the token you just copied
   - Leave **"Trust the controller's certificate"** checked unless you've replaced the
     controller's default self-signed certificate with your own CA-signed one.
6. Go to the **Apartments** tab, click **Refresh from Smoobu**, then **Load UniFi Access doors**,
   and assign each apartment the door(s)/door-group(s) its guests should be able to open.

Both the Smoobu API key and the UniFi Access API token are encrypted at rest (Windows DPAPI)
in the local app database — they never leave your machine except in direct calls to Smoobu/UniFi.
