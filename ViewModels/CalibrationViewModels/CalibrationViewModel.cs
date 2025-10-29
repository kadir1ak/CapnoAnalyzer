using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Models.Device;
using CapnoAnalyzer.ViewModels.DeviceViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace CapnoAnalyzer.ViewModels.CalibrationViewModels
{
    public class CalibrationViewModel : BindableBase
    {
        // Hangi tür kalibrasyon yapıldığını belirtmek için enum
        private enum CalibrationType { Main, Thermal }
        private CalibrationType _currentCalibrationType;

        public DevicesViewModel DevicesVM { get; private set; }
        public ObservableCollection<GasConcentrationTablesViewModel> DeviceTables { get; set; }

        // --- YENİ: Aktif cihazı ve komutları yönetmek için ---

        private GasConcentrationTablesViewModel _activeDeviceTable;
        /// <summary>
        /// Kalibrasyon Tabloları sayfasında şu anda seçili olan cihaz sekmesinin ViewModel'i.
        /// </summary>
        public GasConcentrationTablesViewModel ActiveDeviceTable
        {
            get => _activeDeviceTable;
            set => SetProperty(ref _activeDeviceTable, value);
        }

        /// <summary>
        /// Yeni bir sıcaklık testi oluşturur.
        /// </summary>
        public ICommand StartNewThermalTestCommand { get; private set; }

        /// <summary>
        /// ComboBox'ta seçili olan sıcaklık testine yeni bir ölçüm değeri ekler.
        /// </summary>
        public ICommand AddValueToSelectedThermalTestCommand { get; private set; }

        // --- Mevcut Özellikler ---

        public ICommand AppliedGasCommand { get; private set; }

        private bool _isInputEnabled = true;
        public bool IsInputEnabled { get => _isInputEnabled; set => SetProperty(ref _isInputEnabled, value); }

        private int _mainSampleTimeProgressBar = 0;
        public int MainSampleTimeProgressBar { get => _mainSampleTimeProgressBar; set => SetProperty(ref _mainSampleTimeProgressBar, value); }

        private double? _appliedGasConcentration = null;
        public double? AppliedGasConcentration { get => _appliedGasConcentration; set => SetProperty(ref _appliedGasConcentration, value); }

        private DispatcherTimer _timer;
        private Dictionary<string, List<double>> _refDataBuffer;
        private Dictionary<string, List<double>> _gasDataBuffer;
        private Dictionary<string, List<double>> _tempDataBuffer;
        private Dictionary<string, List<double>> _pressureDataBuffer;
        private Dictionary<string, List<double>> _humidityDataBuffer;


        public CalibrationViewModel(DevicesViewModel devicesVM)
        {
            DevicesVM = devicesVM;
            DeviceTables = new ObservableCollection<GasConcentrationTablesViewModel>();

            // Komutları başlat
            AppliedGasCommand = new RelayCommand(() => StartSamplingCalculation(CalibrationType.Main));
            StartNewThermalTestCommand = new RelayCommand(ExecuteStartNewThermalTest, CanExecuteThermalTestCommands);
            AddValueToSelectedThermalTestCommand = new RelayCommand(ExecuteAddValueToSelectedThermalTest, CanExecuteThermalTestCommands);

            DevicesVM.DeviceAdded += OnDeviceAdded;
            DevicesVM.DeviceRemoved += OnDeviceRemoved;

            foreach (var device in DevicesVM.IdentifiedDevices)
            {
                AddDeviceTable(device.Properties.ProductId);
            }

            _refDataBuffer = new Dictionary<string, List<double>>();
            _gasDataBuffer = new Dictionary<string, List<double>>();
            _tempDataBuffer = new Dictionary<string, List<double>>();
            _pressureDataBuffer = new Dictionary<string, List<double>>();
            _humidityDataBuffer = new Dictionary<string, List<double>>();
        }

        // --- YENİ KOMUT METOTLARI ---

        private void ExecuteStartNewThermalTest()
        {
            // Komutu aktif olan cihazın ViewModel'ine yönlendir
            ActiveDeviceTable?.CreateNewTestCommand.Execute(null);
        }

        private void ExecuteAddValueToSelectedThermalTest()
        {
            if (ActiveDeviceTable?.SelectedTest == null)
            {
                MessageBox.Show("Lütfen önce bir sıcaklık testi seçin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Örneklemeyi "Thermal" modunda başlat
            StartSamplingCalculation(CalibrationType.Thermal);
        }

        private bool CanExecuteThermalTestCommands()
        {
            return ActiveDeviceTable != null && IsInputEnabled;
        }

        // --- GÜNCELLENMİŞ ÖRNEKLEM ALMA MANTIĞI ---

        private void StartSamplingCalculation(CalibrationType type)
        {
            if (AppliedGasConcentration == null)
            {
                MessageBox.Show("Uygulanan gaz konsantrasyonu belirtilmedi!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _currentCalibrationType = type; // Hangi modda olduğumuzu kaydet
            IsInputEnabled = false;
            MainSampleTimeProgressBar = 0;

            // Veri tamponlarını sıfırla
            _refDataBuffer.Clear();
            _gasDataBuffer.Clear();
            _tempDataBuffer.Clear();
            _pressureDataBuffer.Clear();
            _humidityDataBuffer.Clear();

            int mainSampleMaxTimeCount = 0;
            foreach (var device in DevicesVM.IdentifiedDevices)
            {
                var id = device.Properties.ProductId;
                _refDataBuffer[id] = new List<double>();
                _gasDataBuffer[id] = new List<double>();
                _tempDataBuffer[id] = new List<double>();
                _pressureDataBuffer[id] = new List<double>();
                _humidityDataBuffer[id] = new List<double>();

                device.Interface.IsInputEnabled = false;
                device.Interface.SampleTimeCount = 0;
                if (device.Interface.SampleTime > mainSampleMaxTimeCount)
                {
                    mainSampleMaxTimeCount = device.Interface.SampleTime;
                }
            }

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += (sender, args) => TimerTick(sender, args, mainSampleMaxTimeCount);
            _timer.Start();
        }

        private void TimerTick(object sender, EventArgs e, int maxTime)
        {
            // 1. Ana ProgressBar'ı doğru ondalıklı hesaplama ile ilerlet.
            // (100.0 / maxTime) -> 1 saniyedeki ilerleme yüzdesi.
            // Bunu 10'a bölerek 100ms'deki ilerlemeyi buluyoruz.
            double increment = (100.0 / maxTime) / 10.0;
            MainSampleTimeProgressBar += (int)Math.Round(increment); // Yuvarlama ile daha hassas artış

            // Değerin 100'ü geçmediğinden emin ol
            if (MainSampleTimeProgressBar > 100)
            {
                MainSampleTimeProgressBar = 100;
            }

            // 2. Her cihazın ProgressBar'ını ve veri toplamayı yönet.
            foreach (var device in DevicesVM.IdentifiedDevices)
            {
                var id = device.Properties.ProductId;

                // Cihazın kendi örnekleme süresi dolana kadar veri topla.
                // Geçen süreyi ana ilerleme çubuğunun yüzdesinden hesaplayabiliriz.
                double elapsedSeconds = (MainSampleTimeProgressBar / 100.0) * maxTime;

                if (elapsedSeconds <= device.Interface.SampleTime)
                {
                    // Cihazın ProgressBar'ını geçen süreye göre güncelle.
                    // Bu, ana bar ile tam senkronizasyon sağlar.
                    device.Interface.SampleTimeProgressBar = (int)((elapsedSeconds / device.Interface.SampleTime) * 100.0);

                    if (device.Interface.SampleTimeProgressBar > 100)
                    {
                        device.Interface.SampleTimeProgressBar = 100;
                    }

                    // Sensör verilerini topla
                    (double refValue, double gasValue) = GetSensorValues(device);
                    _refDataBuffer[id].Add(refValue);
                    _gasDataBuffer[id].Add(gasValue);

                    // Ortam verilerini topla
                    _tempDataBuffer[id].Add(device.Interface.Data.Temperature);
                    _pressureDataBuffer[id].Add(device.Interface.Data.Pressure);
                    _humidityDataBuffer[id].Add(device.Interface.Data.Humidity);
                }
                else
                {
                    // Eğer bu cihazın süresi dolduysa ama ana süre devam ediyorsa,
                    // barını 100'de sabit tut.
                    device.Interface.SampleTimeProgressBar = 100;
                }
            }

            // 3. Ana süre dolduğunda zamanlayıcıyı durdur.
            if (MainSampleTimeProgressBar >= 100)
            {
                _timer.Stop();

                // Cihazların barlarının da 100 olduğundan emin ol
                foreach (var device in DevicesVM.IdentifiedDevices)
                {
                    device.Interface.SampleTimeProgressBar = 100;
                }

                var waitTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                waitTimer.Tick += (s, args) => { waitTimer.Stop(); CompleteCalibration(); };
                waitTimer.Start();
            }
        }

        private void CompleteCalibration()
        {
            foreach (var device in DevicesVM.IdentifiedDevices)
            {
                var id = device.Properties.ProductId;
                double calculatedRefValue = CalculateValue(device.Interface.SampleMode, _refDataBuffer[id]);
                double calculatedGasValue = CalculateValue(device.Interface.SampleMode, _gasDataBuffer[id]);

                var table = DeviceTables.FirstOrDefault(t => t.DeviceName == id);
                if (table == null) continue;

                var newDeviceData = new Data
                {
                    GasConcentration = (double)AppliedGasConcentration,
                    Ref = Math.Round(calculatedRefValue, 4),
                    Gas = Math.Round(calculatedGasValue, 4),
                    // Ortalama ortam verilerini ekle
                    Temperature = CalculateValue(SampleMode.AVG, _tempDataBuffer[id]),
                    Pressure = CalculateValue(SampleMode.AVG, _pressureDataBuffer[id]),
                    Humidity = CalculateValue(SampleMode.AVG, _humidityDataBuffer[id])
                };

                // VERİYİ DOĞRU YERE EKLE
                if (_currentCalibrationType == CalibrationType.Main)
                {
                    var lastSample = table.DeviceData.LastOrDefault();
                    newDeviceData.Sample = (lastSample != null ? int.Parse(lastSample.Sample) + 1 : 1).ToString();
                    table.DeviceData.Add(newDeviceData);
                }
                else // Thermal
                {
                    if (table.SelectedTest != null)
                    {
                        table.SelectedTest.TestData.Add(newDeviceData);
                    }
                }

                device.Interface.SampleTimeCount = 0;
                device.Interface.SampleTimeProgressBar = 0;
                device.Interface.IsInputEnabled = true;
            }

            MainSampleTimeProgressBar = 0;
            IsInputEnabled = true;
        }

        #region Helper Metotları (Değişiklik Yok)
        private (double, double) GetSensorValues(Device device)
        {
            double refValue = 0, gasValue = 0;
            if (device.Properties.DataPacketType == "2")
            {
                refValue = device.DataPacket_2.GainAdsVoltagesIIR[1];
                gasValue = device.DataPacket_2.GainAdsVoltagesIIR[0];
            }
            // Diğer paket tipleri için de eklenebilir...
            return (refValue, gasValue);
        }

        private double CalculateValue(SampleMode mode, List<double> data)
        {
            if (data == null || !data.Any()) return 0;
            switch (mode)
            {
                case SampleMode.AVG: return data.Average();
                case SampleMode.RMS: return Math.Sqrt(data.Select(d => d * d).Average());
                case SampleMode.PP: return data.Max() - data.Min();
                default: return 0;
            }
        }

        private void OnDeviceAdded(Device newDevice) => AddDeviceTable(newDevice.Properties.ProductId);
        private void OnDeviceRemoved(Device removedDevice) => RemoveDeviceTable(removedDevice.Properties.ProductId);

        private void AddDeviceTable(string deviceName)
        {
            // Cihaz için zaten bir tablo varsa tekrar ekleme.
            if (DeviceTables.Any(t => t.DeviceName == deviceName))
            {
                // İSTEĞE BAĞLI: Eğer zaten var olan bir cihaz tekrar bağlanırsa,
                // o sekmeyi tekrar aktif hale getirebilirsiniz.
                var existingTable = DeviceTables.FirstOrDefault(t => t.DeviceName == deviceName);
                if (existingTable != null)
                {
                    ActiveDeviceTable = existingTable;
                }
                return;
            }

            // İlgili cihaz nesnesini bul.
            var selectedDevice = DevicesVM.IdentifiedDevices.FirstOrDefault(d => d.Properties.ProductId == deviceName);
            if (selectedDevice != null)
            {
                // Cihaz için yeni bir tablo ViewModel'i oluştur.
                var newDeviceTable = new GasConcentrationTablesViewModel(DevicesVM, selectedDevice);

                // Yeni tabloyu koleksiyona ekle. Bu, UI'da yeni bir sekme oluşturur.
                DeviceTables.Add(newDeviceTable);

                // --- İSTENEN İŞLEVSELLİK İÇİN EKLENEN SATIR ---
                // Yeni oluşturulan bu tabloyu "aktif" tablo olarak ayarla.
                // XAML'deki binding sayesinde UI'daki TabControl bu sekmeyi seçecektir.
                ActiveDeviceTable = newDeviceTable;
            }
        }

        private void RemoveDeviceTable(string deviceName)
        {
            var table = DeviceTables.FirstOrDefault(t => t.DeviceName == deviceName);
            if (table != null) DeviceTables.Remove(table);
        }
        #endregion
    }
}
