using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LocalXI.Core.Config;
using LocalXI.Core.Util;

namespace LocalXI.Core.Game;

/// <summary>Ashita loads plugins (compiled .dll) with /load and addons (Lua) with /addon load.</summary>
public enum AddonKind { Addon, Plugin }

public enum AddonOrigin
{
    /// <summary>Ships with Ashita itself.</summary>
    Bundled,

    /// <summary>The newest release's zip on the author's GitHub page.</summary>
    GitHubRelease,

    /// <summary>A folder in the author's GitHub repository (no releases), at its newest commit.</summary>
    GitHubFolder,
}

/// <param name="LoadName">The name Ashita loads it by (its folder or .dll), when that differs from the id.</param>
/// <param name="AssetPattern">For releases with several zips: a pattern that picks the Ashita v4 one.</param>
public sealed record AddonInfo(
    string Id,
    string Name,
    AddonKind Kind,
    string Description,
    AddonOrigin Origin = AddonOrigin.Bundled,
    string? Repository = null,
    string? AssetPattern = null,
    string? Folder = null,
    string? LoadName = null)
{
    public string Load => LoadName ?? Id;

    public string Homepage => Repository is null ? "https://github.com/AshitaXI/Ashita-v4beta" : $"https://github.com/{Repository}";

    public string Source => Origin == AddonOrigin.Bundled ? "Comes with Ashita" : $"github.com/{Repository}";

    public string LoadCommand => Kind == AddonKind.Plugin ? $"/load {Load}" : $"/addon load {Load}";
}

/// <summary>A downloaded addon: which version is on disk.</summary>
public sealed record InstalledAddon(string Id, string Version);

/// <summary>
/// The client-side addons and plugins LocalXI offers, and XIPivot for texture packs. Downloads come from each
/// author's own GitHub page at their newest version (asked for on install and on "Check for updates"); they're third-
/// party code that runs inside the game, which the Addons page says. Downloads live outside Ashita's folder
/// (runtime/addons) so an Ashita update can't remove them, and are copied into Ashita before each launch.
/// </summary>
public sealed partial class AddonManager(AppPaths paths, HttpClient http)
{
    public static readonly IReadOnlyList<AddonInfo> Catalog =
    [
        new("minimap", "Minimap", AddonKind.Plugin, "A minimap of the area in the corner of the screen."),
        new("minimapmon", "Minimapmon", AddonKind.Addon, "Hides the minimap at times, such as while standing still. Needs Minimap."),
        new("nameplate", "Nameplate", AddonKind.Plugin, "Fixes the game's nameplate drawing (names flickering or showing through walls).",
            AddonOrigin.GitHubRelease, "Shirk/Nameplate"),
        new("thirdparty", "Thirdparty", AddonKind.Plugin, "Commands other tools and scripts rely on. Part of Ashita's default setup."),
        new("aspect", "Aspect", AddonKind.Addon, "Makes the game's aspect ratio match the window, for wide and ultra-wide screens."),
        new("checker", "Checker", AddonKind.Addon, "Shows monster levels and colours /check results."),
        new("cleancs", "CleanCS", AddonKind.Addon, "Hides addon windows during cutscenes."),
        new("freemem", "Freemem", AddonKind.Addon, "Asks Windows to free memory the game isn't using, once when loaded."),
        new("fps", "FPS", AddonKind.Addon, "Shows the frame rate and changes the frame rate cap (/fps)."),
        new("instantchat", "InstantChat", AddonKind.Addon, "Removes the delay before chat messages appear. Its authors note it can crash."),
        new("mapdot", "Mapdot", AddonKind.Addon, "Shows enemies on the compass on every job."),
        new("mobdb", "MobDB", AddonKind.Addon, "Monster information on screen: level, weaknesses, aggro and more.",
            AddonOrigin.GitHubRelease, "ThornyFFXI/mobdb"),
        new("nolock", "NoLock", AddonKind.Addon, "Engage and disengage without the animation lock.",
            AddonOrigin.GitHubRelease, "WinterSolstice8/nolock"),
        new("sexchange", "Sexchange", AddonKind.Addon, "Change your character's race and hair style with commands (only on your screen)."),
        new("statustimers", "Statustimers", AddonKind.Addon, "Status effect icons with timers, configurable.",
            AddonOrigin.GitHubRelease, "HealsCodes/statustimers"),
        new("skillchains", "Skillchains (Chains)", AddonKind.Addon, "Shows the skillchain on your target, its timer and which weapon skills continue it.",
            AddonOrigin.GitHubFolder, "Sippius/Ashita-v4-addons", Folder: "chains", LoadName: "chains"),
        new("timestamp", "Timestamp", AddonKind.Addon, "Adds the time to chat messages."),
        new("tparty", "TParty", AddonKind.Addon, "Shows party members' TP and your target's health percent."),
        new("xicamera", "XICamera", AddonKind.Addon, "Lets the camera zoom further out (/camera).",
            AddonOrigin.GitHubRelease, "Hokuten85/XICamera", AssetPattern: "ashita4"),
        new("xivbar", "XIVBar", AddonKind.Addon, "FFXIV-style HP, MP and TP bars.",
            AddonOrigin.GitHubRelease, "tirem/XIVBar"),
        new("xiui", "XIUI", AddonKind.Addon, "A full interface overhaul: party list, target bar, hotbars and more (/xiui).",
            AddonOrigin.GitHubRelease, "tirem/XIUI", AssetPattern: @"^XIUI-[\d.]+\.zip$"),
        new("zonename", "Zonename", AddonKind.Addon, "Shows the zone and region name for a moment when you change zones.",
            AddonOrigin.GitHubRelease, "onimitch/ffxi-zonename"),
    ];

