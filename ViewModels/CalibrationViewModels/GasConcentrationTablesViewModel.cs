using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CapnoAnalyzer.Helpers;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using OxyPlot;
using OxyPlot.Series;
using System.Text;
using System.Globalization;
using CapnoAnalyzer.ViewModels.DeviceViewModels;
using CapnoAnalyzer.Models.Device;
using System.Windows;
using System.Diagnostics;

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
            set => SetProperty(ref _plotModel, value);
        }

        public ICommand CalculateCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ImportCommand { get; } // YENİ KOMUT
        public string DeviceName { get; }

        public ObservableCollection<TemperatureTestViewModel> TemperatureTests { get; set; }

        private TemperatureTestViewModel _selectedTest;
        public TemperatureTestViewModel SelectedTest
        {
            get => _selectedTest;
            set => SetProperty(ref _selectedTest, value);
        }

        public ICommand CreateNewTestCommand { get; }
        public ICommand AddDataToSelectedTestCommand { get; }

        public GasConcentrationTablesViewModel(DevicesViewModel devicesVM, Device selectedDevice)
        {
            DevicesVM = devicesVM;
            DeviceName = selectedDevice.Properties.ProductId;
            DeviceData = new ObservableCollection<Data>();
            Coefficients = new Coefficients();

            PlotModel = new PlotModel { Title = $"Cihaz: {DeviceName} - Model: y = a(1 - e^{{-bx^c}})" };
            CalculateCommand = new RelayCommand(_ => CalculateCoefficients(), _ => CanCalculate());
            ExportCommand = new RelayCommand(_ => ExportToFile(), _ => DeviceData.Any());
            ImportCommand = new RelayCommand(ExecuteImport); // YENİ KOMUTUN BAŞLATILMASI

            TemperatureTests = new ObservableCollection<TemperatureTestViewModel>();
            CreateNewTestCommand = new RelayCommand(ExecuteCreateNewTest);
            AddDataToSelectedTestCommand = new RelayCommand(ExecuteAddDataToSelectedTest, () => SelectedTest != null);

            for (int i = 1; i <= 3; i++)
            {
                var defaultTest = new TemperatureTestViewModel($"Sıcaklık Komp. Testi {i}")
                {
                    ReferenceTestData = { TestNo = i }
                };
                TemperatureTests.Add(defaultTest);
            }
            SelectedTest = TemperatureTests.FirstOrDefault();
        }

        // --- YENİ METOTLAR: İÇERİ AKTARMA İŞLEVSELLİĞİ ---
        private void ExecuteImport()
        {
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.Title = "İçeri aktarılacak kalibrasyon dosyasını seçin.";
                dialog.Filters.Add(new CommonFileDialogFilter("Metin Dosyaları", "*.txt"));
                dialog.Filters.Add(new CommonFileDialogFilter("CSV Dosyaları", "*.csv"));
                dialog.Filters.Add(new CommonFileDialogFilter("Tüm Dosyalar", "*.*"));

                if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
                {
                    MessageBox.Show("Dosya seçilmedi. İçeri aktarma işlemi iptal edildi.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    string filePath = dialog.FileName;
                    ParseCalibrationFile(filePath);
                    MessageBox.Show($"Veriler başarıyla '{Path.GetFileName(filePath)}' dosyasından içe aktarıldı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);

                    // İçe aktarma sonrası katsayıları otomatik olarak yeniden hesapla
                    if (CanCalculate())
                    {
                        CalculateCoefficients();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Dosya okunurken veya işlenirken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ParseCalibrationFile(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            // TXT dosyaları sistem kültürünü (örn: Türkçe için virgül), CSV ise InvariantCulture (nokta) kullanır.
            var culture = filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                          ? CultureInfo.InvariantCulture
                          : CultureInfo.CurrentCulture;

            char delimiter = filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? ';' : '\t';

            DeviceData.Clear(); // İçe aktarmadan önce mevcut verileri temizle

            bool isDataSection = false;
            int sampleCounter = 1;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(delimiter);
                if (parts.Length < 2 && !line.Contains("Gaz Konsantrasyonu")) continue;

                string key = parts[0].Trim().Replace("\"", "");
                string value = parts.Length > 1 ? parts[1].Trim().Replace("\"", "") : string.Empty;

                // Katsayıları oku
                if (key.Equals("A", StringComparison.OrdinalIgnoreCase)) Coefficients.A = double.Parse(value, culture);
                else if (key.Equals("B", StringComparison.OrdinalIgnoreCase)) Coefficients.B = double.Parse(value, culture);
                else if (key.Equals("C", StringComparison.OrdinalIgnoreCase)) Coefficients.C = double.Parse(value, culture);
                else if (key.Equals("R Kare", StringComparison.OrdinalIgnoreCase)) Coefficients.R = double.Parse(value, culture);
                // Veri tablosunun başlangıcını bul
                else if (key.Contains("Gaz Konsantrasyonu"))
                {
                    isDataSection = true;
                    continue; // Başlık satırını atla
                }

                if (isDataSection && parts.Length >= 3)
                {
                    try
                    {
                        var newData = new Data
                        {
                            Sample = sampleCounter++.ToString(),
                            GasConcentration = double.Parse(parts[0].Replace("\"", "").Trim(), culture),
                            Gas = double.Parse(parts[1].Replace("\"", "").Trim(), culture),
                            Ref = double.Parse(parts[2].Replace("\"", "").Trim(), culture)
                        };
                        DeviceData.Add(newData);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Veri satırı okunamadı: {line}. Hata: {ex.Message}");
                    }
                }
            }
        }
        // --- YENİ METOTLARIN SONU ---

        private void ExecuteCreateNewTest()
        {
            int testCount = TemperatureTests.Count + 1;
            var newTest = new TemperatureTestViewModel($"Sıcaklık Komp. Testi {testCount}");
            newTest.ReferenceTestData.TestNo = testCount;

            if (Coefficients.R < 1.0 && Coefficients.R != 0)
            {
                var zeroData = DeviceData.FirstOrDefault(d => Math.Abs(d.GasConcentration) < 1e-9);
                double zeroValue = zeroData != null ? (zeroData.Gas / zeroData.Ref) : 0;
                UpdateSingleTestReferenceData(newTest, zeroValue);
            }

            TemperatureTests.Add(newTest);
            SelectedTest = newTest;
        }

        private void ExecuteAddDataToSelectedTest()
        {
            if (SelectedTest == null)
            {
                MessageBox.Show("Veri eklemek için lütfen önce bir test seçin veya 'Yeni Sıcaklık Testi Oluştur' butonuna basın.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            MessageBox.Show($"'{SelectedTest.Header}' testine yeni veri ekleme mantığı tetiklendi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateAllTestsReferenceData(double zeroValue)
        {
            foreach (var test in TemperatureTests)
            {
                UpdateSingleTestReferenceData(test, zeroValue);
            }
        }

        private void UpdateSingleTestReferenceData(TemperatureTestViewModel test, double zeroValue)
        {
            var zeroData = DeviceData.FirstOrDefault(d => Math.Abs(d.GasConcentration) < 1e-9);

            test.ReferenceTestData.Zero = zeroValue;
            test.ReferenceTestData.SpanA = Coefficients.A;
            test.ReferenceTestData.B = Coefficients.B;
            test.ReferenceTestData.C = Coefficients.C;
            test.ReferenceTestData.R = Coefficients.R;
            test.ReferenceTestData.Alpha = 0.00070; // Varsayılan
            test.ReferenceTestData.Beta = -0.09850;  // Varsayılan

            /*
            if (zeroData != null)
            {
                test.ReferenceTestData.Temperature = zeroData.Temperature;
                test.ReferenceTestData.Pressure = zeroData.Pressure;
                test.ReferenceTestData.Humidity = zeroData.Humidity;
            }
            */
        }

        private void CalculateCoefficients()
        {
            try
            {
                SortDeviceDataByGasConcentration();
                var zeroData = DeviceData.FirstOrDefault(d => Math.Abs(d.GasConcentration) < 1e-9);
                if (zeroData == null)
                {
                    MessageBox.Show("0.00 konsantrasyonlu satır bulunamadı!");
                    return;
                }
                double zero = zeroData.Gas / zeroData.Ref;
                foreach (var data in DeviceData)
                {
                    data.Ratio = data.Gas / data.Ref;
                    data.Transmittance = data.Ratio / zero;
                    data.Absorption = 1 - data.Transmittance;
                }
                var gasConcentration = DeviceData.Select(d => d.GasConcentration).ToArray();
                var absorption = DeviceData.Select(d => d.Absorption ?? 0.0).ToArray();
                PlotAndOptimize(gasConcentration, absorption);
                foreach (var data in DeviceData)
                {
                    data.PredictedAbsorption = model(Vector<double>.Build.DenseOfArray(new[] { Coefficients.A, Coefficients.B, Coefficients.C }), data.GasConcentration);
                    if (data.Absorption.HasValue && data.Absorption.Value > 0 && data.Absorption.Value < Coefficients.A)
                    {
                        data.PredictedGasConcentration = Math.Pow(-Math.Log(1 - (data.Absorption.Value / Coefficients.A)) / Coefficients.B, 1 / Coefficients.C);
                    }
                    else
                    {
                        data.PredictedGasConcentration = double.NaN;
                    }
                }
                double meanGasConc = DeviceData.Average(d => d.GasConcentration);
                double sst = DeviceData.Sum(d => Math.Pow(d.GasConcentration - meanGasConc, 2));
                double sse = DeviceData.Where(d => !double.IsNaN(d.PredictedGasConcentration ?? double.NaN)).Sum(d => Math.Pow(d.GasConcentration - (d.PredictedGasConcentration ?? 0), 2));
                Coefficients.R = 1 - (sse / sst);
                var device = DevicesVM?.IdentifiedDevices?.FirstOrDefault(d => d.Properties.ProductId == DeviceName);
                if (device != null)
                {
                    device.Interface.DeviceData.CalibrationCoefficients.A = Coefficients.A;
                    device.Interface.DeviceData.CalibrationCoefficients.B = Coefficients.B;
                    device.Interface.DeviceData.CalibrationCoefficients.C = Coefficients.C;
                    device.Interface.DeviceData.CalibrationCoefficients.R = Coefficients.R;
                    device.Interface.DeviceData.CalibrationData.Zero = zero;
                    MessageBox.Show($"Kalibrasyon katsayıları '{DeviceName}' cihazına aktarıldı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateAllTestsReferenceData(zero);
                }
                else
                {
                    MessageBox.Show($"'{DeviceName}' cihazı bulunamadı, kalibrasyon değerleri atanamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hesaplama sırasında bir hata oluştu: {ex.Message}");
            }
        }

        #region Mevcut Metotlar (Değişiklik Yok)
        private void ExportToFile()
        {
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.Title = "Dosyaların kaydedileceği klasörü seçiniz.";
                dialog.IsFolderPicker = true;
                if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
                {
                    MessageBox.Show("Klasör seçilmedi. Kayıt işlemi iptal edildi.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                string folderPath = dialog.FileName;
                try { ExportToTxt(folderPath); } catch (Exception ex) { MessageBox.Show($"Txt dosyası oluşturulurken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
                try { ExportToCsv(folderPath); } catch (Exception ex) { MessageBox.Show($"CSV dosyası oluşturulurken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void ExportToTxt(string folderPath)
        {
            string filePath = Path.Combine(folderPath, $"{DeviceName}_CalibrationData.txt");
            var sb = new StringBuilder();
            sb.AppendLine("Cihaz Bilgileri ve Katsayılar");
            sb.AppendLine($"Cihaz Adı\t{DeviceName}");
            sb.AppendLine($"A\t{Coefficients.A}");
            sb.AppendLine($"B\t{Coefficients.B}");
            sb.AppendLine($"C\t{Coefficients.C}");
            sb.AppendLine($"R Kare\t{Coefficients.R}");
            sb.AppendLine();
            sb.AppendLine("Ölçüm Verileri");
            sb.AppendLine("Gaz Konsantrasyonu\tGaz\tReferans\tOran\tTransmittans\tAbsorpsiyon\tTahmin Edilen Absorpsiyon\tTahmin Edilen Gaz Konsantrasyonu");
            foreach (var data in DeviceData)
            {
                sb.AppendLine($"{data.GasConcentration}\t{data.Gas}\t{data.Ref}\t{data.Ratio}\t{data.Transmittance}\t{data.Absorption}\t{data.PredictedAbsorption}\t{data.PredictedGasConcentration}");
            }
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            MessageBox.Show($"Txt dosyası başarıyla oluşturuldu:\n{filePath}", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportToCsv(string folderPath)
        {
            string filePath = Path.Combine(folderPath, $"{DeviceName}_CalibrationData.csv");
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;
            string CsvEscape(string val) => $"\"{(val ?? "").Replace("\"", "\"\"")}\"";

            sb.AppendLine("Cihaz Bilgileri ve Katsayılar");
            sb.AppendLine($"Cihaz Adı;{CsvEscape(DeviceName)}");
            sb.AppendLine($"A;{CsvEscape(Coefficients.A.ToString(ci))}");
            sb.AppendLine($"B;{CsvEscape(Coefficients.B.ToString(ci))}");
            sb.AppendLine($"C;{CsvEscape(Coefficients.C.ToString(ci))}");
            sb.AppendLine($"R Kare;{CsvEscape(Coefficients.R.ToString(ci))}");
            sb.AppendLine();
            sb.AppendLine("Ölçüm Verileri");
            sb.AppendLine("Gaz Konsantrasyonu;Gaz;Referans;Oran;Transmittans;Absorpsiyon;Tahmin Edilen Absorpsiyon;Tahmin Edilen Gaz Konsantrasyonu");
            foreach (var data in DeviceData)
            {
                sb.AppendLine($"{CsvEscape(data.GasConcentration.ToString(ci))};{CsvEscape(data.Gas.ToString(ci))};{CsvEscape(data.Ref.ToString(ci))};{CsvEscape((data.Ratio ?? 0).ToString(ci))};{CsvEscape((data.Transmittance ?? 0).ToString(ci))};{CsvEscape((data.Absorption ?? 0).ToString(ci))};{CsvEscape((data.PredictedAbsorption ?? 0).ToString(ci))};{CsvEscape((data.PredictedGasConcentration ?? 0).ToString(ci))}");
            }
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            MessageBox.Show($"CSV dosyası başarıyla oluşturuldu:\n{filePath}", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void AddDeviceData(Data data)
        {
            DeviceData.Add(data);
        }

        private void SortDeviceDataByGasConcentration()
        {
            var sortedList = DeviceData.OrderBy(d => d.GasConcentration).ToList();
            DeviceData.Clear();
            foreach (var item in sortedList)
            {
                DeviceData.Add(item);
            }
        }

        private void PlotAndOptimize(double[] x, double[] y)
        {
            Func<Vector<double>, double> objFunc = parameters => x.Zip(y, (xx, yy) => Math.Pow(yy - model(parameters, xx), 2)).Sum();
            var initialGuess = CalculateInitialGuesses(x, y);
            var optimizer = new NelderMeadSimplex(1e-6, 1000);
            var result = optimizer.FindMinimum(ObjectiveFunction.Value(objFunc), initialGuess);
            Coefficients.A = result.MinimizingPoint[0];
            Coefficients.B = result.MinimizingPoint[1];
            Coefficients.C = result.MinimizingPoint[2];
            int numPoints = 100;
            double minX = x.Min();
            double maxX = x.Max();
            double[] xFit = Enumerable.Range(0, numPoints).Select(i => minX + i * (maxX - minX) / (numPoints - 1)).ToArray();
            double[] yFit = xFit.Select(xx => model(result.MinimizingPoint, xx)).ToArray();
            var scatterSeries = new ScatterSeries { Title = "Ölçüm", MarkerType = MarkerType.Circle };
            for (int i = 0; i < x.Length; i++) { scatterSeries.Points.Add(new ScatterPoint(x[i], y[i])); }
            var lineSeries = new LineSeries { Title = "Fit", StrokeThickness = 2 };
            for (int i = 0; i < xFit.Length; i++) { lineSeries.Points.Add(new DataPoint(xFit[i], yFit[i])); }
            PlotModel.Series.Clear();
            PlotModel.Series.Add(scatterSeries);
            PlotModel.Series.Add(lineSeries);
            PlotModel.InvalidatePlot(true);
        }

        private Vector<double> CalculateInitialGuesses(double[] x, double[] y)
        {
            if (!y.Any()) return Vector<double>.Build.DenseOfArray(new double[] { 1, 1, 1 });
            double aGuess = y.Max();
            double xRange = x.Max() - x.Min();
            double bGuess = (xRange > 0) ? 1.0 / xRange : 1.0;
            double cGuess = 1.0;
            return Vector<double>.Build.DenseOfArray(new double[] { aGuess, bGuess, cGuess });
        }

        private bool CanCalculate()
        {
            return DeviceData != null && DeviceData.Any() && DeviceData.All(d => d.Ref > 0 && d.Gas > 0);
        }
        #endregion
    }
}
