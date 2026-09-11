using System;
using SimpleMusicVisualizer.Core.Dsp;
using Xunit;

namespace SimpleMusicVisualizer.Tests;

public class LogarithmicSpectrumBinnerTests
{
    [Fact]
    public void Binner_ProducesValidRangeOutputs()
    {
        var binner = new LogarithmicSpectrumBinner(
            sampleRate: 44100,
            fftLength: 2048,
            bandCount: 64,
            minFrequency: 20f,
            maxFrequency: 20000f,
            tiltDbPerOctave: 4.0f,
            sensitivity: 1.5f,
            noiseGate: 0.01f);

        float[] magnitudes = new float[1025];
        Array.Fill(magnitudes, 0.05f);

        float[] outBands = new float[64];
        binner.Process(magnitudes, outBands);

        for (int i = 0; i < outBands.Length; i++)
        {
            Assert.True(outBands[i] is >= 0.0f and <= 1.0f, $"Band {i} had value {outBands[i]}");
        }
    }

    [Fact]
    public void Binner_NoiseGate_SilencesQuietBins()
    {
        var binner = new LogarithmicSpectrumBinner(
            sampleRate: 44100,
            fftLength: 2048,
            bandCount: 64,
            minFrequency: 20f,
            maxFrequency: 20000f,
            tiltDbPerOctave: 0.0f,
            sensitivity: 1.0f,
            noiseGate: 0.05f);

        float[] magnitudes = new float[1025];
        Array.Fill(magnitudes, 0.02f); // Below 0.05f noise gate

        float[] outBands = new float[64];
        binner.Process(magnitudes, outBands);

        for (int i = 0; i < outBands.Length; i++)
        {
            Assert.Equal(0.0f, outBands[i]);
        }
    }

    [Fact]
    public void Binner_TiltBoostsHighFrequencies()
    {
        var binner = new LogarithmicSpectrumBinner(
            sampleRate: 44100,
            fftLength: 2048,
            bandCount: 64,
            minFrequency: 20f,
            maxFrequency: 20000f,
            tiltDbPerOctave: 4.0f,
            sensitivity: 1.0f,
            noiseGate: 0.0f);

        // Constant flat spectrum
        float[] magnitudes = new float[1025];
        Array.Fill(magnitudes, 0.005f);

        float[] outBands = new float[64];
        binner.Process(magnitudes, outBands);

        // Treble bands should be significantly higher than bass bands due to +4 dB/octave tilt
        Assert.True(outBands[^1] > outBands[0], $"Treble band ({outBands[^1]}) should exceed bass band ({outBands[0]})");
    }
}
