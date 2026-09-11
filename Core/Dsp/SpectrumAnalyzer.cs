using System;

namespace SimpleMusicVisualizer.Core.Dsp;

/// <summary>
/// High-level spectrum analysis pipeline for the Cyberpunk Music Visualizer.
/// Orchestrates Blackman-Harris windowing, radix-2 Cooley-Tukey FFT, logarithmic psychoacoustic binning,
/// high-frequency tilt equalization, and smooth ballistics with gravity-driven peak markers.
/// Maintains pre-allocated working buffers to guarantee zero GC heap allocations per frame.
/// </summary>
public sealed class SpectrumAnalyzer
{
    private int _sampleRate;
    private int _fftSize;
    private int _bandCount;
    private float _minFrequency;
    private float _maxFrequency;
    private float _tiltDbPerOctave;
    private float _sensitivity;
    private float _noiseGate;
    private float _attack;
    private float _decay;
    private float _gravity;
    private int _peakHoldFrames;
    private FrequencyAggregationMode _aggregationMode;

    private BlackmanHarrisWindow _window = null!;
    private FastFourierTransform _fft = null!;
    private LogarithmicSpectrumBinner _binner = null!;
    private SpectrumBallistics _ballistics = null!;

    // Pre-allocated working buffers for zero GC allocations during Process()
    private float[] _real = [];
    private float[] _imag = [];
    private float[] _magnitudes = [];
    private float[] _rawBands = [];
    private float[] _smoothedBands = [];
    private float[] _peakBands = [];

    #region Properties

