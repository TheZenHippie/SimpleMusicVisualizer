namespace SimpleMusicVisualizer.Core.Audio;

/// <summary>
/// High-performance, thread-safe circular buffer for single-channel (mono) float audio samples.
/// Completely allocation-free during runtime.
/// </summary>
public sealed class AudioRingBuffer
{
    private readonly float[] _buffer;
    private readonly Lock _lock = new();
    private int _readIndex;
    private int _writeIndex;
    private int _count;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioRingBuffer"/> class.
    /// </summary>
    /// <param name="capacity">The maximum number of mono float samples the buffer can hold.</param>
    public AudioRingBuffer(int capacity = 65536)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _buffer = new float[capacity];
    }

    /// <summary>
    /// Gets the total capacity of the ring buffer.
    /// </summary>
    public int Capacity => _buffer.Length;

    /// <summary>
    /// Gets the number of samples currently available to read.
    /// </summary>
    public int Available
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    /// <summary>
    /// Writes mono float samples into the circular buffer.
    /// If incoming samples exceed available remaining capacity, older unread samples are overwritten.
    /// </summary>
    /// <param name="samples">The span of float samples to append.</param>
    public void Write(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty)
        {
            return;
        }

        lock (_lock)
        {
            int capacity = _buffer.Length;

            // If incoming batch exceeds buffer capacity, only keep the newest samples
            if (samples.Length > capacity)
            {
                samples = samples[^capacity..];
            }

            // Calculate overflow to advance read pointer if overwriting unread data
            int overflow = (_count + samples.Length) - capacity;
            if (overflow > 0)
            {
                _readIndex = (_readIndex + overflow) % capacity;
                _count -= overflow;
            }

            int firstChunk = Math.Min(samples.Length, capacity - _writeIndex);
            samples[..firstChunk].CopyTo(_buffer.AsSpan(_writeIndex, firstChunk));

            int secondChunk = samples.Length - firstChunk;
            if (secondChunk > 0)
            {
                samples[firstChunk..].CopyTo(_buffer.AsSpan(0, secondChunk));
            }

            _writeIndex = (_writeIndex + samples.Length) % capacity;
            _count += samples.Length;
        }
    }

    /// <summary>
    /// Reads and consumes available samples from the circular buffer into the destination span.
    /// </summary>
    /// <param name="destination">The destination span to write samples into.</param>
    /// <returns>The actual number of samples copied.</returns>
    public int Read(Span<float> destination)
    {
        if (destination.IsEmpty)
        {
            return 0;
        }

        lock (_lock)
        {
            if (_count == 0)
            {
                return 0;
            }

            int capacity = _buffer.Length;
            int toRead = Math.Min(destination.Length, _count);

            int firstChunk = Math.Min(toRead, capacity - _readIndex);
            _buffer.AsSpan(_readIndex, firstChunk).CopyTo(destination[..firstChunk]);

            int secondChunk = toRead - firstChunk;
            if (secondChunk > 0)
            {
                _buffer.AsSpan(0, secondChunk).CopyTo(destination[firstChunk..toRead]);
            }

            _readIndex = (_readIndex + toRead) % capacity;
            _count -= toRead;
            return toRead;
        }
    }

    /// <summary>
    /// Reads available samples from the circular buffer into the destination span without consuming them.
    /// Useful for spectrum visualizers requiring sliding FFT windows.
    /// </summary>
    /// <param name="destination">The destination span to write samples into.</param>
    /// <returns>The actual number of samples copied.</returns>
    public int Peek(Span<float> destination)
    {
        if (destination.IsEmpty)
        {
            return 0;
        }

        lock (_lock)
        {
            if (_count == 0)
            {
                return 0;
            }

            int capacity = _buffer.Length;
            int toRead = Math.Min(destination.Length, _count);

            int firstChunk = Math.Min(toRead, capacity - _readIndex);
            _buffer.AsSpan(_readIndex, firstChunk).CopyTo(destination[..firstChunk]);

            int secondChunk = toRead - firstChunk;
            if (secondChunk > 0)
            {
                _buffer.AsSpan(0, secondChunk).CopyTo(destination[firstChunk..toRead]);
            }

            return toRead;
        }
    }

    /// <summary>
    /// Resets read and write pointers and clears the buffer.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _readIndex = 0;
            _writeIndex = 0;
            _count = 0;
            Array.Clear(_buffer);
        }
    }
}
