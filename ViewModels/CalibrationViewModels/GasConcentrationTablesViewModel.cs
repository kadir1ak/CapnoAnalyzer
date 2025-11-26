using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CapnoAnalyzer.Helpers;
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
using CapnoAnalyzer.Services;

namespace CapnoAnalyzer.ViewModels.CalibrationViewModels
{
    public class GasConcentrationTablesViewModel : BindableBase
    {
        #region Fields & Properties

        public DevicesViewModel DevicesVM { get; private set; }
        public string DeviceName { get; }

        // Ana model fonksiyonu (Görselleştirme için)
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
        public PlotModel PlotModel { get => _plotModel; set => SetProperty(ref _plotModel, value); }

        public ObservableCollection<TemperatureTestViewModel> TemperatureTests { get; set; }

        private TemperatureTestViewModel _selectedTest;
        public TemperatureTestViewModel SelectedTest { get => _selectedTest; set => SetProperty(ref _selectedTest, value); }

        public Coefficients CompensatedCoefficients { get; set; }

        // Ortam Verileri (Ana Kalibrasyon)
        private double _averageTemperature;
        public double AverageTemperature { get => _averageTemperature; set => SetProperty(ref _averageTemperature, value); }

        private double _averagePressure;
        public double AveragePressure { get => _averagePressure; set => SetProperty(ref _averagePressure, value); }

        private double _averageHumidity;
        public double AverageHumidity { get => _averageHumidity; set => SetProperty(ref _averageHumidity, value); }

        #endregion

        #region Commands

        public ICommand CalculateCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ImportCommand { get; }

        public ICommand CreateNewTestCommand { get; }
        public ICommand AddDataToSelectedTestCommand { get; }

        public ICommand CalculateCompensationCommand { get; }
        public ICommand ExportCompensationCommand { get; }
        public ICommand ImportCompensationCommand { get; }

        #endregion

        #region Constructor

        public GasConcentrationTablesViewModel(DevicesViewModel devicesVM, Device selectedDevice)
        {
            DevicesVM = devicesVM;
            DeviceName = selectedDevice.Properties.ProductId;
            DeviceData = new ObservableCollection<Data>();
            Coefficients = new Coefficients();
            CompensatedCoefficients = new Coefficients();

            PlotModel = new PlotModel { Title = $"{DeviceName} : y = a(1 - e^{{-bx^c}})" };
            PlotModel.TitleFontSize = 12;

            CalculateCommand = new RelayCommand(_ => CalculateCoefficients(), _ => CanCalculate());
            ExportCommand = new RelayCommand(_ => ExportToFile(), _ => DeviceData.Any());
            ImportCommand = new RelayCommand(ExecuteImport);

            TemperatureTests = new ObservableCollection<TemperatureTestViewModel>();

            // Varsayılan test sekmeleri
            for (int i = 1; i <= 3; i++)
            {
                var defaultTest = new TemperatureTestViewModel($"Sıcaklık Komp. Testi {i}")
                {
                    ReferenceTestData = { TestNo = i, Alpha = 0.00070, Beta = -0.09850 }
                };
                TemperatureTests.Add(defaultTest);
            }
            SelectedTest = TemperatureTests.FirstOrDefault();

            CreateNewTestCommand = new RelayCommand(ExecuteCreateNewTest);
            AddDataToSelectedTestCommand = new RelayCommand(ExecuteAddDataToSelectedTest, () => SelectedTest != null);

            CalculateCompensationCommand = new RelayCommand(ExecuteCalculateCompensation, () => SelectedTest != null && SelectedTest.TestData.Any());
            ExportCompensationCommand = new RelayCommand(ExecuteExportCompensation, () => SelectedTest != null && SelectedTest.TestData.Any());
            ImportCompensationCommand = new RelayCommand(ExecuteImportCompensation, () => SelectedTest != null);
        }

        #endregion

        #region Main Calibration Methods

