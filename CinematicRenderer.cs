using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AntigravityLauncher
{
    public class CinematicVisualHost : FrameworkElement
    {
        private const int AlphaSteps = 16;

        private readonly DrawingVisual _visual;
        private readonly VisualCollection _children;
        private double _width;
        private double _height;
        private double _scale;
        private bool _previewMode;
        private string _resText;

        // Pre-frozen brushes, pens, and alpha-bucket lookup tables (zero dc.PushOpacity!)
        private Brush _voidBgBrush;
        private StreamGeometry _staticCircuitGeo;

        private Brush[,] _shaftBuckets;
        private Brush[,] _auroraBuckets;
        private Pen[,] _gridPenBuckets;
        private Pen[,] _ringBuckets;
        private Pen[] _polyBuckets;
        private Pen[] _circuitBaseBuckets;
        private Pen[] _circuitGlowBuckets;
        private Brush[] _nodeCoreBuckets;
        private Brush[] _nodeHaloBuckets;
        private Pen[] _filGlowBuckets;
        private Pen[] _filCoreBuckets;
        private Brush[] _wellGlowBuckets;
        private Brush[] _flareStreakBuckets;
        private Brush[] _flareCoreBuckets;
        private Brush[] _flareHaloBuckets;
        private Pen[,] _shockwaveBuckets;
        private Brush[,] _particleBuckets;
        private Brush[,] _bokehBuckets;

        private Brush[] _hudBgBuckets;
        private Pen[] _hudBorderBuckets;
        private Pen[] _hudTopRimBuckets;
        private Pen[] _hudTickBuckets;
        private Pen[] _hudWaveBuckets;
        private Pen[] _hudZeroLineBuckets;
        private Brush[] _hudBarBuckets;
        private Brush[] _hudBarActiveBuckets;
        private Brush[] _hudDotDoneBuckets;
        private Brush[] _hudDotActiveBuckets;
        private Brush[] _hudDotPendingBuckets;
        private Pen[] _chromeTickBuckets;

        private Brush[] _hudTitleBrushBuckets;
        private Brush[] _hudValueBrushBuckets;
        private Brush[] _hudMutedBrushBuckets;
        private Brush[] _hudAccentBrushBuckets;
        private Brush[] _chromeTextBrushBuckets;

        private Typeface _monoTypeface;

        // Cached static FormattedText instances + last bucket index
        private FormattedText[] _cachedHudTitles;
        private FormattedText[] _cachedHudTags;
        private FormattedText[] _cachedRadarLabels;
        private FormattedText[] _cachedSubsystemItems;
        private FormattedText _cachedStatusOk;
        private FormattedText _cachedStatusInit;
        private FormattedText _cachedStatusWait;
        private FormattedText _cachedComputeFooter;
        private FormattedText _cachedChromeLeft;
        private FormattedText _cachedPreviewHint;
        private int _lastHudTextBucket = -1;
        private int _lastChromeTextBucket = -1;

        // Throttled dynamic FormattedText caches
        private string _lastFieldStr;
        private int _lastFieldBucket = -1;
        private FormattedText _cachedFieldFt;
        private string _lastTimeStr;
        private int _lastTimeBucket = -1;
        private FormattedText _cachedTimeFt;

        private struct StarParticle
        {
            public double XNorm;
            public double YNorm;
            public double Size;
            public double Depth;
            public double Speed;
            public double DriftAmp;
            public double DriftFreq;
            public double Phase;
            public double MaxAlpha;
            public int ColorIdx;
        }

        private struct CircuitTrace
        {
            public Point[] Points;
            public double[] CumLengths;
            public double TotalLength;
            public double Delay;
            public double Speed;
        }

        private StarParticle[] _particles;
        private CircuitTrace[] _circuits;
        private int[,] _polyEdges;

        private const int GridCols = 17;
        private const int GridRows = 13;
        private Point[,] _gridProjected = new Point[GridRows, GridCols];
        private double[,] _gridIntensity = new double[GridRows, GridCols];

        public CinematicVisualHost(double width, double height, double scale, bool previewMode, string resText)
        {
            _width = width;
            _height = height;
            _scale = scale;
            _previewMode = previewMode;
            _resText = resText;
            _visual = new DrawingVisual();
            _children = new VisualCollection(this);
            _children.Add(_visual);
            IsHitTestVisible = false;

            InitResources();
            InitGeometryData();
        }

        protected override int VisualChildrenCount
        {
            get { return _children.Count; }
        }

        protected override Visual GetVisualChild(int index)
        {
            return _children[index];
        }

        public void Resize(double width, double height, double scale, string resText)
        {
            _width = width;
            _height = height;
            _scale = scale;
            _resText = resText;
            InitResources();
            InitGeometryData();
        }

        private static int AlphaBucket(double alpha)
        {
            if (alpha <= 0.015) return 0;
            int idx = (int)Math.Round(Clamp01(alpha) * (AlphaSteps - 1));
            return idx < 1 ? 1 : (idx >= AlphaSteps ? AlphaSteps - 1 : idx);
        }

        private static Color WithAlphaScale(Color baseColor, double factor)
        {
            byte a = (byte)Math.Round(baseColor.A * Clamp01(factor));
            return Color.FromArgb(a, baseColor.R, baseColor.G, baseColor.B);
        }

        private static Brush[] BuildSolidBrushTable(Color baseColor)
        {
            Brush[] arr = new Brush[AlphaSteps];
            for (int i = 1; i < AlphaSteps; i++)
            {
                double f = (double)i / (AlphaSteps - 1);
                SolidColorBrush b = new SolidColorBrush(WithAlphaScale(baseColor, f));
                b.Freeze();
                arr[i] = b;
            }
            return arr;
        }

        private static Pen[] BuildSolidPenTable(Color baseColor, double thickness)
        {
            Pen[] arr = new Pen[AlphaSteps];
            for (int i = 1; i < AlphaSteps; i++)
            {
                double f = (double)i / (AlphaSteps - 1);
                SolidColorBrush b = new SolidColorBrush(WithAlphaScale(baseColor, f));
                b.Freeze();
                Pen p = new Pen(b, thickness);
                p.StartLineCap = PenLineCap.Round;
                p.EndLineCap = PenLineCap.Round;
                p.Freeze();
                arr[i] = p;
            }
            return arr;
        }

        private void InitResources()
        {
            _monoTypeface = new Typeface(new FontFamily("Consolas, Cascadia Mono, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            _lastHudTextBucket = -1;
            _lastChromeTextBucket = -1;
            _lastFieldStr = null;
            _lastTimeStr = null;

            // 1. Obsidian Cosmic Studio Void (merged deep vignette edge)
            RadialGradientBrush bg = new RadialGradientBrush();
            bg.Center = new Point(0.5, 0.45);
            bg.GradientOrigin = new Point(0.5, 0.45);
            bg.RadiusX = 0.72;
            bg.RadiusY = 0.78;
            bg.GradientStops.Add(new GradientStop(ColorFromHex("#0D1730"), 0.0));
            bg.GradientStops.Add(new GradientStop(ColorFromHex("#070D1C"), 0.36));
            bg.GradientStops.Add(new GradientStop(ColorFromHex("#03060E"), 0.72));
            bg.GradientStops.Add(new GradientStop(ColorFromHex("#010205"), 1.0));
            bg.Freeze();
            _voidBgBrush = bg;

            // 2. Volumetric Shafts
            _shaftBuckets = new Brush[3, AlphaSteps];
            Color[] shaftCols = new Color[]
            {
                Color.FromArgb(26, 226, 240, 255),
                Color.FromArgb(24, 56, 189, 248),
                Color.FromArgb(20, 139, 92, 246)
            };
            for (int c = 0; c < 3; c++)
            {
                for (int s = 1; s < AlphaSteps; s++)
                {
                    double f = (double)s / (AlphaSteps - 1);
                    LinearGradientBrush sb = new LinearGradientBrush();
                    sb.StartPoint = new Point(0.5, 0.0);
                    sb.EndPoint = new Point(0.5, 1.0);
                    sb.GradientStops.Add(new GradientStop(Color.FromArgb(0, shaftCols[c].R, shaftCols[c].G, shaftCols[c].B), 0.0));
                    sb.GradientStops.Add(new GradientStop(WithAlphaScale(shaftCols[c], f), 0.25));
                    sb.GradientStops.Add(new GradientStop(WithAlphaScale(shaftCols[c], f * 0.5), 0.68));
                    sb.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 1.0));
                    sb.Freeze();
                    _shaftBuckets[c, s] = sb;
                }
            }

            // 3. Aurora Ribbons
            _auroraBuckets = new Brush[4, AlphaSteps];
            Color[] aurA = new Color[]
            {
                Color.FromArgb(46, 37, 99, 235),
                Color.FromArgb(40, 56, 189, 248),
                Color.FromArgb(36, 139, 92, 246),
                Color.FromArgb(24, 234, 67, 53)
            };
            Color[] aurB = new Color[]
            {
                Color.FromArgb(32, 56, 189, 248),
                Color.FromArgb(34, 167, 139, 250),
                Color.FromArgb(30, 52, 211, 153),
                Color.FromArgb(22, 251, 188, 5)
            };
            for (int r = 0; r < 4; r++)
            {
                for (int s = 1; s < AlphaSteps; s++)
                {
                    double f = (double)s / (AlphaSteps - 1);
                    LinearGradientBrush ab = new LinearGradientBrush();
                    ab.StartPoint = new Point(0.0, 0.5);
                    ab.EndPoint = new Point(1.0, 0.5);
                    ab.GradientStops.Add(new GradientStop(Color.FromArgb(0, aurA[r].R, aurA[r].G, aurA[r].B), 0.0));
                    ab.GradientStops.Add(new GradientStop(WithAlphaScale(aurA[r], f), 0.28));
                    ab.GradientStops.Add(new GradientStop(WithAlphaScale(aurB[r], f), 0.72));
                    ab.GradientStops.Add(new GradientStop(Color.FromArgb(0, aurB[r].R, aurB[r].G, aurB[r].B), 1.0));
                    ab.Freeze();
                    _auroraBuckets[r, s] = ab;
                }
            }

            // 4. Grid Pens (0=low, 1=bright, 2=highlight)
            _gridPenBuckets = new Pen[3, AlphaSteps];
            Color[] gridCols = new Color[]
            {
                Color.FromArgb(26, 96, 165, 250),
                Color.FromArgb(78, 56, 189, 248),
                Color.FromArgb(140, 224, 242, 254)
            };
            double[] gridThick = new double[] { 0.85 * _scale, 1.05 * _scale, 1.25 * _scale };
            for (int k = 0; k < 3; k++)
            {
                Pen[] tbl = BuildSolidPenTable(gridCols[k], gridThick[k]);
                for (int s = 1; s < AlphaSteps; s++) _gridPenBuckets[k, s] = tbl[s];
            }

            // 5. Gyro Rings & Polyhedron
            _ringBuckets = new Pen[3, AlphaSteps];
            Color[] ringCols = new Color[]
            {
                Color.FromArgb(125, 56, 189, 248),
                Color.FromArgb(115, 241, 245, 249),
                Color.FromArgb(115, 167, 139, 250)
            };
            double[] ringThick = new double[] { 1.15 * _scale, 0.95 * _scale, 1.10 * _scale };
            for (int k = 0; k < 3; k++)
            {
                Pen[] tbl = BuildSolidPenTable(ringCols[k], ringThick[k]);
                for (int s = 1; s < AlphaSteps; s++) _ringBuckets[k, s] = tbl[s];
            }
            _polyBuckets = BuildSolidPenTable(Color.FromArgb(80, 125, 211, 252), 1.0 * _scale);

            // 6. Silicon Circuits
            _circuitBaseBuckets = BuildSolidPenTable(Color.FromArgb(28, 100, 116, 139), 1.05 * _scale);
            _circuitGlowBuckets = BuildSolidPenTable(Color.FromArgb(190, 125, 211, 252), 1.75 * _scale);
            _nodeCoreBuckets = BuildSolidBrushTable(Color.FromArgb(230, 224, 242, 254));
            _nodeHaloBuckets = new Brush[AlphaSteps];
            for (int s = 1; s < AlphaSteps; s++)
            {
                double f = (double)s / (AlphaSteps - 1);
                RadialGradientBrush nh = new RadialGradientBrush();
                nh.GradientStops.Add(new GradientStop(WithAlphaScale(Color.FromArgb(170, 56, 189, 248), f), 0.0));
                nh.GradientStops.Add(new GradientStop(Color.FromArgb(0, 56, 189, 248), 1.0));
                nh.Freeze();
                _nodeHaloBuckets[s] = nh;
            }

            // 7. Filament & Well Glow
            _filGlowBuckets = BuildSolidPenTable(Color.FromArgb(95, 56, 189, 248), 7.5 * _scale);
            _filCoreBuckets = BuildSolidPenTable(Color.FromArgb(235, 248, 250, 252), 1.8 * _scale);
            _wellGlowBuckets = new Brush[AlphaSteps];
            for (int s = 1; s < AlphaSteps; s++)
            {
                double f = (double)s / (AlphaSteps - 1);
                RadialGradientBrush wg = new RadialGradientBrush();
                wg.GradientStops.Add(new GradientStop(WithAlphaScale(Color.FromArgb(125, 56, 189, 248), f), 0.0));
                wg.GradientStops.Add(new GradientStop(WithAlphaScale(Color.FromArgb(52, 139, 92, 246), f), 0.45));
                wg.GradientStops.Add(new GradientStop(Color.FromArgb(0, 15, 23, 42), 1.0));
                wg.Freeze();
                _wellGlowBuckets[s] = wg;
            }

            // 8. Anamorphic Lens Flare & Shockwaves
            _flareStreakBuckets = new Brush[AlphaSteps];
            _flareCoreBuckets = new Brush[AlphaSteps];
            _flareHaloBuckets = new Brush[AlphaSteps];
            for (int s = 1; s < AlphaSteps; s++)
            {
                double f = (double)s / (AlphaSteps - 1);

                LinearGradientBrush asb = new LinearGradientBrush();
                asb.StartPoint = new Point(0, 0.5);
                asb.EndPoint = new Point(1, 0.5);
                asb.GradientStops.Add(new GradientStop(Color.FromArgb(0, 56, 189, 248), 0.0));
                asb.GradientStops.Add(new GradientStop(WithAlphaScale(Color.FromArgb(85, 56, 189, 248), f), 0.28));
                asb.GradientStops.Add(new GradientStop(WithAlphaScale(Color.FromArgb(220, 224, 242, 254), f), 0.50));
                asb.GradientStops.Add(new GradientStop(WithAlphaScale(Color.FromArgb(85, 167, 139, 250), f), 0.72));
                asb.GradientStops.Add(new GradientStop(Color.FromArgb(0, 167, 139, 250), 1.0));
                asb.Freeze();
                _flareStreakBuckets[s] = asb;

                LinearGradientBrush acb = new LinearGradientBrush();
                acb.StartPoint = new Point(0, 0.5);
                acb.EndPoint = new Point(1, 0.5);
                acb.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.15));
                acb.GradientStops.Add(new GradientStop(WithAlphaScale(Color.FromArgb(255, 255, 255, 255), f), 0.50));
                acb.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.85));
                acb.Freeze();
                _flareCoreBuckets[s] = acb;

                RadialGradientBrush ahb = new RadialGradientBrush();
                ahb.GradientStops.Add(new GradientStop(WithAlphaScale(Color.FromArgb(215, 255, 255, 255), f), 0.0));
                ahb.GradientStops.Add(new GradientStop(WithAlphaScale(Color.FromArgb(130, 125, 211, 252), f), 0.25));
                ahb.GradientStops.Add(new GradientStop(WithAlphaScale(Color.FromArgb(48, 167, 139, 250), f), 0.58));
                ahb.GradientStops.Add(new GradientStop(Color.FromArgb(0, 15, 23, 42), 1.0));
                ahb.Freeze();
                _flareHaloBuckets[s] = ahb;
            }

            _shockwaveBuckets = new Pen[3, AlphaSteps];
            Color[] swCols = new Color[]
            {
                Color.FromArgb(220, 248, 250, 252),
                Color.FromArgb(175, 56, 189, 248),
                Color.FromArgb(145, 167, 139, 250)
            };
            double[] swThick = new double[] { 2.1 * _scale, 1.5 * _scale, 1.2 * _scale };
            for (int k = 0; k < 3; k++)
            {
                Pen[] tbl = BuildSolidPenTable(swCols[k], swThick[k]);
                for (int s = 1; s < AlphaSteps; s++) _shockwaveBuckets[k, s] = tbl[s];
            }

            // 9. Particles & Bokeh
            Color[] pCols = new Color[]
            {
                ColorFromHex("#F8FAFC"),
                ColorFromHex("#7DD3FC"),
                ColorFromHex("#A78BFA"),
                ColorFromHex("#FDE68A"),
                ColorFromHex("#6EE7B7")
            };
            _particleBuckets = new Brush[pCols.Length, AlphaSteps];
            _bokehBuckets = new Brush[pCols.Length, AlphaSteps];
            for (int c = 0; c < pCols.Length; c++)
            {
                for (int s = 1; s < AlphaSteps; s++)
                {
                    double f = (double)s / (AlphaSteps - 1);
                    SolidColorBrush pb = new SolidColorBrush(WithAlphaScale(pCols[c], f));
                    pb.Freeze();
                    _particleBuckets[c, s] = pb;

                    RadialGradientBrush bb = new RadialGradientBrush();
                    bb.GradientStops.Add(new GradientStop(WithAlphaScale(Color.FromArgb(180, pCols[c].R, pCols[c].G, pCols[c].B), f), 0.0));
                    bb.GradientStops.Add(new GradientStop(WithAlphaScale(Color.FromArgb(65, pCols[c].R, pCols[c].G, pCols[c].B), f), 0.48));
                    bb.GradientStops.Add(new GradientStop(Color.FromArgb(0, pCols[c].R, pCols[c].G, pCols[c].B), 1.0));
                    bb.Freeze();
                    _bokehBuckets[c, s] = bb;
                }
            }

            // 10. HUD & Chrome Buckets
            _hudBgBuckets = BuildSolidBrushTable(Color.FromArgb(148, 10, 17, 34));
            _hudBorderBuckets = new Pen[AlphaSteps];
            _hudTopRimBuckets = new Pen[AlphaSteps];
            for (int s = 1; s < AlphaSteps; s++)
            {
                double f = (double)s / (AlphaSteps - 1);

                LinearGradientBrush hudBorder = new LinearGradientBrush();
                hudBorder.StartPoint = new Point(0, 0);
                hudBorder.EndPoint = new Point(1, 1);
                hudBorder.GradientStops.Add(new GradientStop(WithAlphaScale(Color.FromArgb(115, 186, 230, 253), f), 0.0));
                hudBorder.GradientStops.Add(new GradientStop(WithAlphaScale(Color.FromArgb(40, 148, 163, 184), f), 0.5));
                hudBorder.GradientStops.Add(new GradientStop(WithAlphaScale(Color.FromArgb(75, 167, 139, 250), f), 1.0));
                hudBorder.Freeze();
                Pen bp = new Pen(hudBorder, 1.0);
                bp.Freeze();
                _hudBorderBuckets[s] = bp;

                LinearGradientBrush hudRim = new LinearGradientBrush();
                hudRim.StartPoint = new Point(0, 0.5);
                hudRim.EndPoint = new Point(1, 0.5);
                hudRim.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.0));
                hudRim.GradientStops.Add(new GradientStop(WithAlphaScale(Color.FromArgb(135, 224, 242, 254), f), 0.5));
                hudRim.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0));
                hudRim.Freeze();
                Pen rp = new Pen(hudRim, 1.1);
                rp.Freeze();
                _hudTopRimBuckets[s] = rp;
            }

            _hudTickBuckets = BuildSolidPenTable(Color.FromArgb(110, 125, 211, 252), 1.1);
            _hudWaveBuckets = BuildSolidPenTable(Color.FromArgb(225, 56, 189, 248), 1.65);
            _hudZeroLineBuckets = BuildSolidPenTable(Color.FromArgb(70, 148, 163, 184), 0.9);
            _hudBarBuckets = BuildSolidBrushTable(Color.FromArgb(75, 56, 189, 248));
            _hudBarActiveBuckets = BuildSolidBrushTable(Color.FromArgb(220, 125, 211, 252));
            _hudDotDoneBuckets = BuildSolidBrushTable(ColorFromHex("#34D399"));
            _hudDotActiveBuckets = BuildSolidBrushTable(ColorFromHex("#38BDF8"));
            _hudDotPendingBuckets = BuildSolidBrushTable(Color.FromArgb(75, 148, 163, 184));
            _chromeTickBuckets = BuildSolidPenTable(Color.FromArgb(85, 148, 163, 184), 1.0);

            _hudTitleBrushBuckets = BuildSolidBrushTable(Color.FromArgb(215, 226, 232, 240));
            _hudValueBrushBuckets = BuildSolidBrushTable(Color.FromArgb(235, 248, 250, 252));
            _hudMutedBrushBuckets = BuildSolidBrushTable(Color.FromArgb(150, 148, 163, 184));
            _hudAccentBrushBuckets = BuildSolidBrushTable(ColorFromHex("#7DD3FC"));
            _chromeTextBrushBuckets = BuildSolidBrushTable(Color.FromArgb(155, 148, 163, 184));

            // 11. Cache Static FormattedText Objects Once
            string[] titles = new string[] { "GRAVITY FIELD WAVEFORM", "NEURAL COMPUTE MATRIX", "ORBITAL VECTOR TELEMETRY", "SUBSYSTEM INITIALIZATION" };
            string[] tags = new string[] { "SENSOR // 01", "CORE // 128-CH", "LOCK // 99.98%", "BOOT // 05/05" };
            _cachedHudTitles = new FormattedText[4];
            _cachedHudTags = new FormattedText[4];
            for (int i = 0; i < 4; i++)
            {
                _cachedHudTitles[i] = CreateFormattedText(titles[i], _monoTypeface, 9.5 * _scale, _hudTitleBrushBuckets[AlphaSteps - 1]);
                _cachedHudTags[i] = CreateFormattedText(tags[i], _monoTypeface, 8.8 * _scale, _hudAccentBrushBuckets[AlphaSteps - 1]);
            }

            _cachedRadarLabels = new FormattedText[]
            {
                CreateFormattedText("COHERENCE   99.98%", _monoTypeface, 9.6 * _scale, _hudValueBrushBuckets[AlphaSteps - 1]),
                CreateFormattedText("PITCH / YAW  0.00\u00B0", _monoTypeface, 9.2 * _scale, _hudMutedBrushBuckets[AlphaSteps - 1]),
                CreateFormattedText("VECTOR LOCK  NOMINAL", _monoTypeface, 9.2 * _scale, _hudAccentBrushBuckets[AlphaSteps - 1]),
                CreateFormattedText("OPTICAL AXIS ALIGNED", _monoTypeface, 9.0 * _scale, _hudMutedBrushBuckets[AlphaSteps - 1])
            };

            string[] subItems = new string[]
            {
                "SPACETIME CALIBRATION",
                "OPTICAL PRISM ENGINE",
                "NEURAL AGENT RUNTIME",
                "WORKSPACE ENVIRONMENT",
                "ZERO-G PORTAL HANDOFF"
            };
            _cachedSubsystemItems = new FormattedText[subItems.Length];
            for (int i = 0; i < subItems.Length; i++)
            {
                _cachedSubsystemItems[i] = CreateFormattedText(subItems[i], _monoTypeface, 9.2 * _scale, _hudValueBrushBuckets[AlphaSteps - 1]);
            }

            _cachedStatusOk = CreateFormattedText("OK", _monoTypeface, 8.8 * _scale, _hudDotDoneBuckets[AlphaSteps - 1]);
            _cachedStatusInit = CreateFormattedText("INIT", _monoTypeface, 8.8 * _scale, _hudMutedBrushBuckets[AlphaSteps - 1]);
            _cachedStatusWait = CreateFormattedText("WAIT", _monoTypeface, 8.8 * _scale, _hudMutedBrushBuckets[AlphaSteps - 1]);
            _cachedComputeFooter = CreateFormattedText("NEURAL FABRIC \u00B7 QUANTUM SYNC ACTIVE", _monoTypeface, 9.0 * _scale, _hudMutedBrushBuckets[AlphaSteps - 1]);

            _cachedChromeLeft = CreateFormattedText("ANTIGRAVITY  //  ZERO-G LAUNCH SEQUENCE", _monoTypeface, 9.6 * _scale, _chromeTextBrushBuckets[AlphaSteps - 1]);
            _cachedPreviewHint = CreateFormattedText("PREVIEW MODE  \u00B7  [SPACE] PAUSE  \u00B7  [\u2190/\u2192] SCRUB  \u00B7  [R] REPLAY  \u00B7  [ESC] EXIT", _monoTypeface, 9.2 * _scale, _chromeTextBrushBuckets[AlphaSteps - 1]);
        }

        private void InitGeometryData()
        {
            Random rng = new Random(20261126);
            _particles = new StarParticle[100];
            for (int i = 0; i < _particles.Length; i++)
            {
                int depth = (i < 65) ? 0 : (i < 90 ? 1 : 2);
                StarParticle p = new StarParticle();
                p.XNorm = rng.NextDouble();
                p.YNorm = rng.NextDouble();
                p.Depth = depth;
                if (depth == 0)
                {
                    p.Size = (0.9 + rng.NextDouble() * 1.3) * _scale;
                    p.Speed = 0.012 + rng.NextDouble() * 0.018;
                    p.MaxAlpha = 0.25 + rng.NextDouble() * 0.45;
                }
                else if (depth == 1)
                {
                    p.Size = (1.6 + rng.NextDouble() * 2.2) * _scale;
                    p.Speed = 0.032 + rng.NextDouble() * 0.038;
                    p.MaxAlpha = 0.45 + rng.NextDouble() * 0.45;
                }
                else
                {
                    p.Size = (9.0 + rng.NextDouble() * 18.0) * _scale;
                    p.Speed = 0.055 + rng.NextDouble() * 0.045;
                    p.MaxAlpha = 0.16 + rng.NextDouble() * 0.22;
                }
                p.DriftAmp = (3.0 + rng.NextDouble() * 11.0) * _scale;
                p.DriftFreq = 0.6 + rng.NextDouble() * 1.4;
                p.Phase = rng.NextDouble() * Math.PI * 2.0;
                p.ColorIdx = rng.Next(5);
                _particles[i] = p;
            }

            _polyEdges = new int[,]
            {
                {0,2},{0,3},{0,4},{0,5},
                {1,2},{1,3},{1,4},{1,5},
                {2,4},{4,3},{3,5},{5,2}
            };

            double cx = _width * 0.5;
            double cy = _height * 0.44;
            List<CircuitTrace> traces = new List<CircuitTrace>();
            double[] yOffsets = new double[] { -170, -120, -76, -34, 34, 76, 120, 170 };
            for (int side = -1; side <= 1; side += 2)
            {
                for (int k = 0; k < yOffsets.Length; k++)
                {
                    double dy = yOffsets[k] * _scale;
                    double r0 = 78.0 * _scale;
                    double x1 = cx + side * (r0 + (k % 3) * 18.0 * _scale);
                    double y1 = cy + dy * 0.35;
                    double x2 = cx + side * (195.0 + (k % 2) * 38.0) * _scale;
                    double y2 = cy + dy * 0.78;
                    double x3 = cx + side * (370.0 + (k % 4) * 52.0) * _scale;
                    double y3 = y2;
                    double x4 = cx + side * (520.0 + (k % 3) * 65.0) * _scale;
                    double y4 = y2 + ((dy >= 0 ? 1 : -1) * 26.0 * _scale);

                    Point[] pts = new Point[]
                    {
                        new Point(cx + side * r0 * 0.72, cy + dy * 0.12),
                        new Point(x1, y1),
                        new Point(x2, y2),
                        new Point(x3, y3),
                        new Point(x4, y4)
                    };
                    double[] cum = new double[pts.Length];
                    double total = 0.0;
                    for (int p = 1; p < pts.Length; p++)
                    {
                        double dx = pts[p].X - pts[p - 1].X;
                        double ddy = pts[p].Y - pts[p - 1].Y;
                        total += Math.Sqrt(dx * dx + ddy * ddy);
                        cum[p] = total;
                    }
                    CircuitTrace ct = new CircuitTrace();
                    ct.Points = pts;
                    ct.CumLengths = cum;
                    ct.TotalLength = total;
                    ct.Delay = 1.9 + (k * 0.16) + (side > 0 ? 0.08 : 0.0);
                    ct.Speed = 0.42 + (k % 3) * 0.11;
                    traces.Add(ct);
                }
            }
            _circuits = traces.ToArray();

            StreamGeometry cGeo = new StreamGeometry();
            using (StreamGeometryContext cCtx = cGeo.Open())
            {
                for (int i = 0; i < _circuits.Length; i++)
                {
                    Point[] pts = _circuits[i].Points;
                    cCtx.BeginFigure(pts[0], false, false);
                    for (int p = 1; p < pts.Length; p++) cCtx.LineTo(pts[p], true, false);
                }
            }
            cGeo.Freeze();
            _staticCircuitGeo = cGeo;
        }

        public void Render(double t, double totalDuration)
        {
            using (DrawingContext dc = _visual.RenderOpen())
            {
                double cx = _width * 0.5;
                double cy = _height * 0.44;

                dc.DrawRectangle(_voidBgBrush, null, new Rect(0, 0, _width, _height));

                RenderVolumetricShafts(dc, t);
                RenderAuroraRibbons(dc, t);
                RenderSpacetimeGrid(dc, t, cx, cy);
                RenderSiliconCircuits(dc, t);
                RenderGyroAndPolyhedron(dc, t, cx, cy);
                RenderGravityFilament(dc, t, cx, cy);
                RenderShockwavesAndFlare(dc, t, cx, cy);
                RenderParticles(dc, t);
                RenderHudCards(dc, t);
                RenderCinemaChrome(dc, t, totalDuration);
            }
        }

        private void RenderVolumetricShafts(DrawingContext dc, double t)
        {
            double master = SmoothStep(0.3, 1.8, t) * (1.0 - SmoothStep(13.6, 15.0, t));
            if (master <= 0.005) return;

            double boost = 1.0 + 0.45 * SmoothStep(5.3, 6.8, t);
            for (int i = 0; i < 3; i++)
            {
                double phase = i * 1.95;
                double sweep = Math.Sin(t * 0.24 + phase) * 0.08;
                double topX = _width * (0.20 + i * 0.30 + sweep);
                double topW = (62.0 + (i % 2) * 24.0) * _scale;
                double tilt = (i - 1.0) * 120.0 * _scale + Math.Cos(t * 0.19 + phase) * 40.0 * _scale;
                double botW = topW * 2.5;

                double pulse = 0.58 + 0.42 * Math.Sin(t * 0.42 + i * 1.1);
                int bIdx = AlphaBucket(master * boost * pulse * 0.78);
                if (bIdx == 0) continue;

                StreamGeometry geo = new StreamGeometry();
                using (StreamGeometryContext ctx = geo.Open())
                {
                    ctx.BeginFigure(new Point(topX - topW * 0.5, -20.0), true, true);
                    ctx.LineTo(new Point(topX + topW * 0.5, -20.0), true, false);
                    ctx.LineTo(new Point(topX + tilt + botW * 0.5, _height + 20.0), true, false);
                    ctx.LineTo(new Point(topX + tilt - botW * 0.5, _height + 20.0), true, false);
                }
                geo.Freeze();

                dc.DrawGeometry(_shaftBuckets[i % 3, bIdx], null, geo);
            }
        }

        private void RenderAuroraRibbons(DrawingContext dc, double t)
        {
            double preGlow = SmoothStep(1.2, 4.2, t) * 0.28;
            double postBloom = SmoothStep(5.35, 7.2, t) * 0.78;
            double fadeOut = 1.0 - SmoothStep(13.7, 15.0, t);
            double alpha = Clamp01((preGlow + postBloom) * fadeOut);
            if (alpha <= 0.005) return;

            int segs = 16;
            for (int r = 0; r < 3; r++)
            {
                int bIdx = AlphaBucket(alpha * (r == 2 ? 0.68 : 0.82));
                if (bIdx == 0) continue;

                double baseY = _height * (0.37 + r * 0.08);
                double amp1 = (34.0 + r * 12.0) * _scale;
                double amp2 = (18.0 + r * 7.0) * _scale;
                double thickness = (54.0 + r * 16.0) * _scale;
                double speed = 0.48 + r * 0.14;

                StreamGeometry geo = new StreamGeometry();
                using (StreamGeometryContext ctx = geo.Open())
                {
                    Point[] topPts = new Point[segs + 1];
                    Point[] botPts = new Point[segs + 1];
                    for (int s = 0; s <= segs; s++)
                    {
                        double u = (double)s / segs;
                        double x = _width * (0.04 + u * 0.92);
                        double wave = Math.Sin(u * 5.2 + t * speed + r * 1.3) * amp1
                                    + Math.Cos(u * 8.4 - t * speed * 0.7 + r) * amp2;
                        double taper = Math.Sin(u * Math.PI);
                        double halfH = thickness * (0.35 + 0.65 * taper);
                        topPts[s] = new Point(x, baseY + wave - halfH);
                        botPts[s] = new Point(x, baseY + wave + halfH);
                    }

                    ctx.BeginFigure(topPts[0], true, true);
                    for (int s = 1; s <= segs; s++) ctx.LineTo(topPts[s], true, false);
                    for (int s = segs; s >= 0; s--) ctx.LineTo(botPts[s], true, false);
                }
                geo.Freeze();

                dc.DrawGeometry(_auroraBuckets[r, bIdx], null, geo);
            }
        }

        private void RenderSpacetimeGrid(DrawingContext dc, double t, double cx, double cy)
        {
            double gridIn = SmoothStep(0.6, 2.2, t);
            double gridOut = 1.0 - SmoothStep(13.6, 15.0, t);
            double master = gridIn * gridOut;
            if (master <= 0.005) return;

            double horizonY = _height * 0.50;
            double camHeight = 3.1;
            double fov = 560.0 * _scale;

            double wellAmp = 0.0;
            double ripplePhase = 0.0;
            double rippleAmp = 0.0;

            if (t < 5.45)
            {
                wellAmp = -2.35 * SmoothStep(0.9, 4.9, t);
            }
            else
            {
                double dt = t - 5.45;
                double snap = EaseOutBack(Clamp01(dt / 0.65));
                wellAmp = Lerp(-2.35, 0.55, snap) * Math.Exp(-dt * 0.35);
                rippleAmp = 1.15 * Math.Exp(-dt * 0.55);
                ripplePhase = dt * 6.8;
            }

            for (int r = 0; r < GridRows; r++)
            {
                double z = 2.2 + r * 1.25;
                for (int c = 0; c < GridCols; c++)
                {
                    double x = (c - (GridCols - 1) * 0.5) * 1.42;
                    double dz = z - 7.8;
                    double dist2 = x * x + dz * dz;
                    double dist = Math.Sqrt(dist2);

                    double yDip = wellAmp * Math.Exp(-dist2 / 14.0);
                    double yWave = (rippleAmp > 0.001)
                        ? rippleAmp * Math.Sin(dist * 1.45 - ripplePhase) * Math.Exp(-dist * 0.16)
                        : 0.0;
                    double yWorld = camHeight - (yDip + yWave);

                    double px = cx + (x / z) * fov;
                    double py = horizonY + (yWorld / z) * fov;
                    _gridProjected[r, c] = new Point(px, py);
                    _gridIntensity[r, c] = Clamp01(Math.Abs(yDip + yWave) * 0.55);
                }
            }

            for (int r = 0; r < GridRows; r++)
            {
                double depthFade = 1.0 - (double)r / (GridRows - 1);
                depthFade = depthFade * depthFade;
                int bIdx = AlphaBucket(master * 0.85 * (0.25 + 0.75 * depthFade));
                if (bIdx == 0) continue;

                for (int c = 0; c < GridCols; c++)
                {
                    Point p0 = _gridProjected[r, c];
                    double inten = _gridIntensity[r, c];
                    int penTier = inten > 0.42 ? 2 : (inten > 0.14 ? 1 : 0);
                    Pen pen = _gridPenBuckets[penTier, bIdx];

                    if (c + 1 < GridCols)
                    {
                        dc.DrawLine(pen, p0, _gridProjected[r, c + 1]);
                    }
                    if (r + 1 < GridRows)
                    {
                        dc.DrawLine(pen, p0, _gridProjected[r + 1, c]);
                    }
                }
            }
        }

        private void RenderSiliconCircuits(DrawingContext dc, double t)
        {
            double fadeIn = SmoothStep(1.85, 3.2, t);
            double fadeOut = 1.0 - SmoothStep(13.3, 14.8, t);
            double master = fadeIn * fadeOut;
            if (master <= 0.005) return;

            int baseB = AlphaBucket(master * 0.65);
            if (baseB > 0)
            {
                dc.DrawGeometry(null, _circuitBaseBuckets[baseB], _staticCircuitGeo);
            }

            for (int i = 0; i < _circuits.Length; i++)
            {
                CircuitTrace ct = _circuits[i];
                double appear = SmoothStep(ct.Delay, ct.Delay + 0.8, t);
                if (appear <= 0.005) continue;

                if (baseB > 0)
                {
                    Point endPt = ct.Points[ct.Points.Length - 1];
                    dc.DrawEllipse(_nodeCoreBuckets[baseB], null, endPt, 2.1 * _scale, 2.1 * _scale);
                }

                if (t >= ct.Delay + 0.25 && t <= 13.5)
                {
                    double cycle = ((t - ct.Delay) * ct.Speed) % 1.4;
                    if (cycle >= 0.0 && cycle <= 1.0)
                    {
                        int pulseB = AlphaBucket(master * appear * 0.92);
                        if (pulseB > 0)
                        {
                            double dHead = cycle * ct.TotalLength;
                            double dTail = Math.Max(0.0, dHead - 48.0 * _scale);
                            Point pHead = SampleTracePoint(ct, dHead);
                            Point pTail = SampleTracePoint(ct, dTail);

                            dc.DrawLine(_circuitGlowBuckets[pulseB], pTail, pHead);
                            dc.DrawEllipse(_nodeHaloBuckets[pulseB], null, pHead, 7.5 * _scale, 7.5 * _scale);
                            dc.DrawEllipse(_nodeCoreBuckets[pulseB], null, pHead, 2.0 * _scale, 2.0 * _scale);
                        }
                    }
                }
            }
        }

        private Point SampleTracePoint(CircuitTrace ct, double dist)
        {
            if (dist <= 0.0) return ct.Points[0];
            if (dist >= ct.TotalLength) return ct.Points[ct.Points.Length - 1];
            for (int p = 1; p < ct.Points.Length; p++)
            {
                if (dist <= ct.CumLengths[p])
                {
                    double prev = ct.CumLengths[p - 1];
                    double segLen = ct.CumLengths[p] - prev;
                    double u = segLen > 0.0001 ? (dist - prev) / segLen : 0.0;
                    return new Point(
                        Lerp(ct.Points[p - 1].X, ct.Points[p].X, u),
                        Lerp(ct.Points[p - 1].Y, ct.Points[p].Y, u));
                }
            }
            return ct.Points[ct.Points.Length - 1];
        }

        private void RenderGyroAndPolyhedron(DrawingContext dc, double t, double cx, double cy)
        {
            double fadeIn = SmoothStep(2.0, 3.6, t);
            double fadeOut = 1.0 - SmoothStep(13.6, 15.0, t);
            double master = fadeIn * fadeOut;
            if (master <= 0.005) return;

            double boost = 1.0 - 0.58 * SmoothStep(11.4, 13.0, t);
            double burstScale = 1.0;
            if (t >= 5.45 && t < 7.5)
            {
                double dt = t - 5.45;
                burstScale = 1.0 + 0.34 * Math.Sin(Clamp01(dt / 1.4) * Math.PI) * Math.Exp(-dt * 1.1);
            }

            // 1. Octahedral 3D Wireframe Lattice
            int polyB = AlphaBucket(master * boost * 0.42);
            if (polyB > 0)
            {
                double polyRadius = 152.0 * _scale * burstScale;
                double px = t * 0.45;
                double py = t * 0.72;
                double pz = t * 0.25;
                double[,] rawOct = new double[,]
                {
                    {0, 1, 0}, {0, -1, 0},
                    {1, 0, 0}, {-1, 0, 0},
                    {0, 0, 1}, {0, 0, -1}
                };
                Point[] projOct = new Point[6];
                for (int i = 0; i < 6; i++)
                {
                    projOct[i] = Project3DPoint(rawOct[i, 0] * polyRadius, rawOct[i, 1] * polyRadius, rawOct[i, 2] * polyRadius, px, py, pz, cx, cy);
                }

                Pen polyPen = _polyBuckets[polyB];
                for (int e = 0; e < _polyEdges.GetLength(0); e++)
                {
                    dc.DrawLine(polyPen, projOct[_polyEdges[e, 0]], projOct[_polyEdges[e, 1]]);
                }
            }

            // 2. Three Concentric 3D Gyroscopic Astrolabe Rings
            double[] radii = new double[] { 176.0 * _scale * burstScale, 218.0 * _scale * burstScale, 264.0 * _scale * burstScale };
            double[] rx = new double[] { 1.08 + Math.Sin(t * 0.35) * 0.22, 0.72 + Math.Cos(t * 0.28) * 0.25, 1.32 };
            double[] ry = new double[] { t * 0.42, -t * 0.31, t * 0.22 };
            double[] rz = new double[] { 0.32, -0.48, t * 0.16 };

            int segs = 38;
            for (int ring = 0; ring < 3; ring++)
            {
                int rB = AlphaBucket(master * boost * (0.48 - ring * 0.08));
                if (rB == 0) continue;

                double r = radii[ring];
                StreamGeometry geo = new StreamGeometry();
                using (StreamGeometryContext ctx = geo.Open())
                {
                    for (int s = 0; s <= segs; s++)
                    {
                        double ang = (double)s / segs * Math.PI * 2.0;
                        Point pt = Project3DPoint(Math.Cos(ang) * r, 0.0, Math.Sin(ang) * r, rx[ring], ry[ring], rz[ring], cx, cy);
                        if (s == 0) ctx.BeginFigure(pt, false, true);
                        else ctx.LineTo(pt, true, false);
                    }
                }
                geo.Freeze();

                dc.DrawGeometry(null, _ringBuckets[ring, rB], geo);

                Brush haloBr = _nodeHaloBuckets[rB];
                Brush coreBr = _nodeCoreBuckets[rB];
                for (int n = 0; n < 2; n++)
                {
                    double nodeAng = t * (0.95 - ring * 0.22) + n * Math.PI + ring * 1.1;
                    Point nPt = Project3DPoint(Math.Cos(nodeAng) * r, 0.0, Math.Sin(nodeAng) * r, rx[ring], ry[ring], rz[ring], cx, cy);
                    dc.DrawEllipse(haloBr, null, nPt, 8.0 * _scale, 8.0 * _scale);
                    dc.DrawEllipse(coreBr, null, nPt, 2.3 * _scale, 2.3 * _scale);
                }
            }
        }

        private Point Project3DPoint(double x, double y, double z, double rx, double ry, double rz, double cx, double cy)
        {
            double cosX = Math.Cos(rx), sinX = Math.Sin(rx);
            double y1 = y * cosX - z * sinX;
            double z1 = y * sinX + z * cosX;

            double cosY = Math.Cos(ry), sinY = Math.Sin(ry);
            double x2 = x * cosY + z1 * sinY;
            double z2 = -x * sinY + z1 * cosY;

            double cosZ = Math.Cos(rz), sinZ = Math.Sin(rz);
            double x3 = x2 * cosZ - y1 * sinZ;
            double y3 = x2 * sinZ + y1 * cosZ;

            double camDist = 920.0 * _scale;
            double persp = camDist / (camDist + z2);
            return new Point(cx + x3 * persp, cy + y3 * persp);
        }

        private void RenderGravityFilament(DrawingContext dc, double t, double cx, double cy)
        {
            double wellIn = SmoothStep(0.4, 2.8, t);
            double wellOut = 1.0 - SmoothStep(5.55, 6.8, t);
            int wellB = AlphaBucket(wellIn * wellOut * 0.9);
            if (wellB > 0)
            {
                double rx = (180.0 + wellIn * 160.0) * _scale;
                double ry = (65.0 + wellIn * 55.0) * _scale;
                dc.DrawEllipse(_wellGlowBuckets[wellB], null, new Point(cx, cy + 28.0 * _scale), rx, ry);
            }

            if (t < 0.35 || t > 6.35) return;

            double filAlpha = 0.0;
            double halfW = 0.0;
            double dip = 0.0;
            double baseY = cy + 6.0 * _scale;

            if (t < 5.45)
            {
                double grow = EaseOutExpo(Clamp01((t - 0.35) / 2.2));
                halfW = 340.0 * _scale * grow;
                double bend = EaseInOutQuad(Clamp01((t - 1.2) / 3.9));
                dip = 95.0 * _scale * bend;
                filAlpha = SmoothStep(0.35, 1.1, t);
            }
            else
            {
                double snapP = Clamp01((t - 5.45) / 0.55);
                double whip = EaseOutBack(snapP);
                dip = Lerp(95.0 * _scale, -68.0 * _scale, whip);
                halfW = Lerp(340.0 * _scale, 90.0 * _scale, EaseInCubic(snapP));
                filAlpha = 1.0 - SmoothStep(5.65, 6.28, t);
            }

            int filB = AlphaBucket(filAlpha);
            if (filB == 0) return;

            Point pStart = new Point(cx - halfW, baseY);
            Point pCtrl = new Point(cx, baseY + dip * 2.0);
            Point pEnd = new Point(cx + halfW, baseY);

            StreamGeometry geo = new StreamGeometry();
            using (StreamGeometryContext ctx = geo.Open())
            {
                ctx.BeginFigure(pStart, false, false);
                ctx.QuadraticBezierTo(pCtrl, pEnd, true, false);
            }
            geo.Freeze();

            dc.DrawGeometry(null, _filGlowBuckets[filB], geo);
            dc.DrawGeometry(null, _filCoreBuckets[filB], geo);
        }

        private void RenderShockwavesAndFlare(DrawingContext dc, double t, double cx, double cy)
        {
            double[] delays = new double[] { 5.45, 5.58, 5.73 };
            for (int i = 0; i < 3; i++)
            {
                double dt = t - delays[i];
                if (dt >= 0.0 && dt <= 1.85)
                {
                    double p = Clamp01(dt / 1.80);
                    int bIdx = AlphaBucket((1.0 - EaseOutQuad(p)) * (0.90 - i * 0.16));
                    if (bIdx == 0) continue;

                    double e = EaseOutExpo(p);
                    double rx = (45.0 + e * (640.0 + i * 95.0)) * _scale;
                    double ry = rx * (0.24 - 0.04 * e);
                    dc.DrawEllipse(null, _shockwaveBuckets[i, bIdx], new Point(cx, cy), rx, ry);
                }
            }

            double flare1 = 0.0;
            if (t >= 5.42 && t <= 7.45)
            {
                double u = (t - 5.42) / 2.03;
                flare1 = Math.Sin(Clamp01(u * 2.4) * Math.PI * 0.5) * Math.Exp(-(Math.Max(0.0, t - 5.56)) * 2.1);
            }
            double flare2 = 0.0;
            if (t >= 11.45 && t <= 13.1)
            {
                double u = Clamp01((t - 11.45) / 1.65);
                flare2 = Math.Sin(u * Math.PI) * 0.55;
            }
            double flare = Clamp01(flare1 + flare2);
            int fB = AlphaBucket(flare * 0.92);
            if (fB > 0)
            {
                double streakW = _width * (0.65 + 0.35 * flare);
                double streakH = (18.0 + 18.0 * flare) * _scale;
                dc.DrawRectangle(_flareStreakBuckets[fB], null, new Rect(cx - streakW * 0.5, cy - streakH * 0.5, streakW, streakH));
                dc.DrawRectangle(_flareCoreBuckets[fB], null, new Rect(cx - streakW * 0.42, cy - 1.5 * _scale, streakW * 0.84, 3.0 * _scale));
                double haloR = (150.0 + 120.0 * flare) * _scale;
                dc.DrawEllipse(_flareHaloBuckets[fB], null, new Point(cx, cy), haloR, haloR * 0.65);
            }
        }

        private void RenderParticles(DrawingContext dc, double t)
        {
            double masterFade = SmoothStep(0.15, 1.4, t) * (1.0 - SmoothStep(13.8, 15.0, t));
            if (masterFade <= 0.005) return;

            for (int i = 0; i < _particles.Length; i++)
            {
                StarParticle p = _particles[i];
                double yNorm;
                double streakLen = 0.0;

                if (t < 5.45)
                {
                    yNorm = (p.YNorm + t * p.Speed * 0.45) % 1.0;
                }
                else
                {
                    double dt = t - 5.45;
                    double burst = Math.Min(dt, 1.4) * 1.6 + Math.Max(0.0, dt - 1.4) * 0.85;
                    double baseAtInversion = (p.YNorm + 5.45 * p.Speed * 0.45) % 1.0;
                    yNorm = (baseAtInversion - burst * p.Speed) % 1.0;
                    if (yNorm < 0.0) yNorm += 1.0;
                    if (p.Depth == 1 && dt < 2.5)
                    {
                        streakLen = (1.0 - dt / 2.5) * 16.0 * _scale;
                    }
                }

                double twinkle = 0.68 + 0.32 * Math.Sin(t * 2.1 + p.Phase);
                int bIdx = AlphaBucket(p.MaxAlpha * masterFade * twinkle);
                if (bIdx == 0) continue;

                double x = p.XNorm * _width + Math.Sin(t * p.DriftFreq + p.Phase) * p.DriftAmp;
                double y = yNorm * _height;

                if (p.Depth == 2)
                {
                    dc.DrawEllipse(_bokehBuckets[p.ColorIdx, bIdx], null, new Point(x, y), p.Size, p.Size);
                }
                else
                {
                    Brush pb = _particleBuckets[p.ColorIdx, bIdx];
                    dc.DrawEllipse(pb, null, new Point(x, y), p.Size, p.Size);
                    if (streakLen > 1.5)
                    {
                        dc.DrawRectangle(pb, null, new Rect(x - p.Size * 0.45, y, p.Size * 0.9, streakLen));
                    }
                }
            }
        }

        private void RenderHudCards(DrawingContext dc, double t)
        {
            if (t < 8.15) return;

            double enter = EaseOutExpo(Clamp01((t - 8.15) / 1.05));
            double exit = 1.0 - SmoothStep(13.7, 14.9, t);
            double finaleDim = 1.0 - 0.68 * SmoothStep(11.6, 13.0, t);
            double masterAlpha = enter * exit * finaleDim;
            int bIdx = AlphaBucket(masterAlpha);
            if (bIdx == 0) return;

            if (bIdx != _lastHudTextBucket)
            {
                _lastHudTextBucket = bIdx;
                Brush tBr = _hudTitleBrushBuckets[bIdx];
                Brush vBr = _hudValueBrushBuckets[bIdx];
                Brush mBr = _hudMutedBrushBuckets[bIdx];
                Brush aBr = _hudAccentBrushBuckets[bIdx];
                Brush okBr = _hudDotDoneBuckets[bIdx];

                for (int i = 0; i < 4; i++)
                {
                    _cachedHudTitles[i].SetForegroundBrush(tBr);
                    _cachedHudTags[i].SetForegroundBrush(aBr);
                }
                _cachedRadarLabels[0].SetForegroundBrush(vBr);
                _cachedRadarLabels[1].SetForegroundBrush(mBr);
                _cachedRadarLabels[2].SetForegroundBrush(aBr);
                _cachedRadarLabels[3].SetForegroundBrush(mBr);
                _cachedStatusOk.SetForegroundBrush(okBr);
                _cachedStatusInit.SetForegroundBrush(mBr);
                _cachedStatusWait.SetForegroundBrush(mBr);
                _cachedComputeFooter.SetForegroundBrush(mBr);
            }

            double cardW = 284.0 * _scale;
            double cardH = 148.0 * _scale;
            double padX = Math.Max(46.0 * _scale, _width * 0.042);
            double topY = _height * 0.23;
            double botY = _height * 0.56;
            double slide = (1.0 - enter) * 34.0 * _scale;

            Rect rTL = new Rect(padX - slide, topY, cardW, cardH);
            DrawGlassCardShell(dc, rTL, bIdx, 0);
            DrawGravityWaveformContent(dc, rTL, t, bIdx);

            Rect rBL = new Rect(padX - slide, botY, cardW, cardH);
            DrawGlassCardShell(dc, rBL, bIdx, 1);
            DrawComputeMatrixContent(dc, rBL, t, bIdx);

            Rect rTR = new Rect(_width - padX - cardW + slide, topY, cardW, cardH);
            DrawGlassCardShell(dc, rTR, bIdx, 2);
            DrawVectorTelemetryContent(dc, rTR, t, bIdx);

            Rect rBR = new Rect(_width - padX - cardW + slide, botY, cardW, cardH);
            DrawGlassCardShell(dc, rBR, bIdx, 3);
            DrawSubsystemChecklistContent(dc, rBR, t, bIdx);
        }

        private void DrawGlassCardShell(DrawingContext dc, Rect r, int bIdx, int cardIdx)
        {
            double rad = 12.0 * _scale;
            dc.DrawRoundedRectangle(_hudBgBuckets[bIdx], _hudBorderBuckets[bIdx], r, rad, rad);
            dc.DrawLine(_hudTopRimBuckets[bIdx], new Point(r.Left + rad, r.Top + 1.0), new Point(r.Right - rad, r.Top + 1.0));

            double tick = 7.0 * _scale;
            Pen tk = _hudTickBuckets[bIdx];
            dc.DrawLine(tk, new Point(r.Left, r.Top + tick), new Point(r.Left, r.Top));
            dc.DrawLine(tk, new Point(r.Left, r.Top), new Point(r.Left + tick, r.Top));
            dc.DrawLine(tk, new Point(r.Right - tick, r.Bottom), new Point(r.Right, r.Bottom));
            dc.DrawLine(tk, new Point(r.Right, r.Bottom - tick), new Point(r.Right, r.Bottom));

            dc.DrawText(_cachedHudTitles[cardIdx], new Point(r.Left + 14.0 * _scale, r.Top + 11.0 * _scale));
            FormattedText tagFt = _cachedHudTags[cardIdx];
            dc.DrawText(tagFt, new Point(r.Right - 14.0 * _scale - tagFt.Width, r.Top + 11.5 * _scale));
        }

        private void DrawGravityWaveformContent(DrawingContext dc, Rect r, double t, int bIdx)
        {
            double x0 = r.Left + 16.0 * _scale;
            double w = r.Width - 32.0 * _scale;
            double midY = r.Top + r.Height * 0.56;
            double damp = 1.0 - SmoothStep(8.4, 11.5, t);
            double maxAmp = (24.0 * damp + 2.2) * _scale;

            dc.DrawLine(_hudZeroLineBuckets[bIdx], new Point(x0, midY), new Point(x0 + w, midY));

            StreamGeometry waveGeo = new StreamGeometry();
            using (StreamGeometryContext ctx = waveGeo.Open())
            {
                int steps = 36;
                for (int i = 0; i <= steps; i++)
                {
                    double u = (double)i / steps;
                    double env = Math.Sin(u * Math.PI);
                    double yOff = (Math.Sin(u * 11.0 - t * 6.5) * 0.65 + Math.Cos(u * 21.0 + t * 4.2) * 0.35) * maxAmp * env;
                    Point pt = new Point(x0 + u * w, midY + yOff);
                    if (i == 0) ctx.BeginFigure(pt, false, false);
                    else ctx.LineTo(pt, true, false);
                }
            }
            waveGeo.Freeze();
            dc.DrawGeometry(null, _hudWaveBuckets[bIdx], waveGeo);

            double gVal = Math.Round(9.81 * (1.0 - EaseOutCubic(Clamp01((t - 8.2) / 2.9))), 2);
            string read = string.Format(CultureInfo.InvariantCulture, "FIELD: {0:F2} m/s\u00B2   DELTA: {1:F2} g", gVal, gVal / 9.81);
            if (!string.Equals(read, _lastFieldStr, StringComparison.Ordinal) || bIdx != _lastFieldBucket || _cachedFieldFt == null)
            {
                _lastFieldStr = read;
                _lastFieldBucket = bIdx;
                _cachedFieldFt = CreateFormattedText(read, _monoTypeface, 9.2 * _scale, _hudMutedBrushBuckets[bIdx]);
            }
            dc.DrawText(_cachedFieldFt, new Point(x0, r.Bottom - 24.0 * _scale));
        }

        private void DrawComputeMatrixContent(DrawingContext dc, Rect r, double t, int bIdx)
        {
            double x0 = r.Left + 16.0 * _scale;
            double w = r.Width - 32.0 * _scale;
            double baseY = r.Bottom - 32.0 * _scale;
            double maxH = 62.0 * _scale;
            int bars = 16;
            double slot = w / bars;
            double barW = slot * 0.58;

            Brush brNorm = _hudBarBuckets[bIdx];
            Brush brActive = _hudBarActiveBuckets[bIdx];

            for (int b = 0; b < bars; b++)
            {
                double wave = 0.38 + 0.62 * Math.Abs(Math.Sin(t * 3.4 + b * 0.55) * Math.Cos(t * 1.9 - b * 0.31));
                double bh = maxH * wave;
                Brush br = (b % 4 == 0 || wave > 0.78) ? brActive : brNorm;
                dc.DrawRectangle(br, null, new Rect(x0 + b * slot, baseY - bh, barW, bh));
            }
            dc.DrawText(_cachedComputeFooter, new Point(x0, r.Bottom - 23.0 * _scale));
        }

        private void DrawVectorTelemetryContent(DrawingContext dc, Rect r, double t, int bIdx)
        {
            double rCx = r.Left + 56.0 * _scale;
            double rCy = r.Top + r.Height * 0.58;
            double rad = 34.0 * _scale;

            Pen zp = _hudZeroLineBuckets[bIdx];
            dc.DrawEllipse(null, zp, new Point(rCx, rCy), rad, rad);
            dc.DrawEllipse(null, zp, new Point(rCx, rCy), rad * 0.58, rad * 0.58);
            dc.DrawLine(zp, new Point(rCx - rad - 5 * _scale, rCy), new Point(rCx + rad + 5 * _scale, rCy));
            dc.DrawLine(zp, new Point(rCx, rCy - rad - 5 * _scale), new Point(rCx, rCy + rad + 5 * _scale));

            double ang = t * 1.85;
            Point tip = new Point(rCx + Math.Cos(ang) * rad, rCy + Math.Sin(ang) * rad);
            dc.DrawLine(_hudWaveBuckets[bIdx], new Point(rCx, rCy), tip);
            dc.DrawEllipse(_nodeCoreBuckets[bIdx], null, tip, 2.5 * _scale, 2.5 * _scale);

            double textX = r.Left + 108.0 * _scale;
            double ty = r.Top + 42.0 * _scale;
            for (int i = 0; i < 4; i++)
            {
                dc.DrawText(_cachedRadarLabels[i], new Point(textX, ty + i * 20.0 * _scale));
            }
        }

        private void DrawSubsystemChecklistContent(DrawingContext dc, Rect r, double t, int bIdx)
        {
            double[] readyAt = new double[] { 8.65, 9.20, 9.75, 10.30, 10.90 };
            double x0 = r.Left + 18.0 * _scale;
            double y0 = r.Top + 37.0 * _scale;
            double stepY = 20.0 * _scale;

            Brush dotDone = _hudDotDoneBuckets[bIdx];
            Brush dotActive = _hudDotActiveBuckets[bIdx];
            Brush dotPending = _hudDotPendingBuckets[bIdx];
            Brush txtDone = _hudValueBrushBuckets[bIdx];
            Brush txtMuted = _hudMutedBrushBuckets[bIdx];

            for (int i = 0; i < _cachedSubsystemItems.Length; i++)
            {
                bool done = t >= readyAt[i];
                bool active = !done && t >= (readyAt[i] - 0.55);
                Brush dotBrush = done ? dotDone : (active ? dotActive : dotPending);
                double dotR = 3.0 * _scale;
                double ly = y0 + i * stepY;

                dc.DrawEllipse(dotBrush, null, new Point(x0 + dotR, ly + 5.5 * _scale), dotR, dotR);
                FormattedText itemFt = _cachedSubsystemItems[i];
                itemFt.SetForegroundBrush(done ? txtDone : txtMuted);
                dc.DrawText(itemFt, new Point(x0 + 14.0 * _scale, ly));

                FormattedText stFt = done ? _cachedStatusOk : (active ? _cachedStatusInit : _cachedStatusWait);
                dc.DrawText(stFt, new Point(r.Right - 16.0 * _scale - stFt.Width, ly));
            }
        }

        private void RenderCinemaChrome(DrawingContext dc, double t, double totalDuration)
        {
            double fade = SmoothStep(0.15, 0.9, t) * (1.0 - SmoothStep(13.85, 15.0, t));
            int bIdx = AlphaBucket(fade * 0.88);
            if (bIdx == 0) return;

            if (bIdx != _lastChromeTextBucket)
            {
                _lastChromeTextBucket = bIdx;
                Brush cb = _chromeTextBrushBuckets[bIdx];
                _cachedChromeLeft.SetForegroundBrush(cb);
                _cachedPreviewHint.SetForegroundBrush(cb);
            }

            double pad = 38.0 * _scale;
            double arm = 10.0 * _scale;
            Pen tk = _chromeTickBuckets[bIdx];

            double[,] corners = new double[,]
            {
                { pad, pad }, { _width - pad, pad },
                { pad, _height - pad }, { _width - pad, _height - pad }
            };
            for (int i = 0; i < 4; i++)
            {
                double x = corners[i, 0];
                double y = corners[i, 1];
                dc.DrawLine(tk, new Point(x - arm, y), new Point(x + arm, y));
                dc.DrawLine(tk, new Point(x, y - arm), new Point(x, y + arm));
            }

            dc.DrawText(_cachedChromeLeft, new Point(pad + 18.0 * _scale, pad - 5.5 * _scale));

            double quantT = Math.Floor(t * 15.0) / 15.0;
            string rightStr = string.Format(CultureInfo.InvariantCulture, "T+{0:00.00}s / {1:00.0}s  \u00B7  {2}", quantT, totalDuration, _resText);
            if (!string.Equals(rightStr, _lastTimeStr, StringComparison.Ordinal) || bIdx != _lastTimeBucket || _cachedTimeFt == null)
            {
                _lastTimeStr = rightStr;
                _lastTimeBucket = bIdx;
                _cachedTimeFt = CreateFormattedText(rightStr, _monoTypeface, 9.6 * _scale, _chromeTextBrushBuckets[bIdx]);
            }
            dc.DrawText(_cachedTimeFt, new Point(_width - pad - 18.0 * _scale - _cachedTimeFt.Width, pad - 5.5 * _scale));

            if (_previewMode)
            {
                dc.DrawText(_cachedPreviewHint, new Point((_width - _cachedPreviewHint.Width) * 0.5, _height - pad - 4.5 * _scale));
            }
        }

        private FormattedText CreateFormattedText(string text, Typeface tf, double size, Brush brush)
        {
            return new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                tf,
                size,
                brush);
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
        private static double EaseInOutQuad(double t) { return t < 0.5 ? 2.0 * t * t : 1.0 - Math.Pow(-2.0 * t + 2.0, 2.0) * 0.5; }
        private static double EaseOutQuad(double t) { return 1.0 - (1.0 - t) * (1.0 - t); }
        private static double EaseOutCubic(double t) { return 1.0 - Math.Pow(1.0 - t, 3.0); }
        private static double EaseOutExpo(double t) { return t >= 1.0 ? 1.0 : 1.0 - Math.Pow(2.0, -10.0 * t); }
        private static double EaseOutBack(double t)
        {
            double c1 = 1.70158;
            double c3 = c1 + 1.0;
            return 1.0 + c3 * Math.Pow(t - 1.0, 3.0) + c1 * Math.Pow(t - 1.0, 2.0);
        }
    }
}
