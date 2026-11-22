using Microsoft.Win32;

namespace VirtualKeyboard.App.Services;

public static class AutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "VirtualKeyboard";

    /// <summary>Autostart only makes sense for a published exe, not `dotnet run`.</summary>
    public static bool IsSupported
    {
        get
        {
            var path = Environment.ProcessPath;
            return !string.IsNullOrEmpty(path)
                   && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                   && !path.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string;
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        if (!IsSupported)
        {
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Registry access is best-effort; the app still works without autostart.
        }
    }
}
