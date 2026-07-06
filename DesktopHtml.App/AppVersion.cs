using System.Reflection;

namespace DesktopHtml.App;

public static class AppVersion
{
    // Stamped by the release workflow via -p:Version; falls back to the csproj default for dev builds.
    public static readonly string Current =
        typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0]
        ?? "0.0.0-dev";
}
