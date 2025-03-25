using System;
using System.Windows;
using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Models.Device;
using CapnoAnalyzer.ViewModels.DeviceViewModels;
using System.Collections.ObjectModel;
using System.Windows.Documents;
using CapnoAnalyzer.Views.DevicesViews.Devices;
using System.Windows.Input;

namespace CapnoAnalyzer.ViewModels.CalibrationViewModels
{
    public class CalibrationViewModel : BindableBase
    {
        public DevicesViewModel Devices { get; private set; }
        public ObservableCollection<GasConcentrationTablesViewModel> DeviceTables { get; set; }

        public ICommand ShowAppliedGasCommand { get; private set; }

        private int _sample = 0;
        public int Sample
        {
            get => _sample;
            set => SetProperty(ref _sample, value);
        }

        private string _appliedGasConcentration;
        public string AppliedGasConcentration
        {
            get => _appliedGasConcentration;
            set => SetProperty(ref _appliedGasConcentration, value);
        }

        public CalibrationViewModel(DevicesViewModel devices)
        {
            Devices = devices;
            DeviceTables = new ObservableCollection<GasConcentrationTablesViewModel>();

            // Komut tanımla
            ShowAppliedGasCommand = new RelayCommand(ShowAppliedGas);

            // DevicesViewModel'deki olayları dinliyoruz
            Devices.DeviceAdded += OnDeviceAdded;
            Devices.DeviceRemoved += OnDeviceRemoved;

            // Mevcut cihazları ekle
            foreach (var device in Devices.IdentifiedDevices)
            {
                AddDeviceTable(device.Properties.ProductId);
            }
        }
        private void ShowAppliedGas()
        {
            if (string.IsNullOrWhiteSpace(AppliedGasConcentration))
            {
                MessageBox.Show("Uygulanan gaz konsantrasyonu belirtilmedi!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Her cihaz tablosuna yeni bir satır ekle
            foreach (var table in DeviceTables)
            {
                // Tablo boşsa başlangıç değerlerini oluştur
                var lastSample = table.DeviceData.LastOrDefault();
                int newSampleNumber = lastSample != null ? int.Parse(lastSample.Sample) + 1 : 1; // Sample değeri belirle

                // Yeni veri oluştur
                var newDeviceData = new DeviceData
                {
                    Sample = newSampleNumber.ToString(),
                    GasConcentration = double.TryParse(AppliedGasConcentration, out var gasConcentration) ? gasConcentration : 0.0,
                    Ref = Math.Round(2680 + new Random().NextDouble() * 40, 4), // Rastgele Ref değeri
                    Gas = Math.Round(3200 + new Random().NextDouble() * 2000, 4) // Rastgele Gas değeri
                };

                // Yeni veriyi tabloya ekle
                table.DeviceData.Add(newDeviceData);
            }

            // İşlem tamamlandıktan sonra kullanıcıya bilgi ver
            MessageBox.Show("Tüm cihaz tablolarına yeni bir satır eklendi!", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnDeviceAdded(Device newDevice)
        {
            AddDeviceTable(newDevice.Properties.ProductId);
            //DeviceTableTest(newDevice); // Yeni cihaz için veri tablosunu test et ve veri ekle
        }

        private void OnDeviceRemoved(Device removedDevice)
        {
            RemoveDeviceTable(removedDevice.Properties.ProductId);
        }

        private void AddDeviceTable(string deviceName)
        {
            if (DeviceTables.Any(t => t.DeviceName == deviceName))
            {
                return; // Zaten tablo varsa ekleme
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

        private void DeviceTableTest(Device device)
        {
            // Örnek veri oluştur
            var sampleData = GenerateSampleData(); // Veri oluşturulacak

            // İlgili cihazın tablosunu al veya oluştur
            var table = DeviceTables.FirstOrDefault(t => t.DeviceName == device.Properties.ProductId)
                        ?? new GasConcentrationTablesViewModel(device.Properties.ProductId);

            if (!DeviceTables.Contains(table))
                DeviceTables.Add(table);

            // Verileri tabloya ekle
            foreach (var data in sampleData)
            {
                table.AddDeviceData(data);
            }
        }

        private List<DeviceData> GenerateSampleData()
        {
            var sampleData = new List<DeviceData>();
            var random = new Random();

            // Sayaç başlangıç değeri
            int sampleCounter = 1; // 1'den başlayarak numaralandırma yapılacak

            // Önceden tanımlı veriler
            var predefinedData = new[]
            {
                (0.00, 2688.4988, 4912.5496),
                (0.50, 2686.8541, 4653.6383),
                (1.00, 2698.5712, 4482.9614),
                (1.50, 2691.1024, 4324.9308),
                (2.00, 2698.5963, 4199.9262),
                (2.50, 2702.6907, 4104.1672),
                (3.00, 2691.3517, 3937.3630),
                (3.50, 2690.0622, 3833.1201),
                (4.00, 2692.0327, 3692.6704),
                (4.50, 2700.4150, 3643.4684),
                (5.00, 2690.9841, 3534.8201),
                (5.50, 2690.0577, 3456.5425),
                (6.00, 2695.2707, 3405.8441),
                (6.50, 2693.2138, 3341.9859),
                (7.00, 2697.0889, 3300.5941)
            };

            // Önceden tanımlı verileri ekle
            sampleData.AddRange(predefinedData.Select(data => new DeviceData
            {
                Sample = sampleCounter++.ToString(), // Sample değeri sıra numarası olarak atanır
                GasConcentration = data.Item1,
                Ref = data.Item2,
                Gas = data.Item3
            }));

            // Rastgele verileri ekle
            sampleData.AddRange(Enumerable.Range(0, 20).Select(_ => new DeviceData
            {
                Sample = sampleCounter++.ToString(), // Sample değeri sıra numarası olarak atanır
                GasConcentration = Math.Round(random.NextDouble() * 10, 2), // 0 ile 10 arasında rastgele gaz konsantrasyonu
                Ref = Math.Round(2680 + random.NextDouble() * 40, 4),       // 2680 ile 2720 arasında rastgele Ref değeri
                Gas = Math.Round(3200 + random.NextDouble() * 2000, 4)     // 3200 ile 5200 arasında rastgele Gas değeri
            }));

            return sampleData;
        }
    }
}
