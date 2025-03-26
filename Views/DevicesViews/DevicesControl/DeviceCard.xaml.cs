using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CapnoAnalyzer.Views.DevicesViews.DevicesControl
{
    /// <summary>
    /// DeviceCard.xaml etkileşim mantığı
    /// </summary>
    public partial class DeviceCard : UserControl
    {
        public DeviceCard()
        {
            InitializeComponent();
        }

        private void SampleTime_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // Eğer geçerli bir sayıysa siyah, geçersizse kırmızı renkte göster
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
            // Virgülü noktaya çevir (UI'da farklı formatlar için)
            text = text.Replace(',', '.');

            // Tam sayı parse işlemi
            if (!int.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int number))
            {
                // Parse başarısızsa geçersiz
                return false;
            }

            // Aralık kontrolü (0 ile 1000 arasında olmalı)
            if (number < 0 || number > 1000)
                return false;

            return true;
        }
    }
}
