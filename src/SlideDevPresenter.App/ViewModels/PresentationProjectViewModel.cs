using CommunityToolkit.Mvvm.ComponentModel;
using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.App.ViewModels;

/// <summary>ViewModel for a single discovered presentation project in the library.</summary>
public sealed partial class PresentationProjectViewModel : ObservableObject
{
    public Guid Id { get; }
    public Guid? RepositorySourceId { get; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _location;

    [ObservableProperty]
    private PresentationSourceType _sourceType;

    [ObservableProperty]
    private string? _slidesFilePath;

    public PresentationProjectViewModel(PresentationProject project, Guid? repositorySourceId = null)
    {
        Id = project.Id;
        RepositorySourceId = repositorySourceId;
        _name = project.Name;
        _location = project.Location;
        _sourceType = project.SourceType;
        _slidesFilePath = project.SlidesFilePath;
    }

    public PresentationProject ToModel() => new()
    {
        Id = Id,
        Name = Name,
        Location = Location,
        SourceType = SourceType,
        SlidesFilePath = SlidesFilePath
    };
}
