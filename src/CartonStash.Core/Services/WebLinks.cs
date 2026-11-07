namespace CartonStash.Core.Services;

/// <summary>Which addresses the app may open in the browser.</summary>
public static class WebLinks
{
    /// <summary>Only web links are opened; anything else (file:, ms-settings:…) stays plain text (005 security).</summary>
    public static Uri? SafeUri(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && IsWeb(uri) ? uri : null;

    public static bool IsWeb(Uri uri) =>
        uri.IsAbsoluteUri && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
