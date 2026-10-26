using LocalXI.Core.Config;

namespace LocalXI.Core.Server;

/// <summary>
/// The seasonal events LandSandBoat runs, and how LocalXI switches them on outside their dates or off altogether.
/// Starlight Celebration and the Egg Hunt decide from the date alone (scripts/events), so LocalXI's server module
/// overrides their checks; the Harvest Festival has its own settings (HALLOWEEN_2005, HALLOWEEN_YEAR_ROUND).
/// </summary>
public static class SeasonalEvents
{
    public const string ModuleFile = "seasonal_events.lua";

    public sealed record EventInfo(string Name, string Dates, Func<SeasonalEventSettings, EventMode> Get, Action<SeasonalEventSettings, EventMode> Set);

    public static readonly IReadOnlyList<EventInfo> Events =
    [
        new("Harvest Festival", "October 20 to November 1", s => s.HarvestFestival, (s, m) => s.HarvestFestival = m),
        new("Starlight Celebration", "December", s => s.StarlightCelebration, (s, m) => s.StarlightCelebration = m),
        new("Egg Hunt Egg-stravaganza", "April 6 to 17", s => s.EggHunt, (s, m) => s.EggHunt = m),
    ];

    /// <summary>
    /// XI_MAIN_* settings for the Harvest Festival. LandSandBoat ships it switched off (HALLOWEEN_2005 = 0), so "on its
    /// usual dates" turns the 2005 version on and lets the calendar decide.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Settings(SeasonalEventSettings events) => new Dictionary<string, string>
    {
        ["XI_MAIN_HALLOWEEN_2005"] = events.HarvestFestival == EventMode.Off ? "0" : "1",
        ["XI_MAIN_HALLOWEEN_YEAR_ROUND"] = events.HarvestFestival == EventMode.AlwaysOn ? "1" : "0",
    };

    /// <summary>
    /// The server module that forces Starlight Celebration and the Egg Hunt on or off. Events left on their usual
    /// dates get no override. Each registered event keeps its own copy of its date check, so the module also swaps that
    /// copy before LandSandBoat checks the events (at start-up and every JST midnight).
    /// </summary>
    public static string Module(SeasonalEventSettings events)
    {
        var forced = new List<(string Id, string Check, bool On)>();
        if (events.StarlightCelebration != EventMode.UsualDates)
        {
            forced.Add(("StarlightCelebration", "xi.events.starlightCelebration.enabledCheck", events.StarlightCelebration == EventMode.AlwaysOn));
        }

        if (events.EggHunt != EventMode.UsualDates)
        {
            forced.Add(("egg_hunt", "xi.events.eggHunt.enabledCheck", events.EggHunt == EventMode.AlwaysOn));
        }

        var lines = new List<string>
        {
            "-----------------------------------",
            "-- Written by LocalXI each time the server starts; changes here are overwritten.",
            "-- Seasonal events switched on or off from LocalXI's Settings page (Server > Seasonal events).",
            "-----------------------------------",
            "require('modules/module_utils')",
            "-----------------------------------",
            "",
            "local m = Module:new('localxi_seasonal_events')",
            "",
        };

        if (forced.Count == 0)
        {
            lines.Add("-- Every event follows its usual dates.");
            return string.Join("\n", lines) + "\n";
        }

        lines.Add("local forced =");
        lines.Add("{");
        lines.AddRange(forced.Select(f => $"    ['{f.Id}'] = {(f.On ? "true" : "false")},"));
        lines.Add("}");
        lines.Add("");
        lines.Add("m:addOverride('xi.events.handler.checkSeasonalEvents', function()");
        lines.Add("    for _, event in pairs(xi.events.registeredEvents) do");
        lines.Add("        local on = forced[event.id]");
        lines.Add("        if on ~= nil then");
        lines.Add("            event.enableCheck = function()");
        lines.Add("                return on");
        lines.Add("            end");
        lines.Add("        end");
        lines.Add("    end");
        lines.Add("");
        lines.Add("    super()");
        lines.Add("end)");
        foreach (var (_, check, on) in forced)
        {
            lines.Add("");
            lines.Add($"m:addOverride('{check}', function()");
            lines.Add($"    return {(on ? "true" : "false")}");
            lines.Add("end)");
        }

        return string.Join("\n", lines) + "\n";
    }

    public static void WriteModule(string serverDir, SeasonalEventSettings events)
    {
        var folder = Path.Combine(serverDir, "modules", LocalXiModule.Folder);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ModuleFile), Module(events));
    }
}
