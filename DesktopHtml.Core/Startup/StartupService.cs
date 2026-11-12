using Microsoft.Win32;

namespace DesktopHtml.Core.Startup;

public sealed class StartupService
{
    public const string RunValueName = "desktop-html";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    // Task Manager's Startup apps tab and Settings > Apps > Startup don't touch the Run value;
    // they flip a flag here instead. Windows skips a Run entry whose flag says disabled.
    private const string StartupApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    /// <summary>True when the Run entry exists and Windows hasn't switched it off.</summary>
    public bool IsEnabled() => IsRegistered() && !IsDisabledByWindows();

    /// <summary>True when the Run entry exists, whether or not Windows will honour it.</summary>
    public bool IsRegistered()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(RunValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>True when the user (or a tool) turned the entry off in Windows' startup apps list.</summary>
    public bool IsDisabledByWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var key = Registry.CurrentUser.OpenSubKey(StartupApprovedKeyPath, writable: false);
        return IsApprovalDisabled(key?.GetValue(RunValueName) as byte[]);
    }

    /// <summary>
    /// Writes the Run entry. Pass <paramref name="clearWindowsDisable"/> only for an explicit user
    /// action (the settings toggle or CLI), so routine launches don't override a choice made in
    /// Task Manager.
    /// </summary>
    public void Enable(string executablePath, bool clearWindowsDisable = false)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Startup registration is only supported on Windows.");
        }

        using (var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not open the current user Run registry key."))
        {
            key.SetValue(RunValueName, BuildRunValue(executablePath), RegistryValueKind.String);
        }

        if (clearWindowsDisable)
        {
            ClearApprovalFlag();
        }
    }

    public void Disable()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Startup registration is only supported on Windows.");
        }

        using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
        {
            key?.DeleteValue(RunValueName, throwOnMissingValue: false);
        }

        ClearApprovalFlag();
    }

    public static string BuildRunValue(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path is required.", nameof(executablePath));
        }

        return $"\"{Path.GetFullPath(executablePath)}\"";
    }

    /// <summary>
    /// Decodes a StartupApproved value: the low bit of the first byte means disabled
    /// (0x02 = enabled, 0x03 = disabled). A missing value means enabled.
    /// </summary>
    public static bool IsApprovalDisabled(byte[]? value) =>
        value is { Length: > 0 } && (value[0] & 0x01) != 0;

    // With no flag, Windows treats the Run entry as enabled.
    private static void ClearApprovalFlag()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var key = Registry.CurrentUser.OpenSubKey(StartupApprovedKeyPath, writable: true);
        key?.DeleteValue(RunValueName, throwOnMissingValue: false);
    }
}
