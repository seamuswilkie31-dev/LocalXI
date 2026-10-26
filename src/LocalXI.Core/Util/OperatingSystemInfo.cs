using System.Runtime.InteropServices;

namespace LocalXI.Core.Util;

/// <summary>
/// Whether LocalXI is running under Wine (Linux, macOS, Steam Deck), including Valve's Proton. FINAL FANTASY XI only
/// runs there through Wine, and LocalXI runs in the same Wine setup as the game, where some Windows parts (Windows
/// Firewall, optional features, the event log) don't exist.
/// </summary>
public static class OperatingSystemInfo
{
    private static readonly Lazy<string?> WineVersionValue = new(ReadWineVersion);

    public static bool IsWine => WineVersionValue.Value is not null;

    /// <summary>Wine's version ("9.0", or Proton's own build name), or null on Windows.</summary>
    public static string? WineVersion => WineVersionValue.Value;

    public static string Describe => IsWine ? $"Wine {WineVersion}" : RuntimeInformation.OSDescription;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr WineGetVersion();

    // Wine's ntdll exports wine_get_version; Windows' doesn't (the check Wine's own developers recommend).
    private static string? ReadWineVersion()
    {
        if (!OperatingSystem.IsWindows() || !NativeLibrary.TryLoad("ntdll.dll", out var ntdll))
        {
            return null;
        }

        if (!NativeLibrary.TryGetExport(ntdll, "wine_get_version", out var export))
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringAnsi(Marshal.GetDelegateForFunctionPointer<WineGetVersion>(export)()) ?? "";
        }
        catch (Exception e) when (e is MarshalDirectiveException or AccessViolationException)
        {
            return "";
        }
    }
}
