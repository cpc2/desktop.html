using Microsoft.Web.WebView2.Core;

namespace DesktopHtml.App;

/// <summary>
/// Manages the web-message channel for one page lifetime, in both directions.
/// WebView2 silently drops messages while a navigation is in flight — host
/// posts made before NavigationCompleted never reach the page, and page posts
/// made while the document is still parsing never reach the host. Outbound
/// messages are buffered here until the navigation completes; the page-side
/// bootstrap buffers its own calls until it receives the "bridgeReady" event
/// this class sends on completion. All members must be used from the UI thread.
/// </summary>
public sealed class WebViewMessenger
{
    private const string BridgeReadyMessage = """{"type":"bridgeReady"}""";

    private readonly CoreWebView2 _webView;
    private readonly List<string> _queue = new();
    private bool _ready;
    private ulong _currentNavigationId;

    public WebViewMessenger(CoreWebView2 webView)
    {
        _webView = webView;
        webView.NavigationStarting += (_, args) =>
        {
            _currentNavigationId = args.NavigationId;
            _ready = false;
            NavigationStarting?.Invoke();
        };
        webView.NavigationCompleted += (_, args) =>
        {
            // A completion for an older navigation (e.g. the implicit
            // about:blank load right after CoreWebView2 creation) must not
            // mark the messenger ready while the real page is still loading.
            if (args.NavigationId != _currentNavigationId)
            {
                return;
            }

            ConfirmReady();
        };
    }

    /// <summary>Raised when a new navigation starts (e.g. skin reload), so
    /// page-scoped bridge state can be reset.</summary>
    public event Action? NavigationStarting;

    /// <summary>
    /// Handles the page-side bootstrap's "bridgeHello" ping. Receiving any
    /// page message proves the channel is open in both directions, which
    /// NavigationCompleted alone does not: posts made from that event handler
    /// can still be dropped for NavigateToString pages. Returns true if the
    /// message was the handshake and needs no dispatch.
    /// </summary>
    public bool TryHandleHello(string messageJson)
    {
        if (!messageJson.Contains("\"bridgeHello\"", StringComparison.Ordinal))
        {
            return false;
        }

        ConfirmReady();
        return true;
    }

    private void ConfirmReady()
    {
        _ready = true;
        _webView.PostWebMessageAsJson(BridgeReadyMessage);
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
    }

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
