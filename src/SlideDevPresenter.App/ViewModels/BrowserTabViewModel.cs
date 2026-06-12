using CommunityToolkit.Mvvm.ComponentModel;

namespace SlideDevPresenter.App.ViewModels;

public sealed partial class BrowserTabViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private Uri _url;

    public BrowserTabViewModel(Uri url)
    {
        _url = url;
        _title = url.Host.Length > 0 ? url.Host : url.ToString();
    }

    partial void OnUrlChanged(Uri? oldValue, Uri newValue)
    {
        var oldDisplay = oldValue is not null
            ? (oldValue.Host.Length > 0 ? oldValue.Host : oldValue.ToString())
            : string.Empty;
        if (Title == oldDisplay || string.IsNullOrEmpty(Title))
            Title = newValue.Host.Length > 0 ? newValue.Host : newValue.ToString();
    }
}