        private void CalculateCoefficients()
        {
            try
            {
                SortDeviceDataByGasConcentration();

                var zeroData = DeviceData.FirstOrDefault(d => Math.Abs(d.GasConcentration) < 1e-9);
                if (zeroData == null)
                {
                    MessageBox.Show("0.00 konsantrasyonlu satır bulunamadı! Hesaplama yapılamaz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                double zero = zeroData.Gas / zeroData.Ref;

                // Ortam verilerinin ortalamasını hesapla
                UpdateAverageEnvironmentalData();

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

                    // Ana kalibrasyon tamamlandığında, mevcut testlerin referanslarını güncelle
                    UpdateAllTestsReferenceData(zero);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hesaplama sırasında bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateAverageEnvironmentalData()
        {
            if (DeviceData != null && DeviceData.Any())
            {
                AverageTemperature = DeviceData.Average(d => d.Temperature);
                AveragePressure = DeviceData.Average(d => d.Pressure);
                AverageHumidity = DeviceData.Average(d => d.Humidity);
            }
            else
            {
                AverageTemperature = 0;
                AveragePressure = 0;
                AverageHumidity = 0;
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

        private void SortDeviceDataByGasConcentration()
        {
            var sortedList = DeviceData.OrderBy(d => d.GasConcentration).ToList();
            DeviceData.Clear();
            foreach (var item in sortedList)
            {
                DeviceData.Add(item);
            }
        }

        #endregion

        #region Main Calibration File Operations
        // (Import/Export metodları aynı kalacak, yer tasarrufu için kısaltıldı)
        private void ExecuteImport()
        {
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.Title = "İçeri aktarılacak kalibrasyon dosyasını seçin.";
                dialog.Filters.Add(new CommonFileDialogFilter("Metin/CSV Dosyaları", "*.txt;*.csv"));
                if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return;
                try
                {
                    ParseCalibrationFile(dialog.FileName);
                    UpdateAverageEnvironmentalData();
                    MessageBox.Show($"Veriler başarıyla içe aktarıldı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    if (CanCalculate()) CalculateCoefficients();
                }
                catch (Exception ex) { MessageBox.Show($"Dosya okuma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void ParseCalibrationFile(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            var culture = filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;
            char delimiter = filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? ';' : '\t';

            DeviceData.Clear();
            bool isDataSection = false;
            int sampleCounter = 1;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(delimiter);

                if (!isDataSection)
                {
                    if (parts[0].Contains("Gaz Konsantrasyonu")) { isDataSection = true; continue; }
                    if (parts.Length < 2) continue;
                    string key = parts[0].Trim().Replace("\"", "");
                    string val = parts[1].Trim().Replace("\"", "");
                    if (key.Equals("A", StringComparison.OrdinalIgnoreCase)) Coefficients.A = double.Parse(val, culture);
                    else if (key.Equals("B", StringComparison.OrdinalIgnoreCase)) Coefficients.B = double.Parse(val, culture);
                    else if (key.Equals("C", StringComparison.OrdinalIgnoreCase)) Coefficients.C = double.Parse(val, culture);
                    else if (key.Equals("R Kare", StringComparison.OrdinalIgnoreCase)) Coefficients.R = double.Parse(val, culture);
                }
                else if (parts.Length >= 3)
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
                        if (parts.Length > 8) newData.Temperature = double.Parse(parts[8].Replace("\"", "").Trim(), culture);
                        if (parts.Length > 9) newData.Pressure = double.Parse(parts[9].Replace("\"", "").Trim(), culture);
                        if (parts.Length > 10) newData.Humidity = double.Parse(parts[10].Replace("\"", "").Trim(), culture);
                        DeviceData.Add(newData);
                    }
                    catch { }
                }
            }
        }

        private void ExportToFile()
        {
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.Title = "Dosyaların kaydedileceği klasörü seçiniz.";
                dialog.IsFolderPicker = true;
                if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return;
                string folderPath = dialog.FileName;
                try { ExportToTxt(folderPath); } catch (Exception ex) { MessageBox.Show($"Txt hatası: {ex.Message}"); }
                try { ExportToCsv(folderPath); } catch (Exception ex) { MessageBox.Show($"CSV hatası: {ex.Message}"); }
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
            sb.AppendLine("Gaz Konsantrasyonu\tGaz\tReferans\tOran\tTransmittans\tAbsorpsiyon\tTahmin Edilen Absorpsiyon\tTahmin Edilen Gaz Konsantrasyonu\tSıcaklık\tBasınç\tNem");
            foreach (var data in DeviceData)
            {
                sb.AppendLine($"{data.GasConcentration}\t{data.Gas}\t{data.Ref}\t{data.Ratio}\t{data.Transmittance}\t{data.Absorption}\t{data.PredictedAbsorption}\t{data.PredictedGasConcentration}\t{data.Temperature}\t{data.Pressure}\t{data.Humidity}");
            }
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private void ExportToCsv(string folderPath)
        {
            string filePath = Path.Combine(folderPath, $"{DeviceName}_CalibrationData.csv");
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;
            string Csv(object val) => $"\"{(val?.ToString() ?? "").Replace("\"", "\"\"")}\"";
            sb.AppendLine("Cihaz Bilgileri ve Katsayılar");
            sb.AppendLine($"Cihaz Adı;{Csv(DeviceName)}");
            sb.AppendLine($"A;{Csv(Coefficients.A.ToString(ci))}");
            sb.AppendLine($"B;{Csv(Coefficients.B.ToString(ci))}");
            sb.AppendLine($"C;{Csv(Coefficients.C.ToString(ci))}");
            sb.AppendLine($"R Kare;{Csv(Coefficients.R.ToString(ci))}");
            sb.AppendLine();
            sb.AppendLine("Ölçüm Verileri");
            sb.AppendLine("Gaz Konsantrasyonu;Gaz;Referans;Oran;Transmittans;Absorpsiyon;Tahmin Edilen Absorpsiyon;Tahmin Edilen Gaz Konsantrasyonu;Sıcaklık;Basınç;Nem");
            foreach (var d in DeviceData)
            {
                sb.AppendLine($"{Csv(d.GasConcentration.ToString(ci))};{Csv(d.Gas.ToString(ci))};{Csv(d.Ref.ToString(ci))};{Csv(d.Ratio?.ToString(ci))};{Csv(d.Transmittance?.ToString(ci))};{Csv(d.Absorption?.ToString(ci))};{Csv(d.PredictedAbsorption?.ToString(ci))};{Csv(d.PredictedGasConcentration?.ToString(ci))};{Csv(d.Temperature.ToString(ci))};{Csv(d.Pressure.ToString(ci))};{Csv(d.Humidity.ToString(ci))}");
            }
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
        #endregion

        #region Temperature Compensation Methods

        private void ExecuteCalculateCompensation()
        {
            try
            {
                if (SelectedTest == null) return;

                // 1. Ana Kalibrasyon Verilerini Kontrol Et
                if (Coefficients.A == 0 || Coefficients.B == 0 || Coefficients.C == 0)
                {
                    MessageBox.Show("Lütfen önce Ana Kalibrasyon tablosunu hesaplayın. Referans katsayılar (A, B, C) eksik.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2. Referans Değerleri Ana Tablodan Al
                // T_ref: Ana Kalibrasyonun Ortalama Sıcaklığı
                double refTemp = AverageTemperature;
                if (refTemp == 0) refTemp = 36.5; // Fallback

                // Zero_ref: Ana Kalibrasyondaki 0.00 gaz konsantrasyonuna ait Oran (Gas/Ref)
                var zeroDataPoints = DeviceData.Where(d => Math.Abs(d.GasConcentration) < 0.01).ToList();
                if (!zeroDataPoints.Any())
                {
                    MessageBox.Show("Ana kalibrasyon tablosunda 0.00 gaz konsantrasyonuna sahip veri bulunamadı. Referans Zero hesaplanamıyor.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                double refZero = zeroDataPoints.Average(d => d.Gas / d.Ref);

                // 3. Referans Verilerini Test Modeline İşle
                SelectedTest.ReferenceTestData.Zero = refZero;
                SelectedTest.ReferenceTestData.Temperature = refTemp; // T_ref
                SelectedTest.ReferenceTestData.SpanA = Coefficients.A;
                SelectedTest.ReferenceTestData.B = Coefficients.B;
                SelectedTest.ReferenceTestData.C = Coefficients.C;
                SelectedTest.ReferenceTestData.R = Coefficients.R;

                // 4. Optimizasyon: Alfa ve Beta'yı Bul
                // Hedef: Test verilerindeki "FinalCompensatedConcentration" ile "ActualGasConcentration" arasındaki hatayı minimize et.

                // Optimizasyon için kullanılacak veri seti (Sadece geçerli satırlar)
                var validData = SelectedTest.TestData.Where(d => d.Ref > 0 && d.Gas > 0).ToList();
                if (validData.Count < 2)
                {
                    MessageBox.Show("Optimizasyon için yeterli veri yok.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Sabit Referans Katsayıları
                var refCoeffs = new Coefficients { A = Coefficients.A, B = Coefficients.B, C = Coefficients.C, R = Coefficients.R };

                // Objektif Fonksiyonu: f(alpha, beta) = Sum((Calculated - Actual)^2)
                Func<Vector<double>, double> objectiveFunction = (parameters) =>
                {
                    double alpha = parameters[0];
                    double beta = parameters[1];
                    double totalError = 0;

                    var tempParams = new ThermalCompensationEngine.ThermalModelParameters { Alfa = alpha, Beta = beta };

                    foreach (var row in validData)
                    {
                        // Geçici bir Data nesnesi üzerinde hesaplama yap (orijinal veriyi bozmamak için)
                        var tempData = new Data
                        {
                            GasConcentration = row.GasConcentration,
                            Gas = row.Gas,
                            Ref = row.Ref,
                            Temperature = row.Temperature
                        };

                        var context = new ThermalCompensationEngine.CalculationContext
                        {
                            CurrentDataPoint = tempData,
                            ReferenceZero = refZero,
                            ReferenceTemperature = refTemp,
                            ReferenceCoefficients = refCoeffs,
                            ModelParameters = tempParams
                        };

                        ThermalCompensationEngine.ProcessDataPoint(context);

                        if (tempData.FinalCompensatedConcentration.HasValue && !double.IsNaN(tempData.FinalCompensatedConcentration.Value))
                        {
                            double error = row.GasConcentration - tempData.FinalCompensatedConcentration.Value;
                            totalError += error * error;
                        }
                        else
                        {
                            // Hesaplama başarısızsa (NaN), cezalandır
                            totalError += 10000;
                        }
                    }
                    return totalError;
                };

                // Başlangıç Tahminleri (Mevcut değerler veya varsayılanlar)
                double initialAlpha = SelectedTest.ReferenceTestData.Alpha != 0 ? SelectedTest.ReferenceTestData.Alpha : 0.00070;
                double initialBeta = SelectedTest.ReferenceTestData.Beta != 0 ? SelectedTest.ReferenceTestData.Beta : -0.09850;
                var initialGuess = Vector<double>.Build.DenseOfArray(new[] { initialAlpha, initialBeta });

                // Optimizasyonu Çalıştır (Nelder-Mead)
                var optimizer = new NelderMeadSimplex(1e-5, 2000);
                var result = optimizer.FindMinimum(ObjectiveFunction.Value(objectiveFunction), initialGuess);

                // Sonuçları Al
                double optimizedAlpha = result.MinimizingPoint[0];
                double optimizedBeta = result.MinimizingPoint[1];

                // 5. Sonuçları Kaydet ve Tabloyu Güncelle
                SelectedTest.ReferenceTestData.Alpha = optimizedAlpha;
                SelectedTest.ReferenceTestData.Beta = optimizedBeta;

                // Tüm satırları yeni katsayılarla tekrar hesapla
                var finalParams = new ThermalCompensationEngine.ThermalModelParameters { Alfa = optimizedAlpha, Beta = optimizedBeta };
                foreach (var row in SelectedTest.TestData)
                {
                    var context = new ThermalCompensationEngine.CalculationContext
                    {
                        CurrentDataPoint = row,
                        ReferenceZero = refZero,
                        ReferenceTemperature = refTemp,
                        ReferenceCoefficients = refCoeffs,
                        ModelParameters = finalParams
                    };
                    ThermalCompensationEngine.ProcessDataPoint(context);
                }

                MessageBox.Show($"Optimizasyon Tamamlandı.\n" +
                                $"T_ref: {refTemp:F2}°C, Zero_ref: {refZero:F6}\n" +
                                $"Hesaplanan Alfa: {optimizedAlpha:F6}\n" +
                                $"Hesaplanan Beta: {optimizedBeta:F6}",
                                "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hesaplama hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteExportCompensation()
        {
            if (SelectedTest == null || !SelectedTest.TestData.Any())
            {
                MessageBox.Show("Dışa aktarılacak veri yok.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.Title = "Sıcaklık Testi Kayıt Klasörü Seçin";
                dialog.IsFolderPicker = true;
                if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return;

                string folderPath = dialog.FileName;
                string fileName = $"{DeviceName}_{SelectedTest.Header.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmm}.csv";
                string fullPath = Path.Combine(folderPath, fileName);

                try
                {
                    var sb = new StringBuilder();
                    var ci = CultureInfo.InvariantCulture;
                    string Csv(object val) => $"\"{(val?.ToString() ?? "").Replace("\"", "\"\"")}\"";

                    sb.AppendLine("Sample;GasConcentration;Ref;Gas;Temperature;Pressure;Humidity;Ratio;Transmittance;Absorption;PredictedAbsorption;NR;NA;Span;NR_Comp;Span_Comp;FinalCompensatedConcentration");

                    foreach (var d in SelectedTest.TestData)
                    {
                        sb.AppendLine($"{Csv(d.Sample)};{Csv(d.GasConcentration.ToString(ci))};{Csv(d.Ref.ToString(ci))};{Csv(d.Gas.ToString(ci))};" +
                                      $"{Csv(d.Temperature.ToString(ci))};{Csv(d.Pressure.ToString(ci))};{Csv(d.Humidity.ToString(ci))};" +
                                      $"{Csv(d.Ratio?.ToString(ci))};{Csv(d.Transmittance?.ToString(ci))};{Csv(d.Absorption?.ToString(ci))};{Csv(d.PredictedAbsorption?.ToString(ci))};" +
                                      $"{Csv(d.NormalizedRatio?.ToString(ci))};{Csv(d.NormalizedAbsorbance?.ToString(ci))};{Csv(d.Span?.ToString(ci))};" +
                                      $"{Csv(d.CompensatedNormalizedRatio?.ToString(ci))};{Csv(d.CompensatedSpan?.ToString(ci))};{Csv(d.FinalCompensatedConcentration?.ToString(ci))}");
                    }

                    File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Dosya kaydedildi:\n{fullPath}", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteImportCompensation()
        {
            if (SelectedTest == null) return;

            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.Title = "Sıcaklık Testi Verisi Seçin (CSV)";
                dialog.Filters.Add(new CommonFileDialogFilter("CSV Dosyaları", "*.csv"));
                if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return;

                try
                {
                    var lines = File.ReadAllLines(dialog.FileName);
                    if (lines.Length < 2) return;

                    SelectedTest.TestData.Clear();

                    // Kültür bağımsız okuma için InvariantCulture kullanıyoruz.
                    var ci = CultureInfo.InvariantCulture;

                    // Yardımcı yerel fonksiyon: Hem virgül hem nokta desteği sağlar
                    double ParseDouble(string input)
                    {
                        if (string.IsNullOrWhiteSpace(input)) return 0;

                        // 1. Tırnak işaretlerini temizle
                        string cleanInput = input.Replace("\"", "").Trim();

                        // 2. Virgülü noktaya çevir (Excel TR formatı uyumu için)
                        // Böylece "2,514" -> "2.514" olur ve InvariantCulture bunu doğru okur.
                        cleanInput = cleanInput.Replace(",", ".");

                        if (double.TryParse(cleanInput, NumberStyles.Any, ci, out double result))
                        {
                            return result;
                        }
                        return 0;
                    }

                    for (int i = 1; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var parts = line.Split(';');
                        if (parts.Length >= 4)
                        {
                            var d = new Data
                            {
                                Sample = parts[0].Replace("\"", ""),
                                // ParseDouble fonksiyonunu kullanarak değerleri okuyoruz
                                GasConcentration = ParseDouble(parts[1]),
                                Ref = ParseDouble(parts[2]),
                                Gas = ParseDouble(parts[3]),
                                Temperature = parts.Length > 4 ? ParseDouble(parts[4]) : 0,
                                Pressure = parts.Length > 5 ? ParseDouble(parts[5]) : 0,
                                Humidity = parts.Length > 6 ? ParseDouble(parts[6]) : 0
                            };
                            SelectedTest.TestData.Add(d);
                        }
                    }
                    MessageBox.Show("Veriler başarıyla içe aktarıldı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Import hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteCreateNewTest()
        {
            int testCount = TemperatureTests.Count + 1;
            var newTest = new TemperatureTestViewModel($"Sıcaklık Komp. Testi {testCount}");
            newTest.ReferenceTestData.TestNo = testCount;
            newTest.ReferenceTestData.Alpha = 0.00070;
            newTest.ReferenceTestData.Beta = -0.09850;

            // Eğer ana kalibrasyon varsa, referansları kopyala
            if (Coefficients.R != 0)
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
                MessageBox.Show("Veri eklemek için lütfen önce bir test seçin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
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
            test.ReferenceTestData.Zero = zeroValue;
            test.ReferenceTestData.Temperature = AverageTemperature; // T_ref
            test.ReferenceTestData.SpanA = Coefficients.A;
            test.ReferenceTestData.B = Coefficients.B;
            test.ReferenceTestData.C = Coefficients.C;
            test.ReferenceTestData.R = Coefficients.R;
        }

        #endregion
    }
}
