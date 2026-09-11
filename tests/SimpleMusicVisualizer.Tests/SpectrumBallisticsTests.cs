using System;
using SimpleMusicVisualizer.Core.Dsp;
using Xunit;

namespace SimpleMusicVisualizer.Tests;

public class SpectrumBallisticsTests
{
    [Fact]
    public void Ballistics_AttackFasterThanDecay()
    {
        var ballistics = new SpectrumBallistics(bandCount: 1, attack: 0.90f, decay: 0.10f);

        float[] input = [1.0f];
        float[] smoothed = [0.0f];
        float[] peaks = [0.0f];

        // Frame 1: sharp rise from 0 to 1 with 0.90 attack
        ballistics.Process(input, smoothed, peaks);
        Assert.Equal(0.90f, smoothed[0], precision: 2);
        Assert.Equal(0.90f, peaks[0], precision: 2);

        // Frame 2: drop input to 0 with 0.10 decay
        input[0] = 0.0f;
        ballistics.Process(input, smoothed, peaks);

        // Smoothed should decay slowly: 0.90 + (0 - 0.90) * 0.10 = 0.81
        Assert.Equal(0.81f, smoothed[0], precision: 2);

        // Peak should still hold because PeakHoldFrames = 6
        Assert.Equal(0.90f, peaks[0], precision: 2);
    }

    [Fact]
    public void Ballistics_PeakMarker_HoldsAndFallsWithGravity()
    {
        int holdFrames = 3;
        float gravity = 0.05f;
        var ballistics = new SpectrumBallistics(bandCount: 1, attack: 1.0f, decay: 0.5f, gravity: gravity, peakHoldFrames: holdFrames);

        float[] input = [1.0f];
        float[] smoothed = [0.0f];
        float[] peaks = [0.0f];

        // Initial hit: sets peak = 1.0
        ballistics.Process(input, smoothed, peaks);
        Assert.Equal(1.0f, peaks[0]);

        // Input drops to zero
        input[0] = 0.0f;

        // Hold frame 1
        ballistics.Process(input, smoothed, peaks);
        Assert.Equal(1.0f, peaks[0]);

        // Hold frame 2
        ballistics.Process(input, smoothed, peaks);
        Assert.Equal(1.0f, peaks[0]);

        // Hold frame 3
        ballistics.Process(input, smoothed, peaks);
        Assert.Equal(1.0f, peaks[0]);

        // Gravity falloff frame 1: velocity becomes 0.05, peak becomes 1.0 - 0.05 = 0.95
        ballistics.Process(input, smoothed, peaks);
        Assert.Equal(0.95f, peaks[0], precision: 3);

        // Gravity falloff frame 2: velocity becomes 0.10, peak becomes 0.95 - 0.10 = 0.85
        ballistics.Process(input, smoothed, peaks);
        Assert.Equal(0.85f, peaks[0], precision: 3);
    }

    [Fact]
    public void Ballistics_Reset_ClearsAllState()
    {
        var ballistics = new SpectrumBallistics(bandCount: 2);
        float[] input = [0.8f, 0.9f];
        float[] smoothed = [0f, 0f];
        float[] peaks = [0f, 0f];

        ballistics.Process(input, smoothed, peaks);
        ballistics.Reset();

        // Process zeros
        input[0] = 0f;
        input[1] = 0f;
        ballistics.Process(input, smoothed, peaks);

        Assert.Equal(0f, smoothed[0]);
        Assert.Equal(0f, smoothed[1]);
        Assert.Equal(0f, peaks[0]);
        Assert.Equal(0f, peaks[1]);
    }
}
