namespace SlideDevPresenter.App.Services;

public sealed class WebViewNavigationPolicy
{
    public bool ShouldOpenExternally(Uri request, Uri? current)
    {
        if (!request.IsAbsoluteUri)
            return !IsInternalRelative(request);

        if (!IsWebScheme(request))
            return false;

        if (IsInternalSlideNavigation(request, current))
            return false;

        return true;
    }

    internal static bool IsInternalSlideNavigation(Uri request, Uri? current)
    {
        if (!request.IsAbsoluteUri)
            return IsInternalRelative(request);

        if (!HasSlideFragment(request))
            return false;

        if (IsLocalSlideHost(request))
            return true;

        return current is { IsAbsoluteUri: true } && IsSameOrigin(request, current);
    }

    private static bool IsInternalRelative(Uri request)
    {
        var value = request.OriginalString;
        return value.StartsWith("#/", StringComparison.Ordinal) || value.StartsWith("/#/", StringComparison.Ordinal);
    }

    private static bool HasSlideFragment(Uri uri) => uri.Fragment.StartsWith("#/", StringComparison.Ordinal);

    private static bool IsLocalSlideHost(Uri uri) =>
        string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);

    private static bool IsSameOrigin(Uri a, Uri b) =>
        string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase) &&
        a.Port == b.Port;

    private static bool IsWebScheme(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
