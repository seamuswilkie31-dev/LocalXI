using System.IO.Compression;
using LocalXI.Core;
using LocalXI.Core.Config;
using LocalXI.Core.Database;
using LocalXI.Core.Game;
using LocalXI.Core.Server;
using LocalXI.Core.Util;

namespace LocalXI.Core.Tests;

public class GamepadTests
{
    [Fact]
    public void Xbox_UsesXInputIdsInTheGamesSlotOrder()
    {
        var settings = new GamepadSettings { Managed = true, Type = ControllerType.Xbox };

        // Slots per Ashita's padsin000 docs; A confirms, B cancels, X menu, Y active window.
        Assert.Equal("8,9,13,12,10,0,1,3,2,15,-1,-1,14,-33,-33,32,32,-36,-36,35,35,6,7,5,4,11,-1", Gamepad.PadButtons(settings));
        Assert.Equal("1,1,1,1,0,1", Gamepad.PadMode(settings));
    }

    [Fact]
    public void PlayStation_UsesDirectInputIdsAndTheDpadHat()
    {
        var settings = new GamepadSettings { Managed = true, Type = ControllerType.PlayStation5, Vibration = false };

        // Cross confirms, Circle cancels, Square menu, Triangle active window; sticks 32/33 and 34/37, D-pad 40/41.
        Assert.Equal("4,6,11,7,10,2,0,1,3,8,-1,-1,9,33,33,32,32,37,37,34,34,41,41,40,40,5,-1", Gamepad.PadButtons(settings));
        Assert.Equal("1,0,1,1,0,0", Gamepad.PadMode(settings));
    }

    [Fact]
    public void PlayStationThroughSteamInput_IsAnXboxControllerToTheGame()
    {
        var settings = new GamepadSettings { Managed = true, Type = ControllerType.PlayStation4, ThroughSteamInput = true };
        Assert.True(Gamepad.UsesXInput(settings));
        Assert.Equal(Gamepad.PadButtons(new GamepadSettings { Managed = true, Type = ControllerType.Xbox }), Gamepad.PadButtons(settings));
        Assert.Contains(Gamepad.Buttons(settings), b => b is { Id: 3, Name: "Cross" });
    }

    [Fact]
    public void Switch_ConfirmsWithAOnTheRightWhenAsked()
    {
        var settings = new GamepadSettings { Managed = true, Type = ControllerType.Switch2Pro, ConfirmOnRight = true };
        var slots = Gamepad.PadButtons(settings).Split(',').Select(int.Parse).ToArray();
        Assert.Equal(0, slots[7]); // confirm: the right button (XInput B position = Nintendo A)
        Assert.Equal(3, slots[5]); // cancel: the bottom button (Nintendo B)
        Assert.Contains(Gamepad.Buttons(settings), b => b is { Id: 0, Name: "A" });
    }

    [Fact]
    public void ChangedButtonsAndInvertedCamera_Apply()
    {
        var settings = new GamepadSettings { Managed = true, Type = ControllerType.Xbox, InvertCamera = true, Buttons = { [0] = 13, [99] = 1, [13] = 1 } };
        var slots = Gamepad.PadButtons(settings).Split(',').Select(int.Parse).ToArray();
        Assert.Equal(27, slots.Length);
        Assert.Equal(13, slots[0]);   // auto-run moved to R3
        Assert.Equal(-33, slots[13]); // stick slots can't be given a button
        Assert.Equal(36, slots[17]);  // camera up/down reversed
    }

    [Fact]
    public void ControllerOff_SwitchesTheGamepadOff()
    {
        Assert.Equal("0,0,0,0,0,0", Gamepad.PadMode(new GamepadSettings { Managed = true, Enabled = false }));
    }
}

