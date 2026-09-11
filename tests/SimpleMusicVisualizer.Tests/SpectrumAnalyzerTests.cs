using System;
using SimpleMusicVisualizer.Core.Dsp;
using Xunit;

namespace SimpleMusicVisualizer.Tests;

public class SpectrumAnalyzerTests
{
    [Fact]
    public void Analyzer_EndToEndPipeline_ProcessesAudioCorrectly()
    {
        var analyzer = new SpectrumAnalyzer(
            sampleRate: 44100,
            fftSize: 2048,
            bandCount: 64,
            sensitivity: 2.0f);

        // Generate 1 kHz tone at 44.1 kHz
        float[] samples = new float[2048];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = MathF.Sin(2.0f * MathF.PI * 1000.0f * i / 44100.0f);
        }

        float[] bands = new float[64];
        float[] peaks = new float[64];

        analyzer.Process(samples, bands, peaks);

        // Verify bands contain valid numbers in [0, 1]
        for (int i = 0; i < 64; i++)
        {
            Assert.True(bands[i] is >= 0.0f and <= 1.0f);
            Assert.True(peaks[i] is >= 0.0f and <= 1.0f);
            Assert.True(peaks[i] >= bands[i], $"Peak {peaks[i]} must be >= band {bands[i]}");
        }

        // Peak energy should occur around 1 kHz band (mid bands around band 30-40)
        float maxBand = 0.0f;
        int maxIndex = -1;
        for (int i = 0; i < 64; i++)
        {
            if (bands[i] > maxBand)
            {
                maxBand = bands[i];
                maxIndex = i;
            }
        }

        Assert.True(maxBand > 0.1f, "1 kHz tone should register significant amplitude");
        Assert.True(maxIndex is >= 25 and <= 45, $"1 kHz should fall in mid-range bands, but was at band {maxIndex}");
    }

    [Fact]
    public void Analyzer_HandlesShorterAudioBuffer_WithZeroPadding()
    {
        var analyzer = new SpectrumAnalyzer(fftSize: 2048, bandCount: 32);

        // Provide only 512 samples instead of full 2048
        float[] shortSamples = new float[512];
        Array.Fill(shortSamples, 0.5f);

        float[] bands = new float[32];
        float[] peaks = new float[32];

        // Should not throw or crash
        analyzer.Process(shortSamples, bands, peaks);

        Assert.Equal(32, bands.Length);
        Assert.Equal(32, peaks.Length);
    }
}
