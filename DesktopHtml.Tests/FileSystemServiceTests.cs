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
    public async Task WriteBytesAsync_WritesBinaryContent()
    {
        using var temp = TempDirectory.Create();
        var service = new FileSystemService();
        var path = Path.Combine(temp.Path, "nested", "blob.bin");
        var bytes = new byte[] { 0, 1, 2, 255, 128, 10, 13 };

        var savedPath = await service.WriteBytesAsync(path, bytes);

        Assert.Equal(path, savedPath);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task WriteBytesAsync_UniqueAvoidsOverwritingExistingFiles()
    {
        using var temp = TempDirectory.Create();
        var service = new FileSystemService();
        var path = Path.Combine(temp.Path, "image.png");

        var first = await service.WriteBytesAsync(path, [1], unique: true);
        var second = await service.WriteBytesAsync(path, [2], unique: true);
        var third = await service.WriteBytesAsync(path, [3], unique: true);

        Assert.Equal(path, first);
        Assert.Equal(Path.Combine(temp.Path, "image (2).png"), second);
        Assert.Equal(Path.Combine(temp.Path, "image (3).png"), third);
        Assert.Equal(new byte[] { 1 }, await File.ReadAllBytesAsync(first));
        Assert.Equal(new byte[] { 2 }, await File.ReadAllBytesAsync(second));
        Assert.Equal(new byte[] { 3 }, await File.ReadAllBytesAsync(third));
    }

    [Fact]
    public void ListDesktopItems_MergesDirectoriesAndSkipsShellMetadata()
    {
        using var temp = TempDirectory.Create();
        var service = new FileSystemService();
        var userDesktop = Path.Combine(temp.Path, "Desktop");
        var publicDesktop = Path.Combine(temp.Path, "PublicDesktop");
        Directory.CreateDirectory(userDesktop);
        Directory.CreateDirectory(publicDesktop);
        File.WriteAllText(Path.Combine(userDesktop, "user-file.txt"), "u");
        File.WriteAllText(Path.Combine(userDesktop, "desktop.ini"), "[shell]");
        File.WriteAllText(Path.Combine(publicDesktop, "public-file.txt"), "p");
        Directory.CreateDirectory(Path.Combine(publicDesktop, "Shared Folder"));

        var items = service.ListDesktopItems(userDesktop, publicDesktop);

        Assert.Equal(3, items.Count);
        Assert.DoesNotContain(items, item => item.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(items, item => item.Name == "user-file.txt");
        Assert.Contains(items, item => item.Name == "public-file.txt");
        // Directories sort before files.
        Assert.True(items[0].IsDirectory);
    }

    [Fact]
    public void ListDesktopItems_DeduplicatesRepeatedAndMissingPaths()
    {
        using var temp = TempDirectory.Create();
        var service = new FileSystemService();
        var desktop = Path.Combine(temp.Path, "Desktop");
        Directory.CreateDirectory(desktop);
        File.WriteAllText(Path.Combine(desktop, "only-once.txt"), "x");
        var missing = Path.Combine(temp.Path, "does-not-exist");

        var items = service.ListDesktopItems(desktop, desktop.ToUpperInvariant(), missing, "");

        Assert.Single(items);
        Assert.Equal("only-once.txt", items[0].Name);
    }

    [Fact]
    public void ShortcutResolver_ReadsUrlFromInternetShortcut()
    {
        using var temp = TempDirectory.Create();
        var urlFile = Path.Combine(temp.Path, "Example.url");
        File.WriteAllLines(urlFile, ["[InternetShortcut]", "URL=https://example.com/page"]);

        Assert.Equal("https://example.com/page", ShortcutResolver.GetUrlFromInternetShortcut(urlFile));
        Assert.Null(ShortcutResolver.ResolveUrlShortcutDirectory(urlFile));
    }

    [Fact]
    public void GetUniquePath_ReturnsOriginalWhenFree()
    {
        using var temp = TempDirectory.Create();
        var path = Path.Combine(temp.Path, "free.txt");

        Assert.Equal(path, FileSystemService.GetUniquePath(path));
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

    [Fact]
    public void ListDirectory_TreatsShortcutToDirectoryAsDirectory()
    {
        using var temp = TempDirectory.Create();
        var service = new FileSystemService();
        var target = Path.Combine(temp.Path, "Target Folder");
        var link = Path.Combine(temp.Path, "Target Folder.lnk");
        var child = Path.Combine(target, "child.txt");

        service.CreateDirectory(target);
        File.WriteAllText(child, "hello");
        if (!TryCreateShortcut(link, target))
        {
            return;
        }

        var entries = service.ListDirectory(temp.Path);
        Assert.Contains(entries, entry => entry.Name == "Target Folder" && entry.FullPath == link && entry.IsDirectory);

        var children = service.ListDirectory(link);
        Assert.Contains(children, entry => entry.Name == "child.txt" && !entry.IsDirectory);
    }

    [Fact]
    public void ListDirectory_TreatsExplorerShortcutArgumentToDirectoryAsDirectory()
    {
        using var temp = TempDirectory.Create();
        var service = new FileSystemService();
        var target = Path.Combine(temp.Path, "Downloads");
        var link = Path.Combine(temp.Path, "Downloads.lnk");
        var child = Path.Combine(target, "download.txt");
        var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

        if (!File.Exists(explorer))
        {
            return;
        }

        service.CreateDirectory(target);
        File.WriteAllText(child, "hello");
        if (!TryCreateShortcut(link, explorer, $"\"{target}\""))
        {
            return;
        }

        var entries = service.ListDirectory(temp.Path);
        Assert.Contains(entries, entry => entry.Name == "Downloads" && entry.FullPath == link && entry.IsDirectory);

        var children = service.ListDirectory(link);
        Assert.Contains(children, entry => entry.Name == "download.txt" && !entry.IsDirectory);
    }

    [Fact]
    public void ListDirectory_PreservesDottedDirectoryNames()
    {
        using var temp = TempDirectory.Create();
        var service = new FileSystemService();
        var directory = Path.Combine(temp.Path, "folder.with.dots");

        service.CreateDirectory(directory);

        var entries = service.ListDirectory(temp.Path);

        Assert.Contains(entries, entry => entry.Name == "folder.with.dots" && entry.IsDirectory);
    }

    [Fact]
    public void ListDesktopItems_MergesDesktopRootsAndHidesShellMetadata()
    {
        using var temp = TempDirectory.Create();
        var service = new FileSystemService();
        var userDesktop = Path.Combine(temp.Path, "user-desktop");
        var publicDesktop = Path.Combine(temp.Path, "public-desktop");
        var missingDesktop = Path.Combine(temp.Path, "missing-desktop");

        service.CreateDirectory(userDesktop);
        service.CreateDirectory(publicDesktop);
        File.WriteAllText(Path.Combine(userDesktop, "Personal App.lnk"), "");
        File.WriteAllText(Path.Combine(userDesktop, "desktop.ini"), "");
        File.WriteAllText(Path.Combine(publicDesktop, "Shared App.lnk"), "");
        File.WriteAllText(Path.Combine(publicDesktop, "desktop.ini"), "");

        var entries = service.ListDesktopItems(userDesktop, publicDesktop, missingDesktop);

        Assert.Contains(entries, entry => entry.Name == "Personal App.lnk" && entry.SourceDirectory == userDesktop);
        Assert.Contains(entries, entry => entry.Name == "Shared App.lnk" && entry.SourceDirectory == publicDesktop);
        Assert.DoesNotContain(entries, entry => string.Equals(entry.Name, "desktop.ini", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryCreateShortcut(string shortcutPath, string targetPath, string? arguments = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            return false;
        }

        try
        {
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            if (arguments is not null)
            {
                shortcut.Arguments = arguments;
            }
            shortcut.Save();
            return File.Exists(shortcutPath);
        }
        catch
        {
            return false;
        }
    }
}