public class SeasonalEventTests
{
    [Fact]
    public void HarvestFestival_UsesItsOwnSettings()
    {
        Assert.Equal("1", SeasonalEvents.Settings(new SeasonalEventSettings())["XI_MAIN_HALLOWEEN_2005"]);
        Assert.Equal("0", SeasonalEvents.Settings(new SeasonalEventSettings())["XI_MAIN_HALLOWEEN_YEAR_ROUND"]);
        Assert.Equal("1", SeasonalEvents.Settings(new SeasonalEventSettings { HarvestFestival = EventMode.AlwaysOn })["XI_MAIN_HALLOWEEN_YEAR_ROUND"]);
        Assert.Equal("0", SeasonalEvents.Settings(new SeasonalEventSettings { HarvestFestival = EventMode.Off })["XI_MAIN_HALLOWEEN_2005"]);
    }

    [Fact]
    public void UsualDates_AddsNoOverrides()
    {
        Assert.DoesNotContain("addOverride", SeasonalEvents.Module(new SeasonalEventSettings()));
    }

    [Fact]
    public void ForcedEvents_OverrideTheirChecks()
    {
        var module = SeasonalEvents.Module(new SeasonalEventSettings { StarlightCelebration = EventMode.AlwaysOn, EggHunt = EventMode.Off });
        Assert.Contains("['StarlightCelebration'] = true,", module);
        Assert.Contains("['egg_hunt'] = false,", module);
        Assert.Contains("m:addOverride('xi.events.handler.checkSeasonalEvents'", module);
        Assert.Contains("m:addOverride('xi.events.starlightCelebration.enabledCheck', function()\n    return true", module);
        Assert.Contains("m:addOverride('xi.events.eggHunt.enabledCheck', function()\n    return false", module);
    }
}

public class CraftTests
{
    [Theory]
    [InlineData(0, true, 0)]     // Amateur
    [InlineData(9, true, 0)]
    [InlineData(10, true, 1)]    // at Amateur's limit: Recruit, so skill-ups continue
    [InlineData(65, true, 6)]    // Craftsman
    [InlineData(100, true, 9)]   // Veteran caps the level-75 era at 100
    [InlineData(100, false, 10)] // Expert today
    [InlineData(110, false, 10)]
    public void RankFollowsLevel(int level, bool classic, int rank) => Assert.Equal(rank, CharacterEditor.CraftRankFor(level, classic));

    [Fact]
    public void CraftsAreTheGamesSkillIds()
    {
        Assert.Equal(Enumerable.Range(48, 9), CharacterEditor.Crafts.Select(c => c.SkillId));
        Assert.Equal("Veteran", CharacterEditor.CraftRanks[9]);
    }
}

public class AddonTests
{
    private static readonly string[] DefaultScript =
    [
        "# Ashita v4 Default Script",
        "/load thirdparty",
        "/load addons",
        "/load screenshot",
        "",
        "/addon load distance",
        "/addon load fps",
        "/addon load tparty",
        "",
        "/bind insert /ashita",
    ];

    [Fact]
    public void Script_KeepsAshitasOwnLinesAndLoadsTheChosenAddons()
    {
        var chosen = new[] { "minimap", "xiui", "skillchains" }.Select(id => AddonManager.Find(id)!);
        var script = AddonManager.BuildScript(DefaultScript, chosen).Split(Environment.NewLine);

        Assert.Contains("/load addons", script);
        Assert.Contains("/load screenshot", script);
        Assert.Contains("/addon load distance", script); // not in LocalXI's list, so Ashita's choice stays
        Assert.DoesNotContain("/load thirdparty", script); // in the list and not chosen
        Assert.DoesNotContain("/addon load fps", script);
        Assert.Contains("/load minimap", script);
        Assert.Contains("/addon load xiui", script);
        Assert.Contains("/addon load chains", script);
        Assert.Contains("/bind insert /ashita", script);
        Assert.True(Array.IndexOf(script, "/load minimap") < Array.IndexOf(script, "/addon load xiui"));
    }

    [Fact]
    public void Script_LoadsTheAddonsPluginWhenAshitasScriptDoesnt()
    {
        var script = AddonManager.BuildScript(["/bind insert /ashita"], [AddonManager.Find("fps")!]).Split(Environment.NewLine);
        Assert.True(Array.IndexOf(script, "/load addons") < Array.IndexOf(script, "/addon load fps"));
    }

