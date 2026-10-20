using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.Core.Services;

public static class RepositoryLocation
{
    public static string Normalize(string location, PresentationSourceType type)
    {
        if (location.Any(char.IsControl))
            throw new ArgumentException("Repository addresses cannot contain control characters.");
        var remote = location.Trim();
        if (type == PresentationSourceType.GitHub && !remote.Contains("://") && !remote.StartsWith("git@", StringComparison.Ordinal))
            remote = "https://github.com/" + remote.Trim('/');

        if (remote.StartsWith("git@", StringComparison.Ordinal))
        {
            var separator = remote.IndexOf(':');
            if (separator <= 4 || separator == remote.Length - 1 || remote.Any(char.IsWhiteSpace)
                || (type == PresentationSourceType.GitHub && !remote.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Use an SSH repository address such as git@github.com:owner/repository.git.");
            return remote;
        }

        if (!Uri.TryCreate(remote, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https" or "ssh" or "git")
            || string.IsNullOrEmpty(uri.Host) || uri.AbsolutePath.Trim('/').Length == 0
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
            || (uri.Scheme is "http" or "https" or "git" && !string.IsNullOrEmpty(uri.UserInfo))
            || (uri.Scheme == "ssh" && uri.UserInfo.Contains(':')))
            throw new ArgumentException("Use a repository HTTP, HTTPS, SSH or git URL without embedded credentials. GitHub sources also accept owner/repository.");

        if (type == PresentationSourceType.GitHub && uri.Host != "github.com")
            throw new ArgumentException("Use github.com/owner/repository, or select Git for another Git server.");

        if (uri.Host == "github.com" && uri.AbsolutePath.Trim('/').Split('/').Length != 2)
            throw new ArgumentException("Use the GitHub repository URL, not a tree, blob or presentation page URL.");

        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }

    public static string Identity(string location)
    {
        var key = Normalize(location, PresentationSourceType.Git);
        if (key.EndsWith(".git", StringComparison.Ordinal))
            key = key[..^4];
        if (key.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
            key = "https://github.com/" + key[15..];
        if (Uri.TryCreate(key, UriKind.Absolute, out var uri) && uri.Host == "github.com")
            return ("https://github.com/" + uri.AbsolutePath.Trim('/')).ToLowerInvariant();
        return key;
    }
}