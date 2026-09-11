# LedgerCat

Small-landlord bookkeeping that stays small.

One portable Windows executable. No install, no account, no cloud. Your data
lives in a SQLite file next to the exe, and CSV/JSON import-export means you
can always take it with you.

**Privacy statement:** this program will not transfer any information to other
networked systems unless specifically requested by the user or the person
installing or operating it.

## Download

Signed and unsigned builds live at <https://neko.omegamc.uk/download/>. Every
release is published with a SHA-256 checksum on that page.

## Building from source

Requirements: .NET SDK 8.0 (any OS; Windows targets are enabled in the project
file).

```sh
dotnet publish src/LedgerCat -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true
```

Output lands in `src/LedgerCat/bin/Release/net8.0-windows/win-x64/publish/`.

CI builds the same artifact on every tagged release via GitHub Actions
(`.github/workflows/build.yml`).

## Screens

- **Properties & Units** — properties, units, tenants, contact name and phone, State ID / driver's license, rent and due dates, lease and move-out notes, lease renewal alerts; deletes go to an undoable Deleted view
- **Money In / Out** — rent received and expenses paid, monthly profit, yearly summary, search and category filter, edit any entry, undoable deletes
- **Appointments & Requests** — maintenance and viewing requests, open/done/canceled status, tenant and handyman contacts, undoable deletes
- **Import/Export** — CSV and JSON, both directions, always
- **About** — who made this and why

## Non-goals (v1)

Payments, tenant portal, cloud sync, billing automation. LedgerCat does the
bookkeeping a small landlord actually does, and nothing that requires a
subscription.

## License

MIT — see [LICENSE](LICENSE).
