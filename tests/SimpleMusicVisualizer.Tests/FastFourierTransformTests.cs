using System;
using SimpleMusicVisualizer.Core.Dsp;
using Xunit;

namespace SimpleMusicVisualizer.Tests;

public class FastFourierTransformTests
{
    [Fact]
    public void Fft_ThrowsOnNonPowerOfTwo()
    {
        Assert.Throws<ArgumentException>(() => new FastFourierTransform(1000));
    }

    [Fact]
    public void Fft_ImpulseResponse_IsFlatAcrossAllFrequencies()
    {
        int n = 1024;
        var fft = new FastFourierTransform(n);

        float[] real = new float[n];
        float[] imag = new float[n];
        real[0] = 1.0f; // Unit impulse delta[0]

        fft.Compute(real, imag);

        // DFT of delta[0] is X[k] = 1.0 for all k
        for (int k = 0; k < n; k++)
        {
            Assert.Equal(1.0f, real[k], precision: 4);
            Assert.Equal(0.0f, imag[k], precision: 4);
        }
    }

    [Fact]
    public void Fft_DcSignal_PutsAllEnergyInBinZero()
    {
        int n = 512;
        var fft = new FastFourierTransform(n);

        float[] real = new float[n];
        float[] imag = new float[n];
        Array.Fill(real, 1.0f); // Constant DC signal of 1.0

        fft.Compute(real, imag);

        float[] magnitudes = new float[(n / 2) + 1];
        fft.ComputeMagnitudes(real, imag, magnitudes);

        // DC bin magnitude should be 1.0 (normalized by 1/N)
        Assert.Equal(1.0f, magnitudes[0], precision: 4);

        // All AC bins should be ~0
        for (int k = 1; k < magnitudes.Length; k++)
        {
            Assert.True(magnitudes[k] < 0.0001f, $"Bin {k} was {magnitudes[k]}, expected ~0");
        }
    }

    [Fact]
    public void Fft_PureSinusoid_PeaksAtExpectedBin()
    {
        int n = 2048;
        int targetBin = 32;
        var fft = new FastFourierTransform(n);

        float[] real = new float[n];
        float[] imag = new float[n];

        // Generate cos(2 * pi * targetBin * t / n)
        for (int i = 0; i < n; i++)
        {
            real[i] = MathF.Cos(2.0f * MathF.PI * targetBin * i / n);
        }

        fft.Compute(real, imag);

        float[] magnitudes = new float[(n / 2) + 1];
        fft.ComputeMagnitudes(real, imag, magnitudes);

        // Find bin with highest magnitude
        int peakBin = 0;
        float peakVal = 0.0f;
        for (int i = 0; i < magnitudes.Length; i++)
        {
            if (magnitudes[i] > peakVal)
            {
                peakVal = magnitudes[i];
                peakBin = i;
            }
        }

        Assert.Equal(targetBin, peakBin);
        Assert.True(peakVal > 0.45f); // For cos wave, energy is split equally between k and N-k (0.5 each)
    }
}
