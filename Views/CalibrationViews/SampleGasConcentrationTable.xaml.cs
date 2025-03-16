using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;
using OxyPlot;
using OxyPlot.Series;

namespace CapnoAnalyzer.Views.CalibrationViews
{
    /// <summary>
    /// SampleGasConcentrationTable.xaml etkileşim mantığı
    /// </summary>
    public partial class SampleGasConcentrationTable : UserControl
    {
        private Func<Vector<double>, double, double> model = (parameters, xVal) =>
        {
            double a = parameters[0];
            double b = parameters[1];
            double c = parameters[2];
            return a * (1 - Math.Exp(-b * Math.Pow(xVal, c)));
        };

        public SampleGasConcentrationTable()
        {
            InitializeComponent();
            LoadData();

            var customController = new PlotController();
            customController.UnbindAll();
            customController.BindMouseDown(OxyMouseButton.Left, OxyPlot.PlotCommands.Track);
            plotView.Controller = customController;
        }

        private void LoadData()
        {
            var deviceData = new List<DeviceData>
            {
                new DeviceData { Sample = "1", GasConcentration = 0.00, Ref = 2688.4988, Gas = 4912.5496 },
                new DeviceData { Sample = "2", GasConcentration = 0.50, Ref = 2686.8541, Gas = 4653.6383 },
                new DeviceData { Sample = "3", GasConcentration = 1.00, Ref = 2698.5712, Gas = 4482.9614 },
                new DeviceData { Sample = "4", GasConcentration = 1.50, Ref = 2691.1024, Gas = 4324.9308 },
                new DeviceData { Sample = "5", GasConcentration = 2.00, Ref = 2698.5963, Gas = 4199.9262 },
                new DeviceData { Sample = "6", GasConcentration = 2.50, Ref = 2702.6907, Gas = 4104.1672 },
                new DeviceData { Sample = "7", GasConcentration = 3.00, Ref = 2691.3517, Gas = 3937.3630 },
                new DeviceData { Sample = "8", GasConcentration = 3.50, Ref = 2690.0622, Gas = 3833.1201 },
                new DeviceData { Sample = "9", GasConcentration = 4.00, Ref = 2692.0327, Gas = 3692.6704 },
                new DeviceData { Sample = "10", GasConcentration = 4.50, Ref = 2700.4150, Gas = 3643.4684 },
                new DeviceData { Sample = "11", GasConcentration = 5.00, Ref = 2690.9841, Gas = 3534.8201 },
                new DeviceData { Sample = "12", GasConcentration = 5.50, Ref = 2690.0577, Gas = 3456.5425 },
                new DeviceData { Sample = "13", GasConcentration = 6.00, Ref = 2695.2707, Gas = 3405.8441 },
                new DeviceData { Sample = "14", GasConcentration = 6.50, Ref = 2693.2138, Gas = 3341.9859 },
                new DeviceData { Sample = "15", GasConcentration = 7.00, Ref = 2697.0889, Gas = 3300.5941 }
            };

            DataGridDeviceData.ItemsSource = deviceData;
            plotView.Model = new PlotModel { Title = "Hesaplama için butona tıklayın" };
        }
        private void btnCoefficientCal_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var deviceData = (List<DeviceData>)DataGridDeviceData.ItemsSource;

            double Zero = deviceData.First(d => d.GasConcentration == 0.00).Gas / deviceData.First(d => d.GasConcentration == 0.00).Ref;

            foreach (var data in deviceData)
            {
                data.Ratio = data.Gas / data.Ref;
                data.Transmittance = data.Ratio / Zero;
                data.Absorption = 1 - data.Transmittance;
            }

            var gasConcentration = deviceData.Select(d => d.GasConcentration).ToArray();
            var absorption = deviceData.Select(d => d.Absorption ?? 0.0).ToArray(); // null kontrolü

            var coefficients = new Coefficients();
            PlotAndOptimize(gasConcentration, absorption, coefficients);

            foreach (var data in deviceData)
            {
                data.PredictedAbsorption = model(
                    Vector<double>.Build.DenseOfArray(new double[] { coefficients.A, coefficients.B, coefficients.C }),
                    data.GasConcentration);

                if (data.Absorption.HasValue && data.Absorption.Value > 0 && data.Absorption.Value < coefficients.A)
                {
                    data.PredictedGasConcentration = Math.Pow(
                        -Math.Log(1 - (data.Absorption.Value / coefficients.A)) / coefficients.B,
                        1 / coefficients.C);
                }
                else
                {
                    data.PredictedGasConcentration = double.NaN;
                }
            }

