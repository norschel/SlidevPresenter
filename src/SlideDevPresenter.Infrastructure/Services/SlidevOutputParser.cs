using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace SlideDevPresenter.Infrastructure.Services;

/// <summary>
/// Detects the Slidev dev server ready line in stdout and extracts its local URL.
/// </summary>
internal static partial class SlidevOutputParser
{
    // Slidev 52+ prints "  public slide show   > http://localhost:3030/";
    // older Slidev (Vite banner) prints "  ➜  Local:   http://localhost:3030/".
    [GeneratedRegex(@"(?:\bpublic\s+slide\s+show\s*>|\bLocal:)\s*(?<url>https?://\S+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReadyLineRegex();

    // CSI sequences such as colors/bold ("\e[36m", "\e[1m"); Slidev also colors parts of the URL itself.
    [GeneratedRegex(@"\x1B\[[0-?]*[ -/]*[@-~]")]
    private static partial Regex AnsiEscapeRegex();

    internal static string StripAnsi(string text) => AnsiEscapeRegex().Replace(text, string.Empty);

    internal static bool TryParseReadyUrl(string? line, [NotNullWhen(true)] out Uri? url)
    {
        url = null;
        if (string.IsNullOrEmpty(line))
            return false;

        var match = ReadyLineRegex().Match(StripAnsi(line));
        if (!match.Success
            || !Uri.TryCreate(match.Groups["url"].Value, UriKind.Absolute, out var candidate)
            || !candidate.IsLoopback)
            return false;

        url = candidate;
        return true;
    }
}
