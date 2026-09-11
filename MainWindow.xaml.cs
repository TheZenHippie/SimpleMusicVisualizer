using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SkiaSharp.Views.Desktop;
using SimpleMusicVisualizer.UI;

namespace SimpleMusicVisualizer;

/// <summary>
/// Interaction logic for MainWindow.xaml.
/// Minimal code-behind adhering strictly to WPF MVVM best practices:
/// UI-specific rendering invalidation loop and surface painting delegation.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly CyberpunkVisualizerRenderer _renderer;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        _renderer = new CyberpunkVisualizerRenderer();

        DataContext = _viewModel;

        // Hook into WPF CompositionTarget.Rendering for high-framerate Skia canvas invalidation
        CompositionTarget.Rendering += OnCompositionTargetRendering;

        Closed += OnWindowClosed;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_SETICON = 0x0080;
    private const int ICON_SMALL = 0;
    private const int ICON_BIG = 1;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            try
            {
                var streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/icon.ico"));
                if (streamInfo?.Stream != null)
                {
                    using var icon = new System.Drawing.Icon(streamInfo.Stream);
                    SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_SMALL, icon.Handle);
                    SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_BIG, icon.Handle);
                }
            }
            catch
            {
                // Fallback gracefully to Window.Icon
            }
        }
    }

    private void OnCompositionTargetRendering(object? sender, EventArgs e)
    {
        // Request repaint of the SkiaSharp visual surface on next composition pass
        SkElement.InvalidateVisual();
    }

    private void SkElement_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        // 1. Process latest audio samples, FFT, and ballistics in ViewModel
        _viewModel.UpdateAudioFrame();

        // 2. Delegate hardware-accelerated drawing to CyberpunkVisualizerRenderer
        _renderer.Render(
            e.Surface.Canvas,
            e.Info,
            _viewModel.Magnitudes,
            _viewModel.Peaks,
            _viewModel.CurrentMode,
            _viewModel.Sensitivity,
            _viewModel.AnimationTime,
            _viewModel.Waveform);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        CompositionTarget.Rendering -= OnCompositionTargetRendering;
        _renderer.Dispose();
        _viewModel.Dispose();
    }
}