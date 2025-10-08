using CapnoAnalyzer.Helpers;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System;
using System.Windows;

namespace CapnoAnalyzer.Models.PlotModels
{
    public class CalculatedGasChartModel : BindableBase
    {
        private PlotModel _plotModel;
        private LineSeries _pcCalculatedSeries; // PC'de hesaplanan seri
        private LineSeries _deviceCo2Series;    // Cihazdan gelen CO2 serisi

        public PlotModel PlotModel
        {
            get => _plotModel;
            set { if (_plotModel != value) { _plotModel = value; OnPropertyChanged(); } }
        }

        private int _plotTime = 10;
        public int PlotTime
        {
            get => _plotTime;
            set => SetProperty(ref _plotTime, value);
        }

        public CalculatedGasChartModel() => InitializePlotModel();

        public void InitializePlotModel()
        {
            PlotModel = new PlotModel
            {
                TitleFontSize = 12,
                Title = "Hesaplanan Gaz Konsantrasyonu",
                IsLegendVisible = true // Bu satır legend'ı görünür yapmak için yeterlidir.
                                       // LegendPosition = LegendPosition.TopRight, // HATA VEREN BU SATIRI SİLİN VEYA YORUMA ALIN
            };

            // Eksenler ve diğer ayarlar olduğu gibi kalabilir
            PlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Zaman (sn)",
                FontSize = 10,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            });

            PlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Gaz Konsantrasyonu (%)",
                FontSize = 10,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            });

            // Serilerle ilgili kodda bir değişiklik yapmanıza gerek yok, onlar doğru.
            _pcCalculatedSeries = new LineSeries
            {
                Title = "PC Calculated Gas",
                FontSize = 10,
                TrackerFormatString = "{0}\nTime: {2:0.000}\nValue: {4:0.000}",
                Color = OxyColor.Parse("#4CAF50"),
                StrokeThickness = 2
            };

            _deviceCo2Series = new LineSeries
            {
                Title = "Device CO₂ Value",
                FontSize = 10,
                TrackerFormatString = "{0}\nTime: {2:0.000}\nValue: {4:0.000}",
                Color = OxyColor.Parse("#2196F3"),
                StrokeThickness = 1.5,
                LineStyle = LineStyle.Dash
            };

            PlotModel.Series.Add(_pcCalculatedSeries);
            PlotModel.Series.Add(_deviceCo2Series);
            OnPropertyChanged(nameof(PlotModel));
        }

        /// <summary>
        /// PC'de yapılan kalibrasyon sonucu hesaplanan gaz verisini grafiğe ekler.
        /// </summary>
        public void AddPcCalculatedData(double time, double gasValue)
        {
            // 🔹 DEĞİŞTİ: Metot adını daha spesifik hale getirelim
            AddPointInternal(_pcCalculatedSeries, time, gasValue);
        }

        /// <summary>
        /// Cihazdan gelen hazır CO2 verisini grafiğe ekler.
        /// </summary>
        public void AddDeviceCO2Data(double time, double co2Value)
        {
            // 🔹 YENİ: Cihazdan gelen CO2 verisini eklemek için yeni metot
            AddPointInternal(_deviceCo2Series, time, co2Value);
        }

        /// <summary>
        /// Belirtilen seriye, UI thread'inde güvenli bir şekilde yeni bir nokta ekler ve eski noktaları temizler.
        /// </summary>
        private void AddPointInternal(LineSeries series, double time, double value)
        {
            // 🔹 YENİ: Kod tekrarını önlemek için ortak yardımcı metot
            Application.Current.Dispatcher.Invoke(() =>
            {
                series.Points.Add(new DataPoint(time, value));
                series.Points.RemoveAll(p => p.X < time - PlotTime);
                PlotModel.InvalidatePlot(true);
                // OnPropertyChanged(nameof(PlotModel)) her seferinde çağırmak performansı düşürebilir,
                // InvalidatePlot genellikle yeterlidir.
            });
        }
    }
}
