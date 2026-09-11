namespace SimpleMusicVisualizer.Core.Audio;

/// <summary>
/// Service contract for capturing system audio samples via WASAPI loopback or generating synthetic audio streams.
/// </summary>
public interface IAudioCaptureService
{
    /// <summary>
    /// Starts capturing audio or begins the simulation generator loop.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops audio capture and simulation generators.
    /// </summary>
    void Stop();

    /// <summary>
    /// Gets whether audio capture or simulation is currently active.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets or sets whether synthetic cyberpunk simulation audio is enabled.
    /// When true, synthetic audio is fed into the sample buffer.
    /// </summary>
    bool IsSimulationMode { get; set; }

    /// <summary>
    /// Gets the current audio sample rate in Hz (e.g., 44100 or 48000).
    /// </summary>
    int SampleRate { get; }

    /// <summary>
    /// Reads available mono audio samples into the destination buffer.
    /// </summary>
    /// <param name="destination">The destination span to receive mono float samples.</param>
    /// <returns>The number of samples actually read.</returns>
    int ReadSamples(Span<float> destination);

    /// <summary>
    /// Event raised when new audio samples are captured and written to the internal buffer.
    /// </summary>
    event Action? SamplesAvailable;

    /// <summary>
    /// Enumerates active playback (render) audio endpoint device friendly names.
    /// </summary>
    /// <returns>A list of friendly device names.</returns>
    IReadOnlyList<string> GetDevices();

    /// <summary>
    /// Selects an audio output device by its index in the list returned by <see cref="GetDevices"/>.
    /// </summary>
    /// <param name="index">The zero-based index of the device to capture.</param>
    void SelectDevice(int index);
}
