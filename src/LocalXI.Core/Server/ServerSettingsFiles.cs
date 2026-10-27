using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LocalXI.Core.Config;

namespace LocalXI.Core.Server;

/// <summary>
/// Settings that can't travel as environment variables. LandSandBoat only types numeric environment overrides when
/// every character is a digit, so a rate like 2.5 or a piece of text has to go in a settings/&lt;name&gt;.lua override
/// file instead. LocalXI writes each file as a copy of the defaults with its changes made, and removes it again when
/// nothing is changed; a file someone wrote by hand is left alone.
/// </summary>
public static partial class ServerSettingsFiles
{
    public static readonly IReadOnlyList<double> ExpRates = [1.0, 1.5, 2.0, 2.5, 3.0];

    /// <summary>Weapon skill damage multipliers offered (main.lua WEAPON_SKILL_POWER).</summary>
    public static readonly IReadOnlyList<double> WeaponSkillPowers = [1.0, 1.25, 1.5, 2.0, 2.5, 3.0];

    /// <summary>The longest server name LandSandBoat allows (main.lua says "not longer than 15 characters").</summary>
    public const int MaxServerNameLength = 15;

    private const string Marker = "-- Written by LocalXI";

    private sealed record Change(Regex Line, string Value, string Summary);

    /// <summary>
    /// Writes settings/map.lua and settings/main.lua for the server options. Returns a message for each one that
    /// couldn't be applied (a file edited by hand, or a server without the setting).
    /// </summary>
    public static IReadOnlyList<string> Apply(string serverDir, ServerOptions options)
    {
        var problems = new List<string>();
        if (!ApplyExpRate(serverDir, options.ExpRate))
        {
            problems.Add("The EXP rate couldn't be applied: settings/map.lua was edited by hand, or the server has no EXP_RATE setting.");
        }

        var main = new List<Change>();
        if (CleanServerName(options.ServerName) is { } name)
        {
            main.Add(new Change(ServerNameLine(), $"'{LuaEscape(name)}'", $"server name {name}"));
        }

        if (!string.IsNullOrWhiteSpace(options.WelcomeMessage))
        {
            main.Add(new Change(ServerMessageBlock(), $" '{LuaEscape(options.WelcomeMessage.Trim())}',", "welcome message"));
        }

        if (Math.Abs(options.WeaponSkillPower - 1.0) > 0.001)
        {
            var power = options.WeaponSkillPower.ToString("0.0##", CultureInfo.InvariantCulture);
            main.Add(new Change(WeaponSkillPowerLine(), power, $"weapon skill damage x{power}"));
        }

        if (!WriteOverrides(serverDir, "main", main))
        {
            problems.Add("The server name, welcome message or weapon skill damage couldn't be applied: settings/main.lua was edited by hand, or the server lacks one of those settings.");
        }

        return problems;
    }

    /// <summary>
    /// Writes settings/map.lua as a copy of the defaults with EXP_RATE (combat experience) changed, or removes
    /// LocalXI's copy at 1x. Returns false if it couldn't apply.
    /// </summary>
    public static bool ApplyExpRate(string serverDir, double rate)
    {
        var value = rate.ToString("0.0##", CultureInfo.InvariantCulture);
        return WriteOverrides(serverDir, "map", Math.Abs(rate - 1.0) < 0.001 ? [] : [new Change(ExpRateLine(), value, $"combat EXP x{value}")]);
    }

