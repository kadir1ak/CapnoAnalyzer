using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CapnoAnalyzer.Helpers;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;
using Microsoft.Office.Interop.Excel;
using System.Collections.ObjectModel;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using OxyPlot;
using OxyPlot.Series;
using System.Text;
using System.Globalization;
using CapnoAnalyzer.ViewModels.DeviceViewModels;
using CapnoAnalyzer.Models.Device;
using CapnoAnalyzer.Views.DevicesViews.Devices;
using System.Windows;

namespace CapnoAnalyzer.ViewModels.CalibrationViewModels
{
    public class GasConcentrationTablesViewModel : BindableBase
    {
        public DevicesViewModel DevicesVM { get; private set; }

        private readonly Func<Vector<double>, double, double> model = (parameters, xVal) =>
        {
            double a = parameters[0];
            double b = parameters[1];
            double c = parameters[2];
            return a * (1 - Math.Exp(-b * Math.Pow(xVal, c)));
        };

        public ObservableCollection<Data> DeviceData { get; set; }
        public Coefficients Coefficients { get; set; }

        private PlotModel _plotModel;
        public PlotModel PlotModel
        {
            get => _plotModel;
            set
            {
                _plotModel = value;
                OnPropertyChanged();
            }
        }

        public ICommand CalculateCommand { get; }
        public ICommand ExportCommand { get; }

        public string DeviceName { get; }

        public GasConcentrationTablesViewModel(DevicesViewModel devicesVM, Device selectedDevice)
        {
            DevicesVM = devicesVM;
            DeviceName = selectedDevice.Properties.ProductId;
            DeviceData = new ObservableCollection<Data>();
            Coefficients = new Coefficients();

            PlotModel = new PlotModel { Title = $"Cihaz: {DeviceName} - Model: y = a(1 - e^{{-bx^c}})" };
            CalculateCommand = new RelayCommand(_ => CalculateCoefficients(), _ => CanCalculate());
            ExportCommand = new RelayCommand(_ => ExportToFile(), _ => DeviceData.Any());

        }

        // TXT ve CSV dosyasına çıktı
        private void ExportToFile()
        {
            // Sadece bir kez klasör seçme diyaloğu açalım
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.Title = "Dosyaların kaydedileceği klasörü seçiniz.";
                dialog.IsFolderPicker = true; // Klasör seçimi olsun
                dialog.EnsurePathExists = true;
                dialog.Multiselect = false;

                var result = dialog.ShowDialog();

                // CommonFileDialogResult.Ok
                if (result == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    // Seçilen klasör yolu
                    string folderPath = dialog.FileName;

                    // Seçilen klasöre hem txt hem de csv çıkartmak için
                    try
                    {
                        ExportToTxt(folderPath);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(
                            $"Txt dosyası oluşturulurken hata oluştu: {ex.Message}",
                            "Hata",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error
                        );
                    }

                    try
                    {
                        ExportToCsv(folderPath);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(
                            $"CSV dosyası oluşturulurken hata oluştu: {ex.Message}",
                            "Hata",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error
                        );
                    }
                }
                else
                {
                    // Kullanıcı klasör seçmediyse veya iptal ettiyse
                    System.Windows.MessageBox.Show(
                        "Klasör seçilmedi. Kayıt işlemi iptal edildi.",
                        "Uyarı",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning
                    );
                }
            }
        }

