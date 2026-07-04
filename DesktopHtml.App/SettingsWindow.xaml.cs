using System.Windows;
using Microsoft.Web.WebView2.Core;
using DesktopHtml.Core;
using DesktopHtml.Core.Configuration;
using DesktopHtml.Core.Skins;

namespace DesktopHtml.App;

public partial class SettingsWindow : Window
{
    private readonly AppPaths _paths;
    private readonly DesktopHtmlConfig _config;
    private readonly WpfDesktopHostActions _hostActions;
    private DesktopBridgeDispatcher? _bridgeDispatcher;

    public SettingsWindow(
        AppPaths paths,
        DesktopHtmlConfig config,
        WpfDesktopHostActions hostActions)
    {
        _paths = paths;
        _config = config;
        _hostActions = hostActions;
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var skinStore = new SkinStore(_paths);
            var skin = await ResolveSettingsBridgeSkinAsync(skinStore).ConfigureAwait(true);
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: _paths.WebViewUserDataDirectory);
            await SettingsWebView.EnsureCoreWebView2Async(environment);

            var dispatcher = new DesktopBridgeDispatcher(_paths, _config, skin, _hostActions);
            _bridgeDispatcher = dispatcher;
            var messenger = new WebViewMessenger(SettingsWebView.CoreWebView2);
            messenger.NavigationStarting += dispatcher.ResetPageState;

            dispatcher.OnPostMessage += (msg) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    messenger.Post(msg);
                });
            };

            SettingsWebView.CoreWebView2.WebMessageReceived += async (_, args) =>
            {
                var response = await dispatcher.DispatchAsync(args.WebMessageAsJson);
                messenger.Post(response);
            };

            await SettingsWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(DesktopBridgeBootstrap.Script);
            SettingsWebView.NavigateToString(SettingsPage.Html);
        }
        catch (Exception ex)
        {
            SettingsWebView.NavigateToString(ErrorPage.Create("desktop.html settings failed to load", ex));
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _bridgeDispatcher?.Dispose();
        _bridgeDispatcher = null;
        base.OnClosed(e);
    }

    private async Task<ResolvedSkin> ResolveSettingsBridgeSkinAsync(SkinStore skinStore)
    {
        if (_config.App.SafeMode)
        {
            return await skinStore.ResolveAsync(SampleSkinConstants.Id, "index.html").ConfigureAwait(false);
        }

        try
        {
            return await skinStore.ResolveActiveSkinAsync(_config).ConfigureAwait(false);
        }
        catch
        {
            return await skinStore.ResolveAsync(SampleSkinConstants.Id, "index.html").ConfigureAwait(false);
        }
    }
}
