using System;
using SimpleMusicVisualizer.Core.Dsp;
using Xunit;

namespace SimpleMusicVisualizer.Tests;

public class BlackmanHarrisWindowTests
{
    [Theory]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(4096)]
    public void Window_SymmetryAndBounds_AreValid(int length)
    {
        var window = new BlackmanHarrisWindow(length);
        var (isValid, errorMessage) = window.VerifySymmetryAndBounds();

        Assert.True(isValid, errorMessage);
        Assert.Equal(length, window.Length);
    }

    [Fact]
    public void Window_Endpoints_AreNearZero()
    {
        var window = new BlackmanHarrisWindow(2048);
        var coeffs = window.Coefficients;

        Assert.True(coeffs[0] < 0.001f);
        Assert.True(coeffs[^1] < 0.001f);
    }

    [Fact]
    public void Window_Center_IsNearOne()
    {
        var window = new BlackmanHarrisWindow(2048);
        var coeffs = window.Coefficients;

        Assert.True(coeffs[1024] > 0.99f);
    }

    [Fact]
    public void Window_Apply_InPlaceAndOutOfPlace_ProduceIdenticalResults()
    {
        var window = new BlackmanHarrisWindow(512);
        float[] input = new float[512];
        for (int i = 0; i < 512; i++) input[i] = 1.0f;

        float[] outOfPlace = new float[512];
        window.Apply(input, outOfPlace);

        float[] inPlace = new float[512];
        Array.Copy(input, inPlace, 512);
        window.Apply(inPlace);

        for (int i = 0; i < 512; i++)
        {
            Assert.Equal(outOfPlace[i], inPlace[i], precision: 6);
        }
    }
}
