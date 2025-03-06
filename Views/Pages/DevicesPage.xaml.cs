using System.Windows.Controls;
using CapnoAnalyzer.ViewModels.MainViewModels;

namespace CapnoAnalyzer.Views.Pages
{
    public partial class DevicesPage : Page
    {
        public DevicesPage()
        {
            InitializeComponent();

            // MainViewModel'in içindeki DevicesVM'yi al
            if (App.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
            {
                this.DataContext = mainViewModel.DevicesVM;
            }
        }
    }
}
