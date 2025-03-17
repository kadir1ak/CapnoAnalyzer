using CapnoAnalyzer.Helpers;
using System.Collections.ObjectModel;

namespace CapnoAnalyzer.ViewModels.CalibrationViewModels
{
    public class CalibrationViewModel : BindableBase
    {
        public ObservableCollection<GasConcentrationTablesViewModel> DeviceTables { get; set; }

        public CalibrationViewModel()
        {
            DeviceTables = new ObservableCollection<GasConcentrationTablesViewModel>();
        }

        public void AddDeviceTable(string deviceName)
        {
            var table = new GasConcentrationTablesViewModel(deviceName);
            DeviceTables.Add(table);

            // Örnek veri ekleme
            table.AddDeviceData(new DeviceData { Sample = "1", GasConcentration = 0.5, Ref = 2700, Gas = 4500 });
        }
    }
}
