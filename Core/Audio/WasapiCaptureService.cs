using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

#pragma warning disable CS0618 // WasapiLoopbackCapture is specified by project requirements

namespace SimpleMusicVisualizer.Core.Audio;

/// <summary>
/// Audio capture service implementation capturing loopback audio via Windows WASAPI
/// with seamless failover and toggle to a synthetic cyberpunk audio simulator.
/// </summary>
public sealed class WasapiCaptureService : IAudioCaptureService, IDisposable
{
    private readonly AudioRingBuffer _ringBuffer;
    private readonly CyberpunkAudioSimulator _simulator;
    private readonly MMDeviceEnumerator _deviceEnumerator;

    private WasapiLoopbackCapture? _capture;
    private MMDevice? _selectedDevice;

    private CancellationTokenSource? _simCts;
    private Task? _simTask;

    private float[] _scratchBuffer = new float[4096];
    private int _sampleRate = 48000;
    private bool _isRunning;
    private bool _isSimulationMode;
    private bool _disposed;

    /// <summary>
    /// Event raised when new audio samples are captured and ready to be processed.
    /// </summary>
    public event Action? SamplesAvailable;

    /// <summary>
    /// Initializes a new instance of the <see cref="WasapiCaptureService"/> class.
    /// </summary>
    /// <param name="ringBuffer">Optional custom ring buffer. If null, a default 65536-sample buffer is used.</param>
    /// <param name="simulator">Optional custom cyberpunk audio simulator.</param>
    public WasapiCaptureService(AudioRingBuffer? ringBuffer = null, CyberpunkAudioSimulator? simulator = null)
    {
        _ringBuffer = ringBuffer ?? new AudioRingBuffer();
        _simulator = simulator ?? new CyberpunkAudioSimulator();
        _sampleRate = _simulator.SampleRate;
        _deviceEnumerator = new MMDeviceEnumerator();
    }

    /// <summary>
    /// Gets the internal audio ring buffer.
    /// </summary>
    public AudioRingBuffer RingBuffer => _ringBuffer;

    /// <summary>
    /// Gets whether audio capture or simulation is currently active.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Gets or sets whether synthetic cyberpunk simulation audio is enabled.
    /// When toggled during active playback, transitions dynamically between WASAPI and simulation.
    /// </summary>
    public bool IsSimulationMode
    {
        get => _isSimulationMode;
        set
        {
            if (_isSimulationMode == value)
            {
                return;
            }

            _isSimulationMode = value;

            if (_isRunning)
            {
                if (_isSimulationMode)
                {
                    StopWasapi();
                    StartSimulation();
                }
                else
                {
                    StopSimulation();
                    StartWasapi();
                }
            }
        }
    }

    /// <summary>
    /// Gets the current audio sample rate in Hz.
    /// </summary>
    public int SampleRate => _isSimulationMode ? _simulator.SampleRate : _sampleRate;

    /// <summary>
    /// Starts capturing audio or begins the simulation generator loop.
    /// </summary>
    public void Start()
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        _ringBuffer.Clear();

