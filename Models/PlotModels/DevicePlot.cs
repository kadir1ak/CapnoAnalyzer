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

        /// <summary>
        /// Grafik modeli (PlotModel) özelliği.
        /// </summary>
        public PlotModel PlotModel
        {
            get => _plotModel;
            set
            {
                _plotModel = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Constructor: Grafik modelini başlatır.
        /// </summary>
        public DevicePlot()
        {
            InitializePlotModel();
        }

        /// <summary>
        /// Grafik modelini başlatır ve serileri ekler.
        /// </summary>
        public void InitializePlotModel()
        {
            PlotModel = new PlotModel { Title = "Sensör Verileri" };
            PlotModel.Series.Clear();

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
            var gasSeries = new LineSeries
            {
                Title = "Gas Sensor",
                Color = OxyColors.Orange,
                StrokeThickness = 2
            };
            PlotModel.Series.Add(gasSeries);

            // Referans Sensörü Serisi
            var referenceSeries = new LineSeries
            {
                Title = "Reference Sensor",
                Color = OxyColors.Green,
                StrokeThickness = 2
            };
            PlotModel.Series.Add(referenceSeries);

            // **Legend (Açıklama)**
            PlotModel.Legends.Add(new Legend
            {
                LegendTitle = "Sensörler",
                LegendPosition = LegendPosition.TopRight
            });

            OnPropertyChanged(nameof(PlotModel));
        }

        /// <summary>
        /// Grafik modeline veri noktası ekler.
        /// </summary>
        /// <param name="time">Zaman verisi.</param>
        /// <param name="gasValue">Gaz sensör değeri.</param>
        /// <param name="referenceValue">Referans sensör değeri.</param>
        public void AddDataPoint(double time, double gasValue, double referenceValue)
        {
            if (PlotModel == null || PlotModel.Series.Count < 2)
            {
                Debug.WriteLine("PlotModel veya Series eksik!");
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (PlotModel.Series[0] is LineSeries gasSeries && PlotModel.Series[1] is LineSeries referenceSeries)
                {
                    // Yeni veri noktalarını ekleyelim
                    gasSeries.Points.Add(new DataPoint(time, gasValue));
                    referenceSeries.Points.Add(new DataPoint(time, referenceValue));

                    // 100'den fazla noktayı koruma
                    if (gasSeries.Points.Count > 100)
                        gasSeries.Points.RemoveAt(0);

                    if (referenceSeries.Points.Count > 100)
                        referenceSeries.Points.RemoveAt(0);

                    // **Grafiği Yenile**
                    PlotModel.InvalidatePlot(true);
                    OnPropertyChanged(nameof(PlotModel));
                }
            });
        }
    }
}
