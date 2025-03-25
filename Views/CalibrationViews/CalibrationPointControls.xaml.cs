using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CapnoAnalyzer.Views.CalibrationViews
{
    /// <summary>
    /// CalibrationPointControls.xaml etkileşim mantığı
    /// </summary>
    public partial class CalibrationPointControls : UserControl
    {
        public CalibrationPointControls()
        {
            InitializeComponent();
        }
        private void AppliedGasConcentration_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // If it's valid, keep normal color; if invalid, color red
                if (IsValidNumber(textBox.Text))
                {
                    textBox.Foreground = new SolidColorBrush(Colors.Black);
                }
                else
                {
                    textBox.Foreground = new SolidColorBrush(Colors.Red);
                }
            }
        }

        private static bool IsValidNumber(string text)
        {
            // Replace any commas with dots so "3,14" becomes "3.14"
            // or if your UI always expects dot, this ensures consistent parsing
            text = text.Replace(',', '.');

            // Now parse with an invariant (English-like) decimal format:
            if (!double.TryParse(
                    text,
                    NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out double number))
            {
                // If parse fails altogether, it's not valid
                return false;
            }

            // Range check
            if (number < 0.0 || number > 100.0)
                return false;

            // Optionally, if you want to ensure at most 2 decimal places,
            // you can do something like:
            //   string[] parts = text.Split('.');
            //   if (parts.Length == 2 && parts[1].Length > 2) return false;

            return true;
        }
    }
}
