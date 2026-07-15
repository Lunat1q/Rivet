using Rivet.Core.Transcription;
using Whisper.net.LibraryLoader;

namespace Rivet.Core.Whisper;

/// <summary>
/// Picks which whisper.cpp native backend gets loaded, and reports which one won.
///
/// This single choice is worth more than every other speed knob in the app combined: on a
/// CPU, large-v3-turbo runs many times slower than real time; on a discrete GPU via Vulkan it
/// runs faster than real time. See ADR 0004.
/// </summary>
public static class WhisperRuntime
{
    private static readonly object Sync = new();
    private static bool _configured;

    /// <summary>
    /// Vulkan, then CPU — deliberately *not* Whisper.net's default order, which tries CUDA
    /// first. The CUDA build ships no kernels for the newest cards and takes the whole process
    /// down rather than throwing something catchable. Vulkan covers NVIDIA, AMD and Intel from
    /// one package and degrades to CPU when there is no GPU.
    ///
    /// The Vulkan library is loaded even when the user chose CPU: a native library loads once
    /// per process, and the Vulkan build contains the CPU kernels too. CPU vs GPU is therefore
    /// a per-model-load flag (WhisperFactoryOptions.UseGpu), not a library choice.
    /// </summary>
    public static void Configure()
    {
        lock (Sync)
        {
            if (_configured)
                return;

            RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu];
            _configured = true;
        }
    }

    /// <summary>Null until the first model is loaded — the library is chosen at load time.</summary>
    public static RuntimeLibrary? Loaded => RuntimeOptions.LoadedLibrary;

    /// <summary>Whether the loaded library can drive a GPU at all. Meaningless until a model loads.</summary>
    public static bool GpuAvailable =>
        Loaded is RuntimeLibrary.Vulkan or RuntimeLibrary.Cuda or RuntimeLibrary.Cuda12 or RuntimeLibrary.CoreML;

    /// <summary>Whether a model asked for this backend should be handed to the GPU.</summary>
    public static bool WantsGpu(ComputeBackend backend) => backend is ComputeBackend.Auto or ComputeBackend.Gpu;

    /// <summary>For the UI: <paramref name="useGpu" /> is what we asked for; <see cref="GpuAvailable" /> is what we got.</summary>
    public static string Describe(bool useGpu) => useGpu && GpuAvailable
        ? Loaded switch
        {
            RuntimeLibrary.Vulkan => "GPU (Vulkan)",
            RuntimeLibrary.Cuda or RuntimeLibrary.Cuda12 => "GPU (CUDA)",
            RuntimeLibrary.CoreML => "GPU (CoreML)",
            _ => "GPU"
        }
        : Loaded is null ? "starting up" : "CPU";
}
