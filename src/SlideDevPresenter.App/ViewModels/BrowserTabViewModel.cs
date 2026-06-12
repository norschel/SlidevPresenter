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

    partial void OnUrlChanged(Uri value)
    {
        if (Title == _url.Host || string.IsNullOrEmpty(Title))
            Title = value.Host.Length > 0 ? value.Host : value.ToString();
    }
}