    /// <summary>XIPivot (a PlayOnline plugin Ashita loads at start-up) puts texture and DAT packs over the game's files.</summary>
    public static readonly AddonInfo Pivot = new("pivot", "XIPivot", AddonKind.Plugin, "Loads texture packs and other DAT replacements over the game's files, without changing them.",
        AddonOrigin.GitHubRelease, "HealsCodes/XIPivot", AssetPattern: "Ashita_v4");

    /// <summary>Ashita's plugin for a micro-stutter under newer Wine versions, loaded automatically on Linux.</summary>
    private static readonly AddonInfo WineFix = new("winefix", "Wine fix", AddonKind.Plugin, "Fixes a micro-stutter under newer Wine versions.");

    /// <summary>What Ashita's own default script loads, so a new LocalXI install plays the same as before.</summary>
    public static readonly IReadOnlyList<string> Defaults = ["thirdparty", "fps", "timestamp", "tparty"];

    public const string ScriptName = "localxi.txt";
    private const string MarkerFile = ".localxi-version";

    public static AddonInfo? Find(string id) => id == Pivot.Id ? Pivot : Catalog.FirstOrDefault(a => a.Id == id);

    /// <summary>Where downloaded addons are kept: one folder each, laid out like Ashita's own folder.</summary>
    public string CacheDir => Path.Combine(paths.Runtime, "addons");

    /// <summary>
    /// The folder players put XIPivot texture packs in, one sub-folder per pack. It's with LocalXI's other machine
    /// data rather than in Documents: HD packs run to gigabytes, and Documents is often synced to OneDrive.
    /// </summary>
    public string TexturePacksDir => Path.Combine(paths.DataRoot, "Texture Packs");

    private string AddonFolder(AddonInfo addon) => Path.Combine(CacheDir, addon.Id);

    public bool IsAvailable(AddonInfo addon) => addon.Origin == AddonOrigin.Bundled
        ? File.Exists(BundledPath(addon))
        : InstalledVersion(addon) is not null;

    private string BundledPath(AddonInfo addon) => addon.Kind == AddonKind.Plugin
        ? Path.Combine(paths.AshitaDir, "plugins", addon.Load + ".dll")
        : Path.Combine(paths.AshitaDir, "addons", addon.Load, addon.Load + ".lua");

    public string? InstalledVersion(AddonInfo addon)
    {
        var marker = Path.Combine(AddonFolder(addon), MarkerFile);
        return File.Exists(marker) ? File.ReadAllText(marker).Trim() : null;
    }

    // ------------------------------------------------------------ downloads

    /// <summary>The newest version on GitHub: the release tag, or the folder's newest commit.</summary>
    public async Task<string> LatestVersionAsync(AddonInfo addon, CancellationToken cancellationToken = default) => addon.Origin switch
    {
        AddonOrigin.GitHubRelease => (await LatestReleaseAsync(addon, cancellationToken)).Tag,
        AddonOrigin.GitHubFolder => await LatestFolderCommitAsync(addon, cancellationToken),
        _ => "bundled",
    };

