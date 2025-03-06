using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using CapnoAnalyzer.ViewModels.MainViewModels;
using CapnoAnalyzer.Views.DevicesViews.DevicesControl;
using CapnoAnalyzer.Views.Pages;

namespace CapnoAnalyzer.Views.MainViews
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {

            InitializeComponent();

            //this.Loaded += MainWindow_Loaded;

            // Tasarım modunda çalışıyorsak, hiçbir işlem yapmadan çık
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            DataContext = new MainViewModel();

            // İlk sayfa olarak HomePage yüklüyoruz
            MainFrame.Navigate(new HomePage());

            // Frame'in içindeki Page'lere DataContext aktarımı için event ekleyelim
            MainFrame.Navigated += OnFrameNavigated;
        }

        private void OnFrameNavigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is Page page)
            {
                // Sayfanın DataContext'ini MainViewModel olarak ayarla
                page.DataContext = this.DataContext;
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Normal;
            this.WindowStyle = WindowStyle.None;

            // Get screen working area excluding the taskbar
            var screen = System.Windows.SystemParameters.WorkArea;
            this.Left = screen.Left;
            this.Top = screen.Top;
            this.Width = screen.Width;
            this.Height = screen.Height;
        }
    }
}
