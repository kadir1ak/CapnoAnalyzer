using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Models.Device;
using CapnoAnalyzer.ViewModels.DeviceViewModels;
using System.Collections.ObjectModel;
using System.Windows;

namespace CapnoAnalyzer.ViewModels.CalibrationViewModels
{
    public class CalibrationViewModel : BindableBase
    {
        public DevicesViewModel Devices { get; private set; }
        public ObservableCollection<GasConcentrationTablesViewModel> DeviceTables { get; set; }

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

            // DevicesViewModel'deki olayları dinliyoruz
            Devices.DeviceAdded += OnDeviceAdded;
            Devices.DeviceRemoved += OnDeviceRemoved;

            // Mevcut cihazları ekle
            foreach (var device in Devices.IdentifiedDevices)
            {
                AddDeviceTable(device.Properties.ProductId);
            }
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
                return; // Zaten tablo varsa ekleme
            }

            var table = new GasConcentrationTablesViewModel(deviceName);
            DeviceTables.Add(table);

            // Örnek veri ekleme (isteğe bağlı)
            table.AddDeviceData(new DeviceData { Sample = "1", GasConcentration = 0.5, Ref = 2700, Gas = 4500 });
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
