using System.Windows.Controls;
using System.Windows.Input;
using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Views.Pages;
using System.Windows;
using CapnoAnalyzer.Views.MainViews;
using CapnoAnalyzer.ViewModels.DeviceViewModels;
using CapnoAnalyzer.Views.DevicesViews.DevicesControl;

namespace CapnoAnalyzer.ViewModels.MainViewModels
{
    class MainViewModel : BindableBase
    {
        public DevicesViewModel DevicesVM { get; set; }
        public ICommand NavigateCommand { get; }

        public MainViewModel()
        {
            DevicesVM = new DevicesViewModel();
            NavigateCommand = new RelayCommand(Navigate);
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
                        // 1️⃣ Önce tüm serial portları kapat
                        DevicesVM?.CloseAllPorts();

                        // 2️⃣ Ardından programı tamamen kapat
                        Application.Current.Shutdown();
                    }
                    return; // Kullanıcı "Hayır" dediyse devam etme
                }

                if (mainWindow?.MainFrame.Content is Page currentPage && currentPage.GetType().Name == $"{pageName}Page")
                {
                    // Eğer zaten aynı sayfa açıksa, tekrar açma
                    return;
                }

                // Sayfa oluşturma
                Page targetPage = pageName switch
                {
                    "Home" => new HomePage(),
                    "Devices" => new DevicesPage(),
                    "Settings" => new SettingsPage(),
                    "About" => new AboutPage(),
                    _ => new HomePage(),
                };

                // Frame'e yönlendirme işlemi
                mainWindow?.MainFrame.Navigate(targetPage);
            }
        }
    }
}
