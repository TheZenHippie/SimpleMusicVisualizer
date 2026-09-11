using System;
using System.Numerics;

namespace SimpleMusicVisualizer.Core.Dsp;

/// <summary>
/// High-performance in-place Radix-2 Cooley-Tukey Fast Fourier Transform (FFT)
/// with precomputed bit-reversal tables and twiddle factor lookup tables.
/// Provides zero-allocation forward frequency analysis for audio visualization.
/// </summary>
public sealed class FastFourierTransform
{
    private readonly int _length;
    private readonly int _log2N;
    private readonly int[] _bitReversal;
    private readonly float[] _cosTable;
    private readonly float[] _sinTable;

    /// <summary>
    /// Gets the FFT size N (must be a power of 2, e.g. 1024, 2048, 4096).
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Initializes a new instance of the <see cref="FastFourierTransform"/> class for size <paramref name="length"/>.
    /// </summary>
    /// <param name="length">The FFT length N (must be a power of 2, N >= 2).</param>
    public FastFourierTransform(int length)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 2);

        if (!BitOperations.IsPow2(length))
        {
            throw new ArgumentException($"FFT length must be a power of 2. Received: {length}.", nameof(length));
        }

        _length = length;
        _log2N = BitOperations.Log2((uint)length);

        _bitReversal = new int[length];
        PrecomputeBitReversal();

        // Total twiddle factors across all stages: 1 + 2 + 4 + ... + N/2 = N - 1
        int twiddleCount = length - 1;
        _cosTable = new float[twiddleCount];
        _sinTable = new float[twiddleCount];
        PrecomputeTwiddleFactors();
    }

    private void PrecomputeBitReversal()
    {
        int shift = 32 - _log2N;
        for (int i = 0; i < _length; i++)
        {
            _bitReversal[i] = (int)(ReverseBits((uint)i) >> shift);
        }
    }

    private static uint ReverseBits(uint x)
    {
        x = ((x >> 1) & 0x55555555) | ((x & 0x55555555) << 1);
        x = ((x >> 2) & 0x33333333) | ((x & 0x33333333) << 2);
        x = ((x >> 4) & 0x0F0F0F0F) | ((x & 0x0F0F0F0F) << 4);
        x = ((x >> 8) & 0x00FF00FF) | ((x & 0x00FF00FF) << 8);
        return (x >> 16) | (x << 16);
    }

    private void PrecomputeTwiddleFactors()
    {
        for (int halfLength = 1; halfLength < _length; halfLength <<= 1)
        {
            int length = halfLength << 1;
            int twiddleBase = halfLength - 1;
            float angleStep = -2.0f * MathF.PI / length;

            for (int k = 0; k < halfLength; k++)
            {
                float angle = k * angleStep;
                _cosTable[twiddleBase + k] = MathF.Cos(angle);
                _sinTable[twiddleBase + k] = MathF.Sin(angle);
            }
        }
    }

    /// <summary>
    /// Computes the in-place Radix-2 Cooley-Tukey forward FFT on real and imaginary buffers.
    /// Both spans must have length >= <see cref="Length"/>.
    /// </summary>
    /// <param name="real">Span containing real components (modified in place).</param>
    /// <param name="imag">Span containing imaginary components (modified in place).</param>
    public void Compute(Span<float> real, Span<float> imag)
    {
        if (real.Length < _length)
        {
            throw new ArgumentException($"Real span length ({real.Length}) must be >= FFT length ({_length}).", nameof(real));
        }

        if (imag.Length < _length)
        {
            throw new ArgumentException($"Imaginary span length ({imag.Length}) must be >= FFT length ({_length}).", nameof(imag));
        }

        // 1. Bit-reversal permutation
        for (int i = 0; i < _length; i++)
        {
            int j = _bitReversal[i];
            if (j > i)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imag[i], imag[j]) = (imag[j], imag[i]);
            }
        }

        // 2. Cooley-Tukey butterfly stages
        for (int halfLength = 1; halfLength < _length; halfLength <<= 1)
        {
            int length = halfLength << 1;
            int twiddleBase = halfLength - 1;

            for (int i = 0; i < _length; i += length)
            {
                for (int k = 0; k < halfLength; k++)
                {
                    int twiddleIndex = twiddleBase + k;
                    float c = _cosTable[twiddleIndex];
                    float s = _sinTable[twiddleIndex];

                    int u = i + k;
                    int v = u + halfLength;

                    float vr = real[v];
                    float vi = imag[v];

                    float tr = (vr * c) - (vi * s);
                    float ti = (vr * s) + (vi * c);

                    float ur = real[u];
                    float ui = imag[u];

                    real[u] = ur + tr;
                    imag[u] = ui + ti;
                    real[v] = ur - tr;
                    imag[v] = ui - ti;
                }
            }
        }
    }

    /// <summary>
    /// Computes normalized frequency magnitude: sqrt(re^2 + im^2) / N for each bin up to N/2.
    /// </summary>
    /// <param name="real">Real frequency components of length >= N.</param>
    /// <param name="imag">Imaginary frequency components of length >= N.</param>
    /// <param name="magnitudes">Output magnitudes span to receive values up to (N/2) + 1.</param>
    public void ComputeMagnitudes(ReadOnlySpan<float> real, ReadOnlySpan<float> imag, Span<float> magnitudes)
    {
        if (real.Length < _length)
        {
            throw new ArgumentException($"Real span length ({real.Length}) must be >= FFT length ({_length}).", nameof(real));
        }

        if (imag.Length < _length)
        {
            throw new ArgumentException($"Imaginary span length ({imag.Length}) must be >= FFT length ({_length}).", nameof(imag));
        }

        int maxBins = (_length / 2) + 1;
        int count = Math.Min(magnitudes.Length, maxBins);
        float invN = 1.0f / _length;

        for (int i = 0; i < count; i++)
        {
            float r = real[i];
            float im = imag[i];
            magnitudes[i] = MathF.Sqrt((r * r) + (im * im)) * invN;
        }

        if (magnitudes.Length > count)
        {
            magnitudes[count..].Clear();
        }
    }
}
