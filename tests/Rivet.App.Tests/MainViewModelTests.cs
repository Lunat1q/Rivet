using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Rivet.App.Composition;
using Rivet.App.Updates;
using Rivet.App.ViewModels;
using Rivet.Core.Abstractions;
using Rivet.Core.Audio;
using Rivet.Core.Media;
using Rivet.Core.Pipeline;
using Rivet.Core.Transcription;

namespace Rivet.App.Tests;

/// <summary>
/// Drives the view model against fake media/transcription so the input→output naming and the
/// re-render versioning are exercised end to end, without ffmpeg or whisper.
/// </summary>
public sealed class MainViewModelTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("rivet-vm");
    private readonly IMediaProcessor _media = Substitute.For<IMediaProcessor>();
    private readonly ITranscriberFactory _factory = Substitute.For<ITranscriberFactory>();
    private readonly ITranscriber _transcriber = Substitute.For<ITranscriber>();
    private readonly IVocalIsolator _vocals = Substitute.For<IVocalIsolator>();
    private readonly ITranscriptionBackend _backend = Substitute.For<ITranscriptionBackend>();
    private readonly IUserSettingsStore _settings = Substitute.For<IUserSettingsStore>();
    private readonly IUpdateChecker _checker = Substitute.For<IUpdateChecker>();
    private readonly IUpdateInstaller _installer = Substitute.For<IUpdateInstaller>();

    public MainViewModelTests()
    {
        _media.ProbeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new VideoInfo(1080, 1920, TimeSpan.FromSeconds(5), 30));
        _media.ExtractAudioAsync(Arg.Any<string>(), Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>())
            .Returns(new AudioBuffer(new float[16_000]));
        _media.RenderPreviewFrameAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("no-such-frame.png"); // preview load fails and is swallowed by the VM
        _transcriber.TranscribeAsync(Arg.Any<AudioBuffer>(), Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>())
            .Returns(new Transcript([new TranscribedWord("hi", TimeSpan.Zero, TimeSpan.FromSeconds(1), 1f)]));
        _factory.CreateAsync(Arg.Any<TranscriptionOptions>(), Arg.Any<CancellationToken>()).Returns(_transcriber);
        _backend.Backend.Returns("CPU");
        _settings.Load().Returns(new UserSettings());
        _checker.CheckAsync().Returns((UpdateInfo?)null);

        // Every burn produces the output file, so the versioning sees prior renders on disk.
        _media.BurnSubtitlesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _media.When(m => m.BurnSubtitlesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>()))
            .Do(ci => File.WriteAllText((string)ci[2], "video"));
    }

    private MainViewModel NewViewModel()
    {
        var pipeline = new SubtitlePipeline(_media, _factory, _vocals, NullLogger<SubtitlePipeline>.Instance);
        var update = new UpdateViewModel(_checker, _installer, NullLogger<UpdateViewModel>.Instance);
        return new MainViewModel(pipeline, _media, _backend, _settings, update);
    }

    private string InputPath => Path.Combine(_dir.FullName, "clip.mp4");

    private async Task<MainViewModel> TranscribedViewModel()
    {
        var vm = NewViewModel();
        vm.SetInputVideo(InputPath);
        await vm.TranscribeCommand.ExecuteAsync(null);
        return vm;
    }

    public void Dispose() => _dir.Delete(recursive: true);

    [Fact]
    public void SetInputVideo_derives_the_captioned_output_next_to_the_input()
    {
        var vm = NewViewModel();
        vm.SetInputVideo(InputPath);

        Assert.True(vm.HasInput);
        Assert.Equal(Path.Combine(_dir.FullName, "clip-captioned.mp4"), vm.OutputPath);
    }

    [Fact]
    public void SetInputVideo_resets_editor_state()
    {
        var vm = NewViewModel();
        vm.SetInputVideo(InputPath);

        Assert.False(vm.IsEditing);
        Assert.False(vm.IsDone);
        Assert.Empty(vm.Words);
    }

    [Fact]
    public async Task Transcribe_moves_into_the_editor_with_words()
    {
        var vm = await TranscribedViewModel();

        Assert.True(vm.IsEditing);
        Assert.Single(vm.Words);
        Assert.Equal("hi", vm.Words[0].Text);
    }

    [Fact]
    public async Task First_render_writes_the_base_name()
    {
        var vm = await TranscribedViewModel();

        await vm.RenderCommand.ExecuteAsync(null);

        Assert.True(vm.IsDone);
        Assert.Equal(Path.Combine(_dir.FullName, "clip-captioned.mp4"), vm.OutputPath);
        Assert.True(File.Exists(vm.OutputPath));
    }

    [Fact]
    public async Task Second_render_does_not_overwrite_and_bumps_to_v2()
    {
        var vm = await TranscribedViewModel();

        await vm.RenderCommand.ExecuteAsync(null);
        await vm.RenderCommand.ExecuteAsync(null);

        Assert.Equal(Path.Combine(_dir.FullName, "clip-captioned_v2.mp4"), vm.OutputPath);
        Assert.True(File.Exists(Path.Combine(_dir.FullName, "clip-captioned.mp4")));
        Assert.True(File.Exists(Path.Combine(_dir.FullName, "clip-captioned_v2.mp4")));
    }

    [Fact]
    public async Task Third_render_bumps_to_v3()
    {
        var vm = await TranscribedViewModel();

        await vm.RenderCommand.ExecuteAsync(null);
        await vm.RenderCommand.ExecuteAsync(null);
        await vm.RenderCommand.ExecuteAsync(null);

        Assert.Equal(Path.Combine(_dir.FullName, "clip-captioned_v3.mp4"), vm.OutputPath);
    }

    [Fact]
    public async Task Render_versioning_is_monotonic_when_an_earlier_version_was_deleted()
    {
        // Simulate: v1 and v3 already on disk, v2 was deleted by the user.
        File.WriteAllText(Path.Combine(_dir.FullName, "clip-captioned.mp4"), "");
        File.WriteAllText(Path.Combine(_dir.FullName, "clip-captioned_v3.mp4"), "");

        var vm = await TranscribedViewModel();
        await vm.RenderCommand.ExecuteAsync(null);

        // Must not reuse the freed v2 slot — the newest render is always past the highest seen.
        Assert.Equal(Path.Combine(_dir.FullName, "clip-captioned_v4.mp4"), vm.OutputPath);
    }

    [Fact]
    public async Task Reset_returns_to_setup_and_clears_the_editor()
    {
        var vm = await TranscribedViewModel();

        vm.ResetCommand.Execute(null);

        Assert.False(vm.IsEditing);
        Assert.Empty(vm.Words);
    }
}