        if (_isSimulationMode)
        {
            StartSimulation();
        }
        else
        {
            StartWasapi();
        }
    }

    /// <summary>
    /// Stops audio capture and simulation generators.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;
        StopSimulation();
        StopWasapi();
        _ringBuffer.Clear();
    }

    /// <summary>
    /// Reads available mono audio samples into the destination buffer.
    /// </summary>
    /// <param name="destination">The destination span to receive mono float samples.</param>
    /// <returns>The number of samples actually read.</returns>
    public int ReadSamples(Span<float> destination) => _ringBuffer.Read(destination);

    /// <summary>
    /// Enumerates active playback (render) audio endpoint device friendly names.
    /// </summary>
    /// <returns>A list of friendly device names.</returns>
    public IReadOnlyList<string> GetDevices()
    {
        try
        {
            var devices = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            string[] names = new string[devices.Count];
            for (int i = 0; i < devices.Count; i++)
            {
                names[i] = devices[i].FriendlyName;
            }
            return names;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Selects an audio output device by its index in the list returned by <see cref="GetDevices"/>.
    /// </summary>
    /// <param name="index">The zero-based index of the device to capture.</param>
    public void SelectDevice(int index)
    {
        var devices = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        if (index < 0 || index >= devices.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Device index {index} is out of bounds (0..{devices.Count - 1}).");
        }

        _selectedDevice = devices[index];

        if (_isRunning && !_isSimulationMode)
        {
            StopWasapi();
            StartWasapi();
        }
    }

    private void StartWasapi()
    {
        try
        {
            StopWasapi();

            _capture = _selectedDevice != null
                ? new WasapiLoopbackCapture(_selectedDevice)
                : new WasapiLoopbackCapture();

            _sampleRate = _capture.WaveFormat.SampleRate;
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;
            _capture.StartRecording();
        }
        catch
        {
            // Gracefully fallback to simulation mode if hardware capture is unavailable
            StopWasapi();
            StartSimulation();
        }
    }

    private void StopWasapi()
    {
        try
        {
            if (_capture != null)
            {
                _capture.DataAvailable -= OnDataAvailable;
                _capture.RecordingStopped -= OnRecordingStopped;
                _capture.StopRecording();
                _capture.Dispose();
                _capture = null;
            }
        }
        catch
        {
            // Ignore disposal errors on teardown
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0 || _isSimulationMode || !_isRunning)
        {
            return;
        }

        try
        {
            ProcessIncomingAudio(e.Buffer.AsSpan(0, e.BytesRecorded));
        }
        catch
        {
            // Suppress conversion glitches to prevent audio stream crash
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null && _isRunning && !_isSimulationMode)
        {
            // Automatic failover to simulation mode on unexpected device disconnect
            StartSimulation();
        }
    }

    private void ProcessIncomingAudio(ReadOnlySpan<byte> byteBuffer)
    {
        var waveFormat = _capture?.WaveFormat;
        if (waveFormat == null)
        {
            return;
        }

        int channels = waveFormat.Channels;
        if (channels <= 0)
        {
            return;
        }

        int bitsPerSample = waveFormat.BitsPerSample;

        if (bitsPerSample == 32)
        {
            // 32-bit IEEE float
            int bytesPerFrame = channels * sizeof(float);
            int frameCount = byteBuffer.Length / bytesPerFrame;
            if (frameCount <= 0) return;

            EnsureScratchCapacity(frameCount);

            var floatSpan = MemoryMarshal.Cast<byte, float>(byteBuffer);
            float channelScale = 1.0f / channels;

            for (int i = 0; i < frameCount; i++)
            {
                int baseIndex = i * channels;
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                {
                    sum += floatSpan[baseIndex + c];
                }
                _scratchBuffer[i] = sum * channelScale;
            }

            _ringBuffer.Write(_scratchBuffer.AsSpan(0, frameCount));
            SamplesAvailable?.Invoke();
        }
        else if (bitsPerSample == 16)
        {
            // 16-bit signed PCM
            int bytesPerFrame = channels * sizeof(short);
            int frameCount = byteBuffer.Length / bytesPerFrame;
            if (frameCount <= 0) return;

            EnsureScratchCapacity(frameCount);

            var shortSpan = MemoryMarshal.Cast<byte, short>(byteBuffer);
            float channelScale = 1.0f / (channels * 32768.0f);

            for (int i = 0; i < frameCount; i++)
            {
                int baseIndex = i * channels;
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                {
                    sum += shortSpan[baseIndex + c];
                }
                _scratchBuffer[i] = sum * channelScale;
            }

            _ringBuffer.Write(_scratchBuffer.AsSpan(0, frameCount));
            SamplesAvailable?.Invoke();
        }
        else if (bitsPerSample == 24)
        {
            // 24-bit signed PCM (3 bytes per channel)
            int bytesPerFrame = channels * 3;
            int frameCount = byteBuffer.Length / bytesPerFrame;
            if (frameCount <= 0) return;

            EnsureScratchCapacity(frameCount);

            float channelScale = 1.0f / (channels * 8388608.0f);

            for (int i = 0; i < frameCount; i++)
            {
                int frameOffset = i * bytesPerFrame;
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                {
                    int offset = frameOffset + (c * 3);
                    int sample24 = byteBuffer[offset]
                                 | (byteBuffer[offset + 1] << 8)
                                 | ((sbyte)byteBuffer[offset + 2] << 16);
                    sum += sample24;
                }
                _scratchBuffer[i] = sum * channelScale;
            }

            _ringBuffer.Write(_scratchBuffer.AsSpan(0, frameCount));
            SamplesAvailable?.Invoke();
        }
    }

    private void EnsureScratchCapacity(int required)
    {
        if (_scratchBuffer.Length < required)
        {
            int newSize = Math.Max(required, _scratchBuffer.Length * 2);
            Array.Resize(ref _scratchBuffer, newSize);
        }
    }

    private void StartSimulation()
    {
        StopSimulation();
        _sampleRate = _simulator.SampleRate;
        _simulator.Reset();

        _simCts = new CancellationTokenSource();
        _simTask = Task.Run(() => RunSimulationLoopAsync(_simCts.Token));
    }

    private void StopSimulation()
    {
        if (_simCts != null)
        {
            try
            {
                _simCts.Cancel();
                _simCts.Dispose();
            }
            catch
            {
                // Suppress cancellation exceptions
            }
            finally
            {
                _simCts = null;
                _simTask = null;
            }
        }
    }

    private async Task RunSimulationLoopAsync(CancellationToken cancellationToken)
    {
        // Deliver synthetic audio in 10ms slices (480 samples @ 48 kHz)
        const int chunkSize = 480;
        float[] simBuffer = new float[chunkSize];
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!_isRunning || !_isSimulationMode)
                {
                    break;
                }

                _simulator.Generate(simBuffer);
                _ringBuffer.Write(simBuffer);
                SamplesAvailable?.Invoke();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during cancellation
        }
    }

    /// <summary>
    /// Disposes resources held by the service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _deviceEnumerator.Dispose();
        _capture?.Dispose();
        _capture = null;
        GC.SuppressFinalize(this);
    }
}
