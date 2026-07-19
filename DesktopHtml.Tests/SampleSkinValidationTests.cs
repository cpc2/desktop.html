using DesktopHtml.Core.Skins;

namespace DesktopHtml.Tests;

/// <summary>Keeps the bundled samples honest: every skin in samples/ must pass
/// the same validation the app applies on install.</summary>
public sealed class SampleSkinValidationTests
{
    [Fact]
    public async Task AllBundledSamples_PassValidation()
    {
        var samplesDirectory = FindSamplesDirectory();
        if (!Directory.Exists(samplesDirectory))
        {
            // samples/ is gitignored, so checkouts without it (CI) have
            // nothing to verify.
            return;
        }

        var skinDirectories = Directory.EnumerateDirectories(samplesDirectory)
            .Where(directory => File.Exists(Path.Combine(directory, "manifest.json")))
            .ToArray();
        Assert.NotEmpty(skinDirectories);

        var validator = new SkinValidator();
        var failures = new List<string>();
        foreach (var directory in skinDirectories)
        {
            var result = await validator.ValidateAsync(directory);
            if (!result.IsValid)
            {
                failures.Add($"{Path.GetFileName(directory)}: {string.Join("; ", result.Errors)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static string FindSamplesDirectory()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "desktop-html.sln")))
            {
                return Path.Combine(directory, "samples");
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
    }
}
