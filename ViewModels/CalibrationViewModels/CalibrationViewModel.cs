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
        public DevicesViewModel Devices { get; private set; }
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

        private int _mainSampleTime = 0;
        public int MainSampleTime
        {
            get => _mainSampleTime;
            set => SetProperty(ref _mainSampleTime, value);
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

        public CalibrationViewModel(DevicesViewModel devices)
        {
            Devices = devices;
            DeviceTables = new ObservableCollection<GasConcentrationTablesViewModel>();

            AppliedGasCommand = new RelayCommand(StartSamplingCalculation);

            Devices.DeviceAdded += OnDeviceAdded;
            Devices.DeviceRemoved += OnDeviceRemoved;

            foreach (var device in Devices.IdentifiedDevices)
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
            MainSampleTime = 0;

            // Veri tamponlarını sıfırla
            _refDataBuffer.Clear();
            _gasDataBuffer.Clear();

            foreach (var device in Devices.IdentifiedDevices)
            {
                _refDataBuffer[device.Properties.ProductId] = new List<double>();
                _gasDataBuffer[device.Properties.ProductId] = new List<double>();

                // Girişleri devre dışı bırak
                device.Interface.IsInputEnabled = false;
                device.Interface.SampleTimeCount = 0;
            }

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Tick += TimerTick;
            _timer.Start();
        }

        private void TimerTick(object sender, EventArgs e)
        {
            // Sayaç ilerlet
            MainSampleTime += 1;
        
            // Cihazlardan veri topla
            foreach (var device in Devices.IdentifiedDevices)
            {
                var productId = device.Properties.ProductId;

                // Sensör verilerini al
                double refValue = device.Interface.Data.ReferenceSensor;
                double gasValue = device.Interface.Data.GasSensor;

                // Verileri tamponlara ekle
                _refDataBuffer[productId].Add(refValue);
                _gasDataBuffer[productId].Add(gasValue);

                device.Interface.SampleTimeCount = MainSampleTime;
            }

            // Sayaç 10 saniyeye ulaştıysa işlemi bitir
            if (MainSampleTime >= 100)
            {
                _timer.Stop();
                CompleteCalibration();
            }
        }

        private void CompleteCalibration()
        {
            foreach (var device in Devices.IdentifiedDevices)
            {
                var productId = device.Properties.ProductId;

                // RMS değerlerini hesapla
                double rmsRef = CalculateRMS(_refDataBuffer[productId]);
                double rmsGas = CalculateRMS(_gasDataBuffer[productId]);

                // İlgili tabloyu bul
                var table = DeviceTables.FirstOrDefault(t => t.DeviceName == productId);
                if (table == null) continue;

                // Yeni veri oluştur
                var lastSample = table.DeviceData.LastOrDefault();
                int newSampleNumber = lastSample != null ? int.Parse(lastSample.Sample) + 1 : 1;

                var newDeviceData = new DeviceData
                {
                    Sample = newSampleNumber.ToString(),
                    GasConcentration = (double)AppliedGasConcentration,
                    Ref = Math.Round(rmsRef, 4),
                    Gas = Math.Round(rmsGas, 4)
                };

                // Yeni veriyi tabloya ekle
                table.DeviceData.Add(newDeviceData);


                // SampleTime sıfırla
                device.Interface.SampleTimeCount = 0;

                // Girişleri tekrar aktif hale getir
                device.Interface.IsInputEnabled = true;
            }

            // SampleTime sıfırla
            MainSampleTime = 0;

            // Girişleri tekrar aktif hale getir
            IsInputEnabled = true;

            // MessageBox.Show("Kalibrasyon tamamlandı ve veriler tabloya eklendi!", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private double CalculateRMS(List<double> data)
        {
            if (data == null || data.Count == 0) return 0;

            double sumOfSquares = data.Sum(x => x * x);
            return Math.Sqrt(sumOfSquares / data.Count);
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

            var table = new GasConcentrationTablesViewModel(deviceName);
            DeviceTables.Add(table);
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
