using SlideDevPresenter.App.Services;
using SlideDevPresenter.App.ViewModels;
using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.Tests.ViewModels;

internal sealed class FakeThemeService : IThemeService
{
    public string? LastAppliedTheme { get; private set; }
    public int ApplyCallCount { get; private set; }

    public void ApplyTheme(string? theme)
    {
        ApplyCallCount++;
        LastAppliedTheme = theme;
    }
}

public sealed class SettingsViewModelTests
{
    [Fact]
    public void ShortcutLabels_OnMacOS_ShowMacDefaults()
    {
        var vm = new SettingsViewModel(new FakeSettingsService(), new FakeThemeService(), platformInfo: new SlideDevPresenter.Tests.Services.FakePlatformInfo(true));

        Assert.Equal("⌘+Shift+Return", vm.StartFromBeginningDefaultShortcut);
        Assert.Equal("⌘+Return", vm.StartFromCurrentSlideDefaultShortcut);
        Assert.Equal("Option+Return", vm.StartPresenterViewDefaultShortcut);
        Assert.Equal("macOS default (⌘+Shift+Return)", vm.StartFromBeginningShortcutPlaceholder);
        Assert.Contains("Cmd+Shift+Enter", vm.ShortcutHelpText);
        Assert.DoesNotContain("F5", vm.ShortcutHelpText);
    }

    [Fact]
    public void ShortcutLabels_OnWindows_ShowWindowsDefaults()
    {
        var vm = new SettingsViewModel(new FakeSettingsService(), new FakeThemeService(), platformInfo: new SlideDevPresenter.Tests.Services.FakePlatformInfo(false));

        Assert.Equal("F5", vm.StartFromBeginningDefaultShortcut);
        Assert.Equal("Shift+F5", vm.StartFromCurrentSlideDefaultShortcut);
        Assert.Equal("Alt+F5", vm.StartPresenterViewDefaultShortcut);
        Assert.Equal("Windows default (Alt+F5)", vm.StartPresenterViewShortcutPlaceholder);
        Assert.Contains("Shift+F5", vm.ShortcutHelpText);
        Assert.DoesNotContain("Cmd", vm.ShortcutHelpText);
    }

    [Fact]
    public async Task SaveLocalSource_WithFilePath_RejectsBeforePersisting()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            var settings = new FakeSettingsService();
            var vm = new SettingsViewModel(settings, new FakeThemeService());
            vm.AddLocalSource(filePath);

            await vm.SaveCommand.ExecuteAsync(null);

            Assert.Empty(settings.Settings.Sources);
            Assert.Contains("Select a local folder, not a file.", vm.SourceMessage);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task AddLocalSource_UsesSelectedFolderAndPersistsAsLocalRoot()
    {
        var settings = new FakeSettingsService();
        var vm = new SettingsViewModel(settings, new FakeThemeService());
        var folderPath = Path.Combine(Path.GetTempPath(), "My Slides");

        vm.AddLocalSource(folderPath);
        await vm.SaveCommand.ExecuteAsync(null);

        var source = Assert.Single(vm.Sources);
        Assert.Same(source, vm.SelectedSource);
        Assert.Equal("My Slides", source.Name);
        Assert.Equal("Local", source.TypeName);
        Assert.True(source.IsLocal);
        var saved = Assert.Single(settings.Settings.Sources);
        Assert.Equal(folderPath, saved.Location);
        Assert.Equal(PresentationSourceType.LocalRoot, saved.Type);
        Assert.True(saved.IsEnabled);
        Assert.Contains("Local", vm.AvailableSourceTypes);
        Assert.DoesNotContain("LocalRoot", vm.AvailableSourceTypes);
        Assert.DoesNotContain("LocalProject", vm.AvailableSourceTypes);
    }

    [Theory]
    [InlineData(PresentationSourceType.LocalRoot)]
    [InlineData(PresentationSourceType.LocalProject)]
    public void LegacyLocalSources_DisplayAsLocalAndSupportTypeChanges(PresentationSourceType type)
    {
        var source = new SourceViewModel(new PresentationSource
        {
            Id = Guid.NewGuid(), Name = "Local", Type = type, Location = "/slides"
        });

        Assert.Equal("Local", source.TypeName);
        Assert.True(source.IsLocal);
        Assert.Equal(PresentationSourceType.LocalRoot, source.Type);

        source.TypeName = "HostedUrl";
        Assert.False(source.IsLocal);
        Assert.Equal(PresentationSourceType.HostedUrl, source.Type);
        source.TypeName = "Local";
        Assert.True(source.IsLocal);
        Assert.Equal(PresentationSourceType.LocalRoot, source.Type);
    }

