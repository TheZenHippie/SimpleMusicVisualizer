using System;

namespace SimpleMusicVisualizer.Core.Dsp;

/// <summary>
/// Temporal ballistics engine for music visualizer bars.
/// Implements dual-rate attack/decay smoothing and gravity-accelerated peak falloff markers.
/// </summary>
public sealed class SpectrumBallistics
{
    private int _bandCount;
    private float _attack;
    private float _decay;
    private float _gravity;
    private int _peakHoldFrames;

    private float[] _smoothed = [];
    private float[] _peaks = [];
    private int[] _peakHoldTimers = [];
    private float[] _peakVelocities = [];

    /// <summary>
    /// Gets or sets the number of frequency bands to smooth.
    /// </summary>
    public int BandCount
    {
        get => _bandCount;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            if (_bandCount != value)
            {
                _bandCount = value;
                ResizeArrays();
            }
        }
    }

    /// <summary>
    /// Gets or sets the attack smoothing factor (typical 0.80f to 0.95f for punchy transients).
    /// </summary>
    public float Attack
    {
        get => _attack;
        set => _attack = Math.Clamp(value, 0.01f, 1.0f);
    }

    /// <summary>
    /// Gets or sets the decay smoothing factor (typical 0.15f to 0.25f for smooth trailing falloff).
    /// </summary>
    public float Decay
    {
        get => _decay;
        set => _decay = Math.Clamp(value, 0.01f, 1.0f);
    }

    /// <summary>
    /// Gets or sets downward gravity acceleration for peak markers (typical 0.003f to 0.015f).
    /// </summary>
    public float Gravity
    {
        get => _gravity;
        set => _gravity = MathF.Max(0.00001f, value);
    }

    /// <summary>
    /// Gets or sets how many frames peak markers hold their position before gravity falloff begins (default 6).
    /// </summary>
    public int PeakHoldFrames
    {
        get => _peakHoldFrames;
        set => _peakHoldFrames = Math.Max(0, value);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="SpectrumBallistics"/>.
    /// </summary>
    public SpectrumBallistics(
        int bandCount = 64,
        float attack = 0.85f,
        float decay = 0.20f,
        float gravity = 0.006f,
        int peakHoldFrames = 6)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bandCount, 1);

        _bandCount = bandCount;
        _attack = Math.Clamp(attack, 0.01f, 1.0f);
        _decay = Math.Clamp(decay, 0.01f, 1.0f);
        _gravity = MathF.Max(0.00001f, gravity);
        _peakHoldFrames = Math.Max(0, peakHoldFrames);

        ResizeArrays();
    }

    private void ResizeArrays()
    {
        Array.Resize(ref _smoothed, _bandCount);
        Array.Resize(ref _peaks, _bandCount);
        Array.Resize(ref _peakHoldTimers, _bandCount);
        Array.Resize(ref _peakVelocities, _bandCount);
    }

    /// <summary>
    /// Resets all internal ballistics states, zeroing out bars and peak markers.
    /// </summary>
    public void Reset()
    {
        Array.Clear(_smoothed);
        Array.Clear(_peaks);
        Array.Clear(_peakHoldTimers);
        Array.Clear(_peakVelocities);
    }

    /// <summary>
    /// Smooths new spectrum magnitudes and updates gravity-accelerated peak markers.
    /// Allocates zero heap memory during frame execution.
    /// </summary>
    /// <param name="newValues">Input raw spectrum band magnitudes in [0.0, 1.0].</param>
    /// <param name="outSmoothed">Output buffer for smoothed bar heights.</param>
    /// <param name="outPeaks">Output buffer for peak marker heights.</param>
    public void Process(ReadOnlySpan<float> newValues, Span<float> outSmoothed, Span<float> outPeaks)
    {
        int count = Math.Min(_bandCount, Math.Min(newValues.Length, Math.Min(outSmoothed.Length, outPeaks.Length)));

        for (int i = 0; i < count; i++)
        {
            float target = newValues[i];
            float current = _smoothed[i];

            // 1. Dual-rate attack / decay smoothing
            if (target > current)
            {
                current += (target - current) * _attack;
            }
            else
            {
                current += (target - current) * _decay;
            }

            current = Math.Clamp(current, 0.0f, 1.0f);
            _smoothed[i] = current;

            // 2. Gravity-driven peak markers
            float peak = _peaks[i];

            if (current >= peak)
            {
                peak = current;
                _peakHoldTimers[i] = _peakHoldFrames;
                _peakVelocities[i] = 0.0f;
            }
            else
            {
                if (_peakHoldTimers[i] > 0)
                {
                    _peakHoldTimers[i]--;
                }
                else
                {
                    _peakVelocities[i] += _gravity;
                    peak -= _peakVelocities[i];

                    if (peak < current)
                    {
                        peak = current;
                        _peakVelocities[i] = 0.0f;
                    }
                }
            }

            peak = Math.Clamp(peak, 0.0f, 1.0f);
            _peaks[i] = peak;

            outSmoothed[i] = current;
            outPeaks[i] = peak;
        }

        if (outSmoothed.Length > count)
        {
            outSmoothed[count..].Clear();
        }

        if (outPeaks.Length > count)
        {
            outPeaks[count..].Clear();
        }
    }
}
