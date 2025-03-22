using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Views.Pages;
using System.Windows;
using System.Windows.Navigation;
using System.ComponentModel;
using CapnoAnalyzer.Views.MainViews;
using CapnoAnalyzer.ViewModels.DeviceViewModels;
using CapnoAnalyzer.Views.DevicesViews.DevicesControl;
using CapnoAnalyzer.ViewModels.SettingViewModels;
using CapnoAnalyzer.Models.Settings;
using CapnoAnalyzer.ViewModels.CalibrationViewModels;
using CapnoAnalyzer.Views.DevicesViews.Devices;
using System.Threading.Tasks;

namespace CapnoAnalyzer.ViewModels.MainViewModels
{
    class MainViewModel : BindableBase
    {
        public DevicesViewModel DevicesVM { get; private set; }
        public SettingViewModel SettingVM { get; private set; }
        public ICommand NavigateCommand { get; }

        // Sayfa önbelleği
        private readonly Dictionary<string, Page> _pageCache = new Dictionary<string, Page>();

        public MainViewModel()
        {
            // Tasarım modunda çalışıyorsak, veri bağlama için sahte veriler kullan
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                InitializeDesignTimeData();
                return;
            }

            // Çalışma zamanı için ViewModel'leri başlat
            DevicesVM = new DevicesViewModel();
            SettingVM = new SettingViewModel();
            NavigateCommand = new RelayCommand(Navigate);

            // Ayar değişikliklerine abone olunması
            SettingVM.CurrentSetting.PropertyChanged += OnSettingChanged;
        }

        /// <summary>
        /// Tasarım modunda çalışırken kullanılacak sahte verileri başlatır.
        /// </summary>
        private void InitializeDesignTimeData()
        {
            DevicesVM = new DevicesViewModel { };

            SettingVM = new SettingViewModel
            {
                CurrentSetting = new Setting
                {
                    PlotTime = 10,
                    SampleTime = 5
                }
            };
        }

        private void OnSettingChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Setting.PlotTime):
                    UpdateDevicesPlotTime();
                    break;
                case nameof(Setting.SampleTime):
                    UpdateDevicesSampleTime();
                    break;
            }
        }

        private void UpdateDevicesSampleTime()
        {
            // Cihazlara örnekleme süresini güncelle
            foreach (var device in DevicesVM.IdentifiedDevices)
            {
               //device.Interface.SensorSample.SampleTime = SettingVM.CurrentSetting.SampleTime;
            }
        }

        private void UpdateDevicesPlotTime()
        {
            // Cihazlara çizim süresini güncelle
            foreach (var device in DevicesVM.IdentifiedDevices)
            {
                device.Interface.SensorPlot.PlotTime = SettingVM.CurrentSetting.PlotTime;
                device.Interface.UpdatePlot();
            }
        }

        private void Navigate(object parameter)
        {
            if (parameter is not string pageName) return;

            var mainWindow = Application.Current.MainWindow as MainWindow;

            if (pageName == "Close")
            {
                PromptCloseApplication();
                return;
            }

            // Önbellekten sayfa getir veya oluştur
            var targetPage = GetOrCreatePage(pageName);

            // Eğer aynı sayfa zaten açıksa yönlendirme yapma
            if (mainWindow?.MainContentArea.Content == targetPage) return;

            // Frame'e yönlendirme işlemi
            mainWindow?.MainContentArea.Navigate(targetPage);
        }

        private Page GetOrCreatePage(string pageName)
        {
            // Null veya boş kontrolü
            if (string.IsNullOrWhiteSpace(pageName))
            {
                throw new ArgumentException("pageName null, boş veya yalnızca boşluk içeremez.", nameof(pageName));
            }

            // Eğer önbellekte yoksa yeni bir sayfa oluştur ve önbelleğe ekle
            if (!_pageCache.ContainsKey(pageName))
            {
                Page targetPage = pageName switch
                {
                    "Devices" => new DevicesPage { DataContext = DevicesVM },
                    "CalibrationTables" => new CalibrationTablesPage(),
                    "Equation" => new EquationTestPage(),
                    "DeviceConnections" => new DeviceConnectionsPage { DataContext = DevicesVM },
                    "Settings" => new SettingsPage(),
                    "Notes" => new NotesPage(),
                    _ => throw new ArgumentException($"Geçersiz sayfa adı: {pageName}", nameof(pageName)) // Hatalı sayfa adı için
                };

                // Yeni sayfayı önbelleğe ekle
                _pageCache[pageName] = targetPage;
            }

            // Önbellekten sayfayı döndür
            return _pageCache[pageName];
        }

        private void PromptCloseApplication()
        {
            // Kullanıcıya çıkış onayı sor
            var result = MessageBox.Show(
                "Uygulamadan çıkmak istediğinize emin misiniz?",
                "Çıkış Onayı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                CloseApplication();
            }
        }

        public async void CloseApplication()
        {
            try
            {
                // Seri port bağlantılarını kapat
                if (DevicesVM != null)
                {
                    await Task.Run(() => DevicesVM.CloseAllPorts());
                }

                // Arka plan işlemlerini iptal et
                DevicesVM?.StopConnectedDevicesUpdateInterfaceDataLoop();
                DevicesVM?.StopIdentifiedDevicesUpdateInterfaceDataLoop();

                // Önbellekteki sayfaları temizle
                _pageCache.Clear();

                // Ana pencereyi kapat
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Application.Current.MainWindow?.Close();
                });

                // Uygulamayı tamamen kapat
                App.Current.Shutdown();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kapatma işlemi sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
