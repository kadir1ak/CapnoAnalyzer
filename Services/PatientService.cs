using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CapnoAnalyzer.Models.Patient;
using ClosedXML.Excel;

namespace CapnoAnalyzer.Services
{
    /// <summary>
    /// Hasta verilerini JSON dosyasında saklar ve Excel import/export işlemlerini yönetir.
    /// </summary>
    public class PatientService : IPatientService
    {
        private static readonly string DataFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "CapnoAnalyzer", "Patients");

        private static readonly string DataFilePath =
            Path.Combine(DataFolder, "patients.json");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public PatientService()
        {
            Directory.CreateDirectory(DataFolder);
        }

        // ─── CRUD ────────────────────────────────────────────────────────────────

        /// <summary>Tüm hastaları JSON dosyasından okur.</summary>
        public List<Patient> GetAllPatients()
        {
            if (!File.Exists(DataFilePath))
                return new List<Patient>();

            try
            {
                string json = File.ReadAllText(DataFilePath);
                return JsonSerializer.Deserialize<List<Patient>>(json, JsonOptions)
                       ?? new List<Patient>();
            }
            catch
            {
                return new List<Patient>();
            }
        }

        /// <summary>Hasta ekler veya Id eşleşirse günceller.</summary>
        public void SavePatient(Patient patient)
        {
            var list = GetAllPatients();
            var existing = list.FirstOrDefault(p => p.Id == patient.Id);

            if (existing != null)
            {
                existing.CopyFrom(patient);
            }
            else
            {
                patient.KayitTarihi = DateTime.Now;
                list.Add(patient);
            }

            WriteAll(list);
        }

        /// <summary>Belirtilen Id'ye sahip hastayı siler.</summary>
        public bool DeletePatient(Guid id)
        {
            var list = GetAllPatients();
            var toRemove = list.FirstOrDefault(p => p.Id == id);
            if (toRemove == null) return false;

            list.Remove(toRemove);
            WriteAll(list);
            return true;
        }

        private void WriteAll(List<Patient> patients)
        {
            string json = JsonSerializer.Serialize(patients, JsonOptions);

            // Güvenli yazma: önce temp dosyaya yaz, sonra ana dosyayı atomik olarak değiştir.
            string tempPath = DataFilePath + ".tmp";

            File.WriteAllText(tempPath, json);

            // Orijinal dosya bozulmadan üzerine yaz.
            File.Copy(tempPath, DataFilePath, overwrite: true);
            File.Delete(tempPath);
        }

        // ─── Excel Export (ClosedXML) ────────────────────────────────────────────

        /// <summary>
        /// Hasta listesini Excel dosyasına yazar (Office bağımsız).
        /// </summary>
        public void ExportToExcel(List<Patient> patients, string filePath)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var sheet = workbook.Worksheets.Add("Hasta Listesi");

                string[] headers =
                {
                    "Ad Soyad", "TC Kimlik", "Kayıt No", "Ambulans No",
                    "Cinsiyet", "Yaş", "Boy (cm)", "Kilo (kg)", "Kayıt Tarihi"
                };

                // Başlık satırı
                for (int c = 0; c < headers.Length; c++)
                {
                    var cell = sheet.Cell(1, c + 1);
                    cell.Value = headers[c];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromArgb(46, 59, 78);
                    cell.Style.Font.FontColor = XLColor.White;
                }

                // Veri satırları
                for (int r = 0; r < patients.Count; r++)
                {
                    var p = patients[r];
                    int row = r + 2;

                    sheet.Cell(row, 1).Value = p.AdSoyad;
                    sheet.Cell(row, 2).Value = p.TcKimlik;
                    sheet.Cell(row, 3).Value = p.KayitNo;
                    sheet.Cell(row, 4).Value = p.AmbulansNo;
                    sheet.Cell(row, 5).Value = p.Cinsiyet;
                    sheet.Cell(row, 6).Value = p.Yas;
                    sheet.Cell(row, 7).Value = p.Boy;
                    sheet.Cell(row, 8).Value = p.Kilo;
                    sheet.Cell(row, 9).Value = p.KayitTarihi.ToString("yyyy-MM-dd HH:mm");

                    // Zebra renklendirme
                    if (r % 2 == 1)
                    {
                        var dataRange = sheet.Range(row, 1, row, headers.Length);
                        dataRange.Style.Fill.BackgroundColor = XLColor.FromArgb(240, 244, 249);
                    }
                }

                sheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
            catch (IOException ex)
            {
                throw new IOException(
                    "Excel dosyası başka bir uygulama tarafından kullanılıyor olabilir. " +
                    "Lütfen dosyayı kapatıp tekrar deneyin.",
                    ex);
            }
        }

        // ─── Excel Import (ClosedXML) ────────────────────────────────────────────

        /// <summary>
        /// ExportToExcel ile oluşturulan Excel dosyasından hasta listesini okur.
        /// </summary>
        public List<Patient> ImportFromExcel(string filePath)
        {
            var result = new List<Patient>();

            try
            {
                using var workbook = new XLWorkbook(filePath);
                var sheet = workbook.Worksheet(1);

                var usedRange = sheet.RangeUsed();
                if (usedRange == null)
                    return result;

                // 2. satırdan itibaren oku (1. satır başlık)
                foreach (var row in usedRange.RowsUsed().Skip(1))
                {
                    string adSoyad = row.Cell(1).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(adSoyad)) continue;

                    var patient = new Patient
                    {
                        AdSoyad     = adSoyad,
                        TcKimlik    = row.Cell(2).GetString().Trim(),
                        KayitNo     = row.Cell(3).GetString().Trim(),
                        AmbulansNo  = row.Cell(4).GetString().Trim(),
                        Cinsiyet    = row.Cell(5).GetString().Trim(),
                        Yas         = row.Cell(6).GetString().Trim(),
                        Boy         = row.Cell(7).GetString().Trim(),
                        Kilo        = row.Cell(8).GetString().Trim(),
                        KayitTarihi = row.Cell(9).TryGetValue<DateTime>(out var dt)
                                      ? dt
                                      : DateTime.Now
                    };

                    result.Add(patient);
                }
            }
            catch (IOException ex)
            {
                throw new IOException(
                    "Excel dosyasına erişilemedi. Dosya başka bir uygulama tarafından kullanılıyor olabilir.",
                    ex);
            }
            return result;
        }
    }
}
