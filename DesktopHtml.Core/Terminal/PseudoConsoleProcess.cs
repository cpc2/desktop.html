using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace DesktopHtml.Core.Terminal;

/// <summary>
/// A child process attached to a Windows pseudoconsole (ConPTY). The child
/// sees a real TTY — prompts, colors, cursor addressing, resize — and its
/// output arrives as a VT byte stream on <see cref="Output"/>.
/// </summary>
internal sealed class PseudoConsoleProcess : IDisposable
{
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const int STARTF_USESTDHANDLES = 0x00000100;
    private static readonly IntPtr ProcThreadAttributePseudoConsole = (IntPtr)0x00020016;

    private readonly IntPtr _pseudoConsole;
    private readonly IntPtr _processHandle;
    private readonly IntPtr _threadHandle;
    private readonly IntPtr _attributeList;
    private bool _disposed;

    public int ProcessId { get; }
    public FileStream Output { get; }
    public FileStream Input { get; }

    private PseudoConsoleProcess(
        IntPtr pseudoConsole,
        IntPtr processHandle,
        IntPtr threadHandle,
        IntPtr attributeList,
        int processId,
        FileStream output,
        FileStream input)
    {
        _pseudoConsole = pseudoConsole;
        _processHandle = processHandle;
        _threadHandle = threadHandle;
        _attributeList = attributeList;
        ProcessId = processId;
        Output = output;
        Input = input;
    }

    public static PseudoConsoleProcess Start(string command, IReadOnlyList<string> args, string? workingDirectory, int cols, int rows)
    {
        if (!CreatePipe(out var inputRead, out var inputWrite, IntPtr.Zero, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreatePipe (input) failed.");
        }

        if (!CreatePipe(out var outputRead, out var outputWrite, IntPtr.Zero, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreatePipe (output) failed.");
        }

        var size = new COORD { X = (short)Math.Clamp(cols, 20, 500), Y = (short)Math.Clamp(rows, 5, 300) };
        var hr = CreatePseudoConsole(size, inputRead, outputWrite, 0, out var pseudoConsole);
        if (hr != 0)
        {
            throw new Win32Exception(hr, "CreatePseudoConsole failed.");
        }

        // The pseudoconsole holds its own references to these ends.
        inputRead.Dispose();
        outputWrite.Dispose();

        var attributeListSize = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attributeListSize);
        var attributeList = Marshal.AllocHGlobal(attributeListSize);
        if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref attributeListSize) ||
            !UpdateProcThreadAttribute(
                attributeList,
                0,
                ProcThreadAttributePseudoConsole,
                pseudoConsole,
                (IntPtr)IntPtr.Size,
                IntPtr.Zero,
                IntPtr.Zero))
        {
            var error = Marshal.GetLastWin32Error();
            Marshal.FreeHGlobal(attributeList);
            ClosePseudoConsole(pseudoConsole);
            throw new Win32Exception(error, "Pseudoconsole attribute setup failed.");
        }

        var startupInfo = new STARTUPINFOEX
        {
            StartupInfo = new STARTUPINFO
            {
                cb = Marshal.SizeOf<STARTUPINFOEX>(),
                // STARTF_USESTDHANDLES with NULL handles: without this the
                // child's PEB copies the parent's std handle values (pipes,
                // when the host itself has redirected stdio) and its output
                // bypasses the pseudoconsole entirely. Nulling them forces
                // fresh console handles bound to the pty.
                dwFlags = STARTF_USESTDHANDLES
            },
            lpAttributeList = attributeList
        };

        var commandLine = BuildCommandLine(command, args);
        if (!CreateProcessW(
                null,
                new StringBuilder(commandLine),
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                ExtendedStartupInfoPresent,
                IntPtr.Zero,
                string.IsNullOrWhiteSpace(workingDirectory) ? null : workingDirectory,
                ref startupInfo,
                out var processInfo))
        {
            var error = Marshal.GetLastWin32Error();
            DeleteProcThreadAttributeList(attributeList);
            Marshal.FreeHGlobal(attributeList);
            ClosePseudoConsole(pseudoConsole);
            throw new Win32Exception(error, $"CreateProcess failed for '{commandLine}'.");
        }

        var output = new FileStream(outputRead, FileAccess.Read, bufferSize: 4096, isAsync: false);
        var input = new FileStream(inputWrite, FileAccess.Write, bufferSize: 4096, isAsync: false);

        return new PseudoConsoleProcess(
            pseudoConsole,
            processInfo.hProcess,
            processInfo.hThread,
            attributeList,
            processInfo.dwProcessId,
            output,
            input);
    }

    public void Resize(int cols, int rows)
    {
        var size = new COORD { X = (short)Math.Clamp(cols, 20, 500), Y = (short)Math.Clamp(rows, 5, 300) };
        ResizePseudoConsole(_pseudoConsole, size);
    }

    public Task<int> WaitForExitAsync()
    {
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var waitHandle = new ManualResetEvent(false)
        {
            SafeWaitHandle = new SafeWaitHandle(_processHandle, ownsHandle: false)
        };

        RegisteredWaitHandle? registration = null;
        registration = ThreadPool.RegisterWaitForSingleObject(
            waitHandle,
            (_, _) =>
            {
                GetExitCodeProcess(_processHandle, out var exitCode);
                tcs.TrySetResult(unchecked((int)exitCode));
                registration?.Unregister(null);
                waitHandle.Dispose();
            },
            null,
            Timeout.Infinite,
            executeOnlyOnce: true);

        return tcs.Task;
    }

    public void Kill()
    {
        try
        {
            using var process = Process.GetProcessById(ProcessId);
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Already exited.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Closing the pseudoconsole ends the conhost session; the output
        // stream then reads EOF.
        ClosePseudoConsole(_pseudoConsole);
        try
        {
            Input.Dispose();
        }
        catch
        {
        }

        try
        {
            Output.Dispose();
        }
        catch
        {
        }

        DeleteProcThreadAttributeList(_attributeList);
        Marshal.FreeHGlobal(_attributeList);
        CloseHandle(_threadHandle);
        CloseHandle(_processHandle);
    }

    private static string BuildCommandLine(string command, IReadOnlyList<string> args)
    {
        var builder = new StringBuilder();
        AppendArgument(builder, command);
        foreach (var arg in args)
        {
            builder.Append(' ');
            AppendArgument(builder, arg);
        }

        return builder.ToString();
    }

    private static void AppendArgument(StringBuilder builder, string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny([' ', '\t', '"']) < 0)
        {
            builder.Append(argument);
            return;
        }

        builder.Append('"');
        var backslashes = 0;
        foreach (var ch in argument)
        {
            if (ch == '\\')
            {
                backslashes++;
                continue;
            }

            if (ch == '"')
            {
                builder.Append('\\', backslashes * 2 + 1);
                builder.Append('"');
            }
            else
            {
                builder.Append('\\', backslashes);
                builder.Append(ch);
            }

            backslashes = 0;
        }

        builder.Append('\\', backslashes * 2);
        builder.Append('"');
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, IntPtr lpPipeAttributes, int nSize);

    [DllImport("kernel32.dll")]
    private static extern int CreatePseudoConsole(COORD size, SafeFileHandle hInput, SafeFileHandle hOutput, uint dwFlags, out IntPtr phPC);

    [DllImport("kernel32.dll")]
    private static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

    [DllImport("kernel32.dll")]
    private static extern void ClosePseudoConsole(IntPtr hPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessW(
        string? lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFOEX lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
