namespace DesktopHtml.Core.Execution;

public sealed class ShellExecutionOptions
{
    public string File { get; set; } = "";
    public List<string> Args { get; set; } = new();
    public string? WorkingDirectory { get; set; }
    public string? Verb { get; set; }
    public string ShowWindow { get; set; } = "normal";
    public bool WaitForExit { get; set; }
}

public sealed record ShellExecutionResult(int? ProcessId, int? ExitCode);

public sealed class RunOptions
{
    public string Command { get; set; } = "";
    public List<string> Args { get; set; } = new();
    public string? WorkingDirectory { get; set; }
    public bool WaitForExit { get; set; } = true;
    public bool CaptureOutput { get; set; }
}

public sealed class CommandLineOptions
{
    public string CommandLine { get; set; } = "";
    public string? WorkingDirectory { get; set; }
    public bool WaitForExit { get; set; } = true;
    public bool CaptureOutput { get; set; }
}

public sealed class PowerShellOptions
{
    public string ScriptOrFile { get; set; } = "";
    public string? WorkingDirectory { get; set; }
    public bool WaitForExit { get; set; } = true;
    public bool CaptureOutput { get; set; }
}

public sealed class BatchOptions
{
    public string ScriptOrFile { get; set; } = "";
    public string? WorkingDirectory { get; set; }
    public bool WaitForExit { get; set; } = true;
    public bool CaptureOutput { get; set; }
}

public sealed record RunResult(
    int? ProcessId,
    int? ExitCode,
    string? StandardOutput,
    string? StandardError);
