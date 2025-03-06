using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using CapnoAnalyzer.ViewModels.MainViewModels;
using CapnoAnalyzer.Views.Pages;

namespace CapnoAnalyzer.Views.MainViews
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {

            InitializeComponent();

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
    }
}
