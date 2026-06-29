using System.Text.Json;

namespace DesktopHtml.Core.Configuration;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly AppPaths _paths;

    public ConfigService(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<DesktopHtmlConfig> LoadOrCreateAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();

        if (!File.Exists(_paths.ConfigFile))
        {
            var created = CreateDefault();
            await SaveAsync(created, cancellationToken).ConfigureAwait(false);
            return created;
        }

        await using var stream = File.OpenRead(_paths.ConfigFile);
        var config = await JsonSerializer.DeserializeAsync<DesktopHtmlConfig>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return config ?? CreateDefault();
    }

    public async Task SaveAsync(DesktopHtmlConfig config, CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        var tempFile = $"{_paths.ConfigFile}.{Guid.NewGuid():N}.tmp";

        await using (var stream = File.Create(tempFile))
        {
            await JsonSerializer.SerializeAsync(stream, config, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(tempFile, _paths.ConfigFile, overwrite: true);
    }

    public static DesktopHtmlConfig CreateDefault() => new();
}
