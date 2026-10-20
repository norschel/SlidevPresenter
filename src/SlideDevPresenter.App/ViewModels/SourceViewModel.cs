using CommunityToolkit.Mvvm.ComponentModel;
using SlideDevPresenter.Core.Models;

namespace SlideDevPresenter.App.ViewModels;

/// <summary>ViewModel for a single presentation source entry in the settings UI.</summary>
public sealed partial class SourceViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _location;

    [ObservableProperty]
    private PresentationSourceType _type;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _comment;

    [ObservableProperty]
    private bool _isPublic;

    [ObservableProperty]
    private bool _requiresAuthentication;

    public Guid Id { get; }

    public SourceViewModel(PresentationSource source)
    {
        Id = source.Id;
        _name = source.Name;
        _location = source.Location;
        _type = source.Type;
        _isEnabled = source.IsEnabled;
        _comment = source.Comment;
        _isPublic = source.IsPublic;
        _requiresAuthentication = source.RequiresAuthentication;
    }

    public PresentationSource ToModel() => new()
    {
        Id = Id,
        Name = Name,
        Location = Location,
        Type = Type,
        IsEnabled = IsEnabled,
        Comment = Comment,
        IsPublic = IsPublic,
        RequiresAuthentication = RequiresAuthentication
    };
}
