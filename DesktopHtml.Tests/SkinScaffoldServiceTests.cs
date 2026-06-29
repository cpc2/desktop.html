using DesktopHtml.Core.Skins;

namespace DesktopHtml.Tests;

public sealed class SkinScaffoldServiceTests
{
    [Theory]
    [InlineData("blank")]
    [InlineData("classic")]
    [InlineData("launcher")]
    [InlineData("dashboard")]
    public async Task ScaffoldAsync_CreatesValidSkin(string template)
    {
        using var temp = TempDirectory.Create();
        var target = Path.Combine(temp.Path, template);

        var result = await new SkinScaffoldService().ScaffoldAsync("example.scaffold", target, template);
        var validation = await new SkinValidator().ValidateAsync(result.Directory, strict: true);

        Assert.Equal("example.scaffold", result.SkinId);
        Assert.Equal(template, result.Template);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        Assert.Contains("manifest.json", result.Files);
        Assert.Contains("index.html", result.Files);
    }

    [Fact]
    public async Task ScaffoldAsync_RejectsNonEmptyDirectoryWithoutOverwrite()
    {
        using var temp = TempDirectory.Create();
        var target = Path.Combine(temp.Path, "skin");
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "existing.txt"), "keep me");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SkinScaffoldService().ScaffoldAsync("example.scaffold", target, "blank"));
    }
}
