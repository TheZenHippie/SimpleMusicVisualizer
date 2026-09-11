using System;
using SkiaSharp;

namespace SimpleMusicVisualizer.UI;

/// <summary>
/// High-performance SkiaSharp rendering engine for cyberpunk audio visualizations.
/// Supports SpectrumBars, RadialIris, and LaserRibbon modes with multi-pass neon bloom,
/// floating gravity peak beads, mirrored floor reflections, and CRT scanlines.
/// </summary>
public sealed class CyberpunkVisualizerRenderer : IDisposable
{
    private readonly SKPath _ribbonCorePath = new();
    private readonly SKPath _ribbonCyanPath = new();
    private readonly SKPath _ribbonMagentaPath = new();
    private readonly SKPath _ribbonGhostPath = new();

    // Cached shaders for dimensions to avoid allocation per frame
    private SKShader? _cachedBgShader;
    private float _lastBgHeight = -1;

    private SKShader? _cachedBarShader;
    private float _lastBarTop = -1;
    private float _lastBarBottom = -1;

    private SKShader? _cachedReflShader;
    private float _lastReflTop = -1;
    private float _lastReflBottom = -1;

    // Dynamic Paints
    private readonly SKPaint _dynamicBarCorePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private readonly SKPaint _dynamicBarGlowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        MaskFilter = CyberpunkPalette.SoftGlowFilter
    };

    private readonly SKPaint _dynamicReflPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    private readonly SKPaint _baselinePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        Color = new SKColor(0x00, 0xF0, 0xFF, 0x80),
        StrokeWidth = 1.5f
    };

    private readonly SKPaint _baselineGlowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        Color = new SKColor(0x9D, 0x00, 0xFF, 0x50),
        StrokeWidth = 5.0f,
        MaskFilter = CyberpunkPalette.CrispGlowFilter
    };

    private readonly SKPaint _radialBarPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round
    };

    private readonly SKPaint _radialGlowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        MaskFilter = CyberpunkPalette.CrispGlowFilter
    };

    /// <summary>
    /// Renders the audio visualization on the given Skia canvas.
    /// </summary>
    /// <param name="canvas">The target SkiaSharp canvas.</param>
    /// <param name="info">Canvas surface image information.</param>
    /// <param name="magnitudes">Normalized frequency magnitudes (0.0 to 1.0).</param>
    /// <param name="peaks">Normalized peak marker values (0.0 to 1.0).</param>
    /// <param name="mode">Visualizer render mode.</param>
    /// <param name="sensitivity">User sensitivity multiplier.</param>
    /// <param name="animationTime">Total elapsed time in seconds for procedural animations.</param>
    /// <param name="waveform">Optional raw waveform samples for oscilloscope mode.</param>
    public void Render(
        SKCanvas canvas,
        SKImageInfo info,
        ReadOnlySpan<float> magnitudes,
        ReadOnlySpan<float> peaks,
        VisualizerMode mode,
        float sensitivity = 1.0f,
        float animationTime = 0.0f,
        ReadOnlySpan<float> waveform = default)
    {
        float width = info.Width;
        float height = info.Height;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        // 1. Draw Cyberpunk Void Gradient & CRT Scanlines
        DrawBackground(canvas, width, height);

        // Compute overall bass energy from lower 15% of spectrum
        float bassEnergy = ComputeBassEnergy(magnitudes, sensitivity);

        // 2. Render Selected Visualizer Mode
        switch (mode)
        {
            case VisualizerMode.SpectrumBars:
                RenderSpectrumBars(canvas, width, height, magnitudes, peaks, sensitivity, bassEnergy);
                break;

            case VisualizerMode.RadialIris:
                RenderRadialIris(canvas, width, height, magnitudes, peaks, sensitivity, bassEnergy, animationTime);
                break;

            case VisualizerMode.LaserRibbon:
                RenderLaserRibbon(canvas, width, height, magnitudes, waveform, sensitivity, bassEnergy, animationTime);
                break;
        }

        // 3. Subtle Vignette / Corner Accents
        DrawCyberpunkHudAccents(canvas, width, height, animationTime);
    }

    #region Background & Scanlines

    private void DrawBackground(SKCanvas canvas, float width, float height)
    {
        // Void gradient
        if (_cachedBgShader == null || Math.Abs(_lastBgHeight - height) > 1.0f)
        {
            _cachedBgShader?.Dispose();
            _cachedBgShader = CyberpunkPalette.CreateVerticalGradient(0, height, CyberpunkPalette.VoidBlack, CyberpunkPalette.DeepSpace);
            CyberpunkPalette.BackgroundPaint.Shader = _cachedBgShader;
            _lastBgHeight = height;
        }

        canvas.DrawRect(0, 0, width, height, CyberpunkPalette.BackgroundPaint);

        // Subtle vertical cyber grid
        float gridSpacingX = 64.0f;
        for (float x = gridSpacingX; x < width; x += gridSpacingX)
        {
            canvas.DrawLine(x, 0, x, height, CyberpunkPalette.GridLinePaint);
        }

        // Subtle horizontal CRT scanlines
        float scanlineStep = 4.0f;
        for (float y = 0; y < height; y += scanlineStep)
        {
            canvas.DrawLine(0, y, width, y, CyberpunkPalette.ScanlinePaint);
        }
    }

    private void DrawCyberpunkHudAccents(SKCanvas canvas, float width, float height, float time)
    {
        using var cornerPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x00, 0xF0, 0xFF, 0x40),
            StrokeWidth = 1.5f
        };

        // Top-left reticle
        canvas.DrawLine(16, 16, 36, 16, cornerPaint);
        canvas.DrawLine(16, 16, 16, 36, cornerPaint);

        // Top-right reticle
        canvas.DrawLine(width - 36, 16, width - 16, 16, cornerPaint);
        canvas.DrawLine(width - 16, 16, width - 16, 36, cornerPaint);

        // Bottom-left reticle
        canvas.DrawLine(16, height - 16, 36, height - 16, cornerPaint);
        canvas.DrawLine(16, height - 36, 16, height - 16, cornerPaint);

        // Bottom-right reticle
        canvas.DrawLine(width - 36, height - 16, width - 16, height - 16, cornerPaint);
        canvas.DrawLine(width - 16, height - 36, width - 16, height - 16, cornerPaint);
    }

    #endregion

    #region Mode 1: SpectrumBars

    private void RenderSpectrumBars(
        SKCanvas canvas,
        float width,
        float height,
        ReadOnlySpan<float> magnitudes,
        ReadOnlySpan<float> peaks,
        float sensitivity,
        float bassEnergy)
    {
        int count = magnitudes.Length;
        if (count == 0) return;

        float baselineY = height * 0.72f;
        float maxBarHeight = baselineY - 45.0f;
        float maxReflHeight = (height - baselineY) * 0.75f;

        // Ensure bar shader is initialized
        float barTop = baselineY - maxBarHeight;
        if (_cachedBarShader == null || Math.Abs(_lastBarTop - barTop) > 1.0f || Math.Abs(_lastBarBottom - baselineY) > 1.0f)
        {
            _cachedBarShader?.Dispose();
            _cachedBarShader = CyberpunkPalette.CreateBarGradient(barTop, baselineY);
            _dynamicBarCorePaint.Shader = _cachedBarShader;
            _dynamicBarGlowPaint.Shader = _cachedBarShader;
            _lastBarTop = barTop;
            _lastBarBottom = baselineY;
        }

        // Ensure reflection shader is initialized
        float reflBottom = baselineY + maxReflHeight;
        if (_cachedReflShader == null || Math.Abs(_lastReflTop - baselineY) > 1.0f || Math.Abs(_lastReflBottom - reflBottom) > 1.0f)
        {
            _cachedReflShader?.Dispose();
            _cachedReflShader = CyberpunkPalette.CreateReflectionGradient(baselineY, reflBottom);
            _dynamicReflPaint.Shader = _cachedReflShader;
            _lastReflTop = baselineY;
            _lastReflBottom = reflBottom;
        }

        float margin = width * 0.04f;
        float usableWidth = width - (margin * 2.0f);
        float slotWidth = usableWidth / count;
        float barWidth = Math.Max(2.0f, slotWidth * 0.72f);
        float cornerRadius = Math.Clamp(barWidth * 0.35f, 1.5f, 4.0f);

        // --- Pass 1: Outer Neon Glow Pass (Bloom) ---
        for (int i = 0; i < count; i++)
        {
            float mag = Math.Clamp(magnitudes[i] * sensitivity, 0.005f, 1.0f);
            float barH = mag * maxBarHeight;

            if (barH > 4.0f)
            {
                float x = margin + (i * slotWidth) + ((slotWidth - barWidth) * 0.5f);
                var glowRect = new SKRoundRect(
                    new SKRect(x - 3.0f, baselineY - barH - 3.0f, x + barWidth + 3.0f, baselineY),
                    cornerRadius + 2.0f,
                    cornerRadius + 2.0f);
                canvas.DrawRoundRect(glowRect, _dynamicBarGlowPaint);
            }
        }

        // --- Pass 2: Inner Core Crisp Rounded Bar Pass ---
        for (int i = 0; i < count; i++)
        {
            float mag = Math.Clamp(magnitudes[i] * sensitivity, 0.005f, 1.0f);
            float barH = mag * maxBarHeight;
            float x = margin + (i * slotWidth) + ((slotWidth - barWidth) * 0.5f);

            var barRect = new SKRoundRect(
                new SKRect(x, baselineY - barH, x + barWidth, baselineY),
                cornerRadius,
                cornerRadius);
            canvas.DrawRoundRect(barRect, _dynamicBarCorePaint);
        }

        // --- Pass 3: Floating Peak Beads with Gravity ---
        for (int i = 0; i < count; i++)
        {
            float peak = (peaks.Length > i) ? peaks[i] : magnitudes[i];
            peak = Math.Clamp(peak * sensitivity, 0.005f, 1.0f);
            float peakH = peak * maxBarHeight;

            float x = margin + (i * slotWidth) + ((slotWidth - barWidth) * 0.5f);
            float beadY = baselineY - peakH - 4.0f;
            float beadRadius = Math.Clamp(barWidth * 0.45f, 1.5f, 4.0f);
            var beadCenter = new SKPoint(x + (barWidth * 0.5f), beadY);

            // Glowing halo
            canvas.DrawCircle(beadCenter, beadRadius + 3.0f, CyberpunkPalette.PeakBeadGlowPaint);
            // Pure white core
            canvas.DrawCircle(beadCenter, beadRadius, CyberpunkPalette.PeakBeadCorePaint);
        }

        // --- Pass 4: Inverted Floor Reflection Pass ---
        for (int i = 0; i < count; i++)
        {
            float mag = Math.Clamp(magnitudes[i] * sensitivity, 0.005f, 1.0f);
            float reflH = mag * maxReflHeight;
            if (reflH < 2.0f) continue;

            float x = margin + (i * slotWidth) + ((slotWidth - barWidth) * 0.5f);
            var reflRect = new SKRoundRect(
                new SKRect(x, baselineY + 2.0f, x + barWidth, baselineY + 2.0f + reflH),
                cornerRadius,
                cornerRadius);
            canvas.DrawRoundRect(reflRect, _dynamicReflPaint);
        }

        // Floor reflection scanline cutouts
        for (float y = baselineY + 4.0f; y < baselineY + maxReflHeight; y += 4.0f)
        {
            canvas.DrawLine(margin, y, width - margin, y, CyberpunkPalette.ScanlinePaint);
        }

        // Glowing Baseline Line
        canvas.DrawLine(margin, baselineY, width - margin, baselineY, _baselineGlowPaint);
        canvas.DrawLine(margin, baselineY, width - margin, baselineY, _baselinePaint);
    }

    #endregion

    #region Mode 2: RadialIris

    private void RenderRadialIris(
        SKCanvas canvas,
        float width,
        float height,
        ReadOnlySpan<float> magnitudes,
        ReadOnlySpan<float> peaks,
        float sensitivity,
        float bassEnergy,
        float time)
    {
        int count = magnitudes.Length;
        if (count == 0) return;

        var center = new SKPoint(width * 0.5f, height * 0.48f);
        float minDim = Math.Min(width, height);
        float baseRadius = minDim * 0.16f;
        float maxSpokeLength = minDim * 0.28f;

        // --- 1. Pulsing Bass Orb in Center ---
        float pulseFactor = Math.Clamp(bassEnergy, 0.0f, 1.5f);
        float orbRadius = baseRadius * (0.62f + (0.38f * pulseFactor));

        using var orbGlowShader = CyberpunkPalette.CreateRadialOrbShader(
            center,
            orbRadius * 1.6f,
            CyberpunkPalette.ElectricCyan,
            CyberpunkPalette.GlowingViolet);
        CyberpunkPalette.BassOrbGlowPaint.Shader = orbGlowShader;
        canvas.DrawCircle(center, orbRadius * 1.5f, CyberpunkPalette.BassOrbGlowPaint);

        using var orbCoreShader = CyberpunkPalette.CreateRadialOrbShader(
            center,
            orbRadius,
            CyberpunkPalette.PureWhite,
            CyberpunkPalette.HotMagenta);
        CyberpunkPalette.BassOrbCorePaint.Shader = orbCoreShader;
        canvas.DrawCircle(center, orbRadius, CyberpunkPalette.BassOrbCorePaint);

        // Center reticle lines
        using var reticlePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(0x00, 0xF0, 0xFF, 0x80),
            StrokeWidth = 1.2f
        };
        float reticleLen = orbRadius * 0.4f;
        canvas.DrawLine(center.X - reticleLen, center.Y, center.X + reticleLen, center.Y, reticlePaint);
        canvas.DrawLine(center.X, center.Y - reticleLen, center.X, center.Y + reticleLen, reticlePaint);

        // --- 2. Inner & Outer Orbit Rings ---
        canvas.DrawCircle(center, baseRadius, CyberpunkPalette.OrbitalRingPaint);

        float outerRingRadius = baseRadius + maxSpokeLength + 14.0f;
        using var outerOrbitPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.0f,
            Color = new SKColor(0x00, 0xF0, 0xFF, 0x30),
            PathEffect = SKPathEffect.CreateDash([6.0f, 6.0f], (time * 15.0f) % 12.0f)
        };
        canvas.DrawCircle(center, outerRingRadius, outerOrbitPaint);

        // --- 3. Radiating Mirrored Spectrum Spokes ---
        // Symmetrical layout: left and right hemispheres both display bass to treble
        int halfCount = count / 2;
        if (halfCount < 2) halfCount = count;

        float rotationOffset = (time * 0.15f); // Gentle rotation drift
        float angleStep = (float)(Math.PI / halfCount);

        for (int i = 0; i < halfCount; i++)
        {
            float mag = Math.Clamp(magnitudes[i] * sensitivity, 0.01f, 1.0f);
            float peak = (peaks.Length > i) ? peaks[i] : magnitudes[i];
            peak = Math.Clamp(peak * sensitivity, 0.01f, 1.0f);

            float spokeLen = mag * maxSpokeLength;
            float peakDist = peak * maxSpokeLength;

            // Frequency-based color interpolation
            float t = (float)i / halfCount;
            SKColor spokeColor = LerpCyberColor(t);

            _radialBarPaint.Color = spokeColor;
            _radialBarPaint.StrokeWidth = Math.Clamp((float)(2.0 * Math.PI * baseRadius / (halfCount * 2.5)), 1.5f, 4.0f);

            _radialGlowPaint.Color = spokeColor;
            _radialGlowPaint.StrokeWidth = _radialBarPaint.StrokeWidth + 4.0f;

            // Right spoke angle (from bottom up to top)
            float angleRight = (float)(Math.PI * 0.5) - (i * angleStep) + rotationOffset;
            DrawRadialSpoke(canvas, center, baseRadius, spokeLen, peakDist, angleRight, spokeColor);

            // Left spoke angle (from bottom up to top, mirrored)
            float angleLeft = (float)(Math.PI * 0.5) + (i * angleStep) + rotationOffset;
            DrawRadialSpoke(canvas, center, baseRadius, spokeLen, peakDist, angleLeft, spokeColor);
        }

        // --- 4. Orbital Compass Ticks ---
        using var compassTickPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.0f,
            Color = CyberpunkPalette.ElectricCyan
        };
        for (int q = 0; q < 4; q++)
        {
            float tickAngle = (float)(q * Math.PI * 0.5) + rotationOffset;
            float cos = (float)Math.Cos(tickAngle);
            float sin = (float)Math.Sin(tickAngle);

            var p0 = new SKPoint(center.X + ((outerRingRadius - 4.0f) * cos), center.Y + ((outerRingRadius - 4.0f) * sin));
            var p1 = new SKPoint(center.X + ((outerRingRadius + 4.0f) * cos), center.Y + ((outerRingRadius + 4.0f) * sin));
            canvas.DrawLine(p0, p1, compassTickPaint);
        }
    }

    private void DrawRadialSpoke(
        SKCanvas canvas,
        SKPoint center,
        float baseRadius,
        float spokeLen,
        float peakDist,
        float angle,
        SKColor color)
    {
        float cos = (float)Math.Cos(angle);
        float sin = (float)Math.Sin(angle);

        var innerPt = new SKPoint(center.X + (baseRadius * cos), center.Y + (baseRadius * sin));
        var outerPt = new SKPoint(center.X + ((baseRadius + spokeLen) * cos), center.Y + ((baseRadius + spokeLen) * sin));

        // Glow pass
        canvas.DrawLine(innerPt, outerPt, _radialGlowPaint);
        // Crisp core pass
        canvas.DrawLine(innerPt, outerPt, _radialBarPaint);

        // Peak bead dot
        if (peakDist > spokeLen + 2.0f)
        {
            var peakPt = new SKPoint(center.X + ((baseRadius + peakDist + 3.0f) * cos), center.Y + ((baseRadius + peakDist + 3.0f) * sin));
            canvas.DrawCircle(peakPt, 2.5f, CyberpunkPalette.PeakBeadGlowPaint);
            canvas.DrawCircle(peakPt, 1.5f, CyberpunkPalette.PeakBeadCorePaint);
        }
    }

    #endregion

    #region Mode 3: LaserRibbon

    private void RenderLaserRibbon(
        SKCanvas canvas,
        float width,
        float height,
        ReadOnlySpan<float> magnitudes,
        ReadOnlySpan<float> waveform,
        float sensitivity,
        float bassEnergy,
        float time)
    {
        float midY = height * 0.48f;
        float maxAmplitude = height * 0.36f;
        int points = 160;

        _ribbonCorePath.Reset();
        _ribbonCyanPath.Reset();
        _ribbonMagentaPath.Reset();
        _ribbonGhostPath.Reset();

        float stepX = width / (points - 1);

        // Construct dynamic laser waveform with chromatic aberration
        for (int p = 0; p < points; p++)
        {
            float x = p * stepX;
            float u = (float)p / (points - 1); // 0.0 to 1.0 across width

            // Window edge damping so ribbon tapers gracefully at screen edges
            float edgeWindow = (float)Math.Sin(u * Math.PI);

            float displacement = 0.0f;

            if (waveform.Length >= points)
            {
                // Raw time-domain waveform available
                int sampleIdx = (int)(u * (waveform.Length - 1));
                displacement = waveform[sampleIdx] * maxAmplitude * sensitivity;
            }
            else if (magnitudes.Length > 0)
            {
                // Synthesize rich harmonic laser wave from spectrum bins + bass energy
                int binLow = Math.Clamp((int)(u * (magnitudes.Length * 0.25f)), 0, magnitudes.Length - 1);
                int binMid = Math.Clamp((int)(u * (magnitudes.Length * 0.65f)), 0, magnitudes.Length - 1);
                int binHigh = Math.Clamp((int)(u * (magnitudes.Length - 1)), 0, magnitudes.Length - 1);

                float lowMag = magnitudes[binLow] * sensitivity;
                float midMag = magnitudes[binMid] * sensitivity;
                float highMag = magnitudes[binHigh] * sensitivity;

                // Multi-frequency laser interference
                float wave1 = (float)Math.Sin((u * 4.0 * Math.PI) - (time * 6.0f)) * (lowMag * 0.7f + bassEnergy * 0.3f);
                float wave2 = (float)Math.Sin((u * 12.0 * Math.PI) + (time * 10.0f)) * midMag * 0.45f;
                float wave3 = (float)Math.Sin((u * 28.0 * Math.PI) - (time * 16.0f)) * highMag * 0.25f;

                displacement = (wave1 + wave2 + wave3) * maxAmplitude;
            }
            else
            {
                // Idle breathing wave
                displacement = (float)Math.Sin((u * 4.0 * Math.PI) + (time * 3.0f)) * (maxAmplitude * 0.08f);
            }

            displacement *= edgeWindow;

            float yCore = midY + displacement;
            float yCyan = midY + displacement - 3.0f;
            float yMagenta = midY + displacement + 3.0f;
            float yGhost = midY + (displacement * 0.75f) + 1.5f;

            if (p == 0)
            {
                _ribbonCorePath.MoveTo(x, yCore);
                _ribbonCyanPath.MoveTo(x - 2.0f, yCyan);
                _ribbonMagentaPath.MoveTo(x + 2.0f, yMagenta);
                _ribbonGhostPath.MoveTo(x, yGhost);
            }
            else
            {
                _ribbonCorePath.LineTo(x, yCore);
                _ribbonCyanPath.LineTo(x - 2.0f, yCyan);
                _ribbonMagentaPath.LineTo(x + 2.0f, yMagenta);
                _ribbonGhostPath.LineTo(x, yGhost);
            }
        }

        // --- Pass 1: Phosphor Persistence Ghost Trail ---
        using var ghostPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeWidth = 2.0f,
            Color = new SKColor(0x8A, 0x2B, 0xE2, 0x40)
        };
        canvas.DrawPath(_ribbonGhostPath, ghostPaint);

        // --- Pass 2: Outer Neon Ultraviolet Bloom Pass ---
        canvas.DrawPath(_ribbonCorePath, CyberpunkPalette.LaserBloomPaint);

        // --- Pass 3: Chromatic Aberration - Cyan Shift (-2px, -3px) ---
        canvas.DrawPath(_ribbonCyanPath, CyberpunkPalette.LaserCyanPaint);

        // --- Pass 4: Chromatic Aberration - Magenta Shift (+2px, +3px) ---
        canvas.DrawPath(_ribbonMagentaPath, CyberpunkPalette.LaserMagentaPaint);

        // --- Pass 5: Intense Pure White-Hot Center Laser Core ---
        canvas.DrawPath(_ribbonCorePath, CyberpunkPalette.LaserCorePaint);
    }

    #endregion

    #region Helpers

    private static float ComputeBassEnergy(ReadOnlySpan<float> magnitudes, float sensitivity)
    {
        if (magnitudes.Length == 0) return 0.0f;

        int bassBinCount = Math.Max(1, (int)(magnitudes.Length * 0.15f));
        float sum = 0.0f;
        for (int i = 0; i < bassBinCount; i++)
        {
            sum += magnitudes[i];
        }

        return (sum / bassBinCount) * sensitivity;
    }

    private static SKColor LerpCyberColor(float t)
    {
        t = Math.Clamp(t, 0.0f, 1.0f);

        if (t < 0.33f)
        {
            float localT = t / 0.33f;
            return Lerp(CyberpunkPalette.AcidLime, CyberpunkPalette.ElectricCyan, localT);
        }
        else if (t < 0.66f)
        {
            float localT = (t - 0.33f) / 0.33f;
            return Lerp(CyberpunkPalette.ElectricCyan, CyberpunkPalette.HotMagenta, localT);
        }
        else
        {
            float localT = (t - 0.66f) / 0.34f;
            return Lerp(CyberpunkPalette.HotMagenta, CyberpunkPalette.GlowingViolet, localT);
        }
    }

    private static SKColor Lerp(SKColor a, SKColor b, float t)
    {
        byte red = (byte)(a.Red + ((b.Red - a.Red) * t));
        byte green = (byte)(a.Green + ((b.Green - a.Green) * t));
        byte blue = (byte)(a.Blue + ((b.Blue - a.Blue) * t));
        return new SKColor(red, green, blue);
    }

    public void Dispose()
    {
        _ribbonCorePath.Dispose();
        _ribbonCyanPath.Dispose();
        _ribbonMagentaPath.Dispose();
        _ribbonGhostPath.Dispose();

        _cachedBgShader?.Dispose();
        _cachedBarShader?.Dispose();
        _cachedReflShader?.Dispose();

        _dynamicBarCorePaint.Dispose();
        _dynamicBarGlowPaint.Dispose();
        _dynamicReflPaint.Dispose();
        _baselinePaint.Dispose();
        _baselineGlowPaint.Dispose();
        _radialBarPaint.Dispose();
        _radialGlowPaint.Dispose();
    }

    #endregion
}
