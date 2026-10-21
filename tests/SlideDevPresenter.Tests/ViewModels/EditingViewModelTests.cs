using SlideDevPresenter.App.ViewModels;
using SlideDevPresenter.Core.Models;
using SlideDevPresenter.Core.Services;

namespace SlideDevPresenter.Tests.ViewModels;

internal sealed class FakeCodeEditorService : ICodeEditorService
{
    public IReadOnlyList<CodeEditorEdition> Editions { get; set; } = [CodeEditorEdition.Stable, CodeEditorEdition.Insiders];
    public string? OpenedDirectory { get; private set; }
    public CodeEditorEdition? OpenedEdition { get; private set; }
    public Exception? LaunchError { get; set; }

    public IReadOnlyList<CodeEditorEdition> GetAvailableEditions() => Editions;

    public void OpenProject(string projectDirectory, CodeEditorEdition edition)
    {
        if (LaunchError is not null)
            throw LaunchError;
        OpenedDirectory = projectDirectory;
        OpenedEdition = edition;
    }
}

public sealed class EditingViewModelTests
{
    private static MainViewModel CreateMain(FakeSettingsService settings, FakeCodeEditorService editor) =>
        new(settings, new FakeSourceScanner(), new FakeProcessHost(), new FakeSlideDeckMetadataReader(),
            new FakePresentationWindowService(), new FakeDisplayService(), codeEditorService: editor);

    private static PresentationProjectViewModel Project(PresentationSourceType type) => new(new PresentationProject
    {
        Id = Guid.NewGuid(),
        Name = "Slides",
        SourceType = type,
        Location = type == PresentationSourceType.HostedUrl ? "https://example.com/slides" : Path.GetTempPath()
    });

