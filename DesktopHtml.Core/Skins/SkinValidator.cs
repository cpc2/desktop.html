using System.Text.Json;
using System.Text.RegularExpressions;

namespace DesktopHtml.Core.Skins;

public sealed class SkinValidator
{
    private static readonly Regex AssetReferencePattern = new(
        """
        (?:
          \b(?:src|href)\s*=\s*["'](?<path>[^"']+)["']
          |
          url\(\s*["']?(?<path>[^"')]+)["']?\s*\)
        )
        """,
        RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled);

    private static readonly Regex PrivateAbsolutePathPattern = new(
        @"[A-Za-z]:\\Users\\[^\\\s""']+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public async Task<SkinValidationResult> ValidateAsync(
        string skinDirectory,
        bool strict = false,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var fullDirectory = Path.GetFullPath(skinDirectory);

        if (!Directory.Exists(fullDirectory))
        {
            return SkinValidationResult.Invalid(fullDirectory, ["Skin directory does not exist."]);
        }

        var manifestFile = Path.Combine(fullDirectory, "manifest.json");
        if (!File.Exists(manifestFile))
        {
            return SkinValidationResult.Invalid(fullDirectory, ["manifest.json is missing."]);
        }

        SkinManifest? manifest;
        try
        {
            await using var stream = File.OpenRead(manifestFile);
            manifest = await JsonSerializer.DeserializeAsync<SkinManifest>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            return SkinValidationResult.Invalid(fullDirectory, [$"manifest.json is invalid JSON: {ex.Message}"]);
        }

        if (manifest is null)
        {
            return SkinValidationResult.Invalid(fullDirectory, ["manifest.json did not contain a manifest object."]);
        }

        if (manifest.SchemaVersion <= 0)
        {
            errors.Add("schemaVersion must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            errors.Add("id is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            errors.Add("name is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Entry))
        {
            errors.Add("entry is required.");
        }
        else if (!IsSafeRelativePath(manifest.Entry))
        {
            errors.Add("entry must be a relative path inside the skin folder.");
        }
        else if (!File.Exists(Path.Combine(fullDirectory, manifest.Entry)))
        {
            errors.Add($"entry file does not exist: {manifest.Entry}");
        }

        foreach (var entry in manifest.Entries)
        {
            if (!IsSafeRelativePath(entry.Value))
            {
                errors.Add($"named entry '{entry.Key}' must point inside the skin folder.");
            }
            else if (!File.Exists(Path.Combine(fullDirectory, entry.Value)))
            {
                errors.Add($"named entry '{entry.Key}' file does not exist: {entry.Value}");
            }
        }

        if (strict)
        {
            await AddStrictValidationAsync(fullDirectory, errors, warnings, cancellationToken)
                .ConfigureAwait(false);
        }

        return errors.Count == 0
            ? SkinValidationResult.Valid(manifest, fullDirectory, warnings)
            : SkinValidationResult.Invalid(fullDirectory, errors, manifest, warnings);
    }

    public static bool IsSafeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            return false;
        }

        var normalized = relativePath.Replace('\\', '/');
        return !normalized.Split('/').Any(part => part is ".." or "");
    }

    private static async Task AddStrictValidationAsync(
        string skinDirectory,
        List<string> errors,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        foreach (var file in Directory.EnumerateFiles(skinDirectory, "*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file).ToLowerInvariant();
            if (extension is not (".html" or ".htm" or ".css" or ".js" or ".json"))
            {
                continue;
            }

            var relativeFile = Path.GetRelativePath(skinDirectory, file);
            var content = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            if (PrivateAbsolutePathPattern.IsMatch(content))
            {
                warnings.Add($"Private absolute path found in {relativeFile}.");
            }

            foreach (Match match in AssetReferencePattern.Matches(content))
            {
                var reference = match.Groups["path"].Value.Trim();
                if (string.IsNullOrWhiteSpace(reference) || ShouldIgnoreAssetReference(reference))
                {
                    continue;
                }

                if (IsRemoteReference(reference))
                {
                    warnings.Add($"Remote asset reference in {relativeFile}: {reference}");
                    continue;
                }

                if (Path.IsPathRooted(reference) || reference.Contains(':'))
                {
                    warnings.Add($"Absolute asset reference in {relativeFile}: {reference}");
                    continue;
                }

                var referencePath = reference.Split('#')[0].Split('?')[0];
                if (!IsSafeRelativePath(referencePath))
                {
                    errors.Add($"Referenced asset path escapes the skin folder in {relativeFile}: {reference}");
                    continue;
                }

                var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, referencePath));
                if (!resolved.StartsWith(skinDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(resolved))
                {
                    errors.Add($"Referenced asset does not exist in {relativeFile}: {reference}");
                }
            }

            if (extension == ".js")
            {
                var syntaxError = FindObviousJavaScriptSyntaxError(content);
                if (syntaxError is not null)
                {
                    errors.Add($"JavaScript syntax check failed in {relativeFile}: {syntaxError}");
                }
            }
        }
    }

    private static bool ShouldIgnoreAssetReference(string reference) =>
        reference.StartsWith('#')
        || reference.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
        || reference.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
        || reference.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
        || reference.StartsWith("about:", StringComparison.OrdinalIgnoreCase);

    private static bool IsRemoteReference(string reference) =>
        reference.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || reference.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || reference.StartsWith("//", StringComparison.Ordinal);

    private static string? FindObviousJavaScriptSyntaxError(string content)
    {
        var stack = new Stack<char>();
        var inString = false;
        var stringQuote = '\0';
        var inLineComment = false;
        var inBlockComment = false;
        var escaped = false;

        for (var i = 0; i < content.Length; i++)
        {
            var current = content[i];
            var next = i + 1 < content.Length ? content[i + 1] : '\0';

            if (inLineComment)
            {
                if (current is '\r' or '\n')
                {
                    inLineComment = false;
                }

                continue;
            }

            if (inBlockComment)
            {
                if (current == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                }

                continue;
            }

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (current == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (current == stringQuote)
                {
                    inString = false;
                    stringQuote = '\0';
                    continue;
                }

                if (stringQuote != '`' && current is ('\r' or '\n'))
                {
                    return "unterminated string literal";
                }

                continue;
            }

            if (current == '/' && next == '/')
            {
                inLineComment = true;
                i++;
                continue;
            }

            if (current == '/' && next == '*')
            {
                inBlockComment = true;
                i++;
                continue;
            }

            if (current is '"' or '\'' or '`')
            {
                inString = true;
                stringQuote = current;
                continue;
            }

            if (current is '(' or '{' or '[')
            {
                stack.Push(current);
                continue;
            }

            if (current is ')' or '}' or ']')
            {
                if (stack.Count == 0)
                {
                    return $"unexpected '{current}'";
                }

                var opener = stack.Pop();
                if (!MatchesPair(opener, current))
                {
                    return $"expected '{ClosingFor(opener)}' before '{current}'";
                }
            }
        }

        if (inString)
        {
            return "unterminated string literal";
        }

        if (inBlockComment)
        {
            return "unterminated block comment";
        }

        return stack.Count == 0 ? null : $"missing '{ClosingFor(stack.Peek())}'";
    }

    private static bool MatchesPair(char opener, char closer) =>
        (opener == '(' && closer == ')')
        || (opener == '{' && closer == '}')
        || (opener == '[' && closer == ']');

    private static char ClosingFor(char opener) =>
        opener switch
        {
            '(' => ')',
            '{' => '}',
            '[' => ']',
            _ => '?'
        };
}
