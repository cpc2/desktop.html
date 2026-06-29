using System.Text.Json;
using System.Text.Json.Nodes;

namespace DesktopHtml.Core.Configuration;

public static class ConfigPatchService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static DesktopHtmlConfig ApplyPatch(DesktopHtmlConfig config, JsonObject patch)
    {
        var root = JsonSerializer.SerializeToNode(config, JsonOptions)?.AsObject()
            ?? throw new InvalidOperationException("Could not serialize config.");

        MergeObject(root, patch);

        return root.Deserialize<DesktopHtmlConfig>(JsonOptions)
            ?? throw new InvalidOperationException("Config patch produced an invalid config.");
    }

    public static DesktopHtmlConfig SetPath(DesktopHtmlConfig config, string dottedPath, JsonNode? value)
    {
        var root = JsonSerializer.SerializeToNode(config, JsonOptions)?.AsObject()
            ?? throw new InvalidOperationException("Could not serialize config.");

        var segments = dottedPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            throw new InvalidOperationException("Config path is required.");
        }

        var current = root;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (current[segments[i]] is not JsonObject child)
            {
                child = new JsonObject();
                current[segments[i]] = child;
            }

            current = child;
        }

        current[segments[^1]] = value?.DeepClone();

        return root.Deserialize<DesktopHtmlConfig>(JsonOptions)
            ?? throw new InvalidOperationException("Config set produced an invalid config.");
    }

    private static void MergeObject(JsonObject target, JsonObject patch)
    {
        foreach (var item in patch)
        {
            if (item.Value is JsonObject patchChild && target[item.Key] is JsonObject targetChild)
            {
                MergeObject(targetChild, patchChild);
                continue;
            }

            target[item.Key] = item.Value?.DeepClone();
        }
    }
}
