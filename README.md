# LocalXI

**Your own FINAL FANTASY XI private server, on your own PC, in a few clicks.**

LocalXI is a launcher for Windows that sets up and runs a [LandSandBoat](https://github.com/LandSandBoat/server)
server and starts the game through [Ashita](https://www.ashitaxi.com) for you. No compiling, no database setup,
no config files. Play solo, host a world for friends on your home network or over Radmin VPN, or join a
friend's world. Your characters are saved on your PC and can be carried to another PC as a single file.

> **Status: early development.** Setup, the database, the full LandSandBoat server (from this repo's
> published package), accounts, save files, and logging in and playing are tested on real PCs. **Multiplayer
> works:** two players on two PCs have played together in one world, one hosting and one joining over Radmin VPN.
> See [docs/TESTING.md](docs/TESTING.md).

## What you need

- Windows 10 or 11, 64-bit, or Linux / Steam Deck through Wine or Proton (experimental, see
  [docs/LINUX.md](docs/LINUX.md))
- Your own FINAL FANTASY XI install (see below)
- About 2 GB of disk space for LocalXI, plus the game
- Memory: 8 GB is enough to join a friend; **16 GB recommended to host or play solo** (the classic-era server
  keeps every zone loaded, about 3 GB on its own). With 8 GB or less, turn on **Settings → Server → Low memory
  mode**: the server then starts at about 120 MB (some classic rules kept in zone scripts don't apply then)

## You bring the game

LocalXI contains **no Square Enix files** and never downloads any. You need FINAL FANTASY XI installed
yourself from Square Enix's official installer, and **updated to the current version**. The server follows the
current retail game, so an old install will have problems.

**Updating the game needs one sign-in to Square Enix's official servers.** The official installer stops at
April 2019, and PlayOnline won't offer FINAL FANTASY XI under *Check Files* until the game has connected to the
official servers once. Any of these works:

- an existing or past FFXI account
- a newly bought copy (it includes 30 days of play)
- Square Enix's free trial
- a "Return Home to Vana'diel" campaign, when Square Enix lets former players back in for free

After that one sign-in, **PlayOnline → Check Files → FINAL FANTASY XI** updates the game whenever needed,
even if the subscription has lapsed. This follows LandSandBoat's own
[client setup guide](https://github.com/LandSandBoat/server/wiki/Client-Setup-Windows). LocalXI doesn't support
ways around it.

On Windows 10, also turn on **DirectPlay** (*Turn Windows features on or off → Legacy Components*).

## Getting started

1. Download `LocalXI.exe` from the [latest release](../../releases/latest). It's a single file and needs
   nothing else installed.
2. Run it and follow the four-step setup:
   - **How do you want to play?** Solo, host for friends, or join a friend.
   - **Where is the game installed?** Usually found automatically. If Windows doesn't know where the game is
     (common when it was copied from another PC), press **Fix registry**.
   - **Your login.** A username and password for *your* server. This is not your Square Enix account.
   - **Setup.** Downloads the server, database and loader (a few hundred MB, once), then loads the game data.
3. Press **Play**.

The server starts when you press Play and stops, with an automatic backup, when you close the game.

## Classic or modern

New worlds play like the **classic level-75 era**: the original game plus Rise of the Zilart, Chains of
Promathia, Treasures of Aht Urhgan and Wings of the Goddess (with its add-on scenarios), and the old-style
rules. Later systems such as Abyssea, Adoulin, Records of Eminence and the Mog Garden are switched off. This
uses LandSandBoat's own era support: its content restrictions, the `era/lua` rule modules and the `era/sql` data packs.

Prefer today's game? **Settings → Game era → Modern** (level 99, every expansion). Switching reloads the game
data the next time you press Play. Your characters are kept and backed up first.

## Playing with friends

**Same house:** choose *Host for friends* and pick your home network address. Friends choose *Join a
friend* and type that address.

**Over the internet with [Radmin VPN](https://www.radmin-vpn.com):**

1. Everyone installs Radmin VPN. The host creates a network and friends join it.
2. The host chooses *Host for friends*, picks the **Radmin VPN - 26.x.x.x** address, and presses
   **Allow friends through Windows Firewall** on the Network page.
3. Friends choose *Join a friend* and enter the host's 26.x.x.x address. LocalXI creates their account on the
   host's server automatically.

Hamachi, ZeroTier and Tailscale work the same way. More detail: [docs/LAN-PLAY.md](docs/LAN-PLAY.md).

## Taking your characters to another PC

**Saves → Export save** writes one `.xisave` file containing every character on the PC: levels, jobs, items,
Mog House, quests, missions and story progress. Copy it to the other PC (USB stick, cloud drive, Discord) and
use **Saves → Import save** there. Your current characters are backed up automatically before an import, so it
can always be undone.

Automatic backups are kept in `Documents\LocalXI\Backups`; exports go to `Documents\LocalXI\Saves`.

## Changing characters

The **Modifiers** page edits the characters on your own world, one character at a time:

| Tab | What it does |
|---|---|
| Inventory | Set gil; search every item in the game and add it; remove items (not ones being worn) |
| Jobs | Set job levels and unlock jobs (up to 75 in classic mode) |
| Crafting | Set Fishing and every craft's skill level, with the matching guild rank (up to 100 in classic mode) |
| Abilities | Unlock the sub job, give the character Dual Wield on every job, learn every quest, relic, mythic and empyrean weapon skill, and unlock every map and every mount |
| Fame | Set fame level 1–9 for San d'Oria, Bastok, Windurst and Norg |
| Quests | Set any quest to not started, accepted or completed (the quest log only; no rewards) |
| Home points | Register every home point crystal at once, so you can warp between them straight away |
| Storage | Unlock every storage container at 80 slots: Gobbie bag, Mog Safe and Safe 2, Mog Locker, Satchel, Sack, Case and Wardrobes |
| Magic | Learn every White Magic, Black Magic, Blue Magic, Ninjutsu, song, summon and Geomancy spell (pick which), and every trust |
| GM | Make the character a GM (level 0–5) to use LandSandBoat's `!` chat commands, switch GM mode or the New Adventurer "?" icon, and add the Judge's outfit |

The same page sets the **EXP rate** (×1 to ×3) and **weapon skill damage** (×1 to ×3). They apply to the whole
world, because LandSandBoat has one setting for everyone.

Changes are saved straight into the database, so the server has to be stopped while you make them (the page
offers to stop it). Everything is backed up automatically before the first change each session.

Step-by-step guide, including how to undo changes and a list of handy GM commands:
[docs/MODIFIERS.md](docs/MODIFIERS.md).

## Addons and texture packs

The **Addons** page picks the Ashita addons and plugins that load with your game. They change how the game looks
and works on your PC only; the server and your friends' games aren't affected.

- **Comes with Ashita:** Aspect, BluCheck, BluMon, BluSets, CFHBlock, Checker, CleanCS, Craftmon, Debuff,
  Distance, DrawDistance (pick how far to draw, 1 to 20), Equipmon, FPS, Freemem, InstantChat, Mapdot, Minimap,
  Minimapmon, NoName (hides your own name), Sexchange, Thirdparty, Timestamp and TParty.
- **Downloaded from the author's GitHub page**, at the newest version, when you tick one and press **Save
  addons**: AMANTracker, CrystalBuff, EasyTH, FancyChat, FancyCompass, FancyTrusts, FindAll, MobDB, Nameplate,
  NoLock, Shorthand, Skillchains (Chains), Statustimers, tHotBar, XICamera, XIUI, XIVBar, XivParty and Zonename.
  **Check for updates** fetches newer versions; tick **Keep this version** on any you'd rather leave as they are.
  These are other people's work, run inside your game; each has a **Page** button to its source. Plugins are
  checked when they download, so one built for an older Ashita is refused instead of failing in game.
- Some often-requested addons only exist for Windower or the old Ashita v3 (FFXI Hotbar, EnemyBar2, InfoHUD,
  EquipViewer, FFXIDB's Minimap). The Addons page lists them with what to use instead.

**Texture packs** (such as AshenbubsHD or XI-View) load through [XIPivot](https://github.com/HealsCodes/XIPivot),
which lays them over the game's files without changing them. Tick **Use texture packs**, press **Open texture
packs folder** (`%LOCALAPPDATA%\LocalXI\Texture Packs`), put each pack in its own folder (the one holding its `ROM`
folders), press **Refresh**, tick the packs you want and put them in order: packs higher in the list win.

## Controllers

**Settings → Controller → Set up my controller here** sets the game's controller up from LocalXI instead of
FINAL FANTASY XI's own Gamepad Config tool: pick **Xbox**, **PlayStation 4**, **PlayStation 5**, **Nintendo
Switch Pro** or **Switch 2 Pro**, and the buttons are laid out for it (confirm with the bottom button, or the right
one for Nintendo's A). **Change buttons** moves any action to another button. Vibration, inverted camera and
playing with the window in the background are switches on the same page. PlayStation controllers work directly or
through Steam Input / DS4Windows; Nintendo controllers need Steam Input (or BetterJoy for the Switch 1 Pro
Controller), which makes them act like an Xbox controller.

## Your world's settings

**Settings → Server** also sets, for everyone on your world, the next time the server starts:

- **Server name** (up to 15 letters) and a **welcome message** shown in chat after logging in
- **Movement speed**, from normal up to twice as fast (speed gear and Flee still add to it)
- **Sub job level**: half the main job's level as normal, two thirds, or the same as the main job (75/75, 99/99)
- **Low memory mode**, for PCs with 8 GB or less (see [What you need](#what-you-need))

**Settings → Updates** can keep your current server, or your current Ashita and xiloader, when a newer LocalXI
ships newer ones. LocalXI never updates anything on its own otherwise, and addons only update when you ask.

## Seasonal events

**Settings → Server → Seasonal events** runs LandSandBoat's seasonal events on their usual dates, all year, or not
at all: the **Harvest Festival** (October 20 to November 1), the **Starlight Celebration** (December) and the
**Egg Hunt Egg-stravaganza** (April 6 to 17). Changes apply the next time the server starts.

## Linux and Steam Deck

LocalXI runs in the same Wine or Proton prefix as the game (Lutris, Bottles, Steam). It notices Wine and skips the
Windows-only steps. This is new and **not yet tested on Linux**; see [docs/LINUX.md](docs/LINUX.md) for the steps
and what to report.

## Troubleshooting

| Problem | Try |
|---|---|
| The game doesn't open | Approve the Windows permission prompt when you press Play (Ashita needs administrator rights to start the game). Then check Settings → **Fix registry**. |
| "Your game files are older than this server expects" | Update the game in PlayOnline (*Check Files → FINAL FANTASY XI*). If FINAL FANTASY XI isn't in that list, the game hasn't signed in to Square Enix's servers yet; see [You bring the game](#you-bring-the-game). |
| Friends can't connect | Network → **Allow friends through Windows Firewall**. Check you're both in the same Radmin network and that the host picked the Radmin address. Friends can use **Test connection**. |
| The game closes as soon as it starts (the loader window says "Closing...") | LocalXI shows what it found and copies Ashita's log for that launch to the Logs page, including how far the game's start-up got (window, Direct3D 8, graphics device, input), and leaves the server running for another try. Check, in order: **Turn on DirectPlay** on the Play page if it's shown; Settings → Display → **Windowed**; untick **Start the game through Ashita** to see whether Ashita is the cause; update the graphics driver. If it keeps happening, send the Logs page. |
| "Ports are already in use" | Another FFXI server or launcher is running. Close it. |
| The Windows key does nothing in game | Settings → Display → **Let the Windows key work while the game is open** (on by default; needs Ashita). Windows+Shift+Left/Right then moves the game to another monitor. |
| An addon misbehaves or the game crashes after adding one | Untick it on the Addons page. Heavy HD texture packs can run the 32-bit game out of memory; try fewer packs. |
| "Failed to load plugin 'nameplate'. Plugin is missing required exports." | LocalXI 0.2.0 downloaded a Nameplate built for an older Ashita. Update LocalXI: on the next Save or Play it replaces it with the Ashita 4.3 build. |
| The controller does nothing | Settings → Controller: check the controller type, and for PlayStation controllers whether **Steam Input or DS4Windows** is ticked. Untick **Set up my controller here** to go back to the game's own Gamepad Config tool. |
| Anything else | The **Logs** page shows everything the server and database print. |

## How it works

| Piece | What it does | Where it comes from |
|---|---|---|
| [LandSandBoat](https://github.com/LandSandBoat/server) | The game server | Built for Windows by this repo's GitHub Actions |
| [MariaDB](https://mariadb.org) | Stores the world and your characters | Official portable zip, pinned and hash-checked |
| [Ashita v4](https://www.ashitaxi.com) | Starts the game client | Official release, pinned and hash-checked |
| [xiloader](https://github.com/LandSandBoat/xiloader) | Connects the client to the server | Official release, pinned and hash-checked |
| [Python](https://www.python.org) + MariaDB connector | Converts your characters when a LocalXI update brings a newer server (LandSandBoat's own migrations) | Official embeddable Python and PyPI packages, pinned and hash-checked; private to LocalXI |
| Visual C++ runtime | Needed by the above | Microsoft, installed only if missing |

Nothing is installed system-wide apart from the Visual C++ runtime, the PlayOnline registry fix and firewall
rules (each only if needed, each behind a Windows permission prompt). Everything else lives in
`%LOCALAPPDATA%\LocalXI`. Put a file named `portable.txt` next to `LocalXI.exe` to keep everything beside it
instead.

Details: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and [docs/SAVE-FORMAT.md](docs/SAVE-FORMAT.md).

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```
dotnet build LocalXI.slnx
dotnet test tests/LocalXI.Core.Tests
dotnet run --project src/LocalXI.App
```

Single-file release build:

```
dotnet publish src/LocalXI.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

| Folder | Contents |
|---|---|
| `src/LocalXI.Core` | Everything that isn't UI: downloads, database, server processes, accounts, saves, networking |
| `src/LocalXI.App` | The WPF launcher (setup wizard, Play, Saves, Modifiers, Addons, Network, Settings, Logs) |
| `tests/LocalXI.Core.Tests` | Unit tests |
| `components.json` | Pinned versions and SHA-256 hashes of everything the launcher downloads |
| `.github/workflows` | Launcher CI/release, and the LandSandBoat Windows build |

Publishing releases (including the first server package): [docs/RELEASING.md](docs/RELEASING.md).

## Legal

LocalXI is free software under the [GPL-3.0](LICENSE), like LandSandBoat, Ashita and xiloader. It is a fan
project, not affiliated with or endorsed by Square Enix. FINAL FANTASY is a registered trademark of Square Enix
Holdings Co., Ltd. Running a private server is against Square Enix's terms of service for the official game;
LocalXI is a non-commercial hobby project and must never be sold or used to charge for play.
