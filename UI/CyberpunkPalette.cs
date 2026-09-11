using SkiaSharp;

namespace SimpleMusicVisualizer.UI;

/// <summary>
/// Defines the cyberpunk neon color palette and reusable SkiaSharp paints, gradients, and filters.
/// </summary>
public static class CyberpunkPalette
{
    // --- Cyberpunk Color Constants ---
    public static readonly SKColor VoidBlack = new(0x06, 0x02, 0x0E);         // #06020E - Void background
    public static readonly SKColor DeepSpace = new(0x12, 0x05, 0x24);         // #120524 - Deep cosmic purple
    public static readonly SKColor DarkSurface = new(0x0C, 0x06, 0x18);       // #0C0618 - Surface background
    public static readonly SKColor ElectricCyan = new(0x00, 0xF0, 0xFF);      // #00F0FF - High energy primary
    public static readonly SKColor BrightCyan = new(0x00, 0xFF, 0xFF);        // #00FFFF - Intense peak cyan
    public static readonly SKColor Ultraviolet = new(0x8A, 0x2B, 0xE2);       // #8A2BE2 - Deep neon violet
    public static readonly SKColor GlowingViolet = new(0x9D, 0x00, 0xFF);     // #9D00FF - Vivid neon violet
    public static readonly SKColor HotMagenta = new(0xFF, 0x00, 0x7F);        // #FF007F - Hot pink / magenta
    public static readonly SKColor AcidLime = new(0x00, 0xFF, 0xA3);          // #00FFA3 - Bright green accent
    public static readonly SKColor PureWhite = new(0xFF, 0xFF, 0xFF);         // #FFFFFF - Hot peak core
    public static readonly SKColor DimText = new(0x7A, 0x6A, 0x99);           // #7A6A99 - Muted cyberpunk label
    public static readonly SKColor ScanlineColor = new(0x00, 0xF0, 0xFF, 0x08); // 3% opacity cyan scanlines

