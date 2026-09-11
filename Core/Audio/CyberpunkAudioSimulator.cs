namespace SimpleMusicVisualizer.Core.Audio;

/// <summary>
/// Synthetic cyberpunk audio stream generator for offline sandbox testing and when system audio is silent.
/// Synthesizes a punchy multi-frequency cyberpunk electronic mix (48 kHz default) featuring:
/// - Sub-bass kick pitch drop (55 Hz - 110 Hz)
/// - Cyberpunk arpeggiated saw bassline (220, 330, 440, 550, 660 Hz)
/// - Sizzling metallic hi-hat noise bursts (6 kHz - 12 kHz)
/// - Ambient synth pad chord sweep (300 Hz - 800 Hz)
/// </summary>
public sealed class CyberpunkAudioSimulator
{
    private readonly int _sampleRate;
    private readonly float _dt;

    // Tempo & Sequence Timing (128 BPM)
    private readonly int _samplesPerStep; // 16th note
    private readonly int _samplesPerBar;  // 4 beats (16 steps)
    private readonly int _totalPatternSamples; // 4-bar loop (64 steps)

    private long _totalSampleCount;

    // Sub-bass kick state
    private float _kickPhase;
    private int _kickSampleTimer;

    // Bass arpeggiator state
    private float _sawPhase;
    private float _sawFilterState;
    private readonly float[] _arpNotes = [220f, 330f, 220f, 440f, 330f, 550f, 440f, 660f, 550f, 440f, 330f, 220f, 330f, 440f, 550f, 440f];

    // Hi-hat bandpass filter state (6 kHz - 12 kHz)
    private uint _xorshiftState = 0x12345678;
    private float _bpfX1, _bpfX2, _bpfY1, _bpfY2;
    private readonly float _bpfB0, _bpfB2, _bpfA1, _bpfA2;
    private int _hatSampleTimer;
    private bool _isCrashCymbal;

    // Ambient pad state (300 Hz - 800 Hz)
    private float _padPhase1;
    private float _padPhase2;
    private float _padPhase3;

    /// <summary>
    /// Initializes a new instance of the <see cref="CyberpunkAudioSimulator"/> class.
    /// </summary>
    /// <param name="sampleRate">Sampling rate in Hz (default: 48000 Hz).</param>
    public CyberpunkAudioSimulator(int sampleRate = 48000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        _sampleRate = sampleRate;
        _dt = 1.0f / sampleRate;

        // 128 BPM: 1 beat = 60 / 128 s. 16th note = 1 beat / 4
        float stepSeconds = 60.0f / (128.0f * 4.0f);
        _samplesPerStep = Math.Max(1, (int)(sampleRate * stepSeconds));
        _samplesPerBar = _samplesPerStep * 16;
        _totalPatternSamples = _samplesPerBar * 4;

        // Initialize 2nd-order Biquad Band-Pass filter centered at 8.5 kHz, Q = 1.2
        float centerFreq = 8500.0f;
        float q = 1.2f;
        float omega = 2.0f * MathF.PI * centerFreq * _dt;
        float sinOmega = MathF.Sin(omega);
        float cosOmega = MathF.Cos(omega);
        float alpha = sinOmega / (2.0f * q);

        float a0 = 1.0f + alpha;
        _bpfB0 = (sinOmega / 2.0f) / a0;
        _bpfB2 = (-sinOmega / 2.0f) / a0;
        _bpfA1 = (-2.0f * cosOmega) / a0;
        _bpfA2 = (1.0f - alpha) / a0;
    }

