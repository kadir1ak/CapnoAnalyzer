using CapnoAnalyzer.Helpers;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;

namespace CapnoAnalyzer.Models.PlotModels
{
    public class CalculatedGasChartModel : BindableBase, IDisposable
    {
        public PlotModel PlotModel { get; private set; }

        public double TimeWindowSeconds { get; set; }
        public int MaxFps
        {
            get => _maxFps;
            set { var v = Math.Clamp(value, 5, 120); if (_maxFps != v) { _maxFps = v; RestartTimer(); OnPropertyChanged(); } }
        }

        private double _yRange = 0;
        public double YRange
        {
            get => _yRange;
            set
            {
                if (_yRange != value)
                {
                    _yRange = value;
                    OnPropertyChanged();
                    ApplyY();
                }
            }
        }
        /// <summary>
        /// Ana grafikten gelen zaman bilgisiyle X eksenini günceller.
        /// </summary>
        /// <param name="lastTimeSeen">Görülen en son zaman damgası.</param>
        public void UpdateAxes(double lastTimeSeen)
        {
            if (_xAxis == null) return;
            _xAxis.Minimum = Math.Max(0, lastTimeSeen - TimeWindowSeconds);
            _xAxis.Maximum = lastTimeSeen;
            PlotModel.InvalidatePlot(false);
        }

        public void AddPcCalculatedData(double time, double gasValue) => Enqueue(_pcQueue, time, gasValue);
        public void AddDeviceCO2Data(double time, double co2Value) => Enqueue(_devQueue, time, co2Value);

        public void Reset()
        {
            _pcQueue.Clear();
            _devQueue.Clear();
            _pcBuf.Clear();
            _devBuf.Clear();

            _pcSeries.Points.Clear();
            _devSeries.Points.Clear();

            _lastEnqueuedTime = double.NaN;
            _xAxis.Minimum = 0;
            _xAxis.Maximum = TimeWindowSeconds;
            ApplyY();
            PlotModel.InvalidatePlot(true);
        }

        public CalculatedGasChartModel(double timeWindowSeconds = 10, int maxFps = 60)
        {
            TimeWindowSeconds = Math.Max(1, timeWindowSeconds);
            _maxFps = Math.Clamp(maxFps, 5, 120);
            InitPlot();
            StartTimer();
        }

        private readonly ConcurrentQueue<DataPoint> _pcQueue = new();
        private readonly ConcurrentQueue<DataPoint> _devQueue = new();
        private readonly List<DataPoint> _pcBuf = new(20000);
        private readonly List<DataPoint> _devBuf = new(20000);

        private LineSeries _pcSeries = null!;
        private LineSeries _devSeries = null!;
        private LinearAxis _xAxis = null!;
        private LinearAxis _yAxis = null!;

        private int _maxFps;
        private DispatcherTimer? _timer;
        private double _lastEnqueuedTime = double.NaN;

        private readonly bool _useFixedY = true;
        private readonly double _yMin = 0, _yMax = 6;

        private void InitPlot()
        {
            PlotModel = new PlotModel { Title = "Hesaplanan Gaz Konsantrasyonu", IsLegendVisible = true, TitleFontSize = 12 };
            _xAxis = new LinearAxis { Position = AxisPosition.Bottom, Title = "Zaman (sn)", MajorGridlineStyle = LineStyle.Solid, MinorGridlineStyle = LineStyle.Dot };
            _yAxis = new LinearAxis { Position = AxisPosition.Left, Title = "Gaz Konsantrasyonu (%)", MajorGridlineStyle = LineStyle.Solid, MinorGridlineStyle = LineStyle.Dot };
            PlotModel.Axes.Add(_xAxis);
            PlotModel.Axes.Add(_yAxis);

            _pcSeries = new LineSeries { Title = "PC Calculated Gas", Color = OxyColor.Parse("#4CAF50"), StrokeThickness = 2 };
            _devSeries = new LineSeries { Title = "Device CO₂ Value", Color = OxyColor.Parse("#2196F3"), StrokeThickness = 1.5, LineStyle = LineStyle.Dash };

            PlotModel.Series.Add(_pcSeries);
            PlotModel.Series.Add(_devSeries);
            ApplyY();
        }