    /// <summary>A server name LandSandBoat and the game can show: trimmed, plain characters, 15 at most; null for the default.</summary>
    public static string? CleanServerName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var plain = new string(name.Trim().Where(c => c is >= ' ' and <= '~').ToArray()).Trim();
        return plain.Length == 0 ? null : plain[..Math.Min(plain.Length, MaxServerNameLength)].TrimEnd();
    }

    /// <summary>A Lua single-quoted string body: backslashes and quotes escaped, line breaks as \n.</summary>
    internal static string LuaEscape(string text)
    {
        var builder = new StringBuilder();
        foreach (var c in text.Replace("\r\n", "\n").Replace('\r', '\n'))
        {
            builder.Append(c switch
            {
                '\\' => @"\\",
                '\'' => @"\'",
                '\n' => @"\n",
                _ when c < ' ' => " ",
                _ => c.ToString(),
            });
        }

        return builder.ToString();
    }

    private static bool WriteOverrides(string serverDir, string name, IReadOnlyList<Change> changes)
    {
        var target = Path.Combine(serverDir, "settings", name + ".lua");
        var ours = !File.Exists(target) || File.ReadAllText(target).StartsWith(Marker, StringComparison.Ordinal);
        if (!ours)
        {
            return false;
        }

        if (changes.Count == 0)
        {
            File.Delete(target);
            return true;
        }

        var source = Path.Combine(serverDir, "settings", "default", name + ".lua");
        if (!File.Exists(source))
        {
            return false;
        }

        var text = File.ReadAllText(source);
        foreach (var change in changes)
        {
            if (!change.Line.IsMatch(text))
            {
                return false;
            }

            text = change.Line.Replace(text, m => m.Groups[1].Value + change.Value, 1);
        }

        var summary = string.Join(", ", changes.Select(c => c.Summary));
        File.WriteAllText(target, $"{Marker}: {summary}. Removed again when these are set back to LandSandBoat's defaults.\n{text}");
        return true;
    }

    [GeneratedRegex(@"^(\s*EXP_RATE\s*=\s*)[0-9.]+", RegexOptions.Multiline)]
    private static partial Regex ExpRateLine();

    [GeneratedRegex(@"^(\s*SERVER_NAME\s*=\s*)'[^'\n]*'", RegexOptions.Multiline)]
    private static partial Regex ServerNameLine();

    // The message runs over several lines ('...' .. '...') up to the first line ending in "',".
    [GeneratedRegex(@"^(\s*SERVER_MESSAGE\s*=)[\s\S]*?'\s*,[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex ServerMessageBlock();

    [GeneratedRegex(@"^(\s*WEAPON_SKILL_POWER\s*=\s*)[0-9.]+", RegexOptions.Multiline)]
    private static partial Regex WeaponSkillPowerLine();
}

/// <summary>Server settings that are whole numbers, sent as XI_MAP_* environment variables at start-up.</summary>
public static class ServerTuning
{
    /// <summary>Player movement speeds offered, in percent of the normal speed.</summary>
    public static readonly IReadOnlyList<int> SpeedPercents = [100, 110, 125, 150, 175, 200];

    // map.lua BASE_SPEED (50) and SPEED_LIMIT (80): Flee and movement gear can take a player up to the limit.
    private const int BaseSpeed = 50;
    private const int SpeedLimit = 80;

    /// <summary>map.lua SUBJOB_RATIO: 1 half the main job's level (retail), 2 two thirds, 3 the same level.</summary>
    public static readonly IReadOnlyList<int> SubJobRatios = [1, 2, 3];

    public static IReadOnlyDictionary<string, string> Settings(ServerOptions options)
    {
        var settings = new Dictionary<string, string>();
        var percent = Math.Clamp(options.SpeedPercent, 50, 300);
        if (percent != 100)
        {
            // The limit rises with the base, so speed gear and Flee keep their usual boost; 255 is the game's highest.
            settings["XI_MAP_BASE_SPEED"] = Math.Min(255, BaseSpeed * percent / 100).ToString(CultureInfo.InvariantCulture);
            settings["XI_MAP_SPEED_LIMIT"] = Math.Min(255, SpeedLimit * percent / 100).ToString(CultureInfo.InvariantCulture);
        }

        if (options.SubJobRatio is 2 or 3)
        {
            settings["XI_MAP_SUBJOB_RATIO"] = options.SubJobRatio.ToString(CultureInfo.InvariantCulture);
        }

        return settings;
    }
}
