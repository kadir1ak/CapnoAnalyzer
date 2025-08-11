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
    /// 📈 Yüksek frekans veriler için optimize edilmiş OxyPlot çizim modeli.
    /// - Veri geldiği anda Enqueue (lock-free, düşük GC)
    /// - UI tarafında DispatcherTimer ile sabit FPS’te toplu flush
    /// - Zaman penceresi kadar veri saklanır (kaydırmalı görünüm)
    /// </summary>
    public class SensorChartModel : BindableBase, IDisposable, IHighFreqPlot
    {
        // ---- Public API ----
        public PlotModel PlotModel { get; private set; }

        /// <summary>Zaman penceresi (s)</summary>
        public double TimeWindowSeconds
        {
            get => _timeWindowSeconds;
            set
            {
                var v = Math.Max(1, value);
                if (Math.Abs(_timeWindowSeconds - v) > double.Epsilon)
                {
                    _timeWindowSeconds = v;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>UI refresh FPS (5–120)</summary>
        public int MaxFps
        {
            get => _maxFps;
            set
            {
                var v = Clamp(value, 5, 120);
                if (_maxFps != v)
                {
                    _maxFps = v;
                    RestartTimer();
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Parser bu metodu çağırır (thread-safe)</summary>
        public void Enqueue(double time, double gas, double reference)
        {
            _queue.Enqueue(new Sample(time, gas, reference));
        }

        /// <summary>Geriye dönük uyumluluk</summary>
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

        // İç buffer’lar – render maliyeti düşük
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
                LineStyle = LineStyle.Solid,
                TrackerFormatString = "{0}\nTime: {2:0.000}\nValue: {4:0.000}"
            };

            PlotModel.Series.Add(_gasSeries);
            PlotModel.Series.Add(_refSeries);
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
            // Kuyruktaki tüm örnekleri çek
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
                int idxG = LowerBound(_gas, cutoff);
                if (idxG > 0) _gas.RemoveRange(0, idxG);

                int idxR = LowerBound(_ref, cutoff);
                if (idxR > 0) _ref.RemoveRange(0, idxR);
            }

            // Serileri tazele
            _gasSeries.Points.Clear();
            _gasSeries.Points.AddRange(_gas);

            _refSeries.Points.Clear();
            _refSeries.Points.AddRange(_ref);

            // X eksenini kaydır
            if (_lastTimeSeen > 0)
            {
                _xAxis.Minimum = Math.Max(0, _lastTimeSeen - _timeWindowSeconds);
                _xAxis.Maximum = _lastTimeSeen;
            }

            PlotModel.InvalidatePlot(false);
        }

        // Sorted listte ilk X >= cutoff index'i (binary search)
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

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public void Dispose()
        {
            if (_timer != null)
            {
                _timer.Tick -= OnTick;
                _timer.Stop();
                _timer = null;
            }
        }
    }
}
