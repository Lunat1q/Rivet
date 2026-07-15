using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Rivet.Core.Abstractions;
using Rivet.Core.Audio;
using Rivet.Core.Media;
using Rivet.Core.Pipeline;
using Rivet.Core.Subtitles;
using Rivet.Core.Transcription;
using static Rivet.Core.Tests.TestData;

namespace Rivet.Core.Tests;

public class SubtitlePipelineTests
{
    private readonly IMediaProcessor _media = Substitute.For<IMediaProcessor>();
    private readonly ITranscriberFactory _factory = Substitute.For<ITranscriberFactory>();
    private readonly ITranscriber _transcriber = Substitute.For<ITranscriber>();
    private readonly IVocalIsolator _vocals = Substitute.For<IVocalIsolator>();

    private readonly VideoInfo _info = new(1080, 1920, TimeSpan.FromSeconds(10), 30);

    private SubtitlePipeline Build() =>
        new(_media, _factory, _vocals, NullLogger<SubtitlePipeline>.Instance);

    private void ArrangeHappyPath(Transcript transcript)
    {
        _media.ProbeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_info);
        _media.ExtractAudioAsync(Arg.Any<string>(), Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>())
            .Returns(new AudioBuffer(new float[16_000]));
        _transcriber.TranscribeAsync(Arg.Any<AudioBuffer>(), Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>())
            .Returns(transcript);
        _factory.CreateAsync(Arg.Any<TranscriptionOptions>(), Arg.Any<CancellationToken>())
            .Returns(_transcriber);
    }

    private static SubtitleJob Job(bool isolate = false) => new()
    {
        InputVideoPath = "in.mp4",
        OutputVideoPath = "out.mp4",
        IsolateVocals = isolate
    };

    [Fact]
    public async Task Transcribe_probes_extracts_and_transcribes()
    {
        ArrangeHappyPath(Clip(Word("hi", 0, 0.5)));

        var (transcript, info) = await Build().TranscribeAsync(Job());

        Assert.Equal(_info, info);
        Assert.Equal("hi", transcript.Text);
        await _media.Received(1).ProbeAsync("in.mp4", Arg.Any<CancellationToken>());
        await _media.Received(1).ExtractAudioAsync("in.mp4", Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transcribe_skips_vocal_isolation_by_default()
    {
        ArrangeHappyPath(Clip(Word("hi", 0, 0.5)));

        await Build().TranscribeAsync(Job(isolate: false));

        await _vocals.DidNotReceiveWithAnyArgs()
            .IsolateVocalsAsync(default!, default, default);
    }

    [Fact]
    public async Task Transcribe_isolates_vocals_and_extracts_from_the_isolated_track()
    {
        ArrangeHappyPath(Clip(Word("hi", 0, 0.5)));
        _vocals.IsolateVocalsAsync(Arg.Any<string>(), Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>())
            .Returns("vocals.wav");

        await Build().TranscribeAsync(Job(isolate: true));

        // Audio must be pulled from the isolated file, not the original video.
        await _media.Received(1).ExtractAudioAsync("vocals.wav", Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>());
        await _media.DidNotReceive().ExtractAudioAsync("in.mp4", Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transcribe_throws_a_clear_error_when_no_speech_is_found()
    {
        ArrangeHappyPath(Transcript.Empty);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Build().TranscribeAsync(Job()));
        Assert.Contains("No speech", ex.Message);
    }

    [Fact]
    public async Task Transcribe_disposes_the_transcriber()
    {
        ArrangeHappyPath(Clip(Word("hi", 0, 0.5)));

        await Build().TranscribeAsync(Job());

        await _transcriber.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task Render_writes_an_ass_file_then_burns_it_and_cleans_up()
    {
        string? capturedAss = null;
        _media.BurnSubtitlesAsync(
                Arg.Any<string>(), Arg.Do<string>(p => capturedAss = p),
                Arg.Any<string>(), Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await Build().RenderAsync(Job(), Clip(Word("hi", 0, 0.5)), _info);

        Assert.NotNull(capturedAss);
        await _media.Received(1).BurnSubtitlesAsync(
            "in.mp4", Arg.Any<string>(), "out.mp4", Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>());
        Assert.False(File.Exists(capturedAss), "temp .ass should be deleted after the burn");
    }

    [Fact]
    public async Task Render_deletes_the_temp_ass_even_when_the_burn_fails()
    {
        string? capturedAss = null;
        _media.BurnSubtitlesAsync(
                Arg.Any<string>(), Arg.Do<string>(p => capturedAss = p),
                Arg.Any<string>(), Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("ffmpeg boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Build().RenderAsync(Job(), Clip(Word("hi", 0, 0.5)), _info));

        Assert.NotNull(capturedAss);
        Assert.False(File.Exists(capturedAss), "temp .ass should be deleted even on failure");
    }

    [Fact]
    public async Task Run_chains_transcribe_then_render()
    {
        ArrangeHappyPath(Clip(Word("hi", 0, 0.5)));

        await Build().RunAsync(Job());

        await _media.Received().ProbeAsync("in.mp4", Arg.Any<CancellationToken>());
        await _media.Received(1).BurnSubtitlesAsync(
            "in.mp4", Arg.Any<string>(), "out.mp4", Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteAssAsync_produces_a_readable_ass_file()
    {
        var path = await SubtitlePipeline.WriteAssAsync(
            Clip(Word("hi", 0, 0.5)), new SubtitleStyle(), _info);
        try
        {
            Assert.True(File.Exists(path));
            Assert.EndsWith(".ass", path);
            Assert.Contains("[Script Info]", await File.ReadAllTextAsync(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Transcribe_reports_progress()
    {
        ArrangeHappyPath(Clip(Word("hi", 0, 0.5)));
        var stages = new List<JobStage>();

        // A synchronous reporter so assertions don't race Progress<T>'s thread-pool post.
        await Build().TranscribeAsync(Job(), new SyncProgress<JobProgress>(p => stages.Add(p.Stage)));

        Assert.Contains(JobStage.Preparing, stages);
    }

    // A synchronous IProgress so assertions don't race the thread pool.
    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
