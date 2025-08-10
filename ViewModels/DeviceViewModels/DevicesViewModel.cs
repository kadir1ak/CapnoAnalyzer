using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Diagnostics;
using System.Threading.Tasks;
using CapnoAnalyzer.Helpers;
using CapnoAnalyzer.Models.Device;
using CapnoAnalyzer.Services;
using System.Collections.Concurrent;
using System.ComponentModel;
using CapnoAnalyzer.Views.DevicesViews.Devices;
using CapnoAnalyzer.Views.DevicesViews.DevicesControl;
using System;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
using MathNet.Numerics.Distributions;
namespace CapnoAnalyzer.ViewModels.DeviceViewModels
{
    public class DevicesViewModel : BindableBase
    {
        private readonly SerialPortsManager PortManager;

        private CancellationTokenSource _updateConnectedDevicesInterfaceLoopCancellationTokenSource;
        private readonly object _ConnectedDevicesInterfaceDataLock = new();
        private int ConnectedDevicesUpdateTimeMillisecond = 100;  // 10 Hz (100ms)

        private CancellationTokenSource _updateIdentifiedDevicesInterfaceLoopCancellationTokenSource;
        private readonly object _IdentifiedDevicesInterfaceDataLock = new();
        private int IdentifiedDevicesUpdateTimeMillisecond = 100;  // 10 Hz (100ms)

        //public CalibrationViewModel CalibrationVM = new CalibrationViewModel();

