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
    [NotifyPropertyChangedFor(nameof(IsRepository))]
    [NotifyPropertyChangedFor(nameof(CanEnable))]
    [NotifyPropertyChangedFor(nameof(IsLocal))]
    [NotifyPropertyChangedFor(nameof(TypeName))]
    private PresentationSourceType _type;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string? _checkoutPath;

    [ObservableProperty]
    private string? _branch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEnable))]
    private bool _isGitAvailable;

    public bool IsRepository => Type is PresentationSourceType.Git or PresentationSourceType.GitHub;
    public bool CanEnable => !IsRepository || IsGitAvailable;
    public bool IsLocal => Type is PresentationSourceType.LocalRoot or PresentationSourceType.LocalProject;
    public string TypeName
    {
        get => IsLocal ? "Local" : Type.ToString();
        set => Type = value == "Local" ? PresentationSourceType.LocalRoot : Enum.Parse<PresentationSourceType>(value);
    }

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
        _type = source.Type == PresentationSourceType.LocalProject ? PresentationSourceType.LocalRoot : source.Type;
        _isEnabled = source.IsEnabled;
        _comment = source.Comment;
        _isPublic = source.IsPublic;
        _requiresAuthentication = source.RequiresAuthentication;
        _checkoutPath = source.CheckoutPath;
        _branch = source.Branch;
    }

    public PresentationSource ToModel() => new()
    {
        Id = Id,
        Name = Name,
        Location = Location,
        Type = Type,
        CheckoutPath = CheckoutPath,
        Branch = Branch,
        IsEnabled = IsEnabled,
        Comment = Comment,
        IsPublic = IsPublic,
        RequiresAuthentication = RequiresAuthentication
    };
}