    [Fact]
    public async Task MissingGit_DisablesRepositoryCommandsAndEntriesWithoutDisablingLocalSources()
    {
        var settings = new FakeSettingsService();
        settings.Settings.Sources.Add(new PresentationSource
        {
            Id = Guid.NewGuid(), Name = "Repository", Type = PresentationSourceType.Git, Location = "https://github.com/owner/slides"
        });
        var git = new FakeGitRepositoryService { Available = false };
        var vm = new SettingsViewModel(settings, new FakeThemeService(), git);

        await vm.CheckGitAvailabilityAsync();

        Assert.False(vm.AddGitSourceCommand.CanExecute(null));
        Assert.False(vm.AddGitHubSourceCommand.CanExecute(null));
        Assert.False(vm.Sources[0].CanEnable);
        vm.AddLocalSource("/slides");
        Assert.True(vm.SelectedSource!.CanEnable);
        Assert.True(vm.AddHostedUrlSourceCommand.CanExecute(null));
        Assert.Contains("Install Git", vm.SourceMessage);
        git.Available = true;
        await vm.CheckGitAvailabilityAsync();
        Assert.True(vm.Sources[0].CanEnable);
        Assert.True(vm.AddGitSourceCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(PresentationSourceType.Git)]
    [InlineData(PresentationSourceType.GitHub)]
    public async Task AddAndRemoveRepositorySource_PersistsCheckoutAndSourceType(PresentationSourceType type)
    {
        var settings = new FakeSettingsService();
        var vm = new SettingsViewModel(settings, new FakeThemeService(), new FakeGitRepositoryService());
        await vm.CheckGitAvailabilityAsync();
        if (type == PresentationSourceType.Git)
            vm.AddGitSourceCommand.Execute(null);
        else
            vm.AddGitHubSourceCommand.Execute(null);
        var source = Assert.Single(vm.Sources);
        source.Location = "https://github.com/owner/slides";
        source.CheckoutPath = "~/existing-slides";
        source.Branch = "release/slides";

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(settings.Settings.Sources);
        Assert.Equal(type, saved.Type);
        Assert.Equal("~/existing-slides", saved.CheckoutPath);
        Assert.Equal("release/slides", saved.Branch);
        vm.RemoveSelectedSourceCommand.Execute(null);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Empty(settings.Settings.Sources);
    }

    [Fact]
    public async Task SaveRepositoryWithEmbeddedCredentials_RejectsBeforePersisting()
    {
        var settings = new FakeSettingsService();
        var vm = new SettingsViewModel(settings, new FakeThemeService(), new FakeGitRepositoryService());
        await vm.CheckGitAvailabilityAsync();
        vm.AddGitSourceCommand.Execute(null);
        vm.SelectedSource!.Location = "https://user:secret@github.com/owner/slides";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(settings.Settings.Sources);
        Assert.Contains("without embedded credentials", vm.SourceMessage);
        Assert.DoesNotContain("secret", vm.SourceMessage);
    }

    [Fact]
    public async Task LoadBranches_PopulatesBranchSuggestionsForSelectedSource()
    {
        var vm = new SettingsViewModel(new FakeSettingsService(), new FakeThemeService(), new FakeGitRepositoryService());
        await vm.CheckGitAvailabilityAsync();
        vm.AddGitHubSourceCommand.Execute(null);

        await vm.LoadBranchesCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "main", "release/slides" }, vm.Branches);
        vm.SelectedSource = null;
        Assert.Empty(vm.Branches);
    }

    [Fact]
    public async Task SaveAsync_PersistsAndAppliesThemeImmediately()
    {
        var settings = new FakeSettingsService();
        var themeService = new FakeThemeService();
        var vm = new SettingsViewModel(settings, themeService)
        {
            Theme = "Dark"
        };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Dark", settings.Settings.Appearance.Theme);
        Assert.Equal(1, themeService.ApplyCallCount);
        Assert.Equal("Dark", themeService.LastAppliedTheme);
    }

    [Fact]
    public async Task SaveAsync_PersistsCustomShortcuts()
    {
        var settings = new FakeSettingsService();
        var vm = new SettingsViewModel(settings, new FakeThemeService())
        {
            StartFromBeginningShortcut = "Ctrl+F5",
            StartFromCurrentSlideShortcut = "Ctrl+Shift+F5",
            StartPresenterViewShortcut = "Ctrl+Alt+F5"
        };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Ctrl+F5", settings.Settings.Shortcuts.StartFromBeginning);
        Assert.Equal("Ctrl+Shift+F5", settings.Settings.Shortcuts.StartFromCurrentSlide);
        Assert.Equal("Ctrl+Alt+F5", settings.Settings.Shortcuts.StartPresenterView);
    }

    [Fact]
    public async Task SaveAsync_EmptyShortcutSavedAsNull()
    {
        var settings = new FakeSettingsService();
        var vm = new SettingsViewModel(settings, new FakeThemeService())
        {
            StartFromBeginningShortcut = ""
        };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(settings.Settings.Shortcuts.StartFromBeginning);
    }

    [Fact]
    public void RestoreDefaultShortcutsCommand_ClearsAllShortcuts()
    {
        var settings = new FakeSettingsService();
        settings.Settings.Shortcuts.StartFromBeginning = "Ctrl+F5";
        var vm = new SettingsViewModel(settings, new FakeThemeService());

        vm.RestoreDefaultShortcutsCommand.Execute(null);

        Assert.Equal(string.Empty, vm.StartFromBeginningShortcut);
        Assert.Equal(string.Empty, vm.StartFromCurrentSlideShortcut);
        Assert.Equal(string.Empty, vm.StartPresenterViewShortcut);
    }

    [Fact]
    public void ShortcutConflictError_WhenDuplicates_ReturnsErrorMessage()
    {
        var settings = new FakeSettingsService();
        var vm = new SettingsViewModel(settings, new FakeThemeService())
        {
            StartFromBeginningShortcut = "F5",
            StartFromCurrentSlideShortcut = "F5"
        };

        Assert.NotNull(vm.ShortcutConflictError);
        Assert.Contains("Duplicate", vm.ShortcutConflictError);
    }

    [Fact]
    public void ShortcutConflictError_WhenNoDuplicates_ReturnsNull()
    {
        var settings = new FakeSettingsService();
        var vm = new SettingsViewModel(settings, new FakeThemeService())
        {
            StartFromBeginningShortcut = "F5",
            StartFromCurrentSlideShortcut = "Shift+F5",
            StartPresenterViewShortcut = "Alt+F5"
        };

        Assert.Null(vm.ShortcutConflictError);
    }

    [Fact]
    public void LoadFromSettings_LoadsExistingShortcuts()
    {
        var settings = new FakeSettingsService();
        settings.Settings.Shortcuts.StartFromBeginning = "Ctrl+F5";
        var vm = new SettingsViewModel(settings, new FakeThemeService());

        Assert.Equal("Ctrl+F5", vm.StartFromBeginningShortcut);
    }

    [Fact]
    public async Task SaveAsync_PersistsNavigationSettings()
    {
        var settings = new FakeSettingsService();
        var vm = new SettingsViewModel(settings, new FakeThemeService())
        {
            OpenExternalLinksInSystemBrowser = false,
            OpenExternalLinksInEmbeddedBrowser = true
        };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(settings.Settings.Navigation.OpenExternalLinksInSystemBrowser);
        Assert.True(settings.Settings.Navigation.OpenExternalLinksInEmbeddedBrowser);
    }

    [Fact]
    public async Task ImportConfigurationAsync_RefreshesSettingsAndAppliesImportedTheme()
    {
        var settings = new FakeSettingsService();
        var themeService = new FakeThemeService();
        var vm = new SettingsViewModel(settings, themeService);
        var importedSettings = new SlideDevPresenter.Core.Models.AppSettings
        {
            Appearance = new() { Theme = "Dark" },
            Defaults = new() { DefaultPort = 4444 }
        };
        await using var stream = new MemoryStream();
        await System.Text.Json.JsonSerializer.SerializeAsync(stream, importedSettings);
        stream.Position = 0;

        await vm.ImportConfigurationAsync(stream);

        Assert.Equal(4444, vm.DefaultPort);
        Assert.Equal("Dark", vm.Theme);
        Assert.Equal("Dark", themeService.LastAppliedTheme);
        Assert.Equal("Configuration imported.", vm.ConfigurationMessage);
    }

    [Fact]
    public async Task ImportSourceListAsync_AppendsOnlineSourcesWithoutRemovingExistingSources()
    {
        var settings = new FakeSettingsService();
        settings.Settings.Sources.Add(new SlideDevPresenter.Core.Models.PresentationSource
        {
            Id = Guid.NewGuid(),
            Name = "Local",
            Type = SlideDevPresenter.Core.Models.PresentationSourceType.LocalRoot,
            Location = "~/slides"
        });
        var vm = new SettingsViewModel(settings, new FakeThemeService());
        const string json = """
            {
              "version": 1,
              "sources": [
                {
                  "title": "Shared",
                  "address": "https://slides.example.com/talk",
                  "type": "https",
                  "comment": "Shared deck",
                  "isPublic": true,
                  "requiresAuthentication": false
                }
              ]
            }
            """;
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        await vm.ImportSourceListAsync(stream);

        Assert.Equal(2, vm.Sources.Count);
        Assert.Equal("Local", vm.Sources[0].Name);
        Assert.Equal("Shared", vm.Sources[1].Name);
        Assert.Equal("Shared deck", vm.Sources[1].Comment);
        Assert.Equal("Source list imported.", vm.ConfigurationMessage);
    }
}