        // TXT oluşturma metodu
        private void ExportToTxt(string folderPath)
        {
            // TXT dosya adı
            string fileName = $"{DeviceName}_CalibrationData.txt";
            // Tam dosya yolu
            string filePath = Path.Combine(folderPath, fileName);

            // Eğer aynı isimde dosya varsa önce silelim
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            // Dosya içeriğini oluştur
            var sb = new StringBuilder();

            // -- Cihaz Bilgileri ve Katsayılar
            sb.AppendLine("Cihaz Bilgileri ve Katsayılar");
            sb.AppendLine($"Cihaz Adı\t{DeviceName}");
            sb.AppendLine($"A\t{Coefficients.A}");
            sb.AppendLine($"B\t{Coefficients.B}");
            sb.AppendLine($"C\t{Coefficients.C}");
            sb.AppendLine($"R Kare\t{Coefficients.R}");
            sb.AppendLine();  // Boş satır

            // -- Ölçüm Verileri
            sb.AppendLine("Ölçüm Verileri");
            sb.AppendLine("Gaz Konsantrasyonu\tGaz\tReferans\tOran\tTransmittans\tAbsorpsiyon\tTahmin Edilen Absorpsiyon\tTahmin Edilen Gaz Konsantrasyonu");

            foreach (var data in DeviceData)
            {
                sb.AppendLine(
                    $"{data.GasConcentration}\t" +
                    $"{data.Gas}\t" +
                    $"{data.Ref}\t" +
                    $"{data.Ratio}\t" +
                    $"{data.Transmittance}\t" +
                    $"{data.Absorption}\t" +
                    $"{data.PredictedAbsorption}\t" +
                    $"{data.PredictedGasConcentration}"
                );
            }

            // Dosyayı UTF-8 olarak yazma
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

            // Başarılı mesajı
            System.Windows.MessageBox.Show(
                $"Txt dosyası başarıyla oluşturuldu:\n{filePath}",
                "Başarılı",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information
            );
        }

        // CSV oluşturma metodu (noktalı virgül, UTF-8, InvariantCulture)
        private void ExportToCsv(string folderPath)
        {
            // CSV dosya adı
            string fileName = $"{DeviceName}_CalibrationData.csv";
            // Tam dosya yolu
            string filePath = Path.Combine(folderPath, fileName);

            // Eğer aynı isimde dosya varsa önce silelim
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            // CSV içeriğini oluşturma
            var sb = new StringBuilder();

            // Ondalıklar için InvariantCulture
            var ci = CultureInfo.InvariantCulture;

            // CSV standardına uygun şekilde hücreleri tırnaklamak için yardımcı fonksiyon
            string CsvEscape(string val)
            {
                if (val == null) return "\"\"";
                val = val.Replace("\"", "\"\""); // İçerideki tırnakları çift tırnak ile kaçır
                return $"\"{val}\"";
            }

            // -- Cihaz Bilgileri ve Katsayılar
            sb.AppendLine("Cihaz Bilgileri ve Katsayılar");
            sb.AppendLine($"Cihaz Adı;{CsvEscape(DeviceName)}");
            sb.AppendLine($"A;{CsvEscape(Coefficients.A.ToString(ci))}");
            sb.AppendLine($"B;{CsvEscape(Coefficients.B.ToString(ci))}");
            sb.AppendLine($"C;{CsvEscape(Coefficients.C.ToString(ci))}");
            sb.AppendLine($"R Kare;{CsvEscape(Coefficients.R.ToString(ci))}");
            sb.AppendLine(); // Boşluk bırak

            // -- Ölçüm Verileri
            sb.AppendLine("Ölçüm Verileri");
            sb.AppendLine("Gaz Konsantrasyonu;Gaz;Referans;Oran;Transmittans;Absorpsiyon;Tahmin Edilen Absorpsiyon;Tahmin Edilen Gaz Konsantrasyonu");

            foreach (var data in DeviceData)
            {
                // Her bir değeri string'e dönüştürüp tırnaklıyoruz
                string gc = CsvEscape(data.GasConcentration.ToString(ci));
                string gas = CsvEscape(data.Gas.ToString(ci));
                string r = CsvEscape(data.Ref.ToString(ci));
                double ratio_temp = data.Ratio ?? 0.0;
                string ratio = CsvEscape(ratio_temp.ToString(ci));
                double transmittance_temp = data.Ratio ?? 0.0;
                string trans = CsvEscape(transmittance_temp.ToString(ci));
                string abs = CsvEscape((data.Absorption ?? 0.0).ToString(ci));
                string predAbs = CsvEscape((data.PredictedAbsorption ?? 0.0).ToString(ci));
                string predGas = CsvEscape((data.PredictedGasConcentration ?? 0.0).ToString(ci));

                sb.AppendLine($"{gc};{gas};{r};{ratio};{trans};{abs};{predAbs};{predGas}");
            }

            // Dosyayı UTF-8 olarak yazma
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

            // Başarılı mesajı
            System.Windows.MessageBox.Show(
                $"CSV dosyası başarıyla oluşturuldu:\n{filePath}",
                "Başarılı",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information
            );
        }
        public void AddDeviceData(Data data)
        {
            DeviceData.Add(data);
        }

