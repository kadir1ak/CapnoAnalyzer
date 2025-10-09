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
    /// 💨 PC hesaplanan ve cihaz CO₂ verisi için yüksek-performanslı gerçek zamanlı grafik.
    /// - Producer/consumer mimari, sabit FPS toplu flush
    /// - Otomatik Y: görünür pencere genliği ±%Padding
    /// - Eski noktaları binary-search ile kırpar, X ekseni kaydırır
    /// </summary>
    public class CalculatedGasChartModel : BindableBase, IDisposable
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
            set { var v = Math.Clamp(value, 5, 120); if (_maxFps != v) { _maxFps = v; RestartTimer(); OnPropertyChanged(); } }
        }

        // Otomatik / Sabit Y
        public bool UseFixedY
        {
            get => _useFixedY;
            set { if (_useFixedY != value) { _useFixedY = value; ApplyY(); } }
        }
        public double YMin { get => _yMin; set { _yMin = value; if (UseFixedY) ApplyY(); } }
        public double YMax { get => _yMax; set { _yMax = value; if (UseFixedY) ApplyY(); } }
        public double AutoYPaddingPercent { get => _autoPadPct; set { _autoPadPct = Math.Max(0, value); } }
        public bool ClampYToZero { get; set; } = true;

        // Outlier politikası (sabit Y açıkken)
        public bool DropOutsideFixedRange { get; set; } = false;

        // ---- Public API ----
        public PlotModel PlotModel { get; private set; }

        public void AddPcCalculatedData(double time, double gasValue) => Enqueue(_pcQueue, time, gasValue);
        public void AddDeviceCO2Data(double time, double co2Value) => Enqueue(_devQueue, time, co2Value);

        public void Reset()
        {
            _pcQueue.Clear(); _devQueue.Clear();
            _pcBuf.Clear(); _devBuf.Clear();
            _pcSeries.Points.Clear(); _devSeries.Points.Clear();
            _lastTimeSeen = double.NaN;

            _xAxis.Minimum = 0;
            _xAxis.Maximum = TimeWindowSeconds;
            ApplyY();
            PlotModel.InvalidatePlot(true);
        }

        public CalculatedGasChartModel(double timeWindowSeconds = 10, int maxFps = 40)
        {
            _timeWindowSeconds = Math.Max(1, timeWindowSeconds);
            _maxFps = Math.Clamp(maxFps, 5, 120);
            InitPlot();
            StartTimer();
        }

        // ---- Private fields ----
        private readonly ConcurrentQueue<DataPoint> _pcQueue = new();
        private readonly ConcurrentQueue<DataPoint> _devQueue = new();

        private readonly List<DataPoint> _pcBuf = new(capacity: 20000);
        private readonly List<DataPoint> _devBuf = new(capacity: 20000);

        private LineSeries _pcSeries = null!;
        private LineSeries _devSeries = null!;
        private LinearAxis _xAxis = null!;
        private LinearAxis _yAxis = null!;

        private double _timeWindowSeconds;
        private int _maxFps;
        private DispatcherTimer? _timer;
        private double _lastTimeSeen;
        private double _lastEnqueuedTime = double.NaN;

        private bool _useFixedY = false;
        private double _yMin = 0, _yMax = 6;
        private double _autoPadPct = 25.0;

        // ---- Init ----
        private void InitPlot()
        {
            PlotModel = new PlotModel
            {
                TitleFontSize = 12,
                Title = "Hesaplanan Gaz Konsantrasyonu",
                IsLegendVisible = true
            };

            _xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Zaman (sn)",
                FontSize = 12,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            };
            _yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Gaz Konsantrasyonu (%)",
                FontSize = 12,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            };
            PlotModel.Axes.Add(_xAxis);
            PlotModel.Axes.Add(_yAxis);

            _pcSeries = new LineSeries
            {
                Title = "PC Calculated Gas",
                FontSize = 12,
                TrackerFormatString = "{0}\nTime: {2:0.000}\nValue: {4:0.000}",
                Color = OxyColor.Parse("#4CAF50"),
                StrokeThickness = 2
            };
            _devSeries = new LineSeries
            {
                Title = "Device CO₂ Value",
                FontSize = 12,
                TrackerFormatString = "{0}\nTime: {2:0.000}\nValue: {4:0.000}",
                Color = OxyColor.Parse("#2196F3"),
                StrokeThickness = 1.5,
                LineStyle = LineStyle.Dash
            };

            PlotModel.Series.Add(_pcSeries);
            PlotModel.Series.Add(_devSeries);

            ApplyY();
            OnPropertyChanged(nameof(PlotModel));
        }

        // ---- Producer helper ----
        private void Enqueue(ConcurrentQueue<DataPoint> q, double t, double v)
        {
            if (double.IsNaN(t) || double.IsInfinity(t) || double.IsNaN(v) || double.IsInfinity(v)) return;

            // Zaman geri sardıysa temizle
            if (!double.IsNaN(_lastEnqueuedTime) && t < _lastEnqueuedTime) Reset();
            _lastEnqueuedTime = t;

            if (UseFixedY && DropOutsideFixedRange && (v < YMin || v > YMax)) return;

            q.Enqueue(new DataPoint(t, v));
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

        // ---- UI tick ----
        private void OnTick(object? s, EventArgs e)
        {
            while (_pcQueue.TryDequeue(out var p)) { _pcBuf.Add(p); _lastTimeSeen = Math.Max(_lastTimeSeen, p.X); }
            while (_devQueue.TryDequeue(out var d)) { _devBuf.Add(d); _lastTimeSeen = Math.Max(_lastTimeSeen, d.X); }

            if (_pcBuf.Count == 0 && _devBuf.Count == 0) return;

            double cutoff = _lastTimeSeen - _timeWindowSeconds;
            if (cutoff > 0)
            {
                int iPc = LowerBound(_pcBuf, cutoff); if (iPc > 0) _pcBuf.RemoveRange(0, iPc);
                int iDv = LowerBound(_devBuf, cutoff); if (iDv > 0) _devBuf.RemoveRange(0, iDv);
            }

            _pcSeries.Points.Clear(); _pcSeries.Points.AddRange(_pcBuf);
            _devSeries.Points.Clear(); _devSeries.Points.AddRange(_devBuf);

            if (_lastTimeSeen > 0)
            {
                _xAxis.Minimum = Math.Max(0, _lastTimeSeen - _timeWindowSeconds);
                _xAxis.Maximum = _lastTimeSeen;
            }

            if (!UseFixedY) AutoFitYFromBuffers(_pcBuf, _devBuf);

            PlotModel.InvalidatePlot(false);
        }

        // ---- Y ekseni hesaplama ----
        private void AutoFitYFromBuffers(List<DataPoint> a, List<DataPoint> b)
        {
            double min = double.PositiveInfinity, max = double.NegativeInfinity;

            if (a.Count > 0) { for (int i = 0; i < a.Count; i++) { var y = a[i].Y; if (y < min) min = y; if (y > max) max = y; } }
            if (b.Count > 0) { for (int i = 0; i < b.Count; i++) { var y = b[i].Y; if (y < min) min = y; if (y > max) max = y; } }

            if (double.IsInfinity(min) || double.IsInfinity(max) || min == max) return;

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

        public void Dispose()
        {
            if (_timer != null) { _timer.Tick -= OnTick; _timer.Stop(); _timer = null; }
        }
    }
}
