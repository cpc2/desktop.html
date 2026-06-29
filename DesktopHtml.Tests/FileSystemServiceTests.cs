using System.Text.Json.Nodes;
using DesktopHtml.Core.FileSystem;

namespace DesktopHtml.Tests;

public sealed class FileSystemServiceTests
{
    [Fact]
    public async Task TextAndJsonOperations_RoundTrip()
    {
        using var temp = TempDirectory.Create();
        var service = new FileSystemService();
        var textPath = Path.Combine(temp.Path, "nested", "note.txt");
        var jsonPath = Path.Combine(temp.Path, "nested", "data.json");

        await service.WriteTextAsync(textPath, "hello");
        await service.WriteJsonAsync(jsonPath, new JsonObject { ["answer"] = 42 });

        Assert.True(service.Exists(textPath));
        Assert.Equal("hello", await service.ReadTextAsync(textPath));
        Assert.Equal(42, (await service.ReadJsonAsync(jsonPath))?["answer"]?.GetValue<int>());
    }

    [Fact]
    public void DirectoryOperations_CopyMoveListAndDelete()
    {
        using var temp = TempDirectory.Create();
        var service = new FileSystemService();
        var source = Path.Combine(temp.Path, "source");
        var copied = Path.Combine(temp.Path, "copied");
        var moved = Path.Combine(temp.Path, "moved");

        service.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "file.txt"), "content");

        service.CopyPath(source, copied);
        service.MovePath(copied, moved);
        var entries = service.ListDirectory(moved);

        Assert.Contains(entries, entry => entry.Name == "file.txt" && !entry.IsDirectory);

        service.DeletePath(moved, new DeletePathOptions { Recursive = true });
        Assert.False(service.Exists(moved));
    }
}
