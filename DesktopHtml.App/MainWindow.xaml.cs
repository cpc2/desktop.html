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
    private bool _closed;

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

    private void ApplyDesktopPlacement()
    {
        RefreshMonitorSnapshot();
        _placementService.ApplyPlacement(this, _config);
    }

    public void ReapplyDesktopPlacement() => ApplyDesktopPlacement();

    public void RefreshMonitorSnapshot()
    {
        if (_monitor is null || string.Equals(_monitor.Id, "span", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var refreshed = new MonitorService().GetMonitors()
            .FirstOrDefault(candidate => string.Equals(candidate.Id, _monitor.Id, StringComparison.OrdinalIgnoreCase));
        if (refreshed is not null)
        {
            _monitor = refreshed;
            ApplyTitle();
        }
    }

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
                    PostToPage(messenger, msg);
                });
            };

            DesktopWebView.CoreWebView2.WebMessageReceived += async (_, args) =>
            {
                var messageJson = args.WebMessageAsJson;
                if (messenger.TryHandleHello(messageJson))
                {
                    return;
                }

                var response = await dispatcher.DispatchAsync(messageJson);
                PostToPage(messenger, response);
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

    /// <summary>
    /// Posts a message to the page unless the window is already closed.
    /// Background event sources (stats timer, terminal output, media) can
    /// queue posts on the UI dispatcher that run after the WebView2 control
    /// is disposed during window teardown (e.g. skin activation replaces the
    /// host windows); those must not crash the process.
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

    /// <summary>
    /// The host must keep filling its monitor in physical pixels; WPF's
    /// default WM_DPICHANGED handling resizes the window to preserve its DIP
    /// size instead. Deferred so placement is not re-entered while the move
    /// that triggered the DPI change is still inside SetWindowPos.
    /// </summary>
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        if (IsLoaded)
        {
            Dispatcher.BeginInvoke(ApplyDesktopPlacement, System.Windows.Threading.DispatcherPriority.Background);
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
        _closed = true;
        _bridgeDispatcher?.Dispose();
        _bridgeDispatcher = null;
        _hostActions.UnregisterWindow(this);
        base.OnClosed(e);
    }
}