        private void Enqueue(ConcurrentQueue<DataPoint> q, double t, double v)
        {
            if (double.IsNaN(t) || double.IsNaN(v)) return;
            if (!double.IsNaN(_lastEnqueuedTime) && t < _lastEnqueuedTime) Reset();
            _lastEnqueuedTime = t;
            q.Enqueue(new DataPoint(t, v));
        }

        private void StartTimer()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(1000.0 / _maxFps) };
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

        // --- DEĞİŞTİRİLDİ: Bu metot artık eksenleri GÜNCELLEMİYOR ---
        private void OnTick(object? s, EventArgs e)
        {
            bool dataAdded = false;

            // Verileri kuyruklardan tampon listelere aktar
            while (_pcQueue.TryDequeue(out var p)) { _pcBuf.Add(p); dataAdded = true; }
            while (_devQueue.TryDequeue(out var d)) { _devBuf.Add(d); dataAdded = true; }

            if (!dataAdded) return;

            // Görülen son zamanı tampondaki son elemandan al (yaklaşık değer)
            double lastTimeInBuffers = _pcBuf.LastOrDefault().X;
            if (_devBuf.Any()) lastTimeInBuffers = Math.Max(lastTimeInBuffers, _devBuf.LastOrDefault().X);

            if (double.IsNaN(lastTimeInBuffers)) return;

            // Zaman penceresinin dışındaki eski verileri tamponlardan temizle
            double cutoff = lastTimeInBuffers - TimeWindowSeconds;
            if (cutoff > 0)
            {
                int iPc = LowerBound(_pcBuf, cutoff); if (iPc > 0) _pcBuf.RemoveRange(0, iPc);
                int iDv = LowerBound(_devBuf, cutoff); if (iDv > 0) _devBuf.RemoveRange(0, iDv);
            }

            // Serileri manuel olarak güncelle
            _pcSeries.Points.Clear();
            _pcSeries.Points.AddRange(_pcBuf);

            _devSeries.Points.Clear();
            _devSeries.Points.AddRange(_devBuf);

            // Eksen güncellemesi buradan kaldırıldı. Artık UpdateAxes metodu ile dışarıdan yapılacak.
            // PlotModel.InvalidatePlot(false) çağrısı da UpdateAxes içine taşındı.
        }

        private void ApplyY()
        {
            if (_yAxis == null) return;
            
            if (_yRange > 0)
            {
                // YRange > 0: Osiloskop Merkezi Sabit (0-6 fixed için 3 baz alındı vs, veya AutoCenter)
                // Mevcut verilerin merkezini bulalım
                double minY = double.PositiveInfinity;
                double maxY = double.NegativeInfinity;
                foreach(var p in _pcBuf) { if(p.Y < minY) minY = p.Y; if(p.Y > maxY) maxY = p.Y; }
                foreach(var p in _devBuf) { if(p.Y < minY) minY = p.Y; if(p.Y > maxY) maxY = p.Y; }

                if (double.IsInfinity(minY)) { minY = 0; maxY = 6; }
                double centerY = (minY + maxY) / 2.0;

                _yAxis.Minimum = centerY - (_yRange / 2.0);
                _yAxis.Maximum = centerY + (_yRange / 2.0);
            }
            else
            {
                if (_useFixedY) { _yAxis.Minimum = _yMin; _yAxis.Maximum = _yMax; }
                else { _yAxis.Reset(); }
            }
            PlotModel?.InvalidatePlot(true);
        }

        private static int LowerBound(List<DataPoint> list, double cutoff)
        {
            int lo = 0, hi = list.Count;
            while (lo < hi) { int mid = (lo + hi) >> 1; if (list[mid].X < cutoff) lo = mid + 1; else hi = mid; }
            return lo;
        }

        public void Dispose() { if (_timer != null) { _timer.Stop(); _timer.Tick -= OnTick; _timer = null; } }
    }
}
