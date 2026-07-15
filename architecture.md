# Rivet — Architecture

Automatic viral subtitles for short-form video, on Windows. You give Rivet a clip; it
transcribes the speech locally with [whisper.cpp](https://github.com/ggml-org/whisper.cpp),
lays the words out as TikTok/Shorts-style captions with the spoken word highlighted, and
burns them into the video. Audio never leaves the machine.

---

## 1. Problem shape

Rivet is **offline and batch**, not live. That one fact drives most of the design:

- The whole clip is on disk before we start, so there is no streaming, no VAD state machine,
  no partial-result trickery. whisper.cpp transcribes a finite buffer — we hand it the whole
  track (ADR 0003).
- The product is not the transcript, it is the *rendered video*. So the two hard parts are
  **per-word timing** (the highlight has to land on the word being said) and **rendering that
  survives any footage** (thick outline, big text). Both are solved with tools that already
  exist: whisper.cpp for timing, libass for rendering (ADR 0005).

---

## 2. Layers

```
┌──────────────────────── Rivet.App (Avalonia, MVVM) ────────────────────────┐
│  Styles/Tokens+Controls   the palette; one accent, everything else grey     │
│  Views/MainWindow         one screen: pick a video, tune the look, Generate │
│  ViewModels/MainViewModel drives the pipeline, turns progress into a bar    │
│         ↓ uses interfaces only (no ffmpeg / Whisper.net types)              │
└───────────────────────────────────┬─────────────────────────────────────────┘
                                    │ (interfaces + POCOs across the boundary)
┌──────────────────────── Rivet.Core (net, headless) ───────────────────────┐
│  Pipeline    SubtitlePipeline: probe → extract → transcribe → lay out → burn │
│  Media       IMediaProcessor, VideoInfo  (ffmpeg/ffprobe behind it)          │
│  Transcription ITranscriber, Transcript (word-timed), TranscriptionOptions   │
│  Whisper     WhisperTranscriber, WhisperModelProvider, WhisperRuntime        │
│  Subtitles   Caption, CaptionBuilder, SubtitleStyle, AssSubtitleWriter       │
│  Audio       AudioBuffer (16k mono f32)                                       │
└─────────────────────────────────────────────────────────────────────────────┘
```

`Rivet.Core` has **zero** dependency on Avalonia. It knows only interfaces + POCOs.
ffmpeg and whisper.cpp are each behind one interface (`IMediaProcessor`, `ITranscriber`) and
wired in `Rivet.App/Composition`. Physically these are folders inside two projects, not many
assemblies — the layer boundary is real, extra csproj files to enforce it are not (ADR 0007).

---

## 3. The pipeline

`SubtitlePipeline` splits into two user-driven halves with an editing step between them, so a
wrong word or a loose highlight is fixed before the one full burn (ADR 0010). Each half reports
into one `IProgress<JobProgress>`.

```
TranscribeAsync:
  video file
    → ffprobe                → VideoInfo (WxH, duration, fps)   — sizes the caption canvas
    → [Demucs, optional]     → isolated vocals track            — only when the user asks (ADR 0009)
    → ffmpeg                 → 16 kHz mono float32 PCM on stdout — the only format whisper.cpp reads
    → whisper.cpp            → Transcript (word + start/end + confidence)

  ── editor ── user fixes text / realigns words, watching a live preview frame
               (RenderPreviewFrameAsync burns one frame through the real ffmpeg+libass path)

RenderAsync:
    → CaptionBuilder         → captions (few words each, split on pauses & sentence ends)
    → AssSubtitleWriter      → an .ass file (one Dialogue per word, cards tiled so none overlap)
    → ffmpeg + libass        → captioned .mp4
```

`RunAsync` chains both halves for a headless one-shot. Nothing but the composition root names a
concrete type, so the ffmpeg, whisper.cpp and Demucs choices stay swappable.

---

## 4. Per-word timing

The highlight has to sit on the word being spoken, so segment-level timing (a whole sentence
at once) is not enough. The processor is built with `WithTokenTimestamps()`, and
`WhisperTranscriber` regroups whisper's sub-word tokens into words (a new word at every
leading-space token) and takes each word's time from its first and last token. Forcing short
segments instead was tried and rejected — it corrupts the decoded text. See ADR 0003.

---

## 5. Rendering (why ASS + libass, not our own frames)

Burning captions is done by writing an **ASS** subtitle file and letting ffmpeg's `subtitles`
filter (libass) render it. We do not rasterise frames ourselves — libass already does outline,
shadow, positioning, scaling and per-character layout correctly, and ffmpeg already re-encodes
the video. The "one word pops" effect is **one Dialogue event per word** with a full override
block (colour + scale), not ASS karaoke (`\k`) — `\k` can only cross-fade a fill, it cannot
grow the active word. See ADR 0005.

`SubtitleStyle` carries every knob the user turns. Sizes that must scale with the clip (font,
outline, vertical margin) are stored as a **percentage of video height**, so one saved preset
looks the same on a 720p and a 4K export.

---

## 6. CPU vs GPU

The single biggest factor in how long a render takes. On a discrete GPU (Vulkan) whisper.cpp
runs faster than real time; on a CPU it is many times slower. Vulkan is used, not CUDA (it
covers NVIDIA + AMD + Intel from one package and does not crash on the newest cards), and CPU
vs GPU is a per-model-load flag, not a library choice — so it can change between jobs without
restarting. See ADR 0004.

---

## 7. Deliberately deferred

| Thing | Why deferred | Seam that exists |
|---|---|---|
| Playing (video) preview | A single-frame preview reuses the real renderer and is enough (ADR 0010) | `RenderPreviewFrameAsync` |
| Bundling ffmpeg / Demucs | Large; licence/build is the user's call, editors already have ffmpeg | `FFmpegLocator`, `IVocalIsolator` |
| Non-Windows | Windows-first; nothing in Core is deeply Windows-bound except the target framework | `IMediaProcessor` |
| Word-level DTW timestamps | token timestamps + user realignment cover it | `ITranscriber` |
| Animated caption entrance (pop/slide) | Static highlight is the core look | ASS override blocks in `AssSubtitleWriter` |

Each is a swap of one implementation, not a rewrite.

## 8. Rules for contributors

- Max **300 lines** per file. Past that, decompose.
- `Rivet.Core` may not reference Avalonia.
- ffmpeg and whisper.cpp are reached only through `IMediaProcessor` / `ITranscriber`.
- Every non-obvious decision gets an ADR in [docs/decisions/](docs/decisions/).
