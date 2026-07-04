using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using DesktopHtml.Core.FileSystem;

namespace DesktopHtml.App;

/// <summary>
/// Shell icon extraction with a host-side cache. Icons are returned as PNG
/// data URLs. Supported sizes: small (16), large (32), extralarge (48),
/// jumbo (256). Cached by path + size + file mtime so launcher skins can
/// request the same icons every reload for free.
/// </summary>
public static class IconService
{
    private const int MaxCacheEntries = 1024;

    private static readonly ConcurrentDictionary<string, string?> Cache = new();

    public static string? GetIconDataUrl(string path, string size)
    {
        var normalizedSize = NormalizeSize(size);
        var cacheKey = BuildCacheKey(path, normalizedSize);

        if (cacheKey != null && Cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var dataUrl = ExtractIconDataUrl(path, normalizedSize);

        if (cacheKey != null)
        {
            if (Cache.Count >= MaxCacheEntries)
            {
                Cache.Clear();
            }

            Cache[cacheKey] = dataUrl;
        }

        return dataUrl;
    }

    private static string NormalizeSize(string size) => size.ToLowerInvariant() switch
    {
        "small" => "small",
        "extralarge" => "extralarge",
        "jumbo" => "jumbo",
        _ => "large"
    };

    private static string? BuildCacheKey(string path, string size)
    {
        try
        {
            var info = new FileInfo(path);
            var stamp = info.Exists
                ? info.LastWriteTimeUtc.Ticks
                : Directory.Exists(path) ? 0L : -1L;
            return $"{path.ToLowerInvariant()}|{size}|{stamp}";
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractIconDataUrl(string path, string size)
    {
        var targetPath = path;
        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var resolved = ShortcutResolver.ResolveDirectory(path) ?? ShortcutResolver.ResolveTargetPath(path);
            if (!string.IsNullOrEmpty(resolved) && (File.Exists(resolved) || Directory.Exists(resolved)))
            {
                targetPath = resolved;
            }
        }

        var hIcon = GetShellIcon(targetPath, size);
        if (hIcon == IntPtr.Zero && targetPath != path)
        {
            hIcon = GetShellIcon(path, size);
        }

        if (hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bitmapSource.Freeze();

            using var ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(ms);

            return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static IntPtr GetShellIcon(string path, string size)
    {
        var info = new SHFILEINFO();

        // For small/large SHGetFileInfo returns the icon directly; for the
        // bigger sizes we need the system image list index instead.
        if (size is "small" or "large")
        {
            var flags = SHGFI_ICON | (size == "small" ? SHGFI_SMALLICON : SHGFI_LARGEICON);
            var result = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
            return result == IntPtr.Zero ? IntPtr.Zero : info.hIcon;
        }

        var indexResult = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_SYSICONINDEX);
        if (indexResult == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var listId = size == "jumbo" ? SHIL_JUMBO : SHIL_EXTRALARGE;
        var iidImageList = typeof(IImageList).GUID;
        if (SHGetImageList(listId, ref iidImageList, out var imageList) != 0 || imageList is null)
        {
            return IntPtr.Zero;
        }

        return imageList.GetIcon(info.iIcon, ILD_TRANSPARENT, out var hIcon) == 0 ? hIcon : IntPtr.Zero;
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_SYSICONINDEX = 0x000004000;
    private const int SHIL_EXTRALARGE = 0x2;
    private const int SHIL_JUMBO = 0x4;
    private const int ILD_TRANSPARENT = 0x1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("shell32.dll")]
    private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList? ppv);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [ComImport]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
        [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, ref int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
        [PreserveSig] int AddMasked(IntPtr hbmImage, uint crMask, ref int pi);
        [PreserveSig] int Draw(IntPtr pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, int flags, out IntPtr picon);
    }
}
