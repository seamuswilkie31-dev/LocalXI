using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalXI.Core;
using LocalXI.Core.Config;
using LocalXI.Core.Database;
using LocalXI.Core.Game;
using LocalXI.Core.Server;
using LocalXI.Core.Util;
using Microsoft.Win32;

namespace LocalXI.App.ViewModels;

public sealed record EventModeOption(EventMode Mode, string Label);

public sealed partial class EventRow(SeasonalEvents.EventInfo info) : ObservableObject
{
    public SeasonalEvents.EventInfo Info { get; } = info;
    public string Name => Info.Name;
    public string Dates => Info.Dates;

    [ObservableProperty]
    private EventModeOption? _selected;
}

public sealed record ControllerOption(ControllerType Type, string Label);

public sealed record NumberOption(int Value, string Label);

public sealed partial class GamepadButtonRow(GamepadAction action) : ObservableObject
{
    public GamepadAction Action { get; } = action;
    public string Name => Action.Name;
    public ObservableCollection<GamepadButton> Choices { get; } = [];

    [ObservableProperty]
    private GamepadButton? _selected;
}

public sealed partial class SettingsViewModel(MainViewModel main) : PageViewModel(main)
{
    public ObservableCollection<InstallCheck> GameChecks { get; } = [];

    public ObservableCollection<EventRow> EventRows { get; } = [];

    public IReadOnlyList<ControllerOption> Controllers { get; } =
    [
        new(ControllerType.Xbox, "Xbox, or any XInput controller"),
        new(ControllerType.PlayStation4, "PlayStation 4 (DualShock 4)"),
        new(ControllerType.PlayStation5, "PlayStation 5 (DualSense)"),
        new(ControllerType.SwitchPro, "Nintendo Switch Pro Controller"),
        new(ControllerType.Switch2Pro, "Nintendo Switch 2 Pro Controller"),
    ];

    public ObservableCollection<GamepadButtonRow> ButtonRows { get; } = [];

    private bool _loadingGamepad;

    [ObservableProperty]
    private bool _gamepadManaged;

