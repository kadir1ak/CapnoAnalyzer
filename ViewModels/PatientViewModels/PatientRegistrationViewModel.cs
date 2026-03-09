using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Models.Device;
using CapnoAnalyzer.Models.Patient;
using CapnoAnalyzer.Services;
using CapnoAnalyzer.ViewModels.DeviceViewModels;

namespace CapnoAnalyzer.ViewModels.PatientViewModels
{
    public class PatientRegistrationViewModel : BindableBase
    {
        private readonly IPatientService _patientService;
        private readonly DevicesViewModel _devicesVM;
        private readonly IDeviceCommunicator _deviceCommunicator;

        private CancellationTokenSource _searchCts;

        // ─── Cinsiyet seçenekleri (ComboBox kaynağı) ─────────────────────────────
        public IReadOnlyList<string> CinsiyetOptions { get; } =
            new List<string> { "Erkek", "Kadın", "Diğer" };

        // ─── Hasta Listesi ────────────────────────────────────────────────────────
        private ObservableCollection<Patient> _patients = new();
        public ObservableCollection<Patient> Patients
        {
            get => _patients;
            set => SetProperty(ref _patients, value);
        }

        // ─── Seçili Hasta (DataGrid seçimi) ──────────────────────────────────────
        private Patient _selectedPatient;
        public Patient SelectedPatient
        {
            get => _selectedPatient;
            set
            {
                if (SetProperty(ref _selectedPatient, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        // ─── Form Verileri (CurrentPatient) ──────────────────────────────────────
        private Patient _currentPatient = new();
        public Patient CurrentPatient
        {
            get => _currentPatient;
            set
            {
                if (SetProperty(ref _currentPatient, value))
                {
                    OnPropertyChanged(nameof(HasValidationErrors));

                    if (_currentPatient != null)
                    {
                        _currentPatient.ErrorsChanged -= CurrentPatientOnErrorsChanged;
                        _currentPatient.ErrorsChanged += CurrentPatientOnErrorsChanged;
                    }

                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool HasValidationErrors => CurrentPatient?.HasErrors == true;

        // ─── Arama ───────────────────────────────────────────────────────────────
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    DebouncedFilterPatients();
            }
        }

        private ObservableCollection<Patient> _filteredPatients = new();
        public ObservableCollection<Patient> FilteredPatients
        {
            get => _filteredPatients;
            set => SetProperty(ref _filteredPatients, value);
        }

        // ─── Cihaz Seçimi ─────────────────────────────────────────────────────────
        public ObservableCollection<Device> AvailableDevices
            => _devicesVM?.IdentifiedDevices ?? new ObservableCollection<Device>();

        private Device _selectedDevice;
        public Device SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (SetProperty(ref _selectedDevice, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        // ─── Durum Mesajı ─────────────────────────────────────────────────────────
        private string _statusMessage = "Hazır.";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private StatusSeverity _statusSeverity = StatusSeverity.Info;
        public StatusSeverity StatusSeverity
        {
            get => _statusSeverity;
            set => SetProperty(ref _statusSeverity, value);
        }

        // ─── Komutlar ─────────────────────────────────────────────────────────────
        public ICommand AddPatientCommand      { get; }
        public ICommand UpdatePatientCommand   { get; }
        public ICommand DeletePatientCommand   { get; }
        public ICommand LoadToFormCommand      { get; }
        public ICommand ClearFormCommand       { get; }
        public ICommand SendToDeviceCommand    { get; }
        public ICommand ExportExcelCommand     { get; }
        public ICommand ImportExcelCommand     { get; }

        // ─── Constructor ──────────────────────────────────────────────────────────
        public PatientRegistrationViewModel(
            DevicesViewModel devicesVM,
            IPatientService patientService,
            IDeviceCommunicator deviceCommunicator)
        {
            _devicesVM          = devicesVM ?? throw new ArgumentNullException(nameof(devicesVM));
            _patientService     = patientService ?? throw new ArgumentNullException(nameof(patientService));
            _deviceCommunicator = deviceCommunicator ?? throw new ArgumentNullException(nameof(deviceCommunicator));

            // Komutlar — cihaza gönderme hariç tüm CRUD komutları cihaz bağımsız çalışır
            AddPatientCommand    = new RelayCommand(ExecuteAddPatient,    CanExecuteAddPatient);
            UpdatePatientCommand = new RelayCommand(ExecuteUpdatePatient, () => SelectedPatient != null);
            DeletePatientCommand = new RelayCommand(ExecuteDeletePatient, () => SelectedPatient != null);
            LoadToFormCommand    = new RelayCommand(ExecuteLoadToForm,    () => SelectedPatient != null);
            ClearFormCommand     = new RelayCommand(ExecuteClearForm);
            SendToDeviceCommand  = new RelayCommand(ExecuteSendToDevice,  CanExecuteSendToDevice);
            ExportExcelCommand   = new RelayCommand(ExecuteExportExcel,   () => Patients.Count > 0);
            ImportExcelCommand   = new RelayCommand(ExecuteImportExcel);

            LoadPatients();

            // Cihaz listesi değişince ComboBox'ı güncelle
            if (_devicesVM != null)
            {
                _devicesVM.IdentifiedDevices.CollectionChanged += (s, e) =>
                    OnPropertyChanged(nameof(AvailableDevices));
            }
        }

        // ─── Hasta Yükleme ────────────────────────────────────────────────────────
        private void LoadPatients()
        {
            try
            {
                var list = _patientService.GetAllPatients();
                Patients = new ObservableCollection<Patient>(list);
                FilterPatients();
                SetStatus($"Toplam {Patients.Count} hasta yüklendi.", StatusSeverity.Info);
            }
            catch (Exception ex)
            {
                SetStatus($"Yükleme hatası: {ex.Message}", StatusSeverity.Error);
            }
        }

        private void FilterPatients()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredPatients = new ObservableCollection<Patient>(Patients);
            }
            else
            {
                string q = SearchText.ToLower();
                var result = Patients.Where(p =>
                    (p.AdSoyad?    .ToLower().Contains(q) == true) ||
                    (p.TcKimlik?   .ToLower().Contains(q) == true) ||
                    (p.KayitNo?    .ToLower().Contains(q) == true) ||
                    (p.AmbulansNo? .ToLower().Contains(q) == true));
                FilteredPatients = new ObservableCollection<Patient>(result);
            }
        }

        private async void DebouncedFilterPatients()
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            try
            {
                await Task.Delay(200, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (token.IsCancellationRequested) return;
            FilterPatients();
        }

        // ─── Hasta Ekle ──────────────────────────────────────────────────────────
        private void ExecuteAddPatient()
        {
            try
            {
                var newPatient = new Patient();
                newPatient.CopyFrom(CurrentPatient);
                newPatient.Id          = Guid.NewGuid();
                newPatient.KayitTarihi = DateTime.Now;

                _patientService.SavePatient(newPatient);
                Patients.Add(newPatient);
                FilterPatients();

                SetStatus($"Kaydedildi: {newPatient.AdSoyad}", StatusSeverity.Success);
                ExecuteClearForm();
            }
            catch (Exception ex)
            {
                SetStatus($"Hata: {ex.Message}", StatusSeverity.Error);
                MessageBox.Show($"Hasta kaydedilemedi:\n{ex.Message}", "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteAddPatient()
            => CurrentPatient != null
               && !CurrentPatient.HasErrors
               && !string.IsNullOrWhiteSpace(CurrentPatient.AdSoyad)
               && !string.IsNullOrWhiteSpace(CurrentPatient.TcKimlik)
               && CurrentPatient.TcKimlik.Trim().Length == 11;

        // ─── Hasta Güncelle ──────────────────────────────────────────────────────
        private void ExecuteUpdatePatient()
        {
            if (SelectedPatient == null) return;
            try
            {
                SelectedPatient.CopyFrom(CurrentPatient);
                SelectedPatient.KayitTarihi = DateTime.Now;
                _patientService.SavePatient(SelectedPatient);

                FilterPatients();
                SetStatus($"Güncellendi: {SelectedPatient.AdSoyad}", StatusSeverity.Success);
            }
            catch (Exception ex)
            {
                SetStatus($"Güncelleme hatası: {ex.Message}", StatusSeverity.Error);
                MessageBox.Show($"Hasta güncellenemedi:\n{ex.Message}", "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Hasta Sil ───────────────────────────────────────────────────────────
        private void ExecuteDeletePatient()
        {
            if (SelectedPatient == null) return;

            var res = MessageBox.Show(
                $"'{SelectedPatient.AdSoyad}' silinecek. Emin misiniz?",
                "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;

            try
            {
                _patientService.DeletePatient(SelectedPatient.Id);
                Patients.Remove(SelectedPatient);
                FilterPatients();
                SetStatus("Hasta silindi.", StatusSeverity.Success);
            }
            catch (Exception ex)
            {
                SetStatus($"Silme hatası: {ex.Message}", StatusSeverity.Error);
                MessageBox.Show($"Hasta silinemedi:\n{ex.Message}", "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Forma Yükle ─────────────────────────────────────────────────────────
        private void ExecuteLoadToForm()
        {
            if (SelectedPatient == null) return;
            CurrentPatient = new Patient();
            CurrentPatient.CopyFrom(SelectedPatient);
            SetStatus($"'{SelectedPatient.AdSoyad}' forma yüklendi.", StatusSeverity.Info);
        }

        // ─── Formu Temizle ───────────────────────────────────────────────────────
        private void ExecuteClearForm()
        {
            CurrentPatient = new Patient();
            SetStatus("Form temizlendi.", StatusSeverity.Info);
        }

        // ─── Cihaza Gönder ───────────────────────────────────────────────────────
        private async void ExecuteSendToDevice()
        {
            if (SelectedPatient == null || SelectedDevice == null) return;
            try
            {
                await _deviceCommunicator.SendPatientAsync(SelectedDevice, SelectedPatient);
                string cmd = PatientDeviceCommandBuilder.Build(SelectedPatient);
                SetStatus($"Cihaza gönderildi → {cmd}", StatusSeverity.Success);
                MessageBox.Show($"Cihaza gönderilen komut:\n{cmd}",
                    "Cihaza Gönderildi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetStatus($"Gönderme hatası: {ex.Message}", StatusSeverity.Error);
                MessageBox.Show($"Cihaza gönderilemedi:\n{ex.Message}", "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteSendToDevice()
            => SelectedPatient != null
               && SelectedDevice != null
               && (SelectedDevice.Properties.Status == DeviceStatus.Connected
                   || SelectedDevice.Properties.Status == DeviceStatus.Identified);

        // ─── Excel Dışa Aktar ─────────────────────────────────────────────────────
        private async void ExecuteExportExcel()
        {
            try
            {
                string fileName = $"HastaListesi_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                string filePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    fileName);

                await Task.Run(() => _patientService.ExportToExcel(Patients.ToList(), filePath));
                SetStatus($"Excel kaydedildi: {filePath}", StatusSeverity.Success);

                MessageBox.Show($"Excel dosyası oluşturuldu:\n{filePath}",
                    "Dışa Aktarma", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetStatus("Excel dışa aktarma hatası.", StatusSeverity.Error);
                MessageBox.Show(ex.Message, "Excel Dışa Aktarma Hatası",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Excel İçe Aktar ─────────────────────────────────────────────────────
        private async void ExecuteImportExcel()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title      = "Excel Dosyası Seç",
                Filter     = "Excel Dosyaları|*.xlsx;*.xls|Tüm Dosyalar|*.*",
                DefaultExt = ".xlsx"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var imported = await Task.Run(() => _patientService.ImportFromExcel(dialog.FileName));
                int addedCount = 0;

                foreach (var p in imported)
                {
                    bool exists = !string.IsNullOrWhiteSpace(p.TcKimlik) &&
                                  Patients.Any(e => e.TcKimlik == p.TcKimlik);
                    if (!exists)
                    {
                        _patientService.SavePatient(p);
                        Patients.Add(p);
                        addedCount++;
                    }
                }

                FilterPatients();
                SetStatus($"{addedCount} hasta içe aktarıldı.", StatusSeverity.Success);
                MessageBox.Show($"{addedCount} yeni hasta eklendi.\n{imported.Count - addedCount} kayıt zaten mevcut.",
                    "İçe Aktarma", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetStatus("Excel içe aktarma hatası.", StatusSeverity.Error);
                MessageBox.Show(ex.Message, "Excel İçe Aktarma Hatası",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CurrentPatientOnErrorsChanged(object sender, System.ComponentModel.DataErrorsChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasValidationErrors));
            CommandManager.InvalidateRequerySuggested();
        }

        private void SetStatus(string message, StatusSeverity severity)
        {
            StatusMessage = message;
            StatusSeverity = severity;
        }
    }

    public enum StatusSeverity
    {
        Info,
        Success,
        Error
    }
}
