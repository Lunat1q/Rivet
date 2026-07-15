# ADR 0006 — Load the model inside Task.Run

**Status:** accepted · **Date:** 2026-07-14

## Context

`WhisperFactory.FromPath` is a synchronous native call that reads 1.5–3 GB of weights. It is
reached through an `async` method — but "async" is not "off this thread".

## Decision

Wrap the native model load (and processor construction) in `Task.Run`
(`WhisperTranscriberFactory`). The first time, the model download genuinely awaits I/O and
yields. But on every run *after* the model is cached, every `await` on the way down completes
synchronously — so without `Task.Run` the multi-second native load runs on whatever thread
pressed **Generate**, which is the UI thread, and the window freezes.

This is the same trap and the same fix as Steno's ADR 0021; it is worth its own ADR here
because the failure only appears on the *second* run, which is easy to miss in testing.

## Consequences

- The UI stays responsive through model load; progress and the Cancel button keep working.
- The `IProgress<double>` callbacks handed to the pipeline are created on the UI thread, so
  `Progress<T>` marshals them back automatically — no manual dispatcher calls in the view model.