        // -- 1) Bağlı Cihazların Listesi --
        private ObservableCollection<Device> _connectedDevices = new();
        public ObservableCollection<Device> ConnectedDevices
        {
            get => _connectedDevices;
            set
            {
                _connectedDevices = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<Device> _identifiedDevices = new();
        public ObservableCollection<Device> IdentifiedDevices
        {
            get => _identifiedDevices;
            set
            {
                _identifiedDevices = value;
                OnPropertyChanged();
            }
        }

        // -- 2) Manager'dan gelen port listeleri -- 
        public ObservableCollection<SerialPort> ConnectedPorts => PortManager.ConnectedPorts;
        public ObservableCollection<string> AvailablePorts => PortManager.AvailablePorts;

        // Cihaz ekleme/kaldırma için bir olay tanımlıyoruz
        public event Action<Device> DeviceAdded;
        public event Action<Device> DeviceRemoved;

        // -- 3) Seçili Cihaz Nesnesi --
        private Device _device;
        public Device Device
        {
            get => _device;
            set
            {
                SetProperty(ref _device, value);
                // Command'ların tekrar CanExecute kontrolü yapmasını sağlamak için:
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // -- 4) UI’daki ComboBox’tan seçilen “PortName” --
        private string _selectedPortName;
        public string SelectedPortName
        {
            get => _selectedPortName;
            set
            {
                SetProperty(ref _selectedPortName, value);
                // Port adı değişince Connect butonunu aktif/pasif güncelle:
                DeviceIDUpdate();
                DeviceTestModeUpdate();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _deviceID;
        public string DeviceID
        {
            get => _deviceID;
            set
            {
                SetProperty(ref _deviceID, value);
            }
        }

        private string _dataPacketType;
        public string DataPacketType
        {
            get => _dataPacketType;
            set
            {
                SetProperty(ref _dataPacketType, value);
            }
        }

        private int _selectedBaudRate = 921600;
        public int SelectedBaudRate
        {
            get => _selectedBaudRate;
            set
            {
                SetProperty(ref _selectedBaudRate, value);
                // Port adı değişince Connect butonunu aktif/pasif güncelle:
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // -- 5) Komutlar (Connect/Disconnect/SendMessage) --
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand IdentifyDeviceCommand { get; }

        private readonly SerialPortDataParser _dataParser = new SerialPortDataParser();

        // -- 6) Yapıcı Metot --
        public DevicesViewModel()
        {
            PortManager = new SerialPortsManager();
            // Serial port değişikliklerini dinle
            PortManager.MessageReceivedEx += OnMessageReceivedEx;

            ConnectedDevices = new ObservableCollection<Device>();
            IdentifiedDevices = new ObservableCollection<Device>();
            IdentifiedDevices.CollectionChanged += IdentifiedDevices_CollectionChanged;

            ConnectCommand = new DeviceRelayCommand(ExecuteConnect, CanExecuteConnect);
            DisconnectCommand = new DeviceRelayCommand(ExecuteDisconnect, CanExecuteDisconnect);
            IdentifyDeviceCommand = new DeviceRelayCommand(ExecuteIdentifyDevice, CanExecuteIdentifyDevice);

            // UI Güncelleme Döngüsünü Başlat
            StartConnectedDevicesUpdateInterfaceDataLoop();
            StartIdentifiedDevicesUpdateInterfaceDataLoop();

        }
        private void IdentifiedDevices_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (Device newDevice in e.NewItems)
                {
                    DeviceAdded?.Invoke(newDevice); // Yeni cihaz eklendiğinde tetiklenir
                }
            }

            if (e.OldItems != null)
            {
                foreach (Device removedDevice in e.OldItems)
                {
                    DeviceRemoved?.Invoke(removedDevice); // Cihaz kaldırıldığında tetiklenir
                }
            }
        }
        private void DeviceIDUpdate()
        {
            try
            {
                // Seçili porta göre cihazı bul
                var device = ConnectedDevices.FirstOrDefault(d => d.Properties.PortName == SelectedPortName);

                if (device != null)
                {
                    // Eğer cihaz bulunduysa ve DeviceID bilgisi mevcutsa, DeviceID'yi ata
                    if (!string.IsNullOrEmpty(device.Properties.ProductId))
                    {
                        DeviceID = device.Properties.ProductId;
                    }
                }
                else
                {
                    DeviceID = SelectedPortName;
                }

                // UI'yi güncelle
                OnPropertyChanged(nameof(IdentifiedDevices));
                OnPropertyChanged(nameof(ConnectedDevices));

                Debug.WriteLine($"Device ID Updated: {SelectedPortName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Device ID Update Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeviceTestModeUpdate()
        {
            try
            {
                // Seçili porta göre cihazı bul
                var device = ConnectedDevices.FirstOrDefault(d => d.Properties.PortName == SelectedPortName);

                if (device != null)
                {
                    // Eğer cihaz bulunduysa ve TestMode bilgisi mevcutsa, TestMode'u ata
                    if (!string.IsNullOrEmpty(device.Properties.DataPacketType))
                    {
                        DataPacketType = device.Properties.DataPacketType;
                    }
                }
                else
                {
                    DataPacketType = "1";
                }

                // UI'yi güncelle
                OnPropertyChanged(nameof(IdentifiedDevices));
                OnPropertyChanged(nameof(ConnectedDevices));

                Debug.WriteLine($"Device Test Mode Updated: {SelectedPortName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Device Test Mode Update Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task UpdateConnectedDevicesInterfaceDataLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(ConnectedDevicesUpdateTimeMillisecond, token);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var device in ConnectedDevices.ToList()) // 🔥 Eğer liste değişirse hata almamak için kopyasını al
                        {
                            if (device == null || device.IncomingMessage == null)
                                continue; // 🔥 Null kontrolü ekle

                            var lastMessage = device.IncomingMessage.LastOrDefault();
                            if (lastMessage == null) continue;

                            device.Interface?.IncomingMessage.Add(lastMessage);

                            while (device.Interface?.IncomingMessage.Count > 10)
                            {
                                device.Interface.IncomingMessage.RemoveAt(0);
                            }

                            DeviceIdentification(device);
                        }

                        // **UI güncellemesi**
                        OnPropertyChanged(nameof(IdentifiedDevices));
                        OnPropertyChanged(nameof(ConnectedDevices));
                        OnPropertyChanged(nameof(ConnectedPorts));
                    });
                }
            }
            catch (TaskCanceledException)
            {
                // **Görev iptal edildi, hata fırlatma**
                Debug.WriteLine("UpdateConnectedDevicesInterfaceDataLoop task canceled.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Interface update loop error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void StartConnectedDevicesUpdateInterfaceDataLoop()
        {
            StopConnectedDevicesUpdateInterfaceDataLoop(); // Eski döngüyü durdur
            _updateConnectedDevicesInterfaceLoopCancellationTokenSource = new CancellationTokenSource();
            var token = _updateConnectedDevicesInterfaceLoopCancellationTokenSource.Token;
            _ = UpdateConnectedDevicesInterfaceDataLoop(token);
        }

        public void StopConnectedDevicesUpdateInterfaceDataLoop()
        {
            if (_updateConnectedDevicesInterfaceLoopCancellationTokenSource != null &&
                !_updateConnectedDevicesInterfaceLoopCancellationTokenSource.IsCancellationRequested)
            {
                _updateConnectedDevicesInterfaceLoopCancellationTokenSource.Cancel();
                _updateConnectedDevicesInterfaceLoopCancellationTokenSource.Dispose();
                _updateConnectedDevicesInterfaceLoopCancellationTokenSource = null;
            }
        }
        private async Task UpdateIdentifiedDevicesInterfaceDataLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(IdentifiedDevicesUpdateTimeMillisecond, token);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var device in IdentifiedDevices.ToList()) // 🔥 Kopyasını al, değişiklik olursa hata engellenir.
                        {
                            if (device == null)
                                continue; // 🔥 Null nesneleri atla, hata önle.

                            device.Interface.SyncWithDevice(device);
                        }
                        OnPropertyChanged(nameof(IdentifiedDevices));
                    });
                }
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine("UpdateIdentifiedDevicesInterfaceDataLoop task canceled."); // ❌ Hata yerine debug mesajı.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Interface update loop error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void StartIdentifiedDevicesUpdateInterfaceDataLoop()
        {
            StopIdentifiedDevicesUpdateInterfaceDataLoop(); // Eski döngüyü durdur
            _updateIdentifiedDevicesInterfaceLoopCancellationTokenSource = new CancellationTokenSource();
            var token = _updateIdentifiedDevicesInterfaceLoopCancellationTokenSource.Token;
            _ = UpdateIdentifiedDevicesInterfaceDataLoop(token);
        }

        public void StopIdentifiedDevicesUpdateInterfaceDataLoop()
        {
            if (_updateIdentifiedDevicesInterfaceLoopCancellationTokenSource != null && !_updateIdentifiedDevicesInterfaceLoopCancellationTokenSource.IsCancellationRequested)
            {
                _updateIdentifiedDevicesInterfaceLoopCancellationTokenSource.Cancel();
                _updateIdentifiedDevicesInterfaceLoopCancellationTokenSource.Dispose();
                _updateIdentifiedDevicesInterfaceLoopCancellationTokenSource = null;
            }
        }
        // ========== Gelen Veri Yakalama (Event) ==========
        private void OnMessageReceivedEx(object sender, SerialMessageEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var device = ConnectedDevices.FirstOrDefault(d => d.Properties.PortName == e.PortName);
                if (device == null)
                    return;

                // Son gelen mesajı ekle, en fazla 10 mesajı tut
                device.IncomingMessage.Add(e.Message);
                if (device.IncomingMessage.Count > 10)
                    device.IncomingMessage.RemoveAt(0);

                // Yüksek performanslı parser ile veriyi işle
                _dataParser.TryParsePacket(device, e.Message);

                CalculateSampleRate(device);
            });
        }

        private void CalculateSampleRate(Device device)
        {
            if (device?.Properties == null)
                return;

            device.Properties.SampleCount++;
            var now = DateTime.Now;
            var elapsed = now - device.Properties.LastUpdate;

            // Her 1 saniyede bir örnekleme frekansını güncelle
            if (elapsed.TotalSeconds >= 1)
            {
                device.Properties.DataSamplingFrequency = device.Properties.SampleCount;
                device.Properties.SampleCount = 0;
                device.Properties.LastUpdate = now;
            }
        }

        private void DeviceIdentification(Device device)
        {
            try
            {
                if (device.Properties.Status == DeviceStatus.Identified)
                    return;

                // Null kontrolü
                var lastMessage = device.IncomingMessage.LastOrDefault();
                if (lastMessage == null || string.IsNullOrWhiteSpace(lastMessage))
                    return; // Mesaj yoksa işlemi sonlandır

                string deviceInfo = lastMessage;
                Debug.WriteLine(deviceInfo);
                string[] parts = deviceInfo.Split(';');
                if (parts.Length == 6 &&
                    !string.IsNullOrWhiteSpace(parts[0]) &&
                    !string.IsNullOrWhiteSpace(parts[1]) &&
                    !string.IsNullOrWhiteSpace(parts[2]) &&
                    !string.IsNullOrWhiteSpace(parts[3]) &&
                    !string.IsNullOrWhiteSpace(parts[4]) &&
                    !string.IsNullOrWhiteSpace(parts[5]))
                {
                    // UI iş parçacığında çalıştır
                    if (Application.Current?.Dispatcher.CheckAccess() == true)
                    {
                        AssignDeviceProperties(device, parts);
                    }
                    else
                    {
                        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            AssignDeviceProperties(device, parts);
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing device info: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void AssignDeviceProperties(Device device, string[] parts)
        {
            // Mevcut cihazı IdentifiedDevices koleksiyonunda kontrol et
            var existingDevice = IdentifiedDevices.FirstOrDefault(d => d.Properties.PortName == device.Properties.PortName);

            if (existingDevice != null)
            {
                // Cihaz zaten varsa, özelliklerini güncelle
                existingDevice.Properties.ProductId = parts[4];
                existingDevice.Properties.FirmwareVersion = parts[5];
                existingDevice.Properties.Status = DeviceStatus.Identified;
            }
            else
            {
                // Cihaz yoksa, özelliklerini ata ve koleksiyona ekle
                device.Properties.ProductId = parts[4];
                device.Properties.FirmwareVersion = parts[5];
                device.Properties.Status = DeviceStatus.Identified;

                IdentifiedDevices.Add(device);
            }
            Debug.WriteLine($"Device Identified: {device.Properties.PortName}");
            // UI güncellemesi
            OnPropertyChanged(nameof(IdentifiedDevices));
            OnPropertyChanged(nameof(ConnectedDevices));
        }
        // ========== CONNECT ==========
        private void ExecuteConnect()
        {
            if (string.IsNullOrEmpty(SelectedPortName))
                return;

            // 1) Manager üzerinden ilgili portu aç
            PortManager.ConnectToPort(SelectedPortName, SelectedBaudRate);

            // 2) ConnectedDevices içinde bu porta ait bir cihaz var mı?
            var existingDevice = ConnectedDevices.FirstOrDefault(d => d.Properties.PortName == SelectedPortName);
            if (existingDevice == null)
            {
                // Yoksa yeni bir device oluşturup ekleyin:
                var newDevice = new Device(PortManager, SelectedPortName, deviceStatus: DeviceStatus.Connected);
                newDevice.Properties.BaudRate = SelectedBaudRate;
                ConnectedDevices.Add(newDevice);
                Device = newDevice;
            }
            else
            {
                // Varsa yalnızca IsConnected durumunu güncelle
                existingDevice.Properties.Status = DeviceStatus.Connected;
                Device = existingDevice;
            }
            Debug.WriteLine($"ConnectedDevices: {Device.Properties.PortName}");

            // UI'yi yeniden tetikle
            OnPropertyChanged(nameof(ConnectedDevices));
            OnPropertyChanged(nameof(ConnectedPorts));
        }

        // Butonun aktif olması için: Sadece Port seçiliyse
        private bool CanExecuteConnect() => !string.IsNullOrEmpty(SelectedPortName);

        // ========== DISCONNECT ==========
        private async void ExecuteDisconnect()
        {
            // İlk olarak IdentifiedDevices içerisindeki cihazı kontrol et
            var identifiedDevice = IdentifiedDevices.FirstOrDefault(d => d.Properties.PortName == SelectedPortName);
            if (identifiedDevice != null)
            {
                // PortName boşsa işlem yapma
                if (!string.IsNullOrEmpty(identifiedDevice.Properties.PortName))
                {
                    // Manager'dan portu kapat (arka planda çalıştır)
                    await Task.Run(() => PortManager.DisconnectFromPort(identifiedDevice.Properties.PortName));

                    // Cihazın durumunu güncelle
                    identifiedDevice.Properties.Status = DeviceStatus.Disconnected;
                    identifiedDevice.StopAutoSend();

                    // IdentifiedDevices'tan cihazı çıkar
                    IdentifiedDevices.Remove(identifiedDevice);

                    // UI'yi güncelle
                    OnPropertyChanged(nameof(IdentifiedDevices));
                }
            }

            // Daha sonra ConnectedDevices içerisindeki cihazı kontrol et
            var connectedDevice = ConnectedDevices.FirstOrDefault(d => d.Properties.PortName == SelectedPortName);
            if (connectedDevice != null)
            {
                // PortName boşsa işlem yapma
                if (!string.IsNullOrEmpty(connectedDevice.Properties.PortName))
                {
                    // Manager'dan portu kapat (arka planda çalıştır)
                    await Task.Run(() => PortManager.DisconnectFromPort(connectedDevice.Properties.PortName));

                    // Cihazın durumunu güncelle
                    connectedDevice.Properties.Status = DeviceStatus.Disconnected;
                    connectedDevice.StopAutoSend();

                    // ConnectedDevices'tan cihazı çıkar
                    ConnectedDevices.Remove(connectedDevice);

                    // UI'yi güncelle
                    OnPropertyChanged(nameof(ConnectedDevices));
                }
            }
            Debug.WriteLine($"Disconnected: {SelectedPortName}");
            // Seçili cihazı sıfırla (UI'da buton vs. güncellenecek)
            Device = null;
            OnPropertyChanged(nameof(ConnectedPorts));
        }

        // Butonun aktif olması için: Seçili bir Device ve Port’u dolu olmalı
        private bool CanExecuteDisconnect()
        {
            // Seçili port adına sahip, IsConnected=true durumda bir Device var mı?
            return !string.IsNullOrEmpty(SelectedPortName) && ConnectedDevices.Any(d => d.Properties.PortName == SelectedPortName && (d.Properties.Status == DeviceStatus.Connected || d.Properties.Status == DeviceStatus.Identified));
        }
        // ========== DISCONNECT ==========
        private async void ExecuteIdentifyDevice()
        {
            // İlk olarak IdentifiedDevices içerisindeki cihazı kontrol et
            var identifiedDevice = ConnectedDevices.FirstOrDefault(d => d.Properties.PortName == SelectedPortName);
            if (identifiedDevice != null)
            {
                // PortName boşsa işlem yapma
                if (!string.IsNullOrEmpty(identifiedDevice.Properties.PortName))
                {
                    // Cihaz zaten varsa, özelliklerini güncelle
                    identifiedDevice.Properties.ProductId = DeviceID;
                    identifiedDevice.Properties.BaudRate = SelectedBaudRate;
                    identifiedDevice.Properties.DataPacketType = DataPacketType;
                    identifiedDevice.Properties.FirmwareVersion = "Null";
                    identifiedDevice.Properties.Status = DeviceStatus.Identified;

                    // IdentifiedDevices'tan cihazı çıkar
                    IdentifiedDevices.Add(identifiedDevice);

                    // UI'yi güncelle
                    OnPropertyChanged(nameof(IdentifiedDevices));
                    OnPropertyChanged(nameof(ConnectedDevices));
                }
            }
            Debug.WriteLine($"Identify Device: {SelectedPortName}");
        }

        // Butonun aktif olması için: Seçili bir Device olmalı (SelectedPortName boş olmamalı).
        // Seçili porta bağlı bir cihaz olmalı(ConnectedDevices listesinde, IsConnected == true olan bir cihaz bulunmalı).
        // Bu cihaz, IdentifiedDevices listesinde olmamalı.
        private bool CanExecuteIdentifyDevice()
        {
            // Seçili port adı boş mu?
            if (string.IsNullOrEmpty(SelectedPortName))
                return false;

            // Seçili porta bağlı bir cihaz var mı ve bu cihaz IdentifiedDevices listesinde değil mi?
            return ConnectedDevices.Any(connectedDevice =>
                connectedDevice.Properties.PortName == SelectedPortName &&
                connectedDevice.Properties.Status == DeviceStatus.Connected &&
                !IdentifiedDevices.Any(identifiedDevice =>
                    identifiedDevice.Properties.PortName == SelectedPortName));
        }


        // ========== Cihazın Durumunu Bildir ==========
        public DeviceStatus? IsDeviceStatus(string portName)
        {
            var device = ConnectedDevices.FirstOrDefault(d => d.Properties.PortName == portName);
            return device?.Properties.Status;
        }

        // ========== Port Bağlı mı Kontrol ==========
        public bool IsPortConnected(string portName)
        {
            return PortManager.ConnectedPorts.Any(sp => sp.PortName == portName);
        }
        public void CloseAllPorts()
        {
            foreach (var device in ConnectedDevices)
            {
                device.StopAutoSend(); // Otomatik veri göndermeyi durdur
                PortManager.DisconnectFromPort(device.Properties.PortName); // Tüm portları kapat
            }

            // Portları yönetim nesnesinden de temizle
            PortManager.ConnectedPorts.Clear();
        }
    }
}
