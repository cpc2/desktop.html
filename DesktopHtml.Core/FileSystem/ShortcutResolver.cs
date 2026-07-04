using System.Runtime.InteropServices;
using System.Text;

namespace DesktopHtml.Core.FileSystem;

public sealed record ShortcutTarget(string? Path, string? Arguments);

/// <summary>
/// Resolves Windows .lnk and .url shortcuts. Single home for the ShellLink COM
/// interop so the file system service, icon service, and bridge dispatcher all
/// share one implementation.
/// </summary>
public static class ShortcutResolver
{
    /// <summary>Resolves a .lnk file to its target path and arguments, or null.</summary>
    public static ShortcutTarget? ResolveTarget(string shortcutPath)
    {
        string? target = null;
        string? arguments = null;
        var thread = new Thread(() =>
        {
            try
            {
                var link = (IShellLinkW)new ShellLink();
                var file = (IPersistFile)link;
                file.Load(shortcutPath, 0);

                link.Resolve(IntPtr.Zero, 1); // SLR_NO_UI

                var sb = new StringBuilder(260);
                link.GetPath(sb, sb.Capacity, out _, 0);
                target = sb.ToString();

                var args = new StringBuilder(260);
                link.GetArguments(args, args.Capacity);
                arguments = args.ToString();
            }
            catch (Exception)
            {
            }
        });
#pragma warning disable CA1416
        thread.SetApartmentState(ApartmentState.STA);
#pragma warning restore CA1416
        thread.Start();
        thread.Join();

        return string.IsNullOrWhiteSpace(target) && string.IsNullOrWhiteSpace(arguments)
            ? null
            : new ShortcutTarget(
                string.IsNullOrWhiteSpace(target) ? null : target,
                string.IsNullOrWhiteSpace(arguments) ? null : arguments);
    }

    public static string? ResolveTargetPath(string shortcutPath) => ResolveTarget(shortcutPath)?.Path;

    /// <summary>
    /// Resolves a .lnk file to a directory when it points at one, including
    /// explorer.exe shortcuts whose directory is passed as an argument.
    /// </summary>
    public static string? ResolveDirectory(string shortcutPath)
    {
        var shortcut = ResolveTarget(shortcutPath);
        if (shortcut is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(shortcut.Path) && Directory.Exists(shortcut.Path))
        {
            return shortcut.Path;
        }

        if (!IsExplorerExecutable(shortcut.Path))
        {
            return null;
        }

        var explorerTarget = GetFirstExplorerPathArgument(shortcut.Arguments);
        return !string.IsNullOrWhiteSpace(explorerTarget) && Directory.Exists(explorerTarget)
            ? explorerTarget
            : null;
    }

    /// <summary>Reads the URL= line from a .url internet shortcut.</summary>
    public static string? GetUrlFromInternetShortcut(string path)
    {
        try
        {
            foreach (var line in File.ReadLines(path))
            {
                if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring(4).Trim();
                }
            }
        }
        catch
        {
        }

        return null;
    }

    /// <summary>Resolves a .url shortcut to a local directory when it uses a file:// URL.</summary>
    public static string? ResolveUrlShortcutDirectory(string path)
    {
        var targetUrl = GetUrlFromInternetShortcut(path);
        if (!string.IsNullOrEmpty(targetUrl) && targetUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var localPath = Uri.UnescapeDataString(new Uri(targetUrl).LocalPath);
                if (Directory.Exists(localPath))
                {
                    return localPath;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static bool IsExplorerExecutable(string? path) =>
        string.Equals(Path.GetFileName(path), "explorer.exe", StringComparison.OrdinalIgnoreCase);

    private static string? GetFirstExplorerPathArgument(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return null;
        }

        var trimmed = arguments.Trim();
        foreach (var prefix in new[] { "/select,", "/e,", "/root," })
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[prefix.Length..].Trim();
                break;
            }
        }

        if (trimmed.StartsWith("\"", StringComparison.Ordinal))
        {
            var endQuote = trimmed.IndexOf('"', 1);
            if (endQuote > 1)
            {
                trimmed = trimmed[1..endQuote];
            }
        }
        else
        {
            var firstSpace = trimmed.IndexOf(' ');
            if (firstSpace > 0)
            {
                trimmed = trimmed[..firstSpace];
            }
        }

        trimmed = Environment.ExpandEnvironmentVariables(trimmed.Trim());
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out WIN32_FIND_DATAW pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out ushort pwHotkey);
        void SetHotkey(ushort wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATAW
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }
}
