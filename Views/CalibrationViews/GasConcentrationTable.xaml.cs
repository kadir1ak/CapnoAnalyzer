using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CapnoAnalyzer.Views.CalibrationViews
{
    /// <summary>
    /// GasConcentrationTable.xaml etkileşim mantığı
    /// </summary>
    public partial class GasConcentrationTable : UserControl
    {
        public GasConcentrationTable()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            // Örnek veri listesi oluşturuluyor
            var deviceData = new List<DeviceData>
            {
                new DeviceData { Sample = "1", GasConcentration = 0.00, Ref = 2721.1243, Gas = 4951.2079, Ratio = 1.819545, Transmittance = 1.000000 },
                new DeviceData { Sample = "2", GasConcentration = 0.50, Ref = 2735.1913, Gas = 4721.7909, Ratio = 1.726311, Transmittance = 0.948760 },
                new DeviceData { Sample = "3", GasConcentration = 1.00, Ref = 2728.3126, Gas = 4532.7526, Ratio = 1.661376, Transmittance = 0.913072 },
                new DeviceData { Sample = "4", GasConcentration = 1.50, Ref = 2729.3862, Gas = 4371.1245, Ratio = 1.601505, Transmittance = 0.880168 },
                new DeviceData { Sample = "5", GasConcentration = 2.00, Ref = 2736.2397, Gas = 4251.3034, Ratio = 1.553703, Transmittance = 0.853896 },
                new DeviceData { Sample = "6", GasConcentration = 2.50, Ref = 2733.7811, Gas = 4134.0959, Ratio = 1.512226, Transmittance = 0.831101 },
                new DeviceData { Sample = "7", GasConcentration = 3.00, Ref = 2738.8093, Gas = 4038.1686, Ratio = 1.474425, Transmittance = 0.810326 },
                new DeviceData { Sample = "8", GasConcentration = 3.50, Ref = 2738.0730, Gas = 3923.0220, Ratio = 1.432767, Transmittance = 0.787432 },
                new DeviceData { Sample = "9", GasConcentration = 4.00, Ref = 2747.3742, Gas = 3824.6099, Ratio = 1.392096, Transmittance = 0.765079 },
                new DeviceData { Sample = "10", GasConcentration = 4.50, Ref = 2734.0905, Gas = 3735.8008, Ratio = 1.366378, Transmittance = 0.750945 },
                new DeviceData { Sample = "11", GasConcentration = 5.00, Ref = 2737.8267, Gas = 3650.4465, Ratio = 1.333337, Transmittance = 0.732786 },
                new DeviceData { Sample = "12", GasConcentration = 5.50, Ref = 2733.2578, Gas = 3572.3155, Ratio = 1.306981, Transmittance = 0.718301 },
                new DeviceData { Sample = "13", GasConcentration = 6.00, Ref = 2734.4179, Gas = 3504.6287, Ratio = 1.281673, Transmittance = 0.704392 },
                new DeviceData { Sample = "14", GasConcentration = 6.50, Ref = 2732.1453, Gas = 3432.8748, Ratio = 1.256476, Transmittance = 0.690544 },
                new DeviceData { Sample = "15", GasConcentration = 7.00, Ref = 2729.8178, Gas = 3363.6873, Ratio = 1.232202, Transmittance = 0.677203 },
            };

            // DataGrid'e veri bağlama
            DataGridDeviceData.ItemsSource = deviceData;
        }

        // Veri model sınıfı
        public class DeviceData
        {
            public string Sample { get; set; }
            public double GasConcentration { get; set; }
            public double Ref { get; set; }
            public double Gas { get; set; }
            public double Ratio { get; set; }
            public double Transmittance { get; set; }
        }
    }
}
