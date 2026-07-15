using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rivet.App.ViewModels;
using Rivet.Core.Abstractions;
using Rivet.Core.Media.Demucs;
using Rivet.Core.Media.FFmpeg;
using Rivet.Core.Pipeline;
using Rivet.Core.Whisper;

namespace Rivet.App.Composition;

/// <summary>
/// The one place concrete types are named (ADR 0002). Everything else takes interfaces, which
/// is what keeps whisper.cpp and ffmpeg swappable.
/// </summary>
public static class ServiceRegistration
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder
            .AddDebug()
            .SetMinimumLevel(LogLevel.Information));

        // Media: ffmpeg/ffprobe (ADR 0002). Replace this pair to change how video is handled.
        services.AddSingleton<FFmpegLocator>(_ => new FFmpegLocator());
        services.AddSingleton<IMediaProcessor, FFmpegMediaProcessor>();

        // Optional vocal isolation before transcription (ADR 0009). Only runs when the user asks;
        // errors only then if Demucs isn't installed.
        services.AddSingleton<IVocalIsolator, DemucsVocalIsolator>();

        // Transcription: ggml-org/whisper.cpp via Whisper.net (ADR 0001).
        services.AddSingleton<IWhisperModelProvider, WhisperModelProvider>();
        services.AddSingleton<WhisperTranscriberFactory>();
        services.AddSingleton<ITranscriberFactory>(sp => sp.GetRequiredService<WhisperTranscriberFactory>());
        services.AddSingleton<ITranscriptionBackend>(sp => sp.GetRequiredService<WhisperTranscriberFactory>());

        services.AddSingleton<SubtitlePipeline>();

        services.AddSingleton<IUserSettingsStore, JsonUserSettingsStore>();
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
