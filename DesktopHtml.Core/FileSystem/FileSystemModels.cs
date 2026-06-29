namespace DesktopHtml.Core.FileSystem;

public sealed class DeletePathOptions
{
    public bool Recursive { get; set; }
}

public sealed record DirectoryEntryInfo(
    string Name,
    string FullPath,
    bool IsDirectory,
    long? Size,
    DateTimeOffset LastWriteTime);