    /// <summary>
    /// Gets the sample rate in Hz.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Generates synthetic cyberpunk audio samples and writes them into the destination span.
    /// </summary>
    /// <param name="destination">Destination span to fill with mono float samples (-1.0 to 1.0).</param>
    public void Generate(Span<float> destination)
    {
        for (int i = 0; i < destination.Length; i++)
        {
            long patternPos = _totalSampleCount % _totalPatternSamples;
            int stepIndex = (int)(patternPos / _samplesPerStep);
            int stepOffset = (int)(patternPos % _samplesPerStep);

            // --- 1. Kick Trigger (every 4 steps = 4-on-the-floor) ---
            if (stepOffset == 0 && (stepIndex % 4 == 0))
            {
                _kickSampleTimer = 0;
                _kickPhase = 0f;
            }

            float kickSample = SynthesizeKick();

            // --- 2. Cyberpunk Arp Bass Synth (every 16th note step) ---
            float bassSample = SynthesizeBassArp(stepIndex, stepOffset);

            // --- 3. Sizzling Hi-Hat / Cymbal Burst ---
            if (stepOffset == 0)
            {
                // Crash on step 0 of pattern, closed hats on off-beats (steps 2, 6, 10, 14) and ghost hats
                if (stepIndex == 0)
                {
                    _hatSampleTimer = 0;
                    _isCrashCymbal = true;
                }
                else if (stepIndex % 2 == 0)
                {
                    _hatSampleTimer = 0;
                    _isCrashCymbal = false;
                }
            }

            float hatSample = SynthesizeHiHat();

            // --- 4. Ambient Synth Pad Chord Sweep (300 Hz - 800 Hz) ---
            float padSample = SynthesizePadSweep();

            // --- Mix & Analog Saturation Limit ---
            float rawMix = (kickSample * 0.45f) + (bassSample * 0.30f) + (hatSample * 0.22f) + (padSample * 0.25f);
            destination[i] = MathF.Tanh(rawMix * 1.25f) * 0.85f;

            _totalSampleCount++;
            _kickSampleTimer++;
            _hatSampleTimer++;
        }
    }

    private float SynthesizeKick()
    {
        float kickTime = _kickSampleTimer * _dt;
        if (kickTime > 0.35f)
        {
            return 0f;
        }

        // Pitch envelope: sweeps from 110 Hz down to 55 Hz exponentially
        float freq = 55.0f + (55.0f * MathF.Exp(-kickTime * 32.0f));
        _kickPhase += 2.0f * MathF.PI * freq * _dt;
        if (_kickPhase > MathF.PI * 2.0f)
        {
            _kickPhase -= MathF.PI * 2.0f;
        }

        // Amplitude envelope: instant attack, decay
        float amp = MathF.Exp(-kickTime * 14.0f);
        float kickSine = MathF.Sin(_kickPhase);

        // Click / transient punch
        float click = MathF.Sin(2.0f * MathF.PI * 300.0f * kickTime) * MathF.Exp(-kickTime * 120.0f) * 0.25f;

        return MathF.Tanh((kickSine + click) * amp * 1.6f);
    }

    private float SynthesizeBassArp(int stepIndex, int stepOffset)
    {
        int noteIndex = stepIndex % _arpNotes.Length;
        float targetFreq = _arpNotes[noteIndex];

        // Minor variation on bar 3 and 4: sub-octave drop on selected notes
        if (stepIndex >= 32 && (stepIndex % 4 == 0))
        {
            targetFreq *= 0.5f; // Drop to 110 Hz sub
        }

        // Phase accumulation
        _sawPhase += 2.0f * MathF.PI * targetFreq * _dt;
        if (_sawPhase >= MathF.PI * 2.0f)
        {
            _sawPhase -= MathF.PI * 2.0f;
        }

        // Gritty band-rich saw wave: primary + detuned harmonic
        float saw1 = 1.0f - (_sawPhase / MathF.PI);
        float saw2 = MathF.Sin(_sawPhase * 2.0f) * 0.35f;
        float rawSaw = (saw1 * 0.7f) + saw2;

        // Step envelope (16th note plucky attack & decay)
        float stepTime = stepOffset * _dt;
        float env = MathF.Exp(-stepTime * 22.0f);

        // Dynamic resonant filter sweep modulated over 4 bars (cutoff 600 Hz - 2400 Hz)
        float lfo = 0.5f + (0.5f * MathF.Sin(2.0f * MathF.PI * 0.25f * (_totalSampleCount * _dt)));
        float cutoff = 600.0f + (1800.0f * lfo);
        float alpha = Math.Clamp(2.0f * MathF.PI * cutoff * _dt, 0.01f, 0.95f);

        _sawFilterState += alpha * (rawSaw - _sawFilterState);
        return _sawFilterState * env;
    }

