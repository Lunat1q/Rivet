using Rivet.Core.Audio;
using Rivet.Core.Media;
using Rivet.Core.Pipeline;
using Rivet.Core.Subtitles;
using Rivet.Core.Transcription;
using static Rivet.Core.Tests.TestData;

namespace Rivet.Core.Tests;

public class TranscriptTests
{
    [Fact]
    public void Empty_is_empty()
    {
        Assert.True(Transcript.Empty.IsEmpty);
        Assert.Empty(Transcript.Empty.Words);
    }

    [Fact]
    public void Non_empty_reports_not_empty()
    {
        Assert.False(Clip(Word("hi", 0, 1)).IsEmpty);
    }

    [Fact]
    public void Text_joins_words_with_spaces()
    {
        var t = Clip(Word("hello", 0, 0.5), Word("world", 0.5, 1.0));
        Assert.Equal("hello world", t.Text);
    }
}

public class AudioBufferTests
{
    [Fact]
    public void Duration_is_samples_over_sample_rate()
    {
        var buffer = new AudioBuffer(new float[AudioConstants.SampleRate]); // exactly one second
        Assert.Equal(TimeSpan.FromSeconds(1), buffer.Duration);
    }

    [Fact]
    public void DurationOf_zero_samples_is_zero()
    {
        Assert.Equal(TimeSpan.Zero, AudioConstants.DurationOf(0));
    }

    [Fact]
    public void SampleRate_is_whisper_fixed_16k_mono()
    {
        Assert.Equal(16_000, AudioConstants.SampleRate);
        Assert.Equal(1, AudioConstants.Channels);
    }
}

public class VideoInfoTests
{
    [Theory]
    [InlineData(1080, 1920, true)]  // portrait
    [InlineData(1920, 1080, false)] // landscape
    [InlineData(1000, 1000, true)]  // square counts as portrait
    public void IsPortrait_when_height_at_least_width(int w, int h, bool expected)
    {
        Assert.Equal(expected, new VideoInfo(w, h, TimeSpan.Zero, 0).IsPortrait);
    }
}

public class JobProgressTests
{
    [Theory]
    [InlineData(JobStage.Preparing, "Reading the video…")]
    [InlineData(JobStage.IsolatingVocals, "Isolating vocals (this is slow)…")]
    [InlineData(JobStage.ExtractingAudio, "Extracting audio…")]
    [InlineData(JobStage.Transcribing, "Transcribing speech…")]
    [InlineData(JobStage.Rendering, "Building captions…")]
    [InlineData(JobStage.Burning, "Rendering the video…")]
    [InlineData(JobStage.Done, "Done")]
    public void Label_reads_for_humans(JobStage stage, string expected)
    {
        Assert.Equal(expected, new JobProgress(stage, 0).Label);
    }
}

public class DefaultsTests
{
    [Fact]
    public void SubtitleStyle_defaults_are_the_short_form_look()
    {
        var s = new SubtitleStyle();
        Assert.True(s.Uppercase);
        Assert.True(s.Bold);
        Assert.Equal(3, s.MaxWordsPerCaption);
        Assert.Equal(CaptionPosition.Center, s.Position);
        Assert.Equal("#FFFFFF", s.PrimaryColor);
    }

    [Fact]
    public void TranscriptionOptions_default_to_turbo_auto()
    {
        var o = new TranscriptionOptions();
        Assert.Equal(WhisperModel.LargeV3Turbo, o.Model);
        Assert.Equal(ComputeBackend.Auto, o.Backend);
        Assert.Equal("auto", o.Language);
    }

    [Fact]
    public void SubtitleJob_defaults_have_no_vocal_isolation()
    {
        var job = new SubtitleJob { InputVideoPath = "in.mp4", OutputVideoPath = "out.mp4" };
        Assert.False(job.IsolateVocals);
        Assert.NotNull(job.Style);
        Assert.NotNull(job.Transcription);
    }
}