            // R^2 hesapla
            // Gerçek gaz konsantrasyonlarının ortalaması
            double meanGasConc = deviceData.Average(d => d.GasConcentration);

            // Toplam kareler toplamı (SST)
            double sst = deviceData.Sum(d => Math.Pow(d.GasConcentration - meanGasConc, 2));

            // Hata kareleri toplamı (SSE)
            double sse = deviceData
                .Where(d => !double.IsNaN(d.PredictedGasConcentration.Value))
                .Sum(d => Math.Pow(d.GasConcentration - d.PredictedGasConcentration.Value, 2));

            // R-kare (R²) hesaplama
            coefficients.R = 1 - (sse / sst);
            txtCoefficientR.Text = $"R^2: {coefficients.R:F6}";

            DataGridDeviceData.Items.Refresh();
        }

        private void PlotAndOptimize(double[] x, double[] y, Coefficients coefficients)
        {
            // Grafik modeli oluştur
            var plotModel = new PlotModel { Title = "Nonlineer Model Fit: y = a(1 - e^{-bx^c})" };

            // Hedef fonksiyonu tanımlama
            Func<Vector<double>, double> objFunc = parameters =>
                x.Zip(y, (xx, yy) => Math.Pow(yy - model(parameters, xx), 2)).Sum();

            // Başlangıç tahminlerini hesapla
            var initialGuess = CalculateInitialGuesses(x, y);

            // Optimizasyon işlemi
            var optimizer = new NelderMeadSimplex(1e-6, 1000);
            var result = optimizer.FindMinimum(ObjectiveFunction.Value(objFunc), initialGuess);

            // Parametreleri al
            coefficients.A = result.MinimizingPoint[0];
            coefficients.B = result.MinimizingPoint[1];
            coefficients.C = result.MinimizingPoint[2];

            // Fit eğrisi için değerler hesapla
            int numPoints = 100;
            double minX = x.Min();
            double maxX = x.Max();
            double[] xFit = Enumerable.Range(0, numPoints)
                                      .Select(i => minX + i * (maxX - minX) / (numPoints - 1))
                                      .ToArray();
            double[] yFit = xFit.Select(xx => model(result.MinimizingPoint, xx)).ToArray();

            // Grafik: Ölçüm noktaları
            var scatterSeries = new ScatterSeries
            {
                Title = "Ölçüm",
                MarkerType = MarkerType.Circle,
                MarkerSize = 3
            };
            for (int i = 0; i < x.Length; i++)
            {
                scatterSeries.Points.Add(new ScatterPoint(x[i], y[i]));
            }
            plotModel.Series.Add(scatterSeries);

            // Grafik: Fit eğrisi
            var lineSeries = new LineSeries
            {
                Title = "Fit",
                StrokeThickness = 2
            };
            for (int i = 0; i < xFit.Length; i++)
            {
                lineSeries.Points.Add(new DataPoint(xFit[i], yFit[i]));
            }
            plotModel.Series.Add(lineSeries);

            // Grafiği ekrana bağlama
            plotView.Model = plotModel;

            // Katsayıları arayüze göster
            txtCoefficientA.Text = $"a: {coefficients.A:F6}";
            txtCoefficientB.Text = $"b: {coefficients.B:F6}";
            txtCoefficientC.Text = $"c: {coefficients.C:F6}";
            txtCoefficientR.Text = $"R^2: {coefficients.R:F6}";
        }

        private Vector<double> CalculateInitialGuesses(double[] x, double[] y)
        {
            double aGuess = y.Max(); // a: y'nin maksimum değeri
            double xRange = x.Max() - x.Min(); // b: x'in aralığına göre ölçekleme
            double bGuess = 1.0 / xRange;
            double cGuess = 1.0; // c: başlangıç olarak 1
            return Vector<double>.Build.DenseOfArray(new double[] { aGuess, bGuess, cGuess });
        }

        // Veri model sınıfı
        public class DeviceData
        {
            public string Sample { get; set; }
            public double GasConcentration { get; set; }
            public double Ref { get; set; }
            public double Gas { get; set; }

            // Diğer kolonlar boş kalacaksa nullable olarak tanımlayın
            public double? Ratio { get; set; }
            public double? Transmittance { get; set; }
            public double? Absorption { get; set; }
            public double? PredictedAbsorption { get; set; }
            public double? PredictedGasConcentration { get; set; }
        }

        public class Coefficients
        {
            public double A { get; set; }
            public double B { get; set; }
            public double C { get; set; }
            public double R { get; set; }
        }
    }
}
