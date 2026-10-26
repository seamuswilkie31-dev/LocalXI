# Playing on Linux and Steam Deck

> **Experimental, not yet tested.** LocalXI's Linux support was written from Wine's and Ashita's documentation
> but hasn't been run on a Linux PC yet. If you try it, please open an issue on GitHub saying what worked and
> what didn't (your distro, Wine or Proton version, and the Logs page).

FINAL FANTASY XI is a Windows game, so on Linux it runs through **Wine**, or **Proton** (Valve's version of Wine,
used by Steam and the Steam Deck). LocalXI runs the same way: you start `LocalXI.exe` in the **same Wine prefix as
the game**, so it can find the game, its registry entries and PlayOnline. There's no separate Linux build.

When LocalXI notices it's running under Wine it changes a few things by itself:

- Ashita's **winefix** plugin is loaded with the game (it fixes a micro-stutter with newer Wine versions).
- The **DirectPlay** check is skipped: Wine has DirectPlay built in.
- The **Windows Firewall** button explains Linux firewalls instead (see [Hosting](#hosting-for-friends)).
- Folders open in your Linux file manager.
- Windows permission prompts don't appear; Wine doesn't have them.

## What you need

- A 64-bit Wine prefix with FINAL FANTASY XI installed and **updated** (the same rules as on Windows: see the
  README's "You bring the game"). Wine, Proton, Lutris and Bottles all make 64-bit prefixes by default.
- Wine 9 or newer, or Proton 9 or newer.
- `LocalXI.exe` from the [latest release](https://github.com/seamuswilkie31-dev/LocalXI/releases/latest). It
  includes everything it needs, so no .NET install is needed in the prefix.

## Lutris or Bottles

1. Install FINAL FANTASY XI with Lutris's install script for it, or with Square Enix's own installer in a new
   Bottles bottle or Wine prefix, and update it through PlayOnline (*Check Files*).
2. Put `LocalXI.exe` somewhere in your home folder.
3. Run it **inside the game's prefix**:
   - **Lutris:** right-click the game → *Wine tools* → *Run EXE inside Wine prefix* → choose `LocalXI.exe`.
   - **Bottles:** open the game's bottle → *Run Executable* → choose `LocalXI.exe`. You can also add it to the
     bottle's programs so it has its own button.
   - **Plain Wine:** `WINEPREFIX=~/path/to/prefix wine ~/LocalXI.exe`
4. Follow the setup as on Windows. The game folder is usually
   `C:\Program Files (x86)\PlayOnline\SquareEnix\FINAL FANTASY XI` inside the prefix. If setup says Windows
   doesn't know where the game is, press **Fix registry**; it writes into the prefix's registry.

## Steam and Steam Deck (Proton)

The Steam version of FINAL FANTASY XI (app 230330) lives in its own Proton prefix. LocalXI has to run in that
prefix, and the simplest way is **protontricks**:

1. Install and update the game through Steam as usual, and start it once so Steam creates its prefix.
2. Install protontricks (on Steam Deck: Discover → *Protontricks*, in desktop mode).
3. Run LocalXI in the game's prefix:

   ```
   protontricks-launch --appid 230330 ~/LocalXI.exe
   ```

4. In setup, point LocalXI at the game folder inside the prefix if it isn't found:
   `C:\Program Files (x86)\PlayOnline\SquareEnix\FINAL FANTASY XI` (or wherever Steam installed it; Steam shows the
   folder under *Manage → Browse local files*).

To start LocalXI from Steam's game mode, add a non-Steam game whose target is `protontricks-launch` with the
launch options `--appid 230330 /home/deck/LocalXI.exe`.

## Controllers

On Linux, Steam Input (or the Steam Deck's own controls) presents controllers to the game as an Xbox controller.
In LocalXI, Settings → Controller, pick your controller and, for PlayStation controllers, tick
**It's connected through Steam Input or DS4Windows**. Nintendo controllers always go that way.

## Hosting for friends

- **Firewall:** most desktop Linux systems ship with the firewall off. If yours uses ufw, run:

  ```
  sudo ufw allow 54001:54003/tcp
  sudo ufw allow 54230:54231/tcp
  sudo ufw allow 54230/udp
  sudo ufw allow 51220/tcp
  ```

  With firewalld (Fedora, Bazzite), open the same ports with `firewall-cmd --add-port`.
- **VPN:** Radmin VPN has no Linux version. Use ZeroTier or Tailscale instead (both run on Linux, Windows and the
  Steam Deck); everyone in the group needs to use the same one. LocalXI lists their addresses on the Network page
  like any other.

## If something goes wrong

| Problem | Try |
|---|---|
| LocalXI's window is blank or its text is missing | Install the core fonts into the prefix: `winetricks corefonts` (Steam: `protontricks 230330 corefonts`). |
| The game closes right after starting | Turn on Settings → Display → **Windowed**. If it still closes, install DirectPlay into the prefix: `winetricks directplay` (Steam: `protontricks 230330 directplay`). |
| The server doesn't start | Send the Logs page in a GitHub issue; the database and server programs are Windows programs too and this is the least tested part. |
| Characters lose their last minutes of progress | Log out in game before closing it. Wine may not pass the shutdown signal to the server, which then can't save players still logged in. |
