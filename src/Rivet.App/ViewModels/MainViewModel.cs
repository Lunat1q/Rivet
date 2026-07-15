using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rivet.App.Composition;
using Rivet.Core.Abstractions;
using Rivet.Core.Media;
using Rivet.Core.Pipeline;
using Rivet.Core.Subtitles;
using Rivet.Core.Transcription;

namespace Rivet.App.ViewModels;

/// <summary>
/// Drives the three-step flow: pick a video and settings → <b>Transcribe</b> → edit the words and
/// their timing while watching a live preview frame → <b>Render</b> to burn them in. Editing is
/// optional: once transcribed you can render immediately. Bindable option lists live in
/// <see cref="Choices" />.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly SubtitlePipeline _pipeline;
    private readonly IMediaProcessor _media;
    private readonly ITranscriptionBackend _backend;
    private readonly IUserSettingsStore _settingsStore;
    private CancellationTokenSource? _cts;
    private VideoInfo? _video;
    private bool _previewInFlight;

    public MainViewModel(
        SubtitlePipeline pipeline,
        IMediaProcessor media,
        ITranscriptionBackend backend,
        IUserSettingsStore settingsStore,
        UpdateViewModel update)
    {
        _pipeline = pipeline;
        _media = media;
        _backend = backend;
        _settingsStore = settingsStore;
        Update = update;
        Restore(settingsStore.Load());

        ModelDownloadProgress = new Progress<double>(f =>
            StatusLine = f >= 1 ? "Loading model…" : $"Downloading speech model… {f:P0}");

        // Re-evaluate the offer as the update state and busy state change (installing quits the app).
        Update.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanOfferUpdate));
        _ = Update.CheckInBackgroundAsync();
    }

    /// <summary>The installed version, shown in the header.</summary>
    public string Version => AppVersion.Current;

    public UpdateViewModel Update { get; }

    /// <summary>Offer an update only when idle — installing closes the app, so never mid-job.</summary>
    public bool CanOfferUpdate => Update.IsOffered && !IsBusy;

    // Options
    public IReadOnlyList<Choice<WhisperModel>> Qualities => Choices.Qualities;
    public IReadOnlyList<Choice<ComputeBackend>> Processings => Choices.Processings;
    public IReadOnlyList<Choice<CaptionPosition>> Positions => Choices.Positions;
    public IReadOnlyList<Choice<string>> Fonts => Choices.Fonts;
    public IReadOnlyList<Choice<string>> PrimaryColors => Choices.PrimaryColors;
    public IReadOnlyList<Choice<string>> HighlightColors => Choices.HighlightColors;

    // Selections
    [ObservableProperty] private Choice<WhisperModel> _quality = Choices.Qualities[1];
    [ObservableProperty] private Choice<ComputeBackend> _processing = Choices.Processings[0];
    [ObservableProperty] private Choice<CaptionPosition> _position = Choices.Positions[0];
    [ObservableProperty] private Choice<string> _font = Choices.Fonts[0];
    [ObservableProperty] private Choice<string> _primaryColor = Choices.PrimaryColors[0];
    [ObservableProperty] private Choice<string> _highlightColor = Choices.HighlightColors[0];
    [ObservableProperty] private double _fontSizePercent = 7.0;
    [ObservableProperty] private int _wordsPerCaption = 3;
    [ObservableProperty] private bool _uppercase = true;
    [ObservableProperty] private bool _isolateVocals;

    // Job I/O
    [ObservableProperty] private string? _inputPath;
    [ObservableProperty] private string? _outputPath;

    // Runtime state
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isDone;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _statusLine = "Choose a video to get started.";
    [ObservableProperty] private string? _errorMessage;

    // Editor
    public ObservableCollection<WordViewModel> Words { get; } = [];
    [ObservableProperty] private Bitmap? _previewImage;
    [ObservableProperty] private double _previewTime;
    [ObservableProperty] private double _videoDuration = 1;

    public IProgress<double> ModelDownloadProgress { get; }
    public bool HasInput => !string.IsNullOrEmpty(InputPath);

    /// <summary>Called by the view once the user has picked a file (dialogs need the window).</summary>
    public void SetInputVideo(string path)
    {
        InputPath = path;
        OutputPath = OutputNaming.CaptionedPath(path);
        ErrorMessage = null;
        IsDone = false;
        IsEditing = false;
        Words.Clear();
        StatusLine = $"Ready: {Path.GetFileName(path)}";
    }

    [RelayCommand(CanExecute = nameof(CanTranscribe))]
    private async Task TranscribeAsync()
    {
        if (InputPath is null || OutputPath is null)
            return;

        _settingsStore.Save(Capture());
        Begin();

        try
        {
            var (transcript, info) = await _pipeline.TranscribeAsync(BuildJob(), Reporter(), _cts!.Token);
            _video = info;
            VideoDuration = Math.Max(0.1, info.Duration.TotalSeconds);
            PreviewTime = Math.Min(1.0, VideoDuration / 2);

            Words.Clear();
            foreach (var w in transcript.Words)
                Words.Add(new WordViewModel(w));

            IsEditing = true;
            StatusLine = $"Transcribed {Words.Count} words on {_backend.Backend}. Edit if needed, then render.";
            await RefreshPreviewAsync();
        }
        catch (OperationCanceledException) { StatusLine = "Cancelled."; }
        catch (Exception ex) { ErrorMessage = ex.Message; StatusLine = "Something went wrong."; }
        finally { End(); }
    }

    [RelayCommand(CanExecute = nameof(CanRender))]
    private async Task RenderAsync()
    {
        if (_video is null || OutputPath is null)
            return;

        Begin();
        IsDone = false;

        // Never clobber an earlier render: clip-captioned.mp4, then _v2, _v3, … Version off the
        // input (not the last output) so re-renders don't stack _v2_v2, and monotonically so a
        // deleted _v2 is not reused.
        OutputPath = OutputNaming.NextVersionedPath(OutputNaming.CaptionedPath(InputPath!));

        try
        {
            await _pipeline.RenderAsync(BuildJob(), CurrentTranscript(), _video.Value, Reporter(), _cts!.Token);
            IsDone = true;
            StatusLine = $"Done — saved to {Path.GetFileName(OutputPath)}.";
        }
        catch (OperationCanceledException) { StatusLine = "Cancelled."; }
        catch (Exception ex) { ErrorMessage = ex.Message; StatusLine = "Something went wrong."; }
        finally { End(); }
    }

    /// <summary>Re-render the preview frame with the current words, timings and style.</summary>
    [RelayCommand]
    private async Task RefreshPreviewAsync()
    {
        if (_video is null || InputPath is null || _previewInFlight)
            return;

        var transcript = CurrentTranscript();
        if (transcript.IsEmpty)
            return;

        _previewInFlight = true;
        try
        {
            var assPath = await SubtitlePipeline.WriteAssAsync(transcript, BuildStyle(), _video.Value);
            try
            {
                var png = await _media.RenderPreviewFrameAsync(
                    InputPath, assPath, TimeSpan.FromSeconds(PreviewTime));
                var bitmap = new Bitmap(png);
                PreviewImage?.Dispose();
                PreviewImage = bitmap;
                TryDelete(png);
            }
            finally { TryDelete(assPath); }
        }
        catch (Exception ex)
        {
            // A preview failure must never take the editor down — the render is what matters.
            StatusLine = "Couldn't refresh preview: " + ex.Message;
        }
        finally { _previewInFlight = false; }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (OutputPath is null || !File.Exists(OutputPath))
            return;

        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{OutputPath}\"") { UseShellExecute = true });
    }

    /// <summary>Back to the setup card to re-transcribe (e.g. after toggling vocal isolation).</summary>
    [RelayCommand]
    private void Reset()
    {
        IsEditing = false;
        IsDone = false;
        Words.Clear();
        _video = null;
        StatusLine = HasInput ? $"Ready: {Path.GetFileName(InputPath!)}" : "Choose a video to get started.";
    }

    private bool CanTranscribe() => HasInput && !IsBusy;
    private bool CanRender() => IsEditing && !IsBusy;

    // Auto-refresh the frame as the user scrubs; the in-flight guard drops the intermediate
    // positions so ffmpeg is never queued up.
    partial void OnPreviewTimeChanged(double value)
    {
        if (IsEditing && !IsBusy)
            _ = RefreshPreviewAsync();
    }

    partial void OnInputPathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasInput));
        TranscribeCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        TranscribeCommand.NotifyCanExecuteChanged();
        RenderCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanOfferUpdate));
    }

    partial void OnIsEditingChanged(bool value) => RenderCommand.NotifyCanExecuteChanged();

    private SubtitleJob BuildJob() => new()
    {
        InputVideoPath = InputPath!,
        OutputVideoPath = OutputPath!,
        Transcription = new TranscriptionOptions { Model = Quality.Value, Backend = Processing.Value },
        Style = BuildStyle(),
        IsolateVocals = IsolateVocals
    };

    private Transcript CurrentTranscript() => new(
        Words.Select(w => w.ToWord()).Where(w => w.Text.Length > 0).ToList());

    private SubtitleStyle BuildStyle() => new()
    {
        FontName = Font.Value,
        FontSizePercent = FontSizePercent,
        PrimaryColor = PrimaryColor.Value,
        HighlightColor = HighlightColor.Value,
        Uppercase = Uppercase,
        MaxWordsPerCaption = WordsPerCaption,
        Position = Position.Value
    };

    private void Begin()
    {
        ErrorMessage = null;
        IsBusy = true;
        Progress = 0;
        _cts = new CancellationTokenSource();
    }

    private void End()
    {
        IsBusy = false;
        _cts?.Dispose();
        _cts = null;
    }

    private IProgress<JobProgress> Reporter() => new Progress<JobProgress>(p =>
    {
        StatusLine = p.Label;
        Progress = p.Fraction * 100;
    });

    private void Restore(UserSettings s)
    {
        Quality = Pick(Choices.Qualities, s.QualityName, Choices.Qualities[1]);
        Processing = Pick(Choices.Processings, s.ProcessingName, Choices.Processings[0]);
        Position = Pick(Choices.Positions, s.PositionName, Choices.Positions[0]);
        Font = Pick(Choices.Fonts, s.FontName, Choices.Fonts[0]);
        PrimaryColor = Pick(Choices.PrimaryColors, s.PrimaryColorName, Choices.PrimaryColors[0]);
        HighlightColor = Pick(Choices.HighlightColors, s.HighlightColorName, Choices.HighlightColors[0]);
        FontSizePercent = s.FontSizePercent ?? 7.0;
        WordsPerCaption = s.WordsPerCaption ?? 3;
        Uppercase = s.Uppercase ?? true;
        IsolateVocals = s.IsolateVocals ?? false;
    }

    private UserSettings Capture() => new()
    {
        QualityName = Quality.Name,
        ProcessingName = Processing.Name,
        PositionName = Position.Name,
        FontName = Font.Name,
        PrimaryColorName = PrimaryColor.Name,
        HighlightColorName = HighlightColor.Name,
        FontSizePercent = FontSizePercent,
        WordsPerCaption = WordsPerCaption,
        Uppercase = Uppercase,
        IsolateVocals = IsolateVocals
    };

    private static Choice<T> Pick<T>(IReadOnlyList<Choice<T>> options, string? name, Choice<T> fallback) =>
        options.FirstOrDefault(o => o.Name == name) ?? fallback;

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { /* temp; ignored */ }
    }
}
