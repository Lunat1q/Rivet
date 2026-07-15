using Microsoft.Extensions.Logging;
using Rivet.Core.Abstractions;
using Rivet.Core.Transcription;
using Whisper.net;

namespace Rivet.Core.Whisper;

/// <summary>
/// Loads the GGML model into memory once and builds the whisper.cpp processor. The model
/// weights are the expensive part (0.8-3 GB); the processor is cheap. A reload only happens
/// when the model or the CPU/GPU choice changes.
/// </summary>
public sealed class WhisperTranscriberFactory : ITranscriberFactory, ITranscriptionBackend
{
    private readonly IWhisperModelProvider _modelProvider;
    private readonly ILogger<WhisperTranscriberFactory> _logger;
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    private WhisperFactory? _whisperFactory;
    private WhisperModel? _loadedModel;
    private bool _loadedOnGpu;
    private bool _disposed;

    public WhisperTranscriberFactory(
        IWhisperModelProvider modelProvider,
        ILogger<WhisperTranscriberFactory> logger)
    {
        _modelProvider = modelProvider;
        _logger = logger;
    }

    /// <summary>Progress of a model download, if one is needed. Surfaced by the UI before a job starts.</summary>
    public IProgress<double>? DownloadProgress { get; set; }

    public async Task<ITranscriber> CreateAsync(
        TranscriptionOptions options,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var factory = await GetOrLoadModelAsync(options.Model, options.Backend, cancellationToken)
            .ConfigureAwait(false);

        // Building a processor allocates a native whisper context — synchronous, not free.
        return await Task.Run(() => new WhisperTranscriber(BuildProcessor(factory, options)),
            cancellationToken).ConfigureAwait(false);
    }

    public string Backend => WhisperRuntime.Describe(_loadedOnGpu);

    public bool IsGpu => _loadedOnGpu && WhisperRuntime.GpuAvailable;

    private async Task<WhisperFactory> GetOrLoadModelAsync(
        WhisperModel model,
        ComputeBackend backend,
        CancellationToken cancellationToken)
    {
        var useGpu = WhisperRuntime.WantsGpu(backend);

        await _loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // The processor is baked into the model load, so a change of backend needs a reload
            // just as much as a change of model does.
            if (_whisperFactory is not null && _loadedModel == model && _loadedOnGpu == useGpu)
                return _whisperFactory;

            if (_whisperFactory is not null)
            {
                _logger.LogInformation("Reloading: model {Old} -> {New}, gpu {WasGpu} -> {UseGpu}",
                    _loadedModel, model, _loadedOnGpu, useGpu);
                _whisperFactory.Dispose();
                _whisperFactory = null;
                _loadedModel = null;
            }

            var path = await _modelProvider
                .GetModelPathAsync(model, DownloadProgress, cancellationToken)
                .ConfigureAwait(false);

            // Must happen before the first load: the native library is chosen when it loads.
            WhisperRuntime.Configure();

            _logger.LogInformation("Loading whisper.cpp model from {Path} (gpu: {UseGpu})", path, useGpu);

            // Task.Run, because FromPath is a synchronous native call that reads 1.5-3 GB of
            // weights. With the model cached every await above completes synchronously, so
            // without this the load runs on whatever thread pressed Go — the UI thread (ADR 0006).
            _whisperFactory = await Task
                .Run(() => WhisperFactory.FromPath(path, new WhisperFactoryOptions { UseGpu = useGpu }),
                    cancellationToken)
                .ConfigureAwait(false);

            _loadedModel = model;
            _loadedOnGpu = useGpu;

            // "GPU" means the user asked for one on purpose. Falling back to a CPU that is 60x
            // slower is not a fallback — say so instead.
            if (backend == ComputeBackend.Gpu && !WhisperRuntime.GpuAvailable)
            {
                _whisperFactory.Dispose();
                _whisperFactory = null;
                _loadedModel = null;
                _loadedOnGpu = false;

                throw new InvalidOperationException(
                    "No GPU backend could be loaded on this machine. Choose Automatic or CPU.");
            }

            _logger.LogInformation("whisper.cpp running on {Backend}", WhisperRuntime.Describe(_loadedOnGpu));

            return _whisperFactory;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private static WhisperProcessor BuildProcessor(WhisperFactory factory, TranscriptionOptions options)
    {
        var builder = factory.CreateBuilder()
            .WithProbabilities()
            .WithNoSpeechThreshold(options.NoSpeechThreshold)
            // Per-token start/end times, from which WhisperTranscriber rebuilds word timings.
            // The segments themselves stay whole phrases, which is what keeps the text correct —
            // forcing short segments corrupts the decode (see WhisperTranscriber).
            .WithTokenTimestamps();

        if (options.Threads > 0)
            builder = builder.WithThreads(options.Threads);

        builder = string.Equals(options.Language, "auto", StringComparison.OrdinalIgnoreCase)
            ? builder.WithLanguageDetection()
            : builder.WithLanguage(options.Language);

        return builder.Build();
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;
        _whisperFactory?.Dispose();
        _loadGate.Dispose();
        return ValueTask.CompletedTask;
    }
}
