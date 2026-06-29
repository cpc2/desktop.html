using System.Windows;
using DesktopHtml.Core;
using DesktopHtml.Core.Configuration;
using DesktopHtml.Core.Logging;
using DesktopHtml.Core.Skins;

namespace DesktopHtml.App;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var paths = AppPaths.CreateDefault();
        paths.EnsureCreated();

        var configService = new ConfigService(paths);
        var logService = new LogService(paths);
        logService.InfoAsync("app", "desktop.html starting.", new
        {
            AppVersion.Current,
            args = args.Length
        }).GetAwaiter().GetResult();
        new SampleSkinWriter(paths).EnsureInstalledAsync().GetAwaiter().GetResult();

        if (args.Length > 0)
        {
            return CliRunner.RunAsync(args, paths, configService).GetAwaiter().GetResult();
        }

        var app = new App();
        return app.Run();
    }
}
