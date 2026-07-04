using Microsoft.Web.WebView2.Core;

namespace DesktopHtml.App;

/// <summary>
/// Buffers outbound web messages until the current navigation completes.
/// WebView2 silently drops PostWebMessageAsJson calls made while a navigation
/// is in flight, which loses bridge responses to calls the page makes during
/// its initial script execution. All members must be used from the UI thread.
/// </summary>
public sealed class WebViewMessenger
{
    private readonly CoreWebView2 _webView;
    private readonly List<string> _queue = new();
    private bool _ready;

    public WebViewMessenger(CoreWebView2 webView)
    {
        _webView = webView;
        webView.NavigationStarting += (_, _) =>
        {
            _ready = false;
            NavigationStarting?.Invoke();
        };
        webView.NavigationCompleted += (_, _) =>
        {
            _ready = true;
            if (_queue.Count == 0)
            {
                return;
            }

            var pending = _queue.ToArray();
            _queue.Clear();
            foreach (var message in pending)
            {
                _webView.PostWebMessageAsJson(message);
            }
        };
    }

    /// <summary>Raised when a new navigation starts (e.g. skin reload), so
    /// page-scoped bridge state can be reset.</summary>
    public event Action? NavigationStarting;

    public void Post(string messageJson)
    {
        if (!_ready)
        {
            _queue.Add(messageJson);
            return;
        }

        _webView.PostWebMessageAsJson(messageJson);
    }
}
