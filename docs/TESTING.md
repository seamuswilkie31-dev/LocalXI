# Testing status

## Verified (October 2026, Windows 10 22H2)

- **Unit tests** (`dotnet test`, 149 tests): controller button layouts, the seasonal events module, craft ranks,
  the addon start-up script and its DrawDistance commands, download layouts (including source code left out),
  reading a DLL's exports and refusing plugins without Ashita 4.3's, XIPivot's settings, the server name, welcome
  message and weapon skill damage in `settings/main.lua`, movement speed and sub job settings, and the mount key
  items, plus INI editing, password rules, client-version parsing, the
  component manifest, download hash checking, zip install/upgrade with preserved folders, era presets and
  data-pack ordering, the module list, the player-table list, server package detection, login-protocol replies,
  network adapter classification, Windows argument quoting, the character data layouts and server-script
  parsers the Modifiers page uses (including the map list and key item layout, checked against a real character), the game's COM class registrations that **Fix registry** writes (in a
  scratch per-user registry key), the boot config's window modes and interface size, and server programs
  being safe to ask about once stopped, and reading the character-data converter's report.
- **Seasonal events in the real server** (October 2026, a throwaway world): with the Starlight Celebration and the
  Egg Hunt set to **Always on**, the map server logged "Starting Seasonal Event" for both in October, and the
  Harvest Festival's settings (`XI_MAIN_HALLOWEEN_2005`, `XI_MAIN_HALLOWEEN_YEAR_ROUND`) were applied, with no
  module errors.
- **Crafting** against the real database (throwaway world, copy of a real character): levels and guild ranks
  written as the game stores them (Fishing 100 → value 1000, Veteran; Cooking 37 → 370, Novice; 150 capped at 100).
- **Addons**: every downloadable addon (Nameplate, MobDB, NoLock, Statustimers, Skillchains, XICamera, XIVBar,
  XIUI, Zonename) and XIPivot fetched from its GitHub page at the newest version and put where Ashita loads it,
  every bundled one found, LocalXI's start-up script and `pivot.ini` written, and the boot config's script, XIPivot
  and controller lines set. The new Addons page, Settings → Controller and Seasonal events, and the Crafting tab
  were checked on screen.
- **The Nameplate fix**: the abandoned Nameplate build (interface 4.16) lacks `expDestroyPlugin`, which is why
  Ashita 4.3 says "Plugin is missing required exports"; the 4.30 port exports exactly what Ashita's own 4.3 plugins
  do. In a throwaway world holding the old download, LocalXI recognised it as unusable and replaced it with the port.
  Every addon added in this update (AMANTracker, CrystalBuff, EasyTH, FancyChat, FancyCompass, FancyTrusts, FindAll,
  Shorthand, tHotBar, XivParty) downloaded and landed where Ashita loads it, along with every bundled one, and the
  start-up script ends with the DrawDistance commands after Ashita's `/wait`.
- **World settings in the real server** (throwaway world): the speed and sub job variables were applied by every
  server program, `settings/main.lua` held the server name, welcome message and weapon skill damage, the server ran
  without errors, and the file was removed again on going back to defaults.
- **Low memory mode** (classic world): the map server used 120 MB at start-up instead of 2.9 GB.
- **All mounts** against the real database: 39 mount key items and the chocobo license given, other key items
  untouched; unticking removed the mounts and kept the license.
- **Per-character Dual Wield in the real server engine**, with LandSandBoat's own test runner (`xi_test.exe`)
  and LocalXI's module and trait rows: a level-30 warrior has no Dual Wield without the setting and Dual Wield I
  with it, loses it when the setting is cleared, and a ninja keeps its own Dual Wield II either way.
- **Character data conversion**, in a throwaway world with a copy of a real character: a new world is marked
  converted without running; loading a save runs the converter (nothing to do); an older layout (columns from
  migrations 069-071 removed, covering accounts_profile, char_jobs and char_exp) is converted on the next Play
  with the character's levels, account and character intact; a failing migration stops the update with the
  backup message and isn't marked done; the real server then starts cleanly.
- **Slow start-ups**: the launcher now waits for the map server's "ready to work" line for as long as it keeps
  printing progress (up to 20 minutes, giving up after 3 quiet minutes); before, an 8 GB laptop still loading
  after 4 minutes got the game started early. A real start on a fast PC is picked up within a fraction of a second
  of the line.
- **Game diagnostics on a real PC** (read-only): DirectPlay's state read through WMI without admin rights, the
  game crash Windows recorded (xiloader.exe in d3d8.dll after a lost graphics device), and Ashita's log for that
  launch, found and summarised.
- **Server shutdown popups** ("No process is associated with this object", one per server program): reproduced
  by checking server status just after each program stopped, as the Play page does; 4 errors before the fix,
  none after.
- **Registry check** against a real official install: the stricter check (install folders plus COM classes)
  accepts it, so working installs aren't flagged.
