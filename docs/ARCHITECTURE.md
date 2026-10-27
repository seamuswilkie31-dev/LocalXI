# How LocalXI works

LocalXI glues together existing open-source pieces. It never modifies them; it downloads pinned builds,
configures them from the outside, and starts and stops them in the right order.

## Folders

```
%LOCALAPPDATA%\LocalXI\
  config.json             launcher settings (mode, game path, ports, generated DB password, login)
  mariadb-client.cnf      credentials for MariaDB's command-line tools (keeps the password off command lines)
  downloads\              verified downloads, kept so reinstalling doesn't download again
  runtime\.components\    one marker per installed component (version + hash)
  runtime\mariadb\        portable MariaDB
  runtime\server\         LandSandBoat build (+ navmeshes\, ximeshes\, log\)
  runtime\ashita\         Ashita v4, with bootloader\xiloader.exe added
  runtime\python\         private Python 3.13 for converting player data (+ python-packages\, python-packaging\)
  runtime\addons\         downloaded addons, one folder each laid out like Ashita's (copied into Ashita at launch)
  database\               MariaDB data folder: the live world
  logs\                   one log per program
  Texture Packs\          XIPivot texture packs, one folder per pack (not in Documents: they can be gigabytes)

Documents\LocalXI\
  Saves\                  exported .xisave files
  Backups\                automatic backups (auto-*, before-import-*, manual-*)
  Screenshots\            the game's own screenshot folder
```

