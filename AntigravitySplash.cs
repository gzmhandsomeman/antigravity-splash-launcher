using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AntigravityLauncher
{
    public class SplashApp : Application
    {
        private const double TotalDuration = 15.0;

        private string _targetExe = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Programs\antigravity\Antigravity.exe");
        private string _targetWorkDir;

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private bool _previewMode;
        private bool _fastMode;
        private bool _fullscreenMode = true; // Fullscreen by default
        private bool _soundMode;
        private bool _paused;
        private string _captureDir;
        private string _captureGifPath;
        private string _benchLogPath;
        private List<string> _forwardArgs;

        private Window _window;
        private Grid _rootGrid;
        private Border _surfaceBorder;
        private Grid _surfaceContent;
        private ScaleTransform _surfaceScale;
        private CinematicVisualHost _cinematicHost;

        private double _surfaceWidth = 1120.0;
        private double _surfaceHeight = 640.0;
        private double _scale = 1.0;

        // Hero Original Antigravity Icon Stage
        private BitmapSource _originalIcon;
        private Grid _heroIconStage;
        private TranslateTransform _heroStageTranslate;
        private ScaleTransform _heroStageScale;
        private Ellipse _heroBackGlow;
        private ScaleTransform _heroBackGlowScale;
        private Border _pedestalRing;
        private ScaleTransform _pedestalScale;

        private Image _cyanGhostImg;
        private TranslateTransform _cyanGhostTranslate;
        private Image _magentaGhostImg;
        private TranslateTransform _magentaGhostTranslate;

        private Grid _mainIconContainer;
        private Image _mainIconImg;
        private ScaleTransform _mainIconScale;
        private TranslateTransform _mainIconTranslate;
        private Grid _specularHost;
        private Rectangle _specularBar;
        private TranslateTransform _specularTranslate;

        private Image _floorReflectionImg;

        // Act 1-2 Keynote Prologue & Callout Text
        private Grid _prologueHost;
        private TextBlock _prologueText;
        private TranslateTransform _prologueTranslate;

        private Grid _calloutHost;
        private TextBlock _calloutText;
        private ScaleTransform _calloutScale;

        // Act 4-5 Hero Wordmark, Subtitle, Hairline Progress & Telemetry
        private Grid _wordmarkHost;
        private TranslateTransform _wordmarkTranslate;
        private List<TextBlock> _wordmarkGlyphs;
        private List<double> _glyphBaseWidths;
        private double _wordmarkCanvasWidth;

        private TextBlock _subtitleText;
        private TranslateTransform _subtitleTranslate;

        private Grid _telemetryHost;
        private TranslateTransform _telemetryTranslate;
        private Rectangle _hairlineFill;
        private ScaleTransform _hairlineScale;
        private Ellipse _hairlineTipGlow;
        private TranslateTransform _hairlineTipTranslate;
        private double _barWidth;
        private TextBlock _telemetryText;
        private double _lastWordmarkTracking = -1.0;
        private string _lastTelemetryText;

        // Playback State
        private Stopwatch _stopwatch;
        private double _timeScale = 1.0;
        private double _manualTime;
        private bool _isRenderingHooked;
        private bool _launchedTarget;
        private bool _soundStarted;
        private bool _audioReady;
        private bool _finished;
        private SoundPlayer _soundPlayer;
        private MemoryStream _soundStream;

        [STAThread]
        public static int Main(string[] args)
        {
            SplashApp app = new SplashApp();
            app.ParseArgs(args);
            return app.Run();
        }

        private void ParseArgs(string[] args)
        {
            _forwardArgs = new List<string>();
            if (args == null) return;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.Equals(a, "--preview", StringComparison.OrdinalIgnoreCase))
                {
                    _previewMode = true;
                }
                else if (string.Equals(a, "--fast", StringComparison.OrdinalIgnoreCase))
                {
                    _fastMode = true;
                }
                else if (string.Equals(a, "--fullscreen", StringComparison.OrdinalIgnoreCase))
                {
                    _fullscreenMode = true;
                }
                else if (string.Equals(a, "--windowed", StringComparison.OrdinalIgnoreCase))
                {
                    _fullscreenMode = false;
                }
                else if (string.Equals(a, "--sound", StringComparison.OrdinalIgnoreCase))
                {
                    _soundMode = true;
                }
                else if (string.Equals(a, "--capture", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    _captureDir = args[++i];
                }
                else if (string.Equals(a, "--capture-gif", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    _captureGifPath = args[++i];
                }
                else if (string.Equals(a, "--target", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    _targetExe = args[++i];
                }
                else if (string.Equals(a, "--bench", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    _benchLogPath = args[++i];
                }
                else
                {
                    _forwardArgs.Add(a);
                }
            }

            if (_fastMode)
            {
                _timeScale = 3.0;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _targetWorkDir = System.IO.Path.GetDirectoryName(_targetExe);
            _originalIcon = IconAssetLoader.LoadOriginalIcon(_targetExe);
            BuildWindow();

            if (!string.IsNullOrEmpty(_captureDir))
            {
                RunCaptureMode();
                return;
            }

            if (!string.IsNullOrEmpty(_captureGifPath))
            {
                RunCaptureGifMode();
                return;
            }

            if (!string.IsNullOrEmpty(_benchLogPath))
            {
                RunBenchMode();
                return;
            }

            if (_soundMode)
            {
                PrepareAudio();
            }

            StartPlayback();
        }

        private void BuildWindow()
        {
            int pxW = GetSystemMetrics(SM_CXSCREEN);
            int pxH = GetSystemMetrics(SM_CYSCREEN);
            string resLabel = (pxW > 0 && pxH > 0) ? string.Format("{0}x{1}", pxW, pxH) : "HD";

            if (_fullscreenMode)
            {
                _surfaceWidth = Math.Ceiling(SystemParameters.PrimaryScreenWidth);
                _surfaceHeight = Math.Ceiling(SystemParameters.PrimaryScreenHeight);
            }
            else
            {
                _surfaceWidth = 1120.0;
                _surfaceHeight = 640.0;
            }

            _scale = Math.Min(_surfaceWidth / 1600.0, _surfaceHeight / 900.0);

            _window = new Window();
            _window.Title = "Antigravity";
            _window.WindowStyle = WindowStyle.None;
            _window.ResizeMode = ResizeMode.NoResize;
            _window.ShowInTaskbar = false;
            _window.Topmost = true;
            _window.UseLayoutRounding = true;
            _window.SnapsToDevicePixels = true;

            if (_fullscreenMode)
            {
                _window.AllowsTransparency = true;
                _window.Background = Brushes.Transparent;
                _window.Left = 0.0;
                _window.Top = 0.0;
                _window.Width = _surfaceWidth;
                _window.Height = _surfaceHeight;
                _window.WindowStartupLocation = WindowStartupLocation.Manual;

                _window.Loaded += delegate(object sender, RoutedEventArgs args)
                {
                    PresentationSource src = PresentationSource.FromVisual(_window);
                    if (src != null && src.CompositionTarget != null && pxW > 0 && pxH > 0)
                    {
                        Matrix m = src.CompositionTarget.TransformFromDevice;
                        Point dipPt = m.Transform(new Point(pxW, pxH));
                        double exactW = Math.Ceiling(dipPt.X);
                        double exactH = Math.Ceiling(dipPt.Y);
                        if (Math.Abs(exactW - _surfaceWidth) > 1.0 || Math.Abs(exactH - _surfaceHeight) > 1.0)
                        {
                            _surfaceWidth = exactW;
                            _surfaceHeight = exactH;
                            _scale = Math.Min(_surfaceWidth / 1600.0, _surfaceHeight / 900.0);
                            _window.Width = exactW;
                            _window.Height = exactH;
                            _rootGrid.Width = exactW;
                            _rootGrid.Height = exactH;
                            _surfaceBorder.Width = exactW;
                            _surfaceBorder.Height = exactH;
                            _surfaceContent.Width = exactW;
                            _surfaceContent.Height = exactH;
                            _cinematicHost.Resize(exactW, exactH, _scale, resLabel);
                        }
                    }
                };
            }
            else
            {
                _window.AllowsTransparency = true;
                _window.Background = Brushes.Transparent;
                _window.Width = _surfaceWidth + 80.0;
                _window.Height = _surfaceHeight + 80.0;
                _window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            _window.KeyDown += OnWindowKeyDown;

            bool isCapture = !string.IsNullOrEmpty(_captureDir) || !string.IsNullOrEmpty(_captureGifPath);
            _rootGrid = new Grid();
            _rootGrid.Width = _fullscreenMode ? _surfaceWidth : (_surfaceWidth + 80.0);
            _rootGrid.Height = _fullscreenMode ? _surfaceHeight : (_surfaceHeight + 80.0);
            _rootGrid.Background = isCapture ? (Brush)(new SolidColorBrush(ColorFromHex("#020307"))) : Brushes.Transparent;

            _surfaceBorder = new Border();
            _surfaceBorder.Width = _surfaceWidth;
            _surfaceBorder.Height = _surfaceHeight;
            _surfaceBorder.CornerRadius = _fullscreenMode ? new CornerRadius(0) : new CornerRadius(22);
            _surfaceBorder.ClipToBounds = true;
            _surfaceBorder.HorizontalAlignment = HorizontalAlignment.Center;
            _surfaceBorder.VerticalAlignment = VerticalAlignment.Center;
            _surfaceBorder.Background = Brushes.Black;
            _surfaceBorder.RenderTransformOrigin = new Point(0.5, 0.5);
            _surfaceScale = new ScaleTransform(1.0, 1.0);
            _surfaceBorder.RenderTransform = _surfaceScale;

            if (!_fullscreenMode)
            {
                _surfaceBorder.BorderThickness = new Thickness(1);
                _surfaceBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(70, 186, 230, 253));
                DropShadowEffect shadow = new DropShadowEffect();
                shadow.Color = Colors.Black;
                shadow.BlurRadius = 38;
                shadow.ShadowDepth = 12;
                shadow.Opacity = 0.75;
                _surfaceBorder.Effect = shadow;
            }

            _surfaceContent = new Grid();
            _surfaceContent.Width = _surfaceWidth;
            _surfaceContent.Height = _surfaceHeight;
            _surfaceContent.ClipToBounds = true;

            // Layer A: DrawingVisual Procedural Graphics Engine
            _cinematicHost = new CinematicVisualHost(_surfaceWidth, _surfaceHeight, _scale, _previewMode, resLabel);
            _surfaceContent.Children.Add(_cinematicHost);

            // Layer C: Hero Original Antigravity Icon Stage
            BuildHeroIconStage(_surfaceContent);

            // Layer D: Apple Keynote Kinetic Typography & Telemetry Bar
            BuildTypographyAndTelemetry(_surfaceContent);

            _surfaceBorder.Child = _surfaceContent;
            _rootGrid.Children.Add(_surfaceBorder);
            _window.Content = _rootGrid;
        }

        private void BuildHeroIconStage(Grid host)
        {
            double iconSize = Math.Round(212.0 * _scale);
            double stageSize = Math.Round(340.0 * _scale);
            double topOffset = Math.Round(-156.0 * _scale);

            _heroIconStage = new Grid();
            _heroIconStage.Width = stageSize;
            _heroIconStage.Height = stageSize + Math.Round(120.0 * _scale);
            _heroIconStage.HorizontalAlignment = HorizontalAlignment.Center;
            _heroIconStage.VerticalAlignment = VerticalAlignment.Center;
            _heroIconStage.Margin = new Thickness(0, topOffset, 0, 0);
            _heroIconStage.RenderTransformOrigin = new Point(0.5, 0.42);
            _heroStageTranslate = new TranslateTransform(0.0, 0.0);
            _heroStageScale = new ScaleTransform(1.0, 1.0);
            TransformGroup stageTf = new TransformGroup();
            stageTf.Children.Add(_heroStageScale);
            stageTf.Children.Add(_heroStageTranslate);
            _heroIconStage.RenderTransform = stageTf;
            _heroIconStage.Opacity = 0.0;

            // Multi-spectral backlight radial bloom
            _heroBackGlow = new Ellipse();
            _heroBackGlow.Width = stageSize * 1.15;
            _heroBackGlow.Height = stageSize * 1.15;
            _heroBackGlow.HorizontalAlignment = HorizontalAlignment.Center;
            _heroBackGlow.VerticalAlignment = VerticalAlignment.Top;
            _heroBackGlow.Margin = new Thickness(0, -stageSize * 0.075, 0, 0);
            _heroBackGlow.RenderTransformOrigin = new Point(0.5, 0.5);
            _heroBackGlowScale = new ScaleTransform(0.7, 0.7);
            _heroBackGlow.RenderTransform = _heroBackGlowScale;
            RadialGradientBrush glowBrush = new RadialGradientBrush();
            glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(145, 56, 189, 248), 0.0));
            glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(72, 99, 102, 241), 0.34));
            glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(28, 244, 114, 182), 0.62));
            glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 10, 16, 32), 1.0));
            glowBrush.Freeze();
            _heroBackGlow.Fill = glowBrush;
            _heroIconStage.Children.Add(_heroBackGlow);

            // Liquid-glass pedestal aura ring around the squircle
            double ringSize = iconSize * 1.12;
            _pedestalRing = new Border();
            _pedestalRing.Width = ringSize;
            _pedestalRing.Height = ringSize;
            _pedestalRing.CornerRadius = new CornerRadius(ringSize * 0.24);
            _pedestalRing.HorizontalAlignment = HorizontalAlignment.Center;
            _pedestalRing.VerticalAlignment = VerticalAlignment.Top;
            _pedestalRing.Margin = new Thickness(0, (stageSize - ringSize) * 0.5, 0, 0);
            _pedestalRing.BorderThickness = new Thickness(1.25 * _scale);
            LinearGradientBrush ringStroke = new LinearGradientBrush();
            ringStroke.StartPoint = new Point(0, 0);
            ringStroke.EndPoint = new Point(1, 1);
            ringStroke.GradientStops.Add(new GradientStop(Color.FromArgb(165, 255, 255, 255), 0.0));
            ringStroke.GradientStops.Add(new GradientStop(Color.FromArgb(70, 56, 189, 248), 0.45));
            ringStroke.GradientStops.Add(new GradientStop(Color.FromArgb(120, 167, 139, 250), 1.0));
            ringStroke.Freeze();
            _pedestalRing.BorderBrush = ringStroke;
            _pedestalRing.Background = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255));
            _pedestalRing.RenderTransformOrigin = new Point(0.5, 0.5);
            _pedestalScale = new ScaleTransform(1.0, 1.0);
            _pedestalRing.RenderTransform = _pedestalScale;
            _heroIconStage.Children.Add(_pedestalRing);

            // Pre-baked Mirrored Studio Floor Reflection below the icon (zero OpacityMask overhead)
            if (_originalIcon != null)
            {
                BitmapSource refBmp = IconAssetLoader.CreateFloorReflection(_originalIcon);
                _floorReflectionImg = new Image();
                _floorReflectionImg.Source = refBmp;
                _floorReflectionImg.Width = iconSize;
                _floorReflectionImg.Height = iconSize * 0.40;
                _floorReflectionImg.Stretch = Stretch.Fill;
                _floorReflectionImg.HorizontalAlignment = HorizontalAlignment.Center;
                _floorReflectionImg.VerticalAlignment = VerticalAlignment.Top;
                _floorReflectionImg.Margin = new Thickness(0, (stageSize - iconSize) * 0.5 + iconSize * 0.92, 0, 0);
                RenderOptions.SetBitmapScalingMode(_floorReflectionImg, BitmapScalingMode.HighQuality);
                _floorReflectionImg.Opacity = 0.0;
                _heroIconStage.Children.Add(_floorReflectionImg);
            }

            // Icon + Dispersion Ghosts + Clipped Specular Sweep container
            _mainIconContainer = new Grid();
            _mainIconContainer.Width = iconSize;
            _mainIconContainer.Height = iconSize;
            _mainIconContainer.HorizontalAlignment = HorizontalAlignment.Center;
            _mainIconContainer.VerticalAlignment = VerticalAlignment.Top;
            _mainIconContainer.Margin = new Thickness(0, (stageSize - iconSize) * 0.5, 0, 0);
            _mainIconContainer.RenderTransformOrigin = new Point(0.5, 0.5);

            _mainIconScale = new ScaleTransform(1.0, 1.0);
            _mainIconTranslate = new TranslateTransform(0.0, 0.0);
            TransformGroup iconTf = new TransformGroup();
            iconTf.Children.Add(_mainIconScale);
            iconTf.Children.Add(_mainIconTranslate);
            _mainIconContainer.RenderTransform = iconTf;

            if (_originalIcon != null)
            {
                // Chromatic Ghost 1: Ice-Cyan
                BitmapSource cyanBmp = IconAssetLoader.CreateTintedGhost(_originalIcon, ColorFromHex("#38BDF8"));
                _cyanGhostImg = new Image();
                _cyanGhostImg.Source = cyanBmp;
                _cyanGhostImg.Width = iconSize;
                _cyanGhostImg.Height = iconSize;
                RenderOptions.SetBitmapScalingMode(_cyanGhostImg, BitmapScalingMode.HighQuality);
                _cyanGhostTranslate = new TranslateTransform(0.0, 0.0);
                _cyanGhostImg.RenderTransform = _cyanGhostTranslate;
                _cyanGhostImg.Opacity = 0.0;
                _mainIconContainer.Children.Add(_cyanGhostImg);

                // Chromatic Ghost 2: Prism-Coral / Magenta
                BitmapSource magBmp = IconAssetLoader.CreateTintedGhost(_originalIcon, ColorFromHex("#F472B6"));
                _magentaGhostImg = new Image();
                _magentaGhostImg.Source = magBmp;
                _magentaGhostImg.Width = iconSize;
                _magentaGhostImg.Height = iconSize;
                RenderOptions.SetBitmapScalingMode(_magentaGhostImg, BitmapScalingMode.HighQuality);
                _magentaGhostTranslate = new TranslateTransform(0.0, 0.0);
                _magentaGhostImg.RenderTransform = _magentaGhostTranslate;
                _magentaGhostImg.Opacity = 0.0;
                _mainIconContainer.Children.Add(_magentaGhostImg);

                // Soft radial ambient shadow behind icon (replaces DropShadowEffect)
                Ellipse iconAmbientShadow = new Ellipse();
                iconAmbientShadow.Width = iconSize * 1.12;
                iconAmbientShadow.Height = iconSize * 1.12;
                iconAmbientShadow.HorizontalAlignment = HorizontalAlignment.Center;
                iconAmbientShadow.VerticalAlignment = VerticalAlignment.Center;
                iconAmbientShadow.Margin = new Thickness(0, 12.0 * _scale, 0, 0);
                RadialGradientBrush shadowBrush = new RadialGradientBrush();
                shadowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(145, 2, 6, 23), 0.45));
                shadowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(65, 2, 6, 23), 0.72));
                shadowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 2, 6, 23), 1.0));
                shadowBrush.Freeze();
                iconAmbientShadow.Fill = shadowBrush;
                _mainIconContainer.Children.Add(iconAmbientShadow);

                // Main Authentic 512x512 Antigravity Icon
                _mainIconImg = new Image();
                _mainIconImg.Source = _originalIcon;
                _mainIconImg.Width = iconSize;
                _mainIconImg.Height = iconSize;
                RenderOptions.SetBitmapScalingMode(_mainIconImg, BitmapScalingMode.HighQuality);
                _mainIconContainer.Children.Add(_mainIconImg);

                // Diagonal Specular Light Sweep clipped strictly to the icon squircle (no OpacityMask)
                _specularHost = new Grid();
                _specularHost.Width = iconSize;
                _specularHost.Height = iconSize;
                _specularHost.ClipToBounds = true;
                _specularHost.Clip = new RectangleGeometry(
                    new Rect(iconSize * 0.095, iconSize * 0.095, iconSize * 0.81, iconSize * 0.81),
                    iconSize * 0.18,
                    iconSize * 0.18);
                _specularHost.IsHitTestVisible = false;
                _specularHost.Visibility = Visibility.Collapsed;

                _specularBar = new Rectangle();
                _specularBar.Width = iconSize * 0.52;
                _specularBar.Height = iconSize * 1.9;
                _specularBar.HorizontalAlignment = HorizontalAlignment.Left;
                _specularBar.VerticalAlignment = VerticalAlignment.Top;
                LinearGradientBrush sheen = new LinearGradientBrush();
                sheen.StartPoint = new Point(0, 0.5);
                sheen.EndPoint = new Point(1, 0.5);
                sheen.GradientStops.Add(new GradientStop(Color.FromArgb(0, 56, 189, 248), 0.0));
                sheen.GradientStops.Add(new GradientStop(Color.FromArgb(115, 56, 189, 248), 0.30));
                sheen.GradientStops.Add(new GradientStop(Color.FromArgb(185, 224, 242, 254), 0.50));
                sheen.GradientStops.Add(new GradientStop(Color.FromArgb(110, 167, 139, 250), 0.70));
                sheen.GradientStops.Add(new GradientStop(Color.FromArgb(0, 167, 139, 250), 1.0));
                sheen.Freeze();
                _specularBar.Fill = sheen;

                _specularTranslate = new TranslateTransform(-iconSize * 1.0, -iconSize * 0.45);
                TransformGroup specTf = new TransformGroup();
                specTf.Children.Add(_specularTranslate);
                specTf.Children.Add(new RotateTransform(-28.0, iconSize * 0.5, iconSize * 0.5));
                _specularBar.RenderTransform = specTf;
                _specularBar.Opacity = 0.0;
                _specularHost.Children.Add(_specularBar);
                _mainIconContainer.Children.Add(_specularHost);
            }

            _heroIconStage.Children.Add(_mainIconContainer);
            host.Children.Add(_heroIconStage);
        }

        private void BuildTypographyAndTelemetry(Grid host)
        {
            FontFamily displayFont = new FontFamily("Segoe UI Variable Display, Segoe UI, Arial");
            FontFamily monoFont = new FontFamily("Consolas, Cascadia Mono, Segoe UI");

            // Act 1 Prologue: "REIMAGINING GRAVITY"
            _prologueHost = new Grid();
            _prologueHost.HorizontalAlignment = HorizontalAlignment.Center;
            _prologueHost.VerticalAlignment = VerticalAlignment.Center;
            _prologueHost.Margin = new Thickness(0, -18.0 * _scale, 0, 0);
            _prologueTranslate = new TranslateTransform(0.0, 0.0);
            _prologueHost.RenderTransform = _prologueTranslate;
            _prologueHost.Opacity = 0.0;

            _prologueText = new TextBlock();
            _prologueText.Text = "REIMAGINING  GRAVITY";
            _prologueText.FontFamily = displayFont;
            _prologueText.FontSize = 26.0 * _scale;
            _prologueText.FontWeight = FontWeights.Light;
            _prologueText.Foreground = new SolidColorBrush(ColorFromHex("#E2E8F0"));
            _prologueHost.Children.Add(_prologueText);
            host.Children.Add(_prologueHost);

            // Act 2 Callout: "ENGINEERED FOR ZERO-G INTELLIGENCE"
            _calloutHost = new Grid();
            _calloutHost.HorizontalAlignment = HorizontalAlignment.Center;
            _calloutHost.VerticalAlignment = VerticalAlignment.Center;
            _calloutHost.Margin = new Thickness(0, -18.0 * _scale, 0, 0);
            _calloutHost.RenderTransformOrigin = new Point(0.5, 0.5);
            _calloutScale = new ScaleTransform(1.0, 1.0);
            _calloutHost.RenderTransform = _calloutScale;
            _calloutHost.Opacity = 0.0;

            _calloutText = new TextBlock();
            _calloutText.Text = "ENGINEERED  FOR  ZERO-G  INTELLIGENCE";
            _calloutText.FontFamily = displayFont;
            _calloutText.FontSize = 23.0 * _scale;
            _calloutText.FontWeight = FontWeights.Regular;
            _calloutText.Foreground = new SolidColorBrush(ColorFromHex("#F1F5F9"));
            _calloutHost.Children.Add(_calloutText);
            host.Children.Add(_calloutHost);

            // Act 4-5 Hero Wordmark: "ANTIGRAVITY" (per-glyph animated tracking)
            _wordmarkCanvasWidth = 860.0 * _scale;
            _wordmarkHost = new Grid();
            _wordmarkHost.Width = _wordmarkCanvasWidth;
            _wordmarkHost.Height = 68.0 * _scale;
            _wordmarkHost.HorizontalAlignment = HorizontalAlignment.Center;
            _wordmarkHost.VerticalAlignment = VerticalAlignment.Center;
            _wordmarkHost.Margin = new Thickness(0, 152.0 * _scale, 0, 0);
            _wordmarkTranslate = new TranslateTransform(0.0, 18.0 * _scale);
            _wordmarkHost.RenderTransform = _wordmarkTranslate;
            _wordmarkHost.Opacity = 0.0;

            Canvas wordmarkCanvas = new Canvas();
            wordmarkCanvas.Width = _wordmarkCanvasWidth;
            wordmarkCanvas.Height = 68.0 * _scale;
            _wordmarkHost.Children.Add(wordmarkCanvas);

            _wordmarkGlyphs = new List<TextBlock>();
            _glyphBaseWidths = new List<double>();
            string title = "ANTIGRAVITY";

            LinearGradientBrush titleBrush = new LinearGradientBrush();
            titleBrush.StartPoint = new Point(0.5, 0.0);
            titleBrush.EndPoint = new Point(0.5, 1.0);
            titleBrush.GradientStops.Add(new GradientStop(ColorFromHex("#FFFFFF"), 0.0));
            titleBrush.GradientStops.Add(new GradientStop(ColorFromHex("#E2E8F0"), 0.65));
            titleBrush.GradientStops.Add(new GradientStop(ColorFromHex("#94A3B8"), 1.0));
            titleBrush.Freeze();

            for (int i = 0; i < title.Length; i++)
            {
                TextBlock tb = new TextBlock();
                tb.Text = title[i].ToString();
                tb.FontFamily = displayFont;
                tb.FontSize = 42.0 * _scale;
                tb.FontWeight = FontWeights.SemiBold;
                tb.Foreground = titleBrush;
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                _wordmarkGlyphs.Add(tb);
                _glyphBaseWidths.Add(tb.DesiredSize.Width);
                wordmarkCanvas.Children.Add(tb);
            }
            LayoutWordmarkGlyphs(24.0 * _scale);
            host.Children.Add(_wordmarkHost);

            // Subtitle: "AGENTIC DEVELOPMENT ENVIRONMENT"
            _subtitleText = new TextBlock();
            _subtitleText.Text = "A G E N T I C   D E V E L O P M E N T   E N V I R O N M E N T";
            _subtitleText.FontFamily = displayFont;
            _subtitleText.FontSize = 11.5 * _scale;
            _subtitleText.FontWeight = FontWeights.Light;
            _subtitleText.Foreground = new SolidColorBrush(ColorFromHex("#94A3B8"));
            _subtitleText.HorizontalAlignment = HorizontalAlignment.Center;
            _subtitleText.VerticalAlignment = VerticalAlignment.Center;
            _subtitleText.Margin = new Thickness(0, 232.0 * _scale, 0, 0);
            _subtitleTranslate = new TranslateTransform(0.0, 12.0 * _scale);
            _subtitleText.RenderTransform = _subtitleTranslate;
            _subtitleText.Opacity = 0.0;
            host.Children.Add(_subtitleText);

            // Sleek Hairline Progress Bar + Gravity Readout
            _barWidth = Math.Round(290.0 * _scale);
            _telemetryHost = new Grid();
            _telemetryHost.Width = 480.0 * _scale;
            _telemetryHost.Height = 58.0 * _scale;
            _telemetryHost.HorizontalAlignment = HorizontalAlignment.Center;
            _telemetryHost.VerticalAlignment = VerticalAlignment.Center;
            _telemetryHost.Margin = new Thickness(0, 316.0 * _scale, 0, 0);
            _telemetryTranslate = new TranslateTransform(0.0, 10.0 * _scale);
            _telemetryHost.RenderTransform = _telemetryTranslate;
            _telemetryHost.Opacity = 0.0;

            StackPanel telStack = new StackPanel();
            telStack.Orientation = Orientation.Vertical;
            telStack.HorizontalAlignment = HorizontalAlignment.Center;

            Grid barContainer = new Grid();
            barContainer.Width = _barWidth;
            barContainer.Height = 10.0 * _scale;
            barContainer.HorizontalAlignment = HorizontalAlignment.Center;
            barContainer.Margin = new Thickness(0, 0, 0, 10.0 * _scale);

            Rectangle barTrack = new Rectangle();
            barTrack.Width = _barWidth;
            barTrack.Height = 1.2 * _scale;
            barTrack.VerticalAlignment = VerticalAlignment.Center;
            barTrack.Fill = new SolidColorBrush(Color.FromArgb(42, 148, 163, 184));
            barContainer.Children.Add(barTrack);

            _hairlineFill = new Rectangle();
            _hairlineFill.Width = _barWidth;
            _hairlineFill.Height = 2.0 * _scale;
            _hairlineFill.HorizontalAlignment = HorizontalAlignment.Left;
            _hairlineFill.VerticalAlignment = VerticalAlignment.Center;
            _hairlineFill.RenderTransformOrigin = new Point(0.0, 0.5);
            _hairlineScale = new ScaleTransform(0.0, 1.0);
            _hairlineFill.RenderTransform = _hairlineScale;
            LinearGradientBrush barBrush = new LinearGradientBrush();
            barBrush.StartPoint = new Point(0, 0.5);
            barBrush.EndPoint = new Point(1, 0.5);
            barBrush.GradientStops.Add(new GradientStop(ColorFromHex("#38BDF8"), 0.0));
            barBrush.GradientStops.Add(new GradientStop(ColorFromHex("#F8FAFC"), 0.5));
            barBrush.GradientStops.Add(new GradientStop(ColorFromHex("#A78BFA"), 1.0));
            barBrush.Freeze();
            _hairlineFill.Fill = barBrush;
            barContainer.Children.Add(_hairlineFill);

            _hairlineTipGlow = new Ellipse();
            _hairlineTipGlow.Width = 10.0 * _scale;
            _hairlineTipGlow.Height = 10.0 * _scale;
            _hairlineTipGlow.HorizontalAlignment = HorizontalAlignment.Left;
            _hairlineTipGlow.VerticalAlignment = VerticalAlignment.Center;
            RadialGradientBrush tipBrush = new RadialGradientBrush();
            tipBrush.GradientStops.Add(new GradientStop(Color.FromArgb(245, 255, 255, 255), 0.0));
            tipBrush.GradientStops.Add(new GradientStop(Color.FromArgb(140, 56, 189, 248), 0.45));
            tipBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 56, 189, 248), 1.0));
            tipBrush.Freeze();
            _hairlineTipGlow.Fill = tipBrush;
            _hairlineTipTranslate = new TranslateTransform(-5.0 * _scale, 0.0);
            _hairlineTipGlow.RenderTransform = _hairlineTipTranslate;
            barContainer.Children.Add(_hairlineTipGlow);

            telStack.Children.Add(barContainer);

            _telemetryText = new TextBlock();
            _telemetryText.Text = "G = 9.81 m/s\u00B2  \u00B7  CALIBRATING";
            _telemetryText.FontFamily = monoFont;
            _telemetryText.FontSize = 10.5 * _scale;
            _telemetryText.Foreground = new SolidColorBrush(ColorFromHex("#94A3B8"));
            _telemetryText.HorizontalAlignment = HorizontalAlignment.Center;
            telStack.Children.Add(_telemetryText);

            _telemetryHost.Children.Add(telStack);
            host.Children.Add(_telemetryHost);
        }

        private void LayoutWordmarkGlyphs(double tracking)
        {
            double totalWidth = 0.0;
            for (int i = 0; i < _glyphBaseWidths.Count; i++)
            {
                totalWidth += _glyphBaseWidths[i];
                if (i < _glyphBaseWidths.Count - 1) totalWidth += tracking;
            }

            double x = (_wordmarkCanvasWidth - totalWidth) * 0.5;
            for (int i = 0; i < _wordmarkGlyphs.Count; i++)
            {
                TextBlock tb = _wordmarkGlyphs[i];
                Canvas.SetLeft(tb, x);
                Canvas.SetTop(tb, 6.0 * _scale);
                x += _glyphBaseWidths[i] + tracking;
            }
        }

        private ImageBrush CreateFilmGrainBrush()
        {
            int size = 128;
            WriteableBitmap wb = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
            byte[] pixels = new byte[size * size * 4];
            Random rng = new Random(42077);

            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte v = (byte)rng.Next(125, 255);
                byte a = (byte)rng.Next(0, 7);
                pixels[i] = v;
                pixels[i + 1] = v;
                pixels[i + 2] = v;
                pixels[i + 3] = a;
            }

            wb.WritePixels(new Int32Rect(0, 0, size, size), pixels, size * 4, 0);
            wb.Freeze();

            ImageBrush brush = new ImageBrush(wb);
            brush.TileMode = TileMode.Tile;
            brush.Viewport = new Rect(0, 0, size, size);
            brush.ViewportUnits = BrushMappingMode.Absolute;
            brush.Freeze();
            return brush;
        }

        private void StartPlayback()
        {
            _launchedTarget = false;
            _soundStarted = false;
            _finished = false;
            _paused = false;
            _manualTime = 0.0;
            _stopwatch = Stopwatch.StartNew();

            RenderFrame(0.0);
            _window.Show();

            if (!_isRenderingHooked)
            {
                CompositionTarget.Rendering += OnCompositionRendering;
                _isRenderingHooked = true;
            }
        }

        private void OnCompositionRendering(object sender, EventArgs e)
        {
            if (_finished) return;

            double t;
            if (_paused)
            {
                t = _manualTime;
            }
            else
            {
                t = _stopwatch.Elapsed.TotalSeconds * _timeScale;
                _manualTime = t;
            }

            if (!_launchedTarget && !_previewMode && t >= 8.0)
            {
                _launchedTarget = true;
                LaunchAntigravityProcess();
            }

            if (_soundMode && !_soundStarted && _audioReady && t >= 0.02)
            {
                _soundStarted = true;
                PlayAudioCue();
            }

            if (t >= TotalDuration)
            {
                RenderFrame(TotalDuration);
                if (_previewMode)
                {
                    _finished = true;
                    return;
                }

                StopRendering();
                _window.Close();
                CleanupAudio();
                Shutdown(0);
                return;
            }

            RenderFrame(t);
        }

        private void RenderFrame(double t)
        {
            t = Clamp(t, 0.0, TotalDuration);

            // 1. Smooth Fade-In (0.0 - 0.48s) & Portal Exit Fade-Out (13.85 - 15.0s)
            double enterEase = EaseOutCubic(SmoothStep(0.0, 0.48, t));
            double exitEase = EaseInOutCubic(SmoothStep(13.85, 15.0, t));
            double masterAlpha = enterEase * (1.0 - exitEase);

            _surfaceBorder.Opacity = masterAlpha;
            if (!_fullscreenMode)
            {
                double cardScale = Lerp(0.96, 1.0, enterEase) + exitEase * 0.03;
                _surfaceScale.ScaleX = cardScale;
                _surfaceScale.ScaleY = cardScale;
            }

            // 2. Render GPU DrawingVisual Scene
            _cinematicHost.Render(t, TotalDuration);

            // 3. Act 1 Prologue: "REIMAGINING GRAVITY" (0.90s - 3.25s)
            if (t >= 0.85 && t <= 3.30)
            {
                if (_prologueHost.Visibility != Visibility.Visible) _prologueHost.Visibility = Visibility.Visible;
                double fadeIn = SmoothStep(0.85, 1.55, t);
                double fadeOut = 1.0 - SmoothStep(2.55, 3.25, t);
                _prologueHost.Opacity = fadeIn * fadeOut;
                _prologueTranslate.Y = Lerp(12.0 * _scale, 0.0, EaseOutCubic(fadeIn)) - (1.0 - fadeOut) * 8.0 * _scale;
            }
            else
            {
                if (_prologueHost.Visibility != Visibility.Collapsed) _prologueHost.Visibility = Visibility.Collapsed;
            }

            // 4. Act 2 Callout: "ENGINEERED FOR ZERO-G INTELLIGENCE" (3.30s - 5.30s)
            if (t >= 3.25 && t <= 5.35)
            {
                if (_calloutHost.Visibility != Visibility.Visible) _calloutHost.Visibility = Visibility.Visible;
                double fadeIn = SmoothStep(3.25, 3.85, t);
                double pullIn = SmoothStep(4.75, 5.32, t);
                _calloutHost.Opacity = fadeIn * (1.0 - pullIn);
                double sc = Lerp(0.97, 1.0, EaseOutCubic(fadeIn)) * Lerp(1.0, 0.86, EaseInCubic(pullIn));
                _calloutScale.ScaleX = sc;
                _calloutScale.ScaleY = sc;
            }
            else
            {
                if (_calloutHost.Visibility != Visibility.Collapsed) _calloutHost.Visibility = Visibility.Collapsed;
            }

            // 5. Act 3-6 Hero Original Antigravity Icon Stage (5.45s - 15.0s)
            if (t >= 5.45)
            {
                if (_heroIconStage.Visibility != Visibility.Visible) _heroIconStage.Visibility = Visibility.Visible;
                double riseP = Clamp01((t - 5.45) / 1.45);
                double riseEase = EaseOutExpo(riseP);

                double springOvershoot = 0.0;
                if (t < 7.6)
                {
                    double dt = t - 5.45;
                    springOvershoot = -Math.Exp(-dt * 4.2) * Math.Sin(dt * 7.8) * 14.0 * _scale;
                }

                double hoverY = 0.0;
                if (t >= 7.2)
                {
                    double ht = t - 7.2;
                    double hoverAmp = SmoothStep(7.2, 8.2, t);
                    hoverY = Math.Sin(ht * 1.95) * 4.5 * _scale * hoverAmp;
                }

                double iconOpacity = SmoothStep(5.45, 5.95, t);
                double iconScale = Lerp(0.72, 1.0, riseEase) + exitEase * 0.06;

                _heroIconStage.Opacity = iconOpacity;
                _heroStageTranslate.Y = Lerp(74.0 * _scale, 0.0, riseEase) + springOvershoot + hoverY;
                _heroStageScale.ScaleX = iconScale;
                _heroStageScale.ScaleY = iconScale;

                // Breathing backlight & pedestal ring
                double breathe = 1.0 + 0.06 * Math.Sin(Math.Max(0.0, t - 6.8) * 1.8);
                _heroBackGlowScale.ScaleX = (0.75 + 0.30 * riseEase) * breathe;
                _heroBackGlowScale.ScaleY = (0.75 + 0.30 * riseEase) * breathe;
                _heroBackGlow.Opacity = SmoothStep(5.55, 6.8, t) * (0.85 + 0.15 * Math.Sin(t * 2.1));

                double pedIn = SmoothStep(6.0, 7.3, t);
                _pedestalRing.Opacity = pedIn * (0.78 - 0.18 * SmoothStep(11.6, 13.0, t));
                _pedestalScale.ScaleX = 0.94 + 0.06 * pedIn;
                _pedestalScale.ScaleY = 0.94 + 0.06 * pedIn;

                if (_floorReflectionImg != null)
                {
                    double refOp = SmoothStep(6.4, 7.8, t) * 0.32 * (1.0 - exitEase);
                    if (refOp <= 0.002)
                    {
                        if (_floorReflectionImg.Visibility != Visibility.Collapsed) _floorReflectionImg.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        if (_floorReflectionImg.Visibility != Visibility.Visible) _floorReflectionImg.Visibility = Visibility.Visible;
                        _floorReflectionImg.Opacity = refOp;
                    }
                }

                // Chromatic Dispersion Ghosts (peaks at t=5.9s, snaps cleanly to 0 by t=7.55s)
                if (_cyanGhostImg != null && _magentaGhostImg != null)
                {
                    double dispWin = 0.0;
                    if (t >= 5.45 && t <= 7.55)
                    {
                        double u = (t - 5.45) / 2.10;
                        dispWin = Math.Sin(u * Math.PI) * (1.0 - u * 0.32);
                    }
                    double dispAlpha = dispWin * 0.65;
                    if (dispAlpha <= 0.002)
                    {
                        if (_cyanGhostImg.Visibility != Visibility.Collapsed) _cyanGhostImg.Visibility = Visibility.Collapsed;
                        if (_magentaGhostImg.Visibility != Visibility.Collapsed) _magentaGhostImg.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        if (_cyanGhostImg.Visibility != Visibility.Visible) _cyanGhostImg.Visibility = Visibility.Visible;
                        if (_magentaGhostImg.Visibility != Visibility.Visible) _magentaGhostImg.Visibility = Visibility.Visible;
                        double dispPx = dispWin * 16.0 * _scale;
                        _cyanGhostTranslate.X = -dispPx * 0.55;
                        _cyanGhostTranslate.Y = -dispPx;
                        _magentaGhostTranslate.X = dispPx * 0.55;
                        _magentaGhostTranslate.Y = dispPx;
                        _cyanGhostImg.Opacity = dispAlpha;
                        _magentaGhostImg.Opacity = dispAlpha;
                    }
                }

                // Dual Clipped Specular Light Sweeps across the Original Icon (7.35-8.45s & 11.45-12.55s)
                if (_specularBar != null && _specularHost != null)
                {
                    double iconSize = _mainIconContainer.Width;
                    double s1 = (t >= 7.35 && t <= 8.45) ? (t - 7.35) / 1.10 : -1.0;
                    double s2 = (t >= 11.45 && t <= 12.55) ? (t - 11.45) / 1.10 : -1.0;
                    double sp = s2 >= 0.0 ? s2 : s1;
                    if (sp >= 0.0 && sp <= 1.0)
                    {
                        if (_specularHost.Visibility != Visibility.Visible) _specularHost.Visibility = Visibility.Visible;
                        double se = EaseInOutQuad(sp);
                        _specularBar.Opacity = Math.Sin(se * Math.PI) * 0.88;
                        _specularTranslate.X = Lerp(-iconSize * 0.75, iconSize * 1.35, se);
                    }
                    else
                    {
                        if (_specularHost.Visibility != Visibility.Collapsed) _specularHost.Visibility = Visibility.Collapsed;
                    }
                }
            }
            else
            {
                if (_heroIconStage.Visibility != Visibility.Collapsed) _heroIconStage.Visibility = Visibility.Collapsed;
            }

            // 6. Act 4-5 Hero Wordmark Reveal ("ANTIGRAVITY") (8.0s - 15.0s)
            if (t >= 8.0)
            {
                if (_wordmarkHost.Visibility != Visibility.Visible) _wordmarkHost.Visibility = Visibility.Visible;
                double wordP = Clamp01((t - 8.0) / 1.45);
                double wordEase = EaseOutExpo(wordP);
                _wordmarkHost.Opacity = SmoothStep(8.0, 8.85, t);
                _wordmarkTranslate.Y = Lerp(20.0 * _scale, 0.0, wordEase);
                double tracking = Lerp(24.0 * _scale, 11.0 * _scale, wordEase);
                if (Math.Abs(tracking - _lastWordmarkTracking) > 0.08)
                {
                    _lastWordmarkTracking = tracking;
                    LayoutWordmarkGlyphs(tracking);
                }
            }
            else
            {
                if (_wordmarkHost.Visibility != Visibility.Collapsed) _wordmarkHost.Visibility = Visibility.Collapsed;
            }

            // 7. Subtitle Reveal (8.65s - 15.0s)
            if (t >= 8.65)
            {
                if (_subtitleText.Visibility != Visibility.Visible) _subtitleText.Visibility = Visibility.Visible;
                double subP = EaseOutCubic(Clamp01((t - 8.65) / 1.1));
                _subtitleText.Opacity = subP * 0.92;
                _subtitleTranslate.Y = Lerp(12.0 * _scale, 0.0, subP);
            }
            else
            {
                if (_subtitleText.Visibility != Visibility.Collapsed) _subtitleText.Visibility = Visibility.Collapsed;
            }

            // 8. Hairline Progress Bar & Gravity Readout (8.3s - 15.0s)
            if (t >= 8.3)
            {
                if (_telemetryHost.Visibility != Visibility.Visible) _telemetryHost.Visibility = Visibility.Visible;
                double telFade = SmoothStep(8.3, 9.0, t);
                double barP = EaseOutCubic(Clamp01((t - 8.3) / 4.2));
                _telemetryHost.Opacity = telFade;
                _telemetryTranslate.Y = Lerp(10.0 * _scale, 0.0, EaseOutCubic(telFade));
                _hairlineScale.ScaleX = barP;
                _hairlineTipTranslate.X = barP * _barWidth - 5.0 * _scale;
                _hairlineTipGlow.Opacity = (barP > 0.01 && barP < 0.995) ? 0.95 : (1.0 - SmoothStep(12.5, 13.2, t)) * 0.55;

                double gP = EaseOutCubic(Clamp01((t - 8.3) / 3.1));
                string newTel;
                if (gP >= 0.995)
                {
                    newTel = "G = 0.00 m/s\u00B2  \u00B7  ZERO-G ENGINE READY";
                }
                else
                {
                    double gVal = Math.Round(Lerp(9.81, 0.00, gP), 2);
                    newTel = string.Format(CultureInfo.InvariantCulture, "G = {0:F2} m/s\u00B2  \u00B7  INVERTING GRAVITY FIELD", gVal);
                }
                if (!string.Equals(newTel, _lastTelemetryText, StringComparison.Ordinal))
                {
                    _lastTelemetryText = newTel;
                    _telemetryText.Text = newTel;
                }
            }
            else
            {
                if (_telemetryHost.Visibility != Visibility.Collapsed) _telemetryHost.Visibility = Visibility.Collapsed;
            }
        }

        private void LaunchAntigravityProcess()
        {
            try
            {
                if (string.IsNullOrEmpty(_targetExe) || !File.Exists(_targetExe)) return;

                StringBuilder argBuilder = new StringBuilder();
                argBuilder.Append("--remote-debugging-port=0");
                for (int i = 0; i < _forwardArgs.Count; i++)
                {
                    string arg = _forwardArgs[i];
                    if (string.Equals(arg, "--remote-debugging-port=0", StringComparison.OrdinalIgnoreCase)) continue;
                    argBuilder.Append(" ");
                    if (arg.IndexOf(' ') >= 0) argBuilder.Append("\"").Append(arg).Append("\"");
                    else argBuilder.Append(arg);
                }

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = _targetExe;
                psi.Arguments = argBuilder.ToString();
                if (!string.IsNullOrEmpty(_targetWorkDir) && Directory.Exists(_targetWorkDir))
                {
                    psi.WorkingDirectory = _targetWorkDir;
                }
                psi.UseShellExecute = false;
                Process.Start(psi);
            }
            catch
            {
            }
        }

        private void PrepareAudio()
        {
            double duration = TotalDuration;
            double scale = _timeScale;
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                try
                {
                    byte[] wav = AudioSynthesizer.GenerateSoundtrackWav(duration, scale);
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (_finished) return;
                        try
                        {
                            _soundStream = new MemoryStream(wav);
                            _soundPlayer = new SoundPlayer(_soundStream);
                            _soundPlayer.Load();
                            _audioReady = true;
                        }
                        catch
                        {
                            CleanupAudio();
                        }
                    }));
                }
                catch
                {
                }
            });
        }

        private void PlayAudioCue()
        {
            if (_soundPlayer != null)
            {
                try { _soundPlayer.Play(); } catch { }
            }
        }

        private void CleanupAudio()
        {
            if (_soundPlayer != null)
            {
                try { _soundPlayer.Stop(); _soundPlayer.Dispose(); } catch { }
                _soundPlayer = null;
            }
            if (_soundStream != null)
            {
                try { _soundStream.Dispose(); } catch { }
                _soundStream = null;
            }
        }

        private void RunCaptureMode()
        {
            Directory.CreateDirectory(_captureDir);

            _window.Opacity = 1.0;
            _window.Show();

            double[] times = new double[] { 1.80, 4.30, 5.65, 6.50, 9.80, 11.90, 13.10, 14.35 };
            string[] names = new string[]
            {
                "frame_01_void_prologue.png",
                "frame_02_silicon_convergence.png",
                "frame_03_inversion_burst.png",
                "frame_04_icon_dispersion.png",
                "frame_05_hud_online.png",
                "frame_06_specular_lockin.png",
                "frame_07_hero_finale.png",
                "frame_08_portal_handoff.png"
            };

            for (int i = 0; i < times.Length; i++)
            {
                RenderFrame(times[i]);
                _rootGrid.UpdateLayout();
                Dispatcher.Invoke(DispatcherPriority.Render, new Action(delegate { }));

                int pixelW = (int)Math.Round(_rootGrid.Width);
                int pixelH = (int)Math.Round(_rootGrid.Height);
                RenderTargetBitmap rtb = new RenderTargetBitmap(pixelW, pixelH, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(_rootGrid);

                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                string outPath = System.IO.Path.Combine(_captureDir, names[i]);
                using (FileStream fs = new FileStream(outPath, FileMode.Create, FileAccess.Write))
                {
                    encoder.Save(fs);
                }
            }

            _window.Close();
            Shutdown(0);
        }

        private void RunCaptureGifMode()
        {
            string parentDir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(_captureGifPath));
            if (!string.IsNullOrEmpty(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            _window.Opacity = 1.0;
            _window.Show();

            int gifW = 720;
            int gifH = 405;
            int pixelW = (int)Math.Round(_rootGrid.Width);
            int pixelH = (int)Math.Round(_rootGrid.Height);
            double scaleX = (double)gifW / pixelW;
            double scaleY = (double)gifH / pixelH;

            // Sample 36 key moments spanning all 6 acts (0.6s -> 14.3s)
            int frameCount = 36;
            GifBitmapEncoder encoder = new GifBitmapEncoder();

            for (int i = 0; i < frameCount; i++)
            {
                double t = 0.6 + (13.7 * i) / (frameCount - 1);
                RenderFrame(t);
                _rootGrid.UpdateLayout();
                Dispatcher.Invoke(DispatcherPriority.Render, new Action(delegate { }));

                RenderTargetBitmap rtb = new RenderTargetBitmap(pixelW, pixelH, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(_rootGrid);
                TransformedBitmap scaled = new TransformedBitmap(rtb, new ScaleTransform(scaleX, scaleY));
                encoder.Frames.Add(BitmapFrame.Create(scaled));
            }

            byte[] rawGif;
            using (MemoryStream ms = new MemoryStream())
            {
                encoder.Save(ms);
                rawGif = ms.ToArray();
            }

            byte[] loopedGif = PatchGifLoopAndDelay(rawGif, 15);
            File.WriteAllBytes(_captureGifPath, loopedGif);

            _window.Close();
            Shutdown(0);
        }

        private static byte[] PatchGifLoopAndDelay(byte[] src, int delayCs)
        {
            if (src == null || src.Length < 14) return src;

            // Locate end of Global Color Table after 13-byte GIF89a header + Logical Screen Descriptor
            int pos = 13;
            byte packed = src[10];
            if ((packed & 0x80) != 0)
            {
                int gctEntries = 1 << ((packed & 0x07) + 1);
                pos += gctEntries * 3;
            }
            if (pos >= src.Length) return src;

            byte[] netscapeExt = new byte[]
            {
                0x21, 0xFF, 0x0B,
                0x4E, 0x45, 0x54, 0x53, 0x43, 0x41, 0x50, 0x45, 0x32, 0x2E, 0x30,
                0x03, 0x01, 0x00, 0x00, 0x00
            };

            byte delayLo = (byte)(delayCs & 0xFF);
            byte delayHi = (byte)((delayCs >> 8) & 0xFF);

            using (MemoryStream outMs = new MemoryStream(src.Length + 512))
            {
                outMs.Write(src, 0, pos);
                outMs.Write(netscapeExt, 0, netscapeExt.Length);

                // Walk blocks and set frame delay in every Graphic Control Extension (0x21 0xF9 0x04)
                // or inject one right before any Image Descriptor (0x2C) lacking a preceding GCE.
                bool hasGce = false;
                while (pos < src.Length)
                {
                    byte b = src[pos];
                    if (b == 0x3B) // Trailer
                    {
                        outMs.WriteByte(0x3B);
                        break;
                    }
                    else if (b == 0x21) // Extension
                    {
                        if (pos + 2 < src.Length && src[pos + 1] == 0xF9 && src[pos + 2] == 0x04 && pos + 7 < src.Length)
                        {
                            outMs.WriteByte(0x21);
                            outMs.WriteByte(0xF9);
                            outMs.WriteByte(0x04);
                            outMs.WriteByte(0x04); // disposal = 1 (do not dispose), no transparent index
                            outMs.WriteByte(delayLo);
                            outMs.WriteByte(delayHi);
                            outMs.WriteByte(0x00);
                            outMs.WriteByte(0x00);
                            pos += 8;
                            hasGce = true;
                        }
                        else
                        {
                            outMs.WriteByte(b);
                            pos++;
                            if (pos < src.Length)
                            {
                                outMs.WriteByte(src[pos]);
                                pos++;
                            }
                            while (pos < src.Length)
                            {
                                int subLen = src[pos];
                                outMs.WriteByte((byte)subLen);
                                pos++;
                                if (subLen == 0) break;
                                int copyLen = Math.Min(subLen, src.Length - pos);
                                outMs.Write(src, pos, copyLen);
                                pos += copyLen;
                            }
                        }
                    }
                    else if (b == 0x2C) // Image Descriptor
                    {
                        if (!hasGce)
                        {
                            byte[] gce = new byte[] { 0x21, 0xF9, 0x04, 0x04, delayLo, delayHi, 0x00, 0x00 };
                            outMs.Write(gce, 0, gce.Length);
                        }
                        hasGce = false;

                        if (pos + 10 > src.Length) break;
                        byte imgPacked = src[pos + 9];
                        outMs.Write(src, pos, 10);
                        pos += 10;

                        if ((imgPacked & 0x80) != 0)
                        {
                            int lctBytes = (1 << ((imgPacked & 0x07) + 1)) * 3;
                            int copyLct = Math.Min(lctBytes, src.Length - pos);
                            outMs.Write(src, pos, copyLct);
                            pos += copyLct;
                        }

                        if (pos < src.Length)
                        {
                            outMs.WriteByte(src[pos]); // LZW minimum code size
                            pos++;
                        }
                        while (pos < src.Length)
                        {
                            int subLen = src[pos];
                            outMs.WriteByte((byte)subLen);
                            pos++;
                            if (subLen == 0) break;
                            int copyLen = Math.Min(subLen, src.Length - pos);
                            outMs.Write(src, pos, copyLen);
                            pos += copyLen;
                        }
                    }
                    else
                    {
                        outMs.WriteByte(b);
                        pos++;
                    }
                }

                return outMs.ToArray();
            }
        }

        private void RunBenchMode()
        {
            _window.Show();
            int frames = 0;
            Stopwatch benchSw = Stopwatch.StartNew();
            Stopwatch cpuSw = new Stopwatch();
            double cpuMs = 0.0;

            CompositionTarget.Rendering += delegate(object sender, EventArgs e)
            {
                double sec = benchSw.Elapsed.TotalSeconds;
                double simT = 8.5 + (sec % 3.0);
                cpuSw.Restart();
                RenderFrame(simT);
                cpuSw.Stop();
                cpuMs += cpuSw.Elapsed.TotalMilliseconds;
                frames++;

                if (sec >= 2.5)
                {
                    string summary = string.Format(
                        CultureInfo.InvariantCulture,
                        "FPS={0:F1} | AvgFrameMs={1:F2} | AvgCpuMs={2:F2} | Frames={3} | Sec={4:F2}",
                        frames / sec, (sec * 1000.0) / frames, cpuMs / frames, frames, sec);
                    File.WriteAllText(_benchLogPath, summary);
                    _window.Close();
                    Shutdown(0);
                }
            };
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || (!_previewMode && (e.Key == Key.Space || e.Key == Key.Enter)))
            {
                if (!_previewMode && !_launchedTarget)
                {
                    _launchedTarget = true;
                    LaunchAntigravityProcess();
                }
                StopRendering();
                _window.Close();
                CleanupAudio();
                Shutdown(0);
                return;
            }

            if (_previewMode)
            {
                if (e.Key == Key.R)
                {
                    _finished = false;
                    _paused = false;
                    _manualTime = 0.0;
                    _soundStarted = false;
                    _stopwatch.Restart();
                    if (_soundMode)
                    {
                        _soundStarted = true;
                        PlayAudioCue();
                    }
                }
                else if (e.Key == Key.Space)
                {
                    if (_paused)
                    {
                        _paused = false;
                        _finished = false;
                        // Resume stopwatch relative to manualTime
                        _stopwatch = Stopwatch.StartNew();
                    }
                    else
                    {
                        _paused = true;
                        _manualTime = Clamp(_stopwatch.Elapsed.TotalSeconds * _timeScale, 0.0, TotalDuration);
                    }
                }
                else if (e.Key == Key.Left || e.Key == Key.Right)
                {
                    _paused = true;
                    _finished = false;
                    double delta = (e.Key == Key.Right) ? 1.0 : -1.0;
                    _manualTime = Clamp(_manualTime + delta, 0.0, TotalDuration);
                    RenderFrame(_manualTime);
                }
            }
        }

        private void StopRendering()
        {
            if (_isRenderingHooked)
            {
                CompositionTarget.Rendering -= OnCompositionRendering;
                _isRenderingHooked = false;
            }
        }

        private static Color ColorFromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        private static double Clamp(double val, double min, double max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }

        private static double Clamp01(double val)
        {
            return Clamp(val, 0.0, 1.0);
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        private static double SmoothStep(double edge0, double edge1, double x)
        {
            double t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3.0 - 2.0 * t);
        }

        private static double EaseInCubic(double t) { return t * t * t; }
        private static double EaseOutCubic(double t) { return 1.0 - Math.Pow(1.0 - t, 3.0); }
        private static double EaseInOutQuad(double t) { return t < 0.5 ? 2.0 * t * t : 1.0 - Math.Pow(-2.0 * t + 2.0, 2.0) * 0.5; }
        private static double EaseInOutCubic(double t) { return t < 0.5 ? 4.0 * t * t * t : 1.0 - Math.Pow(-2.0 * t + 2.0, 3.0) * 0.5; }
        private static double EaseOutExpo(double t) { return t >= 1.0 ? 1.0 : 1.0 - Math.Pow(2.0, -10.0 * t); }
    }
}
