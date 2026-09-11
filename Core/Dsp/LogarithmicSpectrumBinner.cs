using System;

namespace SimpleMusicVisualizer.Core.Dsp;

/// <summary>
/// Mode for aggregating FFT frequency bins within a logarithmic band.
/// </summary>
public enum FrequencyAggregationMode
{
    /// <summary>
    /// Uses the maximum peak magnitude within the band. Preserves transients and sharp harmonics.
    /// </summary>
    MaxPeak,

    /// <summary>
    /// Uses the arithmetic average magnitude across the band.
    /// </summary>
    Average,

    /// <summary>
    /// Weighted combination of peak and average (default: 75% peak, 25% average).
    /// Highly recommended for cyberpunk music visualization to balance punch and body.
    /// </summary>
    PeakEmphasis
}

/// <summary>
/// Maps linear FFT frequency bins into logarithmic psychoacoustic musical bands (e.g. 32 to 128 bands).
/// Applies equal-loudness high-frequency tilt (+3 to +4.5 dB/octave) and dynamic range normalization.
/// </summary>
public sealed class LogarithmicSpectrumBinner
{
    private int _sampleRate;
    private int _fftLength;
    private int _bandCount;
    private float _minFrequency;
    private float _maxFrequency;
    private float _tiltDbPerOctave;
    private float _sensitivity;
    private float _noiseGate;
    private float _peakEmphasisRatio;
    private FrequencyAggregationMode _aggregationMode;

    private int[] _bandStartBin = [];
    private int[] _bandEndBin = [];
    private float[] _bandTiltMultipliers = [];