    /// <summary>Downloads the newest version and keeps it ready for the next launch. Returns the version installed.</summary>
    public async Task<string> InstallAsync(AddonInfo addon, IProgress<string>? progress, CancellationToken cancellationToken = default)
    {
        if (addon.Origin == AddonOrigin.Bundled)
        {
            return "bundled";
        }

        progress?.Report($"Downloading {addon.Name}...");
        Directory.CreateDirectory(CacheDir);
        var staging = Path.Combine(CacheDir, $".{addon.Id}-download");
        DeleteDirectory(staging);
        Directory.CreateDirectory(staging);
        try
        {
            string version;
            var zipPath = Path.Combine(staging, "download.zip");
            if (addon.Origin == AddonOrigin.GitHubRelease)
            {
                var release = await LatestReleaseAsync(addon, cancellationToken);
                await DownloadAsync(release.Url, zipPath, cancellationToken);
                version = release.Tag;
            }
            else
            {
                version = await LatestFolderCommitAsync(addon, cancellationToken);
                await DownloadAsync($"https://codeload.github.com/{addon.Repository}/zip/{version}", zipPath, cancellationToken);
            }

            var layout = Path.Combine(staging, "ashita");
            ExtractToAshitaLayout(addon, zipPath, layout);

            var target = AddonFolder(addon);
            DeleteDirectory(target);
            Directory.Move(layout, target);
            await File.WriteAllTextAsync(Path.Combine(target, MarkerFile), version, cancellationToken);
            progress?.Report($"{addon.Name} {version} downloaded.");
            return version;
        }
        finally
        {
            DeleteDirectory(staging);
        }
    }

    /// <summary>
    /// Puts a download's files where Ashita expects them. Release zips come either laid out like Ashita's folder
    /// (addons/, plugins/, polplugins/, config/, docs/) or as the addon's own folder, which goes under addons/.
    /// </summary>
    internal static void ExtractToAshitaLayout(AddonInfo addon, string zipPath, string layout)
    {
        var raw = Path.Combine(Path.GetDirectoryName(layout)!, "raw");
        ZipFile.ExtractToDirectory(zipPath, raw, overwriteFiles: true);
        var root = raw;

        if (addon.Origin == AddonOrigin.GitHubFolder)
        {
            // codeload zips hold one "<repo>-<commit>" folder.
            var repoRoot = Directory.GetDirectories(raw).Single();
            var folder = Path.Combine(repoRoot, addon.Folder!);
            if (!Directory.Exists(folder))
            {
                throw new InvalidDataException($"{addon.Name}: the folder '{addon.Folder}' is no longer in {addon.Repository}.");
            }

            CopyDirectory(folder, Path.Combine(layout, "addons", addon.Load));
            return;
        }

        string[] ashitaFolders = ["addons", "plugins", "polplugins", "config", "docs", "resources"];
        var top = Directory.GetDirectories(root).Select(Path.GetFileName).ToList();
        if (Directory.GetFiles(root, "*.lua").Length > 0)
        {
            // The addon's own files with no folder around them.
            CopyDirectory(root, Path.Combine(layout, "addons", addon.Load));
            return;
        }

        if (top.Count == 1 && Directory.GetFiles(root).Length == 0 && !ashitaFolders.Contains(top[0]!, StringComparer.OrdinalIgnoreCase))
        {
            // An addon's folder holds its .lua files (and may have its own config/ or docs/ folders); a version folder
            // around an Ashita layout holds addons/, plugins/ or polplugins/ and no Lua of its own.
            var only = Path.Combine(root, top[0]!);
            string[] programFolders = ["addons", "plugins", "polplugins"];
            var wrapsLayout = Directory.GetFiles(only, "*.lua").Length == 0 &&
                              Directory.GetDirectories(only).Any(d => programFolders.Contains(Path.GetFileName(d), StringComparer.OrdinalIgnoreCase));
            if (wrapsLayout)
            {
                root = only;
            }
            else
            {
                CopyDirectory(only, Path.Combine(layout, "addons", addon.Load));
                return;
            }
        }

        if (!Directory.GetDirectories(root).Any(d => ashitaFolders.Contains(Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException($"{addon.Name}: the download isn't laid out like an Ashita addon or plugin.");
        }

        CopyDirectory(root, layout);
    }

    private async Task<(string Tag, string Url)> LatestReleaseAsync(AddonInfo addon, CancellationToken cancellationToken)
    {
        var release = await GetJsonAsync<GitHubRelease>($"https://api.github.com/repos/{addon.Repository}/releases/latest", cancellationToken);
        var zips = release.Assets.Where(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)).ToList();
        var pick = addon.AssetPattern is { } pattern
            ? zips.FirstOrDefault(a => Regex.IsMatch(a.Name, pattern, RegexOptions.IgnoreCase))
            : zips.FirstOrDefault();
        if (pick is null)
        {
            throw new InvalidDataException($"{addon.Name}'s newest release ({release.TagName}) has no Ashita v4 download.");
        }

        return (release.TagName, pick.BrowserDownloadUrl);
    }

    private async Task<string> LatestFolderCommitAsync(AddonInfo addon, CancellationToken cancellationToken)
    {
        var commits = await GetJsonAsync<List<GitHubCommit>>(
            $"https://api.github.com/repos/{addon.Repository}/commits?path={Uri.EscapeDataString(addon.Folder!)}&per_page=1", cancellationToken);
        return commits.FirstOrDefault()?.Sha ?? throw new InvalidDataException($"{addon.Name}: no commits found in {addon.Repository}.");
    }

    private async Task<T> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await http.SendAsync(request, timeout.Token);
        if ((int)response.StatusCode is 403 or 429)
        {
            throw new HttpRequestException("GitHub is limiting how often it can be asked (60 times an hour without signing in). Try again in a while.");
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken) ?? throw new InvalidDataException($"Empty reply from {url}.");
    }

