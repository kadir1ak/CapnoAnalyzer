using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Models.Device;
using CapnoAnalyzer.ViewModels.DeviceViewModels;
using CapnoAnalyzer.Views.DevicesViews.Devices;
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
        public DevicesViewModel DevicesVM { get; private set; }
        public ObservableCollection<GasConcentrationTablesViewModel> DeviceTables { get; set; }

        public ICommand AppliedGasCommand { get; private set; }

        private bool _isInputEnabled = true;
        public bool IsInputEnabled
        {
            get => _isInputEnabled;
            set => SetProperty(ref _isInputEnabled, value);
        }

        private int _sample = 0;
        public int Sample
        {
            get => _sample;
            set => SetProperty(ref _sample, value);
        }

        private int _mainSampleMaxTimeCount = 0;
        public int MainSampleMaxTimeCount
        {
            get => _mainSampleMaxTimeCount;
            set => SetProperty(ref _mainSampleMaxTimeCount, value);
        }

        private int _mainTimeCount = 0;
        public int MainTimeCount
        {
            get => _mainTimeCount;
            set => SetProperty(ref _mainTimeCount, value);
        }

        private int _mainSampleTimeCount = 0;
        public int MainSampleTimeCount
        {
            get => _mainSampleTimeCount;
            set => SetProperty(ref _mainSampleTimeCount, value);
        }

        private int _mainSampleTimeProgressBar = 0;
        public int MainSampleTimeProgressBar
        {
            get => _mainSampleTimeProgressBar;
            set => SetProperty(ref _mainSampleTimeProgressBar, value);
        }

        private double? _appliedGasConcentration = null;
        public double? AppliedGasConcentration
        {
            get => _appliedGasConcentration;
            set => SetProperty(ref _appliedGasConcentration, value);
        }

        private DispatcherTimer _timer;
        private Dictionary<string, List<double>> _refDataBuffer;
        private Dictionary<string, List<double>> _gasDataBuffer;

        public CalibrationViewModel(DevicesViewModel devicesVM)
        {
            DevicesVM = devicesVM;
            DeviceTables = new ObservableCollection<GasConcentrationTablesViewModel>();

            AppliedGasCommand = new RelayCommand(StartSamplingCalculation);

            DevicesVM.DeviceAdded += OnDeviceAdded;
            DevicesVM.DeviceRemoved += OnDeviceRemoved;

            foreach (var device in DevicesVM.IdentifiedDevices)
            {
                AddDeviceTable(device.Properties.ProductId);
            }

            _refDataBuffer = new Dictionary<string, List<double>>();
            _gasDataBuffer = new Dictionary<string, List<double>>();
        }

        private void StartSamplingCalculation()
        {
            if (AppliedGasConcentration == null)
            {
                MessageBox.Show("Uygulanan gaz konsantrasyonu belirtilmedi!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (AppliedGasConcentration < 0 || AppliedGasConcentration > 100)
            {
                MessageBox.Show("Uygulanan gaz konsantrasyonu 0 ile 100 arasında olmalıdır!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Girişleri devre dışı bırak
            IsInputEnabled = false;

            // Sayaç başlangıcı
            MainSampleTimeCount = 0;

            // Veri tamponlarını sıfırla
            _refDataBuffer.Clear();
            _gasDataBuffer.Clear();
            MainSampleMaxTimeCount = 0;
            foreach (var device in DevicesVM.IdentifiedDevices)
            {
                _refDataBuffer[device.Properties.ProductId] = new List<double>();
                _gasDataBuffer[device.Properties.ProductId] = new List<double>();

                // Girişleri devre dışı bırak
                device.Interface.IsInputEnabled = false;
                device.Interface.SampleTimeCount = 0;
                if (device.Interface.SampleTime > MainSampleMaxTimeCount) 
                {
                    MainSampleMaxTimeCount = device.Interface.SampleTime;
                }
            }

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Tick += TimerTick;
            _timer.Start();
        }

        private void TimerTick(object sender, EventArgs e)
        {
            // Ana sayaç ilerlet
            MainTimeCount++;
            if (MainTimeCount >= 10)
            {
                MainTimeCount = 0;
                MainSampleTimeCount += (int)(100 / MainSampleMaxTimeCount);

                // MainProgressBar'ı güncelle
                MainSampleTimeProgressBar = MainSampleTimeCount;
            }

            // Her cihazın zamanlayıcı mantığını kontrol et
            foreach (var device in DevicesVM.IdentifiedDevices)
            {
                var productId = device.Properties.ProductId;

                // Cihazın zamanlayıcı mantığını güncelle
                if (device.Interface.SampleTimeCount < device.Interface.SampleTime)
                {
                    device.Interface.TimeCount++;
                    if (device.Interface.TimeCount >= 10)
                    {
                        device.Interface.TimeCount = 0;
                       
                        device.Interface.SampleTimeCount++;
                        device.Interface.SampleTimeProgressBar = (int)((100.0 / device.Interface.SampleTime) * device.Interface.SampleTimeCount);
                    }

                    // Sensör verilerini topla
                    double refValue = 0;
                    double gasValue = 0;
                    if (device.Properties.DataPacketType == "1")
                    {
                        refValue = device.DataPacket_1.ReferenceSensor;
                        gasValue = device.DataPacket_1.GasSensor;
                    }
                    else if (device.Properties.DataPacketType == "2")
                    {
                        refValue = device.DataPacket_2.GainAdsVoltagesIIR[1];
                        gasValue = device.DataPacket_2.GainAdsVoltagesIIR[0];
                    }
                    else if (device.Properties.DataPacketType == "3")
                    {
                        refValue = device.DataPacket_3.Ch1;
                        gasValue = device.DataPacket_3.Ch0;
                    }

                    _refDataBuffer[productId].Add(refValue);
                    _gasDataBuffer[productId].Add(gasValue);
                }
            }

            // Ana sayaç maks süreye ulaştıysa işlemi bitir
            if (MainSampleTimeCount >= 100)
            {
                _timer.Stop();

                // 3 saniyelik bekleme için yeni bir zamanlayıcı başlat
                var waitTimer = new DispatcherTimer();
                waitTimer.Interval = TimeSpan.FromSeconds(3);
                waitTimer.Tick += (s, args) =>
                {
                    waitTimer.Stop();
                    CompleteCalibration();
                };
                waitTimer.Start();
            }
        }

        private void CompleteCalibration()
        {
            foreach (var device in DevicesVM.IdentifiedDevices)
            {
                var productId = device.Properties.ProductId;

                // SampleMode durumuna göre hesaplama yap
                double calculatedRefValue = 0;
                double calculatedGasValue = 0;

                switch (device.Interface.SampleMode)
                {
                    case SampleMode.AVG:
                        calculatedRefValue = CalculateAverage(_refDataBuffer[productId]);
                        calculatedGasValue = CalculateAverage(_gasDataBuffer[productId]);
                        break;

                    case SampleMode.RMS:
                        calculatedRefValue = CalculateRMS(_refDataBuffer[productId]);
                        calculatedGasValue = CalculateRMS(_gasDataBuffer[productId]);
                        break;

                    case SampleMode.PP:
                        calculatedRefValue = CalculatePeakToPeak(_refDataBuffer[productId]);
                        calculatedGasValue = CalculatePeakToPeak(_gasDataBuffer[productId]);
                        break;
                }

                // İlgili tabloyu bul
                var table = DeviceTables.FirstOrDefault(t => t.DeviceName == productId);
                if (table == null) continue;

                // Yeni veri oluştur
                var lastSample = table.DeviceData.LastOrDefault();
                int newSampleNumber = lastSample != null ? int.Parse(lastSample.Sample) + 1 : 1;

                var newDeviceData = new Data
                {
                    Sample = newSampleNumber.ToString(),
                    GasConcentration = (double)AppliedGasConcentration,
                    Ref = Math.Round(calculatedRefValue, 4),
                    Gas = Math.Round(calculatedGasValue, 4)
                };

                // Yeni veriyi tabloya ekle
                table.DeviceData.Add(newDeviceData);

                // SampleTime sıfırla
                device.Interface.SampleTimeCount = 0;
                device.Interface.SampleTimeProgressBar = 0;

                // Girişleri tekrar aktif hale getir
                device.Interface.IsInputEnabled = true;
            }

            // SampleTime sıfırla
            MainSampleTimeCount = 0;
            MainSampleTimeProgressBar = 0;

            // Girişleri tekrar aktif hale getir
            IsInputEnabled = true;
        }
        private double CalculateAverage(List<double> data)
        {
            if (data == null || data.Count == 0)
                return 0;

            // Daha performanslı bir ortalama hesaplama
            double sum = 0;
            foreach (var value in data)
            {
                sum += value;
            }

            return sum / data.Count;
        }
        private double CalculateRMS(List<double> data)
        {
            if (data == null || data.Count == 0)
                return 0;

            // Performansı artırmak için "foreach" döngüsü ile RMS hesaplama
            double sumOfSquares = 0;
            foreach (var value in data)
            {
                sumOfSquares += value * value;
            }

            return Math.Sqrt(sumOfSquares / data.Count);
        }
        private double CalculatePeakToPeak(List<double> data)
        {
            if (data == null || data.Count == 0)
                return 0;

            // Minimum ve maksimum değerleri tek döngüde bul
            double min = double.MaxValue;
            double max = double.MinValue;

            foreach (var value in data)
            {
                if (value < min) min = value;
                if (value > max) max = value;
            }

            return max - min;
        }

        private void OnDeviceAdded(Device newDevice)
        {
            AddDeviceTable(newDevice.Properties.ProductId);
        }

        private void OnDeviceRemoved(Device removedDevice)
        {
            RemoveDeviceTable(removedDevice.Properties.ProductId);
        }

        private void AddDeviceTable(string deviceName)
        {
            if (DeviceTables.Any(t => t.DeviceName == deviceName))
            {
                return;
            }

            // Cihazı bul (Varsayım: Devices içinde Device nesneleri var)
            var selectedDevice = DevicesVM.IdentifiedDevices.FirstOrDefault(device => device.Properties.ProductId == deviceName);
            if (selectedDevice != null)
            {
                var table = new GasConcentrationTablesViewModel(DevicesVM, selectedDevice);
                DeviceTables.Add(table);
            }
            else
            {
                Console.WriteLine("❌ Cihaz bulunamadı.");
            }
        }

        private void RemoveDeviceTable(string deviceName)
        {
            var table = DeviceTables.FirstOrDefault(t => t.DeviceName == deviceName);
            if (table != null)
            {
                DeviceTables.Remove(table);
            }
        }
    }
}
