using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MathNet.Numerics;
using MathNet.Numerics.Optimization;
using MathNet.Numerics.LinearAlgebra;
using OxyPlot;
using OxyPlot.Series;

namespace CapnoAnalyzer.Views.Pages
{
    public partial class EquationTestPage : Page
    {
        public EquationTestPage()
        {
            InitializeComponent();

            // 1. Veri Seti Tanımlama
            // x: Bağımsız değişkenler (örneğin zaman veya mesafe)
            // y: Bağımlı değişkenler (örneğin ölçülen değerler)
            double[] x = { 0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20 };
            double[] y = { 0, 0.36856969, 0.520018621, 0.57080947, 0.629335451, 0.654898524,
                           0.680461596, 0.693243132, 0.706024668, 0.718806205, 0.718806205 };

            // 2. Veri Setini DataGrid'e Yükleme
            // x ve y değerleri birleştirilerek DataGrid'e gösterilir.
            var data = x.Zip(y, (xi, yi) => new { X = xi, Y = yi }).ToList();
            dataGrid.ItemsSource = data;

            // 3. Model Fonksiyonu Tanımlama
            // Matematiksel model: y = a * (1 - exp(-b * x^c))
            // Bu denklem, bir büyüme veya doygunluk eğrisini temsil eder.
            // a: Maksimum değer (y'nin asimptotik limiti)
            // b: Büyüme oranı
            // c: Eğrinin şekil parametresi
            Func<Vector<double>, double, double> model = (parameters, xVal) =>
            {
                double a = parameters[0]; // Maksimum değer
                double b = parameters[1]; // Büyüme oranı
                double c = parameters[2]; // Şekil parametresi
                return a * (1 - Math.Exp(-b * Math.Pow(xVal, c)));
            };

            // 4. Hata Fonksiyonu Tanımlama
            // Hata fonksiyonu: Ölçülen y değerleri ile modelden elde edilen y değerleri arasındaki farkların karelerinin toplamı.
            // Bu fonksiyon, optimizasyon algoritması tarafından minimize edilir.
            Func<Vector<double>, double> objectiveFunction = parameters =>
            {
                return x.Zip(y, (xi, yi) => Math.Pow(yi - model(parameters, xi), 2)).Sum();
            };

            // 5. Başlangıç Tahminleri
            // Optimizasyon algoritmasının başlayabilmesi için parametreler (a, b, c) için başlangıç değerleri verilmelidir.
            var initialGuess = Vector<double>.Build.DenseOfArray(new double[] { 0.7, 0.1, 1.0 });

            try
            {
                // 6. Optimizasyon Algoritması
                // Nelder-Mead Simplex algoritması kullanılarak hata fonksiyonu minimize edilir.
                // Nelder-Mead Simplex, türev gerektirmeyen bir optimizasyon algoritmasıdır.
                var optimizer = new NelderMeadSimplex(1e-6, 1000); // Yakınsama toleransı: 1e-6, Maksimum iterasyon: 1000
                var result = optimizer.FindMinimum(ObjectiveFunction.Value(objectiveFunction), initialGuess);

                // 7. Sonuçların Elde Edilmesi
                // Optimize edilen parametreler (a, b, c)
                double aOpt = result.MinimizingPoint[0]; // Optimize edilmiş a değeri
                double bOpt = result.MinimizingPoint[1]; // Optimize edilmiş b değeri
                double cOpt = result.MinimizingPoint[2]; // Optimize edilmiş c değeri

                // 8. Sonuçları Kullanıcı Arayüzüne Yazdırma
                txtParameterA.Text = $"a: {aOpt:F6}";
                txtParameterB.Text = $"b: {bOpt:F6}";
                txtParameterC.Text = $"c: {cOpt:F6}";

                // 9. Modelin Çizimi İçin Veri Üretimi
                // Optimize edilen parametrelerle modelin tahmin ettiği y değerleri hesaplanır.
                int numPoints = 100; // Grafikte kullanılacak veri noktası sayısı
                double[] xFit = Enumerable.Range(0, numPoints)
                                          .Select(i => x.Min() + i * (x.Max() - x.Min()) / (numPoints - 1))
                                          .ToArray();
                double[] yFit = xFit.Select(xi => model(result.MinimizingPoint, xi)).ToArray();

                // 10. Grafiğin Çizimi
                // OxyPlot kullanılarak ölçülen veriler ve model eğrisi çizilir.
                var plotModel = new PlotModel { Title = "Nonlineer Model Fit: y = a(1 - e^{-bx^c})" };

                // Ölçülen veriler (kırmızı noktalar)
                var scatterSeries = new ScatterSeries { MarkerType = MarkerType.Circle, MarkerSize = 4, MarkerFill = OxyColors.Red };
                for (int i = 0; i < x.Length; i++)
                {
                    scatterSeries.Points.Add(new ScatterPoint(x[i], y[i]));
                }

                // Model eğrisi (mavi çizgi)
                var lineSeries = new LineSeries { Color = OxyColors.Blue, StrokeThickness = 2 };
                for (int i = 0; i < xFit.Length; i++)
                {
                    lineSeries.Points.Add(new DataPoint(xFit[i], yFit[i]));
                }

                plotModel.Series.Add(scatterSeries);
                plotModel.Series.Add(lineSeries);

                // 11. Grafiği Arayüze Bağlama
                plotView.Model = plotModel;
            }
            catch (MaximumIterationsException ex)
            {
                // Optimizasyon sırasında maksimum iterasyon sınırına ulaşıldığında hata mesajı gösterilir.
                MessageBox.Show("Optimizasyon iterasyon sınırına ulaştı. Lütfen başlangıç tahminlerini değiştirin veya maksimum iterasyon sayısını artırın.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}