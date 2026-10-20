using System.Security.Cryptography;
using System.Text;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.Infrastructure.Services;

public sealed class GitRepositoryService(string? cacheDirectory = null, string gitExecutable = "git", IGitCliClient? gitClient = null) : IGitRepositoryService
{
    public const string InstallHint = "Git is unavailable. Install Git for Windows from https://git-scm.com/downloads, or on macOS run xcode-select --install (or brew install git). On Linux use your package manager. Then restart the presenter.";

    private readonly string _cacheDirectory = cacheDirectory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SlideDevPresenter", "repositories");
    private readonly IGitCliClient _gitClient = gitClient ?? new GitCliClient(gitExecutable);

    public async Task<bool> IsGitAvailableAsync()
    {
        var result = await _gitClient.RunAsync(null, "--version");
        return result.ExitCode == 0;
    }

    public static string NormalizeRemote(string location, PresentationSourceType type) => RepositoryLocation.Normalize(location, type);

    public string GetCheckoutPath(PresentationSource source)
    {
        if (!string.IsNullOrWhiteSpace(source.CheckoutPath))
            return Path.GetFullPath(SourceScanner.ExpandHomePath(source.CheckoutPath.Trim()));

        var remote = NormalizeRemote(source.Location, source.Type);
        var key = RepositoryLocation.Identity(remote);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..24];
        return Path.Combine(_cacheDirectory, hash);
    }

    public async Task<IReadOnlyList<string>> GetBranchesAsync(PresentationSource source)
    {
        var checkout = GetCheckoutPath(source);
        if (!Directory.Exists(checkout) || !await IsGitAvailableAsync())
            return [];
        var result = await _gitClient.RunAsync(checkout, "for-each-ref", "--format=%(refname:short)", "refs/heads", "refs/remotes/origin");
        if (result.ExitCode != 0)
            throw new InvalidOperationException("Cannot list branches. Check the local checkout manually, then refresh.");
        return result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(branch => branch.StartsWith("origin/", StringComparison.Ordinal) ? branch[7..] : branch)
            .Where(branch => branch != "HEAD").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }

    public async Task<RepositoryPreparation> PrepareAsync(PresentationSource source, Func<string, Task<bool>> confirmUpdate)
    {
        string? checkout = null;
        try
        {
            var remote = NormalizeRemote(source.Location, source.Type);
            checkout = GetCheckoutPath(source);
            if (!await IsGitAvailableAsync())
                return new(null, InstallHint);

            var branch = source.Branch?.Trim();
            if (!string.IsNullOrEmpty(branch))
            {
                var validBranch = await _gitClient.RunAsync(null, "check-ref-format", "--branch", branch);
                if (branch.StartsWith('-') || validBranch.ExitCode != 0)
                    return new(null, "Invalid branch name. Enter a Git branch name such as main or release/slides, then refresh.");
            }

            if (!Directory.Exists(checkout))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(checkout)!);
                if (string.IsNullOrEmpty(branch))
                    await _gitClient.OpenTerminalAsync("clone", "--", remote, checkout);
                else
                    await _gitClient.OpenTerminalAsync("clone", "--branch", branch, "--", remote, checkout);
                return new(null, $"Cloning {source.Name} in your terminal to {checkout}. Complete authentication there, then refresh the library. If cloning fails, fix it in that terminal and refresh.");
            }

            var repository = await _gitClient.RunAsync(checkout, "rev-parse", "--show-toplevel");
            if (repository.ExitCode != 0 || !string.Equals(Path.GetFullPath(repository.Output.Trim()), Path.TrimEndingDirectorySeparator(checkout),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                return new(null, $"{checkout} is not a completed Git checkout. Finish or repair the clone manually, or choose another checkout folder, then refresh. The presenter will not overwrite it.");

            var origin = await _gitClient.RunAsync(checkout, "remote", "get-url", "origin");
            if (origin.ExitCode != 0 || RepositoryLocation.Identity(origin.Output.Trim()) != RepositoryLocation.Identity(remote))
                return new(null, $"The checkout at {checkout} does not match {source.Name}'s configured repository. Fix its origin remote manually or select the correct checkout folder, then refresh.");

            var currentBranch = await _gitClient.RunAsync(checkout, "branch", "--show-current");
            var needsSwitch = !string.IsNullOrEmpty(branch) && (currentBranch.ExitCode != 0 || currentBranch.Output.Trim() != branch);
            var fetch = await _gitClient.RunAsync(checkout, "fetch", "--quiet", "--no-recurse-submodules");
            if (fetch.ExitCode != 0)
                return new(needsSwitch ? null : checkout, $"Could not check updates for {source.Name}. In {checkout}, run git fetch manually and fix authentication, network or remote errors, then refresh. {(needsSwitch ? "The requested branch has not been checked out." : "The existing local copy is still available.")}");

            if (needsSwitch)
            {
                var status = await _gitClient.RunAsync(checkout, "status", "--porcelain");
                if (status.ExitCode != 0 || !string.IsNullOrWhiteSpace(status.Output))
                    return new(null, $"Cannot switch {source.Name} to {branch} while the checkout has uncommitted changes. Commit or stash them manually in {checkout}, then refresh. No changes were discarded.");
                var local = await _gitClient.RunAsync(checkout, "show-ref", "--verify", "--quiet", "refs/heads/" + branch);
                var remoteBranch = await _gitClient.RunAsync(checkout, "show-ref", "--verify", "--quiet", "refs/remotes/origin/" + branch);
                if (local.ExitCode != 0 && remoteBranch.ExitCode != 0)
                    return new(null, $"Branch {branch} was not found for {source.Name}. Check the branch name and remote manually in {checkout}, then refresh.");
                if (!await confirmUpdate($"Switch {source.Name} from {currentBranch.Output.Trim()} to {branch} in a terminal? Checkout: {checkout}"))
                    return new(null, $"Branch switch declined for {source.Name}. Select the current branch in Sources or refresh to try again.");
                if (local.ExitCode == 0)
                    await _gitClient.OpenTerminalAsync("-C", checkout, "switch", "--", branch!);
                else
                    await _gitClient.OpenTerminalAsync("-C", checkout, "switch", "--track", "-c", branch!, "--", "origin/" + branch);
                return new(null, $"Switching {source.Name} to {branch} in your terminal. Resolve any errors manually, then refresh the library before starting the presentation.");
            }

            var incoming = await _gitClient.RunAsync(checkout, "rev-list", "--count", "HEAD..@{upstream}");
            if (incoming.ExitCode != 0)
                return new(checkout, $"Cannot determine updates for {source.Name}. In {checkout}, check the current branch and configure its upstream with git branch --set-upstream-to=origin/<branch>, then refresh.");

            if (int.TryParse(incoming.Output.Trim(), out var count) && count > 0
                && await confirmUpdate($"{source.Name} has {count} incoming commit(s). Try to get the latest changes in a terminal? Local checkout: {checkout}"))
            {
                await _gitClient.OpenTerminalAsync("-C", checkout, "pull", "--ff-only");
                return new(checkout, $"Updating {source.Name} in your terminal. Refresh after it finishes. If Git reports conflicts or diverged branches, resolve them manually in {checkout}; no changes will be discarded by the presenter.");
            }

            return new(checkout, null);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new(null, $"Could not prepare {source.Name}. {exception.Message} Fix the repository manually{(checkout is null ? "" : $" at {checkout}")}, then refresh. Check Git installation, terminal availability, repository access and folder permissions.");
        }
    }
}