using System.Windows;
using Microsoft.Web.WebView2.Core;
using DesktopHtml.Core;
using DesktopHtml.Core.Configuration;
using DesktopHtml.Core.Monitors;
using DesktopHtml.Core.Skins;

namespace DesktopHtml.App;

public partial class MainWindow : Window
{
    private readonly AppPaths _paths;
    private readonly DesktopHtmlConfig _config;
    private readonly ResolvedSkin _skin;
    private readonly WpfDesktopHostActions _hostActions;
    private readonly DesktopPlacementService _placementService;
    private MonitorSnapshot? _monitor;
    private DesktopBridgeDispatcher? _bridgeDispatcher;

    public MainWindow(
        AppPaths paths,
        DesktopHtmlConfig config,
        ResolvedSkin skin,
        MonitorSnapshot? monitor,
        WpfDesktopHostActions hostActions,
        DesktopPlacementService placementService)
    {
        _paths = paths;
        _config = config;
        _skin = skin;
        _monitor = monitor;
        _hostActions = hostActions;
        _placementService = placementService;
        _hostActions.RegisterWindow(this);
        InitializeComponent();

        ApplyTitle();
    }

    public string? MonitorId => _monitor?.Id;
    public MonitorSnapshot? CurrentMonitor => _monitor;
    public string SkinId => _skin.Manifest.Id;
    public string SkinEntry => _skin.Entry;
    public event EventHandler? HostMinimized;

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyDesktopPlacement();
        await InitializeWebViewAsync();
    }

    private void ApplyDesktopPlacement() => _placementService.ApplyPlacement(this, _config);

    public void ReapplyDesktopPlacement() => ApplyDesktopPlacement();

    private async Task InitializeWebViewAsync()
    {
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: _paths.WebViewUserDataDirectory);

            await DesktopWebView.EnsureCoreWebView2Async(environment);
            var dispatcher = new DesktopBridgeDispatcher(_paths, _config, _skin, _hostActions, () => _monitor);
            _bridgeDispatcher = dispatcher;
            var messenger = new WebViewMessenger(DesktopWebView.CoreWebView2);

            // A reload restarts the page; terminal sessions, watchers, and
            // stats subscriptions belong to the old page and must not leak.
            messenger.NavigationStarting += dispatcher.ResetPageState;

            dispatcher.OnPostMessage += (msg) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    messenger.Post(msg);
                });
            };

            DesktopWebView.CoreWebView2.WebMessageReceived += async (_, args) =>
            {
                var response = await dispatcher.DispatchAsync(args.WebMessageAsJson);
                messenger.Post(response);
            };

            await DesktopWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(DesktopBridgeBootstrap.Script);
            DesktopWebView.Source = new Uri(_skin.EntryFile);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"desktop.html WebView2 initialization failed: {ex}");
            System.Windows.MessageBox.Show($"desktop.html WebView2 initialization failed:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "desktop.html Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    public Task ReloadSkinAsync()
    {
        return Dispatcher.InvokeAsync(() => DesktopWebView.Reload()).Task;
    }

    public void ApplyMonitorSnapshot(MonitorSnapshot? monitor)
    {
        _monitor = monitor;
        ApplyTitle();
        if (IsLoaded)
        {
            ApplyDesktopPlacement();
        }
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized)
        {
            HostMinimized?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ApplyTitle()
    {
        Title = _monitor is null ? "desktop.html" : $"desktop.html - {_monitor.Id}";
    }

    protected override void OnClosed(EventArgs e)
    {
        _bridgeDispatcher?.Dispose();
        _bridgeDispatcher = null;
        _hostActions.UnregisterWindow(this);
        base.OnClosed(e);
    }
}
