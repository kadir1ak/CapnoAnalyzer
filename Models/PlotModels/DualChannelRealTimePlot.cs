using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace CapnoAnalyzer.Models.PlotModels
{
    public sealed class DualChannelRealTimePlot : IDisposable
    {
        public PlotModel Model { get; private set; }

        private readonly LineSeries _series1;
        private readonly LineSeries _series2;

        // --- Ring Buffer (Döngüsel Tampon) ---
        private double[] _hT = new double[4096];
        private double[] _hY1 = new double[4096];
        private double[] _hY2 = new double[4096];
        private int _hStart = 0;
        private int _hCount = 0;

        // --- Producer Buffer (Gelen Veri Kuyruğu) ---
        private readonly List<double> _pT = new List<double>();
        private readonly List<double> _pY1 = new List<double>();
        private readonly List<double> _pY2 = new List<double>();
        private readonly object _gate = new object();

        // --- Decimation (Seyreltme) Çıktıları ---
        private double[] _dx = new double[2048];
        private double[] _dy1 = new double[2048];
        private double[] _dy2 = new double[2048];

        private double _timeWindowSec;
        private DispatcherTimer _timer;
        private int _maxFps = 30;
        private int _viewportWidthPx = 800;

        public DualChannelRealTimePlot(double timeWindowSeconds = 10)
        {
            _timeWindowSec = Math.Max(1, timeWindowSeconds);

            Model = new PlotModel
            {
                PlotMargins = new OxyThickness(40, 10, 40, 30),
                IsLegendVisible = true
            };

            // Legend Ayarları
            Model.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.TopRight,
                LegendOrientation = LegendOrientation.Horizontal,
                LegendPlacement = LegendPlacement.Inside,
                LegendBackground = OxyColor.FromAColor(200, OxyColors.White)
            });

            // X Ekseni (Zaman)
            Model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Zaman (sn)",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                IsZoomEnabled = false,
                IsPanEnabled = false
            });

            // Y1 Ekseni (Sol - Mavi)
            Model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Key = "Y1",
                TitleColor = OxyColor.Parse("#FF8C449E"),
                TextColor = OxyColor.Parse("#FF8C449E"),
                MajorGridlineStyle = LineStyle.Solid,
                IsZoomEnabled = false,
                IsPanEnabled = false
            });

            // Y2 Ekseni (Sağ - Kırmızı)
            Model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Right,
                Key = "Y2",
                TitleColor = OxyColor.Parse("#FFF56715"),
                TextColor = OxyColor.Parse("#FFF56715"),                
                MajorGridlineStyle = LineStyle.None,
                IsZoomEnabled = false,
                IsPanEnabled = false
            });

            _series1 = new LineSeries { Title = "Kanal 1", Color = OxyColor.Parse("#FF8C449E"), StrokeThickness = 1.5, YAxisKey = "Y1" };
            _series2 = new LineSeries { Title = "Kanal 2", Color = OxyColor.Parse("#FFF56715"), StrokeThickness = 1.5, YAxisKey = "Y2" };

            Model.Series.Add(_series1);
            Model.Series.Add(_series2);

            StartTimer();
        }

        public void Enqueue(double time, double val1, double val2)
        {
            lock (_gate)
            {
                _pT.Add(time);
                _pY1.Add(val1);
                _pY2.Add(val2);
            }
        }

        private void StartTimer()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Render);
            _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / _maxFps);
            _timer.Tick += (s, e) => RenderTick();
            _timer.Start();
        }

        private void RenderTick()
        {
            // 1. Veriyi Ring Buffer'a aktar
            lock (_gate)
            {
                if (_pT.Count > 0)
                {
                    AppendToRing(_pT, _pY1, _pY2);
                    _pT.Clear(); _pY1.Clear(); _pY2.Clear();
                }
            }

            if (_hCount <= 1) return;

            // 2. Zaman penceresini yönet
            double tmax = _hT[(_hStart + _hCount - 1) % _hT.Length];
            double tmin = tmax - _timeWindowSec;

            int drop = CountLeftToTrim(tmin);
            if (drop > 0)
            {
                _hStart = (_hStart + drop) % _hT.Length;
                _hCount -= drop;
            }

            // 3. Decimation (Veri Seyreltme)
            int targetPoints = Math.Max(200, Math.Min(_viewportWidthPx * 2, 4000));
            EnsureDecimationCapacity(targetPoints * 2);

            int count1 = MinMaxDecimateRing(_hT, _hY1, _hStart, _hCount, _dx, _dy1, targetPoints);
            UpdateSeries(_series1, _dx, _dy1, count1);

            int count2 = MinMaxDecimateRing(_hT, _hY2, _hStart, _hCount, _dx, _dy2, targetPoints);
            UpdateSeries(_series2, _dx, _dy2, count2);

            // 4. Eksenleri Güncelle
            var xAxis = (LinearAxis)Model.Axes[0];
            xAxis.Minimum = tmin;
            xAxis.Maximum = tmax;

            AutoScaleAxis((LinearAxis)Model.Axes[1], _series1);
            AutoScaleAxis((LinearAxis)Model.Axes[2], _series2);

            Model.InvalidatePlot(false);
        }

        // --- Yardımcı Metotlar ---
        private void UpdateSeries(LineSeries series, double[] x, double[] y, int count)
        {
            series.Points.Clear();
            for (int i = 0; i < count; i++) series.Points.Add(new DataPoint(x[i], y[i]));
        }

        private void AutoScaleAxis(LinearAxis axis, LineSeries series)
        {
            if (series.Points.Count == 0) return;
            double min = double.MaxValue, max = double.MinValue;
            foreach (var p in series.Points) { if (p.Y < min) min = p.Y; if (p.Y > max) max = p.Y; }

            if (min == double.MaxValue) return;
            double range = max - min;
            if (range < 1e-6) range = 1.0;
            axis.Minimum = min - (range * 0.1);
            axis.Maximum = max + (range * 0.1);
        }

        private void AppendToRing(List<double> t, List<double> y1, List<double> y2)
        {
            int n = t.Count;
            EnsureRingCapacity(_hCount + n);
            int cap = _hT.Length;
            for (int i = 0; i < n; i++)
            {
                int idx = (_hStart + _hCount + i) % cap;
                _hT[idx] = t[i]; _hY1[idx] = y1[i]; _hY2[idx] = y2[i];
            }
            _hCount += n;
        }

        private void EnsureRingCapacity(int needed)
        {
            if (_hT.Length >= needed) return;
            int newCap = Math.Max(needed, _hT.Length * 2);
            double[] nT = new double[newCap], nY1 = new double[newCap], nY2 = new double[newCap];
            for (int i = 0; i < _hCount; i++)
            {
                int old = (_hStart + i) % _hT.Length;
                nT[i] = _hT[old]; nY1[i] = _hY1[old]; nY2[i] = _hY2[old];
            }
            _hT = nT; _hY1 = nY1; _hY2 = nY2; _hStart = 0;
        }

        private int CountLeftToTrim(double tmin)
        {
            int drop = 0, cap = _hT.Length;
            for (int k = 0; k < _hCount; k++)
            {
                if (_hT[(_hStart + k) % cap] < tmin) drop++; else break;
            }
            return drop;
        }

        private void EnsureDecimationCapacity(int size)
        {
            if (_dx.Length < size) { _dx = new double[size]; _dy1 = new double[size]; _dy2 = new double[size]; }
        }

        private static int MinMaxDecimateRing(double[] xs, double[] ys, int start, int count, double[] outX, double[] outY, int target)
        {
            if (count <= 0) return 0;
            int cap = xs.Length;
            if (count <= target)
            {
                for (int k = 0; k < count; k++) { int i = (start + k) % cap; outX[k] = xs[i]; outY[k] = ys[i]; }
                return count;
            }
            int buckets = target / 2;
            double step = (double)count / buckets;
            int outIdx = 0; double idx = 0;
            for (int b = 0; b < buckets; b++)
            {
                int s = (int)idx; idx += step; int e = Math.Min((int)idx, count - 1);
                double ymin = double.MaxValue, ymax = double.MinValue, xmin = 0, xmax = 0;
                for (int k = s; k <= e; k++)
                {
                    int i = (start + k) % cap;
                    double y = ys[i];
                    if (y < ymin) { ymin = y; xmin = xs[i]; }
                    if (y > ymax) { ymax = y; xmax = xs[i]; }
                }
                outX[outIdx] = xmin; outY[outIdx] = ymin; outIdx++;
                outX[outIdx] = xmax; outY[outIdx] = ymax; outIdx++;
            }
            return outIdx;
        }

        public void SetTitles(string t1, string t2) { _series1.Title = t1; _series2.Title = t2; }
        public void Dispose() { _timer?.Stop(); }
    }
}
