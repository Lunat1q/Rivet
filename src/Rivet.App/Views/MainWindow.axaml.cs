using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Rivet.App.ViewModels;
using Rivet.Core.Media;

namespace Rivet.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

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
                new FilePickerFileType("Video") { Patterns = [.. VideoFile.Patterns] }
            ]
        });

        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path is not null)
            vm.SetInputVideo(path);
    }

    // Accept a single dropped video file; anything else shows no drop cursor and is ignored.
    private static string? DroppedVideo(DragEventArgs e)
    {
        var path = e.DataTransfer.TryGetFile()?.TryGetLocalPath();
        return path is not null && VideoFile.IsVideo(path) ? path : null;
    }

    private void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = DroppedVideo(e) is not null ? DragDropEffects.Copy : DragDropEffects.None;

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is MainViewModel vm && DroppedVideo(e) is { } path)
            vm.SetInputVideo(path);
    }
}