    [ObservableProperty]
    private bool _gamepadEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlayStation), nameof(IsNintendo), nameof(ControllerHint))]
    private ControllerOption? _selectedController;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ControllerHint))]
    private bool _throughSteamInput;

    [ObservableProperty]
    private bool _confirmOnRight;

    [ObservableProperty]
    private bool _vibration;

    [ObservableProperty]
    private bool _gamepadInBackground;

    [ObservableProperty]
    private bool _invertCamera;

    public bool IsPlayStation => SelectedController?.Type is ControllerType.PlayStation4 or ControllerType.PlayStation5;
    public bool IsNintendo => SelectedController?.Type is ControllerType.SwitchPro or ControllerType.Switch2Pro;

    public string ControllerHint => SelectedController?.Type switch
    {
        ControllerType.SwitchPro =>
            "Windows has no proper driver for the Switch Pro Controller. Connect it through Steam (add LocalXI as a non-Steam game with Steam Input on) or BetterJoy; both make it act like an Xbox controller, and LocalXI names the buttons as printed on yours.",
        ControllerType.Switch2Pro =>
            "The Switch 2 Pro Controller works on PCs through Steam: add LocalXI as a non-Steam game with Steam Input on, so it acts like an Xbox controller. LocalXI names the buttons as printed on yours.",
        ControllerType.PlayStation4 or ControllerType.PlayStation5 when ThroughSteamInput =>
            "Steam Input or DS4Windows makes the controller act like an Xbox controller; LocalXI still names the buttons as printed on yours.",
        ControllerType.PlayStation4 or ControllerType.PlayStation5 =>
            "Plug it in or pair it over Bluetooth and the game reads it directly. If you use Steam Input or DS4Windows with it, tick the box below.",
        _ => "Xbox controllers, and most other PC controllers, work straight away.",
    };

    public IReadOnlyList<EventModeOption> EventModes { get; } =
    [
        new(EventMode.UsualDates, "On its usual dates"),
        new(EventMode.AlwaysOn, "Always on"),
        new(EventMode.Off, "Off"),
    ];

    public IReadOnlyList<WindowMode> WindowModes { get; } = Enum.GetValues<WindowMode>();

    public IReadOnlyList<GameEra> Eras { get; } = Enum.GetValues<GameEra>();

    [ObservableProperty]
    private GameEra _era;

    [ObservableProperty]
    private string _ffxiPath = "";

    [ObservableProperty]
    private string _registryStatus = "";

    [ObservableProperty]
    private bool _registryOk;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UiScaleHint))]
    private int _width;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UiScaleHint))]
    private int _height;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UiScaleHint))]
    private int _uiScale;

    public IReadOnlyList<int> UiScales => Display.UiScales;

    public string UiScaleHint
    {
        get
        {
            var (width, height) = Display.MenuResolution(Math.Clamp(Width, 640, 7680), Math.Clamp(Height, 480, 4320), UiScale);
            return $"The game draws its menus and text at {width} x {height} and stretches them to the window. " +
                   "Bigger percentages make the interface larger; the 3D world keeps the full window resolution.";
        }
    }

    [ObservableProperty]
    private WindowMode _windowMode;

    [ObservableProperty]
    private bool _useAshita;

    [ObservableProperty]
    private bool _windowsKey;

    [ObservableProperty]
    private bool _lazyZones;

    [ObservableProperty]
    private string _serverName = "";

    [ObservableProperty]
    private string _welcomeMessage = "";

    [ObservableProperty]
    private NumberOption? _selectedSpeed;

    [ObservableProperty]
    private NumberOption? _selectedSubJobRatio;

    [ObservableProperty]
    private bool _lowMemory;

    [ObservableProperty]
    private bool _holdServerUpdates;

    [ObservableProperty]
    private bool _holdClientUpdates;

    [ObservableProperty]
    private string _heldBackSummary = "";

    public int MaxServerNameLength => ServerSettingsFiles.MaxServerNameLength;

    public IReadOnlyList<NumberOption> Speeds { get; } =
        ServerTuning.SpeedPercents.Select(p => new NumberOption(p, p == 100 ? "Normal (100%)" : $"{p}%")).ToList();

    public IReadOnlyList<NumberOption> SubJobRatios { get; } =
    [
        new(1, "Half the main job's level (normal: 75 / 37)"),
        new(2, "Two thirds (75 / 50, 99 / 66)"),
        new(3, "The same as the main job (75 / 75, 99 / 99)"),
    ];

    [ObservableProperty]
    private bool _allowOlderClients;

    [ObservableProperty]
    private bool _stopWhenGameCloses;

    [ObservableProperty]
    private bool _backupOnGameExit;

    [ObservableProperty]
    private int _backupsToKeep;

    [ObservableProperty]
    private string _overrideDirectory = "";

    [ObservableProperty]
    private string _newPassword = "";

    public string Username => Launcher.Config.Account.Username;
    public bool RunsLocalServer => Launcher.Config.RunsLocalServer;
    public string DataFolder => Launcher.Paths.DataRoot;
    public string UserFolder => Launcher.Paths.UserRoot;
    public string Version => $"LocalXI {LauncherService.AppVersion}";

    public override void OnShown()
    {
        var config = Launcher.Config;
        FfxiPath = config.FfxiPath ?? "";
        Width = config.Window.Width;
        Height = config.Window.Height;
        WindowMode = config.Window.Mode;
        UiScale = Display.UiScales.Contains(config.Window.UiScale) ? config.Window.UiScale : 100;
        UseAshita = config.UseAshita;
        WindowsKey = config.Window.WindowsKey;
        Era = config.Server.Era;
        LazyZones = config.Server.LazyZones;
        ServerName = config.Server.ServerName ?? "";
        WelcomeMessage = config.Server.WelcomeMessage ?? "";
        SelectedSpeed = Speeds.FirstOrDefault(o => o.Value == config.Server.SpeedPercent) ?? Speeds[0];
        SelectedSubJobRatio = SubJobRatios.FirstOrDefault(o => o.Value == config.Server.SubJobRatio) ?? SubJobRatios[0];
        LowMemory = config.Server.LowMemory;
        HoldServerUpdates = config.Updates.HoldServer;
        HoldClientUpdates = config.Updates.HoldClient;
        var held = Launcher.HeldBack();
        HeldBackSummary = held.Count == 0
            ? ""
            : $"Kept back: {string.Join(", ", held.Select(c => c.Name))}. This LocalXI ships newer versions; untick to update on the next Play.";
        AllowOlderClients = config.Server.AllowOlderClients;
        StopWhenGameCloses = config.Server.StopWhenGameCloses;
        BackupOnGameExit = config.Backups.OnGameExit;
        BackupsToKeep = config.Backups.Keep;
        OverrideDirectory = config.Server.OverrideDirectory ?? "";
        var gamepad = config.Gamepad;
        _loadingGamepad = true;
        GamepadManaged = gamepad.Managed;
        GamepadEnabled = gamepad.Enabled;
        SelectedController = Controllers.First(c => c.Type == gamepad.Type);
        ThroughSteamInput = gamepad.ThroughSteamInput;
        ConfirmOnRight = gamepad.ConfirmOnRight;
        Vibration = gamepad.Vibration;
        GamepadInBackground = gamepad.WorksInBackground;
        InvertCamera = gamepad.InvertCamera;
        _loadingGamepad = false;
        BuildButtonRows(gamepad.Buttons);

        EventRows.Clear();
        foreach (var info in SeasonalEvents.Events)
        {
            var mode = info.Get(config.Server.Events);
            EventRows.Add(new EventRow(info) { Selected = EventModes.First(o => o.Mode == mode) });
        }

        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(RunsLocalServer));
        CheckGame();
    }

    [RelayCommand]
    private void CheckGame()
    {
        GameChecks.Clear();
        if (string.IsNullOrWhiteSpace(FfxiPath) || !Directory.Exists(FfxiPath))
        {
            GameChecks.Add(new InstallCheck("Game folder", false, "Choose your FINAL FANTASY XI folder."));
            RegistryOk = false;
            RegistryStatus = "";
            return;
        }

        var install = FfxiInstallation.Inspect(FfxiPath);
        foreach (var check in install.Checks)
        {
            GameChecks.Add(check);
        }

        RegistryOk = PlayOnlineRegistry.Matches(install);
        RegistryStatus = RegistryOk
            ? "✓ Windows knows where the game is installed."
            : "Windows doesn't have this install location registered. The game needs it to start.";
    }

    [RelayCommand]
    private void BrowseGameFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select your FINAL FANTASY XI folder" };
        if (dialog.ShowDialog() == true)
        {
            FfxiPath = dialog.FolderName;
            CheckGame();
        }
    }

    [RelayCommand]
    private async Task FixRegistryAsync()
    {
        var install = FfxiInstallation.Inspect(FfxiPath);
        await Main.RunAsync(async progress =>
        {
            progress.Report("Updating the registry (approve the Windows prompt)...");
            await Launcher.RepairRegistryAsync(install);
            progress.Report("Registry updated.");
        });
        CheckGame();
    }

    [RelayCommand]
    private void BrowseOverride()
    {
        var dialog = new OpenFolderDialog { Title = "Select a LandSandBoat folder containing xi_map.exe" };
        if (dialog.ShowDialog() == true)
        {
            OverrideDirectory = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void Save()
    {
        var config = Launcher.Config;
        config.FfxiPath = FfxiPath;
        config.Window.Width = Math.Clamp(Width, 640, 7680);
        config.Window.Height = Math.Clamp(Height, 480, 4320);
        config.Window.Mode = WindowMode;
        config.Window.UiScale = UiScale;
        config.Window.WindowsKey = WindowsKey;
        config.UseAshita = UseAshita;
        var eraChanged = Era != config.Server.Era;
        config.Server.Era = Era;
        config.Server.LazyZones = LazyZones;
        config.Server.ServerName = ServerSettingsFiles.CleanServerName(ServerName);
        config.Server.WelcomeMessage = string.IsNullOrWhiteSpace(WelcomeMessage) ? null : WelcomeMessage.Trim();
        config.Server.SpeedPercent = SelectedSpeed?.Value ?? 100;
        config.Server.SubJobRatio = SelectedSubJobRatio?.Value ?? 1;
        config.Server.LowMemory = LowMemory;
        config.Updates.HoldServer = HoldServerUpdates;
        config.Updates.HoldClient = HoldClientUpdates;
        config.Server.AllowOlderClients = AllowOlderClients;
        config.Server.StopWhenGameCloses = StopWhenGameCloses;
        config.Gamepad = CurrentGamepad(includeButtons: true);
        foreach (var row in EventRows)
        {
            row.Info.Set(config.Server.Events, row.Selected?.Mode ?? EventMode.UsualDates);
        }

        config.Backups.OnGameExit = BackupOnGameExit;
        config.Backups.Keep = Math.Clamp(BackupsToKeep, 1, 100);

        var overrideDir = string.IsNullOrWhiteSpace(OverrideDirectory) ? null : OverrideDirectory.Trim();
        var serverChanged = overrideDir != config.Server.OverrideDirectory;
        config.Server.OverrideDirectory = overrideDir;

        Launcher.SaveConfig();
        if (serverChanged)
        {
            Launcher.Reload();
        }

        Main.Status = (eraChanged, Launcher.Config.RunsLocalServer && Launcher.Server.IsRunning) switch
        {
            (true, true) => "Settings saved. Stop the server; the next Play reloads game data for the new era (characters are kept).",
            (true, false) => "Settings saved. The next Play reloads game data for the new era (characters are kept and backed up first).",
            (false, true) => "Settings saved. Server settings apply the next time the server starts.",
            _ => "Settings saved.",
        };
        Main.RefreshModeSummary();
        OnShown();
    }

    partial void OnSelectedControllerChanged(ControllerOption? value)
    {
        if (!_loadingGamepad && value is not null)
        {
            // Nintendo's A, the button that confirms, is on the right; PlayStation and Xbox confirm with the bottom one.
            _loadingGamepad = true;
            ConfirmOnRight = IsNintendo;
            _loadingGamepad = false;
            BuildButtonRows(new Dictionary<int, int>());
        }
    }

    partial void OnThroughSteamInputChanged(bool value) => RebuildButtonRows();

    partial void OnConfirmOnRightChanged(bool value) => RebuildButtonRows();

    private void RebuildButtonRows()
    {
        if (!_loadingGamepad)
        {
            BuildButtonRows(new Dictionary<int, int>());
        }
    }

    /// <summary>The controller settings as chosen on the page.</summary>
    private GamepadSettings CurrentGamepad(bool includeButtons)
    {
        var settings = new GamepadSettings
        {
            Managed = GamepadManaged,
            Enabled = GamepadEnabled,
            Type = SelectedController?.Type ?? ControllerType.Xbox,
            ThroughSteamInput = ThroughSteamInput,
            ConfirmOnRight = ConfirmOnRight,
            Vibration = Vibration,
            WorksInBackground = GamepadInBackground,
            InvertCamera = InvertCamera,
        };

        if (includeButtons)
        {
            // Only buttons that differ from the controller's own layout are kept.
            var defaults = Gamepad.DefaultButtons(settings);
            foreach (var row in ButtonRows.Where(r => r.Selected is not null && defaults.GetValueOrDefault(r.Action.Slot, Gamepad.None) != r.Selected.Id))
            {
                settings.Buttons[row.Action.Slot] = row.Selected!.Id;
            }
        }

        return settings;
    }

    /// <summary>One row per action, with the chosen controller's buttons to pick from.</summary>
    private void BuildButtonRows(IReadOnlyDictionary<int, int> changed)
    {
        var settings = CurrentGamepad(includeButtons: false);
        var buttons = Gamepad.Buttons(settings);
        var defaults = Gamepad.DefaultButtons(settings);
        ButtonRows.Clear();
        foreach (var action in Gamepad.ButtonActions)
        {
            var row = new GamepadButtonRow(action);
            foreach (var button in buttons)
            {
                row.Choices.Add(button);
            }

            var id = changed.TryGetValue(action.Slot, out var mine) ? mine : defaults.GetValueOrDefault(action.Slot, Gamepad.None);
            row.Selected = row.Choices.FirstOrDefault(b => b.Id == id) ?? row.Choices.Last();
            ButtonRows.Add(row);
        }
    }

    [RelayCommand]
    private void ResetButtons() => BuildButtonRows(new Dictionary<int, int>());

    /// <summary>FINAL FANTASY XI's own controller tool, for players who'd rather set the controller up there.</summary>
    [RelayCommand]
    private void OpenGamepadConfigTool()
    {
        var tool = new[] { "ToolsUS", "ToolsEU", "ToolsJP", "Tools" }
            .Select(folder => Path.Combine(FfxiPath, folder, "FFXiPadConfig.exe"))
            .FirstOrDefault(File.Exists);
        if (tool is null)
        {
            MessageBox.Show(@"FINAL FANTASY XI's Gamepad Config tool (ToolsUS\FFXiPadConfig.exe) wasn't found in the game folder.",
                "Controller", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tool) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(tool)! })?.Dispose();
    }

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        if (!Secrets.IsValidPassword(NewPassword))
        {
            MessageBox.Show("Passwords are 6-32 characters without spaces or quotes.", "Password", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!Launcher.Config.RunsLocalServer)
        {
            MessageBox.Show(
                "While joining a friend, your account lives on their server. Ask them to reset it on their Network page, then enter the new password here.",
                "Password", MessageBoxButton.OK, MessageBoxImage.Information);
            Launcher.Config.Account.Password = NewPassword;
            Launcher.SaveConfig();
            NewPassword = "";
            return;
        }

        await Main.RunAsync(async progress =>
        {
            await Launcher.MariaDb.StartAsync(Launcher.Database, CancellationToken.None);
            await new AccountStore(Launcher.Database).SetPasswordAsync(Launcher.Config.Account.Username, NewPassword);
            Launcher.Config.Account.Password = NewPassword;
            Launcher.SaveConfig();
            progress.Report("Password changed.");
        });
        NewPassword = "";
    }

    [RelayCommand]
    private void RerunSetup()
    {
        if (Launcher.Config.RunsLocalServer && Launcher.Server.IsRunning)
        {
            MessageBox.Show("Stop the server on the Play page first.", "Setup", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Main.StartSetup(resume: false);
    }

    [RelayCommand]
    private void OpenDataFolder() => MainViewModel.OpenFolder(Launcher.Paths.DataRoot);

    [RelayCommand]
    private void OpenUserFolder() => MainViewModel.OpenFolder(Launcher.Paths.UserRoot);
}
