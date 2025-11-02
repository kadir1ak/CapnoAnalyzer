using CapnoAnalyzer.Helpers;
using System.Collections.ObjectModel;

namespace CapnoAnalyzer.ViewModels.CalibrationViewModels
{
    /// <summary>
    /// Tek bir sıcaklık kompanzasyon testini (bir sekme) temsil eden ViewModel.
    /// </summary>
    public class TemperatureTestViewModel : BindableBase
    {
        public string Header { get; }
        public ObservableCollection<Data> TestData { get; set; }
        public ReferenceDataViewModel ReferenceTestData { get; set; }

        public TemperatureTestViewModel(string header)
        {
            Header = header;
            TestData = new ObservableCollection<Data>();
            ReferenceTestData = new ReferenceDataViewModel();

            // TestData koleksiyonuna bir öğe eklendiğinde veya çıkarıldığında
            // ortalama değerleri yeniden hesapla.
            TestData.CollectionChanged += (sender, args) => UpdateAverageEnvironmentalData();
        }

        /// <summary>
        /// TestData listesindeki Sıcaklık, Basınç ve Nem değerlerinin ortalamasını alarak
        /// başlıkta gösterilen ReferenceTestData'yı günceller.
        /// </summary>
        private void UpdateAverageEnvironmentalData()
        {
            if (TestData.Any())
            {
                // LINQ kullanarak her bir özelliğin ortalamasını hesapla
                ReferenceTestData.Temperature = TestData.Average(d => d.Temperature);
                ReferenceTestData.Pressure = TestData.Average(d => d.Pressure);
                ReferenceTestData.Humidity = TestData.Average(d => d.Humidity);
            }
            else
            {
                // Eğer tabloda veri yoksa, değerleri sıfırla
                ReferenceTestData.Temperature = 0;
                ReferenceTestData.Pressure = 0;
                ReferenceTestData.Humidity = 0;
            }
        }
    }

    /// <summary>
    /// Sıcaklık testi başlığında gösterilecek referans verilerini tutan model.
    /// </summary>
    public class ReferenceDataViewModel : BindableBase
    {
        private int _testNo;
        public int TestNo { get => _testNo; set => SetProperty(ref _testNo, value); }

        private double _temperature;
        public double Temperature { get => _temperature; set => SetProperty(ref _temperature, value); }

        private double _pressure;
        public double Pressure { get => _pressure; set => SetProperty(ref _pressure, value); }

        private double _humidity;
        public double Humidity { get => _humidity; set => SetProperty(ref _humidity, value); }

        private double _zero;
        public double Zero { get => _zero; set => SetProperty(ref _zero, value); }

        private double _spanA;
        public double SpanA { get => _spanA; set => SetProperty(ref _spanA, value); }

        private double _b;
        public double B { get => _b; set => SetProperty(ref _b, value); }

        private double _c;
        public double C { get => _c; set => SetProperty(ref _c, value); }

        private double _r;
        public double R { get => _r; set => SetProperty(ref _r, value); }

        private double _alpha;
        public double Alpha { get => _alpha; set => SetProperty(ref _alpha, value); }

        private double _beta;
        public double Beta { get => _beta; set => SetProperty(ref _beta, value); }
    }
}