    [Fact]
    public void Defaults_MatchAshitasDefaultScript()
    {
        Assert.All(AddonManager.Defaults, id => Assert.NotNull(AddonManager.Find(id)));
        Assert.Equal(new AddonSettings().Enabled, AddonManager.Defaults);
    }

    [Theory]
    [InlineData("xicamera", new[] { "xicamera/xicamera.lua", "xicamera/README.md" }, "addons/xicamera/xicamera.lua")]       // the addon's own folder
    [InlineData("xiui", new[] { "XIUI/XIUI.lua", "XIUI/config/hotbar.lua", "XIUI/docs/x.md" }, "addons/xiui/config/hotbar.lua")] // ...with its own config folder
    [InlineData("mobdb", new[] { "mobdb-v1/addons/mobdb/mobdb.lua" }, "addons/mobdb/mobdb.lua")]                            // a version folder around Ashita's layout
    [InlineData("nolock", new[] { "nolock.lua", "README.md" }, "addons/nolock/nolock.lua")]                                  // loose files
    [InlineData("statustimers", new[] { "addons/statustimers/statustimers.lua" }, "addons/statustimers/statustimers.lua")] // laid out like Ashita
    [InlineData("nameplate", new[] { "plugins/Nameplate.dll", "docs/nameplate/README.md" }, "plugins/Nameplate.dll")]
    [InlineData("pivot", new[] { "polplugins/pivot.dll", "config/pivot/pivot.sample.ini" }, "polplugins/pivot.dll")]
    public void Downloads_LandInAshitasLayout(string id, string[] entries, string expected)
    {
        var root = Directory.CreateTempSubdirectory("localxi-addon-").FullName;
        try
        {
            var zip = Path.Combine(root, "download.zip");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                foreach (var entry in entries)
                {
                    using var writer = new StreamWriter(archive.CreateEntry(entry).Open());
                    writer.Write("x");
                }
            }

            var layout = Path.Combine(root, "ashita");
            AddonManager.ExtractToAshitaLayout(AddonManager.Find(id)!, zip, layout);
            Assert.True(File.Exists(Path.Combine(layout, expected)), $"{expected} missing");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Pivot_UsesTheTexturePacksFolderInTheChosenOrder()
    {
        var root = Directory.CreateTempSubdirectory("localxi-pivot-").FullName;
        try
        {
            var paths = new AppPaths(root, Path.Combine(root, "user"));
            var manager = new AddonManager(paths, new HttpClient());
            foreach (var pack in new[] { "AshenbubsHD", "XI-View", "Unused" })
            {
                Directory.CreateDirectory(Path.Combine(manager.TexturePacksDir, pack, "ROM"));
            }

            var settings = new AddonSettings { TexturePacks = [new("XI-View", true), new("Gone", true)] };
            var listed = manager.ListTexturePacks(settings);
            Assert.Equal(["XI-View", "AshenbubsHD", "Unused"], listed.Select(p => p.Name)); // saved order, then new ones; missing dropped
            Assert.False(listed[1].Enabled);
            Assert.True(manager.LooksLikeTexturePack("AshenbubsHD"));

            settings.TexturePacks = [new("AshenbubsHD", true), new("XI-View", true), new("Unused", false)];
            manager.WritePivotConfig(settings);
            var ini = IniFile.Load(Path.Combine(paths.AshitaDir, "config", "pivot", "pivot.ini"));
            Assert.Equal(manager.TexturePacksDir, ini.Get("settings", "root_path"));
            Assert.Equal("AshenbubsHD", ini.Get("overlays", "0"));
            Assert.Equal("XI-View", ini.Get("overlays", "1"));
            Assert.Null(ini.Get("overlays", "2"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

public class WineTests
{
    [Fact]
    public void Windows_IsNotWine() => Assert.False(OperatingSystemInfo.IsWine);
}
