using System;
using System.Diagnostics;
using System.Windows;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using CapnoAnalyzer.Helpers;

namespace CapnoAnalyzer.Models.PlotModels
{
    public class DevicePlot : BindableBase
    {
        private PlotModel _plotModel;
        private LineSeries _gasSeries;
        private LineSeries _referenceSeries;

        public PlotModel PlotModel
        {
            get => _plotModel;
            set
            {
                if (_plotModel != value)
                {
                    _plotModel = value;
                    OnPropertyChanged();
                }
            }
        }

        public DevicePlot()
        {
            InitializePlotModel();
        }

        public void InitializePlotModel()
        {
            PlotModel = new PlotModel { Title = "Sensör Verileri" };

            // X Ekseni (Zaman)
            PlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Zaman (sn)",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            });

            // Y Ekseni (Sensör Değerleri)
            PlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "ADC Değeri",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            });

            // Gaz Sensörü Serisi
            _gasSeries = new LineSeries
            {
                Title = "Gas Sensor",
                Color = OxyColors.Orange,
                StrokeThickness = 2
            };
            PlotModel.Series.Add(_gasSeries);

            // Referans Sensörü Serisi
            _referenceSeries = new LineSeries
            {
                Title = "Reference Sensor",
                Color = OxyColors.Green,
                StrokeThickness = 2
            };
            PlotModel.Series.Add(_referenceSeries);

            // Legend (Açıklama)
            PlotModel.Legends.Add(new Legend
            {
                LegendTitle = "Sensörler",
                LegendPosition = LegendPosition.TopRight
            });

            OnPropertyChanged(nameof(PlotModel));
        }

        public void AddDataPoint(double time, double gasValue, double referenceValue)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _gasSeries.Points.Add(new DataPoint(time, gasValue));
                _referenceSeries.Points.Add(new DataPoint(time, referenceValue));

                if (_gasSeries.Points.Count > 100) _gasSeries.Points.RemoveAt(0);
                if (_referenceSeries.Points.Count > 100) _referenceSeries.Points.RemoveAt(0);

                PlotModel.InvalidatePlot(true);
                OnPropertyChanged(nameof(PlotModel));
            });
        }
    }
}