    private async Task DownloadAsync(string url, string path, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        await using var file = File.Create(path);
        await response.Content.CopyToAsync(file, timeout.Token);
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = "";

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = "";
    }

    private sealed class GitHubCommit
    {
        [JsonPropertyName("sha")]
        public string Sha { get; set; } = "";
    }

    // ------------------------------------------------------------ launch

    /// <summary>
    /// Gets Ashita ready for a launch: copies the downloaded addons that are switched on into its folder, writes
    /// LocalXI's start-up script (which loads them) and XIPivot's settings. Returns whether XIPivot should load. An
    /// addon that can't be copied is left out (and reported) rather than stopping the game.
    /// </summary>
    public bool PrepareForLaunch(AddonSettings settings, Action<string>? warn = null)
    {
        var enabled = settings.Enabled.Select(Find).OfType<AddonInfo>().Where(IsAvailable).ToList();
        foreach (var addon in enabled.Where(a => a.Origin != AddonOrigin.Bundled).ToList())
        {
            if (!TryCopyIntoAshita(addon, warn))
            {
                enabled.Remove(addon);
            }
        }

        // Ashita's winefix plugin fixes a micro-stutter under newer Wine versions; it's only for Linux.
        WriteScript(OperatingSystemInfo.IsWine ? enabled.Append(WineFix) : enabled);

        var pivot = settings.PivotEnabled && IsAvailable(Pivot) && TryCopyIntoAshita(Pivot, warn);
        if (pivot)
        {
            WritePivotConfig(settings);
        }

        return pivot;
    }

    private bool TryCopyIntoAshita(AddonInfo addon, Action<string>? warn)
    {
        try
        {
            CopyIntoAshita(addon);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            warn?.Invoke($"{addon.Name} couldn't be put in place, so the game starts without it: {e.Message}");
            return false;
        }
    }

    private void CopyIntoAshita(AddonInfo addon)
    {
        var source = AddonFolder(addon);
        var version = InstalledVersion(addon);
        var copied = Path.Combine(paths.AshitaDir, "config", "localxi", addon.Id + ".version");
        if (version is not null && File.Exists(copied) && File.ReadAllText(copied).Trim() == version && Path.Exists(AshitaEntry(addon)))
        {
            return;
        }

        RemoveFromAshita(addon);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (relative == MarkerFile)
            {
                continue;
            }

            var target = Path.Combine(paths.AshitaDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            // Keep settings files an addon or the player already has (sample configs are only a starting point).
            if (relative.StartsWith("config" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(target))
            {
                continue;
            }

            File.Copy(file, target, overwrite: true);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(copied)!);
        File.WriteAllText(copied, version ?? "");
    }

    private string AshitaEntry(AddonInfo addon) => addon == Pivot
        ? Path.Combine(paths.AshitaDir, "polplugins", "pivot.dll")
        : BundledPath(addon);

    private void RemoveFromAshita(AddonInfo addon)
    {
        var source = AddonFolder(addon);
        if (Directory.Exists(source))
        {
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file);
                if (!relative.StartsWith("config" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    var target = Path.Combine(paths.AshitaDir, relative);
                    if (File.Exists(target))
                    {
                        File.Delete(target);
                    }
                }
            }
        }

        if (addon.Kind == AddonKind.Addon && addon != Pivot)
        {
            DeleteDirectory(Path.Combine(paths.AshitaDir, "addons", addon.Load));
        }

        var copied = Path.Combine(paths.AshitaDir, "config", "localxi", addon.Id + ".version");
        if (File.Exists(copied))
        {
            File.Delete(copied);
        }
    }

