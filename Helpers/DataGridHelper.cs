using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CapnoAnalyzer.Helpers
{
    public static class DataGridHelper
    {
        // BeginningEdit olayı için Attached Property
        public static readonly DependencyProperty BeginningEditCommandProperty =
            DependencyProperty.RegisterAttached("BeginningEditCommand", typeof(ICommand), typeof(DataGridHelper), new PropertyMetadata(null, OnBeginningEditCommandChanged));

        public static void SetBeginningEditCommand(DependencyObject element, ICommand value) => element.SetValue(BeginningEditCommandProperty, value);
        public static ICommand GetBeginningEditCommand(DependencyObject element) => (ICommand)element.GetValue(BeginningEditCommandProperty);

        private static void OnBeginningEditCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid dataGrid)
            {
                dataGrid.BeginningEdit -= DataGrid_BeginningEdit;
                if (e.NewValue != null) dataGrid.BeginningEdit += DataGrid_BeginningEdit;
            }
        }

        private static void DataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            var dataGrid = sender as DataGrid;
            var command = GetBeginningEditCommand(dataGrid);

            if (command != null && command.CanExecute(e))
            {
                // Komutu çalıştır (Parametre olarak EventArgs gönderiyoruz ki iptal edebilelim)
                command.Execute(e);
            }
        }
    }
}