        private void CalculateCoefficients()
        {
            try
            {
                // (1) Hesaplamalara başlamadan önce veri setinizi sıralayın:
                SortDeviceDataByGasConcentration();

                // (2) Sıfır konsantrasyonlu ölçümü bulma (örneğin 0.00)
                // Burada .First(...) yerine .FirstOrDefault(...) kullanarak hata almaktan kaçınabilirsiniz.
                var zeroData = DeviceData.FirstOrDefault(d => Math.Abs(d.GasConcentration) < 1e-9);
                if (zeroData == null)
                {
                    // 0.00 konsantrasyonlu veri yoksa hesaplamadan çıkabilirsiniz veya uyarı verebilirsiniz
                    System.Windows.MessageBox.Show("0.00 konsantrasyonlu satır bulunamadı!");
                    return;
                }

                double zero = zeroData.Gas / zeroData.Ref;

                // (3) Ratio, Transmittance, Absorption hesaplamaları
                foreach (var data in DeviceData)
                {
                    data.Ratio = data.Gas / data.Ref;
                    data.Transmittance = data.Ratio / zero;
                    data.Absorption = 1 - data.Transmittance;
                }

                // (4) Optimize etmek üzere dizi haline getirelim
                var gasConcentration = DeviceData.Select(d => d.GasConcentration).ToArray();
                var absorption = DeviceData.Select(d => d.Absorption ?? 0.0).ToArray();

                // (5) NelderMeadSimplex optimizasyonu + Plot çizimi
                PlotAndOptimize(gasConcentration, absorption);

                // (6) Optimize sonuçları ile tahmini değerler
                foreach (var data in DeviceData)
                {
                    data.PredictedAbsorption = model(
                        Vector<double>.Build.DenseOfArray(new[] { Coefficients.A, Coefficients.B, Coefficients.C }),
                        data.GasConcentration);

                    if (data.Absorption.HasValue &&
                        data.Absorption.Value > 0 &&
                        data.Absorption.Value < Coefficients.A)
                    {
                        data.PredictedGasConcentration = Math.Pow(
                            -Math.Log(1 - (data.Absorption.Value / Coefficients.A)) / Coefficients.B,
                            1 / Coefficients.C);
                    }
                    else
                    {
                        data.PredictedGasConcentration = double.NaN;
                    }
                }

                // (7) R² hesaplama
                double meanGasConc = DeviceData.Average(d => d.GasConcentration);
                double sst = DeviceData.Sum(d => Math.Pow(d.GasConcentration - meanGasConc, 2));
                double sse = DeviceData
                    .Where(d => !double.IsNaN(d.PredictedGasConcentration ?? double.NaN))
                    .Sum(d => Math.Pow(d.GasConcentration - (d.PredictedGasConcentration ?? 0), 2));

                Coefficients.R = 1 - (sse / sst);


                // (8) Kalibrasyon Katsayılarını ve Denklemini Cihaza Aktar
                var device = DevicesVM?.IdentifiedDevices?.FirstOrDefault(d => d.Properties.ProductId == DeviceName);
                if (device != null)
                {
                    device.DeviceData.CalibrationCoefficients.A = Coefficients.A;
                    device.DeviceData.CalibrationCoefficients.B = Coefficients.B;
                    device.DeviceData.CalibrationCoefficients.C = Coefficients.C;
                    device.DeviceData.CalibrationCoefficients.R = Coefficients.R;
                    device.DeviceData.CalibrationData.Zero = zero;
                    MessageBox.Show(
                        $"Kalibrasyon katsayıları '{DeviceName}' cihazına aktarıldı.\n" +
                        $"A: {Coefficients.A}, B: {Coefficients.B}, C: {Coefficients.C}, R²: {Coefficients.R}",
                        "Başarılı",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    MessageBox.Show(
                        $"'{DeviceName}' cihazı bulunamadı, kalibrasyon değerleri atanamadı.",
                        "Hata",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    System.Diagnostics.Debug.WriteLine($"❗ '{DeviceName}' ID'li cihaz bulunamadı, kalibrasyon değerleri atanamadı.");
                }
            }
            catch (Exception ex)
            {
                // İster loglayın, ister mesaj kutusunda gösterin:
                // System.Windows.MessageBox.Show(ex.Message);
            }
        }

        private void SortDeviceDataByGasConcentration()
        {
            // ObservableCollection üzerinde sıralama yapabilmek için önce List'e çeviriyoruz
            var sortedList = DeviceData.OrderBy(d => d.GasConcentration).ToList();

            // Eski verileri temizle
            DeviceData.Clear();

            // Sıralanmış verileri yeniden ekle
            foreach (var item in sortedList)
            {
                DeviceData.Add(item);
            }
            // DataGrid, ItemsSource = DeviceData şeklinde bağlıysa anında güncellenecektir.
        }

        private void PlotAndOptimize(double[] x, double[] y)
        {
            Func<Vector<double>, double> objFunc = parameters =>
                x.Zip(y, (xx, yy) => Math.Pow(yy - model(parameters, xx), 2)).Sum();

            var initialGuess = CalculateInitialGuesses(x, y);

            var optimizer = new NelderMeadSimplex(1e-6, 1000);
            var result = optimizer.FindMinimum(ObjectiveFunction.Value(objFunc), initialGuess);

            Coefficients.A = result.MinimizingPoint[0];
            Coefficients.B = result.MinimizingPoint[1];
            Coefficients.C = result.MinimizingPoint[2];

            int numPoints = 100;
            double minX = x.Min();
            double maxX = x.Max();
            double[] xFit = Enumerable.Range(0, numPoints)
                                      .Select(i => minX + i * (maxX - minX) / (numPoints - 1))
                                      .ToArray();
            double[] yFit = xFit.Select(xx => model(result.MinimizingPoint, xx)).ToArray();

            var scatterSeries = new ScatterSeries { Title = "Ölçüm", MarkerType = MarkerType.Circle };
            for (int i = 0; i < x.Length; i++)
            {
                scatterSeries.Points.Add(new ScatterPoint(x[i], y[i]));
            }

            var lineSeries = new LineSeries { Title = "Fit", StrokeThickness = 2 };
            for (int i = 0; i < xFit.Length; i++)
            {
                lineSeries.Points.Add(new DataPoint(xFit[i], yFit[i]));
            }

            PlotModel.Series.Clear();
            PlotModel.Series.Add(scatterSeries);
            PlotModel.Series.Add(lineSeries);
            PlotModel.InvalidatePlot(true);
        }

        private Vector<double> CalculateInitialGuesses(double[] x, double[] y)
        {
            double aGuess = y.Max();
            double xRange = x.Max() - x.Min();
            double bGuess = 1.0 / xRange;
            double cGuess = 1.0;
            return Vector<double>.Build.DenseOfArray(new double[] { aGuess, bGuess, cGuess });
        }

        private bool CanCalculate()
        {
            try
            {
                return DeviceData != null && DeviceData.Any() &&
                       DeviceData.All(d => d.Ref > 0 && d.Gas > 0);
            }
            catch (Exception) { return false; }
        }
    }
}
