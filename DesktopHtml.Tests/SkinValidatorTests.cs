using DesktopHtml.Core;
using DesktopHtml.Core.Skins;

namespace DesktopHtml.Tests;

public sealed class SkinValidatorTests
{
    [Fact]
    public async Task ValidateAsync_AcceptsBundledSampleSkin()
    {
        using var temp = TempDirectory.Create();
        var paths = CreatePaths(temp.Path);
        await new SampleSkinWriter(paths).EnsureInstalledAsync();

        var result = await new SkinValidator().ValidateAsync(
            Path.Combine(paths.SkinsDirectory, SampleSkinConstants.Id));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal(SampleSkinConstants.Id, result.Manifest?.Id);
    }

    [Theory]
    [InlineData("../outside.html")]
    [InlineData("/absolute.html")]
    [InlineData("")]
    public void IsSafeRelativePath_RejectsUnsafePaths(string path)
    {
        Assert.False(SkinValidator.IsSafeRelativePath(path));
    }

    [Fact]
    public void IsSafeRelativePath_AcceptsNestedRelativePath()
    {
        Assert.True(SkinValidator.IsSafeRelativePath("pages/index.html"));
    }

    [Fact]
    public async Task ValidateAsync_Strict_FlagsMissingReferencedAssets()
    {
        using var temp = TempDirectory.Create();
        var skin = CreateSkin(temp.Path);
        await File.WriteAllTextAsync(Path.Combine(skin, "index.html"), """
<!doctype html>
<link rel="stylesheet" href="missing.css">
<script src="script.js"></script>
""");
        await File.WriteAllTextAsync(Path.Combine(skin, "script.js"), "console.log('ok');");

        var result = await new SkinValidator().ValidateAsync(skin, strict: true);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("missing.css"));
    }

    [Fact]
    public async Task ValidateAsync_Strict_FlagsObviousJavaScriptSyntaxErrors()
    {
        using var temp = TempDirectory.Create();
        var skin = CreateSkin(temp.Path);
        await File.WriteAllTextAsync(Path.Combine(skin, "script.js"), "function broken() {");

        var result = await new SkinValidator().ValidateAsync(skin, strict: true);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("JavaScript syntax check failed"));
    }

    [Fact]
    public async Task ValidateAsync_Strict_WarnsForPrivatePathsAndRemoteAssets()
    {
        using var temp = TempDirectory.Create();
        var skin = CreateSkin(temp.Path);
        await File.WriteAllTextAsync(Path.Combine(skin, "index.html"), """
<!doctype html>
<img src="https://example.com/image.png">
<script>const path = "C:\Users\Example\secret.txt";</script>
""");

        var result = await new SkinValidator().ValidateAsync(skin, strict: true);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.Contains(result.Warnings, warning => warning.Contains("Remote asset"));
        Assert.Contains(result.Warnings, warning => warning.Contains("Private absolute path"));
    }

    private static string CreateSkin(string root)
    {
        var skinRoot = Path.Combine(root, "skin");
        Directory.CreateDirectory(skinRoot);
        File.WriteAllText(Path.Combine(skinRoot, "manifest.json"), """
{
  "schemaVersion": 1,
  "id": "example.strict",
  "name": "Strict Example",
  "version": "0.1.0",
  "author": "test",
  "entry": "index.html",
  "permissions": {
    "fullTrust": true,
    "network": true,
    "rawExecution": true
  }
}
""");
        File.WriteAllText(Path.Combine(skinRoot, "index.html"), "<!doctype html><title>Strict Example</title>");
        return skinRoot;
    }

    private static AppPaths CreatePaths(string root)
    {
        return new AppPaths(
            root,
            Path.Combine(root, "config.json"),
            Path.Combine(root, "state.json"),
            Path.Combine(root, "logs"),
            Path.Combine(root, "skins"),
            Path.Combine(root, "storage"),
            Path.Combine(root, "backups"),
            Path.Combine(root, "webview2"));
    }
}
