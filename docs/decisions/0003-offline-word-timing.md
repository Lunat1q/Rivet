# ADR 0003 — Offline whole-file transcription; word timing from token timestamps

**Status:** accepted · **Date:** 2026-07-14

## Context

Steno fakes streaming: it VAD-cuts live audio into utterances because it must show words while
someone is still talking. Rivet has the whole clip on disk before it starts, and the highlight
must land on the exact word being spoken — so its needs are the opposite: no latency pressure,
but tight per-word timing.

## Decision

**Transcribe the whole track in one pass**, no segmentation state machine. `ExtractAudioAsync`
decodes the entire clip to a single `AudioBuffer` (16 kHz mono f32) and hands it to
`ITranscriber.TranscribeAsync`. Short-form clips are minutes, not hours; the buffer is a few MB.

**Get per-word timing from whisper's token timestamps.** The processor is built with
`WithTokenTimestamps()`; each decoded token then carries a start/end in centiseconds.
`WhisperTranscriber` regroups the sub-word BPE tokens into words — a new word begins at every
token with a leading space, punctuation sticks to the word before it — and takes each word's
time from its first and last token. No separate alignment model.

## Options considered

- **Force one word per segment** with `WithMaxSegmentLength(1).SplitOnWord()`. This was the
  first attempt and it is **wrong**: constraining the segment length corrupts the decode — the
  transcribed *text* came back garbled, and the segment times with it. Verified against the
  same clip, where a plain decode produces perfect text. Reserving segment length for its real
  job (readable line breaks) and taking timing from tokens is the fix.
- **Segment-level timing + proportional word split.** A whisper segment is often a whole
  sentence; splitting its time evenly across words drifts badly, and drift is exactly what the
  highlight can't have.
- **DTW timestamps** (`WithDtwTimestamps`, alignment heads). Smoother still, but model-specific
  and heavier. Token timestamps proved accurate enough — measured word times land on the spoken
  word across a real reel — so DTW is the upgrade path, not the baseline. `ITranscriber` doesn't
  change either way.

## Consequences

- whisper's own no-speech probability gates out music/noise segments (`> 0.8` dropped); its
  control tokens (`[_BEG_]`, `[_TT_nnn]`, `<|..|>`) are skipped, and inline non-speech
  annotations (`[music]`, `(laughs)`) are stripped in `Clean`.
- Where whisper collapses a phrase's tokens onto one timestamp, those words share a start; the
  highlight steps through them together. Rare, and DTW is the fix if it ever matters.
- A clip with no speech fails the job with a plain "no speech found" message rather than
  producing an empty video.
