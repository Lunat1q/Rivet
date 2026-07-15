using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Rivet.App.Composition;
using Rivet.App.ViewModels;
using Rivet.App.Views;
using Rivet.Core.Whisper;

namespace Rivet.App;

public partial class App : Application
{
    private IServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        _services = ServiceRegistration.Build();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = _services.GetRequiredService<MainViewModel>();

            // Model downloads happen inside the factory; the bar lives in the view model.
            _services.GetRequiredService<WhisperTranscriberFactory>().DownloadProgress =
                viewModel.ModelDownloadProgress;

            desktop.MainWindow = new MainWindow { DataContext = viewModel };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
