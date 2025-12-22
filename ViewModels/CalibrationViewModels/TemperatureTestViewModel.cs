using CapnoAnalyzer.Helpers;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace CapnoAnalyzer.ViewModels.CalibrationViewModels
{
    public class TemperatureTestViewModel : BindableBase
    {
        public string Header { get; }
        public ObservableCollection<Data> TestData { get; set; }
        public ReferenceDataViewModel ReferenceTestData { get; set; }

        // Silme Komutu
        public ICommand DeleteDataCommand { get; }

        public TemperatureTestViewModel(string header)
        {
            Header = header;
            TestData = new ObservableCollection<Data>();
            ReferenceTestData = new ReferenceDataViewModel();

            // Silme mantığı
            DeleteDataCommand = new RelayCommand(param => {
                if (param is Data row)
                {
                    var result = MessageBox.Show("Bu test verisini silmek istediğinize emin misiniz?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result == MessageBoxResult.Yes)
                    {
                        TestData.Remove(row);
                    }
                }
            });

            TestData.CollectionChanged += (sender, args) => UpdateAverageEnvironmentalData();
        }

        private void UpdateAverageEnvironmentalData()
        {
            if (TestData.Any())
            {
                ReferenceTestData.Temperature = TestData.Average(d => d.Temperature);
                ReferenceTestData.Pressure = TestData.Average(d => d.Pressure);
                ReferenceTestData.Humidity = TestData.Average(d => d.Humidity);
            }
            else
            {
                ReferenceTestData.Temperature = 0;
                ReferenceTestData.Pressure = 0;
                ReferenceTestData.Humidity = 0;
            }
        }
    }

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
