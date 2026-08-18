# Changelog

## 1.1.0

- Default message templates for English, Dutch, German, and French, seeded automatically on first
  run for the Request, Clarification, and Confirmation message kinds.
- The app now auto-replies to guests: if a reply's license plate or PIN can't be read clearly, it
  asks the guest to resend it; if it's read clearly, it sends a short "thanks, got it" confirmation.
- New Settings toggle to turn off all automated guest messaging (arrival requests, clarification
  requests, confirmations) while keeping the rest of the automation running.
- Easier language picker on the Message Templates screen (dropdown for common languages, or a
  custom code) instead of typing a code from scratch, plus a template "kind" selector.
- "Check for Updates" now shows the release notes for the new version, not just the version number.

## 1.0.1

- Clearer error message when GitHub's update-check rate limit is hit, explaining it's shared per
  network and temporary instead of showing the raw HTTP error.

## 1.0.0

- First-launch license agreement gate; declining exits the app without opening the main window.
- Settings to keep running in the background when closed (or exit outright), and to auto-start
  with Windows.
- Test-connection buttons for Smoobu, UniFi Access, SMTP, and individual error webhooks.
- Guest language shown on the Dashboard; all displayed dates switched to dd-MM-yyyy.

## 0.1.0 - 0.6.0

Initial build: Smoobu <-> UniFi Access guest-messaging and access-provisioning automation, message
templates, per-apartment automation webhooks, test mode, backup/restore, SMTP and webhook error
alerts, GitHub-based auto-update, in-app uninstall, Smoobu HMAC authentication, and an app icon.
