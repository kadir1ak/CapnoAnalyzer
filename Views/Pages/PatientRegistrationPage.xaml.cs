using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CapnoAnalyzer.ViewModels.MainViewModels;

namespace CapnoAnalyzer.Views.Pages
{
    /// <summary>
    /// Hasta Kayıt ve Yönetim Sayfası.
    /// DataContext: PatientRegistrationViewModel.
    /// Ana pencere üzerinden MainViewModel.PatientRegistrationVM bağlanır;
    /// farklı bir şekilde oluşturulsa bile burada tekrar güvenceye alınır.
    /// </summary>
    public partial class PatientRegistrationPage : Page
    {
        public PatientRegistrationPage()
        {
            InitializeComponent();

            // Tasarım modunda DataContext atama
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            TryAttachDataContextFromMainWindow();
        }

        private void TryAttachDataContextFromMainWindow()
        {
            // Ana pencerenin DataContext'inden PatientRegistrationVM'i al
            if (Application.Current?.MainWindow?.DataContext is MainViewModel mainVm
                && mainVm.PatientRegistrationVM != null)
            {
                DataContext = mainVm.PatientRegistrationVM;
            }
        }
    }
}
