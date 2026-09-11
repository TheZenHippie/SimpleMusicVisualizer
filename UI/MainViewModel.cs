using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using SimpleMusicVisualizer.Core.Audio;
using SimpleMusicVisualizer.Core.Dsp;

namespace SimpleMusicVisualizer.UI;

/// <summary>
/// WPF MVVM ViewModel coordinating audio capture, Blackman-Harris FFT spectrum analysis,
/// render parameters, and UI commands for the Cyberpunk Music Visualizer.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IAudioCaptureService _audioService;
    private readonly SpectrumAnalyzer _spectrumAnalyzer;

    private VisualizerMode _currentMode = VisualizerMode.SpectrumBars;
    private float _sensitivity = 1.2f;
    private int _barCount = 64;
    private bool _isSimulationMode;
    private IReadOnlyList<string> _availableDevices = [];
    private int _selectedDeviceIndex;
    private float _fps = 60.0f;
    private string _audioStatusText = "INITIALIZING...";
    private bool _hasAudioSignal;

    // Working buffers for zero allocation audio processing
    private readonly float[] _sampleBuffer = new float[2048];
    private float[] _magnitudes = new float[64];
    private float[] _peaks = new float[64];

    // FPS calculation tracking
    private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
    private int _frameCount;
    private double _lastFpsCheckTime;

    // Animation elapsed time
    private readonly Stopwatch _animationStopwatch = Stopwatch.StartNew();
    private double _lastFrameTime;

    public event PropertyChangedEventHandler? PropertyChanged;

    #region Properties

    /// <summary>
    /// Gets or sets the active visualizer display mode (SpectrumBars, RadialIris, LaserRibbon).
    /// </summary>
    public VisualizerMode CurrentMode
    {
        get => _currentMode;
        set
        {
            if (_currentMode != value)
            {
                _currentMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBarsMode));
                OnPropertyChanged(nameof(IsRadialMode));
                OnPropertyChanged(nameof(IsLaserMode));
            }
        }
    }

    public bool IsBarsMode => CurrentMode == VisualizerMode.SpectrumBars;
    public bool IsRadialMode => CurrentMode == VisualizerMode.RadialIris;
    public bool IsLaserMode => CurrentMode == VisualizerMode.LaserRibbon;

    /// <summary>
    /// Gets or sets the visualizer amplitude sensitivity (0.2x to 3.0x, default 1.2x).
    /// </summary>
    public float Sensitivity
    {
        get => _sensitivity;
        set
        {
            float clamped = Math.Clamp(value, 0.2f, 3.0f);
            if (Math.Abs(_sensitivity - clamped) > 0.001f)
            {
                _sensitivity = clamped;
                _spectrumAnalyzer.Sensitivity = clamped;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the spectrum frequency bar count (32, 64, 96, 128).
    /// </summary>
    public int BarCount
    {
        get => _barCount;
        set
        {
            if (_barCount != value && value >= 16)
            {
                _barCount = value;
                _spectrumAnalyzer.BandCount = value;
                _magnitudes = new float[value];
                _peaks = new float[value];
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether synthetic cyberpunk audio generation is active instead of live WASAPI capture.
    /// </summary>
    public bool IsSimulationMode
    {
        get => _isSimulationMode;
        set
        {
            if (_isSimulationMode != value)
            {
                _isSimulationMode = value;
                _audioService.IsSimulationMode = value;
                UpdateAudioStatus();
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Available audio render devices for loopback capture.
    /// </summary>
    public IReadOnlyList<string> AvailableDevices
    {
        get => _availableDevices;
        private set
        {
            _availableDevices = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Zero-based index of the currently selected audio render endpoint.
    /// </summary>
    public int SelectedDeviceIndex
    {
        get => _selectedDeviceIndex;
        set
        {
            if (_selectedDeviceIndex != value && value >= 0 && value < _availableDevices.Count)
            {
                _selectedDeviceIndex = value;
                _audioService.SelectDevice(value);
                UpdateAudioStatus();
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Measured rendering frames per second.
    /// </summary>
    public float Fps
    {
        get => _fps;
        private set
        {
            if (Math.Abs(_fps - value) > 0.1f)
            {
                _fps = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Cyberpunk HUD status line for audio stream state.
    /// </summary>
    public string AudioStatusText
    {
        get => _audioStatusText;
        private set
        {
            if (_audioStatusText != value)
            {
                _audioStatusText = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Indicates whether active audio signals are passing through the visualizer.
    /// </summary>
    public bool HasAudioSignal
    {
        get => _hasAudioSignal;
        private set
        {
            if (_hasAudioSignal != value)
            {
                _hasAudioSignal = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Total elapsed procedural animation time in seconds.
    /// </summary>
    public float AnimationTime { get; private set; }

    /// <summary>
    /// Normalized smoothed frequency bands (0.0 to 1.0).
    /// </summary>
    public ReadOnlySpan<float> Magnitudes => _magnitudes;

    /// <summary>
    /// Normalized falling peak markers with simulated gravity (0.0 to 1.0).
    /// </summary>
    public ReadOnlySpan<float> Peaks => _peaks;

    /// <summary>
    /// Latest raw audio waveform samples for oscilloscope laser ribbon mode.
    /// </summary>
    public ReadOnlySpan<float> Waveform => _sampleBuffer;

    #endregion

    #region Commands

    public ICommand SwitchModeCommand { get; }
    public ICommand ToggleSimulationCommand { get; }
    public ICommand MinimizeCommand { get; }
    public ICommand MaximizeCommand { get; }
    public ICommand CloseCommand { get; }

    #endregion

    /// <summary>
    /// Initializes a new instance of <see cref="MainViewModel"/>.
    /// </summary>
    /// <param name="audioService">Optional custom audio service; if null, defaults to WasapiCaptureService.</param>
    /// <param name="spectrumAnalyzer">Optional custom DSP analyzer.</param>
    public MainViewModel(IAudioCaptureService? audioService = null, SpectrumAnalyzer? spectrumAnalyzer = null)
    {
        _audioService = audioService ?? new WasapiCaptureService();
        _spectrumAnalyzer = spectrumAnalyzer ?? new SpectrumAnalyzer(
            sampleRate: _audioService.SampleRate > 0 ? _audioService.SampleRate : 48000,
            fftSize: 2048,
            bandCount: _barCount,
            sensitivity: _sensitivity);

        // Commands
        SwitchModeCommand = new RelayCommand<VisualizerMode>(mode => CurrentMode = mode);
        ToggleSimulationCommand = new RelayCommand(() => IsSimulationMode = !IsSimulationMode);
        MinimizeCommand = new RelayCommand<Window>(win =>
        {
            var target = win ?? Application.Current.MainWindow;
            if (target != null) target.WindowState = WindowState.Minimized;
        });
        MaximizeCommand = new RelayCommand<Window>(win =>
        {
            var target = win ?? Application.Current.MainWindow;
            if (target != null)
            {
                target.WindowState = target.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
        });
        CloseCommand = new RelayCommand<Window>(win =>
        {
            var target = win ?? Application.Current.MainWindow;
            target?.Close();
        });

        // Load devices
        RefreshAudioDevices();

        // Start capture service
        try
        {
            _audioService.Start();
            UpdateAudioStatus();
        }
        catch (Exception ex)
        {
            AudioStatusText = $"AUDIO ERROR: {ex.Message.ToUpperInvariant()}";
        }
    }

    /// <summary>
    /// Refreshes the list of available audio devices from the capture service.
    /// </summary>
    public void RefreshAudioDevices()
    {
        try
        {
            var devices = _audioService.GetDevices();
            AvailableDevices = devices.Count > 0 ? devices : ["Default System Output"];
            _selectedDeviceIndex = 0;
            OnPropertyChanged(nameof(SelectedDeviceIndex));
        }
        catch
        {
            AvailableDevices = ["Default System Output"];
        }
    }

    /// <summary>
    /// Core render tick: reads audio samples, runs FFT & ballistics, and computes FPS.
    /// Called directly before Skia canvas repaint.
    /// </summary>
    public void UpdateAudioFrame()
    {
        // 1. Advance animation clock
        double totalSeconds = _animationStopwatch.Elapsed.TotalSeconds;
        float deltaTime = (float)(totalSeconds - _lastFrameTime);
        _lastFrameTime = totalSeconds;
        AnimationTime = (float)totalSeconds;

        // 2. Read new audio samples into buffer
        int samplesRead = _audioService.ReadSamples(_sampleBuffer);

        // Keep spectrum analyzer sample rate synchronized
        if (_audioService.SampleRate > 0 && _spectrumAnalyzer.SampleRate != _audioService.SampleRate)
        {
            _spectrumAnalyzer.SampleRate = _audioService.SampleRate;
        }

        // 3. Process DSP & spectrum ballistics
        _spectrumAnalyzer.Process(_sampleBuffer.AsSpan(0, samplesRead > 0 ? samplesRead : _sampleBuffer.Length), _magnitudes, _peaks);

        // 4. Signal detection & status update
        float maxMag = 0.0f;
        for (int i = 0; i < _magnitudes.Length; i++)
        {
            if (_magnitudes[i] > maxMag) maxMag = _magnitudes[i];
        }
        HasAudioSignal = maxMag > 0.015f;

        // 5. FPS update every 250ms
        _frameCount++;
        double elapsedSinceFps = _fpsStopwatch.Elapsed.TotalSeconds - _lastFpsCheckTime;
        if (elapsedSinceFps >= 0.25)
        {
            Fps = (float)Math.Round(_frameCount / elapsedSinceFps, 1);
            _frameCount = 0;
            _lastFpsCheckTime = _fpsStopwatch.Elapsed.TotalSeconds;
            UpdateAudioStatus();
        }
    }

    private void UpdateAudioStatus()
    {
        if (IsSimulationMode)
        {
            AudioStatusText = "CYBERPUNK SYNTH // 48kHz DEMO STREAM";
        }
        else if (HasAudioSignal)
        {
            AudioStatusText = $"LIVE AUDIO // WASAPI LOOPBACK ({_audioService.SampleRate}Hz)";
        }
        else
        {
            AudioStatusText = $"LIVE AUDIO // LISTENING... ({_audioService.SampleRate}Hz)";
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        _audioService.Stop();
        if (_audioService is IDisposable disposableService)
        {
            disposableService.Dispose();
        }
    }
}
