using LocalXI.Core.Config;

namespace LocalXI.Core.Game;

/// <summary>A game action a controller button can be given (one slot of the game's padsin000 setting).</summary>
public sealed record GamepadAction(int Slot, string Name);

/// <summary>A controller button as the game numbers it, with the name printed on that controller.</summary>
public sealed record GamepadButton(int Id, string Name);

/// <summary>
/// Builds the game's controller settings, which Ashita applies over the registry at launch ([ffxi.registry]
/// padmode000 and padsin000, documented in Ashita's configuration docs):
/// <list type="bullet">
/// <item>padmode000: six switches (gamepad on, vibration, sliders, hat switches, works in the background, XInput).</item>
/// <item>padsin000: 27 slots, one per game action, each holding the button or stick axis that does it. Button and axis
/// numbers differ between XInput (Xbox-style) and DirectInput (PlayStation controllers on their own driver).</item>
/// </list>
/// </summary>
public static class Gamepad
{
    public const int SlotCount = 27;

    /// <summary>The actions that take a button; slots 13-24 are stick and D-pad directions, set from the layout.</summary>
    public static readonly IReadOnlyList<GamepadAction> ButtonActions =
    [
        new(7, "Confirm / open the action menu"),
        new(5, "Cancel"),
        new(6, "Main menu"),
        new(8, "Switch the active window"),
        new(0, "Auto-run"),
        new(1, "Ctrl macro bar"),
        new(3, "Alt macro bar"),
        new(4, "Heal / lock target"),
        new(2, "First / third person camera"),
        new(9, "Show or hide windows"),
        new(12, "Log out window"),
        new(25, "Screenshot (with windows hidden)"),
        new(10, "Navigate menus with the left stick while held"),
        new(11, "Move the camera with the left stick while held"),
        new(26, "Switch movement / menu / camera controls"),
    ];

    public const int None = -1;

    /// <summary>Whether the game reads the controller through XInput (Xbox-style) for this setup.</summary>
    public static bool UsesXInput(GamepadSettings settings) => settings.Type switch
    {
        ControllerType.PlayStation4 or ControllerType.PlayStation5 => settings.ThroughSteamInput,
        _ => true,
    };

    // XInput button ids (Ashita docs): B 0, X 1, Y 2, A 3, D-pad R/L/U/D 4-7, LB 8, LT 9, L3 10, RB 11, RT 12,
    // R3 13, Start 14, Back 15. Positions: A bottom, B right, X left, Y top.
    private static readonly Layout XInput = new(Bottom: 3, Right: 0, Left: 1, Top: 2, L1: 8, R1: 11, L2: 9, R2: 12, L3: 10, R3: 13,
        Start: 14, Select: 15, MoveX: 32, MoveY: -33, CameraX: 35, CameraY: -36, MenuUp: 6, MenuDown: 7, MenuLeft: 5, MenuRight: 4);

    // DirectInput ids for PlayStation controllers (Ashita docs): Square 0, Cross 1, Circle 2, Triangle 3, L1 4, R1 5,
    // L2 6, R2 7, Share/Create 8, Options 9, L3 10, R3 11; left stick axes 32/33, right stick 34/37, D-pad hat 40/41.
    private static readonly Layout DirectInput = new(Bottom: 1, Right: 2, Left: 0, Top: 3, L1: 4, R1: 5, L2: 6, R2: 7, L3: 10, R3: 11,
        Start: 9, Select: 8, MoveX: 32, MoveY: 33, CameraX: 34, CameraY: 37, MenuUp: 41, MenuDown: 41, MenuLeft: 40, MenuRight: 40);

    private sealed record Layout(int Bottom, int Right, int Left, int Top, int L1, int R1, int L2, int R2, int L3, int R3, int Start, int Select,
        int MoveX, int MoveY, int CameraX, int CameraY, int MenuUp, int MenuDown, int MenuLeft, int MenuRight);

