using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot;
using CapnoAnalyzer.Helpers;
using System.Windows;

namespace CapnoAnalyzer.Models.PlotModels
{
    public class SensorChartModel : BindableBase
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

        private int _plotTime = 10;
        public int PlotTime
        {
            get => _plotTime;
            set => SetProperty(ref _plotTime, value);
        }

        public SensorChartModel()
        {
            InitializePlotModel();
        }

        public void InitializePlotModel()
        {
            PlotModel = new PlotModel { TitleFontSize = 12, Title = "Sensör Verileri" };

            // X Ekseni (Zaman)
            PlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Zaman (sn)",
                FontSize = 10,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            });

            // Y Ekseni (Sensör Değerleri)
            PlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Analog Değeri",
                FontSize = 10,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            });

            // Gaz Sensörü Serisi
            _gasSeries = new LineSeries
            {
                Title = "Gas Sensor",
                FontSize = 10,
                TrackerFormatString = "{0}\nTime: {2:0.000}\nValue: {4:0.000}",
                Color = OxyColor.Parse("#4CAF50"),
                StrokeThickness = 2
            };
            PlotModel.Series.Add(_gasSeries);

            // Referans Sensörü Serisi 
            _referenceSeries = new LineSeries
            {
                Title = "Reference Sensor",
                FontSize = 10,
                TrackerFormatString = "{0}\nTime: {2:0.000}\nValue: {4:0.000}",
                Color = OxyColor.Parse("#FF5722"),
                StrokeThickness = 2
            };
            PlotModel.Series.Add(_referenceSeries);

            // Legend (Açıklama)
            //PlotModel.Legends.Add(new Legend
            //{
            //    LegendTitle = "Sensörler",
            //    LegendPosition = LegendPosition.TopRight
            //});

            OnPropertyChanged(nameof(PlotModel));
        }

        public void AddDataPoint(double time, double gasValue, double referenceValue)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _gasSeries.Points.Add(new DataPoint(time, gasValue));
                _referenceSeries.Points.Add(new DataPoint(time, referenceValue));

                // 10 saniyeden eski verileri sil (1000 ms * 10) (Zaman bazlı silme)
                _gasSeries.Points.RemoveAll((p => p.X < time - PlotTime));
                _referenceSeries.Points.RemoveAll((p => p.X < time - PlotTime));

                PlotModel.InvalidatePlot(true);
                OnPropertyChanged(nameof(PlotModel));
            });
        }
    }
}
