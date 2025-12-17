using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;
using OxyPlot;
using OxyPlot.Series;

namespace CapnoAnalyzer.Views.Pages
{
    public partial class EquationTestPage : Page
    {
        // Ortak model fonksiyonumuz: y = a * (1 - e^(-b * x^c))
        private Func<Vector<double>, double, double> model = (parameters, xVal) =>
        {
            double a = parameters[0];
            double b = parameters[1];
            double c = parameters[2];
            return a * (1 - Math.Exp(-b * Math.Pow(xVal, c)));
        };

        public EquationTestPage()
        {
            InitializeComponent();

            // Veri setlerini tanımlıyoruz
            var cihaz1Data = new
            {
                X = new double[] { 0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20 },
                Y = new double[] { 0, 0.36856969, 0.520018621, 0.57080947, 0.629335451, 0.654898524, 0.680461596, 0.693243132, 0.706024668, 0.718806205, 0.718806205 }
            };

            // Verileri DataGrid'lere bağlama
            dataGrid1.ItemsSource = cihaz1Data.X.Zip(cihaz1Data.Y, (x, y) => new { X = x, Y = y }).ToList();

            // Grafik modeli oluştur
            var plotModel = new PlotModel { Title = "Nonlineer Model Fit: y = a(1 - e^{-bx^c})" };

            // Optimizasyon işlemleri
            OptimizeAndPlot(cihaz1Data.X, cihaz1Data.Y, "Cihaz 1", plotModel, txtCihaz1A, txtCihaz1B, txtCihaz1C);

            // Grafiği ekrana bağlama
            plotView.Model = plotModel;
        }

        /// <summary>
        /// Başlangıç tahminlerini otomatik hesaplayan fonksiyon
        /// </summary>
        private Vector<double> CalculateInitialGuesses(double[] x, double[] y)
        {
            double aGuess = y.Max(); // a: y'nin maksimum değeri
            double xRange = x.Max() - x.Min(); // b: x'in aralığına göre ölçekleme
            double bGuess = 1.0 / xRange;
            double cGuess = 1.0; // c: başlangıç olarak 1
            return Vector<double>.Build.DenseOfArray(new double[] { aGuess, bGuess, cGuess });
        }

        /// <summary>
        /// Optimizasyon ve grafiğe ekleme işlemleri için genel fonksiyon
        /// </summary>
        private void OptimizeAndPlot(double[] x, double[] y, string cihazAdi, PlotModel plotModel, TextBlock txtA, TextBlock txtB, TextBlock txtC)
        {
            // Hedef fonksiyonu tanımlama
            Func<Vector<double>, double> objFunc = parameters =>
                x.Zip(y, (xx, yy) => Math.Pow(yy - model(parameters, xx), 2)).Sum();

            // Başlangıç tahminlerini hesapla
            var initialGuess = CalculateInitialGuesses(x, y);

            // Optimizasyon ayarları
            var optimizer = new NelderMeadSimplex(1e-6, 10000)
            {
                MaximumIterations = 5000,
                ConvergenceTolerance = 1e-10
            };

            try
            {
                // Optimizasyonu gerçekleştir
                var result = optimizer.FindMinimum(ObjectiveFunction.Value(objFunc), initialGuess);

                // Parametreleri al
                double a = result.MinimizingPoint[0];
                double b = result.MinimizingPoint[1];
                double c = result.MinimizingPoint[2];

                // Arayüzde göster
                txtA.Text = $"a: {a:F6}";
                txtB.Text = $"b: {b:F6}";
                txtC.Text = $"c: {c:F6}";

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
                    Title = $"{cihazAdi} Ölçüm",
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
                    Title = $"{cihazAdi} Fit",
                    StrokeThickness = 2
                };
                for (int i = 0; i < xFit.Length; i++)
                {
                    lineSeries.Points.Add(new DataPoint(xFit[i], yFit[i]));
                }
                plotModel.Series.Add(lineSeries);
            }
            catch (MaximumIterationsException)
            {
                MessageBox.Show($"{cihazAdi} için optimizasyon iterasyon sınırına ulaştı. Lütfen ayarları gözden geçirin.",
                                "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
