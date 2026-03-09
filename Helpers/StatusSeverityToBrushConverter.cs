using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using CapnoAnalyzer.ViewModels.PatientViewModels;

namespace CapnoAnalyzer.Helpers
{
    public class StatusSeverityToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not StatusSeverity severity)
                return Brushes.Gray;

            return severity switch
            {
                StatusSeverity.Success => new SolidColorBrush(Color.FromRgb(22, 163, 74)),   // Yeşil
                StatusSeverity.Error   => new SolidColorBrush(Color.FromRgb(220, 38, 38)),   // Kırmızı
                _                      => new SolidColorBrush(Color.FromRgb(37, 99, 235)),   // Mavi
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

