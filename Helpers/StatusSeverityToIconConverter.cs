using System;
using System.Globalization;
using System.Windows.Data;
using CapnoAnalyzer.ViewModels.PatientViewModels;
using FontAwesome.WPF;

namespace CapnoAnalyzer.Helpers
{
    public class StatusSeverityToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not StatusSeverity severity)
                return FontAwesomeIcon.InfoCircle;

            return severity switch
            {
                StatusSeverity.Success => FontAwesomeIcon.CheckCircle,
                StatusSeverity.Error   => FontAwesomeIcon.ExclamationCircle,
                _                      => FontAwesomeIcon.InfoCircle
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

