using System.Windows.Controls;
using System.Windows.Input;
using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Views.Pages;
using System.Windows;
using CapnoAnalyzer.Views.MainViews;

namespace CapnoAnalyzer.ViewModels.MainViewModels
{
    class MainViewModel : BindableBase
    {
        public ICommand NavigateCommand { get; }

        public MainViewModel()
        {
            NavigateCommand = new RelayCommand(Navigate);
        }

        private void Navigate(object parameter)
        {
            if (parameter is string pageName)
            {
                Page targetPage = pageName switch
                {
                    "Home" => new HomePage(),
                    "Settings" => new SettingsPage(),
                    "About" => new AboutPage(),
                    _ => new HomePage(),
                };

                // Frame'e yönlendirme işlemi
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.MainFrame.Navigate(targetPage);
            }
        }
    }
}
