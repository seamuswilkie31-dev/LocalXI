using System.Management;
using System.Runtime.InteropServices;
using LocalXI.Core.Util;

namespace LocalXI.Core.Game;

/// <summary>
/// FINAL FANTASY XI needs DirectPlay, a Windows feature (Turn Windows features on or off > Legacy Components) that
/// Windows 10 and 11 leave off.
/// </summary>
public static class WindowsFeatures
{
    // Win32_OptionalFeature.InstallState: 1 enabled, 2 disabled, 3 absent, 4 unknown.
    private const uint Enabled = 1;
    private const uint Disabled = 2;
    private const uint Absent = 3;

    /// <summary>
    /// True when DirectPlay is on, false when it's off, null when Windows can't say. Read through WMI, which
    /// needs no administrator rights (DISM and Get-WindowsOptionalFeature do).
    /// </summary>
    public static bool? IsDirectPlayEnabled()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT InstallState FROM Win32_OptionalFeature WHERE Name = 'DirectPlay'");
            using var results = searcher.Get();
            foreach (var feature in results)
            {
                using (feature)
                {
                    return Convert.ToUInt32(feature["InstallState"]) switch
                    {
                        Enabled => true,
                        Disabled or Absent => false,
                        _ => null,
                    };
                }
            }

            return null;
        }
        catch (Exception e) when (e is ManagementException or COMException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Turns DirectPlay on with DISM. Needs administrator rights, so the launcher runs it elevated.</summary>
    public static async Task EnableDirectPlayAsync()
    {
        var result = await ProcessRunner.RunAsync(
            Path.Combine(Environment.SystemDirectory, "dism.exe"),
            ["/Online", "/Enable-Feature", "/FeatureName:DirectPlay", "/All", "/NoRestart", "/Quiet"]);

        // 3010: done, but Windows wants a restart before everything uses it.
        if (!result.Success && result.ExitCode != 3010)
        {
            throw new InvalidOperationException($"Windows couldn't turn DirectPlay on (DISM exit code {result.ExitCode}). {result.CombinedOutput}".Trim());
        }
    }
}
