using SimpleMusicVisualizer.Core.Audio;

namespace SimpleMusicVisualizer.Tests;

public sealed class AudioTests
{
    [Fact]
    public void AudioRingBuffer_BasicWriteAndRead_ReturnsCorrectData()
    {
        var buffer = new AudioRingBuffer(16);
        float[] input = [1f, 2f, 3f, 4f, 5f];
        buffer.Write(input);

        Assert.Equal(5, buffer.Available);

        Span<float> output = stackalloc float[5];
        int read = buffer.Read(output);

        Assert.Equal(5, read);
        Assert.Equal(0, buffer.Available);
        for (int i = 0; i < input.Length; i++)
        {
            Assert.Equal(input[i], output[i]);
        }
    }

    [Fact]
    public void AudioRingBuffer_WrapAround_PreservesOrder()
    {
        var buffer = new AudioRingBuffer(8);

        // Write 6, read 4 -> readIndex at 4, writeIndex at 6
        buffer.Write([1f, 2f, 3f, 4f, 5f, 6f]);
        Span<float> firstRead = stackalloc float[4];
        buffer.Read(firstRead);

        // Write 5 more -> wraps around the 8-sample boundary
        buffer.Write([7f, 8f, 9f, 10f, 11f]);

        Assert.Equal(7, buffer.Available); // 2 remaining + 5 new

        Span<float> secondRead = stackalloc float[7];
        int read = buffer.Read(secondRead);

        Assert.Equal(7, read);
        float[] expected = [5f, 6f, 7f, 8f, 9f, 10f, 11f];
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], secondRead[i]);
        }
    }

    [Fact]
    public void AudioRingBuffer_Overflow_OverwritesOldestSamples()
    {
        var buffer = new AudioRingBuffer(4);
        buffer.Write([1f, 2f, 3f, 4f]);
        Assert.Equal(4, buffer.Available);

        // Write 2 more, should overwrite 1f and 2f
        buffer.Write([5f, 6f]);
        Assert.Equal(4, buffer.Available);

        Span<float> output = stackalloc float[4];
        buffer.Read(output);

        Assert.Equal(3f, output[0]);
        Assert.Equal(4f, output[1]);
        Assert.Equal(5f, output[2]);
        Assert.Equal(6f, output[3]);
    }

    [Fact]
    public void AudioRingBuffer_Peek_ReadsWithoutConsuming()
    {
        var buffer = new AudioRingBuffer(16);
        buffer.Write([10f, 20f, 30f]);

        Span<float> peekOutput = stackalloc float[3];
        int peeked = buffer.Peek(peekOutput);

        Assert.Equal(3, peeked);
        Assert.Equal(3, buffer.Available); // Not consumed
        Assert.Equal(10f, peekOutput[0]);

        Span<float> readOutput = stackalloc float[3];
        int read = buffer.Read(readOutput);

        Assert.Equal(3, read);
        Assert.Equal(0, buffer.Available); // Now consumed
    }

    [Fact]
    public void AudioRingBuffer_Clear_ResetsBuffer()
    {
        var buffer = new AudioRingBuffer(16);
        buffer.Write([1f, 2f, 3f]);
        buffer.Clear();

        Assert.Equal(0, buffer.Available);
        Span<float> output = stackalloc float[5];
        Assert.Equal(0, buffer.Read(output));
    }

    [Fact]
    public async Task AudioRingBuffer_ConcurrentReadWrite_ThreadSafe()
    {
        var buffer = new AudioRingBuffer(1024);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var token = cts.Token;

        var writeTask = Task.Run(() =>
        {
            float[] chunk = new float[64];
            while (!token.IsCancellationRequested)
            {
                Array.Fill(chunk, 1.0f);
                buffer.Write(chunk);
                Thread.Yield();
            }
        }, token);

        var readTask = Task.Run(() =>
        {
            float[] readBuffer = new float[128];
            int readTotal = 0;
            while (!token.IsCancellationRequested)
            {
                int read = buffer.Read(readBuffer);
                if (read > 0)
                {
                    readTotal += read;
                    for (int i = 0; i < read; i++)
                    {
                        Assert.Equal(1.0f, readBuffer[i]);
                    }
                }
                else
                {
                    Thread.Yield();
                }
            }
            return readTotal;
        }, token);

        await Task.Delay(250);
        await cts.CancelAsync();

        try
        {
            await Task.WhenAll(writeTask, readTask);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        int totalRead = await readTask;
        Assert.True(totalRead > 0, "Concurrent reader should have successfully read samples");
    }

    [Fact]
    public void CyberpunkAudioSimulator_GeneratesValidBoundedAudio()
    {
        var simulator = new CyberpunkAudioSimulator(48000);
        Assert.Equal(48000, simulator.SampleRate);

        float[] samples = new float[4800]; // 100 ms
        simulator.Generate(samples);

        bool hasNonZero = false;
        for (int i = 0; i < samples.Length; i++)
        {
            float s = samples[i];
            Assert.False(float.IsNaN(s), $"Sample at index {i} was NaN");
            Assert.False(float.IsInfinity(s), $"Sample at index {i} was Infinity");
            Assert.InRange(s, -1.05f, 1.05f);

            if (MathF.Abs(s) > 0.001f)
            {
                hasNonZero = true;
            }
        }

        Assert.True(hasNonZero, "Simulator produced all zeros");
    }

    [Fact]
    public void WasapiCaptureService_SimulationMode_GeneratesSamples()
    {
        using var service = new WasapiCaptureService();
        service.IsSimulationMode = true;
        Assert.True(service.IsSimulationMode);
        Assert.Equal(48000, service.SampleRate);

        bool eventFired = false;
        service.SamplesAvailable += () => { eventFired = true; };

        service.Start();
        Assert.True(service.IsRunning);

        // Wait up to 1 second for simulation samples to arrive
        int timeoutMs = 1000;
        int elapsed = 0;
        while (!eventFired && elapsed < timeoutMs)
        {
            Thread.Sleep(20);
            elapsed += 20;
        }

        Assert.True(eventFired, "SamplesAvailable event did not fire in simulation mode");

        Span<float> dest = stackalloc float[256];
        int read = service.ReadSamples(dest);
        Assert.True(read > 0, "No samples could be read from ring buffer in simulation mode");

        service.Stop();
        Assert.False(service.IsRunning);
    }

    [Fact]
    public void WasapiCaptureService_GetDevices_DoesNotThrow()
    {
        using var service = new WasapiCaptureService();
        var devices = service.GetDevices();
        Assert.NotNull(devices);
        // On headless CI/dev machines devices may be empty or contain real devices, but must never throw
    }
}