    /// <summary>
    /// LocalXI's start-up script: Ashita's own default.txt (its key binds and aliases) with the plugin and addon
    /// lines replaced by the player's choices. Ashita's own plugins LocalXI doesn't list (addons, screenshot) and
    /// addons it doesn't list (distance, move) stay as Ashita has them.
    /// </summary>
    internal void WriteScript(IEnumerable<AddonInfo> enabled)
    {
        var scripts = Path.Combine(paths.AshitaDir, "scripts");
        Directory.CreateDirectory(scripts);
        var defaults = Path.Combine(scripts, "default.txt");
        var lines = File.Exists(defaults) ? File.ReadAllLines(defaults) : ["/load addons"];
        File.WriteAllText(Path.Combine(scripts, ScriptName), BuildScript(lines, enabled));
    }

    internal static string BuildScript(IReadOnlyList<string> defaultScript, IEnumerable<AddonInfo> enabled)
    {
        var listed = Catalog.SelectMany(a => new[] { a.Id, a.Load }).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var plugins = new List<string>();
        var addons = new List<string>();
        var rest = new List<string>();
        foreach (var line in defaultScript)
        {
            var trimmed = line.Trim();
            if (LoadLine().Match(trimmed) is { Success: true } load)
            {
                if (!listed.Contains(load.Groups[2].Value))
                {
                    (load.Groups[1].Success ? addons : plugins).Add(trimmed);
                }
            }
            else
            {
                rest.Add(line);
            }
        }

        var chosen = enabled.ToList();
        plugins.AddRange(chosen.Where(a => a.Kind == AddonKind.Plugin).Select(a => a.LoadCommand));
        addons.AddRange(chosen.Where(a => a.Kind == AddonKind.Addon).Select(a => a.LoadCommand));

        // The addons plugin runs every Lua addon, so it has to load before them.
        if (addons.Count > 0 && !plugins.Any(p => p.Equals("/load addons", StringComparison.OrdinalIgnoreCase)))
        {
            plugins.Insert(0, "/load addons");
        }

        var output = new List<string>
        {
            "# Written by LocalXI each time the game starts, from Ashita's default.txt and the Addons page.",
            "# Changes here are overwritten; choose addons on LocalXI's Addons page instead.",
            "",
        };
        output.AddRange(plugins);
        output.Add("");
        output.AddRange(addons);
        output.Add("");
        output.AddRange(rest);
        return string.Join(Environment.NewLine, output) + Environment.NewLine;
    }

    [GeneratedRegex(@"^/(addon\s+)?load\s+(\S+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex LoadLine();

    // ------------------------------------------------------------ XIPivot texture packs

    /// <summary>
    /// The texture packs in the Texture Packs folder, in the saved order (new ones last, switched off). A pack is any
    /// folder in it.
    /// </summary>
    public IReadOnlyList<TexturePack> ListTexturePacks(AddonSettings settings)
    {
        Directory.CreateDirectory(TexturePacksDir);
        var onDisk = Directory.GetDirectories(TexturePacksDir).Select(Path.GetFileName).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ordered = settings.TexturePacks.Where(p => onDisk.Contains(p.Name)).ToList();
        ordered.AddRange(onDisk.Where(name => !ordered.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(name => new TexturePack(name, false)));
        return ordered;
    }

    /// <summary>True when a pack folder holds the game's ROM folders, as XIPivot expects (ROM, ROM2 ...).</summary>
    public bool LooksLikeTexturePack(string name) =>
        Directory.Exists(Path.Combine(TexturePacksDir, name)) &&
        Directory.GetDirectories(Path.Combine(TexturePacksDir, name)).Any(d => Path.GetFileName(d).StartsWith("ROM", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// XIPivot's settings: the Texture Packs folder as its root, and the switched-on packs in order (the first one
    /// wins when two packs replace the same file).
    /// </summary>
    internal void WritePivotConfig(AddonSettings settings)
    {
        var folder = Path.Combine(paths.AshitaDir, "config", "pivot");
        Directory.CreateDirectory(folder);
        var packs = ListTexturePacks(settings).Where(p => p.Enabled).ToList();
        var lines = new List<string>
        {
            "; Written by LocalXI each time the game starts; choose texture packs on LocalXI's Addons page.",
            "[settings]",
            "debug_log=false",
            $"root_path={TexturePacksDir}",
            "",
            "[overlays]",
        };
        lines.AddRange(packs.Select((p, i) => $"{i}={p.Name}"));
        lines.Add("");
        lines.Add("[cache]");
        lines.Add("enabled=false");
        lines.Add("max_age=600");
        lines.Add("size=128");
        File.WriteAllLines(Path.Combine(folder, "pivot.ini"), lines);
    }

    // ------------------------------------------------------------ helpers

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
