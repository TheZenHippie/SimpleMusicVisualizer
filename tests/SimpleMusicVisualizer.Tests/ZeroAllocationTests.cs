using System;
using SimpleMusicVisualizer.Core.Dsp;
using Xunit;

namespace SimpleMusicVisualizer.Tests;

public class ZeroAllocationTests
{
    [Fact]
    public void SpectrumAnalyzer_Process_PerformsZeroHeapAllocations()
    {
        var analyzer = new SpectrumAnalyzer(fftSize: 2048, bandCount: 64);
        float[] samples = new float[2048];
        float[] bands = new float[64];
        float[] peaks = new float[64];

        // Warm-up to ensure JIT and any one-time initializers are resolved
        for (int i = 0; i < 5; i++)
        {
            analyzer.Process(samples, bands, peaks);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 100; i++)
        {
            analyzer.Process(samples, bands, peaks);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        long totalAllocated = after - before;

        Assert.Equal(0, totalAllocated);
    }
}
