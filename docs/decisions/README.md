# Decisions

One file per decision, newest last. If you change one of these, amend the ADR — the code
alone never explains *why not the other thing*.

| # | Decision | One-line why |
|---|---|---|
| [0001](0001-whisper-cpp-binding.md) | whisper.cpp via the Whisper.net binding | Whisper.net *is* whisper.cpp + the P/Invoke we would hand-write |
| [0002](0002-ffmpeg-for-media.md) | Shell out to ffmpeg/ffprobe, don't bundle | ffmpeg already does extract + burn correctly; a wrapper lib is a dependency we'd own forever |
| [0003](0003-offline-word-timing.md) | Offline whole-file; one word per whisper segment | Batch has the whole clip, and max-segment-length gives per-word times with no alignment pass |
| [0004](0004-cpu-or-gpu.md) | Vulkan GPU backend, CPU/GPU a per-load flag | GPU is worth ~60×; Vulkan covers every vendor and doesn't crash on new cards |
| [0005](0005-ass-libass-rendering.md) | Burn ASS with libass; one Dialogue per word | libass renders captions correctly; per-word events give the "word pops" look \k can't |
| [0006](0006-no-blocking-the-ui-thread.md) | Load the model inside Task.Run | With the model cached every await runs synchronously — on the UI thread — and the window freezes |
| [0007](0007-project-layout.md) | Two projects, layers as folders | The layer boundary is real; extra csproj files to enforce it are not |
| [0008](0008-style-as-percent-of-height.md) | Caption sizes are a % of video height | One saved preset must look the same on a 720p and a 4K export |
| [0009](0009-vocal-isolation.md) | Optional Demucs vocal isolation, off by default | Music/noise wrecks transcription; isolation helps but is slow and heavy, so it's opt-in |
| [0010](0010-editable-transcript-review.md) | Transcribe → edit → render, with a live single-frame preview | whisper mishears words and mistimes some; the user fixes both before the one full burn |
