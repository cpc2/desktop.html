using System.Net;

namespace DesktopHtml.App;

public static class ErrorPage
{
    public static string Create(string title, Exception exception)
    {
        return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>{{WebUtility.HtmlEncode(title)}}</title>
  <style>
    body {
      margin: 0;
      padding: 40px;
      background: #16191c;
      color: #f7f3e8;
      font-family: "Segoe UI", system-ui, sans-serif;
    }

    h1 {
      margin: 0 0 16px;
      font-size: 28px;
      letter-spacing: 0;
    }

    pre {
      max-width: 920px;
      overflow: auto;
      padding: 16px;
      border: 1px solid rgba(255, 255, 255, 0.14);
      border-radius: 8px;
      background: rgba(255, 255, 255, 0.06);
      white-space: pre-wrap;
    }
  </style>
</head>
<body>
  <h1>{{WebUtility.HtmlEncode(title)}}</h1>
  <pre>{{WebUtility.HtmlEncode(exception.ToString())}}</pre>
</body>
</html>
""";
    }
}
