using System.Runtime.InteropServices;

namespace DesktopHtml.Core.FileSystem;

/// <summary>Sends files or directories to the Windows Recycle Bin instead of
/// deleting them permanently, so accidental deletions stay recoverable.</summary>
public static class RecycleBin
{
    public static bool TryMoveToRecycleBin(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var operation = new ShFileOpStruct
        {
            Func = FoDelete,
            // The API expects a double-null-terminated list; marshalling adds
            // the final terminator.
            From = Path.GetFullPath(path) + '\0',
            Flags = FofAllowUndo | FofNoConfirmation | FofSilent | FofNoErrorUi
        };

        return SHFileOperation(ref operation) == 0 && !operation.AnyOperationsAborted;
    }

    private const uint FoDelete = 0x0003;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofSilent = 0x0004;
    private const ushort FofNoErrorUi = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr Hwnd;
        public uint Func;
        public string From;
        public string? To;
        public ushort Flags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool AnyOperationsAborted;
        public IntPtr NameMappings;
        public string? ProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")]
    private static extern int SHFileOperation(ref ShFileOpStruct fileOp);
}
