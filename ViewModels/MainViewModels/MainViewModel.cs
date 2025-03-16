using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Views.Pages;
using System.Windows;
using CapnoAnalyzer.Views.MainViews;
using CapnoAnalyzer.ViewModels.DeviceViewModels;
using CapnoAnalyzer.Views.DevicesViews.DevicesControl;
using CapnoAnalyzer.ViewModels.SettingViewModels;
using CapnoAnalyzer.Models.Settings;
using CapnoAnalyzer.ViewModels.CalibrationViewModels;
using CapnoAnalyzer.Views.DevicesViews.Devices;

namespace CapnoAnalyzer.ViewModels.MainViewModels
{
    class MainViewModel : BindableBase
    {
        public DevicesViewModel DevicesVM { get; set; }
        public SettingViewModel SettingVM { get; set; }
        public ICommand NavigateCommand { get; }

        // **Sayfaları yeniden oluşturmamak için bir Dictionary kullanıyoruz**
        private readonly Dictionary<string, Page> _pageCache = new Dictionary<string, Page>();

        public MainViewModel()
        {
            DevicesVM = new DevicesViewModel();          
            NavigateCommand = new RelayCommand(Navigate);
            SettingVM = new SettingViewModel();

            // Ayar değiştiğinde cihazlara yansıtmak için abone olun:
            SettingVM.CurrentSetting.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Setting.BaudRate))
                {
                    UpdateDevicesBaudRate();
                }
                else if (e.PropertyName == nameof(Setting.PlotTime))
                {
                    UpdateDevicesPlotTime();
                }
            };
        }
        private void UpdateDevicesBaudRate()
        {
            DevicesVM.SelectedBaudRate = SettingVM.CurrentSetting.BaudRate;
        }

        private void UpdateDevicesPlotTime()
        {
            foreach (var device in DevicesVM.IdentifiedDevices)
            {
                device.Interface.SensorPlot.PlotTime = SettingVM.CurrentSetting.PlotTime;
                device.Interface.UpdatePlot(); 
            }
        }
        private void Navigate(object parameter)
        {
            if (parameter is string pageName)
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;

                if (pageName == "Close")
                {
                    // Kullanıcıya çıkış onayı sor
                    MessageBoxResult result = MessageBox.Show(
                        "Uygulamadan çıkmak istediğinize emin misiniz?",
                        "Çıkış Onayı",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );

                    if (result == MessageBoxResult.Yes)
                    {
                        CloseApplication(); // Bağımsız kapatma metodunu çağır
                    }
                    return;
                }

                // **Önbellekten sayfayı getir, eğer yoksa oluştur**
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
                        _ => new DevicesPage(),
                    };

                    _pageCache[pageName] = targetPage;
                }

                // Eğer zaten aynı sayfa açıksa tekrar yönlendirme yapma
                if (mainWindow?.MainFrame.Content == _pageCache[pageName])
                {
                    return;
                }

                // Frame'e yönlendirme işlemi (önbellekteki sayfayı kullanarak)
                mainWindow?.MainFrame.Navigate(_pageCache[pageName]);
            }
        }
        public async void CloseApplication()
        {
            try
            {
                // 1️⃣ **Tüm seri port bağlantılarını güvenli şekilde kapat**
                if (DevicesVM != null)
                {
                    await Task.Run(() => DevicesVM.CloseAllPorts());
                }

                // 2️⃣ **Tüm arka plan işlemlerini iptal et**
                DevicesVM?.StopConnectedDevicesUpdateInterfaceDataLoop();
                DevicesVM?.StopIdentifiedDevicesUpdateInterfaceDataLoop();

                // 3️⃣ **Önbellekteki tüm sayfaları temizle (Bellek sızıntısını önlemek için)**
                _pageCache.Clear();

                // 4️⃣ **Ana pencereyi kapat ve tüm kaynakları temizle**
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = Application.Current.MainWindow;
                    mainWindow?.Close();
                });

                // 5️⃣ **Arka planda kapanmayı bekle ve zorla kapat**
                await Task.Delay(500);
                App.Current.Shutdown();

                // 6️⃣ **Uygulamanın tamamen kapandığından emin olmak için**
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kapatma işlemi sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
