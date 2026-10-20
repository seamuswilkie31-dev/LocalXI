using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalXI.Core.Config;
using LocalXI.Core.Game;
using LocalXI.Core.Network;
using LocalXI.Core.Server;

namespace LocalXI.App.ViewModels;

public sealed partial class PlayViewModel : PageViewModel
{
    private bool _watchingGame;
    private LsbServer? _subscribedServer;

    public PlayViewModel(MainViewModel main) : base(main)
    {
    }

    public ObservableCollection<ServerProcessStatus> ServerProcesses { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];

    [ObservableProperty]
    private bool _isServerRunning;

    [ObservableProperty]
    private bool _isGameRunning;

    [ObservableProperty]
    private bool _directPlayOff;

    public bool ShowServer => Launcher.Config.RunsLocalServer;
    public bool IsHost => Launcher.Config.Mode == PlayMode.Host;
    public bool IsJoin => Launcher.Config.Mode == PlayMode.Join;
    public string HostAddress => Launcher.Config.Host.AdvertisedIp;
    public string JoinAddress => Launcher.Config.Join.ServerAddress;
    public string AccountName => Launcher.Config.Account.Username;

    public override void OnShown()
    {
        OnPropertyChanged(string.Empty);
        EnsureStatusUpdates();
        RefreshServerStatus();
        _ = RefreshWarningsAsync();
    }

    public void RefreshServerStatus()
    {
        var server = Launcher.Config.RunsLocalServer && Launcher.ServerPackage.LooksValid ? Launcher.Server : null;
        ServerProcesses.Clear();
        foreach (var status in server?.Status ?? [])
        {
            ServerProcesses.Add(status);
        }

        IsServerRunning = server?.IsRunning == true;
    }

    [RelayCommand]
    private async Task PlayAsync()
    {
        EnsureStatusUpdates();
        var started = await Main.RunAsync(progress => Launcher.PlayAsync(progress, CancellationToken.None));
        if (started)
        {
            Main.Status = "Have fun in Vana'diel!";
            _ = WatchGameAsync();
        }
    }

    [RelayCommand]
    private async Task StopServerAsync()
    {
        if (IsGameRunning && MessageBox.Show(
                "The game is still running. Stop the server anyway? You will be disconnected.",
                "Stop server", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        await Main.RunAsync(progress => Launcher.StopServerAsync(progress));
    }

    [RelayCommand]
    private async Task TurnOnDirectPlayAsync()
    {
        if (await Main.RunAsync(async progress =>
            {
                progress.Report("Turning on DirectPlay - Windows will ask for permission...");
                await Launcher.EnableDirectPlayAsync();
            }))
        {
            Main.Status = "DirectPlay is on. If the game still closes straight away, restart Windows once.";
            await RefreshWarningsAsync();
        }
    }

    [RelayCommand]
    private void CopyHostAddress()
    {
        Clipboard.SetText(HostAddress);
        Main.Status = $"Copied {HostAddress} - send it to your friends.";
    }

    private async Task WatchGameAsync()
    {
        if (_watchingGame)
        {
            return;
        }

        _watchingGame = true;
        try
        {
            IsGameRunning = true;
            var ran = await Launcher.Game.WaitForGameToCloseAsync(CancellationToken.None);
            IsGameRunning = false;
            string? problem = null;
            await Main.RunAsync(async progress => problem = await Launcher.AfterGameClosedAsync(ran, progress));
            if (problem is not null)
            {
                await RefreshWarningsAsync();
                MessageBox.Show(problem, "LocalXI", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            _watchingGame = false;
            IsGameRunning = Launcher.Game.IsGameRunning;
        }
    }

    /// <summary>The launcher creates a new server object after setup or a mode change, so (re)subscribe to whichever is current.</summary>
    private void EnsureStatusUpdates()
    {
        if (!Launcher.Config.RunsLocalServer || ReferenceEquals(_subscribedServer, Launcher.Server))
        {
            return;
        }

        _subscribedServer = Launcher.Server;
        _subscribedServer.StatusChanged += () => Application.Current.Dispatcher.BeginInvoke(RefreshServerStatus);
        _subscribedServer.ProcessCrashed += (name, _) => Application.Current.Dispatcher.BeginInvoke(() =>
            Main.Status = $"{name} stopped unexpectedly - see the Logs page for details.");
    }

    private async Task RefreshWarningsAsync()
    {
        var warnings = new List<string>();
        var config = Launcher.Config;

        // Win32_OptionalFeature takes about a second to answer; keep it off the UI thread.
        DirectPlayOff = await Task.Run(WindowsFeatures.IsDirectPlayEnabled) == false;
        if (DirectPlayOff)
        {
            warnings.Add("DirectPlay is turned off on this PC. FINAL FANTASY XI needs it, and without it the game can close as soon as it starts. Press \"Turn on DirectPlay\" below.");
        }

        if (config.RunsLocalServer && GameDiagnostics.TotalMemoryGb < GameDiagnostics.LowMemoryGb)
        {
            warnings.Add($"This PC has {GameDiagnostics.TotalMemoryGb:0.#} GB of memory. The server uses about 3 GB of it, so loading the world can take several minutes " +
                         "and the game may close if memory runs out. Close other programs before pressing Play (16 GB is recommended for hosting or playing solo).");
        }

        if (Launcher.MissingComponents().Count > 0)
        {
            warnings.Add("An update is ready. It downloads and installs automatically when you press Play.");
        }

        if (string.IsNullOrWhiteSpace(config.FfxiPath) || !FfxiInstallation.Inspect(config.FfxiPath).IsValid)
        {
            warnings.Add("The FINAL FANTASY XI folder is missing or incomplete. Check it in Settings.");
        }
        else
        {
            var install = FfxiInstallation.Inspect(config.FfxiPath);
            if (!PlayOnlineRegistry.Matches(install))
            {
                warnings.Add("Windows doesn't have the game's install location registered, so it may not start. Use \"Fix registry\" in Settings.");
            }

            if (config.RunsLocalServer && install.IsUpToDateFor(Launcher.ServerPackage.ClientVersion) == false && !config.Server.AllowOlderClients)
            {
                warnings.Add($"Your game files ({install.ClientBuildDate:MMMM yyyy}) are older than this server expects. " +
                             "Update the game through PlayOnline (this needs one sign-in to Square Enix's servers first; a free " +
                             "trial works - see the README), or allow older clients in Settings for testing.");
            }
        }

        if (config.Mode == PlayMode.Host && Launcher.ServerPackage.LooksValid && !await FirewallRules.AreInstalledAsync(Launcher.ServerDirectory))
        {
            warnings.Add("Windows Firewall may block your friends. Open the Network page and allow LocalXI through the firewall.");
        }

        Warnings.Clear();
        foreach (var warning in warnings)
        {
            Warnings.Add(warning);
        }
    }
}