    private float SynthesizeHiHat()
    {
        float hatTime = _hatSampleTimer * _dt;
        float maxDuration = _isCrashCymbal ? 0.40f : 0.08f;

        if (hatTime > maxDuration)
        {
            return 0f;
        }

        // Amplitude envelope: fast decay
        float decayRate = _isCrashCymbal ? 10.0f : 55.0f;
        float amp = MathF.Exp(-hatTime * decayRate);

        // White noise via Xorshift32 PRNG
        _xorshiftState ^= _xorshiftState << 13;
        _xorshiftState ^= _xorshiftState >> 17;
        _xorshiftState ^= _xorshiftState << 5;
        float whiteNoise = ((_xorshiftState & 0x7FFFFFFF) / (float)0x7FFFFFFF) * 2.0f - 1.0f;

        // Band-pass filter (6 kHz - 12 kHz)
        float filtered = (_bpfB0 * whiteNoise) + (_bpfB2 * _bpfX2) - (_bpfA1 * _bpfY1) - (_bpfA2 * _bpfY2);
        _bpfX2 = _bpfX1;
        _bpfX1 = whiteNoise;
        _bpfY2 = _bpfY1;
        _bpfY1 = filtered;

        return filtered * amp * (_isCrashCymbal ? 0.9f : 0.6f);
    }

    private float SynthesizePadSweep()
    {
        // Pad Chord sweep oscillating between 300 Hz and 800 Hz
        float time = _totalSampleCount * _dt;
        float sweepLfo = 0.5f + (0.5f * MathF.Sin(2.0f * MathF.PI * 0.12f * time));

        // Chord notes centered in 300 - 800 Hz range (e.g. 330 Hz, 440 Hz, 660 Hz with detuning)
        float baseFreq1 = 330.0f + (150.0f * sweepLfo);
        float baseFreq2 = 440.0f + (200.0f * sweepLfo);
        float baseFreq3 = 550.0f + (250.0f * sweepLfo);

        _padPhase1 += 2.0f * MathF.PI * baseFreq1 * _dt;
        if (_padPhase1 >= MathF.PI * 2.0f) _padPhase1 -= MathF.PI * 2.0f;

        _padPhase2 += 2.0f * MathF.PI * (baseFreq2 * 1.004f) * _dt; // Slight chorus detune
        if (_padPhase2 >= MathF.PI * 2.0f) _padPhase2 -= MathF.PI * 2.0f;

        _padPhase3 += 2.0f * MathF.PI * (baseFreq3 * 0.997f) * _dt;
        if (_padPhase3 >= MathF.PI * 2.0f) _padPhase3 -= MathF.PI * 2.0f;

        float padWave = (MathF.Sin(_padPhase1) * 0.4f)
                      + (MathF.Sin(_padPhase2) * 0.35f)
                      + (MathF.Sin(_padPhase3) * 0.25f);

        // Slow breathing tremolo
        float tremolo = 0.8f + (0.2f * MathF.Sin(2.0f * MathF.PI * 0.5f * time));
        return padWave * tremolo * 0.4f;
    }

    /// <summary>
    /// Resets the generator's phase and sample counters.
    /// </summary>
    public void Reset()
    {
        _totalSampleCount = 0;
        _kickSampleTimer = int.MaxValue / 2;
        _hatSampleTimer = int.MaxValue / 2;
        _kickPhase = 0f;
        _sawPhase = 0f;
        _sawFilterState = 0f;
        _padPhase1 = 0f;
        _padPhase2 = 0f;
        _padPhase3 = 0f;
        _bpfX1 = 0f;
        _bpfX2 = 0f;
        _bpfY1 = 0f;
        _bpfY2 = 0f;
    }
}
