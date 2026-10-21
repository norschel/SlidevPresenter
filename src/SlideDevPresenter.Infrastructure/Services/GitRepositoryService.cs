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

    private static bool IsLocal(PresentationSource source) => source.Type is PresentationSourceType.LocalRoot or PresentationSourceType.LocalProject;

    public string GetCheckoutPath(PresentationSource source)
    {
        if (IsLocal(source))
            return Path.GetFullPath(SourceScanner.ExpandHomePath(source.Location.Trim()));
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
        if (IsLocal(source) && await GetCurrentBranchAsync(source) is null)
            return [];
        var result = await _gitClient.RunAsync(checkout, "for-each-ref", "--format=%(refname:short)", "refs/heads", "refs/remotes/origin");
        if (result.ExitCode != 0)
            throw new InvalidOperationException("Cannot list branches. Check the local checkout manually, then refresh.");
        return result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(branch => branch.StartsWith("origin/", StringComparison.Ordinal) ? branch[7..] : branch)
            .Where(branch => branch != "HEAD").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }

    public async Task<string?> GetCurrentBranchAsync(PresentationSource source)
    {
        var folder = GetCheckoutPath(source);
        if (!Directory.Exists(folder) || !await IsGitAvailableAsync())
            return null;
        var workTree = await _gitClient.RunAsync(folder, "rev-parse", "--show-toplevel");
        if (workTree.ExitCode != 0)
            return null;
        var branch = await _gitClient.RunAsync(folder, "branch", "--show-current");
        return branch.ExitCode == 0 ? branch.Output.Trim() : string.Empty;
    }

    public async Task<RepositoryPreparation> PrepareAsync(PresentationSource source, Func<string, Task<bool>> confirmUpdate)
    {
        string? checkout = null;
        try
        {
            var local = IsLocal(source);
            var branch = source.Branch?.Trim();
            checkout = GetCheckoutPath(source);
            if (local && string.IsNullOrEmpty(branch))
                return new(checkout, null);
            var remote = local ? null : NormalizeRemote(source.Location, source.Type);
            if (!await IsGitAvailableAsync())
                return new(local ? checkout : null, InstallHint);

            if (!string.IsNullOrEmpty(branch))
            {
                var validBranch = await _gitClient.RunAsync(null, "check-ref-format", "--branch", branch);
                if (branch.StartsWith('-') || validBranch.ExitCode != 0)
                    return new(local ? checkout : null, "Invalid branch name. Enter a Git branch name such as main or release/slides, then refresh.");
            }

            if (local)
            {
                var current = await GetCurrentBranchAsync(source);
                if (current is null)
                    return new(checkout, $"{checkout} is not a Git repository, so branch {branch} was ignored for {source.Name}. Clear the branch in Settings, then refresh.");
                return current == branch
                    ? new(checkout, null)
                    : await SwitchBranchAsync(source, checkout, branch!, current.Length == 0 ? "a detached HEAD" : current, checkout, confirmUpdate);
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
            if (origin.ExitCode != 0 || RepositoryLocation.Identity(origin.Output.Trim()) != RepositoryLocation.Identity(remote!))
                return new(null, $"The checkout at {checkout} does not match {source.Name}'s configured repository. Fix its origin remote manually or select the correct checkout folder, then refresh.");

            var currentBranch = await _gitClient.RunAsync(checkout, "branch", "--show-current");
            var needsSwitch = !string.IsNullOrEmpty(branch) && (currentBranch.ExitCode != 0 || currentBranch.Output.Trim() != branch);
            var fetch = await _gitClient.RunAsync(checkout, "fetch", "--quiet", "--no-recurse-submodules");
            if (fetch.ExitCode != 0)
                return new(needsSwitch ? null : checkout, $"Could not check updates for {source.Name}. In {checkout}, run git fetch manually and fix authentication, network or remote errors, then refresh. {(needsSwitch ? "The requested branch has not been checked out." : "The existing local copy is still available.")}");

            if (needsSwitch)
                return await SwitchBranchAsync(source, checkout, branch!, currentBranch.Output.Trim(), null, confirmUpdate);

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

    private async Task<RepositoryPreparation> SwitchBranchAsync(PresentationSource source, string checkout, string branch, string currentBranch, string? unchangedDirectory, Func<string, Task<bool>> confirmUpdate)
    {
        var status = await _gitClient.RunAsync(checkout, "status", "--porcelain");
        if (status.ExitCode != 0 || !string.IsNullOrWhiteSpace(status.Output))
            return new(unchangedDirectory, $"Cannot switch {source.Name} to {branch} while the checkout has uncommitted changes. Commit or stash them manually in {checkout}, then refresh. No changes were discarded.");
        var local = await _gitClient.RunAsync(checkout, "show-ref", "--verify", "--quiet", "refs/heads/" + branch);
        var remoteBranch = await _gitClient.RunAsync(checkout, "show-ref", "--verify", "--quiet", "refs/remotes/origin/" + branch);
        if (local.ExitCode != 0 && remoteBranch.ExitCode != 0)
            return new(unchangedDirectory, $"Branch {branch} was not found for {source.Name}. Check the branch name and remote manually in {checkout}, then refresh.");
        if (!await confirmUpdate($"Switch {source.Name} from {currentBranch} to {branch} in a terminal? Checkout: {checkout}"))
            return new(checkout, $"Branch switch declined for {source.Name}. Select the current branch in Sources or refresh to try again.", BranchSwitchDeclined: true);
        if (local.ExitCode == 0)
            await _gitClient.OpenTerminalAsync("-C", checkout, "switch", "--", branch);
        else
            await _gitClient.OpenTerminalAsync("-C", checkout, "switch", "--track", "-c", branch, "--", "origin/" + branch);
        return new(null, $"Switching {source.Name} to {branch} in your terminal. Resolve any errors manually, then refresh the library before starting the presentation.");
    }
}