using CapnoAnalyzer.Helpers;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Windows.Threading;

namespace CapnoAnalyzer.Models.PlotModels
{
    /// <summary>
    /// 📈 Yüksek frekans veriler için optimize edilmiş, gerçek zamanlı sensör grafiği.
    /// - Enqueue: thread-safe, düşük GC
    /// - UI: sabit FPS'te toplu flush
    /// - Otomatik Y: görünür pencere genliğine ±%Padding
    /// </summary>
    public class SensorChartModel : BindableBase, IDisposable, IHighFreqPlot
    {
        // ---- Genel ayarlar ----
        public double TimeWindowSeconds
        {
            get => _timeWindowSeconds;
            set { var v = Math.Max(1, value); if (Math.Abs(_timeWindowSeconds - v) > double.Epsilon) { _timeWindowSeconds = v; OnPropertyChanged(); } }
        }
        public int MaxFps
        {
            get => _maxFps;
            set { var v = Clamp(value, 5, 120); if (_maxFps != v) { _maxFps = v; RestartTimer(); OnPropertyChanged(); } }
        }

        // Otomatik / Sabit Y
        public bool UseFixedY
        {
            get => _useFixedY;
            set { if (_useFixedY != value) { _useFixedY = value; ApplyY(); } }
        }
        public double YMin { get => _yMin; set { _yMin = value; if (UseFixedY) ApplyY(); } }
        public double YMax { get => _yMax; set { _yMax = value; if (UseFixedY) ApplyY(); } }

        /// <summary>Otomatik Y modunda, görünür penceredeki (min,max) için padding yüzdesi.</summary>
        public double AutoYPaddingPercent { get => _autoPadPct; set { _autoPadPct = Math.Max(0, value); } }

        /// <summary>Otomatik Y alt sınırını 0’ın altına düşürmemek için true.</summary>
        public bool ClampYToZero { get; set; } = true;

        // ---- Public API (producer) ----
        public void Enqueue(double time, double gas, double reference) =>
            _queue.Enqueue(new Sample(time, gas, reference));

        public PlotModel PlotModel { get; private set; }

        // Geriye dönük uyumluluk: eski çağrılar için
        public void AddDataPoint(double time, double gas, double reference) => Enqueue(time, gas, reference);

        public SensorChartModel(double timeWindowSeconds = 10, int maxFps = 40)
        {
            _timeWindowSeconds = Math.Max(1, timeWindowSeconds);
            _maxFps = Clamp(maxFps, 5, 120);
            InitPlot();
            StartTimer();
        }

        // ---- Private fields ----
        private readonly ConcurrentQueue<Sample> _queue = new();
        private readonly List<DataPoint> _gas = new(capacity: 20000);
        private readonly List<DataPoint> _ref = new(capacity: 20000);

        private LineSeries _gasSeries = null!;
        private LineSeries _refSeries = null!;
        private LinearAxis _xAxis = null!;
        private LinearAxis _yAxis = null!;

        private double _timeWindowSeconds;
        private int _maxFps;
        private DispatcherTimer? _timer;
        private double _lastTimeSeen;

        private bool _useFixedY = false;
        private double _yMin = 0, _yMax = 1;
        private double _autoPadPct = 25.0;

        private readonly struct Sample
        {
            public readonly double T, G, R;
            public Sample(double t, double g, double r) { T = t; G = g; R = r; }
        }

        // ---- Init ----
        private void InitPlot()
        {
            PlotModel = new PlotModel { Title = "Sensör Verileri", TitleFontSize = 12 };

            _xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Zaman (s)",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            };
            _yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Analog Değer",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            };
            PlotModel.Axes.Add(_xAxis);
            PlotModel.Axes.Add(_yAxis);

            _gasSeries = new LineSeries
            {
                Title = "Gas",
                StrokeThickness = 1.8,
                TrackerFormatString = "{0}\nTime: {2:0.000}\nValue: {4:0.000}"
            };
            _refSeries = new LineSeries
            {
                Title = "Ref",
                StrokeThickness = 1.4,
                TrackerFormatString = "{0}\nTime: {2:0.000}\nValue: {4:0.000}"
            };
            PlotModel.Series.Add(_gasSeries);
            PlotModel.Series.Add(_refSeries);

            ApplyY(); // başlangıç
        }

