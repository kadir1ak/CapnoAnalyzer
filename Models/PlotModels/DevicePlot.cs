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

        /// <summary>
        /// Grafik modeli (PlotModel) özelliği.
        /// </summary>
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
            // **Her cihaz için ayrı PlotModel nesnesi oluşturuluyor**
            PlotModel = new PlotModel { Title = "Sensör Verileri" };
            PlotModel.Series.Clear();

            // **X Ekseni (Zaman)**
            PlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Zaman (sn)",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            });

            // **Y Ekseni (Sensör Değerleri)**
            PlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "ADC Değeri",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            });

            // **Gaz Sensörü Serisi**
            _gasSeries = new LineSeries
            {
                Title = "Gas Sensor",
                Color = OxyColors.Orange,
                StrokeThickness = 2
            };
            PlotModel.Series.Add(_gasSeries);

            // **Referans Sensörü Serisi**
            _referenceSeries = new LineSeries
            {
                Title = "Reference Sensor",
                Color = OxyColors.Green,
                StrokeThickness = 2
            };
            PlotModel.Series.Add(_referenceSeries);

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
            try
            {
                if (PlotModel == null || _gasSeries == null || _referenceSeries == null)
                {
                    Debug.WriteLine("PlotModel veya Series eksik!");
                    return;
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    // **Yeni veri noktalarını ekleyelim**
                    _gasSeries.Points.Add(new DataPoint(time, gasValue));
                    _referenceSeries.Points.Add(new DataPoint(time, referenceValue));

                    // **Eski noktaları kaldır (100 noktadan fazla veri tutmayalım)**
                    if (_gasSeries.Points.Count > 100)
                        _gasSeries.Points.RemoveAt(0);

                    if (_referenceSeries.Points.Count > 100)
                        _referenceSeries.Points.RemoveAt(0);

                    // **Grafiği yenile**
                    PlotModel.InvalidatePlot(true);
                    OnPropertyChanged(nameof(PlotModel));
                });
            }
            catch (Exception ex)
            { 
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        /// <summary>
        /// Grafiği temizler ve sıfırlar.
        /// </summary>
        public void ClearPlot()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _gasSeries.Points.Clear();
                _referenceSeries.Points.Clear();
                PlotModel.InvalidatePlot(true);
            });
        }
    }
}
