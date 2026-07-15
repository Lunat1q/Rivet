using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Rivet.App.ViewModels;

namespace Rivet.App.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    // The file picker needs the window, so it lives here rather than in the view model.
    private async void OnChooseVideoClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a video",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Video")
                {
                    Patterns = ["*.mp4", "*.mov", "*.mkv", "*.webm", "*.avi", "*.m4v"]
                }
            ]
        });

        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path is not null)
            vm.SetInputVideo(path);
    }
}
