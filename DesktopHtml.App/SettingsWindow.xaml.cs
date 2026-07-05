using System.IO;
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
    private bool _closed;

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
                    PostToPage(messenger, msg);
                });
            };

            SettingsWebView.CoreWebView2.WebMessageReceived += async (_, args) =>
            {
                var messageJson = args.WebMessageAsJson;
                if (messenger.TryHandleHello(messageJson))
                {
                    return;
                }

                var response = await dispatcher.DispatchAsync(messageJson);
                PostToPage(messenger, response);
            };

            await SettingsWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(DesktopBridgeBootstrap.Script);

            // Serve settings from a real file. NavigateToString loads the page
            // as a data: URI with an opaque origin, and WebView2 drops the
            // page's early chrome.webview.postMessage calls for such pages —
            // the initial data load never reaches the host. file:// pages
            // (like skins) deliver web messages reliably.
            var settingsFile = Path.Combine(_paths.Root, "settings.html");
            await File.WriteAllTextAsync(settingsFile, SettingsPage.Html).ConfigureAwait(true);
            SettingsWebView.Source = new Uri(settingsFile);
        }
        catch (Exception ex)
        {
            SettingsWebView.NavigateToString(ErrorPage.Create("desktop.html settings failed to load", ex));
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _bridgeDispatcher?.Dispose();
        _bridgeDispatcher = null;
        base.OnClosed(e);
    }

    /// <summary>
    /// Posts a message to the page unless the window is already closed; see
    /// <see cref="MainWindow"/> for the teardown race this guards against.
    /// </summary>
    private void PostToPage(WebViewMessenger messenger, string messageJson)
    {
        if (_closed)
        {
            return;
        }

        try
        {
            messenger.Post(messageJson);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // The WebView2 was disposed between the closed check and the post.
        }
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