    // --- Mask Filters for Bloom & Glow ---
    public static readonly SKMaskFilter SoftGlowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 14.0f);
    public static readonly SKMaskFilter MediumGlowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8.0f);
    public static readonly SKMaskFilter CrispGlowFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3.5f);

    // --- Reusable Anti-Aliased Paints ---

    /// <summary>
    /// Background paint using deep space fill.
    /// </summary>
    public static readonly SKPaint BackgroundPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        Color = VoidBlack
    };

    /// <summary>
    /// Subtle horizontal scanline overlay paint.
    /// </summary>
    public static readonly SKPaint ScanlinePaint = new()
    {
        IsAntialias = false,
        Style = SKPaintStyle.Stroke,
        Color = ScanlineColor,
        StrokeWidth = 1.0f
    };

    /// <summary>
    /// Subtle background grid paint.
    /// </summary>
    public static readonly SKPaint GridLinePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        Color = new SKColor(0x9D, 0x00, 0xFF, 0x12), // 7% opacity purple grid
        StrokeWidth = 1.0f
    };

    /// <summary>
    /// Wide outer neon glow bloom for spectrum bars.
    /// </summary>
    public static readonly SKPaint OuterGlowBarPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        MaskFilter = SoftGlowFilter
    };

    /// <summary>
    /// Crisp inner core rounded bar paint.
    /// </summary>
    public static readonly SKPaint CoreBarPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    /// <summary>
    /// Glowing peak bead cap paint (outer bloom).
    /// </summary>
    public static readonly SKPaint PeakBeadGlowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        Color = new SKColor(0x00, 0xF0, 0xFF, 0x90),
        MaskFilter = MediumGlowFilter
    };

    /// <summary>
    /// Crisp white-hot floating peak bead core.
    /// </summary>
    public static readonly SKPaint PeakBeadCorePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        Color = PureWhite
    };

    /// <summary>
    /// Inverted floor reflection bar paint.
    /// </summary>
    public static readonly SKPaint ReflectionBarPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    /// <summary>
    /// Radial iris center bass pulse orb glow.
    /// </summary>
    public static readonly SKPaint BassOrbGlowPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        MaskFilter = SoftGlowFilter
    };

    /// <summary>
    /// Radial iris center bass pulse orb core.
    /// </summary>
    public static readonly SKPaint BassOrbCorePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    /// <summary>
    /// Orbital ring neon outline paint.
    /// </summary>
    public static readonly SKPaint OrbitalRingPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2.0f,
        Color = new SKColor(0x8A, 0x2B, 0xE2, 0x80)
    };

    /// <summary>
    /// Radial tick accent paint.
    /// </summary>
    public static readonly SKPaint RadialTickPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeWidth = 2.0f
    };

    /// <summary>
    /// Laser ribbon chromatic aberration - Cyan shift pass.
    /// </summary>
    public static readonly SKPaint LaserCyanPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
        StrokeWidth = 3.5f,
        Color = new SKColor(0x00, 0xF0, 0xFF, 0xB0),
        MaskFilter = CrispGlowFilter
    };

    /// <summary>
    /// Laser ribbon chromatic aberration - Magenta shift pass.
    /// </summary>
    public static readonly SKPaint LaserMagentaPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
        StrokeWidth = 3.5f,
        Color = new SKColor(0xFF, 0x00, 0x7F, 0xB0),
        MaskFilter = CrispGlowFilter
    };

    /// <summary>
    /// Laser ribbon intense white-hot center core.
    /// </summary>
    public static readonly SKPaint LaserCorePaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
        StrokeWidth = 2.0f,
        Color = PureWhite
    };

    /// <summary>
    /// Laser ribbon outer ambient neon bloom.
    /// </summary>
    public static readonly SKPaint LaserBloomPaint = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
        StrokeWidth = 8.0f,
        Color = new SKColor(0x9D, 0x00, 0xFF, 0x60),
        MaskFilter = MediumGlowFilter
    };

    // --- Helper Shader Generators ---

    /// <summary>
    /// Creates a vertical linear gradient shader from top to bottom.
    /// </summary>
    public static SKShader CreateVerticalGradient(float top, float bottom, SKColor startColor, SKColor endColor)
    {
        return SKShader.CreateLinearGradient(
            new SKPoint(0, top),
            new SKPoint(0, bottom),
            [startColor, endColor],
            null,
            SKShaderTileMode.Clamp);
    }

    /// <summary>
    /// Creates a full spectrum vertical gradient shader for spectrum bars (Ultraviolet -> Hot Magenta -> Electric Cyan -> Acid Lime).
    /// </summary>
    public static SKShader CreateBarGradient(float top, float bottom)
    {
        SKColor[] colors =
        [
            AcidLime,
            ElectricCyan,
            HotMagenta,
            GlowingViolet
        ];
        float[] colorPositions = [0.0f, 0.35f, 0.70f, 1.0f];

        return SKShader.CreateLinearGradient(
            new SKPoint(0, top),
            new SKPoint(0, bottom),
            colors,
            colorPositions,
            SKShaderTileMode.Clamp);
    }

    /// <summary>
    /// Creates a floor reflection gradient shader that fades quickly to transparency.
    /// </summary>
    public static SKShader CreateReflectionGradient(float top, float bottom)
    {
        SKColor[] colors =
        [
            new SKColor(0x00, 0xF0, 0xFF, 0x50),
            new SKColor(0x9D, 0x00, 0xFF, 0x1A),
            new SKColor(0x06, 0x02, 0x0E, 0x00)
        ];
        float[] colorPositions = [0.0f, 0.45f, 1.0f];

        return SKShader.CreateLinearGradient(
            new SKPoint(0, top),
            new SKPoint(0, bottom),
            colors,
            colorPositions,
            SKShaderTileMode.Clamp);
    }

    /// <summary>
    /// Creates a radial gradient shader for pulsing energy bass orbs.
    /// </summary>
    public static SKShader CreateRadialOrbShader(SKPoint center, float radius, SKColor coreColor, SKColor edgeColor)
    {
        return SKShader.CreateRadialGradient(
            center,
            radius,
            [coreColor, edgeColor, new SKColor(edgeColor.Red, edgeColor.Green, edgeColor.Blue, 0)],
            [0.0f, 0.6f, 1.0f],
            SKShaderTileMode.Clamp);
    }
}
