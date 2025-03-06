using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using CapnoAnalyzer.Models.Device;
using CapnoAnalyzer.ViewModels.DeviceViewModels;

namespace CapnoAnalyzer.Helpers
{
    public class PortConnectedToBrushMultiConverter : IMultiValueConverter
    {
        // Fırça renklerini tanımlayın
        public Brush ConnectedBrush { get; set; } = Brushes.Green;
        public Brush DisconnectedBrush { get; set; } = Brushes.Gray;
        public Brush IdentifiedDeviceBrush { get; set; } = Brushes.Blue;
        public Brush UnidentifiedDeviceBrush { get; set; } = Brushes.Orange;

        // values[0]: port adı (string)
        // values[1]: DevicesViewModel (DataContext)
        // values[2]: Tetik amaçlı (örneğin, ConnectedPorts.Count)
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Giriş parametrelerini kontrol et
            if (values == null || values.Length < 3)
                return DisconnectedBrush;

            string portName = values[0] as string;
            var vm = values[1] as DevicesViewModel;

            // Eğer port adı veya ViewModel geçersizse, bağlantı kesilmiş fırçayı döndür
            if (string.IsNullOrEmpty(portName) || vm == null)
                return DisconnectedBrush;

            // Cihaz durumunu kontrol et
            var deviceStatus = vm.IsDeviceStatus(portName);
            if (deviceStatus == null)
                return DisconnectedBrush;

            // Duruma göre uygun fırçayı döndür
            if (deviceStatus == DeviceStatus.Connected)
                return ConnectedBrush;
            else if (deviceStatus == DeviceStatus.Identified)
                return IdentifiedDeviceBrush;
            else if (deviceStatus == DeviceStatus.Unidentified)
                return UnidentifiedDeviceBrush;
            else
                return DisconnectedBrush;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // ConvertBack işlemi desteklenmiyor
            throw new NotImplementedException();
        }
    }
}
