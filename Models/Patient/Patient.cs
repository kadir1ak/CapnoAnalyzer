using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using CapnoAnalyzer.Helpers;

namespace CapnoAnalyzer.Models.Patient
{
    /// <summary>
    /// Hasta veri modeli. TextBox binding hatalarını önlemek için
    /// sayısal değerler (Yas, Boy, Kilo) string olarak tutulur;
    /// cihaz komutunda PatientDeviceCommandBuilder parse eder.
    /// </summary>
    public class Patient : BindableBase, INotifyDataErrorInfo
    {
        private readonly Dictionary<string, List<string>> _errors =
            new(StringComparer.OrdinalIgnoreCase);

        private Guid _id;
        public Guid Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private string _adSoyad = string.Empty;
        public string AdSoyad
        {
            get => _adSoyad;
            set
            {
                if (SetProperty(ref _adSoyad, value))
                    ValidateProperty(value, nameof(AdSoyad));
            }
        }

        private string _tcKimlik = string.Empty;
        public string TcKimlik
        {
            get => _tcKimlik;
            set
            {
                if (SetProperty(ref _tcKimlik, value))
                    ValidateProperty(value, nameof(TcKimlik));
            }
        }

        private string _kayitNo = string.Empty;
        public string KayitNo
        {
            get => _kayitNo;
            set => SetProperty(ref _kayitNo, value);
        }

        private string _ambulansNo = string.Empty;
        public string AmbulansNo
        {
            get => _ambulansNo;
            set => SetProperty(ref _ambulansNo, value);
        }

        // "Erkek" | "Kadın" | "Diğer"
        private string _cinsiyet = "Erkek";
        public string Cinsiyet
        {
            get => _cinsiyet;
            set => SetProperty(ref _cinsiyet, value);
        }

        // String olarak saklanır — binding validation hatası olmaz
        private string _yas = string.Empty;
        public string Yas
        {
            get => _yas;
            set
            {
                var cleaned = KeepNumericCharacters(value);
                if (SetProperty(ref _yas, cleaned))
                    ValidateProperty(cleaned, nameof(Yas));
            }
        }

        private string _boy = string.Empty;
        public string Boy
        {
            get => _boy;
            set
            {
                var cleaned = KeepNumericCharacters(value, allowDecimal: true);
                if (SetProperty(ref _boy, cleaned))
                    ValidateProperty(cleaned, nameof(Boy));
            }
        }

        private string _kilo = string.Empty;
        public string Kilo
        {
            get => _kilo;
            set
            {
                var cleaned = KeepNumericCharacters(value, allowDecimal: true);
                if (SetProperty(ref _kilo, cleaned))
                    ValidateProperty(cleaned, nameof(Kilo));
            }
        }

        private DateTime _kayitTarihi = DateTime.Now;
        public DateTime KayitTarihi
        {
            get => _kayitTarihi;
            set => SetProperty(ref _kayitTarihi, value);
        }

        public Patient()
        {
            Id          = Guid.NewGuid();
            KayitTarihi = DateTime.Now;
        }

        public void CopyFrom(Patient other)
        {
            AdSoyad     = other.AdSoyad;
            TcKimlik    = other.TcKimlik;
            KayitNo     = other.KayitNo;
            AmbulansNo  = other.AmbulansNo;
            Cinsiyet    = other.Cinsiyet;
            Yas         = other.Yas;
            Boy         = other.Boy;
            Kilo        = other.Kilo;
            KayitTarihi = other.KayitTarihi;
        }

        public static Patient Empty() => new Patient();

        // ─── INotifyDataErrorInfo ────────────────────────────────────────────────

        public bool HasErrors => _errors.Values.Any(list => list.Count > 0);

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public IEnumerable GetErrors(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                return Enumerable.Empty<string>();

            return _errors.TryGetValue(propertyName, out var list)
                ? list
                : Enumerable.Empty<string>();
        }

        private void ValidateProperty(object value, string propertyName)
        {
            var errors = new List<string>();

            switch (propertyName)
            {
                case nameof(AdSoyad):
                    if (string.IsNullOrWhiteSpace(AdSoyad))
                        errors.Add("Ad / Soyad boş bırakılamaz.");
                    break;

                case nameof(TcKimlik):
                    var tc = TcKimlik?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(tc))
                        errors.Add("TC Kimlik numarası zorunludur.");
                    else
                    {
                        if (tc.Length != 11)
                            errors.Add("TC Kimlik 11 haneli olmalıdır.");
                        if (!tc.All(char.IsDigit))
                            errors.Add("TC Kimlik sadece rakamlardan oluşmalıdır.");
                    }
                    break;

                case nameof(Yas):
                    if (!string.IsNullOrWhiteSpace(Yas) && !Yas.All(char.IsDigit))
                        errors.Add("Yaş alanı sadece rakam içerebilir.");
                    break;

                case nameof(Boy):
                    if (!IsNumeric(Boy))
                        errors.Add("Boy alanı geçerli bir sayı olmalıdır.");
                    break;

                case nameof(Kilo):
                    if (!IsNumeric(Kilo))
                        errors.Add("Kilo alanı geçerli bir sayı olmalıdır.");
                    break;
            }

            if (errors.Count > 0)
                _errors[propertyName] = errors;
            else
                _errors.Remove(propertyName);

            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        private static string KeepNumericCharacters(string input, bool allowDecimal = false)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var chars = new List<char>();
            bool decimalUsed = false;

            foreach (var c in input)
            {
                if (char.IsDigit(c))
                {
                    chars.Add(c);
                }
                else if (allowDecimal && (c == '.' || c == ',') && !decimalUsed)
                {
                    chars.Add(c);
                    decimalUsed = true;
                }
            }

            return new string(chars.ToArray());
        }

        private static bool IsNumeric(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return true; // boş alanı burada hata saymıyoruz; zorunluluk ayrı kontrol edilir

            var cleaned = KeepNumericCharacters(input, allowDecimal: true);
            return string.Equals(cleaned, input, StringComparison.Ordinal);
        }
    }
}
