using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalXI.Core.Config;
using LocalXI.Core.Game;

namespace LocalXI.App.ViewModels;

public sealed partial class AddonRow(AddonInfo info) : ObservableObject
{
    public AddonInfo Info { get; } = info;
    public string Name => Info.Name;
    public string Description => Info.Description;
    public string Kind => Info.Kind == AddonKind.Plugin ? "Plugin" : "Addon";
    public bool IsDownload => Info.Origin != AddonOrigin.Bundled;

    [ObservableProperty]
    private bool _enabled;

    [ObservableProperty]
    private string _source = "";
}

public sealed partial class TexturePackRow(string name, bool looksValid) : ObservableObject
{
    public string Name { get; } = name;
    public string Warning { get; } = looksValid ? "" : "No ROM folders inside: the pack's own folder may be one level deeper.";

    [ObservableProperty]
    private bool _enabled;
}

/// <summary>
/// Client-side addons and plugins (loaded into this PC's game only, never the server) and XIPivot texture packs.
/// Choices take effect the next time the game starts.
/// </summary>
public sealed partial class AddonsViewModel(MainViewModel main) : PageViewModel(main)
{
    public ObservableCollection<AddonRow> Addons { get; } = [];
    public ObservableCollection<TexturePackRow> TexturePacks { get; } = [];

    public string TexturePacksFolder => Launcher.Addons.TexturePacksDir;
    public bool AshitaOff => !Launcher.Config.UseAshita;

    [ObservableProperty]
    private bool _pivotEnabled;

    [ObservableProperty]
    private string _pivotSource = "";

    [ObservableProperty]
    private TexturePackRow? _selectedPack;

    [ObservableProperty]
    private string _packSummary = "";

    public override void OnShown()
    {
        OnPropertyChanged(nameof(AshitaOff));
        var settings = Launcher.Config.Addons;
        Addons.Clear();
        foreach (var info in AddonManager.Catalog)
        {
            Addons.Add(new AddonRow(info) { Enabled = settings.Enabled.Contains(info.Id), Source = SourceText(info) });
        }

        PivotEnabled = settings.PivotEnabled;
        PivotSource = SourceText(AddonManager.Pivot);
        RefreshTexturePacks();
    }

    private string SourceText(AddonInfo info)
    {
        if (info.Origin == AddonOrigin.Bundled)
        {
            return Launcher.Addons.IsAvailable(info) ? "Comes with Ashita" : "Comes with Ashita (not found: run setup again)";
        }

        return Launcher.Addons.InstalledVersion(info) is { } version
            ? $"From {info.Source}  ·  {ShortVersion(version)} downloaded"
            : $"From {info.Source}  ·  downloaded when you save";
    }

    // Folder addons are versioned by commit; a full hash is noise.
    private static string ShortVersion(string version) => version.Length == 40 ? version[..7] : version;

    [RelayCommand]
    private void RefreshTexturePacks()
    {
        var current = TexturePacks.Select(p => new TexturePack(p.Name, p.Enabled)).ToList();
        var settings = Launcher.Config.Addons;
        if (current.Count > 0)
        {
            settings.TexturePacks = current;
        }

        TexturePacks.Clear();
        foreach (var pack in Launcher.Addons.ListTexturePacks(settings))
        {
            TexturePacks.Add(new TexturePackRow(pack.Name, Launcher.Addons.LooksLikeTexturePack(pack.Name)) { Enabled = pack.Enabled });
        }

        PackSummary = TexturePacks.Count == 0
            ? "No texture packs yet. Open the folder and put each pack in its own folder (the one holding its ROM folders), then press Refresh."
            : "Packs higher in the list win when two replace the same file.";
    }

    [RelayCommand]
    private void OpenTexturePacksFolder() => MainViewModel.OpenFolder(Launcher.Addons.TexturePacksDir);

    [RelayCommand]
    private void MovePackUp() => MovePack(-1);

    [RelayCommand]
    private void MovePackDown() => MovePack(1);

    private void MovePack(int step)
    {
        if (SelectedPack is not { } pack)
        {
            return;
        }

        var index = TexturePacks.IndexOf(pack);
        var target = index + step;
        if (target >= 0 && target < TexturePacks.Count)
        {
            TexturePacks.Move(index, target);
        }
    }

    [RelayCommand]
    private static void OpenHomepage(AddonInfo? info)
    {
        if (info is not null)
        {
            Process.Start(new ProcessStartInfo(info.Homepage) { UseShellExecute = true })?.Dispose();
        }
    }

    /// <summary>Saves the choices and downloads any switched-on addon that isn't on this PC yet.</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        var settings = Launcher.Config.Addons;
        settings.Enabled = Addons.Where(a => a.Enabled).Select(a => a.Info.Id).ToList();
        settings.PivotEnabled = PivotEnabled;
        settings.TexturePacks = TexturePacks.Select(p => new TexturePack(p.Name, p.Enabled)).ToList();
        Launcher.SaveConfig();

        var missing = Addons.Where(a => a.Enabled && a.IsDownload && !Launcher.Addons.IsAvailable(a.Info)).Select(a => a.Info).ToList();
        if (PivotEnabled && !Launcher.Addons.IsAvailable(AddonManager.Pivot))
        {
            missing.Add(AddonManager.Pivot);
        }

        if (missing.Count == 0)
        {
            Main.Status = "Addons saved. They load the next time you press Play.";
            return;
        }

        await Main.RunAsync(async progress =>
        {
            foreach (var addon in missing)
            {
                await Launcher.Addons.InstallAsync(addon, progress);
            }

            progress.Report("Addons saved and downloaded. They load the next time you press Play.");
        });
        OnShown();
    }

    /// <summary>Asks each downloaded addon's GitHub page for a newer version and downloads it.</summary>
    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        var downloaded = AddonManager.Catalog.Append(AddonManager.Pivot)
            .Where(a => a.Origin != AddonOrigin.Bundled && Launcher.Addons.InstalledVersion(a) is not null)
            .ToList();
        if (downloaded.Count == 0)
        {
            Main.Status = "Nothing downloaded yet to update.";
            return;
        }

        await Main.RunAsync(async progress =>
        {
            var updated = new List<string>();
            foreach (var addon in downloaded)
            {
                progress.Report($"Checking {addon.Name}...");
                if (await Launcher.Addons.LatestVersionAsync(addon) != Launcher.Addons.InstalledVersion(addon))
                {
                    await Launcher.Addons.InstallAsync(addon, progress);
                    updated.Add(addon.Name);
                }
            }

            progress.Report(updated.Count == 0 ? "Every downloaded addon is up to date." : $"Updated: {string.Join(", ", updated)}.");
        });
        OnShown();
    }
}
