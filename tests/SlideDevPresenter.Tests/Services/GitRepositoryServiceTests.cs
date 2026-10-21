using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Infrastructure.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class GitRepositoryServiceTests
{
    [Theory]
    [InlineData("owner/slides", PresentationSourceType.GitHub, "https://github.com/owner/slides")]
    [InlineData("https://github.com/owner/slides.git", PresentationSourceType.GitHub, "https://github.com/owner/slides.git")]
    [InlineData("git@github.com:owner/slides.git", PresentationSourceType.Git, "git@github.com:owner/slides.git")]
    [InlineData("git://example.com/slides.git", PresentationSourceType.Git, "git://example.com/slides.git")]
    public void NormalizeRemote_AcceptsRepositoryLocations(string location, PresentationSourceType type, string expected)
    {
        Assert.Equal(expected, GitRepositoryService.NormalizeRemote(location, type));
    }

    [Theory]
    [InlineData("https://user:secret@github.com/owner/slides")]
    [InlineData("https://github.com/owner/slides/tree/main")]
    [InlineData("--upload-pack=bad")]
    [InlineData("file:///tmp/slides")]
    [InlineData("https://github.com/owner/slides\n")]
    [InlineData("ssh://git:secret@example.com/slides")]
    public void NormalizeRemote_RejectsUnsafeOrNonRepositoryLocations(string location)
    {
        Assert.Throws<ArgumentException>(() => GitRepositoryService.NormalizeRemote(location, PresentationSourceType.Git));
    }

    [Fact]
    public void GetCheckoutPath_ReusesGitHubCloneAcrossSourceEntries()
    {
        var service = new GitRepositoryService();
        var first = Source("https://github.com/owner/slides");
        var second = Source("git@github.com:owner/slides.git");
        Assert.Equal(service.GetCheckoutPath(first), service.GetCheckoutPath(second));
    }

    [Fact]
    public async Task PrepareAsync_MissingGit_ReturnsInstallHint()
    {
        var service = new GitRepositoryService(gitExecutable: "presenter-nonexistent-git-client");
        Assert.False(await service.IsGitAvailableAsync());
        var result = await service.PrepareAsync(Source("https://github.com/owner/slides"), _ => Task.FromResult(false));
        Assert.Null(result.Directory);
        Assert.Equal(GitRepositoryService.InstallHint, result.Message);
    }

    [Fact]
    public async Task PrepareAsync_NewRepository_ClonesInTerminalWithoutCheckingUpdates()
    {
        using var checkout = new TestCheckout(false);
        var result = await checkout.Service.PrepareAsync(checkout.Source, _ => throw new InvalidOperationException());

        Assert.Null(result.Directory);
        Assert.Contains("then refresh", result.Message);
        Assert.Equal(new[] { "clone", "--", checkout.Source.Location, checkout.Path }, Assert.Single(checkout.Client.TerminalCommands));
        Assert.DoesNotContain(checkout.Client.Commands, command => command[0] == "fetch");
    }

    [Fact]
    public async Task PrepareAsync_ExistingRepository_ReusesCheckoutWithoutCloneOrPrompt()
    {
        using var checkout = new TestCheckout();
        var result = await checkout.Service.PrepareAsync(checkout.Source, _ => throw new InvalidOperationException());

        Assert.Equal(checkout.Path, result.Directory);
        Assert.Null(result.Message);
        Assert.Empty(checkout.Client.TerminalCommands);
        Assert.Contains(checkout.Client.Commands, command => command[0] == "fetch");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PrepareAsync_IncomingCommits_AsksBeforeTerminalPull(bool accept)
    {
        using var checkout = new TestCheckout();
        checkout.Client.Incoming = "3";
        string? prompt = null;
        var result = await checkout.Service.PrepareAsync(checkout.Source, message =>
        {
            prompt = message;
            return Task.FromResult(accept);
        });

        Assert.Contains("3 incoming commit(s)", prompt);
        Assert.Equal(checkout.Path, result.Directory);
        if (accept)
            Assert.Equal(new[] { "-C", checkout.Path, "pull", "--ff-only" }, Assert.Single(checkout.Client.TerminalCommands));
        else
            Assert.Empty(checkout.Client.TerminalCommands);
    }

    [Fact]
    public async Task PrepareAsync_FetchFails_KeepsLocalCopyAndAsksForManualRepair()
    {
        using var checkout = new TestCheckout();
        checkout.Client.FetchExitCode = 1;
        var result = await checkout.Service.PrepareAsync(checkout.Source, _ => throw new InvalidOperationException());

        Assert.Equal(checkout.Path, result.Directory);
        Assert.Contains("git fetch manually", result.Message);
        Assert.Empty(checkout.Client.TerminalCommands);
    }

    [Fact]
    public async Task PrepareAsync_WrongOrigin_DoesNotFetchOrOverwrite()
    {
        using var checkout = new TestCheckout();
        checkout.Client.Origin = "https://example.com/wrong-repository.git";
        var result = await checkout.Service.PrepareAsync(checkout.Source, _ => Task.FromResult(true));

        Assert.Null(result.Directory);
        Assert.Contains("does not match", result.Message);
        Assert.DoesNotContain(checkout.Client.Commands, command => command[0] == "fetch");
        Assert.Empty(checkout.Client.TerminalCommands);
    }

    [Fact]
    public async Task PrepareAsync_IncompleteCheckout_AsksForManualRepairWithoutClone()
    {
        using var checkout = new TestCheckout();
        checkout.Client.RepositoryExitCode = 1;
        var result = await checkout.Service.PrepareAsync(checkout.Source, _ => Task.FromResult(true));

        Assert.Null(result.Directory);
        Assert.Contains("Finish or repair the clone manually", result.Message);
        Assert.Empty(checkout.Client.TerminalCommands);
    }

    [Fact]
    public async Task PrepareAsync_TerminalUnavailable_ReportsManualRecovery()
    {
        using var checkout = new TestCheckout(false);
        checkout.Client.TerminalException = new InvalidOperationException("Terminal unavailable");
        var result = await checkout.Service.PrepareAsync(checkout.Source, _ => Task.FromResult(false));

        Assert.Null(result.Directory);
        Assert.Contains("Terminal unavailable", result.Message);
        Assert.Contains("Fix the repository manually", result.Message);
    }

    [Fact]
    public void NormalizeRemote_GitHubType_RejectsOtherSshHosts()
    {
        Assert.Throws<ArgumentException>(() => GitRepositoryService.NormalizeRemote("git@example.com:owner/slides", PresentationSourceType.GitHub));
    }

    [Fact]
    public async Task PrepareAsync_NewCloneWithBranch_PassesBranchToGitClone()
    {
        using var checkout = new TestCheckout(false);
        checkout.Source.Branch = "release/slides";

        await checkout.Service.PrepareAsync(checkout.Source, _ => Task.FromResult(true));

        Assert.Equal(new[] { "clone", "--branch", "release/slides", "--", checkout.Source.Location, checkout.Path }, Assert.Single(checkout.Client.TerminalCommands));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task PrepareAsync_DifferentBranch_AsksBeforeSwitching(bool localBranch, bool accept)
    {
        using var checkout = new TestCheckout();
        checkout.Source.Branch = "release/slides";
        checkout.Client.LocalBranchExitCode = localBranch ? 0 : 1;
        string? prompt = null;

        var result = await checkout.Service.PrepareAsync(checkout.Source, message =>
        {
            prompt = message;
            return Task.FromResult(accept);
        });

        Assert.Contains("from main to release/slides", prompt);
        if (!accept)
        {
            Assert.Equal(checkout.Path, result.Directory);
            Assert.True(result.BranchSwitchDeclined);
            Assert.Empty(checkout.Client.TerminalCommands);
            Assert.Contains("declined", result.Message);
        }
        else
        {
            Assert.Null(result.Directory);
            Assert.False(result.BranchSwitchDeclined);
            if (localBranch)
                Assert.Equal(new[] { "-C", checkout.Path, "switch", "--", "release/slides" }, Assert.Single(checkout.Client.TerminalCommands));
            else
                Assert.Equal(new[] { "-C", checkout.Path, "switch", "--track", "-c", "release/slides", "--", "origin/release/slides" }, Assert.Single(checkout.Client.TerminalCommands));
        }
    }

    [Fact]
    public async Task PrepareAsync_DirtyCheckout_DoesNotSwitchOrDiscardChanges()
    {
        using var checkout = new TestCheckout();
        checkout.Source.Branch = "release/slides";
        checkout.Client.WorkingChanges = " M slides.md";

        var result = await checkout.Service.PrepareAsync(checkout.Source, _ => throw new InvalidOperationException());

        Assert.Null(result.Directory);
        Assert.Contains("Commit or stash them manually", result.Message);
        Assert.Empty(checkout.Client.TerminalCommands);
    }

    [Fact]
    public async Task PrepareAsync_UnknownBranch_ReturnsManualRecovery()
    {
        using var checkout = new TestCheckout();
        checkout.Source.Branch = "missing";
        checkout.Client.LocalBranchExitCode = 1;
        checkout.Client.RemoteBranchExitCode = 1;

        var result = await checkout.Service.PrepareAsync(checkout.Source, _ => throw new InvalidOperationException());

        Assert.Null(result.Directory);
        Assert.Contains("was not found", result.Message);
        Assert.Empty(checkout.Client.TerminalCommands);
    }

    [Fact]
    public async Task PrepareAsync_InvalidBranch_DoesNotCloneOrSwitch()
    {
        using var checkout = new TestCheckout(false);
        checkout.Source.Branch = "bad..branch";
        checkout.Client.BranchValidationExitCode = 1;

        var result = await checkout.Service.PrepareAsync(checkout.Source, _ => Task.FromResult(true));

        Assert.Null(result.Directory);
        Assert.Contains("Invalid branch", result.Message);
        Assert.Empty(checkout.Client.TerminalCommands);
    }

    [Fact]
    public async Task GetBranchesAsync_ReturnsDistinctLocalAndRemoteBranchNames()
    {
        using var checkout = new TestCheckout();
        Assert.Equal(new[] { "main", "release/slides" }, await checkout.Service.GetBranchesAsync(checkout.Source));
    }

    private sealed class TestCheckout : IDisposable
    {
        private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "presenter-git-tests-" + Guid.NewGuid());
        public string Path => System.IO.Path.Combine(_root, "checkout");
        public FakeGitCliClient Client { get; } = new();
        public PresentationSource Source { get; }
        public GitRepositoryService Service { get; }

        public TestCheckout(bool exists = true)
        {
            Directory.CreateDirectory(_root);
            if (exists)
                Directory.CreateDirectory(Path);
            Source = GitRepositoryServiceTests.Source(Client.Origin);
            Source.CheckoutPath = Path;
            Service = new GitRepositoryService(gitClient: Client);
        }

        public void Dispose() => Directory.Delete(_root, true);
    }

    private sealed class FakeGitCliClient : IGitCliClient
    {
        public List<string[]> Commands { get; } = [];
        public List<string[]> TerminalCommands { get; } = [];
        public string Origin { get; set; } = "https://github.com/owner/slides";
        public int FetchExitCode { get; set; }
        public int RepositoryExitCode { get; set; }
        public string Incoming { get; set; } = "0";
        public string WorkingChanges { get; set; } = "";
        public int LocalBranchExitCode { get; set; }
        public int RemoteBranchExitCode { get; set; }
        public int BranchValidationExitCode { get; set; }
        public Exception? TerminalException { get; set; }

        public Task<(int ExitCode, string Output)> RunAsync(string? directory, params string[] arguments)
        {
            Commands.Add(arguments);
            return Task.FromResult(arguments[0] switch
            {
                "--version" => (0, "git version 2.50.0"),
                "check-ref-format" => (BranchValidationExitCode, ""),
                "branch" => (0, "main"),
                "status" => (0, WorkingChanges),
                "show-ref" => (arguments[^1].StartsWith("refs/heads/", StringComparison.Ordinal) ? LocalBranchExitCode : RemoteBranchExitCode, ""),
                "for-each-ref" => (0, "main\norigin/main\norigin/release/slides\norigin/HEAD\n"),
                "rev-parse" => (RepositoryExitCode, directory!),
                "remote" => (0, Origin),
                "fetch" => (FetchExitCode, ""),
                "rev-list" => (0, Incoming),
                _ => throw new InvalidOperationException(string.Join(' ', arguments))
            });
        }

        public Task OpenTerminalAsync(params string[] arguments)
        {
            TerminalCommands.Add(arguments);
            return TerminalException is null ? Task.CompletedTask : Task.FromException(TerminalException);
        }
    }

    private static PresentationSource Source(string location) => new()
    {
        Id = Guid.NewGuid(), Name = "Slides", Type = PresentationSourceType.Git, Location = location
    };
}