using System.Text.Json;

namespace DesktopHtml.Core.Bridge;

public sealed class BridgeRequest
{
    public string Id { get; set; } = "";
    public string Method { get; set; } = "";
    public JsonElement Params { get; set; }
}

public sealed record BridgeError(string Code, string Message, object? Details = null);

public sealed record BridgeResponse(string Id, bool Ok, object? Result = null, BridgeError? Error = null);

public sealed record RuntimeInfo(
    string AppVersion,
    string AppDataRoot,
    string ConfigPath,
    string ActiveSkinId,
    string ActiveSkinPath,
    string PlacementMode,
    string DesktopPath,
    string PublicDesktopPath);
