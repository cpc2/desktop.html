namespace DesktopHtml.Core.Skins;

public sealed record SkinValidationResult(
    bool IsValid,
    SkinManifest? Manifest,
    string SkinDirectory,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public static SkinValidationResult Valid(
        SkinManifest manifest,
        string skinDirectory,
        IReadOnlyList<string>? warnings = null) =>
        new(true, manifest, skinDirectory, Array.Empty<string>(), warnings ?? Array.Empty<string>());

    public static SkinValidationResult Invalid(
        string skinDirectory,
        IReadOnlyList<string> errors,
        SkinManifest? manifest = null,
        IReadOnlyList<string>? warnings = null) =>
        new(false, manifest, skinDirectory, errors, warnings ?? Array.Empty<string>());
}