    [Fact]
    public void Editing_IsOffByDefault()
    {
        var settings = new FakeSettingsService();
        var editor = new FakeCodeEditorService();
        var main = CreateMain(settings, editor);
        main.SelectedProject = Project(PresentationSourceType.LocalProject);
        var preferences = new SettingsViewModel(settings, new FakeThemeService(), codeEditorService: editor);

        Assert.False(settings.Settings.Editing.Enabled);
        Assert.False(preferences.EditingEnabled);
        Assert.False(main.ShowEditTab);
        Assert.False(main.EditSelectedProjectCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Settings_OffersOnlyInstalledReleases(bool stable, bool insiders)
    {
        var editor = new FakeCodeEditorService
        {
            Editions = Enum.GetValues<CodeEditorEdition>()
                .Where(edition => edition == CodeEditorEdition.Stable ? stable : insiders).ToArray()
        };
        var vm = new SettingsViewModel(new FakeSettingsService(), new FakeThemeService(), codeEditorService: editor);

        Assert.Equal(editor.Editions, vm.AvailableCodeEditors);
        Assert.Equal(stable || insiders, vm.HasCodeEditor);
        if (!stable && !insiders)
            Assert.Null(vm.SelectedCodeEditor);
        else
            Assert.Equal(stable ? CodeEditorEdition.Stable : CodeEditorEdition.Insiders, vm.SelectedCodeEditor);
    }

    [Fact]
    public async Task Settings_SavesAndReloadsEditingPreferences()
    {
        var settings = new FakeSettingsService();
        var editor = new FakeCodeEditorService();
        var vm = new SettingsViewModel(settings, new FakeThemeService(), codeEditorService: editor)
        {
            EditingEnabled = true,
            SelectedCodeEditor = CodeEditorEdition.Insiders
        };

        await vm.SaveCommand.ExecuteAsync(null);
        var reloaded = new SettingsViewModel(settings, new FakeThemeService(), codeEditorService: editor);

        Assert.True(reloaded.EditingEnabled);
        Assert.Equal(CodeEditorEdition.Insiders, reloaded.SelectedCodeEditor);
    }

    [Theory]
    [InlineData(PresentationSourceType.LocalRoot)]
    [InlineData(PresentationSourceType.LocalProject)]
    [InlineData(PresentationSourceType.Git)]
    [InlineData(PresentationSourceType.GitHub)]
    public void Edit_OpensNonRemotePresentationInSelectedRelease(PresentationSourceType type)
    {
        var settings = new FakeSettingsService();
        settings.Settings.Editing.Enabled = true;
        settings.Settings.Editing.Edition = CodeEditorEdition.Insiders;
        var editor = new FakeCodeEditorService();
        var vm = CreateMain(settings, editor);
        vm.SelectedProject = Project(type);

        Assert.True(vm.ShowEditTab);
        Assert.True(vm.EditSelectedProjectCommand.CanExecute(null));
        vm.SelectEditRibbonCommand.Execute(null);
        Assert.True(vm.IsEditRibbonSelected);
        vm.EditSelectedProjectCommand.Execute(null);

        Assert.Equal(Path.GetTempPath(), editor.OpenedDirectory);
        Assert.Equal(CodeEditorEdition.Insiders, editor.OpenedEdition);
    }

    [Fact]
    public void Edit_ResolvesRelativeProjectLocation()
    {
        var settings = new FakeSettingsService();
        settings.Settings.Editing.Enabled = true;
        var editor = new FakeCodeEditorService();
        var vm = CreateMain(settings, editor);
        vm.SelectedProject = new PresentationProjectViewModel(new PresentationProject
        {
            Id = Guid.NewGuid(),
            Name = "Slides",
            SourceType = PresentationSourceType.LocalProject,
            Location = "./talk"
        });

        vm.EditSelectedProjectCommand.Execute(null);

        Assert.Equal(Path.GetFullPath("./talk"), editor.OpenedDirectory);
    }

    [Fact]
    public void Edit_RemoteOrNoSelectionCannotLaunchAndLeavesHiddenTab()
    {
        var settings = new FakeSettingsService();
        settings.Settings.Editing.Enabled = true;
        var editor = new FakeCodeEditorService();
        var vm = CreateMain(settings, editor);
        vm.SelectedProject = Project(PresentationSourceType.LocalProject);
        vm.SelectEditRibbonCommand.Execute(null);

        vm.SelectedProject = Project(PresentationSourceType.HostedUrl);

        Assert.False(vm.ShowEditTab);
        Assert.False(vm.IsEditRibbonSelected);
        Assert.False(vm.EditSelectedProjectCommand.CanExecute(null));
        vm.EditSelectedProjectCommand.Execute(null);
        Assert.Null(editor.OpenedDirectory);

        vm.SelectedProject = null;
        Assert.False(vm.ShowEditTab);
        Assert.False(vm.EditSelectedProjectCommand.CanExecute(null));
    }

    [Fact]
    public void RefreshPreferences_UpdatesAvailabilityAndResetsHiddenEditTab()
    {
        var settings = new FakeSettingsService();
        var editor = new FakeCodeEditorService();
        var vm = CreateMain(settings, editor);
        vm.SelectedProject = Project(PresentationSourceType.LocalProject);
        settings.Settings.Editing.Enabled = true;
        vm.RefreshPreferences();
        Assert.True(vm.ShowEditTab);
        vm.SelectEditRibbonCommand.Execute(null);

        editor.Editions = [CodeEditorEdition.Insiders];
        vm.RefreshPreferences();

        Assert.False(vm.ShowEditTab);
        Assert.False(vm.IsEditRibbonSelected);
        Assert.False(vm.EditSelectedProjectCommand.CanExecute(null));

        settings.Settings.Editing.Edition = CodeEditorEdition.Insiders;
        vm.RefreshPreferences();
        Assert.True(vm.ShowEditTab);

        settings.Settings.Editing.Enabled = false;
        vm.RefreshPreferences();
        Assert.False(vm.ShowEditTab);
    }

    [Fact]
    public void Edit_LaunchFailureShowsRecoveryMessage()
    {
        var settings = new FakeSettingsService();
        settings.Settings.Editing.Enabled = true;
        var editor = new FakeCodeEditorService { LaunchError = new InvalidOperationException("Editor was removed.") };
        var vm = CreateMain(settings, editor);
        vm.SelectedProject = Project(PresentationSourceType.LocalProject);

        vm.EditSelectedProjectCommand.Execute(null);

        Assert.Equal("Could not open VS Code: Editor was removed.", vm.LibraryMessage);
    }
}