`portable.txt` next to `LocalXI.exe` moves all of this to `LocalXI-data\` beside the exe.

## Components (`components.json`)

Every download is pinned to a version and SHA-256 hash and checked before use. Kinds:

- **zip**: replaces its folder on upgrade, carrying over `preserve` subfolders (logs, meshes, Ashita config).
- **zip-overlay**: extracted on top of another component's folder (meshes into the server folder).
- **file**: one file copied in (xiloader into Ashita's `bootloader\`).
- **installer**: a Microsoft installer run elevated only when its `detect` check fails (Visual C++ runtime).

Components have a role: `client` components are needed on every PC; `server` components only on PCs that
run a server (Solo and Host). Join mode needs about 20 MB.

The LandSandBoat server and meshes are built by `.github/workflows/build-server.yml`, because LandSandBoat
doesn't publish Windows binaries. See [RELEASING.md](RELEASING.md).

## The database

MariaDB runs from the runtime folder as an ordinary process, not a Windows service. On first run
`mariadb-install-db` creates `database\` with a random root password. The launcher then sets
`bind-address=127.0.0.1` (only this PC can reach it, even when hosting) and LandSandBoat's expected
`utf8mb4_general_ci` collation, and picks a free port starting at 3307 so it never collides with another MySQL.

Game data is loaded the way LandSandBoat's `tools/dbtool.py` does it: every `sql/*.sql` file in alphabetical
order with `triggers.sql` last, each piped through the `mariadb` command-line client. The client is needed
rather than a driver because the files use client features such as `DELIMITER`. A `localxi_meta` table records
that the import finished.

### Converting player data between server versions

A newer LandSandBoat can change the player tables' layout. LandSandBoat ships Python migrations for that
(`tools/migrations`): each one checks whether it's needed (`needs_to_run`) and converts the tables (`migrate`).
`MigrationRunner` runs them like `tools/dbtool.py` does (check every migration, then run the needed ones in
file-name order) through a small driver script on a private Python 3.13 with only the MariaDB connector and the
one package it needs, all pinned components. Credentials go to the driver through environment variables.

`LauncherService` runs it after reloading game data for a newer server, and straight after loading a save (which
may come from an older server); `migrated_server` in `localxi_meta` records the server version the player
tables were last converted for, so it only runs when that changes. A new world is created in the current layout,
so it's marked as converted without running. If any migration is still needed after running, the server isn't
started: the update or import backup is on the Saves page and the failing migration on the Logs page. Problems
with single characters' data (`migration_errors.log`) are reported as warnings, as dbtool does.

## Starting the server

`LsbServer` starts LandSandBoat's programs in the order of its docker-compose file, waiting for each to listen
before starting the next:

| Program | Role | Listens on |
|---|---|---|
| `xi_world` | message broker the others connect to | TCP 54003 |
| `xi_connect` | login / lobby | TCP 54001, 54230, 54231 |
| `xi_search` | search, auction house | TCP 54002 |
| `xi_profile` | PlayOnline profile (if present) | TCP 51220 |
| `xi_map` | the game world | UDP 54230 |

Settings are passed as environment variables, which LandSandBoat applies over its `settings/*.lua`
(`XI_<FILE>_<SETTING>`, e.g. `XI_NETWORK_SQL_PASSWORD`), so the launcher never edits the server's files.
Note that LandSandBoat turns all-digit values into numbers, which is why generated passwords always start with
a letter.

A program counts as started once its ports are open **and** it has logged LandSandBoat's
"The <name>-server is ready to work" line. Ports open long before that: the map server spends anywhere from a
few seconds to over a minute loading scripts, items and zones. If a future version stops printing the line,
open ports plus a grace period (20 s, or 4 minutes for the map server) is enough to carry on.

Consoles are hidden and output goes to `logs\` and the Logs page. Stopping sends Ctrl+C to each program (so
the map server saves characters), waits, and only then force-closes, in reverse order. A fully started server
stops cleanly in a second or two; a map server still starting up ignores Ctrl+C and is force-closed after 20 s.

In **modern** mode `xi_map` runs with `--lazy` (zones load when someone enters them), which starts in seconds.
**Classic** mode loads every zone at start-up: many `era/lua` overrides target zone scripts (notorious monster
respawns, shop stock, fame checks), and with lazy zones those scripts don't exist yet when the overrides are
applied, so 114 of them were silently skipped. Loading every zone takes the map server to about 2.8 GB of RAM.

## Game era

`EraPresets` maps the launcher's era choice onto LandSandBoat's own era support (its `modules/README.md`):

| | Classic (default) | Modern |
|---|---|---|
| `XI_MAIN_MAX_LEVEL` | 75 | LandSandBoat default (99) |
| `XI_MAIN_RESTRICT_CONTENT` | 1: content tagged for disabled expansions isn't loaded | default |
| Expansions (`XI_MAIN_ENABLE_*`) | ROTZ, COP, TOAU, WOTG, ACP, AMK, ASA on; ABYSSEA, VOIDWATCH, SOA, ROV, TVR off | all on |
| Later systems | Mog Garden, Mog House 2F, Records of Eminence, Voidwalker, daily tally off | on |
| `modules/init.txt` | `era/lua` | `era/lua` (its overrides only apply to disabled expansions, so they do nothing here) |
| `era/sql` data packs | abyssea, voidwatch, soa, rov, tvr (whichever exist) | none |

The settings travel as environment variables at server start, so switching is instant for them. The data packs
edit game tables in place, so the launcher records the era in `localxi_meta`. When it changes, the next
Play backs up characters (`before-era-change-*`), re-imports game data (player tables untouched) and applies
the packs for the new era. The packs only touch game-data tables (abilities, spells, drops, recipes, zones...).

The packs are optional extras that can lag behind LandSandBoat's schema, so they're applied with
`mariadb --force`: a stale statement is skipped and reported on the Logs page instead of failing the import.
(As of October 2026, two statements in the `abyssea` pack reference columns that no longer exist.) The main
game data is still imported strictly.

## Solo vs Host vs Join

The address clients use to reach a zone comes from the `zone_settings.zoneip` column, and `xi_map` only serves
zones whose address matches its `--ip` argument. Before each start the launcher sets both:

| Mode | zone address / `--ip` | login server binds to | game connects to |
|---|---|---|---|
| Solo | `127.0.0.1` | `127.0.0.1` | `127.0.0.1` |
| Host | chosen LAN/VPN address | `0.0.0.0` | the same chosen address |
| Join | (no local server) | - | the friend's address |

In Host mode the host's own game also connects through the chosen address, so the login and zone connections
come from the same IP, which LandSandBoat checks.

Firewall rules (Host only, added elevated on request) allow the five server programs, and only from
`LocalSubnet` plus the Radmin (26/8), Hamachi (25/8) and Tailscale (100.64/10) ranges, so even a forwarded
router port doesn't expose the server to the whole internet.

## Accounts

- **On a local server**, accounts are written straight to the `accounts` table, mirroring LandSandBoat's
  `auth_session.cpp`: ids start at 1000 and passwords are bcrypt hashes.
- **On a friend's server**, `LoginClient` speaks xiloader's protocol on TCP 54231: a TLS connection (the server
  uses a self-signed certificate), one JSON request (`{"command": 32, "username", "password", "version": [2,2,0], ...}`)
  and one JSON reply (`{"result": 3}` means created). This only works while the host allows account creation,
  which is LandSandBoat's default.
- The login server accepts **TLS 1.3 only** (`tlsv13_server` in `src/login/handler.h`), which Windows 10's built-in
  TLS can't do as a client. Like xiloader bringing its own mbedTLS, `LoginClient` uses BouncyCastle's managed TLS
  rather than `SslStream`, so it works on every Windows version.

## Starting the game

`GameLauncher` writes `runtime\ashita\config\boot\localxi.ini`, based on Ashita's own private-server example.
It sets `.\bootloader\xiloader.exe --server <address> --user <u> --pass <p> --hide` as the boot command, plus the
game's window size, mode and screenshot folder (Ashita applies these instead of the registry). Then it runs
`Ashita-cli.exe localxi.ini`. The game runs inside the `xiloader` process; the launcher watches for that process
to exit, then waits for the server to save the character, backs up and stops the server (unless friends are
still online in Host mode).

Windows finds the game through two kinds of registry entries, both written by Square Enix's installer:

- **COM classes.** xiloader creates PlayOnline (`POLCoreCom`, in `viewer\com\polcore.dll`) and the game
  (`FFXiEntry`, in `FFXi.dll`, which loads `FFXiMain.dll`) with `CoCreateInstance`, so Windows looks the DLLs up
  under `HKLM\SOFTWARE\Classes\CLSID` (32-bit view). Machine-wide entries are what count, because COM ignores
  per-user registrations in elevated processes and the game runs elevated.
- **Install folders.** xiloader hands PlayOnline the folder in `HKLM\SOFTWARE\PlayOnlineUS\InstallFolder`
  value `1000` (PlayOnline Viewer), and the game reads its data from value `0001` (FINAL FANTASY XI).

A folder copied from another PC has neither. `PlayOnlineRegistry` checks both against the chosen folders, and
**Fix registry** writes both. It writes the ten classes the official US installer registers for files in those
folders (game, PlayOnline core, app, contents and movie source), but only those whose DLL exists and isn't
already registered there, so a working install is left alone. When it re-points a class, it also drops any
Windows Installer descriptor (the `InprocServer32` value), which COM would otherwise follow back to the old
install.

## Addons, texture packs and controllers

`AddonManager` knows a fixed list of addons and plugins. Ones that ship with Ashita are only switched on or off.
The others are fetched from their author's GitHub page at the newest version: the latest release's zip (picked by a
name pattern when a release has several) or, for an addon kept as a folder in a repository, that folder at its
newest commit. Downloads are unpacked into `runtime\addons\<id>\`, laid out like Ashita's folder (a zip that is
just the addon's own folder goes under `addons\`), because an Ashita update replaces Ashita's whole folder. Before
each launch the switched-on downloads are copied into Ashita (skipped when the copied version already matches;
settings files already there are kept). Only Ashita's own folders are taken from a download (some carry their
source code alongside).

Plugins are compiled against one Ashita plugin interface. Where a release has one zip per interface, LocalXI picks
the 4.30 build (Ashita 4.3). Every downloaded plugin is also read without loading it (`PeExports`): Ashita 4.3
refuses plugins that don't export `expCreatePlugin`, `expDestroyPlugin` and `expGetInterfaceVersion` ("Plugin is
missing required exports"), so such a download is refused with a clear message, and one already on the PC counts
as not downloaded and is fetched again. That's how LocalXI 0.2.0's Nameplate, built for interface 4.16 by its
abandoned original, gets replaced by the 4.30 port.

Ashita runs a start-up script named in the boot config. LocalXI writes its own, `scripts\localxi.txt`, from
Ashita's `default.txt`: its key binds and the plugins and addons LocalXI doesn't list stay as Ashita has them,
and the listed ones are replaced by the player's choices (plugins first, then `/load addons`, then addons).

XIPivot is a PlayOnline plugin, so it's switched on in the boot config's `[ashita.polplugins]` (`pivot = 1`).
LocalXI writes `config\pivot\pivot.ini` each launch with `root_path` set to `%LOCALAPPDATA%\LocalXI\Texture Packs` and
the switched-on packs as overlays `0=`, `1=` ... in the chosen order (XIPivot uses the first pack holding a file).

Controller settings go in the boot config's `[ffxi.registry]`, which Ashita applies over the game's registry:
`padmode000` (six switches: gamepad, vibration, sliders, hat switches, works in the background, XInput) and
`padsin000` (27 slots, one per game action, each holding a button or stick-axis number). `Gamepad` builds both from
Ashita's documented XInput and DirectInput numbering: Xbox and Nintendo controllers (through Steam Input or
BetterJoy) as XInput, PlayStation controllers as DirectInput unless they go through Steam Input or DS4Windows.
Left unmanaged, both are `-1` and the game keeps what its own Gamepad Config tool saved.

## Seasonal events

`SeasonalEvents` covers the three events LandSandBoat has built. The Harvest Festival has settings of its own
(`HALLOWEEN_2005`, off in LandSandBoat's defaults, and `HALLOWEEN_YEAR_ROUND`), sent as `XI_MAIN_*` variables.
The Starlight Celebration and the Egg Hunt decide from the date alone (`scripts/events`), so for those LocalXI's
server module gets a `seasonal_events.lua` that overrides their date checks. Each registered event keeps its own
copy of its check, so the module also swaps that copy in `xi.events.handler.checkSeasonalEvents`, which runs at
start-up and every JST midnight. With every event on its usual dates there's no module file: LandSandBoat logs a
module that declares no overrides as an error.

## World settings and updates

Whole numbers travel as environment variables (`ServerTuning`): movement speed as `XI_MAP_BASE_SPEED` (normally
50) and `XI_MAP_SPEED_LIMIT` (normally 80, raised in step so speed gear keeps its boost), and the sub job level as
`XI_MAP_SUBJOB_RATIO` (1 half, 2 two thirds, 3 the same as the main job). Text and decimals can't, as LandSandBoat
only types an environment override as a number when every character is a digit, so `ServerSettingsFiles` writes
`settings/main.lua` (server name, welcome message, `WEAPON_SKILL_POWER`) and `settings/map.lua` (`EXP_RATE`) as
copies of the defaults with those lines changed, and removes them again when everything is back to default.

Low memory mode starts `xi_map` with `--lazy` in the classic era too. Measured with a classic world: about 120 MB
at start-up instead of 2.9 GB. Module overrides are applied once at start-up, to zones that are loaded then, so the
`era/lua` rules kept in zone scripts don't apply to zones loaded later.

Updates only happen when a newer LocalXI pins newer component versions in `components.json`. Settings → Updates
can hold the server (`server`, `meshes`) or Ashita and xiloader: a held component that is installed at any version
counts as present (`ComponentInstaller.IsPresent`), so it isn't replaced, and as the server's version doesn't
change, neither the game data nor the player data conversion runs again.

## Running under Wine

`OperatingSystemInfo.IsWine` looks for Wine's `wine_get_version` export in `ntdll.dll`. Under Wine (Linux,
Steam Deck, Proton) LocalXI skips what Wine doesn't have: the DirectPlay check (Wine has DirectPlay built in), the
Windows event log, and Windows Firewall rules (it explains Linux firewalls instead). It loads Ashita's `winefix`
plugin, and opens folders with `winebrowser`. Everything else, including the server and MariaDB, runs as the same
Windows programs inside the game's Wine prefix. See [LINUX.md](LINUX.md).

## Administrator rights

The launcher runs as a normal user. Registry repair and firewall rules start a second copy of `LocalXI.exe`
with `--elevated <resultFile> <task> ...` behind a UAC prompt; it does that one task, writes the result and
exits. Visual C++ runtime installers are run elevated directly.

`Ashita-cli.exe` declares `requireAdministrator` in its manifest (it injects into the game), so it is started
through the shell, which shows the UAC prompt on every Play; starting it directly fails with "requires
elevation". The game therefore runs elevated. The launcher only watches for its `xiloader` process to close,
which works without inspecting it.

## Saves

See [SAVE-FORMAT.md](SAVE-FORMAT.md).
