using System;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Controls;
using CapnoAnalyzer.ViewModels.MainViewModels;
using CapnoAnalyzer.Views.Pages;

namespace CapnoAnalyzer.Views.MainViews
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            try
            {
                InitializeComponent();
                
                // Frame'in içindeki Page'lere DataContext aktarımı için event ekle
                MainContentArea.Navigated += OnFrameNavigated;

                // İlk sayfa olarak DevicesPage yüklüyoruz
                // DataContext, OnFrameNavigated içinde MainViewModel olarak atanacak.
                MainContentArea.Navigate(new DevicesPage());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Uygulama başlatılırken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Frame içinde navigasyon gerçekleştiğinde sayfalara DataContext aktarımı yapar.
        /// </summary>
        private void OnFrameNavigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is Page page)
            {
                // Sadece DataContext'i henüz atanmadıysa MainViewModel'i ver.
                // PatientRegistrationPage gibi özel ViewModel'lere sahip sayfaların
                // kendi DataContext'i korunur, buton komutları çalışır.
                if (page.DataContext == null)
                {
                    page.DataContext = this.DataContext;
                }
            }
        }

        /// <summary>
        /// MainWindow yüklendiğinde ekran boyutlarını ayarlar.
        /// </summary>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Pencereyi tam ekran yap
                this.WindowState = WindowState.Normal;
                this.WindowStyle = WindowStyle.None;

                // Çalışma alanını (taskbar hariç ekran boyutunu) al
                var screen = System.Windows.SystemParameters.WorkArea;
                this.Left = screen.Left;
                this.Top = screen.Top;
                this.Width = screen.Width;
                this.Height = screen.Height;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Pencere boyutları ayarlanırken bir hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
