using System;
using System.Globalization;
using CapnoAnalyzer.Models.Patient;

namespace CapnoAnalyzer.Services
{
    /// <summary>
    /// Hasta bilgilerini cihazın anlayacağı "PV,..." formatında komuta çevirir.
    /// Format: PV,AdSoyad,TcKimlik,KayitNo,AmbulansNo,Cinsiyet,Yas,Boy,Kilo
    /// Boy ve Kilo string olarak gelir; cihaz için InvariantCulture ile "." ondalık gönderilir.
    /// </summary>
    public static class PatientDeviceCommandBuilder
    {
        public static string Build(Patient patient)
        {
            if (patient == null)
                throw new ArgumentNullException(nameof(patient));

            // Boy ve Kilo string → double parse (hata varsa 0.0 kullan)
            double boy  = ParseDouble(patient.Boy);
            double kilo = ParseDouble(patient.Kilo);
            int    yas  = ParseInt(patient.Yas);

            string cmd = string.Format(
                CultureInfo.InvariantCulture,
                "PV,{0},{1},{2},{3},{4},{5},{6:F1},{7:F1}",
                patient.AdSoyad?.Trim()    ?? string.Empty,
                patient.TcKimlik?.Trim()   ?? string.Empty,
                patient.KayitNo?.Trim()    ?? string.Empty,
                patient.AmbulansNo?.Trim() ?? string.Empty,
                patient.Cinsiyet?.Trim()   ?? string.Empty,
                yas,
                boy,
                kilo);

            return cmd;
        }

        private static double ParseDouble(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0.0;
            return double.TryParse(s.Replace(',', '.'),
                NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0.0;
        }

        private static int ParseInt(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            return int.TryParse(s, out int v) ? v : 0;
        }
    }
}