- **Multiplayer over Radmin VPN** (October 15, 2026): one PC hosting, a second PC joining from another location.
  The joining player's account was created from Join setup, both characters were in the world at the same time
  (the host's server logged both Radmin addresses), and both saved on logout.
- **In game** with the October 2026 client: Ashita 4.3.2.1 and xiloader start the game, log in to the local
  server, create a character and load into the world; the character is still there after logging out.
- **The real server, end-to-end** (package `server-1f204df5e2`, built by this repo's workflow in 71 minutes),
  in a fresh portable folder:
  - all five components downloaded and hash-checked (about 500 MB in 1.5 minutes), the game data loaded in
    classic mode in 30 seconds
  - all five programs start; every port listens (TCP 54001, 54002, 54003, 54230, 54231, 51220; UDP 54230)
  - start-up waits for the map server's "ready to work" line (5 to 90 seconds depending on zone loading and
    antivirus scanning); no crashes in the following 25 seconds
  - the classic settings reach the server (`XI_MAIN_*` logged as applied) and all 114 `era/lua` zone overrides
    apply with every zone loaded (they were skipped with `--lazy`, hence classic mode no longer uses it)
  - account creation over the login protocol with TLS 1.3, which Windows 10's own TLS can't do (hence BouncyCastle)
  - Ctrl+C shutdown of all five programs in 1-2 seconds, then an automatic backup and a clean database stop
- **Database end-to-end**, using the real portable MariaDB 12.3.3 and LandSandBoat's real SQL
  (commit `f038f1e`):
  - download and hash check, `mariadb-install-db`, start and clean shutdown
  - all 120 SQL files imported, including 9 triggers, in about 30 seconds
  - account creation with a bcrypt hash that verifies
  - the LAN zone-address update (299 of 300 zones; the disabled zone untouched)
  - export → wipe → import of a character with job, level and mission variables, including clearing stray
    rows and switching the login account
- **Game era switching** against real LandSandBoat data (commit `7b39a67`): a classic import applies the
  era data packs; switching to modern reloads clean game data (abilities, spells, recipes and skill ranks
  change); switching back reproduces the classic data checksum-for-checksum; characters survive both switches,
  with a backup before each. Two stale upstream statements in the `abyssea` pack are skipped and logged.
- **Setup wizard** steps 1-3 on a real install: the game folder was found automatically, the registry check
  passed, the out-of-date client was flagged, and the Radmin VPN address was detected and suggested.
- **Single-file publish**: `LocalXI.exe` (about 63 MB) starts on a PC without the .NET runtime installed.
- **Visual C++ runtime** detection, and the pinned Microsoft installers' hashes and signatures.

## Not yet verified

These need the game client:

- [ ] Ctrl+C shutdown saves a logged-in character (shutdown itself is verified, with nobody online)
- [ ] Classic era in-game: level cap 75, later-expansion areas closed
- [ ] Close the game → automatic backup → server stop
- [ ] Modifiers changes as seen in game (gil, items, job levels, fame, quest log, home points, EXP rate,
  storage sizes and the Mog Locker, learned spells and trusts, GM mode and the New Adventurer icon, sub job,
  Dual Wield, weapon skills, maps). The Judge's outfit has been worn in game.
- [ ] Window modes in game: borderless windowed (0034 = 2) is a borderless window of the chosen size, borderless
  fullscreen (0034 = 3) covers the screen (the client's documented names for 2 and 3 are swapped)
- [ ] Interface size in game: a smaller menu resolution (0037/0038) makes the menus bigger
- [ ] Windows key in game with Ashita's keyboard.windowskeyenabled = 1 (Start menu, Windows+Shift+arrows to
  move the window to another monitor)
- [ ] Firewall rules: friends connect after **Allow friends through Windows Firewall**
- [ ] On a PC where the game closes as soon as it starts: the message, the Ashita log on the Logs page, and the
  server left running
- [ ] **Turn on DirectPlay** on a PC where it's off (Windows permission prompt, then DISM)
- [ ] Registry repair on a copied install (install folders and COM classes), then the game starts
- [ ] Visual C++ runtime install on a clean Windows install
- [ ] Each addon on the Addons page loading and working in game (including the Nameplate port), and a real texture
  pack through XIPivot
- [ ] In game: the server name and welcome message, movement speed, sub job levels, weapon skill damage, riding
  mounts in a classic world, and Low memory mode with players moving between zones
- [ ] Controllers in game: Xbox, DualShock 4 and DualSense (direct and through Steam Input), Switch and Switch 2 Pro
  through Steam Input (the button numbers come from Ashita's documentation, not from testing each controller)
- [ ] Seasonal event decorations and NPCs in game
- [ ] **Linux and Steam Deck**: nothing of LocalXI has run under Wine or Proton yet (see [LINUX.md](LINUX.md))

## Manual end-to-end checklist

1. Update FINAL FANTASY XI through PlayOnline. This needs one sign-in to Square Enix's official servers first
   (see the README's "You bring the game").
2. Build or download `LocalXI.exe` (the server package is already published and pinned).
3. Fresh run → Solo → finish setup → **Play** → create a character → walk into another zone → close the game.
   Expect: the Logs page shows all five programs starting, the game reaches the character screen, and on close
   a new `auto-*.xisave` appears in Backups.
4. **Saves → Export**, then on a second PC (or after **Run setup again** in a new portable folder) **Import**.
   Expect the character to be there with the same items and position.
5. **Network → Host for friends** → pick the Radmin address → allow firewall → **Play**. On a second PC:
   **Join a friend** → host's address → Test connection → finish setup → **Play**.

Tip: to test without touching your real data, drop a `portable.txt` next to the exe.
