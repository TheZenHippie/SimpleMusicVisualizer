using System;
using System.Numerics.Tensors;

namespace SimpleMusicVisualizer.Core.Dsp;

/// <summary>
/// 4-term minimum Blackman-Harris window function for high-dynamic-range spectral analysis.
/// Formula: w(n) = a0 - a1*cos(2*pi*n/(N-1)) + a2*cos(4*pi*n/(N-1)) - a3*cos(6*pi*n/(N-1))
/// Provides ~92 dB sidelobe suppression to eliminate spectral leakage in neon visualizers.
/// </summary>
public sealed class BlackmanHarrisWindow
{
    private const float A0 = 0.35875f;
    private const float A1 = 0.48829f;
    private const float A2 = 0.14128f;
    private const float A3 = 0.01168f;

    private readonly float[] _coefficients;

    /// <summary>
    /// Gets the length N of the precomputed window.
    /// </summary>
    public int Length => _coefficients.Length;

    /// <summary>
    /// Exposes read-only access to the precomputed window coefficients.
    /// </summary>
    public ReadOnlySpan<float> Coefficients => _coefficients;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlackmanHarrisWindow"/> class of length <paramref name="length"/>.
    /// </summary>
    /// <param name="length">The number of samples N (typically 1024, 2048, or 4096).</param>
    public BlackmanHarrisWindow(int length)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 2);

        _coefficients = new float[length];
        PrecomputeCoefficients();
    }

    private void PrecomputeCoefficients()
    {
        int n = _coefficients.Length;
        float denominator = n - 1;
        float twoPi = 2.0f * MathF.PI;
        float fourPi = 4.0f * MathF.PI;
        float sixPi = 6.0f * MathF.PI;

        for (int i = 0; i < n; i++)
        {
            float ratio = i / denominator;
            _coefficients[i] = A0
                - (A1 * MathF.Cos(twoPi * ratio))
                + (A2 * MathF.Cos(fourPi * ratio))
                - (A3 * MathF.Cos(sixPi * ratio));
        }
    }

    /// <summary>
    /// Multiplies the input audio samples by the precomputed Blackman-Harris window coefficients.
    /// </summary>
    /// <param name="input">Input audio samples of at least length N.</param>
    /// <param name="output">Output windowed samples of at least length N.</param>
    public void Apply(ReadOnlySpan<float> input, Span<float> output)
    {
        if (input.Length < _coefficients.Length)
        {
            throw new ArgumentException($"Input span length ({input.Length}) must be >= window length ({_coefficients.Length}).", nameof(input));
        }

        if (output.Length < _coefficients.Length)
        {
            throw new ArgumentException($"Output span length ({output.Length}) must be >= window length ({_coefficients.Length}).", nameof(output));
        }

        TensorPrimitives.Multiply(input[.._coefficients.Length], _coefficients, output[.._coefficients.Length]);
    }

    /// <summary>
    /// In-place multiplies the buffer by the precomputed Blackman-Harris window coefficients.
    /// </summary>
    /// <param name="buffer">Buffer containing at least length N samples.</param>
    public void Apply(Span<float> buffer)
    {
        Apply(buffer, buffer);
    }

    /// <summary>
    /// Verification helper testing symmetry across the center and coefficient bounds within [0.0, 1.0].
    /// </summary>
    /// <param name="tolerance">Maximum permissible floating point delta for symmetry.</param>
    /// <returns>A tuple indicating whether verification succeeded and an optional error message.</returns>
    public (bool IsValid, string? ErrorMessage) VerifySymmetryAndBounds(float tolerance = 1e-4f)
    {
        int n = _coefficients.Length;

        for (int i = 0; i < n; i++)
        {
            float val = _coefficients[i];
            if ((val < -tolerance) || (val > 1.0f + tolerance))
            {
                return (false, $"Coefficient at index {i} out of bounds: {val} (expected in [0, 1])");
            }

            int oppositeIndex = n - 1 - i;
            float oppVal = _coefficients[oppositeIndex];
            if (MathF.Abs(val - oppVal) > tolerance)
            {
                return (false, $"Symmetry violation at index {i} ({val}) vs index {oppositeIndex} ({oppVal})");
            }
        }

        // Verify peak is at or adjacent to center
        int mid = n / 2;
        float centerVal = _coefficients[mid];
        if (centerVal < 0.95f)
        {
            return (false, $"Center peak coefficient is lower than expected: {centerVal}");
        }

        return (true, null);
    }
}
