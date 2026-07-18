# Axis Sphere51 Data Server

A small, fast **ASP.NET Core (.NET 9) Web API** that parses legacy **Sphere 0.51a**
("The Abyss") **text scripts** and serves the data (items, NPCs, areas, categories)
over HTTP/JSON — so a GM/admin can browse everything through Axis (or any client)
**without needing the raw script files locally**.

* Boxed, proven stack: ASP.NET Core Minimal API + Swagger/OpenAPI.
* Same parser semantics as the Axis Sphere51 desktop tool (bare-hex `[0000]` blocks,
  `spherechar*` → NPCs, `DUPELIST`/`DUPEITEM`, hex-width tolerance).
* Fully covered by automated tests (`dotnet test` → 30 tests, unit + HTTP integration).

## Requirements
* .NET 9 SDK

## Configure
Edit `AxisSphere51.Server/appsettings.json` → `Scripts`:

```json
"Scripts": {
  "BaseDirectory": "D:\\SphereX64-Abyss\\GraySvr\\x64Release",
  "ItemFiles": [ "sphereitem.scp", "sphereitem2.scp" ],
  "CharFiles": [ "spherechar.scp", "spherechar2.scp" ],
  "MapFiles":  [ "spheremap.scp", "spheremap3.scp" ]
}
```
* `*char*.scp` files are parsed as **NPCs**, everything else as **items**.
* File names may be absolute or relative to `BaseDirectory`.

## Run (standalone executable)
A self-contained Windows executable is published to **`dist/`** — no .NET install needed:

```
dist\AxisSphere51.Server.exe      (double-click, or run from a console)
```

* Config lives right next to it: **`dist\appsettings.json`** — edit `Scripts.BaseDirectory`
  and the file lists there, then restart the exe.
* Binds to the address in `appsettings.json` → `Urls` (default `http://0.0.0.0:5099`,
  reachable from other machines on the LAN).
* Open **http://localhost:5099/swagger** for the interactive API (Swagger UI).
* Scripts are parsed once at startup and cached; `POST /api/reload` re-reads them.

To (re)build the single-file executable (one `AxisSphere51.Server.exe` + editable `appsettings.json`,
no loose DLLs, no .NET install needed):
```
dotnet publish AxisSphere51.Server -p:PublishProfile=win-x64
```
Output goes to `AxisSphere51.Server/bin/Publish/`. For development you can also just
`dotnet run --project AxisSphere51.Server`.

## Logging
The server writes a daily log next to the exe: **`logs/server-YYYY-MM-DD.log`**. It records the
server start (with item/NPC counts and loaded files), server stop, every authentication attempt
(client IP + who logged in / failed + PLEVEL), and every API request (IP + user + route + status,
incl. 401/403). Example:
```
[START] Server started on http://0.0.0.0:5099 — 17402 items, 380 npcs, ... ; 1 accounts (min plevel 2)
[AUTH ] 127.0.0.1 login OK: gm (plevel 4)
[REQ  ] 127.0.0.1 user=gm GET /api/items?limit=200 -> 200
[AUTH ] 203.0.113.7 login FAILED for 'bob' (bad login or password)
```

## Endpoints
| Method | Route | Description |
|--------|-------|-------------|
| GET  | `/health` | Liveness probe |
| GET  | `/api/stats` | Counts + loaded files |
| GET  | `/api/items?search=&category=&subsection=&offset=&limit=` | Items (paged) |
| GET  | `/api/items/{id}` | One item (exact id or hex-tolerant, e.g. `2` == `0002`) |
| GET  | `/api/npcs?search=&category=&offset=&limit=` | NPCs (paged) |
| GET  | `/api/npcs/{id}` | One NPC |
| GET  | `/api/areas?search=&offset=&limit=` | Areas / rooms (flat list) |
| GET  | `/api/regions?search=&map=` | Travel regions: areas with nested rooms + standalone rooms, full geometry (rects, centre point, Z, map) |
| GET  | `/api/spells?search=` | Parsed `[SPELL n]` blocks (id, defname, name, resources) |
| GET  | `/api/categories?kind=item\|npc` | Category → subsection tree with counts |
| POST | `/api/reload` | Re-parse the scripts from disk |

`/api/regions` and `/api/spells` back the desktop **Travel** and **Misc** tabs when a profile is a
Web Profile — the client maps their JSON onto the exact same models the local `ScriptParser`
produces, so both tabs behave identically whether data comes from local scripts or the server.
Spell files are configured under `Scripts.SpellFiles` (default `spherespell.scp`).

CORS is open (any origin) so the desktop tool / a browser can call it directly.

## Tests
```
dotnet test
```
Tests use throwaway fixture scripts (no real shard needed) and boot the real API host
via `WebApplicationFactory`.

## How it connects to Axis
Axis profiles have a **Web Profile** (`IsWebProfile` + `URL`) concept. The desktop tool's
`WebDataService` (`Axis2.WPF/Services/WebDataService.cs`) loads items/NPCs from this server's
`/api/*` endpoints — paging through `/api/items` and `/api/npcs`, checking `/api/stats`, and
sending the profile's login/password as HTTP Basic auth. When a profile is marked as a Web
Profile and its URL points here, GMs work with zero local scripts.

This server folder lives alongside the desktop tool (`Axis2/AxisSphere51Server`) so the two
ship and version together.