        private void StartTimer()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(1000.0 / _maxFps)
            };
            _timer.Tick += OnTick;
            _timer.Start();
        }
        private void RestartTimer()
        {
            if (_timer == null) return;
            _timer.Stop();
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / _maxFps);
            _timer.Start();
        }

        // ---- UI tick: toplu flush ----
        private void OnTick(object? sender, EventArgs e)
        {
            while (_queue.TryDequeue(out var s))
            {
                _lastTimeSeen = s.T;
                _gas.Add(new DataPoint(s.T, s.G));
                _ref.Add(new DataPoint(s.T, s.R));
            }
            if (_gas.Count == 0 && _ref.Count == 0) return;

            // Eski noktaları kırp (time window)
            double cutoff = _lastTimeSeen - _timeWindowSeconds;
            if (cutoff > 0)
            {
                int iG = LowerBound(_gas, cutoff); if (iG > 0) _gas.RemoveRange(0, iG);
                int iR = LowerBound(_ref, cutoff); if (iR > 0) _ref.RemoveRange(0, iR);
            }

            // Serileri tazele
            _gasSeries.Points.Clear(); _gasSeries.Points.AddRange(_gas);
            _refSeries.Points.Clear(); _refSeries.Points.AddRange(_ref);

            // X ekseni kaydır
            if (_lastTimeSeen > 0)
            {
                _xAxis.Minimum = Math.Max(0, _lastTimeSeen - _timeWindowSeconds);
                _xAxis.Maximum = _lastTimeSeen;
            }

            // Otomatik Y (gaz+ref birlikte)
            if (!UseFixedY) AutoFitYFromBuffers(_gas, _ref);

            PlotModel.InvalidatePlot(false);
        }

        // ---- Y ekseni hesaplama ----
        private void AutoFitYFromBuffers(List<DataPoint> a, List<DataPoint> b)
        {
            double min = double.PositiveInfinity, max = double.NegativeInfinity;

            if (a.Count > 0) { min = Math.Min(min, a[0].Y); max = Math.Max(max, a[0].Y); }
            for (int i = 1; i < a.Count; i++) { var y = a[i].Y; if (y < min) min = y; if (y > max) max = y; }

            if (b.Count > 0) { min = Math.Min(min, b[0].Y); max = Math.Max(max, b[0].Y); }
            for (int i = 1; i < b.Count; i++) { var y = b[i].Y; if (y < min) min = y; if (y > max) max = y; }

            if (double.IsInfinity(min) || double.IsInfinity(max) || min == max) { return; }

            double amp = max - min;
            double pad = amp * (_autoPadPct / 100.0);
            double y0 = min - pad;
            double y1 = max + pad;
            if (ClampYToZero) y0 = Math.Max(0, y0);

            _yAxis.Minimum = y0;
            _yAxis.Maximum = y1;
        }

        private void ApplyY()
        {
            if (_yAxis == null) return;
            if (UseFixedY) { _yAxis.Minimum = YMin; _yAxis.Maximum = YMax; _yAxis.Zoom(YMin, YMax); }
            else { _yAxis.Minimum = double.NaN; _yAxis.Maximum = double.NaN; }
            PlotModel?.InvalidatePlot(false);
        }

        // ---- utils ----
        private static int LowerBound(List<DataPoint> list, double cutoff)
        {
            int lo = 0, hi = list.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (list[mid].X < cutoff) lo = mid + 1; else hi = mid;
            }
            return lo;
        }
        private static int Clamp(int v, int mn, int mx) => v < mn ? mn : (v > mx ? mx : v);

        public void Dispose()
        {
            if (_timer != null) { _timer.Tick -= OnTick; _timer.Stop(); _timer = null; }
        }
    }
}
