using System.Text.Json;
using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.Core.Services;

public static class PresentationSourceListSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly HashSet<string> SensitiveQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "access_token", "refresh_token", "token", "api_key", "apikey", "password", "passwd",
        "client_secret", "authorization", "auth", "auth_token", "oauth_token", "oauth_signature",
        "signature", "sig"
    };

    public static async Task ExportAsync(
        IEnumerable<PresentationSource> sources,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        var entries = sources
            .Select(source => TryCreateEntry(source, out var entry) ? entry : null)
            .OfType<SourceListEntry>()
            .ToList();

        await JsonSerializer.SerializeAsync(
            destination,
            new SourceListDocument { Version = 1, Sources = entries! },
            JsonOptions,
            cancellationToken);
    }

    public static async Task<IReadOnlyList<PresentationSource>> ImportAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        var document = await JsonSerializer.DeserializeAsync<SourceListDocument>(
                           source,
                           JsonOptions,
                           cancellationToken)
                       ?? throw new InvalidDataException("The source list is empty or invalid.");

        if (document.Version != 1 || document.Sources is null)
            throw new InvalidDataException("The source list has an unsupported format.");

        var result = new List<PresentationSource>();
        foreach (var entry in document.Sources)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.Title)
                || string.IsNullOrWhiteSpace(entry.Address) || string.IsNullOrWhiteSpace(entry.Type)
                || entry.Comment is null)
                throw new InvalidDataException("A source entry is missing required information.");

            if (!TrySanitizeAddress(entry.Address, out var address, out var isGitHub, out var isHttp))
                throw new InvalidDataException("Source addresses must be valid HTTP or HTTPS URLs.");

            var type = entry.Type.ToLowerInvariant();
            if (type switch
                {
                    "github" => !isGitHub,
                    "http" => isGitHub || !isHttp,
                    "https" => isGitHub || isHttp,
                    _ => true
                })
                throw new InvalidDataException("Source types must match their HTTP or HTTPS address.");

            result.Add(new PresentationSource
            {
                Id = Guid.NewGuid(),
                Name = entry.Title.Trim(),
                Type = PresentationSourceType.HostedUrl,
                Location = address.Value,
                IsEnabled = true,
                Comment = entry.Comment,
                IsPublic = entry.IsPublic,
                RequiresAuthentication = entry.RequiresAuthentication || address.AuthenticationRemoved
            });
        }

        return result;
    }

    private static bool TryCreateEntry(PresentationSource source, out SourceListEntry? entry)
    {
        entry = null;
        if (!TrySanitizeAddress(source.Location, out var address, out var isGitHub, out _))
            return false;

        entry = new SourceListEntry
        {
            Title = string.IsNullOrWhiteSpace(source.Name) ? address.Value : source.Name,
            Address = address.Value,
            Type = isGitHub ? "github" : address.IsHttp ? "http" : "https",
            Comment = source.Comment,
            IsPublic = source.IsPublic,
            RequiresAuthentication = source.RequiresAuthentication || address.AuthenticationRemoved
        };
        return true;
    }

    private static bool TrySanitizeAddress(
        string value,
        out SanitizedAddress address,
        out bool isGitHub,
        out bool isHttp)
    {
        address = default;
        isGitHub = false;
        isHttp = false;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return false;

        isHttp = uri.Scheme == Uri.UriSchemeHttp;
        isGitHub = uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                   || uri.Host.Equals("www.github.com", StringComparison.OrdinalIgnoreCase);

        var removedAuthentication = !string.IsNullOrEmpty(uri.UserInfo);
        var builder = new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty };
        builder.Query = FilterSensitiveParameters(uri.Query.TrimStart('?'), ref removedAuthentication);

        var fragment = uri.Fragment.TrimStart('#');
        var queryStart = fragment.IndexOf('?');
        var fragmentParameters = queryStart >= 0 ? fragment[(queryStart + 1)..] : fragment;
        var safeFragmentParameters = FilterSensitiveParameters(fragmentParameters, ref removedAuthentication);
        if (!string.Equals(fragmentParameters, safeFragmentParameters, StringComparison.Ordinal))
        {
            fragment = queryStart >= 0
                ? string.Concat(fragment[..(queryStart + 1)], safeFragmentParameters)
                : safeFragmentParameters;
            builder.Fragment = fragment;
        }

        address = new SanitizedAddress(builder.Uri.AbsoluteUri, isHttp, removedAuthentication);
        return true;
    }

    private static string FilterSensitiveParameters(string parameters, ref bool removedAuthentication)
    {
        var safeParameters = new List<string>();
        foreach (var parameter in parameters.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var key = Uri.UnescapeDataString(parameter.Split('=', 2)[0].Replace('+', ' '));
            if (SensitiveQueryKeys.Contains(key)
                || key.StartsWith("x-amz-", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("x-goog-", StringComparison.OrdinalIgnoreCase))
            {
                removedAuthentication = true;
                continue;
            }

            safeParameters.Add(parameter);
        }

        return string.Join('&', safeParameters);
    }

    private readonly record struct SanitizedAddress(string Value, bool IsHttp, bool AuthenticationRemoved);

    private sealed class SourceListDocument
    {
        public required int Version { get; init; }
        public required List<SourceListEntry> Sources { get; init; }
    }

    private sealed class SourceListEntry
    {
        public required string Title { get; init; }
        public required string Address { get; init; }
        public required string Type { get; init; }
        public required string Comment { get; init; }
        public required bool IsPublic { get; init; }
        public required bool RequiresAuthentication { get; init; }
    }
}
