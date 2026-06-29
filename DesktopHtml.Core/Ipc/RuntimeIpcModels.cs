using System.Text.Json.Nodes;

namespace DesktopHtml.Core.Ipc;

public static class RuntimeIpcDefaults
{
    public const string PipeName = "desktop-html-runtime-command";
}

public sealed class RuntimeCommandRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Command { get; set; } = "";
    public JsonObject? Params { get; set; }
}

public sealed class RuntimeCommandResponse
{
    public string Id { get; set; } = "";
    public bool Ok { get; set; }
    public JsonNode? Result { get; set; }
    public RuntimeCommandError? Error { get; set; }
}

public sealed record RuntimeCommandError(string Code, string Message);
