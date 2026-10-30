using System.Diagnostics.Eventing.Reader;

namespace LocalXI.Core.Game;

/// <summary>A game crash Windows recorded (Application log, "Application Error" event 1000).</summary>
public sealed record GameCrash(DateTime Time, string Application, string Module, string ExceptionCode, string ModulePath);

/// <summary>
/// Gathers what's known when the game closes almost as soon as it starts: Ashita's own log for that launch, the
/// crash Windows recorded, and the PC's memory. Everything here only reads.
/// </summary>
public static class GameDiagnostics
{
    /// <summary>A game that closes this soon after appearing didn't really start.</summary>
    public static readonly TimeSpan EarlyExit = TimeSpan.FromSeconds(60);

    /// <summary>Hosting or playing solo with less than this, the server and the game can run short of memory.</summary>
    public const double LowMemoryGb = 10;

    // The game runs inside xiloader (started by Ashita); pol.exe and the game DLLs show up when PlayOnline is used.
    private static readonly string[] GameProcesses = ["xiloader.exe", "pol.exe", "ashita-cli.exe"];

    // Lines Ashita logs on every healthy launch, which would only hide the real problem.
    private static readonly string[] Routine = ["Configuration file has invalid section names or no data"];

    /// <summary>Total physical memory Windows reports, in GB (an 8 GB laptop sharing 2 GB with its graphics shows 6).</summary>
    public static double TotalMemoryGb => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024);

    /// <summary>Ashita's log file for a launch: the newest one written since <paramref name="since"/>.</summary>
    public static string? AshitaLogSince(string ashitaDirectory, DateTime since)
    {
        var logs = new DirectoryInfo(Path.Combine(ashitaDirectory, "logs"));
        return logs.Exists
            ? logs.EnumerateFiles("*.txt").Where(f => f.LastWriteTime >= since).OrderByDescending(f => f.LastWriteTime).FirstOrDefault()?.FullName
            : null;
    }

    /// <summary>
    /// Ashita's warnings and errors, then the last lines it wrote before shutting down: the shutdown itself (every
    /// manager released, hooks uninstalled) is the same on every exit and would push the telling lines out.
    /// </summary>
    public static IReadOnlyList<string> SummarizeAshitaLog(IReadOnlyList<string> lines, int tail = 15)
    {
        var problems = lines
            .Where(l => l.Contains("| WARN", StringComparison.Ordinal) || l.Contains("| ERROR", StringComparison.Ordinal) || l.Contains("| CRITICAL", StringComparison.Ordinal))
            .Where(l => !Routine.Any(r => l.Contains(r, StringComparison.Ordinal)));
        var shutdown = lines.ToList().FindIndex(IsShutdownLine);
        var beforeShutdown = shutdown < 0 ? lines : lines.Take(shutdown).ToList();
        return problems.Concat(beforeShutdown.TakeLast(tail)).Distinct().ToList();
    }

    // Ashita's shutdown starts with the game's devices being destroyed, then every manager released.
    private static bool IsShutdownLine(string line) =>
        line.Contains("::~", StringComparison.Ordinal) || line.Contains("::Release |", StringComparison.Ordinal) ||
        line.Contains("UninstallAshita |", StringComparison.Ordinal);

    /// <summary>A step every healthy game start goes through, and the Ashita log line that shows it happened.</summary>
    public sealed record StartupStep(string Name, string LogMarker);

    /// <summary>The game's start-up steps in order, as Ashita logs them (checked against a healthy launch).</summary>
    public static readonly IReadOnlyList<StartupStep> StartupSteps =
    [
        new("game window created", "Creating Final Fantasy XI window"),
        new("Direct3D 8 loaded", "Direct3DCreate8 - Creating new Direct3D instance"),
        new("graphics device requested", "Creating a new graphics device"),
        new("graphics device created", "newIDirect3DDevice8::newIDirect3DDevice8 | .ctor"),
        new("keyboard and controllers opened", "newIDirectInput8A::CreateDevice"),
    ];

    /// <summary>How many of <see cref="StartupSteps"/> the launch got through, in order.</summary>
    public static int StartupStepsReached(IReadOnlyList<string> lines)
    {
        var reached = 0;
        foreach (var step in StartupSteps)
        {
            if (!lines.Any(l => l.Contains(step.LogMarker, StringComparison.Ordinal)))
            {
                break;
            }

            reached++;
        }

        return reached;
    }

    /// <summary>What the step the game stopped at says about the cause, for the early-exit message.</summary>
    public static string DescribeStartupStop(int stepsReached) => stepsReached switch
    {
        0 => "The game quit before it even created its window, so FINAL FANTASY XI itself couldn't start: usually its " +
             "settings, a missing system part (DirectPlay, the Visual C++ runtime) or, on Linux, the Wine/Proton setup.",
        1 => "The game created its window but couldn't load Direct3D 8 (d3d8), which draws it. On Linux this is the " +
             "Wine/Proton graphics setup (DXVK or wined3d); on Windows, the graphics driver.",
        2 or 3 => "The game loaded Direct3D 8 but couldn't create a graphics device. Try Settings > Display > Windowed " +
                  "and a smaller resolution, and update the graphics driver (on Linux, try another Proton or DXVK version).",
        _ => "The game got through its graphics start-up and closed afterwards.",
    };

    /// <summary>Game crashes Windows recorded since <paramref name="since"/>, newest first. Empty if the log can't be read.</summary>
    public static IReadOnlyList<GameCrash> CrashesSince(DateTime since)
    {
        var crashes = new List<GameCrash>();
        if (Util.OperatingSystemInfo.IsWine)
        {
            return crashes; // Wine keeps no Windows event log.
        }

        try
        {
            var milliseconds = Math.Max(1000, (long)(DateTime.Now - since).TotalMilliseconds);
            var query = new EventLogQuery("Application", PathType.LogName,
                $"*[System[Provider[@Name='Application Error'] and EventID=1000 and TimeCreated[timediff(@SystemTime) <= {milliseconds}]]]")
            {
                ReverseDirection = true,
            };
            using var reader = new EventLogReader(query);
            for (var record = reader.ReadEvent(); record is not null; record = reader.ReadEvent())
            {
                using (record)
                {
                    // Application Error 1000: 0 application, 3 faulting module, 6 exception code, 11 module path.
                    var values = record.Properties.Select(p => p.Value?.ToString() ?? "").ToList();
                    var application = values.ElementAtOrDefault(0) ?? "";
                    if (GameProcesses.Contains(application, StringComparer.OrdinalIgnoreCase))
                    {
                        crashes.Add(new GameCrash(record.TimeCreated ?? DateTime.Now, application, values.ElementAtOrDefault(3) ?? "",
                            values.ElementAtOrDefault(6) ?? "", values.ElementAtOrDefault(11) ?? ""));
                    }
                }
            }
        }
        catch (Exception e) when (e is EventLogException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // No event log to read; the Ashita log still helps.
        }

        return crashes;
    }
}