    /// <summary>The buttons of the chosen controller, named as printed on it, for picking a button per action.</summary>
    public static IReadOnlyList<GamepadButton> Buttons(GamepadSettings settings)
    {
        var layout = UsesXInput(settings) ? XInput : DirectInput;
        var names = settings.Type switch
        {
            ControllerType.PlayStation4 => new[] { "Cross", "Circle", "Square", "Triangle", "L1", "R1", "L2", "R2", "L3 (press left stick)", "R3 (press right stick)", "Options", "Share" },
            ControllerType.PlayStation5 => ["Cross", "Circle", "Square", "Triangle", "L1", "R1", "L2", "R2", "L3 (press left stick)", "R3 (press right stick)", "Options", "Create"],
            ControllerType.SwitchPro or ControllerType.Switch2Pro => ["B", "A", "Y", "X", "L", "R", "ZL", "ZR", "Press left stick", "Press right stick", "+ (Plus)", "- (Minus)"],
            _ => ["A", "B", "X", "Y", "LB", "RB", "LT", "RT", "Press left stick", "Press right stick", "Start (Menu)", "Back (View)"],
        };
        var ids = new[] { layout.Bottom, layout.Right, layout.Left, layout.Top, layout.L1, layout.R1, layout.L2, layout.R2, layout.L3, layout.R3, layout.Start, layout.Select };
        var buttons = ids.Zip(names, (id, name) => new GamepadButton(id, name)).ToList();
        if (!UsesXInput(settings))
        {
            buttons.Add(new GamepadButton(12, "PS button"));
            buttons.Add(new GamepadButton(13, "Touchpad press"));
        }

        buttons.Add(new GamepadButton(None, "(nothing)"));
        return buttons;
    }

    /// <summary>The controller's own layout: what each action gets before any change on the Settings page.</summary>
    public static IReadOnlyDictionary<int, int> DefaultButtons(GamepadSettings settings)
    {
        var layout = UsesXInput(settings) ? XInput : DirectInput;
        var (confirm, cancel) = settings.ConfirmOnRight ? (layout.Right, layout.Bottom) : (layout.Bottom, layout.Right);
        return new Dictionary<int, int>
        {
            [7] = confirm,
            [5] = cancel,
            [6] = layout.Left,
            [8] = layout.Top,
            [0] = layout.L1,
            [1] = layout.L2,
            [3] = layout.R2,
            [4] = layout.L3,
            [2] = layout.R3,
            [9] = layout.Select,
            [12] = layout.Start,
            [25] = layout.R1,
            [10] = None,
            [11] = None,
            [26] = None,
        };
    }

    /// <summary>padmode000: gamepad on, vibration, sliders, hat switches (D-pad), works in the background, XInput.</summary>
    public static string PadMode(GamepadSettings settings) => settings.Enabled
        ? string.Join(",", 1, settings.Vibration ? 1 : 0, 1, 1, settings.WorksInBackground ? 1 : 0, UsesXInput(settings) ? 1 : 0)
        : "0,0,0,0,0,0";

    /// <summary>padsin000: every slot's button or axis, with the player's own button choices applied.</summary>
    public static string PadButtons(GamepadSettings settings)
    {
        var layout = UsesXInput(settings) ? XInput : DirectInput;
        var slots = Enumerable.Repeat(None, SlotCount).ToArray();
        foreach (var (slot, button) in DefaultButtons(settings))
        {
            slots[slot] = button;
        }

        var valid = Buttons(settings).Select(b => b.Id).ToHashSet();
        foreach (var (slot, button) in settings.Buttons)
        {
            if (ButtonActions.Any(a => a.Slot == slot) && valid.Contains(button))
            {
                slots[slot] = button;
            }
        }

        // Up and down share one axis, as do left and right; a negative number reverses it.
        var cameraY = settings.InvertCamera ? -layout.CameraY : layout.CameraY;
        (slots[13], slots[14], slots[15], slots[16]) = (layout.MoveY, layout.MoveY, layout.MoveX, layout.MoveX);
        (slots[17], slots[18], slots[19], slots[20]) = (cameraY, cameraY, layout.CameraX, layout.CameraX);
        (slots[21], slots[22], slots[23], slots[24]) = (layout.MenuUp, layout.MenuDown, layout.MenuLeft, layout.MenuRight);
        return string.Join(",", slots);
    }
}