    /// <summary>
    /// Gets or sets the audio sample rate in Hz (e.g. 44100, 48000).
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
                RecomputeBands();
            }
        }
    }

    /// <summary>
    /// Gets or sets the FFT window length N (e.g. 2048, 4096).
    /// </summary>
    public int FftLength
    {
        get => _fftLength;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 16);
            if (_fftLength != value)
            {
                _fftLength = value;
                RecomputeBands();
            }
        }
    }

    /// <summary>
    /// Gets or sets the number of logarithmic visualizer bands (e.g. 64, range 16 to 256).
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
                RecomputeBands();
            }
        }
    }

    /// <summary>
    /// Gets or sets the lower frequency bound in Hz (e.g. 20 Hz).
    /// </summary>
    public float MinFrequency
    {
        get => _minFrequency;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            if (MathF.Abs(_minFrequency - value) > 0.001f)
            {
                _minFrequency = value;
                RecomputeBands();
            }
        }
    }

    /// <summary>
    /// Gets or sets the upper frequency bound in Hz (e.g. 20000 Hz).
    /// </summary>
    public float MaxFrequency
    {
        get => _maxFrequency;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            if (MathF.Abs(_maxFrequency - value) > 0.001f)
            {
                _maxFrequency = value;
                RecomputeBands();
            }
        }
    }

    /// <summary>
    /// Gets or sets the high-frequency tilt boost in dB per octave (typical: +3.0 to +4.5 dB/octave).
    /// Equalizes pink noise energy distribution so treble sparkles alongside heavy bass.
    /// </summary>
    public float TiltDbPerOctave
    {
        get => _tiltDbPerOctave;
        set
        {
            if (MathF.Abs(_tiltDbPerOctave - value) > 0.001f)
            {
                _tiltDbPerOctave = value;
                RecomputeBands();
            }
        }
    }

    /// <summary>
    /// Gets or sets the overall sensitivity multiplier (e.g. 1.0f to 3.0f).
    /// </summary>
    public float Sensitivity
    {
        get => _sensitivity;
        set => _sensitivity = MathF.Max(0.01f, value);
    }

    /// <summary>
    /// Gets or sets the noise floor threshold below which bands are silenced (e.g. 0.005f to 0.05f).
    /// </summary>
    public float NoiseGate
    {
        get => _noiseGate;
        set => _noiseGate = Math.Clamp(value, 0.0f, 0.5f);
    }

    /// <summary>
    /// Gets or sets the aggregation mode (MaxPeak, Average, PeakEmphasis).
    /// </summary>
    public FrequencyAggregationMode AggregationMode
    {
        get => _aggregationMode;
        set => _aggregationMode = value;
    }

    /// <summary>
    /// Gets or sets the peak weighting ratio when using <see cref="FrequencyAggregationMode.PeakEmphasis"/> (default 0.75f).
    /// </summary>
    public float PeakEmphasisRatio
    {
        get => _peakEmphasisRatio;
        set => _peakEmphasisRatio = Math.Clamp(value, 0.0f, 1.0f);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="LogarithmicSpectrumBinner"/>.
    /// </summary>
    public LogarithmicSpectrumBinner(
        int sampleRate = 44100,
        int fftLength = 2048,
        int bandCount = 64,
        float minFrequency = 20.0f,
        float maxFrequency = 20000.0f,
        float tiltDbPerOctave = 4.0f,
        float sensitivity = 1.5f,
        float noiseGate = 0.01f,
        FrequencyAggregationMode aggregationMode = FrequencyAggregationMode.PeakEmphasis)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 8000);
        ArgumentOutOfRangeException.ThrowIfLessThan(fftLength, 16);
        ArgumentOutOfRangeException.ThrowIfLessThan(bandCount, 8);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(minFrequency, maxFrequency);

        _sampleRate = sampleRate;
        _fftLength = fftLength;
        _bandCount = bandCount;
        _minFrequency = minFrequency;
        _maxFrequency = maxFrequency;
        _tiltDbPerOctave = tiltDbPerOctave;
        _sensitivity = sensitivity;
        _noiseGate = noiseGate;
        _peakEmphasisRatio = 0.75f;
        _aggregationMode = aggregationMode;

        RecomputeBands();
    }

    private void RecomputeBands()
    {
        if (_bandStartBin.Length != _bandCount)
        {
            _bandStartBin = new int[_bandCount];
            _bandEndBin = new int[_bandCount];
            _bandTiltMultipliers = new float[_bandCount];
        }

        int maxNyquistBin = _fftLength / 2;
        float binResolution = (float)_sampleRate / _fftLength;
        float ratio = _maxFrequency / _minFrequency;
        float b = _bandCount;

        for (int i = 0; i < _bandCount; i++)
        {
            float fStart = _minFrequency * MathF.Pow(ratio, i / b);
            float fEnd = _minFrequency * MathF.Pow(ratio, (i + 1) / b);
            float fCenter = MathF.Sqrt(fStart * fEnd);

            float binStart = fStart / binResolution;
            float binEnd = fEnd / binResolution;

            // DC (bin 0) is excluded to eliminate audio interface DC bias
            int startIdx = Math.Clamp((int)MathF.Floor(binStart), 1, maxNyquistBin);
            int endIdx = Math.Clamp((int)MathF.Ceiling(binEnd), startIdx + 1, maxNyquistBin + 1);

            _bandStartBin[i] = startIdx;
            _bandEndBin[i] = endIdx;

            // High frequency tilt: calculate octaves above minFrequency
            float octaves = MathF.Log2(fCenter / _minFrequency);
            float gainDb = octaves * _tiltDbPerOctave;
            _bandTiltMultipliers[i] = MathF.Pow(10.0f, gainDb / 20.0f);
        }
    }

    /// <summary>
    /// Bins linear FFT magnitudes into logarithmic psychoacoustic musical bands.
    /// Allocates zero heap memory.
    /// </summary>
    /// <param name="linearMagnitudes">Input FFT magnitudes up to (N/2) + 1 bins.</param>
    /// <param name="outBands">Output span to receive normalized band heights [0.0, 1.0].</param>
    public void Process(ReadOnlySpan<float> linearMagnitudes, Span<float> outBands)
    {
        int bandsToCompute = Math.Min(outBands.Length, _bandCount);
        int availableBins = linearMagnitudes.Length;
        float alpha = _peakEmphasisRatio;
        float oneMinusAlpha = 1.0f - alpha;

        for (int i = 0; i < bandsToCompute; i++)
        {
            int start = _bandStartBin[i];
            int end = Math.Min(_bandEndBin[i], availableBins);

            float aggregated;

            if (start >= availableBins)
            {
                aggregated = 0.0f;
            }
            else if (end <= start + 1)
            {
                aggregated = linearMagnitudes[start];
            }
            else
            {
                switch (_aggregationMode)
                {
                    case FrequencyAggregationMode.MaxPeak:
                        {
                            float max = 0.0f;
                            for (int k = start; k < end; k++)
                            {
                                float m = linearMagnitudes[k];
                                if (m > max) max = m;
                            }
                            aggregated = max;
                            break;
                        }
                    case FrequencyAggregationMode.Average:
                        {
                            float sum = 0.0f;
                            for (int k = start; k < end; k++)
                            {
                                sum += linearMagnitudes[k];
                            }
                            aggregated = sum / (end - start);
                            break;
                        }
                    case FrequencyAggregationMode.PeakEmphasis:
                    default:
                        {
                            float max = 0.0f;
                            float sum = 0.0f;
                            for (int k = start; k < end; k++)
                            {
                                float m = linearMagnitudes[k];
                                if (m > max) max = m;
                                sum += m;
                            }
                            float avg = sum / (end - start);
                            aggregated = (alpha * max) + (oneMinusAlpha * avg);
                            break;
                        }
                }
            }

            // Apply frequency tilt & sensitivity
            float boosted = aggregated * _bandTiltMultipliers[i] * _sensitivity;

            // Apply noise gate
            if (boosted <= _noiseGate)
            {
                outBands[i] = 0.0f;
            }
            else
            {
                float normalized = (boosted - _noiseGate) / (1.0f - _noiseGate);
                outBands[i] = Math.Clamp(normalized, 0.0f, 1.0f);
            }
        }

        if (outBands.Length > bandsToCompute)
        {
            outBands[bandsToCompute..].Clear();
        }
    }
}