    /// <summary>
    /// Audio sample rate in Hz (e.g. 44100, 48000).
    /// </summary>
    public int SampleRate
    {
        get => _sampleRate;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 8000);
            if (_sampleRate != value)
            {
                _sampleRate = value;
                _binner.SampleRate = value;
            }
        }
    }

    /// <summary>
    /// FFT length N (must be a power of 2, e.g. 1024, 2048, 4096).
    /// </summary>
    public int FftSize
    {
        get => _fftSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 16);
            if (_fftSize != value)
            {
                _fftSize = value;
                Reinitialize();
            }
        }
    }

    /// <summary>
    /// Number of output visualizer bands (e.g. 64, range 16 to 256).
    /// </summary>
    public int BandCount
    {
        get => _bandCount;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 8);
            if (_bandCount != value)
            {
                _bandCount = value;
                Reinitialize();
            }
        }
    }

    /// <summary>
    /// Minimum frequency in Hz (e.g. 20 Hz).
    /// </summary>
    public float MinFrequency
    {
        get => _minFrequency;
        set
        {
            _minFrequency = value;
            _binner.MinFrequency = value;
        }
    }

    /// <summary>
    /// Maximum frequency in Hz (e.g. 20000 Hz).
    /// </summary>
    public float MaxFrequency
    {
        get => _maxFrequency;
        set
        {
            _maxFrequency = value;
            _binner.MaxFrequency = value;
        }
    }

    /// <summary>
    /// Equal-loudness high frequency tilt in dB/octave (typical: +3.0 to +4.5 dB/octave).
    /// </summary>
    public float TiltDbPerOctave
    {
        get => _tiltDbPerOctave;
        set
        {
            _tiltDbPerOctave = value;
            _binner.TiltDbPerOctave = value;
        }
    }

    /// <summary>
    /// Amplitude sensitivity multiplier (default 1.5f).
    /// </summary>
    public float Sensitivity
    {
        get => _sensitivity;
        set
        {
            _sensitivity = value;
            _binner.Sensitivity = value;
        }
    }

    /// <summary>
    /// Noise gate floor threshold (default 0.01f).
    /// </summary>
    public float NoiseGate
    {
        get => _noiseGate;
        set
        {
            _noiseGate = value;
            _binner.NoiseGate = value;
        }
    }

    /// <summary>
    /// Attack smoothing rate (0.80 to 0.95 for instant punch).
    /// </summary>
    public float Attack
    {
        get => _attack;
        set
        {
            _attack = value;
            _ballistics.Attack = value;
        }
    }

    /// <summary>
    /// Decay smoothing rate (0.15 to 0.25 for smooth trailing falloff).
    /// </summary>
    public float Decay
    {
        get => _decay;
        set
        {
            _decay = value;
            _ballistics.Decay = value;
        }
    }

    /// <summary>
    /// Gravity falloff acceleration for peak markers.
    /// </summary>
    public float Gravity
    {
        get => _gravity;
        set
        {
            _gravity = value;
            _ballistics.Gravity = value;
        }
    }

    /// <summary>
    /// Peak hold duration in frames before gravity falloff begins (default 6).
    /// </summary>
    public int PeakHoldFrames
    {
        get => _peakHoldFrames;
        set
        {
            _peakHoldFrames = value;
            _ballistics.PeakHoldFrames = value;
        }
    }

    /// <summary>
    /// Aggregation mode across FFT bins per band.
    /// </summary>
    public FrequencyAggregationMode AggregationMode
    {
        get => _aggregationMode;
        set
        {
            _aggregationMode = value;
            _binner.AggregationMode = value;
        }
    }

    /// <summary>
    /// Direct read-only span of current smoothed bands.
    /// </summary>
    public ReadOnlySpan<float> SmoothedBands => _smoothedBands;

    /// <summary>
    /// Direct read-only span of current peak marker positions.
    /// </summary>
    public ReadOnlySpan<float> PeakBands => _peakBands;

    #endregion

    /// <summary>
    /// Initializes a new instance of <see cref="SpectrumAnalyzer"/> with default or custom DSP parameters.
    /// </summary>
    public SpectrumAnalyzer(
        int sampleRate = 44100,
        int fftSize = 2048,
        int bandCount = 64,
        float minFrequency = 20.0f,
        float maxFrequency = 20000.0f,
        float tiltDbPerOctave = 4.0f,
        float sensitivity = 1.5f,
        float noiseGate = 0.01f,
        float attack = 0.85f,
        float decay = 0.20f,
        float gravity = 0.006f,
        int peakHoldFrames = 6,
        FrequencyAggregationMode aggregationMode = FrequencyAggregationMode.PeakEmphasis)
    {
        _sampleRate = sampleRate;
        _fftSize = fftSize;
        _bandCount = bandCount;
        _minFrequency = minFrequency;
        _maxFrequency = maxFrequency;
        _tiltDbPerOctave = tiltDbPerOctave;
        _sensitivity = sensitivity;
        _noiseGate = noiseGate;
        _attack = attack;
        _decay = decay;
        _gravity = gravity;
        _peakHoldFrames = peakHoldFrames;
        _aggregationMode = aggregationMode;

        Reinitialize();
    }

    private void Reinitialize()
    {
        _window = new BlackmanHarrisWindow(_fftSize);
        _fft = new FastFourierTransform(_fftSize);
        _binner = new LogarithmicSpectrumBinner(
            _sampleRate,
            _fftSize,
            _bandCount,
            _minFrequency,
            _maxFrequency,
            _tiltDbPerOctave,
            _sensitivity,
            _noiseGate,
            _aggregationMode);

        _ballistics = new SpectrumBallistics(
            _bandCount,
            _attack,
            _decay,
            _gravity,
            _peakHoldFrames);

        _real = new float[_fftSize];
        _imag = new float[_fftSize];
        _magnitudes = new float[(_fftSize / 2) + 1];
        _rawBands = new float[_bandCount];
        _smoothedBands = new float[_bandCount];
        _peakBands = new float[_bandCount];
    }

    /// <summary>
    /// Resets all internal ballistics and clears working buffers.
    /// </summary>
    public void Reset()
    {
        _ballistics.Reset();
        Array.Clear(_real);
        Array.Clear(_imag);
        Array.Clear(_magnitudes);
        Array.Clear(_rawBands);
        Array.Clear(_smoothedBands);
        Array.Clear(_peakBands);
    }

    /// <summary>
    /// Processes a frame of audio samples and produces smoothed visualizer bands and peak markers.
    /// Operates with ZERO heap allocations.
    /// </summary>
    /// <param name="samples">Input audio PCM samples (mono or downmixed float).</param>
    /// <param name="outBands">Output span to receive smoothed bar amplitudes in [0.0, 1.0].</param>
    /// <param name="outPeaks">Output span to receive peak marker positions in [0.0, 1.0].</param>
    public void Process(ReadOnlySpan<float> samples, Span<float> outBands, Span<float> outPeaks)
    {
        // 1. Copy samples into _real buffer with zero padding if samples.Length < _fftSize
        int copyCount = Math.Min(samples.Length, _fftSize);
        samples[..copyCount].CopyTo(_real.AsSpan(0, copyCount));

        if (copyCount < _fftSize)
        {
            _real.AsSpan(copyCount).Clear();
        }

        // 2. Clear imaginary components
        _imag.AsSpan().Clear();

        // 3. Apply Blackman-Harris window in place to eliminate spectral leakage
        _window.Apply(_real);

        // 4. Compute in-place Radix-2 Cooley-Tukey FFT
        _fft.Compute(_real, _imag);

        // 5. Calculate normalized magnitudes
        _fft.ComputeMagnitudes(_real, _imag, _magnitudes);

        // 6. Map to logarithmic psychoacoustic bands with high-frequency tilt & noise gate
        _binner.Process(_magnitudes, _rawBands);

        // 7. Smooth temporal ballistics and update gravity-accelerated peak markers
        _ballistics.Process(_rawBands, _smoothedBands, _peakBands);

        // 8. Output to caller spans
        int outputCount = Math.Min(_bandCount, Math.Min(outBands.Length, outPeaks.Length));
        _smoothedBands.AsSpan(0, outputCount).CopyTo(outBands[..outputCount]);
        _peakBands.AsSpan(0, outputCount).CopyTo(outPeaks[..outputCount]);
    }
}
